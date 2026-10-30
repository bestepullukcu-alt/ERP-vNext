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

**Status of this amendment:** revision 2 (§17.1–17.8) was **approved** at pack sha256 `803fbaa5…`
(`execution/approvals/approvals.tsv`, 2026-10-26, §17 only). That row is `execution/approvals/approvals.tsv:16` on
origin/main, but `pack_approval.py check` currently reports the file untrusted (#241), so the approval card computes "no
previous approved version" (`docs/records/reviews/MOD-0023/REVIEW-REQUEST-rev3-01.md:8-9`). **Revision 3 (§17.9, 2026-10-29) is `proposed` and not
approved** — the file no longer has the approved bytes, so no §17 slice is dispatched until revision 3 passes CT-APR-02
and CT-APR-01. Revisions 1–2 were authored by the Treasury Intake Control Tower under owner decisions D9 and
D21/1–4 (`docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md`; the repository owner, K1, answered the
questions sent to the platform code owner); revision 3 is drafted on the MOD-0023 owner's instruction `rev3-yaz`
(2026-10-29). It does **not** change this pack's frontmatter/status and authorizes no code
until it passes CT-APR-02 and CT-APR-01. **Read §17.9, then §17.8, then §17.7 first: revision 3's code-inspection findings, then the producer's review points and D32, then the producer answer and D25, supersede earlier text where they conflict** (revision 2, 2026-10-26; revision 3, 2026-10-29). TRM-15 is closed only when §17 is approved and the §17.9 closure holds:
AC-W1…W18 have **passed** as restated in §17.9 (running a test is not passing it), the Diten.Platform build is green,
every AC-W3 / AC-W17 consumer result is bound to that consumer owner's evidence (MDM included) and each slice is merged
by someone other than its author. **Write path:** a write-path change is allowed only in DL-2, in the MOD-0023 owner's
WP under D25/1 (§17.7/1; files §17.9/14); DL-1's only write-side change is the refusing `DeleteAsync` override
(§17.8/9). The earlier rule "§17 authorizes no write-path change" (D22/2) no longer applies to DL-2. **If an AC fails
and its fix lies outside DL-2's allowed paths, §17 stays blocked**: that fix is scoped in a separate §17 revision with
its own approval, and the Treasury re-issue gate stays closed until then. Queue: `treasury-intake` TRM-15. The policy decisions
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
  (`WorkflowRepositories.cs:961-974`) is `SlaEscalationRule`. **Corrected in §17.8/2:** an inherited
  `RepositoryBase<T>.DeleteAsync` exists for all three types (no caller found).
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
4. **Same-key answer (D21/4, clarified by D22/3) — target behaviour, measured against today's code** (rows for a whole start
   with different facts and for concurrent transitions are **corrected in §17.7/5**):
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
route, permission or write-path change (D22/2) — if the race ACs fail, see the status note above. **Extended by §17.8/9** (repositories for DL-1, write-path files for DL-2).

### 17.4 Acceptance criteria (defined; not implemented or run)

- AC-W1 — Retention guard (architecture test): no code path deletes, soft-deletes (`IsDeleted` / `DeletedAt` set) or
  TTL-expires `WorkflowInstance` / `WorkflowTransitionLog`; the schema manifest declares no `ExpireAfter` on their
  collections. Sabotage: adding a `DeleteAsync` or an `ExpireAfter` turns the test red.
- AC-W2 — `by-objects` and `history` return the stored `idempotencyKey` (and `approvalTaskId` on history) for keyed writes
  and `null` for unkeyed ones; other-tenant id → 404 unchanged. `instances/{id}` + `idempotencyKey` is **optional** (§17.7/4)
  and not asserted by this AC.
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
- AC-W6 — **Approve–Approve** replay race (final expectation per §17.7/5): two concurrent approves, same task and key →
  exactly one transition row; each answer is either 200 (that transition) or 409 `WORKFLOW_TRANSITION_CONFLICT` / 503
  `WORKFLOW_TRANSACTION_UNAVAILABLE` / 503 `WORKFLOW_TRANSITION_CONFLICT`; a replay of the same key after the race returns
  200 with `isIdempotent = true` and the same transition.
- AC-W7 — **Reject–Reject** and **RequestInfo–RequestInfo** replay races (MOD-0119 RequestInfo, MOD-0126 RequestEvidence):
  the same final expectation as AC-W6.
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

### 17.7 Producer answer and owner decisions D25 (2026-10-25) — supersedes §17.1–17.6 where they conflict

Source: `docs/records/audits/2026-10/treasury-intake-handover-review-01/PRODUCER-ANSWER-MOD0023-01.md` (platform code owner,
measured at origin/main @05f311784, read-only, no tests run — producer's statement). Decisions: D25 in
`docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md`.

1. **Delivery (D25/1):** §17 is the specification for the **MOD-0023 owner's WP** (policy + guard, additive read fields,
   fingerprint fix + race tests). The owner writes and merges the code. The §17 rule "no write-path change" (D22/2) does
   not apply to that WP: the fingerprint fix is a write-path change made under the owner's own approval. Treasury slices
   that use MOD-0023 (MOD-0126/0119 DL-2, MOD-0120/0124 DL-3) are not accepted before this WP is on main.
2. **Retention (17.3/1, widened):** the policy covers `WorkflowInstance`, **`ApprovalTask`** and `WorkflowTransitionLog`:
   never deleted, never expired; a change needs an owner decision and prior notice to consumers (Treasury). AC-W1 guards
   all three.
3. **Deleted flag (D25/2 — replaces 17.3/2):** `by-objects` and `history` return `isDeleted`; `by-objects` hides deleted rows
   by default and returns them flagged with `includeDeleted=true`. Treasury Reconcile reads with `includeDeleted=true`; a
   deleted row seen means no re-issue. New AC-W13: `includeDeleted` default unchanged for existing consumers; with
   `includeDeleted=true` a soft-deleted row (test fixture) is returned with `isDeleted=true`.
4. **Read fields (17.3/3, producer's list):** `by-objects` → `idempotencyKey`, `isDeleted`; `history` → `approvalTaskId`,
   `idempotencyKey`, `isDeleted` (`action` already present). `instances/{id}` + `idempotencyKey` was not in the producer's
   list — optional; Treasury does not depend on it (reads use `by-objects` and `history`).
5. **Corrections to the 17.3/4 table (CT's earlier reading was wrong):**
   - *Same key + different facts on a **whole** start:* today **200 + existing instance, facts not compared**
     (`StartWorkflowInstanceHandler.cs:385-391`, producer). The `:419-438` comparison applies only to an **incomplete**
     start. Target after the owner's fix: 409 `WORKFLOW_INSTANCE_START_CONFLICT`, plus an `idempotentReplay` flag on
     replays. AC-W11 fails on today's code and passes only after the fix.
   - *Concurrent identical starts:* loser gets the winner's 200, or a retryable **503 `WORKFLOW_TRANSITION_CONFLICT`** if the
     winner is not whole yet (existing test `Parallel_submits_of_one_key_write_one_task_and_answer_200_or_a_retryable_503`).
   - *Concurrent identical transitions:* the loser usually gets **409 `WORKFLOW_TRANSITION_CONFLICT`** (not 200); without a
     transaction: 200 replay / 409 / 503 `WORKFLOW_TRANSACTION_UNAVAILABLE`. Sequential replay: 200 + `isIdempotent=true`
     (`WorkflowTaskTransitionSupport.cs:1054-1084`). AC-W6/W7 are restated: one transition row; every final answer is
     either 200 (same transition) or 409 `WORKFLOW_TRANSITION_CONFLICT` / 503, and a replay of the same key after the race
     returns 200 `isIdempotent=true`.
   - *Same key + different body on a transition (D25/3):* today only the actor is compared (403); target 409. New AC-W14
     (sequential and concurrent).
6. **Consumer rule (CT, applying D21/7):** for Treasury, 409 `WORKFLOW_TRANSITION_CONFLICT` and 503
   `WORKFLOW_TRANSACTION_UNAVAILABLE` / `WORKFLOW_TRANSITION_CONFLICT` are **never a business refusal** → intent `Unknown`,
   replay the same key or read. 409 `WORKFLOW_TASK_INVALID_STATE` (task decided by another decision, AC-W8) stays a definite
   refusal of the loser's own intent. Never reuse a key for a different intent (producer's warning; Treasury keys are
   deterministic per intent).

TRM-15 closure now needs AC-W1…W14 passed in the MOD-0023 owner's WP, the Platform build green and consumer evidence
(**superseded by §17.8 closure**).

### 17.8 Producer review points and owner decisions D32 (2026-10-26) — supersedes §17.1–17.7 where they conflict

Source: the MOD-0023 owner's six review points on §17 (relayed by the repository owner, 2026-10-26) and owner decisions D32
(`docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md`). Code facts below were **read at origin/main
@dea686c73, not run**.

1. **Author ≠ merger (D32/1):** the MOD-0023 owner writes the WP code; a different person (the K2 approver) merges it by
   merge commit. The same person never writes and merges a write-path change of this WP.
2. **Correction to §17.2 — an inherited delete path exists (read, not run):** `WorkflowInstanceRepository`,
   `ApprovalTaskRepository` and `WorkflowTransitionLogRepository` derive from `TenantRepository<T>`
   (`WorkflowRepositories.cs:141, 452, 798`), which inherits the public `virtual` `RepositoryBase<T>.DeleteAsync`; it
   soft-deletes: it sets `IsDeleted = true` and does **not** set `DeletedAt`
   (`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs:9, 35-46, 70`). The three
   interfaces (`IWorkflowRepositories.cs:41, 191, 371`) declare no delete member. No caller in `Features/Workflow` was found
   by `grep`. Whether any environment holds soft-deleted rows of the three types was
   **not measured** (data, not code): the owner counts `isDeleted: true` rows per environment and records the result in the
   WP. `by-objects` hides deleted rows with `DeletedAt is null` (`GetWorkflowInstancesByObjectsHandler.cs:51`), so a row
   deleted through the inherited method would **not** be hidden — the two deletion markers disagree.
3. **Meaning of `isDeleted` / `includeDeleted` (D32/2):** defensive only. The policy (§17.7/2) is that no row of the three
   types is deleted; the flag exists so that a row deleted before the policy, or outside it, is still seen and a consumer
   does not re-issue. `isDeleted` is read from `IsDeleted`; the deleted filter of every §17 read uses `IsDeleted` (one
   marker), not `DeletedAt` alone.
4. **Retention vs. personal-data erasure (D32/3):** rows are never deleted. On an erasure request (KVKK / GDPR) the
   person-identifying fields are **pseudonymised in place**; the row, its keys (`IdempotencyKey`, `ApprovalTaskId`,
   `Action`), status, `SequenceNo` and timestamps stay. Candidate fields (found by name, @dea686c73): `ApprovalTask`
   `AssigneeRef`, `ActionedBy`; `WorkflowTransitionLog` `ActorId`, `ActorRef`, `Comment`; `WorkflowInstance` `StartedBy`,
   `StartedByUserId`, `DelegatedMakerUserId`. Pseudonymising `StartedByUserId` must keep the same-starter comparison of
   AC-W11 meaningful (the owner states how). The owner confirms the full list in the WP. The erasure procedure itself is **not** built by this WP; it is
   open item **O-S17-1**, pending a legal opinion (owner: repository owner K1 with the legal / data-protection function).
   AC-W1 must not forbid an in-place pseudonymising update.
5. **Consumer check for DL-2 (D32/4):** the DL-2 answer changes (transition: same key + different body 403 → 409; whole
   start: same key + different facts 200 → 409; `idempotentReplay` flag added) are checked against existing consumers the
   way AC-W3 checks DL-1 (AC-W17).
6. **MDM consumer test (D32/5):** no exception. The MDM domain owner writes a consumer test for `Diten.MdmService`
   `GatewayWorkflowClient` (`by-objects`, `history`); AC-W3 and AC-W17 do not pass without it.
7. **`includeDeleted` permission (D32/6):** no new permission; the existing `instances.view`
   (`WorkflowDefinitionsController.cs:121-122`). A deleted row is a row of the same tenant that the caller could already view.
8. **Template codes and step counts** (MOD-0126 O-8, MOD-0119 O-11, MOD-0124 O-20) and the WP delivery date: the MOD-0023
   owner proposes them after this revision is approved (no decision here).
9. **Allowed paths — extends §17.3 (CT-APR-02 rev-2 finding P2):**
   - **DL-1** adds `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs`
     — only an `override` of `DeleteAsync` in `WorkflowInstanceRepository`, `ApprovalTaskRepository` and
     `WorkflowTransitionLogRepository` that refuses (throws, no write), and the deleted filter (`IsDeleted`) of the §17 reads;
     plus the `by-objects` / `history` read handlers under
     `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/` for the
     `includeDeleted` branch. **Not allowed:** `services/Diten.Platform.Common/**` — the shared
     `RepositoryBase<T>.DeleteAsync` stays unchanged for every other module; the three repository interfaces gain no delete
     member.
   - **DL-2** adds the write-path files of the fingerprint fix (start handler, transition support, terminal writer under
     `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/`); the owner names each file in the WP
     prompt before code (D25/1).
   - Both slices keep `services/Diten.Platform/tests/**` and `tests/architecture/**` (new rules only).

New acceptance criteria (defined; not implemented or run):

- AC-W15 — **One deletion marker; inherited delete closed (DL-1):** for the three types the inherited
  `RepositoryBase<T>.DeleteAsync` cannot delete: it is overridden in the three repositories to refuse, guarded by the AC-W1
  architecture test. The test calls delete through the derived repository **and** through a `RepositoryBase<T>` /
  `TenantRepository<T>` reference of the same instance; both refuse and the row is unchanged (`IsDeleted` stays false,
  `UpdatedAt` unchanged). The architecture test also asserts that the three interfaces declare no delete member and that
  `services/Diten.Platform.Common/**` is unchanged by this WP. Every §17 read filters deleted rows by `IsDeleted`. A fixture
  row with `IsDeleted = true`, `DeletedAt = null` is hidden by default and returned with `isDeleted = true` under
  `includeDeleted=true`. Sabotage: removing an override, reverting the filter to `DeletedAt is null`, or adding a delete
  member to an interface turns it red.
- AC-W16 — **`includeDeleted` stays inside the tenant and the permission (DL-1):** with `includeDeleted=true`, another
  tenant's instance id / object → 404 (as without the flag); a caller without `instances.view` → refused as today; no row of
  another tenant appears. Sabotage: dropping the tenant filter on the `includeDeleted` branch turns it red.
- AC-W17 — **DL-2 consumer compatibility:** each AC-W3 consumer (Inventory, CRM, MDM, Web) and Treasury is checked for the
  new 409 answers and the `idempotentReplay` flag: either its own test passes against them, or the consumer owner records
  that the consumer never sends the changed case, with the code line. Status until run: expected, not tested.

**Closure (replaces the last line of §17.7):** TRM-15 closes when AC-W1…W17 have passed in the MOD-0023 owner's WP, the
Platform build is green, every AC-W3 / AC-W17 result is bound to its consumer owner's evidence (MDM included), and each
slice is merged by someone other than its author. O-S17-1 does not block the slices; it must be decided before the first
erasure request is handled.

### 17.9 Revision 3 — code inspection 01 findings (2026-10-29) — supersedes §17.1–17.8 where they conflict

Source: `docs/records/audits/2026-10/mod0023-s17-inspection-01/INSPECTION.md` (@f1303a9d8; read-only, measured at
@4c08fc75a = origin/main 2026-10-29; no build, no test, no Mongo). Owner instruction `rev3-yaz` (MOD-0023 owner,
2026-10-29): revision 3 is limited to that note's numbered findings; each item below names its finding (`DL-1/n`,
`DL-2/n`). Every code fact was re-read by the pack author at @4c08fc75a (**read, not run**). Revision 3 adds no
permission, no module id, no route path and no index.

