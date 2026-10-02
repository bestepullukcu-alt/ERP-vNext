# MOD-0290 — what MVP-1 (Inventory) needs from the Product Master

**For:** the MDM developer / MDM control tower (owner of MOD-0290, `Diten.MdmService`)
**From:** MVP-1 Control Tower (MOD-0173 Inventory Ledger & Valuation, `Diten.InventoryService` 5066)
**Date:** 2026-10-02 · **Status:** request (owner decision 2026-10-02: MDM builds it; MVP-1 consumes only)
**Tracking IDs on the MVP-1 side:** FU-PM-01 (blocks real use and G1), FU-UI2-02, FU-UI2-03, BG-01, W-UI-01

> Placement: `docs/roadmap/` — it describes work that has not happened yet (docs-organization §1, question 3).
> Measurement method: read-only code inspection of `services/Diten.MdmService` on branch
> `feature/sce/mod-0173-inventory-ledger-valuation` (HEAD `60cae6d95`), every claim below has a file reference.

---

## 1. Why this matters

The inventory ledger (MOD-0173) is built and accepted. Its UI (stock list, movement history, new-movement form, correction,
location master) is built and accepted. **None of it can be used for real yet**, because every stock movement must be
validated against the product master and that validation has no real provider:

- `Diten.InventoryService` validates `itemId`/`skuId`/`skuLevel`, converts the movement unit to the base unit and enforces
  lot/serial control by calling **PRODUCT-MASTER-BUNDLE v1** (`docs/analysis/contracts/product-master-bundle.openapi.yaml`,
  `x-status: FROZEN`, owner MOD-0290). It is fail-closed: no answer → `503 PRODUCT_MASTER_UNAVAILABLE`, nothing written.
- The new-movement form reads unit options, lot/serial flags and labels through the Gateway at `/api/product-master/...`.
- Today both run against a **Prism mock** of that contract (port 4010) as a time-boxed development bridge.
- The G1 gate ("Product & Inventory Truth Ready", inventory report §20.1) cannot close until MOD-0290 serves the
  contract for real: canonical identity resolves, `Gsku→GlobalProduct` resolves, base UoM + conversion resolves, and
  the identity contract is versioned + frozen.

## 2. What exists in MDM today (Model A) — measured

| Contract need | MDM today | Evidence |
|---|---|---|
| `itemId` = GlobalProduct id, `canonicalCode`, `name`, `lifecycleStatus` | ✅ exists | `Domain/Entities/GlobalProduct.cs:5-13`, `EntityBase.cs:5` |
| `skuId`, `canonicalCode` for Gsku / Lsku / FinishedGood | ✅ exists | `Gsku.cs:9`, `Lsku.cs:9`, `FinishedGood.cs:8` |
| `packUomCode`, `packQuantity` (Gsku) · `marketCode` (Lsku) | ✅ exists | `Gsku.cs:13-16`, `Lsku.cs:12-13` |
| `skuLevel` (Gsku \| Lsku \| FinishedGood) | ⚠️ implicit (entity type / collection), no field | — |
| SKU → GlobalProduct (`itemId` on a SKU) | ⚠️ indirect: `Gsku.ProductDefinitionRevisionId` → `ProductDefinitionRevision.GlobalProductId` | `Gsku.cs:8`, `ProductDefinitionRevision.cs:7` |
| `baseUomId` | ❌ missing | — |
| UoM conversions (`fromUom`, `toUom`, `numerator`, `denominator`) | ❌ missing; no conversion entity; UoMs come from the Platform reference catalog as `ReferenceCatalogSelection` | `GskuCreateOptionsFacade.cs:26`, `ValueObjects/ReferenceCatalogSelection.cs:5-11` |
| `lotControlled`, `serialControlled` | ❌ missing | — |
| `shelfLifeDays`, `minRemainingShelfLifeDays`, `storageCondition`, `retestDays` | ❌ missing | — |
| `gtin` | ❌ missing and **actively rejected** by validators | `CreateFinishedGoodDraftValidator.cs:14`, `CreateFirstGskuDraftValidator.cs:15`, `CreateLskuDraftValidator.cs:16` |
| Routes `/api/product-master/**` (5 operations) | ❌ none in MDM, none in the Gateway | `Api/Controllers/*`, `gateway/Diten.ApiGateway/ocelot.json` |
| Tenant scoping | ✅ `TenantId` on every entity; no `LegalEntityId` (correct — product identity is tenant-level) | `EntityBase.cs:6` |

