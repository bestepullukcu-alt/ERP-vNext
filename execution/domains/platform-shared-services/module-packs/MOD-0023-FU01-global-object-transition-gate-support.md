---
id: MOD-0023-FU01
name: Global Object Transition Gate Support
parent: MOD-0023
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: TenantScopedEntity
status: ready-for-dev
owner: platform-shared-services
branch: feature/pss/mod-0023-fu01-global-object-transition-gate-support
started: 2026-08-06
target: TBD
form_field_count: 0
---

# MOD-0023-FU01 — Global Object Transition Gate Support

> **Ready for development planning.** This follow-up pack does not authorize runtime implementation by itself. It records the approved
> cross-context transition-gate contract needed after B09 identified that Module Catalog activation is a
> platform/global operation while the blocking Workflow instance is tenant-scoped.

> **DCP-002 gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0023-FU01 --name "Global Object Transition Gate Support" --parent MOD-0023`
> returned `OK  MOD-0023-FU01: proven against Blueprint/registry.` on 2026-08-06.

## 1. Module Summary

MOD-0023-FU01 is a focused follow-up to `MOD-0023 Workflow Designer (Approvals/SLAs/Escalations)`.
It defines explicit target tenant/scope support for the MOD-0023 transition gate so platform/global objects
can be safely gated by tenant-scoped workflow instances without weakening tenant isolation.

The immediate reference consumer is Module Catalog activation. Current B09 evidence records direct gate
evaluation as `PASS-with-gap`: the gate can return `Blocked / PendingApproval` for a tenant-owned Workflow
instance, but Module Catalog activation itself runs as a platform/global operation and has no approved way to
declare the tenant scope that owns the relevant Workflow instance.

This pack records explicit `TargetScope` + `TargetTenantId` on the gate request as the safest option.
Platform/global workflow instances are rejected for this follow-up, and silent `NotApplicable` for ambiguous
global-object gating is not allowed.

The target tenant is not a client-trust shortcut. For platform/global mutations, `TargetTenantId` must come from
an approved consumer-owned source of authority for the object being mutated, not from an arbitrary browser field.
For Module Catalog activation, the approved ready-for-dev source is stored workflow-binding metadata / a governed ownership
record that can be audited and validated before the gate lookup runs. Free request input is explicitly rejected as
the source of authority.

Ready-for-dev decision reconciliation (2026-08-06):

- `TargetScope` values are approved for ready-for-dev as `CurrentTenant` and `Tenant`.
- `TargetTenantId` authority rules are approved for ready-for-dev as documented: target tenant must come from an
  approved consumer-owned source of authority and is not sufficient authorization by itself.
- Module Catalog target-tenant source is approved for ready-for-dev as stored workflow-binding metadata /
  governed ownership record, not free request input.
- `RequiresWorkflowGate` is approved for ready-for-dev as a generic field on `WorkflowGateRequest`;
  Module Catalog sets it `true` when activation requires approval.
- Unavailable evaluation maps to `503 Service Unavailable`; workflow-blocked mutation remains `409 Conflict`.
- Permission strategy reuses `platform.workflow.transitions.evaluate` and `platform.module-catalog.update`;
  no AuthService seed/grant change is approved for this FU.
- Audit/correlation metadata is transient for this FU; `TargetTenantSource` is derived from the approved
  binding/governed ownership record.
- Live fixture setup, ownership-check, cleanup, and retained-history strategy is approved for ready-for-dev as
  documented in §§16-18.
- Module Catalog-owned workflow-binding metadata shape is approved for ready-for-dev with `ObjectType`,
  `ObjectId`, `ObjectRef`, `TargetTenantId`, `TargetTenantSource`, `RequiresWorkflowGate`,
  `WorkflowDefinitionKey` or `WorkflowTemplateId`, actor/timestamp metadata, and `CorrelationId`.
- Module Catalog-owned workflow-binding metadata storage is resolved for local implementation as optional
  `ModuleCatalogItem.WorkflowBinding` metadata on the existing global catalog item.
- Additive JSON compatibility defaults are approved for ready-for-dev: missing `TargetScope` defaults to
  `CurrentTenant`, missing `TargetTenantId` is valid only for `CurrentTenant`, and missing
  `RequiresWorkflowGate` preserves optional-gate behavior.
- Reason-code constants and HTTP mappings are approved for ready-for-dev as recorded in §§13-15.
- Workflow fixture cleanup path is approved for ready-for-dev as Workflow task cancel API first, controlled
  close API as fallback, then Module Catalog lifecycle cleanup.
- Module Catalog cleanup rule is approved for ready-for-dev: deactivate first when active, then soft-delete;
  direct soft-delete is allowed only for `Draft` or `Inactive`; hard delete and bulk delete remain prohibited.
- The pack is promoted to `status: ready-for-dev` by explicit user approval on 2026-08-06. Runtime
  implementation was explicitly authorized locally by the user on 2026-08-06.

## 2. Ownership and Boundaries

### In scope

- Additive transition-gate contract planning for explicit target scope.
- Fail-closed behavior when a platform/global caller requires a tenant-scoped gate but omits target tenant scope.
- HTTP/API error mapping for workflow-blocked, unavailable, and invalid-scope outcomes.
- In-process gate behavior for source modules that call `IWorkflowTransitionGate`.
- Module Catalog activation as the reference consumer proof.
- Focused tests around tenant scope resolution, cross-tenant rejection, and blocked/unavailable behavior.
- Additive request compatibility for existing JSON clients that omit the new fields.
- Audit/correlation metadata expectations for the effective target tenant used by cross-context gate evaluation.

### Out of scope

- Replacing MOD-0023 Workflow runtime model.
- Introducing platform/global Workflow instances.
- Changing Module Catalog ownership from `GlobalEntity`.
- Adding frontend UI, menu entries, Gateway routes, appsettings, seeds, migrations, fixture data, or raw Mongo cleanup.
- Reworking tenant middleware globally.
- Modifying AuthService permission seed/grant ownership.
- Treating this ready-for-dev pack as runtime implementation approval.

## 3. Owned Objects

This follow-up owns contract-level changes only after runtime implementation is explicitly authorized:

| Object / contract | Ownership decision |
|---|---|
| `WorkflowGateRequest` | Additive target scope fields for in-process callers. |
| `EvaluateWorkflowTransitionGateRequest` | Additive target scope fields for API callers. |
| `EvaluateWorkflowTransitionGateQuery` / handler | Resolve effective workflow tenant scope before repository lookup. |
| `IWorkflowTransitionGate` implementation | Preserve fail-closed semantics and map controlled gate failures. |
| Workflow gate reason/error codes | Stable codes for invalid target scope, workflow blocked, and evaluation unavailable. |
| Module Catalog activation reference consumer | Prove a global object transition can be blocked by a tenant-scoped Workflow instance when explicit tenant scope is supplied. |
| Module Catalog-owned workflow-binding metadata | Governed object-owned source for target tenant and required-gate authority. |
| Audit/correlation metadata | Record effective target tenant, source object reference, and correlation id for cross-context gate decisions. |

No new standalone persistent entity is approved by this pack. Module Catalog-owned workflow-binding metadata is
implemented as optional governed object-owned metadata on `ModuleCatalogItem.WorkflowBinding`.

## 4. Entity Fields

No new MongoDB entity is created by this follow-up.

Approved ready-for-dev additive request fields:

| Field | Type | Required | Applies to | Notes |
|---|---|---:|---|---|
| `TargetScope` | enum/string | Yes after contract update | `WorkflowGateRequest`, `EvaluateWorkflowTransitionGateRequest` | Approved ready-for-dev values: `CurrentTenant`, `Tenant`. `CurrentTenant` preserves existing tenant-call behavior. |
| `TargetTenantId` | `Guid?` | Conditional | `WorkflowGateRequest`, `EvaluateWorkflowTransitionGateRequest` | Required when `TargetScope = Tenant`; omitted for pure current-tenant calls. |
| `RequiresWorkflowGate` | `bool?` | Conditional | `WorkflowGateRequest`; API exposure remains additive if needed | Approved as a generic gate field. If true and no scoped Workflow instance is found, response must fail closed instead of returning `NotApplicable`. Module Catalog sets it true when activation requires approval. |
| `TargetTenantSource` | enum/string | Required in transient audit metadata for scoped platform/global evaluation | internal metadata, not necessarily public API | Approved as transient audit metadata for this FU, derived from stored workflow-binding metadata / governed ownership record. |

Existing object fields remain unchanged: `ObjectType`, `ObjectId`, `ObjectRef`, `RequestedTransition`,
`RequestedTargetState`, `ActorId`, `ReasonCode`, and `CorrelationId`.

Approved Module Catalog-owned workflow-binding metadata shape for ready-for-dev:

| Field | Type | Required | Notes |
|---|---|---:|---|
| `ObjectType` | string | Yes | Must be `ModuleCatalogItem` for the reference consumer proof. |
| `ObjectId` | string/Guid | Yes | Module Catalog item id being activated. |
| `ObjectRef` | string | Yes | Stable catalog object ref/code used by Workflow binding. |
| `TargetTenantId` | Guid | Yes | Tenant that owns the approval Workflow instance. |
| `TargetTenantSource` | enum/string | Yes | Approved value: `WorkflowBindingMetadata` or equivalent governed ownership source. |
| `RequiresWorkflowGate` | bool | Yes | Module Catalog sets true when activation requires approval. |
| `WorkflowDefinitionKey` or `WorkflowTemplateId` | string/Guid | Conditional | Required when binding is definition/template-specific. |
| `CreatedBy`, `CreatedAtUtc`, `UpdatedBy`, `UpdatedAtUtc` | audit metadata | Yes | Governed ownership record lifecycle metadata. |
| `CorrelationId` | string | Yes | Required for traceable activation/gate evaluation. |

Backward compatibility rule: existing JSON payloads that omit all new fields must deserialize and behave as
`TargetScope = CurrentTenant`, `TargetTenantId = null`, and optional-gate semantics unless the consumer explicitly
marks the gate as required.

## 5. Repo Scope

Authorized local implementation scope after explicit runtime implementation approval:

- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/IWorkflowTransitionGate.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Queries/EvaluateWorkflowTransitionGateQuery.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/EvaluateWorkflowTransitionGateHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Validators/EvaluateWorkflowTransitionGateValidator.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/WorkflowTransitionGate.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/ModuleCatalogItem.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/ModuleCatalogContracts.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/ActivateModuleCatalogItemCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/CreateModuleCatalogItemCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/UpdateModuleCatalogItemCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Validators/CreateModuleCatalogItemCommandValidator.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Validators/ModuleCatalogItemRequestValidator.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Validators/UpdateModuleCatalogItemCommandValidator.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Common/**` or existing exception mapping surface only if
  needed to map `WorkflowTransitionBlockedException` consistently.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/**`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleCatalog/**`

No frontend, Gateway, appsettings, seed, migration, fixture-data, AuthService seed/grant, raw Mongo, or unrelated
files are authorized by this local implementation.

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/**`
- `frontend/Diten.Web/**`
- `services/Diten.AuthService/**`
- `services/Diten.MdmService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.Platform.Common/**` unless a later approved design explicitly requires shared tenant helper changes.
- Appsettings, seed data, migrations, fixture data, and raw Mongo data.

## 7. Dependencies

| Dependency | Use | Status |
|---|---|---|
| MOD-0023 parent Workflow runtime | Existing transition gate, Workflow instance/task repositories, status model. | Required. |
| `TenantScope` helper | Candidate mechanism to temporarily resolve a tenant-scoped repository lookup from a platform/global caller. | Existing. |
| Tenant context middleware | Platform routes currently resolve as platform context with `Guid.Empty`; this is the source of the gap. | Existing; not modified by default. |
| Module Catalog | Reference consumer for platform/global object activation. | Existing global object model. |
| MOD-0018 Auth/RBAC | Permission enforcement for workflow evaluate and module-catalog update actions. | Consumed; existing keys reused, no AuthService seed/grant edit in this FU. |
| MOD-0021 Audit | Correlation and target tenant audit metadata must remain consistent. | Consumed; transient metadata only for this FU. |
| Existing exception handling / API result mapping | Needed to turn `WorkflowTransitionBlockedException` into a controlled mutation response. | Must reuse existing surface; no new global error subsystem. |

## 8. Runtime Constraints

- Existing Workflow persistence remains tenant-scoped; no platform/global Workflow instance store is introduced.
- Tenant isolation must fail closed: a platform/global caller cannot accidentally bypass a tenant-owned active Workflow by omitting scope.
- Existing tenant callers must retain backward-compatible behavior when using current tenant context.
- `TargetTenantId` must never authorize cross-tenant access by itself; actor policy and permission checks remain required.
- `Guid.Empty` is not a valid tenant target for tenant-scoped Workflow gate evaluation.
- Gate evaluation must remain read-only against Workflow and source business objects.
- `TenantScope` use must be constrained to the smallest repository lookup block and must restore the previous
  context in all success/failure paths.
- The standalone evaluate endpoint may return a successful evaluation payload for `Blocked`; a source-object
  mutation blocked by `WorkflowTransitionBlockedException` must map to a controlled non-success mutation response.
- The new contract must be additive: older clients that omit target-scope fields must continue to work for
  current-tenant optional gate evaluation.
- Audit/correlation metadata for a scoped platform/global evaluation must include the effective target tenant
  when one is used; for FU01 this metadata is transient and derives `TargetTenantSource` from the approved
  workflow-binding/governed ownership record.

## 9. Layout & Shell Contract

`shell: none`.

This is backend/API contract work only. No Razor views, menu entries, JavaScript, DataTables, localization files,
or tenant/platform shell surfaces are part of this follow-up.

## 10. Backend File Convention

Use the existing MOD-0023 live conventions:

- Keep Workflow contracts in `Features/Workflow/WorkflowModels.cs` and `Application/Contracts/IWorkflowTransitionGate.cs`.
- Keep the query as a sealed record under `Features/Workflow/Queries/`.
- Keep handler naming under `Handlers/QueryHandlers/`.
- Keep validators under `Features/Workflow/Validators/`.
- Keep controllers thin and MediatR-dispatched.
- Keep all API responses inside `Response<T>` / `CustomBaseController`.
- Do not create a new tenant context abstraction, base repository, entity base, or workflow engine subsystem.

## 11. Frontend File Contract

No frontend files are authorized.

If a future UI needs to expose this decision, it must be separately approved and must use the existing Gateway /
same-origin MVC proxy posture. This FU does not add any page, button, route, menu entry, localization file, or
DataTable contract.

## 12. Validation Rules

Ready-for-dev validation rules after separate runtime implementation approval:

- `TargetScope` defaults to `CurrentTenant` for omitted JSON fields.
- `TargetScope` must be one of the approved ready-for-dev values: `CurrentTenant` or `Tenant`.
- `TargetTenantId` is required when `TargetScope = Tenant`.
- Missing `TargetTenantId` remains valid only when the effective `TargetScope` is `CurrentTenant`.
- `TargetTenantId` must not be `Guid.Empty`.
- Tenant users cannot supply a `TargetTenantId` different from their resolved tenant.
- Platform/partner actors may supply `TargetTenantId` only when the endpoint/action has the workflow evaluate
  permission and the consuming command is approved to perform tenant-scoped gate checks.
- `TargetTenantSource` must be present in audit/internal metadata for platform/global mutations that use
  `TargetScope = Tenant`.
- If `RequiresWorkflowGate = true`, no matching Workflow instance is a controlled blocked/unavailable outcome,
  not `NotApplicable`.
- Module Catalog sets `RequiresWorkflowGate = true` when activation requires approval.
- If `RequiresWorkflowGate` is omitted, existing optional-gate behavior is preserved for current-tenant callers.
- Existing field validation for object type/id/ref, requested transition/target state, actor id, and reason code remains.

Approved reason-code constants for ready-for-dev:

| Code | HTTP mapping | Use |
|---|---:|---|
| `WORKFLOW_PENDING_APPROVAL` | 409 | Active/pending Workflow blocks a source-object mutation. |
| `WORKFLOW_EVALUATION_UNAVAILABLE` | 503 | Workflow gate evaluation cannot be completed; fail closed. |
| `WORKFLOW_REQUIRED_GATE_NOT_FOUND` | 409 | Required gate has no matching Workflow instance. |
| `WORKFLOW_INVALID_TARGET_SCOPE` | 400 | Target scope value or combination is invalid. |
| `WORKFLOW_TARGET_TENANT_REQUIRED` | 400 | `TargetScope = Tenant` without a non-empty target tenant. |
| `WORKFLOW_TARGET_TENANT_UNAUTHORIZED` | 403 | Actor lacks authority for target tenant evaluation. |
| `WORKFLOW_TARGET_TENANT_MISMATCH` | 403 | Tenant actor attempts another tenant's target scope. |

## 13. Failure Path to Verify

| Failure path | Expected behavior |
|---|---|
| Platform/global caller omits `TargetTenantId` for a required tenant-scoped gate | Controlled non-success, fail closed; source object state unchanged. |
| Platform/global caller supplies `Guid.Empty` as `TargetTenantId` | 400 `WORKFLOW_INVALID_TARGET_SCOPE` or `WORKFLOW_TARGET_TENANT_REQUIRED`. |
| Tenant caller supplies another tenant's `TargetTenantId` | 403 `WORKFLOW_TARGET_TENANT_MISMATCH`; no workflow metadata leak. |
| Matching tenant Workflow instance is active / waiting approval | Gate returns blocked / pending approval; Module Catalog activation does not commit. |
| Matching tenant Workflow instance is approved/completed | Gate allows the transition; Module Catalog activation may commit if its own rules pass. |
| Gate repository/service unavailable | 503 `WORKFLOW_EVALUATION_UNAVAILABLE`; source transition blocked. |
| No Workflow found while gate is optional | Existing `NotApplicable` behavior remains allowed. |
| No Workflow found while gate is required | 409 `WORKFLOW_REQUIRED_GATE_NOT_FOUND`; no silent bypass. |
| `TenantScope` throws or downstream lookup fails | Previous tenant context is restored; response fails closed. |
| Existing client omits new JSON fields | Current-tenant optional-gate behavior is preserved. |

## 14. Authorization Convention

Existing permission keys remain the base:

- `platform.workflow.transitions.evaluate`
- `platform.module-catalog.update`

Permission strategy is approved for ready-for-dev as reuse of these existing keys. No new MOD-0018-owned key,
AuthService seed, or AuthService grant change is approved for this FU.

Implementation authority note after commit `5858ef4b`: Module Catalog activation uses `ModuleCatalogItem.WorkflowBinding`
as the governed source for target tenant scope. The direct `POST /api/v1/workflow/transitions/evaluate` endpoint remains
a permission-based read-only evaluation surface; platform callers can submit `TargetScope = Tenant` and
`TargetTenantId` only with `platform.workflow.transitions.evaluate`. This is a residual authority-control gap to revisit
if direct evaluation is later treated as source-of-authority proof rather than diagnostic/read-only evaluation.

Actor rules:

- Tenant actors may evaluate gates only for their resolved tenant.
- Platform/partner actors may evaluate a tenant-scoped gate only through explicit `TargetScope = Tenant` and
  non-empty `TargetTenantId` from an approved source of authority.
- Missing or unauthorized actor context must fail closed.
- `TargetTenantId` in a request is an input to validation and scoping, not authorization by itself.
- Audit metadata for cross-context evaluation must record actor, effective target tenant, source object,
  target-tenant source, and correlation id. For FU01, this is transient audit metadata and `TargetTenantSource`
  is derived from the approved workflow-binding/governed ownership record.

## 15. Gateway / API Routing Decision

No new Gateway route is expected.

The existing route family remains:

- Platform direct API: `POST /api/v1/workflow/transitions/evaluate`
- Gateway exposure: existing `/api/v1/workflow/**` routing remains integration-agent owned.
- Module Catalog mutation route remains separate: `POST /api/platform/module-catalog/{id}/activate`.

If a future contract adds public fields to the API request, Gateway should require no route change. If a new route
is proposed later, it must be handled as a separate integration-agent task.

Route behavior distinction:

- `POST /api/v1/workflow/transitions/evaluate` is a read-only evaluation endpoint. A `Blocked` decision may be
  returned as a successful `Response<EvaluateWorkflowTransitionGateResponse>` because the endpoint successfully
  evaluated the gate.
- `POST /api/platform/module-catalog/{id}/activate` is a mutation endpoint. If the gate blocks, throws
  `WorkflowTransitionBlockedException`, or is unavailable, the mutation must return a controlled non-success
  response and leave the source object unchanged.

Implemented ready-for-dev HTTP mapping:

| Scenario | Evaluate endpoint | Mutation endpoint |
|---|---|---|
| Workflow active / pending approval | 200 with `Decision=Blocked`, `GateStatus=PendingApproval` | 409 `WORKFLOW_PENDING_APPROVAL` |
| Invalid target scope / missing required target tenant | 400 `WORKFLOW_INVALID_TARGET_SCOPE` or `WORKFLOW_TARGET_TENANT_REQUIRED` | 400 `WORKFLOW_INVALID_TARGET_SCOPE` or `WORKFLOW_TARGET_TENANT_REQUIRED`; no mutation |
| Unauthorized target tenant / cross-tenant tenant actor | 403 `WORKFLOW_TARGET_TENANT_UNAUTHORIZED` or `WORKFLOW_TARGET_TENANT_MISMATCH` | 403 `WORKFLOW_TARGET_TENANT_UNAUTHORIZED` or `WORKFLOW_TARGET_TENANT_MISMATCH`; no mutation |
| Workflow evaluation unavailable | 503 `WORKFLOW_EVALUATION_UNAVAILABLE`; fail closed | 503 `WORKFLOW_EVALUATION_UNAVAILABLE`; no mutation |
| Optional no-workflow | 200 `NotApplicable` | Mutation may proceed if consumer rules allow |
| Required no-workflow | 409 `WORKFLOW_REQUIRED_GATE_NOT_FOUND` | 409 `WORKFLOW_REQUIRED_GATE_NOT_FOUND`; no mutation |
| `WorkflowTransitionBlockedException` from in-process gate | N/A unless exposed by a caller | 409 `WORKFLOW_PENDING_APPROVAL` or equivalent blocked-transition response |

## 16. Acceptance Criteria

- `WorkflowGateRequest` and `EvaluateWorkflowTransitionGateRequest` support explicit target scope without breaking existing tenant callers.
- Existing tenant-scoped gate tests continue to pass.
- Platform/global caller with `TargetScope = Tenant` and valid `TargetTenantId` can see the matching tenant-owned Workflow instance.
- Platform/global caller without required tenant scope fails closed and does not receive `NotApplicable`.
- Cross-tenant tenant-user request is denied without leaking Workflow instance/task identifiers.
- Module Catalog activation is blocked when the target tenant has an active Workflow instance bound to the catalog object.
- Module Catalog activation leaves the catalog item in `Draft` when the gate blocks or is unavailable.
- Module Catalog activation can proceed when the relevant tenant Workflow is approved/completed and Module Catalog status rules pass.
- HTTP/API mapping is controlled and documented for invalid scope, blocked workflow, unavailable workflow evaluation, and optional no-workflow cases.
- `WorkflowTransitionBlockedException` raised by an in-process consumer maps to HTTP 409 for mutation endpoints.
- The standalone evaluate endpoint preserves successful blocked-evaluation semantics and does not pretend a
  blocked decision is an endpoint failure.
- JSON backward compatibility is proven for older evaluate/gate payloads that omit new fields.
- Audit/correlation records include the effective target tenant and target-tenant source for scoped
  platform/global evaluations; for FU01 this is transient metadata derived from the approved binding/governed
  ownership record.
- Live proof uses an approved API-created fixture contract only: one non-baseline `Draft` or `Inactive`
  `ModuleCatalogItem`, one tenant-scoped active Workflow instance bound to that item, and one active approval
  task for that instance.
- Live proof verifies ownership before mutation: the catalog item id/code, target tenant, Workflow instance,
  and active approval task must match the intended fixture before any activation/deactivation/cancel/archive
  style action is attempted.
- Live fixture cleanup uses only approved APIs: cancel/close the Workflow task through the Workflow task API,
  using `POST /api/v1/workflow/tasks/{taskId}/cancel` first; if cancel is unavailable, use the controlled
  `POST /api/v1/workflow/tasks/{taskId}/close` fallback with reason `B09_FU01_FIXTURE_CLEANUP`. Cleanup then
  deactivates the Module Catalog item if it became active and soft-deletes it through the Module Catalog API.
  Raw Mongo edits, bulk delete, fixture-data files, seed files, and hard delete are prohibited.
- Module Catalog cleanup follows lifecycle state: `Draft` or `Inactive` items may be soft-deleted directly;
  `Active` items must be deactivated first, then soft-deleted.
- Workflow history, transition logs, audit records, and soft-deleted/deactivated lifecycle traces are expected
  to remain after cleanup as regulated execution evidence; cleanup must not attempt to erase history.
- No frontend, Gateway, appsettings, seeds, migrations, fixture-data, or raw Mongo data changes are included in this FU unless separately approved.

## 17. Test Expectations

Focused tests required after implementation approval:

- Unit tests for request validation around `TargetScope` and `TargetTenantId`.
- Unit tests for `WorkflowTransitionGate` mapping of blocked, unavailable, and invalid-scope outcomes.
- Handler tests proving explicit tenant scope uses tenant-scoped repositories and restores previous tenant context.
- Cross-tenant negative tests for tenant users.
- Module Catalog activation tests using the real gate path that prove active Workflow blocks activation and approved Workflow allows activation.
- Mongo-backed repository/gate test reusing the B09 pattern to prove no `DateTimeOffset` sort regression.
- API/controller test or integration test proving HTTP status mapping and `Response<T>` reason/correlation behavior.
- JSON serialization/deserialization tests proving old payloads without `TargetScope`, `TargetTenantId`, or
  `RequiresWorkflowGate` remain valid and use current-tenant optional-gate semantics.
- Tests proving `TenantScope` restores the prior tenant context after successful lookup, blocked lookup,
  validation failure, and repository exception.
- Tests proving audit/correlation metadata carries effective target tenant for platform/global scoped evaluation.
- Live smoke plan must create fixture records only through approved APIs:
  - one non-baseline `ModuleCatalogItem` in `Draft` or `Inactive` status;
  - one tenant-scoped active Workflow instance whose `ObjectType`, `ObjectId`, and `ObjectRef` bind to that
    Module Catalog item;
  - one active approval task for that Workflow instance.
- Live smoke must record fixture ids and perform ownership checks before mutation. If ownership cannot be proven,
  the smoke stops without cleanup mutation and reports the blocker.
- Cleanup smoke must use approved APIs only: `POST /api/v1/workflow/tasks/{taskId}/cancel` first, controlled
  `POST /api/v1/workflow/tasks/{taskId}/close` fallback if cancel is unavailable, Module Catalog deactivate if
  the item became active, then Module Catalog soft-delete. `Draft` or `Inactive` Module Catalog items may be
  soft-deleted directly. No raw Mongo, no bulk delete, no fixture-data files, no seed-file changes, and no hard
  delete.
- Cleanup verification must read back current object state where APIs allow it and record that Workflow history
  remains as expected instead of being erased.

Coverage status after local implementation commit `5858ef4b`:

- Present: handler/unit coverage for request validation, explicit tenant scope lookup, tenant cross-scope denial,
  required-gate fail-closed behavior, legacy JSON/default compatibility, Module Catalog `WorkflowBinding`
  persistence, Module Catalog activation binding handoff, controlled blocked activation response, and the existing
  Mongo-backed transition-gate sort regression.
- Follow-up: API/controller mapping coverage for final HTTP envelopes, repository-exception `TenantScope`
  restoration coverage, and audit/correlation target-tenant evidence tests remain open unless implemented in a
  later local runtime/test pass.

## 18. Ready-for-dev Checklist

- [x] User approves this pack for ready-for-dev (2026-08-06).
- [x] DCP-002 follow-up identity evidence is recorded for ready-for-dev.
- [x] Final enum names for `TargetScope` are approved for ready-for-dev: `CurrentTenant`, `Tenant`.
- [x] Decision on `RequiresWorkflowGate` field is approved for ready-for-dev: generic field in `WorkflowGateRequest`; Module Catalog sets it true when activation requires approval.
- [x] HTTP mapping table is approved for ready-for-dev: unavailable evaluation maps to 503 Service Unavailable; workflow-blocked mutation remains 409 Conflict.
- [x] Module Catalog activation target-tenant source is approved for ready-for-dev: stored workflow-binding metadata / governed ownership record, not free request input.
- [x] Module Catalog-owned workflow-binding metadata shape is approved for ready-for-dev.
- [x] Module Catalog-owned workflow-binding metadata storage is resolved for local implementation as optional `ModuleCatalogItem.WorkflowBinding`.
- [x] Target tenant authority/source rules are approved for ready-for-dev; arbitrary client-supplied tenant ids are not accepted as sufficient authority.
- [x] Evaluate endpoint vs mutation endpoint response semantics are approved for ready-for-dev.
- [x] `WorkflowTransitionBlockedException` 409 mapping is approved for ready-for-dev; workflow-blocked mutation remains `409 Conflict`.
- [x] Additive JSON compatibility behavior is approved for ready-for-dev: omitted fields preserve current-tenant optional-gate behavior.
- [x] Reason-code constants are approved for ready-for-dev.
- [x] Audit/correlation target-tenant metadata requirements are approved for ready-for-dev: transient metadata with `TargetTenantSource` derived from approved binding/governed ownership record.
- [x] Live fixture setup contract is approved for ready-for-dev: exactly one non-baseline Draft/Inactive Module Catalog item,
  one tenant-scoped active Workflow instance bound to it, and one active approval task.
- [x] Live fixture ownership checks before mutation are approved for ready-for-dev.
- [x] Live fixture cleanup contract is approved for ready-for-dev: workflow task cancel/close API, Module Catalog deactivate API
  when applicable, Module Catalog soft-delete API, with remaining Workflow history expected.
- [x] Workflow task cleanup API path is approved for ready-for-dev: cancel first, controlled close fallback.
- [x] Module Catalog cleanup lifecycle rule is approved for ready-for-dev: deactivate active items before soft-delete; direct soft-delete only for Draft/Inactive items.
- [x] Raw Mongo cleanup, bulk delete, fixture-data files, seed-file changes, and hard delete are explicitly
  prohibited for FU01 live proof.
- [x] MOD-0018/AuthService permission strategy is approved for ready-for-dev: reuse existing keys; no AuthService seed/grant change.
- [x] Runtime scope criteria are recorded for ready-for-dev review: Platform Workflow + Module Catalog reference consumer only.
- [x] Excluded runtime surfaces are recorded for ready-for-dev review: no frontend/Gateway/appsettings/seed/migration/fixture-data/AuthService seed-grant/raw Mongo scope.
- [x] Final ready-for-dev status change is explicitly approved by the user (2026-08-06).
- [x] Runtime implementation is explicitly authorized after ready-for-dev for local implementation only (2026-08-06).

## 19. Implementation Notes

Options considered:

| Option | Assessment |
|---|---|
| Explicit `TargetScope` / `TargetTenantId` on gate request | Approved for ready-for-dev. It preserves tenant-scoped Workflow storage while making platform/global consumers declare the tenant context needed for evaluation. |
| Platform/global Workflow instances | Rejected for this FU. It would require broad persistence/model changes and risks weakening tenant isolation. |
| Disallow global-object workflow gating | Safe but insufficient. It formalizes the limitation but does not close the Module Catalog activation proof gap. |

Recommended behavior:

- Default/current tenant path remains compatible for tenant-owned modules.
- Platform/global consumers must opt into tenant-scoped evaluation explicitly.
- Required gate with missing scope must fail closed.
- Optional gate with no matching Workflow may keep the current `NotApplicable` behavior.
- `TenantScope.Begin` / `BeginPlatform` usage, if selected, must wrap only the Workflow repository lookup and
  active-task lookup and must restore the prior context with `IDisposable`/`using` semantics.
- Evaluate endpoint and mutation endpoint semantics remain intentionally different: evaluate reports the
  decision; mutation maps blocked decisions to no-commit failures.
- `WorkflowTransitionBlockedException` is the in-process signal for source-object mutation paths and should map
  to HTTP 409 with stable reason/correlation metadata.
- `TargetTenantId` authority must be proven from the object/consumer contract before lookup; it is not a
  permission grant and not a tenant-isolation bypass.
- Module Catalog target tenant comes from stored workflow-binding metadata / governed ownership record, not free
  request input.
- Module Catalog-owned workflow-binding metadata shape is `ObjectType`, `ObjectId`, `ObjectRef`,
  `TargetTenantId`, `TargetTenantSource`, `RequiresWorkflowGate`, `WorkflowDefinitionKey` or
  `WorkflowTemplateId`, actor/timestamp metadata, and `CorrelationId`.
- `RequiresWorkflowGate` is a generic `WorkflowGateRequest` field; Module Catalog sets it true when activation
  requires approval.
- Additive JSON compatibility is default-preserving: omitted target-scope fields are treated as
  current-tenant optional gate evaluation.
- Workflow evaluation unavailable maps to `503 Service Unavailable`; workflow-blocked mutation remains
  `409 Conflict`.
- Approved reason codes are `WORKFLOW_PENDING_APPROVAL`, `WORKFLOW_EVALUATION_UNAVAILABLE`,
  `WORKFLOW_REQUIRED_GATE_NOT_FOUND`, `WORKFLOW_INVALID_TARGET_SCOPE`, `WORKFLOW_TARGET_TENANT_REQUIRED`,
  `WORKFLOW_TARGET_TENANT_UNAUTHORIZED`, and `WORKFLOW_TARGET_TENANT_MISMATCH`.
- Permission strategy reuses `platform.workflow.transitions.evaluate` and `platform.module-catalog.update`;
  no AuthService seed/grant change is part of FU01.
- Audit/correlation metadata is transient for FU01, with `TargetTenantSource` derived from the approved
  binding/governed ownership record.
- Workflow fixture cleanup uses `POST /api/v1/workflow/tasks/{taskId}/cancel` first and controlled
  `POST /api/v1/workflow/tasks/{taskId}/close` fallback with reason `B09_FU01_FIXTURE_CLEANUP`.
- Module Catalog cleanup soft-deletes `Draft` or `Inactive` items directly; `Active` items must be deactivated
  first, then soft-deleted.
- Module Catalog activation is the first proof point, not a general license for every global object to use Workflow without a pack-level consumer decision.

Open decisions:

- Exact controller/service location for the approved Workflow task cancel/close cleanup APIs.

## 20. Follow-up Items

1. Confirm the concrete controller/service location for Workflow task cancel/close cleanup APIs.
2. Carry the approved live fixture setup, ownership-check, cleanup, and retained-history contract into implementation review.
3. After implementation, repeat B09-style live smoke without raw Mongo, bulk delete, fixture-data files, seed-file
   changes, or hard delete.
4. After Module Catalog proof passes, reconcile MOD-0023 governance docs to remove the Module Catalog activation design gap.
