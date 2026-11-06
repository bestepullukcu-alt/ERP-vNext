# MOD-0178 DL-5 — Control Tower kabulü

- Tarih: 2026-11-06. CT: MVP-5 CT (Beste, merkez), oturum `ca7382cf`. Doğrulama: depo dışı detached ağaç `C:\Users\Ali\ct-verify-fu02-dl1` @ `2005dc238`.
- İncelenen: `2005dc238` (feat(sce): MOD-0178 DL-5 — S2b pick / pack / ReadyToShip / Shipped / cancel (backend)), ebeveyn `c101e22fa`.
  MVP-1 hattında (`feature/sce/mod-0173-inventory-ledger-valuation`); push / PR yok. Geliştirme raporu `mod-0178-dl5-01-rapor.md` (oturum `907fcb42`).
- Paket: `MOD-0178-putaway-picking-packing.md` @ sha256 `b24ff82545199af399d06ff6273143b75a9fd5d0d6cf0a2bead08fa657416de4`, `2005dc238`'de
  yeniden ölçüldü (`git show` baytları, Python); onay satırı `execution/approvals/approvals.tsv:46` (HIGH). Başlama kapısı: DL-4 CT kabulü
  (`docs/records/audits/2026-11/mod-0178-dl4-ct-acceptance-2026-11-05.md`, `c101e22fa`'da).
- Sonuç: **KOŞULLU** — kod ve testler ölçüldü, sayılar geliştiriciyle birebir; iki CT sabotajı yeşil kaldı (F-1 ORTA, F-2 DÜŞÜK): paketin
  durum × komut tablosundaki iki kural kodda var, testte yok. Kapanış yalnız test eklemesidir (üretim kodu değişmez); F-1 testi gelince CT
  aynı sabotajı yeniden koşar ve sonuç VERIFIED olur. ACCEPTED ayrıca AC-76 Auth kapısına ve canlı E3'e bağlı.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam (`c101e22fa..2005dc238`) | 59 dosya (23 yeni, 36 değişen): `services/Diten.InventoryService/src` 45, `…/tests` 13, `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` 1. `ocelot.json`, Web, `.antigravity/**`, `_Layout.cshtml`, başka servis yok. `tests/…/Recall/` farkı boş |
| Seam'ler | MOD-0177 `IRecallTraceSource.cs` (yalnız eklemeli, varsayılan gövdeli iki üye) ve `ShipmentBundleHttpReader.cs` (iki üyenin uygulaması; `ReadAsync` ve DI değişmedi) — pack §5; `CentralAuditDeclarationTests` 87 → 101 |
| XMC-001 (Beste 2026-11-05) | `SingleSystemOfRecordArchitectureTests`: `SinceDl5` = [`wms_handling_units`, `wms_outbox`], sıralı `Assert.Equal` + bildirilen kümeye `Concat` (SinceDl4 deseni); `LedgerImmutabilityArchitectureTests`: yalnız `PackagesController:{packageId}` PUT, `Assert.Single` + `RemoveAll`, DELETE muafiyeti yok; iki yorumda karar tarihi |
| Tam Inventory takımı (filtresiz; `INVENTORY_TEST_MONGO` yok, `DITEN_TEST_MONGOD` = mongod 9.0, 27017 yok) | **1844 / 1848, 4 atlandı, 0 kırmızı** — geliştirici sayısıyla aynı (taban `c101e22fa` 1799 / 1803). Atlananlar dört performans testi (Expiry ×2, Recall ×2), tabanda da aynı |
| Tam Gateway takımı | **367 / 368, 1 kırmızı** `Mod0173Pm2InternalInventoryRouteTests.The_inventory_contract_paths_split_exactly_between_the_inventory_route_and_no_route` — tabanda aynı, DL-5 dışı (taban 350 / 351) |
| Geliştirici ölçümleri (CT tekrar koşmadı) | TenantArchitecture 221 / 6 ve Tenancy 3 / 0, tabanla aynı; Inventory uyarı 0; sabotaj tablosu (`sabotage-table.md`) **40 / 40** kırmızı (37 + XMC sınır sabotajı 3), hepsi bayt-eşit geri alındı |

## CT sabotajları (uygulandı → sonuç → geri alındı)

Hepsi `OutboundFlowHandlers.cs`'te (sha256 `4e66f26c…ddefe`); her biri tek satır değişiklik → derleme → `Diten.InventoryService.Tests.Warehouse`
ad alanı (331 test) → `git checkout` ile geri alma → sha256 eşit; iş sonunda ağaç temiz.

| # | Sabotaj | Sonuç |
|---|---|---|
| CT-1 | `ReleaseOutboundShipmentHandler`: `Status != Packed` koşulu `is not (Packed or Picked)` yapıldı (koliler kapanmadan `Picked` outbound sevke hazır olur; paket durum × komut tablosu Release / `Picked` = T) | **YEŞİL — 331 / 331 geçti** (F-1) |
| CT-2 | `CancelOutboundShipmentHandler`: `ReadyToShip or Shipped` reddi yalnız `Shipped` yapıldı (`ReadyToShip` iptal edilebilir; Q-M6-7) | kırmızı: `CancelReleaseTests.AC32_ready_to_ship_and_a_pending_pick_refuse_the_cancel` (330 / 331) |
| CT-3 | `CancelOutboundShipmentHandler`: koli iptal döngüsü `Where(p => false)` (iptalde koliler `Cancelled` olmaz, `PackageCancelled` olayı yazılmaz; §8.22) | **YEŞİL — 331 / 331 geçti** (F-2) |

## Bulgular

- **F-1 (ORTA):** Paketin durum × komut tablosunda Release'in `Picked` (ve `Allocated`) hücresi `T` = 409 `WMS_INVALID_TRANSITION`; kod
  doğru (`OutboundFlowHandlers.cs` release ön koşulu), ama hiçbir test `Packed` olmayan outbound'a release göndermiyor — CT-1 yeşil. Kural
  korunmuyor: kapanmamış kolilerle `ReadyToShip` + `OutboundShipmentReady` outbox satırı yazılabilir. Düzeltme: `PackingReleaseTests`'e
  `Picked` (ve `Allocated`) outbound'da release → 409 `WMS_INVALID_TRANSITION`, outbound / outbox değişmez testi; CT-1 kırmızı olmalı.
- **F-2 (DÜŞÜK, S-101):** §8.22 iptalde kolileri `Cancelled` yapar; testler bunu ölçmüyor (CT-3 yeşil). Etki dar: iptal edilmiş outbound'da
  koli komutları zaten `T` döner, stok etkisi yok; yalnız koli durumu / olay izi bozulur. Düzeltme F-1 ile aynı test dilimine:
  `AC32_a_picked_cancel…` vakasına açık koli + `PackageCancelled` beklentisi.
- **F-3 (DÜŞÜK, bilgi):** geliştirici raporunun §4'ü "37 / 37" der, sonraki `sabotage-table.md` 40 satırdır (üç XMC sınır sabotajı
  sonradan eklendi). Commit iletisi sayı vermiyor; kayıt düzeltmesi gerekmez.

