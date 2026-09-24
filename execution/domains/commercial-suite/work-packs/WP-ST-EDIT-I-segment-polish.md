# WORK PACKAGE — WP-ST-EDIT-I · Segment satır beyaz bg + Task Center font + rounded-pill badge + boş-hal font (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (WP-ST-EDIT-H §37 sonrası HEAD üstü — form.js/css SIRALI, EDIT-H aynı dosyalar). **E4 rötuşu (EDIT-G segment canlı):** (1) satır/picker backgroundları gri → **beyaz** (var(--bs-card-bg)); (2) fontlar çok büyük + aile Task Center'a uymuyor → **Task Center create font-size/family**; (3) tip badge (kişi/hekim) **rounded-pill**; (4) boş-hal ("Henüz segment yok…") fontu **küçült**. **Frontend** (strategy-create.css + form.js badge class). Backend/resx DEĞİŞMEZ.

## Kanıt
- `strategy-create.css`: `.st-seg-row`/`.st-segment-choice`/`.st-segment-empty`/`.st-segment-add`/`.st-seg-role` `background: var(--bs-body-bg)` — bu temada body-bg **gri sayfa zemini**; kart (var(--bs-card-bg)=beyaz) üzerinde gri görünür. `.st-seg-name` explicit font-size YOK (büyük inherit); `.st-segment-empty` font-size YOK (büyük).
- Tip badge form.js `segTypeBadgeClass` → `bg-label-primary`/`bg-label-warning` (rounded-pill YOK).

## NE (frontend; backend/resx DEĞİŞMEZ)
1. **Beyaz background:** satır ve picker seçim yüzeyleri `var(--bs-body-bg)` → **`var(--bs-card-bg)`** (beyaz, tema-duyarlı): `.st-seg-row`, `.st-segment-choice`, `.st-segment-add`, `.st-seg-role` base. (Hover `.st-segment-choice:hover` var(--bs-secondary-bg) kalabilir; `.st-seg-role.is-active` accent-soft kalır.) `.st-segment-empty` dashed kutu: gri sayfa zemini yerine **var(--bs-card-bg)** (kullanıcı beyaz istiyor — EDIT-G'de bg-body idi, şimdi beyaz).
2. **Task Center font-size/family:** segment bölümü tipografisi `/Tasks/Create` satır/kart tipografisiyle hizala — `.st-editor-scope` veya segment blokları `font-family: var(--bs-body-font-family)`; `.st-seg-name` boyut ~`0.875rem`/`0.9375rem` normal-medium (büyük değil); `.st-seg-meta`/`.st-segment-choice-code` küçük mono; `.st-seg-role` ~`0.8125rem`. Task Center form satır boyutlarını referans al.
3. **Badge rounded-pill:** form.js tip badge'ine `rounded-pill` ekle (mockup yuvarlakımsı). `bg-label-primary`/`warning` korunur.
4. **Boş-hal font küçült:** `.st-segment-empty` font-size ~`0.875rem` (13px), Task Center muted metin gibi.

## KORU / YAPMA
- Backend/resx DEĞİŞMEZ. Segment mantığı (renderSegments state/rol toggle/SegmentBindingsJson/homojen/SubjectType türetme/picker filtre) DEĞİŞMEZ — yalnız badge class + css (bg/font). KAPSAM (EDIT-H)/Frekans/Ürün/İçerik/sağ panel DOKUNMA. Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA. Tema-duyarlı (card-bg/body-font token'ları; dark mode korunur). Resolved KAPSAM kutusu (EDIT-E bg-body) DOKUNMA — o ayrı, kullanıcı orada bg-body istemişti.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: strategy-create.css + form.js (badge). Backend/resx/_Form/Controller/Liste/Detay diff YOK (yalnız css + form.js badge; _Form gerekmezse dokunma).
- **E4:** segment satırları/picker beyaz; fontlar Task Center boyut/aile; tip badge yuvarlak (rounded-pill); boş-hal metni küçük. **css → Ctrl+F5 (form.js badge de wwwroot → Ctrl+F5; Razor değişmezse restart gerekmez).**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (WP-ST-EDIT-H §37 sonrası)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-I · Segment satır beyaz + Task Center font + rounded-pill badge + boş-hal font (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: <WP-ST-EDIT-H §37 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-I-segment-polish.md · frontend/Diten.Web/wwwroot/assets/css/strategy-create.css (.st-seg-*/.st-segment-* ~353-470) · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/form.js (segTypeBadgeClass + tip badge render, renderSegments) · (font referans) frontend/Diten.Web/Views/Tasks/Create.cshtml + Task Center kart/satır tipografisi.

NE (frontend; backend/resx DEĞİŞMEZ):
 1) .st-seg-row / .st-segment-choice / .st-segment-add / .st-seg-role base background var(--bs-body-bg) → var(--bs-card-bg) (beyaz, tema-duyarlı). .st-segment-empty background → var(--bs-card-bg). Hover (.st-segment-choice:hover secondary-bg) + .st-seg-role.is-active (accent-soft) korunur.
 2) Segment bölümü font-family var(--bs-body-font-family); .st-seg-name ~0.9375rem normal-medium; .st-seg-meta/.st-segment-choice-code küçük mono; .st-seg-role ~0.8125rem; boyutlar Task Center /Tasks/Create satırlarıyla hizalı (büyük değil).
 3) form.js tip badge'ine "rounded-pill" ekle (bg-label-primary/warning korunur).
 4) .st-segment-empty font-size ~0.875rem (küçük, muted).
KORU/YAPMA: backend/resx DEĞİŞMEZ; renderSegments state/rol toggle/SegmentBindingsJson/homojen/SubjectType/picker filtre DEĞİŞMEZ (yalnız badge class + css); KAPSAM(EDIT-H)/Frekans/Ürün/İçerik/sağ panel DOKUNMA; Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA; resolved KAPSAM kutusu (EDIT-E bg-body) DOKUNMA; tema-duyarlı token.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff strategy-create.css + form.js; backend/resx/Controller/Liste/Detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-I — segment satır beyaz + Task Center font + rounded-pill badge + boş-hal font (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: css/badge dışına taşarsa; renderSegments mantığını bozarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 943f788b · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steiti-verify @943f788b
```
- ✅ **Kapsam (2 dosya, +18/−7):** strategy-create.css + form.js. **backend/resx/_Form/Controller/liste/detay TEMİZ** ✓.
- ✅ Satır/picker/add/empty/role base `var(--bs-body-bg)`→**`var(--bs-card-bg)`** (beyaz); `font-family: var(--bs-body-font-family)`; `.st-seg-name` 0.9375rem, `.st-seg-role` 0.8125rem, `.st-segment-empty` 0.875rem; tip badge `+rounded-pill` (bg-label-primary/warning korundu). Hover/is-active korundu.
- ✅ **KORU=0:** renderSegments state/rol toggle/SegmentBindingsJson/homojen/SubjectType/picker filtre değişmedi; KAPSAM(EDIT-H)/resolved(EDIT-E bg-body)/Frekans/Ürün/İçerik/sağ panel dokunulmadı; tema-duyarlı.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ℹ️ `.st-segment-picker-note` bilinçle bg-body bırakıldı (WP listesinde yoktu).
- ⏳ E4: segment satırları beyaz + Task Center font + rounded-pill badge + küçük boş-hal. **css/wwwroot JS → Ctrl+F5 (Razor değişmedi).**

**WP-ST-EDIT-I KOMPLE. Düzenle sayfası (Kimlik/KAPSAM/Segment + sağ panel) mockup'a hizalı.**
```
