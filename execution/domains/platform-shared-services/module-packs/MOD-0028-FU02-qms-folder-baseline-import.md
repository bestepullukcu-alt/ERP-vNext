---
id: MOD-0028-FU02
name: Documentation Management QMS Workbook Import Profile for Structure Baselines
parent: MOD-0028
previous: MOD-0028-FU01
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: TenantScopedEntity
status: approved
owner: platform-shared-services
branch: feature/document-management-integration
started: 2026-06-15
target: 2026-06-29
form_field_count: 0
---

# MOD-0028-FU02 - Documentation Management QMS Workbook Import Profile for Structure Baselines

## 1. Module Summary

MOD-0028-FU02 is a backend-only follow-up to `MOD-0028 Documentation Management` and the next step after
`MOD-0028-FU01 Backend Contract Foundation`. It implements the first **governed baseline import** slice of the parent
pack's Wave 3 (Corporate governance core): turning an approved QMS folder hierarchy into a tenant-scoped
`CollectionDefinition` tree, a draft `BaselineRelease`, and—on publish—an immutable `BaselineSnapshotManifest` with a
deterministic structural hash.

### Naming reconciliation

This FU02 pack is semantically renamed from **QMS Folder Baseline Import** to **QMS Workbook Import Profile for
Structure Baselines**. QMS is not the general MOD-0028 product concept; it is the first source profile/category for the
general `Structure Baseline` model. The persisted business objects remain `BaselineRelease`,
`BaselineSnapshotManifest`, and `CollectionDefinition`; QMS belongs in source/profile metadata such as
`SourceProfile = QMS` or `StructureCategory = QualityManagement`.

Preferred future API and permission names use `structure-baselines`. Existing `qms-baselines` route, DTO, folder, and
permission names are transitional compatibility names from the first implementation slice and must not be used as the
long-term product vocabulary.

The source business input is the spreadsheet `Configuraiton of QMS folders v2 (1).xlsx`. That workbook is the desired
**corporate baseline folder tree and its metadata**. FU02 treats it strictly as governance metadata:

- it is a baseline *definition* tree, not physical server folders;
- it is not document upload/storage;
- it is not a document lifecycle.

Conceptual mapping inherited from the parent pack:

| Business input | FU02 object |
|---|---|
| Excel folder hierarchy | `CollectionDefinition` tree |
| Published QMS workbook structure profile | `BaselineRelease` structure baseline |
| Immutable published tree snapshot | `BaselineSnapshotManifest` |
| Later company adoption | `CollectionInstance` — **deferred to a later follow-up, not FU02** |

### FU01 Status Context (carried-in debt, not re-opened)

- FU01 core backend implementation is complete.
- FU01 Gateway route integration is PASS (`/api/v1/document-management` + `/{everything}`, GET/OPTIONS only).
- FU01 permission seed/alias/policy is statically verified.
- The FU01 permission task final verdict was **PARTIAL** only because focused-test execution was deferred due to
  local running-process file locks; the implementation itself was inspection-verified.
- The user accepts moving to FU02 with this deferred test execution recorded as **validation debt**.
- FU02 does **not** modify or expand the FU01 foundation except where it must **consume** existing FU01 contract
  patterns: the `Response<T>` `reason_code`/`correlation_id` members, the `api/v1/document-management` route family,
  the `[HasPermission]` attribute, the directional `PermissionAliasMap` convention, the centralized
  `DocumentManagementFeatureFlags`/typed-options pattern, and the `DocumentManagementReasonCodes` catalog.

### Approval Scope

- This pack is `status: approved` for the exact FU02 backend QMS-folder-baseline-import scope only.
- The user explicitly approved the FU02 backend QMS folder baseline import scope. The approval covers
  `CollectionDefinition` + `BaselineRelease` + `BaselineSnapshotManifest`, QMS import dry-run/commit, baseline
  list/detail/publish, definitions list/detail, deterministic hashing, and the import validation summary.
- The approval is **not** approval for: frontend UI, TenantShell import wizard, company instantiation,
  `CollectionInstance`, document upload/storage, physical folder creation, or full MOD-0028 implementation.
- No frontend UI, company instantiation, provisioning, template, exception, local-node, or document-lifecycle
  implementation is approved by this pack.
- FU02 does not re-open or expand FU01, and does not authorize the remaining parent MOD-0028 waves.

## 2. Ownership and Boundaries

### In scope

- QMS folder baseline import foundation: parse/import an approved-format QMS folder hierarchy, dry-run validate
  before commit, and commit only after validation passes.
- Parent-child folder tree construction with deterministic canonical IDs/stable keys, derived path segments and full
  paths, ordering preservation, and structural validation (duplicate sibling paths, empty/invalid names, hierarchy
  gaps, cycles).
- `CollectionDefinition` aggregate (tenant-scoped) persisted with the parent pack's field contract.
- `BaselineRelease` draft creation from an imported tree, and publish when validations pass.
- `BaselineSnapshotManifest` immutable manifest creation on publish with a deterministic, reproducible structural
  hash and definition IDs/hashes.
- An import result/validation summary model that uses controlled `reason_code` and body-level `correlation_id`.
- The minimum FU02 permission subset, its directional alias foundation, and policy enforcement.
- Focused backend contract, validation, tenant-isolation, security, and determinism tests.

### Consumed, not owned

