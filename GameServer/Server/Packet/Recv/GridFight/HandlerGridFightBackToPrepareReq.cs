using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.GameServer.Game.GridFight.Sync;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Server.Packet.Send.GridFight;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.GridFight;

[Opcode(CmdIds.GridFightBackToPrepareCsReq)]
public class HandlerGridFightBackToPrepareReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        _ = GridFightBackToPrepareReq.Parser.ParseFrom(data);
        var player = connection.Player!;
        var inst = player.GridFightManager?.GridFightInstance;

        if (inst == null)
        {
            var rsp = new BasePacket((ushort)CmdIds.GridFightBackToPrepareScRsp);
            rsp.SetData(new GDMIIBNJJEJ { Retcode = (uint)Retcode.RetGridFightNotInGameplay });
            await connection.SendPacket(rsp);
            return;
        }

        await connection.SendPacket(new PacketGDMIIBNJJEJ());

        if (inst.PendingAction?.ReturnPreparationAction == null)
        {
            if (GridFightLevelResolver.IsCombatNode(inst) && inst.NeedsBattleEncounterConfiguration())
            {
                var encounter = GridFightLevelResolver.Resolve(inst);
                inst.ConfigureNextBattle(encounter.StageId, encounter.Monsters.Select(m => m.MonsterId));
            }

            await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(player, GridFightSyncKind.Preparation));
            return;
        }

        var oldPos = inst.QueuePosition;
        inst.AdvanceQueue();
        inst.PendingAction = new GridFightPendingAction
        {
            QueuePosition = inst.QueuePosition,
            RoundBeginAction = new GridFightRoundBeginActionInfo()
        };
        await connection.SendPacket(new PacketGridFightHandlePendingActionScRsp(inst.QueuePosition));
        await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(player, GridFightSyncKind.PendingAdvance, (oldPos, inst.QueuePosition)));
    }
}
