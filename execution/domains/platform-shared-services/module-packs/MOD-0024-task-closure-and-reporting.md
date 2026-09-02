---
id: MOD-0024
name: Task & Checklist Engine
slice: Task Closure & Reporting (closure record, outcome, work report)
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: none
entity_base: TenantScopedEntity
status: draft
owner: ali.tufanoglu
branch: feature/pss/mod-0024-task-closure-reporting
started: 2026-09-02
target: TBD
form_field_count: 0
supersedes_slice: none — additive to MOD-0024-task-checklist-engine.md (contract) and MOD-0024-task-engine-create-runtime.md (runtime)
---

# MOD-0024 — Task Closure & Reporting

> **Draft scope:** this pack decides WHO writes a closure record, WHAT shape it has, and what a work report
> is allowed to say. It authorizes no code. No `.cs`, `.js`, `.cshtml`, `.css` or `.resx` file is touched by
> this slice, and no new user-visible string is introduced.
>
> **Measurement basis:** every field, enum and line reference below was read from the working tree on
> 2026-09-02. Corrections to earlier assumptions are recorded in §12 rather than quietly applied.

## 1. Module Summary

Closing a task and reporting on tasks are two different questions, and this repository has been treating
them as one. The engine already records that work ENDED — `CompletedAt`, `CancelledAt`,
`ClosureReasonCode` and a full `TaskTransition` log are all persisted today. It records almost nothing
about what the work PRODUCED, and the screen shows less than what is stored.

This pack separates the two objects that "task report" has been collapsing:

| | **A · Closure record** | **B · Work report (analytics)** |
|---|---|---|
| What it says | what this piece of work produced | how work is flowing |
| Scope | one task | every provider |
| Owner | `lifecycleOwner` — the module that owns the work | MOD-0024 |
| Written by | the owning module, at closure | derived, never authored |
| Optional | yes | not applicable |

The consequence is the point of this pack: **MOD-0024 does not own the closure record of work it does not
own.** It owns the envelope's shape, its display, and the report built on top of it.

### Delivery slice

| Slice | Included in this pack |
|---|---|
| Ownership decision: who writes a closure record | Yes — §2 |
| Closure envelope shape (contract-level) | Yes — §4 |
| Already-exists vs genuinely-missing measurement | Yes — §4 |
| Outcome model and the reason-required flag | Yes — §5 |
| Template-driven closure form decision | Yes — §6 |
| Field-by-field in/out decisions with reasons | Yes — §7 |
| Explicitly excluded designs, with reasons | Yes — §8 |
| Open decision left unanswered for the owner | Yes — §9 |
| Lifecycle gaps blocking the report | Yes — §10 |
| Phase 1–5 delivery slices | Listed only — §11, none authorized |
| Any implementation | No |
| Contract change to `fixture-contract.js` | No — Faz 2 |
| Backend entity, endpoint, migration | No — Faz 2 |
| Localization work | No — this pack adds no string |

## 2. Ownership and Boundaries

### The rule

A closure record belongs to whoever owns the work's lifecycle, not to whoever displays it. The contract
already names that party: `lifecycleOwner` is a declared, required field whenever it differs from the
source provider (`MOD-0024-task-checklist-engine.md` §4).

- A CRM task's closure record is written by CRM. MOD-0024 displays it.
- A document review's closure record belongs to Document Management (MOD-0028/MOD-0029).
- An approval decision's record belongs to MOD-0023. MOD-0024 reports it and hands it on; it never decides.
- **MOD-0024 writes a closure record only for work where `source.providerCode === 'tasks'`** — its own
  tasks and self-tasks. Everywhere else it is a reader.

### No module is obliged to write one

A closure envelope is an offer, not a gate. A provider that fills it gets a rich report; a provider that
does not gets a thin one — a lifecycle, a timestamp, a transition log. Nothing fails, nothing is blocked,
and no placeholder record is synthesized on a provider's behalf. A synthesized closure record would be a
confident zero: a report sentence with no author behind it.

### The meeting case

A meeting report is the MEETING's closure record. A task born out of a meeting produces its own, separate
closure record. The two are **linked and never merged**:

