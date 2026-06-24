---
id: MOD-0028
name: Documentation Management
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: BaseEntity
status: approved
owner: platform-shared-services
branch: feature/pss/mod-0028-documentation-management
started: 2026-06-15
target: 2026-06-22
form_field_count: 19
---

# MOD-0028 - Documentation Management

## 1. Module Summary

MOD-0028 is the tenant-scoped backbone governance module for documentation collection architecture, baseline releases, company adoption, template masters and variants, exceptions, and provisioning/reconciliation job lineage. ERP modules consume MOD-0028; MOD-0028 is not embedded inside an ERP business module.

- Canonical functional specification: `MOD-0028_Documentation_Management_Spec_v2_3_0.md`.
- Specification governance: **PASS**.
- Direct-coding readiness: **NOT YET**.
- Required first action: read-only Wave 1 Profile C inspection using prompt `MOD0028-P0-INSPECT-V230-ALIGNMENT`.
- This is an **inspection-first readiness pack**, not authorization to start state-changing implementation.
- No implementation prompt may start until Wave 1 returns `PASS` or an explicitly controlled `CONDITIONAL PASS`.

### Module Identity Gate

- Blueprint lookup result: `MOD-0028 = Documentation & Evidence Management`.
- Registry result: `MOD-0028 = Documentation & Evidence Management`; deprecated aliases include `MOD-0028-document-management` and `MOD-0028-document-managementController`.
- v2.3.0 specification and this requested pack name use `Documentation Management`.
- Interim module-pack/runtime display name: `Documentation Management`.
- Enterprise Architect decision gate:
  - If MOD-0031 remains the separate owner of Evidence Pack assembly/export, preserve `Documentation Management` as the MOD-0028 pack/display name and mark `Documentation & Evidence Management` as a legacy/broad alias in canonical sources.
  - If the Blueprint canonical name remains unchanged, update this pack's frontmatter `name` to `Documentation & Evidence Management` and record `Documentation Management` as the v2.3.0 functional/spec alias.
- Recommended outcome:
  - Preserve the MOD-0028 pack/display name as `Documentation Management`.
  - Treat `Documentation & Evidence Management` as a legacy/broad canonical alias unless the Enterprise Architect decides otherwise.
  - Rationale: MOD-0031 remains the owner of Evidence Pack assembly/export, so MOD-0028 must not expand into evidence-pack ownership.
  - If the Enterprise Architect rejects this recommendation, change frontmatter `name` to `Documentation & Evidence Management` and record `Documentation Management` as the v2.3.0 functional/spec alias.
- **BLOCKER / 🔴 TBD:** The Enterprise Architect must choose and record one of the two reconciliation outcomes above. Until then, the requested/spec name does not match the current Blueprint and registry canonical name, and this pack cannot become `approved` or `ready-for-dev`.
- The Python preflight could not run because Python is unavailable on this workstation; the workbook was inspected directly and confirms the mismatch.
- No new ID is created. The existing `MOD-0028` identity remains in use.

### Documentation Structure Naming Decision

Decision date: 2026-06-16.

MOD-0028 must not model its general documentation-governance surface as "QMS Baselines". QMS is only the first
source profile/category proven by FU02/FU03, not the product-wide concept. The canonical MOD-0028 product vocabulary is:

| Concept | Canonical wording | Notes |
|---|---|---|
| Tenant menu | `Documentation Structures` | TenantShell navigation/display label |
| List/catalog | `Structure Baselines` or `Documentation Structure Baselines` | Prefer the shorter `Structure Baselines` in dense UI |
| Publishable baseline record | `StructureBaseline` / `BaselineRelease` | `BaselineRelease` may remain the persisted entity name |
| Tree | `CollectionDefinition tree` | Existing aggregate name remains valid and general |
| QMS import | `QMS Workbook Import Profile` | A QMS-specific source adapter/profile for structure baselines |
| QMS category | `SourceProfile = QMS` or `StructureCategory = QualityManagement` | QMS is metadata, not the route/product name |

Backend naming decision:

- Preferred long-term route family: `/api/v1/document-management/structure-baselines`.
- QMS workbook import should sit under the generic family as a profile/source adapter, for example
  `/api/v1/document-management/structure-baselines/import/qms/dry-run`.
- Existing `/api/v1/document-management/qms-baselines` names are now classified as a **narrow transitional route
  family** created by FU02/FU03/FU04 before this reconciliation.
- If runtime implementation has already spread, keep `/qms-baselines` only as a backward-compatible alias/shim during a
  deprecation window; no new feature should be designed against it.
- If implementation is still easy to rename before release closure, rename to `/structure-baselines` now and add the
  alias only if an external/browser consumer already depends on `/qms-baselines`.

Permission naming decision:

- Preferred keys:
  - `platform.document-management.structure-baselines.view`
  - `platform.document-management.structure-baselines.import`
  - `platform.document-management.structure-baselines.publish`
  - `platform.document-management.structure-baselines.create`
  - `platform.document-management.structure-baselines.validate`
  - `platform.document-management.collection-definitions.*`
