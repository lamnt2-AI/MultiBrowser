#!/usr/bin/env node
/**
 * MultiBrowser MCP Server
 * Standard Model Context Protocol (MCP) Server for controlling MultiBrowser application.
 * Connects to the embedded HTTP API server in MultiBrowser (http://127.0.0.1:5055).
 */

const http = require('http');
const readline = require('readline');
const fs = require('fs');
const path = require('path');

const API_HOST = '127.0.0.1';
let API_PORT = 5055;

// Tự động tìm và đọc file api_token.txt để xác thực an toàn
function getApiToken() {
  if (process.env.MULTIBROWSER_API_TOKEN) {
    return process.env.MULTIBROWSER_API_TOKEN.trim();
  }

  const potentialPaths = [
    path.join(__dirname, '..', 'api_token.txt'),
    path.join(__dirname, '..', 'MultiBrowser', 'bin', 'Release', 'net10.0-windows', 'api_token.txt'),
    path.join(__dirname, '..', 'MultiBrowser', 'bin', 'Release', 'net10.0-windows', 'publish', 'api_token.txt'),
    path.join(__dirname, '..', 'MultiBrowser', 'bin', 'Debug', 'net10.0-windows', 'api_token.txt'),
    path.join(process.cwd(), 'api_token.txt')
  ];

  for (const p of potentialPaths) {
    try {
      if (fs.existsSync(p)) {
        const token = fs.readFileSync(p, 'utf8').trim();
        if (token) return token;
      }
    } catch (e) {}
  }
  return '';
}

// Tự động đọc port.txt mà MultiBrowser đã ghi khi khởi động
function getActualPort() {
  if (process.env.MULTIBROWSER_PORT) {
    const p = parseInt(process.env.MULTIBROWSER_PORT, 10);
    if (!isNaN(p)) return p;
  }

  const potentialPaths = [
    path.join(__dirname, '..', 'port.txt'),
    path.join(__dirname, '..', 'MultiBrowser', 'bin', 'Release', 'net10.0-windows', 'port.txt'),
    path.join(__dirname, '..', 'MultiBrowser', 'bin', 'Release', 'net10.0-windows', 'publish', 'port.txt'),
    path.join(__dirname, '..', 'MultiBrowser', 'bin', 'Debug', 'net10.0-windows', 'port.txt'),
    path.join(process.cwd(), 'port.txt')
  ];

  for (const p of potentialPaths) {
    try {
      if (fs.existsSync(p)) {
        const content = fs.readFileSync(p, 'utf8').trim();
        const port = parseInt(content, 10);
        if (!isNaN(port) && port > 0) return port;
      }
    } catch (e) {}
  }
  return API_PORT;
}

// Helper to make HTTP requests to the running MultiBrowser WPF App with fallback
function callApi(endpoint, method = 'GET', data = null, attemptedFallback = false) {
  return new Promise((resolve, reject) => {
    const postData = data ? JSON.stringify(data) : '';
    const token = getApiToken();
    const currentPort = getActualPort();

    const options = {
      hostname: API_HOST,
      port: currentPort,
      path: endpoint,
      method: method,
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        ...(data ? { 'Content-Length': Buffer.byteLength(postData) } : {})
      },
      timeout: 10000
    };

    const req = http.request(options, (res) => {
      let body = '';
      res.setEncoding('utf8');
      res.on('data', (chunk) => body += chunk);
      res.on('end', () => {
        try {
          const parsed = JSON.parse(body);
          // Nếu port 5055 bị chiếm bởi service khác (không phải MultiBrowser)
          if (!attemptedFallback && currentPort === 5055 && res.statusCode === 404 && !parsed.appName) {
            API_PORT = 5056;
            callApi(endpoint, method, data, true).then(resolve).catch(reject);
            return;
          }
          resolve(parsed);
        } catch (e) {
          // Phản hồi không phải JSON của MultiBrowser (ví dụ service khác đang dùng port 5055)
          if (!attemptedFallback && currentPort === 5055) {
            API_PORT = 5056;
            callApi(endpoint, method, data, true).then(resolve).catch(reject);
            return;
          }
          resolve({ raw: body, statusCode: res.statusCode });
        }
      });
    });

    req.on('error', (err) => {
      // Nếu port 5055 bị từ chối kết nối, thử fallback sang 5056
      if (!attemptedFallback && currentPort === 5055) {
        API_PORT = 5056;
        callApi(endpoint, method, data, true).then(resolve).catch(reject);
        return;
      }

      resolve({
        success: false,
        error: `Không thể kết nối đến MultiBrowser (Port ${currentPort}). Hãy chắc chắn ứng dụng MultiBrowser.exe đang chạy trên màn hình của bạn!`,
        details: err.message
      });
    });

    req.on('timeout', () => {
      req.destroy();
      resolve({ success: false, error: 'Yêu cầu tới MultiBrowser bị quá thời gian chờ (timeout).' });
    });

    if (data) {
      req.write(postData);
    }
    req.end();
  });
}

