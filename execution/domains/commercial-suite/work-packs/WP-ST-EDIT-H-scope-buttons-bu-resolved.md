# WORK PACKAGE — WP-ST-EDIT-H · KAPSAM buton yazı inceltme + BU tam genişlik + ÇÖZÜMLENEN KAPSAM "ülke / BU" (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (WP-ST-EDIT-G §37 sonrası HEAD üstü — _Form/form.js/css SIRALI, EDIT-G aynı dosyalar). **E4 rötuşu (EDIT-F sonrası KAPSAM):** (1) scope-type butonları dolu `btn-outline-primary` yerine **Task Center `.choice-box` kartı gibi** (seçilmemiş: bg-body + subtle border; seçili: primary-subtle wash + primary border + primary yazı) — **genişliklere DOKUNMA** (bizimkiler iyi); (2) İş birimi seçilince 2 alan (ülke filtresi + BU) **kartı dolduran genişlik** (BU block col-md-4 → col-12); (3) İş birimi'nde **ÇÖZÜMLENEN KAPSAM "ülke / BU"** (ör. "Belarus / Beta"). **Frontend** (_Form.cshtml + form.js + strategy-create.css). Backend/resx DEĞİŞMEZ.

## Kanıt
- css: `.st-scope-type-title { font-weight:600 }` + `.st-scope-type-sub { font-size:11px; opacity:.75 }`; butonlar `btn btn-outline-primary` (default padding → büyük).
- `_Form.cshtml` BU block: `<div class="col-12 col-md-4 d-none" data-scope-block="business-unit">` → içinde col-6 (buFilterCountry) + col-6 (businessUnitId). md+ ekranda blok 1/3 genişlik → alanlar dar.
- `form.js` renderResolvedScope: `business-unit → refLabel = businessUnitEl text` → value "İş birimi — Beta". country → "Ülke — Azerbaijan" (mevcut desen `{typeLabel} — {ref}`).

## NE (frontend; backend/resx DEĞİŞMEZ)
1. **Butonlar → Task Center `.choice-box` renk/border deseni** (backbone-custom.css:4989 referans; `:has(:checked)` → primary border + faint primary wash): `btn btn-outline-primary`'yi KALDIR (dolu-fill istenmiyor). `.st-scope-type`: base `background: var(--bs-body-bg); border: 1px solid var(--bs-border-color); color: var(--bs-body-color)`; **aktif** (`.active`/`.is-active`): `border-color: var(--bs-primary); background: var(--bs-primary-bg-subtle)` (veya choice-box'ın faint primary wash'ı) + title `var(--bs-primary)`. Yazı ağırlığı title 500-600 makul, kalın-dolu görünüm kalkar. **Genişlik/d-flex/gap DOKUNMA** (bizimkiler iyi). Gizli-input/applyScopeType/aktif-toggle mantığı DEĞİŞMEZ (yalnız görsel + active class korunur).
2. **BU block tam genişlik:** `data-scope-block="business-unit"` bloğu `col-12 col-md-4` → **`col-12`** (kart genişliğini doldurur; içteki 2 select col-6 geniş kalır). (country/legal-entity blokları col-md-4 kalabilir — onlar tek alan; yalnız BU 2-alanlı olduğundan tam genişlik.)
3. **renderResolvedScope BU:** business-unit dalında `refLabel = "{buFilterCountry text} / {businessUnit text}"` (ör. "Belarus / Beta"); boşsa yalnız dolu olan. Sonuç değer mevcut desenle "İş birimi — Belarus / Beta". (country/legal-entity dalları DEĞİŞMEZ.)

