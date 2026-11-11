---
id: MOD-0174
name: Lot/Batch/Serial Tracking
domain: supply-chain-execution
service: Diten.InventoryService (5066, in-process with the MOD-0173 ledger — domain-config OD-5) + frontend/Diten.Web
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: Lots
    data_mode: server
  - module: Serials
    data_mode: server
  - module: LotMovements
    data_mode: server
  - module: LotMigration
    data_mode: server
entity_base: EntityBase
status: ready-for-dev
status_note: "ready-for-dev 2026-10-05 (owner). Was draft — WP-0174-P0 (CT-MVP1-0174-P0 v1), 2026-10-05. Owner decisions K-A…K-G (2026-10-05) recorded in §19 as DECISIONS. CT decisions CT-D1…CT-D8 TAKEN 2026-10-05 (§19; CT-D1 = option a, owner). WP-0174-P0 CT-ACCEPTED 2026-10-05. TRACE v1.1 proposal: docs/analysis/contracts/drafts/trace-v1.1-proposal.md. DCP-002 verifier re-run pending (no Python — waiver W-0173-01 pattern)."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0174-lot-serial-tracking
started: 2026-10-05
target: 2026-10-31
form_field_count: 9
dependencies: [MOD-0173, MOD-0173-FU01, MOD-0173-FU02, MOD-0290, TRACE-v1, INT-TRACE, DEV-0001]
---

# MOD-0174 — Lot/Batch/Serial Tracking

> **Identity (DCP-002):** `MOD-0174 · Lot/Batch/Serial Tracking` is a Blueprint module, registered
> `reserved / planned` (`execution/registries/module-id-registry.md:272`: "DCP-002 gate OK 2026-09-11. DCP-009. SoR =
> lot/serial/genealogy."); DCP-009 records the preflight "MOD-0173/0174/0178 exit 0" (DCP-009 lines 24, 106). **Not
> re-measured here:** no Python interpreter on the MVP-1 machine, so `python3 .antigravity/scripts/verify_module_id.py .
> --check-id MOD-0174 --name "Lot/Batch/Serial Tracking"` could not run → ready-for-dev item (§18), same waiver pattern
> as W-0173-01. No registry row is written by this pack (CT action at acceptance).
>
> **Authority:** this pack > `domain-config.md` > `AGENTS.md` > `.antigravity/`. Owner decisions **K-A…K-G
> (2026-10-05)** are binding (§19 "Owner decisions"); where the approved mockup "Lot Seri Takibi v2 (offline).html"
> disagrees with them, K-* wins and the difference is listed (§21.3). Wire authority: `docs/analysis/contracts/trace.openapi.yaml`
> (TRACE-BUNDLE **v1.0.0, FROZEN**, owner MOD-0174) — implemented as is; everything new is the additive **v1.1
> proposal** `docs/analysis/contracts/drafts/trace-v1.1-proposal.md` (written into the yaml in P1 only after CT accepts it).
> `INVENTORY-BUNDLE` v1 (MOD-0173, FROZEN) is consumed and **not** changed — see CT-D1 for the one semantic question.

## 1. Module Summary

MOD-0174 is the **single source of lot and serial identity**: every lot (`Lot`), every serial number (`SerialNumber`)
and every lot-to-lot transformation (`LotGenealogyLink`) of a tenant. It ties every stock movement of the MOD-0173
ledger to a registered lot / serial (inside the same posting transaction — in-process, like the MOD-0173-FU02 location
master), owns the lot lifecycle (Draft → Released ⇄ Blocked, Expired by the system), the change history (GxP audit
trail), and forward / backward traceability. It retires waiver **W-UI-02** (typed lots in the Stock Ledger form) by
migrating every typed lot text to a registered lot without changing a single ledger transaction.

MVP-1 chain: `0290 → 0173 → 0174 → {0175 ∥ 0176 ∥ 0177}`. MOD-0174 leaves **extension points only** for quarantine
release / e-signature (0175), FEFO (0176) and recall (0177); it designs none of them.

### Slice plan (each slice = its own Work Package, CT-verified before the next)

| Slice | Scope | Contract | Closes |
|---|---|---|---|
| **P1 — Lot / serial SoR** | `Lot`, `LotChangeEntry`, `SerialNumber`, `LotGenealogyLink` entities + `LotIdentityPolicy` (K-G single point); TRACE v1 ops + v1.1 ops; status lifecycle + reason + history; expiry sweeper; GS1 check; manifest `LOT-SERIAL-TRACKING` | TRACE v1 implemented; v1.1 written into the yaml (single-writer step, after CT accepts the proposal) | lot SoR, history, K-C, K-E, K-G, B-3 |
| **P2 — Ledger integration** | in-process `ILotMasterClient` for the ledger; lot required / allowed / registered / same SKU / not Blocked / not Expired; serial registry + count (= quantity) + uniqueness + consumption; `inventory_lot_positions` (K-A); availability rule per CT-D1 | INVENTORY-BUNDLE v1 unchanged (new `error.code` values only) | lot+serial posting invariants (report §4 R6/R7) |
| **P3 — UI** | 8 screens (§11) + the Stock Ledger form lot field / quick lot / serial entry (3 methods, K-B) | — | G1 E4 UI leg for lots |
| **P4 — Migration** | "Elle girilmiş lotları aktarma" screen + commit; typing closed | v1.1 migration ops | **W-UI-02 closed** |
| **P5 — L10n** | 7 languages for every key of §11.6 | — | language parity |
| **INT-TRACE** | Gateway `/api/trace/**` → 5066 + permission catalog check (integration-agent) | — | external consumers (0142, 0175–0177, 0194) |

Order: P1 → INT-TRACE → P2 → P3 → P4 → P5 (P5 may run with P3/P4 per file). P2 needs CT-D1.

## 2. Ownership and Boundaries

**Owns (single writer, K15):**

- `TRACE-BUNDLE` contract (v1 frozen, v1.1 proposal) and its implementation under `/api/trace`.
- `Lot`, `LotChangeEntry`, `SerialNumber`, `LotGenealogyLink`, `MovementLotLink`, `ManualLotMigration`.
- Lot status lifecycle (`Draft · Released · Blocked · Expired`), serial status (`Active · Consumed · Blocked`).
- Lot / serial identity policy (`LotIdentityPolicy`, K-G) and its migration path (§8).
- Permission namespace **`trace.*`** (proposed, PKS-001 §4 — measured: no `trace.` key exists in `services/`,
  `gateway/`, `execution/`, `.antigravity/` on `876805f0e`).

**Consumes:**

- MOD-0173 ledger **in-process** (same service): balance rows `?lotId=`, transactions `?lotId=`, the per-lot projection
  `inventory_lot_positions` (written by the ledger code, §8). Never a second balance.
- `PRODUCT-MASTER-BUNDLE` v1 (MOD-0290, Model A only): `lotControlled`, `serialControlled`, `gtin` (nullable — D-1),
  `resolveSkuProduct` (SKU → itemId). Fail-closed 503 `PRODUCT_MASTER_UNAVAILABLE`.
- Company scope: MOD-0173 scope middleware + W-0173-02 company selector (FU01 component, reused as is).

**Does not own / must not do:**

- Stock quantities, stock status, movements, reservations (MOD-0173). Quarantine hold / release / e-signature
  (MOD-0175). Shelf-life rules, FEFO (MOD-0176). Recall cases, drills, evidence packs (MOD-0177). BOM / work orders
  (MOD-0193/0194 — they **call** `linkGenealogy`). Shipment / customer (MVP-6).
- Model B (`Product`, `mdm_products`, `/api/mdm/products`) — never.
- The frozen yaml files (`trace.openapi.yaml` is edited only in P1 after CT acceptance of the proposal;
  `inventory-bundle.openapi.yaml` never), registries, DCP, domain-config, report.

## 3. Owned Objects

| Object | Kind | Notes |
|---|---|---|
| `Lot` | Entity (`EntityBase`) | collection `trace_lots`; business key per `LotIdentityPolicy` (K-G) |
| `LotChangeEntry` | Entity (append-only) | `trace_lot_history`; one row per created lot, per changed field, per status change, per link / migration |
| `SerialNumber` | Entity (`EntityBase`) | `trace_serials`; created by an inbound posting (P2) |
| `LotGenealogyLink` | Entity (append-only) | `trace_genealogy_links`; written via API by MOD-0194 later |
| `MovementLotLink` | Entity (append-only) | `trace_movement_lot_links`; typed-lot transaction → registered lot (P4) |
| `ManualLotMigration` | Entity (one per tenant) | `trace_lot_migrations`; the commit record + `ManualLotEntryClosed` flag |
| `LotIdentityPolicy` | Domain service | the ONLY place that knows "a lot belongs to a SKU" and the uniqueness key (K-G rule 1) |
| `LotStatusRules` | Domain rules | allowed transitions, `ILotStatusChangeGate` extension point (0175) |
| `Gs1CodeParser`, `SerialRangeExpander`, `ManualLotMatcher` | Domain helpers | K-B, K-B range, K-D |
| `InventoryLotDirectory` | in-process adapter | implements the ledger's new `ILotMasterClient` from the repository (pattern `InventoryLocationDirectory`) |
| `LotExpirySweeper` | hosted service | sets `Expired` (B-3) |
| `TraceController` (service) | API | base `/api/trace` (contract `servers`) |
| `LotSerialTrackingManifestProvider` | self-registration | module code `LOT-SERIAL-TRACKING` (§14) |
| MVC controllers (Web) | `LotsController`, `SerialsController`, `LotMovementsController`, `LotTraceController`, `LotMigrationController` | `[Authorize]`; pages + same-origin proxies |
| Permissions | `trace.lots.read`, `trace.lots.create`, `trace.lots.update`, `trace.lots.update-released`, `trace.lots.change-status`, `trace.lots.migrate`, `trace.serials.read`, `trace.genealogy.link` | §14 |

## 4. Entity Fields

**Entity base (decision):** the MOD-0173 `EntityBase` of `Diten.InventoryService`
(`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Entities/EntityBase.cs:9` — `Id`, `TenantId`,
`LegalEntityId`, `CreatedAt`, `UpdatedAt`, `Version` (:22, ETag), `IsDeleted`, `DeletedAt`; `CreatedBy`/`UpdatedBy` are
added per entity, as `Location`). **Why:** same service, same scope middleware, same Mongo transaction as the ledger
posting; `Version` gives `If-Match`. **No delete:** a lot or serial is never deleted — `IsDeleted` exists for the
standard and is always `false`; no delete route or repository method (architecture test). A wrong lot is corrected by
editing (K-C) or blocking, never by deletion (GxP).

### `Lot` (`trace_lots`) — mapping to `LotRef` (v1 + v1.1)

| Field | Type | Rule | `LotRef` |
|---|---|---|---|
| `Id` | string (Guid "D") | surrogate, immutable — what the ledger stores in `LotId` after P2 | `lotId` |
| `TenantId` | Guid | from the scope | — |
| `LegalEntityId` | Guid | the company that **created** the lot (audit); visibility per CT-D3 | `legalEntityId` (v1.1) |
| `ItemId` | Guid | **always filled** — resolved from the SKU through PRODUCT-MASTER (K-G rule 2) | `itemId` |
| `SkuId`, `SkuLevel` | Guid, `Gsku · Lsku · FinishedGood` | set at create, **immutable** (owner: Lot No and SKU locked even in Draft) | `skuId`, `skuLevel` (v1.1) |
| `LotNumber` | string 1–40 `^[A-Z0-9./-]+$` | trimmed + upper-case (same normalisation as W-UI-02 — `frontend/Diten.Web/Models/Inventory/MovementRules.cs:77,280`); immutable | `lotNumber` |
| `IdentityKey` | string | built ONLY by `LotIdentityPolicy` (today `{TenantId}|{SkuLevel}|{SkuId}|{LotNumber}`); unique index | — |
| `ManufacturerLot`, `SupplierLot` | string 1–40? | attributes, not keys (DEC-INV-14) | same |
| `ManufacturingDate`, `ExpiryDate`, `RetestDate` | date (UTC midnight)? | §12 date rules | same (date-time) |
| `CountryOfOrigin` | ISO 3166-1 alpha-2? | UI shows the localized country name | `countryOfOrigin` |
| `CoaReference` | string 1–60? | | `coaReference` |
| `Status` | `Draft · Released · Blocked · Expired` | new lots `Draft`; §12 transitions | `status` (v1 enum, unchanged) |
| `TrackingMode` | `Lot · LotAndSerial` | from PRODUCT-MASTER flags at create | `trackingMode` (v1.1) |
| `CreatedBy`, `UpdatedBy` | string | JWT `sub` | — |
| `CreationSource` | `LotForm · LedgerShortcut · Api · Migration` | | history `source` |

Indexes: unique `(TenantId, IdentityKey)`; `(TenantId, SkuId, SkuLevel, ExpiryDate)`; `(TenantId, ItemId)`;
`(TenantId, Status, ExpiryDate)` (sweeper); text-ish prefix `(TenantId, LotNumber)`.

**Form fields (9 → Compact):** ① Ürün / SKU* (create only) ② Lot No* (create only) ③ Üretici Lotu ④ Tedarikçi Lotu
⑤ Üretim Tarihi ⑥ Son Kullanma (SKT) ⑦ Yeniden Test Tarihi ⑧ Menşe Ülke ⑨ CoA Referansı — plus **Gerekçe** on the edit
form of a non-Draft lot (conditional, not counted). Status is not a form field (status dialog).

### `LotChangeEntry` (`trace_lot_history`)

`Id, TenantId, LegalEntityId, LotId, Action (Created · Updated · StatusChanged · Linked · Migrated · SystemExpired),
Field?, OldValue?, NewValue?, Reason?, ActorUserId, ActorDisplay, Source, CorrelationId, LeSource, OccurredAt`.
Written in the **same transaction** as the change; insert-only (no update / delete route — pattern
`LocationRepository.AppendHistoryAsync`, `…/Persistence/Repositories/LocationRepository.cs:243`).

### `SerialNumber` (`trace_serials`)

`Id` (surrogate = TRACE `serialId`), `TenantId`, `LegalEntityId` (receiving company), `ItemId`, `SkuId`, `SkuLevel`,
`LotId?`, `SerialNumber` (3–24 `^[A-Z0-9-]+$` — CT 2026-10-05, = §12; upper-case — mockup rule), `IdentityKey` (by the policy:
Tenant + SKU + SerialNumber, CT-D7), `Status` (`Active · Consumed` stored; `Blocked` **derived on read** when the lot
is Blocked/Expired or the balance row status is BLOCKED/QUARANTINE — mockup note), `WarehouseId?`, `LocationId?`,
`LastTransactionId`, `LastMovementAt`, `ReceivedTransactionId`, `CreatedAt`. Unique `(TenantId, IdentityKey)`.

### `LotGenealogyLink` (`trace_genealogy_links`)

`Id, TenantId, ParentLotId, ChildLotId, TransformationReference, SourceModule, QuantityConsumed?, UomId?,
IdempotencyKey, CreatedBy, CreatedAt`. Unique `(TenantId, ParentLotId, ChildLotId, TransformationReference)`.

### `MovementLotLink` (`trace_movement_lot_links`) and `ManualLotMigration` (`trace_lot_migrations`)

`MovementLotLink`: `Id, TenantId, LegalEntityId, TransactionId, TypedText, LotId, MigrationId, CreatedAt`; unique
`(TenantId, TransactionId)`. `ManualLotMigration`: `Id, TenantId, CompletedAt, CompletedBy, Reason,
TransactionsLinked, LotsLinked, LotsCreated, ManualLotEntryClosed (bool)`; one per tenant (unique `TenantId`).

## 5. Repo Scope

The development WPs may write only:

- `services/Diten.InventoryService/src/**/Traceability/**` (entities, rules, repositories, handlers, `TraceController`,
  `InventoryLotDirectory`, `LotExpirySweeper`), `…/Infrastructure/Authorization/TracePermissions.cs`,
  `…/Api/ModuleRegistration/LotSerialTrackingManifestProvider.cs` (+ one DI line in `Program.cs`), the scope
  middleware base-path list (`/api/trace`), the index startup gate.
- **P2 only** — the ledger hook points in MOD-0173 code, listed exactly: `MovementMasterDataValidator.cs` (lot/serial
  checks), the posting transaction (serial + lot-position writes), `GetAvailabilityHandler.cs` + SKU-position
  maintenance (CT-D1), `InventoryErrorCodes.cs` (new codes). No change to any INVENTORY-BUNDLE path/schema.
- `services/Diten.InventoryService/tests/**`.
- `docs/analysis/contracts/trace.openapi.yaml` — **P1 only, single-writer additive v1.1 step**, after CT accepts the proposal.
- `frontend/Diten.Web/Controllers/{Lots,Serials,LotMovements,LotTrace,LotMigration}Controller.cs`,
  `frontend/Diten.Web/Models/Traceability/**`, `Views/Traceability/**`, `wwwroot/assets/js/Traceability/**`,
  `Resources/Views/Traceability/**` (7 languages), `SharedResource.{7}.resx` — **only** `Nav.Page.*` / module keys of §14.
- **P3 only** — the FU01 Stock Ledger form files for the lot field: `Views/Inventory/StockMovements/_Form.cshtml`,
  `wwwroot/assets/js/Inventory/StockMovements/form.js`, `Models/Inventory/MovementRules.cs`,
  `Models/Inventory/MovementFormSources.cs`, their resx (lot / serial keys only) and the INVENTORY-LEDGER manifest
  action `NEWLOTQUICK` (CT-D5).
- `frontend/Diten.Web.Tests/**`, `docs/records/audits/<yyyy-mm>/mod-0174-*`, this pack (§19 only after ready-for-dev).

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, `CLAUDE.md`, `execution/registries/**`, `execution/portfolio/**`, `domain-config.md`,
  the inventory report, the MOD-0173 / FU01 / FU02 packs (proposals go to the CT).
- `docs/analysis/contracts/inventory-bundle.openapi.yaml`, `product-master-bundle.openapi.yaml`, `location.openapi.yaml`,
  every other contract; `trace.openapi.yaml` except the P1 step above.
- `gateway/**` (INT-TRACE, integration-agent).
- MdmService, Platform, AuthService, ProcurementService (its createLot mock stays until MVP-2 wires it), every other service.
- `_LayoutTenantShell`, `_Layout`, shared JS/CSS (`diten-datatable.js`, `dt-defaults.js`, `backbone-custom.css`,
  `_ListShell`, `_TableSkeleton`, `_GlobalConfirmation`), the FU01 company-selector files (reused as is).

## 7. Dependencies

| Dependency | State (measured on `876805f0e`) | Effect |
|---|---|---|
| MOD-0173 ledger S1/S2, FU01 UI-1/UI-2, FU02 LOC1/LOC2, RSV1 | CT-accepted (pack §19 records) | same service, scope, `EntityBase`, `ApiPermissions` (standard §2d) |
| TRACE v1 | FROZEN, **no implementation** (no `/lots` route, no TRACE client anywhere — §19 facts F-10) | implemented in P1 |
| PRODUCT-MASTER `lotControlled` / `serialControlled` | read by the ledger today (`…/Infrastructure/Upstream/ProductMasterHttpClient.cs:54-55`) | reused |
| PRODUCT-MASTER `gtin` | `SkuRef.gtin` nullable in the contract; MDM returns null (MOD-0290 D-1, `docs/roadmap/plans/mod-0290-product-master-bundle-requirements-for-mvp1-2026-10-02.md:111`); not read by any client today (F-12) | K-B: GTIN check skipped with a visible warning while null |
| Gateway `/api/trace/**` | **missing** | INT-TRACE before P3 and before any external consumer |
| MOD-0175 / 0176 / 0177 | not started | extension points only (§8) |
| MOD-0194 work orders | MVP-3 | genealogy writer later; backward trace shows receipt source only until then |
| MVP-6 shipment | `feature/mvp6-logistics` | forward trace shows document numbers only (no customer name) until wired |
| Golden Reference Compact (DEV-0001) | live | lists / form / details structure |

## 8. Runtime Constraints

- **Service:** `Diten.InventoryService` (5066) — in-process with the ledger, **not** a separate service (domain-config
  line 64: "Diten.InventoryService · port 5066 · MOD-0173…0177"). Reason: a lot check that is a network call would put a
  second system in the posting's consistency boundary; the FU02 location master proved the in-process pattern
  (`InventoryLocationDirectory`). Base path `/api/trace` (contract `servers`).
- **Scope:** MOD-0173 scope middleware extended to `/api/trace` (one rule set, W-0173-02 included). Lot identity is
  tenant-wide per CT-D3 (recommended); stock figures are always per selected company.
- **Ledger link (in-process, P2):** a new ledger port `ILotMasterClient` (`ResolveLotAsync`, `ValidateSerialsAsync`,
  `RegisterSerialsAsync`, `ConsumeSerialsAsync`) implemented by `InventoryLotDirectory` on the repository, called
  **inside the posting transaction**; lot status is re-read inside that transaction, and every status change writes the
  lot document → a posting racing a block gets a WriteConflict and retries on the committed state (FU02
  `StockTouchSeq` technique).
- **K-A per-lot stock (`inventory_lot_positions`):** `(TenantId, LegalEntityId, LotId) → OnHand (base UoM, all
  statuses), BaseUomId, Version`, upserted by the **ledger posting code** in the same transaction as the balance rows
  (a projection of the same transactions — the single-SoR rule holds; architecture test lists it as a ledger
  projection, like `inventory_sku_positions`). The Lot list reads it; it is never computed per row.
- **Availability of a Blocked / Expired lot — CT-D1 (blocks P2):**
  - (a) *Lot status as a ledger-side projection:* availability (`GetAvailabilityHandler`, today
    `available = Σ OnHand(AVAILABLE) − reserved`, `…/QueryHandlers/GetAvailabilityHandler.cs:77-79`) and the SKU
    position used by reservations subtract the AVAILABLE quantity of Blocked / Expired lots; balance rows keep their
    stock status; postings out of such a lot are refused (`LOT_BLOCKED` / `LOT_EXPIRED`). **+** keeps the owner rule
    "bloke lot ≠ bloke stok"; no ledger noise; unblock is one status change. **−** changes the documented meaning of
    INVENTORY-BUNDLE `available` (`inventory-bundle.openapi.yaml:294` "available = onHand(AVAILABLE) − reserved") without
    a shape change; the lot status change must also adjust the SKU position in the same transaction.
  - (b) *Automatic STATUS_TRANSFER:* blocking a lot posts AVAILABLE→BLOCKED movements for its rows, unblocking posts them
    back. **+** zero contract question; every consumer already understands BLOCKED. **−** breaks the owner/mockup rule
    "the stock status of the balance rows does NOT change"; unblock must remember each row's prior status (QC,
    quarantine …); a reserved quantity makes the transfer fail; lot governance starts to own stock status, which is
    MOD-0175's lifecycle (DEC-INV-08).
  - **Recommendation: (a)**, with the MOD-0173 owner adding a description-only note to INVENTORY-BUNDLE in its own next
    additive step (no shape change). **CT decision required** — neither option is implemented before it.
