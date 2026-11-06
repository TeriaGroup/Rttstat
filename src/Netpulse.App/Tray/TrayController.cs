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
    private readonly List<BadgeSlot> _slots = [];
    private readonly HudWindow _hud = new();
    private readonly FlyoutWindow _flyout = new();
    private readonly MonitorHub _hub;
    private readonly ISettingsProvider _settings;
    private readonly SpeedtestService _speed;
    private Action _openMain = () => { };
    private Action _openSettings = () => { };
    private Action _exit = () => { };
    private double? _heldPing;
    private int _pinTries;

    public TrayController(
        MonitorHub hub,
        ISettingsProvider settings,
        SpeedtestService speed,
        Action openMain,
        Action openSettings,
        Action exit)
    {
        _hub = hub;
        _settings = settings;
        _speed = speed;
        _openMain = openMain;
        _openSettings = openSettings;
        _exit = exit;
        AddSlot();
        TrayPin.Promote();
        _hud.Clicked += ToggleFlyout;
        _flyout.Configure(settings);
        _flyout.OpenMain += openMain;
        _flyout.OpenSettings += openSettings;
        _flyout.ExitApp += exit;
        _flyout.TogglePause += () =>
        {
            _hub.Paused = !_hub.Paused;
            _hub.EmitEvent(EventLevel.Info, EventCategory.App, _hub.Paused ? "Monitoring paused" : "Monitoring resumed");
        };
        _flyout.RunSpeed += () => _ = _speed.RunNowAsync();
        _settings.Changed += () => Dispatch(RefreshFromSettings);
        hub.Updated += snap => Dispatch(() => Apply(snap));
    }

    public TaskbarIcon Icon => _slots[0].Icon;

    private ContextMenu BuildMenu(Action openMain, Action openSettings, Action exit)
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) => RebuildDynamic(menu, openMain, openSettings, exit);
        RebuildDynamic(menu, openMain, openSettings, exit);
        return menu;
    }

    private void RebuildDynamic(ContextMenu menu, Action openMain, Action openSettings, Action exit)
    {
        menu.Items.Clear();
        menu.Items.Add(Item(I18n.Current["tray.open"], openMain));
        menu.Items.Add(Item(_hub.Paused ? I18n.Current["tray.resume"] : I18n.Current["tray.pause"], () => _hub.Paused = !_hub.Paused));
        menu.Items.Add(Item(I18n.Current["tray.speed"], () => _ = _speed.RunNowAsync()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(I18n.Current["tray.settings"], openSettings));
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
        _flyout.ApplySavedSize();
        _flyout.Apply(_hub.Current);
        _flyout.PlaceNearTray();
        _flyout.Show();
        _flyout.Activate();
    }

    private void RefreshFromSettings()
    {
        foreach (var slot in _slots)
            slot.Key = "";
        if (_flyout.IsVisible)
            _flyout.ApplySavedSize();
        Apply(_hub.Current);
        if (_flyout.IsVisible)
            _flyout.PlaceNearTray();
    }

    private void Apply(MonitorSnapshot s)
    {
        var tray = _settings.Current.Tray;
        var probe = _settings.Current.Monitor;
        var tip = BuildTip(s, _hub.CurrentRoute);
        var ping = HoldPing(s);
        var wanted = new List<BadgeSpec>
        {
            new(
                IconFactory.PingText(s.Quality, ping),
                IconFactory.SeverityColor(IconFactory.PingTone(s.Quality, ping, probe.PingWarnMs, probe.PingBadMs)),
                $"{I18n.Current["ping"]}  {NetFormat.Ping(ping)} ms\n{tip}"),
            new(
                IconFactory.LossText(s.Quality, s.Loss1m),
                IconFactory.SeverityColor(IconFactory.LossTone(s.Quality, s.Loss1m, probe.LossWarnPercent, probe.LossBadPercent)),
                $"{I18n.Current["loss"]}  {NetFormat.Loss(s.Loss1m)}\n{tip}")
        };

        while (_slots.Count < wanted.Count)
            AddSlot();
        while (_slots.Count > wanted.Count)
            RemoveSlot();

        for (var i = 0; i < wanted.Count; i++)
        {
            var spec = wanted[i];
            var slot = _slots[i];
            var key = $"{spec.Text}|{spec.Color.ToArgb()}|{tray.Colorize}";
            if (slot.Key != key)
            {
                try
                {
                    var next = IconFactory.Badge(spec.Text, spec.Color, tray.Colorize);
                    slot.Icon.Icon = next;
                    slot.Image?.Dispose();
                    slot.Image = next;
                    slot.Key = key;
                }
                catch (Exception ex)
                {
                    slot.Key = "";
                    System.Diagnostics.Trace.WriteLine(ex);
                }
            }
            slot.Icon.ToolTipText = spec.Tip;
        }

        if (_pinTries < 4)
        {
            _pinTries++;
            TrayPin.Promote();
        }

        if (tray.HudEnabled)
            _hud.Apply(s, tray);
        else if (_hud.IsVisible) _hud.Hide();
        if (_flyout.IsVisible)
            _flyout.Apply(s);
    }

    private static string BuildTip(MonitorSnapshot s, RouteSnapshot route)
    {
        if (s.Quality == LinkQuality.Down && s.OpenOutage is { } o)
            return $"Rttstat  DOWN  {NetFormat.Duration(o.Duration)}\n{s.StatusText}";
        var lines = new List<string>
        {
            "Rttstat",
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

    private static void Dispatch(Action action)
    {
        var disp = Application.Current?.Dispatcher;
        if (disp is null) return;
        if (disp.CheckAccess()) action();
        else disp.BeginInvoke(action);
    }

    private double? HoldPing(MonitorSnapshot s)
    {
        if (s.AggregatePingMs is { } live)
        {
            _heldPing = live;
            return live;
        }

        var known = s.Targets.Select(t => t.LastRttMs).Where(v => v is > 0).Select(v => v!.Value).ToList();
        if (known.Count > 0)
        {
            _heldPing = known.Average();
            return _heldPing;
        }

        var spark = s.PingSpark.LastOrDefault(v => !double.IsNaN(v) && v > 0);
        if (spark > 0)
        {
            _heldPing = spark;
            return spark;
        }

        return _heldPing;
    }

    private void AddSlot()
    {
        var image = IconFactory.Badge("—", IconFactory.SeverityColor(LinkQuality.Paused));
        var icon = new TaskbarIcon
        {
            ToolTipText = "Rttstat",
            Icon = image,
            MenuActivation = PopupActivationMode.RightClick,
            ContextMenu = BuildMenu(_openMain, _openSettings, _exit)
        };
        icon.TrayLeftMouseUp += (_, _) =>
        {
            if (_settings.Current.Tray.FlyoutOpenOnLeftClick && _settings.Current.Tray.FlyoutEnabled)
                ToggleFlyout();
            else _openMain();
        };
        icon.TrayMouseDoubleClick += (_, _) => _openMain();
        _slots.Add(new BadgeSlot(icon, image));
    }

    private void RemoveSlot()
    {
        if (_slots.Count <= 1) return;
        var slot = _slots[^1];
        _slots.RemoveAt(_slots.Count - 1);
        slot.Icon.Icon = null;
        slot.Icon.Dispose();
        slot.Image?.Dispose();
    }

    public void Dispose()
    {
        _hud.Close();
        _flyout.Close();
        foreach (var slot in _slots)
        {
            slot.Icon.Icon = null;
            slot.Icon.Dispose();
            slot.Image?.Dispose();
        }
        _slots.Clear();
    }

    private sealed class BadgeSlot(TaskbarIcon icon, Icon image)
    {
        public TaskbarIcon Icon { get; } = icon;
        public Icon? Image { get; set; } = image;
        public string Key { get; set; } = "";
    }

    private readonly record struct BadgeSpec(string Text, Color Color, string Tip);
}
