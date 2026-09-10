# INVENTORY-BUNDLE — Detailed API Contract (v0.1 DRAFT for freeze)

> **Owner:** MOD-0173 Inventory Ledger & Valuation · **Consumers:** 0142 GRN · 0178 WMS · 0181 Count · 0172 ATP · 0189 MRP · 0176 FEFO
> **Amaç:** Developer'ın **mock yazacağı / gerçek implement edeceği** seviyede tam contract. §22 Faz-0.
> **Durum:** v0.1 DRAFT — owner+consumer review sonrası freeze (K16: freeze sonrası additive-only).
> Üst-seviye iskelet: [wave0-contracts](wave0-contracts-product-master-and-inventory.md#contract-2--inventory-bundle--owner-mod-0173--istenen-iskelet)

---

## 0. Ortak konvansiyonlar

**Base path:** `/api/inventory` (gateway üzerinden)
**Auth:** `Authorization: Bearer <jwt>`. `TenantId` + `LegalEntityId` **server-resolved** (token'dan; çok-LE operatörlerde izinli ise `X-Legal-Entity-Id` header ile override). Payload'da GÖNDERİLMEZ.
**Idempotency:** state-changing POST'larda `Idempotency-Key` header **veya** body `idempotencyKey` (zorunlu). Aynı key + aynı payload → **aynı sonucu** döndürür (replay), yeni kayıt YOK.
**Precision:** miktar/maliyet **decimal → JSON string** (float değil; DEC-INV finance precision). Örn `"quantity": "100.000"`.
**Tarih:** ISO-8601 UTC (`2026-09-09T10:00:00Z`).
**Versioning:** her response `"contractVersion": "v1"`.
**Concurrency:** mutable kayıtlarda (reservation) `ETag` / `If-Match`.

### 0.1 Canonical error envelope
```jsonc
HTTP <4xx|5xx>
{
  "error": {
    "code": "INSUFFICIENT_STOCK",         // stabil makine-kodu (enum, §6)
    "message": "İnsan-okur açıklama",
    "details": { "available": "80.000", "requested": "100.000" },
    "correlationId": "req-7f3a…"
  },
  "contractVersion": "v1"
}
```

---

## 1. POST /inventory/movements  — stok değiştiren TEK API

Append-only, idempotent, L3. Stok değişmenin başka yolu yok (balance doğrudan edit edilmez).

### Request
```jsonc
POST /api/inventory/movements
Idempotency-Key: mv-2026-09-09-abc123          // veya body.idempotencyKey
Content-Type: application/json
{
  "idempotencyKey": "mv-2026-09-09-abc123",
  "documentGroupId": "GRN-10001",              // opsiyonel — çok-satırlı belge grubu
  "movementType": "GOODS_RECEIPT_PO",          // §6.1 enum (CANONICAL)
  "movementReason": "STANDARD",                // opsiyonel — MOD-0048 reference kodu
  "itemId": "b1f2…",                           // GlobalProduct (PRODUCT-MASTER)
  "skuId": "c3d4…",
  "skuLevel": "Lsku",                          // Gsku|Lsku|FinishedGood (polimorfik)
  "toWarehouseId": "wh-01",
  "toLocationId": "loc-A-12-3",
  "fromWarehouseId": null,
  "fromLocationId": null,
  "quantity": "100.000",
  "uomId": "BOX",                              // 0173 baseUom'a çevirir (PRODUCT-MASTER.uom)
  "lotId": "lot-777",                          // lot-controlled ise zorunlu
  "serialIds": null,                           // serial-controlled ise: count == quantity
  "toStockStatus": "QUALITY_INSPECTION",       // §6.2 — default AVAILABLE
  "fromStockStatus": null,
  "sourceModule": "MOD-0142",
  "sourceType": "GOODS_RECEIPT",
  "sourceDocumentId": "GRN-10001",
  "sourceLineId": "3",
  "sourceSystem": null,                        // dış-entegrasyon LE'de dolu (DEC-INV-19)
  "transactionDate": "2026-09-09T09:00:00Z",
  "postingDate": "2026-09-09T10:00:00Z"
}
```

### Success — 201 Created
```jsonc
{
  "transactionId": "txn-55010",
  "transactionNumber": "INV-2026-0000123",
  "postedAt": "2026-09-09T10:00:00Z",
  "balanceSnapshot": {
    "itemId": "b1f2…", "skuId": "c3d4…", "skuLevel": "Lsku",
    "legalEntityId": "le-tr", "warehouseId": "wh-01", "locationId": "loc-A-12-3",
    "lotId": "lot-777", "stockStatus": "QUALITY_INSPECTION",
    "onHand": "100.000", "reserved": "0.000", "available": "0.000",
    "baseUomId": "EA", "baseQuantity": "1000.000"      // 1 BOX = 10 EA
  },
  "contractVersion": "v1"
}
```
### Idempotent replay — 200 OK
Aynı `idempotencyKey` tekrar → **201 değil 200**, orijinal `transactionId` + `"idempotentReplay": true`.

### Error cases
| HTTP | code | Ne zaman |
|---|---|---|
| 400 | `INVALID_MOVEMENT_TYPE` | movementType enum dışı |
| 422 | `INSUFFICIENT_STOCK` | çıkış > available, negative-policy=prohibited (DEC-INV-12) |
| 422 | `SERIAL_COUNT_MISMATCH` | serial-controlled item'da count(serialIds) ≠ quantity (R6) |
| 422 | `LOT_REQUIRED` | lot-controlled item'da lotId yok |
| 422 | `POSTING_PERIOD_CLOSED` | backdated/kapalı dönem, policy izin vermiyor (DEC-INV-13) |
| 422 | `UNKNOWN_LOCATION` | locationId LOCATION contract'ında yok |
| 403 | `CROSS_LE_FORBIDDEN` | başka legal-entity'ye posting |
| 424/503 | `PRODUCT_MASTER_UNAVAILABLE` | itemId/skuId doğrulaması yapılamadı (fail-closed, silent-pass YOK) |

```jsonc
// 422 INSUFFICIENT_STOCK örneği
{ "error": { "code": "INSUFFICIENT_STOCK",
  "message": "Yetersiz stok: 80 mevcut, 100 istendi.",
  "details": { "available": "80.000", "requested": "100.000", "itemId": "b1f2…" },
  "correlationId": "req-7f3a…" }, "contractVersion": "v1" }
```

### Mock rehberi (Faz-1)
Mock: gelen movement'i in-memory bir dict'te toplasın; balance = Σ(qty by dimension); `available = onHand(AVAILABLE-status) − reserved`; idempotencyKey'i hatırlasın. GR/issue/transfer için `balanceSnapshot` döndürsün.

---

## 2. GET /inventory/availability  — 0172/0189/0176 en çok bunu çağırır

### Request
```
GET /api/inventory/availability?itemId=b1f2…&skuId=c3d4…&skuLevel=Lsku&warehouseId=wh-01
    (scope opsiyonel: warehouseId/locationId yoksa LE toplamı)
```
### Success — 200 OK
```jsonc
{
  "itemId": "b1f2…", "skuId": "c3d4…", "skuLevel": "Lsku", "legalEntityId": "le-tr",
  "scope": { "warehouseId": "wh-01" },
  "onHand": "100.000", "reserved": "30.000", "available": "70.000",
  "byStatus": {
    "AVAILABLE": "100.000", "QUALITY_INSPECTION": "0.000",
    "QUARANTINE": "0.000", "BLOCKED": "0.000", "IN_TRANSIT": "0.000"
  },
  "baseUomId": "EA", "asOf": "2026-09-09T10:05:00Z", "contractVersion": "v1"
}
```
**Invariant:** `available = onHand(status=AVAILABLE) − reserved`.
### Error
| 404 | `UNKNOWN_ITEM` / `UNKNOWN_SKU` | 400 | `MISSING_QUERY_PARAM` (itemId|skuId zorunlu) |

---

## 3. GET /inventory/balance  — dimension bazlı bakiye

### Request
```
GET /api/inventory/balance?itemId=b1f2…&skuLevel=Lsku&warehouseId=wh-01&lotId=lot-777&stockStatus=AVAILABLE
```
### Success — 200 OK
```jsonc
{
  "rows": [
    { "itemId":"b1f2…","skuId":"c3d4…","skuLevel":"Lsku","legalEntityId":"le-tr",
      "warehouseId":"wh-01","locationId":"loc-A-12-3","lotId":"lot-777",
      "stockStatus":"AVAILABLE",
      "onHand":"70.000","reserved":"30.000","available":"40.000",
      "inTransit":"0.000","blocked":"0.000","qualityInspection":"0.000",
      "baseUomId":"EA","lastMovementId":"txn-55010","lastPostingDate":"2026-09-09T10:00:00Z" }
  ],
  "total": 1, "contractVersion": "v1"
}
```

---

## 4. GET /inventory/transactions  — immutable hareket geçmişi

### Request
```
GET /api/inventory/transactions?itemId=b1f2…&lotId=lot-777&locationId=loc-A-12-3
    &from=2026-09-01&to=2026-09-30&page=1&pageSize=50
```
### Success — 200 OK
```jsonc
{
  "items": [
    { "transactionId":"txn-55010","transactionNumber":"INV-2026-0000123",
      "movementType":"GOODS_RECEIPT_PO","movementReason":"STANDARD",
      "itemId":"b1f2…","skuId":"c3d4…","skuLevel":"Lsku",
      "quantity":"100.000","uomId":"BOX","baseQuantity":"1000.000","baseUomId":"EA",
      "toWarehouseId":"wh-01","toLocationId":"loc-A-12-3","lotId":"lot-777",
      "fromStockStatus":null,"toStockStatus":"QUALITY_INSPECTION",
      "sourceModule":"MOD-0142","sourceDocumentId":"GRN-10001","sourceLineId":"3",
      "transactionDate":"2026-09-09T09:00:00Z","postingDate":"2026-09-09T10:00:00Z",
      "createdBy":"user-…","createdAt":"2026-09-09T10:00:00Z" }
  ],
  "page":1,"pageSize":50,"total":1,"contractVersion":"v1"
}
```
> Transaction'lar **immutable** — düzeltme = `REVERSAL` movement (yeni kayıt), edit yok.

---

## 5. Reservations  (fiziksel hold — ATP kararı 0172'de)

### 5.1 POST /inventory/reservations
```jsonc
POST /api/inventory/reservations
{ "idempotencyKey":"rsv-abc",
  "itemId":"b1f2…","skuId":"c3d4…","skuLevel":"Lsku",
  "quantity":"30.000","reservationType":"SALES_ORDER",
  "sourceModule":"MOD-0172","sourceDocumentId":"SO-900","sourceLineId":"1",
  "priority":10, "expiresAt":"2026-09-16T00:00:00Z" }
```
**201:**
```jsonc
{ "reservationId":"rsv-4001","status":"ACTIVE","reservedQuantity":"30.000",
  "availableAfter":"40.000","etag":"W/\"1\"","contractVersion":"v1" }
```
**422 `INSUFFICIENT_AVAILABLE`** — reserve > available.

### 5.2 POST /inventory/reservations/{id}/release
```
POST /api/inventory/reservations/rsv-4001/release
If-Match: W/"1"
```
**200:** `{ "reservationId":"rsv-4001","status":"RELEASED","availableAfter":"70.000" }`
**409 `RESERVATION_CONFLICT`** — ETag uyuşmazlığı (eşzamanlı değişiklik).

---

## 6. Enumler + kurallar (freeze'de sabit)

### 6.1 MovementType (CANONICAL — DEC-INV-16, kullanıcı yeni yaratamaz)
`OPENING_BALANCE · GOODS_RECEIPT_PO · GOODS_RECEIPT_PROD · GOODS_ISSUE_SALES · GOODS_ISSUE_PROD · WAREHOUSE_TRANSFER · LOCATION_TRANSFER · STATUS_TRANSFER · CUSTOMER_RETURN · SUPPLIER_RETURN · COUNT_GAIN · COUNT_LOSS · SCRAP · WRITE_OFF · REVERSAL`
> Sebep detayı `movementReason` = **MOD-0048 reference** (extensible), type'a eritilmez.

### 6.2 StockStatus (dimension — DEC-INV-08)
`AVAILABLE · QUALITY_INSPECTION · QUARANTINE · BLOCKED · IN_TRANSIT · DAMAGED · EXPIRED`
> Quarantine lifecycle 0175'te; 0175 balance tutmaz, `STATUS_TRANSFER` movement ile status değiştirir.

### 6.3 Error codes (stabil)
`INVALID_MOVEMENT_TYPE · INSUFFICIENT_STOCK · SERIAL_COUNT_MISMATCH · LOT_REQUIRED · POSTING_PERIOD_CLOSED · UNKNOWN_LOCATION · UNKNOWN_ITEM · UNKNOWN_SKU · CROSS_LE_FORBIDDEN · PRODUCT_MASTER_UNAVAILABLE · INSUFFICIENT_AVAILABLE · RESERVATION_CONFLICT · MISSING_QUERY_PARAM`

### 6.4 Değişmez kurallar
- **Balance = projection**, doğrudan edit YASAK (DEC-INV-02) — sadece movement.
- **Append-only + REVERSAL** (edit yok, DEC-INV-15).
- **Idempotent** her state-changing POST.
- **Negative stock** default prohibited (DEC-INV-12); exception = explicit LE/warehouse/item policy + audit.
- **Tenant + LegalEntity** scoped; currency = LE base (DEC-INV-18).
- **Costing** = `CostingMethodPolicy` per-LE/product; fallback MOVING_AVERAGE (DEC-INV-06).
- Her posting **audit event + correlationId** üretir.

---

## 7. Consumer mock matrisi (Faz-1'de kim neyi kullanır)
| Consumer | Endpoint | Amaç |
|---|---|---|
| MVP-2 GRN (0142) | `POST /movements` (GOODS_RECEIPT_PO) | mal kabul → stok |
| MVP-5 WMS (0178) | `POST /movements` (transfer/pick) + `GET /availability` | fiziksel hareket |
| MVP-5 Count (0181) | `POST /movements` (COUNT_GAIN/LOSS) | sayım düzeltme |
| MVP-4 MRP (0189) | `GET /availability` | net requirement |
| MOD-0172 ATP | `GET /availability` + `POST /reservations` | promise + hold |
| MVP-1 FEFO (0176) | `GET /balance?stockStatus=` + lot expiry | FEFO allocation |

> **Freeze onayı:** yukarıdaki consumer'lar "bu endpoint'lerle mock'layabilirim" derse INVENTORY-BUNDLE v1 **frozen** → MVP-2/5/4/0172 paralel başlar.
