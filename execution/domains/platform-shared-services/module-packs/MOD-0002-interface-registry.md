---
id: MOD-0002
name: Interface Registry
domain: platform-shared-services
service: Diten.Platform
status: approved
owner: module-pack-author
branch: feature/pss/mod-0033-consumer-quota-model
started: 2026-05-13
target: 2026-06-30
wave: W3-E
priority: medium
ui_pattern: review-confirm-workbench
datatable: false
form_field_count: 0
golden_reference: manifest-import-diff-review-confirm-active-snapshot
---

# MOD-0002 - Interface Registry

## Module Summary
Interface Registry, ERP-vNext platformundaki API/interface metadata'sinin merkezi kayit ve review omurgasidir. Moduller kendi interface metadata'sini manifest/attribute sozlesmesi ile uretir; Platform Interface Registry bu metadata'yi alir, Module Catalog ile owner module dogrulamasi yapar, diff uretir ve Platform Admin'e review/confirm akisi sunar.

Bu module pack kalici execution sozlesmesidir. Kod gelistirmesi yalnizca frontmatter `status` degeri `approved` veya `ready-for-dev` iken baslar; `draft` durumunda backend, frontend, gateway veya servis dosyasi degistirilmez.

Master Plan'daki `MOD-0002 Interface Registry` maddesi API endpoint sahipligi, version, consumer ve compatibility metadata kaydini hedefler. Bu pack, mevcut repo yapisina gore runtime sahibinin `platform-shared-services` / `Diten.Platform` oldugunu belirler.

## Domain Decision
Interface Registry'nin dogru runtime sahibi Platform Shared Services'tir.

Gerekce:
- Canli platform katalog omurgasi `Diten.Platform` icindedir.
- Module identity icin System of Record mevcut `Module Catalog` / `ModuleCatalogItem.ModuleCode` sozlesmesidir.
- Interface Registry, "modul kimdir?" sorusunu tekrar sahiplenmez; "hangi interface'leri sunuyor/tuketiyor, hangi consumer'lar bagli, hangi versiyon/deprecation durumu var?" sorularinin SoR'u olur.
- Registry metadata'si domainler arasi yatay bir governance yetenegidir; MDM ana veri modeli degildir.
- Frontend ve servis entegrasyonu Gateway uzerinden Platform Admin yuzeyinde yonetilir.

### Ownership Conflict Note
Mevcut `execution/domains/master-data-management/module-packs/MDM-002-interface-registry.md` dosyasi `draft` durumundadir ve servis karsiligi olmayan bootstrap/backlog kaydi gibi gorunmektedir.

Karar:
- `MDM-002-interface-registry.md` bu pack tarafindan degistirilmez.
- MDM-002, Interface Registry'nin Platform tarafinda saglayacagi merkezi registry'ye ileride metadata ureten/tuketen domain referansi olarak ele alinmalidir.
- Runtime SoR, API contract, manifest import, diff/review/confirm ve active snapshot sahipligi bu `MOD-0002` Platform pack'indedir.
- Uygulama baslamadan once kullanici MDM-002'nin `blocked`, `superseded-by MOD-0002` veya backlog reference olarak kalmasina karar vermelidir.

## Ownership and Boundaries
### System of Record
- Interface definition metadata.
- Interface endpoint metadata.
- Provider module / owner module relationship.
- Consumer dependency metadata.
- Manifest import contract and idempotent sync state.
- Discovery diff state: `new`, `changed`, `deprecated`, `missing`, `unchanged`, `rejected`.
- Review/confirm state model.
- Confirmed active interface snapshot/read model.
- Lightweight abstraction package contract for attributes, enums, manifests and manifest providers.

### In Scope
- Platform Interface Registry feature under `Diten.Platform`.
- Owner module validation through Module Catalog; `ownerModuleCode` must resolve to a known module.
- Manifest import API contract for service/module-produced metadata.
- Diff generation between incoming manifest and latest confirmed active snapshot.
- Admin review/confirm/reject flow for discovered changes.
- Read-only registry browser for confirmed interfaces and consumer dependencies.
- Deprecation metadata and lifecycle status tracking.
- Compatibility metadata fields required by Master Plan, without full breaking-change CI enforcement in this phase.
- Dependency-light abstraction package:
  - Preferred project/path: `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.InterfaceRegistry.Abstractions`
  - Preferred namespace/assembly: `Diten.BuildingBlocks.InterfaceRegistry.Abstractions`
  - Fallback only if repo structure blocks a Building Blocks project split: `services/Diten.Platform.Common/src/Diten.Platform.InterfaceRegistry.Abstractions`
  - Fallback rationale must be documented if used.
  - Package must remain dependency-light and must not bring MongoDB, ASP.NET Core, Ocelot, persistence, Platform API or service runtime dependencies into ERP modules.

### Out of Scope
- Manual create/edit CRUD for endpoint path, method or owner module.
- Reflection scanner implementation.
- ASP.NET Core endpoint inspection adapter.
- OpenAPI enrichment/import adapter.
- Gateway route auto-validation.
- Breaking-change CI enforcement.
- Data Contract Registry ownership (`MOD-0003`).
- Event schema registry ownership (`MOD-0039` / `MOD-0003`).
- Runtime API authorization enforcement (`MOD-0018`).
- API Gateway hardening (`MOD-0032`).
- ERP module code changes to add attributes.
- Public external developer portal.

