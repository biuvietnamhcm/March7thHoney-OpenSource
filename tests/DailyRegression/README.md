# Daily regression checks

Run from the repository root with .NET 10 and the supplied resource pack:

```powershell
dotnet run --project tests/DailyRegression -c Release -- Resources
```

The runner creates an isolated temporary SQLite database, loads the real daily/reward/item tables and one NPC daily graph, and calls production managers. It never starts a server or opens the configured player database.

The 16 scenarios cover resource tiers/packet registration, unlock conditions, quest point claims, stamina quantity, concurrent milestone claims, claim-all, missing resources, SQLite reload, reset boundaries and clock rollback, real NPC dialogue progression, cumulative battle targets, client-request guards, level brackets, crafting costs, item hand-in and legacy data.

Resource test fixtures use NPC `3000201` and the actual reward rows for World Levels 2 and 6. Battle results are simulated events; this is not client/combat/UI validation. Temporary database paths are printed for inspection.
