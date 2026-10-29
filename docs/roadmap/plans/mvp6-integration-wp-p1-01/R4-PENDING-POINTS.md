# WP-P1 r4 — pending points (MOD-0183 / MOD-0186)

DURUM: BEKLİYOR — Natig 2026-10-26: r4, MVP-1 CT'nin r3 (`ada5e1901`, push `3830c740f`) yeniden incelemesi gelince yazılır;
o incelemenin bulguları ve aşağıdaki altı nokta **tek** revizyonda işlenir. Paketler bu dosya yüzünden değişmez.

Kaynak: paralel bağımsız inceleme (oturum `local_e3703e4c`, r3 öncesi taslaklar MOD-0183 `3c76e79e` / MOD-0186 `ba2392bb`),
yerel dal `docs/sce/wp-p1-g2-g4-patches-01` @ `8d14e5636` (bulgu dosyaları) ve `4ba5084ee` (o oturumun cevabı, yalnız referans;
dal birleştirilmez). Bu incelemenin diğer bulguları r3'te zaten kapalı (sabit anahtar, kilit, saklı tarihler, LE başlığı,
REVERSAL gövdesi, çıkış hareketi okuması, G2 metni, 403).

| # | Paket | Kaynak bulgu | r3'te eksik olan | r4 önerisi |
|---|---|---|---|---|
| 1 | MOD-0183 | F-4 | Başkası (ör. Inventory kullanıcısı) çıkışı zaten geri aldıysa telafi 422 `ALREADY_REVERSED` alır; r3 bunu yalnız loglar | `ALREADY_REVERSED` telafide "satır geri alınmış" sayılır (orijinal hareketin geri alındığı INVENTORY'den okunarak doğrulanır), bütünlük kaydı ayrıca yazılır |
| 2 | MOD-0183 | F-6 | `transitionShipment` yalnız 200/404/409/422/503 bildiriyor; r3'ün 403'ü ve INVENTORY 400 → 500'ü tanımsız | MOD-0147/0148 gibi "tanımsız statüler" tablosu; sözleşme halefi listesi |
| 3 | MOD-0183 | F-7 | "lotId'siz lot izli satır INVENTORY çağrılmadan reddedilir" kontrolü PRODUCT-MASTER ister (dondurulmamış) | Ön kontrol kaldırılır; INVENTORY'nin lot reddi `INVENTORY_REFUSED` olarak gösterilir; `SHIPMENT_LINE_STOCK_INCOMPLETE` yalnız PRODUCT-MASTER donunca |
| 4 | MOD-0186 | F-1 | Donmuş Returns sözleşmesi `transitionReturn` açıklaması "No Inventory/Warehouse HTTP" diyor (shipment-bundle.openapi.yaml:2009-2014) | 3.4.0 yamasında açıklama değişir: `Received` INVENTORY `CUSTOMER_RETURN` yazar (MOD-0186 §34); paketin eski "Inventory HTTP sıfır" metni §34'e göre düzeltilir |
| 5 | MOD-0186 | F-2 | Satır başına `InventoryTransactionReferenceId` için wire'da yer yok (alan başlıkta tek) | 3.4.0'a salt-okunur `ReturnLine.inventoryTransactionReferenceId`; başlık alanı `Received`'de istemciden reddedilir (r3) |
| 6 | MOD-0186 | F-5 | DL-1 devreye girdiği gün, çıkış hareketi olmayan sevkiyatların `InTransit` iadeleri ne alınır ne iptal edilir — takılır | Geçiş kuralı: DL-1 öncesi sevk edilmiş satır için `Received` eski davranışla (INVENTORY yazmadan, "manual assertion") kalır ve bu açıkça işaretlenir; ya da sahip kararı — Natig'e soru |

Not: 6. nokta bir sahip kararı gerektirebilir (eski iadeler INVENTORY'siz alınsın mı); r4 yazılırken soru kutusuyla sorulur.

## Ek: aynı paralel incelemenin r3 turu (2026-10-26, oturum `local_e3703e4c`, r3 `ada5e1901` üzerinde)

MVP-1 CT incelemesi **değildir** (o hâlâ bekleniyor); yalnız r4 girdisi. Sonuç: MOD-0183 KOŞULLU (4 ORTA, 1 DÜŞÜK), MOD-0186
KOŞULLU (3 ORTA, 1 DÜŞÜK), engel yok; hedefler `2acd1866…` / `ec08008e…`, `slices()` DL-1, DL-2. Bulgu dosyaları o oturumun
paylaşılan not klasöründe (`MOD-0183-r3-2acd1866.md` sha256 `589db9b9…`, `MOD-0186-r3-ec08008e.md` sha256 `98c04df8…`); repoya
alınmadı.

| # | Paket | Bulgu | Yukarıdaki noktalarla ilişki |
|---|---|---|---|
| 7 | MOD-0183 | F-1 ORTA: kirası dolmuş eski yazıcı, telafi edilmiş bir denemeye geri alınmamış çıkış yazabilir — çit belirteci (attempt + lease sürümü her yazımda koşul) ve kendi geç yazımını geri alma | yeni |
| — | MOD-0183 | F-2 ORTA: `ALREADY_REVERSED` dışarıdan elle REVERSAL ile olabilir → telafi edilmiş say; yoksa CompensationFailed çıkışı sonsuz döngü | = 1 |
| — | MOD-0183 | F-3 ORTA: 403 `INVALID_REQUEST` ve 500 `transitionShipment`'ta tanımsız; `inventoryCode` listesi `SERIAL_COUNT_MISMATCH` vb. eksik | = 2 (+ kod listesi) |
| — | MOD-0183 | F-4 ORTA: S23-5 PRODUCT-MASTER olmadan ölçülemez; boş `skuLevel` davranışı yok | = 3 (+ boş skuLevel) |
| 8 | MOD-0183 | F-5 DÜŞÜK: :699-701 kopmuş cümle; :167 "DL-4" bayat; satırların gönderim sırası yazılı değil | yeni |
| — | MOD-0186 | F-1 ORTA: :704 geçiş satırı, D186-04 :518, R06 :561, §4 :52-54, :783 hâlâ "Inventory yok / manual assertion"; 3.4.0 returns-semantics'i değiştirmiyor; satır başına referans wire'da yok | = 4 + 5 (+ paket içi eski metinler) |
| 9 | MOD-0186 | F-2 ORTA: saklı ilk deneme değişmez → yanlış lokasyon / kapalı dönem / `ITEM_NOT_ACTIVE` iadeyi `InTransit`'te sonsuza kilitler; hiç satır yazılmamışken denemeyi yeniden açma ve kalıcı ret için çıkış | yeni |
| — | MOD-0186 | F-3 ORTA: DL-1 öncesi sevkiyatların yoldaki iadeleri takılır (`InTransit → Cancelled` yok) | = 6 |
| 10 | MOD-0186 | F-4 DÜŞÜK: iadede ret wire kodu ve INVENTORY 400 eşlemesi adsız | yeni (2'nin iade karşılığı) |

r4 = MVP-1 CT bulguları + 1–10.
