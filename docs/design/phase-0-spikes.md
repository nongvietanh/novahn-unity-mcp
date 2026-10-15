# Phase 0 — compatibility và quyết định còn mở

**Snapshot 2026-10-15 trên Windows:** `codex-cli 0.162.0-alpha.17.2`, `Claude Code 2.1.296`, `Python 3.14.8`, `uv 0.12.23`. Đây là phiên bản binary có trên máy, chưa là ma trận hỗ trợ.

## Đã có bằng chứng

- FastMCP 3.0.2 dựng đồng thời hai MCP endpoint local; test gọi `hub.health`, `unity.scenes.list` với fake bridge và bác token dùng sai endpoint.
- Task/message từ hai endpoint dùng chung SQLite; test claim, complete, ack, idempotency và đọc lại sau khi mở database mới.
- Bridge-only listener trả 404 cho `/mcp`, 401 cho REST thiếu token. WebSocket `/hub/plugin` nhận kết nối và gửi `welcome` trong smoke test.
- `TransportCommandDispatcher` của nhánh source hiện có là hàng đợi Unity main thread được giữ lại. Chưa thử Unity Editor thật trong phiên này.

## Cần xác minh trước khi mở mutation

1. Codex và Claude Code cài thực tế cùng `tools/list`/`tools/call` Hub; ghi MCP revision được thương lượng, lỗi auth, reconnect.
2. Unity 2020.3/2021.3/2022.3/Unity 6: package kết nối Hub, đọc scene thật, domain reload và nhiều instance. Unity 2020.1/2020.2 chưa thuộc phạm vi.
3. Thiết kế xác thực WebSocket Unity, project identity bền, audit mọi tool call, task deadline và phục hồi sau crash.
4. Lock lease/approval được kiểm tra tại Editor ngay trước thực thi; lệnh không rõ đã thực thi không được retry tự động.
5. CLI adapter: streaming event, hủy process tree, resume và quyền trên phiên bản được công bố.

Mỗi hàng compatibility matrix cần OS, Unity, phiên bản Codex/Claude, MCP protocol, SDK, mode, kết quả và log kiểm thử. Không gắn nhãn supported chỉ từ tài liệu hoặc FastMCP client thử nghiệm.
