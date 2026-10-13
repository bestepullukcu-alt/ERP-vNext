# TRACE-BUNDLE v1.1 — additive proposal (MOD-0174)

> **Status: PROPOSAL (draft) — not a contract.** The frozen contract `docs/analysis/contracts/trace.openapi.yaml`
> (TRACE-BUNDLE **v1.0.0**, `x-status: FROZEN`, `x-owner: MOD-0174`) is **not edited** by this document. If the CT accepts
> this proposal, the MOD-0174 owner writes it into the yaml as ONE single-writer step inside slice **P1** (same method as
> LOCATION 1.1.0, `location.openapi.yaml` `x-changelog`), with `info.version: 1.1.0` and `contractVersion` staying
> `"v1"` on every body.
>
> Source: WP-0174-P0 (CT-MVP1-0174-P0 v1), owner decisions K-A…K-G (2026-10-05), the approved mockup
> "Lot Seri Takibi v2 (offline).html", module pack `execution/domains/supply-chain-execution/module-packs/MOD-0174-lot-serial-tracking.md`.

## 1. Rules of this proposal

1. **Additive only.** No v1 path, method, parameter, schema property, enum value or response code is removed, renamed,
   retyped or made required (§9 check table).
2. New schema properties are **optional**; new query parameters are **optional**; a v1 caller that sends none of them
   gets the v1 behaviour — except the two **behaviour changes** of §8, which are listed for an explicit CT decision.
3. Every body keeps `contractVersion: "v1"`. Errors use the v1 `Error` schema; new `error.code` values are values of the
   existing free string (as MOD-0173 ASSUMPTION-0173-04) and use 400 / 401 / 403 / 404 / 409 / 422 / 503; a stale
   `If-Match` is 409 (LOCATION 1.1 precedent), never 412.
4. Scope as v1 (`Tenant+LegalEntity scoped` request scope). **Lot identity visibility** (tenant-wide vs per company) is an
   open CT decision — pack §19 CT-D3; the proposal is written for the recommended tenant-wide identity.
5. Permissions use the new namespace `trace.*` (pack §14). v1 operations get their permission here too (v1 named none).

## 2. Schema additions

### 2.1 `LotRef` — new optional properties

| Property | Type | Why |
|---|---|---|
| `skuId` | string uuid | K-G — the lot belongs to one SKU |
| `skuLevel` | string enum `Gsku · Lsku · FinishedGood` (same values as INVENTORY-BUNDLE `SkuLevel`) | K-G |
| `version` | integer | optimistic concurrency; equals the `ETag` `W/"<version>"` of the owner reads |
| `legalEntityId` | string | the company that created the lot (audit; see CT-D3) |
| `stockSummary` | `LotStockSummary` (§2.2), only on `listLots` / `searchLots` items when `includeStock=true` and on `getLotDetail` | K-A |
| `expiryState` | string enum `None · Ok · ExpiringSoon · Expired` | computed: `Expired` when `expiryDate < today`, `ExpiringSoon` when ≤ 90 days |
| `retestDue` | boolean | computed: `retestDate < today` (K-F; separate icon in UI) |
| `trackingMode` | string enum `Lot · LotAndSerial` | from PRODUCT-MASTER `lotControlled` / `serialControlled` at creation |

`itemId` stays **always filled** (K-G rule 2): resolved from the SKU through PRODUCT-MASTER (`resolveSkuProduct`).

### 2.2 `LotStockSummary` (K-A)

`{ onHand: Decimal-string, baseUomId: string, legalEntityId: string, asOf: date-time }` — read from the ledger's per-lot
projection (`inventory_lot_positions`, owned by the MOD-0173 ledger code, pack §8), **never** computed by summing balance
rows per request. `onHand` = all stock statuses of the lot in the caller's company, base UoM.

### 2.3 `LotChangeEntry` (history)

`{ entryId, lotId, action: Created · Updated · StatusChanged · Linked · Migrated, field: string?, oldValue: string?,
newValue: string?, reason: string?, actorUserId, actorDisplay, occurredAt, correlationId, source: string }`
— `source` = `lot-form · ledger-shortcut · migration · system-expiry · api`.

### 2.4 `SerialListItem`

`SerialRef` (unchanged) **plus** optional `skuId`, `skuLevel`, `lotNumber`, `warehouseId`, `locationId`,
`lastTransactionId`, `lastMovementAt`.

### 2.5 `GenealogyLinkRequest` / `GenealogyLink`

