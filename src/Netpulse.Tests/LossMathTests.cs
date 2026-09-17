using Netpulse.Core.Monitoring;

namespace Netpulse.Tests;

public class LossMathTests
{
    [Fact]
    public void Percent_ZeroAttempts_IsZero() => Assert.Equal(0, LossMath.Percent(5, 0));

    [Fact]
    public void Percent_HalfLost() => Assert.Equal(50, LossMath.Percent(5, 10));

    [Fact]
    public void CombineAvg_FromEmpty() => Assert.Equal(10, LossMath.CombineAvg(0, 0, 10));

    [Fact]
    public void CombineAvg_Running() => Assert.Equal(15, LossMath.CombineAvg(10, 1, 20));

    [Fact]
    public void Mbps_TenMegabytesInOneSecond() =>
        Assert.Equal(80, LossMath.Mbps(10_000_000, 1), 5);
}
