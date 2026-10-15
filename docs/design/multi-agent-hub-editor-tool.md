# Unity Editor tool cho Multi-Agent Hub

**Mục tiêu phiên bản:** Unity 2020.3 LTS đến Unity 6.x. Đây là mức tối thiểu của package trong `package.json`; Unity 2020.1/2020.2 chưa nằm trong ma trận. Mã dùng C# 8 và API UI Toolkit Editor đã có từ 2020.3. Cần chạy compile và smoke test trên từng bản Editor trước khi gắn nhãn verified.

## Bố cục

Mở bằng **Novahn MCP → Multi-Agent Hub**. Window độc lập để có thể dock cạnh Console/Inspector, đồng thời giữ màn hình MCP hiện tại cho cấu hình server và client.

```text
┌ Multi-Agent Hub ───────────────────────── [Làm mới] ┐
│ Hub đang chạy • giờ cập nhật                         │
│ http://127.0.0.1:8080/hub/status                    │
├ Unity Editor đang kết nối ──────────────────────────┤
│ Project • Unity version • instance hash             │
├ Agent ──────────────────────────────────────────────┤
│ Codex / Claude Code • port • yêu cầu cuối           │
├ Task gần đây ───────────────────────────────────────┤
│ id ngắn • state • creator → assignee                │
├ Tin nhắn chưa đọc ──────────────────────────────────┤
│ số lượng mỗi agent                                  │
└ metadata, không hiển thị nội dung task/message ─────┘
```

Window đọc `GET /hub/status` trên **HTTP Local** đã cấu hình, mỗi 3 giây khi mở. Request bất đồng bộ, timeout 3 giây và được hủy khi window đóng/domain reload. Không giữ agent token trong Editor. Nút Làm mới chỉ cập nhật trạng thái. Dữ liệu được dựng bằng UI Toolkit API cơ bản (`EditorWindow.CreateGUI`, `VisualElement`, `Label`, `ScrollView`, `Button`) để cùng một code path chạy trên 2020.3 và Unity 6. Nhãn đi qua `McpUiLocalization` cho Tiếng Việt/English.

## Contract trạng thái

`/hub/status` chỉ bind loopback và chỉ nhận GET với Host/Origin hợp lệ. Response gồm `status`, `updatedAt`, `instances`, `agents`, `taskCounts`, `unreadMessages`, `tasks` (tối đa 20). `lastSeenAt` là thời điểm **request xác thực cuối cùng**, không phải xác nhận agent còn kết nối. Task chỉ có ID, creator, assignee, state và timestamps. Không trả token, task request/result, message body hay log.

Các trạng thái UI cần phân biệt: Hub không chạy, dữ liệu lỗi, không có Unity instance, agent chưa có request, task rỗng. Cổng Hub và URL local phải trùng cấu hình của Unity. Nếu dùng transport khác HTTP Local, window vẫn xem Hub local nhưng không đổi transport đang dùng.

## Hướng mở rộng

- **Onboarding:** wizard tạo token riêng, hiển thị cấu hình Codex/Claude trước khi ghi, kiểm tra endpoint và Unity bridge. Không đưa token vào snapshot trạng thái.
- **Timeline/inbox:** cần endpoint có xác thực cho Editor và phân quyền theo project trước khi xem nội dung task/message hoặc gửi/ack.
- **Locks/approval:** chỉ thêm khi Hub có lock lease, policy và xác minh trong Unity dispatcher ngay trước mutation. Mọi nút gây thay đổi phải hiển thị target, owner, operation, correlation ID và kết quả.
- **Diagnostics:** hiển thị lỗi kết nối/compile/reconnect, tạo support bundle có redaction; không trộn log chứa secret vào route trạng thái.

## Ma trận xác minh

| Unity | Cần kiểm tra |
| --- | --- |
| 2020.3 LTS | package import, compile C# 8, mở window, polling, domain reload, Play Mode |
| 2021.3 LTS | các mục trên và WebSocket bridge |
| 2022.3 LTS | các mục trên và scene read |
| Unity 6.x | các mục trên, package import/compile và scene read |

Hiện đã kiểm tra cú pháp C# 8 trên toàn bộ package và import/compile package bằng Unity 6000.3.13f1. Chưa kiểm tra Editor 2020.3/2021.3/2022.3 thực tế.
