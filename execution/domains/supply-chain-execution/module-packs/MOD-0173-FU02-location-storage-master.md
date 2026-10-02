---
id: MOD-0173-FU02
name: Location / Storage Master
parent: MOD-0173
parent_name: Inventory Ledger & Valuation
domain: supply-chain-execution
service: Diten.InventoryService (5066 — owner of the frozen LOCATION v1 contract; consumed via Gateway /api/locations after INT-2) + frontend/Diten.Web
shell: tenant
golden_reference: slim
data_mode: server
screens:
  - module: Locations
    data_mode: server
entity_base: EntityBase
status: ready-for-dev
status_note: "ready-for-dev — owner approval (Ali) 2026-10-01; mockup v2 approved by CT (zone optional, bulk bins in scope, code always editable — ledger keys on immutable Id). Order: LOC1 → INT-2 → LOC2 → (FU01) UI-2. DCP-002 re-run pending (waiver W-0173-01)."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-10-01
target: 2026-10-31
form_field_count: 8
dependencies: [MOD-0173, MOD-0173-FU01, INT-2, MOD-0288, MDM-LEGAL-ENTITY, DEV-0000]
---

# MOD-0173-FU02 — Location / Storage Master

> **Identity (DCP-002 — FU of an existing Blueprint MOD, no new MOD minted).** Measured 2026-10-01:
> - Blueprint `master 8.1.xlsx` (Blueprint_Data, read from the workbook's shared strings): MOD-0173 SoR =
>   **"inventory balances, movements, valuation, locations (as scoped)"**. No other Blueprint module owns a physical
>   site / warehouse / bin master ("Warehouse (WMS)" is execution, MOD-0178…0182, not a master).
> - Owner status report "Supply Planning Procure Cap Block": *New canonical module IDs = 0*.
> - `docs/analysis/inventory-capability-scope-and-dependency-report.md` §9 DEC-INV-03 (shared master, Option B) and
>   §14.3 ("MOD-?" candidate) — resolved here as a follow-up of MOD-0173, the module whose SoR already names locations.
> - No `MOD-0173-FU02` exists in the repo (`execution/**` search: 0 hits).
> - The fail-closed preflight `verify_module_id.py --check-id MOD-0173-FU02 --parent MOD-0173` **could not run** (no
>   Python) → **DCP-002 re-run pending (waiver W-0173-01)**. No registry row is written here (CT action).
>
> **Authority:** this pack > `domain-config.md` > `AGENTS.md` > `.antigravity/`. The parent pack
> `MOD-0173-inventory-ledger-valuation.md` §8 (scope), §14 (`inventory.*` namespace, K1) and §19 W-0173-02 (company
> selector) are binding; the sibling `MOD-0173-FU01-inventory-ledger-ui.md` supplies the reused UI components (company
> selector, list adapter pattern, UAS-001, manifest/Nav pattern). The frozen `LOCATION` v1.0.0 read contract is not
> changed by anything here: the owner API is an **additive v1.1** (§15, §19).
>
> **Mockup v2** (approved by the CT, 2026-10-01) is a content/flow reference only; layout and colours come from Golden
> Reference Slim (DEV-0000) and the `.antigravity` UI rules.

## 1. Module Summary

The company's **single list of physical places** — Site → Warehouse → StorageLocation → Zone → Bin (DEC-INV-03,
Option B). The stock ledger (MOD-0173), WMS (MVP-5, MOD-0178…0182), procurement receiving and manufacturing all READ
it through the frozen LOCATION contract; nobody keeps a second copy. Users of a tenant legal entity (warehouse
managers, master-data stewards) create and maintain the hierarchy; everybody else consumes it.

| Screen | Route | Kind (UI pattern §17.3) | Purpose |
|---|---|---|---|
| **L-1 Tree + detail** | `/LocationTree` | Single detail/section — custom tree component, **not a DataTable** | Browse the hierarchy, read a node, start every action on it |
| **L-2 List** | `/Locations` | Table — DataTable, `data_mode: server` (thousands of bins) | Search / filter / bulk inspection |
| **L-3 Create / Edit form** | offcanvas on L-1 and L-2 | Form (Slim, 8 fields) | Create a node, edit it, move it (parent change) |
| **L-4 Bulk bin generation** | modal on L-1 (Zone / StorageLocation node) | Form + preview | Generate up to 1000 bins in one operation |

**Pattern boundaries:** L-1 never pages or filters (it lazily expands one parent at a time and hands over to L-2 for
"show all children" above 500 children); L-2 never shows a tree; L-3 is the only place a field is edited; status
changes are row/detail actions with a confirmation, not form fields; L-4 only creates bins.

## 2. Ownership and Boundaries

**Owns (single writer, K15):** `Location` (one aggregate for all five levels) and its append-only change history
`LocationChangeEntry`; the LOCATION contract implementation (v1 reads + v1.1 owner writes).

**In scope**
- Backend in `Diten.InventoryService`: entity, repository, the 4 frozen v1 read operations, the v1.1 owner operations
  (§15), in-process adapter for the ledger (§8), stock-presence read from the ledger (K-10).
- Tenant-shell screens L-1…L-4, 7 languages, UAS-001, company selector reuse (W-0173-02).
- Self-registration pages/actions in the existing `INVENTORY-LEDGER` manifest (§14).

**Out of scope**
- Using `OrganizationUnit` as a place (report §14.3, K6): an org unit is only an optional **soft** link (`orgUnitRef`).
- WMS behaviour (putaway rules, capacity, bin types, slotting) — MOD-0178+.
- Deleting a location (K-04: no delete; Archived is the end state).
- Quantities on location screens (K-10: presence only; quantities live in `/StockLedger`).
- Changing the LOCATION v1 read operations or the `LocationRef` shape.
- A cost-centre master (none exists — §7); `costCenterCode` is a validated text code.
- Cross-company linking (K-03).

## 3. Owned Objects

