# DEC-INV-17 — Canonical Inventory Item/SKU Identity Reconciliation (Evidence-Backed Decision Report)

> **Rol:** CONTROL TOWER · **Tur:** Profile C read-only reconciliation (kod/entity/pack/dispatch YOK) · **Tarih:** 2026-09-07
> **Kapsam:** SADECE DEC-INV-17. Yeni genel inventory analizi yok.
> **Amaç:** MOD-0290 içindeki iki paralel product modelinin GERÇEK repo kullanımını ölç → MOD-0173'ün hangi canonical Item/SKU contract'ını tüketeceğini evidence ile belirle.
> **Kural (uygulandı):** isim benzerliğiyle ownership varsayılmadı; her iddia grep/consumer/route/collection evidence'ıyla (K2).

---

## ⚠️ Önemli düzeltme (K2/K9 — rubber-stamp yok)

Görev "B gereksiz" varsayımıyla geldi. **Ölçüm bunu DESTEKLEMİYOR:** Model B (`Product`) canlı, persist edilmiş, gateway'de açık **ve cross-service tüketiliyor** (CRM Segmentation + Auth seed). Kod yorumu onu **"MOD-0290-FU02 — Product master aggregate. SoR is MOD-0290"** olarak tanımlıyor. Bu yüzden bu, "A'yı seç" değil, **gerçek bir dual-SoR uzlaştırma** kararıdır. Rapor B'yi ölçüp rolünü netleştiriyor.

---

## 1. CURRENT-STATE MODEL MAP

| | **Model A** — Canonical Identity / SKU Coding | **Model B** — Brand/Product Master |
|---|---|---|
| Zincir | `GlobalProduct → ProductDefinitionRevision → Gsku → Lsku → FinishedGood` | `Product` (opsiyonel `BrandId` → Brand) |
| MOD/FU | MOD-0290 SKU coding foundation (DCP-004-mod-0290-sku-coding) | **MOD-0290-FU02** (kod yorumu: "SoR is MOD-0290") |
| Collections | `mdm_global_products`, `mdm_product_definition_revisions`, `mdm_gskus`, `mdm_lskus`, `mdm_finished_goods`, `mdm_code_reservations`, `mdm_canonical_code_counters` | `mdm_products` (+ ix tenant_brand, tenant_code, tenant_archived_status) |
| Gateway routes | `/api/global-products`, `/api/gskus`, `/api/lskus`, `/api/finished-goods` | `/api/mdm/products` |
| Kimlik/kod | `CanonicalCode` + `CodeReservation` + counter (governed coding) | `ProductCode` (serbest), `ExternalReferences` |
| Lifecycle | `ProductIdentityLifecycleStatus` (Draft…) + AuditIntent (her seviyede) | `ProductStatus` (Draft) + archive |
| Attribute zenginliği | **ZAYIF** — GlobalProduct: code+name+lifecycle; PDR: revision+lifecycle; Gsku: `PackQuantity`+`PackUomCode`+PackApplicability | **ZENGİN** — DosageForm, Strength, PackSize, `UnitOfMeasure`, ATCCode, TherapeuticAreaId, Indications, EffectiveFrom/To |
| CRUD yüzeyi | drafts + code-reservations + selector (governed create) | tam CRUD (GET/POST/PUT/archive) |

**Seviye semantiği (ölçülen alanlardan):**
- `GlobalProduct` = global **product** kimliği (canonical code).
- `ProductDefinitionRevision` = GlobalProduct'ın versioned tanımı (`GlobalProductId` taşır).
- `Gsku` = **Global SKU** (pack seviyesi: PackQuantity/PackUomCode) — `ProductDefinitionRevisionId` taşır, **doğrudan `GlobalProductId` YOK**.
- `Lsku` = **Local/market SKU** (`GskuId` + `MarketCode`).
- `FinishedGood` = pazarlanan **finished good** (`GskuId`).
- `Product` (B) = pharma/brand **product master** — attribute'lu ama **SKU/pack alt-yapısı YOK**, lot/serial/shelf-life YOK.

---

## 2. CONSUMER / USE-CASE MATRIX (ölçülen, non-MDM)

| Entity | Non-MDM consumer dosya | Başlıca consumer'lar | Amaç |
|---|---|---|---|
| **Gsku** | **72** | Diten.Platform (**44** — verified reference catalog), CrmService (**19**, StrategyTemplate), frontend | Cross-service canonical **SKU** reference; StrategyTemplate ProductLines (SKU%) |
| **GlobalProduct** | **46** | CrmService StrategyTemplate, MOD-0162 (ConceptNode ExternalRef=Global Product), MOD-0018-FU16, frontend | Cross-service canonical **product** reference |
| **Lsku** | 7 | frontend + MDM chain | Market SKU |
| **FinishedGood** | 4 | frontend + MDM chain | Marketed good |
| **ProductDefinitionRevision** | 2 | çoğunlukla MDM-içi | Versioned definition |
| **Product (B)** | canlı | **CRM Segmentation** (`MdmSegmentProductReferenceValidator`, `SegmentAttributeValueSource`), **Auth seed** (permissions), frontend Products/Brands UI | Segment attribute value source (targeting); brand-product master |

