using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Send.GridFight;

/// <summary>
/// Acknowledges GridFightGetDataPostSyncCsReq so the client can continue loading gameplay UI.
/// </summary>
public class PacketGridFightGetDataPostSyncScRsp : BasePacket
{
    public PacketGridFightGetDataPostSyncScRsp() : base(CmdIds.GridFightGetDataPostSyncScRsp)
    {
        SetData(new KMDHLENLIMF { Retcode = (uint)Retcode.RetSucc });
    }
}
