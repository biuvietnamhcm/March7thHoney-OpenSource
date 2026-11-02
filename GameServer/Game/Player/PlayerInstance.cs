using March7thHoney.GameServer.Game.DailyActive;
using March7thHoney.Data;
using March7thHoney.Database;
using March7thHoney.Database.Avatar;
using March7thHoney.Database.Calyx;
using March7thHoney.Database.Friend;
using March7thHoney.Database.Player;
using March7thHoney.Database.Scene;
using March7thHoney.Database.Tutorial;
using March7thHoney.Enums.Avatar;
using March7thHoney.GameServer.Game.Activity;
using March7thHoney.GameServer.Game.Avatar;
using March7thHoney.GameServer.Game.Battle;
using March7thHoney.GameServer.Game.Calyx;
using March7thHoney.GameServer.Game.Challenge;
using March7thHoney.GameServer.Game.ChallengePeak;
using March7thHoney.GameServer.Game.Expedition;
using March7thHoney.GameServer.Game.Friend;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Game.Gacha;
using March7thHoney.GameServer.Game.Inventory;
using March7thHoney.GameServer.Game.Lineup;
using March7thHoney.GameServer.Game.Mail;
using March7thHoney.GameServer.Game.Message;
using March7thHoney.GameServer.Game.Mission;
using March7thHoney.GameServer.Game.Player.Components;
using March7thHoney.GameServer.Game.Quest;
using March7thHoney.GameServer.Game.Raid;
using March7thHoney.GameServer.Game.Scene;
using March7thHoney.GameServer.Game.Shop;
using March7thHoney.GameServer.Game.Sync.Player;
using March7thHoney.GameServer.Game.Task;
using March7thHoney.GameServer.Game.TrainCakeCatch;
using March7thHoney.GameServer.Game.TrainParty;
using March7thHoney.GameServer.Server;
using March7thHoney.GameServer.Server.Packet.Send.Player;
using March7thHoney.GameServer.Server.Packet.Send.PlayerSync;
using March7thHoney.Kcp;
using March7thHoney.Proto;
using March7thHoney.Util;
using static March7thHoney.GameServer.Plugin.Event.PluginEvent;
using OfferingManager = March7thHoney.GameServer.Game.Inventory.OfferingManager;

namespace March7thHoney.GameServer.Game.Player;

public partial class PlayerInstance(PlayerData data)
{
    #region Managers

    #region Basic Managers

    public AvatarManager? AvatarManager { get; private set; }
    public LineupManager? LineupManager { get; private set; }
    public InventoryManager? InventoryManager { get; private set; }
    public BattleManager? BattleManager { get; private set; }
    public SceneSkillManager? SceneSkillManager { get; private set; }
    public CalyxOverrideManager? CalyxOverrideManager { get; private set; }
    public BattleInstance? BattleInstance { get; set; }

    #endregion

    #region Shopping Managers

    public GachaManager? GachaManager { get; private set; }
    public ShopService? ShopService { get; private set; }
    public OfferingManager? OfferingManager { get; private set; }

    #endregion

    #region Quest & Mission Managers

    public MissionManager? MissionManager { get; private set; }
    public DailyActivityManager? DailyActivityManager { get; private set; }
    public QuestManager? QuestManager { get; private set; }
    public RaidManager? RaidManager { get; private set; }
    public StoryLineManager? StoryLineManager { get; private set; }
    public MessageManager? MessageManager { get; private set; }
    public TaskManager? TaskManager { get; private set; }

    #endregion

    #region Activity Managers

    public ActivityManager? ActivityManager { get; private set; }
    public TrainPartyManager? TrainPartyManager { get; private set; }
    public TrainCakeCatchManager? TrainCakeCatchManager { get; private set; }

    #endregion

    #region Others

    public MailManager? MailManager { get; private set; }
    public FriendManager? FriendManager { get; private set; }
    public ChallengeManager? ChallengeManager { get; private set; }
    public ChallengePeakManager? ChallengePeakManager { get; private set; }
    public GridFightManager? GridFightManager { get; private set; }
    public ExpeditionManager? ExpeditionManager { get; private set; }

    #endregion

    #endregion

    #region Datas

