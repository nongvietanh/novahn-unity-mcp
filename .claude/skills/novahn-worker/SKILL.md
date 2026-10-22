---
name: novahn-worker
description: Thực hiện task được giao cho AI phụ trong Novahn Studio Hub; dùng khi người dùng chỉ định vai trò AI phụ và endpoint của AI đó.
---

# Novahn Worker

Chỉ dùng endpoint Hub được chỉ định trong prompt tham gia. Gọi `hub.agents.list` để kiểm tra `self` và `leader`; nếu vai trò hoặc project không khớp, báo người dùng trước khi sửa.

1. Đọc `hub.task.inbox`. Chọn task theo phạm vi phụ trách, gọi `hub.task.claim(task_id)` trước khi làm. Khi prompt tham gia yêu cầu duy trì phiên, dùng `hub.task.wait(timeout_seconds=45)` để chờ việc; hết thời gian chờ thì tiếp tục cho tới khi người dùng dừng. Với yêu cầu một lần, nếu không có task thì báo sẵn sàng.
2. Request chứa phạm vi và điều kiện hoàn thành. Đọc AGENTS.md trong project, chọn đúng Unity instance; dùng kết nối Unity MCP hiện có để đọc và sửa Editor. Các tool `hub.*` dùng cho phối hợp.
3. Dùng skill Unity phù hợp nếu có. Khi thiếu tool hoặc thông tin cần thiết, gửi `hub.message.send` cho AI chính cùng `task_id`, mô tả việc đã làm và điều đang thiếu; không báo hoàn thành giả.
4. Xong việc, gọi `hub.task.complete(task_id, result)` với đường dẫn đã sửa, kết quả kiểm tra thực tế và phần chưa xác minh. Gửi cập nhật cho AI chính bằng `hub.message.send` khi cần. Không tự tạo task cho AI khác.
5. Đọc `hub.message.inbox`, xử lý rồi `hub.message.ack`. Chỉ tiếp tục task trong phạm vi người dùng đã giao.

Mỗi thao tác gửi tin nhắn dùng `idempotency_key` riêng, ổn định khi thử lại; không gửi lại bằng khóa mới nếu chưa rõ lần trước đã thành công hay chưa. Không đồng thời sửa cùng scene, prefab hoặc file mà AI khác đang phụ trách; trao đổi với AI chính để xếp thứ tự.
