using System;

namespace Smop.MainApp.Utils;

internal static class IoHelper
{
    public static string GetShortestFilePath(string filename)
    {
        var relativeFilename = System.IO.Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, filename);
        if (relativeFilename.StartsWith(".."))
        {
            return filename;
        }
        else
        {
            return relativeFilename;
        }
    }
}