    public PlayerData Data { get; set; } = data;
    public PlayerUnlockData? PlayerUnlockData { get; private set; }
    public FriendRecordData? FriendRecordData { get; private set; }
    public SceneData? SceneData { get; private set; }
    public HeartDialData? HeartDialData { get; private set; }
    public TutorialData? TutorialData { get; private set; }
    public TutorialGuideData? TutorialGuideData { get; private set; }
    public BattleCollegeData? BattleCollegeData { get; private set; }
    public CalyxOverrideData? CalyxOverrideData { get; private set; }
    public ServerPrefsData? ServerPrefsData { get; private set; }
    public SceneInstance? SceneInstance { get; private set; }
    public List<BasePlayerComponent> Components { get; } = [];
    public int Uid { get; set; }
    public Connection? Connection { get; set; }
    public bool Initialized { get; set; }
    public bool IsNewPlayer { get; set; }
    public int NextBattleId { get; set; } = 0;
    public int ChargerNum { get; set; } = 0;
    public bool LoginAnnounceSent { get; set; }

    public uint ActiveFarmElementEntityId { get; set; }
    public Position? FarmElementReturnPos { get; set; }
    public Position? FarmElementReturnRot { get; set; }

    #endregion

    #region Initializers

    public PlayerInstance(int uid) : this(new PlayerData { Uid = uid })
    {
        
        IsNewPlayer = true;
        Data.WelcomeAnnouncePending = true;
        Data.NextStaminaRecover = Extensions.GetUnixSec() + GameConstants.STAMINA_RESERVE_RECOVERY_TIME;
        Data.Level = ConfigManager.Config.ServerOption.StartTrailblazerLevel;
        Data.Name = ConfigManager.Config.ServerOption.DefaultNickname;
        OnLevelChange();

        DatabaseHelper.SaveInstance(Data);


        var t = System.Threading.Tasks.Task.Run(async () =>
        {
            await InitialPlayerManager();

            
            
            
            
            await AddAvatar(8001);
            await AddAvatar(8002);
            await AddAvatar(1001);
            if (ConfigManager.Config.ServerOption.EnableMission)
            {
                await LineupManager!.AddSpecialAvatarToCurTeam(10010050);
            }
            else
            {
                await LineupManager!.AddAvatarToCurTeam(8001);
                Data.CurrentGender = Gender.Woman;
                Data.CurBasicType = 8002;
                
                
                var hero = AvatarManager!.GetHero();
                if (hero != null) hero.AvatarId = 8002;
            }
        });
        t.Wait();

        Initialized = true;
    }

