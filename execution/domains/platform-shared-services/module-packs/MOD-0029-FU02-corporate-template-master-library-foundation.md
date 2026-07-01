---
id: MOD-0029-FU02
name: Corporate Template Master Library Foundation
parent: MOD-0029
previous: MOD-0029-FU01
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: TenantScopedEntity
status: approved
owner: platform-shared-services
branch: feature/pss/mod-0029-fu02-corporate-template-master-library-foundation
started: 2026-06-27
target: 2026-07-18
form_field_count: 12
---

# MOD-0029-FU02 - Corporate Template Master Library Foundation

## 1. Module Summary

MOD-0029-FU02 adds the planning contract for a TenantShell Corporate Template Library / Corporate Template
Masters foundation under the MOD-0029 parent, **Controlled Documents (SOPs/Work Instructions)**.

The purpose is to introduce a corporate reusable master-template layer without converting or breaking the
existing MOD-0029-FU01 folder/company-scoped template model:

- `TemplateMaster` is the tenant-level or corporate reusable master template definition.
- `TemplateMasterVersion` is the master-template binary/version lifecycle.
- `TemplateDocument` remains the folder-attached or company-scoped template item.
- `TemplateVersion` remains the folder-attached template version lifecycle.

This pack is an **approved planning contract** for the Corporate Template Master Library foundation. Runtime
implementation still starts only when `@orchestrator` is invoked with this pack and the remaining
pre-implementation tasks in §18 are handled.

Form field count decision: the Corporate Template Master create/edit surface has more than eight user-editable
fields (`MasterCode`, `TemplateName`, `Description`, `Classification`, `CollectionDefinitionId`, `CanonicalId`,
`VariantPolicy`, `OwnerCompanyId`, `OwnerUserId`, `EffectiveDate`, file/content input, `ChangeSummary`), so the
module uses `golden_reference: compact`.

## 2. Ownership and Boundaries

### In-scope

- New tenant-scoped aggregates:
  - `TemplateMaster`.
  - `TemplateMasterVersion`.
- Nullable lineage fields on existing `TemplateDocument`:
  - `TemplateMasterId?`.
  - `TemplateMasterVersionId?`.
  - `SourceTemplateDocumentId?`.
  - `SourceTemplateVersionId?` if implementation needs explicit version lineage.
- Corporate Template Library TenantShell surface:
  - master template list/grid.
  - filters for status, classification, collection/canonical binding, and variant policy.
  - master details.
  - publish new master version action.
  - deprecate action.
  - adoption impact placeholder/basic usage summary.
- Reuse the existing MOD-0029-FU01 content storage abstraction for master version content.
- Preserve all existing `TemplateDocument` / `TemplateVersion` endpoints and folder-attached template behavior.

### Out-of-scope

- Converting `TemplateDocument` into a master template.
- Automatic folder-attached `TemplateDocument` creation when a master is published.
- `TemplateVariant` aggregate implementation.
- Drift detection engine.
- Compare UI.
- Rebase engine.
- Full adoption impact engine.
- Governance dashboard.
- Deviation queue.
- Approval workflow, approval queue, approver assignment, review state machine.
- E-signature.
- MOD-0023 workflow integration.
- Template variant approval.
- Evidence export.
- MOD-0028 structure mutation, `CollectionDefinition` editing, or `CollectionInstance` tree mutation.
- AuthService permission seed implementation in this pack.
- Gateway `ocelot.json` modification in this pack.

## 3. Owned Objects

### Domain objects

- `TemplateMaster`.
- `TemplateMasterVersion`.
- `TemplateMasterStatus` enum: `Draft`, `Published`, `Deprecated`, `Archived`.
- `TemplateVariantPolicy` enum: `Allowed`, `Locked`.
- `TemplateMasterClassification` may be an enum or controlled lookup-like vocabulary only if the implementation
  scope explicitly chooses the source. Hardcoded frontend fallback lists are not accepted.

### Existing objects extended

