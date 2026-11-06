using March7thHoney.Database.Gacha;
using March7thHoney.Enums;

namespace March7thHoney.Data.Custom;

// Rates below are probabilities (0..1). Soft-pity ramps are an approximation,
// not a published HoYoverse server algorithm. Keep the hard guarantees explicit.
public static class GachaRules
{
    public static readonly int[] StandardAvatars = [1003, 1004, 1101, 1104, 1107, 1209, 1211];
    public static readonly int[] StandardLightCones = [23000, 23002, 23003, 23004, 23005, 23012, 23013];
    public static readonly int[] DepartureFourStarAvatars = [1001, 1002, 1008, 1009, 1103, 1105, 1106, 1108, 1109, 1110, 1201, 1202, 1206];
    // Warp-only 4-star cones through 4.2: exclude Battle Pass, Herta, event and shop cones.
    public static readonly int[] FourStarLightCones = [21000, 21001, 21002, 21003, 21004, 21005, 21006,
        21007, 21008, 21009, 21010, 21011, 21012, 21013, 21042, 21043, 21044,
        21045, 21046, 21047, 21048, 21051, 21054];

    public static bool IsWeapon(BannerConfig banner) => banner.GachaType == GachaTypeEnum.WeaponUp;
    public static bool IsEvent(BannerConfig banner) => banner.GachaType is GachaTypeEnum.AvatarUp or GachaTypeEnum.WeaponUp;
    public static string Group(BannerConfig banner) => banner.PityGroup ?? (banner.GachaType switch
    {
        GachaTypeEnum.Normal => "standard",
        GachaTypeEnum.Newbie => "departure",
        GachaTypeEnum.AvatarUp => banner.GachaId is >= 5000 and < 6000 ? "collaboration-character" : "character",
        GachaTypeEnum.WeaponUp => banner.GachaId is >= 6000 and < 7000 ? "collaboration-lightcone" : "lightcone",
        _ => "unsupported"
    });

    public static double FiveStarRate(BannerConfig banner, int nextPull)
    {
        var weapon = IsWeapon(banner);
        if (nextPull >= (banner.MaxCount ?? (weapon ? 80 : 90))) return 1;
        var rate = (banner.GetRateUpItem5Chance ?? (weapon ? 8 : 6)) / 1000d;
        if (banner.UseSoftPity) rate += Math.Max(0, nextPull - (weapon ? 63 : 73)) * (weapon ? .07 : .06);
        return Math.Clamp(rate, 0, 1);
    }

    public static int Roll(BannerConfig banner, GachaPityState state, GachaPools pools, Func<double> random)
    {
        int Pick(IReadOnlyList<int> values) => values[Math.Min(values.Count - 1, (int)(random() * values.Count))];
        int Either(IReadOnlyList<int> avatars, IReadOnlyList<int> weapons) => avatars.Count == 0 ? Pick(weapons)
            : weapons.Count == 0 ? Pick(avatars) : random() < .5 ? Pick(avatars) : Pick(weapons);
        state.TotalPulls++;
        state.FiveStarPity++;
        state.FourStarPity++;
        var newbie = banner.GachaType == GachaTypeEnum.Newbie;
        var rarityRoll = random();
        var fiveRate = FiveStarRate(banner, state.FiveStarPity);
        var five = (newbie && state.TotalPulls == 50 && !state.ReceivedFiveStar) ||
            rarityRoll < fiveRate;
        var four = !five && (state.FourStarPity >= 10 || rarityRoll < fiveRate + (IsWeapon(banner) ? .066 : .051));
        if (!five && !four) return Pick(pools.ThreeStar);

        int item;
        if (five)
        {
            state.FiveStarPity = 0;
            state.ReceivedFiveStar = true;
            if (newbie) return Pick(pools.FiveStarAvatars);
            if (!IsEvent(banner)) return Either(pools.FiveStarAvatars, pools.FiveStarWeapons);
            var up = state.GuaranteedFiveStar || random() < (banner.EventChance ?? (IsWeapon(banner) ? 75 : 50)) / 100d;
            var fallback = IsWeapon(banner) ? pools.FiveStarWeapons : pools.FiveStarAvatars;
            // Keep duplicate entries if the featured character is also selected in Celestial Invitation.
            // An eight-entry fallback matches the 4.2 client's consolidated featured rates (1.113/1.534%).
            item = up ? Pick(pools.FeaturedFive) : Pick(banner.IncludeFeaturedInFallback
                ? pools.FeaturedFive.Concat(fallback).ToList() : fallback);
            state.GuaranteedFiveStar = !pools.FeaturedFive.Contains(item);
        }
        else
        {
            state.FourStarPity = 0;
            if (!IsEvent(banner) || pools.FeaturedFour.Count == 0)
                return Either(pools.FourStarAvatars, pools.FourStarWeapons);
            var up = state.GuaranteedFourStar || random() < (IsWeapon(banner) ? .75 : .5);
            item = up ? Pick(pools.FeaturedFour) : Either(pools.FourStarAvatars, pools.FourStarWeapons);
            state.GuaranteedFourStar = !pools.FeaturedFour.Contains(item);
        }
        return item;
    }
}

public record GachaPools(List<int> FiveStarAvatars, List<int> FiveStarWeapons,
    List<int> FourStarAvatars, List<int> FourStarWeapons, List<int> ThreeStar,
    List<int> FeaturedFive, List<int> FeaturedFour);
