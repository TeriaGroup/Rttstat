namespace Netpulse.Core.Models;

public sealed class HopLiveState
{
    public int Hop { get; init; }
    public string Ip { get; set; } = "";
    public string Name { get; set; } = "";
    public bool LastOk { get; set; }
    public double? LastRttMs { get; set; }
    public double? AvgRtt { get; set; }
    public double? MinRtt { get; set; }
    public double? MaxRtt { get; set; }
    public double Jitter { get; set; }
    public double LossPercent { get; set; }
    public int Sent { get; set; }
    public int Recv { get; set; }
    public string Status { get; set; } = "";
    public bool IntermediateOnlyLoss { get; set; }
    public string Note { get; set; } = "";
    public IReadOnlyList<double> Spark { get; set; } = [];
}

public sealed class RouteSample
{
    public long Ts { get; init; }
    public Guid ProfileId { get; init; }
    public Guid TargetId { get; init; }
    public int Hop { get; init; }
    public string? Ip { get; init; }
    public double? RttMs { get; init; }
    public bool Ok { get; init; }
    public string Status { get; init; } = "";
}

public sealed class RouteSnapshot
{
    public Guid TargetId { get; init; }
    public string TargetHost { get; init; } = "";
    public bool Tracing { get; init; }
    public string Banner { get; init; } = "";
    public IReadOnlyList<HopLiveState> Hops { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<double?>> Heat { get; init; } = [];
    public DateTimeOffset Updated { get; init; } = DateTimeOffset.UtcNow;
}
