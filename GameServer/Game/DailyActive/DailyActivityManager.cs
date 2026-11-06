using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.Database;
using March7thHoney.Database.Inventory;
using March7thHoney.Database.Quests;
using March7thHoney.Enums.Mission;
using March7thHoney.Enums.Quest;
using March7thHoney.GameServer.Game.Battle;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Server.Packet.Send.DailyActive;
using March7thHoney.GameServer.Server.Packet.Send.PlayerSync;
using March7thHoney.Proto;
using March7thHoney.Util;

namespace March7thHoney.GameServer.Game.DailyActive;

public class DailyActivityManager(PlayerInstance player, Func<long>? clock = null) : BasePlayerManager(player)
{
    public DailyActivityData Data { get; } = DatabaseHelper.Instance!.GetInstanceOrCreateNew<DailyActivityData>(player.Uid);
    private readonly Func<long> _clock = clock ?? ServerTimeProvider.GetServerUnixSec;
    private readonly SemaphoreSlim _claims = new(1, 1);
    private readonly HashSet<BattleInstance> _creditedBattles = [];
    // 4.2 modern pools (6..17) supersede legacy training pools (2..5).
    // One objective per pool; the highest-ID Rogue alternative replaces its older variant.

    private IEnumerable<int> SelectTrainingQuests()
    {
        var groups = GameData.DailyActiveQuestPoolData.Values.Where(x => x.Type == 1 || x.Type >= 6)
            .Where(x => GameData.DailyQuestData.TryGetValue(x.QuestID, out var daily) && !daily.IsDelete &&
                Data.AssignmentLevel >= daily.MinLevel && Data.AssignmentLevel <= daily.MaxLevel &&
                GameData.QuestDataData.TryGetValue(x.QuestID, out var quest) && IsUnlocked(quest) &&
                GameData.FinishWayData.ContainsKey(quest.FinishWayID) &&
                (x.Type != 1 || ConfigManager.Config.ServerOption.EnableMission))
            .GroupBy(x => x.Type).OrderBy(x => x.Key);
        foreach (var group in groups)
        {
            var candidates = group.OrderBy(x => x.QuestID).ToList();
            var index = group.Key == 9 ? candidates.Count - 1 :
                (int)((Data.Day + (long)Player.Uid * 31 + group.Key * 17L) % candidates.Count);
            yield return candidates[index].QuestID;
        }
    }

    public static long GetDay(long unixSeconds)
    {
        var options = ConfigManager.Config.ServerOption.DailyActivity;
        return (long)Math.Floor((unixSeconds + Math.Clamp(options.UtcOffsetHours, -12, 14) * 3600L
            - Math.Clamp(options.ResetHour, 0, 23) * 3600L) / 86400d);
    }

    public bool IsDailyQuest(int questId) => GameData.DailyQuestData.Values.Any(x => x.QuestList.Contains(questId));
    public bool IsAssigned(int questId) => ConfigManager.Config.ServerOption.EnableQuest && Data.QuestIds.Contains(questId);

    private bool IsUnlocked(QuestDataExcel quest) => quest.UnlockType switch
    {
        QuestUnlockTypeEnum.AutoUnlock => true,
        QuestUnlockTypeEnum.FinishMission => quest.UnlockParamList.All(id => Player.MissionManager?.GetMainMissionStatus(id) == MissionPhaseEnum.Finish),
        QuestUnlockTypeEnum.FinishQuest => quest.UnlockParamList.All(id => Player.QuestManager?.GetQuestStatus(id) is QuestStatus.QuestFinish or QuestStatus.QuestClose),
        _ => false
    };

    private void ResetNpcMission(int missionId)
    {
        if (missionId == 0 || Player.MissionManager == null || !GameData.MainMissionData.TryGetValue(missionId, out var main)) return;
        Player.MissionManager.Data.SetMainMissionStatus(missionId, MissionPhaseEnum.None);
        foreach (var id in main.SubMissionIds)
        {
            Player.MissionManager.Data.SetSubMissionStatus(id, MissionPhaseEnum.None);
            Player.MissionManager.Data.SubMissionProgressDict.Remove(id);
            Player.MissionManager.Data.InteractedMissionProps.Remove(id);
        }
    }

