using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Netpulse.Core.Abstractions;
using Netpulse.Core.Models;

namespace Netpulse.Core.Monitoring;

public sealed class MonitorLoopService : BackgroundService
{
    public const int ChartWindowSec = 120;
    private const int ChartPoints = 120;
    private readonly MonitorHub _hub;
    private readonly IProfileProvider _profiles;
    private readonly ISettingsProvider _settings;
    private readonly ILogger<MonitorLoopService> _log;
    private readonly PingEngine _ping = new();
    private readonly NicSampler _nic = new();
    private readonly OutageTracker _outages = new();
    private readonly Dictionary<Guid, RollingWindow> _windows = [];
    private readonly Dictionary<Guid, TargetLiveState> _live = [];
    private readonly Queue<double> _downSpark = new();
    private readonly Queue<double> _upSpark = new();
    private DateTimeOffset _onlineSince = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastQualityToast = DateTimeOffset.MinValue;
    private Guid _lastProfileId;
    private string _lastAdapterId = "";
    private bool _icmpBanner;

    public MonitorLoopService(
        MonitorHub hub,
        IProfileProvider profiles,
        ISettingsProvider settings,
        ILogger<MonitorLoopService> log)
    {
        _hub = hub;
        _profiles = profiles;
        _settings = settings;
        _log = log;
        _profiles.Changed += () => { };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hub.EmitEvent(EventLevel.Info, EventCategory.App, "Monitor started");
        PreferPhysicalAdapter();
        ApplyGateway();
        while (!stoppingToken.IsCancellationRequested)
        {
            var profile = _profiles.Active;
            var interval = Math.Clamp(profile.PingIntervalMs, 200, 60_000);
            try
            {
                await TickAsync(profile, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Monitor tick failed");
                _hub.EmitEvent(EventLevel.Error, EventCategory.Ping, "Monitor tick failed: " + ex.Message);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var closed = _outages.CloseOnExit();
        if (closed is not null)
            _hub.EmitOutage(closed);
        _hub.EmitEvent(EventLevel.Info, EventCategory.App, "Monitor stopped");
    }

    private async Task TickAsync(Profile profile, CancellationToken ct)
    {
        if (profile.Id != _lastProfileId)
        {
            _windows.Clear();
            _live.Clear();
            _outages.Reset();
            _lastProfileId = profile.Id;
            ApplyGateway();
        }

        var s = _settings.Current;
        var nic = _nic.Sample(s.SelectedAdapterId, s.AdapterUserChosen);
        if (!string.IsNullOrEmpty(nic.Id) && nic.Id != _lastAdapterId)
        {
            if (_lastAdapterId != "")
                _hub.EmitEvent(EventLevel.Info, EventCategory.Adapter, $"Adapter changed to '{nic.Name}'");
            _lastAdapterId = nic.Id;
            if (!s.AdapterUserChosen && s.SelectedAdapterId != nic.Id)
            {
                s.SelectedAdapterId = nic.Id;
                _settings.Save();
            }
            ApplyGateway();
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _hub.EmitNic(new NicSample { Ts = nowMs, AdapterId = nic.Id, RecvBps = nic.RecvBps, SentBps = nic.SentBps });
        _downSpark.Enqueue(nic.RecvBps);
        _upSpark.Enqueue(nic.SentBps);
        while (_downSpark.Count > ChartPoints) _downSpark.Dequeue();
        while (_upSpark.Count > ChartPoints) _upSpark.Dequeue();

        if (_hub.Paused)
        {
            Publish(profile, nic, true);
            return;
        }

        var targets = profile.EnabledTargets.Take(8).ToList();
        var tasks = targets.Select(t => ProbeTargetAsync(profile, t, nowMs, ct)).ToList();
        await Task.WhenAll(tasks);

        foreach (var t in targets)
        {
            if (!_live.ContainsKey(t.Id)) continue;
            if (!_windows.TryGetValue(t.Id, out var w)) continue;
            var st = _live[t.Id];
            var s1 = w.Stats(nowMs, TimeSpan.FromMinutes(1));
            var s5 = w.Stats(nowMs, TimeSpan.FromMinutes(5));
            var sH = w.Stats(nowMs, TimeSpan.FromHours(1));
            st.Loss1m = s1.LossPercent;
            st.Loss5m = s5.LossPercent;
            st.Loss1h = sH.LossPercent;
            st.AvgRtt5m = s5.AvgRtt;
            st.MinRtt5m = s5.MinRtt;
            st.MaxRtt5m = s5.MaxRtt;
            st.JitterMs = s1.Jitter;
        }

        var liveList = targets.Select(t => _live.GetValueOrDefault(t.Id)).Where(x => x is not null).Cast<TargetLiveState>().ToList();
        var prev = _outages.Current;
        var outage = _outages.OnTick(profile, nic.Up, liveList, nic.Id);
        if (outage is not null && (prev is null || prev.Id != outage.Id || prev.IsOpen != outage.IsOpen || prev.EndedAtUtc != outage.EndedAtUtc))
            _hub.EmitOutage(outage);

        if (outage is { IsOpen: true })
            _onlineSince = DateTimeOffset.UtcNow;

        Publish(profile, nic, false);
        RaiseThresholds(profile, liveList);
    }

    private async Task ProbeTargetAsync(Profile profile, Target target, long nowMs, CancellationToken ct)
    {
        if (!_live.TryGetValue(target.Id, out var state))
        {
            state = new TargetLiveState
            {
                TargetId = target.Id,
                DisplayName = target.DisplayName,
                Host = target.Host,
                Role = target.Role
            };
            _live[target.Id] = state;
        }

        var forceTcp = state.UsingTcp || state.ConsecutiveIcmpErrors >= 10;
        var result = await _ping.ProbeAsync(target, profile, forceTcp, ct);
        if (result.IcmpError)
        {
            state.ConsecutiveIcmpErrors++;
            if (state.ConsecutiveIcmpErrors >= 10 && !state.UsingTcp && profile.Protocol != PingProtocol.Icmp)
            {
                state.UsingTcp = true;
                _icmpBanner = true;
                _hub.EmitEvent(EventLevel.Warn, EventCategory.Ping,
                    $"ICMP ping blocked. Switching to TCP connect on port {profile.TcpPort}.");
            }
        }
        else if (result.Ok)
            state.ConsecutiveIcmpErrors = 0;

        state.LastOk = result.Ok;
        state.LastRttMs = result.Rtt;
        state.LastStatus = result.Status;
        state.ResolvedIp = result.Resolved;
        if (result.Ok) state.ConsecutiveFails = 0;
        else state.ConsecutiveFails++;

        if (!_windows.TryGetValue(target.Id, out var w))
        {
            w = new RollingWindow(TimeSpan.FromHours(1.1));
            _windows[target.Id] = w;
        }
        w.Add(nowMs, result.Ok, result.Rtt);

        _hub.EmitPing(new PingSample
        {
            Ts = nowMs,
            ProfileId = profile.Id,
            TargetId = target.Id,
            RttMs = result.Rtt,
            Ok = result.Ok,
            Status = result.Status,
            TargetName = target.DisplayName,
            Role = target.Role
        });
    }

    private void Publish(Profile profile, (bool Up, double RecvBps, double SentBps, string Name, string Id) nic, bool paused)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var live = profile.EnabledTargets
            .Select(t => _live.GetValueOrDefault(t.Id))
            .Where(x => x is not null)
            .Cast<TargetLiveState>()
            .ToList();

        var ext = live.Where(t => t.Role is TargetRole.External or TargetRole.Dns).ToList();
        var pingSrc = ext.Count > 0 ? ext : live;
        var okPings = pingSrc.Where(t => t.LastOk && t.LastRttMs is not null).Select(t => t.LastRttMs!.Value).ToList();
        double? agg = okPings.Count > 0 ? okPings.Average() : null;
        var loss1 = pingSrc.Count == 0 ? 0 : pingSrc.Average(t => t.Loss1m);
        var loss5 = pingSrc.Count == 0 ? 0 : pingSrc.Average(t => t.Loss5m);
        var lossH = pingSrc.Count == 0 ? 0 : pingSrc.Average(t => t.Loss1h);
        var jitter = pingSrc.Count == 0 ? 0 : pingSrc.Average(t => t.JitterMs);
        var min = pingSrc.Select(t => t.MinRtt5m).Where(v => v is not null).Select(v => v!.Value).DefaultIfEmpty().Min();
        var max = pingSrc.Select(t => t.MaxRtt5m).Where(v => v is not null).Select(v => v!.Value).DefaultIfEmpty().Max();
        var avg5 = pingSrc.Select(t => t.AvgRtt5m).Where(v => v is not null).Select(v => v!.Value).DefaultIfEmpty().Average();

        var (q, text) = LinkQualityEvaluator.Evaluate(paused, nic.Up, _outages.Current is { IsOpen: true }, live, profile);
        if (q is LinkQuality.Down) _onlineSince = DateTimeOffset.UtcNow;

        var sparkTarget = pingSrc.FirstOrDefault();
        IReadOnlyList<double> pingSpark = [];
        IReadOnlyList<double> lossSpark = [];
        if (sparkTarget is not null && _windows.TryGetValue(sparkTarget.TargetId, out var w))
        {
            var win = TimeSpan.FromSeconds(ChartWindowSec);
            pingSpark = w.Spark(nowMs, win, ChartPoints, true);
            lossSpark = w.Spark(nowMs, win, ChartPoints, false);
        }

        var snap = new MonitorSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Profile = profile,
            Quality = q,
            StatusText = text,
            Targets = live,
            AggregatePingMs = agg,
            AggregateMinMs = pingSrc.Count == 0 ? null : min,
            AggregateAvgMs = pingSrc.Count == 0 ? null : avg5,
            AggregateMaxMs = pingSrc.Count == 0 ? null : max,
            AggregateJitterMs = jitter,
            Loss1m = loss1,
            Loss5m = loss5,
            Loss1h = lossH,
            RecvBps = nic.RecvBps,
            SentBps = nic.SentBps,
            AdapterName = nic.Name,
            AdapterId = nic.Id,
            AdapterUp = nic.Up,
            OpenOutage = _outages.Current is { IsOpen: true } ? _outages.Current : null,
            OnlineFor = DateTimeOffset.UtcNow - _onlineSince,
            Paused = paused,
            IcmpBlocked = _icmpBanner,
            SpeedtestPhase = _hub.Current.SpeedtestPhase,
            SpeedtestLiveMbps = _hub.Current.SpeedtestLiveMbps,
            LastSpeedtest = _hub.Current.LastSpeedtest,
            PingSpark = pingSpark,
            LossSpark = lossSpark,
            DownSpark = _downSpark.ToArray(),
            UpSpark = _upSpark.ToArray(),
            Banner = _icmpBanner ? $"ICMP blocked; using TCP port {profile.TcpPort}." : ""
        };
        _hub.Publish(snap);
    }

    private void RaiseThresholds(Profile profile, List<TargetLiveState> live)
    {
        if (DateTimeOffset.UtcNow - _lastQualityToast < TimeSpan.FromMinutes(10)) return;
        foreach (var t in live.Where(t => t.Role is TargetRole.External or TargetRole.Dns))
        {
            if (t.LastRttMs >= profile.PingWarnMs)
            {
                _hub.EmitEvent(EventLevel.Warn, EventCategory.Threshold, $"{t.DisplayName} ping {t.LastRttMs:0} ms");
                _lastQualityToast = DateTimeOffset.UtcNow;
                return;
            }
            if (t.Loss5m >= profile.LossWarnPercent)
            {
                _hub.EmitEvent(EventLevel.Warn, EventCategory.Threshold, $"{t.DisplayName} loss {t.Loss5m:0.0}%");
                _lastQualityToast = DateTimeOffset.UtcNow;
                return;
            }
        }
    }

    private void PreferPhysicalAdapter()
    {
        var s = _settings.Current;
        if (s.AdapterUserChosen) return;
        var best = GatewayDetector.PickDefault(null, false);
        if (best is null) return;
        if (s.SelectedAdapterId == best.Id) return;
        s.SelectedAdapterId = best.Id;
        _settings.Save();
        _hub.EmitEvent(EventLevel.Info, EventCategory.Adapter, $"Using '{best.Name}' instead of VPN/virtual adapter");
    }

    private void ApplyGateway()
    {
        var s = _settings.Current;
        var adapter = GatewayDetector.PickDefault(s.SelectedAdapterId, s.AdapterUserChosen);
        if (adapter is null || string.IsNullOrEmpty(adapter.Gateway)) return;
        var changed = false;
        foreach (var p in _profiles.All)
        {
            var gw = p.Targets.FirstOrDefault(t => t.Role == TargetRole.Gateway);
            if (gw is null) continue;
            if (gw.Host != adapter.Gateway)
            {
                gw.Host = adapter.Gateway;
                gw.Enabled = true;
                changed = true;
                _hub.EmitEvent(EventLevel.Info, EventCategory.Adapter, $"Gateway set to {adapter.Gateway}");
            }
        }
        if (changed) _profiles.Save();
    }

    public override void Dispose()
    {
        _ping.Dispose();
        base.Dispose();
    }
}