**DL-1 — no-deletion policy + additive read fields**

1. **Correction to §17.8/2 (INSPECTION.md DL-1/1):** soft-deleted rows of the three types are **already hidden at the
   repository layer**. `TenantRepository<T>.ExecutionFilter` is `TenantId == current` **and** `IsDeleted == false`
   (`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs:75-79@4c08fc75a`), and every read
   the §17 handlers use applies it: `ListByObjectIdsAsync`
   (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs:181-185@4c08fc75a`),
   the inherited `GetByIdAsync` (`Repositories.cs:22-28@4c08fc75a`), `ApprovalTaskRepository.ListByInstanceIdAsync`
   (`WorkflowRepositories.cs:589-595@4c08fc75a`) and `WorkflowTransitionLogRepository.ListByInstanceIdAsync`
   (`WorkflowRepositories.cs:822-830@4c08fc75a`). The handler filter `.Where(x => x.DeletedAt is null)`
   (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetWorkflowInstancesByObjectsHandler.cs:51@4c08fc75a`)
   is therefore **dead on real Mongo**: a row deleted by the inherited `DeleteAsync` (`IsDeleted = true`, `DeletedAt` unset,
   `Repositories.cs:35-46@4c08fc75a`) never reaches it. §17.8/2's "would not be hidden — the two deletion markers
   disagree" holds **only for fake / in-memory repositories** that do not reproduce `ExecutionFilter`. Consequences:
   (a) the §17.8/3 rule "one marker, `IsDeleted`" stands; the default hide comes from `ExecutionFilter`, the dead handler
   filter may be removed; (b) `includeDeleted=true` cannot be served by any existing repository member — it needs one new read
   member (item 2); (c) AC-W13 and AC-W15 (deleted fixture hidden by default, returned flagged under `includeDeleted=true`)
   and AC-W16 (item 15: the `includeDeleted` branch and its tenant sabotage — the new member's default body throws, item 2,
   so they run only on the real repository; interim check 03 F-7) are evidenced against the real repositories on Mongo; a
   fake store's result is not evidence for them; (d) AC-W15's
   sabotage "reverting the filter to `DeletedAt is null`" is read as: the `includeDeleted` branch reading through
   `ExecutionFilter` (flag silently ignored), or the default branch reading without it, turns the test red. The
   `DeleteAsync` override of AC-W15 is unchanged — the inherited delete path still exists.
