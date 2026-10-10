# MVP6-CT-AUDIT-2026-09-25 — Control Tower audit

Date: 2026-09-25 (Istanbul). Role: MVP6 Control Tower (Claude), first audit after the Codex → Claude handoff.
Requested by: repository owner (CEO), "as first stage do audit MVP6 and update development plan".
Successor plan: [v9.0](../../../roadmap/plans/mvp6-development-plan-v9.0.md).

**Profile: static, read-only audit.** No build, test, runtime, browser, publication, pack, contract, guard or Git
mutation was performed. .NET is not available in the audit environment, so no test result is claimed.
This record adds only this file. Historical plans, records and decisions are unchanged (docs K4).

## 1. Baseline

| Item | Observed |
|---|---|
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| Last commit | 2026-09-16 17:54 +0500, "feat(supply-chain): implement MOD-0183 shipment core" |
| Working tree | 337 short-status rows (13 `M`, 324 `??`); 3,477 rows with `-uall` |
| Of which | 98 service/test source paths; 2,938 files under `docs/records`; `docs/records/audits/2026-09` = 129 MB |
| Worktrees | 17 registered; 10 `/private/tmp` entries prunable; `~/.codex/worktrees` not visible from this environment (not proof of absence) |
| Handoff package | `SHA256SUMS` 12/12 OK; uploaded ZIP byte-identical to repo copy; `SOURCES.tsv` 1,381/1,381 files match current repo |
| A12 identities | patch `b3c7dcbb…`, successor manifest `8ffa6c96…`, archive `7b6a0d1a…`, predecessor manifest `7d7bec63…` — all PASS |

## 2. Canonical contracts (fresh SHA-256)

| Contract | State | Hash |
|---|---|---|
| SHIPMENT-BUNDLE | 3.0.0 FROZEN (canonical) | `5dfe7c1b…d21c` |
| Loads annex v2 | canonical, unchanged | `a2187c93…2be1` |
| Loads 3.1.0 amendment | FINAL-PROPOSED, **not published**; preimages intact | YAML `6dc1dd48…`, annex `9d8a3706…` (candidate) |
| SANDOP-CAPACITY | 3.0.0 FROZEN — **published**, equals approved target | `5213b535…adab`; v3 annex `f9d9d555…` present; v2 annex `eb1df138…` unchanged |
| Carrier semantics | v1.1.0, guard-bound | `87557ef9…` |
| SUPPLIER-PERFORMANCE | 1.0.0 FROZEN (file header) | not re-verified against a decision in this audit |

Docs-path guard authority: `docs/reference/architecture/docs-path-authority.json` status `APPROVED`; payload recomputed with the
test algorithm = `3bd20e26…`, equal to decision `MVP6-CAPACITY-GUARD-SINGLE-DISPOSITION-01` (file `877c3878…`).
This proves record integrity only; human authorization is a separate CT fact.

## 3. Module status

| Module | Pack (HEAD / tree) | Bounded backend CT | UI | Integrated (common checkout) | Full module / G5 |
|---|---|---|---|---|---|
| 0183 Shipment/POD | ready-for-dev / ready-for-dev | ACCEPTED (core, Root R2 producer, Root R2 emission) | PARTIAL: B01/B02, accessibility, A03/PRES-183-02, A08/A09 accepted; A12 static only; A10/A13/A14/PNG open | No | No |
| 0184 Carrier | draft / ready-for-dev | ACCEPTED (bounded E4, 18 Sep) | PARTIAL: real-Auth functional accepted, PNG open | No | No |
| 0185 Loads | draft / ready-for-dev | ACCEPTED (bounded WP) | list/create design only; no UI authority | No | No |
| 0186 Returns | draft / draft ("DEV/VER HELD") | ACCEPTED (isolated WP, lane authority) | none | No | No |
| 0187 Claims | draft / draft ("Phase 1.5 BLOCKED") | ACCEPTED (bounded WP, lane authority) | none | No | No |
| 0190 S&OP | draft / draft | ACCEPTED (isolated WP) | none | No | No |
| 0192 Capacity | draft / draft | BOUNDED ACCEPTED (BC successor `dec28b6…`) | none | No | No |
| 0147 Supplier Performance | draft / draft | none (spec only) | none | No | No |
| 0148 Supplier Portal | draft / draft | none (spec only) | none | No | No |

Milestone counts (gate counts, not effort): bounded backend CT acceptance **7/9**; bounded UI acceptance **0/9 complete, 2/9 partial**;
common-checkout integration **0/9**; full module / E5 / G5 **0/9**. Effort index (Update 07): **1,452 / 2,842 h = 51.1%**;
CT-accepted share under Rev2 rules **1,128 h = 39.7%** (see the 25 Sep delivery/performance report).

