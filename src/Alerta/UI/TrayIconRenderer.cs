using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Alerta.UI;

/// <summary>Draws the "breathing" ring: grey track, colored progress arc, colored center dot.</summary>
internal static class TrayIconRenderer
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Render(double progress, Color color, int size)
    {
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var stroke = size * 0.16f;
            var inset = size * 0.09f + stroke / 2;
            var ring = new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset);

            using var track = new Pen(Color.FromArgb(90, 128, 128, 128), stroke);
            g.DrawEllipse(track, ring);

            var sweep = (float)(360 * Math.Clamp(double.IsNaN(progress) ? 0 : progress, 0, 1));
            if (sweep > 0.5f)
            {
                using var arc = new Pen(color, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(arc, ring, -90, sweep);
            }

            var dot = size * 0.28f;
            using var fill = new SolidBrush(color);
            g.FillEllipse(fill, (size - dot) / 2, (size - dot) / 2, dot, dot);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
