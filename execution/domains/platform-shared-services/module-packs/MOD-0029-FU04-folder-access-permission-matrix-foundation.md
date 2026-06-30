---
id: MOD-0029-FU04
name: Folder Access & Permission Matrix Foundation
parent: MOD-0029
previous: MOD-0029-FU03B
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: TenantScopedEntity
status: approved
owner: platform-shared-services
branch: feature/pss/mod-0029-fu04-folder-access-permission-matrix-foundation
started: 2026-06-29
target: 2026-07-27
form_field_count: 14
---

# MOD-0029-FU04 - Folder Access & Permission Matrix Foundation

## 1. Module Summary

MOD-0029-FU04, MOD-0029 parent'i (**Controlled Documents (SOPs/Work Instructions)**) altinda company,
documentation structure ve folder seviyesinde resource-level access matrix foundation tasarlar.

Bu pack **runtime implementation yapmaz**. Bu dosya, kullanici review/onayi sonrasi executable implementation
pack olarak `@orchestrator` tarafindan uygulanacak sozlesmedir.

Problem: mevcut Layer 1 global permission key'leri module/action seviyesinde calisir. Document Management icin bu
yeterli degildir; tenant icinde birden fazla company, documentation structure, folder, document/template, corporate
template master ve template variant bulunabilir. Bir kullanici Document Management ekranina girebilse bile yalnizca
yetkili oldugu folder'lari gormeli ve sadece izinli oldugu aksiyonlari yapabilmelidir.

Discovery verdict: **PARTIAL**. Mevcut runtime'da `ControlledDocument`, `TemplateDocument`,
`FolderDocumentAccessPolicy`, `DocumentAccessEvaluator` ve Explorer server-side permission filtering vardir; ancak
genel `TargetType + TargetId + PrincipalType + PrincipalId + Actions + Effect` aggregate, inheritance, deny precedence,
effective access preview ve audit-ready matrix henuz yoktur.

Runtime context:

- MOD-0029-FU03 Template Variant Drift Foundation tamamlandi.
- MOD-0029-FU03A Create Variant From Master With Folder-Attached TemplateDocument mini integration tamamlandi.
- MOD-0029-FU03B Variant Content Source Selection & Local Upload mini integration tamamlandi.
- DCP-002 preflight `MOD-0029-FU03B` child identity icin gectigi icin `previous: MOD-0029-FU03B` olarak
  netlestirilmistir.

Form field count karari: Access matrix create/edit yuzeyi sekizden fazla kullanici alani icerir (`TargetType`,
`TargetId`, `PrincipalType`, `PrincipalId`, `Actions`, `Effect`, `InheritFromParent`, `ValidFrom`, `ValidTo`,
`Reason`, `Status`, `SourcePolicyId`, `CorrelationId`, preview principal/target secimleri). Bu nedenle
`golden_reference: compact` secilmistir.

## 2. Ownership and Boundaries

### In-scope

- Yeni generalized access aggregate:
  - `DocumentAccessPolicy` adinin mevcut embedded value object ile cakisma riski oldugu icin implementation'da
    `DocumentAccessPolicyEntry` veya `ControlledDocumentAccessPolicy` adi secilmelidir. Tavsiye: **`DocumentAccessPolicyEntry`**
    (sidecar collection, audit-ready policy row).
- Access target model:
  - `Tenant`.
  - `Company`.
  - `CollectionDefinition`.
  - `CollectionInstance`.
  - `TemplateDocument`.
  - `ControlledDocument`.
  - `TemplateMaster`.
  - `TemplateVariant`.
- Principal model:
  - `User`.
  - `Role`.
  - `Group` placeholder.
  - `Company` placeholder, only as product-safe company grant.
- Action set:
  - `View`.
  - `Download`.
  - `CreateDocument`.
  - `CreateTemplate`.
  - `EditMetadata`.
  - `UploadVersion`.
  - `Publish`.
  - `Archive`.
  - `Share`.
  - `ManageAccess`.
- Future approval placeholders only:
  - `RequestApproval`.
  - `Approve`.
  - `Reject`.
  - `Review`.
