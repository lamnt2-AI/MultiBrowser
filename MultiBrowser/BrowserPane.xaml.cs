using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace MultiBrowser
{
    public partial class BrowserPane : UserControl, IDisposable
    {
        private string _profilePath = string.Empty;
        private int _profileId;
        private bool _isInitialized = false;
        private int _activeTab = 1;
        private bool _isForceMuted = false;
        private CoreWebView2Environment? _env;
        private Task? _initTask;

        public int ProfileId => _profileId;
        public bool IsBrowserReady => _isInitialized;

        public Microsoft.Web.WebView2.Wpf.WebView2 ActiveBrowser => 
            (_activeTab == 2) ? BrowserControl2 : BrowserControl1;

        public Microsoft.Web.WebView2.Wpf.WebView2 YouTubeBrowser => BrowserControl1;
        public Microsoft.Web.WebView2.Wpf.WebView2 TikTokBrowser => BrowserControl2;

        public BrowserPane()
        {
            InitializeComponent();
        }

        public Task InitializeAsync(int profileId, string customName, string defaultUrl1 = "https://www.youtube.com/@lam-kid-studio/shorts", string defaultUrl2 = "https://www.tiktok.com")
        {
            if (_isInitialized) return Task.CompletedTask;
            if (_initTask != null) return _initTask;

            _initTask = DoInitializeAsync(profileId, customName, defaultUrl1, defaultUrl2);
            return _initTask;
        }

        private async Task DoInitializeAsync(int profileId, string customName, string defaultUrl1, string defaultUrl2)
        {
            _profileId = profileId;
            TxtProfileBadge.Text = customName;

            _profilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles", $"Profile_{profileId}");
            Directory.CreateDirectory(_profilePath);

            try
            {
                string proxyFilePath = Path.Combine(_profilePath, "proxy.txt");
                string proxyServer = string.Empty;
                string proxyUser = string.Empty;
                string proxyPass = string.Empty;

                if (File.Exists(proxyFilePath))
                {
                    string[] lines = File.ReadAllLines(proxyFilePath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                        var parts = trimmed.Split(':');
                        if (parts.Length >= 2)
                        {
                            proxyServer = $"{parts[0]}:{parts[1]}";
                            if (parts.Length >= 4)
                            {
                                proxyUser = parts[2];
                                proxyPass = parts[3];
                            }
                            break;
                        }
                    }
                }

                // Cấu hình cờ Chromium: Chống phát hiện, bảo vệ WebRTC và tối ưu bộ nhớ
                string chromiumArgs = "--disable-blink-features=AutomationControlled " +
                                     "--force-webrtc-ip-handling-policy=disable_non_proxied_udp " +
                                     "--disable-features=IsolateOrigins,site-per-process " +
                                     "--no-default-browser-check";

                if (!string.IsNullOrEmpty(proxyServer))
                {
                    chromiumArgs += $" --proxy-server={proxyServer}";
                }

                var envOptions = new CoreWebView2EnvironmentOptions(chromiumArgs);
                _env = await CoreWebView2Environment.CreateAsync(null, _profilePath, envOptions);

                var task1 = InitSingleWebView(BrowserControl1, 1, proxyUser, proxyPass, defaultUrl1);
                var task2 = InitSingleWebView(BrowserControl2, 2, proxyUser, proxyPass, defaultUrl2);

                await Task.WhenAll(task1, task2);

                _isInitialized = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;

                SelectTab(1);
            }
            catch (Exception ex)
            {
                TxtInitStatus.Text = $"Lỗi khởi tạo: {ex.Message}";
            }
        }

        private async Task InitSingleWebView(Microsoft.Web.WebView2.Wpf.WebView2 webView, int tabIndex, string proxyUser, string proxyPass, string initialUrl)
        {
            await webView.EnsureCoreWebView2Async(_env);

            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // Tự phục hồi khi Renderer process gặp sự cố crash
            webView.CoreWebView2.ProcessFailed += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = $"⚠️ Tiến trình duyệt bị gián đoạn ({e.ProcessFailedKind}). Đang tự phục hồi...";
                    webView.Reload();
                });
            };

            // Kịch bản Anti-Detection chuẩn: Xóa webdriver trên Prototype và ẩn định danh WebView2
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                try {
                    const newProto = Object.getPrototypeOf(navigator);
                    delete newProto.webdriver;
                } catch(e) {}

                if (window.chrome && window.chrome.webview) {
                    try {
                        delete window.chrome.webview;
                    } catch(e) {}
                }
            ");

            if (!string.IsNullOrEmpty(proxyUser))
            {
                webView.CoreWebView2.BasicAuthenticationRequested += (s, args) =>
                {
                    args.Response.UserName = proxyUser;
                    args.Response.Password = proxyPass;
                };
            }

            webView.NavigationStarting += (s, e) =>
            {
                if (_activeTab == tabIndex)
                {
                    LoadingBar.Visibility = Visibility.Visible;
                    TxtStatus.Text = $"[Tab {tabIndex}] Đang tải: {e.Uri}...";
                }
            };

            webView.NavigationCompleted += (s, e) =>
            {
                if (_activeTab == tabIndex)
                {
                    LoadingBar.Visibility = Visibility.Collapsed;
                    TxtStatus.Text = e.IsSuccess ? $"[Tab {tabIndex}] Tải xong" : $"[Tab {tabIndex}] Lỗi tải: {e.WebErrorStatus}";
                }
            };

            webView.SourceChanged += (s, e) =>
            {
                if (_activeTab == tabIndex && webView.Source != null)
                {
                    TxtUrl.Text = webView.Source.ToString();
                }
            };

            webView.CoreWebView2.DocumentTitleChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_activeTab == tabIndex)
                    {
                        TxtStatus.Text = $"{webView.CoreWebView2.DocumentTitle}";
                    }
                });
            };

            webView.CoreWebView2.Navigate(initialUrl);
        }

        public void SetMuteAll(bool isMuted)
        {
            _isForceMuted = isMuted;
            if (BrowserControl1.CoreWebView2 != null) BrowserControl1.CoreWebView2.IsMuted = isMuted;
            if (BrowserControl2.CoreWebView2 != null) BrowserControl2.CoreWebView2.IsMuted = isMuted;
        }

        public void SelectTab(int tabIndex)
        {
            _activeTab = tabIndex;
            BtnSplitTab.IsChecked = false;
            RestoreTabContainerLayout();

            if (_activeTab == 1)
            {
                RbTab1.IsChecked = true;
                WrapperTab1.Visibility = Visibility.Visible;
                BrowserControl1.Visibility = Visibility.Visible;

                WrapperTab2.Visibility = Visibility.Collapsed;
                BrowserControl2.Visibility = Visibility.Collapsed;

                if (_isForceMuted)
                {
                    if (BrowserControl1.CoreWebView2 != null) BrowserControl1.CoreWebView2.IsMuted = true;
                    if (BrowserControl2.CoreWebView2 != null) BrowserControl2.CoreWebView2.IsMuted = true;
                }
                else
                {
                    if (BrowserControl1.CoreWebView2 != null) BrowserControl1.CoreWebView2.IsMuted = false;
                    if (BrowserControl2.CoreWebView2 != null) BrowserControl2.CoreWebView2.IsMuted = true;
                }

                TxtActiveTabIndicator.Text = "[Tab 1: YouTube]";
                TxtActiveTabIndicator.Foreground = System.Windows.Media.Brushes.DodgerBlue;
                TxtUrl.Text = BrowserControl1.Source?.ToString() ?? "";
            }
            else
            {
                RbTab2.IsChecked = true;
                WrapperTab1.Visibility = Visibility.Collapsed;
                BrowserControl1.Visibility = Visibility.Collapsed;

                WrapperTab2.Visibility = Visibility.Visible;
                BrowserControl2.Visibility = Visibility.Visible;

                if (_isForceMuted)
                {
                    if (BrowserControl1.CoreWebView2 != null) BrowserControl1.CoreWebView2.IsMuted = true;
                    if (BrowserControl2.CoreWebView2 != null) BrowserControl2.CoreWebView2.IsMuted = true;
                }
                else
                {
                    if (BrowserControl1.CoreWebView2 != null) BrowserControl1.CoreWebView2.IsMuted = true;
                    if (BrowserControl2.CoreWebView2 != null) BrowserControl2.CoreWebView2.IsMuted = false;
                }

                TxtActiveTabIndicator.Text = "[Tab 2: TikTok]";
                TxtActiveTabIndicator.Foreground = System.Windows.Media.Brushes.HotPink;
                TxtUrl.Text = BrowserControl2.Source?.ToString() ?? "";
            }
        }

        private void RestoreTabContainerLayout()
        {
            TabContainer.RowDefinitions.Clear();
            TabContainer.ColumnDefinitions.Clear();
            Grid.SetRow(WrapperTab1, 0);
            Grid.SetColumn(WrapperTab1, 0);
            Grid.SetRow(WrapperTab2, 0);
            Grid.SetColumn(WrapperTab2, 0);
            WrapperTab1.BorderThickness = new Thickness(0);
            WrapperTab2.BorderThickness = new Thickness(0);
        }

        public void SetSplitMode(bool isSplit)
        {
            BtnSplitTab.IsChecked = isSplit;
            if (isSplit)
            {
                TabContainer.RowDefinitions.Clear();
                TabContainer.ColumnDefinitions.Clear();

                // Chia đôi theo chiều dọc: Cột 0 (Trái: YouTube) | Cột 1 (Phải: TikTok)
                TabContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                TabContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(WrapperTab1, 0);
                Grid.SetColumn(WrapperTab1, 0);

                Grid.SetRow(WrapperTab2, 0);
                Grid.SetColumn(WrapperTab2, 1);

                // Đường phân cách dọc thanh lịch giữa 2 bên
                WrapperTab1.BorderThickness = new Thickness(0, 0, 1, 0);
                WrapperTab1.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 55, 65, 81));
                WrapperTab1.Visibility = Visibility.Visible;
                BrowserControl1.Visibility = Visibility.Visible;

                WrapperTab2.BorderThickness = new Thickness(1, 0, 0, 0);
                WrapperTab2.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 55, 65, 81));
                WrapperTab2.Visibility = Visibility.Visible;
                BrowserControl2.Visibility = Visibility.Visible;

                if (_isForceMuted)
                {
                    if (BrowserControl1.CoreWebView2 != null) BrowserControl1.CoreWebView2.IsMuted = true;
                    if (BrowserControl2.CoreWebView2 != null) BrowserControl2.CoreWebView2.IsMuted = true;
                }
                else
                {
                    if (BrowserControl1.CoreWebView2 != null) BrowserControl1.CoreWebView2.IsMuted = false;
                    if (BrowserControl2.CoreWebView2 != null) BrowserControl2.CoreWebView2.IsMuted = false;
                }

                TxtActiveTabIndicator.Text = "[🪟 Chia Dọc: YouTube | TikTok]";
                TxtActiveTabIndicator.Foreground = System.Windows.Media.Brushes.MediumPurple;
            }
            else
            {
                SelectTab(_activeTab);
            }
        }

        private void RbTab1_Click(object sender, RoutedEventArgs e)
        {
            SelectTab(1);
        }

        private void RbTab2_Click(object sender, RoutedEventArgs e)
        {
            SelectTab(2);
        }

        private void BtnSplitTab_Click(object sender, RoutedEventArgs e)
        {
            SetSplitMode(BtnSplitTab.IsChecked == true);
        }

        private void BtnQuickTikTok_Click(object sender, RoutedEventArgs e)
        {
            ActiveBrowser.CoreWebView2?.Navigate("https://www.tiktok.com");
        }

        public void Navigate(string url)
        {
            if (!_isInitialized || ActiveBrowser.CoreWebView2 == null) return;
            if (string.IsNullOrWhiteSpace(url)) return;

            string targetUrl = url.Trim();
            if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (targetUrl.Contains(".") && !targetUrl.Contains(" "))
                {
                    targetUrl = "https://" + targetUrl;
                }
                else
                {
                    targetUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(targetUrl)}";
                }
            }

            try
            {
                ActiveBrowser.CoreWebView2.Navigate(targetUrl);
                TxtUrl.Text = targetUrl;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Lỗi: {ex.Message}";
            }
        }

        public void UpdateDefaultUrls(string urlTab1, string urlTab2)
        {
            if (!_isInitialized) return;
            try
            {
                if (!string.IsNullOrWhiteSpace(urlTab1) && BrowserControl1.CoreWebView2 != null)
                {
                    BrowserControl1.CoreWebView2.Navigate(urlTab1);
                }
                if (!string.IsNullOrWhiteSpace(urlTab2) && BrowserControl2.CoreWebView2 != null)
                {
                    BrowserControl2.CoreWebView2.Navigate(urlTab2);
                }
            }
            catch { }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitialized && ActiveBrowser.CanGoBack)
            {
                ActiveBrowser.GoBack();
            }
        }

        private void BtnForward_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitialized && ActiveBrowser.CanGoForward)
            {
                ActiveBrowser.GoForward();
            }
        }

        public void Reload()
        {
            if (_isInitialized && ActiveBrowser.CoreWebView2 != null)
            {
                ActiveBrowser.Reload();
            }
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            Navigate("https://accounts.google.com");
        }

        private void BtnGo_Click(object sender, RoutedEventArgs e)
        {
            Navigate(TxtUrl.Text);
        }

        private void TxtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Navigate(TxtUrl.Text);
            }
        }

        private void TxtUrl_GotFocus(object sender, RoutedEventArgs e)
        {
            TxtUrl.SelectAll();
        }

        private void BtnGoogleLogin_Click(object sender, RoutedEventArgs e)
        {
            Navigate("https://accounts.google.com");
        }

        private void BtnGmail_Click(object sender, RoutedEventArgs e)
        {
            Navigate("https://mail.google.com");
        }

        private void BtnYoutube_Click(object sender, RoutedEventArgs e)
        {
            Navigate("https://www.youtube.com");
        }

        private void MenuGoogleDrive_Click(object sender, RoutedEventArgs e)
        {
            Navigate("https://drive.google.com");
        }

        private void MenuGoogleDocs_Click(object sender, RoutedEventArgs e)
        {
            Navigate("https://docs.google.com");
        }

        // ================= TỰ ĐỘNG HÓA YOUTUBE SHORTS (CHẠY TRÊN TAB 1 - YOUTUBE) =================

        public async Task<bool> EnsureShortsPlayerOpenAsync()
        {
            if (!_isInitialized || BrowserControl1.CoreWebView2 == null) return false;

            string script = @"
            (function() {
                if (window.location.pathname.includes('/shorts/')) return 'IN_PLAYER';

                let shortLink = document.querySelector('ytd-reel-item-renderer a, ytd-rich-item-renderer a[href*=""/shorts/""], a[href*=""/shorts/""]');
                if (shortLink) {
                    shortLink.click();
                    return 'CLICKED';
                }
                
                if (!window.location.pathname.includes('/shorts')) {
                    let base = window.location.href.split('?')[0].replace(/\/videos.*|\/featured.*|\/community.*$/, '');
                    window.location.href = base.replace(/\/$/, '') + '/shorts';
                    return 'REDIRECTING';
                }
                return 'NOT_FOUND';
            })();";

            try
            {
                var res = await BrowserControl1.CoreWebView2.ExecuteScriptAsync(script);
                return res != null && !res.Contains("NOT_FOUND");
            }
            catch { return false; }
        }

        public async Task<string> CheckAndLikeShortAsync()
        {
            if (!_isInitialized || BrowserControl1.CoreWebView2 == null) return "NOT_INITIALIZED";

            // Selector không phụ thuộc ngôn ngữ: kiểm tra attribute aria-pressed trên nút like chuẩn
            string script = @"
            (function() {
                let activeReel = document.querySelector('ytd-reel-video-renderer[is-active]') || document;
                let likeBtn = activeReel.querySelector('#like-button button, like-button-view-model button, ytd-like-button-renderer button');

                if (likeBtn) {
                    let isPressed = likeBtn.getAttribute('aria-pressed');
                    if (isPressed === 'true') {
                        return 'ALREADY_LIKED';
                    } else {
                        likeBtn.click();
                        return 'LIKED_SUCCESS';
                    }
                }
                return 'NO_LIKE_BUTTON';
            })();";

            try
            {
                var rawResult = await BrowserControl1.CoreWebView2.ExecuteScriptAsync(script);
                string result = rawResult?.Trim('"') ?? "NO_RESULT";
                if (result == "LIKED_SUCCESS")
                {
                    TxtStatus.Text = "[Tab 1] ❤️ Đã thả tym video Short!";
                }
                else if (result == "ALREADY_LIKED")
                {
                    TxtStatus.Text = "[Tab 1] ✓ Video Short đã được tym trước đó.";
                }
                return result;
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public async Task<string> ScrollShortAsync(string direction)
        {
            if (!_isInitialized || BrowserControl1.CoreWebView2 == null) return "NOT_INITIALIZED";

            bool isUp = direction.ToLower().Contains("up") || direction.ToLower().Contains("lên");
            string script = $@"
            (function() {{
                let btn = document.querySelector(isUp ? '#navigation-button-up button' : '#navigation-button-down button');
                if (btn) {{
                    btn.click();
                    return 'BTN_CLICKED';
                }}
                let key = {(isUp ? "'ArrowUp'" : "'ArrowDown'")};
                let code = {(isUp ? "38" : "40")};
                document.dispatchEvent(new KeyboardEvent('keydown', {{ key: key, keyCode: code, code: key, bubbles: true }}));
                return 'KEY_SENT';
            }})();";

            try
            {
                var raw = await BrowserControl1.CoreWebView2.ExecuteScriptAsync(script);
                return raw?.Trim('"') ?? "OK";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public async Task AutoOpenShortsAndScrollAsync()
        {
            if (!_isInitialized || BrowserControl1.CoreWebView2 == null) return;

            string script = @"
            (async function() {
                window.__autoScrollActive = true;

                if (!window.location.pathname.endsWith('/shorts')) {
                    let tabs = Array.from(document.querySelectorAll('yt-tab-shape, tp-yt-paper-tab, a, div[role=""tab""]'));
                    let shortsTab = tabs.find(el => {
                        let t = (el.innerText || el.textContent || '').trim().toLowerCase();
                        let href = el.getAttribute('href') || '';
                        return t === 'shorts' || href.endsWith('/shorts') || href.includes('/shorts');
                    });

                    if (shortsTab) {
                        shortsTab.click();
                        await new Promise(r => setTimeout(r, 2500));
                    } else {
                        let base = window.location.href.split('?')[0].replace(/\/videos.*|\/featured.*|\/community.*|\/playlists.*|\/about.*$/, '');
                        if (!base.endsWith('/shorts')) {
                            window.location.href = base.replace(/\/$/, '') + '/shorts';
                            return 'NAVIGATING';
                        }
                    }
                }

                let lastHeight = 0;
                let unchangedCount = 0;
                let loops = 80;

                while (window.__autoScrollActive && loops > 0) {
                    loops--;
                    let currentHeight = document.documentElement.scrollHeight || document.body.scrollHeight;
                    window.scrollTo({
                        top: currentHeight,
                        behavior: 'smooth'
                    });

                    await new Promise(r => setTimeout(r, 1200));

                    let newHeight = document.documentElement.scrollHeight || document.body.scrollHeight;
                    if (newHeight === lastHeight) {
                        unchangedCount++;
                        if (unchangedCount >= 3) {
                            break;
                        }
                    } else {
                        unchangedCount = 0;
                        lastHeight = newHeight;
                    }
                }

                window.__autoScrollActive = false;
                return 'DONE';
            })();";

            try
            {
                TxtStatus.Text = "[Tab 1] Đang tự động mở Shorts và cuộn đến cuối...";
                await BrowserControl1.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Lỗi script: {ex.Message}";
            }
        }

        public async Task ScrollToBottomAsync()
        {
            if (!_isInitialized || ActiveBrowser.CoreWebView2 == null) return;

            string script = @"
            (async function() {
                window.__autoScrollActive = true;
                let lastHeight = 0;
                let unchangedCount = 0;
                let loops = 60;
                while (window.__autoScrollActive && loops > 0) {
                    loops--;
                    let h = document.documentElement.scrollHeight || document.body.scrollHeight;
                    window.scrollTo({ top: h, behavior: 'smooth' });
                    await new Promise(r => setTimeout(r, 1000));
                    let newH = document.documentElement.scrollHeight || document.body.scrollHeight;
                    if (newH === lastHeight) {
                        unchangedCount++;
                        if (unchangedCount >= 3) break;
                    } else {
                        unchangedCount = 0;
                        lastHeight = newH;
                    }
                }
                window.__autoScrollActive = false;
            })();";

            try
            {
                TxtStatus.Text = "Đang cuộn xuống đáy trang...";
                await ActiveBrowser.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Lỗi: {ex.Message}";
            }
        }

        public async Task NextShortAsync()
        {
            if (!_isInitialized || BrowserControl1.CoreWebView2 == null) return;
            await ScrollShortAsync("down");
        }

        public async Task StopAutoScrollAsync()
        {
            if (!_isInitialized) return;

            try
            {
                if (BrowserControl1.CoreWebView2 != null)
                    await BrowserControl1.CoreWebView2.ExecuteScriptAsync("window.__autoScrollActive = false;");
                if (BrowserControl2.CoreWebView2 != null)
                    await BrowserControl2.CoreWebView2.ExecuteScriptAsync("window.__autoScrollActive = false;");
                TxtStatus.Text = "Đã dừng tự động cuộn";
            }
            catch { }
        }

        // ================= TỰ ĐỘNG HÓA ĐA NỀN TẢNG (YOUTUBE, TIKTOK, FACEBOOK, INSTAGRAM) =================

        public async Task<VideoPlaybackState> GetVideoPlaybackStateAsync()
        {
            if (!_isInitialized || ActiveBrowser.CoreWebView2 == null)
                return new VideoPlaybackState { Found = false };

            string script = @"
            (function() {
                try {
                    let skipBtn = document.querySelector('.ytp-ad-skip-button, .ytp-ad-skip-button-modern, .ytp-skip-ad-button, .videoAdUiSkipButton');
                    if (skipBtn) skipBtn.click();
                } catch(e) {}

                let v = null;
                let activeReel = document.querySelector('ytd-reel-video-renderer[is-active]');
                if (activeReel) {
                    v = activeReel.querySelector('video');
                }
                if (!v) {
                    let allVideos = Array.from(document.querySelectorAll('video'));
                    v = allVideos.find(vid => vid.offsetWidth > 120 && vid.offsetHeight > 120) || allVideos[0];
                }

                if (!v) return JSON.stringify({ found: false, duration: 0, currentTime: 0, paused: true, ended: false });

                if (v.paused && !v.ended) {
                    try { v.play(); } catch(e) {}
                }

                return JSON.stringify({
                    found: true,
                    duration: (isNaN(v.duration) || v.duration === Infinity) ? 0 : v.duration,
                    currentTime: isNaN(v.currentTime) ? 0 : v.currentTime,
                    paused: v.paused,
                    ended: v.ended
                });
            })();";

            try
            {
                var raw = await ActiveBrowser.CoreWebView2.ExecuteScriptAsync(script);
                if (!string.IsNullOrEmpty(raw) && raw != "null")
                {
                    string json = raw;
                    if (json.StartsWith("\"") && json.EndsWith("\""))
                    {
                        json = System.Text.RegularExpressions.Regex.Unescape(json.Substring(1, json.Length - 2));
                    }
                    var state = System.Text.Json.JsonSerializer.Deserialize<VideoPlaybackState>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (state != null) return state;
                }
            }
            catch { }

            return new VideoPlaybackState { Found = false };
        }

        public async Task<string> LikeCurrentPlatformAsync(PlatformType platform)
        {
            if (!_isInitialized || ActiveBrowser.CoreWebView2 == null) return "NOT_INITIALIZED";

            string script = @"
            (function() {
                // 1. YouTube Shorts
                let activeReel = document.querySelector('ytd-reel-video-renderer[is-active]');
                if (activeReel) {
                    let btn = activeReel.querySelector('#like-button button, like-button-view-model button, ytd-like-button-renderer button');
                    if (btn) {
                        if (btn.getAttribute('aria-pressed') === 'true') return 'ALREADY_LIKED';
                        btn.click();
                        return 'LIKED_SUCCESS';
                    }
                }

                // 2. YouTube Long Video
                let ytLike = document.querySelector('#top-level-buttons-computed ytd-like-button-renderer button, like-button-view-model button, ytd-watch-metadata like-button-view-model button');
                if (ytLike) {
                    if (ytLike.getAttribute('aria-pressed') === 'true') return 'ALREADY_LIKED';
                    ytLike.click();
                    return 'LIKED_SUCCESS';
                }

                // 3. TikTok
                let tiktokLike = document.querySelector('[data-e2e=""like-icon""], span[data-e2e=""like-icon""], button[aria-label*=""Like""], button[aria-label*=""Thích""]');
                if (tiktokLike) {
                    tiktokLike.click();
                    return 'LIKED_SUCCESS';
                }

                // 4. Facebook Reels & Video
                let fbLike = document.querySelector('div[aria-label=""Thích""][role=""button""], div[aria-label=""Like""][role=""button""], div[aria-label=""Bày tỏ cảm xúc: Thích""]');
                if (fbLike) {
                    fbLike.click();
                    return 'LIKED_SUCCESS';
                }

                // 5. Instagram Reels
                let igLike = document.querySelector('svg[aria-label=""Like""], svg[aria-label=""Thích""]')?.closest('button') || document.querySelector('svg[aria-label=""Like""], svg[aria-label=""Thích""]');
                if (igLike) {
                    igLike.click();
                    return 'LIKED_SUCCESS';
                }

                return 'NO_LIKE_BUTTON';
            })();";

            try
            {
                var rawResult = await ActiveBrowser.CoreWebView2.ExecuteScriptAsync(script);
                string result = rawResult?.Trim('"') ?? "NO_RESULT";
                if (result == "LIKED_SUCCESS")
                {
                    TxtStatus.Text = $"[{TxtActiveTabIndicator.Text}] ❤️ Đã thả tym/thích video!";
                }
                return result;
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public async Task<string> ScrollPlatformAsync(PlatformType platform, string direction)
        {
            if (!_isInitialized || ActiveBrowser.CoreWebView2 == null) return "NOT_INITIALIZED";

            bool isUp = direction.ToLower().Contains("up") || direction.ToLower().Contains("lên");
            string script = $@"
            (function() {{
                let isUp = {(isUp ? "true" : "false")};
                
                // 1. YouTube Shorts Nav Buttons
                let navBtn = document.querySelector(isUp ? '#navigation-button-up button' : '#navigation-button-down button');
                if (navBtn) {{
                    navBtn.click();
                    return 'BTN_CLICKED';
                }}

                // 2. Dispatch phím mũi tên chuẩn
                let key = isUp ? 'ArrowUp' : 'ArrowDown';
                let code = isUp ? 38 : 40;
                let eventDown = new KeyboardEvent('keydown', {{ key: key, keyCode: code, which: code, code: key, bubbles: true }});
                let eventUp = new KeyboardEvent('keyup', {{ key: key, keyCode: code, which: code, code: key, bubbles: true }});
                document.dispatchEvent(eventDown);
                document.dispatchEvent(eventUp);

                // 3. Cuộn mượt fallback
                window.scrollBy({{ top: isUp ? -window.innerHeight * 0.8 : window.innerHeight * 0.8, behavior: 'smooth' }});
                return 'KEY_SENT';
            }})();";

            try
            {
                var raw = await ActiveBrowser.CoreWebView2.ExecuteScriptAsync(script);
                return raw?.Trim('"') ?? "OK";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public async Task<bool> AutoScrollToBottomAndClickOldestAsync(PlatformType platform)
        {
            if (!_isInitialized || ActiveBrowser.CoreWebView2 == null) return false;

            string script = @"
            (async function() {
                let lastHeight = 0;
                let unchanged = 0;
                let maxLoops = 40;

                while (maxLoops > 0) {
                    maxLoops--;
                    let h = document.documentElement.scrollHeight || document.body.scrollHeight;
                    window.scrollTo({ top: h, behavior: 'smooth' });
                    await new Promise(r => setTimeout(r, 900));

                    let newH = document.documentElement.scrollHeight || document.body.scrollHeight;
                    if (newH === lastHeight) {
                        unchanged++;
                        if (unchanged >= 3) break;
                    } else {
                        unchanged = 0;
                        lastHeight = newH;
                    }
                }

                await new Promise(r => setTimeout(r, 1000));

                let allItems = Array.from(document.querySelectorAll(
                    'ytd-reel-item-renderer a, ytd-rich-item-renderer a[href*=""/shorts/""], [data-e2e=""user-post-item""] a, a[href*=""/reel/""], a[href*=""/video""]'
                ));

                if (allItems.length > 0) {
                    let lastItem = allItems[allItems.length - 1];
                    lastItem.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    await new Promise(r => setTimeout(r, 600));
                    lastItem.click();
                    return 'CLICKED_OLDEST';
                }

                return 'NO_ITEMS';
            })();";

            try
            {
                TxtStatus.Text = "⏳ Đang cuộn xuống đáy trang để chọn video cũ nhất...";
                var raw = await ActiveBrowser.CoreWebView2.ExecuteScriptAsync(script);
                return raw != null && raw.Contains("CLICKED_OLDEST");
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EnsurePlayerStartedAsync(PlatformType platform)
        {
            if (!_isInitialized || ActiveBrowser.CoreWebView2 == null) return false;

            if (platform == PlatformType.YouTubeShorts)
            {
                return await EnsureShortsPlayerOpenAsync();
            }
            else if (platform == PlatformType.TikTok)
            {
                string script = @"
                (function() {
                    if (window.location.pathname.includes('/video/')) return 'IN_PLAYER';
                    let firstVideo = document.querySelector('[data-e2e=""user-post-item""] a, div[data-e2e=""recommend-list-item-container""] a');
                    if (firstVideo) {
                        firstVideo.click();
                        return 'CLICKED';
                    }
                    return 'NOT_FOUND';
                })();";
                try {
                    var raw = await ActiveBrowser.CoreWebView2.ExecuteScriptAsync(script);
                    return raw != null && !raw.Contains("NOT_FOUND");
                } catch { return false; }
            }
            else if (platform == PlatformType.FacebookReels)
            {
                string script = @"
                (function() {
                    if (window.location.pathname.includes('/reel/')) return 'IN_PLAYER';
                    let firstReel = document.querySelector('a[href*=""/reel/""]');
                    if (firstReel) { firstReel.click(); return 'CLICKED'; }
                    return 'NOT_FOUND';
                })();";
                try {
                    var raw = await ActiveBrowser.CoreWebView2.ExecuteScriptAsync(script);
                    return raw != null && !raw.Contains("NOT_FOUND");
                } catch { return false; }
            }
            else if (platform == PlatformType.InstagramReels)
            {
                string script = @"
                (function() {
                    if (window.location.pathname.includes('/reel/') || window.location.pathname.includes('/reels/')) return 'IN_PLAYER';
                    let firstReel = document.querySelector('a[href*=""/reel/""]');
                    if (firstReel) { firstReel.click(); return 'CLICKED'; }
                    return 'NOT_FOUND';
                })();";
                try {
                    var raw = await ActiveBrowser.CoreWebView2.ExecuteScriptAsync(script);
                    return raw != null && !raw.Contains("NOT_FOUND");
                } catch { return false; }
            }

            return true;
        }

        public async Task<string> AutoInteractPlatformAsync(PlatformType platform)
        {
            if (!_isInitialized || ActiveBrowser.CoreWebView2 == null) return "NOT_INITIALIZED";

            string script = @"
            (function() {
                let shareBtn = document.querySelector('#share-button button, [data-e2e=""share-icon""], div[aria-label=""Chia sẻ""]');
                if (shareBtn) {
                    shareBtn.focus();
                    return 'HOVER_SHARE';
                }
                return 'NO_INTERACTION';
            })();";

            try {
                var raw = await ActiveBrowser.CoreWebView2.ExecuteScriptAsync(script);
                return raw?.Trim('"') ?? "OK";
            } catch (Exception ex) {
                return $"ERROR: {ex.Message}";
            }
        }

        public void Dispose()
        {
            try
            {
                BrowserControl1?.Dispose();
                BrowserControl2?.Dispose();
                _env = null;
                _initTask = null;
            }
            catch { }
        }

        private async void MenuClearData_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || ActiveBrowser.CoreWebView2 == null) return;

            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa toàn bộ Cookie, Cache và đăng xuất khỏi Profile {_profileId} không?",
                "Xác nhận xóa dữ liệu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await ActiveBrowser.CoreWebView2.Profile.ClearBrowsingDataAsync();
                    MessageBox.Show($"Đã xóa toàn bộ cookie & dữ liệu của Profile {_profileId}.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    Navigate("https://accounts.google.com");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuSetProxy_Click(object sender, RoutedEventArgs e)
        {
            string proxyFilePath = Path.Combine(_profilePath, "proxy.txt");
            if (!File.Exists(proxyFilePath))
            {
                File.WriteAllText(proxyFilePath, "# Nhap proxy cho Profile nay theo dinh dang:\n# ip:port\n# hoac\n# ip:port:user:pass\n");
            }
            Process.Start(new ProcessStartInfo("notepad.exe", proxyFilePath) { UseShellExecute = true });
            MessageBox.Show("Đã mở file cấu hình proxy.txt.\nSau khi lưu proxy, hãy khởi động lại ứng dụng để áp dụng proxy cho ô này.", "Cấu hình Proxy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_profilePath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", _profilePath) { UseShellExecute = true });
            }
        }
    }
}
