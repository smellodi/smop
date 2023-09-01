using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Smop.PulseGen.Dialogs
{
    internal class DialogTools
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
}
