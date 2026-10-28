# MOD-0183 DL-1 — evidence (Q500, steps 1–5)

- Branch `feature/sce/mod0183-dl1-01`, base `6be3d0ae1`. Local commits only; nothing pushed.
- Pack `MOD-0183-shipment-tracking-pod.md` sha256 `07152235c49640c6…` (r6 §23). Plan: `DL1-IMPLEMENTATION-PLAN.md` r3.
- Evidence level: E3. That means an owned replica set, plus the real host, repository and INVENTORY client, talking to a
  fake INVENTORY handler. It is not E4: there was no Gateway tour and no real INVENTORY.
- My PASS does not close the work (K13); the CT verifies.

## Commits

| Step | Commit | Content |
|---|---|---|
| 1 | `d34f6656c` | SHIPMENT-BUNDLE 3.4.0 by `git apply` of the patch, 7 error codes (59 in total), return reader accepts the 3.4.0 line fields |
| 2 | `edff5723c` | dispatch attempt, line stock fields, conditional storage, `stockIssued` / `dispatchState` |
| 3 | `e2362aae4` | INVENTORY movement client, issue/REVERSAL mapper |
| 4 | `2cdeb44f9` | coordinator (A/B/B′/C/D), compensation, exit, late writer, routing |
| 4b | `cafdfead9` | review fixes F-1, F-2, F-3, F-5…F-8 (`MOD-0183-DL1-CODE-REVIEW-STEPS1-4.md`) |
| 4c | `0420296a1` | central-audit guard counts the outcome transaction (9 → 10) |
| 5 | `1bda485ee` | evidence note, first version |
| 4d | `463d054b3` | re-review fixes N-1, N-2, R-1…R-4 (`MOD-0183-DL1-CODE-REVIEW-FIXES.md`); guard count 10 → 11 |
| 5′ | this note | evidence, updated |

## Full suite (unfiltered)

