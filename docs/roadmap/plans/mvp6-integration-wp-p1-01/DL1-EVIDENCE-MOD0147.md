# MOD-0147 DL-1 — evidence (Q502, steps 1–5)

- Branch `feature/sce/mod0147-dl1-01`, base `origin/main` `63acdf2ec`. Local commits only; nothing pushed, no PR.
- Pack `MOD-0147-supplier-performance-risk.md`, sha256 `f1c951bc4d68bd617326bed9f7c2e835393765424832fe46ee709cc12216aa03`
  (measured before the first commit; ready-for-dev, CT-APR-02 UYGUN `docs/records/reviews/MOD-0147/2026-10-28-f1c951bc.md`).
  Plan: `MOD-0147-DL1-IMPLEMENTATION-PLAN-r2.md` (outside the repo).
- **Start on mock (owner decision, Natig 2026-10-29):** SUPPLIER 1.3.0 is not on `main`. The Procurement fake is written from
  the draft frozen on `origin/feature/mvp2/supplier-dl1-contract-130` @ `cfd917df3` (`x-producer-status FROZEN 2026-10-29`),
  file `supplier.openapi.yaml`, **sha256 `7204e6d4d795218ce1560d6e6a41ee51b709c69830831606c59fc7730bfd7035`**. The file is not
  copied to this branch. It carries every name §0.0 uses: `GET /api/suppliers/{supplierId}/eligibility`, `SupplierEligibility`
  (`supplierId`, `supplierMasterId`, `relationshipExists`, `status`, `onboardingStatus`, …), `UNKNOWN_SUPPLIER`,
  `UNKNOWN_LEGAL_ENTITY`, `LEGAL_ENTITY_LOOKUP_FORBIDDEN`, `DEPENDENCY_UNAVAILABLE`. When 1.3.0 reaches `main`, its hash and
  the `SupplierEligibility` schema are measured again before the PR (plan §5.1).
- Evidence level: E3 — owned replica set, the real host (Program.cs composition, context middleware, MediatR pipeline with the
  central-audit behavior, Mongo transactions) with a fake Procurement handler and a stub MDM validator. Not E4: no Gateway tour,
  no real MOD-0140 endpoint (that live run is the enabling gate, Natig P2). My PASS does not close the work; the CT verifies.

## Commits

| Step | Commit | Content |
|---|---|---|
| 1 | `3ac7d16ba` | domain, `supplier-score-policy/1` (exact integer aggregate, one half-even rounding, band without division, calendar month); SCORE/BAND/MISS/CODE/PER unit tests |
| 2 | `82ab30c52` | `supplier_evaluations` + `supplier_performance_receipts`, unique receipt tuple and active-period (partial) indexes, one create transaction; storage tests |
| 3 | `bdead1fe2` | eligibility reader (D2 table), wire models, error codes, permission attribute/keys, `FakeProcurement`; ELIG-01 reader tests |
| 4 | `da74fea0f` | context middleware (§0.5 stages 1–3b, 4 headers/path/query), create handler (stages 5–8), four reads, central audit (18 commands / 12 transactions), ledger note, `KnownWithoutProvider` |
| 5 | `d4b5ad7a5` | composition in `Program.cs` (D2), Development base URL (Gateway 5000), composition test |
| 5a | `8451f63f7` | double-rounding and untrimmed-key test cases; no `docs/analysis` path in code (DocsPathGuard) |
| 5b | `dd4b0975f` | deterministic RP-07 (late identical request is a replay); one database per test host |
| 5c | `cad08fd3e` | own database only for the unique-index race tests. RP07 was red at this commit |
| 4b | `113c6e628` | fix: the receipt re-read after a clash reads locally; RP-07 runs four rounds |
| 5d | this note | evidence |

## Full suite (unfiltered)

| HEAD | Lane | mongod | Result | Run folder |
|---|---|---|---|---|
| `113c6e628` | 6154, fresh replica set `rs-q502s`, shut down afterwards | 8.0.18 | **880 passed, 0 failed, 1 skipped** (`ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, by design), 3 m 42 s, exit 0 | `~/Projects/ERP-vNext-lanes/mod0147v1/run-20261030-140432` |
| `dd4b0975f` | 6154 | 8.0.18 | 833 passed, **47 failed**, 1 skipped: mongod down, see the sabotage notes | `run-20261029-223814` |
| `d4b5ad7a5` | 6154, fresh replica set `rs-q502s`, shut down afterwards | 8.0.18 | 878 passed, 0 failed, 1 skipped (`ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, by design), 12 m 40 s, exit 0 | `~/Projects/ERP-vNext-lanes/mod0147v1/run-20261029-212714` |

