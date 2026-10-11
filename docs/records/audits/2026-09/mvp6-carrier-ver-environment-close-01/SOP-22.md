# MVP6-CARRIER-VER-ENVIRONMENT-CLOSE-01 — SOP §22

Date: 2026-09-23  
Role: orchestrator; environment/evidence-only  
Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Environment verdict: **READY**  
Real-Auth Carrier E2E gate: **OPEN — Auth independent REWORK; successor pending**

## Native .NET 8 disposition

A native arm64 .NET 8 installation already exists; no installation or system change is required.

- Executable: `/Users/natig/.dotnet/dotnet`.
- Executable SHA-256: `6d06c4a53676021a7572c989528067a3fe1195f569c27fbcac25e5302dc0d72c`.
- SDK: `8.0.417`.
- Host, .NET runtime and ASP.NET Core runtime: `8.0.23`.
- Explicit check with both roll-forward variables removed returned only the user-root .NET/ASP.NET Core 8.0.23
  runtimes.

The system `/usr/local/share/dotnet/dotnet` is SDK/host 10 and has no ASP.NET Core 8 runtime. A launch through that
host with `DOTNET_ROLL_FORWARD=Major` is ASP.NET Core 10 execution and must not be labelled native .NET 8.

This disposition matches earlier accepted native evidence in
`mvp6-mod0190-ct-acceptance-review-01` and `mvp6-bc-successor-independent-ver-01`. The Carrier Auth independent
VER's .NET 10 major-roll-forward execution remains historical and is not retroactively upgraded.

The required successor prefix, binary/runtime hashes, and inventory are in `NATIVE-NET8-EVIDENCE.txt` and
`RUNTIME-INVENTORY.tsv`.

## Final UI source binding

The final Carrier UI source is the 21-path v3 manifest
`3b7086f0cc839c33f0753076e732aa436b9a8e5cd186f1484f2b0225364d033c`, bound to Carrier patch
`57f90dadedd6e2304e3775fd1ea0bd7def29ad9d265ccbbeb892afdc04ba1adb`. The registered UI worktree was rehashed
fresh: **21/21 exact**.

The two successor paths are:

1. `index.l10n.js` — `8a62578a0f5652a12eed8cd5d4bd8824b8a079f50cc018848f001768aa76ab8d`.
2. `CarrierIndexBehaviorTests.cs` — `5393232f80bf7a24e468e7d49fbc2efcc7b564b1fa5c9daa2fcecc655760e79b`.

`e96578d0…` is retained only as the accepted preimage identity. It is not the final source. `FINAL-UI-SOURCE.tsv`
and `FINAL-UI-DELTA.tsv` are controlling for successor preflight. The final-v3 L10n and responsive PASS results are
hash-bound and need no repetition absent relevant source/layout drift.

## Isolated launch readiness

The proposed successor ports 5400/5401/5456/5457/5459/5461 and Mongo 37484 were free at inspection time. That
check is evidence of availability, not a reservation; the successor must fail closed on a fresh listener preflight.

`PORT-DB-PLAN.tsv` assigns separate Web, Gateway, Auth, Platform, MDM, SupplyChain and replica-set surfaces.
`LAUNCH-CLEANUP-PLAN.md` requires explicit native executable selection, fixed lane-local databases, operational
Mongo 27017 avoidance, PID/binary verification, bounded TERM/KILL cleanup, final listener checks, and post-run
21/21 source rehashing. No process was started in this lane.

## Browser artifact capability

The supported browser API can produce screenshot bytes and display images inline, but exposes no explicit durable
PNG save/export operation. Page-content export and page-asset bundling are not screenshot export. Durable PNG
therefore remains OPEN. The prior data-URL rejection was not retried or bypassed with CDP, encoding, native capture,
or another mechanism. Details are in `BROWSER-ARTIFACT-CAPABILITY.md`.

## Updated dependency handoff

The previous phrase “Auth implementation is absent” is obsolete. The current exact disposition is:

> **Auth/Platform independent runtime VER: REWORK — successor pending.** GAP-CARRIER-AUTH-LE-01 was independently
> reproduced: the correct internal key reaches the resolver and returns HTTP 400 because TenantId is unresolved.
> GAP-CARRIER-AUTH-LE-02 is source-confirmed: there is no authorized server-to-server MDM validation channel.
> Real-Auth Carrier E2E waits for an approved/applied Auth rework successor and a separate independent runtime PASS.
> No Auth-dependent E2E was executed in this environment lane.

The exact update to the historical successor matrix is recorded without rewriting it in
`SUCCESSOR-ACCEPTANCE-UPDATE.tsv`. L10n and 768/390 responsive results remain PASS on v3; Auth-issued
list/create/replay/status, cross-scope isolation, mutation persistence, status offcanvas, and durable PNG remain OPEN.

## Change boundary

This task created only `docs/records/audits/2026-09/mvp6-carrier-ver-environment-close-01/`. It did not change
Auth, Platform, MDM, UI, Gateway, SupplyChain, runtime configuration, permission, contract, pack, or Git state. No
runtime installation, browser E2E, diagnostic-token acceptance, commit, push, or stash occurred.