**Kanıt (kod):**
- CRM StrategyTemplate (MOD-0167 FU04): *"ProductLines = MDM **GlobalProduct + Gsku**, proven cross-service before any write"*; validator `/api/global-products/{id}` + `/api/gskus/{id}`.
- CRM Segmentation: `MdmSegmentProductReferenceValidator` + `SegmentAttributeValueSource` → `/api/mdm/products` (**Model B**).
- → **CRM her iki modeli de tüketiyor, farklı amaçlarla.**

---

## 3. DUPLICATE / OVERLAP ANALYSIS

| Boyut | Bulgu |
|---|---|
| **Product-level overlap** | `GlobalProduct` (A) ve `Product` (B) **ikisi de "product" SoR** iddiasında; ikisi de MOD-0290 etiketli. |
| **Ölçülü FK var mı?** | **YOK.** `Product`(B) → GlobalProductId=0; `GlobalProduct` → ProductId=0. `BrandProductExternalReference` = dış-sistem xref, A↔B linki değil. |
| **Attribute vs identity ayrışması** | Attribute'lar (UoM, DosageForm, Strength, PackSize) **B'de**; canonical identity + SKU/pack alt-yapısı **A'da**. İki yarım, bağlı değil. |
| **SKU alt-yapısı** | **Sadece A'da** (Gsku/Lsku/FinishedGood). B'nin SKU'su yok → B **SkuId sağlayamaz**. |
| **Duplicate-truth riski** | **YÜKSEK** — iki paralel product master, cross-service tüketilen, FK'sız. CRM Segmentation(B) ile StrategyTemplate(A) farklı "product" gerçeğine bağlı. |
| **Intra-A gap** | `Gsku` doğrudan `GlobalProductId` taşımıyor (D-SKU-LINK) — SKU→Product traversal PDR üzerinden. |

---

## 4. RECOMMENDED CANONICAL PRODUCT → ITEM → SKU HIERARCHY

**Model A canonical omurga (evidence: cross-service, platform-verified, governed coding):**

```
GlobalProduct            = PRODUCT   (material/product identity — canonical code)
   └─ ProductDefinitionRevision   (versioned product definition — attribute taşıyıcı)
        └─ Gsku          = ITEM / GLOBAL SKU   (pack-level stock-keeping template)
             └─ Lsku     = LOCAL SKU   (market-scoped: MarketCode)
                  └─ FinishedGood = MARKETED PACK   (satılan/stoklanan birim)
```

**Model B (`Product`) = commercial/brand attribute master** — canonical identity SoR **değil**; ya A'ya bağlanır (reference) ya attribute'ları A'ya taşınır (§7).

---

## 5. EXACT MAPPING — mevcut entity → hiyerarşi + MOD-0173 referansları

| Inventory kavramı | Canonical aggregate (öneri) | Gerekçe (evidence) |
|---|---|---|
| **MOD-0173 `ItemId`** | **`GlobalProduct`** (Model A) | Cross-service canonical product ref; B SKU sağlayamadığı için ItemId de A'da tutulmalı (tek SoR straddle yok) |
| **MOD-0173 `SkuId`** | **`Gsku`** (global scope) **veya** **`Lsku`/`FinishedGood`** (market/pazar scope) — **DEC-INV-01/11'e bağlı** | Stockable SKU **yalnız A zincirinde** var; global mi market mi = scope kararı |
| Product level | GlobalProduct | — |
| Item / SKU level | Gsku | Pack template |
| Local SKU | Lsku | MarketCode |
| Finished Good | FinishedGood | Marketed |

> **NET:** SKU identity **kesinlikle Model A**'dan gelir (B'de SKU yok). ItemId de A ile hizalanır. **SkuId'nin tam seviyesi (Gsku vs Lsku/FinishedGood) DEC-INV-01/11 kapanmadan kesinleşmez.**

---

## 6. DEC-INV-17 — RECOMMENDED DECISION

