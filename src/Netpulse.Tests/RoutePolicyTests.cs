using Netpulse.Core.Monitoring;
using Netpulse.Core.Reports;

namespace Netpulse.Tests;

public class RoutePolicyTests
{
    [Fact]
    public void MidHopLoss_NotOutage_WhenDestHealthy()
        => Assert.False(RoutePolicy.IntermediateLossIsOutage(0, 100));

    [Fact]
    public void DestLoss_IsOutage()
        => Assert.True(RoutePolicy.IntermediateLossIsOutage(80, 80));

    [Fact]
    public void RouteChange_OnIpDiff()
        => Assert.True(RoutePolicy.IsRouteChange(["1.1.1.1"], ["8.8.8.8"]));

    [Fact]
    public void RouteChange_Same()
        => Assert.False(RoutePolicy.IsRouteChange(["1.1.1.1", "2.2.2.2"], ["1.1.1.1", "2.2.2.2"]));

    [Theory]
    [InlineData(10, 12, "A")]
    [InlineData(10, 40, "C")]
    [InlineData(10, 250, "F")]
    public void BloatGrades(double idle, double loaded, string g)
        => Assert.Equal(g, RoutePolicy.BufferbloatGrade(idle, loaded));

    [Fact]
    public void TtlClamp()
    {
        var ttl = Math.Clamp(99, 5, 30);
        Assert.Equal(30, ttl);
        Assert.Equal(5, Math.Clamp(1, 5, 30));
    }

    [Fact]
    public void ReportContainsSections()
    {
        var t = IspReport.Build("today", "ping 20", "none", "1 1.1.1.1", "A");
        Assert.Contains("Ping", t);
        Assert.Contains("Route hops", t);
        Assert.Contains("Outages", t);
    }
}
