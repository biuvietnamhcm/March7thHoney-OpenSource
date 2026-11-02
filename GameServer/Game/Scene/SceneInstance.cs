using March7thHoney.Data;
using March7thHoney.Data.Config.Scene;
using March7thHoney.Data.Excel;
using March7thHoney.Database.Avatar;
using March7thHoney.Enums.Avatar;
using March7thHoney.Enums.Mission;
using March7thHoney.Enums.Scene;
using March7thHoney.GameServer.Game.Activity.Loaders;
using March7thHoney.GameServer.Game.Battle;
using March7thHoney.GameServer.Game.Challenge;
using March7thHoney.GameServer.Game.Mission;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Game.Scene.Component;
using March7thHoney.GameServer.Game.Scene.Entity;
using March7thHoney.GameServer.Server.Packet.Send.Scene;
using March7thHoney.Proto;
using March7thHoney.Util;

namespace March7thHoney.GameServer.Game.Scene;

public class SceneInstance
{
    #region Scene Details

    public EntityProp? GetNearestSpring(long minDistSq)
    {
        EntityProp? spring = null;
        long springDist = 0;

        foreach (var prop in HealingSprings)
        {
            var dist = Player.Data?.Pos?.GetFast2dDist(prop.Position) ?? 1000000;
            if (dist > minDistSq) continue;

            if (spring == null || dist < springDist)
            {
                spring = prop;
                springDist = dist;
            }
        }

        return spring;
    }

    #endregion

    #region Serialization

    public int ResolveDimensionId()
    {
        if (EntityLoader is StoryLineEntityLoader loader && loader.DimensionId != 0)
            return loader.DimensionId;

        return ResolveDimensionId(FloorInfo, EntryId, Groups);
    }

    public static int ResolveDimensionId(FloorInfo? floorInfo, int entryId = 0, IEnumerable<int>? preferredGroupIds = null)
    {
        if (floorInfo == null)
            return 0;

        HashSet<int> candidateGroupIds = [];
        if (entryId != 0
            && GameData.MapEntranceData.TryGetValue(entryId, out var entrance)
            && entrance.FloorID == floorInfo.FloorID)
            candidateGroupIds.Add(entrance.StartGroupID);

        if (floorInfo.StartGroupID != 0)
            candidateGroupIds.Add(floorInfo.StartGroupID);

        if (preferredGroupIds != null)
            foreach (var groupId in preferredGroupIds.Where(groupId => groupId != 0))
                candidateGroupIds.Add(groupId);

        foreach (var groupId in candidateGroupIds)
        {
            var dimension = floorInfo.DimensionList.FirstOrDefault(info => info.GroupIDList.Contains(groupId));
            if (dimension != null)
                return dimension.ID;
        }

        return floorInfo.DimensionList.FirstOrDefault(info => info.ID != 0)?.ID
               ?? floorInfo.DimensionList.FirstOrDefault()?.ID
               ?? 0;
    }

