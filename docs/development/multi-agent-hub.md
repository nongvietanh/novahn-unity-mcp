# Chạy thử Multi-Agent MCP Hub

Lát cắt hiện tại cung cấp hai MCP endpoint độc lập và các tool đọc Unity. Cần Python/`uv` và package Unity từ repo này. Hub thay thế tiến trình HTTP Local MCP cũ ở cổng 8080; dừng tiến trình cũ trước khi chạy Hub.

## Khởi động trên PowerShell

Tại root repo:

```powershell
$env:UNITY_HUB_CODEX_TOKEN = python -c "import secrets; print(secrets.token_urlsafe(48))"
$env:UNITY_HUB_CLAUDE_TOKEN = python -c "import secrets; print(secrets.token_urlsafe(48))"
uv run --project Server --frozen novahn-unity-hub
```

Mặc định Hub nghe `127.0.0.1:4317` (Codex), `127.0.0.1:4318` (Claude Code) và `127.0.0.1:8080` (Unity bridge). Có thể đổi bằng `UNITY_HUB_CODEX_PORT`, `UNITY_HUB_CLAUDE_PORT`, `UNITY_HUB_BRIDGE_URL`; bridge URL phải là HTTP loopback. Các token phải khác nhau, tối thiểu 32 ký tự ASCII và không có khoảng trắng. Giữ token trong môi trường của process client tương ứng; không commit chúng.

Task/message/audit được lưu mặc định tại `~/.novahn-unity-hub/hub.sqlite3`; đặt `UNITY_HUB_DB_PATH` để chọn file khác. Inbox trả tối đa 50 record đầu tiên mỗi lần gọi.

Trong Unity chọn HTTP Local, URL `http://127.0.0.1:8080`, rồi kết nối bridge. Nếu Unity tự bật server HTTP cũ khi mở Editor, tắt tính năng tự bật để Hub giữ cổng 8080.

Mở **Novahn MCP → Multi-Agent Hub** để xem trạng thái Hub, Unity instance, hoạt động gần đây của agent, task và số tin nhắn chưa đọc. Window đọc `/hub/status` mỗi 3 giây. `last request` chỉ là hoạt động gần đây, không khẳng định agent còn online. Window không hiển thị nội dung task hoặc token.

## Cấu hình client

Codex, trong `~/.codex/config.toml`:

```toml
[mcp_servers.novahnHub]
url = "http://127.0.0.1:4317/mcp"
bearer_token_env_var = "UNITY_HUB_CODEX_TOKEN"
```

Claude Code, trong `.mcp.json` của project hoặc scope local tương đương:

```json
{
  "mcpServers": {
    "novahnHub": {
      "type": "http",
      "url": "http://127.0.0.1:4318/mcp",
      "headers": {
        "Authorization": "Bearer ${UNITY_HUB_CLAUDE_TOKEN}"
      }
    }
  }
}
```

Xác minh với `codex mcp list`, `claude mcp list` và gọi `hub.health`. `hub.project.info` liệt kê Editor đang kết nối; `unity.scenes.list` đọc scene đã load. Nếu nhiều Editor, truyền `unity_instance` bằng `hash`, tên project duy nhất, hoặc `Name@hash`.

Để giao việc ở chế độ client tự kết nối, agent gửi `hub.task.create` với `assignee`, `request`, `idempotency_key`. Agent nhận gọi `hub.task.inbox`, `hub.task.claim`, rồi `hub.task.complete` hoặc người tham gia gọi `hub.task.cancel`. Creator đọc kết quả qua `hub.task.get`. Tin nhắn dùng `hub.message.send`, `hub.message.inbox`, `hub.message.ack`. Idempotency key phải ổn định khi retry cùng một thao tác.

## Trạng thái hiện tại

Đã test hai endpoint và token bằng FastMCP client, task/message xuyên hai endpoint, SQLite sau restart, snapshot UI không lộ nội dung, cùng smoke test ba cổng trên Windows. EditorWindow trạng thái đã có; onboarding, approval, diagnostics và cấu hình tự động chưa có. Package đã được kiểm tra cú pháp C# 8 và compile trên Unity 6000.3.13f1; Unity 2020.3/2021.3/2022.3 chưa được kiểm tra thực tế. Chưa kiểm tra Codex/Claude CLI thực tế với Hub hoặc Unity Editor kết nối thật. Orchestrator chủ động chạy CLI, deadline/recovery, lock và mutation chưa có trong lát cắt này. Cổng bridge đóng MCP trực tiếp nhưng WebSocket đăng ký Unity chưa có xác thực riêng; chỉ dùng trên máy cục bộ tin cậy ở giai đoạn này.
