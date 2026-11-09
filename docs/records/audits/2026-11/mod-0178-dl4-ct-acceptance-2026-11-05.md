# MOD-0178 DL-4 — Control Tower kabulü

- Tarih: 2026-11-05. CT: MVP-5 CT (Beste, merkez), oturum `ca7382cf`. Doğrulama: depo dışı detached ağaç `C:\Users\Ali\ct-verify-fu02-dl1` @ `c9f6e4296`.
- İncelenen: `c9f6e4296` (feat(sce): MOD-0178 DL-4 — S2a outbound + allocation + maintenance (backend, Item line included)), ebeveyn
  `d6aff589e`. MVP-1 hattında (`feature/sce/mod-0173-inventory-ledger-valuation`); push / PR yok. Geliştirme oturumu `local_75b9d993`.
- Paket: `MOD-0178-putaway-picking-packing.md` @ sha256 `b24ff82545199af399d06ff6273143b75a9fd5d0d6cf0a2bead08fa657416de4`, `c9f6e4296`'da
  yeniden ölçüldü; onay satırı `execution/approvals/approvals.tsv:46` (HIGH). Başlama kapısı: FU03 üç dilim CT kabulünde (`2e6b29dcc`).
- Sonuç: **VERIFIED** — kod ve testler ölçüldü. ACCEPTED iki kapıya bağlı: AC-76 (a)–(d) Auth kapısı (Ali) ve aşağıdaki E3.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam (`d6aff589e..c9f6e4296`) | 49 dosya: `services/Diten.InventoryService/src` 37, `…/tests` 11, `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` 1. `ocelot.json`, Web, başka servis yok |
| XMC-001 (Beste 2026-11-05) | `SingleSystemOfRecordArchitectureTests`: `SinceDl4` = [`wms_outbound_events`, `wms_outbound_shipments`], sıralı `Assert.Equal`; `LedgerImmutabilityArchitectureTests`: yalnız `OutboundShipmentsController:{outboundId}` PUT, `Assert.Single` + `RemoveAll`; iki yorumda karar tarihi; başka satır yok |
| Merkezi denetim | `CentralAuditDeclarationTests` 84 → 87 (DL-4'ün 3 komutu; 0178 payı 85, +2 MOD-0175-FU01), işlem kapsayıcısı 12 → 13 (`OutboundShipmentRepository.InTransactionAsync`) |
| Tam Inventory takımı (filtresiz, kendi replica set'i, 27017 yok) | 1799 / 1803, 4 atlandı, 0 kırmızı — geliştirici sayısıyla aynı (taban 1755 / 1759) |
| Tam Gateway takımı | 350 / 351, 1 kırmızı `Mod0173Pm2InternalInventoryRouteTests…` — tabanda aynı, DL-4 dışı |
| Geliştirici ölçümleri (CT tekrar koşmadı) | TenantArchitecture 221 / 6 kırmızı ve Tenancy 3 / 0, tabanla aynı; Inventory uyarı 0; 34 sabotajın 34'ü kırmızı (iki XMC sabotajı dahil), hepsi bayt-eşit geri alındı |

## CT sabotajı (uygulandı → sonuç → geri alındı)

| Sabotaj | Sonuç |
|---|---|
| `OutboundAllocationPlanner.Pickable` içinden `&& !view.EffectiveRoles.Contains(WarehouseRole.Receiving)` silindi (mal kabul stoğu toplanabilir sayılır) | kırmızı: `OutboundAllocationTests.AC26_receiving_stock_with_an_open_putaway_task_is_never_picked` ve `AC26_a_lotless_SKU_uses_the_same_pickable_definition_with_one_warehouse_allocation` (Item, Lsku); Outbound 52 / 55. `git checkout` ile geri alındı, sha256 `37d3c62b…9fca` eşit, ağaç temiz |

## Sapmalar (CT kabul etti)

- Merkezi denetim sayısı paketteki 85 yerine 87: fark MOD-0175-FU01'in iki komutu; 0178 payı 85. DL-5 hedefi 99 yerine 101. DÜŞÜK, sürüm
  değişmez (S-101); DL-5 talimatında.
- DL-5'e bırakılanlar paket dilimlemesine uyuyor: donmuş `GET /outbound-shipments` uçları S1 davranışında; AC-78 (iii) "başka Staging
  düğümü" fikstürü ve sabotajı; AC-71 (vii)'nin bırakma kısmı.

## Açık kalanlar

- **AC-76 (a)–(d)** Auth kabul kapısı — Ali'nin WP'si. 0178 tarafı (e) geliştirici ölçümü: `.read` anahtarsız token `/search`, `/view`,
  `/events` uçlarında 403.
- **E3 (canlı) ve FU03 AC-19 E3:** Gateway 5000 üzerinden gerçek login token'ıyla rezervasyon yarat → oku → tüket → oku. DL-4 yalnız arka
  uç; Web'de çıkış sevkiyatı ekranı ya da proxy yok ve CT token okumaz. DL-5 Web ekranlarıyla (proxy, oturumdaki kullanıcı) CT koşar.
- DL-3 CT bulguları F-7 / F-8 (DÜŞÜK) DL-5 Web talimatında.

ACCEPTED, kod main'e girdiğinde ve iki kapı kapandığında yazılır (SOP §7).
