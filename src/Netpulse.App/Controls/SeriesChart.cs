using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Netpulse.App.Controls;

public sealed class ChartSeries
{
    public string Name { get; init; } = "";
    public Brush Stroke { get; init; } = Brushes.White;
    public double Thickness { get; init; } = 1.6;
    public IReadOnlyList<double> Values { get; init; } = [];
}

public sealed class SeriesChart : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series), typeof(IReadOnlyList<ChartSeries>), typeof(SeriesChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WindowSecondsProperty = DependencyProperty.Register(
        nameof(WindowSeconds), typeof(double), typeof(SeriesChart),
        new FrameworkPropertyMetadata(3600.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitSuffixProperty = DependencyProperty.Register(
        nameof(UnitSuffix), typeof(string), typeof(SeriesChart),
        new FrameworkPropertyMetadata(" %", FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<ChartSeries>? Series
    {
        get => (IReadOnlyList<ChartSeries>?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public double WindowSeconds
    {
        get => (double)GetValue(WindowSecondsProperty);
        set => SetValue(WindowSecondsProperty, value);
    }

    public string UnitSuffix
    {
        get => (string)GetValue(UnitSuffixProperty);
        set => SetValue(UnitSuffixProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 8 || ActualHeight < 8) return;
        const double left = 52, right = 12, top = 8, bottom = 22;
        var plotW = Math.Max(1, ActualWidth - left - right);
        var plotH = Math.Max(1, ActualHeight - top - bottom);
        var series = Series ?? [];
        var finite = series.SelectMany(s => s.Values).Where(v => !double.IsNaN(v)).ToList();

        double min = 0, max = 1;
        if (finite.Count > 0)
        {
            min = Math.Min(0, finite.Min());
            max = Math.Max(finite.Max(), 1);
        }
        Nice(min, max, out min, out max, out var step);

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
        gridPen.Freeze();
        var axisBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6));
        axisBrush.Freeze();
        var typeface = new Typeface("Segoe UI");
        var pixels = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (var v = min; v <= max + step * 0.01; v += step)
        {
            var y = top + plotH - (v - min) / (max - min) * plotH;
            dc.DrawLine(gridPen, new Point(left, y), new Point(left + plotW, y));
            var label = Format(v) + UnitSuffix;
            var ft = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, axisBrush, pixels);
            dc.DrawText(ft, new Point(Math.Max(2, left - ft.Width - 6), y - ft.Height / 2));
        }

        var now = DateTime.Now;
        var span = TimeSpan.FromSeconds(Math.Max(1, WindowSeconds));
        for (var i = 0; i <= 4; i++)
        {
            var frac = i / 4.0;
            var x = left + frac * plotW;
            dc.DrawLine(gridPen, new Point(x, top), new Point(x, top + plotH));
            var t = now - span + TimeSpan.FromSeconds(span.TotalSeconds * frac);
            var text = WindowSeconds <= 600 ? t.ToString("HH:mm:ss")
                : WindowSeconds <= 6 * 3600 ? t.ToString("HH:mm")
                : WindowSeconds <= 48 * 3600 ? t.ToString("dd.MM HH:mm")
                : t.ToString("dd.MM");
            var tft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, axisBrush, pixels);
            var tx = i == 0 ? x : i == 4 ? x - tft.Width : x - tft.Width / 2;
            dc.DrawText(tft, new Point(tx, top + plotH + 4));
        }

        foreach (var s in series)
        {
            if (s.Values.Count == 0) continue;
            var pen = new Pen(s.Stroke, Math.Max(1, s.Thickness)) { LineJoin = PenLineJoin.Round };
            pen.Freeze();
            var geo = new StreamGeometry();
            var started = false;
            var count = s.Values.Count;
            using (var ctx = geo.Open())
            {
                for (var i = 0; i < count; i++)
                {
                    var value = s.Values[i];
                    if (double.IsNaN(value))
                    {
                        started = false;
                        continue;
                    }
                    var x = count == 1 ? left + plotW : left + i / (double)(count - 1) * plotW;
                    var y = top + plotH - (value - min) / (max - min) * plotH;
                    var p = new Point(x, y);
                    if (!started)
                    {
                        ctx.BeginFigure(p, false, false);
                        started = true;
                    }
                    else ctx.LineTo(p, true, false);
                    if (count < 40)
                        dc.DrawEllipse(s.Stroke, null, p, 2.2, 2.2);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }
    }

    private static void Nice(double min, double max, out double nmin, out double nmax, out double step)
    {
        var range = Math.Max(max - min, 1);
        var raw = range / 4;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var norm = raw / mag;
        step = norm < 1.5 ? mag : norm < 3 ? 2 * mag : norm < 7 ? 5 * mag : 10 * mag;
        if (step <= 0) step = 1;
        nmin = Math.Floor(min / step) * step;
        nmax = Math.Ceiling(max / step) * step;
        if (nmax <= nmin) nmax = nmin + step;
    }

    private static string Format(double v)
    {
        var a = Math.Abs(v);
        if (a >= 100) return v.ToString("0");
        if (a >= 10) return v.ToString("0.0");
        return v.ToString("0.00");
    }
}
