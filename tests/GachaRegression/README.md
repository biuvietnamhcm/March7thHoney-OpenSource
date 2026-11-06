# Gacha regression checks

Run from the repository root:

```powershell
dotnet run --project tests/GachaRegression -c Release -- Resources
```

Loads the actual 4.2 resource pack and banner configuration, creates an isolated temporary SQLite database, and uses injected RNG/time to cover pity boundaries, pool membership, guarantees, tickets, concurrent requests, Departure, duplicate conversion, Celestial Invitation, the standard 300 selector, migration and persistence. No server is started and no player database is opened. Tests verify server rules and protobuf state, not the live client UI or the unpublished official RNG distribution.

## MySQL

Both suites also pass against MySQL 8.4.11. Create a fresh, disposable schema whose name starts with `march7th_test_`, then set `MARCH7TH_TEST_MYSQL_CONNECTION` to its connection string (with an explicit `Database` key) before running the command above. Use a new schema for each run; fixed test UIDs are intentional. Unset the environment variable to return to SQLite. Never point the runner at a player database.

The runner uses the same JSON column mapping as production and creates all model tables. The gacha suite additionally checks upgrading a populated history column from TEXT to LONGTEXT and saving/reloading 4,000 records beyond the 64 KiB TEXT limit. Gacha: 18/18; daily: 21/21 on each backend. These checks do not exercise recovery from crashes during reward delivery.
