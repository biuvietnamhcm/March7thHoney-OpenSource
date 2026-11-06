# Warp 4.2: cơ chế, dữ liệu và kiểm thử

## Đã triển khai

- Pity riêng cho Stellar, Departure, Character Event, Light Cone Event, Character Collaboration và Light Cone Collaboration. Các banner cùng nhóm chia sẻ pity 4★/5★ và trạng thái bảo hiểm khi đổi banner.
- Tỷ lệ cơ bản 5★ nhân vật/thường 0,6%, nón 0,8%; hard pity tương ứng 90/80. Tỷ lệ 4★ cơ bản 5,1%/6,6%; bảo đảm 4★ trở lên trong 10 lượt. Nếu lượt đó là 5★, bảo hiểm 4★ còn chờ đến lượt kế tiếp.
- Nhánh rate-up 5★ 50% cho nhân vật, 75% cho nón; kết quả không phải rate-up bật bảo hiểm cho lần 5★ kế tiếp. Có bảo hiểm rate-up 4★ tương tự. Pool 4★ được lọc rarity; không còn 5★ trong danh sách 4★. Nón shop/Battle Pass/Herta/event không nằm trong pool thường.
- Celestial Invitation chọn đúng 7 ID khác nhau trong 13 ID từ `DecideAvatarOrder`. Lựa chọn áp dụng cho Character Event/Collaboration, lưu qua reconnect; không thay pool 7 nhân vật của Stellar/Departure.
- Departure: chỉ gói 10 lượt, tốn 8 vé, tổng cộng 50 lượt; bảo đảm ít nhất một nhân vật 5★. Nếu đã có 5★ sớm thì không ép thêm ở lượt 50. Dùng pool 4★ ban đầu và đóng banner khi hết lượt.
- Stellar: đếm tổng lượt độc lập pity; mốc 300 chọn một trong 7 nhân vật chuẩn, chỉ nhận một lần. Đã thêm handler GetGachaCeiling/ExchangeGachaCeiling.
- Nhân vật trùng: kiểm tra cả Eidolon đã nâng và token chưa dùng. 4★ nhận 8 Starlight + token, vượt E6 nhận 20; 5★ nhận 40 + token, vượt E6 nhận 100 + 1 Silver Companion Spirit (282). Nón 4★/5★ nhận 8/40 Starlight, 3★ nhận 20 Embers.
- Kiểm tra số lượt 1/10, banner, lịch, vé và chỗ chứa trước khi trừ vé. Khóa theo người chơi chống tiêu vé/nhận mốc trùng khi gọi đồng thời. Lưu pity, vé, phần thưởng và lịch sử sau mỗi request; không còn xóa lịch sử xuống 50 dòng khi đăng nhập.

## Điểm cần phân biệt với thuật toán gốc

Soft pity 5★ dùng ramp mô phỏng: tăng từ lượt 74 (nhân vật) và 64 (nón). `UseSoftPity: false` tắt ramp nhưng giữ hard pity. Đường cong chính xác, cơ chế cân bằng loại vật phẩm trong banner thường và soft pity 4★ chưa được xác minh; hiện 4★ dùng base rate và hard guarantee.

`IncludeFeaturedInFallback` mặc định true: sau nhánh 50%/75% không thắng trực tiếp, pool dự phòng gồm 7 mục tiêu thường/được chọn cộng thêm một mục tiêu rate-up. Với một featured không trùng Celestial, xác suất featured trên một 5★ chưa được bảo hiểm là 56,25%/78,125%. Nếu featured cũng nằm trong 7 lựa chọn thì giữ cả hai entry. Đây là cách mô phỏng suy ra để khớp mô tả tỷ lệ tổng hợp 1,113%/1,534% trong `GachaBasicInfo` + TextMap của resource 4.2; không phải xác nhận đã biết mã RNG server gốc. Có thể đặt false để dùng nhánh 50%/75% thuần.

Các field protobuf bị obfuscate cho Celestial được sử dụng theo cấu trúc hiện có, cần kiểm tra bằng client thật. Handler và payload đã có nhưng chưa xác nhận toàn bộ giao diện/counter/animation của client 4.2. Lịch sử được lưu đầy đủ phía server; chưa có trang web history/details để mở từ UI. Chưa mô phỏng lượt quay hướng dẫn bắt buộc hoặc toàn bộ shop Stellar Convergence.

Các mốc 4.2 trong `Banners.json` giữ lịch gốc; ngoài thời gian này banner không hiển thị và request bị từ chối. Muốn thử bản 4.2 ở ngày khác, dùng cấu hình thời gian server hiện có hoặc chỉnh begin/end trong cấu hình banner cục bộ. Chú ý fake server time hiện cố định cả ngày, nên cũng ảnh hưởng reset daily.

## Dữ liệu cũ

Các trường pity cũ được giữ. Lần nâng cấp đầu: bộ đếm chung được gán cho nhóm của banner cuối cùng trong lịch sử, không nhân bản sang mọi banner. Cờ bảo hiểm nhân vật/nón được chuyển sang nhóm event tương ứng. Nếu không còn lịch sử đủ dài, không thể khôi phục chính xác pity riêng và tổng 300 từ dữ liệu đã mất. Chỉ số tổng khởi tạo từ phần lịch sử còn lại; việc di trú chỉ chạy một lần.

Khóa chống yêu cầu đồng thời không phải transaction nguyên tử bao trùm callback/DB: `SaveUidData` vẫn dùng cơ chế lưu của repo. Không bảo đảm khôi phục hoàn toàn nếu tiến trình/database lỗi giữa lúc cấp thưởng. Không reset database người chơi trong quá trình phát triển/kiểm thử.

Các cột JSON trên MySQL dùng `LONGTEXT` để tránh giới hạn kích thước hàng và lịch sử dài bị cắt. Khởi tạo schema báo lỗi rõ bảng thay vì bỏ qua lỗi rồi chạy với bảng thiếu. Đã kiểm thử nâng cấp cột lịch sử từ `TEXT` mà giữ nguyên dữ liệu trong database thử; chưa chạy migration trên database người chơi.

## Kiểm thử

`dotnet run --project tests/GachaRegression -c Release -- Resources`

18/18 tình huống trên cả SQLite riêng và MySQL 8.4.11 riêng, với resource thật và RNG điều khiển được: pool/rarity, pity 90/80/10, mất/giữ bảo hiểm, nhóm banner, thiếu vé/request sai, lịch, quay đồng thời, 50 lượt Departure, E6/token/Spirit, Celestial 7/13, selector 300, reconnect/history, di trú cũ, kho đầy, nâng cấp cột JSON và lưu 4.000 lượt quay (vượt 64 KiB). Không mở server hoặc truy cập database người chơi.

## Nguồn đối chiếu

- Resource người dùng cung cấp: `GachaBasicInfo`, `GachaTypeBasicInfo`, `GachaCeiling`, `DecideAvatarOrder`, `GachaNews`, các bảng Avatar/Equipment/Item và TextMapEN.
- [Thông báo cập nhật 4.2 của Honkai: Star Rail](https://www.hoyolab.com/article/44742273): mở rộng Celestial Invitation lên 7/13 và áp dụng cho Collaboration.
- TextMapEN hash `1802234304168954221`: quy tắc nhận Silver Companion Spirit khi có thêm nhân vật 5★ vượt Eidolon tối đa, từ 3.2.
- TextMap của banner 2116/3116 trong resource 4.2: tỷ lệ tổng hợp featured 1,113%/1,534%; banner 4001 mô tả giảm 20% và ít nhất một nhân vật 5★ trong 50 lượt.