    public SceneInfo ToProto()
    {
        SceneInfo sceneInfo = new()
        {
            WorldId = (uint)(Excel.WorldID == 100 ? GameConstants.LAST_TRAIN_WORLD_ID : Excel.WorldID),
            GameModeType = (uint)GameModeType,
            PlaneId = (uint)PlaneId,
            FloorId = (uint)FloorId,
            EntryId = (uint)EntryId,
            SceneMissionInfo = new MissionStatusBySceneInfo(),
            DimensionId = (uint)ResolveDimensionId(),
            GameStoryLineId = (uint)(Player.StoryLineManager?.StoryLineData.CurStoryLineId ?? 0),
            
            SceneIdentifier = new SceneIdentifier
            {
                FloorId = (uint)FloorId,
                GameStoryLineId = (uint)(Player.StoryLineManager?.StoryLineData.CurStoryLineId ?? 0)
            }
        };

        if (Player.TrainCakeCatchManager?.GetSceneTeleportRoomOwnerUid() is { } roomOwnerUid)
            sceneInfo.SceneIdentifier.TeleportInfo = new TeleportInfo
            {
                TeleportId = roomOwnerUid
            };

        var playerGroupInfo = new SceneEntityGroupInfo(); 
        foreach (var avatar in AvatarInfo)
            playerGroupInfo.EntityList.Add(avatar.Value.ToProto());
        if (playerGroupInfo.EntityList.Count > 0)
        {
            if (LeaderEntityId == 0) LeaderEntityId = AvatarInfo.Values.First().EntityId;

            sceneInfo.LeaderEntityId = (uint)LeaderEntityId;
        }

        foreach (var summonUnit in SummonUnit.Values) playerGroupInfo.EntityList.Add(summonUnit.ToProto());

        sceneInfo.EntityGroupList.Add(playerGroupInfo);

        List<SceneEntityGroupInfo> groups = []; 

        
        foreach (var entity in Entities)
        {
            if (entity.Value.GroupId == 0) continue;
            if (groups.FindIndex(x => x.GroupId == entity.Value.GroupId) == -1)
            {
                var property = FloorInfo?.Groups.GetValueOrDefault(entity.Value.GroupId)?.GroupPropertyMap ?? [];

                Dictionary<string, int> resProperty = [];
                var savedProp = Player.SceneData!.GroupPropertyData.GetValueOrDefault(FloorId, [])
                    .GetValueOrDefault(entity.Value.GroupId, []);

                foreach (var info in property.Values.Where(x => x.Side != GroupPropertySideEnum.ClientOnly))
                    resProperty.Add(info.Name, savedProp.GetValueOrDefault(info.Name, info.DefaultValue));

                groups.Add(new SceneEntityGroupInfo
                {
                    GroupId = (uint)entity.Value.GroupId,
                    OPLJDFOOMGF = { resProperty },
                    State = 1
                });
            }

            groups[groups.FindIndex(x => x.GroupId == entity.Value.GroupId)].EntityList.Add(entity.Value.ToProto());
        }

        foreach (var groupId in Groups) 
            if (groups.FindIndex(x => x.GroupId == groupId) == -1)
            {
                var property = FloorInfo?.Groups.GetValueOrDefault(groupId)?.GroupPropertyMap ?? [];

                Dictionary<string, int> resProperty = [];
                var savedProp = Player.SceneData!.GroupPropertyData.GetValueOrDefault(FloorId, [])
                    .GetValueOrDefault(groupId, []);

                foreach (var info in property.Values.Where(x => x.Side != GroupPropertySideEnum.ClientOnly))
                    resProperty.Add(info.Name, savedProp.GetValueOrDefault(info.Name, info.DefaultValue));

                groups.Add(new SceneEntityGroupInfo
                {
                    GroupId = (uint)groupId,
                    OPLJDFOOMGF = { resProperty },
                    State = 1
                });
            }

        foreach (var group in groups) sceneInfo.EntityGroupList.Add(group);

        
        Player.SceneData!.CustomSaveData.TryGetValue(EntryId, out var data);

        if (data != null)
            foreach (var customData in data)
                sceneInfo.CustomDataList.Add(new CustomSaveData
                {
                    GroupId = (uint)customData.Key,
                    SaveData = customData.Value
                });

        Player.SceneData!.FloorSavedData.TryGetValue(FloorId, out var floorData);

        var floorTouchedByActivity = GameData.ContentPackageConfigData.Values
            .Any(pkg => pkg.MainMissionIDList.Count > 0 && pkg.TouchesFloor(FloorId));

        foreach (var value in FloorInfo?.FloorSavedValue ?? [])
            if (floorData != null && floorData.TryGetValue(value.Name, out var v))
                sceneInfo.FloorSavedData[value.Name] = v;
            else if (value.Name.Contains("_IsHidden"))
                sceneInfo.FloorSavedData[value.Name] = 0;
            else if (floorTouchedByActivity && value.DefaultValue == 0)
                sceneInfo.FloorSavedData[value.Name] = 1;
            else
                sceneInfo.FloorSavedData[value.Name] = value.DefaultValue;

        foreach (var value in floorData ?? [])
            sceneInfo.FloorSavedData[value.Key] = value.Value;

        
        Player.MissionManager!.OnLoadScene(sceneInfo);

        
        if (!ConfigManager.Config.ServerOption.AutoLightSection)
        {
            Player.SceneData!.UnlockSectionIdList.TryGetValue(FloorId, out var unlockSectionList);
            if (unlockSectionList != null)
                foreach (var sectionId in unlockSectionList)
                    sceneInfo.LightenSectionList.Add((uint)sectionId);
        }
        else
        {
            GameData.GetFloorInfo(PlaneId, FloorId, out var floorInfo);
            sceneInfo.LightenSectionList.AddRange(floorInfo.MapSections.Select(x => (uint)x));
        }

        return sceneInfo;
    }

    #endregion

    #region Data

