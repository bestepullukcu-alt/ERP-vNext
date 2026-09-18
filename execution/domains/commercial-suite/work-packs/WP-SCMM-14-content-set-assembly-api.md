# WORK PACKAGE — WP-SCMM-14 (backend+API) · ContentScope + ContentSet (Assembly) aggregate

> **Control Tower kaydı (SoR).** SCMM R1, Content Studio ④⑤⑥. Module: **CAND-CAP-0011**, CrmService. Branch: `feature/scmm-content-studio` (HEAD accae9aa). Bağ: SCMM-10/11/12/13 ✅. **Tasarım: [DESIGN-SCMM-14](DESIGN-SCMM-14-content-set-assembly.md) (kararlar KİLİTLİ).**
> **Sınır:** yalnız **mutable draft authoring**. Freeze/immutable Revision/review = SCMM-15 (MOD-0023 bloklu); render/output = 16; release = 17. **Bu WP freeze/review/output/release YAPMAZ.** UI = SCMM-14-UI (ayrı, D14-e).

## Ölçülmüş girdi (CT)
- **Tüketilen (hazır):** `ConceptChainTemplate` (`ChainVersion` + `Steps: List<ConceptChainStep>` — arrangement iskeleti; SCMM-10) · `Claim`/`ClaimDto` (`ClaimVersion`; SCMM-12) · `KnowledgeContent` (`ContentVersion`+`LanguageCode`+`ContentSetId` variant; SCMM-13) · `IEligibilityEvaluationPort.EvaluateAsync(ResolveEligibilityQuery, ct) → Response<EligibilityResult>` (in-process; SCMM-11).
- **Mirror desen:** `KnowledgeConceptChainTemplatesController` + `Features/ContentComposition/Claims/*` (aggregate+CQRS+controller+RBAC seed SCMM-05-S1 deseni).
- **Gateway:** `/api/crm/content-composition/{everything}` ocelot route **VAR** (SCMM-12-API-GW) → yeni route gerekmez.
- Yeni aggregate (ContentScope, ContentSet) — mevcut YOK.

## Kapsam (backend + API) — KİLİTLİ kararlara göre
### A) `ContentScope` aggregate (D14-a — reusable, versioned)
- Alanlar: `ScopeCode`, `ScopeName`, `ProductRefs[]`, `MarketRefs[]`, `AudienceRefs[]`, `Channel?`, `LanguageCode?`, `PeriodFrom?/To?`, `ScopeVersion`, `Status` (draft/active/archived), tenant-scoped. **İsim ContentScope — MOD-0167 StrategyTemplate DEĞİL.**
- CQRS: Create/Update/Archive + List/Get + repo + class-map(GUID trap) + audit (MOD-0162 audit seam). RBAC `crm.content-scope.read/manage`.

