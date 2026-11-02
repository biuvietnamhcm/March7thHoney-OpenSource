using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.Database;
using March7thHoney.Database.Avatar;
using March7thHoney.Database.Inventory;
using March7thHoney.Enums.Avatar;
using March7thHoney.GameServer.Game.Calyx;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.GameServer.Game.Lineup;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Game.Scene;
using March7thHoney.GameServer.Game.Scene.Entity;
using March7thHoney.GameServer.Server.Packet.Send.BattleCollege;
using March7thHoney.Proto;
using March7thHoney.Util;
using LineupInfo = March7thHoney.Database.Lineup.LineupInfo;

namespace March7thHoney.GameServer.Game.Battle;

public class BattleInstance(PlayerInstance player, LineupInfo lineup, List<StageConfigExcel> stages)
    : BasePlayerManager(player)
{
    public BattleInstance(PlayerInstance player, LineupInfo lineup, List<EntityMonster> monsters) : this(player, lineup,
        new List<StageConfigExcel>())
    {
        if (player.ActivityManager!.TrialActivityInstance != null &&
            player.ActivityManager!.TrialActivityInstance.Data.CurTrialStageId != 0)
        {
            var instance = player.ActivityManager!.TrialActivityInstance;
            GameData.StageConfigData.TryGetValue(instance.Data.CurTrialStageId, out var stage);
            if (stage != null) Stages.Add(stage);
            StageId = Stages[0].StageID;
        }
        else
        {
            var addedStageIds = new HashSet<int>();

            foreach (var monster in monsters)
            {
                if (!TryResolveStageFromEvent(monster.GetStageId(), player.Data.WorldLevel, out var stage) ||
                    stage == null)
                    continue;
                if (addedStageIds.Add(stage.StageID))
                    Stages.Add(stage);
            }

            
            if (Stages.Count == 0)
                foreach (var monster in monsters)
                {
                    if (!TryResolveStageFromEvent(monster.Info.EventID, player.Data.WorldLevel, out var stage) ||
                        stage == null)
                        continue;

                    if (addedStageIds.Add(stage.StageID))
                        Stages.Add(stage);
                }

            EntityMonsters = monsters;
            if (Stages.Count > 0)
                StageId = Stages[0].StageID;
        }
    }

    public int BattleId { get; set; } = ++player.NextBattleId;
    public int StaminaCost { get; set; }
    public int WorldLevel { get; set; }
    public int CocoonWave { get; set; }
    public int MappingInfoId { get; set; }
    public int RoundLimit { get; set; }
    public int StageId { get; set; } = stages.Count > 0 ? stages[0].StageID : 0; 
    public int EventId { get; set; }
    public int CustomLevel { get; set; }
    public BattleEndStatus BattleEndStatus { get; set; }

    public List<ItemData> MonsterDropItems { get; set; } = [];

    public List<StageConfigExcel> Stages { get; set; } = stages;
    public LineupInfo Lineup { get; set; } = lineup;
    public List<EntityMonster> EntityMonsters { get; set; } = [];
    public List<AvatarSceneInfo> AvatarInfo { get; set; } = [];
    public List<MazeBuff> Buffs { get; set; } = [];
    public BattleRogueMagicInfo? MagicInfo { get; set; }
    public Dictionary<int, BattleEventInstance> BattleEvents { get; set; } = [];
    public Dictionary<int, BattleTargetList> BattleTargets { get; set; } = [];
    public BattleCollegeConfigExcel? CollegeConfigExcel { get; set; }
    public PVEBattleResultCsReq? BattleResult { get; set; }
    public bool IsTournRogue { get; set; }
    public GridFightInstance? GridFightContext { get; set; }
    public CalyxOverrideContext? CalyxOverride { get; set; }

    /// <summary>
    /// Cached random seed so repeated battle-info queries stay stable for the same encounter.
    /// </summary>
    public uint? LogicRandomSeed { get; set; }

    public delegate ValueTask OnBattleEndDelegate(BattleInstance battle, PVEBattleResultCsReq req);

    public event OnBattleEndDelegate? OnBattleEnd;

    public async ValueTask TriggerOnBattleEnd()
    {
        if (OnBattleEnd != null)
            await OnBattleEnd(this, BattleResult!);
    }

    public ItemList GetDropItemList()
    {
        if (BattleEndStatus != BattleEndStatus.BattleEndWin) return new ItemList();
        var list = new ItemList();

        foreach (var item in MonsterDropItems) list.ItemList_.Add(item.ToProto());

        var t = System.Threading.Tasks.Task.Run(async () =>
        {
            foreach (var item in await Player.InventoryManager!.HandleMappingInfo(MappingInfoId, WorldLevel))
                list.ItemList_.Add(item.ToProto());
        });

        t.Wait();

        if (CollegeConfigExcel == null ||
            Player.BattleCollegeData?.FinishedCollegeIdList.Contains(CollegeConfigExcel.ID) != false)
            return list; 

        
        Player.BattleCollegeData.FinishedCollegeIdList.Add(CollegeConfigExcel.ID);
        var t2 = System.Threading.Tasks.Task.Run(async () =>
        {
            await Player.SendPacket(new PacketBattleCollegeDataChangeScNotify(Player));
            foreach (var item in await Player.InventoryManager!.HandleReward(CollegeConfigExcel.RewardID))
                list.ItemList_.Add(item.ToProto());
        });

        t2.Wait();

        return list;
    }

    public void AddBattleTarget(int key, int targetId, int progress, int totalProgress = 0)
    {
        if (!BattleTargets.TryGetValue(key, out var value))
        {
            value = new BattleTargetList();
            BattleTargets.Add(key, value);
        }

        var battleTarget = new BattleTarget
        {
            Id = (uint)targetId,
            Progress = (uint)progress,
            TotalProgress = (uint)totalProgress
        };
        value.BattleTargetList_.Add(battleTarget);
    }

    public List<AvatarLineupData> GetBattleAvatars()
    {
        var excel = GameData.StageConfigData[StageId];
        List<int> list = [.. excel.TrialAvatarList];

        
        if (CollegeConfigExcel is { TrialAvatarList.Count: > 0 }) list = [.. CollegeConfigExcel.TrialAvatarList];

        if (list.Count > 0)
        {
            List<int> tempList = [.. list];
            if (Player.Data.CurrentGender == Gender.Man)
                foreach (var avatar in tempList.Where(avatar =>
                             GameData.SpecialAvatarData.TryGetValue(avatar * 10 + 0, out var specialAvatarExcel) &&
                             specialAvatarExcel.AvatarID is 8002 or 8004 or 8006))
                    list.Remove(avatar);
            else
                foreach (var avatar in tempList.Where(avatar =>
                             GameData.SpecialAvatarData.TryGetValue(avatar * 10 + 0, out var specialAvatarExcel) &&
                             specialAvatarExcel.AvatarID is 8001 or 8003 or 8005))
                    list.Remove(avatar);
        }

        if (list.Count > 0) 
        {
            List<AvatarLineupData> avatars = [];
            foreach (var avatar in list)
            {
                var specialAvatar = Player.AvatarManager!.GetTrialAvatar(avatar);
                if (specialAvatar != null)
                {
                    specialAvatar.CheckLevel(Player.Data.WorldLevel);
                    avatars.Add(new AvatarLineupData(specialAvatar, AvatarType.AvatarTrialType));
                }
                else
                {
                    var avatarInfo = Player.AvatarManager!.GetFormalAvatar(avatar);
                    if (avatarInfo != null) avatars.Add(new AvatarLineupData(avatarInfo, AvatarType.AvatarFormalType));
                }
            }

            return avatars;
        }
        else
        {
            List<AvatarLineupData> avatars = [];
            foreach (var avatar in Lineup.BaseAvatars!) 
            {
                BaseAvatarInfo? avatarInstance = null;
                var avatarType = AvatarType.AvatarFormalType;

                if (avatar.AssistUid != 0)
                {
                    var player = DatabaseHelper.Instance!.GetInstance<AvatarData>(avatar.AssistUid);
                    if (player != null)
                    {
                        avatarInstance = player.FormalAvatars.Find(item => item.BaseAvatarId == avatar.BaseAvatarId);
                        avatarType = AvatarType.AvatarAssistType;
                    }
                }
                else if (avatar.SpecialAvatarId != 0)
                {
                    var specialAvatar = Player.AvatarManager!.GetTrialAvatar(avatar.SpecialAvatarId);
                    if (specialAvatar != null)
                    {
                        specialAvatar.CheckLevel(Player.Data.WorldLevel);
                        avatarInstance = specialAvatar;
                        avatarType = AvatarType.AvatarTrialType;
                    }
                }
                else
                {
                    avatarInstance = Player.AvatarManager!.GetFormalAvatar(avatar.BaseAvatarId);
                }

                if (avatarInstance == null) continue;

                avatars.Add(new AvatarLineupData(avatarInstance, avatarType));
            }

            return avatars;
        }
    }

    public SceneBattleInfo ToProto()
    {
        var proto = new SceneBattleInfo
        {
            BattleId = (uint)BattleId,
            WorldLevel = (uint)WorldLevel,
            RoundsLimit = (uint)RoundLimit,
            StageId = (uint)StageId,
            LogicRandomSeed = LogicRandomSeed ?? (uint)Random.Shared.Next()
        };

        if (MagicInfo != null) proto.BattleRogueMagicInfo = MagicInfo;

        foreach (var protoWave in Stages.Select(wave => wave.ToProto()))
        {
            if (CustomLevel > 0)
                foreach (var item in protoWave)
                    item.MonsterParam.Level = (uint)CustomLevel;

            proto.MonsterWaveList.AddRange(protoWave);
        }

        if (Player.BattleManager!.NextBattleMonsterIds.Count > 0)
        {
            var ids = Player.BattleManager!.NextBattleMonsterIds;
            
            for (var i = 0; i < (ids.Count - 1) / 5 + 1; i++)
            {
                var count = Math.Min(5, ids.Count - i * 5);
                var waveIds = ids.GetRange(i * 5, count);

                proto.MonsterWaveList.Add(new SceneMonsterWave
                {
                    BattleStageId = (uint)(Stages.FirstOrDefault()?.StageID ?? 0),
                    BattleWaveId = (uint)(proto.MonsterWaveList.Count + 1),
                    MonsterParam = new SceneMonsterWaveParam(),
                    MonsterList =
                    {
                        waveIds.Select(x => new SceneMonster
                        {
                            MonsterId = (uint)x
                        })
                    }
                });
            }
        }

        List<AvatarLineupData> avatars;
        if (GridFightContext != null)
        {
            avatars = GridFightBattleProtoBuilder.HandleProto(this, GridFightContext, proto);
        }
        else
        {
            avatars = GetBattleAvatars();
            foreach (var avatar in avatars)
                proto.BattleAvatarList.Add(avatar.AvatarInfo.ToBattleProto(
                    new PlayerDataCollection(Player.Data, Player.InventoryManager!.Data, Lineup), avatar.AvatarType));
        }

        System.Threading.Tasks.Task.Run(async () =>
        {
            foreach (var monster in EntityMonsters) await monster.ApplyBuff(this);

            foreach (var avatar in AvatarInfo)
                if (avatars.Select(x => x.AvatarInfo).FirstOrDefault(x =>
                        x.BaseAvatarId == avatar.AvatarInfo.BaseAvatarId) !=
                    null) 
                    await avatar.ApplyBuff(this);
        }).Wait();

        foreach (var buff in Buffs.Clone())
            if (Enum.IsDefined(typeof(DamageTypeEnum), buff.BuffID))
                Buffs.RemoveAll(x => x.BuffID == buff.BuffID && x.DynamicValues.Count == 0);

        foreach (var eventInstance in BattleEvents.Values) proto.BattleEvent.Add(eventInstance.ToProto());

        for (var i = 1; i <= 5; i++)
        {
            var battleTargetEntry = new BattleTargetList();

            if (BattleTargets.TryGetValue(i, out var battleTargetList))
                battleTargetEntry.BattleTargetList_.AddRange(battleTargetList.BattleTargetList_);

            proto.BattleTargetInfo.Add((uint)i, battleTargetEntry);
        }

        if (GridFightContext == null)
        {
            foreach (var buff in GameData.AvatarGlobalBuffConfigData.Values)
                if (Player.AvatarManager!.GetFormalAvatar(buff.AvatarID) != null)
                    Buffs.Add(new MazeBuff(buff.MazeBuffID, 1, -1)
                    {
                        WaveFlag = -1
                    });
        }

        foreach (var buff in Buffs)
        {
            if (buff.WaveFlag != null) continue;
            var buffs = Buffs.FindAll(x => x.BuffID == buff.BuffID);
            if (buffs.Count < 2) continue;
            var count = 0;
            foreach (var mazeBuff in buffs)
            {
                mazeBuff.WaveFlag = (int)Math.Pow(2, count);
                count++;
            }
        }

        CalyxOverride?.Apply(this, proto);

        proto.BuffList.AddRange(Buffs.Select(buff => buff.ToProto(this)));
        return proto;
    }

    private static bool TryResolveStageFromEvent(int eventId, int worldLevel, out StageConfigExcel? stage)
    {
        stage = null;
        if (eventId <= 0) return false;

        if (GameData.PlaneEventData.TryGetValue(eventId * 10 + worldLevel, out var exactPlaneEvent) &&
            GameData.StageConfigData.TryGetValue(exactPlaneEvent.StageID, out stage))
            return true;

        if (GameData.PlaneEventData.TryGetValue(eventId, out var directPlaneEvent) &&
            GameData.StageConfigData.TryGetValue(directPlaneEvent.StageID, out stage))
            return true;

        if (GameData.StageConfigData.TryGetValue(eventId, out stage))
            return true;

        var fallbackPlaneEvent = GameData.PlaneEventData.Values.FirstOrDefault(x => x.EventID == eventId);
        if (fallbackPlaneEvent != null &&
            GameData.StageConfigData.TryGetValue(fallbackPlaneEvent.StageID, out stage))
            return true;

        return false;
    }
}

