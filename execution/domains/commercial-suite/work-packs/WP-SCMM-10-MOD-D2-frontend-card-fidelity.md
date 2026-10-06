# WORK PACKAGE — WP-SCMM-10-MOD-D2 · ChainTemplate step/branch kart tasarım-sadakati (frontend)

> **CT (SoR).** MOD-0162 SCMM-10. Branch `feature/scmm-content-studio` (WP-D `81f2bfdd` üstüne). **Yalnız frontend, YENİ CSS/style YOK** — mevcut class. Owner 6 tasarım eksiği listeledi (hedef = 2. resim kart anatomisi). **Paketli; dispatch owner'da (CRM paralel).**

## Ölçülmüş girdi (CT)
- Renderer: `wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js` (Create+Edit paylaşır). `types` = `{conceptTypeId, conceptTypeCode, conceptTypeName, isArchived}` (code/name AYRI → badge/başlık ayrılabilir). `labelType` şu an "CODE — Name" birleşik döndürüyor.
- Mevcut (WP-D) step kartı: başlık=birleşik label + `1–∞` cardinality badge + move/remove **altta ortada**. Branch header: name input + trash (number badge YOK, step-count YOK). Add-step: kart-altı **compose-row** (Select+Min+Max+Add). Step satırı `d-flex flex-wrap` (gap yok).

## Owner'ın 6 tasarım eksiği → düzeltme (hepsi mevcut class)
1. **Branch number badge** — header'da name'in ÖNÜNE sıra numarası: `<span class="badge bg-primary rounded-pill">${bi+1}</span>`.
2. **Step sayısı** — header'da name'in yanına: `<span class="badge bg-label-secondary">${b.steps.length} ${L.Steps}</span>` ("5 steps").
3. **Add-step = satır-sonu KART** (2. resim "ADD STEP" kartı): compose-row'u step kartlarının SONUNA, aynı satırda bir `card border shadow-none` içine al — "ADD STEP" etiketi + ConceptType select + Min/Max + `+ Append to branch` (mevcut `L.AddToSequence`). Kart-altı compose-row'u kaldır. (Paging: ADD-STEP kartı **son step sayfasında** görünür.)
3.5 **CT-code badge (badgeler yok):** kart üstünde `<span class="badge bg-label-primary">${codeOf(id)}</span>`; başlık = `nameOf(id)` (yalnız isim, birleşik değil). `codeOf/nameOf` helper'ı `types`'tan (conceptTypeCode/conceptTypeName). `1–∞` cardinality badge korunur.
4. **Order butonları top-right** — move(←/→)+remove(×) kart **başlığına, sağ üste** (2. resim): `<div class="d-flex justify-content-between align-items-start">` → sol: CT-code badge, sağ: butonlar (`btn-icon btn-xs`). Alttan kaldır.
5. **Kart araları (pendingler):** step satırına tutarlı boşluk — `d-flex flex-wrap align-items-stretch gap-2` (arrow flex item olarak aralarda).
6. **2. satıra sarınca buton yeri:** butonlar artık kart başlığında (sağ üst) olduğu için sarmada bozulmaz; ayrıca `align-items-stretch` + `gap-2` ile kart yükseklikleri/hizası tutarlı. (Sarma sorunu bu iki değişiklikle çözülür.)

## YAPMA
- **Yeni CSS/inline-style YOK** (dashed kenar için existing `border`/varsa `border-dashed` — yeni class yazma). Step'e Moderator/Audience geri ekleme (merkezi). Backend/DTO/API. Reference set. Faz-2 position. Content Set/eligibility. Başka modül. Yatay scroll. Paging'i bozma (WP-D korunur).

