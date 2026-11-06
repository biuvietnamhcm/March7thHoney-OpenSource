using March7thHoney.Data;
using March7thHoney.Data.Custom;
using March7thHoney.Database;
using March7thHoney.Database.Gacha;
using March7thHoney.Database.Inventory;
using March7thHoney.Enums;
using March7thHoney.Enums.Item;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Server.Packet.Send.PlayerSync;
using March7thHoney.Proto;
using March7thHoney.Util;

namespace March7thHoney.GameServer.Game.Gacha;

public class GachaManager : BasePlayerManager
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<double> _random;
    private readonly Func<long> _clock;
    public GachaData GachaData { get; }

    public GachaManager(PlayerInstance player, Func<double>? random = null, Func<long>? clock = null) : base(player)
    {
        _random = random ?? Random.Shared.NextDouble;
        _clock = clock ?? ServerTimeProvider.GetServerUnixSec;
        GachaData = DatabaseHelper.Instance!.GetInstanceOrCreateNew<GachaData>(player.Uid);
        var allowed = GameData.DecideAvatarOrderData.Keys.ToHashSet();
        GachaData.GachaDecideOrder = GachaData.GachaDecideOrder.Where(allowed.Contains).Distinct()
            .Concat(GameData.DecideAvatarOrderData.Values.OrderByDescending(x => x.Order).Select(x => x.ItemID))
            .Distinct().ToList();
        if (!GachaData.PityMigrated)
        {
            // Old releases shared one counter. Attribute it only to the last recorded banner;
            // never duplicate it into every pool. Old flags remain available for audit.
            var lastId = GachaData.GachaHistory.LastOrDefault()?.GachaId;
            var last = GameData.BannersConfig.Banners.Find(x => x.GachaId == lastId);
            if (last != null)
            {
                var state = State(last);
                state.FiveStarPity = Math.Clamp(GachaData.LastGachaFailedCount, 0, 89);
                state.FourStarPity = Math.Clamp(GachaData.LastGachaPurpleFailedCount, 0, 10);
            }
            foreach (var banner in GameData.BannersConfig.Banners)
            {
                var state = State(banner);
                if (GachaRules.Group(banner) == "character") state.GuaranteedFiveStar = GachaData.LastAvatarGachaFailed;
                if (GachaRules.Group(banner) == "lightcone") state.GuaranteedFiveStar = GachaData.LastWeaponGachaFailed;
                state.TotalPulls = GachaData.GachaHistory.Count(h => GameData.BannersConfig.Banners
                    .Any(b => b.GachaId == h.GachaId && GachaRules.Group(b) == GachaRules.Group(banner)));
                state.ReceivedFiveStar = GachaData.GachaHistory.Any(h => h.GachaId == banner.GachaId && GetRarity(h.ItemId) == 5);
            }
            GachaData.PityMigrated = true;
            DatabaseHelper.Instance.SaveUidData(Player.Uid);
        }
    }

    public GachaPityState State(BannerConfig banner)
    {
        var key = GachaRules.Group(banner);
        if (!GachaData.PityGroups.TryGetValue(key, out var state)) GachaData.PityGroups[key] = state = new();
        return state;
    }

    public List<int> GetGoldAvatars() => GachaRules.StandardAvatars.ToList();
    public int GetRarity(int id) => GameData.AvatarConfigData.TryGetValue(id, out var avatar)
        ? avatar.Rarity == RarityEnum.CombatPowerAvatarRarityType5 ? 5 : 4
        : GameData.EquipmentConfigData.TryGetValue(id, out var cone)
            ? cone.Rarity == RarityEnum.CombatPowerLightconeRarity5 ? 5
                : cone.Rarity == RarityEnum.CombatPowerLightconeRarity4 ? 4 : 3 : 0;

    public bool IsActive(BannerConfig banner) => (banner.BeginTime == 0 || _clock() >= banner.BeginTime) &&
        (banner.EndTime == 0 || _clock() <= banner.EndTime) &&
        (banner.GachaType != GachaTypeEnum.Newbie || State(banner).TotalPulls < 50);

    public GachaPools BuildPools(BannerConfig banner)
    {
        bool Valid(int id, int rarity) => GetRarity(id) == rarity && GameData.ItemConfigData.ContainsKey(id);
        var eventBanner = GachaRules.IsEvent(banner);
        var five = banner.RateUpItems5.Where(id => Valid(id, 5) &&
            (GachaRules.IsWeapon(banner) ? GameData.EquipmentConfigData.ContainsKey(id) : GameData.AvatarConfigData.ContainsKey(id))).Distinct().ToList();
        var four = banner.RateUpItems4.Where(id => Valid(id, 4)).Distinct().ToList();
        var goldAvatars = banner.GachaType == GachaTypeEnum.AvatarUp ? GachaData.GachaDecideOrder.Take(7) : GachaRules.StandardAvatars;
        return new(goldAvatars.Where(id => Valid(id, 5)).ToList(),
            GachaRules.StandardLightCones.Where(id => Valid(id, 5)).ToList(),
            GameData.AvatarConfigData.Values.Where(x => x.Rarity == RarityEnum.CombatPowerAvatarRarityType4 &&
                x.MaxRank > 0 && (!GameData.MultiplePathAvatarConfigData.TryGetValue(x.AvatarID, out var path) || path.BaseAvatarID == x.AvatarID))
                .Select(x => x.AvatarID).Where(id => Valid(id, 4) && (!eventBanner || !four.Contains(id)) &&
                    (banner.GachaType != GachaTypeEnum.Newbie || GachaRules.DepartureFourStarAvatars.Contains(id))).ToList(),
            GachaRules.FourStarLightCones.Where(id => Valid(id, 4) && (!eventBanner || !four.Contains(id)) &&
                (banner.GachaType != GachaTypeEnum.Newbie || id <= 21013)).ToList(),
            GameData.EquipmentConfigData.Values.Where(x => x.Rarity == RarityEnum.CombatPowerLightconeRarity3)
                .Select(x => x.EquipmentID).Where(id => Valid(id, 3)).ToList(), five, four);
    }

    public async ValueTask<DoGachaScRsp> DoGacha(int bannerId, int times)
    {
        await _gate.WaitAsync();
        try
        {
            DoGachaScRsp Error(Retcode code) => new() { Retcode = (uint)code, GachaId = (uint)bannerId };
            var banner = GameData.BannersConfig.Banners.Find(x => x.GachaId == bannerId);
            if (banner == null) return Error(Retcode.RetGachaIdNotExist);
            if (times is not (1 or 10) || (banner.GachaType == GachaTypeEnum.Newbie && times != 10)) return Error(Retcode.RetGachaNumInvalid);
            if (banner.GachaType == GachaTypeEnum.Newbie && State(banner).TotalPulls + times > 50) return Error(Retcode.RetGachaNewbieClose);
            if (!IsActive(banner)) return Error(Retcode.RetGachaNotInSchedule);
            var cost = banner.GachaType == GachaTypeEnum.Newbie ? 8 : times;
            var ticket = banner.GachaType.GetCostItemId();
            if (ticket == 0 || Player.InventoryManager == null || Player.AvatarManager == null) return Error(Retcode.RetGachaNotSupport);
            if ((Player.InventoryManager.GetItem(ticket)?.Count ?? 0) < cost) return Error(Retcode.RetItemNotEnough);
            if (Player.InventoryManager.Data.EquipmentItems.Count + times > GameConstants.INVENTORY_MAX_EQUIPMENT)
                return Error(Retcode.RetEquipmentExceedLimit);
            var pools = BuildPools(banner);
            if (pools.ThreeStar.Count == 0 || pools.FourStarAvatars.Count == 0 || pools.FourStarWeapons.Count == 0 ||
                pools.FiveStarAvatars.Count == 0 || pools.FiveStarWeapons.Count == 0 ||
                (GachaRules.IsEvent(banner) && pools.FeaturedFive.Count == 0)) return Error(Retcode.RetGachaNotSupport);
            var rsp = new DoGachaScRsp { GachaId = (uint)bannerId, GachaNum = (uint)times };
            await Player.InventoryManager.RemoveItem(ticket, cost);
            try
            {
                // Grant in pull order so duplicate conversion also works within a ten-pull.
                for (var i = 0; i < times; i++)
                {
                    var id = GachaRules.Roll(banner, State(banner), pools, _random);
                    rsp.GachaItemList.Add(await GrantItem(id));
                    GachaData.GachaHistory.Add(new March7thHoney.Database.Gacha.GachaInfo
                        { GachaId = bannerId, ItemId = id, Time = _clock() });
                }
            }
            finally { DatabaseHelper.Instance!.SaveUidData(Player.Uid); }
            return rsp;
        }
        finally { _gate.Release(); }
    }

    private async ValueTask<GachaItem> GrantItem(int id)
    {
        var rarity = GetRarity(id);
        var rsp = new GachaItem { GachaItem_ = new Item { ItemId = (uint)id, Num = 1, Level = 1, Rank = 1 },
            TransferItemList = new(), TokenItem = new() };
        var starlight = 0;
        if (GameData.AvatarConfigData.ContainsKey(id))
        {
            var avatar = Player.AvatarManager!.GetFormalAvatar(id);
            if (avatar == null) await Player.AvatarManager.AddAvatar(id, isGacha: true);
            else
            {
                var rank = avatar.PathInfos.GetValueOrDefault(id)?.Rank ?? 0;
                var tokens = Player.InventoryManager!.GetItem(id + 10000)?.Count ?? 0;
                var capped = rank + tokens >= 6;
                starlight = rarity == 5 ? capped ? 100 : 40 : capped ? 20 : 8;
                if (rarity == 5 && capped)
                {
                    await Player.InventoryManager.AddItem(282, 1, false);
                    rsp.TokenItem.ItemList_.Add(new Item { ItemId = 282, Num = 1 });
                }
                if (!capped)
                {
                    await Player.InventoryManager.AddItem(id + 10000, 1, false);
                    rsp.TransferItemList.ItemList_.Add(new Item { ItemId = (uint)(id + 10000), Num = 1 });
                }
            }
        }
        else
        {
            await Player.InventoryManager!.AddItem(id, 1, false);
            starlight = rarity == 5 ? 40 : rarity == 4 ? 8 : 0;
        }
        if (starlight > 0)
        {
            await Player.InventoryManager!.AddItem(252, starlight, false);
            rsp.TokenItem.ItemList_.Add(new Item { ItemId = 252, Num = (uint)starlight });
        }
        if (rarity == 3)
        {
            await Player.InventoryManager!.AddItem(251, 20, false);
            rsp.TokenItem.ItemList_.Add(new Item { ItemId = 251, Num = 20 });
        }
        return rsp;
    }

    public GachaCeiling Ceiling() => new()
    {
        CeilingNum = (uint)(GachaData.PityGroups.GetValueOrDefault("standard")?.TotalPulls ?? 0),
        IsClaimed = GachaData.StandardCeilingClaimed,
        AvatarList = { GachaRules.StandardAvatars.Select(id => new GachaCeilingAvatar { AvatarId = (uint)id }) }
    };

    public async ValueTask<ExchangeGachaCeiling> ExchangeCeiling(uint type, int avatarId)
    {
        await _gate.WaitAsync();
        try
        {
            var result = new ExchangeGachaCeiling { GachaType = type, AvatarId = (uint)avatarId, GachaCeiling = Ceiling(), TransferItemList = new() };
            if (type != (uint)GachaTypeEnum.Normal || !GachaRules.StandardAvatars.Contains(avatarId)) result.Retcode = (uint)Retcode.RetGachaNotSupport;
            else if (GachaData.StandardCeilingClaimed) result.Retcode = (uint)Retcode.RetGachaCeilingClose;
            else if (Ceiling().CeilingNum < 300) result.Retcode = (uint)Retcode.RetGachaCeilingNotEnough;
            else if (!GameData.ItemConfigData.ContainsKey(avatarId) || Player.AvatarManager == null || Player.InventoryManager == null) result.Retcode = (uint)Retcode.RetFail;
            else
            {
                GachaData.StandardCeilingClaimed = true;
                try { result.TransferItemList = (await GrantItem(avatarId)).TransferItemList; }
                finally { DatabaseHelper.Instance!.SaveUidData(Player.Uid); }
                result.GachaCeiling = Ceiling();
            }
            return result;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask<Retcode> SetDecideItems(int bannerId, IReadOnlyList<int> ids)
    {
        await _gate.WaitAsync();
        try
        {
            var banner = GameData.BannersConfig.Banners.Find(x => x.GachaId == bannerId);
            if (banner?.GachaType != GachaTypeEnum.AvatarUp) return Retcode.RetGachaDecideItemTypeInvalid;
            if (ids.Count != 7 || ids.Distinct().Count() != 7 || ids.Any(id => !GameData.DecideAvatarOrderData.ContainsKey(id)))
                return Retcode.RetGachaDecideItemIdInvalid;
            GachaData.GachaDecideOrder = ids.Concat(GameData.DecideAvatarOrderData.Values.OrderByDescending(x => x.Order)
                .Select(x => x.ItemID).Except(ids)).ToList();
            DatabaseHelper.Instance!.SaveUidData(Player.Uid);
            return Retcode.RetSucc;
        }
        finally { _gate.Release(); }
    }

    public GetGachaInfoScRsp ToProto()
    {
        var result = new GetGachaInfoScRsp { GachaRandom = (uint)Random.Shared.Next(1000, 1999) };
        foreach (var banner in GameData.BannersConfig.Banners.Where(IsActive))
        {
            var info = banner.ToInfo(GachaData.GachaDecideOrder, GetGoldAvatars());
            info.CBHAEFNAFLE.Clear(); info.CBHAEFNAFLE.AddRange(BuildPools(banner).FeaturedFour.Select(x => (uint)x));
            if (banner.GachaType == GachaTypeEnum.Normal) info.GachaCeiling = Ceiling();
            result.GachaInfoList.Add(info);
            if (banner.GachaType == GachaTypeEnum.AvatarUp)
                result.NMBAAOBBJMI.Add(new OEIEJHBCOOM { PAPOKACIPPJ = (uint)banner.GachaId, MBOEFLAHLEM = 1,
                    DGOMHDMJHEK = { GachaData.GachaDecideOrder.Take(7).Select(x => (uint)x) } });
        }
        return result;
    }
}
