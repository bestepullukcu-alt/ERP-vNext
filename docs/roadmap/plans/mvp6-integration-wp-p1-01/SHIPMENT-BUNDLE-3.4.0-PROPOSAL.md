---
status: draft
kind: contract-change-proposal
contract: SHIPMENT-BUNDLE (docs/analysis/contracts/shipment-bundle.openapi.yaml)
from_version: 3.3.0
to_version: 3.4.0 (additive)
measured_at: origin/main 1afd37843 (2026-10-22)
---

DURUM: TASLAK — öneri, onay değil. Dondurulmuş sözleşme dosyası **değiştirilmedi**; değişiklik ayrı sürümlü, tek yazıcılı bir
Faz A adımıdır (`MOD-0183-shipment-tracking-pod.md:134-135`).

# SHIPMENT-BUNDLE 3.4.0 — sevkiyat satırı stok alanları ve Inventory reddi (öneri)

Kaynak kararlar: G1, G7, G8 (Natig, 2026-10-22; `DECISION-DRAFT.md` §3a). Hepsi **yalnız ekleme**: var olan yol, parametre,
zorunlu alan, tür, enum değeri ve durum kodu değişmez; bugünkü istemciler aynen çalışır.

## 1. `ShipmentLine` — dört isteğe bağlı alan (G1)

Bugün (`shipment-bundle.openapi.yaml:3124-3134`): `additionalProperties: false`, zorunlu `lineNumber, itemId, skuId, quantity,
uomId`, isteğe bağlı `inventoryReferenceId`.

| alan | tür | anlamı |
|---|---|---|
| `skuLevel` | `[string, 'null']`, INVENTORY `SkuLevel` değerleri | Çıkış hareketinin zorunlu `skuLevel`'ı (INVENTORY `MovementRequest`, `inventory-bundle.openapi.yaml:347`). Verilmezse PRODUCT-MASTER'dan çözülür (G6 dondurulunca); verilirse onunla çapraz kontrol edilir |
| `lotId` | `[string, 'null']` | Lot izli üründe çıkışın lotu. Depo modülü olmayan kiracıda kullanıcı girer; depo modülü olanda MVP-5 toplama sonucu doldurur (G10) |
| `fromLocationId` | `[string, 'null']` | Çıkış lokasyonu; depo başlıkta (`warehouseReferenceId`) kalır |
| `fefoBypassReason` | `[string, 'null']`, 10–1000 | FEFO'nun ilk uygun lotu dışındaki lot için gerekçe (INVENTORY 1.3.0 `fefoBypassReason`); aynen iletilir |

Kural: dört alan da **Dispatched'a geçişte** okunur; `Draft` / `Planned`'da boş olabilir. Lot izli bir üründe `lotId` boşsa
geçiş Inventory'ye gitmeden reddedilir (yeni 422 kodu `SHIPMENT_LINE_STOCK_INCOMPLETE`, `details.lineNumber`).

## 2. `INVENTORY_REFUSED` — Inventory reddi tek wire kodu (G7)

- `Unprocessable` yanıtına yeni örnek: `{ error: { code: INVENTORY_REFUSED, message: …, details: { lineNumber, inventoryCode },
  correlationId }, contractVersion: v1 }`. `inventoryCode` INVENTORY'nin kendi 422 kodudur (`LOT_UNDER_RECALL`, `LOT_RETEST_DUE`,
  `INSUFFICIENT_STOCK`, `FEFO_BYPASS_REASON_REQUIRED`, `FEFO_ORDER_VIOLATION`, `LOT_BELOW_MIN_REMAINING_SHELF_LIFE`,
  `ITEM_NOT_ACTIVE`, `SKU_LEVEL_NOT_STOCKABLE`, `PRODUCT_MASTER_DATA_INCOMPLETE`); Inventory'nin `details`'i `details.inventory`
  altında aynen taşınır.
- `Error.details` zaten açık (`additionalProperties: true`, `shipment-bundle.openapi.yaml:3481-3492`): şema değişmez, yalnız
  örnek ve açıklama eklenir.
- Mevcut `INVENTORY_REFERENCE_INVALID` (aynı yanıtın `inventory` örneği) okuma doğrulaması için kalır; yazma reddi
  `INVENTORY_REFUSED`'dir.
- Inventory erişilemez / 5xx → mevcut `DependencyUnavailable` (503). Durum değişmez.
- Kullanıcı metni wire'da değil, Shipments ekranında: `inventoryCode` başına bir `.resx` anahtarı, 7 dil.

## 3. `TransitionReturnCommand` — iade alımı varış yeri (G8)

Bugün (`shipment-bundle.openapi.yaml:3337-3345`): `targetStatus, occurredAt`, isteğe bağlı `inventoryTransactionReferenceId`,
`dispositionCode`.

| alan | tür | anlamı |
|---|---|---|
| `toWarehouseId` | `[string, 'null']` | `Received`'e geçişte zorunlu (iş kuralı, şema değil): iadenin girdiği depo |
| `toLocationId` | `[string, 'null']` | İsteğe bağlı varış lokasyonu |

Kalem / SKU / seviye / lot orijinal çıkış hareketinden okunur (MOD-0186 §34.1); istemci göndermez. Orijinal çıkış hareketi
olmayan satır için yeni 422 kodu `RETURN_SOURCE_MOVEMENT_MISSING` (`details.shipmentLineNumber`).

