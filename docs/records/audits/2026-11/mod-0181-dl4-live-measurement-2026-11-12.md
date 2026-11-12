# MOD-0181 DL-4 — canlı ölçüm 1 (2026-11-12): AC-38 geçti, AC-39 kısmi, Sayım Formu çeviri hatası (ORTA-YÜKSEK)

- CT: MVP-5 WMS CT (oturum `ca7382cf`). Sahip: Beste — kiracı girişini ve test rolünü sahip verdi (sayım izinleri: `counts.read/create/update/
  record/cancel/bulk-delete`, `variances.read/approve/reject/recount`); CT şifre ve rol ayarına dokunmadı.
- Ortam: yerel filo, ana checkout (MVP-1 hattı `200c1a71a` ve üstü), Web 5001 → Gateway 5000 → Inventory 5067. Şirket GMG, depo D01.
- Önceki kayıt: `mod-0181-dl4-ct-acceptance-2026-11-11.md` (VERIFIED, ACCEPTED değil — canlı ölçüm + Auth rol şablonu kapısı).

## Ölçümler
| AC | Ne | Sonuç |
|---|---|---|
| AC-38 | izinsiz kullanıcı (`cycle-count.*` yokken) — `/Warehouse/CycleCounts`, `/Warehouse/CountVariances` | **geçti** — yalnız "… için erişim yetkiniz yok" iletisi; tablo / iskelet / form / sayfa betiği yok; yönlendirme yok (adres aynı); menüde Sayım bağlantısı yok; `ar` RTL ileti; 375 px yatay kaydırma yok |
| AC-39 / AC-35 | Pano (KPI + liste + "Yeni Sayım") tr / ar / 375 px | **geçti** — `ar` RTL, KPI metinleri Arapça; 375 px `scrollWidth` 375 |
| AC-39 | Yeni sayım yan paneli 375 px | **geçti** — panel 375 px, kaydırma yok; sayım `GMG-CNT-000001` (taslak) oluşturuldu |
| AC-39 / AC-35 | Sayım Farkları + sayfa yardımı, ar / 375 px | **geçti** |
| AC-39 / AC-35 | Sayım Formu `/Warehouse/CycleCounts/Sheet/{id}` | **KALDI** — bütün çeviriler boş (aşağıda) |

## Bulgu — ORTA-YÜKSEK: Sayım Formu çevirileri okunmuyor
- `CountSheet.cshtml:126` `@Json.Serialize(new { Words = words, Cancel = …, Save = … })` camelCase üretir (`words`, `cancel`, `save`; canlıda
  ölçüldü: 97 anahtar, `CountSheetTitle` ve `CycleCount.Status.Draft` = "Taslak" var). `countsheet.js:17` `.Words` okur → `words = {}` →
  `W()` hep boş: başlık "· GMG-CNT-000001", alt satır "D01 · İstanbul Merkez Depo · ", `ReadonlyDraft` uyarısı yok.
- DL-4 CT kabulünde (2026-11-11) kaçtı: testler resx anahtarlarını ölçüyor, sayfanın verilen JSON'u okuduğunu ölçmüyor.
- Düzeltme: WP-0181-DL4-FIX-01 DL-5 oturumuna gönderildi (sahip onayı "evet doğrudan gönder", 2026-11-12) — okunan ad = render çıktısı, bekçi
  testi, `Words` sabotajı.

## Kalan
- FIX-01 sonrası Sayım Formu'nun yeniden ölçümü: başlatılmış sayımda miktar girişi, gönder, tr / ar / 375 px, dokunma hedefleri ≥ 48 px.
- §24 DL-4 kabul kapısı: AuthService rol şablonu (Q-4 = A; Ali, MOD-0018-FU31).
- Canlı veride iz: `GMG-CNT-000001` (taslak, not "CT canlı ölçüm MOD-0181 DL-4").
