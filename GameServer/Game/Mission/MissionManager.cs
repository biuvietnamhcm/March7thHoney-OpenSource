using March7thHoney.Data;
using March7thHoney.Data.Config;
using March7thHoney.Database;
using March7thHoney.Database.Inventory;
using March7thHoney.Enums.Item;
using March7thHoney.Enums.Mission;
using March7thHoney.GameServer.Game.Battle;
using March7thHoney.GameServer.Game.Mission.FinishAction;
using March7thHoney.GameServer.Game.Mission.FinishType;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Plugin.Event;
using March7thHoney.GameServer.Server.Packet.Send.HeartDial;
using March7thHoney.GameServer.Server.Packet.Send.Mission;
using March7thHoney.GameServer.Server.Packet.Send.PlayerSync;
using March7thHoney.Proto;
using March7thHoney.Util;
using MissionData = March7thHoney.Database.Quests.MissionData;

namespace March7thHoney.GameServer.Game.Mission;

public class MissionManager(PlayerInstance player) : BasePlayerManager(player)
{
    #region Initializer & Properties

    public MissionData Data { get; set; } = DatabaseHelper.Instance!.GetInstanceOrCreateNew<MissionData>(player.Uid);
    public static readonly Dictionary<FinishActionTypeEnum, MissionFinishActionHandler> ActionHandlers = [];
    public static readonly Dictionary<MissionFinishTypeEnum, MissionFinishTypeHandler> FinishTypeHandlers = [];

    public readonly List<int> SkipSubMissionList = []; 

    #endregion

    #region Mission Actions

    private readonly SemaphoreSlim _itemHandIn = new(1, 1);

    public async ValueTask<Retcode> ConsumeMissionItems(int missionId, List<ItemData>? selectedItems = null)
    {
        await _itemHandIn.WaitAsync();
        try
        {
            if (Player.DailyActivityManager != null) await Player.DailyActivityManager.RefreshAsync();
            var mission = GetSubMissionInfo(missionId);
            if (mission == null || mission.FinishType is not (MissionFinishTypeEnum.ConsumeMissionItem or MissionFinishTypeEnum.UseSelectedItem) ||
                GetSubMissionStatus(missionId) != MissionPhaseEnum.Accept) return Retcode.RetMissionNotDoing;
            var costs = (mission.ParamItemList ?? []).GroupBy(x => x.ItemID)
                .Select(x => new ItemData { ItemId = x.Key, Count = x.Sum(y => y.ItemNum) }).ToList();
            if (mission.FinishType == MissionFinishTypeEnum.UseSelectedItem)
            {
                // ParamInt1 is the number of selection slots. Only the supplied alternatives may be consumed.
                if (selectedItems == null || selectedItems.Count != Math.Max(1, mission.ParamInt1) ||
                    selectedItems.Select(x => x.ItemId).Distinct().Count() != selectedItems.Count ||
                    selectedItems.Any(x => !costs.Any(c => c.ItemId == x.ItemId && c.Count == x.Count)))
                    return Retcode.RetMissionNotDoing;
                costs = selectedItems;
            }
            if (costs.Count == 0 || Player.InventoryManager == null ||
                costs.Any(x => x.Count <= 0 || (Player.InventoryManager.GetItem(x.ItemId)?.Count ?? 0) < x.Count))
                return Retcode.RetItemNotEnough;
            foreach (var item in costs) await Player.InventoryManager.RemoveItem(item.ItemId, item.Count);
            await FinishSubMission(missionId);
            return Retcode.RetSucc;
        }
        finally { _itemHandIn.Release(); }
    }

