---
id: MOD-0024
name: Task & Checklist Engine
slice: Task Creation & Self-Task Runtime (backend + form)
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: TenantScopedEntity
status: ready-for-dev
status_changed: draft -> ready-for-dev on 2026-07-25 (EA/user-approved; DEV-1 deviation signed off; OD-1..OD-4 resolved)
owner: platform-team
branch: feature/pss/mod-0024-task-engine-create
started: 2026-07-25
target: TBD
form_field_count: 20
supersedes_slice: "MOD-0024-task-checklist-engine.md (frontend-only fixture/resolver slice) — that pack §2 reserved this backend slice"
---

# MOD-0024 — Task Creation & Self-Task Runtime (backend + form)

> **Identity (DCP-002).** `MOD-0024` is Blueprint-canonical ("Task & Checklist Engine"). No new ID is minted.
> Preflight `verify_module_id.py --check-id MOD-0024 --name "Task & Checklist Engine"` → **exit 0** (2026-07-25).
> This is **MOD-0024's own** runtime slice, not `CAND-CAP-0006`: the Task Center only *renders* work
> ([DCP-004](../../../portfolio/delivery-capability-packs/DCP-004-work-aggregation-task-center.md) §4). The
> existing frontend-only pack [`MOD-0024-task-checklist-engine.md`](MOD-0024-task-checklist-engine.md) §2
> explicitly reserved this slice ("in a later separately approved backend slice").
>
> **This pack is `ready-for-dev`** (EA/user 2026-07-25). Implementation proceeds **phase by phase** (§19);
> each phase after Phase 1 needs its own go-ahead. Signed off at approval: the **DEV-1** deviation (§12 K9 —
> quick-create offcanvas under `golden_reference: compact`) and **OD-1…OD-4** (§18).
> External blockers outside this slice: the Ocelot route (§15) and the MOD-0018 permission seed (§14).

## 1. Module Summary

MOD-0024 becomes a real **task producer**: users can create tasks (for themselves, for a person, or for a
position-based pool), the engine owns their native lifecycle, and it publishes them to the Task Center as a
**second `IWorkItemProvider`** so İşlerim/Havuz fill with real data instead of fixtures.

The engine is deliberately **generic**. Domain-specific columns (Phase, Work Type, Market/Country, Domain,
External Party) are **configurable field definitions**, never hard-coded columns (§12 K1) — otherwise every
consuming module (Finance "Fiscal Period", HCM "Position Code") would demand a new column and the engine would
collapse into a union of every module's form.

Two form depths (§12 K9): a **quick** create (offcanvas, 4 fields) and the **detailed** full-page form (20
fields). One draft, no data loss when switching.

`golden_reference: compact` + `form_field_count: 20` — the detailed form is the primary create/edit surface and
exceeds 8 fields, so Compact (separate `Create/Edit/Details` pages) is mandatory.

## 2. Ownership and Boundaries

### MOD-0024 owns
- `TaskItem` native lifecycle (`Open|Planned|InProgress|Waiting|PendingReview|Done|Cancelled`) and its
  normalized projection.
- Assignment intent (self / person / pool-position), ownership + admission transitions (accept, claim, release).
- Checklist primitives (`ChecklistTemplate`, `ChecklistRun`) and subtasks — MOD-0024 is their **source**.
- Its own dependency edges between **its own** tasks (§12 Y3).
- Configurable field **definitions and values** (§12 K1).
- Its own `IWorkItemProvider` projection into the Task Center.

### MOD-0024 does NOT own (consume, never re-implement)
| Concern | Owner |
|---|---|
| Approval / review state machine, SLA, escalation | **MOD-0023** — Binding A (§12 K2); MOD-0024 never writes a second approval engine |
| Permission grant, delegation **eligibility**, data-scope decisions | **MOD-0018** / AuthService (§12 Y5) |
| Position / OrganizationUnit / PositionAssignment master data | **MOD-0288** (read-only consumer) |
| Email template render + send | **MOD-0027** (`INotificationEventDispatchAdapter`) |
| Recurring job scheduling | **MOD-0026** Hangfire seam (`IRecurringJobRegistrar`) |
| Binary attachment storage | **MOD-0028/0029/0031** or an approved storage provider (§12 Y4 — excluded here) |
| Work-item aggregation, personal overlay, effective-action render | **CAND-CAP-0006 / WC-1** (`Features/WorkAggregation` — **not modified**) |
| In-app notification / header bell | **BL-025** (no in-app channel exists: `NotificationChannelCode { Email = 0 }`) |

## 3. Owned Objects

### Runtime entities (all `TenantScopedEntity`, Mongo, soft delete, tenant-first indexes)

| Entity | Collection | Purpose |
|---|---|---|
| `TaskItem` | `task_items` | The task aggregate (lifecycle, assignment, dates, effort, org context, embedded field values) |
| `TaskAssignment` | `task_assignments` | Append-only assignment/ownership history (assign, accept, claim, release, delegate) |
| `TaskDependency` | `task_dependencies` | Typed edges between MOD-0024 tasks (§12 Y3) |
| `TaskWatcher` | `task_watchers` | Watcher/consultant participation (§12 K3) |
| `TaskFieldDefinition` | `task_field_definitions` | Configurable field catalog (tenant + optional module scope) — §12 K1 |
| `ChecklistTemplate` | `checklist_templates` | Reusable checklist (Phase 2) |
| `ChecklistRun` | `checklist_runs` | A template instantiated on a task (Phase 2) |
| `TaskTemplate` | `task_templates` | Reusable task shape incl. checklist ref (Phase 2 — §12 E5) |
| `TaskRecurrenceRule` | `task_recurrence_rules` | Recurrence definition (Phase 4 — §12 K8) |

