# PVG yedinci dalga — bağımsız doğrulama: WP-0230-DL7C (AC-32)

> Control Tower kaydı, 2026-10-31. Kuyrukta `DISPATCHED → VERIFIED` geçişinin kanıtıdır. Kabul ya da entegrasyon kaydı değildir.
> Karar: **KABUL EDİLEBİLİR.** Engel ve ORTA yok; bir yeni DÜŞÜK bulgu açık. DL-7'nin gerekli kanıtı E4, kabul canlı kapıya bağlı kalır.

## Teslim

| | |
|---|---|
| İşçi dalı | `feature/pvg/wp-0230-dl7b-r1` (işçi oturumu pvg-dl7b) |
| İş commit'i | `1bfaab167`; HEAD^1 = Expected HEAD `9e9cf178f` |
| CT dalına birleştirme | `8020d5815` (yerel, push edilmedi) |
| Kaynak bulgular | `docs/records/audits/2026-10/pvg-wave7-verify-02/VERIFY-DL7B-R1.md` |
| DoR | `docs/records/audits/2026-10/pvg-wave7-dor-03/DOR.md` |
| Prompt | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL7C-v1.0.md` (sha256 `60f5e069…`); dağıtım kapısı 2026-10-31 çıkış 0; başlatma sözü sahibin "Başlat" yanıtı |

Ölçümü işi yazmayan bir CT ölçücüsü (alt ajan) `8020d5815`'te yaptı. Sabotajlar, PVG vitest ve toplu vitest çalışma ağacı dışındaki karalama kopyada koştu; kopya ağaçla aynı (`diff -rq` çıkış 0). C# süiti gerçek ağaçta koştu.

İşçi raporunda bir sapma yazıyor: değişiklikten hemen sonraki ara koşuyu yük 34'teyken kapıda beklemeden başlatmış, bu koşuyu kanıt saymamış.

## Kaynak bulguların durumu

| Önceki bulgu | Durum | Kanıt |
|---|---|---|
| ORTA: sayfa bağlantısını hiçbir test ölçmüyor | Kapandı | Önceki kayıtta yeşil kalan beş bağlantı sabotajı W15'te kırmızı; ayrıca betiği iki kez yükleme ve sıra değiştirme sabotajları kırmızı |
| DÜŞÜK: "Görünümü kaydet" bildirimi yanlış metin | Kapandı | Çizilmiş sayfa, gerçek `diten-datatable.js` ve gerçek Notyf ile jsdom'da akış koştu: tr `Görünümü Kaydet` → `Değişiklikler başarıyla kaydedildi.`; 7 dilin hepsinde yeni metin, hata 0 |
| DÜŞÜK: `index.js` `t()` sabotajı yeşil | Kapandı | V14 kırmızı |
| DÜŞÜK: `window.L10n = normalized` sabotajı yeşil | Kapandı | V15 kırmızı |
| DÜŞÜK: `ReceivedAtUtc` `step` farkı | Girdi düzeyinde kapandı | Çizilmiş Create ve Edit'te iki girdi de `step="1"`; aşağıdaki bilgi notuna bakın |

## Bulgular

| Önem | Bulgu | Yol | Sonraki adım |
|---|---|---|---|
| DÜŞÜK | W15 sayfa metnindeki sırayı ölçer, çalışma sırasını ölçmez. Create'te yükleyiciye `defer` eklenmesi ve etiketin HTML yorumuna alınması W15 ve PVG vitest'te yeşil kaldı. Bugünkü kod bu durumda değil. | `PvgCaseIntakeTriageWebTests.cs:737` | Bilgi; DL-7'nin canlı kapısında ekran zaten ölçülür |
| Bilgi | Kabuk anahtarının köprünün önüne geçmesi sabotajı yeşil kaldı. Çakışma 7 dilde 0, bugün görünür etkisi yok. | — | Bilgi |
| Bilgi | Edit `ReceivedAtUtc`'yi doldurmuyor (`form.js:263` yalnız `AwarenessAtUtc`). Saniyeli kayıt açılınca alan boş ve zorunlu; kullanıcı değeri yeniden yazar. Düzeltmeden önce de sonra da aynı; DoR F-4'te kayıtlı. | `form.js:263` | Edit sessiz silme işiyle birlikte değerlendirilir |

Önceki kayıttan açık kalan: Edit'in üç isteğe bağlı alanı sessizce silmesi (ORTA; servis). Bu tur yeniden ölçülmedi.

## Temiz ölçülenler

- **Kapsam.** Yalnız dört izinli dosya (C# +71/-0, `_Form` +1/-1, `_IndexL10n` +1/-0, vitest +33/-2); `git diff --check` çıkış 0. Merge'in ilk ebeveyne farkı aynı dört dosya. Test gevşetme, silme, atlama yok; `it` 32→34, C# satır 42→49. Liste `mount`unun eski fikstürü `l10n ?? {…}` içinde aynen duruyor; eski dört çağrı tek argümanlı. Sır ya da kişi verisi 0.
- **Testler.**

  | Süit | Geçen | Başarısız | Atlanan |
  |---|---|---|---|
  | PVG vitest | 34 | 0 | 0 |
  | `PvgCaseIntakeTriageWebTests` | 49 | 0 | 0 |
  | C# üçlü filtre | 73 | 0 | 0 |
  | Tüm vitest | 5383 | 32 | 0 |

- **Tüm vitest kırmızıları.** 32 ad önceki listeyle ad bazında aynı, hiçbiri PVG dosyasında değil. `Test timed out` 0, suite hatası 0.
- **Doğrulayıcı.** Çıkış 1, 12 madde; önceki çıktıyla bayt düzeyinde aynı (sha256 `e8ffe96e…`).
- **Yan etki.** Ortak okuyucu farkı 7 dilde yalnız `diten-datatable.js` `RecordSaved`. Kabuk ile köprü anahtar çakışması 0.
- **Tarih.** `form.js` ve denetleyici değişmedi. Gerçek MVC vekili (antiforgery dahil, sahte Gateway): create ve update'te `…:30` → 200 ve `…:30Z`; `HH:mm` → 200 ve `:00Z`; `.000` ve `.500` → 400, Gateway çağrısı yok. Chromium 152'de `step`siz `…:30` geçersiz, `step="1"` ile geçerli ve `…:30` gönderiliyor (kodla kurulan tek girdide; çizilmiş formda değil).
- **Sabotajlar.** 26 sabotajın çapası tam bir kez eşleşti, hepsi sha256 ile geri yazıldı, `.cshtml` dosyalarına touch uygulandı. 23'ü kırmızı (hepsi ölçülen iddiayla; derleme hatası 0), 3'ü yeşil kaldı (yukarıdaki DÜŞÜK ve bilgi satırları).

  | Sabotaj | Kırılan test |
  |---|---|
  | Create yükleyici yok; `form.js` önce; yükleyici iki kez; `form.js` iki kez | W15 Create |
  | Edit köprü yok; `form.js` önce | W15 Edit |
  | Details yükleyici yok; köprü yükleyiciden sonra; `details.js` önce | W15 Details |
  | Index yükleyici yok; `index.js` önce; köprü iki kez | W15 Index |
  | `RecordSaved` satırı yok | W16, V15 |
  | `RecordSaved` sayfa kaynağından | W16 |
  | `ReceivedAtUtc` `step` yok; `step="60"` | W17 Received |
  | `AwarenessAtUtc` `step` yok | W17 Awareness + 3 mevcut vitest |
  | `index.js` `t()` anahtar döner; satır eylemi anahtar; Ayrıntılar başlığı anahtar | V14 |
  | Boş durum anahtar | V14 + 2 mevcut vitest |
  | `window.L10n = normalized`; `PvgCaseIntakeTriageL10n = normalized` | V15 |

## Ölçülmeyenler

- Canlı ekran, Gateway, gerçek servis. Zincir sahte ağla ölçüldü; servisin saniyeli değeri kabulü koşulmadı. `pvg.case-intake-triage.*` anahtarlarının bir role verilmediği önceki kayıttan alındı.
- Bildirim akışı gerçek tarayıcıda koşmadı (jsdom); görsel görünüm ölçülmedi. Safari ve Firefox yok.
- Parent'ta C# koşulmadı; karşılaştırma önceki kaydın 66 sayısı ve ad kümesiyle yapıldı (66→73).
- `ar` sağdan sola görünüm ve çeviri kalitesi; yalnız yedi dilde değerin geldiği ölçüldü.
- Toplu koşuda 1 dakikalık yük 46'ya çıktı (başlangıçta 4,09; kaynağı vitest); zaman aşımı yok.
