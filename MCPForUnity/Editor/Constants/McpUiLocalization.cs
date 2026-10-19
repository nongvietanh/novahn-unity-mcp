using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Constants
{
    /// <summary>
    /// Small built-in language switch for the setup and connection UI.
    /// MCP tool names and protocol fields intentionally remain stable across languages.
    /// </summary>
    internal static class McpUiLocalization
    {
        private const string LanguageKey = "Novahn.MCP.UiLanguage";

        private static readonly Dictionary<string, string> Vietnamese = new Dictionary<string, string>()
        {
            ["Novahn MCP for Unity"] = "Novahn MCP cho Unity",
            ["Checking Hub..."] = "Đang kiểm tra Hub...",
            ["Connected Unity Editors"] = "Unity Editor đang kết nối",
            ["Agents"] = "Agent",
            ["Recent Tasks"] = "Task gần đây",
            ["Unread Messages"] = "Tin nhắn chưa đọc",
            ["No data"] = "Chưa có dữ liệu",
            ["No connection"] = "Không có kết nối",
            ["This dashboard shows metadata only. Task and message bodies never enter the Editor."] = "Bảng trạng thái chỉ hiển thị metadata; nội dung task và tin nhắn không được gửi tới Editor.",
            ["Hub unavailable: "] = "Hub không phản hồi: ",
            ["Invalid Hub data: "] = "Dữ liệu Hub không hợp lệ: ",
            ["Hub is not ready"] = "Hub chưa sẵn sàng",
            ["Hub running"] = "Hub đang chạy",
            ["No Unity Editors connected"] = "Chưa có Unity Editor kết nối",
            ["No agents"] = "Chưa có agent",
            ["No tasks"] = "Chưa có task",
            ["no requests seen"] = "chưa thấy yêu cầu",
            ["last request"] = "yêu cầu cuối",
            ["MCP for Unity"] = "Novahn MCP cho Unity",
            ["MCP Setup"] = "Thiết lập MCP",
            ["MCP for Unity Setup"] = "Thiết lập Novahn MCP cho Unity",
            ["Connect"] = "Kết nối",
            ["Tools"] = "Công cụ",
            ["Resources"] = "Tài nguyên",
            ["Generative"] = "Tạo nội dung",
            ["Deps"] = "Phụ thuộc",
            ["Advanced"] = "Nâng cao",
            ["Server"] = "Máy chủ MCP",
            ["Transport:"] = "Giao thức kết nối:",
            ["Use HTTP Local when multiple AI clients need to share one Unity Editor; stdio is intended for one client."] = "Chọn HTTP Local để nhiều AI client cùng dùng một Unity Editor; stdio phù hợp khi chỉ dùng một client.",
            ["Automatically start the local HTTP server or connect to the configured remote HTTP server when Unity opens. Stdio servers are started by the AI client. This setting takes effect the next time Unity starts."] = "Tự bật máy chủ HTTP nội bộ hoặc kết nối máy chủ HTTP từ xa khi mở Unity. Với stdio, AI client sẽ tự khởi chạy máy chủ. Cài đặt có hiệu lực từ lần mở Unity tiếp theo.",
            ["Manual Server Launch"] = "Khởi chạy máy chủ thủ công",
            ["Use this command to launch the server manually:"] = "Chạy lệnh này trong terminal để khởi động máy chủ thủ công:",
            ["HTTP URL:"] = "Địa chỉ HTTP:",
            ["API Key:"] = "Khóa API:",
            ["Get API Key"] = "Lấy khóa API",
            ["Local Server:"] = "Máy chủ trên máy này:",
            ["Start Server"] = "Bật máy chủ",
            ["Stop Server"] = "Tắt máy chủ",
            ["Unity Socket Port:"] = "Cổng kết nối Unity:",
            ["Disconnected"] = "Chưa kết nối",
            ["Connected"] = "Đã kết nối",
            ["Start"] = "Bắt đầu",
            ["Stop"] = "Dừng",
            ["Start Session"] = "Bắt đầu phiên",
            ["End Session"] = "Kết thúc phiên",
            ["Disconnect"] = "Ngắt kết nối",
            ["No Session"] = "Chưa có phiên",
            ["Resuming..."] = "Đang kết nối lại...",
            ["Checking..."] = "Đang kiểm tra...",
            ["Transport Mismatch"] = "Sai giao thức kết nối",
            ["Auto-start MCP on Unity startup:"] = "Tự bật MCP khi mở Unity:",
            ["Client Configuration"] = "Kết nối AI",
            ["Configure All Detected Clients"] = "Thiết lập các AI đã tìm thấy",
            ["Per-client setup"] = "Thiết lập từng AI",
            ["Client:"] = "AI client:",
            ["Not Configured"] = "Chưa thiết lập",
            ["Configure"] = "Thiết lập",
            ["Install Skills"] = "Cài bộ kỹ năng Novahn",
            ["Install Novahn Skills"] = "Cài bộ kỹ năng Novahn",
            ["Manual Configuration"] = "Cấu hình thủ công",
            ["Config Path:"] = "Đường dẫn cấu hình:",
            ["Configuration:"] = "Cấu hình:",
            ["Installation Steps:"] = "Các bước cài đặt:",
            ["Copy"] = "Sao chép",
            ["Open"] = "Mở",
            ["Browse"] = "Duyệt",
            ["Clear"] = "Xóa",
            ["Select"] = "Chọn",
            ["System Requirements"] = "Yêu cầu hệ thống",
            ["MCP for Unity requires Python 3.10+ and UV package manager to function. Git is only needed to install or update the package from a Git URL."] = "Novahn MCP cần Python 3.10+ và trình quản lý gói uv. Chỉ cần Git khi cài hoặc cập nhật package bằng Git URL.",
            ["Python"] = "Python",
            ["Git (optional)"] = "Git (không bắt buộc)",
            ["UV Package Manager"] = "Trình quản lý gói uv",
            ["Installation Instructions"] = "Hướng dẫn cài đặt",
            ["Install UV Automatically"] = "Cài uv tự động",
            ["Installing UV…"] = "Đang cài uv…",
            ["Installing uv… this can take a moment."] = "Đang cài uv… quá trình này có thể mất một lúc.",
            ["Open Python Install Page"] = "Mở trang cài Python",
            ["Open UV Install Page"] = "Mở trang cài uv",
            ["AI Asset Generation"] = "Tạo tài nguyên bằng AI",
            ["Script Validation"] = "Kiểm tra mã nguồn",
            ["Validation Level:"] = "Mức kiểm tra:",
            ["Project-Scoped Tools:"] = "Công cụ theo dự án:",
            ["Core Tools"] = "Công cụ chính",
            ["VFX & Shaders"] = "Hiệu ứng và shader",
            ["Animation"] = "Hoạt ảnh",
            ["UI Toolkit"] = "Giao diện UI Toolkit",
            ["Scripting Extensions"] = "Công cụ lập trình bổ sung",
            ["Testing"] = "Kiểm thử",
            ["ProBuilder — Experimental"] = "ProBuilder — thử nghiệm",
            ["Profiling & Frame Debugger"] = "Phân tích hiệu năng và Frame Debugger",
            ["Asset Gen"] = "Tạo tài nguyên",
            ["Custom Tools"] = "Công cụ tùy chỉnh",
            ["Built-in Resources"] = "Tài nguyên tích hợp",
            ["Custom Resources"] = "Tài nguyên tùy chỉnh",
            ["Discovering tools..."] = "Đang tìm công cụ...",
            ["Discovering resources..."] = "Đang tìm tài nguyên...",
            ["Enable All"] = "Bật tất cả",
            ["Disable All"] = "Tắt tất cả",
            ["Rescan"] = "Quét lại",
            ["Reconfigure Clients"] = "Cấu hình lại AI client",
            ["Changes apply after reconnecting or re-registering tools."] = "Thay đổi có hiệu lực sau khi kết nối lại hoặc đăng ký lại công cụ.",
            ["Changes apply after reconnecting or re-registering resources."] = "Thay đổi có hiệu lực sau khi kết nối lại hoặc đăng ký lại tài nguyên.",
            ["Advanced Settings"] = "Cài đặt nâng cao",
            ["UVX Path:"] = "Đường dẫn uvx:",
            ["Server Source:"] = "Nguồn máy chủ:",
            ["Debug Logging:"] = "Ghi log gỡ lỗi:",
            ["Log Record (Assets/mcp.log):"] = "Ghi log (Assets/mcp.log):",
            ["Server Health:"] = "Trạng thái máy chủ:",
            ["Force Fresh Install:"] = "Cài mới hoàn toàn:",
            ["Allow LAN Bind (HTTP Local):"] = "Cho phép kết nối mạng LAN (HTTP Local):",
            ["Allow Insecure Remote HTTP:"] = "Cho phép HTTP từ xa không mã hóa:",
            ["Screenshots Folder:"] = "Thư mục ảnh chụp:",
            ["Package Source:"] = "Nguồn package:",
            ["Addon Socket:"] = "Cổng kết nối addon:",
            ["Test Connection"] = "Kiểm tra kết nối",
            ["Sync Addon"] = "Đồng bộ addon",
            ["Check Updates"] = "Kiểm tra cập nhật",
            ["Import Blender Selection (GLB)"] = "Nhập mục Blender đã chọn (GLB)",
            ["Preferences"] = "Tùy chọn",
            ["Default 3D Format:"] = "Định dạng 3D mặc định:",
            ["Output Root:"] = "Thư mục đầu ra:",
            ["Auto-Normalize Imported Models:"] = "Tự chuẩn hóa model đã nhập:",
            ["Refresh"] = "Làm mới",
            ["Next"] = "Tiếp tục",
            ["Configure MCP Clients"] = "Thiết lập AI client",
            ["Skip"] = "Bỏ qua",
            ["Configure Selected"] = "Thiết lập mục đã chọn",
            ["✓ All requirements met! MCP for Unity is ready to use."] = "✓ Đã đủ yêu cầu. Novahn MCP sẵn sàng sử dụng.",
            ["⚠ Missing dependencies. MCP for Unity requires all dependencies to function."] = "⚠ Thiếu thành phần cần thiết để Novahn MCP hoạt động.",
            ["Not Found"] = "Không tìm thấy",
            ["Unknown"] = "Chưa rõ",
            ["Available"] = "Sẵn sàng",
            ["Not available"] = "Không khả dụng",
            ["No MCP tools discovered."] = "Chưa tìm thấy công cụ MCP nào.",
            ["No MCP resources discovered."] = "Chưa tìm thấy tài nguyên MCP nào.",
            ["No MCP tools found. Add classes decorated with [McpForUnityTool] to expose tools."] = "Chưa có công cụ MCP. Thêm lớp có gắn [McpForUnityTool] để cung cấp công cụ.",
            ["No MCP resources found. Add classes decorated with [McpForUnityResource] to expose resources."] = "Chưa có tài nguyên MCP. Thêm lớp có gắn [McpForUnityResource] để cung cấp tài nguyên.",
            ["No custom resources detected in loaded assemblies."] = "Không tìm thấy tài nguyên tùy chỉnh trong các assembly đã nạp.",
            ["Error"] = "Lỗi",
            ["Novahn Skills Installed"] = "Đã cài kỹ năng Novahn",
            ["Installed at:"] = "Đã cài tại:",
            ["Skill install failed"] = "Cài kỹ năng thất bại",
            ["OK"] = "Đóng",
        };

        public static string CurrentLanguage => EditorPrefs.GetString(LanguageKey, "en") == "vi" ? "vi" : "en";
        public static event System.Action LanguageChanged;

        public static string CurrentDisplayName => CurrentLanguage == "vi" ? "Tiếng Việt" : "English";

        public static void SetLanguage(string language)
        {
            string next = language == "vi" ? "vi" : "en";
            if (next == CurrentLanguage) return;
            EditorPrefs.SetString(LanguageKey, next);
            LanguageChanged?.Invoke();
        }

        public static string Translate(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (CurrentLanguage == "vi")
            {
                const string activeSessionPrefix = "Session Active (";
                if (value.StartsWith(activeSessionPrefix, System.StringComparison.Ordinal))
                    return "Phiên đang chạy (" + value.Substring(activeSessionPrefix.Length);

                int countSuffixIndex = value.LastIndexOf(" (", System.StringComparison.Ordinal);
                if (countSuffixIndex > 0 && value.EndsWith(")", System.StringComparison.Ordinal)
                    && Vietnamese.TryGetValue(value.Substring(0, countSuffixIndex), out var translatedPrefix))
                    return translatedPrefix + value.Substring(countSuffixIndex);

                return Vietnamese.TryGetValue(value, out var translation) ? translation : value;
            }

            const string vietnameseSessionPrefix = "Phiên đang chạy (";
            if (value.StartsWith(vietnameseSessionPrefix, System.StringComparison.Ordinal))
                return "Session Active (" + value.Substring(vietnameseSessionPrefix.Length);

            int vietnameseCountSuffixIndex = value.LastIndexOf(" (", System.StringComparison.Ordinal);
            if (vietnameseCountSuffixIndex > 0 && value.EndsWith(")", System.StringComparison.Ordinal))
            {
                string vietnamesePrefix = value.Substring(0, vietnameseCountSuffixIndex);
                foreach (var pair in Vietnamese)
                {
                    if (pair.Value == vietnamesePrefix)
                        return pair.Key + value.Substring(vietnameseCountSuffixIndex);
                }
            }

            foreach (var pair in Vietnamese)
            {
                if (pair.Value == value)
                    return pair.Key;
            }

            return value;
        }

        public static void Apply(VisualElement root)
        {
            if (root == null)
                return;

            root.Query<Label>().ForEach(label => label.text = Translate(label.text));
            root.Query<Button>().ForEach(button => button.text = Translate(button.text));
            root.Query<ToolbarToggle>().ForEach(toggle => toggle.text = Translate(toggle.text));
            root.Query<Foldout>().ForEach(foldout => foldout.text = Translate(foldout.text));
            root.Query<Toggle>().ForEach(toggle => toggle.label = Translate(toggle.label));
            root.Query<TextField>().ForEach(field => field.label = Translate(field.label));
            root.Query<DropdownField>().ForEach(field => field.label = Translate(field.label));
        }
    }
}
