using System;
using System.IO;
using System.Windows;

namespace Smop.MainApp.Utils;

internal static class Resources
{
    public readonly static string UriBase = Path.GetDirectoryName(Application.Current.StartupUri.LocalPath) ?? "";
    public static Uri GetUri(string filename, UriKind kind = UriKind.Relative)
    {
        return new Uri(Path.Combine(UriBase, filename), kind);
    }
    public static string GetVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version == null ? "X" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
