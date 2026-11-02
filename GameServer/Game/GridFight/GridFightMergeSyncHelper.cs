using System.Linq;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Server;
using March7thHoney.GameServer.Server.Packet.Send.GridFight;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Game.GridFight;

/// <summary>
/// 升星动画 sync 的统一发送入口。
/// </summary>
public static class GridFightMergeSyncHelper
{
    /// <summary>
    /// 发送升星动画与收尾 sync 包。
    /// </summary>
    public static async System.Threading.Tasks.Task SendMergeSyncsAsync(
        Connection connection,
        PlayerInstance player,
        IEnumerable<GridFightInstance.RoleMergeResult> merges,
        GridFightUpdateSrcType mergeAnimationSrc)
    {
        var manager = player.GridFightManager!;
        var inst = manager.GridFightInstance;
        if (inst == null) return;

        foreach (var merge in merges.Where(m => m.Merged))
        {
            await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(
                manager.BuildRoleMergeAnimationNotify(merge, mergeAnimationSrc)));
            await System.Threading.Tasks.Task.Yield();
            await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(
                manager.BuildRoleMergeFinalizeNotify(merge)));
        }
    }
}
