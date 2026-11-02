using March7thHoney.Data;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Game.GridFight.Sync;
using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Game.GridFight;

public class GridFightManager(PlayerInstance player) : BasePlayerManager(player)
{
    public GridFightInstance? GridFightInstance { get; set; }
    public uint CurUniqueId { get; set; }

    public static readonly uint[] AugmentPoolJL =
    {
        100301, 100401, 100402, 100702, 100901, 101101, 101401, 101601, 101602, 102501, 102901, 103001, 103401, 103701,
        110102, 150101, 150201, 150301, 150901, 151101, 200201, 200301, 200502, 200601, 200701, 200801, 201001, 201201,
        201302, 201601, 202001, 203201, 203302, 210701, 210801, 211001, 220401, 231101, 250601, 250701, 250801, 251301,
        251401, 251601, 252201, 252401, 253101, 253201, 300401, 300701, 301002, 301301, 301401, 301601, 301701, 301901,
        302601, 302701, 320101, 320301, 320401, 330101, 330201, 330901, 331101, 332201, 350101, 350301, 350501, 350601,
        350701, 350901, 351101, 351401, 351601, 351701, 360201
    };

    public static readonly uint[] AugmentPoolKD =
    {
        100101, 100301, 100401, 100402, 100403, 100501, 100601, 100701, 100702, 100901, 101101, 101102, 101201, 101301,
        101401, 101402, 101501, 101601, 101602, 101701, 101801, 101901, 102001, 102101, 102201, 102501, 102502, 102601,
        102801, 102901, 103001, 103201, 103202, 103401, 103402, 103701, 110101, 110102, 130101, 130301, 130401, 130501,
        130701, 131001, 131101, 150101, 150201, 150301, 150601, 150701, 150801, 150901, 151101, 200101, 200201, 200301,
        200502, 200601, 200602, 200701, 200801, 201001, 201102, 201201, 201301, 201302, 201501, 201601, 201801, 201901,
        202001, 202101, 202201, 202301, 202501, 202701, 202901, 203001, 203201, 203302, 210301, 210701, 210801, 211001,
        220401, 220501, 220701, 220801, 230201, 230301, 230401, 230501, 230601, 230801, 230901, 231101, 231201, 231301,
        231601, 250101, 250301, 250401, 250601, 250701, 250801, 250901, 251001, 251201, 251301, 251401, 251501, 251601,
        251801, 252001, 252101, 252201, 252301, 252401, 252501, 252601, 252701, 252801, 252901, 253101, 253201, 260301,
        300101, 300401, 300501, 300701, 300801, 300901, 301001, 301002, 301101, 301301, 301401, 301601, 301701, 301801,
        301901, 302001, 302201, 302601, 302701, 302901, 303001, 303101, 303401, 310501, 310601, 310701, 311101, 320101,
        320201, 320301, 320401, 320801, 320901, 321101, 330101, 330201, 330301, 330401, 330501, 330701, 330801, 330901,
        331001, 331101, 331201, 331301, 331401, 331501, 331601, 331901, 332101, 332201, 350101, 350301, 350402, 350403,
        350501, 350601, 350701, 350801, 350901, 351001, 351101, 351401, 351501, 351601, 351701, 351801, 360201
    };

    private static uint MaxDivisionId() =>
        GameData.GridFightDivisionInfoData.Count > 0
            ? GameData.GridFightDivisionInfoData.Keys.Max()
            : 10940u;

