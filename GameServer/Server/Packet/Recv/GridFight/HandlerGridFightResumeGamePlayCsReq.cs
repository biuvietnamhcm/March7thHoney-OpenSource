using March7thHoney.GameServer.Game.GridFight.Sync;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Server.Packet.Send.GridFight;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.GridFight;

[Opcode(CmdIds.GridFightResumeGamePlayCsReq)]
public class HandlerGridFightResumeGamePlayCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        _ = GridFightResumeGamePlayCsReq.Parser.ParseFrom(data);
        var player = connection.Player!;
        if (player.GridFightManager!.GridFightInstance == null)
            player.GridFightManager.StartGamePlay(0, 0, false);

        await connection.SendPacket(new PacketKMDHLENLIMF());
        await connection.SendPacket(new PacketINHDFEIOBNK(player));

        var inst = player.GridFightManager!.GridFightInstance!;
        if (inst.InitialBenchRolesSynced)
            await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(player, GridFightSyncKind.Bootstrap));
    }
}
