using March7thHoney.Data;
using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Game.GridFight;

/// <summary>
/// 三星昔涟进入出战席后触发一次专属免费诗篇商店。（但是好像没什么用？）
/// </summary>
public static class GridFightCyreneSpecialShopService
{
    private const uint CyreneAvatarId = 1415;
    private const uint ThreeStar = 3;
    private const uint TraitGoodsPrefix = 3004;
    private const uint RoleGoodsPrefix = 1415;
    private const uint BattlefieldPosMin = 1;
    private const uint BattlefieldPosMax = 13;
    private const int SpecialShopSlotCount = 5;

    private static readonly uint[] PreferredTraitGoodsIds = [300401, 300402, 300403, 300404, 300405];
    private static readonly uint[] FallbackRoleGoodsIds = [141501, 141502, 141503, 141504, 141505];

    /// <summary>
    /// 若本局未触发且出战席存在三星昔涟，则刷新为免费专属诗篇商店。
    /// </summary>
    public static bool TryApplySpecialShop(GridFightInstance inst)
    {
        if (inst.CyreneSpecialShopTriggered)
            return false;
        if (!TryGetThreeStarCyreneBattlefieldRole(inst, out _, out _))
            return false;

        var goodsIds = ResolveSpecialGoodsIds();
        if (goodsIds.Count == 0)
            return false;

        inst.ShopGoods.Clear();
        inst.ShopRollCounter++;
        inst.LastBoughtShopIndex = -1;
        inst.LastBoughtUniqueId = 0;

        foreach (var goodsId in goodsIds)
        {
            inst.ShopGoods.Add(new GridFightShopGoodsInfo
            {
                ShopGoodsPrice = 0,
                SpecialGoodsInfo = new GridFightSpecialGoodsInfo { SpecialGoodsId = goodsId }
            });
        }

        inst.CyreneSpecialShopTriggered = true;
        return true;
    }

    /// <summary>
    /// 解析客户端资源中真实存在的诗篇 augment ID（优先 300401-405，不足则补 141501-505）。
    /// </summary>
    public static List<uint> ResolveSpecialGoodsIds()
    {
        var ids = new List<uint>();
        var seen = new HashSet<uint>();

        void AddIfValid(uint id)
        {
            if (id == 0 || !seen.Add(id))
                return;
            if (!GameData.GridFightAugmentData.ContainsKey(id))
                return;
            ids.Add(id);
        }

        foreach (var id in PreferredTraitGoodsIds)
            AddIfValid(id);
        if (ids.Count >= SpecialShopSlotCount)
            return ids.Take(SpecialShopSlotCount).ToList();

        foreach (var id in FallbackRoleGoodsIds)
            AddIfValid(id);
        if (ids.Count >= SpecialShopSlotCount)
            return ids.Take(SpecialShopSlotCount).ToList();

        foreach (var id in GameData.GridFightAugmentData.Keys.OrderBy(x => x))
        {
            if (id / 100 != TraitGoodsPrefix && id / 100 != RoleGoodsPrefix)
                continue;
            AddIfValid(id);
            if (ids.Count >= SpecialShopSlotCount)
                break;
        }

        return ids.Take(SpecialShopSlotCount).ToList();
    }

    /// <summary>
    /// 获取出战席三星昔涟的 uniqueId 与 pos。
    /// </summary>
    public static bool TryGetThreeStarCyreneBattlefieldRole(GridFightInstance inst, out uint uniqueId, out uint pos)
    {
        uniqueId = 0;
        pos = 0;
        foreach (var (slot, uid) in inst.UniqueIdByPos)
        {
            if (slot is < BattlefieldPosMin or > BattlefieldPosMax || uid == 0)
                continue;
            if (!inst.RoleByUniqueId.TryGetValue(uid, out var roleKey))
                continue;
            if (GridFightRoleLookup.ToAvatarId(roleKey) != CyreneAvatarId)
                continue;
            if (inst.RoleStarByUniqueId.GetValueOrDefault(uid, 1u) != ThreeStar)
                continue;
            uniqueId = uid;
            pos = slot;
            return true;
        }

        return false;
    }
}
