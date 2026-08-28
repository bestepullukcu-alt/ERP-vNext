---
id: MOD-0024
name: Task & Checklist Engine
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: none
entity_base: BaseEntity
status: review
owner: ali.tufanoglu
branch: feature/pss/mod-0024-workcenter-task-detail
started: 2026-07-24
target: 2026-08-07
form_field_count: 0
---

# MOD-0024 — Task & Checklist Engine

> **Draft scope:** WorkCenterNext canonical fixture contract, scenario catalog, task-detail resolver, and
> resolver-driven Task Detail UX/UI only. This slice is frontend-only and mock-driven. It does not authorize
> a production MOD-0024 backend, persistence, gateway route, provider integration, or runtime permission seed.
>
> **Canonical identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0024
> --name "Task & Checklist Engine"` returned exit code `0` on 2026-07-24.

## 1. Module Summary

MOD-0024 is the canonical owner of reusable generic task and checklist primitives. It owns task/checklist
state only when MOD-0024 itself is the source provider, such as a future generic platform task or self-task.
It does not own the native lifecycle of tasks supplied by Finance, HCM, PPM, MDM, or another business module.

This draft authorizes a bounded WorkCenterNext design slice that makes the existing isolated, mock-driven
tenant UI internally consistent before any backend aggregation contract is implemented:

1. Define one canonical, source-agnostic work-item fixture contract.
2. Separate work intent, assignment, ownership, admission, normalized status, task lifecycle, execution,
   timer, waiting, and system-safety concerns.
3. Provide canonical, provider-example, edge-case, trigger-only, and migration/regression fixture groups.
4. Resolve each fixture into a deterministic Task Detail surface without inventing provider actions.
5. Render the standalone `/WorkCenterNext/Details/{id}` page from the resolver output.
6. Preserve `/WorkCenter` unchanged for comparison and rollback.

This is not a CRUD/DataTable create/edit module. `golden_reference: none` and `form_field_count: 0` are
intentional. The list/table surfaces already exist; this slice changes the task-detail contract and rendering,
not a create/edit form.

### Delivery slice

| Slice | Included in this draft |
|---|---|
| Canonical fixture schema and invariant validation | Yes |
| Scenario fixture data | Yes |
| Pure task-detail resolver | Yes |
| Standalone Task Detail page | Yes |
| Seven-language WorkCenterNext localization | Yes |
| Browser-side mock transitions for scenario demonstration | Yes, non-authoritative |
| Production aggregation API | No |
| MongoDB entities/repositories | No |
| Gateway changes | No |
| Provider certification/runtime registration | No |
| Split/Kanban/Calendar views | No; BL-015 |

## 2. Ownership and Boundaries

### MOD-0024 owns

- Generic `Task`, `TaskAssignment`, `ChecklistTemplate`, and `ChecklistRun` primitives in a later separately
  approved backend slice.
- Native lifecycle only for tasks for which MOD-0024 is the declared source provider.
- Generic/self-task execution and checklist semantics when that later runtime slice is approved.
- The WorkCenterNext frontend fixture vocabulary and resolver contract in this draft slice.

### WorkCenterNext owns

- Presentation of provider projections.
- Personal overlay only: pin, snooze, seen/unseen, personal planned date, personal reminder, and personal note.
- Client interaction state such as `submittingActionCode`; this must not mutate canonical business state.
- Deterministic selection of visible UI blocks from already-resolved projection data.

### WorkCenterNext does not own

- A source module's native task lifecycle, deadline, dependency graph, time record, or business object state.
- Approval workflow semantics or decisions owned by MOD-0023.
- Effective authorization, delegation eligibility, or permission grants.
- Evidence linking/completeness owned by MOD-0031, retention/legal hold owned by MOD-0030, and binary content
  owned by an approved document/storage provider.
- Documentation structures/metadata owned by MOD-0028, controlled-document/version lifecycle owned by
  MOD-0029, and evidence-link/completeness semantics owned by MOD-0031.
- Audit system-of-record data owned by MOD-0021.
- Provider-side action eligibility, concurrency resolution, or command result.
- Project scheduling, Gantt, WBS editing, or dependency graph editing.

### Approval/review boundary

- `approval` is a separate work intent and never a stage in the operational task lifecycle.
- An operational task waiting for review remains visible to its owner as `taskLifecycle: PendingReview`.
- A reviewer receives a separate `review` work item when the source provider models review that way.
- Task and review fixtures may be linked through minimal `relatedWorkItems[]`; they do not share mutable
  frontend state.
- MOD-0024 does not implement approve/reject/delegate semantics. WorkCenterNext may render effective actions
  supplied by a MOD-0023/source-provider projection without taking ownership of those semantics.

### Source ownership examples

| Projection | Native lifecycle owner |
|---|---|
| MOD-0024 self-task | MOD-0024 |
| Finance close task | Finance source module |
| HCM onboarding task | HCM source module |
| PPM task instance | MOD-0117 / PPM |
| Approval work item | MOD-0023 |
| Review work item | Declared source/review provider |
| Personal overlay | WorkCenter aggregation layer |

### Documentation and evidence boundary

WorkCenter may present document/evidence context but does not become a document or evidence system of record:

| Concern | Authority | WorkCenter treatment |
|---|---|---|
| Documentation structure, collection and metadata | MOD-0028 | Read-only source/context reference |
| Controlled document and immutable version | MOD-0029 | Read-only document/version reference and safe navigation |
| Formal controlled-document approval/review | Future declared workflow/provider; not MOD-0029-FU01 | Separate approval/review projection only when that provider exists |
| Object-to-evidence link and configured completeness | MOD-0031 | Provider-supplied evidence projection/blocker metadata |
| Retention and legal hold | MOD-0030 | Read-only restriction/blocker projection when supplied |
| Binary content and access token | Approved external repository/provider | Never copied into fixture payload; access checked by authoritative endpoint |
| Work-item attachment presentation | Declared attachment/document provider | `workItemCapabilities`-driven block; no storage/version ownership |

`MOD-0029-FU01` uses `ACTIVE` as a technical current-version state, not an approval decision. WorkCenter must
not infer an approval work item from `DRAFT`, `ACTIVE`, `SUPERSEDED`, or `ARCHIVED`. A controlled-document
review/approval item may exist only after a declared lifecycle provider supplies an actionable projection.
Likewise, an evidence requirement may disable an existing effective action, but it does not create a task
lifecycle or action by itself.

## 3. Owned Objects

### Runtime objects

No new persisted runtime object is authorized in this draft slice.

### Frontend contract objects

| Object | Purpose |
|---|---|
| `WorkItemFixture` | Source-agnostic input projection used by mock scenarios |
| `TriggerOnlyFixture` | Non-work-item response projection with no task lifecycle or ownership |
| `TriggerResponseSurface` | Trigger resolver output consumed by the inbox-inline renderer |
| `RawMigrationFixture` | Legacy input retained only for adapter/regression coverage |
| `EffectiveAction` | Single authoritative browser-facing action representation |
| `WaitingContext` | Why/who/since/expected-until detail for a waiting work item |
| `BlockedState` | Source/aggregation-computed block signal and affected actions |
| `SystemState` | Fresh/stale/unavailable/authority/processing/reconciliation state |
| `PersonalOverlay` | WorkCenter-owned personal fields |
| `RelatedWorkItem` | Minimal link between task/review/follow-up items |
| `BusinessContext` | Bounded, render-only provider context using allowlisted field types |
| `Concurrency` | Single projection-level optimistic-concurrency token |
| `TaskDetailSurface` | Pure resolver output consumed by the renderer |
| `FixtureExpectation` | Expected surface, blocks, notices, actions, and read-only state |

### Fixture groups

```text
canonical/
  task/
  approval/
  review/
  issue/
  exception/