- Existing `platform.document-management.qms-baselines.*` keys are transitional aliases for QMS-era FU02/FU03/FU04
  work and require a MOD-0018/security-owned migration plan before runtime seed/alias changes.

Impact on follow-up packs:

- FU02 title/semantics change to **QMS Workbook Import Profile for Structure Baselines**.
- FU03 title/semantics change to **Tenant Structure Baselines UI**, with a QMS-specific import action labeled
  `Import QMS Workbook`.
- FU04 title/semantics change to **Manual Structure Baseline Builder** and is general for HR, Finance, Legal, Project,
  Supplier, Audit, QMS, and future tenant-owned documentation structures.
- FU05 company adoption should target `structure-baselines/{id}/adoptions`, not `qms-baselines/{id}/adoptions`.
- Gateway widening remains on `/api/v1/document-management/{everything}` and therefore supports either concrete
  sub-route; the route-name migration itself is an API/controller/frontend/proxy/security reconciliation, not a
  Gateway path-family expansion.

## 2. Ownership and Boundaries

### In scope

- Corporate/group documentation collection definitions inside a tenant.
- Published baseline releases and immutable snapshot manifests.
- Tenant corporate documentation root and company instantiation/adoption.
- Company binding to MOD-0220 LegalEntity by authoritative GUID.
- Controlled local collection nodes where the parent permits extensions.
- Template masters, immutable published versions, company variants, drift, and rebase metadata.
- Time-boxed documentation governance exceptions.
- Provisioning and reconciliation job records, progress, and safe failure lineage.
- Metadata/report export protected by MOD-0028 row-level security.

### Consumed, not owned

- MOD-0018 RBAC/ABAC Authorization.
- MOD-0021 Audit Trail Service and audit store/query.
- MOD-0023 Workflow Designer only when enabled.
- MOD-0030 Retention and Legal Hold enforcement.
- MOD-0031 Evidence Pack assembly/export.
- MOD-0048 Reference Data Management.
- MOD-0220 Legal Entity contract from `Diten.MdmService`.
- External binary content repository.

### Explicitly out of scope

- Controlled document lifecycle; owned by MOD-0029.
- Retention engine or legal-hold enforcement; owned by MOD-0030.
- Evidence pack assembly/export; owned by MOD-0031.
- Workflow engine; owned by MOD-0023.
- Audit store/query; owned by MOD-0021.
- Binary upload/download or repository implementation.
- ERP-specific document manager positioning.
- Primary governance screens under Platform Admin shell.
- Runtime activation of `POSITION` or `PERSON` collection scope.

## 3. Owned Objects

- `CollectionDefinition`
- `BaselineRelease`
- `BaselineSnapshotManifest`
- `CorporateDocumentationRoot`
- `CollectionInstance`
- `ScopeBinding` (embedded value object)
- `CollectionBinding`
- `LocalCollectionNode`
- `TemplateMaster`
- `TemplateMasterVersion`
- `TemplateVariant`
- `Exception`
- `ContentRef` (embedded external pointer; never binary content or repository token)
- `ProvisioningJob`

MOD-0028 also owns semantic APIs under `api/v1/document-management`, MOD-0028 audit event emission, tenant-facing governance pages, and canonical permission keys listed in section 14.

## 4. Entity Fields

All persisted objects are tenant-owned and provisionally use `BaseEntity`. This is the default decision for tenant-owned Mongo persistence in `Diten.Platform`, subject to Wave 1 confirmation against the live service convention. If inspection identifies a different canonical base class for tenant-scoped Platform entities, frontmatter and this section must be updated before implementation. In every outcome, `TenantId`, soft-delete, technical concurrency, and applicable audit fields are server-side/base-entity concerns and are never accepted from client payloads. Business versions use names such as `BaselineVersion`, `ManifestVersion`, or `VersionNumber`.

