# MOD-0173 — INVENTORY-BUNDLE v1 API (as implemented, Slice S1)

> **Authority:** the frozen contract `inventory-bundle.openapi.yaml` (INVENTORY-BUNDLE 1.0.0, `x-status: FROZEN`,
> `x-owner: MOD-0173`) and the MOD-0173 module pack (`ready-for-dev`). This page explains how
> `Diten.InventoryService` (port 5066) implements that contract today. It does not redefine it — where the two
> differ, the contract and the pack win.
>
> **Slice:** S1 — Ledger core. `/reservations*` (S2) and valuation (S3) are not served yet.

## 1. Surface

| Operation | Method + path | Permission | Success |
|---|---|---|---|
| `postMovement` | `POST /api/inventory/movements` | `supplychain.inventory.movements.post` | 201 new · 200 idempotent replay |
| `getAvailability` | `GET /api/inventory/availability` | `supplychain.inventory.read` | 200 `AvailabilityView` |
| `getBalance` | `GET /api/inventory/balance` | `supplychain.inventory.read` | 200 `{ rows, total, contractVersion }` |
| `getTransactions` | `GET /api/inventory/transactions` | `supplychain.inventory.read` | 200 `{ items, page, pageSize, total, contractVersion }` |

- **Wire format** — the unwrapped contract schemas (pack §8 exception to `Response<T>`). Every body carries
  `contractVersion: "v1"`. Decimals are strings with at least three fraction digits (`"100.000"`); timestamps are UTC
  ISO-8601 (`"2026-09-30T10:00:00.000Z"`). Members that are null are **omitted**, not written as `null`.