2. **Allowed paths for DL-1 — replaces §17.8/9's DL-1 list (INSPECTION.md DL-1/2):**
   *(Made consistent with O-S17-2 = (b), item 3 — the CT's note in `docs/records/reviews/MOD-0023/REVIEW-REQUEST-rev3-01.md:44` and interim check 02 F-8,
   `docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-02.md:25-26`.)* §17.8/9's permission
   for an `includeDeleted` branch in the `history` read handler is **removed**; the `WorkflowRepositories.cs` override of
   §17.8/9 stays (second bullet below). The list:
   - `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IWorkflowRepositories.cs` — only **one** new
     **read** member on `IWorkflowInstanceRepository` (`:41@4c08fc75a`): the instance read by object ids that includes
     deleted rows (the deleted-including twin of `ListByObjectIdsAsync`, `:161@4c08fc75a`). It keeps the tenant condition
     and drops only `IsDeleted == false`. `IApprovalTaskRepository` and `IWorkflowTransitionLogRepository` gain no member.
     **Still no delete member** on any interface (AC-W15 unchanged).
     **Default interface body (proposal; the WP keeps it or justifies another choice):** the new member gets a default
     body that throws `NotSupportedException`, as the transactional `GetByIdAsync` does (`IWorkflowRepositories.cs:53-57@4c08fc75a`),
     and only `WorkflowInstanceRepository` (`WorkflowRepositories.cs:141@4c08fc75a`) overrides it. Not a filtering default
     like `ListByObjectIdsAsync` (`IWorkflowRepositories.cs:161-167@4c08fc75a`): that default reads through
     `GetAllForTenantAsync`, which on Mongo applies `ExecutionFilter` (`WorkflowRepositories.cs:349-352@4c08fc75a`), so an
     implementation without an override would silently return no deleted row — the "flag silently ignored" failure of
     item 1(d). Not abstract either: test doubles implement the interface (e.g.
     `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowStartRecoveryMongoTests.cs:1783-1784@4c08fc75a`, `AllReservedBarrier`)
     and would all have to change. A throwing default fails loudly; AC-W13/W15 run on the real repository anyway (item 1(c)).
   - `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs` —
     the refusing `DeleteAsync` override in the three repositories (§17.8/9, AC-W15) and the new read member's
     implementation (the default `IsDeleted` hide already comes from `ExecutionFilter`, item 1(a)).
   - `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetWorkflowInstancesByObjectsHandler.cs`
     — its `includeDeleted` branch and the `isDeleted` mapping.
   - `.../Handlers/QueryHandlers/GetWorkflowInstanceHistoryHandler.cs` (same folder) — **only** the additive read fields
     of item 3; no `includeDeleted` branch and no deleted-including read.
   - Binding the optional query parameter (pack author's addition under DL-1/2, not named in the note): today the action
     does not take it (`services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs:121-131@4c08fc75a`).
     Allowed: that controller file (one optional `includeDeleted` query parameter, default `false`, on the `by-objects`
     action only; path, verb and `[HasPermission]` unchanged) and the query record
     `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Queries/GetWorkflowInstancesByObjectsQuery.cs`
     (one optional trailing member). The `history` action (`WorkflowDefinitionsController.cs:104-110@4c08fc75a`) and
     `.../Queries/GetWorkflowInstanceHistoryQuery.cs` stay unchanged.
   - `services/Diten.Platform.Common/**` stays untouched (§17.8/9 unchanged).
3. **Owner decision O-S17-2 — history of a soft-deleted instance (INSPECTION.md DL-1/2) — decided (b), below.** Today `history` of a
   soft-deleted instance answers **404** (`GetWorkflowInstanceHistoryHandler.cs:51-59@4c08fc75a`), with or without the flag.
   - **(a)** `history` also takes `includeDeleted=true`: the deleted instance is found and its transition rows are
     returned, each flagged `isDeleted`; without the flag the answer stays 404. Needs deleted-including reads for instance
     by id, logs and tasks; AC-W13 and AC-W16 gain the `history` case.
   - **(b)** `history` stays 404 for a deleted instance in every case; Treasury reads deletion only from
     `by-objects?includeDeleted=true` (`isDeleted = true` on the instance row ⇒ do not re-issue, D21/2 consumer rule); an
     `Unknown` transition intent under a deleted instance stays `Unknown` with an alert.
   - **Öneri — sahip kararı:** (b). The policy is that nothing is deleted (§17.7/2) and the flag is defensive (§17.8/3);
     (b) already stops a re-issue, needs fewer new repository members and keeps `history`'s answer unchanged.
   - **Decided (MOD-0023 owner, `gecmis-404`, CT · İş akışı chat, 2026-10-29): (b)** — record
     `docs/records/decisions/2026-10/mod0023-s17-owner-decisions-01.md`, row S17-2. That record numbers its rows
     S17-1…S17-3 as decisions taken (`rev3-yaz`, `gecmis-404`, `duzelt`); only S17-2 decides an open item (O-S17-2); rows
     S17-1 and S17-3 do not decide O-S17-1 or O-S17-3, which stay open (interim check 07 F-4). O-S17-2 is no longer a gate. Consequences for item 2: `history`
     takes no `includeDeleted` parameter and gets no deleted-including read; `GetWorkflowInstanceHistoryQuery.cs` and the
     `history` action stay unchanged; the `history` handler changes only for the additive read fields (`approvalTaskId`,
     `idempotencyKey`, `isDeleted` of each returned row — always `false` there, since deleted rows are filtered). The only
     new deleted-including read member is the instance read by object ids used by `by-objects?includeDeleted=true`.
4. **Person-field list — extends §17.8/4 (INSPECTION.md DL-1/3):** add `WorkflowTransitionLog.EvidenceRef`,
   `SignatureId` (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Workflow/WorkflowTransitionLog.cs:24, 27@4c08fc75a`)
   and `WorkflowInstance.ObjectRef`, `DisplayContext` (free-text snapshot)
   (`.../Entities/Workflow/WorkflowInstance.cs:23, 66@4c08fc75a`). `EvidenceRef` is compared by the terminal writer's
   `SamePayload` (`.../Handlers/CommandHandlers/WorkflowTerminalTransitionWriter.cs:433-438@4c08fc75a`) and holds the trusted
   cancel fingerprint (`.../Services/TrustedWorkflowCancellationCoordinator.cs:288@4c08fc75a`); pseudonymising it must keep
   those comparisons meaningful, like `StartedByUserId`. **Further candidates (review round 3, interim check 02 F-6;
   read, not run, @4c08fc75a):**
   - `RuntimeAssignmentSnapshot.ResolvedPrincipalId`, `CandidatePrincipalIds` (principal ids,
     `.../Entities/Workflow/RuntimeAssignmentSnapshot.cs:10-11`); the WP still measures `TieBreakExplanation` (`:13`).
   - `WorkflowInstance.StartRequestFingerprint` (`.../Entities/Workflow/WorkflowInstance.cs:33`): a SHA-256 over, among
     others, the maker id, object ref and candidate ids
     (`.../Features/Workflow/Services/WorkflowInstanceStartCoordinator.cs:761-790`), compared on a same-key trusted start
     at `:228-255` — with `DelegatedMakerUserId` and `ObjectRef` also compared directly (`MatchesReservation`, `:598-615`)
     and the legacy fingerprint recomputed from the stored snapshot (`:228-238`). Pseudonymising those inputs must not
     turn a genuine same-key replay into 409 `WORKFLOW_START_IDEMPOTENCY_CONFLICT`.
   - `WorkflowInstance.ObjectId` (`.../Entities/Workflow/WorkflowInstance.cs:20`; interim check 03 F-5): `ObjectRef`
     embeds it (`:22-23`), `MatchesReservation` compares it (`WorkflowInstanceStartCoordinator.cs:614`) and it is a
     fingerprint input (`:783`). A consumer's object id may name a person (e.g. an employee record); the WP measures
     whether any consumer sends one. If pseudonymised, it must change together with `ObjectRef` and the fingerprint input.
   - The trusted cancel evidence read parses `log.ActorId` as a `Guid`
     (`.../Services/TrustedWorkflowCancellationCoordinator.cs:289`; failure → Conflict `:293`): a pseudonym of
     `ActorId` must stay Guid-shaped. So must a pseudonym of `ApprovalTask.AssigneeRef` and of a decision log's
     `ActorId`: Timesheet parses both as a `Guid`
     (`.../Features/TimeEntry/Services/TimesheetApprovalService.cs:202, 297`; interim check 04 F-4).
   - Free-text `WorkflowTransitionLog.ReasonCode` (`.../Entities/Workflow/WorkflowTransitionLog.cs:21`) and
     `ApprovalTask.ReasonCode` / `ActionReasonCode` (`.../Entities/Workflow/ApprovalTask.cs:21, 51`; interim check 04
     F-4). The log's `ReasonCode` is compared by `SamePayload` (`WorkflowTerminalTransitionWriter.cs:436`), so
     pseudonymising it must keep that comparison meaningful, like `EvidenceRef`.
   - `RequestInfo` writes `target:{principal}` into the log `Comment` even when the user wrote none
     (`.../Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs:587-591`).
   - `WorkflowTransitionLog.TargetPrincipalId` (new in DL-2, item 7; review 5512cbca F-1): a principal id. Its
     pseudonym stays Guid-shaped, like `ActorId`, keeps `""` (no target) as is and must change together with the
     `target:` part of `Comment`, so the body comparison stays meaningful.
   - A caller-chosen `IdempotencyKey` may be free text (Diten.Web inputs, item 13); D32/3 keeps the key unchanged, so
     a key holding personal data is a question for O-S17-1.
   - **Boundary of the list (interim check 05 F-2; read, not run, @4c08fc75a):** the list covers the three retained
     types (`WorkflowInstance`, `ApprovalTask`, `WorkflowTransitionLog`, §17.7/2) plus `RuntimeAssignmentSnapshot`. All
     four derive from `TenantScopedEntity`, which adds only `TenantId`
     (`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/EntityVariations.cs:8-11`); its base adds the
     inherited person fields `CreatedBy` and `UpdatedBy`
     (`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/BaseEntity.cs:7, 10`), which join the list
     (no assignment of either found by `grep` under `.../Features/Workflow/` or in `WorkflowRepositories.cs`; the WP
     measures stored rows). `WorkflowTemplateVersion.PublishedBy` (`.../Entities/Workflow/WorkflowTemplateVersion.cs:16`)
     and `SlaEscalationRule.EscalationPrincipalIds` (`.../Entities/Workflow/SlaEscalationRule.cs:14`) are **outside §17
     retention**: they are configuration (a published template version, an SLA rule), not runtime records of a decision;
     §17.7/2 does not cover them and O-S17-1 does not decide them.
   The list stays a candidate list; the owner confirms it in the WP. O-S17-1 stays open.
5. **AC-W3 consumer list — extends §17.4 (INSPECTION.md DL-1/4):** add Platform's own `by-objects` tests
   (`services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowCrossServiceTests.cs:437-482@4c08fc75a`:
   batch read, limits, route and permission), which must pass against the new fields. Diten.Web has **no history-body test**:
   `frontend/Diten.Web.Tests/Controllers/ClaimsV2ProxyTests.cs:154@4c08fc75a` asserts only the proxy route of
   `frontend/Diten.Web/Controllers/CRM/ClaimsController.V2.cs:280`; the Web owner names or writes one (status until then:
   not tested). MDM (D32/5) and Treasury (no code on main) are unchanged.
   **Further AC-W3 rows (interim check 02 F-5, F-7; read, not run, @4c08fc75a):**
   - Platform's own `history` tests (`WorkflowCrossServiceTests.cs:551-634`: sequence, step names, actor display,
     other-tenant 404, route and permission) must pass against the new fields.
   - `instances/{id}`: Diten.Web `getInstance`
     (`frontend/Diten.Web/wwwroot/assets/js/Platform/Workflow/workflow.api.js:125-126`; no caller found by `grep` under
     `assets/js/Platform/Workflow`), and `listInstances` (`:125`, `GET instances`), which returns the same
     `WorkflowInstanceDto` (`.../Handlers/QueryHandlers/GetWorkflowInstanceListHandler.cs:18-23`; callers
     `.../Platform/Workflow/index.js:925, 956`, `tasks.js:434`) — consumers if the optional `idempotencyKey` (§17.7/4) is
     added; the Web owner records the result. This corrects §17.4 AC-W3's "no consumer outside Platform".
   - Inventory: the body evidence is `QuarantineWorkflowTests` through `FakeWorkflowGateway`
     (`services/Diten.InventoryService/tests/Diten.InventoryService.Tests/TestInfrastructure/FakeWorkflowGateway.cs:47`);
     `QuarantineArchitectureTests` checks only the approval-port registration (`QuarantineArchitectureTests.cs:165-183`)
     and reads no body.
6. **Schema manifest path (INSPECTION.md DL-1/5):** §17.2 and AC-W1 mean
   `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.WorkflowWorkCenter.cs`
   (index names at `:125`, `:220` @4c08fc75a).

**DL-2 — same-key answer**

7. **Error-code correction to §17.7/5 and AC-W14 (INSPECTION.md DL-2/1):** a same-(task, action, key) replay by a
   different actor is answered **403 `WORKFLOW_ACTOR_DENIED`**
   (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs:1004-1009,
   1018-1028@4c08fc75a`). `WORKFLOW_ACTOR_MISMATCH` is a different case — the request body names an actor other than the
   signed-in caller (`.../Features/Workflow/WorkflowActorBinding.cs:42-52`,
   `.../Controllers/WorkflowDefinitionsController.cs:316-326` @4c08fc75a) — and DL-2 does not change it. **AC-W14
   restated:** same (task, action, key), different actor — by the item-10 timing cases (interim check 05 F-1; read, not
   run, @4c08fc75a; the actor check runs only in the replay `WorkflowTaskTransitionSupport.cs:1018-1030`):
   - **(a)** the twin's row is found by the first lookup (`:61-65`; RequestInfo `:539-547`) → 403
     `WORKFLOW_ACTOR_DENIED` before and after the fix, under both O-S17-3 options (proposal: D25/3 is read as changing
     only the body case).
   - **(b)** late arrival (lookup before the twin commits): today 409 `WORKFLOW_TRANSITION_CONFLICT` with a stale
     `expectedVersion` (`StaleVersion`, `:77-80`, `:1011-1016`; RequestInfo `:558-561`) or 409
     `WORKFLOW_TASK_INVALID_STATE` (`:82-89`), before any actor check. **Approve/Reject:** under O-S17-3 (i) the added
     re-read routes it to the actor-bound replay → 403; under (ii) today's 409s stay and the consumer maps both to
     `Unknown` (§17.7/6 for `WORKFLOW_TRANSITION_CONFLICT`, item 17 (ii) for `WORKFLOW_TASK_INVALID_STATE` after a
     same-key send). **RequestInfo** (not part of O-S17-3; interim check 06 F-1): without `expectedVersion` → 403 under
     both options — at the candidate check (`:567-570`) if the other actor is no candidate, otherwise through the
     duplicate-key mapping (DL-2 base scope, item 17); with a stale `expectedVersion` → 409 `WORKFLOW_TRANSITION_CONFLICT`
     (`:558-561`) under both options, `Unknown` (§17.7/6).
   - **(c)** in-transaction overlap: the loser gets 409 `WORKFLOW_TRANSITION_CONFLICT`
     (`WorkflowTerminalTransitionWriter.cs:108-110`; on the unavailable path `:370-376`, the version moved without a row
     of this actor) or 503 `WORKFLOW_TRANSACTION_UNAVAILABLE` (`:390`; `WorkflowTaskTransitionSupport.cs:834-838`),
     before any actor check. O-S17-3 (i) as written (item 17) adds no re-read on these paths for Approve/Reject, so the
     answer stays 409/503 under both options and is `Unknown` (§17.7/6); a replay of the key then meets case (a) → 403
     once a row under the key is recorded (review 5512cbca F-2; with no row it is a first write, item 10).
     A RequestInfo loser whose log meets the unique index goes through the duplicate-key mapping (item 17) → 403 under
     both options; the WP measures which RequestInfo path the race takes (item 10 (c)).
   **Same actor, different body** (reason code, comment, evidence ref, — for Approve — `SignatureId` and — for
   RequestInfo — `TargetPrincipalId`; restated for interim check 06 F-2, interim check 07 F-3 and interim check 08 F-3;
   read, not run, @4c08fc75a) — no new row in any case:
   - **`TargetPrincipalId` (interim check 08 F-3; rewritten for review 5512cbca F-1; read, not run, @4c08fc75a):** the
     RequestInfo request carries it (`WorkflowModels.cs:505-512`, `string?`, not parsed) and the support writes it only
     into the log `Comment` (`WorkflowTaskTransitionSupport.cs:587-591`: target only → `target:P`; comment and target →
     `{comment} target:P`); `WorkflowTransitionLog` has no target field (`.../Entities/Workflow/WorkflowTransitionLog.cs:9-33`).
     So (target `P`, comment `not`) and (no target, comment `not target:P`) store the same `Comment`, and a comparison
     through `Comment` / `SamePayload` (`WorkflowTerminalTransitionWriter.cs:433-438`) would take the second for a replay
     of the first. **Representation (proposal; the owner approves it with this pack):** DL-2 adds one additive nullable
     property `TargetPrincipalId` (`string?`) to `WorkflowTransitionLog`. Every new RequestInfo row stores it: the trimmed
     target, or `""` when the request has none — so `null` means only "written before the field existed" (a missing
     BSON field reads as `null`); other actions leave it `null`. The stored `Comment` format is unchanged, so no reader
     (`history`, Diten.Web, Timesheet) changes, and the field is added to no read DTO. The body comparison — the
     sequential path (the actor-bound replay after the fix), `SamePayload` (unavailable re-read) and the duplicate-key
     mapping (item 17) — compares `Comment` **and** `TargetPrincipalId` separately (ordinal). With the target compared
     on its own, equal `Comment`s mean equal (trimmed) user comments, so the two collision bodies differ.
     **Legacy rows (RequestInfo row, field `null`) — öneri, stated per path (pre-review 1 F-1; read, not run,
     @4c08fc75a):** today no path compares the target on its own — the actor-bound replay compares no body at all
     (`WorkflowTaskTransitionSupport.cs:1018-1030`) and `SamePayload` carries the target only inside `Comment`
     (`WorkflowTerminalTransitionWriter.cs:433-438`; it compares `ToState`, `ToStatus`, `ReasonCode`, `Comment`,
     `EvidenceRef`). So "as today" is not a rule; the rule is the same on each of the three paths — (1) the sequential
     path (the actor-bound replay after the fix), (2) the duplicate-key mapping (item 17), (3) the unavailable re-read
     (`SamePayload`): **if the stored `TargetPrincipalId` is `null` (legacy row), the target is not compared separately
     and is compared only through `Comment`** (the retry's composed `Comment` against the stored one; the path's other
     body fields as for any row); otherwise the two `TargetPrincipalId` values are compared ordinal (`""` equals only
     `""`). A naive `string.Equals(stored, attempted)` would compare the legacy `null` with the retry's `""` (no target)
     or `P` and turn an identical retry against a legacy row into 409; the `null` branch runs first. The collision above
     stays undetected for legacy rows only; accepted, because they exist only for keys sent before DL-2 is deployed and a
     genuine retry must not get 409. (Rejected: actor-only replay for legacy rows — it would drop the reason, comment
     and evidence comparison too.) No index and no schema-manifest change: the manifest declares indexes only
     (`PlatformSchemaManifest.WorkflowWorkCenter.cs:195-227`), and the field joins none. **AC (AC-W14, RequestInfo):** on
     real Mongo, same actor and key — direction A: first (target `P`, comment `not`), then (no target, comment
     `not target:P`) → the sequential different-body 409, one row holding target `P`; direction B, the reverse order →
     the same 409; an identical retry → 200 `isIdempotent = true`; the same pair through the duplicate-key mapping
     (case (b), item 10) → the same answers. **Unavailable re-read path (interim check 09 F-2):** a forced "transaction
     unavailable" with a colliding twin committed meanwhile — same actor and key, directions A and B as above (twin =
     the stored row, the call = the retry) — is answered **not** 200 Replayed by `SamePayload` in `AfterUnavailableAsync`
     (`WorkflowTerminalTransitionWriter.cs:399-438`): 409 `WORKFLOW_TRANSITION_CONFLICT` if the version moved, 503
     `WORKFLOW_TRANSACTION_UNAVAILABLE` if nothing moved (per the path, item 7 (c)); pattern
     `WorkflowCandidateDecisionMongoTests.D2Fix1.cs:509-543@4c08fc75a` (twin inside `ReportsUnavailableAfter`).
     **Legacy-row AC, one per path:** a crafted legacy RequestInfo row (field unset) and an identical retry — once without
     a target (stored `Comment` `not`; the retry's field `""`) and once with target `P` (stored `Comment`
     `not target:P`; the retry's field `P`) — → (1) sequential: 200 `isIdempotent = true`; (2) duplicate-key mapping
     (case (b), item 10): 200 `isIdempotent = true`; (3) unavailable re-read, the legacy row as the twin inside
     `ReportsUnavailableAfter`: 200 Replayed (`isIdempotent = true`); no new row on any path. Sabotage: replacing the
     `null` branch by a plain `string.Equals` turns all six red.
     **Collision sabotage, by direction:** (s1) dropping `TargetPrincipalId` from the comparison → directions A and B
     red on all three paths (equal `Comment`s); (s2) writing `null` instead of `""` for a request without a target →
     the stored no-target row of direction B looks legacy, so **direction B** turns red on all three paths (target
     compared only through `Comment` → 200 / Replayed); direction A stays 409 under (s2) (stored `P` is not legacy) and
     is not evidence for it. For (s2) to reach the unavailable re-read test, the twin's `TargetPrincipalId` (and
     `Comment`) is the value of a row **written by the handler** through a real RequestInfo call (the model-row step of
     the pattern, `D2Fix1.cs:512-514`), not a hand-set value: today's field-by-field twin (`D2Fix1.cs:519-536`) has no
     `TargetPrincipalId` and would store `null` — a legacy row — under any build, so direction B would pass or fail for
     the wrong reason. The WP asserts the twin's stored field equals the handler-written row's (`""` in direction B).
   - **`SignatureId` (interim check 07 F-3):** the Approve request carries it (`WorkflowModels.cs:467-475`), the decision
     log stores it for Approve only (`WorkflowTaskTransitionSupport.cs:270, 288`; Reject and RequestInfo store none), but
     `SamePayload` does not compare it (`WorkflowTerminalTransitionWriter.cs:433-438`) and today's actor-bound replay
     compares no body (`WorkflowTaskTransitionSupport.cs:1018-1030`), so the answer to a same-key, same-actor Approve
     retry with a different `SignatureId` is not defined by the target. **Öneri — the owner approves it with this
     pack:** `SignatureId` is part of the compared body on the sequential path (the actor-bound replay after the fix) and
     in `SamePayload`; such a retry gets the sequential different-body 409 below. Test the WP adds: on a
     signature-required step (fixture `Definition(signatureRequired: true)`,
     `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ESignature/WorkflowESignatureGateTests.cs:52-57@4c08fc75a`;
     real Mongo replica set, `:23-26`), approve with signature A, then repeat the same key and actor with signature B →
     the different-body 409, one row for (task, `Approve`, key) holding A; sabotage: dropping `SignatureId` from the
     comparison turns it red. The replay lookup (`WorkflowTaskTransitionSupport.cs:61-65`) runs before the signature
     gate (`:146-167`), so the retry is answered by the replay, not by the gate.
   - **Sequential path only** — case (a) (the actor-bound replay, which after the fix compares the body as `SamePayload`
     does) and case (b) where the late call reaches that replay (Approve/Reject under O-S17-3 (i) through the re-read;
     RequestInfo without `expectedVersion` through the duplicate-key mapping, item 17, under both options) → 409 with a
     distinct code, named in the WP (proposal: not `WORKFLOW_TRANSITION_CONFLICT`, which §17.7/6 treats as retryable
     `Unknown`). Under O-S17-3 (ii) Approve/Reject case (b) keeps today's 409s; RequestInfo with a stale
     `expectedVersion` keeps 409 `WORKFLOW_TRANSITION_CONFLICT` (`:558-561`) under both options.
   - **Concurrent path (case (c))** — the version fence cannot tell bodies apart: the loser gets 409
     `WORKFLOW_TRANSITION_CONFLICT` (`WorkflowTerminalTransitionWriter.cs:108-110`, mapped at
     `WorkflowTaskTransitionSupport.cs:839-843`), `Unknown` for the consumer (§17.7/6), whatever its body. Only the
     transaction-unavailable re-read tells bodies apart (`MyCommittedLogAsync` / `SamePayload`,
     `WorkflowTerminalTransitionWriter.cs:399-438`): same actor and body → 200 Replayed; same actor, different body → not
     its own commit, so with the version moved → 409 `WORKFLOW_TRANSITION_CONFLICT` (`:370-376`), 503
     `WORKFLOW_TRANSACTION_UNAVAILABLE` if nothing moved (`:390`). The existing tests that pin this —
     `WorkflowCandidateDecisionMongoTests.D2.cs:265-276` and `WorkflowCandidateDecisionMongoTests.D2Fix1.cs:368-378`,
     both asserting `WorkflowTransitionConflict` — stay unchanged.
   **AC-W11's transition sentence restated (interim check 08 F-2)** — replaces §17.4 AC-W11's "different actor or reason
   → the defined conflict code" for the same (task, action, K): a **different actor** → as the actor cases above (403
   `WORKFLOW_ACTOR_DENIED` in case (a); cases (b) and (c) as listed there, Approve/Reject case (b) per O-S17-3); a
   **different body** (reason code, comment, evidence ref, `SignatureId`, RequestInfo `TargetPrincipalId`) → the
   sequential different-body 409 above. No new row in either case.
   **Delegate (interim check 08 F-4; read, not run, @4c08fc75a):** Delegate's same-key replay uses the same shared
   actor-bound replay (`WorkflowTaskTransitionSupport.cs:388` → `:1018-1030`), so a body comparison added inside that
   helper would change Delegate too. **Öneri — the owner approves it with this pack:** the body comparison is added per
   action for Approve, Reject, RequestInfo and Cancel (item 8), not inside the shared helper's Delegate path; Delegate's
   same-key replay stays unchanged (actor-bound, no body comparison) and Delegate is in neither AC-W14 nor AC-W17. Test
   the WP adds: delegate with key K, repeat K with the same actor and a different reason / comment / delegate target →
   200 `isIdempotent = true` with the first transition, as today; sabotage: comparing the body on Delegate's replay
   turns it red. The rejected alternative — Delegate joins AC-W14/AC-W17 — would add its callers to the consumer check:
   Diten.Web `delegateTask` (`frontend/Diten.Web/wwwroot/assets/js/Platform/Workflow/workflow.api.js:134`) and the
   WorkCenter dispatcher (`.../Features/WorkAggregation/Providers/WorkflowApprovalWorkItemActionDispatcher.cs:140`).
