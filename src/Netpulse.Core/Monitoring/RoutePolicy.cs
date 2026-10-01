namespace Netpulse.Core.Monitoring;

public static class RoutePolicy
{
    public static bool IntermediateLossIsOutage(double destLossPercent, double hopLossPercent)
        => destLossPercent >= 50;

    public static bool IsRouteChange(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        if (before.Count != after.Count) return before.Count > 0 && after.Count > 0;
        for (var i = 0; i < before.Count; i++)
        {
            if (!string.Equals(before[i], after[i], StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public static string BufferbloatGrade(double idleMs, double loadedMs)
    {
        var d = Math.Max(0, loadedMs - idleMs);
        if (d <= 5) return "A";
        if (d <= 20) return "B";
        if (d <= 50) return "C";
        if (d <= 100) return "D";
        if (d <= 200) return "E";
        return "F";
    }

    public static double MosEstimate(double rttMs, double lossPercent)
    {
        var r = 93.2 - (rttMs / 2.0) * 0.024 - lossPercent * 2.5;
        r = Math.Clamp(r, 0, 100);
        var mos = 1 + 0.035 * r + r * (r - 60) * (100 - r) * 7e-6;
        return Math.Clamp(mos, 1, 4.5);
    }
}
