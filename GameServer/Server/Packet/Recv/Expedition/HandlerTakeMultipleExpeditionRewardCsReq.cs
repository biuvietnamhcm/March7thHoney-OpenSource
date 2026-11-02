using March7thHoney.GameServer.Server.Packet.Send.Expedition;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.Expedition;

[Opcode(CmdIds.TakeMultipleExpeditionRewardCsReq)]
public class HandlerTakeMultipleExpeditionRewardCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var player = connection.Player!;
        var manager = player.ExpeditionManager!;

        if (!manager.CanTakeRewardNow())
        {
            await connection.SendPacket(new PacketTakeMultipleExpeditionRewardScRsp(
                player,
                Retcode.RetExpeditionNotFinish));
            return;
        }

        var rewards = manager.BuildTakeRewardItems();
        if (rewards.Count > 0) await player.InventoryManager!.AddItems(rewards);

        manager.MarkRewardTaken();
        if (player.DailyActivityManager != null) await player.DailyActivityManager.RecordProgress(2100133, 1);

        await connection.SendPacket(new PacketTakeMultipleExpeditionRewardScRsp(
            player,
            Retcode.RetSucc,
            rewards));
    }
}
