# MOD-0173 — INVENTORY-BUNDLE v1 API (as implemented, Slices S1 + S2)

> **Authority:** the frozen contract `inventory-bundle.openapi.yaml` (INVENTORY-BUNDLE 1.0.0, `x-status: FROZEN`,
> `x-owner: MOD-0173`) and the MOD-0173 module pack (`ready-for-dev`). This page explains how
> `Diten.InventoryService` (port 5066) implements that contract today. It does not redefine it — where the two
> differ, the contract and the pack win.
>
> **Slices:** S1 — Ledger core · S2 — Reservations (+ FU-S1-01, FU-S1-02). Valuation (S3) is not served yet.

## 1. Surface

| Operation | Method + path | Permission | Success |
|---|---|---|---|
| `postMovement` | `POST /api/inventory/movements` | `inventory.movements.create` | 201 new · 200 idempotent replay |
| `getAvailability` | `GET /api/inventory/availability` | `inventory.stock.read` | 200 `AvailabilityView` |
| `getBalance` | `GET /api/inventory/balance` | `inventory.stock.read` | 200 `{ rows, total, contractVersion }` |
| `getTransactions` | `GET /api/inventory/transactions` | `inventory.stock.read` | 200 `{ items, page, pageSize, total, contractVersion }` |
| `createReservation` | `POST /api/inventory/reservations` | `inventory.reservations.manage` | 201 `ReservationResponse` · 200 idempotent replay |
| `releaseReservation` | `POST /api/inventory/reservations/{id}/release` | `inventory.reservations.manage` | 200 `{ reservationId, status, availableAfter, contractVersion }` |

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
| `tenant_id`, `sub` claims | exactly one UUID each; they ARE the tenant and user scope | 403 |
| `legal_entity_id` claim | at most one UUID; when present it IS the legal-entity scope (audit `LeSource = token`) | 403 (malformed) |
| `X-Tenant-Id` header | optional; must equal `tenant_id` | 403 |
| `X-Legal-Entity-Id` header (`LegalEntityOverride`), token **with** the claim | optional; must equal `legal_entity_id` | 403 `CROSS_LE_FORBIDDEN` |
| `X-Legal-Entity-Id` header, token **without** the claim (W-0173-02 company selector) | **required**, one UUID; verified with MDM `GET /api/legal-entities/{id}/lookup-validation` through the Gateway with the caller's token = an ACTIVE legal entity of the caller's tenant (positive answer cached ≤ 60 s per tenant + LE + token); audit `LeSource = header-unverified-membership` (user↔LE membership is not checked — accepted risk) | absent/invalid → 403 "select a company" · unknown / inactive / other tenant / MDM refuses the caller → 403 `CROSS_LE_FORBIDDEN` · MDM unreachable / 5xx / timeout → 503 `LEGAL_ENTITY_MASTER_UNAVAILABLE` (nothing read or written) |
| `tenantId` / `legalEntityId` in query or body | never accepted | 400 `INVALID_REQUEST` |
| permission claim | `inventory.stock.read` for GET, `inventory.movements.create` for POST /movements, `inventory.reservations.manage` for both reservation operations — no role shortcut (FU-S1-02: a `SuperAdmin` role without the permission is 403) | 403 |
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

