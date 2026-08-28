---
id: MOD-0165-FU05
name: Campaign / Targeting Admin UI
parent: MOD-0165
parent_name: Campaign Management
domain: commercial-suite
service: frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: review
runtime_code_allowed: true
runtime_code_scope: "UI ONLY — frontend/Diten.Web Campaign / Targeting Admin UI, UI tests ve evidence. Diten.CrmService backend runtime, Gateway config, Auth seed/grant, registry ve Mongo değişikliği yasaktır."
owner: module-pack-author
branch: feature/crm/mod-0165-fu05-campaign-targeting-admin-ui
started: 2026-08-03
target: 2026-08-03
form_field_count: 21
dependencies:
  - MOD-0165-FU02 (Campaign / Targeting boundary PASS)
  - MOD-0165-FU04 (Campaign / Targeting runtime + static target snapshot PASS)
  - MOD-0164-FU02 (Consent & Preference runtime / evaluation provider PASS)
  - MOD-0048 (Consent vocabulary Runtime=SoT reconciliation PASS)
  - MOD-0285 (existing tenant navigation pattern; no Platform runtime change)
  - DEV-0000 (Golden Reference Slim — target canvas, archive confirmation, toast)
  - DEV-0001 (Golden Reference Compact — primary Campaign surface)
---

# MOD-0165-FU05 — Campaign / Targeting Admin UI

