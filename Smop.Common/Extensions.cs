namespace Smop.Common;

public static class StringExtension
{
    public static string Max(this string self, int maxLength, bool printSkippedCharsCount = true)
    {
        var suffix = printSkippedCharsCount ? $"... and {self.Length - maxLength} chars more." : null;
        return self.Length > maxLength ? (self[..maxLength] + suffix) : self;
    }
}