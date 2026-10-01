using Microsoft.Win32;

namespace Netpulse.Infrastructure.Windows;

public static class AutostartService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Rttstat";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
        {
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Rttstat.exe");
            key.DeleteValue("Netpulse", false);
            key.SetValue(ValueName, "\"" + exe + "\" --tray");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}
