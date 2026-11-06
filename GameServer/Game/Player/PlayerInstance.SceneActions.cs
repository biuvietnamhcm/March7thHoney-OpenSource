using March7thHoney.Data;
using March7thHoney.Database;
using March7thHoney.Database.Scene;
using March7thHoney.Enums.Mission;
using March7thHoney.Enums.Scene;
using March7thHoney.GameServer.Game.Drop;
using March7thHoney.GameServer.Game.Scene;
using March7thHoney.GameServer.Game.Scene.Entity;
using March7thHoney.GameServer.Server.Packet.Send.Lineup;
using March7thHoney.GameServer.Server.Packet.Send.MarkChest;
using March7thHoney.GameServer.Server.Packet.Send.Scene;
using March7thHoney.Kcp;
using March7thHoney.Proto;
using March7thHoney.Util;
using static March7thHoney.GameServer.Plugin.Event.PluginEvent;

namespace March7thHoney.GameServer.Game.Player;

public partial class PlayerInstance
{
    private readonly SemaphoreSlim _worldChestInteractionLock = new(1, 1);
    #region Scene Actions

    public async ValueTask OnMove()
    {
        if (SceneInstance != null)
        {
            var prop = SceneInstance.GetNearestSpring(25_000_000);

            var isInRange = prop != null;

            if (isInRange)
                if (LineupManager?.GetCurLineup()?.Heal(10000, true) == true)
                    await SendPacket(new PacketSyncLineupNotify(LineupManager.GetCurLineup()!));
        }
    }

    public async ValueTask<EntityProp?> InteractProp(int propEntityId, int interactId)
    {
        if (SceneInstance == null) return null;
        SceneInstance.Entities.TryGetValue(propEntityId, out var entity);
        if (entity is not EntityProp prop) return null;
        if (!prop.IsPersistentWorldChest) return await InteractPropCore(prop, interactId);

        await _worldChestInteractionLock.WaitAsync();
        try
        {
            // The entity ID may have been reused by a scene change while waiting.
            if (SceneInstance != prop.Scene) return null;
            if (prop.WasOpened)
            {
                await prop.SetState(PropStateEnum.ChestUsed);
                return prop;
            }
            return await InteractPropCore(prop, interactId);
        }
        finally
        {
            _worldChestInteractionLock.Release();
        }
    }

