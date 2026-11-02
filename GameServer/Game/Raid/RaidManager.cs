using March7thHoney.Data;
using March7thHoney.Database;
using March7thHoney.Database.Scene;
using March7thHoney.Enums.Mission;
using March7thHoney.Enums.Scene;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Server.Packet.Send.Lineup;
using March7thHoney.GameServer.Server.Packet.Send.Raid;
using March7thHoney.Proto;
using RaidData = March7thHoney.Database.Scene.RaidData;

namespace March7thHoney.GameServer.Game.Raid;

public class RaidManager : BasePlayerManager
{
    public RaidManager(PlayerInstance player) : base(player)
    {
        RaidData = DatabaseHelper.Instance!.GetInstanceOrCreateNew<RaidData>(player.Uid);
    }

    public RaidData RaidData { get; }

    #region Information

    public RaidStatus GetRaidStatus(int raidId, int worldLevel = 0)
    {
        if (!RaidData.RaidRecordDatas.TryGetValue(raidId, out var dict)) return RaidStatus.None;
        if (!dict.TryGetValue(worldLevel, out var record)) return RaidStatus.None;
        return record.Status;
    }

    #endregion

    #region Player Handler

    public async ValueTask OnLogin()
    {
        
        if (RaidData.CurRaidId > 0 && RaidData.RaidRecordDatas.TryGetValue(RaidData.CurRaidId, out var value))
        {
            if (value.TryGetValue(RaidData.CurRaidWorldLevel, out var record))
            {
                await Player.SendPacket(new PacketRaidInfoNotify(record));
            }
            else
            {
                RaidData.CurRaidId = 0;
                RaidData.CurRaidWorldLevel = 0;
                await Player.SendPacket(new PacketRaidInfoNotify());
            }
        }
        else
        {
            RaidData.CurRaidId = 0;
            RaidData.CurRaidWorldLevel = 0;
            await Player.SendPacket(new PacketRaidInfoNotify());
        }
    }

    #endregion

    #region Player Action

