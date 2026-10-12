# MVP6 development plan v9.0 — 2026-09-25

Supersedes the current-state and next-action statements of [v8.0](mvp6-development-plan-v8.0.md) and
[continuation 2026-09-23](mvp6-development-continuation-2026-09-23.md). Their bytes stay unchanged.
Basis: [CT audit 2026-09-25](../../records/audits/2026-09/mvp6-ct-audit-2026-09-25.md) and the
[Claude handoff](mvp6-claude-development-handoff-01/README.md).

This plan schedules work. It grants no execution, publication, promotion, commit, push or rollout authority.
Every stage that needs an owner decision names it; prepared decision texts are not approvals.

## 1. Position on 25 September

- Bounded backend work packages accepted for 7 of 9 modules (0183, 0184, 0185, 0186, 0187, 0190, 0192).
- UI: Carrier and Shipment partially accepted; all others have no UI.
- Integration: **0/9** modules run together in one checkout. Full module / E5 / G5: **0/9**.
- Effort (Update 07): 1,452 delivered / 1,390 remaining / 2,842 h estimated; CT-accepted 1,128 h (39.7%).
  Three Loads items are unestimated, so the denominator is incomplete.
- Contracts: SHIPMENT-BUNDLE 3.0.0 and SANDOP-CAPACITY 3.0.0 published and frozen; Loads 3.1.0 awaiting owner decisions.
- Completed since v8.0 / 09-23: Capacity v3 publication, BC Capacity uptake (accepted 23 Sep), Carrier real-Auth UI E2E
  (PARTIAL, PNG open), Shipment shared UI A03/PRES-183-02, accessibility and A08/A09 closures, A12 static rework.

## 2. Stage order

| Stage | Work | Depends on | Owner decision needed | Exit |
|---|---|---|---|---|
| **0 — Protect** | Non-invasive backup of the whole working tree (bundle + tracked patch + untracked tarball in `.git-backups/`, git-backup-policy §A); then commit strategy for accepted work, K7 large-file and `TestResults/` hygiene | — | **Yes: backup now; commit strategy** | Backup files hashed and listed; nothing in the tree changed |
| **1A — A12 runtime VER** | Lane-1: HEAD archive + A12 360 overlay + Auth 22 overlay in a disposable checkout; real-Auth browser VER; PNG capability check | — | No (confirmed 25 Sep) | SOP §22 with PASS/FAIL/NOT RUN per criterion → CT disposition |
| **1B — Acceptance matrix** | Lane-2: one A01–A16 + PRES matrix for Shipment UI; missing-decision delta only | — | No | Matrix record; only genuinely missing decisions listed |
| **1C — Owner decisions** | Loads A (version + consumer inventory), then B (publication), then C (uptake); DN-01, DN-02, PC-02/03/04/28 | — | **Yes, one at a time** | Decision records with exact hashes |
| **2 — Integration baseline** | Refresh the 422-path selection to the latest accepted successors (Shipment UI + A12 if accepted, Auth 22, Capacity BC `dec28b6…`, S&OP 379, Returns, Claims, Carrier, Loads); one integration writer builds one registered checkout; run architecture tests (incl. DocsPathGuard) and the minimum golden-flow regression | Stage 0; 1A result for A12 | **Yes: exact target-bound integration decision** (refresh of `mvp6-integration-baseline-exec-01/AUTHORITY-REQUEST.md`) | Integrated source manifest; build/test/runtime evidence; independent VER; CT disposition |
| **3A — Loads completion** | Publication (B) → producer uptake (C) → transition UI (16/28/48 h) → detail/lookup only if owner adds them | 1C decisions; Stage 2 for UI | Yes (1C); UI scope pack | Loads VER + CT |
| **3B — Pack alignment** | Promote Returns via `mvp6-final-pack-delta-01`; equivalent deltas for Claims, S&OP, Capacity; commit pack statuses with Stage 0 | Stage 0 | **Yes, per pack** | Pack status equals accepted authority |
| **4 — UI wave** | Module-pack author defines UI scope (shell, gateway route, permissions, form count / golden reference, 7 languages, browser acceptance) for Returns, Claims, S&OP, Capacity; then one UI writer per module | Stage 2; 3B | Yes: each UI pack | UI VER + CT per module |
| **5 — Supplier** | 0147 / 0148 core and UI after DC-01..05 | DC decisions | **Yes: DC-01..05** | Packs ready-for-dev; DEV → VER → CT |
| **6 — MVP6 release** | Golden flow warehouse shipment → carrier/load → POD → returns/claims on the integrated baseline; E5/G5; PNG evidence; rollout plan | Stages 2–5 | Yes: release | G5 record |

