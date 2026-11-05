# MOD-0173-FU03 DL-3 — Control Tower kabulü

- Tarih: 2026-11-05. CT: MVP-1 CT, oturum `ca7382cf`. Doğrulama: depo dışı detached ağaç `C:\Users\Ali\ct-verify-fu02-dl1` @ `f5d490d7a`.
- İncelenen: `f5d490d7a` (feat(inventory): MOD-0173-FU03 DL-3 — FEFO hook counts the free (unreserved) lot quantity), ebeveyn `6661724cb`.
  MVP-1 hattında; push / PR yok. Geliştirme oturumu `local_b98fb9c9`.
- Paket: `MOD-0173-FU03-inventory-lot-location-reservation.md` @ sha256 `0e93c1ba…847f2d` (r4, onaylı `c03ac5511`), `f5d490d7a`'da yeniden ölçüldü.
- Sonuç: **VERIFIED**. Bu kayıtla FU03'ün üç dilimi (DL-1 `372082aa0`, DL-2 `123c4e803`, DL-3 `f5d490d7a`) CT kabulünde; MOD-0178 DL-4 başlama kapısı (§7, r20) açıldı.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam (`6661724cb..f5d490d7a`) | 9 dosya; yalnız `services/Diten.InventoryService` ve `docs/reference/architecture/api/mod-0173-inventory-bundle.md`. MOD-0176 dosyalarında yalnız `FefoCandidateReader` yapıcısı / `freeBase` girdisi ve `ExpiryPostingPolicy.FefoAsync` tek satır (XMC-001, §5). Sözleşme değişmedi |
| Kod okuması | `LotReservationReader` vade süzgeci olarak `ReservationActiveFilter` (tek yer, son bakış 01 F-1); TenantId + LegalEntityId + IsDeleted süzgeci; bağlı defter oturumunda işlem içi okuma; serbest miktar alt sınırı 0 |
| Tam Inventory takımı (filtresiz, kendi replica set'i, 27017 yok) | 1752 / 1756, 4 atlandı, 0 kırmızı — geliştirici sayısıyla aynı (taban 1741 / 1745) |
| Geliştirici ölçümleri (CT tekrar koşmadı) | TenantArchitecture 6 kırmızı, tabanla aynı; derleme uyarısı 0; S-31, S-38, S-40, S-41, S-42, DI ve karantina sabotajları kırmızı; S-22 atlanan sayısını artırdı |
| MOD-0176 bekçileri | yeşil; bekçi dosyalarına dokunulmadı |

## CT sabotajı (uygulandı → sonuç → geri alındı)

| Sabotaj | Sonuç |
|---|---|
| `ExpiryPostingPolicy.FefoAsync` `freeBase: true` → `false` (serbest taban kapalı) | kırmızı: `FefoFreeQuantityTests` 3 test (`Early_lot_reserved_for_another_outbound_is_not_a_violation`, `Reservation_not_yet_due_hides_early_lot`, `Quarantine_hold_closed_by_source_reversal_does_not_free_a_reserved_early_lot`); `git checkout` ile geri alındı, ağaç temiz |

## Sapmalar (CT kabul etti)

- MOD-0175-FU01 etkileşim testi geliştiricinin kurgusu (paket senaryoyu yazmıyordu): kaynak geri alımıyla kapanan karantina, ayrılmış erken lotu FEFO için serbest bırakmaz.
- `Bound_read_sees_same_transaction_change` ilk sürümde aralıklı kırmızıydı (test fikstürü rastgele şablon seçiyordu); şablon W + ACTIVE ile süzüldü, 6/6 yeşil, S-40 yeniden kırmızı.

## Açık kalanlar

- §8.5 bilinen fark: `getFefoOrder` okuması `basis: onHandAvailable` ile kalır (FEFO-BUNDLE değişmedi); serbest tabana geçmesi MOD-0176 sözleşme revizyonudur (FU-03-FEFO).
- AC-19 E3 Gateway smoke (DL-1'den) — fleet ve gerçek token ile CT koşar.
- CT-02: ebeveyn MOD-0173 paketine INVENTORY 1.6.0 / 1.7.0 sürüm notu.

ACCEPTED, kod main'e girdiğinde yazılır (SOP §7).
