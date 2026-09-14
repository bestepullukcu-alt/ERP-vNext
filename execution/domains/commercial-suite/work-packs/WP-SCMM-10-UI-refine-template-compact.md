# WORK PACKAGE — WP-SCMM-10-UI-refine · Chain Template Create/Edit → Golden Compact + Moderator/Audience select2

> **Control Tower kaydı (SoR).** SCMM R1, kavram konsolu UX rötuşu (kullanıcı manuel-test geri bildirimi). Module: **MOD-0162 / CAND-CAP-0011**. Branch: `feature/scmm-content-studio` (HEAD `c09a0814`). **Yalnız frontend** (Diten.Web, KnowledgeConcepts Chain Template yüzeyi); backend/API/RBAC'a **DOKUNMA**. Kaynak: manuel test notları 5-6. **Sıra:** WP-SCMM-09-UI-refine'dan SONRA (çakışmayı önle).

## Ölçülmüş girdi (CT)
- **Mevcut Chain Template create/edit:** `Views/CRM/KnowledgeConcepts/_TemplateCreateEditOffcanvas.cshtml` (**Slim offcanvas**) + `wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js` `renderBranches()` (~947) — branch/step builder. Step satırında **Moderator** (`js-step-roles`, ~967) ve **ForWhom** (`js-step-aud`, ~968) = **virgülle ayrılmış text input** (opak string dizileri: `roles[]`, `audiences[]`).
- **Golden Compact referansı:** `docs/records/audits/2026-07/mod-0149-frontend-compact-vertical-golden-flow-2026-07-17.md` (dikey Compact akış standardı) + çalışan örnek `Views/CRM/Claims/Create.cshtml` + `Views/CRM/KnowledgeConcepts/Create.cshtml` (node Compact full-page: `_Form.cshtml` partial, `conceptNodeForm`).
- **Audience lookup (ForWhom kaynağı, VAR):** `KnowledgeController` proxy `/api/crm/knowledge/audience-profiles` (`api/audience-profiles`). Template formu KnowledgeConcepts controller altında → aynı proxy deseniyle audience-profiles picker beslenir.
- **Moderator kaynağı:** pozisyon/rol servisi YOK (MOD-0288 bloklu) → select2 **tags** modu (serbest çok-değer).
- **Backend:** template `Branches[].Steps[]` = `{conceptTypeId, minSelection, maxSelection, allowedRoleRefs[], audienceDimensionRefs[]}` — **şekil değişmez** (roles→allowedRoleRefs, audiences→audienceDimensionRefs opak string dizileri kalır; UI sadece giriş biçimini değiştirir).

