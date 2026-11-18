# Multi-Account Grid Browser (Trình duyệt đa tài khoản Google, YouTube, TikTok, Facebook & Instagram)

Ứng dụng Windows C# WPF sử dụng **Microsoft Edge WebView2**, kế thừa và hoàn thiện từ các giải pháp kiến trúc của các repository mã nguồn mở hàng đầu trên GitHub (`light-browser`, `splitbrowser`, `Prism-Browser`). Cho phép chia màn hình linh hoạt từ **1, 2, 4, 6 đến 8 ô trình duyệt độc lập hoàn toàn**, hỗ trợ đăng nhập nhiều tài khoản Google cùng lúc mà không bị trùng session hay bị Google chặn, tích hợp hệ thống **Dual-Tab (Mỗi ô 2 Tab YouTube + TikTok)**, công cụ **Kịch Bản Xem Lần Lượt Đa Nền Tảng (YouTube Shorts, YouTube Video Dài, TikTok, Facebook Reels/Video, Instagram Reels)**, thanh công cụ tự động hóa lướt/thả tym và **tính năng Mute toàn bộ âm thanh ứng dụng**.

---

## 🚀 Cách chạy ứng dụng

1. **Cách nhanh nhất:** Nhấp đúp vào [MultiBrowser.lnk](MultiBrowser.lnk) hoặc [Chay-App.bat](Chay-App.bat) ở thư mục gốc.
2. **Hoặc chạy file .exe trực tiếp tại:**
   ```
   MultiBrowser\bin\Release\net10.0-windows\publish\MultiBrowser.exe
   ```

---

## 🎬 Tính năng Kịch Bản Xem Lần Lượt Đa Nền Tảng (Sequence Automation)

Nhấn nút **`🎬 Xem Lần Lượt`** trên thanh công cụ góc trên để mở bảng quản lý kịch bản:

### 1. Tự động nhận diện nền tảng & loại video thông minh
* Chỉ cần dán đường link (URL), hệ thống sẽ tự động nhận diện và gán cơ chế xử lý tương ứng:
  * **🔴 YouTube Shorts:** Tự động mở player Shorts, bấm nút điều hướng hoặc dispatch phím, thả tym chuẩn `aria-pressed`.
  * **📺 YouTube Video Dài (Master):** Tự động phát, bỏ qua nút quảng cáo (Skip Ad) nếu có, theo dõi xem hết video (`duration`).
  * **🎵 TikTok Video / Kênh:** Tự động mở player TikTok, dispatch phím lướt mượt mà, thả tym icon.
  * **📘 Facebook Reels / Video:** Tự động phát video Reels trên Facebook, cuộn và bấm Thích/Like.
  * **📷 Instagram Reels:** Tự động phát Reels Instagram và tương tác thả tym.

### 2. Chiến lược cuộn video linh hoạt
* **⬇️ Lướt từ trên xuống (Mới nhất ➔ Cũ):** Bắt đầu xem từ video đầu tiên trên trang và lướt xuống dần.
* **⬆️ Cuộn xuống đáy rồi lướt lên (Cũ nhất ➔ Mới):** Tự động cuộn load toàn bộ danh sách trang xuống tận đáy, mở video cũ nhất và lướt ngược lên trên.

### 3. Kiểm soát thời lượng xem thông minh (Smart Video Duration)
* **⏱️ Xem hết video (Full Duration):** Quét trực tiếp thẻ HTML5 `<video>` để lấy độ dài thực tế (`v.duration`) và tự động chờ đến khi xem hết toàn bộ video mới next.
* **⌛ Xem cố định X giây:** Đặt số giây cố định cần dừng xem mỗi video.
* **🎲 Xem ngẫu nhiên (Min - Max giây):** Xem trong khoảng ngẫu nhiên mô phỏng thói quen người dùng thật.

### 4. Chạy tuần tự và xoay vòng liên tục
* Thiết lập số video cần xem ở mỗi link (ví dụ: xem 5 video Shorts kênh A ➔ chuyển sang lướt 3 video TikTok ➔ chuyển sang xem 1 video dài YouTube kênh B).
* Hỗ trợ chế độ **🔁 Lặp lại kịch bản liên tục** qua tất cả các liên kết trong danh sách.
* Đồng bộ thực thi đồng loạt trên **toàn bộ 2, 4, 6 hoặc 8 cửa sổ** đang hiển thị.

---

## ⚙️ Cài đặt số lượng cửa sổ (2, 4, 6, 8 Cửa Sổ) & Thông số mặc định

