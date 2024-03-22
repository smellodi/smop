using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Smop.Common;

public static class ScreenLogger
{
    public static void Print(params string[] lines)
    {
        if (_isConsoleApp)
        {
            if (lines.Length == 0)
                Console.WriteLine();
            else
                foreach (var line in lines)
                    Console.WriteLine(line);
        }
        else
        {
            if (lines.Length == 0)
                Debug.WriteLine("");
            else foreach (var line in lines)
                    Debug.WriteLine(line);
        }
    }

    // Internal

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    static readonly bool _isConsoleApp = GetConsoleWindow() != IntPtr.Zero;
}
