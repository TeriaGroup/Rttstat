using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;

namespace Netpulse.Tests;

public class OutageTrackerTests
{
    private static Profile P() => new() { ConsecutiveTimeoutsForOutage = 3, ConsecutiveSuccessesToRecover = 2 };

    private static TargetLiveState Ext(bool ok) => new() { Role = TargetRole.External, LastOk = ok };

    [Fact]
    public void OpensAfterConsecutiveFails()
    {
        var t = new OutageTracker();
        var profile = P();
        Outage? last = null;
        for (var i = 0; i < 3; i++)
            last = t.OnTick(profile, true, [Ext(false)], "a");
        Assert.NotNull(last);
        Assert.True(last!.IsOpen);
    }

    [Fact]
    public void DoesNotOpenOnSingleFail()
    {
        var t = new OutageTracker();
        var last = t.OnTick(P(), true, [Ext(false)], "a");
        Assert.Null(last);
    }

    [Fact]
    public void RecoversAfterSuccesses()
    {
        var t = new OutageTracker();
        var p = P();
        for (var i = 0; i < 3; i++)
            t.OnTick(p, true, [Ext(false)], "a");
        Assert.True(t.Current!.IsOpen);
        t.OnTick(p, true, [Ext(true)], "a");
        var closed = t.OnTick(p, true, [Ext(true)], "a");
        Assert.NotNull(closed);
        Assert.False(closed!.IsOpen);
        Assert.Null(t.Current);
    }
}
