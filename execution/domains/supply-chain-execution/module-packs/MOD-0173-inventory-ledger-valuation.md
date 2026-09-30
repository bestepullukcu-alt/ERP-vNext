---
id: MOD-0173
name: Inventory Ledger & Valuation
domain: supply-chain-execution
service: Diten.InventoryService
shell: none
golden_reference: none
entity_base: EntityBase
status: ready-for-dev
status_note: "ready-for-dev — owner approval (Ali) 2026-09-30, incl. §21 UI-input decisions and OD-5 own service Diten.InventoryService on port 5066. Code starts only after WP-0173-C1 (central records) is CT-accepted. DCP-002 re-run carried as waiver W-0173-01 (§18)."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-09-30
target: 2026-10-31
form_field_count: 0
---

# MOD-0173 — Inventory Ledger & Valuation

> **Identity gate (DCP-002):** canonical ID + name `MOD-0173 · Inventory Ledger & Valuation` is recorded in
> `execution/registries/module-id-registry.md` ("DCP-002 gate OK 2026-09-11") and DCP-009 §20 ("MOD-0173 exit 0,
> proven"). **Not re-measured in this session:** the authoring machine has no Python interpreter, so
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0173 --name "Inventory Ledger & Valuation"`
> could not run. Re-running it with exit 0 is a ready-for-dev checklist item (§18).
>
> **Authority:** this pack > `domain-config.md` > `AGENTS.md` > `.antigravity/`. Planning base:
> `docs/analysis/inventory-capability-scope-and-dependency-report.md` (v2.1, 19 DEC-INV, all RESOLVED).
> Implementation authority for the wire surface: `docs/analysis/contracts/inventory-bundle.openapi.yaml`
> (`INVENTORY-BUNDLE` v1.0.0, `x-status: FROZEN`, `x-owner: MOD-0173`).
>
> **ASSUMPTION-0173-00:** the first slices are backend/contract only (`shell: none`). No UI, form field count or
> Slim/Compact decision is invented here. The G1 E4 evidence bar ("UI + API + persisted + audit") therefore cannot
> close from this pack alone; a tenant-shell UI follow-up (7 languages) is listed in §20.

## 1. Module Summary

MOD-0173 is the **single system of record for stock truth** in ERP-vNext. Every stock change is an append-only,
idempotent, source-traceable `InventoryTransaction`; `InventoryBalance` is a projection of those transactions and is
never edited directly (DEC-INV-02/07/15). Stock is scoped per Tenant + Legal Entity (DEC-INV-18). Valuation is
per Legal Entity + per Product with MOVING_AVERAGE as fallback (DEC-INV-06).

It is the heart of MVP-1 (Inventory Foundation). The critical path is `0173 → 0174 → {0175 ∥ 0176 ∥ 0177}`; the
exit is **G1** (report §20.1). Consumers after G1: MOD-0142 GRN, MOD-0178 WMS, MOD-0181 count, MOD-0172 ATP,
MOD-0189 MRP, MOD-0176 FEFO — all through `INVENTORY-BUNDLE`, never a second balance.

### Slice plan (each slice = its own Work Package, CT-verified before the next)

| Slice | Scope | Contract ops | Closes (G1 §20.1) |
|---|---|---|---|
| **S1 — Ledger core** | Transaction append, balance projection, idempotency, negative-stock guard, StockStatus dimension, reversal, product/location validation via frozen mocks, audit + outbox | `postMovement`, `getAvailability`, `getBalance`, `getTransactions` | opening flow, issue flow, idempotency, negative stock, StockStatus, Tenant+LE isolation, RBAC, audit+correlation, SourceSystem hook |
| **S2 — Reservations** | Physical hold, `Available = OnHand(AVAILABLE) − Reserved`, ETag release, expiry | `createReservation`, `releaseReservation` | reservation box |
| **S3 — Valuation** | `CostingMethodPolicy` (LE × product), MOVING_AVERAGE fallback, cost layers | none frozen yet — needs additive contract v1.1 (single-writer Phase A) first | — (DEC-INV-06; production-ready prerequisite, not a G1 box) |
| **INT-1** | Gateway route `/api/inventory/**` → 5066 + permission seed | — | integration-agent WP |

## 2. Ownership and Boundaries

**Owns (single writer, K15):**

- `INVENTORY-BUNDLE` contract and its implementation (movement / balance / availability / transactions / reservations).
- `InventoryTransaction` (append-only), `InventoryBalance` (projection), `InventoryDimension` key,
  `MovementType` canonical enum (15 values, not user-extensible — DEC-INV-16), `StockStatus` dimension values,
  `InventoryReservation` (S2), valuation objects (S3).
- Inventory audit records and `InventoryMovementPosted` outbox events for its own postings.

**Consumes (frozen, read-only, via HTTP client or Prism mock — never DB/internal types):**

- `PRODUCT-MASTER-BUNDLE` v1 (MOD-0290, MDM, **Model A only**): `validateRefs`, `getProduct`, `getSku`,
  `resolveSkuProduct`, `getSkuUom`. `ItemId → GlobalProduct`, `SkuId + SkuLevel → Gsku|Lsku|FinishedGood` (DEC-INV-17).
- `LOCATION` v1 (shared Location master, DEC-INV-03): `validateLocations`, `getLocation`, `getLocationPath`.
- `MovementReason` codes: MOD-0048 Reference Data (opaque code in S1; validation is a follow-up, §20).
- Backbone: JWT/RBAC (MOD-0018), Event Bus envelope + outbox (Diten.BuildingBlocks), audit (MOD-0021 pattern).

**Does not own / must not do:**

- Product identity, UoM master, Location master, lot/serial master (MOD-0174), quarantine lifecycle (MOD-0175),
  FEFO policy (MOD-0176), ATP/allocation decision (MOD-0172).
- Model B (`Product` / `mdm_products`) — never read, never linked (DEC-INV-17).
- Any other service's code or database (incl. `Diten.SupplyChainService` / MOD-0183 on `feature/mvp6-logistics`).
- Gateway routes, shared permission registry, DCP/registry/AGENTS.md, frozen contract files.

## 3. Owned Objects

| Object | Slice | Purpose |
|---|---|---|
| `InventoryTransaction` | S1 | Immutable line-level movement fact (DEC-INV-15), grouped by `DocumentGroupId` |
| `InventoryBalance` | S1 | Projection row per `InventoryDimension`; OnHand / Reserved / Available / InTransit / Blocked / QualityInspection |
| `InventoryDimension` (value object) | S1 | Canonical uniqueness key (§4) |
| `MovementType` (enum) | S1 | 15 canonical values, exactly as the frozen contract |
| `StockStatus` (enum) | S1 | AVAILABLE, QUALITY_INSPECTION, QUARANTINE, BLOCKED, IN_TRANSIT, DAMAGED, EXPIRED |
| `InventoryAuditRecord` | S1 | Who/what/when/correlation per posting |
| `InventoryMovementPosted` outbox event | S1 | Internal event, Pending until an integration-owned transport exists |
| `InventoryReservation` | S2 | Physical hold with ETag |
| `CostingMethodPolicy` / `InventoryCostLayer` / `InventoryValuation` | S3 | Valuation |
| Commands | S1 | `PostMovementCommand` (covers all 15 types incl. REVERSAL) |
| Queries | S1 | `GetAvailabilityQuery`, `GetBalanceQuery`, `GetTransactionsQuery` |
| API | S1 | `/api/inventory/movements` POST · `/availability` GET · `/balance` GET · `/transactions` GET |
| Permissions | S1 | `supplychain.inventory.read`, `supplychain.inventory.movements.post` · S2: `supplychain.inventory.reservations.manage` |

## 4. Entity Fields

### InventoryTransaction (collection `inventory_transactions`, insert-only)

| Field | Rule |
|---|---|
| `Id` / `TransactionId` | Server-minted; wire `transactionId` string |
| `TransactionNumber` | Server-minted `INV-{yyyy}-{0000000}`, unique per Tenant + LE (atomic counter) |
| `TenantId`, `LegalEntityId` | Required; **from JWT claims only**, never from body/query |
| `DocumentGroupId` | Optional; groups lines of one business document |
| `MovementType` | Required; canonical enum |
| `MovementReason` | Optional opaque MOD-0048 code |
| `ItemId` (uuid), `SkuId` (uuid), `SkuLevel` | Required; validated against PRODUCT-MASTER; `sku.itemId` must equal `itemId` (D-SKU-LINK) |
| `FromWarehouseId`, `FromLocationId`, `ToWarehouseId`, `ToLocationId` | Per movement-direction rules (§12) |
| `Quantity` + `UomId` | Required; decimal > 0 parsed from string, never float |
| `BaseQuantity` + `BaseUomId` | Server-computed via `getSkuUom` conversion; never client-supplied |
| `LotId` | Optional; required when product `lotControlled` |
| `SerialIds` | Optional; required when product `serialControlled` |
| `FromStockStatus`, `ToStockStatus` | Per direction rules; default AVAILABLE when omitted |
| `SourceModule`, `SourceType` | Required |
| `SourceDocumentId`, `SourceLineId`, `SourceSystem` | Optional (`SourceSystem` = DEC-INV-19 hook); `ExternalRef` stored nullable for DEC-INV-19 |
| `ReversesTransactionId` / `ReversedByTransactionId` | Server-derived for REVERSAL (see ASSUMPTION-0173-05) |
| `TransactionDate`, `PostingDate` | Required UTC; preserved separately (DEC-INV-13) |
| `IdempotencyKey` + `RequestFingerprint` | Required; unique `(TenantId, LegalEntityId, IdempotencyKey)` |
| `CorrelationId` | UUID; from `X-Correlation-Id` or server-minted |
| `CreatedBy`, `CreatedAt` | From JWT `sub`; server clock |
| `IsDeleted`, `DeletedAt` | Present per entity-base standard, **always false**; no delete path exists (append-only) |

### InventoryBalance (collection `inventory_balances`, projection only)

Key = `InventoryDimension`: `TenantId, LegalEntityId, ItemId, SkuId, SkuLevel, WarehouseId, LocationId, LotId,
StockStatus` (+ reserved nullable `OwnerId`, `ProjectId` for P2). Serial is tracked on transactions, not in the key.

Values (base UoM): `OnHand`, `Reserved`, `Available`, `InTransit`, `Blocked`, `QualityInspection`, `BaseUomId`,
`LastMovementId`, `LastPostingDate`, `Version` (optimistic concurrency). Bucket rule for a row with status S:
`OnHand = Σ`, `QualityInspection = OnHand if S=QUALITY_INSPECTION`, `Blocked = OnHand if S ∈ {BLOCKED, QUARANTINE, DAMAGED, EXPIRED}`,
`InTransit = OnHand if S=IN_TRANSIT`, `Available = OnHand − Reserved if S=AVAILABLE else 0`.

### Indexes (tenant-first, DB-010 test DB naming)

- `inventory_transactions`: unique `(TenantId, LegalEntityId, IdempotencyKey)`; unique `(TenantId, LegalEntityId, TransactionNumber)`;
  `(TenantId, LegalEntityId, ItemId, PostingDate)`; `(TenantId, LegalEntityId, LotId)`; `(TenantId, LegalEntityId, ToLocationId)`.
- `inventory_balances`: unique on the full dimension key; `(TenantId, LegalEntityId, ItemId, WarehouseId)`.

## 5. Repo Scope

The development slice may write only:

- `services/Diten.InventoryService/**` — new five-layer service (Api / Application / Domain / Persistence /
  Infrastructure) + `tests/`, scaffolded after the pattern of `services/Diten.ProcurementService` on `main`
  (reference only — never edited). Port **5066** in `launchSettings.json` / `appsettings*.json`.
- `docs/records/audits/2026-10/mod-0173-*` (evidence) and this module pack (Implementation Notes only).
- `docs/reference/architecture/api/mod-0173-*` (API reference, `/add-module` Phase 6).
- `execution/registries/module-implementation-status.md` is **not** written by the dev slice (shared registry); the
  dev report proposes the row and CT records it at acceptance.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, `CLAUDE.md`.
- `docs/analysis/contracts/**` — all frozen contracts, including `inventory-bundle.openapi.yaml` (consume as-is).
- `execution/portfolio/**` (DCP-009 etc.), `execution/registries/**`, `execution/domains/supply-chain-execution/domain-config.md`.
- `gateway/Diten.ApiGateway/**` (integration-agent only).
- `frontend/**` (no UI in S1/S2).
- All other services (`services/*` other than `Diten.InventoryService`), incl. `Diten.MdmService` (Model A/B),
  `Diten.ProcurementService` (scaffold reference, read-only), `Diten.CrmService`.
- Solution/run scripts shared by all services (`run_all.sh`, `run-diten.sh`, root `*.sln`) — registration there is a
  separate integration WP.
- Shared `Diten.BuildingBlocks` / `Diten.Platform.Common` sources.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| Central records for `Diten.InventoryService` / port 5066 (WP-0173-C1) | **PENDING** | Code starts only after AGENTS.md §3 + DCP-009 OD-5 + domain-config are updated (5066 measured free 2026-09-30) |
| `INVENTORY-BUNDLE` v1 | FROZEN (owned) | Implement exactly; no redefinition |
| `PRODUCT-MASTER-BUNDLE` v1 | FROZEN | Consume via typed HTTP client; Prism mock in dev/test; real MOD-0290 at progressive integration |
| `LOCATION` v1 | FROZEN | Same as above |
| MongoDB replica set (transactions) | Required | No multi-document transaction ⇒ BLOCKED, not faked |
| MOD-0048 MovementReason | Available as backbone | Opaque code in S1 |
| Event Bus transport | integration-owned | Outbox events stay Pending without transport |

## 8. Runtime Constraints

- Service `Diten.InventoryService`, port **5066**, five layers + CQRS/MediatR, four pipeline behaviors, `CustomBaseController`.
- Base path `/api/inventory` (contract `servers.url`).
- **Scope:** `tenant_id`, `legal_entity_id`, `sub` from the signed JWT (exactly one each, UUID), JWT validation as in
  the repo's existing services (issuer/audience/lifetime/signing key from configuration, never hardcoded). `X-Tenant-Id` if present must equal the claim (else 403). `X-Legal-Entity-Id` (contract
  `LegalEntityOverride`) if present and ≠ token LE → **403 `CROSS_LE_FORBIDDEN`** (multi-LE operator override is a
  follow-up, ASSUMPTION-0173-03). `tenantId`/`legalEntityId` in query or body → 400.
- **Correlation:** `X-Correlation-Id` is not declared in the frozen contract ⇒ optional; if present must be a UUID
  (else 400); if absent the server mints one. Always echoed in the response header and `error.correlationId`.
- **Idempotency:** `Idempotency-Key` header (required by contract) **and** body `idempotencyKey` (required) must be
  equal, trimmed, 1–128 chars, else 400. Same key + same fingerprint → **200** with the original durable result and
  `idempotentReplay: true`, no second transaction/balance change/event. Same key + different payload → 400
  `IDEMPOTENCY_KEY_REUSED` (ASSUMPTION-0173-04).
- **Wire format:** unwrapped OpenAPI v1 schemas (explicit pack exception to `Response<T>`, limited to `/api/inventory`,
  required for exact frozen-contract/mock compatibility; handlers may use `Response<T>` internally); errors use the contract `Error` schema; every body carries `contractVersion: "v1"`.
- **Consistency:** transaction insert + balance upsert(s) + audit + outbox in **one Mongo transaction**; balance
  update is guarded by `Version`/conditional filter so concurrent issues cannot overdraw. No partial success.
- **Append-only:** no update/delete endpoint or repository method for transactions; correction = REVERSAL.
- **Fail-closed consumers:** PRODUCT-MASTER unreachable → 503 `PRODUCT_MASTER_UNAVAILABLE`; LOCATION unreachable →
  503 `LOCATION_MASTER_UNAVAILABLE` (ASSUMPTION-0173-06). Never silent-pass.
- Soft-delete fields exist on entities for standard compliance but transactions/balances are never deleted.

## 9. Layout & Shell Contract

`shell: none`. No Razor view, menu, DataTable, resx or frontend route in S1/S2.

## 10. Backend File Convention

Five layers as in `Diten.ProcurementService`; feature root `Features/Inventory/Commands`, `Queries`,
`Handlers/CommandHandlers`, `Handlers/QueryHandlers`, `Validators`, `InventoryModels.cs`; one type per file;
Domain has no Mongo/framework dependency; Persistence owns Mongo; specific repositories (`IInventoryLedgerRepository`)
are allowed where atomic transaction+projection is required — no generic repository that could drop the
Tenant/LE predicate.

## 11. Frontend File Contract

Not applicable (S1/S2). See §20 UI follow-up.

## 12. Validation Rules

### Movement direction matrix (derived from DEC-INV-16 semantics)

| Class | MovementTypes | from* | to* | Stock check |
|---|---|---|---|---|
| Inbound | OPENING_BALANCE, GOODS_RECEIPT_PO, GOODS_RECEIPT_PROD, CUSTOMER_RETURN, COUNT_GAIN | forbidden | `toWarehouseId` + `toLocationId` required | — |
| Outbound | GOODS_ISSUE_SALES, GOODS_ISSUE_PROD, SUPPLIER_RETURN, COUNT_LOSS, SCRAP, WRITE_OFF | required | forbidden | source row must cover `BaseQuantity` |
| Warehouse transfer | WAREHOUSE_TRANSFER | required | required, warehouse differs | source row |
| Location transfer | LOCATION_TRANSFER | required | required, same warehouse, location differs | source row |
| Status transfer | STATUS_TRANSFER | required | same wh+loc; `fromStockStatus ≠ toStockStatus` | source row |
| Reversal | REVERSAL | derived from original | derived from original | inverse legs must not go negative |

### Field rules

| Field / header | Rule | Error |
|---|---|---|
| `Idempotency-Key` / `idempotencyKey` | Both present, equal, 1–128 | 400 `INVALID_REQUEST` |
| `movementType` | One of 15 | 400 `INVALID_MOVEMENT_TYPE` |
| `quantity` | Decimal string matching contract pattern, > 0, ≤ 6 fraction digits | 400 `INVALID_REQUEST` |
| `itemId`/`skuId`/`skuLevel` | Exist in PRODUCT-MASTER; `sku.itemId == itemId` | 422 `UNKNOWN_ITEM` / `SKU_ITEM_MISMATCH` |
| `uomId` | Convertible to base UoM via `getSkuUom` | 422 `UOM_CONVERSION_MISSING` |
| `lotId` | Required if `lotControlled` | 422 `LOT_REQUIRED` |
| `serialIds` | If `serialControlled`: distinct count == `BaseQuantity` (integer) | 422 `SERIAL_COUNT_MISMATCH` |
| warehouse/location ids | Exist, Active, location `isStockable`, location path contains the warehouse | 422 `UNKNOWN_LOCATION` |
| stock | Outbound/transfer source row sufficient (DEC-INV-12, negative prohibited, no override in S1) | 422 `INSUFFICIENT_STOCK` with `details.available/requested` |
| `transactionDate`, `postingDate` | UTC ISO-8601 | 400 |
| posting period | No period master in S1 ⇒ all periods open; hook left for `POSTING_PERIOD_CLOSED` | (follow-up) |
| GET `/availability` | `itemId` or `skuId` required | 400 / 404 `UNKNOWN_ITEM` |
| `page`/`pageSize` | ≥ 1; `pageSize` ≤ 200 | 400 |

## 13. Failure Path to Verify

1. **Duplicate:** same idempotency key posted twice → 1 transaction, balance changed once, 1 outbox event; 2nd = 200 `idempotentReplay: true`.
2. **Key reuse with different payload** → 400 `IDEMPOTENCY_KEY_REUSED`, no write.
3. **Missing/invalid:** unknown movementType, zero/negative/float-exponent quantity, missing to-location on receipt → 400/422, no write.
4. **Unauthorized:** no token → 401; token without `supplychain.inventory.movements.post` → 403; read without `.read` → 403.
5. **Cross-tenant / cross-LE:** tenant A data invisible to tenant B (GET returns empty/404, never A's rows); `X-Legal-Entity-Id` ≠ token LE → 403 `CROSS_LE_FORBIDDEN`.
6. **Concurrency:** two parallel issues of 60 against balance 100 → exactly one succeeds, the other 422 `INSUFFICIENT_STOCK`; balance 40, never −20.
7. **Negative stock:** issue > balance → 422, no transaction persisted.
8. **Fail-closed upstream:** product-master mock down → 503 `PRODUCT_MASTER_UNAVAILABLE`, no write; location mock down → 503.
9. **Reversal:** reversing a transaction twice → second 422 (`ALREADY_REVERSED`); reversing a receipt whose stock was already issued → 422 `INSUFFICIENT_STOCK`.
10. **Immutability:** no HTTP route or repository method can update/delete a transaction (architecture test).

## 14. Authorization Convention

Actor: tenant user or service actor (e.g. MOD-0142 GRN). Server-side `[HasPermission]` on every endpoint:
`supplychain.inventory.read` (GET), `supplychain.inventory.movements.post` (POST /movements), S2
`supplychain.inventory.reservations.manage`. Names follow the service's existing `supplychain.*` convention.
Permission **definition/seed** is a shared seam → INT-1 WP, not the dev slice.

## 15. Gateway / API Routing Decision

Route family `/api/inventory/**` → `127.0.0.1:5066` is required, **owned by `integration-agent`** in INT-1 after the
controller exists; headers to propagate: `Authorization`, `X-Tenant-Id`, `X-Legal-Entity-Id`, `X-Correlation-Id`,
`Idempotency-Key`, `If-Match`. The dev slice does not edit `ocelot.json`.

## 16. Acceptance Criteria (S1; S2 adds reservation boxes)

- [ ] `Diten.InventoryService` builds 0 errors / 0 new warnings; five layers, four pipeline behaviors, `CustomBaseController`; `/health` up on 5066.
- [ ] **Opening golden flow:** `OPENING_BALANCE +100` → 201, 1 transaction persisted, balance row OnHand=100 / Available=100; GET `/balance` and `/availability` return 100 after a service restart.
- [ ] **Issue flow:** `GOODS_ISSUE_SALES −20` → 2nd immutable transaction; balance 80; 1st transaction byte-identical in the DB before/after.
- [ ] **Idempotency** per §13.1–2.
- [ ] **Negative stock** prohibited per §13.6–7.
- [ ] **StockStatus:** receipt into QUALITY_INSPECTION → OnHand 100, Available 0, QualityInspection 100; STATUS_TRANSFER QI→AVAILABLE moves it; `byStatus` in `/availability` reflects each status (AVAILABLE/QI/QUARANTINE/BLOCKED/IN_TRANSIT).
- [ ] **Reversal** restores the balance with a new transaction; original untouched.
- [ ] **Base UoM:** `100 BOX` with conversion BOX→EA 10/1 persists `BaseQuantity 1000 EA`.
- [ ] **Tenant + LE isolation** per §13.5 on every endpoint.
- [ ] **RBAC** per §13.4 on every endpoint.
- [ ] **Audit + correlation:** every posting has an audit record and a Pending outbox event carrying the same correlation UUID as the response header.
- [ ] **DEC-INV-19 hook:** `SourceSystem` / `ExternalRef` persisted and returned where the contract exposes them.
- [ ] **Single SoR:** no other stock collection/balance exists in the service (architecture test); no Model B access.
- [ ] Contract conformance: every response body validated against `inventory-bundle.openapi.yaml` schemas (automated), and a Prism-mock smoke of the consumed PRODUCT-MASTER + LOCATION contracts passes with the real HTTP clients.

## 17. Test Expectations

- DCP-002 identity verifier exit 0 (or explicit environment-blocked record — never a fabricated PASS).
- Domain unit tests: direction matrix, bucket rules, decimal parsing, reversal derivation.
- Handler tests: idempotency, key reuse, negative stock, concurrency (real Mongo transaction), fail-closed upstream, tenant/LE isolation, permission denial.
- API contract tests: each declared status code and error code; schema validation of responses.
- Mongo integration tests on an isolated DB per DB-010; replica set required.
- Architecture: no update/delete on transactions; no second stock collection; no reference to Model B / MDM DB.
- Runtime E4 probe: running service on 5066 + Prism mocks; captured request/response + persisted Mongo documents + audit/outbox rows for the opening/issue/replay/negative flows, and state after restart.
- **Sabotage proof (K3):** for idempotency, negative-stock and tenant-isolation guards, show the test goes RED when the guard is removed, GREEN when restored.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name recorded as DCP-002 PASS (registry, 2026-09-11).
- [ ] DCP-002 verifier re-run exit 0 on a machine with Python (blocked here: no interpreter).
  **Waiver W-0173-01** — owner: Ali · reason: no Python on the MVP-1 machine · compensating control: registry row
  "DCP-002 gate OK 2026-09-11" + DCP-009 §20 · expiry: before the MVP-1 push/PR (must be re-run, exit 0).
- [x] DCP-009, domain-config, inventory report §4/§9/§17/§20/§21/§22 read.
- [x] Owned contract `INVENTORY-BUNDLE` v1 is FROZEN; consumed PRODUCT-MASTER and LOCATION v1 are FROZEN.
- [x] Backend-only first slices; no UI pattern invented.
- [x] Service/port, entity base, owned objects, repo scope, protected paths explicit.
- [x] Tenant+LE, RBAC, idempotency, concurrency, audit/outbox, fail-closed rules explicit.
- [x] ≥ 4 failure paths (10) and measurable acceptance criteria.
- [x] Gateway + permission seed routed to integration-agent (INT-1).
- [x] **Owner approval → `ready-for-dev`** (Ali, 2026-09-30).
- [x] Service decision: own service `Diten.InventoryService`, port 5066 (owner, 2026-09-30).
- [ ] **Code precondition:** WP-0173-C1 central records landed on this branch.

## 19. Implementation Notes — ASSUMPTIONS (autonomous mode; each is revisable only by a new pack version)

- **ASSUMPTION-0173-01:** superseded by OD-5 (own service); no shared composition root with MOD-0183.
- **ASSUMPTION-0173-02:** wire `legalEntityId` = the token LE UUID as string. Mock examples (`le-tr`) are illustrative.
- **ASSUMPTION-0173-03:** `X-Legal-Entity-Id` override for multi-LE operators is not granted in S1; a mismatch is 403.
- **ASSUMPTION-0173-04:** new error codes `IDEMPOTENCY_KEY_REUSED`, `SKU_ITEM_MISMATCH`, `UOM_CONVERSION_MISSING`,
  `LOT_REQUIRED`, `ALREADY_REVERSED`, `LOCATION_MASTER_UNAVAILABLE` are values of the frozen `error.code` string —
  within schema, not a contract change; they use only declared HTTP statuses (400/422/503).
- **ASSUMPTION-0173-05 (reversal):** the frozen request has no "reversed transaction" field. REVERSAL is posted
  with `sourceModule=MOD-0173`, `sourceType=REVERSAL`, `sourceDocumentId=<original transactionId>`; item/sku/quantity
  must equal the original; the server derives the inverse legs. A REVERSAL cannot itself be reversed.
- **ASSUMPTION-0173-06:** LOCATION unavailability returns 503 with the contract `Error` schema (code above).
- **ASSUMPTION-0173-07 (mock limits, §22.3-4):** Prism static mocks return a fixed `legalEntityId` (`le-tr`) for
  locations. The Location-LE == caller-LE check is implemented and **on by default**; it is disabled only in the
  mock test profile, with a test proving it rejects a mismatch when on. Real data-quality proof is a progressive
  integration (G1) item.
- **ASSUMPTION-0173-08:** `InventoryMovementPosted` event payload is internal (the frozen bundle has no event
  section); consumers must not depend on it until an additive contract publishes it.
- **ASSUMPTION-0173-09:** decimal output normalised with at least 3 fraction digits (matches contract examples),
  no precision loss; internal type `decimal`.
- **ASSUMPTION-0173-10:** `GET /transactions` `from`/`to` filter `PostingDate` by UTC day, inclusive.
- **ASSUMPTION-0173-11:** the target date `2026-10-31` is a schema-required planning default, not a commitment.

- **S2 reservation design (CT decision 2026-09-30, autonomous mode; binding for WP-0173-S2).** The frozen
  `ReservationRequest` carries only item/SKU/quantity — no warehouse, location, lot or status. Therefore:
  - **ASSUMPTION-0173-12 (scope):** a reservation holds quantity at **Tenant + LE + ItemId + SkuId + SkuLevel**
    level against `AVAILABLE` stock. Row-level `Reserved` on `inventory_balances` stays 0 in S2; row allocation
    (which bin/lot) belongs to MOD-0176 FEFO / MOD-0178 WMS. `/balance` rows therefore show `reserved 0`;
    `/availability` shows the SKU-level reserved total and `available = Σ onHand(AVAILABLE) − Σ active reserved`.
  - **ASSUMPTION-0173-13 (atomic position):** a per-SKU projection document `inventory_sku_positions`
    (`AvailableOnHand`, `Reserved`, `Version`) is maintained **inside the same Mongo transaction** as every ledger leg
    that touches an `AVAILABLE` row and every reservation create/release/expiry/consumption. It is a projection of
    the ledger + reservations (not a second stock truth) and must be rebuildable; an invariant test proves
    `Σ AVAILABLE rows onHand == AvailableOnHand` and `Σ ACTIVE reservations == Reserved`.
  - **ASSUMPTION-0173-14 (guard both ways):** create reservation succeeds only if
    `AvailableOnHand − Reserved ≥ q` (else 422 `INSUFFICIENT_AVAILABLE`, contract). A decrease leg on an `AVAILABLE`
    row must also keep `AvailableOnHand − Reserved ≥ 0` at SKU level (else 422 `INSUFFICIENT_STOCK`) — reserved stock
    cannot be issued to someone else.
  - **ASSUMPTION-0173-15 (consumption):** a movement whose `(sourceModule, sourceDocumentId, sourceLineId)` equals an
    ACTIVE reservation's consumes it: the reserved quantity it covers is released in the same transaction (partial →
    reservation stays ACTIVE with the remainder; full → `RELEASED`). The frozen status enum has no CONSUMED value.
  - **ASSUMPTION-0173-16 (expiry):** `expiresAt ≤ now` ⇒ `EXPIRED`. Due reservations of a SKU are expired inside the
    transaction of any create/release/decrease on that SKU, plus a periodic hosted sweeper. Expired quantity no longer
    counts as reserved.
  - **ASSUMPTION-0173-17 (release):** `If-Match` required (contract); ETag = `W/"<version>"`; mismatch or already
    released/expired → 409 `RESERVATION_CONFLICT`; missing `If-Match` → 400. Unknown/cross-scope id → 404.
  - **ASSUMPTION-0173-18 (FU-S1-01):** `/availability` with no ledger rows asks PRODUCT-MASTER: exists → 200 zeros;
    not found → 404 `UNKNOWN_ITEM`; unreachable → 503 `PRODUCT_MASTER_UNAVAILABLE` (extra status, as ASSUMPTION-S1-05).
  - **ASSUMPTION-0173-19 (FU-S1-02):** the `SuperAdmin` role bypass is removed from this service; every endpoint needs
    its explicit permission.

- **WP-0173-S1 CT-ACCEPTED (2026-09-30)** — commits 47029756..950e82c2; audit
  `docs/records/audits/2026-10/mod-0173-inventory-ledger-s1-audit-2026-09-30.md`. CT re-measured independently:
  clean build 0/0; 218/218 tests (0 skipped) on the replica-set Mongo; repo architecture guards 18/18; own sabotage of
  the coverage guard → 5 red, restored → green, empty diff; own runtime probe run (separate DB) 33/33 + 4/4 after a
  real restart (different PIDs); no token in logs. Agent assumptions S1-01…S1-20 accepted as recorded in the audit,
  with two follow-ups below (FU-S1-01, FU-S1-02).

## 20. Follow-up Items

- **FU-S1-01 (before G1 freeze):** `GET /availability` for a *known* item/SKU with no ledger rows returns
  404 `UNKNOWN_ITEM` (ASSUMPTION-S1-04). Consumers (0172 ATP, 0189 MRP) must see 200 with zeros; 404 only when
  PRODUCT-MASTER says the item/SKU does not exist (fail-closed 503 when it is unreachable).
- **FU-S1-02 (before G1 freeze):** the template's `SuperAdmin` role bypass lets that role post stock movements
  without `supplychain.inventory.movements.post`. For a GxP stock SoR, mutations require the explicit permission;
  review with security-agent.

- S2 Reservations; S3 Valuation (requires additive `INVENTORY-BUNDLE` v1.1 for any valuation read surface).
- INT-1: gateway route + permission seed (integration-agent).
- MovementReason validation against MOD-0048; posting-period master (`POSTING_PERIOD_CLOSED`); negative-stock
  policy exceptions per company/warehouse/item (DEC-INV-12).
- Multi-LE operator override (`X-Legal-Entity-Id`).
- Tenant-shell UI (stock inquiry / transaction history), 7 languages, Slim/Compact decision — required for G1 E4.
- Progressive integration: swap mocks for real MOD-0290 (MDM, Model A) and Location master; Location-LE data quality.
- Generic external-integration adapter (DEC-INV-19).

## 21. UI input — approved mockup (2026-09-30)

Owner-produced mockup "Stok Defteri v4" was reviewed by MVP-1 CT over four rounds and is accepted as the **input**
for the UI follow-up pack. It is not a UI spec, not implementation evidence and not a design authority; the
follow-up pack still needs its own `ready-for-dev` gate (shell `tenant`, 7 languages, UAS-001, verifier).

Accepted content decisions:

- **Screens:** Stock list (projection view) · Movement history (immutable, reversal only) · New movement (separate
  page; ~15 user fields ⇒ `golden_reference: compact`).
- **Manual movement types (7):** OPENING_BALANCE, COUNT_GAIN, COUNT_LOSS, SCRAP, WRITE_OFF, WAREHOUSE_TRANSFER,
  LOCATION_TRANSFER. All other canonical types are posted by their source modules, shown as "Automatic" in history,
  and **cannot be reversed from this UI** ("reversed in the source module").
- **STATUS_TRANSFER is not a manual action here** — status lifecycle belongs to MOD-0175 (DEC-INV-08). Inbound
  manual types may set only the *initial* status: AVAILABLE (default), QUALITY_INSPECTION, QUARANTINE.
- **Transfers are single-step in S1** (source decreases, target increases with the same status). Two-step
  in-transit transfer is an **open decision** tied to MVP-6 integration.
- Lots and locations are selected from masters (MOD-0174 / Location), never typed; lot required only when
  `lotControlled`; serial count must equal base quantity.
- **Reservation panel** (S2): reservation no, type, source module + document, quantity, expiresAt, status only.
- **Shipment panel** (owner decision 2026-09-30): kept as a **read-only consumer view of `SHIPMENT-BUNDLE` v1**
  (MOD-0183), hidden until MVP-6 integration; fields limited to that contract (number, status, carrier, mode,
  from/to, planned ship/deliver, actual delivery, POD receiver, lines); link via movement source reference
  (`sourceModule=MOD-0183`, `sourceDocumentId=<shipment>`), no local shipment copy. Vehicle/driver/distance/address
  are **not** in the contract — any need is an additive request to the MVP-6 contract owner.
- Error/permission states covered: insufficient stock, lot missing, serial mismatch, UoM conversion missing,
  upstream unavailable, idempotent duplicate, view-only, unauthorized (UAS-001: no skeleton, no redirect).

Open notes (non-blocking): shipment-driven history rows use labels "Sevkiyat çıkışı/teslimi" that must map to
canonical MovementTypes once the two-step decision is made; reason codes and source-module names in the mockup are
sample data (real values: MOD-0048 and the module registry); CSV export is a separate export gate (SOP §18.7).
