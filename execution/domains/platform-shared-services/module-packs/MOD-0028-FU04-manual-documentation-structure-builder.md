---
id: MOD-0028-FU04
name: Documentation Management Manual Structure Baseline Builder
parent: MOD-0028
previous: MOD-0028-FU03
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: TenantScopedEntity
status: approved
owner: platform-shared-services
branch: feature/pss/mod-0028-fu04-manual-documentation-structure-builder
started: 2026-06-16
target: 2026-06-30
form_field_count: 11
---

# MOD-0028-FU04 - Documentation Management Manual Structure Baseline Builder

## 1. Module Summary

MOD-0028-FU04 is a backend + TenantShell follow-up to `MOD-0028 Documentation Management`. It builds on:

- `MOD-0028-FU01 Backend Contract Foundation`
- `MOD-0028-FU02 QMS Workbook Import Profile for Structure Baselines`
- `MOD-0028-FU03 Tenant Structure Baselines UI`

FU04 lets tenant users create and maintain a documentation structure manually, without Excel import. The manual builder
operates only on `DRAFT` baselines. `PUBLISHED` baselines are immutable and every edit/move/delete attempt against a
published baseline returns a controlled failure.

### Naming reconciliation

This FU04 pack is semantically renamed from **Manual Documentation Structure Builder** to **Manual Structure Baseline
Builder**. It is a general structure builder for tenant-owned documentation structures: QMS, HR, Finance, Legal,
Project, Supplier, Audit, and future categories. QMS is only one `SourceProfile`/`StructureCategory`, not the product
route or menu name.

Recommended canonical terms:

- Menu: `Documentation Structures`
- List/catalog: `Structure Baselines`
- Manual builder: `Manual Structure Baseline Builder`
- Tree: `CollectionDefinition tree`
- QMS import: `Import QMS Workbook`

Preferred backend route and permission family is `structure-baselines`. Any existing `qms-baselines` route,
controller, frontend folder, JS namespace, or permission key is transitional compatibility naming and should be renamed
or aliased before wider rollout.

### FU03 status context

- FU03 frontend implementation is code-complete.
- FU03 final validation verdict is `PARTIAL` only because the DataTable verifier and runtime browser smoke were
  environment-bound and could not run.
- No FU03 implementation defect was found.
- FU03 remains import/list/detail/tree/publish UI only.
- FU04 must not modify FU03 except where it consumes existing UI/API conventions or optionally links to the manual
  designer through a separately approved navigation touch.

### Approval scope

- This pack is `status: approved` for the exact FU04 manual documentation structure builder scope only.
- The user explicitly approved the FU04 scope: manual tenant-scoped `DRAFT` baseline creation; manual root node
  creation; manual child node creation; draft node metadata edit; draft node move/reorder; draft node soft-delete;
  draft tree validation before publish; TenantShell manual designer/tree editor UI; controlled
  `reason_code`/`correlation_id` failures; permission-gated backend endpoints and frontend controls; Gateway method
  widening as a separate integration-agent task; and permission seed/alias as a separate MOD-0018/security task if
  protected.
- The approval is **not** for: company adoption, `CollectionInstance` provisioning, MOD-0220 LegalEntity adoption,
  MOD-0029 controlled document lifecycle, MOD-0030 retention/legal hold, MOD-0031 evidence export, physical folder
  creation, document upload/storage, binary/content repository work, template management, exception workflow, or
  editing `PUBLISHED` baselines.
- Controlled gates remain in force: Gateway method widening, permission seed/alias ownership, DCP-002/registry
  governance, and canonical-id/full-path-after-move algorithm documentation before coding.

## 2. Ownership and Boundaries

### In scope

- Create a new tenant-scoped manual `DRAFT` structure baseline without Excel import.
- Add root `CollectionDefinition` nodes under a `DRAFT` baseline.
- Add child nodes under an existing same-tenant draft node.
- Edit draft node metadata.
- Move/reorder draft nodes.
- Soft-delete draft nodes.
- Validate the draft tree before publish.
- TenantShell manual baseline designer and tree editor.
- Permission-gated UI controls and backend endpoints for create/edit/move/delete/validate.
- Controlled `reason_code` + `correlation_id` failure behavior.
- Gateway method widening as a separate `integration-agent` task.
- Permission seed/alias changes only through a separate MOD-0018/security-owned task when the seed path is protected.

