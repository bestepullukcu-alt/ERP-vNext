# WORK PACKAGE — WP-SCMM-10-MOD-E · ChainTemplate Branches/Steps → Tasks-checklist dikey satır editörü (frontend)

> **CT (SoR).** MOD-0162 SCMM-10. Branch `feature/scmm-content-studio` (HEAD `3b9cdc10` WP-D2 üstü). **Yalnız frontend, YENİ CSS/dep YOK** — mevcut class + repo'da MEVCUT SortableJS. Owner onayı: dikey satır (Tasks-checklist gibi) + satır-içi Details=**yalnız Min/Max**. WP-D/D2 yan-yana-kart yaklaşımının yerini alır. **Paketli; dispatch owner'da (CRM paralel).**

## Karar (owner)
Ekran mockup'ı onaylandı **(a) ile:** layout birebir (dikey satır + drag + sıra# + isim + CT-badge + cardinality chip + Details/Close + sil + satır-sonu Add step), **ama Details açılınca YALNIZ Min/Max**. **Moderator/ForWhom template-seviyesinde kalır** (Identity & Classification, WP-A/B/C). **Backend DEĞİŞMEZ** (step refs-free `{conceptTypeId,min,max}`; Phase 1 korunur). Step'e Moderator/Audience GERİ EKLENMEZ.

## Ölçülmüş girdi (CT)
- Renderer: `wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js` (Create+Edit paylaşır; mevcut = WP-D2 yan-yana kart). `types` code/name ayrı (`codeOf/nameOf` var). Backend sözleşmesi: template-level ModeratorRoleType+ForWhomAudienceProfileIds (dokunma) + step {conceptTypeId,minSelection,maxSelection}.
- **Referans desen 1 — Tasks-checklist:** `Views/Tasks/ChecklistTemplates/_Form.cshtml` + `wwwroot/assets/js/Tasks/ChecklistTemplates/form.js` (dikey satır editörü; add-row + hep bir boş satır + reindex; kolonlar + form-control-sm; card section + table-responsive; mevcut class).
- **Referans desen 2 — sıralı adım drag:** `wwwroot/assets/js/CRM/KnowledgePaths/form.js` (satır 246 grip `bx-grid-vertical` + satır 277 `window.Sortable.create(list,{handle,onEnd→renumber})`); SortableJS repo'da: `assets/vendor/libs/sortablejs/sortable.js` (KnowledgePaths/Edit.cshtml satır 38 script). **Yeni dep değil.**

