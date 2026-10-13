# Wave-0 Frozen Contracts (mock source)

§22 Contract-First — bu klasör **dondurulmuş (frozen) contract'lar** ve **mock kaynağı**dır. Developer'lar bunları review edip mock çalıştırır, gerçek implementasyonu ona göre yazar.

## Contract durumu

| Contract | Owner | Format | Durum |
|---|---|---|---|
| **INVENTORY-BUNDLE** | MOD-0173 | `inventory-bundle.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 · mock DOĞRULANDI (Prism 4.10.5)** · **1.2.0 additive** (WP-0173-PM1, CT-PM1-1): 422 `PRODUCT_MASTER_DATA_INCOMPLETE` / `ITEM_NOT_ACTIVE` / `SKU_LEVEL_NOT_STOCKABLE` on `postMovement` (PRODUCT-MASTER 1.1 consumer rules) · **1.1.0 additive** (WP-0173-PM2, MOD-0290 R-12): `SkuLevel` + `Item`; `getStockPresence` — `GET /api/internal/inventory/stock-presence`, service-to-service (`X-Internal-Api-Key` + `X-Tenant-Id`, no quantities, not on the Gateway) · 1.0.2 description-only (1.0.1 MOD-0174 CT-D1 lot hold; 1.0.2 MOD-0175 CT-Q3: `LOT_RETEST_DUE` + availability "retest-due lots excluded") · **1.2.1** description only (WP-0174-ITEM): `SKU_LEVEL_NOT_STOCKABLE` also for a `stockable: false` SKU · **1.3.0 additive** (WP-0176-F2, MOD-0176 KF-B / KF-C): `MovementRequest.fefoBypassReason`, `MovementResponse.expiryChecks`; 422 `FEFO_BYPASS_REASON_REQUIRED` / `FEFO_ORDER_VIOLATION` / `LOT_BELOW_MIN_REMAINING_SHELF_LIFE` (`details.requiredToStockStatus = QUARANTINE`, CT-F1) |
| **PRODUCT-MASTER-BUNDLE** | MOD-0290 | `product-master-bundle.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 CT-review PASS · freeze-ready** · this path now holds the **v1.1 DRAFT** (`1.1.0-draft.11`, taken file-only from `origin/chore/ct-round-2` @ `00a4d4268` by WP-MVP1-FUB2 — earlier `1.1.0-draft.8` @ `56490f48e` by WP-0173-PM1, OD-8; since draft.11 the first example of every v1.1 operation is v1.1-shaped and the v1.0 one is the last, `v1.0-mock`); the frozen v1.0 is `product-master-bundle.v1.0.openapi.yaml` (unchanged) |
| **LOCATION** | Location Master | `location.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** |
| **TRACE-BUNDLE** | MOD-0174 | `trace.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** · **1.3.0 additive** (WP-0174-ITEM): `SkuLevel` + `Item`; `createLot` 422 `PRODUCT_MASTER_DATA_INCOMPLETE` / `SKU_LEVEL_NOT_STOCKABLE` · **1.4.0 additive** (WP-0176-F2, MOD-0176 KF-D): `createLot` / `updateLot` (Draft) `dateOverride`; `LotRef` / `LotDetail` `suggestedExpiryDate` / `suggestedRetestDate` / `expiryOverride`; `LotChangeEntry.action` `ExpiryOverridden`; 5 lot-date codes |
| **QUARANTINE-BUNDLE** | MOD-0175 | `quarantine.openapi.yaml` (OpenAPI 3.1) | ✅ **1.0.0 FROZEN** (WP-0175-Q1) · **1.1.0 additive** (WP-0174-ITEM): `SkuLevel` + `Item`; nothing else · **1.2.0 additive** (WP-MVP1-FUB2): `HistoryAction` + `NoteUpdated` with the optional `HistoryEntry.noteChange` (`NoteChange`: note length / attachment count before → after) — FU-0175-ITEM-01; `updateHold` lists `QUARANTINE_DECISION_ALREADY_PENDING` (409, the note is locked while a submitted decision is open) — FU-0175-ITEM-03; the replay answers the first result (O-ITEM-4); the 1.1.0 file is the test fixture `quarantine-v1.1.0.openapi.yaml` |
| **FEFO-BUNDLE** | MOD-0176 | `fefo.openapi.yaml` (OpenAPI 3.1) | ✅ **1.0.0 FROZEN** (WP-0176-F1, CT-kabullü öneri `drafts/fefo-bundle-v1-proposal.md` + pack §19.7: `EXPIRY_SOD_VIOLATION` 409 (CT-F10), varsayılan anlam `APPROVE` (CT-F8), liste CSV `expiry.dashboard.read` altında (CT-F14)). F1 politika + istisna işlemlerini sunar; panel / listeler / FEFO sırası / kanıt / tarih sapması / imza konusu F2–F5 (`x-implemented-by-slice`) · **1.0.1** description only (WP-0176-F2): `x-implemented-by-slice.F2 = [createDateOverride]` |
| ↳ **FEFO-BUNDLE** | MOD-0176 | `fefo.openapi.yaml` | **1.0.2** description only (WP-0176-F3): `x-implemented-by-slice.F3 = [getSignableSubject]` · **1.1.0 additive** (WP-0176-F4): `openException` optional `baseUomId` (FU-0176-F1-UOM: omitted → the lot's stock unit, unreadable → none); `x-implemented-by-slice.F4 = [getDashboard, listApproaching, listExpired, getFefoOrder]`; the 1.0.2 file is the test fixture `fefo-v1.0.2.openapi.yaml` |
| **RECALL-BUNDLE** | MOD-0177 | `recall.openapi.yaml` (OpenAPI 3.1) | ✅ **1.0.0 FROZEN** (WP-0177-R1, CT-kabullü öneri `drafts/recall-bundle-v1-proposal.md` + pack §19.7: dört göz iki yönde `RECALL_SOD_VIOLATION` 409 (CT-R1), onayda kapsam mührü `RECALL_SCOPE_CHANGED` 409, `getLotRecallStatus` kiracı genelinde (CT-R7), `recall.settings.update` imzası varsayılan değil (CT-R16); uzlaştırmalar R-1…R-12 changelog'da, ör. R1 elle kapsam girişi `addScopeItems`). R1 ayar + vaka + kayıt + lot durumu işlemlerini sunar; izleme / karantina isteği / iade / tatbikat / kanıt / imza konusu R2–R7 (`x-implemented-by-slice`) |
| **RECALL-BUNDLE 1.1.0** | MOD-0177 | `recall.openapi.yaml` | ✅ **1.1.0** (WP-0177-R2, yalnız ekleme): `runTrace`, `exportTrace`, `getCaseScopeDiff` sunulur; izleme mührü `rt-v1` (sevkiyat okuması mühre girmez); gönderimde dondurulan anlık görüntü, onayda canlı yeniden izleme → farkta `RECALL_SCOPE_CHANGED` 409 + fark; yeni isteğe bağlı üyeler `SnapshotRef`, `ScopeItem.origin/new`; `reviseScope` R3'e (uzlaştırmalar R2-1…R2-7 changelog'da) |
| **GRN-EVENT** | MOD-0142 | `grn-event.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** |
| **BOM** | MOD-0193 | `bom.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 FROZEN** · v1.1.0 additive (2026-10-06, MVP-3: management surface; v1.0.0 operations unchanged) · **v1.2.0 additive** (2026-10-13): BOM-version lifecycle event `BomVersionEvent` (`manufacturing.bomversion.effective.v1` / `.superseded.v1`, outbox in the BOM transaction; identity + state only, consumer MOD-0189 MRP) |
| **DEMAND** | MOD-0188 | `demand.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** |
| **ATP-BUNDLE** | MOD-0172 | `atp-bundle.openapi.yaml` (OpenAPI 3.1) | ✅ **v1 freeze-ready** |

**→ 8/8 Faz-0 contract OpenAPI TAMAM (hepsi valid, x-status FROZEN).**

### MVP-1 Platform Paylaşılan Hizmetler (2026-10-08)

| Contract | Owner | Format | Durum |
|---|---|---|---|
| **ESIGN-BUNDLE** | MOD-0022 (Diten.Platform + Diten.AuthService §4.1–4.3) | `esign.openapi.yaml` (OpenAPI 3.1, v1.0.1) | ✅ **v1 FROZEN** (WP-0022-E2, CT-kabullü öneri `drafts/esign-bundle-v1-proposal.md` ile birebir — diff testi `ESignatureContractTests`). E1 (§4.1–4.3) + E2 (§4.4–4.16, §4.20–4.26) uygulandı; §4.17–4.19 (örnekler) E3, Workflow eki E4. Sonrası yalnız additive (minor). · **1.0.1 yalnız açıklama** (WP-MVP1-FUB2, FU-0022-CT-01): görünürlük notu — şirketsiz (kiracı düzeyi) kayıt `esign.signatures.read` / `esign.signatures.export` sahiplerine görünür, şirket kaydı SEC-002 veri kapsamına göre, imzalayan kendi kaydını görür (VIS1 / FUB1 davranışı). |

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