- The link is `relatedRecords[]` — read-only, already in the contract, and explicitly never an implicit
  blocker.
- Two closure records exist. Neither is a section of the other.
- The task's closure record must not be pre-filled from the meeting's, and the meeting's must not be
  rewritten when the task closes.

Merging them would make the task's owner the author of a record they did not write, which is exactly the
ownership inversion this section exists to prevent.

### MOD-0024 owns

- The SHAPE of the closure envelope: its fields, their types, their validation, their contract position.
- The RENDERING of a closure record supplied by any provider.
- The closure record of its own tasks (`providerCode: tasks`).
- The outcome vocabulary MECHANISM — where an outcome dictionary lives and how it is resolved (§5).
- The work report (B): cycle time, unattended, productivity as a count, waiting distribution (§11, Faz 5).

### The lifecycle owner owns

- Whether a closure record exists at all for its work.
- Its content: outcome, narrative, deliverables, follow-ups.
- The business meaning of its own outcome codes.
- Any approval or gate that governs closing its own work.

### MOD-0024 does not own

- Another module's closure record, or the authority to author one on its behalf.
- Approval decisions — MOD-0024's approval boundary reports and hands on, never decides
  (established in the review-boundary section of `MOD-0024-task-checklist-engine.md` §2; a local
  `if (ApprovalRequired)` branch is a regression, not a feature).
- Meeting scheduling, attendee availability or agenda — Calendar's, and the reason `followUpMeeting` is
  demoted to a request in §7.
- Evidence completeness (MOD-0031), retention (MOD-0030), controlled-document versioning (MOD-0029),
  audit system-of-record (MOD-0021).
- Timesheet, costing, or payroll semantics — see the open decision in §9.

### Source ownership examples

| Work item | Lifecycle owner | Who writes the closure record |
|---|---|---|
| MOD-0024 self-task | MOD-0024 | MOD-0024 |
| CRM follow-up task | CRM | CRM |
| Document review | MOD-0028 / MOD-0029 | Document Management |
| Approval work item | MOD-0023 | MOD-0023 |
| Meeting | Calendar / meeting provider | The meeting provider |
| Task arising from a meeting | Its own provider | Its own provider; linked via `relatedRecords` |

### Industry basis

SAP Task Center federates tasks from provider applications and, for anything beyond a simple decision,
deep-links back to the source system so the owning application renders the detail. The aggregator holds
the inbox; the provider holds the record. Our `lifecycleOwner` plus `actionDepth: deeplink` pair is the
same decision, arrived at from the same constraint.

Source: SAP Task Center documentation —
<https://help.sap.com/doc/ab1cc29fb9aa41889779ce4f699142cd/Cloud/en-US/TaskCenter_PUBLIC_EN_1.pdf>

## 3. Owned Objects

### Runtime objects

No new persisted runtime object is authorized by this pack. The closure envelope is expected to extend
the EXISTING `TaskItem` closure block (`TaskItem.cs:342-344`) rather than open a new collection: the
fields already there — `CompletedAt`, `CancelledAt`, `ClosureReasonCode` — are the same record, and
splitting a record across two stores to add three fields to it is how a closure story becomes two
half-stories.

`entity_base: TenantScopedEntity` records the posture only; `TaskItem` already carries it
(`TaskItem.cs:20`).

### Frontend contract objects

| Object | Purpose | Status |
|---|---|---|
| `closure` | Closure envelope on a work-item projection | Proposed — Faz 2 |
| `ClosureOutcome` | `{ code, label, requiresReason }` from the task type's dictionary | Proposed — Faz 3 |
| `ClosureDeliverable` | One produced artefact: text, rich text, or reference link | Proposed — Faz 2 |
| `ClosureFollowUp` | Reference to a task created as a next step | Proposed — Faz 2 |
| `TaskTransition` history block | Already persisted; not yet projected as a readable history | Faz 1 |

### Fixture groups

Closure fixtures extend the existing groups rather than opening a parallel tree; the canonical/edge-case
split already carries the cases this slice needs:

```text
canonical/
  task/          closed with a rich closure record
  approval/      closed by MOD-0023; MOD-0024 renders, does not author
  review/        returned once, then closed (needs the Faz 4 lifecycle)
edge-cases/
  closed-thin/           terminal, no closure record at all — must render cleanly
  closed-foreign/        lifecycleOwner ≠ tasks, closure record read-only
  closure-outcome-reason/ outcome carrying requiresReason
provider-examples/
  meeting-linked/        two closure records, related, not merged
```

The `closed-thin` fixture is the load-bearing one: it proves that an absent closure record is a normal
state and not a rendering failure.

## 4. Entity Fields

### The closure envelope

```text
closure: {
  outcomeCode          from the TASK TYPE's dictionary (§5)
  closureReasonCode    EXISTS
  completedAt          EXISTS
  effort { estimate, actual }   EXISTS — and now on screen
  note                 the closing narrative
  deliverables[]       'evidence' and 'attachments' capabilities already declared
  followUps[]          references to tasks created as next steps
}
```

### What already exists, and what the screen does with it

Measured 2026-09-02. This table is the reason this slice is small: most of the envelope is already stored.

| Field | Stored | Projected to the browser | Rendered | Gap |
|---|---|---|---|---|
| `ClosureReasonCode` | `TaskItem.cs:344` · mapped `TaskItemMapper.cs:110` · DTO `TaskModels.cs:835` | **No** — the WC-1 projection carries no closure reason | **No** — `closureReason` appears in **0** files under `frontend/` | Projection + render |
| `CompletedAt` | `TaskItem.cs:342` | Yes, as `closedAt` — `TaskWorkItemProvider.cs:711`, `WorkAggregationModels.cs:454` | Only as an SLA freeze input (`fixture-contract.js:406-422`); never shown as a closing date | Render |
| `CancelledAt` | `TaskItem.cs:343` | Folded into `closedAt` (`ClosedAt: terminal ? CompletedAt ?? CancelledAt : null`) | No | Render — and the fold loses WHICH of the two it was |
| `EstimateHours` | `TaskItem.cs:134` | Yes — `TaskWorkItemProvider.cs:722` | **Yes** — effort card, `app.js:4253-4267` | None |
| `SpentHours` | `TaskItem.cs:137` | Yes — `TaskWorkItemProvider.cs:729` | **Yes** — same card | None |
| `TaskTransition` log | `TaskSupportingEntities.cs:62-84` — `FromLifecycle`, `ToLifecycle`, `ActorUserId`, `ReasonCode`, `Reason`, field changes | Partially, as activity entries | As a feed | No closure-oriented history view |
| `outcomeCode` | — | — | — | **Missing entirely** |
| `note` (closing narrative) | — | — | — | **Missing entirely** |
| `followUps[]` | — | — | — | **Missing entirely** |
| `deliverables[]` | Capabilities `evidence`, `attachments` declared (`fixture-contract.js:175-176`) | Containers exist | Rendered as attachments/evidence | No closure-scoped deliverable concept |

Genuinely missing: **`outcomeCode`, `note`, `followUps[]`**. Everything else is a projection or a rendering
gap, not a data gap — which is why Faz 1 (§11) ships value without a single schema change.

### ⚠ The capability that gated a card nobody could draw

`taskContext` was ADDED to `CAPABILITIES` on 2026-08-24 (Tur B). Before that, the effort card had existed on
the detail page from the beginning and had **never once rendered**: `app.js:4253` gates it on
`hasCap(item, 'taskContext')`, the capability was not in the `CAPABILITIES` list at `fixture-contract.js:167`,
so no fixture could legally declare it and no provider emitted it. The data was being collected on the create
form and stored on `TaskItem` the whole time.

The failure was not a missing field. It was a renderer gating on a capability that the contract's vocabulary
did not contain — the gate and the vocabulary drifted, and nothing failed loudly.

**The rule this produces for the closure envelope:** a projection field and its capability enter the contract
in the SAME change. `closure` may not be added to a fixture, a provider, or a renderer until the capability
that gates it is in `CAPABILITIES` and in `DATA_CAPABILITIES`
(`fixture-contract.js:167-193`, whose asymmetric validation already refuses data without its capability).

Related and worth fixing when Faz 2 opens the contract: `MOD-0024-task-checklist-engine.md` §4 still lists
twelve capabilities and does not include `taskContext`. The pack is stale against the code.

