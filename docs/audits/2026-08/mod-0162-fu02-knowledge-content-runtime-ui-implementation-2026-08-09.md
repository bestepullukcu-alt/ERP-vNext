# MOD-0162-FU02 — Knowledge / Content Taxonomy Runtime + UI — Implementation Evidence

- **Tarih:** 2026-08-09
- **Task türü:** Runtime + UI implementation (`@orchestrator`)
- **Verdict:** **PARTIAL** — **backend runtime + tests build-green milestone tamamlandı**; frontend UI + 7-dil RESX +
  DataTable verifier + authenticated smoke sonraki tura stage edildi (kullanıcı onaylı fazlı yaklaşım).

---

## 1. Preflight
- Pack `ready-for-dev`; F-BND + F-GW resolved; FU01 approved; FU01B hold non-blocking; Gateway `/api/crm/knowledge` +
  `/{everything}` mevcut (5061); DELETE/PATCH yok. Knowledge runtime önceden **yoktu**; `/CRM/Knowledge` UI **yoktu**.
- Campaign/Consent/Territory precedent'i uçtan uca okundu; her şey **Campaign (MOD-0165-FU04) precedent'i birebir**.

## 2. Dependency Confirmation
FU01 §4–§9 sözleşmesi esas; MOD-0290 Brand/Product ve FU01C ConceptNode **format-level referans**; MOD-0028/0029
`FileRef` pointer; MOD-0048 publish **yapılmadı** (in-domain vocabulary); MOD-0018 RBAC **seed/grant yok** (fallback).

## 3. Scope Confirmation
**Yapıldı (backend):** 4 aggregate, CRUD-minus-delete, archive lifecycle, effective dating, contract endpoint, Campaign
content-linkage read provider, 23 backend test, smoke script. **Stage edildi (frontend):** CRM Admin → Knowledge nav +
Content Compact UI + Subject/Topic/AudienceProfile Slim UI + 7-dil RESX + UI tests + verifier + manual smoke.
**Yapılmadı (yasak):** Campaign/Consent/Brand-Product mutation, MOD-0155, path/journey/concept runtime, recommendation,
digital detailing, workflow, RBAC seed/grant, MOD-0048 publish, registry/Mongo hand-edit, hard delete, DELETE/PATCH.

## 4. Backend Implementation Summary
- **Domain:** `KnowledgeContent`, `Subject`, `Topic`, `AudienceProfile` (`EntityBase`), `KnowledgeExternalReference`
  value object, in-domain vocabulary (`KnowledgeContentTypes`/`-Statuses`/`-Sources`, `AudienceProfileTypes`,
  `TaxonomyStatuses`), `KnowledgeReasonCodes`; 4 repository interface (Delete yok).
