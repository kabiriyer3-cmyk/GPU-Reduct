using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using GpuReduct.Core;

namespace GpuReduct.Services;

/// <summary>
/// Draws the live tray icon: VRAM % on a rounded badge, colored by usage level
/// (accent &lt; 75%, orange &lt; 90%, red above), like Mem Reduct's RAM % icon.
/// </summary>
internal static class TrayIconRenderer
{
    private static readonly Color Accent = Color.FromArgb(0x5E, 0x6A, 0xD2);
    private static readonly Color Warning = Color.FromArgb(0xF2, 0x99, 0x4A);
    private static readonly Color Danger = Color.FromArgb(0xEB, 0x57, 0x57);

    public static Icon Render(int percent, string level, int size)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            var color = level switch { "Critical" => Danger, "Warn" => Warning, _ => Accent };
            using (var badge = RoundedRect(new RectangleF(0, 0, size, size), size * 0.24f))
            using (var brush = new SolidBrush(color))
                g.FillPath(brush, badge);

            // Text as a path so it can be centered and scaled to fit exactly.
            string text = Math.Clamp(percent, 0, 100).ToString();
            using var glyphs = new GraphicsPath();
            using var family = new FontFamily("Segoe UI");
            glyphs.AddString(text, family, (int)FontStyle.Bold, 100f, PointF.Empty, StringFormat.GenericTypographic);

            var b = glyphs.GetBounds();
            if (b.Width > 0 && b.Height > 0)
            {
                float scale = Math.Min(size * 0.80f / b.Width, size * 0.58f / b.Height);
                using var m = new Matrix();
                m.Translate(size / 2f, size / 2f);
                m.Scale(scale, scale);
                m.Translate(-(b.X + b.Width / 2f), -(b.Y + b.Height / 2f));
                glyphs.Transform(m);
                g.FillPath(Brushes.White, glyphs);
            }
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone(); // owns its own copy of the handle
        }
        finally
        {
            Native.DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
