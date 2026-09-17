using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Netpulse.App.Loc;
using Netpulse.Core.Formatting;
using Netpulse.Core.Models;

namespace Netpulse.App.Tray;

public partial class FlyoutWindow : Window
{
    private bool _suppressProfile;

    public FlyoutWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Hide(); };
    }

    public event Action? OpenMain;
    public event Action? TogglePause;
    public event Action? RunSpeed;
    public event Action? ExitApp;
    public event Action<Guid>? SwitchProfile;

    public void Apply(MonitorSnapshot s, IReadOnlyList<Profile> profiles)
    {
        TitleBlock.Text = s.Profile?.Name ?? "Rttstat";
        StatusBlock.Text = I18n.Current[s.StatusText];
        StatusBlock.Foreground = (Brush)new BrushConverter().ConvertFrom(NetFormat.QualityColor(s.Quality))!;
        PingBlock.Text = $"{I18n.Current["ping"]}  {NetFormat.Ping(s.AggregatePingMs)} ms";
        LossBlock.Text = $"{I18n.Current["loss"]}  1м {NetFormat.Loss(s.Loss1m)}   5м {NetFormat.Loss(s.Loss5m)}";
        DownBlock.Text = $"{I18n.Current["dn"]}  {NetFormat.Throughput(s.RecvBps)}";
        UpBlock.Text = $"{I18n.Current["up"]}  {NetFormat.Throughput(s.SentBps)}";
        Chart.Values = s.PingSpark;
        Chart.Stroke = StatusBlock.Foreground;
        OpenBtn.Content = I18n.Current["open"];
        PauseBtn.Content = s.Paused ? I18n.Current["resume"] : I18n.Current["pause"];
        SpeedBtn.Content = I18n.Current["speedtest"];
        ExitBtn.Content = I18n.Current["exit"];
        OutageBlock.Text = s.OpenOutage is { } o
            ? string.Format(I18n.Current["outage.for"], NetFormat.Duration(o.Duration))
            : string.Format(I18n.Current["online.for"], NetFormat.Duration(s.OnlineFor));
        _suppressProfile = true;
        ProfileBox.ItemsSource = profiles;
        ProfileBox.DisplayMemberPath = nameof(Profile.Name);
        ProfileBox.SelectedItem = profiles.FirstOrDefault(p => p.IsActive);
        _suppressProfile = false;
    }

    public void PlaceNearTray()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 12;
        Top = work.Bottom - Height - 12;
    }

    private void OnDeactivated(object sender, EventArgs e) => Hide();
    private void OpenClick(object sender, RoutedEventArgs e) { Hide(); OpenMain?.Invoke(); }
    private void PauseClick(object sender, RoutedEventArgs e) => TogglePause?.Invoke();
    private void SpeedClick(object sender, RoutedEventArgs e) => RunSpeed?.Invoke();
    private void ExitClick(object sender, RoutedEventArgs e) => ExitApp?.Invoke();

    private void ProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressProfile) return;
        if (ProfileBox.SelectedItem is Profile p)
            SwitchProfile?.Invoke(p.Id);
    }
}
