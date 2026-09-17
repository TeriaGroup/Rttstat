using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var outPath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Netpulse.App", "Assets", "rttstat.ico"));

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
var sizes = new[] { 256, 64, 48, 32, 16 };
var pngs = sizes.Select(Draw).ToList();
WriteIco(outPath, sizes, pngs);
var pngPath = Path.ChangeExtension(outPath, ".png");
File.WriteAllBytes(pngPath, pngs[0]);
Console.WriteLine(outPath);
Console.WriteLine(pngPath);

static byte[] Draw(int size)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);

    var pad = Math.Max(1, size / 16f);
    var rect = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);
    var radius = size * 0.22f;
    using (var path = RoundRect(rect, radius))
    using (var bg = new SolidBrush(Color.FromArgb(255, 18, 22, 28)))
        g.FillPath(bg, path);

    var accent = Color.FromArgb(255, 0, 200, 83);
    var dim = Color.FromArgb(90, 0, 200, 83);
    using var pen = new Pen(accent, Math.Max(1.5f, size / 18f))
    {
        LineJoin = LineJoin.Round,
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
    };
    using var dimPen = new Pen(dim, Math.Max(1f, size / 28f));

    float X(float t) => pad + (size - pad * 2) * t;
    float Y(float t) => pad + (size - pad * 2) * t;

    g.DrawLine(dimPen, X(0.12f), Y(0.55f), X(0.88f), Y(0.55f));

    using var pulse = new GraphicsPath();
    pulse.AddLines(new[]
    {
        new PointF(X(0.12f), Y(0.55f)),
        new PointF(X(0.32f), Y(0.55f)),
        new PointF(X(0.40f), Y(0.55f)),
        new PointF(X(0.46f), Y(0.22f)),
        new PointF(X(0.54f), Y(0.78f)),
        new PointF(X(0.60f), Y(0.55f)),
        new PointF(X(0.70f), Y(0.55f)),
        new PointF(X(0.88f), Y(0.55f)),
    });
    g.DrawPath(pen, pulse);

    if (size >= 32)
    {
        var r = Math.Max(2f, size / 22f);
        using var dot = new SolidBrush(accent);
        g.FillEllipse(dot, X(0.12f) - r, Y(0.55f) - r, r * 2, r * 2);
        g.FillEllipse(dot, X(0.88f) - r, Y(0.55f) - r, r * 2, r * 2);
    }

    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

static GraphicsPath RoundRect(RectangleF r, float radius)
{
    var d = radius * 2;
    var p = new GraphicsPath();
    p.AddArc(r.X, r.Y, d, d, 180, 90);
    p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    p.CloseFigure();
    return p;
}

static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
{
    using var fs = File.Create(path);
    using var w = new BinaryWriter(fs);
    w.Write((ushort)0);
    w.Write((ushort)1);
    w.Write((ushort)sizes.Length);
    var offset = 6 + 16 * sizes.Length;
    for (var i = 0; i < sizes.Length; i++)
    {
        var s = sizes[i];
        w.Write((byte)(s >= 256 ? 0 : s));
        w.Write((byte)(s >= 256 ? 0 : s));
        w.Write((byte)0);
        w.Write((byte)0);
        w.Write((ushort)1);
        w.Write((ushort)32);
        w.Write(pngs[i].Length);
        w.Write(offset);
        offset += pngs[i].Length;
    }
    foreach (var png in pngs)
        w.Write(png);
}
