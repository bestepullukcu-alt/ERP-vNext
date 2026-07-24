---
id: CAND-CAP-0006
name: WC-1 — Unified Work-Item Provider Contract & Projection
governance_identity: CAND-CAP-0006
charter: DCP-004
slice: WC-1
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
owner: platform-team
branch: feature/pss/candcap0006-wc1-work-item-projection
started: 2026-07-24
target: TBD
status_changed: draft -> ready-for-dev on 2026-07-24 (EA/user-approved; condition 2 of CAP-001 §7)
form_field_count: 0
---

# WC-1 — Unified Work-Item Provider Contract & Projection (CAND-CAP-0006 / DCP-004)

> **Identity (DCP-002):** governance identity is **`CAND-CAP-0006`** (temporary candidate; no Blueprint
> `MOD-xxxx` yet). This is the **first executable slice of the approved charter
> [DCP-004](../../../portfolio/delivery-capability-packs/DCP-004-work-aggregation-task-center.md)** (§8 order 1).
> `CAND-CAP-0006` is a governance identity **only** — it is **never** written into runtime code, namespaces,
> or literals. The runtime feature slug is `Features/WorkAggregation` (clean). The real Blueprint `MOD-xxxx`
> is minted after WC-1 is proven ([BL-019](../../../../docs/product-backlog.md)).
>
> **This pack is `ready-for-dev`.** No code lives in this document itself; it remains a development contract.
> Per CAP-001 §7 two-condition gate, DCP-004 is `approved` (condition 1) **and** this pack is `ready-for-dev`
> (condition 2, EA/user 2026-07-24) — implementation of the authorized scope (§5) may now begin.
>
> **Executable authority:** `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/fixture-contract.js` is the
> **contract of record** for the work-item shape/enums/invariants. The projection DTO conforms to it
> field-by-field; on any conflict, **the contract wins**.

## 1. Module Summary

WC-1 builds the **backend read/projection layer** that turns real provider tasks into the canonical,
source-agnostic work item the WorkCenterNext surface already consumes. It closes charter finding **§20 F2**
(the projection layer is missing: `GetMyWorkflowTasks` returns a raw `WorkflowTaskDto` with no `title`, no
`actions[]`, no `source`/deep-link, no `normalizedStatus`/`nativeStatus` normalization, no exposed
concurrency).

**Three locked scope decisions (EA 2026-07-24):**

1. **READ / PROJECTION ONLY.** Command execution (approve/reject/delegate) stays on MOD-0023's existing
   transition endpoints. WC-1 writes **no** state and adds **no** command endpoint.
2. **BACKEND-ONLY.** Wiring the frontend from mock → real API is **not** in this slice (separate follow-up
   **WC-1b**). The frontend already consumes the `fixture-contract.js` shape; WC-1 produces a backend
   projection matching that shape.
3. **PROVIDER SCOPE = MOD-0023 `ApprovalTask` only** (Binding A — charter §10.4). Enterprise Strategy and
   other providers are deferred ([BL-018](../../../../docs/product-backlog.md)). The projection is nonetheless
   structured behind a **provider abstraction** so WC-5 can add providers later **without rewrite**; in WC-1,
   only the MOD-0023 provider is bound.

Target user: any authenticated tenant user viewing their personal work inbox. Capacity: a per-user projection
query returning canonical work items for the actionable set, built on the existing `GetMyWorkflowTasks`
foundation.

This is **not** a CRUD/DataTable module. `golden_reference: none`, `shell: none`, `form_field_count: 0`,
`entity_base: BaseEntity` (posture only) are intentional — **no persisted entity is created**.

### Delivery slice

| Included in this draft | |
|---|---|
| Canonical work-item projection DTO (contract-conformant) | Yes |
| Provider abstraction (`IWorkItemProvider`) + MOD-0023 provider impl | Yes |
| Projection service (status normalize + `actions[]` eligibility + source join + title/concurrency) | Yes |
| `GetMyWorkItems` query + handler + thin controller (read-only) | Yes |
| Backend projection unit tests | Yes |
| **Command execution / state write / new command endpoint** | **No** (stays in MOD-0023) |
| **Frontend mock → real API wiring** | No (WC-1b) |
| **Providers other than MOD-0023** (ES, etc.) | No (BL-018) |
| Persisted entity / repository / MongoDB collection | No |
| Gateway route edit / AuthService permission seed | No (separate integration-agent / MOD-0018 tasks) |

