# MVP6 development plan v9.3 — DRAFT (NOT APPROVED)

**Status: DRAFT — not approved.** Successor text proposed for [v9.2](../mvp6-development-plan-v9.2.md). It grants no execution, publication,
promotion, commit, push or rollout authority. v9.2 stays in force until the owner approves this text or an alternative.

Basis: owner decisions 2026-09-26 recorded in `docs/records/audits/2026-09/mvp6-ct-owner-decisions-modules-first-2026-09-26.md`
(1: finish modules first, integrate at the end; 2: follow the plan, run in parallel whatever can safely run in parallel; pilot limit unchanged).
Evidence: this folder (MODULE-READINESS.tsv, SOURCE-LOCATIONS.tsv, BLOCKING-DECISIONS.tsv, WAVES.md).

## 1. What changes from v9.2

| Area | v9.2 | v9.3 (draft) |
|---|---|---|
| Stage order | R → 2 Integration (early, one checkout) → 4 UI wave per module → 6 Release | **R → M (modules in isolation, parallel waves) → I (one final integration) → Release** |
| Where a module is built and verified | Integrated checkout (after Q15) | Its own isolated environment: HEAD `4a8d4d4b` archive + that module's accepted overlays |
| Shared files (Program.cs, gateway, nav, L10n, Auth) | Integration owner, in sequence, during Stage 2/4 | Each module delivers a **per-module shared overlay package**; the integration owner applies and reconciles them **once** in Stage I |
| Module closure (Phase 4.5 in integrated target, Phase 6) | After each module's UI in the integrated target | Phase 4.5 in the integrated target and final closure wait for Stage I; isolated runtime VER is required before a module counts as "finished in isolation" |
| Self-registration D4 | Provider ships with the module UI in the integrated target | **Conflicts** — needs owner decision SR-D4 (proposed: build provider/tests/nav values as the module's overlay; ship at Stage I) |
| Q14/Q15 | Next after A12 | Moved to Stage I |
| Lanes | ≤2 product + 1 environment | unchanged; one writer per shared seam and per ledger |

Sections 2 (development line) and 3 (lanes) of v9.2 stay, with two edits: step 6 "Self-registration" reads "per-module overlay; ships at Stage I (SR-D4)"; step 8 "Runtime smoke (Phase 4.5)" is split into 8a isolated runtime VER (Stage M) and 8b Phase 4.5 in the integrated target (Stage I).

## 2. Definition — "module finished in isolation"

All of: pack `ready-for-dev` (Phase 0); approved backend and UI Phase 1.5 tables; UI built in owned paths; early vertical slice passed; independent runtime VER on the frozen source (Mac) with durable PNG; per-module shared overlay package delivered and hash-bound (Program.cs lines, gateway routes, nav, resx values for 7 languages, permission grants, self-registration provider + tests); backlog entries for deferred items; CT disposition. Not included: Phase 4.5 in the integrated target, golden flow, E5/G5, Phase 6 closure, rollout.

## 3. Stage order

| Stage | Work | Queue | State now | Exit |
|---|---|---|---|---|
| R Runtime | A12 VER (done), Loads 3.1.0 publication (done), evidence kit v1.1 → validation | Q04 ✓, Q25/Q32 ✓, Q57 → Q58 → Q24 | Q57 in progress | Kit validated |
| M1 | Decision prep; Shipment final isolated VER; Carrier UI source archive + Carrier final isolated VER; isolated-env recipes | new | READY | See WAVES.md Wave 1 |
| M2 | Returns and Claims UI builds + independent runtime VERs | Q18 (re-scoped) | needs SR-D4, PH15-UI-186/187 | Both modules finished in isolation |
| M3 | Loads uptake/root/UI; S&OP and Capacity UI scope + builds; self-registration overlays for Shipment/Carrier; Supplier packs | Q09, Q27, Q28, Q19 | DECISION-REQUIRED | Each module finished in isolation |
| I Integration | Q14 refresh → Q15 decision → one checkout; single writer applies all overlays + DCP-009 §21; reconcile tests; Phase 4.5 per module; golden flow; integration VER | Q14, Q15 | HELD to this stage | Integrated target green |
| Release | Phase 6 closure docs (Q42), E5/G5, release checklist, single push | Q42 + later | — | `/release-checklist` |

Remaining effort (latest ledger `mvp6-effort-shipment-ct-update-07`, M): 1,390 h of 2,842 h; not yet in the ledger: Returns/Claims UI net +42 h M (Q33), self-registration 70 h M (38/70/127, of which the 3/5/8 pack-section row is already done), and unestimated items (Loads root read, detail, lookup; per-module overlay packaging and one-time reconciliation added by this plan). Q51 reconciles them.

## 4. Risks introduced by "integrate last"

| Risk | Mitigation in this plan |
|---|---|
| Late discovery of DI, route or shared-file collisions (the v9.2 audit's reason for early integration) | Every module's overlay is hash-bound and dry-run against HEAD + BC-SOURCE in its isolated env; Stage I starts with a golden-flow vertical slice |
| Self-registration and nav guard only testable together | Provider completeness tests run per module in isolation; reconcile-state R-01 in a Platform fixture per module; R-02…R-04 at Stage I |
| Accepted source only outside the repo (Carrier v3 UI) | E1 archives it first |
| Uncommitted work grows | Commit per working-mode record from Mac sessions (Q03 to confirm) |

## 5. Owner decision queue (one at a time, question tool, recommended option marked)

1. **SR-D4** — self-registration under "modules first" (unblocks provider work in every wave)
2. **PH15-UI-186** — Returns UI Phase 1.5 table
3. **PH15-UI-187** — Claims UI Phase 1.5 table
4. **Q27 / Q28** — S&OP and Capacity pack promotion (prepared, option A recommended)
5. **Q11 → Q10 → Q12** — Shipment DN-02, DN-01, PC rows
6. **Q09** — Loads C producer uptake, then **LOADS-ROOT**, **LOADS-UI-SCOPE**, **RS-06/07**
7. **Q19** — Supplier DC-01..05
8. **PH15-UI-183/184** — retroactive UI tables for Shipment and Carrier (or a recorded waiver)
9. **Q03** — commit strategy confirmation; **Q21** — measurement policy
10. **Q15** — integration execution (Stage I)

## 6. Measures

Unchanged from v9.2 §6 (process v1.0 §9). Effort changes only at writer complete, independent VER and CT decision.
