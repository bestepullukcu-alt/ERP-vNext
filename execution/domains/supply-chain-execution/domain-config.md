# Supply Chain Execution — Domain Config

> Bu dosya Supply Chain Execution domain'ine özgü ownership ve boundary kararlarını kaydeder. Mühendislik
> standartları `.antigravity/rules/` içindedir ve tekrar edilmez, referans verilir. Yetki: Module Pack > bu dosya.

## Purpose

Supply Chain Execution (SCE), ERP-vNext'in **stok gerçeği, izlenebilirlik, kalite-statü, depo yürütme, planlama
ve lojistik** yürütme sistem-of-record'larını sahiplenir. Blueprint 8.1 "Supply Chain Execution Suite" +
"Planning" + "Manufacturing Execution" bölümlerinin execution katmanıdır.

**Planlama tabanı:** [inventory-capability-scope-and-dependency-report.md](../../../docs/analysis/inventory-capability-scope-and-dependency-report.md)
(v2.1 decision-complete, 19 DEC-INV kararı) · **Frozen contract'lar:** [docs/analysis/contracts/](../../../docs/analysis/contracts/)

## In-Scope Reserved Modules (boundary-authorized 2026-09-11)

Blueprint-canonical SoR modülleri (DCP-002 gate PASS; registry'ye DCP-009 ile eklendi):

**Inventory & Traceability**
- `MOD-0173 Inventory Ledger & Valuation` — **authoritative stok balance/valuation SoR** (append-only transaction; balance=projection)
- `MOD-0174 Lot/Batch/Serial Tracking` — traceability/genealogy
- `MOD-0175 Quarantine / Blocked Stock` — kalite statüsü (balance tutmaz; 0173 StockStatus dimension'ını değiştirir)
- `MOD-0176 Expiry / FEFO Management` — son-kullanma/FEFO politikası
- `MOD-0177 Recall Readiness` — geri çağırma/iz sürme (support)

**Warehouse (WMS)**
- `MOD-0178 Putaway/Picking/Packing` · `MOD-0180 Wave Planning` · `MOD-0181 Cycle Counting` · `MOD-0182 Barcode/RFID` · `MOD-0179 3PL Integration` (conditional)

**Planning · Manufacturing · Logistics** (sonraki wave'ler)
- `MOD-0188 Demand Planning` · `MOD-0189 MRP & Replenishment` · `MOD-0190 S&OP` · `MOD-0191 Safety Stock` · `MOD-0192 Capacity Planning`
- `MOD-0193 BOM & Routings` · `MOD-0194 Work Orders` · `MOD-0195 Batch Execution/eBR` · `MOD-0196 OEE` · `MOD-0197 Co-Manufacturer`
- `MOD-0183-0187 Transport/Logistics` (Shipment/Carrier/Load/Reverse/Claims)

> Her modül **kendi module pack'i** `approved`/`ready-for-dev` olmadan runtime kod yazmaz. Bu domain-config yalnız boundary/ownership tanımlar.

## Consumed Masters (bu domain SAHİPLENMEZ — reference only)

- `MOD-0290 Product/Item/SKU Master` → **MDM-owned** (item kimliği, UoM, materialType). SCE consume eder (`itemId/skuId`), lokal kopya açmaz.
- `MOD-0172 Allocation & ATP/CTP` → **Commercial-owned** (O2C). 0173 availability+reservation okur; ikinci balance açmaz.
- **Physical Location Master** (Site→Warehouse→StorageLocation→Zone→Bin) → **DEC-INV-03: Option B shared master** (yeni lightweight master; MOD-# registry collision-check pending). OrganizationUnit DEĞİL (K6).
- Backbone (consume): Canonical ID (MOD-0040/0288) · RBAC (MOD-0018) · Audit (MOD-0021) · Workflow (MOD-0023) · Reference Data (MOD-0048) · Evidence/Docs (MOD-0029) · Event Bus · Gateway.
- Feeder seam: `MOD-0142 GRN` (Procurement/Commercial) → 0173'e goods-receipt post eder.

## Domain-Level Owned Boundaries

- **Stok gerçeği tek SoR = MOD-0173.** Hiçbir modül (warehouse/procurement/ATP) ikinci stok balance açamaz; herkes INVENTORY contract'ından okur/post eder.
- **Single-writer shared seam'ler (K15):** `0173 movement/availability` · `0290 identity` (MDM) · `0188 demand` · Location · gateway route family · permission tanımları. Aynı seam'e iki writer YASAK.
- **Contract-first:** cross-module iletişim yalnız published contract (OpenAPI, `docs/analysis/contracts/`). DB/iç tip paylaşımı yok.

## Key Decisions (rapor §0.0 — RESOLVED)

- **Scope:** Inventory + Warehouse (DEC-INV-01) · **İş tipi:** Pharma/GxP + Manufacturing (DEC-INV-11).
- **Kimlik:** MOD-0173 `ItemId`→GlobalProduct, `SkuId`+`SkuLevel`→Gsku|Lsku|FinishedGood (DEC-INV-17); Model B'ye dokunma.
- **Scoping:** Tenant + Legal-Entity; `TenantId`/`LegalEntityId` server-resolved, payload'dan alınmaz; cross-LE fail-closed (DEC-INV-18).
- **ERP entegrasyonu:** vNext birincil SoR + per-LE sisteme-agnostik dış entegrasyon; data model `SourceSystem`/`ExternalRef`/`LegalEntity.InventoryOperatingMode` taşır (DEC-INV-19).
- **Transaction:** append-only + idempotent + REVERSAL (edit yok); balance=projection (DEC-INV-02/15) · MovementType canonical + Reason via MOD-0048 (DEC-INV-16) · negative-stock prohibited default (DEC-INV-12) · costing per-LE/product, fallback MOVING_AVERAGE (DEC-INV-06).
- **Raw material (hammadde):** ayrı modül değil — MOD-0290 item (`materialType`); composition/miktar = MOD-0193 BOM (rapor §23).

## Service & Deployment

- **Servis:** `Diten.SupplyChainService` (tek servis — inventory/warehouse/traceability feature'lar; kullanıcı onayı 2026-09-11).
- **Port:** **5061** (mikroservis bandı 5011-5060 doluydu; band 5061'e uzatıldı — AGENTS.md §3 güncellenir).
- **Persistence:** MongoDB (repo deseni) · **Gateway:** `/api/inventory` vb. route'lar yalnız `integration-agent` tarafından eklenir (protected ocelot.json).
- ⚠️ **Servis scaffold** yalnız ilgili module pack `ready-for-dev` + `@orchestrator /add-module` ile başlar (AGENTS.md §2). Bu domain-config scaffold'ı **yetkilendirir**, tetiklemez.

## Cross-references
DCP: [DCP-009-supply-chain-inventory](../../portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md) · Registry: `MOD-0173…0197` satırları · Contracts: `docs/analysis/contracts/*.openapi.yaml`
