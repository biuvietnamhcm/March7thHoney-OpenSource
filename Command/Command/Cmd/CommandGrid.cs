using March7thHoney.Data;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.GameServer.Game.GridFight.Sync;
using March7thHoney.GameServer.Server.Packet.Send.GridFight;
using March7thHoney.Internationalization;
using March7thHoney.Proto;

namespace March7thHoney.Command.Command.Cmd;

[CommandInfo("grid", "Game.Command.Grid.Desc", "Game.Command.Grid.Usage", permission: CommandPermissions.Grid)]
public class CommandGrid : ICommand
{
    [CommandMethod("0 role")]
    public async ValueTask AddRole(CommandArg arg)
    {
        var player = arg.Target?.Player;
        var inst = player?.GridFightManager?.GridFightInstance;
        if (player == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }
        if (inst == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.NotInGame"));
            return;
        }
        if (arg.BasicArgs.Count < 2)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var roleId = (uint)arg.GetInt(0);
        var star = (uint)Math.Max(1, arg.GetInt(1));
        if (!GameData.GridFightRoleStarData.ContainsKey(roleId << 4 | star))
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.InvalidRole"));
            return;
        }

        if (!inst.TryAllocBenchPos(out var pos))
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var uniqueId = inst.AllocRoleUniqueId();
        inst.RoleByUniqueId[uniqueId] = roleId;
        inst.RoleStarByUniqueId[uniqueId] = star;
        inst.UniqueIdByPos[pos] = uniqueId;

        var roleInfo = new GridGameRoleInfo
        {
            Id = roleId,
            Pos = pos,
            UniqueId = uniqueId,
            RoleStar = star
        };
        if (GameData.GridFightRoleBasicInfoData.TryGetValue(roleId, out var roleExcel)
            && roleExcel.RoleSavedValueList.Count > 0)
        {
            roleInfo.GridFightValueInitComponent[roleExcel.RoleSavedValueList[0]] = 0;
        }

