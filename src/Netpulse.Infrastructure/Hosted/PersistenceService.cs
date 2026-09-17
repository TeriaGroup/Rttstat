using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;
using Netpulse.Infrastructure.Sqlite;

namespace Netpulse.Infrastructure.Hosted;

public sealed class PersistenceService : BackgroundService
{
    private readonly SampleRepository _repo;
    private readonly MonitorHub _hub;
    private readonly ConcurrentQueue<PingSample> _pings = new();
    private readonly ConcurrentQueue<NicSample> _nics = new();

    public PersistenceService(SampleRepository repo, MonitorHub hub)
    {
        _repo = repo;
        _hub = hub;
        _hub.PingSampleProduced += s => _pings.Enqueue(s);
        _hub.NicSampleProduced += s => _nics.Enqueue(s);
        _hub.OutageChanged += o => _repo.UpsertOutage(o);
        _hub.EventRaised += e => _repo.InsertEvent(e);
        _hub.SpeedtestCompleted += r => _repo.InsertSpeedtest(r);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            Flush();
        Flush();
    }

    private void Flush()
    {
        var pings = Drain(_pings);
        var nics = Drain(_nics);
        if (pings.Count > 0) _repo.InsertPings(pings);
        if (nics.Count > 0) _repo.InsertNics(nics);
    }

    private static List<T> Drain<T>(ConcurrentQueue<T> q)
    {
        var list = new List<T>();
        while (q.TryDequeue(out var item) && list.Count < 500)
            list.Add(item);
        return list;
    }
}
