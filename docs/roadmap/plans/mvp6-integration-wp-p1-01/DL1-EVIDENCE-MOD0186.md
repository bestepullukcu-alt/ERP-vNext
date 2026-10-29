# MOD-0186 DL-1 — evidence (Q503, plan r3 steps 1–4)

- Branch `feature/sce/mod0186-dl1-01`, base `origin/main` `317a48b13` (MOD-0183 DL-1, #294, is in it). Local commits only;
  nothing pushed. The queue row Q503 is in PR #303 (`9a5f7e468`), not in this branch; the owner allowed the early local start
  (Natig, 2026-10-29, `~/Projects/ERP-vNext-lanes/dl1/DECISION-EARLY-START-Q503-Q502-2026-10-29.md`).
- Pack `MOD-0186-reverse-logistics.md` **r8**, sha256 `e96a9add5b9213ecc444a826ac86e61957b5673027b3e809c2da02e781776e89`
  (measured before the first commit; `ready-for-dev`, CT-APR-02 `docs/records/reviews/MOD-0186/2026-10-27-e96a9add.md`).
  Plan: `MOD-0186-DL1-IMPLEMENTATION-PLAN.md` r3. DL-1 covers R34-1, R34-2, R34-4…R34-17 and the non-text part of R34-3.
- Contract: SHIPMENT-BUNDLE 3.4.0 as it is on `main` — unchanged. INVENTORY, BuildingBlocks, `ocelot.json`, `.antigravity/**`
  and resx are untouched.
- Evidence level: E3 — an owned replica set, the real host, coordinator, repository, shipment reader and INVENTORY client,
  talking to a fake INVENTORY and a fake shipment detail (R34-8: the real Shipments API of the same host). Not E4: there was
  no Gateway tour and no real INVENTORY (plan §4 step 5 is not part of this work).
- My PASS does not close the work (K13); the CT verifies independently.

## Commits

| Step | Commit | Content |
|---|---|---|
| 1 | `221077681` | `ReceiptAttempt` (lease, per-line version `k`, sent key, last answer, six states) and append-only `ReceiptAuditHistory` on `ReturnOrder`; begin/resume conditional on the observed revision, saves on the held lease, all on tenant + LE + InTransit; step D inside the existing `MutateAsync` transaction; storage tests |
| 2 | `ebd96c7b3` | reader reads 3.4.0 `stockIssued` and each line's `skuLevel`, `lotId`, `issueTransactionId`; CUSTOMER_RETURN and late REVERSAL bodies; tests |
| 3 | `5c435a674` | `ReturnReceiptCoordinator` (A–D), `unpostedLines`, `ReturnLateWriterPolicy`, `Received` routing, wire `lines[]` and error `details`, client reference → 400; guards S23-12 (+2 users) and central audit (11 → 12 transactions); acceptance tests |
| 4 | `acf0cea77` | joint scenario R34-8 / S23-16 with MOD-0183 DL-1 |
| 3′ | `bb4ddc408` | R34-10 guard: no receipt type takes MOD-0183's repository or Mongo |
| 5 | this note | evidence |

## Tests

| What | HEAD | Lane | Result |
|---|---|---|---|
| Full SupplyChain suite, unfiltered (`~/Projects/ERP-vNext-lanes/q500-dl1/sc-suite-6152.sh`) | `bb4ddc408` | 6152, fresh replica set `rs-q500s`, shut down afterwards | **729 passed, 0 failed, 1 skipped** (`ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, skipped by design), 4 m 21 s, exit 0, mongod 8.0.18, `git status` empty at start; run folder `~/Projects/ERP-vNext-lanes/q500-dl1/run-20261029-221419` |
| Full SupplyChain suite, unfiltered (pre-check) | `acf0cea77` | 6153 (`rs-q503d`) | 728 passed, 0 failed, 1 skipped (`ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, skipped by design) |
| `ContractErrorCodeParityTests`, central-audit declaration tests, S23-12 | — | — | inside the full suite |
| AUD-001 architecture `TenantArchitecture.ArchitectureTests --filter AuditTrailStandard` | `bb4ddc408` | — | 20 passed, 2 failed — both list only Diten.CrmService (53) and Diten.Platform (7) commands, no SupplyChain line; this branch changes neither service (log `~/Projects/ERP-vNext-lanes/q503-dl1/arch-bb4ddc408.log`) |
| phase1 `python3 -m unittest discover -s scripts/tests` | `acf0cea77`…`bb4ddc408` (no `scripts/` change in this work) | — | 220 tests, OK (3343 s) |

New tests: `Returns/Receipt/ReceiptAttemptStorageTests` (7), `CustomerReturnMapperTests` (5), `ReturnReceiptTests` (27),
`ReturnReceiptJointScenarioTests` (1).

## Sabotage table (lane port 6153, mongod 8.0.18)

Each row breaks the production code once. The guarding tests went red, the file was restored byte for byte (sha256
compared), and the same tests then ran green. **30 of 30 rows went red, were restored and then went green**; `git status`
was clean afterwards. Harness `~/Projects/ERP-vNext-lanes/q503-dl1/tools/sabotage-q503.py` (same method as Q500's
`sabotage6.py`); raw tables `sabotage-acf0cea77.tsv` (rows 1–28) and `sabotage-bb4ddc408.tsv` (`details`, `R34-10b`).
`bb4ddc408` adds only a test to `acf0cea77`. The first `details` run at `acf0cea77` did not compile (the sabotage made
`ReturnContractError.Create(…, null)` ambiguous) and does not count; it was re-run with a typed null.

| Rule | Sabotage | File | Sabotaged run | Restored | Run after restore |
|---|---|---|---|---|---|
| R34-1a | CUSTOMER_RETURN sent into AVAILABLE | `CustomerReturnMovementMapper.cs` | Failed failed=3 passed=0 | restored | Passed failed=0 passed=3 |
| R34-1b | lotId taken from the request (toLocationId), not from the issued line | `CustomerReturnMovementMapper.cs` | Failed failed=3 passed=0 | restored | Passed failed=0 passed=3 |
| R34-2 | no receipt replay before the attempt | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R34-3 | a refusal sends a REVERSAL for the posted lines | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R34-4 | INVENTORY 403 answered as 503 | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=2 | restored | Passed failed=0 passed=3 |
| R34-5 | tenant / legal-entity filter dropped from the return lookups | `ReturnRepository.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| R34-6 | issued line without issueTransactionId posted anyway | `ReturnReceiptCoordinator.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| R34-7a | 409 for other values dropped | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R34-7b | stored requests (dates) rebuilt on retry, Posted lines included | `ReturnReceiptCoordinator.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| R34-8 | the return reads attempt a1's issue | `ShipmentProjection.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R34-9 | serialIds sent with the CUSTOMER_RETURN | `CustomerReturnMovementMapper.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R34-10 | a shipment with stockIssued = true treated as PRE_WP_P1 | `ReturnReceiptCoordinator.cs` | Failed failed=2 passed=1 | restored | Passed failed=0 passed=3 |
| R34-10b | the coordinator reads MOD-0183's store (IShipmentRepository injected) | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=1 | restored | Passed failed=0 passed=2 |
| R34-11 | corrected line resent under a1 | `ReturnReceiptCoordinator.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| R34-12 | unpostedLines accepted for any refusal code | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R34-13 | the new value applied to an Unknown line | `ReturnReceiptCoordinator.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| R34-14 | a key-reused (Blocked) line unpostable as INTEGRITY_FAULT | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R34-15 | sent marker ignored: a timeout leaves the line correctable (Refused) | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| R34-16 | late writer without the k comparison | `ReturnLateWriterPolicy.cs` | Failed failed=1 passed=1 | restored | Passed failed=0 passed=2 |
| R34-17 | late writer without the state condition | `ReturnLateWriterPolicy.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| client-ref | client inventoryTransactionReferenceId kept on Received | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=1 | restored | Passed failed=0 passed=2 |
| lease-id | lease-id condition dropped from attempt saves | `ReturnRepository.cs` | Failed failed=2 passed=1 | restored | Passed failed=0 passed=3 |
| lease-rev | takeover without the observed revision | `ReturnRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| lease-time | lease expiry not checked on resume | `ReturnReceiptCoordinator.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| stepD | step D commits without the held lease / complete attempt | `ReturnRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| reader | stockIssued not read from the shipment detail | `ReturnReferenceReader.cs` | Failed failed=3 passed=0 | restored | Passed failed=0 passed=3 |
| details | error details dropped at the wire | `ReturnsController.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| audit-reasons | Received audit row without the per-line reasons and answers | `ReturnRepository.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| audit-late | late-writer audit row dropped | `ReturnRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| audit-central | late-writer transaction without central-audit enlistment | `ReturnRepository.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |

Where a row differs from plan r3 §3's wording: R34-1b — there is no client lot, so "lotId from the request" is the request's
location; R34-2 — "write the receipt in step B" has no seam here, the replay check is removed instead; R34-15 — the sent
marker is the Unknown state, so the sabotage records a timeout as Refused.

## Decisions taken in the code (for the CT review)

1. **Posted lines are resent on every retry** (R34-3, R34-11: "line 1 is resent with its stored request (replay,
   unchanged)"); an answer other than the same replay is logged and the stored transactionId is kept.
2. **The first refused line ends the command** (422 `INVENTORY_REFUSED` for that line); lines after it are posted by the
   retry. Same as MOD-0183's stop at the first refusal.
3. **Every stop releases the lease** (422, 400, 403, 5xx, timeout), so the user's retry starts at once; a line answered
   403 / 5xx / timeout stays Unknown and is resent with its key first. MOD-0183 kept the lease on an ambiguous answer; here
   a resend of the same key and body is always safe (INVENTORY replays or answers 409, which stays Unknown).
4. **Blocked line**: a command that does not unpost it answers 500 again without sending it.
5. **The 409 `RETURN_RECEIPT_IN_PROGRESS` details** carry the first stored line's `toWarehouseId`, `toLocationId`,
   `occurredAt` (the contract's `returnReceiptInProgress` example).
6. **Fingerprint**: the 3.4.0 fields join the transition fingerprint only when present, so a key stored before them keeps
   its hash.
7. **Late REVERSAL** reuses MOD-0183's §23.4 body builder; `ALREADY_REVERSED` counts as reversed with a fault (open point 2).

## Open points

Plan r3 §5 (unchanged by this work):

1. **MVP-1 CT R-1 (extended):** whether INVENTORY reverses a `CUSTOMER_RETURN` out of `QUARANTINE` with the §34.2 body.
   R34-16 / R34-17 are measured against the fake INVENTORY only.
2. **MVP-1 CT R-2:** `ALREADY_REVERSED` on the late REVERSAL is not named in §34.2; treated like MOD-0183.
3. **Pack header text:** §34's opening note still says "Draft revision — not approved" while `status` is `ready-for-dev`.
4. **Permission grant for tests and tours:** the receiving role needs `inventory.movements.create` (BL-652 interim); the
   lane tour's token must carry it. Not exercised here (fake INVENTORY).

Found in this work:

5. **Central audit details (same as MOD-0183 F-4):** `CentralAuditMetadata` has no details field, so the per-line reasons
   (`PRE_WP_P1`, `unpostedLines`) and last INVENTORY answers are in the in-product `returns_audit` row of the same
   transaction (`ReceiptLines`, `ReceiptAttempt`), not in Platform's central record. Needs the same owner decision.
6. **Guard changes to review:** S23-12 now allows `ReturnReceiptCoordinator` and `ReturnLateWriterPolicy` to use
   `IInventoryMovementClient`; the central-audit transaction count is 12 (the late writer's append transaction).
7. **Gateway tour and MVP-1 live tour "iade → karantina"** (plan §3 / §4 step 5) not run; acceptance needs them.
8. **R34-3 texts** are DL-2 (resx, 7 languages).
