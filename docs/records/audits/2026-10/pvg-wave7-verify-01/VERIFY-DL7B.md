# PVG yedinci dalga — bağımsız doğrulama: WP-0230-DL7B (AC-32, AC-15, AC-2)

> Control Tower kaydı, 2026-10-30. Kuyrukta `DISPATCHED → REWORK` geçişinin kanıtıdır. Kabul ya da entegrasyon kaydı değildir.
> Karar: **KABUL EDİLEMEZ.** Tek engel var. Düzeltme ayrı iş olarak açılır: `WP-0230-DL7B-R1` (kart §0.2 Y1, SOP §28.2).

## Teslim

| | |
|---|---|
| İşçi dalı | `feature/pvg/wp-0230-dl7b-r1` (işçi oturumu pvg-dl7b) |
| İş commit'i | `18e23b575`; HEAD^1 = Expected HEAD `ef68ec8d3` |
| CT dalına birleştirme | `77c216e84` (yerel, push edilmedi). `git diff 18e23b575 77c216e84 -- frontend/Diten.Web` boş. |
| DoR | `docs/records/audits/2026-10/pvg-wave7-dor-01/DOR.md` |
| Prompt | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL7B-v1.7.md` (sha256 `1c455145…`); dağıtım kapısı 2026-10-30 çıkış 0 |

Ölçümü işi yazmayan bir CT ölçücüsü (alt ajan) ayrı bir worktree'de `77c216e84`'te yaptı. Madde "Saniyeli değer"in tarayıcı ölçümü ve engelin kod okuması CT'nindir.

## Engel: seçenek adları ve ekran iletileri ham kod olarak görünüyor

- **Kök neden.** Köprü camelCase üretiyor; yükleyici anahtarları çevirmeden yazıyor; ekran PascalCase okuyor.
  - `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/_IndexL10n.cshtml:7`: `Json.Serialize`. `Program.cs`'te ad politikası ayarı yok, MVC varsayılanı camelCase.
  - `frontend/Diten.Web/wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.l10n.js:8-10`: `JSON.parse` sonucunu olduğu gibi `window.L10n`'e yazıyor.
  - `form.js:8,127,131,135`: `L[key]`, `L[...ReportType...]`, `L[...ProductRole...]`, `L.CountryUnknown`. `index.js:8` da aynı kalıpta.
- **Ölçüm.** Gerçek MVC sunucusunun çizdiği tr sayfası, gerçek `index.l10n.js` ve gerçek `form.js` (jsdom):
  - Seçimlerde `Spontaneous`, `Suspect`, `unknown` görünüyor; köprüde `reportTypeSpontaneous: Spontan` duruyor.
  - Okuma hatasında uyarı metni `OptionsLoadFailed`. `form.js`'teki 8 `t()` anahtarının 8'i ham çıkıyor.
  - Karşı ölçüm: yalnız yükleyici bellekte PascalCase yapılınca `Spontan` ve `Bilinmiyor` geliyor; karşılığı olmayan kod kendisi kalıyor.
- **CT okuması.** Üç dosyayı CT de okudu; çizilmiş sayfada `reportTypeSpontaneous` var, `ReportTypeSpontaneous` yok. Depoda aynı tuzak için emsal ve açıklama var: `frontend/Diten.Web/wwwroot/assets/js/CRM/CyclePeriods/index.l10n.js:36-45`.
- **Neden testler görmedi.** V11 ve V12 PascalCase fikstürü doğrudan veriyor ve yükleyiciyi atlıyor (`pvg-case-intake-triage-ui.test.js:658, :1159, :1172`). Köprü denetimi yalnız C# kaynağını tarıyor (`:63`).
- **Yayılım.** Yükleyici `0860632f1`'den beri aynı. `index.js` ve `details.js` iletileri de etkileniyor; DL7A'nın doğrulaması bunu görmedi. Seçenek adı ise DL7B'nin yeni gereği (AC-32). Düzeltme izinli yolda, servis ya da ortak dosya gerekmiyor.

## Diğer bulgular

| Önem | Bulgu | Yol |
|---|---|---|
| ORTA | Options ve Detail yolunda kimlik başlığı ölçülmüyor. Kod doğru, ama `ProxyGetAsync`'ten `AddAuthHeaders` kapısı kalkınca denetleyici sınıfı 39/39 yeşil kaldı. W4 ve 401 testi yalnız `List` ve `RouteDraft` çağırıyor. | `PharmacovigilanceCaseIntakeTriageController.cs:292-296`, `PvgCaseIntakeTriageWebTests.cs:243, :255, :269-271` → R1 |
| ORTA | Edit üç isteğe bağlı alanı sessizce siliyor: ReporterContactSummary, EventOnsetDate, EvidenceLinkReferences. Servis tam değiştirme yapıyor; yanıt bu alanları döndürmüyor ve form önceden doldurmuyor. DL7A'dan kalıyor. Bugün hiçbir yazma canlıda başarılı olamadığı için veri kaybı yok. | `PvgIntakeDraftApplicationService.cs:208-223, :825-853`; `_Form.cshtml:111, :138, :217` → servis değişikliği ister, R1 dışında; sahibe ayrı karar |
| DÜŞÜK | Saniye asimetrisi: `ReceivedAtUtc`'de `step` yok (`_Form.cshtml:75`), `AwarenessAtUtc`'de `step="1"` var (`:79`). API'den saniyeli alınma anıyla açılmış kayıt ekranda aynı değerle gönderilemez. | Edit işiyle birlikte |
| DÜŞÜK | Sabotaj S3.08'de W12 de kırmızı oluyor (c bölümü); prompt yalnız (a)'yı konuşuyor. | Bilgi |

## Temiz ölçülenler

- **Kapsam.** Taban → işçi 14 dosya, hepsi izinli yolda; korunan yol yok; `git diff --check` temiz. `index.js` farkı boş.
- **Kod.**
  - 11. eylem ilk iş `RequireJson(ReadPermission)` çağırıyor (`Controller:108`); kiracı claim'den, Bearer çerezden; hata eşlemesi diğer GET'lerle aynı.
  - Seçenek kodu istemcide ve denetleyicide sabit değil (`_Form:63-65` yalnız muaf SourceType).
  - NE (4) adları denetleyicide 0 satır; `SuspectProductText` üretimde 0; `style=` 0; JS'te görünür sabit metin yok.
- **Resx.** Yedi dilde 111 anahtar, anahtar özeti aynı (`1306788c…`). İngilizce kopya yalnız dokuz istisna. zh, ar, ru değerlerinin hepsi kendi yazısında.
- **Testler.** Her komuttan önce yük ölçüldü (başlangıçta 1,4–11,9; 12'nin üstünde görülünce beklendi).

  | Süit | Geçen | Başarısız | Atlanan |
  |---|---|---|---|
  | PVG vitest | 31 | 0 | 0 |
  | C# üçlü filtre (W1–W12 dahil) | 63 | 0 | 0 |
  | Tüm vitest | 5380 | 32 | 0 |

  `dotnet build`: 0 hata, 13 uyarı (taban da 13). Doğrulayıcı: 12 madde, yeni madde yok.
- **Tüm vitest kırmızıları.** 32'nin hiçbiri PVG dosyasında değil; zaman aşımı iletisi yok. Dosyalar: campaign-targeting-admin-ui, consent-preference-admin-ui, diten-enter-primary, diten-field-icons, global-confirm-input-type, global-confirm-text-is-text, objectives-edit-hydration, page-help-ledger, planning-cycles-owner-position, planning-cycles-register, role-assignments-label-ledger, strategy-apis, strategy-periods-owner-position, strategy-periods-register, wcn-calendar-view, wcn-dead-code-and-timer. Taban karşılaştırması bu kayıtta yapılmadı; işçinin raporu aynı sayıyı veriyor.
- **Sabotajlar.** Prompt'un 80 sabotajının hepsi koşuldu (61 vitest, 2 doğrulayıcı, 17 C#). Çapası tutmayan 0, beklenen kırmızı gelmeyen 0; hepsi sha256 ile geri alındı. Ek iki sabotaj: `ProxyGetAsync` kapısı → kırmızı yok (ORTA bulgu); `AddAuthHeaders` kiracı kapısı → 401 testi kırmızı.

## Saniyeli değer (CT tarayıcı ölçümü)

İşçi, jsdom'un `…:30.000` döndürdüğünü ve vekilin bu biçimi reddedeceğini işaretledi. CT gerçek tarayıcıda ölçtü (Chromium 152, uygulamanın tarayıcı bölmesi; `datetime-local`, `step="1"`):

| Atanan değer | Gönderilen (FormData) | Geçerli | Vekil biçimi |
|---|---|---|---|
| `2026-10-30T10:15:30` | `2026-10-30T10:15:30` | evet | kabul |
| `2026-10-30T10:15:00` | `2026-10-30T10:15` | evet | kabul |
| `2026-10-30T10:15:30.000` | `2026-10-30T10:15:30` | evet | kabul |
| `2026-10-30T10:15:30.500` | `2026-10-30T10:15:30.5` | hayır | ret |

`.000` farkı Chromium'da yok. Safari ve Firefox ölçülmedi.

## Ölçülmeyenler

- Canlı ekran ve canlı servis/Gateway. `pvg.case-intake-triage.*` anahtarları hiçbir role verilmedi (MOD-0018); yetki verilene kadar ekran 403 döner. DL-7'nin gerekli kanıtı E4, kabul canlı kapıya bağlı kalır (DALGA5-01 D5-5).
- Köprü en ve tr çizildi, yalnız tr okundu; diğer beş dil ölçülmedi (mekanizma aynı).
- Çeviri terim kalitesi (yalnız mekanik denetim). `ar` sağdan sola görünüm.
- Edit silmesinin çalıştırılması (statik okuma; servis koşulmadı).

## CI notu (onay isteği)

Bu kayıt onay isteğine girmedi. Onay isteğinin kırmızıları ana dalla aynı (`phase1`: `AuditTrailStandardTests` iki test ve `DocsPathGuardTests`; `service-tests (platform-app)`: `TreasuryReferenceCatalogTests`), ana dalın `bf3b0cd57` koşularında da aynı testler kırmızı. `Diten.Web.Tests` ve vitest CI'da koşmuyor; bu engeli CI göremezdi.
