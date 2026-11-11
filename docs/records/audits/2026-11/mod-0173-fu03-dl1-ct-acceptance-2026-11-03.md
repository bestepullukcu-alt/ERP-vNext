# MOD-0173-FU03 DL-1 — Control Tower kabulü

- Tarih: 2026-11-03. CT: MVP-1 CT, oturum `ca7382cf`. Doğrulama: depo dışı detached ağaç `C:\Users\Ali\ct-verify-fu02-dl1` @ `372082aa0`.
- İncelenen: `372082aa0` (feat(inventory): MOD-0173-FU03 DL-1 — lot / warehouse scoped reservations, INVENTORY 1.6.0), ebeveyn
  `658945363`. MVP-1 hattında (`feature/sce/mod-0173-inventory-ledger-valuation`); push / PR yok.
- Paket: `MOD-0173-FU03-inventory-lot-location-reservation.md` @ sha256 `0e93c1ba…847f2d` (r4, onaylı `c03ac5511`).
- Sonuç: **VERIFIED** — AC-19 E3 (Gateway üzerinden gerçek token'la `getReservation` smoke) açık AC olarak kalır.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam (`658945363..372082aa0`) | 80 dosya: `services/Diten.InventoryService`, `docs/analysis/contracts/inventory-bundle*` (1.6.0, 1.5.0 dondurulmuş kopya), `docs/reference/architecture/api/mod-0173-inventory-bundle.md`. `ocelot.json`, `.antigravity`, başka modül servisi yok |
| XMC-001 | `LotHeldStock.cs` yalnız `MoveAsync` (CT-07); MOD-0174 `TraceArchitectureTests` kanca listesi değişmedi |
| Tam Inventory takımı (filtresiz, kendi replica set'i, 27017 yok) | 1638 / 1642, 4 atlandı (taban performans testleri), 0 kırmızı — geliştirici sayısıyla aynı |
| Geliştirici ölçümleri (CT tekrar koşmadı) | TenantArchitecture taban ve yeni aynı 6 kırmızı (Inventory'ye ait değil); derleme uyarısı 0; 38 adlı sabotaj kırmızı, byte-eşit geri alındı |

## CT sabotajı (uygulandı → sonuç → geri alındı)

| Sabotaj | Sonuç |
|---|---|
| `ReservationActiveFilter.At` vade koşulu kaldırıldı (yalnız `Status == Active`) | kırmızı: `ScopedAvailabilityTests.Due_reservation_is_not_counted_in_scope_figures`, `InventoryReservationPortTests.Scope_free_excludes_due_reservation` (son bakış 01 F-1 kapandı); `git checkout` ile geri alındı |

## Sapmalar (CT kabul etti)

- `CreateReservationValidator` silindi, yapı kuralları handler'da: doğrulayıcı handler'dan önce 400 döndüğü için AC-15'in ret satırı
  yazılamıyordu.
- Lot kuralı `Persistence/IReservationLotRule` içinde: MOD-0174 `TraceArchitectureTests` kanca listesine dokunmamak için.
- 422 örnekleri `components.examples` altında: OAS 3.1'de `example` ile `examples` birlikte olamaz; 1.5.0 bayt eşitliği korunur.
- Test sınıfı adları §17.1'deki adlar (§5'teki dosya adları değil).

## Açık kalanlar

- AC-19 E3: Gateway (5000) üzerinden gerçek token'la `getReservation` smoke — fleet ayağa kalkınca CT koşar.
- CT-02: ebeveyn MOD-0173 paketine INVENTORY 1.6.0 sürüm notu (CT kabul adımı).
- Geliştiricinin bıraktığı depo dışı ağaçlar: `../erp-vnext-wt-fu03-dl1`, `../erp-vnext-wt-base-fu03` (temizlik).
- DL-2 ve DL-3 başlamadı.

ACCEPTED, kod main'e girdiğinde yazılır (SOP §7).
