---
id: MOD-0028-FU05
name: Documentation Management Company Adoption & Collection Instance Provisioning
parent: MOD-0028
previous: MOD-0028-FU04
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: TenantScopedEntity
status: approved
owner: platform-shared-services
branch: feature/pss/mod-0028-fu05-company-adoption-collection-instance-provisioning
started: 2026-06-16
target: 2026-07-13
form_field_count: 0
---

# MOD-0028-FU05 - Documentation Management Company Adoption & Collection Instance Provisioning

> Short alias: **Structure Baseline Instantiation Wizard**. Product concept is generic ("Documentation Structures",
> "Instantiate / Apply to Company"); **QMS is only a profile/category, never the product-wide concept.**

## 1. Module Summary

MOD-0028-FU05 is a **backend + TenantShell-frontend** follow-up to `MOD-0028 Documentation Management`, the next step
after FU01 (backend contract), FU02 (baseline import), FU03 (tenant baseline UI), and FU04 (manual structure builder).
FU05 implements the parent pack's Wave 4 (Company adoption): it lets a tenant user **instantiate a PUBLISHED
documentation structure baseline for a specific company / legal entity**, producing a company-scoped
`CollectionInstance` tree. The instantiation surface supports both the existing full-tree behavior and
**Selective Structure Baseline Instantiation**, where the user applies only selected branch/subtree portions of the
published baseline.

FU04/FU02/FU03 **define and publish** the template/baseline (the `CollectionDefinition` tree on a `BaselineRelease`).
FU05 **applies** a PUBLISHED baseline to a real company. Example: the `2026 Corporate Documentation Structure`
baseline release → instantiate for `COMP-001` → creates a company-specific `CollectionInstance` tree.

| Concept | Meaning |
|---|---|
| `BaselineRelease` | Published template/version (FU02/FU04 owned; FU05 consumes PUBLISHED only) |
| `CollectionDefinition` | Canonical template/tree node (FU02/FU04 owned) |
| `CollectionInstance` | Company-specific instantiated tree node (**FU05 owned**) |
| Instantiation | Operation that creates/updates a company-specific instance tree from a PUBLISHED baseline |
| ScopeBinding | Company/plant/business-unit scope binding (embedded) |
| Full instantiation | Applies the entire PUBLISHED baseline tree to the company |
| Partial instantiation | Applies one selected branch/subtree from the PUBLISHED baseline to the company |
| Multi-branch instantiation | Applies multiple selected branches/subtrees to the same company instance scope |

### Selective Structure Baseline Instantiation

FU05 must support the following selection modes for dry-run and execute:

| Mode | Behavior | First-version status |
|---|---|---|
| `FULL_TREE` | Existing behavior; the whole `CollectionDefinition` tree for the PUBLISHED baseline is included | In scope |
| `SELECTED_BRANCHES` | User selects one or more `CollectionDefinition` nodes; selected nodes and descendants are included, and required ancestors are included for tree integrity | In scope |
| `SELECTED_NODES` | User selects individual nodes without automatically including children | Deferred follow-up |

Default request behavior is `selectionMode = FULL_TREE`, `selectedCanonicalIds = []`, `includeDescendants = true`,
and `includeRequiredAncestors = true`.

### FU04 status context (consumed, not re-opened)

- FU04 is code-complete and offline-green; its final runtime smoke is PARTIAL only because the agent environment could
  not perform a browser login/smoke (no defect).
- FU04 creates/edits/publishes baselines. **FU05 consumes only PUBLISHED baselines.**
- FU05 does **not** re-open the FU04 manual builder except as navigation/handoff (a link back to the designer).

### Naming reconciliation (important)

- The runtime backend still uses the `qms-baselines.*` permission/route family from FU02/FU04. FU05 introduces the
  **generic product concept** — `instantiations` / `collection-instances` routes and "Documentation Structures" UI —
  while keeping `qms-baselines` as a profile only.
- FU05 mints new generic permission keys (§8); it does not rename existing `qms-baselines.*` keys. Any reconciliation
  of the legacy family is a separate MOD-0018/security task, not FU05.

### Approval Scope

- This pack is `status: approved` by explicit user approval on 2026-06-17.
- Approval is limited to the FU05 company-adoption / instantiation / `CollectionInstance` provisioning scope only.
- Approved runtime work is limited to: PUBLISHED baseline selection, Company/LegalEntity selection, optional Plant ID /
  Business Unit ID / Instance Token, dry-run preview, execute instantiation, company-scoped `CollectionInstance` tree
  creation, **full-tree or selected-branch baseline instantiation**, idempotent provisioning, created/skipped/failed
  counts, per-node outcomes, retry failed subset if feasible, flow-level correlation id across dry-run/execute/retry,
  TenantShell Instantiation Wizard, `CollectionInstance`
  list/detail if the repo convention supports it, permission-gated backend endpoints and frontend controls, controlled
  `reason_code`/`correlation_id` failures, MOD-0220 LegalEntity fail-closed seam, and feature-flagged local-smoke
  fallback only if MOD-0220 is unavailable.
- No document lifecycle, retention, evidence export, template editing, physical folder creation, or binary storage is
  approved by this pack.

## 2. Ownership and Boundaries

### In scope

- Selection of a PUBLISHED `BaselineRelease` (DRAFT cannot be instantiated; no-published-release blocks the wizard).
- Company / scope selection (Company/LegalEntity id; optional Plant, Business Unit, Instance Token), with a MOD-0220
  LegalEntity fail-closed validation seam (and a controlled local-only fallback when MOD-0220 is unavailable).
