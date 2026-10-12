# Q97 v2.1 Part 1 — environment readiness (WP-MVP6-ENV-097, AL-MVP6-ENV-097)

Measured on the owner's Mac in a local Claude Code terminal session, 2026-09-26 (captured 19:29:47Z; raw: `env-raw.txt`).
Evidence level (SOP §32.0): **measured locally (L-measured), not runtime acceptance.**

## Preflight (SOP §20)

| Check | Expected | Measured | Result |
|---|---|---|---|
| `uname -s` | Darwin | Darwin (arm64, kernel 25.5.0) | PASS |
| `sw_vers` | — | macOS 26.5.2 (25F84) | recorded |
| `dotnet --info` | SDK 8.0.417 / runtime 8.0.23 | SDK 8.0.417; Microsoft.NETCore.App 8.0.23; Microsoft.AspNetCore.App 8.0.23; no other SDK/runtime; arm64 | PASS |
| `GIT_OPTIONAL_LOCKS=0` | set in every shell | set in every lane shell; kit git calls are read-only (`ek_git`) | PASS |
| `.git/index.lock` | absent | absent (start and end) | PASS |
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b…` | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` | PASS |
| `git diff --name-only HEAD` | 19 paths | 19 paths (list in the Q64b report §1) | PASS |

## Tools

| Item | Version / detail | State |
|---|---|---|
| .NET SDK / runtime | 8.0.417 / 8.0.23 (`/Users/natig/.dotnet`) | PRESENT |
| node / npm | v25.5.0 / 11.8.0 (`/opt/homebrew/bin`) | PRESENT |
| python3 | 3.9.6 (`/usr/bin/python3`) | PRESENT |
| python `bcrypt` (kit K07) | not in the system python → installed into a lane venv `~/mvp6-env/venv` (bcrypt 5.0.0, no system change) | MISSING system-wide → PRESENT in lane venv |
| git | 2.50.1 (Apple Git-155) | PRESENT |
| MongoDB server `mongod` | 8.0.18 (`/opt/homebrew/bin/mongod`) | PRESENT |
| `mongosh` | 2.6.0 | PRESENT |
| legacy `mongo` shell | — | MISSING (not needed: kit uses mongosh) |
| container runtime (docker / podman / colima) | — | MISSING (not needed: native mongod) |
| Playwright (node) | 1.59.1 + `@playwright/test` 1.59.1, installed locally in `~/mvp6-env/node_modules` (npm cache had 1.59.1) | PRESENT (lane-local) |
| Chromium for Playwright | revision 1217, "Google Chrome for Testing 147.0.7727.15" (`~/Library/Caches/ms-playwright/chromium-1217`) | PRESENT |
| Google Chrome.app, MongoDB Compass.app | installed | PRESENT (not used) |

## Capacity

| Item | Value | Note |
|---|---|---|
| CPU | Apple M1, 8 cores | — |
| RAM | 8 GiB (8,589,934,592 bytes) | six .NET services + mongod + headless Chromium fit, with little headroom |
| Disk (home volume) | 460 GiB, 23 GiB free at start → 19 GiB free after composing the tree and first builds (95–96 % used) | enough for two ~0.85 GB trees + builds; owner should watch free space |
| Operational MongoDB | a `mongod` listens on 127.0.0.1:27017 (owner's local DB) | never used by this lane; kit netcheck proves 0 connections |

## Owner commands (nothing was installed system-wide; no sudo)

- None required for this lane. For a system-wide K07 prerequisite the owner could run `python3 -m pip install --user bcrypt`;
  the lane used a private venv instead: `python3 -m venv ~/mvp6-env/venv && ~/mvp6-env/venv/bin/pip install bcrypt`.
