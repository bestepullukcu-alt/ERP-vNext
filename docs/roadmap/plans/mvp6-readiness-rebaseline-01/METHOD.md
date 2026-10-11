# MVP6 readiness rebaseline — measurement method

**Work package:** `MVP6-READINESS-REBASELINE-01`  
**Cut-off:** 2026-09-24, Europe/Istanbul  
**Repository identity:** `feature/mvp6-logistics` at `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
**Baseline status inventory:** 276 dirty/untracked entries; `git status --porcelain=v1` SHA-256 `52c278d5ddd812b2a7a8ff376b258ce2ae580f467de8e9774f771b07615b2793` before report creation.

## Purpose

This is the first fixed, reproducible baseline for the nine-module MVP6 delivery. It does not reinterpret the historical “≈48%” estimate as an earlier observation made with this method. Future deltas must recalculate the same rows in `DELIVERABLES.tsv`; they must not compare unlike denominators.

The denominator is the final MVP6 delivery described by `WP-MVP6-logistics.md`: the Shipment → Carrier/Load → POD → Return/Claim chain, S&OP/Capacity, and Supplier feedback. A bounded backend work package can complete backend rows, but it cannot remove frontend, shared integration, G5, or rollout rows from the denominator.

## Development completion rubric

Every module has the same seven deliverables. Each is worth one point; the module denominator is always 7. A bounded pack saying `shell: none` describes that bounded slice. It does not make the final MVP6 UI deliverable N/A unless a final-scope owner decision explicitly does so.

| ID | Deliverable | What counts |
|---|---|---|
| D1 | Pack and owner design | Exact scope and business decisions are approved and bound to an immutable pack/artifact. |
| D2 | Phase 1.5 technical mapping | Owned paths, entity/wire/index/transaction/error/acceptance mappings and dispatch boundaries are closed. |
| D3 | Contract and consumer pin | Required canonical contract/annex is published and the module is pinned to the exact bytes. |
| D4 | Backend core | Module-owned persistence/application/domain implementation exists on the approved source identity. |
| D5 | Composed HTTP/process | Real process, JWT/RBAC, persistence/restart/failure paths exist for the approved bounded scope. |
| D6 | Frontend/final UI disposition | The final MVP6 UI surface is implemented, or an explicit final-scope owner decision says it is N/A. |
| D7 | Shared integration target | Source and shared composition are applied to the selected integration target; common-target acceptance may still be pending. |

Development states are scored as follows:

| State | Score | Meaning |
|---|---:|---|
| `DONE` | 1.00 | Exact deliverable is implemented and accepted/verified at its stated boundary. |
| `IMPLEMENTED_AWAITING_ACCEPTANCE` | 0.75 | Writer-complete or applied in an isolated integration target, but independent VER/CT/common-target acceptance is open. |
| `READY` | 0.50 | Exact authorized candidate/dispatch exists; product implementation is not complete. |
| `SPEC` | 0.25 | Concrete, reviewable design/preparation exists but required decisions or implementation authority remain open. |
| `OPEN`, `BLOCKED`, `NOT_STARTED`, `UNKNOWN` | 0.00 | No completed deliverable credit. `UNKNOWN` stays in the denominator. |
| `N/A` | 1.00 | Allowed only with an explicit final MVP6 owner decision. No module uses this state in this baseline. |

Formula: `Development completion = sum(D1..D7 scores) / 7 × 100`.

## Release readiness rubric

Release readiness is deliberately stricter. Every module has five binary gates, each worth 20%. Partial implementation, candidate PASS, or writer evidence earns no fractional release credit.

| ID | Release gate | PASS rule |
|---|---|---|
| R1 | Independent bounded runtime VER | The latest approved bounded runtime scope has source-bound independent verification. Mixed writer/archive evidence without a complete independent verdict remains open. |
| R2 | CT bounded acceptance | CT explicitly accepted the bounded module/work package. This is not full-module acceptance. |
| R3 | Integrated/common-target acceptance | Selected source and shared composition were independently verified and accepted in the integration/common target. |
| R4 | MVP6 G5/golden-flow acceptance | Cross-module Shipment → Load → POD → Return/Claim and Supplier feedback requirements are accepted. |
| R5 | Release/rollout acceptance | Consumer uptake, data/migration/rollback/operations and production release authority are complete where applicable. |

Formula: `Release readiness = passed gates / 5 × 100`.

## Evidence rules

1. `DONE` is tied to a source/pack/contract/report hash, not a narrative date.
2. Historical tests are cited as historical evidence and were not rerun here.
3. Isolated worktree/archive acceptance is reported separately from the mutable shared checkout.
4. The selected 422-source integration target has writer evidence, but no independent integrated VER/CT acceptance. It therefore gives D7 development credit and no R3 release credit.
5. Current shared pack files for MOD-0186, MOD-0187, MOD-0190 and MOD-0192 remain `draft`; their promoted pack identities exist only in isolated acceptance records. This does not erase accepted implementation, and it blocks treating the shared checkout as the accepted delivery.
6. Unknown UI scope remains `UNKNOWN` with score 0. A backend-only first slice does not silently remove UI from the final MVP6 denominator.
7. Contract publication is not runtime uptake. Fixture/model PASS is not HTTP/Mongo/JWT acceptance. CT bounded acceptance is not G5, E5, rollout, or full-module completion.

## Recalculation procedure

1. Freeze a new cut-off, branch/HEAD and dirty-inventory hash.
2. Rehash every evidence reference used by changed rows.
3. Change only rows whose exact artifact or controlling disposition changed.
4. Apply the state table without altering weights.
5. Recompute module and portfolio totals from `DELIVERABLES.tsv`.
6. Record scope changes as new rows or an explicit denominator revision; never hide them by deleting `UNKNOWN` rows.

## Limitations

- This is a management completion measure, not effort, duration, cost, or calendar forecasting.
- Scores express artifact/gate completion. They do not estimate defect probability.
- The repository is intentionally dirty and contains parallel lane outputs. No claim is made that HEAD alone reconstructs accepted isolated sources.
- MOD-0192 R1 remains open because the hosted close used a mixed evidence boundary; CT’s narrow close is credited separately in R2.
- Carrier Auth token rework is writer-complete on an isolated 22-path source, while the available independent VER predates that handoff. Carrier real-Auth browser E2E therefore remains open at this cut-off.
