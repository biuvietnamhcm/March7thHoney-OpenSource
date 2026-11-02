using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.GameServer.Game.GridFight.Sync;
using March7thHoney.GameServer.Game.Battle;
using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.GameServer.Game.GridFight.Component;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Server.Packet.Send.GridFight;
using March7thHoney.Proto;
using March7thHoney.Enums.GridFight;

namespace March7thHoney.GameServer.Game.GridFight;

public class GridFightInstance(PlayerInstance player, uint season, uint divisionId, bool isOverLock, uint uniqueId)
{
    public sealed class RoleMergeResult
    {
        public List<uint> RemovedUniqueIds { get; } = new();
        public uint KeptUniqueId { get; set; }
        public uint NewStar { get; set; }
        public uint RoleId { get; set; }
        /// <summary>
        /// 合成参与角色在合成前的 pos，供客户端升星特效定位。
        /// </summary>
        public Dictionary<uint, uint> ParticipantPosByUniqueId { get; } = new();
        /// <summary>
        /// 合成前各参与角色的快照（含旧星级），供升星动画 sync 使用。
        /// </summary>
        public Dictionary<uint, GridGameRoleInfo> PreMergeRoleSnapshots { get; } = new();
        /// <summary>
        /// 本次合成刚完成时的保留卡快照，避免连续合成后 finalize 读到已消耗状态。
        /// </summary>
        public GridGameRoleInfo? FinalKeeperRoleInfo { get; set; }
        public bool Merged => RemovedUniqueIds.Count > 0 && KeptUniqueId > 0;
    }
    public PlayerInstance Player { get; } = player;
    public uint Season { get; } = season;
    public uint DivisionId { get; } = divisionId;
    public bool IsOverLock { get; } = isOverLock;
    public uint UniqueId { get; } = uniqueId;

    
    public uint Gold { get; set; } = 3;
    public uint LineupHp { get; set; } = 80;
    public uint LineupMaxHp { get; } = 100;
    public uint BattleMaxHp { get; } = 10939;
    public uint Level { get; } = 1;
    public uint SectionId { get; set; } = 1;
    public uint NDOCIKPLKIF => ResolveCombatEliteGroup();
    public uint CurrentChapterId { get; set; } = 1;
    public uint CurrentBranchId { get; set; } = 1;
    public uint RouteId { get; set; } = 1200;
    public uint KeepWinCnt { get; set; }
    public int BattlesFinished { get; set; }
    public GridFightBattleComponent BattleComponent { get; } = new();

    
    public uint QueuePosition { get; set; } = 1;
    public GridFightPendingAction? PendingAction { get; set; }
    public uint NextRoleUniqueId { get; set; } = 100;
    public uint NextEquipUniqueId { get; set; } = 50;
    public uint NextOrbUniqueId { get; set; } = 10;

    
    public Dictionary<uint, uint> RoleByUniqueId { get; } = new();
    public Dictionary<uint, uint> UniqueIdByPos { get; } = new();
    public Dictionary<uint, uint> RoleStarByUniqueId { get; } = new();
    public Dictionary<uint, List<uint>> EquipUniqueIdsByRoleUniqueId { get; } = new();

    /// <summary>角色绑定的 augment ID（key 为角色 uniqueId）。</summary>
    public Dictionary<uint, List<uint>> RoleAugmentIdsByUniqueId { get; } = new();

    /// <summary>最近一次 special_goods 购买绑定的核心角色 uniqueId。</summary>
    public uint LastSpecialGoodsTargetUniqueId { get; set; }
    
    public uint ShopRefreshLeft { get; set; } = 2;
    public uint ShopRollCounter { get; set; }
    public List<GridFightShopGoodsInfo> ShopGoods { get; } = new();
    public List<uint> ShopRolePool { get; } = new();

    /// <summary>最近一次成功购买的商店槽位索引，用于幂等重试。</summary>
    public int LastBoughtShopIndex { get; set; } = -1;

    /// <summary>最近一次成功购买生成的角色 uniqueId。</summary>
    public uint LastBoughtUniqueId { get; set; }

    
    public List<uint> StageCandidates { get; } = new() { 35030205, 35030405, 35030208, 350202, 35030606 };
    public List<uint> CurrentEquipDraft { get; private set; } = new() { 35030205u, 35030405u, 35030208u };
    public List<GridFightEquipmentInfo> Equipments { get; } = new();
    public List<GridFightConsumableInfo> Consumables { get; } = new();

    
    public List<uint> SectionAffixIds { get; private set; } = new();
    public List<uint> SessionCampIds { get; private set; } = new();
    public List<uint> SessionBossMonsterIds { get; private set; } = new();
    public List<uint> ActivePortalBuffIds { get; } = new();
    public List<uint> AvailablePortalBuffRerolls { get; } = new() { 105, 1014, 132 };
    public List<uint> CurrentPortalBuffOffer { get; private set; } = new();
    
    public List<uint> ActiveAugmentIds { get; } = new();

    /// <summary>本局是否已触发三星昔涟出战专属免费商店。</summary>
    public bool CyreneSpecialShopTriggered { get; set; }

    /// <summary>已激活「头号玩家」GM 控制台的角色 uniqueId（15061/15062/15063）。</summary>
    public HashSet<uint> HeadPlayerActivatedUniqueIds { get; } = new();
    
    public uint LastEncounterQuality { get; set; }
    public uint LastEncounterAppliedSection { get; set; }
    public uint LastEncounterAppliedChapter { get; set; }

    public List<uint> CurrentAugmentOffer { get; private set; } = new();
    public uint CurrentAugmentReroll { get; set; } = 3;
    public uint LastAugmentConsumedSection { get; set; }
    public List<(uint RoleId, uint EquipId)> CurrentSupplyOffer { get; private set; } = new();
    public uint CurrentSupplyReroll { get; set; } = 1;
    public uint LastSupplyConsumedSection { get; set; }

    public sealed class EliteBranchOption
    {
        public uint EncounterId;
        public uint StageId;
        public uint PenaltyRuleId;
        public List<uint> MonsterIds = new();
        public uint RewardItemId;
        public uint RewardCount;
        public uint DifficultyTier;
    }
    public List<EliteBranchOption> CurrentEliteBranchOptions { get; private set; } = new();
    public uint CurrentEliteBranchReroll { get; set; } = 1;
    public uint LastEliteBranchConsumedSection { get; set; }

    private static readonly uint[] FallbackPortalBuffPool =
    {
        101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115,
        116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 127, 129, 132, 134, 135, 138,
        147, 1001, 1002, 1003, 1004, 1005, 1007, 1008, 1010, 1014, 1016,
        1101, 1102, 1104, 1106, 1107, 1108, 1112, 1113, 1114, 1115, 1116, 1118
    };

