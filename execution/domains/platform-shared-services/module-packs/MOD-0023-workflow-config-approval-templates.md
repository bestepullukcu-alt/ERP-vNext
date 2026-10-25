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

### WP-0023-FX note (2026-10-07, agent result — pending CT)

Owner decision 2026-10-07 (MOD-0175 CT-Q2): three runtime defects fixed on the MVP-1 branch. Evidence:
`docs/records/audits/2026-10/mod-0023-fx-evidence-2026-10-07.md` (probe source in `mod-0023-fx-scripts-2026-10-07.md`).

- **Four eyes (SOD-003) for tenant users.** `StartedBy` held the e-mail (`ActorName`) while approvers are compared by
  user id, so "submitter cannot approve" never fired. The instance now stores `StartedByUserId` (JWT `sub`) and the check
  compares ids; instances started before the fix use their Start log row (always the starter's id). Applies to the API and
  WorkCenter paths alike (same handler).
- **Actor binding.** `tasks/{id}/approve|reject|delegate|request-info|cancel` took the acting person from the body; the
  body actor must now be empty or name the authenticated caller (id or own e-mail), else 403 `WORKFLOW_ACTOR_MISMATCH`.
  In-process callers (WorkCenter dispatcher, Tasks services) already pass the server-resolved id.
- **Runtime key scope.** `instances.start`, `instances.view`, `tasks.approve/reject/delegate/request-info/cancel` are
  declared as Tenant API permissions (no longer actions of `/Platform/Workflow` pages) and moved PlatformAdmin → Tenant by
  AuthService's allowlisted, audited correction; design / publish / SLA / escalation / transition-gate keys stay
  PlatformAdmin. A tenant administrator can grant the runtime keys to any tenant role (e.g. QA).
- Follow-ups: FU-0023-FX-WEB (remove the free-text "Actor Id" field from the `/Platform/Workflow` task forms),
  FU-0023-FX-SOD-SWITCH (per-template SoD switch, not built).

## 17. TRM-15 — retention policy, additive reconciliation read fields, same-key race answer (proposal, not approved)

**Status of this amendment:** `proposed` — authored by the Treasury Intake Control Tower under owner decisions D9 and
D21/1–4 (`docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md`; the repository owner, K1, answered the
questions sent to the platform code owner). It does **not** change this pack's frontmatter/status and authorizes no code
until it passes CT-APR-02 and CT-APR-01. TRM-15 is closed only when §17 is approved, AC-W1…W12 have **passed** (running
a test is not passing it), the Diten.Platform build is green and every AC-W3 consumer result is bound to that
consumer owner's evidence. **If AC-W5…W12 fail against today's write path, §17 stays blocked**: §17 authorizes no
write-path change (owner decision D22/2); the needed fix is scoped in a separate §17 revision with its own approval,
and the Treasury re-issue gate stays closed until then. Queue: `treasury-intake` TRM-15. The policy decisions
below do not prove that today's code behaves this way; the ACs do.

### 17.1 Why

Diten.FinanceTreasuryService (MOD-0126, MOD-0119, MOD-0120, MOD-0124) starts MOD-0023 instances and records
transitions with deterministic idempotency keys. After an unknown outcome its Reconcile must (a) find **its own**
instance / transition by key, not "the newest one for the object", and (b) rely on the key staying claimed forever
before it re-issues with the same key. Today neither is guaranteed by contract.

### 17.2 Current code truth (@ef4f3a8bc; Diten.Platform identical to origin/main)

- Entities carry the keys: `WorkflowInstance.IdempotencyKey` (`WorkflowInstance.cs:30`), `WorkflowTransitionLog`
  `ApprovalTaskId` / `Action` / `IdempotencyKey` (`WorkflowTransitionLog.cs:12-22`).
- Unique partial indexes `ux_workflow_instances_tenant_idempotency_key_active` and
  `ux_workflow_transition_logs_task_action_idempotency_active` (`PlatformSchemaManifest.WorkflowWorkCenter.cs:118-131,
  211-225`), both filtered `IsDeleted = false`.
- No delete path and no `ExpireAfter` for these two types; the only workflow soft delete
  (`WorkflowRepositories.cs:961-974`) is `SlaEscalationRule`.
- Reads do **not** expose the keys: `WorkflowInstanceStatusDto` (`WorkflowModels.cs:200-207`: Id, Status, Outcome,
  Stage, Step, StartedAt, CompletedAt) used by `GET instances/by-objects`, which hides deleted rows
  (`GetWorkflowInstancesByObjectsHandler.cs:51`); `WorkflowInstanceDto` (`:406-424`, `GET instances/{id}`);
  `WorkflowInstanceHistoryEntryDto` (`:451-465`, `GET instances/{id}/history`: SequenceNo, Action, Actor…, no task id,
  no key).
- Same-key behaviour: sequential replay returns the stored start (`StartWorkflowInstanceHandler.cs:76-85`) and stored
  transitions (`RejectWorkflowTaskHandler.cs:79-80`, `WorkflowTaskTransitionSupport.cs:61, 381, 539`). Concurrent same-key
  starts: the loser re-reads after a write conflict and answers as the winner's replay when the start is whole, 503
  "replay the same key" when it is not whole yet, 409 when the instance left Active
  (`StartWorkflowInstanceHandler.cs:300-330, 700-716`); terminal transitions follow the same pattern
  (`WorkflowTerminalTransitionWriter.cs:112-114, 357-…`). Mongo tests exist in the Platform suite
  (e.g. `WorkflowCandidateDecisionMongoTests*`, `TrustedWorkflowStartRecoveryMongoTests`); whether they cover concurrent
  **same-key** start and transition was **not measured**.

### 17.3 Change

1. **Retention policy (D21/1):** `WorkflowInstance` and `WorkflowTransitionLog` are never deleted (no hard delete, no
   soft delete, no TTL, no archive-and-remove). Written into this pack; guarded by an architecture test (AC-W1).
2. **No deleted-distinguishing read (D21/2):** not added; the policy plus AC-W1 replace it. Consumers keep their rule:
   a deleted row that is nevertheless seen means "do not re-issue".
3. **Additive read fields (D21/3)** — trailing optional members, the existing "additive, trailing" pattern:
   - `WorkflowInstanceStatusDto` + `string? IdempotencyKey = null` (`by-objects`);
   - `WorkflowInstanceDto` + `string? IdempotencyKey = null` (`instances/{id}`);
   - `WorkflowInstanceHistoryEntryDto` + `Guid? ApprovalTaskId = null`, `string? IdempotencyKey = null` (`history`).
   Values come from the entity unchanged (null when the write had no key). Same permission (`instances.view`), same
   tenant filter, same 404, same routes; nothing removed or renamed; property order is not part of the contract. The key
   is not a secret (deterministic for Treasury; caller-chosen otherwise) and is returned only to callers that may
   already view the instance.
4. **Same-key answer (D21/4, clarified by D22/3) — target behaviour, measured against today's code:**
   | Case | Final answer (after any replay) | Today's code (@ef4f3a8bc, read, not run) |
   |---|---|---|
   | two concurrent starts, one key, same facts and starter | both **200**, same instance id; one instance row | re-read after write conflict (`StartWorkflowInstanceHandler.cs:300-330, 700-716`) |
   | replay after the first start was **whole**, whatever the instance became since (approved, rejected, cancelled, closed) | **200**, same instance id, current status — never a new refusal | `ExistingStartResponseAsync` returns the whole start "whatever has become of the instance since" (`:372-391`) |
   | replay of a start that was **never whole** and was closed by the abandoned-start recovery | **409** `WORKFLOW_INSTANCE_START_CONFLICT` — a defined *closed* answer, not a business refusal; Treasury closes the intent only from the recovery's close record (`Cancelled`), otherwise `Unknown` | `:393-398` |
   | same key, different facts or a different starter | **409** `WORKFLOW_INSTANCE_START_CONFLICT` (key misuse) — Treasury never sends this (deterministic keys); treated as `Unknown` + alert | `:419-438` |
   | two concurrent identical transitions (same task, action, key) | both **200**, same transition (one log row) | replay by (task, action, key) (`WorkflowTaskTransitionSupport.cs:61, 381, 539`; terminal writer `WorkflowTerminalTransitionWriter.cs:357-…`) |
   | replay of a transition that was recorded, after the task moved on | **200**, the recorded transition | same lookup |
   | two different decisions on one task (e.g. approve vs reject, different keys) | exactly one terminal decision; the loser gets **409** `WORKFLOW_TASK_INVALID_STATE` — a defined conflict, not a business refusal of the loser's own intent | `WorkflowTaskTransitionSupport.cs:87` (409) |
   An interim **503** "replay the same key" (start or transition not whole yet) is not a final answer: consumers keep
   the intent `Unknown` and replay. A duplicate-key error is never returned as a business refusal.

Allowed paths: `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs`, the three
read handlers (`GetWorkflowInstancesByObjectsHandler.cs`, `GetWorkflowInstanceByIdHandler`, `GetWorkflowInstanceHistoryHandler.cs`)
for the mapping only, `services/Diten.Platform/tests/**`, `tests/architecture/**` (new rule only). No index, schema,
route, permission or write-path change (D22/2) — if the race ACs fail, see the status note above.

### 17.4 Acceptance criteria (defined — not implemented, not run)

- AC-W1 — Retention guard (architecture test): no code path deletes, soft-deletes (`IsDeleted` / `DeletedAt` set) or
  TTL-expires `WorkflowInstance` / `WorkflowTransitionLog`; the schema manifest declares no `ExpireAfter` on their
  collections. Sabotage: adding a `DeleteAsync` or an `ExpireAfter` turns the test red.
- AC-W2 — `by-objects`, `instances/{id}` and `history` return the stored `IdempotencyKey` (and `ApprovalTaskId` on history)
  for keyed writes and `null` for unkeyed ones; other-tenant id → 404 unchanged.
- AC-W3 — JSON compatibility: existing properties keep name, JSON type and meaning; the new ones are added; order not
  asserted. **Consumers (measured @ef4f3a8bc) — each one's own test must pass against a body with the new fields;
  status until run: expected compatibility, not tested:**
  | Read | Consumer | Test |
  |---|---|---|
  | `by-objects`, `history` | Diten.InventoryService `GatewayWorkflowClient.cs:103, 129` (+ `QuarantineWorkflow.cs:183`) | `QuarantineWorkflowTests`, `QuarantineArchitectureTests` (`FakeWorkflowGateway`) |
  | `by-objects`, `history` | Diten.CrmService `GatewayClaimWorkflowClient.cs:109, 236` | `ClaimsWorkflowApprovalTests`, `KnowledgePathReviewTests` |
  | `by-objects`, `history` | Diten.MdmService `GatewayWorkflowClient.cs:110, 237` | **none found** (no test references it) — owner follow-up |
  | `history` | Diten.Web `ClaimsController.V2.cs:280` (MVC proxy) | owner to name; none found by name |
  | all three | Treasury (MOD-0126/0119/0120/0124) | new consumer; its own AC (AC-36/38 family) |
  Measured by `grep` for `workflow/instances` / `instances/by-objects` over `services/` and `frontend/` outside Diten.Platform
  (@ef4f3a8bc); `instances/{id}` (single read) has no consumer outside Platform in this scan. Re-run at code time.
  A Platform build alone is **not** compatibility evidence (gap 11 improvement).
- AC-W4 — Code compatibility: `dotnet build` of Diten.Platform succeeds; construction sites
  (`git grep -n "WorkflowInstanceStatusDto\|WorkflowInstanceDto\|WorkflowInstanceHistoryEntryDto"`) re-scanned at code time;
  optional trailing members keep existing calls compiling.
- AC-W5 — Concurrent same-key **start** on a real Mongo replica set (N ≥ 2 parallel requests, repeated): exactly one
  instance; every final answer (after replaying any 503) names that instance; no answer is a business refusal.
  Sabotage: removing the post-conflict re-read turns it red.
- AC-W6 — **Approve–Approve** replay race: two concurrent approves, same task and key → one transition row, both final
  answers 200 and identical.
- AC-W7 — **Reject–Reject** replay race: same as AC-W6 for reject; and **RequestInfo–RequestInfo** (used by MOD-0119
  RequestInfo and MOD-0126 RequestEvidence) the same.
- AC-W8 — **Approve–Reject conflict** (different keys / actions, one pending task): exactly one terminal decision is
  recorded; the loser's final answer is 409 `WORKFLOW_TASK_INVALID_STATE`; no second terminal row; a replay of the
  loser's own key afterwards returns the same 409 (never 200 for a decision that was not recorded).
  Sabotage for W5–W8: removing the post-conflict re-read or the (task, action, key) lookup turns them red.

Sequential (non-concurrent) replay cases of the §17.3/4 table — separate from the race tests (gap 12):

- AC-W9 — **Replay after a whole start, instance since closed:** start with key K, let the instance reach each terminal
  state in turn (approved, rejected, cancelled by the user, closed by the abandoned-start recovery *after* it was whole);
  replay K with the same facts and starter → **200**, the same instance id, its current status; no second instance; no
  business refusal. Sabotage: answering a non-Active instance with 409 turns it red.
- AC-W10 — **Replay of a recorded transition after the task moved on:** approve (and separately reject, request-info)
  with key K; advance the instance (next step, or terminal); replay the same (task, action, K) → **200**, the same
  transition (same `SequenceNo`), no new log row, no state change. Sabotage: re-evaluating the task's current state
  before the (task, action, key) lookup turns it red.
- AC-W11 — **Same key, different facts or a different starter:** replay K with (a) a different template, (b) a different
  object id / ref, (c) a different candidate set (fingerprint), (d) another authenticated starter → each **409**
  `WORKFLOW_INSTANCE_START_CONFLICT`; the first instance is unchanged and no second one exists. For transitions: the
  same (task, action, K) with a different actor or reason → the defined conflict code measured at code time (no new row).
- AC-W12 — **Never-whole start closed by recovery:** a start that wrote its instance but not its task, then closed by
  `CloseAbandonedWorkflowStart` → replay K → **409** `WORKFLOW_INSTANCE_START_CONFLICT`; the recovery's close record for
  that instance (transition log, key `workflow-start-recovery-close:{instanceId:N}`) is readable through `history`
  with its `IdempotencyKey`, so a consumer can map the 409 to that close evidence (Treasury: `Cancelled` only with that
  record; without it `Unknown`).

### 17.4a Treasury → MOD-0023 action matrix (gap 11/P2)

| Treasury command | Packs | MOD-0023 endpoint | Action | Same-key replay AC | Race AC |
|---|---|---|---|---|---|
| Submit (start) | 0126, 0119, 0120, 0124 | `POST instances` | start | AC-W9, W11, W12 | AC-W5 |
| Approve, ApproveWithSodWaiver (0126) | all four | `POST tasks/{taskId}/approve` | approve | AC-W10, W11 | AC-W6, AC-W8 |
| Reject | all four | `POST tasks/{taskId}/reject` | reject | AC-W10, W11 | AC-W7, AC-W8 |
| RequestEvidence (0126), RequestInfo (0119) | 0126, 0119 | `POST tasks/{taskId}/request-info` | request-info | AC-W10, W11 | AC-W7 |
| Escalate (0126) | — | **none** — no task-level endpoint (only `POST escalations/run`, an SLA job) | — | — | **removed from v1 (D22/1)** |
| (no Treasury command) | — | `delegate`, `cancel` | — | not used by Treasury | — |

Reads used by Reconcile: `GET instances/by-objects`, `GET instances/{id}`, `GET instances/{id}/history` (AC-W2).

### 17.5 Delivery

- Code lands **with MOD-0126 DL-2** (first consumer of the reconciliation reads), as its own commit within the paths
  above; XMC-001 consent is D9 and must be restated in that WP prompt.
- Consumer-side rules stay in the Treasury packs (MOD-0126 / 0119 / 0120 / 0124 Runtime Constraints): match only by
  key (+ task + action for transitions); a read without the key field cannot close an `Unknown` intent.

### 17.6 Out of scope

Deleted-row reads; any write-path or index change; trusted service identity (MOD-0033-FU02); background reconciliation
sweeper; changes to SoD, actor binding or permissions.