- **Dry-run preview** (no mutation) returning diagnostics: valid/blocked, warnings, errors, nodes-to-create,
  nodes-to-skip, conflicts, missing prerequisites, selected/included/excluded canonical-id counts, included ancestors,
  included descendants, and blocked selections.
- **Execute instantiation**: creates company-scoped `CollectionInstance` records from a PUBLISHED baseline;
  idempotent (deterministic instance key); returns counts + per-node outcomes. Execute creates only nodes included by
  the current dry-run plan; unselected branches are not instantiated.
- **Selective Structure Baseline Instantiation**: full-tree (`FULL_TREE`) and selected-branch (`SELECTED_BRANCHES`)
  modes for applying all or part of a PUBLISHED `CollectionDefinition` tree to a company.
- **Retry failed subset** (pack defines it; implementation may defer with graceful UI handling).
- Company-scoped instance aggregates + an instantiation-operation/outcome lineage.
- Flow-level correlation id shared across dry-run/execute/retry; MOD-0021 audit/correlation seams.
- A TenantShell **Instantiation Wizard** (Select release → Dry-run → Execute → Results).
- Focused backend + frontend tests.

### Consumed, not owned

- FU02/FU04 `BaselineRelease` + `CollectionDefinition` (PUBLISHED), `Response<T>` (`reason_code`/`correlation_id`),
  the `api/v1/document-management` family, `[HasPermission]`, and the directional `PermissionAliasMap` convention.
- MOD-0220 LegalEntity lookup/eligibility validation (the FU01-confirmed `ILegalEntityReferenceValidator` seam).
- MOD-0018 permission ownership for new keys; MOD-0021 audit store.
- Platform Common `TenantScopedEntity`, tenant context, tenant repository filtering, correlation middleware.

### Explicitly out of scope

- Editing `CollectionDefinition` templates; manual baseline builder changes (FU04); QMS import changes (FU02).
- Creating or editing a PUBLISHED baseline (FU05 reads PUBLISHED only).
- MOD-0029 controlled-document lifecycle; MOD-0030 retention/legal-hold; MOD-0031 evidence-pack export.
- Physical file-system folder creation; document upload/storage; binary/content repository; template file management.
- Workflow approval lifecycle; retention enforcement.
- `SELECTED_NODES` mode (single-node selection without descendants) and company-local override nodes.
- Removing/deleting previously instantiated branches or reconciling a company instance after a later baseline release.
- User/person/position scoped instantiation.

## 3. Owned Objects

FU05 owns the company-adoption aggregates plus the instantiation lineage and its contracts:

- `CollectionInstance` — company-specific instantiated tree node (the primary owned aggregate).
- `ScopeBinding` — embedded company/plant/BU scope binding value object (MOD-0220 GUID for COMPANY scope).
- `InstantiationOperation` — a dry-run/execute/retry operation record (status, counts, correlation id, lineage).
- `InstantiationOutcome` — per-node outcome (nodeKey, canonicalId, status, reason_code, message, retryable).
- Instantiation dry-run/execute/retry request/result contracts.
- Selection contract fields on dry-run/execute: `selectionMode`, `selectedCanonicalIds`, `includeDescendants`,
  `includeRequiredAncestors`.
- Minimum FU05 permission mapping records (§8).

FU05 must **not** create or mutate `CollectionDefinition`, `BaselineRelease`, `BaselineSnapshotManifest`,
`TemplateMaster*`, `Exception`, `ContentRef`, or any document-lifecycle object. It must not introduce binary storage
or physical folders.

## 4. Entity Fields

FU05 persists tenant-owned, company-scoped aggregates using `Diten.Platform.Common.Persistence.TenantScopedEntity`
(the live convention confirmed by FU01/FU02). `TenantId`, `IsDeleted`, and technical `Version` are inherited /
server-resolved and never accepted from client payloads. `DeletedAt` is added on the FU05 aggregate (not on
`Diten.Platform.Common`). Business versions use semantic names (`InstanceVersion`), never the technical `Version`.

| Object | Principal fields | Required constraints / indexes |
|---|---|---|
| CollectionInstance | InstanceKey, CompanyId, BaselineReleaseId, CanonicalId, ParentCanonicalId, Name, FullPath, DisplayOrder, CollectionScopeType, InstanceStatus, ScopeBindings[], InstanceToken?, SourceDefinitionHash, LastChangeAt, VersionToken | Tenant + InstanceKey unique (non-deleted); tenant-first index; acyclic parent tree; no hard delete |
| ScopeBinding (embedded) | OrgBindingScopeType (COMPANY/PLANT/BU), OrgBindingScopeId, ScopeSourceModule, BindingStatus, EffectiveFrom/To, LastValidatedAt | COMPANY binding uses MOD-0220 LegalEntity GUID |
| InstantiationOperation | OperationId, CompanyId, BaselineReleaseId, InstanceToken?, OperationType (DRY_RUN/EXECUTE/RETRY), Status, Created/Skipped/Failed/Total, CorrelationId, RequestedBy, StartedAt, CompletedAt | Tenant + OperationId unique; CorrelationId indexed |
| InstantiationOutcome | OperationId, NodeKey, CanonicalId, Status (CREATED/SKIPPED/FAILED), ReasonCode, Message, Retryable | Tenant + OperationId + NodeKey unique |

**Deterministic instance key (closed):** `InstanceKey = {tenantId}|{companyId}|{baselineReleaseId}|{canonicalId}`
(or a repo-approved equivalent confirmed at implementation). An optional `InstanceToken` distinguishes additional
instances for the same company/baseline (appended to the key when present). Partial instantiation uses the same key
model, so later selected-branch executions can expand the same company/baseline instance tree without duplicates.
`FullPath` is server-derived from the source definition tree; the instance never re-derives hierarchy from client input.