**S2 step, same transaction:** the posting's NET AVAILABLE delta moves the SKU position (`inventory_sku_positions`).
A transfer between two AVAILABLE rows nets 0 and touches nothing (reservations are not located). On a net decrease:
(1) the SKU's due reservations expire, (2) an ACTIVE reservation with the same `(sourceModule, sourceDocumentId,
sourceLineId)` is consumed oldest-first (partial → stays ACTIVE with the remainder, full → RELEASED), (3) only then is
`AvailableOnHand − Reserved ≥ 0` enforced — otherwise **422 `INSUFFICIENT_STOCK`** with `details.available` = the
SKU's unreserved stock. Reserved stock can be issued by its holder, never by anyone else.

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

## 4. Reservations (S2 — pack ASSUMPTION-0173-12..17)

A reservation holds quantity of AVAILABLE stock at **Tenant + LE + itemId + skuId + skuLevel** — not a bin, lot or
status (the frozen request carries none; row allocation belongs to MOD-0176 FEFO / MOD-0178 WMS). Quantities are in
the SKU base unit (the request has no UoM; `baseQuantity`, when sent, must equal `quantity`).

**POST /reservations** — `Idempotency-Key` header = body `idempotencyKey` (same rules as movements). Body: `itemId`,
`skuId` (UUIDs), `skuLevel`, `quantity`, `reservationType`, `sourceModule` (required); `sourceDocumentId`,
`sourceLineId`, `priority` (integer), `expiresAt` (UTC, in the future) optional.

| Situation | Answer |
|---|---|
| `AvailableOnHand − Reserved ≥ q` at SKU level | **201** `{ reservationId, status: ACTIVE, reservedQuantity, availableAfter, etag: W/"1", contractVersion }` |
| not covered (incl. a SKU without stock) | **422 `INSUFFICIENT_AVAILABLE`**, `details.available` / `details.requested`, nothing written |
| same key, same payload | **200**, the original result |
| same key, different payload | **400 `IDEMPOTENCY_KEY_REUSED`** |
| malformed body / headers | **400 `INVALID_REQUEST`** |

Two parallel reserves of 60 on 100: one 201, one 422; Reserved is 60, never 120.

**POST /reservations/{id}/release** — `If-Match: W/"<version>"` required.

| Situation | Answer |
|---|---|
| ACTIVE and the ETag matches | **200** `{ reservationId, status: RELEASED, availableAfter, contractVersion }` |
| `If-Match` missing | **400 `INVALID_REQUEST`** |
| id unknown, malformed or of another Tenant / LE | **404** (Error body without code) |
| stale ETag, or no longer ACTIVE (released, consumed, expired) | **409 `RESERVATION_CONFLICT`** |

The version (and so the ETag) changes on every change of the reservation, including a partial consumption.

**Expiry** — `expiresAt ≤ now` ⇒ EXPIRED: inside every create / release / decrease transaction of the SKU, and by a
periodic sweeper (`Inventory:Reservations:SweepIntervalSeconds`, default 60). Reads already ignore due reservations.

**Audit / outbox** — every create, release, expiry and consumption writes an audit record (`inventory_audit`, action
`InventoryReservationCreated|Released|Expired|Consumed|PartiallyConsumed`) and a Pending `InventoryReservationChanged`
outbox event (internal payload) with the request's correlation id; the sweeper uses the system actor and one id per
sweep.

## 5. Reads

- **GET /availability** — `itemId` or `skuId` required; optional `skuLevel`, `warehouseId`, `locationId` (echoed in
  `scope`). `onHand` and `byStatus` (OnHand for **every** StockStatus, zeros included) sum the caller's balance rows;
  `reserved` = Σ remaining quantity of the matching SKUs' ACTIVE, not-yet-due reservations; `available` =
  Σ OnHand(AVAILABLE) − `reserved` (floored at 0 when a warehouse / location scope is given — reservations are not
  located). With **no ledger row** in this Tenant + LE, PRODUCT-MASTER decides (FU-S1-01): known item/sku → **200 with
  zeros**; unknown → **404 `UNKNOWN_ITEM`**; unreachable → **503 `PRODUCT_MASTER_UNAVAILABLE`**. With rows, no upstream
  call is made.
- **GET /balance** — filters `itemId`, `skuId`, `skuLevel`, `warehouseId`, `locationId`, `lotId`, `stockStatus`;
  `page ≥ 1`, `1 ≤ pageSize ≤ 200` (default 1 / 50). Row buckets follow the pack §4 rule
  (QI / BLOCKED+QUARANTINE+DAMAGED+EXPIRED / IN_TRANSIT / AVAILABLE).
- **GET /transactions** — filters `itemId`, `lotId`, `locationId` (either side), `from` / `to` (`yyyy-MM-dd`, UTC
  day of `postingDate`, both inclusive); newest posting first; same paging rules.

Malformed read parameters answer 400 `INVALID_REQUEST` (pack §12), in addition to the statuses the contract
declares for these operations.

## 6. Persistence (MongoDB, replica set required)

| Collection | Content | Key indexes |
|---|---|---|
| `inventory_transactions` | append-only facts | unique (Tenant, LE, IdempotencyKey) · unique (Tenant, LE, TransactionNumber) · unique partial (Tenant, LE, ReversesTransactionId) |
| `inventory_balances` | the only balance projection | unique full dimension key (Tenant, LE, Item, Sku, SkuLevel, Warehouse, Location, Lot, StockStatus, Owner, Project) |
| `inventory_audit` | one record per posting and per reservation change | (Tenant, LE, TransactionId) · (Tenant, LE, CorrelationId) · (Tenant, LE, ReservationId) |
| `inventory_outbox` | `InventoryMovementPosted` / `InventoryReservationChanged`, `Pending` (no transport yet) | (Tenant, LE, Status, OccurredAt) |
| `inventory_sequences` | transaction-number counters | `_id` |
| `inventory_reservations` | SKU-level holds (ACTIVE / RELEASED / EXPIRED, Version = ETag) | unique (Tenant, LE, IdempotencyKey) · (Tenant, LE, Item, Sku, SkuLevel, Status, ExpiresAt) · (Tenant, LE, Source…, Status) · system (Status, ExpiresAt) for the sweeper |
| `inventory_sku_positions` | per-SKU projection `AvailableOnHand` / `Reserved` — rebuilt at startup, never a second truth | unique (Tenant, LE, Item, Sku, SkuLevel) |

Both event payloads are internal (ASSUMPTION-0173-08); consumers must not depend on them until an additive contract
publishes it.
