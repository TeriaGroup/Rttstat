using System.Net;
using System.Net.Sockets;
using Netpulse.Core.Monitoring;

namespace Netpulse.Tests;

public class Cycle2Tests
{
    [Fact(Timeout = 3000)]
    public async Task LiveSeries_ShareTheRightEdge()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        var path = Path.Combine(Path.GetTempPath(), "rttstat-cycle2-" + Guid.NewGuid().ToString("N"));
        timed.Restore(() => { if (File.Exists(path)) File.Delete(path); });
        File.WriteAllText(path, "x");

        var aligned = SeriesAlign.Right([10, 20], 4);
        Assert.Equal(4, aligned.Count);
        Assert.True(double.IsNaN(aligned[0]));
        Assert.True(double.IsNaN(aligned[1]));
        Assert.Equal(10, aligned[2]);
        Assert.Equal(20, aligned[3]);
        Assert.DoesNotContain(0d, aligned);
        await Task.CompletedTask;
    }

    [Fact(Timeout = 3000)]
    public async Task Spark_SharesClock_AndTimeoutIsAGap()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        var now = 9_000_000L;
        var a = new RollingWindow(TimeSpan.FromMinutes(5));
        var b = new RollingWindow(TimeSpan.FromMinutes(5));
        a.Add(now - 90_000, true, 20);
        a.Add(now - 2_000, false, null);
        a.Add(now - 1_000, true, 30);
        b.Add(now - 1_000, true, 15);
        var pingA = a.Spark(now, TimeSpan.FromSeconds(120), 120, true);
        var pingB = b.Spark(now, TimeSpan.FromSeconds(120), 120, true);
        Assert.Equal(pingA.Count, pingB.Count);
        Assert.Equal(120, pingA.Count);
        Assert.Equal(30, pingA[^1]);
        Assert.Equal(15, pingB[^1]);
        Assert.Contains(pingA, double.IsNaN);
        Assert.DoesNotContain(0d, pingA);
        Assert.Equal(20, pingA[^3]);
        await Task.CompletedTask;
    }

    [Fact(Timeout = 3000)]
    public async Task Spark_DoesNotWarp_UntilBucketElapses()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        var w = new RollingWindow(TimeSpan.FromMinutes(5));
        var now = 9_000_000L;
        w.Add(now - 2_000, true, 40);
        var before = w.Spark(now, TimeSpan.FromSeconds(120), 120, true);
        var after = w.Spark(now + 200, TimeSpan.FromSeconds(120), 120, true);
        Assert.Equal(before, after);
        var shifted = w.Spark(now + 1_000, TimeSpan.FromSeconds(120), 120, true);
        Assert.NotEqual(before, shifted);
        await Task.CompletedTask;
    }

    [Fact(Timeout = 3000)]
    public async Task MissingSecond_StaysConnectedToPreviousPoint()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        var now = 8_000_000L;
        var w = new RollingWindow(TimeSpan.FromMinutes(5));
        w.Add(now - 3_000, true, 20);
        w.Add(now - 1_000, true, 30);
        var spark = w.Spark(now, TimeSpan.FromSeconds(10), 10, true);
        Assert.Equal(20, spark[^3]);
        Assert.Equal(20, spark[^2]);
        Assert.Equal(30, spark[^1]);
        await Task.CompletedTask;
    }

    [Fact(Timeout = 3000)]
    public async Task LongOutage_KeepsScrollingLastPing()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        var now = 20_000_000L;
        var w = new RollingWindow(TimeSpan.FromMinutes(5));
        w.Add(now - 180_000, true, 42);
        for (var t = now - 120_000; t < now; t += 1_000)
            w.Add(t, false, null);
        var spark = w.Spark(now, TimeSpan.FromSeconds(120), 120, true);
        Assert.Equal(120, spark.Count);
        Assert.All(spark, v => Assert.Equal(42, v));
        var shifted = w.Spark(now + 1_000, TimeSpan.FromSeconds(120), 120, true);
        Assert.Equal(120, shifted.Count);
        Assert.All(shifted, v => Assert.Equal(42, v));
        await Task.CompletedTask;
    }

    [Fact(Timeout = 3000)]
    public async Task Axis_ExpandsNow_ShrinksSlowly()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        Assert.Equal(80, AxisHold.Next(20, 80));
        var shrunk = AxisHold.Next(100, 20, 0.1);
        Assert.InRange(shrunk, 90, 99);
        var smooth = AxisHold.Smooth([10, double.NaN, 30]);
        Assert.Equal(10, smooth[0]);
        Assert.True(double.IsNaN(smooth[1]));
        Assert.Equal(30, smooth[2]);
        await Task.CompletedTask;
    }

    [Fact(Timeout = 3000)]
    public async Task HostNormalize_StripsBrackets_AndUnwrapsMapped()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        Assert.Equal("2606:4700:4700::1111", IpChoice.NormalizeHost("[2606:4700:4700::1111]"));
        var mapped = IPAddress.Parse("::ffff:1.1.1.1");
        var v4 = IpChoice.Unwrap(mapped);
        Assert.Equal(AddressFamily.InterNetwork, v4.AddressFamily);
        Assert.Equal("1.1.1.1", v4.ToString());
        await Task.CompletedTask;
    }
}