        var sync = new GridFightSyncUpdateResultScNotify();
        var sec = new GridFightSyncResultData();
        sec.UpdateDynamicList.Add(new GridFightSyncData { AddRoleInfo = roleInfo });
        sync.SyncResultDataList.Add(sec);
        await player.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));
        await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.AddedRole"));
    }

    [CommandMethod("0 gold")]
    public async ValueTask UpdateGold(CommandArg arg)
    {
        var player = arg.Target?.Player;
        var inst = player?.GridFightManager?.GridFightInstance;
        if (player == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }
        if (inst == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.NotInGame"));
            return;
        }
        if (arg.BasicArgs.Count < 1)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var delta = arg.GetInt(0);
        if (delta >= 0) inst.Gold += (uint)delta;
        else inst.Gold = inst.Gold < (uint)(-delta) ? 0u : inst.Gold - (uint)(-delta);

        var sync = new GridFightSyncUpdateResultScNotify();
        var sec = new GridFightSyncResultData();
        sec.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = inst.Gold });
        sync.SyncResultDataList.Add(sec);
        await player.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));
        await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.UpdateGold", delta.ToString()));
    }

    [CommandMethod("0 equip")]
    public async ValueTask AddEquipment(CommandArg arg)
    {
        var player = arg.Target?.Player;
        var inst = player?.GridFightManager?.GridFightInstance;
        if (player == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }
        if (inst == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.NotInGame"));
            return;
        }
        if (arg.BasicArgs.Count < 1)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var equipmentId = (uint)arg.GetInt(0);
        if (!GameData.GridFightEquipmentData.ContainsKey(equipmentId))
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.InvalidEquipment"));
            return;
        }

        var equip = new GridFightEquipmentInfo
        {
            GridFightEquipmentId = equipmentId,
            Source = 1,
            UniqueId = inst.AllocEquipUniqueId()
        };
        inst.Equipments.Add(equip);

        var addItem = new GridFightGameItemSyncInfo();
        addItem.GridFightEquipmentList.Add(equip);
        var sync = new GridFightSyncUpdateResultScNotify();
        var sec = new GridFightSyncResultData();
        sec.UpdateDynamicList.Add(new GridFightSyncData { AddGameItemInfo = addItem });
        sync.SyncResultDataList.Add(sec);
        await player.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));
        await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.AddEquipment", equipmentId.ToString()));
    }

    [CommandMethod("0 orb")]
    public async ValueTask AddOrb(CommandArg arg)
    {
        var player = arg.Target?.Player;
        var inst = player?.GridFightManager?.GridFightInstance;
        if (player == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }
        if (inst == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.NotInGame"));
            return;
        }
        if (arg.BasicArgs.Count < 1)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var orbId = (uint)arg.GetInt(0);
        if (!GameData.GridFightOrbData.ContainsKey(orbId))
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.InvalidOrb"));
            return;
        }

        var uniqueId = inst.AllocOrbUniqueId();
        inst.OrbItemByUniqueId[uniqueId] = orbId;

        var sync = new GridFightSyncUpdateResultScNotify();
        var sec = new GridFightSyncResultData();
        sec.UpdateDynamicList.Add(new GridFightSyncData
        {
            OrbSyncInfo = new GridFightOrbSyncInfo { UniqueId = uniqueId, OrbItemId = orbId }
        });
        sync.SyncResultDataList.Add(sec);
        await player.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));
        await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.AddOrb", orbId.ToString()));
    }

    [CommandMethod("0 consumable")]
    public async ValueTask AddConsumable(CommandArg arg)
    {
        var player = arg.Target?.Player;
        var inst = player?.GridFightManager?.GridFightInstance;
        if (player == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }
        if (inst == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.NotInGame"));
            return;
        }
        if (arg.BasicArgs.Count < 1)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var consumableId = (uint)arg.GetInt(0);
        if (!GameData.GridFightConsumablesData.ContainsKey(consumableId))
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.InvalidConsumable"));
            return;
        }

        var c = inst.Consumables.FirstOrDefault(x => x.ItemId == consumableId);
        var firstAdd = c == null;
        if (firstAdd)
        {
            c = new GridFightConsumableInfo { ItemId = consumableId, Num = 1 };
            inst.Consumables.Add(c);
        }
        else
        {
            c!.Num += 1;
        }

        
        
        var payload = new GridFightGameItemSyncInfo();
        payload.UpdateGridFightConsumableList.Add(new GridFightConsumableUpdateInfo
        {
            ItemId = consumableId,
            ItemStackCount = 1,
            Num = c.Num
        });
        var sync = new GridFightSyncUpdateResultScNotify();
        var sec = new GridFightSyncResultData();
        sec.UpdateDynamicList.Add(firstAdd
            ? new GridFightSyncData { AddGameItemInfo = payload }
            : new GridFightSyncData { UpdateGameItemInfo = payload });
        sync.SyncResultDataList.Add(sec);
        await player.SendPacket(new PacketGridFightSyncUpdateResultScNotify(sync));
        await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.AddConsumable", consumableId.ToString()));
    }

    [CommandMethod("0 section")]
    public async ValueTask SetSection(CommandArg arg)
    {
        var player = arg.Target?.Player;
        var inst = player?.GridFightManager?.GridFightInstance;
        if (player == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }
        if (inst == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.NotInGame"));
            return;
        }
        if (arg.BasicArgs.Count < 2)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var chapterId = (uint)Math.Max(1, arg.GetInt(0));
        var sectionId = (uint)Math.Max(1, arg.GetInt(1));
        var maxSection = inst.GetChapterSectionCount(chapterId);
        if (chapterId > 3 || maxSection == 0 || sectionId > maxSection)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        inst.LastAugmentConsumedSection = 0;
        inst.LastSupplyConsumedSection = 0;
        inst.LastEliteBranchConsumedSection = 0;
        inst.ClearAugmentOffer();
        inst.ClearSupplyOffer();
        inst.ClearEliteBranchOptions();
        inst.CurrentAugmentReroll = 3;
        inst.CurrentSupplyReroll = 1;
        inst.CurrentEliteBranchReroll = 1;
        inst.BattleComponent.SetEncounter(0, Array.Empty<uint>());

        inst.CurrentChapterId = chapterId;
        inst.SectionId = sectionId - 1;

        inst.AdvanceQueue(7);
        inst.PendingAction = new GridFightPendingAction
        {
            QueuePosition = inst.QueuePosition,
            RoundBeginAction = new GridFightRoundBeginActionInfo()
        };

        await player.SendPacket(new PacketGridFightSyncUpdateResultScNotify(player, GridFightSyncKind.PostBattle));

        inst.SectionId = sectionId;

        if (GridFightLevelResolver.IsCombatNode(inst))
        {
            var encounter = GridFightLevelResolver.Resolve(inst);
            inst.ConfigureNextBattle(encounter.StageId, encounter.Monsters.Select(m => m.MonsterId));
        }

        await player.SendPacket(new PacketGridFightSyncUpdateResultScNotify(player, GridFightSyncKind.RefreshShop));

        await arg.SendMsg(I18NManager.Translate("Game.Command.Grid.EnterSection", chapterId.ToString(), sectionId.ToString()));
    }
}
