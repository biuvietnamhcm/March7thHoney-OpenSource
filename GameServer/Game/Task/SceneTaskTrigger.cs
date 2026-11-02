using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.GameServer.Game.Player;

namespace March7thHoney.GameServer.Game.Task;

public class SceneTaskTrigger(PlayerInstance player)
{
    public PlayerInstance Player { get; } = player;

    public async ValueTask TriggerFloor(int planeId, int floorId)
    {
        GameData.GetFloorInfo(planeId, floorId, out var floor);
        if (floor == null) return;

        foreach (var group in floor.Groups.Values)
        {
            if (group.LevelGraphConfig == null) continue;
            foreach (var task in group.LevelGraphConfig.OnInitSequece)
                await Player.TaskManager!.LevelTask.TriggerInitAct(task, new SubMissionData(0), group);

            foreach (var task in group.LevelGraphConfig.OnStartSequece)
                await Player.TaskManager!.LevelTask.TriggerStartAct(task, new SubMissionData(0), group);
        }
    }

    public async ValueTask TriggerGroup(int planeId, int floorId, int groupId)
    {
        GameData.GetFloorInfo(planeId, floorId, out var floor);
        if (floor == null) return;
        if (floor.Groups.TryGetValue(groupId, out var group))
        {
            if (group.LevelGraphConfig == null) return;
            foreach (var task in group.LevelGraphConfig.OnInitSequece)
                await Player.TaskManager!.LevelTask.TriggerInitAct(task, new SubMissionData(0), group);

            foreach (var task in group.LevelGraphConfig.OnStartSequece)
                await Player.TaskManager!.LevelTask.TriggerStartAct(task, new SubMissionData(0), group);
        }
    }
}