Request: `{ idempotencyKey (req), parentLotId (req), childLotId (req), transformationReference (req, 1–60),
sourceModule (req, e.g. MOD-0194), quantityConsumed: Decimal-string?, uomId?: string }`.
Response: `{ linkId, parentLotId, childLotId, transformationReference, sourceModule, createdAt, contractVersion }`.

### 2.6 Migration (W-UI-02 retirement) — `ManualLotGroup`, `MigrationDecision`, `MigrationResult`

- `ManualLotGroup`: `{ groupId, itemId, skuId, skuLevel, typedText, transactionCount, onHand: Decimal-string,
  baseUomId, suggestion: { kind: ExactLot · SimilarLot · SimilarGroup · None, lotId?, groupId?, note } }`.
- `MigrationDecision`: `{ groupId, kind: LinkExisting · Typo · NewLot, lotId? (LinkExisting / Typo→lot),
  targetGroupId? (Typo→group), newLot?: LotFields (NewLot: lotNumber + the optional §2.7 fields) }`.
- `MigrationResult`: `{ migrationId, completedAt, completedBy, transactionsLinked (X), lotsLinked (Y), lotsCreated,
  contractVersion }`.

### 2.7 `LotFields` (create / edit bodies)

`manufacturerLot, supplierLot` (string 1–40, nullable), `manufacturingDate, expiryDate, retestDate` (date-time,
nullable; v1 format kept), `countryOfOrigin` (ISO 3166-1 alpha-2, nullable — the v1 example is `TR`),
`coaReference` (string 1–60, nullable).

## 3. Changed-by-addition v1 operations

| Op (v1) | Addition | Permission |
|---|---|---|
| `GET /lots/{lotId}` `getLot` | response gains the optional §2.1 properties; `ETag` header | `trace.lots.read` |
| `GET /lots` `listLots` | optional query: `q` (lot no / manufacturer lot / supplier lot / product name or code, contains, case-insensitive), `skuId`, `skuLevel`, `warn` (bool: expired ∪ ≤ 90 days ∪ retest due), `includeStock` (bool), `page`, `pageSize` (≤ 200), `sort` (`expiryDate · lotNumber · createdAt`, `-` = desc). Without any of them = v1 behaviour. Response may carry optional `page`, `pageSize`. | `trace.lots.read` |
| `POST /lots` `createLot` | optional body `skuId`, `skuLevel` (**see behaviour change B-2**), `coaReference`; response `LotRef` with `skuId/skuLevel`; status always `Draft`. New codes: `LOT_SKU_REQUIRED` (422), `LOT_NUMBER_INVALID` (400), `LOT_DATES_INVALID` (400), `PRODUCT_NOT_LOT_CONTROLLED` (422), `PRODUCT_MASTER_UNAVAILABLE` (503). `LOT_ALREADY_EXISTS` (409) keeps its code, **scope changes (B-1)**. | `trace.lots.create` |
| `GET /serials/{serialId}` `getSerial` | response gains §2.4 optional properties | `trace.serials.read` |
| `GET /lots/{lotId}/genealogy` | parents/children items gain optional `linkId`, `sourceModule`, `createdAt`; query `depth` (1–2, default 1) | `trace.lots.read` |
| `POST /validate` | results gain optional `status`, `skuId`, `skuLevel`, `usable` (false for Blocked / Expired) | `trace.lots.read` |

## 4. New operations

All new writes take `Idempotency-Key` (header = body `idempotencyKey`, MOD-0173 rule) unless marked "ETag", and return
`ETag: W/"<version>"` on lot reads/writes. Stale `If-Match` → 409 `LOT_CONFLICT`; missing → 400 `INVALID_REQUEST`.

### 4.1 `PATCH /lots/{lotId}` — `updateLot` (K-C)

- Headers: `If-Match` (req). Body: any subset of `LotFields` + `reason` (string).
- `lotNumber`, `itemId`, `skuId`, `skuLevel`, `status` in the body → 400 `LOT_KEY_IMMUTABLE` (Lot No and SKU are locked
  even in Draft — owner).
- Draft lot: `reason` optional; permission `trace.lots.update`.
- Non-Draft lot (Released / Blocked / Expired — pack CT-D8): `reason` **required, ≥ 10 characters
  after trim** → else 400 `REASON_REQUIRED`; permission `trace.lots.update-released` (K-C: separate key).