- **Errors** — the contract `Error` schema: `{ error: { code, message, details?, correlationId }, contractVersion }`.
- **Gateway** — the `/api/inventory/**` route is an INT-1 deliverable (integration-agent); headers to propagate:
  `Authorization`, `X-Tenant-Id`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match`.

## 2. Scope, identity and correlation (every endpoint)

| Input | Rule | Refusal |
|---|---|---|
| Bearer JWT | issuer / audience / signature / lifetime from configuration | 401 (no token, bad signature, expired) |
| `tenant_id`, `legal_entity_id`, `sub` claims | exactly one UUID each; they ARE the scope | 403 |
| `X-Tenant-Id` header | optional; must equal `tenant_id` | 403 |
| `X-Legal-Entity-Id` header (`LegalEntityOverride`) | optional; must equal `legal_entity_id` — multi-LE override not granted in S1 | 403 `CROSS_LE_FORBIDDEN` |
| `tenantId` / `legalEntityId` in query or body | never accepted | 400 `INVALID_REQUEST` |
| permission claim | `.read` for GET, `.movements.post` for POST | 403 |
| `X-Correlation-Id` | optional; if sent must be one UUID, else minted by the server | 400 `INVALID_REQUEST` |

The correlation id is echoed in the `X-Correlation-Id` response header of every response and in
`error.correlationId`; a posting persists it on the transaction, the audit record and the outbox event.

401 and the permission 403 carry the `Error` body **without** `error.code` — the contract declares no code for them.

## 3. POST /movements

### 3.1 Request checks (400, nothing written)

| Field | Rule | Code |
|---|---|---|
| `Idempotency-Key` header + body `idempotencyKey` | both present, equal after trim, 1–128 chars | `INVALID_REQUEST` |
| `movementType` | one of the 15 canonical values, exact spelling | `INVALID_MOVEMENT_TYPE` |
| `itemId`, `skuId` | UUID | `INVALID_REQUEST` |
| `skuLevel` | `Gsku` \| `Lsku` \| `FinishedGood` | `INVALID_REQUEST` |
| `quantity` | JSON **string**, `^\d+(\.\d{1,6})?$`, > 0 (no sign, exponent, grouping) | `INVALID_REQUEST` |
| `uomId`, `sourceModule`, `sourceType` | required, ≤ 200 chars | `INVALID_REQUEST` |
| `fromStockStatus` / `toStockStatus` | a `StockStatus` value when sent | `INVALID_REQUEST` |
| `transactionDate`, `postingDate` | ISO-8601 date-time with `Z` or `+00:00` | `INVALID_REQUEST` |
| sides | direction matrix below | `INVALID_REQUEST` |

### 3.2 Direction matrix (pack §12)

| Class | Types | from* | to* |
|---|---|---|---|
| Inbound | OPENING_BALANCE, GOODS_RECEIPT_PO, GOODS_RECEIPT_PROD, CUSTOMER_RETURN, COUNT_GAIN | must be omitted | warehouse + location required; status default AVAILABLE |
| Outbound | GOODS_ISSUE_SALES, GOODS_ISSUE_PROD, SUPPLIER_RETURN, COUNT_LOSS, SCRAP, WRITE_OFF | warehouse + location required; status default AVAILABLE | must be omitted |
| Warehouse transfer | WAREHOUSE_TRANSFER | required | required, other warehouse, same status (single-step) |
| Location transfer | LOCATION_TRANSFER | required | required, same warehouse, other location, same status |
| Status transfer | STATUS_TRANSFER | required | same warehouse + location, different status |
| Reversal | REVERSAL | derived from the original | derived from the original |

"from*" / "to*" include the side's stock status.

### 3.3 Master-data checks (422, nothing written)

In order: item + sku exist in PRODUCT-MASTER (`UNKNOWN_ITEM`) → `sku.itemId == itemId` (`SKU_ITEM_MISMATCH`) →
the posted UoM converts to the SKU base UoM through declared conversions, chains allowed, exact to 6 fraction
digits (`UOM_CONVERSION_MISSING`) → `lotId` when the item is lot-controlled (`LOT_REQUIRED`) → distinct `serialIds`
= base quantity when serial-controlled (`SERIAL_COUNT_MISMATCH`) → every named location exists, is Active and
stockable, belongs to the caller's legal entity and sits under the named warehouse (`UNKNOWN_LOCATION`).

PRODUCT-MASTER unreachable / timing out / answering outside its contract → **503 `PRODUCT_MASTER_UNAVAILABLE`**;
LOCATION → **503 `LOCATION_MASTER_UNAVAILABLE`**. Nothing is written in either case.

### 3.4 Posting

One MongoDB multi-document transaction: next `INV-{yyyy}-{0000000}` number (per Tenant + LE + posting year) →
balance legs (decreases first, each only if the row covers it: `Available ≥ q` on an AVAILABLE row, `OnHand ≥ q`
otherwise) → transaction insert (with the resulting balance snapshot) → audit record → `InventoryMovementPosted`
outbox event with status `Pending`. An uncovered leg aborts everything: **422 `INSUFFICIENT_STOCK`** with
`details.available` / `details.requested`. Concurrent postings on the same row are serialised by MongoDB; the loser
re-evaluates the coverage condition, so balance 100 with two parallel issues of 60 ends at 40, never −20.

### 3.5 Idempotency

| Situation | Answer |
|---|---|
| New key | 201, `idempotentReplay: false` |
| Same key, same canonical payload | **200**, `idempotentReplay: true`, the ORIGINAL transaction and balance snapshot; no write, no upstream call |
| Same key, different payload | **400 `IDEMPOTENCY_KEY_REUSED`**, no write |
| Same key racing in parallel | exactly one commits; the others answer as above |

"Canonical" compares typed values: `"100"` and `"100.000"` are the same quantity; an omitted status and its default
are the same status. Keys are scoped per Tenant + LE.

### 3.6 Reversal (ASSUMPTION-0173-05)

Post `movementType: REVERSAL` with `sourceModule: MOD-0173`, `sourceType: REVERSAL`,
`sourceDocumentId: <original transactionId>`, the original's item / sku / skuLevel / quantity / uomId, and **no**
sides, lot or serials (they are copied from the original). The server posts the inverse legs as a new transaction.

| Case | Answer |
|---|---|
| Envelope wrong, original not found in this Tenant + LE, fields differ, or the original is itself a reversal | 400 `INVALID_REQUEST` |
| Original already reversed (also under a race) | 422 `ALREADY_REVERSED` |
| Inverse leg not covered (stock already consumed) | 422 `INSUFFICIENT_STOCK` |

The original transaction document is never modified.

## 4. Reads

- **GET /availability** — `itemId` or `skuId` required; optional `skuLevel`, `warehouseId`, `locationId` (echoed in
  `scope`). Sums the caller's balance rows: `onHand`, `reserved`, `available`, and `byStatus` with OnHand for **every**
  StockStatus (zeros included). An item/sku with no ledger row in this Tenant + LE → **404 `UNKNOWN_ITEM`** (no
  upstream call on the read path).
- **GET /balance** — filters `itemId`, `skuId`, `skuLevel`, `warehouseId`, `locationId`, `lotId`, `stockStatus`;
  `page ≥ 1`, `1 ≤ pageSize ≤ 200` (default 1 / 50). Row buckets follow the pack §4 rule
  (QI / BLOCKED+QUARANTINE+DAMAGED+EXPIRED / IN_TRANSIT / AVAILABLE).
- **GET /transactions** — filters `itemId`, `lotId`, `locationId` (either side), `from` / `to` (`yyyy-MM-dd`, UTC
  day of `postingDate`, both inclusive); newest posting first; same paging rules.

Malformed read parameters answer 400 `INVALID_REQUEST` (pack §12), in addition to the statuses the contract
declares for these operations.

## 5. Persistence (MongoDB, replica set required)

| Collection | Content | Key indexes |
|---|---|---|
| `inventory_transactions` | append-only facts | unique (Tenant, LE, IdempotencyKey) · unique (Tenant, LE, TransactionNumber) · unique partial (Tenant, LE, ReversesTransactionId) |
| `inventory_balances` | the only balance projection | unique full dimension key (Tenant, LE, Item, Sku, SkuLevel, Warehouse, Location, Lot, StockStatus, Owner, Project) |
| `inventory_audit` | one record per posting | (Tenant, LE, TransactionId) · (Tenant, LE, CorrelationId) |
| `inventory_outbox` | `InventoryMovementPosted`, `Pending` (no transport in S1) | (Tenant, LE, Status, OccurredAt) |
| `inventory_sequences` | transaction-number counters | `_id` |

The event payload is internal (ASSUMPTION-0173-08); consumers must not depend on it until an additive contract
publishes it.