## 2. Ownership and Boundaries

### WC-1 owns

- The **canonical work-item projection DTO** and its mapping from provider aggregates to the
  `fixture-contract.js` shape.
- The **status normalization map** (charter §10.1) as pure, deterministic backend logic.
- The **single authoritative `actions[]`** resolution (native + permission + assignment/SoD + evidence/comment
  blockers → one array), computed server-side.
- The **`IWorkItemProvider` contract** (the extension seam WC-5 uses) and the MOD-0023 provider implementation.
- The read-only `GetMyWorkItems` query/handler/controller.

### WC-1 does NOT own (REUSE, never re-implement — charter §4)

| Concern | Owner |
|---|---|
| Approval semantics (approve/reject/delegate/request-info/cancel), transition, SLA/escalation | **MOD-0023** (its existing endpoints; WC-1 only reads/projects) |
| Effective permission / eligibility computation | **MOD-0018** + `Diten.AuthService` (WC-1 consumes decisions) |
| Native business-object lifecycle / status | the **source business module** |
| Audit system-of-record | **MOD-0021** |
| Assignee authority (who may act) | `RuntimeAssignmentSnapshot` (MOD-0023) resolved via MOD-0018 |
| Personal overlay (pin/snooze/note) | WorkCenter aggregation layer (frontend) — **not** WC-1 backend |
| Frontend `surfaceMode`/render selection | `task-detail-resolver.js` (frontend) — WC-1 only supplies fields |

- WC-1 is a **projection**, not a state machine. It never re-derives a provider's business decision and never
  invents an action the provider did not authorize.
- MOD-0024 (Tasks) and MOD-0023 (Approvals) responsibilities are never merged; WC-1 aggregates their
  projections, it does not fuse their semantics.

## 3. Owned Objects

### Runtime objects (all under `Features/WorkAggregation` — NO persistence)

| Object | Kind | Purpose |
|---|---|---|
| `GetMyWorkItemsQuery` | sealed record (query) | Current-user personal work-item projection request (read-only) |
| `GetMyWorkItemsHandler` | sealed class (QueryHandler) | Orchestrates providers → canonical items; tenant-scoped |
| `IWorkItemProvider` | interface | Provider abstraction (WC-5 seam); one method returning provider-native items to project |
| `WorkflowApprovalWorkItemProvider` | sealed class | MOD-0023 `ApprovalTask` provider (the only provider bound in WC-1) |
| `IWorkItemProjectionService` / `WorkItemProjectionService` | interface + sealed class | Pure mapping engine: normalize status, resolve `actions[]`, join source, derive title, project concurrency |
| `WorkItemsController` | thin controller | `GET api/v1/work-items/mine`; `[Authorize]` + `[HasPermission]`; `Response<T>` |
| `WorkAggregationModels.cs` | single models file | All projection DTOs (below), one file (Golden Reference convention) |

### Projection DTOs (in `WorkAggregationModels.cs`, contract-conformant)

| DTO | Mirrors contract field group |
|---|---|
| `WorkItemProjectionDto` | top-level work item (all enums below) |
| `WorkItemSourceDto` | `source { providerCode, providerContractVersion, objectType, objectId, deepLink }` |
| `NativeStatusDto` | `nativeStatus { code, label }` |
| `EffectiveActionDto` | one entry of authoritative `actions[]` |
| `WaitingContextDto` | `waitingContext { type, waitingOn, since, expectedUntil }` |
| `ConcurrencyDto` | `concurrency { kind, token }` (one projection-level token) |
| `LabelDto` | discriminated `{ kind: resource, key, args }` (resource form for l10n) |

### API endpoint

- `GET api/v1/work-items/mine` → `Response<IReadOnlyList<WorkItemProjectionDto>>` (read-only).

### Permissions (constants defined locally; **seed = separate MOD-0018 task**)

- **New read permission (proposed):** `platform.work-aggregation.inbox.view` — the aggregation inbox is
  cross-provider and outlives workflow-only scope, so a dedicated read key is cleaner than reusing
  `platform.workflow.instances.view`. Defined as a `[HasPermission]` constant in `Diten.Platform`; the
  **seed/grant lives in MOD-0018 / `Diten.AuthService`** (separate task — this pack does not edit AuthService).
