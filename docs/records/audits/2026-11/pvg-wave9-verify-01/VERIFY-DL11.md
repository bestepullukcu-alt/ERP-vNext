# PVG dokuzuncu dalga — bağımsız doğrulama: WP-0230-DL11

> Control Tower kaydı, 2026-11-02. `DISPATCHED → INTEGRATED` geçişinin kanıtıdır.

- **Teslim:** dal `feature/pvg/wp-0230-dl11`, iki commit: `9d48a4ac662e0b3404ab66ac08e770927820da10` (ebeveyn Expected HEAD `ec18ebae6`) ve doğrulama bulgularının düzeltmesi `ac085d1cbe5aa242c1dd35b56f4fc8bf613a0885`.
- **İşçi:** alt ajan, Sonnet 5.5 · high istendi, `claude-sonnet-5-5` olarak bildirdi. Kendi yalıtılmış worktree'si (dağıtım kaydı). Yedi resx dosyasındaki iki anahtarı `l10n-agent` alt ajanı ekledi.
- **Doğrulayıcı:** ayrı alt ajan, Opus 5.5 · high istendi, `claude-opus-5-5` olarak bildirdi; effort doğrulanamadı. İşçi raporu verilmedi, işçinin sabotajları tekrar edilmedi.
- **Karar: KABUL EDİLEBİLİR** (iki tur). 1. tur: ENGEL 0, ORTA 2, DÜŞÜK 3. 2. tur `ac085d1cb` üzerinde: beşi de kapandı, açık bulgu yok.

## Ölçülenler

| Ölçüm | Sonuç |
|---|---|
| Kapsam `ec18ebae6..9d48a4ac6` | 20 dosya, hepsi İzinli yollarda; Platform, Auth, Gateway, `.antigravity`, MOD-0189/MOD-0191 dokunulmadı; `git diff --check` temiz; sır yok |
| Kapsam `9d48a4ac6..ac085d1cb` | 3 dosya; üretim değişikliği yalnız beklenmeyen istisnadaki deneme sayısı (B-5) |
| `Diten.PvgService.Api.Tests` | taban 352 → 373 geçti, 0 kırmızı (iki turda da); `known_failures.py` YENİ=0 |
| `Diten.PvgService.RegPvBase.Tests` | 211 geçti |
| `NavManifestL10nGuardTests` | 6 geçti, 2 taban kırmızısı; hata satırları tabanla aynı (yalnız MRPREPLENISHMENT / SAFETYSTOCKOPTIMIZATION ve `MrpManifestProvider.cs: no pages parsed`); CASEINTAKETRIAGE ya da PHARMACOVIGILANCE geçen satır yok (sahip sözü «taban-kabul», `pvg-mod0230-nav-taban-01.md`) |
| Mimari süit | 173 geçti, 3 kırmızı (DocsPathGuardTests ×1, AuditTrailStandardTests ×2), tabanla aynı; Pvg satırı yok |
| Paket şartları (kod okuması) | manifest alanları, 4 sayfa, 8 eylem, 5 anahtar ve etiketleri; IHostedService; beklemeyen StartAsync; bekleyen StopAsync; 5 deneme, 2/4/8/16 sn, deneme başı 5 sn; durum tablosunun 6 satırı ve log kodları; yalnız süreç ortamı; üretim benzeri denetim önce; AllowAutoRedirect false; log kategorisi None; anahtar, URL ve istisna loga gitmiyor; AC-13 (f) listesi tam bir tip büyüdü |

## Sabotaj

- **İşçi:** 1. turda 60, hepsi adıyla kırmızı. 2. turda 5, hepsi kırmızı.
- **Doğrulayıcı, 1. tur:** 17 yeni sabotaj; 13 kırmızı, 4 yeşil. Yeşillerden biri bulgu değil (sayfa sırası; pakette şart yok). Üçü bulgu oldu:
  - V6: StopAsync beklemeden döner (B-1, ORTA).
  - V1: anahtar log scope'una yazılır (B-3).
  - V9: adres kırpılmaz (B-4).
  - Ayrıca V12 (bağlı belirteç) yalnız zamanlama tesadüfüyle geçiyordu (B-2, ORTA).
- **Doğrulayıcı, 2. tur:** V1, V6, V9 ve V12 aynı çapalarla yeniden koşuldu, B-5 için üç yeni sabotaj eklendi. 7/7 adlı testte kırmızı oldu. V12'nin kırmızısı artık işleyicinin zaman aşımına değil durdurma sınırına bağlı.
- Her sabotaj geri alındı, özetler eşleşti.

Kanıt (CT karalama alanı, depo dışı): işçi `sab-results.jsonl`, `sab-results2.jsonl`, `baseline/`, `final1/`, `final2/`; doğrulayıcı `dl11-vfy01-results/`, `dl11-vfy02-results/`.

## Birleştirme

`feature/pvg/wp-0230-dl11` CT dalına `--no-ff` ile birleşti: `51867acaf`. Çakışma yok. İşçi dalının tabanı `ec18ebae6` WP-0230-DL10-R1'i zaten içerdiğinden birleşik ağaç doğrulanan ağaçla aynı koddur. İşçi dalı yerelde kalır, push edilmez (S-125).

## Ölçülmeyenler

- **AC-38 / E5:** canlı çapraz modül koşusu ve yeniden başlatma denetimi koşulmadı; kabulde CT'nin işidir. DL-11 kabulü bu yüzden açık.
- **`resx-check`:** doğrulayıcı koşmadı. İşçi koştu: çıkış 1, ama iki yeni anahtar çıktıda hiç geçmiyor; kalan bulgular main'in var olan borcu.
- **Açık sahip maddeleri (paket, OD-PVG-13 satırı):** `route` anahtarı; kendiliğinden verilen izinler (SuperAdmin, Viewer); doğrudan abonelik çağrısının anahtar vermesi; OD-PVG-14.
