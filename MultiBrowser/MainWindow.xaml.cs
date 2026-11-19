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
            _settings.WindowCount = count;
            _settings.Save();
            UpdateLayoutRadioSelection(count);
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

            // Đồng bộ trạng thái Split 2 Tab nếu đang bật
            if (_isAllSplit)
            {
                foreach (var pane in _panes)
                {
                    pane.SetSplitMode(true);
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
            string oldUrl1 = _settings.DefaultUrlTab1;
            string oldUrl2 = _settings.DefaultUrlTab2;

            var dlg = new SettingsWindow(_settings) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                // Cập nhật cài đặt mới
                TxtIntervalSec.Text = _settings.AutoWatchIntervalSec.ToString();
                ChkAutoLike.IsChecked = _settings.AutoLikeEnabled;
                UpdateLayoutRadioSelection(_settings.WindowCount);
                await SwitchWindowCountAsync(_settings.WindowCount);

                // Nếu có cập nhật URL mặc định, áp dụng ngay cho các ô đang hoạt động
                if (oldUrl1 != _settings.DefaultUrlTab1 || oldUrl2 != _settings.DefaultUrlTab2)
                {
                    foreach (var pane in GetActivePanes())
                    {
                        pane.UpdateDefaultUrls(_settings.DefaultUrlTab1, _settings.DefaultUrlTab2);
                    }
                }

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

        public void SetSplitModeAll(bool split)
        {
            _isAllSplit = split;
            foreach (var pane in _panes)
            {
                pane.SetSplitMode(split);
            }
            TxtAutoStatus.Text = _isAllSplit 
                ? $"Đã bật chế độ chia đôi DỌC (Trái: YouTube | Phải: TikTok) trên tất cả các ô!" 
                : "Đã trở về chế độ 1 tab.";
        }

        private void BtnAllSplitTab_Click(object sender, RoutedEventArgs e)
        {
            SetSplitModeAll(!_isAllSplit);
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
            var cts = _autoLoopCts;
            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                }
                catch { }
            }

            foreach (var pane in _panes)
            {
                _ = pane.StopAutoScrollAsync();
            }
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

            var currentCts = new CancellationTokenSource();
            _autoLoopCts = currentCts;
            var token = currentCts.Token;

            BtnStartAutoWatch.IsEnabled = false;
            BtnStartAutoWatch.Opacity = 0.6;
            if (BtnStartPlaylist != null) { BtnStartPlaylist.IsEnabled = false; BtnStartPlaylist.Opacity = 0.6; }
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
                    // Lấy động danh sách các ô đang hiển thị (thích ứng ngay nếu đổi layout)
                    activePanes = GetActivePanes().ToList();
                    if (!activePanes.Any()) break;

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
                bool isLastRun = Interlocked.CompareExchange(ref _autoLoopCts, null, currentCts) == currentCts;
                currentCts.Dispose();

                if (isLastRun)
                {
                    if (BtnStartAutoWatch != null) { BtnStartAutoWatch.IsEnabled = true; BtnStartAutoWatch.Opacity = 1.0; }
                    if (BtnStartPlaylist != null) { BtnStartPlaylist.IsEnabled = true; BtnStartPlaylist.Opacity = 1.0; }
                    TxtAutoStatus.Text = $"⏹ Hoàn tất: Đã xem {_totalViewedCount} video, thả {_totalLikedCount} ❤️.";
                }
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
            await StartPlaylistExecutionAsync(allowDialog: true);
        }

        private async Task StartPlaylistExecutionAsync(bool allowDialog = false)
        {
            StopAutoLoop();

            if (_playlistConfig.Items.Count == 0)
            {
                if (allowDialog)
                {
                    var dlg = new PlaylistWindow(_playlistConfig) { Owner = this };
                    if (dlg.ShowDialog() != true || !dlg.ShouldStartImmediately) return;
                }
                else
                {
                    TxtAutoStatus.Text = "⚠️ Danh sách kịch bản đang trống!";
                    return;
                }
            }

            var currentCts = new CancellationTokenSource();
            _autoLoopCts = currentCts;
            var token = currentCts.Token;

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
                bool isLastRun = Interlocked.CompareExchange(ref _autoLoopCts, null, currentCts) == currentCts;
                currentCts.Dispose();

                if (isLastRun)
                {
                    if (BtnStartPlaylist != null) { BtnStartPlaylist.IsEnabled = true; BtnStartPlaylist.Opacity = 1.0; }
                    if (BtnStartAutoWatch != null) { BtnStartAutoWatch.IsEnabled = true; BtnStartAutoWatch.Opacity = 1.0; }
                }
            }
        }

        // Trang kênh TikTok dạng https://www.tiktok.com/@name (không phải link một video cụ thể)
        private string _lastLikeReport = "";
        private string _lastScrollReport = "";

        // So khớp URL video theo đường dẫn (bỏ query/fragment, không phân biệt hoa thường)
        private static bool UrlMatchesVideo(string current, string expected)
        {
            if (!Uri.TryCreate(current, UriKind.Absolute, out var a) || !Uri.TryCreate(expected, UriKind.Absolute, out var b)) return false;
            return string.Equals(a.AbsolutePath.TrimEnd('/'), b.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTikTokProfileUrl(string url)
        {
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)) return false;
            if (!uri.Host.EndsWith("tiktok.com", StringComparison.OrdinalIgnoreCase)) return false;
            var path = uri.AbsolutePath.Trim('/');
            return path.StartsWith("@") && !path.Contains('/');
        }

        private static bool IsYouTubeShortsChannelUrl(string url)
        {
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)) return false;
            if (!uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase)) return false;
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            return (parts.Length == 2 && parts[0].StartsWith("@") && parts[1].Equals("shorts", StringComparison.OrdinalIgnoreCase))
                || (parts.Length == 3 && parts[0].Equals("channel", StringComparison.OrdinalIgnoreCase) && parts[2].Equals("shorts", StringComparison.OrdinalIgnoreCase));
        }

        private async Task RunPlaylistCampaignAsync(PlaylistConfig config, CancellationToken token)
        {
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

                    // Tự động thích ứng với các ô hiển thị thực tế (nếu người dùng hoặc API đổi số ô)
                    var activePanes = GetActivePanes().ToList();
                    if (!activePanes.Any())
                    {
                        TxtAutoStatus.Text = "⚠️ Không có ô trình duyệt nào sẵn sàng!";
                        await Task.Delay(1500, token);
                        continue;
                    }

                    TxtAutoStatus.Text = $"▶ [Vòng {loopCount} - Bước {i + 1}/{config.Items.Count}] Đang mở: {step.Title} ({step.PlatformBadge})...";

                    // 1. Điều hướng tất cả các ô active đến URL của bước này
                    foreach (var pane in activePanes)
                    {
                        pane.Navigate(step.Url);
                    }

                    // Chờ nạp trang ban đầu
                    await Task.Delay(3500, token);

                    // TikTok kênh: lấy danh sách link video một lần rồi mở lần lượt từng link cho cả các ô,
                    // để các ô luôn cùng một video và đi đúng tới video cuối (không dựa vào nút "next"/gợi ý của TikTok).
                    List<string>? directLinks = null;
                    bool isYtShortsChannel = step.Platform == PlatformType.YouTubeShorts && IsYouTubeShortsChannelUrl(step.Url);
                    if ((step.Platform == PlatformType.TikTok && IsTikTokProfileUrl(step.Url)) || isYtShortsChannel)
                    {
                        TxtAutoStatus.Text = isYtShortsChannel ? "⏳ Đang quét danh sách Shorts của kênh YouTube..." : "⏳ Đang quét danh sách video của kênh TikTok...";
                        var found = await activePanes[0].CollectTikTokProfileVideoLinksAsync(token, 180, isYtShortsChannel);
                        if (found.Count > 0)
                        {
                            if (step.Strategy == ScrollStrategy.BottomToTop) found.Reverse();
                            directLinks = found;
                        }
                    }

                    // 2. Chiến lược cuộn
                    string direction = "down";
                    if (directLinks != null)
                    {
                        // Không cần cuộn/click: từng video sẽ được mở trực tiếp ở vòng lặp bên dưới
                    }
                    else if (step.Strategy == ScrollStrategy.BottomToTop)
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
                    int targetCount = Math.Max(1, step.VideoCount);
                    if (directLinks != null) targetCount = Math.Min(targetCount, directLinks.Count);
                    for (int vIdx = 1; vIdx <= targetCount; vIdx++)
                    {
                        token.ThrowIfCancellationRequested();

                        // Cập nhật lại activePanes nếu layout vừa đổi
                        activePanes = GetActivePanes().ToList();
                        if (!activePanes.Any()) break;

                        if (directLinks != null)
                        {
                            string videoUrl = directLinks[vIdx - 1];
                            if (vIdx == 1)
                            {
                                TxtAutoStatus.Text = $"▶ [Bước {i + 1}/{config.Items.Count}] Mở video 1/{targetCount} của kênh...";
                                if (isYtShortsChannel)
                                {
                                    // Bấm vào video đầu ngay trong trang kênh (không mở thẳng link); ô nào không bấm được/không vào đúng video thì mở link dự phòng
                                    var clickRes = await Task.WhenAll(activePanes.Select(p => p.ClickGridVideoAsync(videoUrl)));
                                    await Task.Delay(3500, token);
                                    bool fixedAny = false;
                                    int openedByClick = activePanes.Count(p => UrlMatchesVideo(p.CurrentUrl, videoUrl));
                                    foreach (var pane in activePanes)
                                    {
                                        if (!UrlMatchesVideo(pane.CurrentUrl, videoUrl)) { pane.Navigate(videoUrl); fixedAny = true; }
                                    }
                                    _lastScrollReport = $"video 1: bấm từ lưới kênh {openedByClick}/{activePanes.Count} (bấm được {clickRes.Count(r => r)}), phải mở link {activePanes.Count - openedByClick}";
                                    if (fixedAny) await Task.Delay(3500, token);
                                }
                                else
                                {
                                    foreach (var pane in activePanes) pane.Navigate(videoUrl);
                                    await Task.Delay(3500, token);
                                }
                            }
                            else
                            {
                                // Cuộn như người thật (lăn chuột thật, mỗi ô lệch nhịp một chút)
                                TxtAutoStatus.Text = $"⏭️ [Bước {i + 1}/{config.Items.Count}] Cuộn sang video {vIdx}/{targetCount}...";
                                await Task.WhenAll(activePanes.Select(async p =>
                                {
                                    await Task.Delay(Random.Shared.Next(200, 1500), token);
                                    await p.HumanScrollNextAsync(isYtShortsChannel);
                                }));
                                await Task.Delay(3000, token);

                                // Ô nào vẫn đứng nguyên ở video cũ (cú lăn chưa đủ mạnh) thì lăn thêm, tối đa 2 lần, trước khi dùng link dự phòng
                                string prevUrl = directLinks[vIdx - 2];
                                for (int retry = 0; retry < 2; retry++)
                                {
                                    var stuck = activePanes.Where(p => UrlMatchesVideo(p.CurrentUrl, prevUrl)).ToList();
                                    if (stuck.Count == 0) break;
                                    await Task.WhenAll(stuck.Select(async p =>
                                    {
                                        await Task.Delay(Random.Shared.Next(200, 900), token);
                                        await p.HumanScrollNextAsync(isYtShortsChannel);
                                    }));
                                    await Task.Delay(2500, token);
                                }

                                // Đối chiếu: ô nào không ở đúng video kế tiếp của kênh (nhảy quá/sang video gợi ý) thì đưa về đúng link
                                int scrolledOk = activePanes.Count(p => UrlMatchesVideo(p.CurrentUrl, videoUrl));
                                bool corrected = false;
                                foreach (var pane in activePanes)
                                {
                                    if (!UrlMatchesVideo(pane.CurrentUrl, videoUrl))
                                    {
                                        pane.Navigate(videoUrl);
                                        corrected = true;
                                    }
                                }
                                _lastScrollReport = $"video {vIdx}: cuộn thật {scrolledOk}/{activePanes.Count}, phải mở link {activePanes.Count - scrolledOk}";
                                if (corrected) await Task.Delay(3500, token);
                            }
                            await Task.WhenAll(activePanes.Select(p => p.EnsurePlayerStartedAsync(step.Platform)));
                            await Task.Delay(1500, token);
                        }

                        // Thả tym nếu bật
                        if (step.AutoLike)
                        {
                            // Tym có kiểm chứng (đọc lại nút, chưa đỏ thì bấm lại, tối đa 4 lần) và ghi lại kết quả từng acc để dễ tìm lỗi
                            var likeResults = await Task.WhenAll(activePanes.Select(async p =>
                            {
                                string res = await p.LikeVerifiedAsync(step.Platform, token);
                                if (res == "LIKED_SUCCESS")
                                {
                                    Interlocked.Increment(ref _totalLikedCount);
                                }
                                return $"acc{p.ProfileId}={res}";
                            }));
                            _lastLikeReport = $"video {vIdx}: " + string.Join(", ", likeResults);
                        }

                        // Mô phỏng tương tác thêm nếu bật
                        if (step.AutoInteract)
                        {
                            await Task.WhenAll(activePanes.Select(p => p.AutoInteractPlatformAsync(step.Platform)));
                        }

                        Interlocked.Increment(ref _totalViewedCount);

                        // Giữ video (loop) ngay từ đầu để autoplay không trôi sang video khác, làm các ô bị lệch nhau.
                        // Với FullVideoDuration: video lặp nên không còn 'ended'; vòng chờ bên dưới vẫn thoát được nhờ
                        // currentTime sát cuối hoặc mốc dự phòng (duration + 2s).
                        await Task.WhenAll(activePanes.Select(p => p.HoldCurrentVideoAsync()));

                        // Thời lượng xem
                        if (step.DurationMode == WatchDurationMode.FullVideoDuration)
                        {
                            double targetDuration = 0;
                            int elapsedSec = 0;
                            const int maxWaitSec = 86400; // Hỗ trợ cả video dài nhiều giờ

                            // Vòng lặp theo dõi tiến trình thực tế của video theo thời gian thực
                            while (!token.IsCancellationRequested && elapsedSec < maxWaitSec)
                            {
                                VideoPlaybackState? state = null;
                                try
                                {
                                    state = await activePanes[0].GetVideoPlaybackStateAsync();
                                }
                                catch { }

                                if (state != null && state.Found)
                                {
                                    if (state.Duration > 0)
                                    {
                                        targetDuration = state.Duration;
                                    }

                                    // Chỉ coi video đã phát xong khi đã xem ít nhất 3s VÀ (state.Ended hoặc currentTime đã tới gần cuối video)
                                    bool isFinished = elapsedSec >= 3 && (state.Ended || (targetDuration > 0 && state.CurrentTime >= targetDuration - 0.5));
                                    if (isFinished)
                                    {
                                        TxtAutoStatus.Text = $"▶ [Bước {i + 1}/{config.Items.Count}] Video {vIdx}/{targetCount} | Đã xem hết video ({FormatTime(targetDuration)}) | Đã thả {_totalLikedCount} ❤️";
                                        break;
                                    }
                                }

                                elapsedSec++;

                                if (targetDuration > 0)
                                {
                                    double curTime = (state != null && state.CurrentTime > 0) ? state.CurrentTime : elapsedSec;
                                    string curStr = FormatTime(curTime);
                                    string durStr = FormatTime(targetDuration);
                                    TxtAutoStatus.Text = $"▶ [Bước {i + 1}/{config.Items.Count}] Video {vIdx}/{targetCount} | Đang phát ({curStr} / {durStr}) | Đã thả {_totalLikedCount} ❤️";

                                    // Nếu đã phát vượt quá tổng thời lượng + 2 giây thì kết thúc
                                    if (elapsedSec >= Math.Ceiling(targetDuration) + 2)
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    // Trong vài giây đầu đợi nạp video, nếu sau 8s vẫn không có thẻ video thì dùng FixedSeconds fallback
                                    int fallbackSec = step.FixedSeconds > 0 ? step.FixedSeconds : 15;
                                    TxtAutoStatus.Text = $"▶ [Bước {i + 1}/{config.Items.Count}] Video {vIdx}/{targetCount} | Đang nạp video ({elapsedSec}s/{fallbackSec}s)... | Đã thả {_totalLikedCount} ❤️";
                                    if (elapsedSec >= fallbackSec)
                                    {
                                        break;
                                    }
                                }

                                await Task.Delay(1000, token);
                            }
                        }
                        else if (step.DurationMode == WatchDurationMode.RandomRange)
                        {
                            int minSec = Math.Clamp(Math.Min(step.MinSeconds, step.MaxSeconds), 1, 86400);
                            int maxSec = Math.Clamp(Math.Max(step.MinSeconds, step.MaxSeconds), minSec, 86400);
                            int rSec = (minSec == maxSec) ? minSec : new Random().Next(minSec, maxSec + 1);
                            for (int s = rSec; s > 0; s--)
                            {
                                token.ThrowIfCancellationRequested();
                                TxtAutoStatus.Text = $"▶ [Bước {i + 1}/{config.Items.Count}] Video {vIdx}/{targetCount} | Ngẫu nhiên: next sau {s}s... | Đã thả {_totalLikedCount} ❤️";
                                await Task.Delay(1000, token);
                            }
                        }
                        else
                        {
                            int fixedSec = Math.Clamp(step.FixedSeconds, 1, 86400);
                            for (int s = fixedSec; s > 0; s--)
                            {
                                token.ThrowIfCancellationRequested();
                                TxtAutoStatus.Text = $"▶ [Bước {i + 1}/{config.Items.Count}] Video {vIdx}/{targetCount} | Chuyển sau {s}s... | Đã thả {_totalLikedCount} ❤️";
                                await Task.Delay(1000, token);
                            }
                        }

                        // Next sang video tiếp theo
                        if (vIdx < targetCount && directLinks == null)
                        {
                            await Task.WhenAll(activePanes.Select(p => p.ReleaseCurrentVideoAsync()));
                            string prevUrl = activePanes[0].CurrentUrl;
                            TxtAutoStatus.Text = $"⏭️ Đang lướt sang video tiếp theo ({direction})...";
                            await Task.WhenAll(activePanes.Select(p => p.ScrollPlatformAsync(step.Platform, direction)));
                            await Task.Delay(2500, token);

                            string newUrl = activePanes[0].CurrentUrl;
                            // Nếu URL không đổi sau khi lướt (và đã xem ít nhất 2 video): kiểm tra xem có phải đã hết video trên kênh
                            if (!string.IsNullOrEmpty(prevUrl) && !string.IsNullOrEmpty(newUrl) && prevUrl == newUrl && vIdx >= 2)
                            {
                                await Task.WhenAll(activePanes.Select(p => p.ScrollPlatformAsync(step.Platform, direction)));
                                await Task.Delay(2000, token);
                                if (activePanes[0].CurrentUrl == prevUrl)
                                {
                                    TxtAutoStatus.Text = $"✅ Đã xem hết toàn bộ video trên {step.Title} ({vIdx} video). Đang chuyển sang mục tiêu tiếp theo...";
                                    break; // Thoát khỏi bước này để chuyển sang link tiếp theo!
                                }
                            }
                        }
                    }

                    // Xong bước: giữ nguyên video cuối cùng trên mọi ô (kể cả chế độ FullVideoDuration)
                    if (activePanes.Any())
                    {
                        await Task.WhenAll(activePanes.Select(p => p.HoldCurrentVideoAsync()));
                    }

                    // Nghỉ giữa các bước liên kết
                    if (i < config.Items.Count - 1 || config.LoopContinuous)
                    {
                        int delaySec = Math.Clamp(config.DelayBetweenLinksSec, 0, 86400);
                        TxtAutoStatus.Text = $"⏳ Xong bước {i + 1}/{config.Items.Count}. Nghỉ {delaySec}s trước khi mở link tiếp theo...";
                        int delayMs = (int)Math.Min((long)delaySec * 1000, (long)int.MaxValue);
                        await Task.Delay(delayMs, token);
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
                likeReport = _lastLikeReport,
                scrollReport = _lastScrollReport,
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

        public bool ExecuteLayoutActionFromApi(string action)
        {
            switch (action.ToLowerInvariant())
            {
                case "all_tab1":
                    BtnAllTab1_Click(this, new RoutedEventArgs());
                    return true;
                case "all_tab2":
                    BtnAllTab2_Click(this, new RoutedEventArgs());
                    return true;
                case "split_all":
                    SetSplitModeAll(true);
                    return true;
                case "unsplit_all":
                    SetSplitModeAll(false);
                    return true;
                case "reload_all":
                    BtnReloadAll_Click(this, new RoutedEventArgs());
                    return true;
                default:
                    return false;
            }
        }

        // Chẩn đoán (và tùy chọn bấm) nút tym của một ô theo profileId. Dùng để tìm lỗi tym ở từng acc.
        public async Task<object> LikeDiagnosticsFromApiAsync(int profileId, bool click)
        {
            var pane = _panes.FirstOrDefault(p => p.ProfileId == profileId && p.IsBrowserReady);
            if (pane == null)
                return new { success = false, error = $"Không tìm thấy ô sẵn sàng với profileId={profileId}." };

            string before = await pane.GetLikeDiagnosticsAsync();
            string? clickResult = null;
            string? after = null;
            if (click)
            {
                clickResult = await pane.LikeCurrentPlatformAsync(PlatformType.TikTok);
                await Task.Delay(1500);
                after = await pane.GetLikeDiagnosticsAsync();
            }

            object? Parse(string? j)
            {
                if (j == null) return null;
                try { return System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(j); }
                catch { return j; }
            }

            return new { success = true, profileId, before = Parse(before), clickResult, after = Parse(after) };
        }

        public void SetMuteFromApi(bool mute)
        {
            BtnMuteAll.IsChecked = mute;
            BtnToggleMuteAll_Click(this, new RoutedEventArgs());
        }

        public async Task<List<PlaylistItem>> AutoArrangeAndRunPlaylistFromApiAsync(PlaylistApiRequest request)
        {
            int videoCountPerLink = Math.Clamp(request.VideoCountPerLink, 1, 10000);
            int delayBetweenLinksSec = Math.Clamp(request.DelayBetweenLinksSec, 0, 86400);
            int fixedSeconds = Math.Clamp(request.FixedSeconds, 1, 86400);

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
                        strategy = ScrollStrategy.TopToBottom;
                    }

                    WatchDurationMode durationMode = WatchDurationMode.FullVideoDuration;
                    if (!string.IsNullOrWhiteSpace(request.DurationMode) && 
                        Enum.TryParse<WatchDurationMode>(request.DurationMode, true, out var parsedMode))
                    {
                        durationMode = parsedMode;
                    }

                    bool isSpecificSingleVideo = (platform == PlatformType.YouTubeLong && (link.Contains("watch?v=") || link.Contains("youtu.be/"))) ||
                                                (platform == PlatformType.FacebookVideo && (link.Contains("/videos/") || link.Contains("/watch/")));
                    int count = isSpecificSingleVideo
                        ? 1
                        : videoCountPerLink;

                    newItems.Add(new PlaylistItem
                    {
                        Url = link.Trim(),
                        Title = title,
                        Platform = platform,
                        Strategy = strategy,
                        DurationMode = durationMode,
                        FixedSeconds = fixedSeconds,
                        VideoCount = count,
                        AutoLike = request.AutoLike
                    });
                }
            }

            if (!newItems.Any())
            {
                return newItems;
            }

            StopAutoLoop();

            _playlistConfig.Items = newItems;
            _playlistConfig.LoopContinuous = request.LoopContinuous;
            _playlistConfig.DelayBetweenLinksSec = delayBetweenLinksSec;
            _playlistConfig.Save();

            // Khởi chạy ngay lập tức trên app ở background, không block API response (tránh timeout khi loop continuous)
            _ = StartPlaylistExecutionAsync(allowDialog: false);

            return newItems;
        }

        public void StopAutomationFromApi()
        {
            StopAutoLoop();
        }

        private static string FormatTime(double totalSeconds)
        {
            if (double.IsNaN(totalSeconds) || double.IsInfinity(totalSeconds) || totalSeconds < 0) return "00:00";
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return ts.TotalHours >= 1 
                ? ts.ToString(@"hh\:mm\:ss") 
                : ts.ToString(@"mm\:ss");
        }
    }
}