    public GridFightSystemInfo ToProto()
    {
        var divisionId = MaxDivisionId();

        var staticInfo = new GridFightStaticGameInfo
        {
            DivisionId = divisionId,
            CKFIACKHNAE = 1,
            CALIMAKGGHJ = 3,
            Exp = new JIBAKJGOPJM(),
            OJLAODIALLE = new GridFightTalentInfo(),
            EJCFDAABLOC = new GridFightHandBookInfo
            {
                GridFightMonsterInfo = new GridFightHandBookMonsterInfo(),
                GridFightRoleInfo = new GridFightHandBookRoleInfo(),
                GridFightPortalInfo = new GridFightHandBookPortalInfo(),
                GridFightEquipInfo = new GridFightHandBookEquipInfo(),
                GridFightAugmentInfo = new GridFightHandBookAugmentInfo()
            }
        };

        if (GameData.GridFightSeasonTalentData.Count > 0)
            staticInfo.OJLAODIALLE.DANAGDAPKJE.Add(GameData.GridFightSeasonTalentData.Keys.OrderBy(x => x));
        else
            staticInfo.OJLAODIALLE.DANAGDAPKJE.Add([2011, 2021, 2022, 2031, 2032, 2033, 2041, 2042, 2043, 2044, 2051, 2061, 2062, 2063, 3011, 3021, 3022, 3031, 3032, 3033, 3041, 3042, 3043, 3044, 3051, 3061, 3062, 3063, 4011, 4021, 4022, 4031, 4032, 4041, 4042, 4043, 4051, 4052, 4053, 4061]);
        staticInfo.EJCFDAABLOC.GridFightMonsterInfo.HGAHMIPIBLO.Add([1, 2, 4, 5, 6, 8, 10, 11, 12, 13, 15, 16, 17, 23, 24, 25, 27]);
        staticInfo.EJCFDAABLOC.GridFightRoleInfo.GridFightAvatarList.Add([1001, 1003, 1004, 1005, 1009, 1013, 1014, 1015, 1105, 1108, 1112, 1202, 1203, 1204, 1205, 1208, 1209, 1212, 1213, 1217, 1218, 1220, 1221, 1222, 1223, 1225, 1301, 1302, 1303, 1304, 1305, 1306, 1307, 1308, 1309, 1310, 1313, 1314, 1317, 1401, 1402, 1403, 1404, 1405, 1406, 1407, 1408, 1409, 1410, 1412, 1413, 1414, 1415, 1501, 1502, 1505, 8007, 8009, 11011, 11012, 15061, 15062, 15063]);
        staticInfo.EJCFDAABLOC.GridFightPortalInfo.PELJLONLDNM.Add([101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 127, 129, 134, 135, 138, 147, 1001, 1002, 1003, 1004, 1005, 1007, 1008, 1010, 1014, 1016, 1101, 1102, 1104, 1106, 1107, 1112, 1113, 1114, 1115, 1116, 1118]);
        staticInfo.EJCFDAABLOC.GridFightPortalInfo.GridFightPortalBuffList.Add([101, 105, 106, 107, 108, 109, 110, 112, 113, 115, 120, 121, 123, 124, 127, 129, 138, 1002, 1003, 1004, 1007, 1014, 1016, 1112, 1113, 1116]);
        staticInfo.EJCFDAABLOC.GridFightEquipInfo.GridFightItemList.Add([99990, 99991, 99992, 99998, 99999, 350101, 350102, 350103, 350104, 350105, 350106, 350107, 350201, 350202, 350203, 350204, 350205, 350206, 350207, 350208, 350601, 350602, 350701, 352101, 352502, 35030101, 35030102, 35030103, 35030104, 35030105, 35030106, 35030107, 35030108, 35030202, 35030203, 35030204, 35030205, 35030206, 35030207, 35030208, 35030303, 35030304, 35030305, 35030306, 35030307, 35030308, 35030404, 35030405, 35030406, 35030407, 35030408, 35030505, 35030506, 35030507, 35030508, 35030606, 35030607, 35030608, 35030707, 35030708, 35030808, 35040101, 35051001, 35051006, 35051007, 35051008, 35051010, 35051012, 35052001, 35052002, 35052003, 35052004, 35052008, 35052009, 35100001, 35100002]);
        staticInfo.EJCFDAABLOC.GridFightAugmentInfo.JLCDEDKPEAB.Add(AugmentPoolJL);
        staticInfo.EJCFDAABLOC.GridFightAugmentInfo.KDFBCMANFMB.Add(AugmentPoolKD);

        var system = new GridFightSystemInfo
        {
            EGLCKGKECAJ = staticInfo,
            FCKKGFFLDFA = new MIGEAHDEBOE { OJLAODIALLE = new GridFightTalentInfo() }
        };
        if (GameData.GridFightTalentData.Count > 0)
            system.FCKKGFFLDFA.OJLAODIALLE.DANAGDAPKJE.Add(GameData.GridFightTalentData.Keys.OrderBy(x => x));
        else
            system.FCKKGFFLDFA.OJLAODIALLE.DANAGDAPKJE.Add([1011, 1021, 1022, 1031, 1041, 1042, 1051, 1061, 1062, 1071, 1072, 1073, 1081]);
        return system;
    }

    public GridFightSyncUpdateResultScNotify BuildSyncUpdateNotify(
        IEnumerable<GridFightPosInfo>? updatedPosList = null,
        int kind = GridFightSyncKind.Bootstrap,
        object? extra = null)
    {
        if (kind == GridFightSyncKind.PosUpdate)
            return BuildPosUpdateSync(extra);
        if (updatedPosList != null)
        {
            return BuildPosUpdateSync(new PosUpdateSyncPayload
            {
                UpdatedPosList = updatedPosList.ToList()
            });
        }
        return kind switch
        {
            GridFightSyncKind.Bootstrap      => BuildBootstrapSync(),
            GridFightSyncKind.PendingAdvance => BuildPendingAdvanceSync(extra),
            GridFightSyncKind.SelectEquip    => BuildSelectEquipSync(extra),
            GridFightSyncKind.BuyGoods       => BuildBuyGoodsSync(extra),
            GridFightSyncKind.RefreshShop    => BuildRefreshShopSync(),
            GridFightSyncKind.RecycleRole    => BuildRecycleRoleSync(extra),
            GridFightSyncKind.BuyExp         => BuildBuyExpSync(),
            GridFightSyncKind.PostBattle     => BuildPostBattleSync(),
            GridFightSyncKind.PreSettle      => BuildPreSettleSync(),
            GridFightSyncKind.Preparation    => BuildPreparationStateSync(),
            GridFightSyncKind.NoOp           => new GridFightSyncUpdateResultScNotify(),
            _                                => new GridFightSyncUpdateResultScNotify()
        };
    }

    private GridFightSyncUpdateResultScNotify BuildPosUpdateSync(object? extra)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        if (inst == null) return notify;

        List<GridFightPosInfo> updatedPosList = [];
        List<GridFightInstance.RoleMergeResult> merges = [];
        if (extra is PosUpdateSyncPayload payload)
        {
            updatedPosList = payload.UpdatedPosList;
            merges = payload.Merges;
        }
        else if (extra is ValueTuple<IEnumerable<GridFightPosInfo>, List<GridFightInstance.RoleMergeResult>> tuple)
        {
            updatedPosList = tuple.Item1.ToList();
            merges = tuple.Item2;
        }
        else if (extra is IEnumerable<GridFightPosInfo> list)
        {
            updatedPosList = list.ToList();
        }