- FU01 `Response<T>` envelope, route family, `[HasPermission]`, alias-map convention, feature flags, and reason codes.
- MOD-0018 permission ownership for new lowercase keys and any approved uppercase spec aliases.
- MOD-0021 audit emit/correlation seams (FU02 emits import/publish audit metadata; it does not own the store).
- Platform Common `TenantScopedEntity`, tenant context, tenant repository filtering, and correlation middleware.

### Explicitly out of scope

- Frontend UI, TenantShell pages, Excel upload screen, navigation, JavaScript, and localization.
- Company instantiation, `CollectionInstance` provisioning, reconciliation, and provisioning jobs.
- MOD-0220 LegalEntity adoption/binding and any company-scope runtime behavior.
- Local collection node management.
- Template master/version/variant, drift, and rebase.
- Exception request/approval/decision/queue/closure/expiry.
- MOD-0029 controlled-document lifecycle, MOD-0030 retention/legal-hold, MOD-0031 evidence-pack export.
- Binary upload/download or repository implementation.
- **Physical file-system folder creation on any server or storage.**
- **Document upload, content storage, or `ContentRef` binary handling.**
- Runtime activation of `POSITION` or `PERSON` scope.
- Re-opening or expanding the FU01 foundation beyond consuming its existing contract patterns.

## 3. Owned Objects

FU02 owns the following three parent MOD-0028 business aggregates plus its import/result contracts:

- `CollectionDefinition` — tenant-scoped imported folder-definition node.
- `BaselineRelease` — draft/published baseline for an imported QMS tree.
- `BaselineSnapshotManifest` — immutable manifest produced on publish.
- `QmsBaselineImport*` request/result contracts (dry-run and commit summaries).
- Minimum FU02 permission mapping records (preferred `structure-baselines.*` keys; existing `qms-baselines.*` keys are
  transitional aliases) plus the two existing FU01 `collection-definitions.*` foundation keys now becoming enforced.
- Repository/index definitions for the three aggregates, tenant-first and soft-delete aware.

FU02 must **not** introduce `CorporateDocumentationRoot`, `CollectionInstance`, `ScopeBinding`, `CollectionBinding`,
`LocalCollectionNode`, `TemplateMaster`, `TemplateMasterVersion`, `TemplateVariant`, `Exception`, `ContentRef`, or
`ProvisioningJob`. Those remain later-wave objects.

## 4. Entity Fields

FU02 persists three tenant-owned aggregates. All use `Diten.Platform.Common.Persistence.TenantScopedEntity` (the
live convention FU01 confirmed). `TenantId`, `IsDeleted`, and technical `Version` are inherited/server-resolved and are
never accepted from client payloads. `DeletedAt` is **not** present on the live common base; FU02 records this gap and,
if a governed `DeletedAt` is required, adds it on the FU02 aggregate itself rather than modifying `Diten.Platform.Common`.
Business versions use semantic names (`BaselineVersion`, `ManifestVersion`), never the technical `Version`.

| Object | Principal fields | Required constraints / indexes |
|---|---|---|
| CollectionDefinition | CanonicalId, ParentCanonicalId, Name, PurposeScope, RequiredByScope, AllowsManualChildren, TemplatesAllowed, AllowedDocClass, DefaultClassificationLevel, DefaultRetentionHint, IsMandatory, IsAutoProvisioned, IsProtected, PathSegment, DisplayOrder, Status, VersionToken (or repo-equivalent concurrency token) | Tenant + CanonicalId unique; acyclic parent tree; sibling PathSegment unique case-insensitively among non-deleted rows; tenant-first index; no hard delete |
| BaselineRelease | BaselineReleaseId, BaselineVersion, EffectiveDate, Status (DRAFT/PUBLISHED), ChangeSummary, SnapshotHash, ManifestId, DeprecationNoticeWindowDays | Tenant + BaselineReleaseId unique; only DRAFT may publish; only PUBLISHED is later instantiable (instantiation out of FU02 scope) |
| BaselineSnapshotManifest | ManifestId, BaselineReleaseId, ManifestVersion, DefinitionIds, DefinitionHashes, StructuralControlsHash, SnapshotHash | Immutable after publish; deterministic and reproducible for identical input; tenant-scoped |

Notes:

- **CanonicalId / stable-key decision (closed):** `CanonicalId` is generated **deterministically**. The same
  `tenant + normalized full path + source baseline key` always yields the same CanonicalId/stable key, so re-import and
  manifest hashing are reproducible. The key conforms to the parent pack rule
  (`^CAN-[A-Z0-9]{2,10}-[A-Z0-9]{2,16}-[0-9]{3,6}$`) or the repository's confirmed deterministic stable-key scheme; the
  exact algorithm is selected against repo conventions during implementation, but test acceptance mandates the
  deterministic behavior regardless of the chosen algorithm.
- **PathSegment normalization (closed):** each `PathSegment` is normalized before keying/uniqueness — trim, collapse
  internal whitespace, clean up/reject forbidden path/control characters — and sibling uniqueness is enforced
  case-insensitively among non-deleted rows.
- **SnapshotHash (closed):** the structural hash is computed by a deterministic algorithm; identical input yields an
  identical `SnapshotHash`. The exact hash algorithm is chosen against repo conventions during implementation, but test
  acceptance mandates the deterministic, reproducible result.
- `FullPath` is server-derived from ordered normalized `PathSegment` values; max depth and length follow parent §12
  limits.