> **Naming correction (deliberate).** The predecessor pack said entity `Task`. A domain class named `Task`
> collides with `System.Threading.Tasks.Task` in a codebase where every method returns `Task<T>`, forcing
> alias/qualification in every file. This slice names it **`TaskItem`**; the concept is unchanged.

### CQRS (per §10 convention)
Commands: `CreateTaskItem`, `UpdateTaskItem`, `DeleteTaskItem`, `BulkDeleteTaskItem`, `AcceptTaskItem`,
`ClaimTaskItem`, `ReleaseTaskItem`, `PlanTaskItem`, `StartTaskItem`, `CompleteTaskItem`, `CancelTaskItem`.
Queries: `GetTaskItemList`, `GetTaskItemById`, `GetTaskAssignmentPositionLookup` (§12 K4).

### Providers / services
| Object | Purpose |
|---|---|
| `TaskWorkItemProvider : IWorkItemProvider` | Projects `TaskItem` → canonical work item (§12 K10) |
| `ITaskLifecycleService` | The single owner of lifecycle transitions + the normalize map (§4) |
| `ITaskAssignmentResolver` | Resolves self/person/pool intent into contract fields (§12 K5) |
| `ITaskFieldDefinitionService` | Validates configurable field values against definitions + contract limits |
| `TaskManifestProvider : IModuleManifestProvider` | Pages, permissions **and notification events** (§14, §17) |
| `TaskRecurrenceSweepJob : IBackgroundJobHandler<TaskRecurrenceSweepJobArgs>` | Phase 4 |

### API (`api/v1/tasks`)
`POST /` · `GET /` · `GET /{id}` · `PUT /{id}` · `DELETE /{id}` · `POST /bulk-delete` ·
`POST /{id}/accept` · `POST /{id}/claim` · `POST /{id}/release` · `POST /{id}/plan` · `POST /{id}/start` ·
`POST /{id}/complete` · `POST /{id}/cancel` · `GET /lookups/assignable-positions`.

> `api/tasks` is **already taken** by the frozen legacy `TaskApiController`
> (`frontend/Diten.Web/Controllers/TaskApiController.cs:8`, serves legacy `/WorkCenter`). The new backend uses
> `api/v1/tasks`; the frontend proxy uses `/Tasks/api/*` — **never** `api/tasks`.

## 4. Entity Fields

### `TaskItem` (core — Phase 1)

| Field | Type | Required | Rule |
|---|---|---|---|
| `Title` | string | Yes | trim, max 200 |
| `Description` | string? | No | max 4000 |
| `Lifecycle` | enum `TaskLifecycle` | Yes | `Open` default — **system-set**, never user-chosen (§12 Y2) |
| `Priority` | enum `TaskPriority` | Yes | `Low\|Medium\|High` (contract-neutral) |
| `AssignmentTarget` | enum | Yes | `SelfAssigned \| Person \| PositionPool` (§12 K5) |
| `AssigneeUserId` | Guid? | Conditional | required iff `Person`/`SelfAssigned`; **null for pool** (§12 K5) |
| `PoolPositionId` | Guid? | Conditional | required iff `PositionPool`; FK → MOD-0288 `Position` |
| `OrganizationUnitId` | Guid | Yes | org context (§12 K6); default = assignee's/position's unit |
| `DueAt` | DateTimeOffset? | **Yes — all three targets** | required for Self/Person **and Pool** (§18 OD-3: an ownerless pool task without a due date waits indefinitely) |
| `StartAt` | DateTimeOffset? | No | |
| `PlannedDate` | DateTimeOffset? | No | personal plan; must not contradict `DueAt` (warn) |
| `EstimateHours` | decimal? | No | ≥ 0 |
| `SpentHours` | decimal | System | **always 0 at create** (§12 Y1); execution-only |
| `RemainingHours` | decimal? | Derived | `Estimate - Spent`, floored at 0, never stored (§12 E4) |
| `Tags` | List\<string\> | No | ≤ 20, each ≤ 40 |
| `ReviewRequired` | bool | No | `false`; when true → MOD-0023 review (§12 K2) |
| `ApprovalRequired` | bool | No | `false`; when true → MOD-0023 approval before start (§12 K2) |
| `ApprovalManagerUserId` | Guid? | Conditional | required iff `ApprovalRequired`; **candidate hint only** — MOD-0023 resolves authority |
| `WorkflowInstanceId` | Guid? | System | MOD-0023 instance ref when approval/review active |
| `EmailNotificationsEnabled` | bool | No | `true` |
| `DelegationAllowed` | bool | No | policy flag only — **no eligibility decision** (§12 Y5) |
| `RecurrenceRuleId` | Guid? | No | Phase 4 |
| `ProcessInstanceId` | string? | System | distinguishes recurring instances (§12 K8) |
| `FieldValues` | List\<`TaskFieldValue`\> | No | embedded configurable values (§12 K1) |
| `ConcurrencyVersion` | int | System | inherited `Version`; the projection's concurrency token |

