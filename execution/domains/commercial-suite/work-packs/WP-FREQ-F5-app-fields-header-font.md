# WORK PACKAGE — WP-FREQ-F5 · Editör alanları app field'ına + kart başlık fontu app'e (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`86c73240` üstü). Kullanıcı (F4 app-card sonrası): (1) Açıklama/Notlar (ve Kimlik kartındaki alanlar) `/Tasks/Create` field'ları gibi olsun (`form-label fw-medium` + `diten-field` ikon + `form-control`); (2) kart başlık fontları task-create'teki gibi olsun. **Frontend only** (_Editor.cshtml + visit-frequency-create.css). Yapı/mantık/id/payload KORUNUR.

## Kök neden
- `visit-frequency-create.css:34` → `.vfp-create-scope { font-family:'Inter' }` blanket kural app kart chrome'una (h6.text-heading) da sızıyor → başlık fontu task-create'ten farklı. **Fix:** Segment font-split deseni — Inter yalnız mockup-içerik (`[class^="vfp-"]` içerik / target chip / band / lifecycle vb.), **card chrome (`.card`, `.card-body`, `h6.text-heading`) app fontunu (var(--bs-body-font-family)/Public Sans) miras alsın.**
- Kimlik kartı alanları `vfp-label/vfp-input/vfp-textarea` (özel) → app field deseni değil.

## Referans app field deseni (/Tasks/Create)
```html
<label class="form-label fw-medium" for="X">Etiket <span class="text-danger">*</span></label>
<div class="diten-field">
  <i class="bx bx-<icon> diten-field-icon" aria-hidden="true"></i>            <!-- textarea: diten-field-icon--top -->
  <input class="form-control" id="X" ...>   <!-- veya <textarea class="form-control"> -->
</div>
```
(`diten-field`/`diten-field-icon` app-genel CSS — teyit et global yükleniyor; /Tasks/Create'te kullanılıyor.)

## Kapsam
1. **Kart başlık fontu (editor-geneli):** visit-frequency-create.css — `.vfp-create-scope` Inter kuralını daralt: card chrome/h6.text-heading app fontunu miras alsın; Inter yalnız mockup-içerik sınıflarında. Böylece 8 kart başlığı (`h6.text-uppercase.text-heading.fw-semibold`) task-create ile aynı font.
2. **Kimlik kartı alanları → app field:** _Editor.cshtml Kimlik bölümü (Politika kodu, Politika adı, Açıklama, Notlar):
   - `vfp-label` → `form-label fw-medium` (zorunlu alanlarda `<span class="text-danger">*</span>`).
   - `vfp-input`/`vfp-textarea` → `diten-field` sarmalayıcı + `bx diten-field-icon` (textarea `--top`) + `form-control`.
   - İkon önerisi: Politika kodu=bx-hash, Politika adı=bx-text, Açıklama=bx-align-left(--top), Notlar=bx-note(--top).
   - **Element id'leri (vfpPolicyCode/vfpPolicyName/vfpDescription/vfpNotes) + vfp-error/vfp-help div'leri KORUNUR** (form.js dokunmaz). `vfpTargetLockNote` help korunur.
3. Diğer bölümlerin alanları (Hedef/Context select'leri, Frekans, Geçerlilik, Source) bu WP'de DEĞİŞMEZ — kullanıcıya ayrıca sorulacak (select2/vocab riski). Sadece Kimlik kartı + başlık fontu.

## KORU / YAPMA
- form.js/validation/buildPayload/id'ler DEĞİŞMEZ. Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment(segment-create.css)/başka modül DOKUNMA. Mockup-içerik (target chip/band/lifecycle/özet/checklist) DEĞİŞMEZ. Yalnız _Editor.cshtml (Kimlik alanları) + visit-frequency-create.css (font-split + gerekirse vfp-input/textarea artık Kimlik'te kullanılmıyorsa stil kalabilir/başka bölümlerde kullanılıyor). Tema-duyarlı. diten-field CSS global değilse DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Editor.cshtml + visit-frequency-create.css. Backend/form.js/liste/detay/Segment diff YOK.
- **E4:** /Create Kimlik kartı: Açıklama/Notlar (+kod/ad) app `diten-field`+ikon+`form-control` görünümünde (task-create gibi); kart başlıkları app fontunda; işlev+payload aynı; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F5 · Editör Kimlik alanları app field'ına + kart başlık fontu app'e (MOD-0165-FU03, frontend, görsel)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 86c73240 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F5-app-fields-header-font.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (Kimlik bölümü) · wwwroot/assets/css/visit-frequency-create.css (satır 34 .vfp-create-scope font-family:Inter) · frontend/Diten.Web/Views/Tasks/Create.cshtml (diten-field + form-control + form-label fw-medium deseni) · (diten-field CSS'in global yüklendiğini teyit — app genel stil).

NE (frontend; id/mantık/payload KORUNUR):
 1) Kart başlık fontu: visit-frequency-create.css .vfp-create-scope Inter kuralını daralt — card chrome (.card/.card-body/h6.text-heading) app fontunu (var(--bs-body-font-family)) miras alsın; Inter yalnız mockup-içerik (target chip/band/lifecycle/reads-as/özet/checklist ve .vfp- içerik). 8 kart başlığı task-create ile aynı font.
 2) Kimlik kartı alanları (Politika kodu/adı, Açıklama, Notlar) → app field: label form-label fw-medium (+ text-danger * zorunlu); vfp-input/vfp-textarea → <div class="diten-field"><i class="bx <icon> diten-field-icon [--top textarea]"></i><control class="form-control"></div>. İkon: kod=bx-hash, ad=bx-text, Açıklama=bx-align-left --top, Notlar=bx-note --top. Element id'leri (vfpPolicyCode/vfpPolicyName/vfpDescription/vfpNotes) + vfp-error/vfp-help/vfpTargetLockNote KORUNUR.
 3) Diğer bölüm alanları (Hedef/Context/Frekans/Geçerlilik/Source) DEĞİŞMEZ (bu WP dışı).
