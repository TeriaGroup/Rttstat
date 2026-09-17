namespace Netpulse.Core.Models;

public sealed class MonitorSnapshot
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Profile? Profile { get; init; }
    public LinkQuality Quality { get; init; }
    public string StatusText { get; init; } = "Starting";
    public IReadOnlyList<TargetLiveState> Targets { get; init; } = [];
    public double? AggregatePingMs { get; init; }
    public double? AggregateMinMs { get; init; }
    public double? AggregateAvgMs { get; init; }
    public double? AggregateMaxMs { get; init; }
    public double AggregateJitterMs { get; init; }
    public double Loss1m { get; init; }
    public double Loss5m { get; init; }
    public double Loss1h { get; init; }
    public double RecvBps { get; init; }
    public double SentBps { get; init; }
    public string AdapterName { get; init; } = "";
    public string AdapterId { get; init; } = "";
    public bool AdapterUp { get; init; }
    public Outage? OpenOutage { get; init; }
    public TimeSpan OnlineFor { get; init; }
    public bool Paused { get; init; }
    public bool IcmpBlocked { get; init; }
    public SpeedtestPhase SpeedtestPhase { get; init; }
    public double? SpeedtestLiveMbps { get; init; }
    public SpeedtestResult? LastSpeedtest { get; init; }
    public IReadOnlyList<double> PingSpark { get; init; } = [];
    public IReadOnlyList<double> LossSpark { get; init; } = [];
    public IReadOnlyList<double> DownSpark { get; init; } = [];
    public IReadOnlyList<double> UpSpark { get; init; } = [];
    public string Banner { get; init; } = "";
}
