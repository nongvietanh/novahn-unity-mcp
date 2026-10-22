---
name: novahn-unity
description: Hướng dẫn AI thao tác an toàn, gọn và hiệu quả với Unity Editor qua Novahn MCP.
---

# Novahn Unity

Trả lời bằng tiếng Việt, trừ khi người dùng muốn ngôn ngữ khác. Dùng tên tool và API đúng như MCP cung cấp; không tự dịch tên kỹ thuật.

## Cách làm việc

1. Đọc trạng thái Editor qua `mcpforunity://editor/state` trước khi thao tác. Nếu có nhiều Unity Editor, đọc `mcpforunity://instances` và chọn đúng instance bằng `set_active_instance`.
2. Tìm đối tượng, scene hoặc asset trước khi sửa. Ưu tiên tool MCP chuyên dụng thay vì chạy C# tùy ý.
3. Gộp các thao tác độc lập bằng `batch_execute`; tách các bước phụ thuộc để kiểm tra kết quả giữa chừng.
4. Sau khi sửa script, đợi Unity biên dịch xong rồi đọc Console để tìm lỗi. Chỉ chạy test khi người dùng yêu cầu hoặc khi quy trình dự án bắt buộc.
5. Báo ngắn gọn các thay đổi và kết quả kiểm tra; hỏi trước nếu yêu cầu chưa nói rõ đối tượng hoặc phạm vi có thể gây mất dữ liệu.

## Chọn tool

- Scene và GameObject: `manage_scene`, `find_gameobjects`, `manage_gameobject`.
- Component: `manage_components`.
- Asset và prefab: `manage_asset`, `manage_prefabs`.
- Script: `manage_script`, `validate_script`.
- Unity Editor: `manage_editor`, `read_console`, `execute_menu_item`.
- Test: `run_tests`, `get_test_job`.

Đọc `mcpforunity://project/info` trước khi dùng package hoặc framework tùy chọn. Dùng `unity_docs` hoặc `unity_reflect` để kiểm tra API khi chưa chắc về phiên bản Unity hay chữ ký hàm.
