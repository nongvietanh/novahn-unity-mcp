# Novahn Studio

Workspace điều phối AI cho Unity. Mở **Novahn → Studio** hoặc **Ctrl + Shift + M**.

Mặc định **English**; chuyển **EN/VI** trên thanh đầu Studio. Chọn **Primary AI** bằng dropdown: Claude Desktop, Claude Code, Codex hoặc client được hỗ trợ; tên tự sinh, không mặc định Codex. Giao yêu cầu ngay trong ứng dụng AI qua Unity MCP: AI dùng `novahn_workspace` với đúng client để nhận vai trò, bật Hub, nhận mục tiêu và phân công. Không cần nhập lại yêu cầu trong Unity Editor. Cài skill `novahn-desktop` từ phần Setup để hướng dẫn AI sử dụng luồng này.

- **Đội AI:** một AI chính và nhiều AI phụ; setup MCP và skill theo từng client.
- **Công việc:** gửi mục tiêu, phân công, tách task, hẹn ngày giờ và xem kết quả.
- **Kỹ năng:** cài bộ Novahn cho từng AI/cả đội hoặc thêm skill riêng từ thư mục.
- **Kết nối / Công cụ / Tài nguyên / Tạo nội dung / Cài đặt:** quản lý kết nối và khả năng thao tác Unity.

Xem [hướng dẫn Studio và chat desktop](STUDIO_GUIDE.md).

Hub sử dụng Python và uvx; các AI chạy trong client của người dùng. Bật Hub và dùng Prompt tham gia trong từng phiên AI để duy trì nhận việc. Lịch gửi hoạt động khi Unity mở và Hub chạy.

Mã nguồn nền phát triển từ [MCP for Unity của CoplayDev](https://github.com/CoplayDev/unity-mcp), theo giấy phép MIT. Giấy phép và ghi nhận tác giả gốc được giữ trong [LICENSE](LICENSE).
