using Netpulse.Core.Abstractions;
using Netpulse.Core.Models;
using Netpulse.Core.Profiles;

namespace Netpulse.Infrastructure.Config;

public sealed class ProfileService : IProfileProvider
{
    private readonly IAppPaths _paths;
    private readonly object _gate = new();
    private ProfileFile _file;

    public ProfileService(IAppPaths paths)
    {
        _paths = paths;
        _file = JsonStore.LoadOrCreate(paths.ProfilesFile, DefaultProfiles.Create);
        EnsureActive();
    }

    public IReadOnlyList<Profile> All
    {
        get { lock (_gate) return _file.Profiles.ToList(); }
    }

    public Profile Active
    {
        get
        {
            lock (_gate)
            {
                EnsureActive();
                return _file.Profiles.First(p => p.IsActive);
            }
        }
    }

    public event Action? Changed;

    public void SetActive(Guid id)
    {
        lock (_gate)
        {
            foreach (var p in _file.Profiles)
                p.IsActive = p.Id == id;
            EnsureActive();
        }
        Save();
    }

    public void Upsert(Profile profile)
    {
        lock (_gate)
        {
            var i = _file.Profiles.FindIndex(p => p.Id == profile.Id);
            if (i >= 0) _file.Profiles[i] = profile;
            else _file.Profiles.Add(profile);
        }
        Save();
    }

    public void Delete(Guid id)
    {
        lock (_gate)
        {
            if (_file.Profiles.Count <= 1) return;
            _file.Profiles.RemoveAll(p => p.Id == id);
            EnsureActive();
        }
        Save();
    }

    public void Save()
    {
        lock (_gate)
            JsonStore.Save(_paths.ProfilesFile, _file);
        Changed?.Invoke();
    }

    private void EnsureActive()
    {
        if (_file.Profiles.Count == 0)
            _file.Profiles.AddRange(DefaultProfiles.Create().Profiles);
        if (!_file.Profiles.Any(p => p.IsActive))
            _file.Profiles[0].IsActive = true;
        var first = true;
        foreach (var p in _file.Profiles)
        {
            if (!p.IsActive) continue;
            if (!first) p.IsActive = false;
            first = false;
        }
    }
}