- Effect model:
  - `Allow`.
  - `Deny`.
- Inheritance model:
  - company -> documentation structure -> folder.
  - folder -> `TemplateDocument` / `ControlledDocument`.
  - linked `TemplateDocument` -> `TemplateVariant` access context.
- Effective access resolver:
  - deny overrides allow.
  - nearest explicit policy wins where applicable.
  - inherited allow applies if no deny.
  - tenant isolation.
  - non-leakage for cross-tenant target.
- Effective access preview:
  - returns effective actions for user/role + target.
- TenantShell Compact UI:
  - Document Access Matrix page.
  - Folder Access Details panel/page.
  - Role/User access assignment.
  - Effective Access Preview.
- Enforcement points:
  - Explorer folder tree.
  - Folder contents list.
  - `TemplateDocument` create/upload.
  - `TemplateDocument` edit/archive/share.
  - `TemplateVariant` create target folder selector.
  - `TemplateVariant` detail/compare/rebase visibility/action gates.
  - `TemplateMaster` corporate library visibility.
- Backward compatibility:
  - existing `FolderDocumentAccessPolicy` / `DocumentAccessEvaluator` behavior must not break.
  - adapter/compatibility layer is required if replacing the existing model is risky.

### Out-of-scope

- Approval / Review implementation.
- MOD-0023 workflow integration.
- E-signature.
- Legal hold / retention.
- External sharing portal.
- Document-specific deep ACL editor.
- Complex ABAC policy engine.
- Full audit history UI.
- AuthService seed implementation.
- Gateway `ocelot.json` change.
- MOD-0028 mutation.
- New folder creation.
- `CollectionDefinition` / `CollectionInstance` editing.

## 3. Owned Objects

### Domain objects

- `DocumentAccessPolicyEntry` (recommended name; implementation may use `ControlledDocumentAccessPolicy` only if
  existing naming conflict analysis prefers it).
- `DocumentAccessTargetType`.
- `DocumentAccessPrincipalType`.
- `DocumentAccessAction`.
- `DocumentAccessEffect`.
- `DocumentAccessPolicyStatus`.

### Repositories

- `IDocumentAccessPolicyRepository`.
- Mongo repository implementation under Platform persistence.
- Indexes for tenant isolation, target lookup, principal lookup, inheritance traversal and soft-delete filtering.

### Application services

- `DocumentAccessResolver`.
- `DocumentAccessInheritanceResolver`.
- `DocumentAccessPrincipalResolver` or adapter over existing `IDocumentAccessPrincipalAccessor`.
- `DocumentAccessCompatibilityAdapter` over existing `FolderDocumentAccessPolicy` and embedded
  `DocumentAccessPolicy`.
- `DocumentAccessTargetResolver` for target validation/non-leakage.

### Commands

- `CreateDocumentAccessPolicyCommand`.
- `UpdateDocumentAccessPolicyCommand`.
- `DeleteDocumentAccessPolicyCommand`.
- `BulkDeleteDocumentAccessPolicyCommand`.

### Queries

- `GetDocumentAccessPolicyListQuery`.
- `GetDocumentAccessPolicyByIdQuery`.
- `GetEffectiveDocumentAccessQuery`.
- `GetEffectiveDocumentAccessBatchQuery`.
- `GetDocumentAccessTargetOptionsQuery`.
- `GetDocumentAccessPrincipalOptionsQuery`.

### DTOs / models

- `DocumentAccessPolicyListItemModel`.
- `DocumentAccessPolicyDetailModel`.
- `DocumentAccessPolicyInput`.
- `DocumentAccessPolicyTargetModel`.
- `DocumentAccessPrincipalModel`.
- `EffectiveDocumentAccessModel`.
- `EffectiveDocumentAccessBatchInput`.
- DataTable request/response models following existing Platform conventions.

### API endpoints

- `GET /api/v1/document-management/access-policies`.
- `GET /api/v1/document-management/access-policies/{id}`.
- `POST /api/v1/document-management/access-policies`.
- `PUT /api/v1/document-management/access-policies/{id}`.
- `DELETE /api/v1/document-management/access-policies/{id}`.
- `DELETE /api/v1/document-management/access-policies/bulk`.
- `GET /api/v1/document-management/access-policies/effective`.
- `POST /api/v1/document-management/access-policies/effective/batch`.
- `GET /api/v1/document-management/access-target-options`.
- `GET /api/v1/document-management/access-principal-options`.