- **Expiry (B-3):** `LotExpirySweeper` (hosted, every 15 min and at start) sets `Status = Expired` for every lot with
  `ExpiryDate < today (UTC)` in any other status, writes a `SystemExpired` history row ("Sistem (otomatik)"), in a
  transaction per lot. Postings check `ExpiryDate` by date regardless of the sweeper.
- **Status-change extension point (K-E):** `ILotStatusChangeGate` → `Allow` in 0174. MOD-0175 may wrap Draft→Released /
  Blocked→Released with approval + e-signature (DEC-INV-08) by returning `PendingApproval`; the contract reserves 202.
- **Extension points for 0176 / 0177:** `listLots(expiryBefore, sort=expiryDate)` + `expiryState` (FEFO input);
  `getGenealogy(depth)` + `getTransactions?lotId=` (recall trace); no FEFO or recall logic here.
- **K-G single policy point and its future migration:** `LotIdentityPolicy` (Domain) is the only code that (1) decides
  which SKU levels a lot may bind to (today all three), (2) builds `IdentityKey`, (3) normalizes `LotNumber`. Repositories,
  indexes and handlers call it; an architecture test fails if another class builds a lot filter on `LotNumber`.
  **Steps to move to "Lsku only" (or another scope) later:** ① add `IdentityKeyV2` to the policy (new scope) without
  switching; ② backfill job writes `IdentityKeyV2` for every lot and reports collisions (two lots that would merge) to
  the owner — no automatic merge; ③ owner resolves collisions (block + relabel, or explicit merge WP); ④ build the unique
  index on `IdentityKeyV2`; ⑤ flip the policy version (one config/code switch); new lots on a non-allowed SKU level →
  422; ⑥ drop the V1 index; ⑦ additive TRACE note + behaviour-change record (like B-1). Ledger rows are untouched (they
  store the surrogate `LotId`).
- **Migration re-key (P4, CT-D6):** the ledger stores the typed text as `LotId`
  (`…/Domain/Entities/InventoryTransaction.cs:48`) and balances are keyed by it (`…/ValueObjects/InventoryDimension.cs:17`,
  upsert filter `…/Repositories/InventoryLedgerRepository.cs:401`). The commit writes `MovementLotLink`s and rebuilds the
  affected `inventory_balances` / `inventory_lot_positions` rows under the registered `LotId` in the same transaction
  (projection rebuild — DEC-INV-02 allows it; transactions untouched). Reads (`getTransactions?lotId=`) return linked
  rows under the registered lot.
- **Idempotency / concurrency / correlation:** MOD-0173 rules (Idempotency-Key = body key, replay 200, reuse 400
  `IDEMPOTENCY_KEY_REUSED`); lot reads return `ETag: W/"<Version>"`; PATCH and status require `If-Match` (409
  `LOT_CONFLICT`); correlation id on every history row.
- **Browser** talks only to same-origin MVC; MVC calls Gateway 5000 through `InventoryProxy.Gateway` (company header
  rule included). No browser call to 5066 or `/api/trace`.

## 9. Layout & Shell Contract

- `shell: tenant` → every page sets `Layout = "_LayoutTenantShell";` explicitly (grep = 8 pages:
  `Views/Traceability/Lots/{Index,Details,Create,Edit}.cshtml`, `Serials/Index`, `LotMovements/Index`,
  `LotTrace/Index`, `LotMigration/Index`). Partials set no layout.
- Folders under `Views/Traceability/` (ASSUMPTION-0174-01): the module is not INVENTORY-LEDGER, so it does not share
  `Views/Inventory/`.
- Routes are tenant routes (no `/Platform/…`) → permission scope Tenant (standard §2c).
- Company selector partial (FU01) under the page header on every page; without a ready company the data part is not
  rendered (LE1 behaviour). Lot identity is tenant-wide (CT-D3), stock figures follow the selected company.
- UAS-001: a user without the page permission gets `_AccessDenied` in the shell — no skeleton, no redirect.
- 7 languages; `ar` RTL; values inside sentences as bidi isolates; quantities in the reader's number format (FU01 UI2-19/20).

## 10. Backend File Convention

```text
services/Diten.InventoryService/src/
├── Diten.InventoryService.Domain/Traceability/
│   ├── Entities/ Lot.cs, LotChangeEntry.cs, SerialNumber.cs, LotGenealogyLink.cs, MovementLotLink.cs, ManualLotMigration.cs
│   ├── Enums/ LotStatus.cs, SerialStatus.cs, LotTrackingMode.cs, LotChangeAction.cs
│   ├── Rules/ LotIdentityPolicy.cs (K-G), LotStatusRules.cs, LotDateRules.cs, Gs1CodeParser.cs, SerialRangeExpander.cs, ManualLotMatcher.cs
│   └── Repositories/ ILotRepository.cs, ISerialRepository.cs, IGenealogyRepository.cs, IManualLotMigrationRepository.cs
├── Diten.InventoryService.Application/Features/Traceability/
│   ├── Commands/ (CreateLot, UpdateLot, ChangeLotStatus, LinkGenealogy, CommitManualLotMigration) + handlers + validators
│   ├── Queries/  (GetLot, ListLots, GetLotDetail, GetLotHistory, GetSerial, ListSerials, GetGenealogy, ValidateTrace, CheckGs1Code, ListManualLotGroups, GetMovementLotLinks)
│   ├── Ports/ ILotStatusChangeGate.cs (0175 extension point)
│   ├── TraceModels.cs (LotRef v1 + v1.1 wire shapes) · TraceErrorCodes.cs
├── Diten.InventoryService.Application/Common/Upstream/ILotMasterClient.cs   (ledger port, P2)
├── Diten.InventoryService.Persistence/Repositories/ LotRepository.cs, SerialRepository.cs, GenealogyRepository.cs, ManualLotMigrationRepository.cs, InventoryLotDirectory.cs
├── Diten.InventoryService.Infrastructure/ Authorization/TracePermissions.cs · Jobs/LotExpirySweeper.cs
└── Diten.InventoryService.Api/ Controllers/TraceController.cs ([Route("api/trace")], [HasPermission(TracePermissions.*)]) · ModuleRegistration/LotSerialTrackingManifestProvider.cs
```

