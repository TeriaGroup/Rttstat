using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Netpulse.Core.Models;

namespace Netpulse.App.Tray;

public static class IconFactory
{
    private static readonly Color[] Series =
    [
        Color.FromArgb(0x3D, 0xDC, 0x6A),
        Color.FromArgb(0x4C, 0x9A, 0xFF),
        Color.FromArgb(0xFF, 0x4D, 0x4D),
        Color.FromArgb(0xF5, 0xC5, 0x42),
        Color.FromArgb(0xC7, 0x92, 0xEA),
        Color.FromArgb(0xFF, 0x9B, 0x6A)
    ];

    public static Color SeriesColor(int index) => Series[Math.Abs(index) % Series.Length];

    public static Color SeverityColor(LinkQuality tone) => tone switch
    {
        LinkQuality.Ok => Color.FromArgb(0x3D, 0xDC, 0x6A),
        LinkQuality.Warn => Color.FromArgb(0x3B, 0x9E, 0xFF),
        LinkQuality.Bad or LinkQuality.Down => Color.FromArgb(0xFF, 0x4D, 0x55),
        _ => Color.FromArgb(0xD0, 0xD4, 0xDA)
    };

    public static Icon Badge(string text, Color digit, bool colorize = true)
    {
        var dpi = TrayDpi();
        var size = dpi <= 96 ? 16 : 32;
        var fontPx = size == 16 ? (text.Length >= 3 ? 11f : 13f) : (text.Length >= 3 ? 20f : 25f);
        var ink = colorize ? digit : Color.White;
        var plate = colorize ? Plate(digit) : Color.FromArgb(0x3A, 0x3E, 0x46);
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(plate);
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            using var font = new Font("Tahoma", fontPx, text.Length >= 3 ? FontStyle.Regular : FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(ink);
            var origin = CenterInk(g, text, font, plate, ink, size);
            g.DrawString(text, font, brush, origin.X, origin.Y);
        }

        return ToAlphaIcon(bmp);
    }

    private static Font FitFont(Graphics g, string text, int size, Color square, Color ink)
    {
        var margin = text.Length <= 1 ? size * 0.22f : size * 0.14f;
        var limit = Math.Max(7f, size - margin * 2f);
        var px = size * 0.95f;
        for (var i = 0; i < 6; i++)
        {
            using var probe = new Font("Tahoma", px, FontStyle.Bold, GraphicsUnit.Pixel);
            var box = InkBox(probe, text, square, ink);
            if (box.Width <= 0 || box.Height <= 0)
                break;
            var scale = Math.Min(limit / box.Width, limit / box.Height);
            if (scale is > 0.92f and < 1.08f)
                return new Font("Tahoma", px, FontStyle.Bold, GraphicsUnit.Pixel);
            px = Math.Max(6f, px * scale);
        }
        return new Font("Tahoma", px, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    private static PointF CenterInk(Graphics g, string text, Font font, Color square, Color ink, int size)
    {
        var box = InkBox(font, text, square, ink);
        return new PointF((size - box.Width) / 2f - box.X, (size - box.Height) / 2f - box.Y);
    }

    private static RectangleF InkBox(Font font, string text, Color square, Color ink)
    {
        const int pad = 4;
        var w = 72;
        var h = 48;
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(square);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var brush = new SolidBrush(ink);
        g.DrawString(text, font, brush, pad, pad);
        var minX = w;
        var minY = h;
        var maxX = 0;
        var maxY = 0;
        var any = false;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (Math.Abs(c.R - square.R) + Math.Abs(c.G - square.G) + Math.Abs(c.B - square.B) < 28)
                    continue;
                any = true;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }
        if (!any)
            return new RectangleF(pad, pad, 8, 8);
        return new RectangleF(minX - pad, minY - pad, maxX - minX + 1, maxY - minY + 1);
    }

    private static Color Plate(Color c) => Color.FromArgb(
        Clamp((int)(c.R * 0.38 + 6)),
        Clamp((int)(c.G * 0.38 + 8)),
        Clamp((int)(c.B * 0.38 + 6)));

    private static Color TextOn(Color square)
    {
        var luma = 0.3 * square.R + 0.59 * square.G + 0.11 * square.B;
        return luma > 160 ? Color.Black : Color.White;
    }

    private static int TrayDpi()
    {
        try
        {
            var tray = FindWindow("Shell_TrayWnd", null);
            var dpi = tray == IntPtr.Zero ? 0 : GetDpiForWindow(tray);
            if (dpi >= 96)
                return (int)dpi;
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            return (int)Math.Round(g.DpiX);
        }
        catch
        {
            return 96;
        }
    }

    private static Color DarkTile(Color c) => Color.FromArgb(
        Clamp((int)(c.R * 0.20 + 8)),
        Clamp((int)(c.G * 0.24 + 10)),
        Clamp((int)(c.B * 0.20 + 8)));

    private static int Clamp(int v) => Math.Clamp(v, 0, 255);

    public static string PingText(LinkQuality link, double? pingMs)
    {
        if (link == LinkQuality.Down) return "!";
        if (link == LinkQuality.Paused) return "-";
        if (pingMs is null) return "-";
        var n = (int)Math.Round(pingMs.Value);
        if (n < 0) return "?";
        if (n >= 1000) return "1s";
        return n.ToString();
    }

    public static string LossText(LinkQuality link, double loss)
    {
        if (link == LinkQuality.Paused) return "-";
        if (link == LinkQuality.Down) return "!";
        if (double.IsNaN(loss) || loss < 0) return "-";
        if (loss >= 99.5) return "!";
        return ((int)Math.Round(loss)).ToString();
    }

    public static LinkQuality PingTone(LinkQuality link, double? pingMs, int warnMs, int badMs)
    {
        if (link is LinkQuality.Down or LinkQuality.Paused) return link;
        if (pingMs is null) return LinkQuality.Paused;
        if (pingMs >= badMs) return LinkQuality.Bad;
        if (pingMs >= warnMs) return LinkQuality.Warn;
        return LinkQuality.Ok;
    }

    public static LinkQuality LossTone(LinkQuality link, double loss, double warn, double bad)
    {
        if (link is LinkQuality.Down or LinkQuality.Paused) return link;
        if (double.IsNaN(loss)) return LinkQuality.Paused;
        if (loss >= bad) return LinkQuality.Bad;
        if (loss >= warn) return LinkQuality.Warn;
        return LinkQuality.Ok;
    }

    private static void DrawFitted(Graphics g, string text, int size, Color fg)
    {
        var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces
        };
        format.SetMeasurableCharacterRanges([new CharacterRange(0, text.Length)]);
        using var probe = new Font("Segoe UI", 100, FontStyle.Bold, GraphicsUnit.Pixel);
        var probeBox = g.MeasureCharacterRanges(text, probe, new RectangleF(0, 0, 800, 800), format)[0].GetBounds(g);
        var targetW = size * (text.Length >= 3 ? 0.92f : 0.84f);
        var targetH = size * 0.74f;
        var fontPx = 100f * Math.Min(targetW / Math.Max(1, probeBox.Width), targetH / Math.Max(1, probeBox.Height));
        using var font = new Font("Segoe UI", Math.Max(8, fontPx), FontStyle.Bold, GraphicsUnit.Pixel);
        var bounds = g.MeasureCharacterRanges(text, font, new RectangleF(0, 0, size * 4, size * 4), format)[0].GetBounds(g);
        var x = (size - bounds.Width) / 2f - bounds.X;
        var y = (size - bounds.Height) / 2f - bounds.Y;
        using var brush = new SolidBrush(fg);
        g.DrawString(text, font, brush, x, y, format);
    }