### Frontend routes

- `/DocumentManagementAccessMatrix`.
- `/DocumentManagementAccessMatrix/Create`.
- `/DocumentManagementAccessMatrix/Edit/{id}`.
- `/DocumentManagementAccessMatrix/Details/{id}`.
- `/DocumentManagementAccessMatrix/Details/{targetType}/{targetId}` may be added as a read-only target detail route
  if routing ambiguity is avoided.
- Effective preview can be a modal or a full page panel under the matrix surface.

### Permission keys

- `platform.document-management.access.view`.
- `platform.document-management.access.manage`.
- `platform.document-management.access.preview`.
- `platform.document-management.access.audit.view`.

AuthService seed implementation is a separate security task and is not part of this pack-author turn.

## 4. Entity Fields

### DocumentAccessPolicyEntry

`DocumentAccessPolicyEntry` uses the Platform tenant-scoped base pattern used by neighboring MOD-0029
document-management entities. Base fields such as `Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`,
`UpdatedAt` and technical concurrency `Version` must not be redefined.

| Field | Type | Required | Rules / meaning | Index / pre-check |
|---|---|---:|---|---|
| Base | `TenantScopedEntity` | Yes | Inherits tenant, soft-delete and audit base fields according to Platform pattern. | Tenant filter required. |
| AccessPolicyId | `Guid` | Yes | Stable policy identifier; may equal entity `Id` unless separate external id is required. | Unique per tenant if separate from `Id`. |
| TargetType | `DocumentAccessTargetType` | Yes | `Tenant`, `Company`, `CollectionDefinition`, `CollectionInstance`, `TemplateDocument`, `ControlledDocument`, `TemplateMaster`, `TemplateVariant`. | Compound index with `TargetId`. |
| TargetId | `Guid` or `string` | Yes | Target identifier. `Tenant` may use tenant id; `Company` may use company/legal-entity id. | Resolve through target resolver; cross-tenant -> 404 non-leakage. |
| PrincipalType | `DocumentAccessPrincipalType` | Yes | `User`, `Role`, `Group`, `Company`. `Group` is placeholder only until a group source exists. | Compound index with `PrincipalId`. |
| PrincipalId | `string` | Yes | Typed principal id normalized as string to support role/group ids. | Validate user/role/company when reference seam exists. |
| Actions | `IReadOnlyList<DocumentAccessAction>` | Yes | Non-empty set of actions. Approval placeholders may be stored but do not execute approval runtime. | Multikey/action filter index optional. |
| Effect | `DocumentAccessEffect` | Yes | `Allow` or `Deny`. Deny takes precedence. | Indexed for resolver. |
| InheritFromParent | `bool` | Yes | Whether this policy participates in child target inheritance. | Resolver input. |
| IsInherited | `bool` | No | Computed/read model flag for preview; persisted only if implementation justifies materialized policy rows. | Prefer computed. |
| SourcePolicyId | `Guid?` | No | Parent/source policy for materialized inherited rows or audit trace. | Validate same tenant when set. |
| ValidFrom | `DateTimeOffset?` | No | Optional start boundary. | Resolver ignores policy before this time. |
| ValidTo | `DateTimeOffset?` | No | Optional end boundary; must be >= `ValidFrom`. | Resolver ignores expired policy. |
| Status | `DocumentAccessPolicyStatus` | Yes | `Active`, `Disabled`, `Archived`. | Filter index. |
| Reason | `string?` | No | Optional governance reason, max 1000. | Validator. |
| CreatedBy | `Guid?` or `string?` | Yes if not provided by base | Actor from current user context. Not accepted from DTO. | Audit. |
| UpdatedBy | `Guid?` or `string?` | On update | Actor from current user context. Not accepted from DTO. | Audit. |
| CorrelationId | `string?` | No | Last write correlation id for traceability. | Audit/search optional. |

