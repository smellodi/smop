using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace Smop.Common;

internal static class PlotColorTheme
{
    /// <summary>
    /// Creates a plot color from a normalized value
    /// </summary>
    /// <param name="value">0..1</param>
    /// <returns>Color</returns>
    public static Color ValueToColor(double value)
    {
        var i = _levels.TakeWhile(level => value > level).Count();
        return Color.FromRgb(_r[i](value), _g[i](value), _b[i](value));
    }

    public static void CreateTheme(KeyValuePair<double, Color>[]? theme)
    {
        if (theme == null)
            return;

        var levels = theme.Select(kv => kv.Key).ToArray();
        if (levels.Length < 2 || levels[0] != 0 || levels[^1] != 1 ||
            levels.Aggregate(-1.0, (accum, v) => v > accum ? v : double.MaxValue) > 1)    // the final will ne >1 if the order is not ascending
            return;

        _levels = levels.Skip(1).SkipLast(1).ToArray();
        _r = MakeColorScale(theme.Select(kv => kv.Value.R).ToArray());
        _g = MakeColorScale(theme.Select(kv => kv.Value.G).ToArray());
        _b = MakeColorScale(theme.Select(kv => kv.Value.B).ToArray());
    }


    // Internal

    static double[] _levels = new double[] { 0.05, 0.2, 0.4, 0.7 };

    // RGB functions, +1 to the number of levels.   Colors: grey cyan green brown red  white
    static Func<double, byte>[] _r = MakeColorScale(240, 0, 0, 128, 128, 216);
    static Func<double, byte>[] _g = MakeColorScale(240, 208, 176, 190, 0, 216);
    static Func<double, byte>[] _b = MakeColorScale(240, 208, 0, 0, 0, 216);

    // Helpers
    private static (double, double) GetMinMax(int levelIndex)
    {
        var min = levelIndex <= 0 ? 0 : _levels[levelIndex - 1];
        var max = levelIndex >= _levels.Length ? 1 : _levels[levelIndex];
        return (min, max);
    }

    // Constant and transition functions for a custom scale, X..Y where 0 <= X,Y <= 255 and X < Y
    private static Func<double, byte> Keep(byte value) => (double _) => value;
    private static Func<double, byte> Up(int levelIndex, byte from, byte to)
    {
        var (min, max) = GetMinMax(levelIndex);
        return (double value) => (byte)Math.Min(to, from + (to - from) * (value - min) * (1f / (max - min)));
    }
    private static Func<double, byte> Down(int levelIndex, byte from, byte to)
    {
        var (min, max) = GetMinMax(levelIndex);
        return (double value) => (byte)Math.Min(from, to + (from - to) * (max - value) * (1f / (max - min)));
    }

    private static Func<double, byte>[] MakeColorScale(params byte[] values)
    {
        var result = new List<Func<double, byte>>();

        byte current = values[0];
        int index = 0;

        foreach (var value in values.Skip(1))
        {
            if (current < value)
                result.Add(Up(index, current, value));
            else if (current > value)
                result.Add(Down(index, current, value));
            else
                result.Add(Keep(value));

            current = value;
            index++;
        }

        return result.ToArray();
    }
}