- No effective change → 422 `LOT_NO_CHANGES`. Dates invalid → 400 `LOT_DATES_INVALID`.
- One `LotChangeEntry` per changed field (old → new, reason), same transaction.
- 200 → `LotRef` (with `version`). Errors: 400, 403, 404 `UNKNOWN_LOT`, 409 `LOT_CONFLICT`, 422.

### 4.2 `POST /lots/{lotId}/status` — `changeLotStatus` (K-E)

- Headers: `If-Match` (req), `Idempotency-Key` (req). Body: `{ idempotencyKey, toStatus, reason, acknowledgeImpact }`.
- Allowed: `Draft → Released`, `Released → Blocked`, `Blocked → Released`. `Expired` is **system-only** (target or source)
  → 422 `LOT_STATUS_TRANSITION_INVALID`. Same status → 422 `LOT_STATUS_TRANSITION_INVALID`.
- `reason` ≥ 10 chars → else 400 `REASON_REQUIRED`. `toStatus = Blocked` requires `acknowledgeImpact: true` → else 400
  `ACKNOWLEDGEMENT_REQUIRED`.
- **Extension point (0175):** before the change the server asks `ILotStatusChangeGate`; in MOD-0174 the only
  implementation returns `Allow`. When MOD-0175 adds approval / e-signature it may return `PendingApproval` → 202 with
  `{ lotId, pendingRequestId, contractVersion }` (declared now, unused in v1.1 runtime) — no e-signature is built here.
- 200 → `LotRef`. Permission `trace.lots.change-status`.

### 4.3 `GET /lots/{lotId}/history` — `getLotHistory`

Query `page`, `pageSize` (≤ 200). Newest first. `{ items: LotChangeEntry[], total, page, pageSize, contractVersion }`.
Append-only (no delete/update route). Permission `trace.lots.read`.

### 4.4 `GET /lots/{lotId}/detail` — `getLotDetail`

`LotRef` with `stockSummary`, `expiryState`, `retestDue`, `trackingMode`, `source` (`{ kind: GoodsReceipt ·
ProductionReceipt · OpeningBalance · Manual · Migration, documentNumber?, partyName?, date? }` — from the first inbound
ledger transaction of the lot) and `serialCount`. One call for the Lot Detail header. Permission `trace.lots.read`.
(Stock distribution and movement history stay INVENTORY-BUNDLE v1 `getBalance?lotId=` / `getTransactions?lotId=`.)

### 4.5 `GET /serials` — `listSerials`

Query: `q` (serial number contains), `skuId`, `skuLevel`, `lotId`, `status` (`Active · Consumed · Blocked`), `page`,
`pageSize` (≤ 200). `{ items: SerialListItem[], total, page, pageSize, contractVersion }`. Permission
`trace.serials.read`.

### 4.6 `POST /serials/gs1-check` — `checkGs1Code` (K-B)

- Body: `{ code (raw scan text), skuId, skuLevel, lotId }`. Response 200: `{ serialNumber, gtin?, lotNumber?, expiry?,
  checks: [{ field: Gtin · Lot · Expiry · Format, result: Ok · Mismatch · Missing · Skipped, expected?, actual? }],
  accepted: bool, gtinSkippedReason?: "PRODUCT_MASTER_GTIN_NULL", contractVersion }`.
- AIs parsed: (01) GTIN-14, (21) serial, (17) expiry `YYMMDD`, (10) lot; both bracketed text and raw GS1 with FNC1/GS
  separators accepted.
- `accepted = Format Ok ∧ Lot Ok ∧ Expiry Ok ∧ (Gtin Ok ∨ Gtin Skipped)`. **GTIN is skipped only** when PRODUCT-MASTER
  returns `gtin: null` for the SKU (MOD-0290 D-1); lot and expiry are checked **always**. `(17)` compares to the lot's
  `expiryDate` (day precision; `DD = 00` = last day of the month, GS1 rule).
- Read-only (nothing stored). Errors 400 (`GS1_UNREADABLE`), 404 `UNKNOWN_LOT`, 503 `PRODUCT_MASTER_UNAVAILABLE`.
  Permission `trace.lots.read`.

### 4.7 `POST /lots/{lotId}/genealogy` — `linkGenealogy` (writer = MOD-0194 later)

- Body `GenealogyLinkRequest` (`lotId` in path must equal `childLotId`). Both lots exist in the tenant → else 404
  `UNKNOWN_LOT`; parent ≠ child → 422 `GENEALOGY_SELF_LINK`; link that would close a cycle → 422 `GENEALOGY_CYCLE`;
  same parent+child+transformationReference again → 200 replay (idempotent).