    public List<uint> RollPortalBuffs(int count = 3, IEnumerable<uint>? exclude = null)
    {
        var pool = (GameData.GridFightSeasonPortalData.TryGetValue(Season, out var seasonPool) && seasonPool.Count > 0)
            ? seasonPool.ToList()
            : FallbackPortalBuffPool.ToList();
        if (exclude != null)
            foreach (var id in exclude) pool.Remove(id);
        if (pool.Count <= count)
        {
            var fallback = pool.Distinct().ToList();
            while (fallback.Count < count)
                fallback.Add(FallbackPortalBuffPool[fallback.Count % FallbackPortalBuffPool.Length]);
            CurrentPortalBuffOffer = fallback.Take(count).ToList();
            return CurrentPortalBuffOffer;
        }
        var rng = Random.Shared;
        var picked = new List<uint>(count);
        while (picked.Count < count)
        {
            var idx = rng.Next(pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        CurrentPortalBuffOffer = picked;
        return CurrentPortalBuffOffer;
    }

    /// <summary>
    /// 获取当前 portal 三选一列表；优先沿用 pending 下发给客户端的选项，避免重掷导致校验失败。
    /// </summary>
    public List<uint> EnsurePortalBuffOffer()
    {
        if (CurrentPortalBuffOffer.Count == 0
            && PendingAction?.PortalBuffAction?.GridFightPortalBuffList is { Count: > 0 } pendingOffer)
        {
            CurrentPortalBuffOffer = pendingOffer.ToList();
        }
        if (CurrentPortalBuffOffer.Count == 0) RollPortalBuffs();
        return CurrentPortalBuffOffer;
    }

    /// <summary>
    /// 判断 portal buff 是否尚未完成选择（未激活 buff 且未同步初始备战角色）。
    /// </summary>
    public bool IsPortalBuffSelectionPending() =>
        !InitialBenchRolesSynced && ActivePortalBuffIds.Count == 0;

    public void ClearPortalBuffOffer() => CurrentPortalBuffOffer = new List<uint>();

    
    public List<(uint AvatarId, uint Pos, uint UniqueId, string Component)> RolledBenchRoles { get; private set; }
        = new();

    public void EnsureDefaultRoles()
    {
        
        if (ShopGoods.Count == 0) RotateShop();
        EnsureSessionPreview();
    }

    public void EnsureSessionPreview()
    {
        if (SessionCampIds.Count == 0) SessionCampIds = RollSessionCamps();
        if (SessionBossMonsterIds.Count == 0) SessionBossMonsterIds = ResolveSessionBossMonsterIds();
        if (SectionAffixIds.Count == 0) SectionAffixIds = RollSessionAffixes();
    }

    
    
    private List<uint> RollSessionCamps(int count = 3)
    {
        var pool = GameData.GridFightCampData.Values
            .Where(c => c.SeasonID == Season || c.SeasonID == 0)
            .Where(c => c.BossBattleArea > 0)
            .Where(c => c.Monsters.Any(m => m.MonsterTier >= 5))
            .Select(c => c.ID)
            .Distinct()
            .ToList();
        if (pool.Count == 0)
            pool = new List<uint> { 5, 8, 10, 16, 23, 25, 26, 27, 28 };
        return PickDistinct(pool, count);
    }

    
    private List<uint> ResolveSessionBossMonsterIds()
    {
        var bosses = new List<uint>(SessionCampIds.Count);
        foreach (var campId in SessionCampIds)
        {
            if (!GameData.GridFightCampData.TryGetValue(campId, out var camp))
            {
                bosses.Add(0);
                continue;
            }
            var boss = camp.Monsters
                .Where(m => m.MonsterTier >= 5)
                .OrderBy(m => m.MonsterTier)
                .ThenBy(m => m.MonsterID)
                .FirstOrDefault();
            bosses.Add(boss?.MonsterID ?? 0u);
        }
        return bosses;
    }

    public uint GetCurrentBossMonsterId()
    {
        EnsureSessionPreview();
        var idx = (int)CurrentChapterId - 1;
        if (idx < 0 || idx >= SessionBossMonsterIds.Count) return 0;
        return SessionBossMonsterIds[idx];
    }

    private List<uint> RollSessionAffixes()
    {
        var totalCount = 3;
        if (GameData.GridFightDivisionStageData.TryGetValue(DivisionId, out var stage)
            && stage.AffixChooseNumList.Count > 0)
        {
            totalCount = (int)stage.AffixChooseNumList.Sum(x => x);
        }
        if (totalCount <= 0) return new List<uint>();
        var pool = GameData.GridFightAffixConfigData.Keys.ToList();
        if (pool.Count == 0)
            pool = new List<uint> { 1001, 1002, 1003, 1004, 1005, 2002, 2003, 2004, 2005, 2006, 3001, 3002, 3003, 3004, 3005, 3006, 3007, 3008, 4001, 4002, 4003, 4005, 4006, 4007, 4008, 4009, 4010, 4011, 4012, 4013 };
        return PickDistinct(pool, totalCount);
    }

    private static List<uint> PickDistinct(List<uint> pool, int count)
    {
        var rng = Random.Shared;
        var copy = pool.ToList();
        var picked = new List<uint>(count);
        while (picked.Count < count && copy.Count > 0)
        {
            var idx = rng.Next(copy.Count);
            picked.Add(copy[idx]);
            copy.RemoveAt(idx);
        }
        while (picked.Count < count) picked.Add(pool[picked.Count % Math.Max(1, pool.Count)]);
        return picked;
    }

    
    
    public void MaterializeInitialBenchTeam(uint requiredTrait = 0)
    {
        if (RoleByUniqueId.Count > 0) return;
        if (RolledBenchRoles.Count == 0
            || (requiredTrait > 0 && !RolledBenchRoles.Any(r => RoleHasTrait(r.AvatarId, requiredTrait))))
        {
            RollInitialBenchRoles(requiredTrait: requiredTrait);
        }
        foreach (var (avatarId, pos, uniqueId, _) in RolledBenchRoles)
        {
            RoleByUniqueId[uniqueId] = avatarId;
            RoleStarByUniqueId[uniqueId] = 1;
            UniqueIdByPos[pos] = uniqueId;
        }
    }

    private static bool RoleHasTrait(uint avatarId, uint trait) =>
        GameData.GridFightRoleBasicInfoData.Values
            .Any(r => r.AvatarID == avatarId && r.TraitList.Contains(trait));

    public List<(uint AvatarId, uint Pos, uint UniqueId, string Component)> RollInitialBenchRoles(int count = 4, uint requiredTrait = 0)
    {
        var pool = GameData.GridFightRoleBasicInfoData.Values
            .Where(r => r.IsInPool && (r.SeasonID == Season || r.SeasonID == 0))
            .Where(r => r.AvatarID >= 1000 && r.AvatarID < 2000)
            .Where(r => r.RoleSavedValueList.Count > 0)
            .ToList();

        var rng = Random.Shared;
        var copy = pool.ToList();
        var picked = new List<GridFightRoleBasicInfoExcel>(count);

        
        if (requiredTrait > 0)
        {
            var traitMatched = copy.Where(r => r.TraitList.Contains(requiredTrait)).ToList();
            if (traitMatched.Count > 0)
            {
                var pivot = traitMatched[rng.Next(traitMatched.Count)];
                picked.Add(pivot);
                copy.Remove(pivot);
            }
        }

        while (picked.Count < count && copy.Count > 0)
        {
            var idx = rng.Next(copy.Count);
            picked.Add(copy[idx]);
            copy.RemoveAt(idx);
        }

        RolledBenchRoles = new List<(uint, uint, uint, string)>(picked.Count);
        for (var i = 0; i < picked.Count; i++)
        {
            var role = picked[i];
            RolledBenchRoles.Add((role.AvatarID, (uint)(14 + i), AllocRoleUniqueId(),
                role.RoleSavedValueList[0]));
        }
        return RolledBenchRoles;
    }


    public uint AllocRoleUniqueId() => ++NextRoleUniqueId;
    public uint AllocEquipUniqueId() => ++NextEquipUniqueId;
    public uint AllocOrbUniqueId() => ++NextOrbUniqueId;

    public GridFightEquipmentInfo? FindEquipment(uint uniqueId)
        => Equipments.FirstOrDefault(e => e.UniqueId == uniqueId);

    public void RemoveEquipmentByUniqueId(uint uniqueId)
    {
        Equipments.RemoveAll(e => e.UniqueId == uniqueId);
        foreach (var kv in EquipUniqueIdsByRoleUniqueId)
            kv.Value.RemoveAll(x => x == uniqueId);
    }

    public List<uint> UnequipAllFromRole(uint roleUid)
    {
        if (!EquipUniqueIdsByRoleUniqueId.TryGetValue(roleUid, out var list)) return new List<uint>();
        var snapshot = list.ToList();
        list.Clear();
        return snapshot;
    }

    public GridFightEquipmentInfo AddEquipment(uint equipmentId, uint source = 1)
    {
        var info = new GridFightEquipmentInfo
        {
            GridFightEquipmentId = equipmentId,
            Source = source,
            UniqueId = AllocEquipUniqueId()
        };
        Equipments.Add(info);
        return info;
    }

    
    public GridFightEquipmentInfo? RollEquipment(uint oldUniqueId, uint newEquipId, uint source = 1)
    {
        var old = FindEquipment(oldUniqueId);
        if (old == null) return null;
        uint? wearer = null;
        foreach (var kv in EquipUniqueIdsByRoleUniqueId)
        {
            if (kv.Value.Contains(oldUniqueId)) { wearer = kv.Key; break; }
        }
        RemoveEquipmentByUniqueId(oldUniqueId);
        var added = AddEquipment(newEquipId, source);
        if (wearer.HasValue)
        {
            if (!EquipUniqueIdsByRoleUniqueId.TryGetValue(wearer.Value, out var list))
                EquipUniqueIdsByRoleUniqueId[wearer.Value] = list = new List<uint>();
            list.Add(added.UniqueId);
        }
        return added;
    }

    /// <summary>
    /// 将 3503**** 成品装备（Craftable）升级为 3504****（Radiant）；保留穿戴角色与 source。
    /// </summary>
    public GridFightEquipmentInfo? UpgradeCraftableEquipment(uint equipUniqueId)
    {
        var old = FindEquipment(equipUniqueId);
        if (old == null) return null;
        if (!GameData.GridFightEquipUpgradeData.TryGetValue(old.GridFightEquipmentId, out var upgradeExcel))
            return null;
        if (!GameData.GridFightEquipmentData.TryGetValue(old.GridFightEquipmentId, out var oldExcel)
            || oldExcel.EquipCategory != GridFightEquipCategoryEnum.Craftable)
            return null;
        if (!GameData.GridFightEquipmentData.ContainsKey(upgradeExcel.UpgradeID))
            return null;

        return RollEquipment(equipUniqueId, upgradeExcel.UpgradeID, old.Source);
    }

    public bool TryConsumeConsumable(uint itemId, int amount = 1)
    {
        for (var i = 0; i < amount; i++)
        {
            var entry = Consumables.FirstOrDefault(c => c.ItemId == itemId);
            if (entry == null) return false;
            if (entry.Num <= 1) Consumables.Remove(entry);
            else entry.Num -= 1;
        }
        return true;
    }

    
    public static uint RollSameCategoryEquipment(uint currentEquipId)
    {
        if (!GameData.GridFightEquipmentData.TryGetValue(currentEquipId, out var current)) return 0;
        var category = current.EquipCategory;
        var pool = GameData.GridFightEquipmentData.Values
            .Where(e => e.EquipCategory == category && e.ID != currentEquipId)
            .Select(e => e.ID)
            .ToList();
        if (pool.Count == 0) return 0;
        return pool[Random.Shared.Next(pool.Count)];
    }

    
    public uint PreBattleLineupHp { get; set; }
    public uint PreBattleLevel { get; set; }
    public uint PreBattleExp { get; set; }
    public uint PlayerLevel { get; set; } = 3;
    public uint PlayerExp { get; set; }
    public uint PlayerMaxLevel { get; } = 10;
    public uint CampId { get; set; } = 18;
    public uint PreBattleGold { get; set; }
    public Proto.GridFightDamageSttInfo? LastBattleDamageStt { get; set; }
    public uint LastBattleIDEAAPCCFPF { get; set; }
    public Proto.GridFightSettleReason LastSettleReason { get; set; } = Proto.GridFightSettleReason.CdphdhnlhaoFmpbhelfgee;
    public List<(uint ItemId, uint UniqueId)> LastRewardedOrbs { get; } = new();
    public Dictionary<uint, uint> OrbItemByUniqueId { get; } = new() { [7u] = 199u };
    public List<Proto.GridFightHpModifyInfo> LastHpModifyTimeline { get; } = new();
    public Dictionary<uint, Proto.GridFightDropInfo> LastSectionRewards { get; } = new();

    public void AdvanceQueue(uint by = 1)
    {
        QueuePosition += by;
        PendingAction = null;
    }

    /// <summary>
    /// 校验并应用走位（含换位）；出战席（pos 1-13）禁止同名角色并存。
    /// </summary>
    /// <returns>实际发生位置变化的角色列表，供 sync 使用。</returns>
    public (Retcode retcode, List<GridFightPosInfo> affected) TryValidateAndApplyPositionList(
        IEnumerable<GridFightPosInfo> posInfoList)
    {
        EnsureDefaultRoles();
        var updates = posInfoList.ToList();
        if (updates.Any(p => p.Pos < BattlefieldPosMin || p.Pos > BenchPosMax))
            return (Retcode.RetGridFightPosNotValid, []);
        if (updates.Count == 0)
            return (Retcode.RetSucc, []);

        var projected = new Dictionary<uint, uint>(UniqueIdByPos);
        foreach (var posInfo in updates)
        {
            if (posInfo.UniqueId != 0 && !RoleByUniqueId.ContainsKey(posInfo.UniqueId))
                return (Retcode.RetGridFightRoleNotExist, []);

            ApplyProjectedMove(projected, posInfo.Pos, posInfo.UniqueId);
        }

        var battlefieldRoleIds = projected
            .Where(kv => kv.Key is >= 1 and <= 13 && kv.Value != 0)
            .Select(kv => GridFightRoleLookup.ToAvatarId(RoleByUniqueId.GetValueOrDefault(kv.Value)))
            .Where(id => id != 0)
            .ToList();
        if (battlefieldRoleIds.Count > GetCurrentMaxBattleRoleNum())
            return (Retcode.RetGridFightRegionFull, []);
        if (battlefieldRoleIds.Count != battlefieldRoleIds.Distinct().Count())
            return (Retcode.RetGridFightSameRoleInBattle, []);

        var affected = new List<GridFightPosInfo>();
        foreach (var posInfo in updates)
        {
            if (posInfo.UniqueId == 0)
            {
                if (UniqueIdByPos.Remove(posInfo.Pos, out var removedUid) && removedUid != 0)
                    affected.Add(new GridFightPosInfo { Pos = 0, UniqueId = removedUid });
                continue;
            }

            ApplyActualMove(affected, posInfo.Pos, posInfo.UniqueId);
        }

        return (Retcode.RetSucc, affected);
    }

    /// <summary>
    /// 在投影布局上模拟单次走位（含目标位互换）。
    /// </summary>
    private static void ApplyProjectedMove(Dictionary<uint, uint> layout, uint targetPos, uint movingUid)
    {
        if (movingUid == 0)
        {
            layout.Remove(targetPos);
            return;
        }

        var sourcePos = layout.FirstOrDefault(kv => kv.Value == movingUid).Key;
        var displacedUid = layout.GetValueOrDefault(targetPos);

        if (sourcePos > 0 && sourcePos != targetPos)
            layout.Remove(sourcePos);

        if (displacedUid != 0 && displacedUid != movingUid)
        {
            if (sourcePos > 0)
                layout[sourcePos] = displacedUid;
            else
                layout.Remove(targetPos);
        }

        layout[targetPos] = movingUid;
    }

    /// <summary>
    /// 应用单次走位并记录所有受影响角色，便于客户端同步。
    /// </summary>
    private void ApplyActualMove(List<GridFightPosInfo> affected, uint targetPos, uint movingUid)
    {
        var sourcePos = UniqueIdByPos.FirstOrDefault(kv => kv.Value == movingUid).Key;
        var displacedUid = UniqueIdByPos.GetValueOrDefault(targetPos);

        if (sourcePos > 0 && sourcePos != targetPos)
            UniqueIdByPos.Remove(sourcePos);

        if (displacedUid != 0 && displacedUid != movingUid)
        {
            if (sourcePos > 0)
            {
                UniqueIdByPos[sourcePos] = displacedUid;
                affected.Add(new GridFightPosInfo { Pos = sourcePos, UniqueId = displacedUid });
            }
            else if (TryAllocBenchPos(out var benchPos))
            {
                UniqueIdByPos[benchPos] = displacedUid;
                affected.Add(new GridFightPosInfo { Pos = benchPos, UniqueId = displacedUid });
            }
        }

        UniqueIdByPos[targetPos] = movingUid;
        affected.Add(new GridFightPosInfo { Pos = targetPos, UniqueId = movingUid });
    }

    /// <summary>
    /// 构建用于 sync 的角色快照。
    /// </summary>
    public GridGameRoleInfo BuildGridGameRoleInfo(uint uniqueId, uint pos = 0)
    {
        if (!RoleByUniqueId.TryGetValue(uniqueId, out var roleKey))
            return new GridGameRoleInfo();

        var syncRoleId = GridFightRoleLookup.ToSyncRoleId(roleKey);
        if (pos == 0)
            pos = UniqueIdByPos.FirstOrDefault(kv => kv.Value == uniqueId).Key;

        var roleInfo = new GridGameRoleInfo
        {
            Id = syncRoleId,
            Pos = pos,
            UniqueId = uniqueId,
            RoleStar = RoleStarByUniqueId.GetValueOrDefault(uniqueId, 1u)
        };
        if (GridFightRoleLookup.TryFind(roleKey, out var roleExcel)
            && roleExcel.RoleSavedValueList.Count > 0)
            roleInfo.GridFightValueInitComponent[roleExcel.RoleSavedValueList[0]] = 0;
        return roleInfo;
    }

    /// <summary>
    /// 解析角色当前站位；未找到时返回 0。
    /// </summary>
    public uint ResolveRolePos(uint uniqueId) =>
        UniqueIdByPos.FirstOrDefault(kv => kv.Value == uniqueId).Key;

    /// <summary>
    /// 绑定 augment 至指定角色（出战席/备战席均可；不校验角色类型）。
    /// </summary>
    public void BindRoleAugment(uint uniqueId, uint augmentId)
    {
        if (!GameData.GridFightAugmentData.ContainsKey(augmentId))
            return;

        if (!RoleAugmentIdsByUniqueId.TryGetValue(uniqueId, out var augments))
        {
            augments = [];
            RoleAugmentIdsByUniqueId[uniqueId] = augments;
        }

        if (!augments.Contains(augmentId))
            augments.Add(augmentId);
    }

    /// <summary>
    /// 构建备战段用的角色 augment 绑定条目（pos + uniqueId + augmentId）。
    /// </summary>
    public CKCKIDHMMEG BuildPrepRoleAugmentBinding(uint uniqueId, uint augmentId, uint pos = 0)
    {
        if (pos == 0)
            pos = ResolveRolePos(uniqueId);

        return new CKCKIDHMMEG
        {
            Pos = pos,
            UniqueId = uniqueId,
            JCMFPHMFAON = augmentId
        };
    }

    /// <summary>
    /// 构建战斗段用的角色 augment 绑定条目（pos + uniqueId + augmentId）。
    /// </summary>
    public CCGEOHGFAFD BuildBattleRoleAugmentBinding(uint uniqueId, uint augmentId, uint pos = 0)
    {
        if (pos == 0)
            pos = ResolveRolePos(uniqueId);

        return new CCGEOHGFAFD
        {
            Pos = pos,
            UniqueId = uniqueId,
            JCMFPHMFAON = augmentId
        };
    }

    /// <summary>
    /// 获取角色绑定的首个 augment ID；无绑定时返回 0。
    /// </summary>
    public uint GetPrimaryRoleAugmentId(uint uniqueId) =>
        RoleAugmentIdsByUniqueId.TryGetValue(uniqueId, out var augments) && augments.Count > 0
            ? augments[0]
            : 0u;

    /// <summary>
    /// 填充队伍段 MMAJCLACOBN（角色 augment 绑定全量 sync）。
    /// </summary>
    public void PopulatePrepRoleAugmentBindings(GridFightGameTeamInfo team)
    {
        foreach (var (pos, uniqueId) in UniqueIdByPos.OrderBy(kv => kv.Key))
        {
            if (uniqueId == 0 || !RoleAugmentIdsByUniqueId.TryGetValue(uniqueId, out var augments))
                continue;

            foreach (var augmentId in augments)
                team.MMAJCLACOBN.Add(BuildPrepRoleAugmentBinding(uniqueId, augmentId, pos));
        }
    }

    /// <summary>
    /// 向 sync 段追加 BAODHPCOJLH（单角色 augment 绑定增量同步）。
    /// </summary>
    public static void AppendRoleAugmentBindingSync(
        GridFightSyncResultData sync,
        GridFightInstance inst,
        uint uniqueId,
        uint augmentId)
    {
        sync.UpdateDynamicList.Add(new GridFightSyncData
        {
            BAODHPCOJLH = inst.BuildPrepRoleAugmentBinding(uniqueId, augmentId)
        });
    }

    /// <summary>
    /// 枚举当前棋盘与备战席上的全部有效角色（pos 1-22，不含临时溢出位）。
    /// </summary>
    public IEnumerable<(uint UniqueId, uint Pos)> EnumerateBoardRoles()
    {
        foreach (var (pos, uniqueId) in UniqueIdByPos.OrderBy(kv => kv.Key))
        {
            if (pos is < 1 or > BenchPosMax || uniqueId == 0) continue;
            if (!RoleByUniqueId.ContainsKey(uniqueId)) continue;
            yield return (uniqueId, pos);
        }
    }

    /// <summary>
    /// 扫描全场角色并执行所有可触发的三连升星；每次只合成一组，直到无可合成组合。
    /// </summary>
    public List<RoleMergeResult> TryAutoMergeAllRoles()
    {
        var results = new List<RoleMergeResult>();
        while (true)
        {
            var mergedAny = false;
            var roleIds = RoleByUniqueId.Values
                .Select(GridFightRoleLookup.ToRoleId)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            foreach (var roleId in roleIds)
            {
                while (TryFindMergeableStarter(roleId, out var starter))
                {
                    var merge = TryAutoMergeRole(roleId, starter);
                    if (!merge.Merged) break;
                    results.Add(merge);
                    mergedAny = true;
                }
            }

            if (!mergedAny) break;
        }

        return results;
    }

    /// <summary>
    /// 查找指定角色是否存在可三连合成的星级组，并返回其中一张参与卡的 uniqueId。
    /// </summary>
    private bool TryFindMergeableStarter(uint roleId, out uint starterUid)
    {
        starterUid = 0;
        roleId = GridFightRoleLookup.ToRoleId(roleId);
        if (roleId == 0) return false;

        var mergeableGroup = RoleByUniqueId
            .Where(kv => GridFightRoleLookup.ToRoleId(kv.Value) == roleId)
            .GroupBy(kv => RoleStarByUniqueId.GetValueOrDefault(kv.Key, 1u))
            .FirstOrDefault(g => g.Count() >= 3);
        if (mergeableGroup == null) return false;

        starterUid = mergeableGroup.First().Key;
        return starterUid != 0;
    }

    /// <summary>
    /// 对指定角色尝试三连升星（备战席与出战席均参与合成）。
    /// </summary>
    public RoleMergeResult TryAutoMergeRolesForRole(uint roleId)
    {
        var syncRoleId = GridFightRoleLookup.ToRoleId(roleId);
        var starter = RoleByUniqueId
            .FirstOrDefault(kv => GridFightRoleLookup.ToRoleId(kv.Value) == syncRoleId).Key;
        return starter == 0 ? new RoleMergeResult() : TryAutoMergeRole(syncRoleId, starter);
    }

    public List<(uint RoleId, uint Pos)> ResolveForegroundRoles()
    {
        EnsureDefaultRoles();
        return UniqueIdByPos.OrderBy(kv => kv.Key)
            .Where(kv => kv.Key is > 0 and <= 4)
            .Select(kv => (Role: RoleByUniqueId.GetValueOrDefault(kv.Value), Pos: kv.Key))
            .Where(t => t.Role != 0)
            .Select(t => (RoleId: t.Role, t.Pos))
            .ToList();
    }

    public List<(uint RoleId, uint Pos)> ResolveBackgroundRoles()
    {
        EnsureDefaultRoles();
        return UniqueIdByPos.OrderBy(kv => kv.Key)
            .Where(kv => kv.Key is > 4 and <= 13)
            .Select(kv => (Role: RoleByUniqueId.GetValueOrDefault(kv.Value), Pos: kv.Key))
            .Where(t => t.Role != 0)
            .Select(t => (RoleId: t.Role, t.Pos))
            .ToList();
    }

    public List<int> BuildForegroundAvatarIds(int maxCount = 4)
    {
        var list = ResolveForegroundRoles().Select(t => RoleIdToAvatarId(t.RoleId)).Where(id => id != 0).Take(maxCount).ToList();
        if (list.Count == 0)
            list = RoleByUniqueId.Values.Take(maxCount).Select(x => (int)x).ToList();
        return list;
    }

    public List<int> BuildBackgroundAvatarIds()
    {
        return ResolveBackgroundRoles().Select(t => RoleIdToAvatarId(t.RoleId)).Where(id => id != 0).ToList();
    }

    private static int RoleIdToAvatarId(uint roleKey) => (int)GridFightRoleLookup.ToAvatarId(roleKey);

    public List<IENNMHMOONM> CheckTrait()
    {
        EnsureDefaultRoles();
        var counts = new Dictionary<uint, uint>();
        var memberMap = new Dictionary<uint, List<(uint AvatarId, uint UniqueId)>>();

        foreach (var kv in UniqueIdByPos.Where(kv => kv.Key is > 0 and <= 13))
        {
            var uniqueId = kv.Value;
            if (uniqueId == 0 || !RoleByUniqueId.TryGetValue(uniqueId, out var roleKey)) continue;
            var role = GridFightRoleLookup.Find(roleKey);
            if (role == null) continue;
            var roleTraits = new HashSet<uint>(role.TraitList);
            if (EquipUniqueIdsByRoleUniqueId.TryGetValue(uniqueId, out var dressed))
            {
                foreach (var equipUid in dressed)
                {
                    var equip = Equipments.FirstOrDefault(e => e.UniqueId == equipUid);
                    if (equip == null) continue;
                    if (!GameData.GridFightEquipmentData.TryGetValue(equip.GridFightEquipmentId, out var equipExcel)) continue;
                    if (equipExcel.EquipFunc != Enums.GridFight.GridFightEquipFuncTypeEnum.OriginEmblem
                        && equipExcel.EquipFunc != Enums.GridFight.GridFightEquipFuncTypeEnum.ClassEmblem) continue;
                    if (equipExcel.EquipFuncParamList.Count == 0) continue;
                    roleTraits.Add(equipExcel.EquipFuncParamList[0]);
                }
            }
            foreach (var tid in roleTraits)
            {
                counts[tid] = counts.GetValueOrDefault(tid) + 1;
                if (!memberMap.TryGetValue(tid, out var list))
                    memberMap[tid] = list = new List<(uint, uint)>();
                list.Add((role.AvatarID, uniqueId));
            }
        }

        var result = new List<IENNMHMOONM>();
        foreach (var (tid, count) in counts)
        {
            if (count == 0) continue;
            if (!GameData.GridFightTraitBasicInfoData.TryGetValue(tid, out var traitExcel)) continue;

            uint layer = 0;
            if (GameData.GridFightTraitLayerData.TryGetValue(tid, out var layerMap) && layerMap.Count > 0)
            {
                foreach (var (threshold, excel) in layerMap)
                    if (threshold <= count && excel.Layer > layer)
                        layer = excel.Layer;
            }

            var trait = new IENNMHMOONM { TraitId = tid, NKFDBEHPNLG = layer };
            if (memberMap.TryGetValue(tid, out var members))
            {
                foreach (var (avatarId, uniqueId) in members)
                {
                    trait.MEEPFKLLIJB.Add(new BGNGLHHBGMI
                    {
                        EIHHLAOKAPH = GAPBBJCLMGP.Dgecgaafdjm,
                        GDNIKJGAEBH = avatarId,
                        IPDCMHIEKIJ = uniqueId,
                        GridUpdateSrc = PFODGDGFBBN.Iomeeecoiob
                    });
                }
            }
            if (layer > 0)
            {
                foreach (var effectId in traitExcel.TraitEffectList)
                {
                    var effectInfo = new BattleGridFightTraitEffectInfo { EffectId = effectId };
                    AttachTraitEffectLevelInfo(effectInfo, effectId);
                    trait.TraitEffectList.Add(effectInfo);
                }
            }
            result.Add(trait);
        }
        return result;
    }

    private static void AttachTraitEffectLevelInfo(BattleGridFightTraitEffectInfo effectInfo, uint effectId)
    {
        if (!GameData.GridFightTraitEffectData.TryGetValue(effectId, out var effectExcel)) return;
        if (effectExcel.TraitEffectType != GridFightTraitEffectTypeEnum.TraitBonus) return;
        if (!GameData.GridFightTraitBonusData.TryGetValue(effectId, out var thresholdMap)) return;

        var levelInfo = new GridFightTraitEffectLevelInfo();
        foreach (var (threshold, bonusExcel) in thresholdMap)
        {
            var drop = new GridFightDropInfo();
            foreach (var combinationId in bonusExcel.BonusParamList)
            {
                if (!GameData.GridFightCombinationBonusData.TryGetValue(combinationId, out var combination)) continue;
                foreach (var poolId in combination.CombinationBonusList)
                {
                    if (!GameData.GridFightBasicBonusPoolV2Data.TryGetValue(poolId, out var pool)) continue;
                    var itemId = pool.BonusTypeParamList.Count > 0 ? pool.BonusTypeParamList[0] : pool.BonusTypeParam;
                    if (itemId == 0) continue;
                    drop.PIBLJLBCKJL.Add(new LHPPIAKKFME
                    {
                        BGKDAMDFFKH = GridFightDropType.HiolcnpoponMkppcdpchie,
                        JJFFLMCCCMM = itemId,
                        Num = 1
                    });
                }
            }
            if (drop.PIBLJLBCKJL.Count > 0)
                levelInfo.TraitEffectLevelReward[threshold] = drop;
        }
        if (levelInfo.TraitEffectLevelReward.Count > 0)
            effectInfo.TraitEffectLevelInfo = levelInfo;
    }

    public void RotateShop()
    {
        ShopGoods.Clear();
        ShopRollCounter++;
        LastBoughtShopIndex = -1;
        LastBoughtUniqueId = 0;

        var rng = Random.Shared;
        var rarityWeights = GetShopRarityWeights();
        var rolePoolByRarity = BuildShopRolePoolByRarity();

        for (var i = 0; i < 5; i++)
        {
            var rarity = RollShopRarity(rarityWeights, rng);
            if (!rolePoolByRarity.TryGetValue(rarity, out var pool) || pool.Count == 0)
            {
                pool = rolePoolByRarity.Values.FirstOrDefault(x => x.Count > 0) ?? [];
            }
            if (pool.Count == 0) break;

            var roleId = pool[rng.Next(pool.Count)];
            var price = GetShopGoodsPrice(GridFightRoleLookup.Find(roleId)?.Rarity ?? rarity, 1);
            ShopGoods.Add(new GridFightShopGoodsInfo
            {
                ShopGoodsPrice = price,
                RoleGoodsInfo = new GridFightRoleGoodsInfo { RoleId = roleId, RoleStar = 1 }
            });
        }
    }

    private List<uint> GetShopRarityWeights() => GetShopRarityWeightsForLevel(PlayerLevel);

    /// <summary>
    /// 读取指定玩家等级对应的商店费用权重（与 RollShopRarity 使用同一配置）。
    /// </summary>
    public static List<uint> GetShopRarityWeightsForLevel(uint playerLevel)
    {
        if (GameData.GridFightPlayerLevelData.TryGetValue(playerLevel, out var conf)
            && conf.RarityWeights.Count >= 5)
            return conf.RarityWeights;
        return [100, 0, 0, 0, 0];
    }

    /// <summary>
    /// 构建商店 UI 费用概率展示数据（LDEDGOOKHFL / FLICPMGFKOK）。
    /// </summary>
    public FJPONJFLOOH BuildShopRarityDisplayInfo() =>
        BuildShopRarityDisplayInfoForLevel(PlayerLevel);

    /// <summary>
    /// 按玩家等级构建商店各费用档位出现概率，供 sync 与 ToProto 使用。
    /// </summary>
    public static FJPONJFLOOH BuildShopRarityDisplayInfoForLevel(uint playerLevel)
    {
        var weights = GetShopRarityWeightsForLevel(playerLevel);
        var info = new FJPONJFLOOH();
        for (var i = 0; i < 5; i++)
        {
            info.EDJPMNLLGGB.Add(new MJJEHCBNOKI
            {
                MMKNFIFOPPA = (uint)(i + 1),
                FLICPMGFKOK = i < weights.Count ? weights[i] : 0u
            });
        }
        return info;
    }

    private Dictionary<uint, List<uint>> BuildShopRolePoolByRarity()
    {
        var excluded = GetShopExcludedRoleIds();
        return BuildShopRolePoolByRarityCore(excluded);
    }

    /// <summary>
    /// 收集出战席（pos 1-13）与备战席（pos 14+）上 3 星及以上角色的内部 roleId，供商店刷新时从候选池排除。
    /// </summary>
    private HashSet<uint> GetShopExcludedRoleIds()
    {
        var excluded = new HashSet<uint>();
        foreach (var (pos, uniqueId) in UniqueIdByPos)
        {
            if (uniqueId == 0) continue;
            if (!IsBattlefieldOrBenchPos(pos)) continue;
            if (RoleStarByUniqueId.GetValueOrDefault(uniqueId, 1u) < 3) continue;
            if (!RoleByUniqueId.TryGetValue(uniqueId, out var roleKey)) continue;
            excluded.Add(GridFightRoleLookup.ToRoleId(roleKey));
        }
        return excluded;
    }

    /// <summary>
    /// 判断 pos 是否属于出战席或备战席（不含临时溢出落点）。
    /// </summary>
    private static bool IsBattlefieldOrBenchPos(uint pos) =>
        pos is >= BattlefieldPosMin and <= BattlefieldPosMax
        || pos is >= BenchPosMin and <= BenchPosMax;

    /// <summary>
    /// 按稀有度构建商店角色候选池；excludedRoleIds 非空时排除对应角色。
    /// </summary>
    private Dictionary<uint, List<uint>> BuildShopRolePoolByRarityCore(IReadOnlySet<uint>? excludedRoleIds)
    {
        var dict = new Dictionary<uint, List<uint>>();
        foreach (var role in GameData.GridFightRoleBasicInfoData.Values)
        {
            if (!role.IsInPool) continue;
            if (role.SeasonID != 0 && role.SeasonID != Season) continue;
            if (role.AvatarID < 1000 || role.AvatarID >= 2000) continue;
            if (excludedRoleIds != null && excludedRoleIds.Contains(role.ID)) continue;
            if (!dict.TryGetValue(role.Rarity, out var list))
                dict[role.Rarity] = list = [];
            list.Add(role.ID);
        }
        return dict;
    }

    private static uint RollShopRarity(IReadOnlyList<uint> weights, Random rng)
    {
        var total = 0u;
        for (var i = 0; i < Math.Min(5, weights.Count); i++) total += weights[i];
        if (total == 0) return 1;

        var roll = (uint)rng.Next(1, (int)total + 1);
        for (var i = 0; i < Math.Min(5, weights.Count); i++)
        {
            if (roll <= weights[i]) return (uint)(i + 1);
            roll -= weights[i];
        }
        return 1;
    }

    private static uint GetShopGoodsPrice(uint rarity, uint star)
    {
        if (!GameData.GridFightShopPriceData.TryGetValue(rarity, out var priceConf))
            return 1;
        var idx = (int)Math.Clamp(star, 1u, 4u) - 1;
        return priceConf.BuyGoldList[idx];
    }

    public void RefreshEquipDraft()
    {
        var startIdx = (BattlesFinished * 3) % StageCandidates.Count;
        CurrentEquipDraft = new List<uint>
        {
            StageCandidates[startIdx % StageCandidates.Count],
            StageCandidates[(startIdx + 1) % StageCandidates.Count],
            StageCandidates[(startIdx + 2) % StageCandidates.Count]
        };
    }

    public (bool ok, uint addedUniqueId, uint roleId, uint pos, List<GridFightPosInfo> benchMoved, bool duplicateRetry, uint purchasedAugmentId, Retcode retcode) TryBuyGoods(int shopIndex)
    {
        if (shopIndex < 0 || shopIndex >= ShopGoods.Count)
            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightParamNotMatch);
        var goods = ShopGoods[shopIndex];
        if (goods.SpecialGoodsInfo != null)
            return TryBuySpecialGoods(shopIndex, goods);

        if (goods.RoleGoodsInfo == null)
            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightParamNotMatch);
        if (goods.IsSoldOut)
        {
            if (LastBoughtShopIndex == shopIndex)
            {
                if (LastBoughtUniqueId != 0 && RoleByUniqueId.ContainsKey(LastBoughtUniqueId))
                {
                    var uid = LastBoughtUniqueId;
                    var existingPos = UniqueIdByPos.FirstOrDefault(kv => kv.Value == uid).Key;
                    var existingRoleId = GridFightRoleLookup.ToSyncRoleId(RoleByUniqueId[uid]);
                    return (true, uid, existingRoleId, existingPos, [], true, 0, Retcode.RetSucc);
                }

                var soldRoleId = goods.RoleGoodsInfo.RoleId;
                var keptUid = RoleByUniqueId
                    .FirstOrDefault(kv => GridFightRoleLookup.ToRoleId(kv.Value) == soldRoleId).Key;
                if (keptUid != 0)
                {
                    var keptPos = UniqueIdByPos.FirstOrDefault(kv => kv.Value == keptUid).Key;
                    return (true, keptUid, soldRoleId, keptPos, [], true, 0, Retcode.RetSucc);
                }

                return (true, 0, 0, 0, [], true, 0, Retcode.RetSucc);
            }

            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightGoodsSold);
        }