    public async ValueTask RefreshAsync(bool notify = false)
    {
        if (Player.QuestManager == null || !ConfigManager.Config.ServerOption.EnableQuest) return;
        // Refuse to consume today's reset when required resources are unavailable.
        if (GameData.DailyActiveConfigData.Count == 0 || GameData.DailyQuestData.Count == 0 || GameData.DailyActiveQuestPoolData.Count == 0) return;
        var day = GetDay(_clock());
        var changed = false;
        if (day > Data.Day)
        {
            ResetNpcMission(Data.NpcMissionId);
            foreach (var id in Player.QuestManager.QuestData.Quests.Keys.Where(IsDailyQuest).ToList())
                Player.QuestManager.QuestData.Quests.Remove(id);
            Player.QuestManager.WaitToSync.RemoveAll(q => IsDailyQuest(q.QuestId));
            Data.Day = day;
            Data.Points = 0;
            Data.RewardWorldLevel = Player.Data.WorldLevel;
            Data.AssignmentLevel = Player.Data.Level;
            Data.QuestIds.Clear(); Data.ClaimedLevels.Clear();
            Data.NpcMissionId = 0; Data.NpcCompleted = false; Data.BoardAssigned = false;
            _creditedBattles.Clear(); changed = true;
        }
        if (Data.QuestIds.Count == 0)
        {
            Data.AssignmentLevel = Player.Data.Level;
            Data.RewardWorldLevel = Player.Data.WorldLevel;
        }
        // Existing boards survive an upgrade until their next daily reset.
        var selection = Data.BoardAssigned || Data.QuestIds.Count > 0 ? Array.Empty<int>() : SelectTrainingQuests().ToArray();
        foreach (var id in selection)
        {
            if (id == 2100003 && !ConfigManager.Config.ServerOption.EnableMission) continue;
            if (Data.QuestIds.Contains(id) || !GameData.DailyQuestData.TryGetValue(id, out var daily) ||
                daily.IsDelete || Data.AssignmentLevel < daily.MinLevel || Data.AssignmentLevel > daily.MaxLevel ||
                !GameData.QuestDataData.TryGetValue(id, out var quest) || !IsUnlocked(quest) ||
                !GameData.FinishWayData.TryGetValue(quest.FinishWayID, out var finish)) continue;
            Data.QuestIds.Add(id);
            // Login finishes on assignment; other objectives wait for their matching events.
            Player.QuestManager.QuestData.Quests[id] = new QuestInfo
            {
                QuestId = id, QuestStatus = finish.FinishType == MissionFinishTypeEnum.AutoFinish ? QuestStatus.QuestFinish : QuestStatus.QuestDoing,
                Progress = finish.FinishType == MissionFinishTypeEnum.AutoFinish ? finish.Progress : 0, FinishTime = finish.FinishType == MissionFinishTypeEnum.AutoFinish ? _clock() : 0
            };
            changed = true;
        }
        if (Data.QuestIds.Count > 0 && !Data.BoardAssigned) { Data.BoardAssigned = true; changed = true; }
        if (Data.NpcMissionId == 0 && IsAssigned(2100003) && ConfigManager.Config.ServerOption.EnableMission)
        {
            var eligible = GameData.DailyMissionData.Values.Where(x => x.DailyMissionType == 1 &&
                (x.UnlockMainMission == 0 || Player.MissionManager?.GetMainMissionStatus(x.UnlockMainMission) == MissionPhaseEnum.Finish) &&
                GameData.MainMissionData.TryGetValue(x.ID, out var main) && main.MissionInfo.StartSubMissionList.Count > 0 &&
                main.MissionInfo.SubMissionList.Count > 0 && main.MissionInfo.FinishSubMissionList.Count > 0 &&
                main.MissionInfo.SubMissionList.All(s => s.FinishType is MissionFinishTypeEnum.Talk
                    or MissionFinishTypeEnum.SubMissionFinishCnt or MissionFinishTypeEnum.ConsumeMissionItem
                    or MissionFinishTypeEnum.StageWin or MissionFinishTypeEnum.KillMonster
                    or MissionFinishTypeEnum.MessageSectionFinish or MissionFinishTypeEnum.EnterFloor
                    or MissionFinishTypeEnum.InteractPropList or MissionFinishTypeEnum.UseSelectedItem))
                .OrderBy(x => x.ID).ToList();
            if (eligible.Count > 0)
            {
                Data.NpcMissionId = eligible[(int)((Data.Day + (long)Player.Uid) % eligible.Count)].ID;
                ResetNpcMission(Data.NpcMissionId); // Also recover legacy daily missions without a daily ledger.
                changed = true;
            }
        }
        if (Data.NpcMissionId != 0 && !Data.NpcCompleted && Player.MissionManager != null &&
            Player.MissionManager.Data.GetMainMissionStatus(Data.NpcMissionId) == MissionPhaseEnum.None)
            await Player.MissionManager.AcceptMainMission(Data.NpcMissionId);
        if (changed)
        {
            DatabaseHelper.Instance?.SaveUidData(Player.Uid);
            if (notify) await SyncAsync();
        }
    }

    public IEnumerable<DailyActiveConfigExcel> Levels => GameData.DailyActiveConfigData.Values
        .Where(x => x.WorldLevel == Data.RewardWorldLevel).OrderBy(x => x.Level);