- `TemplateDocument` may gain nullable lineage fields only:
  - `TemplateMasterId?`.
  - `TemplateMasterVersionId?`.
  - `SourceTemplateDocumentId?`.
  - `SourceTemplateVersionId?`.

### Repositories

- `ITemplateMasterRepository`.
- `ITemplateMasterVersionRepository`.
- Mongo repository implementations under the existing Platform persistence layer.

### Commands

- `CreateTemplateMasterCommand`.
- `PublishTemplateMasterVersionCommand`.
- `DeprecateTemplateMasterCommand`.
- Optional metadata update command only if explicitly approved during implementation: `UpdateTemplateMasterCommand`.

### Queries

- `GetTemplateMasterListQuery`.
- `GetTemplateMasterByIdQuery`.
- `GetTemplateMasterVersionsQuery`.
- `GetTemplateMasterAdoptionImpactQuery`.
- `GetTemplateMasterOptionsQuery`.

### DTOs / models

- `TemplateMasterListItemModel`.
- `TemplateMasterDetailModel`.
- `TemplateMasterVersionModel`.
- `TemplateMasterOptionModel`.
- `TemplateMasterAdoptionImpactModel`.
- `CreateTemplateMasterInput`.
- `PublishTemplateMasterVersionInput`.
- `DeprecateTemplateMasterInput`.

### API endpoints

- `GET /api/v1/document-management/template-masters`.
- `GET /api/v1/document-management/template-masters/{id}`.
- `POST /api/v1/document-management/template-masters`.
- `POST /api/v1/document-management/template-masters/{id}/versions/publish`.
- `POST /api/v1/document-management/template-masters/{id}/deprecate`.
- `GET /api/v1/document-management/template-masters/{id}/versions`.
- `GET /api/v1/document-management/template-masters/{id}/adoption-impact`.
- `GET /api/v1/document-management/template-master-options`.

### Frontend routes

- `/DocumentManagementTemplateMasters`.
- `/DocumentManagementTemplateMasters/Create`.
- `/DocumentManagementTemplateMasters/Edit/{id}` if metadata editing is approved.
- `/DocumentManagementTemplateMasters/Details/{id}`.

### Permission keys

- `platform.document-management.template-masters.view`.
- `platform.document-management.template-masters.create`.
- `platform.document-management.template-masters.version.publish`.
- `platform.document-management.template-masters.deprecate`.
- `platform.document-management.template-masters.impact.view`.
- `platform.document-management.template-masters.manage`.

## 4. Entity Fields

### TemplateMaster

