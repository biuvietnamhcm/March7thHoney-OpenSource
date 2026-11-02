using March7thHoney.GameServer.Server.Packet.Send.Mission;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.Mission;

[Opcode(CmdIds.AcceptMainMissionCsReq)]
public class HandlerAcceptMainMissionCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = AcceptMainMissionCsReq.Parser.ParseFrom(data);
        var missionId = req.MainMissionId;

        var retcode = await connection.Player!.MissionManager!.AcceptMainMissionFromClient((int)missionId);

        await connection.SendPacket(new PacketAcceptMainMissionScRsp(missionId, retcode));
    }
}
