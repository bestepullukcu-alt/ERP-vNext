# WORK PACKAGE — WP-ST-EDIT-M · Frekans bölümü mockup (mode buton seçici + primary info-box + insan-dostu alanlar) (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`3df2d7de` üstü). **E4 rötuşu:** Frekans bölümü mockup gibi — (1) mode seçici raw `<select>` → **3 buton** (Politikaya işaretçi / Beyan edilen niyet / Yok + alt-etiket motor okur/yalnızca doküman/referans verilmedi); (2) "Frekans, Ziyaret Sıklığı Politikaları'nda yönetilir" info-box → **primary color**; (3) seçimlerde gelen alanlar **insan-dostu** (raw "weekly"/"day"/"declared-intent" DEĞİL — L10n friendly). **Frontend** (form.js + _Form.cshtml + strategy-create.css + resx). Backend/contract DEĞİŞMEZ (FrequencyIntentJson sözleşmesi korunur).

## Kanıt
- `_Form.cshtml` FrequencySection: `.st-freq-info` box (dot/strong/text/link — EDIT-B; primary değil) + `<select id="frequencyMode">` (raw opsiyonlar) + `#frequencyPolicyBlock` (policy select) + declared-intent alanları (frequencyType/periodType/requiredVisitCount/intentNote).
- `form.js` renderFrequency (~296): `frequencyMode` select'e raw opsiyon (`<option value="m">m</option>`); frequencyType/periodType raw enum opsiyon (weekly/day — friendly YOK). policy select `/visit-frequency-policies` load.
- **friendly frequency L10n YOK** (FrequencyType_*/PeriodType_*/FrequencyMode_* yok; yalnız IntentNote). Mockup: mode 3 buton + primary info + policy "VFP-… · A+ hekim 2/ay".
- Referans: KAPSAM mode buton deseni `.st-scope-type` (choice-box, EDIT-H/J) — mode butonları için birebir reuse edilebilir. VFP cadence/period label deseni (L10n map).

