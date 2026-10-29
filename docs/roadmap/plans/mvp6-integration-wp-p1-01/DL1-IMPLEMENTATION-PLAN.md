# MOD-0183 DL-1 — implementation plan (stock issue at dispatch)

> **Plan revizyonu r3 (2026-10-27):** paket r6'ya göre yeniden yazıldı (onaya giden sürüm, PR #262). r2'den farklar: deneme
> numarası ve kira kimliği, satır başına *gönderildi* işareti, tamamlamadan ya da telafiden önce yeniden gönderme, geç yazan
> kuralı, deneme geçmişi, `ShipmentDetail.stockIssued`, 400 / 403 / `ALREADY_REVERSED` kuralları, `CompensationFailed`
> çıkışı. Paketle çelişen satırda paket kazanır.

DURUM: PLAN — kod izni değildir. MOD-0183 r6'nın CT-APR-01 satırı `main`'de (PR #269, 2026-10-28); kod, kuyruk satırı Q500 (READY) `main`'e girince başlar (Çekirdek 7, R8).
Kaynak: `MOD-0183-shipment-tracking-pod.md` §23 (pack sha256 `07152235c49640c6…`, `status: ready-for-dev`, son CT-APR-02
`docs/records/reviews/MOD-0183/2026-10-27-07152235.md` UYGUN), `SHIPMENT-BUNDLE-3.4.0.patch`, INVENTORY-BUNDLE 1.5.0
`postMovement`. Kod tabanı: `origin/main` (dosya yolları 2026-10-27'de ölçüldü). Dilim: DL-1 = S23-1…S23-6, S23-8…S23-15,
S23-17…S23-20; S23-7 DL-2; S23-16 MOD-0186 DL-1 ile.

## 1. Shape of the change

The dispatch transition calls INVENTORY over HTTP, so it cannot run inside one Mongo transaction. `TransitionShipmentHandler`
keeps every other transition as today; `target == Dispatched` (and the exit from `CompensationFailed`, §1.3) goes to a new
`ShipmentDispatchCoordinator`.

### 1.1 Stored attempt (§23.4 table)

`Shipment.DispatchAttempt` (one at a time) + `Shipment.DispatchHistory` (append-only, closed attempts):

| Field | Note |
|---|---|
| `N` | 1 for the first attempt; +1 **only** when an attempt closes as `Compensated` |
| `State` | `InProgress` / `CompensationFailed` / `Compensated` / `Completed`; wire `dispatchState` (`None` when no open attempt) |
| `OccurredAt`, `TransactionDate`, `PostingDate` | from the command that opened the attempt; never changed (E-6: posting day = UTC day) |
| `LeaseId`, `LeaseUntil` | single-writer lease = request timeout + 30 s, renewed before each INVENTORY call |
| per line, in stored (create) order | `Sent` marker, full request body as first sent, issue `TransactionId` + `LotId`, reversal dates, reversal `TransactionId`, `ExternallyReversed` flag |

`ShipmentDetail.stockIssued`: `true` when the attempt is `Completed`; `null` when the shipment never had an attempt (pre-DL-1
dispatches — MOD-0186's `PRE_WP_P1` marker); `false` otherwise.

### 1.2 Steps

| Step | Where | What |
|---|---|---|
| A — begin / resume | conditional update | Planned + correlation + LE checks (as today); a line without `skuLevel` → 422 `SHIPMENT_LINE_STOCK_INCOMPLETE` (`details.lineNumber`), INVENTORY not called (no `lotId` pre-check — INVENTORY's `LOT_REQUIRED` decides). Take the lease with a filter on `dispatchState` + `leaseUntil`; lease held by another → 409 `DISPATCH_IN_PROGRESS`. No attempt → open `N` (1, or history + 1) with this command's dates. `InProgress` with expired lease → resume with the stored dates. |
| B — post | outside transaction | Per line in stored order: renew lease; mark **sent** (key + body) conditional on `N` + `LeaseId`; `POST` with key `{shipmentId}:{lineNumber}:dispatch:a{N}`, caller token, `X-Legal-Entity-Id` = shipment LE, `X-Correlation-Id`; record `transactionId` + `lotId` conditional on `N` + `LeaseId`. `idempotentReplay: true` = success. |
| B′ — resend sent lines | before C or D | Every *sent* line without a `transactionId` is resent with the same key and body first (replay → record; 422 → nothing was posted). Replaces the r5 reconciliation job. |
| C — compensate | outside, then conditional update | On a line 422, or a 400 on an issue: REVERSAL of every posted line in stored order (§23.4 body; key `…:{line}:reversal:a{N}`; reversal dates stored at the first reversal and resent). `ALREADY_REVERSED` → counts as reversed, integrity fault, central-audit record. All reversed → attempt `Compensated` into history, `N`+1, `dispatchState = None`, shipment stays `Planned`, answer 422 `INVENTORY_REFUSED` (or 500 with correlation id after a 400). A REVERSAL failure (5xx, timeout, 403, 400) → `CompensationFailed`, 409 `DISPATCH_COMPENSATION_FAILED` with the lines. |
| D — complete | Mongo transaction | All lines have an issue → `Dispatched`, `DispatchedAt`, attempt `Completed`, `stockIssued = true`, frozen `ShipmentDispatched` outbox event, idempotency receipt, central-audit enlistment (`CentralAuditTransaction.EnlistAsync`) — one commit (G9). |

The command's idempotency receipt is written only in D or with the C outcome, so a replay after success posts nothing (S23-3).

### 1.3 Other outcomes

- **INVENTORY 403** before or during posting → 403 `INVALID_REQUEST`; the log keeps INVENTORY's code (`CROSS_LE_FORBIDDEN` vs
  missing permission). Before the first post nothing changes; mid-attempt the attempt stays `InProgress`. During compensation
  → `CompensationFailed` (S23-20).
- **INVENTORY 5xx / timeout** → 503 `DEPENDENCY_UNAVAILABLE`; mid-attempt the attempt stays `InProgress`, the user's retry
  resumes after the lease (same keys, same dates).
- **`IDEMPOTENCY_KEY_REUSED`** → integrity fault, attempt blocked like `CompensationFailed`; never a reason to post again.
- **Exit from `CompensationFailed`:** a transition to `Dispatched` or `Cancelled` first resends the open reversals (same keys,
  stored dates); all succeed → `Compensated`, `N`+1, then the requested transition (`Dispatched` opens attempt `N` with this
  command's `occurredAt`). A failure again → 409, nothing else. Permissions: `supplychain.shipments.dispatch` /
  `supplychain.shipments.cancel`, plus `inventory.movements.create` for the reversals. Any other transition and POD while
  `CompensationFailed` → 409 `DISPATCH_COMPENSATION_FAILED`; `Cancelled` and POD while `InProgress` → 409
  `DISPATCH_IN_PROGRESS`.
- **Late writer:** a writer that lost the lease and got a 201 re-reads the shipment: stored `N` equal and `InProgress` /
  `Completed` → log only; stored `N` greater, or equal and `Compensated` / `CompensationFailed` → REVERSAL with
  `…:{line}:reversal:a{own N}`, integrity fault. This is the only INVENTORY call made without the lease.
- **After `Dispatched`:** later transitions post and reverse nothing (returns go through MOD-0186).
- **Background:** a hosted job may only flag expired leases; it posts and reverses nothing (S23-12).

## 2. Files

Under `services/Diten.SupplyChainService/src/Diten.SupplyChainService.<Layer>/`:

| Layer | File | Change |
|---|---|---|
| Contract | `docs/analysis/contracts/shipment-bundle.openapi.yaml` | apply `SHIPMENT-BUNDLE-3.4.0.patch` in the same change (`ContractErrorCodeParityTests`); owner `git apply` if the path is protected |
| Domain | `Domain/Features/Shipments/ShipmentLine.cs` | `SkuLevel`, `LotId`, `FromLocationId`, `FefoBypassReason` (optional, additive) |
| Domain | `Shipment.cs`; new `DispatchAttempt.cs`, `DispatchAttemptLine.cs`, `DispatchState.cs` | §1.1 |
| Domain | `IShipmentRepository.cs` | `TryBeginOrResumeDispatchAsync`, `MarkLineSentAsync`, `RecordLineIssueAsync`, `RecordLineReversalAsync`, `RenewLeaseAsync`, `CloseAttemptAsync(Compensated / CompensationFailed)`, `CompleteDispatchAsync` — every write filtered on tenant, LE, `N` and `LeaseId` |
| Application | new `Features/Shipments/Dispatch/IInventoryMovementClient.cs`, `ShipmentDispatchCoordinator.cs`, `InventoryMovementMapper.cs` (issue + REVERSAL bodies), `LateWriterPolicy.cs` | §1.2–§1.3 |
| Application | `Features/Shipments/Handlers/CommandHandlers/TransitionShipmentHandler.cs`, `CapturePodHandler.cs` | route `Dispatched` and the exit to the coordinator; 409 rules |
| Application | `Common/ContractErrorCodes.cs` | `INVENTORY_REFUSED`, `SHIPMENT_LINE_STOCK_INCOMPLETE`, `DISPATCH_IN_PROGRESS`, `DISPATCH_COMPENSATION_FAILED`; the `RETURN_*` codes of 3.4.0 (MOD-0186 uses them, the parity test needs them now) |
| Application | line validator, create validator, projection, models | new line fields; `ShipmentDetail.dispatchState`, `stockIssued`, line `issueTransactionId` |
| Infrastructure | new `Features/Shipments/InventoryMovementClient.cs`; `Api/Program.cs` | typed `HttpClient` to the Gateway (existing reference-base-URL pattern, port from §3, no new port); maps 201 / replay / 400 / 403 / 422 / 5xx / timeout to distinct results |
| Infrastructure | `Features/Returns/ReturnReferenceReader.cs` | accept the 3.4.0 line fields (today it rejects unknown line properties → every return read would answer 502) |
| Persistence | `Features/Shipments/ShipmentRepository.cs`, `ShipmentDetailMaterializer.cs` | the repository methods, history, detail fields |
| Web | none in DL-1 | screen texts and labels are DL-2 (S23-7) |

## 3. Tests and sabotage proofs

New `tests/.../Shipments/Dispatch/` with a fake INVENTORY `HttpMessageHandler` (records requests, scripted answers, fault
injection between calls) and the owned-replica-set Mongo fixture for the transaction paths. Each rule: a test **and** a
sabotage run in production code (test must go red, then restore).

| AC | Test | Sabotage |
|---|---|---|
| S23-1 | one line → one `GOODS_ISSUE_SALES` with every §23.1 field, key `…:1:dispatch:a1`, LE header | drop `lotId` from the mapper |
| S23-2 | 422 `LOT_UNDER_RECALL` → 422 `INVENTORY_REFUSED`, `details.inventoryCode`, still `Planned`, no event | set status before posting |
| S23-3 | replay of the same command after success → no second request, no second event | write the receipt in step A |
| S23-4 | 503 / timeout before first post → 503, no change; 403 → 403 `INVALID_REQUEST` | map 403 to 503; drop `X-Legal-Entity-Id` |
| S23-5 | no `skuLevel` → 422 incomplete, zero requests; `LOT_REQUIRED` → 422 `INVENTORY_REFUSED`, posted lines compensated | skip the `skuLevel` check |
| S23-6 | cross-tenant / cross-LE id → 404, zero requests | remove the LE filter |
| S23-8 | 3 lines, line 2 refused → line 1 REVERSAL `…:1:reversal:a1`, `sourceDocumentId` = issue id; `Compensated`, `N = 2`; `Planned`; no event | skip compensation |
| S23-9 | crash between line 1 and 2, retry after lease → line 1 replay, line 2 posted, `Dispatched` once | re-date on resume |
| S23-10 | REVERSAL fails → `CompensationFailed`; transitions + POD 409; exit by `Cancelled` and by `Dispatched` resend reversals first; `Dispatched` then uses `…:a2` | return `Planned` on failure |
| S23-11 | stored dates resent; forced date change → `IDEMPOTENCY_KEY_REUSED` = integrity fault, blocked | treat it as a refusal |
| S23-12 | architecture test: only the coordinator and the late-writer policy reference `IInventoryMovementClient` | inject the client into `ShipmentOutboxWorker` |
| S23-13 | crash during compensation, retry → REVERSAL replay, no `ALREADY_REVERSED`, no second reversal | new reversal key per retry |
| S23-14 | re-dispatch after S23-8 → three new `…:a2` issues, new ids, net −(sum) | reuse `a1` keys |
| S23-15 | two concurrent dispatches → one 409 `DISPATCH_IN_PROGRESS`; `Cancelled` / POD during `InProgress` → 409 | drop the lease filter |
| S23-17 | REVERSAL body has only the §23.4 fields; a 400 → integrity fault | add `lotId` or a `from*` field |
| S23-18 | fencing cases (a)–(e) of §23.5 | reverse in case (a); drop the lease-id condition |
| S23-19 | external reversal → `ALREADY_REVERSED` counts as reversed, `Compensated`, no loop | treat it as a failure |
| S23-20 | 403 on a REVERSAL → `CompensationFailed`, 409 (not 403) | map it to 403 |
| reader | shipment detail with 3.4.0 line fields → return create still 201 | restore the strict line-property check |

Then: full SupplyChain test assembly unfiltered on an owned mongod (`~/Projects/ERP-vNext-lanes/suite/sc-suite.sh`),
`ContractErrorCodeParityTests`, central-audit ledger tests, phase1 Python tests (`scripts/tests`), and the Gateway tour
extended with a dispatch against a live INVENTORY on lane ports 6100–6199. MVP-1 live tours remain the acceptance.

## 4. Order of work (local commits)

1. Contract 3.4.0 + error codes + parity test + return-reader tolerance (one commit).
2. Domain + persistence (line fields, attempt, history, conditional repository methods) with storage tests.
3. INVENTORY client + mapper (issue and REVERSAL bodies) with fake-handler tests (S23-1, S23-4, S23-5, S23-17).
4. Coordinator A–D, B′, exit, late writer + handler routing (S23-2, S23-3, S23-6, S23-8…S23-15, S23-18…S23-20), sabotage runs.
5. Full suite + Gateway tour; then one PR for DL-1 (no push before the slice is finished).

## 5. Open points

r2's five points are closed in the pack: S23-13 is in DL-1; mid-attempt 503 keeps `InProgress`; cancel during `InProgress` →
409 `DISPATCH_IN_PROGRESS`; INVENTORY 403 → 403; the reader change and `issueTransactionId` are in 3.4.0 / this change.

Still open (none blocks the start of DL-1 code):

1. **MVP-1 CT R-1 / R-2** (REVERSAL bodies incl. `CUSTOMER_RETURN` reversal; `ALREADY_REVERSED`). S23-17 and S23-19 follow
   the pack as written; an answer that differs means a new pack revision.
2. **Closed `inventoryCode` list:** re-read `InventoryErrorCodes.cs` at `origin/main` when DL-1 starts; a new code needs a text
   in DL-2.
3. **Pack header text:** §23's opening note still says "Draft revision — not approved" while `status` is `ready-for-dev`
   (same kind as MOD-0147/0148 F-1). Not changed now — the pack is under approval in #262; fix in the next MOD-0183 revision.
4. **Permission grant for tests and tours:** the dispatching role needs `inventory.movements.create` through Roles →
   Permissions (BL-652 interim); the lane tour's minted token must carry it.
