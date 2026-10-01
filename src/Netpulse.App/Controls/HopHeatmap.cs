using System.Windows;
using System.Windows.Media;

namespace Netpulse.App.Controls;

public sealed class HopHeatmap : FrameworkElement
{
    public static readonly DependencyProperty HeatProperty = DependencyProperty.Register(
        nameof(Heat), typeof(IReadOnlyList<IReadOnlyList<double?>>), typeof(HopHeatmap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<IReadOnlyList<double?>>? Heat
    {
        get => (IReadOnlyList<IReadOnlyList<double?>>?)GetValue(HeatProperty);
        set => SetValue(HeatProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var heat = Heat;
        if (heat is null || heat.Count == 0 || ActualWidth < 4 || ActualHeight < 4) return;
        var cols = heat.Max(r => r.Count);
        if (cols == 0) return;
        var rw = ActualWidth / cols;
        var rh = ActualHeight / heat.Count;
        var max = 1.0;
        foreach (var row in heat)
        foreach (var v in row)
            if (v is { } x && x > max) max = x;

        for (var r = 0; r < heat.Count; r++)
        for (var c = 0; c < heat[r].Count; c++)
        {
            var v = heat[r][c];
            Color col;
            if (v is null) col = Color.FromRgb(0x2A, 0x2D, 0x33);
            else
            {
                var t = Math.Clamp(v.Value / max, 0, 1);
                col = Color.FromRgb((byte)(30 + 200 * t), (byte)(200 - 140 * t), (byte)(80));
            }
            dc.DrawRectangle(new SolidColorBrush(col), null, new Rect(c * rw, r * rh, Math.Max(1, rw - 0.5), Math.Max(1, rh - 0.5)));
        }
    }
}
