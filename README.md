# Novahn MCP for Unity

Plugin kết nối Unity Editor với nhiều AI client qua Model Context Protocol (MCP). Dự án này được phát triển từ [MCP for Unity của CoplayDev](https://github.com/CoplayDev/unity-mcp); giấy phép MIT và ghi nhận tác giả gốc được giữ nguyên.

Nếu dự án Unity đang dùng package `com.coplaydev.unity-mcp`, hãy gỡ package cũ trước khi cài `com.novahn.unity-mcp` để tránh nạp trùng mã nguồn.

## Bắt đầu nhanh

1. Trong Unity, mở **Window → Package Manager → + → Add package from git URL**.
2. Dán URL:

   ```text
   https://gitlab.com/novahn/unity-mcp.git?path=/MCPForUnity#main
   ```

3. Mở **Novahn MCP → Toggle MCP Window**. Chọn **HTTP Local** để nhiều AI client cùng dùng một Unity Editor.
4. Chọn AI client rồi bấm **Configure** hoặc **Configure All Detected Clients**.
5. Muốn tự khởi động MCP cùng Unity, bật **Tự bật MCP khi mở Unity** ở mục Kết nối. Có thể tắt lại bất cứ lúc nào.

Yêu cầu: Unity 2020.3 LTS đến Unity 6, Python 3.10 trở lên và `uv`. Unity 2020.3 chưa được kiểm tra compile trên Editor thật trong nhánh phát triển Hub này.

## AI client và nhiều MCP

Có thể cấu hình các client được hỗ trợ như Claude Desktop/Code, Codex, Cursor, VS Code, Windsurf, Gemini CLI, Cline và một số client khác. Client không có cấu hình tự động vẫn có thể dùng phần **Manual Configuration**.

Chọn **HTTP Local** nếu muốn nhiều AI client kết nối đồng thời tới cùng Unity Editor. **stdio** phù hợp khi chỉ dùng một AI client.

## Tiếng Việt và skills

Giao diện có lựa chọn **Tiếng Việt / English** ở góc trên. Trong mục Kết nối, chọn Codex hoặc Claude rồi bấm **Cài bộ kỹ năng Novahn** để cài các skill hướng dẫn AI thao tác Unity, dựng scene và phát triển gameplay.

## Tùy biến tool

Có thể thêm tool C# riêng trong dự án Unity bằng `[McpForUnityTool]`. Các tool MCP hiện có vẫn giữ nguyên tên để tương thích với AI client.

## License

MIT. Xem [LICENSE](LICENSE) và giữ nguyên ghi nhận của dự án gốc khi phân phối bản sửa đổi.
