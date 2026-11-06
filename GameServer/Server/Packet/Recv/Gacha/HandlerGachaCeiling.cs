using March7thHoney.Enums;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.Gacha;

[Opcode(CmdIds.GetGachaCeilingCsReq)]
public class HandlerGetGachaCeilingCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = GetGachaCeilingCsReq.Parser.ParseFrom(data);
        var rsp = new GetGachaCeilingScRsp { GachaType = req.GachaType };
        if (req.GachaType == (uint)GachaTypeEnum.Normal) rsp.GachaCeiling = connection.Player!.GachaManager!.Ceiling();
        else rsp.Retcode = (uint)Retcode.RetGachaNotSupport;
        var packet = new BasePacket(CmdIds.GetGachaCeilingScRsp);
        packet.SetData(rsp);
        await connection.SendPacket(packet);
    }
}

[Opcode(CmdIds.ExchangeGachaCeilingCsReq)]
public class HandlerExchangeGachaCeilingCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = ExchangeGachaCeilingCsReq.Parser.ParseFrom(data);
        var rsp = await connection.Player!.GachaManager!.ExchangeCeiling(req.GachaType, (int)req.AvatarId);
        var packet = new BasePacket(CmdIds.ExchangeGachaCeiling);
        packet.SetData(rsp);
        await connection.SendPacket(packet);
    }
}
