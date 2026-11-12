# MOD-0180 Dalga Planlama — sahip kararları 02 (2026-11-12)

Kaynak: kararlar 01 (`mod0180-wave-planning-owner-decisions-01.md`, D-1…D-5) ve mockup tur 1 (`Dalga Planlama (standalone).html`, mockup v1;
açık sorular D-1…D-16). Paket henüz yok; kararlar paketin girdisidir. Sahip: Beste.

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| D-4 | `WAV` kayıt kodu öneki ve izin ad alanı | **Evet:** görünen kod `{ŞİRKET}-WAV-000042` (RCS-001 ortak servisi); izinler `warehouse.waves.*` (PKS-001 `warehouse.*` ad alanı; mockup'taki `wave.*` adları buna çevrilir). `.antigravity/rules/record-coding-standard.md` (WAV satırı) ve `permission-key-standard.md` satırları paket hazırlanırken yazılır; içerik bu kararla onaylı, dosya değişikliği yine sahibin önünden geçer | Beste | 2026-11-12 |

| D-17 | Paketin onaylayanı (EES 2.14: yazan ≠ onaylayan) | **Ali (GMG-ERP)** — MOD-0178 / MOD-0181 ile aynı | Beste | 2026-11-12 |

Mockup tur 2 (`Dalga Planlama (standalone) v2.html`, 2026-11-12): D-2, D-4, D-5, D-15 düzeltmeleri yapıldı (CT incelemesi); paket yazımına
girdi olarak kabul.

## Mockup tur 1 ile kararlar arasındaki farklar (tur 2 düzeltme girdisi)
- **D-2:** mockup "rota optimizasyonu yok, raf sırası" önerdi; karar 01 **v1'de var**. Tur 2'de en kısa yol sırası gösterilir.
- **D-4:** mockup `wave.*` izinleri kullandı; karar `warehouse.waves.*`.
- **D-5:** mockup yalnız "Paketleme" sütunu ve yalnız toplama ilerlemesi gösterdi; karar 01 toplama + paketleme + **sevk** ilerlemesi.
- **D-15:** 7 dil listesi kuralla sabittir (kiracı modülü: en, tr, fr, es, zh, ar, ru); mockup "paket sırasında belirlenecek" dememeli.
- Mockup'ın kendi açık soruları D-6…D-14, D-16: **hepsi mockup önerisiyle kabul** (Beste, 2026-11-12):
  D-6 riskli dalga = kesime < 90 dk ve ilerleme < %70, eşikler depo ayarı · D-7 sınır aşımı engellenmez, uyarı + serbest bırakırken onay ·
  D-8 sipariş tek dalgada, bölünmez · D-9 "Kısmen tamamlandı" yalnız kalan görevler geri çekilince; kesimden sonra otomatik kapanış yok,
  istisna açılır · D-10 lot geri çağırmada otomatik çıkarma yok, istisna + vardiya sorumlusu kararı · D-11 bekleyen görev eşiği 30 dk, depo
  ayarı · D-12 ağırlık / hacim yoksa "—", v1'de sınır değil · D-13 "sonraki dalgaya taşı" varsayılanı aynı depoda kesimi en yakın taslak
  dalga, kullanıcı değiştirebilir · D-14 iyimser kilit (sürüm), çakışmada "Kayıt değişti, yenileyin" · D-16 performans menüde ayrı sayfa.
