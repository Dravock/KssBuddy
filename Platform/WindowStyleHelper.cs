using System;
using System.Windows;
using System.Windows.Interop;

namespace KssBuddy.Platform
{
    /// <summary>Schaltet Click-Through und versteckt das Fenster aus Alt+Tab.</summary>
    internal static class WindowStyleHelper
    {
        public static void HideFromAltTab(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            NativeMethods.SetExStyle(hwnd, NativeMethods.GetExStyle(hwnd) | NativeMethods.WS_EX_TOOLWINDOW);
        }

        public static void SetClickThrough(Window window, bool enabled)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            int style = NativeMethods.GetExStyle(hwnd) | NativeMethods.WS_EX_LAYERED;
            style = enabled ? style | NativeMethods.WS_EX_TRANSPARENT
                            : style & ~NativeMethods.WS_EX_TRANSPARENT;
            NativeMethods.SetExStyle(hwnd, style);
        }
    }
}
