using March7thHoney.Data;
using March7thHoney.Data.Config;
using March7thHoney.Data.Config.Scene;
using March7thHoney.Data.Config.Task;
using March7thHoney.Data.Excel;
using March7thHoney.Database;
using March7thHoney.Database.Player;
using March7thHoney.Database.Scene;
using March7thHoney.Enums;
using March7thHoney.Enums.Mission;
using March7thHoney.Enums.Scene;
using March7thHoney.GameServer.Game.Avatar;
using March7thHoney.GameServer.Game.Battle;
using March7thHoney.GameServer.Game.Inventory;
using March7thHoney.GameServer.Game.Lineup;
using March7thHoney.GameServer.Game.Mission;
using March7thHoney.GameServer.Game.Mission.FinishType;
using March7thHoney.GameServer.Game.Mission.FinishType.Handler;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Game.Quest;
using March7thHoney.GameServer.Game.Raid;
using March7thHoney.GameServer.Game.Scene;
using March7thHoney.GameServer.Game.Scene.Entity;
using March7thHoney.GameServer.Game.Task;
using March7thHoney.GameServer.Server.Packet.Send.Player;
using March7thHoney.Proto;
using March7thHoney.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using MissionInfo = March7thHoney.Data.Config.MissionInfo;

var resourcePath = Path.GetFullPath(args.Length > 0 ? args[0] : "Resources");
ConfigManager.Config.ServerOption.EnableMission = true;
ConfigManager.Config.Path.ResourcePath = resourcePath;
var dbPath = Path.Combine(Path.GetTempPath(), $"march7th-story-{Guid.NewGuid():N}.sqlite");
using var database = new SqlSugarScope(new ConnectionConfig
{
    ConnectionString = $"Data Source={dbPath};", DbType = DbType.Sqlite, IsAutoCloseConnection = true,
    ConfigureExternalServices = new() { SerializeService = new CustomSerializeService() }
});
DatabaseHelper.sqlSugarScope = database;
_ = new DatabaseHelper();
foreach (var type in typeof(PlayerData).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BaseDatabaseDataHelper))))
    database.CodeFirst.InitTables(type);

var tests = new (string, Func<Task>)[]
{
    ("Basic info cannot silently complete character selection", BasicInfo),
    ("Main mission waits for asynchronous child initialization", AwaitedAcceptance),
    ("Missing final objectives cannot auto-complete a main mission", EmptyFinishList),
    ("Trial-avatar objectives require actual lineup changes", TrialConditions),
    ("PropStateExecute awaits its target resolver", PropTarget),
    ("Opening story follows real resource dependencies and survives SQLite reload", OpeningStory)
};
var failed = 0;
foreach (var (name, run) in tests)
{
    GameData.MainMissionData.Clear(); GameData.SubMissionInfoData.Clear(); MissionManager.FinishTypeHandlers.Clear();
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {name}: {ex}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} scenarios passed. Isolated SQLite: {dbPath}");
return failed == 0 ? 0 : 1;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static void Set(PlayerInstance player, string property, object value) =>
    typeof(PlayerInstance).GetProperty(property)!.SetValue(player, value);

static PlayerInstance Fixture(int uid)
{
    var data = DatabaseHelper.Instance!.GetInstanceOrCreateNew<PlayerData>(uid);
    var p = new PlayerInstance(data) { Uid = uid };
    Set(p, nameof(p.SceneData), DatabaseHelper.Instance.GetInstanceOrCreateNew<SceneData>(uid));
    Set(p, nameof(p.AvatarManager), new AvatarManager(p));
    Set(p, nameof(p.LineupManager), new LineupManager(p));
    Set(p, nameof(p.InventoryManager), new InventoryManager(p));
    Set(p, nameof(p.QuestManager), new QuestManager(p));
    Set(p, nameof(p.RaidManager), new RaidManager(p));
    Set(p, nameof(p.MissionManager), new MissionManager(p));
    Set(p, nameof(p.TaskManager), new TaskManager(p));
    p.LineupManager!.LineupData.Lineups.TryAdd(0, new()
    { Name = "", BaseAvatars = [], LineupData = p.LineupManager.LineupData, AvatarData = p.AvatarManager!.AvatarData });
    return p;
}

static MainMissionExcel Mission(int id, params SubMissionInfo[] children)
{
    var main = new MainMissionExcel { MainMissionID = id, TakeOperation = OperationEnum.And };
    main.SetMissionInfo(new MissionInfo
    {
        MainMissionID = id, StartSubMissionList = [children[0].ID],
        FinishSubMissionList = [children[^1].ID], SubMissionList = [.. children]
    });
    GameData.MainMissionData[id] = main;
    return main;
}

