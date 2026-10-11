using System;
using System.Diagnostics;
using System.Text;

namespace KssBuddy.Platform
{
    /// <summary>
    /// Erkennt, ob gerade eine fremde Anwendung im Vollbild läuft (Spiel, Video, Präsentation).
    /// Ansatz: Vordergrundfenster deckt den kompletten Monitor ab, ist nicht der Desktop und nicht unser Prozess.
    /// </summary>
    internal static class FullscreenDetector
    {
        private static readonly uint OwnPid = (uint)Process.GetCurrentProcess().Id;

        public static bool IsForegroundFullscreen()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == OwnPid) return false;

            // Desktop / Shell nicht als Vollbild werten
            var cls = new StringBuilder(64);
            NativeMethods.GetClassName(hwnd, cls, cls.Capacity);
            string name = cls.ToString();
            if (name is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;

            if (!NativeMethods.GetWindowRect(hwnd, out var win)) return false;

            IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return false;

            var m = info.rcMonitor;
            return win.Left <= m.Left && win.Top <= m.Top && win.Right >= m.Right && win.Bottom >= m.Bottom;
        }
    }
}
