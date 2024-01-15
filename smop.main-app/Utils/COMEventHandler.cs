using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Smop.MainApp.Utils;

internal static class CommPortEventHandler
{
    public static async Task Do(Action action)
    {
        try
        {
            await Task.Run(action);
        }
        catch (TaskCanceledException) { }
    }

    public static async Task Do(Dispatcher dispatcher, Action action)
    {
        try
        {
            await Task.Run(() => dispatcher.Invoke(action));
        }
        catch (TaskCanceledException) { }
    }
}
