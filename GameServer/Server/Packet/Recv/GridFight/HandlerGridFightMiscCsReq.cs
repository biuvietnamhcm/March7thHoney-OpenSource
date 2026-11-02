using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.GameServer.Game.GridFight.Sync;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Server.Packet.Send.GridFight;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.GridFight;

[Opcode(CmdIds.GridFightEquipDressCsReq)]
public class HandlerGridFightEquipDressCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = GridFightEquipDressCsReq.Parser.ParseFrom(data);
        var player = connection.Player!;
        var inst = player.GridFightManager?.GridFightInstance;

        await connection.SendPacket(new PacketGridFightEquipDressScRsp());
        if (inst == null) return;

        var equipUid = req.DressEquipmentUniqueId;
        var roleUid = req.DressRoleUniqueId;
        if (!inst.RoleByUniqueId.TryGetValue(roleUid, out var roleId)) return;
        if (!inst.Equipments.Any(e => e.UniqueId == equipUid)) return;

        if (!inst.EquipUniqueIdsByRoleUniqueId.TryGetValue(roleUid, out var equipList))
            inst.EquipUniqueIdsByRoleUniqueId[roleUid] = equipList = new List<uint>();
        if (!equipList.Contains(equipUid)) equipList.Add(equipUid);

        
        
        var craftedAdvancedEquip = TryAutoMergeBasicsOnRole(inst, roleUid, equipList);

        uint pos = 0;
        foreach (var kv in inst.UniqueIdByPos)
            if (kv.Value == roleUid) { pos = kv.Key; break; }

        var roleInfo = new GridGameRoleInfo
        {
            Id = roleId,
            Pos = pos,
            UniqueId = roleUid,
            RoleStar = inst.RoleStarByUniqueId.GetValueOrDefault(roleUid, 1u)
        };
        foreach (var eid in equipList) roleInfo.UpdateEquipsComponent.Add(eid);
        if (GameData.GridFightRoleBasicInfoData.TryGetValue(roleId, out var roleExcel)
            && roleExcel.RoleSavedValueList.Count > 0)
        {
            roleInfo.GridFightValueInitComponent[roleExcel.RoleSavedValueList[0]] = 0;
        }

        var sync = new GridFightSyncUpdateResultScNotify();

        
        if (craftedAdvancedEquip != null)
        {
            var craftSec = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpDgojihijlaf };
            foreach (var removed in craftedAdvancedEquip.RemovedBasics)
            {
                var item = new GridFightGameItemSyncInfo();
                item.GridFightEquipmentList.Add(removed);
                craftSec.UpdateDynamicList.Add(new GridFightSyncData { RemoveGameItemInfo = item });
            }
            var addItem = new GridFightGameItemSyncInfo();
            addItem.GridFightEquipmentList.Add(craftedAdvancedEquip.Added);
            craftSec.UpdateDynamicList.Add(new GridFightSyncData { AddGameItemInfo = addItem });
            GridFightInstance.AppendBattlefieldLayoutSync(craftSec, inst);
            sync.SyncResultDataList.Add(craftSec);
        }

        var sec = new GridFightSyncResultData();
        sec.UpdateDynamicList.Add(new GridFightSyncData { UpdateRoleInfo = roleInfo });
        GridFightInstance.AppendBattlefieldLayoutSync(sec, inst);
        sync.SyncResultDataList.Add(sec);
        await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));
    }

    private sealed class AutoMergeResult
    {
        public List<GridFightEquipmentInfo> RemovedBasics = new();
        public GridFightEquipmentInfo Added = null!;
    }

    private static AutoMergeResult? TryAutoMergeBasicsOnRole(GridFightInstance inst, uint roleUid, List<uint> equipList)
    {
        
        var basics = new List<GridFightEquipmentInfo>();
        foreach (var uid in equipList)
        {
            var info = inst.FindEquipment(uid);
            if (info == null) continue;
            if (!GameData.GridFightEquipmentData.TryGetValue(info.GridFightEquipmentId, out var excel)) continue;
            if (excel.EquipCategory != Enums.GridFight.GridFightEquipCategoryEnum.Basic) continue;
            basics.Add(info);
            if (basics.Count >= 2) break;
        }
        if (basics.Count < 2) return null;

        var a = basics[0].GridFightEquipmentId;
        var b = basics[1].GridFightEquipmentId;
        
        var ia = (int)(a % 10);
        var ib = (int)(b % 10);
        if (ia <= 0 || ib <= 0) return null;
        var lo = Math.Min(ia, ib);
        var hi = Math.Max(ia, ib);
        var advancedId = (uint)(35030000 + lo * 100 + hi);
        if (!GameData.GridFightEquipmentData.ContainsKey(advancedId)) return null;

        
        var removedSnapshots = basics.Select(b2 => b2.Clone()).ToList();
        foreach (var basic in basics)
            inst.RemoveEquipmentByUniqueId(basic.UniqueId);
        var added = inst.AddEquipment(advancedId, source: 5);

        
        equipList.RemoveAll(uid => basics.Any(b2 => b2.UniqueId == uid));
        equipList.Add(added.UniqueId);

        return new AutoMergeResult { RemovedBasics = removedSnapshots, Added = added };
    }
}

