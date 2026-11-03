using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace PodBridge.App.Tray;

/// <summary>Draws the tray icon: a coloured rounded square showing the battery percentage.</summary>
internal static class TrayIconRenderer
{
    private const int Size = 32;

    private static readonly Color Green = Color.FromArgb(46, 160, 67);
    private static readonly Color Amber = Color.FromArgb(191, 120, 0);
    private static readonly Color Red = Color.FromArgb(205, 49, 49);
    private static readonly Color Grey = Color.FromArgb(105, 105, 115);

    /// <summary>Creates an HICON. The caller owns it and must release it with DestroyIcon.</summary>
    public static IntPtr CreateIcon(int? percent, bool charging)
    {
        using var bitmap = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var background = percent switch
            {
                null => Grey,
                <= 20 => Red,
                <= 40 => Amber,
                _ => Green,
            };

            using (var path = RoundedRect(new RectangleF(0, 0, Size, Size), 8))
            using (var brush = new SolidBrush(background))
            {
                g.FillPath(brush, path);
            }

            string text = percent is int p ? p.ToString() : "?";
            float fontSize = text.Length >= 3 ? 14f : 20f;
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(text, font, Brushes.White, new RectangleF(0, 1, Size, Size), format);

            if (charging)
            {
                // Small white dot in the corner marks "charging".
                g.FillEllipse(Brushes.White, Size - 10, 2, 7, 7);
            }
        }

        return bitmap.GetHicon();
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