    public async ValueTask<RaidRecord?> EnterRaid(int raidId, int worldLevel, List<int>? avatarList = null,
        bool enterSaved = false)
    {
        if (RaidData.CurRaidId != 0) return null;

        GameData.RaidConfigData.TryGetValue(raidId * 100 + worldLevel, out var excel);
        if (excel == null) return null; 

        RaidData.RaidRecordDatas.TryGetValue(raidId, out var dict);
        dict ??= [];
        if (dict.ContainsKey(worldLevel) && !enterSaved)
            
            await ClearRaid(raidId, worldLevel);
        dict.TryGetValue(worldLevel, out var record);

        RaidData.CurRaidId = excel.RaidID;
        RaidData.CurRaidWorldLevel = worldLevel;

        if (record == null)
        {
            
            var entranceId = 0;
            var firstMission = excel.MainMissionIDList[0];
            var subMissionId =
                GameData.MainMissionData[firstMission].MissionInfo!.StartSubMissionList[0]; 
            var subMission = GameData.SubMissionInfoData[subMissionId];

            entranceId =
                int.Parse(subMission.SubMissionInfo!.LevelFloorID.ToString()
                    .Replace("00", "0")); 

            if (!GameData.MapEntranceData.ContainsKey(entranceId)) entranceId = subMission.SubMissionInfo!.LevelFloorID;

            if (avatarList?.Count > 0)
            {
                Player.LineupManager!.SetExtraLineup(ExtraLineupType.LineupHeliobus, avatarList);
                await Player.SendPacket(new PacketSyncLineupNotify(Player.LineupManager!.GetCurLineup()!));
            }
            else if (excel.TeamType == RaidTeamTypeEnum.TrialOnly)
            {
                
                List<int> list = [..excel.TrialAvatarList];
                if (list.Count > 0)
                {
                    if (Player.Data.CurrentGender == Gender.Man)
                    {
                        foreach (var avatar in excel.TrialAvatarList)
                            if (avatar > 10000) 
                                if (avatar.ToString().EndsWith("8002") ||
                                    avatar.ToString().EndsWith("8004") ||
                                    avatar.ToString().EndsWith("8006"))
                                    list.Remove(avatar);
                    }
                    else
                    {
                        foreach (var avatar in excel.TrialAvatarList)
                            if (avatar > 10000) 
                                if (avatar.ToString().EndsWith("8001") ||
                                    avatar.ToString().EndsWith("8003") ||
                                    avatar.ToString().EndsWith("8005"))
                                    list.Remove(avatar);
                    }
                }

                Player.LineupManager!.SetExtraLineup(ExtraLineupType.LineupHeliobus, list);
                if (excel.LockCaptain) Player.LineupManager!.GetCurLineup()!.LeaderAvatarId = excel.LockCaptainAvatarID;
                await Player.SendPacket(new PacketSyncLineupNotify(Player.LineupManager!.GetCurLineup()!));
            }
            else
            {
                
                var lineup = Player.LineupManager!.GetCurLineup()!;
                Player.LineupManager!.SetExtraLineup(ExtraLineupType.LineupHeliobus,
                    lineup.BaseAvatars!.Select(x => x.SpecialAvatarId > 0 ? x.SpecialAvatarId / 10 : x.BaseAvatarId)
                        .ToList());
                await Player.SendPacket(new PacketSyncLineupNotify(Player.LineupManager!.GetCurLineup()!));
            }

            var oldEntryId = Player.Data.EntryId;
            var oldPos = Player.Data.Pos;
            var oldRot = Player.Data.Rot;

            await Player.MissionManager!.AcceptMainMission(firstMission);

            await Player.EnterScene(entranceId, 0, false);

            record = new RaidRecord
            {
                PlaneId = Player.Data.PlaneId,
                FloorId = Player.Data.FloorId,
                EntryId = entranceId,
                Pos = Player.Data.Pos!,
                Rot = Player.Data.Rot!,
                Status = RaidStatus.Doing,
                WorldLevel = worldLevel,
                RaidId = raidId,
                Lineup = Player.LineupManager!.GetCurLineup()!.BaseAvatars!,
                OldEntryId = oldEntryId,
                OldPos = oldPos!,
                OldRot = oldRot!
            };

            if (RaidData.RaidRecordDatas.TryGetValue(raidId, out var value))
                value[worldLevel] = record;
            else
                RaidData.RaidRecordDatas[raidId] = new Dictionary<int, RaidRecord> { { worldLevel, record } };
        }
        else
        {
            
            record.Status = RaidStatus.Doing;
            Player.LineupManager!.SetExtraLineup(ExtraLineupType.LineupHeliobus,
                record.Lineup.Select(x => x.SpecialAvatarId > 0 ? x.SpecialAvatarId : x.BaseAvatarId).ToList());
            await Player.LoadScene(record.PlaneId, record.FloorId, record.EntryId, record.Pos, record.Rot, false);
        }

        await Player.SendPacket(new PacketRaidInfoNotify(record));
        return record;
    }

    public async ValueTask CheckIfLeaveRaid()
    {
        if (RaidData.CurRaidId == 0) return;

        var record = RaidData.RaidRecordDatas[RaidData.CurRaidId][RaidData.CurRaidWorldLevel];

        GameData.RaidConfigData.TryGetValue(RaidData.CurRaidId * 100 + record.WorldLevel, out var excel);
        if (excel == null) return;
        var leave = true;
        foreach (var id in excel.MainMissionIDList)
            if (Player.MissionManager!.GetMainMissionStatus(id) != MissionPhaseEnum.Finish)
                leave = false;

        if (leave)
        {
            await FinishRaid();
            
            await Player.MissionManager!.HandleFinishType(MissionFinishTypeEnum.RaidFinishCnt);
        }
    }

