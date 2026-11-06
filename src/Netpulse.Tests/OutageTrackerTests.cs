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

    [Fact]
    public void ShouldEmitOpensOnce()
    {
        var t = new OutageTracker();
        var p = P();
        Outage? prev = null;
        Outage? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = t.OnTick(p, true, [Ext(false)], "a");
            prev ??= last;
        }
        while (last is null)
            last = t.OnTick(p, true, [Ext(false)], "a");
        // emissions while open must be limited to the opening tick
        Assert.NotNull(prev);
        for (var i = 0; i < 3; i++)
            Assert.False(OutageTracker.ShouldEmit(prev, t.Current, t.Current));
        Assert.True(OutageTracker.ShouldEmit(null, t.Current, t.Current));
    }

    [Fact]
    public void ShouldEmitDetectsClosureOnSameInstance()
    {
        var t = new OutageTracker();
        var p = P();
        for (var i = 0; i < 3; i++)
            t.OnTick(p, true, [Ext(false)], "a");
        var open = t.Current;
        Assert.NotNull(open);
        // previous reference is the same mutated instance the tracker returned while open
        Assert.False(OutageTracker.ShouldEmit(open, t.Current, t.Current));
        t.OnTick(p, true, [Ext(true)], "a");
        var closed = t.OnTick(p, true, [Ext(true)], "a");
        Assert.NotNull(closed);
        // same reference, tracker no longer stores it => transition to closed must be emitted
        Assert.True(OutageTracker.ShouldEmit(open, closed, t.Current));
        Assert.Null(t.Current);
    }

    [Fact]
    public void ShouldEmitIgnoresNothingReturned()
    {
        Assert.False(OutageTracker.ShouldEmit(null, null, null));
        Assert.False(OutageTracker.ShouldEmit(new Outage(), null, null));
    }
}
