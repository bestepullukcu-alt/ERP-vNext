# CONTROL TOWER — Inventory Capability: v2.1 FINAL ARCHITECTURE BASELINE

> **Rol:** CONTROL TOWER (delivery control plane) · **Tur:** INSPECT → RECONCILE → PLAN (Profile C, read-only — kod/pack/dispatch YOK)
> **Sürüm:** **v2.1 FINAL ARCHITECTURE BASELINE** (v2'yi geçersiz kılar) · **Tarih:** 2026-09-07
> **Kaynaklar (ölçüldü, K2):** `docs/System Capability & Implementation Blueprint - master 8.1.xlsx` (Blueprint_Data, Contract Bundle Dictionary) · `Project ongoing status report v 05 SEP 2026.xlsx` · `execution/registries/module-id-registry.md` · repo kodu (`services/`, `frontend/`)
> **v2.1 gerekçesi:** İkinci dış review 9 mimari remediation önerdi. CONTROL TOWER her maddeyi K2/K9 ile inceledi: 9'u da mimari olarak tutarlı, blueprint'i ihlal etmiyor, yeni modül yaratmıyor → benimsendi. Madde 7 (lot uniqueness) ve 8 (header/line) "aç bırak" yerine **gerekçeli öneri** ile kapatıldı. Değişiklik özeti §0.1'de.

---

## ⚠️ 0.0.0 — HER DEVELOPER ÖNCE BUNU OKUR (çakışma önleme)

> ### ALTIN KURAL
> **OWNS'a yaz · CONSUMES'u sadece FROZEN oku · MUST-NOT'a asla dokunma · başka seam'e yazman gerekiyorsa sahibinden contract iste, kod yazma.**
>
> **Geliştirmeye başlamadan önce sırayla:**
> 1. **§21** — kendi MVP'nin kontratı (OWNS / CONSUMES / MUST-NOT) + global kurallar
> 2. **§17** — hangi contract frozen (tüketmeden önce frozen olmalı)
> 3. **§20** — kendi gate'inin exit checklist'i (ne zaman "bitti")
> 4. **§19** — hangi developer hangi MVP (ayrı worktree)
>
> **Bu kurallara uyulmadan yazılan kod = collision + duplicate-truth + rework (K6/K15).** Emin değilsen CT'ye sor, invent etme (K12).

---

## 0.0. OWNER DECISIONS — RESOLVED (2026-09-08)

| Karar | Sonuç | Etki |
|---|---|---|
| **DEC-INV-11** İş tipi | **Pharma/GxP + Manufacturing** | 0174-0177 **zorunlu/tam** (Lot+CoA+retest, Quarantine+e-imza, FEFO, Recall); **BOM (MOD-0193)** + üretim hareket tipleri kapsamda (GOODS_RECEIPT_PROD / GOODS_ISSUE_PROD) |
| **DEC-INV-01** Kapsam | **Inventory + Warehouse** | WMS (0178/0180/0181/0182) **dahil** — W8 zorunlu, +4-6 hafta |
| **DEC-INV-17** Kimlik | **Model A kanonik · Model B'ye DOKUNMA** | Inventory yalnız `GlobalProduct→Gsku→Lsku→FinishedGood` tüketir. Hardening alanları **Model A'ya** eklenir. Model B (Product) olduğu gibi kalır — migration/link YOK. **→ W1B UNBLOCKED** (hedef aggregate'ler A'da) |
| **DEC-INV-18** Scoping | **Tenant + Legal-Entity** | Stok bakiye + değerleme **legal-entity** bazında; her LE kendi base currency'sinde (mevcut MDM pattern). 0173 dimension'a `LegalEntityId` eklenir |
| **DEC-INV-19** ERP ilişkisi | **vNext birincil SoR + per-LE sisteme-agnostik dış entegrasyon** | vNext SoR'dur; ama bazı legal-entity'ler stok/üretimi **başka (arbitrary, SAP/Oracle şart değil) sistemlerde** tutabilir → vNext entegre olup **mutabakat** yapabilmeli. **Veri modeli şimdiden taşır** (`SourceSystem`, `ExternalRef`, `LegalEntity.InventoryOperatingMode: internal\|external-integrated`); generic integration adapter = **follow-up slice** |
| **DEC-INV-03** Location | **Shared Location/Storage Master (Option B)** | Tek ortak master Site→Warehouse→StorageLocation→Zone→Bin; herkes okur, duplicate yok. Yeni lightweight modül (MOD-# collision-check pending) |
| **DEC-INV-06** Costing | **per-Legal-Entity + per-Product** | `CostingMethodPolicy` LE ve ürün bazında; MOVING_AVERAGE/STANDARD/FIFO desteklenir; fallback default = MOVING_AVERAGE (finans override edebilir) |
| **DEC-INV-02/05/08/12/13/14/15/16** | **Teknik öneriler ONAYLANDI** | transaction-first · reservation/ATP sınırı · StockStatus dim + e-Sign conditional · negative-stock prohibited · backdated policy · lot uniqueness · line-level transaction · MovementType governance |

> **TÜM DEC-INV kararları RESOLVED (2026-09-08).** Kalan tek açık nokta: **stockable-level policy** (W1A spec'te pinlenecek — DEC değil, spec-level detay). Rapor **decision-complete**.

**DEC-INV-17 alt-nokta (SkuId) — DIRECTION SET:** SKU zinciri seviye-seviye opsiyonel (ölçüm: Lsku/FinishedGood çoğu üründe yok). Çözüm **polimorfik**: `SkuId` + `SkuLevel` (Gsku\|Lsku\|FinishedGood), kanonik = ürünün **leaf** SKU'su; kesin stockable-level policy **W1A spec**'te pinlenir. **Gsku→GlobalProduct resolve (D-SKU-LINK)** Model A içinde kurulacak (W1A).

---

## 0.1. v2 → v2.1: FINAL REMEDIATION (9 madde, CONTROL TOWER incelemesiyle)

| # | Remediation | CT verdict | Nerede |
|---|---|---|---|
| R1 | MOD-0175 e-Signature = **CONDITIONAL** (GxP/policy), Workflow HARD | ✅ Doğru — regüle olmayan tenant için release e-sig gerektirmeyebilir | §2, §9 DEC-INV-08 |
| R2 | MOD-0176 FEFO: Demand HARD **değil**; 0174 lot/expiry base; Demand/ATP/Picking=consumer | ✅ Doğru — FEFO fundamentally lot/expiry'e dayanır, Demand tüketici yön | §2, §6 |
| R3 | InventoryReservation modeli derinleştir (10 alan) | ✅ Benimsendi | §4 |
| R4 | DEC-INV-12 Negative Inventory Policy | ✅ Eklendi | §9 |
| R5 | DEC-INV-13 Backdated Posting / Posting Period | ✅ Eklendi | §9 |
| R6 | Serial-controlled invariant: quantity == unique serial count | ✅ Eklendi | §4 (Invariants), §10 |
| R7 | Lot uniqueness scope explicit | ✅ **DEC-INV-14 (öneri: surrogate LotId canonical; business key Tenant+Item+LotNumber)** | §9 |
| R8 | InventoryTransaction header/line canonical seçimi | ✅ **DEC-INV-15 (öneri: line-level immutable + DocumentGroupId)** | §4, §9 |
| R9 | MovementType governance: system-canonical + MOD-0048 reason extension | ✅ **DEC-INV-16** | §4, §9 |

> **CT notu (K2/K9):** Hiçbiri blueprint/ownership çelişkisi yaratmadı → fail-closed gerekmedi. İki maddede (R7, R8) review "ikisinden birini seç / aç bırak" dedi; append-only immutability invariant'ı bir tasarımı tercih ettirdiği için CT gerekçeli öneri ekledi (owner onayına açık, ama boş TBD değil).

---

## 0. v1 → v2: NE DEĞİŞTİ (doğrulanmış düzeltmeler)

| # | v1'deki ifade | Ölçüm sonucu (Blueprint 8.1) | v2 kararı |
|---|---|---|---|
| D1 | "Reservation/Allocation + ATP için modül/entity yok" | **YANLIŞ.** `MOD-0172 Allocation & ATP/CTP` **var** — Commercial Suite / Order-to-Cash bridge, SoR="allocation decisions", contract=`ATP-BUNDLE` | ATP yeniden yaratılmaz. Reservation↔ATP sınırı netleştirildi (§9 DEC-INV-05). **Cross-suite seam** eklendi. |
| D2 | "Location/Storage Master yok" (fazla kesin) | **v2.1 INS düzeltmesi:** MOD-0241 Site Management = **KLİNİK (CTMS/eTMF)**, fiziksel depo site'ı değil → inventory'e uygun DEĞİL. Kodda hiçbir serviste Site/Warehouse/Location/Bin aggregate yok. Fiziksel site/location owner'ı **gerçekten yok** | Tüm location hiyerarşisi (Site→WH→StorLoc→Zone→Bin) = **TBD** (§9 DEC-INV-03). Reuse edilecek master yok; sıfırdan. |
| D3 | "MOD-0290'da UoM/conversion yok" (fazla kesin) | **KISMEN.** MOD-0290 SoR'unda UoM + "UoM & Identifier Mapping" page **var**. `UoMConversion` **semantics'i** explicit değil | UoM var; **conversion contract'ı MOD-0290 hardening** kapsamı (yeni modül değil). |

**Korunan v1 bulguları (doğru):** Inventory = capability block (CRUD değil) · MOD-0290 zorunlu foundation · MOD-0173 = authoritative SoR çekirdek · balance doğrudan edit edilmez · append-only movement = stok gerçeği · warehouse ikinci balance yaratmaz · 0174→0175→0176→0177 pharma zinciri doğru · contract bundle'lar Blueprint sözlüğünde tanımlı (blocker yok).

---

## 1. MODULE ROSTER

### 1.1 Foundation & Consumed Masters (Inventory'nin TÜKETTİĞİ — ayrı sahiplik)

| MOD | Ad | Suite / Group | Rol (Inventory'ye göre) | Repo Durumu |
|---|---|---|---|---|
| **MOD-0290** | Product / Item / SKU Master | Master Data / Product Foundation | Ürün/item/SKU/UoM kimliği SoR — Inventory **referans verir** | **PARTIAL** (MdmService) |
| ~~MOD-0241~~ | ~~Site Management~~ | **Clinical Ops (CTMS/eTMF)** | **KLİNİK site — fiziksel depo site'ı DEĞİL, kullanılamaz** | Registry'de YOK, kod YOK |
| **[YOK]** | Physical Site / Plant master | — | Fiziksel operating site owner'ı **hiç yok** (kodda aggregate yok) | DEC-INV-03 kapsamı |
| **MOD-0172** | Allocation & ATP/CTP | Commercial Suite / O2C bridge | Availability/promise **kararı** — Inventory'nin availability query'sini tüketir | Registry'de var (**reserved/planned, kod YOK**) |
| **[TBD]** | Warehouse / Storage / Bin hierarchy owner | Supply Chain (belirsiz) | WH→StorLoc→Zone→Bin master | **Owner YOK — DEC-INV-03** |

### 1.2 Inventory & Traceability çekirdeği (ASIL İŞ — Supply Chain Execution Suite)

| MOD | Ad | Wave (BP) | Core/Support | SoR objesi (BP) | Repo |
|---|---|---|---|---|---|
| **MOD-0173** | **Inventory Ledger & Valuation** | W-2 | **Core — SoR çekirdek** | inventory balances | YOK |
| **MOD-0174** | Lot / Batch / Serial Tracking | W-2 | Core | lots/serials | YOK |
| **MOD-0175** | Quarantine / Blocked Stock | W-3 | Core | quarantine holds | YOK (statik shell) |
| **MOD-0176** | Expiry / FEFO Management | W-4 | Core | expiry policies | YOK |
| **MOD-0177** | Recall Readiness | W-4 | Support | recall cases | YOK |

### 1.3 Warehouse / WMS (ayrı delivery wave — ledger stabil olunca)

| MOD | Ad | Wave | SoR (BP) | Core/Cond. |
|---|---|---|---|---|
| **MOD-0178** | Putaway / Picking / Packing | W-3 | warehouse tasks | Core |
| **MOD-0180** | Wave Planning | W-4 | waves | Core |
| **MOD-0181** | Cycle Counting | W-4 | count events | Core |
| **MOD-0182** | Barcode / RFID | W-4 | scan events | Core |
| **MOD-0179** | 3PL Integration | W-3 | — | Conditional |

### 1.4 Feeders / Downstream (contract seam — burada geliştirilmez)

MOD-0142 GRN (besler→0173) · MOD-0141 PO · MOD-0188/0189 Demand/MRP (okur←0173) · MOD-0172 ATP (okur←0173) · MOD-0193 BOM (manufacturing ise).

---

## 2. DEPENDENCY MATRIX

| WP / Modül | Depends on (gate) | Blocks | Parallel-safe with |
|---|---|---|---|
| MOD-0290 identity reconciliation (DEC-INV-17) | Data Contract Registry, Canonical ID | 0290 hardening, 0173 | Location decision |
| MOD-0290 inventory-facing hardening | DEC-INV-17 kapalı | 0173, 0174 | Location decision |
| **Location ownership decision (DEC-INV-03)** | 0290, registry/capability collision-check | 0173, 0178 | — |
| **MOD-0173 Ledger** | 0290 (inventory-facing gaps kapalı), Location decision, Canonical ID | 0174,0175,0176,0177,0178,0181,0172-consume | — (single-writer SoR) |
| MOD-0174 Lot/Serial | 0173, Canonical ID | 0175,0176,0177 | — |
| MOD-0175 Quarantine | 0174 · **Workflow (MOD-0023) HARD** · **e-Sign (MOD-0022) CONDITIONAL** (GxP/policy-driven) | release flows | 0176,0177 |
| MOD-0176 FEFO | **0174 lot/expiry (HARD base)** | pick allocation constraint | 0175,0177 |
| MOD-0176 FEFO consumers | — | Demand (0188), ATP (0172), Picking (0178) **tüketir** (integration, dep değil) | — |
| MOD-0177 Recall | 0174, Evidence (MOD-0029) | — | 0175,0176 |
| MOD-0178 WMS | 0173 (movement contract frozen), Location | 0180,0181,0182 | 0181 |
| MOD-0172 ATP (consumer) | 0173 availability query frozen | O2C | — |

**Kural:** 0174–0177 ve 0178+ paralel gidebilir **ancak** MOD-0173 hareket + availability contract'ları **freeze** edildikten sonra (single-writer shared seam, K15).

---

## 3. SoR OWNERSHIP MATRIX (duplicate-ownership yasak)

| Obje / Concern | Owner (SoR) | Consumers | Yasak |
|---|---|---|---|
| Product / Item / SKU / UoM kimliği | **MOD-0290** | tümü | başkası ürün kimliği yaratamaz |
| UoM conversion | **MOD-0290** | 0173 (base qty'ye çevirir) | — |
| Physical Site / Plant / Warehouse / StorageLocation / Zone / Bin | **TBD — DEC-INV-03** (owner YOK, greenfield) | 0173, 0178 | karar öncesi kod yok; **MOD-0241 klinik site, aday değil** |
| **Inventory movement / transaction** | **MOD-0173** | herkes okur | ikinci ledger yasak |
| **Inventory balance (projection)** | **MOD-0173** | herkes okur | doğrudan edit yasak |
| **Stock status (fiziksel) dimension** | **MOD-0173** (dimension) | 0175 lifecycle | 0175 balance tutamaz |
| Quarantine lifecycle / release / disposition | **MOD-0175** | 0173 status transition | balance yaratamaz |
| **Reservation (fiziksel stok hold)** | **MOD-0173** | 0172 okur | 0172 stok tutamaz |
| **Allocation / ATP / CTP decision** | **MOD-0172** | O2C | ikinci stok balance yasak |
| Lot / Batch / Serial | **MOD-0174** | 0173,0175,0176,0177 | — |
| Shelf-life **policy** | **MOD-0290** (product) | 0176 | — |
| Actual expiry/mfg/retest (lot) | **MOD-0174** | 0176 | — |
| FEFO allocation constraint | **MOD-0176** | 0178 pick, 0172 | — |
| Valuation / cost layers | **MOD-0173** | Finance/GL | — |
| Warehouse task / wave / count / scan | **MOD-0178/0180/0181/0182** | 0173 posting | balance yaratamaz |

---

## 4. ENTITY MATRIX (developer-ready — Blueprint minimal SoR'un altındaki gerçek domain)

> **[BP]**=blueprint'te açık · **[EXT]**=gerçek ERP için gerekli genişletme (benimsendi) · **[P2]**=extensibility, MVP sonrası

### MOD-0290 Product/Item/SKU (foundation — hardening) — **INS ölçüldü 2026-09-07**
**VAR (MdmService):** SKU-coding zinciri `GlobalProduct→ProductDefinitionRevision→Gsku→Lsku→FinishedGood` (CanonicalCode, CodeReservation, lifecycle, audit-intent) · brand `Product`(ProductCode, DosageForm, Strength, PackSize, `UnitOfMeasure`=tek string, ExternalReferences) · `Gsku.PackUomCode`+`PackQuantity` (pack descriptor)
**YOK → hardening (inventory-facing):** **`UoMConversion`** (From/To/Numerator/Denominator/Effective — pack descriptor≠conversion) · **`ProductIdentifier` GTIN/GS1/barcode** (sadece internal code + generic ExternalReference) · **`ShelfLifeDays`/`MinRemainingShelfLife`/`StorageCondition`/`RetestDays`** (hiç yok) · **`LotControlled`/`SerialControlled` flags** · `ProductClassification`(ABC) [P2]

### MOD-0173 Inventory Ledger & Valuation (THE HEART)
- **`InventoryTransaction`/`InventoryMovement`** [EXT, append-only]: Id, TransactionNumber, **DocumentGroupId**, **TenantId, LegalEntityId** (DEC-INV-18), MovementType, MovementReason, TransactionDate, PostingDate, ItemId (GlobalProduct), **SkuId + SkuLevel** (Gsku\|Lsku\|FinishedGood, DEC-INV-17), From/To Warehouse+Location, Quantity, UomId, **BaseQuantity, BaseUomId**, LotId, SerialId, From/ToStockStatus, **SourceModule, SourceType, SourceDocumentId, SourceLineId, SourceSystem** (DEC-INV-19 dış entegrasyon), ReasonCode, UnitCost, TotalCost, Currency (LE base), **CorrelationId, IdempotencyKey**, CreatedBy, CreatedAt
  - **Header/line kararı → DEC-INV-15 (öneri: line-level immutable movement + `DocumentGroupId`)**: her hareket satırı atomik immutable fact; multi-line belge `DocumentGroupId` ile bağlanır. Header+Line yerine bu tercih ediliyor çünkü mutable header aggregate append-only immutability invariant'ıyla çelişir.
- **`MovementType`** [EXT, first-class, **system-canonical/SABİT** — DEC-INV-16]: OPENING_BALANCE, GOODS_RECEIPT_PO, GOODS_RECEIPT_PROD, GOODS_ISSUE_SALES, GOODS_ISSUE_PROD, WAREHOUSE_TRANSFER, LOCATION_TRANSFER, STATUS_TRANSFER, CUSTOMER_RETURN, SUPPLIER_RETURN, COUNT_GAIN, COUNT_LOSS, SCRAP, WRITE_OFF, REVERSAL — *kullanıcı serbestçe yeni type yaratamaz (stok semantiğini bozar)*
- **`MovementReason`** [EXT, ayrı boyut, **MOD-0048 Reference Data ile kontrollü genişletilebilir** — DEC-INV-16]: (WRITE_OFF×EXPIRED, WRITE_OFF×DAMAGED …) — type'a eritilmez
- **`InventoryDimension`** [EXT, canonical uniqueness key]: Tenant, **LegalEntity** (DEC-INV-18), Item, **Sku+SkuLevel**, Site, Warehouse, Location, Lot, Serial, StockStatus, Owner, Project/SpecialStock
- **`LegalEntity.InventoryOperatingMode`** [EXT, DEC-INV-19]: `internal` (vNext SoR) \| `external-integrated` (dış sistem SoR, vNext mutabakatlı ayna) — per-LE; external-integrated LE'ler generic inventory integration seam kullanır (inbound/outbound/reconcile, follow-up)
- **`InventoryBalance`** [EXT, projection/read-model]: ItemId, InventoryDimensionKey, OnHand, Reserved, Available, InTransit, Blocked, QualityInspection, BaseUomId, LastMovementId, LastPostingDate — **invariant: `Available ≠ OnHand`** (policy formülü)
- **`StockStatus`** [EXT, dimension]: AVAILABLE, QUALITY_INSPECTION, QUARANTINE, BLOCKED, IN_TRANSIT, DAMAGED, EXPIRED
- **`InventoryReservation`** [EXT] (fiziksel hold — StockStatus değil, ayrı commitment): ReservationId · ItemId · InventoryDimensionKey · Quantity · BaseQuantity · ReservationType · ReservationStatus · SourceModule · SourceDocumentId · SourceLineId · Priority · ExpiresAt · CreatedAt · ReleasedAt
- **`InventoryValuation`/`InventoryCostLayer`/`InventoryCostAdjustment`/`CostingMethodPolicy`** [EXT] (STANDARD/MOVING_AVG/FIFO — DEC-INV-06)
- **`InventoryOwnershipType`+`OwnerPartyId`** [P2]: OWNED, CONSIGNMENT, SUPPLIER_OWNED, CUSTOMER_OWNED, 3PL_MANAGED, PROJECT_STOCK, SUBCONTRACT_STOCK

### MOD-0174 Lot/Batch/Serial
`Lot`(**LotId** surrogate, LotNumber, ItemId, ManufacturerLot, SupplierLot, ManufacturingDate, ExpiryDate, RetestDate, CountryOfOrigin, Status, CoAReference, StorageCondition) · `SerialNumber`(SerialStatus, ItemId, LotId) · **`LotGenealogyLink`(ParentLotId, ChildLotId, TransformationReference)** [EXT — recall için]

### Posting Invariants (0173 + 0174)
- **Serial-controlled item (R6):** posting'de `Quantity` ile ilgili hareketteki **unique serial sayısı EŞİT** olmak zorunda; serial olmadan serial-controlled posting reddedilir.
- **Lot uniqueness (R7 → DEC-INV-14):** canonical kimlik = surrogate `LotId`; business dedup scope **Tenant+Item+LotNumber** (default öneri). `ManufacturerLot`/`SupplierLot` **attribute**'tur, key değil — iki tedarikçinin aynı LotNumber'ı çakışmasın diye. Scope karar verilmeden implementation varsayım yapmaz.

### MOD-0175 Quarantine/Blocked
`QuarantineHold` · `ReleaseDecision` · `Disposition` · `StockStatusTransition` · `BlockReason` — **balance tutmaz**, 0173 status'unu değiştirir

### MOD-0176 Expiry/FEFO
`ShelfLifeRule` · `ExpiryPolicy` · **`FEFOAllocationConstraint`** (pick/allocation'a contract üretir) · `FEFOExceptionCase`

### MOD-0177 Recall
`RecallCase` · `RecallDrillPlan` · `TraceForwardQuery` · `TraceBackwardQuery` · `EvidencePack`

### MOD-0178 WMS
`WarehouseTask`(Putaway/Pick/Pack) · `ScanEvent` · `HandlingUnit/Pallet` [EXT] · Bin refs (Location owner'a bağlı) — her confirmation 0173'e post

---

## 5. PAGE / UI MATRIX + NAMING CORRECTION

**Kritik: "Add Material" tek aksiyon DEĞİL — iki ayrı golden flow, iki ayrı owner:**

| İşlem | Owner | UI aksiyonu (doğru isim) |
|---|---|---|
| Yeni ürün/item tanımlama | **MOD-0290** | **New Item / New SKU** |
| Stoğa giriş | **MOD-0173** | **Post Stock Movement** → alt: Opening Stock · Goods Receipt · Goods Issue · Transfer · Adjustment · Return · Scrap |

> Normal satın almada **manual stock add kullanılmaz**: PO→GRN(0142)→Inventory posting(0173). Manual movement sadece: opening balance, controlled adjustment, transfer, count variance, write-off, scrap, exceptional correction.

| Owner | Pages |
|---|---|
| **MOD-0290** | Product/Item List · Item Detail · Create/Edit Item · SKU Management · UoM & Identifier Mapping [BP] |
| **MOD-0173** | Inventory Dashboard · Stock On Hand · Inventory Transactions · Transaction Detail · Post Stock Movement · Opening Balance · Stock Transfer · Stock Adjustment · Stock by Warehouse/Location/Lot/Status · Reservation/Availability · Inventory Valuation · Ledger Trace [BP] · Evidence Pack [BP] |
| **MOD-0174** | Lot/Batch Explorer · Serial Explorer · Trace Forward · Trace Backward · Lot Transaction History |
| **MOD-0175** | Quarantine Stock · Blocked Stock · Release/Reject · Disposition |
| **MOD-0176** | Expiry Dashboard · Expiring Soon · Expired Stock · FEFO Exception Inbox |
| **MOD-0177** | Recall Readiness Dashboard · Evidence Pack Generator · Drill Planner |

---

## 6. CROSS-MODULE INTERFACE MATRIX (contract-first, versioned)

| Seam | Producer | Consumer | Contract / Event | Kural |
|---|---|---|---|---|
| Product identity | MOD-0290 | 0173,0174 | PRODUCT-MASTER-BUNDLE ✓ | referans, kopyalama yok |
| UoM conversion | MOD-0290 | 0173 | (PRODUCT-MASTER-BUNDLE ext) | 0173 base qty'ye çevirir |
| Goods receipt posting | MOD-0142 GRN | 0173 | inventory receipt event | idempotent, source-ref zorunlu |
| Inventory movement | MOD-0173 | herkes | INVENTORY-BUNDLE ✓ | append-only |
| Availability query | MOD-0173 | **0172 ATP**, 0189 MRP | availability contract | OnHand/Reserved/Available |
| Allocation/ATP promise | **MOD-0172** | O2C | **ATP-BUNDLE** ✓ | ikinci stok yok |
| Lot/serial trace | MOD-0174 | 0175,0176,0177 | TRACE-BUNDLE ✓ | correlation ID |
| Quarantine status transition | MOD-0175 | 0173 | QUARANTINE-BUNDLE ✓ | status transfer movement |
| FEFO constraint | MOD-0176 | 0178 pick, 0172 ATP, 0188 Demand | FEFO-BUNDLE ✓ | allocation constraint (consumer yön) |
| WMS confirmation | MOD-0178 | 0173 | WMS-BUNDLE ✓ | movement post, idempotent |
| Recall trace | MOD-0177 | evidence | RECALL-BUNDLE ✓ | — |

> **Cross-suite dikkat:** MOD-0172 **Commercial Suite**'te — inventory↔ATP seam iki capability bloğu arasında; integration order ve contract freeze buna göre.

---

## 7. SAP / ORACLE / DYNAMICS GAP MATRIX

| Capability | SAP | Oracle | Dynamics | Blueprint | Durum / Aksiyon |
|---|---|---|---|---|---|
| Product/Item/SKU | Material Master | Item (PIM) | Released Product | MOD-0290 | Var; hardening |
| UoM | ✓ | ✓ | ✓ | MOD-0290 | Var |
| UoM Conversion | ✓ | ✓ | ✓ | Kısmi | **Explicit entity/contract** |
| Inventory Movement Ledger | Material Doc | Material Txn | Inventory Txn | MOD-0173 (implied) | **Explicit implementation** |
| Movement Type | Movement types | Txn types | — | Dolaylı | **First-class model** |
| Movement Reason | Reason codes | ✓ | ✓ | Açık değil | **Eklenmeli** |
| Inventory Balance | Stock | On-hand | On-hand | MOD-0173 | **Projection tasarımı** |
| Inventory Dimensions | Plant/SLoc/Batch | Org/Sub/Locator | Site/WH/Dim | Açık değil | **Explicit canonical model** |
| Physical Site / Plant | Plant | Org | Site | **owner YOK** (MOD-0241=klinik) | **GAP / canonical owner TBD (DEC-INV-03)** |
| Warehouse/Location/Bin | WM/EWM bin | Locator | WMS location | Kısmi | **Ownership TBD (DEC-INV-03)** |
| Stock Status | Unrestr/QI/Blocked | Status | Status | 0175 ilişkili | **0173 dimension** |
| Lot/Batch | ✓ | ✓ | ✓ | MOD-0174 | Var |
| Serial | ✓ | ✓ | ✓ | MOD-0174 | Var |
| Expiry/SLED | ✓ | ✓ | ✓ | MOD-0176 | Var |
| FEFO | ✓ | ✓ | ✓ | MOD-0176 | Var (constraint netleş) |
| Reservation | ✓ | ✓ | ✓ | Belirsiz | **MOD-0173 ownership** |
| ATP/Allocation | ATP | ATP/GOP | ATP | **MOD-0172** | **Zaten var — yeniden yaratma** |
| Valuation/Costing | MovAvg/Std/ML | FIFO/LIFO/Avg/Std | Std/Avg/FIFO | MOD-0173 | **Method engine kararı** |
| Cycle Count | ✓ | ✓ | ✓ | MOD-0181 | Var |
| WMS | WM/EWM | WMS | WMS | MOD-0178+ | Var |
| Barcode/RFID | ✓ | ✓ | ✓ | MOD-0182 | Var |
| Special Stock | Consign/Subcon/Project | Consigned | — | Belirsiz | **P2 extensibility** |
| Inventory Ownership | ✓ | ✓ | ✓ | Belirsiz | **P2 model hazırlığı** |
| Lot Genealogy | Batch derivation | Genealogy | — | Kısmi | **0174 explicit** |
| Reversal Posting | Cancel/reverse | ✓ | ✓ | Açık değil | **0173 zorunlu** |
| Posting Idempotency | (interface) | ✓ | ✓ | Açık değil | **0173 zorunlu control** |
| Transaction Source Ref | Ref doc | Source/lot ref | Ref | Açık değil | **Cross-module contract** |
| In-transit transfer | 2-step / STO | Inter-org | Transfer order | Kısmi | **Netleştir** |

---

## 8. P0 / P1 / P2 GAP REGISTER

**P0 (architecture-critical — 0173 production-ready olmadan kapanmalı):**
1. InventoryTransaction / Movement Ledger append-only semantics
2. MovementType + MovementReason first-class
3. InventoryDimension canonical uniqueness model
4. StockStatus dimension (0173) + 0175 lifecycle boundary
5. UoM conversion contract (MOD-0290 hardening)
6. Reservation ownership (0173) ↔ MOD-0172 ATP boundary
7. Transaction source/reference + idempotency + reversal semantics
8. Valuation / costing method decision (DEC-INV-06)
9. InventoryBalance = projection (doğrudan edit yasak)
10. Location hierarchy canonical ownership (DEC-INV-03, TBD)
11. InventoryTransaction header/line model — line-level immutable + DocumentGroupId (DEC-INV-15)
12. Negative inventory + backdated/closed-period posting policy (DEC-INV-12/13)
13. MovementType governance — system-canonical + MOD-0048 reason extension (DEC-INV-16)

**P1:** Lot genealogy (0174) · FEFO allocation constraint consumers · In-transit two-step transfer · Cycle-count adjustment posting · Serial-count invariant · Lot uniqueness scope (DEC-INV-14).

**P2 (extensibility, MVP sonrası):** Inventory ownership / special stock types · Multi-costing / material ledger · 3PL (MOD-0179).

---

## 9. ARCHITECTURE DECISION REGISTER

| ID | Karar | Durum |
|---|---|---|
| **DEC-INV-01** | **RESOLVED 2026-09-08: Inventory + Warehouse.** Core (0290/0173/0174-0177) **+ WMS (0178/0180/0181/0182) dahil**. | ✅ **RESOLVED** |
| **DEC-INV-02** | `InventoryTransaction` authoritative; `InventoryBalance` projection; doğrudan balance edit **yasak** | ✅ **RESOLVED** |
| **DEC-INV-03** | **RESOLVED 2026-09-08: Option B — Shared Location/Storage Master.** Tek ortak master (Site→Warehouse→StorageLocation→Zone→Bin); 0173+0178+procurement+manufacturing **okur**. Duplicate ownership yok. Yeni lightweight master modül (**MOD-? candidate — registry collision-check gerekli**; MOD-0241 klinik, aday değil). | ✅ **RESOLVED** (MOD-# atama pending) |
| **DEC-INV-04** | Canonical InventoryDimension key explicit tanımlanacak | ✅ **RESOLVED** |
| **DEC-INV-05** | Operational reservation = MOD-0173; Allocation/ATP/CTP decision = MOD-0172. 0172 ikinci balance yaratmaz | ✅ **RESOLVED** (measured) |
| **DEC-INV-06** | **RESOLVED 2026-09-08: costing = per-Legal-Entity + per-Product.** `CostingMethodPolicy` LE ve ürün bazında konfigüre edilir; üç yöntem de desteklenir (MOVING_AVERAGE / STANDARD / FIFO). Konfigüre edilmemiş fallback default = **MOVING_AVERAGE** (finans override edebilir). | ✅ **RESOLVED** |
| **DEC-INV-07** | Posting semantics: append-only + idempotent + correlation-aware + source-traceable + reversible | ✅ **RESOLVED** |
| **DEC-INV-08** | StockStatus = MOD-0173 dimension; MOD-0175 = status lifecycle/governance owner. Quarantine release'de **Workflow HARD, e-Signature CONDITIONAL** (GxP/policy) | ✅ **RESOLVED** |
| **DEC-INV-09** | MOD-0290 inventory-facing gaps (UoM conversion, identifier, shelf-life) kapanmadan MOD-0173 production-ready sayılmaz | ✅ **RESOLVED** |
| **DEC-INV-10** | Warehouse hiçbir zaman ikinci authoritative balance oluşturamaz | Öneri (blueprint kuralı) |
| **DEC-INV-11** | **RESOLVED 2026-09-08: Pharma/GxP + Manufacturing.** 0174-0177 zorunlu/tam (CoA/retest/e-imza); **BOM MOD-0193 + üretim hareketleri** kapsamda. | ✅ **RESOLVED** |
| **DEC-INV-12** | **Negative Inventory Policy:** default = negative stock **yasak**; exception ancak explicit company/warehouse/item policy + audit ile | ✅ **RESOLVED** |
| **DEC-INV-13** | **Backdated Posting / Posting Period:** `TransactionDate` ≠ `PostingDate` korunur; kapalı dönem / backdated posting explicit policy ile yönetilir | ✅ **RESOLVED** |
| **DEC-INV-14** | **Lot uniqueness scope:** surrogate `LotId` canonical; business dedup key default = **Tenant+Item+LotNumber** (Manufacturer/SupplierLot = attribute). Kesinleşmeden varsayım yok | ✅ **RESOLVED** (owner onayladı) |
| **DEC-INV-15** | **InventoryTransaction model:** **line-level immutable movement + `DocumentGroupId`** (öneri) — Header+Line yerine, append-only immutability için | ✅ **RESOLVED** (owner onayladı) |
| **DEC-INV-16** | **MovementType governance:** system-level category **canonical/sabit**; business reason/code **MOD-0048 Reference Data** ile kontrollü genişletilebilir; kullanıcı serbest movement type yaratamaz | ✅ **RESOLVED** |
| **DEC-INV-17** | **RESOLVED 2026-09-08: Model A kanonik, Model B'ye dokunma.** MOD-0173 `ItemId`→`GlobalProduct`, `SkuId`→`Gsku\|Lsku\|FinishedGood` (Model A). Hardening alanları (UoMConversion/GTIN/shelf-life/Lot-Serial) **Model A'ya** eklenir. Model B (`Product`) **olduğu gibi kalır** — migration/link yok, inventory B'yi tüketmez. Açık alt-nokta: SkuId seviyesi + Gsku→GlobalProduct link (D-SKU-LINK, A içinde kurulacak). | ✅ **RESOLVED** (alt-nokta W1A) |
| **DEC-INV-18** | **RESOLVED 2026-09-08: Scoping = Tenant + Legal-Entity.** Stok bakiye/değerleme LE bazında; valuation currency = LE base currency. 0173 dimension'a `LegalEntityId`. | ✅ **RESOLVED** |
| **DEC-INV-19** | **RESOLVED 2026-09-08: vNext birincil SoR + per-LE sisteme-agnostik dış entegrasyon.** Bazı LE'ler stok/üretimi dış (arbitrary) sistemde tutabilir → vNext mutabakatlı entegre olur. Data model şimdiden: `SourceSystem`, `ExternalRef`, `LegalEntity.InventoryOperatingMode`. Generic adapter = follow-up. | ✅ **RESOLVED** (adapter follow-up) |

---

## 10. GOLDEN-FLOW IMPLEMENTATION SEQUENCE

**Build waves:**
```
W0  Prereq:  Data Contract Registry · Canonical ID (0040) · Reference Data (0048) · RBAC (0018) · Audit (0021) · Evidence/Records/Observability
W1A MOD-0290 canonical identity reconciliation (DEC-INV-17): Product vs GlobalProduct/Gsku/Lsku/FinishedGood ownership → tek canonical Item/SKU contract
W1B MOD-0290 inventory-facing hardening (DEC-INV-17 kapalı SONRASI): UoMConversion · ProductIdentifier/GTIN · ShelfLife/MinRemainingShelfLife · StorageCondition/Retest · LotControlled/SerialControlled
W2  Physical location ownership DECISION (DEC-INV-03): Site→Warehouse→StorageLocation→Zone→Bin (greenfield; MOD-0241 aday değil; collision-check)
W3  MOD-0173 core: MovementType·MovementReason·InventoryTransaction·InventoryDimension·InventoryBalance·InventoryReservation·InventoryValuation
W4 MOD-0174: Lot·Serial·LotAttributes·Genealogy·Traceability
W5 MOD-0175: Quarantine·Blocked·Release·Disposition·StatusTransition
W6 MOD-0176: ShelfLifeRules·Expiry·FEFO·FEFOException
W7 MOD-0177: RecallCase·TraceFwd·TraceBwd·Drill·EvidencePack
W8 Warehouse: 0178·0181·0182·0180
```

**MOD-0173 golden flow (PASS için zorunlu):**
```
Existing Item/SKU → Warehouse/Location seç → Opening +100 post
→ InventoryTransaction persist (L3) → InventoryBalance=100 projection
→ reload: hareket+bakiye aynı → Issue -20 post → 2. immutable txn
→ Balance=80, ilk movement değişmez (history)
Failure paths:
- aynı IdempotencyKey → 2. posting YOK
- yetersiz stok + negative-policy=prohibited (DEC-INV-12) → movement YOK, balance değişmez, canonical domain error
- serial-controlled item'de quantity ≠ unique serial count (R6) → reddedilir
- kapalı dönem / backdated posting policy izin vermiyorsa (DEC-INV-13) → reddedilir; TransactionDate≠PostingDate korunur
```

**Lot/Quality golden flow:**
```
Existing SKU → GRN/opening receipt → Lot oluştur (mfg/expiry) → post
→ StockStatus=QUALITY_INSPECTION/QUARANTINE → QC release → AVAILABLE
→ reload: lot + status transition + quantity trace korunur
```

---

## 11. GOVERNANCE GATES + CONTROL TOWER NEXT (dispatch DEĞİL)

**Uygulamadan önce (fail-closed DoR):** Delivery Capability Pack (supply-chain inventory) → supply-chain domain config → MOD-0173 module pack (`approved/ready-for-dev`) → registry adoption (0173–0178 satırları, DCP-002 gate). Contract bundle'lar tanımlı ✓ (blocker yok).

**Açık owner kararları:** **YOK — tüm DEC-INV RESOLVED (2026-09-08).** Kalan tek nokta: stockable-level policy (W1A spec pin, DEC değil).

**Sıradaki güvenli iş (öneri):** *(INS + DEC-INV-17 reconciliation TAMAMLANDI; kapsam/iş-tipi/kimlik kararları alındı.)*
1. **W1A module spec** — Model A identity + **Gsku→GlobalProduct resolve contract** (D-SKU-LINK) + SkuId seviyesi kesinleştir.
2. **W1B module spec** — MOD-0290 hardening (§8 tablosu, hedef Model A: PDR/Gsku/Lsku/FinishedGood).
3. **DEC-INV-03 location** (greenfield Option A vs B) → sonra **MOD-0173 module pack** (transaction-first, §4 + §14.2).

---

## 12. CHATGPT REVIEW RECONCILIATION (evidence provenance — K2/K13)

Dış review claim'di (E0); CONTROL TOWER Blueprint authority'sine karşı ölçtü:

| İddia | Ölçüm | Verdict |
|---|---|---|
| MOD-0172 Allocation & ATP/CTP var | Blueprint_Data: MOD-0172, Commercial Suite/O2C, ATP-BUNDLE ✓ | **CONFIRMED** — v1 hatası düzeltildi |
| MOD-0173 "locations (as scoped)" tanır | MOD-0173 SoR = "inventory balances", location literal **yok** | **NOT CONFIRMED** |
| MOD-0241 inventory Site master olabilir | INS: MOD-0241 = **Clinical Ops (CTMS/eTMF) klinik site** — fiziksel inventory/warehouse site DEĞİL; inventory location çözümü değil | **REJECTED** (physical location owner YOK, DEC-INV-03) |
| MOD-0290 UoM/mapping var | MOD-0290 SoR: UoM + "UoM & Identifier Mapping" page ✓ | **CONFIRMED** — conversion semantics ext |
| Transaction-first / dimension / movement-type / idempotency / reversal / source-ref | Gerçek ERP (SAP/Oracle) ile uyumlu, DERIVED CONTROL | **BENİMSENDİ** |

> Not: Review'ın mimari genişletmeleri güçlü ve benimsendi. Olgusal düzeltmelerin **ikisi doğrulandı, biri kısmen yanlıştı** (location). CONTROL TOWER hiçbir claim'i ölçmeden kabul etmez.

---

## 13. WAVE DEPENDENCY & DURATION (Supply Planning sheet ⋈ Blueprint)

> **Kaynak:** `Supply Planning Procure Cap Blo` (MVP gate'leri G1–G5, SoR boundary) + Blueprint 8.1 (per-MOD dependency gate + wave). **Terminoloji:** aşağıdaki **Build Wave** (inşa sırası) ≠ WMS **Pick Wave** (MOD-0180) ≠ Blueprint **W-1…W-4** (portföy dalgası).
> **Süre uyarısı (K10):** rakamlar **planning estimate**'tir, taahhüt değil. Varsayımlar: (a) **1 primary Development Agent Lane** / shared seam (single-writer, K15); (b) tahmin = build + self-validate + **independent verification + integration gate** (sadece kodlama değil); (c) DoR geçmiş, contract'lar frozen, ağır rework yok; (d) **pharma/GxP scope** (0174–0177 zorunlu — distribution-only ise kısılır); (e) açık kararlar (DEC-INV-01/03/06/11/17) kapanmış. Rework/karar gecikmesi süreyi uzatır.

### 13.1 Reconciled dependency chain (MVP gate ⋈ Blueprint gate)

```
MVP-1 FOUNDATION  (Gate G1 "Product & Inventory Truth Ready")   ← INVENTORY CAPABILITY BURADA
   0290 (id+hardening) → 0173 → {0174 → 0175/0176/0177}
   BP gate: 0173←DataContractRegistry+SoR/OwnershipRegistry · 0174←CanonicalID · 0175←0174+Workflow · 0176←0174 · 0177←0174+Evidence
   ⛔ MVP-1 FREEZE olmadan MVP-2/3 başlayamaz (hard gate)
        │
        ├── MVP-2 PROCUREMENT (G2A)  0140/0141/0142/0143/0145  — 0142 GRN → 0173 posting
        └── MVP-3 BOM (G2B)          0193                       — manufacturing ise
             (MVP-2 ⇄ MVP-3 paralel; ikisi de MVP-4'ü besler)
                  │
             MVP-4 DEMAND+MRP (G3)   0188/0189/0191            — 0173 availability ← okur
                  │
             MVP-5 WAREHOUSE (G4)    0178/0180/0181/0182       — 0173 movement contract FROZEN sonrası; late MVP-4 ile overlap OK
                  │
             MVP-6 INTEGRATED (G5)   0190/0192/0183-0187       — en son
```

### 13.2 Build Wave süre tahmini — INVENTORY CORE (MVP-1 + MVP-5)

| Build Wave | Modül / iş | Depends on (gate) | Efor | Est. hafta (1 lane) | Not |
|---|---|---|---|---|---|
| **W0** | Backbone doğrulama | — (çoğu mevcut) | S | **0.5–1** | Verify-only: Canonical ID/RBAC/Audit/Workflow/Evidence |
| **W1A** | MOD-0290 identity reconciliation (DEC-INV-17) | W0 | M | **1–2** | Karar + A↔B link/consolidate tasarımı; **CRM koordinasyonu** süreyi uzatabilir |
| **W1B** | MOD-0290 inventory-facing hardening | **W1A kapalı** | L | **2–3** | UoMConv+GTIN+shelf-life+storage+retest+Lot/Serial flags; migration/backfill |
| **W2** | Physical location ownership (DEC-INV-03) | W0 | M–L | **2–3** | Greenfield Site→WH→StorLoc→Zone→Bin; owner + master |
| **W3** | **MOD-0173 Inventory Ledger (THE HEART)** | W1B, W2, CanonicalID | **XL** | **4–6** | Transaction engine + movement type/reason + dimension + balance projection + reservation + valuation + idempotency/reversal. **HIGH risk** |
| **W4** | MOD-0174 Lot/Batch/Serial | W3 frozen | L | **2–3** | + genealogy (recall için) |
| **W5** | MOD-0175 Quarantine | W4 | M | **1.5–2** | Workflow HARD, e-Sign conditional |
| **W6** | MOD-0176 Expiry/FEFO | W4 | M | **1.5–2** | FEFO allocation constraint |
| **W7** | MOD-0177 Recall | W4 | M | **1.5–2** | Trace fwd/bwd + evidence pack |
| **W8** | Warehouse (0178/0181/0182/0180) | W3 movement contract frozen | L–XL | **4–6** | 4 modül; MVP-5 = ayrı delivery |

**Rollup (inventory core):**
- **Sequential (tek lane):** ≈ **20.5–30 hafta (~5–7 ay)**.
- **Parallel (2–3 lane, single-writer korunarak):** W4–W7 0173 freeze sonrası paralel + W8 late-W4 overlap → calendar ≈ **13–18 hafta (~3–4.5 ay)**.
- **Kritik yol (uzatılamaz):** W1A → W1B → W3 (ledger) → W4. Bunlar seri; ledger (W3) tek başına en büyük kalem.

### 13.3 Downstream MVP'ler (inventory DIŞI — ayrı capability, kaba tahmin)

| MVP | Blok | Depends on | Efor | Est. hafta (kaba) |
|---|---|---|---|---|
| MVP-2 | Procurement/P2P (0140–0145) | MVP-1 freeze | L–XL | ~6–8 |
| MVP-3 | BOM & Routings (0193) | MVP-1 | M | ~3–4 |
| MVP-4 | Demand+MRP (0188/0189/0191) | MVP-2+3 | L | ~5–7 |
| MVP-6 | Integrated S&OP + Logistics (0190/0192/0183-0187) | MVP-2…5 | XL | ~8–12 |

> Bunlar inventory raporunun kapsamı dışında; sadece sıralama/ölçek görünürlüğü için. Kendi capability pack'lerinde ayrıca ölçülmeli.

### 13.4 Süreyi belirleyen kararlar
- **DEC-INV-17** (identity) → **RESOLVED**: Model A hedef, W1B **UNBLOCKED**.
- **DEC-INV-01/11** → **RESOLVED**: +Warehouse (W8 IN) + Pharma/Manufacturing (0174-0177 full + BOM) → **tam kapsam**; takvim üst banda (~parallel 15-18h+) yaklaşır.
- Kalan: **DEC-INV-03** (location) → W2 · **DEC-INV-06** (costing) → W3 valuation.

### 13.5 Full-block timeline (MVP-1…MVP-6) — Figure 4
Inventory (§13.2) tüm bloğun MVP-1 + MVP-5'i. Tüm Supply Planning bloğu için MVP bazında (parallel calendar, planning estimate):

| MVP | Blok | Başlar | Est. hafta | Kritik yol? |
|---|---|---|---|---|
| **MVP-1** | Foundation (inventory core: 0290+0173+0174-0177) | 0 | **12–13** | ★ |
| **MVP-5** | Warehouse (0178/80/81/82) | ~13 (0173 freeze) | 4–6 | overlap |
| **MVP-2** | Procurement/P2P (0140-0148) | ~13 (G1) | **6–8** | ★ |
| **MVP-3** | BOM (0193) | ~13 (G1) | 3–4 | MVP-2 ∥ |
| **MVP-4** | Planning (0188-0192) | ~21 (G2A/B) | **5–7** | ★ |
| **MVP-6** | Integrated + Logistics (0183-0187, S&OP) | ~28 (G3) | **8–12** | ★ |

**Rollup:**
- **Tüm blok (parallel):** ≈ **32–40 hafta (~8–10 ay)**. Kritik yol = MVP-1 → MVP-2 → MVP-4 → MVP-6.
- **Senin scope'un (Inventory + WMS = MVP-1 + MVP-5):** ≈ **13–19 hafta (~3–4.5 ay)** — Figure 4'te ◆ milestone.
- MVP-3 (BOM), MVP-5 (Warehouse) kritik yolda değil (paralel/overlap).
- **DEC-INV-01** (scope: core vs +WMS) → W8 dahil mi?
- **DEC-INV-11** (distribution vs pharma) → 0174–0177 tam mı, kısık mı; W4–W7 eforu.
- **DEC-INV-03** (location) → W2 var mı, ne kadar.
- **DEC-INV-06** (costing) → W3 valuation eforu.

---

## 14. BLOCK OWNED-OBJECT REGISTRY (Supply Planning Block, MOD-0140–0193)

> **Altitude:** Bu **block-level object registry** (SCQ-CB-DOC-006 muadili) — "hangi object var, sahibi hangi MOD, hangi seviye". **Tam per-field entity spec DEĞİL** — o, DEC-INV-17 kapandığına göre artık **module dokümanı** aşamasıdır (Module Data/API Spec, module-pack ile).
> **Scope işareti (2026-09-08 kararlarına göre):** `[core]` inventory çekirdeği · `[wms]` DEC-INV-01=+Warehouse ile IN · `[gxp]` DEC-INV-11=Pharma zorunlu · `[mfg]` DEC-INV-11=Manufacturing IN · `[P2]` sonraya · `[ext]` ayrı capability (bu inventory raporunun dışında, sıralama görünürlüğü için).

### 14.1 Product / Item / SKU Master — MOD-0290 (**Model A**, SoR)
| Object | Seviye | Durum | Amaç |
|---|---|---|---|
| `GlobalProduct` | Product | **var** | Canonical ürün kimliği (→ `ItemId`) |
| `ProductDefinitionRevision` | Product-def | **var** | Versioned tanım — hardening owner (base UoM/shelf-life/storage/retest/Lot-Serial flags) |
| `Gsku` | Global SKU | **var** | Pack-level SKU (→ `SkuId` aday); UoMConversion + GTIN owner |
| `Lsku` | Local/market SKU | **var** | Market-scoped SKU |
| `FinishedGood` | Marketed pack | **var** | Satılan/stoklanan birim; market GTIN |
| `UoMConversion` | — | **YOK→hardening** | Base↔pack dönüşümü (Gsku) |
| `ProductIdentifier` (GTIN/GS1) | — | **YOK→hardening** | Barkod/GTIN (Gsku + Lsku/FinishedGood) |
| `ShelfLife/StorageCondition/RetestDays` | — | **YOK→hardening** `[gxp]` | Raf ömrü/saklama/retest (PDR) |

### 14.2 Inventory Ledger — MOD-0173 (SoR çekirdek) `[core]`
| Object | Durum | Amaç |
|---|---|---|
| `InventoryTransaction / Movement` | yeni | Append-only stok gerçeği (SoR) |
| `MovementType` (canonical, sabit) | yeni | GR/GI/transfer/adjust/scrap + `[mfg]` GOODS_RECEIPT_PROD/ISSUE_PROD |
| `MovementReason` (MOD-0048 ext) | yeni | Sebep kodu |
| `InventoryDimension` | yeni | Uniqueness key (tenant/company/item/site/wh/loc/lot/serial/status/owner/project) |
| `InventoryBalance` (projection) | yeni | OnHand/Reserved/Available/InTransit/Blocked/QI |
| `StockStatus` (dimension) | yeni | AVAILABLE/QI/QUARANTINE/BLOCKED/IN_TRANSIT/DAMAGED/EXPIRED |
| `InventoryReservation` | yeni | Fiziksel hold (0172 ATP okur) |
| `InventoryValuation / CostLayer / CostingMethodPolicy` | yeni | Değerleme (DEC-INV-06) |
| `InventoryOwnershipType` | `[P2]` | Consignment/subcontract/3PL/project |

**MOD-0172 · Allocation & ATP/CTP** `[ext, O2C — mevcut/reserved]`: `AllocationDecision` · `ATPPromise` · `CTPPromise` · `AllocationPriority` — 0173 availability + reservation **okur**, ikinci balance tutmaz.

### 14.3 MOD-? · Shared Location/Storage Master (DEC-INV-03 = Option B) `[core]`/`[wms]`
| Object | Durum | Amaç |
|---|---|---|
| `Site / Plant` · `Warehouse` · `StorageLocation` · `Zone` · `Bin` | **YOK→greenfield (yeni shared master)** | Tek ortak lokasyon hiyerarşisi; 0173 balance + 0178 task + procurement + manufacturing **okur** (tek kaynak) |

> **Reconciliation (K6/K10 — measured):** Location Master ≠ `OrganizationUnit`. Platform'daki `OrganizationUnit` = organizasyon/raporlama yapısı (OrgUnitType=Department, ManagerPositionId, CostCenterCode) — fiziksel yer DEĞİL. `OrganizationUnit` ve `Position`, **serbest-metin `LocationCode` string** taşır — ama **validation/FK YOK** (`Clean()` edilip saklanan düz etiket; bugün hiçbir location entity'sine bağlı değil, çünkü master yok). **Location Master fiziksel yeri (site/warehouse/bin) tanımlar.** İlişki (öneri, mevcut kablolama değil): Location `LegalEntityId` taşır (DEC-INV-18); Location Master kurulunca `LocationCode` string'i ona **opsiyonel olarak bağlanabilir** (validation eklenerek); Warehouse opsiyonel OrgUnit/CostCenter soft-referansı verebilir. OrgUnit'i depo/raf için kullanmak iki olguyu birleştirir (yasak).

### 14.4 MOD-0174–0177 · Traceability & Quality (Pharma) `[gxp]`
| MOD | Object |
|---|---|
| 0174 | `Lot` (LotId+LotNumber, mfg/expiry/retest, CoA ref) · `SerialNumber` · `LotGenealogyLink` |
| 0175 | `QuarantineHold` · `ReleaseDecision` (e-imza) · `Disposition` · `StockStatusTransition` · `BlockReason` |
| 0176 | `ShelfLifeRule` · `ExpiryPolicy` · `FEFOAllocationConstraint` · `FEFOExceptionCase` |
| 0177 | `RecallCase` · `RecallDrillPlan` · `TraceForwardQuery` · `TraceBackwardQuery` · `EvidencePack` |

### 14.5 MOD-0178–0182 · Warehouse / WMS `[wms]`
| MOD | Object |
|---|---|
| 0178 | `WarehouseTask` (Putaway/Pick/Pack) · `ScanEvent` · `HandlingUnit/Pallet` |
| 0180 | `Wave` (pick wave) · `WavePlan` |
| 0181 | `CountEvent / CycleCount` · `CountAdjustment` |
| 0182 | `BarcodeLabel` · `ScanBinding` |

### 14.6 MOD-0140–0148 · Procurement / Purchasing `[ext]` (MVP-2 — ayrı capability)
| MOD | Object |
|---|---|
| 0140 | `Supplier` (onboarding) |
| 0145 | `SourcingEvent / RFQ` |
| 0141 | `Requisition` · `PurchaseOrder` (SoR) |
| 0142 | `GoodsReceipt / GRN` → 0173'e post |
| 0143 | `InvoiceMatch` (3-way) |
| 0144 | `Contract / ClauseLibrary` |

### 14.7 MOD-0193 · BOM & Manufacturing `[mfg]` (MVP-3)
| MOD | Object |
|---|---|
| 0193 | `BOM` · `BOMVersion` · `BOMComponentLine` · `Routing` · `RoutingStep` |

### 14.8 MOD-0188–0192 · Planning (MVP-4) `[ext]` · MOD-0183–0187 · Logistics (MVP-6) `[ext]`
| MOD | Object |
|---|---|
| 0188 | `DemandPlan / Forecast` |
| 0189 | `MRPRun` · `NetRequirement` · `ReplenishmentProposal` |
| 0191 | `SafetyStockPolicy` |
| 0192 | `CapacityPlan` |
| 0190 | `S&OPPlan` · `SignOff` |
| 0183-87 | `Shipment` · `POD` · `Carrier` · `LoadPlan` · `ReturnOrder/RMA` · `Claim` |

> **Sıradaki adım (module doküman aşaması):** DEC-INV-17 kapandı → artık **W1A/W1B/W3 için module-level Data/API Spec** yazılabilir (per-field, index, contract). Sıra: W1A (Model A identity + Gsku→GlobalProduct resolve) → W1B (0290 hardening, §8 tablosu) → W3 (0173, §4 + §14.2). Bunlar **module pack** ister (kod/dispatch değil, spec).

---

## 15. FULL BLOCK DEPENDENCY & PARALLELIZATION (Blueprint 8.1'e göre)

> Supply Planning Procure Cap Block'un **tamamını** bitirmek için gereken bağımlılıklar (Blueprint `Dependency Gate` sütunundan ölçüldü) + paralel gidecek iş akışları. **Legend:** `[INT]`=blok-içi modül · `[BB]`=backbone/platform (blok dışı, mevcut olmalı) · `[EXT]`=external SoR/provider (build dışı).

### 15.1 Granular dependency list (her modül ← gate)

| MOD | Modül | Blueprint dependency gate |
|---|---|---|
| **0290** | Product/Item/SKU | Data Contract Registry `[BB]` · Canonical ID (0040) `[BB]` · Audit Trail (0021) `[BB]` · ERP Core, PLM `[EXT]` |
| **0173** | Inventory Ledger | Data Contract Registry `[BB]` · SoR & Ownership Registry `[BB]` |
| **0174** | Lot/Batch/Serial | Canonical ID (0040) `[BB]` · Data Contract Registry `[BB]` |
| **0175** | Quarantine | **0174** `[INT]` · Workflow Designer (0023) `[BB]` |
| **0176** | Expiry/FEFO | **0174** `[INT]` · Demand Planning **0188** `[INT]` *(bizde consumer-yön, DEC: base=0174)* |
| **0177** | Recall | **0174** `[INT]` · Evidence Linking (0029/0030) `[BB]` |
| **0178** | Putaway/Pick/Pack | **0173** `[INT]` · Data Contract Registry `[BB]` |
| **0179** | 3PL Integration | API Gateway `[BB]` · Integration Monitoring `[BB]` |
| **0180** | Wave Planning | **0178** `[INT]` |
| **0181** | Cycle Counting | **0173** `[INT]` |
| **0182** | Barcode/RFID | **0178** `[INT]` · Device integration `[EXT]` |
| **0172** | Allocation & ATP/CTP | Demand Planning **0188** `[INT]` · Inventory Ledger **0173** `[INT]` |
| **0140** | Supplier Onboarding | SSO/MFA `[BB]` · Workflow (0023) `[BB]` · Doc Mgmt (0029) `[BB]` · Policy Library `[BB]` · KYC provider `[EXT]` |
| **0141** | Requisition & PO | API Gateway `[BB]` · Data Contract Registry `[BB]` · Workflow `[BB]` |
| **0142** | Receiving/GRN | **0141** `[INT]` · Data Contract Registry `[BB]` → **posts 0173** |
| **0143** | Invoice 3-Way Match | **0142** `[INT]` · AP `[EXT/Treasury]` · Workflow `[BB]` |
| **0144** | Contracting | CLM · Clause Library · Doc Mgmt `[BB]` |
| **0145** | Sourcing (RFQ) | **0140** `[INT]` · Workflow `[BB]` |
| **0146** | Payment Run Support | Payments & Approvals `[EXT/Treasury]` · AP `[EXT]` |
| **0147** | Supplier Performance | Risk Register `[BB]` · Metric Registry `[BB]` · **0140** `[INT]` |
| **0148** | Supplier Portal | **0140** `[INT]` · API Gateway `[BB]` |
| **0188** | Demand Planning | DWH/Lakehouse (0063) `[BB]` · Metric & Semantic Registry `[BB]` |
| **0189** | MRP & Replenishment | **0188** `[INT]` (+ okur: **0173**, **0193**, **0141/0142**) |
| **0190** | S&OP Workflow | Workflow `[BB]` · **0188** `[INT]` |
| **0191** | Safety Stock Opt. | **0189** `[INT]` · Scenario Modeling `[BB]` |
| **0192** | Capacity Planning | **0188** `[INT]` · supply-constraints data `[BB]` |
| **0193** | BOM & Routings | Data Contract Registry `[BB]` · Change Control (0209) `[BB]` |
| **0183** | Shipment/POD | Event Bus `[BB]` · Integration Monitoring `[BB]` |
| **0184** | Carrier Mgmt | Data Contract Registry `[BB]` |
| **0185** | Routing & Load | **0184** `[INT]` |
| **0186** | Reverse Logistics | Returns/RMA · **0183** `[INT]` |
| **0187** | Claims Mgmt | **0183** `[INT]` |

**Backbone prerequisites (hepsi hazır olmalı — WAVE 0):** Data Contract Registry (~0003) · SoR & Ownership Registry · Canonical ID (0040) · Workflow (0023) · Audit (0021) · Doc Mgmt/Evidence (0029/0030) · Reference Data (0048) · Change Control (0209) · DWH/Lakehouse + Metric Registry (0063) · API Gateway · Event Bus · Integration Monitoring · RBAC (0018) · SSO/MFA.
**External SoR/provider (build dışı, contract seam):** ERP Core · PLM · KYC provider · CLM · AP/Payments (Treasury) · Device (barcode/RFID) · 3PL.

### 15.2 Paralel iş akışları (single-writer korunarak, K15)

```
WAVE 0  BACKBONE (mevcut olmalı — hepsi paralel/hazır)
        │  ⛔ GATE G1: Product & Inventory Truth
        ▼
LANE A  FOUNDATION (MVP-1) — kritik, tek yazıcı, SIRALI:
        0290 → 0173 → {0174 → 0175 ∥ 0176 ∥ 0177}   +  Warehouse-core (0178→0180/0182 ∥ 0181)
        │  ⛔ 0173 movement + 0290 identity contract FREEZE → aşağısı paralel açılır
        ▼
   ┌──────────────── 3 PARALEL LANE (MVP-1 freeze sonrası) ────────────────┐
   │ LANE B  PROCUREMENT (MVP-2)          │ LANE C  BOM (MVP-3)  │           │
   │ 0140→0145 ∥ 0141→0142→0143 ∥ 0144    │ 0193                 │           │
   │ ∥ 0147 ∥ 0148 ∥ 0146                  │ (mfg ise)            │           │
   │ 0142 GRN → 0173 (consumer seam)      │                      │           │
   └───────────────┬──────────────────────┴──────────┬───────────┘           │
                   │ (B ⇄ C paralel; ikisi de D'yi besler)                    │
                   ▼                                                          │
LANE D  PLANNING (MVP-4)  0188 → {0189 → 0191} ∥ 0190 ∥ 0192                  │
        0188 Demand FREEZE → 0172 ATP ∥ 0176 FEFO ∥ 0189 ∥ 0192 (consumers)  │
                   │                                                          │
LANE E  WAREHOUSE EXEC (MVP-5)  ← 0173 freeze sonrası, late-MVP-4 ile OVERLAP │
                   ▼                                                          │
LANE F  LOGISTICS + INTEGRATED (MVP-6)  0183→{0185∥0186∥0187} ∥ 0184 ; 0190/0192 ; 0147/0148
```

**Paralel-safe kuralları (ölçülen gate'lerden):**
- **0173 = shared seam** → tek yazıcı. Ona **post eden** her şey (0142 GRN, 0178 WMS, 0181 count) contract freeze'den sonra **consumer** olarak paralel gider.
- **0188 Demand = shared seam** → freeze sonrası 0172/0176/0189/0192 paralel tüketir.
- **LANE B (Procurement) ⇄ LANE C (BOM)** MVP-1 freeze'den sonra **tam paralel** (disjoint scope).
- **LANE A içi seri** (0290→0173→0174) — kritik yol, bölünmez.
- **Aynı anda 2+ writer YASAK** olan seam'ler: 0173 ledger · 0290 identity · 0188 demand · Workflow/permission tanımları · gateway route family.

### 15.3 Şema — Figure 3 (full block dependency + parallel lanes).

---

## 16. SPEC-READINESS GATE (module-spec yazmaya geçiş DoR'u)

### 16.1 Kapanan kararlar (spec'e hazır)
DEC-INV-01 (scope +WMS) · 11 (pharma+mfg) · 17 (Model A) · **18 (Tenant+LE scoping)** · **19 (vNext SoR + per-LE dış entegrasyon)** · SkuId (polimorfik direction) · 02/05/07/08/10/12/13/16 (mimari öneriler).

### 16.2 Kalan açık — tüm DEC RESOLVED
| Nokta | Neyi bloklar | Durum |
|---|---|---|
| DEC-INV-03 Location owner | W2, W3 | ✅ Option B shared master (MOD-# atama pending) |
| DEC-INV-06 Costing | W3 valuation | ✅ per-LE/product, fallback MovingAvg |
| DEC-INV-14/15 Lot/Transaction model | W4/W3 | ✅ owner onayladı |
| **Stockable-level policy** | W1A pin | ⏳ **tek kalan** — spec-level, W1A'da pinlenir (DEC değil) |

### 16.3 Governance/process prerequisite (spec'in "kabı")
- **Delivery Capability Pack** (supply-chain inventory) · **supply-chain domain config** · **MOD-0173 module pack** (`approved/ready-for-dev`) · **registry adoption** (0173-0193 satırları, DCP-002 gate).

### 16.4 Verdict
```
W1A spec YAZILABILIR  → identity + Gsku→GlobalProduct resolve + Tenant/LE scoping + polimorfik SkuId hepsi net.
W1B spec YAZILABILIR  → 0290 hardening (§8, Model A hedef).
W3 (0173) spec         → DEC-INV-03/06/15 W1A/W1B sırasında kapanmalı (paralel).
Adapter (DEC-19 dış entegrasyon) → follow-up slice; data model hooks şimdi konur.
```

---

## 17. CONTRACT FREEZE REGISTER (single-writer seam sırası)

| Contract | Donar (freeze) | Açtığı consumer'lar |
|---|---|---|
| **0290 Product/SKU identity + resolve** (ItemId/SkuId/SkuLevel, Gsku→GlobalProduct) | W1A sonu | 0173, 0174, CRM StrategyTemplate |
| **Location model** (Site→WH→StorLoc→Zone→Bin) | W2 sonu | 0173 dimension, 0178 WMS |
| **0173 movement + availability** (transaction schema, StockStatus, reservation, availability query) | W3 sonu | 0142 GRN, 0178 WMS, 0181 count, **0172 ATP**, 0189 MRP |
| **0188 demand plan** | MVP-4 | 0172, 0176 FEFO, 0189, 0192 |
| **Inventory external-integration contract** (inbound/outbound/reconcile, SourceSystem) | follow-up | external-integrated LE'ler (DEC-19) |

> **Kural (K15):** Bir contract freeze edilmeden **tüketici lane paralel başlamaz**. Freeze = versioned + published; freeze sonrası consumer'lar paralel, ama **producer tek yazıcı**.

---

## 18. PARALLEL EXECUTION PLAN (paralel gidebilen bloklar)

Hangi lane'ler aynı anda çalışabilir, hangi gate açar, hangi seam tek-yazıcı (K14/K15 — paralel-safe ölçülür, varsayılmaz).

| # | Window (gate açar) | Paralel gidebilen bloklar | Tek-yazıcı seam (paralel YASAK) |
|---|---|---|---|
| **1** | WAVE 0 Backbone (başta) | Tüm backbone bileşenleri paralel hazırlanabilir: DataContractRegistry · CanonicalID · Workflow · Audit · Evidence · DWH/Metric · Gateway · EventBus | — (farklı modüller) |
| **2** | MVP-1 içi · **0174 freeze sonrası** | **0175 Quarantine ∥ 0176 FEFO ∥ 0177 Recall** (üçü de sadece 0174'e bağlı) | `0290 → 0173 → 0174` **seri** (kritik yol) |
| **3** | **G1 · 0290 identity + 0173 movement freeze** | **MVP-2 Procurement ∥ MVP-3 BOM ∥ MVP-5 Warehouse** (3 lane, disjoint scope) + 0173 consumer'ları (0142 GRN · 0178 WMS · 0181 count) | **0173 ledger** tek yazıcı |
| **4** | **0188 Demand freeze** | **0172 ATP ∥ 0176 FEFO ∥ 0189 MRP ∥ 0192 Capacity** (hepsi demand tüketir) | **0188 demand** tek yazıcı |
| **5** | **G3 · MVP-4 sonrası** | MVP-6: **0185 Load ∥ 0186 Reverse ∥ 0187 Claims** + 0190 S&OP + 0147/0148 Supplier | **0183 Shipment** (0186/0187 buna bağlı) |

**Tek-yazıcı seam register (K15 — aynı anda 2+ writer YASAK):** `0173 ledger` · `0290 identity` · `0188 demand` · `Workflow/permission tanımları` · `gateway route family` · `shared registrations`.

**WIP kuralı:** default her shared seam için **1 aktif writer Agent Lane**; paralel işler **ayrı worktree + disjoint scope**. Consumer lane'ler producer contract **freeze**'inden sonra paralel açılır.

**Özet — kaç lane aynı anda:** MVP-1 içinde ≤3 (0175/0176/0177) · G1 sonrası **3 büyük lane** (Procurement + BOM + Warehouse) · Demand sonrası **4 consumer** · MVP-6'da 3 logistics alt-lane. Tek-yazıcı kritik yol (0290→0173→0174→…) hiçbir zaman bölünmez.

---

## 19. MVP → DEVELOPER ALLOCATION (paralel geliştirme planı)

Her MVP bir developer'a (veya küçük lane-team'e) atanır. **Bir developer = bir MVP lane = ayrı worktree = disjoint scope** (single-writer, K15). Aşağıdaki kaç developer'ın aynı anda paralel çalışabileceğini gate'lere göre gösterir.

| MVP | Modüller | Başlar (gate) | Paralel-safe with | Önerilen dev | Tek-yazıcı kısıt |
|---|---|---|---|---|---|
| **MVP-1** Foundation | 0290 · 0173 · 0174-0177 · Location | başta | *içinde* 0175∥0176∥0177 | **2–3** (kritik path serial) | `0290→0173→0174` zinciri tek writer |
| **MVP-2** Procurement | 0140-0148 | **G1** (0290+0173 freeze) | MVP-3, MVP-5 | **1–2** | ayrı worktree |
| **MVP-3** BOM | 0193 | **G1** | MVP-2, MVP-5 | **1** | — |
| **MVP-5** Warehouse | 0178-0182 | 0173 movement freeze | MVP-2, MVP-3, late MVP-4 | **1** | 0173'e post (consumer) |
| **MVP-4** Planning | 0188-0192 | MVP-2+3 stabil (G2A/B) | late MVP-5 | **1–2** | `0188 demand` tek writer |
| **MVP-6** Integrated | 0183-0187 · S&OP · supplier | MVP-4 sonrası (G3) | *içinde* 0185∥0186∥0187 | **2–3** | `0183 shipment` tek writer |

### 19.1 Eşzamanlı developer sayısı (staffing over time)

```
wk 0–13   MVP-1                       → 2–3 dev   (kritik path serial'i sınırlar)
wk 13–21  MVP-2 ∥ MVP-3 ∥ MVP-5        → 3–4 dev   ★ PEAK — 3 developer ayrı MVP
wk 21–28  MVP-4 ( + biten MVP-5)       → 2–3 dev
wk 28–40  MVP-6                        → 2–3 dev
```

### 19.2 Kritik kurallar (developer ataması)
- **G1'den ÖNCE** gerçek 3'lü paralellik yok: MVP-1 kritik yolu (`0290→0173→0174`) **seri** — buraya fazla developer atamak hızlandırmaz (Brooks). En fazla 2–3 dev (biri 0290, biri 0173, biri 0174 sonrası quality lane).
- **G1'den SONRA** temiz 3-developer paralelliği açılır: **Dev-1 → MVP-2**, **Dev-2 → MVP-3**, **Dev-3 → MVP-5**. Her biri disjoint, ayrı worktree.
- İki developer **aynı shared seam'e** (0173/0290/0188/gateway/permission) yazamaz — o seam'in tek sahibi vardır; diğerleri contract freeze sonrası **consumer**.
- Consumer lane'ler (0142 GRN, 0178 WMS, 0181 count → hepsi 0173'e post) 0173 **freeze sonrası** paralel; ama 0173 ledger'ın kendisi hâlâ tek writer.

> **Sonuç:** "Her developer ayrı MVP" modeli **G1 kapısından sonra** tam çalışır (3 dev paralel). Öncesinde (MVP-1) takım kritik foundation'a odaklanır — bu dönem hızlandırılamaz, sadece 2–3 dev absorbe eder.

---

## 20. GATE DEFINITIONS & EXIT CHECKLISTS

**Gate = iki MVP arasındaki geç/geçme kontrol noktası.** Bir MVP "bitti" demek için o gate'in **çıkış kriterleri PASS** olmalı. Gate PASS olmadan bir sonraki (bağımlı) MVP başlamaz — şemalardaki kırmızı kesikli çizgi = gate freeze noktası.

| Gate | Anlamı | Çıkışı |
|---|---|---|
| **G1** | Product & Inventory Truth Ready | MVP-1 Foundation |
| **G2A** | Procure-to-Receive Ready | MVP-2 Procurement |
| **G2B** | BOM Structure Ready | MVP-3 BOM |
| **G3** | Replenishment Planning Ready | MVP-4 Planning |
| **G4** | Warehouse Execution Ready | MVP-5 Warehouse |
| **G5** | Integrated Supply Chain Ready | MVP-6 |

### 20.1 G1 — EXIT CHECKLIST (MVP-1 kapanış; hepsi PASS → MVP-2/3/5 fan-out açılır)

**Kimlik — MOD-0290 (Model A):**
- [ ] Canonical kimlik resolve oluyor: `ItemId→GlobalProduct`, `SkuId+SkuLevel→Gsku\|Lsku\|FinishedGood`
- [ ] `Gsku→GlobalProduct` resolve contract çalışıyor (D-SKU-LINK)
- [ ] Base UoM + UoM conversion resolve oluyor
- [ ] Identity contract **versioned + frozen** (published)

**Stok gerçeği — MOD-0173:**
- [ ] Opening golden flow: `+100 post → InventoryTransaction persist (L3) → InventoryBalance=100 → reload stabil`
- [ ] `Issue -20 → 2. immutable transaction → Balance=80, 1. movement değişmez (history)`
- [ ] **Idempotency:** aynı `IdempotencyKey` → 2. posting YOK
- [ ] **Negative stock** policy enforce (DEC-INV-12: default prohibited)
- [ ] **StockStatus dimension** çalışıyor (AVAILABLE/QI/QUARANTINE/BLOCKED/IN_TRANSIT)
- [ ] **Reservation:** reserve → `Available = OnHand − Reserved`
- [ ] Movement schema + **availability query** contract **versioned + frozen**

**Scoping & security:**
- [ ] **Tenant + Legal-Entity** izolasyonu enforce (DEC-INV-18); cross-LE leak bloklu
- [ ] Tüm mutation'larda **server-side RBAC**
- [ ] Her posting'de **audit event + correlation ID**

**Data quality (STOP-SHIP):**
- [ ] Hiçbir yerde rakip ürün/stok gerçeği yok (tek SoR)
- [ ] External-integrated LE'ler için `SourceSystem`/`ExternalRef` alanları mevcut (DEC-INV-19 hook)

**Evidence seviyesi:**
- [ ] State-changing akışlar **E4** ile ölçüldü: UI + API response + persisted record + audit **birlikte**

> **G1 = 0290 identity + 0173 movement/availability contract'ları donduğu (frozen) an.** Bu checklist PASS olmadan procurement/warehouse/BOM developer'ları başlatılmaz.

### 20.2 Diğer gate'ler — çıkış kriteri (özet)

- **G2A (Procure-to-Receive):** `PO → GRN → 0173 inventory posting → invoice 3-way match` golden flow PASS; L3; idempotent; GRN gerçekten 0173'e post ediyor; approval/audit.
- **G2B (BOM Structure):** versioned BOM/routing L3 persist; optimistic concurrency; controlled effective-state transition; change-control audit.
- **G3 (Replenishment Planning):** `forecast → net against inventory → replenishment proposal` reproducible snapshot; idempotent proposal; 0141'e handoff; explainable netting inputs.
- **G4 (Warehouse Execution):** `putaway/pick/pack/count` golden flows; per-row/server authz; idempotent scans; her hareket 0173'e post (ikinci balance yok); partial-failure marker.
- **G5 (Integrated Supply Chain):** cross-module e2e; versioned plan snapshots; shipment lifecycle boyunca correlation zinciri; source SoR'lara reconciliation; regression suite PASS.

> Her gate **CT acceptance** ile kapanır (agent PASS ≠ gate PASS, K13). Gate PASS = independent verification + required evidence seviyesi + records updated.

---

## 21. PARALLEL DEVELOPMENT CONTRACT & RULES

Amaç: MVP'ler **çakışmadan paralel** geliştirilsin. Sequence değil — her developer'ın uyacağı **bağımlılık kontratı + kural seti**.

### 21.1 Global kurallar (ihlal = merge/rework riski)

1. **Single-writer per seam (K15):** Bir shared contract'ın **tek sahibi** vardır; ona sadece o modül yazar. `0173 ledger · 0290 identity · 0188 demand · Location · Workflow/permission · gateway route family` = tek yazıcı.
2. **Consume only FROZEN (K16):** Bir başkası bir contract'ı ancak **versioned + published (frozen)** olduktan sonra tüketir. Frozen değilse consumer lane başlamaz.
3. **No shadow copy (K6):** Kimse başkasının SoR'unun **ikinci kopyasını** tutmaz. Warehouse ikinci balance tutmaz; procurement shadow stock tutmaz; ATP ikinci stok tutmaz → hepsi **contract üzerinden** okur/post eder.
4. **Cross-module = published contract only:** Modüller birbirinin DB'sine/iç tipine dokunmaz; sadece **published API/event** (contract bundle) ile konuşur.
5. **Disjoint scope + ayrı worktree:** Her paralel MVP kendi worktree + kendi path'lerinde; overlapping dosya yok.
6. **Producer freezes before consumers start:** Örn. 0173 movement+availability freeze edilmeden 0142/0178/0181/0172 **consumer** olarak başlamaz.
7. **Invent yok (K12):** Eksik contract uydurulmaz; contract yoksa fail-closed → CT'ye bildir.

### 21.2 Per-MVP dependency contract (owns / consumes / must-NOT / start-gate)

| MVP | OWNS (tek-yazıcı SoR) | CONSUMES (frozen) | MUST-NOT (yasak) | Start gate |
|---|---|---|---|---|
| **MVP-1 Foundation** | PRODUCT-MASTER-BUNDLE (0290) · INVENTORY-BUNDLE (0173) · TRACE (0174) · LOCATION (master) · QUARANTINE/FEFO/RECALL (0175/76/77) | backbone (0018/0021/0040/0048/0003) | — | backbone hazır |
| **MVP-2 Procurement** | Supplier · PO (0141) · GRN (0142) · InvoiceMatch (0143) | **PRODUCT-MASTER-BUNDLE (frozen)** · **INVENTORY-BUNDLE (frozen, post GR)** · LOCATION | 0173 balance'a **doğrudan yazma** (sadece movement API) · ürün kimliği uydurma · shadow stock | **G1** |
| **MVP-3 BOM** | BOM · Routing (0193) | **PRODUCT-MASTER-BUNDLE (frozen)** · Change Control (0209) | ürün kimliği/inventory'e yazma | **G1** |
| **MVP-5 Warehouse** | WarehouseTask · Scan · Wave · CycleCount (0178/80/81/82) | **INVENTORY-BUNDLE (movement, frozen)** · LOCATION · TRACE | **ikinci balance tutma** (movement API ile post) | 0173 movement freeze |
| **MVP-4 Planning** | DemandPlan (0188) · MRP/Proposal (0189) · SafetyStock (0191) · Capacity (0192) | INVENTORY **availability query** · PRODUCT · BOM · DWH (0063) | **PO yaratma** (0141'e handoff) · stok tutma | G2A + G2B |
| **MVP-6 Logistics** | Shipment/Carrier/Load/Reverse/Claims (0183-87) · Supplier Perf/Portal (0147/48) | Warehouse · Shipment · Supplier | source SoR'ları override | G3 |
| **MOD-0172 ATP** (consumer) | AllocationDecision/ATP/CTP | **INVENTORY availability + reservation (frozen)** · Demand | **ikinci stok balance** | 0173 + 0188 freeze |

### 21.3 Shared-seam single-writer register

| Seam / contract | Tek sahip (writer) | Consumer'lar (frozen sonrası okur/post) |
|---|---|---|
| `PRODUCT-MASTER-BUNDLE` (identity/SKU/UoM) | **MOD-0290** | 0173, 0174, MVP-2, MVP-3, CRM |
| `LOCATION` (site→bin) | **Location Master** | 0173, 0178, MVP-2, MVP-4 |
| `INVENTORY-BUNDLE` (movement/balance/availability/reservation) | **MOD-0173** | 0142, 0178, 0181, 0172, 0189, 0176 |
| `TRACE-BUNDLE` (lot/serial) | **MOD-0174** | 0175, 0176, 0177 |
| `Demand plan` | **MOD-0188** | 0172, 0176, 0189, 0192 |
| Workflow / permission / gateway route | ilgili platform sahibi | herkes okur |

> **Developer'a tek cümle:** "Kendi MVP'nin OWNS listesine yaz; CONSUMES listesini **sadece frozen** oku; MUST-NOT'a asla dokunma; başka seam'e yazman gerekiyorsa o seam'in sahibinden **contract iste**, kod yazma." Bu kurallara uyulursa 3 developer G1 sonrası **çakışmasız paralel** gider.

---

## 22. 6-WAY PARALLEL STRATEGY (Contract-First)

**Hedef (yönetici direktifi):** 6 MVP mümkün olduğunca paralel yürüsün.

### 22.1 Dürüst gerçeklik (K9)
- **"6 MVP 1. günden implementasyon paralel" MÜMKÜN DEĞİL:** MVP-2/3/5 → 0290+0173 contract'ı olmadan başlayamaz; MVP-4 → MVP-2+3'e, MVP-6 → hepsine bağlı. Zorlanırsa collision + duplicate-truth + rework (K15/K16).
- **AMA paralelliği açan şey implementasyon değil, CONTRACT'tır.** Dondurulmuş arayüz + mock varsa, herkes ona karşı paralel geliştirir.

### 22.2 Nasıl — 3 faz

| Faz | Ne | Kim | Süre |
|---|---|---|---|
| **Faz 0 — Contract Freeze Sprint** | Tüm cross-MVP contract'ları tanımla + **dondur** + **mock** yaz: PRODUCT-MASTER · LOCATION · INVENTORY (movement/availability/reservation/stockstatus) · TRACE · ATP · GRN event · BOM · Demand | 1-2 architect + module owner'lar | ~2-3 hafta |
| **Faz 1 — 6 MVP paralel** | 6 takım eşzamanlı; herkes kendi modülünü OWNS, başkasını **mock üzerinden** tüketir | 6 dev/lane | build boyunca |
| **Faz 2 — Progressive integration** | Gerçek implementasyon indikçe mock→gerçek: 0290/0173 (G1) → MVP-2/3/5; 0188 → MVP-4; … | takımlar + CT | dependency sırasında |

### 22.3 Güvenli yapan kurallar
1. **Contract churn = katil (K16):** Faz-0'da contract donunca değişmez; değişirse tüm consumer rework. Faz-0 kalitesi her şey.
2. **Mock contract'a sadık** olmalı.
3. **Single-writer hâlâ geçerli:** contract'ı + gerçek implementasyonu tek sahip yazar; consumer sadece mock'a karşı build eder.
4. **"Paralel build" ≠ "paralel done":** Gate'ler (G1-G5) **kabul/entegrasyon** kapısıdır; özellikle **data-quality mock ile kanıtlanamaz** → gerçek entegrasyon dependency sırasında.

### 22.4 Verdict
| Yorum | Cevap |
|---|---|
| 6 MVP 1. günden **implementasyon** paralel | ❌ Hayır (hard gate, rework) |
| 6 MVP **contract-first + mock** ile paralel **build** | ✅ Evet — Faz-0 sonrası |
| 6 MVP paralel **kabul/entegrasyon** | ❌ Hayır — gate'ler dependency sırasında |

**Takvim etkisi:** naive "MVP-1 bitince fan-out" (~13h bekle) yerine Faz-0 (~3h) sonrası 6 takım başlar → tüm blok **~32-40h yerine ~24-30h** — *yalnız contract stabil kalırsa.*

> **Maliyet/karar:** "6 paralel" = **"önce contract'lara ciddi yatırım (Faz-0)"** kararı. Faz-0 zayıf yapılırsa 6-yönlü paralel avantajı rework'e döner. Görsel: **Figure 5**.