## 4. Findings

| ID | Severity | Finding | Evidence | Required action (owner) |
|---|---|---|---|---|
| F-01 | **High** | Nine days of MVP6 work exist only in the local working tree. No commit since 16 Sep; 3,477 changed paths including accepted production source (Carrier, Loads, Shipment root), all 2026-09 records (129 MB), pack promotions and the 10 untracked owner-decision records. A disk loss would erase the evidence chain the acceptances depend on. | `git log`, `git status -uall` | Owner decision: non-invasive backup (git-backup-policy §A: bundle + tracked patch + untracked tarball in `.git-backups/`), then a separate commit decision. |
| F-02 | **High** | No accepted module runs in one integrated checkout. Accepted work sits in separate immutable archives (Shipment UI 360, Auth 22, Capacity BC 422, S&OP 379, Returns/Claims packages). Integration dispatch v1.0 is HELD and exec-01 is BLOCKED for a target-bound decision; its 422-path selection predates the 23–25 Sep successors. | `mvp6-integration-baseline-selection-01/`, `mvp6-integration-baseline-exec-01/SOP-22-BLOCKED.md` | Refresh the selection to the latest accepted successors, then one exact owner decision for one integration writer. |
| F-03 | Medium | Pack status is inconsistent with acceptance. HEAD: 1/9 ready-for-dev; tree: 3/9. Returns, Claims, S&OP and Capacity packs say draft / HELD while bounded backend work was accepted under lane-local authority copies. | pack frontmatter; `mvp6-mod0186-pack-activate-01` | New DEV on these modules must cite lane authority; promote packs through the prepared deltas (`mvp6-final-pack-delta-01` for Returns) — owner decision. |
| F-04 | Medium | Plan chain is stale. v8.0 (18 Sep) and continuation 2026-09-23 still list completed steps: Capacity v3 publication (canonical now equals the approved target), BC uptake (accepted 23 Sep), first Carrier UI (real-Auth E2E done). | §2 hashes; BC CT handoff | Superseded by plan v9.0. |
| F-05 | Medium | A12 was reported "not runnable" because the verifier did not apply the proven HEAD-archive + overlay method (HEAD `4a8d4d4` + A12 360 overlay + Auth 22 overlay; no path overlap). Owner confirmed on 25 Sep that existing authority covers a disposable, evidence-only run. | a08/a09/a12 evidence `runtime-and-input-hashes.txt`; CT decision 25 Sep | Lane-1 runtime VER per plan v9.0. |
| F-06 | Medium | Mandatory durable PNG evidence is open for Carrier and Shipment UI. No supported save/export path was demonstrated under the previous tooling; Claude tooling not yet checked. | `mvp6-carrier-real-auth-ct-review-01/PNG-DISPOSITION.md` | Capability check inside Lane-1; otherwise owner decision on the evidence criterion (no waiver implied). |
| F-07 | Medium | First-review acceptance 0/8 bounded work packages; 3/8 were returned only for evidence quality. | CT review records | Pre-submission evidence checklist for every writer lane (plan v9.0 §6). |
| F-08 | Medium | Owner decisions awaiting input block most remaining work: Loads A/B/C, DN-01 (A10 proxy), DN-02 (A14 profile), PC-02/03/04/28, Supplier DC-01..05, integration decision (F-02), backup/commit (F-01). | handoff AUTHORITY-AND-GATES; decision packs | Present one at a time, recommended option marked. |
| F-09 | Low | Repository hygiene before any commit: 29 files > 1 MB under `docs/records` (logs/TRX, docs K7); untracked `TestResults/` folders under `services/.../tests` and `tests/architecture`. | `find -size +1M` | Decide K7 handling and ignore rules together with F-01. |
| F-10 | Low | Docs structure deviations: `docs/` has 7 top-level folders (`analysis/`, `decisions/` beyond the five of K2), both already tracked in HEAD and holding guard-bound canonical contracts; `docs/roadmap/plans` has 23 top-level files (> 20 without an axis, K3); v9.0 adds one. | docs K-checks | Record only. Any move needs the §4 protocol and guard re-binding — not proposed now. |
| F-11 | Info | Architecture tests (incl. DocsPathGuard) and service tests were not run by this audit; their status on the current dirty tree is unknown. | — | Run inside the integration lane (F-02), not in the common checkout. |

## 5. Scope boundaries preserved

No new acceptance, waiver, effort credit or percentage is created. Isolated bounded acceptances are not common-checkout,
E5/G5 or rollout acceptance. Candidate decision texts in prepared packs are not approvals. A10 remains unauthorized.

## 6. Next

Plan v9.0 sets the stage order. First owner decision: F-01 backup.
