# SOP §22 — MVP6-SHIPMENT-A12-RUNTIME-INDEPENDENT-VER-01

Role: MVP6 Lane-1 independent A12 runtime verifier (did not write the A12 patch).
Result: **STOPPED AT PREFLIGHT — runtime NOT RUN.** Bounded result returned to CT; CT decides.

## Identity

- Repo `/Users/natig/Projects/ERP-vNext-recovery`, branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c` — **matches** expected (`raw/01-preflight-probe.txt`).
- Short-status count 343 at first probe, 348 at second probe (includes this new record directory). Dirty entries are user/other-lane work; not touched and not used as source.

## Blocker (reason for stop)

The only shell available to this verifier on the Mac is an isolated Linux VM (Ubuntu 22.04.5, aarch64) with the repo folder mounted. From it: `/Users/natig/.dotnet/dotnet` ABSENT, `/private/tmp` ABSENT, no `dotnet`, no `mongod` (`raw/01-preflight-probe.txt`). The mandated native .NET 8 (SDK 8.0.417 / runtime 8.0.23) Release build, the DB-010 isolated Mongo replica set and the /private/tmp workspace therefore cannot be produced by this verifier. A Linux or non-8.0.417 substitute would not be native acceptance (ENVIRONMENT.md). Owner chose to stop (`AUTHORITY.md`).

## Preflight

| # | Step | Result | Evidence |
|---|---|---|---|
| P1 | Input hashes: A12 patch `b3c7dcbb…ecefb`, archive `7b6a0d1a…314d`, manifest `8ffa6c96…0d36`, Auth overlay `f50350b8…e2cd`, FINAL-22 manifest `b9713185…1731` | **PASS** (5/5 exact) | `raw/01-preflight-probe.txt` |
| P2 | /private/tmp dir; git archive 4a8d4d4 → A12 360 overlay → Auth 22 overlay; zero overlap; 360/360, 22/22 | **NOT RUN** | blocked: /private/tmp not reachable |
| P3 | Disposable gateway route config from HEAD ocelot.json (lane ports), diff + hash | **NOT RUN** | depends on P2/ports |
| P4 | BUILD-INPUT-MANIFEST.tsv; missing-dependency check | **NOT RUN** | `BUILD-INPUT-MANIFEST.tsv` (header + status only) |
| P5 | Native dotnet SDK 8.0.417 / runtime 8.0.23 | **NOT RUN** — runtime unreachable | `raw/01-preflight-probe.txt` |

## Acceptance criteria

| Criterion | Result | Note |
|---|---|---|
| Early vertical slice: one authorized normal detail load end-to-end | NOT RUN | no build/process |
| A12 normal detail renders summary / lines / POD / actions | NOT RUN | |
| A12 cross-LE detail → only localized safe-not-found + support reference | NOT RUN | |
| A12 unknown detail → same | NOT RUN | |
| A12 soft-deleted detail → same | NOT RUN | |
| A12 hidden surfaces hidden/inert, not keyboard-reachable | NOT RUN | |
| A12 late async responses do not re-expose stale surfaces | NOT RUN | |
| A12 backend 404 `SHIPMENT_NOT_FOUND`, correlation unchanged | NOT RUN | |
| A12 DB before/after zero writes | NOT RUN | |
| Negative controls | NOT RUN | |
| Regression A08 stale-transition browser flow (EVIDENCE-REUSE row 2: rerun) | NOT RUN | inherited PASS is **not** counted — impacted by A12 `load()` change |
| Regression A09 stale-POD, 409 variant | NOT RUN | same |
| Regression A09 stale-POD, 422 eligibility variant | NOT RUN | same |
| Accessibility (PRES-183-01) | INHERITED — no impact (EVIDENCE-REUSE row 4); not re-verified, not counted as PASS here | |
| A03 / PRES-183-02 | INHERITED — no impact (EVIDENCE-REUSE row 5); not re-verified, not counted as PASS here | |
| PNG via supported save/export | NOT RUN — not assessed; no browser session was opened. Remains OPEN | |

No static or inherited evidence is counted as PASS. No tokens or credentials were created or stored.

## What CT needs to unblock

A native macOS executor for this run (host shell with `/Users/natig/.dotnet/dotnet` 8.0.417/8.0.23, `mongod`, `/private/tmp`), or an owner-authorised alternative execution path. Preflight P1 can be reused as-is if the input hashes are unchanged.