## MVP Implementation Scope
This pack is ready for development for the MVP described here.

MVP includes:
- Dependency-light abstraction package for attributes, enums, manifest DTOs and provider interface.
- Platform manifest import endpoint.
- Module Catalog `ownerModuleCode` validation.
- Idempotent import and diff generation.
- Discovery batch and diff review persistence.
- Admin review/confirm/reject UI.
- Confirmed active snapshot read model.
- Separate Mongo collection for confirmed active snapshots: `platform_interface_active_snapshots`.
- Local review metadata for all review decisions.

MVP excludes:
- ERP module attribute adoption.
- Reflection scanner implementation.
- OpenAPI import/enrichment adapter.
- Gateway/OpenAPI automatic validation.
- CI breaking-change enforcement.
- Public developer portal.
- Manual endpoint create/edit CRUD.

## Owned Objects
### Abstractions
- `InterfaceRegistryAttribute`
- `ConsumesInterfaceAttribute`
- `InterfaceStability`
- `InterfaceVisibility`
- `InterfaceLifecycleStatus`
- `InterfaceChangeType`
- `InterfaceReviewDecision`
- `InterfaceDefinitionManifest`
- `InterfaceEndpointManifest`
- `InterfaceConsumerManifest`
- `InterfaceManifestDocument`
- `IInterfaceManifestProvider`

Preferred package:
- Project/path: `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.InterfaceRegistry.Abstractions`
- Namespace/assembly: `Diten.BuildingBlocks.InterfaceRegistry.Abstractions`

Allowed dependencies:
- .NET base class library only.

Forbidden dependencies:
- MongoDB / `MongoDB.Driver`
- ASP.NET Core / MVC endpoint metadata
- Ocelot
- Persistence repositories
- `Diten.Platform.API`
- `Diten.Platform.Infrastructure`
- Any service-specific runtime project

Fallback:
- If current solution conventions make the Building Blocks location unavailable, use `services/Diten.Platform.Common/src/Diten.Platform.InterfaceRegistry.Abstractions` only as a dependency-light subpackage.
- The implementation must explain why the fallback was used and must keep the same forbidden dependency list.

### Domain / Persistence
- `InterfaceDefinition` global aggregate.
- `InterfaceEndpoint` embedded or child metadata owned by an interface definition snapshot.
- `InterfaceConsumerDependency` embedded or child metadata owned by an interface definition snapshot.
- `InterfaceDiscoveryBatch` import batch aggregate.
- `InterfaceDiscoveryDiffItem` review item aggregate or embedded collection under a batch, depending on implementation discovery.
- `InterfaceActiveSnapshot` read model or versioned snapshot contract.
- Mongo collections:
  - `platform_interface_definitions`
  - `platform_interface_discovery_batches`
  - `platform_interface_active_snapshots`
- Unique indexes:
  - `InterfaceCode + Version` for confirmed interface definitions.
  - `OwnerModuleCode + EndpointMethod + EndpointPath + Version` for endpoint metadata, if endpoint-level uniqueness is required.
  - `BatchId + InterfaceCode + EndpointKey` for diff items.

### Application Commands
- `ImportInterfaceManifestRequest`
- `ConfirmInterfaceDiscoveryBatchRequest`
- `RejectInterfaceDiscoveryBatchRequest`
- `ConfirmInterfaceDiffItemRequest`
- `RejectInterfaceDiffItemRequest`
- `DeprecateInterfaceRequest`

### Application Queries
- `GetInterfaceDefinitionsRequest`
- `GetInterfaceDefinitionByCodeRequest`
- `GetInterfaceDefinitionSnapshotRequest`
- `GetInterfaceDiscoveryBatchesRequest`
- `GetInterfaceDiscoveryBatchByIdRequest`
- `GetInterfaceDiscoveryDiffItemsRequest`
- `GetInterfaceConsumersRequest`
- `GetInterfaceProvidersRequest`

### DTO / Contracts
- `InterfaceDefinitionDto`
- `InterfaceEndpointDto`
- `InterfaceConsumerDependencyDto`
- `InterfaceManifestImportResultDto`
- `InterfaceDiscoveryBatchDto`
- `InterfaceDiscoveryDiffItemDto`
- `InterfaceReviewDecisionDto`
- `InterfaceActiveSnapshotDto`
- `InterfaceRegistryFilterRequest`
- `InterfaceDiffFilterRequest`

