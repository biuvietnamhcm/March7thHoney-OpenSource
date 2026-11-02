# Khảo sát nguồn bổ sung tính năng cho March7thHoney 4.2

Ngày kiểm tra: 2026-10-29. Đã đối chiếu GitHub API, cây source, commit và một số manager; chưa build hoặc kiểm thử gameplay của các nguồn bên ngoài. Có code không đồng nghĩa tính năng đã hoàn chỉnh.

## Mốc hiện tại

- Checkout: `d960c85336227cdbe47ee21e13aa19ab4a4b7262`, commit `4.2`.
- `Common/Util/GameConstants.cs`: `GAME_VERSION = "4.2.0"`.
- Có GridFight, TrainParty, TrainCakeCatch, DiceCombat; không nên coi các tên này là tính năng mới chỉ vì xuất hiện trong repo khác.
- Chưa có module gameplay `Rogue`, `ChessRogue`, `RogueTourn`, `RogueMagic`, `BoxingClub`, `FightActivity` tại `GameServer/Game`.
- `ServerSideProto/ProtoFile` chỉ có ChallengeData và GridFightData. Nguồn Danheng bên dưới cũng chỉ có hai schema này; các tính năng bổ sung chủ yếu nằm ở gameplay, packet handlers và database.

## 1. Ưu tiên: các thay đổi cùng nền 4.2

