# Proxy cho client 4.2

Nếu launcher đã redirect về server, dùng launcher đó; không bật thêm Local Capture cùng lúc. Bộ proxy dưới đây là tùy chọn cho launcher/client cần proxy bên ngoài, không tự vá khóa hoặc kiểm tra chứng chỉ trong client.

Cài mitmproxy rồi chạy `Start-Proxy.bat`. Mặc định dùng Local Capture cho `StarRail.exe` và mở thêm HTTP proxy thử nghiệm tại `127.0.0.1:18080`. Dừng bằng Ctrl+C. Không thay system proxy, không tự cài CA, không sửa client. HTTPS yêu cầu client tin CA mitmproxy; tài liệu [certificate](https://docs.mitmproxy.org/stable/concepts/certificates/) và [Local Capture](https://docs.mitmproxy.org/stable/concepts/modes/).

Với launcher đã chỉ định HTTP proxy, chạy regular mode ở cổng trống và nhập cùng cổng vào launcher:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Proxy.ps1 -Mode regular -Port 18080
```

Đích mặc định là `http://127.0.0.1:21000`; đổi bằng `-ServerUrl`. Có thể truyền `-MitmdumpPath` nếu không cài ở Program Files hoặc PATH. Local Capture có thể cần quyền Windows cho trình redirector.

Script chỉ redirect đường dispatch/gateway, SDK Star Rail và các API dùng chung đã liệt kê dưới domain HoYoverse/miHoYo/StarRails/BHSR. Giữ path, query và body; sửa Host về server đích để gateway không trỏ về host chính thức. Các đường CDN không bị rewrite. Dùng `connection_strategy=lazy` và `upstream_cert=false` để request được redirect trước khi kết nối upstream. Log riêng chỉ ghi method/host/path, không ghi body hoặc query. Không lưu flow ra file.

Đã kiểm tra HTTPS dispatch trả gateway `127.0.0.1:21000`, SDK loadConfig và từ chối login rỗng qua mitmproxy 12.3.1. Chưa xác nhận vào gameplay qua Local Capture; hiện người dùng sử dụng launcher riêng.

## Hộp thoại trống và proxy redirect toàn bộ domain

Trong log client ngày 2026-11-07, script ngoài repo chuyển cả `webstatic.mihoyo.com/admin/mi18n/...` về Kestrel, khiến hai file version ngôn ngữ trả 404. `/query_security_file` cũng bị chuyển nhầm và trả 404. Kiểm tra trực tiếp cùng các URL gốc đều nhận HTTP 200. Thiếu tài nguyên ngôn ngữ có thể làm hộp thoại lỗi mất chữ; chưa đủ bằng chứng để kết luận đó là nguyên nhân duy nhất khiến game không vào được.

Dừng proxy cũ rồi chạy `Start-Proxy.bat` trong repo để dùng addon chọn lọc. Không ép `hkrpg_cn` thành `hkrpg_global` hoặc đổi `zh-cn` thành `en-us` bằng proxy: việc sửa các chuỗi đó không chuyển đổi SDK của client. Chế độ một người chơi xử lý cả route CN/Global hiện có mà không cần giải mã thông tin đăng nhập gửi lên.

Kiểm thử addon bằng bản mitmproxy đã cài:

```powershell
mitmdump --set server=false --set flow_detail=0 -s tests/ProxyRegression/check_addon.py
```

11/11 tình huống qua: chuyển API cần thiết, giữ nguyên hai URL ngôn ngữ thực tế trong log, security file, CDN, game khác, domain giả; giữ body/query/vùng/ngôn ngữ, sửa Host về backend, loại Alt-Svc và kiểm tra origin cấu hình. Các test này không khởi động listener hoặc gửi thông tin đăng nhập ra mạng.
