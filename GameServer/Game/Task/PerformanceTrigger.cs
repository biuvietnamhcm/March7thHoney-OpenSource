using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.GameServer.Game.Player;

namespace March7thHoney.GameServer.Game.Task;

public class PerformanceTrigger(PlayerInstance player)
{
    public PlayerInstance Player { get; } = player;

    public async ValueTask TriggerPerformanceE(int performanceEId, SubMissionData subMission)
    {
        GameData.PerformanceEData.TryGetValue(performanceEId, out var excel);
        if (excel != null) await TriggerPerformanceE(excel, subMission);
    }

    public async ValueTask TriggerPerformanceE(PerformanceEExcel excel, SubMissionData subMission)
    {
        if (excel.ActInfo == null) return;
        foreach (var act in excel.ActInfo.OnInitSequece) await Player.TaskManager!.LevelTask.TriggerInitAct(act, subMission);

        foreach (var act in excel.ActInfo.OnStartSequece)
            await Player.TaskManager!.LevelTask.TriggerStartAct(act, subMission);
    }

    public async ValueTask TriggerPerformanceD(int performanceDId, SubMissionData subMission)
    {
        GameData.PerformanceDData.TryGetValue(performanceDId, out var excel);
        if (excel != null) await TriggerPerformanceD(excel, subMission);
    }

    public async ValueTask TriggerPerformanceD(PerformanceDExcel excel, SubMissionData subMission)
    {
        if (excel.ActInfo == null) return;
        foreach (var act in excel.ActInfo.OnInitSequece) await Player.TaskManager!.LevelTask.TriggerInitAct(act, subMission);

        foreach (var act in excel.ActInfo.OnStartSequece)
            await Player.TaskManager!.LevelTask.TriggerStartAct(act, subMission);
    }
}