        var star = goods.RoleGoodsInfo.RoleStar > 0 ? goods.RoleGoodsInfo.RoleStar : 1u;
        var roleId = goods.RoleGoodsInfo.RoleId;
        var price = GetShopGoodsPrice(
            GridFightRoleLookup.TryFind(roleId, out var roleExcel) ? roleExcel.Rarity : 1u,
            star);
        if (Gold < price)
            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightCoinNotEnough);

        if (!TryAcquirePurchasePos(roleId, star, out var pos))
            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightNoEmptyPos);

        Gold -= price;
        goods.IsSoldOut = true;
        var uniqueId = AllocRoleUniqueId();
        RoleByUniqueId[uniqueId] = roleId;
        RoleStarByUniqueId[uniqueId] = star;
        UniqueIdByPos[pos] = uniqueId;
        LastBoughtShopIndex = shopIndex;
        LastBoughtUniqueId = uniqueId;
        return (true, uniqueId, roleId, pos, [], false, 0, Retcode.RetSucc);
    }

    /// <summary>
    /// 投影仪复制角色：生成与目标同角色的 1 星副本，落点规则与购买一致（备战席优先，可升星溢出）。
    /// </summary>
    /// <param name="sourceUniqueId">被复制角色的 uniqueId。</param>
    /// <param name="maxRarity">允许复制的最高费用（350105=3，350106=5）。</param>
    public (bool ok, uint newUniqueId, uint targetPos, uint sourcePos, Retcode retcode) TryCopyRole(
        uint sourceUniqueId,
        uint maxRarity)
    {
        if (!RoleByUniqueId.TryGetValue(sourceUniqueId, out var roleKey))
            return (false, 0, 0, 0, Retcode.RetGridFightParamNotMatch);

        if (!GridFightRoleLookup.TryFind(roleKey, out var roleExcel))
            return (false, 0, 0, 0, Retcode.RetGridFightParamNotMatch);

        if (maxRarity > 0 && roleExcel.Rarity > maxRarity)
            return (false, 0, 0, 0, Retcode.RetGridFightParamNotMatch);

        const uint copyStar = 1u;
        var sourcePos = ResolveRolePos(sourceUniqueId);
        if (sourcePos == 0)
            return (false, 0, 0, 0, Retcode.RetGridFightParamNotMatch);

        if (!TryAcquirePurchasePos(roleKey, copyStar, out var targetPos))
            return (false, 0, 0, sourcePos, Retcode.RetGridFightNoEmptyPos);

        var newUniqueId = AllocRoleUniqueId();
        RoleByUniqueId[newUniqueId] = roleKey;
        RoleStarByUniqueId[newUniqueId] = copyStar;
        UniqueIdByPos[targetPos] = newUniqueId;

        return (true, newUniqueId, targetPos, sourcePos, Retcode.RetSucc);
    }

    /// <summary>
    /// 购买昔涟专属诗篇等特殊商品：仅绑定至出战席三星昔涟。
    /// </summary>
    private (bool ok, uint addedUniqueId, uint roleId, uint pos, List<GridFightPosInfo> benchMoved, bool duplicateRetry, uint purchasedAugmentId, Retcode retcode) TryBuySpecialGoods(
        int shopIndex,
        GridFightShopGoodsInfo goods)
    {
        var augmentId = goods.SpecialGoodsInfo!.SpecialGoodsId;
        if (augmentId == 0)
            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightParamNotMatch);
        if (!GameData.GridFightAugmentData.ContainsKey(augmentId))
            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightParamNotMatch);

        if (!GridFightCyreneSpecialShopService.TryGetThreeStarCyreneBattlefieldRole(this, out var targetUid, out _))
            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightParamNotMatch);

        if (goods.IsSoldOut)
        {
            if (LastBoughtShopIndex == shopIndex
                && RoleAugmentIdsByUniqueId.TryGetValue(targetUid, out var owned)
                && owned.Contains(augmentId))
            {
                LastSpecialGoodsTargetUniqueId = targetUid;
                return (true, 0, 0, 0, [], true, augmentId, Retcode.RetSucc);
            }

            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightGoodsSold);
        }

        if (RoleAugmentIdsByUniqueId.TryGetValue(targetUid, out var existing) && existing.Contains(augmentId))
            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightParamNotMatch);

        var price = goods.ShopGoodsPrice;
        if (Gold < price)
            return (false, 0, 0, 0, [], false, 0, Retcode.RetGridFightCoinNotEnough);

        Gold -= price;
        goods.IsSoldOut = true;
        LastBoughtShopIndex = shopIndex;
        LastBoughtUniqueId = 0;
        LastSpecialGoodsTargetUniqueId = targetUid;

        BindRoleAugment(targetUid, augmentId);

        return (true, 0, 0, 0, [], false, augmentId, Retcode.RetSucc);
    }

    /// <summary>
    /// 对指定角色尝试一次三连升星（备战席与出战席均参与合成）。
    /// </summary>
    public RoleMergeResult TryAutoMergeRole(uint roleId, uint roleUniqueId)
    {
        var result = new RoleMergeResult();
        roleId = GridFightRoleLookup.ToRoleId(roleId);
        if (roleId == 0 || roleUniqueId == 0) return result;
        if (!TryFindMergeableGroup(roleId, roleUniqueId, out var starTier, out var sameTier))
            return result;

        var nextStar = starTier + 1;
        if (!CanRoleUpgradeToStar(roleId, nextStar))
            return result;

        var keepUniqueId = sameTier.Contains(roleUniqueId) ? roleUniqueId : sameTier[0];
        var consume = sameTier.Where(x => x != keepUniqueId).Take(2).ToList();
        if (consume.Count < 2) return result;

        result.RoleId = roleId;
        foreach (var uid in sameTier)
        {
            result.PreMergeRoleSnapshots[uid] = BuildGridGameRoleInfo(uid);
            var participantPos = UniqueIdByPos.FirstOrDefault(kv => kv.Value == uid).Key;
            if (participantPos > 0)
                result.ParticipantPosByUniqueId[uid] = participantPos;
        }

        foreach (var uid in consume)
        {
            RoleByUniqueId.Remove(uid);
            RoleStarByUniqueId.Remove(uid);
            foreach (var pos in UniqueIdByPos.Where(kv => kv.Value == uid).Select(kv => kv.Key).ToList())
                UniqueIdByPos.Remove(pos);
            result.RemovedUniqueIds.Add(uid);
        }

        RoleStarByUniqueId[keepUniqueId] = nextStar;
        result.KeptUniqueId = keepUniqueId;
        result.NewStar = nextStar;
        result.FinalKeeperRoleInfo = BuildGridGameRoleInfo(keepUniqueId);
        return result;
    }

    /// <summary>
    /// 查找指定角色当前可合成的星级组；优先使用 hintUid 所在星级。
    /// </summary>
    private bool TryFindMergeableGroup(uint roleId, uint hintUid, out uint starTier, out List<uint> sameTier)
    {
        starTier = 0;
        sameTier = [];

        var groups = RoleByUniqueId
            .Where(kv => GridFightRoleLookup.ToRoleId(kv.Value) == roleId)
            .GroupBy(kv => RoleStarByUniqueId.GetValueOrDefault(kv.Key, 1u))
            .Where(g => g.Count() >= 3)
            .OrderBy(g => g.Key)
            .ToList();
        if (groups.Count == 0) return false;

        if (hintUid != 0
            && RoleByUniqueId.ContainsKey(hintUid)
            && GridFightRoleLookup.ToRoleId(RoleByUniqueId[hintUid]) == roleId)
        {
            var hintStar = RoleStarByUniqueId.GetValueOrDefault(hintUid, 1u);
            var hintGroup = groups.FirstOrDefault(g => g.Key == hintStar);
            if (hintGroup != null)
            {
                starTier = hintStar;
                sameTier = hintGroup.Select(kv => kv.Key).Distinct().ToList();
                return sameTier.Count >= 3;
            }
        }

        var first = groups[0];
        starTier = first.Key;
        sameTier = first.Select(kv => kv.Key).Distinct().ToList();
        return sameTier.Count >= 3;
    }

    /// <summary>
    /// 判断角色能否升到指定星级；配置缺失时允许升至 3 星以匹配客户端默认规则。
    /// </summary>
    private static bool CanRoleUpgradeToStar(uint roleKey, uint nextStar)
    {
        var internalId = GridFightRoleLookup.ToRoleId(roleKey);
        if (GameData.GridFightRoleStarData.ContainsKey(internalId << 4 | nextStar))
            return true;
        return nextStar <= 3;
    }

    public (bool ok, uint refund) TryRecycleRole(uint uniqueId)
    {
        if (!RoleByUniqueId.TryGetValue(uniqueId, out var roleId)) return (false, 0);
        RoleByUniqueId.Remove(uniqueId);
        var roleStar = RoleStarByUniqueId.GetValueOrDefault(uniqueId, 1u);
        RoleStarByUniqueId.Remove(uniqueId);
        foreach (var pos in UniqueIdByPos.Where(kv => kv.Value == uniqueId).Select(kv => kv.Key).ToList())
            UniqueIdByPos.Remove(pos);
        var refund = GetRoleSellPrice(roleId, roleStar);
        Gold += refund;
        return (true, refund);
    }

    private static uint GetRoleSellPrice(uint roleKey, uint roleStar)
    {
        if (!GridFightRoleLookup.TryFind(roleKey, out var role))
            return 1;
        if (!GameData.GridFightShopPriceData.TryGetValue(role.Rarity, out var priceConf))
            return 1;

        var idx = (int)Math.Clamp(roleStar, 1u, 4u) - 1;
        return priceConf.SellGoldList[idx];
    }

    public bool TryRefreshShop()
    {
        
        var cost = ShopRefreshLeft == 0 ? 2u : ShopRefreshLeft;
        Gold = Gold < cost ? 0 : Gold - cost;
        RotateShop();
        return true;
    }

    public bool TryBuyExp()
    {
        var cost = GetBuyExpCost();
        if (cost == 0) cost = 4;

        Gold = Gold < cost ? 0 : Gold - cost;
        AddPlayerExp(cost);
        return true;
    }

    public uint GetBuyExpCost()
    {
        if (GameData.GridFightPlayerLevelData.TryGetValue(PlayerLevel, out var conf))
        {
            if (conf.LevelUpExp == 0) return 0; 
        }
        return 4;
    }

    /// <summary>备战席固定 9 格（pos 14-22）。</summary>
    public const uint BenchSlotCount = 9;
    public const uint BenchPosMin = 14;
    public const uint BenchPosMax = BenchPosMin + BenchSlotCount - 1;
    public const uint BattlefieldPosMin = 1;
    public const uint BattlefieldPosMax = 13;

    /// <summary>出战席前台固定格数（pos 1-4）。</summary>
    public const uint BattlefieldForegroundMax = 4;

    /// <summary>
    /// 统计指定角色在某星级的现有数量。
    /// </summary>
    public uint CountRolesAtStar(uint roleKey, uint star)
    {
        var roleId = GridFightRoleLookup.ToRoleId(roleKey);
        if (roleId == 0) return 0;
        return (uint)RoleByUniqueId.Count(kv =>
            GridFightRoleLookup.ToRoleId(kv.Value) == roleId
            && RoleStarByUniqueId.GetValueOrDefault(kv.Key, 1u) == star);
    }

    /// <summary>
    /// 购买一张同角色同星级卡是否会触发三连升星（已有至少 2 张）。
    /// </summary>
    public bool WouldPurchaseTriggerMerge(uint roleKey, uint star) =>
        star < uint.MaxValue && CanRoleUpgradeToStar(roleKey, star + 1) && CountRolesAtStar(roleKey, star) >= 2;

    /// <summary>
    /// 分配购买落点；备战席满员时若购买可触发升星则允许临时溢出落点。
    /// </summary>
    public bool TryAcquirePurchasePos(uint roleKey, uint star, out uint pos)
    {
        if (TryAllocBenchPos(out pos))
            return true;
        if (!WouldPurchaseTriggerMerge(roleKey, star))
            return false;

        pos = BenchPosMax + 1;
        return true;
    }

    /// <summary>
    /// 升星后回收临时溢出落点（pos &gt; 22），不压缩已有备战席布局。
    /// </summary>
    public List<GridFightPosInfo> FinalizeBenchAfterMerge()
    {
        var moved = new List<GridFightPosInfo>();
        var overflowEntries = UniqueIdByPos
            .Where(kv => kv.Key > BenchPosMax && kv.Value != 0)
            .ToList();
        foreach (var (overflowPos, uid) in overflowEntries)
        {
            UniqueIdByPos.Remove(overflowPos);
            if (!RoleByUniqueId.ContainsKey(uid)) continue;
            if (!TryAllocBenchPos(out var benchPos)) continue;
            UniqueIdByPos[benchPos] = uid;
            moved.Add(new GridFightPosInfo { Pos = benchPos, UniqueId = uid });
        }

        return moved;
    }

    /// <summary>
    /// 将备战席角色压缩到连续低位 pos，返回位置发生变化的角色。
    /// </summary>
    public List<GridFightPosInfo> CompactBenchSlots()
    {
        var moved = new List<GridFightPosInfo>();
        var benchRoles = UniqueIdByPos
            .Where(kv => kv.Key is >= BenchPosMin and <= BenchPosMax && kv.Value != 0)
            .OrderBy(kv => kv.Key)
            .ToList();

        foreach (var (pos, _) in benchRoles)
            UniqueIdByPos.Remove(pos);

        for (var i = 0; i < benchRoles.Count; i++)
        {
            var newPos = BenchPosMin + (uint)i;
            var uid = benchRoles[i].Value;
            var oldPos = benchRoles[i].Key;
            UniqueIdByPos[newPos] = uid;
            if (newPos != oldPos)
                moved.Add(new GridFightPosInfo { Pos = newPos, UniqueId = uid });
        }

        return moved;
    }

    /// <summary>出战总人数硬上限（含特殊效果加成后）。</summary>
    public const uint MaxBattleDeployTotal = 13;

    /// <summary>财富宝钻装备 ID；持有则上阵人数 +1。</summary>
    public const uint WealthGemEquipmentId = 350701;

    /// <summary>是否已向客户端同步过开局备战席角色。</summary>
    public bool InitialBenchRolesSynced { get; set; }

    /// <summary>
    /// 返回备战席槽位数（客户端 grid_fight_max_avatar_count），固定为 9。
    /// </summary>
    public uint GetBenchSlotCount() => BenchSlotCount;

    /// <summary>
    /// 是否持有财富宝钻；物品栏与角色穿戴均计入（装备实例统一保存在 Equipments 中）。
    /// </summary>
    public bool HasWealthGem() =>
        Equipments.Any(e => e.GridFightEquipmentId == WealthGemEquipmentId);

    /// <summary>
    /// 返回由装备/策略等提供的出战总人数额外上限（如财富宝钻 +1）。
    /// </summary>
    public uint GetDeployCapBonus() => HasWealthGem() ? 1u : 0u;

    /// <summary>
    /// Builds the deployment limit from player-level data and equipment bonuses.
    /// </summary>
    public GridFightSyncData BuildMaxBattleRoleNumSyncData() =>
        new() { MaxBattleRoleNum = GetCurrentMaxBattleRoleNum() };

    /// <summary>
    /// 构建后台区域物理格数 sync（GridFightOffFieldMaxCount = 总人数 - 前台 4 格）。
    /// </summary>
    public GridFightSyncData BuildOffFieldMaxCountSyncData() =>
        new() { GridFightOffFieldMaxCount = GetCurrentOffFieldMaxCount() };

    /// <summary>
    /// 返回后台出战席物理格数；与 MaxBattleRoleNum 配套下发以扩展客户端棋盘。
    /// </summary>
    public uint GetCurrentOffFieldMaxCount() =>
        GetCurrentMaxBattleRoleNum() > BattlefieldForegroundMax
            ? GetCurrentMaxBattleRoleNum() - BattlefieldForegroundMax
            : 0;

    /// <summary>
    /// 出战席布局相关 sync：总人数上限 + 后台物理格数。
    /// </summary>
    public IEnumerable<GridFightSyncData> BuildBattlefieldLayoutSyncData()
    {
        yield return BuildMaxBattleRoleNumSyncData();
        yield return BuildOffFieldMaxCountSyncData();
    }

    /// <summary>
    /// 向 sync 段追加出战席布局字段（MaxBattleRoleNum + GridFightOffFieldMaxCount）。
    /// </summary>
    public static void AppendBattlefieldLayoutSync(GridFightSyncResultData sync, GridFightInstance inst)
    {
        foreach (var entry in inst.BuildBattlefieldLayoutSyncData())
            sync.UpdateDynamicList.Add(entry);
    }

    /// <summary>
    /// Keeps level-based progression instead of unlocking all 13 slots at startup.
    /// </summary>
    public uint GetCurrentMaxBattleRoleNum()
    {
        var baseLimit = GameData.GridFightPlayerLevelData.TryGetValue(PlayerLevel, out var config)
            ? Math.Min(config.AvatarMaxNumber, MaxBattleDeployTotal)
            : BattlefieldForegroundMax;
        return Math.Min(baseLimit + GetDeployCapBonus(), MaxBattleDeployTotal);
    }

    /// <summary>
    /// Counts roles currently placed on the battlefield (pos 1-13).
    /// </summary>
    public uint GetDeployedBattleRoleCount() =>
        (uint)UniqueIdByPos.Count(kv => kv.Key is >= 1 and <= 13 && kv.Value != 0);

    /// <summary>
    /// Tries to allocate the next free bench slot (pos 14+).
    /// </summary>
    public bool TryAllocBenchPos(out uint pos)
    {
        pos = 0;
        for (uint i = 0; i < GetBenchSlotCount(); i++)
        {
            var candidate = BenchPosMin + i;
            if (UniqueIdByPos.ContainsKey(candidate)) continue;
            pos = candidate;
            return true;
        }

        return false;
    }

    private void AddPlayerExp(uint value)
    {
        if (value == 0) return;

        PlayerExp += value;
        while (PlayerLevel < GetConfiguredMaxPlayerLevel())
        {
            var need = GetLevelUpExpNeed(PlayerLevel);
            if (need == 0 || PlayerExp < need) break;
            PlayerExp -= need;
            PlayerLevel++;
        }
    }

    public void GainPlayerExp(uint value) => AddPlayerExp(value);

    public sealed class OrbUseResult
    {
        public uint UniqueId;
        public uint ItemId;
        public uint GoldAfter; 
        public bool GoldChanged;
        public bool LevelChanged;
        public uint? AddRoleId;
        public uint? AddRoleUniqueId;
        public uint? AddRolePos;
        public string? AddRoleComponent;
        public uint? AddEquipmentId;
        public uint? AddEquipmentUniqueId;
        public uint? AddConsumableItemId;
        public uint AddConsumableNewTotal;
    }

    private static readonly uint[] OrbEquipmentPool =
    {
        350201, 350202, 350203, 350204, 350205, 350206, 350207, 350208
    };

    private static readonly uint[] OrbConsumablePool = { 350101, 350103 };

    public List<OrbUseResult> TryUseOrbsDetailed(IEnumerable<uint> uniqueIds, bool isGetAll)
    {
        var targets = isGetAll ? OrbItemByUniqueId.Keys.ToList() : uniqueIds.Distinct().ToList();
        var results = new List<OrbUseResult>();
        var rng = Random.Shared;

        foreach (var uniqueId in targets)
        {
            if (!OrbItemByUniqueId.TryGetValue(uniqueId, out var orbItemId)) continue;
            OrbItemByUniqueId.Remove(uniqueId);

            var r = new OrbUseResult { UniqueId = uniqueId, ItemId = orbItemId };

            
            if (TryResolveRoleGrantOrb(orbItemId, out var roleId))
            {
                ApplyRoleGrant(r, roleId);
                results.Add(r);
                continue;
            }

            
            
            
            
            
            
            switch (orbItemId)
            {
                case 199u:
                    ApplyGold(r, 2);
                    break;
                case 102u:
                case 103u:
                    ApplyGold(r, 1);
                    ApplyConsumable(r, orbItemId == 102u ? 350101u : 350103u);
                    break;
                case 203u:
                    ApplyEquipment(r, OrbEquipmentPool[rng.Next(OrbEquipmentPool.Length)]);
                    break;
                case 204u:
                    ApplyGold(r, 3);
                    break;
                default:
                    ApplyTypeFallback(r, orbItemId, rng);
                    break;
            }

            results.Add(r);
        }

        return results;
    }

    private static bool TryResolveRoleGrantOrb(uint orbItemId, out uint roleId)
    {
        roleId = 0;
        
        if (orbItemId < 100000u) return false;
        var tail = orbItemId % 1000u;
        if (tail < 1u || tail > 999u) return false;
        
        var candidate = 1000u + tail;
        if (!GameData.GridFightRoleBasicInfoData.ContainsKey(candidate)) return false;
        roleId = candidate;
        return true;
    }

    private void ApplyGold(OrbUseResult r, uint amount)
    {
        Gold += amount;
        r.GoldAfter = Gold;
        r.GoldChanged = true;
    }

    private void ApplyExp(OrbUseResult r, uint amount)
    {
        var oldLevel = PlayerLevel;
        var oldExp = PlayerExp;
        AddPlayerExp(amount);
        if (PlayerLevel != oldLevel || PlayerExp != oldExp) r.LevelChanged = true;
    }

    private void ApplyConsumable(OrbUseResult r, uint itemId)
    {
        var entry = Consumables.FirstOrDefault(c => c.ItemId == itemId);
        if (entry == null)
        {
            entry = new GridFightConsumableInfo { ItemId = itemId, Num = 1 };
            Consumables.Add(entry);
        }
        else entry.Num += 1;
        r.AddConsumableItemId = itemId;
        r.AddConsumableNewTotal = entry.Num;
    }

    private void ApplyEquipment(OrbUseResult r, uint equipId)
    {
        var uid = AllocEquipUniqueId();
        Equipments.Add(new GridFightEquipmentInfo
        {
            GridFightEquipmentId = equipId,
            Source = 1,
            UniqueId = uid
        });
        r.AddEquipmentId = equipId;
        r.AddEquipmentUniqueId = uid;
    }

    private void ApplyRoleGrant(OrbUseResult r, uint roleId)
    {
        if (!GameData.GridFightRoleBasicInfoData.TryGetValue(roleId, out var roleExcel)) return;
        if (!TryAllocBenchPos(out var pos)) return;
        var uid = AllocRoleUniqueId();
        RoleByUniqueId[uid] = roleId;
        RoleStarByUniqueId[uid] = 1;
        UniqueIdByPos[pos] = uid;
        r.AddRoleId = roleId;
        r.AddRoleUniqueId = uid;
        r.AddRolePos = pos;
        if (roleExcel.RoleSavedValueList.Count > 0)
            r.AddRoleComponent = roleExcel.RoleSavedValueList[0];
    }

    private void ApplyTypeFallback(OrbUseResult r, uint orbItemId, Random rng)
    {
        if (!GameData.GridFightOrbData.TryGetValue(orbItemId, out var orbExcel))
        {
            ApplyGold(r, 1);
            return;
        }
        switch (orbExcel.Type)
        {
            case GridFightOrbTypeEnum.White: ApplyGold(r, 2); break;
            case GridFightOrbTypeEnum.Blue: ApplyExp(r, 2); break;
            case GridFightOrbTypeEnum.Glod:
                if (rng.Next(2) == 0) ApplyEquipment(r, OrbEquipmentPool[rng.Next(OrbEquipmentPool.Length)]);
                else ApplyGold(r, 5);
                break;
            case GridFightOrbTypeEnum.Colorful: ApplyExp(r, 4); break;
            case GridFightOrbTypeEnum.BigColorful: ApplyExp(r, 8); break;
            case GridFightOrbTypeEnum.GoldenEgg: ApplyGold(r, 8); break;
            default: ApplyGold(r, 1); break;
        }
    }

    public uint GetLevelUpExpNeed(uint level)
    {
        if (GameData.GridFightPlayerLevelData.TryGetValue(level, out var conf))
            return conf.LevelUpExp;
        return 4;
    }

    public uint GetConfiguredMaxPlayerLevel()
    {
        if (GameData.GridFightPlayerLevelData.Count > 0)
            return GameData.GridFightPlayerLevelData.Keys.Max();
        return PlayerMaxLevel;
    }

    public uint GetEnemyDifficultyLevel()
    {
        if (GameData.GridFightDivisionStageData.TryGetValue(DivisionId, out var stage)
            && stage.EnemyDifficultyLevel > 0)
            return stage.EnemyDifficultyLevel;
        return 1;
    }

    public uint GetDivisionLevel()
    {
        if (GameData.GridFightDivisionInfoData.TryGetValue(DivisionId, out var info) && info.DivisionLevel > 0)
            return info.DivisionLevel;
        
        return Math.Clamp((DivisionId - 10000u) / 100u, 1u, 9u);
    }

    
    
    public uint GetActiveAugmentDifficultyAdd()
    {
        var divLv = GetDivisionLevel();
        if (!GameData.GridFightAugmentMonsterData.TryGetValue(divLv, out var byQuality)) return 0;
        uint sum = 0;
        foreach (var augId in ActiveAugmentIds)
        {
            if (!GameData.GridFightAugmentData.TryGetValue(augId, out var aug)) continue;
            if (byQuality.TryGetValue(aug.Quality, out var entry)) sum += entry.EnemyDiffLvAdd;
        }
        return sum;
    }

    
    public uint GetEncounterDifficultyAdd()
    {
        if (LastEncounterQuality == 0
            || LastEncounterAppliedSection != SectionId
            || LastEncounterAppliedChapter != CurrentChapterId) return 0;
        if (!GameData.GridFightDivisionStageData.TryGetValue(DivisionId, out var stage)) return 0;
        var rule = stage.BinaryNodeDiffAddRule;
        if (rule == 0) return 0;
        if (!GameData.GridFightBinaryDiffAddRuleData.TryGetValue(rule, out var byQuality)) return 0;
        return byQuality.TryGetValue(LastEncounterQuality, out var entry) ? entry.EnemyDifficultyAddValue : 0u;
    }

    
    public uint GetEffectiveEnemyDifficultyLevel()
        => GetEnemyDifficultyLevel() + GetActiveAugmentDifficultyAdd() + GetEncounterDifficultyAdd();

    
    
    
    
    /// <summary>
    /// Resolves the elite group id used by battle monsters for the current section.
    /// </summary>
    public uint ResolveEliteGroupForCurrentSection()
    {
        var route = Battle.GridFightLevelResolver.ResolveRoute(this);
        if (route?.NodeType == Enums.GridFight.GridFightNodeTypeEnum.Monster)
            return ResolveRewardEliteGroup(CurrentChapterId, SectionId);
        if (route?.NodeType == Enums.GridFight.GridFightNodeTypeEnum.Supply)
            return 0;

        return ResolveCombatEliteGroup();
    }

    /// <summary>
    /// Returns the combat-progression elite group id shown in level/shop sync payloads.
    /// </summary>
    public uint ResolveCombatEliteGroup()
    {
        return 1800u + Math.Max(1u, CountCombatNodesUpToCurrent());
    }

    private static uint ResolveRewardEliteGroup(uint chapter, uint section)
    {
        
        if (chapter == 1 && section <= 2) return 1816;
        if (chapter == 1) return 1817;     
        return 1816u + chapter;            
    }

    private uint CountCombatNodesUpToCurrent()
    {
        var route = Battle.GridFightLevelResolver.ResolveRoute(this);
        if (route == null)
            return 1;

        if (!GameData.GridFightStageRouteData.TryGetValue(route.ID, out var bucket))
            return 1;

        uint count = 0;
        foreach (var r in bucket.Values.OrderBy(r => r.ChapterID).ThenBy(r => r.SectionID))
        {
            if (r.NodeType == Enums.GridFight.GridFightNodeTypeEnum.Supply) continue;
            if (r.NodeType == Enums.GridFight.GridFightNodeTypeEnum.Monster) continue;
            count++;
            if (r.ChapterID == CurrentChapterId && r.SectionID == SectionId)
                return count;
        }

        return 1;
    }

    /// <summary>
    /// Aligns RouteId with the resolved stage-route table for the current chapter/section.
    /// </summary>
    public void EnsureRouteBinding()
    {
        var route = Battle.GridFightLevelResolver.ResolveRoute(this);
        if (route != null)
            RouteId = route.ID;
    }

    /// <summary>
    /// Builds the route layer payload used by the client node map and battle preview.
    /// </summary>
    public GridFightLayerInfo BuildCurrentLayerInfo()
    {
        EnsureRouteBinding();

        var routeInfo = new GridFightRouteInfo
        {
            FightCampId = 20,
            EliteBranchId = CurrentBranchId > 0 ? CurrentBranchId : 1u
        };

        if (BattleComponent.StageId > 0 && BattleComponent.MonsterIds.Count > 0)
        {
            AppendRouteEncounter(routeInfo, BattleComponent.StageId, BattleComponent.MonsterIds);
        }
        else if (GridFightLevelResolver.IsCombatNode(this))
        {
            var encounter = GridFightLevelResolver.Resolve(this);
            AppendRouteEncounter(routeInfo, encounter.StageId,
                encounter.Monsters.Select(m => (m.MonsterId, m.RoleStar)));
        }

        return new GridFightLayerInfo { RouteInfo = routeInfo };
    }

    private static void AppendRouteEncounter(GridFightRouteInfo routeInfo, uint stageId,
        IEnumerable<uint> monsterIds)
    {
        AppendRouteEncounter(routeInfo, stageId, monsterIds.Select(id => (id, 1u)));
    }

    private static void AppendRouteEncounter(GridFightRouteInfo routeInfo, uint stageId,
        IEnumerable<(uint MonsterId, uint RoleStar)> monsterIds)
    {
        var encounterInfo = new GridFightEncounterInfo { LFKBMDHKPFI = stageId };
        var wave = new GridEncounterMonsterWave { IGMMPDDCJIN = 1 };
        foreach (var (monsterId, roleStar) in monsterIds)
            wave.PPOEDDFFEKK.Add(new PJLBDMPEKFP { MonsterId = monsterId, RoleStar = roleStar });
        encounterInfo.MonsterWaveList.Add(wave);
        routeInfo.RouteEncounterList.Add(encounterInfo);
    }

    public bool TryEquipFromDraft(uint idx)
    {
        if (idx >= CurrentEquipDraft.Count) return false;
        var equipId = CurrentEquipDraft[(int)idx];
        Equipments.Add(new GridFightEquipmentInfo
        {
            GridFightEquipmentId = equipId,
            Source = 1,
            UniqueId = AllocEquipUniqueId()
        });
        return true;
    }

    public void OnBattleResolved(bool win)
    {
        BattlesFinished++;
        if (win)
        {
            var nextSection = SectionId + 1;
            var maxSection = GetChapterSectionCount(CurrentChapterId);
            if (maxSection > 0 && nextSection > maxSection)
            {
                var nextChapter = CurrentChapterId + 1;
                if (GetChapterSectionCount(nextChapter) > 0)
                {
                    CurrentChapterId = nextChapter;
                    SectionId = 1;
                    LastAugmentConsumedSection = 0;
                    LastSupplyConsumedSection = 0;
                    LastEliteBranchConsumedSection = 0;
                    ClearAugmentOffer();
                    ClearSupplyOffer();
                    ClearEliteBranchOptions();
                    CurrentAugmentReroll = 3;
                    CurrentSupplyReroll = 1;
                    CurrentEliteBranchReroll = 1;
                }
            }
            else
            {
                SectionId = nextSection;
            }
        }
        ShopRefreshLeft = 2;
        RotateShop();
        RefreshEquipDraft();
    }

    public uint GetChapterSectionCount(uint chapter)
    {
        if (!GameData.GridFightStageRouteData.TryGetValue(RouteId, out var bucket)) return 0;
        var max = 0u;
        foreach (var route in bucket.Values)
        {
            if (route.ChapterID == chapter && route.SectionID > max) max = route.SectionID;
        }
        return max;
    }

    public uint GetMaxChapterId()
    {
        if (!GameData.GridFightStageRouteData.TryGetValue(RouteId, out var bucket)) return 0;
        var max = 0u;
        foreach (var route in bucket.Values)
            if (route.ChapterID > max) max = route.ChapterID;
        return max;
    }

    public bool IsFinalSection(uint chapter, uint section)
    {
        var maxChapter = GetMaxChapterId();
        if (maxChapter == 0 || chapter != maxChapter) return false;
        return section == GetChapterSectionCount(chapter);
    }

    private static readonly uint[] Section1RewardOrbs = { 102u, 199u, 203u, 204u };

    
    private static readonly uint[] RewardOrbPool = { 102u, 103u, 199u, 199u, 203u, 204u };

    private List<uint> RollRewardOrbs()
    {
        var rng = Random.Shared;
        var count = rng.Next(4, 6); 
        var picks = new List<uint>(count);
        
        picks.Add(rng.Next(2) == 0 ? 102u : 103u);
        picks.Add(199u);
        picks.Add(203u);
        picks.Add(204u);
        while (picks.Count < count)
            picks.Add(RewardOrbPool[rng.Next(RewardOrbPool.Length)]);
        return picks;
    }

    /// <summary>
    /// 判断战斗统计上报的 role_id 是否与出战角色匹配（兼容 AvatarID / 内部 RoleId）。
    /// </summary>
    private static bool MatchesBattleStatRoleId(uint reportedRoleId, uint roleKey)
    {
        if (reportedRoleId == 0 || roleKey == 0) return false;
        if (reportedRoleId == roleKey) return true;
        if (reportedRoleId == GridFightRoleLookup.ToSyncRoleId(roleKey)) return true;
        if (reportedRoleId == GridFightRoleLookup.ToAvatarId(roleKey)) return true;
        return reportedRoleId == GridFightRoleLookup.ToRoleId(roleKey);
    }

    /// <summary>
    /// 根据战斗统计 role_id 查找对应出战角色（pos 1-13）。
    /// </summary>
    private (uint uniqueId, uint roleKey)? FindDeployedRoleByBattleStatId(uint reportedRoleId)
    {
        foreach (var (pos, uniqueId) in UniqueIdByPos.Where(kv => kv.Key is >= 1 and <= 13 && kv.Value != 0))
        {
            if (!RoleByUniqueId.TryGetValue(uniqueId, out var roleKey)) continue;
            if (MatchesBattleStatRoleId(reportedRoleId, roleKey))
                return (uniqueId, roleKey);
        }

        return null;
    }

    public void RecordBattleSnapshot(PVEBattleResultCsReq req, bool win)
    {
        PreBattleLineupHp = LineupHp;
        PreBattleLevel = PlayerLevel;
        PreBattleExp = PlayerExp;
        PreBattleGold = Gold;

        LastBattleDamageStt = new GridFightDamageSttInfo();
        LastBattleIDEAAPCCFPF = req.Stt?.OGPOFMOGPIP?.IDEAAPCCFPF ?? 0;
        var activeRoleIds = new HashSet<uint>();
        if (req.Stt?.OGPOFMOGPIP?.JPDJMCCKENI != null)
        {
            foreach (var entry in req.Stt.OGPOFMOGPIP.JPDJMCCKENI)
            {
                var roleId = entry.RoleId;
                if (roleId == 0) continue;
                var deployed = FindDeployedRoleByBattleStatId(roleId);
                var syncRoleId = deployed != null
                    ? GridFightRoleLookup.ToSyncRoleId(deployed.Value.roleKey)
                    : GridFightRoleLookup.ToSyncRoleId(roleId);
                var roleStar = deployed != null
                    ? RoleStarByUniqueId.GetValueOrDefault(deployed.Value.uniqueId, 1u)
                    : 1u;
                var inActive = entry.Damage > 0 || entry.BOIEGPAPHOP > 0;
                if (inActive) activeRoleIds.Add(syncRoleId);
                LastBattleDamageStt.EABPCKEDDBH.Add(new HHHMMJBGCNG
                {
                    RoleId = syncRoleId,
                    RoleStar = roleStar,
                    TotalDamage = entry.Damage,
                    LDMNBDIDFCC = inActive,
                    HNLEDBPGDBC = !inActive
                });
            }
        }

        LastRewardedOrbs.Clear();
        LastSectionRewards.Clear();
        LastHpModifyTimeline.Clear();

        if (!win) return;

        
        Gold += 3;
        Gold += 1;
        AddPlayerExp(2);

        
        
        var route = GridFightLevelResolver.ResolveRoute(this);
        if (route?.NodeType != GridFightNodeTypeEnum.Monster) return;

        var dropInfo = new GridFightDropInfo();
        foreach (var orbId in RollRewardOrbs())
        {
            var orbUniqueId = AllocOrbUniqueId();
            LastRewardedOrbs.Add((orbId, orbUniqueId));
            OrbItemByUniqueId[orbUniqueId] = orbId;
            dropInfo.PIBLJLBCKJL.Add(new LHPPIAKKFME
            {
                BGKDAMDFFKH = GridFightDropType.HiolcnpoponMkppcdpchie,
                JJFFLMCCCMM = orbId,
                Num = 1
            });
        }
        LastSectionRewards[2u] = dropInfo;

        
        var perfectClear = req.Stt?.OGPOFMOGPIP?.GMOBFEBBFIE >= 0; 
        if (req.EndStatus == BattleEndStatus.BattleEndWin && perfectClear)
        {
            LineupHp = Math.Min(LineupMaxHp, LineupHp + 2);
        }

        
        var hpBefore = PreBattleLineupHp;
        foreach (var reason in new[]
        {
            GridFightUpdateGlobalHpReason.BakggpnhnneAhikonfebmj,
            GridFightUpdateGlobalHpReason.BakggpnhnneIkhlilhaiol,
            GridFightUpdateGlobalHpReason.BakggpnhnnePonicjjabek,
            GridFightUpdateGlobalHpReason.BakggpnhnneGpdhibafdcg
        })
        {
            LastHpModifyTimeline.Add(new GridFightHpModifyInfo
            {
                Reason = reason,
                FGEDKOINMAG = (int)hpBefore,
                EHMKLNEKIOE = (int)hpBefore
            });
        }
        LastHpModifyTimeline.Add(new GridFightHpModifyInfo
        {
            Reason = GridFightUpdateGlobalHpReason.BakggpnhnneGfimnccfkik,
            FGEDKOINMAG = (int)PreBattleLineupHp,
            EHMKLNEKIOE = (int)LineupHp,
            HPOPDNGCALL = LineupHp - PreBattleLineupHp,
            PDEKDHPNCEN = 1081
        });
    }

    public GridFightSeasonHandBookNotify BuildHandbookNotifyForBattle()
    {
        var n = new GridFightSeasonHandBookNotify
        {
            HandbookGridFightPortalInfo = new GridFightHandBookPortalInfo()
        };
        n.HandbookGridFightPortalInfo.GridFightPortalBuffList.Add(132u);
        return n;
    }

    public GridFightEndBattleStageNotify BuildEndBattleNotify(bool win)
    {
        var notify = new GridFightEndBattleStageNotify
        {
            BCOLJFHDLLD = LineupHp,
            FGEDKOINMAG = PreBattleLineupHp,
            IJEIEJLPGGJ = LineupMaxHp,
            KDOINLGKMBI = LineupMaxHp,
            DCPKPNLKGMM = CurrentChapterId,
            NDOCIKPLKIF = NDOCIKPLKIF,
            SectionId = SectionId,
            EJEIBPEKHLD = PlayerLevel,
            PNOJLNNHBIH = win ? 4u : 1u,
            JGLEDADBNGP = win ? 1u : 0u,
            HPOACJCPJHN = win,
            PACIAIJBOHO = KeepWinCnt,
            IDEAAPCCFPF = LastBattleIDEAAPCCFPF,
            IPCFJBAKLCG = GetCurrentMaxBattleRoleNum(),
            MAGCGPMHHEA = PlayerMaxLevel,
            GCBBEEGANEG = new MIOMFOAEHEC
            {
                IPHHGMECAKB = new BLKAIEHOCBC
                {
                    Level = PlayerLevel,
                    Exp = PlayerExp,
                    DKHKEJIAJBN = GetLevelUpExpNeed(PlayerLevel)
                },
                IJAEBDCFEMG = new BLKAIEHOCBC
                {
                    Level = PlayerLevel,
                    DKHKEJIAJBN = GetLevelUpExpNeed(PlayerLevel)
                },
                MAGCGPMHHEA = PlayerMaxLevel
            }
        };

        if (LastBattleDamageStt != null)
            notify.GridFightDamageSttInfo = LastBattleDamageStt.Clone();

        foreach (var (sectionIdx, drop) in LastSectionRewards)
            notify.DHMBKAPKJFN[sectionIdx] = drop.Clone();

        foreach (var hp in LastHpModifyTimeline)
            notify.NNFAFGCGMBB.Add(hp.Clone());

        return notify;
    }

    public BattleInstance? StartBattle()
    {
        return GridFightBattleModule.StartBattle(Player, this);
    }

    public uint LastConfiguredBattleSection { get; set; }

    public bool NeedsBattleEncounterConfiguration()
    {
        return LastConfiguredBattleSection != SectionId;
    }

    public void ConfigureNextBattle(uint stageId, IEnumerable<uint> monsterIds)
    {
        BattleComponent.SetEncounter(stageId, monsterIds);
        LastConfiguredBattleSection = SectionId;
    }

    public async ValueTask EndBattle(BattleInstance battle, PVEBattleResultCsReq req)
    {
        var win = req.EndStatus != BattleEndStatus.BattleEndLose;
        RecordBattleSnapshot(req, win);

        if (win) KeepWinCnt++;

        var isSeasonFinish = win && IsFinalSection(CurrentChapterId, SectionId);
        LastSettleReason = win
            ? Proto.GridFightSettleReason.CdphdhnlhaoClnlgbcmoij
            : Proto.GridFightSettleReason.CdphdhnlhaoFlnhnhbcdkm;

        AdvanceQueue(7);
        PendingAction = new GridFightPendingAction
        {
            QueuePosition = QueuePosition,
            RoundBeginAction = new GridFightRoundBeginActionInfo()
        };

        await Player.SendPacket(new PacketGridFightSyncUpdateResultScNotify(Player, GridFightSyncKind.PreSettle));
        await Player.SendPacket(new PacketGridFightEndBattleStageNotify(this, win));

        if (isSeasonFinish)
        {
            if (req.EndStatus == BattleEndStatus.BattleEndWin && Player.DailyActivityManager != null)
                await Player.DailyActivityManager.RecordProgress(2100156, 1);
            BattlesFinished++;
            await Player.SendPacket(new PacketGridFightSyncKeepWinCntNotify(1));
            await Player.SendPacket(new PacketGridFightSettleNotify(Player));
            Player.GridFightManager!.GridFightInstance = null;
            return;
        }

        await Player.SendPacket(new PacketGridFightSyncUpdateResultScNotify(Player, GridFightSyncKind.PostBattle));

        OnBattleResolved(win);
    }

    public GridFightCurrentInfo ToProto()
    {
        EnsureDefaultRoles();
        if (CurrentEquipDraft.Count == 0) RefreshEquipDraft();
        if (ShopGoods.Count == 0) RotateShop();

        var info = new GridFightCurrentInfo
        {
            Season = Season,
            DivisionId = DivisionId,
            IsOverlock = IsOverLock,
            UniqueId = UniqueId,
            PendingAction = PendingAction ?? new GridFightPendingAction
            {
                QueuePosition = QueuePosition,
                PortalBuffAction = new GridFightPortalBuffActionInfo
                {
                    FCHPJKAIBHB = 1,
                    GridFightPortalBuffList = { EnsurePortalBuffOffer() }
                }
            },
            BCHPAOCOHIL = new FCBEHGJBJCN { AMNJHJJMPJF = { CurrentEquipDraft } },
            GridFightGameData = new GridFightGameData
            {
                PGFMICHMHFC =
                {
                    new GridFightGameItemInfo { UniqueId = 4, LMGLPGNACLP = new DCGINOONGLH() },
                    new GridFightGameItemInfo
                    {
                        UniqueId = 8,
                        MDFGMBJONAM = new HJJHNNDAEPF
                        {
                            KIBHCHLKGGC =
                            {
                                [1] = 2, [2] = 2, [3] = 4, [4] = 6, [5] = 20,
                                [6] = 40, [7] = 52, [8] = 72, [9] = 84, [10] = 0
                            }
                        }
                    },
                    new GridFightGameItemInfo { UniqueId = 9 },
                    new GridFightGameItemInfo { UniqueId = 12, ONJFAJOAFOG = new KPNJMMPJDKG() },
                    new GridFightGameItemInfo { UniqueId = 49, CJPMGPEIFGG = new BFBNMHEFKIG { CGDLGFHOECL = 2 } },
                    new GridFightGameItemInfo { UniqueId = 87, CJPMGPEIFGG = new BFBNMHEFKIG { CGDLGFHOECL = 1 } }
                }
            }
        };

        info.RogueCurrentGameInfo.Add(BuildBasicInfoSection());
        info.RogueCurrentGameInfo.Add(BuildTeamSection());
        info.RogueCurrentGameInfo.Add(BuildDraftSection());
        info.RogueCurrentGameInfo.Add(BuildItemsShopSection());
        info.RogueCurrentGameInfo.Add(BuildOrbSection());
        info.RogueCurrentGameInfo.Add(BuildLevelSection());
        
        var augSync = new GridFightGameAugmentSync();
        foreach (var aug in ActiveAugmentIds)
            augSync.SyncAugmentInfo.Add(new GridGameAugmentInfo { AugmentId = aug, MHMLMKDFJLN = true });
        info.RogueCurrentGameInfo.Add(new GridFightGameInfo { GridOrbInfo = augSync });
        info.RogueCurrentGameInfo.Add(BuildAugmentSection());
        info.RogueCurrentGameInfo.Add(BuildTeamGameInfoSection());
        return info;
    }

    private GridFightGameInfo BuildBasicInfoSection() => new()
    {
        GridBasicInfo = new GridFightGameBasicInfo
        {
            ANBBPPHBCJH = 3,
            FLEJPPKLJIC = PlayerLevel,
            HAEOPKELNEO = GetCurrentMaxBattleRoleNum(),
            Gold = Gold,
            GridFightBuyExpCost = GetBuyExpCost(),
            GridFightLineupHp = LineupHp,
            GridFightLineupMaxHp = LineupMaxHp,
            GridFightMaxAvatarCount = 9,
            GridFightMaxInterestGold = 5,
            GridFightOffFieldMaxCount = GetCurrentOffFieldMaxCount(),
            GridFightSyncCurtaskInfo = new GridFightSyncCurrentTaskInfo(),
            GameLockInfo = new GridFightLockInfo()
        }
    };

    private GridFightGameInfo BuildTeamSection()
    {
        var team = new GridFightGameTeamInfo();
        foreach (var (pos, uniqueId) in UniqueIdByPos.OrderBy(kv => kv.Key))
        {
            if (uniqueId == 0 || !RoleByUniqueId.TryGetValue(uniqueId, out var roleKey)) continue;
            team.GridGameRoleList.Add(new GridGameRoleInfo
            {
                Id = GridFightRoleLookup.ToSyncRoleId(roleKey),
                Pos = pos,
                RoleStar = RoleStarByUniqueId.GetValueOrDefault(uniqueId, 1u),
                UniqueId = uniqueId
            });
        }

        PopulatePrepRoleAugmentBindings(team);
        return new GridFightGameInfo { GridTraitGameInfo = team };
    }

    private GridFightGameInfo BuildDraftSection()
    {
        var draft = new GridFightGameItemsInfo();
        foreach (var c in Consumables) draft.GridFightConsumableList.Add(c);
        foreach (var e in Equipments) draft.GridFightEquipmentList.Add(e);
        return new GridFightGameInfo { GridDraftInfo = draft };
    }

    private GridFightGameInfo BuildItemsShopSection() => new()
    {
        GridItemsInfo = new GridFightGameShopInfo
        {
            GLIFNMBMMBL = ShopRefreshLeft,
            DNOIFMMLJDN = { ShopRolePool },
            LDEDGOOKHFL = BuildShopRarityDisplayInfo(),
            ShopGoodsList = { ShopGoods }
        }
    };

    private GridFightGameInfo BuildOrbSection() => new()
    {
        GridLevelInfo = new GridFightGameOrbInfo
        {
            GridGameOrbList =
            {
                OrbItemByUniqueId.Select(kv => new GridGameOrbInfo { UniqueId = kv.Key, OrbItemId = kv.Value })
            }
        }
    };

    private GridFightGameInfo BuildLevelSection()
    {
        var lvl = new GridFightLevelInfo
        {
            DCPKPNLKGMM = CurrentChapterId,
            NDOCIKPLKIF = NDOCIKPLKIF,
            SectionId = SectionId,
            ECCGJDMOGAN = new DDJIOFONKME(),
            BossInfo = new GridFightBossInfo(),
            CMHBDMOJJEN = new IKFEDFBLOOG(),
            GridFightLayerInfo = BuildCurrentLayerInfo()
        };
        EnsureSessionPreview();
        foreach (var campId in SessionCampIds)
            lvl.HGAHMIPIBLO.Add(new OPBCCOLPDPC { PMOGHFIGKPO = campId });
        foreach (var bossId in SessionBossMonsterIds.Where(id => id != 0))
            lvl.BossInfo.IJOPBPABPPM.Add(new PJLBDMPEKFP { MonsterId = bossId, RoleStar = 1 });
        foreach (var affix in SectionAffixIds)
            lvl.IAKFPMOEJLF.Add(new DIBJGAKOCLO { AffixId = affix });
        foreach (var portalBuff in ActivePortalBuffIds)
            lvl.GridFightPortalBuffList.Add(new GridFightGamePortalBuffInfo { PortalBuffId = portalBuff });
        return new GridFightGameInfo { GridShopInfo = lvl };
    }

    private GridFightGameInfo BuildAugmentSection()
    {
        var trait = new GridFightGameTraitInfo();
        trait.ALIDDLBDPDH.Add(new ELEOGABGBKG { DMEKIFJDKFL = 8007, CELFGCJFMPH = { 8007u, 8009u } });
        trait.ALIDDLBDPDH.Add(new ELEOGABGBKG { DMEKIFJDKFL = 11011, CELFGCJFMPH = { 11011u, 11012u } });
        return new GridFightGameInfo { GridAugmentInfo = trait };
    }

    private GridFightGameInfo BuildTeamGameInfoSection() => new()
    {
        GridTeamGameInfo = new LHFDOPGEOML
        {
            ANLGPLOLMOH = 8,
            OGHGLMGJGEM = { ["GP_Avatar_Sparxie_00"] = 2 }
        }
    };

    public List<uint> RollAugments(int count = 3, IEnumerable<uint>? exclude = null)
    {
        var pool = GridFightManager.AugmentPoolKD.ToList();
        if (exclude != null) foreach (var id in exclude) pool.Remove(id);
        var rng = Random.Shared;
        var picked = new List<uint>(count);
        while (picked.Count < count && pool.Count > 0)
        {
            var idx = rng.Next(pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        while (picked.Count < count)
            picked.Add(GridFightManager.AugmentPoolKD[picked.Count % GridFightManager.AugmentPoolKD.Length]);
        CurrentAugmentOffer = picked;
        return CurrentAugmentOffer;
    }

    public void ClearAugmentOffer() => CurrentAugmentOffer = new List<uint>();

    public List<(uint RoleId, uint EquipId)> RollSupplies(int count = 5)
    {
        var rolePool = GameData.GridFightRoleBasicInfoData.Values
            .Where(r => r.IsInPool && (r.SeasonID == Season || r.SeasonID == 0))
            .Where(r => r.AvatarID >= 1000 && r.AvatarID < 2000)
            .Select(r => r.AvatarID)
            .Distinct()
            .ToList();
        if (rolePool.Count == 0)
            rolePool = new List<uint> { 1218, 1304, 1308, 1220, 1014 };

        var equipPool = GameData.GridFightEquipmentData.Values
            .Where(e => e.EquipCategory == Enums.GridFight.GridFightEquipCategoryEnum.Basic)
            .Select(e => e.ID)
            .ToList();
        if (equipPool.Count == 0)
            equipPool = new List<uint> { 350201, 350202, 350203, 350204, 350207 };

        var rng = Random.Shared;
        var roleCopy = rolePool.ToList();
        var picks = new List<(uint, uint)>(count);
        while (picks.Count < count && roleCopy.Count > 0)
        {
            var rIdx = rng.Next(roleCopy.Count);
            var roleId = roleCopy[rIdx];
            roleCopy.RemoveAt(rIdx);
            var equipId = equipPool[rng.Next(equipPool.Count)];
            picks.Add((roleId, equipId));
        }
        while (picks.Count < count)
            picks.Add((rolePool[picks.Count % rolePool.Count], equipPool[picks.Count % equipPool.Count]));
        CurrentSupplyOffer = picks;
        return CurrentSupplyOffer;
    }

    public void ClearSupplyOffer() => CurrentSupplyOffer = new List<(uint, uint)>();

    public List<EliteBranchOption> RollEliteBranchOptions()
    {
        var camp = GameData.GridFightCampData.Values
            .Where(c => c.Monsters.Count > 0 && (c.SeasonID == Season || c.SeasonID == 0))
            .OrderBy(_ => Random.Shared.Next())
            .FirstOrDefault();
        var monsters = camp?.Monsters ?? new List<GridFightMonsterExcel>();

        var route = GridFightLevelResolver.ResolveRoute(this);
        var baseStage = route?.StageID ?? 70000007u;
        var groupBase = (route != null && route.ParamList.Count > 0) ? route.ParamList[0] : 103u;

        var rng = Random.Shared;
        List<uint> RollMonsters(int count, uint maxTier)
        {
            var picks = new List<uint>(count);
            for (var i = 0; i < count; i++)
            {
                if (monsters.Count == 0) break;
                var pool = monsters.Where(m => m.MonsterTier <= maxTier).ToList();
                if (pool.Count == 0) pool = monsters.ToList();
                picks.Add(pool[rng.Next(pool.Count)].MonsterID);
            }
            return picks;
        }

        var qualities = new[] { 1u, 2u, 3u, 4u }
            .OrderBy(_ => rng.Next())
            .Take(2)
            .OrderBy(q => q)
            .ToList();

        var rewardByQuality = new (uint Type, uint Item, uint Count)[]
        {
            (0, 0, 0),
            (1, 2u, 2),        
            (1, 2u, 5),        
            (5, 350103u, 1),   
            (5, 350105u, 1)    
        };

        var chapter = groupBase / 100;
        var variant = (groupBase / 10) % 10;
        var penaltyBase = 91000u + chapter * 100 + variant * 10;
        CurrentEliteBranchOptions = qualities.Select(quality => new EliteBranchOption
        {
            EncounterId = groupBase * 100 + quality,
            StageId = baseStage,
            PenaltyRuleId = penaltyBase + quality,
            MonsterIds = RollMonsters(10, quality <= 2 ? 2u : 3u),
            RewardItemId = rewardByQuality[quality].Item,
            RewardCount = rewardByQuality[quality].Count,
            DifficultyTier = quality
        }).ToList();
        return CurrentEliteBranchOptions;
    }

    public void ClearEliteBranchOptions() => CurrentEliteBranchOptions = new List<EliteBranchOption>();

    public GridFightPendingAction BuildSectionEntryPending(uint queuePos)
    {
        var route = GridFightLevelResolver.ResolveRoute(this);
        if (route == null)
            return new GridFightPendingAction { QueuePosition = queuePos, ReturnPreparationAction = new GridFightReturnPreparationActionInfo() };

        if (route.NodeType == GridFightNodeTypeEnum.Supply && LastSupplyConsumedSection != SectionId)
        {
            if (CurrentSupplyOffer.Count == 0) RollSupplies();
            var supplyInfo = new GridFightSupplyActionInfo
            {
                FCHPJKAIBHB = CurrentSupplyReroll,
                CGFLMCHMBHL = 1
            };
            foreach (var (roleId, equipId) in CurrentSupplyOffer)
            {
                var sup = new GridFightSupplyRoleInfo { RoleId = roleId };
                sup.GridFightItemList.Add(equipId);
                supplyInfo.SupplyRoleInfoList.Add(sup);
            }
            return new GridFightPendingAction
            {
                QueuePosition = queuePos,
                SupplyAction = supplyInfo
            };
        }

        if (route.NodeType == GridFightNodeTypeEnum.EliteBranch && LastEliteBranchConsumedSection != SectionId)
        {
            if (CurrentEliteBranchOptions.Count == 0) RollEliteBranchOptions();
            return new GridFightPendingAction
            {
                QueuePosition = queuePos,
                EliteBranchAction = new GridFightEliteBranchActionInfo { FCHPJKAIBHB = CurrentEliteBranchReroll }
            };
        }

        if (route.NodeType == GridFightNodeTypeEnum.CampMonster && route.IsAugment == 1
            && LastAugmentConsumedSection != SectionId)
        {
            if (CurrentAugmentOffer.Count == 0) RollAugments();
            var augInfo = new GridFightAugmentActionInfo();
            foreach (var aid in CurrentAugmentOffer)
                augInfo.PendingAugmentInfoList.Add(new GridFightPendingAugmentInfo { AugmentId = aid, ALJBADEOPAH = 1 });
            return new GridFightPendingAction
            {
                QueuePosition = queuePos,
                AugmentAction = augInfo
            };
        }

        return new GridFightPendingAction
        {
            QueuePosition = queuePos,
            ReturnPreparationAction = new GridFightReturnPreparationActionInfo()
        };
    }
}
