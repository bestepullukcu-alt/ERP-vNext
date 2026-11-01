# MOD-0147 DL-2 — evidence (Q505, steps 0–6)

- Branch `feature/sce/mod0147-dl2-01`, started from `e881907eb` (PR #320, DL-1). #320 is now on `main`; the CT merged `main` into this branch (`dd72aac9e`). Local commits only; nothing pushed, no PR.
- **This version answers review 01** (`REVIEW-01-5db56929.md`, outside the repo). Every figure below the tables marked "final" is on the merged tree.
- Pack `MOD-0147-supplier-performance-risk.md`, sha256 `f1c951bc4d68bd617326bed9f7c2e835393765424832fe46ee709cc12216aa03`, measured before the first commit.
  Contract `supplier-performance.openapi.yaml` sha256 `e311cd10…`, unchanged. Plan: `MOD-0147-DL2-IMPLEMENTATION-PLAN-r1.md` (outside the repo), approved by the CT (Q505-P2): K-1, K-2, D-1…D-7.
- Evidence level: E3 — owned replica set, the real host with a fake Procurement and a stub MDM. Not E4: no Gateway tour, no transport, no dev environment. My PASS does not close the work; the CT verifies.

## Commits

| Step | Commit | Content |
|---|---|---|
| 1 | `86bf40c52` | one `CommitAsync` for create and submit; receipt `CommitId`; no behaviour change (200 feature tests green) |
| 2–5 | `8715dbc71` | domain fields, `.publish`, `VERSION_CONFLICT`, submit schema and fingerprint, `SubmitAsync`, outbox collection, handler, controller, `If-Match`, central audit, outbox store and worker, tests. Planned as four commits; they were written and proven together, so they are one |
| 5a | `3674e7023` | storage half of submit: scope, version, indexes |
| 5b | `a42a37564` | stage 5 before stage 6; a real rival for `CommitId` (two sabotage rows had stayed green) |
| 6 | `5db56929a` | evidence, first version |
| — | `dd72aac9e` | CT: `main` merged in, review F-1 |
| R1 | `8727a4a13` | review 01: F-2 barrier at stage 8 and Status test, F-3 real outbox row, F-5 company in the tuple, ledger count |
| R2 | this version | evidence for `8727a4a13` |

## Full suite (unfiltered) — final is the first row

| HEAD | Lane | mongod | Result | Run folder |
|---|---|---|---|---|
| `8727a4a13` (merged with `main`) | 6156, fresh replica set `rs-q505s`, shut down afterwards | 8.0.18 | **1004 passed, 0 failed, 1 skipped** (by design), 4 m 3 s, exit 0, no disk refusal | `~/Projects/ERP-vNext-lanes/mod0147v1/run-20261031-024935` |
| `a42a37564` | 6156, fresh replica set `rs-q505s`, shut down afterwards | 8.0.18 | **959 passed, 0 failed, 1 skipped** (`ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, by design), 4 m 40 s, exit 0, no disk refusal | `~/Projects/ERP-vNext-lanes/mod0147v1/run-20261031-020719` |
| `3674e7023` | 6156 | 8.0.18 | 958 passed, 0 failed, 1 skipped | `run-20261031-013713` |
| `3674e7023` | 6156 | 8.0.18 | **invalid**, 52 failed: free disk fell under mongod's 500 MB minimum; 52 refusal lines in the log, not matched test by test | `run-20261031-013131` |
| `3674e7023` | 6156 | 8.0.18 | **invalid**, 430 failed: the same disk refusal, cause read from the first failures | `run-20261031-012909` |

Port 6156 is a copy of the 6154 script (CT note): 6154 was in use by other lanes. `ContractErrorCodeParityTests` and the central-audit guards are inside the suite.
`CentralAuditDeclarationTests`: 19 commands, 13 transactions after the merge (`main` brought the Return late writer; DL-2 adds no transaction, shared `CommitAsync`). The ledger paragraph now says the same. CI uses mongod 7.0.28 (recorded), these runs 8.0.18.

- Phase1 `python3 -m unittest discover -s scripts/tests`: **220 tests OK** on `8727a4a13`, raw `round-8727a4a13/phase1.txt`.
- Ledger: `git diff e881907eb..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '` is empty.
- Architecture filter (`AuditTrailStandard`, `DocsPathGuard`, `MongoTestDatabaseGuard`) on `8727a4a13`: 64 passed, 3 failed, raw `round-8727a4a13/arch-head.txt`. The same three tests failed on DL-1's HEAD and base (DL-1 evidence). The only `supplier` line in the output is the contract file's own text; `SubmitSupplierEvaluationCommand` is not listed. The base was not measured again here: its worktree no longer exists.

## Sabotage table (lane 6156, mongod 8.0.18)

Each row changes one production (or guard) line, runs the guarding tests, writes the original bytes back and checks `git diff --quiet`. **Final: 76 of 77 rows red on `8727a4a13`, all 77 restored clean, no build error.** Harness `~/Projects/ERP-vNext-lanes/mod0147v1/sabotage-dl2.py` (sha256 `7274912165544acd…`), raw `round-8727a4a13/sabotage.json`, `sabotage.log`. The whole table ran on this one HEAD: 39 DL-1 rows and 38 DL-2 rows.

- **The one green row, `D2-UQ`:** with the receipt index non-unique, `Race_SameKey…` still passes. Inferred reason: the tuple holds the evaluation id, so two submits of one key always hit the same document, and the version condition lets one through; the loser never reaches the receipt insert. So the index does not guard submit today. It does guard create (`RP-07b` red). It is not a test gap to close: no submit race can reach it. The row stays in the table as measured.
- **The first pass of `3674e7023` was 66 of 68:** `D2-RP09` and `D2-CID` stayed green, both test gaps, closed by `a42a37564`.
- **Review F-2:** the three race tests hold every request at stage 8 (`HoldAtStage8`, probe point `submit`), so all have passed stages 5 and 6 and the overlap is certain. `D2-RACE-V` no longer has the source-text test in its filter. `D2-VCOND` and `D2-STATUS` measure each condition alone. `IMM01_TheRepository…` stays, as a guard of the surface, outside those rows.
- **Review F-3:** `TXN03_TheRowSubmitWrites…` runs a real submit on the real host, then the real store and the shared publisher with a test transport (`D2-NEXT`, `D2-CAUSE`).
- **Review F-5:** the five missing rows ran: `D2-FP-IM`, `D2-UQ`, `D2-ENL`, `D2-LE`, `D2-CODE`. `D2-LE` needed a new assertion: the company's own key must not reach another company's receipt.

| Row | Rule | Sabotage | File | Red |
|---|---|---|---|---|
| D2-RP09 | RP-09: stage 5 before stage 6 | stage 5 lookup removed | handler | 1 of 1 |
| D2-RCPT | RP-09: submit writes its receipt | no receipt for status 200 | repository | 1 of 1 |
| D2-INST | §0.5: `submittedAt` as an instant | fingerprint keeps the offset | fingerprint | 1 of 1 |
| D2-CMT | §0.5: `""` is not null | `""` normalised to null | fingerprint | 1 of 1 |
| D2-PERM | V1-PERM-01: `.publish` | opened with `.evaluate` | controller | 1 of 1 |
| D2-ST4 | §0.5 stage 4 | validator accepts any `submittedAt` text | validator | 8 of 12 |
| D2-SURR | D-6: lone surrogate → 400 | `GetString` check removed | models | 2 of 13 |
| D2-LEN | `comment` max 1000 | limit 1001 | models | 1 of 1 |
| D2-VER | stage 6: stale `If-Match` → 409 | version check removed | handler | 1 of 1 |
| D2-LIFE | stage 6: not a draft → 422 | lifecycle check removed | handler | 1 of 1 |
| D2-LATE | stage 6: late identical request replays | late re-read removed | handler | 2 of 2 |
| D2-IFM | K-2: syntax → 400 | syntax refusal removed | middleware | 13 of 13 |
| D2-TXN-OB | TXN-01: outbox in the transaction | written outside the session | repository | 3 of 4 |
| D2-TXN-ST | TXN-01: state in the transaction | written outside the session | repository | 4 of 4 |
| D2-RACE-5 | F-2: lost race re-runs stage 5 | re-read removed | handler | 2 of 3 |
| D2-RACE-V | F-2: version and status guard the update | both conditions removed | repository | 3 of 3 |
| D2-VCOND | F-2: the Version condition alone | removed | repository | 1 of 1 |
| D2-STATUS | F-2: the Status condition alone | removed | repository | 1 of 1 |
| D2-NEXT | F-3: the real row is claimable | `NextAttemptAt` not written | repository | 1 of 1 |
| D2-CAUSE | F-3: the real row's metadata | wrong `CausationId` | repository | 1 of 1 |
| D2-FP-IM | §0.5: `If-Match` not in the fingerprint | added to it | handler | 1 of 1 |
| D2-UQ | §0.5: receipt unique index, submit race | index not unique | schema | **green**, 0 of 2 |
| D2-ENL | AUD-001: submit enlists | `EnlistAsync` removed | repository | 1 of 1 |
| D2-LE | ISO-01: company in the tuple | removed from the tuple | repository | 1 of 1 |
| D2-CODE | error codes | `VERSION_CONFLICT` renamed | error codes | 2 of 2 |
| D2-CID | RP-08: per-request `CommitId` | evaluation id used as `CommitId` | repository | 1 of 4 |
| D2-CID2 | RP-08: only the own receipt proves | any receipt counts | repository | 1 of 1 |
| D2-IMM-RE | IMM-01: not recomputed | scorecard recomputes on another revision | scorecard | 1 of 3 |
| D2-IMM-SF | IMM-01: no other write | a delete method added | repository | 1 of 1 |
| D2-CARD | §16: scorecard id set | not set | repository | 1 of 1 |
| D2-ISO | ISO-01: scope in the update | scope removed from the filter | repository | 1 of 1 |
| D2-EVT | §0.3: contract envelope | `riskLevel` dropped | models | 1 of 1 |
| D2-TXN02 | TXN-02: retry keeps the EventId | new id on a retry | outbox store | 1 of 1 |
| D2-TXN03 | TXN-03: stale row claimed again | stale branch removed | outbox store | 1 of 3 |
| D2-DEAD | §0.3: attempts end | never dead-letters | outbox store | 1 of 1 |
| D2-KEY | K-1: Shipment keeps its store | also registered unkeyed | registration | 1 of 1 |
| D2-AUD-M | AUD-001: path b | marker removed | command | 2 of 12 |
| D2-COR | D-2: publication's correlation | create's correlation on the scorecard | scorecard | 1 of 1 |

## A bug the plan did not name

`Stage6_WhenTheWinnersReceiptAppearsLate…`: a request whose stage 5 missed the winner's receipt could see the published evaluation at stage 6 and answer 409 or 422 to an identical request. Stage 6 now re-reads the receipt before any refusal (row D2-LATE). This is the submit form of the RP-07 bug.

## Rule → test map

| Rule | Tests |
|---|---|
| RP-09, fingerprint | `SubmitSupplierEvaluationTests.RP09…`, `Replay_…` |
| §0.5 stages 2–6 | `Permissions_…`, `Submit_WhenTheBodyIsNotTheContractSchema…`, `Stage3_…`, `Stage5_…`, `Stage6_…` |
| TXN-01, RP-08 | `TXN01_…`, `RP08_…` |
| F-2 races | `Race_…` |
| IMM-01 | `IMM01_…` |
| ISO-01 | `Stage6_ShouldHide…`, `Stage5_ShouldScope…`, `EvaluationStorageTests.Submit_ShouldNotTouchAnotherCompanysEvaluation` |
| §0.3 outbox | `Submit_ShouldWritePendingEvent…`, `SupplierPerformanceOutboxTests` (TXN-02, TXN-03, dead letter, composition) |
| AUD-001 path b | `Submit_ShouldWriteOneCentralAuditRecord…`, `CentralAuditDeclarationTests` |

## Open points

1. **TXN-02:** v1 has no production consumer. Production code proves "a retry keeps the EventId"; "the consumer ignores it" is a test double (K-1).
2. **No transport:** nothing is delivered in any environment; rows stay Pending until the integration composes `IEventTransportPublisher`. That transport would also start Shipment's worker.
3. **§21 Before/After and the event name are not in the central record (review F-4, a Platform / BuildingBlocks request).** The pack asks Before/After `Status` and AfterState `ScorecardId`, `OverallScore`; `CentralAuditOutboxRecord` has no such field, and `supplier-evaluation.published` is carried only as `LifecycleTransition` + `RequestType`. So the central log cannot say when and with which score an evaluation was published; the business record can. AUD-001 §3 row 10 is not met. Needs an owner record or a dated work item; no code here.
4. **Refused command:** the shared forwarder keeps no record of a refusal; `testing-agent` asks for a `Failed` record. Tests measure "no record", as DL-1 does.
5. **Lost commit with a failed read-back:** the enlisted row goes Abandoned and the replay enlists nothing (DL-1 point 6, BuildingBlocks).
6. **AUD-001 §8-(3)** dev-environment proof is not done; v1 has no delete command.
7. **D-3:** more than seven fractional digits of `submittedAt` answer 400, narrower than RFC 3339. Offsets beyond .NET's ±14:00 also answer 400 (seen in code, no test).
8. **Stage 6 read:** `GetAsync` does not map a Mongo failure to 503, so it answers 500 (DL-1 F-7 family).
9. **DL-1 F-11:** the scorecard index is added; `ListPublishedAsync` stays unbounded, and now returns real rows. The contract has no paging.
10. **Pack text:** review F-2 is followed by the code but is not in the pack; the pack's five low findings passed their 2026-10-30 date.
11. **Scope beyond `Features/SupplierPerformance/**`:** `CentralAuditDeclarationTests.cs` (count) and the ledger paragraph only; `Program.cs` is unchanged. `SupplierPerformanceHost`'s `Snapshot` now covers the outbox.
12. **Merge:** done by the CT (`dd72aac9e`); the suite ran on the merged tree.
13. **Review F-6:** the late replay and the lost-answer read use `Local`. A rival's receipt seen before majority could answer 200 and be rolled back if the primary falls in that window. Very narrow, from DL-1's pattern, not measured.
14. **Review F-7:** a stale `Publishing` claim does not count an attempt, so a row that crashes the process is claimed every 5 minutes and never dead-letters. Same as Shipment; no effect without a transport.
15. **Review F-9:** `If-Match` accepts leading zeros (`007` = 7). Not a K-2 violation, no test, no harm: the version is still compared.
16. **`D2-UQ`:** see the sabotage section.
17. **Not measured:** a real primary change under these races and the lost-commit path; E4.