`TaskFieldValue` (embedded): `DefinitionCode`, `ValueType` (contract `VALUE_TYPES`), `Value` (string-encoded),
`Classification?`, `AccessState?`, `Redacted` — the last three carried **from day one** so BL-024 field-level
authorization becomes additive with **no schema migration** (§12 K1).

### `TaskFieldDefinition`
`Code`* (tenant-unique, lowercase-dotted) · `LabelResourceKey`* · `ValueType`* (must be in the contract's
`VALUE_TYPES`) · `Section`* · `Importance` (`primary|secondary`) · `IsRequired` · `SortOrder` ·
`OptionsSourceKind` (`none|lookup|referenceData`) + `OptionsSourceKey?` (FG-004: no hard-coded lists) ·
`AppliesToModuleCode?` · `Classification?`/`AccessState?` (BL-024-ready) · `IsActive`.

Contract limits are hard bounds: ≤ **6** sections, ≤ **8** fields/section, ≤ **2000** chars/text field,
≤ **8** `primary` fields, ≤ **20** related records (`fixture-contract.js` `LIMITS`).

### Lifecycle → normalized status (authoritative; the browser never derives it)

| `Lifecycle` | `normalizedStatus` | Extra |
|---|---|---|
| `Open` | `Pending` | (`Backlog` from the prototype maps here — §12 Y2) |
| `Planned` | `Pending` | `plannedDate` set → Planlı segment |
| `InProgress` | `InProgress` | |
| `Waiting` | `Waiting` | **`waitingContext` required** (contract is bidirectional) |
| `PendingReview` | `Waiting` | `waitingContext { type: 'review' }` |
| `Done` | `Done` | terminal, read-only |
| `Cancelled` | `Cancelled` | terminal, read-only |
| approval pending (pre-start) | `Waiting` | `waitingContext { type: 'approval' }` + `Lifecycle = Open`; set by the **system** |

> The existing mock emits `taskLifecycle: 'Open'` **with** `normalizedStatus: 'InProgress'`
> (`app.js` `createSelfTask`) — wrong for an unstarted task. This table is authoritative and replaces it.

### MOD-0288 consumption (read-only; verified shapes)
- `Position.OrganizationUnitId` is `required Guid` and `OrganizationUnit.LegalEntityId` is `required Guid` →
  a position is **always** unit-bound, which is precisely why pools are position-targeted (§12 K4).
- Pool membership = `PositionAssignment` where `PositionId` matches and the interval is active. Intervals are
  **half-open**: `EffectiveFrom <= now && (EffectiveTo == null || EffectiveTo > now)`. (`WorkflowCandidateResolver`
  uses `>=` — the odd one out; this slice uses `>` and says so.)
- The person link is **`PositionAssignment.UserId`** (an AuthService user id). `PersonReference` has **no**
  `UserId`/`Email` and is a read-only directory → it is **not** an assignee source. Assignee pickers resolve
  users, not person references.
- `Position.Status` defaults to **`Draft`** and `GetPositionsQueryHandler` applies **no** status/archive filter →
  the assignable-position lookup **must** filter `Status == Active && !IsArchived`, or drafts/closed positions
  become assignable pools.

## 5. Repo Scope

### Backend (Diten.Platform)
- `…/Diten.Platform.Domain/Entities/Tasks/**` — the 9 entities + enums under `Domain/Enums/Tasks/**`
- `…/Diten.Platform.Domain/Repositories/ITaskRepositories.cs`
- `…/Diten.Platform.Application/Features/Tasks/**` — Commands / Queries / Handlers / Validators /
  `TaskModels.cs` / `Services/**` / `Providers/TaskWorkItemProvider.cs` / `SelfRegistration/TaskManifestProvider.cs`
- `…/Diten.Platform.Infrastructure/Persistence/Repositories/TaskRepositories.cs`
- `…/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs` — **extend**
- `…/Diten.Platform.Application/DependencyInjection.cs` — register services, provider, manifest, job handler
- `…/Diten.Platform.Application/BackgroundJobs/PlatformRecurringJobRegistrar.cs` — **one registration** (Phase 4)
- `…/Diten.Platform.API/Controllers/TasksController.cs`
- `…/tests/Diten.Platform.Application.Tests/Tasks/**`
- **New MOD-0288 read methods** (indexes already exist; today every caller does `GetAllAsync()` + in-memory
  LINQ with N+1 lookups): `IPositionAssignmentRepository.GetActiveByPositionIdAsync`,
  `GetActiveByUserIdAsync`; `IPositionRepository.GetByOrganizationUnitIdAsync`. Additive only — no behaviour
  change to existing callers.

