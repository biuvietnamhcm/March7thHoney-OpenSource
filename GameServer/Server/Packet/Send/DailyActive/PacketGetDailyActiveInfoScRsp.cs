using March7thHoney.GameServer.Game.DailyActive;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Send.DailyActive;

public class PacketGetDailyActiveInfoScRsp : BasePacket
{
    public PacketGetDailyActiveInfoScRsp(DailyActivityManager manager) : base(CmdIds.GetDailyActiveInfoScRsp)
    {
        var proto = new GetDailyActiveInfoScRsp
        {
            DailyActivePoint = (uint)manager.Data.Points
        };
        proto.DailyActiveLevelList.AddRange(manager.ToLevels());
        proto.DailyActiveQuestIdList.AddRange(manager.Data.QuestIds.Select(x => (uint)x));
        SetData(proto);
    }
}
