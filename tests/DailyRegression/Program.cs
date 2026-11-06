using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.Database;
using March7thHoney.Database.Player;
using March7thHoney.Database.Scene;
using March7thHoney.Enums.Mission;
using March7thHoney.GameServer.Game.Battle;
using March7thHoney.GameServer.Game.DailyActive;
using March7thHoney.GameServer.Game.Inventory;
using March7thHoney.GameServer.Game.Mission;
using March7thHoney.GameServer.Game.Mission.FinishType.Handler;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Game.Quest;
using March7thHoney.GameServer.Game.Raid;
using March7thHoney.GameServer.Server.Packet.Send.DailyActive;
using March7thHoney.GameServer.Server.Packet.Send.Quest;
using March7thHoney.Proto;
using March7thHoney.Util;
using Newtonsoft.Json;
using SqlSugar;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : "Resources");
ConfigManager.Config.Path.ResourcePath = root;
ConfigManager.Config.ServerOption.EnableMission = true;
ConfigManager.Config.ServerOption.EnableQuest = true;
ConfigManager.Config.ServerOption.AutoUpgradeWorldLevel = false;
void Load<T>(string name) where T : ExcelResource
{
    foreach (var row in JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(Path.Combine(root, "ExcelOutput", name + ".json")))!)
        row.Loaded();
}
Load<DailyActiveConfigExcel>("DailyActiveConfig"); Load<DailyQuestExcel>("DailyQuest");
Load<DailyActiveQuestPoolExcel>("DailyActiveQuestPool");
Load<CocoonConfigExcel>("CocoonConfig");
Load<DailyMissionExcel>("DailyMissionData"); Load<QuestDataExcel>("QuestData");
Load<FinishWayExcel>("FinishWay"); Load<RewardDataExcel>("RewardData"); Load<ItemConfigExcel>("ItemConfig");
// One real NPC daily graph keeps this test focused and deterministic.
var npc = JsonConvert.DeserializeObject<List<MainMissionExcel>>(File.ReadAllText(Path.Combine(root, "ExcelOutput/MainMission.json")))!
    .Single(x => x.MainMissionID == 3000201);