    private async ValueTask InitialPlayerManager()
    {
        Uid = Data.Uid;
        ActivityManager = new ActivityManager(this);
        AvatarManager = new AvatarManager(this)
        {
            AvatarData =
            {
                DatabaseVersion = GameConstants.AvatarDbVersion
            }
        };

        LineupManager = new LineupManager(this);
        InventoryManager = new InventoryManager(this);
        BattleManager = new BattleManager(this);
        SceneSkillManager = new SceneSkillManager(this);
        CalyxOverrideManager = new CalyxOverrideManager(this);
        MissionManager = new MissionManager(this);
        GachaManager = new GachaManager(this);
        MessageManager = new MessageManager(this);
        MailManager = new MailManager(this);
        FriendManager = new FriendManager(this);
        ShopService = new ShopService(this);
        ChallengeManager = new ChallengeManager(this);
        ChallengePeakManager = new ChallengePeakManager(this);
        GridFightManager = new GridFightManager(this);
        ExpeditionManager = new ExpeditionManager(this);
        TaskManager = new TaskManager(this);
        RaidManager = new RaidManager(this);
        StoryLineManager = new StoryLineManager(this);
        QuestManager = new QuestManager(this);
        DailyActivityManager = new DailyActivityManager(this);
        TrainPartyManager = new TrainPartyManager(this);
        TrainCakeCatchManager = new TrainCakeCatchManager(this);
        OfferingManager = new OfferingManager(this);

        PlayerUnlockData = InitializeDatabase<PlayerUnlockData>();
        SceneData = InitializeDatabase<SceneData>();
        HeartDialData = InitializeDatabase<HeartDialData>();
        TutorialData = InitializeDatabase<TutorialData>();
        TutorialGuideData = InitializeDatabase<TutorialGuideData>();
        ServerPrefsData = InitializeDatabase<ServerPrefsData>();
        BattleCollegeData = InitializeDatabase<BattleCollegeData>();
        CalyxOverrideData = InitializeDatabase<CalyxOverrideData>();
        CalyxOverrideManager!.AttachData(CalyxOverrideData);
        FriendRecordData = InitializeDatabase<FriendRecordData>();

        Components.Add(new SwitchHandComponent(this));

        if ((int)(ServerPrefsData.Version * 1000) != GameConstants.GameVersionInt)
        {
            ServerPrefsData.ServerPrefsDict.Clear();
            ServerPrefsData.Version = GameConstants.GameVersionInt / 1000d;
        }

        Data.LastActiveTime = Extensions.GetUnixSec();

        foreach (var avatar in AvatarManager?.AvatarData.FormalAvatars ?? [])
        foreach (var path in avatar.PathInfos.Values)
        foreach (var skill in path.GetSkillTree())
        {
            GameData.AvatarSkillTreeConfigData.TryGetValue(skill.Key * 100 + 1, out var config);
            if (config == null) continue;
            path.GetSkillTree()[skill.Key] = Math.Min(skill.Value, config.MaxLevel); 
        }

        foreach (var info in LineupManager!.GetAllLineup().SelectMany(lineupInfo => lineupInfo.BaseAvatars ?? []))
        {
            if (info.SpecialAvatarId > 0 &&
                GameData.SpecialAvatarData.TryGetValue(info.SpecialAvatarId, out var excel))
            {
                info.SpecialAvatarId = excel.SpecialAvatarID;
                AvatarManager!.GetTrialAvatar(excel.SpecialAvatarID)?.CheckLevel(Data.WorldLevel);
            }

            if (info.SpecialAvatarId > 0 &&
                GameData.SpecialAvatarData.TryGetValue(info.SpecialAvatarId * 10 + 0, out var e))
                AvatarManager!.GetTrialAvatar(e.SpecialAvatarID)?.CheckLevel(Data.WorldLevel);
        }

        if (ConfigManager.Config.ServerOption.EnableMission) await MissionManager!.AcceptMainMissionByCondition();

        foreach (var friendDevelopmentInfoPb in FriendRecordData.DevelopmentInfos.ToArray())
            if (Extensions.GetUnixSec() - friendDevelopmentInfoPb.Time >=
                TimeSpan.TicksPerDay * 7 / TimeSpan.TicksPerSecond)
                FriendRecordData.DevelopmentInfos.Remove(friendDevelopmentInfoPb);

        await QuestManager!.AcceptQuestByCondition();
        await DailyActivityManager!.RefreshAsync();
    }

    public T InitializeDatabase<T>() where T : BaseDatabaseDataHelper, new()
    {
        var instance = DatabaseHelper.Instance?.GetInstanceOrCreateNew<T>(Uid);
        return instance!;
    }

    #endregion

    #region Network

    public async ValueTask OnGetToken()
    {
        if (!Initialized) await InitialPlayerManager();
    }

    public async ValueTask OnLogin()
    {
        await SendPacket(new PacketStaminaInfoScNotify(this));

        ChallengeManager?.ResurrectInstance();
        if (StoryLineManager != null)
            await StoryLineManager.OnLogin();

        if (RaidManager != null)
            await RaidManager.OnLogin();

        if (LineupManager!.GetCurLineup() != null) 
        {
            if (LineupManager!.GetCurLineup()!.IsExtraLineup() &&
                RaidManager!.RaidData.CurRaidId == 0 && StoryLineManager!.StoryLineData.CurStoryLineId == 0 &&
                ChallengeManager!.ChallengeInstance == null) 
            {
                LineupManager!.SetExtraLineup(ExtraLineupType.LineupNone, []);
                if (LineupManager!.GetCurLineup()!.IsExtraLineup()) await LineupManager!.SetCurLineup(0);
            }

            foreach (var lineup in LineupManager.LineupData.Lineups)
            {
                if (lineup.Value.BaseAvatars!.Count >= 5)
                    lineup.Value.BaseAvatars = lineup.Value.BaseAvatars.GetRange(0, 4);

                foreach (var avatar in lineup.Value.BaseAvatars!)
                    if (avatar.BaseAvatarId > 10000)
                    {
                        GameData.SpecialAvatarData.TryGetValue(avatar.BaseAvatarId, out var special);
                        if (special != null)
                        {
                            avatar.SpecialAvatarId = special.SpecialAvatarID;
                            avatar.BaseAvatarId = special.AvatarID;
                        }
                        else
                        {
                            GameData.SpecialAvatarData.TryGetValue(avatar.BaseAvatarId * 10 + Data.WorldLevel,
                                out special);
                            if (special != null)
                            {
                                avatar.SpecialAvatarId = special.SpecialAvatarID;
                                avatar.BaseAvatarId = special.AvatarID;
                            }
                        }
                    }
            }

            foreach (var avatar in LineupManager.GetCurLineup()!.BaseAvatars!)
            {
                var avatarData = AvatarManager!.GetFormalAvatar(avatar.BaseAvatarId);
                if (avatarData is { CurrentHp: <= 0 })
                    
                    avatarData.CurrentHp = 2000;
            }
        }

        await LoadScene(Data.PlaneId, Data.FloorId, Data.EntryId, Data.Pos!, Data.Rot!, false);
        if (SceneInstance == null) await EnterScene(2000101, 0, false);

        InvokeOnPlayerLogin(this);
    }

