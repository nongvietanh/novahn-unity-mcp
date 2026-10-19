# Novahn Studio — MCP for Unity

Plugin kết nối Unity Editor với nhiều AI client qua Model Context Protocol (MCP). Dự án này được phát triển từ [MCP for Unity của CoplayDev](https://github.com/CoplayDev/unity-mcp); giấy phép MIT và ghi nhận tác giả gốc được giữ nguyên.

Package Unity có tên `com.novahn.unity-mcp`, hiển thị là **Novahn Studio**.

## Import MCP vào Unity

### Chuẩn bị

- Cài [Git](https://git-scm.com/downloads), cho phép dùng qua `PATH`, rồi mở lại Unity nếu vừa cài Git.
- Cài Python 3.10+ và [uv](https://docs.astral.sh/uv/getting-started/installation/) để chạy MCP server và Hub.
- Nếu đang dùng `com.coplaydev.unity-mcp`, gỡ bản đó trong Package Manager trước để tránh trùng mã nguồn.

### Cách 1: Cài từ GitHub qua Package Manager

1. Mở project Unity → **Window → Package Manager**.
2. Bấm **+** → **Install package from git URL…** hoặc **Add package from git URL…**, tùy phiên bản Unity.
3. Dán **nguyên URL** dưới đây, rồi bấm **Install / Add**:

   ```text
   https://github.com/nongvietanh/novahn-unity-mcp.git?path=/MCPForUnity#main
   ```

4. Chờ Unity tải package và biên dịch. Trong danh sách **In Project**, tìm **Novahn Studio**.
5. Mở **Novahn → Studio** hoặc **Ctrl + Shift + M**.

`?path=/MCPForUnity` trỏ tới package bên trong repo; `#main` chọn nhánh mã nguồn. Không dùng URL repo gốc thiếu phần `?path=...`, và không import cả repo vào `Assets`.

### Cách 2: Thêm vào manifest của project

Trong file **`<UnityProject>/Packages/manifest.json`**, thêm dòng sau vào object `dependencies`, giữ nguyên các dependency khác:

```json
"com.novahn.unity-mcp": "https://github.com/nongvietanh/novahn-unity-mcp.git?path=/MCPForUnity#main"
```

Thêm dấu phẩy giữa các dòng JSON nếu cần, lưu file rồi quay lại Unity để Package Manager tải package. Đây là manifest của **project**, không phải `package.json` trong repo này.

### Cách 3: Dùng bản clone trên máy để phát triển

Clone repo về máy. Trong Package Manager, chọn **+ → Install/Add package from disk…** rồi chọn **`<Repo>/MCPForUnity/package.json`**. Cách này cho phép sửa trực tiếp mã nguồn trong bản clone.

### Nếu project đang dùng bản embedded

Nếu có thư mục **`<UnityProject>/Packages/com.novahn.unity-mcp/`**, Unity ưu tiên bản đó thay cho dependency Git. Đổi URL trong manifest sẽ chưa chuyển sang bản GitHub.

Để chuyển sang GitHub: đóng Unity, sao lưu rồi chuyển **thư mục embedded này ra ngoài thư mục `Packages` của project**, sau đó mở Unity và cài theo Cách 1 hoặc 2. Giữ bản sao lưu nếu có chỉnh sửa riêng. Không sửa trực tiếp `Library/PackageCache`.

### Cập nhật sau khi có code mới trên GitHub

Với bản cài Git, mở lại **Install/Add package from git URL…** và nhập lại URL ở Cách 1 để cập nhật revision. Package Manager có thể giữ commit cũ dù URL vẫn là `#main`; bản embedded/local cần được cập nhật theo nguồn tương ứng.

Tham khảo Unity: [Cài bằng Git URL](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-ui-giturl.html), [ưu tiên của embedded package](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/dependencies-lp/upm-embed).

## Kết nối AI sau khi import

1. Mở **Novahn → Studio**. Để dùng backend Novahn từ cùng repo, trong **Settings → Advanced Settings → Server Source** nhập:

   ```text
   git+https://github.com/nongvietanh/novahn-unity-mcp.git@main#subdirectory=Server
   ```

   Nếu server đã chạy trước khi đổi nguồn, dừng rồi khởi động lại. Đây là nguồn Python server, khác URL import package Unity ở trên.

2. Trong **Connection**, chọn **HTTP Local**; bấm **Start Server** và **Start** để kết nối Editor nếu chưa chạy.
3. Trong **AI Team**, dùng dropdown **Primary AI** chọn Claude Desktop, Claude Code, Codex hoặc client được hỗ trợ. Thêm AI phụ bằng dropdown **AI client**; tên tự sinh.
4. Bấm **Setup MCP + Skills** cho các AI cần dùng, rồi **Start Hub**. Mở/reconnect ứng dụng AI để nhận cấu hình và dùng **Join prompt** với phiên endpoint riêng.
5. Giao yêu cầu ngay trong chat desktop của AI chính. Không cần nhập lại trong Unity Editor. Xem task trong **Tasks** và trao đổi trong **AI Log**.

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