| Object | Kind | Notes |
|---|---|---|
| `Location` | Entity (`EntityBase`, Tenant + LegalEntity scoped) | All five levels; `type` decides the allowed parent (K-01) |
| `LocationChangeEntry` | Entity (append-only) | One row per created location and per changed field (K-11) |
| `inventory_locations`, `inventory_location_history` | Mongo collections | in the InventoryService database |
| `LocationsController` (service) | API | base `/api/locations` (contract `servers`) |
| `InventoryLocationDirectory` | in-process adapter | implements the ledger's `ILocationMasterClient` from the repository (§8) |
| `LocationTreeController`, `LocationsController` (Web) | MVC | `[Authorize]`; page + same-origin proxies (§8) |
| Permissions | `inventory.locations.read`, `inventory.locations.manage` | `manage` = Tier-3 domain action, documented here (PKS-001 §5); namespace `inventory.*` owned by InventoryService (K1) |

## 4. Entity Fields

**`Location`** (frozen `LocationRef` mapping in the last column — §15)

| Field | Type | Rule | `LocationRef` |
|---|---|---|---|
| `Id` | Guid | system identity, **never changes**; the ledger stores THIS (K-02) | `locationId` (UUID string) |
| `TenantId`, `LegalEntityId` | Guid | `EntityBase`; LE = the Site's company, inherited, immutable (K-03) | `legalEntityId` |
| `Code` | string 1–40 `[A-Za-z0-9._/-]` | unique per company, case-insensitive, **always editable** (K-02) | `code` |
| `CodeNormalized` | string | `Code` upper-invariant; unique index with Tenant + LE | — |
| `Name` | string 1–120 | trimmed | `name` |
| `Type` | enum `Site · Warehouse · StorageLocation · Zone · Bin` | set at create, immutable (ASSUMPTION-LOC-04) | `type` |
| `ParentId` | Guid? | null only for Site; allowed parent type per K-01 | `parentId` |
| `AncestorIds` | Guid[] | materialized path root→parent, rewritten on a move (subtree queries, K-05/K-07) | — (`getLocationPath` builds `path`) |
| `Status` | enum `Active · Inactive · Archived` | K-04…K-06; new nodes are Active | `status` |
| `IsStockable` | bool | default Bin = true, others false; StorageLocation may be true (K-08) | `isStockable` |
| `OperatingMode` | `internal · external-integrated`? | only Site / Warehouse (DEC-INV-19); null below; locked while stock below (K-09) | `operatingMode` (own value; null for lower levels — ASSUMPTION-LOC-05) |
| `OrgUnitRef` | string? (Guid of a Platform OrganizationUnit of the SAME LE) | soft link, never a FK; validated on write only (§7) | `orgUnitRef` |
| `CostCenterCode` | string? 1–40 `[A-Za-z0-9._-]` | free code (no master exists, §7) | `costCenterCode` |
| `StockTouchSeq` | long | bumped by every ledger posting to this location (serializes K-05/K-07/K-08 against postings — §8) | — |
| `Version` | int | optimistic concurrency → `ETag: W/"<Version>"` on owner reads/writes | — (not in v1) |

**`LocationChangeEntry`** — `Id`, `TenantId`, `LegalEntityId`, `LocationId`, `Action` (`Created · Updated · Moved ·
StatusChanged · BulkCreated`), `Field` (`code · name · parentId · status · isStockable · operatingMode · orgUnitRef ·
costCenterCode`, null for Created), `OldValue`, `NewValue` (strings), `ActorUserId`, `CorrelationId`, `LeSource`
(`token | header-unverified-membership`, W-0173-02), `OccurredAt`. Written in the **same transaction** as the change;
no update, no delete endpoint (K-11).

