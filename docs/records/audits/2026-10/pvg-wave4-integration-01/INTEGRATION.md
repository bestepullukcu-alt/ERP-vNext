# PVG dördüncü dalga — entegrasyon kapısı raporu (WP-0230-DL6, WP-0230-DL3D, WP-0230-DL4B)

> Control Tower kaydı, 2026-10-28. Bu kayıt kuyrukta `VERIFIED → INTEGRATION_READY → INTEGRATED` geçişinin kanıtıdır. Kabul kaydı değildir.
> Doğrulama kayıtları: `docs/records/audits/2026-10/pvg-wave4-verify-01/VERIFY.md` (DL6, DL3D) ve `pvg-wave4-verify-02/VERIFY-DL4B.md`.

## Entegrasyon

Üç iş ürün kodu değiştirdi. Sahibin "tek-pr" kuralı gereği işçi dalları önce CT dalına birleştirildi (S-76). Ardından `main`'e tek bir zorunlu PR ile girdiler. PR'ın zorunlu olma nedeni (madde a): paket revizyon 12'nin onay satırı.

| PR | Birleşme | Birleştiren | CI |
|---|---|---|---|
| [#267](https://github.com/bestepullukcu-alt/ERP-vNext/pull/267) | `f0582a50b`, 2026-10-27 14:48 UTC | bestepullukcu-alt | Beş PVG süiti ve `gateway` yeşil. Kırmızı olan yalnız `phase1`; o da `main`'de bilinen ve PVG dışı bir kırmızı. |

Aynı PR, MOD-0230 revizyon 12'nin HIGH onayını da `main`'e taşıdı. `pack_approval.py check` sonucu: "onaylı — HIGH · onaylayan bestepullukcu-alt · 2026-10-27", çıkış 0.

## Entegrasyon kapısı ölçümü: PASS

CT dalı `main` `7495c4d27` ile birleştirildi. `services` ve `gateway` yolları `main` ile aynı. Testler PR'dan hemen önce, aynı kod üzerinde koşuldu. O tarihten bu yana `main`'de PvgService ve Gateway'de değişiklik yok.

| Süit | Sonuç | `known_failures` |
|---|---|---|
| PVG Api | 287/287 | 0 |
| PVG RegPvBase | 210/210 | 0 |
| Gateway | 313/313 | 0 |

PR'dan sonra yapılan kayıtlı DL8 smoke koşusu (run-04, kod `74d95ac38`) DL4B'nin etkisini de gösterdi: Gateway ve servis token'sız isteğe aynı 401 cevabını verdi.

## Kabul engeli

| İş | Ürettiği kanıt | Gerekli seviye |
|---|---|---|
| DL6 | E2 | E5 |
| DL3D | E2 | E3 |
| DL4B | E2 | E4 |

Satırlar canlı kapı açılana kadar `INTEGRATED` kalır. DL-4'ün AC-6 kısmı OD-PVG-3'ü bekliyor (WP-0230-DL4C).
