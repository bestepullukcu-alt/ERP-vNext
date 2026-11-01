---
status: approved
decided: 2026-10-31
module: MOD-0230
work_packages: WP-0230-08, WP-0230-DL10, DL-5, DL-11 (revizyon 15 taslağı)
---

# PVG-MOD0230-KAPALI-KAYIT-04 — Paket ekinde sıra ölçüsü ve düzeltme turu

> **Karar sahibi:** sahip. 2026-10-31'de PVG Control Tower sohbetinde, inceleme 18'in orta bulguları için sorulan "Paket eki için ne yapalım?" sorusuna "Duzelt" dedi. Sorudan hemen önce CT önerisi yazılıydı: sıra ölçüsü "kabul edildi" yerine "doğrulanıp birleşti" olur, döngü böyle çözülür, triyaj hatası bir parçaya yazılır, izin yolu okunup sonucu sahibe yazılır.
> **Kaydı tutan:** PVG Control Tower.
> **Sınır:** Bu kayıt kod, push, PR ya da dağıtım izni değildir. Paket içeriği yine bağımsız inceleme ve HIGH onaydan geçer.

## Karar

| # | Karar |
|---|---|
| KK-7 | Bu pakette bir dilimin başka dilime **sıra koşulu** kuyruk durumu `INTEGRATED`tır (bağımsız doğrulandı, ana dala birleşti). DEPO-01 KD-3'teki "DL-10 kabulü"nün sıra anlamı bununla okunur; DL-10'un kendi kabulü kaldırılmaz. |
| KK-8 | Gerekli kanıtı canlıda üretilemeyen dilimlerin **kabulü** ertelenir, düşürülmez (SOP §23.1). Bu dilimler canlı yol açılana kadar `INTEGRATED` kalır (`pvg-mod0230-kanit-seviyesi-01.md`). |
| KK-9 | OD-PVG-14'ün (b) yolu "DL-5 `INTEGRATED` olduktan sonra" diye okunur. OD-PVG-14 sahipte bekler; bu kayıt hiçbir kapıyı açmaz. |
| KK-10 | Triyaj bağlama hatası pakette bir boşluk satırı ve sahip dilimle yazılır. Platform yöneticisinin doğrudan abonelik çağrısının rollere izin verip vermediği kodda okunur, sonuç sahibin onay listesine yazılır. |

## Ölçüm kaydı: triyaj bağlaması

2026-10-31, paket yazarı ajan, depo dışı karalama programı; gerçek host (`PvgServiceApiHost.CreatePvgServiceWebHostBuilder`), Development, loopback, üretilmiş JWT; vekil ve Gateway yok.

| Çağrı | Yanıt |
|---|---|
| Triyaj, `TriageOutcome` metin (`"Triaged"`) | 400, boş gövde |
| Triyaj, `TriageOutcome` null | 409 `PVG_PERMISSION_DENIED` |
| Triyaj, `TriageOutcome` sayı (`1`) | 409 `PVG_PERMISSION_DENIED` |
| Yönlendirme, metin ya da null kuyruk | 409 `PVG_PERMISSION_DENIED` |
| Güncelleme `{}` | 409 `PVG_PERMISSION_DENIED` |
| Liste | 409 `PVG_PERMISSION_DENIED` |
| Seçenekler | 200 |

Sınır: formdan vekile ve servise giden tam yol koşulmadı; form değeri kod okumasından alındı.

## Gerileme değerlendirmesi

- Sıra ölçüsü gevşemez: `INTEGRATED` bağımsız doğrulama ve ana dala birleşme ister.
- Kabul sayısı bu kararla artmaz. DL-2, DL-4, DL-5, DL-7 ve DL-10'un kabulü canlı yol açılana kadar bekler; sahip "bitti" ile "kabul edildi"yi ayrı okumalıdır.
