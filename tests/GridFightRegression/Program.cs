using Google.Protobuf;
using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.Database.Player;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Game.GridFight.Battle;
using March7thHoney.GameServer.Game.GridFight.Sync;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Server.Packet;
using March7thHoney.GameServer.Server.Packet.Recv.GridFight;
using March7thHoney.Kcp;
using March7thHoney.Proto;

// In-memory fixtures only: no server entrypoint, listeners, resources or user database.
var tests = new (string Name, Action Run)[]
{
    ("Level-based deployment limits and equipment bonus", DeploymentLimits),
    ("Internal role IDs and avatar IDs resolve consistently", RoleLookup),
    ("Position swaps sync both affected roles", PositionSwap),
    ("Invalid/unknown/duplicate-avatar moves are atomic", RejectedMoves),
    ("Deployment capacity cannot be bypassed", DeploymentCapacity),
    ("Nine copies cascade into one three-star role", CascadeMerge),
    ("Full bench purchase merges and retries without double charge", FullBenchPurchase),
    ("Full bench rejects purchases that cannot merge", FullBenchReject),
    ("Insufficient gold and invalid indexes preserve inventory", InvalidPurchase),
    ("Projector respects rarity and creates a one-star copy", Projector),
    ("Shop excludes owned three-star roles including exhausted pools", ShopExclusion),
    ("Shop fallback rarity displays the charged price", ShopFallbackPrice),
    ("Shop probability payload uses level configuration", ShopProbabilities),
    ("Merge and route notifications round-trip through protobuf", SyncRoundTrip),
    ("Packet registry includes post-query sync without collisions", PacketRegistration)
};
var failed = 0;
foreach (var test in tests)
{
    ResetData();
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} scenarios passed.");
return failed == 0 ? 0 : 1;

static void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

static void Equal<T>(T expected, T actual, string message) =>
    Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{message}: expected {expected}, got {actual}");

static void ResetData()
{
    GameData.GridFightRoleBasicInfoData.Clear();
    GameData.GridFightRoleStarData.Clear();
    GameData.GridFightPlayerLevelData.Clear();
    GameData.GridFightShopPriceData.Clear();
    GameData.GridFightPlayerLevelData[3] = new() { PlayerLevel = 3, AvatarMaxNumber = 4, Rarity1Weight = 100 };
    GameData.GridFightShopPriceData[1] = new() { Rarity = 1, BuyGoldStar1 = 1, BuyGoldStar2 = 3, BuyGoldStar3 = 9 };
    GameData.GridFightShopPriceData[5] = new() { Rarity = 5, BuyGoldStar1 = 5, BuyGoldStar2 = 15, BuyGoldStar3 = 45 };
    for (uint i = 1; i <= 12; i++)
        GameData.GridFightRoleBasicInfoData[10000 + i] = new()
        { ID = 10000 + i, AvatarID = 1000 + i, Rarity = 1, SeasonID = 1, IsInPool = true };
}

static (GridFightInstance Inst, GridFightService Service, GridFightManager Manager) Fixture()
{
    var player = new PlayerInstance(new PlayerData { Uid = 999999, Name = "Regression fixture" });
    var inst = new GridFightInstance(player, 1, 10940, false, 1) { Gold = 100 };
    var manager = new GridFightManager(player) { GridFightInstance = inst };
    // Assign only the manager under test; avoid InitialPlayerManager's database side effects.
    typeof(PlayerInstance).GetProperty(nameof(PlayerInstance.GridFightManager))!.SetValue(player, manager);
    return (inst, new GridFightService(player), manager);
}

static uint Add(GridFightInstance inst, uint role, uint pos, uint star = 1)
{
    var uid = inst.NextRoleUniqueId++;
    inst.RoleByUniqueId[uid] = role;
    inst.RoleStarByUniqueId[uid] = star;
    inst.UniqueIdByPos[pos] = uid;
    return uid;
}

static void Offer(GridFightInstance inst, uint role = 10001, uint star = 1)
{
    inst.ShopGoods.Clear();
    inst.ShopGoods.Add(new() { ShopGoodsPrice = 1, RoleGoodsInfo = new() { RoleId = role, RoleStar = star } });
}

static string Layout(GridFightInstance inst) =>
    string.Join(",", inst.UniqueIdByPos.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}"));

static void DeploymentLimits()
{
    var (inst, _, _) = Fixture();
    Equal(4u, inst.GetCurrentMaxBattleRoleNum(), "Starting cap");
    Equal(0u, inst.GetCurrentOffFieldMaxCount(), "Starting background slots");
    inst.Equipments.Add(new() { GridFightEquipmentId = GridFightInstance.WealthGemEquipmentId });
    Equal(5u, inst.GetCurrentMaxBattleRoleNum(), "Equipment cap bonus");
    Equal(1u, inst.GetCurrentOffFieldMaxCount(), "Background slots after bonus");
    GameData.GridFightPlayerLevelData[3].AvatarMaxNumber = uint.MaxValue;
    Equal(13u, inst.GetCurrentMaxBattleRoleNum(), "Hard cap with overflowing resource value");
    inst.Equipments.Clear();
    inst.PlayerLevel = 999;
    Equal(4u, inst.GetCurrentMaxBattleRoleNum(), "Missing resource fallback");
}

