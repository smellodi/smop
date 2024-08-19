using System;

namespace Smop.MainApp.Utils.Extensions;

internal static class StringExt
{
    public static string ToPath(this string s, string replacement = "-")
    {
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        string[] temp = s.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(replacement, temp);
    }
    public static string ToFileNameOnly(this string s) => System.IO.Path.GetFileNameWithoutExtension(s);
    public static string ToCamelCase(this string s) => char.ToLowerInvariant(s[0]) + s.Substring(1);
}
