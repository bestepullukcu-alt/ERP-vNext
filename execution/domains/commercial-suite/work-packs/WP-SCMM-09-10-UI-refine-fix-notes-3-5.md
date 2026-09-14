# WORK PACKAGE — WP-SCMM-09-10-UI-refine-fix · Not 3 (auto-name concept-type) + Not 5 (required-tracker konumu)

> **Control Tower kaydı (SoR).** Kullanıcı manuel-test 2. tur geri bildirimi: 6 nottan 4'ü tam (1,2,4,6 ✓), ikisi düzeltme istiyor. Module: **MOD-0162 / CAND-CAP-0011**. Branch: `feature/scmm-content-studio` (HEAD `a6e1db76`). **Yalnız frontend** (Diten.Web/KnowledgeConcepts); backend'e DOKUNMA.

## Ölçülmüş girdi (CT)
- **[Not 3]** `concept-slim.js`: `autoFillRelationshipName` (~101-102) `${labelNode(from)} → ${labelNode(to)}` üretiyor → node etiketi ("CN-2026-… — Ad"). `nodeMap[id]` (~121) `{label, subjectId, isArchived}` tutuyor — **conceptTypeId YOK**. `typeMap[id]` (~631) = `"conceptTypeCode — conceptTypeName"`. Kullanıcı istediği: node'ların **concept-type ADI** ("Hasta Profili → Fayda").
- **[Not 5]** `TemplateCreate.cshtml`/`TemplateEdit.cshtml`: `conceptTemplateRequiredTracker` span'i **başlık `<h5>` yanında (sol)** (~13). **Kanonik golden-compact yerleşimi** (`Views/Organization/Positions/Form.cshtml` ~28-32): tracker host **sağ aksiyon grubunda** (`d-flex align-items-center gap-2`), **Cancel ve Save butonlarıyla birlikte, Save'den hemen önce** — başlığın yanında değil.

## Kapsam (yalnız frontend)
1. **[Not 3] auto-name = concept-type adı:** `autoFillRelationshipName` node'un **concept-type display adını** kullansın: `"{typeName(fromNode)} → {typeName(toNode)}"` (ör. "Hasta Profili → Fayda"). Gereken: `nodeMap` build'ine `conceptTypeId` ekle (~121); node→conceptTypeId→**tip adı** çöz. Tip adı = `conceptTypeName` (kod öneki YOK — typeMap "code — name" tutuyorsa ya name-only bir çözüm ekle ya kodu ayıkla). `relNameDirty` guard aynen korunur (elle düzenleme/mevcut ad ezilmez). Tip çözülemezse node etiketine düşme yerine boş bırak (silent yanlış-ad yok).
2. **[Not 5] required-tracker konumu:** `conceptTemplateRequiredTracker` span'ini başlık `<h5>` yanından **sağ aksiyon grubuna** taşı (Organization/Positions/Form deseni: Cancel + tracker-host + Save aynı `d-flex gap-2` grubunda, tracker Save'den hemen önce). `data-required-tracker-*` attribute'ları ve davranışı korunur — yalnız DOM konumu/görünümü kanonikle hizalanır. Hem Create hem Edit sayfası.

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA. Tracker davranışını (data-required-tracker mekaniği) değiştirme — yalnız konum. Node/Type aggregate şekli, başka not (1/2/4/6 tamam) veya başka konsol/yüzey DEĞİŞTİRME. relNameDirty guard'ı bozma. 7-dil key-echo.

