# HCM-EMP-03 — Codex incelemesi 03'ün CT karşılaştırması (SOP §20.4/4)

> Control Tower kaydıdır; onay, paket değişikliği ya da kod izni değildir.

## Künye

- İnceleme: Codex, inceleme 03; kaynak GitHub dalı `chore/ct/hcm-employee-ct-01` @ cbcd47249b6ddcbeac27c71a90c328d79cd5f1a2
  (sahip sözü `push-cbcd472`). Model, effort, oturum: "verilemiyor". Sahip sohbete yapıştırdı, 2026-10-29.
- Hedef: MOD-0251 paketi revizyon 3 @ sha256 `03d9e80fdf82af1daea3c518f3becbcd9ade25e6352d361a8795b25a0afb0122`.
- Sonuç: **UYGUN DEĞİL** — inceleme 02'nin 8 açık bulgusunun 8'i kapandı; 1 yeni ENGEL (F-26).
- Bulgu dosyası: `docs/records/reviews/MOD-0251/2026-10-29-03d9e80f.md`. Biçim çevirisi: yalnız seviye sözcüğü `engel` →
  `ENGEL` (araç büyük harf ister) ve başlık sırası; içerik aynı. Ayrıştırıcı 1 bulgu okur, hata yok. Ham metin commit'e
  girmez (`draft-codex-03-raw.txt`).

## F-26 — DL-2 merkezi denetimi E5 ister

**CT kararı: kabul.** Ölçüm:

- SOP §23.1 (`control-tower-sop.md:1602-1606`): "Cross-module capability seam: E5". HCM yazımının ortak iletici üzerinden
  Platform merkezi günlüğüne (MOD-0021) gitmesi iki modül arası bir bağlantıdır.
- `PlatformCentralAuditClient.cs:45-60`: Platform adresi / iç anahtar yapılandırılmamışsa sonuç `Retry`; kategori, işlem ya
  da aktör türü Platform'un tanıdığı bir ad değilse `Rejected`. Yerel outbox kaydı merkezi günlüğe teslimi kanıtlamaz.
- Paket:203 (AE-1) ve 343: K4 teyidi, HCM izinin kabulü ve AE-2…AE-4 kapanmadan hiçbir denetim satırı kodlanmaz;
  paket:1701 bunu DL-2'nin hazır olma koşulu sayıyor.

**CT'nin ek gözlemi (inceleyen bulgusu değil):** merkezi denetim DL-2'nin içinde kaldıkça DL-2'nin tamamı (F-1, F-2, F-3,
F-15, F-16 kod düzeltmeleri; F-14 yetki; F-6 fixture sınırı) AE-1…AE-4 kararlarını ve E5 entegrasyon kanıtını bekler. Bu
kod düzeltmeleri tek servis içindedir (E4 yeter) ve kiracı / kişisel veri riskini kapatır; Platform tarafındaki açıklar
(AE-3 değişen alan, AE-4 HCM kategorisi) başka modülün işidir (S-90).

## Revizyon 4 için iki yol (sahip kararı)

1. **Ayrı dilim (CT önerisi):** taslak yazımlarının merkezi denetimi yeni bir dilime (ör. DL-7 "Draft-write central audit
   (HCM → MOD-0021)", E5) alınır; ön koşulu AE-1…AE-4, DL-4'e bağlı değil. DL-2 kod düzeltmeleri E4 kalır ve hemen
   yürüyebilir. P2 kapanışı ve GWT-A12 açıkça DL-7'ye bağlanır (inceleme 02 F-22'nin "ya da P2 kapanış iddiası açıkça
   ayrılmalı" seçeneği).
2. **DL-2'yi E5'e yükselt (inceleyenin önerisi):** DL-2 tek parça kalır, seviye E5, çıkış Platform merkezi günlüğünde
   görülen kayıt ve teslim kesintisi sonrası kurtarma. Kod düzeltmeleri de AE-1…AE-4 kapanana kadar bekler.

Her iki yolda da DL-2 / DL-7 çıkışı F-26'nın ölçümünü taşır: gerçek HCM yazımı Platform merkezi günlüğünde tenant, aktör,
korelasyon, nesne ve güvenli değişiklik bilgisiyle görülür; teslim kesintisi sonrası kurtarma ölçülür.
