using March7thHoney.Data;
using March7thHoney.Database.Avatar;
using March7thHoney.GameServer.Game.Battle;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Game.Lineup;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.Proto;
using March7thHoney.Util;

namespace March7thHoney.GameServer.Game.GridFight.Battle;

public static class GridFightBattleProtoBuilder
{
    public static List<AvatarLineupData> HandleProto(BattleInstance battle, GridFightInstance inst, SceneBattleInfo proto)
    {
        var enc = GridFightLevelResolver.Resolve(inst);
        var player = battle.Player;
        var foregroundData = new List<AvatarLineupData>();

        var collection = new PlayerDataCollection(player.Data, player.InventoryManager!.Data, battle.Lineup);

        foreach (var (roleKey, pos) in inst.ResolveForegroundRoles())
        {
            var resolved = ResolveForegroundBattleAvatar(player, roleKey);
            var avatar = resolved.Avatar;
            if (avatar == null) continue;

            var battleAvatar = avatar.ToBattleProto(collection, AvatarType.AvatarTrialType);
            battleAvatar.Index = pos;
            proto.BattleAvatarList.Add(battleAvatar);
            foregroundData.Add(new AvatarLineupData(avatar, AvatarType.AvatarTrialType));
        }

        proto.MonsterWaveList.Clear();

        var stageId = inst.BattleComponent.StageId > 0 ? inst.BattleComponent.StageId : enc.StageId;
        var monsterSpecs = inst.BattleComponent.MonsterIds.Count > 0
            ? inst.BattleComponent.MonsterIds.Select(id => new GridFightMonsterSpec(id, 1u, [])).ToList()
            : enc.Monsters;

        proto.StageId = stageId;

        var monsterWorldLevel = (uint)Math.Max(1, battle.Player.Data.WorldLevel - 1);
        var wave = new SceneMonsterWave
        {
            BattleStageId = stageId,
            BattleWaveId = 1,
            MonsterParam = new SceneMonsterWaveParam
            {
                EliteGroup = inst.ResolveEliteGroupForCurrentSection(),
                BDCCEFHMFHO = monsterWorldLevel
            }
        };
        foreach (var spec in monsterSpecs)
        {
            var sceneMonster = new SceneMonster
            {
                MonsterId = spec.MonsterId,
                ExtraInfo = new MEHAOMGBOMC
                {
                    AFCMOOFGBPK = new DLGEGGCHCID { RoleStar = spec.RoleStar }
                }
            };
            foreach (var itemId in spec.DropItemIds)
            {
                sceneMonster.ExtraInfo.AFCMOOFGBPK.PGNMDJIIKJB.Add(new LHPPIAKKFME
                {
                    BGKDAMDFFKH = GridFightDropType.HiolcnpoponMkppcdpchie,
                    JJFFLMCCCMM = itemId,
                    Num = 1
                });
            }

            wave.MonsterList.Add(sceneMonster);
        }

        proto.MonsterWaveList.Add(wave);

        foreach (var buffId in enc.BindingBuffs)
        {
            if (battle.Buffs.Any(b => b.BuffID == (int)buffId)) continue;
            battle.Buffs.Add(new MazeBuff((int)buffId, 1, -1) { WaveFlag = -1 });
        }

        foreach (var beId in enc.BattleEvents)
            battle.BattleEvents.TryAdd((int)beId, new BattleEventInstance((int)beId, 0, 100000));

        return foregroundData;
    }

    /// <summary>
    /// Resolves a foreground board role as a Grid Fight trial avatar (pos 1-4).
    /// </summary>
    internal static (BaseAvatarInfo? Avatar, AvatarType AvatarType) ResolveForegroundBattleAvatar(
        PlayerInstance player, uint roleKey)
    {
        var basicInfo = GridFightRoleLookup.Find(roleKey);
        if (basicInfo == null)
            return (null, AvatarType.AvatarTrialType);

        var trial = player.AvatarManager?.GetTrialAvatarByWorldLevel(
            (int)basicInfo.SpecialAvatarID, player.Data.WorldLevel);
        if (trial != null)
            return (trial, AvatarType.AvatarTrialType);

        var formal = player.AvatarManager?.GetFormalAvatar((int)basicInfo.AvatarID);
        return (formal, AvatarType.AvatarTrialType);
    }

    /// <summary>
    /// Resolves a background board role for Grid Fight battle payloads (pos 5-13).
    /// </summary>
    internal static (BaseAvatarInfo? Avatar, AvatarType AvatarType) ResolveBackgroundBattleAvatar(
        PlayerInstance player, uint roleKey)
    {
        var basicInfo = GridFightRoleLookup.Find(roleKey);
        if (basicInfo == null)
            return (null, AvatarType.AvatarGridFightType);

        var formal = player.AvatarManager?.GetFormalAvatar((int)basicInfo.AvatarID);
        if (formal != null)
            return (formal, AvatarType.AvatarGridFightType);

        var trial = player.AvatarManager?.GetTrialAvatarByWorldLevel(
            (int)basicInfo.SpecialAvatarID, player.Data.WorldLevel);
        return (trial, AvatarType.AvatarGridFightType);
    }
}
