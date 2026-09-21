# WORK PACKAGE — WP-FREQ-F10 · KAPSAM özet kutusu mockup birebir + bg-body (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**F9 landing sonrası HEAD üstü** — F9 ile aynı dosyalar, SIRALI). Kullanıcı: KAPSAM özet kutusu mockup HTML'ine birebir + arka plan `bg-body`. **Frontend only** (_Editor.cshtml #vfpScopeBody KAPSAM + form.js renderScopeSummary + visit-frequency-create.css). id/clear/chip mantığı KORUNUR (#vfpScopeChips/#vfpScopeClear).

## Mockup HTML (kullanıcının verdiği — inline stiller → token'a çevrilir)
- **Dış kutu:** `display:grid; grid-template-columns: minmax(0,1fr) auto; align-items:start; gap:10px 12px; padding:12px 14px; border-radius:6px; background: var(--bs-body-bg) (kullanıcı bg-body); border:1px solid var(--bs-border-color)`.
- **Sol kolon:** flex-col gap 8; "KAPSAM" label (`font-size:11px; text-transform:uppercase; letter-spacing:.06em; font-weight:600; color:var(--bs-secondary-color)`) + çip satırı (`flex-wrap; gap:6px`).
- **Çip (her kapsam kısıtı):** `inline-flex; align-items:center; gap:6px; max-width:100%; padding:4px 9px; border-radius:5px; background:var(--bs-card-bg); border:1px solid var(--bs-border-color); font-size:12px; overflow:hidden` → içinde: **label** (muted, `color:var(--bs-secondary-color)`, nowrap) + **value** (`font-weight:600; color:var(--bs-heading-color)`, ellipsis nowrap). Boş-durum çipi: "Kapsam · tüm hedefler".
- **"Kapsamı temizle" buton:** `align-self:end; white-space:nowrap; padding:5px 10px; border-radius:4px; border:1px solid var(--bs-border-color); background:var(--bs-card-bg); font-size:12px; color:var(--bs-secondary-color)`.

## Kapsam
- `_Editor.cshtml` (#vfpScopeBody `.vfp-scope-summary`): yukarıdaki grid yapısına getir; sol kolon KAPSAM label + `#vfpScopeChips`; sağ `#vfpScopeClear` "Kapsamı temizle" (align-self end). id'ler KORUNUR.
- `form.js` `renderScopeSummary`: çip markup'ını label:value yapısına getir (muted label + bold value); boşken "tüm hedefler" fallback. Chip üretim mantığı/selectedScope KORUNUR (yalnız markup/sınıf).
- `visit-frequency-create.css`: `.vfp-scope-summary` grid (minmax(0,1fr) auto) + bg-body; çip label:value stilleri; clear buton. Tema-duyarlı (light+dark, --bs-* token).

## KORU / YAPMA
- form.js selectedScope/clear/cascade/buildPayload/id DEĞİŞMEZ (yalnız renderScopeSummary çip markup'ı). Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler + HEDEF üst-kısmı + F9 select2 DOKUNMA (yalnız KAPSAM özet). Tema-duyarlı. Yalnız _Editor(#vfpScopeBody KAPSAM) + form.js(renderScopeSummary) + css.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Editor.cshtml + form.js + visit-frequency-create.css. Backend/liste/detay/diğer bölüm diff YOK.
- **E4:** KAPSAM kutusu mockup gibi (grid, KAPSAM label + label:value çipler, sağda Kapsamı temizle), bg-body; boşken "tüm hedefler"; kapsam seçilince çipler + temizle çalışır; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (F9 landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F10 · KAPSAM özet kutusu mockup birebir + bg-body (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <F9 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F10-kapsam-summary-mockup.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (#vfpScopeBody .vfp-scope-summary) · wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (renderScopeSummary/selectedScope) · wwwroot/assets/css/visit-frequency-create.css (.vfp-scope-summary).

NE (frontend; id/clear/cascade/buildPayload KORUNUR):
 KAPSAM özet kutusu → mockup grid: display grid grid-template-columns minmax(0,1fr) auto, gap 10/12, padding 12/14, radius 6, background var(--bs-body-bg) [bg-body], border 1px --bs-border-color. Sol: "KAPSAM" label (11px uppercase ls.06em 600 --bs-secondary-color) + #vfpScopeChips flex-wrap gap6. Çip: inline-flex gap6 padding4/9 radius5 bg --bs-card-bg border 12px → muted label + bold value (--bs-heading-color); boşken "tüm hedefler". #vfpScopeClear "Kapsamı temizle" align-self end (padding5/10 radius4 border bg --bs-card-bg). form.js renderScopeSummary çip markup'ı label:value; selectedScope/clear mantığı DEĞİŞMEZ.
KORU/YAPMA: form.js selectedScope/clear/cascade/buildPayload/id DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler + HEDEF üst + F9 select2 DOKUNMA; tema-duyarlı; yalnız _Editor(#vfpScopeBody KAPSAM)+form.js(renderScopeSummary)+css.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor+form.js+css; backend/liste/detay/diğer bölüm diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: clear/chip/buildPayload bozuluyorsa; kapsam KAPSAM dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)** — F13 ile birleşik commit
```
Commit: bd1f2466 (F10+F13) · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-fb-verify @bd1f2466
```
- ✅ KAPSAM özet: grid minmax(0,1fr) auto + bg-body + "KAPSAM" label + label:value çipler (`renderScopeChips`) + "Kapsamı temizle" align-end. selectedScope/clearScope/scopeText/id DEĞİŞMEDİ. Kapsam: _Editor+form.js+css+7resx; backend/detay/liste/F9=0. Diten.Web.Tests 137/0.
```
