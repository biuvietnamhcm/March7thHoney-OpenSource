namespace March7thHoney.GameServer.Game.GridFight.Sync;

/// <summary>
/// 商店购买后的 sync 附加数据。
/// </summary>
public sealed class BuyGoodsSyncPayload
{
    public uint RoleId { get; init; }
    public uint RoleUniqueId { get; init; }
    public uint Pos { get; init; }
    public int GoldDelta { get; init; }
    public List<uint> MergedRemoved { get; init; } = [];
    public uint MergedKeepUid { get; init; }
    public uint MergedNewStar { get; init; }
    public uint ShopIndex { get; init; }
    public List<GridFightInstance.RoleMergeResult> Merges { get; init; } = [];
    public List<Proto.GridFightPosInfo> BenchRepositions { get; init; } = [];
    public bool DuplicateRetry { get; init; }
    public uint PurchasedAugmentId { get; init; }
    public uint PurchasedAugmentTargetUniqueId { get; init; }
}
