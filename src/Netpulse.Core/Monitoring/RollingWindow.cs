namespace Netpulse.Core.Monitoring;

public sealed class RollingWindow
{
    private readonly Queue<(long Ts, bool Ok, double? Rtt)> _items = new();
    private readonly object _gate = new();
    private readonly long _keepMs;
    private double? _lastOkRtt;
    private long _lastOkRttTs = long.MaxValue;

    public RollingWindow(TimeSpan keep)
    {
        _keepMs = (long)keep.TotalMilliseconds;
    }

    public void Add(long ts, bool ok, double? rtt)
    {
        lock (_gate)
        {
            if (ok && rtt is { } held)
            {
                _lastOkRtt = held;
                _lastOkRttTs = ts;
            }
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
            double sum = 0, sumSq = 0, min = double.MaxValue, max = double.MinValue;
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
                        sumSq += r * r;
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
            double std = 0;
            if (rttN > 1)
            {
                var mean = sum / rttN;
                var variance = (sumSq - rttN * mean * mean) / (rttN - 1);
                std = Math.Sqrt(Math.Max(0, variance));
            }
            return new LossStats(
                total == 0 ? 0 : fail * 100.0 / total,
                rttN == 0 ? null : sum / rttN,
                rttN == 0 ? null : min,
                rttN == 0 ? null : max,
                jitterN == 0 ? 0 : jitterSum / jitterN,
                ok,
                fail,
                std);
        }
    }

    public List<double> Spark(long now, TimeSpan window, int maxPoints, bool rttNotLoss)
    {
        lock (_gate)
        {
            Trim(now);
            _ = maxPoints;
            var points = Math.Max(2, (int)window.TotalSeconds);
            const long bucket = 1000;
            var closedEnd = now / bucket * bucket;
            var from = closedEnd - points * bucket;
            double? seed = null;
            foreach (var item in _items)
            {
                if (item.Ts >= from) break;
                if (item.Ok && item.Rtt is { } prior) seed = prior;
            }
            if (seed is null && _lastOkRttTs < from)
                seed = _lastOkRtt;
            var selected = _items.Where(i => i.Ts >= from && i.Ts < closedEnd).ToList();
            var values = new double[points];
            var sum = new double[points];
            var ok = new int[points];
            var fail = new int[points];
            foreach (var item in selected)
            {
                var idx = (int)((item.Ts - from) / bucket);
                if (idx < 0 || idx >= points) continue;
                if (item.Ok && item.Rtt is { } r)
                {
                    sum[idx] += r;
                    ok[idx]++;
                }
                else if (!item.Ok)
                    fail[idx]++;
            }
            for (var i = 0; i < points; i++)
            {
                if (rttNotLoss)
                    values[i] = ok[i] == 0 ? double.NaN : sum[i] / ok[i];
                else
                {
                    var n = ok[i] + fail[i];
                    values[i] = n == 0 ? double.NaN : fail[i] * 100.0 / n;
                }
            }
            FillForward(values);
            if (rttNotLoss && seed is { } held)
            {
                for (var i = 0; i < values.Length && double.IsNaN(values[i]); i++)
                    values[i] = held;
            }
            return values.ToList();
        }
    }

    public static void FillForward(double[] values)
    {
        double? last = null;
        for (var i = 0; i < values.Length; i++)
        {
            if (double.IsNaN(values[i]))
            {
                if (last is { } held) values[i] = held;
            }
            else last = values[i];
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
    int Fail,
    double StdDev);