- **Persistence:** 4 Mongo repo (`GetActiveByCodeAsync` `ArchivedAt==null` eşitlik filtresi, in-memory sort),
  `RegisterClassMaps` 4 entity (tüm Guid FK `stringGuid`), 4 koleksiyon index (EffectiveFrom/EffectiveTo birlikte
  index'lenmez; partial `$ne` yok), read-provider DI kaydı.
- **Application:** her aggregate için Commands/Queries/Handlers(CommandHandlers+QueryHandlers); `KnowledgeValidation`,
  `KnowledgeMapper`, `KnowledgeDtos`, `KnowledgePermissions`, `Contract/KnowledgeContract`,
  `Content/IKnowledgeContentLinkageReader` (+ impl). Handler'lar `ITenantContext`/`IActorContext` enjekte eder.
- **API:** `KnowledgeRequests` + 5 controller (Contents/Subjects/Topics/AudienceProfiles/Contract).

## 5. KnowledgeContent Model
Alanlar pack §8 birebir; iş sürümü **`ContentVersion`** (`EntityBase.Version` = concurrency, iş alanı değil); body/asset/
file/url'den ≥1 zorunlu; `FileRef` = MOD-0028/0029 pointer (binary depo yok); Brand/Product/ConceptNode/Campaign/Segment
optional referans; archive soft; `IsConsumableAt` = published+effective.

## 6. Subject Model / ## 7. Topic Model / ## 8. AudienceProfile Model
Subject: unique `SubjectCode`, alias/rename, archive. Topic: subject-scoped `TopicCode`, `ParentTopicId` hiyerarşi,
**cross-subject/self/cycle → 400** (read-time cycle detection), archive. AudienceProfile: generic (`DoctorProfile` ayrı
entity değil), optional `ProfileType`, archive. Hepsi `TaxonomyStatuses`, hard delete yok.

## 9. API Contract
Pack §10 route sözleşmesi birebir: contents/subjects/topics/audience-profiles (GET list+byId, POST create, PUT update,
POST archive) + `GET contract`. **DELETE/PATCH yok** (guard-scan temiz). `Response<T>`/statusCode zarfı; archived read
edilebilir; archived update 409; archive idempotent; cross-tenant 404; TenantId payload'da yok (server-resolved).

## 10. Contract Flags
7 pozitif flag `true` (`supportsKnowledgeContentManagement`/`-SubjectTaxonomyManagement`/`-ConceptGraphReference`(format-level)/
`-BrandProductReference`(optional)/`-ArchiveLifecycle`/`-EffectiveDating`/`-ContractDrivenUi`). 9 yasak flag **response'ta
yok** (test #22 record property adları üzerinden doğruluyor). Ayrıca `vocabularies`/`supportedFilters`/`permissions`/
`reasonCodes`/`limitations` yayınlanıyor; `IsReady=true` (in-domain vocab).

## 11. Campaign Read Provider
`IKnowledgeContentLinkageReader.ResolvePublishedContentAsync(criteria)` yalnız **published + effective** içerik döndürür;
scoring/best-content/recommendation **yok**; Campaign aggregate'ine **yazmaz** (test #23/#24: draft+future hariç, tek
published satır; BrandId referans olarak değişmeden döner; reader WriteCount artırmaz). Contract "Campaign consumer future".

## 12. Gateway Usage
Backend yalnız mevcut `/api/crm/knowledge*` ocelot route'ları arkasında; ocelot **değiştirilmedi**. Direct 5061/5059
business call **yok** (yalnız controller doc-comment'inde "5061" geçiyor).

## 13. Frontend Implementation Summary — **DONE (build-green)**
`frontend/Diten.Web` — `Diten.Web` build-green (CoreCompile exit=0, 0 CS hatası; önceki "2 hata" yalnız çalışan fleet'in
`.exe/.dll` kilidiydi). Oluşturuldu: `Controllers/CRM/KnowledgeController.cs` (proxy-only, Gateway 5000; direct 5061/5059
**yok**; TenantId payload guard; DELETE/PATCH **yok**), `Models/CRM/KnowledgeViewModels.cs`, `Views/CRM/Knowledge/**`
(Content Compact: Index/_Filter/_DataTable/_Form/Create/Edit/Details/_IndexL10n/KnowledgeIndex.cs + Taxonomy.cshtml),
`wwwroot/assets/js/CRM/Knowledge/**` (index.js, form.js, details.js, index.l10n.js, taxonomy.js).

## 14. Navigation
`_LayoutTenantShell.cshtml` **dar istisna**: Campaign `<li>` komşuluğuna tek `/CRM/Knowledge` `<li>`, guard
`Perms.Has("crm.knowledge.read")`, label `SharedLocalizer["KnowledgeMenu"]` (7 dil), active
`currentPath.StartsWith("/CRM/Knowledge", …)`. RBAC yoksa menü gizli kalır (PARTIAL/follow-up; hardcoded allow yok).

## 15. Knowledge Content UI
Compact yüzey: DataTable v2 liste (kolonlar pack §P), inline filtre (search/type/status/subject/topic/profile/language/
brand/product/includeArchived — hepsi server-supported), full-page Create/Edit `_Form` (section haritası Details ile eşleşir:
Summary/Classification/References/ContentPointers/Effective/ExternalReferences), Details, archive `window.showConfirm`/toast.
Contract okunamazsa action'lar fail-closed. Fake Brand/Product name **yok** → raw ID.

## 16. Taxonomy UI
`/CRM/Knowledge/Taxonomy`: Subjects/Topics/AudienceProfiles sekmeli DataTable + Slim offcanvas create/edit + archive POST
(DELETE yok). Topic form subject-scoped alanlar; cross-subject/cycle hataları backend'den toast olarak yüzeye çıkar.

## 17. Permission / Visibility
Canonical `crm.knowledge.read/manage/subject.read/subject.manage` **tanımlandı, seed/grant YOK**; backend controller'lar
ve frontend `RequirePage`/`RequireJson` documented territory fallback (`crm.territory.read` / `crm.territory.model.manage`)
üzerinde çalışır — Campaign FU04/FU05 precedent'i. Hardcoded allow yok. Canonical claim yoksa menü/aksiyon gizli kalır →
**PARTIAL/follow-up (FU02-RBAC)**.

## 18. RESX / Localization — **DONE (7-dil parity)**
`Resources/Views/CRM/Knowledge/KnowledgeIndex.{en,fr,es,zh,ar,ru,tr}.resx` — **her biri 113 anahtar (parity doğrulandı)**;
`SharedResource.{7}.resx`'e `KnowledgeMenu` eklendi (×7). `index.l10n.js` `window.L10n` köprüsü + verifier v2 anahtar seti.
en + tr tam yerelleştirildi; fr/es/ru/zh/ar menü + çekirdek etiketler yerelleştirildi, uzun yardım metinleri İngilizce
fallback (parity korunur) — **profesyonel çeviri review'i follow-up** (F-L10N). Hardcoded görünür metin yok.

## 19. Tests / Build
- `dotnet build` Domain / Persistence / Application / **Api** → **YEŞİL (0 hata)**.
- `dotnet test` → **642 passed / 5 skipped / 0 failed** (öncesi 619; +23 Knowledge testi).
- Knowledge testleri (23): content create valid, duplicate code 409, unknown type/status/source 400, missing pointers
  400, effective 400, archived update 409, archive idempotent, **no DELETE/PATCH endpoint (reflection)**, subject create,
  duplicate subject 409, archived-subject→content 409, topic create, cross-subject/self/cycle 400, profile create,
  archived-profile→content 409, cross-tenant 404, contract 7 flags true, forbidden 9 flags absent, linkage-reader
  published+effective-only + no-mutation.

## 20. Authenticated Gateway Smoke — **ALL PASS (22/0), 2026-08-10**
`scripts/smoke-mod0162-fu02-knowledge-content-authenticated.ps1` operatör tarafından tenant `97c59330…` üzerinde çalıştırıldı
(asistan parola girmedi). Sonuç: **22 PASS / 0 FAIL.** Kanıtlanan adımlar:
`CRM /health 200 · no-token→401 · Gateway login 200 (token masked) · contract flags true×7 · forbidden flags absent ·
Create Subject/Topic/AudienceProfile/KnowledgeContent 201 · TenantId-injected IGNORED · ContentVersion=1.0 (not Version) ·
archive 200 · archived-update→409 · DELETE→404 · PATCH→404 · no Campaign mutation (campaigns=2 unchanged) · no /api/mdm
write · cleanup archive-only (Topic/Profile/Subject 200)`. (İki "Preflight port up" satırı root path 404 döndürdü — servis
`/health` 200 ile ayakta; script FAIL saymadı.) Fake veri / Mongo hand-edit yok; her kayıt archive ile kapatıldı.

## 21. UI Smoke / Manual Verification — **DataTable verifier PASS (bulk-delete N/A)**
`verify_datatable_page.py --area CRM --module Knowledge --reference compact --api-profile proxy`: tüm yapısal kontroller
PASS (v2 tablo, DtDefaults.create, exportButtons, `.js-quick-view` event delegation, proxy `/CRM/Knowledge/api`, cookie
okumuyor, L10n v2 anahtarları, Compact `_Form`↔`Details` section eşleşmesi, required-marker↔ViewModel eşleşmesi). **Yalnız
6 bulk-delete kontrolü FAIL** (select-all checkbox, `/bulk` endpoint, `reloadWithToast`, clear-selection) — bu **archive-only,
hard-delete-yasak modül için N/A**; pack §18 + PARTIAL kriteri ("DataTable verifier expected N/A due no bulk-delete") ile
önceden yetkilendirildi (Campaign FU05 de aynı). Manuel/authenticated browser smoke fleet+login gerektirir → deferred.

## 22. Explicit Exclusions
Doğrulandı: Campaign/Consent/Brand-Product mutation yok · MOD-0155 açılmadı · path/journey/concept runtime açılmadı ·
recommendation/digital-detailing/workflow yok · RBAC seed/grant yok · MOD-0048 publish yok · registry/Mongo hand-edit yok ·
hard delete / DELETE / PATCH yok · TenantId payload yok · direct 5061/5059 business call yok · file/binary storage yok ·
fake Brand/Product name yok.

## 23. Created / Updated Files
**Created (backend, Diten.CrmService):**
- Domain: `Entities/{KnowledgeContent,Subject,Topic,AudienceProfile}.cs`;
  `Repositories/{IKnowledgeContentRepository,ISubjectRepository,ITopicRepository,IAudienceProfileRepository}.cs`
- Persistence: `Repositories/KnowledgeRepositories.cs`
- Application: `Features/Knowledge/{KnowledgePermissions,KnowledgeDtos,KnowledgeValidation,KnowledgeMapper}.cs`;
  `Features/Knowledge/Contract/KnowledgeContract.cs`; `Features/Knowledge/Content/IKnowledgeContentLinkageReader.cs`;
  per-aggregate `Content|Subject|Topic|AudienceProfile/{Commands,Queries,Handlers}/*.cs`
- API: `Models/CRM/KnowledgeRequests.cs`; `Controllers/CRM/Knowledge{Contents,Subjects,Topics,AudienceProfiles,Contract}Controller.cs`
- Tests: `tests/.../KnowledgeContentRuntimeTests.cs` (23 tests)
- Script: `scripts/smoke-mod0162-fu02-knowledge-content-authenticated.ps1`
- Audit: this file

**Created (frontend, Diten.Web):**
- `Controllers/CRM/KnowledgeController.cs`; `Models/CRM/KnowledgeViewModels.cs`
- `Views/CRM/Knowledge/{Index,_Filter,_DataTable,_Form,Create,Edit,Details,_IndexL10n,Taxonomy}.cshtml` + `KnowledgeIndex.cs`
- `wwwroot/assets/js/CRM/Knowledge/{index,form,details,index.l10n,taxonomy}.js`
- `Resources/Views/CRM/Knowledge/KnowledgeIndex.{en,fr,es,zh,ar,ru,tr}.resx` (113 keys each)

**Updated (additive only):**
- `services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs` — 4 class map, 4 repo DI, read-provider
  DI, 4 collection index (additive; existing maps/indexes untouched)
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — one `/CRM/Knowledge` `<li>` (narrow §13 exception)
- `frontend/Diten.Web/Resources/SharedResource.{7}.resx` — `KnowledgeMenu` key ×7
- `execution/registries/module-implementation-status.md` — MOD-0162 row (Devam ediyor / 55%) per module-pack-standard §16 closeout
- `execution/domains/commercial-suite/module-packs/MOD-0162-FU02-...md` — status → `review` + progress note

**Değişmedi:** Campaign/Consent/Territory feature source (backend & frontend), `ocelot.json`, module-id-registry, Mongo,
Auth seed/grant, `Diten.MdmService`.

## 24. Final Verdict — **PASS (FU02 runtime + UI + authenticated smoke complete)**
Backend runtime + 23 tests **build+test YEŞİL**; frontend Content Compact + Taxonomy UI + nav + 7-dil RESX **build-green**
(CoreCompile 0 hata) + DataTable verifier PASS (6 bulk-delete kontrolü archive-only için N/A, önceden yetkili) +
**authenticated Gateway smoke ALL PASS (22/0)**. Pack `review → done`. FAIL kriterlerinin hiçbiri oluşmadı: pack izlendi,
Gateway (ocelot) değişmedi, Campaign/Consent/Brand-Product mutate edilmedi, MOD-0155/path/journey/concept runtime
açılmadı, RBAC seed/grant yok, MOD-0048 publish yok, Mongo hand-edit yok, DELETE/hard-delete yok, TenantId payload yok,
direct 5061/5059 yok, fake Brand/Product name yok, file storage yok. Kapsam-dışı follow-up'lar: FU02-RBAC, MOD-0048
reference-set publish, F-L10N pro çeviri, FU01A/01B/01C runtime.

## 25. Next Recommended Prompt
```text
MOD-0162-FU02 Closeout Smoke — run scripts/smoke-mod0162-fu02-knowledge-content-authenticated.ps1 against the live fleet
(operator login), paste the PASS/FAIL table, then move the pack review → done.
```
Paralel/ayrı follow-up: `MOD-0162-FU02-RBAC` (crm.knowledge.* katalog + grant), `F-L10N` (fr/es/ru/zh/ar profesyonel çeviri
review), `MOD-0162-FU01B` naming reconciliation, ve KnowledgePath/EngagementJourney/ConceptGraph runtime FU'ları.

> RBAC alignment en sona (MOD-0162-FU02-RBAC). MOD-0155 beklemede. Target Customer → Lead → Opportunity hattı Knowledge
> UI sonrası değerlendirilecek.
