using March7thHoney.Data;
using March7thHoney.Data.Config.Scene;
using March7thHoney.Data.Excel;
using March7thHoney.Database;
using March7thHoney.Database.Inventory;
using March7thHoney.Database.Player;
using March7thHoney.Database.Scene;
using March7thHoney.Enums.Item;
using March7thHoney.Enums.Scene;
using March7thHoney.GameServer.Game.Inventory;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Game.Scene;
using March7thHoney.GameServer.Game.Scene.Entity;
using March7thHoney.GameServer.Game.Mission;
using March7thHoney.GameServer.Game.Raid;
using Newtonsoft.Json;
using SqlSugar;

// No server entrypoint or user database. All persistence uses an isolated temporary SQLite file.
var databasePath = Path.Combine(Path.GetTempPath(), $"march7th-chests-{Guid.NewGuid():N}.sqlite");
using var database = new SqlSugarScope(new ConnectionConfig
{
    ConnectionString = $"Data Source={databasePath};",
    DbType = DbType.Sqlite,
    IsAutoCloseConnection = true,
    ConfigureExternalServices = new() { SerializeService = new CustomSerializeService() }
});
DatabaseHelper.sqlSugarScope = database;
_ = new DatabaseHelper();
database.CodeFirst.InitTables<PlayerData, SceneData, InventoryData>();
database.CodeFirst.InitTables<RaidData, March7thHoney.Database.Quests.MissionData>();
SeedResources();
var tests = new (string Name, Func<Task> Run)[]
{
    ("World chest awards once and persists despite Reset group", OpenOnce),
    ("Concurrent duplicate requests award exactly once", ConcurrentOpen),
    ("Puzzle state changes cannot reopen a claimed chest", PuzzleReset),
    ("Scene loader restores opened state and client payload", ReloadScene),
    ("Physical chest identities and accounts remain independent", IndependentChests),
    ("Invalid interactions and locked chests grant nothing", RejectedInteractions),
    ("Missing reward resources leave the chest claimable", MissingRewards),
    ("Legacy ChestUsed JSON migrates without another reward", LegacyState),
    ("Instance chests and ordinary Reset props keep reset behavior", InstanceReset),
    ("Fresh SQLite reload preserves claim, currency, materials and marks", DatabaseReload),
    ("Raid cleanup preserves world chest claims on shared floors", RaidCleanup)
};
var failed = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} scenarios passed.");
if (args.Length > 0)
{
    try { await ResourceChest(args[0]); Console.WriteLine("PASS Chest from supplied Resources opens once across SQLite reload"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL Resources chest: {ex}"); }
}
Console.WriteLine($"Isolated database: {databasePath}");
return failed == 0 ? 0 : 1;

static void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

static void Set(PlayerInstance player, string name, object value) =>
    typeof(PlayerInstance).GetProperty(name)!.SetValue(player, value);

static void SeedResources()
{
    GameData.InteractConfigData[99001] = new()
    { InteractID = 99001, SrcState = PropStateEnum.ChestClosed, TargetState = PropStateEnum.ChestUsed };
    GameData.MazeChestData[99002] = new()
    { ID = 99002, ChestType = [ChestTypeEnum.CHEST_LOW_LEVEL] };
    GameData.MazePropData[99003] = new()
    { ID = 99003, PropType = PropTypeEnum.PROP_TREASURE_CHEST };
    foreach (var id in new[] { 1, 2, 212, 222, 232 })
        GameData.ItemConfigData[id] = new()
        { ID = id, ItemMainType = id <= 2 ? ItemMainTypeEnum.Virtual : ItemMainTypeEnum.Material, PileLimit = 99999 };
}

static PlayerInstance Player(int uid)
{
    var data = DatabaseHelper.Instance!.GetInstanceOrCreateNew<PlayerData>(uid);
    var player = new PlayerInstance(data) { Uid = uid };
    Set(player, nameof(PlayerInstance.SceneData), DatabaseHelper.Instance.GetInstanceOrCreateNew<SceneData>(uid));
    Set(player, nameof(PlayerInstance.InventoryManager), new InventoryManager(player));
    Scene(player);
    return player;
}

static SceneInstance Scene(PlayerInstance player, PlaneTypeEnum type = PlaneTypeEnum.Maze, int floor = 99004)
{
    var scene = new SceneInstance(player, new MazePlaneExcel { PlaneID = 99005, PlaneType = type }, floor, 0);
    Set(player, nameof(PlayerInstance.SceneInstance), scene);
    return scene;
}

static async Task<EntityProp> Chest(PlayerInstance player, int group = 10, int id = 20,
    PropStateEnum state = PropStateEnum.ChestClosed, SaveTypeEnum save = SaveTypeEnum.Reset)
{
    var prop = await new SceneEntityLoader(player.SceneInstance!).LoadProp(
        new PropInfo { ID = id, PropID = 99003, ChestID = 99002, State = state, Name = "TestChest" },
        new GroupInfo { Id = group, SaveType = save });
    return prop ?? throw new InvalidOperationException("Chest not loaded");
}

static async Task Open(PlayerInstance player, EntityProp chest) =>
    _ = await player.InteractProp(chest.EntityId, 99001);

static void Claimed(PlayerInstance player, EntityProp chest) =>
    Check(chest.State == PropStateEnum.ChestUsed &&
          player.GetScenePropData(chest.Scene.FloorId, chest.GroupId, chest.InstId) is
              { ChestOpened: true, State: PropStateEnum.ChestUsed }, "Missing permanent claim");

static string Rewards(PlayerInstance player) => JsonConvert.SerializeObject(new
{
    player.Data.Hcoin, player.Data.Scoin, player.InventoryManager!.Data.MaterialItems
});

static async Task OpenOnce()
{
    var player = Player(1001);
    var chest = await Chest(player);
    await Open(player, chest);
    Claimed(player, chest);
    Check(player.Data.Hcoin == 5 && player.Data.Scoin == 750, "Wrong currency reward");
    Check(player.InventoryManager!.Data.MaterialItems.Count == 3, "Missing materials");
    var rewards = Rewards(player);
    await Open(player, chest);
    Check(Rewards(player) == rewards, "Duplicate reward");
}

static async Task ConcurrentOpen()
{
    var player = Player(1002);
    var chest = await Chest(player);
    await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => Open(player, chest))));
    Claimed(player, chest);
    Check(player.Data.Hcoin == 5 && player.Data.Scoin == 750, "Concurrent duplicate currency");
    Check(player.InventoryManager!.Data.MaterialItems.All(item => item.Count is >= 3 and <= 5),
        "Concurrent duplicate materials");
}

