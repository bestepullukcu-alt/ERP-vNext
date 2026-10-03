# MVP6-MOD0185-CT-REVIEW-01

Date: 2026-09-19  
Role: Control Tower  
Verdict: **REWORK**

## Scope decision

The bounded MOD-0185 Loads package is reviewable and most rework evidence is independently reproduced. Central acceptance is **REWORK**, because the independent A04 failure probe does not actually measure persisted-state counts after connection refusal and timeout. It records the text `no writes asserted...` without querying Mongo collections.

The exact accepted implementation scope remains the approved Loads slice only:

- `GET /api/shipment-bundle/loads`
- `POST /api/shipment-bundle/loads`
- `POST /api/shipment-bundle/loads/{loadId}/transition`
- five-layer service, CQRS, tenant/legal-entity isolation, RBAC, idempotency, lifecycle, audit/receipt/outbox consistency
- GET-only Carrier/Shipment reference boundary
- Pending-only outbox; no publisher/worker or operational migration

No full-module done, E5/G5, live ingress, gateway uptake or downstream GO is granted.

## Branch and baseline

- Branch: `feature/mvp6-logistics`
- HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Worktree was already dirty at CT preflight and was preserved.
- CT changed only this README. No runtime, contract, pack or historical audit file was modified.

Parallel MOD-0186/MOD-0187 preparation changes remain pre-existing dirty paths and are excluded from this review’s ownership.

## Hash chain

DEV-02 handoff SHA-256: `a32ff496ce8986138e09ae5507efed652f5ccb32f97c870074cc6a05c6d63ffe`  
DEV-02 manifest SHA-256: `f9eb90fa8fdf341f92540cbb12acb0f281be504be98ce612f93530d9efd4ddf0`  
DEV-03 handoff SHA-256: `ff9cf30cf2791b69bac249762d81016f48ad87547bb1762a3eb9acc5e44d99a8`  
DEV-03 manifest SHA-256: `f3343d7f963841988e55a4a20a51524fc8dcc2c87a2d6f046f1cf5968ba8316a`

The DEV-03 manifest contains 47 Loads source/test entries. The independent VER-02 disposable copy reported zero manifest mismatches. Runtime fingerprints bind the evidence to source-set digest `246f20f85b28a0c2abf5af40992a20056fce57ec0041dfd5cc58eb370eab33c3` and API binary SHA-256 `e10b6de0a7a1917b98703eba8c40c19309e3a26e82d1cfe430c8685f4a888737`.

Frozen inputs remained unchanged:

| Input | SHA-256 |
|---|---|
| SHIPMENT-BUNDLE | `93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571` |
| Loads annex | `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1` |
| Carrier annex | `87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee` |

## Independent evidence reviewed

The independent run used `/private/tmp/mod0185-ver02-independent/` and `/private/tmp/mod0185-ver02-runtime/`.

- Disposable .NET 8 build: PASS, 0 warnings/errors.
- Full SupplyChain TRX: `.../TestResults/ver02-full-net8.trx`, `total=132`, `passed=132`, `failed=0`.
- Loads TRX: `.../TestResults/mod0185-dev03-loads.trx`, `total=33`, `passed=33`, `failed=0`.
- Older TRX files in the disposable directory were not used: `ver02-full.trx` had zero executed tests; DEV-02/DEV-03 historical TRXs were treated as historical only.
- HTTP runtime: `/private/tmp/mod0185-ver02-runtime/runtime.json`, create/list/replay PASS; actual create/replay request bytes 294 and bodyless GET 0.
- Restart: `/private/tmp/mod0185-ver02-runtime/restart.json`, two process IDs, durable replay/list and one Pending outbox record.
- A12 negative controls: old DEV-01 record rejected; request-byte mutant rejected; correct runtime record passed 18 verifier checks.
- A07 logs: standalone and unavailable startup fail closed; createIndexes failpoint produced `MongoNotPrimaryException` in `ShipmentSchema.StartAsync` and no healthy service. `enableTestCommands` and `configureFailPoint` were independently verified.

## A01–A12 acceptance matrix

| AC | CT disposition | Evidence / limitation |
|---|---|---|
| A01 | PASS | Three bounded routes; runtime create/list/replay and service tests. |
| A02 | PASS | Full 132-test TRX and contract tests. |
| A03 | PASS | Isolation/RBAC/security coverage in full suite. |
| A04 | **OPEN — REWORK** | Refusal/timeout return 503 `DEPENDENCY_UNAVAILABLE` with correlation, but `failure_probe.py` only writes a textual persistence assertion; it does not query `loads`, receipts, audit or outbox counts. Evidence path: `/private/tmp/mod0185-ver02-runtime/failure-paths.json`; implementation path in disposable copy: `services/Diten.SupplyChainService/tests/loads/failure_probe.py:30-32`. |
| A05 | PASS | Lifecycle coverage included in 132/132. |
| A06 | PASS | Replay, restart and no dependency reread evidence. |
| A07 | PASS | Real failpoint-enabled 33/33; standalone, unreachable and index-failure startup logs show expected failure causes. |
| A08 | PASS | Bounded Load business rules covered by service tests. |
| A09 | PASS | Restart evidence proves durable state and Pending outbox. |
| A10 | PASS within bounded mock scope | GET-only reference boundary; live source DB remains excluded. |
| A11 | PASS for service suite | 132/132 fresh in disposable copy. Architecture 50/3 remains historical and is not promoted. |
| A12 | PASS | Independent transport capture, byte/body mutants and verifier negative controls. |

## Required rework

Add a bounded, isolated persistence assertion to the A04 refusal/timeout probe. After each failed create, query the same test database and record exact counts (zero new load, assignment, receipt, audit and outbox records), with tenant/legal-entity scope and correlation. Re-run the independent VER-02 checks; do not reuse a prose claim as persistence evidence.

## No-change and CT boundaries

No tests were rerun in the real repository during CT review; the cited executions are from the disposable copy. CT did not modify runtime, contracts, module packs, gateway/shared files or historical records. No commit, push or stash occurred. This README is the sole CT-owned output.

**Central decision: REWORK — A04 persistence evidence is insufficient.**
