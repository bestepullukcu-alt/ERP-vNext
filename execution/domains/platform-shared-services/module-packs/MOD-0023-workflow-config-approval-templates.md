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
