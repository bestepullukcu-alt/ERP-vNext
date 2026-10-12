# MVP6 development plan v9.2 — 2026-09-26

Successor to [v9.1](mvp6-development-plan-v9.1.md) (v9.0 and v9.1 stay as records). Basis: [CT audit 2026-09-26](../../records/audits/2026-09/mvp6-ct-audit-2026-09-26.md) (AU-01…AU-09).
Process: [development process v1.0](../../guides/operations/mvp6-development-process-v1.0.md) (§11 .antigravity gates), Control Tower SOP, `AGENTS.md`, `.antigravity/` (orchestrator, `/add-module`, self-registration standard, git-safety).

This plan grants no execution, publication, promotion, commit, push or rollout authority. Each such step needs its own exact owner decision.

## 1. What changed from v9.1

| Area | v9.1 | v9.2 |
|---|---|---|
| Critical path | Stage 0 → 2 → 4 → 6 | **Stage R (runtime in Terminal) → 2 → 4 → 6**; Stage R is the only step that turns preparation into accepted runtime evidence |
| Development line | process §3 | Process §3 mapped to the `/add-module` phases and named `.antigravity` agents (§2) — mandatory for every code package |
| Verification | independent VER for product work | Also for every pack/DCP text apply (owner decision Q49) |
| Self-registration | not planned | DCP-009 §21 + pack sections done (except 0186, Q48); code ships per module with its UI (D4) |
| Lanes | Lane-1/2/3 chat, Lane-4 decisions | Terminal runtime lane added as the single environment lane; Lane-3 = independent verifier (§3) |
| Backup | 25 Sep 22:10 | Refresh before any further apply (AU-03) |

## 2. Development line for every code work package (CT + .antigravity)

A package that is not in this line is not dispatched. CT writes one queue row per package with its phase.

| # | Step | Rule source | Owner / `.antigravity` agent | Exit condition |
|---|---|---|---|---|
| 0 | Classify the request | orchestrator rule 9 | CT (`@orchestrator`) | Class recorded: module package, capability (DCP), audit-only, governance-only, git, release |
| 1 | Pack gate (Phase 0) | orchestrator rule 2; add-module Phase 0 | CT | Pack `approved`/`ready-for-dev`; DCP-002 ID check PASS; for cross-cutting work the DCP is `approved` |
| 2 | Preflight | process §3.1 | writer lane | Scope, authority, source baseline (HEAD archive + overlays), acceptance matrix rows, environment |
| 3 | Architecture table (Phase 1.5) | add-module Phase 1.5 | CT → **owner via question tool** | 9-row table filled with `Plan:` answers and approved; any "No" returns to the pack |
| 4 | Early vertical slice | process §3.2; SOP §18.0 | writer (`@backend-architect` + `@frontend-ui-ux`) | One real-Auth operation Auth → Gateway → UI → backend → DB |
| 5 | Build (Phases 2, 3, 3.5, 4, 4a) | add-module | `@data-agent`, `@backend-architect`, `@l10n-agent` (7 tenant languages), `@integration-agent` (gateway), `@frontend-ui-ux` (Golden Reference, no "copy Sneat/module") | Owned paths only; shared Auth/Gateway/nav/L10n/`Program.cs` only through the integration owner |
| 6 | Self-registration | self-registration standard; D4 | integration owner | Module's manifest provider + nav keys ship with its UI; completeness tests both ways |
| 7 | Writer self-check + evidence checklist | process §5 | writer | Checklist complete; source frozen (manifest + archive hash) |
| 8 | Runtime smoke (Phase 4.5) | add-module Phase 4.5 | Terminal runtime lane | Channel A (MCP browser), B (Playwright) or C (owner) recorded; no "done" without it |
| 9 | Quality & security (Phase 5) | add-module Phase 5 | `@testing-agent`, `@security-agent`, `@code-quality-agent` | Tests incl. tenant safety; DataTable gate `verify_datatable_page.py` or recorded DN-02 scope |
| 10 | Independent VER | CT SOP; process §3.6 | Lane-3 (never the writer) | Report on the frozen source; fresh vs inherited evidence separated |
| 11 | CT disposition | CT SOP | CT | One new record; effort credit only with O/M/P split |
| 12 | Closure docs (Phase 6) | add-module Phase 6; rule 10 | `@documentation-writer`, `@user-manual-generator` | API narrative, illustrated manual, module audit report, backlog closure (Q42) |

