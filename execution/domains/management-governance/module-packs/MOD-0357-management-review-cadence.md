---
id: MOD-0357
name: Management Review & Cadence
domain: management-governance
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: TenantScopedEntity
status: ready-for-dev
status_changed: draft -> ready-for-dev on 2026-09-11 (owner answered the five open questions; see §22)
owner: ali.tufanoglu
branch: feature/mg/mod-0357-management-review-cadence
started: 2026-09-11
target: TBD
form_field_count: 11
---

# MOD-0357 — Management Review & Cadence

> **Product name: "Toplantılar" ("Meetings").** `MOD-0357 Management Review & Cadence` is the Blueprint 8.1
> canonical identity (`execution/registries/module-id-registry.md:160`) and the name this pack's frontmatter
> and every runtime literal use. "Toplantılar" is the tenant-facing product name only — it never appears as
> a `MOD-` identifier, a permission namespace segment, or a collection prefix.

> **Authority.** This pack is `status: draft` and grants **no** code-start authority. `verify_module_id.py
> --check-id MOD-0357 --name "Management Review & Cadence"` returned `OK` on 2026-09-11 (Blueprint W-5,
> pulled forward by owner decision — see ADR-003 §2). No new identity or `CAND-CAP` is opened by this pack.
> Everything this pack decides is bound by, and must not contradict,
> [`docs/records/decisions/2026-09/ADR-003-meetings-are-mod-0357-now-and-live-in-platform.md`](../../../../docs/records/decisions/2026-09/ADR-003-meetings-are-mod-0357-now-and-live-in-platform.md).
> Where this pack is more specific than the ADR (field lists, entity shapes, failure codes), the ADR's
> *decisions* still govern; this pack only fills in the implementation detail the ADR deliberately left open.

---

## 1. Module Summary

MOD-0357 is the system of record for **meetings**: the meeting record itself (type, time, place/link,
organizer, attendees, agenda), invitations and responses, the minutes that close a meeting, and the
recurring/continuation relationship between one meeting and its follow-up. It is **not** a second task
engine and **not** a second approval engine — decisions live with MOD-0007 (not yet built; recorded as a
plain line inside the minutes until it exists), tasks live with MOD-0024, governance bodies live with
MOD-0359 (not yet built; a meeting *type* setting stands in for a governing body until it exists).

The reason this module exists at all, in the owner's own words, is a two-way bridge between meetings and
tasks that nothing in the repo builds today:

- **Meeting → task**, three moments: *before* a meeting (preparation work), *during* a meeting (an
  action item captured on the spot), and *while writing the minutes* (a decision produces an action). All
  three are **ordinary MOD-0024 tasks** — same creation path, same assignment-scope rule
  (`01bc0915`/`ITaskAssignmentGuard`), same permissions. MOD-0357 never opens a second task type or a second
  assignment rule for these; the pack that would try to is the thing ADR-003 §"Neden" calls out by name
  ("Görevi toplantının içine koyan yok").
- **Task → meeting**: a task type may declare it needs a review meeting before it can be signed off
  (`reviewMeetingPolicy.required`, already modeled at the WC-1 contract level — see §7). MOD-0357 is what
  answers that requirement: `scheduleReviewMeeting` opens a MOD-0357 meeting linked back to the task.

Target user: any tenant user who organizes or attends recurring or ad-hoc review meetings — management
review, quality review, project/portfolio cadence, or a plain team meeting — and needs the outcome of one
meeting to become traceable work before the next one.

---

## 2. Ownership and Boundaries

### MOD-0357 owns

- The meeting record: type, schedule, place/link, organizer, attendee list, agenda items.
- Invitation and response state for each attendee (accept / decline / open on the calendar).
- The **minutes** — the meeting's own closure record (attendance actually taken, decisions as their own
  rows, action references) — draft → published, with corrections as a new dated version.
- The **link record** between a meeting and a MOD-0024 task (see §3 `RecordLink`) — its own collection,
  many-to-many, bidirectional by query.
- The meeting-type **setting** (agenda template, default task type for actions raised in this type of
  meeting, whether minutes are a quality record, whether attendance is mandatory).
- Its own `IWorkItemProvider` binding into the Task Center (the fourth provider, alongside MOD-0023,
  MOD-0024 and — per ADR-001 §3 — whichever else follows), surfacing "response pending" invitations only
  (see §3 `MeetingAttendee`, §16 AC-K5).

### MOD-0357 does NOT own (consume, never re-implement)

| Concern | Owner | Until then |
|---|---|---|
| Task lifecycle, assignment scope, closure record of a task | **MOD-0024** | N/A — MOD-0024 exists today; consumed as-is via the ordinary create path |
| Decision record as a first-class object | **MOD-0007** (not built) | A decision is a plain row inside the minutes (§3 `Decision`); no separate entity, no forward reference to an ID that does not exist |
| Governance body / charter / membership | **MOD-0359** (not built) | The meeting-type setting's "governing body" field is a free-text/lookup label, not a foreign key |
| Approval workflow for publishing minutes | **MOD-0023** | Not used in this slice — publish/lock is MOD-0357's own state transition, no workflow instance opened (ADR-003 §2, "ilk aşamada tutanak onayı iş akışsız") |
| Calendar scheduling, attendee availability, agenda authoring UX beyond this module's own agenda list | **Calendar / CAND-CAP-0010** | Consumed for the working-time seam only; MOD-0357 does not build a calendar |
| Evidence linking, e-signature | **MOD-0031 / Document Management** | Deferred (§20) — DM's e-signature signs a field summary, not a minutes *document*; the minutes text itself is out of that seam's reach today |
| File/attachment storage | *(no provider exists)* | Deferred (§20) |
| Google Calendar/Meet | *(not built)* | Deferred to Phase 2 (§20) |
| Work aggregation / personal overlay / effective-action rendering | **CAND-CAP-0006 / WC-1** (`Features/WorkAggregation`) | Consumed, never modified beyond one DI registration line |

### Ownership boundary this pack states because ADR-003 §3 requires it

MOD-0357 lives in `Diten.Platform` as a **documented deviation** (no `Diten.ManagementGovernanceService`
exists; DCP-006 OD-04 integration gaps are open). Portability is this pack's own contract:

- Its own Mongo collections, prefixed `meeting_*` — never shares a collection with MOD-0024 or any other module.
- Its own permission family, `platform.meetings.*` — never reuses `platform.tasks.*` or any task permission.
- **`TaskItem` gains no field.** The meeting↔task relationship is the `RecordLink` collection only (§3), read
  by MOD-0024 for `relatedRecords` and by MOD-0357 for its own "linked tasks" list — it is never a foreign key
  embedded on either aggregate.
- If MOD-0357 is ever extracted into its own service, the extraction unit is: `meeting_*` collections +
  `platform.meetings.*` permissions + `RecordLink` + this pack's own Features folder. Nothing else in
  `Diten.Platform` needs to change for that extraction to be possible — this is the ADR-003 §3 promise, made
  concrete.

---

## 3. Owned Objects

### Runtime entities (all `TenantScopedEntity`, Mongo, soft delete, tenant-first indexes)

| Entity | Collection | Purpose |
|---|---|---|
| `Meeting` | `meeting_meetings` | The meeting aggregate: type, schedule, place/link, organizer, description, `FollowUpOfMeetingId` |
| `MeetingAttendee` | `meeting_attendees` | One row per invited person: response state (invited/accepted/declined), attended/absent/excused (written only at minutes time) |
| `AgendaItem` | `meeting_agenda_items` | One ordered agenda line; may carry a `RecordLink` (see below) when it is "prepared by a task" or "raised from a carried-over action" |
| ~~`MeetingMinutes`~~ | ~~`meeting_minutes`~~ | **DROPPED (CT, 2026-09-12, S6 delivery):** no parent row is written. The current minutes ARE the latest `MeetingMinutesVersion` for the meeting; a parent carrying nothing but a meeting id would be a second collection with no owner. Everything §"Minutes as a versioned document" describes lives on the version rows. |
| `MeetingMinutesVersion` | `meeting_minutes_versions` | Append-only: draft, published, and every dated correction — never overwritten in place |
| `Decision` | *(embedded in `MeetingMinutesVersion`)* | A decision row inside a minutes version — **not** a standalone collection; see §"Decisions are not MOD-0007" |
| `MeetingType` | `meeting_types` | Tenant setting: agenda template, default action task type, `IsQualityRecord`, `RequiresESignature` (QA-set, default false), `AttendanceMandatory` |
| `RecordLink` | `meeting_record_links` | The bridge — see next subsection |

#### `RecordLink` — the one bridge, in one collection

```
RecordLink
  Id, TenantId
  SourceModuleCode: "meetings" | "tasks" | ...      (open string, not an enum — MOD-0007 joins later without a schema change)
  SourceRecordId:  Guid                              (the Meeting.Id or TaskItem.Id on the "from" side)
  TargetModuleCode: string
  TargetRecordId:  Guid
  LinkType: "preparation" | "agenda" | "bornFromMeeting" | "reviewMeeting"
  CreatedAtUtc, CreatedByUserId
```

- **Many-to-many, bidirectional by query** (query by either `SourceRecordId` or `TargetRecordId`, not a
  single-direction embedded list) — ADR-003 §"Sonuçlar".
- **MOD-0357 is the first owner and the only writer in this slice.** MOD-0024 reads it (batched, the same
  discipline every other `relatedRecords` read in the Work-Item contract already follows —
  `MOD-0024-task-checklist-engine.md` §"Effective action shape" — `relatedRecords` is read-only,
  never an implicit blocker) but never writes a row. If a second consumer needs to write links later, this
  collection is promoted to a shared contract — not duplicated (ADR-003 §"Sonuçlar", last line).
- **Never an implicit blocker.** A task linked to a meeting is not gated by the meeting's state and vice
  versa; the one documented exception is the task TYPE's own `reviewMeetingPolicy.required` flag (§7, K3),
  which is a MOD-0024 rule reading this link, not a rule MOD-0357 enforces.

#### Minutes as a versioned document

`MeetingMinutes` holds the STABLE identity (`MeetingId`, `TenantId`, `CurrentVersionNumber`,
`Status: Draft|Published`). `MeetingMinutesVersion` is append-only: a draft is edited in place (same version
row) until published; publishing freezes it (`ExpectedVersion`-conditional, same optimistic-concurrency
discipline every other MOD-0024-adjacent write uses); a correction after publish creates a **new** version
row with a mandatory `CorrectionReason`, and the **prior published version is never deleted or overwritten**
(K4). A minutes version carries:

```
MeetingMinutesVersion
  Id, TenantId, MeetingId, VersionNumber, Status: Draft|Published
  Attendance: [{ AttendeeUserId, Status: Present|Absent|Excused }]
  Decisions: [{ Code (server-minted, e.g. "D-3"), Text, DecidedByUserId? }]   (see next subsection)
  ActionReferences: [RecordLink.Id]              (decisions/agenda items that produced a task — read via RecordLink, not duplicated)
  PublishedAtUtc?, PublishedByUserId?
  CorrectionOfVersionNumber?, CorrectionReason?
  CreatedAtUtc, CreatedByUserId, UpdatedAtUtc?, UpdatedByUserId?
```

