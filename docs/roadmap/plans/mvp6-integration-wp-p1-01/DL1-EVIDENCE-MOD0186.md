# MOD-0186 DL-1 — evidence (Q503, plan r3 steps 1–4; Q503-P2 review 01 fixes)

> **Update Q503-P2 (2026-10-30):** independent review `~/Projects/ERP-vNext-lanes/dl1/REVIEW-01-185e690a.md` (ŞARTLI) and the
> owner's decisions (Natig, 2026-10-29) are applied in `880321875`. Every table below is re-measured at that HEAD.

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
| 5 | `185e690a6` | evidence, first version |
| P2 | `880321875` | review 01: F-1 last accepted values, F-2 resend mismatch → integrity fault, F-4 failed D releases the lease, F-7 source-scan guard |
| P2′ | this note | evidence for `880321875` |

## Tests

| What | HEAD | Lane | Result |
|---|---|---|---|
| Full SupplyChain suite, unfiltered (`sc-suite-6152.sh`) | `880321875` | 6152, fresh replica set `rs-q500s`, shut down afterwards | **733 passed, 0 failed, 1 skipped** (same skip), 4 m 10 s, exit 0, `git status` empty at start; run folder `~/Projects/ERP-vNext-lanes/q500-dl1/run-20261030-000923` |
| Full SupplyChain suite, unfiltered (`~/Projects/ERP-vNext-lanes/q500-dl1/sc-suite-6152.sh`) | `bb4ddc408` | 6152, fresh replica set `rs-q500s`, shut down afterwards | **729 passed, 0 failed, 1 skipped** (`ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, skipped by design), 4 m 21 s, exit 0, mongod 8.0.18, `git status` empty at start; run folder `~/Projects/ERP-vNext-lanes/q500-dl1/run-20261029-221419` |
| Full SupplyChain suite, unfiltered (pre-check) | `acf0cea77` | 6153 (`rs-q503d`) | 728 passed, 0 failed, 1 skipped (`ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, skipped by design) |
| `ContractErrorCodeParityTests`, central-audit declaration tests, S23-12 | — | — | inside the full suite |
| AUD-001 architecture `TenantArchitecture.ArchitectureTests --filter AuditTrailStandard` | `bb4ddc408` | — | 20 passed, 2 failed — both list only Diten.CrmService (53) and Diten.Platform (7) commands, no SupplyChain line; this branch changes neither service (log `~/Projects/ERP-vNext-lanes/q503-dl1/arch-bb4ddc408.log`) |
| phase1 `python3 -m unittest discover -s scripts/tests` | `acf0cea77`…`bb4ddc408` (no `scripts/` change in this work) | — | 220 tests, OK (3343 s) |

New tests: `Returns/Receipt/ReceiptAttemptStorageTests` (7), `CustomerReturnMapperTests` (5), `ReturnReceiptTests` (27),
`ReturnReceiptJointScenarioTests` (1); review 01 added 4 to `ReturnReceiptTests` (F-1 ×2, F-2, F-7 source scan).

## Sabotage table at `880321875` (lane port 6153, mongod 8.0.18)

All 34 rows were re-run at `880321875`: **34 of 34 went red, were restored byte for byte and then went green**; `git status`
was clean afterwards. Raw table `~/Projects/ERP-vNext-lanes/q503-dl1/sabotage-880321875-all.tsv`. The 30 rows below keep their
earlier counts except R34-10 (failed=2 passed=3, the filter now also matches the scan guard) and R34-10b (failed=2 passed=1).
The four new rows:

| Rule | Sabotage | File | Sabotaged run | Restored | Run after restore |
|---|---|---|---|---|---|
| F-1 | a new command compared with every line's stored values | `ReturnReceiptCoordinator.cs` | Failed failed=2 passed=0 | restored | Passed failed=0 passed=2 |
| F-2 | posted resend answered with another transactionId only logged | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| F-4 | a failed step D keeps the lease | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |
| F-7 | a Returns source references IServiceProvider | `ReturnReceiptCoordinator.cs` | Failed failed=1 passed=0 | restored | Passed failed=0 passed=1 |

### Earlier table (`acf0cea77` / `bb4ddc408`)

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
   unchanged)"). A Posted answer with **another** transactionId is an integrity fault (review 01 F-2, Natig 2026-10-29): the
   line becomes Unknown, the command stops with 500, the pair goes to `ReceiptAuditHistory` and a `ReceiptIntegrityFault`
   `returns_audit` row; no automatic REVERSAL. Any other non-replay answer is logged and the stored transactionId is kept.
