---
name: resx-check
description: "SharedResource çeviri denetimi (resx). Yalnız frontend/Diten.Web/Resources/SharedResource.{dil}.resx dosyalarını kontrol eder: eksik anahtar, İngilizceyle aynı bırakılmış değer, eylem metinlerinde Title Case. Platform (en, tr) veya kiracı (7 dil) bağlamında çalışır. SharedResource anahtarı eklendiğinde ya da değiştiğinde veya ortak çevirilerin kontrolü istendiğinde kullan. Sayfaya özel .resx dosyalarını, .cshtml metnini ve *.l10n.js dosyalarını KONTROL ETMEZ. Yalnız raporlar; dosya değiştirmez; yayın kapısı değildir."
---

# resx-check — SharedResource çeviri denetimi

Var olan doğrulayıcıyı çalıştırır ve sonucunu olduğu gibi raporlar. Yeni bir kontrol eklemez.

## Ne kontrol eder, ne etmez

| Kontrol eder | Kontrol ETMEZ |
|---|---|
| `frontend/Diten.Web/Resources/SharedResource.{dil}.resx` dosyaları var mı | Sayfaya özel `Resources/Views/**/*.resx` dosyaları |
| `en`'deki her anahtar diğer dillerde var mı | `.cshtml` içinde elle yazılmış metin |
| Değer İngilizceyle aynı mı bırakılmış (yer tutucu çeviri) | `*.l10n.js` ve `window.L10n` köprüsü |
| Eylem metinlerinde Title Case (büyük harfi olmayan `zh`, `ar` hariç) | Çevirinin anlamca doğru olup olmadığı |

Sayfaya özel kaynak dosyaları için bir betik yok; onlar `.antigravity/rules/localization-standard.md` kuralına göre elle kontrol edilir.

## Çalıştır

Repo kökünden:

- macOS / Linux: `python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py . --context tenant`
- Windows: `py -3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py . --context tenant` (`py` yoksa `python`)

Platform (yönetim) modülü için `--context platform` kullan: yalnız `en` ve `tr` denetlenir.

## Sonucu raporla

- Çıkış kodu `0` ve `[OK]` satırı → **PASS**.
- Çıkış kodu `1` → **FAIL**. `[X]` satırlarını aynen aktar. FAIL sonucunu asla PASS diye özetleme.
- **Bilinen borç (ölçüm 2026-10-14, main):** kiracı bağlamında `fr`, `es`, `zh`, `ar`, `ru` dillerinde 33'er anahtar eksik; dil başına 31–43 değer İngilizceyle aynı; `tr`'de 5 değer İngilizceyle aynı. Bu yüzden betik bugün main'de FAIL verir.
  Bu sayıları senin değişikliğinin hatası sayma. Değişikliğinin eklediği anahtarları `git diff` ile bul ve yalnız onların bütün gerekli dillerde olup olmadığını ayrıca belirt.
- Bu skill dosya değiştirmez, çeviri yazmaz ve yayın kapısı değildir. Eksik çeviriyi tamamlamak `l10n-agent`'ın işidir.
