using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Smop.MainApp.Dialogs;

internal static class DialogTools
{
    public static void HideWindowButtons(Window window)
    {
        if (window.IsLoaded)
        {
            RemoveSysMenu(window);
        }
        else
        {
            window.Loaded += (s, e) => RemoveSysMenu(window);
        }
    }

    public static void SetCentralPosition(Window window)
    {
        if (!Application.Current.MainWindow.IsLoaded || Application.Current.Dispatcher.Thread != System.Threading.Thread.CurrentThread)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.ShowInTaskbar = true;
        }
        else
        {
            window.Owner = Application.Current.MainWindow;
        }
    }

    public static T ShowSafe<T>(Func<T> createAndShow)
    {
        if (Application.Current.Dispatcher.Thread != System.Threading.Thread.CurrentThread)
        {
            return Application.Current.Dispatcher.Invoke(createAndShow);
        }
        else
        {
            return createAndShow();
        }
    }

    // Internal

    const int GWL_STYLE = -16;
    const int WS_SYSMENU = 0x80000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private static void RemoveSysMenu(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        _ = SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
    }
}
