using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;

namespace Netpulse.Tests;

public class PathDiagnosisTests
{
    [Fact]
    public void MidHopLoss_NotOutage_WhenDestHealthy()
    {
        Assert.False(RoutePolicy.IntermediateLossIsOutage(0, 100));
        var hops = new List<HopLiveState>
        {
            Hop(1, 0, 10),
            Hop(2, 0, 12),
            Hop(3, 100, 12),
            Hop(4, 0, 14)
        };
        var d = PathDiagnosis.Diagnose(hops, true);
        Assert.Equal("healthy", d.Code);
        Assert.True(PathDiagnosis.IsIcmpLimited(0, 100, false, true));
        Assert.False(PathDiagnosis.IsIcmpLimited(0, 100, true, true));
    }

    [Fact]
    public void LossStartingAtHop3_IsPath()
    {
        var hops = new List<HopLiveState>
        {
            Hop(1, 0, 10),
            Hop(2, 0, 12),
            Hop(3, 80, 40),
            Hop(4, 78, 42)
        };
        var d = PathDiagnosis.Diagnose(hops, true);
        Assert.Equal("path", d.Code);
        Assert.Equal(3, d.LossHop);
    }

    [Fact]
    public void LossAtFirstHop_IsHome()
    {
        var hops = new List<HopLiveState>
        {
            Hop(1, 80, 20),
            Hop(2, 82, 30),
            Hop(3, 79, 31)
        };
        var d = PathDiagnosis.Diagnose(hops, true);
        Assert.Equal("home", d.Code);
        Assert.Equal(1, d.LossHop);
    }

    [Fact]
    public void LossOnlyAtEnd_IsDestination()
    {
        var hops = new List<HopLiveState>
        {
            Hop(1, 0, 10),
            Hop(2, 1, 12),
            Hop(3, 80, 14)
        };
        var d = PathDiagnosis.Diagnose(hops, true);
        Assert.Equal("destination", d.Code);
    }

    [Fact]
    public void LatencyJump_AtLargestStep()
    {
        var hops = new List<HopLiveState>
        {
            Hop(1, 0, 10),
            Hop(2, 0, 12),
            Hop(3, 0, 80),
            Hop(4, 0, 85)
        };
        var d = PathDiagnosis.Diagnose(hops, true);
        Assert.Equal(3, d.JumpHop);
    }

    [Fact]
    public void NotReached_IsTracing()
        => Assert.Equal("tracing", PathDiagnosis.Diagnose([Hop(1, 0, 10)], false).Code);

    [Fact]
    public void MissingBucket_IsGap_NotZero()
    {
        var aligned = PathDiagnosis.Align([1, 2, 3], new Dictionary<long, double> { [1] = 4, [3] = 9 });
        Assert.Equal(4, aligned[0]);
        Assert.True(double.IsNaN(aligned[1]));
        Assert.Equal(9, aligned[2]);
    }

    private static HopLiveState Hop(int hop, double loss, double avg) => new()
    {
        Hop = hop,
        LossPercent = loss,
        AvgRtt = avg,
        Ip = hop.ToString()
    };
}