Existing Model A read routes (useful, but not the contract): `GET api/global-products[/selector|/{id}]`
(`mdm.global-products.read`), `GET api/gskus[/{id}]` (`mdm.gskus.read`), `GET api/lskus[/{id}]` (`mdm.lskus.read`),
`GET api/finished-goods[/{id}]` (`mdm.finished-goods.read`).

**Do not use / do not extend for this:** Model B — `Product` entity, `mdm_products` collection, `api/mdm/products`
(`ProductsController.cs:18`). Inventory consumes Model A only (DEC-INV-17).

## 3. What we need — requirements

Priority **P0** = blocks real use and G1. **P1** = needed before MVP-1 closes. **P2** = removes an MVP-1 waiver.

### P0 — serve the frozen contract (no contract change)

**R-01 · Five operations exactly as PRODUCT-MASTER-BUNDLE v1** (paths, parameters, schemas, status codes, error body
`{error:{code,message,correlationId}, contractVersion}`), base path `/api/product-master`:

| Operation | Path | Inventory uses it for |
|---|---|---|
| `getProduct` | `GET /products/{itemId}` | lot/serial flags, base UoM, labels, shelf-life data |
| `getSku` | `GET /skus/{skuId}` (+ `skuLevel`) | SKU exists, which product it belongs to |
| `resolveSkuProduct` | `GET /skus/{skuId}/product` | **D-SKU-LINK**: `sku.itemId == movement.itemId` check |
| `getSkuUom` | `GET /skus/{skuId}/uom` | movement unit → base unit (`BaseQuantity`) |
| `validateRefs` | `POST /validate` | bulk existence check (fail-closed consumers) |

- Model A only. `itemId` = GlobalProduct id; `skuId` + `skuLevel` → Gsku | Lsku | FinishedGood.
- Unknown id → `404` with the contract error body (inventory maps it to `UNKNOWN_ITEM` / `SKU_ITEM_MISMATCH`).
- Tenant-scoped from the caller's JWT (same rules as the existing MDM controllers); cross-tenant ids → 404.
- Read permissions: the inventory service forwards the **caller's** token. Please define the permission key(s)
  (PKS-001 format, `mdm.*` namespace) and tell us their names; tenant users who post stock will need them.
- Accept tokens up to **64 KB** of headers (a real seed token is ~34.6 KB with 695 permission claims).

**R-02 · Gateway route** `/api/product-master` and `/api/product-master/{everything}` → MDM 5059, GET + POST + OPTIONS
(integration-agent, same pattern as INT-1 / INT-2: no AuthenticationOptions, headers passed through).

**R-03 · Base UoM + conversions on Model A** (DEC-INV-09 "MOD-0290 inventory-facing gaps"; inventory report W1B).
`getSkuUom` must return `baseUomId` and directed conversions `{fromUom, toUom, numerator, denominator}` (decimal strings,
exact — inventory chains up to 4 steps and divides last, so no rounding). Unit codes from the Platform reference catalog.
Today: only `PackUomCode`/`PackQuantity` on Gsku — enough to derive one conversion (pack → base) **only if** a base UoM
exists.

**R-04 · Lot / serial control flags** on the product (`lotControlled`, `serialControlled`). Without them inventory cannot
enforce `LOT_REQUIRED` / `SERIAL_COUNT_MISMATCH` (GxP, DEC-INV-11).

**R-05 · `skuLevel` and SKU→product resolution**: return `skuLevel` explicitly; resolve every level to its GlobalProduct
(Gsku via ProductDefinitionRevision; Lsku via Gsku; FinishedGood via Gsku today).

