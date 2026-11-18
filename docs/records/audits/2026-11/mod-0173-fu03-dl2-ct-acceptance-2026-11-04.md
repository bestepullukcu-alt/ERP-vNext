# MOD-0173-FU03 DL-2 — Control Tower kabulü

- Tarih: 2026-11-04. CT: MVP-1 CT, oturum `ca7382cf`. Doğrulama: depo dışı detached ağaç `C:\Users\Ali\ct-verify-fu02-dl1` @ `123c4e803`.
- İncelenen: `123c4e803` (feat(inventory): MOD-0173-FU03 DL-2 — consume a named reservation, REVERSAL restore, INVENTORY 1.7.0),
  ebeveyn `0f32e805c`. MVP-1 hattında; push / PR yok. Geliştirme oturumu `local_b98fb9c9`.
- Paket: `MOD-0173-FU03-inventory-lot-location-reservation.md` @ sha256 `0e93c1ba…847f2d` (r4, onaylı `c03ac5511`).
- Sonuç: **VERIFIED**.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam (`0f32e805c..123c4e803`) | 35 dosya; yalnız `services/Diten.InventoryService`, `docs/analysis/contracts/inventory-bundle*` (1.7.0, donmuş 1.6.0), `docs/analysis/contracts/README.md`, `docs/reference/architecture/api/mod-0173-inventory-bundle.md` |
| Tam Inventory takımı (filtresiz, kendi replica set'i, 27017 yok) | 1741 / 1745, 4 atlandı, 0 kırmızı — geliştirici sayısıyla aynı (taban 1715 / 1719) |
| Geliştirici ölçümleri (CT tekrar koşmadı) | TenantArchitecture 221 / 6 kırmızı, tabanla aynı; derleme uyarısı 0; DL-2 sabotajlarının hepsi kırmızı (S-19 üçüncü, gerçek sürümle) |
| MOD-0175-FU01 DL-1 etkileşimi | AC-35 (g) ve tam koşu; Expiry / Quarantine bekçisi yeşil, bekçi dosyasına dokunulmadı |

## CT sabotajı (uygulandı → sonuç → geri alındı)

| Sabotaj | Sonuç |
|---|---|
| `InternalMovementPostingService` REVERSAL geri getirmesinde `movement.RestoreReservation` koşulu kaldırıldı (CT-08 sınırı) | kırmızı: `ReservationReversalTests.Reversal_without_restore_flag_keeps_reservation_closed`; `git checkout` ile geri alındı |

## Sapmalar (CT kabul etti)

- NOT_FOUND mesaj sabiti `InventoryErrorCodes`'ta (MOD-0176 bekçisine dokunmamak için).
- `reservationConsumption` anlık görüntüsü hareket kaydında saklanır (1.3.0 ExpiryChecks emsali; paket §4.3'te sayılmamıştı).
- AC-28 fixture'ı FEFO tarihsiz; geri getirme miktarı aslın tüketim anlık görüntüsünden.

## Açık kalanlar

- AC-19 E3 Gateway smoke (DL-1'den) — fleet ve gerçek token ile CT koşar.
- CT-02: ebeveyn MOD-0173 paketine INVENTORY 1.6.0 / 1.7.0 sürüm notu.
- DL-3 başlamadı.

ACCEPTED, kod main'e girdiğinde yazılır (SOP §7).
