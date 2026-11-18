using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace MultiBrowser
{
    public partial class PlaylistWindow : Window
    {
        private readonly ObservableCollection<PlaylistItem> _playlist = new();
        public PlaylistConfig Config { get; private set; }
        public bool ShouldStartImmediately { get; private set; } = false;

        public PlaylistWindow(PlaylistConfig config)
        {
            InitializeComponent();
            Config = config ?? new PlaylistConfig();
            if (Config.Items == null) Config.Items = new List<PlaylistItem>();
            Loaded += PlaylistWindow_Loaded;
        }

        private void PlaylistWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _playlist.Clear();
            foreach (var item in Config.Items)
            {
                _playlist.Add(item);
            }

            DgPlaylist.ItemsSource = _playlist;
            ChkLoopContinuous.IsChecked = Config.LoopContinuous;
            TxtDelayBetweenLinks.Text = Config.DelayBetweenLinksSec.ToString();

            // Kích hoạt nhận diện URL mẫu ban đầu
            DetectCurrentUrl();
        }

        private void TxtUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            DetectCurrentUrl();
        }

        private void DetectCurrentUrl()
        {
            if (TxtUrl == null || BadgePlatform == null || TxtPlatformDetected == null || CmbPlatform == null) return;

            string url = TxtUrl.Text.Trim();
            var (platform, title, defaultCount) = PlatformDetector.Detect(url);

            TxtTitle.Text = title;
            TxtVideoCount.Text = defaultCount.ToString();

            switch (platform)
            {
                case PlatformType.YouTubeShorts:
                    BadgePlatform.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#991B1B")!;
                    TxtPlatformDetected.Text = "🔴 Đã nhận diện: YouTube Shorts";
                    CmbPlatform.SelectedIndex = 0;
                    break;
                case PlatformType.YouTubeLong:
                    BadgePlatform.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1E3A8A")!;
                    TxtPlatformDetected.Text = "📺 Đã nhận diện: YouTube Video Dài (Master)";
                    CmbPlatform.SelectedIndex = 1;
                    break;
                case PlatformType.TikTok:
                    BadgePlatform.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#831843")!;
                    TxtPlatformDetected.Text = "🎵 Đã nhận diện: TikTok Video/Channel";
                    CmbPlatform.SelectedIndex = 2;
                    break;
                case PlatformType.FacebookReels:
                    BadgePlatform.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1E40AF")!;
                    TxtPlatformDetected.Text = "📘 Đã nhận diện: Facebook Reels";
                    CmbPlatform.SelectedIndex = 3;
                    break;
                case PlatformType.FacebookVideo:
                    BadgePlatform.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D4ED8")!;
                    TxtPlatformDetected.Text = "📘 Đã nhận diện: Facebook Video Dài";
                    CmbPlatform.SelectedIndex = 4;
                    break;
                case PlatformType.InstagramReels:
                    BadgePlatform.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#701A75")!;
                    TxtPlatformDetected.Text = "📷 Đã nhận diện: Instagram Reels";
                    CmbPlatform.SelectedIndex = 5;
                    break;
                default:
                    BadgePlatform.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#27272A")!;
                    TxtPlatformDetected.Text = "🌐 Liên kết Web thông thường";
                    CmbPlatform.SelectedIndex = 6;
                    break;
            }
        }

        private void BtnAddStep_Click(object sender, RoutedEventArgs e)
        {
            string url = TxtUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Vui lòng nhập đường dẫn URL!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PlatformType platform = CmbPlatform.SelectedIndex switch
            {
                0 => PlatformType.YouTubeShorts,
                1 => PlatformType.YouTubeLong,
                2 => PlatformType.TikTok,
                3 => PlatformType.FacebookReels,
                4 => PlatformType.FacebookVideo,
                5 => PlatformType.InstagramReels,
                _ => PlatformType.OtherWeb
            };

            ScrollStrategy strategy = RbBottomToTop.IsChecked == true 
                ? ScrollStrategy.BottomToTop 
                : ScrollStrategy.TopToBottom;

            WatchDurationMode durationMode = WatchDurationMode.FullVideoDuration;
            if (RbFixedDuration.IsChecked == true) durationMode = WatchDurationMode.FixedSeconds;
            else if (RbRandomDuration.IsChecked == true) durationMode = WatchDurationMode.RandomRange;

            int.TryParse(TxtSecFixed.Text, out int fixedSec);
            fixedSec = Math.Clamp(fixedSec < 1 ? 15 : fixedSec, 1, 86400);

            int.TryParse(TxtSecMax.Text, out int maxSec);
            maxSec = Math.Clamp(maxSec < fixedSec ? fixedSec + 15 : maxSec, fixedSec, 86400);

            int.TryParse(TxtVideoCount.Text, out int videoCount);
            videoCount = Math.Clamp(videoCount < 1 ? 1 : videoCount, 1, 10000);

            var newItem = new PlaylistItem
            {
                Url = url,
                Title = string.IsNullOrWhiteSpace(TxtTitle.Text) ? "Mục tiêu" : TxtTitle.Text.Trim(),
                Platform = platform,
                Strategy = strategy,
                DurationMode = durationMode,
                FixedSeconds = fixedSec,
                MinSeconds = fixedSec,
                MaxSeconds = maxSec,
                VideoCount = videoCount,
                AutoLike = ChkAutoLike.IsChecked == true,
                AutoInteract = ChkAutoInteract.IsChecked == true
            };

            _playlist.Add(newItem);
            TxtStatus.Text = $"Đã thêm bước #{_playlist.Count}: {newItem.Title} ({newItem.PlatformBadge})";
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = DgPlaylist.SelectedIndex;
            if (idx > 0)
            {
                var item = _playlist[idx];
                _playlist.RemoveAt(idx);
                _playlist.Insert(idx - 1, item);
                DgPlaylist.SelectedIndex = idx - 1;
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = DgPlaylist.SelectedIndex;
            if (idx >= 0 && idx < _playlist.Count - 1)
            {
                var item = _playlist[idx];
                _playlist.RemoveAt(idx);
                _playlist.Insert(idx + 1, item);
                DgPlaylist.SelectedIndex = idx + 1;
            }
        }

        private void BtnDeleteStep_Click(object sender, RoutedEventArgs e)
        {
            int idx = DgPlaylist.SelectedIndex;
            if (idx >= 0)
            {
                _playlist.RemoveAt(idx);
            }
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_playlist.Count > 0 && MessageBox.Show("Bạn có chắc muốn xóa toàn bộ các bước trong kịch bản không?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _playlist.Clear();
            }
        }

        private void BtnLoadDefault_Click(object sender, RoutedEventArgs e)
        {
            _playlist.Clear();
            _playlist.Add(new PlaylistItem
            {
                Title = "1. Kênh YouTube Shorts",
                Url = "https://www.youtube.com/@lam-kid-studio/shorts",
                Platform = PlatformType.YouTubeShorts,
                Strategy = ScrollStrategy.BottomToTop,
                DurationMode = WatchDurationMode.FullVideoDuration,
                VideoCount = 3,
                AutoLike = true
            });
            _playlist.Add(new PlaylistItem
            {
                Title = "2. Lướt Video TikTok",
                Url = "https://www.tiktok.com",
                Platform = PlatformType.TikTok,
                Strategy = ScrollStrategy.TopToBottom,
                DurationMode = WatchDurationMode.FixedSeconds,
                FixedSeconds = 12,
                VideoCount = 3,
                AutoLike = true
            });
            _playlist.Add(new PlaylistItem
            {
                Title = "3. Video Dài YouTube (Master)",
                Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                Platform = PlatformType.YouTubeLong,
                Strategy = ScrollStrategy.TopToBottom,
                DurationMode = WatchDurationMode.FullVideoDuration,
                VideoCount = 1,
                AutoLike = true
            });
            TxtStatus.Text = "Đã nạp 3 bước mẫu đa nền tảng!";
        }

        private void SaveConfigFromUI()
        {
            Config.Items.Clear();
            foreach (var item in _playlist)
            {
                Config.Items.Add(item);
            }
            Config.LoopContinuous = ChkLoopContinuous.IsChecked == true;
            if (int.TryParse(TxtDelayBetweenLinks.Text, out int delay))
                Config.DelayBetweenLinksSec = Math.Clamp(delay, 0, 86400);

            Config.Save();
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigFromUI();
            TxtStatus.Text = $"Đã lưu kịch bản ({Config.Items.Count} bước) vào playlist.json!";
            MessageBox.Show($"Đã lưu thành công kịch bản gồm {Config.Items.Count} bước!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnStartPlaylistNow_Click(object sender, RoutedEventArgs e)
        {
            if (_playlist.Count == 0)
            {
                MessageBox.Show("Kịch bản hiện chưa có bước nào! Hãy thêm ít nhất 1 link.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveConfigFromUI();
            ShouldStartImmediately = true;
            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigFromUI();
            DialogResult = false;
            Close();
        }
    }
}