    public async ValueTask TrySendWelcomeAnnounce()
    {
        if (LoginAnnounceSent || !Data.WelcomeAnnouncePending) return;

        var announceCfg = ConfigManager.Config.ServerOption.ServerAnnounce;
        if (announceCfg.EnableAnnounce && !string.IsNullOrWhiteSpace(announceCfg.AnnounceContent))
        {
            await SendPacket(new PacketServerAnnounceNotify(
                announceCfg.AnnounceContent,
                announceCfg.GetDurationSeconds(),
                announceCfg.GetBannerFrequencySeconds()));
            LoginAnnounceSent = true;
        }

        Data.WelcomeAnnouncePending = false;
        DatabaseHelper.ToSaveUidList.SafeAdd(Uid);
    }

    public void OnLogoutAsync()
    {
        InvokeOnPlayerLogout(this);
    }

    public async ValueTask SendPacket(BasePacket packet)
    {
        if (Connection?.IsOnline == true) await Connection.SendPacket(packet);
    }

    #endregion

    #region Actions

    public async ValueTask SetPlayerHeadFrameId(uint headFrameId, long expireTime)
    {
        Data.HeadFrame = new PlayerHeadFrameInfo
        {
            HeadFrameId = headFrameId,
            HeadFrameExpireTime = expireTime
        };

        await SendPacket(new PacketPlayerSyncScNotify([new PlayerBoardSync(this)]));
    }

    public async ValueTask ChangeAvatarPathType(int baseAvatarId, MultiPathAvatarTypeEnum type)
    {
        FormalAvatarInfo avatar;
        if (baseAvatarId == 8001)
        {
            var id = (int)((int)type + Data.CurrentGender - 1);
            if (Data.CurBasicType == id) return;
            Data.CurBasicType = id;
            avatar = AvatarManager!.GetHero()!;
            
            avatar.AvatarId = id;
            avatar.ValidateHero(Data.CurrentGender);
            avatar.SetCurSp(0, LineupManager!.GetCurLineup()!.IsExtraLineup());
            
            avatar.CheckPathSkillTree();
            await SendPacket(new PacketAvatarPathChangedNotify(8001, (MultiPathAvatarType)id));
            await SendPacket(new PacketPlayerSyncScNotify(AvatarManager!.GetHero()!));
        }
        else
        {
            avatar = AvatarManager!.GetFormalAvatar(baseAvatarId)!;
            avatar.AvatarId = (int)type;
            avatar.SetCurSp(0, LineupManager!.GetCurLineup()!.IsExtraLineup());
            
            avatar.CheckPathSkillTree();
            await SendPacket(new PacketAvatarPathChangedNotify((uint)baseAvatarId, (MultiPathAvatarType)type));
            await SendPacket(new PacketPlayerSyncScNotify(avatar));
        }

        
        if (SceneInstance != null)
        {
            var avatarScene =
                SceneInstance.AvatarInfo.Values.FirstOrDefault(x => x.AvatarInfo.BaseAvatarId == baseAvatarId);
            if (avatarScene == null) return;

            await avatarScene.ClearAllBuff();
        }
    }

    public async ValueTask ChangeAvatarSkin(int avatarId, int skinId)
    {
        PlayerUnlockData!.Skins.TryGetValue(avatarId, out var skins);
        if (skins != null && (skins.Contains(skinId) || skinId == 0))
        {
            var avatar = AvatarManager!.GetFormalAvatar(avatarId)!;
            avatar.GetPathInfo(avatarId)!.Skin = skinId;
            await SendPacket(new PacketPlayerSyncScNotify(avatar));
        }
    }

