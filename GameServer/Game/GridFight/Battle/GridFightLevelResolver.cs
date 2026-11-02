using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.GameServer.Game.GridFight;

namespace March7thHoney.GameServer.Game.GridFight.Battle;

public record GridFightMonsterSpec(uint MonsterId, uint RoleStar, List<uint> DropItemIds);

public record GridFightLevelEncounter(
    uint StageId,
    uint PenaltyBonusRuleId,
    uint EliteGroupId,
    List<GridFightMonsterSpec> Monsters,
    List<uint> BindingBuffs,
    List<uint> BattleEvents,
    List<uint> TraitIds
);

public static class GridFightLevelResolver
{
    public const uint UnifiedStageId = 70000001;

    public static GridFightLevelEncounter Resolve(GridFightInstance inst)
    {
        var route = ResolveRoute(inst);
        var stageId = route?.StageID ?? UnifiedStageId;

        if (route?.NodeType == Enums.GridFight.GridFightNodeTypeEnum.Supply)
        {
            return new GridFightLevelEncounter(
                StageId: stageId,
                PenaltyBonusRuleId: 0u,
                EliteGroupId: 0u,
                Monsters: new List<GridFightMonsterSpec>(),
                BindingBuffs: new List<uint>(),
                BattleEvents: new List<uint>(),
                TraitIds: new List<uint>()
            );
        }

        var penaltyRuleId = ResolvePenaltyRuleId(inst);
        var (eliteGroup, monsters) = ResolveWave(inst);
        var bindingBuffs = ResolveBindingBuffs();
        var battleEvents = ResolveBattleEvents();
        var traitIds = ResolveTraitIds(inst);

        return new GridFightLevelEncounter(
            StageId: stageId,
            PenaltyBonusRuleId: penaltyRuleId,
            EliteGroupId: eliteGroup,
            Monsters: monsters,
            BindingBuffs: bindingBuffs,
            BattleEvents: battleEvents,
            TraitIds: traitIds
        );
    }

    public static GridFightStageRouteExcel? ResolveRoute(GridFightInstance inst)
    {
        var key = (inst.CurrentChapterId << 8) | inst.SectionId;
        if (GameData.GridFightStageRouteData.TryGetValue(inst.RouteId, out var routeBucket)
            && routeBucket.TryGetValue(key, out var route))
            return route;
        return GameData.GridFightStageRouteData.Values
            .SelectMany(d => d.Values)
            .FirstOrDefault(r => r.ChapterID == inst.CurrentChapterId && r.SectionID == inst.SectionId);
    }

    public static bool IsCombatNode(GridFightInstance inst)
    {
        var nt = ResolveRoute(inst)?.NodeType;
        return nt is not (null or Enums.GridFight.GridFightNodeTypeEnum.Supply);
    }

    private static uint ResolvePenaltyRuleId(GridFightInstance inst)
    {
        var route = ResolveRoute(inst);
        if (route == null) return 90303u;

        if (route.PenaltyBonusRuleIDList.Count > 0) return route.PenaltyBonusRuleIDList[0];
        if (GameData.GridFightNodeTemplateData.TryGetValue(route.NodeTemplateID, out var tpl) && tpl.PenaltyBonusRuleID > 0)
            return tpl.PenaltyBonusRuleID;
        return 90303u;
    }

