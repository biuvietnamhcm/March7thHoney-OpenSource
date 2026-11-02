using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.Database.Lineup;
using March7thHoney.GameServer.Game.Battle;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.Proto;
using LineupInfo = March7thHoney.Database.Lineup.LineupInfo;

namespace March7thHoney.GameServer.Game.GridFight.Battle;

public static class GridFightBattleModule
{
    public static BattleInstance? StartBattle(PlayerInstance player, GridFightInstance gridFightInstance)
    {
        if (player.BattleInstance != null)
            return player.BattleInstance;

        var stageConfig = ResolveStageConfig(gridFightInstance);
        if (stageConfig == null)
            return null;

        var foregroundIds = gridFightInstance.BuildForegroundAvatarIds();
        var backgroundIds = gridFightInstance.BuildBackgroundAvatarIds();

        var tempLineup = new LineupInfo
        {
            LineupType = (int)ExtraLineupType.LineupChessRogue,
            BaseAvatars = foregroundIds.Concat(backgroundIds)
                .Select(id => new LineupAvatarInfo { BaseAvatarId = id })
                .ToList(),
            AvatarData = player.AvatarManager?.AvatarData
        };

        var battle = new BattleInstance(player, tempLineup, [stageConfig])
        {
            WorldLevel = player.Data.WorldLevel,
            GridFightContext = gridFightInstance,
            LogicRandomSeed = (uint)Random.Shared.Next()
        };

        player.BattleInstance = battle;
        player.QuestManager?.OnBattleStart(battle);
        return battle;
    }

    private static StageConfigExcel? ResolveStageConfig(GridFightInstance gridFightInstance)
    {
        var configuredStageId = gridFightInstance.BattleComponent.StageId;
        if (configuredStageId > 0
            && GameData.StageConfigData.TryGetValue((int)configuredStageId, out var configuredStage))
            return configuredStage;

        var encounter = GridFightLevelResolver.Resolve(gridFightInstance);
        if (GameData.StageConfigData.TryGetValue((int)encounter.StageId, out var encounterStage))
            return encounterStage;

        if (GameData.StageConfigData.TryGetValue((int)GridFightLevelResolver.UnifiedStageId, out var unifiedStage))
            return unifiedStage;

        return GameData.StageConfigData.Values.FirstOrDefault();
    }
}