## Acceptance
- **E2:** `frontend/Diten.Web.Tests` baseline-diff yeşil. Branch number badge + step-count; CT-code badge + isim başlık; move/remove sağ-üst; add-step satır-sonu kart; tutarlı gap; sarmada buton bozulmaz; paging (WP-D) + refs-free + publish-freeze korunur. **git diff'te yeni CSS/style YOK.** Create+Edit aynı.
- **E4:** A2d — 2. resme benzer kart görünümü, scroll yok, paging çalışır.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner

```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SCMM-10-MOD-D2 · Prompt v1.0  (ChainTemplate step/branch kart tasarım-sadakati — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (WP-D 81f2bfdd üstü)> · Worktree: ana checkout
(CRM paralel işi: bu WP yalnız KnowledgeConcepts template-form.js + o sayfanın L10n'ine dokunur; çakışırsa sıralı git.)

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-10-MOD-D2-frontend-card-fidelity.md (bu WP — 6 madde)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (stepCard, composeRow, renderBranches, labelType, types) — MEVCUT WP-D render
3. GÖRSEL HEDEF: WP-C öncesi kart anatomisi `git show 67262f31^:...template-form.js` (badge top-left + butonlar top-right + MIN/MAX; AMA step-Moderator/Audience ALMA, yatay-scroll ALMA — paging kalır)

NE (yalnız template-form.js + _TemplateFormL10n.cshtml + 7 resx):
 1) Branch header: name ÖNÜNE number badge (badge bg-primary rounded-pill = bi+1); name YANINA step-count (badge bg-label-secondary = "${count} ${Steps}").
 2) CT-code badge: kart üstünde badge bg-label-primary = conceptTypeCode; başlık = conceptTypeName (yalnız isim). codeOf/nameOf helper types'tan. cardinality 1–∞ badge kalır.
 3) Order butonları (move ←/→ + remove ×) kart BAŞLIĞINA SAĞ ÜST: <div class="d-flex justify-content-between align-items-start"> sol=CT-code badge, sağ=butonlar (btn-icon btn-xs). Alttan kaldır.
 4) Add-step = satır-sonu KART (card border shadow-none, son step sayfasında step kartlarından sonra): "ADD STEP" etiketi + ConceptType select + Min/Max + "+ Append to branch" (L.AddToSequence). Kart-altı compose-row'u kaldır.
 5) Step satırı: d-flex flex-wrap align-items-stretch gap-2 (arrow aralarda); sarmada buton başlıkta olduğu için bozulmaz.
NASIL: SADECE mevcut class (badge/bg-primary/bg-label-*/rounded-pill/card border shadow-none/btn-icon/btn-xs/d-flex/justify-content-between/gap-2/form-select-sm/form-control-sm). Paging (WP-D) + refs-free {conceptTypeId,min,max} + publish-freeze KORUNUR. Create+Edit paylaşılan renderer.
YAPMA: yeni CSS/inline-style (dashed için existing border; yeni class yazma); step'e Moderator/Audience geri; backend/DTO/API; reference set; Faz-2 position; başka modül; yatay scroll; paging'i bozma.
DOĞRULA (E2): frontend/Diten.Web.Tests baseline-diff yeşil; 6 madde uygulandı; git diff eklenen style= ≤ silinen style= (yeni inline style yok), 0 yeni .css; resx yeni key (Steps) 7 dil dupe=0. Ayrı commit. §22 TÜRKÇE. K13 — CT bağımsız doğrular.

Durma: paging/dashed mevcut class'la olmuyor + yeni CSS gerekiyorsa DUR+raporla (yazma); backend sözleşmesi farklıysa; kapsam KnowledgeConcepts dışına taşarsa; CRM paralel dosya çakışması → DUR + raporla.
```

## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → Diten.Web.Tests baseline-diff; logic-read (6 madde: number/step-count badge, CT-code badge+isim, butonlar sağ-üst, add-step satır-sonu kart, gap, paging korundu); **yeni CSS/style yok** diff; resx dupe=0 + Steps key.

## Kalan (bu WP dışı)
- A2d manuel test → A3→A8 → sync/PR. · Faz-2 position lookup.
