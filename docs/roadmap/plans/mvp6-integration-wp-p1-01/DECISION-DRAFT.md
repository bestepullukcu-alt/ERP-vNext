---
status: draft
kind: pack-revision-decision-draft
work_package: WP-P1 (MVP-6 entegrasyon, Dalga 2 ön koşulu)
modules: [MOD-0183, MOD-0186]
consumed: [INVENTORY-BUNDLE 1.5.0, PRODUCT-MASTER 1.1.0-draft.16, WAREHOUSE-OUTBOUND 1.0.0]
measured_at: origin/main 1afd37843 (2026-10-22)
---

DURUM: TASLAK — **kısmen kararlaştırıldı (Natig, yönetim, 2026-10-22): G1, G5, G6, G7, G8, G9, G10 öneriler gibi kabul edildi
(§3a).** G2, G3, G4 MVP-1 CT'nin cevabını bekliyor. Paket yamaları yalnız kararlaştırılmış kısımlar için yazıldı (§8).

# WP-P1 — MOD-0183 / MOD-0186 paket revizyonu: Inventory'ye yazma (karar taslağı)

> Öneri, onay değil. Kod, paket durumu ya da dağıtım yetkisi vermez. Ölçüm salt okunur kod / sözleşme okumasıdır;
> **çalışma testi yapılmadı**. Inventory (MOD-0173…0177) ve MDM / PRODUCT-MASTER başka sahiplerindir: oralarda gereken her
> değişiklik XMC-001 ister ve burada yalnız soru olarak durur.

## 1. Neden

Sahip kararları (Beste, 2026-10-21; `docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md` §2.1, §4.1):

- **W-4:** satış stok çıkışını (`GOODS_ISSUE_SALES`) MOD-0183 yazar, sevkiyat `Dispatched` olduğunda; tek yazıcı. Inventory
  reddederse sevkiyat `Dispatched` olmaz; geri alma `REVERSAL` ile; rezervasyon varsa tüketilir; iki aşamalı tasarım (hareket →
  durum geçişi; idempotent tekrar) MVP-6 CT ve MVP-1 CT onayıyla.
- **E-7:** sevkiyat satırında MDM ürün doğrulaması; müşteri iadesi Inventory'ye `CUSTOMER_RETURN`, `toStockStatus=QUARANTINE`.
- **E-6:** gün sınırı UTC.

Bugünkü paketler bunu **yasaklıyor** — kod bu paketlerle yazılamaz (Çekirdek 7):

| # | Olgu | Kaynak |
|---|---|---|
| P1 | MOD-0183 sahip olmadığı şey: "Inventory balance, reservation or movement truth; no shadow stock" | `MOD-0183-shipment-tracking-pod.md:62` |
| P2 | MOD-0183 bağımlılığı: INVENTORY "reads only in this slice" | `MOD-0183-shipment-tracking-pod.md:152` |
| P3 | MOD-0183: "Inventory is consumed by API/mock only; no database/internal-type sharing and no second balance" | `MOD-0183-shipment-tracking-pod.md:173` |
| P4 | MOD-0186 sahip olmadığı: "inventory movement/balance" | `MOD-0186-reverse-logistics.md:36` |
| P5 | MOD-0186: INVENTORY "movement truth"u değiştirmez | `MOD-0186-reverse-logistics.md:80` |

P3 W-4 ile çelişmez (API üzerinden yazma, ikinci bakiye yok); P1, P2, P4, P5 revizyonla değişir.

## 2. Okunan sözleşme ve kod olguları (`1afd37843`)

