# Q213 — VER MOD-0192 Capacity Planning against its pack · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q213 · `AL-SCM-VER-0192` (VER) · required E1 — **reached: E1** (static; nothing built, run or started) |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `read-only-auditor` — mode **strict**, no fixes, no Write. Phase B `documentation-writer` — this folder only |
| Scope (§17.4) | `src/*/Features/CapacityPlans/` (35 files, 904 lines, all read) + `tests/…/CapacityPlans/` (10 files, 930 lines, all read) + the pack (571 lines, read in full) + `MOD-0192-OWNED.tsv` (43 rows) |
| Read first | `AGENTS.md` and the three agent/workflow files (unchanged since Q211, same sha256) · the pack · `MOD-0192-OWNED.tsv` · preflight `SOP-22.md` · Q208 `NEW-BASELINE.tsv` · Q214 `RECOMMENDATION.md`, `BLAST-RADIUS.tsv` |
| Start / End (Europe/Istanbul) | 2026-10-02 20:39:50 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        BLOCKED for module acceptance in the common checkout (list in §7).
                      Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      git status --porcelain: 89 " M" · 576 "??" · 0 staged = 665 at start and at end.
                      No .git/index.lock. No git diff, no fetch, no git write, no gh.
Changed files:        this record folder only
Build / Tests:        NOT RUN (forbidden). Test results are cited from Q208 (CapacityPlans 48/48) and Q214.
Fixes:                none. No checkbox ticked, no ledger row changed.
```

## 1. Acceptance walk (`ACCEPTANCE-MATRIX.tsv`, 59 rows)

| Block (pack lines) | Rows | MET | NOT MET | INSUFFICIENT EVIDENCE |
|---|---:|---:|---:|---:|
| §16 Acceptance Criteria (194-203) | 10 | 6 | 1 | 3 |
| §18 Ready-for-dev checklist (217-228) | 12 | 11 | 0 | 1 |
| §13 Failure paths (165-172), supplementary | 8 | 4 | 0 | 4 |
| §23.11 UI rows CP-* (446-466) | 21 | 0 | 21 | 0 |
| §24 manifest tests M-01…M-08 (551-558) | 8 | 0 | 8 | 0 |
| **Total** | **59** | **21** | **30** | **8** |

The 29 UI and manifest rows and the G5 row are open by the pack's own text (line 279). Their absence is not a defect.

§16 in one line each:

| Line | Criterion (short) | Verdict |
|---|---|---|
| 194 | One scoped plan aggregate, immutable DEMAND provenance | MET |
| 195 | No demand series persisted | MET |
| 196 | Scenario stores only owned adjustments and opaque references | MET |
| 197 | Evaluation persists once; four states; fixture oracle; fenced executor | MET (repository / process level) |
| 198 | Terminal effect atomic: result, slot release, one audit, one Pending event | MET |
| 199 | Correlation preserved from HTTP request through response, audit, event | INSUFFICIENT EVIDENCE (HTTP half untested) |
| 200 | Cross-scope 404; missing permission 403 | INSUFFICIENT EVIDENCE (handlers and middleware untested) |
| 201 | Six operations match YAML and annex, full matrix | INSUFFICIENT EVIDENCE (routes and pin proven; matrix not) |
| 202 | No persistence or DTO shared with MOD-0190 | MET (structural) |
| 203 | G5 evidence | NOT MET (open by the pack) |

## 2. Scope (`OWNED-PATH-COVERAGE.tsv`, `SCOPE-DELTA.tsv`)

- **43 of 43 owned paths present.** All 43 equal their Q202a postimage (38 from BASE L2 A12-360, 5 test files as
  rewritten by Q131). `MOD-0192-OWNED.tsv` sha256 `88f2327d…` = the pack's pin; 0 paths shared with MOD-0190's 38.
- **2 files present but outside the 43:** `CapacityTestMongo.cs` and `CapacityTestMongoTests.cs`. Both are Q131
  NEW files (CT ACCEPTED) and fall inside the pack's Repo Scope glob (line 94). Authorized, but the 43-path list is
  now two short of the folder.
- **Nothing present that neither the 43 nor the pack authorizes.**

## 3. Boundary (`BOUNDARY-REFS.tsv`, 13 rows)

- 0 references from `Features/CapacityPlans/` into any other feature's namespace; 0 references from S&OP into Capacity.
- 7 collections, all `capacity_*`. No HTTP client, no publisher, no shared outbox.
- DEMAND and constraints are hard-coded fixtures; only the reference tuple is stored. MOD-0188 is neither called nor edited.
- No boundary violation found. Three notes for CT: B-03 (two `IDemandFixtureReader` / `DemandFixtureReader` with the
  same simple name), B-11 (one cross-tenant discovery read), B-12 (product code works for the fixture tenant only).

## 4. The reference pattern

`FAILPOINT-REFERENCE-PATTERN.md` — error code 8 with an explicit label (or write-concern error 64), `appName` scope
with a separate control client, count read before and after, disarm in `finally`, serial collection. It also says
what Q215 must **not** copy as-is: Capacity asserts `hits >= 1` because its product code has no commit retry loop;
Loads and Returns need an exact count.

Timing check of the 48 tests: none of the 12 fail-point episodes depends on timing. Two tests do
(`Twenty_distinct_keys…` under load; `Separate_processes…` by design), one makes a client-clock assumption, and one
(`ChildWorker`) is vacuous in a normal run.

## 5. Test credibility — the three highest-risk criteria

| Risk | Criterion | Test that proves it | Verdict |
|---|---|---|---|
| Tenant / legal-entity isolation | §16 line 200; §13 line 166 | `CapacityIsolationTests.Invalid_fixture_and_foreign_or_deleted_scope_create_no_visible_data` (`CapacityIsolationTests.cs:12`, lines 31-34, 42-43) and `CapacityAtomicityTests.cs:87` — foreign tenant, foreign LE and soft-deleted plan read as null | MET for a **plan read at repository level**. **INSUFFICIENT EVIDENCE** for scenario/evaluation reads, for the 404 mapping and for 403 |
| One durable terminal effect | §16 line 198 | X06 `CapacityAtomicityTests.cs:313`; X07 `:333`, `:350`, `:180` (13 mutants), `:232` (5 boundaries); `CapacityLeaseTests.cs:12` | **MET** |
| One active evaluation; restart recovery | §16 line 197; §13 line 170 | `CapacityConcurrencyTests.Twenty_distinct_keys_produce_one_active_evaluation` (`:100`); `CapacityRestartTests.Separate_processes_claim_once_and_restart_recovers_after_server_lease_expiry` (`:75`) | **MET**, with the timing notes in §4 |

Criteria with no test behind them: correlation over HTTP (line 199), 403 (line 200), the contract matrix (line 201),
duplicate plan 409 and unknown scenario 404 (§13 lines 167, 169), the 400 family (§13 line 172).

## 6. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q213-1** | 🔴 Blocker (open by the pack) | 0 of 6 endpoints usable through the real service: no gateway route, no Program.cs registration, no middleware, no permission seed. | `REACHABILITY.md` rows 1, 4-9 |
| **F-Q213-2** | 🟠 High — for Q209 | The evaluation executor is a `BackgroundService` registered nowhere, and `AddCapacityPersistence()` does not register it or the two fixture readers the repository needs. Calling that method alone yields a service that accepts evaluations and never completes them. | `REACHABILITY.md` "The executor"; `CapacityPersistenceRegistration.cs:10-13` |
| **F-Q213-3** | 🟠 High | The 48 tests never touch HTTP. 0 hits in all ten test files for the controller, the middleware, any handler, any validator, the permission attribute, `WebApplicationFactory` or `DefaultHttpContext`. Three of ten §16 criteria are therefore INSUFFICIENT EVIDENCE. | `REACHABILITY.md` "What the 48 tests exercise" |
| **F-Q213-4** | 🟡 Medium | Reference pattern delivered, with a caveat for Q215: Capacity's `hits >= 1` is correct only because Capacity has no commit retry loop. Loads and Returns need an exact count, or they stay green for the wrong reason. Also: `appName` limits who can be hit, not who can arm or disarm — the serial collection stays necessary. | `FAILPOINT-REFERENCE-PATTERN.md` §3-§4 |
| **F-Q213-5** | 🟡 Medium | Two of the 48 tests depend on timing and one is vacuous: `Twenty_distinct_keys…` (bounded retry budget of 5 attempts), `Separate_processes…` (31 s sleep, three child processes), `ChildWorker` (returns at once unless a child variable is set). The same retry budget means the product can answer 503 instead of 409 under contention; not observed, not run. | `FAILPOINT-REFERENCE-PATTERN.md` §5; `CapacityRepository.cs:146,172-173` |
| **F-Q213-6** | 🟡 Medium — CT to confirm | `FindPendingScopesAsync` reads `capacity_evaluations` without a tenant filter. It returns only scope pairs and all later work is scoped and fenced, but AGENTS.md states the tenant rule without exception and the pack does not name this one. | `BOUNDARY-REFS.tsv` B-11; `CapacityLeaseStore.cs:23-29` |
| **F-Q213-7** | 🟡 Medium | `RenewAsync` has no production caller. The pack describes a 10 s renewal; the executor never renews. Harmless with the instant fixture oracle. | `REACHABILITY.md` "The executor" item 1 |
| **F-Q213-8** | 🟡 Medium | Untested failure paths: `CAPACITY_PLAN_ALREADY_EXISTS`, `UNKNOWN_CAPACITY_SCENARIO`, `UNKNOWN_CAPACITY_PLAN`, `UNKNOWN_CAPACITY_EVALUATION`, `INVALID_CORRELATION_ID`, `INVALID_REQUEST`, `FORBIDDEN`, `UNAUTHENTICATED` — 0 hits each in the tests. No validator checks the decimal-string format of `availableCapacityDelta`. | `ACCEPTANCE-MATRIX.tsv` FP13-3, -5, -8 |
| **F-Q213-9** | ⚪ Low | Naming hazard at the seam: MOD-0190 and MOD-0192 each have an `IDemandFixtureReader` and a `DemandFixtureReader`. | `BOUNDARY-REFS.tsv` B-03 |
| **F-Q213-10** | ⚪ Low | The module is a fixture slice: product code completes evaluations only for one hard-coded tenant and legal entity. Authorized by the pack; to keep in view before any rollout wording. | `BOUNDARY-REFS.tsv` B-12 |
| **F-Q213-11** | ⚪ Low | The 43-path list is no longer the whole folder: Q131 added two test files. Stale pack wording: line 277 still says of the 43 paths "none exists in the common checkout yet". Nothing was edited. | `SCOPE-DELTA.tsv` rows 2-3; pack line 277 |
| **F-Q213-12** | ⚪ Info (positive) | Scope and boundary are clean; the contract and annex in the tree equal the published 3.0.0 pins (`5213b535…`, `f9d9d555…`); the owned-path file, the executor decision and the module ID check all match their pins. | `ACCEPTANCE-MATRIX.tsv` RFD18-01, -05, -09, -11 |
| **F-Q213-13** | ⚪ Info | The prompt's quote 'a feature-local executor "alone is not startup composition"' was not found in the pack by grep. The substance is confirmed from code (F-Q213-2). | `REACHABILITY.md` |

## 7. Overall verdict — **BLOCKED** for module acceptance in the common checkout

What stands today: the 43-path backend arrived intact, stays inside its pack, shares nothing with MOD-0190, and its
repository, lease store and fixture executor are proven by 48 component tests — including the one fail-point
pattern in the suite that is green for the right reason.

Missing:

1. Capacity composition in `Program.cs`: `AddCapacityPersistence()`, the two fixture readers, the executor as a hosted service, the middleware branch, the model-error adapter — Q209.
2. Gateway routes for the six operations — integration-agent.
3. The four permission keys in the shared definition / seed.
4. Any HTTP-level evidence in this tree: controller, middleware, handlers, validators, 401/403/400/404 mapping, correlation header, real JWT. The September hosted acceptance exists as a record; nothing in the tree reproduces it.
5. Tests for the untested failure paths of F-Q213-8.
6. A CT decision on the cross-tenant discovery read (F-Q213-6).
7. A CT decision on whether the missing renewal call matters for this slice (F-Q213-7).
8. Open by the pack and outside this lane: live DEMAND and constraint producers, publisher, UI (21 rows), self-registration (8 rows), E5/G5.

Items 1-3 are shared seams and were not patched. Item 5 is test work for a DEV lane.

If CT's question is narrower — "is the Q202a uptake of the bounded 43-path package faithful and clean?" — the answer
from this lane is yes.

## 8. Refused / not done

- No build, no test run, no service start, no edit, no patch to `Program.cs` or `ocelot.json`, no checkbox ticked, no ledger row changed.
- The September records the pack cites (hosted acceptance, X01/X07 VER, owner decisions) were confirmed to exist, and to match where the pack gives a hash that was checked; their content was not re-audited.
- The contract matrix was not compared field by field with the YAML and annex.
- The read-only auditor's standard no-change block uses `git diff`; the prompt forbids it. The no-change proof is porcelain-based (665 = 665).
- Read-only script run: `verify_module_id.py` (exit 0). It writes nothing.

## 9. Files

`SOP-22.md` · `ACCEPTANCE-MATRIX.tsv` (59 rows) · `OWNED-PATH-COVERAGE.tsv` (43 rows) · `FAILPOINT-REFERENCE-PATTERN.md` ·
`SCOPE-DELTA.tsv` (17 rows) · `BOUNDARY-REFS.tsv` (13 rows) · `REACHABILITY.md` · `ARTIFACTS.sha256`

Return to CT; CT decides.
