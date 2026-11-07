# Đăng nhập một người chơi

Trong `Config.json` của **server đang chạy**, thêm vào `WebSecurity`:

```json
"SinglePlayerLogin": {
  "Enabled": true,
  "Username": "biu",
  "AccountUid": 10001
}
```

Khởi động lại server. Các route đăng nhập Shield/Passport, verify phiên đã lưu và combo đều trả cùng tài khoản `biu` khi request đến từ loopback. Bạn có thể nhập bất kỳ tên/mật khẩu không rỗng nào để vượt kiểm tra form của client; server bỏ qua nội dung đó trong chế độ này. Không cần tài khoản HoYoverse. Tài khoản bị ban vẫn bị từ chối.

`AccountUid` là ràng buộc rõ ràng với save hiện có: nếu UID tồn tại thì sửa tên tài khoản, giữ nguyên UID và dữ liệu gameplay. Nếu muốn tạo/tìm theo tên, dùng `0`. Nếu tên đã thuộc UID khác, server báo lỗi thay vì ghi đè tài khoản. Mặc định tính năng tắt; tắt `Enabled` sẽ dùng lại xác thực thông thường, nên lúc đó cần tên/mật khẩu hợp lệ của server.

Chế độ này dành cho server cá nhân. Cổng HTTP và proxy nên chỉ bind `127.0.0.1`. Kiểm tra localhost dùng IP socket được lưu trước `ForwardedHeaders`; header `X-Forwarded-For` không thể tự bật chế độ này cho kết nối từ xa. Không dùng một reverse proxy công khai trên localhost phía trước chế độ này.

## Tương thích launcher 4.2

- Đã thêm HEAD cho `/query_dispatch` và `/query_gateway`, bên cạnh GET hiện có.
- Đã thêm phản hồi Passport cho `/account/ma-cn-session/app/exchange` và `/account/ma-session/app/exchange` **trong chế độ một người chơi**. Đây là phản hồi tương thích dùng cùng token nội bộ; chưa xác minh toàn bộ hợp đồng exchange của SDK chính thức. Ngoài chế độ này, route trả lỗi thay vì cấp token không xác thực.
- Passport thông thường từ chối ciphertext không giải mã được, thay vì tạo tài khoản có tên là ciphertext.
- Token vẫn được cấp/lưu và được game server kiểm tra; không bỏ xác thực UDP/KCP.

Tên đăng nhập đã sửa không chứng minh đã xử lý mọi lỗi hộp thoại trống/0%. Client, hotfix, launcher và protocol có thể có lỗi độc lập. Trong log đã cung cấp, GET dispatch/gateway và combo đã tới server; HEAD trả 405 và exchange trả 404. Khóa `ClientSecretKey.bin` của launcher trùng byte với `Config/ClientSecretKey.ec2b` của instance được kiểm tra.

## Nguồn tham khảo

Đã đọc trực tiếp source, không nhập nguyên module hoặc nới xác thực của mọi tài khoản:

- [LunarCore AppLoginHandler, commit 04b5de8](https://github.com/Melledy/LunarCore/blob/04b5de871c62895091c4d65243b2e03997e66f23/src/main/java/emu/lunarcore/server/http/handlers/AppLoginHandler.java): Passport chọn tên cố định từ cấu hình; là cơ sở tham khảo cho tùy chọn một người chơi.
- [DanhengServer UsernameLoginHandler, commit ae613e5](https://github.com/Mar7thLover/DanhengServer-OpenSource/blob/ae613e59c8995f8b41bd2779e48a281c9f4da6e3/WebServer/Handler/UsernameLoginHandler.cs): tìm/tạo tài khoản rồi cấp dispatch token. Không dùng cách chấp nhận trực tiếp tên chưa giải mã cho đăng nhập thông thường.

## Kiểm thử

`dotnet run --project tests/LoginRegression -c Release`

13 tình huống, HTTP Kestrel thật trên cổng tạm và SQLite riêng: mặc định tắt, HEAD/GET, UID cố định, cached login, exchange/combo, yêu cầu đồng thời, giả mạo forwarded IP, reconnect DB, RSA đúng/sai khóa, ban, tạo một lần và xung đột tên. Không sử dụng database người chơi hay dịch vụ đăng nhập chính thức.