### Frontend (Diten.Web) — Compact set
- `Controllers/TasksController.cs` (proxy, `[Authorize]`, `/Tasks/api/*` → `{GatewayUrl}/api/v1/tasks/*`)
- `Views/Tasks/{Index,Create,Edit,Details}.cshtml` + `_DataTable`, `_Filter`, `_Form`, `_IndexL10n` + `TasksIndex.cs`
- `Views/Tasks/_QuickCreateOffcanvas.cshtml` — **documented deviation**, see §12 K9 / DEV-1
- `wwwroot/assets/js/Tasks/{index.js,index.l10n.js,form.js}`
- `Resources/Views/Tasks/TasksIndex.{en,tr,fr,es,zh,ar,ru}.resx`
- `wwwroot/assets/css/backbone-custom.css` — only if needed, `.task-*`-scoped classes (FG-003)
- WorkCenterNext `+ Yeni` → replace the mock `createSelfTask` with the real quick-create call
  (`wwwroot/assets/js/WorkCenterNext/app.js` — **that one seam only**)

## 6. Protected Paths
- `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**` — WC-1 is **consumed**;
  only a DI line adds the new provider. If a WC-1 change looks necessary → **STOP and report**.
- `Features/Workflow/**`, `Entities/Workflow/**` — MOD-0023 consumed via its endpoints/contracts only.
- `services/Diten.AuthService/**` — permission seed/grant is a separate MOD-0018 task (§14).
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent only (§15).
- Legacy `/WorkCenter`: `Controllers/WorkCenterController.cs`, `Controllers/TaskApiController.cs`,
  `Views/WorkCenter/**`, `wwwroot/assets/js/WorkCenter/**`, `Services/WorkCenter/**`, `Models/WorkCenter/**` — frozen.
- `Views/Shared/_Layout.cshtml`; `Entities/Organization/**` (MOD-0288 owns writes);
  `services/Diten.Platform.Common/**`; `.antigravity/**`; Blueprint `.xlsx`; `execution/registries/**`;
  `execution/portfolio/**`; other domain services.

## 7. Dependencies

| Dependency | Use | Boundary |
|---|---|---|
| WC-1 `IWorkItemProvider` (`ProviderCode`, `ProviderContractVersion`, `GetWorkItemsAsync(WorkItemActor, ct)`) | Task Center projection | Second provider; WC-1 code unchanged |
| `fixture-contract.js` | **Executable authority** for the projected shape | Contract wins on conflict |
| MOD-0023 | Approval + review (Binding A) | Instance started/consumed; no second engine |
| MOD-0288 | Position / OrgUnit / PositionAssignment | Read-only; additive repo methods |
| MOD-0027 `INotificationEventDispatchAdapter` | Email by `eventCode` | Never throws; returns `Response<T>` |
| MOD-0026 `IRecurringJobRegistrar` + `IBackgroundJobHandler<T>` | Recurrence (Phase 4) | Reuse; no new hosted service |
| MOD-0018 | Effective permission, delegation eligibility | Consumed; browser never authority |
| MOD-0021 | Audit (FG-005 — write commands are auditable) | `IAuditableCommand` + metadata provider |
| MOD-0048 / BRD | Configurable field option sources | FG-004: no hard-coded lists |

## 8. Runtime Constraints
1. Mongo, tenant-scoped, soft delete, **tenant-first compound indexes** on every collection.
2. `TenantId` resolved server-side (`TenantRepository<T>` overwrites it); never accepted from the client.
3. Every projected item satisfies `validateWorkItem`; the browser invents no action and derives no eligibility.
4. Lifecycle transitions happen **only** through `ITaskLifecycleService` (one owner, one normalize map).
5. MOD-0024 writes **no** approval/review state. Toggle → MOD-0023 instance; outcome consumed (§12 K2).
6. Pool tasks have **no** assignee until claimed; claim is guarded by optimistic concurrency (§13).
7. `SpentHours` is never settable at create (§12 Y1).
8. Configurable field values validate against definitions **and** contract limits; unauthorized values are
   never sent to the browser (`redacted` ⇒ value omitted, never CSS-hidden).
9. Attachments are **out of scope** this slice (§12 Y4).
10. Browser never calls a service port: browser → `/Tasks/api/*` → Gateway 5000 → Platform.
11. Recurring jobs: UTC only, args JSON round-trippable, id `Diten.Platform.MOD-0024.{JobName}`.
12. 7-language l10n for all UI strings; no new hard-coded user-facing text.
13. No inline CSS (FG-003).

## 9. Layout & Shell Contract
- `shell: tenant` → `Layout = "_LayoutTenantShell";` stated **explicitly** in all four `.cshtml` files.
- Routes `/Tasks`, `/Tasks/Create`, `/Tasks/{id}`, `/Tasks/{id}/Edit`; views in `Views/Tasks/`.
- Manifest pages are declared **`IsNavigationVisible: false`** (precedent: AccessGovernance `PERMISSIONS`).
  Rationale: the Task Center (`Görev Merkezi`) is the single personal entry point — a competing "Görevler" nav
  entry would fragment it. Pages are registered for **permission attribution**, reached from WorkCenter `+ Yeni`.

## 10. Backend File Convention

Live Platform CQRS (mirrors GoldenReferenceCompact + MOD-0023 §6):

