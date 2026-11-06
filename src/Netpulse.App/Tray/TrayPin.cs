using Microsoft.Win32;

namespace Netpulse.App.Tray;

internal static class TrayPin
{
    public static void Promote()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", writable: true);
            if (key is null) return;
            foreach (var name in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(name, writable: true);
                if (sub is null) continue;
                var exe = sub.GetValue("ExecutablePath") as string;
                if (exe is null || !exe.EndsWith("Rttstat.exe", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (sub.GetValue("IsPromoted") is int promoted && promoted == 1)
                    continue;
                sub.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
            }
        }
        catch
        {
            // tray visibility is best-effort
        }
    }
}
