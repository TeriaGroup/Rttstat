using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Netpulse.Core.Abstractions;
using UiLoc = Netpulse.App.Loc.I18n;
using Netpulse.Core.Formatting;
using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;
using Netpulse.Core.Speedtest;
using Netpulse.Infrastructure.Config;
using Netpulse.Infrastructure.Sqlite;
using Netpulse.Infrastructure.Windows;

namespace Netpulse.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly MonitorHub _hub;
    private readonly ISettingsProvider _settings;
    private readonly IProfileProvider _profiles;
    private readonly SampleRepository _repo;
    private readonly SpeedtestService _speed;
    private readonly IAppPaths _paths;
    private DateTimeOffset _uiTick;

    public MainViewModel(
        MonitorHub hub,
        ISettingsProvider settings,
        IProfileProvider profiles,
        SampleRepository repo,
        SpeedtestService speed,
        IAppPaths paths)
    {
        _hub = hub;
        _settings = settings;
        _profiles = profiles;
        _repo = repo;
        _speed = speed;
        _paths = paths;
        Settings = settings.Current;
        UiLoc.Current.SetLanguage(Settings.Language);
        hub.Updated += s =>
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _uiTick < TimeSpan.FromMilliseconds(250)) return;
            _uiTick = now;
            var d = Application.Current?.Dispatcher;
            if (d is null) return;
            d.BeginInvoke(() => Apply(s));
        };
        ReloadLog();
        ReloadSpeed();
        RefreshStats();
        RefreshAdapters();
        RefreshProfiles();
    }

    [ObservableProperty] private string _section = "Dashboard";
    [ObservableProperty] private string _statusText = "Starting";
    [ObservableProperty] private string _qualityText = "…";
    [ObservableProperty] private string _pingText = "—";
    [ObservableProperty] private string _lossText = "—";
    [ObservableProperty] private string _jitterText = "—";
    [ObservableProperty] private string _downText = "—";
    [ObservableProperty] private string _upText = "—";
    [ObservableProperty] private string _speedText = "—";
    [ObservableProperty] private string _banner = "";
    [ObservableProperty] private string _adapterLabel = "";
    [ObservableProperty] private string _onlineText = "";
    [ObservableProperty] private string _logSearch = "";
    [ObservableProperty] private string _logCategory = "All";
    [ObservableProperty] private string _statsBucket = "Day";
    [ObservableProperty] private string _statsSummary = "";
    [ObservableProperty] private string _newTargetHost = "";
    [ObservableProperty] private string _newTargetName = "";
    [ObservableProperty] private Profile? _selectedProfile;
    [ObservableProperty] private AdapterChoice? _selectedAdapter;
    private bool _suppressAdapter;
    [ObservableProperty] private IReadOnlyList<double> _pingSpark = [];
    [ObservableProperty] private IReadOnlyList<double> _downSpark = [];
    [ObservableProperty] private IReadOnlyList<double> _upSpark = [];
    [ObservableProperty] private IReadOnlyList<double> _lossSpark = [];
    [ObservableProperty] private IReadOnlyList<double> _statsSpark = [];

    public AppSettings Settings { get; }
    public ObservableCollection<TargetLiveState> Targets { get; } = [];
    public ObservableCollection<LogRow> LogRows { get; } = [];
    public ObservableCollection<SpeedtestResult> Speedtests { get; } = [];
    public ObservableCollection<Profile> Profiles { get; } = [];
    public ObservableCollection<AdapterChoice> Adapters { get; } = [];
    public IReadOnlyList<IdLabel> CategoryOptions =>
    [
        new("All", UiLoc.Current["cat.All"]),
        new("App", UiLoc.Current["cat.App"]),
        new("Outage", UiLoc.Current["cat.Outage"]),
        new("Ping", UiLoc.Current["cat.Ping"]),
        new("Threshold", UiLoc.Current["cat.Threshold"]),
        new("Adapter", UiLoc.Current["cat.Adapter"]),
        new("Speedtest", UiLoc.Current["cat.Speedtest"]),
        new("Profile", UiLoc.Current["cat.Profile"]),
        new("Settings", UiLoc.Current["cat.Settings"]),
    ];
    public IReadOnlyList<IdLabel> BucketOptions =>
    [
        new("Hour", UiLoc.Current["bucket.Hour"]),
        new("Day", UiLoc.Current["bucket.Day"]),
        new("Week", UiLoc.Current["bucket.Week"]),
    ];
    public IReadOnlyList<string> StripMetrics { get; } = ["Ping", "Loss", "Down"];
    public IReadOnlyList<string> Schedules { get; } = ["Off", "Every 6", "Every 12", "04:00"];
    public UiLoc Ui => UiLoc.Current;

    public string SelectedLanguage
    {
        get => UiLoc.Current.Language;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == UiLoc.Current.Language) return;
            UiLoc.Current.SetLanguage(value);
            Settings.Language = value;
            _settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(BucketOptions));
            OnPropertyChanged(nameof(CategoryOptions));
            Apply(_hub.Current);
            RefreshStats();
        }
    }

    private void Apply(MonitorSnapshot s)
    {
        StatusText = UiLoc.Current[s.StatusText];
        QualityText = UiLoc.Current["q." + s.Quality];
        PingText = $"{NetFormat.Ping(s.AggregatePingMs)} ms";
        LossText = NetFormat.Loss(s.Loss1m);
        JitterText = $"{s.AggregateJitterMs:0.0} ms";
        DownText = NetFormat.Throughput(s.RecvBps, Settings.Units.Throughput);
        UpText = NetFormat.Throughput(s.SentBps, Settings.Units.Throughput);
        AdapterLabel = s.AdapterName;
        OnlineText = s.OpenOutage is { } o
            ? string.Format(UiLoc.Current["outage.for"], NetFormat.Duration(o.Duration))
            : string.Format(UiLoc.Current["online.for"], NetFormat.Duration(s.OnlineFor));
        Banner = s.Banner;
        PingSpark = s.PingSpark;
        DownSpark = s.DownSpark.Select(v => v * 8 / 1_000_000.0).ToList();
        UpSpark = s.UpSpark.Select(v => v * 8 / 1_000_000.0).ToList();
        LossSpark = s.LossSpark;
        if (s.LastSpeedtest is { Error: null } st)
            SpeedText = $"↓ {st.DownloadMbps:0.00}  ↑ {st.UploadMbps:0.00} Mbps";
        else if (s.SpeedtestPhase is not SpeedtestPhase.Idle)
            SpeedText = $"{s.SpeedtestPhase} {s.SpeedtestLiveMbps:0.00} Mbps";
        Targets.Clear();
        foreach (var t in s.Targets) Targets.Add(t);
    }

    [RelayCommand] private void Go(string section) => Section = section;

    [RelayCommand]
    private void Pause()
    {
        _hub.Paused = !_hub.Paused;
        _hub.EmitEvent(EventLevel.Info, EventCategory.App, _hub.Paused ? "Monitoring paused" : "Monitoring resumed");
    }

    [RelayCommand]
    private async Task RunSpeedAsync()
    {
        if (!_settings.Current.LibreSpeed.HideBandwidthWarning)
        {
            var r = MessageBox.Show(
                UiLoc.Current["msg.speed.warn"],
                "Rttstat", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            _settings.Current.LibreSpeed.HideBandwidthWarning = true;
            _settings.Save();
        }
        await _speed.RunNowAsync(true);
        ReloadSpeed();
    }

    [RelayCommand] private void CancelSpeed() => _speed.Cancel();

    [RelayCommand]
    private void SaveSettings()
    {
        AutostartService.Apply(Settings.StartWithWindows);
        _settings.Save();
        _hub.EmitEvent(EventLevel.Info, EventCategory.Settings, "Settings saved");
    }

    [RelayCommand]
    private void OpenDataFolder() => System.Diagnostics.Process.Start("explorer.exe", _paths.Root);

    [RelayCommand]
    private void ReloadLog()
    {
        LogRows.Clear();
        var to = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var from = to - (long)TimeSpan.FromDays(30).TotalMilliseconds;
        foreach (var e in _repo.QueryEvents(from, to, LogSearch, LogCategory))
        {
            LogRows.Add(new LogRow(
                DateTimeOffset.FromUnixTimeMilliseconds(e.Ts).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                e.Level.ToString(),
                e.Category.ToString(),
                e.Message));
        }
    }

    [RelayCommand]
    private void ExportLog()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "CSV|*.csv", FileName = "rttstat-log.csv" };
        if (dlg.ShowDialog() != true) return;
        var lines = new List<string> { "Time,Level,Category,Message" };
        lines.AddRange(LogRows.Select(r => $"{r.Time},{r.Level},{r.Category},\"{r.Message.Replace("\"", "\"\"")}\""));
        File.WriteAllLines(dlg.FileName, lines);
    }

    [RelayCommand]
    private void RefreshStats()
    {
        var now = DateTimeOffset.Now;
        DateTimeOffset from = StatsBucket switch
        {
            "Hour" => now.AddHours(-1),
            "Week" => StartOfWeek(now),
            _ => now.Date
        };
        var row = _repo.QueryStats(from.ToUniversalTime().ToUnixTimeMilliseconds(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), null);
        var tot = TimeSpan.FromMilliseconds(row.OutageTotalMs).ToString(@"hh\:mm\:ss");
        var longest = TimeSpan.FromMilliseconds(row.LongestMs).ToString(@"hh\:mm\:ss");
        StatsSummary =
            string.Format(UiLoc.Current["stats.ping"], $"{row.Avg:0.0}", $"{row.Min:0}", $"{row.Max:0}") + "\n" +
            string.Format(UiLoc.Current["stats.loss"], $"{row.Loss:0.00}", row.Ok, row.Fail) + "\n" +
            string.Format(UiLoc.Current["stats.outages"], row.OutageCount, tot, longest) + "\n" +
            string.Format(UiLoc.Current["stats.traffic"], $"{row.BytesRecv / 1_000_000.0:0.0}", $"{row.BytesSent / 1_000_000.0:0.0}") + "\n" +
            string.Format(UiLoc.Current["stats.speed"], row.Speedtests, $"{row.BestDown:0.00}", $"{row.BestUp:0.00}");
        var bucket = StatsBucket == "Week" ? "day" : StatsBucket == "Hour" ? "minute" : "hour";
        var series = _repo.QueryPingSeries(from.ToUniversalTime().ToUnixTimeMilliseconds(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), bucket);
        StatsSpark = series.Select(p => p.AvgPing).ToList();
    }

    [RelayCommand]
    private void ExportStats()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "CSV|*.csv", FileName = "rttstat-stats.csv" };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, StatsSummary.Replace("\n", "\r\n"));
    }

    [RelayCommand]
    private void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (var p in _profiles.All) Profiles.Add(p);
        SelectedProfile = Profiles.FirstOrDefault(p => p.IsActive);
    }

    [RelayCommand]
    private void ActivateProfile()
    {
        if (SelectedProfile is null) return;
        _profiles.SetActive(SelectedProfile.Id);
        _hub.EmitEvent(EventLevel.Info, EventCategory.Profile, "Active profile: " + SelectedProfile.Name);
        RefreshProfiles();
    }

    [RelayCommand]
    private void SaveProfile()
    {
        if (SelectedProfile is null) return;
        _profiles.Upsert(SelectedProfile);
    }

    [RelayCommand]
    private void AddTarget()
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(NewTargetHost)) return;
        SelectedProfile.Targets.Add(new Target
        {
            DisplayName = string.IsNullOrWhiteSpace(NewTargetName) ? NewTargetHost : NewTargetName,
            Host = NewTargetHost.Trim(),
            Role = TargetRole.Custom
        });
        _profiles.Upsert(SelectedProfile);
        NewTargetHost = "";
        NewTargetName = "";
        RefreshProfiles();
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile is null) return;
        _profiles.Delete(SelectedProfile.Id);
        RefreshProfiles();
    }

    [RelayCommand]
    private void DuplicateProfile()
    {
        if (SelectedProfile is null) return;
        var copy = System.Text.Json.JsonSerializer.Deserialize<Profile>(
            System.Text.Json.JsonSerializer.Serialize(SelectedProfile, Netpulse.Core.Json.JsonDefaults.Options),
            Netpulse.Core.Json.JsonDefaults.Options)!;
        copy.Id = Guid.NewGuid();
        copy.Name += " copy";
        copy.IsActive = false;
        foreach (var t in copy.Targets) t.Id = Guid.NewGuid();
        _profiles.Upsert(copy);
        RefreshProfiles();
    }

    [RelayCommand]
    private void DetectGateway()
    {
        var a = GatewayDetector.PickDefault(Settings.SelectedAdapterId, Settings.AdapterUserChosen);
        if (a is null || SelectedProfile is null) return;
        var gw = SelectedProfile.Targets.FirstOrDefault(t => t.Role == TargetRole.Gateway);
        if (gw is null)
        {
            gw = new Target { DisplayName = "Gateway", Role = TargetRole.Gateway };
            SelectedProfile.Targets.Add(gw);
        }
        gw.Host = a.Gateway;
        gw.Enabled = !string.IsNullOrEmpty(a.Gateway);
        _profiles.Upsert(SelectedProfile);
        RefreshProfiles();
    }

    [RelayCommand]
    private void RefreshAdapters()
    {
        _suppressAdapter = true;
        Adapters.Clear();
        foreach (var a in GatewayDetector.ListAdapters(true))
        {
            var tag = a.Virtual ? "VPN" : a.Up ? "Up" : "Down";
            Adapters.Add(new AdapterChoice(a.Id, $"{a.Name}  {a.Ipv4}  {tag}"));
        }
        SelectedAdapter = Adapters.FirstOrDefault(a => a.Id == Settings.SelectedAdapterId) ?? Adapters.FirstOrDefault();
        _suppressAdapter = false;
    }

    partial void OnSelectedAdapterChanged(AdapterChoice? value)
    {
        if (_suppressAdapter || value is null) return;
        Settings.SelectedAdapterId = value.Id;
        Settings.AdapterUserChosen = true;
        _settings.Save();
    }

    [RelayCommand]
    private void ReloadSpeed()
    {
        Speedtests.Clear();
        foreach (var s in _repo.QuerySpeedtests())
            Speedtests.Add(s);
    }

    [RelayCommand]
    private void Vacuum()
    {
        _repo.ApplyRetention(Settings.Retention);
        _repo.Vacuum();
        MessageBox.Show(UiLoc.Current["msg.db"], "Rttstat");
    }

    [RelayCommand]
    private void ResetSettings()
    {
        var fresh = new AppSettings();
        JsonStore.Save(_paths.SettingsFile, fresh);
        _settings.Reload();
        MessageBox.Show(UiLoc.Current["msg.reset"], "Rttstat");
    }

    private static DateTimeOffset StartOfWeek(DateTimeOffset now)
    {
        var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
        return now.Date.AddDays(-diff);
    }
}

public sealed record LogRow(string Time, string Level, string Category, string Message);
public sealed record AdapterChoice(string Id, string Label)
{
    public override string ToString() => Label;
}
public sealed record IdLabel(string Id, string Label)
{
    public override string ToString() => Label;
}