- **Seed path (clarified 2026-07-24):** the seed is most likely handled by the **WorkCenter tenant manifest
  self-registration + catalog→auth permission sync** (WC-1b — DCP-004 §8, [BL-022](../../../../docs/product-backlog.md)),
  **not** a bespoke MOD-0018 task. WC-1 backend only **defines** the `[HasPermission]` constant; it does not
  seed and does not register a manifest (WC-1b owns that). Confirm the seed path at WC-1b.
- **Consumed for `actions[]` eligibility (not defined here):** `platform.workflow.tasks.approve` /
  `.reject` / `.delegate` / `.request-info` / `.cancel` (MOD-0023 §8) — used to enable/disable each projected
  action; the seed already belongs to MOD-0023/MOD-0018.

## 4. Entity Fields

**No MongoDB entity is introduced.** `entity_base: BaseEntity` records the Platform tenant-aware posture only;
no entity class may be created by this slice. WC-1 **reads** existing MOD-0023 aggregates:

| Read source (MOD-0023, unchanged) | Fields consumed |
|---|---|
| `ApprovalTask` | `Status`, `AssigneeRef`, `AssignmentSnapshotId`, `CommentRequired`, `EvidenceRequired`, `DueAt`, `EscalatedAt`, `EscalationLevel`, `TimedOutAt`, `CompletedAt`, `WorkflowInstanceId`, inherited `Version`, `TenantId` |
| `WorkflowInstance` | `ObjectRef` (`"{module}|{objectType}|{objectId}"`), `ObjectType`, `ObjectId` — the **source join** |
| `RuntimeAssignmentSnapshot` | `ResolvedPrincipalId`, `CandidatePrincipalIds` — candidate/assignment for eligibility |

### Projection field mapping (contract shape ← provider)