    public PlayerInstance Player { get; set; }
    public MazePlaneExcel Excel { get; set; }
    public FloorInfo? FloorInfo { get; set; }
    public int FloorId { get; set; }
    public int PlaneId { get; set; }
    public int EntryId { get; set; }

    public int LeaveEntryId { get; set; }
    public int LastEntityId { get; set; }
    public bool IsLoaded { get; set; } = false;

    public Dictionary<int, AvatarSceneInfo> AvatarInfo { get; set; } = [];
    public int LeaderEntityId { get; set; }
    public Dictionary<int, BaseGameEntity> Entities { get; set; } = [];
    public List<int> Groups { get; set; } = [];
    public List<EntityProp> HealingSprings { get; set; } = [];

    public SceneEntityLoader? EntityLoader { get; set; }

    public GameModeTypeEnum GameModeType { get; set; }
    public List<BaseSceneComponent> Components { get; set; } = [];

    public Dictionary<int, EntitySummonUnit> SummonUnit { get; set; } = [];

    public SceneInstance(PlayerInstance player, MazePlaneExcel excel, int floorId, int entryId)
    {
        Player = player;
        Excel = excel;
        PlaneId = excel.PlaneID;
        FloorId = floorId;
        EntryId = entryId;
        LeaveEntryId = 0;

        GameData.GetFloorInfo(PlaneId, FloorId, out var floor);
        FloorInfo = floor;
        if (FloorInfo == null) return;

        GameModeType = (GameModeTypeEnum)excel.PlaneType;
        switch (Excel.PlaneType)
        {
            case PlaneTypeEnum.Rogue:
                EntityLoader = new SceneEntityLoader(this);
                break;
            case PlaneTypeEnum.Challenge:
                EntityLoader = new ChallengeEntityLoader(this, Player);
                break;
            case PlaneTypeEnum.TrialActivity:
                EntityLoader = new TrialActivityEntityLoader(this, Player);
                break;
            default:
                EntityLoader = Player.StoryLineManager?.StoryLineData.CurStoryLineId != 0
                    ? new StoryLineEntityLoader(this)
                    : new SceneEntityLoader(this);
                break;
        }

        foreach (var module in floor.LevelFeatureModules.ToHashSet())
            switch (module)
            {
                case LevelFeatureTypeEnum.RotatableRegion:
                    Components.Add(new RotatableRegionSceneComponent(this));
                    break;
            }

        if (GameData.SceneRainbowGroupPropertyData.FloorProperty.ContainsKey(FloorId))
            Components.Add(new RainbowSceneComponent(this));

        System.Threading.Tasks.Task.Run(async () => { await EntityLoader.LoadEntity(); }).Wait();

        _ = InitializeComponents();
    }

    #endregion

    #region Event

    public delegate ValueTask GroupPropertyUpdateArg(GroupPropertyRefreshData data);

    public event GroupPropertyUpdateArg? GroupPropertyUpdated;

    #endregion

    #region Scene Actions

    public async ValueTask<GroupPropertyRefreshData> UpdateGroupProperty(int groupId, string name, int value,
        bool callEvent = true)
    {
        
        if (!Player.SceneData!.GroupPropertyData.TryGetValue(FloorId, out var groupData))
        {
            groupData = [];
            Player.SceneData.GroupPropertyData[FloorId] = groupData;
        }

        var group = FloorInfo?.Groups.GetValueOrDefault(groupId);
        if (group == null) return new GroupPropertyRefreshData(groupId, name, 0, value);
        var property = group.GroupPropertyMap.Values
            .FirstOrDefault(x => x.Name == name);
        if (property == null) return new GroupPropertyRefreshData(groupId, name, 0, value);

        if (!groupData.TryGetValue(groupId, out var propertyData))
        {
            propertyData = [];
            groupData[groupId] = propertyData;
        }

        var oldValue = propertyData.GetValueOrDefault(name, property.DefaultValue);
        propertyData[name] = value;
        
        var res = new GroupPropertyRefreshData(groupId, name, oldValue, value);
        await Player.SendPacket(
            new PacketSceneGroupRefreshScNotify(this, [res]));

        if (callEvent && GroupPropertyUpdated != null) await GroupPropertyUpdated(res);

        if (name == "SGP_PuzzleState" && group.ControlFloorSavedValue.Count > 0)
            
            foreach (var key in group.ControlFloorSavedValue)
                await UpdateFloorSavedValue(key, value);

        return res;
    }

