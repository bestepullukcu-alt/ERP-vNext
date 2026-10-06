# Procurement — Domain Config

> Bu dosya Procurement (Procure-to-Pay) domain'ine özgü ownership ve boundary kararlarını kaydeder. Mühendislik
> standartları `.antigravity/rules/` içindedir ve tekrar edilmez, referans verilir. Yetki: Module Pack > bu dosya.

## Purpose

Procurement domain'i, ERP-vNext'in **tedarikçi yaşam döngüsü + satın alma yürütme** sistem-of-record'larını
sahiplenir: supplier onboarding, sourcing/RFx, requisition & purchase order, mal kabul (GRN), fatura 3-yönlü
eşleştirme ve sözleşme/clause yönetimi. Blueprint 8.1 **"Procurement Suite" / "Procure-to-Pay (P2P)"** capability
group'unun execution katmanıdır (Domain 4 — Enterprise Application Ecosystem).

**Planlama tabanı:** [inventory-capability-scope-and-dependency-report.md](../../../docs/analysis/inventory-capability-scope-and-dependency-report.md)
(§13.1/§14.6/§15.1/§21.2 Procurement lane) · **DCP:** [DCP-010-procurement-p2p](../../portfolio/delivery-capability-packs/DCP-010-procurement-p2p.md)
· **Frozen contract'lar:** [docs/analysis/contracts/](../../../docs/analysis/contracts/)

## In-Scope Reserved Modules (boundary-authorized 2026-09-16)

