# MVP6-MOD0186-R01-REWORK-01 — SOP §22 DEV handoff

**Agent verdict:** PASS for the bounded R01 rework; writer complete. Independent Returns VER is still required.

**Branch / HEAD:** `codex/mod-0186-r01-rework-01` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`

**Worktree:** `/private/tmp/mvp6-mod0186-r01-rework-01` (registered Git worktree). The dirty state is the hash-bound integration baseline plus the two-file R01 delta; see `raw/rework-worktree-status.txt`.

## Changed files

| Path | Baseline SHA256 | Target SHA256 |
|---|---|---|
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Returns/ReturnReferenceReader.cs` | `b499556f6d96b74e63b1789836bbbb050f3410489b2ec0d5c84357741cc99d8d` | `eaa0aa73d1a2c6e296d57dbfc6ac278cef2081ec1a48cf6510f4b27c0dc2eea5` |
| `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnReferenceTests.cs` | `c66eca35ca909a0228195c831386ad869caaa4b4b20d00f0399effb69e1fa635` | `c36c462153aadaa3774af592be3a3d9905287a0b1c388a67c718fd594c33f7c7` |

Exact patch SHA256: `0bb36d02d3972f65d6644a5b12eaabc7a8820a1a50da13b17769ef7a5148e82c` (`product.patch`). It applies cleanly to the two exact baseline bytes and reproduces both target hashes.

## Contract flow and decision

The published Returns operation exposes `502 RETURN_SHIPMENT_ROOT_INVALID` for a malformed authoritative Shipment root. The producer detail operation exposes a malformed persisted root as the standard Error body `500 SHIPMENT_ROOT_INVALID`. The Returns reader now inspects only that exact `500` error identity with `contractVersion: v1` and maps it to the Returns-owned `502` code.

Unrelated `500`, malformed error JSON, wrong contract version, `501`, `401/403`, refusal and timeout remain `503 DEPENDENCY_UNAVAILABLE`. Missing/null root remains `503 RETURN_SHIPMENT_ROOT_UNAVAILABLE`. A malformed root inside a successful detail payload remains `502 RETURN_SHIPMENT_ROOT_INVALID`. No producer or contract change was made.

Authority bindings are in `manifests/input-bindings.sha256`: published YAML `5dfe7c1...`, Returns annex `00990a...`, root annex `7d1327...`, promoted pack authority `1c80cca...`, Phase 1.5 `c921c4...`, error matrix `2da816...`, and effective 46-path list `96f43b...`.

## Failure paths and tests

- True pre-fix RED: 1 failing test, expected 502 / actual 503.
- Targeted GREEN: 10/10.
- Full Returns-owned regression: 78/78.
- Fresh composed HTTP/DB: 52/52.
- Fresh restart process: 4/4.
- Fresh Release build: 0 warnings, 0 errors.

The fresh API binary is `81e7af9ee0a7e70b7f7a6f93209ea324e050b1e964025287749c73d22d672195`. Process evidence binds it to PID `87872` for the main suite and PID `88363` for restart on port `51862`.

## Persistence, security and audit evidence

The suite used isolated replica set `returns_r01_rework` on `127.0.0.1:27187`, database `returns_r01_rework_db`. JWT parsing, RBAC denial, tenant/LE isolation, soft-delete, five transaction write-stage rollbacks, concurrency caps, unknown commit, lost response and durable replay all passed. Final Returns state is 11 aggregates, 9 entitlements, 19 receipts, 19 audit rows and 19 Pending outbox rows; non-Pending is zero. No Inventory/Warehouse/stock collection exists and the API log contains no Inventory/Warehouse/stock outbound request.

The exact executed `http_probe.py`, Mongo/API helpers, redacted launch view, command exits, stdout/stderr, raw HTTP records and DB queries are retained. The corrected result text explicitly records `producer 500 SHIPMENT_ROOT_INVALID → Returns 502 RETURN_SHIPMENT_ROOT_INVALID`; it does not repeat the historical “null” explanation.

## Decisions and boundaries

- Kept the mapping in the Returns-owned reference/error layer.
- Rejected mapping every producer 5xx to root-invalid because it would hide availability/internal failures.
- No 46-path scope expansion was required.
- Intentionally did not edit Program.cs, Claims, shared helpers, producer, canonical, annex, guard, worker or stock behavior.
- No commit, push, stash or rollout action occurred.

## Blockers / known gaps

No DEV rework blocker remains. This is writer evidence, not independent VER, CT acceptance, deployment readiness or full-module completion. The next gate is an independent Returns VER against `product.patch` and the exact manifest.