## KORU / YAPMA
- Backend/resx DEĞİŞMEZ. scopeType gizli-input + applyScopeType + cascade + single-reference + buFilterCountry→BU territory-fill + submit DEĞİŞMEZ (yalnız buton stil + BU col + resolved BU-value). Segment/Frekans/Ürün/İçerik/sağ panel DOKUNMA (EDIT-G segment değişikliğiyle çakışma yok — bu WP EDIT-G SONRASI). Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA. Resolved kutu bg-body (EDIT-E) korunur. Tema/L10n köprüsü.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: _Form.cshtml + form.js + strategy-create.css. Backend/resx/Controller/ViewModel/Liste/Detay diff YOK (resx gerekmez).
- **E4:** buton yazıları ince/küçük; İş birimi'nde 2 alan kart genişliğinde; ÇÖZÜMLENEN KAPSAM "İş birimi — Belarus / Beta". **Razor+css → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (WP-ST-EDIT-G §37 sonrası)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-H · KAPSAM buton inceltme + BU tam genişlik + ÇÖZÜMLENEN KAPSAM ülke/BU (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: <WP-ST-EDIT-G §37 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-H-scope-buttons-bu-resolved.md · frontend/Diten.Web/wwwroot/assets/css/strategy-create.css (.st-scope-type-title/-sub ~301) · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (business-unit scope block ~119: col-12 col-md-4 + içte col-6+col-6) · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/form.js (renderResolvedScope; business-unit dalı + buFilterCountryEl/businessUnitEl).

NE (frontend; backend/resx DEĞİŞMEZ):
 1) Butonlar Task Center .choice-box renk/border deseni (backbone-custom.css:4989 :has(:checked) → primary border + faint primary wash): renderScopeTypeButtons'tan "btn btn-outline-primary" KALDIR (dolu-fill yok), .st-scope-type kalsın. css .st-scope-type base: background var(--bs-body-bg) + border 1px var(--bs-border-color) + color body; aktif (.active/.is-active): border-color var(--bs-primary) + background var(--bs-primary-bg-subtle) + title var(--bs-primary). Genişlik/d-flex/gap DOKUNMA. Gizli-input/applyScopeType/active-toggle mantığı DEĞİŞMEZ.
 2) _Form business-unit scope block: col-12 col-md-4 → col-12 (kart genişliği; içteki 2 col-6 geniş kalır). country/legal-entity blokları col-md-4 kalır.
 3) form.js renderResolvedScope business-unit dalı: refLabel = buFilterCountry text + " / " + businessUnit text (ör. "Belarus / Beta"); yalnız biri doluysa onu. country/legal-entity dalları DEĞİŞMEZ. Değer mevcut "{typeLabel} — {ref}" deseniyle "İş birimi — Belarus / Beta".
KORU/YAPMA: backend/resx DEĞİŞMEZ; scopeType gizli-input + applyScopeType + cascade + single-reference + buFilterCountry→BU territory-fill + submit DEĞİŞMEZ; Segment/Frekans/Ürün/İçerik/sağ panel DOKUNMA; Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA; resolved kutu bg-body korunur; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff _Form+form.js+css; backend/resx/Controller/ViewModel/Liste/Detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-H — KAPSAM buton inceltme + BU tam genişlik + ÇÖZÜMLENEN KAPSAM ülke/BU (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: BU resolved/territory-fill bozuluyorsa; kapsam dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 5704b28a · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steith-verify @5704b28a
```
- ✅ **Kapsam (3 dosya, +40/−12):** _Form.cshtml + strategy-create.css + form.js. **backend/resx/Controller/ViewModel/liste/detay TEMİZ** ✓.
- ✅ Butonlar `btn btn-outline-primary` kaldırıldı → `.st-scope-type` choice-box renkleri (base bg-body+border; `.active` border var(--bs-primary) + background var(--bs-primary-bg-subtle) + title primary; title 600→500) + non-active hover; genişlik/d-flex/gap dokunulmadı. BU block col-md-4→**col-12** (2 select geniş). renderResolvedScope BU: "{buFilterCountry} / {businessUnit}" (value-guarded, placeholder sızmaz) → "İş birimi — Belarus / Beta".
- ✅ **KORU=0:** scopeType gizli-input + applyScopeType + cascade + single-reference + buFilterCountry→BU + submit + resolved kutu bg-body korundu.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: choice-box buton renkleri + BU tam genişlik + resolved ülke/BU. **FLEET RESTART.**

**WP-ST-EDIT-H KOMPLE.**
```