    private static (uint EliteGroup, List<GridFightMonsterSpec> Monsters) ResolveWave(GridFightInstance inst)
    {
        var route = ResolveRoute(inst);

        var camp = ResolveCamp(inst);
        var monsters = camp?.Monsters ?? [];
        if (monsters.Count == 0)
        {
            return (1816u, new List<GridFightMonsterSpec>
            {
                new(201101005, 1, new List<uint> { 102 }),
                new(800104004, 1, new List<uint> { 199 }),
                new(800104004, 1, new List<uint> { 203 })
            });
        }

        var rng = Random.Shared;
        var wave = new List<GridFightMonsterSpec>();
        var nodeType = route?.NodeType;

        
        if (nodeType == Enums.GridFight.GridFightNodeTypeEnum.Monster)
        {
            var pool = monsters.Where(m => m.MonsterTier <= 2).ToList();
            if (pool.Count == 0) pool = monsters.ToList();
            for (var i = 0; i < 5 && pool.Count > 0; i++)
            {
                var pick = pool[rng.Next(pool.Count)];
                wave.Add(new GridFightMonsterSpec(pick.MonsterID, 1, []));
            }
            return (inst.ResolveEliteGroupForCurrentSection(), wave);
        }

        if (nodeType == Enums.GridFight.GridFightNodeTypeEnum.CampMonster
            || nodeType == Enums.GridFight.GridFightNodeTypeEnum.EliteBranch)
        {
            var isEarly = IsEarlyChapterBattle(inst, route);
            var isEncounter = nodeType == Enums.GridFight.GridFightNodeTypeEnum.EliteBranch;
            const int totalCount = 10;

            var tier2Pool = monsters.Where(m => m.MonsterTier == 2).ToList();
            var tier3Pool = monsters.Where(m => m.MonsterTier == 3).ToList();
            if (tier2Pool.Count == 0) tier2Pool = monsters.Where(m => m.MonsterTier <= 2).ToList();
            if (tier2Pool.Count == 0) tier2Pool = monsters.ToList();

            
            
            
            
            int tier3Count;
            if (isEarly || tier3Pool.Count == 0) tier3Count = 0;
            else if (isEncounter) tier3Count = Math.Min(totalCount, rng.Next(2, 4));
            else tier3Count = Math.Min(totalCount, rng.Next(1, 3));

            int normalCount = totalCount - tier3Count;
            int normalStar2Count = isEarly ? rng.Next(0, 2) : rng.Next(1, 3);
            normalStar2Count = Math.Min(normalStar2Count, normalCount);

            for (var i = 0; i < tier3Count; i++)
            {
                var pick = tier3Pool[rng.Next(tier3Pool.Count)];
                
                var star = rng.Next(2) == 0 ? 1u : 2u;
                wave.Add(new GridFightMonsterSpec(pick.MonsterID, star, []));
            }
            for (var i = 0; i < normalCount; i++)
            {
                var pick = tier2Pool[rng.Next(tier2Pool.Count)];
                var star = i < normalStar2Count ? 2u : 1u;
                wave.Add(new GridFightMonsterSpec(pick.MonsterID, star, []));
            }
            Shuffle(wave, rng);
            
            return (inst.ResolveEliteGroupForCurrentSection(), wave);
        }

        if (nodeType == Enums.GridFight.GridFightNodeTypeEnum.Supply)
        {
            return (0u, new List<GridFightMonsterSpec>());
        }

        if (nodeType == Enums.GridFight.GridFightNodeTypeEnum.Boss)
        {
            
            var bossEg = inst.ResolveEliteGroupForCurrentSection();
            var promised = inst.GetCurrentBossMonsterId();
            if (promised != 0)
            {
                wave.Add(new GridFightMonsterSpec(promised, 2, []));
                return (bossEg, wave);
            }
            var bossPool = monsters.Where(m => m.MonsterTier >= 5).OrderBy(m => m.MonsterTier).ThenBy(m => m.MonsterID).ToList();
            if (bossPool.Count == 0) bossPool = monsters.ToList();
            wave.Add(new GridFightMonsterSpec(bossPool[0].MonsterID, 2, []));
            return (bossEg, wave);
        }

        var fallbackPool = monsters.Where(m => m.MonsterTier <= 2).ToList();
        if (fallbackPool.Count == 0) fallbackPool = monsters.ToList();
        for (var i = 0; i < 3 && fallbackPool.Count > 0; i++)
        {
            var pick = fallbackPool[rng.Next(fallbackPool.Count)];
            wave.Add(new GridFightMonsterSpec(pick.MonsterID, 1, []));
        }
        return (1816u, wave);
    }

    
    private static bool IsEarlyChapterBattle(GridFightInstance inst, GridFightStageRouteExcel? route)
    {
        if (route == null) return false;
        if (route.NodeType != Enums.GridFight.GridFightNodeTypeEnum.CampMonster) return false;
        if (!GameData.GridFightStageRouteData.TryGetValue(inst.RouteId, out var bucket)) return false;
        var combatSections = bucket.Values
            .Where(r => r.ChapterID == inst.CurrentChapterId
                        && r.NodeType == Enums.GridFight.GridFightNodeTypeEnum.CampMonster)
            .Select(r => r.SectionID)
            .OrderBy(s => s)
            .Take(2)
            .ToList();
        return combatSections.Contains(inst.SectionId);
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    
    private static GridFightCampExcel? ResolveCamp(GridFightInstance inst)
    {
        if (inst.SessionCampIds.Count > 0)
        {
            var planeIdx = (int)inst.CurrentChapterId - 1;
            if (planeIdx >= 0 && planeIdx < inst.SessionCampIds.Count)
            {
                var pick = inst.SessionCampIds[planeIdx];
                if (GameData.GridFightCampData.TryGetValue(pick, out var camp) && camp.Monsters.Count > 0)
                    return camp;
            }
            foreach (var fallbackId in inst.SessionCampIds)
            {
                if (GameData.GridFightCampData.TryGetValue(fallbackId, out var camp) && camp.Monsters.Count > 0)
                    return camp;
            }
        }

        return GameData.GridFightCampData.Values
            .Where(c => c.Monsters.Count > 0 && (c.SeasonID == 0 || c.SeasonID == inst.Season))
            .OrderBy(c => c.ID)
            .FirstOrDefault();
    }

    private static List<uint> ResolveBindingBuffs() => new() { 35100001 };

    private static List<uint> ResolveBattleEvents() => new()
    {
        62210, 11216, 60014, 60020, 60023, 60024, 60028, 60036
    };

    private static List<uint> ResolveTraitIds(GridFightInstance inst)
    {
        var set = new HashSet<uint>();
        foreach (var roleId in inst.UniqueIdByPos.Values
                     .Select(uid => inst.RoleByUniqueId.GetValueOrDefault(uid))
                     .Where(r => r != 0))
        {
            var info = GridFightRoleLookup.Find(roleId);
            if (info == null) continue;
            foreach (var t in info.TraitList) set.Add(t);
        }
        return set.ToList();
    }
}
