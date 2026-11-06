# Daily regression checks

Run from the repository root with .NET 10 and the supplied resource pack:

```powershell
dotnet run --project tests/DailyRegression -c Release -- Resources
```

The runner creates an isolated temporary SQLite database, loads the real daily/reward/item tables and one NPC daily graph, and calls production managers. It never starts a server or opens the configured player database.

The 21 scenarios cover resource tiers/packet registration, unlock conditions, quest point claims, stamina quantity, concurrent milestone claims, claim-all, missing resources, database reload, reset boundaries and clock rollback, real NPC dialogue progression, cumulative battle targets, client-request guards, level brackets, crafting costs, item hand-in and legacy data.

Resource test fixtures use NPC `3000201` and the actual reward rows for World Levels 2 and 6. Battle results are simulated events; this is not client/combat/UI validation. Temporary database paths are printed for inspection.

Additional 4.2 checks cover rotating modern resource pools, farm-type wins, photo-only client reports, real NPC prop interaction 3011601 and selected item hand-in 3011605.

## MySQL

Both suites also pass against MySQL 8.4.11. Create a fresh, disposable schema whose name starts with `march7th_test_`, then set `MARCH7TH_TEST_MYSQL_CONNECTION` to its connection string (with an explicit `Database` key) before running the command above. Use a new schema for each run; fixed test UIDs are intentional. Unset the environment variable to return to SQLite. Never point the runner at a player database.

The runner uses the same JSON column mapping as production and creates all model tables. The gacha suite additionally checks upgrading a populated history column from TEXT to LONGTEXT and saving/reloading 4,000 records beyond the 64 KiB TEXT limit. Gacha: 18/18; daily: 21/21 on each backend. These checks do not exercise recovery from crashes during reward delivery.