## Sapmalar (CT kabul etti)

- S-1 XMC-001: Beste kararı (2026-11-05) koda iki yorumla işlendi; darlığı (xmc5-coll), (xmc5-put), (xmc5-drop-hu), (xmc5-drop-outbox),
  (xmc5-delete) sabotajlarıyla ölçülmüş.
- S-2 merkezi denetim 101 (paket 99 + 2 MOD-0175-FU01) — DÜŞÜK, S-101; DL-4 kaydında öngörülmüştü.
- S-3 outbox koleksiyonu `wms_outbox` (Beste kararı; mevcut `inventory_outbox` MOD-0173 tipine bağlı). Paket §8.9'a ölçüm notu bir sonraki
  revizyonda.
- S-4…S-12 (bucket başına geri yerleştirme, fikstür kurulumları, kural düzeyinde AC-78 (iii), işlem adları, sayfalama, FEFO okuması, Item
  outbound'u, AC-77 `retryQuarantineRequest` DL-7'de) paket sınırları içinde; davranış değiştirmez.

## Açık kalanlar

- **F-1 / F-2 testleri** — DL-6 talimatının başına (yalnız test; CT aynı sabotajları yeniden koşar).
- **AC-76 (a)–(d)** Auth kabul kapısı — Ali'nin WP'si (Viewer dışlaması, hibe profili, `WarehouseSupervisor` / `ShippingPlanner` /
  `WarehouseOperator`). DL-4 / DL-5 için ortak.
- **E3 (canlı):** DL-4 ve DL-5 arka uçları Web ekranı olmadan canlı koşulamaz (CT token okumaz); DL-6 S2 ekranları (proxy, oturumdaki
  kullanıcı) geldiğinde CT Gateway 5000 üzerinden outbound → pick → pack → ReadyToShip → iptal akışını koşar.
- **Canlıya çıkış (§7):** gerçek MVP-6 MOD-0183 ile koşu, `reservationId` gönderen MOD-0183 revizyonu, Q-M6-1 / Q-M6-3 / Q-M6-5 / Q-M6-9.
- Kayıt kodu `PCK` / `PKG` / `PUT` geçici sayaçta (FU-0178-RCS-01).
- DL-3 CT bulguları F-7 / F-8 (DÜŞÜK) DL-6 Web talimatında.

ACCEPTED, kod main'e girdiğinde, F-1 kapandığında ve iki kapı (AC-76, E3) kapandığında yazılır (SOP §7).
