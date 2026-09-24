# WORK PACKAGE — WP-ST-EDIT-K · Segment isim font küçült + siyah renk + WCN hover (frontend, css)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (WP-ST-EDIT-J §37 sonrası HEAD üstü — strategy-create.css SIRALI). **E4 rötuşu (segment picker/satır canlı):** (1) fontlar hâlâ büyük (özellikle picker isimleri) → **küçült**; (2) isim rengi gri → **siyah** (heading-color); (3) hover gri → **WorkCenterNext gibi** (faint primary). **Frontend css-only** (strategy-create.css). Backend/form.js/resx DEĞİŞMEZ.

## Kanıt
- `--st-text: var(--bs-body-color)` (gri-siyah); `.st-seg-name` color `--st-text` (0.9375rem, EDIT-I) — picker `.st-segment-choice-name` **explicit font-size YOK** (büyük inherit) + color `--st-text` (choice `color: var(--st-text)`).
- Hover: `.st-segment-choice:hover { background: var(--bs-secondary-bg) }` (gri). WCN referans `.wcn-seg:hover:not(.active) { color: var(--bs-body-color); background: rgba(var(--bs-primary-rgb), .07); }` (faint primary).

## NE (frontend css-only)
1. **Font küçült:** `.st-segment-choice-name` font-size ~`0.875rem` (Task Center/WCN; büyük inherit yerine). `.st-seg-name` 0.9375rem → **0.875rem** (picker ile tutarlı). `.st-seg-role`/meta zaten küçük.
2. **Siyah renk:** `.st-seg-name` + `.st-segment-choice-name` color `var(--st-text)` → **`var(--bs-heading-color)`** (koyu/siyah, tema-duyarlı).
3. **WCN hover:** `.st-segment-choice:hover` background `var(--bs-secondary-bg)` → **`rgba(var(--bs-primary-rgb), .07)`** (WCN faint primary; metin rengi korunur). Segment satırında (`.st-seg-row`) gri hover varsa aynı WCN faint-primary'ye çevir (yoksa ekleme zorunlu değil).

## KORU / YAPMA
- Yalnız `strategy-create.css`. form.js/backend/resx/_Form DOKUNMA. Beyaz bg (card-bg, EDIT-I/J) + badge rounded-pill + rol toggle is-active + KAPSAM/resolved/diğer bölümler DEĞİŞMEZ. Tema-duyarlı (heading-color/primary-rgb token; dark mode korunur).

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: yalnız strategy-create.css. Başka diff YOK.
- **E4:** segment picker/satır isimleri küçük + siyah; hover WCN faint-primary (gri değil). **css → Ctrl+F5.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (WP-ST-EDIT-J §37 sonrası)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-K · Segment isim font küçült + siyah + WCN hover (MOD-0167-FU04, frontend css)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: <WP-ST-EDIT-J §37 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-K-segment-font-hover.md · frontend/Diten.Web/wwwroot/assets/css/strategy-create.css (.st-seg-name ~378, .st-segment-choice/.st-segment-choice-name ~435-456, --st-text ~19) · (referans) backbone-custom.css .wcn-seg:hover ~5674.

NE (frontend, YALNIZ strategy-create.css):
 1) .st-segment-choice-name font-size 0.875rem (ekle); .st-seg-name 0.9375rem → 0.875rem.
 2) .st-seg-name + .st-segment-choice-name color var(--st-text) → var(--bs-heading-color) (siyah/koyu).
 3) .st-segment-choice:hover background var(--bs-secondary-bg) → rgba(var(--bs-primary-rgb), .07) (WCN faint primary; metin korunur). .st-seg-row gri hover varsa aynı yap.
KORU/YAPMA: yalnız strategy-create.css; form.js/backend/resx/_Form DOKUNMA; beyaz bg (card-bg)/badge rounded-pill/rol is-active/KAPSAM/resolved/diğer bölümler DEĞİŞMEZ; tema-duyarlı token.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff yalnız strategy-create.css. Ayrı commit ("feat(strategy): WP-ST-EDIT-K — segment isim font küçült + siyah + WCN hover (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: css dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 38f109bb · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitk-verify @38f109bb
```
- ✅ **Kapsam:** yalnız strategy-create.css (+5/−3). `.st-seg-name`+`.st-segment-choice-name` font-size 0.875rem + color `var(--bs-heading-color)` (siyah); `.st-segment-choice:hover` → `rgba(var(--bs-primary-rgb),0.07)` (WCN faint primary). `.st-seg-row` gri hover yoktu (eklenmedi). **Başka diff YOK.**
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: segment isimleri küçük+siyah + hover WCN faint-primary. **css → Ctrl+F5.**

**WP-ST-EDIT-K KOMPLE.**
```
