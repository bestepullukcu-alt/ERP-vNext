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
| Permissions | S1 | `inventory.stock.read`, `inventory.movements.create` · S2: `inventory.reservations.manage` |

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
4. **Unauthorized:** no token → 401; token without `inventory.movements.create` → 403; read without `.read` → 403.
5. **Cross-tenant / cross-LE:** tenant A data invisible to tenant B (GET returns empty/404, never A's rows); `X-Legal-Entity-Id` ≠ token LE → 403 `CROSS_LE_FORBIDDEN`.
6. **Concurrency:** two parallel issues of 60 against balance 100 → exactly one succeeds, the other 422 `INSUFFICIENT_STOCK`; balance 40, never −20.
7. **Negative stock:** issue > balance → 422, no transaction persisted.
8. **Fail-closed upstream:** product-master mock down → 503 `PRODUCT_MASTER_UNAVAILABLE`, no write; location mock down → 503.
9. **Reversal:** reversing a transaction twice → second 422 (`ALREADY_REVERSED`); reversing a receipt whose stock was already issued → 422 `INSUFFICIENT_STOCK`.
10. **Immutability:** no HTTP route or repository method can update/delete a transaction (architecture test).

## 14. Authorization Convention

Actor: tenant user or service actor (e.g. MOD-0142 GRN). Server-side `[HasPermission]` on every endpoint:
`inventory.stock.read` (GET), `inventory.movements.create` (POST /movements), S2
`inventory.reservations.manage`. Namespace `inventory.*` is owned by `Diten.InventoryService` (owner decision 2026-10-01, PKS-001 §4 one owner per namespace — `supplychain.*` stays with `Diten.SupplyChainService` / MVP-6). `create` and `read` are Tier-1 actions; `manage` is a documented Tier-3 action of this module.
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
  **Superseded in part by W-0173-02 (owner, 2026-10-01)** for tokens that carry no `legal_entity_id` claim — see §19.
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

- **WP-0173-OPS1 CT-ACCEPTED with incident (2026-10-02)** — commits 2b13548ed..21da43fc2: run_all.sh / run-diten.sh /
  run_watch.sh / watch-diten.ps1 start the Prism product-master mock (4010, until FU-PM-01) and InventoryService (5066);
  additions only; `bash -n` clean; watch-diten.ps1 saved with UTF-8 BOM → 0 parse errors on PowerShell 5.1. **Incident:**
  an unguarded "what-if" copy ran real `dotnet build/watch` for ≤ 60 s (shim PATH split on `C:`), which hot-restarted the
  owner's watch windows once (09:58–10:00); no process was killed, all services back, CT measured. Unrelated: RabbitMQ
  5672 down → Auth/Platform health 503 (masstransit "not started"), login page 200. Lesson for prompts: dry runs only with
  guarded shims (fail if a command resolves outside the shim dir) or not at all.

- **WP-0173-LE1 CT-ACCEPTED (2026-10-01)** — commits 98b3b05f1..c9111e7b7. CT re-measured: Web 391/391, service
  295/295, architecture 18/18 (0 skipped); middleware read (claim wins; claim-less → header required + MDM active-LE
  check with the caller's token; 503 fail-closed; only positive results cached, key includes the bearer hash); own
  sabotage of the membership check → 5 red, restored → green, empty diff. Real-login E2E (AuthService token) evidenced
  by the agent against Prism product/location mocks.

- **W-0173-02 — LE from a company selector (waiver, K19; owner Ali 2026-10-01).** Measured: AuthService tokens carry
  **no** `legal_entity_id` claim and the repo has **no** user↔legal-entity assignment (same gap in ProcurementService,
  which falls back to an unsigned header + dev bypass, and in MOD-0183, "held"). The owner has several legal entities
  and chose a company selector. Rules:
  - Token **has** `legal_entity_id` → unchanged (header, if present, must equal it, else 403 `CROSS_LE_FORBIDDEN`).
  - Token has **no** claim → `X-Legal-Entity-Id` (UUID) is **required** (absent/invalid → 403 with a "select a company"
    message, no data). The id must be an **active legal entity of the caller's tenant**, checked server-side against MDM
    (`/api/legal-entities/{id}/lookup-validation` through the Gateway with the caller's token); unknown/inactive/other
    tenant → 403 `CROSS_LE_FORBIDDEN`; MDM unreachable → 503 `LEGAL_ENTITY_MASTER_UNAVAILABLE` (fail-closed, no write).
  - Every posting/reservation audit record stores the LE **and its source** (`token` | `header-unverified-membership`).
  - **Accepted risk:** without membership data any user of the tenant can select any of its legal entities.
    Therefore the G1 item "cross-LE leak blocked" **cannot close** under this waiver.
  - Owner: Ali. **Expiry: before G1 sign-off.** Retired by the permanent fix (central/PSS request FU-LE-01).

- **WP-0173-K1 CT-ACCEPTED (2026-10-01)** — commits fe855440..a27ce77f. CT re-measured: clean build 0/0; 278/278
  (0 skipped); old-name grep 0 (deliberate split literals only in `PermissionNamespaceTests`); all 6 `[HasPermission]` use
  constants; own sabotage (Read constant → old name) → 62 red, restored → 278 green, empty diff. FU-S2-01 closed
  (test-DB rule: a `_`-segment `test`/`tests`/`itest`; anything else fails closed on unscoped groups).

- **WP-0173-INT1 CT-ACCEPTED (2026-10-01)** — commits 5f97a4b5..6b9660b4: `ocelot.json` +2 routes (`/api/inventory`,
  `/api/inventory/{everything}` → localhost:5066, GET/POST/OPTIONS, no AuthenticationOptions), +34/−0. CT re-measured:
  gateway build 0/0; gateway tests 83/84 — the one red `EveryRoute_DownstreamPortIsInKnownServiceSet` is **pre-existing**
  (measured at dc2b6dd0: 5063×46, 5064×62, 5065×14; INT-1 adds 5066×2); own E2E run through a probe gateway 12/12
  (Idempotency-Key and If-Match pass the gateway, correlation preserved, 401 passthrough, PUT 404), ports released, no token.
- **Permission namespace (owner decision 2026-10-01):** `supplychain.inventory.*` → `inventory.*`
  (`inventory.stock.read`, `inventory.movements.create`, `inventory.reservations.manage`). Nothing was seeded, so no
  alias is needed; code/tests/docs rename = WP-0173-K1.
- **WP-0173-K1 (agent PASS, pending CT, 2026-10-01)** — rename done in `services/Diten.InventoryService/**` (constants
  `InventoryPermissions.Read` / `CreateMovements` / `ManageReservations`; every `[HasPermission]` uses them), tests,
  runtime probe, README and the API reference; no alias. A token carrying only the old names is 403 on all 6 endpoints.
  FU-S2-01: the startup gate logs the SKU-position groups the rebuild skipped for an incomplete scope (WARNING, with the
  count, "never guessed, never deleted"); on a database whose name has no `test` / `tests` / `itest` segment (DB-010
  test naming as practised in the repo) a count > 0 fails the gate (LogCritical, the host does not start). Unscoped
  documents are never fixed or deleted. Evidence `docs/records/audits/2026-10/mod-0173-k1-evidence/`.

- **WP-0173-S2 CT-ACCEPTED (2026-10-01)** — commits 6441bf25..b91bbba7; audit
  `docs/records/audits/2026-10/mod-0173-inventory-ledger-s2-audit-2026-10-01.md`. CT re-measured independently:
  clean build 0/0; 262/262 (0 skipped); repo architecture guards 18/18; own sabotage of the SKU-level reserve guard
  (`ApplyAvailableDeltaAsync` `$expr`) → 5 red, restored → green, empty diff; own runtime probe (separate DB) 48/48 +
  6/6 after restart; no token in logs. FU-S1-01 and FU-S1-02 closed. Agent assumptions S2-01…S2-15 accepted.

- **WP-0173-S1 CT-ACCEPTED (2026-09-30)** — commits 47029756..950e82c2; audit
  `docs/records/audits/2026-10/mod-0173-inventory-ledger-s1-audit-2026-09-30.md`. CT re-measured independently:
  clean build 0/0; 218/218 tests (0 skipped) on the replica-set Mongo; repo architecture guards 18/18; own sabotage of
  the coverage guard → 5 red, restored → green, empty diff; own runtime probe run (separate DB) 33/33 + 4/4 after a
  real restart (different PIDs); no token in logs. Agent assumptions S1-01…S1-20 accepted as recorded in the audit,
  with two follow-ups below (FU-S1-01, FU-S1-02).

- **WP-0173-LE1 (agent result, pending CT, 2026-10-01) — W-0173-02 implemented.** `InventoryScopeResolutionMiddleware`:
  claim → unchanged (header must equal it); no claim → `X-Legal-Entity-Id` required and verified by the new
  `LegalEntityMasterHttpClient` (MDM `lookup-validation` via Gateway, caller token; 404 → 403 `CROSS_LE_FORBIDDEN`, 401/403
  → 403 `CROSS_LE_FORBIDDEN` + log, unreachable/5xx/timeout → 503 `LEGAL_ENTITY_MASTER_UNAVAILABLE`). Positive answers
  cached ≤ 60 s keyed tenant + LE + SHA-256(bearer); refusals never cached. `LeSource` (`token` |
  `header-unverified-membership`) on posting and reservation audit rows (null for the expiry sweeper). New config
  `Inventory:Upstream:LegalEntityMasterBaseUrl`; new code `LEGAL_ENTITY_MASTER_UNAVAILABLE` (named by W-0173-02); the
  "select a company" 403 carries no code. Real-login E2E measured; evidence `docs/records/audits/2026-10/mod-0173-le1-evidence/`.

- **WP-0173-RSV1 (CT-ACCEPTED 2026-10-02) — FU-RSV-01 fixed, owner decision 2026-10-02 = option 1.**
  The shared manifest gains an OPTIONAL, LAST `ApiPermissions` list (`ModuleManifestPermission(PermissionKey,
  DisplayName, Scope = "Tenant")`); the Platform reconcile syncs each key to AuthService with its declared scope
  ("Tenant" | "PlatformAdmin" exactly; anything else skipped with a warning), creates no page/action, prunes nothing.
  INVENTORY-LEDGER declares `inventory.reservations.manage` (Tenant) there — no fake UI action. Standard §2d added; the
  WC-D1 Y2 pinned manifest property set +4 lines (CT reviewed: no address-shaped field). Commit `c0abc6728`. Measured live
  after the fast-forward: one reconcile pass (13:15:40Z) refreshed the 4 existing `inventory.*` keys and created
  `inventory.reservations.manage` — Module `inventory-ledger`, Scope Tenant, 1 row, 0 pages / 0 actions for it; catalog
  unchanged at 8 pages / 19 actions. Granting it to a role and reserve/release 403 → 201 are NOT claimed (CT measures).
  Evidence `docs/records/audits/2026-10/wp-0173-rsv1-evidence-2026-10-02.md`.
  **CT acceptance (K13):** re-measured in a separate worktree at `8105e1581` — Inventory 355/355, Platform
  ModuleRegistration + NoAddress 48/48; CT sabotage (handler scope forced to "PlatformAdmin") → 7 red, restored, empty
  diff; read-only catalog read: 5/5 `inventory.*` keys Module `inventory-ledger`, Scope 0 (Tenant). Live after the owner's
  grant + re-login (run G10G21Z, 46/46): token carries 5/5 keys; reserve 30 → 201 (`W/"1"`), reserved 30, available
  278 = AVAILABLE 308 − 30; release with current If-Match → 200 (available back to 308); stale If-Match → 409
  `RESERVATION_CONFLICT`. Persisted: `inventory_reservations` 1 row (Released, Version 2, same tenant/LE), 2 audit rows
  (Created, Released), 2 outbox rows `InventoryReservationChanged` (Pending — RabbitMQ down on this machine).

- **WP-0173-PM2 (agent result, pending CT, 2026-10-09) — INVENTORY-BUNDLE 1.1.0: `SkuLevel` `Item` + `getStockPresence`
  (MOD-0290 R-12).** Branch `feature/sce/mod-0173-pm2` from `04ad7fb6d`; commits `4411a2108` (contract 1.1.0, additive, FROZEN,
  README) and `56bf2c637` (code + tests).
  - **What it serves.** `GET /api/internal/inventory/stock-presence` on InventoryService directly. It is not on the Gateway:
    no route matches, and `ocelot.json` is unchanged (route test). The scope middleware does not cover the path, and JWT
    authorization is off for it.
  - **The key gate.** The only gate is `InternalServiceKeyFilter`, configured from `Inventory:InternalServiceKeys`
    (`Identifier`, ≤ 2 `Secrets`, `Endpoints`). The header is `X-Internal-Api-Key: <identifier>:<secret>` (ASSUMPTION-0173-PM2-01).
    - Comparison: SHA-256 + `FixedTimeEquals`, over every key and secret.
    - One byte-identical 401 for: missing / wrong / unknown / other-operation key, user token (alone or with a key), and
      http outside Development.
    - Every call is audited in `inventory_audit` (key identifier, tenant, SKU, level, status, outcome, correlation); the secret
      never appears.
  - **The read.** `inventory_balances` for ONE tenant across ALL legal entities and ALL statuses: `OnHand ≠ 0`, live rows,
    new index `ix_tenant_sku_level`. `inventory_sku_positions` carries AVAILABLE only, so it is not enough. A store read or
    audit failure answers 503 `INVENTORY_UNAVAILABLE`; `coverage` is always `COMPLETE`.
  - **`Item`.** Accepted only here. `postMovement` still refuses `skuLevel: Item` with 400 `INVALID_REQUEST`, at request
    validation, before PRODUCT-MASTER (measured; PM1).
  - **Configuration and dev key.** `appsettings.json` carries an empty key list, so every call is 401 until a key is configured.
    `UserSecretsId` is added. `scripts/new-inventory-internal-key.ps1` writes the key straight into the user-secrets file (never
    a command line), copies it to the clipboard, and rotates (2nd secret; `-RetireOldest`).
  - **Tests.** Inventory 692 → **722/722**, Gateway 101/102 → 107/108 (the pre-existing 5065 red), Web 649/649, architecture
    18/18, 0 new warnings.
  - **Sabotages.** (a) tenant filter · (b) secret check · (c) AVAILABLE only · (d) negative as zero · (e) second secret ignored ·
    (f) endpoint list → each red, restored (md5 equal).
  - **Probe 27/27.** Run on own Auth / Platform / two Inventory hosts (Development + Staging) / own mongod.
    - Every status and the other company → true; other tenant → false.
    - Six refusals → identical 401; Staging over http → 401 + warning.
    - p95 16 ms over 300 calls; live rotation without restart.
    - 323 audit rows; secret scan 0.
  - **Opens.** O-PM2-1 (header format → MDM), O-PM2-2 (TLS-terminating proxy → ForwardedHeaders), O-PM2-3 (`TestSettings`
    defaults to 27017), O-PM2-4 (`Item` elsewhere → PM1), O-PM2-5 (gateway 5065 red, not PM2).
  - **Evidence.** `docs/records/audits/2026-10/mod-0173-pm2-evidence-2026-10-09.md`, `-scripts-2026-10-09.md`.

**PM2 — CT acceptance (2026-10-09).** CT ran the Inventory suite on its OWN replica set (`INVENTORY_TEST_MONGO`, port 27217 —
not the owner's 27017): **722/722**; gateway **107/108** (the one red, `EveryRoute_DownstreamPortIsInKnownServiceSet` / port 5065, is
pre-existing on the MVP-1 branch and belongs to the procurement routes); after merging the MVP-1 branch (E5W2) into
`feature/sce/mod-0173-pm2` (clean) architecture **18/18**. Read `InternalServiceKeyFilter` (one 401 body, user token refused,
https outside Development, refusals audited, secret never logged), `InternalServiceKeyAuthenticator` (SHA-256 of both sides +
`FixedTimeEquals`, ≤ 2 secrets, endpoint list) and `StockPresenceReader` (`TenantId` + `SkuId` + `SkuLevel` + `OnHand ≠ 0` on
`inventory_balances`, no legal-entity filter on purpose). CT sabotage (own): the "user token sent" refusal removed →
`Every_refused_key_gets_the_same_401_with_the_same_body_byte_for_byte` red, restored, porcelain clean. **INVENTORY-BUNDLE 1.1.0
FROZEN.** Fast-forwarded into the MVP-1 branch. Decisions on the opens:
- **O-PM2-1** key header `X-Internal-Api-Key: <identifier>:<secret>` — accepted; the CT tells MDM with the freeze note.
- **O-PM2-2** behind a TLS-terminating proxy the request is not https → 401 (safe direction) → **FU-0173-PM2-01** (deployment): Kestrel
  TLS or `ForwardedHeaders` with a known-proxy list before production.
- **O-PM2-3** `TestSettings` falls back to the owner's 27017 → **FU-0173-TEST-01**, delivered in PM1 (no default: the suite refuses to
  start without `INVENTORY_TEST_MONGO`, or starts its own ephemeral replica set). CT note: the CT's own Inventory runs at the Q3 / Q4 /
  Q4R / Q5 acceptances also used the 27017 test database `diten_inventory_s1_tests` (not the live data) — disclosed to the owner.
- **O-PM2-4** `Item` on the other operations → PM1. **O-PM2-5** gateway 5065 red → procurement owner.

**PM1 — PRODUCT-MASTER v1.1 consumer (WP-0173-PM1, CT-MVP1-0173-PM1 v1, 2026-10-09; agent result, pending CT).**
- **Contracts.**
  - `product-master-bundle.openapi.yaml` 1.1.0-draft.8 and the v1.0 copy were taken unchanged from `56490f48e` (OD-8).
  - **INVENTORY-BUNDLE 1.2.0** is additive (CT-PM1-1): three 422 codes, `PRODUCT_MASTER_DATA_INCOMPLETE`, `ITEM_NOT_ACTIVE` and
    `SKU_LEVEL_NOT_STOCKABLE`, plus the `postMovement` rule note.
- **Rules** (`ProductMasterStockRules`, pure).
  - Lot / serial flags come from `getSku` as `bool?`. An unknown flag is never read as false.
  - Lot null → `PRODUCT_MASTER_DATA_INCOMPLETE`.
  - GSKU is accepted only when the product is explicitly serial-free; otherwise `SKU_LEVEL_NOT_STOCKABLE`.
  - LSKU / FG serial null → data incomplete.
- **Lifecycle.**
  - **ASSUMPTION-0173-PM1-01:** the effective status is the most restrictive of the item row, the SKU row and `getSku`;
    Archived = Retired.
  - Deactivated: no OPENING_BALANCE / GOODS_RECEIPT_PO / GOODS_RECEIPT_PROD.
  - Retired: only SCRAP / WRITE_OFF.
  - Draft, unknown or missing: nothing (CT-PM1-2/3) → `ITEM_NOT_ACTIVE`.
  - **ASSUMPTION-0173-PM1-02:** REVERSAL is not checked against PRODUCT-MASTER (the contract accepts it in every state).
- **MDM calls per movement:** 4 → **3** (GSKU 4: + getProduct).
- **Item level.** Item is open on postMovement, availability, balance, transactions and reservations. TRACE and QUARANTINE still refuse
  `Item` with 400.
- **Lots.** **ASSUMPTION-0173-PM1-03:** in `createLot` an unknown lot flag → 503 `PRODUCT_MASTER_UNAVAILABLE`.
- **Web.**
  - Flags come from `getSku`; an unknown flag blocks the form.
  - The picker uses `/stockable-items?status=Active` (**W-UI-01 closed**).
  - Item level is selectable; resx in 7 languages.
- **FU-0173-TEST-01 delivered.** `TestSettings` has no 27017 fallback. **ASSUMPTION-0173-PM1-04:** the address comes from
  `INVENTORY_TEST_MONGO` (27017 refused) or from the suite's own EphemeralMongo replica set.
- **Measured.**
  - Inventory 722 → **800** (own mongod); Web 675 → **685**; gateway 107/108 (pre-existing 5065); architecture 18/18.
  - Warnings 0 / 21 / 7, unchanged.
  - 30 red-first cases.
  - Sabotages (a)–(e) red with 3 / 2 / 1 / 7 / 1 failures.
  - Runtime probe on own ports: Item receipt 201; unknown-lot SKU 422 / form blocked; Deactivated receipt 422 `ITEM_NOT_ACTIVE` on
    screen.
- **Opens.**
  - **O-PM1-1:** QUARANTINE 1.1.0 + TRACE 1.3.0 (`Item`) are required before MDM v1.1 goes live (owner decision). An Item receipt
    into QUARANTINE opens a hold that `HoldList` cannot carry.
  - O-PM1-2: REVERSAL vs CT-PM1-2.
  - O-PM1-3: `stockable:false`.
  - O-PM1-4: Deactivated CUSTOMER_RETURN / COUNT_GAIN.
  - O-PM1-5: there is no gateway route for `/api/product-master` (the Web bridge is Development only).
  - O-PM1-6: ledger "Item" label / Global Product picker text.
  - O-PM1-7: gateway 5065.
- **Evidence.** `docs/records/audits/2026-10/mod-0173-pm1-evidence-2026-10-09.md`, `-scripts-2026-10-09.md`, `mod-0173-pm1-shots/`.

**PM1 — CT acceptance (2026-10-09).** The CT merged the MVP-1 branch (VIS1) into `feature/sce/mod-0173-pm1` (clean) and measured
the merged tree: Inventory **800/800** on the CT's OWN replica set (27217), Web **685/685**, gateway **107/108** (known 5065 red),
architecture **18/18**; the two PRODUCT-MASTER files are byte-identical to `56490f48e`. CT sabotage (own): the Retired rule allowed
every movement type → 8 red in `ProductMasterV11RulesTests` (Retired × STATUS_TRANSFER / COUNT_LOSS / LOCATION_TRANSFER /
CUSTOMER_RETURN …, item-vs-SKU status), restored, porcelain clean. **INVENTORY-BUNDLE 1.2.0 FROZEN.** Decisions on the opens:
- **O-PM1-1 → WP-0174-ITEM (before MDM v1.1 goes live):** TRACE 1.3.0 + QUARANTINE 1.1.0 add `Item` (additive). Until then a
  lot-controlled material at `Item` level cannot get a lot (`createLot` refuses the level), so its receipt cannot post, and an
  `Item` quarantine hold breaks the QUARANTINE list schema. ASSUMPTION-0173-PM1 `createLot` unknown lot flag → 503 gets its own
  TRACE code in the same WP.
- **O-PM1-2:** REVERSAL is exempt from the lifecycle check in every state (a correction must stay possible; a Draft item has no
  transaction to reverse) — CT-PM1-2 amended accordingly.
- **O-PM1-3 → WP-0174-ITEM:** `stockable: false` refuses every movement with 422 `SKU_LEVEL_NOT_STOCKABLE`.
- **O-PM1-4:** accepted — OD-3 lets customer returns and count gains in on a Deactivated item.
- **O-PM1-5:** the Gateway route `/api/product-master` is MDM's R-02 — reminded to MDM; production picker depends on it.
- **O-PM1-6 → small FU batch** (raw `Item` label on the ledger list; GlobalProduct picker text). O-PM1-7 → procurement owner.
- **CT-PM1-4 (dev environment):** the first `getSku` example of draft.8 is the v1.0 shape (no flags, no lifecycle), so the fleet's
  Prism makes every posting 422 under PM1 (correct fail-closed behaviour). The fleet's 4010 is switched to a dev PRODUCT-MASTER
  bridge (the PM1 `pm-stub.js` with a dataset seeded from the v1.0 example ids: Amoksisilin GP-000123 / LSKU-TR-000045, lot true,
  serial false, plus `Item` materials); MDM is asked to make the first example of each v1.1 operation complete (draft.9).

### FUB1 — implementation note (WP-MVP1-FUB1, 2026-10-09, pending CT) — O-PM1-6 closed

- **Branch and commit:** branch `feature/mvp1-fub1` from `9697fda76`, local only, no push. Commit `02905f3ea` (FU01 Web).
  Evidence: `docs/records/audits/2026-10/mvp1-fub1-evidence-2026-10-09.md`.
- **Ledger list:**
  - `SkuLevelItem` added to `StockLedgerIndex.{7}` (the StockMovements wording), `_IndexL10n` and the required keys.
  - The `Item` level is labelled in 7 languages; the movement list already was.
  - New guard: every level the ledger knows is labelled in both lists in 7 languages.
- **Product picker:**
  - A Global Product shows its own name ("code · name"), read with getProduct (`/api/product-master/products/{itemId}`,
    the read the ledger labels already use; at most one call per product on the page).
  - The SKU's display name is the second line (`PickerOption.Sub`, a select2 two-line template in the ledger filter,
    the movement list filter and the movement form).
  - A failed read keeps today's text. The SKU picker is unchanged.
- **PM1 guard `Product_and_sku_pickers_call_only_the_contract_search_for_Active_records` extended:** besides the search,
  only that one product read is allowed (OPEN O-FUB1-2 for the W-UI-01 closure note).
- **Tests:**
  - Web 685 → **687** (+2).
  - Before-red was shown with the base label files and with the old picker behaviour.
  - Sabotage (b) (raw level) → red; restored, diff empty.
- No InventoryService, contract, Lots or Quarantine file was touched.

### FUB2 — implementation note (WP-MVP1-FUB2, 2026-10-10, pending CT)

- **Branch and commits:** branch `feature/mvp1-fub2` from `fa4c4e188`, local only, no push. Commits `f35197c5a` (contracts),
  `52176c83e` (InventoryService), `6b5382e23` (Web). Evidence: `docs/records/audits/2026-10/mvp1-fub2-evidence-2026-10-10.md`
  (+ `-scripts`, `mvp1-fub2-shots/`).
- **PRODUCT-MASTER 1.1.0-draft.11.**
  - Taken file-only from `origin/chore/ct-round-2` @ `00a4d4268` (branch head `6ddda3cc2`, same blobs).
  - The v1.0 file is byte-identical (unchanged); no MDM code; the files are not edited.
  - Consumer behaviour unchanged, no production code change. Prism smokes adapted to the example names:
    - the default answer is now v1.1;
    - v1.0 is `Prefer: example=v1.0-mock`;
    - the Global Product example carries the deprecated lot / serial flags (doubt = yes), so GSKU stays refused.
  - The v1.0 backward-compatibility smoke stays on the v1.0 file. Prism + validator 66/66.
- **O-ITEM-3 BLOCKED (O-FUB2-2).**
  - The stock movement list rows (INVENTORY `TransactionItem`) carry no user, although `InventoryTransaction.CreatedBy` is stored.
  - Showing the recorder by name needs an additive `TransactionItem.createdBy` (INVENTORY 1.2.2) — outside this WP's allowed
    contract changes. The ledger list is a balance projection without a recorder.
  - No ledger / movement Web change.

### BL-501 — central audit (WP-MVP1-AUDIT1 v2, 2026-10-14, agent result, pending CT)

- **Branch:** `feature/platform/bl-501-central-audit` from `9729409e0`, local only, no push. Evidence:
  `docs/records/audits/2026-10/wp-mvp1-audit1-bl501-evidence-2026-10-14.md`.
- **Shared forwarder (AUD-001 K4):** `Diten.BuildingBlocks.CentralAudit(.Abstractions)` — marker `ICentralAuditDeclaration`
  (same shape as MVP-3's 82ebedeb4), MediatR behavior, outbox `central_audit_outbox` in Inventory's own database, dispatcher to
  Platform `POST /api/internal/audit/append` (per-event idempotency added to Platform: `AuditAppendRequest.EventId`).
- **Inventory:** all 58 write commands path **b** (`inventory-merkezi-iletim`). The central record is written in the command's own
  Mongo transaction (9 transaction callbacks enlist it), so it commits with the business data or not at all (K2). The in-product
  trails (`inventory_audit`, quarantine history, recall timeline, expiry events, lot / location history) are unchanged.
  `Program.cs`: one `AddCentralAudit` line (`RequireEnlistment`) + `/health/central-audit`.
- **Numbers:** Inventory 1159+4 → 1174+4 (16 Prism reds both sides, Prism alone 16/16); AuditTrail unaudited-new 123 → 65;
  architecture 69/77 → 71/79, same 8 reds. Item 6 (Platform / Auth e-sign commands) **BLOCKED** — options in the evidence §5.
- **CT HOLD sonrası (2026-10-14, agent result, pending CT):** MVP-1 ucu `394c05550` merge edildi (`b9c113b54`, çakışmasız); R4
  `PrepareRecallSettingsChangeCommand` işaretlendi (59 komut, hepsi yol b). Başarısız komutun commit olmuş satırı `Abandoned` (gerçek Mongo
  testi, s8); sahipsiz satır Platform'a `Unconfirmed` gider (eklemeli `AuditOutcome.Unconfirmed = 4`, s9). Madde 6 karar A:
  Platform e-imza işlem-içi izi (18'in 16'sı; 2 komut işlem dışı yazıyor → CT'ye soru), Auth 3 komut K2 borcu 8 → 11, CT-DECISIONS
  kaydı. Inventory 1182+4 → 1199+4, Platform 260 kırmızı ad ad aynı, mimari 69/77 → 71/79 aynı 8, AuditTrail denetimsiz-yeni 65 → 46,
  s1–s9 kırmızı → geri yeşil. Kanıt §11.
- **BL-501 / WP-MVP1-AUDIT1 v2 CT-ACCEPTED (2026-10-14).** Owner decision A (BL-501 now). First result CT HOLD: R4 command unmarked, CT sabotage Abandon → Pending stayed green, orphan rows sent as normal records. After the hold: MVP-1 tip merged, R4 command marked (59, all path b), real-Mongo abandoned-row test, `AuditOutcome.Unconfirmed` (additive) for released orphans, CT decision A written to CT-DECISIONS.md (Platform e-sign in-transaction trace 16/18; Auth 3 → K2 8 → 11). CT trial merge on `394c05550`: Inventory **1199 + 4**, Building.Blocks 28/28, architecture 71/79 same 8, Inventory new-unaudited **0** (all services 46). **CT sabotage repeated:** Abandon → Pending → `A_failed_command_whose_transaction_committed_leaves_an_abandoned_row_that_is_never_sent` red; restored. Open: `RegisterSignableActionsCommand` + `VerifySignatureCommand` write outside the transaction → CT choice A = move into `ESignatureAuditIntent` (FU-0022-AUD-03, MOD-0022 owner); FU-0022-AUD-02 (Auth write-once); FU-BL501-CATEGORY; FU-BL501-MIGRATE (MDM / Auth copies). Merged `--no-ff` into MVP-1. SYNC3 note: PR #144 touches the same ledgers — re-measure on the owner main sync.
**WP-MVP1-FU-CLEAN1 — agent result, pending CT (2026-10-19).** Dal `feature/sce/wp-mvp1-fu-clean1` (MVP-1 ucu `437e52c9e`), yerel. Kanıt `docs/records/audits/2026-10/mvp1-fu-clean1-evidence-2026-10-19.md`.
- **`fd746417e` FU-QUI1-ESC-TEST:** hareket iptali ve lokasyon onayında `subtext` / `entityName` düz metin (global onay penceresi kaçışlar; çift kaçış yok). Bekçi MOD-0175 commit'inde (`web-scripts-guard.test.mjs`).
- **`c2ee96f62` FU-EVIDENCE-AUDIT-NAMES:** ortak denetim-ad okuyucusu nötr katmanda — `Application/Common/IAuditActorNames` + `Persistence/CentralAuditActorNames` (`InventoryCollections.CentralAuditOutbox` sabiti; kiracı + korelasyon + kişi eşleşmesi). Expiry / Recall kopyaları kendi commit'lerinde silindi.
- **`8a92871b5` FU-WEB-CONTRACT-FAKES:** `ContractRoutes` — eski yedi sahtenin 2xx gövdesi sahip sözleşmenin cevabı olmalı (kapalı nesne, format, pattern, enum); bilerek sözleşme dışı gövde `RequireOff` ile yalnız adı verilen ihlale izinli. Inventory / Location / MovementForm sahte gövdeleri düzeltildi (itemKind, getSku ↔ UoM rotası, uuid itemId, LocationDetail); sözleşme değişmedi.
- **Sayılar:** Inventory 1278 + 4 → 1286 + 4, Web 2575 → 2607; mimari aynı 8, gateway 164 / 165, Inventory yeni-denetimsiz 0. Sabotaj 13 / 13 kırmızı → yeşil.
- **CT'ye:** FU-0173-UI-USER-01 DUR-SOR — hareket satırında kullanıcı yok (`TransactionItem`), Web-only yol yok; A: INVENTORY-BUNDLE'a eklemeli `createdBy: UserRef`, B: bugünkü hâl. O-CLEAN1-3 (yük altında sallantılı merkezi denetim yetim testi), O-CLEAN1-4 (bin'den başlayan host 5067'yi alır).
**G1 re-measure + owner decisions (CT, 2026-10-20, MVP-1 tip `03c697e1a`).** Read-only re-measure of the 17 items of `docs/analysis/inventory-capability-scope-and-dependency-report.md` §20.1 (last formal record: `docs/records/audits/2026-10/mod-0173-g1-closure-measurement-2026-10-02.md`). PASS 11 (5–11, 13–16; the live evidence of 5–10 is from 2026-10-06 → re-run live on the tip in the closing G1 run), PARTIAL 4 (1–4: MDM PRODUCT-MASTER code is on the branch since SYNC3 but the fleet still uses the 4010 bridge and v1.1 is `1.1.0-draft.16`), OPEN 2 (12 cross-LE leak; 17 E4 UI + API + persisted + audit together). Owner decisions: **G1-D1 = A** — item 12 passes CONDITIONALLY; waiver W-0173-02 is extended until the central user↔legal-entity work (FU-LE-01) lands (owner deferred it after all MVPs), its expiry is no longer "before G1 sign-off". **G1-D2 = A** — items 1–4 pass CONDITIONALLY and close with WP-MVP1-PM-LIVE right after MVP-1 (real MDM PRODUCT-MASTER, bridge removed, data option A). **G1-D3 = A** — FU-INT1-02: non-UUID correlation id accepted and replaced (FIX-LIVE3 addendum). **G1-D4 = A** — W-UI-02: the seed tenant `…0001` is a test tenant, reset by the PM-LIVE data rebuild. Record fixes in this pass: FU-S1-01 / FU-S1-02 / FU-S2-01 marked CLOSED (closed in S2 / K1, rows never marked), FU-PM-01 PARTIAL, FU01 W-UI-01 closed in code. **DCP-002:** the verifier ran on 2026-10-20 with Python 3.12.10 + openpyxl 3.1.5 — `verify_module_id.py . --check-id MOD-0173 --name "Inventory Ledger & Valuation"` → OK (proven against Blueprint/registry) → **W-0173-01 closed**. Remaining for G1 sign-off: the live G1 run on the tip (items 5–10 + 17 with the owner session) and item 13 note (3 Inventory endpoints check the key in the body — `QuarantineController.cs:102,135`, `TraceController.cs:69`, pack §4.6 design — vs Core rule 3; CT proposal: a declared either-key attribute like Platform `[HasAnyPermission]`, FIX-LIVE3 addendum).


## 20. Follow-up Items

- **FU-LE1-01 (shared UI owner):** after a failed first list load the DataTables processing indicator stays visible (shared CSS, protected).
- **FU-LE1-02 (platform):** a real seed token is ~34.6 KB (695 permission claims). Every hop that forwards it needs ≥ 64 KB header limits (Prism/Node default 16 KB failed); token size itself is a platform concern.
- **FU-LOC-01 (blocks real use + G1):** the shared Location master (DEC-INV-03) does not exist anywhere — no code, no gateway route, no registry ID. Without it every real posting fails closed (503 `LOCATION_MASTER_UNAVAILABLE`) and the form has no warehouse/location picker; today only the Prism mock answers.
  **Status (2026-10-02): CLOSED** — the location master is MOD-0173-FU02 (in-process, LOC1/INT2/LOC2 accepted); the G1
  runs built real location trees through `POST /api/locations` (5 × 201, run G10G21Z).
- **FU-PM-01 (MDM — blocks real use + G1):** no gateway route `/api/product-master` — MDM (MOD-0290) does not yet expose PRODUCT-MASTER-BUNDLE v1; real postings fail closed (503 `PRODUCT_MASTER_UNAVAILABLE`).
  **Status (2026-10-20, G1 re-measure): PARTIAL** — MDM `ProductMasterController` + gateway route `/api/product-master` → 5059 arrived with SYNC3 (`f656e9548`); the fleet still uses the 4010 bridge and PRODUCT-MASTER v1.1 is `1.1.0-draft.16`. Closes with WP-MVP1-PM-LIVE (owner decision G1-D2 = A).

- **FU-LE-01 (central / PSS — blocks G1):** user↔legal-entity assignment + AuthService issuing `legal_entity_id` (active company; switching = token re-issue). Same gap affects MVP-2 (ProcurementService header fallback) and MVP-6 (MOD-0183 "held"). Retires W-0173-02.

- **FU-INT1-01 (central/gateway owner):** `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs` `KnownDownstreamPorts` lacks 5063/5064/5065/5066 (AGENTS.md §3) — the guard is red on `main`-derived code since before MVP-1; update belongs to the gateway test owner.
- **FU-INT1-02 (before G1):** a caller sending a non-UUID `X-Correlation-Id` (e.g. `req-1`) gets 400 from this service; decide with consumers (MOD-0142 GRN, WMS) whether to accept-and-replace instead.
  **Owner decision G1-D3 (2026-10-20) = A:** a non-UUID `X-Correlation-Id` is accepted and replaced by a new UUID (the original value logged, never trusted as an id) instead of 400 → implemented in WP-MVP1-FIX-LIVE3 (addendum).

- **FU-S2-01 (low, before G1):** the startup SKU-position rebuild counts scope-incomplete groups (`LastSkippedUnscoped`) but nothing logs it — a skipped group would be silent. Log a warning with the count (and fail the startup gate if > 0 outside test databases).
  **Status (2026-10-20, G1 re-measure): CLOSED** in WP-0173-K1 (§19: the startup gate logs the skipped scope-incomplete groups as WARNING and fails closed outside test databases — `InventoryIndexInitializerHostedService.cs:45,59-74`); this row was never marked.

- **FU-RSV-01 (blocks G1 reservation item; found by the CT G1 live run 2026-10-02):** `inventory.reservations.manage`
  can never be granted to a tenant role. The key exists only on the API (`InventoryController` `[HasPermission]`); the
  only way a non-Platform service's key reaches the Auth permission catalog is the self-registration manifest, which
  mirrors UI buttons only (module-self-registration-standard §2) — and reservations have no UI (consumers MOD-0175/0177
  call the API). Platform's `[HasPermission]` auto-registration (A1) reflects the Platform assembly only. Measured: owner
  granted "all Inventory" to Admin, re-login, token (runs G1X9U4R, G1Y13QY) still carries 4/5 `inventory.*` keys; reserve
  and release → 403. Mechanics stay proven by the test suite (S2 acceptance) but not live. Fix needs a decision on a
  shared seam (manifest API-only permission entries, or a per-service A1 sync) — not an in-module invented action.
  **Status (2026-10-02): CLOSED — CT measured live (run G10G21Z), see §19 WP-0173-RSV1.** WP-0173-RSV1 (`c0abc6728`): manifest `ApiPermissions`
  (standard §2d); the key is in the AuthService catalog as Module `inventory-ledger`, Scope Tenant (measured). Open for
  CT: role grant on the tenant role screen, re-login, reserve/release 403 → 201.

- **FU-S1-01 (before G1 freeze):** `GET /availability` for a *known* item/SKU with no ledger rows returns
  404 `UNKNOWN_ITEM` (ASSUMPTION-S1-04). Consumers (0172 ATP, 0189 MRP) must see 200 with zeros; 404 only when
  PRODUCT-MASTER says the item/SKU does not exist (fail-closed 503 when it is unreachable).
  **Status (2026-10-20, G1 re-measure): CLOSED** in WP-0173-S2 (§19: no ledger rows → PRODUCT-MASTER decides, `GetAvailabilityHandler.cs:25-26,62-80`).
- **FU-S1-02 (before G1 freeze):** the template's `SuperAdmin` role bypass lets that role post stock movements
  without `inventory.movements.create`. For a GxP stock SoR, mutations require the explicit permission;
  review with security-agent.
  **Status (2026-10-20, G1 re-measure): CLOSED** in WP-0173-S2 (no SuperAdmin bypass, `PermissionAuthorizationHandler.cs:8-10`).

- S2 Reservations; S3 Valuation (requires additive `INVENTORY-BUNDLE` v1.1 for any valuation read surface).
- INT-1: gateway route + permission seed (integration-agent).
- MovementReason validation against MOD-0048; posting-period master (`POSTING_PERIOD_CLOSED`); negative-stock
  policy exceptions per company/warehouse/item (DEC-INV-12).
- Multi-LE operator override (`X-Legal-Entity-Id`) — interim W-0173-02 (§19).
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
