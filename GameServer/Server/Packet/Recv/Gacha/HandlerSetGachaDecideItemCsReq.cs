using March7thHoney.GameServer.Server.Packet.Send.Gacha;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.Gacha;

[Opcode(CmdIds.SetGachaDecideItemCsReq)]
public class HandlerSetGachaDecideItemCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = SetGachaDecideItemCsReq.Parser.ParseFrom(data);

        var result = await connection.Player!.GachaManager!.SetDecideItems((int)req.GachaId, req.DGOMHDMJHEK.Select(x => (int)x).ToList());
        await connection.SendPacket(new PacketSetGachaDecideItemScRsp(req.GachaId, req.DGOMHDMJHEK.ToList(), result));
    }
}

