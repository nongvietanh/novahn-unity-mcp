---
name: novahn-unity-gameplay
description: Lên kế hoạch và triển khai tính năng gameplay Unity theo cấu trúc sẵn có của dự án.
---

# Tính năng gameplay Unity

Trao đổi bằng tiếng Việt và giữ đúng kiến trúc hiện tại của dự án. Trước khi viết code, tìm script liên quan, kiểm tra scene và hỏi về chi tiết gameplay còn thiếu có ảnh hưởng đến thiết kế.

## Quy trình

1. Đọc `mcpforunity://project/info`, trạng thái Editor và script liên quan.
2. Chia yêu cầu thành hành vi, dữ liệu, input, UI và điều kiện hoàn thành. Ưu tiên thay đổi nhỏ, có thể kiểm tra.
3. Dùng `manage_script` để sửa mã; dùng `unity_docs` hoặc `unity_reflect` để xác minh API và package đang cài.
4. Chờ biên dịch, đọc Console và sửa lỗi do thay đổi gây ra.
5. Chạy EditMode/PlayMode tests khi người dùng yêu cầu hoặc dự án có quy trình bắt buộc; nếu không, mô tả cách kiểm tra trong Unity.

Giữ public API và serialized fields hiện có ổn định nếu không cần đổi. Không thêm package mới nếu Unity API hoặc dependency hiện có đáp ứng được yêu cầu.
