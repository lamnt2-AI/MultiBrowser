# Multi-Account Grid Browser (Trình duyệt đa tài khoản Google, YouTube & TikTok Multi-Tab)

Ứng dụng Windows C# WPF sử dụng **Microsoft Edge WebView2**, kế thừa và hoàn thiện từ các giải pháp kiến trúc của các repository mã nguồn mở hàng đầu trên GitHub (`light-browser`, `splitbrowser`, `Prism-Browser`). Cho phép chia màn hình thành 1 đến 4 ô trình duyệt độc lập hoàn toàn, hỗ trợ đăng nhập nhiều tài khoản Google cùng lúc mà không bị trùng session hay bị Google chặn, tích hợp hệ thống **Dual-Tab (Mỗi ô 2 Tab YouTube + TikTok)**, thanh công cụ tự động hóa lướt/thả tym YouTube Shorts và **tính năng Mute toàn bộ âm thanh ứng dụng**.

---

## 🚀 Cách chạy ứng dụng

1. **Cách nhanh nhất:** Nhấp đúp vào [MultiBrowser.lnk](MultiBrowser.lnk) hoặc [Chay-App.bat](Chay-App.bat) ở thư mục gốc.
2. **Hoặc chạy file .exe trực tiếp tại:**
   ```
   MultiBrowser\bin\Release\net10.0-windows\publish\MultiBrowser.exe
   ```

---

## 🔇 Tính năng Mute hết toàn bộ App (Global Mute)

* Nút bật/tắt âm thanh **`🔊 Âm thanh: BẬT` / `🔇 ĐÃ TẮT HẾT ÂM`** được đặt ở góc trên bên phải thanh công cụ.
* Khi nhấn nút này:
  * Toàn bộ 8 tab trình duyệt (4 ô x 2 tab) sẽ lập tức bị **tắt âm thanh hoàn toàn** (`IsMuted = true`).
  * Nút chuyển sang màu đỏ cảnh báo `🔇 ĐÃ TẮT HẾT ÂM`.
  * Kể cả khi bạn chuyển qua lại giữa các tab hoặc bật chế độ chia đôi, âm thanh vẫn được giữ ở trạng thái câm tuyệt đối.
* Khi bấm lại lần nữa, âm thanh của tab đang xem sẽ được khôi phục bình thường.

---

## 🪟 Tính năng Dual-Tab (Mỗi ô 2 Tab: YouTube + TikTok)

Trong mỗi ô (mỗi tài khoản) hiện tại được trang bị thanh quản lý Tab riêng biệt:

1. **🔴 Tab 1 (YouTube):** Mặc định mở kênh YouTube của bạn (`@lam-kid-studio/shorts`) với tài khoản Google tương ứng.
2. **🎵 Tab 2 (TikTok):** Mặc định mở trang `tiktok.com` để chạy song song tài khoản TikTok.
3. **🪟 Nút "Chia đôi 2 Tab":**
   * Cho phép chia đôi không gian trong ô đó thành 2 nửa để bạn nhìn thấy và thao tác cả YouTube lẫn TikTok cùng một lúc!
4. **Nút điều khiển hàng loạt trên thanh Global (Góc trên bên phải):**
   * **🔴 Tất cả Tab 1:** Cả 4 ô đồng loạt chuyển sang xem Tab YouTube.
   * **🎵 Tất cả Tab 2:** Cả 4 ô đồng loạt chuyển sang xem Tab TikTok.
   * **🪟 Chia đôi 2 Tab:** Bật chế độ chia đôi song song cả YouTube và TikTok trên cả 4 ô (hiển thị cùng lúc 8 khung trình duyệt).

---

## ⚡ Tính năng Tự động hóa YouTube Shorts (Automation Toolbar)

1. **❤️ Tự động kiểm tra & Thả Tym (Auto-Like):**
   * Quét nút Like của video Short trên Tab 1. Nếu chưa có tym -> tự động thả tym. Nếu đã có -> bỏ qua.
2. **⏱️ Cài đặt thời gian xem (Delay Seconds):**
   * Cho phép tùy chỉnh số giây dừng lại xem video trước khi lướt (mặc định là `8` giây).
3. **🔄 Hướng lướt (Direction):**
   * Lướt xuống (Next) hoặc lướt lên (Prev).
4. **▶ BẮT ĐẦU AUTO:**
   * Tự động mở Shorts, thả tym và lướt liên tục trên cả 4 tài khoản cùng lúc.

---

## 🛡️ Các giải pháp kỹ thuật đã tích hợp

1. **Cô lập phiên đăng nhập 100% (Isolated Profiles):**
   * Thư mục dữ liệu riêng (`Profiles/Profile_1`, `Profile_2`, `Profile_3`, `Profile_4`).
2. **Chống phát hiện Bot (Anti-Detection):**
   * `--disable-blink-features=AutomationControlled`
   * `--force-webrtc-ip-handling-policy=disable_non_proxied_udp` (chống rò rỉ IP thật qua WebRTC khi dùng proxy)
   * Tự động xóa `webdriver` trên Prototype và ẩn `window.chrome.webview`.
3. **Hỗ trợ Proxy riêng cho từng Profile (HTTP / SOCKS5):**
   * Cấu hình trong file `proxy.txt` (`ip:port` hoặc `ip:port:user:pass`).
4. **Tự phục hồi khi Renderer Crash:**
   * Bắt sự kiện `ProcessFailed` để tự động reload tab bị lỗi.
