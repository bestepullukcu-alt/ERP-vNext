# MOD-0173-FU02 — LOCATION v1 / v1.1 API (as implemented, slice LOC1)

> **Authority:** the contract `location.openapi.yaml` (LOCATION 1.1.0, `x-owner: MOD-0173-FU02` — the four v1
> operations frozen and unchanged, seven owner operations added in 1.1.0) and the FU02 module pack
> `MOD-0173-FU02-location-storage-master.md` (`ready-for-dev`). This page explains how `Diten.InventoryService`
> (port 5066) serves that contract today. It does not redefine it — where they differ, the contract and the pack win.
>
> **Slice:** LOC1 — API + ledger link. The screens (L-1 tree, L-2 list, forms, bulk dialog) and the manifest pages are
> LOC2; the Gateway route `/api/locations/**` is INT-2 (not yet in `ocelot.json` — until then only 5066 serves it).

## 1. Surface

| Operation | Method + path | Permission | Success |
|---|---|---|---|
| `getLocation` (v1) | `GET /api/locations/{id}` | `inventory.locations.read` | 200 `LocationRef` + `ETag` |
| `listLocations` (v1) | `GET /api/locations?type&parentId&legalEntityId` | `inventory.locations.read` | 200 `{ items, total, contractVersion }` — every status, unpaged, ordered by code |
| `getLocationPath` (v1) | `GET /api/locations/{id}/path` | `inventory.locations.read` | 200 `{ locationId, path: [{ type, id, code }], contractVersion }` root Site → node |
| `validateLocations` (v1) | `POST /api/locations/validate` `{ locationIds }` | `inventory.locations.read` | 200 `{ results: [{ locationId, exists, isStockable? }], contractVersion }` (≤ 1000 ids) |
| `createLocation` | `POST /api/locations` | `inventory.locations.manage` | 201 `LocationDetail` + `ETag` · 200 idempotent replay |
| `updateLocation` | `PATCH /api/locations/{id}` (`If-Match`) | `inventory.locations.manage` | 200 `LocationDetail` + new `ETag` |
| `changeLocationStatus` | `POST /api/locations/{id}/status` (`If-Match`) `{ status }` | `inventory.locations.manage` | 200 `LocationDetail` + `ETag` |
| `bulkCreateBins` | `POST /api/locations/{id}/bins/bulk` | `inventory.locations.manage` | `dryRun` 200 `{ toCreate, skipped, count }` · real 201 `{ created, skipped, count }` · 200 replay |
| `searchLocations` | `GET /api/locations/search?page&pageSize&search&type*&status*&isStockable&parentId&orderBy&orderDir` | `inventory.locations.read` | 200 `{ items, total, page, pageSize, contractVersion }` |
| `getLocationHistory` | `GET /api/locations/{id}/history?page&pageSize` | `inventory.locations.read` | 200 `{ items: [LocationChange], total, page, pageSize }` newest first |
| `getLocationStockPresence` | `GET /api/locations/{id}/stock-presence` | `inventory.locations.read` | 200 `{ locationId, hasStock, subtreeHasStock }` — never a quantity |

There is **no PUT and no DELETE** (a location is archived, never removed). Exactly these eleven routes exist
(`LocationRouteArchitectureTests`).

- **Wire format** — unwrapped contract schemas, `contractVersion: "v1"` on every body (1.1 is additive to the v1 major).
  Null members are omitted. Ids are canonical lower-case UUID strings.
- **Errors** — the contract `Error` schema `{ error: { code, message, correlationId }, contractVersion }`.
- **Concurrency** — every single-location answer carries `ETag: W/"<version>"` (also `getLocation`, so a client can
  edit from a v1 read). PATCH and status need `If-Match`: missing → 400 `INVALID_REQUEST`; stale or malformed → 409
  `LOCATION_CONFLICT`. A ledger posting does **not** change the version.
