using System.Windows;
using System.Windows.Media;
using Netpulse.App.Loc;
using Netpulse.Core.Formatting;
using Netpulse.Core.Models;

namespace Netpulse.App.Tray;

public partial class HudWindow : Window
{
    public HudWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeMethods.ApplyNoActivate(this);
    }

    public void Apply(MonitorSnapshot s, TraySettings tray)
    {
        Strip.Visibility = tray.StripEnabled ? Visibility.Visible : Visibility.Collapsed;
        Height = tray.StripEnabled ? 52 : 36;
        var color = (Color)ColorConverter.ConvertFromString(NetFormat.QualityColor(s.Quality))!;
        Strip.Stroke = new SolidColorBrush(color);
        Strip.Values = tray.StripMetric == "Loss" ? s.LossSpark
            : tray.StripMetric == "Down" ? s.DownSpark.Select(v => v * 8 / 1_000_000).ToArray()
            : s.PingSpark;

        if (s.Quality == LinkQuality.Down && s.OpenOutage is not null)
            Line.Text = $"{I18n.Current["down"]}  {NetFormat.Duration(s.OpenOutage.Duration)}  {I18n.Current[s.StatusText]}";
        else
        {
            Line.Text =
                $"{NetFormat.Ping(s.AggregatePingMs)} ms   {NetFormat.Loss(s.Loss1m)}   ↓ {NetFormat.Throughput(s.RecvBps)}   ↑ {NetFormat.Throughput(s.SentBps)}";
        }
        Line.Foreground = new SolidColorBrush(color);
        Position();
        var hide = tray.HideHudInFullscreen && NativeMethods.IsForegroundFullscreen();
        if (!tray.HudEnabled || hide) Hide();
        else if (!IsVisible) Show();
    }

    public void Position()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 10;
        Top = work.Bottom - Height - 10;
        if (SystemParameters.WorkArea.Top > 0)
            Top = work.Top + 10;
        if (SystemParameters.WorkArea.Left > 0)
            Left = work.Left + 10;
    }

    private void OnClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => Clicked?.Invoke();

    public event Action? Clicked;
}