### Where the closure stage discriminator goes

`TaskFieldDefinition` (`TaskSupportingEntities.cs:205-289`) already carries everything a closure form needs —
`Code`, label split, `ValueType`, `Section`, `Importance`, `IsRequired`, `SortOrder`, options source,
`AppliesToModuleCode`, and the BL-024 access pair. The create form consumes it live today:
`Tasks/api.js:246` → `GET /field-definitions` → `Tasks/form-page.js:542-554`, served by
`TasksController.cs:592`.

The single missing thing is WHEN a definition is asked. Proposed name and shape, for Faz 2:

```csharp
/// <summary>WHEN this definition is asked: on creation, at closure, or both.</summary>
public TaskFieldStage Stage { get; set; } = TaskFieldStage.Create;

public enum TaskFieldStage { Create = 0, Closure = 1, Both = 2 }
```

`Create` as the default is load-bearing: every definition written before the field existed is a create-form
field, and any other default would silently move existing tenant fields onto a closure form nobody designed.
This is the same defaulting argument `ViewPermission`/`EditPermission` already make on the same entity.

## 5. Outcome model

### Outcome is not status

`Done` is a state. It says the work stopped, not what it decided. An approval task that closes as `Done`
carries no record of whether it was approved or rejected — the decision is legible only by chasing MOD-0023.
`outcomeCode` is the field that answers "what was decided", beside the field that answers "did it finish".

### The dictionary belongs to the task type

Oracle BPM defines a human task's outcomes in the task DEFINITION (Approve/Reject, Resolved/Unresolved),
and those outcomes surface in the worklist as the selectable actions. The dictionary is per task kind, not
global — a "Reject" that means the same thing on a purchase approval and a deviation investigation is a
coincidence, not a shared vocabulary.

Source: Oracle BPM, Configuring Human Tasks —
<https://docs.oracle.com/en/middleware/bpm/12.2.1.3/bpm-develop/configuring-human-tasks.html>

`TaskType` (`TaskSupportingEntities.cs:552-616`) is the right home: it is tenant-unique, immutable by code,
already governs `RecordClass`, `GqmsDomain`, `FunctionCode` and the controlled-document layers, and is
already the thing a task is opened AS. It carries no outcome list today.

### ⭐ The reason-required flag lives on the outcome

`requiresReason` is a property of the OUTCOME, not a global "notes are mandatory" toggle.

- `Rejected` requires a reason. `Approved` does not.
- A global switch forces a reason onto outcomes that do not need one, and the field fills with "ok" —
  which destroys the reason data on the outcomes that DO need it.
- The contract already has the matching mechanism at the action level: `requiresReason` on `EffectiveAction`
  (`MOD-0024-task-checklist-engine.md` §4). This is the same flag, one level down, on the outcome.

### Rolling subtask outcomes up — a later phase, not this one

Oracle computes a parent outcome from child outcomes. We have `subtasks` and `dependencies` capabilities and
real dependency blocking (`TaskBlockingRules.cs`), so the raw material exists. It is **noted as a future
phase and is not in scope**: a roll-up rule needs an outcome dictionary to roll up first, and inventing the
aggregation before the vocabulary would fix the vocabulary's shape by accident.

## 6. The closure form is template-driven

Fifteen minutes of work and a three-month delivery do not fill in the same form. Force one form on both and
everyone types "done", and the report lies — a report of uniformly empty closure records is worse than no
report, because it looks like data.

The infrastructure is already in place; nothing new is invented:

| Piece | Where | Role at closure |
|---|---|---|
| `TaskFieldDefinition` | `TaskSupportingEntities.cs:205` | The closure fields themselves, once `Stage` exists (§4) |
| `TaskType` | `TaskSupportingEntities.cs:552` | Owns the outcome dictionary (§5) and `RecordClass` |
| `TaskTemplate` | `TaskSupportingEntities.cs:378` | Carries `DefaultFieldValues`; the natural place to bind a closure shape |
| `ChecklistTemplateItem.EvidenceRequired` | `TaskSupportingEntities.cs:325` | Already gates completion on evidence, per item |
| `ChecklistRunItem.EvidenceRequired` | `TaskSupportingEntities.cs:350` | The live instance of that gate |

