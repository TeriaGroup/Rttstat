using Microsoft.Extensions.Hosting;
using Netpulse.Core.Abstractions;
using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;
using Netpulse.Infrastructure.Sqlite;

namespace Netpulse.Infrastructure.Hosted;

public sealed class MaintenanceService : BackgroundService
{
    private readonly SampleRepository _repo;
    private readonly ISettingsProvider _settings;
    private readonly MonitorHub _hub;

    public MaintenanceService(SampleRepository repo, ISettingsProvider settings, MonitorHub hub)
    {
        _repo = repo;
        _settings = settings;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        RunOnce();
        while (await timer.WaitForNextTickAsync(stoppingToken))
            RunOnce();
    }

    private void RunOnce()
    {
        try
        {
            _repo.RollupHoursAndDays();
            _repo.ApplyRetention(_settings.Current.Retention);
            _repo.Vacuum();
            _hub.EmitEvent(EventLevel.Debug, EventCategory.App, "Retention and rollup completed");
        }
        catch (Exception ex)
        {
            _hub.EmitEvent(EventLevel.Error, EventCategory.App, "Maintenance failed: " + ex.Message);
        }
    }
}
