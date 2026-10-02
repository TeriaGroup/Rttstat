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
