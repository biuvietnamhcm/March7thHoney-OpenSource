using March7thHoney.Data;
using March7thHoney.Data.Custom;
using March7thHoney.Data.Excel;
using March7thHoney.Database;
using March7thHoney.Database.Player;
using March7thHoney.Database.Friend;
using March7thHoney.Database.Gacha;
using March7thHoney.Enums;
using March7thHoney.GameServer.Game.Avatar;
using March7thHoney.GameServer.Game.Gacha;
using March7thHoney.GameServer.Game.Inventory;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Game.Quest;
using March7thHoney.GameServer.Game.Mission;
using March7thHoney.Proto;
using March7thHoney.Util;
using Newtonsoft.Json;
using SqlSugar;

var root = Path.GetFullPath(args.FirstOrDefault() ?? "Resources");
void Load<T>(string name) where T : ExcelResource
{
    foreach (var row in JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(Path.Combine(root, "ExcelOutput", name + ".json")))!) { row.Loaded(); if (row is AvatarSkillTreeConfigExcel) row.AfterAllDone(); }
}
Load<AvatarConfigExcel>("AvatarConfig"); Load<AvatarConfigExcel>("AvatarConfigLD");
Load<AvatarSkillTreeConfigExcel>("AvatarSkillTreeConfig"); Load<AvatarSkillTreeConfigExcel>("AvatarSkillTreeConfigLD"); Load<EquipmentConfigExcel>("EquipmentConfig");
Load<DecideAvatarOrderExcel>("DecideAvatarOrder");
foreach (var name in new[] { "ItemConfig", "ItemConfigAvatar", "ItemConfigAvatarLD", "ItemConfigAvatarRank", "ItemConfigAvatarRankLD", "ItemConfigEquipment" }) Load<ItemConfigExcel>(name);
GameData.BannersConfig.Banners = JsonConvert.DeserializeObject<List<BannerConfig>>(File.ReadAllText("Config/Custom/Banners.json"))!;
var path = Path.Combine(Path.GetTempPath(), $"march7th-gacha-{Guid.NewGuid():N}.sqlite");
var mysqlConnection = Environment.GetEnvironmentVariable("MARCH7TH_TEST_MYSQL_CONNECTION");
if (mysqlConnection != null)
{
    var connection = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = mysqlConnection };
    if (!connection.TryGetValue("Database", out var schema) || !schema.ToString()!.StartsWith("march7th_test_", StringComparison.Ordinal))
        throw new InvalidOperationException("MySQL regression requires a dedicated march7th_test_ database.");
}
using var db = new SqlSugarScope(new ConnectionConfig { ConnectionString = mysqlConnection ?? $"Data Source={path};", DbType = mysqlConnection == null ? DbType.Sqlite : DbType.MySql,
    IsAutoCloseConnection = true, ConfigureExternalServices = DatabaseHelper.CreateExternalServices(mysqlConnection == null ? DbType.Sqlite : DbType.MySql) });
DatabaseHelper.sqlSugarScope = db; _ = new DatabaseHelper();
foreach (var type in typeof(PlayerData).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BaseDatabaseDataHelper)))) {
    try { db.CodeFirst.InitTables(type); }
    catch (Exception e) { throw new InvalidOperationException($"Schema initialization failed: {type.FullName}", e); }
}
var tests = new (string, Func<Task>)[]
{
    ("Resource pools exclude shop cones, off-rarity rate-ups and limited standard five-stars", Pools),
    ("Hard pity at 90 character/80 cone; ten-pull four-star guarantee and deferred four-star", Pity),
    ("Five-star and four-star loss guarantees survive banner change", Guarantees),
    ("Base rates, soft-pity boundaries and 50/75-percent featured rates", Rates),
    ("Invalid count, missing banner, insufficient tickets and expired banner do not mutate state", Rejections),
    ("Standard, character, cone, collaboration and departure pity are independent", Groups),
    ("Concurrent pulls cannot spend the same ticket", Concurrent),
    ("Departure costs eight tickets, guarantees a character within fifty and then closes", Departure),
    ("An early departure five-star consumes its one-time guarantee", EarlyDeparture),
    ("Duplicates grant Eidolons and correct Starlight, including E6 with no token stack", Duplicates),
    ("Four-star and five-star light cones grant correct Starlight", ConeTokens),
    ("Celestial Invitation validates seven of thirteen and excludes it from standard Warp", Selection),
    ("300-pull standard selector unlocks and can only be claimed once", Ceiling),
    ("Database reload keeps independent pity, guarantees, selection and history beyond fifty", Reload),
    ("Legacy shared pity migrates only once to the last known banner", Migration),
    ("Full equipment inventory rejects before spending tickets", Capacity),
    ("Fallback featured slot matches consolidated 4.2 rate and clears the guarantee", Fallback),
    ("Schema upgrade preserves existing JSON and persists history beyond 64 KiB", SchemaStorage)
};
var failures = 0;
foreach (var (name, run) in tests)
{
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {name}: {ex}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} scenarios passed. Isolated database: {(mysqlConnection == null ? path : "MySQL test schema")}");
return failures == 0 ? 0 : 1;