    public async ValueTask UpdateFloorSavedValue(string name, int value)
    {
        if (!Player.SceneData!.FloorSavedData.TryGetValue(FloorId, out var floorSavedData))
        {
            floorSavedData = [];
            Player.SceneData.FloorSavedData[FloorId] = floorSavedData;
        }

        if (FloorInfo?.FloorSavedValue.All(x => x.Name != name) == true) return; 

        floorSavedData[name] = value;

        await Player.SendPacket(new PacketUpdateFloorSavedValueNotify(name, value, Player));
        await Player.MissionManager!.HandleFinishType(MissionFinishTypeEnum.FloorSavedValue);
    }

    public int GetFloorSavedValue(string name)
    {
        return Player.SceneData!.GetFloorSavedValue(FloorId, name);
    }

    public int GetGroupProperty(int groupId, string name)
    {
        if (!Player.SceneData!.GroupPropertyData.TryGetValue(FloorId, out var groupData))
        {
            groupData = [];
            Player.SceneData.GroupPropertyData[FloorId] = groupData;
        }

        if (!groupData.TryGetValue(groupId, out var propertyData))
        {
            propertyData = [];
            groupData[groupId] = propertyData;
        }

        var property = FloorInfo?.Groups.GetValueOrDefault(groupId)?.GroupPropertyMap.Values
            .FirstOrDefault(x => x.Name == name);

        if (property == null) return 0; 

        var oldValue = propertyData.GetValueOrDefault(name, property.DefaultValue);
        return oldValue;
    }

    public async ValueTask SyncLineup(bool notSendPacket = false)
    {
        var oldAvatarInfo = AvatarInfo.Values.ToList();
        AvatarInfo.Clear();
        var sendPacket = false;
        var addAvatar = new List<BaseGameEntity>();
        var removeAvatar = new List<BaseGameEntity>();
        var avatars = Player.LineupManager?.GetAvatarsFromCurTeam() ?? [];
        foreach (var sceneInfo in oldAvatarInfo)
            if (avatars.FindIndex(x => x.AvatarInfo.BaseAvatarId == sceneInfo.AvatarInfo.BaseAvatarId) !=
                -1) 
            {
                AvatarInfo.Add(sceneInfo.EntityId, sceneInfo);
            }
            else 
            {
                removeAvatar.Add(sceneInfo);
                sendPacket = true;
            }

        foreach (var avatar in avatars) 
        {
            if (AvatarInfo.Any(x => x.Value.AvatarInfo.BaseAvatarId == avatar.AvatarInfo.BaseAvatarId))
                continue; 
            var avatarInfo = new AvatarSceneInfo(avatar.AvatarInfo, avatar.AvatarType, Player)
            {
                
                EntityId = ++LastEntityId
            };

            AvatarInfo.Add(avatarInfo.EntityId, avatarInfo);
            addAvatar.Add(avatarInfo);
            sendPacket = true;
        }

        foreach (var avatar in removeAvatar)
        {
            Entities.Remove(avatar.EntityId);

            if (avatar is AvatarSceneInfo info) await info.OnDestroyInstance();
        }

        foreach (var avatar in addAvatar) Entities.Add(avatar.EntityId, avatar);

        if (AvatarInfo.Count == 0) return;
        var leaderAvatarId = Player.LineupManager?.GetCurLineup()?.LeaderAvatarId;
        var leaderAvatar = AvatarInfo.Values.FirstOrDefault(x => x.AvatarInfo.BaseAvatarId == leaderAvatarId);
        if (leaderAvatar == null)
        {
            leaderAvatar = AvatarInfo.Values.First();

            Player.LineupManager!.GetCurLineup()!.LeaderAvatarId = leaderAvatar.AvatarInfo.BaseAvatarId;
        }

        LeaderEntityId = leaderAvatar.EntityId;
        if (sendPacket && !notSendPacket)
            await Player.SendPacket(new PacketSceneGroupRefreshScNotify(Player, addAvatar, removeAvatar));
    }

    public void SyncGroupInfo()
    {
        EntityLoader?.SyncEntity();
    }