static void RoleLookup()
{
    Equal(10001u, GridFightRoleLookup.ToRoleId(1001), "Avatar to role");
    Equal(1001u, GridFightRoleLookup.ToAvatarId(10001), "Role to avatar");
    Equal(10001u, GridFightRoleLookup.ToSyncRoleId(1001), "Sync uses internal ID");
    Check(GridFightRoleLookup.Find(0) == null, "Zero role must not resolve");
}

static void PositionSwap()
{
    var (inst, service, _) = Fixture();
    var a = Add(inst, 10001, 1);
    var b = Add(inst, 10002, 14);
    var result = service.UpdatePos([new() { Pos = 14, UniqueId = a }]);
    Equal(Retcode.RetSucc, result.retcode, "Swap result");
    Equal(a, inst.UniqueIdByPos[14], "Moved role");
    Equal(b, inst.UniqueIdByPos[1], "Displaced role");
    Equal(2, result.syncPosList.Count, "Both roles synced");
    Equal(2, inst.UniqueIdByPos.Values.Distinct().Count(), "No ghost duplicate position");
}

static void RejectedMoves()
{
    var (inst, service, _) = Fixture();
    Add(inst, 10001, 1);
    var duplicate = Add(inst, 1001, 14);
    var snapshot = Layout(inst);
    Equal(Retcode.RetGridFightSameRoleInBattle,
        service.UpdatePos([new() { Pos = 2, UniqueId = duplicate }]).retcode, "Duplicate avatar");
    foreach (var pos in new uint[] { 0, 23, uint.MaxValue })
        Equal(Retcode.RetGridFightPosNotValid,
            service.UpdatePos([new() { Pos = pos, UniqueId = duplicate }]).retcode, "Invalid position");
    Equal(Retcode.RetGridFightRoleNotExist,
        service.UpdatePos([new() { Pos = 14, UniqueId = 9999 }]).retcode, "Unknown role");
    Equal(snapshot, Layout(inst), "Rejected moves preserve layout");
}

static void DeploymentCapacity()
{
    var (inst, service, _) = Fixture();
    for (uint i = 1; i <= 4; i++) Add(inst, 10000 + i, i);
    var uid = Add(inst, 10005, 14);
    var snapshot = Layout(inst);
    Equal(Retcode.RetGridFightRegionFull,
        service.UpdatePos([new() { Pos = 5, UniqueId = uid }]).retcode, "Fifth role rejected");
    Equal(snapshot, Layout(inst), "Rejected capacity change is atomic");
}

static void CascadeMerge()
{
    var (inst, _, _) = Fixture();
    for (uint pos = 14; pos <= 22; pos++) Add(inst, pos % 2 == 0 ? 10001u : 1001u, pos);
    var merges = inst.TryAutoMergeAllRoles();
    Equal(4, merges.Count, "Three two-star merges then a three-star merge");
    Equal(1, inst.RoleByUniqueId.Count, "Only survivor remains");
    Equal(3u, inst.RoleStarByUniqueId.Values.Single(), "Final star");
    Equal(1, inst.UniqueIdByPos.Count, "Consumed positions removed");
}

static void FullBenchPurchase()
{
    var (inst, service, _) = Fixture();
    Add(inst, 10001, 14); Add(inst, 10001, 15);
    for (uint pos = 16; pos <= 22; pos++) Add(inst, 10000 + pos - 14, pos);
    Offer(inst);
    var result = service.BuyGoods(new uint[] { 0 });
    Equal(Retcode.RetSucc, result.retcode, "Merge purchase with full bench");
    Equal(99u, inst.Gold, "Charged once");
    Equal(1, result.merges.Count, "Purchase merged");
    Equal(8, inst.RoleByUniqueId.Count, "Ten cards reduced by two");
    Check(inst.UniqueIdByPos.Keys.All(p => p <= 22), "Overflow slot reclaimed");
    Equal(inst.RoleByUniqueId.Count, inst.UniqueIdByPos.Values.Distinct().Count(), "Every role placed");
    var snapshot = Layout(inst);
    var retry = service.BuyGoods(new uint[] { 0 });
    Check(retry.duplicateRetry, "Sold-out retry is recognized");
    Equal(99u, inst.Gold, "Retry does not charge");
    Equal(snapshot, Layout(inst), "Retry does not duplicate roles");
}

static void FullBenchReject()
{
    var (inst, _, _) = Fixture();
    for (uint pos = 14; pos <= 22; pos++) Add(inst, 10000 + pos - 13, pos, pos < 16 ? 3u : 1u);
    Offer(inst, 10012);
    Equal(Retcode.RetGridFightNoEmptyPos, inst.TryBuyGoods(0).retcode, "Full bench purchase");
    Equal(100u, inst.Gold, "Rejected purchase cost");
    Check(!inst.ShopGoods[0].IsSoldOut, "Rejected goods still available");
    // Two maximum-star copies must not authorize a temporary overflow slot.
    inst.RoleByUniqueId[inst.UniqueIdByPos[15]] = 10001;
    Offer(inst, 10001, 3);
    Equal(Retcode.RetGridFightNoEmptyPos, inst.TryBuyGoods(0).retcode, "Cannot merge past maximum star");
}

