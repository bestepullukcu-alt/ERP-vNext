# WORK PACKAGE — WP-SCMM-10-MOD-G · ChainTemplate step satırları → paylaşılan `.diten-checkitem` bileşeni (frontend)

> **CT (SoR).** MOD-0162 SCMM-10. Branch `feature/scmm-content-studio` (WP-F `6691791f` üstü). **Yalnız frontend, YENİ CSS YOK** — repo'da MEVCUT `.diten-checkitem*` CSS'i reuse. Owner: "bu cardlar Task checklist tasarımında olmamış; boyut, drag-drop butonları, hover — **aynı checklist'teki gibi olsun**." Kök neden: D/E/F'de satırları generic `card`'la kurduk; checklist görünümü paylaşılan `.diten-checkitem` bileşeninde. **Paketli; dispatch owner'da (CRM paralel).**

## Kök bulgu (CT ölçümü)
- **Paylaşılan checklist-satır görünümü = `.diten-checkitem*` CSS** (`assets/css/backbone-custom.css` ~6906+): `.diten-checkitem` (1px border · 6px radius · 6px 8px padding), `-grip` (cursor:grab, `:active` grabbing, `:hover` opacity), `-move` (dikey ↑↓ kolon), `-btn` (`:hover` primary), `-text`, `-ghost` (drag ghost), `-remove`, `-withdrawn`. Bu, Tasks/WorkCenter checklist ailesinin gerçek görünümü (boyut+hover+grip).
- **VisitPlanning vp-stop-list bunu SIRALI+NUMARALI+SÜRÜKLENEBİLİR satırlar için birebir reuse ediyor** (details.js ~537-552): `<li class="diten-checkitem vp-stop">` + `-grip vp-stop-handle` + `-move` (↑↓ `-btn`) + order-# badge + `-text`; SortableJS `handle:'.vp-stop-handle', ghostClass, onEnd→renumber`. **Bizim step satırının aynısı.**
- WP-C zaten `.diten-checkitem` kullanıyordu; D/E/F'de generic `card border shadow-none`'a kaydı → görünüm kayboldu.