Critical path: **Stage 0 → Stage 2 → Stage 4 → Stage 6.** Stages 1A, 1B and 1C run now, in parallel, with no shared writes.

## 3. Lanes now

| Lane | Work | Writes | Status |
|---|---|---|---|
| Agent Lane-1 | Stage 1A | `docs/records/audits/2026-09/mvp6-shipment-a12-runtime-independent-ver-01/` + disposable `/private/tmp` checkout | Ready to start (prompt issued 25 Sep) |
| Agent Lane-2 | Stage 1B | `docs/records/audits/2026-09/mvp6-shipment-acceptance-reconcile-01/` | Ready to start |
| Agent Lane-3 | Reserved: Stage 2 selection refresh after Stage 0 | none yet | Waiting |
| Agent Lane-4 | Stage 1C Loads decisions | decision records only after genuine decisions | Start when owner is ready |
| Control Tower | Dispositions, plan, decisions queue | CT records and plans | Active |

At most three writing lanes at once. One writer each for shared contracts, `Program.cs`, permissions and gateway.

## 4. Remaining estimate (Update 07, most-likely hours)

| Module | Remaining | Main content |
|---|---:|---|
| 0183 Shipment/POD | 46 | UI acceptance rows, PNG, pack/Phase 1.5 closure |
| 0184 Carrier | 28 | PNG, Test/VER |
| 0185 Loads | 172 + unestimated | transition UI, live integration, root read (unestimated), detail/lookup (open scope) |
| 0186 Returns | 106 | UI, integration |
| 0187 Claims | 114 | UI, integration |
| 0190 S&OP | 136 | UI, live DEMAND/Workflow seams |
| 0192 Capacity | 144 | UI, live planning seams |
| 0147 / 0148 Supplier | 248 / 232 | core, UI, integration |
| Shared | 164 | integration, release, environment |
| **Total** | **1,390 + unestimated** | |

Hours are estimated specialist-hours, not a schedule. No calendar dates are claimed.

## 5. Owner decision queue (asked one at a time)

1. Stage 0 backup now (recommended) — then commit strategy.
2. Loads A: final 3.1.0 / wire v1 and external consumer inventory.
3. Integration target decision (after the Stage 2 selection refresh).
4. Loads B, then C.
5. DN-02 DataTable profile; DN-01 A10 proxy; PC-02/03/04/28.
6. Pack promotions (Returns first).
7. Supplier DC-01..05.

## 6. Process rules added by this plan

- **Evidence checklist before VER hand-off** (3/8 packages were returned only for evidence): exact source/archive hashes;
  source → binary → process → browser binding; DB before/after; redacted raw evidence; cleanup record; no token persistence.
- A verifier facing an "incomplete archive" first applies the HEAD-archive + overlay method before reporting NOT RUNNABLE.
- Every CT disposition states whether it creates effort credit; none without an O/M/P split.
- Rev2 measurement: no individual dashboard until a human time ledger exists.

## 7. Not in this plan's authority

Commit, push, stash, prune, publication, pack promotion, gateway/permission/`Program.cs` edits, A10 proxy,
waivers, rollout — each needs its own exact owner decision.