| Field | Type | Required | Rules / meaning | Index / pre-check |
|---|---|---:|---|---|
| Base | `TenantScopedEntity` | Yes | Inherits `Id`, `TenantId`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`, technical `Version` through Platform base contracts. | Tenant filter required. |
| MasterCode | `string` | Yes | Trim, uppercase or canonical normalized, max 80, stable business identifier. | Unique per `TenantId + MasterCode + IsDeleted=false`; duplicate returns 409. |
| TemplateName | `string` | Yes | Trim, max 256. | Text/filter index optional. |
| Description | `string?` | No | Trim, max 2000. | - |
| Classification | `string` or enum | Yes | Controlled value; no hardcoded frontend fallback without approved source. | Indexed for list filter. |
| CollectionDefinitionId | `Guid?` | No | Optional MOD-0028 definition binding; read-only validation only. | Indexed if used. |
| CanonicalId | `string?` | No | Optional canonical node binding/snapshot; must not mutate MOD-0028. | Indexed with tenant if used. |
| VariantPolicy | `TemplateVariantPolicy` | Yes | `Allowed` or `Locked`; prepares future variants without implementing them. | Indexed for list filter. |
| OwnerCompanyId | `Guid?` | No | Optional corporate/legal entity owner. | Validate reference when available. |
| OwnerUserId | `Guid?` | No | Optional user owner. | Validate only if an approved user-reference seam exists. |
| Status | `TemplateMasterStatus` | Yes | `Draft`, `Published`, `Deprecated`, `Archived`. | Indexed for list filter. |
| CurrentVersionId | `Guid?` | No | Points to current `TemplateMasterVersion`. | Must reference same tenant/master. |
| CurrentMasterVersion | `int` | Yes | Business version number; do not name this field `Version`. | Monotonic per master. |
| EffectiveDate | `DateTimeOffset?` | No | Optional effective start date. | - |
| DeprecatedAt | `DateTimeOffset?` | No | Set when status becomes `Deprecated`. | - |
| DeprecatedBy | `string?` | No | Actor name/user id from current user context. | - |
| UsageCount | `int?` | No | Placeholder/computed summary; may not be persisted in first slice. | Computed preferred. |
| VariantCount | `int?` | No | Placeholder/computed summary; may not be persisted in first slice. | Computed preferred. |
| DeletedAt | `DateTimeOffset?` | No | Required if the actual Platform base does not provide it directly. | Soft delete only. |

### TemplateMasterVersion

| Field | Type | Required | Rules / meaning | Index / pre-check |
|---|---|---:|---|---|
| Base | `TenantScopedEntity` | Yes | Tenant-owned master version record. | Tenant filter required. |
| TemplateMasterId | `Guid` | Yes | Parent master id. | Unique with `VersionNumber`. |
| VersionNumber | `int` | Yes | Monotonic master version number; business field name is explicit. | Unique `TenantId + TemplateMasterId + VersionNumber`. |
| FileRef / ContentRef | `ContentRef` | Yes | Existing content-storage pointer; no raw bytes in Mongo. | Storage write succeeds before metadata commit. |
| Checksum | `string` | Yes | SHA/checksum from storage abstraction. | Used for unchanged-content guard. |
| Status | `string` or enum | Yes | Suggested: `Draft`, `Published`, `Superseded`, `Archived`; `Published` is technical activation, not approval. | Indexed optional. |
| PublishedAt | `DateTimeOffset?` | Yes for published | Set during publish. | - |
| PublishedBy | `string?` | Yes for published | Actor from current user context. | - |
| ChangeSummary | `string?` | No | Trim, max 1000. | - |
| DeletedAt | `DateTimeOffset?` | No | Required if the actual Platform base does not provide it directly. | Soft delete only. |

### TemplateDocument lineage fields

| Field | Type | Required | Rules / meaning | Backward compatibility |
|---|---|---:|---|---|
| TemplateMasterId | `Guid?` | No | Source master if this folder-attached template was created from a master. | Null for existing records. |
| TemplateMasterVersionId | `Guid?` | No | Source master version at adoption/copy time. | Null for existing records. |
| SourceTemplateDocumentId | `Guid?` | No | Existing folder-template lineage/copy source if needed. | Null for existing records. |
| SourceTemplateVersionId | `Guid?` | No | Existing folder-template version lineage if needed. | Null for existing records. |

## 5. Repo Scope

This pack author turn may only create/update:

- `execution/domains/platform-shared-services/module-packs/MOD-0029-FU02-corporate-template-master-library-foundation.md`.

Future implementation scope after approval may include:

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/**`.
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/DocumentManagement/**`.
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/**`.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementTemplateMasters/**`.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementTemplateMastersController.cs`.
- `services/Diten.Platform/src/Diten.Platform.API/Models/DocumentManagement/**`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/**`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs`.
- `frontend/Diten.Web/Controllers/DocumentManagementTemplateMastersController.cs`.
- `frontend/Diten.Web/Views/DocumentManagement/TemplateMasters/**`.
- `frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/TemplateMasters/**`.
- `frontend/Diten.Web/Resources/Views/DocumentManagement/TemplateMasters/**`.
- `services/Diten.Platform/tests/**`.
- `frontend/Diten.Web` tests/verifier artifacts if present.

## 6. Protected Paths

- `.antigravity/**`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `gateway/Diten.ApiGateway/**/ocelot.json` unless an integration-agent task is explicitly opened.
- `services/Diten.AuthService/**` for this pack; permission seed is a separate security task.
- `services/Diten.MdmService/**`.
- `services/Diten.EnterpriseStrategyService/**`.
- `services/Diten.DevEnablementService/**` except read-only Golden Reference comparison.
- Existing `TemplateDocument` / `TemplateVersion` behavior cannot be broken or replaced.
- MOD-0028 `CollectionDefinition` / `CollectionInstance` mutation paths are out of scope.

## 7. Dependencies

- MOD-0029-FU01 Controlled Document Versioning & Template Sharing Foundation.
- Existing `TemplateDocument` and `TemplateVersion` runtime model.
- Existing MOD-0029 content storage abstraction (`ContentRef` / `FileRef` equivalent).
- Existing `Response<T>` envelope and `CustomBaseController`.
- Existing `[HasPermission]` permission enforcement.
- Existing Gateway route coverage for `/api/v1/document-management/**`; if missing, integration-agent owns route work.
- MOD-0028 read-only reference data for `CollectionDefinitionId` / `CanonicalId` binding validation.
- Existing TenantShell layout and 7-language localization convention for tenant modules.

## 8. Runtime Constraints

- Records are tenant-owned and tenant-isolated. `TenantId` is server-side resolved; request DTOs must not accept
  client-supplied `TenantId`.
- `TemplateMaster` and `TemplateMasterVersion` use Platform's tenant-scoped entity base pattern. The current
  MOD-0029-FU01 runtime uses `TenantScopedEntity`; implementation should follow the concrete Platform base type
  used by neighboring document-management entities.
- Soft delete only; no hard delete/purge in this FU.
- Business version fields must be named `CurrentMasterVersion` / `VersionNumber`; do not use the reserved
  technical `Version` property for business semantics.
- No raw bytes in Mongo.
- No files under `wwwroot`.
- No direct public file URLs.
- Preview/download, if added for master versions, must stream through controlled backend endpoints with Layer 1
  permission checks.
- `TemplateMasterVersion` publish is technical activation, not approval.
- No MOD-0028 structure mutation.
- No automatic folder-attached `TemplateDocument` creation on master publish.

## 9. Layout & Shell Contract

- `shell: tenant`.
- Razor layout: every `.cshtml` page in this module explicitly sets `Layout = "_LayoutTenantShell";`.
- View folder: `frontend/Diten.Web/Views/DocumentManagement/TemplateMasters/`.
- Frontend route: `/DocumentManagementTemplateMasters`.
- Frontend browser calls must go through same-origin MVC proxy or Gateway; no direct service-port `5057` calls.
- The UI is a real Corporate Template Library surface, not a marketing/landing page.

## 10. Backend File Convention

Golden Reference Compact backend shape must be followed, adapted to the module name `DocumentManagementTemplateMasters`
or `TemplateMasters` consistently:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementTemplateMasters/
├── Commands/
│   ├── CreateTemplateMasterCommand.cs
│   ├── PublishTemplateMasterVersionCommand.cs
│   └── DeprecateTemplateMasterCommand.cs
├── Queries/
│   ├── GetTemplateMasterListQuery.cs
│   ├── GetTemplateMasterByIdQuery.cs
│   ├── GetTemplateMasterVersionsQuery.cs
│   ├── GetTemplateMasterAdoptionImpactQuery.cs
│   └── GetTemplateMasterOptionsQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── CreateTemplateMasterHandler.cs
│   │   ├── PublishTemplateMasterVersionHandler.cs
│   │   └── DeprecateTemplateMasterHandler.cs
│   └── QueryHandlers/
│       ├── GetTemplateMasterListHandler.cs
│       ├── GetTemplateMasterByIdHandler.cs
│       ├── GetTemplateMasterVersionsHandler.cs
│       ├── GetTemplateMasterAdoptionImpactHandler.cs
│       └── GetTemplateMasterOptionsHandler.cs
├── Validators/
│   ├── CreateTemplateMasterValidator.cs
│   ├── PublishTemplateMasterVersionValidator.cs
│   └── DeprecateTemplateMasterValidator.cs
└── DocumentManagementTemplateMastersModels.cs
```

Rules:

- Commands and queries are sealed records.
- Handlers are sealed classes ending only in `Handler`; no `CommandHandler`, `QueryHandler`, or `RequestHandler`
  suffix.
- Validators are sealed classes ending only in `Validator`; no `CommandValidator` suffix.
- Controllers stay thin and dispatch MediatR requests only.
- Content-storage orchestration belongs to a service or helper seam, not to controller logic.

## 11. Frontend File Contract

Golden Reference Compact file set:

```text
frontend/Diten.Web/Views/DocumentManagement/TemplateMasters/
├── Index.cshtml
├── Create.cshtml
├── Edit.cshtml                    (only if metadata update is approved)
├── Details.cshtml
├── _Form.cshtml
├── _Filter.cshtml
├── _DataTable.cshtml
├── _IndexL10n.cshtml
└── TemplateMastersIndex.cs

frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/TemplateMasters/
├── index.js
└── index.l10n.js

frontend/Diten.Web/Resources/Views/DocumentManagement/TemplateMasters/
└── TemplateMastersIndex.{lang}.resx
```

Compact rules:

- `_CreateEditOffcanvas.cshtml` is forbidden.
- `_DetailsQuickView.cshtml` is forbidden.
- `Index.cshtml` uses the DataTable v2 contract with `data-dt-standard="v2"`.
- `_Filter.cshtml` is inline/collapsible, not offcanvas.
- `Create/Edit/Details` are full pages and explicitly set `_LayoutTenantShell`.
- TenantShell localization requires 7-language parity: `ar`, `en`, `es`, `fr`, `ru`, `tr`, `zh`.

## 12. Validation Rules

| Field | Required | Format / rule | DB-level | Pre-check |
|---|---:|---|---|---|
| MasterCode | Yes | Trim, normalize, max 80, lowercase/uppercase policy chosen once. | Unique tenant + code + non-deleted. | Duplicate check returns 409. |
| TemplateName | Yes | Trim, max 256. | - | Validator. |
| Description | No | Max 2000. | - | Validator. |
| Classification | Yes | Controlled value; source must be explicit. | Optional filter index. | Validator rejects unknown values. |
| CollectionDefinitionId | No | Guid; if present, must be valid for tenant and read-only MOD-0028 scope. | Optional index. | Reference reader validation. |
| CanonicalId | No | Max 200; optional binding/snapshot; must not mutate MOD-0028. | Optional index. | Validate with CollectionDefinition when available. |
| VariantPolicy | Yes | `Allowed` or `Locked`. | Filter index. | Enum validator. |
| OwnerCompanyId | No | Guid; optional owner legal entity/company. | Optional index. | Validate via existing company/legal entity reference seam when available. |
| OwnerUserId | No | Guid; optional owner user. | - | Validate only through approved user-reference seam. |
| EffectiveDate | No | Date/time. | - | Validator. |
| File / Content | Required for publish | Existing content upload contract; allowed extensions/size from storage options. | No raw bytes in Mongo. | Storage service validation. |
| ChangeSummary | No | Max 1000. | - | Validator. |
| DeprecationReason | Optional for deprecate | Max 1000 if implemented. | - | Validator. |

## 13. Failure Path to Verify

- **Duplicate MasterCode**
  - Expected: 409 conflict; no `TemplateMaster` is created; UI shows localized duplicate message.
- **Missing TemplateName or MasterCode**
  - Expected: 400 validation failure; save/publish blocked; no metadata or content orphan.
- **Invalid CollectionDefinitionId / CanonicalId binding**
  - Expected: 404 non-leakage or 400 validation failure according to reference seam; no MOD-0028 mutation.
- **Publish storage failure**
  - Expected: 503 or storage reason code; no metadata orphan.
- **Metadata commit failure after storage**
  - Expected: best-effort content delete attempted; response uses `Response<T>.Fail`.
- **Duplicate version number**
  - Expected: 409 conflict; existing current version unchanged.
- **Deprecate non-published or missing master**
  - Expected: 404 non-leakage for missing/cross-tenant; 409 for invalid lifecycle transition.
- **Deprecated master used for new folder-attached template**
  - Expected: blocked unless an explicitly approved future rule allows it.
- **Unauthorized actor**
  - Expected: 403 permission denied; UI hides/disables gated action.
- **Existing TemplateDocument flow regression**
  - Expected: existing template upload/version/share/copy/preview/download endpoints remain compatible.

## 14. Authorization Convention

- Policy/controller protection: `[Authorize]` for TenantShell API controllers.
- Permission format: `platform.document-management.template-masters.{action}` because this is implemented in
  `Diten.Platform`.
- Layer 1 permission keys:
  - `platform.document-management.template-masters.view`.
  - `platform.document-management.template-masters.create`.
  - `platform.document-management.template-masters.version.publish`.
  - `platform.document-management.template-masters.deprecate`.
  - `platform.document-management.template-masters.impact.view`.
  - `platform.document-management.template-masters.manage`.
- `platform_admin` bypass behavior follows existing `[HasPermission]` infrastructure.
- Tenant users require explicit permission grants.
- AuthService seed implementation is a separate security task and is not part of this pack-author turn.

## 15. Gateway / API Routing Decision

Decision: Gateway change is expected to be **unnecessary** if the existing `/api/v1/document-management/**`
Gateway routing already covers the new endpoints.

- Frontend must call same-origin MVC proxy or Gateway; it must not call Platform service port `5057` directly.
- If runtime verification shows the Gateway route is missing, `gateway/Diten.ApiGateway/**/ocelot.json` remains a
  protected path and an integration-agent task must add the route.
- OPTIONS/preflight support must be verified if a new explicit route is added.
- This pack does not modify Gateway configuration.

## 16. Acceptance Criteria

- [ ] `TemplateMaster` and `TemplateMasterVersion` are introduced as separate tenant-scoped aggregates.
- [ ] `TemplateDocument` is not converted into a master and its existing endpoints remain compatible.
- [ ] Existing `TemplateDocument` records can remain masterless.
- [ ] `TemplateMasterId` / `TemplateMasterVersionId` lineage fields are nullable.
- [ ] `MasterCode` duplicate is rejected per tenant with a deterministic 409 response.
- [ ] Create master persists classification, variant policy, owner, and optional collection/canonical binding.
- [ ] Publish new master version creates a new immutable content pointer through the existing storage abstraction.
- [ ] No raw bytes are stored in MongoDB.
- [ ] No files are written under `wwwroot`.
- [ ] Preview/download, if implemented, uses controlled backend endpoints and never direct public file URLs.
- [ ] Deprecating a published master sets `DeprecatedAt` / `DeprecatedBy` and prevents new usage unless explicitly allowed.
- [ ] Adoption impact endpoint returns a placeholder/basic usage summary without implementing the full engine.
- [ ] Corporate Template Library list supports filters by status, classification, collection/canonical binding, and variant policy.
- [ ] UI columns include Master ID/MasterCode, Template Name, Version, Status, Classification, binding, variant policy, owner, and actions.
- [ ] Actions include View Details, Publish New Version, View Adoption Impact, and Deprecate.
- [ ] All `.cshtml` pages explicitly set `Layout = "_LayoutTenantShell";`.
- [ ] DataTable v2 verifier passes with Compact reference.
- [ ] MOD-0028 structure mutation is not introduced.
- [ ] AuthService seed changes are not bundled with this implementation; they are tracked as a separate security task.

## 17. Test Expectations

Backend/application tests:

- Create template master.
- Duplicate master code rejected.
- Collection/canonical binding persisted and invalid binding rejected.
- Publish new master version.
- Publish unchanged content behavior is deterministic if checksum guard is included.
- Deprecate published master.
- Deprecated master guarded from new usage unless explicitly allowed.
- List filters by status/classification/collection/variant policy.
- Version history query.
- Adoption impact placeholder/basic summary query.
- Existing `TemplateDocument` / `TemplateVersion` tests not broken.
- Controlled Documents attach/template flow not broken.
- No MOD-0028 structure mutation.
- No raw bytes in Mongo.
- No direct public file URL.

Frontend / verification:

- Build `frontend/Diten.Web`.
- Build `services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj`.
- Run relevant Platform tests.
- Run DataTable verifier:
  - `python3 .antigravity/scripts/verify_datatable_page.py . --area DocumentManagement --module TemplateMasters --reference compact`
- RESX parity across `ar`, `en`, `es`, `fr`, `ru`, `tr`, `zh`.
- Browser smoke: list, filters, create page, details page, publish modal, deprecate action, adoption impact placeholder.

## 18. Ready-for-dev Checklist

- [x] Module identity preflight passed with `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0029-FU02 --name "Corporate Template Master Library Foundation" --parent MOD-0029`.
- [x] Golden Reference Compact pack and live file set were read.
- [x] Frontmatter has `service`, `shell`, `golden_reference`, `entity_base`, and `form_field_count`.
- [x] `golden_reference: compact` selected because form field count is greater than 8.
- [x] Layout & Shell Contract explicitly names `_LayoutTenantShell`.
- [x] Backend File Convention uses `Commands`, `Queries`, `Handlers/CommandHandlers`, `Handlers/QueryHandlers`, `Validators`, and one models file.
- [x] Frontend File Contract lists the Compact file set and forbids Slim-only offcanvas/quick-view files.
- [x] Validation Rules are field-level and testable.
- [x] Failure Path to Verify includes duplicate, missing, unauthorized, lifecycle, storage, and compatibility paths.
- [x] Authorization Convention lists permission keys and actor behavior.
- [x] Gateway routing decision is explicit and protects `ocelot.json`.
- [x] User reviewed and accepted the draft scope.
- [x] User confirmed status transition to `approved`.
- [ ] AuthService permission seed task is required as a separate security task before browser smoke / E2E
  validation; this pack does not modify AuthService.
- [ ] Gateway compatibility is an implementation-time verification item; if the existing route coverage is
  insufficient, open a separate integration-agent task.

## 19. Implementation Notes

- This FU is intentionally additive. It creates a master-template layer above FU01 folder/company-scoped templates.
- `TemplateMasterVersion` should not reuse `TemplateVersion`; the latter belongs to `TemplateDocument` lifecycle.
- `TemplateMasterVersion` content must reuse the existing content storage abstraction to avoid duplicate storage patterns.
- `TemplateMaster` publish/deprecate lifecycle is technical lifecycle only; it is not formal approval.
- Adoption impact in this FU is a placeholder/basic usage summary. Full graph traversal and impact engine are later work.
- `CollectionDefinitionId` / `CanonicalId` binding is read-only consumption of MOD-0028 metadata. No structure mutation.
- Existing folder-attached templates can be copied/linked from master in a later integration slice, but master publish does
  not automatically create folder-attached templates.
- The registry currently contains MOD-0029 and MOD-0029-FU01. The FU02 preflight passed; if governance requires a registry
  row before implementation, add it as a separate governance task. This pack approval does not perform runtime
  implementation or registry mutation.

## 20. Follow-up Items

- AuthService permission seed for the six `template-masters` permission keys.
- Optional `TemplateMaster` metadata update flow if product confirms it.
- Controlled Documents integration: template master option selector for folder-attached `TemplateDocument` creation.
- Template Variant aggregate.
- Drift detection.
- Compare UI.
- Rebase engine.
- Full adoption impact engine.
- Approval workflow integration for Publish New Version / Deprecate if a later approved FU requires it.
- Template Variant approval as a separate FU.
- E-signature as a separate explicitly approved compliance scope.
- MOD-0023 workflow integration as a separate approved workflow scope.
