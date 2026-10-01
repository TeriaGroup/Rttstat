using Hardcodet.Wpf.TaskbarNotification;
using Netpulse.App.Loc;
using Netpulse.Core.Abstractions;
using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;

namespace Netpulse.App.Services;

public sealed class NotificationSink : INotificationSink
{
    private readonly ISettingsProvider _settings;
    private readonly Dictionary<EventCategory, DateTimeOffset> _last = [];
    public TaskbarIcon? Icon { get; set; }

    public NotificationSink(ISettingsProvider settings, MonitorHub hub)
    {
        _settings = settings;
        hub.EventRaised += OnEvent;
        hub.OutageChanged += OnOutage;
        hub.SpeedtestCompleted += OnSpeed;
    }

    public void Show(string title, string body, EventCategory category)
    {
        var n = _settings.Current.Notifications;
        if (!n.Enabled || n.MuteAll) return;
        if (InQuietHours(n)) return;
        if (!Allowed(n, category)) return;
        if (_last.TryGetValue(category, out var t) && DateTimeOffset.UtcNow - t < TimeSpan.FromMinutes(10)
            && category is EventCategory.Threshold or EventCategory.Ping)
            return;
        _last[category] = DateTimeOffset.UtcNow;
        Icon?.ShowBalloonTip(title, body, BalloonIcon.Info);
        if (_settings.Current.AlertSound)
        {
            try { System.Media.SystemSounds.Exclamation.Play(); } catch { /* ignore */ }
        }
    }

    private void OnEvent(AppEvent e)
    {
        if (e.Category is EventCategory.Threshold)
            Show("Rttstat", e.Message, e.Category);
        if (e.Category is EventCategory.Adapter && _settings.Current.Notifications.OnAdapterChange)
            Show("Rttstat", e.Message, e.Category);
        if (e.Category is EventCategory.Ping && e.Message.Contains("ICMP", StringComparison.OrdinalIgnoreCase))
            Show("Rttstat", e.Message, e.Category);
        if (e.Category is EventCategory.Route)
            Show("Rttstat", e.Message, e.Category);
    }

    private void OnOutage(Outage o)
    {
        if (o.IsOpen)
            Show(I18n.Current["toast.outage"], $"{o.Cause}: {o.Detail}", EventCategory.Outage);
        else
            Show(I18n.Current["toast.up"], string.Format(I18n.Current["toast.dur"], o.Duration.ToString(@"hh\:mm\:ss")), EventCategory.Outage);
    }

    private void OnSpeed(SpeedtestResult r)
    {
        if (r.Error is not null)
            Show(I18n.Current["toast.speed.fail"], r.Error, EventCategory.Speedtest);
        else
            Show(I18n.Current["toast.speed.ok"], $"↓ {r.DownloadMbps:0.00}  ↑ {r.UploadMbps:0.00} Mbps", EventCategory.Speedtest);
    }

    private static bool Allowed(NotificationSettings n, EventCategory c) => c switch
    {
        EventCategory.Outage => n.OnOutageStart || n.OnOutageEnd,
        EventCategory.Threshold => n.OnPingThreshold || n.OnLossThreshold,
        EventCategory.Speedtest => n.OnSpeedtestComplete,
        EventCategory.Adapter => n.OnAdapterChange,
        EventCategory.Route => n.OnPingThreshold,
        _ => true
    };

    private static bool InQuietHours(NotificationSettings n)
    {
        if (!n.QuietHoursEnabled) return false;
        if (!TimeSpan.TryParse(n.QuietHoursFrom, out var from) || !TimeSpan.TryParse(n.QuietHoursTo, out var to))
            return false;
        var now = DateTime.Now.TimeOfDay;
        return from <= to ? now >= from && now <= to : now >= from || now <= to;
    }
}