```text
Features/Tasks/
├── Commands/            (sealed records, one per file, no Command suffix on handlers)
├── Queries/
├── Handlers/
│   ├── CommandHandlers/ {Verb}TaskItemHandler.cs
│   └── QueryHandlers/   GetTaskItem{List,ById}Handler.cs
├── Validators/          {Verb}TaskItemValidator.cs
├── Services/            ITaskLifecycleService, ITaskAssignmentResolver, ITaskFieldDefinitionService
├── Providers/           TaskWorkItemProvider.cs
├── SelfRegistration/    TaskManifestProvider.cs
├── BackgroundJobs/      TaskRecurrenceSweepJob.cs (Phase 4)
└── TaskModels.cs        (ALL DTOs in one file)
```
- Repositories inherit **`TenantRepository<T>`** (as Workflow/Organization do) — auto tenant + `IsDeleted` filter.
- Collections: snake_case **plural** (Platform convention: `notification_templates`, `approval_tasks`).
- Controllers: thin, `CustomBaseController`, `[Authorize]` + `[HasPermission]`, `Response<T>`, `ICorrelationContext`.
- **Do not** create new base entity / repository base / tenant / correlation / audit / event / job infrastructure.
- BSON element names are PascalCase (no camelCase convention pack is registered); API JSON is camelCase.

## 11. Frontend File Contract
Golden Reference **Compact**, mirrored file-for-file from `Views/DevEnablement/GoldenReferenceCompact/`:
`Index/Create/Edit/Details.cshtml` + `_DataTable/_Filter/_Form/_IndexL10n.cshtml` + `TasksIndex.cs` marker;
`index.js`, `index.l10n.js`, `form.js`; 7 resx files. Plus the DEV-1 quick-create offcanvas (§12 K9).

## 12. Design Decisions (EA-locked 2026-07-25)

**K1 — Configurable fields, not columns.** Phase/Work Type/Market/Domain/External Party are
`TaskFieldDefinition` + embedded `TaskFieldValue`, modelled on the contract's `businessContext`
(sections/fields, allowlisted `VALUE_TYPES`). Phase 1 = definition; **Phase 2 = field-level authorization is
BL-024, NOT this pack** — but `Classification`/`AccessState`/`Redacted` ship in the schema now so BL-024 is
additive with no migration.

**K2 — Approval & review are delegated to MOD-0023 (Binding A).** MOD-0024 declares no approval state, owns no
"Onay Bekliyor" status. Toggle → workflow instance; `WorkflowInstanceId` stored; the outcome (approved →
startable, rejected → cancelled) is a **workflow** decision consumed by MOD-0024. Charter §10.4 + the
two-engine prohibition.

**K3 — Watcher / Consultant are a FILTER, not a tab.** Their tasks never enter an ownership tab; they surface
via an "İzlediklerim" chip. No new tab (axis law: tab = ownership). The contract does not model these roles and
`viewerRole` is **not read by `validateWorkItem`**, so adding `watcher`/`consultant` is additive and breaks no
invariant — **but the canonical vocabulary is the contract owner's call** and is recorded as such (§18 OD-1).
⚠ **Adding a watcher grants visibility** → the data-access rule is: a watcher may read the task's non-restricted
projection only; restricted configurable fields stay redacted (BL-024). Watchers get no action rights.

**K4 — Pools target `Position`.** Verified in code: `Position.OrganizationUnitId` is `required` and
`OrganizationUnit.LegalEntityId` is `required`, so "QA Specialist" is never global — each unit/facility has its
own position, and the two-facility problem resolves structurally (Facility B's QA cannot see Facility A's pool).
⚠ **Hard UI requirement + a real gap:** `PositionDto` carries **only** `OrganizationUnitId` — no unit name/code —
and the existing workflow picker renders just `"{code} {name}"`, so it *already* cannot distinguish
"QA Specialist — Facility A" from "— Facility B". This slice therefore adds a dedicated
`GetTaskAssignmentPositionLookup` returning `PositionId, PositionCode, PositionName, OrganizationUnitId,
OrganizationUnitCode, OrganizationUnitName, LegalEntityId` and **filters `Status == Active && !IsArchived`**.
Server-side join, not a fragile client-side map.

**K5 — Assignment target = Self | Person | Pool(Position).** "Assignee" stops being mandatory (a pool task has
none). Contract mapping — the only regression-free way to add pools later:

| Target | `assignmentMode` | `ownershipState` | `admissionState` |
|---|---|---|---|
| Self | `direct` | `owned` | `admitted` |
| Person | `direct` | `assigned` | `pendingAcceptance` |
| Pool (Position) | `groupQueue` | `unowned` | `pendingClaim` |

**K6 — Every task carries organization context** (`OrganizationUnitId`), defaulting to the assignee's or
position's unit — for filtering, authorization and reporting.

**K6.1 — Graded unit resolution (EA 2026-07-25).** The user never picks a unit, so the server resolves it in
order and only fails when nothing can be determined:

| # | Source | Notes |
|---|---|---|
| 1 | explicit `OrganizationUnitId` on the request | honoured as given |
| 2 | the assignee's **active** `PositionAssignment` → position's unit | active = `!IsCancelled && EffectiveFrom <= now && (EffectiveTo == null \|\| EffectiveTo > now)`; a `Primary` assignment wins when several are held |
| 3 | the tenant's **root** unit | see the determinism rule below |
| 4 | fail `400 ORGANIZATION_UNIT_UNRESOLVED` | never invent a unit — that silently misfiles the task |