## Acceptance
- **E2:** build temiz; **Not 3** → Connection From/To seçilince Connection Name = "{type-adı} → {type-adı}" (node kodu/adı DEĞİL); elle düzenleme + mevcut ad korunur; **Not 5** → Chain Template Create+Edit'te Required tracker sağ aksiyon grubunda (Save'den önce), Organization/Positions/Form ile aynı yerleşim; `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** connection kur → name otomatik tip-adlı; template create/edit → tracker doğru konumda.
- Kapsam: yalnız Diten.Web (concept-slim.js + TemplateCreate/TemplateEdit.cshtml). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SCMM-09-10-UI-refine-fix · Prompt v1.0  (Not 3 auto-name concept-type + Not 5 required-tracker konumu — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: a6e1db76 · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-09-10-UI-refine-fix-notes-3-5.md (bu WP)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js (autoFillRelationshipName ~101, nodeMap build ~121, typeMap ~631, labelType ~80)
3. frontend/Diten.Web/Views/CRM/KnowledgeConcepts/TemplateCreate.cshtml + TemplateEdit.cshtml (conceptTemplateRequiredTracker span ~13)
4. KANONİK yerleşim: frontend/Diten.Web/Views/Organization/Positions/Form.cshtml (~28-32: sağ aksiyon grubunda Cancel + tracker-host + Save)

NE (yalnız frontend Diten.Web):
 1) [Not 3] autoFillRelationshipName: node etiketi yerine node'un CONCEPT-TYPE ADINI kullan → "{typeName(fromNode)} → {typeName(toNode)}" (ör. "Hasta Profili → Fayda"). nodeMap build'ine conceptTypeId ekle; node→conceptTypeId→conceptTypeName çöz (kod öneki YOK; typeMap "code — name" ise name-only çöz/ayıkla). relNameDirty guard aynen (elle/mevcut ad ezilmez). Tip çözülemezse boş bırak (yanlış-ad yok).
 2) [Not 5] conceptTemplateRequiredTracker span'ini başlık <h5> yanından SAĞ aksiyon grubuna taşı (Organization/Positions/Form deseni: Cancel + tracker-host + Save aynı d-flex gap-2, tracker Save'den hemen önce). data-required-tracker-* mekaniği/attribute'ları korunur — yalnız DOM konumu. Create + Edit ikisi de.
NASIL: Organization/Positions/Form.cshtml tracker yerleşimini birebir örnek al. Node→type çözümü için concept-types verisini (SPECS/typeMap) kullan.
YAPMA: backend/API/RBAC/ocelot DEĞİŞTİR; tracker mekaniğini değiştir (yalnız konum); node/type şekli; başka not (1/2/4/6) veya başka yüzey; relNameDirty guard'ı boz; 7-dil key-echo.
DOĞRULA (E2):
 - build temiz; Connection Name From/To seçilince tip-adlı ("{type} → {type}", node kodu değil), elle/mevcut ad korunur; Template Create+Edit tracker sağ aksiyon grubunda (Save'den önce, Organization/Positions/Form ile aynı); 7-dil key-echo yok.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: node→conceptTypeName çözülemiyorsa (raporla) · tracker taşınınca davranış bozuluyorsa · kapsam bu iki not dışına taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-14) → **ACCEPTED (E2)** · E4 = kullanıcı manuel
```text
Commit: fb8f9b10 (tek) · Agent: PASS · CT: ACCEPTED E2 · izole worktree @fb8f9b10
```
- ✅ **Scope (name-set):** 3 dosya — concept-slim.js + TemplateCreate.cshtml + TemplateEdit.cshtml. Backend/API/RBAC/ocelot/resx/manifest **dokunulmadı** (→ nav guard yapısal olarak etkilenmez).
- ✅ **CT kendi koşumu (izole worktree, Release):** Diten.Web build **0 hata**; **Diten.Web.Tests 137/0**.
- ✅ **Mantık (CT commit blob'undan okudu):** (1) [Not 3] `typeNameMap` (name-only) + `nodeTypeName(id)`=typeNameMap[nodeMap[id].conceptTypeId]; nodeMap build'i conceptTypeId taşıyor; `autoFillRelationshipName` = `{fromType} → {toType}` (concept-type ADI, node kodu değil); tip çözülemezse **boş bırakır** (yarım/yanlış ad yok); `relNameDirty` guard korundu (elle/mevcut ad ezilmez); (2) [Not 5] `conceptTemplateRequiredTracker` span'i `<h5>` yanından **sağ aksiyon grubuna** taşındı — `<div class="d-flex align-items-center gap-2">` içinde **Cancel + tracker + Save**, tracker Save'den hemen önce (Organization/Positions/Form kanonik golden-compact); `<h5>` sade `mb-0`; `data-required-tracker-*` mekaniği değişmedi. Create + Edit ikisinde de.
- ⏳ **E4 = kullanıcı manuel:** connection kur→ad tip-adlı; template create/edit→tracker Save'den önce doğru konumda (ALMIBA yeniden-testinde).

## Kalan (bu WP dışı)
- **6 not + 2 düzeltme TAMAM (CT E2).** Sıradaki: DB temizle (ALFORITA test verisi) → **ALMIBA gerçek ürünüyle yeniden manuel test** (A1→A8) → sonra main sync/push/PR. Blocked/foundation en sonda.
