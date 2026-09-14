# WORK PACKAGE — WP-SCMM-09-UI-refine · Concept Nodes datatable + Connections formu UX düzeltmeleri

> **Control Tower kaydı (SoR).** SCMM R1, kavram konsolu UX rötuşu (kullanıcı manuel-test geri bildirimi). Module: **MOD-0162 / CAND-CAP-0011**. Branch: `feature/scmm-content-studio` (HEAD `c09a0814`). **Yalnız frontend** (Diten.Web, KnowledgeConcepts konsolu); backend/API/RBAC'a **DOKUNMA**. Kaynak: manuel test notları 1-4.

## Ölçülmüş girdi (CT)
- **Nodes datatable:** `wwwroot/assets/js/CRM/KnowledgeConcepts/index.js` — `typeMap` (id→name, `fetchList('concept-types', ... 'conceptTypeName', typeMap)`, satır ~128); Concept Type kolonu target 3 düz metin (`const name = typeMap[v]`, ~265). **concept-types DTO'da `color` alanı VAR** (ConceptType.Color).
- **Connections formu:** `wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js` — `relPriority` number input (~72, offcanvas); `relRelationshipName` text input (~839/884); `relFromNodeId`/`relToNodeId` select2 + `labelNode(id)` helper (node metnini verir); `relDirection`/`relRelationshipType` select2. Offcanvas: `Views/CRM/KnowledgeConcepts/_RelationshipCreateEditOffcanvas.cshtml`.
- **Vocab (sabit, contract):** RelType = `leads-to/requires/addresses/evidences/belongs-to` (+custom); Direction = `outbound`(default)/`bidirectional` (`ConceptGraphVocabulary.cs`).
- **Backend:** `ConceptRelationship.Priority` = `int` (değişmez). Mevcut sıralama "küçük=önce".

## Kapsam (yalnız frontend)
1. **[Not 1] Nodes datatable — Concept Type renkli badge:** `fetchList` concept-types çekerken **color'ı da** yakala (typeMap id→{name,color}); Concept Type kolonunu (target 3) tipin **renginde badge** olarak render et (ConceptType tab'ındaki `colorSwatch`/badge deseniyle tutarlı). Renk yoksa nötr badge fallback.
2. **[Not 2] Connections Priority → select2:** `relPriority` number input yerine **single select2** `Low/Medium/High`. Değer eşleme (backend `int` değişmez): **High=10, Medium=20, Low=30** (mevcut "küçük=önce" sıralaması korunur → High üstte). Kaydederken seçilen etiketi sayıya çevir; edit'te sayıyı en yakın kovaya map edip göster (10→High, 20→Medium, 30→Low; diğer sayılar en yakın). 7-dil resx (Low/Medium/High).
3. **[Not 3] Connection Name otomatik default:** From ve To node seçilince `relRelationshipName` **otomatik** `"{labelNode(from)} → {labelNode(to)}"` olarak doldurulur (kullanıcı elle değiştirmediyse; manuel düzenlemeyi ezme — "dirty" bayrağı). Alan editable kalır.
4. **[Not 4] Direction + RelationshipType açıklamaları:** Her iki select2'ye **seçenek açıklaması** ekle (kullanıcı ne seçeceğini anlasın). Açıklamalar 7-dil resx'te (ör. `leads-to`="Bir kavram diğerine götürür/yol açar", `addresses`="Bir ihtiyacı/sorunu adresler", `requires`="Ön koşul gerektirir", `evidences`="Kanıtlar", `belongs-to`="Aittir"; `outbound`="Yönlü kenar (kaynaktan hedefe)", `bidirectional`="Çift yönlü/eşdeğer bağ"). select2 `templateResult` ile ad + küçük açıklama satırı göster.

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA. `ConceptRelationship.Priority` tipini (int) DEĞİŞTİRME (UI map). Yeni vocab uydurma (RelType/Direction sabit). Concept Type tab'ının mevcut render'ını bozma (yalnız Nodes datatable + Connections formu). 7-dil key-echo. Başka konsol/modül.