* Nhấn nút **`⚙️ Cài đặt`** trên thanh công cụ góc trên bên phải:
  * **Chọn số lượng cửa sổ:**
    * **2 Cửa Sổ:** Bố cục 1 hàng x 2 cột (1x2).
    * **4 Cửa Sổ:** Bố cục 2 hàng x 2 cột (2x2) [Mặc định].
    * **6 Cửa Sổ:** Bố cục 2 hàng x 3 cột (2x3).
    * **8 Cửa Sổ:** Bố cục 2 hàng x 4 cột (2x4 - Tối đa 16 tab song song).
  * **Tùy chỉnh URL mục tiêu:** Cài đặt kênh YouTube Shorts và trang web TikTok mặc định khi khởi động.
  * **Thông số Auto Shorts:** Tùy chỉnh số giây xem video mặc định và bật/tắt tự động thả tym.
* Cấu hình được tự động lưu vào file **`settings.json`** và áp dụng ngay lập tức mà không cần khởi động lại ứng dụng.
* Chuyển đổi nhanh 1-click trực tiếp trên thanh công cụ: `[2 Ô (1x2)]` `[4 Ô (2x2)]` `[6 Ô (2x3)]` `[8 Ô (2x4)]` `[1 Ô]`.

---

## 🔇 Tính năng Mute hết toàn bộ App (Global Mute)

* Nút bật/tắt âm thanh **`🔊 Âm thanh: BẬT` / `🔇 ĐÃ TẮT HẾT ÂM`** được đặt ở góc trên bên phải thanh công cụ.
* Khi nhấn nút này:
  * Toàn bộ các tab trình duyệt đang chạy (lên đến 16 tab: 8 ô x 2 tab) sẽ lập tức bị **tắt âm thanh hoàn toàn** (`IsMuted = true`).
  * Nút chuyển sang màu đỏ cảnh báo `🔇 ĐÃ TẮT HẾT ÂM`.
  * Kể cả khi bạn chuyển qua lại giữa các tab hoặc bật chế độ chia đôi, âm thanh vẫn được giữ ở trạng thái câm tuyệt đối.
* Khi bấm lại lần nữa, âm thanh của tab đang xem sẽ được khôi phục bình thường.

---

## 🪟 Tính năng Dual-Tab (Mỗi ô 2 Tab: YouTube + TikTok)

Trong mỗi ô (mỗi tài khoản) hiện tại được trang bị thanh quản lý Tab riêng biệt:

1. **🔴 Tab 1 (YouTube):** Mặc định mở kênh YouTube của bạn (`@lam-kid-studio/shorts`) với tài khoản Google tương ứng.
2. **🎵 Tab 2 (TikTok):** Mặc định mở trang `tiktok.com` để chạy song song tài khoản TikTok.
3. **🪟 Nút "Chia đôi 2 Tab":** Cho phép chia đôi không gian trong ô đó thành 2 nửa để nhìn thấy và thao tác cả YouTube lẫn TikTok cùng một lúc.
4. **Nút điều khiển hàng loạt trên thanh Global (Góc trên bên phải):**
   * **🔴 Tất cả Tab 1:** Tất cả các ô đồng loạt chuyển sang xem Tab YouTube.
   * **🎵 Tất cả Tab 2:** Tất cả các ô đồng loạt chuyển sang xem Tab TikTok.
   * **🪟 Chia đôi 2 Tab:** Bật chế độ chia đôi song song cả YouTube và TikTok trên tất cả các ô.

---

## 🛡️ Các giải pháp kỹ thuật đã tích hợp

1. **Cô lập phiên đăng nhập 100% (Isolated Profiles):**
   * Thư mục dữ liệu riêng (`Profiles/Profile_1` đến `Profiles/Profile_8`).
2. **Chống phát hiện Bot (Anti-Detection):**
   * `--disable-blink-features=AutomationControlled`
   * `--force-webrtc-ip-handling-policy=disable_non_proxied_udp` (chống rò rỉ IP thật qua WebRTC khi dùng proxy)
   * Tự động xóa `webdriver` trên Prototype và ẩn `window.chrome.webview`.
3. **Hỗ trợ Proxy riêng cho từng Profile (HTTP / SOCKS5):**
   * Cấu hình trong file `proxy.txt` (`ip:port` hoặc `ip:port:user:pass`) trong từng thư mục Profile.
4. **Tự phục hồi khi Renderer Crash:**
   * Bắt sự kiện `ProcessFailed` để tự động reload tab bị lỗi.
