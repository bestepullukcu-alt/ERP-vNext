# MVP-1 ↔ MVP-6 integration — touchpoints and work items

**From:** MVP-1 Control Tower · **Date:** 2026-10-12 · **Status:** plan (owner decisions recorded)
**Measured on:** `origin/main` after PR #134 (`feature/mvp6-logistics`), read-only, against the MVP-1 branch (INVENTORY 1.2.1, TRACE 1.3.0,
QUARANTINE 1.2.0, ESIGN 1.0.1, PRODUCT-MASTER 1.1.0-draft.11).

> Placement: `docs/roadmap/` — work that has not happened yet (docs-organization §1, question 3).

## 1. Owner decision

| # | Decision |
|---|---|
| OD-P1 (2026-10-12) | **Port collision 5066:** MVP-6 `Diten.SupplyChainService` and MVP-1 `Diten.InventoryService` both use 5066. InventoryService moves to **5067**; SupplyChainService keeps 5066. Done inside WP-MVP1-SYNC1 (v1.1 addendum); the CT updates `AGENTS.md` / `CLAUDE.md` port tables and tells MDM (R-12 direct call → 5067). |

## 2. Touchpoints

| # | Touchpoint | MVP-6 (origin/main) | MVP-1 | Status |
|---|---|---|---|---|
| 1 | INVENTORY consumption | `shipment-bundle.openapi.yaml` 3.2.0 `x-consumes: [INVENTORY, …]`, packs say "INVENTORY v1" | 1.2.1 (additive over 1.0.0) | compatible (read-only, 1.x) |
| 2 | TRACE / QUARANTINE / ESIGN / PRODUCT-MASTER / LOCATION | not consumed; product / location ids opaque | — | no coupling |
| 3 | `SkuLevel` | `FrozenReadSchema.cs:11` = Gsku / Lsku / FinishedGood; a row with `Item` is refused (`ReadOnlySourceClients.cs:26,31,55`); WAREHOUSE-OUTBOUND 1.0.0 (MOD-0178) the same | + `Item` | latent mismatch |
| 4 | Availability / balance client | `InventoryReadClient` calls `/api/inventory/availability` and `/balance`; not registered in DI | same paths and parameters | compatible, unwired; `legalEntityId` must be a GUID; paging not sent |
| 5 | Goods issue / reservation at shipment | no POST to movements / reservations; `ShipmentLine.inventoryReferenceId` opaque text | we own reservations and GOODS_ISSUE_SALES | needs an integration WP |
| 6 | Customer returns (MOD-0186) | "Received" is a manual state, no stock posting; `dispositionCode` free text; warehouse receipt / inventory posting = consumed seam | CUSTOMER_RETURN movement, MOD-0175 quarantine decisions | needs an integration WP |
| 7 | Claims (MOD-0187) | no stock writes | DAMAGED / WRITE_OFF | later |
| 8 | Workflow / e-signature | none in SupplyChainService | MOD-0023 / MOD-0022 via MOD-0175 | no coupling |
| 9 | Gateway / ports | `/api/shipment-bundle` → 5066 | our routes → 5066 | **collision → OD-P1** |
| 11 | Evidence hash pins | MVP-6 evidence pins inventory-bundle / trace / location / product-master 1.0.0 hashes (`verify_evidence.py`, not in CI) | our newer versions change the bytes | evidence only — correction note |

## 3. Work items

| Priority | Item |
|---|---|
| P0 | Port 5066 → Inventory 5067 (OD-P1) — in WP-MVP1-SYNC1 v1.1. |
| P1 | **Return receipt into quarantine:** CUSTOMER_RETURN posts to QUARANTINE / QUALITY_INSPECTION, MOD-0175 decision (Workflow + e-signature) to AVAILABLE / SCRAP / BLOCKED; map MOD-0186 `dispositionCode`; write the movement id back to `inventoryTransactionReferenceId`. Closes MOD-0186 D186-03 / D186-04. |
| P1 | **Shipment → goods issue and reservation release:** decide who posts GOODS_ISSUE_SALES at Dispatched (`sourceModule=MOD-0183`, `sourceDocumentId=shipmentId`) and how `ShipmentLine.inventoryReferenceId` maps to our reservation / transaction id. |
| P2 | MVP-6 `FrozenReadSchema.SkuLevels` + `Item` (MVP-6 owner); WAREHOUSE-OUTBOUND + `Item` (MOD-0178 owner) — or a written rule that Item-level stock is never shipped. |
| P2 | Wire `InventoryReadClient` (DI + base URL); `legalEntityId` returned as GUID; paging. |
| P3 | Correction note for the MVP-6 evidence hash pins of our contracts. |
| P3 | Claims → DAMAGED / WRITE_OFF bridge after MOD-0187 D187-04. |

## 4. Input for the MOD-0176 FEFO / MOD-0177 Recall mockups

- Recall cannot trace shipments by lot / serial through SHIPMENT-BUNDLE: `ShipmentLine` has no `lotId`, `serialIds` or `skuLevel`, and `GET /shipments`
  filters by status / source document only. Recall traces through our ledger (GOODS_ISSUE movements with `sourceDocumentId = shipmentId` and lot / serials),
  then reads shipment and customer data per shipment (`GET /shipments/{id}`). Option: ask for an additive SHIPMENT-BUNDLE change (lot / serials on the line,
  query by item or lot). The mockup shows shipment number, status and delivered-at.
- `ShipmentDelivered` / `PodCaptured` events exist — a recall can mark affected deliveries. A recall return is a MOD-0186 return (reason RECALL) that follows
  the P1 return-into-quarantine item.
- FEFO picking is not MVP-6: allocation / picking belong to WAREHOUSE-OUTBOUND (MOD-0178, MVP-5). MOD-0176 FEFO feeds MOD-0178 allocation through our
  `/availability` (excludes Blocked, Expired, retest-due lots) and reservations by lot.
