# MVP6 development plan v9.1 — 2026-09-25

Successor to [v9.0](mvp6-development-plan-v9.0.md) (same day). Adds the development process
[v1.0 pilot](../../guides/operations/mvp6-development-process-v1.0.md) from *MVP6 Geliştirme Önerileri Rev2*.
Stage content and remaining estimates of v9.0 are unchanged unless stated here. Basis:
[CT audit 2026-09-25](../../records/audits/2026-09/mvp6-ct-audit-2026-09-25.md).

This plan grants no execution, publication, promotion, commit, push or rollout authority.

## 1. What changed from v9.0

| Area | v9.0 | v9.1 |
|---|---|---|
| Work tracking | Stage table | Every item in [`CT-QUEUE.tsv`](mvp6-process-pilot-01/CT-QUEUE.tsv) with one state; DECISION-REQUIRED / HELD items are never re-dispatched without a changed trigger |
| Acceptance | per-lane criteria | One acceptance matrix per module before DEV (HTTP / browser / DB result per row); Shipment first (Lane-2) |
| Build order inside a module | implement → VER | preflight → **early real-Auth vertical slice** → build → writer self-check → freeze → independent VER → one CT disposition |
| Evidence | per lane | One evidence checklist (process §5) and one reusable evidence kit (Lane-3 proposal) |
| Verification | reuse by judgement | Inherit only with no impact; every decision in `EVIDENCE-REUSE.tsv`. First result: A08/A09 need a targeted regression in the A12 run |
| Lanes | Lane-3 reserved | Lane-3 = shared-environment lane (evidence kit, PNG capability) |
| Parallelism | ≤ 3 writing lanes | Pilot: ≤ 2 product lanes + 1 environment lane; shared Auth/Gateway/navigation/L10n/`Program.cs` via one integration owner |
| Reporting | — | Effort updated only at writer complete, VER and CT decision; milestone timestamps from 25 Sep, no backfill |
| Authority | exact per package | Unchanged. Optional routine-fix clause for **new** packages — owner decision Q20 |

## 2. Stage order (unchanged, with pilot placement)

| Stage | Work | Queue | State |
|---|---|---|---|
| 0 Protect | Backup | Q01 | DONE (22:09) |
| 0 Protect | Offsite copy; commit strategy and hygiene | Q02, Q03 | Owner |
| 1A | A12 runtime VER + A08/A09 regression + PNG capability — **pilot item** | Q04 | READY (Lane-1) |
| 1B | Shipment acceptance matrix — **pilot item** | Q05 | READY (Lane-2) |
| 1E | Evidence kit proposal — **environment lane** | Q06 | READY (Lane-3) |
| 1C | Owner decisions: Loads A→B→C, DN-01, DN-02, PC rows | Q07–Q12 | DECISION-REQUIRED / HELD |
| 2 | Integration selection refresh → one integration checkout; first action = golden-flow vertical slice | Q14, Q15 | HELD → DECISION-REQUIRED |
| 3A | Loads completion | Q08, Q09 | HELD |
| 3B | Pack alignment (Returns first) | Q16, Q17 | DECISION-REQUIRED / HELD |
| 4 | UI wave, using the closed Shipment/Carrier UI as reference; each module starts with one real-Auth UI operation | Q18 | HELD |
| 5 | Supplier | Q19 | DECISION-REQUIRED |
| 6 | Release: golden flow, E5/G5, PNG, rollout | — | Later |

Critical path unchanged: **Stage 0 → Stage 2 → Stage 4 → Stage 6.** Pilot order for Shipment/Carrier closure:
Lane-1 and Lane-2 now → CT disposition → remaining Shipment UI rows on the final source → Carrier PNG + CT closure.

## 3. Pilot success measures (reviewed by CT after the pilot)

| Measure | Rule |
|---|---|
| Acceptance cycle time | Milestone start → independent acceptance; active work, environment wait and owner wait shown separately |
| First-VER pass rate | Accepted at first independent VER / entered first VER; like-for-like work only (baseline: 0/8 CT first review) |
| Rework share | Defect-fix active effort / recorded active effort; needs `TIME-INTERVALS.tsv` (Q21) |
| Open items | Count in progress, blocker age and owner (`BLOCKERS.tsv`) |
| Evidence reuse | Inherit/rerun decisions with reasons (`EVIDENCE-REUSE.tsv`) |
| Effort progress | Implementation index and accepted effort side by side; unestimated scope listed |

## 4. Owner decision queue (one at a time)

1. Q03 commit strategy — 2. Q07 Loads A — 3. Q20 routine-fix clause — 4. Q21 Rev2 measurement policy —
5. Q11/Q10/Q12 Shipment policy decisions (after the Lane-2 matrix) — 6. Q15 integration (after refresh) — 7. Q16 Returns pack — 8. Q19 Supplier.
