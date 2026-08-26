---
id: MOD-0023
name: Workflow Designer (Approvals/SLAs/Escalations)
scope_title: Workflow Config / Approval Templates
domain: platform-shared-services
service: Diten.Platform
shell: none
status: ready-for-dev
owner: platform-shared-services
branch: feature/pss/mod-0023-workflow-config-approval-templates
entity_base: TenantScopedEntity
golden_reference: n/a
form_field_count: n/a
started: 2026-06-23
target: TBD
status_changed: draft -> ready-for-dev on 2026-06-23 (user-approved; DCP-002 gate PASS)
previous: MOD-0023-workflow-designer (stale pack, superseded by this file)
---

# MOD-0023 — Workflow Designer (Approvals/SLAs/Escalations)

> **Canonical identity (DCP-002):** The Blueprint/registry canonical name for `MOD-0023` is
> **"Workflow Designer (Approvals/SLAs/Escalations)"**. This pack's working scope label is
> **"Workflow Config / Approval Templates"** — a descriptive scope title only, never a second module
> identity. The frontmatter `name:` stays canonical so the DCP-002 gate cannot collide.

> **No code is produced by this pack.** It is a development contract only. Implementation begins only after
> the user sets `status: approved` / `ready-for-dev` and the controlled gates in §13 are satisfied.

## 1. Module Summary

MOD-0023 is the **central, tenant-scoped approval/workflow engine** for Diten ERP vNext, developed as a
**platform-level shared service inside `Diten.Platform`**. It owns versioned approval workflow templates,
their immutable published versions, runtime workflow instances (version-pinned), approval tasks, runtime
assignment snapshots, append-only transition logs, and SLA/escalation rules. Other modules call MOD-0023
**before they commit a business-object lifecycle transition** (the transition gate): an active blocking
workflow holds the transition; a completed workflow clears it.

MVP posture (carried from `domain-config.md`): **approvals-focused workflow only — no BPMN engine, no
low-code/visual builder, no SignalR source-of-truth.** Escalation runs on the existing recurring-job seam.

Although the engine and APIs live in `Diten.Platform` (a platform-level shared service), **all persisted
workflow data is tenant-scoped**: every owned aggregate inherits `TenantScopedEntity`, `TenantId` is never
authoritative from the client payload, tenant context is always resolved server-side, and any cross-tenant
read/update/delete returns a **404 / empty result with no metadata leak**.

## 2. Ownership Boundaries

### MOD-0023 owns

- `WorkflowTemplate`, `WorkflowTemplateVersion` — versioned approval workflow definitions and immutable
  published versions.
- `WorkflowInstance` — a version-pinned running workflow over a source business object.
- `ApprovalTask` — the unit of human action (approve / reject / delegate / request-info / cancel).
- `RuntimeAssignmentSnapshot` — the point-in-time resolved assignee set for a task/step (who may act now).
- `WorkflowTransitionLog` — append-only record of every workflow state/task transition.
- `SlaEscalationRule` — SLA timing + escalation policy metadata.
- The **lifecycle transition gate**: evaluate whether a source object's lifecycle transition is allowed/blocked.

### MOD-0023 does NOT own (consumed, never re-implemented)

| Concern | Owner |
|---|---|
| RBAC / permission catalog / assignee authority / access decisions | **MOD-0018** + the existing Auth/RBAC service (`Diten.AuthService`) |
| Audit trail | **MOD-0021** + the existing audit pipeline |
| Operational task execution (system actions / checklists) | **MOD-0024** |
| Business-object lifecycle **state** itself | the **source business module** (MOD-0023 only gates the transition) |
| BPMN / low-code / visual workflow builder | out of scope (no owner in MVP) |
| Realtime push | SignalR is **projection-only if ever added** — never a source of truth, deferred |

- `MOD-0024` (Tasks) and `MOD-0023` (Approvals) responsibilities are **never merged** — Tasks never writes
  approval semantics, Approvals never executes operational tasks (domain-config §Ownership Boundaries).
- MOD-0023 may **define** `[HasPermission]` permission **constants** in `Diten.Platform`, but the permission
  **seed/grant** belongs to MOD-0018 / `Diten.AuthService` and is a **separate task** (see §5, §8).

## 3. Owned Objects

| Object | Role |
|---|---|
| `WorkflowTemplate` | Logical approval workflow definition; carries `TemplateCode` (tenant-unique), status, current published version pointer. |
| `WorkflowTemplateVersion` | Immutable snapshot of a published definition (steps, routing, SLA bindings, monotonic `VersionNumber`). |
| `WorkflowInstance` | Running workflow over a `ObjectRef`; pins the `WorkflowTemplateVersion` it started from. |
| `ApprovalTask` | A pending/closed approval unit within an instance (state, reasonCode, idempotency key, outcome). |
| `RuntimeAssignmentSnapshot` | Resolved assignee set for a task/step at assignment time (who may act). |
| `WorkflowTransitionLog` | Append-only log of instance/task transitions (actor, from→to, reasonCode, correlationId). |
| `SlaEscalationRule` | SLA window + escalation action metadata bound to a template/step. |

All seven inherit `TenantScopedEntity`. No object stores business-object state; MOD-0023 references the source
object only via an opaque `ObjectRef` (module + objectType + objectId), never by forking the business entity.

## 4. Repo Scope

