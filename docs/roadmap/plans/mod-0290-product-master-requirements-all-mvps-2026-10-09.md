# MOD-0290 — what every MVP needs from the Product Master (MVP-1 … MVP-6)

**For:** the MDM developer / MDM control tower (owner of MOD-0290, `Diten.MdmService`)
**From:** MVP-1 Control Tower, on behalf of the supply-chain MVP teams (MVP-1 … MVP-6)
**Date:** 2026-10-09 · **Status:** request
**Companion:** `mod-0290-product-master-bundle-requirements-for-mvp1-2026-10-02.md` — the detailed MVP-1 list (R-01 … R-12)
and the owner decisions on PRODUCT-MASTER v1.1 (§8, OD-1 … OD-7). This document adds what MVP-2 … MVP-6 need and the
requirements that cut across all of them.

> Placement: `docs/roadmap/` — it describes work that has not happened yet (docs-organization §1, question 3).
> Method (2026-10-09, read-only): module packs, frozen contracts (`x-consumes`, line schemas), MVP briefs
> (`docs/analysis/workpackages/WP-MVP*.md`), the inventory report, and the code on the current branch and on
> `origin/feature/mvp3-bom-routings`, `origin/feature/sce/mod-0188-demand-planning`, `origin/feature/mvp6-logistics`.
> **Evidence level:** every row is marked **E** (a pack line, contract field or code line asks for it) or **I** (inferred —
> the reason is given; confirm when that module's pack is written). No pack exists yet for MOD-0176 … 0182, 0189, 0191; the
> blueprint rows for them name objects only, no product attributes.

---

## 1. Who consumes the product master

| MVP | Modules | Consumes PRODUCT-MASTER | Today |
|---|---|---|---|
| MVP-1 Foundation | 0173 Ledger (+ UI), 0174 Lot/Serial, 0175 Quarantine, 0176 Expiry/FEFO, 0177 Recall | yes — lot/serial flags, UoM, shelf life, GTIN | Prism mock of v1 (companion doc) |
| MVP-2 Procurement | 0141 Requisition & PO, 0142 GRN, 0143 Invoice match, 0145 Sourcing (0140, 0144: no) | yes — identity, SKU at GRN, UoM | `PermissiveProductReferenceValidator` accepts every id; item ids only |
| MVP-3 BOM | 0193 BOM & Routings | yes — parent / component identity, UoM, material type | HTTP `validate` with `{itemId}` only, default mode Permissive |
| MVP-4 Planning | 0188 Demand, 0189 MRP, 0191 Safety stock | yes — SKU identity, base UoM, dated conversions | port `IProductMasterV1Reader` not wired (fails closed) |
| MVP-5 Warehouse | 0178 Putaway/Pick/Pack, 0180 Waves, 0181 Cycle count, 0182 Barcode/RFID | yes (`warehouse-outbound` `x-consumes`) — SKU, pack UoM, lot/serial at pick, GTIN | not built |
| MVP-6 Integrated + Logistics | 0183–0187, 0190, 0192, 0147, 0148 | ids only (opaque) | no product-master call |

## 2. Cross-cutting requirements (all MVPs)

| # | Requirement | Needed by | Ev. | In v1? |
|---|---|---|---|---|
| X-01 | **One lifecycle list**, its wire mapping, and the movement / use rules (companion R-10, OD-3, OD-4). Lifecycle visible **per SKU** and returned by `validateRefs`, so bulk callers can refuse Deactivated / Retired items without a `getProduct` per line. | 0173 (OD-3/4); 0141 / 0142 / 0193 (no order / BOM line for a closed item — I, from fail-closed design); 0188 (wrong SKU identity invalidates history, pack `MOD-0188…md:137` — I) | E / I | partial: product only, enum `Draft/Active/Archived` ≠ MDM `Draft/PendingIdentityApproval/IdentityApproved/Retired` (`MOD-0290…md:222`) |
| X-02 | **Unknown is not false.** Every stock-behaviour field nullable on the wire; lot / serial flags required before activation (companion R-04). | all consumers that enforce lot / serial (0173, 0142, 0178) | E | no (`lotControlled` / `serialControlled` are non-nullable booleans) |
| X-03 | **Product / SKU search** (code, name, paged) for pickers. | 0173 UI (waiver W-UI-01), 0193 (F-0193-02, pack `:264,288` — users type uuids today), 0141 / 0145 forms (I: they also take raw item uuids, 0141 pack `:78`) | E / I | no (companion R-08) |
| X-04 | **UoM check without a SKU**: an item-keyed UoM operation (e.g. `GET /products/{itemId}/uom`) or a `uomId` in `validateRefs`. | 0145 RFx lines carry `itemId` + `uomId`, no `skuId` (pack `:48,78,87,169`); 0193 BOM component UoM (ASSUMPTION-BOM-03, F-0193-03, pack `:278,289`) | E | no (`getSkuUom` is keyed by `skuId`) |
| X-05 | **List the SKUs of an item** (e.g. `GET /products/{itemId}/skus`). | 0189 MRP netting: DEMAND and BOM are keyed by `itemId`, inventory by `skuId` (I); 0177 recall trace-forward (I) | I | no (only SKU → item) |
| X-06 | **`materialType` and `itemKind`** as contract enums (companion R-09). | 0193 (pack `:43` says 0290 owns it, consumed not copied); 0189 make / buy (I); domain-config `:40`; report `:872-887` | E / I | no |
| X-07 | **Events** `ProductLifecycleChanged`, `SkuPublished` (wave0 contract plan). | 0188 / 0189 invalidation (I); every cache-free consumer re-reads on change | E (`wave0-contracts-product-master-and-inventory.md:56-57`) | no (the bundle defines no events) |
| X-08 | **Same `level` handling** on `getSku`, `getSkuUom`, `validateRefs`; state what happens when a caller has no `skuLevel` (shipment and PO lines do not carry one). | 0141 (`requisition-po` line has `skuId`, no level), 0183 (`ShipmentLine` has no level) | E | no (`getSku` needs `level`, `getSkuUom` has none) |

## 3. Requirements per MVP (beyond the cross-cutting ones)

### MVP-1 — see the companion document (R-01 … R-12, OD-1 … OD-7)
MOD-0176 / 0177 (no pack yet): shelf-life policy is owned by the product master (report `:146`) → `shelfLifeDays`,
`retestDays`, `minRemainingShelfLifeDays` on the SKU (companion R-06, E); recall per market uses `gtin` + `marketCode` (I).

### MVP-2 — Procurement

| # | Need | Used by (evidence) | Fields | SKU level | Req / Opt | In v1? |
|---|---|---|---|---|---|---|
| P-01 | Item identity, fail-closed | 0141 (pack `:46,66,129,139`, `requisition-po:16,182`), 0143 (pack `:50,82,115,123`, `invoice-match:17,165`), 0145 (pack `:20,48,78,116`, `sourcing:16,156,183`) | `itemId` | GlobalProduct | Req | yes (`validateRefs`) |
| P-02 | SKU + level at goods receipt, and SKU belongs to the item | 0142 (pack `:50-52,114-116,123`; `grn-event:11,60-68`) | `itemId`, `skuId`, `skuLevel` | Gsku / Lsku / FinishedGood | Req | yes (`validateRefs`, `resolveSkuProduct`) |
| P-03 | Lot / serial flags at goods receipt | `grn-event:69-70`, 0142 pack `:53-54` (I: GRN must know whether lot / serial is required instead of relying on the inventory refusal) | `lotControlled`, `serialControlled` on `getSku` | per SKU | Req (I) | yes on the product (moves to SKU, companion OD-1) |
| P-04 | UoM of RFx lines checked against the product master | 0145 (pack `:48,78,87,169`) | `uomId` | item | Req | no → X-04 |
| — | Not product-master data | lead time is supplier-quoted on the bid (`sourcing:109,184-185`); PO / requisition UoM comes from Reference Data MOD-0048 (0141 pack `:102,131`); 0140 and 0144 consume no item data | — | — | — | — |

### MVP-3 — BOM & Routings (MOD-0193)

| # | Need | Used by (evidence) | Fields | Req / Opt | In v1? |
|---|---|---|---|---|---|
| B-01 | Parent and component identity, fail-closed | pack `:29-30,69,76,124,179,181`; `bom:10,76,91` | `itemId`, `componentItemId` (GlobalProduct) | Req | yes |
| B-02 | Alternate components | pack `:80,185` | `alternates[]` | Opt | yes (if validated) |
| B-03 | Component UoM and base UoM / conversion for explosion | pack `:78,231,278,289` | item-keyed UoM | Opt now, Req when F-0193-03 lands | no → X-04 |
| B-04 | Material type | pack `:43` | `materialType` | Opt | no → X-06 |
| B-05 | Closed items not usable in a new BOM revision | I (fail-closed design; companion OD-3/4) | lifecycle | Req (I) | partial → X-01 |
| — | Not evidenced: scrap %, phantom / kit flags, routing item, product revision / effectivity (the BOM keeps its own revision, change control MOD-0209) | — | — | — | — |

### MVP-4 — Planning

| # | Need | Used by (evidence) | Fields | Req / Opt | In v1? |
|---|---|---|---|---|---|
| L-01 | SKU identity and its item | 0188 (pack `:55`; `CreateDemandHistoryImportBatchHandler.cs:174-176`) | `skuId`, `skuLevel`, `itemId` | Req | yes |
| L-02 | Base UoM of a SKU, server-verified | 0188 port `IProductMasterV1Reader.GetBaseUomIdAsync` (`…/IProductMasterV1Reader.cs:4-7`); `demand-v2:681,787`; pack AC-D07 `:201`, `:108` | `baseUomId` via `getSkuUom` (never `getProduct`) | Req | yes |
| L-03 | **Conversion as of a date, with its own id and version**, for a given level; an error for a missing / ambiguous conversion | 0188 `IHistoryRowReferenceChecker` (`HistoryImportContracts.cs:26-40`), handler `:231-238`, pack `:76` | factor (num / den), `conversionId`, `conversionVersion`, `effectiveFrom/To`; inputs `level`, `asOf`, `fromUom` | Req | **partial**: num / den + effective dates exist; no id / version, no `asOf` / `level` parameter, no error code |
| L-04 | Item → SKUs for MRP netting | I (DEMAND / BOM keyed by item, inventory by SKU) | list | Req (I) | no → X-05 |
| L-05 | Product family for S&OP | name = "internal / global product-family name" (`MOD-0290…md:190,230`) | `name` | Opt | yes |
| L-06 | ABC class | report `:160` (`ProductClassification(ABC)`, P2) | `abcClass` | Opt (P2) | no |
| — | Not product-master data (evidence points elsewhere): purchase lead time (SOURCING bid line); safety-stock policy (owned by 0191); **SKU × warehouse eligibility** (0188 pack `:77`) — owner not decided, do not build in MDM until decided | — | — | — | — |

### MVP-5 — Warehouse (no packs yet)

| # | Need | Used by (evidence) | Fields | Req / Opt | In v1? |
|---|---|---|---|---|---|
| W-01 | SKU + item + level on each outbound line | `warehouse-outbound:17,165-176` | `itemId`, `skuId`, `skuLevel` | Req | yes |
| W-02 | Pack UoM and conversion (BOX, PALLET …) | `warehouse-outbound:127`; report `:481-482` (Gsku owns conversions) | `packUomCode`, `packQuantity`, conversions | Req | yes |
| W-03 | Lot / serial capture at pick | `warehouse-outbound:173-174`; serial count = quantity (report §4 R6) | flags on `getSku` | Req | yes on product (→ SKU) |
| W-04 | **Look up a SKU by GTIN / barcode** for scanning | 0182 "scan-driven traceability" (blueprint r183); TRACE `checkGs1Code` (`trace.openapi.yaml:217`); report `:160,488` (`ProductIdentifier`) | `GET /skus?gtin=` | Req (I for the lookup) | no (one `gtin` per SKU, no lookup) |
| W-05 | Packaging hierarchy (case / pallet, handling units) with GTIN per level | brief `:6` (HandlingUnit); report `:523` | per-level UoM / GTIN | Opt | partial (conversions only) — **MDM defers it** (BL-017, `MOD-0290…md:114`) and GTIN lifecycle (BL-022, `:119`) → decision D-A |
| W-06 | Storage condition for putaway, as a controlled list | `ProductRef.storageCondition` (free string); location `sl-cold` example (`location.openapi.yaml:92`); quality will confirm the list (companion §8) | `storageCondition` code from a list | Opt (I for matching) | yes, as free text |
| — | Not evidenced: package weight / dimensions (the outbound `Package` has `weightKg` / `dimensionsCm`, `:177-183`, but nothing says they come from the product master — they may be measured at pack), hazardous goods, HS code | — | — | — | — |

### MVP-6 — Integrated + Logistics
No product-master attribute is needed: shipment lines carry `itemId` / `skuId` as opaque ids (`shipment-bundle:3018-3027`,
0183 pack `:57,71,154`); S&OP / capacity read DEMAND by item; claims and supplier modules carry no item data. Only X-01
(closed items on returns, I) and X-08 (lines without `skuLevel`) apply. 0186 returns: UoM equality vs conversion is still
open (D186-01, pack `:296`).

## 4. Decisions MDM must take (in addition to the companion's D-1 … D-4)

| # | Topic | Question |
|---|---|---|
| D-A | Packaging hierarchy (BL-017) and GTIN lifecycle (BL-022) are deferred in the MOD-0290 pack, but MVP-5 scanning (W-04, W-05) and recall (0177) need GTIN per packaging level | Which MVP brings them back? Proposal: before MVP-5 starts. |
| D-B | `storageCondition` is free text | Make it a code list (the list you will send to quality), keep the text as a label. |
| D-C | Conversions for planning (L-03) need an id, a version and as-of reading | Is a conversion an entity with versions (fits D-4 "where conversions live")? |
| D-D | SKU × warehouse eligibility (0188 pack `:77`) | Not assigned to MDM — confirm it stays out (LOCATION / planning owner to decide). |

## 5. Priority and timing

| Priority | Requirements | Why |
|---|---|---|
| P0 — MVP-1 closure (G1) | companion R-01 … R-07, R-09 … R-11, X-01, X-02, X-08 | inventory cannot be used for real without them |
| P1 — before MVP-2 / MVP-3 go live | X-03, X-04, X-06, P-02, P-03, B-03 | procurement and BOM validators are permissive today |
| P2 — before MVP-4 / MVP-5 start | L-03, X-05, X-07, W-04, W-05 (D-A), W-06 (D-B), L-06 | planning and warehouse are not built yet |
| — | MVP-6 | nothing beyond X-01 / X-08 |

## 6. Directions for the consumer teams (not MDM — the MVP-1 CT forwards them)

| Team | Fix on their side |
|---|---|
| MVP-2 Procurement | replace `PermissiveProductReferenceValidator` with a fail-closed HTTP validator; validate `skuId` + `skuLevel` on GRN and PO lines (not only `itemId`); read lot / serial flags from `getSku` at GRN and refuse "unknown"; no PO / receipt for a Deactivated / Retired item (companion OD-3 / OD-4) |
| MVP-3 BOM | send `skuId` / UoM when X-04 lands (today `validate` sends `{itemId}` only); switch the default mode from Permissive to fail-closed before go-live; refuse closed components in a new revision |
| MVP-4 Planning | read the base UoM from `getSkuUom`, never `getProduct`; use L-03 for dated conversions; no replenishment for a Deactivated item |
| MVP-5 Warehouse | add PRODUCT-MASTER to the brief's consumed list (`warehouse-outbound` already lists it); storage condition and FEFO data from `getSku` |
| MVP-6 Logistics | carry `skuLevel` on shipment / return lines or accept X-08's rule; decide D186-01 (UoM equality vs conversion) |

## 7. Not requested (no evidence) — do not build now

Weight, dimensions, volume, hazardous goods (UN number / class), HS / customs code, country of origin, MOQ / order multiple,
lot-sizing rule, item lead time, purchasable / producible flags, supplier-item links, CoA / inspection flags, scrap %, phantom /
kit flags. If a later pack asks for one, it is added here with its evidence.

## 8. MDM answer (2026-10-09, contract draft `1.1.0-draft.6`)

| Item | MDM answer | Our position |
|---|---|---|
| X-01 | `validateRefs` returns `lifecycleStatus` per row (v1.1) | accepted |
| X-02 | every stock field nullable; lot / serial required before activation (v1.1) | accepted |
| X-08 | `level` optional on `getSku`, `getSkuUom`, `resolveSkuProduct`, `validateRefs`; resolved from the id and returned; a given level must match (404 / `exists:false`); level-less `skuId` rows accepted (v1.1) | accepted |
| X-03 | `/stockable-items` already searches SKUs and items; GlobalProduct search `GET /products?q=` in v1.2 | ask: is `/stockable-items` in the contract? (not usable otherwise) |
| X-04 | a material item is its own SKU → `getSkuUom(skuId = itemId)` answers today; GlobalProduct has no UoM (GSKU level); `uomId` in `validateRefs` in v1.2 | accepted; implies a new sku level `Item` → confirm (Q-1) |
| X-05 | SKUs of an item in v1.2 | accepted |
| X-07 | events in a separate package (no event publishing in MDM yet) | accepted |
| L-03 / D-C | conversions are append-only on an active record → no version; stable `conversionId` + `asOf` in v1.2 | accepted; write the append-only rule into the contract; MVP-4 (0188 `ConversionContractVersion`) uses `conversionId` + `asOf` instead |
| W-04 / W-05 / D-A | with MDM packages P6 (barcode) and P9 (packaging hierarchy), before MVP-5 | accepted |
| D-B | storage condition is already a code list (`StorageConditionCode`, multiple `storageConditions`); the v1.0 text field carries the code | accepted |
| D-D | SKU × warehouse eligibility not in MDM | agreed |
| L-06 | ABC is computed from consumption → planning / inventory side, not MDM | agreed; request withdrawn |
| §7 | nothing added without evidence | agreed |
