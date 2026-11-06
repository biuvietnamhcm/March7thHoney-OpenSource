using March7thHoney.Database.Gacha;
using March7thHoney.Enums;
using March7thHoney.Proto;
using Newtonsoft.Json;
using GachaInfo = March7thHoney.Proto.GachaInfo;

namespace March7thHoney.Data.Custom;

public class BannersConfig
{
    public List<BannerConfig> Banners { get; set; } = [];
}

public class BannerConfig
{
    [JsonProperty("id")] public int GachaId { get; set; }
    public long BeginTime { get; set; }
    public long EndTime { get; set; }
    public GachaTypeEnum GachaType { get; set; }
    public List<int> RateUpItems5 { get; set; } = [];
    public List<int> RateUpItems4 { get; set; } = [];
    public int? GetRateUpItem5Chance { get; set; }
    public int? MaxCount { get; set; }
    public int? EventChance { get; set; }
    public bool UseSoftPity { get; set; } = true;
    public bool IncludeFeaturedInFallback { get; set; } = true;
    public string? PityGroup { get; set; }

    public GachaInfo ToInfo(List<int> decideOrder, List<int> goldAvatar)
    {
        var info = new GachaInfo
        {
            GachaId = (uint)GachaId,
            DetailUrl = "",
            HistoryUrl = ""
        };

        
        
        if (RateUpItems4.Count > 0) info.CBHAEFNAFLE.AddRange(RateUpItems4.Select(id => (uint)id));

        if (GachaType != GachaTypeEnum.Normal)
        {
            info.BeginTime = BeginTime;
            info.EndTime = EndTime;
        }

        if (GachaId == 1001)
        {
            if (RateUpItems5.Count > 0)
            {
                info.PrizeItemList.AddRange(RateUpItems5.Select(id => (uint)id));
            }

            info.GachaCeiling = new GachaCeiling
            {
                IsClaimed = false,
                AvatarList = { goldAvatar.Select(id => new GachaCeilingAvatar { AvatarId = (uint)id }) }
            };
        }
        else
        {
            if (RateUpItems5.Count > 0)
            {
                info.PrizeItemList.AddRange(RateUpItems5.Select(id => (uint)id));
            }

            
            if (GachaType == GachaTypeEnum.AvatarUp) info.ItemDetailList.Add(11);
        }

        return info;
    }
}
