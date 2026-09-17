using Netpulse.Core.Monitoring;

namespace Netpulse.Tests;

public class RollingWindowTests
{
    [Fact]
    public void LossOverWindow()
    {
        var w = new RollingWindow(TimeSpan.FromMinutes(10));
        var now = 1_000_000L;
        w.Add(now - 1000, true, 10);
        w.Add(now - 500, false, null);
        w.Add(now, true, 12);
        var s = w.Stats(now, TimeSpan.FromMinutes(1));
        Assert.Equal(1, s.Fail);
        Assert.Equal(2, s.Ok);
        Assert.Equal(100.0 / 3, s.LossPercent, 5);
    }
}