> **READY-FOR-DEV UI AUTHORIZATION (2026-08-03).** Kullanıcı bu pack'in hazırlanmasını ve
> `ready-for-dev` olmasını açıkça istedi. Bu pack yalnız FU04'te PASS olan Campaign / CampaignTarget API'lerini
> CRM tenant shell içinde kullanılabilir kılan frontend yüzeyini yetkilendirir. Backend business logic, Gateway
> route değişikliği, RBAC seed/grant, registry, migration ve Mongo yazımı açılmaz.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-03):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU05 --name "Campaign / Targeting Admin UI" --parent MOD-0165`
> → `OK  MOD-0165-FU05: proven against Blueprint/registry.`
>
> **Neden gerekli:** FU04 backend/API runtime PASS olmasına rağmen FU05 yeni ve geniş kapsamlı bir UI
> feature'ıdır. `AGENTS.md` §7/§10 uyarınca `approved` veya `ready-for-dev` module pack olmadan
> `@orchestrator` implementasyona başlayamaz. Commercial Suite domain config ayrıca tenant-shell navigation
> alanını korur; bu pack §6 ve §9'da yalnız Campaign menü girdisi için dar, test edilebilir istisna verir.

---

## 1. Module Summary

Amaç, CRM kullanıcılarının mevcut Gateway arkasındaki FU04 Campaign runtime'ını şu yüzeylerle kullanabilmesidir:

- `CRM Admin → Campaigns` permission-controlled navigation ve deep link.
- Campaign list, detail, create, edit ve archive.
- Detail altında Campaign Targets tab'ı.
- Manual target create, edit ve archive.
- Static Target Snapshot paneli: lightweight manual row editor + JSON paste fallback.
- Consent filter kontrolü, değerlendirme sonucu ve provenance görünümü.
- Contract-driven capability gating, kontrollü loading/empty/error durumları.
- Golden Compact/Slim pattern, DataTable v2 ve yedi dil localization parity.

Hedef kullanıcı tenant içindeki yetkili CRM yöneticisidir. FU05 hiçbir yeni business aggregate veya API açmaz;
FU04 contract'ının frontend consumer'ıdır.

## 2. Ownership and Boundaries

### In-scope

1. CRM Admin → Campaigns navigation/menu entry.
2. Campaign List ve filtreleri.
3. Campaign Detail: summary, references, consent context, external references.
4. Campaign Create/Edit full-page Compact akışı.
5. Campaign archive action; hard delete yok.
6. Campaign Targets tab ve Target detail/provenance görünümü.
7. Manual Target Create/Edit/Archive Slim canvas/offcanvas akışı.
8. Static Target Snapshot paneli.
9. Consent filter controls ve allowed/blocked/unknown/not_applicable gösterimi.
10. ExclusionReason, ReasonCodes ve matched-id provenance gösterimi.
11. `GET /api/crm/campaigns/contract` temelli capability checks.
12. Gateway-only proxy/client entegrasyonu, UI testleri, build, smoke ve evidence.

### Out-of-scope / kesinlikle yetkisiz

- Campaign veya CampaignTarget backend runtime/business-logic değişikliği.
- Consent runtime veya Consent/Preference Admin UI.
- Segment engine veya membership resolution.
- Visit/route planning, due/overdue, last visit veya MOD-0155.
- Frequency, Knowledge, Brand/Product, Digital Detailing veya Recommendation runtime.
- Workflow/approval, import/export engine veya patient data.
- Hard delete veya HTTP `DELETE`.
- Direct port `5061` business call.
- Migration, Mongo hand-edit, RBAC seed/grant, MOD-0048 publish veya registry write.
- `gateway/Diten.ApiGateway/**` değişikliği.
- API'de olmayan master/display resolution veya fake preview/filter.

## 3. Owned Objects

FU05 runtime entity sahiplenmez. `entity_base: EntityBase`, FU04'ün tenant-owned Campaign ve CampaignTarget
aggregate'lerinden miras alınan kontratı belgeler; frontend yeni entity/schema/index oluşturmaz.

- MVC surface: `CampaignsController` (`frontend/Diten.Web`), route `/CRM/Campaigns`.
- Frontend models: contract, campaign, target, snapshot, external-reference ve gateway-envelope view modelleri.
- Views: list/create/edit/details; Campaign form; filters; Campaign ve Target DataTable partial'ları;
  manual-target canvas; snapshot panel; consent/provenance panel; L10n bridge.
- Scripts: Campaign list/detail/form/target/snapshot davranışları ve localization bridge.
- UI permission consumers:
  - `crm.campaign.read`
  - `crm.campaign.manage`
  - `crm.campaign.target.read`
  - `crm.campaign.target.manage`
  - `crm.campaign.snapshot.create` yalnız UI capability adı olarak önerilir; backend seed veya guard değildir.
- API owner: **FU04 / Diten.CrmService**; FU05 yalnız tüketir.

## 4. Entity Fields

Bu UI-only pack schema üretmez. Aşağıdaki alanlar FU04 response/request sözleşmesini tüketir.

### Campaign authoring ve detail

| Field | UI | Kural |
|---|---|---|
| CampaignCode | Create + list/detail | Required; edit requestinde yok/immutable; tenant içinde aktif unique kuralı backend'dedir. |
| CampaignName | Create/Edit | Required. |
| CampaignType | Create/Edit/filter | Required; options contract vocabulary'den. |
| CampaignStatus | Create/Edit/filter | Required; options contract vocabulary'den; archive yalnız endpoint üzerinden. |
| ObjectiveType | Create/Edit | Contract vocabulary; UI API contract'ına göre validate eder. |
| BusinessUnitId | Optional reference | Master fetch yoksa GUID/reference olarak gösterilir. |
| BrandId / ProductId | Optional reference | MOD-0290 runtime resolve edilmez. |
| SubjectId / TopicId | Optional reference | Format-level GUID/reference. |
| ConceptChainTemplateId | Optional reference | Format-level GUID/reference. |
| EngagementJourneyId | Optional reference | Format-level GUID/reference. |
| DefaultKnowledgePathId / DefaultKnowledgeContentId | Optional reference | Runtime/master fetch yapılmaz. |
| DefaultConsentChannel / DefaultConsentPurpose | Optional | Contract vocabulary; snapshot consent filter açıkken request veya campaign default gerekir. |
| StartDate | Required | Date. |
| EndDate | Optional | Varsa `EndDate >= StartDate`. |
| Description / OwnerUserId | Optional | API contract shape'i korunur. |
| ExternalReferences[] | Optional collection | SourceSystem, ExternalId, ExternalCode, ExternalName, ImportedAt, IsPrimary; API'de olmayan alan eklenmez. |
| IsArchived / ArchivedAt / UpdatedAt | Read-only | Lifecycle ve audit gösterimi. |

### CampaignTarget authoring ve detail

| Field | UI | Kural |
|---|---|---|
| TargetType | Create-only identity | Contract options; `campaign-target` hiçbir zaman sunulmaz. |
| TargetId | Create-only identity | Required GUID. |
| TargetDisplayName | Optional label | SoR değildir. |
| TargetStatus | Create/Edit/filter | Contract vocabulary; `excluded` ise ExclusionReason required. |
| TargetSource | Create/Edit/filter | Contract vocabulary. |
| SourceReferenceType / SourceReferenceId | Optional provenance | Master/membership çözümü yapılmaz. |
| SnapshotBatchId | Read-only/filter | Snapshot provenance. |
| Priority | Optional/API default | API contractına uygun numeric validation. |
| SelectionReason | Required | Sessiz target authoring yasak. |
| ReasonCodes[] | Required | Contract reason-code vocabulary; manual create en az `manual_target_selected` veya API'nin kabul ettiği açık değer. |
| ExclusionReason | Conditional | `TargetStatus=excluded` ise required. |
| EffectiveFrom / EffectiveTo | Required/optional | `EffectiveTo >= EffectiveFrom`. |
| ConsentEvaluation | Read-only | Decision/provenance; caller tarafından gönderilmez. |
| ExternalReferences[] | Optional | API request contractı kadar. |
| IsArchived / UpdatedAt | Read-only | Archive lifecycle görünümü. |

## 5. Repo Scope

Yalnız aşağıdaki alanlarda değişiklik yapılabilir:

- `execution/domains/commercial-suite/module-packs/MOD-0165-FU05-campaign-targeting-admin-ui.md`
- `frontend/Diten.Web/Controllers/CRM/CampaignsController.cs`
- `frontend/Diten.Web/Models/CRM/CampaignViewModels.cs`
- `frontend/Diten.Web/Views/CRM/Campaigns/**`
- `frontend/Diten.Web/Resources/Views/CRM/Campaigns/**`
- `frontend/Diten.Web/wwwroot/assets/js/CRM/Campaigns/**`
- `frontend/Diten.Web/tests/**` — yalnız MOD-0165-FU05 test dosyaları veya doğrudan ilgili test registration.
- `frontend/Diten.Web/Resources/SharedResource.{en,fr,es,zh,ar,ru,tr}.resx` — yalnız Campaigns menü/shared
  archive/validation key'leri; var olan key'ler tekrar eklenmez.
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — yalnız §6'daki dar navigation istisnası.
- `docs/audits/mod-0165-fu05-campaign-targeting-admin-ui-2026-08-03.md` — implementation evidence.

Var olan ortak frontend helper'ları tüketilebilir; değiştirilmeleri bu pack tarafından yetkilendirilmez. Ortak
helper değişikliği zorunlu görünürse orchestrator durur ve ayrı authorization ister.

## 6. Protected Paths

### Dar navigation istisnası

Domain config'e göre `_LayoutTenantShell.cshtml` protected alandır. Module Pack daha spesifik otorite olarak yalnız
şu değişikliğe izin verir:

- Dosya: `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml`.
- Section: mevcut `Commercial Suite` hardcoded menu grubunda Accounts / Contacts / Territory Management komşuluğu.
- İzinli değişiklik: `/CRM/Campaigns` deep linkine giden tek `<li>` ve gerekiyorsa Commercial Suite header'ın
  yalnız Campaign permission'ı varken bir kez görünmesini sağlayan mevcut boolean koşulunun minimal genişletilmesi.
- Zorunlu guard: `Perms.Has("crm.campaign.read")`; RBAC fallback nedeniyle canonical key claim'de yoksa mevcut
  frontend resolver davranışı raporlanır, seed/grant veya genişleten yeni resolver yazılmaz.
- Label: yedi dilli `CampaignsMenu` shared key'i; hardcoded visible text yok.
- Aktif route: `currentPath.StartsWith("/CRM/Campaigns", StringComparison.OrdinalIgnoreCase)`.
- Yasak: layout yapısı, diğer menü öğeleri, DynamicModuleMenu ViewComponent, token/cookie akışı, navigation API,
  CSS/JS bundle, impersonation veya shell behavior değişikliği.

MOD-0285 dynamic navigation loader incelendi. Descriptor publish/self-registration Platform/backend değişikliği
gerektirdiği ve FU05 UI-only olduğu için bu pack'te kullanılmaz. İleride MOD-0285 data-driven migration yapılırsa
hardcoded Campaign `<li>` kaldırılması ayrı follow-up'tır; çift menü kabul edilmez.

### Diğer protected alanlar

- `.antigravity/**`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml`.
- `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**`.
- `services/Diten.CrmService/**` ve diğer tüm `services/**`.
- `gateway/Diten.ApiGateway/**` ve özellikle `ocelot.json`.
- `execution/registries/**`, Auth seed/grant dosyaları, migrations ve Mongo data.
- CRM Accounts, Contacts, Territory Management dosyaları; yalnız okunabilir referanstır.

## 7. Dependencies

- FU04 evidence: runtime, contract, Gateway route ve 619-test PASS kanıtı.
- `/api/crm/campaigns` ve catch-all Gateway route'u; methods `GET/POST/PUT/OPTIONS`, `DELETE` yok.
- `GET /api/crm/campaigns/contract`: feature flags, vocabulary, reason codes, permissions ve limitations.
- MOD-0164 `IConsentPreferenceEvaluator` sonucu; UI yalnız provenance gösterir.
- MOD-0048 consent vocabulary reconciliation; publish/write yapılmaz.
- GoldenReferenceCompact: Campaign list/full-page Create/Edit/Details.
- GoldenReferenceSlim: Target create/edit canvas, archive confirmation ve toast davranışı.
- `_LayoutTenantShell`, `IPermissionSnapshot`, `PermissionClaims.HasPermission`, controller `RequirePage` pattern'i.
- Global `window.showConfirm` ve `window.showToast` helper'ları.

## 8. Runtime Constraints

- Frontend browser veya MVC proxy tüm business çağrılarını Gateway `5000` üzerinden yapar.
- Direct `http://localhost:5061`, `https://localhost:5061` veya herhangi bir `:5061` business URL yasaktır.
- Same-origin MVC proxy tercih edilir; HttpOnly access token server-side Gateway requestine aktarılır.
- Payload içinde `TenantId` alanı oluşturulmaz/gönderilmez. Mevcut auth mekanizmasının tenant header/claim akışı korunur.
- Campaign/Target lifecycle archive endpointleriyle yürür; `DELETE` kullanılmaz.
- Contract okunamazsa action'lar varsayılan olarak kapalıdır ve kontrollü error state gösterilir (fail closed).
- Contract flag'i yok/false ise ilgili action hide veya disable edilir; yasak capability türetilmez.
- Backend'in desteklemediği filtre client-side fake filter olarak uygulanmaz; disabled/omitted ve evidence'da limitation.
- Campaign list API bugün yalnız CampaignType, CampaignStatus, BrandId, ProductId, SubjectId ve IncludeArchived
  filtrelerini destekler. Search, ObjectiveType, BusinessUnitId ve date range backend-supported değildir.
- Target list API bugün yalnız TargetType, TargetStatus, TargetSource, SnapshotBatchId ve IncludeArchived destekler.
  ExclusionReason ve consent decision/status server filter değildir.
- API olmayan Snapshot History/Audit tab'ı veya preview uydurulmaz.
- Unknown/future response alanları ignore edilir; yeni feature açılmaz.

## 9. Layout & Shell Contract

- `shell: tenant`.
- Bütün Campaign Razor page'lerinde açıkça `Layout = "_LayoutTenantShell";` yazılır.
- View root: `frontend/Diten.Web/Views/CRM/Campaigns/`.
- MVC route: `/CRM/Campaigns`; detail deep link `/CRM/Campaigns/{campaignId}` veya controller convention ile
  eşdeğer stabil route.
- Breadcrumb, varsa mevcut tenant-shell pattern'iyle `CRM Admin → Campaigns → Detail/Create/Edit` gösterir.
- Yeni shell, table, modal, toast veya breadcrumb pattern'i icat edilmez.
- §6 dar navigation istisnası dışında shared layout değiştirilmez.

## 10. Backend File Convention

FU05 backend feature üretmez. Golden Compact backend klasör convention'ı incelenmiştir fakat FU04 runtime zaten
`services/Diten.CrmService/.../Features/Campaign/` altında PASS'tir. Orchestrator:

- yeni command/query/handler/validator/repository/controller oluşturmaz,
- FU04 sınıflarını taşımaz veya yeniden adlandırmaz,
- request/response sözleşmesini frontend modellerinde birebir tüketir,
- backend ihtiyacı tespit ederse UI'da fake davranış yazmak yerine limitation raporlar.

Bu bölümün standarda göre bulunması zorunludur; `Backend File Convention: N/A — existing FU04 API is protected`
kararı intentional'dır.

## 11. Frontend File Contract

Primary surface GoldenReferenceCompact ile:

```text
frontend/Diten.Web/
├── Controllers/CRM/CampaignsController.cs
├── Models/CRM/CampaignViewModels.cs
├── Views/CRM/Campaigns/
│   ├── Index.cshtml
│   ├── Create.cshtml
│   ├── Edit.cshtml
│   ├── Details.cshtml
│   ├── _Form.cshtml
│   ├── _Filter.cshtml
│   ├── _DataTable.cshtml
│   ├── _TargetsDataTable.cshtml
│   ├── _TargetCreateEditOffcanvas.cshtml
│   ├── _SnapshotPanel.cshtml
│   ├── _ConsentProvenance.cshtml
│   ├── _IndexL10n.cshtml
│   └── CampaignIndex.cs
├── wwwroot/assets/js/CRM/Campaigns/
│   ├── index.js
│   ├── index.l10n.js
│   ├── form.js
│   └── details.js
└── Resources/Views/CRM/Campaigns/
    └── CampaignIndex.{en,fr,es,zh,ar,ru,tr}.resx
```

- Campaign Create/Edit/Details full-page Compact'tır; Campaign için `_CreateEditOffcanvas` yasaktır.
- Manual Target create/edit, daha küçük alt-form olduğu için Golden Slim offcanvas/canvas pattern'ini aynı module
  details yüzeyi içinde kullanabilir.
- Campaign ve Target tablolarında `data-dt-standard="v2"`, skeleton, `_Filter`, toolbar, pagination, sort,
  save-view marker ve loading/empty/error state zorunludur.
- Archive confirmation `window.showConfirm`; toast `window.showToast`. Ham `alert`, `confirm` veya doğrudan custom
  `Swal.fire` kullanımı yasaktır.
- ReasonCodes compact badge/list; excluded rows saklanmaz.
- Snapshot input kararı: lightweight manual row editor + JSON paste fallback. Bu bir import/export engine değildir.

## 12. Validation Rules

| Alan / davranış | Required | UI kuralı |
|---|---|---|
| CampaignCode | Create: yes | Blank submit engellenir; editte immutable/read-only. |
| CampaignName | Yes | Blank engellenir. |
| CampaignType | Yes | Contract vocabulary dışı değer gönderilmez; backend 400 reasonCodes gösterilir. |
| CampaignStatus | Yes | Contract vocabulary; archive değeri edit ile lifecycle bypass etmez. |
| ObjectiveType | API contractına göre | Contract vocabulary'den seçilir. |
| StartDate | Yes | Geçerli tarih. |
| EndDate | No | Varsa `EndDate >= StartDate`. |
| Reference GUID alanları | No | Boş veya geçerli GUID; master fetch/resolve zorunlu değil. |
| DefaultConsentChannel/Purpose | No | Snapshot consent filter açıkken request veya görünür campaign default gerekir. |
| TargetType | Create: yes | Contract vocabulary; `campaign-target` ayrıca deny-list. |
| TargetId | Create: yes | Geçerli GUID. |
| TargetSource | Yes | Contract vocabulary. |
| SelectionReason | Yes | Blank engellenir. |
| ReasonCodes[] | Yes | En az bir görünür reason code; manual default API sözleşmesiyle açıkça gösterilir. |
| ExclusionReason | Conditional | TargetStatus `excluded` ise required. |
| EffectiveFrom | Yes | Geçerli tarih/zaman. |
| EffectiveTo | No | Varsa `EffectiveTo >= EffectiveFrom`. |
| Snapshot TargetItems[] | Yes | En az bir satır; JSON parse ve row validation atomik submit öncesi. |
| ApplyConsentFilter | Yes/default true | True ise ConsentChannel + ConsentPurpose request/default bağlamında required. |
| Consent filter false | Allowed with warning | `consent_filter_not_applied` ciddi warning görünmeden submit edilmez. |
| TenantId | Forbidden | View model/form/JSON payload içinde bulunmaz. |

Client validation erken geri bildirimdir; backend validation ve reasonCodes korunur/gösterilir.

## 13. Failure Path to Verify

1. Contract load 401/403/5xx/timeout → controlled error, capability actionları fail-closed.
2. Campaign list loading/empty/error → Golden state; fake rows yok.
3. Required field veya ters tarih aralığı → submit yok, localized validation.
4. Duplicate CampaignCode veya target 409 → visible toast/detail; veri varmış gibi başarı gösterilmez.
5. Archived campaign edit/target/snapshot → action disabled; yarış durumundaki 409 ayrıca görünür.
6. Archived target edit → disabled; archive tekrarında idempotent/already-archived cevap düzgün işlenir.
7. Snapshot empty/invalid/duplicate row 400 → batch başarı sayılmaz.
8. Different-source 409 → batch yazılmadığı açıkça belirtilir; partial success gösterilmez.
9. Consent context missing 400 → channel/purpose alanlarına validation bağlanır.
10. Blocked/unknown → target excluded olarak görünür; satır gizlenmez.
11. Unauthorized route/action → mevcut 401/403 UX; permission bypass/fallback genişletmesi yok.
12. Unknown backend response field → sessiz ignore; visit/route/recommendation feature açılmaz.

## 14. Authorization Convention

- Actor: authenticated tenant user; controller `[Authorize]` ve mevcut page/action guard pattern'i.
- Menu/list/detail: canonical `crm.campaign.read`.
- Campaign create/edit/archive: `crm.campaign.manage`.
- Target list/detail: `crm.campaign.target.read`.
- Target create/edit/archive: `crm.campaign.target.manage`.
- Snapshot UI intent: `crm.campaign.snapshot.create` önerilir fakat FU04 backend contractında seed/guard olarak yoktur;
  uygulama backend'in mevcut manage guard'ından daha geniş yetki veremez. Seed/grant yokluğunda canonical/fallback
  davranışı evidence'a yazılır.
- FU04 fallback: reads `crm.territory.read`, writes `crm.territory.model.manage`. Frontend yeni fallback icat etmez;
  mevcut `IPermissionSnapshot`, `PermissionClaims.HasPermission` ve `RequirePage` davranışını kullanır.
- Menü guard'ı canonical `crm.campaign.read` kalır. Claim bulunmadığı için görünürlük engellenirse bu **PARTIAL/follow-up**
  olarak raporlanır; seed/grant veya hardcoded allow yapılmaz.
- Permission key'ler lowercase-dotted ve PKS-001 uyumludur.

## 15. Gateway / API Routing Decision

Karar: **Yeni Gateway değişikliği gereksiz.** FU04 rotaları `ocelot.json` içinde mevcuttur; dosya protected kalır.

UI yalnız Gateway üzerinden şunları tüketebilir:

```text
GET    /api/crm/campaigns
GET    /api/crm/campaigns/{campaignId}
POST   /api/crm/campaigns
PUT    /api/crm/campaigns/{campaignId}
POST   /api/crm/campaigns/{campaignId}/archive
GET    /api/crm/campaigns/{campaignId}/targets
GET    /api/crm/campaigns/{campaignId}/targets/{campaignTargetId}
POST   /api/crm/campaigns/{campaignId}/targets
PUT    /api/crm/campaigns/{campaignId}/targets/{campaignTargetId}
POST   /api/crm/campaigns/{campaignId}/targets/{campaignTargetId}/archive
POST   /api/crm/campaigns/{campaignId}/targets/snapshot
GET    /api/crm/campaigns/contract
```

- Gateway base port `5000`; direct `5061` yok.
- HTTP `DELETE` yok.
- TenantId payload yok.
- Backend error/reasonCodes UI toast/detail panelinde görünür.
- Existing authorization cookie/token propagation pattern'i korunur.

## 16. Acceptance Criteria

- [ ] `/CRM/Campaigns` route'u `_LayoutTenantShell` ile render olur ve deep link çalışır.
- [ ] `_LayoutTenantShell.cshtml` değişikliği yalnız §6 dar istisnasındadır; Campaigns menüsü canonical read guard'lıdır.
- [ ] Campaign list DataTable v2/Golden Compact-Slim state contractına uyar.
- [ ] List kolonları: CampaignCode, CampaignName, CampaignType, CampaignStatus, ObjectiveType, BusinessUnitId,
  BrandId, ProductId, StartDate, EndDate, IsArchived, UpdatedAt, Actions.
- [ ] Backend-supported list filtreleri server query kullanır; unsupported Search/ObjectiveType/BusinessUnit/date-range
  fake filter değildir ve evidence'da belgelenir.
- [ ] Campaign details summary, references, consent context, external references ve Targets bölümlerini gösterir.
- [ ] Master runtime yoksa IDs format-level gösterilir; fake display resolution yok.
- [ ] Campaign Create/Edit Compact full-page ve ortak `_Form`; required/date validations vardır.
- [ ] Campaign archive POST archive endpointini ve premium confirmation/toast pattern'ini kullanır; DELETE yok.
- [ ] Targets tab DataTable v2 ile TargetStatus/TargetSource/ReasonCodes/ExclusionReason/ConsentEvaluation/provenance gösterir.
- [ ] Excluded, blocked ve unknown targets gizlenmez.
- [ ] Manual Target Slim canvas açılır; `campaign-target` hiçbir option/listede yoktur.
- [ ] Manual target SelectionReason ve ReasonCodes ister; excluded target ExclusionReason ister.
- [ ] Target archive POST archive endpointini kullanır; DELETE yok.
- [ ] Snapshot paneli lightweight manual row editor + JSON paste fallback kullanır; import/export engine açmaz.
- [ ] ApplyConsentFilter=true channel/purpose ister; campaign defaults görünür prefill olabilir, silent default olamaz.
- [ ] ApplyConsentFilter=false `consent_filter_not_applied` ciddi warning gösterir.
- [ ] Snapshot success SnapshotBatchId, Created/Reconciled/Excluded counts gösterir ve Targets'ı refresh eder.
- [ ] Different-source 409 atomik batch failure; re-run reconciled count görünür.
- [ ] allowed/blocked/unknown/not_applicable ve consent_filter_not_applied badge'leri render olur.
- [ ] MatchedConsentId/MatchedPreferenceIds yalnız provenance; consent/preference payload render edilmez.
- [ ] Contract flags actionları fail-closed enable/disable/hide eder.
- [ ] Permission-controlled list/action/menu visibility mevcut resolver'a bağlıdır; seed/grant yoktur.
- [ ] Tüm yeni visible text en/fr/es/zh/ar/ru/tr RESX/L10n parity taşır.
- [ ] Frontend kodunda direct `5061`, Campaign/Target `DELETE`, TenantId payload veya yasak response alanı yoktur.
- [ ] Diten.Web build, ilgili UI tests, DataTable verifier, RESX parity ve mümkünse authenticated tenant smoke PASS'tir.
- [ ] Evidence raporu belirtilen 24 bölümü ve desteklenmeyen filtre/permission fallback/smoke sınırlamalarını içerir.
- [ ] Backend, Gateway, registry, seed/grant, Mongo ve MOD-0155 değişmemiştir.

## 17. Test Expectations

Minimum otomatik/statik doğrulama:

1. Campaigns route render; contract Gateway client üzerinden load.
2. Campaign list load/loading/empty/error ve filtre render.
3. Create/Edit açılışı, required/date validation, TenantId'siz POST/PUT.
4. Archived campaign edit ve target/snapshot action disable/409 handling.
5. Campaign/Target archive POST kullanımı; Campaign/Target için `DELETE` yokluğu.
6. Detail reference IDs master fetch olmadan render.
7. Targets tab ve status/source/reasons/exclusion/consent provenance render.
8. Target options içinde `campaign-target` yok; SelectionReason ve conditional ExclusionReason validation.
9. Snapshot consent-context, opt-out warning, empty-items validation, success batch/counts ve 409 failure.
10. Consent badge/provenance; consent/preference payload absence.
11. Contract feature flags ve permission hidden/disabled states.
12. Yedi locale dosyasında aynı key seti.
13. Frontend source'ta direct `5061`, forbidden `DELETE`, TenantId ve §17 yasak field guard taraması.
14. `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` PASS.
15. `python3 .antigravity/scripts/verify_datatable_page.py . --area CRM --module Campaigns --reference compact` PASS.
16. Mevcut frontend testleri etkilenmez.

Authenticated smoke hedef tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`. Login → menu/list → create →
detail → manual target → consent-filtered snapshot → allowed/excluded görünümü → opt-out warning → target archive →
campaign archive → disabled mutation → no DELETE → Gateway-only network → locale smoke. Test data uygun değilse
blocked/unknown positive smoke açıkça deferred yazılır; fake veri/Mongo hand-edit yapılmaz.

## 18. Ready-for-dev Checklist

- [x] AGENTS.md, Commercial Suite domain config, master plan, registry ve delivery board okundu.
- [x] DCP-002 identity preflight PASS.
- [x] FU04 evidence ve FU04 gerçek API/contract/controller sözleşmesi okundu.
- [x] Golden Reference Compact pack ve canlı frontend kodu okundu.
- [x] Golden Reference Slim canvas/modal/toast kodu referans olarak okundu.
- [x] Frontmatter tüm zorunlu alanları içeriyor.
- [x] Campaign form field count 21 (>8); `golden_reference: compact`.
- [x] Layout & Shell Contract açıkça `_LayoutTenantShell` yazıyor.
- [x] Backend File Convention, UI-only/N-A olarak açık ve FU04 runtime protected.
- [x] Frontend File Contract Compact dosya setini ve izinli Slim target canvasını listeliyor.
- [x] Field-level ve conditional validation kuralları yazıldı.
- [x] Failure Path en az duplicate, missing, unauthorized, archived/concurrency ve batch conflict içeriyor.
- [x] Authorization permission listesi, actor, canonical keys ve mevcut fallback sınırı açık.
- [x] Gateway routing kararı `değişiklik gereksiz`; endpoint allowlist açık.
- [x] Protected navigation istisnası dosya/section/guard/değişmez alanlar düzeyinde kesin.
- [x] Acceptance criteria test edilebilir.
- [x] Test expectations build/verifier/RESX/smoke kapsıyor.
- [x] Response-shape guard ve explicit exclusions yazıldı.
- [x] Kullanıcı bu hazırlık görevinde `ready-for-dev` hedefini açıkça yetkilendirdi.

## 19. Implementation Notes

- FU04 evidence reportu PASS'tir; Gateway campaign route çifti vardır ve `GET/POST/PUT/OPTIONS` ile sınırlıdır.
- Contract tam altı capability flag'i ve runtime vocabulary/reasonCodes/limitations bloklarını sağlar.
- Campaign list API'nin gerçek server filtreleri: CampaignType, CampaignStatus, BrandId, ProductId, SubjectId,
  IncludeArchived. Talep edilen diğer filtreler backend değişmeden tamamlanamaz; fake client filter yapılamaz.
- Target list gerçek server filtreleri: TargetType, TargetStatus, TargetSource, SnapshotBatchId, IncludeArchived.
- Snapshot history/batch list endpointi yoktur; yalnız mevcut target satırlarındaki SnapshotBatchId ve submit cevabı gösterilir.
- FU04 canonical permissions seed edilmemiştir; backend territory fallback kullanır. UI seed/grant yapmaz ve bu durum
  smoke/evidence verdictinde açıkça değerlendirilir.
- Çalışma ağacı çok sayıda mevcut değişiklik içeriyor. Orchestrator yalnız §5 paths üzerinde çalışmalı, mevcut kullanıcı
  değişikliklerini korumalı ve layout değişikliğini minimal tutmalıdır.
- Implementation başladığında pack status `in-progress`, test sonrası `review`, kabulden sonra `done` yapılır; kod ilk
  kez kazanacağı için `execution/registries/module-implementation-status.md` güncellemesi module-standard gereği
  implementation closeout kapsamındadır. Bu hazırlık task'ı registry write yapmaz.

### Response shape / UI data guard

UI şu alanları beklemez, göstermez ve bunlardan feature üretmez:

```text
visitPlanId
routePlanId
routeId
dueStatus
overdue
lastVisitDate
requiredVisitCount
periodType
frequencyPolicyId
segmentMembership
recommendationId
nextBestAction
workflowApprovalId
contentRenderUrl
consentRecordPayload
preferenceRecordPayload
```

Bu alanlar future/unknown olarak sakince ignore edilir. Consent/Preference record payload hiçbir DOM, view model,
log, toast veya detail paneline taşınmaz.

## 20. Follow-up Items

- **MOD-0165-FU-RBAC:** canonical `crm.campaign.*` keys seed/grant ve backend fallback kaldırma; FU05 kapsamı dışı.
- **MOD-0285 navigation migration:** Campaign page descriptor data-driven olduğunda hardcoded Campaign menu entry'nin
  kaldırılması ve no-double-menu smoke; FU05 kapsamı dışı.
- Backend destekli ek Campaign/Target filtreleri istenirse ayrı runtime FU authorization.
- Snapshot history/list veya preview istenirse ayrı API contract/runtime FU authorization.
- **MOD-0290-FU02 — Brand/Product Runtime + UI:** FU05 PASS sonrası önerilen bağımsız takip.
- MOD-0155 beklemede kalır; bu pack visit/route/frequency/due-overdue kapsamını açmaz.

### Orchestrator handoff

```text
@orchestrator execution/domains/commercial-suite/module-packs/MOD-0165-FU05-campaign-targeting-admin-ui.md

MOD-0165-FU05 — Campaign / Targeting Admin UI Implementation
```
