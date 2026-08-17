---
id: PSS-012
name: Business Reference Data Stewardship
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: TenantScopedEntity
status: approved
owner: module-pack-author
branch: feature/erp-project-integration
started: 2026-05-25
target: TBD
form_field_count: 0
approved_on: 2026-05-25
---

> **Onay (2026-05-25):** Kullanıcı "düzeltmeleri uygula" dedi ve status'u `approved` yaptı. Open Decisions çözümleri:
> 1. **Route:** `/api/v1/reference-data` **korunur** (değiştirilmez). → finding #11 kapsam dışı.
> 2. **Permission prefix:** `Platform.BusinessReferenceData.*` (mevcut + rule uyumlu).
> 3. **Governance prod default:** **`Disabled`** — mutasyon ilerler, audit'e "governance disabled" yazılır; mock yalnız Development/Test.
> 4. **`ReferenceDataEntities.cs`:** Kullanıcı "olduğu gibi bırak, hiçbir şey değiştirme" dedi → silinmez (ölü duplicate olsa da dokunulmaz).
> 5. **`ReferenceDataEntitiesv2.cs` rename:** Entity dosyalarına dokunma talimatı nedeniyle **yapılmaz** → finding #12 kapsam dışı.
> 6. **Index migration:** dev/MVP'de drop+recreate ile uygulanır.

# PSS-012 - Business Reference Data Stewardship

> **Bu pack bir refactor/standartlaştırma paketidir, greenfield değildir.** Kod `feature/erp-project-integration` dalında zaten yazılmış durumda (untracked). Geliştirici bu durumu **bilerek** ele almalı ve mevcut kodu standarda çekmelidir. Bu pack, `/docs/audits/` altındaki "BusinessReferenceData Standart Audit ve Düzeltme Planı" denetiminin module-pack karşılığıdır.

> **Frontend kapsam dışıdır.** Bu pack yalnız Domain/Application/Infrastructure/API katmanlarını kapsar. Razor view, JS, DataTable, layout, frontend localization ve frontend proxy değişikliği yapılmayacaktır. Frontend gerekiyorsa ayrı module pack/plan hazırlanır.

## Module Summary
Business Reference Data Stewardship, tenant-scoped iş referans verisi (reference data set / version / value / mapping / usage / import) yaşam döngüsünü yöneten governance modülüdür. Set tanımlama, sürüm oluşturma, validate → submit → approve → publish onay hattı, import preview/commit ve consumer lookup (values/hierarchy/published-values) yüzeylerini sahiplenir. Veri `Diten.Platform` servisinde host edilir ancak **PSS-011 platform lookup'larından (PSS-011) kavramsal ve teknik olarak ayrıdır**.

