# Sửa luồng cốt truyện mở đầu

Phạm vi đã kiểm thử là nhiệm vụ `1000101`: phần Kafka/Sói Bạc, bước chọn nhân vật và chuyển tiếp sang `1000201`. Chưa xác nhận toàn bộ chương Trạm Không Gian hoặc các chương sau bằng client 4.2.

## Thay đổi

- Cấu hình mới mặc định bật `ServerOption.EnableMission`. Giá trị trong `Config.json` đã tồn tại vẫn được tôn trọng.
- Đọc thông tin tài khoản không tự đánh dấu đã chọn nhân vật. Gói chọn giới tính chỉ xử lý trong bước `100010134` khi bật cốt truyện; yêu cầu lặp không thêm đội hình lần nữa.
- Chờ đầy đủ các bước khởi tạo nhiệm vụ, script, performance và thay đổi đội hình. Script của floor chạy sau khi gán map mới cho người chơi.
- Nhiệm vụ nhận/xóa nhân vật dùng thử kiểm tra đội hình thực tế. Script thiết lập được chạy trước khi xét hoàn thành.
- Thắng trận phải khớp màn của battle đang xử lý; tiêu diệt quái phải khớp group/floor. Yêu cầu nhận chương từ client phải thỏa điều kiện chương trước.
- Không tự hoàn thành main mission khi thiếu danh sách mục tiêu cuối. Gói đồng bộ phản ánh trạng thái thật sau khi các bước tự động kết thúc.
- Lưu dữ liệu khi hoàn thành bước/chương; đánh dấu tiến độ cần lưu. Lỗi script có log kèm loại task và mission ID thay vì bị nuốt im lặng.
- Sửa việc chờ kết quả tìm prop trong `PropStateExecute`. Khi bật cốt truyện, không báo tất cả nhiệm vụ của content package là đang chạy.

## Chạy thử bằng client

Với cấu hình hiện có, kiểm tra `ServerOption.EnableMission = true`. Nên dùng tài khoản mới để kiểm tra tuyến mở đầu; bản sửa không xóa hoặc đặt lại tiến độ tài khoản cũ.

Nếu vừa thay bộ `Resources`, đặt `ServerOption.UseCache = false` trong lần khởi động đầu để server đọc dữ liệu mới. Luồng khởi động hiện có sẽ xóa cache cũ khi tùy chọn này tắt; đây là cache resource, không phải database tài khoản. Có thể bật lại sau khi đã xác nhận dữ liệu đúng.

Kiểm tra trong game: Kafka vào trận đầu → mở cửa → Sói Bạc vào đội → các hội thoại và giao chiến → Sói Bạc rời đội → chọn nhân vật → Kafka rời đội → map tiếp theo và nhận `1000201`. Thử thoát/đăng nhập lại giữa các mốc; ghi mission ID và log nếu có đoạn kẹt.

## Kết quả và giới hạn

`StoryRegression`: 6/6 tình huống qua, gồm đồ thị nhiệm vụ thật và SQLite tạm. `ChestRegression`: 11 tình huống + 1 rương resource thật qua. `GridFightRegression`: 15/15 qua. Build Release solution: không có lỗi, còn cảnh báo dependency đã tồn tại.

Test cốt truyện cung cấp sự kiện client theo kịch bản, không điều khiển client hay mô phỏng toàn bộ combat/cutscene/map. Các script chưa có handler vẫn cần bổ sung khi phát hiện bằng client. Lưu mission dùng cơ chế database hiện có, chưa có transaction cho toàn bộ chuyển bước và phần thưởng khi tiến trình/database gặp lỗi giữa chừng.
