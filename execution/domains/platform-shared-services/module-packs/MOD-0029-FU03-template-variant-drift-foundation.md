---
id: MOD-0029-FU03
name: Template Variant Drift Foundation
parent: MOD-0029
previous: MOD-0029-FU02
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: TenantScopedEntity
status: approved
owner: platform-shared-services
branch: feature/pss/mod-0029-fu03-template-variant-drift-foundation
started: 2026-06-29
target: 2026-07-20
form_field_count: 18
---

# MOD-0029-FU03 - Template Variant Drift Foundation

## 1. Module Summary

MOD-0029-FU03, MOD-0029 parent'i (**Controlled Documents (SOPs/Work Instructions)**) altında
`Document Template Variants` capability'sini planlar. Amaç, FU02 ile eklenen corporate
`TemplateMaster` / `TemplateMasterVersion` kayıtlarından türeyen company / business unit / site scoped
template variant kayıtlarını yönetmek ve drift / compare / rebase foundation'ını kurmaktır.

Bu pack **runtime implementation yapmaz**; kullanıcı review sonrası kapsamı kabul etmiş ve pack
`approved` durumuna taşınmıştır. Runtime geliştirme için kullanıcı ayrıca
`@orchestrator execution/domains/platform-shared-services/module-packs/MOD-0029-FU03-template-variant-drift-foundation.md`
çağırmalıdır.

Kavramsal karar: **Hybrid model** kullanılır.

| Concept | Meaning |
|---|---|
| `TemplateMaster` | Corporate reusable master template. |
| `TemplateMasterVersion` | Master version lifecycle. |
| `TemplateVariant` | Company / BU / site scoped governance + drift record. |
| `TemplateDocument` | Folder-attached runtime template item olarak kalır. |
| `TemplateVariantVersion` | Bu FU'da uygulanmaz. |

Form field count kararı: `TemplateVariant` create/edit yüzeyinde sekizden fazla kullanıcı alanı vardır
(`TemplateMasterId`, `TemplateMasterVersionId`, `VariantCode`, `VariantName`, `Description`, `ScopeType`,
`ScopeId`, owner alanları, status, rebase lineage, linked document, local changes, approval placeholder ve
blocked reason). Bu nedenle `golden_reference: compact` seçilmiştir.

## 2. Ownership and Boundaries

### In-scope

- Yeni tenant-scoped aggregate:
  - `TemplateVariant`.
- Yeni enumlar:
  - `TemplateVariantScopeType`: `Company`, `BusinessUnit`, `Site`.
  - `TemplateVariantStatus`: `Draft`, `Active`, `Deprecated`, `Archived`.
  - `TemplateVariantDriftStatus`: `InSync`, `RebaseRequired`, `Drifted`, `Blocked`.
  - `TemplateVariantApprovalStatus`: `NotRequired`, `Pending`, `Approved`, `Rejected`, `Blocked`.
- Drift foundation:
  - Drift status read-time computed olmalıdır.
  - Persisted drift cache eklenirse stale kalma riski açıkça dokümante edilmeli ve cache invalidation testi
    eklenmelidir.
- Rebase foundation:
  - Basic controlled rebase metadata operation.
  - `LastRebasedMasterVersionId`, `LastRebasedMasterVersionNumber`, `LastRebasedAt` güncellenir.
  - `HasLocalChanges = false` yapılır.
  - Computed drift status `InSync` olur.
- Compare foundation:
  - Metadata-level comparison sonucu döner.
  - Binary diff, content merge ve checksum-based deep comparison yoktur.
- TenantShell Compact UI:
  - Template Variants list/detail/create/compare/rebase yüzeyi.
  - DataTable v2.
  - 7 dil RESX paritesi.

### Out-of-scope

- `TemplateVariantVersion` aggregate.
- Full binary diff engine.
- Full merge/rebase engine.
- Content overwrite during rebase.
- `TemplateDocument` / `TemplateVersion` içeriğinin otomatik overwrite edilmesi.
- Approval workflow.
- Approval queue.
- Approver assignment.
- E-signature.
- MOD-0023 workflow integration.
- Template variant approval actions.
- Approve/Reject UI buttonları.
- Full adoption impact engine.
- Governance dashboard.
- Deviation queue.
- Evidence export.
- MOD-0028 structure mutation.
- `CollectionDefinition` editing.
- `CollectionInstance` mutation.
- AuthService seed implementation.
- Gateway `ocelot.json` modification.

