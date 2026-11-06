using SqlSugar;

namespace March7thHoney.Database.Quests;

[SugarTable("daily_activity")]
public class DailyActivityData : BaseDatabaseDataHelper
{
    public long Day { get; set; }
    public int Points { get; set; }
    public int RewardWorldLevel { get; set; }
    public int AssignmentLevel { get; set; }
    public int NpcMissionId { get; set; }
    public bool NpcCompleted { get; set; }
    public bool BoardAssigned { get; set; }
    [SugarColumn(IsJson = true)] public List<int> QuestIds { get; set; } = [];
    [SugarColumn(IsJson = true)] public List<int> ClaimedLevels { get; set; } = [];
}
