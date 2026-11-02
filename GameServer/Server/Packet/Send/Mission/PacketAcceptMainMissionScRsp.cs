using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Send.Mission;

public class PacketAcceptMainMissionScRsp : BasePacket
{
    public PacketAcceptMainMissionScRsp(uint missionId, Retcode retcode = Retcode.RetSucc) : base(CmdIds.AcceptMainMissionScRsp)
    {
        var proto = new AcceptMainMissionScRsp
        {
            MainMissionId = missionId,
            Retcode = (uint)retcode
        };

        SetData(proto);
    }
}
