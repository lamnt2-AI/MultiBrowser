using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace MultiBrowser
{
    public class MultiBrowserApiServer : IDisposable
    {
        private readonly MainWindow _mainWindow;
        private HttpListener? _listener;
        private bool _isRunning = false;
        private int _port;
        private static string _apiToken = string.Empty;

        public int Port => _port;
        public bool IsRunning => _isRunning;

        public MultiBrowserApiServer(MainWindow mainWindow, int port = 5055)
        {
            _mainWindow = mainWindow;
            _port = port;
            GetOrCreateApiToken();
        }

        public static string GetOrCreateApiToken()
        {
            if (!string.IsNullOrEmpty(_apiToken)) return _apiToken;

            string tokenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "api_token.txt");
            try
            {
                if (File.Exists(tokenPath))
                {
                    _apiToken = File.ReadAllText(tokenPath).Trim();
                    if (!string.IsNullOrEmpty(_apiToken)) return _apiToken;
                }
            }
            catch { }

            _apiToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(tokenPath, _apiToken);
            }
            catch { }

            return _apiToken;
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                _isRunning = true;
                WritePortFile(_port);

                Task.Run(ListenLoopAsync);
            }
            catch
            {
                // Nếu cổng 5055 bị trùng, thử fallback sang 5056
                try
                {
                    _port = 5056;
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                    _listener.Prefixes.Add($"http://localhost:{_port}/");
                    _listener.Start();
                    _isRunning = true;
                    WritePortFile(_port);
                    Task.Run(ListenLoopAsync);
                }
                catch
                {
                    _isRunning = false;
                }
            }
        }

        private static void WritePortFile(int port)
        {
            try
            {
                string portFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "port.txt");
                File.WriteAllText(portFilePath, port.ToString());
            }
            catch { }
        }

        private async Task ListenLoopAsync()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            // 1. Bảo vệ CSRF: Chặn các website bên ngoài gọi localhost, chỉ cho phép localhost
            string origin = req.Headers["Origin"] ?? "";
            if (!string.IsNullOrEmpty(origin))
            {
                if (!origin.Contains("localhost") && !origin.Contains("127.0.0.1"))
                {
                    res.StatusCode = 403;
                    res.Close();
                    return;
                }
                res.AddHeader("Access-Control-Allow-Origin", origin);
            }

            res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Token");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                res.Close();
                return;
            }

            string path = req.Url?.AbsolutePath.ToLowerInvariant().TrimEnd('/') ?? "";
            string method = req.HttpMethod.ToUpperInvariant();

            // 2. Xác thực Token bảo mật (Bearer Token từ api_token.txt)
            string authHeader = req.Headers["Authorization"] ?? req.Headers["X-Api-Token"] ?? "";
            string expectedToken = GetOrCreateApiToken();

            bool isAuthenticated = false;
            if (!string.IsNullOrEmpty(authHeader))
            {
                string token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authHeader.Substring(7).Trim()
                    : authHeader.Trim();
                isAuthenticated = (token == expectedToken);
            }

            // Cho phép xem capabilities công khai để bot khám phá, các thao tác điều khiển bắt buộc có token
            if (path != "/api/capabilities" && path != "/api/help" && !isAuthenticated)
            {
                res.StatusCode = 401;
                byte[] unauthorizedBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Yêu cầu chưa được xác thực. Vui lòng truyền Header 'Authorization: Bearer <token>' lấy từ file api_token.txt."
                }));
                res.ContentType = "application/json; charset=utf-8";
                res.ContentLength64 = unauthorizedBytes.Length;
                await res.OutputStream.WriteAsync(unauthorizedBytes, 0, unauthorizedBytes.Length);
                res.Close();
                return;
            }

            try
            {
                string requestBody = "";
                if (req.HasEntityBody)
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    requestBody = await reader.ReadToEndAsync();
                }

                object responseData;

                if (path == "/api/capabilities" || path == "/api/help")
                {
                    responseData = GetCapabilitiesInfo();
                }
                else if (path == "/api/status" && method == "GET")
                {
                    responseData = await _mainWindow.Dispatcher.InvokeAsync(() => _mainWindow.GetServerStatusSnapshot());
                }
                else if (path == "/api/set_window_count" && method == "POST")
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(requestBody);
                    if (data != null && data.TryGetValue("count", out var countEl) && countEl.TryGetInt32(out int count))
                    {
                        if (count == 1 || count == 2 || count == 4 || count == 6 || count == 8)
                        {
                            var switchTask = _mainWindow.Dispatcher.Invoke(() => _mainWindow.SwitchWindowCountAsync(count));
                            await switchTask;
                            responseData = new { success = true, message = $"Đã chuyển số lượng cửa sổ thành {count}." };
                        }
                        else
                        {
                            res.StatusCode = 400;
                            responseData = new { success = false, error = "Tham số 'count' phải là 1, 2, 4, 6 hoặc 8." };
                        }
                    }
                    else
                    {
                        res.StatusCode = 400;
                        responseData = new { success = false, error = "Tham số 'count' không hợp lệ." };
                    }
                }
                else if (path == "/api/navigate" && method == "POST")
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(requestBody);
                    string targetUrl = data != null && data.TryGetValue("url", out var urlEl) ? urlEl.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(targetUrl))
                    {
                        await _mainWindow.Dispatcher.InvokeAsync(() =>
                        {
                            _mainWindow.NavigateAllFromApi(targetUrl);
                        });
                        responseData = new { success = true, message = $"Đã mở URL '{targetUrl}' trên các cửa sổ." };
                    }
                    else
                    {
                        res.StatusCode = 400;
                        responseData = new { success = false, error = "Thiếu tham số 'url'." };
                    }
                }
                else if (path == "/api/layout" && method == "POST")
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(requestBody);
                    string action = data != null && data.TryGetValue("action", out var actionEl) ? actionEl.GetString() ?? "" : "";

                    bool handled = await _mainWindow.Dispatcher.InvokeAsync(() => _mainWindow.ExecuteLayoutActionFromApi(action));
                    if (handled)
                    {
                        responseData = new { success = true, message = $"Đã thực hiện thao tác bố cục: {action}." };
                    }
                    else
                    {
                        res.StatusCode = 400;
                        responseData = new { success = false, error = $"Hành động bố cục '{action}' không hợp lệ. Hỗ trợ: all_tab1, all_tab2, split_all, reload_all." };
                    }
                }
                else if (path == "/api/mute" && method == "POST")
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(requestBody);
                    bool mute = data != null && data.TryGetValue("mute", out var muteEl) && muteEl.GetBoolean();

                    await _mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _mainWindow.SetMuteFromApi(mute);
                    });
                    responseData = new { success = true, message = mute ? "Đã tắt âm thanh toàn bộ ứng dụng." : "Đã bật lại âm thanh." };
                }
                else if (path == "/api/playlist/start" && method == "POST")
                {
                    // Tự động nhận diện, sắp xếp và kích hoạt chạy trực tiếp trên app
                    var playlistPayload = JsonSerializer.Deserialize<PlaylistApiRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (playlistPayload != null && playlistPayload.Links != null && playlistPayload.Links.Any())
                    {
                        // Sửa lỗi nested-task: invoke synchronous method setup trên UI thread rồi await task kết quả
                        var taskOp = _mainWindow.Dispatcher.Invoke(() => _mainWindow.AutoArrangeAndRunPlaylistFromApiAsync(playlistPayload));
                        var createdItems = await taskOp;

                        responseData = new
                        {
                            success = true,
                            message = $"Đã tự động phân tích và bắt đầu chạy kịch bản gồm {createdItems.Count} mục tiêu trên app!",
                            items = createdItems
                        };
                    }
                    else
                    {
                        res.StatusCode = 400;
                        responseData = new { success = false, error = "Cần truyền danh sách 'links' dạng mảng URL." };
                    }
                }
                else if (path == "/api/playlist/stop" && method == "POST")
                {
                    await _mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _mainWindow.StopAutomationFromApi();
                    });
                    responseData = new { success = true, message = "Đã dừng tự động hóa trên app." };
                }
                else
                {
                    res.StatusCode = 404;
                    responseData = new { success = false, error = "Không tìm thấy endpoint. Gọi /api/capabilities để xem danh sách lệnh." };
                }

                byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(responseData, new JsonSerializerOptions { WriteIndented = true }));
                res.ContentType = "application/json; charset=utf-8";
                res.ContentLength64 = buffer.Length;
                await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                res.StatusCode = 500;
                byte[] errBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                res.ContentType = "application/json; charset=utf-8";
                res.ContentLength64 = errBuffer.Length;
                await res.OutputStream.WriteAsync(errBuffer, 0, errBuffer.Length);
            }
            finally
            {
                try { res.Close(); } catch { }
            }
        }

        private object GetCapabilitiesInfo()
        {
            return new
            {
                appName = "MultiBrowser Automation Engine",
                version = "2.0.0",
                port = _port,
                tokenFile = "api_token.txt",
                description = "Giao thức điều khiển MultiBrowser trực tiếp cho AI Agent / MCP Server. Cho phép bot quan sát, chuyển đổi 1/2/4/6/8 cửa sổ, điều hướng, tự động sắp xếp kịch bản và chạy trực tiếp trên giao diện người dùng.",
                capabilities = new[]
                {
                    new
                    {
                        command = "get_capabilities",
                        endpoint = "GET /api/capabilities",
                        description = "Trả về toàn bộ danh mục tính năng, các lệnh điều khiển và hướng dẫn sử dụng cho Agent."
                    },
                    new
                    {
                        command = "get_status",
                        endpoint = "GET /api/status",
                        description = "Lấy trạng thái thời gian thực của app: số cửa sổ (1, 2, 4, 6, 8), danh sách URL các tab, trạng thái automation, âm thanh..."
                    },
                    new
                    {
                        command = "set_window_count",
                        endpoint = "POST /api/set_window_count",
                        description = "Thiết lập số lượng cửa sổ hiển thị (1, 2, 4, 6, hoặc 8 cửa sổ)",
                        parameters = new { count = "1 | 2 | 4 | 6 | 8" }
                    },
                    new
                    {
                        command = "navigate_all",
                        endpoint = "POST /api/navigate",
                        description = "Điều hướng toàn bộ các cửa sổ active tới 1 URL chỉ định",
                        parameters = new { url = "https://..." }
                    },
                    new
                    {
                        command = "layout_action",
                        endpoint = "POST /api/layout",
                        description = "Thực hiện thao tác điều khiển giao diện",
                        parameters = new { action = "all_tab1 (YouTube) | all_tab2 (TikTok) | split_all (Chia đôi dọc) | reload_all" }
                    },
                    new
                    {
                        command = "set_mute",
                        endpoint = "POST /api/mute",
                        description = "Bật hoặc tắt âm thanh toàn bộ ứng dụng (Global Mute)",
                        parameters = new { mute = "true | false" }
                    },
                    new
                    {
                        command = "auto_arrange_and_run_playlist",
                        endpoint = "POST /api/playlist/start",
                        description = "Nhận danh sách link từ Agent (Shorts, TikTok, YouTube dài, Facebook Reels...), tự động phân tích loại nền tảng, thiết lập chiến lược cuộn (đáy lên hoặc trên xuống), thời lượng xem và bắt đầu chạy trực tiếp trên app ngay lập tức.",
                        parameters = new
                        {
                            links = new[] { "https://www.youtube.com/@channel/shorts", "https://www.tiktok.com/@user", "https://www.youtube.com/watch?v=..." },
                            videoCountPerLink = "Số video xem mỗi link (mặc định: 3)",
                            strategy = "TopToBottom | BottomToTop (hoặc để trống để bot tự chọn tối ưu)",
                            durationMode = "FullVideoDuration (xem hết video) | FixedSeconds | RandomRange",
                            loopContinuous = "true (lặp lại liên tục) | false",
                            autoLike = "true | false"
                        }
                    },
                    new
                    {
                        command = "stop_automation",
                        endpoint = "POST /api/playlist/stop",
                        description = "Dừng ngay lập tức toàn bộ quá trình tự động hóa đang thực thi trên app."
                    }
                }
            };
        }

        public void Dispose()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
                _listener = null;
            }
            catch { }
        }
    }

    public class PlaylistApiRequest
    {
        public List<string>? Links { get; set; }
        public int VideoCountPerLink { get; set; } = 3;
        public string? Strategy { get; set; }
        public string? DurationMode { get; set; } = "FullVideoDuration";
        public int FixedSeconds { get; set; } = 15;
        public bool LoopContinuous { get; set; } = true;
        public bool AutoLike { get; set; } = true;
        public int DelayBetweenLinksSec { get; set; } = 3;
    }
}