## NE (frontend; backend/contract DEĞİŞMEZ)
1. **Mode → 3 buton (choice-box):** `<select id="frequencyMode">` yerine **gizli input #frequencyMode** + 3 buton (KAPSAM `.st-scope-type` deseni ya da yeni `.st-freq-mode`): Politikaya işaretçi (motor okur) / Beyan edilen niyet (yalnızca doküman) / Yok (referans verilmedi). Buton tıklama → gizli mode set + aktif class + ilgili blok göster/gizle (policy-reference→policy select; declared-intent→frekans alanları; none→hiç). L10n: `FrequencyMode_<m>` (başlık) + `FrequencyModeSub_<m>` (alt-etiket). cfg.frequencyIntentModes'a dayalı (hardcode YOK). FrequencyIntentJson (mode + visitFrequencyPolicyId + frequencyType + requiredVisitCount + periodType + intentNote) sözleşmesi KORUNUR.
2. **Info-box primary:** `.st-freq-info` css → primary tint (border-left/sol nokta primary, bg `var(--bs-primary-bg-subtle)` veya faint primary, link primary). Mockup görünümü. Metin/link mantığı DEĞİŞMEZ.
3. **İnsan-dostu alanlar (L10n):** declared-intent alanları friendly — `frequencyType` opsiyonları `FrequencyType_<v>` (ör. weekly→"Haftalık", monthly→"Aylık", quarterly→"Çeyreklik", daily→"Günlük", custom→"Özel"…), `periodType` opsiyonları `PeriodType_<v>` (day→"Gün", week→"Hafta", month→"Ay", quarter→"Çeyrek", cycle→"Döngü"…) — form.js `L['FrequencyType_'+v] || humanize(v)` map (VFP deseni; contract değerleri sürer, humanize fallback). Alan etiketleri mockup: "Frekans", "Dönem başına ziyaret", "Dönem", "Bu ritmin gerekçesi". Policy dropdown opsiyonları friendly (kod · N/period; mevcut load'dan text). "declared-intent" gibi ham mode string'i artık UI'da GÖRÜNMEZ (mode buton).
4. **css + resx (7 dil):** mode buton stili (choice-box reuse) + info-box primary + FrequencyMode_*/Sub_* + FrequencyType_*/PeriodType_* friendly etiketler. PascalCase köprü.

## KORU / YAPMA
- Backend/contract DEĞİŞMEZ (FrequencyIntentJson mode/policy/type/count/period/note sözleşmesi + create/update). form.js frekans STATE + submit mantığı KORUNUR (yalnız mode kontrolü select→buton + friendly label + info primary). KAPSAM/Segment/Ürün/İçerik/sağ panel DOKUNMA. Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA. "policy NEVER written" ilkesi (declared-intent motor okumaz) korunur — yalnız görsel. Uydurma frekans/mode YOK (contract + L10n). Tema/L10n köprüsü.
- **DUR:** mode buton FrequencyIntentJson mode sync'ini bozuyorsa; friendly label contract değerini değiştiriyorsa (yalnız GÖRÜNEN etiket friendly, value ham kalır) → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: form.js + _Form.cshtml + strategy-create.css + resx. Backend/Controller/ViewModel/Liste/Detay diff YOK.
- **E4:** Frekans 3 mode butonu (alt-etiketli) + primary info-box + declared-intent alanları friendly (Haftalık/Ay/…, ham kod yok); mode değişince doğru alanlar; kaydet→FrequencyIntentJson doğru. **Razor+css+resx → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-M · Frekans bölümü mockup (mode buton + primary info + friendly alanlar) (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 3df2d7de üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-M-frequency-mockup.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (FrequencySection: .st-freq-info + #frequencyMode select + #frequencyPolicyBlock + declared alanlar) · wwwroot/assets/js/CRM/StrategyTemplates/form.js (renderFrequency ~296, frequencyType/periodType/policy render) · wwwroot/assets/css/strategy-create.css (.st-freq-info* + .st-scope-type mode-buton referans) · resx · (referans friendly label) VFP form.js/index.js cadence/period L10n. Mockup: /c/tmp/mockup-strategy.html Düzenle Frekans (server 127.0.0.1:8799).

NE (frontend; backend/contract DEĞİŞMEZ):
 1) #frequencyMode select → gizli input + 3 buton (choice-box, KAPSAM .st-scope-type deseni reuse ya da .st-freq-mode): FrequencyMode_<m> başlık + FrequencyModeSub_<m> alt (motor okur/yalnızca doküman/referans verilmedi). Buton→gizli mode set + aktif + blok göster/gizle (policy-reference→policy select, declared-intent→frekans alanları, none→hiç). cfg.frequencyIntentModes'a dayalı. FrequencyIntentJson sözleşmesi KORU.
 2) .st-freq-info css → primary (border/dot/link primary, bg primary-subtle/faint). Metin/link mantığı DEĞİŞMEZ.
 3) declared-intent alanları friendly: frequencyType opsiyon L['FrequencyType_'+v]||humanize(v) (weekly→Haftalık, monthly→Aylık, quarterly→Çeyreklik, daily→Günlük, custom→Özel), periodType L['PeriodType_'+v]||humanize (day→Gün, week→Hafta, month→Ay, quarter→Çeyrek, cycle→Döngü). Ham kod (declared-intent/weekly/day) UI'da görünmez. policy dropdown friendly (kod · N/period). value HAM kalır (yalnız görünen etiket friendly).
 4) css + resx 7 dil: mode buton + info primary + FrequencyMode_*/Sub_* + FrequencyType_*/PeriodType_*. PascalCase köprü.
KORU/YAPMA: backend/contract DEĞİŞMEZ; FrequencyIntentJson (mode/policy/type/count/period/note) + submit + "policy asla yazılmaz" korunur (yalnız görsel); KAPSAM/Segment/Ürün/İçerik/sağ panel DOKUNMA; Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA; uydurma yok (contract+L10n); tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff form.js+_Form+css+resx; backend/Controller/ViewModel/Liste/Detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-M — Frekans bölümü mockup (mode buton + primary info-box + insan-dostu alanlar) (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: mode buton FrequencyIntentJson sync'i bozuyorsa; friendly label contract value'sunu değiştiriyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: f0b2902b · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitm-verify @f0b2902b
```
- ✅ **Kapsam (11 dosya, +225/−12):** form.js + _Form.cshtml + strategy-create.css + _IndexL10n + 7 resx. **backend/Controller/ViewModel/liste/detay TEMİZ** ✓.
- ✅ Mode `<select>` → gizli #frequencyMode + `#frequencyModeButtons` 3 choice-box buton (`.st-freq-mode`, cfg.frequencyIntentModes; FrequencyMode_*/Sub_* etiket; click→value+aktif+blok göster/gizle). `.st-freq-info` primary (bg primary-bg-subtle + link primary). frequencyType/periodType option **value HAM** (weekly/day/cycle-based…) + görünen etiket `L['FrequencyType_'+v]||humanize` / `L['PeriodType_'+v]||humanize` (ham enum UI'da görünmez). policy dropdown friendly.
- ✅ **KORU=0:** FrequencyIntentJson (mode/policy/type/count/period/note) byte-for-byte + submit + "policy asla yazılmaz" korundu; KAPSAM/Segment/Ürün/İçerik/sağ panel dokunulmadı.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: Frekans 3 mode butonu + primary info + insan-dostu alanlar. **FLEET RESTART.**

**WP-ST-EDIT-M KOMPLE.**
```