### Consumed, not owned

- FU01 route family, `Response<T>` envelope, `reason_code`, body-level `correlation_id`, feature flags, and
  permission-alias conventions.
- FU02 persisted objects and import/publish contract patterns.
- FU03 TenantShell list/detail/tree/publish UI conventions and existing publish flow.
- MOD-0018 permission ownership and effective-key approval.
- MOD-0032/Gateway ownership for `ocelot.json`.

### Explicitly out of scope

- FU03 import wizard changes except an optional link/navigation entry to the manual designer.
- Company adoption, `CollectionInstance`, provisioning, reconciliation, or MOD-0220 LegalEntity adoption.
- MOD-0029 controlled document lifecycle.
- MOD-0030 retention/legal-hold enforcement.
- MOD-0031 evidence export or evidence-pack assembly.
- Physical folder creation.
- Document upload, binary storage, content repository implementation, or template management.
- Exception workflow.
- Local node/company-specific overrides unless separately scoped.
- Editing, moving, deleting, or mutating `PUBLISHED` baselines.

## 3. Owned Objects

FU04 owns the manual-builder command/query/API/UI surface around existing MOD-0028 baseline objects:

- Manual `BaselineRelease` creation request/result contracts.
- Manual `CollectionDefinition` create/update/move/delete request/result contracts.
- Draft tree validation request/result contracts.
- Application services/helpers for tree validation, sibling uniqueness, path/canonical-id recalculation, and move safety.
- Backend endpoints under the preferred `api/v1/document-management/structure-baselines` family.
- TenantShell manual designer views, scripts, L10n bridge, and frontend smoke/specs.
- Permission requirements for manual create/edit/move/delete/validate actions.

FU04 does not introduce `CollectionInstance`, `CorporateDocumentationRoot`, `LocalCollectionNode`, `TemplateMaster`,
`TemplateMasterVersion`, `TemplateVariant`, `Exception`, `ContentRef`, or `ProvisioningJob`.

## 4. Entity Fields

FU04 extends mutation behavior for the FU02-owned tenant-scoped objects. It does not create a new aggregate type unless
implementation inspection proves a small command/result model is needed in Application only.

All persisted business records remain tenant-owned and follow the live Platform convention recorded by FU01/FU02:
`TenantScopedEntity` or the confirmed canonical equivalent. `TenantId`, `IsDeleted`, technical `Version`, audit actor,
and correlation identity are server-side concerns and are never accepted from client payloads.

| Object | Fields used by FU04 | Rules |
|---|---|---|
| BaselineRelease | BaselineReleaseId, BaselineVersion, Name/Title if supported by existing contract, ChangeSummary, EffectiveDate, Status, VersionToken | Manual creation creates `DRAFT` only; `PUBLISHED` immutable; business version must not use reserved `Version` |
| CollectionDefinition | CanonicalId, ParentCanonicalId, Name, PurposeScope, RequiredByScope, AllowedDocClass, DefaultClassificationLevel/Classification, DefaultRetentionHint, PathSegment, FullPath, DisplayOrder, IsMandatory, IsProtected, VersionToken, IsDeleted, DeletedAt if available | Node mutations allowed only while parent baseline is `DRAFT`; sibling uniqueness is case-insensitive among active nodes; full path/canonical behavior remains deterministic after move |
| Draft validation result | Errors, warnings, invalidHierarchyFindings, duplicateSiblingFindings, orphanParentFindings, valid flag | Persisted only if repo convention requires validation history; otherwise response-only |

Form field count is 11 because the manual builder may expose baseline fields plus node metadata fields:
baseline version/name/change summary/effective date and node name/parent/purpose/required scope/document class/
classification/retention/order/flags. This selects `golden_reference: compact`.

## 5. Repo Scope

### Authorized implementation scope after approval

- `services/Diten.Platform/src/Diten.Platform.API/**`
- `services/Diten.Platform/src/Diten.Platform.Application/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**`
- `services/Diten.Platform/tests/**`
- `frontend/Diten.Web/**`
- Frontend tests/smoke specs if the repo has a convention.

### Separately governed scope