Nguồn: [Mar7thLover/March7thHoney-OpenSource, main](https://github.com/Mar7thLover/March7thHoney-OpenSource/tree/main).

Mốc đã kiểm tra: `ac0558a3640f1dc1c77354923d57a409903b71b3`. So với checkout hiện tại: ahead 6 commit, tổng cộng 32 file thay đổi. Không có thay đổi trong thư mục Proto hoặc ServerSideProto ở phép so sánh này.

[Diff từ checkout tới mốc khảo sát](https://github.com/Mar7thLover/March7thHoney-OpenSource/compare/d960c85336227cdbe47ee21e13aa19ab4a4b7262...ac0558a3640f1dc1c77354923d57a409903b71b3).

- [9ceed35](https://github.com/Mar7thLover/March7thHoney-OpenSource/commit/9ceed35e75d044d065e0abd6cdb19e5e8118ebaa): thay đổi nền sau snapshot ban đầu; cần đọc cùng chuỗi sửa GridFight, không dựa vào tên commit `2` để quyết định bỏ qua.
- [94239f1](https://github.com/Mar7thLover/March7thHoney-OpenSource/commit/94239f1969be329767bd24b8bc3ef4a37c471b2a): sửa ghép sao, danh sách xuất trận, shop/pool, ô bị chiếm và đồng bộ mua đồ; đồng thời thêm các thay đổi luật chơi.
- [966a3e4](https://github.com/Mar7thLover/March7thHoney-OpenSource/commit/966a3e47ec123aadb808fa6d04116a07837e9b59): sửa thẻ đặc quyền.
- [3be178c](https://github.com/Mar7thLover/March7thHoney-OpenSource/commit/3be178c8438ecb5116d8082b5bd122a2fd63fecd): sửa projector.
- Các thay đổi cộng đồng đã được nhập qua [PR #1](https://github.com/Mar7thLover/March7thHoney-OpenSource/pull/1).

Phạm vi phụ thuộc: `GameServer/Game/GridFight`, `GameServer/Game/Battle/BattleInstance.cs`, packet Recv/Send GridFight, `CommandGrid.cs`, và hai opcode PostSync mới trong `KcpSharp/CmdIds.cs`.

Cần chọn lọc trước khi tích hợp:

- Commit 94239f1 mở sẵn 13 vị trí xuất trận; đây là thay đổi gameplay, không đơn thuần sửa lỗi.
- Chính mô tả commit ghi nhận shop đặc biệt Cyrene và Head Player/GM console chưa hoạt động như mong muốn; không tính là tính năng hoàn chỉnh.
- Diff chung còn đổi mặc định `AutoCreateUser` và `EnvType`; cần tách khỏi phần sửa gameplay.
- Tương thích source có triển vọng cao vì cùng lịch sử 4.2, nhưng vẫn phải build và thử trên client 4.2.

## 2. Nguồn C# cho các mode hiện thiếu

Nguồn: [mikugirls/DanhengServer-OpenSource-3.7.0, clubfight](https://github.com/mikugirls/DanhengServer-OpenSource-3.7.0/tree/clubfight).

Mốc đã đọc: `c0ec30dd37896efcffa64a585e43a915f293a351`; GameConstants khai báo `3.7.0`, namespace `EggLink.DanhengServer`.

| Module | Bằng chứng trong source | Đánh giá tích hợp vào 4.2 |
|---|---|---|
| BoxingClub | Manager 455 dòng, Instance, handler cho match/start/chọn resonance/buff/lineup, model database | Phạm vi nhỏ hơn họ Rogue; ứng viên cho một mode mới đầu tiên. Cần kiểm tra settlement và lưu tiến độ. |
| Rogue | RogueManager 701 dòng, BaseRogueInstance, rooms, event, buff, miracle; có start/finish/score/reward | Nguồn logic Simulated Universe đáng khảo sát; phụ thuộc scene/battle/lineup/inventory khá rộng. |
| ChessRogue | Manager, instance, cell, dice, modifier và ChessRogueNousData | Nguồn cho Gold and Gears; nên port sau phần Rogue nền tảng. |
| RogueTourn | StartRogueTourn có tạo instance, đội hình và vào phòng; có rooms và Titan | Có logic Divergent Universe nhưng nhiều dữ liệu tiến độ/talent được dựng sẵn; chưa chứng minh gameplay đầy đủ. |
| RogueMagic | Manager, instance, rooms, adventure, scepter, magic units | Có logic Unknowable Domain; cần kiểm tra cùng hệ Rogue và giao thức 4.2. |
| FightActivity | Manager có snapshot dữ liệu | Chưa ưu tiên: ProcessEnterStage và ProcessTakeReward chỉ trả Retcode=0; OnBattleSettlement rỗng. |

Liên kết source cố định:

- [BoxingClub](https://github.com/mikugirls/DanhengServer-OpenSource-3.7.0/tree/c0ec30dd37896efcffa64a585e43a915f293a351/GameServer/Game/BoxingClub)
- [Rogue](https://github.com/mikugirls/DanhengServer-OpenSource-3.7.0/tree/c0ec30dd37896efcffa64a585e43a915f293a351/GameServer/Game/Rogue)
- [ChessRogue](https://github.com/mikugirls/DanhengServer-OpenSource-3.7.0/tree/c0ec30dd37896efcffa64a585e43a915f293a351/GameServer/Game/ChessRogue)
- [RogueTourn](https://github.com/mikugirls/DanhengServer-OpenSource-3.7.0/tree/c0ec30dd37896efcffa64a585e43a915f293a351/GameServer/Game/RogueTourn)
- [RogueMagic](https://github.com/mikugirls/DanhengServer-OpenSource-3.7.0/tree/c0ec30dd37896efcffa64a585e43a915f293a351/GameServer/Game/RogueMagic)
- [FightActivityManager](https://github.com/mikugirls/DanhengServer-OpenSource-3.7.0/blob/c0ec30dd37896efcffa64a585e43a915f293a351/GameServer/Game/FightActivity/FightActivityManager.cs)

Không chép Proto/CmdIds 3.7 đè lên bản 4.2. Phải ánh xạ message và field, kể cả tên bị làm rối, sang giao thức 4.2; nối manager vào PlayerInstance, scene, battle settlement, database và resource loader. Repo hiện đã có một số Excel model và opcode liên quan, nhưng điều đó chưa chứng minh đủ tài nguyên hoặc đủ logic.

Đã kiểm tra thêm [mikugirls/DanHengServer2](https://github.com/mikugirls/DanHengServer2): có họ Rogue tương tự nhưng cây module không có BoxingClub/FightActivity như nhánh clubfight; chưa thấy lý do ưu tiên nguồn này hơn.

## 3. Upstream 4.6 và các PR chưa merge

Nguồn: [nhánh 4.6](https://github.com/Mar7thLover/March7thHoney-OpenSource/tree/fe00bdbda3c28ca33d01f171566aa35b5ab95083).

- Có thêm `GameServer/Game/Lobby` và `MultiPlayer`; DiceCombat được tổ chức lại trong MultiPlayer.
- Có `SourceGen`, cấu trúc đã thay đổi và không còn thư mục ServerSideProto ở root tại snapshot này. Đây là thay đổi kiến trúc lớn, không phải gói bổ sung trực tiếp cho 4.2.
- [PR #5](https://github.com/Mar7thLover/March7thHoney-OpenSource/pull/5): sửa settlement gacha và preview banner CN 4.6. Tác giả báo đã kiểm tra trên CNPRODWin4.6.0; khảo sát này chưa tự xác nhận kết quả đó. Có thể tham khảo logic kiểm tra và đồng bộ giao dịch, nhưng phần banner/wire mapping phải đánh giá lại cho 4.2.
- [PR #6](https://github.com/Mar7thLover/March7thHoney-OpenSource/pull/6): sửa cửa vào GridFight sau unlock quest, CycleScore và quest state. Diff hiện chứa cả thay đổi gacha của PR #5, nên không kéo toàn PR nếu chỉ cần bản sửa GridFight. Phần ghi thưởng CycleScore chưa được hỗ trợ đầy đủ theo mô tả PR.

Tại thời điểm kiểm tra cả PR #5 và #6 đều chưa merge.

## 4. Các nguồn khác và kết quả loại trừ

- [Melledy/LunarCore](https://github.com/Melledy/LunarCore): Java, có code Rogue ở nhánh main; phù hợp tham khảo thuật toán hơn là nhập trực tiếp vào C#. Nhánh main đã đọc vẫn khai báo 2.3.0. Nhánh mặc định lite không có thư mục game/rogue ở cây source đã kiểm tra, dù README nêu Simulated Universe; phải chọn đúng branch, không dựa riêng README hoặc kết quả tìm kiếm.
- [March7thHoney-Public](https://github.com/Mar7thDev/March7thHoney-Public): cây root công khai hiển thị docs/image/README; chưa có source gameplay để port.
- So sánh nhánh mặc định: `mikugirls/March7thHoney:main` và `Minato1799/March7thHoney-OpenSource:main` giống upstream main; `Jimqiu1003/March7thHoney-OpenSource:4.6` giống upstream 4.6. Thay đổi riêng của Jimqiu nằm trong các nhánh PR kể trên.
- `rh666666:main` không ahead upstream main vì bản sửa đã merge. `DuDu2255/March7thHoney-OpenSource:main` có thêm workflow build ở đầu lịch sử đã đọc, chưa thấy thêm module gameplay riêng.
- File LICENSE của checkout hiện tại và metadata các nguồn code chính đều là AGPL-3.0; README hiện tại lại ghi GPLv3. Khi port cần giữ nguồn gốc/attribution và kiểm tra license của phần code thực sự lấy.

## Thứ tự đề xuất

1. Tích hợp có chọn lọc các sửa GridFight cùng nền 4.2; tách thay đổi mặc định config và luật mở 13 slot.
2. Port BoxingClub để bổ sung một mode có phạm vi tương đối giới hạn, hoặc chọn Rogue cơ bản nếu ưu tiên Simulated Universe.
3. Sau khi Rogue nền hoạt động, mở rộng ChessRogue/RogueTourn/RogueMagic từng phần.
4. Đánh giá riêng Lobby/Multiplayer và các PR 4.6; giữ nguyên protocol 4.2 trừ khi có kế hoạch nâng phiên bản.

Mỗi phần cần kiểm tra: build, vào mode, hoàn thành/thất bại/thoát, nhận thưởng không trùng, và đăng nhập lại giữ đúng tiến độ. Chưa tích hợp code gameplay trong đợt khảo sát này.
