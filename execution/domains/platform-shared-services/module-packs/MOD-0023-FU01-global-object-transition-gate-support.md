---
id: MOD-0023-FU01
name: Global Object Transition Gate Support
parent: MOD-0023
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: TenantScopedEntity
status: draft
owner: platform-shared-services
branch: feature/pss/mod-0023-fu01-global-object-transition-gate-support
started: 2026-08-06
target: TBD
form_field_count: 0
---

# MOD-0023-FU01 — Global Object Transition Gate Support

> **Draft only.** This follow-up pack does not authorize runtime implementation. It records the proposed
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
For Module Catalog activation, the approved draft source is stored workflow-binding metadata / a governed ownership
record that can be audited and validated before the gate lookup runs. Free request input is explicitly rejected as
the source of authority.

Partial draft decision reconciliation (2026-08-06):

- `TargetScope` values are approved for draft planning as `CurrentTenant` and `Tenant`.
- `TargetTenantId` authority rules are approved for draft planning as documented: target tenant must come from an
  approved consumer-owned source of authority and is not sufficient authorization by itself.
- Module Catalog target-tenant source is approved for draft planning as stored workflow-binding metadata /
  governed ownership record, not free request input.
- `RequiresWorkflowGate` is approved for draft planning as a generic field on `WorkflowGateRequest`;
  Module Catalog sets it `true` when activation requires approval.
- Unavailable evaluation maps to `503 Service Unavailable`; workflow-blocked mutation remains `409 Conflict`.
- Permission strategy reuses `platform.workflow.transitions.evaluate` and `platform.module-catalog.update`;
  no AuthService seed/grant change is approved for this FU.
- Audit/correlation metadata is transient for this FU; `TargetTenantSource` is derived from the approved
  binding/governed ownership record.
- Live fixture setup, ownership-check, cleanup, and retained-history strategy is approved for draft planning as
  documented in §§16-18.
- The pack remains in `status: draft` because user approval for ready-for-dev, DCP-002 evidence confirmation,
  runtime-scope approval, and no-frontend/Gateway/appsettings/seed/migration/fixture-data scope confirmation
  remain unchecked.

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
- Treating this draft as ready-for-dev or implementation approval.

## 3. Owned Objects

This follow-up owns contract-level changes only after approval:

| Object / contract | Ownership decision |
|---|---|
| `WorkflowGateRequest` | Additive target scope fields for in-process callers. |
| `EvaluateWorkflowTransitionGateRequest` | Additive target scope fields for API callers. |
| `EvaluateWorkflowTransitionGateQuery` / handler | Resolve effective workflow tenant scope before repository lookup. |
| `IWorkflowTransitionGate` implementation | Preserve fail-closed semantics and map controlled gate failures. |
| Workflow gate reason/error codes | Stable codes for invalid target scope, workflow blocked, and evaluation unavailable. |
| Module Catalog activation reference consumer | Prove a global object transition can be blocked by a tenant-scoped Workflow instance when explicit tenant scope is supplied. |
| Audit/correlation metadata | Record effective target tenant, source object reference, and correlation id for cross-context gate decisions. |

No new persistent entity is approved by this draft.

## 4. Entity Fields

No new MongoDB entity is created by this follow-up.

Proposed additive request fields:

| Field | Type | Required | Applies to | Notes |
|---|---|---:|---|---|
| `TargetScope` | enum/string | Yes after contract update | `WorkflowGateRequest`, `EvaluateWorkflowTransitionGateRequest` | Approved draft values: `CurrentTenant`, `Tenant`. `CurrentTenant` preserves existing tenant-call behavior. |
| `TargetTenantId` | `Guid?` | Conditional | `WorkflowGateRequest`, `EvaluateWorkflowTransitionGateRequest` | Required when `TargetScope = Tenant`; must be absent or ignored for pure current-tenant calls depending on final compatibility decision. |
| `RequiresWorkflowGate` | `bool?` | Conditional | `WorkflowGateRequest`; API exposure remains additive if needed | Approved as a generic gate field. If true and no scoped Workflow instance is found, response must fail closed instead of returning `NotApplicable`. Module Catalog sets it true when activation requires approval. |
| `TargetTenantSource` | enum/string | Required in transient audit metadata for scoped platform/global evaluation | internal metadata, not necessarily public API | Approved as transient audit metadata for this FU, derived from stored workflow-binding metadata / governed ownership record. |

Existing object fields remain unchanged: `ObjectType`, `ObjectId`, `ObjectRef`, `RequestedTransition`,
`RequestedTargetState`, `ActorId`, `ReasonCode`, and `CorrelationId`.