- **Idempotency** — create and bulk take `Idempotency-Key` = body `idempotencyKey` (1–128): same body → 200 replay of the
  original result; other body → 400 `IDEMPOTENCY_KEY_REUSED`; concurrent requests with one key → one write. A dry run
  does not consume its key; a bulk run that created nothing leaves no key behind.

## 2. Scope (every endpoint)

`/api/locations` runs through the same MOD-0173 scope middleware as `/api/inventory` (tenant + user from the JWT, legal
entity from the claim or — claim-less — the MDM-verified `X-Legal-Entity-Id`, W-0173-02). Every query is Tenant + LE
filtered. A location of another company is **404 `UNKNOWN_LOCATION`** in every operation, except an explicit
cross-company parent in a write (create, move) → **403 `CROSS_LE_FORBIDDEN`**. A legal entity is never accepted from a
body. The frozen `listLocations?legalEntityId=` is honoured only when it equals the scope company (else 403); on every
other location route `legalEntityId` / `tenantId` in the query stay 400 `INVALID_REQUEST`.

## 3. Rules enforced (pack §12)

| Rule | Behaviour | Code |
|---|---|---|
| K-01 level | Site → none · Warehouse → Site · StorageLocation → Warehouse · Zone → StorageLocation · Bin → Zone or StorageLocation | 422 `LOCATION_LEVEL_INVALID` |
| K-02 code | 1–40 `[A-Za-z0-9._/-]`, unique per company case-insensitively across every status (unique index `ux_location_code`), always editable; the id never changes | 409 `LOCATION_CODE_TAKEN` |
| K-03 company | a Site takes the scope company, children inherit it | 403 / 404 |
| K-04 archived | Archived is terminal: edit, move, status, child, bulk → refused | 409 `LOCATION_ARCHIVED` |
| K-05 lifecycle | deactivate / archive with stock (OnHand ≠ 0, any status) on the node or below; with an Active child | 422 `LOCATION_HAS_STOCK` · `LOCATION_HAS_ACTIVE_CHILDREN` |
| K-06 activate | under a parent that is not Active (also create / move / bulk under it) | 422 `LOCATION_PARENT_INACTIVE` |
| K-07 move | with stock on the node or below; into its own subtree; against K-01. A valid move rewrites `AncestorIds` of the whole subtree in the same transaction | 422 `LOCATION_HAS_STOCK` · `LOCATION_MOVE_INTO_SUBTREE` · `LOCATION_LEVEL_INVALID` |
| K-08 stockable | default Bin = true, others false; only a Bin or a StorageLocation may be stockable; turning it off while the node holds stock | 400 · 422 `LOCATION_HAS_STOCK` |
| K-09 operating mode | `internal` (default) · `external-integrated`, Site / Warehouse only; locked while stock is held below | 400 · 422 `LOCATION_HAS_STOCK` |
| K-10 stock presence | read from the ledger's balance rows: `hasStock` = rows on the node; `subtreeHasStock` = rows on a descendant or (warehouse) with `WarehouseId` = node | — |
| K-11 history | one `Created` / `BulkCreated` row per new node, one `Updated` / `Moved` / `StatusChanged` row per changed field (who, when, old → new, correlation id, LE source), in the same transaction; an unchanged PATCH writes nothing | — |
| K-12 bulk bins | target Zone or StorageLocation, Active; codes `{AISLE}-{RACK:00}-{LEVEL:00}-{POS:00}` (aisle one letter or 1–3 digits keeping the typed width); 1…1000 per batch; existing codes (any status) skipped and listed; one transaction | 400 `LOCATION_BULK_LIMIT` · 422 |
| Org unit | `orgUnitRef` = an Active, non-archived Platform OrganizationUnit of the same company, checked through the Gateway (`Inventory:Upstream:OrgUnitDirectoryBaseUrl`) only when it changes; soft link | 422 `ORG_UNIT_INVALID` · 503 `ORG_UNIT_DIRECTORY_UNAVAILABLE` (nothing written) |

## 4. Ledger link (K-13)