[Opcode(CmdIds.GridFightEquipCraftCsReq)]
public class HandlerGridFightEquipCraftCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = GridFightEquipCraftCsReq.Parser.ParseFrom(data);
        var player = connection.Player!;
        var inst = player.GridFightManager?.GridFightInstance;

        await connection.SendPacket(new PacketEPJJBNPIFLC());
        if (inst == null) return;

        var targetId = req.BGEKACPOAOP;
        var materials = req.CraftMaterials.ToList();
        if (targetId == 0 || materials.Count == 0) return;

        
        var matInfos = materials
            .Select(uid => inst.FindEquipment(uid))
            .Where(e => e != null)
            .Cast<GridFightEquipmentInfo>()
            .ToList();
        if (matInfos.Count != materials.Count) return;

        var sync = new GridFightSyncUpdateResultScNotify();
        var sec = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpDgojihijlaf };

        
        foreach (var mat in matInfos)
        {
            var clone = mat.Clone();
            inst.RemoveEquipmentByUniqueId(mat.UniqueId);
            var item = new GridFightGameItemSyncInfo();
            item.GridFightEquipmentList.Add(clone);
            sec.UpdateDynamicList.Add(new GridFightSyncData { RemoveGameItemInfo = item });
        }

        
        var crafted = inst.AddEquipment(targetId, 5);
        var addItem = new GridFightGameItemSyncInfo();
        addItem.GridFightEquipmentList.Add(crafted);
        sec.UpdateDynamicList.Add(new GridFightSyncData { AddGameItemInfo = addItem });
        GridFightInstance.AppendBattlefieldLayoutSync(sec, inst);

        sync.SyncResultDataList.Add(sec);
        sync.SyncResultDataList.Add(new GridFightSyncResultData());
        await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));
    }
}

