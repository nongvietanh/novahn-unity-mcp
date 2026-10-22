---
name: novahn-unity-scene
description: Tạo và chỉnh sửa scene, GameObject, component và prefab trong Unity bằng Novahn MCP.
---

# Scene và GameObject trong Unity

Làm việc theo yêu cầu của người dùng bằng tiếng Việt. Trước khi sửa, kiểm tra scene đang mở và tìm đúng đối tượng. Giữ nguyên các object ngoài phạm vi yêu cầu.

## Quy trình

1. Đọc `mcpforunity://editor/state` và `mcpforunity://scene/gameobject-api`.
2. Dùng `find_gameobjects` để tìm object hiện có; xác nhận scene và parent trước khi tạo hoặc xóa.
3. Dùng `manage_scene`, `manage_gameobject` và `manage_components` cho thay đổi scene. Dùng `manage_prefabs` khi cần tạo hoặc chỉnh prefab.
4. Sau thay đổi, đọc lại hierarchy hoặc trạng thái object để xác nhận vị trí, parent và component.
5. Chụp ảnh khi bố cục hoặc hình ảnh scene là một phần của yêu cầu.

Khi có nhiều thao tác độc lập, dùng `batch_execute`. Không xóa scene, prefab hay asset nếu người dùng chưa chỉ rõ phạm vi.