Tier 3 exists because a person holding **no position** (administrators, new joiners) could otherwise create no
task at all — the live failure on 2026-07-25.

**Root-unit determinism rule.** `OrganizationUnit` has **no** explicit "is default" flag, and a tenant may hold
several roots (one per legal entity), so the choice must not depend on storage order:

> Among units with `ParentOrganizationUnitId == null`, `!IsArchived` and `Status == Active`:
> **(a)** prefer `OrgUnitType == HQ`; **(b)** otherwise the lowest `Code` by ordinal comparison.

Same data in → same unit out, on every node. **Pool tasks never use this chain**: a `Position` is always
unit-bound, so a pooled task inherits its facility from the position (K4), which is also what keeps pooled work
out of the wrong site.

If an explicit "default organization unit" marker is ever added to the tenant, tiers 3(a)/(b) should be replaced
by it — that would be a behaviour change and belongs in a pack revision, not an ad-hoc edit.

**K7 — Email only.** Events are declared in the module manifest (`NotificationEvents`) and dispatched via
`INotificationEventDispatchAdapter`. The header bell is **out of scope** — no in-app channel exists
(`NotificationChannelCode { Email = 0 }`) and the bell is a static theme ornament → **BL-025**.

**K8 — Recurrence reuses the Hangfire seam** (`IRecurringJobRegistrar` + `IBackgroundJobHandler<T>`); no new
engine, no new hosted service. `ProcessInstanceId` separates recurring instances.

**K9 — Two-depth form.** Quick (offcanvas: Title*, Target, Due, Priority) and Detailed (full page, 20 fields);
switching preserves the draft. **DEV-1 — documented standard deviation:** `module-pack-standard.md:215` forbids
`_CreateEditOffcanvas.cshtml` under `golden_reference: compact`. The prohibition exists to prevent two competing
create/edit surfaces; here the offcanvas is a **shortcut that hands its draft to the same compact form** and is
never an alternative edit surface (edit is always the full page). Named `_QuickCreateOffcanvas.cshtml` to keep
the forbidden filename unused, and recorded as an EA-approved deviation requiring sign-off at approval.

**K10 — Second Task Center provider.** `TaskWorkItemProvider` registers alongside the MOD-0023 provider;
`Features/WorkAggregation` is untouched apart from a DI line.

### Corrections carried from review
- **Y1** `SpentHours` removed from create (always 0; execution-only).
- **Y2** `Backlog` → `Open`; when approval is required the **system** sets `Waiting` + `waitingContext`.
- **Y3** MOD-0024 may edit dependencies **between its own tasks** (it is their source). The Task Center still
  renders dependencies read-only; no dependency editor inside the aggregator.
- **Y4** Attachments **excluded** from this slice — binary storage belongs to an approved document/storage
  provider. Deferred to a separate slice (§20).
- **Y5** `DelegationAllowed` is a policy flag; **eligibility remains MOD-0018's decision**.

### Gaps closed
**E1** Checklist (template + items + required/blocking semantics; `complete` disabled with
`CHECKLIST_INCOMPLETE`) · **E2** Subtasks `full` (MOD-0024 is the source) · **E3** `relatedRecords` (≤ 20) ·
**E4** Estimate/Spent/Remaining consistency (Remaining derived) · **E5** Task templates ·
**E6** 7-language l10n for labels **and** email templates.

## 13. Validation Rules / Failure Paths

| Scenario | Expected |
|---|---|
| Missing `Title` / target / conditional `DueAt` | 400 + field-level validator message; nothing persisted |
| `Person` target without `AssigneeUserId`, or `Pool` without `PoolPositionId` | 400 `ASSIGNMENT_TARGET_INVALID` |
| `AssigneeUserId` not a valid tenant user | 400 — validated via `IUserReferenceValidator` (existing seam) |
| `PoolPositionId` inactive/archived/other tenant | 400 `POSITION_NOT_ASSIGNABLE` |
| **Pool claim race** (two users claim simultaneously) | Optimistic concurrency on `Version`: first wins; loser gets **409 `TASK_ALREADY_CLAIMED`** and a refreshed projection — never a silent double-owner |
| Cross-tenant read/update/delete | 404 / empty, **no metadata leak** |
| Approval rejected | Workflow outcome cancels the task; MOD-0024 does not decide |
| Email dispatch fails | Task creation **still succeeds**; adapter returns `Response<T>.Fail` (`EVENT_NOT_ACTIVE`, `REQUIRED_VARIABLE_MISSING`, `RECIPIENT_MISSING`…) which is logged, not thrown |
| Notification event still `Draft` | No email sent (adapter 409 `EVENT_NOT_ACTIVE`) — surfaced as an ops warning, not a task failure |
| Recurrence overlap (previous instance still open) | Skip + log; `ProcessInstanceId` prevents duplicates |
| Configurable field violates definition/limits | 400 with the offending field code |
| Unauthorized actor | 403; UI shows a permission state, never a fake button |
| Terminal task mutated | 409 invalid-state |

## 14. Authorization Convention

