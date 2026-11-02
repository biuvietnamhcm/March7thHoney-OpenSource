using March7thHoney.Kcp;
using March7thHoney.GameServer.Server.Packet.Send.DailyActive;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Server.Packet.Send.Mission;
using March7thHoney.GameServer.Server.Packet.Send.Player;

namespace March7thHoney.GameServer.Server.Packet.Recv.Player;

[Opcode(CmdIds.PlayerLoginFinishCsReq)]
public class HandlerPlayerLoginFinishCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        await connection.SendPacket(CmdIds.PlayerLoginFinishScRsp);
        var player = connection.Player;
        if (player?.InventoryManager != null &&
            MonthCardService.TryClaimDailyReward(player.Data, out var monthCardReward))
        {
            await player.InventoryManager.AddItem(monthCardReward.ItemId, monthCardReward.Count, false);
            await connection.SendPacket(new PacketMonthCardRewardNotify([monthCardReward]));
        }

        await connection.SendPacket(new PacketFinishedMissionScNotify(
            player?.MissionManager?.GetAllFinishedMissionIds() ?? []));
        if (player?.DailyActivityManager != null)
        {
            await player.DailyActivityManager.RefreshAsync();
            await player.DailyActivityManager.SyncAsync();
        }
    }
}