## Kapsam (yalnız frontend)
1. **[Not 5] Chain Template Create/Edit → Golden Compact full-page:** Slim offcanvas'tan **dikey Compact full-page** create/edit'e taşı (Claims/Node `Create.cshtml`+`_Form.cshtml`+Compact desen; mod-0149 golden flow). Kimlik alanları (Subject/ChainCode/ChainName/Version/Status/EffectiveFrom/To/Description) + **Branches/Steps builder** aynı sayfada. Slim listede satır → "Yeni/Düzenle" **Compact sayfaya** yönlendirir (Claims deseni). Frozen (published) note + required-tracker korunur.
2. **[Not 6] Step Moderator + ForWhom → select2 (input değil):**
   - **ForWhom/Audience** (`audienceDimensionRefs`) → **multi-select2**, kaynak `audience-profiles` (KnowledgeController proxy desenini KnowledgeConcepts'e taşı/tüket; adla ara-seç, ham Id yok).
   - **Moderator** (`allowedRoleRefs`) → **select2 tags** modu (aranabilir, serbest çok-değer; pozisyon servisi yok — MOD-0288 gelince gerçek lookup'a repoint edilir; WP'de belirt).
   - Backend dizisi (`allowedRoleRefs`/`audienceDimensionRefs`) aynı kalır; sadece giriş biçimi text→select2.
3. **7-dil L10n:** yeni Compact sayfa etiketleri + Moderator/ForWhom placeholder — gerçek çeviri, key-echo yok.

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA. Step şeklini (`allowedRoleRefs`/`audienceDimensionRefs` opak string dizileri) DEĞİŞTİRME (yalnız giriş biçimi). Moderator'a sahte/hardcoded pozisyon vocab uydurma (tags serbest). ConceptType/Connections/Nodes yüzeylerini DEĞİŞTİRME (bunlar WP-09-UI-refine). Diğer Slim tab'ları (Types/Connections) Compact'e çevirme — **yalnız Chain Template**. 7-dil key-echo. Başka modül.

## Acceptance
- **E2:** build temiz; Chain Template create/edit **Golden Compact full-page** (Claims/Node deseniyle tutarlı); branch/step builder çalışır (adım ekle/çıkar/sırala, min/max); **Moderator = select2 tags**, **ForWhom = multi-select2 (audience-profiles)** — ham Id yok; spine (≥2 tip) korunur; published-frozen note; 7-dil key-echo yok; `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** Chain Template'i Compact sayfada oluştur/düzenle; step'te Moderator (tags) + ForWhom (audience-profiles select2) seç; kaydet→backend dizileri doğru (`allowedRoleRefs`/`audienceDimensionRefs`).
- Kapsam: yalnız Diten.Web (KnowledgeConcepts Template create/edit + concept-slim.js branch builder + audience-profiles picker + resx). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SCMM-10-UI-refine · Prompt P-SCMM-10-UI-refine v1.0  (Chain Template → Golden Compact + Moderator/Audience select2 — MOD-0162/CAND-CAP-0011, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: <WP-SCMM-09-UI-refine sonrası HEAD> · Worktree: ana checkout
ÖNCE: WP-SCMM-09-UI-refine merge/commit edilmiş olmalı (aynı konsol dosyaları — çakışmayı önle).

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-10-UI-refine-template-compact.md (bu WP)
2. Golden Compact referans: docs/records/audits/2026-07/mod-0149-frontend-compact-vertical-golden-flow-2026-07-17.md + çalışan örnek frontend/Diten.Web/Views/CRM/Claims/Create.cshtml + Views/CRM/KnowledgeConcepts/Create.cshtml (+_Form.cshtml, conceptNodeForm)
3. Mevcut template: frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TemplateCreateEditOffcanvas.cshtml + wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js renderBranches() (~947; js-step-roles/js-step-aud text input ~967-968; builder model {name,steps:[{conceptTypeId,min,max,roles[],audiences[]}]})
4. Audience kaynağı: frontend/Diten.Web/Controllers/CRM/KnowledgeController.cs api/audience-profiles proxy (/api/crm/knowledge/audience-profiles) — deseni KnowledgeConcepts'e taşı/tüket
5. Resources/Views/CRM/KnowledgeConcepts/*.resx (7-dil)

NE (yalnız frontend Diten.Web):
 1) [Not 5] Chain Template create/edit → Golden Compact full-page (Slim offcanvas'tan). Claims/Node Create.cshtml+_Form.cshtml+Compact dikey desen (mod-0149). Kimlik alanları + Branches/Steps builder aynı sayfada. Slim liste satırı → Compact "Yeni/Düzenle" sayfasına yönlendir (Claims deseni). published-frozen note + required-tracker korunur.
 2) [Not 6] Step Moderator + ForWhom select2 (text input DEĞİL):
    - ForWhom/Audience (audienceDimensionRefs) → multi-select2, kaynak audience-profiles (KnowledgeController proxy desenini taşı/tüket; ham Id yok).
    - Moderator (allowedRoleRefs) → select2 TAGS modu (serbest çok-değer; pozisyon servisi yok/MOD-0288 bloklu; ileride repoint).
    - Backend dizileri (allowedRoleRefs/audienceDimensionRefs) AYNI kalır; yalnız giriş biçimi.
 3) 7-dil resx (yeni Compact etiketleri + Moderator/ForWhom placeholder) — gerçek çeviri, key-echo YOK.
NASIL: Claims/Node Compact create/edit desenini birebir izle (full-page vertical, _Form partial, required-tracker). audience-profiles proxy = KnowledgeController deseni. Step şekli değişmez (opak string dizileri) — sadece text→select2.
YAPMA: backend/API/RBAC/ocelot DEĞİŞTİR; step şeklini (allowedRoleRefs/audienceDimensionRefs) değiştir; Moderator'a hardcoded pozisyon vocab; Types/Connections/Nodes yüzeylerini değiştir (yalnız Chain Template); diğer Slim tab'ları Compact'e çevir; 7-dil key-echo; başka modül.
DOĞRULA (E2):
 - build temiz; Chain Template Golden Compact full-page; branch/step builder (ekle/çıkar/sırala, min/max, spine≥2); Moderator=select2 tags + ForWhom=multi-select2(audience-profiles), ham Id yok; published-frozen note; 7-dil key-echo yok.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: audience-profiles proxy KnowledgeConcepts'ten beslenemezse · Compact'e taşırken branch/step builder state bozulursa · step şekli backend'le uyuşmazsa · kapsam frontend dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E2 + kullanıcı E4 → manuel teste devam (A7b apply-eligibility/clone + A8 evaluate).