edge-cases/
provider-examples/
  enterprise-strategy/
  documentation/
trigger-only/
migration/
```

- `canonical/` drives target UX.
- `provider-examples/` proves that real provider-shaped data conforms to the same canonical
  `WorkItemFixture`, validator, resolver, and surface enums; it is not a separate contract.
- `edge-cases/` verifies system safety, blockers, delegation, deep-link, and permission behavior.
- `trigger-only/` contains meeting invitations and similar non-task lifecycle items.
- `migration/` contains legacy PendingApproval, legacy blocker, and legacy information-request shapes.
- Trigger-only and migration fixtures must never expand the canonical task lifecycle.
- Provider examples may add bounded `businessContext` and `relatedRecords`, but may not extend canonical
  intent, lifecycle, action, or surface enums and may not introduce provider-specific resolver branches.

Fixture pipelines are separate and explicit:

```text
WorkItemFixture
→ validateWorkItem()
→ resolveTaskDetailSurface()
→ Task Detail renderer

TriggerOnlyFixture
→ validateTrigger()
→ resolveTriggerResponse()
→ inbox-inline renderer

RawMigrationFixture
→ adaptLegacyFixture()
→ validateWorkItem()
→ resolveTaskDetailSurface()
```

`validateTrigger`, `resolveTriggerResponse`, and `adaptLegacyFixture` are pipeline components, not fixture
contract objects. A raw migration fixture and a trigger-only fixture must never enter the Task Detail resolver
directly.

`TriggerOnlyFixture` has its own minimal contract:

```text
fixtureKind: triggerOnly
id
triggerType
source
systemState
actions[]
concurrency
expectation
```

It does not carry assignment, ownership, admission, task lifecycle, execution, timer, waiting, or
work-item-capability fields. Its `actions[]` entries use the same `EffectiveAction` localization/enabled/safety
shape as WorkItem actions. `TriggerResponseSurface` contains the inline presentation, action-code references,
notice/error state, and refresh/remove-after-response behavior; it does not produce a Task Detail route.

## 4. Entity Fields

No MongoDB entity field is introduced in this draft. `entity_base: BaseEntity` records the future canonical
Diten.Platform tenant-aware runtime posture only; no entity class may be created by this slice.

### Canonical fixture fields

| Field | Required | Values / shape | Authority |
|---|---:|---|---|
| `id` | Yes | Stable fixture/work-item ID | Provider/aggregation |
| `workIntent` | Yes | `task`, `approval`, `review`, `issue`, `exception` | Provider |
| `assignmentMode` | Yes | `direct`, `approval`, `groupQueue`, `offered` | Provider |
| `ownershipState` | Yes | `unowned`, `assigned`, `owned`, `notApplicable` | Aggregation projection |
| `admissionState` | Yes | `pendingAcceptance`, `pendingClaim`, `pendingOffer`, `admitted`, `notApplicable` | Aggregation projection |
| `normalizedStatus` | Yes | `Pending`, `InProgress`, `Waiting`, `Done`, `Cancelled` | Aggregation normalization |
| `taskLifecycle` | Conditional | `Open`, `Planned`, `InProgress`, `Waiting`, `PendingReview`, `Done`, `Cancelled`, `notApplicable` | Task provider |
| `nativeStatus` | Yes | `{ code, label }` | Provider |
| `executionState` | Yes | `notStarted`, `active`, `paused`, `notApplicable` | Provider |
| `timerState` | Yes | `inactive`, `running`, `paused`, `notApplicable` | Time provider |
| `waitingContext` | Conditional | `{ type, waitingOn, since, expectedUntil }` | Provider |
| `systemState` | Yes | `fresh`, `stale`, `sourceUnavailable`, `authorityEnded`, `processing`, `reconciliationRequired` | Aggregation |
| `viewerRole` | Yes | Provider-defined role mapped to supported presentation role | Aggregation |
| `delegationContext` | Conditional | Grant/person/scope presentation data | Aggregation |
| `workItemCapabilities` | Yes | Declared conditional UI blocks/features | Provider |
| `actionDepth` | Yes | `inline`, `deeplink` | Provider certification/aggregation |
| `blockedState` | Conditional | Blocked flag, reason, affected action codes, blockers | Provider/aggregation |
| `actions` | Yes | Effective, browser-facing action set | Aggregation backend |
| `concurrency` | Conditional | `{ kind: version|etag|opaque, token }`; one projection-level truth | Provider/aggregation |
| `lifecycleOwner` | Conditional | Stable provider reference; required when different from source provider | Aggregation |
| `businessContext` | No | Bounded render-only sections/fields | Provider |
| `relatedRecords` | No | Read-only provider/source record links; never implicit blockers | Provider |
| `personal` | Yes | Pin/snooze/seen/plan/reminder/note | WorkCenter overlay |
| `relatedWorkItems` | No | Minimal related item references | Provider/aggregation |
| `reviewMeetingPolicy` | No | `{ requirement: notAllowed|optional|required, meetingId?, scheduledAt? }`; review/approval collaboration policy | Provider |
| `source` | Yes | Stable provider code/contract version, source system, object type/ID, optional process instance, deep link | Provider/aggregation |
| `expectation` | Fixture only | Expected resolver result | Test fixture |

The source identity must not invent a module ID. Until Enterprise Strategy receives a Blueprint/registry-backed
canonical module identity, its provider examples use stable `providerCode: enterprise-strategy` and
`sourceSystem: Diten.EnterpriseStrategyService`; they do not use `MOD-ESBP`. `providerCode` remains stable
even if the technical service name changes. `processInstanceId` (or an explicitly named equivalent) is
required when one source object can produce recurring or parallel work items.

### Effective action shape

The browser receives one authoritative `actions[]` array. It does not receive parallel
`availableActions`/`resolvedActions` arrays.

```json
{
  "code": "complete",
  "label": {
    "kind": "resource",
    "key": "WorkCenterNext_Action_Complete",
    "args": {}
  },
  "semanticType": "complete",
  "enabled": false,
  "disabledReasonCode": "CHECKLIST_INCOMPLETE",
  "disabledReason": {
    "kind": "resource",
    "key": "WorkCenterNext_ActionDisabled_ChecklistIncomplete",
    "args": {}
  },
  "source": "provider",
  "requiresConfirmation": true,
  "requiresReason": false,
  "requiresEvidence": false,
  "supportsBulk": false,
  "riskLevel": "normal"
}
```

Raw provider actions, if retained for diagnostics, remain inside the aggregation/support boundary and are
not sent beside `actions[]` in the normal browser payload.

`actions[]` contains only effective commands executable from WorkCenter. Source navigation, audit links,
related-record links, document preview/download links, and source recovery deep-links are not command actions.
They are rendered through `sourceNavigation` or the relevant read-only reference block.

### Review meeting policy

A task/review provider may require or allow a review meeting before the reviewer makes the final decision:

- `notAllowed`: no meeting command is projected.
- `optional`: `approve/signoff` and `scheduleReviewMeeting` may both be enabled.
- `required`: before a meeting is scheduled, `approve/signoff` remains visible but disabled with
  `REVIEW_MEETING_REQUIRED`; `scheduleReviewMeeting` is the primary enabled command.
- Scheduling a meeting never approves the work item and does not synthesize a task lifecycle transition.
- After Calendar returns an authoritative meeting reference, a new projection may carry `meetingId` and
  `scheduledAt`; the provider/aggregation then decides whether the decision action becomes enabled.
- The browser never infers `scheduleReviewMeeting`. It appears only when supplied in effective `actions[]`.
- The Calendar event remains Calendar-owned. WorkCenter renders only the related meeting reference and
  source navigation.

### Concurrency shape

Concurrency has one canonical location in the projection:

```json
{
  "concurrency": {
    "kind": "version",
    "token": "17"
  }
}
```

The same token must not be repeated inside each action. Every future state-changing command uses the
projection token by contract; a per-action `expectedVersion`, `expectedConcurrencyToken`, or
`requiresConcurrency` duplicate is not allowed. A future command envelope copies the projection token:

```json
{
  "workItemId": "WC-A101",
  "actionCode": "approve",
  "expectedConcurrency": {
    "kind": "version",
    "token": "17"
  },
  "idempotencyKey": "generated-per-command"
}
```

This is a future contract example only and does not authorize a backend command endpoint in this slice.

The command-safety invariant applies to every command-capable projection, including `WorkItemFixture` and
`TriggerOnlyFixture`:

```text
one or more enabled inline actions
→ projection-level concurrency is required

