# Novahn Studio — MCP for Unity

Plugin kết nối Unity Editor với nhiều AI client qua Model Context Protocol (MCP). Dự án này được phát triển từ [MCP for Unity của CoplayDev](https://github.com/CoplayDev/unity-mcp); giấy phép MIT và ghi nhận tác giả gốc được giữ nguyên.

Nếu dự án Unity đang dùng package `com.coplaydev.unity-mcp`, hãy gỡ package cũ trước khi cài `com.novahn.unity-mcp` để tránh nạp trùng mã nguồn.

## Bắt đầu nhanh

1. Trong Unity, mở **Window → Package Manager → + → Add package from git URL**.
2. Dán URL:

   ```text
   https://github.com/nongvietanh/novahn-unity-mcp.git?path=/MCPForUnity#main
   ```

3. Mở **Novahn → Studio** hoặc **Ctrl + Shift + M**. Trong **Connection**, chọn **HTTP Local** và cấu hình kết nối Unity MCP cho ứng dụng AI.
4. Trong **AI Team**, dùng dropdown **Primary AI** để chọn Claude Desktop, Claude Code, Codex hoặc client được hỗ trợ. Tên tự điền; không có client mặc định. Thêm AI phụ bằng dropdown **AI client**.
5. Bấm **Setup MCP + Skills** cho từng AI, bật Hub và mở phiên client theo prompt tham gia. Sau đó giao yêu cầu ngay trong chat desktop của AI chính; Editor dùng để cấu hình và theo dõi.

Yêu cầu: Unity 2020.3 LTS đến Unity 6, Python 3.10 trở lên và `uv`. Unity 2020.3 chưa được kiểm tra compile trên Editor thật trong nhánh phát triển Hub này.

## AI client và nhiều MCP

Có thể cấu hình các client được hỗ trợ như Claude Desktop/Code, Codex, Cursor, VS Code, Windsurf, Gemini CLI, Cline và một số client khác. Client không có cấu hình tự động vẫn có thể dùng phần **Manual Configuration**.

Unity MCP dùng HTTP Local để chia sẻ một Editor. Hub điều phối chạy riêng trong backend đi kèm package, lưu task theo project. Claude Desktop dùng stdio proxy tới các endpoint HTTP này; các client có HTTP dùng trực tiếp.

AI chính có thể dùng tool `novahn_workspace` qua Unity MCP để kết nối, nhận mục tiêu, phân công, đọc kết quả và trao đổi trong đội mà không cần nhập lại yêu cầu trong Editor. AI phụ dùng endpoint riêng. Các phiên AI phải hoạt động để nhận việc; Hub không tự khởi chạy mô hình hoặc đánh thức chat đã dừng.

## Tiếng Việt và skills

Giao diện mặc định **English**, chuyển **EN/VI** ở thanh đầu và lưu lựa chọn trên máy. Phần **Skills** cài bộ Unity, scene, gameplay, điều phối/AI phụ và chat desktop theo vai trò; có thể thêm skill từ thư mục có `SKILL.md`.

Phần **Tasks** hỗ trợ mục tiêu, phân công, bản nháp, lịch gửi, hủy và kết quả. Lịch gửi thực hiện khi Unity và Hub đang chạy. Xem [hướng dẫn sử dụng Studio](MCPForUnity/STUDIO_GUIDE.md).

## Tùy biến tool

Có thể thêm tool C# riêng trong dự án Unity bằng `[McpForUnityTool]`. Các tool MCP hiện có vẫn giữ nguyên tên để tương thích với AI client.

## License

MIT. Xem [LICENSE](LICENSE) và giữ nguyên ghi nhận của dự án gốc khi phân phối bản sửa đổi.
