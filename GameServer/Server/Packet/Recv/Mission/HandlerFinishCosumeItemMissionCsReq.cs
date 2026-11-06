using March7thHoney.GameServer.Server.Packet.Send.Mission;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.Mission;

[Opcode(CmdIds.FinishCosumeItemMissionCsReq)]
public class HandlerFinishCosumeItemMissionCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = FinishCosumeItemMissionCsReq.Parser.ParseFrom(data);

        var player = connection.Player!;
        var selected = req.ItemList?.ItemList_.Select(x => new March7thHoney.Database.Inventory.ItemData
            { ItemId = (int)x.ItemId, Count = (int)x.Num }).ToList();
        var result = await player.MissionManager!.ConsumeMissionItems((int)req.SubMissionId, selected);
        await connection.SendPacket(new PacketFinishCosumeItemMissionScRsp(req.SubMissionId, result));
    }
}