### API Endpoints
All endpoints are Platform-owned and Gateway-backed.

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/platform/interface-registry/interfaces` | Confirmed interface catalog list. |
| GET | `/api/platform/interface-registry/interfaces/{interfaceCode}` | Confirmed interface detail. |
| GET | `/api/platform/interface-registry/interfaces/{interfaceCode}/snapshot` | Latest active snapshot. |
| GET | `/api/platform/interface-registry/interfaces/{interfaceCode}/consumers` | Consumer dependency list. |
| POST | `/api/platform/interface-registry/manifests/import` | Import a service/module manifest and produce diff. |
| GET | `/api/platform/interface-registry/discovery-batches` | Discovery/import batch list. |
| GET | `/api/platform/interface-registry/discovery-batches/{batchId}` | Discovery batch detail. |
| GET | `/api/platform/interface-registry/discovery-batches/{batchId}/diffs` | Diff items for review. |
| POST | `/api/platform/interface-registry/discovery-batches/{batchId}/confirm` | Confirm eligible diff items and publish snapshot. |
| POST | `/api/platform/interface-registry/discovery-batches/{batchId}/reject` | Reject a batch with reason. |
| POST | `/api/platform/interface-registry/diffs/{diffItemId}/confirm` | Confirm a single diff item. |
| POST | `/api/platform/interface-registry/diffs/{diffItemId}/reject` | Reject a single diff item with reason. |
| POST | `/api/platform/interface-registry/interfaces/{interfaceCode}/deprecate` | Mark confirmed interface as deprecated. |

### Frontend / UI
- Platform Admin Interface Registry index / catalog browser.
- Discovery batch list.
- Diff review screen with new/changed/deprecated/missing grouping.
- Confirm/reject actions with reason capture.
- Interface detail page with endpoints, consumers, lifecycle, compatibility notes and latest active snapshot.
- Gateway-backed JavaScript only; no direct Platform service port calls.

## Entity Fields
Platform Interface Registry records are platform-global governance data. They use `GlobalEntity` or the current Platform global base type, not tenant-owned `EntityBase`, because they describe service/module contracts across tenants and domains. DTO/request/form payloads must not include `TenantId`.

### InterfaceDefinition
| Field | Type | Rules |
|---|---|---|
| Base | `GlobalEntity` | Platform global record; tenant-owned data degildir. |
| InterfaceCode | `string` | Required, stable, normalized uppercase/kebab or uppercase-dot convention; unique with `Version`. |
| DisplayName | `string` | Required. |
| Description | `string?` | Optional. |
| OwnerModuleCode | `string` | Required; must resolve through Module Catalog. |
| ProviderService | `string` | Required; e.g. `Diten.Platform`, `Diten.AuthService`. |
| Version | `string` | Required semantic interface version. If codebase reserves `Version` for concurrency, implementation must use `InterfaceVersion`. |
| Stability | `InterfaceStability` | Required; e.g. `Experimental`, `Stable`, `Deprecated`. |
| Visibility | `InterfaceVisibility` | Required; e.g. `Internal`, `Platform`, `Tenant`, `Public`. |
| LifecycleStatus | `InterfaceLifecycleStatus` | Required; e.g. `Discovered`, `PendingReview`, `Active`, `Deprecated`, `Retired`, `Rejected`. |
| CompatibilityNotes | `string?` | Optional. |
| DeprecationReason | `string?` | Required when lifecycle becomes `Deprecated`. |
| DeprecatedAtUtc | `DateTimeOffset?` | Set when deprecated. |
| ConfirmedAtUtc | `DateTimeOffset?` | Set when active snapshot is confirmed. |
| ConfirmedBy | `Guid?` | Current user id when available. |
| RowVersion | `byte[]` | Required for update/concurrency if supported by current Platform patterns. |

### InterfaceCode and EndpointKey Standard
`InterfaceCode` format:

```text
{MODULE}.{RESOURCE}.{ACTION}
```

Examples:
- `BANK.TRANSACTIONS.LIST`
- `AP.INVOICES.GET`

Rules:
- Trim input.
- Normalize to uppercase.
- Use dot-separated segments.
- Each segment must contain only `A-Z`, `0-9`, or `_`.
- Minimum segment count: 3 (`MODULE`, `RESOURCE`, `ACTION`).
- Duplicate dots, leading dots and trailing dots are rejected.

`EndpointKey` format:

```text
{HTTP_METHOD}:{NORMALIZED_ROUTE}:{VERSION}
```

Examples:
- `GET:/api/bank/transactions:v1`
- `POST:/api/platform/interface-registry/manifests/import:v1`

Rules:
- `HTTP_METHOD` is uppercase.
- Route is trimmed and lowercased.
- Route always starts with `/`.
- Duplicate slashes are normalized to a single slash.
- Trailing slash is removed except for `/`.
- Version is trimmed and lowercased, for example `v1`.
- EndpointKey uniqueness is evaluated after normalization.

### InterfaceEndpoint
| Field | Type | Rules |
|---|---|---|
| EndpointKey | `string` | Required stable key derived from method + normalized path + version. |
| HttpMethod | `string` | Required; strict HTTP verb enum/string set. |
| RoutePath | `string` | Required; normalized API route path. |
| RouteName | `string?` | Optional framework route name. |
| PermissionKey | `string?` | Optional; must follow existing permission convention when supplied. |
| AuthPolicy | `string?` | Optional; e.g. `PlatformActor`. |
| RequestContract | `string?` | Optional DTO/schema name; detailed schema ownership remains `MOD-0003`. |
| ResponseContract | `string?` | Optional DTO/schema name; detailed schema ownership remains `MOD-0003`. |
| ProducesStatusCodes | `string[]` | Optional list of documented status codes. |
| IsBreakingChangeCandidate | `bool` | Derived during diff when route/method/contract change suggests risk. |

### InterfaceConsumerDependency
| Field | Type | Rules |
|---|---|---|
| ConsumerModuleCode | `string` | Required; must resolve through Module Catalog when known. |
| ConsumerService | `string` | Required. |
| ConsumedInterfaceCode | `string` | Required. |
| ConsumedVersionRange | `string?` | Optional semver range. |
| Required | `bool` | Required; marks hard vs optional dependency. |
| UsageContext | `string?` | Optional; short consumer-side explanation. |

### InterfaceDiscoveryBatch
| Field | Type | Rules |
|---|---|---|
| BatchId | `Guid` | Required. |
| SourceService | `string` | Required. |
| SourceModuleCode | `string` | Required and Module Catalog validated. |
| ManifestHash | `string` | Required for idempotency. |
| ImportedAtUtc | `DateTimeOffset` | Required UTC. |
| ImportedBy | `Guid?` | Optional; system/import user. |
| Status | `string` | `Imported`, `PendingReview`, `PartiallyConfirmed`, `Confirmed`, `Rejected`, `Failed`. |
| NewCount | `int` | Derived from diff. |
| ChangedCount | `int` | Derived from diff. |
| DeprecatedCount | `int` | Derived from diff. |
| MissingCount | `int` | Derived from diff. |
| RejectedCount | `int` | Derived from review decisions. |
| ErrorMessage | `string?` | Controlled failure reason. |

### InterfaceDiscoveryDiffItem
| Field | Type | Rules |
|---|---|---|
| DiffItemId | `Guid` | Required. |
| BatchId | `Guid` | Required. |
| InterfaceCode | `string` | Required. |
| EndpointKey | `string?` | Required for endpoint-level diff. |
| ChangeType | `InterfaceChangeType` | `New`, `Changed`, `Deprecated`, `Missing`, `Unchanged`. |
| PreviousHash | `string?` | Optional; active snapshot hash. |
| IncomingHash | `string?` | Optional; manifest hash. |
| ReviewStatus | `string` | `Pending`, `Confirmed`, `Rejected`. |
| ReviewReason | `string?` | Required when rejected. |
| ReviewedAtUtc | `DateTimeOffset?` | Set on decision. |
| ReviewedBy | `Guid?` | Current user id when available. |

## Review State Model
### Lifecycle States
| State | Meaning |
|---|---|
| Discovered | Manifest metadata was received from a service/module but has not yet entered admin review. |
| PendingReview | Diff item or batch is waiting for Platform Admin decision. |
| Confirmed | Review decision accepted the diff item; it is eligible to update active snapshot. |
| Active | Confirmed interface metadata is published in the active registry snapshot. |
| Changed | Incoming manifest differs from the current active snapshot. |
| MissingInSource | Previously active interface or endpoint is absent from the latest source manifest. |
| Deprecated | Interface remains visible but is marked deprecated with reason and timestamp. |
| Retired | Interface is no longer active and should not be used by new consumers. |
| Rejected | Review decision rejected the diff item; active snapshot must remain unchanged. |

### Review Rules
- Import creates `Discovered` / `PendingReview` records only.
- `Confirmed` diff items update the active snapshot.
- `Rejected` diff items never update the active snapshot.
- `MissingInSource` does not automatically retire an active interface; admin review is required.
- `Deprecated` requires review/deprecation reason and timestamp.
- `Retired` is explicit and must not be inferred from one missing manifest.

### Local Review Metadata
Local review metadata is mandatory for MVP:
- `ReviewedBy`
- `ReviewedAtUtc`
- `ReviewReason`
- `Decision`

The metadata must be persisted for confirm, reject and deprecate decisions. If current user id is not available, implementation must store the best available system/user identity according to existing Platform conventions.

## Repo Scope
Current authoring change:
- `execution/domains/platform-shared-services/module-packs/MOD-0002-interface-registry.md`

Ready-for-dev MVP implementation scope:
- `execution/domains/platform-shared-services/module-packs/MOD-0002-interface-registry.md`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/InterfaceRegistryController.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/InterfaceRegistry/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/InterfaceRegistry/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/InterfaceRegistry/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IInterfaceRegistry*.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/InterfaceRegistry/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/**`
- `services/Diten.Platform/tests/**`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.InterfaceRegistry.Abstractions/**` for the preferred dependency-light abstraction package.
- `services/Diten.Platform.Common/src/Diten.Platform.InterfaceRegistry.Abstractions/**` only if the fallback package location is explicitly justified during implementation.
- `frontend/Diten.Web/Controllers/Platform/InterfaceRegistryController.cs`
- `frontend/Diten.Web/Views/Platform/InterfaceRegistry/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/InterfaceRegistry/**`
- `frontend/Diten.Web/Resources/Views/Platform/InterfaceRegistry/**`
- `gateway/Diten.ApiGateway/**` for route validation/coordination only; `ocelot.json` remains integration-agent owned.

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json` unless explicitly handled by integration-agent in the implementation phase.
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**`
- `execution/domains/master-data-management/module-packs/MDM-002-interface-registry.md` unless user explicitly requests ownership cleanup.
- Runtime files outside the ready-for-dev MVP implementation scope listed above.
- Any Data Contract Registry, Event Taxonomy, Schema Governance or Gateway Hardening ownership outside this pack.

## Dependencies
- `PSS-005-tenant-module-catalog` for Module Catalog identity and `ModuleCatalogItem.ModuleCode` owner validation.
- `MOD-0014-module-boundary-registry` for global platform catalog terminology and module boundary alignment.
- `MOD-0032-api-gateway` for later route/gateway hardening integration; not required for initial manifest review MVP.
- `MOD-0003-data-contract-registry` for future DTO/schema details; this pack stores only lightweight contract references.
- `MOD-0039-schema-compatibility-governance` for future breaking-change policy.
- `MOD-0021-audit-trail-service` for audit instrumentation when review decisions and rejected imports must be recorded centrally.
- Existing Platform API conventions: `Response<T>`, `CustomBaseController`, MediatR/CQRS, FluentValidation, pipeline behaviors and RBAC.
- Existing frontend Gateway proxy pattern; frontend must call Gateway port `5000`.

## Runtime Constraints
- Frontend calls only Gateway port `5000`; direct calls to Platform service port `5057` are forbidden.
- API responses use `Response<T>` envelope and current `CustomBaseController` conventions.
- JWT + RBAC is mandatory for all Platform Admin registry surfaces.
- Suggested permission keys:
  - `Platform.InterfaceRegistry.Read`
  - `Platform.InterfaceRegistry.Import`
  - `Platform.InterfaceRegistry.Review`
  - `Platform.InterfaceRegistry.Deprecate`
- MongoDB is the persistence store.
- Records are platform-global governance records; `TenantId` is not accepted from payloads.
- Soft delete/lifecycle behavior must follow the current Platform global entity pattern; hard delete endpoints are not exposed.
- Manifest import is idempotent by `SourceService + SourceModuleCode + ManifestHash`.
- Discovery import does not directly publish active records; it creates a pending review batch.
- Confirmed snapshot is the active registry state.
- Confirmed active snapshots are stored in a separate Mongo collection: `platform_interface_active_snapshots`.
- Latest confirmed interface state is read from `platform_interface_active_snapshots`.
- Rejected or pending diff items must not change `platform_interface_active_snapshots`.
- Rejected diff items do not mutate active snapshots.
- `ownerModuleCode` and `consumerModuleCode` must be validated against Module Catalog when supplied.
- Scanner/reflection/OpenAPI adapters must not live inside the abstraction package.
- Abstraction package must stay dependency-light: attributes, enums, manifest DTOs and provider interface only.
- Preferred abstraction package is `Diten.BuildingBlocks.InterfaceRegistry.Abstractions` under `services/Diten.Building.Blocks/src/`.
- The abstraction package must not reference MongoDB, ASP.NET Core, Ocelot, persistence repositories, Platform API, Platform Infrastructure or service-specific runtime projects.
- DataTable verifier does not apply to create/edit CRUD because this is not a classic CRUD module.
- `golden_reference: manifest-import-diff-review-confirm-active-snapshot` is intentional. The UI is a review/confirm workbench plus read-only catalog browser, not Slim or Compact create/edit form flow.
- If a tabular registry list uses DataTables, it still must include `data-dt-standard="v2"` and existing DataTable v2 conventions, but no create/edit offcanvas or full CRUD form is implied.
- Platform localization for new Platform modules should be `en` and `tr` unless a more specific module pack rule requires 7 languages.

## Golden Flow
1. Platform service or another approved service produces an interface manifest through the abstraction contract, or sends a manifest document to the import endpoint.
2. Interface Registry receives the manifest through `/api/platform/interface-registry/manifests/import`.
3. Registry validates `ownerModuleCode` against Module Catalog.
4. Registry normalizes `InterfaceCode` and `EndpointKey` values.
5. Registry compares the manifest with the latest active snapshot.
6. Registry creates a discovery batch and diff items classified as `new`, `changed`, `missing`, `deprecated`, or `unchanged`.
7. Platform Admin opens the discovery batch in the Interface Registry review UI.
8. Admin confirms one diff item.
9. Admin rejects one diff item and provides a non-empty reason.
10. Confirmed item is written to the active snapshot.
11. Rejected item is persisted with review metadata and does not change the active snapshot.
12. After reload, batch status, diff item review decisions and active snapshot state are still correct.

## Failure Paths
- Unknown owner module:
  - If `ownerModuleCode` does not exist in Module Catalog, import stops with a controlled validation error.
  - No active snapshot changes are published for that manifest.
- Duplicate interface version:
  - If the same normalized `InterfaceCode + Version` conflicts with an existing confirmed interface outside the allowed idempotent import path, API returns a controlled conflict response.
- Empty reject reason:
  - Reject is blocked when `ReviewReason` is empty, whitespace, or missing.
  - Existing review state remains unchanged.
- Unconfirmed diff:
  - Diff items that remain `PendingReview`, `Discovered`, or `Rejected` must not update active snapshot.
- Duplicate endpoint key:
  - Duplicate normalized `EndpointKey` values in one manifest return a controlled validation error.
- Import failed:
  - Failed imports persist controlled error metadata when a batch was created, or return controlled validation before batch creation when the manifest is structurally invalid.

## Audit Decision
- MVP requires local review metadata for every review decision:
  - `ReviewedBy`
  - `ReviewedAtUtc`
  - `ReviewReason`
  - `Decision`
- Confirm, reject, deprecate, import failed and duplicate conflict paths must be audit-ready.
- If `MOD-0021 Audit Trail Service` is available, implementation emits central audit events for:
  - `interface_manifest.import_failed`
  - `interface_diff.confirmed`
  - `interface_diff.rejected`
  - `interface.deprecated`
  - `interface_registry.duplicate_conflict`
- If `MOD-0021` is not available, this MVP is not blocked.
- When `MOD-0021` is not available, implementation leaves an audit integration seam and still persists local review metadata. Review metadata must not be skipped.

## OpenAPI Decision
- MVP is Attribute/Manifest-first.
- OpenAPI import and enrichment are later-scope adapters.
- This pack does not require OpenAPI import for MVP implementation.
- Future OpenAPI adapter must feed the same manifest import contract instead of bypassing Module Catalog validation, diff generation or review/confirm workflow.

## Implementation Batches
Implementation must be delivered in three controlled batches. A batch is not complete until its golden flow proof, failure path proof and output report are produced.

### Batch 1 - Abstractions + Domain + Manifest Import + Diff Foundation
Scope:
- Create `Diten.BuildingBlocks.InterfaceRegistry.Abstractions`.
- Add attributes, enums, manifest DTOs and `IInterfaceManifestProvider` contracts.
- Add Platform domain entity/model/repository foundations.
- Add manifest import endpoint.
- Add Module Catalog `ownerModuleCode` validation.
- Add manifest hash idempotency.
- Generate diff items: `new`, `changed`, `missing`, `deprecated`, `unchanged`.
- Persist discovery batches and diff items.

Golden flow:
1. Manifest is imported.
2. Owner module is validated through Module Catalog.
3. Normalized `InterfaceCode` and `EndpointKey` values are produced.
4. Discovery batch is created.
5. Diff items are persisted.
6. After reload, batch and diff items are still available.

Failure path:
- Unknown owner module returns controlled validation error and active snapshot remains unchanged.
- Duplicate endpoint key returns controlled validation error and active snapshot remains unchanged.
- Invalid `InterfaceCode` returns controlled validation error and active snapshot remains unchanged.
- Duplicate `InterfaceCode + Version` returns controlled conflict response and active snapshot remains unchanged.

Completion gate:
- Batch 1 status must be `PASS` before Batch 2 starts.

### Batch 2 - Review / Confirm / Reject + Active Snapshot
Scope:
- Add diff item confirm/reject endpoints.
- Add batch confirm/reject endpoints.
- Add empty reject reason validation.
- Write confirmed items to `platform_interface_active_snapshots`.
- Ensure `Rejected`, `PendingReview` and `Discovered` items do not change active snapshot.
- Require local review metadata: `ReviewedBy`, `ReviewedAtUtc`, `ReviewReason`, `Decision`.
- Add deprecate endpoint with reason/timestamp validation.
- Emit audit events if `MOD-0021` exists; otherwise leave audit integration seam and persist local metadata.

Golden flow:
1. Admin opens a discovery batch.
2. Admin confirms one diff item.
3. Admin rejects one diff item and enters a reason.
4. Confirmed item appears in active snapshot.
5. Rejected item does not appear in active snapshot.
6. After reload, review decisions and active snapshot are still correct.

Failure path:
- Empty reject reason blocks reject and leaves state unchanged.
- Unconfirmed or rejected item is not written to active snapshot.
- Missing deprecate reason blocks deprecate and leaves lifecycle unchanged.

Completion gate:
- Batch 2 starts only after Batch 1 is `PASS`.
- Batch 2 status must be `PASS` before Batch 3 starts.

### Batch 3 - Platform Admin UI + Gateway-backed Runtime Smoke
Scope:
- Add Interface Registry list/catalog browser.
- Add discovery batch list.
- Add diff review screen.
- Add confirm/reject actions and reason capture.
- Add interface detail screen: endpoints, consumers, lifecycle, compatibility notes and active snapshot.
- Add loading, empty, no-result, validation error, permission-denied and partial failure states.
- Ensure frontend calls only Gateway port `5000`; direct `5057` calls are forbidden.
- Do not create manual endpoint create/edit screens.
- Add `en` / `tr` localization parity.
- Run UI smoke tests and build validation.

Golden flow:
1. Platform Admin opens Interface Registry.
2. Admin opens discovery batch list.
3. Admin opens diff review screen.
4. Admin confirms one item and rejects one item.
5. Interface detail screen shows active snapshot.
6. After reload, state is preserved.

Failure path:
- Permission denied renders controlled permission-denied state, not fake empty data.
- Backend failure does not leave operational-looking dead buttons.
- Direct `5057` calls are absent from frontend code.

Completion gate:
- Batch 3 starts only after Batch 2 is `PASS`.
- Batch 3 completes the MVP only when golden flow, failure path and validation proof are reported.

### Batch Dependency Rule
- Batch 2 must not start until Batch 1 passes.
- Batch 3 must not start until Batch 2 passes.
- Each batch must report its own golden flow and failure path proof before it can be marked complete.
- A `PARTIAL`, `FAIL` or `BLOCKED` batch blocks later batches unless the user explicitly approves a narrowed continuation.

### Batch Output Report Standard
Every batch completion report must include:
- Batch status: `PASS` / `PARTIAL` / `FAIL` / `BLOCKED`
- Golden flow proof
- Failure path proof
- Changed files
- Boundary / SoR check
- API / DTO / schema impact
- Audit / review metadata impact
- Validation commands
- Open items
- Next batch readiness

## Now vs Later
### Now
- Keep this module pack at `ready-for-dev` and use it as the implementation contract.
- Use `Diten.BuildingBlocks.InterfaceRegistry.Abstractions` as the preferred abstraction namespace/package shape unless implementation documents a Platform.Common fallback reason.
- Define attribute field standard: code, owner module, version, stability, visibility, lifecycle.
- Define manifest DTO shape.
- Define consumer dependency metadata standard.
- Define Platform registry state model: discovered, pending review, confirmed active snapshot, rejected.
- Define Module Catalog ownership validation.

### Later
- Implement reflection scanner.
- Implement ASP.NET Core endpoint inspection.
- Implement OpenAPI enrichment/import.
- Add attributes to ERP modules.
- Add Gateway/OpenAPI automatic route validation.
- Add breaking-change CI enforcement.
- Add full public developer portal.
- Expand Data Contract Registry integration for detailed DTO/schema compatibility.

## Acceptance Criteria
- [ ] [Pack] Module pack exists at `execution/domains/platform-shared-services/module-packs/MOD-0002-interface-registry.md` with `status: ready-for-dev`.
- [ ] [Pack] Domain decision documents Platform Shared Services as runtime owner and explains why MDM-002 is not the runtime SoR.
- [ ] [Batch 1] Module Catalog remains the module identity SoR; Interface Registry validates but does not duplicate module identity ownership.
- [ ] [Batch 1] Abstraction package is implemented as `Diten.BuildingBlocks.InterfaceRegistry.Abstractions`, or Platform.Common fallback is explicitly justified while preserving dependency-light rules.
- [ ] [Batch 1] Manifest import contract includes interface definition, endpoint and consumer dependency metadata.
- [ ] [Batch 1] Import is idempotent for the same `SourceService + SourceModuleCode + ManifestHash`.
- [ ] [Batch 1] Import creates a discovery batch and diff items; it does not directly publish active snapshots.
- [ ] [Batch 1] Diff generation classifies `new`, `changed`, `deprecated`, `missing` and `unchanged` items.
- [ ] [Batch 1] Duplicate `InterfaceCode + Version` and duplicate endpoint keys are rejected or surfaced as controlled validation/conflict errors.
- [ ] [Batch 1] `ownerModuleCode` missing from Module Catalog is rejected with a controlled validation error.
- [ ] [Batch 1] Batch 1 output report includes status, golden flow proof, failure path proof, changed files, boundary/SoR check, API/DTO/schema impact, audit/review metadata impact, validation commands, open items and Batch 2 readiness.
- [ ] [Batch 2] Admin can confirm or reject batch/diff items with controlled reason capture.
- [ ] [Batch 2] Confirmed diff item publishes or updates the active snapshot.
- [ ] [Batch 2] Rejected diff item does not mutate active snapshot.
- [ ] [Batch 2] Unconfirmed diff item does not mutate active snapshot.
- [ ] [Batch 2] Confirmed active snapshots are stored in `platform_interface_active_snapshots`.
- [ ] [Batch 2] Latest confirmed state is read from `platform_interface_active_snapshots`.
- [ ] [Batch 2] Empty reject reason blocks reject action and leaves review state unchanged.
- [ ] [Batch 2] Deprecation requires reason and timestamp.
- [ ] [Batch 2] Local review metadata (`ReviewedBy`, `ReviewedAtUtc`, `ReviewReason`, `Decision`) is persisted for confirm/reject/deprecate decisions.
- [ ] [Batch 2] MOD-0021 central audit emits when available; absence of MOD-0021 leaves audit seam and does not block MVP.
- [ ] [Batch 2] Batch 2 output report includes status, golden flow proof, failure path proof, changed files, boundary/SoR check, API/DTO/schema impact, audit/review metadata impact, validation commands, open items and Batch 3 readiness.
- [ ] [Batch 3] All API responses use `Response<T>` envelope.
- [ ] [Batch 3] Platform Admin UI uses Gateway-backed calls only and never calls `5057` directly.
- [ ] [Batch 3] UI supports registry list, interface detail, discovery batch list, diff review, confirm and reject states.
- [ ] [Batch 3] UI renders loading, empty, no-result, validation error, permission-denied and partial failure states.
- [ ] [Batch 3] Reload after review preserves batch status, diff decisions and active snapshot state in UI.
- [ ] [Batch 3] Localization keys exist for `en` and `tr` for all Platform UI text.
- [ ] [Batch 3] Gateway route coverage is reviewed; if `ocelot.json` changes are required, an integration-agent task is created instead of modifying it directly.
- [ ] [Batch 3] Manual endpoint create/edit screen is not created.
- [ ] [Batch 3] OpenAPI import is not required for MVP and remains a later adapter.
- [ ] [Batch 3] Batch 3 output report includes status, golden flow proof, failure path proof, changed files, boundary/SoR check, API/DTO/schema impact, audit/review metadata impact, validation commands, open items and MVP completion status.

## Test Expectations
- Build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- Build: `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`
- Build/route coverage: `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`

### Batch 1 Tests
- Unit: abstraction attribute constructor/default value tests.
- Unit: enum serialization/normalization tests for stability, visibility, lifecycle, change type and review decision.
- Unit: manifest validation rejects missing interface code, missing owner module code, invalid version and duplicate endpoint keys.
- Unit: Module Catalog owner module validation returns controlled error when `ownerModuleCode` is unknown.
- Unit: import idempotency for identical manifest hash.
- Unit: diff generation for `new`, `changed`, `deprecated`, `missing`, `unchanged`.
- Unit: `InterfaceCode` normalization validates `{MODULE}.{RESOURCE}.{ACTION}`.
- Unit: `EndpointKey` normalization validates `{HTTP_METHOD}:{NORMALIZED_ROUTE}:{VERSION}` and removes duplicate slashes.
- Integration: manifest import endpoint returns `Response<InterfaceManifestImportResultDto>`.
- Integration: discovery batch endpoints list and filter by status/change type.
- Integration: duplicate `InterfaceCode + Version` conflict returns 409 or current controlled conflict convention.
- Integration: missing Module Catalog owner returns 400/404 according to current validation convention.
- Proof: reload after import preserves discovery batch and diff items.

### Batch 2 Tests
- Unit: reject decision requires reason and does not mutate active snapshot.
- Unit: confirm decision updates active snapshot and records reviewer/timestamp.
- Unit: unconfirmed diff item does not mutate active snapshot.
- Unit: deprecate requires reason and timestamp.
- Unit: local review metadata is persisted for confirm/reject/deprecate.
- Integration: confirmed active snapshot can be read by interface code.
- Integration: reload after confirm/reject preserves batch status, decisions and active snapshot.
- Integration: reject without reason returns controlled validation response.
- Integration: deprecate without reason returns controlled validation response.
- Proof: rejected and pending diff items do not write to `platform_interface_active_snapshots`.
- Proof: MOD-0021 unavailable path leaves audit seam and local metadata intact.

### Batch 3 Tests
- UI smoke: Platform Admin opens Interface Registry list.
- UI smoke: Admin opens discovery batch list and diff review screen.
- UI smoke: Admin confirms one item and rejects one item with reason.
- UI smoke: after page reload, review decisions and active snapshot remain visible and correct.
- UI smoke: Interface detail shows endpoints, consumers, lifecycle, compatibility notes and active snapshot state.
- UI smoke: Permission-denied state renders instead of fake empty data.
- UI smoke: backend failure state does not leave operational-looking dead buttons.
- UI smoke: no manual endpoint create/edit screen is present.
- Gateway: Frontend route/proxy uses Gateway port `5000`; no direct `5057` URL appears in Interface Registry frontend assets.
- RESX/l10n: Interface Registry `en` and `tr` resources are present and parity-checked.

## Implementation Notes
- This pack is now ready for development at MVP scope. Implementation must stay inside the scope and exclusions in this file.
- First implementation step should verify existing Platform base types (`GlobalEntity`, row version/concurrency conventions) before choosing exact inheritance.
- If `Version` is already reserved by the codebase for concurrency, implementation must use `InterfaceVersion` for semantic interface version.
- Abstraction package should be created as a separate lightweight project/folder under `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.InterfaceRegistry.Abstractions` when compatible with current repo structure.
- Platform.Common fallback is allowed only with explicit rationale and the same dependency bans.
- The abstraction package must not reference MongoDB, ASP.NET Core MVC, Ocelot, persistence repositories or Platform API projects.
- Scanner adapters can be added later as `Diten.Platform.InterfaceRegistry.AspNetCore` or Platform Infrastructure components in a separate approved scope.
- Manual endpoint create/edit should not be built as the primary UX; review/confirm discovered metadata is the golden flow.
- Master Plan says OpenAPI specs are an acceptance criterion. For this MVP, OpenAPI import/enrichment is explicitly later scope and should plug into the same manifest import contract when implemented.

## Follow-up Items
- [ ] User reviews this ready-for-dev pack and confirms implementation can start.
- [ ] Decide whether `MDM-002-interface-registry.md` should be marked blocked/superseded/reference in a separate explicit cleanup task.
- [ ] Keep OpenAPI import/enrichment as a later adapter unless a separate approved scope change is made.
- [ ] Confirm implementation uses `platform_interface_active_snapshots` as the active snapshot collection.
- [ ] Confirm implementation leaves the MOD-0021 audit integration seam when central audit is unavailable.
- [ ] Call `@orchestrator` with this ready-for-dev module pack; do not ask orchestrator to create the pack.
