using System;
using System.IO;
using System.Text.Json;

namespace KssBuddy.Services
{
    /// <summary>Alle Benutzereinstellungen. Werden als JSON unter %AppData%\KssBuddy\settings.json gespeichert.</summary>
    internal sealed class AppSettings
    {
        public string CharacterId { get; set; } = "slytherin";
        public double Scale { get; set; } = 1.0;            // Fenstergröße: 0.5 .. 2.0
        public double Opacity { get; set; } = 1.0;          // Deckkraft: 0.3 .. 1.0
        public bool Topmost { get; set; } = true;
        public bool ClickThrough { get; set; }
        public bool AutoActions { get; set; } = true;       // gelegentlich von selbst strecken/winken
        public int Fps { get; set; } = 30;                  // 15, 30 oder 60
        public bool PauseOnFullscreen { get; set; } = true;
        public bool Autostart { get; set; }
        public double? Left { get; set; }                   // zuletzt verwendete Fensterposition
        public double? Top { get; set; }
    }

    /// <summary>Lädt und speichert die Einstellungen. Fehler werden ignoriert (dann gelten Standardwerte).</summary>
    internal static class SettingsService
    {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        private static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KssBuddy", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
            catch { /* kaputte Datei -> Standardwerte */ }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Json));
            }
            catch { /* Einstellung geht nur verloren, kein Absturz */ }
        }
    }
}