- FU02 does not persist a `CorporateDocumentationRoot`; a `BaselineRelease` here is a catalogable draft/published tree,
  not an active corporate root. Root initialization remains a later wave.

## 5. Repo Scope

### Authorized FU02 implementation scope (after approval)

- `services/Diten.Platform/src/Diten.Platform.API/**` — thin controller actions under the existing route family and
  controlled response metadata wiring.
- `services/Diten.Platform/src/Diten.Platform.Application/**` — CQRS commands/queries/handlers/validators, import
  parser/validator services, result models, permission constants, and reason codes.
- `services/Diten.Platform/src/Diten.Platform.Domain/**` — only if the live repository convention places persisted
  entities/aggregates and repository interfaces here.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` — repository implementations, Mongo index
  registration, configuration binding, DI, and approved alias registration.
- `services/Diten.Platform/tests/**` — focused FU02 contract, validation, security, tenant, and determinism tests.

### Separately governed scope

- `gateway/Diten.ApiGateway/**/ocelot.json` only through an explicit `integration-agent` task, required because FU02
  adds POST endpoints the current GET/OPTIONS-only catch-all does not cover (see §15).
- Permission seed/alias ownership for new `structure-baselines.*` keys through the canonical MOD-0018/security-owned
  location when that location is outside FU02 Platform scope.

No frontend path is in scope.

## 6. Protected Paths

- `.antigravity/**`
- `frontend/**`
- `gateway/**` except through the separate integration-agent task in §15
- `services/Diten.Platform.Common/**`
- `services/Diten.AuthService/**` except through a separately approved MOD-0018/security-owned permission task
- `services/Diten.MdmService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- MOD-0029, MOD-0030, and MOD-0031 implementation files
- Binary repository internals, document storage, and any physical file-system folder creation
- FU01-owned foundation files, except read-only **consumption** of their public contract patterns
- Parent pack `MOD-0028-document-management.md` unless a separate governance reconciliation authorizes its update

## 7. Dependencies

| Dependency | FU02 usage |
|---|---|
| MOD-0028 parent | Supplies object ownership, field contract, API family, failure semantics, and Wave 3 boundary |
| MOD-0028-FU01 | Supplies route family, `Response<T>` `reason_code`/`correlation_id`, `[HasPermission]`, alias-map convention, feature flags, reason codes — consumed, not modified |
| MOD-0018 | Approves new lowercase `structure-baselines.*` keys, transitional `qms-baselines.*` aliases if needed, any uppercase spec aliases, seed ownership, and effective mapping |
| MOD-0021 | Provides audit/correlation seams; FU02 emits import/publish audit metadata and propagates correlation |
| MOD-0032 / Gateway | Owns route hardening; the FU02 POST-method Gateway extension is an integration-agent task |
| Platform Common | Supplies `TenantScopedEntity`, tenant context, tenant repository filtering, and correlation middleware |

FU02 performs **no** MOD-0220 call and adds **no** company behavior; LegalEntity adoption is a later follow-up.

## 8. Runtime Constraints

- Persistence remains MongoDB with tenant isolation on every persisted `CollectionDefinition`, `BaselineRelease`, and
  `BaselineSnapshotManifest` record.
- `TenantScopedEntity` is the FU02 base; no client-controlled `TenantId`, technical `Version`, audit actor, or
  correlation identity is accepted.
- Every FU02 API envelope includes body-level `correlation_id` sourced from the request-scoped correlation context,
  never from client payloads; controlled failures include a stable `reason_code`; internal exceptions and stack
  traces are never returned.
- Cross-tenant detail behavior is 404 non-leakage; unauthorized mutations are 403; restricted lists omit rows.
- Import is a two-phase contract: **dry-run** validates and returns a summary and persists nothing; **commit**
  persists the `CollectionDefinition` tree and a DRAFT `BaselineRelease` only after validation passes.
- Publish is idempotent at the manifest level: publishing the same DRAFT produces a manifest whose `SnapshotHash` is
  deterministic and reproducible for identical input; a published manifest is immutable.
- Soft delete is mandatory for governed lineage; there is no hard-delete path.
- `CORPORATE` is the only scope FU02 touches at definition level; `COMPANY` adoption, `POSITION`, and `PERSON` are
  out of scope and no background process may create records for them.
- **FU02 creates governed baseline metadata only.** It must not create physical file-system folders, must not upload
  or store documents, and must not implement document lifecycle.
- Optimistic concurrency applies to mutable records via the inherited technical version / `VersionToken`; stale
  writes return 409 `CONFLICT` and never silently overwrite.

Feature flags (reused from FU01 typed-options; FU02 adds none unless inspection requires it):

| Key | Default | FU02 relevance |
|---|---|---|
| `mod0028.corporate_root.enabled` | on | Corporate baseline import is the FU02 surface |
| `mod0028.company_provisioning.enabled` | on | Not consumed by FU02 (company adoption deferred) |
| `mod0028.position_scope.enabled` | off | Must stay off; no POSITION records |
| `mod0028.person_scope.enabled` | off | Must stay off; no PERSON records |

If FU02 needs an import-specific guard, it adds a centralized typed-options flag consistent with the FU01 pattern;
scattered string literals are prohibited and missing configuration resolves to safe defaults.

## 9. Layout & Shell Contract

- `shell: none` is intentional because FU02 contains no frontend surface.
- No `.cshtml`, controller view action, menu item, navigation entry, or JavaScript file is authorized.
- The parent module remains tenant-facing and uses `Layout = "_LayoutTenantShell";` in later UI waves.
- A later TenantShell follow-up (baseline catalog, import wizard, tree viewer) owns the UI; FU02 adds none of it.

## 10. Backend File Convention

FU02 follows the live Diten.Platform CQRS shape (parent §10 / Compact Golden Reference action-based shape):

Naming reconciliation: QMS import commands and parser services may keep `Qms*` names because they are specific to the
QMS workbook source profile. Baseline list/detail/publish and shared route-facing names should migrate toward
`StructureBaseline*` when implementation is reconciled; any `DocumentManagementQmsBaseline` slice name is transitional
compatibility, not the product-wide naming model.

```text
Features/DocumentManagementQmsBaseline/
|-- Commands/
|   |-- DryRunQmsBaselineImportCommand.cs        (sealed record)
|   |-- CommitQmsBaselineImportCommand.cs        (sealed record)
|   `-- PublishQmsBaselineCommand.cs             (sealed record)
|-- Queries/
|   |-- GetQmsBaselineListQuery.cs               (sealed record)
|   |-- GetQmsBaselineByIdQuery.cs               (sealed record)
|   `-- GetQmsBaselineDefinitionsQuery.cs        (sealed record)
|-- Handlers/
|   |-- CommandHandlers/
|   |   |-- DryRunQmsBaselineImportHandler.cs     (sealed class, no suffix)
|   |   |-- CommitQmsBaselineImportHandler.cs
|   |   `-- PublishQmsBaselineHandler.cs
|   `-- QueryHandlers/
|       |-- GetQmsBaselineListHandler.cs
|       |-- GetQmsBaselineByIdHandler.cs
|       `-- GetQmsBaselineDefinitionsHandler.cs
|-- Validators/
|   |-- DryRunQmsBaselineImportValidator.cs       (no Command suffix)
|   |-- CommitQmsBaselineImportValidator.cs
|   `-- PublishQmsBaselineValidator.cs
|-- Services/
|   |-- IQmsFolderImportParser.cs                 (Application interface)
|   |-- QmsFolderTreeValidator.cs                 (structure/cycle/duplicate checks)
|   `-- BaselineSnapshotHasher.cs                 (deterministic structural hash)
`-- DocumentManagementQmsBaselineModels.cs        (all DTOs/result models in one file)
```

- Commands/queries are separate sealed records; handlers are sealed classes named `{Verb}{Slice}Handler` with no
  `CommandHandler`/`QueryHandler` suffix; validators are `{Verb}{Slice}Validator` with no `CommandValidator` suffix.
- Mutating commands return `Response<NoContent>` or a typed result envelope, never `Response<bool>`.
- Controllers inherit `CustomBaseController`, remain thin, and dispatch through MediatR.
- **Source-format decision (closed):** FU02 uses **direct `.xlsx` parsing**. `IQmsFolderImportParser` is an
  Application interface with an Infrastructure implementation that reads the approved workbook
  `Configuraiton of QMS folders v2 (1).xlsx` as the canonical business fixture. The parser consumes a stream/file
  abstraction so it is identical under an API request and under a test fixture. The exact `.xlsx` library is confirmed
  against repo conventions during implementation and bound in DI. Handlers use no raw `HttpClient`.
- **Canonical sheet + hierarchy decision (closed by user after real-workbook validation):**
  - **Canonical sheet:** `last version` (selected by sheet name via a central parser constant, not a hardcoded index).
  - **Canonical hierarchy encoding:** **dotted outline code** in the `Folder (full path)` column (e.g. `0`, `0.01`,
    `00.01.01`), with the node name in the separate `Folder name` column.
  - **`Arkusz1` (level columns `1st/2nd/3rd/4th`) is non-canonical** — a helper/reference format. The parser may keep
    permissive level-column/slash support for fixtures, but the FU02 canonical import contract is the `last version`
    dotted-code sheet.
  - **The parser must not silently flatten the dotted-code hierarchy.** Dotted codes are resolved into a nested tree;
    a dotted code is never used directly as a folder name or path segment.
  - **Dotted-code parsing is required for FU02 final validation.** If the canonical `last version` sheet is absent, the
    parser returns a controlled `VALIDATION_FAILED` (`canonical_sheet_not_found`), never a fabricated success.
  - Dotted codes are normalized **numerically** (each `.`-segment is integer-parsed, stripping leading zeros) so
    `00.01.01` resolves to parent `0.01` and `0.01` to parent `0`; a missing parent code yields a controlled
    `VALIDATION_FAILED` hierarchy gap, and duplicate siblings under one parent yield `CONFLICT`.
  - Because real QMS folder names legitimately contain `/` (e.g. `Versioning & Check-in/Check-out`), dotted-mode
    treats each `Folder name` as an **atomic** segment (not split on `/`); `FullPath` is server-derived by joining the
    resolved ancestor names. Source order is preserved.
- **Import input contract:** the parser accepts two input paths that resolve to the same parser input stream —
  (a) an API request import payload/file abstraction, and (b) a test fixture stream. FU02 has no frontend, so there is
  **no real UI upload screen**; the endpoint contract is exercised at the backend level only.
- Repository access uses the live `IRepository<T>`/tenant repository convention; tenant-first indexes are mandatory.
- Folder import, tree validation, and snapshot hashing are split into focused services rather than oversized handlers.

## 11. Frontend File Contract

No frontend files are in scope. `golden_reference: none` and `form_field_count: 0` are intentional.

Future MOD-0028 Structure Baselines UI (catalog, QMS import wizard, tree viewer) remains governed by the parent pack's
TenantShell contract and requires a separate approved follow-up.

## 12. Validation Rules

| Contract input / operation | Required | Rule | Failure |
|---|---|---|---|
| Import source payload | Yes | Conforms to the approved QMS source-format schema; non-empty | 400 `VALIDATION_FAILED` |
| Folder name | Yes | Trimmed, non-empty, 3-120 chars, no path/control characters | 400 `VALIDATION_FAILED` (empty/invalid name) |
| PathSegment | Yes | Max 100; forbidden path/control characters; trimmed | 409 `CONFLICT` on duplicate sibling segment (case-insensitive) |
| Parent reference | Conditional | Parent exists in the same import; no orphan/gap; no cycle | 400 `VALIDATION_FAILED` (hierarchy gap / cycle) |
| Ordering | Yes | DisplayOrder preserved from source order; deterministic | Recorded in summary; non-deterministic order rejected |
| CanonicalId / stable key | Derived | Deterministic for identical input; immutable after commit | 400 `VALIDATION_FAILED` if non-deterministic |
| Dry-run | N/A | Validates and returns a summary; persists nothing | Summary only; no write |
| Commit | Yes | Allowed only after a passing validation; builds tree + DRAFT baseline | 400 `VALIDATION_FAILED` if validation did not pass |
| Publish | Yes | Source baseline must be DRAFT and structurally valid | 400 `VALIDATION_FAILED`; manifest immutable once published |
| VersionToken | Mutation | Must match current mutable record | 409 `CONFLICT` on stale write |
| TenantId | Never client input | Resolved from tenant context only | Request contract rejected / test fails |
| CorrelationId | All APIs | Non-empty and propagated; body and header identical | Generated server-side if absent |

The dry-run endpoint has no tenant override and no database write. No FU02 endpoint accepts a client `TenantId`,
performs a MOD-0220 call, creates a company instance, or writes a physical folder.

## 13. Failure Path to Verify

- **Invalid Excel/schema/input:** 400 `VALIDATION_FAILED` with field/structural errors; no stack trace.
- **Duplicate sibling path:** 409 `CONFLICT`; the import does not persist a conflicting tree.
- **Invalid hierarchy (empty name, parent gap, cycle):** 400 `VALIDATION_FAILED`; reported in the summary findings.
- **Publish of an invalid or non-DRAFT baseline:** 400 `VALIDATION_FAILED`; no manifest is created or mutated.
- **Missing permission:** 403 `PERM_DENIED`; no handler side effect and no success audit event.
- **Cross-tenant detail identifier:** 404 `NOT_FOUND_NON_LEAKAGE`; no restricted identifier in response or logs.
- **Stale VersionToken on a mutation:** 409 `CONFLICT`; silent overwrite is prohibited.
- **Disabled POSITION/PERSON scope or company adoption request:** rejected; no entity/job is created.
- **Determinism breach:** the same input producing a different `SnapshotHash` is a test failure.
- **No company instance created:** committing/publishing a baseline must never create a `CollectionInstance` or any
  company-scoped record.
- **No physical folder / no document storage:** import/commit/publish must never touch the file system or upload binary
  content; verified by the absence of any storage/file-system seam in FU02 code.
- All controlled errors carry `reason_code` and body/header `correlation_id` parity with no internal exception text.

## 14. Authorization Convention

- Policy: `[Authorize]` on the tenant-facing controller; `[HasPermission]` per semantic action.
- Actor type: `tenant_user`.
- Runtime canonical format: PKS-001 lowercase dotted keys under `platform.document-management.{resource}.{action}`.
- Spec keys remain traceable directional aliases only if MOD-0018/security approves canonical-to-alias mapping;
  reverse grants and dynamic aliases are prohibited (consistent with the FU01 `PermissionAliasMap` convention).
- Backend attributes and any future frontend gate use the same lowercase effective key.

Minimum FU02 permission subset (5):

| Selected runtime canonical key | Endpoint(s) | FU01/FU02 status |
|---|---|---|
| `platform.document-management.structure-baselines.import` | QMS dry-run + commit profile | **preferred** new general key; seed/alias ownership = MOD-0018/security |
| `platform.document-management.structure-baselines.view` | list + by-id | **preferred** new general key; seed/alias ownership = MOD-0018/security |
| `platform.document-management.structure-baselines.publish` | publish | **preferred** new general key; seed/alias ownership = MOD-0018/security |
| `platform.document-management.collection-definitions.list` | definitions list | already seeded+aliased in FU01 (foundation) → enforced by FU02 |
| `platform.document-management.collection-definitions.view` | definition detail | already seeded+aliased in FU01 (foundation) → enforced by FU02 |

Transitional aliases, if already implemented before this reconciliation:

| Transitional key | Preferred replacement | Migration note |
|---|---|---|
| `platform.document-management.qms-baselines.import` | `platform.document-management.structure-baselines.import` | Alias only; do not expand for new structure types |
| `platform.document-management.qms-baselines.view` | `platform.document-management.structure-baselines.view` | Alias only |
| `platform.document-management.qms-baselines.publish` | `platform.document-management.structure-baselines.publish` | Alias only |

Directional uppercase spec-alias proposals (MOD-0018/EA approval required before runtime use):

| Lowercase runtime key | Candidate uppercase spec alias | Note |
|---|---|---|
| `...collection-definitions.list` | `MOD0028.COLLECTION_DEFINITION.LIST` | already mapped in FU01 alias map |
| `...collection-definitions.view` | `MOD0028.COLLECTION_DEFINITION.VIEW` | already mapped in FU01 alias map |
| `...structure-baselines.publish` | `MOD0028.BASELINE_RELEASE.PUBLISH` | parent spec has this key; direction spec→runtime |
| `...structure-baselines.view` | `MOD0028.BASELINE_RELEASE.LIST` | parent spec `LIST`; EA confirms view↔list mapping |
| `...structure-baselines.import` | `MOD0028.BASELINE_RELEASE.IMPORT` or profile-specific alias | EA/MOD-0018 decide whether profile imports share one alias or add source-profile aliases |

Permission strategy (controlled gate):

- FU02 implementation **may add permission constants/attributes within local Platform scope** and protect every runtime
  endpoint with `[HasPermission]` using the lowercase effective key.
- If seed/alias ownership requires a protected security-owned path outside FU02 Platform scope, implementation
  **stops and reports** rather than editing a protected path.
- New `structure-baselines.*` seeds are added only in the canonical security-owned seed location through a separately
  authorized MOD-0018/security task when that location is outside FU02 Platform scope.
- MOD-0018/security approves the lowercase keys and any uppercase aliases before runtime use.
- FU02 may not claim a permission as `confirmed` until seed, backend policy/attribute, alias behavior, and focused
  tests agree.
- **If the permission seed is missing, FU02 validation may remain `PARTIAL`, but the FU02 release gate does not close**
  until the seed/alias ownership is satisfied and the keys are `confirmed`.
- Missing permission returns 403 `PERM_DENIED` with body/header correlation parity.

## 15. Gateway / API Routing Decision

Decision (controlled gate): a Gateway change **is required** before any browser/frontend consumer can call FU02's
mutating endpoints, but it is a **separate integration-agent task** that does not block starting FU02 backend work.

- The FU01 integration task added `/api/v1/document-management` and `/api/v1/document-management/{everything}` as
  **GET and OPTIONS only**. FU02 introduces `POST` endpoints (`dry-run`, `commit`, `publish`), which the current
  catch-all does not allow.
- Required change: widen the existing `/api/v1/document-management/{everything}` catch-all to include `POST` (and keep
  `GET`/`OPTIONS`), preserving version-explicit `v1` routing and backward compatibility; do not add `v2` or
  unversioned routes.
- `gateway/Diten.ApiGateway/**/ocelot.json` remains protected and is changed only by a separate integration-agent
  task after the FU02 backend route/method contract is fixed.
- Gateway acceptance includes POST forwarding, authorization-header forwarding, correlation-header preservation,
  controlled 404 behavior, and no regression to existing routes.
- Frontend must use Gateway port `5000` or a same-origin proxy and must never call `5057` directly.

Sequencing rule (controlled gate):

- **FU02 backend implementation may start before the Gateway POST widening**, developed and tested directly against the
  Platform API endpoints.
- **Browser/frontend consumption cannot start until the Gateway POST widening is PASS.**
- **FU02 final release cannot be considered complete until the Gateway POST route supports the required POST endpoints**
  (`dry-run`, `commit`, `publish`).

Preferred FU02 endpoints (all version-explicit under `api/v1/document-management`; names may adjust to repo convention):

| Method | Path | Permission | Behavior |
|---|---|---|---|
| POST | `api/v1/document-management/structure-baselines/import/qms/dry-run` | `...structure-baselines.import` | Validate QMS profile only; persist nothing; return summary |
| POST | `api/v1/document-management/structure-baselines/import/qms/commit` | `...structure-baselines.import` | Persist definition tree + DRAFT baseline after validation passes |
| GET | `api/v1/document-management/structure-baselines` | `...structure-baselines.view` | List tenant structure baselines |
| GET | `api/v1/document-management/structure-baselines/{id}` | `...structure-baselines.view` | Baseline detail (404 non-leakage cross-tenant) |
| POST | `api/v1/document-management/structure-baselines/{id}/publish` | `...structure-baselines.publish` | Publish DRAFT → immutable manifest |
| GET | `api/v1/document-management/structure-baselines/{id}/definitions` | `...collection-definitions.list` | List the imported definition tree for a baseline |
| GET | `api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}` | `...collection-definitions.view` | Single definition detail |

Backward-compatible transitional endpoints, if already implemented before rename:

| Legacy path | Preferred replacement |
|---|---|
| `api/v1/document-management/qms-baselines/import/dry-run` | `api/v1/document-management/structure-baselines/import/qms/dry-run` |
| `api/v1/document-management/qms-baselines/import/commit` | `api/v1/document-management/structure-baselines/import/qms/commit` |
| `api/v1/document-management/qms-baselines` | `api/v1/document-management/structure-baselines` |
| `api/v1/document-management/qms-baselines/{id}` | `api/v1/document-management/structure-baselines/{id}` |
| `api/v1/document-management/qms-baselines/{id}/publish` | `api/v1/document-management/structure-baselines/{id}/publish` |
| `api/v1/document-management/qms-baselines/{id}/definitions` | `api/v1/document-management/structure-baselines/{id}/definitions` |
| `api/v1/document-management/qms-baselines/{id}/definitions/{canonicalId}` | `api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}` |

## 16. Acceptance Criteria

- [x] FU02 pack is `status: approved` for the exact backend QMS folder baseline import scope only.
- [ ] Scope is limited to the backend QMS folder baseline import foundation; no full MOD-0028 implementation.
- [ ] FU01 deferred test-execution debt is recorded but does not expand FU02 scope, and FU01 foundation is consumed,
  not modified.
- [ ] `CollectionDefinition`, `BaselineRelease`, and `BaselineSnapshotManifest` are implemented with `TenantScopedEntity`
  and the parent field contract; `TenantId` is never client-controlled.
- [ ] `CollectionInstance`, company provisioning, MOD-0220 adoption, local nodes, templates, exceptions, and document
  lifecycle are explicitly out of scope and not created.
- [ ] Physical folder creation, document upload, and content storage are explicitly out of scope and never performed.
- [ ] Import is two-phase: dry-run validates and persists nothing; commit persists only after validation passes.
- [ ] Publish creates an immutable `BaselineSnapshotManifest` with a deterministic structural hash reproducible for
  identical input.
- [ ] All FU02 endpoints are version-explicit under `api/v1/document-management`; no `v2` or unversioned route.
- [ ] Permissions are the minimal five-key FU02 subset; new `structure-baselines.*` keys have an approved seed/alias/
  policy mapping owned by MOD-0018/security; transitional `qms-baselines.*` aliases are allowed only for backward
  compatibility; backend and any future frontend use the same effective lowercase key.
- [ ] Controlled failures (400/403/404/409) return `reason_code` and body/header `correlation_id` with no stack trace.
- [ ] Soft-delete, tenant-first indexes, and optimistic-concurrency requirements are implemented and tested.
- [ ] The Gateway POST-method extension is explicit and assigned to a separate integration-agent task.
- [ ] No frontend governance UI, view, JavaScript, navigation, or localization file is included.

## 17. Test Expectations

Required FU02 tests:

- Dry-run with a valid QMS import returns a passing summary and writes nothing.
- Dry-run with an invalid hierarchy (parent gap / cycle / empty name) returns controlled `VALIDATION_FAILED` findings.
- Duplicate sibling path produces a `CONFLICT`.
- Commit creates the definition tree plus a DRAFT `BaselineRelease`.
- Publish creates an immutable `BaselineSnapshotManifest`.
- Deterministic structural hash: identical input yields an identical `SnapshotHash`; manifest is reproducible.
- Tenant isolation: cross-tenant baseline/definition detail returns 404 non-leakage; lists omit other tenants' rows.
- Missing permission returns 403 `PERM_DENIED` for each guarded action.
- Controlled-failure tests assert `reason_code` and body/header `correlation_id` with no stack-trace/exception text.
- No company instance is created by commit or publish.
- No frontend files are added or modified by the change.
- Permission tests for all five FU02 keys: canonical match, approved alias match, missing claim, and no
  reverse/dynamic alias behavior.
- Build `services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj` and run the relevant Platform
  application, contract, authorization, persistence, and security tests.
- `git diff --check` and protected-path verification.

No frontend build, DataTable verifier, RESX parity, or browser UI smoke is required because FU02 has no frontend.

> **Known environment caveat (carried from FU01):** a running local dev fleet can lock Platform/Auth service DLLs and
> block `dotnet test`. The FU02 implementer must either stop this repo's running services briefly or run tests in an
> isolated worktree/build output; a deferred test run must be recorded as validation debt, not silently skipped.

## 18. Ready-for-dev Checklist

- [x] User reviewed this pack and explicitly approved only the FU02 backend QMS workbook import profile scope.
- [x] **Source-format contract selected: direct `.xlsx` parsing** of `Configuraiton of QMS folders v2 (1).xlsx` as the
  canonical business fixture, via an Application `IQmsFolderImportParser` with an Infrastructure implementation. The
  workbook may be copied into the repo test-fixture area or normalized into a small sample `.xlsx` fixture during
  implementation. If repo policy forbids binary Excel fixtures, implementation **stops and reports** the normalized
  JSON/CSV fixture option rather than expanding scope. **CONTROLLED GATE**
- [x] **Deterministic `CanonicalId`/stable-key and `SnapshotHash` are required:** same `tenant + normalized full path +
  source baseline key` → same key; same input → same `SnapshotHash`. PathSegment normalization (trim, whitespace
  collapse, forbidden-character cleanup/reject, case-insensitive sibling uniqueness) is mandatory. The exact algorithm
  is chosen at implementation per repo conventions; test acceptance mandatorily verifies the deterministic behavior.
- [x] `entity_base: TenantScopedEntity` is accepted as the live Platform tenant convention (confirmed by FU01); the
  parent-pack `BaseEntity` wording does not authorize `Diten.Platform.Domain.Common.BaseEntity`.
- [x] FU01 deferred test-execution debt is acknowledged as carried-in; it is recorded but **does not block FU02 backend
  implementation** and is not expanded into FU02 scope.
- [ ] Parent reference `MOD-0028`, previous foundation `MOD-0028-FU01`, and follow-up identity `MOD-0028-FU02` are
  recorded in `execution/registries/module-id-registry.md` (the FU01 row is also still pending). **CONTROLLED GATE**
- [ ] DCP-002 module-identity preflight (`verify_module_id.py --check-id MOD-0028-FU02 --parent MOD-0028`) is run when
  Python/openpyxl is available; FU02 is an FU/child of an existing Blueprint module, so no new MOD ID is minted. If the
  tooling is unavailable, report it without inventing identity data. **CONTROLLED GATE**
- [ ] **Permission seed/alias ownership remains a controlled gate:** FU02 may add permission constants/attributes in
  local Platform scope and protect endpoints with `[HasPermission]`; if seed/alias ownership requires a protected
  security-owned path, implementation stops/reports. A missing seed may leave validation `PARTIAL` but the release gate
  does not close until MOD-0018/security confirms the three `structure-baselines.*` keys, the two
  `collection-definitions.*` keys becoming enforced, and any uppercase aliases / transitional `qms-baselines.*`
  aliases.
  **CONTROLLED GATE**
- [ ] **Gateway POST widening remains a separate integration-agent task:** FU02 backend may start before it; browser
  consumption cannot start until it is PASS; FU02 final release does not close until the Gateway POST route supports the
  required POST endpoints. `ocelot.json` remains outside FU02. **CONTROLLED GATE**
- [x] FU02 test matrix and protected paths are accepted.
- [x] Status set to `approved` for runtime implementation of the FU02 backend scope.

## 19. Implementation Notes

- FU02 is a bounded slice of the parent pack's Wave 3 (Corporate governance core). It implements
  `CollectionDefinition` + `BaselineRelease` + `BaselineSnapshotManifest` from a QMS folder import, and stops before
  `CorporateDocumentationRoot` activation, company adoption, and all later waves.
- The Excel workbook is **governance metadata**: a baseline folder-definition tree and its attributes. FU02 turns it
  into tenant-scoped definition records and a publishable, hashable baseline. It does not create real folders, store
  documents, or implement any document lifecycle.
- Reuse FU01 contracts directly: `Response<T>` optional `reason_code`/`correlation_id`, the `api/v1/document-management`
  route family, `[HasPermission]`, the directional `PermissionAliasMap`, centralized feature flags, and
  `DocumentManagementReasonCodes`. Do not duplicate or fork these.
- `DeletedAt` is absent from the live `TenantScopedEntity`; if governed soft-delete timestamps are required, add the
  field on the FU02 aggregate, not on `Diten.Platform.Common`.
- Determinism is a first-class requirement: canonical IDs/keys, ordering, and the manifest structural hash must be
  reproducible so re-import and publish are verifiable and idempotent. The **exact** CanonicalId/stable-key and
  `SnapshotHash` algorithms are selected during implementation according to repo conventions, but the FU02 test
  acceptance **mandatorily verifies the deterministic behavior** (same input → same key and same `SnapshotHash`)
  regardless of which concrete algorithm is chosen.
- PathSegment normalization (trim, whitespace collapse, forbidden-character cleanup/reject, case-insensitive sibling
  uniqueness) is part of the deterministic keying contract and is exercised by tests.
- The route family existing in the gateway does not prove FU02 capability; no endpoint may fabricate success or return
  an empty business collection to make a smoke test pass.
- Audit/correlation seams are emitted for import and publish; the full MOD-0028 audit event catalog remains a later
  wave.

### Approved Implementation Handoff

- Next executable action: the orchestrator may implement **FU02 backend only**.
- Allowed:
  - `CollectionDefinition`
  - `BaselineRelease`
  - `BaselineSnapshotManifest`
  - QMS import dry-run
  - QMS import commit
  - baseline list / detail
  - baseline publish
  - definitions list / detail
  - deterministic hash (CanonicalId/stable-key + `SnapshotHash`)
  - import validation summary
- Not allowed:
  - frontend
  - Gateway `ocelot.json` change (separate integration-agent task)
  - company instantiation
  - `CollectionInstance`
  - physical folder creation
  - document upload / storage
  - MOD-0029 / MOD-0030 / MOD-0031
  - template / exception / local-node
- Source format: direct `.xlsx` parsing of `Configuraiton of QMS folders v2 (1).xlsx`; stop/report if repo policy
  forbids binary Excel fixtures (fall back to a normalized JSON/CSV fixture).
- Permissions: add local Platform `[HasPermission]` constants/attributes; stop/report if seed/alias ownership needs a
  protected security-owned path; release gate stays open until MOD-0018/security confirms the keys.
- Gateway: backend may proceed now; browser consumption and final release wait on the separate Gateway POST-widening
  integration-agent task.

## 20. Follow-up Items

1. **Company adoption follow-up:** MOD-0220-bound `CollectionInstance` instantiation from a published baseline,
   provisioning, reconciliation, and jobs.
2. **Corporate root follow-up:** `CorporateDocumentationRoot` initialization/lock and active-baseline binding.
3. **TenantShell UI follow-up:** Structure Baselines catalog, QMS import wizard/upload screen, publish flow, and tree
   viewer.
4. **Local governance follow-up:** local nodes and exception request/detail/queue/expiry.
5. **Template governance follow-up:** masters, immutable versions, variants, drift, and rebase.
6. **Release inspection follow-up:** full audit catalog, NL-01 matrix, accessibility, observability, security,
   Gateway, and release gates.
7. **FU01 validation-debt closure:** execute the deferred FU01 focused/authorization tests once the running-process
   file-lock constraint is resolved.

Each follow-up requires its own approved or ready-for-dev scope. FU02 does not authorize any later wave.
