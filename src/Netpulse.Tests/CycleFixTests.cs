using Microsoft.Win32;
using Netpulse.Core.Monitoring;

namespace Netpulse.Tests;

public class CycleFixTests
{
    [Fact(Timeout = 3000)]
    public async Task LossSpark_IsRollingPercent_NotASingleSpike()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        var w = new RollingWindow(TimeSpan.FromMinutes(5));
        var now = 5_000_000L;
        for (var i = 0; i < 7; i++)
            w.Add(now - (8 - i) * 1000, true, 20);
        w.Add(now, false, null);
        var spark = w.Spark(now, TimeSpan.FromMinutes(1), 40, false);
        Assert.NotEmpty(spark);
        Assert.Equal(12.5, spark[^1], 1);
        Assert.DoesNotContain(spark, v => v > 20);
        await Task.CompletedTask;
    }

    [Fact(Timeout = 3000)]
    public async Task HideKey_RealIp_IgnoresHopIndex()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var hidden = new[] { HopHide.Key(id, 6, "185.0.44.57") };
        Assert.True(HopHide.IsHidden(hidden, id, 2, "185.0.44.57"));
        Assert.False(HopHide.IsHidden(hidden, id, 6, "77.88.8.8"));
        Assert.False(HopHide.IsHidden(hidden, id, 6, "*"));
        await Task.CompletedTask;
    }

    [Fact(Timeout = 3000)]
    public async Task HideKey_Star_IsPerHop_AndLegacyKeyStillMatches()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Assert.True(HopHide.IsHidden([HopHide.Key(id, 6, "*")], id, 6, "*"));
        Assert.False(HopHide.IsHidden([HopHide.Key(id, 6, "*")], id, 7, "*"));
        var legacy = id.ToString("N") + "|4|172.0.0.1";
        Assert.True(HopHide.IsHidden([legacy], id, 4, "172.0.0.1"));
        await Task.CompletedTask;
    }

    [Fact(Timeout = 4000)]
    public async Task Timer_RestoresTempFile_AndDoesNotChangeAutostart()
    {
        var path = Path.Combine(Path.GetTempPath(), "rttstat-cycle-" + Guid.NewGuid().ToString("N"));
        string? before;
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
            before = key?.GetValue("Rttstat") as string;

        using (var timed = new TimedRestore(TimeSpan.FromMilliseconds(250)))
        {
            File.WriteAllText(path, "x");
            timed.Restore(() =>
            {
                if (File.Exists(path)) File.Delete(path);
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (before is null) key.DeleteValue("Rttstat", false);
                else key.SetValue("Rttstat", before);
            });
            await Task.Delay(400);
            Assert.False(File.Exists(path));
        }

        using var after = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
        Assert.Equal(before, after?.GetValue("Rttstat") as string);
    }
}
