# SHIPMENT-BUNDLE 3.3.0 — değişiklik notu (talep geçmişi eki)

**Taban:** `shipment-bundle.openapi.yaml` 3.2.0 on `origin/main` (`4aeae0671`), Ali onaylı, PR #134.
**Yeni sürüm:** `info.version` 3.3.0. `x-status: FROZEN` ve `x-contract-version: v1` **değişmedi** — ölçüldü.
**Kapsam:** yalnız EKLEME. Kod yok.
**Tüketici:** MOD-0188 Talep Planlama, AC D05–D08.
**İhtiyaç listesi:** `origin/feature/sce/mod-0188-demand-planning` dalında
`execution/domains/supply-chain-execution/work-packs/WP-MOD0188-CONTRACT-NEEDS.md` bölüm 1, 14 satır.

## 1. Değişikliğin tamamı

| # | ne | nerede |
|---|---|---|
| 1 | `info.version` 3.2.0 → 3.3.0 | `:15` |
| 2 | yeni enum `RealizedLineStatus` | `components.schemas` |
| 3 | yeni enum `DemandHistoryRecordKind` | `components.schemas` |
| 4 | yeni şema `UomConversionReference` | `components.schemas` |
| 5 | yeni şema `RealizedShipmentLine` | `components.schemas` |
| 6 | `ShipmentDetail`'e tek yeni **isteğe bağlı** özellik: `realizedLines` | `ShipmentDetail.allOf[1].properties` |

Başka hiçbir şey değişmedi.

## 2. `ShipmentLine`'a neden eklenmedi

Bu, notun en önemli maddesi ve bir tasarım kararı değil, ölçümün zorladığı bir sonuç.

`ShipmentLine` şeması **iki yerden** kullanılıyor: `CreateShipmentCommand.lines` (istek, `:3065`) ve
`ShipmentDetail.lines` (cevap, `:3123`). Üstelik `additionalProperties: false` taşıyor.

Dolayısıyla `realizedQuantity`, `realizedAt`, `shipmentLineId` gibi alanlar oraya eklenseydi, bunlar **create
isteğinde de kabul edilen alanlar** hâline gelirdi. Sunucu türetimli ve sevk sonrası değişmez olması gereken
bir değeri istemcinin yazabilmesi, kuralın lafzına uysa bile ("yalnız ekleme") anlamına aykırı olurdu.

Bu yüzden gerçekleşme verisi ayrı bir **yalnız-cevap** şemasına (`RealizedShipmentLine`) kondu ve
`ShipmentDetail`'e isteğe bağlı bir dizi olarak bağlandı. `ShipmentLine` ve `ShipmentDetail.lines`
3.2.0'daki hâliyle bit bit aynı kaldı — bölüm 5'te ölçüldü.

## 3. On dört satırın durumu

| # | ihtiyaç satırı | durum | nasıl karşılandı |
|---|---|---|---|
| 1 | `tenantId` | **eklendi** | `RealizedShipmentLine.tenantId`, cevapta alan |
| 2 | `legalEntityId` | **eklendi** | `RealizedShipmentLine.legalEntityId`, cevapta alan |
| 3 | `shipmentId` | **eklendi** | `RealizedShipmentLine.shipmentId` (ebeveyn anahtar) |
| 4 | `shipmentLineId` | **eklendi** | değişmez satır kimliği; `lineNumber` görüntü/sıra olarak korundu |
| 5 | `sourceRecordKey` | **eklendi** | nullable; null ise satır dosya geçmişiyle birleştirilemez (karantina) |
| 6 | `sourceVersion` | **eklendi** | monoton artan tamsayı, `minimum: 1` |
| 7 | satır düzeyi gerçekleşme durumu | **eklendi** | yeni enum `RealizedLineStatus`; `ShipmentStatus` yeniden kullanılmadı |
| 8 | `realizedAt` | **eklendi** | UTC; `Dispatched` ise zorunlu, `NotRealized` ise null |
| 9 | depo kimliği kanonik eşlemesi | **bağımlılık** | `warehouseReferenceId` opak kaldı; WAREHOUSE-SHIPMENT-TRIGGER (merkez / MVP-5) |
| 10 | `skuId` | **eklendi** | `RealizedShipmentLine.skuId` (ve `itemId`) |
| 11 | `realizedQuantity` | **eklendi** | sevk sonrası değişmez; `quantity`'nin tanımına dokunulmadı |
| 12 | birim + temel birime dönüşüm izi | **referans** | `uomId` + `UomConversionReference` (kural kimliği + sürüm). Kural MDM'nin (MOD-0290 PRODUCT-MASTER); sözleşmeye konulmadı |
| 13 | iptal/düzeltme/iade kaydı ve ilişkili satır | **kısmen eklendi + ertelendi** | `DemandHistoryRecordKind` (`Realization`/`Correction`/`Cancellation`) + `relatedShipmentLineId` eklendi. **İADE bağlantısı 3.3.0'a girmedi** — bkz. bölüm 6 |
| 14 | kanonik alan seti / içerik karşılaştırması | **eklendi** | `canonicalContentVersion`; set adlandırıldı |
| — | stockout / karşılanmamış talep | **kapsam dışı** | sözleşme stok tutmuyor; `RealizedShipmentLine` açıklamasında açıkça yazıldı |