// Danh mục các công cụ (Tools) mà MCP Server cung cấp cho AI Agents
const TOOLS = [
  {
    name: 'multibrowser_get_capabilities',
    description: 'Hiển thị chi tiết tất cả các lệnh, tính năng và trạng thái điều khiển MultiBrowser mà Agent có thể thực thi. Hãy gọi lệnh này ngay lần đầu kết nối để nắm rõ khả năng của app.',
    inputSchema: {
      type: 'object',
      properties: {}
    }
  },
  {
    name: 'multibrowser_get_status',
    description: 'Lấy trạng thái thời gian thực của app MultiBrowser (số cửa sổ đang mở, URL các tab, tiến trình automation, âm thanh...).',
    inputSchema: {
      type: 'object',
      properties: {}
    }
  },
  {
    name: 'multibrowser_set_window_count',
    description: 'Thay đổi số lượng cửa sổ trình duyệt hiển thị trên màn hình thành 2, 4, 6 hoặc 8 cửa sổ.',
    inputSchema: {
      type: 'object',
      properties: {
        count: {
          type: 'integer',
          enum: [1, 2, 4, 6, 8],
          description: 'Số lượng cửa sổ mong muốn: 2 (1x2), 4 (2x2), 6 (2x3), 8 (2x4)'
        }
      },
      required: ['count']
    }
  },
  {
    name: 'multibrowser_auto_arrange_and_run',
    description: 'Nhận danh sách link từ người dùng hoặc agent (YouTube Shorts, YouTube dài, TikTok, Facebook Reels, Instagram Reels), tự động phân tích loại nền tảng, thiết lập chiến lược cuộn (đáy lên hoặc trên xuống), thời lượng xem và bắt đầu chạy trực tiếp trên app ngay lập tức.',
    inputSchema: {
      type: 'object',
      properties: {
        links: {
          type: 'array',
          items: { type: 'string' },
          description: 'Danh sách các đường link video/kênh cần xem (YouTube Shorts, TikTok, Facebook Reels, YouTube dài...)'
        },
        videoCountPerLink: {
          type: 'integer',
          default: 3,
          description: 'Số video cần xem ở mỗi link trước khi chuyển sang link tiếp theo (mặc định: 3, riêng video dài master sẽ tự động đặt là 1)'
        },
        strategy: {
          type: 'string',
          enum: ['TopToBottom', 'BottomToTop'],
          description: 'Chiến lược cuộn: TopToBottom (lướt từ trên xuống) hoặc BottomToTop (cuộn xuống đáy rồi lướt ngược lên). Nếu không truyền, hệ thống sẽ tự động chọn tối ưu cho từng nền tảng.'
        },
        durationMode: {
          type: 'string',
          enum: ['FullVideoDuration', 'FixedSeconds', 'RandomRange'],
          default: 'FullVideoDuration',
          description: 'Chế độ thời lượng xem: FullVideoDuration (xem hết thời lượng thực tế của video), FixedSeconds (cố định), RandomRange (ngẫu nhiên)'
        },
        fixedSeconds: {
          type: 'integer',
          default: 15,
          description: 'Số giây xem nếu chọn FixedSeconds (mặc định 15)'
        },
        loopContinuous: {
          type: 'boolean',
          default: true,
          description: 'Có lặp lại kịch bản liên tục khi chạy hết danh sách hay không'
        },
        autoLike: {
          type: 'boolean',
          default: true,
          description: 'Tự động thả tym/like video nếu chưa like'
        }
      },
      required: ['links']
    }
  },
  {
    name: 'multibrowser_navigate_all',
    description: 'Mở một đường link URL đồng loạt trên toàn bộ các cửa sổ đang hoạt động.',
    inputSchema: {
      type: 'object',
      properties: {
        url: {
          type: 'string',
          description: 'Đường dẫn URL cần mở (ví dụ: https://accounts.google.com hoặc https://www.youtube.com)'
        }
      },
      required: ['url']
    }
  },
  {
    name: 'multibrowser_control',
    description: 'Thực thi các lệnh điều khiển nhanh giao diện app MultiBrowser.',
    inputSchema: {
      type: 'object',
      properties: {
        action: {
          type: 'string',
          enum: ['all_tab1', 'all_tab2', 'split_all', 'unsplit_all', 'reload_all', 'mute_all', 'unmute_all'],
          description: 'all_tab1: xem Tab 1 YouTube | all_tab2: xem Tab 2 TikTok | split_all: chia đôi dọc | unsplit_all: trở về 1 tab | reload_all: tải lại | mute_all: tắt âm | unmute_all: bật âm'
        }
      },
      required: ['action']
    }
  },
  {
    name: 'multibrowser_like_debug',
    description: 'Chẩn đoán nút tym (TikTok) của MỘT ô/acc theo profileId: nút có thấy không, đã đỏ chưa, có popup đăng nhập chặn không. Đặt click=true để bấm tym acc đó rồi đọc lại trạng thái.',
    inputSchema: {
      type: 'object',
      properties: {
        profileId: { type: 'integer', description: 'ID acc/ô cần kiểm tra (1-8, xem profileId trong multibrowser_get_status)' },
        click: { type: 'boolean', default: false, description: 'true: bấm tym rồi trả về trạng thái trước/sau khi bấm' }
      },
      required: ['profileId']
    }
  },
  {
    name: 'multibrowser_stop',
    description: 'Dừng ngay lập tức toàn bộ quá trình tự động hóa / lướt video đang chạy trên app MultiBrowser.',
    inputSchema: {
      type: 'object',
      properties: {}
    }
  }
];

