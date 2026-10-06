# WORK PACKAGE — WP-SEG-A4 · Segment section kabuğunu `/Tasks/Create` app-card'ına hizala (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (SEG-A3 `34fd0736` / docs `bbfacbcb` üstü). **Yalnız frontend (Diten.Web).** Owner kararı (AskUserQuestion): *"Kabuk app, içerik mockup."* Yani **section dış çerçevesi + başlık** `/Tasks/Create` standardına döner (app'e entegre hisseder); **iç etkileşim** (subject pill, membership radio-card, blok editör, koşul/chip/source/reach, sağ ray reach/funnel/sample/checklist) mockup dilinde (oklch + Inter, `seg-*`) **KALIR**. Melez bilinçli tercih.

## Referans desen (Tasks/Create — birebir kopyala)
`frontend/Diten.Web/Views/Tasks/_Form.cshtml`:
```html
<section class="card mb-4">
  <div class="card-body p-4">
    <h6 class="text-uppercase text-heading fw-semibold mb-4 d-flex align-items-center gap-2">
      <i class="bx bx-info-circle dt-card-icon" aria-hidden="true"></i>@Localizer["SectionBasics"]
    </h6>
    ... içerik ...
  </div>
</section>
```
Açıklama satırı gereken section'larda: `<p class="card-section-desc mb-4">…</p>` (Tasks'ta mevcut).

## Kapsam (yalnız `_Form.cshtml` + `segment-create.css`; L10n gerekirse)
1. **Her section'ın dış kabuğu** `seg-card` → **`<section class="card mb-4"><div class="card-body p-4">`** (Tasks standardı: app teması beyaz/border/gölge/radius). Sol 3 section (What is this segment? / Who belongs in it? ↔ Who is on the list? / Exceptions & validity) + sağ ray 4 kart (Who this reaches now / Sample members / How this is stored / Ready to activate) — **hepsi**.
2. **Başlık** `seg-section-head`/`seg-section-no`/`seg-section-title` → **`h6.text-uppercase.text-heading.fw-semibold.mb-4.d-flex.align-items-center.gap-2` + `<i class="bx … dt-card-icon">`** (numaralı badge yerine Tasks tarzı bx ikon). İkon önerisi: (1) `bx-info-circle` · (2) `bx-filter-alt` · (3) `bx-calendar-event` · reach `bx-bar-chart-alt-2` · sample `bx-user` · how-stored `bx-data` · checklist `bx-check-circle`. Alt açıklama → `p.card-section-desc mb-4`.
3. **Font ayrımı (kritik):** `.segment-create-scope` root'undaki `font-family: 'Inter'` **global uygulamadan çıkar** — Inter **yalnız iç mockup elemanlarına** (`seg-input`/`seg-pill`/`seg-radio-*`/`seg-block`/`seg-cond`/`seg-chip`/`seg-reach*`/`seg-funnel`/`seg-sample`/`seg-checklist` vb.) uygulanır. Kart chrome (`.card`, `h6.text-heading`, `.card-section-desc`) **app temasının fontunu (Public Sans) miras alır** → "kabuk app". oklch mor accent iç elemanlarda kalır.
4. **`.segment-create-scope` sarmalayıcı KALIR** (iç `seg-*` stilleri hâlâ scope altında, global sızıntı yok). `.card`/`card-body`/`h6.text-heading`/`card-section-desc` app'in mevcut class'ları — **onlara `segment-create.css`'te dokunma** (global tema kartını override etme).

## KORUNACAK (DOKUNMA)
Section **içerikleri birebir**: subject pill toggle (create-immutable, hidden SubjectType), membership radio-card (`Model.SegmentTypes` kontrat-güdümlü), Reads-as kutusu, empty-state recipe, blok(every/any + AND ALSO), koşul(optgroup SEG-B/op/chip/source badge/free-text/readback/reach chip), JSON toggle, static textarea+reason, exceptions grid, sağ ray içerikleri (big-number/funnel/Preview50→runPreview/sample/how-stored/checklist), hidden Description/Notes/BU/MatchMode round-trip. **form.js'e DOKUNMA** (yalnız render eden section markup'ı `_Form.cshtml`'de; iç blok/koşul/reach markup'ı form.js'te kalır, değişmez). Payload (buildNodes) + catalog + canlı reach (SEG-C) + same-origin proxy + Create+Edit + publish-freeze read-only.

