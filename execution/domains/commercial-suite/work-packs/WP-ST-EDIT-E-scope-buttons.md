# WORK PACKAGE — WP-ST-EDIT-E · KAPSAM header + ScopeType buton-seçici + ÇÖZÜMLENEN KAPSAM kutusu (mockup) (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`657f7312` üstü). **E4 rötuşu (canlı KAPSAM):** (1) header "KAPSAM — NEREDE"; (2) Kapsam seviyesi dropdown → mockup 4-buton segmented seçici; (3) ÇÖZÜMLENEN KAPSAM kutusu mockup stili ama **bg-body**. **Frontend** (_Form.cshtml + form.js + strategy-create.css + resx). Backend DEĞİŞMEZ.

## Kanıt
- `_Form.cshtml` KAPSAM: `<h6>@Localizer["ScopeSection"]</h6>` (="Kapsam") + `<select asp-for="ScopeType" id="scopeType">` (dropdown, form.js fill) + scope blocks (country/legal-entity/business-unit) + `<div id="resolvedScope" class="small text-muted">`.
- `form.js`: `scopeTypeEl = el('scopeType')`; `applyScopeType`/`renderResolvedScope`/cascade `scopeTypeEl.value` okur; `renderScopeOptions` select'i doldurup default 'tenant' set eder; `renderResolvedScope` → `<span>ResolvedScope:</span> value` (inline).
- resx: `ScopeSection`="Kapsam", `ResolvedScope`="Çözümlenen kapsam". ScopeType_tenant/country/legal-entity/business-unit mevcut.

## NE (frontend; backend DEĞİŞMEZ)
1. **Header:** `ScopeSection` → "KAPSAM — NEREDE" (7 dil; "— NEREDE" karşılığı: en WHERE, fr OÙ, es DÓNDE, zh 在哪里, ar أين, ru ГДЕ — ya da sade "Kapsam — Nerede"). Numaralı bölüm rozetiyse (mockup "2") ve diğer bölümlerde varsa tutarlılık için eklenebilir (opsiyonel).
2. **ScopeType dropdown → 4-buton segmented seçici** (mockup): Tenant/Ülke/Tüzel Kişilik/İş Birimi, her buton **başlık + alt-etiket** (tüm şirket / tek pazar / legal entity / BU · territory). Aktif buton vurgulu. **Değeri korumak için:** `scopeType` gizli input (`asp-for="ScopeType"` hidden) kalır; butonlar tıklanınca gizli değeri set eder + aktif class + `applyScopeType` tetikler. form.js `scopeTypeEl` = gizli input (`.value` çalışır); `renderScopeOptions` select-fill yerine mevcut scopeTypes'a göre butonları etkinleştirir + default tenant aktif. Cascade/submit/single-reference DEĞİŞMEZ. Alt-etiketler için L10n (`ScopeTypeSub_*`).
3. **ÇÖZÜMLENEN KAPSAM kutusu** (mockup snippet, ama **bg-body**): `#resolvedScope` her zaman görünür bordered kutu:
   - kutu: `margin-top:14px; padding:9px 11px; background: var(--bs-body-bg); border:1px solid var(--bs-border-color); border-radius:6px; display:flex; align-items:center; gap:10px; flex-wrap:wrap;`
   - label: mono uppercase (`font-family: var(--bs-font-monospace)/Source Code Pro; font-size:10px; letter-spacing:.09em; text-transform:uppercase; color: var(--bs-secondary-color)`) = "ÇÖZÜMLENEN KAPSAM".
   - value: `font-size:13px; font-weight:600` — çözümlenmemişse **danger** renk (`var(--bs-danger)`) + "henüz çözümlenmedi" (yeni L10n `ResolvedScopeNone`); çözülünce heading/success renk + breadcrumb (ör. "Türkiye › GM Pharma …").
   - form.js `renderResolvedScope`: bu label+value yapısını üretir (boşken danger+"henüz çözümlenmedi", doluyken değer). Stiller css'e (`.st-resolved-scope`), inline değil.

