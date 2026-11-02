using March7thHoney.Proto;

namespace March7thHoney.GameServer.Game.GridFight.Sync;

public static class GridFightBootstrapSyncBuilder
{
    public static GridFightSyncUpdateResultScNotify Build()
    {
        var notify = new GridFightSyncUpdateResultScNotify();

        var augmentSync = new GridFightSyncResultData();
        var augmentItem = new GridFightSyncData { GMJLJDJDIGM = new JPBCKCDEGOM() };
        augmentItem.GMJLJDJDIGM.ALIDDLBDPDH.Add(new ELEOGABGBKG { DMEKIFJDKFL = 8007, CELFGCJFMPH = { 8007u, 8009u } });
        augmentItem.GMJLJDJDIGM.ALIDDLBDPDH.Add(new ELEOGABGBKG { DMEKIFJDKFL = 11011, CELFGCJFMPH = { 11011u, 11012u } });
        augmentSync.UpdateDynamicList.Add(augmentItem);
        notify.SyncResultDataList.Add(augmentSync);

        var portalAndRoleSync = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpHndkhmefaal };
        portalAndRoleSync.UpdateDynamicList.Add(new GridFightSyncData { PortalServerDataUpdate = new GridFightPortalServerDataUpdate { PortalBuffId = 111 } });
        notify.SyncResultDataList.Add(portalAndRoleSync);

        var addRoleSync = new GridFightSyncResultData { GridUpdateSrc = GridFightUpdateSrcType.LnpfefkjdhpBjdeaahibge };
        addRoleSync.UpdateDynamicList.Add(new GridFightSyncData { ItemValue = 3 });
        addRoleSync.UpdateDynamicList.Add(new GridFightSyncData { AddRoleInfo = new GridGameRoleInfo { Id = 1009, Pos = 14, RoleStar = 1, UniqueId = 3, GridFightValueInitComponent = { ["GP_Avatar_Asta_01"] = 0 } } });
        addRoleSync.UpdateDynamicList.Add(new GridFightSyncData { AddRoleInfo = new GridGameRoleInfo { Id = 1306, Pos = 15, RoleStar = 1, UniqueId = 4, GridFightValueInitComponent = { ["GP_Avatar_Sparkle_01"] = 0 } } });
        addRoleSync.UpdateDynamicList.Add(new GridFightSyncData { AddRoleInfo = new GridGameRoleInfo { Id = 1403, Pos = 16, RoleStar = 1, UniqueId = 5, GridFightValueInitComponent = { ["GP_Avatar_Tribbie_01"] = 0 } } });
        addRoleSync.UpdateDynamicList.Add(new GridFightSyncData { AddRoleInfo = new GridGameRoleInfo { Id = 1502, Pos = 17, RoleStar = 1, UniqueId = 6, GridFightValueInitComponent = { ["GP_Avatar_YaoGuang_01"] = 0 } } });
        notify.SyncResultDataList.Add(addRoleSync);

        var finishPendingSync = new GridFightSyncResultData();
        finishPendingSync.UpdateDynamicList.Add(new GridFightSyncData { FinishPendingActionPos = 1 });
        finishPendingSync.UpdateDynamicList.Add(new GridFightSyncData { SyncLockInfo = new GridFightLockInfo() });
        notify.SyncResultDataList.Add(finishPendingSync);

        var shopSync = new GridFightSyncResultData();
        shopSync.UpdateDynamicList.Add(new GridFightSyncData
        {
            ShopSyncInfo = new GridFightShopSyncInfo
            {
                GLIFNMBMMBL = 2,
                LDEDGOOKHFL = GridFightInstance.BuildShopRarityDisplayInfoForLevel(3),
                ShopGoodsList =
                {
                    new GridFightShopGoodsInfo { ShopGoodsPrice = 1, RoleGoodsInfo = new GridFightRoleGoodsInfo { RoleId = 1217, RoleStar = 1 } },
                    new GridFightShopGoodsInfo { ShopGoodsPrice = 1, RoleGoodsInfo = new GridFightRoleGoodsInfo { RoleId = 1314, RoleStar = 1 } },
                    new GridFightShopGoodsInfo { ShopGoodsPrice = 1, RoleGoodsInfo = new GridFightRoleGoodsInfo { RoleId = 1108, RoleStar = 1 } },
                    new GridFightShopGoodsInfo { ShopGoodsPrice = 1, RoleGoodsInfo = new GridFightRoleGoodsInfo { RoleId = 1220, RoleStar = 1 } },
                    new GridFightShopGoodsInfo { ShopGoodsPrice = 1, RoleGoodsInfo = new GridFightRoleGoodsInfo { RoleId = 1406, RoleStar = 1 } }
                }
            }
        });
        notify.SyncResultDataList.Add(shopSync);

        var lockAndPendingSync = new GridFightSyncResultData();
        lockAndPendingSync.UpdateDynamicList.Add(new GridFightSyncData
        {
            SyncLockInfo = new GridFightLockInfo
            {
                LockReason = GridFightLockReason.DfofffceffoKjmjdbjmbmc,
                LockType = GridFightLockType.PjbmhhnlclbEhfhdgpocnh
            }
        });
        lockAndPendingSync.UpdateDynamicList.Add(new GridFightSyncData
        {
            PendingAction = new GridFightPendingAction
            {
                QueuePosition = 2,
                RoundBeginAction = new GridFightRoundBeginActionInfo()
            }
        });
        notify.SyncResultDataList.Add(lockAndPendingSync);

        var finishReturnSync = new GridFightSyncResultData();
        finishReturnSync.UpdateDynamicList.Add(new GridFightSyncData { FinishPendingActionPos = 2 });
        finishReturnSync.UpdateDynamicList.Add(new GridFightSyncData { SyncLockInfo = new GridFightLockInfo() });
        notify.SyncResultDataList.Add(finishReturnSync);

        var returnPendingSync = new GridFightSyncResultData();
        returnPendingSync.UpdateDynamicList.Add(new GridFightSyncData
        {
            SyncLockInfo = new GridFightLockInfo
            {
                LockReason = GridFightLockReason.DfofffceffoKjmjdbjmbmc,
                LockType = GridFightLockType.PjbmhhnlclbEhfhdgpocnh
            }
        });
        returnPendingSync.UpdateDynamicList.Add(new GridFightSyncData
        {
            PendingAction = new GridFightPendingAction
            {
                QueuePosition = 8,
                ReturnPreparationAction = new GridFightReturnPreparationActionInfo()
            }
        });
        notify.SyncResultDataList.Add(returnPendingSync);
        return notify;
    }
}
