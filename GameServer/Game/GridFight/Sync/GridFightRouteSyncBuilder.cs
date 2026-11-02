using March7thHoney.Proto;

namespace March7thHoney.GameServer.Game.GridFight.Sync;

/// <summary>
/// Builds route/layer sync payloads for Grid Fight node-map UI.
/// </summary>
public static class GridFightRouteSyncBuilder
{
    /// <summary>
    /// Creates a sync notify that refreshes the current chapter route layer on the client.
    /// </summary>
    public static GridFightSyncUpdateResultScNotify BuildLevelLayerSync(GridFightInstance inst)
    {
        var notify = new GridFightSyncUpdateResultScNotify();
        var section = new GridFightSyncResultData();
        section.UpdateDynamicList.Add(new GridFightSyncData
        {
            LevelSyncInfo = new GridFightLevelSyncInfo
            {
                DCPKPNLKGMM = inst.CurrentChapterId,
                SectionId = inst.SectionId,
                GridFightLayerInfo = inst.BuildCurrentLayerInfo()
            }
        });
        notify.SyncResultDataList.Add(section);
        return notify;
    }
}
