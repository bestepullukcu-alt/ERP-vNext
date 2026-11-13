# MOD-0173-FU03 — AC-19 E3: rezervasyonun Gateway üzerinden canlı okunması (2026-11-13)

- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0173-FU03-inventory-lot-location-reservation.md` — AC-19 son cümlesi:
  "Gateway üzerinden (5000) gerçek login token'ıyla `GET /api/inventory/reservations/{id}` 200 + gövdede `etag` (E3)"; §17 Smoke (E3).
- Ölçen: MVP-1 / MVP-5 CT (Beste adına), uygulama tarayıcısı; kullanıcı Beste kendi girişini yaptı (CT parola girmedi). Salt okuma;
  hiçbir kayıt yazılmadı.
- Ortam: yerel fleet — Gateway 5000, Web 5001, Inventory 5067 (ana checkout, `dotnet watch`). Platform 5057 aynı gün kapalı bulundu ve
  CT tarafından yeniden başlatıldı (giriş için); Auth / Platform `/health` 503 yalnız `masstransit-bus` (yerelde RabbitMQ yok), MongoDB sağlıklı.

## Adımlar ve sonuç

1. Web'de şirket seçimi: GMG (`1754b820-8e6d-4c38-b565-29b2acc03f41`, "GMG-GRAND MEDICAL ILACLARI LTD STI") — W-0173-02 şirket
   seçici (token `legal_entity_id` taşımıyor; seçim `X-Legal-Entity-Id` olarak gider).
2. Gateway'e doğrudan (tarayıcıdan `http://localhost:5000`, kullanıcının giriş oturumunun token'ı, `X-Legal-Entity-Id` başlığı):
   `GET /api/warehouse/outbound-shipments/b4a18616-0e38-4b04-a9a6-ba2e29016bdf` (GMG-OUT-000003, ReadyToShip) → 200; tahsisteki
   `reservationId` = `1f902e67-751f-4104-8e6b-eb9cf81e92b7`.
3. `GET /api/inventory/reservations/1f902e67-751f-4104-8e6b-eb9cf81e92b7` → **200**; gövde anahtarları `reservationId, status, itemId,
   skuId, skuLevel, warehouseId, lotId, quantity, remainingQuantity, reservationType, sourceModule, sourceDocumentId, sourceLineId,
   createdAt, etag, contractVersion`; **`etag` = `W/"1"`**; `status` ACTIVE; `reservationType` WAREHOUSE_OUTBOUND; `sourceModule`
   MOD-0178; `sourceDocumentId` = GMG-OUT-000003'ün kimliği; `quantity` / `remainingQuantity` "2.000"; `contractVersion` v1. HTTP `ETag`
   başlığı tarayıcıya görünmedi (CORS ile açılmamış olabilir); paket "gövdede `etag`" ister, karşılandı.
4. Ön kontrol: aynı uç şirketsiz çağrıldığında 403 "Select a company … X-Legal-Entity-Id … is required" — token kabul edildi, şirket
   kuralı Gateway arkasında işliyor.

**Sonuç: AC-19 E3 KARŞILANDI.** Daha önceki yakın kanıt (MOD-0178 canlı turunda rezervasyonun oluşup tutulması) bu doğrudan okumayla
tamamlandı. MOD-0173-FU03'ün açık kalan son kabul maddesi buydu (iş takibi p-01).

Ölçülmeyenler: tüketim → okuma akışı (§17 Smoke'un "yaratma → okuma → tüketim → okuma" zinciri) bu turda koşulmadı — rezervasyon
ReadyToShip sevkiyatta ACTIVE; tüketim sevkiyat Shipped olunca gerçekleşir (MOD-0178 DL-5 akışı, CT doğrulamasında ölçüldü).
