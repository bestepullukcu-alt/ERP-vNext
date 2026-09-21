# WORK PACKAGE — WP-FREQ-F11 · FREKANS bölümü (03) mockup birebir + app field (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**F9/F10 landing sonrası HEAD üstü** — aynı dosyalar, SIRALI; F10 ile birlikte tek ajanda dispatch edilebilir). Kullanıcı: FREKANS kartı mockup'a hizalansın. **Frontend only** (_Editor.cshtml FREKANS bölümü + form.js cadence/freq fields + visit-frequency-create.css + L10n). buildPayload/id KORUNUR (vfpFrequencyType/vfpRequiredVisitCount/vfpPeriodType).

## Somut istekler
1. **Başlık:** `EditorSectionFrequency` "SIKLIK KONTROLÜ" → **"FREKANS — ASIL DEĞER"** (mockup: 03 FREKANS — ASIL DEĞER). Detay/başka yerde paylaşımlıysa (DvSection*) shared anahtarı bozma → yeni `EditorSectionFrequencyLong` ekle (F7'deki EditorSectionTargetLong deseni). L10n 7 dil.
2. **Field'lar task-create:** Sıklık türü/Gerekli ziyaret/Dönem → app field (label `form-label fw-medium` + diten-field/form-control). FrequencyType + PeriodType select2 arama'lı (F9 deseni, kısa liste → minimumResultsForSearch opsiyonel); RequiredVisitCount number `form-control` (diten-field ikon). **Yerel etiketler korunur** (Sıklık türü/Gerekli ziyaret/Dönem — mockup'taki ham FrequencyType yazımı değil). buildPayload alanları/id KORUNUR.
3. **ÇÖZÜLEN CADENCE kutusu (mockup HTML):** tek satır flex (align-center gap14 wrap), radius6, padding 16/18, **bg `var(--bs-primary-bg-subtle)`** (oklch 0.97 .02 295), **border `var(--bs-primary-border-subtle)`** (oklch 0.9 .04 295):
   - "ÇÖZÜLEN CADENCE" label: 11px uppercase ls .06em 600 `var(--bs-primary)`.
   - value "ayda 4 ziyaret": **18px 600 `var(--bs-heading-color)`**.
   - meta (sağda, `margin-left:auto`): JetBrains Mono/monospace 11px `var(--bs-primary)` → "frequencyType · count · periodType" (ör "weekly · 4 · month").
   - Mevcut yığılmış (label üstte, value altta) yerine **tek satır**; form.js cadence render değeri + meta üretir.

## Kapsam
- `_Editor.cshtml` (FREKANS bölümü): başlık Long anahtar; alanlar app field; cadence kutusu tek-satır markup (label + `#vfpFreqSentence` value + meta span).
- `form.js`: cadence render → value (ayda N ziyaret) + meta (type·count·period) ayrı; FrequencyType/PeriodType select2 init (options sonrası); RequiredVisitCount app. FreqType×PeriodType allow-map/validation/buildPayload/id KORUNUR.
- `visit-frequency-create.css`: cadence kutusu tek-satır + 18px value + mono meta; app field. Tema-duyarlı.
- L10n: EditorSectionFrequencyLong (7 dil).

## KORU / YAPMA
- buildPayload/id/validation/FreqType×PeriodType allow-map DEĞİŞMEZ. Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA (yalnız FREKANS). Shared L10n anahtarı bozma (Long ekle). Hardcode vocabulary yok (contract). Tema-duyarlı (--bs-* token). Yalnız _Editor(FREKANS)+form.js(cadence/freq)+css+resx.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Editor.cshtml + form.js + css + resx. Backend/liste/detay/diğer bölüm diff YOK.
- **E4:** FREKANS başlık "FREKANS — ASIL DEĞER"; alanlar task-create (select2 arama'lı); cadence kutusu tek-satır (label + 18px "ayda 4 ziyaret" + mono meta sağda); buildPayload/işlev aynı; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (F9/F10 landing SONRASI; F10 ile birleşik olabilir)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F11 · FREKANS bölümü mockup birebir + app field (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <F9/F10 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F11-frequency-section-mockup.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (FREKANS bölümü) · wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (cadence/vfpFreqSentence/frequencyType/periodType allow-map) · frontend/Diten.Web/Views/Tasks/Create.cshtml (select2/diten-field) · _DetailsQuickView (EditorSectionFrequency shared mı kontrol).

NE (frontend; buildPayload/id KORUNUR; yalnız FREKANS):
 1) Başlık EditorSectionFrequency "SIKLIK KONTROLÜ"→"FREKANS — ASIL DEĞER"; shared ise yeni EditorSectionFrequencyLong ekle (L10n 7 dil).
 2) Alanlar app field: label form-label fw-medium; FrequencyType+PeriodType select2 arama'lı (options sonrası init, minimumResultsForSearch opsiyonel); RequiredVisitCount number form-control (diten-field). Yerel etiketler korunur. buildPayload/id/allow-map/validation KORUNUR.
 3) ÇÖZÜLEN CADENCE kutusu tek-satır: flex align-center gap14 wrap, radius6 padding16/18, bg var(--bs-primary-bg-subtle) border var(--bs-primary-border-subtle); "ÇÖZÜLEN CADENCE" label(11px uppercase ls.06em 600 --bs-primary) + value(18px 600 --bs-heading-color, #vfpFreqSentence) + meta(margin-left auto, monospace 11px --bs-primary, "type · count · period"). form.js cadence render value+meta üretir.
KORU/YAPMA: buildPayload/id/validation/allow-map DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA; shared L10n bozma (Long ekle); hardcode vocabulary yok; tema-duyarlı; yalnız _Editor(FREKANS)+form.js(cadence/freq)+css+resx.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor+form.js+css+resx; backend/liste/detay/diğer bölüm diff yok; buildPayload/id korunmuş. Ayrı commit. §22 TÜRKÇE. K13.
Durma: buildPayload/allow-map/id bozuluyorsa; select2 cadence'i etkiliyorsa; kapsam FREKANS dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)** — F12 ile birleşik commit
```
Commit: 56344521 (F11+F12) · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-fa-verify @56344521
```
- ✅ Başlık "FREKANS — ASIL DEĞER" via yeni `EditorSectionFrequencyLong` (shared `EditorSectionFrequency` korundu → Detay bozulmadı). FrequencyType/PeriodType select2 arama (F9 köprüsü); RequiredVisitCount diten-field. ÇÖZÜLEN CADENCE tek-satır (label + 18px value #vfpFreqSentence + mono meta #vfpFreqRaw), bg/border --bs-primary subtle.
- ✅ buildPayload/priority/allow-map (checkedValue vfpPriority/ALLOWED_PERIODS) silinmemiş (grep 0). Kapsam: _Editor+form.js+css+7 resx; backend/detay/liste/diğer bölüm = 0. Diten.Web.Tests 137/0.
```