    public async ValueTask OnUseSkill(SceneCastSkillCsReq req)
    {
        foreach (var entity in Entities.Values.OfType<AvatarSceneInfo>())
        {
            if (!GameData.AvatarConfigData.TryGetValue(entity.AvatarInfo.AvatarId, out var excel)) continue;
            GameData.AdventureAbilityConfigListData.TryGetValue(excel.AdventurePlayerID, out var avatarAbility);
            if (avatarAbility == null) continue;
            foreach (var modifier in entity.Modifiers.ToArray())
            {
                
                if (!GameData.AdventureModifierData.TryGetValue(modifier, out var config)) continue;
                if (config.OnAfterLocalPlayerUseSkill.Count > 0)
                    await Player.TaskManager!.AbilityLevelTask.TriggerTasks(avatarAbility,
                        config.OnAfterLocalPlayerUseSkill, entity, [], req, modifier);
            }
        }
    }

    public async ValueTask OnChangeLeader(int curBaseAvatarId)
    {
        foreach (var entity in Entities.Values.OfType<AvatarSceneInfo>())
        {
            if (!GameData.AvatarConfigData.TryGetValue(entity.AvatarInfo.AvatarId, out var excel)) continue;
            GameData.AdventureAbilityConfigListData.TryGetValue(excel.AdventurePlayerID, out var avatarAbility);
            if (avatarAbility == null) continue;

            
            foreach (var modifier in entity.Modifiers.ToArray())
            {
                
                if (!GameData.AdventureModifierData.TryGetValue(modifier, out var config)) continue;
                if (config.OnTeamLeaderChange.Count > 0)
                    await Player.TaskManager!.AbilityLevelTask.TriggerTasks(avatarAbility,
                        config.OnTeamLeaderChange, entity, [], new SceneCastSkillCsReq
                        {
                            CastEntityId = (uint)entity.EntityId
                        }, modifier);
            }

            if (curBaseAvatarId == entity.AvatarInfo.BaseAvatarId) continue;

            
            foreach (var modifier in entity.Modifiers.ToArray())
            {
                
                if (!GameData.AdventureModifierData.TryGetValue(modifier, out var config)) continue;
                if (config.OnUnstage.Count > 0)
                    await Player.TaskManager!.AbilityLevelTask.TriggerTasks(avatarAbility,
                        config.OnUnstage, entity, [], new SceneCastSkillCsReq
                        {
                            CastEntityId = (uint)entity.EntityId
                        }, modifier);
            }
        }
    }

    public async ValueTask OnDestroy()
    {
        foreach (var value in AvatarInfo.Values) await value.OnDestroyInstance();
    }

    #endregion

    #region Components

    public async ValueTask InitializeComponents()
    {
        foreach (var component in Components) await component.Initialize();
    }

    public T? GetComponent<T>() where T : BaseSceneComponent
    {
        return Components.FirstOrDefault(x => x is T) as T;
    }

    #endregion

    #region Entity Management

    public async ValueTask AddEntity(BaseGameEntity entity)
    {
        await AddEntity(entity, IsLoaded);
    }

    public async ValueTask AddEntity(BaseGameEntity entity, bool sendPacket)
    {
        if (entity.EntityId != 0) return;
        entity.EntityId = ++LastEntityId;

        Entities.Add(entity.EntityId, entity);
        if (sendPacket) await Player.SendPacket(new PacketSceneGroupRefreshScNotify(Player, entity));
    }

    public async ValueTask<Retcode> AddSummonUnitEntity(EntitySummonUnit entity)
    {
        if (entity.EntityId != 0) return Retcode.RetServerInternalError;
        entity.EntityId = ++LastEntityId;
        
        if (!GameData.SummonUnitDataData.TryGetValue(entity.SummonUnitId, out var summonUnitExcel))
            return Retcode.RetMonsterConfigNotExist;

        
        var summonUnitKey = summonUnitExcel.UniqueGroup == SummonUnitUniqueGroupEnum.None ? summonUnitExcel.ID : 1;
        if (SummonUnit.TryGetValue(summonUnitKey, out var oldSummonUnit))
        {
            foreach (var e in Entities.Values.Where(x => x is EntityMonster))
            {
                var monster = e as EntityMonster;
                List<SceneBuff> buffList = [.. monster!.BuffList];
                foreach (var sceneBuff in buffList)
                    if (sceneBuff.SummonUnitEntityId == oldSummonUnit.EntityId)
                        
                        await monster.RemoveBuff(sceneBuff.BuffId);
            }

            await Player.SendPacket(new PacketSceneGroupRefreshScNotify(Player, null, oldSummonUnit));
            SummonUnit.Remove(summonUnitKey);
            Entities.Remove(oldSummonUnit.EntityId);
        }

        await Player.SendPacket(new PacketSceneGroupRefreshScNotify(Player, entity, null));
        Entities[entity.EntityId] = entity;
        SummonUnit[summonUnitKey] = entity;

        return Retcode.RetSucc;
    }

