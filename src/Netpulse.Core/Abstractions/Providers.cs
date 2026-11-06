using Netpulse.Core.Models;

namespace Netpulse.Core.Abstractions;

public interface ISettingsProvider
{
    AppSettings Current { get; }
    void Reload();
    void Save();
    event Action? Changed;
}

public interface INotificationSink
{
    void Show(string title, string body, EventCategory category);
}

public interface IAppPaths
{
    string Root { get; }
    string Data { get; }
    string Logs { get; }
    string Config { get; }
    string SettingsFile { get; }
    string ProfilesFile { get; }
    string DatabaseFile { get; }
    bool Portable { get; }
}