static async Task PuzzleReset()
{
    var player = Player(1003);
    var chest = await Chest(player);
    await Open(player, chest);
    foreach (var state in new[] { PropStateEnum.ChestClosed, PropStateEnum.ChestLocked, PropStateEnum.Open })
    {
        await chest.SetState(state);
        Claimed(player, chest);
    }
    // Even direct persisted-state updates must retain the terminal claim.
    player.SetScenePropData(chest.Scene.FloorId, chest.GroupId, chest.InstId, PropStateEnum.ChestClosed);
    Scene(player);
    chest = await Chest(player);
    Claimed(player, chest);
    await Open(player, chest);
    Check(player.Data.Hcoin == 5, "Puzzle awarded twice");
}

static async Task ReloadScene()
{
    var player = Player(1004);
    var chest = await Chest(player);
    await Open(player, chest);
    Scene(player);
    var reloaded = await Chest(player);
    Claimed(player, reloaded);
    Check(reloaded.ToProto().Prop.PropState == (uint)PropStateEnum.ChestUsed, "Client sees closed chest");
    await Open(player, reloaded);
    Check(player.Data.Hcoin == 5, "Map reload awarded twice");
}

static async Task IndependentChests()
{
    var player = Player(1005);
    await Open(player, await Chest(player));
    await Open(player, await Chest(player, id: 21));
    await Open(player, await Chest(player, group: 11));
    Scene(player, floor: 99006);
    await Open(player, await Chest(player));
    Check(player.Data.Hcoin == 20, "Chest identity did not include floor/group/config");
    var other = Player(1006);
    await Open(other, await Chest(other));
    Check(other.Data.Hcoin == 5, "Claim leaked across accounts");
}

