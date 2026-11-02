using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.GameServer.Server;
using March7thHoney.GameServer.Server.Packet.Send.GridFight;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Game.GridFight;

/// <summary>
/// 银狼 LV.999 独立羁绊「头号玩家」：15061/15062/15063 进入出战席时解锁羁绊栏 GM 控制台。（然而并未生效）
/// </summary>
public static class GridFightHeadPlayerService
{
    private const uint HeadPlayerTraitId = 3006;
    private const uint HeadPlayerGmAugmentId = 150601;
    private const uint BattlefieldPosMin = 1;
    private const uint BattlefieldPosMax = 13;

    private static readonly uint[] HeadPlayerRoleIds = [15061, 15062, 15063];

    /// <summary>
    /// 若出战席存在尚未激活 GM 控制台的银狼角色，则绑定 augment 150601 并构建 sync。
    /// </summary>
    public static bool TryActivate(GridFightInstance inst, out GridFightSyncUpdateResultScNotify? notify)
    {
        notify = null;
        if (!TryGetBattlefieldHeadPlayerRole(inst, out var uniqueId, out _))
            return false;
        if (inst.HeadPlayerActivatedUniqueIds.Contains(uniqueId))
            return false;

        ApplyGmConsoleState(inst, uniqueId);
        notify = BuildGmConsoleUnlockNotify(inst, uniqueId);
        return true;
    }