8. **Cancel replay is not actor-bound — new AC-W18 (INSPECTION.md DL-2/2):** the cancel path answers a same-key replay
   directly (`WorkflowTaskTransitionSupport.cs:622-630@4c08fc75a`, `BuildIdempotentResponseAsync` without the actor check),
   so another actor's replay gets 200. **AC-W18 (proposal for the target answer):** same (task, `Cancel`, key) with a
   different actor → the same answer the other transitions give after the fix (403 `WORKFLOW_ACTOR_DENIED`); with the same
   actor and a different body → the AC-W14 409; same actor and body → 200 `isIdempotent = true`. Sabotage: restoring the
   direct replay call turns it red. The changed cancel answer (another actor's replay: 200 today → 403) is part of the
   AC-W17 consumer check: Timesheet withdraw and Diten.Web `cancelTask` (item 13) record their result; AC-W18 does not
   pass without it.
9. **AC-W8 restated (INSPECTION.md DL-2/3; timing cases rewritten for review F-2, `2026-10-29-3e802d2f`):** two
   different decisions on one pending task (e.g. approve vs reject, different keys): exactly one decision is recorded —
   one row for (task, winner's action, winner's key), none under the loser's key — and never 200 for the loser. At the
   final step that is one terminal decision (pattern `WorkflowCandidateDecisionMongoTests.cs:118@4c08fc75a`); at an
   intermediate step an Approve winner also writes exactly one next task and one `{key}:next:{nextTaskId}` Start row
   (item 10, F-4 paragraph; pattern `:142`). AC-W8 covers both steps. The loser's answer depends on when it reads. Order of checks
   (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs@4c08fc75a`):
   replay lookup by (task, action, key) `:61-65` (no row under the loser's key), task lookup `:67-75`, `expectedVersion`
   mismatch → 409 `WORKFLOW_TRANSITION_CONFLICT` (`StaleVersion`, `:77-80, 1011-1016`), not decidable → 409
   `WORKFLOW_TASK_INVALID_STATE` (`:82-89`), then the in-transaction write. Separate measured cases, each forced by the test:
   - **(b) late arrival** — the test awaits the winner's committed answer, then sends the loser. Without `expectedVersion`
     → 409 `WORKFLOW_TASK_INVALID_STATE`; with the version read before the winner (now stale) → 409
     `WORKFLOW_TRANSITION_CONFLICT`. A later replay of the loser's own key gets the same answer as its late arrival
     (under O-S17-3 (i) too: the added re-read finds no row under the loser's key, item 17).
   - **(c) in-transaction overlap** — forced by a barrier around the real executor (pattern: `TwoCallerBarrier` around
     `PlatformTransactionExecutor`,
     `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowCandidateDecisionMongoTests.cs:121, 602@4c08fc75a`),
     so both calls pass `:82-89` before either commits. Loser → 409 `WORKFLOW_TRANSITION_CONFLICT`
     (`WorkflowTerminalTransitionWriter.cs:108-110@4c08fc75a` → `WorkflowTaskTransitionSupport.cs:839-843`), also when the
     executor reports transaction-unavailable and the re-read sees the version moved without a row of its own
     (`WorkflowTerminalTransitionWriter.cs:370-376`); 503 `WORKFLOW_TRANSACTION_UNAVAILABLE` only when nothing moved in the
     wait (`:390`; `WorkflowTaskTransitionSupport.cs:834-838`), forced by an injected "unavailable" executor.
     **Rows per forced branch (interim check 08 F-1; read, not run, @4c08fc75a):** the barrier race and the injected
     "somebody else decided" branch → exactly one decision row, the winner's; the injected 503 "nothing moved" branch →
     **zero rows**: the injected executor never runs the body (`ReportsUnavailableAfter`,
     `WorkflowCandidateDecisionMongoTests.Fix1.cs:635-643`), the writer answers `TransactionUnavailable`
     (`WorkflowTerminalTransitionWriter.cs:379-390`), no decision is recorded and the task and its version are unchanged
     (existing test `Fix1.cs:156-163` asserts the 503 only; the WP adds the zero-row and unchanged-task assertions).
     "Exactly one decision is recorded" above applies to the branches where a winner commits.
   - Case (a) (a recorded row found on the first read) does not apply: no row exists under the loser's key.
   **Existing tests — reused as evidence, not rewritten (interim check 03 F-1; read, not run, @4c08fc75a)**, all in the
   `WorkflowCandidateDecisionMongoTests` partial class under
   `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/`:
   - case (c), approve-vs-approve: final step `WorkflowCandidateDecisionMongoTests.cs:118-139`, intermediate step `:142-175`;
   - case (c), approve-vs-reject at the **final** step: `WorkflowCandidateDecisionMongoTests.Fix1.cs:356-376` (barrier;
     loser 409 `WORKFLOW_TRANSITION_CONFLICT`; one Approve-or-Reject row; one completion event);
   - case (c), transaction-unavailable with different keys: "somebody else decided" → 409 (`Fix1.cs:141-154`, approve vs
     approve, final step) and "nothing moved" → 503, also when the re-read fails (`Fix1.cs:156-172`).
   The WP adds only what is missing: approve-vs-reject in case (b) at both steps, approve-vs-reject in case (c) at the
   **intermediate** step, and the case-(b) replay of the loser's own key. Consumer rule §17.7/6:
   the 409 conflict is `Unknown`. Fact for it: a replay that repeats a stale `expectedVersion` gets 409
   `WORKFLOW_TRANSITION_CONFLICT` again. **AC-W8's asserted answers do not depend on O-S17-3** (no row exists under the
   loser's key under (i) or (ii), item 17) nor on the RequestInfo duplicate-key mapping (item 17; the loser's key differs,
   so no duplicate key arises); **only the consumer rule's definite refusal does:** under (i) it comes from
   409 `WORKFLOW_TASK_INVALID_STATE` (first send or replay without the stale version) or from a read; under (ii) a
   `WORKFLOW_TASK_INVALID_STATE` after a same-key retry is `Unknown`, and the refusal comes only from a first send's
   `WORKFLOW_TASK_INVALID_STATE` or from a `history` read (the other decision recorded, no row under the loser's key).
   Items that depend on O-S17-3 (the same list as item 17, the closure and the DL-2 finish list; interim check 07 F-2):
   AC-W6 and AC-W7 case (b) for Approve/Reject; AC-W14 case (b) for Approve/Reject (different actor; same actor,
   different body); the §17.7/6 consumer rule; under (ii) only, the AC-W17 consumer and Treasury recording of that rule.
   AC-W8 does not depend on O-S17-3.
10. **AC-W6 / AC-W7 restated (INSPECTION.md DL-2/4, DL-2/7; timing cases rewritten for review F-2):** identical
    transitions (same task, action, key, actor and body) → exactly **one row for (task, action, key)** in every case
    where a call commits — cases (a), (b), the barrier race of (c) and the injected 200-Replayed branch (the one
    committed row); the injected 503 "nothing moved" branch of (c) writes **zero rows** and leaves the original state
    unchanged (item 10 (c), interim check 08 F-1).
    AC-W6 is Approve, AC-W7 is Reject and RequestInfo (§17.4); Delegate is in neither and gets no case here. Separate
    measured cases, each forced by the test (lines in `WorkflowTaskTransitionSupport.cs@4c08fc75a` unless named).
    **Final and intermediate step (interim check 02 F-4):** an intermediate Approve is not written by `CommitAsync` but
    by `CommitStepAsync` (`:284-308`; `WorkflowTerminalTransitionWriter.cs:197-259@4c08fc75a`), in one transaction with
    two log rows: the decision (task, `Approve`, key) and the next task's `Start` row under `{key}:next:{nextTaskId}`
    (`:289-306`). Its conflict is `WorkflowTerminalTransitionWriter.cs:251-253` and its transaction-unavailable path
    `:255-257` (the same `AfterUnavailableAsync` as the final step, `:357`). So AC-W6 and AC-W8 assert one row for
    (task, `Approve`, key) and, at the intermediate step, exactly one next task and one next-`Start` row — not one log
    row in total — and each covers both the final step (pattern `WorkflowCandidateDecisionMongoTests.cs:118`) and the
    intermediate step (`:142`). **Case (b) of Approve/Reject depends on owner decision O-S17-3 (item 17).**
    **Approve and Reject** (`TransitionAsync`; the check order `:61-65` / `:67-75` / `:77-80` / `:82-89` is theirs only):
    - **(a) replay found on the first read** — the test awaits the first call's committed answer, then repeats it: the
      lookup `:61-65` finds the row and answers through the actor-bound replay `:1018-1030` → 200 `isIdempotent = true`,
      same transition (`:1054-1084`). No task, version or decidable check runs, so a stale `expectedVersion` does not
      change it; a different actor → 403 (item 7).
    - **(b) late arrival** — a call whose lookup `:61-65` ran before the twin committed and whose task read `:67` ran
      after it. **Today:** without `expectedVersion` → 409 `WORKFLOW_TASK_INVALID_STATE` (`:82-89`); with a stale
      `expectedVersion` → 409 `WORKFLOW_TRANSITION_CONFLICT` (`:77-80`) — although its own transition is recorded.
      **Asserted answer per O-S17-3:** under (i) 200 `isIdempotent = true` with the twin's transition, with or without a
      stale `expectedVersion` (a different actor → 403, item 7; sabotage: removing the added re-read turns it red);
      under (ii) today's two answers, and the AC-W17 / Treasury consumer check shows each mapped to `Unknown` and
      resolved by a `history` read. Forced by holding the late call between the two reads while the twin runs and
      commits — the loser-held / winner-runs pattern of
      `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowStartRecoveryMongoTests.cs@4c08fc75a`:
      `Hold` (`:2718`, holds the first call of a read until released), a repository wrapper that calls `Hold.PassAsync()`
      around the held read (`HoldAfterReserveRepository`, `:2448-2463`), and the test shape `:1870-1889` (start the held
      call, await `ReachedAsync`, run the winner to its committed answer, `Release`, assert). Here the wrapper wraps the
      late call's task repository only (the winner gets the unwrapped one, as `:1878` does) and passes the hold
      **before** the inner `GetByIdAsync` (`:67`), so that read sees the committed twin. `AllReservedBarrier` (`:1783`) is not this pattern: it holds
      every caller at the key lookup until all arrive (AC-W5, item 11).
      **Task-repository wrappers (interim check 03 F-4; read, not run, @4c08fc75a):** the existing ones are
      `HoldingTaskRepository` (`WorkflowStartRecoveryMongoTests.cs:2737-2753`, holds `GetByIdAsync` at `:2742-2746`; used
      at `:684, 708`) and `ReadsFailWhenArmed` (`WorkflowCandidateDecisionMongoTests.Fix1.cs:655-673`). On
      `IApprovalTaskRepository` the session-bound `UpdateAsync` and `CreateAsync` default to the session-less ones
      (`services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IWorkflowRepositories.cs:326-334@4c08fc75a`), and the
      writer receives the same (wrapped) task repository (`WorkflowTaskTransitionSupport.cs:39-41`) and writes through
      `UpdateAsync(session, …)` (`WorkflowTerminalTransitionWriter.cs:318`). `HoldingTaskRepository` forwards only the
      session-bound `CreateAsync` (`:2739`), `ReadsFailWhenArmed` neither. So the case-(b) wrapper **must forward the
      session-bound `UpdateAsync` and `CreateAsync` to the inner repository**; otherwise the measured write leaves the
      transaction and the test measures a different write. `Hold` holds only the **first** call (`:2724-2731`): the Reject
      second-read hold below needs a call counter (hold on the n-th `GetByIdAsync`), not `Hold` as it is.
    - **(c) in-transaction overlap** — barrier around the real executor as in item 9 (c), both calls past `:61-65` and
      `:82-89` before either commits. Loser → 409 `WORKFLOW_TRANSITION_CONFLICT`
      (`WorkflowTerminalTransitionWriter.cs:108-110@4c08fc75a`); on the transaction-unavailable path the re-read finds
      the twin's row with the same actor and payload → 200 `isIdempotent = true` (Replayed;
      `WorkflowTerminalTransitionWriter.cs:112-114, 357-368, 441-442`; `:820-833`), or 503
      `WORKFLOW_TRANSACTION_UNAVAILABLE` when nothing moved (`WorkflowTerminalTransitionWriter.cs:390`; `:834-838`).
      The transaction-unavailable path is not only injected: the writer's own comment records real Mongo reaching it
      after the executor's write-conflict retries (`WorkflowTerminalTransitionWriter.cs:350-353@4c08fc75a`; measured
      2026-10-02 per the comment, not re-measured here). So the barrier test asserts one row and accepts the loser as 409
      conflict or 200 Replayed with the winner's transition id. **Rows per forced branch (interim check 08 F-1):**
      barrier race → exactly one row for (task, action, key); injected 200-Replayed → one row, the committed twin's;
      injected 503 "nothing moved" → zero rows from the loser and the original state unchanged — the body never runs
      (`ReportsUnavailableAfter`, `WorkflowCandidateDecisionMongoTests.Fix1.cs:635-643`; writer
      `WorkflowTerminalTransitionWriter.cs:379-390`; existing test `Fix1.cs:156-163` asserts the 503 only, the WP adds the
      row and state assertions). The 200-Replayed and 503 branches are each forced
      deterministically by an injected "unavailable" executor — the decision tests' own executors
      `CommitsThenReportsUnavailable`, `ReportsUnavailableAfter` and `RunsBeforeTheBody`
      (`WorkflowCandidateDecisionMongoTests.Fix1.cs:620-653@4c08fc75a`) and the failing re-read wrapper
      `ReadsFailWhenArmed` (`:655-673`). **Already covered (interim check 03 F-1; read, not run):** the 200-Replayed branch
      for an identical twin — Reject at the final step (`WorkflowCandidateDecisionMongoTests.D2Fix1.cs:235-246`, asserts
      `isIdempotent`; `WorkflowCandidateDecisionMongoTests.D2.cs:291-301`, asserts success only), Approve at the
      intermediate step (`D2Fix1.cs:248-259`, asserts `isIdempotent` and the step path) and Approve at the final step
      against a crafted identical twin row (`D2Fix1.cs:380-387`, helper `:509-543`); the same-key, different-body 409
      (`D2.cs:265-276`, `D2Fix1.cs:368-378`); the 503 "nothing moved" branch (`Fix1.cs:156-172`, Approve). **Missing:** the
      barrier-forced identical same-key overlap (both calls same task, action, key, actor and body — every existing barrier
      test uses two keys and two actors) and the RequestInfo twin (only the own landed commit is covered,
      `WorkflowCandidateDecisionMongoTests.Guards.cs:98-117`).
    - **Reject's extra reads move the case-(b) hold.** When the comment is blank and a template-version repository is
      wired, `RejectWorkflowTaskHandler` runs its own replay lookup and task read before `TransitionAsync`
      (`.../Handlers/CommandHandlers/RejectWorkflowTaskHandler.cs:41, 50, 74-86@4c08fc75a`; lookup `:79-80`, task read
      `:86`, then instance and version reads `:92, 99`). A wrapper that holds the first task read would hold at `:86`,
      before the support's lookup `:61-65`, and produce case (a), not (b). For Reject the hold sits on the second task
      read (`:67`; a call-counting hold, "Task-repository wrappers" above), or the test sends a non-blank comment / wires no version repository so the pre-check does not run;
      the WP states which.

    **RequestInfo** (`RequestInfoAsync`, `:524`) has its own order: replay lookup `:539-547`, then
    `LoadTransitionContextAsync` `:549` (task read `:732`, instance, assignment snapshot), stale `expectedVersion` → 409
    `WORKFLOW_TRANSITION_CONFLICT` (`:558-561`), not decidable → 409 `WORKFLOW_TASK_INVALID_STATE` (`:562-565`), then the
    candidate and start checks and the non-terminal write (`PersistAndLogAsync` `:593` →
    `WorkflowTerminalTransitionWriter.CommitTransitionAsync`, `WorkflowTerminalTransitionWriter.cs:273@4c08fc75a`).
    RequestInfo leaves the task decidable: it sets `WaitingEvidence` (`:579`) and `IsDecidable` includes
    `WaitingEvidence` (`:846-858`).
    - **(a)** — as for Approve/Reject: the lookup `:539-547` finds the row and answers through the actor-bound replay
      (`:546`) → 200 `isIdempotent = true`.
    - **(b) late arrival** — held between the lookup `:539-547` and the task read `:732` until the twin has committed
      (same hold pattern as Approve/Reject (b)). RequestInfo case (b) is **not** part of O-S17-3 (interim check 06 F-1);
      its answers below hold under both options. With a stale `expectedVersion` → 409 `WORKFLOW_TRANSITION_CONFLICT`
      (`:558-561`), as today: `Unknown` (§17.7/6), and a replay of the key is then case (a) → 200. No
      `WORKFLOW_TASK_INVALID_STATE` arises here (the task stays decidable). Without `expectedVersion` the
      late call meets a still-decidable task, so `:562-565` does not refuse it and it reaches the write with a second log
      under the same (task, `RequestInfo`, key). **Today** the unique index
      `ux_workflow_transition_logs_task_action_idempotency_active`
      (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.WorkflowWorkCenter.cs:212-220@4c08fc75a`)
      may refuse that log; the writer catches only `TerminalConflictException` and `PlatformTransactionUnavailableException`
      (`WorkflowTerminalTransitionWriter.cs:333-340`), so today's answer is not defined by the code read here (not
      measured). **AC-W7 asserts (DL-2 base scope, item 17 — the duplicate-key mapping):** one row for (task,
      `RequestInfo`, key); the identical twin gets 200 `isIdempotent = true` with the twin's transition; a different
      actor → 403 (item 7); the same actor with a different body → the sequential AC-W14 409 (item 7). Sabotage: removing
      the mapping turns it red.
    - **(c) in-transaction overlap** — both calls past `:539-547` and `:562-565` before either commits: the loser's
      version-guarded update fails inside `CommitTransitionAsync` → Conflict
      (`WorkflowTerminalTransitionWriter.cs:318-322, 333-336@4c08fc75a`); transaction-unavailable goes to the same
      re-read (`:337-340`) as Approve/Reject (c). Read, not run; the WP measures it as in Approve/Reject (c).

    **Test helpers.** The case-(b) holds wrap the task repository: in
    `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowCandidateDecisionMongoTests.cs@4c08fc75a`
    only `ApproveAsync` takes `tasks:` (`:518-520`); `RejectAsync` (`:528-535`) and `RequestInfoAsync` (`:537-542`) do
    not. The WP extends both (tests are an allowed path, item 14). `RejectAsync` sends a non-blank comment and wires no
    version repository, so the Reject pre-check above does not run there. **None of the three helpers passes
    `expectedVersion`** (`ApproveAsync` `:526`, `RejectAsync` `:535`, `RequestInfoAsync` `:542` build the request
    without it; the request records take it as the optional trailing `ExpectedVersion`,
    `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs:473, 483, 512@4c08fc75a`):
    the WP adds an `expectedVersion` parameter to `ApproveAsync`, `RejectAsync` and `RequestInfoAsync`
    (`WorkflowCandidateDecisionMongoTests.cs:518-542`) for the stale-version cases. `RequestInfoAsync` (`:537-542`) also
    gains `target` and `comment` parameters — today it sends target `null` and comment `"please add"` fixed — for the
    `TargetPrincipalId` collision tests of item 7 (interim check 09 F-2).

    **Replay after each case (review 5512cbca F-2; read, not run, @4c08fc75a):** a replay of the same key is case (a)
    only after a branch that recorded a row under (task, action, key) — (a), (b), the barrier race and the injected
    200-Replayed branch of (c), and a 409 whose twin committed. After the injected 503 "nothing moved" branch of (c) no
    row exists (zero rows, above): the next attempt with the same key is a **first write** — the lookup (`:61-65`;
    RequestInfo `:539-547`) finds nothing, the full check order runs and, if it commits, the answer is 200
    `isIdempotent = false` — `Replayed` is unset (default `false`, `WorkflowTerminalTransitionWriter.cs:21-22`) at each
    path's commit point: Approve/Reject final step `CommitAsync` (`WorkflowTerminalTransitionWriter.cs:106`), Approve
    intermediate step `CommitStepAsync` (`:249`), RequestInfo (and Delegate) `CommitTransitionAsync` (`:331`); mapped at
    `WorkflowTaskTransitionSupport.cs:830` (pre-review 1 F-3). The WP asserts `isIdempotent = false` on the retry after
    the zero-row branch for the **final step, the intermediate step and RequestInfo** each; the existing zero-row test
    covers only the Approve final step and asserts the 503 only (`Fix1.cs:156-163`).

    **Existing coverage and what the WP adds (replaces "no existing test covers AC-W6/W7 or AC-W9/W10", note DL-2/7;
    interim check 03 F-1; read, not run, @4c08fc75a).** Reused as evidence: the case-(c) tests and the
    transaction-unavailable twin tests named in Approve/Reject (c) above and in item 9; sequential case (a) for Approve,
    Reject and RequestInfo exists only on in-memory doubles
    (`services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowTaskTransitionTests.cs:45-59, 97-115,
    199, 619`), which do not reproduce the unique index or `ExecutionFilter`, so the WP repeats case (a) on Mongo. The WP
    adds only: (1) the RequestInfo cases (a)–(c) on Mongo; (2) the barrier-forced identical same-key overlap (case (c)) for
    Approve, Reject and RequestInfo; (3) the case-(b) late arrivals of all three; (4) approve-vs-reject at the
    intermediate step (item 9). **AC-W9 / AC-W10: not measured** — the in-memory replays above repeat a decision after
    its task closed, but no Mongo test was found by this read that replays after a next step (AC-W10) or replays a start
    after each terminal state (AC-W9); the WP measures, names any existing test it reuses, and writes the rest.