## 3. Owned Objects

### Domain objects

- `TemplateVariant`.
- `TemplateVariantScopeType`.
- `TemplateVariantStatus`.
- `TemplateVariantDriftStatus`.
- `TemplateVariantApprovalStatus`.

### Repositories

- `ITemplateVariantRepository`.
- Mongo repository implementation under Platform persistence.
- Mongo indexes for tenant isolation, duplicate code prevention, master lookup, scope filters and soft-delete.

### Commands

- `CreateTemplateVariantCommand`.
- `RebaseTemplateVariantCommand`.

Metadata update is not in the first required slice. If product later asks for edit behavior, add
`UpdateTemplateVariantCommand` through this pack revision or a follow-up pack.

### Queries

- `GetTemplateVariantListQuery`.
- `GetTemplateVariantByIdQuery`.
- `GetTemplateVariantCompareQuery`.
- `GetTemplateVariantOptionsQuery`.
- `GetTemplateMasterVariantsQuery`.

### DTOs / models

- `TemplateVariantListItemModel`.
- `TemplateVariantDetailModel`.
- `TemplateVariantCompareModel`.
- `TemplateVariantOptionModel`.
- `CreateTemplateVariantInput`.
- `RebaseTemplateVariantInput`.
- DataTable request/response models following existing Platform conventions.

### API endpoints

- `GET /api/v1/document-management/template-variants`.
- `GET /api/v1/document-management/template-variants/{id}`.
- `POST /api/v1/document-management/template-variants`.
- `GET /api/v1/document-management/template-variants/{id}/compare`.
- `POST /api/v1/document-management/template-variants/{id}/rebase`.
- `GET /api/v1/document-management/template-variant-options`.
- `GET /api/v1/document-management/template-masters/{id}/variants`.

### Frontend routes

- `/DocumentManagementTemplateVariants`.
- `/DocumentManagementTemplateVariants/Create`.
- `/DocumentManagementTemplateVariants/Details/{id}`.
- Compare can be a metadata modal/page.
- Rebase can be a basic controlled operation from the list or details surface.

### Permission keys

- `platform.document-management.template-variants.view`.
- `platform.document-management.template-variants.create`.
- `platform.document-management.template-variants.compare`.
- `platform.document-management.template-variants.rebase`.
- `platform.document-management.template-variants.manage`.

AuthService seed implementation is a separate security task and is not part of this pack-author turn.

## 4. Entity Fields

### TemplateVariant

`TemplateVariant` uses the Platform tenant-scoped base pattern used by neighboring MOD-0029 document-management
entities. Base fields such as `Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt` and the
technical concurrency `Version` must not be redefined in the entity if the concrete base already provides them.

