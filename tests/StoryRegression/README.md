# Opening story regression checks

Run from the repository root with the supplied resources and .NET 10:

```powershell
dotnet run --project tests/StoryRegression -c Release -- Resources
```

The runner creates an isolated SQLite database in the system temporary directory. It does not start a gateway, connect a game client, or open the configured user database.

Six scenarios check:

- Reading basic info must not mark character selection complete just because a hero avatar exists.
- Main-mission acceptance waits for an asynchronously suspended child initialization.
- A missing final-objective list cannot complete a main mission automatically.
- Trial-avatar objectives require the expected roster changes.
- A prop-state task awaits its asynchronous target resolver.
- The real `1000101` graph progresses into `1000201`, rejects out-of-order dialogue and premature chapter acceptance, validates battle/monster identity, waits for both parallel dialogue branches, adds/removes the trial party, and reloads progress from SQLite without an explicit test-side save.

The last scenario uses the production mission/resource/task loaders with the resource pack's main/partial mission files and trial-avatar definitions. It supplies external events (dialogue completion, prop states, defeated monsters, battle results, character creation and entering the next floor) headlessly. It does **not** prove client cinematics, character-creation packets, combat simulation, full map loading or the remaining station chapters work end-to-end.