### Authorized backend scope (after approval)

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Workflow/**` — workflow aggregates.
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/Workflow/**` — workflow enums (statuses, task
  states, transition kinds, share/sla enums).
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IWorkflow*.cs` — repository interfaces
  (flat `I*Repositories.cs` / `I*Repository.cs` per live convention).
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/**` — CQRS commands, queries,
  handlers, validators, services, `WorkflowModels.cs`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs`
  — repository implementations (`TenantRepository<T>` subclasses, snake_case collection names).
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs`
  — add tenant-first workflow indexes (extend existing file; do not fork).
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` (or the equivalent live
  Infrastructure DI registration file) — register workflow repositories/services.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Workflow*.cs` — thin MediatR-dispatching
  controllers under `CustomBaseController`.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/**` — focused backend tests.

### Frontend scope — ONLY if a later batch explicitly includes UI (Batch 08, separately approved)

- `frontend/Diten.Web/Controllers/WorkflowController.cs`
- `frontend/Diten.Web/Views/Platform/Workflow/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Workflow/**`
- `frontend/Diten.Web/Resources/Views/Platform/Workflow/**`

Frontend is **excluded from Batch 01** and from every backend batch. If/when admin UI is approved it follows
the live Platform-admin shell (`_LayoutPlatformAdmin.cshtml`) and the Golden Reference DataTable contract;
this pack does not pre-commit Slim/Compact until UI scope is approved (`golden_reference: n/a` for now).

### Separately governed scope (not this pack's runtime work)

- `gateway/Diten.ApiGateway/**/ocelot.json` — **integration-agent follow-up** (see §9, §13).
- `services/Diten.AuthService/**` permission seed/grant — **separate MOD-0018 / security follow-up** (see §8, §13).

## 5. Protected Paths

MOD-0023 must **never** touch these (no runtime edit in this pack or its backend batches):

- `.antigravity/**` — global engineering system.
- `gateway/Diten.ApiGateway/**/ocelot.json` — only the `integration-agent` edits routes; MOD-0023 route is a
  follow-up task, not part of this pack's code.
- `services/Diten.AuthService/**` — permission seed/grant is a separate MOD-0018 / security task.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` — FROZEN archive layout.
- `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**` — FROZEN legacy.
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`,
  `services/Diten.DevEnablementService/**` — other domains' services.
- `services/Diten.Platform.Common/**` — shared base (`TenantScopedEntity`, `TenantRepository<T>`, tenant
  context, correlation) is **consumed, never modified**.
- MOD-0024 / MOD-0021 / MOD-0018 owned files — consumed via their contracts only.

## 6. Backend File Convention

Follow the live Diten.Platform CQRS convention (mirrors the Golden Reference action-based shape; verified
against `Features/DocumentManagement*` and `PlatformAdministrators`):

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/
|-- Commands/                       (sealed records, one per file)
|   |-- CreateWorkflowDefinitionCommand.cs
|   |-- PublishWorkflowDefinitionCommand.cs
|   |-- StartWorkflowInstanceCommand.cs
|   |-- ApproveWorkflowTaskCommand.cs
|   |-- RejectWorkflowTaskCommand.cs
|   |-- DelegateWorkflowTaskCommand.cs
|   |-- RequestInfoWorkflowTaskCommand.cs
|   |-- CancelWorkflowTaskCommand.cs
|   |-- RunWorkflowEscalationsCommand.cs
|   `-- EvaluateWorkflowTransitionCommand.cs
|-- Queries/                        (sealed records, one per file)
|   |-- GetWorkflowDefinitionListQuery.cs
|   |-- GetWorkflowDefinitionByIdQuery.cs
|   |-- GetWorkflowInstanceByIdQuery.cs
|   |-- GetWorkflowInstanceListQuery.cs
|   `-- GetWorkflowTaskListQuery.cs
|-- Handlers/
|   |-- CommandHandlers/            (sealed, no Command suffix: {Verb}{Slice}Handler)
|   `-- QueryHandlers/              (sealed, no Query suffix: Get{Slice}Handler)
|-- Validators/                     ({Verb}{Slice}Validator, no Command suffix)
|-- Services/                       (focused engine services, not oversized handlers)
|   |-- IWorkflowVersioningService.cs   (immutable publish + active-version resolution)
|   |-- IWorkflowInstanceService.cs     (start + version pinning + initial task)
|   |-- IWorkflowTransitionService.cs   (approve/reject/delegate/etc. + SOD + idempotency + transition log)
|   `-- IWorkflowTransitionGate.cs      (lifecycle block/clear evaluation for source modules)
`-- WorkflowModels.cs               (all DTOs / result models in one file)
```

Rules:
- Each command / query / handler / validator in its own file; commands and queries are **sealed records**.
- Handler class names carry **no `Command`/`Query` suffix** (`ApproveWorkflowTaskHandler`, not
  `ApproveWorkflowTaskCommandHandler`); validators carry **no `Command` suffix**.
- Controllers inherit `CustomBaseController`, stay thin, and dispatch via `IMediator`.
- Use `Response<T>` for every API result and the existing `ICorrelationContext` for correlation.
- Use `[Authorize]` on the controller and `[HasPermission]` per action.
- **Do NOT create** a new base entity, repository base, tenant context, permission infra, correlation infra,
  audit infra, event infra, or background-job infra. Reuse `TenantScopedEntity`, `TenantRepository<T>`, the
  existing tenant/correlation seams, and the existing recurring-job seam.

## 7. Persistence Contract

- **MongoDB, single DB, tenant-scoped** (repo-wide runtime decision).
- All seven MOD-0023 persisted entities inherit `Diten.Platform.Common.Persistence.TenantScopedEntity`
  (`TenantId`, `IsDeleted`, `DeletedAt`, technical `Version`, audit fields inherited / server-resolved).
- Repositories inherit the existing **`TenantRepository<T>`** (same base used by `CollectionInstanceRepository`).
- **Soft delete respected** (`IsDeleted` filter on every read); **no hard delete**.
- **Tenant-first compound indexes required** on every collection (TenantId leading key), added to
  `MongoDbIndexConfigurations.cs`.
- **`TenantId` is never client-supplied authority** — resolved from `ITenantContext` server-side only.
- Business versioning uses a semantic `VersionNumber`; never the technical inherited `Version`.

Initial collection names (snake_case, area-prefixed per live convention):

| Collection | Entity |
|---|---|
| `workflow_templates` | `WorkflowTemplate` |
| `workflow_template_versions` | `WorkflowTemplateVersion` |
| `workflow_instances` | `WorkflowInstance` |
| `approval_tasks` | `ApprovalTask` |
| `workflow_runtime_assignment_snapshots` | `RuntimeAssignmentSnapshot` |
| `workflow_transition_logs` | `WorkflowTransitionLog` |
| `workflow_sla_rules` | `SlaEscalationRule` |

Required uniqueness / indexes (closed at implementation):
- `workflow_templates`: `{TenantId, TemplateCode}` unique (non-deleted); tenant-first list index.
- `workflow_template_versions`: `{TenantId, TemplateId, VersionNumber}` unique.
- `workflow_instances`: `{TenantId, _id}`; `{TenantId, ObjectRef}` for the transition gate; pinned
  `TemplateVersionId` indexed.
- `approval_tasks`: `{TenantId, InstanceId}`; `{TenantId, Status}`; idempotency `{TenantId, IdempotencyKey}` unique.
- `workflow_transition_logs`: append-only; `{TenantId, InstanceId, SequenceNo}`.
- `workflow_sla_rules`: `{TenantId, TemplateId}`.

## 8. Permission Convention

Actor type: platform/tenant user via JWT. Runtime canonical format: **lowercase dotted keys** (PKS-001),
defined as `[HasPermission]` constants in `Diten.Platform`:

| Key | Meaning |
|---|---|
| `platform.workflow.definitions.view` | view definition list/detail/versions |
| `platform.workflow.definitions.manage` | create/update draft definition |
| `platform.workflow.definitions.publish` | publish an immutable version |
| `platform.workflow.instances.start` | start a workflow instance |
| `platform.workflow.instances.view` | view instance detail/list |
| `platform.workflow.tasks.approve` | approve a task |
| `platform.workflow.tasks.reject` | reject a task |
| `platform.workflow.tasks.delegate` | delegate a task |
| `platform.workflow.tasks.request-info` | request info on a task |
| `platform.workflow.tasks.cancel` | cancel a task |
| `platform.workflow.escalations.run` | run the escalation worker action |
| `platform.workflow.transitions.evaluate` | evaluate the lifecycle transition gate |
| `platform.workflow.realtime.subscribe` | **deferred** — only if a SignalR projection is later approved |

Permission strategy (controlled):
- MOD-0023 may **define** the constants/attributes locally in `Diten.Platform`.
- Permission **seed/grant** lives in `Diten.AuthService` / MOD-0018 and is a **separate MOD-0018 / security
  follow-up task** — this pack and its backend batches do **not** edit AuthService.
- Backend and (future) frontend resolve the **same** effective lowercase key; missing permission → 403.

## 9. API Contract Plan

Base route: `api/v1/workflow` (version-explicit; no `v2`, no unversioned route). Frontend (when it exists)
calls Gateway `5000` / same-origin proxy, never the Platform service port directly.

| Method | Path | Permission |
|---|---|---|
| POST | `api/v1/workflow/definitions` | `definitions.manage` |
| GET | `api/v1/workflow/definitions` | `definitions.view` |
| GET | `api/v1/workflow/definitions/{id}` | `definitions.view` |
| POST | `api/v1/workflow/definitions/{id}/publish` | `definitions.publish` |
| POST | `api/v1/workflow/instances` | `instances.start` |
| GET | `api/v1/workflow/instances/{id}` | `instances.view` |
| GET | `api/v1/workflow/instances` | `instances.view` |
| GET | `api/v1/workflow/tasks` | `instances.view` |
| POST | `api/v1/workflow/tasks/{taskId}/approve` | `tasks.approve` |
| POST | `api/v1/workflow/tasks/{taskId}/reject` | `tasks.reject` |
| POST | `api/v1/workflow/tasks/{taskId}/delegate` | `tasks.delegate` |
| POST | `api/v1/workflow/tasks/{taskId}/request-info` | `tasks.request-info` |
| POST | `api/v1/workflow/tasks/{taskId}/cancel` | `tasks.cancel` |
| POST | `api/v1/workflow/escalations/run` | `escalations.run` |
| POST | `api/v1/workflow/transitions/evaluate` | `transitions.evaluate` |

**Gateway routing decision:** verify whether an existing `/api/v1/{everything}` or platform catch-all already
covers `api/v1/workflow/**` for `GET/POST`. If a new explicit Ocelot route is required, it is a **separate
integration-agent task** — this pack does **not** touch `ocelot.json` (§5, §13). Confirm at Batch 09.

## 10. Acceptance Criteria

Governance:
- [x] Pack rewritten to live ERP-vNext conventions (stale `Aggregates/`, `src/Backend/`,
  `Diten.Platform.Persistence`, `Diten.WebAPI.csproj` references removed). Governance item only.
- [x] Pack moved to `status: ready-for-dev` only after explicit user approval (2026-06-23).
- [x] DCP-002 module-identity gate confirmed for `MOD-0023` (registry + `verify_module_id.py`, exit 0) — **CONTROLLED GATE PASSED**.

Per-batch (testable; full per-batch detail in §12):
- [ ] **B01** A `WorkflowTemplate` can be created, persisted tenant-scoped, reloaded by id, and listed for the
  same tenant; a cross-tenant read returns not-found / empty list with no leaked id.
- [ ] **B02** A draft template publishes an **immutable** `WorkflowTemplateVersion` with a monotonic
  `VersionNumber`; editing a published version is rejected.
- [ ] **B03** An instance starts **only** from an active published template, pins that version, creates the
  initial `ApprovalTask` + `RuntimeAssignmentSnapshot` + start `WorkflowTransitionLog`; start from
  unpublished/missing template is rejected.
- [ ] **B04** An assigned approver's approve/reject closes the task and writes an append-only transition log;
  the submitter cannot approve own workflow (SOD-003); a duplicate `IdempotencyKey` does not duplicate the
  mutation.
- [ ] **B05** Delegate / request-info / cancel update the assignment snapshot; a non-assigned actor cannot act.
- [ ] **B06** The transition gate blocks a source-module lifecycle transition while a blocking workflow is
  active and clears it when completed; a cross-tenant `ObjectRef` does not leak.
- [ ] **B07** The escalation worker escalates/times out an overdue task **once**; a rerun does not
  double-escalate.
- [ ] **B08** (only if UI approved) Platform admin screens list templates/tasks/instances via the API; API
  failure shows a controlled state with no fake buttons.
- [ ] **B09** Build + tests pass; permission-seed confirmation and gateway-route confirmation are recorded;
  module-identity gate confirmed; any unseeded permission / missing route stays a release blocker.

## 11. Failure Paths

| # | Failure path | Expected behavior |
|---|---|---|
| 1 | Cross-tenant read of template/instance/task | 404 / empty result; **no metadata leak** |
| 2 | Client supplies `TenantId` in payload | ignored; tenant context resolved server-side wins |
| 3 | Duplicate `TemplateCode` in same tenant | blocked (409 `CONFLICT`) |
| 4 | Same `TemplateCode` in a different tenant | allowed |
| 5 | Edit a published template version | blocked (immutable) |
| 6 | Start instance from unpublished/missing template | blocked (400/404) |
| 7 | Duplicate idempotency key on a mutation | does not duplicate the mutation (single effect) |
| 8 | Submitter approves own workflow | blocked (SOD-003, 403/validation) |
| 9 | Actor not in assignment snapshot acts (approve/reject/delegate) | blocked (403) |
| 10 | Completed/closed task transitions again | blocked (409 / invalid-state) |
| 11 | SLA worker rerun over an already-escalated task | does **not** double-escalate |

All controlled failures return `Response<T>` with a stable reason code + correlation id; no stack traces.

## 12. Implementation Batches

### Batch 01 — Data foundation
- **Scope:** `WorkflowTemplate`, `WorkflowInstance`, `ApprovalTask`, `WorkflowTransitionLog` entities;
  repository interfaces + `TenantRepository<T>` implementations; tenant-first indexes; create/read CQRS for
  `WorkflowTemplate`; thin `WorkflowDefinitionsController`; backend tests. **No frontend.**
- **Golden flow:** create workflow template → persist tenant-scoped → reload by id → list for same tenant.
- **Failure path:** cross-tenant read returns not-found / empty list (no leak).

### Batch 02 — Template version & immutable publish
- **Scope:** `WorkflowTemplateVersion`, publish command, immutable version, monotonic `VersionNumber`,
  optimistic concurrency.
- **Golden flow:** draft template → publish version → reload immutable version.
- **Failure path:** edit of a published version blocked.

### Batch 03 — Instance start & version pinning
- **Scope:** start workflow from an active published template; create `WorkflowInstance`; create initial
  `ApprovalTask`; create `RuntimeAssignmentSnapshot`; write start `WorkflowTransitionLog`.
- **Golden flow:** start instance → pinned template version → task created → reload.
- **Failure path:** start from unpublished/missing template blocked.

### Batch 04 — Approve / reject / idempotency / SOD
- **Scope:** approve/reject transitions, `reasonCode`, `idempotencyKey`, SOD-003 (submitter ≠ approver),
  append-only transition log.
- **Golden flow:** assigned approver approves → task closes → instance completes → transition log written.
- **Failure path:** submitter approval blocked; duplicate idempotency does not duplicate the mutation.

### Batch 05 — Delegate / request-info / cancel
- **Scope:** remaining task transitions and assignment-snapshot changes.
- **Golden flow:** assigned approver delegates → new snapshot → delegate acts.
- **Failure path:** non-assigned actor cannot delegate.

### Batch 06 — Integration transition gate
- **Scope:** `EvaluateWorkflowTransition` for source business modules (block while active / clear when done).
- **Golden flow:** active blocking workflow blocks the lifecycle transition → completed workflow clears it.
- **Failure path:** cross-tenant `ObjectRef` does not leak.

### Batch 07 — SLA / escalation
- **Scope:** recurring escalation worker on the **existing background-job seam** (no new realtime infra).
- **Golden flow:** overdue task escalates / times out once.
- **Failure path:** rerun does not double-escalate.

### Batch 08 — Frontend / admin UI (only if separately approved)
- **Scope:** Platform Workflow admin screens (`_LayoutPlatformAdmin.cshtml`) **after** backend APIs exist;
  Golden Reference + DataTable v2 contract; same-origin proxy / Gateway only.
- **Golden flow:** admin lists templates / tasks / instances using the API.
- **Failure path:** API failure shows a controlled state; no fake buttons.

### Batch 09 — Release validation
- **Scope:** build, tests, **permission-seed confirmation** (MOD-0018 task), **gateway-route confirmation**
  (integration-agent task), module-identity gate confirmation.
- **Golden flow:** all backend tests pass and transition flows verified.
- **Failure path:** unseeded permissions or a missing gateway route stay as release blockers.

## 13. Ready-for-dev Checklist

- [x] `AGENTS.md` read.
- [x] `domain-config.md` read (PSS ownership + MVP: approvals-only, BPMN deferred).
- [x] **DCP-002 module-identity gate checked** — registry row for `MOD-0023` exists and matches canonical name;
  `verify_module_id.py` run green. **CONTROLLED GATE — PASSED.**
  - Evidence (2026-06-23): `python .antigravity/scripts/verify_module_id.py . --check-id MOD-0023 --name "Workflow Designer (Approvals/SLAs/Escalations)"`
    → exit code `0`, output `OK  MOD-0023: proven against Blueprint/registry.` (Python 3.12.10, openpyxl 3.1.5).
- [x] Stale folder references removed (`Aggregates/`, `src/Backend/`, `Diten.Platform.Persistence`,
  `Diten.WebAPI.csproj`, `Diten.WebUI.csproj`).
- [x] Repo scope bounded to live Platform structure.
- [x] Protected paths explicit.
- [x] Acceptance criteria testable (per-batch).
- [x] Frontend excluded from Batch 01 (and all backend batches).
- [x] SignalR deferred unless approved (projection-only if ever added).
- [x] AuthService permission seed marked as a **separate MOD-0018 / security task**.
- [x] Gateway route marked as a **separate integration-agent task**.
- [x] User reviewed and set `status: ready-for-dev` (explicit approval 2026-06-23).

## 14. Exact Next Implementation Prompt (Batch 01 — do NOT execute yet)

> **Prompt — MOD-0023 Batch 01 (Data foundation), backend only.** Only run after the user sets the pack
> `status: approved` / `ready-for-dev`.
>
> ```
> @orchestrator execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md
>
> Implement MOD-0023 Batch 01 (Data foundation) only — backend only, no frontend, no gateway edit, no
> AuthService edit.
>
> Scope:
> - Add Domain entities under services/Diten.Platform/src/Diten.Platform.Domain/Entities/Workflow/:
>   WorkflowTemplate, WorkflowInstance, ApprovalTask, WorkflowTransitionLog. Each inherits
>   TenantScopedEntity (from Diten.Platform.Common). Add needed enums under Domain/Enums/Workflow/.
>   No new base entity/repository/tenant/correlation/audit/event/job infrastructure.
> - Add repository interfaces (Domain/Repositories/IWorkflow*.cs) and implementations
>   (Infrastructure/Persistence/Repositories/WorkflowRepositories.cs) inheriting TenantRepository<T> with
>   collections: workflow_templates, workflow_instances, approval_tasks, workflow_transition_logs.
> - Add tenant-first indexes in Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs
>   (workflow_templates {TenantId, TemplateCode} unique non-deleted; tenant-first list indexes for the rest).
> - Register repositories in the existing Infrastructure DI registration file.
> - Add Application/Features/Workflow/ CQRS for WorkflowTemplate create + read only:
>   CreateWorkflowDefinitionCommand, GetWorkflowDefinitionByIdQuery, GetWorkflowDefinitionListQuery,
>   their sealed handlers (CommandHandlers/ + QueryHandlers/, no Command/Query suffix),
>   CreateWorkflowDefinitionValidator, and WorkflowModels.cs. Use Response<T>, ICorrelationContext.
> - Add a thin WorkflowDefinitionsController : CustomBaseController with POST/GET api/v1/workflow/definitions
>   and GET api/v1/workflow/definitions/{id}; [Authorize] + [HasPermission] using the lowercase keys
>   platform.workflow.definitions.manage / platform.workflow.definitions.view (define constants locally;
>   do NOT seed in AuthService).
> - Add tests under services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/.
>
> Golden flow to prove: create workflow template -> persist tenant-scoped -> reload by id -> list for same tenant.
> Failure path to prove: cross-tenant read returns not-found / empty list with no metadata leak;
> client-supplied TenantId cannot override tenant context; duplicate TemplateCode in same tenant blocked.
>
> Do NOT: touch ocelot.json, touch Diten.AuthService, add frontend, add escalation/SLA/instance/approval
> command logic (later batches), or create new shared infrastructure. Build Diten.Platform.API and run the
> Workflow tests at the end.
> ```

## 15. Deprecated / Stale Guidance Removed

| Stale guidance (from `MOD-0023-workflow-designer.md`) | Replacement |
|---|---|
| `Domain/Aggregates/MOD-0023-workflow-designer/` | `Domain/Entities/Workflow/**` (live convention) |
| `Diten.Platform.Persistence/Repositories/...` | `Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs` |
| `src/Backend/Diten.Application/Handlers` | `Diten.Platform.Application/Features/Workflow/Handlers/**` |
| Build target `Diten.WebAPI.csproj` | `Diten.Platform.API.csproj` |
| Build target `Diten.WebUI.csproj` | `frontend/Diten.Web/Diten.Web.csproj` |
| `MOD-0023-workflow-designer` as module ID | Canonical ID is `MOD-0023`; `workflow-designer`/`...Controller` are not module IDs (registry note) |
| Module-id-prefixed folder names (`.../MOD-0023-workflow-designer/`) | Feature-named folders (`Features/Workflow/`, `Entities/Workflow/`) |
| Frontmatter-less single-section summary pack | Full live module-pack contract (frontmatter + scope + protected paths + persistence + permissions + batches) |

> Historical note: the prior pack's intent (versioned definitions, pinned instances, permissioned & audited
> approvals, MVP escalations, reusable cross-domain object model) is preserved above; only the stale
> folder/project layout and the module-id-as-folder naming were corrected to live conventions.

## 16. Follow-up Items

1. **MOD-0018 / security:** seed/grant the §8 permission keys in `Diten.AuthService` (separate task).
2. **Integration-agent:** add/confirm the `api/v1/workflow/**` Ocelot route in `ocelot.json` if the existing
   catch-all does not already cover it (separate task).
3. **SignalR projection (deferred):** real-time approval-inbox push as projection-only, only if approved.
4. **BPMN / visual builder:** explicitly out of MVP; future wave only.
5. **Registry slug reconciliation:** registry slug is `workflow-designer`; this pack's filename slug is
   `workflow-config-approval-templates`. A registry reconciliation (slug/alias) is a separate governance edit.

## 17. PPM ApprovalOutcome Amendment — DRAFT / NON-EXECUTABLE

> **Amendment status boundary:** This section is a governance proposal with status **DRAFT / NON-EXECUTABLE**.
> The parent pack remains `ready-for-dev` only for its previously approved slices. That parent status does not
> approve, activate or provide runtime authority for any item in this amendment. Promoting this amendment
> requires an explicit PSS-owner/user decision recorded against this section; it does not happen implicitly
> through parent-pack status, implementation of another batch, or approval of a PPM consumer amendment.

### 17.1 Identity and authority

- This amendment remains inside canonical parent `MOD-0023 — Workflow Designer
  (Approvals/SLAs/Escalations)`; it creates no MOD, FU, child or runtime identity.
- MOD-0023 remains the sole authoritative source of approval workflow execution and terminal approval truth.
- The governing references are [MOD-0018](MOD-0018-rbac-abac-authorization.md),
  [MOD-0021](MOD-0021-general-audit-trail.md),
  [MOD-0117](../../portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md) and
  [DCP-006](../../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md).

### 17.2 Purpose and non-authority

The proposed slice lets an authenticated PPM submitting service request approval for one InvestmentCase and,
after a terminal MOD-0023 decision, retain only a minimal opaque outcome reference. It does not authorize code,
schema, endpoint, event, UI, Gateway, WorkCenter or deployment changes. It does not expand any existing
ready-for-dev batch.

### 17.3 Exact source-reference contract

The request source profile is exact and case-sensitive:

| Item | Exact value |
|---|---|
| Contract name | `ppm.investment-case-context` |
| Contract version | `1.0` |
| Field allowlist | `ContractName`, `ContractVersion`, `InvestmentCaseId` |

`InvestmentCaseId` is a canonical non-empty opaque UUID. Unknown fields are rejected. In particular,
`TenantId`, `ActorId`, `ApproverId`, module/object type, free-text discriminator, task, route, comment,
assignee or workflow payload fields are forbidden. MOD-0023 validates the typed source reference but owns no
PPM aggregate, lifecycle, visibility rule or relationship cardinality.

### 17.4 Trusted provenance

Tenant, submitting-service identity, effective actor and delegated-actor provenance come only from validated,
authenticated trusted server context. Decision actor and decision time come only from the authoritative
MOD-0023 transition context and server clock. Client-supplied tenant/actor/approver overrides are rejected;
unvalidated or client-controlled override headers and payload fields are never treated as authority. A
validated delegated JWT or allowlisted, authenticated transport metadata may carry trusted context only under
the separately approved S2S/effective-actor mechanism. Missing or invalid trusted context fails closed.

Before submission, the authenticated PPM service must produce a trusted, request-bound attestation that its
authoritative tenant-scoped lookup found the InvestmentCase active and visible to the effective actor. The
attestation is server context, not a fourth source-reference field, and is stored with provenance. MOD-0023
does not query PPM persistence, call a PPM provider endpoint or infer source truth locally. Missing,
cross-tenant or invisible InvestmentCase returns `404` only inside PPM's authoritative validation boundary;
PPM does not call MOD-0023 after that failure. MOD-0023 never converts a missing, invalid, expired, replayed or
mismatched attestation into source-not-found `404`.

The trusted-context attestation contract is exact `ppm.investment-case-source-attestation` / `1.0`, issued and
integrity-protected by the authenticated PPM service for the MOD-0023 audience. Its closed claims are
`ContractName`, `ContractVersion`, `TenantId`, `SubmittingServiceId`, `EffectiveActorId`,
`DelegatedActorChain`, `InvestmentCaseId`, `WorkflowTemplateVersionId`, `SubmissionRequestDigest`,
`IssuedAtUtc`, `ExpiresAtUtc`, `Nonce`. These claims exist only in verified server context and never widen the
three-field source payload. MOD-0023 verifies issuer, audience, integrity, exact claim equality, nonce replay
protection and a short owner-approved validity window. Malformed attestation shape or claims return `400`;
authentication, issuer, audience or integrity failure returns `401`; authenticated S2S/submission authorization
denial returns `403`; expired, replayed or mismatched attestation returns `400`; verifier/key/freshness
dependency unavailable, malformed authoritative verification or indeterminate verification returns `503`.
The exact signing mechanism, maximum validity window and nonce-retention period remain promotion blockers and
must be fixed in bilateral fixtures before execution.

### 17.5 Immutable template-version pin

Submission names one exact immutable `WorkflowTemplateVersionId`; mutable template identity, `latest`, current
published-version lookup or any later re-resolution is forbidden. Before accepting the submission, MOD-0023
must prove that the pinned version is in the authenticated tenant, tenant-visible, published, immutable and
eligible for exact source profile `ppm.investment-case-context` / `1.0`. A later template publication cannot
change an accepted instance's meaning.

### 17.6 MOD-0018 eligibility and delegation boundary

[MOD-0018](MOD-0018-rbac-abac-authorization.md) is authoritative for effective approver eligibility and every
delegation decision. MOD-0023 consumes an allow/deny decision bound to tenant, effective actor, action,
resource/instance and decision freshness; it does not infer authority from role names, locally count grants,
reconstruct effective permissions or use a fail-open/stale cache. Deny is `403`; unavailable, timed-out,
malformed or indeterminate dependency results are `503`. PPM never computes eligibility or delegation.
Before creating an instance, MOD-0023 also requires a distinct fresh MOD-0018 allow decision bound to tenant,
submitting service, effective/delegated actor, exact `SubmitForApproval` action, InvestmentCaseId and pinned
WorkflowTemplateVersionId. Authentication, S2S entitlement or source visibility alone cannot substitute for
this submission authorization.

### 17.7 Submission idempotency

Each submission carries an idempotency key of 1–128 visible ASCII characters and a canonical request digest.
UUIDs use canonical lowercase `8-4-4-4-12` text; strings are Unicode NFC; object member names use the exact
case in this section. The digest input is the RFC 8785 JSON Canonicalization Scheme representation of the
closed allowlist `WorkflowTemplateVersionId`, `ContractName`, `ContractVersion`, `InvestmentCaseId`, encoded
as UTF-8; the digest is SHA-256 encoded as 64 lowercase hexadecimal characters. Unknown members and
noncanonical UUIDs fail `400`. Trusted provenance is not client digest input, but its tenant,
submitting-service, effective-actor and complete delegated-actor chain are equality-bound to the receipt and
must match on replay. Receipt lookup and the unique constraint are scoped exactly to tenant,
submitting-service and idempotency key; actor/delegation are stored comparison fields, not index-scope fields:

- same key + same digest returns the original stable result without another instance or mutation;
- same key + different digest returns `409` with no mutation;
- key comparison and receipt creation are race-safe and protected by a unique persistence constraint.

Receipts are retained at least as long as the workflow instance and its terminal outcome reference can
be resolved; expiry or reuse cannot make a previously accepted key produce a second instance.

### 17.8 Submission atomicity and unknown-commit recovery

The idempotency receipt, pinned template-version identity, exact source reference, authenticated provenance and
new workflow instance are committed in one local transaction. No success is returned before commit. If the
caller loses the response or commit acknowledgement is unknown, it retries the same key and canonical bytes;
MOD-0023 resolves the durable receipt and returns the original result. A missing receipt after an authoritative
rollback permits a fresh attempt; a key is never reused with changed bytes or different trusted provenance.

### 17.9 Terminal outcome ownership

Approve or Reject becomes authoritative only through a valid terminal MOD-0023 transition. That transition
creates one immutable `ApprovalOutcome`; PPM does not create, update or reinterpret the result. In v1, each
`WorkflowInstance` produces exactly one terminal outcome. A terminal instance cannot produce another outcome.

### 17.10 Exact `ApprovalOutcomeReferenceV1` contract

The typed contract is named **`ApprovalOutcomeReferenceV1`**. Its exact case-sensitive wire allowlist is:

| Field | Exact value / meaning |
|---|---|
| `ContractName` | constant `platform.approval-outcome-reference` |
| `ContractVersion` | constant `1.0` |
| `ApprovalOutcomeId` | opaque canonical non-empty UUID allocated by MOD-0023 |

These three fields are sufficient because every terminal outcome has a distinct, immutable opaque identity;
v1 uses neither an outcome version field nor an outcome sequence. Exact serialization contains all three fields once, in the
case shown, and no others. Missing, duplicate or unknown fields are rejected. Unknown contract versions are
rejected. PPM may persist only this reference. MOD-0023 binds every outcome to the authenticated tenant
server-side; dereference/validation requires the same authenticated tenant and returns indistinguishable `404`
for missing, cross-tenant or non-disclosable MOD-0023 outcomes.

### 17.11 Immutability and renewed decision

An `ApprovalOutcome` cannot be updated, soft-deleted, hard-deleted, corrected, restated or superseded. If the
business decision must be taken again, the caller starts a new `WorkflowInstance`; only that new instance may
produce its own single immutable terminal outcome. MOD-0023 may link the new instance to the old
instance/outcome using producer-private provenance that is absent from `ApprovalOutcomeReferenceV1`. The old
outcome remains unchanged and historically resolvable. PPM does not automatically replace its old reference;
changing the PPM relationship is a separate MOD-0117 lifecycle mutation under MOD-0117 authority.

### 17.12 Terminal transaction boundary

The terminal workflow transition, immutable `ApprovalOutcome`, terminal idempotency receipt and
producer-local audit intent are written in the same MOD-0023 local transaction. The terminal key uses the same
1–128 visible-ASCII rule as submission. Receipt lookup and uniqueness are scoped by tenant, submitting service,
workflow instance and terminal key; effective actor and complete delegated-actor chain are stored comparison
fields whose mismatch returns `409`. Its closed digest allowlist is
`WorkflowInstanceId`, `Action`, `ExpectedVersion`, `ReasonCode`; `Action` is exactly `Approve` or `Reject`,
`ExpectedVersion` is a positive integer, and `ReasonCode` is required, non-null, never omitted, and must be an
NFC 1–64 character ASCII token from the exact immutable reason-code allowlist carried by the pinned
`WorkflowTemplateVersion`; it is never a comment. It uses the same UUID/NFC, RFC 8785 UTF-8, SHA-256 and
lowercase-hex rules as §17.7. Failure to
persist the audit intent rolls back the transition, outcome and receipt. A duplicate same-key/same-digest
terminal command with equal trusted provenance returns the original outcome; different digest/provenance or
an already-terminal incompatible action returns `409`. Its receipt follows the retention rule in §17.7.

### 17.13 Audit delivery boundary

The local audit intent is the atomic producer record. [MOD-0021](MOD-0021-general-audit-trail.md) delivery is a
fully asynchronous, post-commit, separately approved adapter that consumes it with at-least-once delivery and
idempotent consumption. Submission and terminal mutation do not invoke that adapter. A controller,
handler or local mutation transaction never calls RabbitMQ/MassTransit or a remote MOD-0021 endpoint directly.
If the producer-local audit intent cannot be written in the same local transaction, the entire mutation rolls
back and returns `503` as a local persistence/dependency failure. After commit, MOD-0021 delivery unavailable,
timeout or failure does not roll back or rewrite business truth; it enters the separately approved
retry/DLQ/alarm/replay process.

### 17.14 Exact error matrix

| HTTP | Exact failure classes |
|---:|---|
| `400` | malformed request/profile/mode; malformed attestation shape or claims; expired, replayed or mismatched attestation |
| `401` | missing/invalid authentication or trusted context; attestation authentication, issuer, audience or integrity failure |
| `403` | permission denial; authenticated S2S/submission authorization denial; MOD-0018 approver-eligibility or delegation denial |
| `404` | missing, soft-deleted, cross-tenant or non-disclosable MOD-0023 template, workflow instance or outcome only |
| `409` | stale template; visible but ineligible template state; same idempotency key with different digest; incompatible already-terminal transition |
| `503` | unsupported version; attestation verifier/key/freshness dependency unavailable, malformed authoritative verification or indeterminate result; MOD-0018 timeout/unavailable/malformed/indeterminate result; local transaction/audit-intent persistence failure |

Missing/cross-tenant/invisible InvestmentCase `404` belongs exclusively to PPM's pre-submission authoritative
validation; no MOD-0023 request is made. MOD-0023 attestation failures never become `404`. MOD-0023 responses
never leak whether one of its own `404` targets exists in another tenant. Dependency uncertainty never becomes
allow, deny, missing-object or local truth. Post-commit audit delivery state is excluded from the request
response mapping and cannot cause `503`.

### 17.15 PPM ownership exclusion

PPM must not create or persist `ApprovalTask`, an approval route/state machine, approve/reject commands,
assignee lists, eligibility/delegation calculations, local `Approved`/`Rejected` state, workflow payload,
task/comment/route copies, or decision actor/time. It owns its InvestmentCase and its relationship cardinality
and may retain only the exact `ApprovalOutcomeReferenceV1` tuple.

### 17.16 MOD-0023 ownership exclusion

MOD-0023 must not own or copy the InvestmentCase aggregate, its lifecycle, PPM visibility rules, PPM
cardinality, or any PPM business payload. Its source link remains exactly the typed source-reference tuple in
§17.3. It owns workflow execution, immutable terminal outcome truth and reference validation only.

### 17.17 Gate 2 and protected surfaces

This governance-only amendment does **not** invoke Gate 2. Gate 2 is mandatory immediately before the first
production change to an `ApprovalTask`, approval route/command/UI, eligibility/delegation runtime,
`MOD-0024` TaskAggregate, WorkCenter projection or DWS execution lifecycle/assignment. This amendment neither
authorizes nor modifies WorkCenter, MOD-0024, MOD-0354/DWS, PPM runtime/frontend, Platform/AuthService runtime,
Gateway or `.antigravity/**`.

### 17.18 Required contract and security tests before promotion

Promotion to executable status requires approved fixtures proving: exact source and outcome field equality;
exact three-field `ApprovalOutcomeReferenceV1` serialization plus missing/duplicate/unknown-field rejection;
unknown-version rejection; immutable template pinning; trusted-context binding; tenant-first MOD-0023
template/instance/outcome `404`; PPM pre-submission source `404` with no MOD-0023 call; source-attestation
shape and signature/issuer/audience/integrity/claim/freshness/nonce error mapping; MOD-0018 submission and approver
deny/timeout/indeterminate handling; role-name/local-grant/fail-open-cache absence; same-key same-digest
replay; cross-actor/cross-delegation replay rejection; same-key different-digest conflict; concurrent duplicate
submission; exact canonical digest vectors; unknown-commit recovery; tenant-bound outcome dereference;
exactly one immutable terminal outcome per instance; renewed decision through a new instance; unchanged
historical outcome resolution; audit-intent rollback with local `503`; post-commit MOD-0021 failure without
business rollback; legacy generic `ObjectRef` endpoint rejection for the PPM profile; and absence of direct
broker calls.

### 17.19 Ownership matrix

| Concern | PPM / MOD-0117 | MOD-0023 | MOD-0018 | MOD-0021 |
|---|---|---|---|---|
| InvestmentCase aggregate, lifecycle, cardinality | Owns | Must not own | No | No |
| Exact source reference | Produces | Validates and stores atomically | No | Audits only via adapter |
| Template/version and workflow execution | No | Owns; exact immutable pin | Eligibility input only | Audits only via adapter |
| Approver eligibility/delegation | Must not compute | Consumes fail-closed decision | Authoritative | No |
| Approve/Reject and terminal outcome truth | Stores reference only | Authoritative | Authorizes actor | Audits only via adapter |
| `ApprovalOutcomeReferenceV1` | May store exact three-field tuple | Owns and validates | No | No |
| Producer-local audit intent | No | Writes atomically with mutation | No | Later idempotent consumer |

### 17.20 Promotion blockers and evidence

This amendment remains blocked from runtime until all of the following are explicit and recorded: PSS
technical/business-owner approval; user runtime authorization; Gate 2 immediately before any protected
production change; exact bilateral serialization/error fixtures with the later MOD-0117 consumer amendment;
an approved trusted S2S/effective-actor mechanism; approved MOD-0018 eligibility/delegation integration;
approved attestation signing/validity/nonce fixtures; approved MOD-0021 adapter contract; local transaction
feasibility for receipts, outcome and audit intent; and
green tests from §17.18. Before promotion, the PPM slice must explicitly override/reconcile the parent pack's
generic `ObjectRef`, local `RuntimeAssignmentSnapshot` authority and B05/B06 wording for this profile only;
those earlier clauses remain unchanged for previously approved non-PPM scope but cannot authorize a PPM path.
Parent `ready-for-dev` status closes none of these blockers.

## 18. ProcessModelVersion ApprovalOutcome Amendment — DRAFT / NON-EXECUTABLE

> **Amendment status boundary:** This section is a governance-only, **DRAFT / NON-EXECUTABLE** source-profile
> contract. The parent pack remains `ready-for-dev` only for its previously approved slices. This amendment
> authorizes no DTO, provider/client, repository, endpoint, route, configuration, signing key, secret, fixture,
> migration, deployment, listener, worker, Gateway, frontend, WorkCenter or production activation. Promotion
> requires separate bilateral fixture evidence and explicit PSS-owner/user runtime authority.

### 18.1 Identity and authority

- The amendment remains inside canonical parent `MOD-0023 — Workflow Designer
  (Approvals/SLAs/Escalations)` and creates no MOD, FU, child or runtime module identity.
- MOD-0023 remains the sole authority for workflow-template versions, workflow execution and immutable terminal
  `ApprovalOutcome` truth.
- `MOD-0355 — Business Process Architecture & Modeling` remains authoritative for
  ProcessModel/ProcessModelVersion identity, lifecycle, content hash and source
  visibility. MOD-0023 neither queries nor copies the ProcessModel aggregate.
- MOD-0018 remains authoritative for authenticated S2S, effective/delegated actor and approver eligibility.
  MOD-0021 remains the later audit-delivery authority.

### 18.2 Purpose and non-authority

The proposed profile lets an authenticated `Diten.ManagementGovernanceService` submit one exact, visible
ProcessModelVersion to a tenant-owned MOD-0023 workflow and later retain only an opaque
`ApprovalOutcomeReferenceV1`. It does not make MOD-0355 publish executable, select a production template, grant
permission, provision a credential, activate a provider or widen the parent runtime batches.

### 18.3 Exact source-reference contract

The source profile is exact, ordinal and case-sensitive:

| Item | Exact value |
|---|---|
| Contract name | `management-governance.process-model-version-context` |
| Contract version | `1.0` |
| Field allowlist | `ContractName`, `ContractVersion`, `ProcessModelId`, `ProcessModelVersionId`, `ContentHash` |

`ProcessModelId` and `ProcessModelVersionId` are canonical non-empty opaque UUIDs. `ContentHash` is exactly
`sha256:` followed by 64 lowercase hexadecimal characters and must equal the current authoritative MOD-0355
version hash. Missing, duplicate, unknown, case-changed or aliased fields fail closed. Tenant, actor, author,
requester, approver, template, task, route, comment, workflow payload and mutable model content are forbidden
source-reference fields.

### 18.4 Trusted source validation

Before submission, MOD-0355 performs its authoritative tenant-scoped lookup and visibility/lifecycle checks for
the exact model, version and hash. Missing, soft-deleted, cross-tenant or non-disclosable source returns `404`
inside MOD-0355 and causes zero MOD-0023 calls. MOD-0023 never infers source existence from UUID syntax, a cache,
an audit record or a previously accepted outcome. A source mismatch, stale hash or ineligible visible lifecycle
is not converted into source-not-found.

### 18.5 Tenant-scoped immutable template provenance

MOD-0023 owns a tenant-scoped source-profile mapping keyed by the exact contract name/version above. The mapping
resolves one exact, already-published immutable `WorkflowTemplateVersionId`. Mutable template identity,
`latest`, current-version lookup, client choice, request-payload override and fallback to another profile or
template are forbidden.

The resolved UUID is trusted server context, not a sixth source-reference field. Before the submission
transaction commits, MOD-0023 revalidates that the same mapping still resolves the same tenant-visible,
published, immutable and profile-eligible version. A changed/stale mapping or template returns `409`; an
unavailable, malformed or indeterminate mapping authority returns `503`. The accepted instance pins the exact
UUID permanently, and later template publication cannot reinterpret it.

### 18.6 Exact source-attestation identity

The source attestation is a separate token/contract family:

| Item | Exact value |
|---|---|
| Contract name | `management-governance.process-model-version-source-attestation` |
| Contract version | `1.0` |
| JWT `typ` | `diten-process-model-version-source-attestation+jwt` |
| Algorithm | `RS256` only |
| RSA size | minimum 3072 bits |
| Issuer | `diten.management-governance` |
| Audience | `diten-platform-service` |
| Protected key selector | one exact non-empty trusted `kid` |

`none`, HS256, other asymmetric algorithms, missing/unknown/duplicate/retired `kid`, algorithm negotiation and
token-provided key locations are rejected. This governance decision allocates no production `kid`, public/private
key, secret, JWKS endpoint, vault location or credential.

### 18.7 Exact attestation claims

The verified claim allowlist is exactly:

`ContractName`, `ContractVersion`, `TenantId`, `SubmittingServiceId`, `EffectiveActorId`,
`DelegatedActorChain`, `ProcessModelId`, `ProcessModelVersionId`, `ContentHash`,
`WorkflowTemplateVersionId`, `SubmissionRequestDigest`, `IssuedAtUtc`, `ExpiresAtUtc`, `Nonce`.

`SubmittingServiceId` is exact `diten.management-governance`. Tenant, effective actor and delegated chain come
only from validated server context. `DelegatedActorChain` preserves the authoritative ordered chain and is empty
only for a non-delegated request. The model/version/hash tuple equals the source reference byte-for-byte, and
`WorkflowTemplateVersionId` equals the server-resolved mapping. Unknown, duplicate, missing, null, empty,
case-changed or type-changed claims fail closed. No client payload/header can override a trusted claim.

### 18.8 Hard freshness and replay contract

`ExpiresAtUtc - IssuedAtUtc` is positive and at most exactly 30 seconds. Validation uses zero clock skew:
future-issued or expired tokens are rejected without grace or last-known-good behavior. `Nonce` is non-empty,
single-use and retained through exactly `ExpiresAtUtc + 30 seconds`; it cannot be purged or reused earlier.
Replay, expired/future time, claim mismatch or request-digest mismatch creates no workflow instance, receipt,
task, outcome or audit intent.

### 18.9 Request binding

`SubmissionRequestDigest` binds the exact immutable template-version UUID and exact five-field source reference
under a versioned canonical request projection. Tenant, submitting service, effective actor and full delegated
chain are trusted equality-bound receipt fields and must also match on replay. The digest is lowercase 64-hex
SHA-256 over UTF-8 RFC 8785 canonical JSON; arbitrary DTO serialization, property aliases and client-supplied
trusted context are forbidden. The normative byte fixture remains a bilateral-evidence blocker and is not
invented by this amendment.

### 18.10 MOD-0018 submission and decision authority

Before instance creation, MOD-0023 consumes a fresh MOD-0018 decision bound to tenant, submitting service,
effective/delegated actor, exact submit action, ProcessModelId, ProcessModelVersionId, ContentHash and pinned
WorkflowTemplateVersionId. Approve/Reject separately requires a fresh actor-eligibility/delegation decision
bound to the exact instance/action. Role names, local grants, cached allow and source visibility cannot replace
either decision. Explicit denial is `403`; unavailable, timed-out, malformed or indeterminate authority is
`503`.

### 18.11 Submission idempotency and atomicity

The exact source reference, pinned template-version identity, verified attestation provenance, workflow
instance and idempotency receipt commit in one MOD-0023 local transaction. Same tenant/service/key plus equal
digest and trusted provenance returns the original stable result. Changed digest, actor/delegation, source tuple,
hash or template identity returns `409`. Unknown commit uses same-session commit-only retry and durable receipt
reconciliation; blind body replay and partial commit are forbidden.

### 18.12 Terminal outcome ownership and binding

Only a valid final MOD-0023 transition can create one immutable terminal `ApprovalOutcome`. Server-private
provenance binds it to the authenticated tenant, exact ProcessModelId, ProcessModelVersionId, ContentHash,
pinned WorkflowTemplateVersionId, final state, decision actor and decision time. MOD-0355 consumes validation of
that binding; it never manufactures, updates, reinterprets or locally restates approval truth. A renewed
decision uses a new workflow instance and new outcome while historical outcomes remain immutable.

### 18.13 ApprovalOutcomeReferenceV1 remains exact three fields

The shared reference is unchanged:

| Field | Exact value / meaning |
|---|---|
| `ContractName` | constant `platform.approval-outcome-reference` |
| `ContractVersion` | constant `1.0` |
| `ApprovalOutcomeId` | opaque canonical non-empty UUID allocated by MOD-0023 |

There is no fourth field. Tenant, source tuple, hash, template version, decision actor/state/time and workflow
payload remain producer-private validation context. Missing, duplicate, unknown or unsupported fields/versions
are rejected. Same-tenant dereference returns indistinguishable `404` for missing, cross-tenant or
non-disclosable outcomes.

### 18.14 Exact failure matrix

| HTTP | Exact failure classes |
|---:|---|
| `400` | malformed source/profile/request/attestation claims; invalid UUID/hash/digest; expired, future-issued, replayed or mismatched attestation |
| `401` | missing/invalid authentication or trusted context; wrong issuer/audience/typ/alg/kid; bad signature |
| `403` | authenticated submission/permission/eligibility/delegation denial; final outcome is not approved |
| `404` | MOD-0355 source missing/invisible before any MOD-0023 call, or same-tenant MOD-0023 template/instance/outcome missing/invisible inside its authority |
| `409` | stale/changed source hash or lifecycle; changed template mapping; ineligible visible template; idempotency/provenance conflict; incompatible terminal transition |
| `503` | source/template/auth/key/freshness authority unavailable, timed out, malformed or indeterminate; unsupported authoritative version; local transaction/audit-intent failure |

Uncertainty never becomes allow, deny, `404`, default template or cached truth. Every pre-commit failure leaves
zero new workflow, receipt, task, outcome and audit-intent residue.

### 18.15 Ownership and no-copy boundary

MOD-0355 owns model/version/hash/lifecycle and whether its publish policy requires approval. MOD-0023 owns the
tenant profile mapping, immutable template version, workflow execution, tasks and terminal outcome. MOD-0018
owns S2S/effective actor/eligibility/delegation. MOD-0021 owns later immutable audit acceptance. Neither side
copies the other's aggregate, mutable content, workflow graph, assignment list, comments, local approval state
or permission truth.

### 18.16 Profile isolation and PPM regression boundary

This profile is distinct from `ppm.investment-case-context` / `1.0`. Source-reference names, fields,
attestation family, issuer, request digest, template mapping, receipts and fixtures cannot be reused across the
two profiles. The PPM amendment's exact three-field source reference and its unresolved signing/validity choices
remain unchanged. Both profiles share only the exact three-field `ApprovalOutcomeReferenceV1` owned by
MOD-0023. No PPM runtime status or authority is promoted by this amendment.

### 18.17 Audit, Gate 2 and protected surfaces

The terminal transition/outcome/receipt and producer-local audit intent remain one local transaction. MOD-0021
delivery is asynchronous and separately approved; direct broker or remote audit calls inside the mutation are
forbidden. This governance-only amendment does not itself invoke Gate 2. Gate 2 and explicit runtime authority
are mandatory before protected approval runtime/UI/task/eligibility changes. WorkCenter, MOD-0024, MOD-0354,
Gateway, frontend, Platform/AuthService runtime and `.antigravity/**` remain unchanged.

### 18.18 Required bilateral fixtures before promotion

Promotion requires producer-owned immutable fixtures proving the exact source bytes, five-field schema,
ContentHash binding, template mapping/pin/revalidation, exact attestation header/claim set, issuer/audience,
RS256/RSA-size/exact-kid enforcement, hard 30-second zero-skew boundary, nonce retention/replay, canonical
request digest, tenant/effective/delegated actor binding, MOD-0018 deny/unavailable matrix, same-key replay and
changed-provenance conflict, unknown-commit recovery, terminal approved/rejected outcomes, exact three-field
reference serialization, renewed-decision immutability, audit-intent rollback and absence of direct broker
calls. Test-only identities/keys must be rejected by production composition.

### 18.19 Promotion blockers and readiness

This amendment remains **DRAFT / NON-EXECUTABLE** until PSS and MOD-0355 owners approve immutable bilateral
fixtures; MOD-0355 FU16 onboarding and exact submit/eligibility permissions are accepted; a production template
mapping is provisioned without client choice; attestation and outcome adapters pass compatibility/security
tests; local transaction feasibility is proven; MOD-0021 delivery is separately approved; Gate 2 is satisfied
where required; and explicit runtime plus activation authority is recorded. Parent `ready-for-dev` status,
MOD-0355 contract-test readiness or a test-only fixture closes none of these blockers.

### 18.20 Follow-up and implementation prohibition

- Author exact canonical source/request/attestation/outcome fixtures in owner worktrees after this governance
  profile is approved; do not copy or regenerate another profile's bytes.
- Record exact test-only signing identity and key ID separately; never create a production key/secret here.
- Reconcile MOD-0355, MOD-0018 and MOD-0021 bilateral packs before any executable adapter.
- Until then no source DTO, provider/client, DI registration, persistence field, endpoint, configuration,
  credential, listener, route or positive runtime fixture may be created from this amendment.

### 18.21 B-05b immutable bilateral fixture authority — NON-RUNTIME / CONTRACT-TEST ONLY

The PSS owner/user has explicitly authorized the immutable **B-05b** bilateral fixture and contract-test
evidence slice for the ProcessModelVersion profile. This authorization does not promote §18 from
**DRAFT / NON-EXECUTABLE**, does not make any fixture a runtime input and grants no production or activation
authority. For this evidence slice only, the final prohibition in §18.20 is narrowed to permit files in the
following exact allowlist:

- `execution/domains/platform-shared-services/module-packs/fixtures/MOD-0023/process-model-version-approval/v1/**`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/ProcessModelVersionApprovalFixtureContractTests.cs`

The fixture root may contain only immutable JSON payloads, lowercase SHA-256 sidecars and one closed evidence
manifest for the exact source reference, canonical submission request, source-attestation header/claims,
tenant-scoped immutable template-version mapping/pin, terminal outcome binding and exact three-field
`ApprovalOutcomeReferenceV1`. Contract tests may validate positive and negative tenant/source/version/hash,
mapping/pin, no-copy and terminal behavior. All fixture identities are test-only and non-production.

This authority explicitly forbids production DTOs, provider/client or adapter implementations, persistence,
DI/composition, endpoints, configuration, production template mapping, signing/JWKS/key/secret material,
credentials, listener/worker/broker behavior, migration, deployment, Gateway, frontend, WorkCenter and
activation. The fixture/test slice must not claim signature verification, nonce persistence, transaction
feasibility, live provider compatibility or runtime acceptance. Any need outside the exact allowlist fails
closed and requires a later amendment. B-05b remains open until the immutable files and contract tests are
green; even then runtime and activation blockers in §18.19 remain open.
