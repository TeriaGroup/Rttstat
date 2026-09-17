using System.Text.Json;
using Netpulse.Core.Json;

namespace Netpulse.Infrastructure.Config;

public static class JsonStore
{
    public static T LoadOrCreate<T>(string path, Func<T> factory) where T : class
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var obj = JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
                if (obj is not null) return obj;
            }
        }
        catch
        {
            try
            {
                File.Copy(path, path + ".bak", true);
            }
            catch
            {
                // ignore backup failure
            }
        }

        var created = factory();
        try { Save(path, created); } catch { /* keep in-memory defaults */ }
        return created;
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, JsonDefaults.Options));
        File.Copy(tmp, path, true);
        File.Delete(tmp);
    }
}
