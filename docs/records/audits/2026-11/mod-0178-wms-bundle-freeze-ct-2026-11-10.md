# MOD-0178 — WMS-BUNDLE CT dondurma adımı (pack §19.7, AC-60)

- Tarih: 2026-11-10. CT: MVP-5 WMS CT (oturum `ca7382cf`); yazım CT'nin alt ajanı, doğrulama CT. Sahip: Beste («Başla», 2026-11-10).
- Taban: hat ucu `65af0e638`. Commit'ler: `3adf141da` (1.0.0 FROZEN: S1 + WAREHOUSE-OUTBOUND 1.0.0 aynen, README satırı),
  `cfeed6523` (1.1.0 eklemeli: S2, S3, öneri §2.3 üyeleri; 1.0.0 test fikstürü `Contract/Fixtures/wms-bundle-v1.0.0.openapi.yaml`),
  `9d85fb6e3` (AC-60: `WmsDraftContract` → `WmsContract`, cevaplar dondurulmuş YAML'a karşı; `WmsBundleContractTests` 7 olgu).
- Değişmeyenler (ölçüldü, `git diff 65af0e638..9d85fb6e3 --stat`): üretim kodu, `warehouse-outbound.openapi.yaml` (1.0.0), taslak, `ocelot.json`,
  `.antigravity/**`, `CycleCountDraftContract.cs`. Yalnız sözleşme + README + fikstür + Inventory testleri (11 dosya).
- Sonuç: **VERIFIED** — AC-60 kapandı. İki açık sahip kararı (aşağıda).

## Ne ölçülüyor (yeni testler)
1.1.0 FROZEN başlıkları; 1.0.0 fikstürü 1.0.0 commit'iyle bayt-eşit; 1.0.0 outbound kısmı `warehouse-outbound.openapi.yaml` 1.0.0'la yol-yol / şema-şema
eşit; 1.1.0, 1.0.0'ın üstüne yalnız ekler (adlı üç genişletme: görev numarası `-(PUT|PCK)-`, `postingKey` `wms:(put|pck):`, bir `format: uuid`);
denetleyicilerin sunduğu 48 rotanın her biri dosyada, eylemin `[HasPermission]` anahtarıyla; JSON `null` yalnız iki adlı yerde; hata kataloğu
`WarehouseErrorCodes` ile eşit. Her `/api/warehouse` test cevabı dondurulmuş şemaya karşı doğrulanıyor (kapalı nesneler, enum'lar, biçimler).

## Taslak / paket / kod farkları ve dondurulan
- Kodun yazdığı, taslakta olmayan üç üye (`TaskConfirmation.error.reason`, iki `contractVersion`) 1.0.0'da (DL-2'nin önerisi).
- `WarehouseTaskOrigin` 7 değer (taslak şema bloğu + kod). Pick üyeleri kodun kümesi (`outbound{…}`, `allocSeq`, `reservationId`).
- `WarehouseException.warehouseId` (kod, her zaman yazılıyor); `ShortDispatchUnresolved` details'te `outboundNumber`, `skuLevel`, `stagingLocationId` isteğe bağlı.
- **Adlı `null` istisnası (paket §8.23 taslak kural 4'ün üstünde):** yalnız `stagingDifference` ve `differenceDeferredBy`; dosyada yazılı.
- `syncOutboundDispatch` `shipmentStatus` (paket §8.21 ile uyumlu). `OutboundShipmentReadyEvent` §2.3 üyeleri + `eventId`.
- `OutboundLine.fefoBypassReason` yalnız `maxLength: 1000` (Warn kipinde kod kısa gerekçe saklayabiliyor).
- 500 gövdesi: servis geneli beklenmeyen 500 `code` taşımıyor → bildirilmemiş 500 `AuthError` şekliyle denetleniyor (küçük gevşeme; 1.1.0
  açıklamasında kural 3 notu). Yalnız release çökme testi bu yola düşüyor.

## Tam takımlar (CT, `9d85fb6e3`, süzgeçsiz; `INVENTORY_TEST_MONGO` yok, 27017 kullanılmadı)
| Takım | Sonuç |
|---|---|
| Inventory | **2021 / 0 / 5 atlandı** |
| Gateway | **371 / 0** |
| TenantArchitecture | **222 / 5** — `DocsPathGuardTests` ×5, main tabanı |

## Sabotajlar (her biri bayt-eşit geri; `git status` temiz)
| # | Sabotaj | Sonuç |
|---|---|---|
| A-S1 | `WarehouseTaskMembers`'tan `remainingQuantity` silindi | kırmızı (PutawayContractTests 2/2) |
| A-S2 | `OutboundShipmentView.itemLevel` yeniden adlandırıldı | kırmızı (OutboundAllocationTests 9/13) |
| A-S3 | outbound enum `Cancelled` → `Canceled` | kırmızı (WmsBundleContractTests 2/8 — eşitlik testi) |
| A-S4 | adlı istisnadan `null` kaldırıldı | kırmızı (6/19) |
| CT-1 | S3 `WarehouseException`'dan `exceptionId` (özellik + required) silindi | **kırmızı** — WarehouseMonitoringTests 7/11 (AC36 ×2, AC71 ×2, AC78 ×3) |

A-S1…A-S4 alt ajanın (ölçüldü, tek tek test adları kaydedilmedi); CT-1 bağımsız.

## Açık kalanlar
- ~~K-1~~ → **karar verildi** (Beste 2026-11-10, CT önerisi): `listPackages` tahsis satırı kodun hâliyle `lotId` + `lotNumber`
  (taslağın `lot` üyesi kullanılmaz). Sözleşme zaten böyle donduruldu; 1.1.0 açıklamasına not düşüldü.
- ~~K-2~~ → **karar verildi** (Beste 2026-11-10, CT önerisi): iki kod 1.1.0'a eklendi — aşağıdaki "Ek: K-2" bölümü.
- `OutboundShipmentReadyEvent.occurredAt` (taslak §7) kod yazmıyor → dondurulmadı.
- Hiçbir testin çağırmadığı üç operasyon (`bulkAssignPickTasks`, `getPickTaskEvents`, `markPickConfirmationReturned`) — şema putaway'den; ölçülmedi.
- Operasyon başına hata listeleri testle zorlanmıyor (yalnız başarı durumları).
- `CycleCountDraftContract.cs` yorumu eski `WmsDraftContract` adını anıyor (MOD-0181, kapsam dışı; DL-3/DL-4'te düzeltilir).
- WAREHOUSE-OUTBOUND 1.1.0 (MVP-6 ile) ayrı adım.

## Ek: K-2 (sahip kararı Beste 2026-11-10) — iki hata kodu 1.1.0'a
- Sözleşme: katalogda `WMS_PACKAGE_NOT_FOUND` (404) ve `WMS_ASSIGNEE_DIRECTORY_UNAVAILABLE` (503, Q-13 A); operasyon listeleri:
  ilki `updatePackage`, `closePackage`; ikincisi `createPutawayTask`, `updatePutawayTask`, `bulkAssignPutawayTasks`, `bulkAssignPickTasks`
  (kodda `AssigneeAsync` çağıranlar). 1.1.0 açıklamasına K-1 / K-2 notu. Yanıt kodları (404 / 503) bu operasyonlarda zaten vardı.
  1.0.0 ve fikstürü değişmedi.
- Test: `WmsBundleContractTests` katalog olgusu artık iki kodun **varlığını** ve operasyon listelerindeki yerini sabitliyor.
- Tam takım (süzgeçsiz, CT): Inventory **2021 / 0 / 5 atlandı**. Sabotajlar (bayt-eşit geri): katalogdan `WMS_PACKAGE_NOT_FOUND`
  silindi → kırmızı (1/8); `bulkAssignPickTasks` listesinden `WMS_ASSIGNEE_DIRECTORY_UNAVAILABLE` silindi → kırmızı (1/8).
- **DÜŞÜK (geliştirici, sonraki 0178 dilimi):** `WarehouseErrorCodes.All` servisin yazdığı `AssigneeDirectoryUnavailable`'ı
  listelemiyor; test bunu açıkça ekleyerek karşılaştırıyor. Kümeye eklenince testteki ek kaldırılır.

VERIFIED