Task SchemaStorage()
{
    const int uid = 4030;
    var data = DatabaseHelper.Instance!.GetInstanceOrCreateNew<GachaData>(uid);
    data.GachaHistory.Add(new() { GachaId = 1001, ItemId = 20000, Time = 1777500000 });
    DatabaseHelper.Instance.SaveUidData(uid);
    if (mysqlConnection != null)
        db.Ado.ExecuteCommand("ALTER TABLE `Gacha` MODIFY COLUMN `GachaHistory` TEXT NOT NULL");
    DatabaseHelper.InitializeSqliteTable<GachaData>();
    DatabaseHelper.InitializeSqliteTable<GachaData>();
    DatabaseHelper.UidInstanceMap.TryRemove(uid, out _);
    data = DatabaseHelper.Instance.GetInstanceOrCreateNew<GachaData>(uid);
    Check(data.GachaHistory.Count == 1 && data.GachaHistory[0].ItemId == 20000, "Schema upgrade lost existing JSON");
    for (var i = 1; i < 4000; i++) data.GachaHistory.Add(new() { GachaId = 1001, ItemId = 20000, Time = 1777500000 + i });
    Check(System.Text.Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(data.GachaHistory)) > 65535, "History fixture is too small");
    DatabaseHelper.Instance.SaveUidData(uid);
    DatabaseHelper.UidInstanceMap.TryRemove(uid, out _);
    data = DatabaseHelper.Instance.GetInstanceOrCreateNew<GachaData>(uid);
    Check(data.GachaHistory.Count == 4000 && data.GachaHistory[^1].Time == 1777503999, "Long history was truncated");
    return Task.CompletedTask;
}