Not: `inventoryTransactionReferenceId` alanı istemciden gelmeye devam eder mi, yoksa sunucu mu yazar — bugünkü anlamı
"Frozen INVENTORY movement reference". §34 ile sunucu yazar; istemcinin değeri yok sayılır mı reddedilir mi, sözleşme
yazıcısının kararıdır. **Karar (revizyon r2, 2026-10-26, MOD-0186 §34.2):** `Received` geçişinde istemci değeri 400
`INVALID_REQUEST` ile **reddedilir** (sessizce yok sayılmaz); sunucu satır başına yazar. Diğer geçişlerde alan bugünkü gibidir.

## 3a. `ShipmentDetail.dispatchState` ve 409 `DISPATCH_COMPENSATION_FAILED` (G4 koşul 4, 2026-10-23)

MVP-1 CT'nin G4 koşulu: telafi REVERSAL'ı başarısızsa sevkiyat görünür bir hata durumunda kalır. Dondurulmuş yaşam döngüsünde
`Planned → Exception` oku yok, bu yüzden `status` değişmez; yerine salt-okunur `dispatchState` (`None` / `InProgress` /
`CompensationFailed`, isteğe bağlı) eklenir ve sonraki her geçiş 409 `DISPATCH_COMPENSATION_FAILED` (`details.lines`) alır.
Yalnız ekleme; var olan istemciler etkilenmez.

## 3b. CT-APR-02 incelemesinden gelen ekler (MVP-1 CT, 2026-10-23; revizyon r2, 2026-10-26)

| Ek | Tür | Neden |
|---|---|---|
| `ShipmentLine.issueTransactionId` | `[string, 'null']`, salt-okunur | MOD-0186 F-5: iade, tamamlanmış sevk denemesinin çıkış hareketini sevkiyat seam'i üzerinden okur; create'te yok sayılır |
| `dispatchState` açıklaması | yalnız metin | MOD-0183 F-2 / F-4: `InProgress` iken iptal ve POD 409 `DISPATCH_IN_PROGRESS`; `CompensationFailed`'dan çıkış `Dispatched` ya da `Cancelled` ile, önce REVERSAL'lar yeniden gönderilir |
| 409 `DISPATCH_IN_PROGRESS` | yeni örnek kod | MOD-0183 F-2: sevkiyat başına tek aktif deneme |
| 409 `RETURN_RECEIPT_IN_PROGRESS` | yeni örnek kod | MOD-0186 F-1: açık alım denemesi başka saklı değerlerle, ya da başka bir komut kilidi tutuyor |

Hepsi ekleme; var olan istemciler etkilenmez. İki yeni kod `ContractErrorCodeParityTests` için DL-1 koduyla aynı değişiklikte
sabit olarak eklenir.

## 3c. r4 ekleri (2026-10-26, bağımsız r3 incelemesi + Natig kararları)

| Ek | Tür | Neden |
|---|---|---|
| `responses.Forbidden` (403) ve `responses.InternalError` (500) | yeni paylaşılan yanıtlar | MOD-0183 r3 F-3: `transitionShipment` ve `transitionReturn` INVENTORY 403 → 403 ve INVENTORY 400 → 500 döndürüyor; artık bildirilmiş |
| `transitionShipment` / `transitionReturn` → `'403'`, `'500'` | eklemeli yanıt | aynı |
| `transitionReturn` açıklaması | metin | MOD-0186 r3 F-1: "No Inventory HTTP" yerine "tek Inventory çağrısı Received'deki CUSTOMER_RETURN"; DL-1 öncesi sevkiyat `NotPosted` / `PRE_WP_P1` |
| `TransitionReturnCommand.unpostedLines` | eklemeli alan | MOD-0186 r3 F-2: kalıcı INVENTORY reddi (`ITEM_NOT_ACTIVE`, `SKU_LEVEL_NOT_STOCKABLE`, `UNKNOWN_ITEM`) için çıkış |
| `ReturnResponse.lines[]` | eklemeli, salt-okunur | MOD-0186 r3 F-1: satır başına `inventoryTransactionReferenceId`, `inventoryPosting`, `inventoryPostingReason` |

`returns-semantics-v3.0.0.md` ("Inventory and Warehouse HTTP calls ZERO") 3.0.0 için tarihsel kalır; 3.4.0'ın normatif metni
`transitionReturn` açıklaması ve MOD-0186 §34'tür. Yeni kodlar `ContractErrorCodeParityTests` için DL-1 koduyla aynı değişiklikte.

## 4. Değişmeyenler

`ShipmentStatus` kümesi, `ShipmentEventPayload` ve `LifecycleEventEnvelope` (G9: `ShipmentDispatched` biçimi aynı, yalnız
zamanı bütün satırlar yazıldıktan sonra), bütün yollar ve güvenlik.

## 5. Onay yolu

Sözleşme tek yazıcısı (MOD-0183 Faz A) → CT-APR-02 bağımsız inceleme → yayın; DocsPathGuard karma bağlaması ayrı adım
(Q26 / Q30 deseni). G2–G4 cevapları gelince çıkış çekirdeğine ait ekler (varsa) aynı sürüme ya da 3.5.0'a girer.