## KORU / YAPMA
- Backend DEĞİŞMEZ (ScopeType hâlâ gizli input ile POST; cascade/create-update aynı). 2b sağ panel + diğer bölümler (Kimlik/Segment/Frekans/Ürün/İçerik) + submit DOKUNMA (yalnız KAPSAM header + scopeType buton + resolved kutu). Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA. scope cascade (country→LE→BU/territory) + single-reference + scope-options feed DEĞİŞMEZ (yalnız ScopeType girişi select→buton). Resolved kutu **bg-body** (mockup'ın rgb(250,250,251) değil). Tema/L10n köprüsü.
- **DUR:** buton→gizli-input scopeType cascade/submit'i bozuyorsa (applyScopeType tetiklenmiyor); → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: _Form.cshtml + form.js + strategy-create.css + resx. Backend/Controller/ViewModel/Liste/Detay diff YOK.
- **E4:** KAPSAM header "KAPSAM — NEREDE"; ScopeType 4-buton (aktif vurgulu, alt-etiketli); seçince cascade + resolved kutu güncellenir; ÇÖZÜMLENEN KAPSAM bordered kutu bg-body (boşken danger "henüz çözümlenmedi", doluyken breadcrumb). **Razor+css+resx → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-E · KAPSAM header + ScopeType buton-seçici + ÇÖZÜMLENEN KAPSAM kutusu (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 657f7312 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-E-scope-buttons.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (KAPSAM section ~79-125: ScopeSection h6 + select#scopeType + #resolvedScope) · wwwroot/assets/js/CRM/StrategyTemplates/form.js (scopeTypeEl ~544, applyScopeType ~600, renderResolvedScope ~586, renderScopeOptions ~609) · wwwroot/assets/css/strategy-create.css · resx (ScopeSection/ResolvedScope/ScopeType_*). Mockup: /c/tmp/mockup-strategy.html Düzenle KAPSAM.

NE (frontend; backend DEĞİŞMEZ):
 1) Header ScopeSection → "KAPSAM — NEREDE" (7 dil).
 2) ScopeType dropdown → 4-buton segmented (Tenant/Ülke/Tüzel Kişilik/İş Birimi, başlık+alt-etiket tüm şirket/tek pazar/legal entity/BU·territory). scopeType GİZLİ input (asp-for ScopeType) kalır; buton tıklama → gizli değeri set + aktif class + applyScopeType tetikle. form.js scopeTypeEl=gizli input; renderScopeOptions butonları etkinleştir (mevcut scopeTypes) + default tenant aktif; cascade/submit/single-reference DEĞİŞMEZ. Alt-etiket L10n ScopeTypeSub_*.
 3) #resolvedScope → bordered kutu (margin-top:14px; padding:9px 11px; background:var(--bs-body-bg); border:1px solid var(--bs-border-color); radius:6px; flex; gap:10px; wrap): mono uppercase label "ÇÖZÜMLENEN KAPSAM" (var(--bs-font-monospace),10px,ls.09em,muted) + value (13px/600; boşken var(--bs-danger)+"henüz çözümlenmedi"[L10n ResolvedScopeNone]; doluyken heading/success+breadcrumb). form.js renderResolvedScope bu yapıyı üretir; stiller css (.st-resolved-scope), inline değil.
KORU/YAPMA: backend DEĞİŞMEZ (ScopeType gizli input POST; cascade/create-update aynı); 2b sağ panel + Kimlik/Segment/Frekans/Ürün/İçerik + submit DOKUNMA; Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA; cascade/single-reference/scope-options feed DEĞİŞMEZ; resolved kutu bg-body (rgb değil); tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff _Form+form.js+css+resx; backend/Controller/ViewModel/Liste/Detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-E — KAPSAM header 'NEREDE' + ScopeType buton-seçici + ÇÖZÜMLENEN KAPSAM kutusu (bg-body) (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: buton→gizli-input scopeType cascade/submit'i bozuyorsa; kapsam dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 791ba472 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-stedite-verify @791ba472
```
- ✅ **Kapsam (11 dosya, +160/−16):** _Form.cshtml + _IndexL10n.cshtml + strategy-create.css + form.js + 7 resx. **backend/Controller/ViewModel/liste/detay/details.js/CrmService TEMİZ** ✓.
- ✅ **Header:** ScopeSection → "Kapsam — Nerede" (text-uppercase → "KAPSAM — NEREDE"), 7 dil.
- ✅ **ScopeType select→4-buton:** `<input type="hidden" id="scopeType" asp-for="ScopeType">` + `#scopeTypeButtons` (renderScopeTypeButtons: başlık `ScopeType_*` + alt-etiket `ScopeTypeSub_*`, default tenant aktif). Gizli input programatik set change tetiklemediği için change-listener kaldırıldı → buton container click-delege → value set + aktif/aria-pressed + `applyScopeType()`. Cascade/single-reference/scope-options/submit DEĞİŞMEDİ; ScopeType gizli input ile POST.
- ✅ **ÇÖZÜMLENEN KAPSAM kutusu:** `.st-resolved-scope` bordered, **bg-body** (var(--bs-body-bg)); mono uppercase label + değer (boş=var(--bs-danger)+"henüz çözümlenmedi"[ResolvedScopeNone], dolu=heading+breadcrumb). Stiller css'te.
- ✅ **KORU=0:** 2b sağ panel + Kimlik/Segment/Frekans/Ürün/İçerik + submit korundu.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: KAPSAM — NEREDE + 4-buton scope + resolved kutu bg-body. **Razor+css+resx → FLEET RESTART.**

**WP-ST-EDIT-E KOMPLE.**
```
