namespace March7thHoney.Data.Excel;

[ResourceEntity("CocoonConfig.json")]
public class CocoonConfigExcel : ExcelResource
{
    public int ID { get; set; }
    public string CocoonType { get; set; } = "";
    public string FarmType { get; set; } = "";
    public int MappingInfoID { get; set; }
    public int WorldLevel { get; set; }
    public int PropID { get; set; }
    public int StaminaCost { get; set; }
    public int MaxWave { get; set; }
    public List<int> StageIDList { get; set; } = [];
    public List<int> DropList { get; set; } = [];

    public override int GetId()
    {
        return ID * 100 + WorldLevel;
    }

    public override void Loaded()
    {
        GameData.CocoonConfigData.Add(GetId(), this);
    }
}