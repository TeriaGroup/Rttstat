using System.Text;
using Netpulse.Core.Models;

namespace Netpulse.Core.Reports;

public static class IspReport
{
    public static string Build(
        string rangeLabel,
        string pingSummary,
        string outages,
        string hops,
        string speed)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Rttstat ISP report");
        sb.AppendLine(rangeLabel);
        sb.AppendLine();
        sb.AppendLine("== Ping ==");
        sb.AppendLine(pingSummary);
        sb.AppendLine();
        sb.AppendLine("== Outages ==");
        sb.AppendLine(string.IsNullOrWhiteSpace(outages) ? "(none)" : outages);
        sb.AppendLine();
        sb.AppendLine("== Route hops ==");
        sb.AppendLine(string.IsNullOrWhiteSpace(hops) ? "(none)" : hops);
        sb.AppendLine();
        sb.AppendLine("== Speed / bufferbloat ==");
        sb.AppendLine(string.IsNullOrWhiteSpace(speed) ? "(none)" : speed);
        return sb.ToString();
    }

    public static string HopsText(IEnumerable<HopLiveState> hops)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Hop\tIP\tLoss%\tAvg\tBest\tWorst");
        foreach (var h in hops)
            sb.AppendLine($"{h.Hop}\t{h.Ip}\t{h.LossPercent:0.0}\t{h.AvgRtt:0.0}\t{h.MinRtt:0.0}\t{h.MaxRtt:0.0}");
        return sb.ToString();
    }
}
