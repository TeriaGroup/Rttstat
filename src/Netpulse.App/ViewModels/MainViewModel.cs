using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using Netpulse.App.Controls;
using Netpulse.Core.Abstractions;
using UiLoc = Netpulse.App.Loc.I18n;
using Netpulse.Core.Formatting;
using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;
using Netpulse.Core.Reports;
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
    private DateTimeOffset _lossTick;
    private string _lossTabId = "all";

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
        ModeButtonText = UiLoc.Current["nav.settings"];
        hub.RouteUpdated += r =>
        {
            var d = Application.Current?.Dispatcher;
            if (d is null) return;
            d.BeginInvoke(() => ApplyRoute(r));
        };
    }

    [ObservableProperty] private string _section = "Monitor";
    [ObservableProperty] private string _period = "Minute";
    [ObservableProperty] private bool _lossAll;
    [ObservableProperty] private string _diagnosisText = "";
    [ObservableProperty] private string _detailLine = "";
    [ObservableProperty] private string _selectedTitle = "";
    [ObservableProperty] private bool _hasHiddenHops;
    [ObservableProperty] private string _modeButtonText = "";
    [ObservableProperty] private double _chartWindowSeconds = 120;
    [ObservableProperty] private IReadOnlyList<ChartSeries> _pingSeries = [];
    [ObservableProperty] private TargetRow? _selectedRow;
    [ObservableProperty] private string _historyPane = "metrics";
    [ObservableProperty] private string _statusText = "Starting";
    [ObservableProperty] private string _qualityText = "…";
    [ObservableProperty] private string _pingText = "—";
    [ObservableProperty] private string _lossText = "—";
    [ObservableProperty] private string _jitterText = "—";
    [ObservableProperty] private string _minText = "—";
    [ObservableProperty] private string _avgText = "—";
    [ObservableProperty] private string _maxText = "—";
    [ObservableProperty] private string _mosText = "—";
    [ObservableProperty] private string _loss5Text = "—";
    [ObservableProperty] private string _loss1hText = "—";
    [ObservableProperty] private string _quickHost = "";
    [ObservableProperty] private string _lossBucket = "Hour";
    [ObservableProperty] private bool _lossCombined = true;
    [ObservableProperty] private bool _lossEmpty;
    [ObservableProperty] private double _lossWindowSeconds = 3600;
    [ObservableProperty] private IReadOnlyList<ChartSeries> _lossSeries = [];
    [ObservableProperty] private string _downText = "—";
    [ObservableProperty] private string _upText = "—";
    [ObservableProperty] private string _speedText = "—";
    [ObservableProperty] private string _banner = "";
    [ObservableProperty] private string _adapterLabel = "";
    [ObservableProperty] private string _onlineText = "";
    [ObservableProperty] private string _logSearch = "";
    [ObservableProperty] private string _logCategory = "All";
    [ObservableProperty] private string _statsBucket = "Hour";
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
    [ObservableProperty] private IReadOnlyList<double> _statsPingSpark = [];
    [ObservableProperty] private IReadOnlyList<double> _statsLossSpark = [];
    [ObservableProperty] private IReadOnlyList<double> _statsDownSpark = [];
    [ObservableProperty] private IReadOnlyList<double> _statsUpSpark = [];
    [ObservableProperty] private double _statsWindowSeconds = 86400;
    [ObservableProperty] private string _routeHost = "";
    [ObservableProperty] private string _routeBanner = "";
    [ObservableProperty] private bool _routeTable = true;
    [ObservableProperty] private IReadOnlyList<IReadOnlyList<double?>> _routeHeat = [];

    public AppSettings Settings { get; }
    public ObservableCollection<TargetLiveState> Targets { get; } = [];
    public ObservableCollection<TargetRow> Rows { get; } = [];
    public ObservableCollection<OutageStatRow> PeriodOutages { get; } = [];
    public ObservableCollection<LogRow> LogRows { get; } = [];
    public ObservableCollection<SpeedtestResult> Speedtests { get; } = [];
    public ObservableCollection<Profile> Profiles { get; } = [];
    public ObservableCollection<AdapterChoice> Adapters { get; } = [];
    public ObservableCollection<PingStatRow> StatsPingRows { get; } = [];
    public ObservableCollection<LossStatRow> StatsLossRows { get; } = [];
    public ObservableCollection<RateStatRow> StatsDownRows { get; } = [];
    public ObservableCollection<RateStatRow> StatsUpRows { get; } = [];
    public ObservableCollection<OutageStatRow> StatsOutageRows { get; } = [];
    public ObservableCollection<HopLiveState> RouteHops { get; } = [];
    public ObservableCollection<LossTabItem> LossTabs { get; } = [];
    public ObservableCollection<LossStatRow> LossDetailRows { get; } = [];
    public ObservableCollection<LossSummaryRow> LossSummaryRows { get; } = [];
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
        new("Minute", UiLoc.Current["bucket.Minute"]),
        new("Hour", UiLoc.Current["bucket.Hour"]),
        new("Day", UiLoc.Current["bucket.Day"]),
        new("Week", UiLoc.Current["bucket.Week"]),
    ];
    public IReadOnlyList<string> StripMetrics { get; } = ["Ping", "Loss", "Down"];
    public IReadOnlyList<string> Schedules { get; } = ["Off", "Every 6", "Every 12", "04:00"];
    public UiLoc Ui => UiLoc.Current;
    public string PingUnit => UiLoc.Current.Language == "en" ? " ms" : " мс";

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
            OnPropertyChanged(nameof(PingUnit));
            OnPropertyChanged(nameof(CategoryOptions));
            Apply(_hub.Current);
            ApplyRoute(_hub.CurrentRoute);
            ModeButtonText = Section == "Settings" ? UiLoc.Current["nav.monitor"] : UiLoc.Current["nav.settings"];
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
        MinText = $"{NetFormat.Ping(s.AggregateMinMs)} ms";
        AvgText = $"{NetFormat.Ping(s.AggregateAvgMs)} ms";
        MaxText = $"{NetFormat.Ping(s.AggregateMaxMs)} ms";
        MosText = s.AggregatePingMs is { } ping
            ? RoutePolicy.MosEstimate(ping, s.Loss1m).ToString("0.00")
            : "—";
        Loss5Text = NetFormat.Loss(s.Loss5m);
        Loss1hText = NetFormat.Loss(s.Loss1h);
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
            SpeedText = $"↓ {st.DownloadMbps:0.00}  ↑ {st.UploadMbps:0.00} Mbps  {st.BloatGrade}";
        else if (s.SpeedtestPhase is not SpeedtestPhase.Idle)
            SpeedText = $"{s.SpeedtestPhase} {s.SpeedtestLiveMbps:0.00} Mbps";
        SyncRows(s);
    }

    partial void OnSelectedRowChanged(TargetRow? value)
    {
        _hub.SetTraceTarget(value?.Id);
        SelectedTitle = value?.Name ?? "";
        RefreshDetailLine();
        RefreshDetail();
        ApplyRoute(_hub.CurrentRoute);
    }

    partial void OnPeriodChanged(string value) => RefreshDetail();

    partial void OnLossAllChanged(bool value) => RefreshDetail();

    [RelayCommand]
    private void SetLossMode(string? mode)
    {
        LossAll = mode == "all";
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        Section = Section == "Settings" ? "Monitor" : "Settings";
        ModeButtonText = Section == "Settings" ? UiLoc.Current["nav.monitor"] : UiLoc.Current["nav.settings"];
    }

    [RelayCommand]
    private void CopyReport()
    {
        var row = SelectedRow;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Rttstat");
        sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm}  {PeriodLabel()}");
        sb.AppendLine($"Target: {row?.Name} {row?.Host} {row?.Ip}");
        sb.AppendLine($"Ping {row?.Rtt}  loss 5m {row?.Loss}  {DiagnosisText}");
        sb.AppendLine();
        sb.AppendLine("Hops");
        if (RouteHops.Count == 0) sb.AppendLine("(none)");
        else
        {
            foreach (var h in RouteHops)
                sb.AppendLine($"{h.Hop}\t{h.Ip}\t{h.LossPercent:0.0}\t{h.AvgRtt:0.0}");
        }
        sb.AppendLine();
        sb.AppendLine("Outages");
        if (PeriodOutages.Count == 0) sb.AppendLine("(none)");
        else
        {
            foreach (var o in PeriodOutages)
                sb.AppendLine($"{o.Start}\t{o.Duration}\t{o.Cause}");
        }
        Clipboard.SetText(sb.ToString());
    }

    private void SyncRows(MonitorSnapshot s)
    {
        var seen = new HashSet<Guid>();
        foreach (var t in s.Targets)
        {
            seen.Add(t.TargetId);
            var row = Rows.FirstOrDefault(r => r.Id == t.TargetId);
            if (row is null)
            {
                row = new TargetRow { Id = t.TargetId, Swatch = Freeze(Palette[Rows.Count % Palette.Length]) };
                Rows.Add(row);
            }
            row.Name = t.DisplayName;
            row.Host = t.Host;
            row.Ip = t.ResolvedIp;
            row.Rtt = NetFormat.Ping(t.LastRttMs);
            row.Loss = NetFormat.Loss(t.Loss5m);
            row.Spark = t.Spark;
            row.PingSpark = t.Spark;
            row.LossSpark = t.LossSpark;
            row.Min = t.MinRtt5m;
            row.Avg = t.AvgRtt5m;
            row.Max = t.MaxRtt5m;
            row.Jitter = t.JitterMs;
            row.AvgText = NetFormat.Ping(t.AvgRtt5m);
            row.MinText = NetFormat.Ping(t.MinRtt5m);
            row.MaxText = NetFormat.Ping(t.MaxRtt5m);
            row.JitterText = $"{t.JitterMs:0.0}";
            row.SentText = t.Sent.ToString();
            row.RecvText = t.Recv.ToString();
        }
        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (seen.Contains(Rows[i].Id)) continue;
            if (ReferenceEquals(SelectedRow, Rows[i])) SelectedRow = null;
            Rows.RemoveAt(i);
        }
        if (SelectedRow is null && Rows.Count > 0)
            SelectedRow = Rows[0];
        else if (Period == "Minute")
            ShowLiveCharts();
        RefreshDetailLine();
    }

    private void RefreshDetailLine()
    {
        var row = SelectedRow;
        SelectedTitle = row?.Name ?? "";
        if (row is null)
        {
            DetailLine = "";
            return;
        }
        DetailLine = $"{UiLoc.Current["col.min"]} {NetFormat.Ping(row.Min)}   {UiLoc.Current["col.avg"]} {NetFormat.Ping(row.Avg)}   {UiLoc.Current["col.max"]} {NetFormat.Ping(row.Max)}   {UiLoc.Current["jitter"]} {row.Jitter:0.0} ms";
    }

    private void RefreshDetail()
    {
        if (Period == "Minute")
        {
            ShowLiveCharts();
            LoadOutages();
            return;
        }
        LoadHistoryCharts();
        LoadOutages();
    }

    private void ShowLiveCharts()
    {
        ChartWindowSeconds = 120;
        if (Rows.Count == 0)
        {
            PingSeries = [];
            LossSeries = [];
            return;
        }
        PingSeries = AllLive(r => r.PingSpark);
        LossSeries = AllLive(r => r.LossSpark);
    }

    private List<ChartSeries> AllLive(Func<TargetRow, IReadOnlyList<double>> pick)
        => Rows.Select(r => Series(r.Name, r.Swatch, SeriesAlign.Right(pick(r), 40), ReferenceEquals(r, SelectedRow) ? 2.8 : 1.5)).ToList();

    private void LoadHistoryCharts()
    {
        var from = PeriodFrom();
        var bucketMs = Period == "Week" ? 86_400_000L : Period == "Day" ? 3_600_000L : 60_000L;
        var fromMs = from.ToUniversalTime().ToUnixTimeMilliseconds();
        var toMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ChartWindowSeconds = Math.Max(60, (toMs - fromMs) / 1000.0);
        if (Rows.Count == 0)
        {
            PingSeries = [];
            LossSeries = [];
            return;
        }
        var row = SelectedRow;
        var times = Buckets(fromMs, toMs, bucketMs);
        try
        {
            var profileId = _profiles.Active.Id.ToString();
            var allLoss = _repo.QueryLossByTarget(fromMs, toMs, bucketMs, profileId);
            var lossBy = allLoss.GroupBy(p => p.TargetId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.GroupBy(p => p.Ts).ToDictionary(x => x.Key, x => x.Last().Loss), StringComparer.OrdinalIgnoreCase);
            var pingList = new List<ChartSeries>();
            var lossList = new List<ChartSeries>();
            foreach (var r in Rows)
            {
                var pts = _repo.QueryTargetSeries(fromMs, toMs, bucketMs, profileId, r.Id.ToString());
                var pingMap = pts.Where(p => p.AvgPing is not null).GroupBy(p => p.Ts).ToDictionary(g => g.Key, g => g.Last().AvgPing!.Value);
                lossBy.TryGetValue(r.Id.ToString(), out var lossMap);
                var thick = ReferenceEquals(r, row) ? 2.8 : 1.5;
                pingList.Add(Series(r.Name, r.Swatch, PathDiagnosis.Align(times, pingMap), thick));
                lossList.Add(Series(r.Name, r.Swatch, PathDiagnosis.Align(times, lossMap ?? new Dictionary<long, double>()), thick));
            }
            PingSeries = pingList;
            LossSeries = lossList;
        }
        catch
        {
            PingSeries = [];
            LossSeries = [];
        }
    }

    private void LoadOutages()
    {
        PeriodOutages.Clear();
        var fromMs = PeriodFrom().ToUniversalTime().ToUnixTimeMilliseconds();
        var toMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var o in _repo.QueryOutages(fromMs, toMs))
        {
            var start = DateTimeOffset.FromUnixTimeMilliseconds(o.StartedTs).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            PeriodOutages.Add(new OutageStatRow(start, "", TimeSpan.FromMilliseconds(o.DurationMs).ToString(@"hh\:mm\:ss"), o.Cause));
        }
    }

    private DateTimeOffset PeriodFrom()
    {
        var now = DateTimeOffset.Now;
        return Period switch
        {
            "Hour" => now.AddHours(-1),
            "Week" => StartOfWeek(now),
            "Day" => now.Date,
            _ => now.AddMinutes(-30)
        };
    }

    private static List<long> Buckets(long fromMs, long toMs, long bucketMs)
    {
        var list = new List<long>();
        if (bucketMs <= 0) return list;
        var start = fromMs / bucketMs * bucketMs;
        var end = toMs / bucketMs * bucketMs;
        for (var t = start; t <= end && list.Count < 2000; t += bucketMs)
            list.Add(t);
        return list;
    }

    private string PeriodLabel() => Period switch
    {
        "Hour" => UiLoc.Current["bucket.Hour"],
        "Day" => UiLoc.Current["bucket.Day"],
        "Week" => UiLoc.Current["bucket.Week"],
        _ => UiLoc.Current["bucket.Minute"]
    };

    private static ChartSeries Series(string name, string hex, IReadOnlyList<double> values, double thickness = 1.5) => new()
    {
        Name = name,
        Stroke = Freeze(hex),
        Thickness = thickness,
        Values = values
    };

    private static ChartSeries Series(string name, Brush stroke, IReadOnlyList<double> values, double thickness) => new()
    {
        Name = name,
        Stroke = stroke,
        Thickness = thickness,
        Values = values
    };

    private static string FormatDiagnosis(PathDiagnosisResult d)
    {
        var line = d.Code switch
        {
            "healthy" => UiLoc.Current["diag.healthy"],
            "home" => string.Format(UiLoc.Current["diag.home"], d.LossHop),
            "path" => string.Format(UiLoc.Current["diag.path"], d.LossHop),
            "destination" => UiLoc.Current["diag.dest"],
            _ => UiLoc.Current["diag.tracing"]
        };
        if (d.JumpHop is int hop)
            line += "  " + string.Format(UiLoc.Current["diag.jump"], hop);
        return line;
    }

    [RelayCommand]
    private void Go(string section)
    {
        Section = section;
        if (section is "History" or "Statistics")
        {
            HistoryPane = "metrics";
            RefreshStats();
        }
        if (section == "Loss") RefreshLoss();
    }

    partial void OnLossBucketChanged(string value) => RefreshLoss();

    [RelayCommand]
    private void SelectLossTab(string? id)
    {
        _lossTabId = string.IsNullOrWhiteSpace(id) ? "all" : id;
        RefreshLoss();
    }

    [RelayCommand]
    private void Hist(string pane)
    {
        Section = "History";
        HistoryPane = pane;
        if (pane == "metrics") RefreshStats();
        if (pane == "log") ReloadLog();
        if (pane == "speed") ReloadSpeed();
    }

    partial void OnStatsBucketChanged(string value) => RefreshStats();

    private void ApplyRoute(RouteSnapshot r)
    {
        RouteHost = r.TargetHost;
        if (SelectedRow is null || r.TargetId != SelectedRow.Id)
        {
            if (RouteHops.Count > 0) RouteHops.Clear();
            DiagnosisText = UiLoc.Current["diag.tracing"];
            HasHiddenHops = false;
            return;
        }
        var note = UiLoc.Current["route.note.icmp"];
        Settings.HiddenHops ??= [];
        var prefix = SelectedRow.Id.ToString("N") + "|";
        HasHiddenHops = Settings.HiddenHops.Any(k => k.StartsWith(prefix, StringComparison.Ordinal));
        var visible = r.Hops.Where(h => !HopHide.IsHidden(Settings.HiddenHops, SelectedRow.Id, h.Hop, h.Ip)).ToList();
        foreach (var h in visible)
            h.Note = h.IntermediateOnlyLoss ? note : "";
        if (RouteHops.Count == visible.Count && RouteHops.Select(h => h.Ip).SequenceEqual(visible.Select(h => h.Ip)))
        {
            for (var i = 0; i < visible.Count; i++)
                RouteHops[i] = visible[i];
        }
        else
        {
            RouteHops.Clear();
            foreach (var h in visible) RouteHops.Add(h);
        }
        DiagnosisText = FormatDiagnosis(PathDiagnosis.Diagnose(r.Hops, !r.Tracing && r.Hops.Count > 0));
        if (r.Banner == "no-ipv6")
            DiagnosisText = UiLoc.Current["no.ipv6"];
    }

    [RelayCommand]
    private void HideHop(HopLiveState? hop)
    {
        if (hop is null || SelectedRow is null) return;
        Settings.HiddenHops ??= [];
        var key = HopHide.Key(SelectedRow.Id, hop.Hop, hop.Ip);
        if (!Settings.HiddenHops.Contains(key))
            Settings.HiddenHops.Add(key);
        _settings.Save();
        ApplyRoute(_hub.CurrentRoute);
    }

    [RelayCommand]
    private void ShowHiddenHops()
    {
        if (SelectedRow is null) return;
        Settings.HiddenHops ??= [];
        var prefix = SelectedRow.Id.ToString("N") + "|";
        Settings.HiddenHops.RemoveAll(k => k.StartsWith(prefix, StringComparison.Ordinal));
        _settings.Save();
        ApplyRoute(_hub.CurrentRoute);
    }

    [RelayCommand]
    private void CopyRoute()
    {
        Clipboard.SetText(Netpulse.Core.Reports.IspReport.HopsText(RouteHops) + Environment.NewLine + UiLoc.Current["route.icmp"]);
    }

    [RelayCommand]
    private void SaveIspReport()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "TXT|*.txt",
            FileName = $"rttstat-report-{DateTime.Now:yyyyMMdd-HHmm}.txt"
        };
        if (dlg.ShowDialog() != true) return;
        var from = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();
        var hopsDb = _repo.LastRouteTable(from);
        var outs = string.Join("\n", StatsOutageRows.Select(o => $"{o.Start} {o.Duration} {o.Cause}"));
        var body = Netpulse.Core.Reports.IspReport.Build(
            DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            StatsSummary,
            outs,
            string.IsNullOrWhiteSpace(hopsDb) ? Netpulse.Core.Reports.IspReport.HopsText(RouteHops) : hopsDb,
            SpeedText);
        File.WriteAllText(dlg.FileName, body);
    }

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
            "Minute" => now.AddMinutes(-30),
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
        var fromMs = from.ToUniversalTime().ToUnixTimeMilliseconds();
        var toMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var bucket = StatsBucket switch
        {
            "Minute" or "Hour" => "minute",
            "Week" => "day",
            _ => "hour"
        };
        StatsWindowSeconds = Math.Max(60, (toMs - fromMs) / 1000.0);
        var ping = _repo.QueryPingSeries(fromMs, toMs, bucket);
        var nic = _repo.QueryNicSeries(fromMs, toMs, bucket);
        StatsPingSpark = ping.Select(p => p.AvgPing).ToList();
        StatsLossSpark = ping.Select(p => p.Loss).ToList();
        StatsDownSpark = nic.Select(n => n.RecvBps * 8 / 1_000_000.0).ToList();
        StatsUpSpark = nic.Select(n => n.SentBps * 8 / 1_000_000.0).ToList();
        StatsSpark = StatsPingSpark;

        StatsPingRows.Clear();
        StatsLossRows.Clear();
        foreach (var p in ping)
        {
            var time = FmtTs(p.Ts);
            StatsPingRows.Add(new PingStatRow(time, $"{p.AvgPing:0.0}", $"{p.MinPing:0.0}", $"{p.MaxPing:0.0}"));
            StatsLossRows.Add(new LossStatRow(time, $"{p.Loss:0.00}"));
        }
        StatsDownRows.Clear();
        StatsUpRows.Clear();
        foreach (var n in nic)
        {
            var time = FmtTs(n.Ts);
            StatsDownRows.Add(new RateStatRow(time, $"{n.RecvBps * 8 / 1_000_000.0:0.000}"));
            StatsUpRows.Add(new RateStatRow(time, $"{n.SentBps * 8 / 1_000_000.0:0.000}"));
        }
        StatsOutageRows.Clear();
        foreach (var o in _repo.QueryOutages(fromMs, toMs))
        {
            var start = DateTimeOffset.FromUnixTimeMilliseconds(o.StartedTs).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            var end = o.EndedTs is { } e
                ? DateTimeOffset.FromUnixTimeMilliseconds(e).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : "—";
            StatsOutageRows.Add(new OutageStatRow(start, end, TimeSpan.FromMilliseconds(o.DurationMs).ToString(@"hh\:mm\:ss"), o.Cause));
        }
    }

    private string FmtTs(long ts)
    {
        var t = DateTimeOffset.FromUnixTimeMilliseconds(ts).ToLocalTime();
        return StatsBucket switch
        {
            "Minute" or "Hour" => t.ToString("HH:mm"),
            "Week" => t.ToString("dd.MM"),
            _ => t.ToString("dd.MM HH:mm")
        };
    }

    [RelayCommand]
    private void ExportStats()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "CSV|*.csv", FileName = "rttstat-stats.csv" };
        if (dlg.ShowDialog() != true) return;
        var lines = new List<string> { StatsSummary.Replace("\n", "\r\n"), "", "Ping", "Time,Avg,Min,Max" };
        lines.AddRange(StatsPingRows.Select(r => $"{r.Time},{r.Avg},{r.Min},{r.Max}"));
        lines.Add("");
        lines.Add("Loss");
        lines.Add("Time,Loss");
        lines.AddRange(StatsLossRows.Select(r => $"{r.Time},{r.Loss}"));
        lines.Add("");
        lines.Add("Download Mbps");
        lines.Add("Time,Mbps");
        lines.AddRange(StatsDownRows.Select(r => $"{r.Time},{r.Mbps}"));
        lines.Add("");
        lines.Add("Upload Mbps");
        lines.Add("Time,Mbps");
        lines.AddRange(StatsUpRows.Select(r => $"{r.Time},{r.Mbps}"));
        lines.Add("");
        lines.Add("Outages");
        lines.Add("Start,End,Duration,Cause");
        lines.AddRange(StatsOutageRows.Select(r => $"{r.Start},{r.End},{r.Duration},{r.Cause}"));
        File.WriteAllLines(dlg.FileName, lines);
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

    public bool PreferIpv6
    {
        get => Settings.PreferIpv6;
        set
        {
            if (Settings.PreferIpv6 == value) return;
            Settings.PreferIpv6 = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool StartWithWindowsTray
    {
        get => Settings.StartWithWindows;
        set
        {
            if (Settings.StartWithWindows == value) return;
            Settings.StartWithWindows = value;
            AutostartService.Apply(value);
            _settings.Save();
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void QuickAdd() => AddHosts(QuickHost, clear: true);

    [RelayCommand]
    private void AddPreset(string? host) => AddHosts(host, clear: false);

    [RelayCommand]
    private void RemoveTarget(TargetRow? row)
    {
        if (row is null) return;
        var profile = _profiles.Active;
        var t = profile.Targets.FirstOrDefault(x => x.Id == row.Id);
        if (t is null) return;
        profile.Targets.Remove(t);
        _profiles.Upsert(profile);
        RefreshProfiles();
    }

    private void AddHosts(string? raw, bool clear)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        var profile = _profiles.Active;
        var changed = false;
        foreach (var token in raw.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = IpChoice.NormalizeHost(token);
            var existing = profile.Targets.FirstOrDefault(t => string.Equals(t.Host, part, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                if (!existing.Enabled)
                {
                    existing.Enabled = true;
                    changed = true;
                }
                continue;
            }
            if (profile.Targets.Count(t => t.Enabled) >= 32) break;
            profile.Targets.Add(new Target
            {
                DisplayName = FriendlyName(part),
                Host = part,
                Role = part is "1.1.1.1" or "1.0.0.1" or "8.8.8.8" or "8.8.4.4" or "9.9.9.9" or "77.88.8.8" or "2606:4700:4700::1111"
                    ? TargetRole.Dns
                    : TargetRole.Custom,
                Enabled = true
            });
            changed = true;
        }
        if (changed)
        {
            _profiles.Upsert(profile);
            RefreshProfiles();
        }
        if (clear) QuickHost = "";
    }

    private static string FriendlyName(string host) => host.ToLowerInvariant() switch
    {
        "1.1.1.1" or "1.0.0.1" or "2606:4700:4700::1111" => "Cloudflare",
        "8.8.8.8" or "8.8.4.4" => "Google DNS",
        "9.9.9.9" => "Quad9",
        "208.67.222.222" or "208.67.220.220" => "OpenDNS",
        "77.88.8.8" => "Yandex DNS",
        "ya.ru" => "Yandex",
        "google.com" => "Google",
        _ => host
    };

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

    private void RefreshLoss()
    {
        _lossTick = DateTimeOffset.UtcNow;
        var now = DateTimeOffset.Now;
        DateTimeOffset from = LossBucket switch
        {
            "Minute" => now.AddMinutes(-30),
            "Hour" => now.AddHours(-1),
            "Week" => StartOfWeek(now),
            _ => now.Date
        };
        var bucketMs = LossBucket switch
        {
            "Minute" or "Hour" => 60_000L,
            "Week" => 86_400_000L,
            _ => 3_600_000L
        };
        var fromMs = from.ToUniversalTime().ToUnixTimeMilliseconds();
        var toMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        LossWindowSeconds = Math.Max(60, (toMs - fromMs) / 1000.0);

        IReadOnlyList<TargetLossPoint> points = [];
        try
        {
            points = _repo.QueryLossByTarget(fromMs, toMs, bucketMs, _profiles.Active.Id.ToString());
        }
        catch
        {
            points = [];
        }

        var start = fromMs / bucketMs * bucketMs;
        var end = toMs / bucketMs * bucketMs;
        var times = new List<long>();
        if (end >= start && bucketMs > 0)
        {
            var guard = 0;
            for (var t = start; t <= end && guard < 2000; t += bucketMs, guard++)
                times.Add(t);
        }

        var byTarget = points
            .GroupBy(p => p.TargetId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(p => p.Ts).ToDictionary(x => x.Key, x => x.Last().Loss),
                StringComparer.OrdinalIgnoreCase);
        var targets = _profiles.Active.Targets.Where(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Host)).ToList();
        var known = targets.Select(t => t.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in byTarget.Keys)
        {
            if (known.Contains(id)) continue;
            targets.Add(new Target { Id = Guid.TryParse(id, out var g) ? g : Guid.NewGuid(), DisplayName = id, Host = id });
        }

        var built = new List<(string Id, string Name, ChartSeries Series, double? Avg, double? Max, double? Last)>();
        var colorIndex = 0;
        foreach (var target in targets)
        {
            var id = target.Id.ToString();
            byTarget.TryGetValue(id, out var map);
            map ??= new Dictionary<long, double>();
            var values = times.Select(t => map.TryGetValue(t, out var v) ? v : double.NaN).ToList();
            var finite = values.Where(v => !double.IsNaN(v)).ToList();
            var brush = Freeze(Palette[colorIndex++ % Palette.Length]);
            built.Add((id, string.IsNullOrWhiteSpace(target.DisplayName) ? target.Host : target.DisplayName, new ChartSeries
            {
                Name = string.IsNullOrWhiteSpace(target.DisplayName) ? target.Host : target.DisplayName,
                Stroke = brush,
                Values = values
            }, finite.Count == 0 ? null : finite.Average(), finite.Count == 0 ? null : finite.Max(), finite.Count == 0 ? null : finite[^1]));
        }

        if (built.All(b => b.Id != _lossTabId))
            _lossTabId = "all";
        LossCombined = _lossTabId == "all";
        LossSeries = LossCombined ? built.Select(b => b.Series).ToList() : built.Where(b => b.Id == _lossTabId).Select(b => b.Series).ToList();
        LossEmpty = !built.Any(b => b.Series.Values.Any(v => !double.IsNaN(v)));

        LossTabs.Clear();
        LossTabs.Add(new LossTabItem("all", UiLoc.Current["loss.all"], LossCombined));
        foreach (var b in built)
            LossTabs.Add(new LossTabItem(b.Id, b.Name, b.Id == _lossTabId));

        LossSummaryRows.Clear();
        foreach (var b in built)
            LossSummaryRows.Add(new LossSummaryRow(b.Name, FmtLoss(b.Avg), FmtLoss(b.Max), FmtLoss(b.Last)));

        LossDetailRows.Clear();
        var selected = built.FirstOrDefault(b => b.Id == _lossTabId);
        if (selected.Series is not null)
        {
            for (var i = 0; i < times.Count && i < selected.Series.Values.Count; i++)
            {
                var v = selected.Series.Values[i];
                if (double.IsNaN(v)) continue;
                LossDetailRows.Add(new LossStatRow(FmtLossTs(times[i]), $"{v:0.00}"));
            }
        }
    }

    private string FmtLossTs(long ts)
    {
        var t = DateTimeOffset.FromUnixTimeMilliseconds(ts).ToLocalTime();
        return LossBucket switch
        {
            "Minute" or "Hour" => t.ToString("HH:mm"),
            "Week" => t.ToString("dd.MM"),
            _ => t.ToString("dd.MM HH:mm")
        };
    }

    private static string FmtLoss(double? v) => v is null ? "—" : $"{v:0.00}%";

    private static SolidColorBrush Freeze(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }

    private static readonly string[] Palette = ["#F07178", "#7EC8E3", "#F5C542", "#3DDC97", "#C792EA", "#FF9B6A", "#82AAFF", "#F0A0C0"];

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
public sealed record PingStatRow(string Time, string Avg, string Min, string Max);
public sealed record LossStatRow(string Time, string Loss);
public sealed record LossSummaryRow(string Name, string Avg, string Max, string Last);
public sealed record LossTabItem(string Id, string Title, bool Selected);
public sealed record RateStatRow(string Time, string Mbps);
public sealed record OutageStatRow(string Start, string End, string Duration, string Cause);

public sealed partial class TargetRow : ObservableObject
{
    public Guid Id { get; init; }
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _host = "";
    [ObservableProperty] private string _ip = "";
    [ObservableProperty] private string _rtt = "—";
    [ObservableProperty] private string _loss = "—";
    [ObservableProperty] private IReadOnlyList<double> _spark = [];
    public IReadOnlyList<double> PingSpark { get; set; } = [];
    public IReadOnlyList<double> LossSpark { get; set; } = [];
    public Brush Swatch { get; init; } = Brushes.White;
    public double? Min { get; set; }
    public double? Avg { get; set; }
    public double? Max { get; set; }
    public double Jitter { get; set; }
    [ObservableProperty] private string _avgText = "—";
    [ObservableProperty] private string _minText = "—";
    [ObservableProperty] private string _maxText = "—";
    [ObservableProperty] private string _jitterText = "—";
    [ObservableProperty] private string _sentText = "0";
    [ObservableProperty] private string _recvText = "0";
}