## Acceptance
- **E2:** build temiz; Nodes datatable Concept Type kolonu **renkli badge**; Connections Priority **select2 Low/Medium/High** (kaydet→10/20/30, edit round-trip doğru); Connection Name From/To seçilince **otomatik dolar** (elle düzenleme korunur); Direction/RelType select2'de **açıklama** görünür; 7-dil key-echo yok; `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** node listesinde renkli tip badge; connection oluştururken priority seç + name otomatik + açıklamalı seçim; kaydet→doğru priority sayısı DB'de.
- Kapsam: yalnız Diten.Web (index.js + concept-slim.js + _RelationshipCreateEditOffcanvas + resx). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SCMM-09-UI-refine · Prompt P-SCMM-09-UI-refine v1.0  (Concept Nodes datatable + Connections formu UX — MOD-0162/CAND-CAP-0011, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: c09a0814 · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-09-UI-refine-nodes-connections.md (bu WP)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/index.js (Nodes datatable: typeMap/fetchList/columns; Concept Type kolonu target 3)
3. frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js (Connections offcanvas: relPriority/relRelationshipName/relFromNodeId/relToNodeId/labelNode/relDirection/relRelationshipType; ConceptType tab'ındaki colorSwatch/badge deseni ~462-469)
4. frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_RelationshipCreateEditOffcanvas.cshtml + Resources/Views/CRM/KnowledgeConcepts/*.resx (7-dil)
5. services/Diten.CrmService/.../Domain/Entities/ConceptGraphVocabulary.cs (RelType/Direction sabit vocab — DOKUNMA, sadece açıklama yazacaksın)

NE (yalnız frontend Diten.Web):
 1) [Not 1] Nodes datatable Concept Type kolonu = tipin RENGİNDE badge. fetchList('concept-types',...) color'ı da yakalasın (typeMap id→{name,color}); target 3 render'ı colorSwatch/badge deseniyle renkli badge; renk yoksa nötr fallback.
 2) [Not 2] Connections Priority: number input → single select2 Low/Medium/High. Map (backend int DEĞİŞMEZ): High=10, Medium=20, Low=30. Kaydet: etiket→sayı. Edit: sayı→en yakın kova (10→High,20→Medium,30→Low). 7-dil resx.
 3) [Not 3] Connection Name auto: From+To node seçilince relRelationshipName = "{labelNode(from)} → {labelNode(to)}" otomatik dolsun (kullanıcı elle değiştirmediyse; dirty-flag ile manuel ezme yok). Alan editable kalır.
 4) [Not 4] Direction + RelationshipType select2'ye açıklama: 7-dil resx'te seçenek açıklamaları (leads-to/requires/addresses/evidences/belongs-to + outbound/bidirectional); select2 templateResult ile ad + açıklama satırı.
NASIL: ConceptType tab'ının mevcut colorSwatch/badge + select2 desenini birebir izle. Vocab sabit (ConceptGraphVocabulary) — yeni değer YOK, sadece açıklama metni. Backend Priority int kalır (UI map).
YAPMA: backend/API/RBAC/ocelot DEĞİŞTİR; Priority tipini değiştir; yeni vocab; ConceptType tab render'ını boz; 7-dil key-echo; başka konsol/modül.
DOĞRULA (E2):
 - build temiz; Nodes Concept Type kolonu renkli badge; Priority select2 Low/Medium/High (kaydet 10/20/30 + edit round-trip); Connection Name From/To'dan otomatik (manuel düzenleme korunur); Direction/RelType açıklamalı; 7-dil key-echo yok.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: concept-types DTO'da color yoksa (raporla) · labelNode/relRelationshipName beklenenden farklıysa · Priority'yi UI-map ile çözemiyorsan · kapsam frontend dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- WP-SCMM-10-UI-refine (Chain Template Golden Compact + Moderator/Audience select2 — Not 5,6).
- CT E2 + kullanıcı E4 → manuel teste devam (A7b apply-eligibility/clone + A8 evaluate).
