# MOD-0018-FU31 — kararlar 06

Bu bir ek kayıttır. 01…05 numaralı kayıtlar değiştirilmez.

- Kaynak: CT · Auth (Yetki kulesi).
- Kişi adı yazılmaz.

| # | Karar | Söz | Kaynak | Nerede | Zaman |
|---|---|---|---|---|---|
| 13 | K-20 kapandı (paketteki seçenek (a)). Platform sahibi izni genişletti: `_IndexL10n.cshtml` içindeki `AuditLogEventLabels` sözlüğüne `role_provisioning_skipped` için tek bir yük satırı eklenebilir. | Alıntı 1 | Platform sahibi | MOD-0018 sahibi cevabı CT · Destek sohbetine yapıştırdı; Destek bu oturuma aynen iletti | 2026-11-11 |
| 14 | Süre kabulü: Platform sahibi ~44 saati uygun buldu. | Alıntı 2 | Platform sahibi | aynı | 2026-11-11 |

Alıntılar. Yalnız ilgili kısımlar alındı; metin aynen:

> 1. Platform etiket satırı: Evet, verdiğimiz izne dahil. _IndexL10n.cshtml içindeki AuditLogEventLabels sözlüğüne
>    role_provisioning_skipped için yalnız ekleme olarak tek satır eklenebilir; değer Localizer[...] ile en / tr kaynak
>    dosyasından okunmalı, mevcut satırlar değişmemeli. Platform'un etiket guard testinin yeşil olduğunu kabul raporunda yazın.
>
> 2. Süre (MOD-0018-FU31, ~44 saat): Uygun. Paketi onaylayıp kod tarihini iletebilirsiniz.

## Paket değişmez

- Sahip kararı 12 (kayıt 05) gereği paket metni değişmez.
- Alıntı 1'in şartları paketin AC-32 maddesiyle ve K-20 (a) seçeneğiyle aynıdır:
  - yalnız ekleme;
  - değer `Localizer[...]` ile kaynak dosyasından gelir;
  - mevcut satırlar değişmez;
  - guard testinin sonucu kabul raporuna yazılır.
- Bu şartlar DL-1 için bağlayıcıdır. DL-1 kabulünde `_IndexL10n.cshtml` değişikliği yalnız bu tek satırı ekler. Bu, AC-32 (1) ölçümüne `git diff -U0` ile dahil edilir.

## Onay

- "Paketi onaylayıp…" cümlesi Platform sahibinin görüşüdür; FU31'in paket onayı değildir.
- Paket onayı, MOD-0018 sahibinin «onayla» sözüyle verilir.

Bu kayıt commit'ten sonra değiştirilmez.
