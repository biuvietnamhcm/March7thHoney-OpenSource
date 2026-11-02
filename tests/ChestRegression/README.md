# Map chest regression checks

Run from the repository root with .NET 10:

```powershell
dotnet run --project tests/ChestRegression -c Release
# Also exercise a chest from the supplied resource pack:
dotnet run --project tests/ChestRegression -c Release -- Resources
```

The executable calls the production scene loader, interaction handler, inventory manager and database save code. It creates a unique SQLite file under the system temporary directory and prints its path; it never starts the server or opens the configured player database.

The 11 synthetic scenarios cover one-time rewards, concurrent duplicate requests, Reset groups, puzzle state changes, scene reload, independent floor/group/config IDs and accounts, invalid interactions, missing resources, legacy saves, instance reset behavior, SQLite reload including map marks, and raid cleanup on shared floors.

The optional resource scenario uses plane `10101`, floor `10101001`, group `161`, chest `10101601`, and the pack's interaction and item definitions. It checks the actual loader/reward path and SQLite reload. This does not exercise the game client's animation or the entire puzzle/mission graph.
