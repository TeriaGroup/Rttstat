namespace Netpulse.Core.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool FirstRun { get; set; } = true;
    public bool OpenWindowOnStart { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "ru";
    public string SelectedAdapterId { get; set; } = "";
    public bool AdapterUserChosen { get; set; }
    public int NicSampleMs { get; set; } = 500;
    public UnitSettings Units { get; set; } = new();
    public TraySettings Tray { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();
    public LibreSpeedSettings LibreSpeed { get; set; } = new();
    public RetentionSettings Retention { get; set; } = new();
    public MainWindowSettings MainWindow { get; set; } = new();
    public int MaxTtl { get; set; } = 30;
    public bool ReverseDns { get; set; }
    public bool AlertSound { get; set; }
    public bool PreferIpv6 { get; set; }
    public List<string> HiddenHops { get; set; } = [];
}

public sealed class UnitSettings
{
    public string Throughput { get; set; } = "auto";
    public int PingDecimals { get; set; }
}

public sealed class TraySettings
{
    public bool ShowPing { get; set; } = true;
    public bool ShowLoss { get; set; } = true;
    public bool ShowThroughput { get; set; } = true;
    public bool ShowOutage { get; set; } = true;
    public bool Colorize { get; set; } = true;
    public string DynamicIconMode { get; set; } = "PingAndStatus";
    public bool StripEnabled { get; set; } = true;
    public string StripMetric { get; set; } = "Ping";
    public int StripSeconds { get; set; } = 60;
    public bool FlyoutEnabled { get; set; } = true;
    public bool FlyoutOpenOnLeftClick { get; set; } = true;
    public bool HudEnabled { get; set; } = true;
    public bool HideHudInFullscreen { get; set; } = true;
}

public sealed class NotificationSettings
{
    public bool Enabled { get; set; } = true;
    public bool OnOutageStart { get; set; } = true;
    public bool OnOutageEnd { get; set; } = true;
    public bool OnPingThreshold { get; set; } = true;
    public bool OnLossThreshold { get; set; } = true;
    public bool OnSpeedtestComplete { get; set; } = true;
    public bool OnAdapterChange { get; set; }
    public bool QuietHoursEnabled { get; set; }
    public string QuietHoursFrom { get; set; } = "23:00";
    public string QuietHoursTo { get; set; } = "07:00";
    public bool MuteAll { get; set; }
}

public sealed class LibreSpeedSettings
{
    public string ServerBaseUrl { get; set; } = "https://speed.cloudflare.com";
    public int DownloadStreams { get; set; } = 4;
    public int UploadStreams { get; set; } = 4;
    public int DurationSec { get; set; } = 10;
    public string Schedule { get; set; } = "Off";
    public bool AllowMetered { get; set; }
    public bool HideBandwidthWarning { get; set; }
}

public sealed class RetentionSettings
{
    public int RawSamplesDays { get; set; } = 7;
    public int MinuteSamplesDays { get; set; } = 30;
    public int HourlySamplesDays { get; set; } = 365;
    public int DailySamplesDays { get; set; } = 1825;
    public int EventsDays { get; set; } = 365;
}

public sealed class MainWindowSettings
{
    public double Width { get; set; } = 1100;
    public double Height { get; set; } = 720;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool Maximized { get; set; }
}
