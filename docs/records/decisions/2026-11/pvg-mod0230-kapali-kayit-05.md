---
status: approved
decided: 2026-11-01
module: MOD-0230
work_packages: WP-0230-DL10, WP-0230-DL10-R1, DL-4, DL-11 (revizyon 15 taslağı)
---

# PVG-MOD0230-KAPALI-KAYIT-05 — Kalıcı depo reddi: paket düzeltmesi ve sıra değişikliği

> **Karar sahibi:** sahip. 2026-11-01'de PVG Control Tower sohbetinde, "Kalıcı depo ve paket eki için hangi yol?" sorusuna "Paketi düzelt" dedi. Seçeneğin metni sorudan hemen önce yazılıydı: ölçüt "bilinmeyen kayıtla aynı yanıt" olur, 404 hata yanıtları işinde gelir; bu düzeltme ve kalan iki orta bulgu paket ekine işlenir, bir inceleme daha yapılır; ek, kalıcı depo birleşmeden önce onaya gider; sonra dar bir düzeltme işi yapılır; 404 kuralı hata yanıtları işine kadar karşılanmaz ve bu kayda yazılır.
> **Kaydı tutan:** PVG Control Tower.
> **Sınır:** Bu kayıt kod, push, PR ya da dağıtım izni değildir. Paket içeriği yine bağımsız inceleme ve HIGH onaydan geçer. İşin başlaması, onay satırı ve PR ayrıca sahibin sözünü ister.
> **Dayanak:** `docs/records/audits/2026-10/pvg-wave8-verify-01/VERIFY-DL10.md` (REDDEDİLDİ).

## Karar

| # | Karar |
|---|---|
| KK-11 | AC-35 (b)'nin API testi 404 yerine şunu ölçer: başka kiracının GET ve PUT'u, bilinmeyen kaydın yanıtıyla aynı durum kodunu ve aynı gövdeyi alır; kayıt verisi taşımaz; PUT kaydı değiştirmez. 404 AC-6 ile gelir (DL-4). Test adı buna uyar. |
| KK-12 | KK-5 ve KK-7'nin sırası değişir: revizyon 15 onaya, WP-0230-DL10 `INTEGRATED` olmadan gidebilir. Değişmeyen: DL-11, WP-0230-DL10 `INTEGRATED` olduktan sonra başlar. |
| KK-13 | Kiracılar arası yanıtın 404 olması (çekirdek kural 2, AC-6) DL-4'ün hata yanıtları işine kadar karşılanmamış kalır. Bu açık, pakette kendi cümlesiyle yazılır. AC-6 uygulandığında bu test 404'ü de ölçer; kural düşürülmüş sayılmaz. |
| KK-14 | İnceleme 19'un iki orta bulgusu aynı yazar geçişinde kapanır: AC-39 (b) sabotajının adlı testi kırmızıya çevirmesi; DCP-004 §10'daki "DL-5 kabulü" cümlesi ile KK-8 ve KK-9 arasındaki döngü. |
| KK-15 | Kalıcı depo teslimi birleşmez. Parola sızıntısı, test adı ve küçük test boşlukları dar bir düzeltme işinde (WP-0230-DL10-R1) kapanır; sonra dar bir tekrar doğrulama yapılır. |

## Gerileme değerlendirmesi

- Kiracı yalıtımı gevşemez: yanıt bilinmeyen kayıtla bayt düzeyinde aynı olmak zorunda kalır, kiracı süzgeci sabotajı testi kırmızıya çevirmeye devam eder (`VERIFY-DL10.md`, ölçüldü).
- Gevşeyen tek şey durum kodunun ölçülme zamanıdır; KK-13 bunu görünür tutar. Risk: hata yanıtları işi uzarsa açık uzar. Bu iş MOD-0041 / Ops onayını bekliyor.
- Sıra değişikliği DL-11'i öne almaz; yalnız paketin onay zamanını öne alır. Onaylı paket, teslimi reddedilmiş bir dilimin ölçütünü içerir; WP-0230-DL10 bu sürece kadar `REWORK` kalır, R1 ayrıca kuyruğa girer.
