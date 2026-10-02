---
id: MOD-0173-FU01
name: Inventory Ledger UI
parent: MOD-0173
parent_name: Inventory Ledger & Valuation
domain: supply-chain-execution
service: Diten.InventoryService (consumed via Gateway, INVENTORY-BUNDLE v1) + frontend/Diten.Web
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: StockLedger
    data_mode: server
  - module: StockMovements
    data_mode: server
entity_base: EntityBase
status: ready-for-dev
status_note: "ready-for-dev — owner approval (Ali) 2026-10-01 with decisions W-UI-01, W-UI-02 (kept: order UI-1 → UI-2 → MOD-0174), OD-UI-3, KD compliant mapping. INT-1 CT-accepted (5f97a4b5); permission namespace inventory.* (K1, CT-accepted). DCP-002 re-run pending (waiver W-0173-01)."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0173-fu01-inventory-ui
started: 2026-10-01
target: 2026-10-31
form_field_count: 15
dependencies: [MOD-0173, DEV-0001, INT-1, MOD-0290, LOCATION, MOD-0174, MOD-0048]
---

# MOD-0173-FU01 — Inventory Ledger UI

> **Identity (DCP-002):** `MOD-0173-FU01` is a **follow-up of the Blueprint module MOD-0173** (AGENTS.md "Module ID
> Canonicalization Gate" item 4 — FU of an existing Blueprint MOD; no new MOD is minted). There is no other
> `MOD-0173-FU*` in the repo (measured 2026-10-01). The fail-closed preflight
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0173-FU01 --name "Inventory Ledger UI" --parent MOD-0173`
> **could not run** (no Python on this machine) → **DCP-002 re-run pending (waiver W-0173-01 scope)**. No registry
> row is written by this pack; registering the FU row (`Type = Follow-up`, Replacement ID column = `MOD-0173`, as
> MOD-0027-FU02) is a CT/registry-owner action.
>
> **Authority:** this pack > `domain-config.md` > `AGENTS.md` > `.antigravity/`. Parent pack
> `MOD-0173-inventory-ledger-valuation.md` **§21 (approved mockup decisions) is binding** for scope; parent §3
> (permissions), §8 (runtime), §12 (direction matrix + error codes) and §19 (ASSUMPTION-0173-12..19, S2) are binding
> for behaviour. The frozen contract `INVENTORY-BUNDLE` v1.0.0 is the only wire this UI may use for stock.
>
> **Mockup** "Stok Defteri v4" (owner, `C:\Users\Ali\Downloads\Stok Defteri v4.html`, read 2026-10-01) is a
> content/flow reference only. Colours, spacing and layout come from Golden Reference Compact and the
> `.antigravity` UI rules, never from the mockup.

## 1. Module Summary

Tenant-shell screens over the MOD-0173 stock ledger (single stock SoR). Target users: warehouse / inventory staff
and controllers of a tenant legal entity. Three screens:

| Screen | Route | Kind | Purpose |
|---|---|---|---|
| **S-1 Stock list** | `/StockLedger` | DataTable, `data_mode: server` | Balance projection rows (read-only) + KPI strip |
| **S-2 Movement history** | `/StockMovements` | DataTable, `data_mode: server` | Immutable transactions; the only row action is **Reversal** |
| **S-3 New movement** | `/StockMovements/Create` | Compact full page (`_Form`, 15 fields) | Manual movements (7 types, parent §21) → `POST /api/inventory/movements` |
| S-3 result | `/StockMovements/Details` | Compact Details page | Receipt of the posted (or idempotently replayed) movement |

This FU closes the "UI" part of the G1 E4 evidence bar (inventory report §20.1: "UI + API response + persisted
record + audit birlikte") together with the MOD-0173 backend; it owns **no data**.

## 2. Ownership and Boundaries

**In scope**
- Razor views, MVC controllers (page + same-origin list/proxy endpoints), JS, resx (7 languages) for S-1..S-3.
- A **list adapter** in the MVC layer that translates the repo's server-mode DataTable wire into the frozen
  INVENTORY-BUNDLE query shape (§8, §15) — presentation code, not a second backend.
- Module self-registration manifest for the two routes (§3, §14) and `Nav.*` keys.
- UAS-001 unauthorized surface, view-only mode, error-code → localized message mapping (parent §12 codes).

**Out of scope (must not appear in this UI)**
- STATUS_TRANSFER as a manual action (status lifecycle = MOD-0175, DEC-INV-08).
- The 8 automatic movement types as manual choices (GOODS_RECEIPT_PO/PROD, GOODS_ISSUE_SALES/PROD, CUSTOMER_RETURN,
  SUPPLIER_RETURN, STATUS_TRANSFER, REVERSAL as a picker value) — they are posted by their source modules.
- Rejected mockup items: shelf-life/SKT, min-stock, category, free-text description, vehicle/driver/distance/address,
  shipment progress % and delay computation (not in SHIPMENT-BUNDLE).
- Reservation create/release actions (S2 is consumer-API only; no UI action is approved).
- Two-step in-transit transfer (open decision, MVP-6).
- CSV export (separate export gate, SOP §18.7) — see §20.
- Editing/deleting a transaction (append-only, parent §8). No import button (BL-441).
- Any change to `Diten.InventoryService` behaviour or to the frozen contracts — gaps are listed, not filled (§7).

## 3. Owned Objects

| Object | Kind | Notes |
|---|---|---|
| Entity | **none** | The UI consumes MOD-0173. `entity_base: EntityBase` is recorded only because the consumed records (InventoryTransaction / InventoryBalance) are tenant-owned `EntityBase` documents; no entity, repository or collection is created here. |
| `StockLedgerController` | MVC | `[Authorize]`, `[Route("StockLedger")]`: `GET ""` (Index), `GET "api"` (list adapter, JSON) |
| `StockMovementsController` | MVC | `[Authorize]`, `[Route("StockMovements")]`: `GET ""` (Index), `GET "api"` (list adapter), `GET "Create"`, `POST "Create"` (`[ValidateAntiForgeryToken]`), `GET "Details"` (PRG receipt), `POST "reverse/{transactionId}"` (`[ValidateAntiForgeryToken]`, JSON), `GET "lookups/{kind}"` (picker proxy) |
| View models | `Models/Inventory/StockLedgerViewModels.cs`, `Models/Inventory/StockMovementViewModels.cs` | one file per screen family, mirrors `Models/GoldenReferenceCompact/GoldenReferenceCompactViewModels.cs` |
| Marker classes | `StockLedgerIndex`, `StockMovementsIndex` | localization markers (`IHtmlLocalizer<T>`) |
| Self-registration | `InventoryLedgerManifestProvider` (in `Diten.InventoryService.Api/ModuleRegistration/`) | ModuleCode `INVENTORY-LEDGER` (§14) |
| Consumed API | `GET /api/inventory/balance`, `GET /api/inventory/availability`, `GET /api/inventory/transactions`, `POST /api/inventory/movements` | through Gateway 5000 (INT-1) only |
| Consumed masters (pickers/labels) | PRODUCT-MASTER-BUNDLE v1, LOCATION v1, TRACE v1 (lots), MOD-0048 reference data | see §7 / §20 gaps |
| Permissions used | `inventory.stock.read`, `inventory.movements.create` | defined by MOD-0173; `inventory.reservations.manage` is **not** used by any FU01 action |

## 4. Entity Fields

No persisted fields. The screen fields map to contract fields as follows.

**S-1 columns ← `BalanceView`** (rows of `GET /balance`)

| Column (key) | Source | Note |
|---|---|---|
| Product / SKU (`ColProductSku`) | `itemId`, `skuId`, `skuLevel` → labels from PRODUCT-MASTER `getProduct` / `getSku` (adapter, server-side) | ids only in the contract |
| Warehouse / Location (`ColWarehouseLocation`) | `warehouseId`, `locationId` → labels from LOCATION `getLocation` | |
| Lot (`ColLot`) | `lotId` | serials are not in `BalanceView` (serial is tracked on transactions) — column shows lot only (ASSUMPTION-UI-06) |
| Stock status (`ColStockStatus`) | `stockStatus` | badge, 7 values |
| On hand / Reserved / Available / Quality inspection / Blocked / In transit | `onHand`, `reserved`, `available`, `qualityInspection`, `blocked`, `inTransit` | decimal strings rendered with culture formatting, never parsed through float. `reserved` is `0` on rows by parent ASSUMPTION-0173-12 |
| Unit (`ColUnit`) | `baseUomId` | base UoM |

**S-1 KPI strip ← `AvailabilityView`** (only when an item or SKU filter is set — §8, ASSUMPTION-UI-03):
Total on hand = `onHand` · Available = `available` · Quality inspection = `byStatus.QUALITY_INSPECTION` ·
Quarantine / Blocked = `byStatus.QUARANTINE + BLOCKED + DAMAGED + EXPIRED` (parent §4 "Blocked" bucket).

**S-2 columns ← `TransactionItem`** (items of `GET /transactions`)

| Column (key) | Source | Note |
|---|---|---|
| Transaction no (`ColTransactionNo`) | `transactionNumber` | |
| Movement type (`ColMovementType`) | `movementType` + "Automatic" badge when `sourceModule ≠ MOD-0173` | |
| Reason (`ColReason`) | `movementReason` → MOD-0048 label when the set exists, else the raw code | |
| Product / SKU | `itemId`, `skuId`, `skuLevel` | labels via adapter |
| Quantity / Base quantity | `quantity` + `uomId`, `baseQuantity` + `baseUomId` | sign derived from direction class (parent §12) |
| To (`ColTo`) | `toWarehouseId`, `toLocationId` | **from-side not in the contract** → BG-05 |
| Lot | `lotId` | |
| Status (`ColStatusChange`) | `fromStockStatus` → `toStockStatus` | |
| Source (`ColSource`) | `sourceModule` + `sourceDocumentId` (+ `sourceLineId`) | |
| Transaction date / Posting date | `transactionDate`, `postingDate` | UTC → user time zone for display |

**S-3 form fields (15) → `MovementRequest`** — see §12 for rules.

| # | Field (key) | Contract field |
|---|---|---|
| 1 | Movement type* (`FieldMovementType`) | `movementType` (7 manual values only) |
| 2 | Reason code (`FieldReasonCode`) | `movementReason` |
| 3 | Product* (`FieldProduct`) | `itemId` |
| 4 | SKU* (`FieldSku`) | `skuId` + `skuLevel` |
| 5 | Source (lot / location) (`FieldSource`) | picks one balance row → `fromWarehouseId`, `fromLocationId`, `fromStockStatus`, `lotId` |
| 6 | Target warehouse (`FieldTargetWarehouse`) | `toWarehouseId` |
| 7 | Target location (`FieldTargetLocation`) | `toLocationId` |
| 8 | Target stock status (`FieldTargetStockStatus`) | `toStockStatus` (inbound only) |
| 9 | Lot (`FieldLot`) | `lotId` (inbound only) |
| 10 | Serial numbers (`FieldSerialNumbers`) | `serialIds` |
| 11 | Quantity* (`FieldQuantity`) | `quantity` (decimal string) |
| 12 | Unit* (`FieldUnit`) | `uomId` |
| 13 | Document no (`FieldDocumentNo`) | `sourceDocumentId` |
| 14 | Transaction date* (`FieldTransactionDate`) | `transactionDate` (UTC) |
| 15 | Posting date* (`FieldPostingDate`) | `postingDate` (UTC) |

Server-set, not user fields: `idempotencyKey` (minted on `GET Create`, hidden field, sent as header + body),
`sourceModule = "MOD-0173"`, `sourceType = "MANUAL_MOVEMENT"` (ASSUMPTION-UI-08). `tenantId`/`legalEntityId` are
never sent (parent §8: 400).

## 5. Repo Scope

The UI development WP may write only:

- `frontend/Diten.Web/Controllers/StockLedgerController.cs`, `frontend/Diten.Web/Controllers/StockMovementsController.cs`
- `frontend/Diten.Web/Models/Inventory/**`
- `frontend/Diten.Web/Views/Inventory/StockLedger/**`, `frontend/Diten.Web/Views/Inventory/StockMovements/**`
- `frontend/Diten.Web/wwwroot/assets/js/Inventory/StockLedger/**`, `frontend/Diten.Web/wwwroot/assets/js/Inventory/StockMovements/**`
- `frontend/Diten.Web/Resources/Views/Inventory/StockLedger/StockLedgerIndex.{en,tr,fr,es,zh,ar,ru}.resx`
- `frontend/Diten.Web/Resources/Views/Inventory/StockMovements/StockMovementsIndex.{en,tr,fr,es,zh,ar,ru}.resx`
- `frontend/Diten.Web/Resources/SharedResource.{7 langs}.resx` — **only** the `Nav.*` keys of §14 (shared file:
  single writer, coordinate with any parallel UI WP)
- `frontend/Diten.Web/tests/diten-field-icons.test.js` `ICON_MAP` — only new field glyphs (form rule)
- `frontend/Diten.Web.Tests/**` — new test classes for these screens
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/ModuleRegistration/**` + its registration in
  `Program.cs` + manifest completeness test in `services/Diten.InventoryService/tests/**` (self-registration lives
  with the owning service — module-self-registration-standard §1/§4)
- `docs/records/audits/<yyyy-mm>/mod-0173-fu01-*` (evidence) and this pack (§19 only)

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, `CLAUDE.md`
- `gateway/Diten.ApiGateway/**` (INT-1, integration-agent)
- `docs/analysis/contracts/**` (all frozen contracts)
- `execution/registries/**`, `execution/portfolio/**`, `execution/domains/supply-chain-execution/domain-config.md`,
  the parent pack `MOD-0173-inventory-ledger-valuation.md`
- `services/Diten.InventoryService/**` except the ModuleRegistration scope above — no endpoint, contract or rule change
- All other services (`Diten.MdmService`, `Diten.Platform`, `Diten.ProcurementService`, `Diten.AuthService`, …)
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` (navigation comes from self-registration /
  `DynamicModuleMenu`, not a hand-written menu block), `Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Views/InventoryGovernance/**`, `Controllers/InventoryGovernanceController.cs`,
  `Config/ManagementGovernanceRegistry.cs` (owner decision pending — §19)
- Shared JS/CSS (`diten-datatable.js`, `dt-defaults.js`, `backbone-custom.css`, `_ListShell`, `_TableSkeleton`)

## 7. Dependencies

| Dependency | State (2026-10-01) | Effect |
|---|---|---|
| MOD-0173 S1 (movements, balance, availability, transactions) | CT-ACCEPTED | consumed |
| MOD-0173 S2 (reservations; ASSUMPTION-0173-12..19) | in progress | `/availability.reserved` becomes non-zero; rows stay `reserved 0` |
| **INT-1** gateway `/api/inventory/**` → 5066 + permission seed | **missing** (no route in `ocelot.json`) | **UI development BLOCKED** until done |
| `window.API` key for inventory in `_LayoutTenantShell` | missing | not needed: the list goes through the MVC adapter (§8); the shell is not edited |
| PRODUCT-MASTER-BUNDLE v1 — product/SKU **search** | **no list/search operation in the contract** | **BG-01 (blocking)** for the Product/SKU pickers |
| LOCATION v1 `listLocations(type, parentId)` | contract OK; **no implementation** (Prism only) | warehouse/location pickers work against mocks only |
| TRACE v1 `listLots(itemId)` (MOD-0174) | contract OK; **no implementation** | **BG-02** — inbound lot picker has no real source |
| MOD-0048 movement-reason set | **does not exist**; tenant lookup allow-list has only `legal-form`, `country`, `base-currency` | **BG-03** — Reason code field hidden until the set exists (no hardcoded list) |
| SHIPMENT-BUNDLE v1 (MOD-0183) | only on `origin/feature/mvp6-logistics` | shipment panel hidden (parent §21) |
| Golden Reference Compact (DEV-0001) + live code | live | structural template |
| Governance/Users (server-mode list + UAS-001 gate) | live | second structural example |

**Backend gap list** (contract does not offer it; nothing here may invent it — each needs an additive contract
request to the MOD-0173/MOD-0290 owner or an explicit owner decision):

| ID | Gap | Screen impact | Blocking? |
|---|---|---|---|
| BG-01 | No product/SKU list or search in PRODUCT-MASTER-BUNDLE (MDM has Model A selector APIs outside the contract) | S-3 Product/SKU pickers; S-2 product filter | **Yes** (S-3) |
| BG-02 | TRACE `/lots` not implemented (MOD-0174) | S-3 inbound Lot picker; S-1/S-2 lot filter suggestions | Yes for lot-controlled inbound |
| BG-03 | No MOD-0048 movement-reason set / tenant lookup | S-3 Reason code, S-2 reason label | No (field hidden) |
| BG-04 | `/balance` has no free-text `search`, no sort, no multi-warehouse filter; returns `{rows,total}` with `page/pageSize` | S-1 search box and column sorting omitted; adapter maps paging | No (reduced S-1) |
| BG-05 | `TransactionItem` lacks `fromWarehouseId`/`fromLocationId`, `sourceType`, `createdBy`, `reversedBy` / `reversesTransactionId` | S-2 "From", "Recorded by" columns; "already reversed" cannot be shown before clicking | No (columns omitted; 422 handled) |
| BG-06 | No `GET /transactions/{id}` | S-3 result page cannot be re-opened later by id; reversal needs the row payload | No |
| BG-07 | No aggregate/summary for unfiltered `/balance` | S-1 KPIs only with item/SKU filter | No |
| BG-08 | No reservations read (`GET /reservations`) and reservations are SKU-level (ASSUMPTION-0173-12) | Mockup per-row reservation drawer impossible | No (drawer deferred) |
| BG-09 | `/transactions` has no `movementType` / `skuId` filter | S-2 filters reduced to contract params | No |
| BG-10 | No `/api/locations` / `/api/product-master` gateway routes or real services (mocks only) | labels/pickers unavailable outside Mock | Yes for real-data E4 |

## 8. Runtime Constraints

- Browser talks only to same-origin MVC (`/StockLedger/…`, `/StockMovements/…`); MVC calls **Gateway 5000**
  (`configuration["GatewayUrl"]`, plain `HttpClient`, `AddAuthHeaders` = cookie bearer + `X-Tenant-Id`, exactly as
  `GoldenReferenceCompactController`). No browser call to port 5066 or to `/api/inventory` directly.
- **List adapter (why proxy, not direct-gateway):** the repo server-mode list sends
  `start, length, search, orderBy, orderDir, draw, <filters>` and expects `{ data: { items, total, filteredTotal } }`;
  INVENTORY-BUNDLE expects `page, pageSize ≤ 200` and returns `{ rows|items, total }`. The frozen contract cannot
  change, so `GET /StockLedger/api` and `GET /StockMovements/api` translate:
  `page = start / length + 1` (`length` capped at 200; `start % length ≠ 0` → 400), filters → contract params,
  response → `{ data: { items, total, filteredTotal: total } }` plus `summary` (S-1 KPI, §4) in the **same**
  response (no second browser request — datatable template "Özet/KPI buradan okunur").
  `search` / `orderBy` other than the fixed contract order → **400** (BG-04); the page sets `ordering: false` and no
  search box (ASSUMPTION-UI-02). JS profile = `proxy`; endpoint literal `'/{Route}/api'` (ASSUMPTION-UI-01).
- Labels: the adapter resolves product/SKU/location display names server-side for the ids of the current page only
  (≤ 200 distinct ids), via PRODUCT-MASTER / LOCATION through the Gateway; an unavailable master yields the raw id
  plus a "names unavailable" banner — never a failed list (read path; stock numbers are still correct).
- Decimals: contract decimal strings are kept as strings end-to-end; display formatting uses `Intl.NumberFormat`
  on a decimal string → no float arithmetic on quantities in JS. KPI sums are computed server-side (`decimal`).
- Dates: user enters local date-time; MVC converts to UTC ISO-8601 (`Z`) before posting (parent §12). Display
  converts UTC → user time zone.
- Idempotency: `Idempotency-Key` minted server-side on `GET Create`, carried in a hidden field, sent as header and
  body; a double submit is a replay (200 `idempotentReplay:true`) and shows the replay banner.
- `X-Legal-Entity-Id` is not sent (multi-LE override not granted, parent ASSUMPTION-0173-03); the company chip shows
  the token LE name.
- No soft delete, no edit, no delete: the UI has no such endpoints.

## 9. Layout & Shell Contract

- `shell: tenant` → **every** page sets `Layout = "_LayoutTenantShell";` **explicitly**: `Views/Inventory/StockLedger/Index.cshtml`,
  `Views/Inventory/StockMovements/Index.cshtml`, `Create.cshtml`, `Details.cshtml`. `_ViewStart.cshtml` is not changed;
  partials set no layout.
- View folders: `frontend/Diten.Web/Views/Inventory/StockLedger/` and `frontend/Diten.Web/Views/Inventory/StockMovements/`
  — `{Area}` = `Inventory` (folder grouping, not ASP.NET Areas; sibling of `Views/Procurement/*`).
  **Folder decision:** two list modules because the pack has two independent server lists and the verifier resolves
  one list per folder (`screens[].module`); S-3 (create + receipt) lives with S-2 because a movement's create and its
  history are one resource (`/StockMovements` ↔ `POST /movements`), as Compact `Create` lives with its list.
- Routes: `/StockLedger`, `/StockMovements`, `/StockMovements/Create`, `/StockMovements/Details` — tenant routes
  (not under `/Platform/`), scope = Tenant (self-registration §2c).
- 7 languages (en, tr, fr, es, zh, ar, ru); `ar` renders RTL through the shell's `dir` handling — no page-level RTL CSS.
- Tabs between S-1 and S-2 are two links styled with the WorkCenter pill style (`nav nav-pills … wc-tab-compact`),
  each a real route (not client tabs).

## 10. Backend File Convention

**No backend feature is created by this FU** — no `Features/{Module}` folder, no Command/Query/Handler/Validator,
no `{Module}Models.cs` in any service. All business rules stay in `Diten.InventoryService` (MOD-0173). The only
service-side code is the self-registration manifest:

```text
services/Diten.InventoryService/src/Diten.InventoryService.Api/ModuleRegistration/
├── IModuleManifestProvider.cs              (mirror ProcurementService)
├── InventoryLedgerManifestProvider.cs
├── ModuleRegistrationHostedService.cs      (HTTP push, X-Internal-Api-Key)
└── PlatformRegistrationOptions.cs
```

MVC-side request/response shapes live in `frontend/Diten.Web/Models/Inventory/` (one file per screen family);
naming follows Golden Reference Compact view models.

## 11. Frontend File Contract

**S-1 StockLedger (list only):**
```text
Views/Inventory/StockLedger/
├── Index.cshtml            (Layout = "_LayoutTenantShell"; UAS-001 gate; KPI strip; tabs; _Filter; _DataTable)
├── _Filter.cshtml          (#inlineFilterHost … form#filterForm[data-no-tracker]; Select2 chips)
├── _DataTable.cshtml       (_ListShell: DataMode = "server", HasSelection = false, data-dt-standard="v2")
├── _KpiStrip.cshtml        (UI-026: 4 cards, col-12 col-sm-6 col-xl-3, card skeletons)
├── _IndexL10n.cshtml       (<script id="stockledger-l10n" type="application/json">)
└── StockLedgerIndex.cs     (marker)
wwwroot/assets/js/Inventory/StockLedger/{index.js, index.l10n.js}
Resources/Views/Inventory/StockLedger/StockLedgerIndex.{en,tr,fr,es,zh,ar,ru}.resx
```

**S-2 + S-3 StockMovements (Compact):**
```text
Views/Inventory/StockMovements/
├── Index.cshtml            (history list; UAS-001 gate; tabs; _Filter; _DataTable)
├── Create.cshtml           (page shell + _Form; UAS-001 gate on movements.post)
├── Details.cshtml          (posted-movement receipt; cards mirror _Form sections)
├── _Form.cshtml            (5 section cards, §12)
├── _Filter.cshtml
├── _DataTable.cshtml       (_ListShell: DataMode = "server", HasSelection = false)
├── _IndexL10n.cshtml
└── StockMovementsIndex.cs  (marker)
wwwroot/assets/js/Inventory/StockMovements/{index.js, index.l10n.js, form.js}
Resources/Views/Inventory/StockMovements/StockMovementsIndex.{en,tr,fr,es,zh,ar,ru}.resx
```

Rules carried from the templates (all binding):
- Lists use `DitenDataTable.createList({ dataMode: 'server', … })` only; page JS writes no `serverSide`,
  `ajax.data`, `ext.search`, `matches:`, no `pageSize=`; `stateSave: false`; Save View via `personalizationClient`
  (`moduleKey: 'Inventory'`, `pageKey: 'StockLedger' | 'StockMovements'`).
- **No selection / no bulk:** both lists are read-only → `_ListShell` with literal `HasSelection = false`
  (verifier "Bulk surface: not applicable"); no `_BulkActionBar`, no bulk endpoint.
- Row actions via `DitenDataTable.renderActions` / `bindActionDispatcher`: S-1 "Movement" (→ `/StockMovements/Create?…`
  prefilled source) and "History" (→ `/StockMovements?itemId=…&lotId=…`); S-2 "Reversal" (confirm modal). A
  `key: 'quickView'` action is **not** provided (no QuickView in Compact).
- **`StockMovements/Edit.cshtml` = "Correct" (KD-1 compliant mapping, §19):** never edits a transaction
  (append-only, parent §8, AC17); posts REVERSAL + corrected new movement, with an explicit partial-correction state.
- **No `#offcanvasDetailsPreview` / `_DetailsQuickView` / `_CreateEditOffcanvas`.** `StockLedger/Create.cshtml`
  (KD-2 compliant mapping, §19) hosts the shared movement `_Form` for inbound manual types; no balance is written.
- Skeletons: `_TableSkeleton` (lists), card skeletons for KPI and form lookups; no `spinner-border`, no "Loading" text.
- No import button anywhere (BL-441). No CSV export button until the export gate (§20).

## 12. Validation Rules

Client and MVC validate the **shape**; `Diten.InventoryService` remains the authority for business rules (its
codes are mapped, never re-implemented as silent client rules).

**Conditional matrix (parent §12, restricted to the 7 manual types of §21):**

| Movement type | Class | Source* | Target warehouse* | Target location* | Target status* | Lot | 
|---|---|---|---|---|---|---|
| OPENING_BALANCE, COUNT_GAIN | Inbound | hidden | shown, required | shown, required | shown, required: AVAILABLE (default) · QUALITY_INSPECTION · QUARANTINE | shown; required if product `lotControlled` |
| COUNT_LOSS, SCRAP, WRITE_OFF | Outbound | shown, required (balance row with onHand > 0) | hidden | hidden | hidden | from source row |
| WAREHOUSE_TRANSFER | Transfer | required | required, ≠ source warehouse | required | hidden (keeps source status) | from source row |
| LOCATION_TRANSFER | Transfer | required | locked = source warehouse | required, ≠ source location | hidden (keeps source status) | from source row |

| # | Field | Required | Format / rule | Server error mapped |
|---|---|---|---|---|
| 1 | Movement type | Yes | one of the 7 manual values (Select2, grouped Inbound / Outbound / Transfer) | 400 `INVALID_MOVEMENT_TYPE` |
| 2 | Reason code | No | MOD-0048 code from lookup; field absent while BG-03 open | — |
| 3 | Product | Yes | picker value = `itemId` (UUID) | 422 `UNKNOWN_ITEM` |
| 4 | SKU | Yes | child of Product (`rebuildDependentSelect`), carries `skuLevel` | 422 `UNKNOWN_ITEM` / `SKU_ITEM_MISMATCH` |
| 5 | Source (lot / location) | Outbound + transfer | one `/balance` row of the SKU with `onHand > 0` (label: lot · warehouse location · status · available) | 422 `INSUFFICIENT_STOCK` (details available/requested) |
| 6 | Target warehouse | Inbound + transfer | LOCATION `type=Warehouse`; ≠ source for WAREHOUSE_TRANSFER | 422 `UNKNOWN_LOCATION` |
| 7 | Target location | Inbound + transfer | LOCATION children of the warehouse, stockable; ≠ source for LOCATION_TRANSFER | 422 `UNKNOWN_LOCATION` |
| 8 | Target stock status | Inbound | AVAILABLE · QUALITY_INSPECTION · QUARANTINE only (no STATUS_TRANSFER) | 400 `INVALID_REQUEST` |
| 9 | Lot | Inbound, if `lotControlled` | picked from TRACE `listLots(itemId)` — never typed (§21; BG-02) | 422 `LOT_REQUIRED` |
| 10 | Serial numbers | If `serialControlled` | textarea, one per line, trimmed, no blanks/duplicates; distinct count must equal base quantity (live counter "entered / required") | 422 `SERIAL_COUNT_MISMATCH` |
| 11 | Quantity | Yes | `^\d+(\.\d{1,6})?$`, > 0; kept as string | 400 `INVALID_REQUEST`; 422 `INSUFFICIENT_STOCK` |
| 12 | Unit | Yes | base UoM + `fromUom` values of `getSkuUom(skuId)`; shows "n UNIT = m BASE" hint | 422 `UOM_CONVERSION_MISSING` |
| 13 | Document no | No | trim, max 200 | 400 `INVALID_REQUEST` |
| 14 | Transaction date | Yes | date-time; sent as UTC ISO-8601 | 400 `INVALID_REQUEST` |
| 15 | Posting date | Yes | date-time; sent as UTC ISO-8601 | 400 `INVALID_REQUEST` |

- Required contract parity (datatable template): the required list above is identical in the MVC view model
  (`[Required]` / conditional `IValidatableObject`), Razor `required` + `*` marker, `form.js` conditional toggling and
  the required-fields tracker. Optional numeric/date fields are nullable.
- `_Form` sections (cards, same order in `Details`): ① Movement (1, 2) · ② Item (3, 4) · ③ Source & target (5–9) ·
  ④ Quantity (10, 11, 12) · ⑤ Document & dates (13, 14, 15). Every field in `.diten-field` with an icon; date icons
  open the picker (`diten-datefield.js`).
- Side panel "Stock effect" (mockup) is informational only: it shows the selected source row's current `onHand`
  from `/balance` and the requested quantity; it **does not** pre-judge sufficiency (the server decides).

## 13. Failure Path to Verify

Every case: no partial UI state, stock unchanged unless the response was 201/200, localized message (7 languages),
correlation id shown in a "details" disclosure for support.

- **Insufficient stock** — outbound/transfer above the source row: 422 `INSUFFICIENT_STOCK` → form stays filled,
  field-level message on Quantity with `available` / `requested` from `error.details`; ledger and balance unchanged.
- **Lot missing** — lot-controlled inbound without lot: 422 `LOT_REQUIRED` → message on Lot; no write.
- **Serial mismatch** — distinct serials ≠ base quantity: client counter blocks submit; if bypassed, 422
  `SERIAL_COUNT_MISMATCH` → message on Serial numbers.
- **UoM conversion missing** — unit not convertible: 422 `UOM_CONVERSION_MISSING` → message on Unit naming unit → base.
- **Upstream down** — 503 `PRODUCT_MASTER_UNAVAILABLE` / `LOCATION_MASTER_UNAVAILABLE` → page-level alert "no change
  was made, retry when the connection is back" + Retry (same idempotency key); no toast-only failure.
- **Double submit** — second POST with the same key → 200 `idempotentReplay: true` → Details shows the **original**
  result with the replay banner; exactly one transaction in `/transactions`.
- **Key reuse** — form edited after a failed network round-trip and resubmitted with the same key → 400
  `IDEMPOTENCY_KEY_REUSED` → message "this form was already submitted with other values; start a new movement" and
  a fresh form (new key).
- **Unauthorized (no read)** — `/StockLedger`, `/StockMovements` render `_AccessDenied` inside the shell, no
  skeleton/title/table/button, no redirect, URL unchanged (UAS-001); 401 → shell-less `NotAuthorized`.
- **View-only (read, no post)** — lists render; "New movement", row "Movement", "Reversal" are **absent** (not
  disabled); `/StockMovements/Create` renders `_AccessDenied`.
- **Cross-LE** — any 403 `CROSS_LE_FORBIDDEN` → in-page access message (never "Forbidden"/"403").
- **Reversal of an automatic movement** — action not rendered (`sourceModule ≠ MOD-0173`), tooltip/reason text
  "reversed in the source module".
- **Reversal rejected** — 422 `ALREADY_REVERSED` (BG-05: not knowable before) or 422 `INSUFFICIENT_STOCK` → modal stays
  open with the localized reason; list reloads with `reloadWithToast` only on success.
- **409 (concurrency)** — no FU01 action can produce `RESERVATION_CONFLICT` (no reservation actions); any 409 from the
  API is shown as "data changed, reload" and nothing is retried silently.
- **Bad list parameters** — adapter 400 (search/orderBy, page misalignment) never reaches users through the shipped
  JS; a hand-crafted URL gets the contract `Error` JSON.

## 14. Authorization Convention

- MVC: `[Authorize]` on both controllers (tenant shell). Backend authority stays `[HasPermission]` in
  `Diten.InventoryService` (parent §14); the UI checks are display decisions (`IPermissionSnapshot` /
  `window.__permissionSnapshot`).
- Permissions (PKS-001, defined by MOD-0173 — not new keys):

| Key | Gate |
|---|---|
| `inventory.stock.read` | S-1, S-2 Index (UAS-001 `_AccessDenied` when absent); list adapters; picker proxies |
| `inventory.movements.create` | "New movement" toolbar button, S-1 row "Movement", S-2 row "Reversal", `/StockMovements/Create` (GET + POST), `POST reverse/{id}` |
| `inventory.reservations.manage` | not used by FU01 |

- Actor: `tenant_user`. The parent's FU-S1-02 / ASSUMPTION-0173-19 removes the `SuperAdmin` bypass — the UI must not
  add any role shortcut either.
- **Self-registration** (module-self-registration-standard): `InventoryLedgerManifestProvider` in InventoryService,
  ModuleCode `INVENTORY-LEDGER`, Domain `SUPPLYCHAINEXECUTION` (new domain), `IsTenantAssignable = true`.

| PageCode | RoutePath | RequiredPermission | Nav-visible | Parent |
|---|---|---|---|---|
| `STOCKLEDGER` | `/StockLedger` | `inventory.stock.read` | **true** | — |
| `STOCKMOVEMENTS` | `/StockMovements` | `inventory.stock.read` | false | `STOCKLEDGER` |
| `STOCKMOVEMENTCREATE` | `/StockMovements/Create` | `inventory.movements.create` | false | `STOCKMOVEMENTS` |
| `STOCKMOVEMENTDETAILS` | `/StockMovements/Details` | `inventory.movements.create` | false | `STOCKMOVEMENTS` |
| `STOCKMOVEMENTEDIT` | `/StockMovements/Edit` ("Correct", KD-1 mapping §19) | `inventory.movements.create` | false | `STOCKMOVEMENTS` |
| `STOCKLEDGERCREATE` | `/StockLedger/Create` (inbound entry, KD-2 mapping §19) | `inventory.movements.create` | false | `STOCKLEDGER` |

| ActionCode | Placement | PermissionKey | IsDangerous |
|---|---|---|---|
| `NEWMOVEMENT` | Toolbar (S-1, S-2) | `…movements.create` | false |
| `ROWMOVEMENT` | RowAction (S-1) | `…movements.create` | false |
| `ROWHISTORY` | RowAction (S-1) | `…read` | false |
| `REVERSAL` | RowAction (S-2) | `…movements.create` | **true** |

- Nav keys in all 7 `SharedResource.{lang}.resx`, normalized by `NavNameLocalizer.Normalize`:
  `Nav.Domain.SUPPLYCHAINEXECUTION`, `Nav.Module.INVENTORYLEDGER`, `Nav.Page.STOCKLEDGER` (exact key spelling is
  whatever the shipping normalizer produces from the manifest codes — the guard derives it).

## 15. Gateway / API Routing Decision

- **Decision: gateway change REQUIRED — and owned by INT-1 (integration-agent), not by this FU.**
- `ocelot.json` (measured on 9bc1d625): no `/api/inventory` route, no catch-all covering it.
- INT-1 must add (Strategy A): `/api/inventory` and `/api/inventory/{everything}` → `localhost:5066`,
  `UpstreamHttpMethod` incl. GET, POST, PATCH, OPTIONS; propagate `Authorization`, `X-Tenant-Id`,
  `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match` (parent §15); plus the
  `inventory.*` permission seed.
- Picker masters need their own routes when real services exist (`/api/locations/**`, `/api/product-master/**`,
  `/api/trace/**`) — BG-10, owners MOD-0290 / Location master / MOD-0174 via integration-agent.
- **UI development is BLOCKED until INT-1 is CT-accepted.** This pack does not touch `gateway/**`.

## 16. Acceptance Criteria

- [ ] Every `.cshtml` page under `Views/Inventory/StockLedger/` and `Views/Inventory/StockMovements/` that is a page
      (Index, Create, Details) contains `Layout = "_LayoutTenantShell";` explicitly (grep = 4 hits); no `Areas/`.
- [ ] `python3 .antigravity/scripts/verify_datatable_page.py . --area Inventory --module StockMovements --reference compact --data-mode server --api-profile proxy --format gaps`
      → PASS (full Compact set; Edit = Correct, §19).
- [ ] `python3 .antigravity/scripts/verify_datatable_page.py . --area Inventory --module StockLedger --reference compact --data-mode server --api-profile proxy --format gaps`
      → PASS (Create = inbound movement form, §19); `quality-gate-datatable` run for both.
- [ ] Pack `screens[].data_mode` = `<table data-dt-data-mode="server">` = `createList({ dataMode: 'server' })` on both lists.
- [ ] Both lists render `HasSelection = false`; no bulk bar, no import button, no export button.
- [ ] S-1 list request goes to `/StockLedger/api` and results in exactly **one** Gateway `GET /api/inventory/balance`
      per page (+ one `/availability` when an item/SKU filter is set, folded into the same browser response).
- [ ] Paging 50 → page 2 sends `page=2&pageSize=50` to the contract (adapter log / network evidence).
- [ ] S-3 posts only the 7 manual types; STATUS_TRANSFER and the automatic types are not selectable (DOM evidence).
- [ ] Inbound target status offers exactly AVAILABLE / QUALITY_INSPECTION / QUARANTINE.
- [ ] Each failure path of §13 shown with screenshot + API response + (for state-changing ones) the unchanged/changed
      persisted record — **E4: UI + API + persisted + audit** for: opening balance, count loss, location transfer,
      reversal, insufficient stock, double submit.
- [ ] Double submit produces one transaction and the replay banner.
- [ ] UAS-001: user without `inventory.stock.read` sees `_AccessDenied` in-shell on both routes, no redirect,
      no skeleton; user with read only sees no post actions; live-tried (rule §8).
- [ ] `grep -rn "Permission denied\|Forbidden"` over the new Views/JS = 0.
- [ ] RESX parity: `StockLedgerIndex` and `StockMovementsIndex` have identical key sets in all 7 languages, no value
      equal to English in non-English files, no `|| 'Text'` JS fallbacks.
- [ ] `dotnet test frontend/Diten.Web.Tests --filter "FullyQualifiedName~NavManifestL10nGuardTests"` green; manifest
      completeness test green; restart → module, 4 pages, 4 actions in catalog, re-restart idempotent.
- [ ] Runtime smoke (add-module Phase 4.5, channel A or B): both lists load in tenant shell, filters apply, toolbar
      renders, no console error, "no records" state, Create → Details flow, Reversal modal; Slow-3G skeleton without
      layout jump.

## 17. Test Expectations

- `dotnet build` frontend + InventoryService (manifest) 0 errors / 0 new warnings.
- `Diten.Web.Tests`: adapter unit tests (paging translation, 400 on `search`/`orderBy`, response mapping, summary
  fold-in, label resolution with an unavailable master → raw ids), error-code → localized message map covers every
  code of parent §12/§13, view-model required parity, UAS-001 render tests (no read → `_AccessDenied`; read-only → no
  post actions), `ListShellRenderEqualityTests`-style render test for both `_DataTable` partials.
- `diten-field-icons.test.js` green with the new field glyphs.
- Verifier (§16) for both screens; RESX parity; Nav guard; manifest completeness + reconcile-state test.
- Browser smoke / E4 runtime evidence against InventoryService on 5066 through Gateway 5000 (after INT-1) with Prism
  mocks for PRODUCT-MASTER / LOCATION / TRACE until BG-10 closes; evidence under `docs/records/audits/<yyyy-mm>/mod-0173-fu01-*`.
- Tenant isolation is proven by MOD-0173; here: a second tenant's user sees an empty S-1/S-2 for the same item.

## 18. Ready-for-dev Checklist

- [x] Golden Reference Compact (DEV-0001) pack and live code read; Governance/Users read as server-mode + UAS-001 example.
- [x] Frontmatter complete (id, name, domain, service, shell, golden_reference, data_mode, screens, entity_base, status, owner, branch, started, target, form_field_count).
- [x] Layout & Shell Contract names `_LayoutTenantShell` explicitly and is an AC.
- [x] Backend File Convention: no backend feature (consumer UI); manifest only.
- [x] Frontend File Contract lists both screens' file sets; Edit/Create compliant mapping (§19) declared.
- [x] Validation Rules for all 15 fields + conditional matrix.
- [x] Failure Path ≥ 4 (13 listed).
- [x] Authorization: permissions, actor, policy, manifest pages/actions.
- [x] Gateway decision explicit (INT-1).
- [x] Lookup & reference data decision (§19) — no hardcoded lists.
- [ ] **DCP-002** `verify_module_id.py --check-id MOD-0173-FU01 --parent MOD-0173` exit 0 (waiver W-0173-01 until MVP-1 PR).
- [x] **Owner decision OD-UI-1** taken (2026-10-01) — temporary MDM Model A selectors under waiver W-UI-01 (§19).
- [x] **Owner decision OD-UI-2** taken (2026-10-01) — temporary typed lot under waiver W-UI-02 (§19).
- [x] **Owner decision OD-UI-3** taken (2026-10-01) — untouched until FU01 ships, then retired in a separate WP; never shown beside FU01 under an "Inventory" label.
- [x] KD-1 / KD-2 resolved (owner, 2026-10-01): deviations not accepted → **compliant mapping** (§19 "Owner decisions"); full Compact file set, ledger immutability kept.
- [x] INT-1 CT-accepted (gateway route 5f97a4b5); permission registration is part of this pack (manifest).
- [x] Registry FU row recorded by CT (2026-10-01).
- [x] **Owner approval → `ready-for-dev`** (Ali, 2026-10-01). Lot order decision: keep UI-1 → UI-2 (typed lot, W-UI-02) → MOD-0174.

## 19. Implementation Notes

**Lookup & reference data decision** (tenant pack — PSS lookups are not used; hardcoded lists forbidden):

| Picker / label | Source | Path (MVC proxy → Gateway) | State |
|---|---|---|---|
| Movement type | MOD-0173 canonical enum, filtered to the 7 manual values of parent §21 | none (contract enum — not a reference list) | OK |
| Stock status (filter, target status) | contract `StockStatus` enum (target: 3 values) | none | OK |
| Product / SKU | PRODUCT-MASTER-BUNDLE — **no search op** | `GET /StockMovements/lookups/products?q=` | **BG-01 → OD-UI-1** |
| Unit | `getSkuUom(skuId)` | `GET /StockMovements/lookups/uoms?skuId=` → `/api/product-master/skus/{id}/uom` | OK (contract) |
| Warehouse | `listLocations(type=Warehouse)` | `GET /StockMovements/lookups/warehouses` → `/api/locations?type=Warehouse` | contract OK, mock only |
| Location | `listLocations(parentId=…)` (+ hops to Bin) | `GET /StockMovements/lookups/locations?warehouseId=` | contract OK, mock only |
| Lot (inbound) | TRACE `listLots(itemId)` | `GET /StockMovements/lookups/lots?itemId=` → `/api/trace/lots` | **BG-02 → OD-UI-2** |
| Source row (outbound/transfer) | `GET /api/inventory/balance?skuId=…` | `GET /StockMovements/lookups/sources?skuId=` | OK |
| Reason code | MOD-0048 movement-reason set | — | **BG-03**: field not rendered until a published set + tenant lookup exist |

The S-1 warehouse filter uses the same warehouse lookup; status filter uses the contract enum.

**InventoryGovernance (measured, not touched):** `Views/InventoryGovernance/Index.cshtml` (2020 lines) +
`InventoryGovernanceController` are a hardcoded mock (literal exception rows, `GetInventoryGovernanceKpiMock()`),
legacy `_Layout`, no `[Authorize]`, no localization, two referenced JS files that 404; linked from `_Layout.cshtml`
and from `ManagementGovernanceRegistry.cs` as module `mod-risk-management`. DCP-009 §3 calls it a "hardcoded shell
(K1)". **Proposal (OD-UI-3, owner decision — open):** FU01 does **not** reuse or extend it; it is a different
concern (expiry-risk governance) whose real owner is MOD-0176/0177. Recommended: (a) keep it untouched until
FU01 ships; (b) then retire it in a separate WP (remove the `_Layout` nav link and the `mod-risk-management` route,
or re-point that registry entry to the future MOD-0176/0177 screen); (c) never show both under an "Inventory"
label in the tenant nav. Nothing in this pack depends on it.

**ASSUMPTION-UI-xx** (narrowest reading, autonomous mode):

| ID | Assumption |
|---|---|
| UI-01 | List profile = **proxy** (`/StockLedger/api`, `/StockMovements/api`) because the frozen contract's paging shape differs from the repo server-mode wire; the verifier is run with `--api-profile proxy`. If the verifier's proxy literal requires `'/{Area}/{Module}/api'`, routes become `/Inventory/StockLedger` and `/Inventory/StockMovements` — decided at dev time by measurement, not here. |
| UI-02 | S-1 has **no free-text search** and **no column sorting** (BG-04); filters = warehouse, location, lot, stock status, product/SKU (when OD-UI-1 is decided). The mockup's search box is deferred, not faked client-side. |
| UI-03 | KPI strip is shown only when a product or SKU filter is set (from `/availability`, folded into the list response); otherwise a neutral "select a product to see totals" card state (BG-07). |
| UI-04 | Reserved: rows show contract `reserved` (0 by ASSUMPTION-0173-12); the per-row reservation drawer of the mockup is deferred (BG-08). |
| UI-05 | Shipment drawer/button not rendered (parent §21: hidden until MVP-6) — no placeholder button. |
| UI-06 | S-1 "Lot / Serial" shows lot only; serials exist on transactions, not balance rows. |
| UI-07 | S-2 shows `To` only; "From" and "Recorded by" columns are omitted until BG-05 closes. |
| UI-08 | Manual postings carry `sourceModule = "MOD-0173"`, `sourceType = "MANUAL_MOVEMENT"`; "Automatic" = any `sourceModule ≠ "MOD-0173"`. Reversal is offered only for `sourceModule = "MOD-0173"` and `movementType ≠ REVERSAL`. |
| UI-09 | Reversal posts parent ASSUMPTION-0173-05 (`REVERSAL`, `sourceModule MOD-0173`, `sourceType REVERSAL`, `sourceDocumentId = transactionId`, original item/sku/skuLevel/quantity/uomId from the row) via `POST /StockMovements/reverse/{id}`; no sides/lot/serials. |
| UI-10 | `Details` = receipt of the just-posted movement (PRG via TempData from the POST result); it is not addressable by id later (BG-06). The Compact `_Form`/`Details` section parity still applies. |
| UI-11 | S-1 row "Movement" pre-fills S-3 as LOCATION_TRANSFER with that row as source (mockup); "History" opens S-2 filtered by item + lot. |
| UI-12 | Form field count = 15 as listed by the WP (Reason code counted although BG-03 hides it) → Compact. |
| UI-13 | Quantities are never summed or compared in browser JS; any arithmetic (KPI, stock-effect panel) is done server-side in `decimal`. |
| UI-14 | The company chip shows the token legal entity; no LE switcher (multi-LE override not granted). |
| UI-15 | Frontmatter `branch` = the WP-0173-UI0 target branch `feature/sce/mod-0173-fu01-inventory-ui` (the pack and the later UI dev WP share one branch); the standard's slug form would be `…-inventory-ledger-ui` — kept short to avoid a second branch name. |

**Recommendations for the parent pack (not applied — parent is protected):**
1. §6 says `frontend/**` is protected "(no UI in S1/S2)"; add a pointer that UI lives in MOD-0173-FU01.
2. §20 "Tenant-shell UI … Slim/Compact decision" → replace with "see MOD-0173-FU01 (compact, 2 server lists)".
3. Record BG-01..BG-10 as contract-v1.1 candidates (additive: `/balance` `search`/sort, summary, `TransactionItem`
   from-side + `reversesTransactionId` + `createdBy`, `GET /transactions/{id}`, `GET /reservations`, `/transactions`
   `movementType` filter) under a single-writer Phase A, and decide the product/SKU search source with MOD-0290.
4. §21 open note "Sevkiyat çıkışı/teslimi" labels: keep open until MVP-6.

### Owner decisions (Ali, 2026-10-01) — recorded by MVP-1 CT

- **W-UI-01 (OD-UI-1, waiver — K19).** Product/SKU pickers use the existing **MDM Model A** selector APIs through
  Gateway 5000, read-only: `/api/global-products/selector`, `/api/gskus`, `/api/lskus`,
  `/api/finished-goods/gsku-selector` (routes measured in `ocelot.json`). **Never** `/api/mdm/products` or any Model B
  surface (DEC-INV-17). Compensating controls: the source sits behind one swappable MVC lookup class
  (`ProductPickerSource`) and is the only non-contract call in FU01; every posted id is still checked by
  MOD-0173 against PRODUCT-MASTER (fail-closed). Owner: Ali. Expiry: when PRODUCT-MASTER-BUNDLE gains an additive
  search operation (v1.1, MOD-0290 owner = single writer), and in any case **before G1 sign-off**. Request to the
  MDM owner is a follow-up (§20).
- **W-UI-02 (OD-UI-2, waiver — K19).** Until MOD-0174 exposes lots, a lot-controlled inbound movement takes a
  **typed lot number**. This supersedes, for lots only, the MOD-0173 pack §21 rule "lots selected from masters,
  never typed". Compensating controls: trim + upper-case normalisation; format 1–40 chars `[A-Z0-9./-]`; the
  field and every list/receipt show the lot as **"unregistered lot (pre-MOD-0174)"**; existing lots of the same
  SKU found in `/balance` are offered first so a known lot is chosen instead of retyped. **Cost (measured risk):**
  the ledger stores the typed text as `LotId`, while DEC-INV-14 makes the surrogate `LotId` canonical — MOD-0174
  must register every typed lot (business key Tenant+Item+LotNumber) and map ledger rows before its own freeze;
  typos create separate lots that only a REVERSAL + re-post can fix. Owner: Ali. Expiry: MOD-0174 lots API live
  → picker becomes mandatory and typing is removed.
- **OD-UI-3.** As proposed above: InventoryGovernance untouched until FU01 ships, then retired (separate WP).
- **KD-1 / KD-2 — deviations not accepted; compliant mapping chosen (owner, 2026-10-01).** This supersedes the
  KD-1/KD-2 deviation text in §11/§16: the full Compact file set is delivered and the verifier runs with
  `--reference compact` on both screens, with no known deviation.
  - **KD-1 → `StockMovements/Edit.cshtml` = "Düzelt / Correct".** Never edits a movement. It posts (1) a REVERSAL
    of the original (server-derived legs, `sourceDocumentId` = original transactionId, MOD-0173 ASSUMPTION-0173-05)
    and then (2) the corrected movement as a new manual movement — two immutable records, two idempotency keys.
    Offered only for manual movements (`sourceModule MOD-0173`) that are not reversed and are not a reversal;
    automatic movements keep "reversed in the source module". If (2) fails after (1) succeeded, the page shows an
    explicit **partial-correction** state (reversal number + the error + "post corrected movement" retry with the
    same idempotency key) — never a silent half-correction. Prefill uses the history row; fields the contract
    row does not carry (source side, BG-05) are entered by the user. Permission: `inventory.movements.create`.
  - **KD-2 → `StockLedger/Create.cshtml` = "New stock entry".** No balance is written: the page hosts the same
    shared movement `_Form` partial (single source of truth, no duplicated form) restricted to the inbound manual
    types (Opening balance, Count gain) and posts `POST /movements`; the balance changes only as a projection.
    Edit/Details of a balance row stay N/A by nature (projection) and link to the movement history filtered to that row.

### WP-0173-UI1 implementation notes (2026-10-01, agent PASS pending CT)

UI-1 = S-1 `/StockLedger` + S-2 `/StockMovements` (+ Reversal) only; S-3 form, Correct (Edit), Details and
StockLedger/Create are UI-2. Evidence `docs/records/audits/2026-10/mod-0173-fu01-ui1-evidence/`.

- **UI1-01** Proxy profile kept (routes `/StockLedger/api`, `/StockMovements/api`); the verifier could not be run (no
  Python) to test the UI-01 alternative — the manual checklist (D5) stands in for it.
- **UI1-02** Shared MVC helpers (list adapter, Gateway client, `ProductPickerSource`, `LocationPickerSource`, label
  resolver, error map) live in `Models/Inventory/` (pack §5 allows only that folder besides the two controllers).
- **UI1-03** Reversal idempotency key = one per opened dialog (minted in the browser, validated as a UUID by MVC, sent
  as `reversal-{key}`): a double submit of the same dialog replays; a NEW dialog on an already reversed row gets
  422 ALREADY_REVERSED. "Already reversed" is shown only when the REVERSAL is on the same page (BG-05).
- **UI1-04** The Reversal dialog is the shared `window.showConfirm` (product rule: one dialog implementation); a refusal
  keeps it open through its `didOpen` seam (no raw SweetAlert).
- **UI1-05** Automatic movements show a PASSIVE Reversal button with "reversed in the source module" (WP-0173-UI1 text),
  for users who hold `movements.create`; a view-only user sees no Reversal at all (UAS-001 "no unclickable button").
- **UI1-06** S-1 History opens S-2 filtered by item + lot (URL → applied filters through the factory's Apply).
- **UI1-07** The factory's client-side Action/Export menu is removed on both lists (pack §2/§20: no export until the
  export gate) — a page-level deviation from `frontend-js-standard` §3, recorded here.
- **UI1-08** Product / SKU filters: Model A selectors searched on the server (GSKU + LSKU; FinishedGood not offered —
  its selector needs `mdm.finished-goods.create`). Warehouse / location: LOCATION via Gateway — no route (BG-10), so
  the pickers say "unavailable"; lot: typed text; dates: from/to.
- **UI1-09** Labels: one probe per master per page; an unanswered master leaves raw ids + the localized banner.
- **UI1-10** Manifest: `InventoryLedgerManifestProvider` (2 pages, 2 actions — only what UI-1 renders); wiring needs a
  `ProjectReference` to `Diten.BuildingBlocks.ModuleRegistration.Abstractions` in the InventoryService Api csproj
  (ProcurementService pattern); `PlatformRegistration` is NOT added to appsettings (outside the allowed paths) — the
  push is skipped with a warning until configured (smoke supplied it by environment).
- **FINDING (CT):** AuthService access tokens carry no `legal_entity_id` claim, while InventoryService (S1, JWT-only
  scope) requires one — every real user is 403 on every inventory endpoint. Needs an owner decision (Auth emits the
  LE claim, or InventoryService resolves a default LE). Not changed by UI-1 (both are protected paths).
- Translations of the 7 languages are model translations — native-speaker review pending (FU).

### Company selector (W-0173-02, owner 2026-10-01) and UI-1 acceptance

- **WP-0173-UI1 CT-ACCEPTED (2026-10-01)** — commits 119427544..d0b837902. CT re-measured: Diten.Web.Tests 377/377,
  InventoryService 282/282, architecture 18/18 (0 skipped); 0 deleted lines; SharedResource only 3 `Nav.*` keys; no
  browser call to 5066 or `/api/inventory`; no raw `Swal.fire`; `_LayoutTenantShell` explicit on both pages. Out-of-scope
  file accepted: `Diten.InventoryService.Api.csproj` +1 ProjectReference (ModuleRegistration.Abstractions, Procurement
  pattern). Carried as BLOCKED-ENV, not PASS: `verify_datatable_page.py` / `quality-gate-datatable` (no Python) and the
  vitest suite (no `node_modules`); manual checklist accepted as interim evidence. Real-login use blocked by the LE gap.
- **Company selector:** every inventory page shows a company (legal entity) selector in the page header (not in
  `_LayoutTenantShell`, which stays protected). Source: MDM `/api/legal-entities/lookup` through the Gateway (needs
  `mdm.legal-entities.read`; without it the page shows a localized "company list unavailable" state, no data). The
  choice is kept per user (same-origin cookie, no PII) and sent by `InventoryGatewayClient` as `X-Legal-Entity-Id`
  only when the token has no `legal_entity_id` claim. No selection → localized "select a company" state, no list call.
  Changing company reloads the page data. One isolated component so FU-LE-01 can replace it. 7 languages.

- **WP-0173-LE1 (agent result, pending CT, 2026-10-01) — company selector implemented.** One component:
  `Models/Inventory/CompanySelection.cs` + `Views/Inventory/Shared/_CompanySelector.cshtml` + `js/Inventory/company-selector.js`.
  The list is read SERVER-SIDE at page render (MDM lookup via Gateway) — no controller change; the choice is a
  same-origin cookie named per user (SHA-256 of `sub`), value = LE id only. `InventoryGatewayClient` adds
  `X-Legal-Entity-Id` to inventory calls only for a claim-less token; with no company it does not call the Gateway at all
  (synthetic `COMPANY_NOT_SELECTED` → "select a company"). Without a ready company the data part of both pages is not
  rendered. A token with the claim hides the selector. Also: a failed first list load now removes the export menu too
  (it is removed in `initComplete`, which a failed load never reaches); the shared processing indicator stays visible
  in that case (shared CSS — FU).

- **CT break-glass (2026-10-02, SOP §3.1):** the owner granted inventory permissions but the tenant menu showed no
  Inventory entry. Measured cause: `Diten.InventoryService` had no `PlatformRegistration` (BaseUrl / InternalApiKey), so
  `ModuleRegistrationHostedService` logged "Module self-registration skipped" and the INVENTORY-LEDGER manifest never
  reached the Platform module catalog (UI1-10 had left it out of scope). Fix: `appsettings.Development.json` gets the same
  `PlatformRegistration` section as ProcurementService (Platform 5057, shared local-dev internal key); service restarted.
  The menu additionally needs the module **entitled to the tenant** (Module Catalog → assignment, owner action) and a
  **fresh login** (permissions are read into the token at login). Non-Development environments still need this
  configured through their own secrets (follow-up FU-REG-01).

## 20. Follow-up Items

- Request to MOD-0290 owner (MDM): additive product/SKU search in PRODUCT-MASTER-BUNDLE v1.1 — retires W-UI-01.
- MOD-0174: register typed lots + map ledger rows before TRACE freeze — retires W-UI-02.

- **Shipment panel** — read-only SHIPMENT-BUNDLE v1 consumer view (MOD-0183), shown after MVP-6 integration; fields
  limited to the contract (`shipmentNumber`, `status`, `carrierId`→carrier name via `/carriers`, planned/actual
  dates, `pod.recipientName`, `lines`); "mode" comes from the Load, not the Shipment; no progress %/delay/vehicle.
- **Two-step in-transit transfer** — open decision (MVP-6); until then transfers are single-step.
- **CSV export** — separate export gate (SOP §18.7); adds the toolbar export (and its permission) when approved.
- **Reservation drawer** — after an additive `GET /reservations` (BG-08), SKU-level.
- **Free-text search / sorting / KPI without filter** — after contract v1.1 (BG-04, BG-07).
- **From / Recorded by / Already reversed** in history — after BG-05; re-open of a receipt by id after BG-06.
- **Reason codes** — after MOD-0048 publishes a movement-reason set and a tenant lookup (BG-03).
- **Real masters** — swap Prism mocks for MOD-0290 / Location master / MOD-0174 with gateway routes (BG-10).
- **InventoryGovernance retirement** — OD-UI-3, separate WP.
- **STATUS_TRANSFER UI** — belongs to MOD-0175 (quarantine lifecycle), not here.