```
DEC-INV-17 — Canonical Inventory Item/SKU Identity
Recommendation:
  1. MOD-0173 canonical identity SoR = Model A (GlobalProduct→PDR→Gsku→Lsku→FinishedGood).
     ItemId → GlobalProduct.  SkuId → Gsku|Lsku|FinishedGood (seviye DEC-INV-01/11).
  2. Model B (Product) canonical inventory identity DEĞİLDİR; MOD-0173 B'yi TÜKETMEZ.
  3. Dual-SoR uzlaştırma (owner kararı, inventory dışını da etkiler — CRM Segmentation):
     Option 1 (öneri): B → GlobalProduct'a authoritative reference; B "commercial/brand
        attribute extension" olur; canonical identity A'da tek kalır.
     Option 2: B'nin inventory-facing attribute'ları A'ya (PDR/Gsku) taşınır, B daraltılır.
  4. Karar kapanmadan 0290 hardening alanları rastgele entity'lere EKLENMEZ (W1B bloklu).
```

---

## 7. MIGRATION / RECONCILIATION IMPACT

- **B↔A link:** Şu an FK yok. Option 1 → `Product.GlobalProductId` referansı + backfill; mevcut B kayıtlarının GlobalProduct karşılığı eşlenmeli (veri kalitesi işi).
- **CRM Segmentation (B tüketicisi):** B daraltılır/bağlanırsa Segmentation'ın product-attribute kaynağı etkilenir → CRM ile koordinasyon (cross-capability, MOD-0167 FU02). **Bu inventory'yi aşan bir karar.**
- **Gsku→GlobalProduct (D-SKU-LINK):** SKU→Product resolve'ü PDR üzerinden; MOD-0173'ün tüketeceği contract bunu **açık** sunmalı (yoksa inventory Item↔SKU bağlayamaz).
- **Attribute relokasyonu:** UoM (B'de + Gsku'da pack), shelf-life (hiçbirinde yok) — hangi seviyeye gideceği §8.

---

## 8. MOD-0290 HARDENING TARGET ENTITY LIST (öneri — DEC-INV-17 kapanınca kesinleşir)

| Alan | Önerilen owner seviyesi | Not |
|---|---|---|
| **Base UoM** | `ProductDefinitionRevision` (product karakteristiği) | B'deki `UnitOfMeasure` buraya taşınır |
| **Alternative UoM / UoMConversion** | `Gsku` (pack↔base) | Gsku'da `PackQuantity`+`PackUomCode` zaten kısmi conversion; formalize |
| **GTIN / GS1 / barcode** | `Gsku` (global) + `Lsku`/`FinishedGood` (market GTIN) | Marketed pack GTIN → FinishedGood/Lsku |
| **ShelfLife / MinRemainingShelfLife** | `ProductDefinitionRevision` | ikisinde de YOK — yeni |
| **StorageCondition / RetestDays** | `ProductDefinitionRevision` | ikisinde de YOK — yeni |
| **LotControlled / SerialControlled** | `ProductDefinitionRevision` (material-master control flag) | ikisinde de YOK — yeni |

> **Kural:** Bu tabloyu owner ratify etmeden entity'lere yazma yok (DEC-INV-17 §4).

---

## 9. RISKS / UNRESOLVED TBDs

1. **Dual-SoR (A vs B), FK'sız, ikisi de MOD-0290, ikisi de cross-service tüketilen** → en yüksek risk; inventory'yi aşar (CRM Segmentation).
2. **SkuId seviyesi** (Gsku global vs Lsku/FinishedGood market) → DEC-INV-01/11'e bağlı.
3. **D-SKU-LINK:** Gsku doğrudan GlobalProduct taşımıyor → SKU→Product resolve contract'ı gerekli.
4. **Attribute owner ratifikasyonu** (§8) — shelf-life hiçbirinde yok; UoM iki yerde.
5. **B'nin kaderi** (Option 1 vs 2) CRM ile koordinasyon gerektirir.

---

## 10. VERDICT — W1B'ye geçiş için

```
PARTIAL
```

- **CONFIRMED:** MOD-0173 canonical Item/SKU identity SoR = **Model A**. SkuId **yalnız A'dan** gelebilir (B yapısal olarak SKU sağlayamaz). ItemId → GlobalProduct.
- **BLOCKING W1B (hardening yazımı):**
  - DEC-INV-17 dual-SoR kararı (B'nin kaderi + A↔B link) — owner + CRM koordinasyonu,
  - SkuId seviyesi (DEC-INV-01/11),
  - attribute owner ratifikasyonu (§8),
  - Gsku→GlobalProduct resolve contract (D-SKU-LINK).

> **Sonuç:** W1A (identity reconciliation) yönü **NET** (Model A canonical). Ama W1B (0290 inventory-facing hardening) **başlamaz**: önce owner DEC-INV-17'yi (özellikle B'nin kaderi ve attribute owner) onaylamalı. Rastgele entity'ye alan eklemek K4/K6 (yarım düzeltme / duplicate semantics) riski taşır.
