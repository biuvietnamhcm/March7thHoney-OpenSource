# Nhiệm vụ daily và thưởng huấn luyện

Server đã thay bảng daily giả 500 điểm/đã nhận hết thưởng bằng dữ liệu riêng cho từng UID. Tiến độ, điểm, mốc đã nhận, cấp nhiệm vụ, World Level tính thưởng và nhiệm vụ NPC được lưu trong bảng `daily_activity`; tiến độ từng quest vẫn dùng `quest_data`.

## Luồng hoạt động

- Đọc điều kiện cấp độ, nhiệm vụ mở khóa và phần thưởng từ `DailyQuest`, `DailyMissionData`, `DailyActiveConfig`, `QuestData`, `FinishWay`, `RewardData`.
- Giao một nhiệm vụ NPC daily đủ điều kiện. Khi hết ngày, chỉ đặt lại trạng thái/progress của nhiệm vụ daily được giao; giữ nguyên cốt truyện và thành tựu khác.
- Đọc `DailyActiveQuestPool`: dùng nhóm hiện đại 6–17 và nhóm nhiệm vụ NPC 1, mỗi nhóm chọn một mục tiêu đủ điều kiện cấp độ/mở khóa. Không trộn nhóm cũ 2–5 với nhóm mới. Mục tiêu đã xóa bị loại. Trong nhóm Rogue, chọn biến thể mới nhất còn hợp lệ (`2100156` trong resource 4.2).
- Lựa chọn được xác định từ UID/ngày/nhóm và lưu cố định trong ngày. Đăng nhập lại, nâng cấp hay mở khóa nhiệm vụ giữa ngày không đổi bảng; bảng cũ trước khi cập nhật được giữ tới lần reset kế tiếp.
- Nối sự kiện đăng nhập, thám hiểm, tiêu hao năng lượng, chiến đấu/điểm yếu/hỗ trợ, Calyx/Echo of War/Cavern/Stagnant Shadow, dùng bí kỹ có tiêu hao MP, phá vật thể, nâng cấp nhân vật/nón/di vật và tổng hợp. Chụp ảnh dùng báo cáo client cho đúng objective đã giao; không cho client tự hoàn thành mục tiêu chiến đấu hoặc NPC.
- Mục tiêu `2100156` nhận hoàn thành quest liên quan hoặc thắng lượt Currency War cuối cùng. Một trận Rogue riêng lẻ không tự được tính như hoàn thành cả lượt chơi.
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

`dotnet run --project tests/DailyRegression -c Release -- Resources`: 21/21 tình huống, dùng resource thật trên cả SQLite tạm và MySQL 8.4.11 riêng. Bao gồm NPC hội thoại `3000201`, tương tác vật thể `3011601`, chọn/nộp vật phẩm `3011605`, bảng xoay, đổi cấp độ/ngày, reconnect, phó bản đúng loại, ảnh client, chống nhận trùng và tiêu hao nguyên liệu.

`InteractPropList` kiểm tra floor, group, prop ID và lưu các prop đã tương tác để không đếm lặp. `UseSelectedItem` nhận đúng số ô lựa chọn (`ParamInt1`), kiểm tra vật phẩm thuộc danh sách và số lượng đúng, chỉ trừ lựa chọn đó rồi hoàn thành bước đang chạy. Hai loại này đã được đưa vào pool NPC hợp lệ.

52 dòng `DailyQuest` không có nghĩa cả 52 đều cần xuất hiện trong bảng 4.2: có nhóm cũ, mục tiêu đã xóa và biến thể theo cấp. Quy tắc lấy một mục tiêu/nhóm hiện đại và chọn biến thể Rogue mới là cách diễn giải dữ liệu client; seed/trọng số xoay của server gốc không công khai, nên chưa thể khẳng định giống hoàn toàn. Chưa kiểm thử UI/cutscene toàn bộ NPC bằng client 4.2. Repo chưa có đầy đủ engine Simulated/Divergent Universe; daily chỉ nhận sự kiện hoàn thành có thật từ chế độ đã triển khai, không tự tạo chiến thắng Rogue.

Lưu thưởng dùng `SaveUidData` hiện có, chưa có transaction bao trùm mọi bảng và mọi callback. Kiểm thử chống yêu cầu trùng không đồng nghĩa bảo đảm nguyên tử khi database/tiến trình lỗi giữa lúc cấp thưởng. Không xóa hoặc reset database người chơi trong quá trình triển khai và kiểm thử.
