using System;
using System.Windows.Media;

namespace Smop.MainApp.Utils.Extensions;

internal static class ColorExt
{
    public static Color Lighter(this Color color, double percentage)
    {
        var ratio = Math.Max(0, Math.Min(1, percentage <= 1 ? percentage : percentage / 100));
        byte r = (byte)Math.Min(255, color.R + (255 - color.R) * ratio);
        byte g = (byte)Math.Min(255, color.G + (255 - color.G) * ratio);
        byte b = (byte)Math.Min(255, color.B + (255 - color.B) * ratio);
        return Color.FromArgb(color.A, r, g, b);
    }

    public static Color Darker(this Color color, double percentage)
    {
        var ratio = Math.Max(0, Math.Min(1, percentage <= 1 ? percentage : percentage / 100));
        byte r = (byte)(color.R * (1.0 - ratio));
        byte g = (byte)(color.G * (1.0 - ratio));
        byte b = (byte)(color.B * (1.0 - ratio));
        return Color.FromArgb(color.A, r, g, b);
    }
}