`git status` was empty when each run started. `ContractErrorCodeParityTests` (shipment-bundle) and the central-audit guards are
inside the suite and unchanged in meaning. Other gates: phase1 `python3 -m unittest discover -s scripts/tests` — **220 tests OK**.
The phase1 run was on `d4b5ad7a5`, not re-run on later commits (they touch only SupplyChain C#). Architecture filter
(`AuditTrailStandard`, `DocsPathGuard`) on `113c6e628`: 3 red. `AuditTrailStandardTests` (2) lists only `Diten.CrmService` commands and
`Diten.Platform` duplicate names; `DocsPathGuardTests` (1) lists no SupplierPerformance file. The CRM file checked and the listed
files are outside this branch's diff, but I did not run the tests on `63acdf2ec` itself. Reported, not fixed (XMC-001).

## Sabotage table (HEAD `113c6e628`, lane 6154, mongod 8.0.18)

Each row changes one production (or guard) line, runs the guarding tests, writes the original bytes back and checks
`git diff --quiet` on the file. **32 of 33 rows went red on every run and were restored clean; RP-07c is a race and went red in
6 of 8 tries.** The restored code is the committed code, and the full suite ran green on it after the table. There is no per-row
green run after restore. Harness `~/Projects/ERP-vNext-lanes/mod0147v1/sabotage.py` (sha256 `d6c13c4cd8730604…`), raw results
`sabotage-113c6e628.json`, `sabotage-rp07c-try1…8.json`.

| Row | Rule | Sabotage | File | Sabotaged run | Restore |
|---|---|---|---|---|---|
| SCORE | T-0147-V1-SCORE-01: round once | round to three places, then half-even to two | `ScorePolicy.cs` | red: failed=1 passed=41 | restored |
| BAND | T-0147-V1-BAND-01: band from the unrounded value | band from the rounded overall | `ScorePolicy.cs` | red: failed=2 passed=95 | restored |
| MISS | T-0147-V1-MISS-01: 3 of 4 | accept two metrics | `ScorePolicy.cs` | red: failed=2 passed=51 | restored |
| CODE | T-0147-V1-CODE-01: weight 25 | weight check removed | `ScorePolicy.cs` | red: failed=5 passed=48 | restored |
| PER | T-0147-V1-PER-01: calendar month | month end shifted (at most day 30) | `ScorePolicy.cs` | red: failed=17 passed=80 | restored |
| PERM | T-0147-V1-PERM-01: create opens with .evaluate only | create opened with `.read` | `SupplierPerformanceController.cs` | red: failed=1 passed=0 | restored |
| ELIG-ID | T-0147-V1-ELIG-01: answer must name the requested supplier | answer identity not compared | `SupplierEligibilityReader.cs` | red: failed=2 passed=62 | restored |
| ELIG-SKIP | T-0147-V1-ELIG-01: skipping the read makes the no-relationship case red | eligibility read skipped | `CreateSupplierEvaluationHandler.cs` | red: failed=11 passed=0 | restored |
| ELIG-403 | T-0147-V1-ELIG-01: Procurement 403 is never hidden as 503 | Procurement 403 falls to 503 | `SupplierEligibilityReader.cs` | red: failed=4 passed=60 | restored |
| CANON | §0.0 D2 P1: legacy code → 422, no Procurement call | canonical-code check removed | `CreateSupplierEvaluationHandler.cs` | red: failed=4 passed=0 | restored |
| MDM | §0.5 3b: MDM refusal before Procurement | MDM Unavailable lets the request through | `SupplierPerformanceContextMiddleware.cs` | red: failed=1 passed=1 | restored |
| ISO | T-0147-ISO-01: Tenant + LE filter | LE filter removed from the read scope | `EvaluationRepository.cs` | red: failed=2 passed=0 | restored |
| RP-01 | T-0147-RP-01: receipt inside the commit | receipt not written in the transaction | `EvaluationRepository.cs` | red: failed=7 passed=8 | restored |
| RP-02 | T-0147-RP-02: fingerprint covers the whole body | fingerprint on supplierId only | `SupplierPerformanceRequestFingerprint.cs` | red: failed=1 passed=0 | restored |
| RP-03 | T-0147-RP-03: receipt before the eligibility read | receipt looked up after the eligibility read | `CreateSupplierEvaluationHandler.cs` | red: failed=1 passed=0 | restored |
| RP-04 | T-0147-RP-04: permission before the receipt | permission skipped when a key is sent | `SupplierPerformanceContextMiddleware.cs` | red: failed=1 passed=0 | restored |
| RP-05 | T-0147-RP-05: LegalEntityId in the tuple | LegalEntityId dropped from the tuple | `EvaluationRepository.cs` | red: failed=1 passed=0 | restored |
| RP-06 | T-0147-RP-06: no receipt for a failed request | a receipt written for a 422 | `CreateSupplierEvaluationHandler.cs` | red: failed=1 passed=0 | restored |
| RP-07a | T-0147-RP-07: a period clash re-reads the receipt | period clash answered 409 without re-reading the receipt | `CreateSupplierEvaluationHandler.cs` | red: failed=2 passed=0 | restored |
| RP-07b | T-0147-RP-07 / RP-10: receipt unique index | receipt unique index dropped | `SupplierPerformanceSchema.cs` | red: failed=2 passed=2 | restored |
| RP-07c | T-0147-RP-07: the clash re-read sees the winner's commit | clash re-read at majority instead of local | `CreateSupplierEvaluationHandler.cs` | **race: red in 6 of 8 tries** (failed=1 passed=14); green in the table run with the narrower filter | restored |
| RP-08 | T-0147-RP-08: unknown commit is not rolled back and rewritten | unknown commit retried as a new write | `EvaluationRepository.cs` | red: failed=4 passed=0 | restored |
| RP-11 | T-0147-RP-11: active-period unique index | active-period unique index dropped | `SupplierPerformanceSchema.cs` | red: failed=2 passed=0 | restored |
| RP-12 | T-0147-RP-12: the key is not trimmed | key trimmed | `SupplierPerformanceContextMiddleware.cs` | red: failed=1 passed=6 | restored |
| RP-12ws | T-0147-RP-12: White_Space-only key → 400 | White_Space-only key accepted | `SupplierPerformanceContextMiddleware.cs` | red: failed=4 passed=3 | restored |
| RP-13 | T-0147-RP-13: current correlation in the header | stored correlation copied into the header | `SupplierPerformanceController.cs` | red: failed=1 passed=0 | restored |
| RP-14 | T-0147-RP-14: replay answers the stored snapshot | replay answers the current status | `CreateSupplierEvaluationHandler.cs` | red: failed=1 passed=0 | restored |
| RP-15 | T-0147-RP-15: "" is not null | `""` normalised to null | `SupplierPerformanceRequestFingerprint.cs` | red: failed=1 passed=0 | restored |
| RP-16 | T-0147-RP-16: schema before the receipt | body schema not checked before the receipt | `CreateSupplierEvaluationValidator.cs` | red: failed=1 passed=0 | restored |
| AUD-mark | AUD-001: command is marked (path b) | `ICentralAuditDeclaration` removed | `CreateSupplierEvaluationCommand.cs` | red: failed=2 passed=10 | restored |
| AUD-enlist | AUD-001: record in the command transaction | `EnlistAsync` removed from the transaction | `EvaluationRepository.cs` | red: failed=2 passed=10 | restored |
| KWP | §0.0 F-1: KnownWithoutProvider entry | KnownWithoutProvider entry removed | `EnforcedPermissionDeclarationGuardTests.cs` | red: failed=1 passed=3 | restored |
| COMP | D2 composition: production wires the real reader | typed eligibility client not registered | `Program.cs` | red: failed=7 passed=0 | restored |

Notes:
- **RP-07c is not a deterministic guard.** It needs a commit visible locally before the majority snapshot shows it. The bug it
  guards was found by RP-07 itself: 409 `DUPLICATE_EVALUATION` among identical concurrent creates. Fixed in `113c6e628`.
- First harness run: RP-07a stayed green because the barrier race missed the window between stage 5 and the active-period
  check. `dd4b0975f` adds a gate that holds one request there, and the row is red on every run since.
- RP-07b and RP-11 were first red only in the storage tests: the host tests shared a database whose earlier unique indexes
  survived the sabotage. RP-07, RP-10 and RP-11 now have their own database and also go red at the host.
- `dd4b0975f` gave every host its own database. That exhausted mongod's open files ("Too many open files"): mongod went down and
  47 tests of other modules failed (`run-20261029-223814`). `cad08fd3e` limits own databases to those race tests.
- RP-12 has two rows: trimming (pack sabotage) and accepting a White_Space-only key.
- RP-14's sabotage changes the replayed status to the current one instead of reading the entity. RP-16's makes the validator
  accept any object, so the test goes red on the missing schema check rather than through an early receipt lookup. RP-06's
  writes an evaluation and a receipt for the 422.
- RP-09 (submit) is DL-2.

## Rule → test map

| Rule | Tests |
|---|---|
| T-0147-V1-SCORE-01 / BAND-01 | `ScorePolicyTests.Score_WhenV1InputsAreValid…`, `Overall_And_Band…`, `SupplierPerformanceHttpTests.Create_WhenTheRequestIsValid…`, `Create_WhenAMetricIsAbsent…` |
| T-0147-V1-MISS-01 / CODE-01 / PER-01 | `ScorePolicyTests`, `SupplierPerformanceHttpTests.Create_WhenAV1PolicyRuleIsBroken…` (11 cases, nothing written) |
| T-0147-V1-PERM-01 | `SupplierPerformanceHttpTests.Permissions_ShouldMapCreateToEvaluateAndTheFourReadsToRead` |
| T-0147-V1-ELIG-01 | `SupplierEligibilityReaderTests` (D2 table at the reader), `SupplierPerformanceHttpTests.Create_WhenProcurementAnswers…` (11 cases), `…NotActiveOrNotApproved…`, `…ALegacyCode…` (422, no call), `…MasterUuidAndTheCode…` (canonical stored, 409), `Create_WhenMdmRefusesTheCompany…` (503 `DEPENDENCY_UNAVAILABLE`, no Procurement call) |
| T-0147-ISO-01 | `SupplierPerformanceHttpTests.Reads_WhenTheTenantOrCompanyIsForeign…`, `EvaluationStorageTests.Reads_…` |
| T-0147-RP-01…08, 10…16 | `SupplierPerformanceReplayTests` (+ storage halves in `EvaluationStorageTests`) |
| §0.5 precedence 1–4 | `SupplierPerformanceHttpTests.Gate_ShouldApplyTheStagesInOrder`, `Create_WhenTheBodyIsNotTheContractSchema…` |
| AUD-001 (path b) | `CentralAuditDeclarationTests` (18 / 12), `SupplierPerformanceHttpTests.Create_ShouldWriteOneCentralAuditRecord…` (actor, tenant, company, entity, operation; none for a refusal) |
| Error codes | `SupplierPerformanceErrorCodeTests` (every emitted code is in the contract or the pack, except the house 401/500 codes) |
| Composition / F-1 | `SupplierPerformanceCompositionTests`, `EnforcedPermissionDeclarationGuardTests` (`KnownWithoutProvider`) |

## Open points

1. **1.3.0 on main** (plan §5.1): re-measure the file hash and `SupplierEligibility` against `7204e6d4…` before the PR.
2. **Canonical code pattern:** 1.3.0 names the canonical code (`Supplier.SupplierId`) without a pattern. DL-1 accepts
   `SUP-[A-Z0-9]+` (Procurement mints `SUP-` + 8 upper-case hex; the contract examples are `SUP-2001`) or a D-format uuid. If
   MOD-0140 fixes a pattern, `SupplierPerformanceWire.CanonicalCode` follows it.
3. **ModuleCode `supplier-performance-risk`** is taken from the pack's file and branch name; the enabling change's manifest
   provider must use the same code (the `KnownWithoutProvider` entry and `CentralAuditDeclarationTests` tie them together).
4. **401 / 500 codes:** the pack declares the statuses without codes; DL-1 emits the house `UNAUTHENTICATED` / `INTERNAL_ERROR`.
   The contract successor 1.1.0 should name them (CT contract writer).
5. **Test Mongo variable:** the DL-1 tests use the suite's existing `MOD0183_TEST_MONGO` (set by `supplychain-ci-mongo.sh`) with
   their own databases, so no CI script changed. A separate variable would need a CI-script change (not in this slice).
6. **Unknown commit and the central record:** when the commit succeeded but the answer was lost (RP-08 `afterCommit`), the
   command reports 503, so the shared forwarder marks the enlisted row Abandoned; the evaluation exists without a delivered
   central record. Shared-forwarder behavior (BuildingBlocks), not changed here; CT to decide.
7. **RP-07c** is a race (6 of 8); see the sabotage notes.
8. Plan §5.2 (five low findings of the pack review, due 2026-10-30) and §5.3 (`mdm.legal-entities.read` and
   `procurement.suppliers.eligibility.read` on the evaluating role for the tour) stay open; neither changes DL-1 code.
9. `AuditTrailStandardTests` red on `main` for CRM / Platform (above) — another module (XMC-001), not touched.
