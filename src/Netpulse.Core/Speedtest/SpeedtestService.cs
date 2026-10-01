using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Netpulse.Core.Abstractions;
using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;

namespace Netpulse.Core.Speedtest;

public sealed class SpeedtestService : BackgroundService
{
    private readonly LibreSpeedClient _client;
    private readonly MonitorHub _hub;
    private readonly ISettingsProvider _settings;
    private readonly IProfileProvider _profiles;
    private readonly ILogger<SpeedtestService> _log;
    private CancellationTokenSource? _runCts;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastScheduled = DateTimeOffset.MinValue;

    public SpeedtestService(
        HttpClient http,
        MonitorHub hub,
        ISettingsProvider settings,
        IProfileProvider profiles,
        ILogger<SpeedtestService> log)
    {
        _client = new LibreSpeedClient(http);
        _hub = hub;
        _settings = settings;
        _profiles = profiles;
        _log = log;
    }

    public bool IsRunning => _runCts is not null;

    public async Task RunNowAsync(bool ignoreMetered = false)
    {
        var cfg = _settings.Current.LibreSpeed;
        if (!ignoreMetered && !cfg.AllowMetered && GatewayDetector.IsMetered())
        {
            _hub.EmitEvent(EventLevel.Warn, EventCategory.Speedtest, "Skipped: metered connection");
            return;
        }

        if (!await _gate.WaitAsync(0))
            return;

        _runCts = new CancellationTokenSource();
        try
        {
            _hub.EmitEvent(EventLevel.Info, EventCategory.Speedtest, "Speed test started");
            var idle = _hub.Current.AggregatePingMs;
            var downLoad = new List<double>();
            var upLoad = new List<double>();
            var progress = new Progress<SpeedtestProgress>(p =>
            {
                var cur = _hub.Current;
                if (cur.AggregatePingMs is { } ms)
                {
                    if (p.Phase == SpeedtestPhase.Downloading) downLoad.Add(ms);
                    if (p.Phase == SpeedtestPhase.Uploading) upLoad.Add(ms);
                }
                _hub.Publish(CloneWithSpeed(cur, p.Phase, p.LiveMbps, cur.LastSpeedtest));
            });
            var result = await _client.RunAsync(cfg, _profiles.Active.Id, _hub.Current.AdapterId, progress, _runCts.Token);
            result.IdlePingMs = idle;
            result.DownLoadPingMs = downLoad.Count == 0 ? null : downLoad.Average();
            result.UpLoadPingMs = upLoad.Count == 0 ? null : upLoad.Average();
            var loaded = Math.Max(result.DownLoadPingMs ?? 0, result.UpLoadPingMs ?? 0);
            if (idle is { } i && loaded > 0)
                result.BloatGrade = RoutePolicy.BufferbloatGrade(i, loaded);
            _hub.EmitSpeedtest(result);
            if (result.BloatGrade is "D" or "E" or "F")
                _hub.EmitEvent(EventLevel.Warn, EventCategory.Speedtest, "Bufferbloat " + result.BloatGrade);
            var msg = result.Error is null
                ? $"Down {result.DownloadMbps:0.00} Mbps  Up {result.UploadMbps:0.00} Mbps  ping {result.PingMs:0} ms  bloat {result.BloatGrade}"
                : "Speed test error: " + result.Error;
            _hub.EmitEvent(result.Error is null ? EventLevel.Info : EventLevel.Error, EventCategory.Speedtest, msg);
            _hub.Publish(CloneWithSpeed(_hub.Current, SpeedtestPhase.Idle, null, result));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Speedtest failed");
            _hub.EmitEvent(EventLevel.Error, EventCategory.Speedtest, ex.Message);
        }
        finally
        {
            _runCts.Dispose();
            _runCts = null;
            _gate.Release();
        }
    }

    public void Cancel() => _runCts?.Cancel();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var schedule = _settings.Current.LibreSpeed.Schedule;
            if (schedule is "Off" or "" or null) continue;
            if (_hub.Current.OpenOutage is { IsOpen: true }) continue;
            if (_hub.Paused) continue;
            var now = DateTimeOffset.Now;
            var due = false;
            if (schedule.StartsWith("Every ", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(schedule.Split(' ').ElementAtOrDefault(1), out var hours))
            {
                due = now - _lastScheduled >= TimeSpan.FromHours(Math.Max(1, hours));
            }
            else if (TimeSpan.TryParse(schedule, out var at))
            {
                due = now.TimeOfDay.Hours == at.Hours && now.TimeOfDay.Minutes == at.Minutes
                      && now.Date != _lastScheduled.Date;
            }
            if (!due) continue;
            _lastScheduled = now;
            await RunNowAsync();
        }
    }

    private static MonitorSnapshot CloneWithSpeed(MonitorSnapshot cur, SpeedtestPhase phase, double? live, SpeedtestResult? last)
        => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Profile = cur.Profile,
            Quality = cur.Quality,
            StatusText = cur.StatusText,
            Targets = cur.Targets,
            AggregatePingMs = cur.AggregatePingMs,
            AggregateMinMs = cur.AggregateMinMs,
            AggregateAvgMs = cur.AggregateAvgMs,
            AggregateMaxMs = cur.AggregateMaxMs,
            AggregateJitterMs = cur.AggregateJitterMs,
            Loss1m = cur.Loss1m,
            Loss5m = cur.Loss5m,
            Loss1h = cur.Loss1h,
            RecvBps = cur.RecvBps,
            SentBps = cur.SentBps,
            AdapterName = cur.AdapterName,
            AdapterId = cur.AdapterId,
            AdapterUp = cur.AdapterUp,
            OpenOutage = cur.OpenOutage,
            OnlineFor = cur.OnlineFor,
            Paused = cur.Paused,
            IcmpBlocked = cur.IcmpBlocked,
            SpeedtestPhase = phase,
            SpeedtestLiveMbps = live,
            LastSpeedtest = last ?? cur.LastSpeedtest,
            PingSpark = cur.PingSpark,
            LossSpark = cur.LossSpark,
            DownSpark = cur.DownSpark,
            UpSpark = cur.UpSpark,
            Banner = cur.Banner
        };
}
