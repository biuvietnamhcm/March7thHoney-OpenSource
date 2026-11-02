using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.GameServer.Game.GridFight.Sync;
using March7thHoney.GameServer.Server.Packet.Send.GridFight;
using March7thHoney.Kcp;

namespace March7thHoney.GameServer.Server.Packet.Recv.GridFight;

/// <summary>
/// Handles the post-GetData sync request the client sends right after GridFightGetDataCsReq.
/// </summary>
[Opcode(CmdIds.GridFightGetDataPostSyncCsReq)]
public class HandlerGridFightGetDataPostSyncCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        await connection.SendPacket(new PacketGridFightGetDataPostSyncScRsp());

        var inst = connection.Player?.GridFightManager?.GridFightInstance;
        if (inst == null)
            return;

        inst.EnsureRouteBinding();
        if (inst.BattleComponent.StageId == 0 && GridFightLevelResolver.IsCombatNode(inst))
        {
            var encounter = GridFightLevelResolver.Resolve(inst);
            inst.ConfigureNextBattle(encounter.StageId, encounter.Monsters.Select(m => m.MonsterId));
        }

        await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(
            GridFightRouteSyncBuilder.BuildLevelLayerSync(inst)));
    }
}