    public async ValueTask RemoveEntity(BaseGameEntity monster)
    {
        await RemoveEntity(monster, IsLoaded);
    }

    public async ValueTask RemoveEntity(BaseGameEntity monster, bool sendPacket)
    {
        Entities.Remove(monster.EntityId);

        if (sendPacket) await Player.SendPacket(new PacketSceneGroupRefreshScNotify(Player, null, monster));
    }

    public List<T> GetEntitiesInGroup<T>(int groupId)
    {
        List<T> entities = [];
        foreach (var entity in Entities)
            if (entity.Value.GroupId == groupId && entity.Value is T t)
                entities.Add(t);
        return entities;
    }

    #endregion

    #region SummonUnit

    public async ValueTask<Retcode> TriggerSummonUnit(int entityId, string triggerName, List<uint> targetIds)
    {
        var summonUnit = SummonUnit.Values.FirstOrDefault(x => x.EntityId == entityId);
        if (summonUnit == null) return Retcode.RetSceneEntityNotExist;

        
        var trigger = summonUnit.TriggerList.Find(x => x.TriggerName == triggerName);
        if (trigger == null) return Retcode.RetSceneUseSkillFail;

        await Player.SendPacket(
            new PacketRefreshTriggerByClientScNotify(triggerName, (uint)summonUnit.EntityId, targetIds));
        

        List<BaseGameEntity> targetEnter = [];
        List<BaseGameEntity> targetExit = [];
        foreach (var targetId in targetIds)
        {
            if (!Entities.TryGetValue((int)targetId, out var entity)) continue;
            EntityMonster? monster = null;
            EntityProp? prop = null;

            switch (entity)
            {
                case EntityMonster m:
                    monster = m;
                    break;
                case EntityProp p:
                    prop = p;
                    break;
            }

            if (monster != null)
            {
                if (!monster.IsAlive) continue;
                if (summonUnit.CaughtEntityIds.Contains(monster.EntityId)) continue;

                summonUnit.CaughtEntityIds.Add(monster.EntityId);
                targetEnter.Add(monster);
            }

            if (prop != null)
            {
                if (summonUnit.CaughtEntityIds.Contains(prop.EntityId)) continue;

                summonUnit.CaughtEntityIds.Add(prop.EntityId);
                targetEnter.Add(prop);
            }
        }

        foreach (var gameEntity in Entities.Values)
        {
            if (gameEntity is not EntityMonster monster) continue;

            if (summonUnit.CaughtEntityIds.Contains(monster.EntityId) && !targetEnter.Contains(monster))
            {
                summonUnit.CaughtEntityIds.Remove(monster.EntityId);
                targetExit.Add(monster);
            }
        }

        if (targetEnter.Count > 0)
        {
            
            var config = trigger.OnTriggerEnter;

            Player.TaskManager!.SummonUnitLevelTask.TriggerTasks(config, targetEnter, summonUnit);
        }

        if (targetExit.Count <= 0) return Retcode.RetSucc;
        {
            
            var config = trigger.OnTriggerExit;

            Player.TaskManager!.SummonUnitLevelTask.TriggerTasks(config, targetExit, summonUnit);
        }

        return Retcode.RetSucc;
    }

    public async ValueTask RemoveSummonUnitById(int summonUnitId)
    {
        var summonUnit = SummonUnit.FirstOrDefault(x => x.Value.SummonUnitId == summonUnitId);
        if (summonUnit.Value == null) return;

        await Player.SendPacket(new PacketSceneGroupRefreshScNotify(Player, null, summonUnit.Value));

        SummonUnit.Remove(summonUnit.Key);
        Entities.Remove(summonUnit.Value.EntityId);

        foreach (var entity in Entities.Values.Where(x => x is EntityMonster))
        {
            var monster = entity as EntityMonster;
            List<SceneBuff> buffList = [.. monster!.BuffList];
            foreach (var sceneBuff in buffList)
                if (sceneBuff.SummonUnitEntityId == summonUnit.Value.EntityId)
                    
                    await monster.RemoveBuff(sceneBuff.BuffId);
        }
    }