| Object | Principal fields | Required constraints / indexes |
|---|---|---|
| CollectionDefinition | CanonicalId, ParentCanonicalId, Name, PurposeScope, RequiredByScope, AllowsManualChildren, TemplatesAllowed, AllowedDocClass, DefaultClassificationLevel, DefaultRetentionHint, IsMandatory, IsAutoProvisioned, IsProtected, PathSegment, DisplayOrder, Status, VersionToken | Tenant + CanonicalId unique; acyclic parent tree; sibling PathSegment unique case-insensitively; no hard delete |
| BaselineRelease | BaselineReleaseId, BaselineVersion, EffectiveDate, Status, ChangeSummary, SnapshotHash, ManifestId, DeprecationNoticeWindowDays | Tenant + BaselineReleaseId unique; only PUBLISHED is instantiable |
| BaselineSnapshotManifest | ManifestId, BaselineReleaseId, ManifestVersion, DefinitionIds, DefinitionHashes, StructuralControlsHash, TemplateBindingsHash, SnapshotHash | Immutable after publication; deterministic hash |
| CorporateDocumentationRoot | CorporateRootId, CollectionScopeType, ScopeSourceModule, Status, ActiveBaselineReleaseId, InitializedAt/By, LockedAt/By, VersionToken | Exactly one active root per tenant; CollectionScopeType always CORPORATE |
| CollectionInstance | InstanceId, CompanyId, CanonicalId, BaselineReleaseId, CollectionScopeType, InstanceStatus, ScopeBindings, FullPath, LastChangeAt, VersionToken | Tenant + CompanyId + CanonicalId unique; InstanceId `{company_id}|{canonical_id}` |
| ScopeBinding | OrgBindingScopeType, OrgBindingScopeId, ScopeSourceModule, OwnerDept, BindingStatus, EffectiveFrom/To, LastValidatedAt | COMPANY binding uses MOD-0220 LegalEntity GUID |
| CollectionBinding | BindingId, CompanyId, OrgBindingScopeType/Id, ScopeSourceModule, BaselineReleaseId, BindingStatus, LastValidatedAt, LastValidationResult, VersionToken | Tenant/company scoped; upstream eligibility validated synchronously |
| LocalCollectionNode | LocalNodeId, CompanyId, ParentInstanceId, Name, PathSegment, FullPath, ClassificationOverride, RetentionOverride, Status, VersionToken | Parent must allow manual children; sibling segment unique; no move/reparent in MVP |
| TemplateMaster | TemplateMasterId, Name, LinkedCanonicalId, Status, CurrentVersionNumber, LastPublishedAt, VersionToken | Tenant scoped; published versions immutable |
| TemplateMasterVersion | MasterVersionId, TemplateMasterId, VersionNumber, PublishedAt/By, ContentRef, Checksum | Checksum required when content pointer exists |
| TemplateVariant | TemplateVariantId, CompanyId, TemplateMasterId, DerivedFromVersion, LastRebasedVersion, DriftStatus, Jurisdiction, ExceptionId, ContentRef, VersionToken | Tenant/company scoped; approved exception required for EXCEPTION_GRANTED |
| Exception | ExceptionId, CompanyId, ObjectRef, Category, CurrentValue, ProposedValue, Rationale, RiskRating, ExpiryDate, Status, ApproverRole, DecisionNotes, closure/decision audit fields, VersionToken | Future expiry on submit; typed values by category; SoD for sensitive decisions |
| ContentRef | RepositoryProvider, RepositoryObjectId, VersionRef, Checksum, MimeType, DisplayName, AccessPolicyRef | Approved provider; no token/secret; pointer permission validation |
| ProvisioningJob | JobId, JobType, CompanyId, BaselineReleaseId, RequestedBy, Status, Progress, FailureItems, CorrelationId, StartedAt, CompletedAt | Tenant/job unique; retry-safe; controlled failure details only |

## 5. Repo Scope

### Current draft/readiness work

- `execution/domains/platform-shared-services/module-packs/MOD-0028-document-management.md`

### Future implementation scope, only after approval and Wave 1 gate

- `services/Diten.Platform/**` for MOD-0028 API, Application, Domain, Persistence, Infrastructure, and tests.
- `frontend/Diten.Web/**` only for MOD-0028 tenant-facing surfaces rendered with TenantShell.
- `gateway/Diten.ApiGateway/**` only when routing is required and only through an `integration-agent` task.
- Wave 1 may inspect relevant files repo-wide but must not change runtime files.

`services/Diten.MdmService/**` is not changed by this pack. MOD-0028 consumes the MOD-0220 LegalEntity lookup/validation contract only.

## 6. Protected Paths

- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- Other domain services, including `services/Diten.AuthService/**`, `services/Diten.DevEnablementService/**`, and `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**` unless a separate approved MOD-0220 pack explicitly authorizes work
- `gateway/Diten.ApiGateway/**/ocelot.json` unless an explicit integration-agent task is approved
- Any MOD-0029, MOD-0030, or MOD-0031 implementation files
- Audit store/query, workflow engine, retention engine, evidence export, and binary repository internals

## 7. Dependencies

| Dependency | Contract |
|---|---|
| MOD-0018 | Server-side authorization, effective permission evaluation, obligations, and tenant actor context |
| MOD-0021 | Append-only AUD-01 ingest; MOD-0028 emits, MOD-0021 stores and queries |
| MOD-0023 | Optional workflow routing behind feature flag; direct role-gated decisions remain the minimum |
| MOD-0030 | Future retention/legal-hold authority; MOD-0028 stores hints/read-only seams only |
| MOD-0031 | Evidence linking/export consumer; MOD-0028 does not assemble evidence packs |
| MOD-0048 | Classification, retention class, risk rating, and other governed reference values |
| MOD-0220 | Synchronous LegalEntity lookup/eligibility validation; authoritative CompanyId GUID |
| External repository | ContentRef validation and access checks; binary content remains external |