static Task BasicInfo()
{
    var p = Fixture(2001);
    p.AvatarManager!.AvatarData.FormalAvatars.Add(new(8001, 8001, true));
    var packet = new PacketGetBasicInfoScRsp(p);
    Check(!GetBasicInfoScRsp.Parser.ParseFrom(packet.Data).IsGenderSet && !p.Data.IsGenderSet,
        "Owning the hero must not skip selection");
    p.Data.IsGenderSet = true;
    Check(GetBasicInfoScRsp.Parser.ParseFrom(new PacketGetBasicInfoScRsp(p).Data).IsGenderSet,
        "Completed selection not preserved");
    return Task.CompletedTask;
}

static async Task AwaitedAcceptance()
{
    var p = Fixture(2002);
    var gate = new GateHandler();
    MissionManager.FinishTypeHandlers[MissionFinishTypeEnum.PlayerLevel] = gate;
    Mission(900, new SubMissionInfo() { ID = 90001, FinishType = MissionFinishTypeEnum.PlayerLevel });
    var accept = p.MissionManager!.AcceptMainMission(900, false).AsTask();
    try
    {
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(!accept.IsCompleted, "Parent returned before initialization completed");
    }
    finally { gate.Release.TrySetResult(); }
    var sync = await accept;
    Check(sync.Count == 1 && sync[0]!.MissionList.Count == 1, "Missing child sync after await");
}

static async Task EmptyFinishList()
{
    var p = Fixture(2003);
    var main = Mission(901, new SubMissionInfo() { ID = 90101, FinishType = MissionFinishTypeEnum.Talk, ParamStr1 = "test" });
    main.MissionInfo.FinishSubMissionList.Clear();
    await p.MissionManager!.AcceptMainMission(901);
    await p.MissionManager.HandleTalkStr("test");
    Check(p.MissionManager.GetMainMissionStatus(901) == MissionPhaseEnum.Accept, "Incomplete graph auto-finished");
}

static async Task TrialConditions()
{
    var p = Fixture(2004);
    Mission(902,
        new() { ID = 90201, FinishType = MissionFinishTypeEnum.GetTrialAvatar, ParamInt1 = 1001006 },
        new() { ID = 90202, FinishType = MissionFinishTypeEnum.DelTrialAvatar, ParamInt1 = 1001006,
            TakeType = SubMissionTakeTypeEnum.AnySequence, TakeParamIntList = [90201] });
    MissionManager.FinishTypeHandlers[MissionFinishTypeEnum.GetTrialAvatar] = new MissionHandlerGetTrialAvatar();
    MissionManager.FinishTypeHandlers[MissionFinishTypeEnum.DelTrialAvatar] = new MissionHandlerDelTrialAvatar();
    await p.MissionManager!.AcceptMainMission(902);
    Check(p.MissionManager.GetSubMissionStatus(90201) == MissionPhaseEnum.Accept, "Missing trial auto-finished");
    p.LineupManager!.GetCurLineup()!.BaseAvatars!.Add(new() { BaseAvatarId = 7006, SpecialAvatarId = 1001006 });
    await p.MissionManager.HandleFinishType(MissionFinishTypeEnum.GetTrialAvatar);
    Check(p.MissionManager.GetSubMissionStatus(90202) == MissionPhaseEnum.Accept, "Trial removed too early");
    p.LineupManager.GetCurLineup()!.BaseAvatars!.Clear();
    await p.MissionManager.HandleFinishType(MissionFinishTypeEnum.DelTrialAvatar);
    Check(p.MissionManager.GetMainMissionStatus(902) == MissionPhaseEnum.Finish, "Trial removal did not advance");
}

static async Task PropTarget()
{
    var p = Fixture(2005);
    var scene = Scene(p, 99999);
    var prop = new EntityProp(scene, new() { PropType = PropTypeEnum.PROP_ORDINARY },
        new() { Id = 5 }, new() { ID = 300002 });
    await scene.AddEntity(prop, false);
    var task = (PropStateExecute)TaskConfigInfo.LoadFromJsonObject(JObject.Parse("""
        {"Type":"RPG.GameCore.PropStateExecute", "State":"Open", "TargetType":{
          "Type":"RPG.GameCore.TargetFetchAdvPropEx", "FetchType":"SinglePropByPropID",
          "SinglePropID":{"GroupID":{"FixedValue":{"Value":5}},"ID":{"FixedValue":{"Value":300002}}}}}
        """));
    await p.TaskManager!.LevelTask.PropStateExecute(task, new SubMissionData(0));
    Check(prop.State == PropStateEnum.Open, "Async target was not applied");
}