KORU/YAPMA: form.js/validation/buildPayload/id DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/başka modül DOKUNMA; mockup-içerik değişmez; yalnız _Editor.cshtml(Kimlik) + visit-frequency-create.css; tema-duyarlı; diten-field CSS global değilse DUR+raporla.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor+css; backend/form.js/liste/detay/Segment diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: diten-field CSS global değilse; id/işlev korunamıyorsa; kapsam Kimlik dışına (diğer bölümler) taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 7444e6e7 · Agent: PASS · CT: ACCEPTED E2 (gerçek build, izole) · /c/tmp/ct-freqf5-verify @7444e6e7
```
- ✅ **Kapsam:** yalnız `_Editor.cshtml` + `visit-frequency-create.css` (2 dosya). form.js/backend/liste/detay/çözümleme/resolve.js/index.js/Segment = 0.
- ✅ **Kimlik alanları → app field:** kod(bx-hash)/ad(bx-text)/Açıklama(bx-align-left --top)/Notlar(bx-note --top) → `form-label fw-medium` + `.diten-field` + `.form-control`. id'ler (vfpPolicyCode/Name/Description/Notes) + vfp-error + `#vfpTargetLockNote` KORUNDU (form.js dokunulmadı). diten-field CSS global (backbone-custom.css, _LayoutTenantShell).
- ✅ **Font-split (kök neden):** blanket `[class^="vfp-"]` layout container'larını da eşliyordu → card chrome Inter miras alıyordu. Fix: `.vfp-create-scope .card, .card-body { font-family: var(--bs-body-font-family) }` → 8 kart başlığı + form-label/control app fontu (Public Sans, /Tasks/Create ile aynı); Inter yalnız `.vfp-*` mockup-içerik.
- ✅ **Build+test (CT izole, Release, TEMIZ — fleet-lock workaround YOK):** Diten.Web.Tests **137/0**.
- ⏳ **E4:** /Create Kimlik kartı app field görünümü + kart başlıkları app fontu.

**Not:** diğer bölüm alanları (Hedef/Context/Frekans/Geçerlilik/Source) bu WP'de app-field'e dönüşMEDİ — kullanıcıya "hepsi olsun mu?" sorusu açık.

**FREQ-F5 KOMPLE.**
