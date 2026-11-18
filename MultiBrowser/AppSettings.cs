using System;
using System.IO;
using System.Text.Json;

namespace MultiBrowser
{
    public class AppSettings
    {
        public int WindowCount { get; set; } = 4;
        public string DefaultUrlTab1 { get; set; } = "https://www.youtube.com/@lam-kid-studio/shorts";
        public string DefaultUrlTab2 { get; set; } = "https://www.tiktok.com";
        public int AutoWatchIntervalSec { get; set; } = 8;
        public bool AutoLikeEnabled { get; set; } = true;

        private static string GetConfigPath() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        // Validate window count (must be 1, 2, 4, 6, 8)
                        if (settings.WindowCount != 1 && settings.WindowCount != 2 && 
                            settings.WindowCount != 4 && settings.WindowCount != 6 && 
                            settings.WindowCount != 8)
                        {
                            settings.WindowCount = 4;
                        }
                        return settings;
                    }
                }
            }
            catch { }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
