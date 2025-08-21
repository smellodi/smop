using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Smop.MainApp.Utils;

internal static class Resources
{
    public static string UriBase => Path.GetDirectoryName(Application.Current.StartupUri.LocalPath) ?? "";

    public static Uri GetUri(string filename, UriKind kind = UriKind.Relative) => new(Path.Combine(UriBase, filename), kind);

    public static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var result = new List<string>();

        if (assembly.GetName().Version is Version version)
            result.Add($"{version.Major}.{version.Minor}");

        try
        {
            var buffer = new byte[2048];
            using (var stream = new FileStream(assembly.Location, FileMode.Open, FileAccess.Read))
                stream.Read(buffer, 0, 2048);

            var offset = BitConverter.ToInt32(buffer, PE_HEADER_OFFET);
            var secondsSince1970 = BitConverter.ToInt32(buffer, offset + LINKER_TIMESTAMP_OFFSET);
            var epoch = new DateTime(1970, 1, 1);

            var linkTimeUtc = epoch.AddSeconds(secondsSince1970);

            var localTime = TimeZoneInfo.ConvertTimeFromUtc(linkTimeUtc, TimeZoneInfo.Local);
            result.Add(localTime.ToString("(yyyy.MM.dd HH.mm)"));
        }
        catch { }

        return string.Join(" ", result);
    }

    // Internal

    const int PE_HEADER_OFFET = 60;
    const int LINKER_TIMESTAMP_OFFSET = 8;
}