    public List<DailyActivityInfo> ToLevels() => Levels.Select(x => new DailyActivityInfo
    {
        Level = (uint)x.Level, WorldLevel = (uint)x.WorldLevel,
        DailyActivePoint = (uint)x.DailyActivePoint, IsHasTaken = Data.ClaimedLevels.Contains(x.Level)
    }).ToList();

    public async ValueTask SyncAsync()
    {
        await Player.SendPacket(new PacketDailyActiveInfoNotify(this));
        var quests = Data.QuestIds.Select(id => Player.QuestManager!.QuestData.Quests.GetValueOrDefault(id))
            .OfType<QuestInfo>().ToList();
        if (quests.Count > 0) await Player.SendPacket(new PacketPlayerSyncScNotify(quests));
    }

    public async ValueTask RecordProgress(int questId, int amount)
    {
        if (amount <= 0) return;
        await RefreshAsync();
        if (!IsAssigned(questId) || !Player.QuestManager!.QuestData.Quests.TryGetValue(questId, out var quest) ||
            quest.QuestStatus != QuestStatus.QuestDoing) return;
        var config = GameData.QuestDataData.GetValueOrDefault(questId);
        var finish = GameData.FinishWayData.GetValueOrDefault(config?.FinishWayID ?? 0);
        if (finish == null || finish.Progress <= 0) return;
        quest.Progress = (int)Math.Min(finish.Progress, (long)quest.Progress + amount);
        if (quest.Progress == finish.Progress) { quest.QuestStatus = QuestStatus.QuestFinish; quest.FinishTime = _clock(); }
        DatabaseHelper.Instance?.SaveUidData(Player.Uid);
        await Player.SendPacket(new PacketPlayerSyncScNotify(quest));
    }

    public async ValueTask OnMissionFinished(int missionId)
    {
        await RefreshAsync();
        if (missionId != Data.NpcMissionId || Data.NpcCompleted) return;
        Data.NpcCompleted = true;
        await RecordProgress(2100003, 1);
        DatabaseHelper.Instance?.SaveUidData(Player.Uid);
    }

    public async ValueTask OnQuestFinished(int questId)
    {
        await RefreshAsync();
        var config = GameData.QuestDataData.GetValueOrDefault(2100156);
        if (GameData.FinishWayData.GetValueOrDefault(config?.FinishWayID ?? 0)?.ParamIntList.Contains(questId) == true)
            await RecordProgress(2100156, 1);
    }

    public async ValueTask OnBattleFinished(BattleInstance battle)
    {
        await RefreshAsync();
        if (battle.BattleResult?.EndStatus != BattleEndStatus.BattleEndWin || !_creditedBattles.Add(battle)) return;
        if (battle.Lineup.BaseAvatars?.Any(x => x.AssistUid > 0) == true)
            await RecordEvent(MissionFinishTypeEnum.WinBattleWithAssistAvatarCnt);
        if (battle.ExcellentEntry) await RecordEvent(MissionFinishTypeEnum.ExcellentEnterBattleAndWin);
        foreach (var id in Data.QuestIds.ToArray())
        {
            var finish = FinishWay(id);
            if (finish?.FinishType != MissionFinishTypeEnum.BattleChallenge || battle.BattleResult.Stt == null) continue;
            var progress = battle.BattleResult.Stt.BattleTargetInfo.Values.SelectMany(x => x.BattleTargetList_)
                .Where(x => finish.ParamIntList.Contains((int)x.Id)).Sum(x => (long)x.Progress);
            await RecordProgress(id, (int)Math.Min(Math.Max(progress, 0), int.MaxValue));
        }
        if (battle.CocoonId > 0 && GameData.CocoonConfigData.TryGetValue(battle.CocoonId * 100 + battle.WorldLevel, out var cocoon))
        {
            var type = cocoon.CocoonType switch { "TYPE_WEEK" => 2, "TYPE_RELIC" => 3, _ => 1 };
            await RecordEvent(MissionFinishTypeEnum.AnyCocoonFinish, 1, type);
            await RecordEvent(MissionFinishTypeEnum.CocoonFinish, 1, cocoon.ID);
            var farm = cocoon.FarmType switch { "COCOON_AVATAR_EXP" => 6, "COCOON_EQUIPMENT_EXP" => 7,
                "COCOON_COIN" => 8, "COCOON3" => 5, _ => 0 };
            if (farm > 0) await RecordEvent(MissionFinishTypeEnum.FarmTypeCocoonFinish, 1, farm);
        }
        else if (battle.MappingInfoId > 0 && battle.CocoonWave > 0)
            await RecordEvent(MissionFinishTypeEnum.AnyFarmElementFinish);
    }

