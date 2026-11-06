namespace March7thHoney.Data.Excel;

[ResourceEntity("MonsterConfig.json")]
public class MonsterConfigExcel : ExcelResource
{
    public int MonsterID { get; set; }
    public List<string> StanceWeakList { get; set; } = [];
    public int MonsterTemplateID { get; set; }
    public int HardLevelGroup { get; set; }
    public List<int> SummonIDList { get; set; } = [];

    public override int GetId()
    {
        return MonsterID;
    }

    public override void Loaded()
    {
        GameData.MonsterConfigData.Add(MonsterID, this);
    }
}