## Kapsam (frontend, yalnız template-form.js)
Step satırını **`.diten-checkitem` markup'ıyla yeniden kur** (VisitPlanning vp-stop deseni), E/F içeriğini KORUYARAK:
- `<li class="diten-checkitem">` (liste `<ul>` içinde): `-grip` (SortableJS handle, bx-grid-vertical) + `-move` (↑↓ `-btn` chevron) + **order-# badge** + `-text` = ConceptType **adı** (nameOf) + **CT-code badge** (bg-label-primary, codeOf) + **cardinality chip** (bg-label-secondary, min–max) + **Details toggle** (`-btn`, collapse=YALNIZ Min/Max) + **remove** (`-remove`/`-btn` danger).
- **Boyut/hover/drag** `.diten-checkitem` CSS'inden gelir (yeni CSS yazma) — owner'ın istediği "aynı checklist" görünümü.
- **SortableJS:** `window.Sortable.create(<ul>, {handle:'.diten-checkitem-grip', ghostClass:'diten-checkitem-ghost', onEnd→sıra# yenile+model})` (VisitPlanning/KnowledgePaths deseni; sortable.js zaten view'de).
- **Details** collapse: yalnız Min/Max (moderator/audience YOK).
- **Branch header** (WP-F): number badge + name input `flex-grow-1` + "N steps" badge sağda + delete — KORUNUR.
- **Add-step** satırı + **branch paging** (WP-D) + **refs-free** step + **template-level Moderator/ForWhom** — KORUNUR.
- Create+Edit paylaşılan renderer; publish-freeze read-only.

## YAPMA
- **Yeni CSS/inline-style/vendor YOK** (yalnız mevcut `.diten-checkitem*` + Bootstrap class). Generic `card border shadow-none` step-satır KULLANMA (checklist görünümünü o bozuyordu). Backend/DTO/API/submit. Step'e Moderator/Audience. Details=Min/Max modelini bozma. Reference set. Faz-2. Başka modül. `diten-checkitem.js` JS bileşenini çağırma (o task'a özel — yalnız CSS class'larını kullan, VisitPlanning gibi).

## Acceptance
- **E2:** `frontend/Diten.Web.Tests` baseline-diff yeşil. Step satırları `.diten-checkitem` (grip+move+order#+text+CT-badge+cardinality+Details+remove); boyut/hover/drag checklist ile aynı (aynı CSS); SortableJS grip'ten drag; Details=Min/Max; branch header (WP-F) + add-step + paging + refs-free + template-level korunur. **git diff: yeni CSS/style-değeri/vendor yok**; generic per-row `card` kaldırıldı. Create+Edit aynı.
- **E4:** A2d — satırlar Tasks checklist ile birebir görünüm (boyut+hover+grip+drag); Details→Min/Max.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner

```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SCMM-10-MOD-G · Prompt v1.0  (ChainTemplate step satırları → paylaşılan .diten-checkitem bileşeni — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (WP-F 6691791f üstü)> · Worktree: ana checkout
(CRM paralel: yalnız KnowledgeConcepts template-form.js'e dokunur; çakışırsa sıralı git.)

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-10-MOD-G-frontend-diten-checkitem-rows.md (bu WP)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (MEVCUT: generic card step satırı — bunu .diten-checkitem'e çevir)
3. REFERANS (birebir): frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/details.js ~537-552 (`<li class="diten-checkitem vp-stop">` + -grip + -move + order-# badge + -text) + Sortable ~528 (handle + ghostClass + onEnd renumber)
4. CSS (reuse, yeni yazma): assets/css/backbone-custom.css .diten-checkitem* (~6906+); grip/move/btn/text/ghost/remove

AMAÇ: owner "aynı Task checklist görünümü" istiyor (boyut/hover/drag). Bunu SADECE mevcut .diten-checkitem CSS'ini kullanarak sağla (D/E/F'deki generic card görünümü kaybettiriyordu).

NE (yalnız template-form.js):
 - Step satırını .diten-checkitem markup'ıyla yeniden kur (VisitPlanning vp-stop deseni): <ul> içinde <li class="diten-checkitem"> = .diten-checkitem-grip (SortableJS handle, bx-grid-vertical) + .diten-checkitem-move (↑↓ .diten-checkitem-btn chevron) + order-# badge + .diten-checkitem-text (ConceptType adı, nameOf) + CT-code badge (bg-label-primary, codeOf) + cardinality chip (bg-label-secondary, min–max) + Details toggle (.diten-checkitem-btn, collapse=YALNIZ Min/Max) + remove (.diten-checkitem-remove danger).
 - SortableJS: window.Sortable.create(<ul>, {handle:'.diten-checkitem-grip', ghostClass:'diten-checkitem-ghost', animation:150, onEnd→sıra# yenile+model}).
 - KORU: branch header (WP-F: number badge + name flex-grow-1 + "N steps" sağda + delete); add-step satırı; branch paging (WP-D); Details=Min/Max; refs-free step {conceptTypeId,min,max}; template-level Moderator/ForWhom; publish-freeze read-only; Create+Edit.
NASIL: VisitPlanning vp-stop-list birebir; SADECE mevcut .diten-checkitem* + Bootstrap class. diten-checkitem.js JS bileşenini ÇAĞIRMA (task'a özel) — yalnız CSS class'ları.
YAPMA: yeni CSS/inline-style/vendor; generic card step-satır; backend/DTO/API/submit; step'e Moderator/Audience; Details modelini bozma; başka modül.
DOĞRULA (E2): frontend/Diten.Web.Tests baseline-diff yeşil; satır .diten-checkitem (grip/move/order#/text/CT-badge/cardinality/Details/remove); SortableJS grip drag; Details=Min/Max; branch header+add-step+paging+refs-free+template-level korundu; git diff yeni CSS/style-değeri/vendor yok + generic card kaldırıldı. Ayrı commit. §22 TÜRKÇE. K13 — CT bağımsız doğrular.

Durma: .diten-checkitem mevcut class'larla checklist görünümü vermiyorsa (yeni CSS gerekiyorsa DUR+raporla); backend sözleşmesi farklıysa; kapsam KnowledgeConcepts dışına taşarsa; CRM paralel çakışma → DUR + raporla.
```

## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → Diten.Web.Tests baseline-diff; logic-read (satır `.diten-checkitem` + grip/move/text/CT-badge/cardinality/Details=Min-Max/remove; SortableJS grip; branch header/add-step/paging/refs-free/template-level korundu); **git diff yeni CSS/vendor yok + generic `card` step-satır kaldırıldı**.

## Kalan (bu WP dışı)
- A2d manuel test (checklist görünümlü satırlar) → A3→A8 → sync/PR. · Faz-2 position lookup.