`TaskRecordClass` (`TaskEnums.cs:315-328` — `NOT_A_RECORD`, `OPERATIONAL_RECORD`, `GXP_QUALITY_RECORD`)
already declares what kind of record work of a given type produces. It is the strongest existing signal for
how demanding a closure form should be, and a closure-form design that ignores it would be inventing a second
answer to a question the type already answers.

## 7. Field decisions

| Field | Decision | Reason |
|---|---|---|
| Closing narrative / note | **In — always** | The single most-read thing afterwards. Free text is weak analytically and strong humanly; that is the trade being made deliberately. |
| Deliverables (text / rich text / reference link) | **In — always, for work that produces output** | Highest downstream value in the envelope, and the `evidence` and `attachments` capabilities are already declared, so this costs contract shape rather than new machinery. |
| Next steps → **follow-up task** | **In** | Creates a living record instead of a sentence. A follow-up task is findable, assignable and closable; a line of text is none of those. |
| Next steps → **follow-up meeting** | **Demoted to a REQUEST** | Scheduling needs availability, attendees and an agenda — Calendar's job. MOD-0024 emits a request and links the result via `relatedRecords`; it does not reach into another module's domain. Consistent with the existing `scheduleReviewMeeting` treatment, where the browser never infers the action and the Calendar event stays Calendar-owned. |
| Key learnings | **In — template-dependent** | Valuable where it is real; noise when mandatory. `Stage` + template binding is exactly the mechanism that lets one task type ask and another not. |
| Challenges faced | **In — template-dependent, and STRUCTURED** | Nobody aggregates free text. The systemic value is in obstacle CODES, and `TaskTransition.ReasonCode` (`TaskSupportingEntities.cs:79`) already records them at each transition. Structured here means: pick a code, then optionally say more. |
| Key achievement | **Out** | It asks the same question as the closing note. Two fields for one answer means one gets filled and the other gets "-", and the report then has to guess which one was meant. |

## 8. Excluded by decision

### Efficiency % / "0x multiplier" indicator — excluded

Turning estimate-versus-actual into a personal score makes people inflate estimates, and the inflated
estimate then corrupts the only planning input the system has. The measure destroys the thing it measures.

Oracle's worklist report set is Unattended · Priority · Cycle Time · Productivity · Time Distribution — and
its PRODUCTIVITY report is the COUNT of tasks assigned versus completed in a period, not a percentage against
an estimate.

Source: <https://docs.oracle.com/html/E10224_15/bp_worklist.htm>

Variance between estimate and actual is a **plan-quality** signal, reported against task types and templates.
It is never reported against a person.

### Hard gates and soft advice must not share a list — enforced separately

A reference screen showed "Cannot finish: mandatory checklist" and "Identify at least one key achievement" in
the same list. That presentation makes a hard gate look optional and an optional prompt look mandatory. They
are two different concepts and the pack defines them as such:

| | **Gate** | **Advice** |
|---|---|---|
| Effect | Blocks completion | Blocks nothing |
| Origin | Data, not copy | Copy |
| Existing machinery | `TaskBlockingRules` (`StartActionCode` / `CompleteActionCode`), `blockedState`, `action.enabled: false` with `disabledReasonCode` → `WorkAggregation_ActionDisabled_*` (`app.js:2180`, `app.js:3155`) | None; none needed |
| Counted in the report | Yes | No |
| Rendering | Beside the disabled action it disables, naming that action | Inline near the field it concerns |

Three block-sources exist today and each blocks a specific act (`TaskBlockingRules.cs` header): a blocking
checklist item and an open subtask block COMPLETION; a dependency blocks according to its type. Closure
advice joins none of them.

## 9. ⚠ Open decision — owner's call, not assumed here

**Does session-based time tracking (tracked work sessions) enter scope?**

State of play: `EstimateHours` and `SpentHours` already exist, are projected, and are rendered
(`TaskItem.cs:134,137`; `TaskWorkItemProvider.cs:722-729`; `app.js:4253-4267`). The `timeTracking`
capability and a `timeEntries` container are already declared in the contract
(`fixture-contract.js:175,183`).

