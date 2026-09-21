# Wave-0 Frozen Contracts (mock source)

§22 Contract-First — bu klasör **dondurulmuş (frozen) contract'lar** ve **mock kaynağı**dır. Developer'lar bunları review edip mock çalıştırır, gerçek implementasyonu ona göre yazar.

## Contract durumu

| Contract | Owner | Format | Durum |
|---|---|---|---|
| **INVENTORY-BUNDLE** | MOD-0173 | `inventory-bundle.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 · mock DOĞRULANDI (Prism 4.10.5)** |
| **PRODUCT-MASTER-BUNDLE** | MOD-0290 | `product-master-bundle.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 CT-review PASS · freeze-ready** |
| **LOCATION** | Location Master | `location.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** |
| **TRACE-BUNDLE** | MOD-0174 | `trace.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** |
| **GRN-EVENT** | MOD-0142 | `grn-event.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** |
| **BOM** | MOD-0193 | `bom.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** |
| **DEMAND** | MOD-0188 | `demand.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** |
| **ATP-BUNDLE** | MOD-0172 | `atp-bundle.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** |

**→ 8/8 Faz-0 contract OpenAPI TAMAM (hepsi valid, x-status FROZEN).**

### MVP-6 öne çekilmiş consumed seam'ler (front-loaded 2026-09-15)

MVP-6'yı "ilk giden lane" yapmak için, tükettiği ama sahibi olmadığı iki seam merkezi CT tarafından öne çekilip donduruldu (§22). Producer (MVP-5/MVP-2) sırası gelince bu frozen sözleşmeye **uyarak** implemente eder; değişiklik yalnız additive (K16).

| Contract | Owner (producer) | Öne çekildi | Format | Durum |
|---|---|---|---|---|
| **WAREHOUSE-OUTBOUND** | MOD-0178 (MVP-5) | MOD-0183 için | `warehouse-outbound.openapi.yaml` | ✅ **v1 FROZEN · consumer-facing slice** |
| **SUPPLIER** | MOD-0140 (MVP-2) | MOD-0147/0148 için | `supplier.openapi.yaml` | ✅ **v1 FROZEN · consumer-facing slice** |

> ⚠ Bu iki dosya MVP-6'nın tükettiği **minimal yüzeydir**, producer'ın tam contract'ı değil. MVP-5/MVP-2 kendi tam producer contract'ını yazarken bu slice'ı **kapsamalı** (superset), daraltmamalı.

### MVP-2 Procurement (P2P) owned contract'lar (DCP-010, 2026-09-16)

Procurement domain'inin (Diten.ProcurementService, port 5062) sahibi olduğu contract'lar. `x-status: REVIEW` = freeze-ready, owner+consumer review bekliyor; review PASS sonrası `FROZEN v1` (§Freeze süreci). Blueprint P2P bundle kodlarıyla hizalı.

| Contract | Owner | Min contract | Format | Durum |
|---|---|---|---|---|
| **SUPPLIER** | MOD-0140 | P2P-BUNDLE | `supplier.openapi.yaml` (v1.1.0) | ✅ (A) consumer slice **FROZEN** (MVP-6 için, şekil değişmedi) + (B) producer surface **REVIEW** — SUPERSET, additive (K16) |
| **REQUISITION-PO** | MOD-0141 | PO-BUNDLE | `requisition-po.openapi.yaml` (v1.0.0) | 🔶 **REVIEW** · freeze-ready |
| **GRN-EVENT** | MOD-0142 | GRN-BUNDLE | `grn-event.openapi.yaml` (v1.0.0) | ✅ **FROZEN** (owned; INVENTORY POST /movements'e post eder — değişmedi) |
| **INVOICE-MATCH** | MOD-0143 | MATCH-BUNDLE | `invoice-match.openapi.yaml` (v1.0.0) | 🔶 **REVIEW** · freeze-ready · ASSUMPTION-P2P-01 (tolerance policy-driven) |
| **CONTRACTING** | MOD-0144 | CLM-CONTRACT-BUNDLE | `contracting.openapi.yaml` (v1.0.0) | 🔶 **REVIEW** · freeze-ready |
| **SOURCING** | MOD-0145 | SOURCING-BUNDLE | `sourcing.openapi.yaml` (v1.0.0) | 🔶 **REVIEW** · freeze-ready |

> Procurement CONSUME eder (değiştirmez): **INVENTORY-BUNDLE** (0173, GRN post), **PRODUCT-MASTER-BUNDLE** (0290, item kimliği), **LOCATION** (GRN warehouse/location). Consumed frozen contract redefine edilmez (K16); envanter yalnız MOD-0173'e post edilir (shadow stock YOK).

İskeletler: [wave0-contracts](../wave0-contracts-product-master-and-inventory.md) · INVENTORY detay: [inventory-bundle-contract-detailed-v0.1](../inventory-bundle-contract-detailed-v0.1.md)

## Mock nasıl çalıştırılır (developer)

OpenAPI dosyasından **tek komutla canlı mock** — el ile stub yazmaya gerek yok:

```bash
# Prism (Stoplight) — örnek response'ları döndüren canlı mock server
npx @stoplight/prism-cli mock docs/analysis/contracts/inventory-bundle.openapi.yaml
# → http://localhost:4010  (POST /movements, GET /availability … örnek datayla döner)
```

Alternatif: **WireMock**, **Microcks**, veya OpenAPI'dan **C# client stub** generate (`nswag` / `openapi-generator`).

## Freeze süreci (owner + consumer)

1. **Owner review:** seam sahibi OpenAPI'ı gözden geçirir, eksik yok der.
2. **Consumer review:** her tüketen MVP "bu spec + mock ile geliştirebilirim" onayı.
3. **Freeze:** `x-status: FROZEN` + `version` sabitlenir. **Sonra breaking change YASAK** (K16); sadece additive (yeni optional field / yeni endpoint) → minor version.
4. Mock yayınlanır; 6 MVP takımı paralel başlar (§22 Faz-1).

## CT review verdict (2026-09-09)

8 iskelet consumer gözünden denetlendi. Minör additive gap'ler bulundu ve v1'e katıldı:
- INVENTORY: cross-LE transfer → `CROSS_LE_FORBIDDEN`, inter-LE = 2-movement (known-gap sonraki faz)
- PRODUCT-MASTER: bulk validate (array)
- LOCATION: `isStockable` flag
- TRACE: lot creation = GR→TRACE POST /lots→INVENTORY
- BOM: explode `asOfDate`

Gerisi tutarlı. INVENTORY OpenAPI = **CT-review PASS**, owner/consumer freeze onayına hazır.
