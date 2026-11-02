using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Send.Mission;

public class PacketFinishCosumeItemMissionScRsp : BasePacket
{
    public PacketFinishCosumeItemMissionScRsp(uint subMissionId, Retcode retcode = Retcode.RetSucc) : base(CmdIds.FinishCosumeItemMissionScRsp)
    {
        var proto = new FinishCosumeItemMissionScRsp
        {
            SubMissionId = subMissionId,
            Retcode = (uint)retcode
        };

        SetData(proto);
    }

    public PacketFinishCosumeItemMissionScRsp() : base(CmdIds.FinishCosumeItemMissionScRsp)
    {
        var proto = new FinishCosumeItemMissionScRsp
        {
            Retcode = 1
        };
        SetData(proto);
    }
}
