using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Netpulse.App.Loc;
using Netpulse.Core.Abstractions;
using Netpulse.Core.Formatting;
using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;
using Netpulse.Core.Speedtest;

namespace Netpulse.App.Tray;

public sealed class TrayController : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly HudWindow _hud = new();
    private readonly FlyoutWindow _flyout = new();
    private readonly MonitorHub _hub;
    private readonly IProfileProvider _profiles;
    private readonly ISettingsProvider _settings;
    private readonly SpeedtestService _speed;
    private Icon? _currentIcon;
    private int _lastDrawn = int.MinValue;
    private LinkQuality _lastQ;

    public TrayController(
        MonitorHub hub,
        IProfileProvider profiles,
        ISettingsProvider settings,
        SpeedtestService speed,
        Action openMain,
        Action exit)
    {
        _hub = hub;
        _profiles = profiles;
        _settings = settings;
        _speed = speed;
        _currentIcon = IconFactory.Create(LinkQuality.Paused, null);
        _icon = new TaskbarIcon
        {
            ToolTipText = "Rttstat",
            Icon = _currentIcon,
            MenuActivation = PopupActivationMode.RightClick,
            ContextMenu = BuildMenu(openMain, exit)
        };
        _icon.TrayLeftMouseUp += (_, _) =>
        {
            if (_settings.Current.Tray.FlyoutOpenOnLeftClick && _settings.Current.Tray.FlyoutEnabled)
                ToggleFlyout();
            else openMain();
        };
        _icon.TrayMouseDoubleClick += (_, _) => openMain();
        _hud.Clicked += ToggleFlyout;
        _flyout.OpenMain += openMain;
        _flyout.ExitApp += exit;
        _flyout.TogglePause += () =>
        {
            _hub.Paused = !_hub.Paused;
            _hub.EmitEvent(EventLevel.Info, EventCategory.App, _hub.Paused ? "Monitoring paused" : "Monitoring resumed");
        };
        _flyout.RunSpeed += () => _ = _speed.RunNowAsync();
        _flyout.SwitchProfile += id =>
        {
            _profiles.SetActive(id);
            _hub.EmitEvent(EventLevel.Info, EventCategory.Profile, "Profile changed");
        };
        hub.Updated += snap =>
        {
            var disp = Application.Current?.Dispatcher;
            if (disp is null) return;
            if (disp.CheckAccess()) Apply(snap);
            else disp.BeginInvoke(() => Apply(snap));
        };
    }

    public TaskbarIcon Icon => _icon;

    private ContextMenu BuildMenu(Action openMain, Action exit)
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) => RebuildDynamic(menu, openMain, exit);
        RebuildDynamic(menu, openMain, exit);
        return menu;
    }

    private void RebuildDynamic(ContextMenu menu, Action openMain, Action exit)
    {
        menu.Items.Clear();
        menu.Items.Add(Item(I18n.Current["tray.open"], openMain));
        menu.Items.Add(Item(_hub.Paused ? I18n.Current["tray.resume"] : I18n.Current["tray.pause"], () => _hub.Paused = !_hub.Paused));
        menu.Items.Add(Item(I18n.Current["tray.speed"], () => _ = _speed.RunNowAsync()));
        var profiles = new MenuItem { Header = I18n.Current["tray.profiles"] };
        foreach (var p in _profiles.All)
        {
            var id = p.Id;
            var mi = new MenuItem { Header = p.Name, IsCheckable = true, IsChecked = p.IsActive };
            mi.Click += (_, _) => _profiles.SetActive(id);
            profiles.Items.Add(mi);
        }
        menu.Items.Add(profiles);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(I18n.Current["exit"], exit));
    }

    private static MenuItem Item(string header, Action action)
    {
        var mi = new MenuItem { Header = header };
        mi.Click += (_, _) => action();
        return mi;
    }

    private void ToggleFlyout()
    {
        if (!_settings.Current.Tray.FlyoutEnabled) return;
        if (_flyout.IsVisible) { _flyout.Hide(); return; }
        _flyout.Apply(_hub.Current, _profiles.All);
        _flyout.PlaceNearTray();
        _flyout.Show();
        _flyout.Activate();
    }

    private void Apply(MonitorSnapshot s)
    {
        var pingInt = s.AggregatePingMs is null ? -1 : (int)Math.Round(s.AggregatePingMs.Value);
        if (pingInt != _lastDrawn || s.Quality != _lastQ)
        {
            _lastDrawn = pingInt;
            _lastQ = s.Quality;
            var next = IconFactory.Create(s.Quality, s.AggregatePingMs);
            _icon.Icon = next;
            _currentIcon?.Dispose();
            _currentIcon = next;
        }

        _icon.ToolTipText = BuildTip(s, _hub.CurrentRoute);
        if (_settings.Current.Tray.HudEnabled)
            _hud.Apply(s, _settings.Current.Tray);
        else if (_hud.IsVisible) _hud.Hide();
        if (_flyout.IsVisible)
            _flyout.Apply(s, _profiles.All);
    }

    private static string BuildTip(MonitorSnapshot s, RouteSnapshot route)
    {
        if (s.Quality == LinkQuality.Down && s.OpenOutage is { } o)
            return $"Rttstat  DOWN  {NetFormat.Duration(o.Duration)}\n{s.StatusText}";
        var lines = new List<string>
        {
            $"Rttstat  {s.Profile?.Name}",
            $"ping {NetFormat.Ping(s.AggregatePingMs)} ms   loss {NetFormat.Loss(s.Loss1m)}",
            $"↓ {NetFormat.Throughput(s.RecvBps)}  ↑ {NetFormat.Throughput(s.SentBps)}"
        };
        var gw = s.Targets.FirstOrDefault(t => t.Role == TargetRole.Gateway);
        var dns = s.Targets.FirstOrDefault(t => t.Role == TargetRole.Dns);
        if (gw is not null || dns is not null)
            lines.Add($"gw {NetFormat.Ping(gw?.LastRttMs)}  dns {NetFormat.Ping(dns?.LastRttMs)}");
        if (route.Hops.Count > 0 && route.Hops[^1].LossPercent > 0)
            lines.Add($"dest loss {route.Hops[^1].LossPercent:0.0}%");
        return string.Join("\n", lines);
    }

    public void Dispose()
    {
        _hud.Close();
        _flyout.Close();
        _icon.Dispose();
        _currentIcon?.Dispose();
    }
}