// Xử lý gọi tool
async function handleToolCall(name, args) {
  switch (name) {
    case 'multibrowser_get_capabilities': {
      const res = await callApi('/api/capabilities');
      return res;
    }
    case 'multibrowser_get_status': {
      const res = await callApi('/api/status');
      return res;
    }
    case 'multibrowser_set_window_count': {
      const res = await callApi('/api/set_window_count', 'POST', { count: args.count });
      return res;
    }
    case 'multibrowser_auto_arrange_and_run': {
      const res = await callApi('/api/playlist/start', 'POST', {
        links: args.links,
        videoCountPerLink: args.videoCountPerLink || 3,
        strategy: args.strategy,
        durationMode: args.durationMode || 'FullVideoDuration',
        fixedSeconds: args.fixedSeconds || 15,
        loopContinuous: args.loopContinuous !== false,
        autoLike: args.autoLike !== false
      });
      return res;
    }
    case 'multibrowser_navigate_all': {
      const res = await callApi('/api/navigate', 'POST', { url: args.url });
      return res;
    }
    case 'multibrowser_control': {
      if (args.action === 'mute_all') {
        return await callApi('/api/mute', 'POST', { mute: true });
      } else if (args.action === 'unmute_all') {
        return await callApi('/api/mute', 'POST', { mute: false });
      } else {
        return await callApi('/api/layout', 'POST', { action: args.action });
      }
    }
    case 'multibrowser_like_debug': {
      return await callApi('/api/like', 'POST', { profileId: args.profileId, click: !!args.click });
    }
    case 'multibrowser_stop': {
      const res = await callApi('/api/playlist/stop', 'POST', {});
      return res;
    }
    default:
      throw new Error(`Tool không được hỗ trợ: ${name}`);
  }
}

// Xử lý luồng JSON-RPC stdio
const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  terminal: false
});

function sendResponse(id, result, error = null) {
  const response = {
    jsonrpc: '2.0',
    id: id
  };
  if (error) {
    response.error = error;
  } else {
    response.result = result;
  }
  process.stdout.write(JSON.stringify(response) + '\n');
}

rl.on('line', async (line) => {
  if (!line || !line.trim()) return;

  try {
    const msg = JSON.parse(line);
    const { id, method, params } = msg;

    if (method === 'initialize') {
      sendResponse(id, {
        protocolVersion: '2024-11-05',
        capabilities: {
          tools: {}
        },
        serverInfo: {
          name: 'multibrowser-mcp-server',
          version: '1.0.0'
        }
      });
    } else if (method === 'notifications/initialized') {
      // Client confirmed init
    } else if (method === 'tools/list') {
      sendResponse(id, {
        tools: TOOLS
      });
    } else if (method === 'tools/call') {
      const toolName = params?.name;
      const toolArgs = params?.arguments || {};

      try {
        const result = await handleToolCall(toolName, toolArgs);
        sendResponse(id, {
          content: [
            {
              type: 'text',
              text: typeof result === 'string' ? result : JSON.stringify(result, null, 2)
            }
          ]
        });
      } catch (err) {
        sendResponse(id, {
          isError: true,
          content: [
            {
              type: 'text',
              text: `Lỗi thực thi tool '${toolName}': ${err.message}`
            }
          ]
        });
      }
    } else if (method === 'ping') {
      sendResponse(id, {});
    } else {
      if (id !== undefined) {
        sendResponse(id, null, {
          code: -32601,
          message: `Method '${method}' not found`
        });
      }
    }
  } catch (err) {
    // Parse error
  }
});
