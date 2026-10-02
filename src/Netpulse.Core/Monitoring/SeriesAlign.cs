namespace Netpulse.Core.Monitoring;

public static class SeriesAlign
{
    public static List<double> Right(IReadOnlyList<double> values, int width)
    {
        if (width < 1) width = 1;
        if (values.Count == width) return values.ToList();
        if (values.Count > width) return values.Skip(values.Count - width).ToList();
        var list = new List<double>(width);
        for (var i = values.Count; i < width; i++)
            list.Add(double.NaN);
        list.AddRange(values);
        return list;
    }
}