| Canonical field (contract) | Source / rule | Notes |
|---|---|---|
| `id` | `ApprovalTask.Id` | stable |
| `workIntent` | `approval` (MOD-0023 provider) | provider-declared |
| `assignmentMode` | `approval` | approval = in-place decision, no accept-gate |
| `ownershipState` / `admissionState` | `notApplicable` for approval decisions | approval is not accept/claim/offer |
| `taskLifecycle` | `notApplicable` | non-task intent (contract invariant) |
| `normalizedStatus` | **§10.1 map** (see below) | never parse raw status text |
| `nativeStatus` | `{ code: ApprovalTaskStatus name, label: resource-key }` | raw native retained for display only |
| `executionState` / `timerState` | `notApplicable` / `notApplicable` | approvals have no execution/timer |
| `systemState` | `fresh` (MOD-0023 live source) | stale/unavailable = recovery from provider health, future |
| `waitingContext` | present **iff** `normalizedStatus == Waiting` (WaitingEvidence) | `{ type: 'evidenceRequired', ... }` |
| `actionDepth` | `inline` for approve/reject; `deeplink` only when a source deep-link exists | see title/deepLink gap below |
| `source` | `providerCode: 'workflow'`, `objectType`/`objectId` parsed from `WorkflowInstance.ObjectRef`, `providerContractVersion` from provider, `deepLink` provider-resolved (may be null) | **join** |
| `lifecycleOwner` | `workflow` (differs from source object's module) | charter §10.3 — required when ≠ source |
| `actions[]` | **eligibility resolution** (see §10 / below) | single authoritative array |
| `concurrency` | `{ kind: 'version', token: ApprovalTask.Version.ToString() }` | one projection-level token; no per-action copies |
| `title` | derived (see gap) | **KNOWN GAP — resolved below** |
| `personal` | **not set by backend** | frontend overlay owns it |

### Status normalization map (charter §10.1 — authoritative)

| `ApprovalTaskStatus` | `normalizedStatus` | Extra |
|---|---|---|
| `WaitingApproval` | `Pending` | decision surface |
| `WaitingEvidence` | `Waiting` | **`waitingContext`** (required pair) |
| `Escalated` | `Pending` | **escalation signal** (chip/notice, not a status) |
| `Approved` / `Rejected` | `Done` | terminal; no enabled inline state-changing action |
| `Cancelled` | `Cancelled` | terminal |
| **`TimedOut`** | **`Cancelled`** | terminal (EA 2026-07-24, OD-WC-01) |
| `Delegated` | *(item hidden from this actor)* | disposition, not this actor's active work |

### Known gaps this pack MUST resolve explicitly (charter §20 F2)

- **`title` gap:** `ApprovalTask` has no title. **Resolution:** the `IWorkItemProvider` supplies a title; the
  MOD-0023 provider derives it from `WorkflowInstance.ObjectType` + `ObjectId` via a **resource-key label**
  (e.g. `WorkAggregation_Title_Approval` with args `{objectType, objectId}`) as a deterministic fallback until
  a richer provider-supplied title exists. No raw business text is fabricated; the label is localized.
- **`deepLink` gap:** `ApprovalTask`/`WorkflowInstance` store no deep-link. **Resolution:** deep-link
  construction is **provider-owned** in the `IWorkItemProvider` contract (source module route). For the
  MOD-0023-only phase, `deepLink` may be `null` and `actionDepth` stays `inline` (approve/reject are inline);
  real per-source deep-links are a provider-registry (WC-5) concern. This is phased, not silently skipped.

## 5. Repo Scope

### Authorized backend scope (after explicit approval)

- `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/`
  - `Queries/GetMyWorkItemsQuery.cs`
  - `Handlers/QueryHandlers/GetMyWorkItemsHandler.cs`
  - `Providers/IWorkItemProvider.cs`
  - `Providers/WorkflowApprovalWorkItemProvider.cs`
  - `Services/IWorkItemProjectionService.cs`
  - `Services/WorkItemProjectionService.cs`
  - `WorkAggregationModels.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkItemsController.cs` — thin
  `CustomBaseController` dispatcher.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/.../DependencyInjection.cs` (existing DI file) —
  register the provider + projection service (extend, do not fork).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/WorkAggregation/` — projection unit tests.

### Explicitly out of scope (this slice)

- Any MOD-0023 file change (its endpoints/entities are read/consumed unchanged).
- Any frontend file (WC-1b).
- Any persisted entity / repository / MongoDB collection / index.

## 6. Protected Paths

- `.antigravity/**` — read-only for this work.
- `frontend/**` — no frontend change in WC-1 (WC-1b owns wiring); WorkCenterNext files consumed as the
  contract reference only.
- `frontend/Diten.Web/Controllers/WorkCenterController.cs`, `Views/WorkCenter/**`,
  `wwwroot/assets/js/WorkCenter/**`, `Services/WorkCenter/**` — legacy `/WorkCenter` **frozen**.
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent owned (see §15).
- `services/Diten.AuthService/**` — permission seed/grant is a separate MOD-0018 task.
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`,
  `services/Diten.DevEnablementService/**` — other domains.
- `services/Diten.Platform.Common/**` — shared base (`TenantScopedEntity`, `BaseEntity`, tenant/correlation
  context) consumed, never modified.
- MOD-0023 / MOD-0024 owned files and their module packs — consumed via contract only; the charter did not
  modify them.
- `docs/System Capability & Implementation Blueprint - master 7.xlsx` — never touched.
- `execution/registries/module-id-registry.md`, `execution/portfolio/blueprint-master-plan-reconciliation.md`
  — `CAND-CAP-0006` reservation already recorded by DCP-004; not re-touched here.

## 7. Dependencies

| Dependency | Use in this slice | Boundary |
|---|---|---|
| MOD-0023 Workflow (`ApprovalTask`, `WorkflowInstance`, `RuntimeAssignmentSnapshot`, `GetMyWorkflowTasks` foundation) | Provider source data | Read-only; no approval semantics re-implemented; no endpoint change |
| MOD-0018 RBAC/ABAC | Effective permission for `actions[]` eligibility | No permission computation invented; consumed via existing seam; seed separate |
| MOD-0021 Audit | Not written by WC-1 | Read/projection only |
| `ICurrentUserContext` / correlation (Platform Common) | Current user + correlation id | Reused, not modified |
| `Response<T>` envelope + `CustomBaseController` | API result shape | Reused per live convention |
| DCP-004 charter | Provider law (§10), status map (§10.1), boundary (§4) | Authoritative governance |
| `fixture-contract.js` | Work-item shape/enums/invariants | Executable authority; DTO conforms |

### Downstream WC seams (NOT built here)

WC-2 (working-time/calendar), WC-3 (assignee resolver), WC-4 (notification), WC-5 (provider registry) remain
sequential follow-ups (charter §8). WC-1 only introduces the `IWorkItemProvider` seam so WC-5 can add
providers without rewrite.

## 8. Runtime Constraints

1. **Backend-only, read/projection-only.** No state write; no command endpoint; no persisted entity.
2. The projection is **pure/deterministic** for the same provider input — same `ApprovalTask` set →
   identical projection.
3. Raw provider status text is **never** parsed to infer normalized lifecycle, waiting, eligibility, or
   actions; only the `ApprovalTaskStatus` enum drives the §10.1 map.
4. `actions[]` is the **single authoritative array**, resolved server-side; the browser never adds an action.
5. Terminal items (`Approved`/`Rejected`/`Cancelled`/`TimedOut` → `Done`/`Cancelled`) expose **no enabled
   inline state-changing action** (readonly).
6. `Delegated` tasks are **hidden** from the current actor's projection (disposition, not active work).
7. Concurrency is one projection-level token from `ApprovalTask.Version`; per-action token copies are
   forbidden (contract invariant `ACTION_CONCURRENCY_DUPLICATE`).
8. `normalizedStatus == Waiting` and `waitingContext` are a **bidirectional pair** (contract invariant); WC-1
   sets `waitingContext` only for `WaitingEvidence`.
9. **Tenant isolation:** `TenantId` resolved server-side (`ICurrentUserContext`/tenant context), never from
   client payload; a cross-tenant read returns empty with no metadata leak.
10. Provider scope = MOD-0023 only; the `IWorkItemProvider` list is a single provider in WC-1 but the handler
    iterates a provider collection so WC-5 adds providers additively.
11. `CAND-CAP-0006` never appears in any runtime file, namespace, or literal.

## 9. Layout & Shell Contract

- `shell: none` — backend-only slice; no Razor view, no layout.
- No frontend route is added (WC-1b owns frontend wiring).
- `golden_reference: none` is required: this is a non-CRUD backend read projection, not a DataTable module.

## 10. Backend File Convention

Follows the live `Diten.Platform` CQRS convention (MOD-0023 §6 / Golden Reference):

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/
├── Queries/
│   └── GetMyWorkItemsQuery.cs                 (sealed record, IRequest<Response<IReadOnlyList<WorkItemProjectionDto>>>)
├── Handlers/
│   └── QueryHandlers/
│       └── GetMyWorkItemsHandler.cs           (sealed class, NO Query suffix)
├── Providers/
│   ├── IWorkItemProvider.cs                   (WC-5 extension seam)
│   └── WorkflowApprovalWorkItemProvider.cs    (MOD-0023 provider — only one bound in WC-1)
├── Services/
│   ├── IWorkItemProjectionService.cs
│   └── WorkItemProjectionService.cs           (status normalize + actions[] eligibility + source join + title/concurrency)
└── WorkAggregationModels.cs                    (ALL projection DTOs in one file)
```

Rules (per convention):
- Query is a **sealed record**; handler is a **sealed class with no Query/Request suffix**
  (`GetMyWorkItemsHandler`).
- `Handlers/QueryHandlers/` separation is mandatory (no command handlers in this read-only slice).
- One `public record`/`class` per file **except** `WorkAggregationModels.cs`, which holds all DTOs (single
  models file per convention).
- Controller inherits `CustomBaseController`, stays thin, dispatches via `IMediator`, returns `Response<T>`,
  reuses `ICorrelationContext`.
- **Do NOT create** any new base entity/repository/tenant/correlation/audit/permission infrastructure.

### `actions[]` eligibility resolution (server-side)

```text
provider native rules (ApprovalTaskStatus, CommentRequired, EvidenceRequired)
  + effective permission (platform.workflow.tasks.approve/reject/delegate/... via MOD-0018)
  + assignment (RuntimeAssignmentSnapshot resolved/candidate) / SoD (as MOD-0023 exposes it)
  + blockers (evidence/comment required → approve disabled + disabledReasonCode)
        ↓
   one authoritative actions[]   (each: code, label(resource-key), enabled, source, disabledReasonCode?, safety flags)
```

- SoD / delegation eligibility is used **as far as MOD-0023 batches expose it**. If a signal is not yet
  provided by MOD-0023, WC-1 **phases** it (documented gap) rather than inventing eligibility.
- Actions render as **effective commands only**; source navigation / audit / related-record links are **not**
  in `actions[]` (contract rule).

### OD-WC-04 — `providerContractVersion` (charter open decision, resolved here)

`IWorkItemProvider` declares `ProviderCode` + `ProviderContractVersion`; each projected `source` carries the
provider's `providerContractVersion`. WC-1 fixes the handshake: the projection service validates that a
provider's declared contract version is one it can map, and rejects/ skips (logged) an unknown version rather
than silently mis-projecting. This satisfies charter OD-WC-04 at the WC-1 provider-abstraction layer
(certification of external providers remains WC-5).

## 11. Frontend File Contract

**N/A — backend-only slice.** No `.cshtml`, no `wwwroot/js`, no `.resx` is added by WC-1. Label fields use the
`{ kind: resource, key, args }` discriminated form so the **existing** WorkCenterNext 7-language resources
resolve them; introducing/wiring those resource keys into the live UI is **WC-1b** (frontend wiring), gated by
the standing 7-language l10n rule (§14).

## 12. Validation Rules

The projection is validated against the **contract** (`fixture-contract.js` invariants) as the acceptance
oracle. Field-level rules the projection service must satisfy:

| Field / rule | Validation |
|---|---|
| `workIntent` | `approval` (one of the five canonical intents) |
| `assignmentMode` | supported value (`approval`) |
| `normalizedStatus` | one of `Pending/InProgress/Waiting/Done/Cancelled`; produced only via §10.1 map |
| `taskLifecycle` | `notApplicable` (non-task intent) |
| `nativeStatus.code` | non-empty (`ApprovalTaskStatus` name); `label` a valid resource label |
| `waitingContext` | present **iff** `normalizedStatus == Waiting` |
| `actions[]` | unique `code`; each has `enabled` + `source`; disabled action has `disabledReasonCode` + reason label |
| `concurrency` | exactly one `{ kind:'version', token }`; no per-action duplication |
| `source` | `providerCode/objectType/objectId` required; `objectType`/`objectId` parsed from `ObjectRef`; `deepLink` optional |
| `lifecycleOwner` | set to `workflow` (required because ≠ source object module) |
| `title` | resolved (provider-supplied or deterministic localized fallback) — never empty, never raw fabricated text |
| Terminal item | `Done`/`Cancelled` → no enabled inline state-changing action |
| Tenant | resolved server-side; cross-tenant read → empty/no leak |

## 13. Failure Path to Verify

- **Cross-tenant read**
  - Expected: empty result; no metadata leak; `TenantId` never taken from client payload.
- **Unknown/unmapped `ApprovalTaskStatus`**
  - Expected: projection fails safe (item excluded + logged) rather than emitting an invalid `normalizedStatus`.
- **Unauthorized action (no `platform.workflow.tasks.approve`)**
  - Expected: `approve` projected **disabled** with `disabledReasonCode`; never enabled by the browser.
- **Evidence/comment required**
  - Expected: `approve` disabled with the exact `disabledReasonCode` (e.g. `EVIDENCE_REQUIRED`); requirement
    surfaced.
- **Delegated task**
  - Expected: hidden from the current actor's projection.
- **Terminal (Approved/Rejected/Cancelled/TimedOut)**
  - Expected: readonly projection; `normalizedStatus` per §10.1 (TimedOut → `Cancelled`); no enabled inline
    state-changing action.
- **Concurrency**
  - Expected: exactly one projection-level token from `ApprovalTask.Version`; no per-action copy.
- **Contract violation (any invariant)**
  - Expected: projection unit test fails against `fixture-contract.js` invariants; item not silently shipped.

## 14. Authorization Convention

```text
Policy:     [Authorize]                                  // tenant user (JWT)
Permission: [HasPermission("platform.work-aggregation.inbox.view")]   // NEW read key (PKS-001 lowercase-dotted)
Actions eligibility (consumed, not seeded here):
            platform.workflow.tasks.approve / reject / delegate / request-info / cancel
Actor type: tenant_user (platform_admin passes all)
```

- The new read permission constant is **defined locally** in `Diten.Platform`; the **seed/grant is a separate
  MOD-0018 / `Diten.AuthService` task** — this pack does not edit AuthService. Until seeded, the endpoint is a
  release blocker (documented).
- WC-1 **consumes** MOD-0018 effective permissions to resolve `actions[]` eligibility; it never computes or
  grants permissions and the browser is never the authority.
- 7-language localization gate applies to any user-facing label wired in WC-1b (charter §15).

## 15. Gateway / API Routing Decision

```text
Karar: Gateway değişikliği — CONFIRM at implementation (likely covered by existing platform catch-all).

- Frontend (WC-1b) calls Gateway 5000 / same-origin; never the Platform service port directly.
- Verify whether an existing `/api/v1/{everything}` or platform catch-all already covers
  `api/v1/work-items/**` for GET.
- If a new explicit Ocelot route is required, it is a SEPARATE integration-agent task; this pack does not
  touch `ocelot.json` (protected path).
```

## 16. Acceptance Criteria

### Governance and boundaries
- [ ] Governance identity stays `CAND-CAP-0006`; **no** Blueprint `MOD-xxxx` invented; `CAND-CAP-0006` appears
  in **no** runtime file/namespace/literal.
- [ ] Pack status stays `draft` until explicit user approval; DCP-004 approval alone does not authorize code.
- [ ] Only the authorized `Features/WorkAggregation` + controller + DI + tests scope is changed.
- [ ] MOD-0023 files, legacy `/WorkCenter`, `ocelot.json`, `Diten.AuthService`, Blueprint `.xlsx`, and the
  registry/ledger are **unchanged**.
- [ ] Approval semantics are **read/projected only**; no command endpoint, no state write.

### Contract conformance
- [ ] `WorkItemProjectionDto` matches `fixture-contract.js` enums and invariants field-by-field (contract =
  authority); a `validateWorkItem`-equivalent assertion passes for every projected item.
- [ ] `normalizedStatus` produced **only** via the §10.1 map, including **TimedOut → Cancelled**; raw status
  text is never parsed.
- [ ] Exactly one authoritative `actions[]`, resolved by native + permission + assignment/SoD + blocker;
  browser invents nothing; terminal items are readonly.
- [ ] `source.objectType`/`objectId` parsed from `WorkflowInstance.ObjectRef`; `lifecycleOwner: workflow`;
  `title` resolved (provider or localized deterministic fallback), never empty.
- [ ] Exactly one projection-level `concurrency` token from `ApprovalTask.Version`; no per-action copies.
- [ ] `waitingContext` present iff `normalizedStatus == Waiting` (WaitingEvidence).

### Read-only / isolation
- [ ] No state is written; no persisted entity/collection/index is created.
- [ ] Cross-tenant isolation verified (server-side `TenantId`, empty result, no leak).

### Extensibility
- [ ] Only the MOD-0023 provider is bound, but the handler iterates an `IWorkItemProvider` collection so WC-5
  adds providers **without** rewriting the projection.
- [ ] OD-WC-04 handled: `IWorkItemProvider` declares `ProviderCode` + `ProviderContractVersion`; unknown
  version is rejected/skipped, not mis-projected.

### Gates (linked, not re-written — charter §15)
- [ ] 7-language tenant l10n (labels in resource-key form; UI wiring in WC-1b) — [charter §15].
- [ ] No inline CSS FG-003 (N/A backend; applies to WC-1b) — [charter §15].
- [ ] Branch/commit policy GIT-002 (one branch per slice; commit only when done) —
  [git-safety.md](../../../../.antigravity/rules/git-safety.md).
- [ ] Legacy `/WorkCenter` untouched — [charter §15].

## 17. Test Expectations

### Projection unit tests (per behavior)
- One test per `ApprovalTaskStatus` → correct `normalizedStatus` (all 8 values, incl. TimedOut → Cancelled,
  Delegated → hidden).
- `actions[]` eligibility: approve enabled with permission + no blocker; disabled with `disabledReasonCode`
  when unauthorized / evidence-required / comment-required.
- Terminal readonly: Approved/Rejected/Cancelled/TimedOut → no enabled inline state-changing action.
- `waitingContext` bidirectional pairing with `Waiting`.
- Source join: `ObjectRef` → `objectType`/`objectId`; `lifecycleOwner: workflow`; title fallback resolves.
- Concurrency: single token from `Version`; no per-action duplication.
- **"No state written" invariant:** the projection path performs zero repository writes (assert via mock
  repositories / no write calls).
- Cross-tenant isolation: other tenant's tasks excluded; no leak.
- **DTO-contract-conformance:** every projected item satisfies the `fixture-contract.js` invariant set
  (`validateWorkItem`-equivalent) — the contract is the oracle.

### Build
- `dotnet build` for `Diten.Platform.API` (+ Application) PASS.
- Backend tests under `tests/Diten.Platform.Application.Tests/WorkAggregation/` PASS.
- No frontend/gateway build change expected (backend-only).

## 18. Ready-for-dev Checklist

- [ ] AGENTS.md (§6 runtime, §7–10 pack rules, DCP-002 gate) read.
- [ ] DCP-004 charter (§4/§7/§8/§10/§20) read; WC-1 is charter §8 order 1.
- [ ] `fixture-contract.js` read and treated as executable authority.
- [ ] MOD-0023 provider code read (`ApprovalTask`, `WorkflowInstance`, `RuntimeAssignmentSnapshot`,
  `ApprovalTaskStatus`, `GetMyWorkflowTasks*`, `WorkflowTaskDto`).
- [ ] DCP-002 candidate preflight exit 0 for `CAND-CAP-0006`.
- [ ] Frontmatter mandatory fields complete; `shell: none`, `golden_reference: none`, `entity_base: BaseEntity`
  justified (no entity created).
- [ ] Backend File Convention matches live Platform CQRS (sealed record query, no-suffix handler, single
  models file).
- [ ] Ownership boundaries (reuse MOD-0023/MOD-0018/MOD-0021, never re-implement) stated.
- [ ] Repo scope + protected paths stated.
- [ ] `actions[]` eligibility, status map, source join, title/deepLink gaps, concurrency resolved on paper.
- [ ] Failure paths incl. cross-tenant, unauthorized, terminal, concurrency, contract violation.
- [ ] Authorization: new read key defined locally; seed = separate MOD-0018 task.
- [ ] Gateway decision explicit (integration-agent task if a route is needed).
- [ ] Acceptance criteria testable; test expectations cover projection/isolation/no-write/contract.
- [x] User reviewed the draft and set `status: ready-for-dev` (condition 2 of CAP-001 §7) — 2026-07-24.

## 19. Implementation Notes

### Required implementation order
1. Define `WorkAggregationModels.cs` DTOs to the `fixture-contract.js` shape.
2. Define `IWorkItemProvider` (with `ProviderCode` + `ProviderContractVersion`) and the projection service
   interface.
3. Implement `WorkItemProjectionService` (pure): §10.1 status map, `actions[]` eligibility, source join,
   title/concurrency.
4. Implement `WorkflowApprovalWorkItemProvider` over the `GetMyWorkflowTasks` foundation (candidate/assignee
   resolution reused).
5. Implement `GetMyWorkItemsQuery`/`Handler` (iterate providers → project → tenant-scoped list).
6. Add thin `WorkItemsController` (`GET api/v1/work-items/mine`).
7. Register provider + service in existing DI.
8. Write projection unit tests (per status, eligibility, isolation, no-write, contract conformance); build.

### Charter linkage
- Closes **DCP-004 §20 F2** (missing projection layer). **F1** (MOD-0023 pack stale text) → **BL-020**;
  **F3/F4** (ES provider + fixture-truth) → **BL-018/BL-021**; Blueprint `MOD-xxxx` → **BL-019**.
- Provider law applied: §10.1 status map, §10.2 `actions[]` rule, §10.3 `source`/`lifecycleOwner`, §10.4
  Binding A (MOD-0023 only).

### DCP-002 preflight
`python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0006 --name "Work Aggregation / Task Center (Görev Merkezi)"`
→ exit 0 (governance identity valid; not Blueprint-backed; not in runtime).

## 20. Follow-up Items

Explicitly **not** authorized by this draft:

1. **WC-1b** — frontend mock → real API wiring (WorkCenterNext consumes `GET api/v1/work-items/mine`), plus
   the 7-language resource keys for projected labels.
2. **WC-5** provider registry — how non-workflow modules register as `IWorkItemProvider`s (the seam WC-1
   introduces; the registry itself is WC-5).
3. **WC-3** assignee resolver, **WC-2** working-time/calendar seam, **WC-4** notification seam (charter §8).
4. **BL-018** — Enterprise Strategy as a real WC provider (Binding A / MOD-0023).
5. **BL-019** — Blueprint canonical `MOD-xxxx` allocation + `CAND-CAP-0006 → MOD-xxxx` alias (after WC-1
   proven).
6. **BL-020** — MOD-0023 pack reconciliation (stale "no code produced" text).
7. **BL-021** — Enterprise Strategy fixture-truth cleanup (QA).
8. **MOD-0018 seed** — grant `platform.work-aggregation.inbox.view` (separate security task).
9. **Command execution via WorkCenter** — remains on MOD-0023 endpoints; any future in-aggregator command
   path is a separate approved slice with full authorization/concurrency/idempotency (charter/MOD-0024 rules).

Each follow-up requires its own approved module-pack slice (single-module) or DCP slice (multi-domain/Gateway/
Auth/ordered) before production implementation.