    private async ValueTask<EntityProp?> InteractPropCore(EntityProp prop, int interactId)
    {
        GameData.InteractConfigData.TryGetValue(interactId, out var config);
        if (config == null || config.SrcState != prop.State) return prop;
        var oldState = prop.State;
        var opensWorldChest = prop.IsPersistentWorldChest && oldState == PropStateEnum.ChestClosed
            && config.TargetState == PropStateEnum.ChestUsed;
        // Leave the chest claimable when its reward resources are missing.
        var chestItems = opensWorldChest ? DropService.CalculateDropsFromProp(prop.PropInfo.ChestID) : null;
        if (opensWorldChest && (InventoryManager == null || SceneData == null || chestItems!.Count == 0 ||
                               chestItems.Any(item => !GameData.ItemConfigData.ContainsKey(item.ItemId))))
            return prop;

        await prop.SetState(config.TargetState, opensWorldChest ? false : prop.Scene.IsLoaded);
        var newState = prop.State;

        switch (prop.Excel.PropType)
        {
            case PropTypeEnum.PROP_TREASURE_CHEST:
                if (oldState == PropStateEnum.ChestClosed && newState == PropStateEnum.ChestUsed)
                {
                    
                    var items = chestItems ?? DropService.CalculateDropsFromProp(prop.PropInfo.ChestID);
                    var notifyMark = false;
                    foreach (var markedChest in SceneData!.MarkedChestData.Values)
                    {
                        var chest = markedChest.Find(x =>
                            x.FloorId == prop.Scene.FloorId && x.GroupId == prop.GroupId &&
                            x.ConfigId == prop.PropInfo.ID);

                        if (chest == null) continue;
                        markedChest.Remove(chest);
                        notifyMark = true;
                    }

                    try
                    {
                        await InventoryManager!.AddItems(items);
                    }
                    finally
                    {
                        // Save the claim, removed map mark and inventory without waiting for the heartbeat timer.
                        if (opensWorldChest) DatabaseHelper.Instance?.SaveUidData(Uid);
                    }
                    if (opensWorldChest) await prop.SetState(PropStateEnum.ChestUsed, prop.Scene.IsLoaded);
                    await SendPacket(new PacketOpenChestScNotify(prop.PropInfo.ChestID));

                    if (notifyMark)
                        await SendPacket(new PacketMarkChestChangedScNotify(this));
                }

                break;
            case PropTypeEnum.PROP_DESTRUCT:
                if (newState == PropStateEnum.Closed) await prop.SetState(PropStateEnum.Open);
                break;
            case PropTypeEnum.PROP_MAZE_JIGSAW:
                switch (newState)
                {
                    case PropStateEnum.Closed:
                    {
                        foreach (var p in SceneInstance.GetEntitiesInGroup<EntityProp>(prop.GroupId))
                            if (p.Excel.PropType == PropTypeEnum.PROP_TREASURE_CHEST)
                            {
                                await p.SetState(PropStateEnum.ChestClosed);
                            }
                            else if (p.Excel.PropType == prop.Excel.PropType)
                            {
                                
                            }
                            else
                            {
                                await p.SetState(PropStateEnum.Open);
                            }

                        break;
                    }
                    case PropStateEnum.Open:
                    {
                        foreach (var p in SceneInstance.GetEntitiesInGroup<EntityProp>(prop.GroupId).Where(p =>
                                     p.Excel.PropType is not PropTypeEnum.PROP_TREASURE_CHEST &&
                                     p.Excel.PropType != prop.Excel.PropType))
                            await p.SetState(PropStateEnum.Open);

                        break;
                    }
                }

                break;
            case PropTypeEnum.PROP_MAZE_PUZZLE:
                if (newState is PropStateEnum.Closed or PropStateEnum.Open)
                    foreach (var p in SceneInstance.GetEntitiesInGroup<EntityProp>(prop.GroupId))
                    {
                        if (p.Excel.PropType == PropTypeEnum.PROP_TREASURE_CHEST)
                        {
                            await p.SetState(PropStateEnum.ChestClosed);
                        }
                        else if (p.Excel.PropType == prop.Excel.PropType)
                        {
                            
                        }
                        else
                        {
                            await p.SetState(PropStateEnum.Open);
                        }

                        await MissionManager!.OnPlayerInteractWithProp();
                    }

                break;
            case PropTypeEnum.PROP_ORDINARY:
                if (prop.PropInfo.CommonConsole)
                    
                    foreach (var p in SceneInstance.GetEntitiesInGroup<EntityProp>(prop.GroupId))
                    {
                        await p.SetState(newState);

                        await MissionManager!.OnPlayerInteractWithProp();
                    }

                if (prop.Excel.ID == 104039)
                {
                    foreach (var p in SceneInstance.GetEntitiesInGroup<EntityProp>(prop.GroupId))
                        await p.SetState(newState);

                    await MissionManager!.OnPlayerInteractWithProp();
                }

                if (prop.PropInfo.Name.Contains("Piece"))
                {
                    var pieceDone = SceneInstance.GetEntitiesInGroup<EntityProp>(prop.GroupId)
                        .Where(p => p.PropInfo.Name.Contains("Piece")).All(p => p.State == PropStateEnum.Closed);

                    if (pieceDone)
                        
                        foreach (var p in SceneInstance.GetEntitiesInGroup<EntityProp>(prop.GroupId)
                                     .Where(p => p.PropInfo.Name.Contains("JigsawSir") &&
                                                 p.State != PropStateEnum.Closed))
                            await p.SetState(PropStateEnum.TriggerEnable);
                }

                break;
        }

        
        if (prop.PropInfo.UnlockDoorID.Count > 0)
            foreach (var p in prop.PropInfo.UnlockDoorID.SelectMany(id =>
                         SceneInstance.GetEntitiesInGroup<EntityProp>(id.Key)
                             .Where(p => id.Value.Contains(p.PropInfo.ID))))
            {
                await p.SetState(PropStateEnum.Open);
                await MissionManager!.OnPlayerInteractWithProp();
            }

        
        if (MissionManager != null)
        {
            await MissionManager.OnPlayerInteractWithProp();
            await MissionManager.OnMissionPropInteracted(prop.Scene.FloorId, prop.GroupId, prop.PropInfo.ID);
        }

        
        InventoryManager!.HandlePlaneEvent(prop.PropInfo.EventID);

        
        InvokeOnPlayerInteract(this, prop);

        var floorSavedKey = prop.PropInfo.Name.Replace("Controller_", "");
        var key = $"FSV_ML{floorSavedKey}{(config.TargetState == PropStateEnum.Open ? "Started" : "Complete")}";

        if (prop.Group.GroupName.Contains("JigsawPuzzle") && prop.Group.GroupName.Contains("MainLine"))
        {
            var splits = prop.Group.GroupName.Split('_');
            key =
                $"JG_ML_{splits[3]}_Puzzle{(config.TargetState == PropStateEnum.Open ? "Started" : "Complete")}";
        }

        if (SceneInstance?.FloorInfo?.FloorSavedValue.Find(x => x.Name == key) != null)
        {
            
            var plane = SceneInstance.PlaneId;
            var floor = SceneInstance.FloorId;
            SceneData!.FloorSavedData.TryGetValue(floor, out var value);
            if (value == null)
            {
                value = [];
                SceneData.FloorSavedData[floor] = value;
            }

            value[key] = 1; 
            await SendPacket(new PacketUpdateFloorSavedValueNotify(key, 1, this));

            if (TaskManager != null) await TaskManager.SceneTaskTrigger.TriggerFloor(plane, floor);
            MissionManager?.HandleFinishType(MissionFinishTypeEnum.FloorSavedValue);
        }

        if (prop.PropInfo.IsLevelBtn) await prop.SetState(PropStateEnum.Closed);

        return prop;
    }