    private FinishWayExcel? FinishWay(int id) => GameData.FinishWayData.GetValueOrDefault(
        GameData.QuestDataData.GetValueOrDefault(id)?.FinishWayID ?? 0);

    public async ValueTask RecordEvent(MissionFinishTypeEnum type, int count = 1, int parameter = 0)
    {
        if (count <= 0) return;
        await RefreshAsync();
        foreach (var id in Data.QuestIds.ToArray())
        {
            var finish = FinishWay(id);
            if (finish?.FinishType != type) continue;
            if (finish.ParamIntList.Count > 0 && !finish.ParamIntList.Contains(parameter)) continue;
            if (finish.ParamIntList.Count == 0 && finish.ParamInt1 != 0 && finish.ParamInt1 != parameter) continue;
            await RecordProgress(id, count);
        }
    }

    public async ValueTask<Retcode> ReportClientObjective(int id)
    {
        await RefreshAsync();
        // Photography is a client-only action. Never accept client claims for combat, items or NPC missions.
        if (!IsAssigned(id) || FinishWay(id)?.FinishType != MissionFinishTypeEnum.TakePictureCnt)
            return Retcode.RetQuestStatusError;
        await RecordProgress(id, 1);
        return Retcode.RetSucc;
    }

    public async ValueTask OnItemComposed(int count) => await RecordEvent(MissionFinishTypeEnum.AnyItemComposeCount, count);

    private static List<ItemData>? RewardItems(int rewardId)
    {
        if (!GameData.RewardDataData.TryGetValue(rewardId, out var reward)) return null;
        var items = reward.GetItems().Where(x => x.Item2 > 0).Select(x => new ItemData { ItemId = x.Item1, Count = x.Item2 }).ToList();
        if (reward.Hcoin > 0) items.Add(new() { ItemId = 1, Count = reward.Hcoin });
        return items.Count > 0 && items.All(x => x.ItemId == 23 || GameData.ItemConfigData.ContainsKey(x.ItemId)) ? items : null;
    }

    public async ValueTask<(Retcode, List<ItemData>?)> TakeQuestReward(int questId)
    {
        await _claims.WaitAsync();
        try
        {
            await RefreshAsync();
            if (!IsAssigned(questId) || !Player.QuestManager!.QuestData.Quests.TryGetValue(questId, out var quest)) return (Retcode.RetQuestNotAccept, null);
            if (quest.QuestStatus != QuestStatus.QuestFinish) return (Retcode.RetQuestNotFinish, null);
            var items = RewardItems(GameData.QuestDataData.GetValueOrDefault(questId)?.RewardID ?? 0);
            if (items == null || Player.InventoryManager == null) return (Retcode.RetFail, null);
            quest.QuestStatus = QuestStatus.QuestClose;
            Data.Points = Math.Min(Levels.Select(x => x.DailyActivePoint).DefaultIfEmpty(500).Max(),
                Data.Points + items.Where(x => x.ItemId == 23).Sum(x => x.Count));
            try { await Player.InventoryManager!.AddItems(items.Where(x => x.ItemId != 23).ToList(), false); }
            finally { DatabaseHelper.Instance?.SaveUidData(Player.Uid); }
            await SyncAsync();
            return (Retcode.RetSucc, items);
        }
        finally { _claims.Release(); }
    }

    public async ValueTask<(Retcode Code, List<ItemData> Items, List<int> Levels)> TakeRewards(int? level = null)
    {
        if (!ConfigManager.Config.ServerOption.EnableQuest) return (Retcode.RetQuestNotAccept, [], []);
        await _claims.WaitAsync();
        try
        {
            await RefreshAsync();
            var levels = Levels.Where(x => (level == null || x.Level == level) &&
                x.DailyActivePoint <= Data.Points && !Data.ClaimedLevels.Contains(x.Level)).ToList();
            if (levels.Count == 0) return (Retcode.RetQuestNotFinish, [], []);
            var rewards = levels.Select(x => RewardItems(x.DailyActiveReward)).ToList();
            if (rewards.Any(x => x == null) || Player.InventoryManager == null) return (Retcode.RetFail, [], []);
            var items = rewards.SelectMany(x => x!).GroupBy(x => x.ItemId)
                .Select(x => new ItemData { ItemId = x.Key, Count = x.Sum(y => y.Count) }).ToList();
            var ids = levels.Select(x => x.Level).ToList();
            Data.ClaimedLevels.AddRange(ids);
            try { await Player.InventoryManager.AddItems(items, false); }
            finally { DatabaseHelper.Instance?.SaveUidData(Player.Uid); }
            await SyncAsync();
            return (Retcode.RetSucc, items, ids);
        }
        finally { _claims.Release(); }
    }
}