Lookup decision: enterprise reference values are consumed from MOD-0048 or the approved compatibility mirror. No MOD-0028 hardcoded fallback list is allowed. MOD-0220 company values are MDM-owned and must not be added to PSS lookups.

## 8. Runtime Constraints

- Persistence: MongoDB, single database, tenant isolation on every persisted record.
- `entity_base: BaseEntity` is the provisional tenant-owned persistence choice; Wave 1 must verify the canonical base class currently used by tenant-scoped `Diten.Platform` entities.
- If the live service uses another canonical tenant base, update frontmatter and Entity Fields before any implementation wave begins.
- `TenantId` is server-resolved and never accepted in request DTOs/forms/query parameters.
- Soft-delete and applicable audit fields remain mandatory and are populated server-side or by the canonical base/repository pipeline.
- Cross-tenant detail access returns 404; restricted lists omit rows.
- Soft delete is mandatory; governed lineage has no hard-delete path in MVP.
- Collection runtime scopes: `CORPORATE` active, `COMPANY` active, `POSITION` disabled, `PERSON` disabled.
- Corporate scope means group/holding governance inside the tenant; it never means Diten/platform ownership.
- Company scope means a tenant-local MOD-0220 LegalEntity; `company_id` is its GUID.
- `collection_scope_type` and `org_binding_scope_type` are distinct vocabularies and cannot be collapsed.
- State-changing operations require `correlation_id`, optimistic concurrency, audit emission, and controlled errors with `reason_code`.
- No internal stack traces or restricted identifiers are returned/logged on denied paths.
- Provisioning/reconciliation is idempotent and non-destructive; it never deletes local content or silently weakens stricter local posture.

Feature flags:

| Flag | Default |
|---|---|
| `mod0028.corporate_root.enabled` | on |
| `mod0028.company_provisioning.enabled` | on |
| `mod0028.manual_local_nodes.enabled` | on |
| `mod0028.exceptions.enabled` | on only when live UI and API are ready |
| `mod0028.position_scope.enabled` | off |
| `mod0028.person_scope.enabled` | off |
| `mod0028.workflow_integration.enabled` | off unless MOD-0023 is integrated |

## 9. Layout & Shell Contract

- Primary shell: `shell: tenant`.
- Primary Razor layout: `Layout = "_LayoutTenantShell";` must be explicit in every MOD-0028 user-facing `.cshtml` page.
- Primary actor type: `tenant_user`.
- Primary roles: Tenant Corporate Governance Admin, Tenant Group Standards Owner, Tenant Group Approver, Tenant Company Documentation Admin, Tenant Company Local Editor, Tenant Documentation Auditor.
- Wave 1 must discover the real tenant module view/controller/route convention from the repository.
- A possible path such as `frontend/Diten.Web/Views/DocumentationManagement/{Surface}/` is illustrative only and is not binding.
- The implementation path is fixed only after inspection and must follow the existing TenantShell module convention.
- Main governance screens include baseline, tree, company adoption/binding, provisioning status, templates/variants, and exceptions.
- Platform Admin shell may expose only entitlement/enablement, bootstrap health, operations diagnostics, support, and read-only diagnostics.
- Platform admins cannot manage a tenant's live corporate baseline as a daily governance actor.
- No primary MOD-0028 governance page may use `_LayoutPlatformAdmin.cshtml` or the frozen `_Layout.cshtml`.

## 10. Backend File Convention

Where a MOD-0028 slice is implemented as CQRS, it follows the Compact Golden Reference action-based shape:

```text
Features/{Slice}/
|-- Commands/
|-- Queries/
|-- Handlers/
|   |-- CommandHandlers/
|   `-- QueryHandlers/
|-- Validators/
`-- {Slice}Models.cs
```

- Commands/queries are separate sealed request types.
- Handlers are sealed classes named `{Verb}{Slice}Handler`; no `CommandHandler`, `QueryHandler`, or `RequestHandler` suffix.
- Validators are named `{Verb}{Slice}Validator`; no `CommandValidator` suffix.
- Update/delete/patch commands return `Response<NoContent>`, not `Response<bool>`.
- Controllers inherit `CustomBaseController`, remain thin, and dispatch through MediatR.
- External MOD-0220/repository calls use Application interfaces with Infrastructure implementations; handlers do not use raw `HttpClient`.
- Complex operations such as provisioning are split into orchestration services/jobs rather than oversized CRUD handlers.

## 11. Frontend File Contract

`golden_reference: compact` is required because the principal governance forms exceed eight user-facing fields (`form_field_count: 19`) and the module uses multi-page flows.

GoldenReferenceCompact governs shared list/DataTable conventions where applicable:

