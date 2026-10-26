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
