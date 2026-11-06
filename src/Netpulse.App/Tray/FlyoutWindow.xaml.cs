using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Netpulse.App.Loc;
using Netpulse.Core.Abstractions;
using Netpulse.Core.Formatting;
using Netpulse.Core.Models;

namespace Netpulse.App.Tray;

public partial class FlyoutWindow : Window
{
    private static readonly string[] Palette = ["#F07178", "#7EC8E3", "#F5C542", "#3DDC97", "#C792EA", "#FF9B6A", "#82AAFF", "#F0A0C0"];
    private static readonly Brush[] Swatches = Palette.Select(Freeze).ToArray();

    private ISettingsProvider? _settings;
    private bool _resizing;

    public FlyoutWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Hide(); };
    }

    public event Action? OpenMain;
    public event Action? OpenSettings;
    public event Action? TogglePause;
    public event Action? RunSpeed;
    public event Action? ExitApp;

    public void Configure(ISettingsProvider settings) => _settings = settings;

    public void ApplySavedSize()
    {
        var tray = _settings?.Current.Tray;
        if (tray is null || _resizing) return;
        Width = Math.Clamp(tray.FlyoutWidth, MinWidth, MaxWidth);
        Height = Math.Clamp(tray.FlyoutHeight, MinHeight, MaxHeight);
        ApplyLayout();
    }

    public void Apply(MonitorSnapshot s)
    {
        var tray = _settings?.Current.Tray;
        var color = (Brush)new BrushConverter().ConvertFrom(NetFormat.QualityColor(s.Quality))!;
        Dot.Fill = color;
        StatusBlock.Text = I18n.Current[s.StatusText];
        StatusBlock.Foreground = color;

        var showAdapter = tray?.FlyoutShowAdapter == true && !string.IsNullOrWhiteSpace(s.AdapterName);
        AdapterBlock.Visibility = showAdapter ? Visibility.Visible : Visibility.Collapsed;
        AdapterBlock.Text = s.AdapterName;

        ShowTile(PingTile, tray?.ShowPing != false);
        ShowTile(LossTile, tray?.ShowLoss != false);
        ShowTile(DownTile, tray?.ShowThroughput != false);
        ShowTile(UpTile, tray?.ShowThroughput != false);
        ShowTile(JitterTile, tray?.FlyoutShowJitter == true);
        ShowTile(SpeedTile, tray?.FlyoutShowSpeed == true);

        PingBlock.Text = $"{NetFormat.Ping(s.AggregatePingMs)} ms";
        LossBlock.Text = $"{NetFormat.Loss(s.Loss1m)}  ·  {NetFormat.Loss(s.Loss5m)}";
        DownBlock.Text = NetFormat.Throughput(s.RecvBps);
        UpBlock.Text = NetFormat.Throughput(s.SentBps);
        JitterBlock.Text = $"{s.AggregateJitterMs:0} ms";
        SpeedBlock.Text = SpeedLine(s);

        Chart.Visibility = tray?.FlyoutShowChart != false ? Visibility.Visible : Visibility.Collapsed;
        Chart.Values = s.PingSpark;
        Chart.Stroke = color;

        OutageBlock.Visibility = tray?.ShowOutage != false ? Visibility.Visible : Visibility.Collapsed;
        OutageBlock.Text = s.OpenOutage is { } o
            ? string.Format(I18n.Current["outage.for"], NetFormat.Duration(o.Duration))
            : string.Format(I18n.Current["online.for"], NetFormat.Duration(s.OnlineFor));

        ButtonRow.Visibility = tray?.FlyoutShowButtons != false ? Visibility.Visible : Visibility.Collapsed;
        OpenBtn.Content = I18n.Current["open"];
        PauseBtn.Content = s.Paused ? I18n.Current["resume"] : I18n.Current["pause"];
        SpeedBtn.Content = I18n.Current["speedtest"];
        ExitBtn.Content = I18n.Current["exit"];

        var rows = new List<FlyoutTargetRow>();
        for (var i = 0; i < s.Targets.Count; i++)
        {
            var t = s.Targets[i];
            rows.Add(new FlyoutTargetRow
            {
                Name = string.IsNullOrWhiteSpace(t.DisplayName) ? t.Host : t.DisplayName,
                Host = string.IsNullOrWhiteSpace(t.ResolvedIp) ? t.Host : t.ResolvedIp,
                Ping = t.LastOk ? $"{NetFormat.Ping(t.LastRttMs)} ms" : "—",
                Loss = NetFormat.Loss(t.Loss1m),
                Swatch = Swatches[i % Swatches.Length]
            });
        }
        TargetList.ItemsSource = rows;
        ApplyLayout();
    }

    public void PlaceNearTray()
    {
        var work = SystemParameters.WorkArea;
        if (Width > work.Width - 24)
            Width = Math.Max(MinWidth, work.Width - 24);
        if (Height > work.Height - 24)
            Height = Math.Max(MinHeight, work.Height - 24);
        Left = Math.Max(work.Left + 8, work.Right - Width - 12);
        Top = Math.Max(work.Top + 8, work.Bottom - Height - 12);
        if (work.Top > 0)
            Top = work.Top + 12;
        if (work.Left > 0)
            Left = work.Left + 12;
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        var show = _settings?.Current.Tray.FlyoutShowTargets == true && Width >= 520;
        TargetCol.Width = show ? new GridLength(228) : new GridLength(0);
        TargetPane.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void ShowTile(UIElement tile, bool on)
        => tile.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

    private static string SpeedLine(MonitorSnapshot s)
    {
        if (s.SpeedtestPhase is not (SpeedtestPhase.Idle or SpeedtestPhase.Done or SpeedtestPhase.Error)
            && s.SpeedtestLiveMbps is { } live)
            return $"{live:0.0} Mbps";
        if (s.LastSpeedtest is { Error: null } last)
            return $"↓ {last.DownloadMbps:0.0}  ↑ {last.UploadMbps:0.0}";
        return "—";
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ApplyLayout();

    private void OnWidthDrag(object sender, DragDeltaEventArgs e)
    {
        _resizing = true;
        Width = Math.Clamp(Width + e.HorizontalChange, MinWidth, MaxWidth);
        ApplyLayout();
    }

    private void OnResizeDrag(object sender, DragDeltaEventArgs e)
    {
        _resizing = true;
        Width = Math.Clamp(Width + e.HorizontalChange, MinWidth, MaxWidth);
        Height = Math.Clamp(Height + e.VerticalChange, MinHeight, MaxHeight);
        ApplyLayout();
    }

    private void OnResizeDone(object sender, DragCompletedEventArgs e)
    {
        _resizing = false;
        var tray = _settings?.Current.Tray;
        if (tray is null) return;
        tray.FlyoutWidth = Width;
        tray.FlyoutHeight = Height;
        _settings!.Save();
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            if (!IsActive && IsVisible)
                Hide();
        });
    }

    private void OpenClick(object sender, RoutedEventArgs e) { Hide(); OpenMain?.Invoke(); }
    private void PauseClick(object sender, RoutedEventArgs e) => TogglePause?.Invoke();
    private void SpeedClick(object sender, RoutedEventArgs e) => RunSpeed?.Invoke();
    private void ExitClick(object sender, RoutedEventArgs e) { Hide(); ExitApp?.Invoke(); }

    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        Hide();
        OpenSettings?.Invoke();
    }

    private static Brush Freeze(string hex)
    {
        var brush = (Brush)new BrushConverter().ConvertFrom(hex)!;
        brush.Freeze();
        return brush;
    }
}

public sealed class FlyoutTargetRow
{
    public string Name { get; init; } = "";
    public string Host { get; init; } = "";
    public string Ping { get; init; } = "";
    public string Loss { get; init; } = "";
    public Brush Swatch { get; init; } = Brushes.Gray;
}
