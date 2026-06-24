---
id: MOD-0029-FU01
name: Controlled Document Versioning & Template Sharing Foundation
parent: MOD-0029
previous: MOD-0028-FU05
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: TenantScopedEntity
status: approved
owner: platform-shared-services
branch: feature/pss/mod-0029-fu01-controlled-document-versioning-template-sharing-foundation
started: 2026-06-22
target: 2026-07-20
form_field_count: 10
---

# MOD-0029-FU01 - Controlled Document Versioning & Template Sharing Foundation

> Parent canonical module: **MOD-0029 — Controlled Documents (SOPs / Work Instructions)**.
> The parent module name is **never** renamed to "Document Lifecycle / Template Repository / Sharing"; those
> phrases are only capability/scope descriptions *inside* MOD-0029, not the canonical module name.

## 1. Module Summary

MOD-0029-FU01 is a **backend + TenantShell-frontend** foundation for `MOD-0029 Controlled Documents
(SOPs / Work Instructions)`. It is the first follow-up after the MOD-0028 documentation-structure family
(FU01 backend contract → FU02 baseline import → FU03 tenant baseline UI → FU04 manual structure builder →
FU05 company adoption / `CollectionInstance` provisioning).

MOD-0028-FU05 produces a company-scoped `CollectionInstance` folder tree. **FU01 of MOD-0029 attaches
controlled documents, SOPs, work instructions, and template files to those folder nodes**, makes them
**versionable** (immutable activated versions), and makes selected documents/templates and whole
folder/branches **shareable** to other companies/legal entities in the same tenant under a controlled
share policy (`REFERENCE` or `COPY_ON_ADOPT`).

| Concept | Meaning |
|---|---|
| `CollectionInstance` | Company-specific folder node from MOD-0028-FU05 (**consumed read-only**, never mutated) |
| `ControlledDocument` | Logical controlled document attached to a `CollectionInstance` (FU01 owned) |
| `ControlledDocumentVersion` | Immutable versioned file/content record of a controlled document (FU01 owned) |
| `TemplateDocument` | Reusable template attached to a folder/company structure (FU01 owned) |
| `TemplateVersion` | Versioned template file (FU01 owned) |
| `DocumentType` | SOP / Work Instruction / Policy / Form / Template / Other |
| `DocumentSharePolicy` | Who can access / use / copy a document or template |
| `FolderSharePackage` | A folder/branch + its associated templates shared to another company |
| `FileRef` / `ContentRef` | Pointer to the approved binary/content storage abstraction (no physical FS) |
| `VersionStatus` | DRAFT / ACTIVE / SUPERSEDED / ARCHIVED |

### Sharing model (first version)

| Mode | Behavior | First-version status |
|---|---|---|
| `REFERENCE` | Target company sees/uses the **same** active template version; source updates may be visible per policy | In scope (default for templates) |
| `COPY_ON_ADOPT` / `COPY_ON_SHARE` | Target company receives its **own copied** document/template; version lineage starts from the copied source; source and target diverge after copy | In scope (only when explicitly selected) |

Default recommendation: templates are shared by `REFERENCE`; `COPY_ON_ADOPT` is opt-in per share operation.

### Versioning model (first version)

- A `ControlledDocument` has many `ControlledDocumentVersion` records; a `TemplateDocument` has many
  `TemplateVersion` records.
- A **new upload creates a new version — never an overwrite**. Activated (`ACTIVE`) versions are immutable.
- The current `ACTIVE` version is always queryable; previous versions stay readable per permission.

### Approval Scope

- This pack is `status: approved` by explicit user approval on 2026-06-22.
- Approval is limited to the FU01 controlled-document / SOP / work-instruction / template versioning /
  sharing foundation scope only (see In scope §2 and the approved-scope list below).
- **Approved runtime work** is limited to: `ControlledDocument` creation; SOP / Work Instruction / Policy /
  Form / Template / Other document types; attaching controlled documents/templates to a `CollectionInstance`
  folder node; `ControlledDocumentVersion` creation; `TemplateDocument` creation; `TemplateVersion` creation;
  immutable activated versions; new-upload-creates-new-version (never overwrite); current-active-version query;
  previous-version read by permission; template flags (`reusable` / `shareable` / `copyableOnAdopt` /
  `referenceOnly`); individual document/template sharing; folder/branch share with associated templates;
  `REFERENCE` share mode; `COPY_ON_ADOPT` / `COPY_ON_SHARE` mode (feature-flag gated); folder-share dry-run;
  folder-share execute; per-item share outcomes; the TenantShell Controlled Documents UI (library, folder
  attachments, version history, upload new version, share document/template, folder-share wizard); controlled
  `reason_code`/`correlation_id` failures; MOD-0220 target-company fail-closed validation; and binary/content
  storage abstraction consumption only.
- **Not approved:** MOD-0028 folder-structure editing, `CollectionDefinition` editing, FU05 instantiation
  changes, direct filesystem storage, physical folder creation, implementing binary storage provider
  internals, OCR / content indexing, e-signature, approval workflow (unless the MOD-0029 parent explicitly
  approves it later), retention / legal hold, evidence export, public/anonymous sharing, external portal,
  email notification, browser-based document editing, and MOD-0030 / MOD-0031 implementation.