One type per file; Domain without Mongo; no generic repository that could drop the Tenant predicate. Indexes are added to
the startup index gate (MOD-0173 pattern). Wire format: unwrapped TRACE schemas + `contractVersion: "v1"` (MOD-0173 §8
exception, limited to `/api/trace`).

## 11. Frontend File Contract

### 11.1 Screen → route → page (every mockup screen)

| Mockup screen | Route | PageCode | Kind | Page permission |
|---|---|---|---|---|
| Lot Gezgini | `/Lots` | `LOTS` | DataTable list, server mode | `trace.lots.read` |
| Lot Detayı | `/Lots/Details?lotId=` | `LOTDETAILS` | single record, sections (Compact Details) | `trace.lots.read` |
| Yeni Lot | `/Lots/Create` | `LOTCREATE` | form + summary (Compact) | `trace.lots.create` |
| Lot Düzenle | `/Lots/Edit?lotId=` | `LOTEDIT` | the same form + changes panel + reason | `trace.lots.update` (MVC gate also admits `…update-released`, ASSUMPTION-0174-08) |
| Durum Değiştir | — (dialog on Lots / Details) | — | confirmation dialog `window.showConfirm` | action `CHANGELOTSTATUS` |
| Seri Gezgini | `/Serials` | `SERIALS` | DataTable list | `trace.serials.read` |
| Lot Hareket Geçmişi | `/LotMovements?lotId=&serial=` | `LOTMOVEMENTS` | DataTable list (+ receipt modal) | `trace.lots.read` (+ `inventory.stock.read` for the rows — MVC proxies `getTransactions`) |
| İzlenebilirlik | `/LotTrace?lotId=` | `LOTTRACE` | custom 2+2 level flow (not a DataTable) | `trace.lots.read` |
| Stok Defteri · Yeni Hareket (lot alanı) | existing `/StockMovements/Create`, `/StockLedger/Create` (FU01) | existing | FU01 form, lot field changed (§11.4) | existing `inventory.movements.create` |
| Elle Girilmiş Lotları Aktarma | `/LotMigration` | `LOTMIGRATION` | DataTable list + decision dialogs + confirm | `trace.lots.migrate` |

Pop-ups of the mockup: "Yeni lot" quick dialog (ledger shortcut and migration "Yeni lot olarak oluştur"), "Mevcut lota
bağla" / "Yazım hatası" pick dialog, "Aktarımı onayla", "Hareket fişi" — all modals on the pages above, not routes.

### 11.2 Files

```text
Views/Traceability/
├── Lots/        Index.cshtml · Details.cshtml · Create.cshtml · Edit.cshtml · _Form.cshtml · _Filter.cshtml · _DataTable.cshtml ·
│                _StatusDialog.cshtml · _LotQuickCreateModal.cshtml (shared with LotMigration + Stock Ledger) · _IndexL10n.cshtml · LotsIndex.cs
├── Serials/     Index.cshtml · _Filter.cshtml · _DataTable.cshtml · _IndexL10n.cshtml · SerialsIndex.cs
├── LotMovements/ Index.cshtml · _Filter.cshtml · _DataTable.cshtml · _ReceiptModal.cshtml · _IndexL10n.cshtml · LotMovementsIndex.cs
├── LotTrace/    Index.cshtml · _FlowColumn.cshtml · _IndexL10n.cshtml · LotTraceIndex.cs
└── LotMigration/ Index.cshtml · _DataTable.cshtml · _PickLotModal.cshtml · _ConfirmModal.cshtml · _IndexL10n.cshtml · LotMigrationIndex.cs
wwwroot/assets/js/Traceability/{Lots,Serials,LotMovements,LotTrace,LotMigration}/{index.js, index.l10n.js} + Lots/{form.js, status.js, quick-create.js}
Resources/Views/Traceability/{Lots/LotsIndex, Serials/SerialsIndex, LotMovements/LotMovementsIndex, LotTrace/LotTraceIndex, LotMigration/LotMigrationIndex}.{en,tr,fr,es,zh,ar,ru}.resx
```

Rules (FU01 binding rules carried): lists via `DitenDataTable.createList({ dataMode: 'server' })` only, `stateSave:
false`, `_ListShell` with literal `HasSelection = false` (no bulk anywhere), row actions via `renderActions` /
`bindActionDispatcher` with `{ key: … }` objects (completeness test), no QuickView, `_TableSkeleton` / card skeletons,
no import / export buttons (export gate), confirmations only through `window.showConfirm` (no raw `Swal.fire`).

### 11.3 Screen contents (from the mockup; K-* applied)

- **Lot Gezgini** — filters: search (lot no, product, manufacturer / supplier lot), product, status (4), "SKT şu
  tarihten önce" (`expiryBefore`), "Yalnız uyarısı olanlar" (`warn`), Temizle. Columns: Lot No · Ürün (name + code) ·
  Üretici Lotu · Tedarikçi Lotu · Üretim · SKT (pill: past = red "Doldu", ≤ 90 days = orange "N gün") · Yeniden Test (own
  icon when past) · Menşe · Durum (badge) · CoA Ref. · **Eldeki Stok** (K-A, from `stockSummary`) · Aksiyonlar. Toolbar
  "Yeni Lot". 10 rows per page (server paging).
- **Lot Detayı** — header (lot no, product, status badge, warnings: SKT / retest), buttons (§14). Sections: Eldeki stok
  (total); Lot bilgileri (11 fields incl. Takip and Kaynak); Stok dağılımı (ledger balance rows of the lot: Depo ·
  Lokasyon · Stok durumu · Miktar; Blocked/Expired lot banner "Lot bloke … stoğun tamamı kullanılamaz"); Seri numaraları
  (first 8 + "Seri Gezgini'nde tümü"; serial-tracked only); Değişiklik geçmişi (Tarih · Kim · Alan · Eski → Yeni · Gerekçe;
  "kayıtlar silinemez").
- **Yeni Lot / Düzenle** — 9 fields; summary card (product, tracking, lot no, status "Taslak"); duplicate check live
  (`Bu ürün için bu lot numarası zaten var`); edit of a non-Draft lot: fields locked until the reason has ≥ 10 characters
  (counter "n/10"), "Kaydedilecek değişiklikler" (old → new); Lot No + SKU read-only in edit.
- **Durum Değiştir** — current status; options with allowed / disallowed reason ("Sistem verir; elle seçilemez" for
  Expired); Blocked = danger + impact line ("N birim · M lokasyon · depolar") + required acknowledgement checkbox;
  reason ≥ 10 (K-* — mockup said 5, §21.3).
- **Seri Gezgini** — filters: serial no, product, lot (dependent), status (Aktif · Tüketildi · Bloke). Columns: Seri No ·
  Ürün · Lot No · Durum · Depo / Lokasyon · Son Hareket · actions (Lot Detayı, Hareket Geçmişi). 15 rows per page.
- **Lot Hareket Geçmişi** — lot selector; optional serial chip (from Seri Gezgini). Columns = FU01 S-2 history columns
  filtered to the lot: İşlem no · Hareket türü (+ "Otomatik" badge when `sourceModule ≠ MOD-0173`) · Miktar (± colour) ·
  Nereden → Nereye · Durum · Kaynak · İşlem tarihi · Kaydeden. Row → receipt modal (Hareket fişi) with "Stok Defteri'nde aç".
- **İzlenebilirlik** — lot selector; "Etkilenen toplam miktar" (on hand + shipped, **per unit, never summed across
  units**); stat cards (Depoda · Sevk edilmiş · Etkilenen lot); backward 2 levels, forward 2 levels; clicking a lot card
  re-roots the flow. **MVP-1 reality:** backward = the receipt / opening source of the lot (until MOD-0194 writes
  genealogy); forward = consuming lots from `LotGenealogyLink` + outbound ledger documents shown as **document numbers
  only** (no customer name until MVP-6).
- **Elle Girilmiş Lotları Aktarma** — §11.5.

### 11.4 Stock Ledger form — lot field (P3, FU01 form)

- Lot picker **only for inbound** types (Açılış bakiyesi / OPENING_BALANCE, Sayım fazlası / COUNT_GAIN — the manual
  inbound types of FU01). Outbound and transfers: the lot comes from the chosen source balance row (FU01 rule,
  `frontend/Diten.Web/Models/Inventory/MovementRules.cs:235`), shown read-only.
- Picker = lots of the selected SKU, sorted by SKT; each option "lot no · SKT · status"; Blocked and Expired options
  shown **disabled** with the reason; retest-past lot = warning + explicit confirmation on selection (K-F).
- "Yeni lot oluştur" → `_LotQuickCreateModal` (same 9-field rules, SKU fixed); saved lot is **Draft** and selected.
- Non-lot-tracked product: no lot field at all. Lot-tracked: no save without lot. Serial-tracked: serial panel.
- **Serial entry, three methods (K-B):** Tek tek (Enter adds; GS1 text accepted), Aralık (start/end, same prefix and digit
  count, end ≥ start, ≤ 5,000, preview "N seri: A … B"), Barkod (karekod) via `POST /api/trace/serials/gs1-check`
  (all four AIs checked; GTIN skipped only when the SKU's GTIN is null — visible warning banner; lot and SKT always
  checked; a mismatching code is **not** added and the message names the field). Counter "n / quantity" (green when
  equal); duplicates skipped with a message; invalid format listed.
- The typed-lot input (W-UI-02) and the "unregistered lot" badges (`Details.cshtml:147-149`, `_Form.cshtml:387`,
  `StockMovements/index.js:381`, `StockLedger/index.js:263`) are **removed in P4** when `ManualLotEntryClosed`; until
  then typed text is accepted only for rows that still hold an unmigrated typed lot.

### 11.5 Elle Girilmiş Lotları Aktarma (P4)

- Banner: "Aktarım geçmiş hareketleri **değiştirmez**; yalnız lot bağlantısını ekler." After completion: "Tamamlandı ·
  date · user · X hareket Y lota bağlandı (N yeni lot, Taslak). Stok Defteri'nde elle lot yazma bu andan itibaren kapalı."
- KPI: Toplam satır · Eşleşen · Bekleyen (with movement counts). Tabs: Tümü · Bekleyen · Eşleşen.
- Rows = one per distinct **SKU + typed text** (K-D): Ürün · Elle Yazılan Lot · Hareket · Eldeki Stok · Önerilen
  Eşleşme · Aksiyon. Suggestion rule (`ManualLotMatcher`): same number ignoring case and leading/trailing spaces → "Mevcut
  lota bağla" (exact); same after removing `-` and spaces → "Yazım farkı olabilir" (to a lot or to an earlier row, K-D:
  L-001 / L001); otherwise "Yeni lot".
- Row actions: Öneriyi uygula · Mevcut lota bağla · Yeni lot olarak oluştur (quick modal, lot no prefilled with the
  normalized text — must pass the lot-number format, otherwise the user edits it) · Yazım hatası — başka lota bağla ·
  Değiştir. Toolbar: Tüm önerileri uygula · Aktarımı Onayla (disabled while rows are pending).
- Confirm dialog: danger; "X hareket Y lota bağlanacak" (existing vs new lots), required reason ≥ 10, irreversible
  wording; after commit the screen is read-only.
- Decisions live in the page until commit (no server draft, ASSUMPTION-0174-11); the commit carries all decisions +
  `expectedGroupCount` (stale → 409 `MIGRATION_STALE`, reload).

### 11.6 Resx keys (P5 translates; key list is binding, values are not written in this WP)

Per page file `{Page}Index.{en,tr,fr,es,zh,ar,ru}.resx`:

- **Common (each file):** `Page.Title`, `Page.Breadcrumb`, `Company.Required`, `Error.Generic`, `Error.Correlation`,
  `Status.Draft`, `Status.Released`, `Status.Blocked`, `Status.Expired`, `Serial.Status.Active`,
  `Serial.Status.Consumed`, `Serial.Status.Blocked`, `StockStatus.{Available,QualityInspection,Quarantine,Blocked,InTransit,Damaged,Expired}`,
  `Expiry.Past`, `Expiry.Days`, `Retest.Past`, `Action.*` for each action label of §14, `Empty.*`, `Paging.Range`.
- **LotsIndex:** `Filter.Search`, `Filter.Product`, `Filter.Status`, `Filter.ExpiryBefore`, `Filter.WarnOnly`,
  `Filter.Clear`, `Col.{LotNumber,Product,ManufacturerLot,SupplierLot,ManufacturingDate,ExpiryDate,RetestDate,Origin,Status,CoaRef,OnHand,Actions}`,
  `Detail.{OnHand,Info,StockDistribution,StockDistributionSub,Serials,SerialsAll,History,HistorySub,Unusable.Blocked,Unusable.Expired}`,
  `Info.{Tracking,Source,Source.GoodsReceipt,Source.Production,Source.Opening,Source.Manual,Source.Migration}`,
  `Tracking.{Lot,LotAndSerial,None}`, `Form.{TitleNew,TitleEdit,Product,LotNumber,ManufacturerLot,SupplierLot,ManufacturingDate,ExpiryDate,RetestDate,Country,Coa,Reason,ReasonHint,ReasonCounter,Summary,Changes,NoChanges,LockedBanner,DraftNote,Duplicate}`,
  `Validation.{Required,LotFormat,DatesOrder,ReasonMin}`, `Status.Dialog.{Title,Current,New,SystemOnly,Allowed,NotAllowed,Danger,Impact,Acknowledge,Reason,Confirm,ConfirmBlock}`,
  `Quick.{Title,PrefilledNote,SaveSelect,MarkNew}`, `Country.{TR,CN,IN,IT,DE,ES,…}` (ISO list), `Error.<every TRACE code of proposal §5>`.