[Opcode(CmdIds.GridFightUseConsumableCsReq)]
public class HandlerGridFightUseConsumableCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = GridFightUseConsumableCsReq.Parser.ParseFrom(data);
        var player = connection.Player!;
        var inst = player.GridFightManager?.GridFightInstance;

        await connection.SendPacket(new PacketOLOGIALOJDP());
        if (inst == null) return;

        var itemId = req.ItemId;
        if (!GameData.GridFightConsumablesData.TryGetValue(itemId, out var consumable)) return;
        if (consumable.IfConsume && inst.Consumables.All(c => c.ItemId != itemId || c.Num == 0))
            return;

        var sync = new GridFightSyncUpdateResultScNotify();
        var sec = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpNefkflkampo };
        sec.SyncEffectParamList.Add(itemId);

        
        
        
        
        var target = req.DisplayValue;
        List<GridFightInstance.RoleMergeResult>? copyMerges = null;
        var ruleApplied = consumable.ConsumableRule switch
        {
            Enums.GridFight.GridFightConsumeTypeEnum.Remove => ApplyRemoveRule(inst, target, sec),
            Enums.GridFight.GridFightConsumeTypeEnum.Roll => ApplyRollRule(inst, target, sec),
            Enums.GridFight.GridFightConsumeTypeEnum.Upgrade => ApplyUpgradeRule(inst, target, sec),
            Enums.GridFight.GridFightConsumeTypeEnum.Copy => ApplyCopyRule(inst, consumable, target, sec, out copyMerges),
            _ => false
        };
        if (!ruleApplied)
            return;

        if (consumable.IfConsume)
        {
            var current = inst.Consumables.FirstOrDefault(c => c.ItemId == itemId);
            var remaining = current == null ? 0u : (current.Num > 0 ? current.Num - 1 : 0u);

            var payload = new GridFightGameItemSyncInfo();
            var update = new GridFightConsumableUpdateInfo
            {
                ItemId = itemId,
                ItemStackCount = -1
            };
            if (remaining > 0) update.Num = remaining;
            payload.UpdateGridFightConsumableList.Add(update);

            sec.UpdateDynamicList.Add(remaining > 0
                ? new GridFightSyncData { UpdateGameItemInfo = payload }
                : new GridFightSyncData { RemoveGameItemInfo = payload });
            inst.TryConsumeConsumable(itemId);
        }

        sync.SyncResultDataList.Add(sec);
        sync.SyncResultDataList.Add(new GridFightSyncResultData());
        await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));

        if (copyMerges is { Count: > 0 })
        {
            await GridFightMergeSyncHelper.SendMergeSyncsAsync(
                connection,
                player,
                copyMerges,
                GridFightUpdateSrcType.LnpfefkjdhpNefkflkampo);
        }
    }

    /// <summary>
    /// 拆卸类消耗品：卸下目标角色全部装备。
    /// </summary>
    private static bool ApplyRemoveRule(GridFightInstance inst, GridFightConsumableTargetInfo target, GridFightSyncResultData sec)
    {
        if (target?.RemoveTypeTargetInfo == null) return false;
        var roleUid = target.RemoveTypeTargetInfo.DressRoleUniqueId;
        if (!inst.RoleByUniqueId.TryGetValue(roleUid, out var roleId)) return false;
        inst.UnequipAllFromRole(roleUid);
        sec.UpdateDynamicList.Add(new GridFightSyncData { UpdateRoleInfo = BuildRoleInfoSnapshot(inst, roleUid, roleId) });
        return true;
    }

    /// <summary>
    /// 重铸类消耗品：随机替换装备或角色全身装备。
    /// </summary>
    private static bool ApplyRollRule(GridFightInstance inst, GridFightConsumableTargetInfo target, GridFightSyncResultData sec)
    {
        if (target?.RollTypeTargetInfo == null) return false;
        var equipUid = target.RollTypeTargetInfo.DressEquipmentUniqueId;
        var roleUid = target.RollTypeTargetInfo.DressRoleUniqueId;

        if (equipUid != 0)
            return RollOneEquipment(inst, equipUid, sec);

        if (roleUid == 0 || !inst.RoleByUniqueId.TryGetValue(roleUid, out var roleId))
            return false;

        var equips = inst.EquipUniqueIdsByRoleUniqueId.TryGetValue(roleUid, out var list)
            ? list.ToList()
            : new List<uint>();
        if (equips.Count == 0) return false;

        var rolledAny = false;
        foreach (var uid in equips)
            rolledAny |= RollOneEquipment(inst, uid, sec);
        if (!rolledAny) return false;

        sec.UpdateDynamicList.Add(new GridFightSyncData { UpdateRoleInfo = BuildRoleInfoSnapshot(inst, roleUid, roleId) });
        return true;
    }

    /// <summary>
    /// 升级类消耗品（如特权赋予卡 350104）：将 3503**** 成品装备升级为 3504****。
    /// </summary>
    private static bool ApplyUpgradeRule(GridFightInstance inst, GridFightConsumableTargetInfo target, GridFightSyncResultData sec)
    {
        if (target?.UpgradeTypeTargetInfo == null) return false;
        var equipUid = target.UpgradeTypeTargetInfo.DressEquipmentUniqueId;
        if (equipUid == 0) return false;

        var old = inst.FindEquipment(equipUid);
        if (old == null) return false;

        uint? wearer = null;
        var roleId = 0u;
        foreach (var (roleUid, equipList) in inst.EquipUniqueIdsByRoleUniqueId)
        {
            if (!equipList.Contains(equipUid)) continue;
            wearer = roleUid;
            inst.RoleByUniqueId.TryGetValue(roleUid, out roleId);
            break;
        }

        var oldClone = old.Clone();
        var upgraded = inst.UpgradeCraftableEquipment(equipUid);
        if (upgraded == null) return false;

        var removeItem = new GridFightGameItemSyncInfo();
        removeItem.GridFightEquipmentList.Add(oldClone);
        sec.UpdateDynamicList.Add(new GridFightSyncData { RemoveGameItemInfo = removeItem });

        var addItem = new GridFightGameItemSyncInfo();
        addItem.GridFightEquipmentList.Add(upgraded);
        sec.UpdateDynamicList.Add(new GridFightSyncData { AddGameItemInfo = addItem });

        if (wearer.HasValue && roleId != 0)
            sec.UpdateDynamicList.Add(new GridFightSyncData { UpdateRoleInfo = BuildRoleInfoSnapshot(inst, wearer.Value, roleId) });

        GridFightInstance.AppendBattlefieldLayoutSync(sec, inst);
        return true;
    }

    /// <summary>
    /// 投影仪类消耗品（350105/350106）：复制 1 星形态至购买落点；刷新源槽位，避免客户端覆盖原角色。
    /// </summary>
    private static bool ApplyCopyRule(
        GridFightInstance inst,
        GridFightConsumablesExcel consumable,
        GridFightConsumableTargetInfo target,
        GridFightSyncResultData sec,
        out List<GridFightInstance.RoleMergeResult> merges)
    {
        merges = [];
        if (target?.CopyTypeTargetInfo == null) return false;

        var sourceUid = target.CopyTypeTargetInfo.DressRoleUniqueId;
        if (sourceUid == 0) return false;

        var maxRarity = consumable.ConsumableParamList.Count > 0 ? consumable.ConsumableParamList[0] : 5u;
        var (ok, newUid, targetPos, sourcePos, _) = inst.TryCopyRole(sourceUid, maxRarity);
        if (!ok || newUid == 0) return false;

        sec.SyncEffectParamList.Add(sourcePos);
        sec.SyncEffectParamList.Add(targetPos);

        var copyCtx = new CMCJNKPKBEM();
        copyCtx.CFNPGNMPNDN[sourceUid.ToString()] = sourcePos;
        copyCtx.CFNPGNMPNDN[newUid.ToString()] = targetPos;
        sec.UpdateDynamicList.Add(new GridFightSyncData { CFNPGNMPNDN = copyCtx });

        var addInfo = inst.BuildGridGameRoleInfo(newUid, targetPos);
        addInfo.RoleStar = 1;
        sec.UpdateDynamicList.Add(new GridFightSyncData { AddRoleInfo = addInfo });

        if (inst.RoleByUniqueId.TryGetValue(sourceUid, out var sourceRoleKey))
        {
            sec.UpdateDynamicList.Add(new GridFightSyncData
            {
                UpdateRoleInfo = BuildRoleInfoSnapshot(
                    inst,
                    sourceUid,
                    GridFightRoleLookup.ToSyncRoleId(sourceRoleKey))
            });
        }

        merges = inst.TryAutoMergeAllRoles();
        var benchMoved = merges.Count > 0 ? inst.FinalizeBenchAfterMerge() : [];

        foreach (var benchPos in benchMoved)
        {
            var roleInfo = GridFightManager.TryBuildBenchRepositionRoleInfo(inst, benchPos, merges);
            if (roleInfo == null) continue;
            sec.UpdateDynamicList.Add(new GridFightSyncData { UpdateRoleInfo = roleInfo });
        }

        GridFightInstance.AppendBattlefieldLayoutSync(sec, inst);
        return true;
    }

    private static bool RollOneEquipment(GridFightInstance inst, uint equipUid, GridFightSyncResultData sec)
    {
        var old = inst.FindEquipment(equipUid);
        if (old == null) return false;
        var newId = GridFightInstance.RollSameCategoryEquipment(old.GridFightEquipmentId);
        if (newId == 0) return false;
        var oldClone = old.Clone();
        var added = inst.RollEquipment(equipUid, newId, source: 1);
        if (added == null) return false;

        var removeItem = new GridFightGameItemSyncInfo();
        removeItem.GridFightEquipmentList.Add(oldClone);
        sec.UpdateDynamicList.Add(new GridFightSyncData { RemoveGameItemInfo = removeItem });

        var addItem = new GridFightGameItemSyncInfo();
        addItem.GridFightEquipmentList.Add(added);
        sec.UpdateDynamicList.Add(new GridFightSyncData { AddGameItemInfo = addItem });
        GridFightInstance.AppendBattlefieldLayoutSync(sec, inst);
        return true;
    }

    private static GridGameRoleInfo BuildRoleInfoSnapshot(GridFightInstance inst, uint roleUid, uint roleId)
    {
        uint pos = 0;
        foreach (var kv in inst.UniqueIdByPos)
            if (kv.Value == roleUid) { pos = kv.Key; break; }

        var roleInfo = new GridGameRoleInfo
        {
            Id = roleId,
            Pos = pos,
            UniqueId = roleUid,
            RoleStar = inst.RoleStarByUniqueId.GetValueOrDefault(roleUid, 1u)
        };
        if (inst.EquipUniqueIdsByRoleUniqueId.TryGetValue(roleUid, out var equipList))
            foreach (var eid in equipList) roleInfo.UpdateEquipsComponent.Add(eid);
        if (GameData.GridFightRoleBasicInfoData.TryGetValue(roleId, out var roleExcel)
            && roleExcel.RoleSavedValueList.Count > 0)
            roleInfo.GridFightValueInitComponent[roleExcel.RoleSavedValueList[0]] = 0;
        return roleInfo;
    }
}

