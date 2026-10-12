# Q217 — Build

## Base stack (R9)

`docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md`
sha256 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882`.
`shasum -a 256 -c SHA256SUMS` in that folder: `BASE-STACK-v2.md: OK`, `LAYERS.tsv: OK`.
The prompt quoted no hash to compare against; the value above is what is on disk.

## Other build in flight

None. Before the build: no `dotnet` / `msbuild` / `vstest` / `testhost` process. Only two `mongod`
(pid 825 on 27017, pid 2363 on 31994).

## Builds

Command (both): `dotnet build services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Diten.SupplyChainService.Api.csproj -c Debug`
SDK 8.0.417.

| # | When (+03) | Flags | Errors | Warnings | Time |
|---|---|---|---|---|---|
| 1 | 23:01:23 → 23:01:28 | incremental | 0 | 0 | 4.70 s |
| 2 | after the runs | `--no-incremental` | 0 | 0 | 9.62 s |

Build 1 found everything up to date (the binaries were dated 18:59), so its warning count says little.
Build 2 recompiled all eight projects and is the real warning count: **0 errors, 0 warnings**.

Projects built: BuildingBlocks.ModuleRegistration.Abstractions, BuildingBlocks.Security.Secrets,
BuildingBlocks.Eventing, SupplyChainService.Domain, .Application, .Infrastructure, .Persistence, .Api.

## The binary that ran is the current source

| File (`…Api/bin/Debug/net8.0/`) | sha256 before build 2 (the binary that ran) | sha256 after build 2 |
|---|---|---|
| `Diten.SupplyChainService.Api.dll` | `5d6068706a3b35d9656e36ec53dcaa31a14185f3f2ff75c7a34a20d3bbd51dde` | identical |
| `Diten.SupplyChainService.Application.dll` | `4f520b34c61c9e1d1b714551521c6e8948200df616bc58642b53fb241392d21c` | identical |

A full recompile reproduced both files byte for byte, so the runs in `STARTUP-VERDICT.md` used binaries
equal to the tree at the time of this WP.

## Side effects

`bin/` and `obj/` under the eight projects were rewritten (git-ignored). `git status --porcelain` stayed at
665 lines before and after.