- No runtime code is written by this pack; it is a contract/specification only. Implementation begins only
  after the controlled gates in §18 are satisfied.

## 2. Ownership and Boundaries

### In scope

- **Attach controlled document / template to a folder**: select a `CollectionInstance` folder; upload or link
  a controlled document / SOP / work instruction / template file with metadata; store via the approved
  binary/content storage abstraction (`FileRef`/`ContentRef`). No physical filesystem folder creation.
- **Versioning**: immutable `ControlledDocumentVersion` / `TemplateVersion` records; new upload = new version;
  current active version queryable; previous versions readable per permission.
- **Template support**: folder nodes can have associated template files marked `reusable` / `shareable` /
  `copyableOnAdopt` / `referenceOnly`. First version attaches templates to a `CollectionInstance`.
- **Sharing** of an individual controlled document, a template document, or a folder/branch with its
  associated templates — to users/roles in the same company, or another company/legal entity in the same
  tenant (optional plant/business-unit scope) — in `REFERENCE` or `COPY_ON_ADOPT` mode.
- **Folder/branch share with templates**: identify the selected `CollectionInstance` branch, discover
  associated templates under included nodes, choose include-templates yes/no and share mode, **dry-run**
  preview (folders included, templates included/skipped, conflicts, permission issues), then **execute**
  (share records or target copies per mode). No unselected branch is shared; no unrelated document/template
  is exposed.
- Document-control metadata; flow-level `correlation_id` across dry-run/execute; MOD-0021 audit seams.
- A TenantShell **Controlled Documents** surface (library + folder-detail attachments + version history +
  upload-version + share controls + folder-share wizard).
- Focused backend + frontend tests.

### Consumed, not owned

- MOD-0028-FU05 `CollectionInstance` (read-only): `CollectionInstance` id, tree path, company/legal-entity
  binding, folder/branch scope.
- MOD-0028 FU01 `Response<T>` (`reason_code`/`correlation_id`), the `api/v1/document-management` route family,
  `[HasPermission]`, the directional `PermissionAliasMap` convention, and the FU01/FU02 typed-options pattern.
- MOD-0220 LegalEntity lookup/eligibility for share-target company validation (the FU05-confirmed
  `ILegalEntityReferenceValidator` seam).
- MOD-0018 permission ownership for new keys; MOD-0021 audit store.
- Platform Common `TenantScopedEntity`, tenant context, tenant repository filtering, correlation middleware.
- The **approved binary/content storage abstraction** (an interface/seam; the concrete provider is consumed,
  never re-implemented as direct filesystem access in FU01).

### Explicitly out of scope

- Editing MOD-0028 folder structure; `CollectionDefinition` editing; FU04 manual-builder changes;
  FU05 instantiation logic changes (read-only consumption only).
- OCR / content indexing; e-signature; approval workflow (unless already approved by the MOD-0029 parent);
  retention / legal hold (MOD-0030); evidence export (MOD-0031); external customer portal; public/anonymous
  sharing; email notification.
- Physical folder creation; direct filesystem storage; browser-based document editing.

## 3. Owned Objects

FU01 owns the controlled-document/template aggregates plus the sharing lineage and contracts:

- `ControlledDocument` — logical controlled document attached to a `CollectionInstance` (primary aggregate).
- `ControlledDocumentVersion` — immutable versioned file/content record.
- `TemplateDocument` — reusable template attached to a folder/company structure.
- `TemplateVersion` — versioned template file.
- `DocumentSharePolicy` — embedded value object describing access/use/copy rules.
- `FolderShareOperation` — a folder/branch dry-run/execute share operation record (status, counts,
  correlation id, lineage).
- `FolderShareOutcome` — per-item share outcome (folder/template, status, reason_code, retryable).
- `FileRef`/`ContentRef` — embedded pointer value object to the approved binary/content storage abstraction.
- Controlled-document/template/share request/result contracts and the minimum FU01 permission mapping (§14).

FU01 must **not** create or mutate `CollectionInstance`, `CollectionDefinition`, `BaselineRelease`, or any
MOD-0028 structure object; it must not implement a binary storage provider, physical folders, retention,
or evidence export.

## 4. Entity Fields

FU01 persists tenant-owned, company-scoped aggregates using
`Diten.Platform.Common.Persistence.TenantScopedEntity` (confirmed by FU01/FU02/FU05). `TenantId`,
`IsDeleted`, and technical `Version` are inherited / server-resolved and never accepted from client payloads.
Business versions use semantic names (`VersionNumber`), never the technical `Version`.