    public async ValueTask<FormalAvatarInfo> MarkAvatar(int avatarId, bool isMarked, bool sendPacket = true)
    {
        var avatar = AvatarManager!.GetFormalAvatar(avatarId)!;
        avatar.IsMarked = isMarked;
        if (sendPacket) await SendPacket(new PacketPlayerSyncScNotify(avatar));
        return avatar;
    }

    public async ValueTask AddAvatar(int avatarId, bool sync = true, bool notify = true)
    {
        await AvatarManager!.AddAvatar(avatarId, sync, notify);
    }

    public async ValueTask SpendStamina(int staminaCost)
    {
        if (staminaCost <= 0) return;
        var spent = Math.Clamp(Data.Stamina, 0, staminaCost);
        Data.Stamina -= spent;
        if (DailyActivityManager != null) await DailyActivityManager.RecordProgress(2100139, spent);
        await SendPacket(new PacketStaminaInfoScNotify(this));
    }

    public void OnAddExp()
    {
        GameData.PlayerLevelConfigData.TryGetValue(Data.Level, out var config);
        GameData.PlayerLevelConfigData.TryGetValue(Data.Level + 1, out var config2);
        if (config == null || config2 == null) return;
        var nextExp = config2.PlayerExp - config.PlayerExp;

        while (Data.Exp >= nextExp)
        {
            Data.Exp -= nextExp;
            Data.Level++;
            GameData.PlayerLevelConfigData.TryGetValue(Data.Level, out config);
            GameData.PlayerLevelConfigData.TryGetValue(Data.Level + 1, out config2);
            if (config == null || config2 == null) break;
            nextExp = config2.PlayerExp - config.PlayerExp;
        }

        OnLevelChange();
    }

    public void OnLevelChange()
    {
        if (!ConfigManager.Config.ServerOption.AutoUpgradeWorldLevel) return;
        var worldLevel = 0;
        foreach (var level in GameConstants.UpgradeWorldLevel)
            if (level <= Data.Level)
                worldLevel++;

        if (Data.WorldLevel != worldLevel) Data.WorldLevel = worldLevel;
    }

    public async ValueTask OnStaminaRecover()
    {
        var sendPacket = false;
        while (Data.NextStaminaRecover <= Extensions.GetUnixSec())
        {
            if (Data.Stamina >= GameConstants.MAX_STAMINA)
            {
                if (Data.StaminaReserve >= GameConstants.MAX_STAMINA_RESERVE) 
                    break;
                Data.StaminaReserve = Math.Min(Data.StaminaReserve + 1, GameConstants.MAX_STAMINA_RESERVE);
            }
            else
            {
                Data.Stamina++;
            }

            Data.NextStaminaRecover = Data.NextStaminaRecover + (Data.Stamina >= GameConstants.MAX_STAMINA
                ? GameConstants.STAMINA_RESERVE_RECOVERY_TIME
                : GameConstants.STAMINA_RECOVERY_TIME);
            sendPacket = true;
        }

        if (sendPacket) await SendPacket(new PacketStaminaInfoScNotify(this));
    }

    public async ValueTask OnHeartBeat()
    {
        if (DailyActivityManager != null) await DailyActivityManager.RefreshAsync(true);
        await OnStaminaRecover();

        InvokeOnPlayerHeartBeat(this);
        if (MissionManager != null)
            await MissionManager.HandleAllFinishType();

        if (SceneInstance != null)
            await SceneInstance.OnHeartBeat();

        if (OfferingManager != null)
            await OfferingManager.UpdateOfferingData();

        DatabaseHelper.ToSaveUidList.Add(Uid);
    }

    public T GetComponent<T>() where T : BasePlayerComponent
    {
        return Components.OfType<T>().FirstOrDefault() ??
               throw new InvalidOperationException($"Component {typeof(T)} not found.");
    }

    #endregion

    #region Serialization

    public PlayerBasicInfo ToProto()
    {
        return Data.ToProto();
    }

    public PlayerSimpleInfo ToSimpleProto()
    {
        return Data.ToSimpleProto(FriendOnlineStatus.Online);
    }

    #endregion
}
