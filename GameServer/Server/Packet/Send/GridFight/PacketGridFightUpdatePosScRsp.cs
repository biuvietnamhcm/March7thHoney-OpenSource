using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Send.GridFight;

public class PacketGridFightUpdatePosScRsp : BasePacket
{
    /// <summary>
    /// 返回走位结果；retcode 非 0 时 posInfoList 为空。
    /// </summary>
    public PacketGridFightUpdatePosScRsp(Retcode retcode, IEnumerable<GridFightPosInfo>? posInfoList = null)
        : base(CmdIds.GridFightUpdatePosScRsp)
    {
        var proto = new GridFightUpdatePosScRsp { Retcode = (uint)retcode };
        if (posInfoList != null)
            proto.GridFightPosInfoList.AddRange(posInfoList);
        SetData(proto);
    }
}