```text
Policy:     [Authorize]                       (tenant user)
Permissions (PKS-001 lowercase-dotted):
  platform.tasks.read          platform.tasks.create      platform.tasks.update
  platform.tasks.delete        platform.tasks.bulk-delete platform.tasks.assign
  platform.tasks.claim         platform.tasks.complete    platform.tasks.cancel
  platform.tasks.field-definitions.manage
```
- Constants are defined **locally** in `Diten.Platform`; the **seed/grant is a separate MOD-0018 task**.
- ⚠ **B2 lesson applied.** Keys must be attributed via the **module manifest** (page `RequiredPermission` /
  action `PermissionKey`), because a key first created by the A1 reflection worker is stamped
  `Module="platform"` + `Scope=PlatformAdmin`, and AuthService has **no scope-downgrade path** — permanently
  blocking tenant role assignment. The `ModuleSelfRegistrationGate` (DEC-9) now guarantees the manifest wins,
  and these routes are non-`/Platform/*` so `ScopeFromRoute` yields `Tenant`. The pack requires
  **evidence** of `Module=tasks`/`Scope=Tenant` after first startup (§16).
- Watchers/consultants get **read-only** visibility; no action permissions (§12 K3).

### Notification events (manifest-declared)
`platform.tasks.assigned` · `platform.tasks.claimed` · `platform.tasks.due-soon` · `platform.tasks.completed` ·
`platform.tasks.approval-requested`.
⚠ Two verified constraints: an event becomes **`Active` only when it has zero validation issues AND the manifest
declares `Status: "Active"`**; and `TargetPageCode` / `RequiredPermissionKey` are validated **against this same
manifest's own pages/permissions** — so the manifest must declare its pages and permission keys for the events to
activate. **MOD-0024 would be the first module in the repo to populate `NotificationEvents`** (the plumbing is
live but unexercised) → Phase 1 must verify activation explicitly, not assume it.

## 15. Gateway / API Routing Decision
```text
Karar: Gateway değişikliği GEREKLİ → ayrı integration-agent task'i.
```
`ocelot.json` has **94 explicit routes and no generic catch-all**; `api/v1/tasks/**` is absent. Required pair
(authored by integration-agent, **not** here): `/api/v1/tasks/{everything}` → `localhost:5057`, methods
`GET, POST, PUT, DELETE, OPTIONS` (writes are needed, unlike the read-only WC-1 route). Until it exists the
proxy returns 503 — a release blocker.

## 16. Acceptance Criteria (phase-tagged)

### Governance
- [ ] `MOD-0024` identity unchanged; no new ID; preflight exit 0.
- [ ] `Features/WorkAggregation` unchanged except one DI line; MOD-0023 files, AuthService, `ocelot.json`,
  legacy `/WorkCenter` (incl. `TaskApiController`), `_Layout.cshtml`, Blueprint, registry/portfolio untouched.
- [ ] DEV-1 (quick-create offcanvas under Compact) explicitly approved at sign-off.
- [ ] No `api/tasks` reuse; backend is `api/v1/tasks`, proxy is `/Tasks/api/*`.

### Phase 1 — create/list/detail
- [ ] A task can be created for **self**, a **person**, and a **position pool**; the contract mapping in §12 K5
  holds for all three (unit-tested).
- [ ] `AssigneeUserId` is **not** required for a pool task; `PoolPositionId` is.
- [ ] The position lookup returns unit code+name and renders "QA Specialist — Facility A"; drafts/archived
  positions are excluded.
- [ ] `OrganizationUnitId` is always set (defaulted from assignee/position).
- [ ] `SpentHours == 0` at create and is not settable; `Remaining` is derived.
- [ ] Lifecycle→normalized mapping matches §4 exactly; `Waiting` always carries `waitingContext`.
- [ ] User cannot choose an approval-pending state — the system sets it.
- [ ] Quick and detailed forms share one draft; switching loses nothing.
- [ ] Cross-tenant isolation proven (404/empty, no leak).
- [ ] Two concurrent claims → one owner, other gets 409 + refreshed projection.
- [ ] Tasks appear in the Task Center via `TaskWorkItemProvider`; every item passes `validateWorkItem`.
- [ ] Assignment email sent via the adapter; a dispatch failure does **not** fail task creation.
- [ ] Permission `Module`/`Scope` verified as `tasks`/`Tenant` after first startup (evidence required).
- [ ] All 7 resx files share one key set; no raw key rendered.

### Phase 2 — checklist / subtask / template
- [ ] Required+blocking checklist item disables `complete` with `CHECKLIST_INCOMPLETE`.
- [ ] Subtasks behave as `full`; a task template instantiates its checklist.

### Phase 3 — approval/review via MOD-0023
- [ ] Toggling approval starts a MOD-0023 instance; MOD-0024 stores no approval state.
- [ ] Rejection cancels the task as a workflow outcome; approval makes it startable.

### Phase 4 — recurrence
- [ ] Recurring instances are distinct (`ProcessInstanceId`); a rerun does not duplicate.
- [ ] Job id `Diten.Platform.MOD-0024.TaskRecurrenceSweepJob`, UTC, queue `platform`; **documented** that it
  runs only when `BackgroundJobs:RegisterStandardJobs` **and** `EnabledJobs["<id>"]` are true (today
  `EnabledJobs: {}` ⇒ disabled by default — otherwise "recurrence doesn't work" is misreported as a bug).

