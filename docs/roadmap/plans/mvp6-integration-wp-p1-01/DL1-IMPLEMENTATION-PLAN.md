# MOD-0183 DL-1 — implementation plan (stock issue at dispatch)

DURUM: PLAN — kod izni değildir. Kod, MOD-0183 revizyonunun CT-APR-02 incelemesi (MVP-1 CT) ve `main`'deki CT-APR-01
satırından (Beste) sonra başlar (Çekirdek 7). Kaynak: `MOD-0183-shipment-tracking-pod.md` §23 (pack sha256
`febf6e35…`), `SHIPMENT-BUNDLE-3.4.0.patch`, INVENTORY-BUNDLE 1.5.0 `postMovement`. Kod tabanı: `origin/main` (2026-10-23;
#198 merkezi denetim dahil). Yazan: MVP-6 CT, 2026-10-23.

## 1. Shape of the change

The dispatch transition calls INVENTORY over HTTP, so it cannot run inside one Mongo transaction. `TransitionShipmentHandler`
keeps every other transition as today; `target == Dispatched` goes to a new `ShipmentDispatchCoordinator` in four steps:

| Step | Where | What |
|---|---|---|
| A — begin | Mongo transaction | Planned + correlation + LE checks (as today); line stock fields complete (`SHIPMENT_LINE_STOCK_INCOMPLETE`, INVENTORY not called); `dispatchState = InProgress`; store `DispatchAttempt` with `occurredAt`, `transactionDate`, `postingDate` and per line the dispatch key `{shipmentId}:{lineNumber}:dispatch:v1` and reversal key. An existing `InProgress` attempt is **resumed with its stored dates** (§23.4 step 6), never re-dated. |
| B — post | outside transaction | Per line `POST` movement (§23.1 fields, caller token, `X-Correlation-Id`, stored key and dates). Store each `transactionId` + `lotId` with a conditional single-document update as soon as it returns. `idempotentReplay: true` = success. |
| C — compensate | outside, then transaction | On a line 422: REVERSAL of every stored line (§23.4 G3, stored reversal dates); all reversed → `dispatchState = None`, status stays `Planned`, 422 `INVENTORY_REFUSED`. A REVERSAL failure → `dispatchState = CompensationFailed`, 409 `DISPATCH_COMPENSATION_FAILED` with the lines. |
| D — complete | Mongo transaction | All lines posted → `Dispatched`, `DispatchedAt`, frozen `ShipmentDispatched` outbox event, idempotency receipt, central-audit enlistment (`CentralAuditTransaction.EnlistAsync`), `dispatchState = None` — one commit (G9). |

The command's own idempotency receipt is written only in D (or with the C refusal), so a replay after success returns the
original result and posts nothing (S23-3).

## 2. Files

| Layer | File (under `services/Diten.SupplyChainService/src/`) | Change |
|---|---|---|
| Contract | `docs/analysis/contracts/shipment-bundle.openapi.yaml` | apply `SHIPMENT-BUNDLE-3.4.0.patch` (contract writer / owner `git apply` if the path is protected) — same change as the code (`ContractErrorCodeParityTests`) |
| Domain | `Domain/Features/Shipments/ShipmentLine.cs` | add `SkuLevel`, `LotId`, `FromLocationId`, `FefoBypassReason` (optional, additive) |
| Domain | `Domain/Features/Shipments/Shipment.cs`, new `DispatchAttempt.cs`, new `DispatchState.cs` | `DispatchState` (`None`/`InProgress`/`CompensationFailed`), attempt record with stored dates and per-line keys/results |
| Domain | `Domain/Features/Shipments/IShipmentRepository.cs` | `BeginDispatchAsync`, `RecordLineResultAsync`, `CompleteDispatchAsync`, `EndDispatchAsync(refused / compensationFailed)` |
| Application | new `Application/Features/Shipments/Dispatch/IInventoryMovementClient.cs`, `ShipmentDispatchCoordinator.cs`, `InventoryMovementMapper.cs` | §23.1 mapping, the A–D flow |
| Application | `Handlers/CommandHandlers/TransitionShipmentHandler.cs`, `CapturePodHandler.cs` | route Dispatched to the coordinator; any transition or POD while `CompensationFailed` → 409 |
| Application | `Common/ContractErrorCodes.cs` | `INVENTORY_REFUSED`, `SHIPMENT_LINE_STOCK_INCOMPLETE`, `DISPATCH_COMPENSATION_FAILED` (`RETURN_SOURCE_MOVEMENT_MISSING` belongs to MOD-0186 DL-1 but the parity test needs it with 3.4.0) |
| Application | `Validators/ShipmentLineValidator.cs`, `CreateShipmentValidator.cs`, `ShipmentProjection.cs`, `ShipmentModels.cs` | new line fields; `ShipmentDetail.dispatchState` |
| Infrastructure | new `Infrastructure/Features/Shipments/InventoryMovementClient.cs`; `Api/Program.cs` | typed `HttpClient` to the Gateway (`LegalEntity:ReferenceBaseUrl` pattern, port from §3, no new port); 403/5xx/timeout → dependency failure |
| Persistence | `Persistence/Features/Shipments/ShipmentRepository.cs`, `ShipmentDetailMaterializer.cs` | the four repository methods; line results; detail shows `dispatchState` |
| Web | none in DL-1 | screen texts are DL-2 |

## 3. Tests and sabotage proofs

New `tests/.../Shipments/Dispatch/` with a fake INVENTORY `HttpMessageHandler` (records requests; scripted answers) and the
owned-replica-set Mongo fixture for the transaction paths. Each rule gets a test **and** a sabotage run (break the rule in
production code, the test must go red, restore):

| AC | Test | Sabotage |
|---|---|---|
| S23-1 | one line → one request with every §23.1 field and header | drop `lotId` from the mapper |
| S23-2 | 422 `LOT_UNDER_RECALL` → 422 `INVENTORY_REFUSED`, `details.inventoryCode`, still `Planned`, no event | set status before posting |
| S23-3 | same Idempotency-Key replay after success → no second request, no second event | write receipt in step A |
| S23-4 | INVENTORY 403 / 503 / timeout on line 1 → 503, no state change | map 403 to 422 |
| S23-5 | lot-tracked line without `lotId` → 422 incomplete, handler sees zero requests | skip the check |
| S23-6 | cross-tenant / cross-LE id → 404, zero requests | remove the LE predicate in `BeginDispatchAsync` |
| S23-8 | 3 lines, line 2 refused → line 1 REVERSAL with `…:1:reversal:v1` and `sourceDocumentId` = original transactionId; `Planned`; no event | skip compensation |
| S23-9 | crash after line 1 (fault injection between B calls), user retry → line 1 resent same key + dates, replay true, line 2 posted, one event | re-date on resume |
| S23-10 | REVERSAL fails → `CompensationFailed`, transitions + POD 409; user retry clears | return `Planned` on failure |
| S23-11 | stored dates sent on retry; forced date change → `IDEMPOTENCY_KEY_REUSED` logged as integrity fault, shipment blocked | treat it as refusal |
| S23-12 | no hosted service posts or reverses (architecture test: only the coordinator references `IInventoryMovementClient`) | inject the client into `ShipmentOutboxWorker` |
| S23-13 | crash during compensation, user retry → REVERSAL resent same key + dates, replay true, no `ALREADY_REVERSED` | new reversal key per attempt |

Then: full SupplyChain test assembly unfiltered on an owned mongod (`~/Projects/ERP-vNext-lanes/suite/sc-suite.sh`), the
`ContractErrorCodeParityTests`, central-audit ledger tests, and the Gateway tour extended with a dispatch against a live
INVENTORY on lane ports 6100–6199 (MVP-1 live tours remain the acceptance).

## 4. Order of work (local commits, owner approval before each)

1. Contract 3.4.0 + error codes + parity test (one commit; nothing else compiles against it yet).
2. Domain + persistence (line fields, dispatch state, repository methods) with storage tests.
3. INVENTORY client + mapper with fake-handler tests (S23-1, S23-4, S23-5).
4. Coordinator A–D + handler routing (S23-2, S23-3, S23-6, S23-8…S23-13) with sabotage runs.
5. Full suite + Gateway tour; then the PR (module DL-1 finished, per the no-PR-until-done rule).

## 5. Points to settle before code (raised by this plan; not decided)

1. **S23-13 is outside both slices.** The pack's slice table lists DL-1 as "S23-1…S23-6, S23-8…S23-12"; S23-13 was added
   with the 2026-10-23 follow-up notes. Proposal: DL-1 covers S23-13 — fix in the pack together with any MVP-1 CT findings
   (changes the hash, so before the CT-APR-01 row).
2. **INVENTORY unavailable after some lines are posted.** §23.3 says 503 = no state change, which holds only before the first
   post. Proposal: mid-dispatch 503 keeps `InProgress` with the posted lines and answers 503; the user's retry resumes
   (same keys, same dates) — the same path as a crash (§23.4 step 6).
3. **Cancel while `InProgress`.** `Planned → Cancelled` during an open attempt would leave posted stock issues. Proposal: 409
   (code to choose with the contract owner; 3.4.0 has only `DISPATCH_COMPENSATION_FAILED`).
4. **Permission pre-check.** §23.2 relies on INVENTORY's 403 → 503. Proposal: keep it (no copy of another service's
   permission in SupplyChain), with a log line naming the missing permission.
5. **The return reader breaks on the 3.4.0 line fields.** `Infrastructure/Features/Returns/ReturnReferenceReader.cs` rejects
   any line property except the five required ones and `inventoryReferenceId`, so after this change every return read would
   answer 502. The reader must accept the new fields in this same change, with a test "shipment detail with 3.4.0 line fields →
   return create still 201". Also proposed: add read-only `ShipmentLine.issueTransactionId` to 3.4.0 now, so MOD-0186 DL-1 can
   read the issue over the seam (`MOD-0186-DL1-IMPLEMENTATION-PLAN.md` §4).
