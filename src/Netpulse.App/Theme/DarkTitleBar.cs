using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Netpulse.App.Theme;

internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void Apply(Window window)
    {
        void Set()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            var on = 1;
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, 4) != 0)
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref on, 4);
        }

        if (window.IsLoaded) Set();
        else window.SourceInitialized += (_, _) => Set();
    }
}