2. **The first refused line ends the command** (422 `INVENTORY_REFUSED` for that line); lines after it are posted by the
   retry. Same as MOD-0183's stop at the first refusal.
3. **The answered stops release the lease** — an INVENTORY 422, 400, 403, 5xx or timeout, a posted-resend mismatch and
   (review 01 F-4) a failed step D — so the user's retry starts at once. An unhandled exception (e.g. a Mongo error while
   saving the attempt) or a client cancellation keeps the lease until it expires (request timeout + 30 s); the line stays
   Unknown and is resent with its key, so that is safe but slower; a line answered
   403 / 5xx / timeout stays Unknown and is resent with its key first. MOD-0183 kept the lease on an ambiguous answer; here
   a resend of the same key and body is always safe (INVENTORY replays or answers 409, which stays Unknown).
4. **Blocked line**: a command that does not unpost it answers 500 again without sending it.
5. **Which values a new command must match (review 01 F-1, Natig 2026-10-29).** With no line Refused or Unknown, a command
   is compared with the attempt's **last accepted values** (`CurrentWarehouseId` / `CurrentLocationId` / `CurrentOccurredAt`,
   set when the attempt opens and on every accepted command), never with each line's — after a correction the lines hold
   different values and the return could not leave InTransit (Blocked exit, failed D). Posted lines keep their own request.
   The 409 `RETURN_RECEIPT_IN_PROGRESS` details carry those last accepted values.
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

5. **Açık nokta: Platform merkezi denetim alan talebi (MOD-0183 F-4 ile birlikte) — review 01 F-3, Natig 2026-10-29: code
   unchanged.** Central audit details (same as MOD-0183 F-4): `CentralAuditMetadata` has no details field, so the per-line reasons
   (`PRE_WP_P1`, `unpostedLines`) and last INVENTORY answers are in the in-product `returns_audit` row of the same
   transaction (`ReceiptLines`, `ReceiptAttempt`), not in Platform's central record. Needs the same owner decision.
   In addition, the late writer's and the resend-mismatch transactions enlist the central record, but the command that runs
   them fails, so that record stays Abandoned (BL-501) and the integrity fault does not reach Platform (same pattern as
   MOD-0183 `ShipmentRepository`); the `audit-central` sabotage proves the enlistment, not the delivery.
6. **Guard changes (review 01 F-6):** S23-12 now allows `ReturnReceiptCoordinator` and `ReturnLateWriterPolicy` to use
   `IInventoryMovementClient`; the central-audit transaction count is 12 (the late writer's append transaction). Touching
   MOD-0183's test files (`FakeInventory.cs`, `ShipmentDispatchTests.cs`) was **approved under XMC-001 by Natig on
   2026-10-29**. The pack §25 file list still names other file names (`ReturnReceiptAttempt.cs`, `ReturnReceipt*Tests.cs`);
   updating it is a pack change, not done here.
9. **`stockIssued` absent vs null (review 01 F-5):** the reader treats a 3.4.0 detail without the field like `null`, i.e.
   PRE_WP_P1. The pack defines only `stockIssued = null` on a Dispatched-or-later shipment and says nothing about a missing
   field, so the code is unchanged (owner decision: do not touch while the pack is silent). Risk today is low: the same
   host's producer always writes the field.
10. **Pack authority text (review 01 F-8):** besides §34's "Draft revision — not approved" (point 3), the pack header says
   "runtime implementation is not authorized" while `status` is `ready-for-dev`. Document-only; the pack is not touched.
11. **Açık nokta: paket/sözleşme uyuşmazlığı — `toLocationId` (CT Q503-P3, 2026-10-30): code unchanged.** Pack r8 §34.1
   (line 929) says `toWarehouseId` and `toLocationId` are "required when `targetStatus = Received`". SHIPMENT-BUNDLE 3.4.0
   (`docs/analysis/contracts/shipment-bundle.openapi.yaml`, `origin/main` `63acdf2ec`, identical on this branch) calls
   `toLocationId` an "optional receiving location"; only `toWarehouseId` is required by the business rule. The code follows
   the contract: missing `toWarehouseId` on Received → 400 `INVALID_REQUEST`; missing `toLocationId` is accepted, and the
   CUSTOMER_RETURN body then leaves the field out. One of the two documents must change (owner decision). If the pack wins,
   the contract changes first (3.4.x), then the code gets the 400 rule, a test and a sabotage row.
7. **Gateway tour and MVP-1 live tour "iade → karantina"** (plan §3 / §4 step 5) not run; acceptance needs them.
8. **R34-3 texts** are DL-2 (resx, 7 languages).