    public async ValueTask FinishRaid()
    {
        if (RaidData.CurRaidId == 0) return;

        var record = RaidData.RaidRecordDatas[RaidData.CurRaidId][RaidData.CurRaidWorldLevel];
        GameData.RaidConfigData.TryGetValue(RaidData.CurRaidId * 100 + record.WorldLevel, out var config);
        if (config == null) return;

        record.Status = RaidStatus.Finish;

        await Player.SendPacket(new PacketRaidInfoNotify(record));
    }

    public async ValueTask LeaveRaid(bool save)
    {
        if (RaidData.CurRaidId == 0) return;

        var record = RaidData.RaidRecordDatas[RaidData.CurRaidId][RaidData.CurRaidWorldLevel];
        GameData.RaidConfigData.TryGetValue(RaidData.CurRaidId * 100 + record.WorldLevel, out var config);
        if (config == null) return;

        record.PlaneId = Player.Data.PlaneId;
        record.FloorId = Player.Data.FloorId;
        record.EntryId = Player.Data.EntryId;
        record.Pos = Player.Data.Pos!;
        record.Rot = Player.Data.Rot!;

        if (Player.LineupManager!.GetCurLineup()!.IsExtraLineup())
        {
            Player.LineupManager!.SetExtraLineup(ExtraLineupType.LineupNone, []);
            await Player.SendPacket(new PacketSyncLineupNotify(Player.LineupManager!.GetCurLineup()!));
        }

        if (record.Status == RaidStatus.Finish)
        {
            await Player.SendPacket(new PacketRaidInfoNotify());
            if (config.FinishEntranceID > 0)
            {
                await Player.EnterScene(config.FinishEntranceID, 0, true);
            }
            else
            {
                await Player.EnterScene(record.OldEntryId, 0, true);
                await Player.MoveTo(record.OldPos, record.OldRot);
            }
        }
        else
        {
            await Player.EnterScene(record.OldEntryId, 0, true);
            await Player.MoveTo(record.OldPos, record.OldRot);

            

            await Player.SendPacket(new PacketRaidInfoNotify());

            if (!save) await ClearRaid(record.RaidId, record.WorldLevel);
        }

        RaidData.CurRaidId = 0;
        RaidData.CurRaidWorldLevel = 0;
    }

    public async ValueTask ClearRaid(int raidId, int worldLevel)
    {
        if (!RaidData.RaidRecordDatas.TryGetValue(raidId, out var dict)) return;
        if (!dict.TryGetValue(worldLevel, out var record)) return;

        GameData.RaidConfigData.TryGetValue(raidId * 100 + worldLevel, out var config);
        if (config == null) return;

        HashSet<int> floorIds = [];
        foreach (var missionId in config.MainMissionIDList)
        {
            await Player.MissionManager!.RemoveMainMission(missionId);

            GameData.MainMissionData.TryGetValue(missionId, out var mission);
            if (mission?.MissionInfo.SubMissionList == null) continue;
            foreach (var i in mission.MissionInfo.SubMissionList.Select(x => x.LevelFloorID).ToHashSet())
                floorIds.Add(i);
        }

        dict.Remove(worldLevel);

        if (dict.Count == 0) RaidData.RaidRecordDatas.Remove(raidId);

        
        foreach (var floorId in floorIds)
        {
            Player.SceneData!.PropTimelineData.Remove(floorId);
            Player.SceneData!.GroupPropertyData.Remove(floorId);
            Player.SceneData!.FloorTargetPuzzleGroupData.Remove(floorId);
            Player.SceneData!.FloorSavedData.Remove(floorId);
            if (Player.SceneData!.ScenePropData.TryGetValue(floorId, out var groups))
            {
                // Shared floors can contain permanent exploration chests as well as raid state.
                foreach (var groupId in groups.Keys.ToList())
                {
                    groups[groupId].RemoveAll(prop => !prop.ChestOpened);
                    if (groups[groupId].Count == 0) groups.Remove(groupId);
                }
                if (groups.Count == 0) Player.SceneData.ScenePropData.Remove(floorId);
            }
        }

        await Player.SendPacket(new PacketDelSaveRaidScNotify(raidId, worldLevel));
    }

    #endregion
}
