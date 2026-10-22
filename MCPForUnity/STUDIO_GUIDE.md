# Novahn Studio

Workspace Unity cho một AI chính và nhiều AI phụ. Mở **Novahn → Studio** hoặc **Ctrl + Shift + M**. Mặc định English; chuyển **EN/VI** trên thanh đầu.

## Chọn đội AI

1. Trong **Connection**, thiết lập Unity MCP và kết nối client tới Editor. HTTP Local cho phép nhiều client dùng chung Unity.
2. **AI chính** là AI bạn đang chat (Claude Desktop, Claude Code, Codex...). Khi AI đó gọi `connect` với client ID của chính nó, nó trở thành AI chính; không cần chọn trong Unity. Đổi sang chat với AI khác rồi `connect` thì AI mới thay vai trò, các task cũ vẫn giữ.
3. **Thành viên** phải là AI dạng CLI (Claude Code, Codex, Gemini CLI, Copilot CLI...). Bấm **Scan AI on this machine** để liệt kê các CLI có trong PATH rồi **+ Add AI**, hoặc chọn trong dropdown **AI client**. Tên trùng được thêm số.
4. Bấm **Setup MCP + Skills** để merge cấu hình, giữ các server khác và tạo backup. Với client thủ công, dùng **Copy configuration**. Claude Desktop nhận cấu hình stdio proxy tới Hub và Unity HTTP.
5. Bật Hub, mở/reconnect client và dùng **Join prompt** cho phiên nhận việc qua endpoint riêng.

Khi đổi đội hình đang chạy, Studio khởi động lại Hub để nạp cấu hình; task vẫn lưu trong project. Các phiên đang chờ có thể cần kết nối lại.

## Giao việc từ chat desktop

Cài skill `novahn-desktop` và hướng dẫn AI dùng nó cho yêu cầu Unity. Với client đọc `AGENTS.md`, có thể thêm:

> For substantive Unity requests, use the novahn-desktop skill and the existing Unity MCP connection to receive the goal directly from desktop chat. Do not require the user to re-enter it in Unity. Use the actual calling client ID; the AI that connects becomes the primary.

AI đọc `mcpforunity://custom-tools`, gọi `novahn_workspace` trực tiếp hoặc qua `execute_custom_tool`. `status.availableClients` cung cấp ID client. Ví dụ với Claude Code:

```json
{"action":"connect","arguments":{"client":"claudecode"}}
```

Claude Desktop dùng `claudedesktop`, Codex dùng `codex`. Dùng đúng ID ứng dụng mình đang chạy, không giả danh client khác. Nếu Hub đang khởi động, chờ theo `retryAfterSeconds` rồi lặp lại action với cùng khóa idempotency.

| Action | Mục đích |
| --- | --- |
| `status`, `connect` | Đọc workspace, hoặc nhận vai trò AI chính và bật Hub. |
| `request`, `delegate` | Ghi nhận mục tiêu chat hoặc giao task tự chứa cho AI phụ. Cần `title`, `request`, `idempotency_key`; delegate cần `assignee`. |
| `inbox`, `claim`, `get`, `complete`, `cancel` | Nhận việc, đọc tiến độ, trả kết quả thực tế và hủy. Các thao tác task dùng `task_id`; complete cần `result`. |
| `events`, `messages`, `message`, `ack` | Theo dõi tiến độ và trao đổi trong đội. |
| `add_agent`, `setup_client`, `install_skills` | Thêm thành viên AI dạng CLI với tên tự sinh, thiết lập cấu hình hoặc cài skill. |

Gateway Unity MCP dành cho AI chính được tin cậy. AI phụ dùng endpoint Hub riêng, xác thực bằng token riêng, để gọi `hub.task.wait/inbox/claim/complete` và `hub.message.*`. Không dùng gateway để giả danh AI chính.

## Task, lịch và skill

**Tasks** là form tùy chọn để lưu nháp, gửi mục tiêu, phân công, tách task con, hủy và đọc kết quả. Cũng có thể tạo lịch từ chat qua `scheduled_at`, là thời điểm ISO8601 trong tương lai có múi giờ rõ ràng. `get`/`cancel` chấp nhận `localId` cho task chưa gửi.

Lịch gửi cần Unity mở và Hub hoạt động. Task quá giờ được gửi khi Hub sẵn sàng. Hub không chạy mô hình AI hoặc đánh thức chat đã dừng; các phiên nhận việc phải đang hoạt động. **AI Log** và các bảng chỉ hiển thị hoạt động quan sát được; cấu hình client không có nghĩa là AI đang chạy.

**Skills** cài bộ Unity và hướng dẫn theo vai trò: AI chính có coordinator + desktop, AI phụ có worker. Có thể chọn thư mục đích và nhập skill riêng từ thư mục chứa `SKILL.md` cùng tài nguyên đi kèm. Client có thể cần phiên mới để nạp skill.

## Dữ liệu và kiến trúc

- Package Unity: `MCPForUnity/`; Hub đi kèm: `Editor/StudioServer~/` (Python + uvx). Unity bỏ qua thư mục có hậu tố `~`.
- Server Unity MCP chính trong `Server/` vẫn cung cấp kết nối Unity; Hub Studio dùng lại bridge này, không chiếm cổng của nó.
- Cấu hình đội/token, lịch/task và database theo project nằm trong `UserSettings`; log/cache nằm trong `Library`. Không chia sẻ các thư mục này như mã nguồn package.
- Cài lần đầu cần Internet để uvx tải thư viện. MIT và ghi nhận tác giả gốc được giữ trong `LICENSE`.

Tham khảo transport proxy: [FastMCP Proxy](https://gofastmcp.com/servers/providers/proxy).