| Field | Type | Required | Rules / meaning | Index / pre-check |
|---|---|---:|---|---|
| Base | `TenantScopedEntity` | Yes | Inherits tenant, soft-delete and audit base fields according to Platform document-management pattern. | Tenant filter required. |
| TemplateMasterId | `Guid` | Yes | Source corporate master. Must exist in same tenant and must not be deprecated/archived for create. | Indexed; reference pre-check. |
| TemplateMasterVersionId | `Guid` | Yes | Master version used to create/rebase the variant. Must belong to `TemplateMasterId`. | Indexed; reference pre-check. |
| VariantCode | `string` | Yes | Trim, normalize, max 80, stable business identifier. | Unique per `TenantId + ScopeType + ScopeId + VariantCode + IsDeleted=false`; duplicate returns 409. |
| VariantName | `string` | Yes | Trim, max 256. | Text/filter index optional. |
| Description | `string?` | No | Trim, max 2000. | - |
| ScopeType | `TemplateVariantScopeType` | Yes | `Company`, `BusinessUnit`, or `Site`. | Indexed for filter. |
| ScopeId | `Guid` or `string` | Yes | Scope identifier matching `ScopeType`; validation seam must be explicit at implementation time. | Indexed with `ScopeType`. |
| OwnerCompanyId | `Guid?` | No | Optional company/legal-entity owner. | Validate via approved reference seam when available. |
| OwnerUserId | `Guid?` | No | Optional user owner. | Validate only through approved user-reference seam. |
| Status | `TemplateVariantStatus` | Yes | `Draft`, `Active`, `Deprecated`, `Archived`. | Indexed for list filter. |
| LastRebasedMasterVersionId | `Guid?` | No | Last master version explicitly rebased into the variant. | Must belong to same master when set. |
| LastRebasedMasterVersionNumber | `int?` | No | Business version number; do not use field name `Version`. | Used for computed drift. |
| LastRebasedAt | `DateTimeOffset?` | No | Set by rebase operation. | - |
| CurrentVariantVersionId | `Guid?` | No | Placeholder pointer for future variant versioning; no `TemplateVariantVersion` in this FU. | Nullable only. |
| LinkedTemplateDocumentId | `Guid?` | No | Optional folder-attached runtime template item link. Must not overwrite content on rebase. | Validate same tenant when available. |
| HasLocalChanges | `bool` | Yes | Indicates local variant divergence. | Used for computed drift. |
| ApprovalStatus | `TemplateVariantApprovalStatus` | Yes | Metadata/read-only placeholder only. No approval workflow. | Filterable. |
| ApprovalRequestId | `Guid?` | No | Placeholder external/workflow reference; no MOD-0023 integration in this FU. | Must not drive actions. |
| BlockedReason | `string?` | No | Required when approval status is `Blocked` or implementation sets blocked state. Max 1000. | - |
| CreatedBy | `Guid?` or `string?` | Yes if not provided by base | Actor from current user context. Not accepted from DTO. | - |
| UpdatedBy | `Guid?` or `string?` | On update/rebase | Actor from current user context. Not accepted from DTO. | - |

### Computed drift rules

Drift status priority is deterministic and read-time computed:

1. `Blocked`
   - Master status is `Deprecated` or `Archived`; or
   - variant `ApprovalStatus == Blocked`.
2. `Drifted`
   - `HasLocalChanges == true`.
3. `RebaseRequired`
   - master `CurrentMasterVersion > LastRebasedMasterVersionNumber`; and
   - `HasLocalChanges == false`.
4. `InSync`
   - master `CurrentMasterVersion == LastRebasedMasterVersionNumber`; and
   - `HasLocalChanges == false`.

If `LastRebasedMasterVersionNumber` is null, implementation must choose and test an explicit behavior
(`RebaseRequired` preferred for published/active masters unless create initializes it).

## 5. Repo Scope

This pack-author turn may only create/update:

- `execution/domains/platform-shared-services/module-packs/MOD-0029-FU03-template-variant-drift-foundation.md`.