On dört satırın on biri eklendi, biri referans, biri bağımlılık, biri (13) kısmen eklendi ve iade ayağı
ertelendi; liste dışı stockout kapsam dışı olarak işaretlendi.

## 4. Alanlar

Hepsi `RealizedShipmentLine` altında, hepsi **yalnız cevap**. AC kolonu MOD-0188'in ihtiyaç tablosundan.

| alan | tip | anlam | zorunluluk | örnek | AC |
|---|---|---|---|---|---|
| `tenantId` | uuid | yetkili kiracı kapsamı; JWT'den türetilir | zorunlu | `aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa` | D05–D08 |
| `legalEntityId` | uuid | yetkili şirket kapsamı; MDM doğrulamasından geçer (R-2) | zorunlu | `bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb` | D05–D08 |
| `shipmentId` | uuid | sevkiyat kökü; satır anahtarı olarak tek başına yetersiz | zorunlu | `18300000-0000-0000-0000-000000000001` | D05–D06 |
| `shipmentLineId` | uuid | **değişmez** satır kimliği | zorunlu | `18300001-0000-0000-0000-000000000001` | D05–D06 |
| `lineNumber` | string | görüntü/sıra numarası; iş kimliği değil | zorunlu | `1` | D05 |
| `sourceRecordKey` | string \| null | dosya satırını tek bir gerçekleşmiş satıra eşler | nullable | `DH-2026-09-0001:17` | D05 |
| `sourceVersion` | integer ≥ 1 | kaynak düzeltmelerini ayırır, bayat üzerine yazmayı önler | zorunlu | `2` | D05–D06 |
| `recordKind` | enum | `Realization` \| `Correction` \| `Cancellation` | zorunlu | `Correction` | D05–D08 |
| `relatedShipmentLineId` | uuid \| null | düzeltme/iptalin bağlandığı orijinal satır | `Correction`/`Cancellation` ise zorunlu | `18300001-0000-0000-0000-000000000001` | D05–D08 |
| `realizationStatus` | enum | `NotRealized` \| `Dispatched` | zorunlu | `Dispatched` | D05–D08 |
| `realizedAt` | date-time \| null | iş etkili sevk anı, UTC | `Dispatched` ise zorunlu | `2026-09-20T08:15:00Z` | D06–D08 |
| `realizedQuantity` | Decimal \| null | gerçekten çıkan miktar; sevk sonrası değişmez | `Dispatched` ise zorunlu | `30.000` | D05–D08 |
| `itemId` | uuid | `ShipmentLine.itemId` ile aynı | zorunlu | `b1f2c3d4-0000-0000-0000-000000000001` | D05–D08 |
| `skuId` | uuid | sevk edilen SKU | zorunlu | `c3d4e5f6-0000-0000-0000-000000000002` | D05–D08 |
| `uomId` | string | gerçekleşmenin kaydedildiği birim | zorunlu | `EA` | D06–D08 |
| `uomConversion` | object \| null | `{ ruleId, ruleVersion }` — yalnız referans | nullable | `{ ruleId: uom-rule, ruleVersion: v2 }` | D06–D08 |
| `warehouseReferenceId` | string | **opak** referans (3.2.0'daki hâli) | zorunlu | `wh-01` | D05–D08 |
| `canonicalContentVersion` | string | kanonik karşılaştırma alan setinin sürümü | zorunlu | `v1:lineId,sku,warehouse,realizedAt,qty,uom,kind` | D05 |
| `contractVersion` | const `v1` | şema sürümü; `sourceVersion` ile karıştırılmaz | zorunlu | `v1` | — |

`UomConversionReference`: `ruleId` (string, zorunlu), `ruleVersion` (string, zorunlu). İkisi de MDM'ye gitmek
için; dönüşüm çarpanı ve temel birim tanımı bu sözleşmede **yok**.

## 5. 3.2.0 ile karşılaştırma — ölçülmüş

İki dosya YAML olarak ayrıştırılıp ağaç ağaca karşılaştırıldı (düğüm bazında, metin bazında değil):

```
KALDIRILAN                     : 0
ANLAMI/DEĞERİ DEĞİŞEN          : 2  → /info/version "3.2.0" -> "3.3.0"
                                     /components/schemas/ShipmentDetail/allOf  (yalnız dizi kimliği)
EKLENEN (üst düzey)            : 4  → RealizedLineStatus, DemandHistoryRecordKind,
                                      UomConversionReference, RealizedShipmentLine
yol sayısı                      : 12 -> 12
şema sayısı                     : 47 -> 51
```

`ShipmentDetail/allOf` satırı, karşılaştırıcının diziyi bütün olarak ele almasından geliyor. Ayrıca ölçüldü:

```
allOf eleman sayısı             : 2 -> 2
allOf[0] ($ref ShipmentSummary) : aynı
required                        : aynı  [sourceModule, sourceType, warehouseReferenceId,
                                         shipToReference, lines, contractVersion]
kaldırılan özellik              : []
eklenen özellik                 : [realizedLines]
içeriği değişen mevcut özellik  : []
```

Yani `ShipmentDetail`'in tek deltası eklenen isteğe bağlı `realizedLines`. `lines` ve `ShipmentLine`
dokunulmadı. Ek olarak tüm `$ref`'ler çözümlendi: 319 referans, 85 tekil, **çözülemeyen 0**.

Geriye uyumluluk bu ölçümlerle duruyor: 3.2.0 istemcisi 3.3.0 cevabında bilmediği bir alanı görür ve yok
sayar; gönderdiği hiçbir istek şekli değişmemiştir.

## 6. 3.3.0'a girmeyenler

1. **İade bağlantısı.** İhtiyaç satırı 13'ün iade ayağı **ertelendi**. Sebep sözleşmesel: MOD-0186'nın kendi
   sözleşme çelişkisi açık — paket API-only derken kod ve katalog `CHANGE_STATUS` /
   `supplychain.returns.transition` ilan ediyor (R-2 sahip kararı), ve paketin yönlendirme sözleşmesi koşan
   gateway catch-all'ı ile çelişiyor. Bu çözülmeden iade bağlantısı eklemek, çelişkiyi bir sözleşmeye
   çivilemek olurdu. Çözüldükten sonra ayrı bir ek olacak.
2. **Toplu okuma uç noktası.** 3.3.0 alanları `ShipmentDetail` üzerinden, sevkiyat başına okunur. MOD-0188'in
   talep geçmişini **toplu** okuması gerekiyorsa bu ayrı bir karar: yeni bir yol eklemek ihtiyaç listesinde
   yok ve bu ek onu uydurmuyor. Üretici ve tüketici kararı.
3. **Kanonik depo eşlemesi.** Bağımlılık olarak işaretlendi, tanımlanmadı (satır 9).
4. **Dönüşüm kuralının kendisi.** Yalnız referans (satır 12).
5. **Stockout.** Kapsam dışı.

## 7. Onay

Bu dal yalnız sözleşme ve bu notu taşır. Kod, sözleşme onaylandıktan sonra ayrı iş. PR'ı Ali açacak.