static SceneInstance Scene(PlayerInstance p, int floor)
{
    var scene = new SceneInstance(p, new() { PlaneID = 20001, PlaneType = PlaneTypeEnum.Maze }, floor, 0);
    Set(p, nameof(p.SceneInstance), scene);
    p.Data.FloorId = floor;
    return scene;
}

async Task OpeningStory()
{
    var mains = JsonConvert.DeserializeObject<List<MainMissionExcel>>(
        File.ReadAllText(Path.Combine(resourcePath, "ExcelOutput/MainMission.json")))!;
    foreach (var main in mains.Where(x => x.MainMissionID is 1000101 or 1000201))
        GameData.MainMissionData[main.MainMissionID] = main;
    ResourceManager.LoadMissionInfo(); ResourceManager.LoadSubMissionInfo();
    foreach (var excel in JsonConvert.DeserializeObject<List<SpecialAvatarExcel>>(
                 File.ReadAllText(Path.Combine(resourcePath, "ExcelOutput/SpecialAvatar.json")))!) excel.Loaded();
    MissionManager.FinishTypeHandlers[MissionFinishTypeEnum.PropState] = new MissionHandlerPropState();
    MissionManager.FinishTypeHandlers[MissionFinishTypeEnum.GetTrialAvatar] = new MissionHandlerGetTrialAvatar();
    MissionManager.FinishTypeHandlers[MissionFinishTypeEnum.DelTrialAvatar] = new MissionHandlerDelTrialAvatar();
    MissionManager.FinishTypeHandlers[MissionFinishTypeEnum.KillMonster] = new MissionHandlerKillMonster();
    MissionManager.FinishTypeHandlers[MissionFinishTypeEnum.EnterFloor] = new MissionHandlerEnterFloor();
    MissionManager.FinishTypeHandlers[MissionFinishTypeEnum.SubMissionFinishCnt] = new MissionHandlerSubMissionFinishCnt();
    var p = Fixture(2006);
    await p.LineupManager!.AddSpecialAvatarToCurTeam(10010050, false);
    Scene(p, 20001001);
    await p.MissionManager!.AcceptMainMissionByCondition();
    Check(await p.MissionManager.AcceptMainMissionFromClient(1000201) == Retcode.RetMissionNotFinish,
        "Client bypassed the previous chapter");
    Check(p.MissionManager.GetMainMissionStatus(1000201) == MissionPhaseEnum.None, "Next chapter started early");
    await p.MissionManager.HandleTalkStr("Mission_100010133");
    Check(p.MissionManager.GetSubMissionStatus(100010134) == MissionPhaseEnum.None, "Out-of-order talk skipped story");

    async Task Talk(int id)
    {
        Check(p.MissionManager!.GetSubMissionStatus(id) == MissionPhaseEnum.Accept, $"Talk {id} is not active");
        await p.MissionManager.HandleTalkStr($"Mission_{id}");
        Check(p.MissionManager.GetSubMissionStatus(id) == MissionPhaseEnum.Finish, $"Talk {id} did not finish");
    }
    async Task Prop(int missionId)
    {
        var info = GameData.SubMissionInfoData[missionId].SubMissionInfo!;
        Check(p.MissionManager!.GetSubMissionStatus(missionId) == MissionPhaseEnum.Accept, $"Prop {missionId} inactive");
        var prop = new EntityProp(p.SceneInstance!, new() { PropType = PropTypeEnum.PROP_ORDINARY },
            new() { Id = info.ParamInt1 }, new() { ID = info.ParamInt2 }) { State = (PropStateEnum)info.ParamInt3 };
        await p.SceneInstance!.AddEntity(prop, false);
        await p.MissionManager.OnPlayerInteractWithProp();
        Check(p.MissionManager.GetSubMissionStatus(missionId) == MissionPhaseEnum.Finish, $"Prop {missionId} did not finish");
    }
    async Task Battle(int missionId)
    {
        var info = GameData.SubMissionInfoData[missionId].SubMissionInfo!;
        var instance = new BattleInstance(p, p.LineupManager!.GetCurLineup()!, new List<StageConfigExcel>
            { new() { StageID = info.ParamInt1 * 10 } });
        await p.MissionManager!.OnBattleFinish(instance, new() { StageId = (uint)instance.StageId + 1, EndStatus = BattleEndStatus.BattleEndWin });
        Check(p.MissionManager.GetSubMissionStatus(missionId) == MissionPhaseEnum.Accept, "Mismatched stage accepted");
        await p.MissionManager.OnBattleFinish(instance, new() { StageId = (uint)instance.StageId, EndStatus = BattleEndStatus.BattleEndLose });
        Check(p.MissionManager.GetSubMissionStatus(missionId) == MissionPhaseEnum.Accept, "Lost battle accepted");
        await p.MissionManager.OnBattleFinish(instance, new() { StageId = (uint)instance.StageId, EndStatus = BattleEndStatus.BattleEndWin });
        Check(p.MissionManager.GetSubMissionStatus(missionId) == MissionPhaseEnum.Finish, "Correct battle did not advance");
    }
    async Task Kill(int missionId)
    {
        var info = GameData.SubMissionInfoData[missionId].SubMissionInfo!;
        var monster = new EntityMonster(p.SceneInstance!, new(), new(), info.ParamInt1 + 1, info.ParamInt2, new(), new());
        typeof(EntityMonster).GetProperty(nameof(monster.IsAlive))!.SetValue(monster, false);
        await p.MissionManager!.HandleFinishType(MissionFinishTypeEnum.KillMonster, monster);
        Check(p.MissionManager.GetSubMissionStatus(missionId) == MissionPhaseEnum.Accept, "Wrong group advanced kill");
        monster.GroupId = info.ParamInt1;
        await p.MissionManager.HandleFinishType(MissionFinishTypeEnum.KillMonster, monster);
        Check(p.MissionManager.GetSubMissionStatus(missionId) == MissionPhaseEnum.Finish, "Correct kill did not advance");
    }
    await Talk(100010102); await Talk(100010100); await Talk(100010104);
    await Battle(100010103); await Prop(100010105); await Talk(100010106); await Prop(100010107);
    await Talk(100010108); await Kill(100010122); await Talk(100010109);
    Check(p.MissionManager.GetSubMissionStatus(100010110) == MissionPhaseEnum.Finish &&
          p.LineupManager.GetCurLineup()!.BaseAvatars!.Any(x => x.SpecialAvatarId == 1001006), "Silver Wolf did not join before advancement");
    DatabaseHelper.UidInstanceMap.TryRemove(p.Uid, out _);
    p = Fixture(2006); Scene(p, 20001001);
    Check(p.MissionManager!.GetSubMissionStatus(100010111) == MissionPhaseEnum.Accept, "Reload lost objective");
    await Prop(100010111); await Talk(100010112); await Kill(100010130); await Battle(100010113);
    await Prop(100010124); await Talk(100010116);
    Check(p.LineupManager!.GetCurLineup()!.BaseAvatars!.All(x => x.SpecialAvatarId != 1001006), "Silver Wolf not removed");
    await Talk(100010117);
    Check(p.MissionManager.GetSubMissionStatus(100010127) == MissionPhaseEnum.None, "MultiSequence accepted before both talks");
    await Talk(100010118); await Talk(100010127); await Talk(100010119);
    await Talk(100010125); await Talk(100010129); await Talk(100010128); await Prop(100010126); await Talk(100010133);
    Check(p.MissionManager.GetSubMissionStatus(100010134) == MissionPhaseEnum.Accept && !p.Data.IsGenderSet,
        "Character selection was skipped");
    // Character creation and map transfer are supplied as external client events in this headless test.
    p.Data.IsGenderSet = true;
    await p.MissionManager.FinishSubMission(100010134);
    await Talk(100010136);
    Check(p.LineupManager.GetCurLineup()!.BaseAvatars!.All(x => x.SpecialAvatarId != 1001005), "Kafka not removed");
    Scene(p, 20002001);
    await p.MissionManager.HandleFinishType(MissionFinishTypeEnum.EnterFloor);
    await Talk(100020101); await Talk(100020102); await Talk(100020103);
    Check(p.MissionManager.GetMainMissionStatus(1000101) == MissionPhaseEnum.Finish &&
          p.MissionManager.GetMainMissionStatus(1000201) == MissionPhaseEnum.Accept, "Next chapter did not unlock");
    Check(await p.MissionManager.AcceptMainMissionFromClient(1000101) == Retcode.RetMissionAlreadyDone,
        "Finished chapter accepted again");
}

sealed class GateHandler : MissionFinishTypeHandler
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask HandleMissionFinishType(PlayerInstance player, SubMissionInfo info, object? arg)
    { Entered.TrySetResult(); await Release.Task; }
    public override ValueTask HandleQuestFinishType(PlayerInstance player, QuestDataExcel quest, FinishWayExcel excel, object? arg)
        => ValueTask.CompletedTask;
}