    private static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static int PixelSize()
    {
        try
        {
            var tray = FindWindow("Shell_TrayWnd", null);
            var dpi = tray == IntPtr.Zero ? GetDpiForSystem() : GetDpiForWindow(tray);
            if (dpi < 96) dpi = 96;
            var w = GetSystemMetricsForDpi(49, dpi);
            if (w < 16) w = 16;
            if (w > 64) w = 64;
            return w;
        }
        catch
        {
            return 32;
        }
    }

    private static Icon ToAlphaIcon(Bitmap bmp)
    {
        var width = bmp.Width;
        var height = bmp.Height;
        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var raw = new byte[Math.Abs(stride) * height];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);
            var dib = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var si = y * stride + x * 4;
                    var di = ((height - 1 - y) * width + x) * 4;
                    var b = raw[si];
                    var g = raw[si + 1];
                    var r = raw[si + 2];
                    var a = raw[si + 3];
                    dib[di] = (byte)(b * a / 255);
                    dib[di + 1] = (byte)(g * a / 255);
                    dib[di + 2] = (byte)(r * a / 255);
                    dib[di + 3] = a;
                }
            }

            var info = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = height,
                    biPlanes = 1,
                    biBitCount = 32
                }
            };
            var hColor = CreateDIBSection(IntPtr.Zero, ref info, 0, out var bits, IntPtr.Zero, 0);
            if (hColor == IntPtr.Zero)
                return Fallback(bmp);
            Marshal.Copy(dib, 0, bits, dib.Length);
            var hMask = CreateBitmap(width, height, 1, 1, IntPtr.Zero);
            var iconInfo = new ICONINFO { fIcon = true, hbmColor = hColor, hbmMask = hMask };
            var hIcon = CreateIconIndirect(ref iconInfo);
            DeleteObject(hColor);
            DeleteObject(hMask);
            if (hIcon == IntPtr.Zero)
                return Fallback(bmp);
            using var tmp = Icon.FromHandle(hIcon);
            var clone = (Icon)tmp.Clone();
            DestroyIcon(hIcon);
            return clone;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static Icon Fallback(Bitmap bmp)
    {
        var h = bmp.GetHicon();
        using var tmp = Icon.FromHandle(h);
        var clone = (Icon)tmp.Clone();
        DestroyIcon(h);
        return clone;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitCount, IntPtr bits);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref ICONINFO icon);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
