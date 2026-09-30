using System.Drawing;

namespace Alerta.Core;

/// <summary>Tray icon color: green for the first half, then to amber at 85 %, then to red at 100 %.</summary>
public static class ColorRamp
{
    public static readonly Color Green = Color.FromArgb(0x2E, 0x9E, 0x4F);
    public static readonly Color Amber = Color.FromArgb(0xF2, 0xA9, 0x00);
    public static readonly Color Red = Color.FromArgb(0xD9, 0x30, 0x25);

    public static Color ForProgress(double progress)
    {
        var p = double.IsNaN(progress) ? 0 : Math.Clamp(progress, 0, 1);
        if (p <= 0.5) return Green;
        if (p <= 0.85) return Lerp(Green, Amber, (p - 0.5) / 0.35);
        return Lerp(Amber, Red, (p - 0.85) / 0.15);
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * t),
            (int)Math.Round(a.G + (b.G - a.G) * t),
            (int)Math.Round(a.B + (b.B - a.B) * t));
    }
}