    public async ValueTask SetPropTimeline(int propEntityId, PropTimelineInfo info)
    {
        if (SceneInstance == null) return;
        SceneInstance.Entities.TryGetValue(propEntityId, out var entity);
        if (entity is not EntityProp prop) return;

        var data = new ScenePropTimelineData
        {
            BoolValue = info.TimelineBoolValue,
            ByteValue = info.TimelineByteValue.ToBase64()
        };

        
        SceneData!.PropTimelineData.TryGetValue(Data.FloorId, out var floorData);
        if (floorData == null)
        {
            floorData = new Dictionary<int, Dictionary<int, ScenePropTimelineData>>();
            SceneData.PropTimelineData[Data.FloorId] = floorData;
        }

        if (!floorData.ContainsKey(prop.GroupId))
            floorData[prop.GroupId] = new Dictionary<int, ScenePropTimelineData>();

        floorData[prop.GroupId][prop.PropInfo.ID] = data;

        prop.PropTimelineData = data;

        
        await MissionManager!.HandleFinishType(MissionFinishTypeEnum.TimeLineSetState);
        await MissionManager!.HandleFinishType(MissionFinishTypeEnum.TimeLineSetStateCnt);
    }

    public ScenePropTimelineData? GetScenePropTimelineData(int floorId, int groupId, int propId)
    {
        SceneData!.PropTimelineData.TryGetValue(floorId, out var floorData);
        if (floorData == null) return null;
        floorData.TryGetValue(groupId, out var groupData);
        if (groupData == null) return null;
        groupData.TryGetValue(propId, out var data);
        return data;
    }