[Opcode(CmdIds.GridFightUseForgeCsReq)]
public class HandlerGridFightUseForgeCsReq : Handler { public override async Task OnHandle(Connection connection, byte[] header, byte[] data) { _ = GridFightUseForgeCsReq.Parser.ParseFrom(data); await connection.SendPacket(new PacketEEBMIAFNJMC()); await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(connection.Player!, GridFightSyncKind.NoOp)); } }

[Opcode(CmdIds.GridFightBuyExpCsReq)]
public class HandlerGridFightBuyExpCsReq : Handler { public override async Task OnHandle(Connection connection, byte[] header, byte[] data) { _ = GridFightBuyExpCsReq.Parser.ParseFrom(data); var player = connection.Player!; _ = new GridFightService(player).BuyExp(); await connection.SendPacket(new PacketDJCHCHCAJPB()); await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(player, GridFightSyncKind.BuyExp)); } }

[Opcode(CmdIds.GridFightLockShopCsReq)]
public class HandlerGridFightLockShopCsReq : Handler { public override async Task OnHandle(Connection connection, byte[] header, byte[] data) { _ = GridFightLockShopCsReq.Parser.ParseFrom(data); await connection.SendPacket(new PacketHGOIBDBMDBG()); await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(connection.Player!, GridFightSyncKind.NoOp)); } }

