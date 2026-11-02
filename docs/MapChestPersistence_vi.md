# Rương khám phá map chỉ nhận thưởng một lần

Áp dụng cho `PROP_TREASURE_CHEST` trên các plane `Town`, `Maze`, `Train`. Mỗi rương được định danh theo UID người chơi và bộ `FloorId / GroupId / PropId`; không dùng `ChestID` của bảng thưởng làm khóa vì nhiều rương có thể dùng chung bảng thưởng.

`ChestOpened` được lưu trong JSON `Scene.ScenePropData` hiện có, không cần thêm cột database hay sửa protobuf. Khi mở rương hợp lệ, server ghi nhận trạng thái, cấp thưởng, xóa đánh dấu map và gọi lưu dữ liệu người chơi ngay. Rương đã mở luôn trả `ChestUsed`, kể cả khi giải đố cố đóng lại rương hoặc nhóm dùng `SaveType.Reset`. Trạng thái của rương chưa mở trong nhóm Reset vẫn reset như trước.

Các yêu cầu mở rương map của cùng người chơi được tuần tự hóa để không cấp thưởng trùng. Thiếu cấu hình rương hoặc item thưởng thì giữ rương chưa mở. Loader đọc lại trạng thái khi tải map; bản lưu cũ có `ChestUsed` được bổ sung cờ `ChestOpened`. Những rương đã mở trước đây nhưng chưa từng được lưu không có dữ liệu để khôi phục lịch sử.

Rương instance như Raid, Rogue, Challenge không được áp dụng cờ mở vĩnh viễn. Dọn dữ liệu raid giữ lại cờ rương map trên floor dùng chung.

Phần thưởng vẫn theo `DropService` hiện có, chưa xác nhận khớp từng rương của game gốc. Cơ chế lưu dùng `SaveUidData` hiện tại, chưa có transaction bao trùm trạng thái rương và tất cả bảng inventory; chưa bảo đảm tính nguyên tử khi database lỗi, tiến trình bị kill giữa lúc lưu hoặc việc cấp thưởng bị gián đoạn. Kiểm thử không thay thế việc kiểm tra trực tiếp animation và luồng giải đố bằng client 4.2.

Kiểm thử: `tests/ChestRegression` có 11 tình huống độc lập và một tình huống tùy chọn dùng `Resources` thật. Xem README của project test để chạy lại.