static async Task RejectedInteractions()
{
    var player = Player(1007);
    var chest = await Chest(player, state: PropStateEnum.ChestLocked);
    await Open(player, chest);
    Check(chest.State == PropStateEnum.ChestLocked && player.Data.Hcoin == 0, "Locked chest opened");
    await chest.SetState(PropStateEnum.ChestClosed);
    Check(player.GetScenePropData(99004, 10, 20) == null, "Unclaimed Reset chest should still reset");
    await player.InteractProp(chest.EntityId, -1);
    await player.InteractProp(-1, 99001);
    Check(chest.State == PropStateEnum.ChestClosed && player.Data.Hcoin == 0, "Invalid request granted reward");
    await Open(player, chest);
    Check(player.Data.Hcoin == 5, "Valid request rejected");
}

static async Task MissingRewards()
{
    var player = Player(1008);
    var chest = await Chest(player);
    chest.PropInfo.ChestID = -1;
    await Open(player, chest);
    Check(!chest.WasOpened && player.Data.Hcoin == 0, "Missing chest config consumed claim");
    chest.PropInfo.ChestID = 99002;
    var material = GameData.ItemConfigData[212];
    GameData.ItemConfigData.Remove(212);
    try
    {
        await Open(player, chest);
        Check(!chest.WasOpened && player.Data.Hcoin == 0, "Missing item config consumed claim");
    }
    finally { GameData.ItemConfigData[212] = material; }
    await Open(player, chest);
    Claimed(player, chest);
    Check(player.Data.Hcoin == 5, "Restored resources could not award");
}

static async Task LegacyState()
{
    var player = Player(1009);
    player.SceneData!.ScenePropData[99004] = new()
    {
        [10] = [JsonConvert.DeserializeObject<ScenePropData>("{\"PropId\":20,\"State\":13}")!]
    };
    var chest = await Chest(player);
    Claimed(player, chest);
    await Open(player, chest);
    Check(player.Data.Hcoin == 0, "Legacy used chest awarded again");
}

static async Task InstanceReset()
{
    var player = Player(1010);
    foreach (var type in new[] { PlaneTypeEnum.Raid, PlaneTypeEnum.Rogue, PlaneTypeEnum.Challenge })
    {
        Scene(player, type);
        var chest = await Chest(player);
        Check(!chest.IsPersistentWorldChest, "Instance classified as world");
        await chest.SetState(PropStateEnum.ChestUsed);
        await chest.SetState(PropStateEnum.ChestClosed);
        Check(chest.State == PropStateEnum.ChestClosed &&
              player.GetScenePropData(99004, 10, 20) == null, "Instance Reset behavior changed");
    }
    Scene(player);
    var ordinary = new EntityProp(player.SceneInstance!, new MazePropExcel { PropType = PropTypeEnum.PROP_ORDINARY },
        new GroupInfo { Id = 10, SaveType = SaveTypeEnum.Reset }, new PropInfo { ID = 21 });
    await ordinary.SetState(PropStateEnum.Open);
    Check(player.GetScenePropData(99004, 10, 21) == null, "Ordinary Reset prop persisted");
}

static async Task DatabaseReload()
{
    var player = Player(1011);
    var chest = await Chest(player);
    player.SceneData!.MarkedChestData[99005] =
        [new SceneMarkedChestData { FloorId = 99004, GroupId = 10, ConfigId = 20 }];
    await Open(player, chest);
    var rewards = Rewards(player);
    // No explicit SaveUidData here: opening the chest must have already flushed to SQLite.
    DatabaseHelper.UidInstanceMap.TryRemove(player.Uid, out _);
    var reloaded = Player(1011);
    Check(reloaded.SceneData!.MarkedChestData[99005].Count == 0, "Opened chest map mark survived reload");
    Check(Rewards(reloaded) == rewards, "Rewards were not persisted with claim");
    var reloadedChest = await Chest(reloaded);
    Claimed(reloaded, reloadedChest);
    await Open(reloaded, reloadedChest);
    Check(Rewards(reloaded) == rewards, "SQLite reload awarded twice");
}