## YAPMA
- İç mockup elemanlarını (pill/radio/blok/chip/reach) app temasına çevir (owner "içerik mockup" dedi — oklch+Inter kalır). `segment-create.css`'te `.card`/global tema class'ını override et. `.segment-create-scope` scope'unu kaldır (iç stiller sızar). form.js davranışı/render mantığı. backend/DTO/payload. Katalog-güdümlülük. app shell (sidebar/topbar). Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). Her section `<section class="card mb-4"><div class="card-body p-4">` + `h6.text-uppercase.text-heading.fw-semibold` + bx ikon (Tasks deseni); iç etkileşim (pill/radio/blok/koşul/chip/source/reach/exceptions/sağ ray) **oklch+Inter mockup dilinde korundu**; kart chrome app fontunu (Public Sans) kullanır, iç elemanlar Inter; `.segment-create-scope` scope hâlâ izole (global sızıntı 0, `.card` override 0). Payload byte-identical + catalog + canlı reach + static→manuel + proxy KORUNDU (form.js diff YOK). Create+Edit.
- **E4:** görsel — section'lar app'in geri kalanıyla (Tasks/Create) tutarlı kabuk; içerik mockup.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-A4 · Segment section kabuğunu /Tasks/Create app-card'ına hizala (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (SEG-A3 34fd0736 / docs bbfacbcb üstü)> · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SEG-A4-frontend-app-card-shell.md (bu WP)
2. frontend/Diten.Web/Views/Tasks/_Form.cshtml (REFERANS desen: <section class="card mb-4"><div class="card-body p-4"><h6 class="text-uppercase text-heading fw-semibold mb-4 d-flex align-items-center gap-2"><i class="bx … dt-card-icon">…</h6> + <p class="card-section-desc mb-4">)
3. frontend/Diten.Web/Views/CRM/Segments/_Form.cshtml (MEVCUT: seg-card + seg-section-head/no/title) + wwwroot/assets/css/segment-create.css (scope + Inter root)

AMAÇ (owner kararı "kabuk app, içerik mockup"): section DIŞ ÇERÇEVESİ + başlık Tasks/Create standardına dönsün (app'e entegre); İÇ etkileşim (subject pill, membership radio, blok editör, koşul/chip/source/reach, sağ ray) mockup dilinde (oklch+Inter, seg-*) KALSIN.

NE (yalnız _Form.cshtml + segment-create.css; L10n gerekirse):
 1) Her section dış kabuğu seg-card → <section class="card mb-4"><div class="card-body p-4"> (sol 3 section + sağ ray 4 kart hepsi).
 2) Başlık seg-section-head/no/title → h6.text-uppercase.text-heading.fw-semibold.mb-4.d-flex.align-items-center.gap-2 + <i class="bx … dt-card-icon"> (numara badge yerine bx ikon: 1 bx-info-circle, 2 bx-filter-alt, 3 bx-calendar-event, reach bx-bar-chart-alt-2, sample bx-user, how-stored bx-data, checklist bx-check-circle); alt açıklama → <p class="card-section-desc mb-4">.
 3) Font ayrımı: segment-create.css'te .segment-create-scope root'undaki font-family:Inter'i GLOBAL uygulamadan çıkar; Inter'i YALNIZ iç seg-* elemanlarına ver (seg-input/seg-pill/seg-radio-*/seg-block/seg-cond/seg-chip/seg-reach*/seg-funnel/seg-sample/seg-checklist). Kart chrome (.card/h6.text-heading/.card-section-desc) app fontunu (Public Sans) miras alsın. oklch accent iç elemanlarda kalır.
 4) .segment-create-scope sarmalayıcı KALIR; .card/card-body/text-heading/card-section-desc app class'larına segment-create.css'te DOKUNMA (global tema override YOK).