#### Decisions are not MOD-0007

A `Decision` is a **plain embedded row** inside a `MeetingMinutesVersion` — server-minted `Code` (`D-1`,
`D-2`, …, scoped to that minutes version), free text, optional decider. It is explicitly **not** a task
(K4 — "kararlar, görev DEĞİL, ayrı satır") and explicitly **not** yet a MOD-0007 aggregate: no
`DecisionId`, no forward reference to an entity that does not exist. When MOD-0007 ships, promoting this
embedded row to a real cross-reference is an additive migration this pack records as a known future
step (§20) — not something this pack builds a placeholder for today.

### CQRS (per Golden Reference Compact convention, one Feature folder per aggregate root)

Commands (Meeting): `CreateMeeting`, `UpdateMeeting`, `DeleteMeeting`, `BulkDeleteMeeting`, `CancelMeeting`,
`RescheduleMeeting`, `RespondToInvitation` (accept/decline), `ScheduleFollowUpMeeting`.
Commands (Minutes): `SaveMinutesDraft`, `PublishMinutes`, `CorrectPublishedMinutes`.
Commands (bridge): `CreateTaskFromMeeting` (delegates to `CreateTaskItemCommand` — see §7), `LinkExistingTask`,
`ScheduleReviewMeetingForTask` (the receiving side of MOD-0024's `scheduleReviewMeeting` action — see §7).
Commands (MeetingType): `CreateMeetingType`, `UpdateMeetingType`, `DeleteMeetingType`.
Queries: `GetMeetingList` (upcoming/past + filters — see §9 DataTable), `GetMeetingById`,
`GetMeetingMinutes`, `GetLinkedTasks`, `GetMeetingTypeList`, `GetMeetingTypeById`.

### Providers / services

| Object | Purpose |
|---|---|
| `MeetingWorkItemProvider : IWorkItemProvider` | Projects a pending invitation into a Task Center row (§16 AC-K5) — never the meeting's full content |
| `IRecordLinkService` | The one place that reads/writes `RecordLink`; MOD-0024's `relatedRecords` read and MOD-0357's own "linked tasks" list both call it, so the query shape cannot drift between the two consumers |
| `IMeetingIdempotencyKeyResolver` | `ResolveIdempotencyKey` pattern (already used by BRD reference-data ingestion) applied to meeting creation and meeting→task creation — K11 |
| `MeetingManifestProvider : IModuleManifestProvider` | Pages, permissions, `Nav.*` l10n keys — see §8 |
| `IMeetingInviteMailer` | Wraps `INotificationEventDispatchAdapter` for invite/change/cancel emails; the `.ics` attachment is a **known gap**, not built in this slice — see §7 dependency line and §20 |

### API (`api/v1/meetings`)

`POST /` · `GET /` · `GET /{id}` · `PUT /{id}` · `DELETE /{id}` · `POST /bulk-delete` ·
`POST /{id}/cancel` · `POST /{id}/reschedule` · `POST /{id}/respond` ·
`POST /{id}/follow-up` (schedule a continuation meeting) ·
`GET /{id}/minutes` · `PUT /{id}/minutes/draft` · `POST /{id}/minutes/publish` ·
`POST /{id}/minutes/correct` ·
`POST /{id}/tasks` (create-and-link, delegates to MOD-0024) · `POST /{id}/tasks/{taskId}/link` (link existing) ·
`GET /{id}/tasks` (linked tasks, via `RecordLink`) ·
`POST /tasks/{taskId}/schedule-review-meeting` (the receiving side of `scheduleReviewMeeting`) ·
`GET/POST/PUT/DELETE /types` (meeting type settings).

`api/meetings` does not exist anywhere in the repo today — no legacy collision to avoid, unlike MOD-0024's
`api/tasks` situation.

---

## 4. Entity Fields

### `Meeting` (core)

| Field | Type | Required | Rule |
|---|---|---|---|
| `Title` | string | Yes | trim, max 200 |
| `MeetingTypeId` | Guid | Yes | FK → `MeetingType`, tenant-scoped |
| `StartAt` | DateTimeOffset | Yes | |
| `EndAt` | DateTimeOffset | Yes | must be `> StartAt` |
| `Location` | string? | No | free text — physical place or a link; no format enforced (Calendar's job to structure it, not this slice's — §2) |
| `OrganizerUserId` | Guid | Yes | defaults to the creating user; may be reassigned before the meeting starts (mirrors MOD-0024's requester/holder split — the organizer is this record's "requester") |
| `Description` | string? | No | max 4000 |
| `FollowUpOfMeetingId` | Guid? | No | FK → another `Meeting` in the same tenant; null for a first-of-its-kind meeting (K6) |
| `Lifecycle` | enum | System | `Scheduled \| Cancelled \| Completed` — `Completed` is set when minutes publish, never chosen by the user directly |
| `IdempotencyKey` | string | System | server-computed on create, per `IMeetingIdempotencyKeyResolver` — K11 |

### `MeetingAttendee`

| Field | Type | Required | Rule |
|---|---|---|---|
| `MeetingId` | Guid | Yes | FK → `Meeting` |
| `UserId` | Guid | Yes | tenant-scoped user reference (same directory seam MOD-0024's assignee lookup already uses — no second user-resolution path) |
| `InvitationResponse` | enum | Yes | `Pending \| Accepted \| Declined` |
| `AttendanceStatus` | enum? | Conditional | `Present \| Absent \| Excused` — **null until minutes are drafted**; not set at invitation time |

### `AgendaItem`

| Field | Type | Required | Rule |
|---|---|---|---|
| `MeetingId` | Guid | Yes | |
| `Text` | string | Yes | max 500 |
| `SortOrder` | int | Yes | server-assigned; a continuation meeting's carried-over open actions are inserted at the FRONT (K6) |
| `RecordLinkId` | Guid? | No | set when this agenda line IS a prepared-in-advance task or a carried-over open action, never for a plain typed line |

### `MeetingType`

| Field | Type | Required | Rule |
|---|---|---|---|
| `Name` | string | Yes | trim, max 200, tenant-unique |
| `AgendaTemplate` | List\<string\> | No | ≤ 20 lines, each ≤ 200 chars — pre-fills a new meeting's agenda, editable per instance |
| `DefaultActionTaskTypeId` | Guid? | No | FK → MOD-0024 `TaskType` — the type a meeting-born action defaults to |
| `IsQualityRecord` | bool | No | default `false`; **QA sets this explicitly** — this pack never defaults a type to `true` on the QA's behalf (open question, §21) |
| `RequiresESignature` | bool | No | default `false`; recorded as a field only in this slice — the signing mechanism itself is deferred (§20) |
| `AttendanceMandatory` | bool | No | default `false` |

`TaskFieldValue`-style embedded classification fields are **not** carried on `Meeting` — MOD-0024's own
`Classification`/`AccessState`/`Redacted` triple exists because field-level authorization (BL-024) already
applies to configurable task fields; nothing in this pack's scope asks MOD-0357 to open that same surface,
so it is not pre-built here.

### MOD-0024 consumption (read-only; verified against the actual code)

- A meeting-born task is created through `CreateTaskItemCommand`, exactly the path any other create uses
  (`CreateTaskItemHandler.cs:26`). It goes through `TaskAssignmentIntentRules.Validate` and
  `ITaskAssignmentGuard.CheckTargetAsync` unchanged (`CreateTaskItemHandler.cs:125-178`, fixed by `01bc0915`) —
  MOD-0357 supplies `Title`/`Description`/`AssigneeUserId`/`DueAt` from the agenda line or the decision row and
  nothing else; it never bypasses the guard and never opens a second create path (K2).
- The closure record of that task is **MOD-0024's own** (`TaskItem.ClosureReasonCode` etc.) — MOD-0357 never
  pre-fills it from the meeting and never reads it back into the minutes (`MOD-0024-task-closure-and-reporting.md`
  §"The meeting case" — the two closure records are linked, never merged).
- `TaskType.ReviewMeetingPolicy` (verified in the WC-1 contract shape, `MOD-0024-task-checklist-engine.md`
  §"Review meeting policy": `{ requirement: notAllowed|optional|required, meetingId?, scheduledAt? }`) is
  **read**, never redefined, by `ScheduleReviewMeetingForTask` — MOD-0357 is the module that FULFILLS a
  `required` policy by creating the meeting and writing the `RecordLink` (`LinkType: "reviewMeeting"`) back;
  it never decides what the policy value should be for a given task type (K3).

---

## 5. Repo Scope

This is the ADR-003 §3 documented deviation exercised concretely: MOD-0357 is NOT under
`execution/domains/management-governance/**` for its runtime scope (that domain's own `Repo scope` line
explicitly protects `services/**` and `frontend/**` until a real MG service exists — see
`domain-config.md` §"Repo scope"/"Protected paths"). This pack is the explicit authorization this domain's
own config says is required before that boundary may be crossed.

### Backend (Diten.Platform)

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Meetings/**` — the 8 entities + enums under
  `Domain/Enums/Meetings/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IMeetingRepositories.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/**` — Commands / Queries /
  Handlers / Validators / `MeetingModels.cs` / `Services/**` (incl. `IRecordLinkService`,
  `IMeetingIdempotencyKeyResolver`) / `Providers/MeetingWorkItemProvider.cs` /
  `SelfRegistration/MeetingManifestProvider.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/MeetingRepositories.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs`
  — **extend only**
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` — register services,
  provider, manifest (one block, additive)
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MeetingsController.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**`
- **One additive line into `CreateTaskItemFromTemplateHandler`'s sibling space, or a new
  `CreateTaskItemCommand` construction site inside `Features/Meetings`** — never a modification to
  `CreateTaskItemHandler.cs` itself (protected, §6).
- **One additive field on `IMessagingProvider`/`MessagingProviderEmailRequest`** (an attachment list) IS
  in scope for slice S5b (Stage 1, ADR-003 §5) — recorded here because it is the one place this pack's own repo scope
  touches a file it does not otherwise own; see §7 dependency table and §20. Not built in this pack (draft
  stage authorizes no code at all).

### Frontend (Diten.Web) — Compact set

- `frontend/Diten.Web/Controllers/MeetingsController.cs` (proxy, `[Authorize]`, `/Meetings/api/*` →
  `{GatewayUrl}/api/v1/meetings/*`)
- `frontend/Diten.Web/Views/Meetings/{Index,Create,Edit,Details}.cshtml` + `_DataTable`, `_Filter`, `_Form`,
  `_IndexL10n` + `MeetingsIndex.cs`
- `frontend/Diten.Web/Views/Meetings/{MeetingTypes,MinutesEditor}/**` — the two Compact-shaped satellite
  screens (meeting-type settings list; the minutes editor, its own page, not a tab per §"2a. TAB ≠ PAGE")
- `wwwroot/assets/js/Meetings/{index.js,index.l10n.js,form.js,minutes-editor.js,types/*.js}`
- `Resources/Views/Meetings/MeetingsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (+ one resx set per satellite screen)
- `wwwroot/assets/css/backbone-custom.css` — only if needed, `.meeting-*`-scoped classes (FG-003)
- Task Center (`WorkCenterNext`) — **no file changes**: MOD-0357 enters through the fourth
  `IWorkItemProvider`, which the existing WC-1 aggregation handler already iterates additively
  (`Features/WorkAggregation` untouched apart from the DI line above).

---

## 6. Protected Paths

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/TaskItem.cs` — **no field added, ever**
  (ADR-003 §3, the deviation's own condition). A pack revision that proposes a `TaskItem` field is not this
  pack; it is a new ADR.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/**` — MOD-0024 is consumed through
  its public commands (`CreateTaskItemCommand`) and its manifest-declared permissions only; no file inside
  this tree is edited by MOD-0357.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**` — WC-1 is consumed;
  only the one DI registration line adds the fourth provider — plus ONE contract completion in S1: the
  `relatedRecords` field that `fixture-contract.js` and MOD-0024's checklist-engine pack already declare
  (capability `relatedRecords`, max 20, shape `id · type · title · link`) but no backend emits today
  (measured 2026-09-11: zero `RelatedRecord` symbols in `WorkAggregationModels.cs`/`TaskWorkItemProvider.cs`).
  Adding that declared field is not a new contract; anything beyond it is.
- `services/Diten.AuthService/**` — permission seed/grant is a separate MOD-0018 task; this module's
  permissions arrive through self-registration only (§8).
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent only (§15).
- `execution/domains/management-governance/**` other than this pack file — DWS/BPM's own scope; MOD-0357
  does not touch `Dws`/`ProcessModeling` code, collections, or permission families
  (`domain-config.md` §"Planned service isolation contract" applies by analogy, even though MOD-0357 is not
  one of the two planned internal modules that section names).
- `.antigravity/**`; Blueprint `.xlsx`; `execution/registries/**`; `execution/portfolio/**`; other domain
  services; Office documents.

---

## 7. Dependencies

| Dependency | Use | Boundary | Status |
|---|---|---|---|
| MOD-0024 task engine | Every meeting-born task, and the `reviewMeetingPolicy` read | Consumed via `CreateTaskItemCommand` and the WC-1 contract's `reviewMeetingPolicy` shape; no file inside `Features/Tasks` edited | **Exists** |
| WC-1 `IWorkItemProvider` (`ProviderCode`, `ProviderContractVersion`, `GetWorkItemsAsync(WorkItemActor, ct)`) | Pending-invitation surface in the Task Center | Fourth provider; WC-1 code unchanged except one DI line | **Exists** |
| `INotificationEventDispatchAdapter` / `IMessagingProvider` | Invite / change / cancel email | Never throws; returns `Response<T>`; **`.ics` attachment needs an additive field on `MessagingProviderEmailRequest`** — verified today it carries `Subject`/`To`/`Cc`/`Bcc`/`BodyHtml`/`BodyText` and no attachment field at all | **Exists, gap recorded** — this pack's S5 slice is blocked on that one additive field |
| Working Calendar / CAND-CAP-0010 | Working-time context only (not scheduling logic) | Read-only consumption, same seam MOD-0024 already uses for SLA | **Exists** |
| Hangfire seam (`IRecurringJobRegistrar` + `IBackgroundJobHandler<T>`) | None required in Phase 1 — no recurring-series generation is in scope (§20 defers "tekrarlayan seri") | N/A this slice | **Exists**, unused for now |
| Audit (FG-005) | Every write command is auditable | `IAuditableCommand` + metadata provider, same pattern every Platform write command follows | **Exists** |
| MOD-0018 | Effective permission, tenant isolation | Consumed; browser never authority | **Exists** |
| MOD-0007 (decisions) | *(future)* promote embedded `Decision` rows to a real cross-module reference | Not built — no forward reference minted now | **Does not exist — deferred, not simulated** |
| MOD-0359 (governance bodies) | *(future)* meeting-type "governing body" becomes a real FK | Not built — free-text/lookup label stands in | **Does not exist — deferred, not simulated** |
| Document Management e-signature | *(future)* sign the minutes | Only a field-summary signer exists today; the minutes text itself is out of reach | **Exists but insufficient — deferred (§20)** |
| File/blob storage | Attachments on a meeting or its minutes | No storage provider exists anywhere in the repo | **Does not exist — deferred (§20)** |
| Google Calendar/Meet | External invite/join link generation | Not built | **Does not exist — Phase 2 (§20)** |

---

## 8. Runtime Constraints

1. Mongo, tenant-scoped, soft delete, **tenant-first compound indexes** on every `meeting_*` collection.
2. `TenantId` resolved server-side (`TenantRepository<T>` overwrites it); never accepted from the client.
3. **Multi-company tenant boundary (K10, BL-057 principle):** a meeting's visibility follows the
   organizer's company (legal entity), the same three-leg rule (`ITaskAssignmentScopeResolver`) MOD-0024's
   assignment scope already enforces — same company OR reporting-chain descent OR an explicitly granted
   scope. MOD-0357 does not build a second scope engine; it asks the existing resolver.
4. Every self-registered page/action satisfies the module-self-registration contract (§"Module
   Self-Registration Standard"); the manifest, not a hand-edited catalog row, is the source of truth.
5. Idempotency (K11): both meeting creation and meeting→task creation compute a key via
   `IMeetingIdempotencyKeyResolver`, the same `ResolveIdempotencyKey` pattern the BRD reference-data
   ingestion already uses — a resubmitted create (double-click, retried request) is answered with the
   FIRST result, not a duplicate row.
6. Partial success is visible, never a silent all-or-nothing (K12): creating a meeting, opening a linked
   task, and sending an invite are three separate outcomes on the wire — a failed invite email must not roll
   back a created meeting, and the response tells the caller which of the three happened.
7. Browser never calls a service port: browser → `/Meetings/api/*` → Gateway 5000 → Platform.
8. 7-language l10n for every user-facing string (tenant shell) — no new hard-coded text.
9. No inline CSS (FG-003).
10. Attachments are **out of scope** this slice (§20) — no storage provider exists to receive them.

---

## 9. Layout & Shell Contract

- `shell: tenant` → `Layout = "_LayoutTenantShell";` stated **explicitly** in every `.cshtml` file.
- Routes: `/Meetings`, `/Meetings/Create`, `/Meetings/{id}`, `/Meetings/{id}/Edit`,
  `/Meetings/{id}/Minutes` (its own route — a minutes editor is not a tab of the detail page, per
  self-registration §2a: it is reached from the detail page but is a distinct route/page), `/Meetings/Types`,
  `/Meetings/Types/Create`, `/Meetings/Types/{id}/Edit`.
- Manifest pages declare **`IsNavigationVisible: true`** for the top-level `/Meetings` list only (the
  module's own nav entry — unlike MOD-0024, which is deliberately hidden behind the Task Center; MOD-0357
  is a first-class tenant screen, not a personal-work aggregator). `/Meetings/Types` is
  `IsNavigationVisible: false` (reached from the list's settings entry, per §2a).
- DataTable list (`/Meetings`): upcoming/past segment, filters for meeting type, organizer, "I am an
  attendee", date range, and linked-task presence — per the WP's own field list.

---

## 10. Backend File Convention

Live Platform CQRS (mirrors GoldenReferenceCompact + MOD-0024 §10):

```text
Features/Meetings/
├── Commands/            (sealed records, one per file, no Command suffix on handlers)
├── Queries/
├── Handlers/
│   ├── CommandHandlers/ {Verb}MeetingHandler.cs, {Verb}MeetingMinutesHandler.cs, {Verb}MeetingTypeHandler.cs
│   └── QueryHandlers/   Get{Meeting|MeetingMinutes|MeetingType}{List,ById}Handler.cs
├── Validators/          {Verb}MeetingValidator.cs
├── Services/            IRecordLinkService, IMeetingIdempotencyKeyResolver, IMeetingInviteMailer
├── Providers/            MeetingWorkItemProvider.cs
├── SelfRegistration/    MeetingManifestProvider.cs
└── MeetingModels.cs     (ALL DTOs in one file)
```

- Repositories inherit **`TenantRepository<T>`** (as Tasks/Organization do) — auto tenant + `IsDeleted` filter.
- Collections: snake_case **plural**, `meeting_` prefixed (Platform convention).
- Controller: thin, `CustomBaseController`, `[Authorize]` + `[HasPermission]`, `Response<T>`,
  `ICorrelationContext`.
- **Do not** create new base entity / repository base / tenant / correlation / audit / event / job
  infrastructure — `TenantScopedEntity` and the existing seams are reused verbatim.
- BSON element names PascalCase; API JSON camelCase — same as every live Platform module.

---

## 11. Frontend File Contract

Golden Reference **Compact**, mirrored file-for-file from `Views/DevEnablement/GoldenReferenceCompact/`:
`Index/Create/Edit/Details.cshtml` + `_DataTable/_Filter/_Form/_IndexL10n.cshtml` + `MeetingsIndex.cs`
marker; `index.js`, `index.l10n.js`, `form.js`; 7 resx files — for the **Meeting** list/create/edit/details
surface. Two satellite Compact-shaped screens repeat the same file set under their own module folder:
`MeetingTypes/` (settings CRUD) and the minutes editor under `Meetings/{id}/Minutes` (its own
`Details`-shaped page with a save/publish/correct action bar, not a form in the create/edit sense).

`_CreateEditOffcanvas.cshtml` and `_DetailsQuickView.cshtml` are **forbidden** under `golden_reference:
compact` (module-pack-standard.md §5) — no deviation is requested here, unlike MOD-0024's DEV-1.

---

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `Meeting.Title` | Yes | trim, max 200 | — | — |
| `Meeting.MeetingTypeId` | Yes | must resolve, tenant-scoped, active | FK-style existence | `ExistsAndActiveAsync` |
| `Meeting.StartAt`/`EndAt` | Yes | `EndAt > StartAt` | — | — |
| `Meeting.OrganizerUserId` | Yes | must be a tenant user | — | same directory seam as MOD-0024 assignee resolution |
| `Meeting.FollowUpOfMeetingId` | No | if set, must resolve in-tenant and must not point at itself | — | `ExistsAsync` + self-reference check |
| `MeetingAttendee.UserId` | Yes | must be a tenant user; no duplicate attendee row per meeting | Unique compound index `(MeetingId, UserId)` | `ExistsByMeetingAndUserAsync` |
| `AgendaItem.Text` | Yes | trim, max 500 | — | — |
| `MeetingType.Name` | Yes | trim, max 200, tenant-unique | Unique index | `ExistsByNameAsync` |
| `MeetingType.AgendaTemplate` | No | ≤ 20 lines, each ≤ 200 chars | — | — |
| `MinutesVersion.CorrectionReason` | Conditional | required when `CorrectionOfVersionNumber` is set | — | — |
| Meeting creation, meeting→task creation | — | idempotency key computed server-side | — | `IMeetingIdempotencyKeyResolver` |

---

## 13. Failure Path to Verify

- **Duplicate meeting create (retried request)**
  - Expected: 200/201 with the SAME meeting id as the first attempt — no second row (K11)
- **Duplicate meeting→task create (retried request)**
  - Expected: same task id returned, no second `RecordLink` row (K11)
- **Invite could not be sent (SMTP/adapter failure)**
  - Expected: meeting is still created (201); invite failure is reported as its own outcome, never rolls back
    the meeting (K12) — same posture MOD-0024's `NotifyAssignedAsync` already takes for assignment email
- **Linked task not found (deleted/cross-tenant) when reading a meeting's linked-task list**
  - Expected: the dangling `RecordLink` row is DROPPED from the response, never rendered as "a task" with no
    name behind it (same rule `MOD-0024-task-checklist-engine.md` already states for `relatedRecords` whose
    far end cannot be read)
- **Publishing minutes twice / editing a published version directly**
  - Expected: 409 — a published `MeetingMinutesVersion` is immutable; the only path forward is
    `CorrectPublishedMinutes`, which creates a new version row
- **Correcting minutes without a reason**
  - Expected: 400, validator message, no version written
- **Responding to an invitation for a meeting the caller is not invited to**
  - Expected: 404 (no `MeetingAttendee` row for this caller — never leaks that the meeting exists to someone
    with no attendee row, mirroring the cross-tenant "404, not 403" posture used elsewhere)
- **Reassigning organizer / editing a Cancelled or Completed meeting**
  - Expected: 409 invalid-state
- **Concurrency conflict** (two edits to the same meeting/minutes/type)
  - Expected: 409 + "the record changed meanwhile; reload and retry" — same wording pattern every
    MOD-0024-adjacent handler uses
- **Unauthorized actor** (missing `platform.meetings.*` key)
  - Expected: 403; UI never renders a control it cannot use (same rule MOD-0024's projection follows since
    the BL-361/BL-362 fix — a withheld control, not merely a disabled one, where the actor has no
    relationship to the record at all)
- **Cross-tenant read/update/delete**
  - Expected: 404 / empty, no metadata leak

---

## 14. Authorization Convention

```text
Policy:     [Authorize]                       (tenant user)
Permissions (PKS-001 lowercase-dotted):
  platform.meetings.read          platform.meetings.create      platform.meetings.update
  platform.meetings.delete        platform.meetings.bulk-delete
  platform.meetings.minutes-write platform.meetings.minutes-publish
  platform.meetings.types-manage   platform.meetings.read-all        (§22 D3: every meeting in the tenant, for QA/management)
```

- Constants are defined **locally** in `Diten.Platform`; the seed/grant is a separate MOD-0018 task.
- `Module=meetings` / `Scope=Tenant` is derived from the manifest's own page routes (none are `/Platform/…`)
  — the same self-registration mechanism that already prevents the B2-class scope-lock defect MOD-0024's
  own pack recorded; no permission is hand-seeded.
- **Meetings are not a permission back-door for tasks (K9).** Opening a task from a meeting requires the
  ORDINARY `platform.tasks.create`/`.assign` keys — `platform.meetings.*` grants nothing over `TaskItem`.
  Conversely, an assignee named on a meeting-born task does not need to BE an attendee of that meeting; a
  non-attending task owner sees only the meeting's title and date through the `RecordLink`'s minimal
  projection, never its agenda or minutes content (K9).
- Watchers/observers are not modeled in this slice — attendee response state (K5) is the only
  "who is following this" concept MOD-0357 carries.

### Notification events (manifest-declared)

`platform.meetings.invited` · `platform.meetings.rescheduled` · `platform.meetings.cancelled` ·
`platform.meetings.minutespublished`.

---

## 15. Gateway / API Routing Decision

```text
Karar: Gateway değişikliği GEREKLİ → ayrı integration-agent task'i.
```

`api/v1/meetings/**` does not exist in `ocelot.json` and no catch-all covers it. Required pair (authored by
integration-agent, **not** here): `/api/v1/meetings/{everything}` → `localhost:5057`, methods
`GET, POST, PUT, DELETE, OPTIONS`. Until it exists the proxy returns 503 — a release blocker, exactly as
MOD-0024's own equivalent gap was.

---

## 16. Acceptance Criteria (phase-tagged, K1–K12 made measurable)

### Governance
- [x] `MOD-0357` identity unchanged from this pack's frontmatter; no new ID; `verify_module_id.py` exit 0 *(S1, 5f7dd687)*
      (already measured 2026-09-11 — reproduce before `ready-for-dev`).
- [ ] `Features/Tasks`, `Features/WorkAggregation` (except one DI line), AuthService, `ocelot.json`,
      `TaskItem.cs`, other domains' `execution/domains/**` untouched.
- [ ] No `api/tasks`/`api/meetings` route collision; backend is `api/v1/meetings`, proxy is
      `/Meetings/api/*`.

### K1 — meeting/task are two records, link is many-to-many, bidirectional
- [ ] A meeting and a task it produced are two independently readable/deletable records; deleting one never
      cascades to the other.
- [ ] `GET /meetings/{id}/tasks` and (on MOD-0024's side) a task's `relatedRecords` both resolve through the
      same `RecordLink` rows — verified by a shared-fixture test that creates one link and reads it from
      both directions.
- [x] MOD-0024 never writes a `RecordLink` row (integration test: MOD-0024's own task handlers touch no
      `meeting_record_links` collection).

### K2 — meeting-born task is a normal task
- [ ] Creating a task from an agenda line / a decision goes through `CreateTaskItemCommand` and is refused
      exactly when an ordinary create would be (out-of-scope assignee, missing title) — same reason codes,
      no new ones invented for this path.
- [ ] The task carries no meeting-specific field; its closure record is authored solely by whoever completes
      the task, never pre-filled from the meeting.
- [ ] Quick entry (title + person + date) produces a task identical in shape to one made through the full
      form with those three fields filled and the rest defaulted the same way MOD-0024's own create already
      defaults them.

### K3 — task → meeting via `scheduleReviewMeeting`
- [ ] A task type with `ReviewMeetingPolicy.Requirement == required` keeps `approve`/`signoff` visible but
      disabled with `REVIEW_MEETING_REQUIRED` until the linked meeting's minutes are **published** — not
      merely scheduled (measured against `MOD-0024-task-checklist-engine.md`'s own contract text: "before a
      meeting is scheduled, approve/signoff remains visible but disabled").
- [ ] `ScheduleReviewMeetingForTask` writes exactly one `RecordLink` (`LinkType: reviewMeeting`) and never
      itself flips the task's approval state — MOD-0357 reports, MOD-0024 (or its future MOD-0007 decision
      layer) decides.

### K4 — minutes
- [ ] A draft minutes version is freely editable; publishing requires `ExpectedVersion` and locks the row.
- [ ] Editing a published version directly is refused (409); the only path is `CorrectPublishedMinutes`,
      which requires `CorrectionReason` and produces a NEW version row, leaving the prior one intact and
      readable.
- [ ] A decision row never gains a `TaskId`-shaped field — it is text plus an optional `RecordLinkId`
      pointing at the task it produced, if any.
- [ ] A task opened after minutes publish is flagged distinctly (e.g. `CreatedAfterMinutesPublished: true`
      on the `RecordLink`) so the UI can label it "added later" — per ADR-003 §5.

### K5 — invitation
- [ ] `RespondToInvitation` accepts exactly `Accept | Decline`; there is no third "maybe" state in this
      slice (not requested by the owner's four asks).
- [ ] An accepted invitation does **not** turn into an İşlerim/My Work item (BL-026, explicitly named by the
      owner) — verified by a projection test that accepts an invite and asserts the meeting still projects
      only through `MeetingWorkItemProvider`'s pending-response surface, never through MOD-0024's own
      "mine" list.
- [ ] The `.ics` invite / change / cancel email is **Stage 1** (ADR-003 §5) and lands in slice S5b: it depends on
      ONE additive `Attachments` field on `MessagingProviderEmailRequest` (Notifications; `SmtpMessagingProvider`
      already builds MailKit bodies) — CT infra delivers that field as S5b's first step. S5 (ERP-internal
      accept/decline + calendar-open deep link) must work without it. S5b's own AC: invite, update and cancel
      each carry a valid `.ics` (METHOD:REQUEST / METHOD:CANCEL, the SAME UID across updates), verified by a
      real Gmail inbox adding and removing the event.

### K6 — continuation meeting
- [ ] Scheduling a follow-up copies `FollowUpOfMeetingId` and prepends the previous meeting's still-open
      linked-task agenda lines to the new meeting's agenda, in original order, before any newly typed lines.
- [ ] A closed (Done/Cancelled) linked task from the previous meeting is NOT carried forward.

### K7 — reschedule/cancel
- [ ] Rescheduling or cancelling a meeting notifies every linked task's owner (email + Task Center note, if
      such a note channel exists at build time) but writes nothing onto the task itself — the task's own
      lifecycle is untouched by a meeting-side change.

### K8 — meeting type setting
- [ ] A meeting type's `AgendaTemplate` pre-fills a new meeting of that type; the instance's agenda remains
      independently editable afterward (no live binding back to the template).
- [ ] `IsQualityRecord` and `RequiresESignature` default to `false` and are changed only through an explicit
      QA-authorized update — never inferred from the type's name or domain.

### K9 — permission boundary
- [ ] All eight `platform.meetings.*` keys are attributed `Module=meetings`/`Scope=Tenant` after first
      startup (evidence required, same gate MOD-0024 measured for its own permissions).
- [ ] A user holding `platform.tasks.create` but no `platform.meetings.*` key cannot read a meeting's agenda
      or minutes, only the linked task's own fields — verified by a cross-permission test.

### K10 — tenant/company boundary
- [ ] In a multi-legal-entity tenant, a meeting organized in company A is invisible to a user in company B
      with no reporting-chain or granted-scope relationship to the organizer — same three-leg test matrix
      `TaskAssignmentScopeTests` already runs for MOD-0024, reapplied here against `Meeting.OrganizerUserId`.

### K11 — idempotency
- [ ] A meeting create retried with the same idempotency key returns the SAME meeting id, no duplicate row.
- [ ] A meeting→task create retried the same way returns the same task id and the same `RecordLink` id.

### K12 — partial success is visible
- [ ] Creating a meeting whose invite email fails still returns 201 for the meeting, with the invite
      failure surfaced as its own field/log — never a single opaque "something went wrong."

### 7-language l10n
- [ ] All Meetings resx sets (Index + MeetingTypes + minutes editor) share one key set across all 7
      languages; no raw key ever renders.

---

## 17. Test Expectations

- Unit: entity validation, `IRecordLinkService` read/write shape, `IMeetingIdempotencyKeyResolver`
  determinism, minutes version state machine (draft → published → corrected), the K1–K12 acceptance
  criteria above as executable tests.
- Integration/HTTP (TestHost, same pattern as `TaskAssignmentWriteGuardHttpTests`/
  `TaskLifecycleAuthorityHttpTests`): create → read-back round trip; cross-tenant isolation (404/empty);
  permission boundary (403 for missing key, 404 for a non-attendee's `respond`); concurrency 409;
  meeting→task creation exercising the REAL `ITaskAssignmentGuard` (not a double) so a regression in either
  module is caught by this suite, not assumed away.
- Sabotage proof (per this repo's own testing discipline, `TaskAssignmentWriteGuardSourceTests`-style): a
  source-scanning guard test asserting every class that writes `RecordLink` either goes through
  `IRecordLinkService` or is on an explicit, reasoned exception list — proven by temporarily removing the
  call and observing red, then green again.
- Frontend smoke: create/edit/details round trip on the Meetings DataTable; minutes editor draft→publish→
  correct flow; `verify_datatable_page.py --area Meetings --module Meetings --reference compact`.
- Build PASS: `Diten.Platform` (API+Application+Domain+Infrastructure), `Diten.Web`, gateway (once routed).
- RESX parity PASS across all 7 languages for every Meetings resx set.
- `NavManifestL10nGuardTests`-equivalent: `Nav.Module.meetings` + `Nav.Page.{PageCode}` present in all 7
  `SharedResource.{lang}.resx` files.

---

## 18. Ready-for-dev Checklist

- [ ] Golden Reference Compact (backend + frontend) read as template; no folder/naming deviation planned.
- [ ] Frontmatter complete (`service: Diten.Platform`, `shell: tenant`, `golden_reference: compact`,
      `entity_base: TenantScopedEntity`).
- [ ] Layout & Shell Contract: `Layout = "_LayoutTenantShell"` stated explicitly, per file, in the AC.
- [ ] Backend File Convention: `Handlers/CommandHandlers/` + `Handlers/QueryHandlers/` split named.
- [ ] Frontend File Contract: Compact set complete for Meetings + MeetingTypes + minutes editor; no
      offcanvas files listed.
- [ ] Validation Rules written for every field.
- [ ] Failure Path to Verify: ≥ 4 scenarios present (duplicate, missing, unauthorized, concurrency) — this
      pack lists 10.
- [ ] Authorization Convention: full permission list + policy + actor type.
- [ ] Gateway routing decision explicit (required + integration-agent task named).
- [ ] Acceptance criteria are testable, phase-tagged, and map 1:1 to K1–K12.
- [ ] Test expectations cover build/verifier/RESX/HTTP smoke/sabotage proof.
- [ ] Owner has answered the open questions in §21, or explicitly deferred each with a reason.
- [ ] Owner has approved the S1–S10 slicing in §19, or reordered it.

---

## 19. Implementation Notes

**Slicing (each slice is its own WP; this pack authorizes none of them yet):**

| Slice | Scope |
|---|---|
| S1 ✅ `5f7dd687` (2026-09-11; CT doğruladı, canlı değil) | `RecordLink` collection + `IRecordLinkService` + MOD-0024's `relatedRecords` read wired to it (the one point of contact with `Features/Tasks` this pack allows) |
| S2 ✅ (2026-09-11; CT doğruladı, canlı değil) | `Meeting`/`MeetingAttendee`/`AgendaItem`/`MeetingType` backend CRUD + manifest (9 izin) + meetings resolver; UI yok. Not: manifest `Nav.Module.MEETINGS` ve `Nav.Domain.MANAGEMENTGOVERNANCE` anahtarlarını 7 dilde ister (Web nav muhafızı) — ayrı l10n işi |
| S3 ✅ (2026-09-11; CT doğruladı; canlı: ajan + CT sahibin oturumuyla — menü, liste boş durumu, oluştur formu (tür/katılımcı seçicileri boş: dev kiracısında tür ve pozisyon yok), 404 sayfası, Görev Merkezi regresyonsuz, konsol hatasız) | Screens: list (DataTable), create, edit, detail + Web proxy + gateway + 2 lookup; gündem detay sayfasında (create isteğinde alan yok); `Nav.Page.MEETINGS` 7 dil ayrı l10n işi; verifier 58/84 (boş-area yol birleştirme ve proxy mimarisi — araç sınırı); dev kiracısında pozisyon yok → seçiciler boş (BL-356) |
| S3b | Read-only month calendar view on the meetings list (upcoming meetings by day; reuses the Task Center's `renderCalendar` once extracted — BL-365 #4). Owner decision 2026-09-11: calendar yes, kanban no (three states, no flow) |
| S4 | Meeting → task (three moments) and task → meeting (`scheduleReviewMeeting` receiving side) — ✅ delivered 2026-09-11, commit `680cb888` (WP-MG-MOD0357-S4-TASK-BRIDGE-01): preparation + on-the-spot moments (minutes-born moment → S6), link existing (agenda), receiving side of scheduleReviewMeeting with RecordLink reviewMeeting; K9 dual permissions, K11 idempotency (app-level), reviewMeetingPolicy projection fixed at optional (no TaskType field invented). Live proof waits for BL-358. |
| S5 | Invitation: ERP-internal accept/decline + Task Center trigger — ✅ PARTIAL 2026-09-11, commit (see git log "invitations — accept or decline") (WP-MG-MOD0357-S5-INVITATION-01): respond endpoint (K5), invite/change/cancel mail via IMeetingInviteMailer (K12, never rolls back), organizer = Accepted attendee, 3 templates × 7 languages. Task Center inbox card NOT done — stop rule hit: WC-1 contract has no `meetingInvite` type / accept-decline actions (app.js glyphs are a trigger-only fixture). → S5c |
| S5c | Task Center invite card — ✅ delivered 2026-09-12, commit `d3fa0058` (WP-MG-MOD0357-S5C-INVITE-CARD-01): WC-1 contract gains the `meetingInvite` intent + `acceptInvite`/`declineInvite` with five shape rules and eight self-tests; `MeetingWorkItemProvider` (3rd provider) + dispatcher onto S5's respond command; inbox card with a visible Decline; showcase fixture retired; CT added the real-Mongo pending-query test. Original scope:  extend the WC-1 contract (`fixture-contract.js`) with item type `meetingInvite` and action codes `acceptInvite` / `declineInvite`; add `MeetingWorkItemProvider` (4th IWorkItemProvider: my Pending invitations → inbox; accepted never becomes an İşlerim item, BL-026) and the inbox card with Accept/Decline calling `/respond`; retire the trigger-only showcase fixture. Open from S5: organizer cannot yet remove own attendee row (no backend constraint); a real invite email needs a second eligible user in dev. |
| S5b | `.ics` invite / change / cancel email — first step: additive `Attachments` field on `MessagingProviderEmailRequest` (CT infra, Notifications); then `IMeetingInviteMailer` attaches the `.ics` (same UID across updates; METHOD:CANCEL on cancel). Stage 1 per ADR-003 §5 — ✅ delivered 2026-09-13, commit `af7e03cc` (WP-MG-MOD0357-S5B-ICS-INVITE-01): additive `Attachments` carried through the synchronous notification chain (nothing persisted), `invite.ics` on invite/change/cancel, UID = meeting id on all three, SEQUENCE = Version, METHOD REQUEST/CANCEL, octet folding. CT correction: display names are DQUOTEd parameter values (RFC 5545 §3.2, RFC 6868), not TEXT-escaped. Both closed by WP-MG-MOD0357-BL374-RETRY-FIDELITY-01 (2026-09-13): a retry replays the stored .ics; an unresolved organizer gets no .ics. Live proof via Mailpit not yet run. |
| S6 | Minutes: draft, publish, correct, decisions-as-rows — ✅ delivered 2026-09-12, commit `23af2e58` (WP-MG-MOD0357-S6-MINUTES-01): append-only `MeetingMinutesVersion`, publish locks and sets the meeting Completed, correction demands a reason and writes v+1 while v stays byte-identical, decisions are server-numbered rows holding a RecordLinkId (never a TaskId), a task opened after publication is labelled "added later" and listed separately, own page not a tab, 7 languages. Real-Mongo test pins the unique version index. |
| S7 | Continuation meeting (K6 carry-forward) — ✅ delivered 2026-09-12, commit `4e1be23d` (WP-MG-MOD0357-S7-FOLLOWUP-01): schedule a follow-up from a closed meeting, `followUp` RecordLink back to the source, `FollowUpOfMeetingId` on the new meeting, idempotency key (resubmit returns the same meeting), a meeting cannot be its own follow-up, 7 languages. **CT correction in the same commit:** carry-forward first counted only the agenda-ANCHORED links, so an open action the meeting itself PRODUCED (on the spot, S4, or born from a published decision, S6) had no agenda line and did not carry — the one carry-forward a management review exists for. Now anchored lines first in the source meeting's own order, then `bornFromMeeting` links by creation time, no task twice; `preparation` stays out on purpose (that work fed INTO the meeting). |
| S8 | Meeting type setting screen — ✅ delivered 2026-09-11, commit `6bab238c` (WP-MG-MOD0357-S8-MEETING-TYPES-01): list/create/edit under `platform.meetings.types-manage`, nav-visible page MEETING_TYPES (7 languages), agenda-template pre-fill on the meeting form (K8 box 1 — code + tests; live proof waits for dev organisation data, BL-358), K8 box 2 flags default off. Also closed the Task Center dialog-helper duplication (BL-365/BL-367). |
| S9 | `reviewMeetingPolicy.required` gate wired end-to-end against a real MOD-0024 task type. **Owner decision 2026-09-13:** the decision action unlocks when the linked review meeting's minutes are PUBLISHED, not when the meeting is merely scheduled (K3 already says so; MOD-0024's checklist-engine pack said "scheduled" and is corrected on the PSS branch). Field delivered on MOD-0024's side: `TaskType.ReviewMeetingRequirement`, commit `b5eac362` (PSS branch). Remaining: the projection reads it and gates approve/signoff. **2026-09-15:** no longer waits for main — the meetings lane contains the task-engine lane (merge chain Auth ⊂ PSS ⊂ meetings), so the field and the minutes are in one tree; implementation WP-MG-MOD0357-S9-REVIEW-GATE-01. |
| S11 | Recurring meeting SERIES (weekly quality review, monthly management review): a light rule generating instances on the task engine's recurrence/Hangfire pattern, each instance chained to the previous as a follow-up (K6). Owner decision 2026-09-11 — moved INTO Stage 1 because the Blueprint scopes MOD-0357 as *cadence*; exceptions and attendee-calendar writes stay with Google (Phase 2) — ✅ delivered 2026-09-12, commit `3587b368` (WP-MG-MOD0357-S11-MEETING-SERIES-01): `MeetingSeries` rule (type, frequency Weekly/Monthly/Quarterly/Yearly, interval, window, duration, organizer, attendees, `LeadTimeDays`, `ChainAsFollowUp`) + CRUD screen under `platform.meetings.series-manage` (7 languages, nav page MEETING_SERIES) + `MeetingSeriesSweepJob` on the existing Hangfire seam. **No third creation path:** the first instance goes through `CreateMeetingCommand`, every later one through S7's `ScheduleFollowUpMeetingCommand`, so the carry-forward and the cross-link are the same code a hand-scheduled continuation uses. The claim (`LastProcessInstanceId` under an expected-version write) happens BEFORE the create, so the failure mode is a missed occurrence, never a duplicate meeting. The organizer comes from the rule and never from `ICurrentUserContext` — a sweep has no "self" (the bug `TaskRecurrenceRule`'s own comment records). **Two switches hold the job off:** `BackgroundJobs:RegisterStandardJobs` and `EnabledJobs["Diten.Platform.MOD-0357.MeetingSeriesSweepJob"]`; production has a third, `BackgroundJobs:Enabled=false`. "The series is not generating" is a configuration question before it is a defect. **Measured by CT, not in the agent's report:** a generated instance DOES send the S5 invite mail, and because the acting user is `Guid.Empty` the mailer excludes nobody — the organizer receives an invitation to their own series meeting (BL-373). |
| S10 | Live-session verification pass (this repo's own "green tests ≠ working" discipline — HTTP-level round
        trips against a running Platform, not only in-memory doubles) — **run 2026-09-13** (WP-MG-MOD0357-S10-LIVE-PASS-01): PASS S3, S8, S4 meeting→task, review-meeting action disabled once scheduled, S6, S7, BL-379. FAIL, fixed by CT the same day: the Task Center "schedule review meeting" click (`bea716aa`, BL-381); the S11 sweep could not run — handler missing from DI, and a disabled job stayed in Hangfire (`87f660ac`, BL-382). NOT RUN: S5/S5c invite accept-decline, S5b `.ics` in Mailpit, BL-374 retry — the dev tenant has one real user; S11 generate + no-duplicate — blocked by BL-382. Rerun those four once a second tenant user exists. **S10B rerun 2026-09-13:** S11 PASS end to end (one instance, no duplicate on a second trigger, the job gone from Hangfire after its flag was switched off). S4b FAIL on a second layer — the Task Center views did not load Meetings/api.js (fixed by CT, BL-381). BL-374 FAIL — a first-send failure was never scheduled for retry (fixed by CT). S5b partial: the generated invite .ics is correct (UID, SEQUENCE, METHOD, quoted CN). NOT RUN again: S5/S5c and the change/cancel mails — still no second position-assigned user. |
| S12 | **Toplantı raporu ve aksiyon kaydı** (BL-396, owner decision 2026-09-14) — spec written 2026-09-14 (WP-MG-MOD0357-S12-MEETING-REPORT-PACK-01), **ready-for-dev — owner decisions 2026-09-15 (§23.13)**, full spec at §23. Content: period/type/organizer-filtered meeting list, attendance rate, decisions, open+overdue actions born from a meeting (Blueprint "Follow-up Register"). Visibility = the meeting's own rule (D3), no new hole. Export needs a real audit trail — **shares its prerequisite with MOD-0024's own Dilim 1e/BL-347 gap** (§23 "Audit trail — shared prerequisite, not a private one"); not authorized for code until that shared writer exists AND the owner approves this row's status. **2026-09-15: both hold** — `IDataExportAuditWriter` landed (`5681eaac`, BL-347) and the owner approved (§23.13); implementation WP-MG-MOD0357-S12-MEETING-REPORT-01. |

Each slice needs its own go-ahead once this pack reaches `ready-for-dev`; `@orchestrator` does not run S2
before S1's acceptance criteria are demonstrated, per the standard Phase-by-Phase discipline MOD-0024's own
pack already models.

**Naming note:** `MeetingMinutes`/`MeetingMinutesVersion`, not `MeetingReport` — "report" was ADR-003's own
working phrase for the general closure-record vocabulary (`MOD-0024-task-closure-and-reporting.md`'s
"meeting report"), but "minutes" is the term ISO 9001 §9.3.2 and the company's own `GMG-QMS-SOP-0013`
("Management Review" SOP; `task_type_code: MGMT-REVIEW`, `GMG_ERP_Task_Type_Seed_2026-08-24.csv`) use for
this exact record — matching the vocabulary the SOP already uses.

---

## 20. Follow-up Items

Verbatim from ADR-003 §6, plus the two items ADR-003 names elsewhere in the same document:

- Google Calendar/Meet integration (Phase 2).
- E-signature on the minutes text (DM's e-signature today signs a field summary only; QA must decide
  whether/how a minutes document is signed).
- File attachments on a meeting or its minutes (no storage provider exists in the product yet).
- Tasks assigned to an external (non-tenant-user) attendee.
- Room/resource booking.
- ~~Recurring meeting series~~ — moved into Stage 1 as slice S11 (owner, 2026-09-11); only Google-side exceptions/attendee-calendar sync stay deferred. Original note: today every meeting,
  including a "continuation", is created one at a time.
- AI-suggested actions from a transcript or agenda.
- Connecting Enterprise Strategy's "Strategic Reviews" screens to this engine.
- `.ics` invite email is **not** a follow-up — it is Stage 1, slice S5b (§19); only the additive attachment
  field it needs is recorded in §7 as a dependency (CT infra).
- Promoting an embedded `Decision` row to a real MOD-0007 cross-reference, once MOD-0007 exists.
- A real `MeetingType.GoverningBody` foreign key, once MOD-0359 exists.

---

## 21. Open Questions to Owner

- **Minutes as a quality record, by default or per type?** K4/K8 currently leave `IsQualityRecord` a
  per-`MeetingType` setting QA fills in, defaulting to `false`. Is that the right default, or should the
  `MGMT-REVIEW` task type's own governing SOP (`GMG-QMS-SOP-0013`) make quality-record status the DEFAULT
  for any meeting of that type specifically, with QA opting OUT rather than in?
- **External attendee email privacy.** An attendee row today assumes a tenant user (`UserId`). If a
  non-tenant participant (a customer, auditor, external consultant) needs to appear on the attendee list,
  what may be stored about them (name/email only? nothing persisted beyond the invite send?) and who may
  see it?
- **Multi-company tenant visibility.** §16 K10 applies the existing three-leg assignment-scope rule to
  `Meeting.OrganizerUserId`. Is that the right analogy for meeting VISIBILITY specifically (as opposed to
  who may be *assigned* a task), or should a meeting be visible more broadly within its own legal entity
  regardless of reporting-chain distance from the viewer?
- **Organizer departure.** If the organizer leaves the tenant (deactivated user), does the meeting need an
  explicit reassignment step (mirroring MOD-0024's reassign/return model), or does it simply keep its
  historical organizer field and become read-only for editing purposes?
- **Retention.** How long do published minutes and their correction history need to be retained? Is this
  governed by the same policy as controlled documents (`GMG-QMS-SOP-0013` review cycle: 24 months) or does
  a meeting record need its own retention rule?

---

## 22. Owner Decisions (2026-09-11) — answers to §21, binding for every slice

- **D1 · Minutes as quality record:** default `IsQualityRecord = false` per meeting type; the *Management review*
  type (`MGMT-REVIEW`, GMG-QMS-SOP-0013) defaults to `true`. QA may change either through the meeting-type setting;
  never per meeting instance. SAP/Oracle do the same at TYPE level (a controlled document's type decides its record
  status), which is why this is a setting, not a checkbox on the meeting.
- **D2 · Attendees from another company:** allowed — any ACTIVE tenant user may be invited, whatever their legal
  entity or unit; the people picker groups by company/unit only for findability. Tasks raised in the meeting still
  obey the task assignment scope (K9); inviting is not assigning. External (non-tenant) attendees stay deferred (§20).
- **D3 · Visibility (owner undecided → Control Tower default, revisable by ADR):** a meeting is visible to its
  organizer, its attendees and whoever holds `platform.meetings.read-all` (QA/management, tenant-wide). No
  company-boundary rule in Stage 1; `read` alone shows only meetings you are part of. Unauthorised readers get 404.
- **D4 · Organizer leaves:** an explicit *reassign organizer* command (mirrors MOD-0024 reassign); the historical
  organizer stays in the record and in every published minutes version.
- **D5 · Retention:** minutes of quality-record types follow the controlled-document retention rule; all other
  meetings and minutes are never hard-deleted (soft delete only), no separate retention rule in Stage 1.

---

## 23. Slice S12 — Meeting Report & Action Register (BL-396)

> **Status of this section: PROPOSED.** Written per WP-MG-MOD0357-S12-MEETING-REPORT-PACK-01 (Prompt P1 v1,
> "yalnız paket dilimi — kod YOK"). It authorizes no code. Owner approval turns this row's status from
> proposed to `ready-for-dev`, the same gate every other slice in §19 passed through — see §23.9.

### 23.1 Scope (owner decision, BL-396, 2026-09-14)

Content, verbatim from the owner's decision:

- A **meeting list**, filterable by period, meeting type, and organizer.
- **Attendance rate** per meeting (and rolled up over the filtered set).
- **Decisions** — the `Decision` rows already embedded in each meeting's latest `MeetingMinutesVersion`
  (§3 "Decisions are not MOD-0007"; no new entity).
- **Open and overdue actions born from a meeting** — the Blueprint's "Follow-up Register": tasks reached via
  `RecordLink` with `LinkType: bornFromMeeting` or `LinkType: preparation` (§23.3), task status/due date read
  from MOD-0024, never duplicated.

This is a **read/reporting slice**. It adds no new `Meeting`/`MeetingAttendee`/`AgendaItem`/`MeetingType`/
`MeetingMinutesVersion`/`RecordLink` field and writes nothing to any of them — same posture MOD-0024's own
work-report pack takes toward `TaskItem` (`MOD-0024-task-closure-and-reporting.md` §3: "No new persisted
runtime object is authorized"). It is **not** a second reporting engine: §23.2–§23.5 name, by file and
section, the exact MOD-0024 pieces this slice reuses as a pattern rather than reinvents.

### 23.2 Screens

One new page under the existing `/Meetings` list, mirroring `/Meetings/Types` and `/Meetings/Series`'
own shape (§9: nav-visible children of the top-level list, each its own route, not a tab):

| Route | Purpose | Nav |
|---|---|---|
| `/Meetings/Report` | The report screen: filter bar (period, type, organizer — same three the owner named), a summary tile row (meeting count, attendance rate, decision count, open/overdue action count — same "tile row" shape as `/Tasks/WorkReport`'s four tiles, `MOD-0024-task-closure-and-reporting.md` §11 Faz 5b), the filtered meeting list, a Decisions section, and the Action Register (open + overdue, sortable by meeting/due date/assignee) | `IsNavigationVisible: true`, `ParentPageCode: MEETINGS`, `PageType: "List"` — same manifest shape as `PageMeetingTypes`/`PageMeetingSeries` in `MeetingManifestProvider.cs` |

No offcanvas, no create/edit form — this is `golden_reference: compact`'s reporting exception the same way
MOD-0024's own work-report pack sets `golden_reference: none` (frontmatter, this pack's own line 7 stays
`compact` for the Meeting/MeetingType/Series CRUD surfaces; S12 is a read-only page under the SAME shell,
not a CRUD surface, so it carries no create/edit/details file trio — `_Filter.cshtml` + `_IndexL10n.cshtml`
only, no `_DataTable.cshtml` DataTables-v2 contract since the meeting list here is filter-and-render, not a
sortable/paginated grid the golden-reference DataTable template governs; the Action Register sub-list MAY use
`_DataTable.cshtml` v2 on its own, since it IS a paginated grid — decided at implementation time, not here).

### 23.3 Data sources and query bounds

| Data | Source | Read pattern |
|---|---|---|
| Meeting list (period/type/organizer) | `Meeting` collection, same shape `GetMeetingList` already reads (§3 CQRS) | New query, same repository, REQUIRED period (mirrors `IWorkReportRepository`'s own required-period rule, `MOD-0024-task-closure-and-reporting.md` §12 item 19 — "no filtered read exists; a full-collection scan at scale is refused by requiring a period") |
| Attendance rate | `MeetingAttendee.InvitationResponse`/`AttendanceStatus` per meeting in the filtered set, aggregated in the database (`Aggregate().Match().Group()`, same pattern as `WorkReportRepository`) | Derived, never authored — same "B · analytics, not a record" posture `MOD-0024-task-closure-and-reporting.md` §1 draws between closure record and work report |
| Decisions | `MeetingMinutesVersion.Decisions[]` (embedded), latest **published** version per meeting in the filtered set — a draft's decisions are not yet real (K4: "editing a published version directly is refused"; a draft can still be corrected/abandoned) | Read-only projection; no `Decision` entity is minted (§3 already forbids it) |
| Open + overdue actions (Follow-up Register) | `RecordLink` rows where `SourceModuleCode: "meetings"`, `SourceRecordId` ∈ {meeting ids in the filtered set}, `LinkType` ∈ `{bornFromMeeting, preparation}` — **not** `agenda` (an agenda anchor is where the line SITS, not where the task was BORN) and **not** `reviewMeeting` (that link points the other direction: a task requiring a meeting, not a meeting producing a task) | `IRecordLinkService`'s existing batched read (§3: "the SAME query shape cannot drift between the two consumers" — this makes a THIRD consumer, still through the one service, never a raw collection query) |
| Task status/due date for each action | MOD-0024's `TaskItem` (`Lifecycle`, `DueAt`, `AssigneeUserId`), read **only** — batched by id, same discipline `RecordLinkRepositoryMongoTests.ListBySourceAsync_and_ListByTargetAsync_batch_N_ids_into_ONE_call` already pins for the S1 `relatedRecords` read | No write, ever — `Features/Tasks/**` stays protected (§6) |

**Query bounds:**
- The meeting-list, attendance and decisions reads are bounded the same way `IWorkReportRepository`'s own
  criteria are: a **required** `From`/`To` period, no unbounded "all time" query. Meeting type and organizer
  filters are additive `$match` clauses on an already tenant-scoped, already period-scoped pipeline.
- The Action Register is **not** period-bound by the ACTION's own due date (an overdue action from six months
  ago must still show as overdue) — it is bound by the filtered set of MEETINGS the action was born from
  falling inside the period, mirroring how MOD-0024's own ageing report is anchored to the report's own edge,
  never to "everything that ever existed" (`MOD-0024-task-closure-and-reporting.md` §12 items 28–29).
- On-screen the meeting list and the Action Register are **paginated**; the summary tiles are the aggregated
  totals, not a client-side sum of a page.
- **Index:** no new collection, so no new base index family — but `meeting_record_links` needs a compound
  index `(TenantId, SourceModuleCode, LinkType, SourceRecordId)` if `IRecordLinkService`'s existing
  `ListBySourceAsync` does not already carry one shaped for a MULTI-id batched lookup filtered by `LinkType`
  (S1 built it for the single-record `relatedRecords` read; S12 is the first caller that asks "every
  `bornFromMeeting`/`preparation` link for THIS SET of meetings" — verify against the live index list before
  `ready-for-dev`, add if missing, never query un-indexed at tenant scale).

### 23.4 Action register vocabulary — tied to task lifecycle, consistent with S7 carry-forward

- **Open:** the linked `TaskItem.Lifecycle` is not `Done` or `Cancelled` (the same two terminal states every
  other MOD-0357↔MOD-0024 read already treats as closed — §16 K1 "deleting one never cascades", §7 dependency
  table).
- **Overdue ("geciken"):** open **and** `TaskItem.DueAt` is set and in the past (relative to "now" at query
  time, not frozen to the filter period's own end — an action does not stop being late because the report
  window moved past it).
- **Closed ("kapanan"):** `Lifecycle` is `Done` or `Cancelled` — shown in the register for context/history
  (so a reader can see "this WAS overdue and got closed", not just the currently-open set) but excluded from
  the open/overdue tiles.
- **Attribution — which meeting an action counts against:** the action's **origin** meeting, i.e. the
  `SourceRecordId` of its `bornFromMeeting`/`preparation` `RecordLink` row (§23.3) — **never** the latest
  continuation meeting it was carried into. This is the only answer consistent with S7's own design (§19 S7):
  carry-forward writes a **new, additional** `agenda`-type link anchoring the open line onto the continuation
  meeting's agenda; it does **not** rewrite or move the original `bornFromMeeting`/`preparation` link. An
  action can therefore have one birth link and several later agenda-anchor links across a chain of
  continuations — the register counts it once, against its birth, and MAY show "last carried to: {meeting}"
  as a secondary, non-attributing column derived from the newest `agenda` link in the chain, if the owner
  wants that detail (§23.9, open question).

### 23.5 Permission model — two options, owner's call

Both options land at `Scope: Tenant` regardless of which is chosen: the new page's route is `/Meetings/Report`,
not `/Platform/…`, so `ModulePageDescriptorNormalizer.ScopeFromRoute` (`.antigravity/rules/module-self-registration-standard.md`
§2c) derives `Tenant` automatically — no hand-classification either way.

**Option A — reuse `platform.meetings.read` / `platform.meetings.read-all` (no new key).**
- The report's own visibility rule (§23.6) is already exactly what these two keys already mean (§14 D3:
  organizer + attendee visible with `read`, everything with `read-all`) — the report shows nothing a `read`
  holder could not already piece together by opening each meeting one at a time; it is a faster read of the
  same data, not a wider one.
- Unlike MOD-0024's `platform.tasks.read`, `platform.meetings.read` carries **no** `PersonalWorkSurfaceScoped`
  collision — Meetings is deliberately **not** a personal-work aggregator (§9: "a first-class tenant screen,
  not a personal-work aggregator", the opposite of MOD-0024's own reason for minting
  `platform.tasks.work-report.read` separately from `platform.tasks.read`, `MOD-0024-task-closure-and-reporting.md`
  §12 item 20). The reason MOD-0024 split its key does not reproduce here.
- **Trade-off, stated plainly:** `read`/`read-all` ALSO grant opening an individual meeting's own detail and
  published minutes. Someone the owner wants to see ONLY the aggregate report/export (a QA auditor who should
  not browse arbitrary meeting content) cannot be given that narrower grant under Option A.

**Option B — mint `platform.meetings.report.read` / `platform.meetings.report.read-tenant-wide` (new keys,
mirrors MOD-0024's own precedent exactly: `platform.tasks.work-report.read` / `.read-tenant-wide`).**
- Gives the trade-off in Option A a real answer: a role can hold the report key without the per-meeting
  `read` key, or vice versa.
- Costs two more keys in a permission family that already has nine (§14) — more surface to seed, grant, and
  keep attributed `Module=meetings`/`Scope=Tenant` (§16 K9's own evidence gate would grow by two rows).
- Symmetric with the ONE other reporting slice in the product (MOD-0024's), which is a real argument for
  consistency even though the collision that forced MOD-0024's hand does not itself apply here.

**This pack does not choose.** Recommendation, not a decision: **Option A**, because the trade-off it accepts
(no report-only, meeting-content-blind role) has not been asked for by anyone, and Option B's two new keys are
a cost paid against a need not yet stated. **If the owner picks Option B, that is a new permission key and
requires explicit owner approval before it is seeded** (module-pack-standard.md §10 "Yeni anahtar önerilirse
sahip onayı gerektiğini açıkça yaz" — this line satisfies that instruction; the approval itself is not given
here).

### 23.6 Visibility — the meeting's own rule, no new hole

The report list, the decisions, and the action register are **filtered by the same D3 rule** every other
Meetings read already applies (§14, `MeetingEligibility.CanView`, `MeetingCommandHandlers.cs`):
`hasReadAll || meeting.OrganizerUserId == callerUserId || attendeeUserIds.Contains(callerUserId)`. A `read`
holder's report contains only meetings they organized or attended; a `read-all` holder's contains every
meeting in the tenant for the filtered period. **The report never widens what a `read` holder could already
see one meeting at a time** — it is the SAME predicate, applied as a `$match` stage instead of a per-request
check, which is the only way a report can exist without becoming its own, second visibility system (the exact
trap `MOD-0024-task-closure-and-reporting.md` §12 items 16–17 name for its own report: "a second scope engine
was NOT written").

The Action Register inherits this transitively: an action is listed only if its origin meeting passes the
same `$match`. A task's OWN assignee-scope visibility (K9: "a non-attending task owner sees only the
meeting's title and date") is irrelevant here — the register is read through the MEETING's visibility, not
the task's; a `read-all` holder sees every open action born from every meeting, including ones whose assignee
they could not otherwise see as a task-holder, exactly as `read-all` already means for the meeting itself.

### 23.7 Export and audit trail contract

**Same query, not a second one** — same discipline BL-346 states for its own export ("the file is the
report's own rows": `ExportAsync` calls the same `ReadAsync` the numbers use). Proposed shape, mirroring
`GET /api/v1/tasks/work-report/export?format=csv|json` file-for-file:

```text
GET /api/v1/meetings/report/export?format=csv|json&dataset=meetings|decisions|actions&{same filters as the screen}
```

`dataset` is required and picks ONE of the three grains (meetings, decisions, actions) rather than blending
them into one file — MOD-0024's own export never mixes grains either, and a "toplantı satırı + karar satırı +
aksiyon satırı" CSV would need three different column sets fighting for one header row.

Decisions carried over from BL-346, applied identically here: same permission as the screen (no separate
`.export` key — Option A/B above already covers read; export needs nothing beyond it), 50 000-row refusal
(`MEETING_REPORT_EXPORT_TOO_LARGE`, same posture as `WORK_REPORT_EXPORT_TOO_LARGE` — refuse, never silently
truncate), UTF-8 BOM, invariant-culture numbers, English column headers, a localized filename prefix
(`toplanti-raporu_…` / `meeting-report_…`), the `Content-Disposition`/row-count header pair proxied through
Web and Gateway unchanged, and the object-URL revoke happening in a `finally` block.

**Audit trail — GxP-required (owner decision, BL-396), and it is a SHARED prerequisite, not a private one.**

The root cause is BL-347, read directly: `AuditMetaAuditWriter.WriteAsync`
(`Features/Audit/Services/AuditMetaAuditWriter.cs:54-56,72`) hardcodes every call to
`ActorType = AuditActorType.PlatformAdministrator` and `IsPlatformGlobal = true` — it was built for genuinely
platform-admin-triggered meta-audit events and was never meant to receive a tenant user's own action. A tenant
user's report download recorded through it today would read "a platform administrator did this", which is a
wrong record, not a missing one (BL-347's own framing). Separately, the GENERAL auditable-command pipeline
(`AuditBehavior<TRequest,TResponse>.BuildAuditPlan`, `Contracts/Behaviors/AuditBehavior.cs:123-133`) resolves
the actor correctly for a tenant vs. platform caller — but **skips every query by design** ("a query is not
audited by default"), and an export is a `GET`. Neither existing path, as it stands today, can correctly
record a tenant user's export.

BL-347 itself names the two shapes a fix could take: (a) give `AuditMetaAuditWriter` an explicit actor-type
input instead of hardcoding one — the caller resolves it from `ICurrentUserContext` the same way the command
pipeline already does correctly for writes; or (b) build the query-side equivalent of `IAuditableCommand` so
`AuditBehavior`'s currently-query-skipping branch can opt a read/export request in. This pack does not decide
between (a)/(b) either — that choice belongs to whoever picks up BL-347, and picking one is out of S12's own
scope (BL-396's own wording: "dışa aktarmada denetim izi ŞART → BL-347 ile aynı kiracı tarafı denetim yazıcısı
kararına bağlı").

**What this pack DOES decide: S12's export is blocked on the SAME fix BL-346/BL-347 need, and that fix should
be built ONCE, upstream of both.** Reasoning:
- Building a Meetings-only tenant-actor audit writer while MOD-0024's own export still has none would produce
  TWO tenant-side audit gaps solved once and left open once, in the same product, for the same class of
  action — the opposite of "a kiracı tarafı denetim yazıcısı" (singular, as BL-396 itself phrases it).
- The fix is small and general (a parameter or a query-audit path on ALREADY-shared infrastructure,
  `Features/Audit/**`) — it does not belong to either module's own repo scope, and neither MOD-0024's nor
  MOD-0357's Protected Paths currently authorize either pack to edit `Features/Audit/**`.
- **Therefore: the fixed tenant-side audit writer is a shared pre-slice — its own small WP, owned by whoever
  BL-347 gets assigned to, landed BEFORE S12's export endpoint is built — not S12's own private prerequisite
  and not something S12 builds a second, module-local copy of.** S12's non-export screens (list, tiles,
  decisions, action register) do **not** depend on this fix and may be built and shipped independently; only
  `GET /api/v1/meetings/report/export` is gated on it.

### 23.8 7-language localization

Same resx set shape as every other Meetings screen (§11, §16 "7-language l10n"): a new
`Resources/Views/Meetings/Report/MeetingReportIndex.{en,tr,fr,es,zh,ar,ru}.resx` (or, if the report ships as a
tab-free extension of the existing Meetings list rather than its own folder — an implementation-time call, not
a pack-level one — the SAME shared `MeetingsIndex.*.resx` set already in place, per §5 Frontend/Repo Scope).
Either way: one key set, seven files, `Nav.Page.MEETINGS_REPORT` (or the chosen page code) added to
`SharedResource.*.resx` the same way every other nav-visible Meetings page already was (§19 S3/S8/S11's own
implementation notes each record this same step), and the export's column headers stay **English**
(BL-346's own decision, reused verbatim — §23.7).

### 23.9 Acceptance criteria (testable, phase-tagged for when this becomes ready-for-dev)

- [ ] The report's meeting list, attendance rate, and decisions are all derived from a REQUIRED period; no
      unbounded query path exists (verified: the repository method signature has no default/optional period).
- [ ] The report list for a `read`-only holder contains ONLY meetings where they are organizer or attendee;
      a `read-all` holder's contains every tenant meeting in the period — verified by the SAME three-case test
      shape `MeetingTwoUserFlowMongoTests`-style fixtures already use for D3 (organizer sees it, attendee sees
      it, an unrelated third party's report has zero rows for that meeting).
- [ ] The Action Register lists an action against its origin (`bornFromMeeting`/`preparation` link) meeting,
      never a later continuation it was carried into — verified with a fixture that carries one open action
      through two S7 continuations and asserts it appears exactly once, attributed to the FIRST meeting.
- [ ] "Overdue" is computed against the query-time clock, not the filter period's own end — verified the same
      way `MOD-0024-task-closure-and-reporting.md` §12 item 29 pins ageing-anchor behavior (two different
      "now"s for the same data must not agree).
- [ ] Export refuses (`MEETING_REPORT_EXPORT_TOO_LARGE`) above 50 000 rows rather than truncating; each of the
      three `dataset` values produces a file whose columns match that grain only (no blended file).
- [ ] Export is NOT wired until the shared tenant-side audit writer (§23.7) exists; the non-export screens ARE
      independently testable/shippable without it.
- [ ] `verify_datatable_page.py --area Meetings --module Report --reference compact` PASS, if the Action
      Register sub-list is built as its own DataTables-v2 grid (§23.2's own open implementation-time call).
- [ ] RESX parity PASS across all 7 languages; `Nav.Page.{chosen code}` present in all 7
      `SharedResource.*.resx` files (same guard every prior nav-visible Meetings page already passed).
- [ ] `verify_module_id.py --check-id MOD-0357 --name "Management Review & Cadence"` still exits 0 — S12 opens
      no new identity.

### 23.10 Test gate (same shape as §17, scoped to S12)

- Unit: attendance-rate arithmetic, the open/overdue/closed classification, the origin-attribution rule
  (§23.4) against an S7 fixture with two continuations.
- Integration/HTTP: the D3 visibility three-case matrix (§23.9); cross-tenant isolation (404/empty, same as
  §13); permission boundary for whichever of §23.5's options the owner picks; the batched `RecordLink` →
  `TaskItem` read exercising the REAL `IRecordLinkService` (not a double), same posture §17 already commits
  the rest of this pack to.
- Sabotage proof (this repo's own testing discipline — §17 already commits the pack to this): a
  source-scanning guard proving the Action Register query goes through `IRecordLinkService`, never a raw
  `meeting_record_links` collection read, by temporarily bypassing the service and observing red, then green.
- Frontend smoke: filter → report renders → export (once the audit prerequisite lands) round trip; RESX
  parity; nav-guard.
- Build PASS: `Diten.Platform`, `Diten.Web`, gateway (once routed, §23.11).

### 23.11 Gateway / API routing (decision recorded, not executed)

New routes, same pattern as every other MOD-0357 slice (§15): `GET /api/v1/meetings/report`,
`GET /api/v1/meetings/report/export`. Neither exists in `ocelot.json` today and no catch-all covers them —
required pair, authored by integration-agent, **not** here, once this slice is `ready-for-dev`.

### 23.12 Out of scope

- Scheduled/emailed report delivery (BL-346 excluded the same thing for Work Report; same reasoning: no
  scheduling infrastructure exists for either report today).
- Charts/graphics beyond the summary tiles — BL-346's own Faz 5b shipped charts because MOD-0024 already had
  ApexCharts wired into a tenant page; whether S12 gets charts is an implementation-time UI call, not gated by
  anything this pack decides, and is explicitly NOT required by BL-396's own content list.
- A `Nav.Page`-level "report only" role bundle/preset — a permission-grant UX question, not this slice's.
- Rolling the action register up into a cross-module register (MOD-0024's own Task Center, ESBP's Strategic
  Reviews) — §20 already defers "Connecting Enterprise Strategy's 'Strategic Reviews' screens to this engine"
  as a separate, unbuilt integration; S12 does not open that door.
- Retention/archival of report SNAPSHOTS (as opposed to the underlying meetings/minutes, whose retention is
  already §22 D5) — no snapshot concept exists in this slice; the report is always computed live.

### 23.13 Open questions (owner / Quality decision, numbered)

**Decisions recorded 2026-09-15 (owner accepted all CT recommendations):**
1. Permission model: **Option A** — reuse `platform.meetings.read` / `platform.meetings.read-all`; no new key. Option B stays available as an additive change if a report-only role is ever needed.
2. "Last carried to" column: **yes** — the Action Register shows origin meeting AND the continuation the open action currently sits on (secondary, non-attributing column).
3. Draft-minutes decisions: **no** — only published minutes versions feed the report.
4. Action Register grid: **DataTable v2** (same standard as the work report).
5. Export file retention: CT recommendation "point-in-time, not a controlled copy" — the file carries its generation timestamp and that statement; the export itself is retained as an audit record. **Awaiting Quality confirmation**; implementation follows the recommendation unless Quality answers otherwise.
6. Tenant-side audit writer (BL-347): **built by Infra CT as the shared prerequisite**, consumed by both MOD-0024's work report export and S12's export (WP-PSS-MOD0024-BL347-TENANT-AUDIT-WRITER-01, 2026-09-15).


1. **Permission model (§23.5):** Option A (reuse `read`/`read-all`) or Option B (mint
   `platform.meetings.report.*`)? Recommendation given is A; **owner decides**.
2. **"Last carried to" column (§23.4):** does the Action Register show the current continuation an open
   action's agenda line lives on (a secondary, non-attributing column), or does it stay silent on that and
   show only the origin meeting? **Owner decides** — a UX/scope call, not a data-availability one; the data
   (the newest `agenda`-type `RecordLink` in the chain) is already available either way.
3. **Draft-minutes decisions:** §23.3 excludes a draft's decisions from the report (only published versions
   count). Is that right, or should a QA reader be able to see draft decisions too, clearly marked as
   unpublished? **Owner/Quality decides** — ISO 9001 §9.3.3 expects management-review outputs to be
   *documented*, which a published minutes version already satisfies; a draft is not yet that record.
4. **DataTable v2 for the Action Register (§23.2/§23.9):** built as its own `_DataTable.cshtml` v2 grid
   (paginate/sort/filter via DataTables) or a simpler server-rendered list? **Implementation-time call**,
   flagged here only so the AC checklist item is not silently skipped either way.
5. **Retention of the export file itself** (not the underlying data, already §22 D5): does a downloaded report
   file fall under the same GxP retention discipline as the minutes it summarizes, or is it explicitly
   "point-in-time, not a controlled copy"? **Owner/Quality decides** — GMG-QMS-SOP-0013's own controlled-copy
   language may already answer this; not re-derived here.

**Nothing in §23.1–§23.12 was found to contradict ISO 9001 §9.3.3 (management review inputs/outputs,
including decisions and actions, must be retained as documented information) or GMG-QMS-SOP-0013** — the
slice's own content list (§23.1) is, if anything, a direct implementation of §9.3.3's "documented information"
requirement for the meetings this module already governs; where GMG-QMS-SOP-0013 is silent (draft-decision
visibility, export retention), §23.13 raises the question rather than assuming an answer.