| Object | Principal fields | Required constraints / indexes |
|---|---|---|
| ControlledDocument | DocumentKey, CompanyId, CollectionInstanceId, CollectionPath, Title, DocumentType, Description, Tags[], Controlled (bool), EffectiveDate?, ReviewDate?, ExpiryDate?, CurrentVersionId?, Status, OwnerCompanyId | Tenant + DocumentKey unique (non-deleted); tenant-first index; CollectionInstanceId indexed; no hard delete |
| ControlledDocumentVersion | DocumentId, VersionNumber, FileRef (ContentRef), Checksum, UploadedBy, UploadedAt, ChangeSummary, VersionStatus | Tenant + DocumentId + VersionNumber unique; activated version immutable |
| TemplateDocument | TemplateKey, CompanyId, CollectionInstanceId?, CollectionPath?, Title, Description, Tags[], TemplateFlags (reusable/shareable/copyableOnAdopt/referenceOnly), CurrentVersionId?, Status, OwnerCompanyId | Tenant + TemplateKey unique (non-deleted); tenant-first index; no hard delete |
| TemplateVersion | TemplateId, VersionNumber, FileRef (ContentRef), Checksum, UploadedBy, UploadedAt, ChangeSummary, VersionStatus | Tenant + TemplateId + VersionNumber unique; activated version immutable |
| DocumentSharePolicy (embedded) | ShareMode (REFERENCE/COPY_ON_ADOPT), CanUse, CanCopy, VisibilityScope (COMPANY/PLANT/BU), SourceVisibleOnUpdate | COMPANY target uses MOD-0220 LegalEntity GUID |
| FolderShareOperation | OperationId, SourceCompanyId, TargetCompanyId, SourceBranchCollectionInstanceId, IncludeTemplates, ShareMode, OperationType (DRY_RUN/EXECUTE), Status, FoldersIncluded/TemplatesIncluded/TemplatesSkipped/Failed/Total, CorrelationId, RequestedBy, StartedAt, CompletedAt | Tenant + OperationId unique; CorrelationId indexed |
| FolderShareOutcome | OperationId, ItemType (FOLDER/TEMPLATE), ItemKey, Status (SHARED/COPIED/SKIPPED/FAILED), ReasonCode, Message, Retryable | Tenant + OperationId + ItemKey unique |
| FileRef / ContentRef (embedded) | ContentId, StorageProvider, MediaType, ByteSize, Checksum | Pointer only; FU01 never stores raw bytes in Mongo |

**Deterministic keys (closed at implementation):** `DocumentKey = {tenantId}|{companyId}|{collectionInstanceId}|{slug(title)}`
and `TemplateKey = {tenantId}|{companyId}|{collectionInstanceId?}|{slug(title)}` (or a repo-approved
equivalent). `CollectionPath` is copied read-only from the consumed `CollectionInstance`; FU01 never derives
or edits folder hierarchy.

## 5. Repo Scope

### Authorized FU01 implementation scope (after approval)

- `services/Diten.Platform/src/Diten.Platform.API/**` — thin controlled-document/template/folder-share
  controller actions + controlled response wiring under the existing route family.
- `services/Diten.Platform/src/Diten.Platform.Application/**` — CQRS commands/queries/handlers/validators,
  document/template/versioning/sharing services, MOD-0220 + binary-storage seam consumption, permission
  constants, reason codes.