- `gateway/Diten.ApiGateway/**/ocelot.json` only through an explicit `integration-agent` task to widen
  `/api/v1/document-management/{everything}` methods.
- `services/Diten.AuthService/**` only through a separately approved MOD-0018/security-owned permission seed/alias
  task if runtime seeds or aliases live there.

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` except through the separate integration-agent task in section 15.
- `services/Diten.AuthService/**` except through a separately approved MOD-0018/security-owned task.
- `services/Diten.Platform.Common/**`
- `services/Diten.MdmService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- MOD-0029, MOD-0030, and MOD-0031 implementation files.
- Binary storage internals, external repository internals, and any physical file-system folder creation.
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- Parent, FU01, FU02, and FU03 pack files unless a separate governance reconciliation authorizes an update.

## 7. Dependencies

| Dependency | FU04 usage |
|---|---|
| MOD-0028 parent | Supplies documentation-structure ownership, tenant scope, baseline objects, and out-of-scope boundaries |
| MOD-0028-FU01 | Supplies route family, `Response<T>`, `reason_code`, `correlation_id`, feature-flag and permission conventions |
| MOD-0028-FU02 | Supplies `CollectionDefinition`, `BaselineRelease`, publish semantics, deterministic tree/hash expectations, and QMS import profile behavior |
| MOD-0028-FU03 | Supplies TenantShell structure-baseline list/detail/tree/publish UI conventions consumed by the manual designer |
| MOD-0018 | Owns permission seed/alias approval for new lowercase runtime keys |
| MOD-0021 | Supplies audit/correlation seams consumed by future mutations |
| MOD-0032 / Gateway | Owns Ocelot route hardening and method widening |
| TenantShell | Supplies `_LayoutTenantShell`, navigation, toast/confirm, shared API profile, permission-gate conventions |

No MOD-0220 LegalEntity dependency is introduced by FU04 because company adoption is deferred to FU05.

## 8. Runtime Constraints

- Manual editing is allowed only on `DRAFT` baselines.
- `PUBLISHED` baselines are immutable; edit/move/delete/manual-node-create attempts return controlled 400 or 409 per
  repository convention.
- Manual baseline creation creates a tenant-scoped `DRAFT` baseline only; it does not create company adoption records.
- The UI never sends `TenantId`; tenant context is resolved server-side.
- Soft delete is mandatory for draft node delete; no hard delete path is authorized.
- Moving a node must preserve tree integrity: no cycles, no orphan parent, no duplicate active sibling path segment,
  no invalid path/control characters, and deterministic `FullPath` / canonical-id behavior after move.
- Names may contain `/` as atomic folder names only if the backend contract supports it; path building must not split
  an atomic slash-containing name.
- Publish requires a valid tree; FU04 may reuse FU03 publish flow after validation succeeds.
- Mutations use optimistic concurrency. Stale technical version/version token returns 409 `CONFLICT`.
- All browser calls go through Gateway `5000` or the existing same-origin MVC proxy. Client JS must never call Platform
  API `5057` directly.
- Controlled failures include stable `reason_code` and `correlation_id`; stack traces and internal exception text are
  never returned or rendered.
- No physical folder, document file, binary content, template, or company instance is created by FU04.

## 9. Layout & Shell Contract

- Primary shell: `shell: tenant`.
- Primary Razor layout: every FU04 `.cshtml` page declares `Layout = "_LayoutTenantShell";` explicitly.
- Primary actor type: `tenant_user`.
- Preferred view folder: `frontend/Diten.Web/Views/DocumentManagement/StructureBaselines/`.
- Transitional view folder: `frontend/Diten.Web/Views/DocumentManagement/QmsBaselines/` may remain only if already
  implemented before the naming reconciliation; new UI labels must still say `Documentation Structures` /
  `Structure Baselines`.
- FU04 must not use `_LayoutPlatformAdmin.cshtml`, the frozen `_Layout.cshtml`, or archive layouts.
- `_ViewStart.cshtml` is not changed.
- Platform Admin shell is not used for manual documentation-structure governance.

## 10. Backend File Convention

FU04 follows the Compact Golden Reference action-based CQRS convention with module-specific action names. Preferred
new naming uses `StructureBaseline`; already-created `QmsBaseline` class/file names are transitional and should be
renamed or wrapped with compatibility aliases before wider rollout.

```text
Features/DocumentManagementStructureBaseline/
|-- Commands/
|   |-- CreateManualStructureBaselineCommand.cs
|   |-- CreateStructureBaselineDefinitionCommand.cs
|   |-- UpdateStructureBaselineDefinitionCommand.cs
|   |-- MoveStructureBaselineDefinitionCommand.cs
|   `-- DeleteStructureBaselineDefinitionCommand.cs
|-- Queries/
|   |-- GetStructureBaselineDesignerQuery.cs
|   `-- GetStructureBaselineDefinitionByIdQuery.cs
|-- Handlers/
|   |-- CommandHandlers/
|   |   |-- CreateManualStructureBaselineHandler.cs
|   |   |-- CreateStructureBaselineDefinitionHandler.cs
|   |   |-- UpdateStructureBaselineDefinitionHandler.cs
|   |   |-- MoveStructureBaselineDefinitionHandler.cs
|   |   `-- DeleteStructureBaselineDefinitionHandler.cs
|   `-- QueryHandlers/
|       |-- GetStructureBaselineDesignerHandler.cs
|       `-- GetStructureBaselineDefinitionByIdHandler.cs
|-- Validators/
|   |-- CreateManualStructureBaselineValidator.cs
|   |-- CreateStructureBaselineDefinitionValidator.cs
|   |-- UpdateStructureBaselineDefinitionValidator.cs
|   |-- MoveStructureBaselineDefinitionValidator.cs
|   `-- ValidateStructureBaselineDraftValidator.cs
|-- Services/
|   |-- StructureBaselineDraftTreeValidator.cs
|   |-- StructureBaselineDefinitionPathBuilder.cs
|   `-- StructureBaselineDefinitionMovePlanner.cs
`-- DocumentManagementStructureBaselineModels.cs
```

Naming rules:

- Commands and queries are sealed records.
- Handlers are sealed classes named `{Verb}{Slice}Handler`; no `CommandHandler`, `QueryHandler`, or `RequestHandler`
  suffix.
- Validators are named `{Verb}{Slice}Validator`; no `CommandValidator` suffix.
- Mutating commands return `Response<NoContent>` or a typed result, never `Response<bool>`.
- Controllers inherit `CustomBaseController`, remain thin, and dispatch through MediatR.
- Tree validation/move complexity belongs in Application services/helpers, not oversized handlers.

## 11. Frontend File Contract

`golden_reference: compact` applies because FU04 uses route-based designer/detail/edit flows and exposes more than
eight user-editable fields. Shared list/DataTable conventions follow GoldenReferenceCompact where a list appears.
Tree-editor UX is spec-specific and must still reuse existing TenantShell controls, toast/confirm, and API profile.

Proposed surfaces:

- Baseline list entry/link reuse from FU03, if approved and non-invasive.
- Manual baseline designer landing page.
- Manual baseline create page/form or modal according to live TenantShell convention.
- Tree editor with add root, add child, edit metadata, move/reorder, soft-delete, validate draft, and publish handoff.
- Node details/edit surface.
- Validation summary panel showing errors, warnings, duplicate siblings, orphan parents, and hierarchy findings.

Proposed Compact file set, final path confirmed during implementation inspection:

```text
Views/DocumentManagement/StructureBaselines/
|-- Index.cshtml
|-- CreateManual.cshtml
|-- Designer.cshtml
|-- Details.cshtml
|-- _Form.cshtml
|-- _Filter.cshtml
|-- _DataTable.cshtml
|-- _IndexL10n.cshtml
|-- _TreeEditor.cshtml
|-- _ValidationSummary.cshtml
`-- StructureBaselinesIndex.cs

wwwroot/assets/js/DocumentManagement/StructureBaselines/
|-- index.js
|-- index.l10n.js
|-- designer.js
`-- designer.l10n.js
```

Transitional implementation path if already created before rename: `Views/DocumentManagement/QmsBaselines/` and
`wwwroot/assets/js/DocumentManagement/QmsBaselines/`. Those paths should be renamed before broad release if feasible;
otherwise keep them as internal compatibility paths while all visible labels use `Structure Baselines`.

Compact constraints:

- No `_CreateEditOffcanvas.cshtml` or `_DetailsQuickView.cshtml` unless a later approved scope deliberately chooses a
  Slim surface.
- Any DataTable list uses `data-dt-standard="v2"` and the skeleton loader convention.
- Browser JS uses the existing same-origin proxy or shared Gateway API SSOT; no direct `5057`, no fabricated success,
  no manual bearer-token scraping from cookies.
- All visible strings use the repo localization/L10n bridge convention.

## 12. Validation Rules

| Field / action | Required | Rule | DB-level / pre-check | Failure |
|---|---|---|---|---|
| BaselineVersion | Yes | Non-empty semantic business version; do not name payload field `Version` if it conflicts with technical concurrency | Tenant + baseline release uniqueness if supported | 400 or 409 |
| Baseline name/title | If supported | Trimmed, max length per existing contract | Validator | 400 |
| ChangeSummary | Yes | Non-empty, trimmed, bounded length | Validator | 400 |
| EffectiveDate | If needed | Valid date; no invalid tenant-local date | Validator | 400 |
| Baseline status | Yes | Manual create produces `DRAFT` only | Handler guard | 400 |
| Node Name | Yes | Trimmed; not empty; bounded length; no invalid path/control characters | Validator + sibling uniqueness | 400 or 409 |
| ParentCanonicalId | For child/move | Existing same-tenant active parent within same draft baseline; root node uses null/empty per contract | Parent lookup; orphan check | 400 or 404 |
| PurposeScope | Optional/contractual | Trimmed bounded text | Validator | 400 |
| RequiredByScope | Optional/contractual | Allowed values only; no POSITION/PERSON activation | Validator/feature flags | 400 |
| AllowedDocClass | Optional | Approved value or free text only if existing contract allows | Contract validation | 400 |
| Classification | Optional | Approved classification/reference value if enforced by existing contract | Contract validation | 400 |
| Retention hint | Optional | Bounded string; hint only, no MOD-0030 enforcement | Validator | 400 |
| DisplayOrder | Yes for move/reorder | Non-negative integer; deterministic ordering among siblings | Move planner | 400 |
| Mandatory/protected flags | If supported | Cannot weaken protected imported/manual nodes unless existing contract allows | Handler guard | 400/403 |
| VersionToken | Mutations | Must match current mutable record | Optimistic concurrency | 409 |
| TenantId | Never | Server-resolved only; never accepted in body/query/form | Contract test | 400/test failure |

Tree validation rules:

- No empty node/folder names.
- No duplicate active sibling name/path segment under the same parent.
- No cycles.
- No orphan parent.
- No invalid path/control characters.
- Slash-containing names are atomic if the backend supports them.
- Publish requires a valid tree.
- Draft-only edit/move/delete.
- Published edit attempts return controlled failure.

## 13. Failure Path to Verify

- **Edit published baseline:** 400 `VALIDATION_FAILED` or 409 `CONFLICT`; no mutation, no fabricated UI success.
- **Duplicate sibling:** 409 `CONFLICT`; new/update/move does not persist.
- **Invalid hierarchy:** 400 `VALIDATION_FAILED`; validation summary identifies cycle/orphan/invalid parent.
- **Invalid node name/path:** 400 `VALIDATION_FAILED`; no stack trace.
- **Stale version token:** 409 `CONFLICT`; UI requires refresh/retry and never silently overwrites.
- **Missing permission:** 403 `PERM_DENIED`; no state change; controls hidden/disabled where possible.
- **Cross-tenant detail/mutation:** 404 `NOT_FOUND_NON_LEAKAGE`; no restricted identifier is leaked.
- **Publish invalid draft:** 400 `VALIDATION_FAILED`; publish handoff stops and shows validation findings.
- **Gateway method missing:** frontend PUT/PATCH/DELETE smoke fails through Gateway until integration-agent widening is
  complete; direct `5057` fallback is prohibited.
- **No physical/document storage:** tests and inspection prove no file-system folder, document, or binary content is
  created.
- All controlled failures include `reason_code` and `correlation_id`; stack traces/internal exception text are absent.

## 14. Authorization Convention

- Policy: `[Authorize]` on tenant-facing MOD-0028 API controllers.
- Actor type: `tenant_user`.
- Runtime canonical permission format: PKS-001 lowercase dotted keys under
  `platform.document-management.{resource}.{action}`.
- Do not invent uppercase aliases unless parent spec / Enterprise Architect / MOD-0018 confirms them.
- Backend `[HasPermission]`, frontend gates, seeds, and aliases must resolve to the same lowercase effective key.

### New FU04 permission keys

| Permission key | Used by |
|---|---|
| `platform.document-management.structure-baselines.create` | Manual DRAFT baseline creation |
| `platform.document-management.collection-definitions.create` | Add root/child node |
| `platform.document-management.collection-definitions.edit` | Edit node metadata |
| `platform.document-management.collection-definitions.move` | Move/reorder node |
| `platform.document-management.collection-definitions.delete` | Soft-delete draft node |
| `platform.document-management.structure-baselines.validate` | Validate draft tree |

### Reused permission keys

| Permission key | Used by |
|---|---|
| `platform.document-management.structure-baselines.view` | List/detail/designer read |
| `platform.document-management.structure-baselines.publish` | Publish handoff |
| `platform.document-management.collection-definitions.list` | Tree load |
| `platform.document-management.collection-definitions.view` | Node detail |

Transitional aliases, if already implemented before this reconciliation:

| Transitional key | Preferred replacement |
|---|---|
| `platform.document-management.qms-baselines.create` | `platform.document-management.structure-baselines.create` |
| `platform.document-management.qms-baselines.validate` | `platform.document-management.structure-baselines.validate` |
| `platform.document-management.qms-baselines.view` | `platform.document-management.structure-baselines.view` |
| `platform.document-management.qms-baselines.publish` | `platform.document-management.structure-baselines.publish` |

Permission seed/alias ownership:

- FU04 may add local Platform constants/attributes within authorized scope.
- If runtime seed/alias changes require protected `services/Diten.AuthService/**` or another security-owned path,
  implementation stops and a separate MOD-0018/security task is required.
- Missing seed may leave validation partial, but release cannot close until seed, alias, backend policy, and frontend
  gates agree.

## 15. Gateway / API Routing Decision

Decision: Gateway method widening is required and is a separate `integration-agent` task.

Existing route status:

- `/api/v1/document-management/{everything}` currently supports `GET`, `POST`, and `OPTIONS` for FU03/FU02 usage.
- FU04 introduces `PUT`, `PATCH`, and `DELETE` methods.

Required route after FU04:

- Route family: `/api/v1/document-management/{everything}`
- Required methods: `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `OPTIONS`
- No `v2` route.
- No unversioned route.
- Frontend uses Gateway `5000` or a same-origin MVC proxy only.
- `gateway/Diten.ApiGateway/**/ocelot.json` remains protected and is modified only by integration-agent.

Preferred candidate endpoints, names may adjust to repo convention but must remain under `api/v1/document-management`:

| Method | Path | Permission | Behavior |
|---|---|---|---|
| POST | `api/v1/document-management/structure-baselines/manual` | `...structure-baselines.create` | Create manual tenant-scoped `DRAFT` baseline |
| POST | `api/v1/document-management/structure-baselines/{id}/definitions` | `...collection-definitions.create` | Add root/child node to draft baseline |
| PUT | `api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}` | `...collection-definitions.edit` | Edit draft node metadata |
| PATCH | `api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}/move` | `...collection-definitions.move` | Move/reorder draft node |
| DELETE | `api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}` | `...collection-definitions.delete` | Soft-delete draft node |
| POST | `api/v1/document-management/structure-baselines/{id}/validate` | `...structure-baselines.validate` | Validate draft tree before publish |

Transitional route aliases, if already implemented before the naming reconciliation:

| Legacy path | Preferred replacement |
|---|---|
| `api/v1/document-management/qms-baselines/manual` | `api/v1/document-management/structure-baselines/manual` |
| `api/v1/document-management/qms-baselines/{id}/definitions` | `api/v1/document-management/structure-baselines/{id}/definitions` |
| `api/v1/document-management/qms-baselines/{id}/definitions/{canonicalId}` | `api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}` |
| `api/v1/document-management/qms-baselines/{id}/definitions/{canonicalId}/move` | `api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}/move` |
| `api/v1/document-management/qms-baselines/{id}/validate` | `api/v1/document-management/structure-baselines/{id}/validate` |

Sequencing:

- Backend implementation may be tested directly against Platform API during development if needed.
- Browser/frontend consumption and final release require Gateway widening PASS.
- No direct service-port fallback is allowed in frontend code.

## 16. Acceptance Criteria

- [x] FU04 pack is promoted to `status: approved` after explicit user approval of the FU04 manual-builder scope.
- [ ] Scope is manual documentation structure builder only.
- [ ] Manual baseline creation creates a tenant-scoped `DRAFT` baseline without Excel import.
- [ ] Manual edit/add/move/delete works only on `DRAFT` baselines.
- [ ] `PUBLISHED` baselines are immutable and edit attempts return controlled failure.
- [ ] Backend endpoints are version-explicit under `api/v1/document-management`.
- [ ] Frontend surfaces use `_LayoutTenantShell` explicitly.
- [ ] Frontend calls Gateway/same-origin proxy only; no direct `5057`.
- [ ] Permission-gated controls and backend `[HasPermission]` use the same lowercase effective keys.
- [ ] Controlled failures include `reason_code` and `correlation_id`; no stack traces or fabricated success.
- [ ] Tree validation covers empty names, duplicate siblings, cycles, orphan parents, invalid path/control characters,
  stale versions, and draft-only mutation.
- [ ] Moving/reordering nodes yields deterministic full path and canonical-id behavior according to the chosen repo
  convention.
- [ ] Gateway method widening is explicitly a separate integration-agent task.
- [ ] Permission seed/alias work is explicitly a separate MOD-0018/security-owned task if protected.
- [ ] Company adoption is explicitly deferred to FU05.
- [ ] Document lifecycle is explicitly deferred to MOD-0029.
- [ ] No physical folder creation, document upload, or binary storage is introduced.

## 17. Test Expectations

Backend tests:

- Create manual `DRAFT` baseline.
- Add root node.
- Add child node.
- Edit draft node metadata.
- Move/reorder node.
- Soft-delete draft node.
- Duplicate sibling conflict.
- Invalid hierarchy validation: cycle, orphan parent, invalid parent, invalid path/control character.
- Cannot edit/move/delete `PUBLISHED` baseline.
- Validate draft tree.
- Deterministic canonical ID / full path after move according to selected algorithm.
- Tenant isolation: cross-tenant detail/mutation returns 404 non-leakage.
- Missing permission returns 403 `PERM_DENIED`.
- Controlled errors assert `reason_code` and `correlation_id`.
- No physical folder, document storage, or binary content side effect.

Frontend tests/smoke:

- Manual designer opens in TenantShell.
- Add root, add child, edit metadata, move/reorder, delete draft node, validate draft flows.
- Publish handoff reuses existing FU03 publish flow if possible.
- Permission-gated controls hide/disable and direct denied calls surface controlled errors.
- `correlation_id` appears in support/error detail.
- No direct `5057` literal or service-port call in client JS.
- DataTable verifier for any list surface:
  `python3 .antigravity/scripts/verify_datatable_page.py . --area DocumentManagement --module StructureBaselines --reference compact`.
  If the transitional `QmsBaselines` frontend path is retained temporarily, run the verifier against that module name
  as well and record the compatibility status.
- Browser smoke through frontend `5001` and Gateway `5000` only after Gateway widening.

Build and quality:

- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`
- Relevant Platform application/API/authorization/persistence tests.
- `git diff --check` and protected-path verification.
- Gateway method widening validation by integration-agent after route update.