Adding start/stop session records opens the timesheet and costing door: an approval flow over submitted
time, a correction path, and a period lock. Session time either feeds a real timesheet — with all three of
those — or it is decoration. There is no useful middle.

**This pack does not choose.** No phase below assumes session tracking either way, and Faz 5 is specified so
that it works on `SpentHours` alone.

## 10. Lifecycle gaps

Measured state. Backend `TaskLifecycle` (`TaskEnums.cs:16-25`) has seven members:
`Open · Planned · InProgress · Waiting · PendingReview · Done · Cancelled`. The frontend contract
(`fixture-contract.js:9`) mirrors those seven and adds `notApplicable` for non-task work intents.

### RETURNED / REWORK — missing as a state

`PendingReview` exists. "The review sent it back, do it again" does not. Rework rate is the single most
valuable metric a work report can produce, and it currently cannot be computed.

The act itself is already recorded: `TaskTransitionKind.Returned` exists and maps to the wire code
`returned` (`TaskTransitionCodes.cs`), and `returned` is in `ACTIVITY_EVENT_CODES`
(`fixture-contract.js:148`). So the transition is logged and the word reaches the screen — there is simply no
lifecycle STATE for a returned task to sit in, which means a returned task is indistinguishable from a task
that was merely started again. The gap is narrower than "not modelled" and more precise: **the event exists,
the state does not.**

### BLOCKED ≠ WAITING

`WAITING_CONTEXT_TYPES` is `externalInformation · approval · review · meeting`
(`fixture-contract.js:29`). None of them is "blocked by a dependency" — yet dependency blocking is fully
implemented (`TaskBlockingRules.cs`, `TaskDependency` at `TaskSupportingEntities.cs:106`), and it surfaces
only as a disabled action.

Waiting is waiting on a PERSON or an event. Blocked is waiting on another TASK. They have different owners,
different resolutions and different report meanings; collapsing them makes "what is stuck, and on what"
unanswerable.

### ONHOLD / DEFERRED — missing

`TaskPersonalOverlay.SnoozedUntil` (`TaskSupportingEntities.cs:498`) is explicitly personal: the contract
states outright that it must not create a waiting state (`SNOOZE_MUST_NOT_CREATE_WAITING`) and that the
requester cannot tell the holder snoozed anything. That is the right design for snooze — and it means the
system cannot report "this work was parked for three weeks", because nothing at the task level ever recorded
that it was.

### Closure impact

Each gap costs the report a specific sentence:

| Gap | Sentence the report cannot say |
|---|---|
| No `Returned` state | "18% of reviews came back at least once." |
| Blocked folded into Waiting | "Half our waiting time is waiting on other tasks, not on people." |
| No `OnHold` | "This was deliberately parked for three weeks." |

## 11. Delivery slices — listed, not authorized

Scope of this pack is **Faz 0: the pack itself**. Nothing below is approved by writing it down.

### ⚠ The order changed, because Faz 1 was already done

This table originally opened with "put on screen what is already stored". Measured again on 2026-09-02, that
phase had almost nothing left in it — and what remained was not a display gap:

| Faz 1's original items | Measured state |
|---|---|
| Effort card (estimate / spent) | **Already drawn** — `app.js:4253-4267`, fixed 2026-08-24 (Tur B) |
| `closedAt` on screen | **Already drawn** — the step-bar caption, `app.js:2683` (`StepClosedOn`) |
| Transition history | **Already projected and drawn** — `TaskWorkItemProvider.ToActivity`, with from→to, actor, reason text and field changes per row |
| `closureReasonCode` on screen | **Nothing to draw.** The column was EMPTY |

The last row is why the order is wrong rather than merely optimistic. `TRANSITION_BODIES.__default` in `app.js`
sent `reasonCode: null` as a literal constant, for `complete`, `cancel` and every transition not named
explicitly. The engine faithfully wrote `task.ClosureReasonCode = command.Request.ReasonCode` — and stored null,
every time, on every closure since the engine shipped.

**So projecting the field first would have delivered an empty column to the screen.** The vocabulary has to
exist before the field is worth showing: Faz 3 comes first, and Faz 1's remaining item rides along with it.

