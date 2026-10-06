# WORK PACKAGE — WP-FREQ-F6 · Kimlik kartı 2'li grid + Notlar placeholder (frontend, mini)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`29dcb59f` üstü). Kullanıcı: Kimlik kartında (1) Politika kodu + Politika adı **karta tam sığan 2'li grid** (yan yana, eşit ~%50) olsun; (2) "Dahili not — planlama motoru okumaz." yardımcı metni **Notlar textarea'sının placeholder'ı** olsun (alttaki help kaldırılsın). **Frontend only** (_Editor.cshtml, gerekirse css). id/mantık/payload KORUNUR.

## Kapsam
1. **Kimlik kartı grid:** kod + ad → app grid `row g-3` içinde `col-md-6` + `col-md-6` (karta tam sığar, eşit). Açıklama + Notlar → `col-12` (tam genişlik). (Mevcut `.vfp-grid` yerine app `row g-3`/`col-*` — /Tasks/Create deseni. Diğer bölümlere dokunma.)
2. **Notlar placeholder:** Notlar `<textarea>`ya `placeholder="Dahili not — planlama motoru okumaz."` (mevcut L10n anahtarını — Notlar help metni — placeholder olarak kullan) + **alttaki `.vfp-help` "Dahili not…" div'ini KALDIR**. `vfp-error` (vfpNotesError) korunur.
- id'ler (vfpPolicyCode/vfpPolicyName/vfpDescription/vfpNotes) + vfp-error + vfpTargetLockNote + diten-field/form-control (F5) KORUNUR. form.js dokunulmaz.

## KORU / YAPMA
- form.js/validation/buildPayload/id DEĞİŞMEZ. Yalnız Kimlik kartı grid + Notlar placeholder. Backend/liste/detay/çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA. Tema-duyarlı. Yalnız _Editor.cshtml (+ gerekirse visit-frequency-create.css minimal).

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff yalnız _Editor.cshtml (+css minimal). 
- **E4:** Kimlik kartı: kod+ad yan yana karta tam sığar (2'li), Açıklama/Notlar tam genişlik; Notlar boşken placeholder "Dahili not — planlama motoru okumaz."; alttaki help yok.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F6 · Kimlik kartı 2'li grid + Notlar placeholder (MOD-0165-FU03, frontend, mini)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 29dcb59f üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F6-identity-grid-placeholder.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (Kimlik bölümü) · frontend/Diten.Web/Views/Tasks/Create.cshtml (row g-3 / col-md-6 deseni).

NE (frontend; id/mantık/payload KORUNUR):
 1) Kimlik kartı: kod+ad → app grid row g-3 içinde col-md-6 + col-md-6 (karta tam sığar, eşit); Açıklama + Notlar → col-12. (.vfp-grid yerine app row/col; diğer bölümlere dokunma.)
 2) Notlar textarea → placeholder="Dahili not — planlama motoru okumaz." (mevcut Notlar help L10n anahtarını placeholder yap) + alttaki .vfp-help "Dahili not…" div'ini KALDIR; vfpNotesError korunur.
 id'ler (vfpPolicyCode/vfpPolicyName/vfpDescription/vfpNotes)+vfp-error+vfpTargetLockNote+diten-field/form-control KORUNUR; form.js dokunma.
KORU/YAPMA: form.js/validation/buildPayload/id DEĞİŞMEZ; backend/liste/detay/çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA; yalnız _Editor.cshtml(+css minimal); tema-duyarlı.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor(+css). Ayrı commit. §22 TÜRKÇE. K13.
Durma: id/işlev korunamıyorsa; kapsam Kimlik dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 9bfca195 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-freqf6-verify @9bfca195
```
- ✅ **Kapsam:** yalnız `_Editor.cshtml` (1 dosya, +6/−7). css/form.js/backend/liste/detay/Segment = 0.
- ✅ **Grid:** `vfp-grid`→`row g-3`; kod+ad `col-md-6`×2 (karta tam sığan eşit 2'li); Açıklama+Notlar `col-12`.
- ✅ **Placeholder:** Notlar textarea `placeholder="@Localizer["NotesHelp"]"` (7 dil mevcut); alttaki `.vfp-help` "Dahili not…" div'i kaldırıldı; vfpNotesError korundu.
- ✅ **Korundu:** id'ler + vfpTargetLockNote + F5 diten-field/form-control/form-label; form.js/validation/buildPayload dokunulmadı.
- ✅ **Build+test (CT izole, Release, temiz):** Diten.Web.Tests **137/0**.

**FREQ-F6 KOMPLE.**