### Phase 5 — configurable field definition UI
- [ ] Definitions are tenant-configurable; values validate against definitions + contract limits.
- [ ] `Classification`/`AccessState`/`Redacted` present in schema (BL-024-ready); unauthorized values never
  reach the browser.

## 17. Test Expectations
**Unit** — assignment target → contract mapping (all three); lifecycle→normalized table (every value);
`waitingContext` pairing; pool claim concurrency (409); position lookup filtering (Draft/archived excluded);
`Remaining` derivation; configurable-field limit enforcement (6/8/2000/8/20); `SpentHours` rejected at create.
**Contract conformance** — every projected item validated against `fixture-contract.js` `validateWorkItem`
(the executable authority), plus camelCase serialization assertions.
**Manifest** — zero-drift permission oracle (every `RequiredPermission`/`PermissionKey` is a real constant);
declared `NotificationEvents` reference this manifest's own page codes and permission keys, so they can reach
`Active`.
**Isolation** — cross-tenant read/update/delete; `TenantId` never client-supplied.
**Integration** — MOD-0023 handoff (Phase 3) asserts MOD-0024 stores no approval state.
**Frontend (Vitest)** — quick↔detailed draft continuity; assignment-target field visibility;
7-language resx parity (existing parity test pattern).
**Browser smoke (authenticated)** — create all three targets; task visible in Task Center; console clean;
no `5056`/`5057` in the network tab; Arabic RTL on the form.
**Build** — `dotnet build` Platform + Diten.Web = 0 errors; existing WorkAggregation (37) and Workflow (109)
suites stay green.

## 18. Open Decisions — ALL RESOLVED (EA 2026-07-25)

- **OD-1 — `viewerRole` vocabulary. RESOLVED: deferred to Phase 2.** Watcher/consultant participation is **not
  built in Phase 1** — Phase 1 stays lean (produce a task, persist it, surface it in the Task Center). The
  `TaskWatcher` entity may ship in the Phase-1 schema (additive, no migration later) but no watcher UI, filter,
  or `viewerRole` vocabulary change happens until Phase 2, when the contract owner rules on the canonical value.
- **OD-2 — Pool notification policy. RESOLVED: notify every active position holder; stay silent after a claim.**
  A pool task emails all users with an **active** `PositionAssignment` for that position (half-open interval,
  §4). Once someone claims it, the other candidates receive **no** "no longer yours" email — that would be pure
  noise. The claimer gets the standard assignment notification.
- **OD-3 — `DueAt` requiredness for pool tasks. RESOLVED: REQUIRED for pool tasks too.** A pool task is
  ownerless by definition; without a due date nobody feels accountable and it waits indefinitely. `DueAt` is
  therefore **required for all three assignment targets** in Phase 1. (Supersedes the earlier "pool tasks may be
  open-ended" note in §4 — update that row accordingly.)
- **OD-4 — Watcher read depth. RESOLVED: summary + read-only detail.** A watcher reads the task's
  non-restricted projection (summary fields plus read-only detail) and gets **no action rights**. Restricted
  configurable fields stay redacted; finer-grained field authorization arrives with BL-024. Revisit alongside
  BL-023 (team scope) if manager-level depth is later required.

## 19. Implementation Notes (phased; each phase separately approved)
1. **Phase 1** — entities + indexes + repositories; lifecycle service + normalize map; assignment resolver;
   position lookup; CQRS + controller; manifest (pages, permissions, notification events); `TaskWorkItemProvider`
   + DI; quick & detailed forms; email on assign; 7-language l10n. Verify permission `Module`/`Scope` **before**
   declaring done.
2. **Phase 2** — checklist, subtasks, task templates.
3. **Phase 3** — MOD-0023 approval/review handoff.
4. **Phase 4** — recurrence on the Hangfire seam.
5. **Phase 5** — configurable field definition UI (BL-024 authorization stays out).

The schema is laid down correctly **in Phase 1** (pool fields, `Classification`/`AccessState`/`Redacted`,
`ProcessInstanceId`, `WorkflowInstanceId`) so Phases 2–5 are additive with no migration.

## 20. Follow-up Items (not authorized here)
1. **MOD-0018** — seed/grant the `platform.tasks.*` keys; confirm `Scope=Tenant`.
2. **integration-agent** — the `/api/v1/tasks/{everything}` Ocelot route.
3. **Attachments** — a separate slice bound to an approved document/storage provider (§12 Y4).
4. **BL-024** — field-level authorization for configurable fields.
5. **BL-025** — in-app channel + header bell (email-only until then).
6. **BL-023 / BL-016 (Outbox)** — team scope and creator-scope surfaces build on this slice.
7. **MOD-0288** — a first-class "positions for user / users for position" query surface if in-memory
   `GetAllAsync()` scans become a bottleneck; also the `EffectiveTo >=` vs `>` inconsistency in
   `WorkflowCandidateResolver`.
8. ~~**Backlog hygiene** — `BL-016` is used twice.~~ **DONE (2026-07-25):** the Meeting-invite/Calendar item was
   renumbered to **BL-026**; `BL-016` now unambiguously means "Başlattıklarım / Outbox".
