using March7thHoney.Data;
using March7thHoney.Data.Excel;

namespace March7thHoney.GameServer.Game.GridFight.Battle;

/// <summary>
/// Resolves GridFight role config entries. Runtime maps may store either internal role ID or AvatarID.
/// </summary>
public static class GridFightRoleLookup
{
    /// <summary>
    /// Finds role basic info by internal role ID or AvatarID.
    /// </summary>
    public static GridFightRoleBasicInfoExcel? Find(uint roleKey)
    {
        if (roleKey == 0) return null;
        if (GameData.GridFightRoleBasicInfoData.TryGetValue(roleKey, out var byId))
            return byId;

        foreach (var role in GameData.GridFightRoleBasicInfoData.Values)
        {
            if (role.AvatarID == roleKey) return role;
        }

        return null;
    }

    /// <summary>
    /// Tries to resolve role basic info by internal role ID or AvatarID.
    /// </summary>
    public static bool TryFind(uint roleKey, out GridFightRoleBasicInfoExcel basicInfo)
    {
        var found = Find(roleKey);
        if (found == null)
        {
            basicInfo = null!;
            return false;
        }

        basicInfo = found;
        return true;
    }

    /// <summary>
    /// Converts a stored role key to the avatar ID used in battle payloads.
    /// </summary>
    public static uint ToAvatarId(uint roleKey)
    {
        return Find(roleKey)?.AvatarID ?? roleKey;
    }

    /// <summary>
    /// Converts a stored role key to the internal GridFight role ID.
    /// </summary>
    public static uint ToRoleId(uint roleKey)
    {
        return Find(roleKey)?.ID ?? roleKey;
    }

    /// <summary>
    /// Returns the role ID used in GridGameRoleInfo sync payloads (internal ID, e.g. 11011 for Bronya path 1).
    /// </summary>
    public static uint ToSyncRoleId(uint roleKey) => ToRoleId(roleKey);
}
