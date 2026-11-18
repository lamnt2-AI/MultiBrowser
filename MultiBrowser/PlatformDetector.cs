using System;

namespace MultiBrowser
{
    public static class PlatformDetector
    {
        public static (PlatformType Platform, string SuggestedTitle, int DefaultCount) Detect(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return (PlatformType.OtherWeb, "Trang Web", 1);

            string lower = url.Trim().ToLowerInvariant();

            // 1. YouTube Shorts (bao gồm: kênh /shorts, /shorts/, và video shorts)
            if ((lower.Contains("youtube.com") || lower.Contains("youtu.be")) && lower.Contains("/shorts"))
            {
                return (PlatformType.YouTubeShorts, "YouTube Shorts", 5);
            }

            // 2. YouTube Long Video / Channel Video
            if (lower.Contains("youtube.com/watch") || lower.Contains("youtu.be/") || 
                lower.Contains("youtube.com/live/") || (lower.Contains("youtube.com/") && !lower.Contains("/shorts")))
            {
                bool isSpecificVideo = lower.Contains("watch?v=") || lower.Contains("youtu.be/");
                return (PlatformType.YouTubeLong, isSpecificVideo ? "YouTube Video Dài (Master)" : "Kênh YouTube", isSpecificVideo ? 1 : 3);
            }

            // 3. TikTok
            if (lower.Contains("tiktok.com"))
            {
                return (PlatformType.TikTok, "TikTok Video / Kênh", 5);
            }

            // 4. Facebook Reels
            if (lower.Contains("facebook.com/reel") || lower.Contains("fb.watch/reel") || lower.Contains("facebook.com/reels"))
            {
                return (PlatformType.FacebookReels, "Facebook Reels", 5);
            }

            // 5. Facebook Long Video / Watch
            if (lower.Contains("facebook.com/watch") || lower.Contains("facebook.com/") && lower.Contains("/videos/"))
            {
                return (PlatformType.FacebookVideo, "Facebook Video Dài", 1);
            }

            // 6. Instagram Reels
            if (lower.Contains("instagram.com/reels") || lower.Contains("instagram.com/reel/") || lower.Contains("instagram.com/p/"))
            {
                return (PlatformType.InstagramReels, "Instagram Reels", 5);
            }

            return (PlatformType.OtherWeb, "Liên kết Web", 1);
        }
    }
}
