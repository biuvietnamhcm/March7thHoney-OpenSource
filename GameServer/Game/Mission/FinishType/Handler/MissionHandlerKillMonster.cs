using March7thHoney.Data.Config;
using March7thHoney.Data.Excel;
using March7thHoney.Enums.Mission;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Game.Scene.Entity;

namespace March7thHoney.GameServer.Game.Mission.FinishType.Handler;

[MissionFinishType(MissionFinishTypeEnum.KillMonster)]
public class MissionHandlerKillMonster : MissionFinishTypeHandler
{
    public override async ValueTask HandleMissionFinishType(PlayerInstance player, SubMissionInfo info, object? arg)
    {
        if (arg is not EntityMonster monster) return;
        if (info.LevelFloorID != 0 && monster.Scene.FloorId != info.LevelFloorID) return;
        if (info.ParamInt1 != 0 && monster.GroupId != info.ParamInt1) return;
        if (monster.InstId == info.ParamInt2)
            if (!monster.IsAlive)
                await player.MissionManager!.FinishSubMission(info.ID);
    }

    public override async ValueTask HandleQuestFinishType(PlayerInstance player, QuestDataExcel quest,
        FinishWayExcel excel, object? arg)
    {
        
        await ValueTask.CompletedTask;
    }
}
