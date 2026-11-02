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
    // Modern training board already used by this server, plus the NPC daily objective.
    public static readonly int[] TrainingQuestIds = [2100132, 2100133, 2100003, 2100139,
        2100141, 2100145, 2100148, 2100152, 2100153, 2100155, 2100156];

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
        }
    }

    public async ValueTask RefreshAsync(bool notify = false)
    {
        if (Player.QuestManager == null || !ConfigManager.Config.ServerOption.EnableQuest) return;
        // Refuse to consume today's reset when required resources are unavailable.
        if (GameData.DailyActiveConfigData.Count == 0 || GameData.DailyQuestData.Count == 0) return;
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
            Data.NpcMissionId = 0; Data.NpcCompleted = false;
            _creditedBattles.Clear(); changed = true;
        }
        if (Data.QuestIds.Count == 0)
        {
            Data.AssignmentLevel = Player.Data.Level;
            Data.RewardWorldLevel = Player.Data.WorldLevel;
        }
        foreach (var id in TrainingQuestIds)
        {
            if (id == 2100003 && !ConfigManager.Config.ServerOption.EnableMission) continue;
            if (Data.QuestIds.Contains(id) || !GameData.DailyQuestData.TryGetValue(id, out var daily) ||
                daily.IsDelete || Data.AssignmentLevel < daily.MinLevel || Data.AssignmentLevel > daily.MaxLevel ||
                !GameData.QuestDataData.TryGetValue(id, out var quest) || !IsUnlocked(quest) ||
                !GameData.FinishWayData.TryGetValue(quest.FinishWayID, out var finish)) continue;
            Data.QuestIds.Add(id);
            // Login is a server event. Other daily objectives cannot be completed by FinishQuestCsReq.
            Player.QuestManager.QuestData.Quests[id] = new QuestInfo
            {
                QuestId = id, QuestStatus = id == 2100132 ? QuestStatus.QuestFinish : QuestStatus.QuestDoing,
                Progress = id == 2100132 ? finish.Progress : 0, FinishTime = id == 2100132 ? _clock() : 0
            };
            changed = true;
        }
        if (Data.NpcMissionId == 0 && IsAssigned(2100003) && ConfigManager.Config.ServerOption.EnableMission)
        {
            var eligible = GameData.DailyMissionData.Values.Where(x => x.DailyMissionType == 1 &&
                (x.UnlockMainMission == 0 || Player.MissionManager?.GetMainMissionStatus(x.UnlockMainMission) == MissionPhaseEnum.Finish) &&
                GameData.MainMissionData.TryGetValue(x.ID, out var main) && main.MissionInfo.StartSubMissionList.Count > 0 &&
                main.MissionInfo.SubMissionList.Count > 0 && main.MissionInfo.FinishSubMissionList.Count > 0 &&
                main.MissionInfo.SubMissionList.All(s => s.FinishType is MissionFinishTypeEnum.Talk
                    or MissionFinishTypeEnum.SubMissionFinishCnt or MissionFinishTypeEnum.ConsumeMissionItem
                    or MissionFinishTypeEnum.StageWin or MissionFinishTypeEnum.KillMonster
                    or MissionFinishTypeEnum.MessageSectionFinish or MissionFinishTypeEnum.EnterFloor))
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
        {
            await RecordProgress(2100145, 1);
            await RecordProgress(2100153, 1);
        }
        foreach (var id in new[] { 2100141, 2100152 })
        {
            var config = GameData.QuestDataData.GetValueOrDefault(id);
            var finish = GameData.FinishWayData.GetValueOrDefault(config?.FinishWayID ?? 0);
            if (finish == null || battle.BattleResult.Stt == null) continue;
            var progress = battle.BattleResult.Stt.BattleTargetInfo.Values.SelectMany(x => x.BattleTargetList_)
                .Where(x => finish.ParamIntList.Contains((int)x.Id)).Sum(x => (long)x.Progress);
            await RecordProgress(id, (int)Math.Min(progress, int.MaxValue));
        }
    }

    public async ValueTask OnItemComposed(int count)
    {
        await RecordProgress(2100148, count);
        await RecordProgress(2100155, count);
    }

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
