using System;

namespace Smop.ML.Search;

internal static class DebugDisplay
{
    public static void WriteLine(string? msg = null)
    {
        if (msg != null)
        {
            System.Diagnostics.Debug.WriteLine("[ML.DE] " + msg);
            Console.Out.WriteLine(msg);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(string.Empty);
            Console.Out.WriteLine();
        }
    }

    public static void Write(string msg)
    {
        System.Diagnostics.Debug.Write("[ML.DE] " + msg);
        Console.Out.Write(msg);
    }
}
