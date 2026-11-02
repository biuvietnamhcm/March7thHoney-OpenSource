using March7thHoney.Enums.Avatar;
using March7thHoney.Enums.Mission;
using March7thHoney.Database;
using March7thHoney.Util;
using March7thHoney.GameServer.Server.Packet.Send.Player;
using March7thHoney.GameServer.Server.Packet.Send.PlayerSync;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.Player;

[Opcode(CmdIds.SetPlayerInfoCsReq)]
public class HandlerSetPlayerInfoCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var player = connection.Player!;
        var req = SetPlayerInfoCsReq.Parser.ParseFrom(data);
        if (req == null) return;
        player.Data.Name = req.Nickname;
        if (req.Gender is not (Gender.Man or Gender.Woman) || player.Data.IsGenderSet ||
            (ConfigManager.Config.ServerOption.EnableMission &&
             player.MissionManager!.GetSubMissionStatus(100010134) != MissionPhaseEnum.Accept))
        {
            await connection.SendPacket(new PacketSetPlayerInfoScRsp(player, req.IsModify));
            await connection.SendPacket(new PacketPlayerSyncScNotify(player.ToProto()));
            await connection.SendWatermarkLuaAsync();
            return;
        }

        if (req.Gender == Gender.Woman)
            player.Data.CurrentGender = Gender.Woman;
        else
            player.Data.CurrentGender = Gender.Man;
        player.Data.IsGenderSet = true;
        await player.ChangeAvatarPathType(8001, MultiPathAvatarTypeEnum.Warrior);

        var heroAvatarId = player.Data.CurrentGender == Gender.Woman ? 8002 : 8001;
        await player.LineupManager!.AddAvatarToCurTeam(heroAvatarId);
        await player.LineupManager!.AddAvatarToCurTeam(1001);
        await player.MissionManager!.FinishSubMission(100010134);
        DatabaseHelper.Instance?.SaveUidData(player.Uid);

        await connection.SendPacket(new PacketSetPlayerInfoScRsp(player, req.IsModify));
        await connection.SendPacket(new PacketPlayerSyncScNotify(player.ToProto()));
        await connection.SendWatermarkLuaAsync();
    }
}
