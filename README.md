# March7thHoney

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-2ea44f" alt="Platform" />
  <img src="https://img.shields.io/badge/Status-Active%20Development-f39c12" alt="Status" />
  <img src="https://img.shields.io/badge/License-GPLv3-blue" alt="License" />
</p>

March7thHoney is a server project built with .NET 10.
The project is actively developed, with regular feature updates and fixes.

[EN](README.md) | [简中](docs/README_zh-CN.md) | [繁中](docs/README_zh-TW.md) | [JP](docs/README_ja-JP.md)

## Table of Contents

- [Feature Progress](#feature-progress)
- [Gameplay Implementation Tree](docs/GameplayImplementationTree.md)
- [Muip API Entry](#muip-api-entry)
- [Roadmap](#roadmap)
- [Repository Layout](#repository-layout)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Build](#build)
- [CI Artifacts](#ci-artifacts)
- [Notes](#notes)
- [Troubleshooting](#troubleshooting)
- [Credits](#credits)

## Feature Progress

| Module | Status | Notes |
|---|---|---|
| Shop system | Stable | Browse and purchase flow works |
| Team formation | Stable | Lineup and slot switching available |
| Warp/Gacha | In progress | Separate pity, guarantees, Departure, Celestial selection and 300-Warp reward; client UI validation pending |
| Daily Training | In progress | Resource-based rotation, persisted progress/rewards and NPC daily objectives |
| Battle flow | Stable | Core combat loop is playable |
| Open-world scene | Stable | Scene loading, interaction, exploration |
| Character progression | Stable | Leveling and promotion loops available |
| Quest system | In progress | Main/common progression works, edge cases remain |
| Friend system | Stable | Core interaction and display |
| Anomaly Arbitration | Stable | Entry/challenge/reward supported |
| Challenge modes | Stable | FH / PF / AS playable |
| Currency War | In progress | Basic gameplay flow available |
| Achievement system | Stable | Most achievements supported |

Detailed implementation progress: [Gameplay Implementation Tree](docs/GameplayImplementationTree.md) | [简中](docs/GameplayImplementationTree_zh-CN.md) | [繁中](docs/GameplayImplementationTree_zh-TW.md) | [JP](docs/GameplayImplementationTree_ja-JP.md)

4.2 behavior and verification: [Daily Training (Vietnamese)](docs/DailyActivity_vi.md) | [Warp/Gacha (Vietnamese)](docs/Gacha42_vi.md).

Client connection: [Single-player login (Vietnamese)](docs/SinglePlayerLogin_vi.md) | [Optional mitmproxy setup (Vietnamese)](docs/Proxy_vi.md).

## Muip API Entry

- Muip API docs: [English](docs/MuipAPI.md) | [简中](docs/MuipAPI_zh-CN.md) | [繁中](docs/MuipAPI_zh-TW.md) | [日本語](docs/MuipAPI_ja-JP.md)


## Roadmap

1. Improve Currency War and Divergent Universe.
2. Fill remaining quests and special trigger logic.
3. Improve settlement/sync stability in edge cases.
4. Expand event-mode gameplay coverage.
5. Continue fixing UI and state-sync issues.
6. Add more configurable server options.

## Repository Layout

- `Program/` startup entry and orchestration
- `GameServer/` gameplay logic, packet handlers, runtime managers
- `WebServer/` dispatch/http side services
- `Command/` command implementations
- `Common/` shared data models, config, enums, and database entities
- `Config/` runtime resources and custom data
- `docs/` multilingual docs and API docs

## Requirements

- .NET SDK 10
- Git
- Windows or Linux (`x64` / `arm64`)

## Quick Start

```bash
git clone --recurse-submodules https://github.com/Mar7thLover/March7thHoney.git
cd March7thHoney
dotnet restore
dotnet run --project program
```

## Build

```bash
dotnet build program/program.csproj -c Release
```

For a standalone Windows x64 server, place the game resource pack in `Resources/`
and run `Build-Standalone.bat`. The output is `artifacts/standalone/win-x64/`;
launch `Start-Server.bat` there. Copy the entire output directory to another PC;
no .NET installation is required on that PC. Resources and build outputs are ignored by Git.
See [standalone build instructions (Vietnamese)](docs/StandaloneBuild_vi.md).

## CI Artifacts

Workflow publishes self-contained builds for:

| Runtime | Artifact Name |
|---|---|
| `win-x64` | `March7thHoney-win-x64` |
| `linux-x64` | `March7thHoney-linux-x64` |
| `linux-arm64` | `March7thHoney-linux-arm64` |

## Notes

- If behavior looks inconsistent after updating commits, clear runtime cache/data and retest with a fresh account state first.
- Some modules are still under refactor, so packet behavior may change before docs fully catch up.

## Troubleshooting

- Check server logs under your configured log path.
- `/scene reload` can solve most of the problem.
- Community support: <https://discord.gg/castoriceps>

## Credits
- Commit history details: [History Commits](docs/History%20Commits.md)
### Collaborators

- [Mar7thLover](https://github.com/Mar7thLover)
- [Cyrup](https://github.com/March7thHoney)
- [Melioli](https://github.com/Melioli)

### Contributors

- [Yuki](https://github.com/Yuki8859)
- [Cyt](https://github.com/qinfyy)

### Projects

- [SqlSugar](https://github.com/donet5/SqlSugar)
- [LunarCore](https://github.com/Melledy/LunarCore)
- [DanhengServer](https://github.com/Mar7thLover/DanhengServer-OpenSource)
