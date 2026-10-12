# Q211 — VER MOD-0187 Claims Management against its pack · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q211 · `AL-SCM-VER-0187` (VER) · required E1 — **reached: E1** (static; nothing built, nothing run) |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `read-only-auditor` — mode **strict**, no fixes, no Write. Phase B `documentation-writer` — this folder only |
| Scope (§17.4) | `src/*/Features/Claims/` (34 files, 825 lines) + `tests/Diten.SupplyChainService.Tests/Claims/` (10 files) + `tests/claims/` (3 probes) + `MOD-0187-claims-management.md` (819 lines, read in full) |
| Read first | `AGENTS.md` (unchanged since Q208, sha256 `ce8c12ad…`) · `read-only-auditor.md` · `read-only-audit.md` · `documentation-writer.md` · the pack · Q208 `NEW-BASELINE.tsv`, `FAILURE-CLASSIFICATION.tsv` · Q202a `WRITTEN-FILES.tsv` |
| Start / End (Europe/Istanbul) | 2026-10-02 20:15:26 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        BLOCKED (list in §6). Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      git status --porcelain: 89 " M" · 576 "??" · 0 staged = 665 at start and at end.
                      No .git/index.lock. No git diff, no git fetch, no git write, no gh.
Changed files:        this record folder only
Build / Tests:        NOT RUN (forbidden; Q214 owns the only build). Test results are cited from Q208.
Fixes:                none. No checkbox ticked. No file in the pack, source, tests, Program.cs or ocelot.json touched.
```

## 1. Acceptance walk (`ACCEPTANCE-MATRIX.tsv`, 52 rows)

| Block (pack lines) | Rows | MET | NOT MET | INSUFFICIENT EVIDENCE |
|---|---:|---:|---:|---:|
| §16 Acceptance Criteria (121-126) | 6 | 4 | 0 | 2 |
| §18 Ready-for-dev checklist (135-139) | 5 | 5 | 0 | 0 |
| §27 DEV prerequisites (450-457) | 8 | 6 | 1 | 1 |
| §26 A01–A12 (409-420) | 12 | 3 | 0 | 9 |
| The one failing test (restart) | 1 | 0 | 0 | 1 |
| §32.11 UI rows CU-* (698-709) | 12 | 0 | 12 | 0 |
| §33 manifest tests M-01…M-08 (799-806) | 8 | 0 | 8 | 0 |
| **Total** | **52** | **18** | **21** | **13** |

Backend only (first five blocks): 18 MET · 1 NOT MET · 13 INSUFFICIENT EVIDENCE.

## 2. Pack line 138 — "MOD-0183 executable verification is available" (unchecked)

**Satisfiable now, at E2. Not ticked.**

- MOD-0183 is 74/74 in the Q208 baseline (`NEW-BASELINE.tsv:2`).
- The seam Claims depends on exists in the tree: `ShipmentProjection.cs:31-33` emits `lifecycleCorrelationId`.
  Pack line 485 had recorded that seam as missing.

Three limits for CT to weigh before ticking:

1. The evidence is on the uncommitted working tree.
2. Every acceptance criterion in the MOD-0183 pack is itself still unchecked (its lines 243-253), and that pack is
   held as a three-way conflict.
3. Claims has never called the real Shipment endpoint. All 12 reference tests use a fake transport.

## 3. The one failing test

`Claims.ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` (`ClaimReplayTests.cs:43-54`).

- It is the test behind **A06 "restart"** and **A09 "restart fresh process"** (pack lines 414, 417), and behind
  R08 / R25 / R29 "two-process restart" (`mvp6-mod0187-ct-accept-01/R01-R30.tsv:9,26,30`).
- In an ordinary run those legs are **INSUFFICIENT EVIDENCE**. The failure is by design, not a defect.
- Nothing in the repository can run it: `CLAIMS_RESTART_MODE` is set nowhere outside the test, and
  `tests/claims/restart_probe.py` only compares two JSON observations captured elsewhere (its own docstring:
  "Does not start a host").
- Consequence (F-Q208-3): an unfiltered `dotnet test` always exits 1. A CI gate needs a filter or an agreed
  416/417 rule.

## 4. Test credibility — the three highest-risk criteria

| Risk | Criterion | Test that proves it | Verdict |
|---|---|---|---|
| Tenant / legal-entity isolation | A03; AGENTS.md "Kiracı izolasyonu" | `ClaimReplayTests.Scope_SoftDeleteAndActorReplay_PreserveReceiptAndAudit` (`ClaimReplayTests.cs:17`): foreign tenant and foreign LE each get an empty list and 404, counts unchanged. Middleware: `ClaimIsolationTests.cs:69,82,102` | MET at repository and middleware-component level. **INSUFFICIENT EVIDENCE at HTTP level**: no real-JWT test (`ClaimIsolationTests.cs:23-24`) |
| Atomic, exactly-once mutation | §16 line 124; A07 | `ClaimAtomicityTests.Mutation_FailureAfterDistinctWriteStage_RollsBackAllFourCollections` (`:41`, five stages) · `Mutation_ResponseLostAfterCommit_…` (`:50`) · `ClaimConcurrencyTests.Create_TwentySameKeys_OneFourCollectionCommit` (`:17`) | **MET** |
| Durable recovery across a restart | A06, A09 | `ClaimReplayTests.cs:43` — red by design | **INSUFFICIENT EVIDENCE** |

Other criteria with no test behind them: "Settled creates no payment record" (§16 line 125) and A10 — static proof
is clean, but there is no test and no architecture guard.

## 5. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q211-1** | 🔴 Blocker | Claims is not reachable through the real service: 0 of 3 endpoints. No Program.cs registration, middleware or reference client; no config key; no gateway route; no permission seed. | `REACHABILITY.md` rows 1, 4-11 |
| **F-Q211-2** | 🟠 High (corrects the prompt) | The Claims tests do **not** compose a host. `WebApplicationFactory` / `TestServer` = 0 hits in the Claims test folder. `ClaimsController`, `CreateClaimHandler`, `GetClaimListHandler`, the validators and real JWT are never executed. The 128 green tests are component tests. | `REACHABILITY.md` "What the tests actually exercise"; `ClaimIsolationTests.cs:23-24` |
| **F-Q211-3** | 🟠 High | Durable recovery across a restart (A06, A09) has no runnable evidence in this tree and no driver. | §3 |
| **F-Q211-4** | 🟠 High | The pack pins SHIPMENT-BUNDLE 3.0.0 (`5dfe7c1b…`, line 533). The tree holds 3.1.0 (`6dc1dd48…`, uncommitted edit). Parity of the Claims operations against 3.1.0 is not measured; no test validates against the OpenAPI file at all. The two annexes still match their pinned hashes (`16e65c26…`, `7d1327a1…`). | `docs/analysis/contracts/shipment-bundle.openapi.yaml:13`; `ACCEPTANCE-MATRIX.tsv` AC16-2, DEV27-2 |
| **F-Q211-5** | 🟡 Medium | Negative amount rules are untested: `CLAIM_AMOUNT_INVALID`, `CLAIM_APPROVAL_AMOUNT_INVALID`, `CLAIM_APPROVED_AMOUNT_NOT_ALLOWED` have 0 hits in the tests. The rules exist in code (`ClaimLifecycle.cs:12,18,19`). Also untested: `UNSUPPORTED_MEDIA_TYPE`. | `ACCEPTANCE-MATRIX.tsv` A08 |
| **F-Q211-6** | 🟡 Medium | "Settled does not create a payment/AP/AR record" has no test and no architecture guard, although the pack expects one (line 131). Static proof is clean. | `ACCEPTANCE-MATRIX.tsv` AC16-5, A10 |
| **F-Q211-7** | 🟡 Medium (possible, not verified) | Since Q202a the Application assembly contains Claims handlers whose dependencies Program.cs does not register. Host tests run in `Testing`, where the container is not validated. Whether the service still starts in `Development`: insufficient evidence. For Q209 / Q214. | `REACHABILITY.md` "A start-up risk" |
| **F-Q211-8** | 🟡 Medium | Until Q209, a request to `/api/shipment-bundle/claims` falls into the generic Shipment middleware branch. | `BOUNDARY-REFS.tsv` B-13; `Program.cs:68` |
| **F-Q211-9** | ⚪ Low | The claim document stores a point-in-time `ReferenceSnapshot` with `ShipmentStatus` and `CarrierStatus`. It is not a Carrier master copy and is never updated, but the pack says "no Carrier mutation or local copy" (line 72). CT to confirm it is the approved reference-snapshot choice (line 455). | `BOUNDARY-REFS.tsv` B-07 |
| **F-Q211-10** | ⚪ Low | Stale pack wording: line 23 still says "Execution gate: `status: draft`"; §16 line 121 is a draft-era criterion; §18 lines 138-139 and §27 lines 453-457 are unchecked although later sections supersede them. Nothing was ticked or edited. | pack lines 9, 23, 121, 138-139, 453-457 |
| **F-Q211-11** | ⚪ Info (positive) | Scope is exact: the tree holds the 47 owned paths, no extra file, no `ClaimOutboxWorker`, and all 47 equal their Q202a postimage. Boundary is clean: 0 references into other features' namespaces, only two HTTP GETs (Shipment, Carrier list), only the four `claims*` collections. Carrier: optional, read-only, no by-ID call. Module ID check re-run: OK, exit 0. | `SCOPE-DELTA.tsv`; `BOUNDARY-REFS.tsv` B-01…B-12 |
| **F-Q211-12** | ⚪ Info | UI (12 CU rows) and self-registration (8 M rows) are not in the tree. They are Q202b / UI-lane scope; listed as NOT MET so they are not silently dropped. | `ACCEPTANCE-MATRIX.tsv` |

## 6. Overall verdict — **BLOCKED**

What can be said today: the 47-path Claims backend arrived intact, stays inside its pack, respects every module
boundary, and its domain, repository and middleware logic is proven by 128 component tests.

Missing before CT acceptance of the module in the common checkout:

1. Claims composition in `Program.cs` (DI, reference client, middleware, Shipment-branch exclusion, error adapter) — Q209.
2. `Claims:ReferenceBaseUrl` configuration.
3. Gateway routes for the three operations — integration-agent.
4. The five permission keys in the shared catalogue / seed.
5. At least one HTTP-level test with a real JWT through the composed service (A01, A03) — none exists in this tree.
6. A runnable driver and result for the two-process restart test (A06, A09).
7. Contract parity of the Claims operations against the 3.1.0 file now in the tree — or a decision that 3.0.0 stays the pin.
8. Tests for the negative amount rules (D187-02) and for "no finance record".
9. A start-up check in `Development` after Q209 (F-Q211-7).
10. If "module" includes §32 and §33: the UI and the manifest provider (20 rows).

Items 1-4 are shared seams and were not patched. Items 5, 6 and 8 are test work for a DEV lane.

## 7. Refused / not done

- No build, no test run, no edit, no patch to `Program.cs` or `ocelot.json`, no checkbox ticked.
- The September acceptance records (R01–R30, CT acceptance, owner decisions) were confirmed to exist and, where the
  pack gives a hash, to match; their content was not re-audited.
- Contract parity against the OpenAPI file was not measured field by field.
- The read-only auditor's standard no-change block uses `git diff`; the prompt forbids it. The no-change proof is
  porcelain-based (665 = 665) — same deviation as F-Q201-11.

## 8. Files

`SOP-22.md` · `ACCEPTANCE-MATRIX.tsv` (52 rows) · `SCOPE-DELTA.tsv` (47 paths + 12 items) · `BOUNDARY-REFS.tsv` (13 rows) ·
`REACHABILITY.md` · `ARTIFACTS.sha256`

Return to CT; CT decides.
