# Q212 — VER MOD-0190 S&OP Workflow & Sign-offs: code in tree vs pack · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q212 · `AL-SCM-VER-0190` (VER) · required E1 (static) — **reached: E1**. Nothing built, no test run, no service started. |
| CT-QUEUE row | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv:308` |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `read-only-auditor` (mode **strict**; no fixes) → Phase B `documentation-writer` (this folder only) |
| Scope | `services/Diten.SupplyChainService/src/*/Features/SandopPlans/` · `…/tests/Diten.SupplyChainService.Tests/SandopPlans/` · the MOD-0190 pack · `MOD-0190-OWNED.tsv` |
| Pack under test | `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md`, 552 lines, sha256 `c076790df7fb…` (working-tree copy) |
| Session note | The prompt asks for its own session. This run shares a session with Q201, Q214 and Q210. `AGENTS.md` and the three agent files were read in full earlier in the session; their sha256 values were re-checked at the start of Q212 and are unchanged. The pack, the owned-path file, the dispatch-preflight `SOP-22.md`, the Q208 baseline and the Q214 blast-radius table were read for this WP. |
| Start / End (Europe/Istanbul) | 2026-10-02 20:39:33 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        BLOCKED (see §8). Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      89 " M" · 576 "??" · 0 staged = 665 at start and at end (see ARTIFACTS.sha256).
                      No .git/index.lock. No git diff. No git write. No gh. No fetch.
Changed files:        this record folder only
Tests:                NOT RUN (forbidden). Outcomes are read from the Q208 result file (SandopPlans 19/19).
Commands run:         file reads, grep, sha256, and the read-only identity gate verify_module_id.py (exit 0)
Decisions:            none taken. No pack checkbox ticked.
Out-of-scope changes: none
```

## 1. Acceptance walk (`ACCEPTANCE-MATRIX.tsv`, 87 rows)

Each row has a verdict, a `path:line`, and the test behind it. Line numbers are looked up from the files by
`tools/q212_matrix.py` when the table is generated.

| Group | Pack lines | Rows | MET | NOT MET | INSUFFICIENT EVIDENCE |
|---|---|---:|---:|---:|---:|
| §16 acceptance criteria (9 items) | 195–203 | 14 | 9 | 0 | 5 |
| §18 ready-for-dev checklist (10 items) | 217–226 | 11 | 9 | 1 | 1 |
| §12 validation rules | 155–162 | 8 | 3 | 0 | 5 |
| §13 failure paths | 166–173 | 13 | 7 | 0 | 6 |
| §4, §10, §14, §15 conventions | 8, 87, 141–144, 177, 189 | 8 | 4 | 3 | 1 |
| §17 test expectations | 207–213 | 7 | 2 | 3 | 2 |
| **Backend subtotal** | | **61** | **34** | **7** | **20** |
| §23.11 UI rows | 430–447 | 18 | 0 | 18 | 0 |
| §24 self-registration M-01…M-08 | 532–539 | 8 | 0 | 8 | 0 |
| **Total** | | **87** | **34** | **33** | **20** |

A criterion with independent parts is split into rows `a`, `b`, `c`. The rule applied is the one in the prompt: MET
only when the code does it **and** an in-tree test that passed in Q208 proves it, or when it is structural and
reading proves it. A criterion that needs HTTP, JWT or RBAC evidence is INSUFFICIENT EVIDENCE.

The nine §16 criteria, in short:

| Pack line | Criterion | Verdict |
|---|---|---|
| 195 | Scoped Draft create on the exact fixture | MET |
| 196 | Capture appends immutable provenance, InReview, no demand series | MET |
| 197 | Sign-off per role and snapshot; no overwrite; five approvals do not advance | MET |
| 198 | Receipt, fingerprint, replay, atomic audit + one Pending event | MET |
| 199 | Correlation: original in audit on replay | MET |
| 199 | Correlation: current UUID in header/error; 400 fallback; 401 | INSUFFICIENT EVIDENCE (controller-level; no test) |
| 200 | Cross-scope 404 | MET |
| 200 | Missing permission 403 | INSUFFICIENT EVIDENCE (gate unit test only; no real token) |
| 201 | Six operations exist; three event types | MET (structural) |
| 201 | Payload shape, required/null, error precedence vs YAML | INSUFFICIENT EVIDENCE (no contract-shape test) |
| 202 | Nothing shared with MOD-0192; frozen mocks | MET (structural) |
| 203 | Bounded E4 evidence | INSUFFICIENT EVIDENCE (none on this tree) |

The seven backend **NOT MET** rows:

| Row | What is absent | Module defect? |
|---|---|---|
| RFD-18-9b (pack `:225`) | "The 38 paths are absent" — they are all present since Q202a. Stale wording, also at `:266` and `:549`. | No — the pack text is stale |
| ENT-4 (pack `:8`) | Entities do not use `EntityBase` | Recorded by the pack itself as F190-ENT (`:468`) |
| BC-10-3 (pack `:143-144`) | Controller is on `ControllerBase`, not the service's base controller | Deviation from the pack text; in the accepted source |
| GW-15 (pack `:189`) | Gateway routes | No — integration-owned |
| TE-17-3 (pack `:209`) | Handler/API tests | Yes — the pack expects them; none exist |
| TE-17-4 (pack `:210`) | Contract tests for every success/error shape and event | Yes |
| TE-17-6 (pack `:212`) | Prism-compatible mock smoke | Yes |

The 26 UI and self-registration rows are NOT MET because nothing of the UI exists. The pack's status_note leaves
that open; it is not a defect of this module.

## 2. Scope (`OWNED-PATH-COVERAGE.tsv`, `SCOPE-DELTA.tsv`)

- **38 of 38 owned paths are present.** 32 source + 6 test.
- **38 of 38 are byte-equal to the CT-accepted source** (`source-manifest.tsv` `ae7ef59e…`, pack `:260`) and to
  BASE-STACK v2.
- **Present but outside the 38: none.** No other file in the service has "Sandop" in its path.
- **Present and authorized by neither the 38 nor the pack: none.**
- The owned-path file is byte-identical to the pack's other authority, `mvp6-mod0190-pack-phase15-close-01/OWNED-PATHS.tsv`
  (both `4bfc17f4…`, pack `:266`). 38 ∩ 43 (MOD-0192) = 0, recomputed.
- Absent, and not owned by this module: the `Program.cs` composition, the self-registration provider and its test,
  the 32 UI paths.

## 3. Boundary (`BOUNDARY-REFS.tsv`, 13 rows)

**No boundary violation found.**

| Pack prohibition (`:44-45`) | Finding |
|---|---|
| Copy forecast series or demand quantities into a second SoR | Not done. A snapshot stores the demand plan ID, version, checksum, time and contract label, plus opaque `(source, resourceId, resourceVersion)` strings. No quantity, series or forecast field exists (0 keyword hits in 32 files). |
| Edit MOD-0188 data | Impossible by construction. The module opens six `sandop_*` collections and nothing else, and has no HTTP client. |
| Own capacity scenarios | Not done. 0 references to CapacityPlans in either direction. |
| Accept tenant/LE scope in request bodies | Not done. Scope comes from signed claims and must equal the headers; any extra body property is rejected (tested). |
| Consume DEMAND v1 by reference only | Done through an in-memory exact-match fixture reader that returns a boolean — **permitted read-by-contract**, in the fixture form the pack prescribes (`:119`). |

Two things to note, neither a violation:

- The module uses **no shared-kernel type at all** — no `EntityBase`, no `CustomBaseController`, no `Response<T>`.
  That is why there is nothing to classify as "permitted shared kernel", and why ENT-4 and BC-10-3 are NOT MET.
- The unknown-commit test runs `killAllSessions` on the lane mongod (`SandopAtomicityTests.cs:61`). That is a
  test-side, server-wide side effect, safe only while the assembly runs serially.

## 4. The central question → `THINNESS-VERDICT.md`

**308 lines is the bounded core as delivered and accepted, not a scope shortfall.**

- All 38 paths exist with the accepted bytes; every owned object, operation, event, key, collection and index the
  pack lists is implemented.
- The line count misleads: the code is written in long lines. By bytes S&OP is 62% of Capacity (40,557 vs 65,428),
  not 34%. Capacity also carries an executor and a lease store (194 lines) that S&OP's scope excludes.
- It **is** thin in two ways the pack does not ask for: hollow layering (three entity files with no run-time
  caller; pass-through handlers and validators; a marker permission attribute) and a test layer below the pack's
  §17 (no handler, API, contract-shape or mock-smoke tests).

## 5. Test credibility — the three highest-risk criteria

| # | Criterion | Why highest risk | Test that proves it | Verdict |
|---|---|---|---|---|
| 1 | **Sign-off evidence is append-only: one decision per role and snapshot, never overwritten, also under a race** (pack `:130`, `:171`, `:197`) | A sign-off is the module's governed record | `SandopConcurrencyTests.cs:4` `Concurrent_different_keys_cannot_record_same_role_twice`; `SandopLifecycleTests.cs:4` (duplicate → 409); `SandopLifecycleTests.cs:13` (five roles, plan stays InReview). Code: `SandopRepository.cs:72-73`, unique index `SandopSchema.cs:9` | **MET** — with the caveat that the race protection needs the unique index, and only the tests create it (F-Q212-4) |
| 2 | **Tenant / legal-entity isolation and permission** (pack `:128`, `:200`) | A miss is silent and leaks data | Data scope: `SandopIsolationTests.cs:4`, `:10` → MET. Permission: `SandopContractTests.cs:22` calls the gate function with a fabricated principal; no real token, no action executed | **MET** for data scope · **INSUFFICIENT EVIDENCE** for RBAC/JWT |
| 3 | **Atomic mutation and unknown-commit handling** (pack `:131`, `:173`, `:198`) | Partial state, or a lost or duplicated plan or decision | Rollback: `SandopAtomicityTests.cs:33` `Receipt_insert_failure_after_plan_write_rolls_back_and_same_key_retries` — green for the right reason (Q214) → MET. Replay: `SandopReplayTests.cs:35` → MET. Unknown commit: `SandopAtomicityTests.cs:53` — **weak green** (armed `times: 3`, fires 1, asserts "at least 1"); `:70` — **cannot distinguish** (accepts 201 or 503) | **MET** for rollback and replay · **INSUFFICIENT EVIDENCE** for commit uncertainty |

On item 3, two points specific to S&OP:

- `SandopRepository.cs:81` commits once. There is no application retry loop, so "retry exhaustion" is not a
  behaviour this module has; arming `times: 3` tests nothing the code does three times.
- After an unknown result the code looks the receipt up (`:85`). Under the Q214 mechanism the server is unknown to
  the driver at that moment, so the lookup most likely times out rather than returning "no receipt". The
  response is the same 503, so the test cannot tell. "After scoped receipt resolution" (pack `:173`) is not shown.

What the test's own workaround implies is in `THINNESS-VERDICT.md` §5: the recovery half is proven through a
different, direct client after `killAllSessions`, not through the repository instance that got the unknown result.

## 6. Shared seams → `REACHABILITY.md`

Twelve seams, none patched. The module is half-wired: `MapControllers()` maps its six routes today;
`ISandopRepository` and `IDemandFixtureReader` are unregistered; requests would pass through the Shipment
middleware, whose rules contradict the S&OP contract in three places (correlation error code, scope-mismatch
status, idempotency-key trimming and length). Two seams have no named owner in anything I read: who calls
`SandopSchema.EnsureAsync`, and what supplies DEMAND outside a test environment.

## 7. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q212-1** | 🟢 Result | All 38 owned paths are present and byte-equal to the CT-accepted source. Nothing extra exists. | `OWNED-PATH-COVERAGE.tsv` |
| **F-Q212-2** | 🟢 Result | No boundary violation. MOD-0190 references no other feature and stores no demand data. | `BOUNDARY-REFS.tsv` |
| **F-Q212-3** | 🟢 Result | 308 lines is the bounded core, not a scope shortfall; the 34% line ratio is 62% by bytes. | `THINNESS-VERDICT.md` |
| **F-Q212-4** | 🟠 High | Nothing in the service creates the S&OP indexes. `SandopSchema.EnsureAsync` has one caller, a test helper. Three invariants (one plan per horizon+version, one snapshot per sequence, one sign-off per role and snapshot) rest on unique indexes; without them the in-transaction "find, then insert" check does not stop two concurrent inserts of different documents. The green concurrency test proves the invariant only with the index present. | `SandopSchema.cs:4-12`; `SandopContractTests.cs:6`; `SandopPersistenceRegistration.cs:5`; `SandopRepository.cs:72-75` |
| **F-Q212-5** | 🟠 High | The Shipment middleware sits in front of S&OP paths today, and its rules contradict the S&OP contract in three places. Registering the dependencies alone would not make the module correct. | `Program.cs:68`; `REACHABILITY.md` §2.1 |
| **F-Q212-6** | 🟠 High | The 19 tests execute the repository, two pure functions and the gate function. They execute no controller, handler, validator, pipeline behavior or real token. The prompt's assumption holds, measured: 0 hits for a host or a handler in the test folder. | `REACHABILITY.md` §1 |
| **F-Q212-7** | 🟠 High | Commit uncertainty has no credible test: one weak green, one test that accepts both outcomes, and a recovery proof that depends on `killAllSessions` plus a direct connection. | §5; `SandopAtomicityTests.cs:53-67`, `:70-86` |
| **F-Q212-8** | 🟡 Medium | Three pack test expectations are unmet: handler/API tests, contract-shape tests, mock smoke. Five product branches have code and no test: `SANDOP_PLAN_STATE_CONFLICT`, `SANDOP_SIGN_OFF_STATE_CONFLICT`, `INVALID_SNAPSHOT_REFERENCE`, `INVALID_CORRELATION_ID`, and the unpublished / cross-scope fixture. | rows TE-17-3, -4, -6; FP-13-1b, -4b, -5a, -5b, -8a |
| **F-Q212-9** | 🟡 Medium | Hollow layering: three entity classes and three projection methods have no run-time caller; validators check context only; the permission attribute is a marker. | `THINNESS-VERDICT.md` §4 T1, T4, T5 |
| **F-Q212-10** | 🟡 Medium | Two deviations from the pack text, both in the accepted source: no `EntityBase` (recorded by the pack as F190-ENT) and no service base controller (not recorded anywhere I found). | rows ENT-4, BC-10-3 |
| **F-Q212-11** | 🟡 Medium | Outside a test environment every create returns 422, because DEMAND is a fixture only. Correct for the bounded slice; it means the module cannot be exercised end to end until a DEMAND source is bound. | `SandopRepository.cs:49`; pack `:119` |
| **F-Q212-12** | ⚪ Low | Stale pack wording: the 38 paths are called absent at `:225`, `:266` and `:549`. Not corrected. | row RFD-18-9b |
| **F-Q212-13** | ⚪ Low | The pack says snapshot time is "UTC time" (`:160`); the code accepts any offset. The YAML type is `date-time`. | `SandopPlanModels.cs:18` |
| **F-Q212-14** | ⚪ Info | Contract pins hold: `sandop-capacity.openapi.yaml` `5213b535…`, its annex `f9d9d555…` and `demand.openapi.yaml` `3c77262e…` on disk equal the pack's and the preflight record's values. No pin drift for this module. | pack `:118`; preflight `SOP-22.md:9` |
| **F-Q212-15** | ⚪ Info | This WP ran in a session shared with earlier WPs, not in its own. Stated in the header. | — |

## 8. Overall verdict

**BLOCKED.** The bounded core is intact and unchanged from what CT accepted; what blocks acceptance on this tree is
evidence and wiring, in this order:

1. No HTTP, JWT or RBAC evidence on this tree — five §16 rows cannot be MET without it (correlation header and
   fallback, 403, payload/precedence parity, E4).
2. `Program.cs` composition: register `ISandopRepository` and `IDemandFixtureReader`, and take S&OP paths out of
   the Shipment middleware branch (Q209).
3. Index creation: someone must own the call to `SandopSchema.EnsureAsync` (F-Q212-4).
4. A credible commit-uncertainty test (Q215), without `killAllSessions` and a bypass client.
5. The three §17 test expectations and the five untested branches (F-Q212-8).
6. Gateway routes; shared permission registration; token claims confirmed.
7. A DEMAND source outside test fixtures, when the owner decides to go beyond the bounded slice.
8. A CT ruling on the base-controller deviation (F-Q212-10) and on whether the hollow layering is acceptable as is.
9. Later, by the pack's own status_note: UI, self-registration, navigation keys, event transport, Workflow, E5/G5.

Items 2, 6, 7 and 9 are outside this module's owned scope and are not defects of it. Items 1, 3, 4 and 5 are
where the module's own evidence falls short.

## 9. Known gaps of this VER

- Static only. No statement here is a run-time observation.
- The accepted source manifest was taken as given; its own SHA256SUMS was not re-verified beyond its file hash.
- Payload parity with the YAML was checked for operation and event names, not field by field.
- The pipeline behaviors, Auth service and gateway were read only as far as the cited lines.
- UI rows and M-rows were assessed for presence only.

## 10. Refused / not done

- No build, no test, no service start. The identity gate was the only program run; it only reads.
- Nothing under `.antigravity/`, `gateway/`, `frontend/`, `services/`, `scripts/` changed. No pack checkbox ticked.
  No seam patched. No ledger row changed.

## 11. Files

`SOP-22.md` · `ACCEPTANCE-MATRIX.tsv` · `OWNED-PATH-COVERAGE.tsv` · `THINNESS-VERDICT.md` · `SCOPE-DELTA.tsv` ·
`BOUNDARY-REFS.tsv` · `REACHABILITY.md` · `ARTIFACTS.sha256` · `tools/q212_matrix.py` (generates the four TSVs;
read-only on the repo)

Return to CT; CT decides.