### P1 — needed before MVP-1 closes

**R-06 · Shelf-life / storage / retest** (`shelfLifeDays`, `minRemainingShelfLifeDays`, `storageCondition`,
`retestDays`) — consumed by MOD-0174 (lot expiry) and MOD-0176 (FEFO). Nullable is fine until those modules start.

**R-07 · Development data**: the development MDM has 177 GlobalProducts but **0 Gsku and 0 Lsku**, and none of the products
belong to the dev-runbook seed user's tenant (FU-UI2-03). Please seed a small Model A set (a few products with Gsku/Lsku,
UoMs, one lot-controlled and one serial-controlled item) in the seed tenant, through MDM's own flows/seed files.

### P2 — removes an MVP-1 waiver

**R-08 · Product / SKU search** as an **additive v1.1** operation (e.g. `GET /products?q=&page=&pageSize=` and
`GET /skus?itemId=&q=`). The movement form's product picker currently uses MDM's non-contract Model A selector routes
under waiver **W-UI-01** (expires before G1 sign-off). A contract search retires that waiver.

## 4. Decisions MDM must take (we found contradictions)

| # | Topic | What we see | Question |
|---|---|---|---|
| D-1 | GTIN | Contract `SkuRef.gtin` (nullable); MDM validators reject GTIN; MOD-0290 pack defers it (BL-022) | Return `gtin: null` for now (acceptable to inventory) and keep BL-022 — confirm. |
| D-2 | FinishedGood parent | DEC-INV-17 assumes `GlobalProduct → Gsku → Lsku → FinishedGood`; MDM has `FinishedGood.GskuId` and forbids `LskuId` (`CreateFinishedGoodDraftValidator.cs:10`) | Which is canonical? Inventory only needs `resolveSkuProduct` to reach the GlobalProduct, so either works — but the record should be consistent. |
| D-3 | Stockable SKU level | Inventory accepts all three levels (default Lsku) | Confirm the per-product stockable level policy (open since DEC-INV-17, "W1A spec"). |
| D-4 | Where conversions live | No conversion entity today | New Model A entity vs. derived from pack data — MDM's design choice; inventory only needs `getSkuUom`. |

## 5. How we will verify (MVP-1 side, after MDM says "ready")

1. Point `Inventory__Upstream__ProductMasterBaseUrl` back to the Gateway (remove the Prism bridge) and run the
   InventoryService contract-validating tests + the runtime probe against the real MDM.
2. Every MDM response validates against `product-master-bundle.openapi.yaml` (the inventory test handler already does this
   for its own contract).
3. Golden flow through the UI with a real Model A product: opening balance 2 KOLI → BaseQuantity from real conversions;
   lot-controlled item without lot → `LOT_REQUIRED`; serial item count mismatch → `SERIAL_COUNT_MISMATCH`.
4. Retire the development bridge (§6) and W-UI-01 (if R-08 shipped); record G1 identity items as PASS.

## 6. Meanwhile — development bridge on the MVP-1 side (time-boxed)

Until R-01…R-05 are served, development uses the Prism mock of the frozen contract on port 4010:
InventoryService via `Inventory__Upstream__ProductMasterBaseUrl`, the Web via an equivalent development-only setting
(WP-0173-PM0). Mock answers are **static examples**, not real product data. The bridge is removed when MDM is ready (§5).

## 7. Contacts / references

- Contract: `docs/analysis/contracts/product-master-bundle.openapi.yaml` (FROZEN v1, owner MOD-0290)
- Consumer: `services/Diten.InventoryService` (README, `Infrastructure/Upstream/ProductMasterHttpClient.cs`)
- Decisions: `docs/analysis/inventory-capability-scope-and-dependency-report.md` §9 (DEC-INV-09, -11, -17), §20.1 (G1)
- MVP-1 packs: `execution/domains/supply-chain-execution/module-packs/MOD-0173-*.md` (FU-PM-01, W-UI-01)