static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
static BannerConfig Banner(int id) => GameData.BannersConfig.Banners.Single(x => x.GachaId == id);
static void Set(PlayerInstance p, string name, object value) => typeof(PlayerInstance).GetProperty(name)!.SetValue(p, value);
static PlayerInstance Player(int uid, Func<double>? rng = null)
{
    var p = new PlayerInstance(DatabaseHelper.Instance!.GetInstanceOrCreateNew<PlayerData>(uid)) { Uid = uid };
    Set(p, nameof(p.AvatarManager), new AvatarManager(p)); Set(p, nameof(p.InventoryManager), new InventoryManager(p));
    Set(p, nameof(p.FriendRecordData), DatabaseHelper.Instance.GetInstanceOrCreateNew<FriendRecordData>(uid));
    Set(p, nameof(p.QuestManager), new QuestManager(p)); Set(p, nameof(p.MissionManager), new MissionManager(p));
    Set(p, nameof(p.GachaManager), new GachaManager(p, rng ?? (() => .999999), () => 1777500000));
    return p;
}
static async Task Fund(PlayerInstance p, int count = 100) { await p.InventoryManager!.AddItem(101, count, false); await p.InventoryManager.AddItem(102, count, false); }
static Task Pools()
{
    var m = Player(4001).GachaManager!;
    foreach (var b in GameData.BannersConfig.Banners)
    {
        var pools = m.BuildPools(b);
        Check(pools.FeaturedFour.All(x => m.GetRarity(x) == 4), "Five-star leaked to purple pool");
        Check(!pools.FourStarWeapons.Contains(21036) && !pools.FourStarWeapons.Contains(21014) && (b.GachaType == GachaTypeEnum.Newbie ? !pools.FourStarWeapons.Contains(21054) : pools.FourStarWeapons.Contains(21054)), "Wrong cone pool");
        if (b.GachaType == GachaTypeEnum.Normal) Check(pools.FiveStarAvatars.SequenceEqual(GachaRules.StandardAvatars), "Standard pool changed");
    }
    return Task.CompletedTask;
}
static Task Pity()
{
    var m = Player(4002).GachaManager!;
    foreach (var id in new[] { 2116, 3116 })
    {
        var b = Banner(id); var s = new GachaPityState { FiveStarPity = id == 2116 ? 89 : 79 };
        var item = GachaRules.Roll(b, s, m.BuildPools(b), () => .999999);
        Check(m.GetRarity(item) == 5 && s.FiveStarPity == 0, "Hard pity missed");
        s = new(); var rolls = Enumerable.Range(0, 10).Select(_ => GachaRules.Roll(b, s, m.BuildPools(b), () => .999999)).ToList();
        Check(rolls.Take(9).All(x => m.GetRarity(x) == 3) && m.GetRarity(rolls[9]) == 4, "Off-by-one four-star guarantee");
        s = new() { FourStarPity = 9, FiveStarPity = id == 2116 ? 89 : 79 };
        Check(m.GetRarity(GachaRules.Roll(b, s, m.BuildPools(b), () => .999999)) == 5, "Five-star must take precedence");
        Check(m.GetRarity(GachaRules.Roll(b, s, m.BuildPools(b), () => .999999)) == 4, "Deferred four-star lost");
    }
    return Task.CompletedTask;
}
static Task Guarantees()
{
    var m = Player(4003).GachaManager!; var s = new GachaPityState { FiveStarPity = 89 };
    var b = Banner(2116); var other = Banner(2117);
    var lost = GachaRules.Roll(b, s, m.BuildPools(b), () => .999999);
    Check(s.GuaranteedFiveStar && !b.RateUpItems5.Contains(lost), "Loss did not arm guarantee");
    s.FiveStarPity = 89;
    Check(other.RateUpItems5.Contains(GachaRules.Roll(other, s, m.BuildPools(other), () => .999999)) && !s.GuaranteedFiveStar, "Guaranteed rate-up failed");
    s.FourStarPity = 9;
    GachaRules.Roll(b, s, m.BuildPools(b), () => .999999); Check(s.GuaranteedFourStar, "Purple loss not tracked");
    s.FourStarPity = 9;
    Check(other.RateUpItems4.Contains(GachaRules.Roll(other, s, m.BuildPools(other), () => .999999)) && !s.GuaranteedFourStar, "Purple guarantee failed");
    return Task.CompletedTask;
}
static Task Rates()
{
    Check(Math.Abs(GachaRules.FiveStarRate(Banner(2116), 1) - .006) < 1e-8, "Character base rate");
    Check(Math.Abs(GachaRules.FiveStarRate(Banner(3116), 1) - .008) < 1e-8, "Cone base rate");
    Check(GachaRules.FiveStarRate(Banner(2116), 74) > GachaRules.FiveStarRate(Banner(2116), 73), "Character soft pity");
    Check(GachaRules.FiveStarRate(Banner(3116), 64) > GachaRules.FiveStarRate(Banner(3116), 63), "Cone soft pity");
    var m = Player(4016).GachaManager!;
    foreach (var (id, chance) in new[] { (2116, .5), (3116, .75) })
    foreach (var win in new[] { true, false })
    {
        var values = new Queue<double>(new[] { 0d, win ? chance - .00001 : chance, .999999 });
        var s = new GachaPityState(); var b = Banner(id);
        var item = GachaRules.Roll(b, s, m.BuildPools(b), () => values.Dequeue());
        Check(b.RateUpItems5.Contains(item) == win, "Featured rate boundary wrong");
    }
    return Task.CompletedTask;
}
static async Task Rejections()
{
    var p = Player(4004); var m = p.GachaManager!;
    foreach (var count in new[] { -1, 0, 2, 11, int.MaxValue }) Check((await m.DoGacha(2116, count)).Retcode != 0, "Invalid count accepted");
    Check((await m.DoGacha(2116, 1)).Retcode == (uint)Retcode.RetItemNotEnough, "Free pull");
    await Fund(p, 20);
    Check((await m.DoGacha(99999, 1)).Retcode != 0 && (await m.DoGacha(2120, 1)).Retcode == (uint)Retcode.RetGachaNotInSchedule, "Invalid schedule accepted");
    Check(m.GachaData.GachaHistory.Count == 0 && p.InventoryManager!.GetItem(102)!.Count == 20, "Rejected request mutated account");
}
static async Task Groups()
{
    var p = Player(4005); await Fund(p); var m = p.GachaManager!;
    await m.DoGacha(2116, 1); await m.DoGacha(2117, 1);
    await m.DoGacha(3116, 1); await m.DoGacha(1001, 1); await m.DoGacha(5001, 1); await m.DoGacha(6001, 1);
    Check(m.State(Banner(2116)).FiveStarPity == 2 && m.State(Banner(2117)).FiveStarPity == 2, "Character pity not shared");
    Check(new[] { 3116, 1001, 5001, 6001 }.All(id => m.State(Banner(id)).FiveStarPity == 1) && m.State(Banner(4001)).TotalPulls == 0, "Pity leaked across types");
}
static async Task Concurrent()
{
    var p = Player(4006); await Fund(p, 1);
    var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(async () => await p.GachaManager!.DoGacha(2116, 1))));
    Check(results.Count(x => x.Retcode == 0) == 1 && p.GachaManager!.GachaData.GachaHistory.Count == 1 && (p.InventoryManager!.GetItem(102)?.Count ?? 0) == 0, "Ticket double-spent");
}
static async Task Departure()
{
    var p = Player(4007); await Fund(p, 40); var results = new List<GachaItem>();
    Check((await p.GachaManager!.DoGacha(4001, 1)).Retcode != 0, "Departure single accepted");
    for (var i = 0; i < 5; i++) { var r = await p.GachaManager.DoGacha(4001, 10); Check(r.Retcode == 0, "Departure batch failed"); results.AddRange(r.GachaItemList); }
    Check(results.Count(x => p.GachaManager.GetRarity((int)x.GachaItem_.ItemId) == 5) == 1, "Missing departure guarantee");
    Check((p.InventoryManager!.GetItem(101)?.Count ?? 0) == 0 && (await p.GachaManager.DoGacha(4001, 10)).Retcode != 0 &&
        p.GachaManager.ToProto().GachaInfoList.All(x => x.GachaId != 4001), "Departure cap/discount wrong");
}
static Task EarlyDeparture()
{
    var m = Player(4008).GachaManager!; var b = Banner(4001); var s = new GachaPityState();
    GachaRules.Roll(b, s, m.BuildPools(b), () => 0);
    for (var i = 1; i < 50; i++) Check(m.GetRarity(GachaRules.Roll(b, s, m.BuildPools(b), () => .999999)) != 5, "Second departure guarantee");
    return Task.CompletedTask;
}
static async Task Duplicates()
{
    var p = Player(4009, () => 0); await Fund(p, 20); var m = p.GachaManager!; var id = Banner(2116).RateUpItems5.Single();
    var result = await m.DoGacha(2116, 10);
    Check(result.Retcode == 0 && p.AvatarManager!.AvatarData.FormalAvatars.Count(x => x.AvatarId == id) == 1, "Avatar duplicated");
    Check(p.InventoryManager!.GetItem(id + 10000)?.Count == 6 && p.InventoryManager.GetItem(252)?.Count == 540 && p.InventoryManager.GetItem(282)?.Count == 3, "Duplicate tokens wrong");
    p.AvatarManager!.GetFormalAvatar(id)!.PathInfos[id].Rank = 6;
    await p.InventoryManager.RemoveItem(id + 10000, 6);
    var again = await m.DoGacha(2116, 1);
    Check(again.GachaItemList.Single().TransferItemList.ItemList_.Count == 0 && p.InventoryManager.GetItem(252)?.Count == 640 && p.InventoryManager.GetItem(282)?.Count == 4, "E6/null token conversion wrong");
}
static async Task ConeTokens()
{
    var p = Player(4010, () => 0); await Fund(p, 2);
    var r = await p.GachaManager!.DoGacha(3116, 1);
    Check(r.Retcode == 0 && p.InventoryManager!.GetItem(252)?.Count == 40, "Five-star cone compensation wrong");
    var q = Player(4017, () => .02); await Fund(q, 1);
    r = await q.GachaManager!.DoGacha(3116, 1);
    Check(q.GachaManager.GetRarity((int)r.GachaItemList.Single().GachaItem_.ItemId) == 4 && q.InventoryManager!.GetItem(252)?.Count == 8, "Four-star compensation wrong");
}
static async Task Selection()
{
    var p = Player(4011); var m = p.GachaManager!; var selected = new[] { 1102, 1205, 1208, 1006, 1302, 1221, 1003 };
    Check(GameData.DecideAvatarOrderData.Count == 13 && await m.SetDecideItems(2116, selected) == Retcode.RetSucc, "Valid 7/13 rejected");
    Check(await m.SetDecideItems(1001, selected) != Retcode.RetSucc && await m.SetDecideItems(2116, selected.Take(6).ToList()) != Retcode.RetSucc &&
        await m.SetDecideItems(2116, Enumerable.Repeat(1003, 7).ToList()) != Retcode.RetSucc, "Invalid selection accepted");
    Check(m.BuildPools(Banner(2116)).FiveStarAvatars.SequenceEqual(selected) && !m.BuildPools(Banner(1001)).FiveStarAvatars.Contains(1102), "Selection leaked to standard");
    Check(m.ToProto().NMBAAOBBJMI.Any(x => x.DGOMHDMJHEK.SequenceEqual(selected.Select(i => (uint)i))), "Selection not synchronized");
}
static async Task Ceiling()
{
    var p = Player(4012); var m = p.GachaManager!;
    Check((await m.ExchangeCeiling(2, 1003)).Retcode == (uint)Retcode.RetGachaCeilingNotEnough, "Early selector");
    m.State(Banner(1001)).TotalPulls = 300;
    var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () => await m.ExchangeCeiling(2, 1003))));
    Check(results.Count(x => x.Retcode == 0) == 1 && m.Ceiling().IsClaimed && p.AvatarManager!.GetFormalAvatar(1003) != null, "Selector repeated");
}
static async Task Reload()
{
    var p = Player(4013); await Fund(p, 100); var m = p.GachaManager!;
    for (var i = 0; i < 6; i++) Check((await m.DoGacha(1001, 10)).Retcode == 0, "History setup failed");
    m.State(Banner(2116)).GuaranteedFiveStar = true; m.State(Banner(2116)).FiveStarPity = 45;
    m.State(Banner(3116)).FourStarPity = 7; DatabaseHelper.Instance!.SaveUidData(p.Uid);
    DatabaseHelper.UidInstanceMap.TryRemove(p.Uid, out _); p = Player(4013); m = p.GachaManager!;
    Check(m.GachaData.GachaHistory.Count == 60 && m.Ceiling().CeilingNum == 60 && m.State(Banner(2116)).FiveStarPity == 45 &&
        m.State(Banner(2116)).GuaranteedFiveStar && m.State(Banner(3116)).FourStarPity == 7, "Reload lost ledger");
}
static Task Migration()
{
    var data = DatabaseHelper.Instance!.GetInstanceOrCreateNew<GachaData>(4014);
    data.LastGachaFailedCount = 47; data.LastGachaPurpleFailedCount = 6; data.LastAvatarGachaFailed = true;
    data.GachaHistory.Add(new March7thHoney.Database.Gacha.GachaInfo { GachaId = 3116, ItemId = 20000 });
    var m = Player(4014).GachaManager!;
    Check(m.State(Banner(3116)).FiveStarPity == 47 && m.State(Banner(2116)).FiveStarPity == 0 && m.State(Banner(2116)).GuaranteedFiveStar, "Migration duplicated pity");
    m.State(Banner(3116)).FiveStarPity = 3; DatabaseHelper.Instance.SaveUidData(4014);
    DatabaseHelper.UidInstanceMap.TryRemove(4014, out _); m = Player(4014).GachaManager!;
    Check(m.State(Banner(3116)).FiveStarPity == 3, "Migration ran twice"); return Task.CompletedTask;
}
static async Task Capacity()
{
    var p = Player(4015); await Fund(p, 1);
    for (var i = 0; i < GameConstants.INVENTORY_MAX_EQUIPMENT; i++) p.InventoryManager!.Data.EquipmentItems.Add(new() { ItemId = 20000, UniqueId = i });
    Check((await p.GachaManager!.DoGacha(2116, 1)).Retcode == (uint)Retcode.RetEquipmentExceedLimit && p.InventoryManager!.GetItem(102)?.Count == 1, "Capacity lost pull");
}

static Task Fallback()
{
    var m = Player(4018).GachaManager!;
    foreach (var id in new[] { 2116, 3116 })
    {
        var b = Banner(id); var s = new GachaPityState();
        var values = new Queue<double>(new[] { 0d, .99, 0d });
        var item = GachaRules.Roll(b, s, m.BuildPools(b), () => values.Dequeue());
        Check(b.RateUpItems5.Contains(item) && !s.GuaranteedFiveStar, "Fallback featured did not clear guarantee");
    }
    return Task.CompletedTask;
}
