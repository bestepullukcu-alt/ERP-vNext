# MOD-0186 DL-1 — implementation plan (customer return → quarantine)

> **Plan revizyonu r3 (2026-10-27):** paket r8'e göre yeniden yazıldı (onaya giden sürüm, PR #262). r2'den farklar: satır
> başına istek sürümü `a{k}` ve *gönderildi* işareti, satır durumları (Posted / NotPosted / Refused / Unknown / Blocked),
> 422 alan anahtarın asla yeniden gönderilmemesi, durum temelli geç yazan kuralı ve geç REVERSAL, `unpostedLines` /
> `RETURN_LINE_NOT_UNPOSTABLE`, `PRE_WP_P1` (`stockIssued = null`), `ReturnResponse.lines[]`. Paketle çelişen satırda paket
> kazanır.

DURUM: PLAN — kod izni değildir. MOD-0186 r8'in CT-APR-01 satırı `main`'de (PR #269, 2026-10-28); kod **MOD-0183 §23 DL-1 `main`'e
girince** ve kendi kuyruk satırıyla başlar (CT-APR-01 notu "only DL-1, DL-2; after MOD-0183 DL-1"; Çekirdek 7). Kaynak:
`MOD-0186-reverse-logistics.md` §34 (pack sha256 `e96a9add5b9213ec…`, `status: ready-for-dev`, son CT-APR-02
`docs/records/reviews/MOD-0186/2026-10-27-e96a9add.md` UYGUN), `SHIPMENT-BUNDLE-3.4.0.patch` (MOD-0183 DL-1 ile gelir),
INVENTORY-BUNDLE 1.5.0 `postMovement`, MOD-0183 planı r3 (`DL1-IMPLEMENTATION-PLAN.md`). Kod tabanı: `origin/main` (dosya
yolları 2026-10-27'de ölçüldü). Dilim: DL-1 = R34-1, R34-2, R34-4…R34-17 ve R34-3'ün metin dışı kısmı; DL-2 = R34-3
metinleri.

## 1. Shape of the change

Only `InTransit → Received` changes; every other return transition stays as today. The INVENTORY calls cannot sit inside the
Mongo transaction, so `TransitionReturnHandler` routes `Received` to a new `ReturnReceiptCoordinator`.

### 1.1 Stored attempt (§34.2)

`ReturnOrder.ReceiptAttempt` + `ReturnOrder.ReceiptAuditHistory` (append-only, no lease needed):

| Field | Note |
|---|---|
| `LeaseId`, `LeaseUntil` | single-writer lease (request timeout + 30 s), renewed before each INVENTORY call |
| per line, in stored order | full §34.1 request body (incl. dates and `toWarehouseId` / `toLocationId`), version `K`, `Sent` marker (key + body), last INVENTORY answer, `State`, `TransactionId`, `InventoryPosting` + reason |
| `State` | `Posted` (has `transactionId`) / `NotPosted` (`PRE_WP_P1` or `unpostedLines` reason) / `Refused` (last answer 422) / `Unknown` (sent, no recorded answer) / `Blocked` (400 `INVALID_REQUEST`, or `IDEMPOTENCY_KEY_REUSED`) |
| audit history | late-writer pairs (receipt + reversal `transactionId`), or the receipt alone when the reversal failed |

### 1.2 Steps

| Step | Where | What |
|---|---|---|
| A — read source | HTTP (existing shipment seam) | read the shipment detail through `ReturnReferenceReader`: `stockIssued` and per line `issueTransactionId`, `skuLevel`, `lotId`. `stockIssued = null` → every line `NotPosted` / `PRE_WP_P1`, no INVENTORY call (R34-10). `stockIssued = true` and a line without `issueTransactionId` → 422 `RETURN_SOURCE_MOVEMENT_MISSING`, no movement (R34-6). Never read MOD-0183's store directly. |
| B — begin / resume | conditional update | lease held by another → 409 `RETURN_RECEIPT_IN_PROGRESS`. First command → store the attempt (bodies from §34.1, dates from this command, `K = 1`). Later command: Posted / NotPosted keep their stored request; Refused lines take this command's `toWarehouseId`, `toLocationId`, `occurredAt` under `K+1`; Unknown lines keep theirs. Different values while no line is Refused and none Unknown → 409 `RETURN_RECEIPT_IN_PROGRESS` with the stored values in `details` (R34-7). Client `inventoryTransactionReferenceId` on `Received` → 400 `INVALID_REQUEST`. |
| C — post | outside transaction | per line in stored order, Unknown lines first by the same rule: renew lease; mark **sent** (key `{returnId}:{shipmentLineNumber}:customer-return:a{K}` + body) conditional on the lease; `POST` `CUSTOMER_RETURN` → `QUARANTINE`, caller token, `X-Legal-Entity-Id` = return LE, `X-Correlation-Id`; record the answer conditional on the lease. 201 / replay → Posted. 422 → Refused (that key is never sent again). 400 → Blocked. 5xx / timeout → stays Unknown. |
| D — commit | Mongo transaction | every line Posted or NotPosted-with-reason → `Received`, per-line `InventoryTransactionReferenceId` (service-set), receipt, central-audit enlistment (reasons and last INVENTORY answers), frozen `ReturnReceived` event (with the completing command's `occurredAt`), `ReturnResponse.lines[]` — one commit. |

Posted lines are never reversed by the lease holder: the goods are physically in quarantine.

### 1.3 Outcomes

- Any line Refused → 422 `INVENTORY_REFUSED` (`details.shipmentLineNumber`, `details.inventoryCode`), stays `InTransit`.
- 5xx / timeout → 503 `DEPENDENCY_UNAVAILABLE`, no transition. 403 → 403 `INVALID_REQUEST`, never 503.
- 400 `INVALID_REQUEST` / `IDEMPOTENCY_KEY_REUSED` → 500 with correlation id, integrity fault, line Blocked.
- **`unpostedLines`** (`shipmentLineNumber`, `reasonCode`) accepted only for a line Refused with `ITEM_NOT_ACTIVE`,
  `SKU_LEVEL_NOT_STOCKABLE` or `UNKNOWN_ITEM`, or Blocked by 400 `INVALID_REQUEST` (`INTEGRITY_FAULT`); any other line
  (incl. Blocked by `IDEMPOTENCY_KEY_REUSED`) → 422 `RETURN_LINE_NOT_UNPOSTABLE` (`details.shipmentLineNumber`,
  `details.lastInventoryCode`). Needs `.receive`.
- **Late writer** (lost lease, got a 201): re-read; stored `K` equal and line Unknown, or Posted with this same
  `transactionId` → log only. Stored `K` greater, or equal and line Refused / NotPosted / Blocked → REVERSAL (§34.2 body,
  key `{returnId}:{shipmentLineNumber}:customer-return-reversal:a{own K}`, dates = moment of its 201, reused on its own
  retry), append the pair to the audit history, integrity fault. REVERSAL failure → append the receipt alone, integrity
  fault, owner corrects in Inventory (stated residual risk).

## 2. Files

Under `services/Diten.SupplyChainService/src/Diten.SupplyChainService.<Layer>/`:

| Layer | File | Change |
|---|---|---|
| Contract | `docs/analysis/contracts/shipment-bundle.openapi.yaml` | nothing new: 3.4.0 (shipped with MOD-0183 DL-1) already has `toWarehouseId`, `toLocationId`, `unpostedLines`, `ReturnResponse.lines[]`, `stockIssued`, `issueTransactionId` and the `RETURN_*` codes |
| Domain | `Domain/Features/Returns/ReturnLine.cs`, `ReturnOrder.cs`; new `ReceiptAttempt.cs`, `ReceiptLineState.cs`; `IReturnRepository.cs` | §1.1; conditional methods filtered on tenant, LE and `LeaseId`; append-only `AppendAuditPairAsync` |
| Application | new `Features/Returns/Receipt/ReturnReceiptCoordinator.cs`, `CustomerReturnMovementMapper.cs` (receipt + late REVERSAL bodies), `ReturnLateWriterPolicy.cs` | §1.2–§1.3; reuses MOD-0183's `IInventoryMovementClient` |
| Application | `Features/Returns/Handlers/CommandHandlers/TransitionReturnHandler.cs`, `ReturnModels.cs`, `ReturnRequestFingerprint.cs`, `Validators/TransitionReturnValidator.cs` | route `Received`; new body fields (warehouse / location required on `Received`, `unpostedLines`); fingerprint includes them; refuse client `inventoryTransactionReferenceId` on `Received` |
| Infrastructure | `Features/Returns/ReturnReferenceReader.cs` | read `stockIssued`, `issueTransactionId`, `skuLevel`, `lotId` (tolerance for the 3.4.0 fields already lands in MOD-0183 DL-1) |
| Persistence | `Features/Returns/ReturnRepository.cs` | attempt, history, commit D with per-line references and `lines[]` |
| Web / l10n | none in DL-1 | `frontend/Diten.Web/Resources/Views/SupplyChain/Returns/ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx` texts are DL-2 |

## 3. Tests and sabotage proofs

Fake INVENTORY handler and fake shipment detail (`HttpMessageHandler`, fault injection between calls), owned replica set for
the commit. Each rule: a test **and** a sabotage run in production code.

| AC | Test | Sabotage |
|---|---|---|
| R34-1 | 1-line and 3-line receipt → N `CUSTOMER_RETURN` into `QUARANTINE`, item/SKU/level/lot from the completed attempt's issue, LE header; `lines[]` shows each reference | send `AVAILABLE`; take `lotId` from the request |
| R34-2 | replay of the receipt command → no second request, no second event | write the receipt in step B |
| R34-3 | line 2 refused → `InTransit`, line 1 kept; retry → line 1 replay, line 2 posted, one event | send a REVERSAL for line 1 |
| R34-4 | 503 / timeout → 503, no transition; 403 → 403 | map 403 to 503 |
| R34-5 | cross-tenant / cross-LE return → 404, zero requests | drop the scope filter |
| R34-6 | `stockIssued = true`, line without `issueTransactionId` → 422 `RETURN_SOURCE_MOVEMENT_MISSING`, zero requests | default the lot from the return body |
| R34-7 | no Refused / Unknown line + other values → 409 with stored values; same values → byte-identical resend | recompute dates on retry |
| R34-8 | joint scenario with MOD-0183 (S23-16), numbers as in §34.3 | read attempt a1's issue |
| R34-9 | serial-tracked SKU → INVENTORY serial refusal shown, nothing posted for that line | send `serialIds` |
| R34-10 | `stockIssued = null` → no INVENTORY call, `NotPosted` / `PRE_WP_P1` in audit and `lines[]` | treat `stockIssued = true` as pre-WP-P1; read MOD-0183's store |
| R34-11 | line 2 refused `UNKNOWN_LOCATION`, new `toLocationId` → line 2 posted under `a2`, line 1 replayed unchanged | resend line 2 under `a1` |
| R34-12 | `ITEM_NOT_ACTIVE` + `unpostedLines` → `Received`, `NotPosted`; same for `UNKNOWN_LOCATION` → 422 | accept any code |
| R34-13 | timeout on line 2 + new `toLocationId` → stored request resent first; new value only after Refused | apply the new value to an Unknown line |
| R34-14 | 400 → 500, Blocked; `unpostedLines` `INTEGRITY_FAULT` completes; Posted / fixable / `IDEMPOTENCY_KEY_REUSED` line → 422 `RETURN_LINE_NOT_UNPOSTABLE` | allow unposting a key-reused line |
| R34-15 | lost answer on a corrected line → `a2` resent with X → replay, Posted at X | drop the *sent* marker |
| R34-16 | late writer, superseded version → reverses with `…:customer-return-reversal:a1`, QUARANTINE net +quantity once | remove the `K` comparison |
| R34-17 | late writer, same version, line Refused → reverses; correction goes under `a2`; `ITEM_NOT_ACTIVE` variant hides no stock | drop the state condition |
| client ref | client `inventoryTransactionReferenceId` on `Received` → 400 | keep the client value |

Then: full SupplyChain test assembly unfiltered (`~/Projects/ERP-vNext-lanes/suite/sc-suite.sh`), `ContractErrorCodeParityTests`,
central-audit ledger tests, phase1 Python tests (`scripts/tests`), and the Gateway tour extended "dispatch → return →
quarantine" on lane ports 6100–6199. MVP-1 live tour "iade → karantina" is the acceptance.

## 4. Order of work (local commits)

1. Domain + persistence (attempt, line states, history, conditional methods) with storage tests.
2. Reader fields + mapper (receipt and late REVERSAL bodies) with fake-handler tests (R34-1, R34-6, R34-10).
3. Coordinator A–D, `unpostedLines`, late writer + handler routing (the rest of §3) with sabotage runs.
4. Joint scenario R34-8 / S23-16 with MOD-0183 DL-1 on `main`.
5. Full suite + Gateway tour; then one PR for DL-1.

## 5. Open points

r2's five points are closed in the pack: `issueTransactionId` is on the seam (3.4.0); the reader tolerance ships in MOD-0183
DL-1; dates are stored with the attempt; the contract is SHIPMENT-BUNDLE 3.4.0; the client reference on `Received` → 400.

Still open (none blocks the start once MOD-0183 DL-1 is on `main`):

1. **MVP-1 CT R-1 (extended):** whether INVENTORY reverses a `CUSTOMER_RETURN` out of `QUARANTINE` with the §34.2 body. R34-16 /
   R34-17 follow the pack; a different answer means a new pack revision.
2. **MVP-1 CT R-2:** `ALREADY_REVERSED` on the late REVERSAL is not named in §34.2; the plan treats it like MOD-0183
   (counted as reversed, integrity fault). Confirm with the R-2 answer.
3. **Pack header text:** §34's opening note still says "Draft revision — not approved" while `status` is `ready-for-dev`
   (same as MOD-0183 §23). Fix in the next MOD-0186 revision, not while #262 is under approval.
4. **Permission grant for tests and tours:** the receiving role needs `inventory.movements.create` (Roles → Permissions,
   BL-652 interim); the lane tour's minted token must carry it.