| HEAD | Lane | mongod | Result | Run folder |
|---|---|---|---|---|
| `463d054b3` | 6152, fresh replica set `rs-q500s`, shut down afterwards | 8.0.18 | **677 passed, 0 failed, 1 skipped** (`ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, skipped by design), 3 m 21 s, exit 0 | `~/Projects/ERP-vNext-lanes/q500-dl1/run-20261028-190852` |
| `0420296a1` (earlier) | 6152 | 8.0.18 | 672 passed, 0 failed, 1 skipped | `run-20261028-182912` |

`git status` was empty when the run started. `ContractErrorCodeParityTests` and the central-audit tests run inside the
full suite; they were also run on their own (71/71 with the dispatch tests) on `463d054b3`.

## Sabotage table (HEAD `463d054b3`, lane port 6151, mongod 8.0.18)

Each row breaks the production code once. The guarding test run went red, the file was restored byte for byte (sha256
compared), and the same tests then ran green. **41 of 41 rows went red, were restored and then went green**, and `git
status` was clean afterwards. Every earlier row was re-run on this HEAD. The harness is
`~/Projects/ERP-vNext-lanes/q500-dl1/tools/sabotage3.py` and the raw table is `sabotage-463d054b3.tsv`.

| Rule | Sabotage | File | Sabotaged run | Restored | Run after restore |
|---|---|---|---|---|---|
| S23-1 | drop lotId from the issue mapper | `InventoryMovementMapper.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| S23-2 | refusal treated as posted (no compensation, Dispatched) | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-3 | no receipt replay before the attempt | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-4a | INVENTORY 403 mapped to unavailable (503) | `InventoryMovementClient.cs` | Failed failed=4 passed=11 | restored | Passed failed=0 passed=15 |
| S23-4b | X-Legal-Entity-Id not sent | `InventoryMovementClient.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-5 | skuLevel check skipped | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-6 | legal-entity filter removed from the repository scope | `ShipmentRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-8 | compensation skipped (no REVERSAL) | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-9 | resume re-dates the attempt and rebuilds sent bodies | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-10 | failed REVERSAL treated as reversed (attempt Compensated) | `ShipmentDispatchCoordinator.cs` | Failed failed=3 passed=0 | restored | Passed failed=0 passed=3 |
| S23-11 | IDEMPOTENCY_KEY_REUSED handled as a refusal | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-12 | INVENTORY client injected into ShipmentOutboxWorker | `ShipmentOutboxWorker.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-13 | new REVERSAL key per retry | `DispatchAttempt.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-14 | attempt number never increases (a1 keys reused) | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-15 | lease expiry not checked on resume | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-17 | lotId added to the REVERSAL body | `InventoryMovementMapper.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| S23-18i | late writer reverses in case (a) | `LateWriterPolicy.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-18ii | lease-id condition dropped from attempt writes | `ShipmentRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-19 | ALREADY_REVERSED treated as a failure | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| S23-20 | 403 on a REVERSAL answered as 403 | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=2 | restored | Passed failed=0 passed=3 |
| reader | strict line-property check restored | `ReturnReferenceReader.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| parity | INVENTORY_REFUSED constant drifts from the contract | `ContractErrorCodes.cs` | Failed failed=2 passed=1 | restored | Passed failed=0 passed=3 |
| F-1 | 502/500/504 treated as definite (marker cleared, attempt dropped) | `InventoryMovementClient.cs` | Failed failed=6 passed=9 | restored | Passed failed=0 passed=15 |
| F-2a | takeover filter without the attempt revision | `ShipmentRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| F-2b | takeover replaces the whole attempt from its copy | `ShipmentRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| F-3 | close written outside the outcome transaction | `ShipmentRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| F-5a | late writer REVERSAL dated now, not the stored reversal date | `LateWriterPolicy.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| F-5b | exit ignores the late writer's recorded reversal | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| F-6 | 403 on a resend keeps the lease | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| F-7 | lost begin always answered 409 | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| audit | outcome transaction without central-audit enlistment | `ShipmentRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| F-8 | X-Tenant-Id sent to INVENTORY again | `InventoryMovementClient.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| N-1a | resume of a compensating attempt posts and completes (RunAsync) | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| N-1b | completion does not check for a compensating attempt | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| N-1c | refusal answer not stored with the compensation | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| N-2 | KEY_REUSED on the B′ resend skipped (attempt closed Compensated) | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R-1 | a 503 without INVENTORY's envelope counted as definite | `InventoryMovementClient.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R-2 | block without a stored reversal date | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R-3 | lost begin to CompensationFailed answered 409 DISPATCH_IN_PROGRESS | `ShipmentDispatchCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R-4 | late write recorded outside its transaction | `ShipmentRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| audit2 | late-writer transaction without central-audit enlistment | `ShipmentRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |

Notes:
- S23-20 and parity turn only their own case red. S23-20 fails only the 403 case of its theory, and parity fails only
  the drifted code.
- In the first harness run (on `2cdeb44f9`), S23-15 hung instead of going red. The test now lets only the first post
  wait, and the harness has a 180 s hang timeout.
- N-1 has three rows:
  - N-1a: the resume branch is removed. The resume then posts again, and the completion check stops it with a 409, so
    the test is red.
  - N-1b: the completion check alone is removed. It is reached through a deterministic hook that starts compensation
    just before the D transaction.
  - N-1c: the refusal answer is not stored.

## Review findings — status

| F | Fix | Test | Sabotage |
|---|---|---|---|
| F-1 | 500, 502, 504 and any other 5xx are ambiguous: the line stays sent and the attempt keeps the lease | `F1_…`, `EachInventoryStatusIsItsOwnOutcome` | F-1 |
| F-2 | the takeover is filtered on N, lease id, `leaseUntil`, state and `Rev`, and `$set` touches only the lease fields | `TakeoverLosesToAWrite…` | F-2a, F-2b |
| F-3 | the outcome (close or block, audit, receipt, central audit) is one transaction | `F3_…` | F-3, audit |
| F-4 | **open point**, see below | — | — |
| F-5 | the late writer uses the stored `ReversalDate`; the exit takes the late reversal | `F5_…` | F-5a, F-5b |
| F-6 | a 403 on a resend releases the lease | `F6_…` | F-6 |
| F-7 | a lost begin on a shipment that is no longer Planned answers 422 `INVALID_SHIPMENT_TRANSITION` | `F7_…` | F-7 |
| F-8 | `X-Tenant-Id` is no longer sent | `IssueCarriesEverySection231Field…` | F-8 |
| **N-1** | Compensation is durable before the first REVERSAL: the attempt stores `ReversalDate` and the `Refusal` answer. A resume of a compensating attempt finishes the compensation and answers the stored refusal. D refuses to complete an attempt that has a reversal date, a stored refusal or a reversed line. | `N1_AResumeMidCompensation…` (the reviewer's REV_N1: the stock is freed, the retry gets 422, net 0, no `ShipmentDispatched`), `N1_CompletionRefuses…` | N-1a, N-1b, N-1c |
| **N-2** | KEY_REUSED on a B′ resend blocks the attempt (CompensationFailed, 409), and the exit resends KEY_REUSED lines, so the attempt stays blocked until an operator resolves it (§23.4). The earlier open point 4 (skip) is withdrawn. | `N2_…` (the reviewer's REV_N2: 409, Planned, net −3, no Compensated history) | N-2 |
| R-1 | a 503 without INVENTORY's error envelope (the Gateway's own timeout) is ambiguous | `TimeoutAndConnectionLoss…` | R-1 |
| R-2 | a blocked attempt always stores `ReversalDate` | `S23_11_…` | R-2 |
| R-3 | a begin lost to a CompensationFailed attempt answers 409 `DISPATCH_COMPENSATION_FAILED` | `R3_…` (hook) | R-3 |
| R-4 | the late writer's record and its audit row are one transaction, which also enlists central audit (Abandoned, the command failed) | `R4_…`, storage test | R-4, audit2 |
| R-5 | pack wording only, no code; see open point 2 | — | — |

## Open points

1. **F-4 / AUD-001: needs a decision from the owner.** The central-audit behaviour (BuildingBlocks, BL-501) records only
   successful commands, and `CentralAuditMetadata` has no details field. Platform's `audit/append` contract is involved
   too. Reversal ids, `ALREADY_REVERSED` with the original transactionId, and the failed outcomes cannot reach the
   central record without changes in BuildingBlocks and Platform (XMC-001). For now they are in the in-product
   `sce_shipment_audit` rows, written in the same transaction as the outcome (F-3, R-4). Question for MVP-1 CT or the
   AUD-001 owner: should §23.4 be met by a BuildingBlocks/Platform change, or by this interim plus one line in the pack
   for v1?
2. **R-5 pack wording, one line for MVP-1 CT.** §23.3 and S23-4 should read: "before the first post that INVENTORY may
   have received nothing changes; an ambiguous answer (a 5xx other than INVENTORY's own 503, a timeout) keeps the
   attempt `InProgress` under its lease". §23.4's late-writer rule should read: "reverses its **unrecorded** issue". The
   code already does both.
3. **N-2 consequence:** a KEY_REUSED line keeps the shipment blocked, and every exit answers 409, until an operator
   resolves it. §23.4 accepts that; making it automatic would need a pack change.
4. **R-1:** the Gateway's 503 envelope behaviour (Ocelot 24.1.0) comes from the library, not a measurement; it should be
   confirmed on the Gateway tour.
5. **mongod 8.0.18** (Homebrew) was used instead of the CI-pinned 7.0.28: no pinned tarball was found on this Mac.
6. **Not run:** the Gateway tour (port 5000), the phase1 Python tests (`scripts/tests`) and a real INVENTORY. Before a
   tour, `inventory.movements.create` must be granted per role (BL-652 interim).
7. **Out of scope, as briefed:** S23-16, the DL-2 texts and resx, and `reservationId`. MOD-0186 still has to emit the
   RETURN_* codes. Plan open point 3 (the pack header text) is unchanged.
