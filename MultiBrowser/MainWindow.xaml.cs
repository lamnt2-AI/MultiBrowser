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
        private CancellationTokenSource? _autoLoopCts;
        private int _totalLikedCount = 0;
        private int _totalViewedCount = 0;
        private bool _isAllSplit = false;
        private bool _isMuteAll = false;
        private BrowserPane[] _panes = Array.Empty<BrowserPane>();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _panes = new[] { Pane1, Pane2, Pane3, Pane4 };
            ApplyLayout();

            // Khởi tạo cả 4 ô trình duyệt độc lập với 4 Profile khác nhau: Tab 1 = YouTube Shorts, Tab 2 = TikTok
            var tasks = new[]
            {
                Pane1.InitializeAsync(1, "Acc 1 (Góc Trái Trên)", "https://www.youtube.com/@lam-kid-studio/shorts", "https://www.tiktok.com"),
                Pane2.InitializeAsync(2, "Acc 2 (Góc Phải Trên)", "https://www.youtube.com/@lam-kid-studio/shorts", "https://www.tiktok.com"),
                Pane3.InitializeAsync(3, "Acc 3 (Góc Trái Dưới)", "https://www.youtube.com/@lam-kid-studio/shorts", "https://www.tiktok.com"),
                Pane4.InitializeAsync(4, "Acc 4 (Góc Phải Dưới)", "https://www.youtube.com/@lam-kid-studio/shorts", "https://www.tiktok.com")
            };

            await Task.WhenAll(tasks);
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

        private void LayoutChanged(object sender, RoutedEventArgs e)
        {
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (BrowserContainer == null || Pane1 == null || Pane2 == null || Pane3 == null || Pane4 == null) return;

            BrowserContainer.RowDefinitions.Clear();
            BrowserContainer.ColumnDefinitions.Clear();

            // Reset spans
            Grid.SetRowSpan(Pane1, 1);
            Grid.SetColumnSpan(Pane1, 1);
            Grid.SetRowSpan(Pane2, 1);
            Grid.SetColumnSpan(Pane2, 1);
            Grid.SetRowSpan(Pane3, 1);
            Grid.SetColumnSpan(Pane3, 1);
            Grid.SetRowSpan(Pane4, 1);
            Grid.SetColumnSpan(Pane4, 1);

            if (RbLayout4Grid.IsChecked == true)
            {
                // 4 Ô: Lưới 2x2
                BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(Pane1, 0); Grid.SetColumn(Pane1, 0); Pane1.Visibility = Visibility.Visible;
                Grid.SetRow(Pane2, 0); Grid.SetColumn(Pane2, 1); Pane2.Visibility = Visibility.Visible;
                Grid.SetRow(Pane3, 1); Grid.SetColumn(Pane3, 0); Pane3.Visibility = Visibility.Visible;
                Grid.SetRow(Pane4, 1); Grid.SetColumn(Pane4, 1); Pane4.Visibility = Visibility.Visible;
            }
            else if (RbLayout3Focus.IsChecked == true)
            {
                // 3 Ô: 1 Ô lớn bên trái + 2 ô nhỏ xếp dọc bên phải
                BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
                BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(Pane1, 0); Grid.SetColumn(Pane1, 0);
                Grid.SetRowSpan(Pane1, 2);
                Pane1.Visibility = Visibility.Visible;

                Grid.SetRow(Pane2, 0); Grid.SetColumn(Pane2, 1); Pane2.Visibility = Visibility.Visible;
                Grid.SetRow(Pane3, 1); Grid.SetColumn(Pane3, 1); Pane3.Visibility = Visibility.Visible;
                Pane4.Visibility = Visibility.Collapsed;
            }
            else if (RbLayout3H.IsChecked == true)
            {
                // 3 Ô: 3 cột ngang (1x3)
                BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(Pane1, 0); Grid.SetColumn(Pane1, 0); Pane1.Visibility = Visibility.Visible;
                Grid.SetRow(Pane2, 0); Grid.SetColumn(Pane2, 1); Pane2.Visibility = Visibility.Visible;
                Grid.SetRow(Pane3, 0); Grid.SetColumn(Pane3, 2); Pane3.Visibility = Visibility.Visible;
                Pane4.Visibility = Visibility.Collapsed;
            }
            else if (RbLayout2H.IsChecked == true)
            {
                // 2 Ô: 2 cột chia đôi
                BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(Pane1, 0); Grid.SetColumn(Pane1, 0); Pane1.Visibility = Visibility.Visible;
                Grid.SetRow(Pane2, 0); Grid.SetColumn(Pane2, 1); Pane2.Visibility = Visibility.Visible;
                Pane3.Visibility = Visibility.Collapsed;
                Pane4.Visibility = Visibility.Collapsed;
            }
            else if (RbLayout1.IsChecked == true)
            {
                // 1 Ô: Toàn màn hình
                BrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                BrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(Pane1, 0); Grid.SetColumn(Pane1, 0); Pane1.Visibility = Visibility.Visible;
                Pane2.Visibility = Visibility.Collapsed;
                Pane3.Visibility = Visibility.Collapsed;
                Pane4.Visibility = Visibility.Collapsed;
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
            TxtAutoStatus.Text = "Đã chuyển tất cả các ô sang Tab 1 (YouTube)";
        }

        private void BtnAllTab2_Click(object sender, RoutedEventArgs e)
        {
            _isAllSplit = false;
            foreach (var pane in GetActivePanes())
            {
                pane.SelectTab(2);
            }
            TxtAutoStatus.Text = "Đã chuyển tất cả các ô sang Tab 2 (TikTok)";
        }

        private void BtnAllSplitTab_Click(object sender, RoutedEventArgs e)
        {
            _isAllSplit = !_isAllSplit;
            foreach (var pane in GetActivePanes())
            {
                pane.SetSplitMode(_isAllSplit);
            }
            TxtAutoStatus.Text = _isAllSplit 
                ? "Đã bật chế độ chia đôi DỌC (Trái: YouTube | Phải: TikTok) trên tất cả các ô!" 
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
                intervalSec = 8;
                TxtIntervalSec.Text = "8";
            }

            string direction = CmbDirection.SelectedIndex == 1 ? "up" : "down";
            bool shouldLike = ChkAutoLike.IsChecked == true;

            _autoLoopCts = new CancellationTokenSource();
            var token = _autoLoopCts.Token;

            BtnStartAutoWatch.IsEnabled = false;
            BtnStartAutoWatch.Opacity = 0.6;
            TxtAutoStatus.Text = "▶ Bắt đầu kiểm tra mở player Shorts...";

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
                        TxtAutoStatus.Text = $"▶ Đang xem Video #{_totalViewedCount} | Đã thả {_totalLikedCount} ❤️ | Chuyển tiếp sau {s}s...";
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