- `services/Diten.Platform/src/Diten.Platform.Domain/**` — document/template/version/share aggregates +
  repository interfaces (only if the live convention places entities here).
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` — repository implementations, Mongo indexes,
  DI, approved alias registration, binary-storage seam adapter wiring (no provider re-implementation).
- `frontend/Diten.Web/**` — TenantShell Controlled Documents library + folder attachments + version history +
  upload + share controls + folder-share wizard (controller proxy, views, JS, RESX) per FU03/FU04/FU05
  conventions.
- `services/Diten.Platform/tests/**` and frontend tests/smoke.

### Separately governed scope

- `gateway/Diten.ApiGateway/**/ocelot.json` — only if a new route is needed beyond the existing widened
  catch-all (see §15); FU01 expects GET/POST under the existing catch-all, so **no gateway change is
  anticipated**.
- Permission seed/alias ownership for new keys through the MOD-0018/security-owned location when outside
  FU01 scope (see §14).
- The concrete **binary/content storage provider** internals (consumed via its abstraction only).

## 6. Protected Paths

- `.antigravity/**`
- `gateway/**` except a separately approved integration-agent task if a new route is unexpectedly required
- `services/Diten.Platform.Common/**`
- `services/Diten.AuthService/**` (permission seed/alias) except a separately approved MOD-0018/security task
- `services/Diten.MdmService/**` (MOD-0220 is consumed via its contract, never modified)
- `services/Diten.DevEnablementService/**`, `services/Diten.EnterpriseStrategyService/**`
- MOD-0028 FU01–FU05 owned files except read-only **consumption** of their public contracts
  (`CollectionInstance` id / path / company binding / folder scope)
- MOD-0030 (retention) and MOD-0031 (evidence) implementation files
- Binary repository / content storage provider internals; physical folder creation
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` and any frozen/legacy layout
- Parent pack and sibling packs unless a separate governance reconciliation authorizes an update

## 7. Dependencies

| Dependency | FU01 usage |
|---|---|
| MOD-0029 parent | Supplies controlled-document / SOP / template ownership + Wave boundary |
| MOD-0028-FU05 | Supplies `CollectionInstance` folder tree (id / path / company binding / scope) — consumed, not modified |
| MOD-0028-FU01/FU04 | Supply `Response<T>`, route family, `[HasPermission]`, manual-builder navigation handoff (read-only) |
| MOD-0220 | LegalEntity lookup/eligibility for share-target company validation (fail-closed seam) |
| MOD-0018 | Approves new lowercase permission keys, any uppercase aliases, seed ownership |
| MOD-0021 | Audit/correlation seams across upload/version/share |
| Binary/content storage abstraction | Approved seam for `FileRef`/`ContentRef`; provider consumed, never re-implemented |
| Platform Common | `TenantScopedEntity`, tenant context, repository filtering, correlation middleware |

## 8. Runtime Constraints

- Persistence: MongoDB, tenant isolation on every owned aggregate.
- `TenantScopedEntity` base; no client-controlled `TenantId`, technical `Version`, audit actor, or correlation
  identity.
- **Files are stored only through the approved binary/content storage abstraction** (`FileRef`/`ContentRef`);
  **no direct filesystem access and no physical folder creation**. Raw bytes are never persisted in Mongo.
- **Versions are immutable once `ACTIVE`**; a new upload creates a new version, never an overwrite. The current
  `ACTIVE` version is queryable; previous versions are readable per permission.
- **Sharing is controlled**: `REFERENCE` (target uses the same active version; source updates visible per
  policy) or `COPY_ON_ADOPT` (target receives an independent copy; lineage forks). A non-shareable source
  template cannot be shared (controlled `VALIDATION_FAILED`).
- **Folder/branch share**: dry-run mutates nothing; execute creates share records / target copies only for the
  selected branch and its included templates; no unselected branch is shared and no unrelated item is exposed.
- **MOD-0220 fail-closed** for share-target company: a missing/inactive/non-referenceable LegalEntity rejects
  the share without orphaned writes; caller cancellation is preserved.
- **No metadata orphan on storage failure**: if the binary/content store is unavailable, the operation fails
  in a controlled way and no document/version metadata row is left dangling.
- Every FU01 API envelope includes body-level `correlation_id`; the **same flow correlation id** flows through
  folder-share dry-run → execute; controlled failures include a stable `reason_code`; no stack traces.
- Cross-tenant / cross-company detail behavior is 404 non-leakage; unauthorized actions are 403.

Feature flags (reused FU01/FU02 typed-options pattern; FU01 adds only what it needs):

| Key | Default | FU01 relevance |
|---|---|---|
| `mod0029.controlled_documents.enabled` | on | The FU01 surface |
| `mod0029.template_sharing.enabled` | on | Document/template share actions |
| `mod0029.folder_share_copy_on_adopt.enabled` | off until copy lineage is verified | Gates `COPY_ON_ADOPT` execution |

## 9. Layout & Shell Contract

- Primary shell: `shell: tenant`; every FU01 page declares `Layout = "_LayoutTenantShell";` explicitly.
- Primary actor type: `tenant_user`.
- No `_LayoutPlatformAdmin.cshtml`, frozen `_Layout.cshtml`, or legacy layout.
- Nav: a TenantShell **Controlled Documents** entry (or a sub-action under the existing documentation nav),
  permission-gated on `…controlled-documents.view`; the folder detail/attachments surface is reached from the
  MOD-0028 Documentation Structures `CollectionInstance` detail.
- Exact view/controller/route folder confirmed against the live TenantShell convention (FU03/FU04/FU05 reuse).

## 10. Backend File Convention

FU01 follows the live Diten.Platform CQRS action-based shape (Golden Reference Compact mirrored), split into
focused feature folders:

```text
Features/DocumentManagementControlledDocuments/
|-- Commands/
|   |-- CreateControlledDocumentCommand.cs      (sealed record)
|   |-- CreateControlledDocumentVersionCommand.cs
|   |-- ShareControlledDocumentCommand.cs
|   |-- CreateTemplateDocumentCommand.cs
|   |-- CreateTemplateVersionCommand.cs
|   |-- ShareTemplateCommand.cs
|   |-- DryRunFolderShareCommand.cs
|   `-- ExecuteFolderShareCommand.cs
|-- Queries/
|   |-- GetControlledDocumentListQuery.cs
|   |-- GetControlledDocumentByIdQuery.cs
|   |-- GetControlledDocumentVersionsQuery.cs
|   |-- GetControlledDocumentVersionByIdQuery.cs
|   |-- GetTemplateListQuery.cs
|   |-- GetTemplateByIdQuery.cs
|   |-- GetTemplateVersionsQuery.cs
|   `-- GetFolderShareOperationQuery.cs
|-- Handlers/{CommandHandlers,QueryHandlers}/    (sealed, no Command/Query suffix)
|-- Validators/                                  (no Command suffix)
|-- Services/
|   |-- IContentStorageGateway.cs                (binary/content storage abstraction seam)
|   |-- DocumentVersioningService.cs             (immutable version creation + active-version resolution)
|   |-- TemplateSharingService.cs                (REFERENCE vs COPY_ON_ADOPT semantics)
|   |-- IFolderSharePlanner.cs                   (builds folder-share dry-run plans from a CollectionInstance branch)
|   `-- DocumentKeyFactory.cs                    (deterministic DocumentKey/TemplateKey)
`-- DocumentManagementControlledDocumentsModels.cs (DTOs/result models in one file)
```

- Commands/queries are sealed records; handlers `{Verb}{Slice}Handler` (no `CommandHandler`/`QueryHandler`
  suffix); validators `{Verb}{Slice}Validator` (no `CommandValidator` suffix).
- Controllers inherit `CustomBaseController`, remain thin, dispatch via MediatR; MOD-0220 and the content
  storage abstraction are accessed via Application interfaces + Infrastructure implementations (no raw
  `HttpClient` / no direct filesystem in handlers).
- Repository access uses the live tenant repository convention; tenant-first indexes mandatory.
- Versioning, sharing, and key generation are split into focused services, not oversized handlers.

## 11. Frontend File Contract

`golden_reference: compact`; the add-document/template surface is a multi-field route/page-based form
(`form_field_count: 10`), and the folder-share surface is a multi-step route-based wizard. Reuse the
FU03/FU04/FU05 TenantShell building blocks: same-origin MVC proxy → Gateway `5000`, shared toast/confirm,
the L10n bridge (`_IndexL10n` JSON → `index.l10n.js` → `window.L10n`), DataTable v2 for library lists, the
jsTree folder/branch renderer for folder selection, and `backbone-custom.css` for shared styles (no
page-embedded styles).

Compact view set (final names confirmed against the live convention):

```text
Views/DocumentManagement/ControlledDocuments/
|-- Index.cshtml                 (Layout = "_LayoutTenantShell"; library list)
|-- Create.cshtml                (add controlled document / template — Compact)
|-- Edit.cshtml                  (edit document metadata — Compact)
|-- Details.cshtml               (document detail + version history panel — Compact)
|-- _Form.cshtml                 (Compact shared form partial)
|-- _Filter.cshtml
|-- _DataTable.cshtml            (data-dt-standard="v2" + skeleton)
|-- _IndexL10n.cshtml
`-- ControlledDocumentsIndex.cs  (marker class)

wwwroot/assets/js/DocumentManagement/ControlledDocuments/
|-- index.js
`-- index.l10n.js
```

Compact rule: `_CreateEditOffcanvas.cshtml` and `_DetailsQuickView.cshtml` are **forbidden** for this pack.

Surfaces:

- **Controlled Documents / SOPs / Work Instructions library** (DataTable v2): title, type, company, folder
  path, current version, status, actions.
- **CollectionInstance folder detail attachments**: documents/templates attached to the selected folder node
  (reached from the MOD-0028 Documentation Structures detail).
- **Add controlled document/template** (Compact form): the §12 metadata + file upload/link through the proxy.
- **Version history panel** + **upload new version** (new version, never overwrite).
- **Share document/template** controls (target + mode).
- **Folder/branch share wizard** (route-based): Select source company/branch → Select target
  company/legal entity → Include templates yes/no → Share mode (`REFERENCE` / `COPY_ON_ADOPT`) → Dry-run
  preview → Execute → Results, all permission-gated.

## 12. Validation Rules

| Input / operation | Required | Rule | Failure |
|---|---|---|---|
| CollectionInstance id | Yes | Must resolve to an existing tenant `CollectionInstance` | 404 `NOT_FOUND_NON_LEAKAGE` |
| Title | Yes | Non-empty, trimmed, length-bounded | 400 `VALIDATION_FAILED` |
| Document type | Yes | One of SOP / Work Instruction / Policy / Form / Template / Other | 400 `VALIDATION_FAILED` |
| File (upload/link) | Yes | Supported media type; stored via content abstraction | 400 `VALIDATION_FAILED` (unsupported type) |
| Effective/Review/Expiry dates | No | Valid dates; expiry ≥ effective if both present | 400 `VALIDATION_FAILED` |
| Tags | No | Trimmed, de-duplicated | normalized |
| Version number | Yes (server-assigned) | Monotonic per document/template; activated version immutable | 409 `CONFLICT` on duplicate version number |
| New version upload | Yes | Creates a new version; never overwrites an existing one | 409 `CONFLICT` |
| Template shareability | Conditional | A `referenceOnly`/non-shareable template cannot be `COPY_ON_ADOPT`/shared | 400 `VALIDATION_FAILED` |
| Share mode | Yes | `REFERENCE` or `COPY_ON_ADOPT`; copy gated by feature flag | 400 `VALIDATION_FAILED` |
| Share target company | Yes | MOD-0220 ACTIVE + referenceable LegalEntity in the same tenant | 404 `NOT_FOUND_NON_LEAKAGE` / fail-closed |
| Folder-share branch | Yes | Selected `CollectionInstance` branch resolves; only included nodes are shared | 400 `VALIDATION_FAILED`; no unselected exposure |
| Dry-run gate | Yes | Execute disabled until a non-blocked folder-share dry-run for the current selection | Execute stays disabled |
| Binary storage availability | Yes | Content abstraction must accept the file before metadata commit | controlled failure, no metadata orphan |
| TenantId | Never client input | Resolved from tenant context | request contract rejected / test fails |
| Correlation id | All APIs | Non-empty; shared across the flow; body/header identical | generated server-side if absent |

## 13. Failure Path to Verify

- **Missing `CollectionInstance`** (attach target): 404 `NOT_FOUND_NON_LEAKAGE`; no document created.
- **Cross-tenant folder/document/template** access: 404 `NOT_FOUND_NON_LEAKAGE`; no leaked id.
- **Missing permission**: 403 `PERM_DENIED`; no side effect, no success audit.
- **Unsupported file type**: 400 `VALIDATION_FAILED`; no version stored.
- **Duplicate version number**: 409 `CONFLICT`; no second row.
- **Overwrite attempt** of an `ACTIVE` version: rejected; a new version is required.
- **Source template not shareable**: 400 `VALIDATION_FAILED`; no share record/copy.
- **Share target company not found / inactive**: 404 `NOT_FOUND_NON_LEAKAGE` / fail-closed; cancellation
  preserved.
- **Binary storage unavailable**: controlled failure with reason code; **no metadata orphan** (no dangling
  document/version row).
- **Folder-share copy/share partial failure**: per-item `FolderShareOutcome` (`FAILED` + reason_code +
  retryable); honest counts; no fabricated success.
- **Unselected branch / unrelated template**: never shared; dry-run reports skipped/excluded; execute does not
  expose it.
- All controlled failures carry `reason_code` + body/header `correlation_id`; no stack traces / exception text.

## 14. Authorization Convention

- Policy: `[Authorize]` on the tenant-facing controller; `[HasPermission]` per semantic action.
- Actor type: `tenant_user`; runtime canonical format PKS-001 lowercase dotted keys under
  `platform.document-management.{resource}.{action}`.
- Spec keys remain traceable directional aliases only if MOD-0018/security approves; reverse/dynamic aliases
  prohibited.

Proposed FU01 permission keys (minimal MOD-0029 controlled-document set):

| Key | Endpoint(s) |
|---|---|
| `platform.document-management.controlled-documents.view` | document list/detail/version view |
| `platform.document-management.controlled-documents.create` | create controlled document |
| `platform.document-management.controlled-documents.version.create` | upload new document version |
| `platform.document-management.controlled-documents.version.view` | view document versions |
| `platform.document-management.controlled-documents.share` | share a controlled document |
| `platform.document-management.templates.view` | template list/detail |
| `platform.document-management.templates.create` | create template |
| `platform.document-management.templates.version.create` | upload new template version |
| `platform.document-management.templates.share` | share a template |
| `platform.document-management.folder-shares.create` | folder/branch share dry-run + execute |
| `platform.document-management.folder-shares.view` | folder-share operation status/outcomes |

Permission strategy (controlled gate):

- FU01 may add local Platform `[HasPermission]` constants/attributes. If seed/alias ownership requires a
  protected security path (`services/Diten.AuthService/**`), implementation **stops and reports a separate
  MOD-0018/security task**. A missing seed may leave validation `PARTIAL`, but the release gate does not close
  until the keys are `confirmed`.
- Backend and frontend resolve the **same** effective lowercase key; hidden/disabled controls are the UI
  expression of the backend's 403.

## 15. Gateway / API Routing Decision

- The existing `/api/v1/document-management/{everything}` catch-all already supports `GET, POST, PUT, PATCH,
  DELETE, OPTIONS` (after the FU04 widening). **FU01 uses GET/POST only**, so **no gateway change is
  anticipated** — verify and confirm; if a new explicit route is somehow required, it is a separate
  integration-agent task.
- All FU01 routes stay version-explicit under `api/v1/document-management`; no `v2`, no unversioned route.
- Frontend uses Gateway `5000` or a same-origin proxy; never the Platform API service port directly.

Candidate endpoints (names may adjust to repo convention):

| Method | Path | Used by |
|---|---|---|
| POST | `api/v1/document-management/controlled-documents` | create controlled document |
| GET | `api/v1/document-management/controlled-documents` | document library list |
| GET | `api/v1/document-management/controlled-documents/{documentId}` | document detail |
| POST | `api/v1/document-management/controlled-documents/{documentId}/versions` | upload new version |
| GET | `api/v1/document-management/controlled-documents/{documentId}/versions` | version list |
| GET | `api/v1/document-management/controlled-documents/{documentId}/versions/{versionId}` | version detail |
| POST | `api/v1/document-management/controlled-documents/{documentId}/share` | share document |
| POST | `api/v1/document-management/templates` | create template |
| GET | `api/v1/document-management/templates` | template list |
| GET | `api/v1/document-management/templates/{templateId}` | template detail |
| POST | `api/v1/document-management/templates/{templateId}/versions` | upload template version |
| GET | `api/v1/document-management/templates/{templateId}/versions` | template version list |
| POST | `api/v1/document-management/templates/{templateId}/share` | share template |
| POST | `api/v1/document-management/folder-shares/dry-run` | folder/branch share dry-run |
| POST | `api/v1/document-management/folder-shares/execute` | folder/branch share execute |
| GET | `api/v1/document-management/folder-shares/{operationId}` | folder-share operation status + outcomes |

## 16. Acceptance Criteria

- [x] FU01 pack promoted to `status: approved` after explicit user approval for this FU01 scope only
  (governance item; the implementation criteria below remain open until built and verified).
- [ ] Controlled documents/templates can be attached to a `CollectionInstance` folder (FU05 node consumed
  read-only: id, path, company binding, folder scope).
- [ ] SOP / Work Instruction document types are supported (alongside Policy / Form / Template / Other).
- [ ] Documents/templates have **immutable** version records; a new upload creates a new version, never an
  overwrite.
- [ ] The current/active version can be retrieved; previous versions remain accessible per permission.
- [ ] Templates can be marked `reusable` / `shareable` / `copyableOnAdopt` / `referenceOnly`.
- [ ] `REFERENCE` and `COPY_ON_ADOPT` semantics are documented and enforced (copy forks lineage; reference
  shares the active version).
- [ ] Folder/branch share dry-run shows folders included, templates included, templates skipped, conflicts,
  and permission issues.
- [ ] Execute shares/copies **only** the selected branch and its included templates; no unselected branch or
  unrelated item is exposed.
- [ ] Files are stored only via the approved binary/content storage abstraction; **no physical folder
  creation**, no direct filesystem storage, no raw bytes in Mongo.
- [ ] **No metadata orphan** if binary storage fails (controlled failure, no dangling row).
- [ ] Tenant/company isolation enforced on every owned aggregate; `TenantId` never client-controlled;
  tenant-first indexes; no hard delete.
- [ ] One flow `correlation_id` shared across folder-share dry-run/execute; body/header parity.
- [ ] Permissions are the minimal FU01 subset; backend and frontend resolve the same effective lowercase key;
  missing permission → 403 `PERM_DENIED`.
- [ ] Controlled failures (400/403/404/409) return `reason_code` + `correlation_id`, no stack traces.
- [ ] TenantShell Controlled Documents surface (library + folder attachments + version history + upload +
  share + folder-share wizard) delivered with `Layout = "_LayoutTenantShell"`.
- [ ] No MOD-0028 structure edit / `CollectionDefinition` edit / FU05 instantiation change; no MOD-0030 /
  MOD-0031 side effect.

## 17. Test Expectations

Backend tests:
- attach controlled document/template to a valid `CollectionInstance`; missing instance → 404 non-leakage.
- create version, then upload a second version → two immutable rows; duplicate version number → 409.
- active version queryable; previous version readable; overwrite of active version rejected.
- template flags honored; non-shareable/`referenceOnly` template share → 400 `VALIDATION_FAILED`.
- `REFERENCE` share → target uses source active version; `COPY_ON_ADOPT` → independent copy + forked lineage.
- folder-share dry-run reports included folders/templates, skipped, conflicts, permission issues; execute
  shares only the selected branch; unselected branch not exposed; per-item outcomes + honest counts.
- MOD-0220 share-target success / not-found / inactive / unavailable → fail-closed; cancellation preserved.
- binary storage unavailable → controlled failure, no metadata orphan.
- missing permission 403; cross-tenant 404 non-leakage; correlation id preserved across the flow.
- no physical folder/filesystem write; no MOD-0030/0031 side effect.

Frontend tests/smoke:
- library opens in TenantShell (`_LayoutTenantShell`); add document/template form renders; upload + new version
  works; version history panel renders.
- share document/template controls gated by permission; folder-share wizard: source branch → target company →
  include templates → share mode → dry-run preview → execute disabled until a valid dry-run → results.
- no direct service-port call; no client `TenantId`/`X-Tenant-Id`; controlled `reason_code`/`correlation_id`
  display; no stack traces.

Build/verify: `dotnet build` Platform API + Diten.Web; relevant Platform tests; DataTable verifier (if Python
available); RESX parity for tenant languages; `git diff --check`; protected-path verification.

> **Known environment caveat (carried from FU03/FU04/FU05):** a running local fleet can lock service DLLs
> (use `--no-build` tests or an isolated build), the browser smoke needs a permissioned tenant session, and
> the DCP-002 module-id preflight (`verify_module_id.py`) needs a working Python — deferred runtime/preflight
> checks are recorded as validation debt, not silently skipped.

## 18. Ready-for-dev Checklist

- [x] User reviewed this pack and explicitly approved the FU01 controlled-document / SOP / work-instruction /
  versioning / template-sharing foundation scope; status set to `approved` on 2026-06-22.
- [ ] **DCP-002 module-identity gate for `MOD-0029` + `MOD-0029-FU01`:** Blueprint canonical name
  `MOD-0029 — Controlled Documents (SOPs / Work Instructions)` confirmed; registry row added; preflight
  `verify_module_id.py` run green. **Currently UNVERIFIED** — no MOD-0029 row exists in
  `execution/registries/module-id-registry.md` and Python is unavailable in this environment. **CONTROLLED GATE**
- [ ] **Binary/content storage abstraction decision documented:** confirm the approved `IContentStorageGateway`
  seam + provider, and the "no metadata orphan on failure" contract. **CONTROLLED GATE**
- [ ] **CollectionInstance read-only consumption seam confirmed:** id / path / company binding / folder scope
  only; no FU05 mutation. **CONTROLLED GATE**
- [ ] **Versioning + sharing contract documented:** immutable versions, active-version resolution,
  `REFERENCE` vs `COPY_ON_ADOPT` lineage, folder-share dry-run/execute outcome shape. **CONTROLLED GATE**
- [ ] **Permission keys finalized** with MOD-0018/security (new lowercase keys + any uppercase aliases; seed
  ownership). **CONTROLLED GATE**
- [ ] **Gateway route compatibility verified** (existing catch-all GET/POST is sufficient; no new route).
  **CONTROLLED GATE**
- [ ] **TenantShell L10n key set prepared** for all required tenant languages before UI work. **CONTROLLED GATE**
- [ ] **Approval-workflow boundary confirmed:** approval workflow is out of scope unless the MOD-0029 parent
  already approves it. **CONTROLLED GATE**
- [ ] `golden_reference: compact` + `form_field_count: 10` accepted (multi-field add-document form).
- [ ] `entity_base: TenantScopedEntity` accepted (confirmed by FU01/FU02/FU05).
- [ ] FU01 test matrix and protected paths accepted.

## 19. Implementation Notes

- FU01 is the MOD-0029 foundation: it consumes MOD-0028-FU05 `CollectionInstance` nodes read-only and attaches
  controlled documents / SOPs / work instructions / templates with immutable versions and controlled sharing.
  It never edits folder structure, `CollectionDefinition`, or FU05 instantiation.
- MOD-0028 owns folder/tree/structure metadata; **MOD-0029 owns controlled document records, SOP/work
  instruction files, template files, file references, versions, sharing, and document-control metadata.**
- Files go only through the approved binary/content storage abstraction (`FileRef`/`ContentRef`); FU01 never
  creates physical folders, never writes the filesystem directly, and never re-implements the storage provider.
- Versioning is append-only: a new upload is a new immutable version; the active version is resolved by
  `CurrentVersionId`; previous versions stay readable per permission.
- Sharing defaults to `REFERENCE` for templates; `COPY_ON_ADOPT` is opt-in and feature-flag gated until copy
  lineage is verified. Folder/branch share reuses the FU05 dry-run → execute pattern and the same
  flow-correlation discipline.
- MOD-0220 is consumed read-only via its contract for share-target company validation; FU01 never modifies
  `Diten.MdmService`.
- Reuse FU01/FU02/FU05 contracts directly (`Response<T>` `reason_code`/`correlation_id`, route family,
  `[HasPermission]`, `PermissionAliasMap`, typed feature flags, the jsTree folder renderer). Do not fork them.

### Approved Implementation Handoff (effective only after the user sets the pack to approved/ready-for-dev)

- Next executable action after approval: the orchestrator may implement **MOD-0029-FU01 (backend +
  TenantShell) only**.
- Allowed: `ControlledDocument`/`ControlledDocumentVersion`/`TemplateDocument`/`TemplateVersion`/
  `DocumentSharePolicy`/`FolderShareOperation`/`FolderShareOutcome`, attach/version/share/folder-share
  endpoints, the content-storage abstraction seam consumption, the TenantShell Controlled Documents surface,
  permission-gated controls, localization, frontend/backend tests.
- Not allowed: editing MOD-0028 structure / `CollectionDefinition` / FU05 instantiation, implementing a binary
  storage provider, physical folder creation, direct filesystem storage, OCR/indexing, e-signature, approval
  workflow (unless parent-approved), retention/legal hold (MOD-0030), evidence export (MOD-0031), external
  portal / public sharing / email, browser-based editing, gateway `ocelot.json` changes (unless a new route is
  unexpectedly required and a separate integration-agent task is opened), or AuthService seed/alias edits via a
  protected path.

## 20. Follow-up Items

1. **CollectionDefinition template binding follow-up:** binding template files at the `CollectionDefinition`
   (template/baseline) level so newly instantiated companies inherit templates — only if the MOD-0028 parent
   approves it; FU01 attaches templates to `CollectionInstance` only.
2. **Approval workflow follow-up:** controlled-document review/approve lifecycle (state machine, approvers),
   if and when the MOD-0029 parent approves it.
3. **Retention / legal hold (MOD-0030):** retention enforcement over controlled documents/versions remains
   MOD-0030-owned, never FU01.
4. **Evidence export (MOD-0031):** evidence-pack export over controlled documents remains MOD-0031-owned.
5. **Content services follow-up:** OCR / full-text indexing / preview rendering / browser-based editing.
6. **Notification follow-up:** email/in-app notification on share/version events.
7. **Share governance follow-up:** revoke-share, share expiry, and reconciliation of `REFERENCE` shares when a
   source version is superseded.
8. **Retry follow-up:** retry of a failed folder-share subset (mirror of the FU05 retry pattern) once the
   synchronous flow is proven.

Each follow-up requires its own approved or ready-for-dev scope. FU01 does not authorize any later wave, and
does not authorize approval workflow, retention, or evidence export.
