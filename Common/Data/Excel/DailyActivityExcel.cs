using March7thHoney.Util;

namespace March7thHoney.Data.Excel;

[ResourceEntity("DailyActiveConfig.json")]
public class DailyActiveConfigExcel : ExcelResource
{
    public int WorldLevel { get; set; }
    public int Level { get; set; }
    public int DailyActivePoint { get; set; }
    public int DailyActiveReward { get; set; }
    public override int GetId() => WorldLevel * 100 + Level;
    public override void Loaded() => GameData.DailyActiveConfigData[GetId()] = this;
}

[ResourceEntity("DailyQuest.json")]
public class DailyQuestExcel : ExcelResource
{
    public int DailyID { get; set; }
    public List<int> QuestList { get; set; } = [];
    public int MinLevel { get; set; }
    public int MaxLevel { get; set; }
    public bool IsDelete { get; set; }
    public override int GetId() => DailyID;
    public override void Loaded() => GameData.DailyQuestData[DailyID] = this;
}

[ResourceEntity("DailyMissionData.json")]
public class DailyMissionExcel : ExcelResource
{
    public int ID { get; set; }
    public int DailyMissionType { get; set; }
    public int GroupID { get; set; }
    public int UnlockMainMission { get; set; }
    public int QuestID { get; set; }
    public override int GetId() => ID;
    public override void Loaded() => GameData.DailyMissionData[ID] = this;
}