| # | Olgu | Kaynak |
|---|---|---|
| F1 | `postMovement` zorunlu alanları: `idempotencyKey, movementType, itemId, skuId, skuLevel, quantity, uomId, sourceModule, sourceType, transactionDate, postingDate`; isteğe bağlı `fromWarehouseId/fromLocationId, lotId, serialIds, fromStockStatus, toStockStatus, sourceDocumentId, sourceLineId, fefoBypassReason` | `inventory-bundle.openapi.yaml:345-373` |
| F2 | Ret kodları (422): `LOT_UNDER_RECALL`, `LOT_RETEST_DUE`, `INSUFFICIENT_STOCK`, `FEFO_BYPASS_REASON_REQUIRED`, `FEFO_ORDER_VIOLATION`, `LOT_BELOW_MIN_REMAINING_SHELF_LIFE`, `ITEM_NOT_ACTIVE`, `SKU_LEVEL_NOT_STOCKABLE`, `PRODUCT_MASTER_DATA_INCOMPLETE`; 503 `PRODUCT_MASTER_UNAVAILABLE` | `inventory-bundle.openapi.yaml:16-21,33-46,518-540` |
| F3 | `MovementRequest`'te **rezervasyon alanı yok**; rezervasyon yalnız `POST /reservations` ve `POST /reservations/{id}/release` (If-Match) | `inventory-bundle.openapi.yaml:203-246,345-373` |
| F4 | `REVERSAL` isteği `sourceModule = "MOD-0173"`, `sourceType = "REVERSAL"` ve `sourceDocumentId = orijinal transactionId` ister; başka kaynak modülü reddedilir | `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Rules/ReversalRules.cs:14,16,20-30` |
| F5 | Hareket ucu `[HasPermission("inventory.movements.create")]` | `…/Api/Controllers/InventoryController.cs:27-28`; `…/Infrastructure/Authorization/InventoryPermissions.cs:14` |
| F6 | Sevkiyat satırı: `LineNumber, ItemId, SkuId, Quantity, UomId, InventoryReferenceId?` — **`skuLevel`, `lotId`, `locationId` yok**; depo yalnız başlıkta (`WarehouseReferenceId`) | `…/Application/Features/Shipments/ShipmentModels.cs:5-9` |
| F7 | İade satırı: `ShipmentLineNumber, Quantity, UomId` — kalem/SKU/lot sevkiyat satırından gelir | `…/Domain/Features/Returns/ReturnLine.cs:2`; `ReturnEntitlement.cs:16` |
| F8 | SHIPMENT-BUNDLE 3.3.0, MOD-0183'ün kendi dondurulmuş sözleşmesi | `shipment-bundle.openapi.yaml:15` |
| F9 | PRODUCT-MASTER `/validate` (`validateRefs`) var ama sözleşme **`1.1.0-draft.16`** (dondurulmamış); satır başına `lifecycleStatus`, seviyesiz satır biçimleri | `product-master-bundle.openapi.yaml:19,29,504-506` |
| F10 | INVENTORY 1.4.0 açıklaması "MVP-6 shipment issues included" diyerek sevkiyat çıkışını `LOT_UNDER_RECALL` kapsamına alıyor | `inventory-bundle.openapi.yaml:21` |

## 3. Kararı gereken boşluklar