        var sync = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpHndkhmefaal };
        var synced = new HashSet<uint>();
        foreach (var posInfo in updatedPosList)
        {
            if (posInfo.UniqueId == 0 || !synced.Add(posInfo.UniqueId)) continue;
            var rolePos = posInfo.Pos > 0
                ? posInfo.Pos
                : inst.UniqueIdByPos.FirstOrDefault(kv => kv.Value == posInfo.UniqueId).Key;
            if (rolePos == 0) continue;
            sync.UpdateDynamicList.Add(new GridFightSyncData
            {
                UpdateRoleInfo = inst.BuildGridGameRoleInfo(posInfo.UniqueId, rolePos)
            });
        }

        notify.SyncResultDataList.Add(sync);
        return notify;
    }

    /// <summary>
    /// 构建升星过渡动画 sync（数据与粒子参数同段，不含 RemoveRoleUniqueId 与最终星级）。
    /// </summary>
    public GridFightSyncUpdateResultScNotify BuildRoleMergeAnimationNotify(
        GridFightInstance.RoleMergeResult merge,
        GridFightUpdateSrcType updateSrc)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        if (inst == null || !merge.Merged) return notify;

        var sec = new GridFightSyncResultData
        {
            GridUpdateSrc = updateSrc,
            SyncEffectParamList = { merge.KeptUniqueId, merge.NewStar }
        };
        AppendRoleMergeAnimationEntries(sec, inst, merge);
        notify.SyncResultDataList.Add(sec);
        return notify;
    }

    /// <summary>
    /// 构建升星收尾 sync（移除消耗卡、写入最终星级并清理备战席空位）。
    /// </summary>
    public GridFightSyncUpdateResultScNotify BuildRoleMergeFinalizeNotify(
        GridFightInstance.RoleMergeResult merge)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        if (inst == null || !merge.Merged) return notify;

        var sec = new GridFightSyncResultData
        {
            GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpHndkhmefaal
        };
        AppendRoleMergeFinalizeEntries(sec, inst, merge);
        AppendEmptyBenchSlotClears(sec, inst);
        notify.SyncResultDataList.Add(sec);
        return notify;
    }

    /// <summary>
    /// 向 sync 段追加升星动画触发字段（不下发移除与最终星级，避免客户端瞬间跳变）。
    /// </summary>
    private static void AppendRoleMergeAnimationEntries(
        GridFightSyncResultData sec,
        GridFightInstance inst,
        GridFightInstance.RoleMergeResult merge)
    {
        if (!merge.Merged) return;

        var mergeCtx = new CMCJNKPKBEM();
        foreach (var (uid, pos) in merge.ParticipantPosByUniqueId)
            mergeCtx.CFNPGNMPNDN[uid.ToString()] = pos;

        if (!merge.ParticipantPosByUniqueId.ContainsKey(merge.KeptUniqueId))
        {
            var keepPos = inst.UniqueIdByPos.FirstOrDefault(kv => kv.Value == merge.KeptUniqueId).Key;
            if (keepPos > 0)
                mergeCtx.CFNPGNMPNDN[merge.KeptUniqueId.ToString()] = keepPos;
        }

        sec.UpdateDynamicList.Add(new GridFightSyncData { CFNPGNMPNDN = mergeCtx });

        foreach (var snapshot in merge.PreMergeRoleSnapshots.Values.OrderBy(s => s.Pos))
            sec.UpdateDynamicList.Add(new GridFightSyncData { UpdateRoleInfo = snapshot });

        sec.UpdateDynamicList.Add(new GridFightSyncData { HLFBBANMJDJ = merge.KeptUniqueId });
        sec.UpdateDynamicList.Add(new GridFightSyncData { GDPBJDHGFLB = merge.NewStar });
        sec.UpdateDynamicList.Add(new GridFightSyncData
        {
            AJIMOAMGCII = GridFightRoleLookup.ToSyncRoleId(merge.RoleId)
        });
    }

    /// <summary>
    /// 向 sync 段追加升星收尾字段（移除消耗卡并同步保留卡最终星级）。
    /// </summary>
    private static void AppendRoleMergeFinalizeEntries(
        GridFightSyncResultData sec,
        GridFightInstance inst,
        GridFightInstance.RoleMergeResult merge)
    {
        if (!merge.Merged) return;

        foreach (var removedUid in merge.RemovedUniqueIds)
            sec.UpdateDynamicList.Add(new GridFightSyncData { RemoveRoleUniqueId = removedUid });

        var keeperInfo = merge.FinalKeeperRoleInfo ?? inst.BuildGridGameRoleInfo(merge.KeptUniqueId);
        if (keeperInfo.UniqueId > 0)
        {
            sec.UpdateDynamicList.Add(new GridFightSyncData { UpdateRoleInfo = keeperInfo });
        }
    }

    /// <summary>
    /// 构建购买/复制 AddRoleInfo；升星消耗新卡时仍返回合成前快照，供客户端先看到三张参与卡。
    /// </summary>
    public static GridGameRoleInfo BuildPurchasedRoleAddInfo(
        GridFightInstance inst,
        uint roleUniqueId,
        uint pos,
        IEnumerable<GridFightInstance.RoleMergeResult> buyMerges)
    {
        foreach (var merge in buyMerges.Where(m => m.Merged))
        {
            if (merge.PreMergeRoleSnapshots.TryGetValue(roleUniqueId, out var snapshot))
                return snapshot;
        }

        return inst.BuildGridGameRoleInfo(roleUniqueId, pos);
    }

    /// <summary>
    /// 构建购买/复制后备战席压缩 sync；升星参与卡改由动画包下发，避免提前写入最终星级。
    /// </summary>
    public static GridGameRoleInfo? TryBuildBenchRepositionRoleInfo(
        GridFightInstance inst,
        GridFightPosInfo benchPos,
        IEnumerable<GridFightInstance.RoleMergeResult> buyMerges)
    {
        if (benchPos.UniqueId == 0 || !inst.RoleByUniqueId.ContainsKey(benchPos.UniqueId))
            return null;

        foreach (var merge in buyMerges.Where(m => m.Merged))
        {
            if (merge.RemovedUniqueIds.Contains(benchPos.UniqueId))
                return null;
            if (merge.ParticipantPosByUniqueId.ContainsKey(benchPos.UniqueId))
                return null;
        }

        var roleInfo = inst.BuildGridGameRoleInfo(benchPos.UniqueId, benchPos.Pos);
        return roleInfo.UniqueId == 0 ? null : roleInfo;
    }

    /// <summary>
    /// 追加出战席布局 sync（MaxBattleRoleNum + GridFightOffFieldMaxCount，无条件 13 格）。
    /// </summary>
    private static void AppendBattlefieldMetaSync(GridFightSyncResultData sync, GridFightInstance inst) =>
        GridFightInstance.AppendBattlefieldLayoutSync(sync, inst);

    /// <summary>
    /// 对备战席空位下发占位清除，避免客户端仍保留已移除角色的隐式占用。
    /// </summary>
    private static void AppendEmptyBenchSlotClears(GridFightSyncResultData sec, GridFightInstance inst)
    {
        var occupiedBenchPos = inst.UniqueIdByPos
            .Where(kv => kv.Key is >= GridFightInstance.BenchPosMin and <= GridFightInstance.BenchPosMax && kv.Value != 0)
            .Select(kv => kv.Key)
            .ToHashSet();

        for (uint pos = GridFightInstance.BenchPosMin; pos <= GridFightInstance.BenchPosMax; pos++)
        {
            if (occupiedBenchPos.Contains(pos)) continue;
            sec.UpdateDynamicList.Add(new GridFightSyncData
            {
                UpdateRoleInfo = new GridGameRoleInfo { Pos = pos, UniqueId = 0, Id = 0, RoleStar = 0 }
            });
        }
    }

    private GridFightSyncUpdateResultScNotify BuildPendingAdvanceSync(object? extra)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        var (oldPos, newPos) = extra is ValueTuple<uint, uint> t ? t : (0u, 0u);

        
        if (oldPos > 0 && newPos > oldPos)
        {
            var finishSync = new GridFightSyncResultData();
            finishSync.UpdateDynamicList.Add(new GridFightSyncData { FinishPendingActionPos = oldPos });
            finishSync.UpdateDynamicList.Add(new GridFightSyncData { SyncLockInfo = new GridFightLockInfo() });
            notify.SyncResultDataList.Add(finishSync);
        }

        if (inst?.PendingAction != null)
        {
            var pendingSync = new GridFightSyncResultData();
            pendingSync.UpdateDynamicList.Add(new GridFightSyncData
            {
                SyncLockInfo = new GridFightLockInfo
                {
                    LockReason = GridFightLockReason.DfofffceffoKjmjdbjmbmc,
                    LockType = GridFightLockType.PjbmhhnlclbEhfhdgpocnh
                }
            });
            pendingSync.UpdateDynamicList.Add(new GridFightSyncData { PendingAction = inst.PendingAction });
            notify.SyncResultDataList.Add(pendingSync);
        }
        return notify;
    }

    /// <summary>
    /// Pushes gold, HP, chapter/section, shop and deployment cap when entering preparation.
    /// </summary>
    private GridFightSyncUpdateResultScNotify BuildPreparationStateSync()
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        if (inst == null) return notify;

        notify.SyncResultDataList.Add(BuildPreparationSyncResultData(inst));
        return notify;
    }

    /// <summary>
    /// 构建返回备战界面的合并 sync：解锁、完整备战状态、商店打开段（单包下发，避免自动开商店时状态不同步）。
    /// </summary>
    public GridFightSyncUpdateResultScNotify BuildReturnPreparationNotify(uint ackPos)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        if (inst == null) return notify;

        EnsureNextBattleConfigured(inst);

        var finishSync = new GridFightSyncResultData();
        finishSync.UpdateDynamicList.Add(new GridFightSyncData { FinishPendingActionPos = ackPos });
        finishSync.UpdateDynamicList.Add(new GridFightSyncData { SyncLockInfo = new GridFightLockInfo() });
        notify.SyncResultDataList.Add(finishSync);
        notify.SyncResultDataList.Add(BuildPreparationSyncResultData(inst));
        notify.SyncResultDataList.Add(BuildShopOpenSyncResultData(inst));
        return notify;
    }

    /// <summary>
    /// 在下发备战 sync 前解析并缓存下一战遭遇，确保节点地图与商店 UI 绑定正确关卡。
    /// </summary>
    private static void EnsureNextBattleConfigured(GridFightInstance inst)
    {
        if (!GridFightLevelResolver.IsCombatNode(inst) || !inst.NeedsBattleEncounterConfiguration())
            return;

        var encounter = GridFightLevelResolver.Resolve(inst);
        inst.ConfigureNextBattle(encounter.StageId, encounter.Monsters.Select(m => m.MonsterId));
    }

    /// <summary>
    /// 构建进入备战阶段的完整状态 sync 段。
    /// </summary>
    private GridFightSyncResultData BuildPreparationSyncResultData(GridFightInstance inst)
    {
        var sync = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpEjkejdnhioe };
        sync.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = inst.Gold });
        sync.UpdateDynamicList.Add(new GridFightSyncData { ShopSyncInfo = BuildShopSyncInfo(inst) });
        sync.UpdateDynamicList.Add(new GridFightSyncData
        {
            GridFightLineupHp = new GridFightLineupHpSyncInfo
            {
                GridFightLineupHp = inst.LineupHp,
                GridFightLineupMaxHp = inst.LineupMaxHp
            }
        });
        sync.UpdateDynamicList.Add(new GridFightSyncData
        {
            LevelSyncInfo = new GridFightLevelSyncInfo
            {
                DCPKPNLKGMM = inst.CurrentChapterId,
                SectionId = inst.SectionId,
                GridFightLayerInfo = inst.BuildCurrentLayerInfo()
            }
        });
        sync.UpdateDynamicList.Add(new GridFightSyncData
        {
            PlayerLevel = new GridFightPlayerLevelSyncInfo
            {
                Level = inst.PlayerLevel,
                Exp = inst.PlayerExp,
                MaxLevel = inst.PlayerMaxLevel
            }
        });
        AppendBattlefieldMetaSync(sync, inst);
        return sync;
    }

    /// <summary>
    /// 构建客户端打开商店界面所需的 sync 段（与购买后刷新商店格式一致）。
    /// </summary>
    private GridFightSyncResultData BuildShopOpenSyncResultData(GridFightInstance inst)
    {
        var sync = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpDpekjiiicgh };
        sync.SyncEffectParamList.Add(0u);
        sync.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = inst.Gold });
        sync.UpdateDynamicList.Add(new GridFightSyncData { ShopSyncInfo = BuildShopSyncInfo(inst) });
        return sync;
    }

    private GridFightSyncUpdateResultScNotify BuildSelectEquipSync(object? extra)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var sync = new GridFightSyncResultData();
        if (extra is ValueTuple<uint, uint> ids)
        {
            var (equipId, equipUniqueId) = ids;
            if (equipId > 0)
            {
                sync.UpdateDynamicList.Add(new GridFightSyncData
                {
                    GMJLJDJDIGM = new JPBCKCDEGOM()
                });
                _ = equipUniqueId;
            }
        }
        if (GridFightInstance != null)
        {
            sync.UpdateDynamicList.Add(new GridFightSyncData { FinishPendingActionPos = GridFightInstance.QueuePosition });
        }
        notify.SyncResultDataList.Add(sync);
        return notify;
    }

    private GridFightSyncUpdateResultScNotify BuildBuyGoodsSync(object? extra)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        if (inst == null || extra == null)
        {
            notify.SyncResultDataList.Add(new GridFightSyncResultData());
            return notify;
        }

        uint roleId;
        uint roleUniqueId;
        uint pos;
        List<GridFightInstance.RoleMergeResult> buyMerges = [];
        List<GridFightPosInfo> benchRepositions = [];
        uint shopIndex = 0;
        uint purchasedAugmentId = 0;
        uint purchasedAugmentTargetUniqueId = 0;
        if (extra is BuyGoodsSyncPayload payload)
        {
            roleId = payload.RoleId;
            roleUniqueId = payload.RoleUniqueId;
            pos = payload.Pos;
            buyMerges = payload.Merges;
            benchRepositions = payload.BenchRepositions;
            shopIndex = payload.ShopIndex;
            purchasedAugmentId = payload.PurchasedAugmentId;
            purchasedAugmentTargetUniqueId = payload.PurchasedAugmentTargetUniqueId;
        }
        else if (extra is ValueTuple<uint, uint, uint, int, List<uint>, uint, uint> v2)
        {
            (roleId, roleUniqueId, pos, _, var mergedRemoved, var mergedKeepUid, _) = v2;
            if (mergedRemoved.Count > 0 && mergedKeepUid > 0)
            {
                var legacyMerge = new GridFightInstance.RoleMergeResult
                {
                    KeptUniqueId = mergedKeepUid,
                    NewStar = inst.RoleStarByUniqueId.GetValueOrDefault(mergedKeepUid, 1u),
                    RoleId = roleId
                };
                foreach (var uid in mergedRemoved)
                    legacyMerge.RemovedUniqueIds.Add(uid);
                buyMerges.Add(legacyMerge);
            }
        }
        else if (extra is ValueTuple<uint, uint, uint, int> v1)
        {
            (roleId, roleUniqueId, pos, _) = v1;
        }
        else
        {
            notify.SyncResultDataList.Add(new GridFightSyncResultData());
            return notify;
        }

        var sec0 = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpDpekjiiicgh };
        sec0.SyncEffectParamList.Add(shopIndex);
        sec0.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = inst.Gold });
        foreach (var benchPos in benchRepositions)
        {
            var roleInfo = TryBuildBenchRepositionRoleInfo(inst, benchPos, buyMerges);
            if (roleInfo == null) continue;
            sec0.UpdateDynamicList.Add(new GridFightSyncData { UpdateRoleInfo = roleInfo });
        }
        if (roleUniqueId > 0)
        {
            sec0.UpdateDynamicList.Add(new GridFightSyncData
            {
                AddRoleInfo = BuildPurchasedRoleAddInfo(inst, roleUniqueId, pos, buyMerges)
            });
        }

        if (purchasedAugmentId > 0 && purchasedAugmentTargetUniqueId > 0)
        {
            sec0.UpdateDynamicList.Add(new GridFightSyncData { HLFBBANMJDJ = purchasedAugmentTargetUniqueId });
            sec0.UpdateDynamicList.Add(new GridFightSyncData
            {
                UpdateRoleInfo = inst.BuildGridGameRoleInfo(purchasedAugmentTargetUniqueId)
            });
            sec0.UpdateDynamicList.Add(new GridFightSyncData
            {
                GridGameAugmentUpdate = new GridFightGameAugmentUpdate
                {
                    UpdateAugmentInfo = new GridGameAugmentInfo
                    {
                        AugmentId = purchasedAugmentId,
                        NDCFBKJDPAH = true,
                        MHMLMKDFJLN = true
                    }
                }
            });
            GridFightInstance.AppendRoleAugmentBindingSync(sec0, inst, purchasedAugmentTargetUniqueId, purchasedAugmentId);
        }

        AppendBattlefieldMetaSync(sec0, inst);
        notify.SyncResultDataList.Add(sec0);

        var sec1 = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpDpekjiiicgh };
        sec1.SyncEffectParamList.Add(0u);
        sec1.UpdateDynamicList.Add(new GridFightSyncData { ShopSyncInfo = BuildShopSyncInfo(inst) });
        notify.SyncResultDataList.Add(sec1);

        return notify;
    }

    /// <summary>
    /// 构建商店刷新 sync（syncEffectParam：0 常规，1 免费刷新动画）。
    /// </summary>
    public GridFightSyncUpdateResultScNotify BuildShopRefreshNotify(uint syncEffectParam = 0)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        if (inst == null) return notify;

        var sec = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpDpekjiiicgh };
        sec.SyncEffectParamList.Add(syncEffectParam);
        sec.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = inst.Gold });
        sec.UpdateDynamicList.Add(new GridFightSyncData { ShopSyncInfo = BuildShopSyncInfo(inst) });
        notify.SyncResultDataList.Add(sec);
        return notify;
    }

    private static GridFightShopSyncInfo BuildShopSyncInfo(GridFightInstance inst)
    {
        var shopSync = new GridFightShopSyncInfo
        {
            GLIFNMBMMBL = inst.ShopRefreshLeft,
            LDEDGOOKHFL = inst.BuildShopRarityDisplayInfo(),
        };
        foreach (var goods in inst.ShopGoods)
            shopSync.ShopGoodsList.Add(goods);
        return shopSync;
    }

    private GridFightSyncUpdateResultScNotify BuildRefreshShopSync()
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        if (inst == null) return notify;
        var sync = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpEjkejdnhioe };
        sync.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = inst.Gold });
        sync.UpdateDynamicList.Add(new GridFightSyncData { ShopSyncInfo = BuildShopSyncInfo(inst) });
        notify.SyncResultDataList.Add(sync);
        return notify;
    }

    private GridFightSyncUpdateResultScNotify BuildRecycleRoleSync(object? extra)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var sync = new GridFightSyncResultData();
        if (extra is ValueTuple<uint, int> info)
        {
            var (removedUniqueId, refund) = info;
            sync.UpdateDynamicList.Add(new GridFightSyncData { RemoveRoleUniqueId = removedUniqueId });
            sync.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = (uint)Math.Max(0, GridFightInstance?.Gold ?? 0) });
            _ = refund;
        }
        notify.SyncResultDataList.Add(sync);
        return notify;
    }

    private GridFightSyncUpdateResultScNotify BuildBuyExpSync()
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var sync = new GridFightSyncResultData();
        var inst = GridFightInstance;
        sync.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = (uint)Math.Max(0, inst?.Gold ?? 0) });
        if (inst != null)
        {
            sync.UpdateDynamicList.Add(new GridFightSyncData
            {
                PlayerLevel = new GridFightPlayerLevelSyncInfo
                {
                    Level = inst.PlayerLevel,
                    Exp = inst.PlayerExp,
                    MaxLevel = inst.PlayerMaxLevel
                }
            });
            sync.UpdateDynamicList.Add(new GridFightSyncData
            {
                GridFightBuyExpCost = inst.GetBuyExpCost()
            });
            GridFightInstance.AppendBattlefieldLayoutSync(sync, inst);
            sync.UpdateDynamicList.Add(new GridFightSyncData
            {
                ShopSyncInfo = new GridFightShopSyncInfo { LDEDGOOKHFL = inst.BuildShopRarityDisplayInfo() }
            });
        }
        notify.SyncResultDataList.Add(sync);
        return notify;
    }

    private GridFightSyncUpdateResultScNotify BuildPostBattleSync()
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        if (inst == null) return notify;

        var levelSync = new GridFightSyncResultData();

        var probeSection = inst.SectionId + 1;
        var probeChapter = inst.CurrentChapterId;
        var maxSection = inst.GetChapterSectionCount(probeChapter);
        if (maxSection > 0 && probeSection > maxSection)
        {
            probeChapter++;
            probeSection = 1;
        }
        var prevSection = inst.SectionId;
        var prevChapter = inst.CurrentChapterId;
        inst.SectionId = probeSection;
        inst.CurrentChapterId = probeChapter;
        var nextRoute = GridFightLevelResolver.ResolveRoute(inst);
        var nextType = nextRoute?.NodeType;
        var isEliteBranch = nextType == Enums.GridFight.GridFightNodeTypeEnum.EliteBranch;
        var isSupply = nextType == Enums.GridFight.GridFightNodeTypeEnum.Supply;
        var routeInfo = new GridFightRouteInfo
        {
            FightCampId = isEliteBranch ? 1u : inst.CampId
        };
        if (isEliteBranch)
        {
            inst.RollEliteBranchOptions();
            foreach (var opt in inst.CurrentEliteBranchOptions)
            {
                var enc = new GridFightEncounterInfo
                {
                    BAGCBHFJIMN = opt.EncounterId,
                    LFKBMDHKPFI = opt.PenaltyRuleId
                };
                if (opt.DifficultyTier > 1) enc.GDOEOGMJDAO = (opt.DifficultyTier - 1) * 10u;
                var w = new GridEncounterMonsterWave { IGMMPDDCJIN = 1 };
                foreach (var mid in opt.MonsterIds)
                    w.PPOEDDFFEKK.Add(new PJLBDMPEKFP { MonsterId = mid, RoleStar = 1 });
                enc.MonsterWaveList.Add(w);
                var drop = new GridFightDropInfo();
                var isItemReward = opt.RewardItemId >= 1000;
                var dropItem = new LHPPIAKKFME
                {
                    BGKDAMDFFKH = isItemReward ? GridFightDropType.HiolcnpoponBikhoegfefd : GridFightDropType.HiolcnpoponHhnbgnfbdho,
                    Num = opt.RewardCount
                };
                if (isItemReward) dropItem.JJFFLMCCCMM = opt.RewardItemId;
                drop.PIBLJLBCKJL.Add(dropItem);
                enc.LMLAOPMDCCA = drop;
                routeInfo.RouteEncounterList.Add(enc);
            }
        }
        else if (!isSupply)
        {
            var nextEncounter = GridFightLevelResolver.Resolve(inst);
            var encounter = new GridFightEncounterInfo { LFKBMDHKPFI = nextEncounter.StageId };
            var wave = new GridEncounterMonsterWave { IGMMPDDCJIN = 1 };
            foreach (var spec in nextEncounter.Monsters)
                wave.PPOEDDFFEKK.Add(new PJLBDMPEKFP { MonsterId = spec.MonsterId, RoleStar = 1 });
            encounter.MonsterWaveList.Add(wave);
            routeInfo.RouteEncounterList.Add(encounter);
        }
        inst.SectionId = prevSection;
        inst.CurrentChapterId = prevChapter;

        var levelInfo = new GridFightLevelSyncInfo
        {
            DCPKPNLKGMM = probeChapter,
            SectionId = probeSection,
            GridFightLayerInfo = new GridFightLayerInfo { RouteInfo = routeInfo }
        };
        levelSync.UpdateDynamicList.Add(new GridFightSyncData { LevelSyncInfo = levelInfo });
        notify.SyncResultDataList.Add(levelSync);

        var lockSync = new GridFightSyncResultData();
        lockSync.UpdateDynamicList.Add(new GridFightSyncData
        {
            SyncLockInfo = new GridFightLockInfo
            {
                LockReason = GridFightLockReason.DfofffceffoKjmjdbjmbmc,
                LockType = GridFightLockType.PjbmhhnlclbEhfhdgpocnh
            }
        });
        lockSync.UpdateDynamicList.Add(new GridFightSyncData
        {
            PendingAction = inst.PendingAction ?? new GridFightPendingAction
            {
                QueuePosition = inst.QueuePosition,
                RoundBeginAction = new GridFightRoundBeginActionInfo()
            }
        });
        notify.SyncResultDataList.Add(lockSync);

        return notify;
    }

    private GridFightSyncUpdateResultScNotify BuildPreSettleSync()
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var inst = GridFightInstance;
        if (inst == null) return notify;

        var unlock = new GridFightSyncResultData();
        unlock.UpdateDynamicList.Add(new GridFightSyncData { SyncLockInfo = new GridFightLockInfo() });
        notify.SyncResultDataList.Add(unlock);

        var stt = new GridFightSyncResultData
        {
            GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpJalhdinecfe
        };
        stt.SyncEffectParamList.Add(1u);
        stt.SyncEffectParamList.Add(inst.SectionId);
        if (inst.LastBattleDamageStt != null)
            stt.UpdateDynamicList.Add(new GridFightSyncData { GridFightDamageSttInfo = inst.LastBattleDamageStt.Clone() });
        stt.UpdateDynamicList.Add(new GridFightSyncData
        {
            GridFightLineupHp = new GridFightLineupHpSyncInfo
            {
                GridFightLineupHp = inst.LineupHp,
                GridFightLineupMaxHp = inst.LineupMaxHp
            }
        });
        stt.UpdateDynamicList.Add(new GridFightSyncData
        {
            SectionRecordSyncInfo = new GridFightSectionRecordInfo
            {
                DCPKPNLKGMM = inst.CurrentChapterId,
                SectionId = inst.SectionId,
                CampRecordInfo = new GridFightSectionCampRecordInfo { PMOGHFIGKPO = inst.CampId }
            }
        });
        notify.SyncResultDataList.Add(stt);

        var rewards = new GridFightSyncResultData
        {
            GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpJalhdinecfe
        };
        rewards.SyncEffectParamList.Add(1u);
        rewards.SyncEffectParamList.Add(inst.SectionId);
        rewards.UpdateDynamicList.Add(new GridFightSyncData
        {
            PlayerLevel = new GridFightPlayerLevelSyncInfo
            {
                Level = inst.PlayerLevel,
                Exp = inst.PlayerExp,
                MaxLevel = inst.PlayerMaxLevel
            }
        });
        if (inst.Gold > inst.PreBattleGold)
        {
            rewards.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = inst.PreBattleGold + 3u });
            rewards.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = inst.Gold });
        }
        foreach (var (itemId, uniqueId) in inst.LastRewardedOrbs)
        {
            rewards.UpdateDynamicList.Add(new GridFightSyncData
            {
                OrbSyncInfo = new GridFightOrbSyncInfo { OrbItemId = itemId, UniqueId = uniqueId }
            });
        }
        notify.SyncResultDataList.Add(rewards);

        return notify;
    }

    private GridFightSyncUpdateResultScNotify BuildBootstrapSync()
    {
        return GridFightBootstrapSyncBuilder.Build();
    }

    public GridFightSettleNotify BuildSettleNotify(GridFightSettleReason? overrideReason = null)
    {
        var inst = GridFightInstance;
        var chapterId = inst?.CurrentChapterId ?? 1u;
        var reason = overrideReason ?? inst?.LastSettleReason ?? GridFightSettleReason.CdphdhnlhaoClnlgbcmoij;
        var notify = new GridFightSettleNotify
        {
            BHLDAEKNMCD = 140,
            EDKIICIKJKL = MaxDivisionId(),
            OHOPKAAKOGF = MaxDivisionId(),
            EDKJMPACHNJ = new GridFightFinishInfo
            {
                Reason = reason,
                HBNHKPDMGIP = chapterId,
                BCHPAOCOHIL = new FCBEHGJBJCN
                {
                    AMNJHJJMPJF = { inst?.CurrentEquipDraft ?? new List<uint> { 35030205u, 35030405u, 35030208u } }
                },
                NLILNONCNFC = new JCFJADFEOJN
                {
                    BBDOCJGAEEJ = inst?.DivisionId ?? MaxDivisionId(),
                    BCOLJFHDLLD = inst?.LineupHp ?? 80,
                    BFNPCJOMGFL = (uint)(inst?.BattlesFinished ?? 3),
                    DCPKPNLKGMM = chapterId,
                    IJEIEJLPGGJ = inst?.LineupMaxHp ?? 100,
                    NDOCIKPLKIF = inst?.NDOCIKPLKIF ?? 1600,
                    SectionId = inst?.SectionId ?? 1
                },
                PGPKPMOIAIL = new CLOEPPBCKGF
                {
                    GridFightAvatarList = { 1006u, 1201u, 1202u, 1223u, 1301u, 15062u, 15063u }
                }
            }
        };

        foreach (var portalBuffId in inst?.ActivePortalBuffIds ?? new List<uint> { 106 })
            notify.EDKJMPACHNJ.CEAFFNCKDDD.Add(new GridFightGamePortalBuffInfo { PortalBuffId = portalBuffId });
        foreach (var affixId in inst?.SectionAffixIds ?? new List<uint> { 1002, 3005, 4010 })
            notify.EDKJMPACHNJ.IAKFPMOEJLF.Add(new DIBJGAKOCLO { AffixId = affixId });
        foreach (var equip in inst?.Equipments ?? new List<GridFightEquipmentInfo>())
            notify.EDKJMPACHNJ.GridFightEquipmentList.Add(equip);

        if (inst != null)
        {
            if (inst.LastBattleDamageStt != null)
            {
                foreach (var row in inst.LastBattleDamageStt.EABPCKEDDBH)
                    notify.EDKJMPACHNJ.EABPCKEDDBH.Add(row.Clone());
                foreach (var trait in inst.LastBattleDamageStt.PHDEOPEJIID)
                    notify.EDKJMPACHNJ.PHDEOPEJIID.Add(trait.Clone());
            }

            foreach (var trait in inst.CheckTrait())
            {
                var settleTrait = new GridGameTraitInfo
                {
                    TraitId = trait.TraitId,
                    NKFDBEHPNLG = trait.NKFDBEHPNLG,
                    DFNCFOKPMCJ = trait.NKFDBEHPNLG
                };
                foreach (var m in trait.MEEPFKLLIJB)
                    settleTrait.GridFightTraitMemberUniqueIdList.Add(m.IPDCMHIEKIJ);
                notify.EDKJMPACHNJ.GridFightTraitInfo.Add(settleTrait);
            }

            foreach (var (pos, uniqueId) in inst.UniqueIdByPos.OrderBy(kv => kv.Key))
            {
                if (pos == 0 || pos > 13) continue;
                if (uniqueId == 0 || !inst.RoleByUniqueId.TryGetValue(uniqueId, out var roleKey)) continue;
                notify.EDKJMPACHNJ.GridGameRoleList.Add(new GridGameRoleInfo
                {
                    Id = GridFightRoleLookup.ToSyncRoleId(roleKey),
                    Pos = pos,
                    UniqueId = uniqueId,
                    RoleStar = inst.RoleStarByUniqueId.GetValueOrDefault(uniqueId, 1u)
                });
            }
        }

        return notify;
    }

    public GridFightCurrentInfo BuildCurrentInfo()
    {
        if (GridFightInstance != null) return GridFightInstance.ToProto();

        var maxDiv = MaxDivisionId();
        var stub = new GridFightInstance(Player, 1u, maxDiv, false, CurUniqueId);
        return new GridFightCurrentInfo
        {
            Season = 1,
            DivisionId = maxDiv,
            UniqueId = CurUniqueId,
            PendingAction = new GridFightPendingAction
            {
                QueuePosition = 1,
                PortalBuffAction = new GridFightPortalBuffActionInfo
                {
                    FCHPJKAIBHB = 1,
                    GridFightPortalBuffList = { stub.RollPortalBuffs() }
                }
            }
        };
    }

    public (Retcode, GridFightInstance?) StartGamePlay(uint season, uint divisionId, bool isOverLock)
    {
        if (GridFightInstance != null)
            return (Retcode.RetSucc, GridFightInstance);

        GridFightInstance = new GridFightInstance(Player, season == 0 ? 1u : season,
            divisionId == 0 ? MaxDivisionId() : divisionId, isOverLock, ++CurUniqueId);
        GridFightInstance.EnsureRouteBinding();
        return (Retcode.RetSucc, GridFightInstance);
    }
}