static void InvalidPurchase()
{
    var (inst, _, _) = Fixture();
    Offer(inst);
    Equal(Retcode.RetGridFightParamNotMatch, inst.TryBuyGoods(-1).retcode, "Negative index");
    Equal(Retcode.RetGridFightParamNotMatch, inst.TryBuyGoods(1).retcode, "Missing index");
    inst.Gold = 0;
    Equal(Retcode.RetGridFightCoinNotEnough, inst.TryBuyGoods(0).retcode, "Insufficient gold");
    Equal(0, inst.RoleByUniqueId.Count, "No role granted");
    Check(!inst.ShopGoods[0].IsSoldOut, "No sold-out mutation");
}

static void Projector()
{
    var (inst, _, _) = Fixture();
    GameData.GridFightRoleBasicInfoData[10001].Rarity = 5;
    var uid = Add(inst, 10001, 1, 3);
    Check(!inst.TryCopyRole(uid, 3).ok, "Low rarity projector rejects five-cost role");
    var copy = inst.TryCopyRole(uid, 5);
    Check(copy.ok, "High rarity projector accepts role");
    Equal(1u, inst.RoleStarByUniqueId[copy.newUniqueId], "Copy starts at one star");
    Equal(14u, copy.targetPos, "Copy placed on bench");
    Check(!inst.TryCopyRole(9999, 5).ok, "Unknown source rejected");
}

static void ShopExclusion()
{
    var (inst, _, _) = Fixture();
    Add(inst, 10001, 14, 3);
    for (var i = 0; i < 100; i++)
    {
        inst.RotateShop();
        Equal(5, inst.ShopGoods.Count, "Shop size with available candidates");
        Check(inst.ShopGoods.All(g => g.RoleGoodsInfo.RoleId != 10001), "Owned three-star excluded");
    }
    foreach (var role in GameData.GridFightRoleBasicInfoData.Values) role.IsInPool = role.ID == 10001;
    inst.RotateShop();
    Equal(0, inst.ShopGoods.Count, "Exhausted pool does not reintroduce excluded role");
}

static void ShopFallbackPrice()
{
    var (inst, _, _) = Fixture();
    foreach (var role in GameData.GridFightRoleBasicInfoData.Values) role.IsInPool = role.ID == 10001;
    GameData.GridFightRoleBasicInfoData[10001].Rarity = 5;
    inst.RotateShop(); // Level rolls rarity 1, but only rarity 5 exists in this fixture.
    Equal(5u, inst.ShopGoods[0].ShopGoodsPrice, "Fallback display price");
    var before = inst.Gold;
    Check(inst.TryBuyGoods(0).ok, "Fallback purchase");
    Equal(inst.ShopGoods[0].ShopGoodsPrice, before - inst.Gold, "Display equals charged amount");
}

static void ShopProbabilities()
{
    var config = GameData.GridFightPlayerLevelData[3];
    config.Rarity1Weight = 50; config.Rarity2Weight = 30; config.Rarity3Weight = 20;
    var info = GridFightInstance.BuildShopRarityDisplayInfoForLevel(3);
    Check(info.EDJPMNLLGGB.Select(x => x.FLICPMGFKOK).SequenceEqual(new uint[] { 50, 30, 20, 0, 0 }),
        "Displayed rarity weights match level config");
}

static void SyncRoundTrip()
{
    var (inst, _, manager) = Fixture();
    for (uint pos = 14; pos <= 16; pos++) Add(inst, 10001, pos);
    var merge = inst.TryAutoMergeAllRoles().Single();
    foreach (var packet in new[]
    {
        manager.BuildRoleMergeAnimationNotify(merge, GridFightUpdateSrcType.LnpfefkjdhpKkpbagfhpbb),
        manager.BuildRoleMergeFinalizeNotify(merge),
        GridFightRouteSyncBuilder.BuildLevelLayerSync(inst)
    })
    {
        Check(packet.SyncResultDataList.Count > 0, "Sync has payload");
        Equal(packet, GridFightSyncUpdateResultScNotify.Parser.ParseFrom(packet.ToByteArray()), "Wire round trip");
    }
    Equal(3, merge.PreMergeRoleSnapshots.Count, "Animation captures participants before deletion");
    Equal(2u, merge.FinalKeeperRoleInfo!.RoleStar, "Final sync includes promoted star");
}

static void PacketRegistration()
{
    HandlerManager.Init();
    Check(HandlerManager.GetHandler(CmdIds.GridFightGetDataPostSyncCsReq) is HandlerGridFightGetDataPostSyncCsReq,
        "Post-query handler must be registered");
}
