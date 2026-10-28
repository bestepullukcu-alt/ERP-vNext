# MOD-0183 DL-1 — evidence (Q500, steps 1–5)

- Branch `feature/sce/mod0183-dl1-01`, base `6be3d0ae1`. Local commits only; nothing pushed.
- Pack `MOD-0183-shipment-tracking-pod.md` sha256 `07152235c49640c6…` (r6 §23). Plan: `DL1-IMPLEMENTATION-PLAN.md` r3.
- Evidence level: E3 (owned replica set, real host + repository + INVENTORY client over a fake INVENTORY handler). Not E4:
  no Gateway tour and no real INVENTORY.
- My PASS does not close the work (K13); the CT verifies.

## Commits

| Step | Commit | Content |
|---|---|---|
| 1 | `d34f6656c` | SHIPMENT-BUNDLE 3.4.0 by `git apply` of the patch, 7 error codes (59 in total), return reader accepts the 3.4.0 line fields |
| 2 | `edff5723c` | dispatch attempt, line stock fields, conditional storage, `stockIssued` / `dispatchState` |
| 3 | `e2362aae4` | INVENTORY movement client, issue/REVERSAL mapper |
| 4 | `2cdeb44f9` | coordinator (A/B/B′/C/D), compensation, exit, late writer, routing |
| 4b | `cafdfead9` | code-review fixes F-1, F-2, F-3, F-5, F-6, F-7, F-8 (`MOD-0183-DL1-CODE-REVIEW-STEPS1-4.md`) |
| 4c | `0420296a1` | central-audit guard counts the new outcome transaction (9 → 10) |
| 5 | this note | evidence |

## Full suite (unfiltered)

| HEAD | Lane | mongod | Result | Run folder |
|---|---|---|---|---|
| `0420296a1` | 6152, fresh replica set `rs-q500s`, shut down after | 8.0.18 | **672 passed, 0 failed, 1 skipped** (`ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, skipped by design), 3 m 25 s, exit 0 | `~/Projects/ERP-vNext-lanes/q500-dl1/run-20261028-182912` |
| `cafdfead9` | 6152 | 8.0.18 | 671 passed, **1 failed**: `CentralAuditDeclarationTests.Every_transaction_a_command_can_run_in…` (pinned count 9; F-3 adds a 10th transaction, which enlists) — fixed in `0420296a1` | `run-20261028-182148` |

`ContractErrorCodeParityTests` and the central-audit tests (`FullyQualifiedName~CentralAudit`): 15/15 green on `0420296a1`
(also inside the full suite). Each suite run had `git status` empty at start.

## Sabotage table (HEAD `cafdfead9`, lane port 6151, mongod 8.0.18)

For each row: the production code was broken once, the guarding test run was red, the file was restored byte-exact
(sha256 compared), and the same tests ran green. 32 of 32 rows were red, then restored, then green. `git status` was clean afterwards.
The harness is `~/Projects/ERP-vNext-lanes/q500-dl1/tools/sabotage2.py`; the raw table is `sabotage-cafdfead9.tsv`.

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
| F-8 | X-Tenant-Id sent to INVENTORY again | `InventoryMovementClient.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| audit | outcome transaction without central-audit enlistment | `ShipmentRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |

Rows: 32; every row red → restored → green (`audit` ran on `cafdfead9` + the guard change of `0420296a1`).

Notes:
- The first harness run (on `2cdeb44f9`) hung at S23-15. Under that sabotage the second dispatch took the lease and
  waited on the same gate as the first, which deadlocked. The test now lets only the first post wait, and the harness
  has a 180 s hang timeout. That run's 14 rows were superseded by this table.
- S23-20: the sabotage turns only the 403 case of the theory red (1 failed, 2 passed). That case is exactly what it
  guards.

## Review findings (2cdeb44f9) — what was done

| F | Fix | Test | Sabotage |
|---|---|---|---|
| F-1 ENGEL | 500 / 502 / 504 and any other 5xx are ambiguous. The line stays sent and the attempt keeps the lease; the next holder resends the same key. Only INVENTORY's own 503 and a 401 are definite. | `F1_…` (500/502/504), `EachInventoryStatusIsItsOwnOutcome` | F-1 |
| F-2 | Takeover is filtered on N, lease id, `leaseUntil`, state and a new attempt revision `Rev`. Only the lease fields are `$set`. | `TakeoverLosesToAWriteMadeAfterTheObservationAndChangesOnlyTheLease` | F-2a, F-2b |
| F-3 | Close (Compensated) or CompensationFailed, the in-product audit row, the receipt and the central-audit enlistment are written in one Mongo transaction (`SettleDispatchAttemptAsync`). | `F3_…`, `CompensatedAttemptMovesToHistory…` (a lost lease writes nothing) | F-3 |
| F-4 | **Not done — open point.** See below. | — | — |
| F-5 | The late writer uses the attempt's stored `ReversalDate`. The exit takes a late writer's recorded reversal instead of posting it. | `F5_…` | F-5a, F-5b |
| F-6 | A 403 on a resend keeps the marker and releases the lease. | `F6_…` | F-6 |
| F-7 | When the begin is lost, the shipment is re-read; if it is not Planned, the answer is 422 `INVALID_SHIPMENT_TRANSITION`. | `F7_…` (a deterministic race through a repository hook) | F-7 |
| F-8 | `X-Tenant-Id` is no longer sent to INVENTORY. `postMovement` declares only `X-Legal-Entity-Id`. | `IssueCarriesEverySection231Field…` | F-8 |

## Open points

1. **F-4 / AUD-001.** `CentralAuditBehavior` (BuildingBlocks, BL-501) records only successful commands, and its record
   has no details field. A failed command's enlisted row stays `Abandoned` by design. So the outcomes of the 422 / 500 /
   409 compensations, `ALREADY_REVERSED` with the original transactionId, and the reversal ids of the exit cannot reach
   the central record without a BuildingBlocks change, which belongs to another domain (XMC-001). Interim: they are in
   the in-product `sce_shipment_audit` "dispatch-attempt" rows, written in the same transaction as the outcome (F-3),
   and in the shipment's `DispatchHistory` / `DispatchLateWrites`. → Question for MVP-1 CT / the AUD-001 owner.
2. **F-1 vs S23-4 wording.** After a 500/502/504 on the first post, the attempt now stays `InProgress` with its lease
   (about 40 s of 409). That is safer than §23.3's "before the first post nothing changes", but it differs from that
   wording for these statuses. → One line for MVP-1 CT (pack wording).
3. **Late writer F-1 refinement (CT-APR-02 r6 F-1).** An issue the shipment already records is never reversed by a late
   writer. This is applied in code; the pack text is unchanged.
4. **IDEMPOTENCY_KEY_REUSED on the exit.** The line is skipped and recorded as a fault, so the shipment is not blocked forever.
5. **mongod 8.0.18** (Homebrew) instead of the CI-pinned 7.0.28: no pinned tarball was found on this Mac.
6. **Not run:** the Gateway tour (port 5000), the phase1 Python tests (`scripts/tests`), and a real INVENTORY. The
   permission `inventory.movements.create` has to be granted per role (BL-652 interim) before a tour.
7. **Out of scope (as briefed):** S23-16, DL-2 texts/resx, reservationId. MOD-0186 still has to emit the RETURN_* codes.
8. Plan open point 3 (pack header text) is unchanged.
