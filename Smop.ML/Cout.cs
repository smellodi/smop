﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Smop.ML;

public class Cout : TextWriter, IDisposable
{
    public override Encoding Encoding => Encoding.ASCII;

    /// <summary>
    /// Call this to launch the static constructor
    /// </summary>
    public static void Init() { }

    public override void Write(string? output)
    {
        _lastLine = output != "\n" ? output : null;
        _stdOutWriter.Write(output);
    }

    public override void WriteLine(string? output)
    {
        bool printLastLine = Console.CursorLeft > 0;
        if (printLastLine)
        {
            Console.CursorLeft = 0;
        }

        _stdOutWriter.WriteLine(output);

        if (printLastLine)
        {
            _stdOutWriter.Write(_lastLine);
        }
        else
        {
            _lastLine = null;
        }
    }

    // Internal

    readonly TextWriter _stdOutWriter;

    static string? _lastLine = null;

    static Cout _instance;

    static Cout()
    {
        _instance = new Cout();
    }

    private Cout()
    {
        _stdOutWriter = Console.Out;
        Console.SetOut(this);
    }
}

internal static class StringExt
{
    public static string Max(this string self, int maxLength) => self.Length > maxLength ? self[..maxLength] : self;
}