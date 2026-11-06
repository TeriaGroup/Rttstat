namespace Netpulse.Core.Monitoring;

public static class AxisHold
{
    public static double Next(double held, double data, double shrink = 0.04)
    {
        if (double.IsNaN(data) || double.IsInfinity(data) || data < 1) data = 1;
        if (double.IsNaN(held) || held < 1) held = data;
        if (data >= held) return data;
        return Math.Max(data, held + (data - held) * shrink);
    }

    public static List<double> Smooth(IReadOnlyList<double> values)
    {
        var list = new List<double>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            if (double.IsNaN(values[i]))
            {
                list.Add(double.NaN);
                continue;
            }
            double sum = 0;
            var n = 0;
            for (var j = Math.Max(0, i - 1); j <= Math.Min(values.Count - 1, i + 1); j++)
            {
                if (double.IsNaN(values[j])) continue;
                sum += values[j];
                n++;
            }
            list.Add(n == 0 ? double.NaN : sum / n);
        }
        return list;
    }
}
