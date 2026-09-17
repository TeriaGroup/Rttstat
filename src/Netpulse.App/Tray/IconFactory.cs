using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows;
using Netpulse.Core.Models;

namespace Netpulse.App.Tray;

public static class IconFactory
{
    public static Icon Create(LinkQuality quality, double? pingMs)
    {
        var size = IconPixelSize();
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var (bg, fg) = Colors(quality);
            using (var fill = new SolidBrush(bg))
                g.FillRectangle(fill, 0, 0, size, size);

            var text = Text(quality, pingMs);
            var fontPx = text.Length >= 3 ? size * 0.52f : size * 0.72f;
            using var font = new Font("Consolas", fontPx, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
            };
            using var tb = new SolidBrush(fg);
            g.DrawString(text, font, tb, new RectangleF(-1, -1, size + 2, size + 2), sf);
        }

        var h = bmp.GetHicon();
        using var tmp = Icon.FromHandle(h);
        var clone = (Icon)tmp.Clone();
        DestroyIcon(h);
        return clone;
    }

    private static int IconPixelSize()
    {
        try
        {
            var w = (int)Math.Round(SystemParameters.SmallIconWidth);
            return w >= 24 ? 32 : 16;
        }
        catch
        {
            return 16;
        }
    }

    private static (Color Bg, Color Fg) Colors(LinkQuality q) => q switch
    {
        LinkQuality.Ok => (Color.FromArgb(0x00, 0xC8, 0x53), Color.Black),
        LinkQuality.Warn => (Color.FromArgb(0xFF, 0xD6, 0x00), Color.Black),
        LinkQuality.Bad or LinkQuality.Down => (Color.FromArgb(0xFF, 0x3D, 0x3D), Color.White),
        _ => (Color.FromArgb(0x90, 0xA4, 0xAE), Color.Black)
    };

    private static string Text(LinkQuality q, double? pingMs)
    {
        if (q == LinkQuality.Down) return "!";
        if (q == LinkQuality.Paused) return "--";
        if (pingMs is null) return "?";
        var n = (int)Math.Round(pingMs.Value);
        if (n < 0) return "?";
        if (n >= 1000) return "1s";
        return n.ToString();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
