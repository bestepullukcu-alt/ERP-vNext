# WORK PACKAGE — WP-ST-EDIT-J · KAPSAM scope butonları beyaz bg + font küçült (frontend, css)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`be2ac1d4` üstü). **E4 rötuşu (EDIT-H sonrası KAPSAM):** (1) seçilmemiş scope butonları gri (body-bg) → **beyaz (card-bg)**; (2) buton yazıları çok büyük → **küçült** (Task Center). **Frontend css-only** (strategy-create.css `.st-scope-type*`). Backend/form.js/resx DEĞİŞMEZ.

## Kanıt
- `strategy-create.css` `.st-scope-type` (~303): `background: var(--bs-body-bg)` — bu temada gri sayfa zemini → kart içinde gri. `.st-scope-type-title` (~318): `font-weight:500` ama **font-size YOK** (büyük inherit). Sub `.st-scope-type-sub` 11px (küçük, tamam). `.active` = primary-bg-subtle (kalır); hover = faint primary (kalır).

## NE (frontend css-only)
1. **Beyaz bg:** `.st-scope-type` base `background: var(--bs-body-bg)` → **`var(--bs-card-bg)`** (beyaz, tema-duyarlı). `.active` (primary-bg-subtle) + hover (faint primary) DEĞİŞMEZ.
2. **Font küçült:** `.st-scope-type-title` `font-size: 0.8125rem` (ör. 13px) ekle (Task Center; büyük değil). `.st-scope-type-sub` zaten küçük — dokunma. Gerekiyorsa buton padding hafif düşür (kompakt), ama genişlik/d-flex/gap DOKUNMA.

## KORU / YAPMA
- Yalnız `strategy-create.css` `.st-scope-type*`. form.js/backend/resx/_Form DOKUNMA. `.active`/hover renkleri + gizli-input/applyScopeType/genişlik DEĞİŞMEZ. Segment/resolved/diğer bölümler DOKUNMA. Tema-duyarlı (card-bg token; dark mode korunur).

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: yalnız strategy-create.css. Başka diff YOK.
- **E4:** seçilmemiş scope butonları beyaz; yazılar küçük (Task Center); seçili primary-subtle korunur. **css → Ctrl+F5.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-J · KAPSAM scope butonları beyaz bg + font küçült (MOD-0167-FU04, frontend css)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: be2ac1d4 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-J-scope-btn-white-font.md · frontend/Diten.Web/wwwroot/assets/css/strategy-create.css (.st-scope-type ~296-318).

NE (frontend css-only):
 1) .st-scope-type base background var(--bs-body-bg) → var(--bs-card-bg) (beyaz). .active (primary-bg-subtle) + hover (faint primary) DEĞİŞMEZ.
 2) .st-scope-type-title font-size 0.8125rem ekle (küçült, Task Center). .st-scope-type-sub dokunma. Genişlik/d-flex/gap DOKUNMA.
KORU/YAPMA: yalnız strategy-create.css .st-scope-type*; form.js/backend/resx/_Form DOKUNMA; .active/hover/gizli-input/applyScopeType/genişlik DEĞİŞMEZ; segment/resolved/diğer bölümler DOKUNMA; tema-duyarlı.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff yalnız strategy-create.css. Ayrı commit ("feat(strategy): WP-ST-EDIT-J — KAPSAM scope butonları beyaz bg + font küçült (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: css dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: aa4e99c6 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitj-verify @aa4e99c6
```
- ✅ **Kapsam:** yalnız strategy-create.css (2 satır). `.st-scope-type` base `var(--bs-body-bg)`→`var(--bs-card-bg)` (beyaz); `.st-scope-type-title` `font-size: 0.8125rem`. `.active`/hover/genişlik/gizli-input/applyScopeType korundu. **Başka diff YOK.**
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: seçilmemiş scope butonları beyaz + küçük yazı. **css → Ctrl+F5.**

**WP-ST-EDIT-J KOMPLE.**
```