Bu modül PSS-011 ile karıştırılmamalıdır:
- **PSS-011** = Platform-owned, cross-tenant **system lookup** (`api/lookups`, `GlobalEntity`, read-only, currency/locale/timezone gibi paketleme enum'ları). Otoritesi PSS-011'dir ve **bu pack tarafından değiştirilmez**.
- **PSS-012** = Tenant-scoped **business reference data** stewardship (governed set/version/approval/publish/import/usage, `TenantScopedEntity`, mutating workflow). Bu pack'in konusudur.

## Current Implementation Snapshot (as of 2026-05-25)
Aşağıdaki kod halihazırda mevcuttur ve refactor edilecektir:

**Domain ([services/Diten.Platform/src/Diten.Platform.Domain/](services/Diten.Platform/src/Diten.Platform.Domain/)):**
- [Entities/ReferenceDataEntitiesv2.cs](services/Diten.Platform/src/Diten.Platform.Domain/Entities/ReferenceDataEntitiesv2.cs) — `BusinessReferenceData*` tiplerini içerir: `BusinessReferenceDataSet`, `BusinessReferenceDataVersion`, `BusinessReferenceDataValue`, `BusinessReferenceDataAttributeDefinition`, `BusinessReferenceDataMapping`, `BusinessReferenceDataValidationResult`, `BusinessReferenceDataIntegrationEvent`, `BusinessReferenceDataUsageRegistration`, `BusinessReferenceDataImportPreview*` ve ilgili enum'lar. Hepsi `TenantScopedEntity` tabanlı.
- [Entities/ReferenceDataEntities.cs](services/Diten.Platform/src/Diten.Platform.Domain/Entities/ReferenceDataEntities.cs) — ayrı dosya (untracked). Bu pack'te **disposition kararı bekliyor** (bkz. Open Decisions).
- [Repositories/IBusinessReferenceDataStewardshipRepository.cs](services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IBusinessReferenceDataStewardshipRepository.cs)

**Application ([Features/BusinessReferenceData/](services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/)):**
- `Commands/BusinessReferenceDataStewardshipCommands.cs` — **çoklu public type tek dosyada** (standart ihlali).
- `Queries/BusinessReferenceDataStewardshipQueries.cs` — **çoklu public type tek dosyada**.
- `Handlers/CommandHandlers/BusinessReferenceDataStewardshipCommandHandlers.cs`, `Handlers/QueryHandlers/BusinessReferenceDataStewardshipQueryHandlers.cs` — **çoklu handler tek dosyada**.
- `Validators/BusinessReferenceDataStewardshipValidators.cs`, `Models/BusinessReferenceDataStewardshipModels.cs`.
- `Services/` — governance, validation, publish, import, consumer-query, catalog-loader, model-mapper servisleri + CSV/JSON parser'lar.
- `Services/BusinessReferenceDataGovernanceAdapters.cs` — `MockBusinessReferenceDataWorkflowAdapter`, `DefaultBusinessReferenceDataEvidenceAdapter`, `NoOpBusinessReferenceDataGovernanceAuditAdapter`, `MockBusinessReferenceDataPostPublicationReviewHook`. **Mock/no-op davranış sessizce başarı döndürüyor.**

**Infrastructure:**
- [Persistence/Repositories/BusinessReferenceDataStewardshipRepository.cs](services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/BusinessReferenceDataStewardshipRepository.cs)
- [Persistence/Settings/BusinessReferenceDataCatalogLoadOptions.cs](services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Settings/BusinessReferenceDataCatalogLoadOptions.cs) — `Enabled=false` default, `RequiredSetCodes` listesi.
- [Persistence/Configurations/MongoDbIndexConfigurations.cs:550-617](services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs#L550-L617) — `business_reference_data_*` collection index'leri.

**API:**
- [Controllers/BusinessReferenceDataController.cs](services/Diten.Platform/src/Diten.Platform.API/Controllers/BusinessReferenceDataController.cs) — `[Route("api/v1/reference-data")]`, **fat controller**.
- `Models/BusinessReferenceData/` — request modelleri.
- [Services/BusinessReferenceData/BusinessReferenceDataCatalogLoadWorker.cs](services/Diten.Platform/src/Diten.Platform.API/Services/BusinessReferenceData/BusinessReferenceDataCatalogLoadWorker.cs) — `BackgroundService`.

**Tracked değişiklikler (5 dosya, +103 satır):**
- `Common/Response.cs` (+6) — `Ok(...)` ve ek `Fail(...)` overload'ları.
- `Application/DependencyInjection.cs` (+15) — mock/no-op adapter kayıtları (satır 60-64) + servis kayıtları.
- `Infrastructure/DependencyInjection.cs` (+3), `MongoDbIndexConfigurations.cs` (+77), `Program.cs` (+2, worker `AddHostedService`).

## Bilinen Standart Sapmaları (Düzeltilecek)
Denetimde doğrulanmış bulgular. Bu pack'in **Correction Scope**'u bunları kapatır:

| # | Önem | Bulgu | Doğrulama |
|---|---|---|---|
| 1 | BLOCKER | Module pack yok → bu pack onu kapatıyor (status `draft`). | `execution/` altında `BusinessReferenceData` grep boş. |
| 2 | HIGH | Fat controller: 15 `const` permission + private `HasPermission()` claim parser + manual `ResolveCorrelationId()` + per-action `try/catch` business error mapping + custom `OkResponse/CreatedResponse`. | [BusinessReferenceDataController.cs:23-53,1105-1144](services/Diten.Platform/src/Diten.Platform.API/Controllers/BusinessReferenceDataController.cs#L1105-L1144) |
| 3 | HIGH | `Response<T>.Ok(...)` ve ek `Fail(...)` overload'ları repo standardını genişletiyor. | [Response.cs:18-25](services/Diten.Platform/src/Diten.Platform.Application/Common/Response.cs#L18-L25) |
| 4 | HIGH | Permission merkezi `[HasPermission]` yerine controller içi const + claim parsing ile bypass ediliyor. | aynı controller |
| 5 | HIGH | CQRS handler'ları doğrudan model/null/exception dönüyor; controller hata mapping yapıyor (`Response<T>` envelope handler'da yok). | command/query handler dosyaları |
| 6 | HIGH | Governance audit no-op: `AuditBehavior` kayıtlı olmasına rağmen `NoOpBusinessReferenceDataGovernanceAuditAdapter` mutasyonları merkezi audit hattına bağlamıyor. | [GovernanceAdapters.cs:109-124](services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataGovernanceAdapters.cs#L109-L124) |
| 7 | HIGH | Mock workflow/post-publication adapter'ları production DI'a koşulsuz `AddScoped` ediliyor; eksik MOD-0023/MOD-0031 sessizce "başarılı" davranıyor. | [DependencyInjection.cs:60-64](services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs#L60-L64) |
| 8 | MEDIUM | Correlation ID controller içinde `X-Correlation-Id` header parse edilerek elle üretiliyor. | [BusinessReferenceDataController.cs:1132-1144](services/Diten.Platform/src/Diten.Platform.API/Controllers/BusinessReferenceDataController.cs#L1132-L1144) |
| 9 | MEDIUM | CQRS dosya ayrımı standarda uymuyor: command/query/handler/validator dosyalarında çoklu public type. | `*StewardshipCommands.cs` vb. |
| 10 | MEDIUM | Mongo unique index'lerde `IsDeleted` **key'e** eklenmiş; repo standardı `PartialFilterExpression IsDeleted=false`. | [MongoDbIndexConfigurations.cs:550-617](services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs#L550-L617) vs. satır 374/442/464/486/646. |
| 11 | MEDIUM | Route `/api/v1/reference-data` Platform route standardıyla birebir uyumlu değil. | controller `[Route]` |
| 12 | LOW | `ReferenceDataEntitiesv2.cs` dosya adı kafa karıştırıcı; tipler `BusinessReferenceData*`. | entity dosyası |

## Ownership and Boundaries
In scope:
- Tenant-scoped business reference data governance: set, version, value, attribute-definition, mapping, validation result, usage registration, import preview, integration event.
- Stewardship workflow: create/list/detail/update set; create/validate/submit/approve/publish version; import preview/commit; consumer values/hierarchy/published-values; usage register/deactivate.
- `Diten.Platform` Domain/Application/Infrastructure/API katmanlarında bu modüle ait kod.
- Merkezi standartlara hizalama: thin controller + `CreateActionResultInstance`, `Response<T>` envelope, `[HasPermission]`, `ICorrelationContext`, MOD-0021 audit, partial unique index.

Out of scope:
- **PSS-011 `api/lookups`, `LookupsController`, `Features/Lookups`** — davranış ve shape değişmeyecek.
- **Tüm frontend** (`frontend/Diten.Web/**`): Razor, JS, DataTable, layout, localization, proxy.
- **Gateway route** (`gateway/Diten.ApiGateway/.../ocelot.json`) — gerekiyorsa integration-agent + ayrı kapsam.
- `TenantCommercialSubscriptionsController` ve diğer modüllerin controller'ları (yalnız standart referansı).
- MOD-0023 Workflow Designer ve MOD-0031 Evidence Linking'in **gerçek implementasyonu** (bu modül onları tüketir, yazmaz).
- ERP MDM master-data modelleri (`master-data-management` domain'i / `Diten.MdmService`).

Boundary decision:
- PSS-012 host'u `Diten.Platform` olsa da kayıtları **tenant-scoped** (`TenantScopedEntity`, `TenantId` zorunlu); PSS-011 ise `GlobalEntity` ve `TenantId` taşımaz.
- PSS-012 bir ERP master-data SoR'u değildir. İleride `master-data-management` domain'i / `Diten.MdmService` oluşturulursa bu modülün ownership'i **ayrı bir karar/pack ile** o domain'e devredilebilir; o zamana kadar PSS domain'inde host edilir.
- PSS-012, PSS-011 lookup endpoint'lerini **kaynak olarak fork etmez** ve PSS-011'e yeni lookup key eklemez.

## Owned Objects
Backend API:
- Refactor edilecek controller: `services/Diten.Platform/src/Diten.Platform.API/Controllers/BusinessReferenceDataController.cs` → **thin controller**.
- Önerilen route ailesi (Open Decisions'da onay bekliyor): `/api/business-reference-data/...`
  - `GET|POST /sets`, `GET|PATCH /sets/{setId}`, `GET /sets/{setId}/versions`, `POST /sets/{setId}/versions`
  - `GET /versions/{versionId}`, `.../values`, `.../attribute-definitions`, `.../mappings` (GET/PUT)
  - `POST /versions/{versionId}/validate|submit|approve|publish`
  - `GET /sets/{setCode}/published-values|values|hierarchy` (consumer)
  - `POST /usage-registrations`, `GET /usage-registrations`, `DELETE /usage-registrations/{id}`
  - `POST /imports/preview`, `POST /imports/{previewId}/commit`
  - Fixture endpoint'leri (`fixtures/evidence-required:provision|retire`) yalnız non-production fixture mode'da açık kalır.

Application:
- `Features/BusinessReferenceData/Commands|Queries|Handlers|Validators|Models` — **her command/query/handler/validator ayrı dosyaya** bölünür; DTO/model tek dosyada kalabilir.
- Tüm command/query'ler `IRequest<Response<T>>` döner; handler'lar `Response<T>.Success/Fail` üretir.
- Mutating command'ler `IAuditableCommand` + `IAuditMetadataProvider` implement eder.
- Governance/validation/publish/import/consumer servisleri ve adapter arabirimleri.

Domain/persistence:
- `BusinessReferenceData*` entity ve enum'ları (`TenantScopedEntity` tabanlı).
- `IBusinessReferenceDataStewardshipRepository` + Infrastructure implementasyonu (TenantId + soft delete zorunlu).
- MongoDB collection'ları: `business_reference_data_sets`, `business_reference_data_versions`, `business_reference_data_usage_registrations`, `business_reference_data_import_previews`, `business_reference_data_integration_events`.

## Entity Fields
Base type decision: `TenantScopedEntity`.

Justification:
- Bu kayıtlar tenant-owned business reference data'dır; `TenantId` zorunludur ve soft delete (`IsDeleted`) uygulanır.
- PSS-011'in `GlobalEntity` kararı bu modüle uygulanmaz; iki modül entity tabanı bilinçli olarak farklıdır.

Mevcut aggregate (özet — tam alanlar [ReferenceDataEntitiesv2.cs](services/Diten.Platform/src/Diten.Platform.Domain/Entities/ReferenceDataEntitiesv2.cs)):

| Entity | Anahtar alanlar | Not |
|---|---|---|
| `BusinessReferenceDataSet` | `SetCode`, `Name`, `ScopeType`, `Status`, `ActiveDraftVersionId`, `PublishedVersionId`, `RowVersion`, usage sayaçları | Tenant başına `SetCode` unique olmalı (aktif kayıt). |
| `BusinessReferenceDataVersion` | `VersionNumber`, `Status`, `ConcurrencyToken`, governance/approval state, evidence alanları, `Values/AttributeDefinitions/Mappings` embedded | Set başına `VersionNumber` unique. Optimistic concurrency `ConcurrencyToken`/`RowVersion`. |
| `BusinessReferenceDataValue` | `ValueCode`, `DisplayName`, `IsDeprecated`, `ParentValueCode`, `SortOrder`, `Attributes` | Version içine embedded; hierarchy için `ParentValueCode`. |
| `BusinessReferenceDataUsageRegistration` | `SetCode`, `ConsumerModule`, `ConsumerName`, `ScopeKey`, `VersionPin`, `Criticality`, `IsActive` | Consumer başına unique (aktif). |
| `BusinessReferenceDataImportPreview` | `TargetDraftVersionId`, `Format`, `Rows`, sayım alanları, `CommittedAt`, `CommitIdempotencyKey` | Commit idempotent. |
| `BusinessReferenceDataIntegrationEvent` | `EventName`, `IdempotencyKey`, `PayloadJson` | Publish/commit event outbox; `(version, event, idempotencyKey)` unique. |

Create/edit form user field count: 0. Backend/API governance modülü; bu pack'te CRUD form yoktur.

Golden Reference decision: `golden_reference: none`. DataTable/Razor UI bu pack'te yoktur. Future Platform Admin yönetim UI'ı onaylanırsa ayrı pack `shell` ve Slim/Compact kararını gerçek alan sayısıyla verir.

## Repo Scope
Allowed documentation scope:
- `execution/domains/platform-shared-services/module-packs/PSS-012-business-reference-data-stewardship.md`.
- `docs/audits/**` denetim raporunun korunması (silinmez).

Allowed backend scope:
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/ReferenceDataEntitiesv2.cs` (+ önerilen rename hedefi `BusinessReferenceDataEntities.cs`).
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IBusinessReferenceDataStewardshipRepository.cs`.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/**`.
- `services/Diten.Platform/src/Diten.Platform.Application/Common/Response.cs` — yalnız bu pack kapsamında eklenen `Ok`/ekstra `Fail` overload'larını **geri almak** için.
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` — adapter mode/DI düzeltmesi.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/BusinessReferenceDataStewardshipRepository.cs`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs` — yalnız `business_reference_data_*` index bloğu (satır ~550-617).
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Settings/BusinessReferenceDataCatalogLoadOptions.cs`, `Infrastructure/DependencyInjection.cs`.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/BusinessReferenceDataController.cs`, `Models/BusinessReferenceData/**`, `Services/BusinessReferenceData/**`, `Program.cs` (worker kaydı).

## Protected Paths
- `.antigravity/**`.
- **PSS-011 yüzeyi:** `Controllers/LookupsController.cs`, `Features/Lookups/**`, `api/lookups` davranışı.
- **Tüm frontend:** `frontend/Diten.Web/**` (Razor, JS, DataTable, layout, Resources, proxy).
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent / explicit route approval olmadan değişmez.
- `services/Diten.AuthService/**`, `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**`.
- `execution/domains/master-data-management/**`.
- `Controllers/TenantCommercialSubscriptionsController.cs` ve diğer modül controller'ları (yalnız referans).
- `Response.cs` içindeki diğer modüllerin kullandığı `Success(...)`/`Fail(...)` çekirdek imzaları (yalnız bu pack'te eklenen overload'lar geri alınır; çekirdek kontrat korunur).

## Dependencies
- `Diten.Platform` API/Application/Domain/Infrastructure projeleri.
- `Response<T>` + `CustomBaseController.CreateActionResultInstance(...)` standardı.
- Merkezi `HasPermissionAttribute` ve `Platform.X.Y` permission modeli (security-jwt rule).
- `CorrelationIdMiddleware` / `ICorrelationContext` (MOD-0041 observability seam).
- MOD-0021 Audit Trail: `AuditBehavior`, audit outbox / `IAuditService` (meta-audit hattı).
- MOD-0023 Workflow Designer (approvals MVP) — **planlanmış, henüz yok**; adapter ile tüketilir.
- MOD-0031 Evidence Linking — **planlanmış, henüz yok**; adapter ile tüketilir.
- MongoDB tek instance, tenant logical isolation; `mongo-indexing` rule.
- Platform localization yalnız `en`/`tr` (görünür string eklenirse — bu pack'te beklenmez).

## Runtime Constraints
- Frontend Platform servis portuna doğrudan gitmez; bu pack zaten frontend'e dokunmaz.
- Tüm read query'leri `IsDeleted=false` ve `TenantId` filtreler.
- Mutating endpoint'ler optimistic concurrency (`ConcurrencyToken`/`RowVersion`/`If-Match`) korur.
- Publish ve import commit **idempotent** kalır (`Idempotency-Key` zorunlu).
- Governance adapter'ları production'da **sessiz mock başarı döndüremez** (bkz. Audit & Governance Contract).
- Catalog load worker default `Enabled=false`; explicit config ve required-set/TenantId/CatalogPath doğrulaması olmadan veri mutate etmez.

## Layout & Shell Contract
- `shell: none`. Backend/API-only modül.
- `Views/...` klasörü, navigation entry, Razor layout, Ctrl+K registry kaydı bu pack'te oluşturulmaz.
- Gelecekte yönetim UI'ı onaylanırsa ayrı pack `shell: platform-admin` veya tenant shell kararını açıkça verir.

## Backend File Convention
CQRS dosya ayrımı (standart):
```text
Features/BusinessReferenceData/
├── Commands/                 # her command ayrı dosya (CreateBusinessReferenceDataSetCommand.cs, ...)
├── Queries/                  # her query ayrı dosya
├── Handlers/
│   ├── CommandHandlers/      # her handler ayrı dosya
│   └── QueryHandlers/        # her handler ayrı dosya
├── Validators/               # her validator ayrı dosya
├── Models/                   # DTO/model tek dosyada kalabilir
└── Services/                 # arabirim + implementasyon ayrımı korunur
```
Rules:
- Command record'ları `Command`, query'ler `Query`, handler'lar `Handler`, validator'lar `Validator` ile biter.
- Tüm command/query `IRequest<Response<T>>` döner; handler `Response<T>.Success/Fail` üretir, **exception ile business error sinyallemez**.
- Controller thin'dir: yalnız MediatR'a delegate eder ve `CreateActionResultInstance(response)` döner; per-action business `try/catch` mapping yapmaz.
- Magic string yerine domain enum/constant; HTTP status'u handler'ın `Response<T>.StatusCode`'u taşır.

## Authorization Convention
- Controller seviyesi: `[Authorize]` (ERP/tenant kullanım hedefi nedeniyle **zorunlu `PlatformActor` policy kullanılmaz**).
- Action seviyesi: merkezi `[HasPermission("Platform.BusinessReferenceData...")]`.
- Controller içi `const` permission + private `HasPermission()` claim parser **kaldırılır**.
- Platform/admin yönetim yüzeyi gerekirse ayrı controller/route/policy ile tasarlanır.

> **Bilinçli karar:** Denetim planı bare `BusinessReferenceData.*` önermişti. Bu pack `Platform.BusinessReferenceData.*` prefix'ini korur. Gerekçe: BusinessReferenceData verisi ERP modüllerinde tüketilir; ancak stewardship, governance, approval, publish, audit ve catalog lifecycle Platform servisinin sahipliğindedir. Bu nedenle permission namespace'i Platform-owned stewardship yüzeyini ifade eder, ERP consumer kullanımını PSS-011 system lookup yüzeyiyle karıştırmaz.

Permission matrix (MVP):

| Permission | Endpoint(ler) |
|---|---|
| `Platform.BusinessReferenceData.Read` | set list/detail/versions, version detail/values/attribute-defs/mappings, usage list |
| `Platform.BusinessReferenceData.Create` | POST /sets |
| `Platform.BusinessReferenceData.Update` | PATCH /sets, PUT version values/attribute-defs/mappings |
| `Platform.BusinessReferenceData.Version.Create` | POST /sets/{id}/versions |
| `Platform.BusinessReferenceData.Version.Validate` | POST /versions/{id}/validate |
| `Platform.BusinessReferenceData.Version.Submit` | POST /versions/{id}/submit |
| `Platform.BusinessReferenceData.Version.Approve` | POST /versions/{id}/approve |
| `Platform.BusinessReferenceData.Version.Publish` | POST /versions/{id}/publish (normal publish; `OverrideAction=true` reddedilir) |
| `Platform.BusinessReferenceData.Version.PublishOverride` | POST /versions/{id}/publish-override (override publish; `OverrideReason` zorunlu, audit metadata override/actor/correlation/affected version taşır) |
| `Platform.BusinessReferenceData.Consumer.Read` | published-values, values, hierarchy |
| `Platform.BusinessReferenceData.Usage.Register` | POST/DELETE usage-registrations |
| `Platform.BusinessReferenceData.Import.Preview` | POST /imports/preview |
| `Platform.BusinessReferenceData.Import.Commit` | POST /imports/{id}/commit |
| `Platform.BusinessReferenceData.Fixture.Manage` | fixture provision/retire (non-prod mode) |

## Audit & Governance Contract
Bu pack'in en kritik düzeltmesi. Mock/no-op sessiz başarı **production riski** olarak ele alınır.

Audit:
- Mutating command'ler (`Create/Patch Set`, `Create/Replace Version*`, `Submit/Approve/Publish`, `Import Commit`, `Usage Register/Deactivate`) `IAuditableCommand` + `IAuditMetadataProvider` implement eder.
- Governance audit, `NoOpBusinessReferenceDataGovernanceAuditAdapter` yerine MOD-0021 merkezi audit hattına (`IAuditService` / audit outbox / meta-audit) bağlanır.
- No-op adapter **production DI'dan çıkar**.

Governance adapter mode'ları (workflow / evidence / post-publication):
- Eksik external bağımlılık (MOD-0023 / MOD-0031) **sessizce "başarılı" davranamaz**.
- Production default: `Disabled` veya `FailClosed` — açık config/feature mode ile yönetilir.
- Mock adapter'lar yalnız `Development`/`Test` veya **explicit config flag** ile kayıtlanır.
- Mode matrisi (config ile seçilir):

| Mode | Davranış |
|---|---|
| `Disabled` | Workflow/evidence adımı atlanır; ilgili mutasyon **açıkça** "governance disabled" olarak işaretlenir (silent success değil). |
| `FailClosed` (prod default) | Bağımlılık yoksa submit/approve/publish **reddedilir** (controlled 503/409), veri yarı-onaylı kalmaz. |
| `Mock` (yalnız non-prod) | Geliştirme/test için stub davranış; production'da seçilemez. |
| `Live` | Gerçek MOD-0023/MOD-0031 entegrasyonu (bunlar implement edilince). |

## Gateway / API Routing Decision
> **Bilinçli karar:** Route bu pack'te **`/api/v1/reference-data` olarak korunur**. Bu karar mevcut taşınan BusinessReferenceData kontratını kırmamak için alınmıştır; API versioning standardizasyonu MOD-0032 veya ayrı gateway/API hardening işiyle ele alınacaktır.
- Gateway route ekleme/değiştirme `ocelot.json`'da **bu pack tarafından yapılmaz**; protected path yalnız integration-agent veya açık onaylı ayrı görevle değişir.
- Gateway erişimi gerekirse integration-agent şu eşleşmeleri ekler: upstream `/api/v1/reference-data` ve `/api/v1/reference-data/{everything}`; downstream Platform service `5057` üzerinde aynı path; method listesi yalnız kullanılan HTTP verb'lere göre daraltılır.
- Gateway tenant middleware davranışı ayrıca doğrulanır: ERP tenant consumer akışında `tenant_user` + `X-Tenant-Id` gerekiyorsa route tenant endpoint gibi davranır, Platform admin-only route gibi işaretlenmez.

## Persistence / Index Decision
- Collection adları `business_reference_data_*` korunur.
- Unique index'lerden `IsDeleted` **key'den çıkarılır**, repo standardı `PartialFilterExpression IsDeleted=false` uygulanır:
  - `ux_business_reference_data_sets_*` → `(TenantId, SetCode)` unique + partial `IsDeleted=false`.
  - `ux_business_reference_data_versions_*` → `(TenantId, SetId, VersionNumber)` unique + partial `IsDeleted=false`.
  - `ux_business_reference_data_usage_*` → consumer key unique + partial `IsDeleted=false`.
  - `ux_business_reference_data_events_*` → `(version, event, idempotencyKey)` unique + partial `IsDeleted=false`.
- Non-unique read index'leri (`ix_*`) `IsDeleted`'i filtre/sort kolonu olarak tutabilir; bu kural yalnız **unique** index'ler içindir.
- Index rename eski `*_deleted` index'lerinin migration/drop davranışını gerektirir; deployment notunda belirtilir.

## Domain Invariants
- Her kayıt `TenantId` taşır; tenant filtresi olmadan read/write yapılmaz.
- `IsDeleted=false` olmayan kayıtlar default read'de dönmez.
- Bir set için aynı anda yalnız bir aktif draft version olabilir (`active_draft_exists` invariant'ı korunur).
- Published version **immutable**; değişiklik yeni version gerektirir.
- Submit/approve/publish hattı SoD (`sod_submitter_cannot_approve`) ve evidence/approval ön koşullarını korur.
- Publish ve import commit aynı `Idempotency-Key` ile tekrar çağrılınca yeni yan etki üretmez.
- Consumer lookup retired set için yeni seçim döndürmez (`reference_data_set_retired`).
- Business error'lar `Response<T>.Fail` ile döner; controller'da `catch` ile HTTP'ye map edilmez.

## Forbidden Operations
- Yeni `Response<T>` overload'ı eklemek (mevcut `Success/Fail` çekirdeği dışında); bu pack eklenenleri geri alır.
- Controller içinde permission claim parsing veya correlation header parsing yapmak.
- Handler'dan business error için exception fırlatıp controller'da map etmek.
- Production DI'a mock/no-op governance adapter bağlamak.
- Unique index key'ine `IsDeleted` koymak.
- PSS-011 lookup yüzeyini değiştirmek veya PSS-011'e yeni key eklemek.
- Frontend dosyası, gateway `ocelot.json`, başka modül controller'ı değiştirmek.
- `/api/v1/...` version prefix'ini kalıcılaştırmak.

## Acceptance Criteria
- [x] Controller thin: tüm action'lar `Task<IActionResult>` döner ve `CreateActionResultInstance(response)` kullanır; `const` permission/`HasPermission()`/manual correlation/per-action business `try/catch` kaldırılmıştır.
- [x] Her action `[HasPermission("Platform.BusinessReferenceData...")]` ile korunur; controller `[Authorize]`.
- [x] Tüm command/query `IRequest<Response<T>>` döner; handler `Response<T>.Success/Fail` üretir.
- [x] `Response<T>.Ok(...)` ve bu pack'te eklenen ekstra `Fail(...)` overload'ları kaldırılmış; çekirdek `Success/Fail` korunmuştur.
- [x] Correlation `ICorrelationContext` üzerinden çözülür; controller header parse etmez.
- [x] Mutating command'ler audit metadata üretir; governance audit MOD-0021 hattına bağlıdır; `NoOp` adapter production DI'da yoktur.
- [x] Mock workflow/evidence/post-publication adapter'ları yalnız non-prod/explicit mode'da; production default `Disabled`/`FailClosed`.
- [x] CQRS dosya ayrımı: her command/query/handler/validator ayrı dosya; DTO/model tek dosyada.
- [x] `business_reference_data_*` unique index'leri partial `IsDeleted=false` kullanır; key'de `IsDeleted` yoktur.
- [x] Route kararı uygulanmış: bu pack için `/api/v1/reference-data` korunur; gateway route gerekiyorsa integration-agent ayrı görev yapar.
- [x] `ReferenceDataEntitiesv2.cs` rename bilinçli olarak yapılmaz; tip adları `BusinessReferenceData*` kalır ve entity dosyaları kullanıcı kararıyla korunur.
- [x] Catalog load worker default `Enabled=false`; required-set/TenantId/CatalogPath doğrulaması olmadan mutate etmez.
- [ ] **PSS-011 regression yok:** `GET /api/lookups/currencies|timezones|module-catalog/domains` response shape ve auth davranışı değişmemiştir.
- [x] Frontend, gateway `ocelot.json` ve diğer modül controller'ları değişmemiştir.

## Test Expectations
Build (sırayla):
- `dotnet build services/Diten.Platform/src/Diten.Platform.Domain/Diten.Platform.Domain.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.Application/Diten.Platform.Application.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.Infrastructure/Diten.Platform.Infrastructure.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`

Regression (PSS-011 — değişmemeli):
- `GET /api/lookups/currencies`, `GET /api/lookups/timezones`, `GET /api/lookups/module-catalog/domains`.

BusinessReferenceData smoke:
- set list/create/detail/update
- version create/validate/submit/approve/publish
- import preview/commit
- consumer values/hierarchy/published-values lookup
- usage register/deactivate

Unit/integration (acceptance'a bağlı):
- Controller action'ları `CreateActionResultInstance` kullanıyor; `[HasPermission]` action bazında var.
- Handler'lar `Response<T>` dönüyor; business error exception ile sinyallenmiyor.
- Audit metadata mutating command'lerde üretiliyor; NoOp adapter DI'da yok.
- Production config'te governance adapter mode `Disabled`/`FailClosed`; mock yok.
- Mongo partial unique index tenant + active record uniqueness sağlıyor; soft-deleted kayıt unique çakışması üretmiyor.
- Idempotent publish/commit ikinci çağrıda yeni yan etki üretmiyor.

## Ready-for-dev Checklist
- [x] User `PSS-012` ID ve domain (`platform-shared-services`) yerleşimini onaylar.
- [x] User route kararını onaylar: `/api/v1/reference-data` bu pack'te korunur.
- [x] User permission prefix kararını onaylar: `Platform.BusinessReferenceData.*` (plandaki bare `BusinessReferenceData.*` yerine).
- [x] User `entity_base: TenantScopedEntity` ve PSS-011 ile ayrık olduğunu onaylar.
- [ ] User governance adapter production default'unu onaylar: `Disabled` mı `FailClosed` mı?
- [x] User `ReferenceDataEntities.cs` / `ReferenceDataEntitiesv2.cs` disposition'ını netleştirir: bu turda dokunulmaz.
- [x] User frontend ve gateway'in kapsam dışı olduğunu onaylar.
- [x] Status `approved`; PSS-012 kod düzeltmeleri bu pack üzerinden uygulanmıştır.

## Open Decisions
1. **Governance prod default:** `Disabled` uygulandı; gerçek `FailClosed` / `Live` MOD-0023/MOD-0031 entegrasyonu ile follow-up.
2. **`ReferenceDataEntities.cs` disposition:** Bu ikinci dosya kullanıcı kararıyla şimdilik dokunulmadan kalır.
3. **Index migration:** Eski `*_deleted` unique index'leri partial'a geçerken drop/recreate deployment adımı gerekiyor; sırası onaylanmalı.

## Uygulama Durumu (2026-05-25)
Aşağıdaki düzeltmeler uygulandı ve `Diten.Platform` Domain/Application/Infrastructure/API projeleri **`dotnet build` ile yeşil** doğrulandı:

- **#10 Mongo partial unique index** — `business_reference_data_*` unique index'leri `IsDeleted` key'den çıkarıldı, `PartialFilterExpression IsDeleted=false`'a çevrildi. Index adları `_deleted` suffix'i kaldırıldı (eski index'ler deployment'ta drop edilecek).
- **#6/#7 Governance/audit/DI** — `NoOpBusinessReferenceDataGovernanceAuditAdapter` production DI'dan çıkarıldı; `AuditServiceBusinessReferenceDataGovernanceAuditAdapter` (gerçek MOD-0021 `IAuditService`, `AuditCategory.ReferenceData`) eklendi. Governance mode resolver: prod default **Disabled**, mock yalnız Development/Local/Test. `DisabledBusinessReferenceDataWorkflowAdapter` + `DisabledBusinessReferenceDataPostPublicationReviewHook` eklendi.
- **#5/#6 CQRS Response<T> + audit** — tüm command/query `IRequest<Response<T>>` döner; handler'lar `Response<T>.Success/Fail` üretir. Mutating command'ler `IAuditableCommand`+`IAuditMetadataProvider` implement eder. Yeni `IBusinessReferenceDataRequest` marker + innermost `BusinessReferenceDataExceptionBehavior` coded exception'ları doğru HTTP status'a map eder (controller'daki eski mapping korunarak).
- **#2/#3/#4/#8 thin controller + envelope** — `BusinessReferenceDataController` thin'leştirildi: `const` permission/`HasPermission()`/manual correlation/`try-catch`/`OkResponse`/`CreatedResponse` kaldırıldı; `[HasPermission("Platform.BusinessReferenceData...")]` + `CreateActionResultInstance` + `ICorrelationContext` kullanılıyor. `Response<T>.Ok(...)` ve `Fail(string,string,int)` overload'ları geri alındı.
- **Worker** — `RequiredSetCodes` boşsa mutate etmeme guard'ı eklendi (Enabled=false default ve TenantId/CatalogPath guard'ları zaten vardı).

Bilinçli olarak **uygulanmayan / sapan** maddeler (kullanıcı kararı veya scope):
- **#11 route** — `/api/v1/reference-data` korundu (değiştirilmedi); gateway route değişikliği protected path olduğu için integration-agent follow-up'a bırakıldı.
- **#12 rename + `ReferenceDataEntities.cs`** — entity dosyalarına dokunulmadı (kullanıcı talimatı). `ReferenceDataEntities.cs` ölü duplicate olarak repoda kaldı.
- **#9 CQRS dosya ayrımı (her tip ayrı dosya)** — command/query/handler/validator aggregate dosyaları ayrıldı. Her command, query, command handler, query handler ve validator kendi dosyasındadır.
- **Publish-override permission** — normal publish endpoint'i `OverrideAction=true` isteklerini `403 publish_override_permission_required` ile reddeder. Yeni `POST /versions/{id}/publish-override` endpoint'i `Platform.BusinessReferenceData.Version.PublishOverride` permission ister; override reason validator ile zorunludur ve audit metadata override/actor/correlation/affected version bilgisini taşır.
- **Governance FailClosed/Live mode** — adapter seçimi yalnız Mock vs Disabled wire edildi; FailClosed/Live `Disabled` adapter'a düşer. Gerçek FailClosed/Live MOD-0023/MOD-0031 ile follow-up.

## Follow-up Items
- MOD-0023 Workflow Designer ve MOD-0031 Evidence Linking implement edilince adapter mode `Live`'a alınır; ayrı entegrasyon görevi.
- Frontend yönetim/consumer UI gerekirse ayrı module pack (`shell` + Slim/Compact + golden reference) hazırlanır.
- Gateway route gerekirse integration-agent ile `/api/v1/reference-data` ve `/api/v1/reference-data/{everything}` eklenir.
- `master-data-management` domain'i / `Diten.MdmService` oluşursa ownership devri ayrı kararla değerlendirilir.