## 5. Repo Scope

### Authorized FU05 implementation scope (after approval)

- `services/Diten.Platform/src/Diten.Platform.API/**` — thin instantiation controller actions + controlled response
  wiring under the existing route family.
- `services/Diten.Platform/src/Diten.Platform.Application/**` — CQRS commands/queries/handlers/validators,
  instantiation/reconciliation services, MOD-0220 seam consumption, permission constants, reason codes.
- `services/Diten.Platform/src/Diten.Platform.Domain/**` — instance/operation/outcome aggregates + repository
  interfaces (only if the live convention places entities here).
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` — repository implementations, Mongo indexes, DI,
  approved alias registration.
- `frontend/Diten.Web/**` — TenantShell Instantiation Wizard (controller proxy, views, JS, RESX) per the FU03/FU04
  conventions.
- `services/Diten.Platform/tests/**` and frontend tests/smoke.

### Separately governed scope

- `gateway/Diten.ApiGateway/**/ocelot.json` — only if a new route is needed beyond the existing catch-all (see §14);
  FU05 expects GET/POST under the existing widened catch-all, so **no gateway change is anticipated**.
- Permission seed/alias ownership for new keys through the MOD-0018/security-owned location when outside FU05 scope.

## 6. Protected Paths

- `.antigravity/**`
- `gateway/**` except a separately approved integration-agent task if a new route is unexpectedly required
- `services/Diten.Platform.Common/**`
- `services/Diten.AuthService/**` (permission seed/alias) except a separately approved MOD-0018/security task
- `services/Diten.MdmService/**` (MOD-0220 is consumed via its contract, never modified)
- `services/Diten.DevEnablementService/**`, `services/Diten.EnterpriseStrategyService/**`
- MOD-0029, MOD-0030, MOD-0031 implementation files
- Binary repository internals, document storage, physical folder creation
- FU01–FU04 owned files except read-only **consumption** of their public contracts
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` and any frozen/legacy layout
- Parent pack and FU01–FU04 packs unless a separate governance reconciliation authorizes an update

## 7. Dependencies

| Dependency | FU05 usage |
|---|---|
| MOD-0028 parent | Supplies CollectionInstance/ScopeBinding/ProvisioningJob ownership + Wave 4 boundary |
| MOD-0028-FU02/FU04 | Supply PUBLISHED `BaselineRelease` + `CollectionDefinition` tree — consumed, not modified |
| MOD-0028-FU03 | Supplies TenantShell UI conventions reused by the wizard |
| MOD-0220 | LegalEntity lookup/eligibility (fail-closed seam); authoritative CompanyId GUID |
| MOD-0018 | Approves new lowercase keys, any uppercase aliases, seed ownership |
| MOD-0021 | Audit/correlation seams across dry-run/execute/retry |
| Platform Common | `TenantScopedEntity`, tenant context, repository filtering, correlation middleware |

## 8. Runtime Constraints

- Persistence: MongoDB, tenant isolation on every `CollectionInstance`/`InstantiationOperation`/`InstantiationOutcome`.
- `TenantScopedEntity` base; no client-controlled `TenantId`, technical `Version`, audit actor, or correlation identity.
- **Only PUBLISHED baselines are instantiable**; DRAFT selection is a controlled `VALIDATION_FAILED`.
- **Dry-run mutates nothing**; execute persists only after preconditions pass; the wizard's Execute button stays
  disabled while the dry-run is blocked.
- **Selection modes:** `FULL_TREE` is the default and includes all active source nodes. `SELECTED_BRANCHES` requires at
  least one selected active `CollectionDefinition.CanonicalId` from the same PUBLISHED `BaselineRelease`; selected
  branches include descendants by default and include required ancestors by default.
- **Parent-complete trees only:** no orphan `CollectionInstance` records. If a deep child branch is selected, required
  ancestor containers are included with their source canonical ids (for example `Finance` → `Finance / Tax` →
  `Finance / Tax / Tax Forms`). If `includeRequiredAncestors = false` would produce an orphan, dry-run returns a
  blocked diagnostic and execute remains disabled.
- Duplicate selected canonical ids are normalized. If a parent and child are both selected, the child subtree is not
  instantiated twice; the included canonical-id set is de-duplicated before planning and execution.
- **Idempotent execute:** rerun creates no duplicates; existing nodes are skipped/reconciled by the deterministic
  `InstanceKey`. Provisioning is non-destructive (never deletes company content or weakens a stricter local posture).
- Every FU05 API envelope includes body-level `correlation_id`; the **same flow correlation id** flows through
  dry-run → execute → retry; controlled failures include a stable `reason_code`; no stack traces.
- Cross-tenant / cross-company detail behavior is 404 non-leakage; unauthorized actions are 403.
- **MOD-0220 fail-closed:** a missing/inactive/non-referenceable LegalEntity rejects instantiation without orphaned
  writes; caller cancellation is preserved. **Controlled local fallback** (manual company id, no MOD-0220 call) is
  permitted **for local smoke only**, gated by a feature flag and clearly labeled; it must never be the production path.
- **Company adoption only:** FU05 creates governed company instance metadata. It must not create physical folders,
  upload/store documents, or implement document lifecycle.
- `POSITION`/`PERSON` scope remain disabled; no background job creates records for them.

Feature flags (reused FU01/FU02 typed-options pattern; FU05 adds only what it needs):

| Key | Default | FU05 relevance |
|---|---|---|
| `mod0028.company_provisioning.enabled` | on | The FU05 surface |
| `mod0028.mod0220_fallback.enabled` | off | Local-smoke manual-company fallback ONLY; off in production |
| `mod0028.instantiation_retry.enabled` | off until the retry endpoint is live | Gates the retry action |

## 9. Layout & Shell Contract

- Primary shell: `shell: tenant`; every FU05 page declares `Layout = "_LayoutTenantShell";` explicitly.
- Primary actor type: `tenant_user`.
- No `_LayoutPlatformAdmin.cshtml`, frozen `_Layout.cshtml`, or legacy layout.
- Nav: a TenantShell **Documentation Structures** entry (or a sub-action under the existing documentation nav),
  permission-gated on `…baselines.instantiate` / `…collection-instances.view`; QMS remains a profile label only.
- Exact view/controller/route folder confirmed against the live TenantShell convention (FU03/FU04 reuse).

## 10. Backend File Convention

FU05 follows the live Diten.Platform CQRS action-based shape:

```text
Features/DocumentManagementInstantiation/
|-- Commands/
|   |-- DryRunInstantiationCommand.cs        (sealed record)
|   |-- ExecuteInstantiationCommand.cs       (sealed record)
|   `-- RetryInstantiationCommand.cs         (sealed record)
|-- Queries/
|   |-- GetInstantiationPrerequisitesQuery.cs
|   |-- GetInstantiationOperationQuery.cs
|   |-- GetCollectionInstancesQuery.cs
|   `-- GetCollectionInstanceByIdQuery.cs
|-- Handlers/{CommandHandlers,QueryHandlers}/  (sealed, no suffix)
|-- Validators/                                (no Command suffix)
|-- Services/
|   |-- IInstantiationPlanner.cs               (builds full-tree or selected-branch dry-run plans from a PUBLISHED baseline)
|   |-- InstantiationService.cs                (idempotent execute/reconcile)
|   `-- CompanyInstanceKeyFactory.cs           (deterministic InstanceKey)
`-- DocumentManagementInstantiationModels.cs   (DTOs/result models in one file)
```

- Commands/queries are sealed records; handlers `{Verb}{Slice}Handler` (no `CommandHandler`/`QueryHandler` suffix);
  validators `{Verb}{Slice}Validator` (no `CommandValidator` suffix).
- Controllers inherit `CustomBaseController`, remain thin, dispatch via MediatR; MOD-0220 access uses the Application
  interface + Infrastructure implementation (no raw `HttpClient` in handlers).
- Repository access uses the live tenant repository convention; tenant-first indexes mandatory.
- Instantiation planning, reconciliation, and key generation are split into focused services, not oversized handlers.

## 11. Frontend File Contract

`golden_reference: compact`; multi-step route-based wizard (not a slim offcanvas). `form_field_count: 0` (no standard
entity create/edit form; the wizard is action/route-based). Reuse the FU03/FU04 TenantShell building blocks: same-origin
MVC proxy → Gateway `5000`, shared toast/confirm, the L10n bridge (`_IndexL10n` JSON → `index.l10n.js` → `window.L10n`),
DataTable v2 for instance/outcome lists, and `backbone-custom.css` for shared styles (no page-embedded styles).

Surfaces (final names confirmed against the live convention):

- **Instantiation wizard** (route-based): step badges, release selection, company/scope inputs, dry-run diagnostics
  table, execute result counters (Created/Skipped/Failed/Total), per-node outcomes table, retry-failed action.
- **Selection section in the Instantiation Wizard:**
  - Apply mode segmented control/radio: **Full structure** (`FULL_TREE`) or **Selected branches** (`SELECTED_BRANCHES`).
  - In Selected branches mode, render the PUBLISHED baseline tree with checkbox branch selection.
  - Show selected branch count, included descendants info, included ancestors info, and dry-run included/excluded
    preview before execute.
  - Execute remains disabled until the current dry-run is valid and non-blocked.
  - Result counters stay Created / Skipped / Failed / Total; per-node outcomes are shown only for included nodes.
- **Collection instances list/detail** (optional, per repo convention): DataTable list + a company instance tree
  viewer reusing the FU03 nested-tree renderer.

Dry-run / execute request contract additions:

| Field | Default | Rule |
|---|---|---|
| `selectionMode` | `FULL_TREE` | `FULL_TREE` or `SELECTED_BRANCHES`; `SELECTED_NODES` deferred |
| `selectedCanonicalIds` | `[]` | Empty allowed only for `FULL_TREE`; required for `SELECTED_BRANCHES` |
| `includeDescendants` | `true` | First-version selected-branch behavior keeps descendants included |
| `includeRequiredAncestors` | `true` | Required for orphan-safe execution unless dry-run returns a blocked diagnostic |

Dry-run result additions:

| Field | Meaning |
|---|---|
| `selectionMode` | Effective mode after defaults/normalization |
| `selectedCanonicalIds` | Normalized selected source canonical ids |
| `includedCanonicalIds` | Final planned canonical ids to create/skip |
| `includedAncestors` | Ancestors included for parent-complete tree integrity |
| `includedDescendants` | Descendants included because a selected branch includes its subtree |
| `excludedCanonicalIdsCount` | Count of active baseline nodes not included by the plan |
| `wouldCreate` / `wouldSkip` | Planned mutation/no-op counts for included nodes only |
| `blockedSelections` | Invalid/orphan-risk selections that block execute |
| `diagnostics` | Warnings/errors/reason codes for the selection plan |

## 12. Validation Rules

| Input / operation | Required | Rule | Failure |
|---|---|---|---|
| Baseline selection | Yes | Must be a PUBLISHED `BaselineRelease`; DRAFT rejected | 400 `VALIDATION_FAILED` |
| Published release exists | Yes | At least one PUBLISHED release for the selected definition | 400 `VALIDATION_FAILED` (precondition) |
| Selection mode | Yes | Defaults to `FULL_TREE`; allowed first-version values are `FULL_TREE` and `SELECTED_BRANCHES` | 400 `VALIDATION_FAILED` |
| Selected canonical ids | Conditional | Empty allowed for `FULL_TREE`; at least 1 required for `SELECTED_BRANCHES`; duplicates normalized | 400 `VALIDATION_FAILED` |
| Selected canonical id membership | Conditional | Every selected id belongs to an active `CollectionDefinition` node in the same selected `BaselineRelease` | 400 `VALIDATION_FAILED` or 404 `NOT_FOUND_NON_LEAKAGE` per repo convention |
| Branch inclusion | Conditional | `SELECTED_BRANCHES` includes selected node descendants when `includeDescendants = true` | blocked dry-run diagnostic if unsupported |
| Required ancestors | Conditional | Required ancestors included when `includeRequiredAncestors = true`; if false and orphan risk exists, dry-run is blocked | blocked diagnostic; execute disabled |
| Parent + child selection | Conditional | Child subtree de-duplicated when an ancestor is also selected | no duplicate outcome |
| Planned tree integrity | Yes | Included set must be acyclic and parent-complete | 400 `VALIDATION_FAILED`; no write |
| Company / LegalEntity id | Yes | Non-empty GUID | 400 `VALIDATION_FAILED` |
| LegalEntity validity | Yes (unless fallback) | MOD-0220 ACTIVE + referenceable; id matches | 404 `NOT_FOUND_NON_LEAKAGE` / fail-closed |
| Optional scope (Plant/BU/Token) | No | Trimmed; valid format | 400 `VALIDATION_FAILED` |
| Dry-run gate | Yes | Execute disabled until a non-blocked dry-run for the current selection | Execute stays disabled |
| Idempotency | Yes | Deterministic `InstanceKey`; rerun skips/reconciles | per-node SKIPPED (or 409 on a conflicting mutation) |
| Same company/baseline/canonicalId | Yes | No duplicate `CollectionInstance` for the same deterministic key; optional token participates in key when present | per-node SKIPPED / 409 conflict |
| TenantId | Never client input | Resolved from tenant context | Request contract rejected / test fails |
| Correlation id | All APIs | Non-empty; shared across the flow; body/header identical | Generated server-side if absent |

## 13. Failure Path to Verify

- **DRAFT baseline selected:** 400 `VALIDATION_FAILED`; no instance created.
- **No published release:** 400 `VALIDATION_FAILED`; wizard explains the precondition.
- **Company/LegalEntity missing:** 400 `VALIDATION_FAILED`.
- **Selected branches with no selected ids:** 400 `VALIDATION_FAILED`; execute disabled.
- **Invalid selected canonical id:** 400 `VALIDATION_FAILED` or 404 `NOT_FOUND_NON_LEAKAGE` per repo convention; no write.
- **Selected id from another baseline / inactive definition node:** controlled validation/non-leakage failure; no write.
- **Parent + child both selected:** dry-run normalizes duplicate coverage; execute emits at most one outcome per included
  canonical id.
- **Deep child selected:** dry-run includes required ancestors; no orphan instance is created.
- **Required ancestors disabled with orphan risk:** blocked dry-run diagnostic; execute disabled.
- **Acyclic / parent-complete violation:** 400 `VALIDATION_FAILED`; no write.
- **Unselected branch:** dry-run reports it in excluded count; execute does not create it.
- **Company/LegalEntity not found / cross-tenant baseline or company:** 404 `NOT_FOUND_NON_LEAKAGE`; no leaked id.
- **MOD-0220 unavailable/inactive/non-referenceable:** fail closed with the approved dependency reason; cancellation
  preserved; (local fallback only when the flag is on, clearly labeled).
- **Duplicate instance:** idempotent SKIPPED (or 409 `CONFLICT` for a conflicting mutating operation); no duplicate row.
- **Partial provisioning failure:** per-node `FAILED` outcome with `reason_code` + `retryable`; the operation reports
  created/skipped/failed/total honestly (no fabricated success).
- **Missing permission:** 403 `PERM_DENIED`; no side effect, no success audit.
- **Retry of a non-retryable node:** 400 `VALIDATION_FAILED`.
- **Retry endpoint unavailable (deferred):** UI handles 404/405 gracefully → "retry not available".
- All controlled failures carry `reason_code` + body/header `correlation_id`; no stack traces / exception text.

## 14. Authorization Convention

- Policy: `[Authorize]` on the tenant-facing controller; `[HasPermission]` per semantic action.
- Actor type: `tenant_user`; runtime canonical format PKS-001 lowercase dotted keys under
  `platform.document-management.{resource}.{action}`.
- Spec keys remain traceable directional aliases only if MOD-0018/security approves; reverse/dynamic aliases prohibited.

Proposed FU05 permission keys:

| Key | Endpoint(s) |
|---|---|
| `platform.document-management.baseline-releases.view` | release selection / prerequisites |
| `platform.document-management.baselines.instantiate` | dry-run + execute (instantiate gate) |
| `platform.document-management.instantiations.dry-run` | dry-run endpoint |
| `platform.document-management.instantiations.execute` | execute endpoint |
| `platform.document-management.collection-instances.view` | instance list/detail/tree |
| `platform.document-management.collection-instances.create` | instance creation (execute side) |
| `platform.document-management.collection-instances.retry` | retry endpoint |

Permission strategy (controlled gate):

- FU05 may add local Platform `[HasPermission]` constants/attributes; if seed/alias ownership requires a protected
  security path, implementation **stops and reports** a separate MOD-0018/security task. A missing seed may leave
  validation `PARTIAL` but the release gate does not close until the keys are `confirmed`.
- Backend and frontend resolve the **same** effective lowercase key; hidden/disabled controls are the UI expression of
  the backend's 403.
- `collection-instances.view` already exists in the FU01 seed catalog/alias; FU05 reuses it and adds the remaining keys.
  Directional uppercase spec-alias proposals (e.g. `collection-instances.view → MOD0028.COLLECTION_INSTANCE.VIEW`,
  `baselines.instantiate → MOD0028.COLLECTION_INSTANCE.INSTANTIATE`) require EA/MOD-0018 approval before runtime use.

## 15. Gateway / API Routing Decision

- The existing `/api/v1/document-management/{everything}` catch-all already supports `GET, POST, PUT, PATCH, DELETE,
  OPTIONS` (after the FU04 widening). **FU05 uses GET/POST only**, so **no gateway change is anticipated** — verify and
  confirm; if a new explicit route is somehow required, it is a separate integration-agent task.
- All FU05 routes stay version-explicit under `api/v1/document-management`; no `v2`, no unversioned route.
- Frontend uses Gateway `5000` or a same-origin proxy; never the Platform API service port directly.

Candidate endpoints (generic family; names may adjust to repo convention):

| Method | Path | Used by |
|---|---|---|
| GET | `api/v1/document-management/instantiations/prerequisites` | wizard preconditions / published releases |
| POST | `api/v1/document-management/instantiations/dry-run` | dry-run preview |
| POST | `api/v1/document-management/instantiations/execute` | execute instantiation |
| GET | `api/v1/document-management/instantiations/{operationId}` | operation status + outcomes |
| POST | `api/v1/document-management/instantiations/{operationId}/retry` | retry failed subset (deferrable) |
| GET | `api/v1/document-management/collection-instances` | company instance list |
| GET | `api/v1/document-management/collection-instances/{id}` | instance detail / tree |

## 16. Acceptance Criteria

- [x] FU05 pack promoted to `status: approved` after explicit user approval for this FU05 scope only.
- [ ] Scope is company adoption / instantiation only; no template edit, baseline edit, QMS import change, document
  lifecycle, retention, evidence export, physical folder, or binary storage.
- [ ] Only PUBLISHED baselines are instantiable; DRAFT selection → controlled `VALIDATION_FAILED`.
- [ ] FU05 supports `FULL_TREE` instantiation as the default behavior.
- [ ] FU05 supports `SELECTED_BRANCHES` partial/multi-branch instantiation from a PUBLISHED baseline.
- [ ] Dry-run accurately previews `selectionMode`, normalized selected ids, included ids, included ancestors,
  included descendants, excluded count, `wouldCreate`, `wouldSkip`, blocked selections, and diagnostics.
- [ ] Execute creates/skips only canonical ids included in the dry-run plan; no unselected branch is created.
- [ ] Required ancestors are handled safely; no orphan `CollectionInstance` nodes can be produced.
- [ ] Repeated partial execution is idempotent; same company + baseline + canonical id does not duplicate.
- [ ] Multiple partial executions can expand the same company/baseline instance tree without duplicating existing nodes.
- [ ] `CollectionInstance`/`InstantiationOperation`/`InstantiationOutcome` use `TenantScopedEntity`; `TenantId` never
  client-controlled; tenant-first indexes; no hard delete.
- [ ] Deterministic `InstanceKey` (`{tenantId}|{companyId}|{baselineReleaseId}|{canonicalId}` + optional token);
  execute is idempotent (rerun creates no duplicates).
- [ ] Dry-run mutates nothing; execute persists only after preconditions pass; Execute disabled while dry-run blocked.
- [ ] Per-node outcomes (created/skipped/failed + reason_code + retryable) and honest counts; no fabricated success.
- [ ] MOD-0220 LegalEntity seam is fail-closed; the local manual-company fallback is flag-gated, off by default, and
  clearly labeled "local smoke only".
- [ ] One flow `correlation_id` shared across dry-run/execute/retry; body/header parity; copyable in the UI.
- [ ] All FU05 routes version-explicit under `api/v1/document-management`; gateway catch-all compatibility confirmed.
- [ ] Permissions are the minimal FU05 subset; backend and frontend resolve the same effective lowercase key; missing
  permission → 403 `PERM_DENIED`.
- [ ] Controlled failures (400/403/404/409) return `reason_code` + `correlation_id`, no stack traces.
- [ ] TenantShell wizard (Select release → Dry-run → Execute → Results) delivered with precondition guidance.
- [ ] No `CollectionInstance`-driven document lifecycle / MOD-0029/0030/0031 side effect.

## 17. Test Expectations

Backend tests:
- dry-run valid PUBLISHED baseline; dry-run blocked for DRAFT; dry-run blocked for missing company.
- dry-run `FULL_TREE` includes the whole active baseline tree by default.
- dry-run `SELECTED_BRANCHES` requires at least one selected canonical id; rejects/blocks invalid ids.
- dry-run selected branch includes descendants and required ancestors; parent + child selection de-duplicates coverage.
- dry-run reports included/excluded counts, blocked selections, ancestors, descendants, wouldCreate, and wouldSkip.
- execute creates `CollectionInstance` nodes only for included canonical ids; execute idempotent (rerun skips existing);
  per-node created/skipped/failed.
- multiple partial executions expand the same company/baseline instance tree without duplicate `InstanceKey` records.
- deep child selected branch produces parent-complete instance nodes; no orphan instance node.
- retry failed subset (if implemented); no duplicate instances; cross-tenant non-leakage (404).
- missing permission 403; correlation id preserved across the flow.
- MOD-0220 success / not-found / inactive / unavailable contract behavior; fail-closed; cancellation preserved.
- no physical folder/document storage; no MOD-0029 lifecycle side effect.

Frontend tests/smoke:
- wizard opens in TenantShell; select release; missing-precondition guidance; dry-run preview renders.
- Apply mode defaults to Full structure; Selected branches renders the published baseline tree with branch checkboxes,
  selected count, included descendants info, and included ancestors info.
- execute stays disabled until a valid dry-run for the current selection; dry-run preview shows included/excluded scope.
- execute result counters + per-node outcomes table; retry-failed (if implemented).
- permission gating; no direct service-port call; no client `TenantId`/`X-Tenant-Id`; controlled
  `reason_code`/`correlation_id` display; no stack traces.

Build/verify: `dotnet build` Platform API + Diten.Web; relevant Platform tests; DataTable verifier (if Python
available); RESX parity for tenant languages; `git diff --check`; protected-path verification.

> **Known environment caveat (carried from FU03/FU04):** a running local fleet can lock service DLLs (use `--no-build`
> tests or an isolated build), and the browser smoke needs a permissioned tenant session + a browser/automation tool.
> Deferred runtime smoke is recorded as validation debt, not silently skipped.

## 18. Ready-for-dev Checklist

- [x] User reviewed this pack and explicitly approved only the FU05 company-adoption / instantiation /
  `CollectionInstance` provisioning scope.
- [ ] **MOD-0220 LegalEntity seam decision documented:** confirm `ILegalEntityReferenceValidator` reuse + the
  flag-gated local-smoke fallback contract. **CONTROLLED GATE**
- [ ] **`CollectionInstance` identity model documented:** `InstanceKey` formula + optional `InstanceToken` accepted.
  **CONTROLLED GATE**
- [ ] **Dry-run / execute / retry contract documented**, including per-node outcome shape and idempotency semantics.
  **CONTROLLED GATE**
- [ ] **Selective Structure Baseline Instantiation contract documented:** `selectionMode`, `selectedCanonicalIds`,
  `includeDescendants`, `includeRequiredAncestors`, dry-run included/excluded preview, and parent-complete tree behavior.
  **CONTROLLED GATE**
- [ ] **Permission keys finalized** with MOD-0018/security (new generic keys + any uppercase aliases; reconcile vs the
  legacy `qms-baselines.*` family without renaming it). **CONTROLLED GATE**
- [ ] **Gateway route compatibility verified** (existing catch-all GET/POST is sufficient; no new route needed).
  **CONTROLLED GATE**
- [ ] **TenantShell wizard L10n key set prepared** for all required tenant languages before UI work. **CONTROLLED GATE**
- [ ] Registry/DCP-002 preflight for `MOD-0028-FU05` (FU/child of MOD-0028) when Python/openpyxl is available.
  **CONTROLLED GATE**
- [ ] `entity_base: TenantScopedEntity` accepted (confirmed by FU01/FU02).
- [ ] FU05 test matrix and protected paths accepted.
- [x] User changed status gate to `approved` before runtime implementation.

## 19. Implementation Notes

- FU05 is the parent pack's Wave 4 (Company adoption). It consumes PUBLISHED baselines and produces company-scoped
  `CollectionInstance` trees; it never edits templates/baselines or implements document lifecycle.
- Full instantiation mirrors the whole source `CollectionDefinition` tree by `CanonicalId`/`ParentCanonicalId`/
  `DisplayOrder`. Partial selected-branch instantiation mirrors only the selected branch coverage plus required
  ancestors. `FullPath` is server-derived; atomic node names are preserved verbatim (names may contain `/`, as in
  FU02/FU04).
- Idempotency is anchored on the deterministic `InstanceKey`; rerun reconciles (skip/no-op) rather than duplicating.
  The same key model applies to partial instantiation, so later branches can be added under the same company/baseline.
- Reuse FU01 contracts directly (`Response<T>` `reason_code`/`correlation_id`, route family, `[HasPermission]`,
  `PermissionAliasMap`, feature flags). Do not fork them.
- MOD-0220 is consumed read-only via its contract; FU05 never modifies `Diten.MdmService`. The local-smoke fallback is
  a clearly-labeled, flag-gated convenience for environments without MOD-0220 — never the production path.
- The route family existing in the gateway is not proof of capability; no endpoint fabricates success or an empty
  result to pass a smoke.
- Audit/correlation seams are emitted for dry-run/execute/retry; the full MOD-0028 audit catalog remains later-wave.

### Approved Implementation Handoff (effective only after the user sets the pack to approved/ready-for-dev)

- Next executable action after approval: the orchestrator may implement **FU05 (backend + TenantShell) only**.
- Allowed: `CollectionInstance`/`InstantiationOperation`/`InstantiationOutcome`, instantiation prerequisites/dry-run/
  execute/retry, full-tree and selected-branch baseline instantiation, collection-instances list/detail, MOD-0220
  fail-closed seam (+ flag-gated local fallback), the TenantShell Instantiation Wizard, permission-gated controls,
  localization, frontend/backend tests.
- Not allowed: editing `CollectionDefinition`/`BaselineRelease`, FU04 manual-builder changes, FU02 import changes,
  document lifecycle, MOD-0029/0030/0031, physical folder creation, document/binary storage, template/exception/
  workflow/retention/evidence work, gateway `ocelot.json` changes (unless a new route is unexpectedly required and a
  separate integration-agent task is opened), or AuthService seed/alias edits via a protected path.

## 20. Follow-up Items

1. **Reconciliation jobs follow-up:** scheduled reconciliation of company instances against later baseline releases
   (drift detection / re-instantiation), and `ProvisioningJob` lineage if the synchronous flow needs a job model.
2. **Local governance follow-up:** local collection nodes + governance exceptions on company instances.
3. **Template governance follow-up:** template masters/versions/variants bound to instantiated companies.
4. **MOD-0029 boundary:** controlled document lifecycle over the instantiated structure remains MOD-0029-owned, never
   FU05; MOD-0030 (retention) and MOD-0031 (evidence) remain their owners.
5. **Legacy naming reconciliation:** a separate MOD-0018/security task may map/retire the legacy `qms-baselines.*`
   family toward the generic `structure-baselines`/`instantiations` concept; FU05 does not rename it.
6. **Release inspection follow-up:** full audit catalog, NL-01 matrix, accessibility, observability, security, and
   release gates for the company-adoption surfaces.
7. **Selected nodes follow-up:** `SELECTED_NODES` mode for individual node inclusion without descendants, if a later
   UX and tree-integrity design explicitly approves it.
8. **Company-local override follow-up:** local company-specific node overrides or branch removal/reconciliation after
   later baseline releases.

Each follow-up requires its own approved or ready-for-dev scope. FU05 does not authorize any later wave, and does not
authorize document lifecycle, retention, or evidence export.

## 21. DCP-007 Approved Amendment — Import Completion Visibility and Consumer Guardrails

**Amendment status:** `approved`
**Approved by user:** `2026-08-27`
**Runtime authority:** `false`

This amendment is a bounded DCP-007 governance contract approved by the user on 2026-08-27. The parent FU05 pack
remains `approved`; amendment approval does not authorize implementation.

### Amendment ownership

FU05 owns the completion-guard integration for Company prerequisite/list selection, Company planning/dry-run,
execute/retry, Company operation/instance side effects, and Company-scoped reconciliation/readiness. It consumes
FU02's combined completion/evidence guard and FU07's completion evidence; it does not own Corporate behavior, the FU07
operation/manifest aggregates, or the generic reconciliation engine as a business SoR.

### Mandatory execution order

```text
tenant + baseline lookup
→ combined completion/evidence guard
→ CollectionScopeType == Company
→ ScopeOwnerId == CompanyId
→ scope-filtered definition/instance/provider read
→ planning/reconciliation
→ side effects
```

### Required behavior

- A failed guard creates no prerequisite candidate, plan, instantiation operation, outcome, or `CollectionInstance`.
- Incomplete baselines never enter Company prerequisite or selection results.
- Company reconciliation/readiness reads only the requested Company owner scope.
- Every generic reconciliation-engine call supplies explicit `CollectionScopeType.Company + ScopeOwnerId` and validates
  `ScopeOwnerId == CompanyId` before any scoped read.
- Corporate instances never enter Company provider, readiness, count, finding, or reconciliation results.
- Scope-less, owner-mismatched, cross-tenant, or incomplete calls fail closed before planning/reconciliation.
- Reconciliation side effects begin only after completion and scope-owner validation; a retry re-evaluates both.
- Existing FU09 annotations are AS-IS drift evidence only, not authority or a business SoR.
- Company sharing, overlays, local additions, group-node propagation/removal, and template propagation remain outside
  this amendment and DCP-007.

### Amendment acceptance criteria

- [ ] Prerequisite/list queries omit incomplete baselines without generating a plan or candidate artifact.
- [ ] Dry-run, execute, and retry enforce the mandatory order and re-evaluate the guard at invocation time.
- [ ] Failed completion/scope validation produces no operation, outcome, instance, or reconciliation finding write.
- [ ] Company reconciliation requires explicit Company scope and excludes every Corporate instance sharing the same
      baseline.
- [ ] Provider/readiness queries are owner-scoped and cannot aggregate all same-baseline tenant instances.
- [ ] Generic engine use remains a technical call under FU05 ownership; it becomes no independent canonical owner.
- [ ] Existing FU05 permission, tenant, idempotency, and concurrency behavior remains intact outside the amendment.

### Amendment test expectations

- Prerequisite/list tests cover Completed, incomplete, integrity-mismatched, legacy-null, and cross-tenant baselines.
- Dry-run/execute/retry tests instrument planner, operation, outcome, and instance repositories to prove zero calls or
  writes after guard failure.
- Scope tests cover missing scope, non-Company scope, mismatched CompanyId/ScopeOwnerId, and cross-tenant owner IDs.
- Mixed Company/Corporate fixtures sharing a baseline prove Company-only provider/readiness/reconciliation results.
- Concurrency tests prove a guard/scope change between selection and execute/retry is re-evaluated and fails closed.
- Failure tests cover guard unavailable, integrity mismatch, stale operation, and owner mismatch without orphan state.

### Amendment governance gates

- DCP-007 remains `under-review`; FU07 remains `draft` with `runtime_code_allowed: false`.
- This amendment is approved at governance level; runtime implementation remains prohibited until DCP-007 and the
  active member-pack execution gates close.
- DCP-007 G2 is resolved because FU02, FU03, FU05, and FU06 amendments received separate user approval on 2026-08-27.
- This amendment does not close G12, load/lease/heartbeat, retention/audit, FU07 approval, or runtime-evidence gates.
- It creates no permission seed, MOD/FU identity, Gateway change, Company sharing/overlay, or template-propagation scope.

### Approval note

- Approval covers only this amendment's scope, acceptance criteria, and test governance contract.
- Code may start only after DCP-007 and the active member pack pass their separate execution gates.
- This approval is not runtime implementation, deployment, or activation authority.
