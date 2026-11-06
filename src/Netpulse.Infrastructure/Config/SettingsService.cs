using System.Text.Json;
using Netpulse.Core.Abstractions;
using Netpulse.Core.Json;
using Netpulse.Core.Models;
using Netpulse.Core.Profiles;

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
        EnsureMonitor();
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
        EnsureMonitor();
        Changed?.Invoke();
    }

    public void Save()
    {
        lock (_gate)
            JsonStore.Save(_paths.SettingsFile, _current);
        Changed?.Invoke();
    }

    private void EnsureMonitor()
    {
        _current.Monitor ??= new Profile();
        _current.Monitor.Targets ??= [];
        if (_current.SchemaVersion >= 2)
            return;

        var imported = TryReadProfiles();
        _current.Monitor = imported ?? ProfileMigration.Seed();
        _current.Monitor.Targets ??= [];
        _current.SchemaVersion = 2;
        lock (_gate)
            JsonStore.Save(_paths.SettingsFile, _current);
        if (imported is not null)
            ArchiveProfiles();
    }

    private Profile? TryReadProfiles()
    {
        var path = _paths.ProfilesFile;
        if (!File.Exists(path))
            return null;
        try
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<ProfileFile>(json, JsonDefaults.Options);
            return ProfileMigration.Import(file);
        }
        catch
        {
            return null;
        }
    }

    private void ArchiveProfiles()
    {
        var path = _paths.ProfilesFile;
        if (!File.Exists(path))
            return;
        try
        {
            var bak = path + ".migrated";
            if (File.Exists(bak))
                File.Delete(bak);
            File.Move(path, bak);
        }
        catch
        {
            // schema flag stops a second import
        }
    }
}