- **SerialsIndex:** `Filter.{Serial,Product,Lot,Status}`, `Col.{Serial,Product,Lot,Status,Where,LastMove,Actions}`.
- **LotMovementsIndex:** `Filter.Lot`, `Chip.Serial`, `Col.{Number,Type,Quantity,FromTo,Status,Source,Date,By}`,
  `Badge.Automatic`, `Receipt.{Title,Type,Quantity,Product,Lot,StockStatus,From,To,Document,DateBy,OpenInLedger}`.
- **LotTraceIndex:** `Filter.Lot`, `Total.{Title,Sub}`, `Stat.{OnHand,OnHandSub,Shipped,ShippedSub,Lots,LotsSub}`,
  `Back.{Title,Level1,Level2,None}`, `Fwd.{Title,Level1,Level2,None}`, `Node.{Receipt,Opening,Consumed,Used,Shipment}`,
  `Root.{Title,Expiry,Initial,OnHand}`, `Hint.Reroot`, `Reality.BackwardReceiptOnly`, `Reality.ForwardDocumentOnly`.
- **LotMigrationIndex:** `Banner.NoChange`, `Banner.Done`, `Kpi.{Total,Matched,Pending}`, `Tab.{All,Pending,Done}`,
  `Col.{Product,Typed,Moves,OnHand,Suggestion,Action}`, `Suggest.{Exact,CaseOnly,Similar,None,NewDraft}`,
  `Decision.{Linked,New,Typo}`, `Pick.{TitleLink,TitleTypo,Typed,TypoNote,NoLots}`,
  `Confirm.{Title,Count,Existing,New,Danger,Reason,Button}`, `Toast.Done`.
- **FU01 StockMovementsIndex (P3 additions):** `Lot.{Picker,Placeholder,None,NewLot,FromSource,BlockedDisabled,ExpiredDisabled,RetestWarn,RetestConfirm}`,
  `Serial.{Methods.Single,Methods.Range,Methods.Scan,Add,RangeStart,RangeEnd,RangeAdd,RangePreview,RangeHint,RangePrefix,RangeOrder,RangeMax,ScanHint,GtinSkippedWarning,Mismatch.Gtin,Mismatch.Lot,Mismatch.Expiry,Unreadable,Duplicates,BadFormat,Counter,CounterOk,CounterOver,CounterMore,Clear,Empty}`.
- **SharedResource (nav, P3):** `Nav.Module.LOT-SERIAL-TRACKING`, `Nav.Page.LOTS`, `Nav.Page.SERIALS`,
  `Nav.Page.LOTTRACE`, `Nav.Page.LOTMIGRATION`.

## 12. Validation Rules

| # | Field / rule | Rule | Server error |
|---|---|---|---|
| 1 | Ürün / SKU | required at create; must be lot-controlled in PRODUCT-MASTER (`lotControlled`); immutable | 422 `PRODUCT_NOT_LOT_CONTROLLED` · 400 `LOT_KEY_IMMUTABLE` · 503 `PRODUCT_MASTER_UNAVAILABLE` |
| 2 | Lot No | required; trim + upper; 1–40 `^[A-Z0-9./-]+$`; unique per `LotIdentityPolicy` (Tenant + SKU + LotNumber); immutable | 400 `LOT_NUMBER_INVALID` · 409 `LOT_ALREADY_EXISTS` · 400 `LOT_KEY_IMMUTABLE` |
| 3–4 | Üretici / Tedarikçi Lotu | optional; trim; 1–40 | 400 `INVALID_REQUEST` |
| 5 | Üretim Tarihi | optional; ≤ today | 400 `LOT_DATES_INVALID` |
| 6 | SKT | optional; > Üretim Tarihi when both | 400 `LOT_DATES_INVALID` |
| 7 | Yeniden Test | optional; ≥ Üretim Tarihi and ≤ SKT when given | 400 `LOT_DATES_INVALID` |
| 8 | Menşe Ülke | optional; ISO 3166-1 alpha-2 | 400 `INVALID_REQUEST` |
| 9 | CoA Referansı | optional; trim; 1–60 | 400 `INVALID_REQUEST` |
| R | Gerekçe | required for edit of a non-Draft lot, every status change, migration commit; **≥ 10 characters after trim** everywhere | 400 `REASON_REQUIRED` |
| S | Status transition | Draft→Released, Released→Blocked, Blocked→Released only; Expired system-only; Blocked needs `acknowledgeImpact` | 422 `LOT_STATUS_TRANSITION_INVALID` · 400 `ACKNOWLEDGEMENT_REQUIRED` |
| E | Edit permission by status | Draft → `trace.lots.update`; non-Draft → `trace.lots.update-released` (K-C) | 403 |
| C | Concurrency | `If-Match` on PATCH / status | 400 `INVALID_REQUEST` · 409 `LOT_CONFLICT` |

**Ledger posting rules (P2, INVENTORY-BUNDLE `postMovement`, all 422 unless noted):**

| Rule | Error |
|---|---|
| Lot-controlled product without lot (existing — `…/Services/MovementMasterDataValidator.cs:86-88`) | `LOT_REQUIRED` (unchanged) |
| Lot given for a product that is not lot-controlled (new — today only trimmed, `MovementRequestParser.cs:141`) | `LOT_NOT_ALLOWED` |
| Lot not registered (inbound; outbound only after migration close) | `UNKNOWN_LOT` |
| Lot of another SKU than the movement | `LOT_SKU_MISMATCH` |
| Lot Blocked (any movement except CT-D4 disposal / reversal) | `LOT_BLOCKED` |
| Lot Expired or `ExpiryDate < postingDate` (same exception) | `LOT_EXPIRED` |
| Serial-controlled: distinct serial count ≠ base quantity (existing — `MovementMasterDataValidator.cs:91-96`) | `SERIAL_COUNT_MISMATCH` (unchanged) |
| Inbound serial already Active for the SKU | `SERIAL_ALREADY_EXISTS` |
| Outbound / transfer serial unknown, Consumed, or not at the source location | `SERIAL_NOT_AVAILABLE` |
| Serial of another lot than the movement's lot | `SERIAL_LOT_MISMATCH` |
| REVERSAL keeps deriving lot / serials from the original (`MovementRequestParser.cs:122-123`) | unchanged |

**UI-only rules (MVC + JS, server stays authority):** lot picker inbound only; Blocked/Expired disabled; retest-past
confirmation (K-F — not enforceable server-side because the frozen movement request has no acknowledgement field;
ASSUMPTION-0174-06); serial format 3–24 `^[A-Z0-9-]+$`; range rules (same prefix + digit count, end ≥ start, ≤ 5,000);
GS1 checks via `gs1-check` (K-B).

## 13. Failure Path to Verify

Every case: nothing partially written; localized message (7 languages) with the correlation id in a details disclosure;
never "403"/"Forbidden" text (UAS-001).

1. **Duplicate lot** (same SKU + lot no) → 409 `LOT_ALREADY_EXISTS` on the Lot No field; the same lot no on another SKU of
   the same product succeeds (K-G / B-1).
2. **Edit a Released lot without reason / with 9 characters** → 400 `REASON_REQUIRED`; with a user lacking
   `update-released` → controls absent, API 403.
3. **Change Lot No / SKU** (hand-crafted PATCH) → 400 `LOT_KEY_IMMUTABLE`, nothing changed.
4. **Set Expired by hand / leave Expired** → 422 `LOT_STATUS_TRANSITION_INVALID`.
5. **Block without acknowledgement** → 400 `ACKNOWLEDGEMENT_REQUIRED`; with it → lot Blocked, balance stock statuses
   unchanged (DB before/after), availability per CT-D1.
6. **Stale edit / status** → 409 `LOT_CONFLICT`, reload message.
7. **Post into a Blocked / Expired lot** → 422 `LOT_BLOCKED` / `LOT_EXPIRED`, no transaction; expiry by date even
   before the sweeper ran.
8. **Lot-tracked product without lot** → 422 `LOT_REQUIRED`; **non-tracked with lot** → 422 `LOT_NOT_ALLOWED`;
   **lot of another SKU** → 422 `LOT_SKU_MISMATCH`.
9. **Serial count ≠ quantity** → 422 `SERIAL_COUNT_MISMATCH`; **duplicate inbound serial** → 422
   `SERIAL_ALREADY_EXISTS`; **issue of a consumed serial** → 422 `SERIAL_NOT_AVAILABLE`.
10. **GS1 code with another lot / SKT / GTIN** → not added, message names the field; **GTIN null** → GTIN skipped,
    visible warning, lot + SKT still checked (sabotage: a code with the wrong lot must still be refused).
11. **Range** with different prefix / digit count / reversed / > 5,000 → preview error, nothing added.
12. **Concurrency:** a posting into lot L and blocking L at the same time → exactly one wins; never "posted into a
    Blocked lot".
13. **Migration:** commit with pending rows → 422 `MIGRATION_DECISIONS_INCOMPLETE`; second commit → 409
    `MIGRATION_ALREADY_COMPLETED`; a new typed lot posted after the list → 409 `MIGRATION_STALE`; `inventory_transactions`
    byte-identical before/after (hash); balances / lot positions equal in total per SKU before/after.
14. **Genealogy:** self link → 422 `GENEALOGY_SELF_LINK`; cycle → 422 `GENEALOGY_CYCLE`; repeat → 200 replay.
15. **Tenant / company isolation:** another tenant's lot = 404 `UNKNOWN_LOT`; stock figures only of the selected company;
    `X-Legal-Entity-Id` ≠ token LE → 403 `CROSS_LE_FORBIDDEN`.
16. **Product master down** → 503 `PRODUCT_MASTER_UNAVAILABLE` on create and on gs1-check, nothing written.
17. **Unauthorized** (no `trace.lots.read`) → `_AccessDenied` on every route; read-only user → no New / Edit / Status /
    migration controls (absent, not disabled).

## 14. Authorization Convention

**Module code `LOT-SERIAL-TRACKING`, namespace `trace.*` (proposed — CT-D5).** One owner per namespace (PKS-001 §4):
`inventory.*` stays MOD-0173's; `trace.*` = the contract name. Tier-3 actions registered here (PKS-001 §5):
`update-released`, `change-status`, `migrate`, `link`.

| Key | Tier | Gate |
|---|---|---|
| `trace.lots.read` | 1 | LOTS, LOTDETAILS, LOTMOVEMENTS (+ `inventory.stock.read` for ledger rows), LOTTRACE; GET ops; gs1-check; migration links |
| `trace.lots.create` | 1 | LOTCREATE, `NEWLOT`, `NEWLOTQUICK` (Stock Ledger form), `createLot` (also MOD-0142 GRN later) |
| `trace.lots.update` | 1 | LOTEDIT; `EDITLOT` (Draft lots) |
| `trace.lots.update-released` | 3 | `EDITRELEASEDLOT` (non-Draft lots, K-C — separate from Draft editing) |
| `trace.lots.change-status` | 3 | `CHANGELOTSTATUS` (K-E) |
| `trace.lots.migrate` | 3 | LOTMIGRATION + its actions (administrator, one-off) |
| `trace.serials.read` | 1 | SERIALS, `VIEWALLSERIALS` |
| `trace.genealogy.link` | 3 | `linkGenealogy` — **API-only → manifest `ApiPermissions`** (standard §2d; MOD-0194 calls it, no UI) |

**Manifest (`LotSerialTrackingManifestProvider`, second provider in the service — the hosted service iterates every
registered provider, `…/Api/ModuleRegistration/ModuleRegistrationHostedService.cs:43`; today one registration,
`…/Api/Program.cs:39`):**

| PageCode | RoutePath | RequiredPermission | Nav-visible | Parent |
|---|---|---|---|---|
| `LOTS` | `/Lots` | `trace.lots.read` | **true** | — |
| `LOTDETAILS` | `/Lots/Details` | `trace.lots.read` | false | `LOTS` |
| `LOTCREATE` | `/Lots/Create` | `trace.lots.create` | false | `LOTS` |
| `LOTEDIT` | `/Lots/Edit` | `trace.lots.update` | false | `LOTS` |
| `LOTMOVEMENTS` | `/LotMovements` | `trace.lots.read` | false | `LOTS` |
| `SERIALS` | `/Serials` | `trace.serials.read` | **true** | — |
| `LOTTRACE` | `/LotTrace` | `trace.lots.read` | **true** | — |
| `LOTMIGRATION` | `/LotMigration` | `trace.lots.migrate` | **true** | — |

**Screen → button → action → permission (every mockup button):**

| Screen | Button (mockup) | ActionCode | Placement | PermissionKey | Dangerous |
|---|---|---|---|---|---|
| Lot Gezgini | Yeni Lot | `NEWLOT` | Toolbar | `trace.lots.create` | no |
| Lot Gezgini | (row) Lot Detayı / satır tıklama | `VIEWLOT` | RowAction | `trace.lots.read` | no |
| Lot Gezgini · Lot Detayı | Düzenle (Taslak lot) | `EDITLOT` | Row / detail | `trace.lots.update` | no |
| Lot Gezgini · Lot Detayı | Düzenle (Serbest / Bloke / Süresi Dolmuş lot) | `EDITRELEASEDLOT` | Row / detail | `trace.lots.update-released` | no |
| Lot Gezgini · Lot Detayı | Durum Değiştir | `CHANGELOTSTATUS` | Row / detail | `trace.lots.change-status` | **yes** (Bloke) |
| Lot Gezgini · Lot Detayı | Hareket Geçmişi | `LOTMOVES` | Row / detail | `trace.lots.read` | no |
| Lot Gezgini · Lot Detayı | İzlenebilirlik | `LOTTRACEOPEN` | Row / detail | `trace.lots.read` | no |
| Lot Detayı | Stok Defteri'nde aç | `OPENINLEDGER` | detail | `trace.lots.read` | no |
| Lot Detayı | Seri Gezgini'nde tümü | `VIEWALLSERIALS` | detail | `trace.serials.read` | no |
| Seri Gezgini | Lot Detayı | `SERIALLOT` | RowAction | `trace.lots.read` | no |
| Seri Gezgini | Hareket Geçmişi | `SERIALMOVES` | RowAction | `trace.lots.read` | no |
| Lot Hareket Geçmişi | (row) Hareket fişi | `VIEWRECEIPT` | RowAction | `trace.lots.read` | no |
| Lot Hareket Geçmişi (fiş) | Stok Defteri'nde aç | `RECEIPTINLEDGER` | modal | `trace.lots.read` | no |
| İzlenebilirlik | Lot Detayı | `TRACELOTDETAIL` | detail | `trace.lots.read` | no |
| Aktarma | Tüm önerileri uygula | `APPLYALLSUGGESTIONS` | Toolbar | `trace.lots.migrate` | no |
| Aktarma | Aktarımı Onayla | `CONFIRMMIGRATION` | Toolbar | `trace.lots.migrate` | **yes** |
| Aktarma | Öneriyi uygula | `APPLYSUGGESTION` | RowAction | `trace.lots.migrate` | no |
| Aktarma | Mevcut lota bağla | `LINKEXISTINGLOT` | RowAction | `trace.lots.migrate` | no |
| Aktarma | Yeni lot olarak oluştur | `NEWLOTFROMTEXT` | RowAction | `trace.lots.migrate` | no |
| Aktarma | Yazım hatası — başka lota bağla | `MARKTYPO` | RowAction | `trace.lots.migrate` | no |
| Aktarma | Değiştir | `CHANGEDECISION` | RowAction | `trace.lots.migrate` | no |
| Stok Defteri · Yeni Hareket | Yeni lot oluştur | `NEWLOTQUICK` (INVENTORY-LEDGER pages `STOCKMOVEMENTCREATE`, `STOCKLEDGERCREATE`) | form | `trace.lots.create` | no |