## 5. Repo Scope

This pack-author turn may only create/update:

- `execution/domains/platform-shared-services/module-packs/MOD-0029-FU04-folder-access-permission-matrix-foundation.md`.

Future implementation scope after approval may include:

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/**`.
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/DocumentManagement/**`.
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/**`.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementAccessMatrix/**`.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementControlledDocuments/Services/**`
  for adapter/enforcement integration only.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementTemplateMasters/**`
  for access enforcement integration only.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementTemplateVariants/**`
  for access enforcement integration only.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementAccessPoliciesController.cs`.
- `services/Diten.Platform/src/Diten.Platform.API/Models/DocumentManagement/**`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/**`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs`.
- `frontend/Diten.Web/Controllers/DocumentManagementAccessMatrixController.cs`.
- `frontend/Diten.Web/Views/DocumentManagement/AccessMatrix/**`.
- `frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/AccessMatrix/**`.
- `frontend/Diten.Web/Resources/Views/DocumentManagement/AccessMatrix/**`.
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
- Existing `FolderDocumentAccessPolicy` / `DocumentAccessEvaluator` behavior cannot be broken or removed without a
  compatibility adapter and migration tests.
- Existing `ControlledDocument` / `TemplateDocument` / `TemplateVersion` behavior cannot be broken or replaced.
- Existing `TemplateMaster` / `TemplateMasterVersion` behavior cannot be broken or replaced.
- Existing `TemplateVariant` behavior cannot be broken or replaced.
- MOD-0028 `CollectionDefinition` / `CollectionInstance` mutation paths are out of scope.

## 7. Dependencies

- MOD-0029-FU01 Controlled Document Versioning & Template Sharing Foundation.
- MOD-0029-FU02 Corporate Template Master Library Foundation.
- MOD-0029-FU03 Template Variant Drift Foundation.
- MOD-0029-FU03A Create Variant From Master With Folder-Attached TemplateDocument mini integration.
- MOD-0029-FU03B Variant Content Source Selection & Local Upload mini integration.
- Existing `FolderDocumentAccessPolicy` and `DocumentAccessEvaluator` runtime.
- Existing `ControlledDocument`, `TemplateDocument`, `TemplateVersion`, `TemplateMaster`, `TemplateMasterVersion`,
  and `TemplateVariant` runtime model.
- Existing MOD-0028 `CollectionDefinition` / `CollectionInstance` read-only seams.
- Existing `Response<T>` envelope and `CustomBaseController`.
- Existing `[HasPermission]` permission enforcement.
- Existing Gateway route coverage for `/api/v1/document-management/**`; if missing, integration-agent owns route work.
- Existing TenantShell layout and 7-language localization convention for tenant modules.

## 8. Runtime Constraints

- Access policies are tenant-owned and tenant-isolated. `TenantId` is server-side resolved; request DTOs must not
  accept client-supplied `TenantId`.
- Soft delete only; no hard delete/purge in this FU.
- MOD-0028 structures are read-only. `CollectionDefinition` and `CollectionInstance` must not receive access fields.
- The new aggregate is a sidecar policy collection; it does not mutate folder hierarchy.
- Deny precedence must be deterministic and tested.
- Effective access is computed at read time. Persisted/materialized inherited rows are discouraged unless a cache
  invalidation strategy and stale-cache tests are added.
- Approval actions are placeholders only. No approval workflow, approval queue, approve/reject endpoint, e-signature,
  or MOD-0023 integration.
- Existing transitional behavior must be explicit behind migration/rollout logic.
- No raw bytes in Mongo.
- No direct public file URL.
- No Gateway `ocelot.json` modification in this module implementation.

## 9. Layout & Shell Contract

- `shell: tenant`.
- Razor layout: every `.cshtml` page in this module explicitly sets `Layout = "_LayoutTenantShell";`.
- View folder: `frontend/Diten.Web/Views/DocumentManagement/AccessMatrix/`.
- Frontend route: `/DocumentManagementAccessMatrix`.
- Frontend browser calls must go through same-origin MVC proxy or Gateway; no direct service-port `5057` calls.
- The UI is a real TenantShell access matrix surface, not a landing page.

## 10. Backend File Convention

Golden Reference Compact backend shape must be followed, adapted to `DocumentManagementAccessMatrix`:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementAccessMatrix/
├── Commands/
│   ├── CreateDocumentAccessPolicyCommand.cs
│   ├── UpdateDocumentAccessPolicyCommand.cs
│   ├── DeleteDocumentAccessPolicyCommand.cs
│   └── BulkDeleteDocumentAccessPolicyCommand.cs
├── Queries/
│   ├── GetDocumentAccessPolicyListQuery.cs
│   ├── GetDocumentAccessPolicyByIdQuery.cs
│   ├── GetEffectiveDocumentAccessQuery.cs
│   ├── GetEffectiveDocumentAccessBatchQuery.cs
│   ├── GetDocumentAccessTargetOptionsQuery.cs
│   └── GetDocumentAccessPrincipalOptionsQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── CreateDocumentAccessPolicyHandler.cs
│   │   ├── UpdateDocumentAccessPolicyHandler.cs
│   │   ├── DeleteDocumentAccessPolicyHandler.cs
│   │   └── BulkDeleteDocumentAccessPolicyHandler.cs
│   └── QueryHandlers/
│       ├── GetDocumentAccessPolicyListHandler.cs
│       ├── GetDocumentAccessPolicyByIdHandler.cs
│       ├── GetEffectiveDocumentAccessHandler.cs
│       ├── GetEffectiveDocumentAccessBatchHandler.cs
│       ├── GetDocumentAccessTargetOptionsHandler.cs
│       └── GetDocumentAccessPrincipalOptionsHandler.cs
├── Validators/
│   ├── CreateDocumentAccessPolicyValidator.cs
│   └── UpdateDocumentAccessPolicyValidator.cs
└── DocumentManagementAccessMatrixModels.cs
```

Rules:

- Commands and queries are sealed records.
- Handlers are sealed classes ending only in `Handler`; no `CommandHandler`, `QueryHandler`, or
  `RequestHandler` suffix.
- Validators are sealed classes ending only in `Validator`; no `CommandValidator` suffix.
- Controllers stay thin and dispatch MediatR requests only.
- Effective access resolver logic belongs to application services/helpers, not controller logic.

## 11. Frontend File Contract

Golden Reference Compact file set:

```text
frontend/Diten.Web/Views/DocumentManagement/AccessMatrix/
├── Index.cshtml
├── Create.cshtml
├── Edit.cshtml
├── Details.cshtml
├── _Form.cshtml
├── _Filter.cshtml
├── _DataTable.cshtml
├── _IndexL10n.cshtml
└── AccessMatrixIndex.cs

frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/AccessMatrix/
├── index.js
└── index.l10n.js

frontend/Diten.Web/Resources/Views/DocumentManagement/AccessMatrix/
└── AccessMatrixIndex.{ar,en,es,fr,ru,tr,zh}.resx
```

Compact rules:

- `_CreateEditOffcanvas.cshtml` is forbidden.
- `_DetailsQuickView.cshtml` is forbidden.
- `Index.cshtml` uses the DataTable v2 contract with `data-dt-standard="v2"`.
- `_Filter.cshtml` is inline/collapsible, not offcanvas.
- `Create`, `Edit` and `Details` are full pages and explicitly set `_LayoutTenantShell`.
- TenantShell localization requires 7-language parity: `ar`, `en`, `es`, `fr`, `ru`, `tr`, `zh`.

UI requirements:

- Filters:
  - target type.
  - principal type.
  - effect.
  - action.
  - status.
  - inherited/explicit.
- Columns:
  - Target.
  - Principal.
  - Actions.
  - Effect.
  - Inheritance.
  - Validity.
  - Status.
  - Updated.
  - Actions.
- Actions:
  - View.
  - Create/Edit.
  - Disable/Archive.
  - Effective Preview.
- Approval/review action execution buttons must not exist.
- No e-signature labels.
- No workflow labels.

## 12. Validation Rules

| Field | Required | Format / rule | DB-level | Pre-check |
|---|---:|---|---|---|
| TargetType | Yes | Enum; one of supported target types. | Indexed. | Target resolver must support type. |
| TargetId | Yes | Guid/string according to target type; trim. | Indexed with `TargetType`. | Resolve same-tenant target; missing/cross-tenant -> 404. |
| PrincipalType | Yes | Enum: `User`, `Role`, `Group`, `Company`. | Indexed. | `Group` placeholder blocked unless group seam exists. |
| PrincipalId | Yes | Trim, max 160. | Indexed with `PrincipalType`. | Validate user/role/company when reference seam exists. |
| Actions | Yes | Non-empty, distinct values; approval placeholders allowed only as inert flags. | Optional multikey index. | Unknown action -> 400. |
| Effect | Yes | `Allow` or `Deny`. | Indexed. | Deny precedence tested. |
| InheritFromParent | Yes | Boolean. | - | Only valid for inheritable target types. |
| SourcePolicyId | No | Guid, same tenant. | Optional index. | Must point to existing policy if supplied. |
| ValidFrom | No | Date/time UTC or offset. | - | Must be <= `ValidTo` when both exist. |
| ValidTo | No | Date/time UTC or offset. | Optional expiry index. | Must be >= `ValidFrom`; expired ignored by resolver. |
| Status | Yes | `Active`, `Disabled`, `Archived`; default `Active`. | Indexed. | Disabled/Archived ignored by resolver. |
| Reason | No | Trim, max 1000. | - | Validator. |
| CorrelationId | No | Max 128. | - | Usually server/context supplied. |

## 13. Failure Path to Verify

- **Missing target**
  - Expected: 404 non-leakage; no target details exposed; no policy is created.
- **Cross-tenant target**
  - Expected: 404/403 non-leakage according to target resolver; no policy is created.
- **Unsupported principal type**
  - Expected: 400 validation failure; `Group` is blocked until group source exists.
- **Duplicate policy row**
  - Expected: deterministic 409 for same tenant + target + principal + action/effect if duplicate is not allowed.
- **Deny precedence**
  - Expected: effective preview excludes denied action even when parent allow exists.
- **Expired policy**
  - Expected: resolver ignores policy after `ValidTo`; preview explains source as expired/ignored if explain metadata exists.
- **Unauthorized actor**
  - Expected: 403 permission denied; UI hides/disables manage/preview action according to Layer 1 keys.
- **TemplateVariant target folder without CreateTemplate**
  - Expected: target folder is not returned in selector; backend create returns 403 if manually submitted.
- **Existing folder access compatibility**
  - Expected: existing `FolderDocumentAccessPolicy` grants still work during transitional rollout.
- **MOD-0028 mutation attempt**
  - Expected: no `CollectionDefinition` / `CollectionInstance` write path is used.

## 14. Authorization Convention

- Policy/controller protection: `[Authorize]` for TenantShell API controllers.
- Permission format: `platform.document-management.access.{action}` because this is implemented in `Diten.Platform`.
- Layer 1 permission keys:
  - `platform.document-management.access.view`.
  - `platform.document-management.access.manage`.
  - `platform.document-management.access.preview`.
  - `platform.document-management.access.audit.view`.
- Layer 2 resource access:
  - Effective access resolver enforces target/principal/action permissions after Layer 1 passes.
  - `platform_admin` / active platform administrator bypass follows existing `[HasPermission]` infrastructure.
  - Tenant admins receive transitional full access according to rollout behavior below.
  - Tenant users require explicit or inherited resource policy after rollout.
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

- [ ] `DocumentAccessPolicyEntry` or approved equivalent sidecar aggregate is introduced as tenant-scoped.
- [ ] Access target, principal, action, effect and status enums are introduced.
- [ ] Existing embedded `DocumentAccessPolicy` value object remains compatible.
- [ ] Existing `FolderDocumentAccessPolicy` behavior remains compatible or is bridged by adapter.
- [ ] Effective access resolver computes company -> structure -> folder inheritance.
- [ ] Effective access resolver computes folder -> document/template inheritance.
- [ ] Effective access resolver connects linked `TemplateDocument` access context to `TemplateVariant`.
- [ ] Deny overrides allow.
- [ ] Nearest explicit policy wins where applicable.
- [ ] Inherited allow applies when no deny exists.
- [ ] Cross-tenant targets return 404/403 non-leakage.
- [ ] Effective preview returns expected action set for user/role + target.
- [ ] Explorer folder tree is filtered by effective `View`.
- [ ] Folder contents list is filtered by effective `View`.
- [ ] `TemplateDocument` create/upload is gated by `CreateTemplate` / `UploadVersion`.
- [ ] `TemplateDocument` edit/archive/share is gated by `EditMetadata` / `Archive` / `Share`.
- [ ] `TemplateVariant` create target folder selector returns only allowed folders.
- [ ] `TemplateVariant` detail/compare/rebase respects visibility/action gates.
- [ ] `TemplateMaster` corporate library visibility is gated by effective access.
- [ ] Tenant admin / platform admin rollout behavior is explicit and tested.
- [ ] Existing owner-company users retain transitional `View` until explicit policies are seeded or rollout flag changes.
- [ ] No MOD-0028 mutation is introduced.
- [ ] AuthService seed changes are not bundled with this implementation.
- [ ] Gateway `ocelot.json` is not modified by this module implementation unless a separate integration-agent task is opened.
- [ ] UI route `/DocumentManagementAccessMatrix` renders in TenantShell.
- [ ] All `.cshtml` pages explicitly set `Layout = "_LayoutTenantShell";`.
- [ ] DataTable v2 verifier passes with Compact reference.
- [ ] TenantShell RESX parity exists for `ar`, `en`, `es`, `fr`, `ru`, `tr`, `zh`.

## 17. Test Expectations

Backend/application tests:

- User can view allowed folder.
- User cannot view denied folder.
- Company-level access is inherited by folder.
- Folder deny overrides parent allow.
- Nearest explicit policy wins where applicable.
- User can create template only with `CreateTemplate`.
- User cannot upload version without `UploadVersion`.
- User cannot archive without `Archive`.
- User cannot manage access without `ManageAccess`.
- Template variant create shows only allowed target folders.
- Manual TemplateVariant create submission to unauthorized folder returns 403.
- Linked `TemplateDocument` inherits folder access for TemplateVariant visibility context.
- Effective access preview returns expected actions.
- Expired policy is ignored.
- Disabled/archived policy is ignored.
- Cross-tenant target returns 404/403 non-leakage.
- Existing `FolderDocumentAccessPolicy` compatibility tests remain green.
- No MOD-0028 mutation.
- No AuthService runtime change in this implementation.

Frontend / verification:

- Build `services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj`.
- Build `frontend/Diten.Web/Diten.Web.csproj`.
- Run relevant Platform tests.
- Run DataTable verifier:
  - `python3 .antigravity/scripts/verify_datatable_page.py . --area DocumentManagement --module AccessMatrix --reference compact`
- RESX parity across `ar`, `en`, `es`, `fr`, `ru`, `tr`, `zh`.
- Browser smoke: list, filters, create page, edit page, details page, effective preview, access assignment.
- Protected path verification confirms no AuthService/Gateway/MOD-0028 mutation.

## 18. Ready-for-dev Checklist

- [x] DCP-002 module identity preflight passed with
  `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0029-FU04 --name "Folder Access & Permission Matrix Foundation" --parent MOD-0029`.
- [x] Golden Reference Compact pack and live file set were read.
- [x] Frontmatter has `service`, `shell`, `golden_reference`, `entity_base`, and `form_field_count`.
- [x] `golden_reference: compact` selected because form field count is greater than 8.
- [x] Layout & Shell Contract explicitly names `_LayoutTenantShell`.
- [x] Backend File Convention uses `Commands`, `Queries`, `Handlers/CommandHandlers`, `Handlers/QueryHandlers`,
  `Validators`, and one models file.
- [x] Frontend File Contract lists the Compact file set and forbids Slim-only offcanvas/quick-view files.
- [x] Validation Rules are field-level and testable.
- [x] Failure Path to Verify includes duplicate, missing, unauthorized, inheritance, deny precedence and compatibility paths.
- [x] Authorization Convention lists permission keys and actor behavior.
- [x] Gateway routing decision is explicit and protects `ocelot.json`.
- [x] User review confirmed `previous: MOD-0029-FU03B` and FU03A/FU03B mini integration context.
- [ ] FU02/FU03/FU04 registry rows may be required before implementation; add them as a separate governance task if current
  convention requires registry mutation before executable work.
- [ ] AuthService permission seed task is required as a separate security task before browser smoke / E2E validation.
- [ ] Gateway compatibility is an implementation-time verification item; if existing route coverage is insufficient,
  open a separate integration-agent task.
- [x] Rollout flag/default behavior accepted by product/security before implementation.

## 19. Implementation Notes

- This FU is intentionally additive. It creates a generalized sidecar policy model beside the FU01 folder/document
  policy layer.
- Existing records must not need destructive migration.
- Recommended naming is `DocumentAccessPolicyEntry` to avoid confusion with the existing embedded
  `DocumentAccessPolicy` value object.
- Recommended rollout behavior:
  - Tenant admin / platform admin full access.
  - Existing owner-company users can receive transitional `View` until explicit policies are seeded.
  - Explicit policy is preferred for the new matrix.
  - The target model for normal users is default deny.
  - Transition to default deny must be controlled by migration/rollout flag.
  - Existing documents/templates inherit folder access if no explicit document policy exists.
- Default deny is the secure target for normal users, but switching immediately may break current UX. Implementation
  must include explicit migration/rollout controls.
- Effective access computation should be read-time unless materialized inheritance is explicitly justified.
- Compatibility adapter should translate existing `FolderPermissionSet` to the new action set:
  - `CanViewFolderDocuments` -> `View`, `Download` for folder listing only if accepted.
  - `CanUploadDocument` -> `CreateDocument`, `CreateTemplate`.
  - `CanEditFolderDocuments` -> `EditMetadata`.
  - `CanUploadNewVersion` -> `UploadVersion`.
  - `CanShareFolderDocuments` -> `Share`.
  - `CanManageFolderDocumentAccess` -> `ManageAccess`.

### Implementation slice proposal

1. Slice 1 - Domain + persistence:
   - `DocumentAccessPolicyEntry` entity.
   - enums.
   - repository interface/implementation.
   - Mongo indexes.
2. Slice 2 - Resolver:
   - effective access resolver.
   - inheritance resolver.
   - deny precedence.
   - compatibility adapter.
3. Slice 3 - API:
   - thin MediatR controller.
   - CRUD endpoints.
   - effective preview endpoint.
   - target/principal options endpoints.
4. Slice 4 - Enforcement integration:
   - Explorer folder tree.
   - folder contents list.
   - TemplateDocument create/upload/edit/archive/share.
   - TemplateVariant create target folder selector and detail/compare/rebase gates.
   - TemplateMaster corporate library visibility.
5. Slice 5 - Frontend:
   - MVC proxy.
   - TenantShell Compact UI.
   - DataTable v2.
   - effective preview panel/modal.
   - 7 RESX.
6. Slice 6 - Tests/verifiers:
   - application tests.
   - frontend build.
   - API build.
   - DataTable verifier.
   - RESX parity.
   - protected path verification.

## 20. Follow-up Items

- Add/verify FU02, FU03 and FU04 rows in `execution/registries/module-id-registry.md` if governance requires registry
  rows before implementation.
- AuthService permission seed for:
  - `platform.document-management.access.view`.
  - `platform.document-management.access.manage`.
  - `platform.document-management.access.preview`.
  - `platform.document-management.access.audit.view`.
- Gateway explicit route task only if existing `/api/v1/document-management/**` coverage is insufficient.
- Full audit history UI.
- Document-specific deep ACL editor.
- Group principal runtime support.
- Complex ABAC policy engine.
- Formal approval workflow.
- Approval queue.
- Approver assignment.
- E-signature.
- MOD-0023 workflow integration.
- Legal hold / retention.
- External sharing portal.
- MOD-0028 structure extension only if a future approved pack explicitly requires it.
