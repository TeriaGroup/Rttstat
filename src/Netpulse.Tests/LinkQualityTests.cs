using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;

namespace Netpulse.Tests;

public class LinkQualityTests
{
    private static Profile Profile() => new() { PingWarnMs = 80, PingBadMs = 150, LossWarnPercent = 2, LossBadPercent = 10 };

    [Fact]
    public void Paused()
    {
        var (q, t) = LinkQualityEvaluator.Evaluate(true, true, false, [], Profile());
        Assert.Equal(LinkQuality.Paused, q);
        Assert.Equal("paused", t);
    }

    [Fact]
    public void AdapterDown()
    {
        var (q, _) = LinkQualityEvaluator.Evaluate(false, false, false, [], Profile());
        Assert.Equal(LinkQuality.Down, q);
    }

    [Fact]
    public void OpenOutage()
    {
        var (q, _) = LinkQualityEvaluator.Evaluate(false, true, true, [], Profile());
        Assert.Equal(LinkQuality.Down, q);
    }

    [Fact]
    public void GatewayFailInternetOk_IsWarn()
    {
        var targets = new List<TargetLiveState>
        {
            new() { Role = TargetRole.Gateway, LastOk = false },
            new() { Role = TargetRole.External, LastOk = true, LastRttMs = 20, Loss5m = 0 }
        };
        var (q, text) = LinkQualityEvaluator.Evaluate(false, true, false, targets, Profile());
        Assert.Equal(LinkQuality.Warn, q);
        Assert.Equal("gateway_warn", text);
    }

    [Fact]
    public void HighPing_IsBad()
    {
        var targets = new List<TargetLiveState>
        {
            new() { Role = TargetRole.External, LastOk = true, LastRttMs = 200, Loss5m = 0 }
        };
        var (q, _) = LinkQualityEvaluator.Evaluate(false, true, false, targets, Profile());
        Assert.Equal(LinkQuality.Bad, q);
    }

    [Fact]
    public void OkWhenHealthy()
    {
        var targets = new List<TargetLiveState>
        {
            new() { Role = TargetRole.External, LastOk = true, LastRttMs = 12, Loss5m = 0 }
        };
        var (q, _) = LinkQualityEvaluator.Evaluate(false, true, false, targets, Profile());
        Assert.Equal(LinkQuality.Ok, q);
    }
}