Pack/DCP text work uses steps 0, 1 (for the target), writer apply on exact hashes, 10 and 11.

## 3. Lanes (pilot limit: ≤ 2 product lanes + 1 environment lane)

| Lane | Where it runs | Role now | `.antigravity` knowledge |
|---|---|---|---|
| **Terminal runtime lane** | Claude Code in Terminal on the Mac (native .NET 8, MongoDB, browser) | Environment owner: Q04 → Q24 → Q25 → later integration build and Phase 4.5 smokes | `@devops-agent`, `@testing-agent` |
| Lane-1 (chat) | Claude project chat | Product: design/pack preparation (Q50 label patch, closure-doc templates) | `@module-pack-author`, `@business-analyst` |
| Lane-2 (chat) | Claude project chat | Product: single pack writer (exact-hash applies) | `@module-pack-author` |
| Lane-3 (chat) | Claude project chat | Independent verifier for text work; runtime VER only from Terminal | `@read-only-auditor` |
| Lane-4 | Owner + CT | Decisions only | — |
| Integration owner | Terminal (appointed at Q15) | One integrated checkout; shared files in sequence | `@integration-agent` |

Chat lanes cannot run .NET, MongoDB or a browser. Any step that needs them goes to the Terminal lane, never to a chat lane.

## 4. Stage order

| Stage | Work | Queue | State now | Exit |
|---|---|---|---|---|
| 0 Protect | Refresh non-invasive backup | new Q52 | READY (CT) | Bundle + patch + tar + SHA256SUMS verified |
| 0 Protect | Commit strategy, branch naming, hygiene | Q03 (F-09, AG-09) | DECISION-REQUIRED | Owner decision |
| **R Runtime** | A12 independent VER attempt 2 incl. A08/A09 regression, PNG check | Q04 | READY (Terminal) | SOP-22 with PASS/FAIL per row |
| R | Evidence kit validation | Q24 | READY (Terminal) | Kit validated or defects listed |
| R | Loads 3.1.0 publication + guard binding (alone) | Q25 → Q32 VER | READY (Terminal) | Published with guard green; independent VER |
| 1 Packs | Rebased Returns self-registration patch 05 | Q48 | DECISION-REQUIRED | Signed → apply → VER |
| 1 Packs | Heading-label correction | Q50 | READY (prep) | Patch → sign-off → apply → VER |
| 1 Packs | S&OP / Capacity promotion | Q27, Q28 | DECISION-REQUIRED | `ready-for-dev` |
| 1 Decisions | DN-01, DN-02, PC rows | Q10–Q12 | DECISION-REQUIRED | Recorded |
| 2 Integration | Selection refresh (after A12 disposition) → decision → one checkout; first action: golden-flow vertical slice; then self-registration foundation (DCP-009 §21) | Q14 → Q15 | HELD → DECISION-REQUIRED | Composition build + route smoke; architecture tests run (F-11) |
| 3 Loads | Uptake after publication | Q09 | HELD | — |
| 4 UI wave | Per module through the §2 line; order: Shipment close → Carrier close (PNG) → Claims → Returns → Loads → S&OP/Capacity | Q18 | HELD (needs Stage 2) | Phase 4.5 + VER + CT per module |
| 5 Supplier | DC-01..05, then packs | Q19 | DECISION-REQUIRED | — |
| 6 Release | Golden flow, E5/G5, PNG, closure docs, rollout | Q42 + later | — | `/release-checklist` |

## 5. Owner decision queue (one at a time, question tool, recommended option marked)

1. Q48 rebased Returns self-registration patch (small, unblocks all six sections)
2. Q03 commit strategy (risk grows daily; includes branch naming and hygiene)
3. Q21 Rev2 measurement policy (enables cycle-time and rework measures)
4. Q27 / Q28 S&OP and Capacity pack promotion
5. Q11 → Q10 → Q12 Shipment policy (DN-02, DN-01, PC rows)
6. Q15 integration decision (after Q04 disposition and Q14 refresh)
7. Q19 Supplier DC-01..05

## 6. Measures (process v1.0 §9)

Unchanged. Effort is updated only at writer complete, independent VER and CT decision. Effort successor update Q51 reconciles the Q33 UI estimates and the self-registration estimate (38/70/127 h) with the 2,842 h ledger at the next CT decision event. Current: delivered 1,452 h (51.1 %), CT-accepted 1,128 h (39.7 %).