- `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`
- Separate `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, and `_Form.cshtml` where a form-based surface applies
- `{Surface}Index.cs`, `index.js`, and `index.l10n.js`
- `data-dt-standard="v2"`, skeleton loader, Gateway/same-origin API usage, and localization contract
- Compact surfaces do not use `_CreateEditOffcanvas.cshtml` or `_DetailsQuickView.cshtml`

This module is not a simple CRUD DataTable module. Tree workspace, wizard, queue, provisioning status, detail drawer/dialog, and governance screens follow spec-specific UX, accessibility, and live backend contracts. GoldenReferenceCompact applies only to shared list/form conventions and does not replace the v2.3.0 UX contract.

## 12. Validation Rules

| Field / operation | Required | Rule | Pre-check / DB rule |
|---|---|---|---|
| CanonicalId | Yes | `^CAN-[A-Z0-9]{2,10}-[A-Z0-9]{2,16}-[0-9]{3,6}$`; immutable | Tenant-scoped unique |
| ParentCanonicalId | Conditional | Existing same-tenant definition; no cycles | Parent lookup + cycle check |
| Name | Yes | Trimmed, 3-120 chars | None |
| PurposeScope | Yes | 10-2000 chars | None |
| PathSegment | Yes | Max 100; forbidden path/control characters; trimmed | Case-insensitive sibling uniqueness |
| FullPath | Derived | Max 1024; tree depth max 15 | Server-derived only |
| RequiredByScope | Yes | COMPANY/BU/SITE/DEPT only | Reject POSITION/PERSON |
| CollectionScopeType | Yes | CORPORATE/COMPANY active; POSITION/PERSON contract-only | Feature flag guard |
| CompanyId | COMPANY only | Valid GUID and eligible tenant-local MOD-0220 LegalEntity | Synchronous upstream validation |
| BaselineRelease publish | Yes | Valid tree, unique IDs, deterministic manifest/hash | Status must be DRAFT; concurrency token |
| Local node create | Yes | Parent allows manual children; no root-level free-form node | Parent permission + sibling uniqueness |
| ContentRef | Conditional | Approved provider, valid opaque object ID, no secret/token | External permission/object validation |
| Checksum | Conditional | Required when published content pointer exists | Pointer validation |
| Exception rationale | Yes | Minimum 20 characters | None |
| Exception expiry | Yes | Future tenant-local date at submit | Tenant timezone policy |
| Exception decision | Yes | Valid transition; requestor/approver SoD for sensitive cases | Permission + expected status/version token |
| VersionToken | Mutation | Must match current mutable record | 409 on stale write |
| CorrelationId | All APIs/jobs | Non-empty and propagated | Gateway/service/audit/log consistency |

## 13. Failure Path to Verify

- **Canonical duplicate or sibling path conflict:** return 409 `CONFLICT`; create/update does not persist.
- **Missing/invalid required field:** return 400 `VALIDATION_FAILED` with field errors; UI preserves user context.
- **Unauthorized detail read:** return 404 `NOT_FOUND_NON_LEAKAGE`; response and logs omit restricted identifiers.
- **Unauthorized mutation:** return 403 `PERM_DENIED`; no state change or audit success event.
- **Unauthorized list/search:** restricted rows are omitted rather than masked.
- **Cross-tenant ID:** tenant B cannot read tenant A data and receives 404 non-leakage behavior.
- **Stale VersionToken:** return 409 `CONFLICT`; UI requires refresh/retry and never silently overwrites.
- **MOD-0220 unknown/ineligible company:** reject binding/provisioning with controlled dependency error; no orphaned writes.
- **MOD-0220 unavailable:** fail closed with `UPSTREAM_FAILURE`; no partial active lineage.
- **POSITION/PERSON request while disabled:** return 400 `FEATURE_DISABLED`; UI hides active flows; background jobs create nothing.
- **PlatformAdminShell main governance placement detected:** Wave 1 verdict is at least `PARTIAL/BLOCKED` and records a P0 shell-placement blocker.
- **Missing server-side NL-01, audit, correlation, or company binding seam:** Wave 1 records a P0 blocker; no placeholder UI or fake endpoint is proposed.

## 14. Authorization Convention

- Policy: `[Authorize]` for tenant-facing controllers, with server-side `[HasPermission]` enforcement per semantic action.
- Primary actor type: `tenant_user`.
- Canonical permission pattern mandated by v2.3.0: `MOD0028.<OBJECT>.<ACTION>`.
- Wave 1 must search the repository for existing MOD-0028 permission literals, aliases, backend policies, and frontend gate keys.
- If the live repository standard uses lowercase-dotted permissions, Wave 1 must populate the Effective Permission Mapping Table below rather than renaming literals speculatively.
- **🔴 TBD:** This uppercase MOD-specific pattern conflicts with the current global PKS-001 lowercase-dotted examples. MOD-0018/security owners must approve the effective mapping before implementation.

### Effective Permission Mapping Table

Wave 1 populates one row for every required permission. No implementation starts until backend and frontend resolve to the same effective permission.

Wave 1 population rule:

- Populate the table from actual repository evidence only; do not invent repository aliases.
- Do not rename permission literals during Wave 1.
- For each spec permission, search backend `[HasPermission]` attributes, policy registration/configuration files, permission seed files, RBAC role/claim definitions, frontend permission-gate helpers, navigation/menu visibility rules, and existing MOD-0028/document-management literals.
- If no existing key is found, keep status `missing`.
- If backend and frontend use different effective keys, set status `conflict`.
- If backend and frontend resolve to the same effective permission, set status `confirmed`.
- Implementation remains blocked unless all permissions required by the selected implementation wave are `confirmed` or covered by an approved alias mapping.

| Spec canonical key | Existing repo key / alias | Backend policy name | Frontend gate key | Status |
|---|---|---|---|---|
| `MOD0028.COLLECTION_DEFINITION.LIST` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.COLLECTION_DEFINITION.VIEW` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.COLLECTION_DEFINITION.CREATE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.COLLECTION_DEFINITION.EDIT` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.COLLECTION_DEFINITION.DEPRECATE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.COLLECTION_DEFINITION.RETIRE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.BASELINE_RELEASE.LIST` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.BASELINE_RELEASE.PUBLISH` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.BASELINE_RELEASE.DEPRECATE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.CORPORATE_ROOT.INITIALIZE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.CORPORATE_ROOT.LOCK` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.COLLECTION_INSTANCE.VIEW` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.COLLECTION_INSTANCE.INSTANTIATE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.COLLECTION_INSTANCE.BIND_SCOPE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.LOCAL_NODE.CREATE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.LOCAL_NODE.EDIT` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.TEMPLATE_MASTER.LIST` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.TEMPLATE_MASTER.PUBLISH` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.TEMPLATE_VARIANT.CREATE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.TEMPLATE_VARIANT.EDIT` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.TEMPLATE_VARIANT.REBASE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.EXCEPTION.SUBMIT` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.EXCEPTION.DECIDE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.EXCEPTION.CLOSE` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.EXCEPTION.LIST` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |
| `MOD0028.EXPORT.RUN` | Wave 1 inspection | Wave 1 inspection | Wave 1 inspection | missing |

