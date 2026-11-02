# Build server standalone cho Windows x64

Chạy `Build-Standalone.bat` tại root repository. Cần .NET 10 **SDK** trên máy build và bộ dữ liệu `Resources/ExcelOutput`, `Resources/Config` đầy đủ. Script tự tìm SDK trong PATH và vị trí SDK cục bộ; có thể đặt biến `MARCH7TH_DOTNET` hoặc truyền `-DotnetPath "C:\path\dotnet.exe"` để chỉ định.

Kết quả nằm trong `artifacts/standalone/win-x64/`:

- `March7thHoney.exe`: chương trình server.
- `Start-Server.bat`: chạy server với working directory đúng.
- Các DLL/runtime .NET và thư viện native SQLite đi kèm.
- `Resources/`: bản sao resource hiện có.
- `Config/`: các dữ liệu mặc định cần lúc chạy.

Chạy `Start-Server.bat` hoặc mở `March7thHoney.exe` trong thư mục output. Bản này **self-contained**, máy chạy không cần cài .NET SDK/runtime. Phải giữ/copy cả thư mục, không chỉ riêng file EXE. Không trimming vì server dùng reflection, resource loader và handler registration.

Lần chạy đầu server tự tạo `Config.json` và database trong thư mục output. Script không chép database, log, cache resource hoặc `Config.json` cá nhân từ source, nên đây là một instance riêng. Nếu muốn dùng dữ liệu cũ, dừng server rồi chủ động chuyển cấu hình/database phù hợp; kiểm tra lại đường dẫn và cổng trước khi chạy.

Build lại vào cùng output giữ nguyên `Config.json`, database và các file cấu hình đã tồn tại; cập nhật binary và chép resource bổ sung/thay đổi, không dùng thao tác xóa/mirror. Dừng server trước khi build lại để tránh file EXE/DLL bị khóa. Nếu resource thay đổi, tắt `ServerOption.UseCache` cho lần chạy kế tiếp để tái tạo dữ liệu runtime.

`March7thHoney.exe --version` kiểm tra EXE/runtime mà không mở server hoặc ghi database. Script chạy kiểm tra này sau publish. Khi chạy tự động, đặt `MARCH7TH_NO_PAUSE=1` để batch không chờ phím.

Resource và toàn bộ output `artifacts/` bị ignore bởi Git. Repository lưu source và script build; bản EXE được tạo cục bộ, không commit các binary/resource vào Git.