### B) `ContentSet` aggregate (draft — ⑤ resolved book)
- `SetCode`, `SetName`, `Description?`, tenant-scoped.
- `TemplateRef { ConceptChainTemplateId + ChainVersion }` (pin, D14-d).
- `ScopeRef { ContentScopeId + ScopeVersion }?` (opsiyonel, pin — D14-a/d).
- `SelectedComponents[] { KnowledgeContentId, ContentVersion(pin), LanguageCode, Role?, Arrangement{ TemplateStepId, BranchId?, Position } }` (D14-b/d).
- `SelectedClaims[] { ClaimId, ClaimVersion(pin), Arrangement{ TemplateStepId, BranchId?, Position } }` (D14-b/d).
- `DraftSchemaVersion` (int) · optimistic concurrency = `EntityBase.Version` · `Status` (draft/inactive/archived — **approved/frozen YOK**).
- `EligibilitySnapshot?` (D14-c non-blocking validation sonucu: per-item Eligible/Blocked/Unresolved + policyVersion + evaluatedAt).
- **Davranışlar:**
  - **Create draft:** template (+ ops. scope) seç → boş draft; ref'ler seçim anında **pin**.
  - **clone-to-draft:** mevcut set → yeni id, ref'ler remap, `Status=draft`, **no-inherited-approval** (kopya onaylı başlamaz).
  - Component/claim **ekle/çıkar/arrange:** template cardinality/branch (ConceptChainStep) + dil/variant (SCMM-13) doğrula; geçersiz slot/branch → 400/409.
  - **Apply eligibility (D14-c):** set scope+component'lardan `ResolveEligibilityQuery` kur → `IEligibilityEvaluationPort.EvaluateAsync` → sonucu `EligibilitySnapshot`'a yaz. **Non-blocking** (draft kaydını engellemez). Altyapı hatası **fırlatılır** (fail-closed, Unresolved'a çevrilmez).
- CQRS + repo + class-map(GUID trap) + audit. RBAC `crm.content-set.read/manage`.

### C) HTTP + RBAC
- `ContentScopesController` + `ContentSetsController` (Claims/ChainTemplates controller aynası), route `api/crm/content-composition/content-scopes` + `.../content-sets` (List/Get/Create/Update/Archive + ContentSet: clone, add/remove/arrange, apply-eligibility). `[Authorize]` + `[HasPermission]` + Response<T> + request DTO map.
- RBAC seed/grant (SCMM-05-S1/SCMM-12-API deseni): `crm.content-scope.*` + `crm.content-set.*` AuthService'e + 97c5 grant.

## YAPMA
- Freeze/immutable Revision (SCMM-15) · render/output (16) · release/withdrawal (17) YAPMA. Template/Claim/KnowledgeContent/Eligibility aggregate'lerini DEĞİŞTİRME (yalnız tüket + version-pin). Physical delete (archive/retire). no-inherited-approval'ı ihlal etme (clone onaysız). Inline scope (D14-a reusable). Live-latest ref (D14-d pin). LE boyutu ekleme. Yeni microservice/global serializer. Frontend (SCMM-14-UI ayrı). Başka modül.

## Acceptance
- **E2:** build temiz; unit — ContentScope CRUD/versioned · ContentSet create-from-template (ref pin) · clone-to-draft (yeni id, remap, draft, no-inherited-approval) · component/claim arrange (geçerli slot; geçersiz branch/cardinality→400/409) · version-pin (seçim anındaki sürüm; kaynak sonra değişse set eskiyi tutar) · apply-eligibility (port çağrısı, snapshot yazılır, non-blocking; port-throws→propagate fail-closed) · duplicate-code 409 · audit · class-map GUID trap.
- **Regresyon:** tam CrmService.Application.Tests — bilinen PII flake HARİÇ yeni fail YOK (baseline-diff).
- **E4 (fleet, CT):** 97c5 grant + authenticated — content-scope create → content-set create-from-template → component/claim ekle+arrange → apply-eligibility snapshot → clone-to-draft; yetkisiz→403. (Gateway route hazır.)
- Kapsam: yalnız ContentScope+ContentSet (+audit+RBAC+HTTP). Freeze/render/release YOK; tüketilen aggregate'ler DEĞİŞMEZ; frontend YOK.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-14 · Prompt P-SCMM-14 v1.0  (ContentScope + ContentSet assembly aggregate + API — CAND-CAP-0011)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: accae9aa · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-14-content-set-assembly-api.md (bu WP)
2. execution/domains/commercial-suite/work-packs/DESIGN-SCMM-14-content-set-assembly.md (KİLİTLİ kararlar D14-a..e + LE)
3. DESEN: services/Diten.CrmService/.../Features/ContentComposition/Claims/* (aggregate+CQRS+DTO+mapper+permissions+repo) + Api/Controllers/CRM/KnowledgeConceptChainTemplatesController.cs (controller) + ClaimsController.cs (route/HasPermission/DTO map)
4. TÜKETİLEN (DEĞİŞTİRME, oku): Domain/Entities/*ConceptChainTemplate*.cs (ChainVersion + Steps:ConceptChainStep — branch/cardinality) · Claim.cs/ClaimDtos.cs (ClaimVersion) · KnowledgeContent.cs (ContentVersion/LanguageCode/ContentSetId) · Features/ContentComposition/Eligibility/IEligibilityEvaluationPort.cs + ResolveEligibilityQuery/EligibilityResult
5. RBAC seed: SCMM-05-S1 + SCMM-12-API DataSeeder (crm.* AuthService seed + 97c5 grant)

NE (backend + API, iki yeni aggregate — ContentComposition namespace):
 A) ContentScope (reusable, versioned): ScopeCode/Name + ProductRefs/MarketRefs/AudienceRefs + Channel?/LanguageCode?/Period? +
    ScopeVersion + Status. CRUD+List+Get+repo+class-map(GUID)+audit(MOD-0162). RBAC crm.content-scope.read/manage. İsim ContentScope (MOD-0167 StrategyTemplate DEĞİL).
 B) ContentSet (draft, ⑤ resolved book): SetCode/Name + TemplateRef{ChainTemplateId+ChainVersion(pin)} + ScopeRef{ContentScopeId+ScopeVersion(pin)}? +
    SelectedComponents[]{KnowledgeContentId,ContentVersion(pin),LanguageCode,Role?,Arrangement{TemplateStepId,BranchId?,Position}} +
    SelectedClaims[]{ClaimId,ClaimVersion(pin),Arrangement{...}} + DraftSchemaVersion + EntityBase.Version(optimistic) + Status(draft/inactive/archived) + EligibilitySnapshot?.
    Davranış: create-draft(template+ops.scope, ref PIN seçim anında) · clone-to-draft(yeni id, remap, Status=draft, NO-INHERITED-APPROVAL) ·
    component/claim ekle/çıkar/arrange (template cardinality/branch + dil/variant doğrula; geçersiz→400/409) ·
    apply-eligibility: scope+component'lardan ResolveEligibilityQuery kur → IEligibilityEvaluationPort.EvaluateAsync → EligibilitySnapshot'a yaz
    (NON-BLOCKING draft; port-throws→PROPAGATE fail-closed). CRUD+repo+class-map(GUID)+audit. RBAC crm.content-set.read/manage.
 C) ContentScopesController + ContentSetsController (Claims/ChainTemplates aynası), route api/crm/content-composition/content-scopes + content-sets
    (List/Get/Create/Update/Archive + ContentSet: clone, arrange, apply-eligibility). [Authorize]+[HasPermission]+Response<T>+DTO map.
    RBAC seed crm.content-scope.* + crm.content-set.* AuthService + 97c5 grant (SCMM-12-API deseni).
NASIL: Claims/ChainTemplates aggregate+CQRS+controller desenini birebir izle. Ref PIN = seçim anındaki version snapshot (D14-d). Eligibility=in-process port.
       new-aggregate class-map/GUID subtype-4 tuzağı (CrmService). Arrangement = ConceptChainStep id/branch'e göre slot.
YAPMA: freeze/Revision(SCMM-15)/render(16)/release(17); tüketilen aggregate DEĞİŞTİR; inline scope (reusable); live-latest (pin); no-inherited-approval ihlal;
       physical delete; LE boyutu; frontend(SCMM-14-UI); global serializer; başka modül.
DOĞRULA (E2):
 - build temiz; unit: ContentScope CRUD · ContentSet create(ref pin)/clone(no-inherited-approval)/arrange(geçersiz branch→400/409)/version-pin(kaynak değişse eski)/
   apply-eligibility(snapshot+non-blocking; port-throws propagate)/duplicate-code 409/audit/class-map.
 - TAM CrmService.Application.Tests: bilinen ContactLocationPii flake HARİÇ yeni fail YOK (baseline-diff).
Ayrı commit(ler): ContentScope · ContentSet · controller+RBAC. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E4'ü fleet'te authenticated doğrular.

Durma koşulları: ConceptChainStep branch/cardinality şekli beklenenden farklıysa (raporla) · eligibility port ResolveEligibilityQuery şekli uymuyorsa · arrangement template'e map edilemiyorsa · kapsam ContentScope+ContentSet dışına (freeze/render/release/frontend) taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-14) → **ACCEPTED (E2 + E4-lite)**
```text
Commits: 0e713b3c (backend: 2 aggregate+CQRS+persistence+test) + a80b95f1 (HTTP+RBAC+test) · Agent: PASS · CT: ACCEPTED
```
- ✅ **Scope:** ~31 dosya, hepsi ContentScope/Set + persistence/DI + controller + RBAC + test. Tüketilen aggregate (Template/Claim/KnowledgeContent/Eligibility) + freeze/render/release + frontend **dokunulmadı**. Audit = mevcut `IContentCompositionAuditPublisher` (CAND-CAP-0011 seam, +2 entity etiketi — uydurma değil).
- ✅ **Mantık (CT okudu):** clone-to-draft **no-inherited-approval** (SelectionId=NewGuid, Status=Draft, **EligibilitySnapshot=null**, pinned version taşınır); apply-eligibility **non-blocking + fail-closed** (`EvaluateAsync` try/catch'siz→propagate; no-policy→Unresolved asla atlanmaz; non-success=decision→Unresolved); version-pin seçim anında (D14-d).
- ✅ **CT kendi koşumu (izole worktree):** build 0 hata; SCMM-14 testleri **37/37**; tam CrmService.Application.Tests **1710 pass / 0 fail** (PII flake dahil 0) → regresyon yok.
- ✅ **E4-lite (standalone CrmService 5093 + dev-token):** content-scope unauth→401 · read-only→403 · create→201 (AKKORA/TR/Cardio) · list→200 · content-set endpoint→200 = yeni HTTP+RBAC+persistence **canlı**. (Tam assembly akışı create-from-template = unit-proven 37 test; canlı seed verisi wipe'lı olduğundan SCMM-14-UI manuel testinde tam akış görülecek.)
- ✅ **3 belgelenmiş karar makul:** arrangement TemplateStepId=ConceptTypeId/BranchId=BranchCode (ConceptChainStep sentetik id yok); eligibility anchor=per-claim EligibilityPolicyId; audit=CAND-CAP-0011 (WP "MOD-0162"sinden daha doğru).

## Kalan (bu WP dışı)
- **SCMM-14-UI** (Content Studio workspace — assembly authoring, dropdown/select2, no raw Id — D14-e).
- SCMM-15 (freeze+Revision+review, MOD-0023 bloklu) · SCMM-16 (render/PDF, MOD-0026) · SCMM-17 (release).
- CT E4 (fleet authenticated).