    public async ValueTask<bool> EnterScene(int entryId, int teleportId, bool sendPacket, int storyLineId = 0,
        bool mapTp = false)
    {
        var beforeStoryLineId = StoryLineManager?.StoryLineData.CurStoryLineId;
        if (storyLineId != StoryLineManager?.StoryLineData.CurStoryLineId)
        {
            if (StoryLineManager != null)
                await StoryLineManager.EnterStoryLine(storyLineId, entryId == 0); 
            mapTp = false; 
        }

        GameData.MapEntranceData.TryGetValue(entryId, out var entrance);
        if (entrance == null) return false;

        GameData.GetFloorInfo(entrance.PlaneID, entrance.FloorID, out var floorInfo);

        var startGroup = entrance.StartGroupID;
        var startAnchor = entrance.StartAnchorID;

        if (teleportId != 0)
        {
            floorInfo.CachedTeleports.TryGetValue(teleportId, out var teleport);
            if (teleport != null)
            {
                startGroup = teleport.AnchorGroupID;
                startAnchor = teleport.AnchorID;
            }
        }
        else if (startAnchor == 0)
        {
            startGroup = floorInfo.StartGroupID;
            startAnchor = floorInfo.StartAnchorID;
        }

        var anchor = floorInfo.GetAnchorInfo(startGroup, startAnchor);

        await MissionManager!.HandleFinishType(MissionFinishTypeEnum.EnterMapByEntrance, entrance);

        var beforeEntryId = Data.EntryId;

        await LoadScene(entrance.PlaneID, entrance.FloorID, entryId, anchor!.ToPositionProto(),
            anchor.ToRotationProto(), sendPacket, mapTp);

        var afterEntryId = Data.EntryId;

        return beforeEntryId != afterEntryId ||
               beforeStoryLineId != storyLineId; 
    }

    public async ValueTask EnterSceneByEntranceId(int entranceId, int anchorGroupId, int anchorId, bool sendPacket)
    {
        GameData.MapEntranceData.TryGetValue(entranceId, out var entrance);
        if (entrance == null) return;

        GameData.GetFloorInfo(entrance.PlaneID, entrance.FloorID, out var floorInfo);

        var startGroup = anchorGroupId == 0 ? entrance.StartGroupID : anchorGroupId;
        var startAnchor = anchorId == 0 ? entrance.StartAnchorID : anchorId;

        if (startAnchor == 0)
        {
            startGroup = floorInfo.StartGroupID;
            startAnchor = floorInfo.StartAnchorID;
        }

        var anchor = floorInfo.GetAnchorInfo(startGroup, startAnchor);

        await LoadScene(entrance.PlaneID, entrance.FloorID, entranceId, anchor!.ToPositionProto(),
            anchor.ToRotationProto(), sendPacket);
    }

    public async ValueTask MoveTo(Position position)
    {
        Data.Pos = position;
        await SendPacket(new PacketSceneEntityMoveScNotify(this));
    }

    public void MoveTo(EntityMotion motion)
    {
        Data.Pos = motion.Motion.Pos.ToPosition();
        Data.Rot = motion.Motion.Rot.ToPosition();
    }

    public async ValueTask MoveTo(Position pos, Position rot)
    {
        Data.Pos = pos;
        Data.Rot = rot;
        await SendPacket(new PacketSceneEntityMoveScNotify(this));
    }

