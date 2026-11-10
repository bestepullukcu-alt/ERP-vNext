# PVG yedinci dalga — bağımsız doğrulama: WP-0230-DL7B-R1 (AC-32)

> Control Tower kaydı, 2026-10-30. Kuyrukta `DISPATCHED → VERIFIED` geçişinin kanıtıdır. Kabul ya da entegrasyon kaydı değildir.
> Karar: **KABUL EDİLEBİLİR.** Engel yok. Bir ORTA ve üç DÜŞÜK bulgu açık. DL-7'nin gerekli kanıtı E4, kabul canlı kapıya bağlı kalır.

## Teslim

| | |
|---|---|
| İşçi dalı | `feature/pvg/wp-0230-dl7b-r1` (işçi oturumu pvg-dl7b) |
| İş commit'i | `9e9cf178f`; HEAD^1 = Expected HEAD `18e23b575` |
| CT dalına birleştirme | `88ce1a6e2` (yerel, push edilmedi) |
| Reddedilen tur | `docs/records/audits/2026-10/pvg-wave7-verify-01/VERIFY-DL7B.md` |
| DoR | `docs/records/audits/2026-10/pvg-wave7-dor-02/DOR.md` |
| Prompt | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL7B-R1-v1.1.md` (sha256 `b4a06904…`); dağıtım kapısı 2026-10-30 çıkış 0; başlatma sözü sahibin "Başlat" yanıtı |

Ölçümü işi yazmayan bir CT ölçücüsü (alt ajan) `88ce1a6e2`'de yaptı. Sabotajlar ve toplu vitest çalışma ağacı dışındaki karalama kopyalarda koştu.

## Önceki engelin durumu: kapandı

- **Kök neden giderildi.** `index.l10n.js` köprünün her anahtarını PascalCase'e çevirip yazıyor.
- **Gerçek zincir ölçümü.** Razor'un çizdiği 28 sayfa (4 sayfa × 7 dil) gerçek yükleyiciden geçti. 22 anahtarın hepsi dolu, eksik 0.
  - tr seçimler: `Spontan`, `Çalışma`, `Literatür`, `Talep Üzerine`, `Diğer`; `Şüpheli`, `Birlikte Kullanılan`; `Bilinmiyor`.
  - tr iletiler: seçenek okuma 500 → `Seçenek listeleri okunamadı. Kaydetme kullanılamıyor.`, seçim kutuları ve kaydet kapalı; detayda 409 → `Bu kayıt siz okuduktan sonra değişti…`. Karşılığı olmayan kod kendisi kalıyor.
  - Karşı ölçüm: parent yükleyicisiyle aynı sayfada `Spontaneous`, `unknown`, `OptionsLoadFailed` çıkıyor. Ölçüm ayırt edici.
- **İkinci önceki bulgu (ORTA) kapandı.** `ProxyGetAsync` kapısı kalkınca W14 Options ve Detail kırmızı.

## Bulgular

| Önem | Bulgu | Yol | Sonraki adım |
|---|---|---|---|
| ORTA | Sayfanın yükleyiciyi yüklemesini ve `form.js`'ten önce yüklemesini hiçbir test ölçmüyor. V13 yükleyici → `form.js` zincirini, W13 yalnız çizilen köprü JSON'unu ölçer. Yükleyici etiketini silen ve sırayı değiştiren iki sabotaj yeşil kaldı; gerçek sayfada özgün hata aynen geri geldi. `Edit`, `Details`, `Index` için aynı sınıf sabotaj da yeşil (ekran etkisi koşulmadı). Bugünkü kod doğru: 28 sayfanın hepsinde sıra kabuk `window.L10n` < köprü < yükleyici < sayfa betiği. Görev metni bunu istemiyordu, engel değil. | `Create.cshtml:16-17`, `Edit.cshtml:15`, `Details.cshtml:118`, `Index.cshtml:29`; `PvgCaseIntakeTriageWebTests.cs:204-208` | Ayrı iş |
| DÜŞÜK | Yan etki, kullanıcıya görünür: "Görünümü kaydet" başarı bildirimi `Başarılı` iken `Görünümü Kaydet` oldu. Okuyucu `l.RecordSaved || l.SaveView`; köprü `RecordSaved` taşımıyor, `SaveView` taşıyor. | `diten-datatable.js:1060`; `_IndexL10n.cshtml:40`; `_GlobalNotification.cshtml:160-163` | Ayrı iş; köprüye tek satır |
| DÜŞÜK | `index.js` `t()` anahtar döndürse liste testi yeşil kalıyor: fikstürde değerler anahtarla aynı. Kod doğru; gerçek sayfada `Düzenle`, `Ayrıntılar`. | `pvg-case-intake-triage-ui.test.js:369` | Ayrı iş |
| DÜŞÜK | `window.L10n = normalized` sabotajı yeşil kaldı; bu kabuğun 24 anahtarını siler. Kod bugün `Object.assign` kullanıyor. | `index.l10n.js` | Ayrı iş |

Önceki kayıttan açık kalanlar: Edit'in üç isteğe bağlı alanı sessizce silmesi (ORTA; servis) ve `ReceivedAtUtc`'deki `step` farkı (DÜŞÜK). Bu tur yeniden ölçülmedi.

## Temiz ölçülenler

- **Kapsam.** Yalnız üç izinli dosya (C# +73/-11, vitest +36/-0, yükleyici +9/-2); `git diff --check` boş. Silinen test ve atlama yok. Vitest'te it 31→32, expect 257→265; C#'ta metot 18→20, Assert 144→165. W12'nin Assert sayısı 19→19; silinen satırlar `HostOver`'a taşındı, ayarlar aynı. Sır ya da kişi verisi yok.
- **Testler.**

  | Süit | Geçen | Başarısız | Atlanan |
  |---|---|---|---|
  | PVG vitest | 32 | 0 | 0 |
  | `PvgCaseIntakeTriageWebTests` | 42 | 0 | 0 |
  | C# üçlü filtre | 66 | 0 | 0 |
  | Tüm vitest | 5381 | 32 | 0 |

- **Tüm vitest kırmızıları.** 32 ad, parent `18e23b575`'teki kendi koşuyla, önceki doğrulamanın ham verisiyle ve işçinin iki listesiyle ad bazında aynı. Hiçbiri PVG dosyasında değil. `Test timed out` 0.
- **Doğrulayıcı.** Çıkış 1, 12 madde; yol normalize edilince işçinin taban ve son çıktısıyla fark yok.
- **Sabotajlar.** Her çapa tam bir kez eşleşti, hepsi sha256 ile geri alındı.

  | Sabotaj | Kırılan test | Sonuç |
  |---|---|---|
  | Anahtar çevirisi kalkar | V13 | Kırmızı |
  | `toPascalCase` tüm dizeyi büyütür | V13 | Kırmızı |
  | `toPascalCase` ilk harfi küçültür | V13 | Kırmızı |
  | `PvgCaseIntakeTriageL10n` ataması kalkar | V13 | Kırmızı |
  | `ProxyGetAsync` kapısı kalkar | W14 Options, Detail | Kırmızı |
  | Options kimlik başlıksız | W14 Options | Kırmızı |
  | Detail kimlik başlıksız | W14 Detail | Kırmızı |
  | Başlık eklenir, 401 dönülmez | W14 Options, Detail | Kırmızı |
  | Köprü anahtarı `XCountryUnknown` | V13, V11, W13 | Kırmızı |
  | Köprü PascalCase serileştirilir | W13 | Kırmızı |
  | `form.js` `t()` anahtar döner | V13, V4, V10 | Kırmızı |
  | `details.js` `t()` anahtar döner | V4 | Kırmızı |
  | `form.js` camelCase okur | V13, V11 | Kırmızı |
  | `window.L10n = normalized` | Yok | Yeşil kaldı (DÜŞÜK) |
  | `index.js` `t()` anahtar döner | Yok | Yeşil kaldı (DÜŞÜK) |
  | Create yükleyiciyi yüklemez | Yok | Yeşil kaldı (ORTA) |
  | Create'te `form.js` yükleyiciden önce | Yok | Yeşil kaldı (ORTA) |
  | Edit köprüyü çizmez | Yok | Yeşil kaldı (ORTA ile aynı) |
  | Details yükleyiciyi yüklemez | Yok | Yeşil kaldı (ORTA ile aynı) |
  | Index yükleyiciyi yüklemez | Yok | Yeşil kaldı (ORTA ile aynı) |

## Yan etki taraması

Kabuk betiklerinden 5'i `L10n` içeriyor; camelCase okuyan 0. Kabuğun 24 anahtarıyla çakışma 0; `datatable-l10n` ile çakışma yalnız `Details`. PVG köprüsünden 9 anahtarı ortak okuyucular okuyor:

| Okuyucu | Anahtar | Önce (tr) | Şimdi (tr) | Değerlendirme |
|---|---|---|---|---|
| `dt-defaults.js:602` | Details | Detaylar | Ayrıntılar | Eş anlam; ru `Детали` → `Подробности` |
| `dt-defaults.js:1459` | ColumnVisibility | Column Visibility | Sütun Görünürlüğü | İyileşme |
| `dt-defaults.js:1467` | ShowAll | Her dilde Türkçe | Dilin kendi metni | İyileşme |
| `dt-defaults.js:561,840` | Search | Search... | Ara.. | Görünmüyor (`index.js:92` `searching: false`) |
| `diten-datatable.js:1046` | Filter | Başlık yok | Filtrele | İyileşme |
| `diten-datatable.js:1051,1053` | SaveView | Boş | Görünümü Kaydet | İyileşme |
| `diten-datatable.js:1060` | SaveView (bildirim) | Başarılı | Görünümü Kaydet | DÜŞÜK, anlam kayması |
| `diten-datatable.js:537` | SaveView (`viewName`) | Default | Görünümü Kaydet | Saklanan ad; ekrana yazılmıyor |
| `diten-datatable.js:1064,1351,1379` | ErrorOccurred | Boş | Tam cümle | Yalnız kod okuması, koşulmadı |

## Ölçülmeyenler

- Canlı ekran, Gateway, servis ve gerçek tarayıcı. Zincir jsdom ve sahte vekil yanıtlarıyla ölçüldü. Safari ve Firefox yok. `pvg.case-intake-triage.*` anahtarlarının bir role verilmediği önceki kayıttan alındı, yeniden ölçülmedi.
- `ar` sağdan sola görünüm ve çeviri terim kalitesi; yalnız mekanik denetim yapıldı (AC-15: 7 dilde 111 anahtar, İngilizce kopya yalnız adlı 9 istisna).
- Parent'ta C# ve doğrulayıcı koşulmadı; doğrulayıcı tabanı işçinin dosyası ve önceki kayıt.
- CT worktree'sinde `node_modules` boş olduğu için PVG vitest gerçek ağaçta işçi worktree'sinin vitest 2.1.9'uyla, toplu vitest karalama kopyada koştu; kopyanın `frontend/Diten.Web`'i ağaçla aynı (`diff -rq` çıkış 0; `bin`, `obj`, `node_modules`, `Data` hariç).
- Toplu koşu sırasında 1 dakikalık yük 64'e çıktı (kaynağı vitest'in kendisi, başlangıçta 6,96 ve 11,62); zaman aşımı yok.