- 201 → `GenealogyLink`. Permission `trace.genealogy.link` (API-only → manifest `ApiPermissions`, standard §2d).

### 4.8 Migration of typed lots (W-UI-02) — `/migration/manual-lots`

| Op | Route | Body / response | Errors | Permission |
|---|---|---|---|---|
| `listManualLotGroups` | `GET /migration/manual-lots` (`view=all·pending`, `page`, `pageSize`) | `{ items: ManualLotGroup[], total, totals: { groups, transactions }, completed: MigrationResult?, contractVersion }` | 503 `PRODUCT_MASTER_UNAVAILABLE` | `trace.lots.migrate` |
| `commitManualLotMigration` | `POST /migration/manual-lots/commit` + `Idempotency-Key` | `{ idempotencyKey, reason (≥ 10), expectedGroupCount, decisions: MigrationDecision[] }` → 200 `MigrationResult` | 400 `REASON_REQUIRED` · 409 `MIGRATION_ALREADY_COMPLETED` · 409 `MIGRATION_STALE` (a group appeared / changed since the list, or `expectedGroupCount` ≠ server) · 422 `MIGRATION_DECISIONS_INCOMPLETE` (a group without a decision) · 422 `MIGRATION_TYPO_UNRESOLVED` (typo chain > 3 or to an undecided group) · 409 `LOT_ALREADY_EXISTS` (a NewLot collides) | `trace.lots.migrate` |
| `getMovementLotLinks` | `GET /migration/links?transactionId=` or `?lotId=` | `{ items: [{ transactionId, typedText, lotId, migrationId }], contractVersion }` | — | `trace.lots.read` |

Commit is one Mongo transaction: create NewLot lots (Draft), write one `MovementLotLink` per linked transaction,
re-key the affected balance / lot-position projection rows (pack §8 "migration re-key"), write history rows
(`Migrated` / `Linked`), set the tenant flag `ManualLotEntryClosed = true`. **No `inventory_transactions` document is
changed** (append-only).

## 5. Error codes introduced (values of the v1 `error.code` string)

`LOT_SKU_REQUIRED`, `LOT_SKU_MISMATCH`, `LOT_NUMBER_INVALID`, `LOT_DATES_INVALID`, `LOT_KEY_IMMUTABLE`,
`LOT_NO_CHANGES`, `LOT_CONFLICT`, `LOT_STATUS_TRANSITION_INVALID`, `REASON_REQUIRED`, `ACKNOWLEDGEMENT_REQUIRED`,
`PRODUCT_NOT_LOT_CONTROLLED`, `GS1_UNREADABLE`, `GENEALOGY_SELF_LINK`, `GENEALOGY_CYCLE`,
`MIGRATION_ALREADY_COMPLETED`, `MIGRATION_STALE`, `MIGRATION_DECISIONS_INCOMPLETE`, `MIGRATION_TYPO_UNRESOLVED`,
`PRODUCT_MASTER_UNAVAILABLE`, `CROSS_LE_FORBIDDEN`, `INVALID_REQUEST`. Unchanged v1 codes: `UNKNOWN_LOT`,
`LOT_ALREADY_EXISTS`.

Codes raised by the **ledger** (INVENTORY-BUNDLE `postMovement`, MOD-0173 code changed in slice P2 — also values of the
frozen `error.code` string, 422): `UNKNOWN_LOT`, `LOT_SKU_MISMATCH`, `LOT_BLOCKED`, `LOT_EXPIRED`, `LOT_NOT_ALLOWED`,
`SERIAL_ALREADY_EXISTS`, `SERIAL_NOT_AVAILABLE`, `SERIAL_LOT_MISMATCH` (existing `LOT_REQUIRED`,
`SERIAL_COUNT_MISMATCH` unchanged).

## 6. Permissions (namespace `trace.*`, owner MOD-0174 — PKS-001 §4)

| Key | Tier | Operations |
|---|---|---|
| `trace.lots.read` | 1 | getLot, listLots, getLotDetail, getLotHistory, genealogy GET, validate, gs1-check, migration links |
| `trace.lots.create` | 1 | createLot (screen "Yeni Lot" + ledger shortcut + GRN API) |
| `trace.lots.update` | 1 | updateLot on a **Draft** lot |
| `trace.lots.update-released` | 3 | updateLot on a non-Draft lot (K-C) |
| `trace.lots.change-status` | 3 | changeLotStatus (K-E) |
| `trace.lots.migrate` | 3 | migration list + commit (admin, one-off) |
| `trace.serials.read` | 1 | getSerial, listSerials |
| `trace.genealogy.link` | 3 | linkGenealogy — API-only (`ApiPermissions`) |