[Opcode(CmdIds.GJMIIBDEAAJ)]
public class HandlerGridFightUseOrbCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = GJMIIBDEAAJ.Parser.ParseFrom(data);
        var player = connection.Player!;
        var inst = player.GridFightManager?.GridFightInstance;

        await connection.SendPacket(new PacketCEFIMADBIBH());
        if (inst == null) return;

        var results = inst.TryUseOrbsDetailed(req.HDFJAINBKJG, req.IsGetAll);

        var sync = new GridFightSyncUpdateResultScNotify();
        var levelChanged = false;

        
        
        
        foreach (var r in results)
        {
            if (r.LevelChanged) levelChanged = true;

            var sec = new GridFightSyncResultData
            {
                GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpOopnbcijhmp
            };
            sec.SyncEffectParamList.Add(r.UniqueId);
            sec.SyncEffectParamList.Add(r.ItemId);
            sec.UpdateDynamicList.Add(new GridFightSyncData { RemoveOrbUniqueId = r.UniqueId });

            if (r.AddEquipmentId.HasValue && r.AddEquipmentUniqueId.HasValue)
            {
                var item = new GridFightGameItemSyncInfo();
                item.GridFightEquipmentList.Add(new GridFightEquipmentInfo
                {
                    GridFightEquipmentId = r.AddEquipmentId.Value,
                    Source = 1,
                    UniqueId = r.AddEquipmentUniqueId.Value
                });
                sec.UpdateDynamicList.Add(new GridFightSyncData { AddGameItemInfo = item });
                GridFightInstance.AppendBattlefieldLayoutSync(sec, inst);
            }

            if (r.AddConsumableItemId.HasValue)
            {
                
                var item = new GridFightGameItemSyncInfo();
                item.UpdateGridFightConsumableList.Add(new GridFightConsumableUpdateInfo
                {
                    ItemId = r.AddConsumableItemId.Value,
                    ItemStackCount = 1,
                    Num = r.AddConsumableNewTotal
                });
                sec.UpdateDynamicList.Add(r.AddConsumableNewTotal <= 1
                    ? new GridFightSyncData { AddGameItemInfo = item }
                    : new GridFightSyncData { UpdateGameItemInfo = item });
            }

            if (r.GoldChanged)
                sec.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = r.GoldAfter });

            if (r.AddRoleId.HasValue && r.AddRoleUniqueId.HasValue && r.AddRolePos.HasValue)
            {
                var roleInfo = new GridGameRoleInfo
                {
                    Id = r.AddRoleId.Value,
                    Pos = r.AddRolePos.Value,
                    RoleStar = 1,
                    UniqueId = r.AddRoleUniqueId.Value
                };
                if (!string.IsNullOrEmpty(r.AddRoleComponent))
                    roleInfo.GridFightValueInitComponent[r.AddRoleComponent] = 0;
                sec.UpdateDynamicList.Add(new GridFightSyncData { AddRoleInfo = roleInfo });
            }

            sync.SyncResultDataList.Add(sec);
        }

        if (levelChanged)
        {
            var lvlSec = new GridFightSyncResultData();
            lvlSec.UpdateDynamicList.Add(new GridFightSyncData
            {
                PlayerLevel = new GridFightPlayerLevelSyncInfo
                {
                    Level = inst.PlayerLevel,
                    Exp = inst.PlayerExp,
                    MaxLevel = inst.PlayerMaxLevel
                }
            });
            lvlSec.UpdateDynamicList.Add(new GridFightSyncData { GridFightBuyExpCost = inst.GetBuyExpCost() });
            GridFightInstance.AppendBattlefieldLayoutSync(lvlSec, inst);
            lvlSec.UpdateDynamicList.Add(new GridFightSyncData
            {
                ShopSyncInfo = new GridFightShopSyncInfo { LDEDGOOKHFL = inst.BuildShopRarityDisplayInfo() }
            });
            sync.SyncResultDataList.Add(lvlSec);
        }

        
        if (sync.SyncResultDataList.Count == 0)
        {
            var noop = new GridFightSyncResultData();
            noop.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = inst.Gold });
            sync.SyncResultDataList.Add(noop);
        }

        await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));
    }
}

[Opcode(CmdIds.GridFightRecycleRoleCsReq)]
public class HandlerGridFightRecycleRoleCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = GridFightRecycleRoleCsReq.Parser.ParseFrom(data);
        var player = connection.Player!;
        var refund = new GridFightService(player).RecycleRole(req.UniqueId);
        await connection.SendPacket(new PacketGridFightRecycleRoleScRsp());
        await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(player, GridFightSyncKind.RecycleRole, (req.UniqueId, (int)refund)));
    }
}

[Opcode(CmdIds.GridFightUpdateEquipTrackCsReq)]
public class HandlerGridFightUpdateEquipTrackCsReq : Handler { public override async Task OnHandle(Connection connection, byte[] header, byte[] data) { _ = GridFightUpdateEquipTrackCsReq.Parser.ParseFrom(data); await connection.SendPacket(new PacketGridFightUpdateEquipTrackScRsp()); } }
