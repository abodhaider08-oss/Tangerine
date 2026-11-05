using System;
using System.IO;
using System.Text.Json;

namespace TangerineWin
{
    public class AppSettings
    {
        public string TriggerName { get; set; } = "Shift_Or_X";
        public int TriggerVkCode { get; set; } = 0x00; // 0x00 = Both Shift (0x10) AND X (0x58) active simultaneously!
        public string ThemeName { get; set; } = "AppleWhite";

        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null) return loaded;
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