Blueprint-canonical SoR modülleri (DCP-002 gate PASS 2026-09-16; registry'ye DCP-010 ile eklendi). Kanonik adlar
Blueprint 8.1 `Blueprint_Data` "Module Name" sütunundan **birebir** alınmıştır:

- `MOD-0140 Supplier Onboarding (KYC/Sanctions/Docs)` — **supplier master + onboarding case SoR** (Blueprint W-1; P2P-BUNDLE). SoR: suppliers, onboarding cases, KYC outcomes, approvals.
- `MOD-0141 Requisition & Purchase Orders` — **requisition/PO SoR** (Blueprint W-1; PO-BUNDLE). SoR: requisitions, purchase orders, approval trail, PO lifecycle events.
- `MOD-0142 Receiving (GRN)` — **mal kabul SoR** (Blueprint W-2; GRN-BUNDLE). SoR: goods receipts, receiving exceptions, match artifacts. Envanteri MOD-0173'e post eder (aşağıya bkz).
- `MOD-0143 Invoice Capture & 3-Way Match` — **3-yönlü eşleştirme SoR** (Blueprint W-2; MATCH-BUNDLE). SoR: match outcomes, invoice exceptions, approvals (invoice processing view). AP/payment SoR DEĞİL.
- `MOD-0144 Contracting & Clause Library` — **procurement contract/clause SoR** (Blueprint W-2; CLM-CONTRACT-BUNDLE). SoR: sourcing/procurement contracts, clause deviations (reference).
- `MOD-0145 Sourcing (RFQ/RFP)` — **RFx/bid/award SoR** (Blueprint W-3; SOURCING-BUNDLE). SoR: RFx events, bids, evaluations, award decisions.

> Her modül **kendi module pack'i** `approved`/`ready-for-dev` olmadan ve DCP-010 `approved`/`ready-for-execution`
> olmadan runtime kod yazılmaz (CAP-001 §7). Bu domain-config yalnız boundary/ownership tanımlar; scaffold yetkilendirir,
> tetiklemez.

## Explicitly NOT in this domain (out-of-scope)

- `MOD-0146 Payment Run Support` — **Not SoR** (Blueprint: "orchestrates pay-run support; References SoR: AP/Payments"). Ödeme/AP SoR = Finance/Treasury. **Bu domain kapsamı dışı** (roster'da yok).
- `MOD-0147 Supplier Performance & Risk` ve `MOD-0148 Supplier Portal` — Blueprint Procurement Suite üyesi olsalar da **MVP-6 lane'ine** aittir (rapor §13.5/§19/§21.2; DCP-009 §18 OD-4). Bu domain onları SAHİPLENMEZ. MOD-0140 SUPPLIER contract'ı onların tükettiği yüzeyi **superset** olarak korur (aşağıya bkz).

## Consumed Masters (bu domain SAHİPLENMEZ — reference only)

- `MOD-0290 Product / Item / SKU Master` → **MDM-owned** (item/SKU kimliği, UoM, materialType). Procurement consume eder (`itemId/skuId`), lokal ürün kimliği **açmaz/uydurmaz** (rapor §3, §21.2 MUST-NOT). Contract: `product-master-bundle.openapi.yaml` (frozen).
- `MOD-0173 Inventory Ledger & Valuation` → **Supply-Chain-Execution-owned** (authoritative stok balance/valuation SoR). Procurement **ikinci stok balance açmaz**; mal kabul yalnız frozen INVENTORY contract'ının `POST /movements` (`GOODS_RECEIPT_PO`) çağrısıyla post edilir. Contract: `inventory-bundle.openapi.yaml` (frozen). **İade** = `SUPPLIER_RETURN` movement.
- **Physical Location Master** (Site→Warehouse→StorageLocation→Zone→Bin) → **DEC-INV-03 Option B shared master** (SCE domain-config; MOD-# reservation pending). Procurement (GRN) `warehouseId/locationId` consume eder. Contract: `location.openapi.yaml` (frozen).
- Backbone (consume): RBAC (MOD-0018) · Audit (MOD-0021) · Workflow/Approvals (MOD-0023) · Reference Data (MOD-0048) · Evidence/Docs (MOD-0029/0031) · Notification (MOD-0027) · Event Bus (MOD-0035) · Gateway (MOD-0032).
- Downstream consumer seam: `MOD-0142 GRN` → `MOD-0173` (goods-receipt post) · `MOD-0143 InvoiceMatch` ← `GoodsReceived` event (0142) · `MOD-0141 PO` ← `MOD-0189 MRP` (requirement önerisi; PO SoR burada kalır).

## Domain-Level Owned Boundaries

- **Envanter gerçeği tek SoR = MOD-0173 (SCE domain).** Procurement hiçbir modülde ikinci stok balance/ledger açamaz; mal kabul yalnız INVENTORY contract üzerinden post eder (rapor §21.1 rule 3 — "procurement shadow stock tutmaz"). **Shadow stock YASAK.**
- **Ürün kimliği tek SoR = MOD-0290 (MDM).** Procurement ürün/SKU kimliği **yaratamaz/uydurmaz**; yalnız consume eder.
- **AP/ödeme SoR = Finance/Treasury.** Procurement fatura 3-yönlü eşleştirme sonucunu (`match outcome`) üretir; ödeme yürütmez (MOD-0146 kapsam dışı).
- **Single-writer shared seam'ler (K15):** her OWNED contract'ın (SUPPLIER · PO · GRN · MATCH · CONTRACTING · SOURCING) tek yazıcısı ilgili procurement modülüdür. CONSUMED seam'lere (`0173 movement/availability` · `0290 identity` · Location) procurement yalnız **okur/post eder**, yazıcı değildir. Aynı seam'e iki writer YASAK.
- **Contract-first:** cross-module iletişim yalnız published contract (OpenAPI, `docs/analysis/contracts/`). DB/iç tip paylaşımı yok. Frozen contract donunca değişmez; yalnız additive (K16).

## Key Decisions (rapor + Blueprint — RESOLVED / ASSUMPTION)

- **Kapsam:** Supplier→Sourcing→Req/PO→GRN→Invoice-Match→Contracting (Blueprint P2P W-1..W-3). **İş tipi:** Pharma/GxP + regulated procurement (audit/evidence + KYC/sanctions).
- **Scoping:** Tenant + Legal-Entity; `TenantId`/`LegalEntityId` **server-resolved**, payload'dan alınmaz; cross-LE fail-closed (DEC-INV-18 uyumu). Her sorgu TenantId ile sınırlanır.
- **Transaction/posting:** append-only + idempotent (`Idempotency-Key`) + correlation-aware; GRN posting reversible via INVENTORY `REVERSAL`/`SUPPLIER_RETURN` (DEC-INV-07 uyumu).
- **Para/decimal:** parasal alanlar `Decimal` (string, `^-?\d+(\.\d+)?$` — mevcut contract deseni), **float YASAK**; para birimi LE base currency (DEC-INV-18). Precision/rounding module-pack'te finance-precision gate (SOP §18.8) ile netleşir.
- **ASSUMPTION-P2P-01 (3-way match tolerance):** Blueprint ve inventory raporu 3-yönlü eşleştirme **tolerans değeri** vermiyor. MATCH contract'ı toleransı **policy-driven konfigüre edilebilir alan** (`toleranceProfileId` + qty/price/amount toleransları) olarak modelleyip mismatch'i **exception queue**'ya düşürür; sabit sayısal varsayılan **gömülmez**. Gerçek tolerans değerleri tenant policy/Finance kararıdır (EA/Finance-TBD). Bu, contract-bozan değil additive-safe bir açıklıktır.
- **Persistence/evidence:** state-changing akışlar L3 + E4 (UI+API+persisted+audit); G2A golden flow (PO→GRN→0173 posting→invoice 3-way match) L3 + idempotent (rapor §20.2).
- **External-system:** data model `SourceSystem`/`ExternalRef` taşıyabilir (DEC-INV-19 uyumu; ör. dış ERP'den supplier/PO beslemesi).

## Service & Deployment

- **Servis:** `Diten.ProcurementService` (yeni tek servis — procurement domaini; kullanıcı onayı 2026-09-16).
- **Port:** **5065** (mikroservis bandı 5011-5061 doluydu; band 5065'ye uzatıldı — AGENTS.md §3 güncellenir).
- **Persistence:** MongoDB (repo deseni; V3 GUID subtype-4) · her sorguda `TenantId` izolasyonu.
- **Gateway:** `/api/suppliers`, `/api/sourcing`, `/api/requisitions`, `/api/purchase-orders`, `/api/grn`, `/api/invoice-match`, `/api/contracts` route'ları yalnız `integration-agent` tarafından eklenir (protected `ocelot.json`).
- ⚠️ **Servis scaffold** yalnız DCP-010 `approved`/`ready-for-execution` + ilgili module pack (ilk: MOD-0140) `ready-for-dev` + `@orchestrator /add-module` ile başlar (AGENTS.md §2, CAP-001 §7). Bu domain-config scaffold'ı **yetkilendirir**, tetiklemez.

## Cross-references
DCP: [DCP-010-procurement-p2p](../../portfolio/delivery-capability-packs/DCP-010-procurement-p2p.md) · Registry: `MOD-0140…0145` satırları (Procurement — Reserved bloğu) · Contracts: `docs/analysis/contracts/{supplier,sourcing,requisition-po,grn-event,invoice-match,contracting}.openapi.yaml`
