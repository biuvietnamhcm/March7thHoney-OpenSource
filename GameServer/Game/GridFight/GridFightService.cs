using March7thHoney.GameServer.Game.Player;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Game.GridFight;

public sealed class GridFightService(PlayerInstance player)
{
    private GridFightManager Manager => player.GridFightManager!;

    public GridFightInstance? Current => Manager.GridFightInstance;

    public GridFightInstance EnsureOrStart(uint season = 1, uint divisionId = 10940, bool isOverlock = false)
    {
        var (_, inst) = Manager.StartGamePlay(season, divisionId, isOverlock);
        return inst!;
    }

    /// <summary>
    /// 开启新对局并重置实例，确保 portal 选择等开局 pending 从 queue 1 开始。
    /// </summary>
    public GridFightInstance StartAndPrepare(uint season, uint divisionId, bool isOverlock)
    {
        Manager.GridFightInstance = null;
        var inst = EnsureOrStart(season, divisionId, isOverlock);
        if (inst.PendingAction == null)
        {
            inst.PendingAction = new GridFightPendingAction
            {
                QueuePosition = inst.QueuePosition,
                PortalBuffAction = new GridFightPortalBuffActionInfo
                {
                    FCHPJKAIBHB = 1,
                    GridFightPortalBuffList = { inst.EnsurePortalBuffOffer() }
                }
            };
        }
        return inst;
    }

    /// <summary>
    /// 应用走位并在成功后尝试升星合成。
    /// </summary>
    public (List<GridFightPosInfo> accepted, Retcode retcode, List<GridFightInstance.RoleMergeResult> merges, List<GridFightPosInfo> syncPosList) UpdatePos(
        IEnumerable<GridFightPosInfo> posInfoList)
    {
        var list = posInfoList.ToList();
        var inst = Current;
        if (inst == null)
            return ([], Retcode.RetGridFightNotInGameplay, [], []);

        var (retcode, affected) = inst.TryValidateAndApplyPositionList(list);
        if (retcode != Retcode.RetSucc)
            return ([], retcode, [], []);

        var merges = inst.TryAutoMergeAllRoles();
        var benchMoved = merges.Count > 0 ? inst.FinalizeBenchAfterMerge() : [];
        var syncPosList = MergePosUpdates(affected, benchMoved);

        return (list, Retcode.RetSucc, merges, syncPosList);
    }

    /// <summary>
    /// 合并走位 sync 列表与升星后备战席压缩结果。
    /// </summary>
    private static List<GridFightPosInfo> MergePosUpdates(
        List<GridFightPosInfo> current,
        List<GridFightPosInfo> extra)
    {
        var merged = new Dictionary<uint, uint>();
        foreach (var pos in current.Concat(extra))
        {
            if (pos.UniqueId == 0) continue;
            merged[pos.UniqueId] = pos.Pos;
        }

        return merged.Select(kv => new GridFightPosInfo { UniqueId = kv.Key, Pos = kv.Value }).ToList();
    }

    public (uint roleId, uint roleUniqueId, uint pos, int goldDelta, List<GridFightInstance.RoleMergeResult> merges, List<GridFightPosInfo> benchMoved, bool duplicateRetry, uint purchasedAugmentId, uint purchasedAugmentTargetUniqueId, Retcode retcode) BuyGoods(IList<uint> buyGoodsIndexList)
    {
        var inst = Current;
        if (inst == null || buyGoodsIndexList.Count == 0)
            return (0, 0, 14, 0, [], [], false, 0, 0, Retcode.RetGridFightNotInGameplay);

        var idx = (int)buyGoodsIndexList[0];
        var (ok, uid, rid, addedPos, benchMoved, duplicateRetry, purchasedAugmentId, retcode) = inst.TryBuyGoods(idx);
        if (!ok) return (0, 0, 14, 0, [], benchMoved, false, 0, 0, retcode);

        var merges = duplicateRetry || purchasedAugmentId > 0
            ? []
            : inst.TryAutoMergeAllRoles();
        if (!duplicateRetry && merges.Count > 0)
        {
            inst.LastBoughtUniqueId = merges[^1].KeptUniqueId;
            benchMoved = MergeBenchRepositions(benchMoved, inst.FinalizeBenchAfterMerge());
        }

        return (rid, uid, addedPos, duplicateRetry ? 0 : -1, merges, benchMoved, duplicateRetry, purchasedAugmentId, inst.LastSpecialGoodsTargetUniqueId, Retcode.RetSucc);
    }

    /// <summary>
    /// 合并备战席压缩结果，避免同一角色重复条目。
    /// </summary>
    private static List<GridFightPosInfo> MergeBenchRepositions(
        List<GridFightPosInfo> current,
        List<GridFightPosInfo> extra)
    {
        var merged = new Dictionary<uint, uint>();
        foreach (var pos in current.Concat(extra))
        {
            if (pos.UniqueId == 0) continue;
            merged[pos.UniqueId] = pos.Pos;
        }

        return merged.Select(kv => new GridFightPosInfo { UniqueId = kv.Key, Pos = kv.Value }).ToList();
    }

    public bool RefreshShop()
    {
        return Current?.TryRefreshShop() ?? false;
    }

    public uint RecycleRole(uint uniqueId)
    {
        var inst = Current;
        if (inst == null) return 0;
        var (_, refund) = inst.TryRecycleRole(uniqueId);
        return refund;
    }

    public bool BuyExp()
    {
        return Current?.TryBuyExp() ?? false;
    }
}