| Phase | Content | Schema change | Status |
|---|---|---|---|
| **Faz 3 → now** | Outcome dictionary on `TaskType`, `requiresReason` on each outcome, the picker on complete/cancel, the `reasonCode: null` constant removed, `ClosureReasonCode` projected and rendered, `TaskTransition.ReasonCode` on the activity event | Yes, additive | **Delivered 2026-09-02** |
| **Faz 1 (remainder)** | Distinguish the closing date from the cancellation date on screen — see the `closedAt` note below; measured as SAFE, so this is presentation, not correctness | None | Optional |
| **Faz 2** | Closure envelope proper: `note`, `deliverables[]`, `followUps[]`, and `TaskFieldDefinition.Stage` | Yes, additive | Next |
| **Faz 4** | Lifecycle gaps: `Returned` as a state (the event already exists), `Blocked` separated from `Waiting` | Yes — enum change, migration-sensitive | After Faz 2 |
| **Faz 5** | Work report: Cycle Time · Unattended · Productivity **as a count** · waiting distribution | Read-only | After Faz 4 |

Faz 4 is the one with real regression risk: `TaskLifecycle` is a persisted enum on a document store, and this
module has already taken a live 500 from exactly that shape of change — a stale `"QA"` value that no enum
member could represent brought the task-type list down on deserialization (measured 2026-08-26,
`TaskSupportingEntities.cs:576-586`, the `FunctionCode` commentary). Any lifecycle addition must be able to READ every
value already written.

## 12. Measurement log

Verified in the working tree on 2026-09-02. References given in the source brief that turned out to be
imprecise are corrected here rather than repeated.

**Confirmed exactly as stated**

- `ClosureReasonCode` — `TaskItem.cs:344`, `TaskItemMapper.cs:110`, `TaskModels.cs:835`.
- `CompletedAt` — `TaskItem.cs:342`. `EstimateHours` — `:134`. `SpentHours` — `:137`.
- `closureReason` appears in **0** files under `frontend/`.
- `TaskTransition` carries `FromLifecycle`, `ToLifecycle`, `ActorUserId`, `ReasonCode`
  (`TaskSupportingEntities.cs:62-84`), plus `Reason` and an embedded field-change list.
- `timeTracking`, `evidence`, `attachments`, `processStages`, `relatedRecords` are all declared
  (`fixture-contract.js:175-176`).
- `TASK_LIFECYCLES` is the eight-value frontend list at `fixture-contract.js:9`.
- `returned` is present in the contract but has no lifecycle state (`fixture-contract.js:148`).
- `TaskFieldDefinition` has no stage discriminator; the create form consumes `/field-definitions` live
  (`Tasks/api.js:246`, `form-page.js:542`, `TasksController.cs:592`).
- `TaskType` carries no outcome dictionary.

**Corrected**

1. **The effort card renders.** It was fixed on 2026-08-24 (Tur B): `taskContext` was added to
   `CAPABILITIES` (`fixture-contract.js:175`) and to `DATA_CAPABILITIES` (`:182`), and the provider now
   emits it when figures exist (`TaskWorkItemProvider.cs:886-892`). `app.js:4253-4267` draws it. Faz 1 is
   correspondingly smaller.
2. **The `taskContext` failure ran the other way.** The capability was in the RENDERER's gate from the
   beginning and missing from the contract's vocabulary and the projection — not "in the projection for
   months". Source: the comment at `fixture-contract.js:167-174` and `app.js:4243`.
3. **`CompletedAt` does reach the browser**, as `closedAt` (`TaskWorkItemProvider.cs:711`,
   `WorkAggregationModels.cs:454`), where it freezes the SLA clock (`fixture-contract.js:406-422`). It is
   never displayed as a closing date, and it is folded together with `CancelledAt`, losing which of the two
   it was. `ClosureReasonCode` reaches the detail DTO but **not** the WC-1 projection — so the reason gap is
   a projection gap, one layer earlier than the render gap.
4. **`EvidenceRequired` is on the checklist ITEM, not the template**: `ChecklistTemplateItem` line 325 and
   `ChecklistRunItem` line 350 of `TaskSupportingEntities.cs`. `ChecklistTemplate` (`:291`) has no such flag.