Allowed status values: `confirmed`, `missing`, `conflict`.

Required permission set:

```text
MOD0028.COLLECTION_DEFINITION.LIST
MOD0028.COLLECTION_DEFINITION.VIEW
MOD0028.COLLECTION_DEFINITION.CREATE
MOD0028.COLLECTION_DEFINITION.EDIT
MOD0028.COLLECTION_DEFINITION.DEPRECATE
MOD0028.COLLECTION_DEFINITION.RETIRE
MOD0028.BASELINE_RELEASE.LIST
MOD0028.BASELINE_RELEASE.PUBLISH
MOD0028.BASELINE_RELEASE.DEPRECATE
MOD0028.CORPORATE_ROOT.INITIALIZE
MOD0028.CORPORATE_ROOT.LOCK
MOD0028.COLLECTION_INSTANCE.VIEW
MOD0028.COLLECTION_INSTANCE.INSTANTIATE
MOD0028.COLLECTION_INSTANCE.BIND_SCOPE
MOD0028.LOCAL_NODE.CREATE
MOD0028.LOCAL_NODE.EDIT
MOD0028.TEMPLATE_MASTER.LIST
MOD0028.TEMPLATE_MASTER.PUBLISH
MOD0028.TEMPLATE_VARIANT.CREATE
MOD0028.TEMPLATE_VARIANT.EDIT
MOD0028.TEMPLATE_VARIANT.REBASE
MOD0028.EXCEPTION.SUBMIT
MOD0028.EXCEPTION.DECIDE
MOD0028.EXCEPTION.CLOSE
MOD0028.EXCEPTION.LIST
MOD0028.EXPORT.RUN
```

Legacy permission aliases remain operational until a controlled mapping is approved; frontend and backend must resolve the same effective permission.

## 15. Gateway / API Routing Decision

- Semantic API family: `api/v1/document-management`.
- API responses use Platform `Response<T>` envelope and `CustomBaseController` behavior.
- Every response carries `correlation_id`; controlled failures carry `reason_code` and no stack trace.
- Frontend calls Gateway `5000` or a same-origin MVC proxy; it never calls Platform service port `5057` directly.
- Current gateway route coverage must be inspected in Wave 1.
- Gateway change is **undecided until inspection**. If required, it is a separate integration-agent task with explicit root and catch-all routes, `OPTIONS`, and all required HTTP methods.
- This pack never directly modifies `ocelot.json`.

## 16. Acceptance Criteria