## 7. Non-functional

- Every write: audit (history row) + correlation id (MOD-0173 rule) in the same transaction as the change.
- `Expired` is set by a system sweeper (pack §8) and is also enforced by date at posting time, so a lagging sweeper
  never lets an expired lot through.

## 8. Behaviour changes for v1 consumers (CT records separately)

| # | v1 statement | v1.1 behaviour | Effect on a v1 consumer |
|---|---|---|---|
| **B-1** (K-G) | `info.description`: "Lot uniqueness = surrogate LotId + business key **Tenant+Item+LotNumber** (DEC-INV-14)"; `LotRef.lotNumber` description "business key: Tenant+Item+LotNumber"; 409 `LotConflict` "Aynı Tenant+Item+LotNumber zaten var" | Business key = **Tenant + SKU (`skuId` + `skuLevel`) + LotNumber** (owner change of DEC-INV-14, 2026-10-05) | The same `lotNumber` may now exist **twice under one `itemId`** (two SKUs of the same product). `listLots(itemId=…)` can return two lots with the same `lotNumber`; a consumer that used `itemId + lotNumber` as a unique key must use `lotId` (always unique) or `skuId + skuLevel + lotNumber`. `LOT_ALREADY_EXISTS` is now raised for the same SKU only. |
| **B-2** (K-G) | `CreateLotRequest.required: [idempotencyKey, itemId, lotNumber]` | A lot cannot exist without a SKU: a create **without** `skuId`+`skuLevel` → 422 `LOT_SKU_REQUIRED` (proposal) | A v1-shaped `createLot` call (only `itemId`) is refused. No live v1 caller exists (measured: no HTTP call to `/api/trace` in `services/`, `frontend/`, `gateway/`). The planned caller MOD-0142 GRN runs today on a mock (`services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Common/IInventoryPostingClient.cs:62`) and its GRN line already holds `SkuId` + `SkuLevel` (`services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/Entities/GoodsReceipt.cs:50,52`, next to `LotNumber` :55), so it can send them. **CT decision CT-D2** — alternative: accept and bind to the item's single SKU when exactly one exists. |
| B-3 | `status` enum has `Expired`; v1 says nothing about how it is set | `Expired` is set **automatically** (expiry date passed); it can never be set or left by a caller | Consumers never see a manual Expired; a lot may turn Expired between two reads. |

## 9. Check table — nothing of v1 removed or changed

| v1 element | v1.1 |
|---|---|
| `servers: /api/trace`, `bearerAuth` | unchanged |
| `GET /lots/{lotId}` (path param, 200 `LotRef`, 404 `UnknownLot`) | unchanged; optional props + `ETag` header added |
| `GET /lots` params `itemId`, `status`, `expiryBefore`; 200 `{items,total,contractVersion}` | unchanged; optional params + optional `page`/`pageSize` added |
| `POST /lots` (`Idempotency-Key` required, `CreateLotRequest`, 201 `LotRef`, 409 `LotConflict`) | unchanged shape; optional body props added; **semantics B-1, B-2** |
| `GET /serials/{serialId}` | unchanged; optional props added |
| `GET /lots/{lotId}/genealogy` (`parents[].parentLotId/transformationReference`, `children[].childLotId/…`) | unchanged; optional props + optional `depth` |
| `POST /validate` (`lotIds`, `serialIds` → `results[].lotId/exists`) | unchanged; optional result props |
| `LotRef` 12 properties incl. `status` enum `[Draft, Released, Blocked, Expired]` | all kept, same types, enum unchanged |
| `SerialRef` 6 properties, `status` enum `[Active, Consumed, Blocked]` | all kept, enum unchanged |
| `CreateLotRequest` required `[idempotencyKey, itemId, lotNumber]` | required list unchanged (B-2 is a server rule, not a schema change) |
| `Error` schema | unchanged |
| `UnknownLot` / `LotConflict` responses | unchanged codes; `LotConflict` description scope per B-1 |
| `IdempotencyKey` parameter | unchanged |

Removed: **nothing**. Renamed: **nothing**. Retyped: **nothing**. New required v1 field: **none**.
