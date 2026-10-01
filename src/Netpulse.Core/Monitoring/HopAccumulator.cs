namespace Netpulse.Core.Monitoring;

public sealed class HopAccumulator
{
    private readonly Queue<(bool Ok, double? Rtt)> _q = new();
    private const int Keep = 60;

    public void Add(bool ok, double? rtt)
    {
        _q.Enqueue((ok, rtt));
        while (_q.Count > Keep) _q.Dequeue();
    }

    public (int sent, int recv, double loss, double? avg, double? min, double? max, double jitter, List<double> spark) Stats()
    {
        var sent = _q.Count;
        var recv = _q.Count(x => x.Ok);
        var loss = sent == 0 ? 0 : (sent - recv) * 100.0 / sent;
        var rtts = _q.Where(x => x.Ok && x.Rtt is not null).Select(x => x.Rtt!.Value).ToList();
        double? avg = rtts.Count == 0 ? null : rtts.Average();
        double? min = rtts.Count == 0 ? null : rtts.Min();
        double? max = rtts.Count == 0 ? null : rtts.Max();
        var jitter = 0.0;
        for (var i = 1; i < rtts.Count; i++)
            jitter += Math.Abs(rtts[i] - rtts[i - 1]);
        if (rtts.Count > 1) jitter /= rtts.Count - 1;
        return (sent, recv, loss, avg, min, max, jitter, rtts.TakeLast(40).ToList());
    }
}
