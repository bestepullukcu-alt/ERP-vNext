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

This pack recommends explicit `TargetScope` + `TargetTenantId` on the gate request as the safest option.
Platform/global workflow instances are rejected for this follow-up, and silent `NotApplicable` for ambiguous
global-object gating is not allowed.

## 2. Ownership and Boundaries

### In scope

- Additive transition-gate contract planning for explicit target scope.
- Fail-closed behavior when a platform/global caller requires a tenant-scoped gate but omits target tenant scope.
- HTTP/API error mapping for workflow-blocked, unavailable, and invalid-scope outcomes.
- In-process gate behavior for source modules that call `IWorkflowTransitionGate`.
- Module Catalog activation as the reference consumer proof.
- Focused tests around tenant scope resolution, cross-tenant rejection, and blocked/unavailable behavior.

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

No new persistent entity is approved by this draft.

## 4. Entity Fields

No new MongoDB entity is created by this follow-up.

Proposed additive request fields:

| Field | Type | Required | Applies to | Notes |
|---|---|---:|---|---|
| `TargetScope` | enum/string | Yes after contract update | `WorkflowGateRequest`, `EvaluateWorkflowTransitionGateRequest` | Proposed values: `CurrentTenant`, `Tenant`. `CurrentTenant` preserves existing tenant-call behavior. |
| `TargetTenantId` | `Guid?` | Conditional | `WorkflowGateRequest`, `EvaluateWorkflowTransitionGateRequest` | Required when `TargetScope = Tenant`; must be absent or ignored for pure current-tenant calls depending on final compatibility decision. |
| `RequiresWorkflowGate` | `bool?` | Open decision | optional API/internal caller metadata | If true and no scoped Workflow instance is found, response must fail closed instead of returning `NotApplicable`. |

Existing object fields remain unchanged: `ObjectType`, `ObjectId`, `ObjectRef`, `RequestedTransition`,
`RequestedTargetState`, `ActorId`, `ReasonCode`, and `CorrelationId`.

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
| MOD-0018 Auth/RBAC | Permission enforcement for workflow evaluate and module-catalog update actions. | Consumed; no seed/grant edit in this FU unless separately approved. |
| MOD-0021 Audit | Correlation and target tenant audit metadata must remain consistent. | Consumed. |

## 8. Runtime Constraints

- Existing Workflow persistence remains tenant-scoped; no platform/global Workflow instance store is introduced.
- Tenant isolation must fail closed: a platform/global caller cannot accidentally bypass a tenant-owned active Workflow by omitting scope.
- Existing tenant callers must retain backward-compatible behavior when using current tenant context.
- `TargetTenantId` must never authorize cross-tenant access by itself; actor policy and permission checks remain required.
- `Guid.Empty` is not a valid tenant target for tenant-scoped Workflow gate evaluation.
- Gate evaluation must remain read-only against Workflow and source business objects.

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

Proposed validation rules after implementation approval:

- `TargetScope` is required and must be one of the approved enum values.
- `TargetTenantId` is required when `TargetScope = Tenant`.
- `TargetTenantId` must not be `Guid.Empty`.
- Tenant users cannot supply a `TargetTenantId` different from their resolved tenant.
- Platform/partner actors may supply `TargetTenantId` only when the endpoint/action has the workflow evaluate
  permission and the consuming command is approved to perform tenant-scoped gate checks.
- If `RequiresWorkflowGate = true`, no matching Workflow instance is a controlled blocked/unavailable outcome,
  not `NotApplicable`.
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

## 14. Authorization Convention

Existing permission keys remain the base:

- `platform.workflow.transitions.evaluate`
- `platform.module-catalog.update`

This FU does not approve new AuthService seed/grant work by default. If a new permission is later needed for
cross-context gate evaluation, it must be explicitly recorded and handled through MOD-0018/AuthService ownership.

Actor rules:

- Tenant actors may evaluate gates only for their resolved tenant.
- Platform/partner actors may evaluate a tenant-scoped gate only through explicit `TargetScope = Tenant` and
  non-empty `TargetTenantId`.
- Missing or unauthorized actor context must fail closed.

## 15. Gateway / API Routing Decision

No new Gateway route is expected.

The existing route family remains:

- Platform direct API: `POST /api/v1/workflow/transitions/evaluate`
- Gateway exposure: existing `/api/v1/workflow/**` routing remains integration-agent owned.

If a future contract adds public fields to the API request, Gateway should require no route change. If a new route
is proposed later, it must be handled as a separate integration-agent task.

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

## 18. Ready-for-dev Checklist

- [ ] User approves this draft pack.
- [ ] DCP-002 follow-up identity evidence remains recorded.
- [ ] Final enum names for `TargetScope` are approved.
- [ ] Decision on `RequiresWorkflowGate` field is approved or explicitly deferred.
- [ ] HTTP mapping table is approved.
- [ ] Module Catalog activation target-tenant source is approved.
- [ ] MOD-0018/AuthService confirms no new permission seed/grant is needed, or approves the exact new permission.
- [ ] Runtime scope remains limited to Platform Workflow + Module Catalog reference consumer files.
- [ ] No frontend/Gateway/appsettings/seed/migration/fixture-data scope is added.

## 19. Implementation Notes

Options considered:

| Option | Assessment |
|---|---|
| Explicit `TargetScope` / `TargetTenantId` on gate request | Recommended. It preserves tenant-scoped Workflow storage while making platform/global consumers declare the tenant context needed for evaluation. |
| Platform/global Workflow instances | Rejected for this FU. It would require broad persistence/model changes and risks weakening tenant isolation. |
| Disallow global-object workflow gating | Safe but insufficient. It formalizes the limitation but does not close the Module Catalog activation proof gap. |

Recommended behavior:

- Default/current tenant path remains compatible for tenant-owned modules.
- Platform/global consumers must opt into tenant-scoped evaluation explicitly.
- Required gate with missing scope must fail closed.
- Optional gate with no matching Workflow may keep the current `NotApplicable` behavior.
- Module Catalog activation is the first proof point, not a general license for every global object to use Workflow without a pack-level consumer decision.

Open decisions:

- How Module Catalog activation obtains the target tenant: request body, route/query parameter, linked governance record, or approved workflow-binding metadata.
- Whether `RequiresWorkflowGate` belongs in the generic gate request or remains consumer-specific.
- Exact HTTP status mapping for workflow-blocked: candidate `409 Conflict` for blocked/invalid state, `400 Bad Request` for invalid scope, `503 Service Unavailable` or fail-closed `409` for unavailable evaluation.
- Whether a new permission key is needed for platform actors evaluating tenant-scoped gates.

## 20. Follow-up Items

1. Review and approve/reject this pack for ready-for-dev.
2. Resolve the target-tenant source for Module Catalog activation.
3. Finalize HTTP error mapping and reason codes.
4. Decide whether to add `RequiresWorkflowGate`.
5. After implementation, repeat B09-style live smoke without creating raw Mongo fixture data.
6. After Module Catalog proof passes, reconcile MOD-0023 governance docs to remove the Module Catalog activation design gap.