Backward compatibility rule: existing JSON payloads that omit all new fields must deserialize and behave as
`TargetScope = CurrentTenant`, `TargetTenantId = null`, and optional-gate semantics unless the consumer explicitly
marks the gate as required.

## 5. Repo Scope

Authorized future implementation scope after the pack is approved / ready-for-dev:

- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/IWorkflowTransitionGate.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Queries/EvaluateWorkflowTransitionGateQuery.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/EvaluateWorkflowTransitionGateHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Validators/EvaluateWorkflowTransitionGateValidator.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/WorkflowTransitionGate.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/ActivateModuleCatalogItemCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Common/**` or existing exception mapping surface only if
  needed to map `WorkflowTransitionBlockedException` consistently.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/**`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleCatalog/**`

This draft creation edits only this module pack file.

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

Draft validation rules after implementation approval:

- `TargetScope` defaults to `CurrentTenant` for omitted JSON fields unless the final compatibility decision
  explicitly rejects defaulting.
- `TargetScope` must be one of the approved draft values: `CurrentTenant` or `Tenant`.
- `TargetTenantId` is required when `TargetScope = Tenant`.
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

## 13. Failure Path to Verify

| Failure path | Expected behavior |
|---|---|
| Platform/global caller omits `TargetTenantId` for a required tenant-scoped gate | Controlled non-success, fail closed; source object state unchanged. |
| Platform/global caller supplies `Guid.Empty` as `TargetTenantId` | 400 invalid target scope. |
| Tenant caller supplies another tenant's `TargetTenantId` | 403 or controlled invalid-scope response; no workflow metadata leak. |
| Matching tenant Workflow instance is active / waiting approval | Gate returns blocked / pending approval; Module Catalog activation does not commit. |
| Matching tenant Workflow instance is approved/completed | Gate allows the transition; Module Catalog activation may commit if its own rules pass. |
| Gate repository/service unavailable | Controlled workflow-unavailable mapping; source transition blocked. |
| No Workflow found while gate is optional | Existing `NotApplicable` behavior remains allowed. |
| No Workflow found while gate is required | Controlled blocked/unavailable result; no silent bypass. |
| `TenantScope` throws or downstream lookup fails | Previous tenant context is restored; response fails closed. |
| Existing client omits new JSON fields | Current-tenant optional-gate behavior is preserved. |

## 14. Authorization Convention

Existing permission keys remain the base:

- `platform.workflow.transitions.evaluate`
- `platform.module-catalog.update`

Permission strategy is approved for draft planning as reuse of these existing keys. No new MOD-0018-owned key,
AuthService seed, or AuthService grant change is approved for this FU.

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

Proposed HTTP mapping:

| Scenario | Evaluate endpoint | Mutation endpoint |
|---|---|---|
| Workflow active / pending approval | 200 with `Decision=Blocked`, `GateStatus=PendingApproval` | 409 `WORKFLOW_PENDING_APPROVAL` or equivalent controlled reason |
| Invalid target scope / missing required target tenant | 400 controlled validation failure | 400 controlled validation failure; no mutation |
| Unauthorized target tenant / cross-tenant tenant actor | 403 controlled failure | 403 controlled failure; no mutation |
| Workflow evaluation unavailable | 503 `WORKFLOW_EVALUATION_UNAVAILABLE` or equivalent controlled reason; fail closed | 503 `WORKFLOW_EVALUATION_UNAVAILABLE` or equivalent controlled reason; no mutation |
| Optional no-workflow | 200 `NotApplicable` | Mutation may proceed if consumer rules allow |
| Required no-workflow | 409 controlled required-gate failure | 409 controlled required-gate failure; no mutation |
| `WorkflowTransitionBlockedException` from in-process gate | N/A unless exposed by a caller | 409 controlled blocked-transition response |

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
  deactivate the Module Catalog item if activation occurred, then soft-delete the Module Catalog item through
  the Module Catalog API. Raw Mongo edits, bulk delete, fixture-data files, seed files, and hard delete are
  prohibited.
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
- Cleanup smoke must use approved APIs only: Workflow task cancel/close first, Module Catalog deactivate if
  needed, then Module Catalog soft-delete. No raw Mongo, no bulk delete, no fixture-data files, no seed-file
  changes, and no hard delete.
- Cleanup verification must read back current object state where APIs allow it and record that Workflow history
  remains as expected instead of being erased.

## 18. Ready-for-dev Checklist

- [ ] User approves this draft pack.
- [ ] DCP-002 follow-up identity evidence remains recorded.
- [x] Final enum names for `TargetScope` are approved for draft planning: `CurrentTenant`, `Tenant`.
- [x] Decision on `RequiresWorkflowGate` field is approved for draft planning: generic field in `WorkflowGateRequest`; Module Catalog sets it true when activation requires approval.
- [x] HTTP mapping table is approved for draft planning: unavailable evaluation maps to 503 Service Unavailable; workflow-blocked mutation remains 409 Conflict.
- [x] Module Catalog activation target-tenant source is approved for draft planning: stored workflow-binding metadata / governed ownership record, not free request input.
- [x] Target tenant authority/source rules are approved for draft planning; arbitrary client-supplied tenant ids are not accepted as sufficient authority.
- [x] Evaluate endpoint vs mutation endpoint response semantics are approved for draft planning.
- [x] `WorkflowTransitionBlockedException` 409 mapping is approved for draft planning; workflow-blocked mutation remains `409 Conflict`.
- [ ] Additive JSON compatibility behavior is approved.
- [x] Audit/correlation target-tenant metadata requirements are approved for draft planning: transient metadata with `TargetTenantSource` derived from approved binding/governed ownership record.
- [x] Live fixture setup contract is approved for draft planning: exactly one non-baseline Draft/Inactive Module Catalog item,
  one tenant-scoped active Workflow instance bound to it, and one active approval task.
- [x] Live fixture ownership checks before mutation are approved for draft planning.
- [x] Live fixture cleanup contract is approved for draft planning: workflow task cancel/close API, Module Catalog deactivate API
  when applicable, Module Catalog soft-delete API, with remaining Workflow history expected.
- [x] Raw Mongo cleanup, bulk delete, fixture-data files, seed-file changes, and hard delete are explicitly
  prohibited for FU01 live proof.
- [x] MOD-0018/AuthService permission strategy is approved for draft planning: reuse existing keys; no AuthService seed/grant change.
- [ ] Runtime scope remains limited to Platform Workflow + Module Catalog reference consumer files.
- [ ] No frontend/Gateway/appsettings/seed/migration/fixture-data scope is added.

## 19. Implementation Notes

Options considered:

| Option | Assessment |
|---|---|
| Explicit `TargetScope` / `TargetTenantId` on gate request | Recommended and partially approved for draft planning. It preserves tenant-scoped Workflow storage while making platform/global consumers declare the tenant context needed for evaluation. |
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
- `RequiresWorkflowGate` is a generic `WorkflowGateRequest` field; Module Catalog sets it true when activation
  requires approval.
- Workflow evaluation unavailable maps to `503 Service Unavailable`; workflow-blocked mutation remains
  `409 Conflict`.
- Permission strategy reuses `platform.workflow.transitions.evaluate` and `platform.module-catalog.update`;
  no AuthService seed/grant change is part of FU01.
- Audit/correlation metadata is transient for FU01, with `TargetTenantSource` derived from the approved
  binding/governed ownership record.
- Module Catalog activation is the first proof point, not a general license for every global object to use Workflow without a pack-level consumer decision.

Open decisions:

- Concrete shape/location of the stored workflow-binding metadata / governed ownership record.
- Exact code values for `WORKFLOW_EVALUATION_UNAVAILABLE`, `WORKFLOW_PENDING_APPROVAL`, and required-gate failures.
- Whether additive JSON compatibility behavior is fully approved for ready-for-dev or needs an explicit owner check.
- Exact cleanup API path for Workflow task cancellation/closure in the approved live-smoke actor context.
- Whether Module Catalog cleanup should prefer `Inactive -> soft-delete` or direct soft-delete when the item was
  never activated.

## 20. Follow-up Items

1. Review and approve/reject this pack for ready-for-dev.
2. Define the concrete stored workflow-binding metadata / governed ownership record shape.
3. Finalize reason-code constants for unavailable evaluation, pending approval, and required no-workflow failures.
4. Finalize additive JSON compatibility expectations before ready-for-dev.
5. Finalize transient audit/correlation target-tenant metadata shape.
6. Carry the approved live fixture setup, ownership-check, cleanup, and retained-history contract into ready-for-dev review.
7. After implementation, repeat B09-style live smoke without raw Mongo, bulk delete, fixture-data files, seed-file
   changes, or hard delete.
8. After Module Catalog proof passes, reconcile MOD-0023 governance docs to remove the Module Catalog activation design gap.
