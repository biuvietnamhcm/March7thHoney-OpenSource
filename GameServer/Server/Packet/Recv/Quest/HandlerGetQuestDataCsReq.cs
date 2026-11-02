using March7thHoney.GameServer.Server.Packet.Send.Quest;
using March7thHoney.Kcp;

namespace March7thHoney.GameServer.Server.Packet.Recv.Quest;

[Opcode(CmdIds.GetQuestDataCsReq)]
public class HandlerGetQuestDataCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        if (connection.Player!.DailyActivityManager != null) await connection.Player.DailyActivityManager.RefreshAsync();
        await connection.SendPacket(new PacketGetQuestDataScRsp(connection.Player!));
    }
}
