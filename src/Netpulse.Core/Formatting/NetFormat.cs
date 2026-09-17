using Netpulse.Core.Models;

namespace Netpulse.Core.Formatting;

public static class NetFormat
{
    public static string Ping(double? ms, int decimals = 0)
    {
        if (ms is null) return "—";
        if (ms >= 1000) return "1s+";
        return decimals <= 0 ? $"{ms:0}" : ms.Value.ToString("0." + new string('0', decimals));
    }

    public static string Loss(double percent) => $"{percent:0.0}%";

    public static string Throughput(double bps, string units = "auto")
    {
        var bits = bps * 8.0;
        if (units == "kbps" || (units == "auto" && bits < 1_000_000))
            return $"{bits / 1000.0:0.0} kbps";
        return $"{bits / 1_000_000.0:0.00} Mbps";
    }

    public static string ThroughputShort(double bps)
    {
        var bits = bps * 8.0;
        if (bits < 1_000_000) return $"{bits / 1000.0:0}";
        return $"{bits / 1_000_000.0:0.0}";
    }

    public static string Duration(TimeSpan t)
    {
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
        if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes}m {t.Seconds:00}s";
        return $"{(int)t.TotalSeconds}s";
    }

    public static string QualityColor(LinkQuality q) => q switch
    {
        LinkQuality.Ok => "#3DDC97",
        LinkQuality.Warn => "#F5C542",
        LinkQuality.Bad => "#F07178",
        LinkQuality.Down => "#F07178",
        _ => "#9AA0A6"
    };
}
