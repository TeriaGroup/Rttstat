using Netpulse.Core.Abstractions;

namespace Netpulse.Infrastructure.Paths;

public sealed class AppPaths : IAppPaths
{
    public string Root { get; }
    public string Data { get; }
    public string Logs { get; }
    public string Config { get; }
    public string SettingsFile { get; }
    public string ProfilesFile { get; }
    public string DatabaseFile { get; }
    public bool Portable { get; }

    public AppPaths()
    {
        var exeDir = AppContext.BaseDirectory;
        Portable = File.Exists(Path.Combine(exeDir, "portable.flag"));
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Root = Portable
            ? Path.Combine(exeDir, "data")
            : Path.Combine(appData, "Rttstat");
        if (!Portable)
            MigrateFolder(Path.Combine(appData, "Netpulse"), Root);
        Data = Path.Combine(Root, Portable ? "db" : "data");
        Logs = Path.Combine(Root, "logs");
        Config = Path.Combine(Root, "config");
        SettingsFile = Path.Combine(Config, "settings.json");
        ProfilesFile = Path.Combine(Config, "profiles.json");
        DatabaseFile = Path.Combine(Data, "rttstat.db");
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Config);
        var oldDb = Path.Combine(Data, "netpulse.db");
        if (File.Exists(oldDb) && !File.Exists(DatabaseFile))
            File.Move(oldDb, DatabaseFile);
    }

    private static void MigrateFolder(string from, string to)
    {
        if (Directory.Exists(to) || !Directory.Exists(from)) return;
        try { Directory.Move(from, to); }
        catch { /* keep old folder if locked */ }
    }
}
