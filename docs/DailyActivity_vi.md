# Nhiệm vụ daily và thưởng huấn luyện

Server đã thay bảng daily giả 500 điểm/đã nhận hết thưởng bằng dữ liệu riêng cho từng UID. Tiến độ, điểm, mốc đã nhận, cấp nhiệm vụ, World Level tính thưởng và nhiệm vụ NPC được lưu trong bảng `daily_activity`; tiến độ từng quest vẫn dùng `quest_data`.

## Luồng hoạt động

- Đọc điều kiện cấp độ, nhiệm vụ mở khóa và phần thưởng từ `DailyQuest`, `DailyMissionData`, `DailyActiveConfig`, `QuestData`, `FinishWay`, `RewardData`.
- Giao một nhiệm vụ NPC daily đủ điều kiện. Khi hết ngày, chỉ đặt lại trạng thái/progress của nhiệm vụ daily được giao; giữ nguyên cốt truyện và thành tựu khác.
- Có bộ mục tiêu huấn luyện cho cấp 10–35 và từ cấp 36: đăng nhập, phái đi/nhận thưởng thám hiểm, chiến đấu, dùng hỗ trợ, tổng hợp, nhiệm vụ NPC; cấp cao có thêm tiêu hao 120 năng lượng. Mục tiêu `2100156` nhận sự kiện hoàn thành quest liên quan hoặc thắng lượt Currency War cuối cùng.
- Đăng nhập chỉ hoàn thành mục tiêu đăng nhập. Nhận thưởng quest mới cộng điểm huấn luyện (item 23); item này không được thêm nhầm vào túi đồ. Điểm tối đa bằng mốc cao nhất trong bảng cấu hình.
- Nhận từng mốc qua `TakeApReward` hoặc các mốc đủ điểm chưa nhận qua `TakeAllApReward`. Dùng phần thưởng theo World Level lúc khởi tạo bảng ngày đó. Yêu cầu lặp/đồng thời không cấp lại thưởng.
- Nộp đồ kiểm tra đúng loại nhiệm vụ đang chạy và đủ vật phẩm; tổng hợp kiểm tra nguyên liệu trước khi ghi nhận daily.
- Đồng bộ cả progress và thời gian hoàn thành quest khi đăng nhập lại; heartbeat kiểm tra reset lúc đang online.
- Cache resource cũ thiếu các bảng daily mới được đánh dấu không hợp lệ để server tải lại từ `Resources`.

## Cấu hình

Trong `ServerOption` của `Config.json`:

```json
{
  "EnableQuest": true,
  "EnableMission": true,
  "DailyActivity": {
    "UtcOffsetHours": 8,
    "ResetHour": 4
  }
}
```

Đây là các trường cần ghép vào cấu hình hiện có, không phải toàn bộ file cấu hình. Mặc định reset 04:00 UTC+8, tương đương 03:00 giờ Việt Nam/Thái Lan. Muốn reset 04:00 giờ Việt Nam thì đặt `UtcOffsetHours` thành `7`. Khi tắt `EnableMission`, server không giao mục tiêu/NPC daily cốt truyện. Khi tắt `EnableQuest`, daily không cấp thưởng.

Đồng hồ dùng `ServerTimeProvider`, nên cấu hình fake server time cũng ảnh hưởng daily. Quay đồng hồ lùi không tạo thêm lần reset. Thay đổi cấp độ giữa ngày không tạo thêm bộ mục tiêu khác để nhận thưởng lặp; nhánh cấp độ mới áp dụng khi reset tiếp theo.

## Kiểm thử và giới hạn

`dotnet run --project tests/DailyRegression -c Release -- Resources`: 16/16 tình huống qua, dùng resource thật và SQLite tạm. Bao gồm một chuỗi hội thoại NPC thật `3000201`, nhận thưởng đồng thời, lưu/đọc lại, ranh giới reset, dữ liệu legacy, nhánh cấp thấp, nguyên liệu và nộp vật phẩm. Test cốt truyện 6/6, rương 11 + 1 resource và GridFight 15/15 cũng qua. Build Release solution không lỗi; còn cảnh báo dependency/nullability hiện có.

Bộ mục tiêu huấn luyện đang dùng danh sách lựa chọn đã nối sự kiện server, không mô phỏng toàn bộ thuật toán xoay 52 mục tiêu của game gốc. NPC có loại bước `InteractPropList`/`UseSelectedItem` chưa có luồng hoàn thành phù hợp sẽ không được chọn; các loại bước được chọn gồm hội thoại, đếm bước hoàn thành, nộp đồ, thắng trận, diệt quái, tin nhắn và vào floor. Chưa chạy toàn bộ NPC/cutscene bằng client 4.2 hoặc kiểm thử MySQL. Chế độ Rogue chỉ đóng góp khi hệ thống đó phát sự kiện hoàn thành quest tương ứng; đây không phải bản triển khai thêm toàn bộ chế độ Rogue.

Lưu thưởng dùng `SaveUidData` hiện có, chưa có transaction bao trùm mọi bảng và mọi callback. Kiểm thử chống yêu cầu trùng không đồng nghĩa bảo đảm nguyên tử khi database/tiến trình lỗi giữa lúc cấp thưởng. Không xóa hoặc reset database người chơi trong quá trình triển khai và kiểm thử.
