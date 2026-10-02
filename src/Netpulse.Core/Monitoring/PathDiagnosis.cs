using Netpulse.Core.Models;

namespace Netpulse.Core.Monitoring;

public readonly record struct PathDiagnosisResult(string Code, int? LossHop, int? JumpHop);

public static class PathDiagnosis
{
    public static PathDiagnosisResult Diagnose(IReadOnlyList<HopLiveState> hops, bool reached)
    {
        if (!reached || hops.Count == 0)
            return new PathDiagnosisResult("tracing", null, null);

        var ordered = hops.OrderBy(h => h.Hop).ToList();
        var dest = ordered[^1];
        var jump = LatencyJump(ordered, dest.AvgRtt);
        if (dest.LossPercent < 5)
            return new PathDiagnosisResult("healthy", null, jump);

        int? fault = null;
        for (var n = 0; n < ordered.Count; n++)
        {
            if (ordered[n].LossPercent < dest.LossPercent * 0.5) continue;
            if (Median(ordered.Skip(n).Select(h => h.LossPercent)) < dest.LossPercent * 0.5) continue;
            fault = ordered[n].Hop;
            break;
        }

        if (fault is null || fault == dest.Hop)
            return new PathDiagnosisResult("destination", dest.Hop, jump);
        if (fault == ordered[0].Hop)
            return new PathDiagnosisResult("home", fault, jump);
        return new PathDiagnosisResult("path", fault, jump);
    }

    public static bool IsIcmpLimited(double destLoss, double hopLoss, bool isDestination, bool reached)
        => reached && !isDestination && destLoss < 5 && hopLoss >= 50;

    public static List<double> Align(IReadOnlyList<long> buckets, IReadOnlyDictionary<long, double> values)
    {
        var list = new List<double>(buckets.Count);
        foreach (var b in buckets)
            list.Add(values.TryGetValue(b, out var v) ? v : double.NaN);
        return list;
    }

    private static int? LatencyJump(IReadOnlyList<HopLiveState> hops, double? destAvg)
    {
        if (destAvg is not { } dest || hops.Count < 2) return null;
        int? hop = null;
        var best = 0.0;
        for (var i = 1; i < hops.Count; i++)
        {
            if (hops[i].AvgRtt is not { } cur || hops[i - 1].AvgRtt is not { } prev) continue;
            var d = cur - prev;
            if (d >= 20 && dest >= cur - 15 && d > best)
            {
                best = d;
                hop = hops[i].Hop;
            }
        }
        return hop;
    }

    private static double Median(IEnumerable<double> values)
    {
        var a = values.OrderBy(v => v).ToArray();
        if (a.Length == 0) return 0;
        var mid = a.Length / 2;
        return a.Length % 2 == 1 ? a[mid] : (a[mid - 1] + a[mid]) / 2.0;
    }
}
