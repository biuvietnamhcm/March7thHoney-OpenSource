using March7thHoney.GameServer.Game.GridFight.Sync;
namespace March7thHoney.GameServer.Game.GridFight.Sync;

public static class GridFightSyncKind
{
    public const int Bootstrap = 0;
    public const int PendingAdvance = 1;
    public const int SelectEquip = 2;
    public const int BuyGoods = 3;
    public const int RefreshShop = 4;
    public const int RecycleRole = 5;
    public const int BuyExp = 6;
    public const int PostBattle = 7;
    public const int NoOp = 8;
    public const int PreSettle = 9;
    public const int Preparation = 10;
    public const int PosUpdate = 11;
}