    /// <summary>
    /// 检测出战席银狼并下发 GM 控制台解锁 sync（8456）。
    /// </summary>
    public static async System.Threading.Tasks.Task TrySendActivationAsync(Connection connection, GridFightInstance inst)
    {
        if (!TryGetBattlefieldHeadPlayerRole(inst, out var uniqueId, out _))
            return;

        if (!inst.HeadPlayerActivatedUniqueIds.Contains(uniqueId))
            ApplyGmConsoleState(inst, uniqueId);

        await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(
            BuildGmConsoleUnlockNotify(inst, uniqueId)));
    }

    /// <summary>
    /// 处理 TraitAction 确认（兼容历史 pending）；直接完成 GM 控制台激活，不打开其他 UI。
    /// </summary>
    public static async System.Threading.Tasks.Task HandleTraitActionAsync(
        Connection connection,
        GridFightInstance inst,
        uint ackPos,
        GridFightTraitActionResult? result)
    {
        if (inst.PendingAction?.TraitAction == null)
        {
            await ResyncTraitPending(connection, inst, ackPos);
            return;
        }

        var pending = inst.PendingAction.TraitAction;
        var uniqueId = pending.GridFightTraitMemberUniqueIdList.Count > 0
            ? pending.GridFightTraitMemberUniqueIdList[0]
            : 0u;

        if (result != null && result.UniqueId > 0)
            uniqueId = result.UniqueId;

        if (uniqueId > 0
            && inst.RoleByUniqueId.TryGetValue(uniqueId, out var roleKey)
            && IsHeadPlayerRole(GridFightRoleLookup.ToRoleId(roleKey)))
        {
            ApplyGmConsoleState(inst, uniqueId);
        }

        var nextPos = ackPos + 1;
        inst.QueuePosition = nextPos;
        inst.PendingAction = new GridFightPendingAction
        {
            QueuePosition = nextPos,
            ReturnPreparationAction = new GridFightReturnPreparationActionInfo()
        };

        await connection.SendPacket(new PacketGridFightHandlePendingActionScRsp(ackPos));

        var sync = new GridFightSyncUpdateResultScNotify();

        if (uniqueId > 0)
            sync.SyncResultDataList.Add(BuildGmConsoleRoleSyncSection(inst, uniqueId));

        var finishSec = new GridFightSyncResultData();
        finishSec.UpdateDynamicList.Add(new GridFightSyncData { FinishPendingActionPos = ackPos });
        finishSec.UpdateDynamicList.Add(new GridFightSyncData { SyncLockInfo = new GridFightLockInfo() });
        sync.SyncResultDataList.Add(finishSec);

        var nextSec = new GridFightSyncResultData();
        nextSec.UpdateDynamicList.Add(new GridFightSyncData
        {
            SyncLockInfo = new GridFightLockInfo
            {
                LockReason = GridFightLockReason.DfofffceffoKjmjdbjmbmc,
                LockType = GridFightLockType.PjbmhhnlclbEhfhdgpocnh
            }
        });
        nextSec.UpdateDynamicList.Add(new GridFightSyncData { PendingAction = inst.PendingAction });
        sync.SyncResultDataList.Add(nextSec);

        await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));
    }

    /// <summary>
    /// 判断 roleId 是否属于银狼 LV.999 系列（15061/15062/15063）。
    /// </summary>
    public static bool IsHeadPlayerRole(uint roleId)
    {
        roleId = GridFightRoleLookup.ToRoleId(roleId);
        return HeadPlayerRoleIds.Contains(roleId);
    }

    /// <summary>
    /// 查找出战席上的银狼 LV.999 角色（任意星级）。
    /// </summary>
    public static bool TryGetBattlefieldHeadPlayerRole(GridFightInstance inst, out uint uniqueId, out uint roleId)
    {
        uniqueId = 0;
        roleId = 0;

        foreach (var (pos, uid) in inst.UniqueIdByPos.OrderBy(kv => kv.Key))
        {
            if (pos < BattlefieldPosMin || pos > BattlefieldPosMax || uid == 0)
                continue;
            if (!inst.RoleByUniqueId.TryGetValue(uid, out var roleKey))
                continue;

            var internalRoleId = GridFightRoleLookup.ToRoleId(roleKey);
            if (!IsHeadPlayerRole(internalRoleId))
                continue;

            uniqueId = uid;
            roleId = internalRoleId;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 写入 GM 控制台状态：标记已激活，并绑定 augment 150601。
    /// </summary>
    public static void ApplyGmConsoleState(GridFightInstance inst, uint uniqueId)
    {
        inst.HeadPlayerActivatedUniqueIds.Add(uniqueId);
        inst.BindRoleAugment(uniqueId, HeadPlayerGmAugmentId);
    }

    /// <summary>
    /// 构建 GM 控制台解锁 notify（含 augment 150601 与 BAODHPCOJLH 绑定 sync）。
    /// </summary>
    private static GridFightSyncUpdateResultScNotify BuildGmConsoleUnlockNotify(GridFightInstance inst, uint uniqueId)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        notify.SyncResultDataList.Add(BuildGmConsoleRoleSyncSection(inst, uniqueId));
        return notify;
    }

    /// <summary>
    /// 构建 GM 控制台角色级 sync 段（HLFBBANMJDJ + UpdateRoleInfo + GridGameAugmentUpdate + BAODHPCOJLH）。
    /// </summary>
    private static GridFightSyncResultData BuildGmConsoleRoleSyncSection(GridFightInstance inst, uint uniqueId)
    {
        var roleSec = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpLfnkhcbhmpd };
        roleSec.SyncEffectParamList.Add(HeadPlayerTraitId);
        roleSec.SyncEffectParamList.Add(HeadPlayerGmAugmentId);
        roleSec.UpdateDynamicList.Add(new GridFightSyncData { HLFBBANMJDJ = uniqueId });
        roleSec.UpdateDynamicList.Add(new GridFightSyncData { UpdateRoleInfo = inst.BuildGridGameRoleInfo(uniqueId) });
        roleSec.UpdateDynamicList.Add(new GridFightSyncData
        {
            GridGameAugmentUpdate = new GridFightGameAugmentUpdate
            {
                UpdateAugmentInfo = new GridGameAugmentInfo
                {
                    AugmentId = HeadPlayerGmAugmentId,
                    NDCFBKJDPAH = true,
                    MHMLMKDFJLN = true
                }
            }
        });
        GridFightInstance.AppendRoleAugmentBindingSync(roleSec, inst, uniqueId, HeadPlayerGmAugmentId);
        return roleSec;
    }

    /// <summary>
    /// 重推当前 TraitAction pending（供 PendingActionProcessor 调用）。
    /// </summary>
    public static System.Threading.Tasks.Task ResyncTraitPendingPublic(
        Connection connection,
        GridFightInstance inst,
        uint ackPos) => ResyncTraitPending(connection, inst, ackPos);

    /// <summary>
    /// 重推当前 TraitAction pending。
    /// </summary>
    private static async System.Threading.Tasks.Task ResyncTraitPending(
        Connection connection,
        GridFightInstance inst,
        uint ackPos)
    {
        await connection.SendPacket(new PacketGridFightHandlePendingActionScRsp(ackPos));
        if (inst.PendingAction == null)
            return;

        var sync = new GridFightSyncUpdateResultScNotify();
        var sec = new GridFightSyncResultData();
        sec.UpdateDynamicList.Add(new GridFightSyncData
        {
            SyncLockInfo = new GridFightLockInfo
            {
                LockReason = GridFightLockReason.DfofffceffoKjmjdbjmbmc,
                LockType = GridFightLockType.PjbmhhnlclbEhfhdgpocnh
            }
        });
        sec.UpdateDynamicList.Add(new GridFightSyncData { PendingAction = inst.PendingAction });
        sync.SyncResultDataList.Add(sec);
        await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));
    }
}
