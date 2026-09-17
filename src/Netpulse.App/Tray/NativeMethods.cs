using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Netpulse.App.Tray;

internal static class NativeMethods
{
    private const int GwlExstyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RectInt lpRect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public static void ApplyNoActivate(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var ex = GetWindowLong(hwnd, GwlExstyle);
        SetWindowLong(hwnd, GwlExstyle, ex | WsExNoActivate | WsExToolwindow | WsExTopmost);
    }

    public static bool IsForegroundFullscreen()
    {
        try
        {
            var fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            GetWindowRect(fg, out var r);
            var w = GetSystemMetrics(0);
            var h = GetSystemMetrics(1);
            return r.Left <= 0 && r.Top <= 0 && r.Right >= w && r.Bottom >= h;
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectInt
    {
        public int Left, Top, Right, Bottom;
    }
}
