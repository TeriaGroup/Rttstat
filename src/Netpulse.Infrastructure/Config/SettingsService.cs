using Netpulse.Core.Abstractions;
using Netpulse.Core.Models;

namespace Netpulse.Infrastructure.Config;

public sealed class SettingsService : ISettingsProvider
{
    private readonly IAppPaths _paths;
    private readonly object _gate = new();
    private AppSettings _current;

    public SettingsService(IAppPaths paths)
    {
        _paths = paths;
        _current = JsonStore.LoadOrCreate(paths.SettingsFile, () => new AppSettings());
    }

    public AppSettings Current
    {
        get { lock (_gate) return _current; }
    }

    public event Action? Changed;

    public void Reload()
    {
        lock (_gate)
            _current = JsonStore.LoadOrCreate(_paths.SettingsFile, () => new AppSettings());
        Changed?.Invoke();
    }

    public void Save()
    {
        lock (_gate)
            JsonStore.Save(_paths.SettingsFile, _current);
        Changed?.Invoke();
    }
}
