using March7thHoney.GameServer.Server.Packet.Send.DailyActive;
using March7thHoney.Kcp;

namespace March7thHoney.GameServer.Server.Packet.Recv.DailyActive;

[Opcode(CmdIds.GetDailyActiveInfoCsReq)]
public class HandlerGetDailyActiveInfoCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var manager = connection.Player!.DailyActivityManager!;
        await manager.RefreshAsync();
        await connection.SendPacket(new PacketGetDailyActiveInfoScRsp(manager));
    }
}
