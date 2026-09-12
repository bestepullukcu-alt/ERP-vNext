# WAVE-0 CONTRACT SKELETONS (8) — Faz-0 Contract-First Freeze Set
*PRODUCT-MASTER · INVENTORY · LOCATION · TRACE · GRN-EVENT · BOM · DEMAND · ATP*

> **Amaç:** §22 Contract-First stratejisinin **Faz-0** çıktısı — 6-yönlü paralelin kilidini açan iki dondurulacak contract iskeleti. Consumer'lar bunlara **+ mock**'a karşı geliştirir.
> **Durum:** **v0.1 DRAFT — FREEZE ÖNCESİ.** Freeze'den sonra breaking change YASAK (K16); sadece additive. Freeze = owner'lar + consumer'lar review + mock hazır + version tag.
> **Tip:** Bu bir **contract (arayüz) spec**'idir — implementation kodu DEĞİL. Diller-agnostik JSON şekilleri.
> **Ortak kurallar (tüm contract'lar):** Tenant + Legal-Entity scoped (DEC-INV-18); `TenantId`/`LegalEntityId` **server-resolved**, payload'dan alınmaz. Tüm response'lar versioned (`contractVersion`). Consumer eksik contract uydurmaz (K12).

---

## CONTRACT 1 — PRODUCT-MASTER-BUNDLE  (owner: MOD-0290)

Canonical ürün/item/SKU kimliği (Model A). Consumer'lar **read-only** — kimliği 0290 sahiplenir.

### 1.1 Kimlik modeli (ölçülen Model A)
```
GlobalProduct  = ItemId (product identity)
   └ ProductDefinitionRevision (versioned attributes)
        └ Gsku   ┐
             └ Lsku   ├─ SkuRef = { SkuId + SkuLevel }   (polymorphic — DEC-INV-17)
                  └ FinishedGood ┘
```
`SkuLevel ∈ { Gsku | Lsku | FinishedGood }`. **Stockable-level PIN RESOLVED 2026-09-09:** polimorfik — üçü de stok tutabilir; balance dimension'da `SkuLevel` var. **Per-product default = Lsku** (legacy precedent: Lsku/batch), konfigüre edilebilir; tek-seviye zorunlu değil.

### 1.2 Queries (consumer'ların çağırdığı)
| Query | Girdi | Çıktı |
|---|---|---|
| `GET /product/{itemId}` | ItemId | `ProductRef` |
| `GET /sku/{skuId}?level={skuLevel}` | SkuId+level | `SkuRef` |
| **`GET /sku/{skuId}/product`** | SkuId+level | `ItemId` (**D-SKU-LINK resolve — kritik**) |
| `GET /sku/{skuId}/uom` | SkuId | `UomInfo` (base + conversions) |
| `POST /validate` | `{itemId?, skuId?, skuLevel?}` | `{exists: bool}` (fail-closed consumer'lar için, 503≠silent-pass) |

### 1.3 DTO şekilleri (skeleton)
```jsonc
ProductRef {
  itemId, canonicalCode, name, lifecycleStatus,
  baseUomId,
  // hardening (W1B — DEC-INV-09):
  shelfLifeDays?, minRemainingShelfLifeDays?, storageCondition?, retestDays?,
  lotControlled: bool, serialControlled: bool
}
SkuRef {
  skuId, skuLevel,           // Gsku|Lsku|FinishedGood
  canonicalCode, itemId,     // resolved GlobalProduct (D-SKU-LINK)
  packUomCode?, packQuantity?,
  gtin?,                     // Gsku global / Lsku|FinishedGood market
  marketCode?                // Lsku/FinishedGood
}
UomInfo {
  baseUomId,
  conversions: [ { fromUom, toUom, numerator, denominator, effectiveFrom?, effectiveTo? } ]
}
```

### 1.4 Events (consumer'ların dinlediği)
`ProductLifecycleChanged { itemId, newStatus }` · `SkuPublished { skuId, skuLevel, itemId }`

### 1.5 Kurallar
- **Read-only for consumers.** Kimlik yalnız MOD-0290 tarafından yazılır; consumer yazamaz.
- **Model B (`Product`) bu contract'ta YOK** (DEC-INV-17 — inventory B'yi tüketmez).
- Versioning: freeze sonrası **additive-only**; breaking = yeni major.
- **PIN (W1A açık):** stockable SkuLevel policy — freeze'den önce netleşmeli.

---

## CONTRACT 2 — INVENTORY-BUNDLE  (owner: MOD-0173)  ⭐ ✅ v1 CT-REVIEW PASS

> **Freeze-ready:** OpenAPI 3.1 spec = `contracts/inventory-bundle.openapi.yaml` (mock source, `prism mock` ile canlı) · detay = `inventory-bundle-contract-detailed-v0.1.md`. Owner+consumer freeze onayına hazır.

Authoritative stok gerçeği. **Balance = projection**, doğrudan yazılmaz (DEC-INV-02). Stok değişmenin **tek yolu = movement post**.

### 2.1 Command — Movement Post  (stok değiştiren TEK API)
```jsonc
POST /inventory/movements
Request {
  idempotencyKey,                    // duplicate guard (DEC-INV-07)
  documentGroupId?,                  // çok-satırlı belge grubu (DEC-INV-15)
  movementType,                      // CANONICAL enum (DEC-INV-16) — bkz. 2.6
  movementReason?,                   // MOD-0048 reference (DEC-INV-16)
  itemId, skuId, skuLevel,           // PRODUCT-MASTER ref
  fromWarehouseId?, fromLocationId?,
  toWarehouseId?,   toLocationId?,
  quantity, uomId,                   // 0173 baseQty'ye çevirir (PRODUCT-MASTER.uom)
  lotId?, serialIds?,                // serial-controlled: count(serialIds)==quantity (R6)
  fromStockStatus?, toStockStatus?,  // dimension (DEC-INV-08)
  sourceModule, sourceType, sourceDocumentId, sourceLineId,   // trace (K6)
  sourceSystem?,                     // dış-entegrasyon LE (DEC-INV-19)
  transactionDate, postingDate       // ayrı (DEC-INV-13)
}
Response { transactionId, balanceSnapshot: BalanceView }
```
**Davranış:** append-only, L3, idempotent. Yanlış hareket **edit edilmez** → `REVERSAL` transaction (DEC-INV-15).

### 2.2 Queries (consumer'ların en çok çağırdığı)
| Query | Çıktı |
|---|---|
| **`GET /inventory/availability?itemId=&skuId=&scope=`** | `AvailabilityView` (0172 ATP + 0189 MRP okur) |
| `GET /inventory/balance?dimension=` | `BalanceView` |
| `GET /inventory/transactions?itemId=&lotId=&location=` | hareket geçmişi (immutable) |

### 2.3 Reservation (fiziksel hold — DEC-INV-05; ATP kararı 0172'de)
```jsonc
POST /inventory/reservations
  { reservationId?, itemId, skuId, skuLevel, quantity, baseQuantity?,
    reservationType, sourceModule, sourceDocumentId, sourceLineId, priority, expiresAt }
  → { reservationId, status }
POST /inventory/reservations/{id}/release
```

### 2.4 DTO — Balance/Availability (projection, read-only)
```jsonc
BalanceView {
  itemId, skuId, skuLevel, legalEntityId,
  warehouseId?, locationId?, lotId?, stockStatus,
  onHand, reserved, available,       // invariant: available ≠ onHand (DEC: available = onHand(AVAILABLE) − reserved)
  inTransit, blocked, qualityInspection,
  baseUomId, lastMovementId, lastPostingDate
}
AvailabilityView { itemId, skuId, onHand, reserved, available, inTransit, byStatus:{...} }
```

### 2.5 Events
`InventoryMovementPosted { transactionId, itemId, skuId, delta, ... }` · `StockStatusChanged` · `ReservationCreated/Released` · `BalanceProjectionUpdated`

### 2.6 Enumler + kurallar
- **`MovementType` (CANONICAL/SABİT — DEC-INV-16):** `OPENING_BALANCE · GOODS_RECEIPT_PO · GOODS_RECEIPT_PROD · GOODS_ISSUE_SALES · GOODS_ISSUE_PROD · WAREHOUSE_TRANSFER · LOCATION_TRANSFER · STATUS_TRANSFER · CUSTOMER_RETURN · SUPPLIER_RETURN · COUNT_GAIN · COUNT_LOSS · SCRAP · WRITE_OFF · REVERSAL` — consumer yeni type yaratamaz.
- **`StockStatus` (dimension — DEC-INV-08):** `AVAILABLE · QUALITY_INSPECTION · QUARANTINE · BLOCKED · IN_TRANSIT · DAMAGED · EXPIRED`
- **Negative stock:** default prohibited (DEC-INV-12) → yetersiz stokta movement reddedilir (canonical error).
- **Balance projection:** doğrudan edit YASAK; yalnız movement (DEC-INV-02).
- **Costing:** `CostingMethodPolicy` per-LE/product (DEC-INV-06); fallback MOVING_AVERAGE. Currency = LE base (DEC-INV-18).
- **Scoping:** Tenant + LegalEntity; server-resolved.

### 2.7 Consumer'lar neyi mock'lar (Faz-1)
| Consumer | Mock'ladığı |
|---|---|
| MVP-2 GRN (0142) | `POST /movements` (GOODS_RECEIPT_PO) |
| MVP-5 WMS (0178/0181) | `POST /movements` (transfer/pick/count) + `GET /availability` |
| MVP-4 MRP (0189) / MOD-0172 ATP | `GET /availability` |
| MVP-6 | events |

---

---

## CONTRACT 3 — LOCATION  (owner: Location Master, MOD-? candidate · DEC-INV-03)

Fiziksel yer hiyerarşisi. **NOT `OrganizationUnit`** (K6 — o organizasyon/raporlama yapısı). Consumer'lar read-only. LegalEntity-scoped.

### 3.1 Queries
| Query | Çıktı |
|---|---|
| `GET /locations/{id}` | `LocationRef` |
| `GET /locations?type=&parentId=&legalEntityId=` | list |
| `GET /locations/{id}/path` | Site→…→Bin tam yol |
| `POST /validate` `{locationId}` | `{exists}` (fail-closed) |
### 3.2 DTO
```jsonc
LocationRef {
  locationId, code, name,
  type,                       // Site|Warehouse|StorageLocation|Zone|Bin
  parentId?, legalEntityId, status,
  operatingMode?,             // internal | external-integrated (DEC-INV-19, sadece Site/Warehouse)
  orgUnitRef?, costCenterCode? // SOFT link (kimlik değil)
}
```
### 3.3 Kurallar
Single-writer = Location Master · consumers read-only · **OrganizationUnit değil** · Warehouse'un OrgUnit/CostCenter'ı soft-referans. Consumed by: 0173, 0178, MVP-2, MVP-4.

---

## CONTRACT 4 — TRACE-BUNDLE  (owner: MOD-0174)

Lot/Serial/genealogy. Inventory movement `lotId`/`serialIds` ile buna bağlanır.

### 4.1 Queries / Commands
| Op | Şekil |
|---|---|
| `GET /lots/{lotId}` | `LotRef` |
| `GET /serials/{serialId}` | `SerialRef` |
| `GET /lots?itemId=&status=&expiryBefore=` | list (FEFO/expiry consumer) |
| `GET /lots/{lotId}/genealogy` | forward/backward links |
| `POST /lots` (owned) | lot oluştur (GR sırasında) |
| `POST /validate` `{lotId?, serialId?}` | `{exists}` |
### 4.2 DTO
```jsonc
LotRef { lotId, lotNumber, itemId, manufacturerLot?, supplierLot?,
  manufacturingDate?, expiryDate?, retestDate?, countryOfOrigin?, status, coaReference? }
SerialRef { serialId, serialNumber, itemId, lotId?, status }
GenealogyLink { parentLotId, childLotId, transformationReference }
```
### 4.3 Kurallar
Lot uniqueness = surrogate `LotId` + business key `Tenant+Item+LotNumber` (DEC-INV-14); Manufacturer/SupplierLot = attribute. Serial-controlled: `count(serialIds)==quantity` (R6). Consumed by: 0175, 0176, 0177, INVENTORY (lot ref).

---

## CONTRACT 5 — GRN-EVENT  (owner: MOD-0142 Receiving — MVP-2 üretir)

Mal kabulü seam'i. GRN, **INVENTORY-BUNDLE `POST /movements` (GOODS_RECEIPT_PO)** çağırır (shadow stock YOK); ayrıca event yayınlar (invoice-match/QC dinler).

### 5.1 Event + posting
```jsonc
Event GoodsReceived {
  grnId, poId, poLineId,
  itemId, skuId, skuLevel, lotId?, serialIds?,
  quantity, uomId, warehouseId, locationId,
  receivedAt, idempotencyKey
}
// → tetikler: INVENTORY POST /movements { movementType: GOODS_RECEIPT_PO, sourceModule: "MOD-0142", sourceDocumentId: grnId, ... }
```
### 5.2 Kurallar
GRN = receipt SoR; stok postingi **INVENTORY contract üzerinden** (0173'e); idempotent; QC gerekiyorsa `toStockStatus = QUALITY_INSPECTION`. Consumed by: INVENTORY (posting), MOD-0143 invoice-match, 0175 QC.

---

## CONTRACT 6 — BOM  (owner: MOD-0193 — MVP-3 üretir)

Manufacturing malzeme/rota yapısı. Consumed by MVP-4 MRP.

### 6.1 Queries
| Query | Çıktı |
|---|---|
| `GET /bom/{itemId}/current` | effective `BomVersionRef` |
| `GET /bom/{bomVersionId}` | `BomView` (components) |
| `POST /bom/explode` `{itemId, quantity}` | patlatılmış component ihtiyaçları (MRP için) |
### 6.2 DTO
```jsonc
BomVersionRef { bomVersionId, itemId, version, status, effectiveFrom?, effectiveTo? }
BomComponent  { componentItemId, quantity, uomId, position, alternates?[] }
Routing       { routingId, steps:[ { stepNo, operation, workCenter? } ] }
```
### 6.3 Kurallar
Owner 0193; ürün kimliği = 0290 (PRODUCT-MASTER ref); versioning **Change Control (0209)** ile — freeze/effective. Consumed by: MVP-4 MRP.

---

## CONTRACT 7 — DEMAND  (owner: MOD-0188 — MVP-4 üretir)

Talep planı/forecast. **Shared seam** → freeze sonrası 0172/0176/0189/0192 paralel tüketir.

### 7.1 Queries + Event
| Op | Şekil |
|---|---|
| `GET /demand/plan?itemId=&period=` | `DemandPlanRef` |
| `GET /demand/forecast?itemId=&horizon=` | forecast serisi |
| Event `DemandPlanPublished` | `{ planId, itemId, period }` |
### 7.2 DTO
```jsonc
DemandPlanRef { planId, itemId, period, quantity, uomId, confidence?, status }
```
### 7.3 Kurallar
Single-writer = 0188; consumers **frozen** demand okur. Consumed by: 0172 ATP, 0176 FEFO, 0189 MRP, 0192 Capacity.

---

## CONTRACT 8 — ATP-BUNDLE  (owner: MOD-0172, Commercial/O2C — mevcut/reserved)

Availability/promise **kararı**. INVENTORY availability + reservation + DEMAND okur; **ikinci stok balance YOK** (DEC-INV-05).

### 8.1 Queries / Commands
| Op | Şekil |
|---|---|
| `POST /atp/check` `{itemId, skuId, quantity, requestedDate}` | `AtpResult` |
| `POST /allocation` `{orderRef, itemId, quantity, priority}` | `AllocationDecision` |
### 8.2 DTO
```jsonc
AtpResult { itemId, requestedQty, availableQty, promiseDate, ctpDate? }
AllocationDecision { allocationId, orderRef, itemId, allocatedQty, priority, status }
```
### 8.3 Kurallar
Owner 0172; **INVENTORY availability + reservation (frozen)** ve DEMAND okur; fiziksel hold = 0173 reservation (0172 karar üretir, stok tutmaz). Consumed by: O2C/sales.

---

## FAZ-0 CONTRACT ENVANTERİ (8 iskelet — hepsi bu dokümanda)

| # | Contract | Owner | Üretir (MVP) | Kim tüketir/mock'lar |
|---|---|---|---|---|
| 1 | **PRODUCT-MASTER-BUNDLE** | MOD-0290 | MVP-1 | 0173,0174,MVP-2,MVP-3,CRM |
| 2 | **INVENTORY-BUNDLE** | MOD-0173 | MVP-1 | 0142,0178,0181,0172,0189,0176 |
| 3 | **LOCATION** | Location Master | MVP-1 | 0173,0178,MVP-2,MVP-4 |
| 4 | **TRACE-BUNDLE** | MOD-0174 | MVP-1 | 0175,0176,0177,INVENTORY |
| 5 | **GRN-EVENT** | MOD-0142 | MVP-2 | INVENTORY,0143,0175 |
| 6 | **BOM** | MOD-0193 | MVP-3 | MVP-4 MRP |
| 7 | **DEMAND** | MOD-0188 | MVP-4 | 0172,0176,0189,0192 |
| 8 | **ATP-BUNDLE** | MOD-0172 | (mevcut) | O2C/sales |

> **Kilit gözlem:** MVP-1 **4 contract üretir** (1-4) → G1'de bunlar freeze olunca MVP-2/3/5 mock'u bırakıp gerçeğe bağlanır. MVP-2/3/4 kendi contract'larını (5/6/7) üretir; MVP-4/0172 onları + INVENTORY/DEMAND'i tüketir.

## FREEZE CHECKLIST (Faz-0 bitişi — 8 contract)
- [ ] 8 contract da **owner review** (her seam sahibi kendi contract'ını dondurur)
- [ ] Consumer review: her MVP takımı "kendi CONSUMES listesini bu iskeletlerle **mock'layabilir miyim**?" onayı
- [x] **Stockable SkuLevel policy PIN'lendi** ✅ (polimorfik, per-product default Lsku — 2026-09-09)
- [ ] Her contract için **mock implementasyonu** hazır (canned data)
- [ ] Her contract `contractVersion = v1` tag + published (frozen)
- [ ] Freeze sonrası **breaking change YASAK** (additive-only, K16)

## SONRAKİ (Faz-1)
8 contract freeze edilince **6 MVP takımı paralel başlar** (§22 Faz-1): herkes OWNS'unu yazar, CONSUMES'u mock üzerinden. Gerçek implementasyonlar indikçe (G1→…) mock'lar gerçekle değişir (Faz-2 progressive integration).

> **Not:** Bu iskeletler **v0.1 DRAFT** — CT ölçümü + karar temelli, ama **owner review + PIN kapanışı** olmadan freeze edilmez. Bir contract dondurulduğunda o seam'in tek yazıcısı sahiptir (K15).

> **Sonraki:** bu iki iskelet freeze edilince MVP-2/3/5 mock'a karşı **paralel başlayabilir**. Kalan Faz-0 contract'ları (LOCATION, TRACE, GRN, BOM, Demand, ATP) aynı sprint'te iskeletlenir.