## Kapsam (frontend)
1. **Branch** = kart: number badge (bi+1) + isim input + "N steps" badge + sil; üstte **+ Add branch**; boş branch "0 steps".
2. **Step satırları** (branch içi dikey liste, Tasks-checklist hissi): her satır = **drag grip** (bx-grid-vertical, SortableJS handle) + **↑↓** (erişilebilir reorder) + **sıra#** + **isim** (`nameOf`) + **CT-code badge** (`codeOf`) + **cardinality chip** (`min–max`, ör. 1–∞) + **Details/Close** toggle + **sil (×)**.
3. **Details/Close** açılınca satır-içi **YALNIZ Min/Max** editörü (2 input). Değişince cardinality chip güncellenir. **Moderator/Audience YOK.**
4. **Reorder:** SortableJS drag (KnowledgePaths deseni: handle + `Sortable.create` + onEnd→sıra# yenile + model güncelle) **+** ↑↓ oklar (aynı işi klavye/erişilebilir yapar). Yatay scroll YOK.
5. **Add-step satırı** altta: ConceptType select + Min + Max + **+ Add step** (mevcut compose modeli; Tasks add-row hissi).
6. **Branch çok olursa:** WP-D branch paging KORUNUR (dikey stacked + pager). Step paging'e gerek yok (dikey satır; uzun branch dikey akar, yatay scroll yok).
7. **Publish-freeze** read-only korunur. Create+Edit paylaşılan renderer → ikisi de otomatik. SortableJS script'ini `TemplateCreate.cshtml` + `TemplateEdit.cshtml`'e ekle (KnowledgePaths/Edit.cshtml gibi).

## YAPMA
- **Yeni CSS/inline-style/dep YOK** (SortableJS zaten var; Bootstrap + core.css class'ları). Step'e Moderator/Audience geri ekleme. Backend/DTO/API. Reference set. Faz-2 position. Content Set/eligibility. Başka modül. Yatay scroll. refs-free step modelini bozma. Template-level Moderator/ForWhom (Identity&Classification) DOKUNMA.

## Acceptance
- **E2:** `frontend/Diten.Web.Tests` baseline-diff yeşil. Dikey step satırları (grip+↑↓+#+isim+CT-badge+cardinality+Details/Close+sil); Details=**yalnız Min/Max**; SortableJS drag reorder (+↑↓) çalışır; Add-step satırı; branch number/step-count/paging; **git diff yeni CSS/style yok, 0 yeni vendor**; step payload refs-free; template-level Moderator/ForWhom değişmedi. Create+Edit aynı.
- **E4:** A2d — mockup'a benzer dikey satır görünümü; Details→Min/Max; drag/↑↓ ile sıra; scroll yok; merkezî Moderator/ForWhom yerinde.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner

```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SCMM-10-MOD-E · Prompt v1.0  (ChainTemplate Branches/Steps → Tasks-checklist dikey satır editörü — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (WP-D2 3b9cdc10 üstü)> · Worktree: ana checkout
(CRM paralel: yalnız KnowledgeConcepts template-form.js + TemplateCreate/Edit.cshtml + o sayfanın L10n'ine dokunur; çakışırsa sıralı git.)

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-10-MOD-E-frontend-checklist-rows.md (bu WP)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (MEVCUT WP-D2 yan-yana kart render — bunu dikey satıra yeniden yaz)
3. REFERANS 1 (dikey satır editörü + add-row + reindex, mevcut class): Views/Tasks/ChecklistTemplates/_Form.cshtml + wwwroot/assets/js/Tasks/ChecklistTemplates/form.js
4. REFERANS 2 (sıralı adım drag, mevcut SortableJS): wwwroot/assets/js/CRM/KnowledgePaths/form.js (grip bx-grid-vertical + window.Sortable.create + onEnd renumber) + Views/CRM/KnowledgePaths/Edit.cshtml (assets/vendor/libs/sortablejs/sortable.js script)

BACKEND SÖZLEŞMESİ (DEĞİŞTİRME): step refs-free {conceptTypeId, minSelection, maxSelection}; Moderator/ForWhom TEMPLATE-level (ModeratorRoleType + ForWhomAudienceProfileIds, Identity & Classification). Step'e Moderator/Audience GERİ EKLEME.

NE (frontend):
 1) Branch = kart: number badge (bi+1) + isim + "N steps" badge + sil; + Add branch; boş="0 steps".
 2) Step satırı (dikey liste): drag grip (bx-grid-vertical, SortableJS handle) + ↑↓ + sıra# + isim (nameOf) + CT-code badge (codeOf) + cardinality chip (min–max) + Details/Close toggle + sil (×).
 3) Details/Close açılınca satır-içi YALNIZ Min/Max (2 input); değişince cardinality chip güncellenir. Moderator/Audience YOK.
 4) Reorder: SortableJS (KnowledgePaths deseni: handle + Sortable.create + onEnd→sıra# yenile + model) + ↑↓ oklar. Yatay scroll YOK. TemplateCreate.cshtml+TemplateEdit.cshtml'e sortable.js script ekle.
 5) Add-step satırı altta: ConceptType select + Min + Max + + Add step.
 6) Branch paging (WP-D) korunur; step paging gerekmez (dikey).
 7) Publish-freeze read-only korunur.
NASIL: Tasks-checklist satır anatomisi + KnowledgePaths SortableJS birebir; SADECE mevcut class (card/badge/bg-label-*/rounded-pill/btn-icon/btn-xs/form-control-sm/form-select-sm/table-responsive/collapse) + mevcut SortableJS. Create+Edit paylaşılan renderer.
YAPMA: yeni CSS/inline-style/vendor; step'e Moderator/Audience; backend/DTO/API; reference set; Faz-2 position; template-level Moderator/ForWhom'a dokunma; yatay scroll; refs-free modeli bozma; başka modül.
DOĞRULA (E2): frontend/Diten.Web.Tests baseline-diff yeşil; dikey satır+Details(Min/Max)+drag/↑↓+branch number/step-count/paging; git diff yeni CSS/style yok + 0 yeni vendor; payload refs-free + template-level Moderator/ForWhom değişmedi. Ayrı commit. §22 TÜRKÇE. K13 — CT bağımsız doğrular.

Durma: SortableJS/collapse mevcut class'la olmuyor + yeni CSS/dep gerekiyorsa DUR+raporla; backend sözleşmesi farklıysa; kapsam KnowledgeConcepts dışına taşarsa; CRM paralel dosya çakışması → DUR + raporla.
```

## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → Diten.Web.Tests baseline-diff; template-form.js logic-read (dikey satır anatomisi, Details=Min/Max only + Moderator/Audience YOK, SortableJS+↑↓ reorder, branch number/step-count/paging, refs-free); TemplateCreate/Edit.cshtml sortable.js script; **yeni CSS/style/vendor yok** diff; template-level Moderator/ForWhom dokunulmadı.

## Kalan (bu WP dışı)
- A2d manuel test (yeni dikey-satır layout) → A3→A8 → sync/PR. · Faz-2 position lookup.
