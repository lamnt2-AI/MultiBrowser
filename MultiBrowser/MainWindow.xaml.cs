using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MultiBrowser
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings = new();
        private PlaylistConfig _playlistConfig = new();
        private CancellationTokenSource? _autoLoopCts;
        private int _totalLikedCount = 0;
        private int _totalViewedCount = 0;
        private bool _isAllSplit = false;
        private bool _isMuteAll = false;
        private int _currentWindowCount = 4;
        private BrowserPane[] _panes = Array.Empty<BrowserPane>();
        private MultiBrowserApiServer? _apiServer;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _panes = new[] { Pane1, Pane2, Pane3, Pane4, Pane5, Pane6, Pane7, Pane8 };

            // Khởi chạy Server điều khiển API cục bộ cho MCP Agent (Port 5055)
            _apiServer = new MultiBrowserApiServer(this, 5055);
            _apiServer.Start();

            // Tải cấu hình từ settings.json & playlist.json
            _settings = AppSettings.Load();
            _playlistConfig = PlaylistConfig.Load();
            _currentWindowCount = _settings.WindowCount;

            // Đồng bộ cấu hình lên giao diện tự động hóa
            TxtIntervalSec.Text = _settings.AutoWatchIntervalSec.ToString();
            ChkAutoLike.IsChecked = _settings.AutoLikeEnabled;
            UpdateLayoutRadioSelection(_currentWindowCount);

            // Áp dụng bố cục và khởi tạo các ô được chọn
            await SwitchWindowCountAsync(_currentWindowCount);
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            StopAutoLoop();
            _apiServer?.Dispose();
            foreach (var pane in _panes)
            {
                pane.Dispose();
            }
        }

        private IEnumerable<BrowserPane> GetActivePanes() => 
            _panes.Where(p => p.Visibility == Visibility.Visible && p.IsBrowserReady);

        private void UpdateLayoutRadioSelection(int count)
        {
            if (RbLayout2 == null || RbLayout4 == null || RbLayout6 == null || RbLayout8 == null || RbLayout1 == null) return;

            RbLayout2.IsChecked = (count == 2);
            RbLayout4.IsChecked = (count == 4);
            RbLayout6.IsChecked = (count == 6);
            RbLayout8.IsChecked = (count == 8);
            RbLayout1.IsChecked = (count == 1);
        }

        private async void LayoutChanged(object sender, RoutedEventArgs e)
        {
            int targetCount = 4;
            if (RbLayout2.IsChecked == true) targetCount = 2;
            else if (RbLayout4.IsChecked == true) targetCount = 4;
            else if (RbLayout6.IsChecked == true) targetCount = 6;
            else if (RbLayout8.IsChecked == true) targetCount = 8;
            else if (RbLayout1.IsChecked == true) targetCount = 1;

            _settings.WindowCount = targetCount;
            _settings.Save();

            await SwitchWindowCountAsync(targetCount);
        }

        public async Task SwitchWindowCountAsync(int count)
        {
            _currentWindowCount = count;
            ApplyGridLayout(count);

            // Khởi tạo các ô chưa được nạp trong số các ô đang hiển thị (Lazy Init)
            var initTasks = new List<Task>();
            for (int i = 0; i < count && i < _panes.Length; i++)
            {
                var pane = _panes[i];
                if (!pane.IsBrowserReady)
                {
                    int profileId = i + 1;
                    string name = $"Acc {profileId} (Profile {profileId})";
                    initTasks.Add(pane.InitializeAsync(profileId, name, _settings.DefaultUrlTab1, _settings.DefaultUrlTab2));
                }
            }

            if (initTasks.Any())
            {
                TxtAutoStatus.Text = $"⏳ Đang khởi tạo thêm {initTasks.Count} cửa sổ trình duyệt...";
                await Task.WhenAll(initTasks);
                TxtAutoStatus.Text = $"✅ Đã sẵn sàng {count} cửa sổ trình duyệt!";
            }

            // Đồng bộ trạng thái âm thanh nếu đang Mute All
            if (_isMuteAll)
            {
                foreach (var pane in _panes)
                {
                    pane.SetMuteAll(true);
                }
            }
        }

        private void ApplyGridLayout(int count)
        {
            if (BrowserContainer == null || _panes.Length == 0) return;

            BrowserContainer.RowDefinitions.Clear();
            BrowserContainer.ColumnDefinitions.Clear();

            for (int i = 0; i < _panes.Length; i++)
            {
                Grid.SetRowSpan(_panes[i], 1);
                Grid.SetColumnSpan(_panes[i], 1);
            }

            switch (count)
            {
                case 1:
                    // 1 Ô: Toàn màn hình
                    BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    Grid.SetRow(_panes[0], 0); Grid.SetColumn(_panes[0], 0); _panes[0].Visibility = Visibility.Visible;
                    for (int i = 1; i < _panes.Length; i++) _panes[i].Visibility = Visibility.Collapsed;
                    break;

                case 2:
                    // 2 Ô: 1 Hàng x 2 Cột (1x2)
                    BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    Grid.SetRow(_panes[0], 0); Grid.SetColumn(_panes[0], 0); _panes[0].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[1], 0); Grid.SetColumn(_panes[1], 1); _panes[1].Visibility = Visibility.Visible;
                    for (int i = 2; i < _panes.Length; i++) _panes[i].Visibility = Visibility.Collapsed;
                    break;

                case 6:
                    // 6 Ô: 2 Hàng x 3 Cột (2x3)
                    BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    Grid.SetRow(_panes[0], 0); Grid.SetColumn(_panes[0], 0); _panes[0].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[1], 0); Grid.SetColumn(_panes[1], 1); _panes[1].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[2], 0); Grid.SetColumn(_panes[2], 2); _panes[2].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[3], 1); Grid.SetColumn(_panes[3], 0); _panes[3].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[4], 1); Grid.SetColumn(_panes[4], 1); _panes[4].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[5], 1); Grid.SetColumn(_panes[5], 2); _panes[5].Visibility = Visibility.Visible;
                    for (int i = 6; i < _panes.Length; i++) _panes[i].Visibility = Visibility.Collapsed;
                    break;

                case 8:
                    // 8 Ô: 2 Hàng x 4 Cột (2x4)
                    BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    Grid.SetRow(_panes[0], 0); Grid.SetColumn(_panes[0], 0); _panes[0].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[1], 0); Grid.SetColumn(_panes[1], 1); _panes[1].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[2], 0); Grid.SetColumn(_panes[2], 2); _panes[2].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[3], 0); Grid.SetColumn(_panes[3], 3); _panes[3].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[4], 1); Grid.SetColumn(_panes[4], 0); _panes[4].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[5], 1); Grid.SetColumn(_panes[5], 1); _panes[5].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[6], 1); Grid.SetColumn(_panes[6], 2); _panes[6].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[7], 1); Grid.SetColumn(_panes[7], 3); _panes[7].Visibility = Visibility.Visible;
                    break;

                case 4:
                default:
                    // 4 Ô: 2 Hàng x 2 Cột (2x2)
                    BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    Grid.SetRow(_panes[0], 0); Grid.SetColumn(_panes[0], 0); _panes[0].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[1], 0); Grid.SetColumn(_panes[1], 1); _panes[1].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[2], 1); Grid.SetColumn(_panes[2], 0); _panes[2].Visibility = Visibility.Visible;
                    Grid.SetRow(_panes[3], 1); Grid.SetColumn(_panes[3], 1); _panes[3].Visibility = Visibility.Visible;
                    for (int i = 4; i < _panes.Length; i++) _panes[i].Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private async void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(_settings) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                // Cập nhật cài đặt mới
                TxtIntervalSec.Text = _settings.AutoWatchIntervalSec.ToString();
                ChkAutoLike.IsChecked = _settings.AutoLikeEnabled;
                UpdateLayoutRadioSelection(_settings.WindowCount);
                await SwitchWindowCountAsync(_settings.WindowCount);
                TxtAutoStatus.Text = $"⚙️ Đã áp dụng cài đặt: {_settings.WindowCount} cửa sổ!";
            }
        }

        private void BtnSyncUrl_Click(object sender, RoutedEventArgs e)
        {
            SyncNavigate(TxtGlobalUrl.Text);
        }

        private void TxtGlobalUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SyncNavigate(TxtGlobalUrl.Text);
            }
        }

        private void SyncNavigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            foreach (var pane in GetActivePanes())
            {
                pane.Navigate(url);
            }
        }

        private void BtnGoogleAll_Click(object sender, RoutedEventArgs e)
        {
            SyncNavigate("https://accounts.google.com");
        }

        private void BtnReloadAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var pane in GetActivePanes())
            {
                pane.Reload();
            }
        }

        private void BtnAllTab1_Click(object sender, RoutedEventArgs e)
        {
            _isAllSplit = false;
            foreach (var pane in GetActivePanes())
            {
                pane.SelectTab(1);
            }
            TxtAutoStatus.Text = $"Đã chuyển tất cả {GetActivePanes().Count()} ô sang Tab 1 (YouTube)";
        }

        private void BtnAllTab2_Click(object sender, RoutedEventArgs e)
        {
            _isAllSplit = false;
            foreach (var pane in GetActivePanes())
            {
                pane.SelectTab(2);
            }
            TxtAutoStatus.Text = $"Đã chuyển tất cả {GetActivePanes().Count()} ô sang Tab 2 (TikTok)";
        }

        private void BtnAllSplitTab_Click(object sender, RoutedEventArgs e)
        {
            _isAllSplit = !_isAllSplit;
            foreach (var pane in GetActivePanes())
            {
                pane.SetSplitMode(_isAllSplit);
            }
            TxtAutoStatus.Text = _isAllSplit 
                ? $"Đã bật chế độ chia đôi DỌC (Trái: YouTube | Phải: TikTok) trên tất cả các ô!" 
                : "Đã trở về chế độ 1 tab.";
        }

        private void BtnOpenProfilesFolder_Click(object sender, RoutedEventArgs e)
        {
            string profilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles");
            Directory.CreateDirectory(profilesPath);
            Process.Start(new ProcessStartInfo("explorer.exe", profilesPath) { UseShellExecute = true });
        }

        private void BtnToggleMuteAll_Click(object sender, RoutedEventArgs e)
        {
            _isMuteAll = BtnMuteAll.IsChecked == true;
            foreach (var pane in _panes)
            {
                pane.SetMuteAll(_isMuteAll);
            }

            if (_isMuteAll)
            {
                BtnMuteAll.Content = "🔇 ĐÃ TẮT HẾT ÂM";
                BtnMuteAll.Foreground = System.Windows.Media.Brushes.OrangeRed;
                BtnMuteAll.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#450A0A")!;
                TxtAutoStatus.Text = "🔇 Đã tắt toàn bộ âm thanh trên tất cả các ô trình duyệt.";
            }
            else
            {
                BtnMuteAll.Content = "🔊 Âm thanh: BẬT";
                BtnMuteAll.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#10B981")!;
                BtnMuteAll.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1F2937")!;
                TxtAutoStatus.Text = "🔊 Đã bật lại âm thanh.";
            }
        }

        private async void BtnAutoShorts_Click(object sender, RoutedEventArgs e)
        {
            TxtAutoStatus.Text = "⏳ Đang chuyển sang tab Shorts và tự động cuộn đến cuối...";
            var tasks = GetActivePanes().Select(p => p.AutoOpenShortsAndScrollAsync());
            await Task.WhenAll(tasks);
            TxtAutoStatus.Text = "✅ Đã hoàn tất cuộn đến video Short cuối cùng!";
        }

        private async void BtnScrollBottom_Click(object sender, RoutedEventArgs e)
        {
            TxtAutoStatus.Text = "⏳ Đang cuộn xuống đáy trang trên tất cả các ô...";
            var tasks = GetActivePanes().Select(p => p.ScrollToBottomAsync());
            await Task.WhenAll(tasks);
            TxtAutoStatus.Text = "✅ Đã cuộn đến đáy trang!";
        }

        private async void BtnNextShort_Click(object sender, RoutedEventArgs e)
        {
            TxtAutoStatus.Text = "⏭️ Đang chuyển sang Short tiếp theo...";
            var tasks = GetActivePanes().Select(p => p.NextShortAsync());
            await Task.WhenAll(tasks);
            TxtAutoStatus.Text = "Sẵn sàng";
        }

        private async void BtnStopAuto_Click(object sender, RoutedEventArgs e)
        {
            StopAutoLoop();
            TxtAutoStatus.Text = "⏹️ Đã gửi lệnh dừng tự động";
            var tasks = GetActivePanes().Select(p => p.StopAutoScrollAsync());
            await Task.WhenAll(tasks);
        }

        private void StopAutoLoop()
        {
            if (_autoLoopCts != null)
            {
                try
                {
                    _autoLoopCts.Cancel();
                }
                catch { }
                finally
                {
                    _autoLoopCts.Dispose();
                    _autoLoopCts = null;
                }
            }

            if (BtnStartPlaylist != null) { BtnStartPlaylist.IsEnabled = true; BtnStartPlaylist.Opacity = 1.0; }
            if (BtnStartAutoWatch != null) { BtnStartAutoWatch.IsEnabled = true; BtnStartAutoWatch.Opacity = 1.0; }
        }

        // Tự động hóa trực tiếp trên UI Dispatcher thread với async/await non-blocking (Chuẩn Concurrency)
        private async void BtnStartAutoWatch_Click(object sender, RoutedEventArgs e)
        {
            StopAutoLoop();

            if (!int.TryParse(TxtIntervalSec.Text, out int intervalSec) || intervalSec < 1)
            {
                intervalSec = _settings.AutoWatchIntervalSec;
                TxtIntervalSec.Text = intervalSec.ToString();
            }

            string direction = CmbDirection.SelectedIndex == 1 ? "up" : "down";
            bool shouldLike = ChkAutoLike.IsChecked == true;

            _autoLoopCts = new CancellationTokenSource();
            var token = _autoLoopCts.Token;

            BtnStartAutoWatch.IsEnabled = false;
            BtnStartAutoWatch.Opacity = 0.6;
            TxtAutoStatus.Text = "▶ Bắt đầu kiểm tra mở player Shorts trên các ô hoạt động...";

            try
            {
                var activePanes = GetActivePanes().ToList();
                if (!activePanes.Any())
                {
                    TxtAutoStatus.Text = "⚠️ Không có ô trình duyệt nào sẵn sàng!";
                    return;
                }

                await Task.WhenAll(activePanes.Select(p => p.EnsureShortsPlayerOpenAsync()));
                await Task.Delay(2000, token);

                while (!token.IsCancellationRequested)
                {
                    // 1. Kiểm tra và thả tym đồng loạt trên các ô đang hiển thị
                    if (shouldLike)
                    {
                        var likeTasks = activePanes.Select(async p =>
                        {
                            var res = await p.CheckAndLikeShortAsync();
                            if (res == "LIKED_SUCCESS")
                            {
                                Interlocked.Increment(ref _totalLikedCount);
                            }
                        });
                        await Task.WhenAll(likeTasks);
                    }

                    Interlocked.Increment(ref _totalViewedCount);

                    // 2. Đếm ngược thời gian xem
                    for (int s = intervalSec; s > 0; s--)
                    {
                        token.ThrowIfCancellationRequested();
                        TxtAutoStatus.Text = $"▶ Đang xem Video #{_totalViewedCount} ({activePanes.Count} ô) | Đã thả {_totalLikedCount} ❤️ | Chuyển tiếp sau {s}s...";
                        await Task.Delay(1000, token);
                    }

                    // 3. Cuộn sang video tiếp theo
                    TxtAutoStatus.Text = $"⏭️ Đang lướt sang video {direction}...";
                    await Task.WhenAll(activePanes.Select(p => p.ScrollShortAsync(direction)));

                    await Task.Delay(1500, token);
                }
            }
            catch (OperationCanceledException)
            {
                TxtAutoStatus.Text = "⏹️ Đã dừng tự động hóa.";
            }
            catch (Exception ex)
            {
                TxtAutoStatus.Text = $"Lỗi: {ex.Message}";
            }
            finally
            {
                BtnStartAutoWatch.IsEnabled = true;
                BtnStartAutoWatch.Opacity = 1.0;
                TxtAutoStatus.Text = $"⏹ Hoàn tất: Đã xem {_totalViewedCount} video, thả {_totalLikedCount} ❤️.";
            }
        }

        // ================= KỊCH BẢN XEM LẦN LƯỢT ĐA NỀN TẢNG =================

        private async void BtnOpenPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PlaylistWindow(_playlistConfig) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.ShouldStartImmediately)
            {
                await StartPlaylistExecutionAsync();
            }
        }

        private async void BtnStartPlaylist_Click(object sender, RoutedEventArgs e)
        {
            await StartPlaylistExecutionAsync();
        }

        private async Task StartPlaylistExecutionAsync()
        {
            StopAutoLoop();

            if (_playlistConfig.Items.Count == 0)
            {
                var dlg = new PlaylistWindow(_playlistConfig) { Owner = this };
                if (dlg.ShowDialog() != true || !dlg.ShouldStartImmediately) return;
            }

            _autoLoopCts = new CancellationTokenSource();
            var token = _autoLoopCts.Token;

            BtnStartPlaylist.IsEnabled = false;
            BtnStartPlaylist.Opacity = 0.6;
            BtnStartAutoWatch.IsEnabled = false;
            BtnStartAutoWatch.Opacity = 0.6;

            try
            {
                await RunPlaylistCampaignAsync(_playlistConfig, token);
            }
            catch (OperationCanceledException)
            {
                TxtAutoStatus.Text = "⏹️ Đã dừng kịch bản xem lần lượt.";
            }
            catch (Exception ex)
            {
                TxtAutoStatus.Text = $"Lỗi kịch bản: {ex.Message}";
            }
            finally
            {
                BtnStartPlaylist.IsEnabled = true;
                BtnStartPlaylist.Opacity = 1.0;
                BtnStartAutoWatch.IsEnabled = true;
                BtnStartAutoWatch.Opacity = 1.0;
            }
        }

        private async Task RunPlaylistCampaignAsync(PlaylistConfig config, CancellationToken token)
        {
            var activePanes = GetActivePanes().ToList();
            if (!activePanes.Any())
            {
                TxtAutoStatus.Text = "⚠️ Không có ô trình duyệt nào sẵn sàng!";
                return;
            }

            if (!config.Items.Any())
            {
                TxtAutoStatus.Text = "⚠️ Kịch bản chưa có liên kết nào!";
                return;
            }

            int loopCount = 1;

            while (!token.IsCancellationRequested)
            {
                for (int i = 0; i < config.Items.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var step = config.Items[i];

                    TxtAutoStatus.Text = $"▶ [Vòng {loopCount} - Bước {i + 1}/{config.Items.Count}] Đang mở: {step.Title} ({step.PlatformBadge})...";

                    // 1. Điều hướng tất cả các ô active đến URL của bước này
                    foreach (var pane in activePanes)
                    {
                        pane.Navigate(step.Url);
                    }

                    // Chờ nạp trang ban đầu
                    await Task.Delay(3500, token);

                    // 2. Chiến lược cuộn
                    string direction = "down";
                    if (step.Strategy == ScrollStrategy.BottomToTop)
                    {
                        TxtAutoStatus.Text = $"⏳ Đang cuộn xuống đáy trang để bắt đầu từ video cũ nhất...";
                        await Task.WhenAll(activePanes.Select(p => p.AutoScrollToBottomAndClickOldestAsync(step.Platform)));
                        direction = "up";
                        await Task.Delay(2000, token);
                    }
                    else
                    {
                        await Task.WhenAll(activePanes.Select(p => p.EnsurePlayerStartedAsync(step.Platform)));
                        await Task.Delay(2000, token);
                    }

                    // 3. Thực thi xem lần lượt N video trong bước này
                    int targetCount = step.VideoCount;
                    for (int vIdx = 1; vIdx <= targetCount; vIdx++)
                    {
                        token.ThrowIfCancellationRequested();

                        // Thả tym nếu bật
                        if (step.AutoLike)
                        {
                            await Task.WhenAll(activePanes.Select(async p =>
                            {
                                var res = await p.LikeCurrentPlatformAsync(step.Platform);
                                if (res == "LIKED_SUCCESS")
                                {
                                    Interlocked.Increment(ref _totalLikedCount);
                                }
                            }));
                        }

                        // Mô phỏng tương tác thêm nếu bật
                        if (step.AutoInteract)
                        {
                            await Task.WhenAll(activePanes.Select(p => p.AutoInteractPlatformAsync(step.Platform)));
                        }

                        Interlocked.Increment(ref _totalViewedCount);

                        // Thời lượng xem
                        if (step.DurationMode == WatchDurationMode.FullVideoDuration)
                        {
                            double detectedDuration = 0;
                            try
                            {
                                var state = await activePanes[0].GetVideoPlaybackStateAsync();
                                if (state.Found && state.Duration > 0)
                                {
                                    detectedDuration = state.Duration;
                                }
                            }
                            catch { }

                            int watchSeconds = (detectedDuration > 0 && detectedDuration < 3600)
                                ? (int)Math.Ceiling(detectedDuration)
                                : step.FixedSeconds;

                            for (int s = watchSeconds; s > 0; s--)
                            {
                                token.ThrowIfCancellationRequested();
                                int elapsed = watchSeconds - s;
                                TxtAutoStatus.Text = $"▶ [Bước {i + 1}/{config.Items.Count}] Video {vIdx}/{targetCount} | Đang xem ({elapsed}s/{watchSeconds}s) | Đã thả {_totalLikedCount} ❤️";
                                await Task.Delay(1000, token);
                            }
                        }
                        else if (step.DurationMode == WatchDurationMode.RandomRange)
                        {
                            int rSec = new Random().Next(Math.Min(step.MinSeconds, step.MaxSeconds), Math.Max(step.MinSeconds, step.MaxSeconds) + 1);
                            for (int s = rSec; s > 0; s--)
                            {
                                token.ThrowIfCancellationRequested();
                                TxtAutoStatus.Text = $"▶ [Bước {i + 1}/{config.Items.Count}] Video {vIdx}/{targetCount} | Ngẫu nhiên: next sau {s}s... | Đã thả {_totalLikedCount} ❤️";
                                await Task.Delay(1000, token);
                            }
                        }
                        else
                        {
                            for (int s = step.FixedSeconds; s > 0; s--)
                            {
                                token.ThrowIfCancellationRequested();
                                TxtAutoStatus.Text = $"▶ [Bước {i + 1}/{config.Items.Count}] Video {vIdx}/{targetCount} | Chuyển sau {s}s... | Đã thả {_totalLikedCount} ❤️";
                                await Task.Delay(1000, token);
                            }
                        }

                        // Next sang video tiếp theo
                        if (vIdx < targetCount)
                        {
                            TxtAutoStatus.Text = $"⏭️ Đang lướt sang video tiếp theo ({direction})...";
                            await Task.WhenAll(activePanes.Select(p => p.ScrollPlatformAsync(step.Platform, direction)));
                            await Task.Delay(1500, token);
                        }
                    }

                    // Nghỉ giữa các bước liên kết
                    if (i < config.Items.Count - 1 || config.LoopContinuous)
                    {
                        TxtAutoStatus.Text = $"⏳ Xong bước {i + 1}/{config.Items.Count}. Nghỉ {config.DelayBetweenLinksSec}s trước khi mở link tiếp theo...";
                        await Task.Delay(config.DelayBetweenLinksSec * 1000, token);
                    }
                }

                if (!config.LoopContinuous)
                {
                    break;
                }

                loopCount++;
            }

            TxtAutoStatus.Text = $"✅ Hoàn tất kịch bản! Đã xem {_totalViewedCount} video, thả {_totalLikedCount} ❤️.";
        }

        // ================= API & MCP SERVER INTEGRATION HOOKS =================

        public object GetServerStatusSnapshot()
        {
            return new
            {
                isRunning = true,
                windowCount = _currentWindowCount,
                isMuteAll = _isMuteAll,
                statusText = TxtAutoStatus.Text,
                isAutomationRunning = _autoLoopCts != null,
                panes = _panes.Select(p => new
                {
                    profileId = p.ProfileId,
                    isReady = p.IsBrowserReady,
                    visibility = p.Visibility.ToString(),
                    url = p.IsBrowserReady ? p.ActiveBrowser.Source?.ToString() : null
                }).ToArray()
            };
        }

        public void NavigateAllFromApi(string url)
        {
            TxtGlobalUrl.Text = url;
            SyncNavigate(url);
        }

        public void ExecuteLayoutActionFromApi(string action)
        {
            switch (action.ToLowerInvariant())
            {
                case "all_tab1":
                    BtnAllTab1_Click(this, new RoutedEventArgs());
                    break;
                case "all_tab2":
                    BtnAllTab2_Click(this, new RoutedEventArgs());
                    break;
                case "split_all":
                    BtnAllSplitTab_Click(this, new RoutedEventArgs());
                    break;
                case "reload_all":
                    BtnReloadAll_Click(this, new RoutedEventArgs());
                    break;
            }
        }

        public void SetMuteFromApi(bool mute)
        {
            BtnMuteAll.IsChecked = mute;
            BtnToggleMuteAll_Click(this, new RoutedEventArgs());
        }

        public async Task<List<PlaylistItem>> AutoArrangeAndRunPlaylistFromApiAsync(PlaylistApiRequest request)
        {
            StopAutoLoop();

            var newItems = new List<PlaylistItem>();
            if (request.Links != null)
            {
                foreach (var link in request.Links)
                {
                    if (string.IsNullOrWhiteSpace(link)) continue;
                    var (platform, title, defaultCount) = PlatformDetector.Detect(link);

                    ScrollStrategy strategy;
                    if (!string.IsNullOrWhiteSpace(request.Strategy) && 
                        Enum.TryParse<ScrollStrategy>(request.Strategy, true, out var customStrategy))
                    {
                        strategy = customStrategy;
                    }
                    else
                    {
                        strategy = (platform == PlatformType.YouTubeShorts) 
                            ? ScrollStrategy.BottomToTop 
                            : ScrollStrategy.TopToBottom;
                    }

                    WatchDurationMode durationMode = WatchDurationMode.FullVideoDuration;
                    if (!string.IsNullOrWhiteSpace(request.DurationMode) && 
                        Enum.TryParse<WatchDurationMode>(request.DurationMode, true, out var parsedMode))
                    {
                        durationMode = parsedMode;
                    }

                    int count = (platform == PlatformType.YouTubeLong || platform == PlatformType.FacebookVideo)
                        ? 1
                        : request.VideoCountPerLink;

                    newItems.Add(new PlaylistItem
                    {
                        Url = link.Trim(),
                        Title = title,
                        Platform = platform,
                        Strategy = strategy,
                        DurationMode = durationMode,
                        FixedSeconds = request.FixedSeconds,
                        VideoCount = count,
                        AutoLike = request.AutoLike
                    });
                }
            }

            _playlistConfig.Items = newItems;
            _playlistConfig.LoopContinuous = request.LoopContinuous;
            _playlistConfig.DelayBetweenLinksSec = request.DelayBetweenLinksSec;
            _playlistConfig.Save();

            // Khởi chạy ngay lập tức trên app
            await StartPlaylistExecutionAsync();

            return newItems;
        }

        public void StopAutomationFromApi()
        {
            StopAutoLoop();
        }
    }
}
