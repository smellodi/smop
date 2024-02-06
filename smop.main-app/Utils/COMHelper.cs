using Smop.MainApp.Dialogs;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Smop.MainApp.Utils;

internal static class COMHelper
{
    /// <summary>
    /// Runs a synchronous action asynchronously and waits till it finishes
    /// </summary>
    /// <param name="action">Action to run asynchronously</param>
    public static async Task Do(Action action)
    {
        try
        {
            await Task.Run(action);
        }
        catch (TaskCanceledException) { }
    }

    /// <summary>
    /// Runs a synchronous action asynchronously in the displatcher loop and waits till it finishes.
    /// </summary>
    /// <param name="dispatcher">An UI dispatcher to execute the action within</param>
    /// <param name="action">Action to run asynchronously</param>
    public static async Task Do(Dispatcher dispatcher, Action action)
    {
        try
        {
            await Task.Run(() => dispatcher.Invoke(action));
        }
        catch (TaskCanceledException) { }
    }

    /// <summary>
    /// Logs the result returned from the OdorDisplay method and shows an error message
    /// if this result is not <see cref="Comm.Error.Success"/>
    /// </summary>
    /// <param name="result">The result retuirned from an OdorDisplay method</param>
    /// <param name="action">action explanation in a readable format that follows after "Cannot"</param>
    /// <returns>Error flag</returns>
    public static bool ShowErrorIfAny(Comm.Result result, string action)
    {
        var actionLog = string.Join("", action.Split(' ').Select(p => p.Length > 0 ? char.ToUpper(p[0]) + (p.Length > 1 ? p[1..] : "") : ""));
        Logging.LogIO.Add(result, actionLog, Logging.LogSource.OD);

        bool isOK = result.Error == Comm.Error.Success;
        if (!isOK)
        {
            MsgBox.Error("Odor Display", $"Cannot {action}:\n{result.Reason}");
        }

        return isOK;
    }
}