static async Task RaidCleanup()
{
    var player = Player(1012);
    await Open(player, await Chest(player));
    player.SetScenePropData(99004, 10, 21, PropStateEnum.Open);
    player.SetScenePropData(99004, 11, 22, PropStateEnum.Open);
    Set(player, nameof(PlayerInstance.MissionManager), new MissionManager(player));
    var raid = new RaidManager(player);
    raid.RaidData.RaidRecordDatas[99] = new() { [0] = new() { RaidId = 99 } };
    GameData.RaidConfigData[9900] = new() { RaidID = 99, MainMissionIDList = [999] };
    var mission = new MainMissionExcel { MainMissionID = 999 };
    mission.MissionInfo.SubMissionList.Add(new() { LevelFloorID = 99004 });
    GameData.MainMissionData[999] = mission;
    await raid.ClearRaid(99, 0);
    Check(player.GetScenePropData(99004, 10, 20) is { ChestOpened: true }, "Raid erased world claim");
    Check(player.GetScenePropData(99004, 10, 21) == null && player.GetScenePropData(99004, 11, 22) == null,
        "Raid failed to remove ordinary state");
}

static async Task ResourceChest(string root)
{
    List<T> Read<T>(string name) => JsonConvert.DeserializeObject<List<T>>(
        File.ReadAllText(Path.Combine(root, "ExcelOutput", name + ".json")))!;
    var interact = Read<InteractConfigExcel>("InteractConfig").First(x =>
        x.SrcState == PropStateEnum.ChestClosed && x.TargetState == PropStateEnum.ChestUsed);
    GameData.InteractConfigData[interact.InteractID] = interact;
    foreach (var item in Read<ItemConfigExcel>("ItemConfig")) GameData.ItemConfigData[item.ID] = item;
    foreach (var chest in Read<MazeChestExcel>("MazeChest")) GameData.MazeChestData[chest.ID] = chest;
    foreach (var prop in Read<MazePropExcel>("MazeProp")) GameData.MazePropData[prop.ID] = prop;
    var plane = Read<MazePlaneExcel>("MazePlane").Single(x => x.PlaneID == 10101);
    var group = JsonConvert.DeserializeObject<GroupInfo>(File.ReadAllText(Path.Combine(root,
        "Config/LevelOutput/SharedRuntimeGroup/Groups_P10101_F10101001/LevelGroup_P10101_F10101001_G161.json")))!;
    group.Id = 161;
    var info = group.PropList.Single(x => x.ChestID == 10101601);
    var player = Player(1013);
    Set(player, nameof(PlayerInstance.SceneInstance), new SceneInstance(player, plane, 10101001, 0));
    var entity = (await new SceneEntityLoader(player.SceneInstance!).LoadProp(info, group))!;
    Check(entity.IsPersistentWorldChest, "Real map chest not classified as persistent");
    await player.InteractProp(entity.EntityId, interact.InteractID);
    Claimed(player, entity);
    Check(player.Data.Hcoin == 5 && player.Data.Scoin == 750, "Real chest reward missing");
    Check(player.InventoryManager!.Data.MaterialItems.Any(x => x.ItemId == 120001 && x.Count == 20),
        "World currency missing");
    var rewards = Rewards(player);
    DatabaseHelper.UidInstanceMap.TryRemove(player.Uid, out _);
    player = Player(1013);
    Set(player, nameof(PlayerInstance.SceneInstance), new SceneInstance(player, plane, 10101001, 0));
    entity = (await new SceneEntityLoader(player.SceneInstance!).LoadProp(info, group))!;
    Claimed(player, entity);
    await player.InteractProp(entity.EntityId, interact.InteractID);
    Check(Rewards(player) == rewards, "Real chest awarded twice");
}
