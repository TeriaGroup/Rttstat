using Netpulse.Core.Models;

namespace Netpulse.Core.Monitoring;

public sealed class MonitorHub
{
    private readonly object _gate = new();
    private MonitorSnapshot _current = new();
    private RouteSnapshot _route = new();
    private volatile bool _paused;

    public event Action<MonitorSnapshot>? Updated;
    public event Action<RouteSnapshot>? RouteUpdated;
    public event Action<RouteSample>? RouteSampleProduced;
    public event Action<PingSample>? PingSampleProduced;
    public event Action<NicSample>? NicSampleProduced;
    public event Action<Outage>? OutageChanged;
    public event Action<AppEvent>? EventRaised;
    public event Action<SpeedtestResult>? SpeedtestCompleted;

    public MonitorSnapshot Current
    {
        get { lock (_gate) return _current; }
    }

    public RouteSnapshot CurrentRoute
    {
        get { lock (_gate) return _route; }
    }

    public bool Paused
    {
        get => _paused;
        set => _paused = value;
    }

    public void Publish(MonitorSnapshot snapshot)
    {
        lock (_gate) _current = snapshot;
        Updated?.Invoke(snapshot);
    }

    public void EmitPing(PingSample sample) => PingSampleProduced?.Invoke(sample);
    public void EmitNic(NicSample sample) => NicSampleProduced?.Invoke(sample);
    public void EmitOutage(Outage outage) => OutageChanged?.Invoke(outage);
    public void EmitEvent(EventLevel level, EventCategory category, string message)
        => EventRaised?.Invoke(new AppEvent
        {
            Ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Level = level,
            Category = category,
            Message = message
        });
    public void EmitSpeedtest(SpeedtestResult result) => SpeedtestCompleted?.Invoke(result);
    public void EmitRoute(RouteSample sample) => RouteSampleProduced?.Invoke(sample);
    public void PublishRoute(RouteSnapshot snapshot)
    {
        lock (_gate) _route = snapshot;
        RouteUpdated?.Invoke(snapshot);
    }
}
