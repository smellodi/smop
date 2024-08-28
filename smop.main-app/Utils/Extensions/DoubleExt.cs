namespace Smop.MainApp.Utils.Extensions;

internal static class DoubleExt
{
    public static string ToTime(this double seconds, double? durationSec = null)
    {
        double s = seconds % 60;
        int m = (int)(seconds - s) / 60 % 60;
        int h = (int)(seconds - s) / 3600 % 60;

        bool onlySeconds = durationSec == null ? m == 0 && h == 0 : durationSec < 60;
        bool onlySecondsAndMinutes = durationSec == null ? h == 0 : durationSec < 3600;

        string result;
        if (onlySeconds)
            result = $"{s:N1}"; 
        else if (onlySecondsAndMinutes)
            result = $"{m}:{s:00}";
        else
            result = $"{h}:{m:00}:{s:00}";

        return result;
    }
}
