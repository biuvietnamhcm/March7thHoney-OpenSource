# GridFight regression runner

Run from the repository root with .NET 10:

```powershell
dotnet run --project tests/GridFightRegression/GridFightRegression.csproj -c Release --no-launch-profile
```

This is an executable regression runner (not a `dotnet test` adapter project).
It exits nonzero on failure and prints the result of each scenario.
It references the actual GameServer implementation, uses in-memory resource/player
fixtures, and does not open ports or access an account database.

See `docs/GridFight42Integration.md` for upstream attribution, scope and client-test limitations.