    public async ValueTask OnEnterStage()
    {
        List<BaseGameEntity> removeEntities = [];
        foreach (var unit in SummonUnit.ToArray())
        {
            if (!GameData.SummonUnitDataData.TryGetValue(unit.Value.SummonUnitId, out var excel)) continue;
            if (!excel.DestroyOnEnterBattle) continue;

            foreach (var entity in Entities.Values.Where(x => x is EntityMonster))
            {
                var monster = entity as EntityMonster;
                List<SceneBuff> buffList = [.. monster!.BuffList];
                foreach (var sceneBuff in buffList)
                    if (sceneBuff.SummonUnitEntityId == unit.Value.EntityId)
                        
                        await monster.RemoveBuff(sceneBuff.BuffId);
            }

            removeEntities.Add(unit.Value);
            SummonUnit.Remove(unit.Key);
            Entities.Remove(unit.Value.EntityId);
        }

        await Player.SendPacket(new PacketSceneGroupRefreshScNotify(Player, [], removeEntities));
    }

    public async ValueTask OnHeartBeat()
    {
        foreach (var gameEntity in Entities.Values.Clone().Where(x => x is EntityMonster).OfType<EntityMonster>())
        foreach (var sceneBuff in gameEntity.BuffList.Clone().Where(sceneBuff => sceneBuff.IsExpired()))
            await gameEntity.RemoveBuff(sceneBuff.BuffId);

        foreach (var gameEntity in AvatarInfo.Values.Clone())
        foreach (var sceneBuff in gameEntity.BuffList.Clone().Where(sceneBuff => sceneBuff.IsExpired()))
            await gameEntity.RemoveBuff(sceneBuff.BuffId);

        foreach (var unitValue in SummonUnit.Values)
        {
            if (unitValue.LifeTimeMs == -1) continue;
            var endTime = unitValue.CreateTimeMs + unitValue.LifeTimeMs;

            if (endTime < Extensions.GetUnixMs()) await RemoveSummonUnitById(unitValue.SummonUnitId);
        }
    }

    #endregion
}

public class AvatarSceneInfo : BaseGameEntity, IGameModifier
{
    public AvatarSceneInfo(BaseAvatarInfo avatarInfo, AvatarType avatarType, PlayerInstance player)
    {
        AvatarInfo = avatarInfo;
        AvatarType = avatarType;
        Player = player;

        
        if (!GameData.AvatarConfigData.TryGetValue(avatarInfo.AvatarId, out var excel)) return;
        var configInfo = GameData.CharacterConfigInfoData.GetValueOrDefault(excel.AdventurePlayerID);
        GameData.AdventureAbilityConfigListData.TryGetValue(excel.AdventurePlayerID, out var avatarAbility);
        if (configInfo == null || avatarAbility == null) return;

        foreach (var info in configInfo.SkillList.Where(x => x.UseType == SkillUseTypeEnum.Passive))
        {
            
            var abilityStr = info.EntryAbility;
            
            var ability = avatarAbility.AbilityList.FirstOrDefault(x => x.Name == abilityStr);
            if (ability == null) continue;
            _ = Player.TaskManager!.AbilityLevelTask.TriggerTasks(avatarAbility, ability.OnStart, this, [],
                new SceneCastSkillCsReq());
        }
    }

    public BaseAvatarInfo AvatarInfo { get; set; }
    public AvatarType AvatarType { get; set; }
    public PlayerInstance Player { get; set; }

    public override int EntityId { get; set; }

    public override int GroupId { get; set; } = 0;

    public List<string> Modifiers { get; set; } = [];

    public async ValueTask AddModifier(string modifierName)
    {
        if (Modifiers.Contains(modifierName)) return;
        Modifiers.Add(modifierName);

        GameData.AdventureModifierData.TryGetValue(modifierName, out var modifier);
        GameData.AdventureAbilityConfigListData.TryGetValue(AvatarInfo.AvatarId, out var avatarAbility);
        if (modifier == null || avatarAbility == null) return;

        await Player.TaskManager!.AbilityLevelTask.TriggerTasks(avatarAbility, modifier.OnCreate, this, [],
            new SceneCastSkillCsReq
            {
                TargetMotion = new MotionInfo
                {
                    Pos = Player.Data.Pos?.ToProto() ?? new Vector(),
                    Rot = Player.Data.Rot?.ToProto() ?? new Vector()
                }
            });

        await Player.TaskManager!.AbilityLevelTask.TriggerTasks(avatarAbility, modifier.OnStack, this, [],
            new SceneCastSkillCsReq
            {
                TargetMotion = new MotionInfo
                {
                    Pos = Player.Data.Pos?.ToProto() ?? new Vector(),
                    Rot = Player.Data.Rot?.ToProto() ?? new Vector()
                }
            });
    }

