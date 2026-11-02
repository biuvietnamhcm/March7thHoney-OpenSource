# Currency War / GridFight 4.2 integration

## Source and scope

Ported selected gameplay changes from
[Mar7thLover/March7thHoney-OpenSource](https://github.com/Mar7thLover/March7thHoney-OpenSource),
from base `d960c85336227cdbe47ee21e13aa19ab4a4b7262` to
`ac0558a3640f1dc1c77354923d57a409903b71b3` on `main`.
This includes contributions merged from rh666666 in
[PR #1](https://github.com/Mar7thLover/March7thHoney-OpenSource/pull/1).
The original source remains subject to the repository's AGPL-3.0 license.

Imported areas:

- Role ID resolution, shop selection/probability display and purchases.
- Cascading role merges, duplicate purchase replay and bench synchronization.
- Position swaps, preparation/post-query synchronization and route previews.
- Projector copying and privilege/augment handling.
- GridFight battle lineup, stage selection and stable encounter seed.
- Related GridFight command and packet handlers, including PostSync opcodes.

The 4.2 protocol definitions remain in place. No 3.7/4.6 protocol, database schema,
default account-creation setting or EnvType changes were imported.
BoxingClub and the Rogue family are separate future ports, not part of this change.

## Local adjustments

- Retained level-based deployment capacity, capped at 13, with the upstream wealth-gem bonus;
  removed unconditional access to all 13 deployed slots.
- Reject out-of-range positions and moves exceeding the deployment cap before applying the layout.
- A full bench allows a temporary purchase/copy slot only when the role can actually upgrade
  to the next star, preventing stranded maximum-star cards.
- Exhausting a shop pool no longer reintroduces owned three-star roles.
- A shop rarity fallback uses the selected role's price, matching the amount charged.
- Automatic Cyrene special-shop and Head Player/GM-console activation calls are omitted:
  upstream explicitly describes these experimental behaviors as incomplete. Supporting code
  remains where shared with the imported special-goods/trait-action handling.

## Validation (2026-10-29)

SDK: .NET 10.0.403, installed locally at
`%LOCALAPPDATA%\Codex\Tools\dotnet10` without changing the system PATH.
The SDK ZIP was verified against Microsoft's release-metadata SHA-512 hash.

Commands from the repository root (PowerShell):

```powershell
& "$env:LOCALAPPDATA/Codex/Tools/dotnet10/dotnet.exe" build March7thHoney.sln -c Release
& "$env:LOCALAPPDATA/Codex/Tools/dotnet10/dotnet.exe" run --project tests/GridFightRegression/GridFightRegression.csproj -c Release --no-launch-profile
```

With a .NET 10 SDK already on PATH, ordinary `dotnet build` / `dotnet run` work as well.

Results:

- Full Release solution build: **0 errors**, 23,713 warnings on the initial build.
- Regression runner: **15/15 scenarios passed**.
- `git diff --check`: passed.

The regression runner uses synthetic in-memory player/resource fixtures and the actual
GameServer classes. It does not start network listeners, initialize a user database or
run the application entrypoint. Scenarios cover deployment limits, ID normalization,
swaps and rejected moves, cascading merges, full benches, purchase retries, insufficient
funds, projector restrictions, shop exclusion/pricing/probabilities, protobuf round trips
and packet-handler registration. Shop exclusion is exercised over 100 refreshes.

Build warnings include nullable annotations in generated Proto/ServerSideProto code and
NuGet advisories for existing transitive MySqlConnector, SQLite and Pkcs dependencies.
Dependency versions were not changed as part of this gameplay port.

## Remaining client acceptance

No interactive 4.2 client session was tested. These checks are still required before
calling the gameplay fully verified:

1. Enter Currency War, select the opening portal and check the initial board/shop.
2. Buy, merge, move and sell roles; verify both foreground and background battle lineups.
3. Use projectors and privilege cards, including invalid targets and a full bench.
4. Complete/lose/leave a battle and verify route, rewards and synchronization.
5. Reconnect and verify the mode's existing persistence behavior and account inventory.

The automated tests validate server state and protocol serialization, not client field
interpretation, animations, combat assets or live reconnect/persistence behavior.
