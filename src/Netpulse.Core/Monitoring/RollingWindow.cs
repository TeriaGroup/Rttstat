namespace Netpulse.Core.Monitoring;

public sealed class RollingWindow
{
    private readonly Queue<(long Ts, bool Ok, double? Rtt)> _items = new();
    private readonly object _gate = new();
    private readonly long _keepMs;

    public RollingWindow(TimeSpan keep)
    {
        _keepMs = (long)keep.TotalMilliseconds;
    }

    public void Add(long ts, bool ok, double? rtt)
    {
        lock (_gate)
        {
            _items.Enqueue((ts, ok, rtt));
            Trim(ts);
        }
    }

    public void Clear()
    {
        lock (_gate) _items.Clear();
    }

    public LossStats Stats(long now, TimeSpan window)
    {
        lock (_gate)
        {
            Trim(now);
            var from = now - (long)window.TotalMilliseconds;
            int ok = 0, fail = 0;
            double sum = 0, min = double.MaxValue, max = double.MinValue;
            int rttN = 0;
            double? prev = null;
            double jitterSum = 0;
            int jitterN = 0;
            foreach (var item in _items)
            {
                if (item.Ts < from) continue;
                if (item.Ok)
                {
                    ok++;
                    if (item.Rtt is { } r)
                    {
                        sum += r;
                        rttN++;
                        if (r < min) min = r;
                        if (r > max) max = r;
                        if (prev is { } p)
                        {
                            jitterSum += Math.Abs(r - p);
                            jitterN++;
                        }
                        prev = r;
                    }
                }
                else fail++;
            }

            var total = ok + fail;
            return new LossStats(
                total == 0 ? 0 : fail * 100.0 / total,
                rttN == 0 ? null : sum / rttN,
                rttN == 0 ? null : min,
                rttN == 0 ? null : max,
                jitterN == 0 ? 0 : jitterSum / jitterN,
                ok,
                fail);
        }
    }

    public List<double> Spark(long now, TimeSpan window, int maxPoints, bool rttNotLoss)
    {
        lock (_gate)
        {
            Trim(now);
            var from = now - (long)window.TotalMilliseconds;
            var selected = _items.Where(i => i.Ts >= from).ToList();
            if (selected.Count == 0) return [];
            var step = Math.Max(1, selected.Count / maxPoints);
            var list = new List<double>();
            for (var i = 0; i < selected.Count; i += step)
            {
                var item = selected[i];
                if (rttNotLoss) list.Add(item.Ok ? item.Rtt ?? 0 : 0);
                else list.Add(item.Ok ? 0 : 100);
            }
            return list;
        }
    }

    private void Trim(long now)
    {
        var cut = now - _keepMs;
        while (_items.Count > 0 && _items.Peek().Ts < cut)
            _items.Dequeue();
    }
}

public readonly record struct LossStats(
    double LossPercent,
    double? AvgRtt,
    double? MinRtt,
    double? MaxRtt,
    double Jitter,
    int Ok,
    int Fail);