- [x] Pack exists at the canonical PSS module-pack path and remains `status: draft`.
- [x] Domain is `platform-shared-services`, service is `Diten.Platform`, and shell is `tenant`.
- [x] Primary layout is explicitly `_LayoutTenantShell.cshtml` / `Layout = "_LayoutTenantShell";`.
- [x] Corporate scope is defined as tenant-internal group/holding governance, not Diten/platform ownership.
- [x] Company scope is bound to tenant-local MOD-0220 LegalEntity and uses its GUID as `company_id`.
- [x] Platform Admin usage is limited to entitlement, enablement, bootstrap health, operations/support, and read-only diagnostics.
- [x] Owned objects and consumed modules are separated explicitly.
- [x] MOD-0029, MOD-0030, MOD-0031, MOD-0023, and MOD-0021 ownership is not absorbed into MOD-0028.
- [x] `CORPORATE` and `COMPANY` are active; `POSITION` and `PERSON` are deferred and default-off.
- [x] NL-01 detail 404, mutation 403, filtered lists, tenant isolation, audit, correlation, and denied-path logging requirements are testable.
- [x] API family, envelope, `reason_code`, and correlation requirements are explicit.
- [x] GoldenReferenceCompact is limited to applicable list/form conventions; tree/wizard/queue UX remains spec-driven.
- [x] Wave 1 inspection-first gate is explicit.
- [x] Direct coding is prohibited until Wave 1 returns PASS or controlled CONDITIONAL PASS and pack blockers are resolved.
- [ ] Enterprise Architect resolves the canonical name gate by either preserving `Documentation Management` and marking `Documentation & Evidence Management` as a broad/legacy alias, or retaining the Blueprint name and updating frontmatter while recording the spec name as an alias. Until recorded, approval is blocked.
- [ ] Permission convention conflict is resolved or an approved alias mapping is recorded.

## 17. Test Expectations

### Draft/readiness validation

- Manual module-pack format and 20-section review.
- MOD-0028 v2.3.0 spec parity review.
- Module ID registry and Blueprint name comparison.
- Domain config alignment check.
- TenantShell placement and Platform Admin restriction review.
- Permission convention/alias review with MOD-0018.
- System-of-record and consumed-boundary review.
- Wave 1 read-only evidence map for backend, frontend, routes, entities, permissions, audit, tenant isolation, feature flags, and tests.
- Wave 1 verdict: `PASS`, `CONDITIONAL PASS`, `PARTIAL`, `BLOCKED`, or `FAIL`.

### Wave 1 Inspection Output Contract

- Final verdict: `PASS`, `CONDITIONAL PASS`, `PARTIAL`, `BLOCKED`, or `FAIL`.
- Evidence table containing actual files, routes, and tests inspected.
- Shell placement findings, including any PlatformAdminShell ownership leak.
- Route/controller mapping against `api/v1/document-management` semantics.
- Entity/base-class/persistence gaps and migration posture.
- RBAC findings and the completed effective permission mapping table.
- NL-01 and tenant-isolation findings.
- MOD-0220 LegalEntity binding/validation seam findings.
- Audit event and correlation-id propagation findings.
- UI surface status classified as live, guarded, missing, or broken.
- D+S closure status for the applicable DEL-281 through DEL-290 deliverables.
- Recommended first safe implementation wave with blockers and prerequisites.

### Later implementation waves, only after approval

```text
dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug
```

- Relevant Diten.Platform unit/integration/contract/security tests.
- Tenant isolation and cross-tenant 404 tests.
- NL-01 detail/mutation/list/search/export tests.
- MOD-0220 valid, unknown, ineligible, ambiguous, and unavailable contract tests.
- Provisioning/reconciliation idempotency, partial-failure, retry, and no-orphan tests.
- Deferred-scope feature flag tests proving no POSITION/PERSON records are created.
- Audit event and `correlation_id` propagation tests; denied paths must not log sensitive identifiers.
- RESX parity checker for tenant languages.
- DataTable verifier only for list surfaces that adopt the v2 contract.
- Browser smoke through frontend `5001` and Gateway `5000`, never a direct service port.

## 18. Ready-for-dev Checklist

- [x] AGENTS, domain config, master plan, registry, module-pack standard, Compact Golden Reference, live Compact code, and required architecture/security rules were reviewed.
- [x] Frontmatter contains service, shell, golden reference, entity base, dates, branch, and form field count.
- [x] Layout & Shell Contract explicitly names `_LayoutTenantShell`.
- [x] Backend file convention records Golden Reference folder/naming rules.
- [x] Frontend contract distinguishes shared Compact conventions from spec-specific governance UX.
- [x] Validation, failure, authorization, routing, acceptance, and test sections are present.
- [x] Owned objects and consumed boundaries match v2.3.0.
- [x] Wave 1 inspection prompt and verdict gate are identified.
- [ ] Enterprise Architect chooses and records the canonical-name reconciliation outcome; frontmatter/aliases are updated accordingly. **BLOCKER**
- [ ] Python-based `verify_module_id.py` preflight runs successfully in an environment with Python/openpyxl. **BLOCKER**
- [ ] Wave 1 Profile C inspection returns PASS or approved CONDITIONAL PASS. **BLOCKER**
- [ ] Wave 1 has populated the Effective Permission Mapping Table with actual repository evidence. **BLOCKER**
- [ ] Effective Permission Mapping Table is complete and backend/frontend resolve the same effective permissions. **BLOCKER**
- [ ] Permission naming/alias mapping is approved by MOD-0018/security ownership. **BLOCKER**
- [ ] MOD-0220 LegalEntity lookup/eligibility validation contract is confirmed available for the implementation wave. **BLOCKER**
- [ ] D+S gaps required by the selected implementation wave are closed. **BLOCKER**
- [ ] Recommended first safe implementation wave is explicitly selected after Wave 1. **BLOCKER**
- [ ] If only a subset wave is approved, pack scope is narrowed or a follow-up pack is created for that wave. **BLOCKER**
- [ ] User changes status to `approved` or `ready-for-dev`.