    public async ValueTask LoadScene(int planeId, int floorId, int entryId, Position pos, Position rot, bool sendPacket,
        bool mapTp = false)
    {
        GameData.MazePlaneData.TryGetValue(planeId, out var plane);
        if (plane == null) return;

        if (plane.PlaneType == PlaneTypeEnum.Raid && RaidManager!.RaidData.CurRaidId == 0)
        {
            await EnterScene(2000101, 0, sendPacket);
            return;
        }

        
        
        if (plane.PlaneType == PlaneTypeEnum.Challenge && ChallengeManager!.ChallengeInstance == null &&
            entryId is not GameConstants.CHALLENGE_ENTRANCE and
                not GameConstants.CHALLENGE_BOSS_ENTRANCE and
                not GameConstants.CHALLENGE_PEAK_ENTRANCE and
                not GameConstants.CHALLENGE_STORY_ENTRANCE)
        {
            await EnterScene(100000103, 0, sendPacket);
            return;
        }

        
        Data.Pos = pos;
        Data.Rot = rot;
        var notSendMove = true;
        if (planeId != Data.PlaneId || floorId != Data.FloorId || entryId != Data.EntryId || SceneInstance == null ||
            !mapTp)
        {
            if (SceneInstance != null)
                await SceneInstance.OnDestroy();
            SceneInstance instance = new(this, plane, floorId, entryId);
            InvokeOnPlayerLoadScene(this, instance);
            SceneInstance = instance;

            await instance.SyncLineup(true);
            Data.PlaneId = planeId;
            Data.FloorId = floorId;
            Data.EntryId = entryId;
            // Floor scripts must see the newly assigned scene and its coordinates.
            if (TaskManager != null) await TaskManager.SceneTaskTrigger.TriggerFloor(planeId, floorId);
        }
        else if (StoryLineManager?.StoryLineData.CurStoryLineId == 0 &&
                 mapTp) 
        {
            notSendMove = false;
        }

        if (MissionManager != null)
            await MissionManager.OnPlayerChangeScene();

        Connection?.SendPacket(CmdIds.SyncServerSceneChangeNotify);
        if (sendPacket && notSendMove)
            await SendPacket(new PacketEnterSceneByServerScNotify(SceneInstance!));
        else if (sendPacket && !notSendMove) 
            await SendPacket(new PacketSceneEntityMoveScNotify(this));

        if (MissionManager != null)
        {
            await MissionManager.HandleFinishType(MissionFinishTypeEnum.EnterFloor);
            await MissionManager.HandleFinishType(MissionFinishTypeEnum.EnterPlane);
            await MissionManager.HandleFinishType(MissionFinishTypeEnum.NotInFloor);
            await MissionManager.HandleFinishType(MissionFinishTypeEnum.NotInPlane);
        }
    }

    public ScenePropData? GetScenePropData(int floorId, int groupId, int propId)
    {
        if (SceneData != null)
            if (SceneData.ScenePropData.TryGetValue(floorId, out var floorData))
                if (floorData.TryGetValue(groupId, out var groupData))
                {
                    var propData = groupData.Find(x => x.PropId == propId);
                    return propData;
                }

        return null;
    }

    public void SetScenePropData(int floorId, int groupId, int propId, PropStateEnum state, bool chestOpened = false)
    {
        if (SceneData != null)
        {
            if (!SceneData.ScenePropData.TryGetValue(floorId, out var floorData))
            {
                floorData = [];
                SceneData.ScenePropData.Add(floorId, floorData);
            }

            if (!floorData.TryGetValue(groupId, out var groupData))
            {
                groupData = [];
                floorData.Add(groupId, groupData);
            }

            var propData = groupData.Find(x => x.PropId == propId); 
            if (propData == null)
            {
                propData = new ScenePropData
                {
                    PropId = propId,
                    State = state,
                    ChestOpened = chestOpened
                };
                groupData.Add(propData);
            }
            else
            {
                propData.ChestOpened |= chestOpened;
                propData.State = propData.ChestOpened ? PropStateEnum.ChestUsed : state;
            }
            DatabaseHelper.ToSaveUidList.SafeAdd(Uid);
        }
    }

    public void EnterSection(int sectionId)
    {
        if (SceneInstance != null)
        {
            SceneData!.UnlockSectionIdList.TryGetValue(SceneInstance.FloorId, out var unlockList);
            if (unlockList == null)
            {
                unlockList = [sectionId];
                SceneData.UnlockSectionIdList.Add(SceneInstance.FloorId, unlockList);
            }
            else
            {
                SceneData.UnlockSectionIdList[SceneInstance.FloorId].Add(sectionId);
            }
        }
    }

    public void SetCustomSaveData(int entryId, int groupId, string data)
    {
        if (SceneData != null)
        {
            if (!SceneData.CustomSaveData.TryGetValue(entryId, out var entryData))
            {
                entryData = [];
                SceneData.CustomSaveData.Add(entryId, entryData);
            }

            entryData[groupId] = data;
        }
    }

    public async ValueTask ForceQuitBattle()
    {
        if (BattleInstance != null)
        {
            BattleInstance = null;
            await Connection!.SendPacket(CmdIds.QuitBattleScNotify);
        }
    }

    #endregion
}