    public async ValueTask RemoveModifier(string modifierName)
    {
        if (!Modifiers.Contains(modifierName)) return;

        Modifiers.Remove(modifierName);
        GameData.AdventureModifierData.TryGetValue(modifierName, out var modifier);
        GameData.AdventureAbilityConfigListData.TryGetValue(AvatarInfo.AvatarId, out var avatarAbility);
        if (modifier == null || avatarAbility == null) return;

        await Player.TaskManager!.AbilityLevelTask.TriggerTasks(avatarAbility, modifier.OnDestroy, this, [],
            new SceneCastSkillCsReq());
    }

    public override async ValueTask AddBuff(SceneBuff buff)
    {
        if (!GameData.MazeBuffData.TryGetValue(buff.BuffId * 10 + buff.BuffLevel, out var buffExcel)) return;

        var oldBuff = BuffList.Find(x => x.BuffId == buff.BuffId);
        if (oldBuff != null)
        {
            if (oldBuff.IsExpired())
            {
                BuffList.Remove(oldBuff);
            }
            else
            {
                oldBuff.CreatedTime = Extensions.GetUnixMs();
                oldBuff.Duration = buff.Duration;

                await Player.SendPacket(new PacketSyncEntityBuffChangeListScNotify(this, oldBuff));
                await AddModifier(buffExcel.ModifierName);
                return;
            }
        }

        BuffList.Add(buff);
        await Player.SendPacket(new PacketSyncEntityBuffChangeListScNotify(this, buff));
        await AddModifier(buffExcel.ModifierName);
    }

    public override async ValueTask ApplyBuff(BattleInstance instance)
    {
        if (BuffList.Count == 0) return;
        foreach (var buff in BuffList.Where(buff => !buff.IsExpired())) instance.Buffs.Add(new MazeBuff(buff));

        await Player.SendPacket(new PacketSyncEntityBuffChangeListScNotify(this, BuffList));

        foreach (var sceneBuff in BuffList)
        {
            if (!GameData.MazeBuffData.TryGetValue(sceneBuff.BuffId * 10 + sceneBuff.BuffLevel, out var buffExcel))
                continue;

            await RemoveModifier(buffExcel.ModifierName);
        }

        BuffList.Clear();
    }

    public override SceneEntityInfo ToProto()
    {
        return new SceneEntityInfo
        {
            EntityId = (uint)EntityId,
            Motion = new MotionInfo
            {
                Pos = Player.Data.Pos?.ToProto() ?? new Vector(),
                Rot = Player.Data.Rot?.ToProto() ?? new Vector()
            },
            Actor = new SceneActorInfo
            {
                BaseAvatarId = (uint)AvatarInfo.BaseAvatarId,
                AvatarType = AvatarType
            }
        };
    }

    public async ValueTask RemoveBuff(int buffId)
    {
        if (!GameData.MazeBuffData.TryGetValue(buffId * 10 + 1, out var buffExcel)) return;

        var buff = BuffList.Find(x => x.BuffId == buffId);
        if (buff == null) return;

        BuffList.Remove(buff);
        await Player.SendPacket(new PacketSyncEntityBuffChangeListScNotify(this, [buff]));

        await RemoveModifier(buffExcel.ModifierName);
    }

    public async ValueTask ClearAllBuff()
    {
        if (BuffList.Count == 0) return;
        await Player.SendPacket(new PacketSyncEntityBuffChangeListScNotify(this, BuffList));

        foreach (var sceneBuff in BuffList)
        {
            if (!GameData.MazeBuffData.TryGetValue(sceneBuff.BuffId * 10 + sceneBuff.BuffLevel, out var buffExcel))
                continue;

            await RemoveModifier(buffExcel.ModifierName);
        }

        BuffList.Clear();
    }

    public async ValueTask OnDestroyInstance()
    {
        foreach (var modifier in Modifiers.ToArray()) await RemoveModifier(modifier);

        foreach (var monsterInfo in Player.SceneInstance!.Entities.Values.OfType<EntityMonster>().ToArray())
        foreach (var buff in monsterInfo.BuffList.Where(x => x.OwnerAvatarId == AvatarInfo.BaseAvatarId).ToArray())
            await monsterInfo.RemoveBuff(buff.BuffId);
    }
}