Not actions (navigation / form submit / input methods): Listeye dön, Geri, İptal, Vazgeç, Kapat, Temizle, Kaydet
(the page's own submit, gated by the page), Hareketi kaydet (FU01), Ekle / Aralığı ekle / Örnek okut (serial input
methods inside the movement form, gated by `inventory.movements.create`), Bağla (pick dialog confirm of `LINKEXISTINGLOT`
/ `MARKTYPO`). The mockup's "Örnek okut / Farklı lotlu karekod okut" are demo buttons (scanner simulation) — the real
form takes the scanner input in the "Barkod" box.

Nav keys: `Nav.Module.LOT-SERIAL-TRACKING` + `Nav.Page.{LOTS,SERIALS,LOTTRACE,LOTMIGRATION}` in all 7
`SharedResource` files. Permission **definition** reaches the AuthService catalog through self-registration (pages,
actions, `ApiPermissions`) — no seed, no hand insert (WP-0173-RSV1 rule). No role shortcut (FU-S1-02 applies).

## 15. Gateway / API Routing Decision

**Gateway: change REQUIRED — `/api/trace` + `/api/trace/{everything}` → `localhost:5066`, methods GET, POST, PATCH,
OPTIONS; pass `Authorization`, `X-Tenant-Id`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match`;
return `ETag`. Owned by a separate integration-agent WP (INT-TRACE), not by this pack.**

| Operation | Route | Source |
|---|---|---|
| `getLot`, `listLots`, `createLot`, `getSerial`, `getGenealogy`, `validateTrace` | v1 paths, unchanged | `trace.openapi.yaml` (FROZEN) |
| `updateLot`, `changeLotStatus`, `getLotHistory`, `getLotDetail`, `listSerials`, `checkGs1Code`, `linkGenealogy`, `listManualLotGroups`, `commitManualLotMigration`, `getMovementLotLinks` | v1.1 | `drafts/trace-v1.1-proposal.md` §4 |

The ledger never calls `/api/trace` over HTTP (in-process `ILotMasterClient`).

## 16. Acceptance Criteria

**P1 — Lot / serial SoR**
- [ ] Builds 0 errors / 0 new warnings; `/api/trace` v1 ops answer exactly the frozen schemas (automated schema
  validation); v1.1 ops per the accepted proposal; `trace.openapi.yaml` 1.1.0 has an `x-changelog` and the §9 check table
  of the proposal holds (diff test: no v1 element removed).
- [ ] Create Draft lot → 201; restart → same lot; same SKU + lot no → 409; same lot no on a second SKU of the same item →
  201 and `listLots(itemId)` returns both (B-1 measured).
- [ ] Edit Draft (update) / Released (update-released + reason ≥ 10) → history rows old → new + reason + actor +
  correlation; Lot No / SKU immutable.
- [ ] Status Draft→Released→Blocked→Released with reason; Expired only by the sweeper (history "Sistem (otomatik)").
- [ ] `LotIdentityPolicy` is the only builder of the key (architecture test); no delete route / method for any 0174 entity.
- [ ] Manifest `LOT-SERIAL-TRACKING`: 8 pages, every action of §14, `trace.genealogy.link` in `ApiPermissions`;
  completeness test both directions; live catalog after restart shows the module, keys Scope Tenant.
**P2 — Ledger integration**
- [ ] Every rule of §12 "Ledger posting rules" refused / accepted as written (API tests); `SERIAL_COUNT_MISMATCH`,
  `LOT_REQUIRED` unchanged.
- [ ] Inbound serials create `SerialNumber` rows in the posting transaction; issue marks Consumed; reversal restores.
- [ ] `inventory_lot_positions` equals Σ balance OnHand per lot after any posting sequence (property test) and after restart.
- [ ] CT-D1 option implemented exactly; Blocked lot: balance stock statuses unchanged; availability as decided.
**P3 — UI**
- [ ] 8 pages render with `_LayoutTenantShell` (grep 8), UAS-001 for each page; every button of §14 present only with its key.
- [ ] Stock Ledger form: lot picker inbound only, Blocked/Expired disabled, quick lot (Draft, selected), serial 3 methods,
  GS1 checks incl. GTIN-null warning; the lot list "Eldeki Stok" reads `stockSummary`.
- [ ] E4: UI + API + persisted lot + history row for create / edit / status, captured.
**P4 — Migration**
- [ ] Measured typed groups = rows; commit: X transactions linked, Y lots; `inventory_transactions` hash identical
  before/after; balances re-keyed with equal totals; typing closed (form + server); W-UI-02 badges gone; W-UI-02 closed in
  the FU01 pack by the CT.
**P5 — L10n**
- [ ] Every key of §11.6 in 7 languages (parity test), no English fallback visible; `ar` RTL screenshot.

## 17. Test Expectations

- DCP-002 verifier exit 0 (or an explicit environment-blocked record — never a fabricated PASS).
- Domain unit tests: `LotIdentityPolicy`, `LotStatusRules`, `LotDateRules`, `Gs1CodeParser` (bracketed + FNC1, `DD=00`),
  `SerialRangeExpander` (prefix / width / 5,000), `ManualLotMatcher` (exact / case / dash-space / row chain ≤ 3).
- Handler + API tests: every row of §12 and §13; idempotency replay / reuse; If-Match 400/409; tenant isolation; 503
  upstream; permission denial per key.
- Mongo integration on an isolated DB (DB-010), replica set; posting-vs-block race (P2).
- Architecture: no delete on 0174 entities; only the policy builds lot keys; no second stock collection besides the
  listed ledger projections; no Model B; no HTTP self-call from the ledger to `/api/trace`.
- Manifest completeness (pages ↔ routes, buttons ↔ actions, `[HasPermission]` keys ↔ manifest incl. `ApiPermissions`).
- Web tests: page render + UAS-001 per page, form rules, resx payload parity, GS1 warning when GTIN null.
- Runtime E4 probe on own ports (never the owner's fleet): create / edit / status / post with lot + serials / migration
  commit, Mongo documents + history rows captured.
- **Sabotage proofs (K3):** remove the "Blocked lot refused" check → red; let a second class build a lot filter → the
  architecture test red; skip the GS1 lot check when GTIN is null → red; remove `trace.genealogy.link` from
  `ApiPermissions` → completeness red; migration touching a transaction → hash test red.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name present in the registry (`module-id-registry.md:272`, "DCP-002 gate OK 2026-09-11").
- [x] DCP-002 verifier re-run exit 0 — **waived: W-0174-01** (no Python on the MVP-1 machine; same pattern as W-0173-01; run on a machine with Python before the MVP-1 PR).
- [x] DCP-009, domain-config, report §4/§5/§9/§14.4/§20.1/§23 read; TRACE v1 read; mockup read (6 logic assets).
- [x] Golden Reference decision: 9 form fields → **Compact**; lists DataTable server mode.
- [x] Frontmatter fields filled; Layout contract explicit (§9).
- [x] Backend file convention (§10) and frontend file contract (§11) listed.
- [x] Validation rules per field + ledger rules (§12); ≥ 4 failure paths (17, §13).
- [x] Authorization: keys, tiers, manifest pages/actions, `ApiPermissions` (§14).
- [x] Gateway decision explicit (INT-TRACE, §15).
- [x] Acceptance criteria and tests per slice (§16, §17).
- [x] Owner decisions K-A…K-G recorded as DECISIONS (§19).
- [x] **CT-D1** (taken 2026-10-05, §19) blocked / expired lot availability — option (a) or (b) (blocks P2).
- [x] **CT-D2** (taken 2026-10-05, §19) createLot without SKU (B-2).
- [x] **CT-D3** (taken 2026-10-05, §19) lot identity tenant-wide vs per company.
- [x] **CT-D4** (taken 2026-10-05, §19) disposal / reversal out of Blocked / Expired lots.
- [x] **CT-D5** (taken 2026-10-05, §19) module code `LOT-SERIAL-TRACKING` + namespace `trace.*` + cross-module action `NEWLOTQUICK`.
- [x] **CT-D6** (taken 2026-10-05, §19) migration re-key of balance projections; v1 `TransactionItem.lotId` shows the registered lot after migration.
- [x] **CT-D7** (taken 2026-10-05, §19) serial uniqueness scope (Tenant + SKU + serial number).
- [x] **CT-D8** (taken 2026-10-05, §19) non-Draft edit includes Blocked + Expired lots; which statuses auto-expire.
- [x] (2026-10-05: proposal ACCEPTED by the CT incl. B-1, B-2 (= CT-D2), B-3; DEC-INV-14 change recorded in report §9) CT accepts the TRACE v1.1 proposal (incl. behaviour changes B-1, B-2, B-3) and records the DEC-INV-14 change
  (owner, 2026-10-05) in the report / registry (CT action, protected paths).
- [x] **Owner approval → `ready-for-dev`** (Ali, 2026-10-05).

## 19. Implementation Notes

### Owner decisions (2026-10-05) — DECISIONS, not assumptions

| ID | Decision | Where in this pack |
|---|---|---|
| **K-A** | Lot list has "Eldeki Stok"; read from a per-lot total kept by the ledger, never computed live per row | §8 `inventory_lot_positions`; §11.3 Lot Gezgini; proposal §2.2 `LotStockSummary`; §16 P2 property test |
| **K-B** | Serial entry: single, range (same prefix + digits, ≤ 5,000), ITS/GS1 code; (01) GTIN, (21) serial, (17) SKT, (10) lot all checked; GTIN = SKU GTIN, lot = selected lot, SKT = lot SKT; GTIN null → only the GTIN check skipped + visible warning (until MOD-0290 D-1); lot + SKT always; code written as if GTIN were present | §11.4; §12 UI rules; §13 #10–11; proposal §4.6 `checkGs1Code`; §17 sabotage |
| **K-C** | Released lot fields editable with reason ≥ 10; old/new into the audit trail; separate permission from Draft editing | §12 R/E; §14 `trace.lots.update-released` + `EDITRELEASEDLOT`; proposal §4.1 |
| **K-D** | Migration screen merges typos (L-001 / L001 suggestion) | §11.5 `ManualLotMatcher`; §14 `MARKTYPO` |
| **K-E** | Status Draft→Released, Released→Blocked, Blocked→Released in 0174 with reason + permission; e-signature / approval = 0175 wraps it later; 0174 leaves the extension point, builds no e-signature | §8 `ILotStatusChangeGate`; §12 S; §14 `trace.lots.change-status`; proposal §4.2 (202 reserved) |
| **K-F** | Retest date passed: warning + explicit confirmation on selection; hard block in 0175 | §11.4; §12 UI rules; ASSUMPTION-0174-06 |
| **K-G** | Lot bound to SKU (`skuId` + `skuLevel`; later Lsku); uniqueness Tenant + SKU + LotNumber — owner change of DEC-INV-14 (Tenant + Item + LotNumber). (1) one policy point + written migration steps; (2) v1 fields kept, `itemId` always filled, v1.1 adds `skuId`/`skuLevel`; (3) v1 consumer behaviour change written | (1) §3 `LotIdentityPolicy`, §8 "K-G single policy point" steps ①–⑦, §17 architecture test; (2) §4 `ItemId` / `SkuId`, proposal §2.1; (3) proposal §8 B-1 / B-2 |
| Other | Lot No and SKU immutable after save (Draft too); lot never deleted; Expired set by the system on SKT (status exists in v1 — `trace.openapi.yaml:93`; the automatic transition is the new part); reason ≥ 10 everywhere | §4 (immutable, no delete); §8 sweeper; §12 R; proposal B-3 |

### Mockup rules → pack (WP list)

| Mockup rule | §12 / §13 |
|---|---|
| Lot picker only on inbound; outbound / transfer lot from source row; Blocked / Expired not selectable | §11.4; §12 ledger `LOT_BLOCKED` / `LOT_EXPIRED`; §13 #7 |
| Non-tracked: no lot field; lot-tracked: no save without lot; serial-tracked: serial count = quantity | §12 `LOT_REQUIRED` / `LOT_NOT_ALLOWED` / `SERIAL_COUNT_MISMATCH`; §13 #8–9 |
| Bloke lot ≠ bloke stok | §8 CT-D1; §13 #5 |
| "Yeni lot oluştur" shortcut → Draft, selected | §11.4; §14 `NEWLOTQUICK`; §16 P3 |
| Migration rows / suggestion / append-only / confirm / typing closed / where typed lots live today | §11.5; §8 migration re-key; §13 #13; facts F-1…F-6 |
| Traceability 2 back / 2 forward, units never summed, MVP-1 reality, genealogy by API | §11.3; proposal §4.7; §13 #14 |
| SKT past red / ≤ 90 days orange / retest own icon | §11.3; proposal `expiryState` / `retestDue` |
| Serial statuses Aktif / Tüketildi / Bloke (= v1 `SerialRef` enum) | §4 `SerialNumber`; proposal §9 |
| Change history: who, when, field, old → new, reason | §4 `LotChangeEntry`; §11.3 Lot Detayı |

### Measured facts (MOD-0173 code at `876805f0e`; paths under `services/Diten.InventoryService/src/` unless `frontend/`)

| # | Fact | Evidence |
|---|---|---|
| F-1 | The typed lot is a plain string on the transaction; no lot entity exists | `Diten.InventoryService.Domain/Entities/InventoryTransaction.cs:48` `public string? LotId` |
| F-2 | Balances carry and are keyed by the same string | `…Domain/Entities/InventoryBalance.cs:23`; `…Domain/ValueObjects/InventoryDimension.cs:17`; upsert filter `…Persistence/Repositories/InventoryLedgerRepository.cs:401` |
| F-3 | Collections: `inventory_transactions`, `inventory_balances` (+ audit, outbox, sequences, reservations, sku_positions, locations, location_history); no BSON element renaming (C# names) | `…Persistence/InventoryCollections.cs:11,14`; `…Persistence/InventoryBsonMappings.cs:36-43` |
| F-4 | Service side only trims / blank-checks the lot (no upper-case, no format) | `…Application/Features/Inventory/Services/MovementRequestParser.cs:141`; wire `…Api/Contract/MovementRequestReader.cs:93` |
| F-5 | Web form normalizes typed lots: JS upper-case, MVC `^[A-Z0-9./-]{1,40}$`; non-inbound takes the source row's lot | `frontend/Diten.Web/wwwroot/assets/js/Inventory/StockMovements/form.js:444`; `frontend/Diten.Web/Models/Inventory/MovementRules.cs:77,280,235`; input `frontend/Diten.Web/Views/Inventory/StockMovements/_Form.cshtml:283` |
| F-6 | "Unregistered lot" badge = any non-empty lot (no registry lookup) | `frontend/Diten.Web/Views/Inventory/StockMovements/Details.cshtml:147-149`, `_Form.cshtml:387`, `…/js/Inventory/StockMovements/index.js:381`, `…/js/Inventory/StockLedger/index.js:263` |
| F-7 | `LOT_REQUIRED` defined and raised when the product is `lotControlled` and no lot | `…Application/Common/InventoryErrorCodes.cs:28`; `…/Services/MovementMasterDataValidator.cs:86-88`; flag `…Infrastructure/Upstream/ProductMasterHttpClient.cs:54` |
| F-8 | `SERIAL_COUNT_MISMATCH` defined and raised when `serialControlled` and distinct serials ≠ integer base quantity | `InventoryErrorCodes.cs:16`; `MovementMasterDataValidator.cs:91-96` (check :94); flag `ProductMasterHttpClient.cs:55` |
| F-9 | Serials persist only as strings on the transaction (not in balances, outbox, read DTOs) | `InventoryTransaction.cs:50` `List<string>? SerialIds` |
| F-10 | No lot status / expiry / GTIN check, no TRACE client, no `/lots` route anywhere in the service; `StockStatus.Expired` exists only as a stock status | agent search + `…Domain/Enums/StockStatus.cs:12`; repo grep `api/trace` → only comments in ProcurementService mock (`…ProcurementService…/IInventoryPostingClient.cs:62`) |
| F-11 | Availability = Σ OnHand(AVAILABLE) − reserved; Blocked/Quarantine/Damaged/Expired statuses go to the Blocked bucket; reservations are SKU-level without lot | `…/QueryHandlers/GetAvailabilityHandler.cs:77-79`; `…Domain/Rules/BalanceBuckets.cs:18`; `…Domain/Entities/InventoryReservation.cs:6` |
| F-12 | Product-master DTOs carry `lotControlled` / `serialControlled`, **no GTIN**, no shelf life (service and Web) | `…Application/Common/Upstream/ProductReference.cs:4-9`; `frontend/Diten.Web/Models/Inventory/MovementFormSources.cs:7-13` |
| F-13 | `EntityBase` = Id, TenantId, LegalEntityId, CreatedAt, UpdatedAt, Version, IsDeleted, DeletedAt (no CreatedBy) | `…Domain/Entities/EntityBase.cs:9-26` |
| F-14 | In-process master precedent: `/api/locations` controller with `POST {id}/status` + If-Match, insert-only history (no reason field today) | `…Api/Controllers/LocationsController.cs:25,88`; `…Persistence/Repositories/LocationRepository.cs:243` |
| F-15 | Inbound types = OPENING_BALANCE, GOODS_RECEIPT_PO, GOODS_RECEIPT_PROD, CUSTOMER_RETURN, COUNT_GAIN | `…Domain/Rules/MovementDirectionRules.cs:22-23` |
| F-16 | Multiple manifest providers per service are supported | `…Api/ModuleRegistration/ModuleRegistrationHostedService.cs:43`; registration `…Api/Program.cs:39` |

### CT decisions — TAKEN 2026-10-05 (CT acceptance of WP-0174-P0)

All eight are closed; the texts below are the options that were on the table, kept for the record.

| # | Decision | Taken by |
|---|---|---|
| CT-D1 | **Option (a).** Availability subtracts Blocked / Expired lots via the lot projection; balance-row stock status unchanged ("bloke lot ≠ bloke stok"). INVENTORY-BUNDLE v1 stays structurally unchanged; the `available` description gains "excluding Blocked / Expired lots" (description-only note + change record, MOD-0173 owner single-writer step in P2). This is a **semantic change of a frozen contract**, therefore an owner decision. | **Owner (Ali), 2026-10-05** |
| CT-D2 | Refuse `createLot` without SKU → `LOT_SKU_REQUIRED` (B-2). | CT |
| CT-D3 | Lot identity is **tenant-wide** (K-G key has no company; an inter-company transfer keeps one lot). Stock, movements and every stock read stay Tenant + LegalEntity scoped (DEC-INV-18 unchanged). | CT |
| CT-D4 | From Blocked / Expired lots only SCRAP, WRITE_OFF and REVERSAL are allowed; every other movement and reservation refused. | CT |
| CT-D5 | Separate module `LOT-SERIAL-TRACKING`, namespace `trace.*`, cross-module action `NEWLOTQUICK`; P3 measures the catalog attribution of the shared key and reports it. | CT |
| CT-D6 | Migration re-keys projections to the registered lot; `getTransactions` resolves linked rows to the registered `lotId` read-side; transaction documents unchanged (append-only). | CT |
| CT-D7 | Serial uniqueness Tenant + SKU + serial number (one GTIN per SKU assumed). | CT |
| CT-D8 | Every non-Draft lot edit needs a reason (Blocked and Expired included); the expiry sweeper expires lots in any status. | CT |

Still open for the CT (not pack decisions): accepting the TRACE v1.1 proposal into the yaml (P1 single-writer step)
and recording the DEC-INV-14 change (K-G) in the inventory report §9.

#### Options as proposed (record)

- **CT-D1 (blocks P2) — Blocked / Expired lot availability:** option (a) projection + availability subtracts, or (b)
  automatic STATUS_TRANSFER (§8). Recommendation (a) + a description-only INVENTORY-BUNDLE note by the MOD-0173 owner.
- **CT-D2 — createLot without SKU** (proposal B-2): refuse (`LOT_SKU_REQUIRED`, recommended) or bind to the single SKU.
- **CT-D3 — lot visibility:** tenant-wide identity (recommended; the K-G key has no company and an inter-company
  transfer keeps one lot) vs per company (then the key would need the company — contradicts K-G).
- **CT-D4 — disposal from Blocked / Expired lots:** the mockup disables every source row of such a lot, which makes
  scrapping expired stock impossible (its own reason list has "Son kullanma tarihi geçmiş" under Hurda). Recommendation:
  allow SCRAP, WRITE_OFF and REVERSAL out of Blocked / Expired lots; refuse all other movements.
- **CT-D5 — module code + namespace:** separate module `LOT-SERIAL-TRACKING` + `trace.*` (recommended — own entitlement,
  own nav group "Lot / Seri Takibi" as in the mockup) and the cross-module action `NEWLOTQUICK` on INVENTORY-LEDGER pages
  with a `trace.*` key (INVENTORY-LEDGER only references the key; LOT-SERIAL-TRACKING declares it on `NEWLOT`) — P3 must
  measure which module the AuthService catalog attributes the key to when both manifests sync it.
- **CT-D6 — migration re-key:** projections re-keyed to the registered lot; `getTransactions` returns linked rows under
  the registered `lotId` (read-side resolution; documents unchanged).
- **CT-D7 — serial uniqueness:** Tenant + SKU + serial number (ITS: GTIN + serial; one GTIN per SKU assumed).
- **CT-D8 — non-Draft edit + auto-expire scope:** the mockup locks every non-Draft lot behind a reason (Blocked and
  Expired included) — kept; the sweeper expires lots in any status (Draft / Released / Blocked).

### Assumptions (revisable only by a new pack version)

- **ASSUMPTION-0174-01:** UI folders `Views/Traceability/**`, `js/Traceability/**` (own module, not `Inventory/`).
- **ASSUMPTION-0174-02:** collections prefixed `trace_` in the InventoryService database; per-lot stock projection keeps
  the ledger prefix `inventory_lot_positions` because the ledger code writes it.
- **ASSUMPTION-0174-03:** the ledger's `LotId` field stores the surrogate lot id after P2; no INVENTORY-BUNDLE change
  (wire `lotId` is an opaque string).
- **ASSUMPTION-0174-04:** ledger `serialIds` stay serial **numbers** (what FU01 sends today); TRACE `serialId` is the
  surrogate; the mapping is by `LotIdentityPolicy` (serial key).
- **ASSUMPTION-0174-05:** a reversal of an inbound marks its serials Consumed (history reason "reversed"); a reversal of
  an issue makes them Active again.
- **ASSUMPTION-0174-06:** K-F confirmation is enforced by the MVC form (its own form model), not by the service — the
  frozen movement request has no field for it.
- **ASSUMPTION-0174-07:** Draft lots may receive and issue stock (mockup: Draft lot with QC stock); usability is governed by
  stock status (0173) and later by 0175.
- **ASSUMPTION-0174-08:** `LOTEDIT` page permission in the manifest = `trace.lots.update`; the MVC gate also admits a
  holder of `trace.lots.update-released`, and the server decides per lot status.
- **ASSUMPTION-0174-09:** `StorageCondition` (report §4) is not in TRACE v1, v1.1 or the mockup → not built; follow-up.
- **ASSUMPTION-0174-10:** typed text stays accepted for outbound / transfer rows that still carry an unmigrated typed lot
  until P4 closes typing; a switch `Trace:EnforceRegisteredLots` turns the inbound rule on when P3 is CT-accepted.
- **ASSUMPTION-0174-11:** migration decisions are page state until commit (mockup behaviour); no server-side draft.
- **ASSUMPTION-0174-12:** "Etkilenen toplam miktar" = on hand of the lot and its genealogy children + shipped quantities
  from outbound ledger rows, per base unit.

### WP-0174-P1 (CT-ACCEPTED 2026-10-05) — lot / serial SoR + TRACE 1.1.0

Commits `c282c981e` (TRACE 1.1.0, single-writer step) · `90d64ca88` (code + tests) · `218f4fb41` (sweeper shutdown) ·
`366d16ee3` (yaml changelog text, DocsPathGuard) · `3ebe52ef0` (comment, DocsPathGuard). `/api/trace` in
`Diten.InventoryService`: TRACE v1 ops + the P1 v1.1 ops (updateLot, changeLotStatus, getLotHistory, getLotDetail,
listSerials, checkGs1Code, linkGenealogy, listLots filters); migration ops declared in the yaml, implemented by P4.
`LotIdentityPolicy` is the only key builder (K-G); `Expired` only by `LotExpirySweeper` (B-3, any status, CT-D8);
module `LOT-SERIAL-TRACKING` registered from code (8 pages, every §14 action, `trace.genealogy.link` in `ApiPermissions`).
Tests 355 → 453 (0 skipped), repo architecture 18/18; five sabotages red → green. Own-port runtime probe (5174, itest DB,
Prism with and without GTIN, real restart). Ledger code untouched (P2). Open for the CT: the live catalog step; the §4
serial shape text (1–24 vs §12 3–24, implemented 3–24); the catalog attribution of the cross-namespace `inventory.stock.read`
actions. Slice assumptions ASSUMPTION-0174-P1-01…09. Evidence `docs/records/audits/2026-10/mod-0174-p1-evidence-2026-10-05.md`.
**CT acceptance (K13):** re-measured in a separate worktree at `26c9c9ff0` — 453/453; files outside `**/Traceability/**`
and tests are exactly the three allowed one-line hooks (Program.cs DI, scope middleware base path, index gate); the two
extended guards (single-SoR collections, INVENTORY-LEDGER manifest completeness) were widened to name `TraceCollections` /
the Traceability controllers explicitly, not loosened. CT sabotage: lot key without SKU (`LotIdentityPolicy` line 39)
→ 1 red (B-1), restored, empty diff. CT decisions: §4 serial shape = 3–24 (fixed above); the live catalog step stays OPEN
until the 0174 branch runs in the owner's fleet (the fleet runs the 0173 branch) — measured with INT-TRACE / P3, together
with the catalog attribution of `NEWLOTQUICK` and the `inventory.stock.read` actions. Probe incident recorded: the first
probe start tried the owner's port 5066 via the appsettings Kestrel endpoint, failed on the occupied port, owner's
process unaffected (PID unchanged); the probe script now passes `--urls`.

### WP-0174-INT (CT-ACCEPTED 2026-10-06) — gateway routes INT-TRACE

`ocelot.json`: two blocks after `/api/locations/{everything}` — `/api/trace` and `/api/trace/{everything}` → `localhost:5066`,
GET/POST/PATCH/OPTIONS, no gateway AuthenticationOptions, headers passed through (INT-1/INT-2 pattern, byte-equal to the
locations blocks except the path); 289 → 291 routes, existing routes unchanged in content and order. Gateway tests 84 → 89
(5 new `Mod0174TraceRouteTests` green, incl. every `trace.openapi.yaml` path + method served); red count unchanged at 1 —
the known `EveryRoute_DownstreamPortIsInKnownServiceSet` (FU-INT1-01, not fixed here), whose list grew by exactly the two
new routes. K3 sabotage (port 5066 → 5067) → 2 red, restored by hash. Own-port routing proof (gateway on 127.0.0.1:5190):
`GET /api/trace/lots` was forwarded to 5066 (Ocelot logs the downstream 404 of `http://localhost:5066/api/trace/lots`; the
owner's 5066 runs the 0173 branch without `/api/trace`), the control `/api/tracex/lots` gave `UnableToFindDownstreamRouteError`;
owner PIDs 5000/5066 unchanged. Open for the CT: live end-to-end once the 0174 branch runs in the fleet. Evidence
`docs/records/audits/2026-10/mod-0174-int-evidence-2026-10-05.md`.
**CT acceptance (K13):** gateway tests re-run at `4676ff8a4` → 88/89, the only red the known FU-INT1-01 guard. MVP-1
branch fast-forwarded `876805f0e → 4676ff8a4` (2026-10-06); the owner's fleet rebuilt once (gateway PID 30904 → 43076,
Inventory 18400 → 7660, Web restarted, up again within ~2 min). **Live, owner's fleet:** `GET :5000/api/trace/lots`
without token → 401; real login (dev-runbook seed, tenant `…0001`, company `66068d61-…`) → 200
`{"items":[],"total":0,"contractVersion":"v1"}`, correlation echoed; the token carries all 8 `trace.*` keys. Read-only
Auth catalog: the 8 `trace.*` keys exist, Module `lot-serial-tracking`, Scope Tenant → P1 live catalog item PASS.
**Finding FU-0174-CAT-01 (fix in P2):** `inventory.stock.read` is now attributed to Module `lot-serial-tracking` — the
LOT-SERIAL-TRACKING manifest declares it on its cross-module actions (`OPENINLEDGER` …), and the catalog sync sets
`Module` from the last manifest that synced the key. Grantability is unchanged (Scope Tenant), but the role screen now
groups the MOD-0173 key under the lot module (PKS-001: `inventory.*` is owned by MOD-0173). In-module fix: no
`inventory.*` key in the LOT-SERIAL-TRACKING manifest (the target page enforces its own key), plus a completeness test
that every manifest key belongs to the module's own namespace; the next INVENTORY-LEDGER reconcile restores the
attribution. The systemic guard (catalog sync must not move a key across namespace owners) is a Platform follow-up.
Same mechanism in reverse for CT-D5 `NEWLOTQUICK` (a `trace.*` key on an INVENTORY-LEDGER page would move
`trace.lots.create` to `inventory-ledger`): P3 must not add it to the INVENTORY-LEDGER manifest until the Platform guard
exists — CT decision before P3.

### WP-0174-P2 (CT-ACCEPTED 2026-10-06) — ledger integration + FU-0174-CAT-01

Commits `8f41b771b` (FU-0174-CAT-01: LOT-SERIAL-TRACKING declares only `trace.*`; the ledger-opening actions carry
`trace.lots.read`; namespace test for both manifests) · `02a7fa54e` (in-process `ILotMasterClient` = `InventoryLotDirectory`,
pre-check + re-check inside the posting transaction; §12 lot / serial rules in pack order; CT-D4; lot `StockTouchSeq`
race technique; serial lifecycle; K-A `inventory_lot_positions`; CT-D1 (a) through SKU position, status change, sweeper,
startup rebuild and getAvailability; `Trace:EnforceRegisteredLots` default false) · `5396a5278` (INVENTORY-BUNDLE 1.0.1,
description-only note on `AvailabilityView.available`). Owner decisions 2026-10-06: ledger files beyond §5 —
`PostMovementHandler`, `LedgerPosting`, `LedgerPostingOutcome`, `LedgerPostingResult` (in-transaction refusal → 422);
the note on `AvailabilityView` (§8 cites the row-level `BalanceView.available`, which is unchanged); the S1 version test
→ 1.0.1. Tests 453 → 507 (MOD-0173 subset 355 → 355 green; three MOD-0173 guards extended by name, not loosened), build
0 warnings; six sabotages red → restored clean; own-port probe (5175, `diten_inventory_p2_itest`, own Prism) 27/27,
owner PIDs unchanged. Slice assumptions ASSUMPTION-0174-P2-01…11. Open for the CT: live catalog attribution after the
fast-forward (CAT-01), live end-to-end, §14 table text for the three re-keyed actions, reservation `availableAfter` now
also lot-aware, FU-0174-INT-01 (the INT gateway test trips `DocsPathGuardTests`, pre-existing). Evidence
`docs/records/audits/2026-10/mod-0174-p2-evidence-2026-10-06.md`.
**CT acceptance (K13):** re-measured in a worktree at `f6ef16913` — 507/507. Ledger files touched = the pack §5 hooks
(validator, posting repository, availability + `SkuPositionRebuilder`, error codes) + the four K-1 files the owner
approved in the agent session (`PostMovementHandler`, `LedgerPosting`, `LedgerPostingOutcome`, `LedgerPostingResult`).
CT sabotage: date-based expiry check disabled (`LotPostingRules.cs:62`) → 3 red, restored, empty diff. CT decisions:
§14 rows `OPENINLEDGER` / `VIEWRECEIPT` / `RECEIPTINLEDGER` re-keyed to `trace.lots.read` (fixed above); reservation
`availableAfter` being lot-aware is the same CT-D1 rule — accepted, the INVENTORY-BUNDLE note on `AvailabilityView`
covers the meaning. **FU-0174-INT-01 closed (CT break-glass, SOP §3.1):** the INT gateway test hard-coded a docs
sub-folder; it now finds `trace.openapi.yaml` by name under `docs/` (same as the Inventory `RepoPaths`) — gateway tests
88/89 (only the known FU-INT1-01), repo architecture tests 18/18. The CT missed this in the INT acceptance (repo
architecture tests were not run) — lesson: run them at every acceptance that adds a test file. Live checks after the
fast-forward (MVP-1 branch → `8b062d335`, 2026-10-06; Inventory PID 7660 → 27024, gateway 43076 → 43132, Web restarted):
G1 regression run `G1CU5UK` 46/46 (ledger unchanged for typed / no-lot postings, `Trace:EnforceRegisteredLots` off);
`GET :5000/api/trace/lots` with a real login → 200; read-only Auth catalog: `inventory.stock.read` back to Module
`inventory-ledger` (the INVENTORY-LEDGER reconcile re-attributed it) → **FU-0174-CAT-01 closed live**.

**CT-D5a (CT, 2026-10-06 — revises the `NEWLOTQUICK` part of CT-D5):** the "Yeni lot oluştur" shortcut is an in-form
control of the Stock Ledger lot field, not a manifest action. It is NOT declared in the INVENTORY-LEDGER manifest (a
`trace.*` key there would re-attribute `trace.lots.create` — FU-0174-CAT-01 mechanism); the view renders it only for a
holder of `trace.lots.create`, and `createLot` enforces the same key server-side. The key stays declared by
LOT-SERIAL-TRACKING (`NEWLOT` / `LOTCREATE`). §14 row `NEWLOTQUICK` is superseded by this note. Follow-up FU-0174-CAT-02
(Platform/Auth owner): the catalog sync must not move `Module` across namespace owners.

### WP-0174-P3 (CT-ACCEPTED 2026-10-06) — UI: 7 pages + Stock Ledger lot field

Commit `04f516b17`: Lots Index / Details / Create / Edit + Durum Değiştir dialog, Serials, LotMovements, LotTrace — tenant
shell, UAS-001 per page, every §14 button only with its key (Web table = manifest source, test); TRACE through a
same-origin proxy with the W-0173-02 company header (`TraceGatewayClient`). Stock Ledger lot field per §11.4: picker on
inbound, Blocked / Expired disabled with the reason, K-F warning + confirmation, quick lot (CT-D5a: in-form, Draft,
selected, `trace.lots.create` only), serial single / range / GS1 with the GTIN-null warning; typed lots still accepted
(P4). L10n en + tr; the lot-field keys live in the Traceability family `LotFieldIndex`, not in FU01
`StockMovementsIndex` as §11.6 says (**owner decision 2026-10-06** — §11.6 text for the CT). **LotMigration not drawn
(P4)**: §16 "grep 8" measured as 7 + 1 open. Web tests 509/511 → 588/590 (the 2 reds = Nav guard, P5), architecture
18/18, build 0 new warnings, five sabotages red → restored clean. E4 on an own Web (127.0.0.1:5195) against the owner's
5000: create / Draft edit / Released edit with reason / Draft → Released → Blocked / quick lot / inbound with the
registered lot (`INV-2026-0000031`) — screenshots + API + Mongo, owner PIDs unchanged. E4 found and fixed: Lot Gezgini
500 (FU01 `MasterLabelResolver` throws on products without SKUs → `TraceLabels`), hidden Durum / Eldeki Stok columns,
empty 15-row length selector. Open for the CT: **InventoryService 506/507 since `8b062d335`** — the §14 cells
`` `trace.lots.read` (FU-0174-CAT-01) `` of `OPENINLEDGER` / `VIEWRECEIPT` / `RECEIPTINLEDGER` no longer match the
completeness test's row regex (measured: pack of `f6ef16913` → green). Evidence
`docs/records/audits/2026-10/mod-0174-p3-evidence-2026-10-06.md`.
**CT acceptance (K13):** F-1 was the CT's own error (`8b062d335` annotated three §14 cells and the Inventory suite was
not re-run after the doc edit) — the annotations are removed from the cells (the re-key note stays in §19 P2);
Inventory 507/507 again. Re-measured at `dd0f014e6` + this fix: Web 588/590 (only the 2 Nav-guard reds, P5), architecture
18/18; files outside the Traceability family = the P3-only FU01 form files + `SharedResource.{en,tr}` (5 `Nav.*` keys
only). CT sabotage: a date-expired lot made selectable (`LotFieldRules.cs:54`) → 1 red, restored. CT decisions:
F-2 accepted (8th page = P4); F-3 owner decision recorded (§11.6 superseded for the lot-field keys → `LotFieldIndex`);
**F-7 → FU-0174-P3-07 (P4 scope):** the K-F retest confirmation must also be enforced by the MVC form post
(ASSUMPTION-0174-06), not only client-side; F-9 / F-6 / F-10 / F-11 / F-13 accepted as recorded; F-12 accepted for the
short P3→P4 window (the LotMigration nav entry has no page until P4). **ASSUMPTION-0174-10 revised:**
`Trace:EnforceRegisteredLots` is switched on together with the P4 migration commit (typing closes in the same step),
not at P3 acceptance — switching it on earlier would refuse typed inbound lots before they can be migrated.
Lesson (CT): re-run every test suite that reads a document after editing that document.

### WP-0174-P4 (agent result 2026-10-06, pending CT) — typed-lot migration, W-UI-02 closure

**K4-1…K4-5 (CT, 2026-10-06)** — the BLOCKED of v1 (the Web could not learn the tenant's closure: `completed` only in
`listManualLotGroups`, `trace.lots.migrate`) lifted: **K4-1** TRACE 1.2.0 `getManualLotMigrationStatus` (`trace.lots.read`),
separate single-writer commit; **K4-2** typed postings and the commit serialise on the tenant's migration document
(`InventoryLotDirectory` gate), re-key in a new ledger file; **K4-3** a migrated transaction reverses through its linked
lot (`PostMovementHandler.cs`); **K4-4** typed texts of items that are not lot-controlled get no group, link or re-key, and
closure refuses only a lot-controlled INBOUND typed text (supersedes the §12 "outbound only after migration close" row);
**K4-5** the commit moves the linked transactions' serials to the lot. Commits `13e3e70c0` (TRACE 1.2.0 + 1.1.0 fixture
diff test) · `259387167` (InventoryService: list / commit / links / status, `ManualLotMatcher`, `ManualLotMigrationPlan`,
`LedgerProjectionReKey`, closure, read-side resolution of `getTransactions`) · `b310981c4` (Web: `/LotMigration` 8th page,
W-UI-02 closure in the Stock Ledger form / Details / lists, FU-0174-P3-07 K-F on the form post). Tests: Inventory
507 → 530, Web 588/590 → 604/606 (the 2 Nav-guard reds, P5), architecture 18/18, gateway 88/89 (FU-INT1-01), build 0 new
warnings; seven sabotages (a)–(g) red → restored clean. E4 on an own chain (Prism 4027, Inventory 5176 on
`diten_inventory_p4_itest`, gateway copy 5191 with every non-inventory route dead, Web 5196, own in-memory JWT secret): 4
typed groups (same number, L-001/L001, new) → 5 transactions linked to 3 lots (2 new Draft), `inventory_transactions`
hash equal before / after, balance totals equal, typed inbound → 422, form without typed input, no badges. The owner's
fleet does not run P4 (migration route 404): read-only Mongo shows 6 typed groups / 36 transactions there. Open for the CT:
W-UI-02 closure record in the FU01 pack (proposed text in the evidence §9); §12 / §11.5 / §11.6 texts (K4-4, client-mode
list, banner without a user name, added keys). Slice assumptions ASSUMPTION-0174-P4-01…05. Evidence
`docs/records/audits/2026-10/mod-0174-p4-evidence-2026-10-06.md`.
**CT acceptance (K13, 2026-10-06):** re-measured in a worktree at `f7b45fe94` — Inventory 530/530, Web 604/606 (the 2
Nav-guard reds, P5), architecture 18/18, gateway 88/89 (only FU-INT1-01; the gateway test reads the 1.2.0 yaml, gateway
files unchanged). Files outside Traceability = the allowed FU01 closure files + `StockMovementsController` (K-F) + the
ledger hooks `MovementMasterDataValidator`, `GetTransactionsHandler`, `PostMovementHandler` (K4-3, lines 200–207 only:
reversal lot via the migration link) and the new `LedgerProjectionReKey`; no code writing `inventory_transactions`
changed. CT sabotage: non-lot-tracked products admitted to the migration groups (`ManualLotGroupReader.cs:40`) → 2 red
(K4-4), restored, empty diff. CT decisions on F-*: F-1 → §12 row superseded by K4-4; F-2 client-mode list accepted
(decisions live in the page); F-3 / F-4 / F-5 / F-6 / F-7 / F-10 / F-12 accepted as recorded; F-13 = W-0174-01. The
owner's live migration (6 groups / 36 transactions measured read-only) is an **owner action on the screen** after the
fast-forward, measured by the CT; W-UI-02 is closed in the FU01 pack after that run.
**Live run 2026-10-06 (owner session, CT in the browser):** the owner's role Admin was granted the 8 `trace.*` keys on
the role screen (persisted after reload); after re-login the menu shows "Lot / Seri Takibi" and `/LotMigration` opens —
**0 groups** in the owner's tenant (36 companies; the 6 groups measured earlier are in the seed tenant `…0001`). Owner
decisions: keep typed-lot entry open in the owner's tenant for now (no empty commit); leave the seed tenant's 6 G1 test
groups unmigrated. No commit was made; W-UI-02 recorded in FU01 §19 as "mechanism delivered, closure per tenant".

### WP-0174-P5 (agent result 2026-10-06, pending CT) — L10n fr / es / zh / ar / ru

The six Traceability resx families (`LotsIndex` 217, `LotFieldIndex` 44, `SerialsIndex` 81, `LotMovementsIndex` 113,
`LotTraceIndex` 102, `LotMigrationIndex` 134 keys) written in fr, es, zh, ar and ru from the en template (same keys, same
order, placeholders kept) and the five Nav keys appended to `SharedResource.{fr,es,zh,ar,ru}` — commit `06b863752`;
en / tr, code, views, scripts, manifest and FU01 resx unchanged. WP glossary for lot / serial terms; FU01 wording for
stock statuses, movement types and the Stock Ledger. `TraceL10nAndGuardTests` now checks 7 files per family (key order,
placeholders, no empty / TODO, no English copy outside the per-language `SameAsEnglish` allowlist with reasons, stale
allowlist guard) and the Nav keys in 7 languages. Tests: Web 604/606 → **607/607** (the 2 Nav-guard reds green + 1 new),
Inventory 530/530, architecture 18/18, build 0 new warnings; sabotages (a) fr key deleted, (b) ar value = en, (c)
`Nav.Page.LOTMIGRATION` removed from zh → red, restored clean. Own Web 5195 → owner Gateway 5000: Lot Explorer and Lot
Detail in ar (RTL) and ru, no header overflow. Open for the CT: FU01 term conflicts T-1…T-5 (ar `الدفعة` vs `التشغيلة`,
ru Продукт / Товар, en movement-type wording) and findings F-1…F-4 (raw history values, ru column folding, shell
"Search" placeholders). Evidence `docs/records/audits/2026-10/mod-0174-p5-evidence-2026-10-06.md`.
**CT acceptance (K13, 2026-10-06):** re-measured at `857c2a65e` — Web 607/607 (Nav guard green), diff = 30 new resx +
5 SharedResource (only the 5 `Nav.*` rows) + test + evidence + pack; sample values match the glossary in all five
languages. CT sabotage: `{0}` removed from a ru value (`LotsIndex.ru.resx:233`) → 1 red, restored. **Decisions on the
findings → FU-0174-P5 (one follow-up WP, after MVP-1 core, FU01 resx = MOD-0173 owner):** T-1/T-2 unify the FU01 lot
column to the MOD-0174 glossary (ar `التشغيلة`, tr "Lot" — the owner's term); T-4 ru `Товар` everywhere; T-5 align the
en movement-type wording of P3 to FU01; F-1 lot history must render field names / statuses / actor names localized, not
raw values (UX defect, also FU-LOC2-04 pattern); F-2 ru column priority (SKT before secondary columns); F-3 shell
"Search" placeholders (shared layout owner); F-4 accepted. **MOD-0174 slices P1–P5 complete;** module status 90 %
(W-UI-02 per-tenant closure, W-0174-01, FU-0174-* open).

### WP-0174-FU5 (agent result 2026-10-07, pending CT) — FU-0174-P5: terms, readable lot history, SKT in ru

T-1 / T-2: the Stock Ledger / Stock Movements (FU01) lot values follow the glossary — tr "Parti" → "Lot" (7 values),
ar `الدفعة` / `دفعة` / `دفعات` → `التشغيلة` / `تشغيلة` / `تشغيلات` (15 values; none meant "payment"); T-4: ru `Продукт` →
`Товар` (8 values; "Готовая продукция" = finished-goods SKU level kept); T-5: Lot Movements movement types = FU01 text in
all 7 languages (en 5, tr 4, and fr 4 apostrophes P5 had missed). Values only, no FU01 key added / removed / renamed.
F-1: Lot Detail history labels the 10 field names the service writes (read from the service code by the test) with the
Lot bilgileri labels, statuses with status labels, dates in the page format, countries by name; 2 new `LotsIndex` keys
(`History.Field.TypedText`, `History.Field.GenealogyParent`) in 7 languages; the actor stays the user id — Web has no
shared user-name resolver (only a CRM-controller-private one) → FU-LOC2-04, central user directory. F-2: Lot Explorer
Expiry `min-tablet-l` (P3 pattern) — ru 1440 px shows "Годен до". Commit `67077cd78`. Tests: Web 607 → **622/622**
(`Fu5TermsAndHistoryTests` 14 + FU01 `SameAsEnglish` stale guard; tr `ColLot` = "Lot" = en is a named entry), Inventory
530/530 (a first run had 4 Prism-start reds, re-run green, no Inventory file changed), architecture 18/18, build 0 new
warnings; sabotages (a) tr "Parti" back, (b) raw status in the history, (c) Expiry class removed → red, restored clean.
Own Web 5195 → owner Gateway 5000: tr / ar ledger lot column, ru Lot Explorer 1440 px and Lot Detail history; owner's
fleet PIDs unchanged. Open: FU-LOC2-04 (actor names), F-3 shell "Search" (central), `Info.Source.Production` wording (lot
origin, left as is). Evidence `docs/records/audits/2026-10/mod-0174-fu5-evidence-2026-10-07.md`.
**CT acceptance (K13, 2026-10-07):** re-measured at `97279c250` — Web 622/622, architecture 18/18; files = Inventory /
Traceability resx + Traceability Lots view/JS/models + tests + evidence + pack. CT sabotage: one FU01 ar value back to
`الدفعة` (`StockLedgerIndex.ar.resx`) → 1 red, restored. CT decisions: `Info.Source.Production` stays (lot origin, not a
movement type); actor names stay ids until a central user directory exists (FU-LOC2-04 + FU-0174-P5 together, central);
F-3 shell "Search" → central layout owner. FU-0174-P5 CLOSED.

**ITEM — `Item` level in TRACE (WP-0174-ITEM, CT-MVP1-0174-ITEM v1, 2026-10-09; agent result, pending CT).**
- **TRACE-BUNDLE 1.3.0** (additive).
  - `SkuLevel` + `Item`, appended.
  - `createLot` gains two codes, both 422: `PRODUCT_MASTER_DATA_INCOMPLETE` (unknown lot flag; was 503 —
    ASSUMPTION-0173-PM1-03 closed) and `SKU_LEVEL_NOT_STOCKABLE` (`stockable: false`; owner decision 2026-10-09).
  - Measured against the pinned 1.2.0 file.
- **Lots.**
  - `createLot`, the lot reads and the GS1 check accept `Item` (`skuId == itemId`, checked with `getSku(level=Item)`).
  - An Item lot is tracking mode `Lot`; the GS1 GTIN check is `Skipped` because the GTIN is null.
  - K-G `AllowedSkuLevels` + `Item`. `WireNames.TryParseLegacySkuLevel` is removed.
  - `getProduct` is read only at GSKU.
- **`stockable: false`** (O-PM1-3) refuses a new lot. **ASSUMPTION-0174-ITEM-01:** when the member is missing, the PM1 rules apply.
- **Web.** The Lots form accepts `Item`; Lots details show the SKU level (`Level.*`, 7 languages).
  The Lots change-history actor stays an id (FU-LOC2-04 / FU-0174-P5 decision).
- **Measured.** Inventory 800 → **829** (own replica set); Web 685 → **694**; gateway 107/108 (known 5065); architecture 18/18.
  - Warnings 0 / 21 / 7, unchanged.
  - 21 red-first cases on the base code, run in a separate base worktree.
  - Sabotages (a)–(e) red with 7 / 3 / 6 / 1 / 3 failures.
  - Runtime probe on own ports, built-in browser: Lots → lot on an Item raw material → lotted receipt into QUARANTINE → note edit →
    signed decision (ESIG-2026-000001) → approval by B in WorkCenter (ESIG-2026-000002) → Applied; people shown by name.
- **Evidence.** `docs/records/audits/2026-10/mod-0174-item-evidence-2026-10-09.md`, `-scripts-2026-10-09.md`, `mod-0174-item-shots/`.

**ITEM — CT acceptance (2026-10-10):** TRACE 1.3.0 FROZEN (`SkuLevel` + `Item`; `createLot` + `PRODUCT_MASTER_DATA_INCOMPLETE`,
`SKU_LEVEL_NOT_STOCKABLE`). Lot-controlled `Item` materials get lots and post; details and decisions in the MOD-0175 pack §19 ITEM CT note.

**WP-MVP1-FU-CLEAN1 — agent result, pending CT (2026-10-19).** Kanıt `docs/records/audits/2026-10/mvp1-fu-clean1-evidence-2026-10-19.md`.
- **`089467b8b` FU-QUI1-LOTSTATUS (kapandı):** lot durum / göç onayının form gövdesi `subtextHtml` (sabit şablon, değerler `textContent`); üç `ui.esc` kaldırıldı. Bekçi: `web-scripts-guard.test.mjs` + `global-confirm-text-is-text.test.js` pin listesi.
- **`359532230` FU-WEB-CONTRACT-FAKES:** `TraceProxyControllerTests` ve `LotMigrationWebTests` sahteleri trace sözleşmesine bağlandı — uyumsuzluk yok.
**DCP-002 (CT, 2026-10-20).** The verifier ran with Python 3.12.10 + openpyxl 3.1.5: `verify_module_id.py . --check-id MOD-0174 --name "Lot/Batch/Serial Tracking"` → OK (proven against Blueprint/registry) → **W-0174-01 closed**.

**WP-MVP1-FIX-LIVE3 (ek) — updateLot anahtarı öznitelikte (agent result, pending CT, 2026-10-20).** Dal `feature/sce/wp-mvp1-fix-live3` (ek maddeler e / f), yerel, push yok. Kanıt `docs/records/audits/2026-10/mvp1-fix-live3-evidence-2026-10-20.md` (§7).
- `PATCH /api/trace/lots/{lotId}` `[HasAnyPermission(trace.lots.update, trace.lots.update-released)]` taşır; hangi anahtarın gerektiği lotun durumuna bağlı — adıyla istisna (`BodyKeyedActions`). Manifest tamlık testi PATCH anahtarlarını artık öznitelikten okur.


## 20. Follow-up Items

- **W-UI-02 closure** in the FU01 pack (CT records after P4).
- **DEC-INV-14 change record** (K-G) in the inventory report §9 + TRACE `info.description` wording (CT / P1).
- **INVENTORY-BUNDLE description note** for `available` (CT-D1 option a) — MOD-0173 owner, single-writer.
- MOD-0290 **D-1 GTIN**: when MDM returns GTIN, the K-B skip path stops firing (no code change expected — §11.4).
- MOD-0175: approval / e-signature through `ILotStatusChangeGate`; retest-past hard block (K-F).
- MOD-0176: FEFO on `listLots(expiryBefore, sort)`; shelf-life rules.
- MOD-0177: recall case on `getGenealogy(depth)` + ledger transactions.
- MOD-0194: genealogy writer (`trace.genealogy.link`); backward trace beyond receipts.
- MVP-6: customer names on forward trace.
- Shell quick search "Lot / seri ara" from the mockup header → Ctrl+K data-driven search (standard §7) or a later WP.
- `StorageCondition` field (report §4) — additive v1.2 if the owner wants it.
- MOD-0142 GRN: replace the createLot mock with the real TRACE call (send `skuId` + `skuLevel`).

## 21. UI input — approved mockup ("Lot Seri Takibi v2 (offline).html", 2026-10-05)

### 21.1 How it was read

Single HTML bundle; `__bundler/manifest` entries (gzip + base64) decoded; the six `window.LT` logic assets read in full
(data, helpers, shell + lot screens, serial / movement / traceability + notes, Stock Ledger form + GS1, migration); the
`__bundler/template` page skeleton read for labels and columns. React / runtime assets not read (WP instruction).

### 21.2 Screens in the mockup → this pack

Lot Gezgini · Lot Detayı · Yeni Lot / Düzenle · Durum Değiştir · Seri Gezgini · Lot Hareket Geçmişi · İzlenebilirlik ·
Stok Defteri · Yeni Hareket (lot alanı) · Elle Girilmiş Lotları Aktarma — every one mapped in §11.1; every button in §14.

### 21.3 Differences mockup ↔ K-* / TRACE v1 (K-* and the frozen contract win)

| # | Mockup | Pack | Why |
|---|---|---|---|
| 1 | Status-change reason ≥ **5** characters | ≥ **10** | owner: reason ≥ 10 everywhere |
| 2 | Note "Süresi Dolmuş … Donmuş v1 sözleşmesinde bu durum yok" | `Expired` **is** in v1 (`trace.openapi.yaml:93`); new = automatic transition only | measured |
| 3 | Note "Ürün + Lot No benzersizdir" | Tenant + **SKU** + Lot No | K-G (the mockup's products are SKUs, so the effect matches) |
| 4 | GS1 scan checks **only** the lot; demo GTIN fallback when the SKU has none | GTIN + lot + SKT checked; GTIN skipped only when null, with a warning | K-B |
| 5 | Every source row of a Blocked / Expired lot disabled — scrap of expired stock impossible | CT-D4 (recommend: disposal + reversal allowed) | open decision |
| 6 | Decision toggles A–D (alternatives shown in "Karar" notes) | the owner-chosen first option of each (A: column, B: three methods, C: editable with reason, D: merge) | K-A…K-D |
| 7 | Header quick search "Lot / seri" | not in this pack (follow-up) | no frozen op for a global jump; Ctrl+K standard |
| 8 | Traceability built from production orders and customer shipments | MVP-1: receipt source backward; document numbers forward | MOD-0194 / MVP-6 not live |
| 9 | Serial list is "mockup örneklemi" (sampled) | real `listSerials` paging | — |
| 10 | Edit form of an Expired lot allowed with reason | kept (CT-D8) | K-C names Released; mockup extends to all non-Draft |