## 18. Ready-for-dev Checklist

- [x] User reviewed this pack and explicitly approved the FU04 manual-builder scope.
- [x] Frontmatter contains required fields: service, shell, golden reference, entity base, status, branch, dates, and
  form field count.
- [x] Golden Reference Compact was used as the structure reference for route-based list/form/designer conventions.
- [x] Layout & Shell Contract explicitly names `_LayoutTenantShell`.
- [x] Backend File Convention includes `Handlers/CommandHandlers/` and `Handlers/QueryHandlers/` split.
- [x] Frontend File Contract uses Compact conventions and excludes Slim-only offcanvas/quick-view files.
- [x] Validation Rules, Failure Paths, Authorization, Gateway, Acceptance Criteria, and Test Expectations are explicit.
- [ ] DCP-002 preflight is run successfully when Python/openpyxl is available:
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0028 --name "Documentation & Evidence Management"`.
  Current authoring environment returned `Python bulunamadı`, so this remains a controlled gate.
- [ ] Registry/EA status for `MOD-0028-FU04` as a child of MOD-0028 is recorded if governance requires follow-up rows.
- [ ] Existing FU02/FU03 backend DTOs and frontend API profile are inspected before implementation begins.
- [ ] Gateway method widening task is created for integration-agent and accepted.
- [ ] MOD-0018/security task is created if permission seed/alias changes touch protected ownership.
- [ ] The chosen canonical-id/full-path-after-move algorithm is documented before coding.
- [ ] Tenant language L10n key set is prepared before UI work.
- [x] User changed status to `approved` for FU04 implementation within this pack's approved boundaries.

## 19. Implementation Notes

- FU04 is the concrete realization of the future manual-builder need recorded in FU03 section 20. It must not reopen
  FU03 import/list/detail/tree/publish implementation beyond consuming conventions or adding an explicitly approved
  navigation link.
- Manual creation is not Excel import. It creates a `DRAFT` baseline and lets the user build the tree directly.
- Manual node deletion is soft delete only. The implementation must preserve governed lineage.
- Tree movement is the risky part of FU04. Implement it through a planner/validator service so handlers remain small
  and tests can prove no cycles, orphans, duplicate sibling path segments, or silent overwrites occur.
- The route family existing in Gateway is not enough for FU04 because PUT/PATCH/DELETE are not currently allowed.
- Permission keys are lowercase PKS-001 runtime keys. Uppercase MOD0028 aliases are not invented in this pack.
- Company adoption is the next business step but remains FU05. A manual baseline becoming `PUBLISHED` does not create
  company instances.
- MOD-0029 owns controlled document lifecycle. FU04 only builds documentation structure metadata.
- No implementation should treat folders as physical file-system folders; these are governed structure nodes only.

### Approved Implementation Handoff

- Next executable action: the orchestrator may implement **FU04 backend + frontend only** within this approved scope.
- Invoke:
  `@orchestrator execution/domains/platform-shared-services/module-packs/MOD-0028-FU04-manual-documentation-structure-builder.md`.
- Allowed:
  - DRAFT manual baseline creation.
  - `CollectionDefinition` root/child create.
  - `CollectionDefinition` metadata edit.
  - node move/reorder.
  - node soft-delete only; no hard delete.
  - draft tree validation.
  - TenantShell manual designer/tree editor.
  - frontend API integration via Gateway/same-origin proxy only.
  - focused backend, frontend, authorization, tenant-isolation, Gateway, and smoke tests.
- Not allowed:
  - company adoption.
  - `CollectionInstance`.
  - MOD-0220 LegalEntity adoption.
  - MOD-0029 controlled document lifecycle.
  - MOD-0030 retention/legal hold.
  - MOD-0031 evidence export.
  - physical folder creation.
  - document upload/storage or binary/content repository work.
  - direct `5057` browser calls.
  - editing, moving, deleting, or otherwise mutating `PUBLISHED` baselines.
- Orchestrator must stop before touching protected paths if Gateway widening or permission-seed/alias ownership
  requires the separate controlled task. The canonical-id/full-path-after-move algorithm must be documented before
  coding move/reorder behavior.

## 20. Follow-up Items

1. **MOD-0028-FU05 - Company Adoption / CollectionInstance Provisioning:** apply a `PUBLISHED` baseline to a company /
   LegalEntity and create company-scoped instances. This includes MOD-0220 LegalEntity selection/validation and remains
   separate from FU04.
2. **MOD-0029 - Controlled Document Lifecycle:** document create/draft/review/approve/effective/archive, versioning,
   workflow, ownership, lifecycle audit, and content-reference integration.
3. **Gateway integration task:** widen `/api/v1/document-management/{everything}` to `GET, POST, PUT, PATCH, DELETE,
   OPTIONS`.
4. **MOD-0018/security task:** seed/alias any new FU04 permission keys if protected security-owned paths are required.
5. **L10n closure:** prepare tenant-language resource keys for the manual designer and validation summary.
6. **Accessibility/observability pass:** keyboard tree editing, focus restoration after modals, audit/correlation
   display, and release-gate smoke after implementation.