    public async ValueTask<Retcode> AcceptMainMissionFromClient(int missionId)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return Retcode.RetSucc;
        if (!GameData.MainMissionData.TryGetValue(missionId, out var mission)) return Retcode.RetMissionNotExist;
        var status = Data.GetMainMissionStatus(missionId);
        if (status == MissionPhaseEnum.Finish) return Retcode.RetMissionAlreadyDone;
        if (status == MissionPhaseEnum.Accept) return Retcode.RetSucc;
        if (!mission.IsEqual(Data)) return Retcode.RetMissionNotFinish;
        await AcceptMainMission(missionId);
        return Retcode.RetSucc;
    }

    public List<int> GetAllFinishedMissionIds()
    {
        return Data.FinishedSubMissionIds
            .Concat(Data.FinishedMainMissionIds)
            .Where(x => x > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    public async ValueTask<List<MissionSync?>> AcceptMainMission(int missionId, bool sendPacket = true)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return [];
        if (Data.GetMainMissionStatus(missionId) != MissionPhaseEnum.None) return []; 
        
        GameData.MainMissionData.TryGetValue(missionId, out var mission);
        if (mission == null) return [];

        Data.SetMainMissionStatus(missionId, MissionPhaseEnum.Accept);

        var list = new List<MissionSync?>();
        foreach (var id in mission.MissionInfo?.StartSubMissionList ?? [])
            list.Add(await AcceptSubMission(id, sendPacket));
        if (missionId == 4030001 || missionId == 4030002)
        {
            
            foreach (var subMission in mission.MissionInfo?.SubMissionList ?? [])
                await AcceptSubMission(subMission.ID);
            foreach (var subMission in mission.MissionInfo?.SubMissionList ?? [])
                await FinishSubMission(subMission.ID);
        }

        if (missionId == 1000400)
        {
            await Player.AddAvatar(1003);
            await Player.LineupManager!.AddAvatarToCurTeam(1003);
        }

        
        foreach (var sectionConfigExcel in GameData.MessageSectionConfigData.Values.Where(x =>
                     x.MainMissionLink == missionId))
            await Player.MessageManager!.AddMessageSection(sectionConfigExcel.ID);

        foreach (var info in mission.MissionInfo!.SubMissionList.Where(x =>
                     x.FinishType is MissionFinishTypeEnum.MessagePerformSectionFinish
                         or MissionFinishTypeEnum.MessageSectionFinish))
            await Player.MessageManager!.AddMessageSection(info.ParamInt1);

        return list;
    }

    public async ValueTask<MissionSync> AcceptMainMissionByCondition(bool sendPacket = true)
    {
        var sync = new MissionSync();
        foreach (var nextMission in GameData.MainMissionData.Values)
        {
            if (!nextMission.IsEqual(Data)) continue;
            if (Data.GetMainMissionStatus(nextMission.MainMissionID) != MissionPhaseEnum.None)
                continue; 
            var res = await AcceptMainMission(nextMission.MainMissionID, sendPacket);
            foreach (var subMission in res)
                if (subMission != null)
                    sync.MissionList.AddRange(subMission.MissionList);
        }

        return sync;
    }

    public async ValueTask<List<MissionSync?>> ReAcceptMainMission(int missionId, bool sendPacket = true)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return [];

        GameData.MainMissionData.TryGetValue(missionId, out var mission);
        if (mission == null) return [];
        MissionSync sync = new();

        foreach (var subMission in mission.SubMissionIds)
            if (Data.GetSubMissionStatus(subMission) == MissionPhaseEnum.Finish ||
                Data.GetSubMissionStatus(subMission) == MissionPhaseEnum.Accept)
                sync.MissionList.Add(new Proto.Mission
                {
                    Id = (uint)subMission,
                    Status = MissionStatus.MissionNone
                });

        foreach (var subMission in
                 mission.SubMissionIds) Data.SetSubMissionStatus(subMission, MissionPhaseEnum.None); 

        Data.SetMainMissionStatus(missionId, MissionPhaseEnum.None); 
        await Player.SendPacket(new PacketPlayerSyncScNotify(sync));

        return await AcceptMainMission(missionId, sendPacket);
    }

    public async ValueTask RemoveMainMission(int missionId)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return;
        Data.SetMainMissionStatus(missionId, MissionPhaseEnum.None);

        GameData.MainMissionData.TryGetValue(missionId, out var mission);
        if (mission == null) return;

        MissionSync sync = new();

        foreach (var subMission in mission.SubMissionIds)
        {
            Data.SetSubMissionStatus(subMission, MissionPhaseEnum.None);
            await SetMissionProgress(subMission, 0, false);
            sync.MissionList.Add(new Proto.Mission
            {
                Id = (uint)subMission,
                Status = MissionStatus.MissionNone
            });
        }

        await Player.SendPacket(new PacketPlayerSyncScNotify(sync));
    }

    public async ValueTask AcceptSubMission(int missionId)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return;
        await AcceptSubMission(missionId, true);
    }

    public async ValueTask<MissionSync?> AcceptSubMission(int missionId, bool sendPacket,
        bool doFinishTypeAction = true)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return null;
        GameData.SubMissionInfoData.TryGetValue(missionId, out var mission);
        if (mission == null) return null;
        if (Data.GetSubMissionStatus(missionId) != MissionPhaseEnum.None) return null; 

        Data.SetSubMissionStatus(missionId, MissionPhaseEnum.Accept);

        var sync = new MissionSync();
        sync.MissionList.Add(new Proto.Mission
        {
            Id = (uint)missionId,
            Status = MissionStatus.MissionDoing
        });

        if (sendPacket) await Player.SendPacket(new PacketPlayerSyncScNotify(sync));
        Player.SceneInstance?.SyncGroupInfo();

        // Apply mission setup (trial party, props, transfers) before checking its completion condition.
        if (Player.TaskManager != null)
            await Player.TaskManager.MissionTaskTrigger.TriggerMissionTask(missionId);

        if (mission.SubMissionInfo != null)
            try
            {
                FinishTypeHandlers.TryGetValue(mission.SubMissionInfo.FinishType, out var handler);
                if (doFinishTypeAction)
                    if (handler != null)
                        await handler.HandleMissionFinishType(Player, mission.SubMissionInfo, null);
            }
            catch (Exception ex)
            {
                Logger.GetByClassName().Error($"Failed to evaluate sub-mission {missionId}", ex);
            }

        if (SkipSubMissionList.Contains(missionId)) await FinishSubMission(missionId);

        if (mission.SubMissionInfo?.LevelFloorID == Player.SceneInstance?.FloorId)
            if (mission.SubMissionInfo?.GroupIDList != null)
                foreach (var group in mission.SubMissionInfo.GroupIDList)
                    await Player.SceneInstance!.EntityLoader!.LoadGroup(group);

        
        sync.MissionList[0].Status = Data.GetSubMissionStatus(missionId).ToProto();
        sync.MissionList[0].Progress = (uint)GetMissionProgress(missionId);
        return sync;
    }

    public async ValueTask FinishMainMission(int missionId)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return;
        if (!GameData.MainMissionData.TryGetValue(missionId, out var mainMission)) return;
        if (Data.GetMainMissionStatus(missionId) != MissionPhaseEnum.Accept) return;
        Data.SetMainMissionStatus(missionId, MissionPhaseEnum.Finish);
        var finishedMissionIds = new List<int> { missionId };
        var sync = new MissionSync();
        sync.FinishedMainMissionIdList.Add((uint)missionId);
        
        foreach (var mission in mainMission.SubMissionIds)
            if (GetSubMissionStatus(mission) != MissionPhaseEnum.Finish)
                if (Data.GetSubMissionStatus(mission) != MissionPhaseEnum.Finish)
                {
                    Data.SetSubMissionStatus(mission, MissionPhaseEnum.Finish);
                    finishedMissionIds.Add(mission);
                    sync.MissionList.Add(new Proto.Mission
                    {
                        Id = (uint)mission,
                        Status = MissionStatus.MissionFinish
                    });
                }

        var mainSync = await AcceptMainMissionByCondition(false);
        sync.MissionList.AddRange(mainSync.MissionList);

        await Player.SendPacket(new PacketPlayerSyncScNotify(sync));
        await Player.SendPacket(new PacketStartFinishMainMissionScNotify(missionId));
        await Player.SendPacket(new PacketFinishedMissionScNotify(finishedMissionIds));
        await HandleMissionReward(missionId);
        await HandleFinishType(MissionFinishTypeEnum.FinishMission);

        await Player.RaidManager!.CheckIfLeaveRaid();

        if (Player.DailyActivityManager != null) await Player.DailyActivityManager.OnMissionFinished(missionId);
        PluginEvent.InvokeOnPlayerFinishMainMission(Player, missionId);
        DatabaseHelper.Instance?.SaveUidData(Player.Uid);
    }

    public async ValueTask FinishSubMission(int missionId)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return;
        GameData.SubMissionInfoData.TryGetValue(missionId, out var subMission);
        if (subMission == null) return;
        var mainMissionId = subMission.MainMissionId;
        if (Data.GetSubMissionStatus(missionId) != MissionPhaseEnum.Accept) return; 
        GameData.MainMissionData.TryGetValue(mainMissionId, out var mainMission); 
        if (mainMission == null) return;
        Data.SetSubMissionStatus(missionId, MissionPhaseEnum.Finish); 

        await SetMissionProgress(missionId, subMission.SubMissionInfo?.Progress ?? 1);

        var sync = new MissionSync();
        sync.MissionList.Add(new Proto.Mission
        {
            Id = (uint)missionId,
            Status = MissionStatus.MissionFinish,
            Progress = (uint)(subMission.SubMissionInfo?.Progress ?? 1)
        });

        
        if (mainMission.MissionInfo != null)
            await HandleFinishAction(mainMission.MissionInfo, missionId);

        foreach (var nextMission in mainMission.MissionInfo?.SubMissionList ?? [])
        {
            if (nextMission.TakeType != SubMissionTakeTypeEnum.AnySequence &&
                nextMission.TakeType != SubMissionTakeTypeEnum.MultiSequence) continue;
            var canAccept = nextMission.TakeType == SubMissionTakeTypeEnum.MultiSequence; 
            foreach (var id in nextMission.TakeParamIntList ?? [])
                if (GetSubMissionStatus(id) != MissionPhaseEnum.Finish &&
                    nextMission.TakeType == SubMissionTakeTypeEnum.MultiSequence)
                {
                    canAccept = false;
                    break;
                }
                else if (GetSubMissionStatus(id) == MissionPhaseEnum.Finish &&
                         nextMission.TakeType == SubMissionTakeTypeEnum.AnySequence) 
                {
                    canAccept = true;
                    break;
                }

            if (canAccept)
            {
                var s = await AcceptSubMission(nextMission.ID, false, false);
                if (s != null)
                    sync.MissionList.AddRange(s.MissionList);
            }
        }

        await Player.SendPacket(new PacketPlayerSyncScNotify(sync));
        await Player.SendPacket(new PacketStartFinishSubMissionScNotify(missionId));
        await Player.SendPacket(new PacketFinishedMissionScNotify(missionId));

        
        
        var shouldFinish = mainMission.MissionInfo?.FinishSubMissionList.Count > 0;
        mainMission.MissionInfo?.FinishSubMissionList.ForEach(id =>
        {
            if (GetSubMissionStatus(id) != MissionPhaseEnum.Finish) shouldFinish = false;
        });

        foreach (var nextMission in GetRunningSubMissionList())
        {
            FinishTypeHandlers.TryGetValue(nextMission.FinishType, out var handler);
            if (handler != null) await handler.HandleMissionFinishType(Player, nextMission, null);
        }

        if (shouldFinish) await FinishMainMission(mainMissionId);

        if (missionId == 101140201)
        {
            
            var list = Player.LineupManager!.GetCurLineup()!.BaseAvatars!
                .Select(x => x.SpecialAvatarId > 0 ? x.SpecialAvatarId : x.BaseAvatarId).ToList();
            list[list.IndexOf(8001)] = Player.Data.CurrentGender == Gender.Man ? 1008003 : 1008004;
            Player.LineupManager!.SetExtraLineup(ExtraLineupType.LineupHeliobus, list);
        }

        if (missionId == 103040103)
            await Player.SendPacket(new PacketHeartDialScriptChangeScNotify(HeartDialUnlockStatus.UnlockSingle));

        if (missionId == 103040104)
            await Player.SendPacket(new PacketHeartDialScriptChangeScNotify(HeartDialUnlockStatus.UnlockAll));

        
        await HandleSubMissionReward(missionId);
        
        

        PluginEvent.InvokeOnPlayerFinishSubMission(Player, missionId);
        DatabaseHelper.Instance?.SaveUidData(Player.Uid);
    }

    public async ValueTask HandleFinishAction(MissionInfo info, int subMissionId)
    {
        var subMission = info.SubMissionList.Find(x => x.ID == subMissionId);
        if (subMission == null) return;

        foreach (var action in subMission.FinishActionList ?? []) await HandleFinishAction(action);
    }

    public async ValueTask HandleFinishAction(FinishActionInfo actionInfo)
    {
        ActionHandlers.TryGetValue(actionInfo.FinishActionType, out var handler);
        if (handler != null)
            await handler.OnHandle(actionInfo.FinishActionPara, actionInfo.FinishActionParaString, Player);
    }

    public async ValueTask HandleMissionReward(int mainMissionId)
    {
        GameData.MainMissionData.TryGetValue(mainMissionId, out var mainMission);
        if (mainMission == null) return;
        GameData.RewardDataData.TryGetValue(mainMission.RewardID, out var reward);
        var itemList = new List<ItemData>();

        foreach (var item in reward?.GetItems() ?? [])
        {
            GameData.ItemConfigData.TryGetValue(item.Item1, out var itemExcel);
            var res = await Player.InventoryManager!.AddItem(item.Item1, item.Item2,
                itemExcel?.ItemMainType == ItemMainTypeEnum.AvatarCard); 
            if (res != null) itemList.Add(res);
        }

        var hCoin = await Player.InventoryManager!.AddItem(1, reward?.Hcoin ?? 0, false);
        if (hCoin != null) itemList.Add(hCoin);

        foreach (var i in mainMission.SubRewardList)
        {
            GameData.RewardDataData.TryGetValue(i, out var rewardDataExcel);
            var hCoin2 = await Player.InventoryManager!.AddItem(1, rewardDataExcel?.Hcoin ?? 0, false); 
            if (hCoin2 != null) itemList.Add(hCoin2);
            foreach (var item in rewardDataExcel?.GetItems() ?? []) 
            {
                GameData.ItemConfigData.TryGetValue(item.Item1, out var itemExcel);
                var res = await Player.InventoryManager!.AddItem(item.Item1, item.Item2,
                    itemExcel?.ItemMainType == ItemMainTypeEnum.AvatarCard); 
                if (res != null) itemList.Add(res);
            }
        }


        if (itemList.Count > 0)
            await Player.SendPacket(new PacketMissionRewardScNotify(mainMissionId, 0, itemList));
    }

    public async ValueTask HandleSubMissionReward(int subMissionId)
    {
        GameData.SubMissionInfoData.TryGetValue(subMissionId, out var subMission);
        if (subMission == null) return;
        GameData.RewardDataData.TryGetValue(subMission.SubMissionInfo?.SubRewardID ?? 0, out var reward);
        var itemList = new List<ItemData>();

        foreach (var item in reward?.GetItems() ?? [])
        {
            GameData.ItemConfigData.TryGetValue(item.Item1, out var itemExcel);
            var res = await Player.InventoryManager!.AddItem(item.Item1, item.Item2,
                itemExcel?.ItemMainType == ItemMainTypeEnum.AvatarCard); 
            if (res != null) itemList.Add(res);
        }

        await Player.SendPacket(new PacketSubMissionRewardScNotify(subMissionId, itemList));
    }

    public async ValueTask HandleFinishType(MissionFinishTypeEnum finishType, object? arg = null, bool pushQuest = true)
    {
        FinishTypeHandlers.TryGetValue(finishType, out var handler);
        foreach (var mission in GetRunningSubMissionList())
            if (mission.FinishType == finishType)
                if (handler != null)
                    await handler.HandleMissionFinishType(Player, mission, arg);

        foreach (var quest in Player.QuestManager?.GetRunningQuest() ?? [])
        {
            if (Player.DailyActivityManager?.IsDailyQuest(quest.QuestId) == true) continue;
            var excel = GameData.QuestDataData.GetValueOrDefault(quest.QuestId);
            if (excel == null) continue;
            var finishWay = GameData.FinishWayData.GetValueOrDefault(excel.FinishWayID);
            if (finishWay == null) continue;
            if (finishWay.FinishType == finishType)
                if (handler != null)
                    await handler.HandleQuestFinishType(Player, excel, finishWay, arg);
        }

        if (pushQuest)
            await Player.QuestManager!.SyncQuest();
    }

    public async ValueTask HandleAllFinishType(object? arg = null)
    {
        foreach (var handler in FinishTypeHandlers) await HandleFinishType(handler.Key, arg, false);
        await Player.QuestManager!.SyncQuest();
    }

    public async ValueTask HandleTalkStr(string talkString)
    {
        if (Player.DailyActivityManager != null) await Player.DailyActivityManager.RefreshAsync();
        if (!ConfigManager.Config.ServerOption.EnableMission) return;

        foreach (var mission in GetRunningSubMissionList())
            if (mission.FinishType == MissionFinishTypeEnum.Talk)
                if (mission.ParamStr1 == talkString)
                    await FinishSubMission(mission.ID);

        foreach (var quest in Player.QuestManager?.GetRunningQuest() ?? [])
        {
            if (Player.DailyActivityManager?.IsDailyQuest(quest.QuestId) == true) continue;
            var excel = GameData.QuestDataData.GetValueOrDefault(quest.QuestId);
            if (excel == null) continue;
            var finishWay = GameData.FinishWayData.GetValueOrDefault(excel.FinishWayID);
            if (finishWay == null) continue;
            if (finishWay.FinishType == MissionFinishTypeEnum.Talk)
                if (finishWay.ParamStr1 == talkString)
                    await Player.QuestManager!.FinishQuest(quest.QuestId);
        }
    }

    public async ValueTask HandleCustomValue(List<MissionCustomValue> values, int missionId)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return;

        GameData.SubMissionInfoData.TryGetValue(missionId, out var subMission);
        if (subMission == null) return;
        var mainMissionId = subMission.MainMissionId;
        GameData.MainMissionData.TryGetValue(mainMissionId, out var mainMission);
        if (mainMission == null) return;

        foreach (var mission in mainMission.MissionInfo?.SubMissionList ?? [])
            if (mission.TakeType == SubMissionTakeTypeEnum.CustomValue)
            {
                var index = 0;
                var accept = false;
                List<List<int>> list = [mission.TakeParamIntList ?? []];
                if (mission.TakeParamIntList?.Count > 5)
                {
                    
                    var group = mission.TakeParamIntList.Count / 3;
                    list = [];
                    for (var i = 0; i < group; i++)
                    {
                        var customValue = mission.TakeParamIntList.GetRange(i * 3, 3);
                        list.Add(customValue);
                    }
                }

                foreach (var customValues in list)
                {
                    var thisAccept = true;
                    foreach (var customValue in customValues)
                    {
                        if (customValue == 0 && index == 0) continue; 
                        var valueInst = values.Find(x => x.Index == index);
                        if (valueInst == null) continue;
                        if (valueInst.CustomValue != customValue)
                        {
                            thisAccept = false;
                            break;
                        }

                        index++;
                    }

                    if (thisAccept) accept = true; 
                }

                if (accept) await AcceptSubMission(mission.ID);
            }
    }

    public async ValueTask AddMissionProgress(int missionId, int progress)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return;

        Data.SubMissionProgressDict.TryGetValue(missionId, out var currentProgress);
        Data.SubMissionProgressDict[missionId] = currentProgress + progress;
        DatabaseHelper.ToSaveUidList.SafeAdd(Player.Uid);
        GameData.SubMissionInfoData.TryGetValue(missionId, out var subMission);
        if (subMission == null) return;

        if (currentProgress + progress >= (subMission.SubMissionInfo?.Progress ?? 1)) return;

        var sync = new MissionSync();
        sync.MissionList.Add(new Proto.Mission
        {
            Id = (uint)missionId,
            Progress = (uint)(currentProgress + progress)
        });

        await Player.SendPacket(new PacketPlayerSyncScNotify(sync));
    }

    public async ValueTask SetMissionProgress(int missionId, int progress, bool sendPacket = true)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return;

        Data.SubMissionProgressDict[missionId] = progress;
        DatabaseHelper.ToSaveUidList.SafeAdd(Player.Uid);
        GameData.SubMissionInfoData.TryGetValue(missionId, out var subMission);
        if (subMission == null) return;

        if (progress >= (subMission.SubMissionInfo?.Progress ?? 1)) return;

        var sync = new MissionSync();
        sync.MissionList.Add(new Proto.Mission
        {
            Id = (uint)missionId,
            Progress = (uint)progress
        });

        if (sendPacket)
            await Player.SendPacket(new PacketPlayerSyncScNotify(sync));
    }

    #endregion

    #region Mission Status

    public MissionPhaseEnum GetMainMissionStatus(int missionId)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return MissionPhaseEnum.Finish;

        return Data.GetMainMissionStatus(missionId);
    }

    public MissionPhaseEnum GetSubMissionStatus(int missionId)
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return MissionPhaseEnum.Finish;

        return Data.GetSubMissionStatus(missionId);
    }

    public SubMissionInfo? GetSubMissionInfo(int missionId)
    {
        GameData.SubMissionInfoData.TryGetValue(missionId, out var subMission);
        if (subMission == null) return null;
        return subMission.SubMissionInfo;
    }

    public List<int> GetRunningSubMissionIdList()
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return [];

        var list = new List<int>();
        list.AddRange(Data.RunningSubMissionIds);
        return list;
    }

    public List<SubMissionInfo> GetRunningSubMissionList()
    {
        if (!ConfigManager.Config.ServerOption.EnableMission) return [];

        var list = new List<SubMissionInfo>();
        var ids = new List<int>();
        ids.AddRange(Data.RunningSubMissionIds);
        foreach (var id in ids)
        {
            GameData.SubMissionInfoData.TryGetValue(id, out var mission);
            if (mission != null && mission.SubMissionInfo != null) list.Add(mission.SubMissionInfo);
        }

        return list;
    }

    public int GetMissionProgress(int missionId)
    {
        GameData.SubMissionInfoData.TryGetValue(missionId, out var subMission);
        if (!ConfigManager.Config.ServerOption.EnableMission) return subMission?.SubMissionInfo?.Progress ?? 0;

        Data.SubMissionProgressDict.TryGetValue(missionId, out var progress);
        return progress;
    }

    #endregion

    #region Handlers

    public async ValueTask OnBattleFinish(BattleInstance instance, PVEBattleResultCsReq req)
    {
        foreach (var mission in GetRunningSubMissionIdList())
        {
            var subMission = GetSubMissionInfo(mission);
            if (subMission != null && subMission.FinishType == MissionFinishTypeEnum.StageWin &&
                req.EndStatus == BattleEndStatus.BattleEndWin) 
                if (req.StageId == instance.StageId &&
                    (instance.StageId == subMission.ParamInt1 || instance.StageId / 10 == subMission.ParamInt1))
                {
                    await FinishSubMission(mission);
                    instance.EventId = 0;
                }
        }

        await HandleAllFinishType(instance);
    }

    public async ValueTask OnPlayerInteractWithProp()
    {
        foreach (var id in GetRunningSubMissionIdList())
            if (GetSubMissionInfo(id)?.FinishType == MissionFinishTypeEnum.PropState)
            {
                FinishTypeHandlers.TryGetValue(MissionFinishTypeEnum.PropState, out var handler);
                if (handler != null) await handler.HandleMissionFinishType(Player, GetSubMissionInfo(id)!, null);
            }
    }

    public async ValueTask OnMissionPropInteracted(int floorId, int groupId, int propId)
    {
        if (Player.DailyActivityManager != null) await Player.DailyActivityManager.RefreshAsync();
        foreach (var mission in GetRunningSubMissionList().ToList())
        {
            if (mission.FinishType != MissionFinishTypeEnum.InteractPropList ||
                mission.LevelFloorID != floorId || mission.ParamInt1 != groupId ||
                mission.ParamIntList?.Contains(propId) != true) continue;
            if (!Data.InteractedMissionProps.TryGetValue(mission.ID, out var seen))
                Data.InteractedMissionProps[mission.ID] = seen = [];
            if (seen.Contains(propId)) continue;
            seen.Add(propId);
            await SetMissionProgress(mission.ID, seen.Count);
            if (seen.Count >= Math.Max(1, mission.Progress)) await FinishSubMission(mission.ID);
            DatabaseHelper.Instance?.SaveUidData(Player.Uid);
        }
    }

    public async ValueTask OnPlayerChangeScene()
    {
        foreach (var id in GetRunningSubMissionIdList())
        {
            var info = GetSubMissionInfo(id);
            if (info == null) continue;

            if (info.LevelFloorID == Player.SceneInstance?.FloorId)
            {
                if (info.GroupIDList == null) continue;
                foreach (var group in info.GroupIDList)
                    await Player.SceneInstance.EntityLoader!.LoadGroup(group, false);
            }
        }
    }

    public void OnLoadScene(SceneInfo info)
    {
        var targetSubIds =
            Player.SceneInstance?.FloorInfo?.Groups.Values.SelectMany(x => x.RelatedMissionId).ToList() ?? [];

        HashSet<int> mainIds = [];
        foreach (var mainMission in GameData.MainMissionData.Values)
        foreach (var subMission in mainMission.MissionInfo.SubMissionList)
            if (targetSubIds.Contains(subMission.ID))
            {
                info.SceneMissionInfo.SubMissionStatusList.Add(new Proto.Mission
                {
                    Id = (uint)subMission.ID,
                    Status = GetSubMissionStatus(subMission.ID).ToProto(),
                    Progress = (uint)GetMissionProgress(subMission.ID)
                });

                mainIds.Add(mainMission.MainMissionID);
            }

        foreach (var mainId in mainIds)
            if (GetMainMissionStatus(mainId) == MissionPhaseEnum.Finish)
                info.SceneMissionInfo.FinishedMainMissionIdList.Add((uint)mainId);
            else if (GetMainMissionStatus(mainId) == MissionPhaseEnum.Accept)
                info.SceneMissionInfo.UnfinishedMainMissionIdList.Add((uint)mainId);

        
        
        
        
        var currentFloor = Player.SceneInstance?.FloorId ?? 0;
        if (!ConfigManager.Config.ServerOption.EnableMission && currentFloor != 0)
            foreach (var pkg in GameData.ContentPackageConfigData.Values)
            {
                if (pkg.MainMissionIDList.Count == 0) continue;
                if (!pkg.TouchesFloor(currentFloor)) continue;

                foreach (var missionId in pkg.MainMissionIDList)
                {
                    var mid = (uint)missionId;
                    if (info.SceneMissionInfo.FinishedMainMissionIdList.Contains(mid)) continue;
                    if (info.SceneMissionInfo.UnfinishedMainMissionIdList.Contains(mid)) continue;
                    info.SceneMissionInfo.UnfinishedMainMissionIdList.Add(mid);
                }
            }
    }

    #endregion
}
