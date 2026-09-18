# WORK PACKAGE — WP-SCMM-12-UI · Claim authoring console + component picker (frontend)

> **Control Tower kaydı (SoR).** SCMM R1, gate G2. Module: **CAND-CAP-0011** (Claim). Branch: `feature/scmm-content-studio` (HEAD 861d56ea).
> **Ön koşul HAZIR:** SCMM-12-API (ClaimsController, CT-ACCEPTED E2+E4) + SCMM-12-API-GW (ocelot route, canlı doğrulandı) → claim uçları gateway'den tüketilebilir. Bu WP **yalnız frontend** (Diten.Web); backend'e DOKUNMA.

## Ölçülmüş girdi (CT, 2026-09-13)
- **Mirror console (birebir):** `frontend/Diten.Web/Controllers/CRM/KnowledgeConceptsController.cs` (`Controller`, `[Route("CRM/KnowledgeConcepts")]`, const perms, `RequirePage(perm, fallback)`, `SendGatewayAsync(HttpMethod, "/api/crm/...", payload, ct)` / `ProxyJsonAsync` / `ProxyGetAsync` / `LoadOptionsAsync`, `ViewRoot`) + `Views/CRM/KnowledgeConcepts/*` (Index/Create/Edit/_DataTable/_Filter/_Form/_IndexL10n/offcanvas) + `Resources/Views/CRM/KnowledgeConcepts/*.{en,tr,fr,es,zh,ar,ru}.resx` (7-dil) + `wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js`.
- **Tüketilecek claim API (gateway'den, HAZIR):** `GET /api/crm/content-composition/claims` (list) · `GET .../claims/{id}` · `POST .../claims` (create) · `PUT .../claims/{id}` (update) · `POST .../claims/{id}/approve` · `POST .../claims/{id}/archive`. Perm: `crm.claim.read` / `crm.claim.manage` / `crm.claim.approve` (SCMM-12-API'de **seed + 97c5 grant edildi** → dev-fallback GEREKMEZ, doğrudan kullan).
- **ClaimDto (görüntü/form alanları):** ClaimId, ClaimCode, ClaimName, Description?, ClaimText (governed body — approved olunca donar), Qualifiers[], Applicability{ProductRefs[],MarketRefs[],AudienceRefs[],EligibilityPolicyId?}, EvidenceRefs[] (OPAK string — MOD-0031 ertelendi), **ComponentRefs[] (Guid — KnowledgeContent by-id)**, ClaimVersion, Status, EffectiveFrom, EffectiveTo?, ApprovedAt?/By?, audit, IsArchived. List: ClaimListDto{Items,Total}.
- **Component picker kaynağı (HAZIR):** `GET /api/crm/knowledge/contents` (`KnowledgeContentsController`, ListKnowledgeContentQuery) → KnowledgeContent listesi; picker bunları çoklu-seç → ComponentRefs.
- **Target agent:** `.antigravity/agents/frontend-ui-ux.md`.

## Kapsam (yalnız frontend)
1. **Frontend controller** `CRM/Claims` (KnowledgeConcepts aynası): Index (list, RequirePage `crm.claim.read`) · Create/Edit (RequirePage `crm.claim.manage`) · Approve (POST proxy, `crm.claim.approve`) · Archive (POST proxy, `crm.claim.manage`). Tüm çağrılar `SendGatewayAsync`/`ProxyJsonAsync` ile `/api/crm/content-composition/claims`.
2. **Views** (`Views/CRM/Claims/*`): Index (claim list DataTable: ClaimCode/Name/Version/Status/EffectiveFrom/approved/archived + filtre) + Create/Edit (form ya da offcanvas): ClaimCode, ClaimName, ClaimText, Description, Qualifiers (çoklu), **Applicability** (ProductRefs/MarketRefs/AudienceRefs çoklu + EligibilityPolicyId), **EvidenceRefs** (opak string listesi), **ComponentRefs** (component picker — `/api/crm/knowledge/contents`'ten çoklu-seç). Approve/Archive butonları (durum + perm'e göre). Approved claim düzenleme → yeni versiyon (backend hallediyor; UI davranışı yansıtsın).
3. **7-dil L10n** (`Resources/Views/CRM/Claims/*.{en,tr,fr,es,zh,ar,ru}.resx`): **gerçek çeviriler** (NavL10n guard — key-echo YASAK, 7 dilin hepsi).
4. **Nav kaydı:** claim ekranı sidebar'da görünsün (KnowledgeConcepts'in nav'a nasıl girdiğini ölç ve aynı mekanizmayı kullan — module manifest / DynamicModuleMenu).
5. **JS** (gerekirse): picker/datatable davranışı (concept-slim.js deseni).