GameData.MainMissionData[npc.MainMissionID] = npc;
ResourceManager.LoadMissionInfo();
MissionManager.FinishTypeHandlers[MissionFinishTypeEnum.SubMissionFinishCnt] = new MissionHandlerSubMissionFinishCnt();
var path = Path.Combine(Path.GetTempPath(), $"march7th-daily-{Guid.NewGuid():N}.sqlite");
var mysqlConnection = Environment.GetEnvironmentVariable("MARCH7TH_TEST_MYSQL_CONNECTION");
if (mysqlConnection != null)
{
    var connection = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = mysqlConnection };
    if (!connection.TryGetValue("Database", out var schema) || !schema.ToString()!.StartsWith("march7th_test_", StringComparison.Ordinal))
        throw new InvalidOperationException("MySQL regression requires a dedicated march7th_test_ database.");
}
using var db = new SqlSugarScope(new ConnectionConfig
{
    ConnectionString = mysqlConnection ?? $"Data Source={path};", DbType = mysqlConnection == null ? DbType.Sqlite : DbType.MySql, IsAutoCloseConnection = true,
    ConfigureExternalServices = DatabaseHelper.CreateExternalServices(mysqlConnection == null ? DbType.Sqlite : DbType.MySql)
});
DatabaseHelper.sqlSugarScope = db; _ = new DatabaseHelper();
foreach (var type in typeof(PlayerData).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BaseDatabaseDataHelper))))
    {
    try { db.CodeFirst.InitTables(type); }
    catch (Exception e) { throw new InvalidOperationException($"Schema initialization failed: {type.FullName}", e); }
}
var tests = new (string, Func<Task>)[]
{
    ("Resource tiers cover world levels 0-6 and both daily reward opcodes exist", Resources),
    ("Training unlocks by level and missions; no fixed 500 points", Unlocks),
    ("Login and expedition points require one reward claim", QuestRewards),
    ("Stamina uses quantity; unrelated/negative events cannot add progress", Stamina),
    ("Milestones reject early/invalid/duplicate claims, including concurrent requests", Milestones),
    ("Claim all grants only unclaimed eligible levels with actual world rewards", ClaimAll),
    ("Missing reward config preserves the claim for retry", MissingReward),
    ("Database reload preserves points, partial progress, claims and NPC selection", Reload),
    ("4 AM UTC+8 boundary resets daily state without clearing story/achievements", Reset),
    ("Real NPC dialogue completion grants the daily objective only once", NpcDaily),
    ("Battle objectives accumulate across wins; loss and duplicate callbacks do not count", Battles),
    ("Client cannot force complete or use optional reward to bypass daily claims", ClientGuards),
    ("Low-level board uses valid variants and changes bracket only on next reset", LowLevels),
    ("Crafting requires real materials before granting daily progress", Crafting),
    ("NPC item hand-in validates active objective and available items", HandIn),
    ("Legacy NPC completion can be assigned afresh; disabled quests cannot pay rewards", Legacy),
    ("Modern board rotates by group and remains fixed across reconnect/unlocks", Rotation),
    ("Calyx and cavern progress require a real matching battle victory", FarmBattles),
    ("Photography client report cannot complete server-owned objectives", Photograph),
    ("Real InteractPropList checks floor, group, prop and repeat interactions", PropMission),
    ("Real UseSelectedItem validates alternatives, slot count and exact consumption", SelectedItem)
};
var failures = 0;
foreach (var (name, run) in tests)
{
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failures++; Console.Error.WriteLine($"FAIL {name}: {e}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} scenarios passed. Isolated database: {(mysqlConnection == null ? path : "MySQL test schema")}");
return failures == 0 ? 0 : 1;

static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
static void Set(PlayerInstance p, string name, object value) => typeof(PlayerInstance).GetProperty(name)!.SetValue(p, value);
static PlayerInstance Player(int uid, TestClock clock, int level = 40, int world = 2, bool unlocked = true)
{
    var data = DatabaseHelper.Instance!.GetInstanceOrCreateNew<PlayerData>(uid);
    data.Level = level; data.WorldLevel = world;
    var p = new PlayerInstance(data) { Uid = uid };
    Set(p, nameof(p.SceneData), DatabaseHelper.Instance.GetInstanceOrCreateNew<SceneData>(uid));
    Set(p, nameof(p.MissionManager), new MissionManager(p));
    Set(p, nameof(p.InventoryManager), new InventoryManager(p));
    Set(p, nameof(p.QuestManager), new QuestManager(p));
    Set(p, nameof(p.RaidManager), new RaidManager(p));
    Set(p, nameof(p.DailyActivityManager), new DailyActivityManager(p, () => clock.Now));
    if (unlocked)
        foreach (var id in new[] { 1010403, 2000116, 8023701 }) p.MissionManager!.Data.SetMainMissionStatus(id, MissionPhaseEnum.Finish);
    return p;
}
static async Task Claim(PlayerInstance p, int id)
{
    var result = await p.QuestManager!.TakeQuestReward(id);
    Check(result.Item1 == Retcode.RetSucc, $"Claim {id} rejected: {result.Item1}");
}
static async Task Earn500(PlayerInstance p)
{
    await p.DailyActivityManager!.RefreshAsync();
    await Claim(p, 2100132);
    await p.DailyActivityManager.RecordProgress(2100133, 1); await Claim(p, 2100133);
    await p.SpendStamina(120); await Claim(p, 2100139);
    var extra = p.DailyActivityManager.Data.QuestIds.First(id => id != 2100132 && id != 2100133 && id != 2100139 && id != 2100003 && id != 2100156);
    await p.DailyActivityManager.RecordProgress(extra, 999); await Claim(p, extra);
    Check(p.DailyActivityManager.Data.Points == 500, "Did not reach 500");
}
static Task Resources()
{
    Check(GameData.DailyActiveConfigData.Count == 35, "Missing world-level tiers");
    Check(GameData.DailyMissionData.Count == 88, "NPC pool not loaded");
    var attrs = typeof(PlayerInstance).Assembly.GetTypes().SelectMany(t =>
        t.GetCustomAttributes(typeof(March7thHoney.GameServer.Server.Packet.Opcode), false)
            .Cast<March7thHoney.GameServer.Server.Packet.Opcode>()).ToList();
    Check(attrs.Count(x => x.CmdId == 3391) == 1 && attrs.Count(x => x.CmdId == 3318) == 1, "Daily reward handlers missing or duplicated");
    return Task.CompletedTask;
}
static async Task Unlocks()
{
    var p = Player(3001, new(), 9, unlocked: false);
    await p.DailyActivityManager!.RefreshAsync();
    Check(p.DailyActivityManager.Data.QuestIds.Count == 0 && p.DailyActivityManager.Data.Points == 0, "Locked board active");
    p.Data.Level = 10;
    await p.DailyActivityManager.RefreshAsync();
    Check(p.DailyActivityManager.IsAssigned(2100132) && !p.DailyActivityManager.IsAssigned(2100003), "Unlock conditions ignored");
    var proto = GetDailyActiveInfoScRsp.Parser.ParseFrom(new PacketGetDailyActiveInfoScRsp(p.DailyActivityManager).Data);
    Check(proto.DailyActivePoint == 0 && proto.DailyActiveLevelList.All(x => !x.IsHasTaken && x.WorldLevel == 2), "Fake points or rewards remain");
}
static async Task QuestRewards()
{
    var p = Player(3002, new()); await p.DailyActivityManager!.RefreshAsync();
    await Claim(p, 2100132); await p.DailyActivityManager.RecordProgress(2100133, 1); await Claim(p, 2100133);
    Check(p.DailyActivityManager.Data.Points == 200, "Wrong training points");
    Check((await p.QuestManager!.TakeQuestReward(2100133)).Item1 != Retcode.RetSucc, "Quest paid twice");
    Check(!p.InventoryManager!.Data.MaterialItems.Any(x => x.ItemId == 23), "Points incorrectly stored as inventory");
}
static async Task Stamina()
{
    var p = Player(3003, new()); await p.DailyActivityManager!.RefreshAsync();
    await p.SpendStamina(40); await p.SpendStamina(-100);
    Check(p.QuestManager!.GetQuestProgress(2100139) == 40, "Wrong stamina increment");
    var proto = GetQuestDataScRsp.Parser.ParseFrom(new PacketGetQuestDataScRsp(p).Data);
    Check(proto.QuestList.Single(x => x.Id == 2100139).Progress == 40, "Reconnect payload lost progress");
    await p.SpendStamina(80);
    Check(p.QuestManager.GetQuestStatus(2100139) == QuestStatus.QuestFinish, "120 stamina did not finish");
}
static async Task Milestones()
{
    var p = Player(3004, new()); await p.DailyActivityManager!.RefreshAsync();
    Check((await p.DailyActivityManager.TakeRewards(1)).Code != Retcode.RetSucc, "Early reward granted");
    await Claim(p, 2100132);
    var outcomes = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(async () => await p.DailyActivityManager.TakeRewards(1))));
    Check(outcomes.Count(x => x.Code == Retcode.RetSucc) == 1, "Concurrent reward duplicated");
    var reward = GameData.RewardDataData[GameData.DailyActiveConfigData[201].DailyActiveReward];
    Check(p.Data.Hcoin == reward.Hcoin, "Currency duplicated");
    Check((await p.DailyActivityManager.TakeRewards(999)).Code != Retcode.RetSucc, "Unknown tier granted");
}
static async Task ClaimAll()
{
    var p = Player(3005, new(), world: 6); await Earn500(p);
    await p.DailyActivityManager!.TakeRewards(1);
    var result = await p.DailyActivityManager.TakeRewards();
    Check(result.Code == Retcode.RetSucc && result.Levels.SequenceEqual(new[] { 2, 3, 4, 5 }), "Claim-all included paid tier");
    Check(p.Data.Hcoin == p.DailyActivityManager.Levels.Sum(x => GameData.RewardDataData[x.DailyActiveReward].Hcoin), "Wrong actual reward sum");
    Check((await p.DailyActivityManager.TakeRewards()).Code != Retcode.RetSucc, "Claim-all paid twice");
}
static async Task MissingReward()
{
    var p = Player(3006, new()); await p.DailyActivityManager!.RefreshAsync(); await Claim(p, 2100132);
    var id = GameData.DailyActiveConfigData[201].DailyActiveReward;
    var reward = GameData.RewardDataData[id]; GameData.RewardDataData.Remove(id);
    try { Check((await p.DailyActivityManager.TakeRewards(1)).Code != Retcode.RetSucc && p.DailyActivityManager.Data.ClaimedLevels.Count == 0, "Missing config consumed reward"); }
    finally { GameData.RewardDataData[id] = reward; }
    Check((await p.DailyActivityManager.TakeRewards(1)).Code == Retcode.RetSucc, "Retry failed");
}
static async Task Reload()
{
    var clock = new TestClock(); var p = Player(3007, clock);
    await p.DailyActivityManager!.RefreshAsync(); await Claim(p, 2100132);
    await p.DailyActivityManager.TakeRewards(1); await p.SpendStamina(40);
    var hcoin = p.Data.Hcoin; var npcId = p.DailyActivityManager.Data.NpcMissionId;
    DatabaseHelper.UidInstanceMap.TryRemove(p.Uid, out _); p = Player(3007, clock);
    await p.DailyActivityManager!.RefreshAsync();
    Check(p.Data.Hcoin == hcoin && p.DailyActivityManager.Data.Points == 100 && p.QuestManager!.GetQuestProgress(2100139) == 40 &&
        p.DailyActivityManager.Data.NpcMissionId == npcId, "Daily state lost after Database reload");
    Check((await p.DailyActivityManager.TakeRewards(1)).Code != Retcode.RetSucc, "Reload duplicated reward");
}
static async Task Reset()
{
    var clock = new TestClock { Now = DateTimeOffset.Parse("2026-11-01T19:59:59Z").ToUnixTimeSeconds() };
    var p = Player(3008, clock); await Earn500(p); await p.DailyActivityManager!.TakeRewards();
    p.QuestManager!.QuestData.Quests[999] = new() { QuestId = 999, QuestStatus = QuestStatus.QuestClose, Progress = 7 };
    var day = p.DailyActivityManager.Data.Day;
    clock.Now++; await p.DailyActivityManager.RefreshAsync();
    Check(p.DailyActivityManager.Data.Day == day + 1 && p.DailyActivityManager.Data.Points == 0 &&
        p.DailyActivityManager.Data.ClaimedLevels.Count == 0 && p.QuestManager.GetQuestProgress(2100139) == 0, "Reset boundary wrong");
    Check(p.QuestManager.GetQuestProgress(999) == 7 && p.MissionManager!.GetMainMissionStatus(1010403) == MissionPhaseEnum.Finish, "Reset erased unrelated progress");
    await Claim(p, 2100132); clock.Now -= 86400; await p.DailyActivityManager.RefreshAsync();
    Check(p.DailyActivityManager.Data.Points == 100, "Clock rollback reset rewards");
}
static async Task NpcDaily()
{
    var clock = new TestClock(); var p = Player(3009, clock); await p.DailyActivityManager!.RefreshAsync();
    Check(p.DailyActivityManager.Data.NpcMissionId == 3000201 && p.MissionManager!.GetMainMissionStatus(3000201) == MissionPhaseEnum.Accept, "NPC daily not assigned");
    foreach (var id in new[] { 300020101, 300020103, 300020104, 300020105, 300020106 })
        await p.MissionManager!.HandleTalkStr($"Mission_{id}");
    Check(p.MissionManager!.GetMainMissionStatus(3000201) == MissionPhaseEnum.Finish && p.DailyActivityManager.Data.NpcCompleted &&
        p.QuestManager!.GetQuestStatus(2100003) == QuestStatus.QuestFinish, "NPC completion did not advance daily");
    await Claim(p, 2100003); var money = p.Data.Scoin;
    await p.MissionManager.FinishMainMission(3000201);
    Check(p.Data.Scoin == money && p.DailyActivityManager.Data.Points == 200, "NPC reward duplicated");
    clock.Now += 86400; await p.DailyActivityManager.RefreshAsync();
    Check(!p.DailyActivityManager.Data.NpcCompleted && p.MissionManager.GetSubMissionStatus(300020101) == MissionPhaseEnum.Accept, "NPC did not restart next day");
}
static async Task Battles()
{
    var p = Player(3010, new()); await p.DailyActivityManager!.RefreshAsync();
    BattleInstance Battle(BattleEndStatus status) => new(p,
        new March7thHoney.Database.Lineup.LineupInfo { BaseAvatars = [new() { AssistUid = 123 }] }, new List<StageConfigExcel>())
        { BattleResult = new() { EndStatus = status, Stt = new() { BattleTargetInfo = { [2] = new() { BattleTargetList_ = { new BattleTarget { Id = 20074, Progress = 10 } } } } } } };
    var first = Battle(BattleEndStatus.BattleEndWin);
    await p.DailyActivityManager.OnBattleFinished(first); await p.DailyActivityManager.OnBattleFinished(first);
    await p.DailyActivityManager.OnBattleFinished(Battle(BattleEndStatus.BattleEndLose));
    Check(p.QuestManager!.GetQuestProgress(2100152) == 10 && p.QuestManager.GetQuestStatus(2100153) == QuestStatus.QuestFinish, "Battle duplicate/loss handling incorrect");
    await p.DailyActivityManager.OnBattleFinished(Battle(BattleEndStatus.BattleEndWin));
    Check(p.QuestManager.GetQuestStatus(2100152) == QuestStatus.QuestFinish, "Battle progress did not accumulate");
}
static async Task ClientGuards()
{
    var p = Player(3011, new()); await p.DailyActivityManager!.RefreshAsync();
    Check(await p.QuestManager!.FinishQuestByClient(2100139) != Retcode.RetSucc, "Client auto-finished stamina");
    Check((await p.QuestManager.TakeQuestOptionalReward(2100132, 103101)).Item1 != Retcode.RetSucc, "Optional reward bypassed training");
    Check(await p.QuestManager.AcceptQuest(2100003) == null, "Legacy daily accepted outside assigned board");
}
static async Task LowLevels()
{
    var clock = new TestClock(); var p = Player(3012, clock, level: 35);
    await p.DailyActivityManager!.RefreshAsync();
    var ids = p.DailyActivityManager.Data.QuestIds.ToArray();
    var types = ids.Select(id => GameData.DailyActiveQuestPoolData[id].Type).ToArray();
    Check(types.Contains(11) && types.Contains(12) && types.Contains(13) && !types.Contains(14), "Wrong low-level groups");
    Check(types.Distinct().Count() == types.Length && ids.All(id => !GameData.DailyQuestData[id].IsDelete), "Duplicate group/deleted quest assigned");
    var objective = ids.Single(id => GameData.DailyActiveQuestPoolData[id].Type == 13);
    var finish = GameData.FinishWayData[GameData.QuestDataData[objective].FinishWayID];
    await p.DailyActivityManager.RecordEvent(finish.FinishType, 1);
    Check(p.QuestManager!.GetQuestStatus(objective) == QuestStatus.QuestFinish, "Low-level event not wired");
    p.Data.Level = 36; await p.DailyActivityManager.RefreshAsync();
    Check(!p.DailyActivityManager.IsAssigned(2100155), "Level-up duplicated daily objectives");
    clock.Now += 86400; await p.DailyActivityManager.RefreshAsync();
    Check(p.DailyActivityManager.IsAssigned(2100155) && !p.DailyActivityManager.IsAssigned(2100148), "Next day did not switch brackets");
}
static async Task Crafting()
{
    var p = Player(3013, new()); await p.DailyActivityManager!.RefreshAsync();
    GameData.ItemComposeConfigData[999999] = new() { ID = 999999, ItemID = 212, MaterialCost = [new() { ItemID = 222, ItemNum = 2 }] };
    Check(await p.InventoryManager!.ComposeItem(999999, 1, []) == null &&
        p.QuestManager!.GetQuestProgress(2100155) == 0, "Empty inventory granted crafting progress");
    Check(await p.InventoryManager.ComposeItem(999999, -1, []) == null, "Negative crafting accepted");
    await p.InventoryManager.AddItem(222, 2, false);
    Check(await p.InventoryManager.ComposeItem(999999, 1, []) != null &&
        p.QuestManager!.GetQuestStatus(2100155) == QuestStatus.QuestFinish &&
        (p.InventoryManager.GetItem(222)?.Count ?? 0) == 0, "Valid crafting failed");
}
static async Task HandIn()
{
    var p = Player(3014, new()); await p.DailyActivityManager!.RefreshAsync();
    var main = new MainMissionExcel { MainMissionID = 999999 };
    main.SetMissionInfo(new March7thHoney.Data.Config.MissionInfo
    {
        MainMissionID = main.MainMissionID, StartSubMissionList = [99999901], FinishSubMissionList = [99999901],
        SubMissionList = [new() { ID = 99999901, FinishType = MissionFinishTypeEnum.ConsumeMissionItem,
            ParamItemList = [new() { ItemID = 212, ItemNum = 3 }] }]
    });
    GameData.MainMissionData[main.MainMissionID] = main;
    await p.MissionManager!.AcceptMainMission(main.MainMissionID);
    Check(await p.MissionManager.ConsumeMissionItems(99999901) == Retcode.RetItemNotEnough, "Missing items completed hand-in");
    await p.InventoryManager!.AddItem(212, 3, false);
    Check(await p.MissionManager.ConsumeMissionItems(99999901) == Retcode.RetSucc &&
        (p.InventoryManager.GetItem(212)?.Count ?? 0) == 0, "Valid hand-in did not consume items");
    Check(await p.MissionManager.ConsumeMissionItems(99999901) != Retcode.RetSucc, "Repeated hand-in accepted");
    Check(await p.MissionManager.ConsumeMissionItems(300020101) != Retcode.RetSucc, "Talk objective completed by item request");
}
static async Task Legacy()
{
    var p = Player(3015, new());
    p.MissionManager!.Data.SetMainMissionStatus(3000201, MissionPhaseEnum.Finish);
    p.MissionManager.Data.SetSubMissionStatus(300020101, MissionPhaseEnum.Finish);
    await p.DailyActivityManager!.RefreshAsync();
    Check(p.MissionManager.GetMainMissionStatus(3000201) == MissionPhaseEnum.Accept &&
        p.MissionManager.GetSubMissionStatus(300020101) == MissionPhaseEnum.Accept, "Legacy NPC claim blocked the assigned mission");
    await Claim(p, 2100132);
    ConfigManager.Config.ServerOption.EnableQuest = false;
    try { Check((await p.DailyActivityManager.TakeRewards(1)).Code != Retcode.RetSucc, "Disabled daily paid rewards"); }
    finally { ConfigManager.Config.ServerOption.EnableQuest = true; }
}
static async Task Rotation()
{
    var clock = new TestClock(); var p = Player(3020, clock, level: 35);
    foreach (var id in new[] { 4010121, 4010123, 4010125, 4010127, 1000202 }) p.MissionManager!.Data.SetMainMissionStatus(id, MissionPhaseEnum.Finish);
    var boards = new HashSet<string>();
    for (var day = 0; day < 8; day++)
    {
        await p.DailyActivityManager!.RefreshAsync();
        var ids = p.DailyActivityManager.Data.QuestIds.ToArray();
        Check(ids.Select(id => GameData.DailyActiveQuestPoolData[id].Type).Distinct().Count() == ids.Length, "Multiple objectives per group");
        Check(ids.Contains(2100156) && !ids.Contains(2100150) && ids.All(id => GameData.DailyActiveQuestPoolData[id].Type is 1 or >= 6), "Legacy/obsolete alternatives leaked");
        boards.Add(string.Join(',', ids));
        DatabaseHelper.UidInstanceMap.TryRemove(p.Uid, out _); p = Player(3020, clock, level: 35);
        await p.DailyActivityManager.RefreshAsync();
        Check(p.DailyActivityManager.Data.QuestIds.SequenceEqual(ids), "Relog rerolled board");
        clock.Now += 86400;
    }
    Check(boards.Count > 1, "Board never rotates");
}
static async Task FarmBattles()
{
    foreach (var (uid, unlock, quest, validCocoon, invalidCocoon) in new[]
    {
        (3021, 4010125, 2100137, 1201, 1001),
        (3025, 4010121, 2100157, 1001, 1004),
        (3026, 4010123, 2100158, 1004, 1001),
        (3027, 4010127, 2100138, 0, 1001)
    })
    {
        var p = Player(uid, new(), level: 35);
        p.MissionManager!.Data.SetMainMissionStatus(unlock, MissionPhaseEnum.Finish);
        await p.DailyActivityManager!.RefreshAsync();
        Check(p.DailyActivityManager.IsAssigned(quest), $"Farm objective {quest} missing");
        BattleInstance Battle(int id, BattleEndStatus status) => new(p, new March7thHoney.Database.Lineup.LineupInfo(), new List<StageConfigExcel>())
            { CocoonId = id, CocoonWave = 1, MappingInfoId = id == 0 ? 1 : 0, WorldLevel = 2, BattleResult = new() { EndStatus = status } };
        await p.DailyActivityManager.OnBattleFinished(Battle(validCocoon, BattleEndStatus.BattleEndLose));
        await p.DailyActivityManager.OnBattleFinished(Battle(invalidCocoon, BattleEndStatus.BattleEndWin));
        Check(p.QuestManager!.GetQuestProgress(quest) == 0, "Wrong farm/loss credited");
        await p.DailyActivityManager.OnBattleFinished(Battle(validCocoon, BattleEndStatus.BattleEndWin));
        Check(p.QuestManager.GetQuestStatus(quest) == QuestStatus.QuestFinish, $"Farm win did not credit {quest}");
    }
}
static async Task Photograph()
{
    var clock = new TestClock(); var p = Player(3022, clock, level: 35);
    for (var i = 0; i < 8; i++)
    {
        await p.DailyActivityManager!.RefreshAsync();
        if (p.DailyActivityManager.IsAssigned(2100149)) break;
        clock.Now += 86400;
    }
    Check(p.DailyActivityManager!.IsAssigned(2100149), "Photo never selected");
    Check(await p.QuestManager!.FinishQuestByClient(2100149) == Retcode.RetSucc && p.QuestManager.GetQuestStatus(2100149) == QuestStatus.QuestFinish, "Photo report rejected");
    Check(await p.QuestManager.FinishQuestByClient(2100003) != Retcode.RetSucc, "Client completed NPC mission");
}
static void LoadNpc(int id)
{
    var root = ConfigManager.Config.Path.ResourcePath;
    var npc = JsonConvert.DeserializeObject<List<MainMissionExcel>>(File.ReadAllText(Path.Combine(root, "ExcelOutput/MainMission.json")))!.Single(x => x.MainMissionID == id);
    GameData.MainMissionData[id] = npc;
    ResourceManager.LoadMissionInfo();
}
static async Task PropMission()
{
    LoadNpc(3011601);
    var p = Player(3023, new()); await p.DailyActivityManager!.RefreshAsync();
    await p.MissionManager!.AcceptMainMission(3011601);
    await p.MissionManager.OnMissionPropInteracted(20111002, 75, 300001);
    await p.MissionManager.OnMissionPropInteracted(20111001, 76, 300001);
    await p.MissionManager.OnMissionPropInteracted(20111001, 75, 300002);
    Check(p.MissionManager.GetSubMissionStatus(301160101) == MissionPhaseEnum.Accept, "Wrong prop completed mission");
    await p.MissionManager.OnMissionPropInteracted(20111001, 75, 300001);
    Check(p.MissionManager.GetSubMissionStatus(301160101) == MissionPhaseEnum.Finish, "Actual prop did not complete objective");
    var coins = p.Data.Scoin; await p.MissionManager.OnMissionPropInteracted(20111001, 75, 300001);
    Check(p.Data.Scoin == coins && p.MissionManager.Data.InteractedMissionProps[301160101].Count == 1, "Repeated interaction credited twice");
}
static async Task SelectedItem()
{
    LoadNpc(3011605);
    var p = Player(3024, new()); await p.DailyActivityManager!.RefreshAsync();
    await p.MissionManager!.AcceptMainMission(3011605);
    Check(await p.MissionManager.ConsumeMissionItems(301160501) != Retcode.RetSucc, "Missing selection accepted");
    Check(await p.MissionManager.ConsumeMissionItems(301160501, [new() { ItemId = 401004, Count = 1 }]) == Retcode.RetItemNotEnough, "Missing item accepted");
    await p.InventoryManager!.AddItem(401004, 2, false); await p.InventoryManager.AddItem(401012, 1, false);
    Check(await p.MissionManager.ConsumeMissionItems(301160501, [new() { ItemId = 401004, Count = -1 }]) != Retcode.RetSucc, "Negative count accepted");
    Check(await p.MissionManager.ConsumeMissionItems(301160501, [new() { ItemId = 401004, Count = 1 }, new() { ItemId = 401012, Count = 1 }]) != Retcode.RetSucc, "Extra slots consumed");
    Check(await p.MissionManager.ConsumeMissionItems(301160501, [new() { ItemId = 401004, Count = 1 }]) == Retcode.RetSucc, "Valid selection rejected");
    Check(p.InventoryManager.GetItem(401004)?.Count == 1 && p.InventoryManager.GetItem(401012)?.Count == 1 &&
        p.MissionManager.GetSubMissionStatus(301160501) == MissionPhaseEnum.Finish, "Wrong selected consumption");
    Check(await p.MissionManager.ConsumeMissionItems(301160501, [new() { ItemId = 401004, Count = 1 }]) != Retcode.RetSucc, "Repeated selection accepted");
}
sealed class TestClock { public long Now = DateTimeOffset.Parse("2026-11-01T12:00:00Z").ToUnixTimeSeconds(); }
