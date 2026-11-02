using March7thHoney.GameServer.Server.Packet.Send.GridFight;
using March7thHoney.GameServer.Game.Battle;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.GridFight;

[Opcode(CmdIds.GridFightEnterBattleStageCsReq)]
public class HandlerGridFightEnterBattleStageCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        _ = GridFightEnterBattleStageCsReq.Parser.ParseFrom(data);
        var player = connection.Player!;
        var inst = player.GridFightManager?.GridFightInstance;
        BattleInstance? battle = null;

        if (inst != null)
        {
            if (!GridFightLevelResolver.IsCombatNode(inst))
            {
                await connection.SendPacket(new PacketGridFightEnterBattleStageScRsp(inst, null));
                return;
            }

            EnsureBattleEncounterConfigured(inst);
            battle = inst.StartBattle();
            await connection.SendPacket(new PacketGridFightSeasonHandBookNotify(inst.BuildHandbookNotifyForBattle()));
        }
        await connection.SendPacket(new PacketGridFightEnterBattleStageScRsp(inst, battle));
    }

    private static void EnsureBattleEncounterConfigured(GridFightInstance inst)
    {
        if (!inst.NeedsBattleEncounterConfiguration())
            return;

        var encounter = GridFightLevelResolver.Resolve(inst);
        inst.ConfigureNextBattle(encounter.StageId, encounter.Monsters.Select(m => m.MonsterId));
    }
}
