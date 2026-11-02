using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.DailyActive;

[Opcode(CmdIds.TakeApRewardCsReq)]
public class HandlerTakeApRewardCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = TakeApRewardCsReq.Parser.ParseFrom(data);
        var result = await connection.Player!.DailyActivityManager!.TakeRewards((int)req.Level);
        var packet = new BasePacket(CmdIds.TakeApRewardScRsp);
        packet.SetData(new TakeApRewardScRsp
        {
            Level = req.Level, Retcode = (uint)result.Code,
            Reward = new ItemList { ItemList_ = { result.Items.Select(x => x.ToProto()) } }
        });
        await connection.SendPacket(packet);
    }
}

[Opcode(CmdIds.TakeAllApRewardCsReq)]
public class HandlerTakeAllApRewardCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var result = await connection.Player!.DailyActivityManager!.TakeRewards();
        var packet = new BasePacket(CmdIds.TakeAllApRewardScRsp);
        packet.SetData(new TakeAllApRewardScRsp
        {
            Retcode = (uint)result.Code, TakeRewardLevelList = { result.Levels.Select(x => (uint)x) },
            Reward = new ItemList { ItemList_ = { result.Items.Select(x => x.ToProto()) } }
        });
        await connection.SendPacket(packet);
    }
}