## 19. Implementation Notes

- The first golden flow is read-only: inspect v2.3.0 alignment, map current backend/frontend/routes/entities/permissions/audit/tests, detect shell placement and contract gaps, and produce an evidence-backed verdict without code changes.
- Wave 1 must not seed data, create placeholders, invent endpoints, activate deferred scopes, or mutate runtime files.
- If current implementation places main governance screens under PlatformAdminShell, activates Position/Person, lacks a MOD-0220 binding seam, applies NL-01 only in UI, or lacks audit/correlation propagation, classify the issue as P0.
- Corporate and company governance remain tenant-owned, so `BaseEntity` is used; `GlobalEntity` is not justified.
- `BaseEntity` remains provisional until Wave 1 confirms the live tenant-scoped `Diten.Platform` base-class convention; inspection may require a frontmatter correction but may not weaken tenant, soft-delete, or audit guarantees.
- View/controller/route paths remain intentionally unset until Wave 1 discovers the existing TenantShell convention. Only the shell/layout decision is fixed now.
- The domain config's historical description of MOD-0028 as document storage is superseded for this pack by v2.3.0's collection/template/exception governance boundary. Binary storage remains external.
- The master plan's `Document / Evidence Metadata` wording is also naming drift and must not expand ownership into MOD-0031 evidence export.
- This pack is not authority to implement all MOD-0028 scope in one change. Every implementation wave may require its own approved/ready-for-dev prompt or follow-up pack.

### Sequenced Implementation Plan

1. **Wave 1 - Read-only inspection:** v2.3.0 repository alignment report and evidence-backed verdict.
2. **Wave 2 - Backend contract hardening:** route family, response envelope, `reason_code`, `correlation_id`, idempotency, and optimistic concurrency.
3. **Wave 3 - Corporate governance core:** corporate root, collection definitions, baseline releases, and baseline snapshot manifest.
4. **Wave 4 - Company adoption:** MOD-0220 LegalEntity binding, company provisioning, reconciliation, and provisioning jobs.
5. **Wave 5 - TenantShell core UI:** baseline catalog, instantiation wizard, provisioning status, and company tree viewer.
6. **Wave 6 - Local governance:** local nodes, exception request/detail/queue, and expiry job.
7. **Wave 7 - Template governance:** template masters, template versions, variants, drift, and rebase.
8. **Wave 8 - Release inspection:** audit, security, accessibility, observability, and release-gate closure.

Each implementation wave may be executed only through a separately approved/ready-for-dev prompt or follow-up pack with its own D+S closure and acceptance evidence. This draft pack alone does not authorize coding the full module.

### Next Prompt / Handoff

- The next executable action is not coding.
- The next executable action is the read-only inspection prompt `MOD0028-P0-INSPECT-V230-ALIGNMENT`.
- The inspection must produce a final verdict, evidence table, shell-placement findings, route/controller mapping, entity/base-class findings, completed permission mapping table, MOD-0220 binding findings, audit/correlation findings, UI surfaces classified as guarded/live/missing, and the recommended first safe implementation wave.
- After Wave 1, this pack may remain `draft` with blockers, become approved for a specifically bounded Wave 2/3 implementation, or be split into follow-up packs by implementation wave.

## 20. Follow-up Items

- Enterprise Architect: choose the explicit canonical-name outcome in the Module Identity Gate and record the approved canonical name/alias.
- Tooling: run the fail-closed module ID preflight when Python/openpyxl is available.
- Wave 1: execute `MOD0028-P0-INSPECT-V230-ALIGNMENT` and attach the evidence report.
- Wave 1: populate every required row in the Effective Permission Mapping Table from actual repository evidence.
- Security/MOD-0018: approve canonical permission strings and effective backend/frontend alias mapping.
- MOD-0220 owner: publish/confirm LegalEntity validation and eligibility semantics.
- Close D+S gaps for DEL-283 through DEL-290 before their corresponding implementation waves.
- Keep Position/Person feature flags off until real upstream SoR contracts and separately approved packs exist.