**Form fields (8 → Slim):** ① Code* ② Name* ③ Type* (create only) ④ Parent* (not for Site) ⑤ Stockable ⑥ Operating
mode (Site / Warehouse only) ⑦ Org unit (optional) ⑧ Cost centre (optional). The company is **not** a field: a Site
takes the selected company (or the token's), everything below inherits it (K-03, K-14).

## 5. Repo Scope

The development WPs (LOC1, LOC2) may write only:

- `services/Diten.InventoryService/src/**/Locations/**` (entity, repository, handlers, controller, in-process adapter),
  the DI/registration lines that wire them, the scope middleware's base-path list (`/api/locations`) and the ledger's
  location-touch hook (§8) — no change to any inventory endpoint, rule or contract
- `services/Diten.InventoryService/tests/**` (new location tests; API-test scenarios switch from `StubLocationMaster` to
  real locations — §19)
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/ModuleRegistration/InventoryLedgerManifestProvider.cs`
  (+ its completeness test)
- `docs/analysis/contracts/location.openapi.yaml` — **LOC1 only, single-writer step, additive v1.1** (§15)
- `frontend/Diten.Web/Controllers/LocationTreeController.cs`, `frontend/Diten.Web/Controllers/LocationsController.cs`
- `frontend/Diten.Web/Models/Inventory/**` (reuse `CompanySelection`, `InventoryGatewayClient`; add location models)
- `frontend/Diten.Web/Views/Inventory/LocationTree/**`, `frontend/Diten.Web/Views/Inventory/Locations/**`
- `frontend/Diten.Web/wwwroot/assets/js/Inventory/LocationTree/**`, `…/js/Inventory/Locations/**`
- `frontend/Diten.Web/Resources/Views/Inventory/LocationTree/LocationTreeIndex.{7}.resx`,
  `…/Locations/LocationsIndex.{7}.resx`
- `frontend/Diten.Web/Resources/SharedResource.{7}.resx` — **only** `Nav.Page.LOCATIONTREE` (§14)
- FU01 files for the "view in stock list" deep link only: S-1/S-2 reading `locationId` from the URL (§11)
- `frontend/Diten.Web.Tests/**`, `docs/records/audits/<yyyy-mm>/mod-0173-fu02-*`, this pack (§19 only after ready-for-dev)

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, `CLAUDE.md`; `execution/registries/**`, `execution/portfolio/**`, `domain-config.md`,
  DCP files, the parent pack and FU01 pack (proposals go to the CT)
- Every frozen contract except the LOC1 additive edit of `location.openapi.yaml`; `inventory-bundle.openapi.yaml` untouched
- `gateway/**` (INT-2, integration-agent)
- Platform (`OrganizationUnit` — read-only consumer), MdmService, AuthService, every other service
- `_LayoutTenantShell`, `_Layout`, shared JS/CSS (`diten-datatable.js`, `dt-defaults.js`, `backbone-custom.css`,
  `_ListShell`, `_TableSkeleton`, `_GlobalConfirmation`)
- The FU01 company-selector component files (`CompanySelection.cs`, `_CompanySelector.cshtml`, `company-selector.js`) —
  reused as is

## 7. Dependencies

| Dependency | State (measured 2026-10-01) | Effect |
|---|---|---|
| MOD-0173 S1/S2/INT1/K1/UI1/LE1 | CT-accepted (LE1 pending CT) | same service, ledger, scope middleware, `inventory.*` |
| W-0173-02 company selector (FU01 component) | implemented in LE1 | reused for L-1/L-2 (K-14) |
| LOCATION v1 (frozen read contract) | Prism mock only, **no implementation** | implemented here (LOC1) |
| Gateway `/api/locations/**` | **missing** (`ocelot.json` has no route) | INT-2 required before LOC2 and before any external consumer |
| Platform `OrganizationUnit` (MOD-0288 owner) | `GET /api/platform/organization-units` (gateway route exists, `platform.organization-units.read`), entity has `LegalEntityId`, `CostCenterCode`, `Status`, `IsArchived` | org-unit picker = units of the SAME LE, Active, not archived; soft link only |
| Cost-centre master | **none** in the repo (only `OrganizationUnit.CostCenterCode` text) | `costCenterCode` = validated free code; picker deferred (§20) |
| MDM legal entities | live (LE1) | company of a Site = active LE of the tenant |
| Golden Reference Slim (DEV-0000) | live | structure for L-2 / L-3 |

## 8. Runtime Constraints

- **Scope:** the MOD-0173 scope middleware covers `/api/locations` too (one rule set): tenant + user from the JWT,
  legal entity from the claim or — claim-less — from the verified company header (W-0173-02). Every query is
  `TenantId + LegalEntityId` filtered. A location of another company is **404** to the caller (not 403 — it does not
  exist in their company), except an explicit cross-LE parent in a write, which is **403 CROSS_LE_FORBIDDEN**.
- **Frozen `listLocations` `legalEntityId` query parameter:** accepted only when equal to the resolved scope LE (it is
  part of the frozen contract); any other value → **403 CROSS_LE_FORBIDDEN** (ASSUMPTION-LOC-06). The inventory
  endpoints keep their 400 rule for LE in the query.
- **Ledger link (in-process, no HTTP):** the ledger's `ILocationMasterClient` is re-registered to
  `InventoryLocationDirectory`, which answers `ValidateLocationsAsync / GetLocationAsync / GetLocationPathAsync` from the
  repository with the request's scope. `LocationMasterHttpClient` stays for the Prism contract smoke but is no longer
  the ledger's source (ASSUMPTION-LOC-07). External consumers (procurement, WMS, manufacturing) use HTTP + Gateway.
- **Stock presence (K-10), no second stock store:** "has stock" = at least one ledger balance row of the same Tenant +
  LE with `OnHand ≠ 0` (any status) whose `LocationId` is the node (self) or in its subtree (`AncestorIds` query), or
  whose `WarehouseId` is the node for a Warehouse. Read from the ledger collections in-process; no quantity leaves.
- **Posting vs. lifecycle race:** every ledger posting that touches a location increments that location's
  `StockTouchSeq` **inside the posting transaction**; every K-05/K-07/K-08/K-09 check runs inside the lifecycle
  transaction and writes the same document → a concurrent posting and status/move/stockable change get a Mongo
  WriteConflict and one retries on the committed state (same technique as S2 SKU positions). The posting re-reads the
  location status inside its transaction (K-13).
- **Idempotency:** every owner POST takes `Idempotency-Key` (header = body `idempotencyKey`), stored with a request
  fingerprint; replay → 200 with the original result; same key, different body → 400 `IDEMPOTENCY_KEY_REUSED`.
- **Concurrency:** owner reads return `ETag: W/"<Version>"`; PATCH and status changes require `If-Match`
  (missing → 400 `INVALID_REQUEST`, stale → 409 `LOCATION_CONFLICT`).
- **Correlation:** `X-Correlation-Id` as MOD-0173 (UUID or minted), persisted on every history row.
- **Browser** talks only to same-origin MVC; MVC calls Gateway 5000 with the FU01 `InventoryGatewayClient` (company
  header rule included). No browser call to 5066 or `/api/locations`.

## 9. Layout & Shell Contract

- `shell: tenant` → every page sets `Layout = "_LayoutTenantShell";` explicitly: `Views/Inventory/LocationTree/Index.cshtml`,
  `Views/Inventory/Locations/Index.cshtml` (grep = 2). Partials set no layout; `_ViewStart` unchanged.
- Folders `Views/Inventory/LocationTree/` and `Views/Inventory/Locations/` (`{Area}` = folder, not ASP.NET Areas).
  **Folder decision:** the DataTable verifier resolves one list per folder; L-1 is not a list, so it has its own folder.
- Routes `/LocationTree` and `/Locations` (tenant routes, not `/Platform/…` — permission scope Tenant).
- Company selector partial (FU01) on both pages, under the page header; without a ready company the data part is not
  rendered (FU01 LE1 behaviour).
- L-1 ↔ L-2 = two real routes styled as pills (`wc-tab-compact`), like S-1 ↔ S-2.
- 7 languages; `ar` RTL through the shell (the tree indents with logical CSS properties — `margin-inline-start`).

## 10. Backend File Convention

```text
services/Diten.InventoryService/src/
├── Diten.InventoryService.Domain/Entities/Location.cs, LocationChangeEntry.cs
├── Diten.InventoryService.Domain/Rules/LocationHierarchyRules.cs        (K-01 parent matrix, K-07 subtree, defaults K-08)
├── Diten.InventoryService.Domain/Repositories/ILocationRepository.cs
├── Diten.InventoryService.Application/Features/Locations/
│   ├── Commands/ (CreateLocation, UpdateLocation, ChangeLocationStatus, BulkCreateBins) + handlers + validators
│   ├── Queries/  (GetLocation, ListLocations, GetLocationPath, ValidateLocations, SearchLocations, GetLocationHistory, GetLocationStockPresence)
│   ├── LocationsModels.cs                                              (wire DTOs: LocationRef v1 + v1.1 shapes)
│   └── LocationErrorCodes.cs
├── Diten.InventoryService.Persistence/Repositories/LocationRepository.cs, InventoryLocationDirectory.cs
└── Diten.InventoryService.Api/Controllers/LocationsController.cs     ([HasPermission] with InventoryPermissions constants)
```

`InventoryPermissions` gains `LocationsRead = "inventory.locations.read"` and `LocationsManage =
"inventory.locations.manage"`. Indexes added to the startup gate: unique (TenantId, LegalEntityId, CodeNormalized);
(TenantId, LegalEntityId, ParentId); (TenantId, LegalEntityId, AncestorIds); history (TenantId, LegalEntityId,
LocationId, OccurredAt −1).

## 11. Frontend File Contract

**L-1 LocationTree (custom component, not a DataTable):**
```text
Views/Inventory/LocationTree/
├── Index.cshtml          (Layout = "_LayoutTenantShell"; UAS-001; company selector; pills; tree + detail panel)
├── _DetailPanel.cshtml   (fields, status badge, path breadcrumb, stock presence, actions by permission)
├── _IndexL10n.cshtml     (<script id="locationtree-l10n" type="application/json">)
└── LocationTreeIndex.cs  (marker)
wwwroot/assets/js/Inventory/LocationTree/{index.js, index.l10n.js}
```
Tree: `role="tree"` / `treeitem`, `aria-expanded`, keyboard (↑↓←→ Home End Enter), lazy children per node, card
skeleton while loading, "open in list" above 500 children.

**L-2 Locations (Golden Reference Slim, server mode, proxy profile):**
```text
Views/Inventory/Locations/
├── Index.cshtml              (Layout; UAS-001; company selector; pills; _Filter; _DataTable; offcanvas)
├── _Filter.cshtml            (type multi-chip · status multi-chip · stockable single · parent picker)
├── _DataTable.cshtml         (_ListShell: DataMode = "server", HasSelection = false)
├── _CreateEditOffcanvas.cshtml   (L-3 — the ONE form, also rendered on L-1)
├── _BulkBinsModal.cshtml     (L-4 — rendered on L-1)
├── _IndexL10n.cshtml
└── LocationsIndex.cs
wwwroot/assets/js/Inventory/Locations/{index.js, index.l10n.js, form.js, bulk-bins.js}
```
- L-2 columns: Code · Name · Type · Parent path · Status · Stockable · Operating mode · Actions. Search box and
  sorting ARE offered (code, name, type, status) — the v1.1 `searchLocations` operation supports them (unlike FU01 BG-04).
- Row actions (by permission): Edit · Add child · Activate / Deactivate / Archive · History · View in stock list.
  No bulk selection, no import, no export button until the export gate.
- Confirmations through `window.showConfirm` (Archive = danger, irreversible wording); refusals stay in the dialog via
  its `didOpen` seam (FU01 pattern, no raw `Swal.fire`).
- **"View in stock list" (K-10):** `/StockLedger?locationId={id}` — LOC2 extends FU01 S-1 (and S-2) to read
  `locationId` from the URL into the applied filters (FU01 S-2 already does this for `itemId`/`lotId`).

## 12. Validation Rules

| # | Field | Required | Rule | Server error |
|---|---|---|---|---|
| 1 | Code | yes | trim; 1–40; `^[A-Za-z0-9._/-]+$`; unique per company case-insensitive (all statuses — ASSUMPTION-LOC-03) | 409 `LOCATION_CODE_TAKEN` |
| 2 | Name | yes | trim; 1–120 | 400 `INVALID_REQUEST` |
| 3 | Type | yes (create) | one of 5; immutable after create | 400 `INVALID_REQUEST` |
| 4 | Parent | yes unless Site | same company; parent type per K-01; Active (K-06); not the node or its subtree (K-07) | 422 `LOCATION_LEVEL_INVALID` · 422 `LOCATION_PARENT_INACTIVE` · 422 `LOCATION_MOVE_INTO_SUBTREE` · 404 `UNKNOWN_LOCATION` |
| 5 | Stockable | no | default by type (K-08); turning off with stock refused | 422 `LOCATION_HAS_STOCK` |
| 6 | Operating mode | Site/Warehouse only | `internal · external-integrated`; forbidden below Warehouse; locked while stock below (K-09) | 400 `INVALID_REQUEST` · 422 `LOCATION_HAS_STOCK` |
| 7 | Org unit | no | UUID of an Active, non-archived OrganizationUnit of the SAME LE (checked on write via Platform; unreachable → 503 `ORG_UNIT_DIRECTORY_UNAVAILABLE`, nothing written) | 422 `ORG_UNIT_INVALID` |
| 8 | Cost centre | no | trim; 1–40; `^[A-Za-z0-9._-]+$` | 400 `INVALID_REQUEST` |

**K-01 parent matrix** (`LocationHierarchyRules`): Site → none · Warehouse → Site · StorageLocation → Warehouse ·
Zone → StorageLocation · Bin → Zone **or** StorageLocation (Zone optional — owner).

**Status transitions (K-04…K-06):** Active ⇄ Inactive; Active/Inactive → Archived (terminal). Archived → anything:
409 `LOCATION_ARCHIVED`. Deactivate/Archive with stock on the node or below: 422 `LOCATION_HAS_STOCK`; with an Active
child: 422 `LOCATION_HAS_ACTIVE_CHILDREN`. Activate under a non-Active parent: 422 `LOCATION_PARENT_INACTIVE`.
Any change on an Archived node (edit, move, child add): 409 `LOCATION_ARCHIVED`.

**Move (K-07):** parent change refused with stock on the node or below (422 `LOCATION_HAS_STOCK`), into its own
subtree (422 `LOCATION_MOVE_INTO_SUBTREE`), or against K-01 (422 `LOCATION_LEVEL_INVALID`). A move rewrites
`AncestorIds` of the whole subtree in one transaction.

**Bulk bins (K-12):** target Zone or StorageLocation (Active, same company); `aisleFrom..aisleTo` (one letter A–Z or
1–3 digits, same kind), `rackFrom..rackTo` and `levelFrom..levelTo` (1–99), `positionsPerLevel` (1–99); codes
`{AISLE}-{RACK:00}-{LEVEL:00}-{POS:00}`; count = aisles × racks × levels × positions, 1…1000 (else 400
`LOCATION_BULK_LIMIT`); existing codes (any status) skipped and listed; `dryRun: true` returns the preview without
writing; the real run creates all in ONE transaction with one `BulkCreated` history row per bin.

Client validation mirrors the shape; the service stays the authority (codes mapped to 7-language messages, §13).

## 13. Failure Path to Verify

Every case: nothing partially written, localized message (7 languages) with the correlation id in a details
disclosure, never "403"/"Forbidden" (UAS-001).

- **Duplicate code** (create, edit, bulk) → 409 `LOCATION_CODE_TAKEN` on the Code field; bulk lists skipped codes.
- **Wrong level** (Bin under Warehouse, Zone under Site …) → 422 `LOCATION_LEVEL_INVALID`.
- **Deactivate / archive a node with stock** → 422 `LOCATION_HAS_STOCK` + "view in stock list" link; no change.
- **Deactivate a node with an Active child** → 422 `LOCATION_HAS_ACTIVE_CHILDREN`.
- **Activate under an inactive parent** → 422 `LOCATION_PARENT_INACTIVE`.
- **Move with stock / into own subtree** → 422 `LOCATION_HAS_STOCK` / `LOCATION_MOVE_INTO_SUBTREE`.
- **Stockable off with stock** → 422 `LOCATION_HAS_STOCK`.
- **Operating mode change with stock below** → 422 `LOCATION_HAS_STOCK` (K-09).
- **Touch an Archived node** → 409 `LOCATION_ARCHIVED`.
- **Stale edit** (another user saved first) → 409 `LOCATION_CONFLICT` → "data changed, reload"; no silent retry.
- **Double submit** (create / bulk) → same `Idempotency-Key` → 200 replay, one node / one batch.
- **Bulk above 1000** → 400 `LOCATION_BULK_LIMIT` before preview.
- **Company missing / foreign** → "select a company" (no call) · 403 `CROSS_LE_FORBIDDEN` (W-0173-02).
- **Org-unit directory down** → 503, nothing written; the field can be left empty and saved.
- **Unauthorized** (no `inventory.locations.read`) → `_AccessDenied` in the shell on both routes; read-only →
  no create / edit / status / bulk controls (absent, not disabled).
- **Inactive / archived location chosen in a stock form** → impossible (pickers list Active stockable only);
  a hand-crafted posting → 422 `UNKNOWN_LOCATION` (MOD-0173 rule, K-13).

## 14. Authorization Convention

| Key | Gate |
|---|---|
| `inventory.locations.read` | L-1, L-2 (UAS-001 when absent), all GET operations (v1 + v1.1), history, stock presence, MVC proxies |
| `inventory.locations.manage` | create, edit (incl. move), status change, bulk bins; New / Edit / Add child / status / Bulk controls |
| `inventory.stock.read` | "View in stock list" link (the target page's own gate) |

- `manage` is a **Tier-3 domain action** registered by this pack (PKS-001 §5): one permission for the steward role
  (create + update + lifecycle), because splitting create/update/status would let a user create what they cannot
  correct. Archive is marked `IsDangerous`.
- Consumers of the frozen read contract over HTTP (procurement, WMS, manufacturing users) need
  `inventory.locations.read`; the ledger needs none (in-process).
- No role shortcut (FU-S1-02 applies to the whole service).

**Self-registration — same module `INVENTORY-LEDGER`** (decision): locations are the prerequisite of every stock
row, live in the same service, use the same permission namespace and the same company selector; a separate
tenant-assignable module would let a tenant hold stock without the place master. Pages and actions appended to
`InventoryLedgerManifestProvider` (zero drift — only what LOC2 renders):

| PageCode | RoutePath | RequiredPermission | Nav-visible | Parent |
|---|---|---|---|---|
| `LOCATIONTREE` | `/LocationTree` | `inventory.locations.read` | **true** | — |
| `LOCATIONS` | `/Locations` | `inventory.locations.read` | false | `LOCATIONTREE` |

| ActionCode | Page | Placement | PermissionKey | IsDangerous |
|---|---|---|---|---|
| `NEWLOCATION` | LOCATIONTREE, LOCATIONS | Toolbar | `inventory.locations.manage` | false |
| `EDITLOCATION` | both | RowAction / detail | `…manage` | false |
| `ADDCHILDLOCATION` | both | RowAction / detail | `…manage` | false |
| `CHANGELOCATIONSTATUS` | both | RowAction / detail | `…manage` | **true** |
| `BULKBINS` | LOCATIONTREE | detail (Zone / StorageLocation) | `…manage` | false |
| `LOCATIONHISTORY` | both | RowAction / detail | `inventory.locations.read` | false |
| `VIEWLOCATIONSTOCK` | both | RowAction / detail | `inventory.stock.read` | false |

Nav keys: `Nav.Page.LOCATIONTREE` in all 7 `SharedResource` files (domain and module keys already exist).
The module then has two nav-visible pages (Stock Ledger, Locations) — the UI1 completeness test's "exactly one
nav-visible page" assertion is updated by LOC2 to the measured set.

## 15. Gateway / API Routing Decision

**Gateway: change REQUIRED — `/api/locations` + `/api/locations/{everything}` → `localhost:5066`, methods GET, POST,
PATCH, OPTIONS; `Authorization`, `X-Tenant-Id`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`,
`If-Match` pass through; `ETag` returned. Owned by a separate integration-agent WP (INT-2), not by this pack.**

**Frozen v1 reads (unchanged, implemented by LOC1):**

| Operation | Route | Notes |
|---|---|---|
| `getLocation` | `GET /api/locations/{id}` | `LocationRef`; unknown / other company → 404 `UNKNOWN_LOCATION` |
| `listLocations` | `GET /api/locations?type&parentId&legalEntityId` | `{ items, total, contractVersion }`, all statuses (status field tells consumers — K-13); unpaged by contract (bounded by parent/type filters; ASSUMPTION-LOC-08) |
| `getLocationPath` | `GET /api/locations/{id}/path` | root Site → node |
| `validateLocations` | `POST /api/locations/validate` | `{ results: [{ locationId, exists, isStockable }] }` — `exists` = in the caller's company (any status) |

**v1.1 additive owner API (designed here; written into `location.openapi.yaml` by LOC1 as one single-writer step;
v1 consumers unaffected):**

| Operation | Route | Request | Success | Errors |
|---|---|---|---|---|
| `createLocation` | `POST /api/locations` | header `Idempotency-Key`; `LocationCreateRequest { idempotencyKey, code, name, type, parentId?, isStockable?, operatingMode?, orgUnitRef?, costCenterCode? }` — no `legalEntityId` | 201 `LocationDetail` + `ETag` · 200 replay | 400 `INVALID_REQUEST` / `IDEMPOTENCY_KEY_REUSED` · 403 `CROSS_LE_FORBIDDEN` · 404 `UNKNOWN_LOCATION` (parent) · 409 `LOCATION_CODE_TAKEN` / `LOCATION_ARCHIVED` (parent) · 422 `LOCATION_LEVEL_INVALID` / `LOCATION_PARENT_INACTIVE` / `ORG_UNIT_INVALID` · 503 `ORG_UNIT_DIRECTORY_UNAVAILABLE` |
| `updateLocation` | `PATCH /api/locations/{id}` | header `If-Match`; `LocationUpdateRequest { code?, name?, parentId?, isStockable?, operatingMode?, orgUnitRef?, costCenterCode? }` (absent = unchanged; `type` not accepted) | 200 `LocationDetail` + new `ETag` | 400 · 404 · 409 `LOCATION_CODE_TAKEN` / `LOCATION_CONFLICT` / `LOCATION_ARCHIVED` · 422 `LOCATION_LEVEL_INVALID` / `LOCATION_MOVE_INTO_SUBTREE` / `LOCATION_HAS_STOCK` / `LOCATION_PARENT_INACTIVE` / `ORG_UNIT_INVALID` |
| `changeLocationStatus` | `POST /api/locations/{id}/status` | header `If-Match`; `{ status: Active \| Inactive \| Archived }` | 200 `LocationDetail` + `ETag` | 400 · 404 · 409 `LOCATION_ARCHIVED` / `LOCATION_CONFLICT` · 422 `LOCATION_HAS_STOCK` / `LOCATION_HAS_ACTIVE_CHILDREN` / `LOCATION_PARENT_INACTIVE` |
| `bulkCreateBins` | `POST /api/locations/{id}/bins/bulk` | header `Idempotency-Key`; `BulkBinsRequest { idempotencyKey, aisleFrom, aisleTo, rackFrom, rackTo, levelFrom, levelTo, positionsPerLevel, isStockable? (default true), dryRun }` | `dryRun`: 200 `{ toCreate: [code], skipped: [code], count }` · real: 201 `{ created: [LocationRef], skipped: [code], count }` · 200 replay | 400 `INVALID_REQUEST` / `LOCATION_BULK_LIMIT` / `IDEMPOTENCY_KEY_REUSED` · 404 · 409 `LOCATION_ARCHIVED` · 422 `LOCATION_LEVEL_INVALID` (target not Zone/StorageLocation) / `LOCATION_PARENT_INACTIVE` |
| `searchLocations` | `GET /api/locations/search?page&pageSize(≤200)&search&type*&status*&isStockable&parentId&orderBy(code\|name\|type\|status)&orderDir` | — | 200 `{ items: [LocationRef], total, page, pageSize, contractVersion }` | 400 `INVALID_REQUEST` |
| `getLocationHistory` | `GET /api/locations/{id}/history?page&pageSize` | — | 200 `{ items: [LocationChange], total, page, pageSize }` newest first | 404 |
| `getLocationStockPresence` | `GET /api/locations/{id}/stock-presence` | — | 200 `{ locationId, hasStock, subtreeHasStock }` (no quantities — K-10) | 404 |

`LocationDetail` = `LocationRef` + `version`, `createdAt`, `createdBy`, `updatedAt`, `updatedBy` (additive fields).
`LocationChange` = `{ changeId, action, field, oldValue, newValue, actorUserId, occurredAt, correlationId }`.
Error body = the contract `Error`. `/search`, `/validate` are literal segments; ids are UUIDs, so they never collide
with `/{id}`.

## 16. Acceptance Criteria

K → AC mapping (each with its evidence; **S** = a guard that needs a sabotage proof: break it → its test red →
restore → green, diff empty):

| K | AC | Measured by | S |
|---|---|---|---|
| K-01 | AC-01 every disallowed (type, parent type) pair — 20 of the 25, incl. any parent for a Site — and the 4 non-Site types without a parent → 422 `LOCATION_LEVEL_INVALID`; the 5 allowed pairs (incl. Bin→StorageLocation) and a parentless Site → 201 | API tests on the real pipeline | **S** |
| K-02 | AC-02 code unique per company case-insensitively (create, edit, bulk) → 409; same code in ANOTHER company → 201; code edit keeps `locationId`, ledger rows still resolve; history row `code old→new` | API tests + ledger read after rename | **S** |
| K-03 | AC-03 a Site takes the scope company; children inherit; parent of another company → 403/404; LE never accepted in a body; a company-B user sees none of company A's nodes in any operation | API tests (two companies, W-0173-02 header + claim tokens) | **S** |
| K-04 | AC-04 Archived → any change 409 `LOCATION_ARCHIVED`; no DELETE route exists (405/404) | API tests + route table test | — |
| K-05 | AC-05 deactivate/archive with stock on node or subtree → 422 `LOCATION_HAS_STOCK`; with Active child → 422 `LOCATION_HAS_ACTIVE_CHILDREN`; nothing written | API tests with real postings | **S** |
| K-06 | AC-06 activate under Inactive parent → 422 `LOCATION_PARENT_INACTIVE` | API tests | **S** |
| K-07 | AC-07 move with stock (self or subtree) → 422; into own subtree → 422; level rule applies; a valid move rewrites `AncestorIds` of the subtree (paths correct afterwards) | API tests | **S** |
| K-08 | AC-08 defaults per type; stockable off with stock → 422; StorageLocation can be stockable and receive a posting | API + ledger tests | — |
| K-09 | AC-09 operating mode only on Site/Warehouse (400 below); change with stock below → 422 | API tests | — |
| K-10 | AC-10 `stock-presence` true/false from ledger rows (self, subtree, warehouse); no quantity in any location response; "view in stock list" opens S-1 filtered by `locationId` | API tests + browser smoke | — |
| K-11 | AC-11 create and every field change write one history row (who, when, old → new, correlation, LeSource) in the same transaction; no update/delete route on history | API tests + Mongo evidence | **S** (history write in transaction) |
| K-12 | AC-12 bulk: preview = no write; real run = one transaction, skipped codes listed, ≤ 1000 else 400, replay = no duplicate, one history row per bin | API tests + Mongo evidence | — |
| K-13 | AC-13 Inactive/Archived appear in v1 reads with `status`; the ledger refuses postings to them (422 `UNKNOWN_LOCATION`); FU01/UI-2 pickers list Active stockable only | ledger API tests through the in-process adapter | **S** (in-process adapter status check) |
| K-14 | AC-14 claim-less users work only in the selected company (L-1/L-2 not drawn without it); token-LE users see no selector | browser smoke (real login) + render tests | — |

Plus:
- AC-15 LOCATION v1 conformance: every v1 response of the REAL service validates against `location.openapi.yaml`
  (contract-validating handler, as MOD-0173 does for INVENTORY-BUNDLE).
- AC-16 Concurrency: stale `If-Match` → 409; a posting racing a deactivation never leaves stock on an Inactive
  location (WriteConflict proof, as S2 AC-R9).
- AC-17 Idempotency on create and bulk (replay, key reuse).
- AC-18 UAS-001 on both routes, live-tried; read-only users get no manage controls; `grep "Permission denied\|Forbidden"` = 0.
- AC-19 7-language resx parity for both families, no English copies; `Nav.Page.LOCATIONTREE`; `NavManifestL10nGuardTests` green.
- AC-20 Manifest completeness (both directions) + catalog after restart (module, 4 pages total, actions, idempotent).
- AC-21 `Layout = "_LayoutTenantShell"` grep = 2 new pages; L-2 `data-dt-data-mode="server"` = `createList({ dataMode: 'server' })`.
- AC-22 Ledger regression: all MOD-0173 tests green with `StubLocationMaster` replaced by real locations where the
  scenario needs them.

## 17. Test Expectations

- Builds 0 errors / 0 new warnings; InventoryService, Diten.Web.Tests and `tests/architecture` green, 0 skipped.
- API tests on the real pipeline + replica-set Mongo (DB-010: shared test DB, fresh Tenant per scenario).
- Contract conformance test for the v1 reads (and v1.1 once LOC1 writes it).
- Sabotage proofs for every **S** row of §16.
- Web: adapter/proxy tests (search translation, company header), UAS render tests, resx parity, manifest guard.
- Runtime E4 (LOC2): real login → company → build a small tree → bulk bins → post stock into a bin (FU01 UI-2 or API)
  → deactivation refused (`LOCATION_HAS_STOCK`) → view in stock list; screenshots + API + Mongo + history.
- Verifier `verify_datatable_page.py --area Inventory --module Locations --reference slim --data-mode server
  --api-profile proxy` — BLOCKED-ENV while no Python (manual checklist as in UI1).

## 18. Ready-for-dev Checklist

- [x] Golden Reference Slim (DEV-0000) read; FU01 components identified for reuse.
- [x] Frontmatter complete; `form_field_count: 8` → `slim`; `screens[]` = the DataTable screen (L-2, server).
- [x] Layout & Shell contract explicit (2 pages).
- [x] Backend file convention + owner API v1.1 designed (§15); frozen v1 untouched.
- [x] Validation rules for 8 fields + K-01 matrix + lifecycle + bulk.
- [x] Failure paths (16).
- [x] Authorization + manifest + Nav keys.
- [x] Gateway decision (INT-2).
- [x] K-01…K-14 → AC mapping with sabotage-marked guards (§16).
- [x] Org-unit and cost-centre sources measured (§7).
- [ ] **DCP-002** `verify_module_id.py --check-id MOD-0173-FU02 --parent MOD-0173` exit 0 (waiver W-0173-01).
- [ ] Registry FU row (`Type = Follow-up`, Replacement ID = `MOD-0173`) — CT.
- [ ] Owner approval of: the v1.1 owner API, `inventory.locations.manage` as one Tier-3 action, same-module manifest,
  bin code pattern `{AISLE}-{RACK:00}-{LEVEL:00}-{POS:00}`, ASSUMPTION-LOC-01…12.
- [x] **Owner approval → `ready-for-dev`** (Ali, 2026-10-01).

## 19. Implementation Notes

**ASSUMPTION-LOC-xx** (narrowest reading, autonomous mode):

| ID | Assumption |
|---|---|
| LOC-01 | Identity = FU of MOD-0173 (Blueprint SoR "locations (as scoped)"); no new MOD. |
| LOC-02 | One `Location` aggregate for all five levels (type + parent rule), not five entities: the contract has one `LocationRef`, the tree is uniform, and WMS adds behaviour later by type. |
| LOC-03 | Code uniqueness covers ALL statuses incl. Archived (an archived code stays taken — history and old documents keep pointing at it unambiguously). Owner may relax to "non-archived only". |
| LOC-04 | `Type` is immutable; changing a level = archive + create (moves change the parent, not the level). |
| LOC-05 | `operatingMode` is stored on Site/Warehouse only; `LocationRef.operatingMode` of lower levels is null (the contract marks it Site/Warehouse). The EFFECTIVE mode of a bin = its warehouse's — available via `getLocationPath`. |
| LOC-06 | Frozen `listLocations?legalEntityId` is honoured only when it equals the scope company; otherwise 403. |
| LOC-07 | The ledger switches to the in-process adapter in every environment; Prism location mock stays only for the HTTP client's contract smoke. Existing dev/smoke ledger rows that reference Prism ids (`loc-A-12-3`, `wh-01` …) are test data, not migrated: those ids become unknown (postings refused, reads show raw ids). |
| LOC-08 | `listLocations` stays unpaged (frozen); the tree requests children per parent; L-2 uses `searchLocations`. A parent with > 500 children hands over to L-2 in the tree. |
| LOC-09 | `LOCATION_HAS_STOCK` = any balance row with OnHand ≠ 0 in any status on the node or below. Active reservations are SKU-level (MOD-0173 S2) and do not pin a location. |
| LOC-10 | Status change takes no reason field (K-11 records who/when/old→new); a reason can be added later as an additive field. |
| LOC-11 | Org unit = Platform `OrganizationUnit` (owner MOD-0288) of the same LE, Active and not archived; checked once on write (soft link — a later archive of the unit does not cascade). |
| LOC-12 | Seed for test/smoke: tests build trees per scenario through the real API; the runtime smoke creates a small tree for the selected company through the owner API (LOC1 probe). No hardcoded location ids in code, no fabricated user data. |

**Slice plan**

| Slice | Content | Gate |
|---|---|---|
| **LOC1** | Contract v1.1 (single-writer edit of `location.openapi.yaml`) · entity/repository/indexes · v1 reads + v1.1 owner ops · history · stock presence · in-process ledger adapter + `StockTouchSeq` · scope middleware on `/api/locations` · permissions · tests + sabotage proofs · runtime probe (API) | CT accept |
| **INT-2** | Gateway `/api/locations/**` → 5066 (integration-agent), header pass-through, E2E probe | CT accept |
| **LOC2** | L-1…L-4 (Slim + tree component), company selector reuse, 7 languages, manifest pages/actions + Nav key, FU01 S-1/S-2 `locationId` deep link, UAS-001, runtime E4 (real login) | CT accept → pack done |

**LOC1 implementation notes (WP-0173-LOC1, 2026-10-01 — agent PASS ≠ CT ACCEPTED).** Commits `2c48726b4` (contract
v1.1), `2e8ff898e` (service + tests), docs/evidence commit after it. Evidence:
`docs/records/audits/2026-10/mod-0173-fu02-loc1-evidence/`. API narrative: `docs/reference/architecture/api/mod-0173-location.md`.

| ID | Assumption / decision taken in LOC1 |
|---|---|
| LOC1-01 | The ledger route guards (`No_controller_route_can_put_patch_or_delete`, exact six `api/inventory` routes) scanned EVERY controller, so §15's binding PATCH could not exist. They now pin the ledger controller (unchanged assertions) and are joined by stricter guards: only `InventoryController` serves `api/inventory`; no controller has PUT or DELETE; `LocationsController` has exactly the 11 contract routes with read / manage permissions; history is only read or inserted; no source deletes a location or history row. |
| LOC1-02 | K-13 is enforced inside the posting transaction for EVERY posting, REVERSAL included (a reversal is not pre-validated against the master). |
| LOC1-03 | A location / warehouse id is the canonical lower-case UUID string the API returns (the exact string the ledger stores, so stock presence matches it). Any other spelling — the old Prism ids `loc-A-12-3`, upper case — is not a location (LOC-07). |
| LOC1-04 | Only a Bin or a StorageLocation may be stockable (K-08 "StorageLocation may be true" read as the only exception); others → 400. |
| LOC1-05 | A Site / Warehouse created without `operatingMode` gets `internal` (DEC-INV-19 default); below Warehouse it is absent. |
| LOC1-06 | Bulk idempotency lives on the created bins (key + fingerprint); a replay recomputes `skipped` as the requested codes the batch did not create. A dry run does not consume the key; a run that created nothing leaves no key. |
| LOC1-07 | `contractVersion` stays `"v1"` on every LOCATION body: 1.1.0 is additive to the v1 major. |
| LOC1-08 | Org unit: Platform 404 / 400 → 422 `ORG_UNIT_INVALID`; 401 / 403 / 5xx / timeout / out-of-shape → 503 `ORG_UNIT_DIRECTORY_UNAVAILABLE` (could not be confirmed ≠ invalid). Checked only when the value changes. |
| LOC1-09 | `Location.StructureSeq` (internal, not on the wire) is bumped on a parent when a child is created, moved under it or re-activated, so those writes conflict with a concurrent deactivation of the parent (K-05 / K-06 serialised like `StockTouchSeq`). |
| LOC1-10 | Ledger test scenarios write their location tree as real `inventory_locations` documents (`LocationSeed`, same BSON mapping, fresh UUIDs); the owner API is exercised by the 50 location tests and by the runtime probe. The one "location master unreachable" test keeps the pre-FU02 stub (`StubLocationMasterApiFactory`) — the in-process directory has no remote to switch off. |
| LOC1-11 | `searchLocations?orderBy=type` sorts by the type's wire name (Bin < Site < StorageLocation < Warehouse < Zone), not by level. |
| LOC1-12 | `getLocation` also returns `ETag` (a response header; the frozen v1 block is unchanged) so an edit can start from a v1 read. |
| LOC1-13 | Balances gained `ix_tenant_le_location` / `ix_tenant_le_warehouse` (stock presence); `validateLocations` takes at most 1000 ids. |

**Recommendations (not applied — those files are protected):**
1. LOCATION contract: `x-owner: Location Master (MOD-? candidate)` → `MOD-0173-FU02`; add the v1.1 operations of §15
   (LOC1). Consider documenting that `locationId` is an opaque UUID in production (examples use readable ids).
2. Parent pack §6/§20: point "locations" to this FU; note the ledger's location source becomes in-process (LOC1).
3. FU01 pack: S-1/S-2 accept `locationId` in the URL (LOC2 deep link); UI-2 pickers use `listLocations` (Active,
   stockable) instead of the BG-10 "unavailable" state once INT-2 lands — BG-10 closes for locations.
4. DCP-009 / domain-config: record DEC-INV-03 as resolved by MOD-0173-FU02; registry FU row (CT).

- **WP-0173-LOC1 CT-ACCEPTED (2026-10-02)** — commits 2c48726b4..97d169bed. CT re-measured: clean build 0/0;
  353/353 (0 skipped); repo architecture 18/18; LOCATION contract diff = only `info.version` + `x-owner` changed, all
  else additive, line endings unchanged; `inventory-bundle` untouched. Guard re-scope (ASSUMPTION-LOC1-01) accepted:
  ledger controller still GET/POST only, `/api/inventory` served by it alone, no PUT/DELETE anywhere, location
  controller pinned to the 11 contract routes. Own sabotage of the has-stock check on deactivate/archive → 2 red
  (AC-05 and the AC-16 race), restored → green, empty diff. Race measured by the agent: 30 rounds, both orders, no
  Inactive location holding stock.

## 20. Follow-up Items

- **FU-LOC1-01 (central):** `docs/analysis/contracts/README.md` should list LOCATION as 1.1.0, owner MOD-0173-FU02, served by InventoryService 5066 (v1 frozen, v1.1 additive).

- Cost-centre master / picker — when a finance master exists (today a validated text code).
- Location capacity, bin types, putaway/pick sequences, labels/barcodes — WMS (MOD-0178…0182).
- `OrganizationUnit.LocationCode` / `Position.LocationCode` free-text → optional link to a Location (report §14.3) —
  Platform owner decision.
- Import of an existing site/warehouse/bin list — the central import module (BL-441), not a page button.
- Reason field on status changes (LOC-10) if auditors ask for it.
- Bin code pattern variants (e.g. no aisle letter) — after owner feedback on K-12.
