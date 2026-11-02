using March7thHoney.GameServer.Game.GridFight.Sync;
using March7thHoney.GameServer.Game.GridFight;
using March7thHoney.GameServer.Server.Packet.Send.GridFight;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.GridFight;

[Opcode(CmdIds.GridFightBuyGoodsCsReq)]
public class HandlerGridFightBuyGoodsCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = GridFightBuyGoodsCsReq.Parser.ParseFrom(data);
        var player = connection.Player!;
        var service = new GridFightService(player);
        var (roleId, roleUniqueId, pos, goldDelta, merges, benchMoved, duplicateRetry, purchasedAugmentId, purchasedAugmentTargetUniqueId, retcode) = service.BuyGoods(req.BuyGoodsIndexList);

        var shopIndex = req.BuyGoodsIndexList.Count > 0 ? req.BuyGoodsIndexList[0] : 0u;
        if (retcode == Retcode.RetSucc)
        {
            await connection.SendPacket(new PacketGridFightSyncUpdateResultScNotify(player, GridFightSyncKind.BuyGoods,
                new BuyGoodsSyncPayload
                {
                    RoleId = roleId,
                    RoleUniqueId = roleUniqueId,
                    Pos = pos,
                    GoldDelta = goldDelta,
                    MergedRemoved = merges.SelectMany(m => m.RemovedUniqueIds).ToList(),
                    MergedKeepUid = merges.Count > 0 ? merges[^1].KeptUniqueId : 0,
                    MergedNewStar = merges.Count > 0 ? merges[^1].NewStar : 0,
                    ShopIndex = shopIndex,
                    Merges = merges,
                    BenchRepositions = benchMoved,
                    DuplicateRetry = duplicateRetry,
                    PurchasedAugmentId = purchasedAugmentId,
                    PurchasedAugmentTargetUniqueId = purchasedAugmentTargetUniqueId
                }));

            await GridFightMergeSyncHelper.SendMergeSyncsAsync(
                connection,
                player,
                merges,
                GridFightUpdateSrcType.LnpfefkjdhpKkpbagfhpbb);

        }

        var rsp = new BasePacket((ushort)CmdIds.CEFIMADBIBH);
        rsp.SetData(new CEFIMADBIBH { Retcode = (uint)retcode });
        await connection.SendPacket(rsp);
    }
}
