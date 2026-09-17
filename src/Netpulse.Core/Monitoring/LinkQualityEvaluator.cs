using Netpulse.Core.Models;

namespace Netpulse.Core.Monitoring;

public static class LinkQualityEvaluator
{
    public static (LinkQuality Quality, string Text) Evaluate(
        bool paused,
        bool adapterUp,
        bool outageOpen,
        IReadOnlyList<TargetLiveState> targets,
        Profile profile)
    {
        if (paused) return (LinkQuality.Paused, "paused");
        if (!adapterUp) return (LinkQuality.Down, "adapter_down");
        if (outageOpen) return (LinkQuality.Down, "down");

        var enabled = targets.Where(t => true).ToList();
        var external = enabled.Where(t => t.Role is TargetRole.External or TargetRole.Dns).ToList();
        var gateway = enabled.FirstOrDefault(t => t.Role == TargetRole.Gateway);

        var extFail = external.Count > 0 && external.All(t => !t.LastOk);
        if (extFail) return (LinkQuality.Down, "no_internet");

        var gwFail = gateway is { LastOk: false };
        var extOk = external.Count == 0 || external.Any(t => t.LastOk);
        if (gwFail && extOk) return (LinkQuality.Warn, "gateway_warn");

        foreach (var t in external)
        {
            if (t.Loss5m >= profile.LossBadPercent || (t.LastRttMs is { } r && r >= profile.PingBadMs))
                return (LinkQuality.Bad, "high_latency");
        }

        foreach (var t in external)
        {
            if (t.Loss5m >= profile.LossWarnPercent || (t.LastRttMs is { } r && r >= profile.PingWarnMs))
                return (LinkQuality.Warn, "degraded");
        }

        return (LinkQuality.Ok, "online");
    }

    public static OutageCause GuessCause(bool adapterUp, TargetLiveState? gateway, IReadOnlyList<TargetLiveState> targets)
    {
        if (!adapterUp) return OutageCause.NicDown;
        var external = targets.Where(t => t.Role is TargetRole.External or TargetRole.Dns).ToList();
        var gwFail = gateway is { LastOk: false };
        var extFail = external.Count == 0 || external.All(t => !t.LastOk);
        if (!gwFail && extFail) return OutageCause.NoInternet;
        if (gwFail && extFail) return OutageCause.NoInternet;
        if (gwFail) return OutageCause.NoGateway;
        return OutageCause.Unknown;
    }
}
