using System.Linq;

namespace Smop.MainApp.Logging;

internal enum LogSource { OD, SNT }

internal static class LogIO
{
    public static char LOG_DELIM => '\t';

    /// <summary>
    /// Formats the logging string out of an array of data to log
    /// </summary>
    /// <param name="objs">data to log</param>
    /// <returns>Formatted text</returns>
    public static string Text(params object?[] objs) => string.Join(LOG_DELIM, objs);

    /// <summary>
    /// Logs a result of calling a method from an OdorDisplay of SmellInsp
    /// </summary>
    /// <param name="result">OdorDisplays of SmellInsp's method calling result</param>
    /// <param name="action">OdorDisplays of SmellInsp's method name or action that was called.
    /// Preferrably, the text should be formated using the PascalCase format.</param>
    /// <param name="source"><see cref="LogSource"/> value</param>
    /// <returns>Flag of error</returns>
    public static bool Add(Comm.Result result, string action, LogSource source)
    {
        bool isOK = result.Error == Comm.Error.Success;
        action = string.Join("", action.Split(' ').Select(p => char.ToUpper(p[0]) + p[1..]));

        if (isOK)
            _nlog.Info(Text(source, action, "OK"));
        else
            _nlog.Error(Text(source, action, result.Error, result.Reason));
        return isOK;
    }

    /// <summary>
    /// Logs a result of calling a method from an IonVision
    /// </summary>
    /// <typeparam name="T">Type of the returned data structure</typeparam>
    /// <param name="response">Response from IonVision method</param>
    /// <param name="action">IonVision's method name or action that was called.
    /// Preferrably, the text should be formated using the PascalCase format.</param>
    /// <returns>Flag of error</returns>
    public static bool Add<T>(IonVision.Response<T> response, string action) =>
        Add(response, action, out T? _);

    /// <summary>
    /// Logs a result of calling a method from an IonVision and passes through the calling method response
    /// </summary>
    /// <typeparam name="T">Type of the returned data structure</typeparam>
    /// <param name="response">Response from IonVision method</param>
    /// <param name="action">IonVision's method name or action that was called.
    /// Preferrably, the text should be formated using the PascalCase format.</param>
    /// <param name="resp">The response passed through</param>
    /// <returns>Flag of error</returns>
    public static bool Add<T>(IonVision.Response<T> response, string action, out T? resp)
    {
        if (response.Success)
        {
            var value = response.Value switch
            {
                null => "-",
                IonVision.Defs.Confirm _ => "OK",
                IonVision.Defs.SystemInfo info => $"v{info.CurrentVersion}",
                IonVision.Scan.ScanResult scan => $"{scan.MeasurementData.Ucv.Length}x{scan.MeasurementData.Usv.Length}",
                IonVision.Param.ParameterDefinition paramDef => paramDef.Name,
                string[] arr => string.Join("; ", arr),
                _ => response.Value.ToString()
            };
            _nlog.Info(Text("DMS", action, "OK", value));
        }
        else
        {
            _nlog.Error(Text("DMS", action, "Error", response.Error));
        }

        resp = response.Value;
        return response.Success;
    }

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger("IO");
}
