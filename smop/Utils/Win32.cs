using System.Runtime.InteropServices;

namespace Smop.Utils
{
    public static class Win32
    {
        [DllImport("User32.dll")]
        public static extern bool SetCursorPos(int x, int y);
    }
}