KORU (DOKUNMA): section içerikleri birebir (pill create-immutable+hidden SubjectType, membership radio Model.SegmentTypes kontrat, Reads-as, empty-state recipe, blok every/any+AND ALSO, koşul optgroup/op/chip/source/free-text/readback/reach chip, JSON toggle, static textarea+reason, exceptions grid, sağ ray big-number/funnel/Preview50→runPreview/sample/how-stored/checklist, hidden Description/Notes/BU/MatchMode); form.js'e DOKUNMA; payload buildNodes byte-identical + catalog + canlı reach SEG-C + same-origin proxy + Create+Edit + publish-freeze.
NASIL: Tasks/_Form kabuğunu birebir kopyala; yalnız section chrome + başlık değişir. İç seg-* markup (form.js'in render ettiği + _Form'daki pill/radio/exceptions) DEĞİŞMEZ.
YAPMA: iç mockup elemanlarını app temasına çevir; .card/global tema override; scope kaldır; form.js davranış/render; backend/payload; katalog-güdümlülük; app shell; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); her section .card mb-4 + card-body p-4 + h6.text-uppercase.text-heading + bx ikon; iç etkileşim oklch+Inter korundu; kart chrome app fontu / iç eleman Inter; scope izole (global sızıntı 0, .card override 0); payload byte-identical + catalog + canlı reach + static→manuel + proxy korundu (form.js diff YOK). Ayrı commit. §22 TÜRKÇE. K13.
Durma: Tasks kabuğu iç mockup içeriğiyle çakışıyorsa; font ayrımı yapılamıyorsa; scope izolasyonu bozuluyorsa; form.js'e dokunmak gerekiyorsa; kapsam Segments dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2)**
```text
Commit: 1518c2be · Agent: PASS (137/0) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-sega4-verify @1518c2be
```
- ✅ **Scope:** yalnız 2 dosya (_Form.cshtml + segment-create.css). **form.js diff YOK** → payload/catalog/reach yapısal korundu; backend sızıntısı yok.
- ✅ **Section kabuğu app-card:** 7 section (sol 3 + sağ ray 4) `card mb-4` ×7 + `card-body p-4` ×7 + `h6.text-uppercase.text-heading.fw-semibold` ×7 + `dt-card-icon` bx ×7 (Tasks/Create deseni; ikonlar info-circle/filter-alt/calendar-event/bar-chart/user/data/check-circle).
- ✅ **Font ayrımı (kritik doğru):** `.segment-create-scope` root'unda font-family YOK (global Inter kaldırıldı); Inter yalnız `[class^="seg-"]:not(.seg-json-pre), [class*=" seg-"]:not(.seg-json-pre)` (0,3,0 özgüllük `.seg-*{inherit}`'i yener); kart chrome (.card/h6.text-heading/.card-section-desc — seg-* taşımaz) app fontunu (Public Sans) miras alır; JSON pre monospace, bx ikon boxicons korundu.
- ✅ **Scope izole:** `.card`/global tema override=0; scope-dışı top-level selektör=0. (Dead CSS seg-section-head/no/title/help/rail-kicker vb. zararsız bırakıldı — kapsam büyütmemek için.)
- ✅ **İç etkileşim korundu (dokunulmadı):** subject pill (create-immutable+hidden SubjectType), membership radio (Model.SegmentTypes), reads-as, empty-state, blok every/any+AND ALSO, koşul optgroup/op/chip/source/free-text/readback/reach chip, JSON toggle, static reason, exceptions grid, sağ ray big-number/funnel/Preview50/sample/how-stored/checklist, hidden Description/Notes/BU/MatchMode. Çift-boşluk düzeltmesi (seg-main/rail gap→0, dikey ritim mb-4'e bırakıldı).
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/137** (baseline temiz; MSB3026 uyarıları fleet DLL-kilidi, hata değil).
- ⏳ **E4:** owner görsel onayı (fleet + Segments/Create; kabuk Tasks/Create ile tutarlı, içerik mockup).

**Segment Create/Edit KOMPLE: SEG-A(blok) + A2(iskelet) + A3(birebir port) + A4(app-card kabuk) frontend + SEG-B/C backend — hepsi CT-E2.**
