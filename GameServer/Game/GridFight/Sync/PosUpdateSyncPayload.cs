namespace March7thHoney.GameServer.Game.GridFight.Sync;

/// <summary>
/// 走位后的 sync 附加数据。
/// </summary>
public sealed class PosUpdateSyncPayload
{
    public List<Proto.GridFightPosInfo> UpdatedPosList { get; init; } = [];
    public List<GridFightInstance.RoleMergeResult> Merges { get; init; } = [];
}
