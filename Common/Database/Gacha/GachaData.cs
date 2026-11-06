using SqlSugar;

namespace March7thHoney.Database.Gacha;

[SugarTable("Gacha")]
public class GachaData : BaseDatabaseDataHelper
{
    [SugarColumn(IsJson = true)] public List<GachaInfo> GachaHistory { get; set; } = [];

    public bool LastAvatarGachaFailed { get; set; } = false;
    public bool LastWeaponGachaFailed { get; set; } = false;
    public int LastGachaFailedCount { get; set; } = 0;
    public int LastGachaPurpleFailedCount { get; set; } = 0;
    [SugarColumn(IsJson = true)] public List<int> GachaDecideOrder { get; set; } = [];
    [SugarColumn(IsJson = true)] public Dictionary<string, GachaPityState> PityGroups { get; set; } = [];
    public bool PityMigrated { get; set; }
    public bool StandardCeilingClaimed { get; set; }
}

public class GachaPityState
{
    public int FiveStarPity { get; set; }
    public int FourStarPity { get; set; }
    public bool GuaranteedFiveStar { get; set; }
    public bool GuaranteedFourStar { get; set; }
    public int TotalPulls { get; set; }
    public bool ReceivedFiveStar { get; set; }
}

public class GachaInfo
{
    public int GachaId { get; set; }
    public long Time { get; set; }
    public int ItemId { get; set; }
}