readonly projection or navigation-only projection
→ concurrency is optional
```

Both fixture kinds use the same `actions[]` plus `concurrency` validation rule. This common rule does not
introduce inheritance or merge the trigger and work-item lifecycles.

### Localization shape

Mock fixtures use resource labels so the same fixture validates in all seven tenant languages. A future
production provider may instead supply a display label with its locale. Label values use one discriminated
form, never both:

```text
{ kind: resource, key, args }
or
{ kind: display, text, locale, source }
```

Raw provider status/labels must not be used to infer normalized lifecycle, waiting, eligibility, or actions.

### Bounded business context

`businessContext` is render-only and uses generic `sections[]`/`fields[]`. Allowed field value types are:
`text`, `number`, `currency`, `percentage`, `date`, `datetime`, `boolean`, `status`, `person`, `reference`,
and `link`. Raw HTML, script, provider templates, arbitrary component/class names, executable expressions,
and unvalidated URLs are prohibited.

The bounded limits for this slice are:

| Limit | Value |
|---|---:|
| `maxSections` | 6 |
| `maxFieldsPerSection` | 8 |
| `maxTextLengthPerField` | 2000 characters |
| `maxPrimaryFields` | 8 across the entire detail |
| `maxRelatedRecords` | 20 |

`importance: primary` fields are directly visible in decision context and are capped by
`maxPrimaryFields`; `importance: secondary` fields remain in their section and may be collapsed. Allowed
links are same-origin relative routes and `https` URLs from an approved provider. `javascript`, `data`, and
`file` schemes are prohibited. Future backend/provider certification owns host/route authorization; the mock
validator enforces shape and scheme.

Sensitive fields may carry `classification`, `accessState`, and `redacted` presentation metadata, but an
unauthorized raw value must never be sent to the browser and hidden only with CSS. A provider relation becomes
a WorkCenter blocker only when the effective projection explicitly includes it in `blockedState`.

### Work-item capability vocabulary

The canonical `workItemCapabilities[]` values in this slice are:

```text
planning | execution | timeTracking | checklist | subtasks | dependencies |
attachments | evidence | activity | processStages | businessContext | relatedRecords
```

`informationRequest` is an action/interaction semantic and is not a capability. Review behavior is expressed
by `workIntent`, provider-native state, effective actions, and optional `processStages`; `reviewFlow` is not a
separate capability.

Data-backed capability validation is asymmetric so empty states remain valid:

| Capability | Data container | Validation |
|---|---|---|
| `timeTracking` | timer/time-entry data | Capability permits `inactive` plus an empty entry list; `running/paused` requires the capability |
| `checklist` | checklist data | Container may be empty; non-empty data without capability fails |
| `subtasks` | subtasks data | Container may be empty; non-empty data without capability fails |
| `dependencies` | dependency data | Container may be empty; non-empty data without capability fails |
| `attachments` | attachment data | Container may be empty; non-empty data without capability fails |
| `evidence` | evidence data | Container may be empty; non-empty data without capability fails |
| `activity` | activity data | Container may be empty; non-empty data without capability fails |
| `processStages` | process-stage data | Container may be empty; non-empty data without capability fails |
| `businessContext` | context sections | Container may be empty; non-empty data without capability fails |
| `relatedRecords` | related-record list | Container may be empty; non-empty data without capability fails |

`planning` and `execution` are behavior capabilities rather than list-container declarations. For all
data-backed capabilities: absent capability plus absent data is valid; present capability plus an explicit
empty container is valid; present capability plus populated data is valid; absent capability plus populated
data is invalid. Item type alone never supplies a capability.

### Client interaction state

`submittingActionCode` and similar UI-only fields are not part of `WorkItemFixture`. The UI may temporarily
disable a button while submitting but must not rewrite `action.enabled` as business state. A new projection
is applied after the command result.

## 5. Repo Scope

### Governance scope in this preparation workflow

- `execution/domains/platform-shared-services/module-packs/MOD-0024-task-checklist-engine.md`

### Authorized implementation scope after explicit approval

- `frontend/Diten.Web/Controllers/WorkCenterNextController.cs`
- `frontend/Diten.Web/Views/WorkCenterNext/**`
- `frontend/Diten.Web/Views/WorkCenterNext/WorkCenterNextIndex.cs`
- `frontend/Diten.Web/Resources/Views/WorkCenterNext/**`
- `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/**`
- `frontend/Diten.Web/wwwroot/assets/css/backbone-custom.css`
  - Only selectors scoped to `.wcn-*`, `.wcn-app`, or `.wcn-full-detail-*`.
- Focused WorkCenterNext frontend tests under the existing test convention discovered at implementation time.
- `docs/audits/mod-0024-workcenter-task-detail-audit.md`
- `docs/workcenter-rebuild-spec.md`
  - Documentation reconciliation only; implementation scope must not silently expand from this file.

### Existing user-change preservation

The authorized WorkCenterNext files are already modified in the working tree. Implementation must inspect
and preserve those edits. No file may be replaced wholesale without reconciling its current content.

## 6. Protected Paths

- `.antigravity/**` — read-only for this work.
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/WorkCenterController.cs`
- `frontend/Diten.Web/Views/WorkCenter/**`
- `frontend/Diten.Web/wwwroot/assets/js/WorkCenter/**`
- `frontend/Diten.Web/Services/WorkCenter/**`
- `frontend/Diten.Web/Models/WorkCenter/**`
- `gateway/**`
- `services/**`
- Other domain execution/module-pack files.
- Existing product-backlog items, especially BL-015, unless a separate explicit governance request authorizes
  their modification.

The legacy `/WorkCenter` surface must remain byte-for-byte untouched by the implementation slice.

## 7. Dependencies

| Dependency | Use in this slice | Boundary |
|---|---|---|
| MOD-0018 RBAC/ABAC | Represent permission-disabled scenarios | No permission computation in browser |
| MOD-0021 Audit Trail | Represent activity/audit projection | No audit persistence |
| MOD-0023 Workflow Designer | Approval work-item provider examples | No approval semantics in MOD-0024 |
| MOD-0028 Documentation Management | Documentation structure/metadata references | No structure, storage, or document ownership |
| MOD-0029 Controlled Documents | Controlled document/version references | FU01 technical ACTIVE state is not approval |
| MOD-0030 Records/Retention | Represent supplied retention/legal-hold restriction | No retention policy or enforcement |
| MOD-0031 Evidence Linking | Evidence-required fixture metadata and completeness projection | No evidence storage/upload/link runtime |
| MOD-0037 Reconciliation | Recovery-state fixture | No reconciliation worker |
| Enterprise Strategy provider candidate | Objective approval/review, demand review, decomposition examples | No service change or provider-specific resolver |
| Source business modules | Native status/context fixture examples | No source-specific logic in resolver |
| MOD-0013 Premium Modal | Confirm/reason/error interaction language | Reuse shared wrappers |

Documentation readiness note: the current MOD-0028 parent pack frontmatter says `approved`, while its own
summary still says direct-coding readiness is not complete and records an unresolved naming/EA gate.
MOD-0024 therefore treats all documentation connections in this slice as mock contract examples only and
must not use MOD-0028's frontmatter alone as proof that production integration is ready.

### WorkCenter backend seams

WC-1 through WC-5 in `docs/product-backlog.md` remain backend prerequisites and are not implemented here:

- WC-1 unified work-item provider contract.
- WC-2 working-time/calendar seam.
- WC-3 assignee resolver.
- WC-4 notification seam.
- WC-5 provider registry.

This frontend contract is design input to WC-1, not a production implementation of WC-1.

### Lookup decision

No new platform lookup key or tenant business lookup is authorized. Fixture enums are contract vocabulary,
not runtime lookup/master data. No hardcoded business lookup fallback may be introduced.

## 8. Runtime Constraints

1. Frontend-only and mock-driven; zero backend/API calls in this slice.
2. No persisted entity, seed, permission key, gateway route, or service registration.
3. WorkCenter never owns provider native lifecycle/status/time/dependency.
4. `taskLifecycle` applies only to operational `task` intent; other intents use `notApplicable` plus
   `normalizedStatus` and provider `nativeStatus`.
5. `executionState` is orthogonal and limited to `notStarted`, `active`, `paused`, `notApplicable`.
6. `timerState`:
   - `inactive`: no open timer session.
   - `running`: an open timer session is accumulating time.
   - `paused`: the same open timer session is retained but does not accumulate time.
7. `executionState: active` + `timerState: inactive` is valid.
8. Stop Timer and Pause Work are separate semantic actions.
9. Waiting detail lives in `waitingContext`; execution state must not duplicate Waiting/PendingReview/Done.
   `normalizedStatus: Waiting` and `waitingContext` are a canonical pair. Native status text must never be
   parsed to manufacture either value.
10. Personal snooze never changes `normalizedStatus`, `taskLifecycle`, or creates `waitingContext`.
    `InProgress + snoozed` remains in the Active lifecycle segment; `Planned + snoozed` remains Planned.
    Snoozed is a filter/signal, not a lifecycle segment.
11. Release returns a group item to `ownershipState: unowned` and `admissionState: pendingClaim`; `released`
    is an activity event, not an active state.
12. Decline/delegate/reassign/dispute/return are transition outcomes or disposition/activity records, not
    active ownership states.
13. Provider/aggregation actions are the only actions rendered. The browser must not invent `start`,
    `complete`, `approve`, `signoff`, or any other business action.
14. Browser-side mock transitions demonstrate UI only and must be visibly isolated from future authoritative
    backend behavior.
15. Every real state-changing command will require backend authorization, projection-level concurrency
    token validation, and idempotency in a
    later backend pack/slice.
16. Work-item capability absence means block absence; item type alone must not force
    checklist/time/attachment/evidence blocks.
17. `actionDepth: deeplink` sends complex work to the source and refreshes the projection on return.
18. Terminal projections expose no enabled inline state-changing action. Safe source navigation and readonly
    references remain outside `actions[]`; inline reopen/restore is not authorized in this slice.
19. Split, Kanban, Calendar, Gantt, and dependency editing are out of scope.
20. Meeting invitation is trigger-only, uses a trigger response resolver/renderer, and never enters the
    canonical Task Detail resolver.
21. Acknowledgment is classified by behavior, not its name. A trigger-only acknowledgment has no ownership,
    execution lifecycle, or execution capability and leaves/refreshes after response. A tracked, assignable,
    auditable acknowledgment is a work item.
22. Legacy PendingApproval is migration/regression data only.
23. Source-specific business contexts are provider-declared data, not resolver branches keyed to module ID.
24. Provider examples conform to the canonical contract and cannot extend its enums.

## 9. Layout & Shell Contract

- Shell: `tenant`
- Razor layout: `Layout = "_LayoutTenantShell";`
- Routes:
  - `/WorkCenterNext`
  - `/WorkCenterNext/Details/{id}`
- View folder: `frontend/Diten.Web/Views/WorkCenterNext/`
- The controller remains thin and does not populate a C# ViewModel; mock/projection data is loaded by the
  page script.
- **DEC-WC-DETAIL-01 — Proposed:** on explicit approval of this draft, the standalone detail route becomes
  canonical for this slice and split detail is deferred by BL-015. Until approval, the existing
  `docs/workcenter-rebuild-spec.md` decision remains the operative reference and is not superseded.
- Existing tenant shell and frozen `_Layout.cshtml` must not be modified.

Acceptance of this contract requires both Index and Details views to state `_LayoutTenantShell` explicitly.

## 10. Backend File Convention

### This draft slice

No backend file may be created or modified.

### Future production MOD-0024 slice

A separately approved backend slice must use the live Diten.Platform feature convention rather than the
stale paths previously recorded by this pack:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/
├── Commands/
├── Queries/
├── Handlers/
│   ├── CommandHandlers/
│   └── QueryHandlers/
├── Validators/
├── Services/
└── TaskModels.cs
```

- Commands/queries are sealed records.
- Handler names do not carry `Command`/`Query` suffixes.
- Validators do not carry `Command` suffixes.
- Controllers remain thin MediatR dispatchers.
- `Response<T>` envelope and existing tenant/correlation infrastructure are reused.

This convention is documentary only and does not authorize those files in this draft.

## 11. Frontend File Contract

`golden_reference: none` is required because this is a non-CRUD aggregation/detail experience.

```text
frontend/Diten.Web/
├── Controllers/
│   └── WorkCenterNextController.cs
├── Views/WorkCenterNext/
│   ├── Index.cshtml
│   ├── Details.cshtml
│   ├── _L10n.cshtml
│   └── WorkCenterNextIndex.cs
├── Resources/Views/WorkCenterNext/
│   └── WorkCenterNextIndex.{en,fr,es,zh,ar,ru,tr}.resx
└── wwwroot/assets/js/WorkCenterNext/
    ├── fixture-contract.js
    ├── fixtures/
    │   ├── canonical-fixtures.js
    │   ├── edge-case-fixtures.js
    │   ├── provider-examples/
    │   │   ├── enterprise-strategy-fixtures.js
    │   │   └── documentation-fixtures.js
    │   ├── trigger-only-fixtures.js
    │   └── migration-fixtures.js
    ├── task-detail-resolver.js
    ├── trigger-response-resolver.js
    ├── migration-fixture-adapter.js
    ├── mock-data.js
    ├── l10n.js
    └── app.js
```

Implementation may choose an equivalent smaller file split if the repository's browser asset loading/testing
constraints make nested fixture files disproportionate, but these responsibilities must remain separately
testable and must not collapse back into one lifecycle-heavy `app.js`.

### Rendering contract

The resolver returns:

```text
surfaceMode
readOnly
primaryActionCode
secondaryActionCodes
overflowActionCodes
visibleBlocks
notices
criticalBanner
personalActions
sourceNavigation
```

Supported surface modes:

```text
acceptance | claim | offer | execution | decision |
review | investigation | readonly | recovery | deeplink
```

The resolver output is a projection for rendering, not a new business state machine.

### Critical banner precedence

`criticalBanner` is singular and selected deterministically:

```text
authorityEnded
> sourceUnavailable
> reconciliationRequired
> stale
> claimedByOther
> hardBlocked
> overdue
```

`systemState` remains singular, so its safety states do not coexist; precedence still resolves the selected
system state against ownership, blocker, and deadline signals. This precedence controls presentation only; it
does not calculate action eligibility. The same condition must
not render as both banner and notice. A lower-priority condition may become a notice only when it adds useful,
non-duplicative guidance. On `authorityEnded`, previously cached business context that may no longer be
visible must be cleared/redacted by the replacement projection and must not be trusted or retained by the
browser.

## 12. Validation Rules

| Field/rule | Validation |
|---|---|
| `id` | Required and unique across all fixture groups |
| `workIntent` | Must be one of the five canonical intents |
| `assignmentMode` | Must be a supported value |
| `ownershipState` | Must not contain transition events such as `released` |
| `admissionState` | Must not contain historical disposition such as `declined` |
| `normalizedStatus` | Required for every canonical work item |
| `taskLifecycle` | Required for task; `notApplicable` for approval/review/issue/exception |
| `nativeStatus.code` | Required and non-empty |
| `nativeStatus.label` | Required valid discriminated resource/display localization value |
| `executionState` | Must not duplicate Waiting/PendingReview/Done |
| `timerState` | `running/paused` requires `timeTracking` capability |
| `waitingContext` | Present iff `normalizedStatus: Waiting`; personal snooze never creates it |
| `systemState` | Required; unknown value fails fixture validation |
| `actions[]` | Unique `code`; must include enabled state and source |
| Disabled action | Requires `disabledReasonCode` and localized/display reason |
| Risk action | Confirmation/reason/evidence flags must match fixture expectation |
| `concurrency` | Required for any WorkItem/Trigger projection with an enabled inline action; otherwise optional; action-level copies prohibited |
| `blockedState` | May reference only visible effective `actions[]`; referenced actions exist, are disabled, and carry disabled reason |
| `workItemCapabilities` | Unique values; conditional data may not exist without matching capability |
| `actionDepth` | `deeplink` requires a non-empty source deep link |
| `lifecycleOwner` | Required when lifecycle provider differs from source provider |
| `businessContext` | Max 6 sections × 8 fields, max 2000 chars per text field, max 8 primary fields, sanitized links, no unauthorized raw value |
| `relatedRecords` | Maximum 20; populated data requires capability |
| `personal` | Must not contain provider business status/lifecycle fields |
| Approval/review | Must not carry operational `taskLifecycle` |
| Terminal task | Done/Cancelled resolves read-only and has no enabled state-changing action |
| Action placement | Primary may be null; all codes exist in `actions[]`; primary/secondary/overflow are unique and non-overlapping |
| Source navigation | Never appears as a duplicate command in `actions[]` |
| Authority ended | No state-changing action may remain enabled |
| Source unavailable/stale | Recovery behavior required; state-changing actions disabled |
| Fixture expectation | Resolver result must match expected surface/blocks/notices/actions |

### Explicit invalid combinations

- `taskLifecycle: Done` with `executionState: active`.
- Approval with operational task lifecycle.
- `ownershipState: unowned` with `admissionState: admitted`.
- `timerState: running` without `timeTracking`.
- Personal snooze changing normalized status/task lifecycle or creating `waitingContext`.
- `systemState: authorityEnded` with enabled decision/transition action.
- `version`/`etag` duplicated in source, concurrency, and action-level expected-version fields for the same fact.
- Two representations of the same fact such as `accepted: false` plus
  `admissionState: pendingAcceptance` in canonical fixtures.
- Provider action missing from `actions[]` but synthesized by the resolver.
- Provider example introducing a new canonical enum or using a provider-specific resolver.
- Enabled inline action without a projection-level concurrency token.
- Populated capability-backed data without its declared `workItemCapabilities` value.
- Trigger-only or raw migration fixture entering the Task Detail resolver directly.

## 13. Failure Path to Verify

| ID | Failure path | Expected UX |
|---|---|---|
| FP-01 | Fixture violates a canonical invariant | Development validation fails with fixture ID and rule; item is not silently rendered |
| FP-02 | Projection concurrency token is stale | Critical stale banner; state-changing actions disabled; Refresh is primary recovery |
| FP-03 | Source provider unavailable | Cached context may remain readonly; Retry/Open Source guidance shown |
| FP-04 | User authority ended | Actions unavailable; no permission inference in browser; clear authority notice |
| FP-05 | Hard dependency blocks start/complete | Provider action remains visible but disabled with accessible reason |
| FP-06 | Required checklist/evidence incomplete | Complete action disabled with exact reason; missing requirements highlighted |
| FP-07 | Action command is processing | `submittingActionCode` disables duplicate interaction without rewriting action projection |
| FP-08 | Provider rejects command/concurrency conflict | Inline recovery/error panel; refresh projection; no optimistic permanent state |
| FP-09 | Reconciliation remains unresolved | Persistent recovery notice may use `reconciliationRequired`; technical details remain secondary |
| FP-10 | Unknown item ID on Details route | Localized not-found state and back-to-list action |
| FP-11 | Capability data missing | Block is omitted or fixture validation fails according to requiredness; no JS crash |
| FP-12 | Deep-link item returns to WorkCenter | Projection refresh is represented; stale prior state is not trusted |
| FP-13 | Raw localization key missing | Test fails; raw key must not appear in UI |
| FP-14 | Legacy fixture enters canonical catalog | Validation/grouping test fails |

## 14. Authorization Convention

### This frontend-only draft

- No new permission key or AuthService seed.
- Browser is never authorization or action-eligibility authority.
- Browser consumes one aggregation/backend-resolved effective `actions[]` projection in the future.
- UI may add temporary interaction locks (`submittingActionCode`) but must not rewrite canonical action state.
- Mock permission-disabled fixtures represent the future projection only.

### Future command rule

Every future command must:

1. Authenticate the current actor server-side.
2. Resolve tenant context server-side; never accept authoritative TenantId from browser payload.
3. Re-evaluate native eligibility, effective permission, assignment, delegation, SoD, blocker, and lifecycle.
4. Validate the command envelope's projection concurrency token, idempotency, and command-specific rules.
5. Return the authoritative updated projection or a controlled conflict/error.

Concurrency is command safety, not permission. A stale `systemState` may cause the aggregation projection to
disable actions, but it must not be represented as a permission denial.

Any future permission keys require a separately approved backend/security scope. MOD-0024 must not seed or
grant MOD-0018 permissions implicitly.

## 15. Gateway / API Routing Decision

No gateway or API route change is required or authorized in this draft.

- WorkCenterNext remains mock-driven.
- Browser must not call ports `5056`, `5057`, or any service directly.
- A later production aggregation API requires a separately approved pack/slice.
- If that future route is not covered by an existing Gateway route, only `integration-agent` may change
  `ocelot.json`.
- Future tenant UI should use the approved tenant API profile through Gateway/same-origin infrastructure;
  token and TenantId authority remain server-side.

## 16. Acceptance Criteria

### Governance and boundaries

- [ ] MOD-0024 identity remains canonical and no new module ID is invented.
- [ ] Pack status remains `draft` until explicit user approval.
- [ ] Only the authorized WorkCenterNext frontend/mock scope is changed.
- [ ] Legacy `/WorkCenter` files are unchanged.
- [ ] MOD-0023 approval semantics are rendered only as provider projections and are not reimplemented.
- [ ] MOD-0028 structure/metadata, MOD-0029 controlled-document/version, and MOD-0031 evidence-link ownership
  remain distinct; WorkCenter stores none of them.
- [ ] BL-015 Split/Kanban/Calendar views remain unavailable.
- [ ] No backend, gateway, persistence, permission seed, or lookup key is introduced.

### Canonical contract

- [ ] Canonical fixtures use one fact per field; legacy `accepted`/`claimed` booleans are not canonical state.
- [ ] `normalizedStatus` and `taskLifecycle` are distinct.
- [ ] Only operational tasks use `taskLifecycle`.
- [ ] Approval/review/issue/exception use `taskLifecycle: notApplicable`.
- [ ] `executionState` does not repeat Waiting/PendingReview/Done.
- [ ] Stop Timer, Pause Work, and Wait for External Information are distinct scenarios/actions.
- [ ] Release/decline/delegate/reassign/dispute/return are events/dispositions, not active ownership states.
- [ ] Browser-facing projection contains one authoritative `actions[]` array.
- [ ] `actions[]` contains commands only; source/audit/document/related-record navigation stays outside it.
- [ ] Projection contains at most one canonical concurrency token and actions do not repeat it.
- [ ] Client submission state is separate from business projection.
- [ ] `workItemCapabilities[]` is used; the ambiguous technical field name `capabilities[]` is not canonical.
- [ ] Snooze changes no lifecycle/status/`waitingContext`; it remains a personal filter signal.

### Scenario catalog

- [ ] Canonical task fixtures cover direct-unaccepted, offered, queue-unclaimed, claimed-open, planned,
  in-progress/timer-inactive, in-progress/timer-running, execution-paused, waiting-information,
  dependency-blocked, completion-blocked, pending-review owner, review-returned/reopened, done, cancelled,
  inline, deep-link, and self-task.
- [ ] Approval fixtures cover simple decision, required reason, required evidence, information request,
  delegated decision, limit/SoD blocked, escalated, stale, and completed history.
- [ ] Review fixtures cover reviewer decision, owner pending-review projection, information request,
  approved, returned, and linked task/review.
- [ ] Review fixtures cover both optional and required review-meeting policies; required meeting keeps the
  decision action visible and disabled until an authoritative meeting reference exists.
- [ ] Issue/exception fixtures cover unclaimed, active investigation, external wait, resolution, reopen,
  policy block, acknowledgment, and source-only resolution.
- [ ] Edge fixtures cover stale, unavailable, authority-ended, processing, reconciliation, delegation,
  permission-denied, and plan/deadline conflict.
- [ ] Meeting invite exists only under trigger-only fixtures.
- [ ] Legacy PendingApproval/blocker/information-request shapes exist only under migration fixtures.
- [ ] Enterprise Strategy provider examples cover objective approval, objective periodic review, demand/idea
  review, and decomposition operational task; decomposition blocker and project governance deep-link are
  edge/provider examples.
- [ ] Enterprise Strategy examples use stable `providerCode`, never an invented `MOD-ESBP`, and one recurring
  review instance per period/process instance.
- [ ] Documentation provider examples cover a read-only controlled-document/version reference, an
  evidence-gated effective action, and safe source navigation without inferring approval from technical
  document version status.
- [ ] Every provider example uses the canonical validator and resolver and introduces no provider-specific enum.

### Resolver

- [ ] Resolver is pure/deterministic for the same projection and client interaction input.
- [ ] Resolver never synthesizes a provider business action.
- [ ] Terminal Done/Cancelled items resolve readonly.
- [ ] Trigger-only fixtures never enter the Task Detail resolver.
- [ ] Raw migration fixtures enter the canonical validator/resolver only after `adaptLegacyFixture()`.
- [ ] System safety resolves before actionable surfaces.
- [ ] Assignment/admission, intent, lifecycle, work-item capability, action depth, role/delegation, blocker, and action
  projection all influence the surface without becoming a new business state machine.
- [ ] Critical banners are limited to hard blocked, stale, unavailable, authority-ended, claimed-by-other,
  overdue, and persistent reconciliation states.
- [ ] Critical banner selection follows the locked precedence; it does not alter eligibility and does not
  duplicate the same condition as a notice.
- [ ] Waiting, pending review, snooze, due-soon, plan conflict, and non-blocking checklist issues use compact
  notices rather than critical banners.
- [ ] `actionDepth: deeplink` resolves a clear source navigation surface.
- [ ] Primary/secondary/overflow are non-overlapping action-code references and do not rewrite action content.
- [ ] Every fixture expectation matches the resolver output.

### Task Detail UX/UI

- [ ] `/WorkCenterNext/Details/{id}` is the canonical detail route.
- [ ] Header prioritizes type, title, native/display status, due/SLA, priority, assignee/role, critical
  signal, primary action, overflow actions, and Open in Source.
- [ ] Technical IDs and concurrency token metadata appear only in More Details/support metadata.
- [ ] Provider business context is bounded, allowlisted, sanitized, and omits unauthorized raw values.
- [ ] Type-specific decision context appears before secondary tabs/sections when needed for a safe decision.
- [ ] Conditional blocks render only for declared `workItemCapabilities`.
- [ ] Overview, Work, Process, Collaboration/Audit, Personal, and More Details responsibilities remain clear.
- [ ] Checklist shows required/blocking/evidence/disabled semantics where supplied.
- [ ] Subtasks respect `full` versus `readonly`.
- [ ] Dependencies remain readonly and show typed blocking context.
- [ ] Attachment and evidence semantics are not conflated.
- [ ] Waiting context shows who/what/since/expected-until where supplied.
- [ ] Closure, cancellation, return, and rejection summaries are visible in terminal/rework fixtures.
- [ ] Disabled action reason is visible/accessibility-readable and is not tooltip-only.
- [ ] Loading, empty, not-found, stale, unavailable, processing, permission, and command-error states exist.
- [ ] Confirmation/reason interactions use shared MOD-0013 premium wrappers.
- [ ] All labels resolve through WorkCenterNext localization; no new hardcoded user-facing strings.
- [ ] All seven tenant languages contain the same WorkCenterNext resource-key set.

### Non-regression

- [ ] List/Table/Focus navigation continues to open the standalone Details route.
- [ ] Split/Kanban/Calendar remain excluded.
- [ ] Existing WorkCenterNext personalization behavior remains functional.
- [ ] Existing user changes in dirty WorkCenterNext files are preserved/reconciled.
- [ ] No CSS selector outside the WorkCenterNext scope is changed.

## 17. Test Expectations

### Static/contract tests

- Validate fixture IDs, enum values, required fields, and cross-field invariants.
- Validate fixture group boundaries: canonical versus provider-example/edge/trigger-only/migration.
- Validate one authoritative `actions[]` representation.
- Validate `workItemCapabilities`/data consistency.
- Validate one projection-level concurrency token and reject action-level token duplication.
- Validate enabled inline actions require concurrency for both WorkItem and Trigger projections.
- Validate empty capability containers remain valid and populated undeclared capability data fails.
- Validate action placement code references and non-overlap.
- Validate bounded business context and safe navigation/reference links.
- Validate the 6-section, 8-field, 2000-character, 8-primary-field, and 20-related-record limits.
- Validate all fixture expectations against resolver output.
- Validate provider actions are never synthesized.
- Run Enterprise Strategy and documentation provider examples through the canonical validator and resolver.

### Resolver tests

- One test per canonical scenario family.
- Terminal readonly behavior.
- Admission/claim/offer surface behavior.
- System-safety recovery behavior.
- Normal notice versus critical-banner classification.
- Inline versus deep-link behavior.
- Terminal command-free behavior with separate source navigation.
- Trigger-only resolver isolation and trigger command concurrency.
- Migration adapter before canonical validation/resolution.
- Critical-banner precedence and no banner/notice duplication.
- Task/review linked-item behavior.
- Client submitting state does not mutate canonical action state.

### Localization tests

- Compare resource keys across `en, fr, es, zh, ar, ru, tr`.
- Fail on missing or empty values.
- Verify no raw WorkCenterNext key is rendered.

### Build and runtime

- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`
- Load `/WorkCenterNext` and `/WorkCenterNext/Details/{fixtureId}` through an authenticated tenant session.
- Check browser console for JavaScript errors.
- Exercise at least one fixture from every surface mode.
- Exercise desktop and narrow viewport Task Detail.
- Confirm `/WorkCenter` still behaves unchanged.

### Visual acceptance

- Header remains readable without exposing technical metadata as primary content.
- Critical banners are visually stronger than compact notices.
- Sticky actions do not obscure content on narrow screens.
- Overview/Work/Process/Collaboration sections do not render when irrelevant.
- RTL layout is checked with Arabic.

## 18. Ready-for-dev Checklist

- [x] AGENTS.md read.
- [x] Platform Shared Services domain config read.
- [x] Master Development Plan and Module ID Registry checked.
- [x] Delivery Board and Product Backlog checked.
- [x] DCP-002 identity preflight passed for MOD-0024.
- [x] `golden_reference: none` justified: non-CRUD aggregation/detail surface.
- [x] Frontmatter mandatory fields completed.
- [x] `_LayoutTenantShell` stated explicitly.
- [x] Ownership boundaries between MOD-0024, MOD-0023, source modules, MOD-0018, MOD-0021, and
  MOD-0028/MOD-0029/MOD-0030/MOD-0031 stated.
- [x] Repo scope and protected paths stated.
- [x] Canonical field vocabulary and validation rules stated.
- [x] Failure paths include fixture, security, stale, concurrency, source, and localization cases.
- [x] Authorization convention states browser is not authority.
- [x] Gateway decision is explicit.
- [x] Acceptance criteria are testable.
- [x] Test expectations include contract, resolver, localization, build, runtime, responsive, and RTL checks.
- [x] User reviewed the draft and confirmed the module pack is correct on 2026-07-24.
- [x] User approved the canonical scenario scope on 2026-07-24.
- [x] User approved the authorized implementation file scope on 2026-07-24.
- [x] Status changed to `ready-for-dev` by explicit user decision on 2026-07-24.

## 19. Implementation Notes

### Required implementation order

1. Preserve and inventory the dirty WorkCenterNext working tree.
2. Introduce canonical fixture vocabulary and invariant validator.
3. Add canonical, provider-example, edge, trigger-only, and migration fixtures.
4. Implement and test the work-item resolver, trigger-response resolver, and migration adapter as isolated
   pipelines.
5. Rewire standalone Task Detail rendering to resolver output.
6. Add/align seven-language resources.
7. Build and perform authenticated browser/visual verification.
8. Write the audit report.

### Resolver principle

The resolver consumes the effective projection; it does not recreate provider business logic:

```text
projection + client interaction state
                 ↓
         TaskDetailSurface
                 ↓
              renderer
```

Future effective actions are resolved before reaching the browser:

```text
provider native rules
        + effective permission
        + assignment/delegation
        + separation of duties
        + blocker/system safety
                    ↓
                 actions[]
```

Future command execution is a separate authoritative gate:

```text
authentication
        + authorization and eligibility re-check
        + current assignment/delegation
        + projection concurrency token
        + idempotency
        + command-specific validation
                    ↓
 authoritative result or controlled conflict
```

### UI hierarchy

- Sticky header: operational essentials only.
- Critical recovery banner: only action-changing exceptional states.
- Compact notices: normal waiting/review/snooze/risk states.
- Business context: enough information for safe decision.
- Detail sections: capability-driven.
- More Details: IDs/version/support metadata.
- Personal overlay: visually separate from provider data.

### Compatibility

Legacy fixture adapters may translate `accepted`, `claimed`, legacy status, and legacy DTO blocker shapes into
canonical fixtures for regression demonstration. Canonical fixtures must never emit those duplicate booleans.

## 20. Follow-up Items

The following are explicitly not authorized by this draft:

1. Production WC-1 aggregation backend and provider contract.
2. WC-2 working-time/calendar provider.
3. WC-3 position-aware assignee resolver.
4. WC-4 notification integration.
5. WC-5 provider registry/certification.
6. MOD-0024 MongoDB entities, repositories, commands, queries, and API.
7. Permission definitions/seeds/grants.
8. MOD-0023 production approval integration.
9. MOD-0028/MOD-0029 production documentation integration, document commands, or controlled-document workflow.
10. MOD-0031 production evidence upload/linking/completeness integration.
11. Enterprise Strategy production provider integration or changes to `Diten.EnterpriseStrategyService`.
12. MOD-0037 production reconciliation.
13. Split, Kanban, and Calendar views (BL-015; require separate approved scope).
14. Gantt, WBS editor, dependency graph editor, or source-specific complex forms.
15. Inline terminal reopen/restore or history mutation.
16. Meeting invitation product decision beyond trigger-only fixture treatment.
17. Replacement/swap of `/WorkCenter` with `/WorkCenterNext`.

Each follow-up requires its own approved module-pack slice or applicable Delivery Capability Pack before
production implementation. A mock provider example remains inside this draft; a production integration first
undergoes scope preflight and uses a module pack when ownership is single-module or a Delivery Capability Pack
when multiple domains/modules, Gateway/Auth, or ordered delivery are involved.