5. **`returned` is stronger than "mentioned in the contract"**: `TaskTransitionKind.Returned` exists and has
   a wire code (`TaskTransitionCodes.cs`). The event is recorded backend-side; only the STATE is missing.
6. **Backend `TaskLifecycle` has seven members, not eight** (`TaskEnums.cs:16-25`). `notApplicable` is a
   frontend-contract value for non-task work intents and has no backend counterpart.

**Re-measured 2026-09-02, when the vocabulary slice was built**

7. **The empty column had ONE cause, and it was in the browser.** `TRANSITION_BODIES.__default`
   (`app.js`) read `({ expectedVersion, reason }) => ({ expectedVersion, reasonCode: null, note: reason || null })`.
   Every other link in the chain already worked: `TaskTransitionRequest(ExpectedVersion, ReasonCode, Note)`
   accepts a code, `TaskWorkItemActionDispatcher.cs:72` forwards it, and
   `TaskItemTransitionHandlers.cs:532,536` writes it to the task and to the transition log. Field-name agreement
   was green in `task-transition-contract.test.js` throughout — the guard compared NAMES, and the defect was a
   VALUE.
8. **`closureReasonCode` never reached the projection either.** Zero occurrences in `TaskWorkItemProvider.cs`
   before this slice, confirming §12 item 3: the gap was one layer earlier than the render.
9. **`TaskTransition.ReasonCode` was recorded and never projected.** `ToActivity` emitted `Reason` (the actor's
   free text) and not `ReasonCode`, so a feed row could show the sentence explaining a classification while
   dropping the classification.
10. **The `closedAt` fold is SAFE — measured, not assumed.** `ClosedAt: terminal ? CompletedAt ?? CancelledAt`
    (`TaskWorkItemProvider.cs:711`) can never pick the wrong one: `TaskLifecycleService.CanTransition` refuses
    every transition out of a terminal state (`IsTerminal` covers `Done` and `Cancelled`), and the subtask
    cascade skips children already `Done` or `Cancelled`. Those are the only writers of the two fields, so no
    task can ever carry both. What the fold loses is which of the two it was — and that was never `closedAt`'s
    to carry: the lifecycle says it, and the closure outcome now says what was decided as well. **No change
    made; presentation only.**
11. **A defect this slice would have activated, found and fixed.** `CancelOpenSubtasksAsync` copied the parent's
    `ReasonCode` onto every child it cancelled. Harmless while the value was always null; with a real outcome
    flowing, the code comes from the PARENT's type dictionary and a subtask may be a different type or none, so
    the child would have stored a code its own type does not offer and printed it raw forever. The copy is
    removed; the child is still recorded as cancelled in its own feed.

**Deferred by this slice, and worth naming**

- **No task-type editor UI for the dictionary.** The field round-trips through the existing task-type
  create/update API, and `UpdateTaskTypeRequest.ClosureOutcomes` is nullable precisely so the current editor —
  which does not draw it — cannot delete a configured dictionary on save. Until an editor exists, a dictionary
  is configured through the API.
- **`closure` is a plain projection field, not a capability-gated block.** §4's rule ("a projection field and
  its capability enter the contract together") governs render BLOCKS; `closure` is a caption fact beside
  `closedAt`, which is not capability-gated either. It is declared in `fixture-contract.js` all the same, per
  BL-032.

**Measured but deliberately left out of the pack**

- `TaskGqmsDomain`, `FunctionCode`, `GroupDocuments` / `LocalDocuments` on `TaskType` — a rich governance
  surface, but it answers which documents govern a task type, not what a closure record contains. Naming it
  here would invite a closure form that reaches into DCP-005's controlled-document layer.
- `TaskFieldDefinition.ViewPermission` / `EditPermission` (BL-024 Phase 2) — they will apply to closure
  fields automatically once `Stage` exists, precisely because the closure form reuses the same definition
  entity. No separate closure-permission design is needed, so none is proposed.
- `TaskRecurrenceRule` — recurrence changes what "closed" means for a series versus an occurrence. Real, and
  a separate question from who authors a closure record.
- `TaskWatcher` — closure notification targets. Faz 5 territory at the earliest.
