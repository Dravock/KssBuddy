using System;
using Microsoft.Win32;

namespace KssBuddy.Services
{
    /// <summary>Trägt das Programm im Windows-Autostart des aktuellen Benutzers ein (HKCU, keine Adminrechte nötig).</summary>
    internal static class AutostartService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "KssBuddy";

        public static void Set(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (key == null) return;
                if (enabled)
                {
                    string? exe = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exe)) key.SetValue(ValueName, $"\"{exe}\"");
                }
                else
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }
            catch { /* z. B. durch Richtlinien gesperrt: Einstellung wirkt dann einfach nicht */ }
        }
    }
}
