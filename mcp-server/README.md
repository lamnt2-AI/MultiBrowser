# MultiBrowser MCP Server

Giao thức **Model Context Protocol (MCP)** tiêu chuẩn cho phép các AI Agent (như Antigravity, Claude Code, Codex...) kết nối và điều khiển trực tiếp ứng dụng **MultiBrowser** đang chạy trên máy tính của bạn.

---

## 🛠️ Các công cụ (Tools) mà MCP Server cung cấp

1. **`multibrowser_get_capabilities`**:
   * Hiển thị toàn bộ danh mục chức năng, trạng thái kết nối và danh sách lệnh mà bot có thể thao tác.
2. **`multibrowser_get_status`**:
   * Lấy trạng thái thời gian thực của app: số cửa sổ hiển thị, URL các tab, tiến trình automation, âm thanh...
3. **`multibrowser_set_window_count`**:
   * Chuyển đổi số lượng cửa sổ (2, 4, 6 hoặc 8 cửa sổ) ngay lập tức trên app.
4. **`multibrowser_auto_arrange_and_run`**:
   * Agent chỉ cần gửi danh sách các link (YouTube Shorts, TikTok, Facebook Reels, YouTube dài...). Server sẽ tự động phân tích loại nền tảng, thiết lập chiến lược cuộn (đáy lên hoặc trên xuống), thời lượng xem và bắt đầu chạy tuần tự trên app.
5. **`multibrowser_navigate_all`**:
   * Mở một đường link URL đồng loạt trên toàn bộ các cửa sổ đang hoạt động.
6. **`multibrowser_control`**:
   * Các lệnh điều khiển nhanh: tắt/bật âm thanh (`mute_all` / `unmute_all`), chuyển sang Tab YouTube (`all_tab1`), chuyển sang Tab TikTok (`all_tab2`), chia đôi dọc song song (`split_all`), tải lại (`reload_all`).
7. **`multibrowser_stop`**:
   * Dừng ngay lập tức toàn bộ quá trình tự động hóa trên app.

---

## 🚀 Cách cấu hình MCP Server cho AI Agent

### 1. Trong Antigravity IDE (`antigravity.json` hoặc MCP Config)
Thêm vào cấu hình MCP Server:
```json
{
  "mcpServers": {
    "multibrowser": {
      "command": "node",
      "args": ["d:/tools của tôi/MultiBrowser/mcp-server/index.js"]
    }
  }
}
```

### 2. Trong Codex CLI (`~/.codex/config.toml`)
```toml
[mcp_servers.multibrowser]
command = "node"
args = ["d:/tools của tôi/MultiBrowser/mcp-server/index.js"]
```

### 3. Trong Claude Code / Claude Desktop (`claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "multibrowser": {
      "command": "node",
      "args": ["d:/tools của tôi/MultiBrowser/mcp-server/index.js"]
    }
  }
}
```