11. **AC-W5 — what it requires (§17.4 AC-W5; restated for review 5512cbca F-3 and pre-review 1 F-2; read, not run,
    @4c08fc75a):**
    the existing test (`WorkflowStartRecoveryMongoTests.cs:1821-1845@4c08fc75a`) runs six in-process handler calls through
    `AllReservedBarrier` (`:1783-1819`). That barrier holds each caller only **before** its first idempotency-key read
    (`:1790-1801`; the read itself `:1803`, i.e. `StartWorkflowInstanceHandler.cs:81`); after release one call can
    reserve and finish before another reads the key, which then finds the instance and answers as the replay
    (`StartWorkflowInstanceHandler.cs:82-85`). Its non-vacuity assertion (`:1835`) proves only that all callers reached
    the barrier, so the test does not exclude a sequential execution: it is **supporting evidence only**. AC-W5 needs
    **both**: (1) a forced-overlap test on a real Mongo replica set that **measures** that at least two callers missed the
    key read and entered the reservation race. **The only forcing form (interim check 09 F-1):** the hold sits **after**
    the idempotency-key read, in a wrapper of `IWorkflowInstanceRepository.ReserveTrustedStartAsync`
    (`StartWorkflowInstanceHandler.cs:291`); it is released when all callers have read the key and got `null`, so every
    caller then reserves and the unique index makes the losers' reservations return `Created == false`
    (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs:262-279@4c08fc75a`).
    **Measured, not inferred (pre-review 1 F-2):** with N callers, the wrapper counts (a) the callers whose
    `GetByIdempotencyKeyAsync` (`StartWorkflowInstanceHandler.cs:81`) returned `null` and (b) the callers that entered
    the hold in `ReserveTrustedStartAsync`, and records whether the hold was released because (b) reached N with (a) = N
    or because its timeout fired. The test asserts **(a) = N and (b) = N exactly**, the release by arrival (not by
    timeout), exactly one reservation with `Created == true` and N − 1 with `Created == false` (`:292-295`), next to the
    existing end-state assertions. The hold has a timeout like the existing barrier's (`Task.WhenAny` with 10 s,
    `WorkflowStartRecoveryMongoTests.cs:1800`): a caller that returns before reaching the reservation (e.g. 404
    `NotFoundNonLeakage` from the template read, `StartWorkflowInstanceHandler.cs:88-95`, or a validation refusal) does
    not hang the test — the hold releases at the timeout and the test fails with a message naming that caller (its
    index) and its answer (status code, reason code). `AllReservedBarrier` (hold before the key read,
    `WorkflowStartRecoveryMongoTests.cs:1790-1803`) stays supporting evidence only. Sabotage: removing the hold → (b) = 0
    and no release by arrival, red; moving it back before the key read → at the release no key read has run yet
    ((a) = 0 when (b) reaches N), so "released by arrival with (a) = N" is false and the test turns red, whatever the
    later reads find;
    **and** (2) N ≥ 2 independent concurrent `POST instances` requests without a barrier, repeated
    (count stated in the WP), with the same end-state assertions. Neither replaces the other.
12. **Trusted coordinators out of DL-2 scope, unchanged (INSPECTION.md DL-2/5):**
    `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Services/WorkflowInstanceStartCoordinator.cs`
    already compares the start fingerprint (`:228-255@4c08fc75a`, 409 `WORKFLOW_START_IDEMPOTENCY_CONFLICT`);
    `.../Services/TrustedWorkflowCancellationCoordinator.cs` compares its fingerprint through `EvidenceRef`
    (`:104, 288, 293@4c08fc75a`) with 409 `WORKFLOW_TRUSTED_CANCEL_CONFLICT` (`:360-362`). Neither file nor code changes in
    DL-2; neither is in its allowed paths.
13. **AC-W17 consumers — extends §17.8 (INSPECTION.md DL-2/6):** add the Platform-internal callers, which may send one
    key with changed facts: Tasks (`.../Features/Tasks/Services/TaskApprovalService.cs:245-252, 378`,
    `TaskReviewService.cs:315-322, 432`, `TaskUpwardRequestService.cs:119-125`), Timesheet
    (`.../Features/TimeEntry/Services/TimesheetApprovalService.cs:164-173`; it **expects 200** for a key it has seen and
    refuses a closed instance at `:188-197`; a new 409 would reach its failure branch `:181-186`) and the WorkCenter
    dispatcher (`.../Features/WorkAggregation/Providers/WorkflowApprovalWorkItemActionDispatcher.cs`); and Diten.Web,
    whose key is user-editable (`frontend/Diten.Web/wwwroot/assets/js/Platform/Workflow/workflow.api.js:127-136`,
    inputs `frontend/Diten.Web/Views/Platform/Workflow/Index.cshtml:313, 333`) (all @4c08fc75a). **Cancel callers, for
    the AC-W18 change (interim check 02 F-5):** Timesheet withdraw cancels each open task through MOD-0023's
    `CancelWorkflowTaskCommand` with key `timesheet-withdraw:{approvalTaskId}` and the withdrawing user as actor, and
    returns `false` on any non-success answer (`TimesheetApprovalService.cs:256-270`); Diten.Web `cancelTask`
    (`workflow.api.js:136`; callers `.../Platform/Workflow/index.js:1004`, `tasks.js:380`) sends a user-editable key.
    **Further cancel callers (interim check 03 F-2; read, not run, @4c08fc75a)** — each key is per task or per round,
    not per actor, so a second actor can send the same key:
    - Tasks approval withdraw: `CancelWorkflowTaskCommand` with the current user as actor and key
      `task-approval-cancel:{approvalTaskId}`
      (`services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Services/TaskApprovalService.cs:372-378`).
    - Tasks review withdraw: the same with `task-review-cancel:{reviewTaskId}` (`.../Tasks/Services/TaskReviewService.cs:426-432`).
    - Orphan sweep: system actor `SweepWorkflowOrphansCommand.SystemActor`, key `workflow-orphan-recovery:{taskId:N}`,
      `AllowEscalated: true` (`.../Features/Tasks/Recovery/SweepWorkflowOrphansHandler.cs:87-95`).
    - CRM: `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Approvals/WorkflowApprovalCore.cs:229-236`
      (key `{round key}:withdraw`; `Forbidden` → 403 `ApprovalForbidden`) through
      `services/Diten.CrmService/src/Diten.CrmService.Infrastructure/Workflow/GatewayClaimWorkflowClient.cs:171-175`
      (401/403 → `Forbidden`, `:180`).
    - MDM: `services/Diten.MdmService/src/Diten.MdmService.Application/Features/Approvals/WorkflowApprovalCore.cs:302`
      (`Forbidden` → 403 `ApprovalForbidden`, `:308-309`) through
      `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/GatewayWorkflowClient.cs:172-175` (401/403 →
      `Forbidden`, `:181`).
    - Inventory: `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Workflow/GatewayWorkflowClient.cs:157-161`
      (no actor sent; MOD-0023 binds it; a 403 is a non-success result), port
      `.../Application/Features/Quarantine/Ports/QuarantineWorkflowPort.cs:61`, caller
      `.../Application/Features/Quarantine/Handlers/WorkflowHandlers.cs:136` (key `quarantine:withdraw:{decisionId:N}`).
    **AC-W17 consumer check, extended to AC-W18:** after AC-W18 another actor's replay of the same cancel key gets 403
    `WORKFLOW_ACTOR_DENIED` instead of today's 200. Each cancel caller's owner — Platform Tasks (approval, review, orphan
    sweep), Timesheet, Diten.Web, CRM, MDM, Inventory — records with its own evidence (a named test, or "not tested")
    whether its caller can send that case and what its caller then does (e.g. CRM/MDM report 403 `ApprovalForbidden` to
    the withdrawing user; Timesheet returns `false`). AC-W17 and AC-W18 do not pass while a cancel caller's result is
    missing. Each owner records its result as AC-W17 says. These callers are **not** DL-2 allowed paths: if one must change, stop and ask its owner
    (XMC-001).
14. **DL-2 allowed paths — replaces §17.8/9 DL-2 bullet (INSPECTION.md DL-2/8):** under
    `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/`:
    `Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs`, `Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs`,
    `Handlers/CommandHandlers/WorkflowTerminalTransitionWriter.cs` (under both O-S17-3 options, for the RequestInfo
    duplicate-key mapping of item 17 — DL-2 base scope, interim check 06 F-1 — and for sharing `SamePayload`),
    `WorkflowModels.cs`, `Handlers/CommandHandlers/RejectWorkflowTaskHandler.cs` (replay lookup `:79-80`), and only if
    needed the thin per-action handlers `Handlers/CommandHandlers/ApproveWorkflowTaskHandler.cs`,
    `DelegateWorkflowTaskHandler.cs`, `RequestInfoWorkflowTaskHandler.cs`, `CancelWorkflowTaskHandler.cs`; plus
    `services/Diten.Platform/tests/**` and `tests/architecture/**` (new rules only); and
    `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Workflow/WorkflowTransitionLog.cs` — only the additive
    nullable `TargetPrincipalId` property of item 7 (review 5512cbca F-1; no index, no schema-manifest change). Nothing
    else.
15. **AC-W16 restated (DL-1; review F-1) — replaces the AC-W16 text of §17.8.** Code (read, not run, @4c08fc75a):
    `by-objects` answers every requested object id; an id with no instance in the caller's tenant gets an entry with an
    empty `instances` list and the call is 200
    (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetWorkflowInstancesByObjectsHandler.cs:55-72@4c08fc75a`;
    tenant from `ExecutionFilter` in `ListByObjectIdsAsync`,
    `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs:181-185@4c08fc75a`).
    `history` and `instances/{id}` answer 404 `NotFoundNonLeakage` for an instance not found in the tenant
    (`.../QueryHandlers/GetWorkflowInstanceHistoryHandler.cs:51-59@4c08fc75a`,
    `.../QueryHandlers/GetWorkflowInstanceByIdHandler.cs:20-28@4c08fc75a`). All three actions require `instances.view`
    (`services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs:96, 105, 122@4c08fc75a`).
    **AC-W16 — `includeDeleted` stays inside the tenant and the permission (DL-1):**
    - `by-objects`, with or without `includeDeleted=true`: another tenant's object id (fixture: that tenant holds a live and
      a soft-deleted instance for it) → its entry has an empty `instances` list, 200; no row of another tenant appears in
      any entry.
    - `history` and `instances/{id}` for another tenant's instance id → 404 `NotFoundNonLeakage`, as today (`history`
      takes no `includeDeleted`, item 3).
    - A caller without `instances.view` → refused as today, on all three actions.
    - Sabotage: dropping the tenant condition on the `includeDeleted` branch (the new read member of item 2) turns it red.
16. **AC-W2 restated (DL-1; interim check 02 F-1) — replaces the tenant clause of §17.4 AC-W2 and §17.3/3's "same
    404".** §17.4's "other-tenant id → 404 unchanged" is wrong for `by-objects`, which never answers 404 for a foreign
    object (item 15). **AC-W2:** `by-objects` and `history` return the stored `idempotencyKey` (and `approvalTaskId` on
    `history`) for keyed writes and `null` for unkeyed ones. Tenant clause, unchanged from today:
    - `history` and `instances/{id}` for another tenant's instance id → 404 `NotFoundNonLeakage`
      (`GetWorkflowInstanceHistoryHandler.cs:51-59@4c08fc75a`, `GetWorkflowInstanceByIdHandler.cs:20-28@4c08fc75a`;
      existing test `WorkflowCrossServiceTests.cs:616-625@4c08fc75a`);
    - `by-objects` for another tenant's object → 200, that object's entry with an empty `instances` list
      (`GetWorkflowInstancesByObjectsHandler.cs:55-72@4c08fc75a`; existing assertion
      `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowCrossServiceTests.cs:456-457@4c08fc75a`);
      no row of another tenant appears.
    `instances/{id}` + `idempotencyKey` stays optional (§17.7/4) and is not asserted. The `includeDeleted` variant is
    AC-W16 (item 15).
17. **RequestInfo duplicate-key mapping (DL-2 base scope) and owner decision O-S17-3 — an identical late same-key retry
    answered 409 `WORKFLOW_TASK_INVALID_STATE` (DL-2; interim check 02 F-2, interim check 06 F-1) — O-S17-3 open.**
    - **RequestInfo duplicate-key mapping — DL-2 base scope under both O-S17-3 options (proposal; the owner approves it
      with this pack; formerly (i-b), moved out of O-S17-3 for interim check 06 F-1):** a late identical RequestInfo twin
      without `expectedVersion` is not refused — the task stays decidable (`WorkflowTaskTransitionSupport.cs:846-858`) —
      and reaches the write (`:593` → `CommitTransitionAsync`); its log may meet the unique index
      `ux_workflow_transition_logs_task_action_idempotency_active`
      (`PlatformSchemaManifest.WorkflowWorkCenter.cs:212-220@4c08fc75a`), and the writer catches only
      `TerminalConflictException` and `PlatformTransactionUnavailableException`
      (`WorkflowTerminalTransitionWriter.cs:333-340@4c08fc75a`). The writer maps a duplicate-key refusal on **that index**
      for (task, `RequestInfo`, key) to an actor-bound replay read of that row: same actor and same payload (as
      `MyCommittedLogAsync` compares them, `:399-438`, plus `TargetPrincipalId`, item 7) → 200 `isIdempotent = true`; another actor → 403
      `WORKFLOW_ACTOR_DENIED` (item 7); same actor, different payload → the sequential different-body answer (AC-W14,
      item 7). The unavailable path's own re-read cannot serve the actor case: it would answer 503
      `WORKFLOW_TRANSACTION_UNAVAILABLE` here, not 403 — `MyCommittedLogAsync` finds no row of this actor, the task version
      does not move and after `LostRaceReadAttempts` the writer returns `TransactionUnavailable`,
      `WorkflowTerminalTransitionWriter.cs:357-390`, mapped at `WorkflowTaskTransitionSupport.cs:834-838` (interim check
      04 F-1) — so the WP routes this case to the actor-bound replay `WorkflowTaskTransitionSupport.cs:1018-1030` and
      states how. Any other write error is unchanged. This makes `WorkflowTerminalTransitionWriter.cs` a DL-2 allowed
      path unconditionally (item 14). AC-W7's RequestInfo case (b) asserts it (item 10).
    - **O-S17-3 covers only Approve/Reject case (b).** There (item 10) an identical retry whose own transition is
      already recorded gets 409 `WORKFLOW_TASK_INVALID_STATE` (`WorkflowTaskTransitionSupport.cs:82-89@4c08fc75a`), or
      409 `WORKFLOW_TRANSITION_CONFLICT` with a stale `expectedVersion` (`:77-80`). §17.7/6 calls the first a definite
      refusal, so a consumer would record `Refused` for a transition that was recorded. RequestInfo's stale-version 409
      (`:558-561`) is not part of it: it stays `WORKFLOW_TRANSITION_CONFLICT`, `Unknown` under §17.7/6 (item 10).
    - **(i) re-read before refusal (DL-2 target):** before answering `WORKFLOW_TASK_INVALID_STATE` (`:82-89`) and before
      `StaleVersion` (`:77-80`), the handler re-reads the (task, action, key) log (`GetByTaskActionIdempotencyKeyAsync`,
      as at `:61`); a recorded twin answers through the actor-bound replay (`:1018-1030`): same actor and body → 200
      `isIdempotent = true`; another actor → 403 (item 7); same actor, different body → the sequential AC-W14 409. This
      fixes the answer of the late identical twin (case (b)) for Approve and Reject: it always meets one of the two
      refusals, because the decided task is no longer decidable, and the re-read turns it into the replay. It does not
      make the answer timing-independent (interim check 07 F-1): in case (c) the identical twin still gets 409
      `WORKFLOW_TRANSITION_CONFLICT` from the version fence (`WorkflowTerminalTransitionWriter.cs:108-110`, mapped at
      `WorkflowTaskTransitionSupport.cs:839-843`), or on the transaction-unavailable path 409 / 503
      `WORKFLOW_TRANSACTION_UNAVAILABLE` when its re-read does not find the twin's row (`WorkflowTerminalTransitionWriter.cs:370-376,
      390`; otherwise 200 Replayed, item 10 (c)) — under both options, `Unknown` (§17.7/6) — and its replay then gets
      case (a) once a row under the key is recorded; after a zero-row 503 it is a first write (item 10, review 5512cbca
      F-2). File: `WorkflowTaskTransitionSupport.cs`. AC-W6/W7 case (b) for Approve/Reject assert 200 (item 10);
      AC-W14's Approve/Reject case (b) asserts 403 / the sequential 409 (item 7); §17.7/6 stays as written
      (`WORKFLOW_TASK_INVALID_STATE` a definite refusal) and no consumer records a new rule.
    - **(ii) consumer rule:** after a same-key retry (any send of a key already sent once), `WORKFLOW_TASK_INVALID_STATE`
      is not a definite refusal → intent `Unknown`, read `history` and match by (task, action, key) (AC-W2). No change to
      the Approve/Reject refusal path; AC-W6/W7 case (b) for Approve/Reject and AC-W14's Approve/Reject case (b) (item 7)
      assert today's answers; §17.7/6 is amended this way, and every AC-W17 consumer and Treasury records the rule.
    - **Items that depend on O-S17-3** (the same list in item 9, this item, the closure and the DL-2 finish list;
      interim check 07 F-2): AC-W6 and AC-W7 case (b) for Approve/Reject; AC-W14 case (b) for Approve/Reject (different
      actor; same actor, different body); the §17.7/6 consumer rule; under (ii) only, the AC-W17 consumer and Treasury
      recording of that rule. AC-W8 does not depend on O-S17-3.
    - Under both options: the RequestInfo mapping above and its AC-W7 / AC-W14 assertions hold; AC-W8 does not depend on
      O-S17-3 (no row under the loser's key, item 9).
    - **Öneri — sahip kararı:** (i). The Approve/Reject late-retry answer is fixed once in the producer with one indexed
      read before each of the two refusals, instead of a rule in every consumer (Treasury, Tasks, Timesheet, WorkCenter,
      Web).
    - **Gate:** the DL-2 WP prompt is not written before O-S17-3 is decided; DL-1 does not depend on it.
18. **AC-W9 restated — replaces the AC-W9 text of §17.4 (Codex review 3 F-1, `2026-10-30-d8a191fe`; owner decision
    S17-5; pre-review (S-93) for request 06 F-1…F-3; read, not run, @4c08fc75a).** Code: §17.4's case "closed by the abandoned-start recovery *after* it was whole"
    cannot be reached. The recovery refuses a generic instance that is not `Active`, moved too recently or has any task
    listed, with 409 `WORKFLOW_START_NOT_ABANDONED`
    (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/CloseAbandonedWorkflowStartHandler.cs:80-91@4c08fc75a`);
    its trusted branch refuses an instance that is not `Pending` or whose checkpoint is `Completed` (`:172-177`). The
    reachable terminal state `TimedOut` (`WorkflowModels.cs:134, 142@4c08fc75a`) was missing: the SLA sweep sets it
    when the step's rule has a due timeout
    (`.../Handlers/CommandHandlers/RunWorkflowEscalationsHandler.cs:241-243, 286-293@4c08fc75a`; its clock is the injected
    `TimeProvider`, `:47, 100`). A whole start's replay answers 200 whatever the instance has become
    (`StartWorkflowInstanceHandler.cs:81-84, 385-391@4c08fc75a`; the answer carries `instance.Status.ToString()`,
    `:785`). An approved instance is `Completed`, not `Approved`: the last approve sets `WorkflowInstanceStatus.Completed`
    (`WorkflowTaskTransitionSupport.cs:214-216@4c08fc75a`), nothing writes `Approved`
    (`.../Features/Workflow/WorkflowInstanceOutcome.cs:9-10@4c08fc75a`), and its outcome is "approved"
    (`WorkflowModels.cs:131, 139`).
    **AC-W9 — replay after a whole start, instance since closed:**
    - Fixture: a generic `POST instances` start with key K that is whole (task, snapshot and start log written), brought
      in turn to each reachable terminal state: **Completed** (outcome "approved"; a single-step template whose candidate
      approves, or every step approved), **Rejected**, **Cancelled** by the user (the public task cancel) and **TimedOut**
      (the SLA sweep). For each of the four, the test reads the instance and asserts that terminal state before the
      replay (non-vacuity). Replay K with the same facts and the same authenticated starter → **200**, the same instance
      id, its current status (`"Completed"`, `"Rejected"`, `"Cancelled"`, `"TimedOut"`); no second instance, no second
      task and no new transition-log row (counts read before and after the replay); no business refusal.
    - Store: AC-W9 runs on real Mongo. In-memory doubles do not reproduce the repository's `ExecutionFilter`, and the
      replay reads the task through it: `ExistingStartResponseAsync` calls `GetFirstByInstanceIdAsync`
      (`StartWorkflowInstanceHandler.cs:372-378@4c08fc75a`;
      `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs:534-540@4c08fc75a`).
    - How `TimedOut` is forced — the existing Mongo pattern "timed-out"
      (`services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowCandidateDecisionMongoTests.TerminalEvidence.cs:785-790, 822-825@4c08fc75a`):
      the start task is already overdue, an SLA rule on its step has `TimeoutAfterMinutes`, then one
      `RunWorkflowEscalationsHandler` run. That helper starts through the trusted coordinator; the WP applies the same
      three steps to the generic start (a past `DueAt` in the start request, `StartWorkflowInstanceHandler.cs:192`, or the
      task's `DueAt` moved through the task repository as `MoveDueAsync` does, `TerminalEvidence.cs:747-754`) and asserts
      the instance is `TimedOut` before the replay (non-vacuity).
    - Separately: **the recovery refuses a whole start** — `CloseAbandonedWorkflowStart` on a whole, aged generic start →
      409 `WORKFLOW_START_NOT_ABANDONED`, the instance stays `Active`. Existing test
      `WorkflowStartRecoveryMongoTests.cs:267-288@4c08fc75a`: the whole start is aged, the sweep leaves it `Active`
      (`:279`) and the direct close answers that 409 (`:283-287`); after the direct close it asserts only status code
      and reason code, it does not re-read the instance or the log. The WP adds, after the direct close: the instance is
      re-read and is still `Active`, and the transition-log count is unchanged (no new row).
    - The never-whole start closed by the recovery stays in AC-W12 (unchanged).
    - Sabotage: answering a non-`Active` instance (any of the four states) with 409 turns the replay test red; letting
      `CloseAbandonedWorkflowStart` close a whole start (its refusal at `CloseAbandonedWorkflowStartHandler.cs:80-91`
      bypassed) turns the refusal test red.

**Review round — `docs/records/reviews/MOD-0023/2026-10-29-3e802d2f.md` (formal review; UYGUN DEĞİL; MOD-0023 owner
`duzelt`, CT · İş akışı chat, 2026-10-29).** Review 3e802d2f F-1 → item 15 (AC-W16 restated); review 3e802d2f F-2 →
items 9 and 10; the CT's note in `docs/records/reviews/MOD-0023/REVIEW-REQUEST-rev3-01.md:44` and interim check 02 F-8
(`ARA-KONTROL-02.md`) → item 2; review 3e802d2f F-3 (review input) needs no pack change. In revision 3
the "Teslim dilimleri" cells also gained the §17.7 heading (DL-1) and the §17.4 heading (DL-2), so both slices now list
the same §17 headings.
**Codex review 2 (2026-10-30-5512cbca)** — `docs/records/reviews/MOD-0023/2026-10-30-5512cbca.md` (formal review;
UYGUN DEĞİL; MOD-0023 owner `duzelt`, CT · İş akışı chat, 2026-10-30, recorded as S17-4 in
`docs/records/decisions/2026-10/mod0023-s17-owner-decisions-02.md`): review 5512cbca F-1 → item 7 (`TargetPrincipalId`
field, legacy rule öneri, AC and test), items 4, 14 and 17, the closure and the DL-2 finish list; F-2 → items 7 (c),
10 (replay after each case) and 17 (i), the DL-2 finish list; F-3 → item 11 and the DL-2 finish list.
**Interim check 09** — `docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-09.md` (sub-agent, not a
formal review; two low findings, accepted by the CT; owner `duzelt`, 2026-10-30): F-1 → item 11 (hold after the key
read, in a `ReserveTrustedStartAsync` wrapper, is the only forcing form) and the DL-2 finish list; F-2 → item 7
(`TargetPrincipalId` on the unavailable re-read path), item 10 (test helpers) and the DL-2 finish list.
**Pre-review 1 (S-93)** (independent pre-review, not a formal review; four low findings, numbered F-1…F-4 in the CT's
order — no record file in the repository at 3ddbbf8f2; accepted by the CT; owner `duzelt`, recorded as S17-4 in
`docs/records/decisions/2026-10/mod0023-s17-owner-decisions-02.md`, 2026-10-30): F-1 → item 7 (legacy-row rule stated
per path, legacy-row AC per path, collision sabotage by direction with a handler-written twin) and the DL-2 finish list;
F-2 → item 11 (counts = N exactly, release by arrival, hold timeout, early-returning caller named) and the DL-2 finish
list; F-3 → item 10 (commit point per path for the first write after a zero-row 503); F-4 → item 11 heading, this
paragraph, the interim check 08 paragraph.
**Codex review 3 (2026-10-30-d8a191fe)** — `docs/records/reviews/MOD-0023/2026-10-30-d8a191fe.md` (formal review;
KOŞULLU; MOD-0023 owner «Düzelt», CT · İş akışı chat, 2026-10-30, recorded as S17-5 in
`docs/records/decisions/2026-10/mod0023-s17-owner-decisions-03.md`): F-1 → item 18 (AC-W9 restated: the four reachable
terminal states incl. `TimedOut`; the recovery's refusal of a whole start asserted separately), the closure and the DL-2
finish list.
**Pre-review (S-93) for request 06** (independent pre-review, not a formal review; three findings, accepted by the CT;
authority: owner decision S17-5 «Düzelt», `mod0023-s17-owner-decisions-03.md`): F-1 → item 18 (`Completed`, not
`Approved`; terminal state asserted before the replay for all four) and the DL-2 finish list; F-2 → item 18 (assertions
added after the direct close, refusal sabotage, real Mongo); F-3 → the DL-2 finish list (AC-W11 added).

Interim-check findings below are named "interim check 02 F-n" / "interim check 03 F-n" / "interim check 04 F-n" /
"interim check 05 F-n" / "interim check 06 F-n" / "interim check 07 F-n" / "interim check 08 F-n" / "interim check 09 F-n"
and "pre-review 1 F-n" in the items; they are not the formal review's F-n above.

**Interim check 02 — `docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-02.md` (sub-agent, not a formal
review; all findings accepted by the CT; owner `duzelt`).** Interim check 02 F-1 → item 16 and the DL-1 finish list;
F-2 → item 17 (O-S17-3, open) and items 9, 10; F-3 → item 10 (b) and the test-helper paragraph; F-4 → items 9, 10;
F-5 → items 5, 8, 13; F-6 → item 4; F-7 → item 5 (Inventory test cite), other cites re-checked, no change; F-8 → this
paragraph, the §17 status paragraph, items 2, 3 and the closure.

**Interim check 03 — `docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-03.md` (sub-agent, not a formal
review; KOŞULLU, no blocker; all findings accepted by the CT; owner `duzelt`).** Interim check 03 F-1 → item 9 (existing
tests), item 10 (Approve/Reject (c) "already covered", the existing-coverage paragraph replacing "no existing test
covers AC-W6/W7 or AC-W9/W10"); F-2 → item 13 (further cancel callers, AC-W17 extended to AC-W18); F-3 → item 17
((i-a)/(i-b), recommendation reason), item 10 (RequestInfo (b)), item 14 (writer path); F-4 → item 10 (task-repository
wrappers, Reject call-counting hold); F-5 → item 4 (`ObjectId`); F-6 → this paragraph; F-7 → item 1(c); F-8 → the
"seviye gerekçesi" cells of "Teslim dilimleri" (MDM test contents: the CT adds them to the review input; no pack change).

**Interim check 04 — `docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-04.md` (sub-agent, not a formal
review; KOŞULLU, no blocker; all findings accepted by the CT; owner `duzelt`).** Interim check 04 F-1 → item 17 ((i-b):
503, not 409); F-2 → items 9, 17, the closure and the DL-2 finish list (AC-W8 does not depend on O-S17-3; only AC-W6/W7
and the consumer rule do — and AC-W14's different-actor case (b), added by interim check 05 F-1); F-3 → the §17 status paragraph (approval row, #241), item 2 and the review-round paragraph
(source of the item-2 note); F-4 → item 4 (`ReasonCode`, Timesheet's Guid parsing).

**Interim check 05 — `docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-05.md` (sub-agent, not a formal
review; KOŞULLU, no blocker; both findings accepted by the CT; owner `duzelt`).** Interim check 05 F-1 → item 7 (AC-W14
actor case by timing case), item 17, the interim check 04 paragraph, the closure and the DL-2 finish list (AC-W14's
different-actor case (b) depends on O-S17-3); F-2 → item 4 (boundary of the list, inherited fields).

**Interim check 06 — `docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-06.md` (sub-agent, not a formal
review; KOŞULLU, no blocker; both findings accepted with CT direction; owner `duzelt`).** Interim check 06 F-1 → item 17
(the former (i-b) RequestInfo duplicate-key mapping is DL-2 base scope under both options; O-S17-3 covers only
Approve/Reject case (b); recommendation reason), items 7, 9, 10 (RequestInfo (b) as an AC-W7 assertion), 14 (writer path
unconditional), the closure and the DL-2 finish list; F-2 → item 7 (AC-W14's distinct different-body code on the
sequential path only; concurrent path `WORKFLOW_TRANSITION_CONFLICT`, existing tests unchanged), the closure and the DL-2
finish list. The "(i-a)" / "(i-b)" names in the interim check 03 and 04 paragraphs are historical.

**Interim check 07 — `docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-07.md` (sub-agent, not a formal
review; KOŞULLU, low findings only; all four accepted by the CT; owner `duzelt`).** Interim check 07 F-1 → item 17 (i)
(timing-independent only for the late identical twin, case (b); case (c) stays 409/503 under both options); F-2 → items
9 and 17, the closure and the DL-2 finish list (one list of O-S17-3-dependent items); F-3 → item 7 (`SignatureId` in
the AC-W14 body, öneri and test); F-4 → item 3 and the closure (decision record rows S17-1…S17-3 vs open items
O-S17-1 / O-S17-3).

**Interim check 08 — `docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-08.md` (sub-agent, not a formal
review; KOŞULLU, low findings only; all four accepted by the CT; owner `duzelt`; after it, review 5512cbca, interim
check 09 and pre-review 1 followed — see above).** Interim
check 08 F-1 → items 9 and 10 (rows per forced branch: the injected 503 "nothing moved" branch writes zero rows); F-2 →
item 7 (AC-W11's transition sentence restated); F-3 → item 7 (RequestInfo `TargetPrincipalId` in the AC-W14 body), the
closure and the DL-2 finish list; F-4 → item 7 (Delegate's replay unchanged, öneri), the closure and the DL-2 finish list.

**Closure (replaces the §17.8 closure where they conflict):** AC-W1…W18 as restated here (AC-W2 by item 16, AC-W6/W7
by items 10 and 17 — RequestInfo case (b) is asserted through the duplicate-key mapping, DL-2 base scope under both
options —, AC-W14 by items 7 and 17 — its distinct different-body code applies on the sequential path only; `SignatureId`
is part of the compared body (öneri, item 7), and so is RequestInfo `TargetPrincipalId` as its own log field — on a legacy
row (field `null`) the target is compared only through `Comment`, on each of the three paths (öneri, item 7) —, AC-W5
by item 11 (counts = N exactly, release by arrival),
AC-W8 by item 9; AC-W9 by item 18; AC-W11's transition sentence by item 7; Delegate's same-key replay unchanged and outside AC-W14/AC-W17,
öneri, item 7). Items that depend on O-S17-3 (the same list as items 9
and 17 and the DL-2 finish list): AC-W6 and AC-W7 case (b) for Approve/Reject; AC-W14 case (b) for Approve/Reject
(different actor; same actor, different body); the §17.7/6 consumer rule; under (ii) only, the AC-W17 consumer and
Treasury recording of that rule. AC-W8 does not depend on O-S17-3. O-S17-2 is decided (b) (record
`docs/records/decisions/2026-10/mod0023-s17-owner-decisions-01.md`, S17-2) and no longer a gate. That record numbers its
rows S17-1…S17-3 as decisions taken (`rev3-yaz`, `gecmis-404`, `duzelt`); only S17-2 decides an open item (O-S17-2);
rows S17-1 and S17-3 do not decide O-S17-1 or O-S17-3, which stay open. **O-S17-3 is open and must be decided before
the DL-2 WP prompt**; the rest of the §17.8 closure unchanged.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | seviye gerekçesi |
|---|---|---|---|---|
| DL-1 | §17 silmeme politikası + eklemeli okuma alanları | Change, Acceptance criteria (defined; not implemented or run), Producer answer and owner decisions D25 (2026-10-25) — supersedes §17.1–17.6 where they conflict, Producer review points and owner decisions D32 (2026-10-26) — supersedes §17.1–17.7 where they conflict, Revision 3 — code inspection 01 findings (2026-10-29) — supersedes §17.1–17.8 where they conflict, Delivery | E4 | silinmiş satır okuması gerçek Mongo deposunda (sahte depo kanıt değil, §17.9/1), kiracı sınırı ve `instances.view` (AC-W16), 404 hata yolu; kartın E4 satırı: "+ persistence/RBAC/tenant/audit/failure paths" (`docs/guides/operations/control-tower-operating-card.md:406`) |
| DL-2 | §17 aynı anahtar yanıtı: yarış, sıralı tekrar ve fingerprint düzeltmesi | Change, Acceptance criteria (defined; not implemented or run), Producer answer and owner decisions D25 (2026-10-25) — supersedes §17.1–17.6 where they conflict, Producer review points and owner decisions D32 (2026-10-26) — supersedes §17.1–17.7 where they conflict, Revision 3 — code inspection 01 findings (2026-10-29) — supersedes §17.1–17.8 where they conflict, Delivery | E4 | yazma yolu değişikliği: gerçek Mongo replika kümesinde yarış ve tekrar, 403/409/503 hata yolları, tek satır kalıcılığı (AC-W5…W12, W14, W18); kartın E4 satırı: "+ persistence/RBAC/tenant/audit/failure paths" (`docs/guides/operations/control-tower-operating-card.md:406`) |

Bu tablo **yalnız §17 ekini** kapsar (CT-APR-02 bulgusu F-1, 2026-10-25); §1–§16'nın eski kapsamı ve mevcut `ready-for-dev`
durumu bu dilimlere yetki vermez. Teslim: MOD-0023 sahibinin WP'si (D25/1); kodu sahibi yazar, başka biri merge eder (D32/1).
Bitiş — **DL-1:** AC-W1 (üç tip), AC-W2, AC-W3 (tüketici sahiplerinin kanıtıyla, MDM dahil), AC-W4, AC-W13, AC-W15, AC-W16
(AC-W2, W3, W13, W15, W16 §17.9'daki haliyle; O-S17-2 kararı verildi — (b), `mod0023-s17-owner-decisions-01.md` S17-2 —
artık kapı değil).
**DL-2:** AC-W5…AC-W12, AC-W14, AC-W17, AC-W18 (AC-W5…W9, W14, W17, W18 §17.9'daki haliyle; AC-W11'in geçiş cümlesi §17.9/7'deki haliyle; O-S17-3 kararına bağlı
olanlar — §17.9/9, §17.9/17 ve kapanıştaki listeyle aynı: AC-W6 ve AC-W7'nin Approve/Reject (b) vakası; AC-W14'ün
Approve/Reject (b) vakası (farklı aktör; aynı aktör, farklı gövde); §17.7/6 tüketici kuralı; yalnız (ii)'de AC-W17
tüketicilerinin ve Treasury'nin bu kuralı kaydetmesi; AC-W14 gövdesine `SignatureId` (öneri) ve RequestInfo `TargetPrincipalId` (yeni ayrı log alanı `WorkflowTransitionLog.TargetPrincipalId`, `Comment` biçimi değişmez; eski satırda (alan `null`) hedef ayrıca karşılaştırılmaz, yalnız `Comment` üzerinden karşılaştırılır — üç yolun her birinde, öneri; hedef karşılaştırması ve eski satır kabulü sıralı, yinelenen anahtar ve kullanılamaz yeniden okuma (`SamePayload`) yollarında ayrı ayrı test edilir; `null` yazma sabotajında ikiz satır işleyicinin yazdığı satırdan alınır) girer, §17.9/7, §17.9/14; Delegate'in aynı anahtar tekrarı değişmez, AC-W14/AC-W17 dışında (öneri, §17.9/7); zorlanan 503 "hiçbir şey değişmedi" dalı sıfır satır yazar ve aynı anahtarla sonraki deneme ilk yazmadır (`isIdempotent = false`; son adım, ara adım ve RequestInfo için ayrı ayrı), §17.9/9–10; AC-W5 zorlanan yarışta en az iki çağıranın anahtar okumasını kaçırıp rezervasyon yarışına girdiğini ölçer — tek zorlama biçimi: bekletme anahtar okumasından sonra, `ReserveTrustedStartAsync` sarmalayıcısında; `null` okuyan ve bekletmeye giren çağıran sayısı tam N, bırakma zaman aşımıyla değil varışla; bekletmenin zaman aşımı var, erken dönen çağıran adıyla testi düşürür —, mevcut test yalnız destekleyici kanıttır, §17.9/11; AC-W9 gerçek Mongo'da, bütün başlangıçtan sonra dört ulaşılabilir terminal durumda (Completed — sonuç "approved", tek adımlı şablon ya da her adım onaylı —, Rejected, kullanıcı Cancelled, TimedOut — SLA taramasıyla zorlanır; dördünde de durum tekrardan önce doğrulanır) aynı anahtar tekrarına 200, aynı örnek, ikinci örnek/görev/log yok; kurtarmanın bütün başlangıcı 409 `WORKFLOW_START_NOT_ABANDONED` ile reddi ayrıca doğrulanır (mevcut test; WP doğrudan kapatmadan sonra örneğin hâlâ `Active` olduğunu ve yeni log satırı olmadığını ekler, ret sabotajıyla); hiç bütünleşmemiş başlangıç AC-W12'de, §17.9/18; RequestInfo yinelenen anahtar eşlemesi her iki seçenekte DL-2 temel
kapsamı, AC-W7 onu doğrular, §17.9/17; AC-W14'ün ayrı farklı-gövde 409 kodu yalnız sıralı yolda, eşzamanlı yolda
`WORKFLOW_TRANSITION_CONFLICT`, §17.9/7; AC-W8 bağlı değil, §17.9/9; DL-2 prompt'u O-S17-3 kararından önce yazılmaz) (fingerprint düzeltmesi bir yazma yolu değişikliğidir ve yalnız bu dilimde, sahibin
onayıyla yapılır — D25/1). Treasury'nin MOD-0023 kullanan dilimleri DL-1 **ve** DL-2 main'e girmeden kabul edilmez.
