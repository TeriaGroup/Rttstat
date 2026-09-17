using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Netpulse.App.Controls;

public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Lime, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitSuffixProperty = DependencyProperty.Register(
        nameof(UnitSuffix), typeof(string), typeof(Sparkline),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FloorZeroProperty = DependencyProperty.Register(
        nameof(FloorZero), typeof(bool), typeof(Sparkline),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WindowSecondsProperty = DependencyProperty.Register(
        nameof(WindowSeconds), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(60.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid), typeof(bool), typeof(Sparkline),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public string UnitSuffix
    {
        get => (string)GetValue(UnitSuffixProperty);
        set => SetValue(UnitSuffixProperty, value);
    }

    public bool FloorZero
    {
        get => (bool)GetValue(FloorZeroProperty);
        set => SetValue(FloorZeroProperty, value);
    }

    public double WindowSeconds
    {
        get => (double)GetValue(WindowSecondsProperty);
        set => SetValue(WindowSecondsProperty, value);
    }

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var values = Values;
        if (ActualWidth < 8 || ActualHeight < 8) return;

        var showGrid = ShowGrid;
        var left = showGrid ? 52 : 2;
        var right = showGrid ? 12 : 2;
        var top = showGrid ? 6 : 1;
        var bottom = showGrid ? 22 : 1;
        var plotW = Math.Max(1, ActualWidth - left - right);
        var plotH = Math.Max(1, ActualHeight - top - bottom);

        double min = 0, max = 1;
        if (values is { Count: > 0 })
        {
            min = values.Min();
            max = values.Max();
        }
        if (FloorZero) min = Math.Min(min, 0);
        if (Math.Abs(max - min) < 1e-9)
        {
            max = min + (FloorZero ? 1 : Math.Max(1, Math.Abs(min) * 0.1 + 1));
            if (FloorZero) min = 0;
        }
        Nice(min, max, out min, out max, out var step);

        if (showGrid)
        {
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
            var axisBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6));
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
                var tft = new FormattedText(t.ToString("HH:mm:ss"), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, axisBrush, pixels);
                var tx = x - tft.Width / 2;
                if (i == 0) tx = x;
                if (i == 4) tx = x - tft.Width;
                dc.DrawText(tft, new Point(tx, top + plotH + 4));
            }
        }

        if (values is null || values.Count < 2) return;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < values.Count; i++)
            {
                var x = left + i / (double)(values.Count - 1) * plotW;
                var y = top + plotH - (values[i] - min) / (max - min) * plotH;
                if (i == 0) ctx.BeginFigure(new Point(x, y), false, false);
                else ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, new Pen(Stroke, 1.8) { LineJoin = PenLineJoin.Round }, geo);
    }

    private static void Nice(double min, double max, out double nmin, out double nmax, out double step)
    {
        var range = max - min;
        if (range <= 0) range = 1;
        var raw = range / 4;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var norm = raw / mag;
        step = norm < 1.5 ? mag : norm < 3 ? 2 * mag : norm < 7 ? 5 * mag : 10 * mag;
        nmin = Math.Floor(min / step) * step;
        nmax = Math.Ceiling(max / step) * step;
        if (nmax <= nmin) nmax = nmin + step;
    }

    private static string Format(double v)
    {
        var a = Math.Abs(v);
        if (a >= 100) return v.ToString("0");
        if (a >= 10) return v.ToString("0.0");
        if (a >= 1) return v.ToString("0.00");
        return v.ToString("0.000");
    }
}