## YAPMA
- Backend'e DOKUNMA (ClaimsController/API/ocelot/RBAC HAZIR). KnowledgeConcepts console'unu DEĞİŞTİRME (yalnız desen olarak oku). Yeni API/endpoint uydurma (yalnız mevcut claim + knowledge/contents uçları). EvidenceRefs'i opak-string ötesine taşıma (MOD-0031 yok). ComponentRefs = KnowledgeContent by-id (yeni component aggregate KURMA). dev-fallback perm ekleme (crm.claim.* gerçek + granted). 7-dilde key-echo bırakma. Assembly/execution (SCMM-14) YOK.

## Acceptance
- **E2:** build temiz; console render olur; proxy çağrıları doğru uçlara gider (`/api/crm/content-composition/claims`); RequirePage RBAC (crm.claim.*); 7-dil resx **key-echo yok** (NavL10n guard); component picker `/api/crm/knowledge/contents`'ten listeler; verifier/smoke sıfır-yeni-fail.
- **E4 (fleet WARM):** UI'dan claim create → list'te görünür → approve (draft→approved) → archive; component picker populate; applicability/evidence/qualifiers round-trip; authenticated (97c5 crm.claim.* granted). Yetkisiz kullanıcıya sayfa iskeleti çizilmez (UAS-001).
- **Kapsam:** yalnız Diten.Web (controller+views+resx+js+nav). Backend/API DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SCMM-12-UI · Prompt P-SCMM-12-UI v1.0  (Claim authoring console + component picker — CAND-CAP-0011, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: 861d56ea · Worktree: ana checkout

Önce oku (mirror = KnowledgeConcepts console):
1. execution/domains/commercial-suite/work-packs/WP-SCMM-12-UI-claim-authoring.md (bu WP)
2. frontend/Diten.Web/Controllers/CRM/KnowledgeConceptsController.cs (Controller/RequirePage/SendGatewayAsync/ProxyJsonAsync/ProxyGetAsync/LoadOptionsAsync/ViewRoot deseni — BİREBİR ayna)
3. frontend/Diten.Web/Views/CRM/KnowledgeConcepts/* (Index/Create/Edit/_DataTable/_Filter/_Form/_IndexL10n/offcanvas) + Resources/Views/CRM/KnowledgeConcepts/*.{en,tr,fr,es,zh,ar,ru}.resx (7-dil deseni) + wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js
4. services/Diten.CrmService/.../Features/ContentComposition/Claims/ClaimDtos.cs (ClaimDto/ClaimListDto — görüntü/form alanları)

NE (yalnız frontend, Diten.Web):
 1) CRM/Claims controller (KnowledgeConcepts aynası): Index (list, RequirePage crm.claim.read) · Create/Edit (RequirePage
    crm.claim.manage) · Approve (POST proxy, crm.claim.approve) · Archive (POST proxy, crm.claim.manage). Tüm çağrılar
    SendGatewayAsync/ProxyJsonAsync ile /api/crm/content-composition/claims uçlarına (list/get/create/update/approve/archive).
 2) Views/CRM/Claims: Index (DataTable: ClaimCode/Name/Version/Status/EffectiveFrom/approved/archived + filtre) + Create/Edit
    form: ClaimCode, ClaimName, ClaimText, Description, Qualifiers(çoklu), Applicability(ProductRefs/MarketRefs/AudienceRefs
    çoklu + EligibilityPolicyId), EvidenceRefs(opak string listesi), ComponentRefs(COMPONENT PICKER — /api/crm/knowledge/contents'ten
    çoklu-seç KnowledgeContent). Approve/Archive butonları (durum+perm'e göre). Approved claim düzenleme→yeni versiyon (UI yansıtsın).
 3) 7-dil resx (Resources/Views/CRM/Claims/*.{en,tr,fr,es,zh,ar,ru}) — GERÇEK çeviriler, key-echo YOK.
 4) Nav kaydı: claim ekranı sidebar'da (KnowledgeConcepts nav mekanizmasını ölç + aynısını kullan).
NEDEN: SCMM-12 Claim API + gateway route hazır+doğrulandı; içerik ekibi claim'leri UI'dan yazar/onaylar, component (KnowledgeContent) seçer.
NASIL: KnowledgeConcepts console'unu birebir örnek al (Controller/RequirePage/proxy/Views/7-dil/JS). crm.claim.* gerçek+granted → dev-fallback KULLANMA.
YAPMA: backend/API/ocelot/RBAC DEĞİŞTİRME (hazır); KnowledgeConcepts console DEĞİŞTİRME (yalnız oku); yeni API uydurma; EvidenceRefs'i opak ötesine taşıma (MOD-0031 yok); yeni component aggregate; 7-dilde key-echo; yetkisiz kullanıcıya iskele çizme (UAS-001); assembly/execution (SCMM-14).
DOĞRULA (E2 + E4):
 - build temiz; console render; proxy doğru uçlara; RequirePage crm.claim.*; 7-dil key-echo yok; component picker knowledge/contents'ten listeler.
 - E4 (fleet): UI create→list→approve→archive; picker populate; applicability/evidence/qualifiers round-trip; authenticated (97c5).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT fleet'te E4 doğrular.

Durma koşulları: claim API sözleşmesi beklenenden farklıysa (raporla) · KnowledgeConcepts nav mekanizması claim'e uygulanamıyorsa · component picker knowledge/contents'ten beslenemezse · kapsam frontend dışına (backend/API) taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-14) → **ACCEPTED (E2)** · E4 = kullanıcı manuel UI testi
```text
Commits: 31b2d51e (console) + eeba3815 (nav) · Agent: PASS · CT: ACCEPTED E2 · E4: login gerektirir → kullanıcı manuel
```
- ✅ **Scope:** 20 frontend dosya (Controller/ViewModels/Views/7-dil resx/JS) + nav (CrmManifestProvider +8, SharedResource 7-dil `Nav.Page.CLAIMS`). **Backend/API/ocelot/RBAC/Claim-logic + KnowledgeConcepts console DOKUNULMADI.** Tek Web-dışı dosya CrmManifestProvider.cs (nav mekanizması — meşru, agent flag'ledi).
- ✅ **CT kendi koşumu (izole worktree):** Diten.Web build 0 hata; **Diten.Web.Tests 137/0**; Platform nav/manifest guard **60/0** (NavManifestL10nGuard CLAIMS 7-dil + CrmManifest + ManifestProvider). 
- ✅ **Spot-check:** proxy uçları doğru (`/api/crm/content-composition/claims` + component picker `/api/crm/knowledge/contents`); perm crm.claim.read/manage/approve **dev-fallback YOK** (WP'ye uygun, crm.claim.* gerçek+granted); resx **gerçek çeviri** (ClaimText en"Claim Text"/tr"İddia Metni", key-echo yok). Verifier: agent KnowledgeConcepts aynasıyla 9-fail parity (0 yeni) raporladı.
- ⏳ **E4 = kullanıcı manuel UI testi:** login→/CRM/Claims→create (component picker + applicability/evidence/qualifiers)→approve(gövde donar)→archive. Alttaki API+gateway zaten E4-authenticated kanıtlı (SCMM-12-API + GW); UI ince proxy. Fleet restart gerekir (yeni console + CrmManifestProvider nav yüklenir).

## Kalan (bu WP dışı)
- CT E4 (fleet): UI claim yaşam döngüsü + picker + authenticated.
- SCMM-13 (iki-dil varyant) · SCMM-14 (assembly ④⑤⑥ — component+claim+template tüketir).
- Branch main sync (49 behind) → PR öncesi.
