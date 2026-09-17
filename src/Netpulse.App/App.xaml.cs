using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Netpulse.App.Loc;
using Netpulse.App.Services;
using Netpulse.App.Tray;
using Netpulse.App.ViewModels;
using Netpulse.Core.Abstractions;
using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;
using Netpulse.Core.Speedtest;
using Netpulse.Infrastructure.Config;
using Netpulse.Infrastructure.Hosted;
using Netpulse.Infrastructure.Logging;
using Netpulse.Infrastructure.Paths;
using Netpulse.Infrastructure.Sqlite;
using Netpulse.Infrastructure.Windows;

namespace Netpulse.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private SingleInstance? _instance;
    private TrayController? _tray;
    private MainWindow? _main;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rttstat", "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "crash.log"), DateTimeOffset.Now + " " + args.Exception + Environment.NewLine);
            }
            catch { /* ignore */ }
            args.Handled = true;
        };

        _instance = new SingleInstance();
        if (!_instance.IsPrimary)
        {
            SingleInstance.SignalShow();
            Shutdown();
            return;
        }

        try
        {
        var paths = new AppPaths();
        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(b =>
            {
                b.ClearProviders();
                b.AddProvider(new FileLoggerProvider(paths.Logs));
                b.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(s =>
            {
                s.AddSingleton<IAppPaths>(paths);
                s.AddSingleton<ISettingsProvider, SettingsService>();
                s.AddSingleton<IProfileProvider, ProfileService>();
                s.AddSingleton<NetpulseDb>();
                s.AddSingleton<SampleRepository>();
                s.AddSingleton<MonitorHub>();
                s.AddSingleton<NotificationSink>();
                s.AddSingleton<INotificationSink>(sp => sp.GetRequiredService<NotificationSink>());
                s.AddHttpClient();
                s.AddSingleton(sp =>
                {
                    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                    return new SpeedtestService(
                        http,
                        sp.GetRequiredService<MonitorHub>(),
                        sp.GetRequiredService<ISettingsProvider>(),
                        sp.GetRequiredService<IProfileProvider>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SpeedtestService>>());
                });
                s.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SpeedtestService>());
                s.AddHostedService<MonitorLoopService>();
                s.AddHostedService<PersistenceService>();
                s.AddHostedService<MaintenanceService>();
                s.AddTransient<MainViewModel>();
                s.AddTransient<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var hub = _host.Services.GetRequiredService<MonitorHub>();
        var settings = _host.Services.GetRequiredService<ISettingsProvider>();
        var profiles = _host.Services.GetRequiredService<IProfileProvider>();
        var speed = _host.Services.GetRequiredService<SpeedtestService>();
        var notify = _host.Services.GetRequiredService<NotificationSink>();

        I18n.Current.SetLanguage(settings.Current.Language);
        AutostartService.Apply(settings.Current.StartWithWindows);

        _tray = new TrayController(hub, profiles, settings, speed, ShowMain, ExitApp);
        notify.Icon = _tray.Icon;

        _instance.ShowRequested += () => Dispatcher.BeginInvoke(ShowMain);

        settings.Current.FirstRun = false;
        settings.Save();
        ShowMain();
        }
        catch (Exception ex)
        {
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rttstat", "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "crash.log"), DateTimeOffset.Now + " " + ex + Environment.NewLine);
            }
            catch { /* ignore */ }
            MessageBox.Show(ex.Message, "Rttstat failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void ShowMain()
    {
        _main ??= _host!.Services.GetRequiredService<MainWindow>();
        if (_main.WindowState == WindowState.Minimized)
            _main.WindowState = WindowState.Normal;
        _main.Show();
        _main.ShowInTaskbar = true;
        _main.Activate();
        _main.Topmost = true;
        _main.Topmost = false;
        _main.Focus();
    }

    private async void ExitApp()
    {
        _tray?.Dispose();
        if (_host is not null)
            await _host.StopAsync(TimeSpan.FromSeconds(2));
        _host?.Dispose();
        _instance?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _host?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