Future implementation scope after approval may include:

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/**`.
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/DocumentManagement/**`.
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/**`.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementTemplateVariants/**`.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementTemplateVariantsController.cs`.
- `services/Diten.Platform/src/Diten.Platform.API/Models/DocumentManagement/**`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/**`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs`.
- `frontend/Diten.Web/Controllers/DocumentManagementTemplateVariantsController.cs`.
- `frontend/Diten.Web/Views/DocumentManagement/TemplateVariants/**`.
- `frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/TemplateVariants/**`.
- `frontend/Diten.Web/Resources/Views/DocumentManagement/TemplateVariants/**`.
- `services/Diten.Platform/tests/**`.
- Frontend verifier artifacts if present.

## 6. Protected Paths

- `.antigravity/**`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `gateway/Diten.ApiGateway/**/ocelot.json` unless a separate integration-agent task is explicitly opened.
- `services/Diten.AuthService/**` for this pack; permission seed is a separate security task.
- `services/Diten.MdmService/**`.
- `services/Diten.EnterpriseStrategyService/**`.
- `services/Diten.DevEnablementService/**` except read-only Golden Reference comparison.
- Existing `TemplateMaster` / `TemplateMasterVersion` behavior cannot be broken or replaced.
- Existing `TemplateDocument` / `TemplateVersion` behavior cannot be broken or replaced.
- MOD-0028 `CollectionDefinition` / `CollectionInstance` mutation paths are out of scope.

## 7. Dependencies

- MOD-0029-FU01 Controlled Document Versioning & Template Sharing Foundation.
- MOD-0029-FU02 Corporate Template Master Library Foundation.
- MOD-0029-FU02A Baseline Definition Tree to Related Template Masters mini integration, as existing context.
- Existing `TemplateMaster` and `TemplateMasterVersion` runtime model.
- Existing `TemplateDocument` and `TemplateVersion` runtime model.
- Existing `Response<T>` envelope and `CustomBaseController`.
- Existing `[HasPermission]` permission enforcement.
- Existing Gateway route coverage for `/api/v1/document-management/**`; if missing, integration-agent owns route work.
- Existing TenantShell layout and 7-language localization convention for tenant modules.

## 8. Runtime Constraints

- `TemplateVariant` is tenant-owned and tenant-isolated. `TenantId` is server-side resolved; request DTOs must not
  accept client-supplied `TenantId`.
- Soft delete only; no hard delete/purge in this FU.
- Business version fields must be named `LastRebasedMasterVersionNumber` / equivalent; do not use the reserved
  technical `Version` property for business semantics.
- Drift status is read-time computed. Persisted drift cache is discouraged; if added, stale cache risk and
  invalidation tests are mandatory.
- No full binary diff engine.
- No full merge/rebase engine.
- No checksum-based deep comparison unless checksum is already safely available from existing metadata.
- Compare endpoint may return metadata-level placeholder only.
- Rebase updates metadata only; it does not merge content, change binary/file data, or overwrite
  `TemplateDocument` / `TemplateVersion` content.
- Approval status is metadata/read-only placeholder only. No approval workflow, approval queue, approve/reject
  endpoint, e-signature, or MOD-0023 integration.
- No raw bytes in Mongo.
- No direct public file URL.
- No MOD-0028 structure mutation.

## 9. Layout & Shell Contract

- `shell: tenant`.
- Razor layout: every `.cshtml` page in this module explicitly sets `Layout = "_LayoutTenantShell";`.
- View folder: `frontend/Diten.Web/Views/DocumentManagement/TemplateVariants/`.
- Frontend route: `/DocumentManagementTemplateVariants`.
- Frontend browser calls must go through same-origin MVC proxy or Gateway; no direct service-port `5057` calls.
- The UI is a real TenantShell template variant management surface, not a landing page.

## 10. Backend File Convention

Golden Reference Compact backend shape must be followed, adapted to `DocumentManagementTemplateVariants`:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementTemplateVariants/
├── Commands/
│   ├── CreateTemplateVariantCommand.cs
│   └── RebaseTemplateVariantCommand.cs
├── Queries/
│   ├── GetTemplateVariantListQuery.cs
│   ├── GetTemplateVariantByIdQuery.cs
│   ├── GetTemplateVariantCompareQuery.cs
│   ├── GetTemplateVariantOptionsQuery.cs
│   └── GetTemplateMasterVariantsQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── CreateTemplateVariantHandler.cs
│   │   └── RebaseTemplateVariantHandler.cs
│   └── QueryHandlers/
│       ├── GetTemplateVariantListHandler.cs
│       ├── GetTemplateVariantByIdHandler.cs
│       ├── GetTemplateVariantCompareHandler.cs
│       ├── GetTemplateVariantOptionsHandler.cs
│       └── GetTemplateMasterVariantsHandler.cs
├── Validators/
│   ├── CreateTemplateVariantValidator.cs
│   └── RebaseTemplateVariantValidator.cs
└── DocumentManagementTemplateVariantsModels.cs
```

Rules:

- Commands and queries are sealed records.
- Handlers are sealed classes ending only in `Handler`; no `CommandHandler`, `QueryHandler`, or
  `RequestHandler` suffix.
- Validators are sealed classes ending only in `Validator`; no `CommandValidator` suffix.
- Controllers stay thin and dispatch MediatR requests only.
- Drift calculation belongs to an application service/helper such as `TemplateVariantService`, not to controller logic.
- Rebase logic is metadata-only and must not call binary/content merge code.

## 11. Frontend File Contract

Golden Reference Compact file set:

```text
frontend/Diten.Web/Views/DocumentManagement/TemplateVariants/
├── Index.cshtml
├── Create.cshtml
├── Details.cshtml
├── _Form.cshtml
├── _Filter.cshtml
├── _DataTable.cshtml
├── _IndexL10n.cshtml
└── TemplateVariantsIndex.cs

frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/TemplateVariants/
├── index.js
└── index.l10n.js

frontend/Diten.Web/Resources/Views/DocumentManagement/TemplateVariants/
└── TemplateVariantsIndex.{ar,en,es,fr,ru,tr,zh}.resx
```

Compact rules:

- `_CreateEditOffcanvas.cshtml` is forbidden.
- `_DetailsQuickView.cshtml` is forbidden.
- `Index.cshtml` uses the DataTable v2 contract with `data-dt-standard="v2"`.
- `_Filter.cshtml` is inline/collapsible, not offcanvas.
- `Create` and `Details` are full pages and explicitly set `_LayoutTenantShell`.
- TenantShell localization requires 7-language parity: `ar`, `en`, `es`, `fr`, `ru`, `tr`, `zh`.

UI requirements:

- Filters:
  - master.
  - scope type.
  - drift status.
  - approval status placeholder.
- Columns:
  - Variant.
  - Derived Master.
  - Last Rebased.
  - Drift Status.
  - Scope.
  - Owner.
  - Approval.
  - Actions.
- Actions:
  - View.
  - Compare.
  - Rebase.
- Approve/Reject buttons must not exist.
- No e-signature labels.
- No workflow labels.

## 12. Validation Rules

| Field | Required | Format / rule | DB-level | Pre-check |
|---|---:|---|---|---|
| TemplateMasterId | Yes | Guid; same-tenant master; create blocked if master is deprecated/archived. | Indexed. | Reference check; 404 non-leakage for missing/cross-tenant. |
| TemplateMasterVersionId | Yes | Guid; must belong to `TemplateMasterId`. | Indexed. | Reference check. |
| VariantCode | Yes | Trim, normalize, max 80. | Unique tenant + scope + code + non-deleted. | Duplicate check returns 409. |
| VariantName | Yes | Trim, max 256. | - | Validator. |
| Description | No | Max 2000. | - | Validator. |
| ScopeType | Yes | Enum: `Company`, `BusinessUnit`, `Site`. | Filter index. | Enum validator. |
| ScopeId | Yes | Guid/string matching `ScopeType`; exact reference seam must be explicit. | Compound index with `ScopeType`. | Reference validation when seam exists. |
| OwnerCompanyId | No | Guid. | Optional filter index. | Validate through approved reference seam when available. |
| OwnerUserId | No | Guid. | - | Validate through approved user seam when available. |
| Status | Yes | Enum: `Draft`, `Active`, `Deprecated`, `Archived`. | Filter index. | Enum validator. |
| LastRebasedMasterVersionId | No | Guid; must belong to same master if provided. | Optional index. | Reference check on rebase. |
| LastRebasedMasterVersionNumber | No | Positive int when set. | - | Must match selected master version number on rebase. |
| LastRebasedAt | No | Date/time set by system during rebase. | - | Not accepted from create DTO unless implementation explicitly justifies it. |
| CurrentVariantVersionId | No | Guid placeholder; no `TemplateVariantVersion` creation in this FU. | - | Nullable only. |
| LinkedTemplateDocumentId | No | Guid; same-tenant template document when linked. | Optional index. | Reference check; no content overwrite. |
| HasLocalChanges | Yes | Boolean; default false on create unless explicitly set by product rule. | - | Used for computed drift. |
| ApprovalStatus | Yes | Enum placeholder only. | Filter index. | No workflow side effect. |
| ApprovalRequestId | No | Guid placeholder only. | - | Must not trigger MOD-0023 integration. |
| BlockedReason | No | Max 1000; required when `ApprovalStatus == Blocked`. | - | Validator. |

## 13. Failure Path to Verify

- **Create from deprecated master**
  - Expected: 409 conflict or 400 validation failure; no `TemplateVariant` is created.
- **Create from archived master**
  - Expected: 409 conflict or 400 validation failure; no `TemplateVariant` is created.
- **Duplicate VariantCode in same tenant/scope**
  - Expected: 409 conflict; existing record unchanged.
- **Missing master or cross-tenant master**
  - Expected: 404 non-leakage; no master details exposed.
- **Invalid master version**
  - Expected: 400/404 according to reference seam; version must belong to the selected master.
- **Invalid scope**
  - Expected: validation failure; no variant created.
- **Drift computation with local changes**
  - Expected: `Drifted` wins over rebase-required when `HasLocalChanges == true`.
- **Blocked approval placeholder**
  - Expected: computed drift `Blocked`; no approval workflow or approve/reject action exists.
- **Rebase missing variant**
  - Expected: 404 non-leakage.
- **Rebase blocked variant**
  - Expected: deterministic 409 if business rule blocks rebase; no metadata change.
- **Unauthorized actor**
  - Expected: 403 permission denied; UI hides/disables gated action.
- **Existing TemplateMaster / TemplateDocument regression**
  - Expected: existing master, document and version flows remain compatible.

## 14. Authorization Convention

- Policy/controller protection: `[Authorize]` for TenantShell API controllers.
- Permission format: `platform.document-management.template-variants.{action}` because this is implemented in
  `Diten.Platform`.
- Layer 1 permission keys:
  - `platform.document-management.template-variants.view`.
  - `platform.document-management.template-variants.create`.
  - `platform.document-management.template-variants.compare`.
  - `platform.document-management.template-variants.rebase`.
  - `platform.document-management.template-variants.manage`.
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

- [ ] `TemplateVariant` is introduced as a tenant-scoped aggregate.
- [ ] `TemplateVariantScopeType`, `TemplateVariantStatus`, `TemplateVariantDriftStatus`, and
  `TemplateVariantApprovalStatus` are introduced as domain enums.
- [ ] `TemplateVariantVersion` is not introduced.
- [ ] Existing `TemplateMaster` records remain valid and are not migrated.
- [ ] Existing `TemplateDocument` records remain valid and are not migrated.
- [ ] Create variant from published/active master succeeds.
- [ ] Create variant from deprecated master is blocked.
- [ ] Create variant from archived master is blocked.
- [ ] Duplicate `VariantCode` per tenant/scope is rejected with deterministic 409.
- [ ] Drift status is read-time computed using the priority order `Blocked` > `Drifted` > `RebaseRequired` > `InSync`.
- [ ] Persisted drift cache is not used, or stale-cache risk plus invalidation tests are explicitly implemented.
- [ ] Compare endpoint returns metadata-level comparison and no binary diff.
- [ ] Basic rebase updates `LastRebasedMasterVersionId`, `LastRebasedMasterVersionNumber`, and `LastRebasedAt`.
- [ ] Basic rebase clears `HasLocalChanges`.
- [ ] Rebase does not merge content.
- [ ] Rebase does not overwrite `TemplateDocument` / `TemplateVersion` content.
- [ ] UI route `/DocumentManagementTemplateVariants` renders in TenantShell.
- [ ] All `.cshtml` pages explicitly set `Layout = "_LayoutTenantShell";`.
- [ ] DataTable v2 verifier passes with Compact reference.
- [ ] UI filters include master, scope type, drift status and approval status placeholder.
- [ ] UI columns include Variant, Derived Master, Last Rebased, Drift Status, Scope, Owner, Approval and Actions.
- [ ] UI actions include View, Compare and Rebase only for this FU.
- [ ] Approve/Reject buttons do not exist.
- [ ] No e-signature labels, workflow labels or approval workflow routes are created.
- [ ] AuthService seed changes are not bundled with this implementation; they are tracked as a separate security task.
- [ ] Gateway `ocelot.json` is not modified by this module implementation unless a separate integration-agent task is opened.

## 17. Test Expectations

Backend/application tests:

- Create variant from published master.
- Cannot create variant from deprecated master.
- Cannot create variant from archived master.
- Duplicate variant code per tenant/scope rejected.
- Drift status `InSync`.
- Drift status `RebaseRequired`.
- Drift status `Drifted`.
- Drift status `Blocked`.
- Compare metadata placeholder returns expected result.
- Basic rebase updates `LastRebasedMasterVersionId`, `LastRebasedMasterVersionNumber`, `LastRebasedAt`.
- Rebase clears `HasLocalChanges`.
- Existing `TemplateMaster` tests not broken.
- Existing `TemplateDocument` / `TemplateVersion` tests not broken.
- No MOD-0028 mutation.
- No approval workflow created.
- No raw bytes in Mongo.
- No direct public file URL.

Frontend / verification:

- Build `services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj`.
- Build `frontend/Diten.Web/Diten.Web.csproj`.
- Run relevant Platform tests.
- Run DataTable verifier:
  - `python3 .antigravity/scripts/verify_datatable_page.py . --area DocumentManagement --module TemplateVariants --reference compact`
- RESX parity across `ar`, `en`, `es`, `fr`, `ru`, `tr`, `zh`.
- Browser smoke: list, filters, create page, details page, compare metadata modal/page, rebase action.
- Protected path verification confirms no AuthService/Gateway/MOD-0028 mutation.

## 18. Ready-for-dev Checklist

- [x] DCP-002 module identity preflight passed with
  `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0029-FU03 --name "Template Variant Drift Foundation" --parent MOD-0029`.
- [x] Golden Reference Compact pack and live file set were read.
- [x] Frontmatter has `service`, `shell`, `golden_reference`, `entity_base`, and `form_field_count`.
- [x] `golden_reference: compact` selected because form field count is greater than 8.
- [x] Layout & Shell Contract explicitly names `_LayoutTenantShell`.
- [x] Backend File Convention uses `Commands`, `Queries`, `Handlers/CommandHandlers`, `Handlers/QueryHandlers`,
  `Validators`, and one models file.
- [x] Frontend File Contract lists the Compact file set and forbids Slim-only offcanvas/quick-view files.
- [x] Validation Rules are field-level and testable.
- [x] Failure Path to Verify includes duplicate, missing, unauthorized, lifecycle, drift and compatibility paths.
- [x] Authorization Convention lists permission keys and actor behavior.
- [x] Gateway routing decision is explicit and protects `ocelot.json`.
- [x] User reviewed and accepted the draft scope.
- [x] User confirmed status transition to `approved`.
- [ ] FU02 registry row may still be missing; verify governance expectation before implementation.
- [ ] FU03 registry row may be required before implementation; add it as a separate governance task if current
  convention requires registry mutation before executable work.
- [ ] AuthService permission seed task is required as a separate security task before browser smoke / E2E validation.
- [ ] Gateway compatibility is an implementation-time verification item; if existing route coverage is insufficient,
  open a separate integration-agent task.

## 19. Implementation Notes

- This FU is intentionally additive. It adds `TemplateVariant` beside the FU02 master-template layer without
  converting existing `TemplateDocument` records.
- Existing `TemplateMaster` records remain valid.
- Existing `TemplateDocument` records remain valid.
- `TemplateVariant` is a new collection; no forced migration is required.
- Existing records do not need backfill.
- FU02 `CollectionDefinitionId` / `CanonicalId` deferred validation gap is inherited only indirectly through
  master binding; FU03 must not mutate MOD-0028 structure to compensate for it.
- `ApprovalStatus` is a placeholder/read-only metadata field, not a workflow state machine.
- `ApprovalRequestId` is a placeholder reference only and must not introduce MOD-0023 integration in this FU.
- Compare is metadata-level. Optional checksum equality can be included only if checksum is already safely
  available in existing metadata.
- Rebase is metadata-only. It updates the last-rebased master lineage and clears local-change state; it does not
  perform binary merge, content replacement or folder-attached template overwrite.

### Implementation slice proposal

1. Slice 1 - Domain + persistence:
   - `TemplateVariant` entity.
   - enums.
   - repository interface/implementation.
   - Mongo indexes.
2. Slice 2 - Application:
   - create command.
   - rebase command.
   - list/detail/by-master/options/compare queries.
   - handlers.
   - validators.
   - models.
   - `TemplateVariantService`.
3. Slice 3 - API:
   - thin MediatR controller.
   - permission gates.
4. Slice 4 - Frontend:
   - MVC proxy.
   - TenantShell Compact UI.
   - DataTable v2.
   - compare modal/page.
   - rebase action.
   - 7 RESX.
5. Slice 5 - Tests/verifiers:
   - application tests.
   - frontend build.
   - API build.
   - DataTable verifier.
   - RESX parity.
   - protected path verification.

## 20. Follow-up Items

- FU02 registry row verification/addition if governance requires it.
- FU03 registry row addition if governance requires it before implementation.
- AuthService permission seed for the five `template-variants` permission keys.
- Gateway explicit route task only if existing `/api/v1/document-management/**` coverage is insufficient.
- `TemplateVariantVersion` aggregate.
- Full binary diff engine.
- Full merge/rebase engine.
- Content overwrite/merge policy, if product explicitly approves it later.
- Formal approval workflow.
- Approval queue.
- Approver assignment.
- E-signature.
- MOD-0023 workflow integration.
- Full adoption impact engine.
- Governance dashboard.
- Deviation queue.
- Evidence export.
- MOD-0028 structure extension if future variant behavior requires structure mutation.
