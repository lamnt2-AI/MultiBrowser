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
        private CancellationTokenSource? _autoLoopCts;
        private int _totalLikedCount = 0;
        private int _totalViewedCount = 0;
        private bool _isAllSplit = false;
        private bool _isMuteAll = false;
        private int _currentWindowCount = 4;
        private BrowserPane[] _panes = Array.Empty<BrowserPane>();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _panes = new[] { Pane1, Pane2, Pane3, Pane4, Pane5, Pane6, Pane7, Pane8 };

            // Tải cấu hình từ settings.json
            _settings = AppSettings.Load();
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
    }
}
