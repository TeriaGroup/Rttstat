namespace Netpulse.Core.Models;

public sealed class PingSample
{
    public long Ts { get; init; }
    public Guid ProfileId { get; init; }
    public Guid TargetId { get; init; }
    public double? RttMs { get; init; }
    public bool Ok { get; init; }
    public string Status { get; init; } = "";
    public string TargetName { get; init; } = "";
    public TargetRole Role { get; init; }
}

public sealed class TargetLiveState
{
    public Guid TargetId { get; init; }
    public string DisplayName { get; set; } = "";
    public string Host { get; set; } = "";
    public string ResolvedIp { get; set; } = "";
    public TargetRole Role { get; init; }
    public bool LastOk { get; set; }
    public double? LastRttMs { get; set; }
    public string LastStatus { get; set; } = "—";
    public double Loss1m { get; set; }
    public double Loss5m { get; set; }
    public double Loss1h { get; set; }
    public double? AvgRtt5m { get; set; }
    public double? MinRtt5m { get; set; }
    public double? MaxRtt5m { get; set; }
    public double JitterMs { get; set; }
    public int ConsecutiveFails { get; set; }
    public int ConsecutiveIcmpErrors { get; set; }
    public bool UsingTcp { get; set; }
    public int Sent { get; set; }
    public int Recv { get; set; }
    public double SessionLoss { get; set; }
    public double StdDevMs { get; set; }
    public double Mos { get; set; }
    public IReadOnlyList<double> Spark { get; set; } = [];
    public IReadOnlyList<double> LossSpark { get; set; } = [];
}

public sealed class NicSample
{
    public long Ts { get; init; }
    public string AdapterId { get; init; } = "";
    public double RecvBps { get; init; }
    public double SentBps { get; init; }
}