| # | Boşluk | Seçenekler | Öneri (onay değil) | Karar sahibi |
|---|---|---|---|---|
| G1 | **Lot / lokasyon / seviye nereden?** Çıkış `skuLevel` ister, lot izli üründe `lotId`, FEFO dışı lotta `fefoBypassReason` (F1, F6). Sevkiyat satırı bunları taşımıyor | (a) SHIPMENT-BUNDLE 3.4.0 (ekleme): satıra `skuLevel`, `lotId`, `fromLocationId`, `fefoBypassReason` (isteğe bağlı); depo modülü olan kiracıda MVP-5 toplama sonucundan, olmayanda kullanıcı girer; (b) `skuLevel` PRODUCT-MASTER'dan çözülür, lot Inventory'nin FEFO önerisinden alınır (FEFO öneri ucu INVENTORY'de yok) | **(a)**, `skuLevel` ayrıca G6'daki ürün doğrulamasıyla çapraz kontrol. MOD-0183 kendi sözleşmesinin sahibi; ekleme ayrı sürümlü tek yazıcı Faz A değişikliği (`MOD-0183…md:134-135`) | MVP-6 CT + Natig; MVP-5 (MVP-1 CT) bilgi |
| G2 | **Rezervasyon tüketimi** — hareket isteği rezervasyonu tanımıyor (F3) | (a) INVENTORY'ye ekleme: `MovementRequest.reservationId` + aynı işlemde tüketim (MOD-0173 değişikliği); (b) önce `release`, sonra `GOODS_ISSUE_SALES` — iki çağrı, arada başka biri stoğu alabilir; (c) ilk dilimde rezervasyon yok, yalnız `available` üzerinden çıkış | **(a)** hedef, **(c)** ilk dilim. (b) yarış açar | **MVP-1 CT / MOD-0173 sahibi (XMC-001)** |
| G3 | **Geri alma kimin adına?** REVERSAL yalnız `sourceModule=MOD-0173` ile kabul ediliyor (F4); brief "MOD-0183 yazar" diyor | (a) MOD-0183 REVERSAL'ı `MOD-0173`/`REVERSAL` ile gönderir, `sourceDocumentId` = orijinal; izlenebilirlik `documentGroupId = shipmentId` ile; (b) Inventory REVERSAL'a "orijinalin kaynak modülü" yolunu ekler | **(a)** — Inventory kuralına uyar, sözleşme değişmez; MVP-1 CT'nin "REVERSAL'ı başka modül gönderebilir mi" onayı gerekir | **MVP-1 CT** |
| G4 | **İki aşama ve çok satır** — Inventory'de toplu hareket ucu yok; N satırlı sevkiyatta k. satır reddedilirse ilk k−1 satır yazılmış olur | (a) Satır başına deterministik anahtar `{shipmentId}:{lineNumber}:dispatch:v1`; sevkiyat `DispatchPending` (iç durum, wire'da yok) → tüm satırlar 201/200 → `Dispatched`; bir satır reddedilirse yazılmış satırlar REVERSAL ile geri alınır, sevkiyat `Planned`'a döner, ret nedeni kullanıcıya; çökme sonrası aynı anahtarlarla kaldığı yerden devam; (b) INVENTORY'ye toplu/atomik hareket ucu (MOD-0173 değişikliği) | **(a)** ilk dilim, (b) sonra. (a) bir iç durum ekler: wire durum listesi değişmez (SHIPMENT-BUNDLE 3.3.0 `Status` kümesi aynı) | MVP-6 CT + **MVP-1 CT** (brief: iki aşamalı tasarım ikisinin onayıyla) |
| G5 | **Kimin token'ıyla?** Hareket ucu `inventory.movements.create` ister (F5) | (a) kullanıcının token'ı — sevk eden her kullanıcıya Inventory izni (BL-652'deki "başka modülün iznini ver" deseni; ara çözümle tutarlı: Rol → İzin); (b) servis kimliği (S2S) — Auth/Inventory'de yeni istemci + audience | **(a)** şimdi (BL-652 ara çözümüyle aynı: rol ataması), (b) BL-652 Seçenek 2 ile birlikte | Natig (BL-652 kararı ile tutarlılık); Inventory sahibi bilgi |
| G6 | **Ürün doğrulaması** — PRODUCT-MASTER `validateRefs` taslak (F9) | (a) taslak `1.1.0-draft.16`'ya karşı yaz, dondurulunca sabitle; (b) dondurulmasını bekle | **(b)** — taslağa karşı kod sonradan kırılır; dilim DL-4 ayrı | MDM / PRODUCT-MASTER sahibi (dondurma tarihi) |
| G7 | **Ret metinleri** — Inventory ret kodları kullanıcıya yerelleştirilmiş gösterilecek (7 dil) | SHIPMENT-BUNDLE 3.4.0'a `INVENTORY_REFUSED` + `details.inventoryCode` (ekleme) ya da her kodu ayrı wire kodu yapmak | **`INVENTORY_REFUSED` + iç kod**, metinler Shipments ekranının `.resx` dosyalarında (7 dil) | MVP-6 CT |
| G8 | **İade satırının stok alanları** — iade satırı lot / depo / lokasyon taşımıyor (F7) | (a) `CUSTOMER_RETURN` için lot ve kalem orijinal çıkış hareketinden (`sourceLineId`) okunur, varış deposu/lokasyonu iade alımında girilir — RETURNS sözleşmesine ekleme; (b) yalnız depo, lot boş | **(a)** — recall iade bağlama akışı lotu ister (MVP-1 "karantina ve recall iade bağlama"); `InventoryTransactionReferenceId` alanı zaten var (`MOD-0186…md:51-52`) | MVP-6 CT; MVP-1 CT bilgi |
| G9 | **Dispatched olayı** — MVP-5, MOD-0183'ün `Dispatched` olayıyla outbound'u `Shipped` yapacak; brief "biçimi değiştirmeden önce haber verin" | Olay biçimi **değişmez**; yalnız zamanı "tüm satırlar yazıldıktan sonra" olur | Öneri bu; MVP-1 CT'ye bilgi | MVP-6 CT |
| G10 | **Depo modülü olmayan kiracı** — "sevkiyat stoğu sizin hareketinizle düşer" | Sevkiyat yaratılırken satırın lot/lokasyonu kullanıcıdan (G1-a); depo modülü olan kiracıda MVP-5'in `ReadyToShip`'i doldurur | Öneri bu; MVP-5 akışı yazılınca eşleme ayrıca | MVP-6 CT + MVP-1 CT |

## 3a. Verilen kararlar (Natig, yönetim, 2026-10-22)

| # | Karar |
|---|---|
| G1 | SHIPMENT-BUNDLE 3.4.0 (yalnız ekleme): sevkiyat satırına isteğe bağlı `skuLevel`, `lotId`, `fromLocationId`, `fefoBypassReason`. Depo modülü olmayan kiracıda kullanıcı girer; depo modülü olan kiracıda MVP-5 toplama sonucu doldurur. `skuLevel` ürün doğrulamasıyla (G6) çapraz kontrol edilir |
| G5 | Hareket kullanıcının token'ıyla gönderilir; sevk eden rol `inventory.movements.create`'i Rol → İzin ekranından alır (BL-652 ara çözümüyle aynı; şablon değil). Servis kimliği BL-652 Seçenek 2 ile birlikte, sonra |
| G6 | Ürün doğrulaması PRODUCT-MASTER dondurulunca yazılır; DL-4 ayrı dilim |
| G7 | Inventory reddi wire'da tek kod `INVENTORY_REFUSED`, Inventory kodu `details.inventoryCode`'da; kullanıcı metni Shipments ekranında 7 dilde |
| G8 | İadede kalem / SKU / lot orijinal çıkış hareketinden okunur; varış deposu / lokasyonu iade alımında girilir (RETURNS sözleşmesine ekleme) |
| G9 | `Dispatched` olayının biçimi değişmez; yalnız bütün satırlar yazıldıktan sonra yayımlanır |
| G10 | G1 ile karşılanır (depo modülü olmayan kiracıda kullanıcı girer) |

Açık: **G2, G3, G4** (MVP-1 CT). DL-2 (çıkış çekirdeği) ve DL-6 (rezervasyon) bunlara bağlı.

## 3b. MVP-1 Control Tower cevapları (2026-10-23, Natig iletti; kod ve sözleşme ölçümüne dayalı)

| # | Cevap |
|---|---|
| G2 | INVENTORY değişikliği **gerekmiyor**: hareketin (`sourceModule`, `sourceDocumentId`, `sourceLineId`) üçlüsü aktif bir rezervasyonla aynıysa aynı işlemde tüketilir (`ReservationPositionOperations.cs:215-232`, ASSUMPTION-0173-15). `MovementRequest.reservationId` yalnız MVP-5 (MOD-0178) rezervasyonu için; MOD-0173 sahibi W-5 ile, MVP-5 S2'den önce ekler. "Önce release, sonra issue" önerilmiyor. |
| G3 | **Onay.** REVERSAL `sourceModule=MOD-0173`, `sourceType=REVERSAL`, `sourceDocumentId`=orijinal `transactionId`, `documentGroupId`=`shipmentId`, anahtar `{shipmentId}:{lineNumber}:reversal:v1`. Geri alınan çıkışın tükettiği rezervasyon geri gelmez; stok AVAILABLE döner. |
| G4 | İlk dilim için satır başına anahtar + REVERSAL telafisi **onay**; toplu/atomik uç gerekirse MOD-0173 FU'su. Dört koşul: (1) `transactionDate`/`postingDate` ilk denemede saklanıp her denemede aynen gönderilir (`RequestFingerprint.cs:42-43`); (2) yeniden denemede önce anahtarın işlenip işlenmediğine bakılır, `IDEMPOTENCY_KEY_REUSED` ret değil bütünlük hatası; (3) çökme sonrası devam kullanıcı eylemiyle (arka planda token yok), arka plan iş yalnız durum kontrolü; (4) telafi REVERSAL'ı da başarısızsa sevkiyat görünür hata durumunda kalır, sessizce Planned'a dönmez. |
| Olay | `Dispatched` olayının biçimi değişmeden bütün satırlar yazıldıktan sonra yayımlanması uygun (MVP-5 outbound'u buna göre Shipped yapar). |
| Bilgi 2 | MVP-1, CentralAudit outbox yarışı için ortak kodda FU açıyor; SupplyChain'deki ön-yaratma o gelene kadar uygun. |

**Ek notlar (MVP-1 CT, 2026-10-23, kodda doğrulandı):** (1) rezervasyon eşleşmesi aynı SKU içinde birebir metin
karşılaştırması; boş `sourceDocumentId` hiç eşleşmez; yalnız AVAILABLE'ı azaltan hareket tüketir. (2) süresi dolan
rezervasyonlar tüketimden hemen önce aynı işlemde kapatılır (`InventoryLedgerRepository.cs:291-293`); sevkiyat rezervasyonunda
`expiresAt` gönderilmez. (3) G4/1 REVERSAL için de geçerli: geri almanın tarihleri de saklanıp aynen gönderilir; aynı orijinale
farklı anahtarla ikinci geri alma 422 `ALREADY_REVERSED`. (4) "Önce anahtara bak" = aynı isteği aynı anahtar ve tarihlerle
yeniden göndermek; işlenmiş istek `idempotentReplay: true` döner (ret değil); reddedilmiş istek saklanmaz, yeniden gönderilince
kurallar baştan çalışır.

Pakete işlenişi: MOD-0183 §23.4 (karar), §23.1 anahtar, §23.5 S23-8…S23-12, teslim dilimleri DL-1 (3.4.0 + çekirdek) ve
DL-2 (metinler); ürün doğrulaması ve MVP-5 rezervasyonu dilim değil. Koşul (4) için dondurulmuş yaşam döngüsünde
`Planned → Exception` oku olmadığından görünür durum `ShipmentDetail.dispatchState` (3.4.0, ekleme) ve 409
`DISPATCH_COMPENSATION_FAILED` ile verilir (CT önerisi). MOD-0186 §34: lot ve kalem, MOD-0183'ün satır başına sakladığı çıkış
`transactionId`/`lotId`'den okunur; DL-1, MOD-0183 DL-1'den sonra.

## 3c. MVP-1 CT CT-APR-02 incelemesi ve düzeltilmiş kural (2026-10-23, Natig iletti 2026-10-26)

Sonuç: MOD-0183 **UYGUN DEĞİL** (1 ENGEL, 6 ORTA, 4 DÜŞÜK), MOD-0186 **KOŞULLU** (0 ENGEL, 4 ORTA, 2 DÜŞÜK); inceleme
`d0366fa73`'te, paket özetleri `c0111bcb8`'de aynı. Bulgu dosyaları: `docs/records/reviews/MOD-0183/2026-10-23-febf6e35.md`,
`docs/records/reviews/MOD-0186/2026-10-23-6811f18c.md` (commit `6c5cec410`, 2026-10-26). r3 (2026-10-26) bulgu dosyalarına göre kalan noktaları kapattı: MOD-0183 F-3 (REVERSAL alanları INVENTORY koduna göre: `lotId`/`serialIds`/`from*`/`to*` gönderilmez, 400 bütünlük hatası), F-4 (çıkışın izin/ekran/denetim), F-5 (403 kodu ayrı), F-6 (§5 DL-1 istisnası), F-7 (dilim tablosu başlık adlarıyla, `slices()` → DL-1, DL-2), F-8, F-9, F-10, F-11; MOD-0186 F-2 (§25 dosya listesi), F-3 (iki dilim), F-6.

- **ENGEL (G3/G4 cevabından):** sabit anahtar telafiden sonra yeniden sevkte geri alınmış eski hareketi `idempotentReplay`
  ile döndürür. **Düzeltilmiş kural (MVP-1 CT):** saklı deneme numarası `n`; anahtarlar `…:dispatch:a{n}` /
  `…:reversal:a{n}`; `n` yalnız telafisi tamamlanmış deneme kapanınca artar; tarihler deneme başına saklanır. G3/G4'ün
  diğer koşulları geçerli.
- F-2 tek aktif deneme; F-3/F-4 REVERSAL gövdesi ve başarısız telafiden çıkış; F-5 / MOD-0186 F-4 `X-Legal-Entity-Id`
  sevkiyatın / iadenin doğrulanmış LE'si, INVENTORY 403 → 403 (503 değil); F-10 `inventoryReferenceId` v1'de kullanılmaz.
- MOD-0186: ilk denemede istek, tarih ve hedef lokasyon saklanır; kısmi başarı sonrası tamamlanan satırlar değişmez; seri
  takipli iade v1'de yok; `issueTransactionId` önerisi F-5 ile uyumlu.
- Ortak kabul senaryosu: kısmi sevk → telafi → yeniden sevk → kısmi iade → tekrar (MOD-0183 S23-16, MOD-0186 R34-8).

İşlendiği yer: MOD-0183 §23 r2, MOD-0186 §34 r2, `SHIPMENT-BUNDLE-3.4.0.patch` + PROPOSAL §3b (2026-10-26). §8'deki
`MOD-0183-section-23.patch` / `MOD-0186-section-34.patch` tarihseldir; güncel metin paketlerdedir.

## 4. Önerilen paket değişiklikleri (kararlardan sonra yazılacak — özet)

**MOD-0183**

- §2: "Does not own … movement truth" kalır (bakiye Inventory'nin); **"Owns" ekine:** sevkiyat çıkışının *isteği* ve sonucu
  (hareket kimlikleri), tek yazıcı `GOODS_ISSUE_SALES` (W-4). §7: INVENTORY satırı "reads only" → "reads + `postMovement`
  (GOODS_ISSUE_SALES; REVERSAL G3)". PRODUCT-MASTER satırı (G6).
- §8: yeni kurallar — satır başına deterministik `Idempotency-Key` (G4), `X-Correlation-Id` iletimi (E-1), UTC `postingDate`
  (E-6), `sourceModule: MOD-0183`, `sourceType: SHIPMENT`, `sourceDocumentId: shipmentId`, `sourceLineId: lineNumber`.
- §12 / §13: ret kodları → `INVENTORY_REFUSED` (G7), Inventory erişilemez → 503 ve **durum değişmez**, kısmi yazım →
  REVERSAL telafisi (G4).
- §14: kullanıcı rolüne `inventory.movements.create` (G5-a) — Rol → İzin, şablon değil.
- §16 / §17: kabul turları (brief): `Dispatched` → stok düştü; recall altındaki lot → sevkiyat reddedildi; yeniden deneme
  ikinci hareket yazmaz; kısmi ret → REVERSAL + `Planned`. Her yeni kural için sabotaj kanıtı.
- Yeni §23 "Inventory stock issue at dispatch (W-4)" + `## Teslim dilimleri` (CT-APR-02).

**MOD-0186**

- §2 / §7: "Does not own … inventory movement" kalır; ekine: iade alımında `CUSTOMER_RETURN` isteği, `toStockStatus=QUARANTINE`
  (E-7), sonuç `InventoryTransactionReferenceId`'ye yazılır.
- Yeni §34 "Customer return to Inventory (E-7)" + teslim dilimleri.

**SHIPMENT-BUNDLE 3.4.0** (G1, G7) ve RETURNS eki (G8) ayrı, sürümlü, tek yazıcı sözleşme değişiklikleridir; paket
revizyonuyla aynı PR'da önerilir, ayrı onaylanır.

## 5. Önerilen teslim dilimleri (öneri)

| dilim | ad | kapsar | ön koşul |
|---|---|---|---|
| DL-1 | SHIPMENT-BUNDLE 3.4.0 | satır stok alanları, `INVENTORY_REFUSED` | G1, G7 |
| DL-2 | çıkış çekirdeği | iki aşama, deterministik anahtar, tek satır + çok satır, REVERSAL telafisi | G3, G4, G5, DL-1 |
| DL-3 | iade → karantina | `CUSTOMER_RETURN` (QUARANTINE), referans yazımı | G8 |
| DL-4 | ürün doğrulaması | `validateRefs` sevkiyat satırında | G6 (dondurma) |
| DL-5 | ekran metinleri | ret nedenleri 7 dilde, Shipments ekranı | DL-2 |
| DL-6 | rezervasyon tüketimi | `reservationId` | G2-a (Inventory değişikliği) |

## 6. Onay yolu

Kararlar (§3) → paket yamaları (MOD-0183 §23, MOD-0186 §34 + değişen satırlar) ve sözleşme önerileri → **CT-APR-02 bağımsız
inceleme** (MVP-1 CT; yazandan ayrı oturum) → **CT-APR-01** satırı: risk **yüksek** (entegrasyon + ürün kodu yolu), onaylayıcı
`high: evet` ve yazar olmayan biri (Beste ya da Natig; `execution/approvals/approvers.tsv`).

## 7. Ölçülmeyenler

- Çalışma testi yok: Inventory'ye canlı hareket gönderilmedi (lane portu kararı açık: 5000–5070 filo kuralı).
- MVP-5 (MOD-0178) toplama / `ReadyToShip` akışı yazılmadı; G10 eşlemesi varsayımdır.
- Inventory'nin `GOODS_ISSUE_SALES` için `sourceModule`'ü okuyup okumadığı (kural "never by sourceModule" diyor, F10) yalnız
  sözleşme metninden; kod okunmadı.
- `verify_module_id.py --discover` koşulmadı (mevcut kimlikler; yeni kimlik önerilmiyor).

## 8. Bu dalda hazırlanan yamalar (kararlaştırılmış kısımlar; uygulanmadı)

| dosya | kapsar | bekleyen |
|---|---|---|
| `SHIPMENT-BUNDLE-3.4.0-PROPOSAL.md` | G1 satır alanları, G7 `INVENTORY_REFUSED` | sözleşme tek yazıcısı + CT-APR-02 |
| `MOD-0186-section-34.patch` | E-7 iade → `CUSTOMER_RETURN` (QUARANTINE), G5, G8 | CT-APR-02 → CT-APR-01 |
| `MOD-0183-section-23.patch` | G1, G5, G7, G9, G10 + DL tablosu; DL-2 / DL-6 "G2–G4 bekliyor" olarak işaretli | G2–G4, sonra CT-APR-02 → CT-APR-01 |

Yamalar `git apply --check` ile denendi; paket dosyaları bu dalda **değiştirilmedi** (tek paket yazıcısı kuralı).

## 9. WP-B4 ölçümü ve DL-1 yaması (2026-10-22, `origin/main` `91a1d6031`)

**WP-B4 — Inventory okuma istemcisi.** `IInventoryReadClient` / `InventoryReadClient` yalnız tanım olarak var
(`Application/Features/SourceIntake/Contracts/SourceClients.cs:51`, `Infrastructure/SourceIntake/ReadOnlySourceClients.cs:22`);
**hiçbir üretim kodu onu kullanmıyor** ve kararlaştırılmış hiçbir kural (G1–G10) bir Inventory okumasına ihtiyaç duymuyor:
çıkış ve iade ret kararını Inventory'nin kendisi veriyor. Öneri: DI'a **kaydedilmez**; bir kural ya da ekran onu isteyince kaydedilir.

**Bulgu (G8'i etkiler).** INVENTORY `getTransactions` yalnız `itemId, lotId, locationId, from, to` ile süzer
(`inventory-bundle.openapi.yaml:178-190`); `sourceDocumentId` / `documentGroupId` ile arama yok. Bu yüzden iade, orijinal çıkış
hareketini Inventory'den bulamaz. **DL-2 her satırın `transactionId`'sini (`MovementResponse.transactionId`) ve `lotId`'sini
sevkiyatta saklamalıdır**; DL-3 bunları sevkiyattan okur. MOD-0183 §23.1'e DL-2 yamasında eklenir.

**DL-1 yaması.** `SHIPMENT-BUNDLE-3.4.0.patch`: `version 3.3.0 → 3.4.0`; `ShipmentLine` + `skuLevel`, `lotId`, `fromLocationId`,
`fefoBypassReason`; `TransitionReturnCommand` + `toWarehouseId`, `toLocationId`; `Unprocessable` örnekleri `INVENTORY_REFUSED`,
`SHIPMENT_LINE_STOCK_INCOMPLETE`, `RETURN_SOURCE_MOVEMENT_MISSING`. Yalnız ekleme: kaldırılan tek satır sürüm numarası; YAML
ayrıştırılıyor; `git apply --check` geçiyor. **Tek başına uygulanmaz:** `ContractErrorCodeParityTests` sözleşmedeki kodlarla
koddaki `ContractErrorCodes`'u eşit tutar, yeni üç kod DL-2 koduyla aynı değişiklikte gelir. 3.3.0'daki gibi `SHA256SUMS` ve
uygulama kaydı (`APPLIED.md`) da o değişiklikte güncellenir.
