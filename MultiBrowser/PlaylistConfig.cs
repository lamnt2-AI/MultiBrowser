using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MultiBrowser
{
    public class PlaylistConfig
    {
        public bool LoopContinuous { get; set; } = true;
        public int DelayBetweenLinksSec { get; set; } = 3;
        public List<PlaylistItem> Items { get; set; } = new();

        private static string GetConfigPath() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playlist.json");

        public static PlaylistConfig Load()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<PlaylistConfig>(json);
                    if (config != null)
                    {
                        if (config.Items == null)
                        {
                            config.Items = new List<PlaylistItem>();
                        }
                        else
                        {
                            foreach (var item in config.Items)
                            {
                                if (item.VideoCount < 1) item.VideoCount = 1;
                                if (item.FixedSeconds < 1) item.FixedSeconds = 5;
                            }
                        }
                        if (config.DelayBetweenLinksSec < 0) config.DelayBetweenLinksSec = 0;
                        return config;
                    }
                }
            }
            catch { }

            // Tạo danh sách mẫu mặc định nếu chưa có
            var defaultConfig = new PlaylistConfig();
            defaultConfig.Items.Add(new PlaylistItem
            {
                Title = "YouTube Shorts Mẫu",
                Url = "https://www.youtube.com/@lam-kid-studio/shorts",
                Platform = PlatformType.YouTubeShorts,
                Strategy = ScrollStrategy.BottomToTop,
                DurationMode = WatchDurationMode.FullVideoDuration,
                VideoCount = 3,
                AutoLike = true
            });
            defaultConfig.Items.Add(new PlaylistItem
            {
                Title = "TikTok Mẫu",
                Url = "https://www.tiktok.com",
                Platform = PlatformType.TikTok,
                Strategy = ScrollStrategy.TopToBottom,
                DurationMode = WatchDurationMode.FixedSeconds,
                FixedSeconds = 12,
                VideoCount = 3,
                AutoLike = true
            });

            return defaultConfig;
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
