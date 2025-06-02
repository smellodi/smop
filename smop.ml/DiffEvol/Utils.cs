using System;

namespace Smop.ML.DiffEvol;

internal static class Utils
{
    public static double NextDouble(this Random rnd, double min, double max) =>
        rnd.NextDouble() * (max - min) + min;
}
