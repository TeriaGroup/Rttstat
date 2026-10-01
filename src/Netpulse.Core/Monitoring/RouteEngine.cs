using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Netpulse.Core.Abstractions;
using Netpulse.Core.Models;

namespace Netpulse.Core.Monitoring;

public sealed class RouteEngine : BackgroundService
{
    private readonly MonitorHub _hub;
    private readonly IProfileProvider _profiles;
    private readonly ISettingsProvider _settings;
    private readonly ILogger<RouteEngine> _log;
    private readonly Dictionary<int, HopAccumulator> _acc = [];
    private readonly Queue<List<double?>> _heat = new();
    private List<string> _lastIps = [];
    private int _knownHops = 8;
    private Guid _targetId;
    private readonly byte[] _payload = new byte[32];

    public RouteEngine(MonitorHub hub, IProfileProvider profiles, ISettingsProvider settings, ILogger<RouteEngine> log)
    {
        _hub = hub;
        _profiles = profiles;
        _settings = settings;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = Math.Clamp(_profiles.Active.PingIntervalMs, 500, 10_000);
            try
            {
                if (!_hub.Paused)
                    await RoundAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Route round failed");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RoundAsync(CancellationToken ct)
    {
        var profile = _profiles.Active;
        var target = profile.EnabledTargets.FirstOrDefault(t => t.Role == TargetRole.External)
                     ?? profile.EnabledTargets.FirstOrDefault(t => t.Role == TargetRole.Dns)
                     ?? profile.EnabledTargets.FirstOrDefault();
        if (target is null) return;
        if (target.Id != _targetId)
        {
            _targetId = target.Id;
            _acc.Clear();
            _heat.Clear();
            _lastIps = [];
            _knownHops = 8;
        }

        IPAddress? ip;
        try
        {
            if (!IPAddress.TryParse(target.Host, out ip))
            {
                var entry = await Dns.GetHostEntryAsync(target.Host, ct);
                ip = entry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            }
        }
        catch
        {
            Publish(target, true, "dns");
            return;
        }

        if (ip is null || ip.AddressFamily != AddressFamily.InterNetwork)
        {
            Publish(target, false, "ipv4-only");
            return;
        }

        var maxTtl = Math.Clamp(_settings.Current.MaxTtl, 5, 30);
        var timeout = Math.Clamp(profile.TimeoutMs, 100, 3000);
        var probeTo = Math.Min(_knownHops + 1, maxTtl);
        var hops = new TtlReply[probeTo];
        var reached = false;

        for (var ttl = 1; ttl <= probeTo; ttl++)
        {
            ct.ThrowIfCancellationRequested();
            hops[ttl - 1] = await Task.Run(() => IcmpTtl.Ping(ip, (byte)ttl, timeout, _payload), ct);
            if (hops[ttl - 1] is { Ok: true, TtlExpired: false, Status: "ok" })
            {
                reached = true;
                _knownHops = ttl;
                break;
            }
            if (hops[ttl - 1].TtlExpired)
                _knownHops = Math.Max(_knownHops, ttl + 1);
        }

        if (!reached && _knownHops < maxTtl)
            _knownHops = Math.Min(maxTtl, _knownHops + 1);

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var states = new List<HopLiveState>();
        var heatCol = new List<double?>();
        var ips = new List<string>();

        for (var i = 0; i < hops.Length; i++)
        {
            if (reached && i >= _knownHops) break;
            var h = hops[i];
            if (!_acc.TryGetValue(i + 1, out var acc))
            {
                acc = new HopAccumulator();
                _acc[i + 1] = acc;
            }
            var okHop = h.Ok || h.TtlExpired;
            acc.Add(okHop && h.Ip.Length > 0, h.RttMs);
            var st = acc.Stats();
            var ipStr = string.IsNullOrEmpty(h.Ip) ? "*" : h.Ip;
            ips.Add(ipStr);
            var hopState = new HopLiveState
            {
                Hop = i + 1,
                Ip = ipStr,
                LastOk = okHop,
                LastRttMs = h.RttMs,
                AvgRtt = st.avg,
                MinRtt = st.min,
                MaxRtt = st.max,
                Jitter = st.jitter,
                LossPercent = st.loss,
                Sent = st.sent,
                Recv = st.recv,
                Status = h.Status,
                Spark = st.spark
            };
            states.Add(hopState);
            heatCol.Add(h.RttMs);
            _hub.EmitRoute(new RouteSample
            {
                Ts = ts,
                ProfileId = profile.Id,
                TargetId = target.Id,
                Hop = i + 1,
                Ip = string.IsNullOrEmpty(h.Ip) ? null : h.Ip,
                RttMs = h.RttMs,
                Ok = okHop && h.Ip.Length > 0,
                Status = h.Status
            });
        }

        var destLoss = states.Count == 0 ? 100 : states[^1].LossPercent;
        foreach (var s in states)
            s.IntermediateOnlyLoss = s.LossPercent >= 50 && destLoss < 20 && s.Hop < states.Count;

        if (RoutePolicy.IsRouteChange(_lastIps, ips) && _lastIps.Count > 0)
            _hub.EmitEvent(EventLevel.Warn, EventCategory.Route, "Route changed");
        _lastIps = ips;

        _heat.Enqueue(heatCol);
        while (_heat.Count > 60) _heat.Dequeue();

        if (_settings.Current.ReverseDns)
            _ = ResolveNamesAsync(states);

        var heat = Transpose(_heat);
        _hub.PublishRoute(new RouteSnapshot
        {
            TargetId = target.Id,
            TargetHost = target.Host,
            Tracing = !reached,
            Banner = reached ? "" : "tracing",
            Hops = states,
            Heat = heat
        });
    }

    private static async Task ResolveNamesAsync(List<HopLiveState> hops)
    {
        foreach (var h in hops)
        {
            if (h.Ip is "*" or "") continue;
            try
            {
                var e = await Dns.GetHostEntryAsync(h.Ip);
                h.Name = e.HostName;
            }
            catch { /* ignore */ }
        }
    }

    private static IReadOnlyList<IReadOnlyList<double?>> Transpose(Queue<List<double?>> cols)
    {
        var list = cols.ToList();
        if (list.Count == 0) return [];
        var rows = list.Max(c => c.Count);
        var heat = new List<IReadOnlyList<double?>>();
        for (var r = 0; r < rows; r++)
        {
            var row = new double?[list.Count];
            for (var c = 0; c < list.Count; c++)
                row[c] = r < list[c].Count ? list[c][r] : null;
            heat.Add(row);
        }
        return heat;
    }

    private void Publish(Target target, bool tracing, string banner)
        => _hub.PublishRoute(new RouteSnapshot { TargetId = target.Id, TargetHost = target.Host, Tracing = tracing, Banner = banner });
}