- The ledger's `ILocationMasterClient` is the **in-process** `InventoryLocationDirectory` (no HTTP, no copy of the list):
  exists / Active / stockable / company / path-under-warehouse are answered from `inventory_locations` with the
  request's scope. `LocationMasterHttpClient` remains only for the LOCATION contract smoke against Prism.
- Inside **every** posting transaction (a REVERSAL included), each leg location must still match *Active + stockable +
  this company + under the leg's warehouse*, and its `StockTouchSeq` is incremented. Otherwise the posting is 422
  `UNKNOWN_LOCATION` (`details.locationId`) and nothing is written.
- Because a status change, a move, a stockable change and a posting all write the same location document, a posting
  racing a deactivation is a Mongo WriteConflict: one transaction re-runs on the other's committed state. Measured
  (AC-16, 30 rounds): either the posting wins and the deactivation answers 422 `LOCATION_HAS_STOCK`, or the
  deactivation wins and the posting answers 422 `UNKNOWN_LOCATION` — never stock on an Inactive location.
- A location id that is not the canonical lower-case UUID (e.g. the old Prism example ids `loc-A-12-3`, or an
  upper-case spelling) is not a location: postings to it are refused, reads answer 404 / `exists: false`.

## 5. Storage

`inventory_locations` (unique `ux_location_code`, unique partial `ux_location_create_key`, `ix_location_parent`,
`ix_location_ancestors`, `ix_location_bulk_key`) and `inventory_location_history` (`ix_location_history`, append-only),
both created by the startup index gate; balances gained `ix_tenant_le_location` / `ix_tenant_le_warehouse` for stock
presence. No location document carries a quantity.

## 6. Screens (slice LOC2)

The tenant screens never call 5066, the Gateway or `/api/locations` from the browser: they talk to same-origin MVC
proxies in `Diten.Web`, which call Gateway `/api/locations` (INT-2) with the cookie bearer, `X-Tenant-Id` and — for a
token without a `legal_entity_id` claim — the selected company as `X-Legal-Entity-Id` (W-0173-02; no call at all
without a company).

| Browser → MVC | MVC → Gateway (LOCATION) | Used by |
|---|---|---|
| `GET /LocationTree/children?parentId&includeArchived` | `searchLocations` (`parentId`, or `type=Site` for the root; Active+Inactive unless archived asked) | L-1 tree (lazy; > 500 children → L-2) |
| `GET /Locations/api?start&length&search&orderBy&orderDir&type*&status*&isStockable&parentId` | `searchLocations` + `getLocationPath` per distinct parent | L-2 list (server mode) |
| `GET /Locations/node/{id}` | `getLocation` (+ `ETag`), `getLocationPath`, `getLocationStockPresence` | L-1 detail, L-3 edit, status dialog |
| `GET /Locations/history/{id}?page` | `getLocationHistory` (10 per page) | L-1 history |
| `GET /Locations/parents?q&type*&activeOnly` | `searchLocations` | form parent picker (Active only), L-2 parent filter |
| `GET /Locations/orgunits` | Platform `GET /api/platform/organization-units` (same company, Active, not archived) | form org-unit picker |
| `POST /Locations/create` (anti-forgery) | `createLocation` (`Idempotency-Key` = the form's key) | L-3 create / add child |
| `POST /Locations/edit/{id}` (anti-forgery) | `updateLocation` PATCH with `If-Match`, changed members only | L-3 edit / move |
| `POST /Locations/status/{id}` (anti-forgery) | `changeLocationStatus` with `If-Match` | Activate / Deactivate / Archive |
| `POST /Locations/bins/{id}` (anti-forgery) | `bulkCreateBins` (dry run, then one run with the same key) | L-4 bulk bins |

Refusals keep the service's status and code; the message is the reader's language (`LocationErrorMessages` →
`LocationsIndex` resx, 7 languages) with the correlation id behind a "details" disclosure. "View in stock list" opens
FU01 S-1 as `/StockLedger?locationId={id}` (exact location filter of the frozen `/balance`).
