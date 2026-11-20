using System;

namespace MultiBrowser
{
    public enum PlatformType
    {
        YouTubeShorts,
        YouTubeLong,
        TikTok,
        FacebookReels,
        FacebookVideo,
        InstagramReels,
        OtherWeb
    }

    public enum ScrollStrategy
    {
        TopToBottom,     // Từ trên xuống (Video mới nhất -> cũ dần)
        BottomToTop      // Cuộn xuống đáy rồi lướt ngược lên (Cũ nhất -> mới dần)
    }

    public enum WatchDurationMode
    {
        FullVideoDuration, // Xem hết thời lượng thực tế của video (duration)
        FixedSeconds,      // Xem cố định X giây
        RandomRange        // Xem ngẫu nhiên trong khoảng Min - Max giây
    }

    // Loại danh sách link cần quét từ trang kênh
    public enum ChannelLinkKind
    {
        TikTok,          // /@user  -> các link /video/
        YouTubeShorts,   // /@kênh/shorts -> các link /shorts/
        YouTubeLong      // /@kênh/videos -> các link /watch?v= (video dài / master)
    }

    public class VideoPlaybackState
    {
        public bool Found { get; set; } = false;
        public bool AdShowing { get; set; } = false; // YouTube đang chiếu quảng cáo (chưa phải video chính)
        public double Duration { get; set; } = 0;
        public double CurrentTime { get; set; } = 0;
        public bool Paused { get; set; } = true;
        public bool Ended { get; set; } = false;
    }

    public class PlaylistItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public PlatformType Platform { get; set; } = PlatformType.YouTubeShorts;
        public ScrollStrategy Strategy { get; set; } = ScrollStrategy.TopToBottom;
        public WatchDurationMode DurationMode { get; set; } = WatchDurationMode.FullVideoDuration;
        public int FixedSeconds { get; set; } = 15;
        public int MinSeconds { get; set; } = 10;
        public int MaxSeconds { get; set; } = 30;
        public int VideoCount { get; set; } = 5; // Số video cần xem trước khi chuyển sang link tiếp theo (hoặc 1 nếu là video dài)
        public bool AutoLike { get; set; } = true;
        public bool AutoInteract { get; set; } = false; // Tương tác thêm (like/share/profile)

        public string PlatformBadge
        {
            get
            {
                return Platform switch
                {
                    PlatformType.YouTubeShorts => "🔴 YT Shorts",
                    PlatformType.YouTubeLong => "📺 YT Video Dài",
                    PlatformType.TikTok => "🎵 TikTok",
                    PlatformType.FacebookReels => "📘 FB Reels",
                    PlatformType.FacebookVideo => "📘 FB Video",
                    PlatformType.InstagramReels => "📷 Insta Reels",
                    _ => "🌐 Web Link"
                };
            }
        }

        public string StrategyDescription =>
            Strategy == ScrollStrategy.BottomToTop 
                ? "Cuộn đáy ➔ Lướt ngược lên (Cũ nhất)" 
                : "Lướt từ trên xuống (Mới nhất)";

        public string DurationDescription =>
            DurationMode switch
            {
                WatchDurationMode.FullVideoDuration => "Xem hết video (Full Duration)",
                WatchDurationMode.RandomRange => $"Ngẫu nhiên {MinSeconds}s - {MaxSeconds}s",
                _ => $"Cố định {FixedSeconds}s"
            };
    }
}
