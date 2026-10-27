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

> **Revision R1 (2026-10-27): draft, not approved.** §24 to §26 add a revision: two-level meeting types,
> create-form auto-fill, the manager's five "before the list loads" items, a one-off type-catalogue importer, the
> deferred record codes, and page help with the Meetings tour. They are new slices **S13 to S24**. **Approval state of
> this revision: draft. It needs independent review (SOP §20.4) and owner approval before any code.** The frontmatter
> `status: ready-for-dev` and the S1 to S12 rows in §19 are unchanged and keep their own approvals. Nothing in R1
> authorizes code, a WP or a queue row. Known record inconsistencies are listed in §24.23 for the CT to reconcile;
> this revision does not fix them.

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
| S3b | Ortak takvim bileşeni (ay/hafta/gün), Görev Merkezi sürükle-planla, toplantılar salt okunur; Toplantılar sayfası ve davet kartları 2c. — Görev Merkezi dilimi (2b) WP-UI-CALENDAR-VIEW-01 ile yapıldı (commit CT'de): `shared/diten-calendar.js` + `shared/diten-zoned-time.js` + `Views/Shared/_CalendarAssets.cshtml` (yerel FullCalendar 6.1.15, CDN yok, yalnız takvim çizen sayfada), eski elle yazılmış `renderCalendar` kaldırıldı; BL-365 #4 bu işle kapanır. Toplantılar sayfasının aynı bileşeni kullanması ve davet kartları → 2c. **2c yapıldı** (WP-UI-MEETINGS-CALENDAR-01, P-2026-09-29-04, commit CT'de): Toplantılar liste ↔ takvim anahtarı (`?view=calendar`, varsayılan liste), takvim = ortak bileşen salt okunur ay/hafta/gün; kayıtlar listeyle aynı küme (liste API'si görünen aralık FromUtc/ToUtc + sayfanın tek süzgeç kuralı `MeetingsList.matchesFilters`, bütün sayfalar okunur, eksik aralık söylenir); kabul/düzenlediğim dolu, yanıt bekleyen kesik, reddedilen ve iptal çizilmez, saat kiracı diliminde, gün gölgesi akışın `days`'inden; tıklama → ayrıntı. Davet kartı tek modül `shared/diten-invite-card.js` — Görev Merkezi Davetler paneli ve Toplantılar "Yanıt bekleyen davetlerim" paneli (sayı ile) onu kullanır; kabulde kendi planlı bloğumla çakışma (akışın `overlapsPlan`, yarı açık) `showConfirm` ile uyarır, engellemez; ret sessiz; davetler sürüklenmez. Akışa okuma anında hesaplanan ek alanlar: toplantıda `overlapsPlan` + `planOverlap`, görevde `warnings[]` (BL-471). `calendarUnresolved` uyarısı iki takvimde tek uygulamadan (`DitenCalendar.unresolvedNotice`). Önceki satır: "Read-only month calendar view on the meetings list … BL-365 #4". Sahip kararı 2026-09-11: takvim evet, kanban hayır (üç durum, akış yok); tasarım 2026-09-17, BL-451 2026-09-29 |
| S4 | Meeting → task (three moments) and task → meeting (`scheduleReviewMeeting` receiving side) — ✅ delivered 2026-09-11, commit `680cb888` (WP-MG-MOD0357-S4-TASK-BRIDGE-01): preparation + on-the-spot moments (minutes-born moment → S6), link existing (agenda), receiving side of scheduleReviewMeeting with RecordLink reviewMeeting; K9 dual permissions, K11 idempotency (app-level), reviewMeetingPolicy projection fixed at optional (no TaskType field invented). Live proof waits for BL-358. |
| S5 | Invitation: ERP-internal accept/decline + Task Center trigger — ✅ PARTIAL 2026-09-11, commit (see git log "invitations — accept or decline") (WP-MG-MOD0357-S5-INVITATION-01): respond endpoint (K5), invite/change/cancel mail via IMeetingInviteMailer (K12, never rolls back), organizer = Accepted attendee, 3 templates × 7 languages. Task Center inbox card NOT done — stop rule hit: WC-1 contract has no `meetingInvite` type / accept-decline actions (app.js glyphs are a trigger-only fixture). → S5c |
| S5c | Task Center invite card — ✅ delivered 2026-09-12, commit `d3fa0058` (WP-MG-MOD0357-S5C-INVITE-CARD-01): WC-1 contract gains the `meetingInvite` intent + `acceptInvite`/`declineInvite` with five shape rules and eight self-tests; `MeetingWorkItemProvider` (3rd provider) + dispatcher onto S5's respond command; inbox card with a visible Decline; showcase fixture retired; CT added the real-Mongo pending-query test. Original scope:  extend the WC-1 contract (`fixture-contract.js`) with item type `meetingInvite` and action codes `acceptInvite` / `declineInvite`; add `MeetingWorkItemProvider` (4th IWorkItemProvider: my Pending invitations → inbox; accepted never becomes an İşlerim item, BL-026) and the inbox card with Accept/Decline calling `/respond`; retire the trigger-only showcase fixture. Open from S5: organizer cannot yet remove own attendee row (no backend constraint); a real invite email needs a second eligible user in dev. |
| S5b | `.ics` invite / change / cancel email — first step: additive `Attachments` field on `MessagingProviderEmailRequest` (CT infra, Notifications); then `IMeetingInviteMailer` attaches the `.ics` (same UID across updates; METHOD:CANCEL on cancel). Stage 1 per ADR-003 §5 — ✅ delivered 2026-09-13, commit `af7e03cc` (WP-MG-MOD0357-S5B-ICS-INVITE-01): additive `Attachments` carried through the synchronous notification chain (nothing persisted), `invite.ics` on invite/change/cancel, UID = meeting id on all three, SEQUENCE = Version, METHOD REQUEST/CANCEL, octet folding. CT correction: display names are DQUOTEd parameter values (RFC 5545 §3.2, RFC 6868), not TEXT-escaped. Both closed by WP-MG-MOD0357-BL374-RETRY-FIDELITY-01 (2026-09-13): a retry replays the stored .ics; an unresolved organizer gets no .ics. Live proof via Mailpit not yet run. |
| S6 | Minutes: draft, publish, correct, decisions-as-rows — ✅ delivered 2026-09-12, commit `23af2e58` (WP-MG-MOD0357-S6-MINUTES-01): append-only `MeetingMinutesVersion`, publish locks and sets the meeting Completed, correction demands a reason and writes v+1 while v stays byte-identical, decisions are server-numbered rows holding a RecordLinkId (never a TaskId), a task opened after publication is labelled "added later" and listed separately, own page not a tab, 7 languages. Real-Mongo test pins the unique version index. |
| S7 | Continuation meeting (K6 carry-forward) — ✅ delivered 2026-09-12, commit `4e1be23d` (WP-MG-MOD0357-S7-FOLLOWUP-01): schedule a follow-up from a closed meeting, `followUp` RecordLink back to the source, `FollowUpOfMeetingId` on the new meeting, idempotency key (resubmit returns the same meeting), a meeting cannot be its own follow-up, 7 languages. **CT correction in the same commit:** carry-forward first counted only the agenda-ANCHORED links, so an open action the meeting itself PRODUCED (on the spot, S4, or born from a published decision, S6) had no agenda line and did not carry — the one carry-forward a management review exists for. Now anchored lines first in the source meeting's own order, then `bornFromMeeting` links by creation time, no task twice; `preparation` stays out on purpose (that work fed INTO the meeting). |
| S8 | Meeting type setting screen — ✅ delivered 2026-09-11, commit `6bab238c` (WP-MG-MOD0357-S8-MEETING-TYPES-01): list/create/edit under `platform.meetings.types-manage`, nav-visible page MEETING_TYPES (7 languages), agenda-template pre-fill on the meeting form (K8 box 1 — code + tests; live proof waits for dev organisation data, BL-358), K8 box 2 flags default off. Also closed the Task Center dialog-helper duplication (BL-365/BL-367). |
| S9 | `reviewMeetingPolicy.required` gate wired end-to-end against a real MOD-0024 task type. **Owner decision 2026-09-13:** the decision action unlocks when the linked review meeting's minutes are PUBLISHED, not when the meeting is merely scheduled (K3 already says so; MOD-0024's checklist-engine pack said "scheduled" and is corrected on the PSS branch). Field delivered on MOD-0024's side: `TaskType.ReviewMeetingRequirement`, commit `b5eac362` (PSS branch). Remaining: the projection reads it and gates approve/signoff. **2026-09-15:** no longer waits for main — the meetings lane contains the task-engine lane (merge chain Auth ⊂ PSS ⊂ meetings), so the field and the minutes are in one tree; implementation WP-MG-MOD0357-S9-REVIEW-GATE-01. **Code 2026-09-15 `a9c40ee0` — live check pending:** `start` is never gated; `complete` and `submitReview` are disabled in the projection and refused by the server (409 REVIEW_MEETING_REQUIRED) until a linked non-cancelled review meeting has PUBLISHED minutes; one reader (`ReviewMeetingGateReader`) and one rule (`ReviewMeetingDecisionGate`). CT sabotage: submit gate skipped → 7 red. |
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

---

## 24. Revision R1 (2026-10-27) — two-level meeting types and the pre-load items (DRAFT)

> **Approval state of this revision: draft. It needs independent review (SOP §20.4) and owner approval before any
> code.** Every slice in §24 (S13 to S24) is **draft / not approved**. No WP may cite one of them, `@orchestrator` may
> not run one of them, and no queue row may be opened for one of them until the owner approves this revision. The
> existing `ready-for-dev` status of S1 to S12 (§19, §23) does not change. This file now has a new SHA-256, so any
> approval row bound to the old version no longer covers it (CT-APR-01). The new version needs its own review row and
> its own approval row (`scripts/pack_approval.py`).

### 24.0 Revision log

| Rev | Date | Change | Author · model · effort | Owner instruction |
|---|---|---|---|---|
| R1 | 2026-10-27 | Draft revision. Adds a header note under "Authority", §24 (slices S13 to S24, model decisions, record codes, record inconsistencies, open questions), §25 Audited Events (required by AUD-001 because R1 adds write commands and changes existing ones) and §26 Teslim dilimleri (CT-APR-02). §1 to §23 are not edited. | `module-pack-author` subagent working for CT · Toplantılar. **Model · effort requested (SOP §17.1 r5): Opus · high.** Used: `claude-opus-5-5`. The effort level is set by the dispatcher and cannot be measured from inside the session. | "paket-rev" (owner, 2026-10-27): draft the revision only. Do not commit, approve or write code. |

### 24.1 Sources of the decisions (read; cited as path@commit)

| Source | What it decides for R1 |
|---|---|
| `docs/roadmap/plans/post-closure-program-2026-10-12.md@826150ef4` §5.1, §5.2, §5.5, §5.6 | The manager's catalogue (23 main types, 46 subtypes, per-subtype default fields), the measured gaps (three type flags that "do nothing today"), and the create-form complaint (no agenda or repeat on create; `form.js` applies the agenda after creation). |
| same file §5.7 (**binding**, manager and owner, 2026-10-12 evening) | "Ana tür + alt tür". One default recurrence. Parent rule is None / previous meeting in the series / user selects. Attendance mandatory means no publish until every invitee is marked present, absent or excused. "Owner / Due date / Action items" are not agenda lines. Invitations are by position. The **five software items before the list loads**: confidentiality, mandatory attendance, per-agenda-item note and outcome, honest display of the flags, mandatory agenda items. No 32 new action task types. |
| `docs/roadmap/plans/meetings-mod0357-work-list-2026-10-27.md@2904f4029` (Kısa liste A4 and A7, the "Toplantı türleri" block, §5 Dalga 3/4/5) | Slice content and estimates; A7 (page help and tour before use). The **§6 item 6a** (expected manager document; meeting type is RCS-001 İKİSİ DE with prefix `MTY`; no code field until BL-565) is **only in the uncommitted worktree copy** of this file. It is not in `2904f4029`. See §24.27. |
| `docs/records/decisions/2026-09/ADR-003-meetings-are-mod-0357-now-and-live-in-platform.md@826150ef4` | Identity, hosting in `Diten.Platform`, `meeting_*` collections, `platform.meetings.*`, no `TaskItem` field, e-signature deferred. |
| `.antigravity/rules/record-coding-standard.md@826150ef4` (RCS-001 §2, §3, §5.3, §5.5, §5.12, §6) | Record-code classes, prefix uniqueness, group prefix, and the rule that no module may keep its own counter. |
| `.antigravity/rules/page-help-and-keyboard.md@826150ef4` (UXH-001 §2, §5, §6) | Help keys, the ledger that only shrinks, "Toplantı" as one of the five tour flows, and `data-tour` targets. |
| `docs/roadmap/backlog/page-help-ledger.md@826150ef4` lines 134 to 140 | The seven meeting pages that have no help text today. |
| Code @826150ef4 (`2904f4029` is a docs-only commit on top of it) | `Domain/Entities/Meetings/*.cs`, `Domain/Enums/Meetings/MeetingEnums.cs`, `Application/Features/Meetings/**`, `Infrastructure/Services/MeetingInviteMailer.cs`, `Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs`, `frontend/Diten.Web/Controllers/MeetingsController.cs`, `wwwroot/assets/js/Meetings/**`, `wwwroot/assets/js/shared/diten-tour-catalog.js`, `tests/architecture/audit-ledger/Diten.Platform.md`, `gateway/Diten.ApiGateway/ocelot.json`. |
| DCP-002 preflight, run 2026-10-27 | `verify_module_id.py . --check-id MOD-0357 --name "Management Review & Cadence"` → `OK MOD-0357: proven against Blueprint/registry.` R1 opens no new identity. |

### 24.2 Scope of R1

**In scope (one revision, slices S13 to S24):**

- **A. Two-level meeting type.** A new tenant-scoped **main type** (`MeetingCategory`, collection
  `meeting_categories`). Today's `MeetingType` row becomes the **subtype** and gets a reference to its main type, plus
  new default fields. `Daily` is added to the series frequency. The type screen is grouped by main type, and so is the
  type picker on the create form.
- **B. Create-form auto-fill.** Choosing a subtype fills title, type, place, end time, description, participants,
  the follow-up-of field and the agenda. The agenda is written **at create time** (`CreateMeetingRequest` gains
  agenda items). A "Repeat" option creates the meeting through the existing path and then a series that takes that
  meeting as its first instance. This adds no third creation path (§19 S11).
- **C. The manager's five pre-load items:** confidential meeting type, mandatory attendance, per-agenda-item note
  and outcome, honest display of the flags, and mandatory agenda items for quality-record types.
- **D. A one-off catalogue importer** for main types and subtypes. It is idempotent, has a dry-run preview and is
  audited.
- **E. Record codes (RCS-001).** These are recorded and deferred. No slice builds them (§24.20).
- **F. Page help** for the seven meeting pages, and the Meetings tour.

**Out of scope of R1 (named so that no slice takes them in):**

- A review meeting that covers several tasks, and a per-task outcome gate. These touch MOD-0024's gate
  (`ReviewMeetingDecisionGate`, `Features/Tasks/**`), which is plan §5.5 item 2 and work list Dalga 6.
- Task↔meeting links on the calendars (Dalga 7).
- Availability warnings and "the person's time" (Dalga 8). §24.11 only records the binding "busy only" rule for
  confidential meetings.
- Minutes PDF and distributing the minutes by e-mail (Dalga 9.1, 9.2).
- Attachments and the pre-read pack (Dalga 9.5).
- Binding the minutes to e-signature (MOD-0022, Dalga 9.6).
- Google Meet, Google Calendar and Teams integration (Dalga 10.1, 10.2). A link pasted into Location already reaches
  the `.ics` `LOCATION` line (`MeetingIcsBuilder.cs`).
- External attendees and room booking.
- Loading the task-type catalogue (MOD-0024 data, Dalga 2). R1 never writes task-type data.
- **The meeting catalogue data itself.** It is prepared separately and is not part of any slice. S24 builds the tool
  only.
- The visible record-code field and the meeting-instance identity number (§24.20).
- Agenda text inside the invite e-mail body or the `.ics` `DESCRIPTION`. The mail template seed is shared Notifications
  data (`Infrastructure/Persistence/Configurations/NotificationTemplateSeed.cs`), so this goes to follow-up §24.24.

### 24.3 Model decisions proposed by R1 (each needs owner approval with the revision)

| # | Decision | Reason |
|---|---|---|
| R1-D1 | **Main type = `MeetingCategory`** (new entity, `TenantScopedEntity`, collection `meeting_categories`, soft delete). **Subtype = today's `MeetingType`** (same entity, same collection `meeting_types`, same ids). UI terms: en "Main type" / "Subtype", tr "Ana tür" / "Alt tür" (all 7 languages in resx). | §5.7 "Model: ana tür + alt tür" and §5.6 "Alt tür = bugünkü `MeetingType` satırı". Keeping the subtype as the existing entity means no id changes for any existing `Meeting.MeetingTypeId`, `MeetingSeries.MeetingTypeId` or report filter. |
| R1-D2 | **Migration and backfill: none.** `MeetingType.MeetingCategoryId` is stored as nullable. Existing rows keep `null` and are **never rewritten or deleted at deploy**. Wherever a row with `null` is listed (type screen, type picker, report filter), it appears under an **"Unassigned"** group (localized, sorted last). From R1 on, every create and every update of a subtype **requires** a main type. An admin assigns one to a legacy row by editing it, or in bulk through the importer (S24-5). Existing meetings of an unassigned subtype keep working unchanged. | Two alternatives were rejected. (a) A seeded "General" main type with a backfill invents tenant data and writes to every existing row, and a wrong group would then look intentional. (b) Refusing to create meetings with unassigned subtypes would break the module for every tenant on the day of deploy. "Unassigned" is honest, it is visible, and it disappears on its own once the catalogue is loaded. |
| R1-D3 | **Subtype names stay tenant-unique.** The existing unique index `ux_meeting_types_tenant_name` `(TenantId, Name, IsDeleted)` (`PlatformSchemaManifest.Meetings.cs`) is **not changed**, so the same subtype name cannot exist under two main types. | No index migration is needed. Picker, report and export show the subtype name alone without ambiguity. The importer reports a name clash as a conflict and never moves a row (S24-5). |
| R1-D4 | The catalogue **does not store start or end times**. It stores a default duration. End = Start + default duration. | §5.6 "Ekranda doldurma" and the work list "Toplantı türleri" block. |
| R1-D5 | **Confidentiality ratchet.** A meeting copies `IsConfidential` from its subtype when it is created. The **effective** value is `Meeting.IsConfidential OR (current subtype).IsConfidential`. No R1 command ever lowers `Meeting.IsConfidential`: when the type changes on update, the new value is the old value OR the new type's flag. | Switching a type to confidential protects that type's past meetings at once. Switching it off does not expose meetings that were held under confidentiality. |
| R1-D6 | **Agenda at create.** `CreateMeetingRequest.AgendaItems`: `null` means the server applies the subtype's agenda template; `[]` means no template lines; a list means exactly those lines. Mandatory lines (S18) are always added by the server. | The UI sends the edited list. Callers without a UI (series sweep, follow-up) send `null` and get the standard agenda (Q7). |
| R1-D7 | **Repeat without a third creation path.** The meeting is created by `CreateMeetingCommand`. The same handler then sends the existing `CreateMeetingSeriesCommand` with `CreatedFromMeetingId`. The series **adopts** that meeting as occurrence 0, so the sweep never generates it again. | §19 S11: "No third creation path". The adopted meeting is a normal meeting, and later instances go through S7 or S11 exactly as today. |
| R1-D8 | **Title placeholders.** The automatic ones are `{Period}`, `{Week}`, `{Month}`, `{Quarter}`, `{Year}` and `{Date}`. They are rendered in **invariant ASCII** formats in the **tenant time zone**: `{Date}` = `2026-10-27`, `{Week}` = `W44` (ISO 8601), `{Month}` = `2026-10`, `{Quarter}` = `Q4`, `{Year}` = `2026`. `{Period}` follows the recurrence: weekly → `2026-W44`, monthly → `2026-10`, quarterly → `2026-Q4`, yearly → `2026`, daily or none → the date. Any other `{Name}` (such as `{Manager}` or `{Initiative}`) is filled by the user before saving. | The text does not depend on language for a 7-language tenant. Each series instance gets its own value (§5.6 item 6). |
| R1-D9 | **Participants by position.** The subtype stores position ids. The create form resolves them to the **current holders** through the attendee eligibility the module already uses (`MeetingEligibility.EligibleUserIdsAsync` → `GetTaskAssignmentPersonLookupQuery(Decision)`, whose rows carry `PositionId`). No new directory seam is added. Person-dependent roles ("Line Manager / Employee") are not stored as rules. The subtype carries a free-text `ParticipantSelectionHint` that is shown next to the picker. A series created by Repeat stores the resolved people (a snapshot, as `MeetingSeries.AttendeeUserIds` does today). | §5.6 item 2 and §5.7 "Davet pozisyon bazlı". Re-resolving positions for each series instance is follow-up Q10. |
| R1-D10 | **Honest flags through one server switch.** A single constant set (`MeetingCapabilities`) says which flag effects are active. The UI renders "not yet active" from it, and the slice that makes an effect active changes the constant. | The display cannot drift from the code. Pre-load item 4 can ship first (A4 in the work list). |
| R1-D11 | **No new permission key.** Main-type CRUD, the importer and the positions lookup use `platform.meetings.types-manage`. The auto-fill lookups use `platform.meetings.create` / `.read`. Repeat uses `platform.meetings.series-manage` (Q9). | The brief says no new permission family. Every endpoint is mapped in §24.6. |
| R1-D12 | **Audit.** New commands use path `a`. An existing **non-minutes** command that an R1 slice changes moves to path `a` in that same slice, and its line leaves the known-debt list, which only shrinks. The three minutes commands stay in known debt until the CT classifies them for K2 (Q14). Details in §25. | AUD-001 §10.1 transition rule and §4.4. |

### 24.4 Entity and field additions (R1)

All new entities are `TenantScopedEntity`, live in Mongo, use soft delete, inherit `TenantRepository<T>` and have
tenant-first indexes declared in `Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs`. Every new
field on an existing entity is **additive**: rows written before R1 deserialize with the default shown.

**`MeetingCategory` (new, collection `meeting_categories`, S19)**

| Field | Type | Required | Rule |
|---|---|---|---|
| `Name` | string | Yes | trim, max 200, tenant-unique. Unique index `ux_meeting_categories_tenant_name` `(TenantId, Name, IsDeleted)` plus a pre-check. |
| `Description` | string? | No | max 1000 |
| `SortOrder` | int | Yes | 0..9999, default 0 |

**`MeetingType` (the subtype) — additions**

| Field | Type | Default for old rows | Slice | Rule |
|---|---|---|---|---|
| `MeetingCategoryId` | Guid? | null ("Unassigned") | S19 | Required on create and update from R1 on. Must resolve to a non-deleted category in the tenant. |
| `TitleTemplate` | string? | null | S19 | max 200. Placeholders `{Letters}`. Unbalanced braces are refused. |
| `DefaultDurationMinutes` | int? | null | S19 | 5..1440 |
| `DefaultRecurrenceFrequency` | `MeetingSeriesFrequency?` | null | S19 | null = one-off or event-based ("As needed", "Per release", "Before launch"…). `Daily` is accepted only after S21. |
| `DefaultRecurrenceInterval` | int? | null | S19 | 1..52 when a frequency is set, otherwise null |
| `DefaultLocation` | string? | null | S19 | max 500, free text (a pasted Teams or Meet link goes into `.ics` `LOCATION`) |
| `DefaultParticipantPositionIds` | List\<Guid\> | [] | S19 | ≤ 20. Each must be a tenant position at write time (`GetPositionsQuery`). |
| `ParticipantSelectionHint` | string? | null | S19 | max 500, shown on the create form |
| `DescriptionTemplate` | string? | null | S19 | max 4000 |
| `ParentMeetingRule` | `MeetingParentRule` | `None` | S19 | new enum `None = 0`, `PreviousInSeries = 1`, `UserSelects = 2` |
| `SortOrder` | int | 0 | S19 | 0..9999 |
| `IsConfidential` | bool | false | S15 | set by `types-manage` |
| `MandatoryAgendaItems` | List\<string\> | [] | S18 | ≤ 20 lines, each ≤ 200. Allowed only when `IsQualityRecord`. A line may not also be in `AgendaTemplate`. |

The existing fields (`Name`, `AgendaTemplate`, `DefaultActionTaskTypeId`, `IsQualityRecord`, `RequiresESignature`,
`AttendanceMandatory`) are unchanged. As before, the catalogue's single default action task type goes into
`DefaultActionTaskTypeId`.

**Other additions**

| Entity · field | Type | Default for old rows | Slice | Rule |
|---|---|---|---|---|
| `Meeting.IsConfidential` | bool | false | S15 | Set from the subtype on create. Never lowered (R1-D5). |
| `AgendaItem.IsMandatory` | bool | false | S18 | Set only by the server from `MandatoryAgendaItems`. |
| `MeetingMinutesVersion.AgendaNotes` | List\<`MinutesAgendaNote`\> (embedded) | [] | S17 | `{ AgendaItemId, AgendaTextSnapshot, Note ≤ 4000, Outcome ≤ 1000 }`, ≤ 100 rows |
| `MeetingSeries.TitleTemplate` | string? | null | S21 | max 200, automatic placeholders only |
| `MeetingSeries.CreatedFromMeetingId` | Guid? | null | S23 | Sparse unique index `(TenantId, CreatedFromMeetingId)`, so one meeting can be adopted by at most one series. |
| `MeetingSeriesFrequency.Daily` | enum value `= 4` | — | S21 | **Appended. Existing values are never renumbered** (stored as int). This supersedes the enum's own comment "No `Daily` value" by owner decision §5.7. |

### 24.5 Validation rules for the R1 fields (§12 shape)

| Field | Required | Format/Rule | DB-level | Pre-check / reason code |
|---|---|---|---|---|
| `MeetingCategory.Name` | Yes | trim, max 200, tenant-unique | unique index | `ExistsByNameAsync` → 409 `MEETING_CATEGORY_NAME_DUPLICATE` |
| `MeetingCategory` delete / bulk delete | — | refused while any non-deleted subtype references it | — | 409 `MEETING_CATEGORY_IN_USE` (per row in bulk) |
| `MeetingType.MeetingCategoryId` | Yes (write) | resolves in-tenant, not deleted | — | 400 `MEETING_CATEGORY_REQUIRED` / 400 `MEETING_CATEGORY_NOT_FOUND` |
| `MeetingType.TitleTemplate` | No | max 200, balanced braces, `{[A-Za-z]+}` placeholders | — | 400 `MEETING_TYPE_TITLE_TEMPLATE_INVALID` |
| `MeetingType.DefaultDurationMinutes` | No | 5..1440 | — | validator |
| `MeetingType.DefaultRecurrence*` | No | interval 1..52 if frequency is set, null otherwise | — | validator |
| `MeetingType.DefaultParticipantPositionIds` | No | ≤ 20, distinct, each a tenant position | — | 400 `MEETING_TYPE_POSITION_NOT_FOUND` |
| `MeetingType.MandatoryAgendaItems` | No | ≤ 20 × ≤ 200; only if `IsQualityRecord`; not repeated in `AgendaTemplate` (trimmed, case-insensitive) | — | 400 `MEETING_TYPE_MANDATORY_AGENDA_REQUIRES_QUALITY_RECORD` / 400 `MEETING_TYPE_AGENDA_LINE_DUPLICATED` |
| `CreateMeetingRequest.AgendaItems` | No | ≤ 50 lines, each trim and ≤ 500 | — | validator |
| `Meeting.Title` (create/update) | Yes | no unfilled `{Name}` placeholder left | — | 400 `MEETING_TITLE_PLACEHOLDER_UNFILLED` |
| `SaveMinutesDraftRequest.AgendaNotes[]` | No | `AgendaItemId` belongs to the meeting; no duplicate ids; Note ≤ 4000; Outcome ≤ 1000; a row with both empty is dropped | — | 400 `MEETING_AGENDA_ITEM_NOT_FOUND` / 400 `MEETING_MINUTES_AGENDA_NOTE_DUPLICATE` |
| Publish / correct (attendance-mandatory type) | — | every `MeetingAttendee` row is marked exactly once | — | 409 `MEETING_MINUTES_ATTENDANCE_INCOMPLETE` |
| Publish / correct (meeting with mandatory lines) | — | every mandatory line has a non-empty Outcome (Q6) | — | 409 `MEETING_MINUTES_MANDATORY_OUTCOME_MISSING` |
| Agenda line edit/delete (mandatory) | — | text change or delete is refused; reorder is allowed | — | 409 `MEETING_AGENDA_ITEM_MANDATORY` |
| `MeetingSeries.TitleTemplate` | No | max 200, automatic placeholders only | — | 400 `MEETING_SERIES_TITLE_PLACEHOLDER_NOT_AUTO` |
| Importer file | Yes | UTF-8 JSON, ≤ 1 MB, ≤ 200 rows, schema in S24 | — | 400 `MEETING_TYPE_IMPORT_FILE_INVALID` (row + field named) |
| Importer commit | — | plan hash equals the preview's | — | 409 `MEETING_TYPE_IMPORT_PLAN_CHANGED` |

Every new reason code is added to `MeetingReasonCodes` and to the frontend reason-code bridge, with text in all 7
languages (`tests/meetings-reason-code-bridge.test.js` must stay green).

### 24.6 Rules every R1 slice follows

- **Golden Reference.** The pack is `golden_reference: compact`. The new main-type screen has 3 form fields, so it is
  **Slim** (module-pack-standard §3: 8 or fewer fields → slim). New backend types follow the GR naming and use **one
  public type per file** (`Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`,
  `Validators/`). Handlers are `{Verb}{Module}Handler` with no Command or Query suffix. The existing bundled files
  (`MeetingCommands.cs`, `MeetingCommandHandlers.cs`, …) are a recorded deviation (§24.23 item 10). R1 does not
  refactor them and does not add new types into them.
- **Layout.** Every new or edited `.cshtml` states `Layout = "_LayoutTenantShell";` explicitly.
- **Permissions (existing keys only, PKS-001):**

  | Endpoint (backend `api/v1/meetings/…`) | Key |
  |---|---|
  | `GET/POST categories`, `GET/PUT/DELETE categories/{id}`, `POST categories/bulk-delete` | `platform.meetings.types-manage` |
  | `GET lookups/positions` | `platform.meetings.types-manage` |
  | `POST types/import/preview`, `POST types/import` | `platform.meetings.types-manage` |
  | `GET lookups/types` (now grouped, more fields) | `platform.meetings.read` (unchanged) |
  | `GET lookups/type-default-attendees` | `platform.meetings.create` |
  | `GET lookups/previous-meeting` | `platform.meetings.read` |
  | `POST /` with `repeat` | `platform.meetings.create`; the series part also needs `platform.meetings.series-manage` (Q9) |

- **Tenant isolation.** `TenantId` is resolved on the server and never accepted from the client. A category,
  position, task type, meeting or series id from another tenant behaves as not found (404 / 400 `…_NOT_FOUND`). The
  importer reads and writes only the caller's tenant. Every new collection has tenant-first indexes.
- **Localization.** Every user-facing string is in all 7 languages (en, tr, fr, es, zh, ar, ru) in the screen's resx
  set, `PageHelpResource` and `SharedResource` (`Nav.Page.*`). RESX parity must pass and no raw key may render.
- **Help text upkeep (UXH-001 §2).** A slice that changes what a page does updates that page's `Help.{KOD}.*` text in
  all 7 languages in the same slice. A slice that adds a page brings that page's text, so the ledger never grows. A
  slice that redraws a page keeps its `data-tour` marks (`tests/diten-tour-targets.test.js`).
- **UAS-001.** A control the reader cannot use is not rendered. A page without permission draws no skeleton, no help
  and no tour.
- **UXH-001 §3.** Every new offcanvas primary button carries `data-dt-primary`: `enter` for create and edit, `chord`
  for delete.
- **MOD-0024 boundary (§6).** `TaskItem` gets no field. `Features/Tasks/**` is not edited. Data from it is consumed
  only through existing mediator queries: `GetTaskAssignmentPersonLookupQuery`, `GetActiveTaskTypesQuery`, and
  (TenantOrganization) `GetPositionsQuery`.
- **Web proxy.** Measured: `frontend/Diten.Web/Controllers/MeetingsController.cs` has one explicit action per
  endpoint and no catch-all, so every new backend endpoint needs a proxy action in the same slice.
- **Sabotage proof.** Every slice names one guard and shows it red with the rule removed, then green again.
- **No commit to `main`.** Stage files by explicit path only (GIT-002).

### 24.7 Slice order and estimates

Prompt counts are **estimates** (one prompt ≈ one development session). They are not commitments.

| Order | Slice | Title | Estimate (prompts) | Depends on | DL (evidence level in §26) |
|---|---|---|---|---|---|
| 1 | S13 | Page help and Meetings tour | 2 (estimate) | — | DL-13 |
| 2 | S14 | Honest display of the type flags | 1 (estimate) | — | DL-14 |
| 3 | S15 | Confidential meeting type | 2 (estimate) | — | DL-15 |
| 4 | S16 | Mandatory attendance before publishing | 1 (estimate) | S14 (switch flip only) | DL-16 |
| 5 | S17 | Note and outcome per agenda item | 2 (estimate) | — | DL-17 |
| 6 | S18 | Mandatory agenda items for quality-record types | 1–2 (estimate) | S17 (for S18-5), S15 (type commands audited), S14 (switch flip) | DL-18 |
| 7 | S19 | Main type and subtype default fields (backend) | 2 (estimate) | — | DL-19 |
| 8 | S20 | Type screens and grouped type picker | 2 (estimate) | S19; S15 and S18 if their fields are to be shown | DL-20 |
| 9 | S21 | Daily frequency and series title template | 1–2 (estimate) | — | DL-21 |
| 10 | S22 | Create-form auto-fill and agenda at create | 2 (estimate) | S18, S19, S20, S21 (shared title renderer) | DL-22 |
| 11 | S23 | Repeat on create | 2 (estimate) | S21, S22 | DL-23 |
| 12 | S24 | One-off type catalogue importer | 1–2 (estimate) | S19, S20 (S15 and S18 for the flags it imports). The real import run also waits on the task-type catalogue load (MOD-0024, Dalga 2) and on the QA process owner's name (§5.7). | DL-24 |

**Total: about 19 to 22 prompts (estimate).** Why this order differs from §5.7's order: S13 and S14 come first
because the short list puts them **before use** (A7, A4), and S14's switch lets the honest text ship before the
effects it describes. S15 to S18 are the five pre-load items in the manager's order. S19 to S24 follow "type model →
auto-fill → catalogue load" from §5.7 rows 3 and 4. Inside that order, any slice whose "Depends on" cell is empty may
be pulled forward.

### 24.8 S13 — Page help and Meetings tour

**Status: draft / not approved.** UXH-001 §2, §5, §6; work list A7.

**What the user gets.** The "?" panel on the seven meeting pages explains what the page is for, who uses it and how
(steps), with links to related pages. A guided tour runs on the Meetings list and on the meeting detail page.

**Files / areas.** `frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` ·
`frontend/Diten.Web/Services/Help/PageHelpCatalog.cs` (related pages, step icons) ·
`docs/roadmap/backlog/page-help-ledger.md` (remove the rows `MEETINGS`, `MEETINGCREATE`, `MEETINGDETAIL`,
`MEETINGEDIT`, `MEETINGTYPES`, `MEETINGSERIES` and `MEETINGREPORT`, which are lines 134 to 140 at `826150ef4`, and
lower the header "kiracı" ceiling by 7) · `wwwroot/assets/js/shared/diten-tour-catalog.js` ·
`Views/Meetings/Index.cshtml`, `_Filter.cshtml`, `Details.cshtml` (`data-tour` marks), and
`wwwroot/assets/js/Meetings/index.js` only if a target is drawn by script.

**Help content outline (English step titles; the implementer writes the full text in 7 languages):**

| Page code | Purpose (gist) | Steps (titles) | Related (only if the reader can open it) |
|---|---|---|---|
| `MEETINGS` | Meetings you organize or attend; every meeting with "View All Meetings" | Find a meeting · Switch to the calendar · Answer invitations · Create a meeting · Open a meeting | `MEETINGTYPES`, `MEETINGSERIES`, `MEETINGREPORT`, Task Center |
| `MEETINGCREATE` | Schedule a meeting and invite people | Choose the type · Set the time · Invite people · Place or link (a Teams or Meet link goes into the calendar invite) · Save | `MEETINGS`, `MEETINGTYPES` |
| `MEETINGDETAIL` | Run one meeting: agenda, attendees, tasks, minutes, follow-up | Agenda · Attendees and answers · Open a task from the meeting · Write the minutes · Schedule the follow-up | `MEETINGS`, `MEETINGREPORT` |
| `MEETINGEDIT` | Change time, place or people (attendees get a change e-mail) | Change the time · Change place or description · Cancel instead of delete | `MEETINGDETAIL` |
| `MEETINGTYPES` | What a meeting type sets (agenda template, default action task type, flags) | Create a type · Agenda template · Default action task type · Flags (with S14's honest note) | `MEETINGS` |
| `MEETINGSERIES` | Recurring meetings generated ahead of time | Create a series · Frequency and lead time · Follow-up chaining · Background generation (needs the background job, BL-536) | `MEETINGS`, `MEETINGTYPES` |
| `MEETINGREPORT` | Attendance, decisions and open or overdue actions for a period | Choose the period · Read the tiles · Decisions · Action register · Export (a snapshot, not a controlled copy) | `MEETINGS` |

**Tours (UXH-001 §5; "Toplantı" is one of the five flows):**

| Tour key | Version | `data-tour` targets (in order) |
|---|---|---|
| `MEETINGS` | 1 | `mt-create`, `mt-view-switch`, `mt-filter`, `mt-invites`, `mt-list` |
| `MEETINGDETAIL` | 1 | `mtd-agenda`, `mtd-attendees`, `mtd-add-task`, `mtd-minutes`, `mtd-follow-up` |

This uses two page tours for one flow. That is the same pattern as Time (`MYTIMESHEET` + `TIMEAPPROVALS`).

**Acceptance criteria**
- [ ] S13-1 — `Help.{KOD}.Purpose`, `.Audience`, `.Step{n}` and `.Step{n}Title` (1 ≤ n ≤ 5, no gaps) exist for the
      seven codes in all 7 `PageHelpResource` files, and the key sets are equal across languages.
- [ ] S13-2 — The seven ledger rows are removed and the header ceiling is lowered by exactly 7 (246 → 239 at
      `2904f4029`; recompute against the slice's own base). `tests/page-help-ledger.test.js` is green.
- [ ] S13-3 — For every related page written in `PageHelpCatalog`, `Help.{KOD}.Related.{REL}` exists in 7 languages.
      A related page the reader cannot open is not shown (menu permission rule).
- [ ] S13-4 — `diten-tour-catalog.js` registers `MEETINGS` and `MEETINGDETAIL`. Every target exists in the page
      markup (`tests/diten-tour-targets.test.js` green), and `Tour.{CODE}.Step{n}.Title|Text` exist in 7 languages.
- [ ] S13-5 — For a reader without `platform.meetings.create`, the tour skips the `mt-create` step without an error.
      Measured in the browser with a read-only user.
- [ ] S13-6 — No help sentence describes behavior that is not in the code at the slice's base commit. For example,
      `MEETINGCREATE` does not say the agenda is set on the form until S22 lands. The reviewer checks each step against
      the page.
- [ ] S13-7 — The help code that the minutes editor page (`/Meetings/{id}/Minutes`, which has no manifest page code
      today) resolves to is measured and written in the slice report. If it is `MEETINGDETAIL`, that text contains a
      minutes step.
- [ ] S13-8 — The module tour's owner checklist has three lines: Enter saves, the "?" text is right, and the tour
      runs end to end (UXH-001 §6).

**Failure paths.** A language missing → the RESX parity test is red. A target removed from the markup → the tour
target test is red. A denied page → no help and no tour (`CurrentPage.MarkAccessDenied`).

**Tests.** The existing guards (`page-help-ledger`, `diten-tour-targets`, `help-panel-v2`, RESX parity). Sabotage:
delete one `data-tour` mark, then the tour target test goes red. Delete one `Help.MEETINGS.Step3Title` in `ar`, then
parity goes red. Restore both and they go green.

**L10n.** 7 languages: `PageHelpResource` only. **Permissions:** none new. **Tenant isolation:** not applicable
(static text). **Depends on:** nothing. **Estimate:** 2 prompts (text and catalog; tour, marks and l10n review).

### 24.9 S14 — Honest display of the type flags

**Status: draft / not approved.** Pre-load item 4 (§5.7). Work list A4.

**What the user gets.** The type screen, the meeting detail page and the minutes editor say plainly which flags
work today. "Attendance mandatory: not yet enforced" (until S16). "Quality record: recorded only — mandatory agenda
items not yet enforced; the retention rule (§22 D5) is not active" (the first half until S18, the second half stays).
"E-signature required: not yet active — these minutes are not electronically signed; the e-signature service
(MOD-0022) is not connected to minutes." The module claims no regulatory compliance anywhere.

**Files / areas.** New `Features/Meetings/Services/MeetingCapabilities.cs`, which holds one static set of four
booleans: `AttendanceMandatoryEnforced`, `MandatoryAgendaEnforced`, `RetentionRuleActive` and `ESignatureConnected`,
all `false`. `MeetingModels.cs` adds a `Capabilities` object to `MeetingTypeDto`, `MeetingTypeLookupItemDto` and the
meeting detail DTO. Also `Views/Meetings/MeetingTypes/_Form.cshtml`, `_DataTable.cshtml`, `types/*.js`,
`Views/Meetings/Details.cshtml`, `MinutesEditor/Index.cshtml`, and the resx sets (MeetingTypes, MeetingsIndex,
MinutesEditor) in 7 languages.

**Acceptance criteria**
- [ ] S14-1 — The type endpoints and the type lookup return a `capabilities` object read from `MeetingCapabilities`.
      No JS or Razor file hard-codes an effect state (source scan).
- [ ] S14-2 — With all four switches `false`: the type form shows a "not yet active" note next to each of the three
      checkboxes, and the type list shows a "not active" badge on each flag that is ticked but not enforced.
- [ ] S14-3 — The detail page and the minutes editor of a meeting whose type has `RequiresESignature = true` show
      "E-signature: not yet active — these minutes are not signed", and no "signed" or "verified" badge exists in any
      Meetings view.
- [ ] S14-4 — No Meetings resx string in any of the 7 languages contains `Part 11`, `21 CFR`, `Annex 11` or a
      "compliant" claim about signatures (source scan test with a word list per language).
- [ ] S14-5 — Setting one switch to `true` in a test changes the rendered text without any other edit (one C#
      mapping test and one JS test, each fed both states).
- [ ] S14-6 — The three checkboxes stay editable by `types-manage`, so QA may pre-set flags. Saving them changes no
      behavior beyond what the switches state.

**Failure paths.** A response without `capabilities` (an older API) → the UI shows "not yet active". It never falls
back to "active". Without `types-manage` → no type pages (403, UAS-001). The note on the detail page is shown to
anyone who may view the meeting.

**Tests.** C# mapping test; JS state test; resx word scan. Sabotage: render the e-signature note from a hard-coded
`false` in JS, then the switch test is red. Add "Part 11" to one resx, then the scan is red.

**L10n.** 7 languages: about 8 new keys. **Permissions:** no change. **Tenant isolation:** the capabilities are
global constants with no tenant data. **Depends on:** nothing. S16 and S18 each set their own switch to `true` in
their own slice. **Estimate:** 1 prompt.

### 24.10 S15 — Confidential meeting type

**Status: draft / not approved.** Pre-load item 1 (§5.7). Plan §5.4 ("yalnız meşgul"). Needed before the HR
one-to-one subtype is used (work list B5: "İK birebiri kullanılacaksa A'ya çıkar").

**What the user gets.** A type can be marked **confidential** (for example HR One-to-One). Its meetings are visible
only to the organizer and the attendees. A "View All Meetings" (`read-all`) holder who is neither does not see them
in the list, detail, minutes, report or export, and not as a task's related record. Stored invite e-mails carry the
subtype name, not the meeting title.

**Files / areas.**
- `Domain/Entities/Meetings/MeetingType.cs` (`IsConfidential`) and `Meeting.cs` (`IsConfidential`).
- `MeetingModels.cs`.
- `MeetingCommandHandlers.cs`: `MeetingEligibility.CanView` gains the effective-confidential input, and
  create/update apply the snapshot rule (R1-D5).
- `MeetingQueryHandlers.cs`, `MeetingReportQueryHandlers.cs` (report and export).
- `RecordLinks/MeetingRelatedRecordResolver.cs` (gains `ICurrentUserContext`).
- `Infrastructure/Services/MeetingInviteMailer.cs` and `Services/MeetingIcsBuilder.cs` (title input).
- Type create/update validators; type and meeting views and JS (checkbox; lock badge); resx in 7 languages.
- Audit: `CreateMeetingTypeCommand`, `UpdateMeetingTypeCommand`, `CreateMeetingCommand` and `UpdateMeetingCommand`
  move to path `a`. Each becomes its own file under `Commands/` if moving it out of the bundled file is needed to add
  the interfaces. Their lines are removed from `tests/architecture/audit-ledger/Diten.Platform.md` (§25).

**Acceptance criteria**
- [ ] S15-1 — `MeetingType.IsConfidential` (default `false`) can be set through type create/update under
      `types-manage`. Rows written before R1 read `false`.
- [ ] S15-2 — `Meeting.IsConfidential` = the subtype's flag at create. On `UpdateMeeting`, it becomes `old OR newType`.
      No command in R1 lowers it (unit test over all meeting write handlers).
- [ ] S15-3 — The effective flag (`Meeting.IsConfidential || currentType.IsConfidential`) is an input of the single
      predicate `MeetingEligibility.CanView`. When it is true, view is granted only to the organizer or an attendee.
      `read-all` and the `IsPlatformActor` shortcut do **not** grant it (Q3). A non-participant gets **404** on
      `GET /{id}`, `/{id}/minutes` and `/{id}/tasks`, the meeting is absent from `GET /` and the calendar view, and
      the follow-up title comes back as `null`.
- [ ] S15-4 — The report and export (§23) apply the same predicate. For that caller, tiles, attendance rate,
      decisions and the action register exclude those meetings, and export rows equal screen rows (the BL-346 rule).
- [ ] S15-5 — Task side: a task's `relatedRecords` **drops** a confidential meeting for a caller who is not its
      organizer or attendee, so neither title nor route is shown (same rule as a dangling link, §13). This is
      verified by reading the Task Center projection as a non-participant assignee of a meeting-born task. No file in
      `Features/Tasks/**` changes.
- [ ] S15-6 — For invite, change and cancel of a confidential meeting, the stored `NotificationDispatch.Subject`,
      `VariablesJson` and the `invite.ics` `SUMMARY` carry the **subtype name** in place of the meeting title.
      Description and agenda are not sent today and stay unsent. `LOCATION` is kept (Q3). This is measured on the
      stored dispatch row, not on the template.
- [ ] S15-7 — The audit metadata of `CreateMeetingCommand` and `UpdateMeetingCommand` carries no `Title` when the
      meeting is confidential.
- [ ] S15-8 — Participants see everything as today, plus a localized "Confidential" lock badge on the list, detail
      page and calendar event. The personal calendar feed (`CalendarMeetingReader`, own meetings only) is unchanged.
- [ ] S15-9 — Binding rule for later surfaces (availability, another person's calendar; Dalga 8): a confidential
      meeting shows to a non-participant only as "busy" with start and end. R1 adds a **source-scan guard**: every
      reader of `IMeetingRepository` or `IMeetingAttendeeRepository` outside `Features/Meetings/**` must be on a
      reasoned allow-list. Today that list is `CalendarMeetingReader` (own), `MeetingGatewayAdapter` (own),
      `ReviewMeetingGateReader` (status only), `TaskWorkItemProvider`, and the notification handlers (attendee rows).
      A new reader cannot appear without review.

**Failure paths.**
- A `read-all` non-participant → 404 with no metadata (not 403).
- Cross-tenant → 404.
- Updating the type without `types-manage` → 403.
- Concurrent type update → 409 (existing).
- An organizer reassigned away loses access unless they are an attendee (existing D4 rule, stated because it now
  matters more).

**Tests.** An HTTP matrix of {organizer, attendee, `read-all` non-participant, platform actor, other tenant} × {detail,
list, minutes, tasks, report, export, related record}. A real-Mongo test for the stored dispatch row. The source-scan
allow-list. Sabotage: remove the confidential branch from `CanView`, then the matrix is red. Make the resolver ignore
the flag, then the related-record test is red. Restore both and they go green.

**L10n.** 7 languages: checkbox, help text and badge. **Permissions:** `types-manage` sets the flag; no new key.
**Tenant isolation:** unchanged predicates, tenant-scoped repositories. **Depends on:** nothing. **Estimate:** 2
prompts (backend and matrix; UI, mail and l10n).

### 24.11 S16 — Mandatory attendance before publishing

**Status: draft / not approved.** Pre-load item 2 (§5.7: "Her davetli için katıldı / katılmadı / mazeretli
seçilmeden tutanak yayımlanmaz").

**What the user gets.** For a type with "Attendance mandatory", the minutes cannot be published until every invitee
is marked present, absent or excused. The editor lists who is still unmarked.

**Files / areas.** `Handlers/CommandHandlers/MinutesCommandHandlers.cs` (`PublishMinutesHandler`,
`CorrectPublishedMinutesHandler`), `MeetingModels.cs` (reason code), `minutes-editor.js` with its l10n,
`MinutesEditor/_Attendance.cshtml`, `MeetingCapabilities.AttendanceMandatoryEnforced = true`, and the
`MEETINGDETAIL` help step in 7 languages.

**Acceptance criteria**
- [ ] S16-1 — Publishing a meeting whose **current** type has `AttendanceMandatory = true` is refused with 409
      `MEETING_MINUTES_ATTENDANCE_INCOMPLETE`, unless the draft's `Attendance` names every `MeetingAttendee` row
      exactly once with Present, Absent or Excused. Every row counts: the organizer's own row, and declined invitees
      too (Q5).
- [ ] S16-2 — The same rule applies to `CorrectPublishedMinutes` (a correction is written Published directly).
- [ ] S16-3 — `SaveMinutesDraft` stays permissive: an incomplete draft saves.
- [ ] S16-4 — A type with the flag off publishes exactly as before (regression test).
- [ ] S16-5 — The editor shows the unmarked names before publish. The publish button is not hidden (the server is
      the authority), and the server refusal shows as localized text.
- [ ] S16-6 — `AttendanceMandatoryEnforced` is `true`, and S14's note for this flag disappears without any other edit.

**Failure paths.** An attendee added after the draft was saved → 409 until marked. A stale `ExpectedVersion` → 409
(existing). No `minutes-publish` → 403. A confidential non-participant → 404 (S15).

**Tests.** Unit test of the rule; HTTP publish 409 then 200. Sabotage: skip the check, then the 409 test is red.
**L10n:** 7 languages. **Permissions:** unchanged. **Tenant isolation:** unchanged. **Audit:** `PublishMinutesCommand`
and `CorrectPublishedMinutesCommand` stay in known debt (Q14, §25). **Depends on:** S14 (switch only).
**Estimate:** 1 prompt.

### 24.12 S17 — Note and outcome per agenda item

**Status: draft / not approved.** Pre-load item 3 (§5.7). Plan §5.2 first row. This is the base that a later
multi-task review needs; that review stays out of scope (§24.2).

**What the user gets.** In the minutes, each agenda line has a discussion note and an outcome. They are frozen with
the published version and carried into corrections.

**Files / areas.**
- `Domain/Entities/Meetings/MeetingMinutesVersion.cs` (`AgendaNotes`, new embedded `MinutesAgendaNote` type in its
  own file).
- `MeetingModels.cs`: `SaveMinutesDraftRequest.AgendaNotes` and `CorrectPublishedMinutesRequest.AgendaNotes`
  (nullable), and DTOs.
- `MinutesCommandHandlers.cs` and `MeetingQueryHandlers.cs`.
- `MinutesEditor/` (new partial `_AgendaNotes.cshtml`), `minutes-editor.js` and its l10n.
- resx in 7 languages; `MEETINGDETAIL` help.

**Acceptance criteria**
- [ ] S17-1 — A minutes version stores `AgendaNotes[]`. Versions written before R1 read as an empty list.
- [ ] S17-2 — `SaveMinutesDraft`: `AgendaNotes = null` keeps the draft's current notes and `[]` clears them. Each
      `AgendaItemId` must be an agenda item of this meeting (400 `MEETING_AGENDA_ITEM_NOT_FOUND`), with no duplicates
      (400 `MEETING_MINUTES_AGENDA_NOTE_DUPLICATE`). Note ≤ 4000 and Outcome ≤ 1000. A row with both empty is dropped.
      At most 100 rows.
- [ ] S17-3 — `AgendaTextSnapshot` is taken **on the server** from `MeetingAgendaCodes.TextFor(item)` at save time
      and never from the client. A carried-forward line keeps its code.
- [ ] S17-4 — Publish freezes the notes with the version (K4). `CorrectPublishedMinutes` with
      `AgendaNotes = null` copies them from the corrected version, and the corrected version stays byte-identical.
- [ ] S17-5 — `GET /{id}/minutes` returns `AgendaNotes` for each version. In the editor, each line shows Note and
      Outcome text areas (Enter adds a new line; Ctrl/⌘+Enter saves, UXH-001 §3). Published versions are read-only.
- [ ] S17-6 — An agenda line deleted after a note was saved: the note is kept with its snapshot and labelled "line
      removed", and publishing is allowed.
- [ ] S17-7 — The report and export (§23) are unchanged in R1 (regression test). Adding outcomes to the report is
      follow-up §24.24.

**Failure paths.** An agenda id from another meeting → 400. A published version → 409 (existing). Stale version →
409. No `minutes-write` → 403. A confidential non-participant → 404.

**Tests.** Unit and HTTP tests; a real-Mongo round trip of a published version. Sabotage: accept the client's snapshot
text, then the snapshot test is red. **L10n:** 7 languages. **Permissions:** unchanged (`minutes-write`,
`minutes-publish`). **Tenant isolation:** unchanged. **Audit:** the minutes commands stay in known debt (Q14).
**Depends on:** nothing. **Estimate:** 2 prompts.

### 24.13 S18 — Mandatory agenda items for quality-record types

**Status: draft / not approved.** Pre-load item 5 (§5.7). ISO 9001:2015 §9.3.2.

**What the user gets.** QA can list agenda lines that a quality-record type must always discuss. Every new meeting of
that type gets them. They cannot be removed or reworded, and the minutes cannot be published without an outcome for
each one (Q6).

**Files / areas.** `MeetingType.cs` (`MandatoryAgendaItems`), `AgendaItem.cs` (`IsMandatory`), the type validators,
`MeetingCommandHandlers.cs` (create and type-change insertion), `AgendaItemCommandHandlers.cs` (refusals),
`MinutesCommandHandlers.cs` (S18-5), the type form (list editor shown only when "Quality record" is ticked), the
detail agenda (lock), resx in 7 languages, `MeetingCapabilities.MandatoryAgendaEnforced = true`, and the
`MEETINGTYPES` / `MEETINGDETAIL` help. Audit: `UpdateAgendaItemCommand` and `DeleteAgendaItemCommand` move to path
`a` (§25).

**Acceptance criteria**
- [ ] S18-1 — `MandatoryAgendaItems` (≤ 20 × ≤ 200) is accepted only when `IsQualityRecord = true` (400
      `MEETING_TYPE_MANDATORY_AGENDA_REQUIRES_QUALITY_RECORD`). A line may not repeat an `AgendaTemplate` line (400
      `MEETING_TYPE_AGENDA_LINE_DUPLICATED`).
- [ ] S18-2 — Every meeting of such a type created after this slice gets the mandatory lines from the **server**,
      with `IsMandatory = true`, whatever the client sends. This holds for the Create page, follow-ups (S7) and series
      instances (S11). Order: carried-forward lines first (K6), then mandatory lines in the type's order, then all
      other lines.
- [ ] S18-3 — `DeleteAgendaItem` on a mandatory line → 409 `MEETING_AGENDA_ITEM_MANDATORY`. Changing its text through
      `UpdateAgendaItem` → the same 409. `ReorderAgenda` is allowed.
- [ ] S18-4 — When `UpdateMeeting` changes the type to one with mandatory lines, the missing lines are added and none
      are removed. Lines already flagged keep the flag if the type changes away.
- [ ] S18-5 — (Recommendation; the owner decides, Q6.) Publish or correct of a meeting that has mandatory lines is
      refused with 409 `MEETING_MINUTES_MANDATORY_OUTCOME_MISSING` unless each mandatory line has a non-empty Outcome
      in `AgendaNotes` (S17).
- [ ] S18-6 — Meetings created before this slice are not backfilled and not blocked.
- [ ] S18-7 — In the UI, a mandatory line shows a lock and the text "required for quality records (ISO 9001 §9.3.2)",
      and no edit or delete control is rendered for it.
- [ ] S18-8 — `MandatoryAgendaEnforced` is `true`, and S14's note changes to the retention-only text.
- [ ] S18-9 — The list content is **QA data**. No code seeds it. The §9.3.2 a–f inputs (status of actions from
      earlier reviews; changes in issues; QMS performance; adequacy of resources; risk actions; improvement
      opportunities) are named in the help text only as an example.

**Failure paths.** A non-quality type with a list → 400. Deleting a mandatory line → 409. No `types-manage` → 403. An
unknown agenda id → 404 (existing).

**Tests.** Create through all three paths (page, follow-up, series sweep) and assert the lines and the flag. Refusal
tests. Sabotage: let the client agenda replace the server insertion, then S18-2 is red. **L10n:** 7 languages.
**Permissions:** `types-manage` to set the list; `update` to edit the agenda (unchanged). **Tenant isolation:**
unchanged. **Depends on:** S17 (for S18-5), S15 (type commands already audited), S14. **Estimate:** 1–2 prompts.

### 24.14 S19 — Main type and subtype default fields (backend)

**Status: draft / not approved.** Scope A. Work list Dalga 4.1.

**What the user gets.** Nothing visible yet (S20 draws the screens). The backend can store main types, link each
subtype to one, and hold all of the manager's per-subtype defaults.

**Files / areas.**
- New `Domain/Entities/Meetings/MeetingCategory.cs`, and `Enums/Meetings/MeetingEnums.cs` (`MeetingParentRule`).
- `MeetingType.cs` (§24.4 fields).
- `Domain/Repositories/IMeetingRepositories.cs` and `Infrastructure/Persistence/Repositories/MeetingRepositories.cs`
  (`MeetingCategoryRepository : TenantRepository<MeetingCategory>`).
- `Infrastructure/Persistence/Schema/PlatformCollections.cs` (`MeetingCategories = "meeting_categories"`) and
  `PlatformSchemaManifest.Meetings.cs` (unique index).
- New GR-named files `Commands/{Create,Update,Delete,BulkDelete}MeetingCategoryCommand.cs`,
  `Queries/GetMeetingCategory{List,ById}Query.cs`, `Handlers/CommandHandlers/{…}MeetingCategoryHandler.cs`,
  `Handlers/QueryHandlers/GetMeetingCategory{List,ById}Handler.cs` and `Validators/{Create,Update}MeetingCategoryValidator.cs`.
- `MeetingModels.cs`, the type validators and handlers, `API/Controllers/MeetingCategoriesController.cs`
  (`api/v1/meetings/categories`), `MeetingsController.cs` (`lookups/positions`, grouped `lookups/types`), the
  `DependencyInjection.cs` lines, and the audit ledger.

**Acceptance criteria**
- [ ] S19-1 — Collection `meeting_categories` with unique index `ux_meeting_categories_tenant_name`
      `(TenantId, Name, IsDeleted)`, verified against the live index list. Endpoints `GET/POST categories`,
      `GET/PUT/DELETE categories/{id}` and `POST categories/bulk-delete` exist under `types-manage`.
- [ ] S19-2 — Delete and bulk delete are soft deletes. Each row still referenced by a non-deleted subtype is refused
      with 409 `MEETING_CATEGORY_IN_USE`. Bulk delete reports deleted and refused ids separately (K12).
- [ ] S19-3 — Subtype create/update require `MeetingCategoryId` (400 `MEETING_CATEGORY_REQUIRED` /
      `MEETING_CATEGORY_NOT_FOUND`) and accept every §24.4 field with the §24.5 rules.
- [ ] S19-4 — Deploy writes **nothing** to existing `meeting_types` rows. A real-Mongo test inserts a pre-R1 shaped
      row and reads it with category `null`, empty lists and the defaults.
- [ ] S19-5 — `GET lookups/types` returns each subtype with category id, name and sort order plus its default
      fields. Order: category sort order, category name, subtype sort order, subtype name. Rows with `null` category
      come last in the "Unassigned" group (its name is localized in the UI, not sent by the API).
- [ ] S19-6 — `GET lookups/positions` (`types-manage`) lists tenant positions through `GetPositionsQuery`. Position
      ids written on a subtype are checked the same way.
- [ ] S19-7 — `ux_meeting_types_tenant_name` is unchanged (R1-D3), shown by an index test.
- [ ] S19-8 — Audit: the four category commands use path `a`, and `DeleteMeetingTypeCommand` moves to path `a`
      (§25). `AuditTrailStandardTests` is green.

**Failure paths.** Duplicate category name → 409 `MEETING_CATEGORY_NAME_DUPLICATE`. Stale version → 409. A category
or position id from another tenant → 400 not-found. No `types-manage` → 403. Deleting a category in use → 409.

**Tests.** Real-Mongo index and deserialization tests; HTTP CRUD with tenant isolation. Sabotage: remove the in-use
check, then the delete test is red. Change the type index, then the S19-7 test is red. **L10n:** reason codes in 7
languages (bridge). **Permissions:** `types-manage`. **Tenant isolation:** `TenantRepository`, tenant-first index.
**Depends on:** nothing. **Estimate:** 2 prompts.

### 24.15 S20 — Type screens and grouped type picker

**Status: draft / not approved.** Scope A (screens).

**What the user gets.** A "Main types" screen. A subtype list grouped by main type, with an "Unassigned" group. A
subtype form with every default field. A type picker on the meeting form that is grouped by main type.

**Files / areas.**
- New Slim screen `Views/Meetings/MeetingTypeCategories/` (`Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`
  `data-dt-standard="v2"` `data-dt-data-mode="client"`, `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml`,
  `_DetailsQuickView.cshtml`, `MeetingTypeCategoriesIndex.cs`), `wwwroot/assets/js/Meetings/categories/{index.js,index.l10n.js}`,
  and `Resources/Views/Meetings/MeetingTypeCategories/MeetingTypeCategoriesIndex.{en,tr,fr,es,zh,ar,ru}.resx`.
- The Web `MeetingsController.cs` page and proxy actions.
- A manifest page `MEETING_TYPE_CATEGORIES` (route `/Meetings/MeetingTypes/Categories`, parent `MEETING_TYPES`,
  nav-hidden, `types-manage`).
- `Nav.Page.MEETINGTYPECATEGORIES` and `Help.MEETINGTYPECATEGORIES.*` in 7 languages.
- `Views/Meetings/MeetingTypes/*` and `types/*.js`.
- `Views/Meetings/_Form.cshtml` and `form.js` (grouped picker).

**Acceptance criteria**
- [ ] S20-1 — The main-type screen follows the GR Slim file set and states `Layout = "_LayoutTenantShell"`.
      `verify_datatable_page.py --area Meetings --module MeetingTypeCategories --reference slim --data-mode client`
      PASS, with `data_mode: client` and `data_mode_max_rows: 100` (the catalogue has 23 main types). The create/edit
      offcanvas is `data-dt-primary="enter"` and the delete confirm is `chord`.
- [ ] S20-2 — The subtype list has a "Main type" column and filter, is ordered by main type, and shows the
      "Unassigned" group with its count. `verify_datatable_page.py --area Meetings --module MeetingTypes` passes, or
      every gap is reported under the touch protocol (BL-440).
- [ ] S20-3 — The subtype form requires a main type (an unassigned row cannot be saved without one) and shows every
      §24.4 field with inline help: the list of automatic placeholders next to the title template, a position search,
      a recurrence frequency with interval, and the parent rule.
- [ ] S20-4 — The type picker on the meeting create and edit forms is grouped (`optgroup`) by main type, with
      "Unassigned" last.
- [ ] S20-5 — The new page ships with its help text in 7 languages, so the ledger does not grow. The `MEETINGTYPES`
      help is updated.
- [ ] S20-6 — Without `types-manage`: no page, no skeleton, no help (UAS-001).

**Failure paths.** Server refusals (duplicate, in use, required) show as localized field or toast messages. A stale
version → reload prompt.

**Tests.** JS screen tests, `verify_datatable_page`, RESX parity, `NavManifestL10nGuardTests` for the new page code,
and a browser smoke test. Sabotage: drop the `optgroup` mapping, then the grouped-picker JS test is red. **L10n:** 7
languages. **Permissions:** `types-manage` (screens), `read` (picker). **Tenant isolation:** server side, unchanged.
**Depends on:** S19 (and S15 and S18 if their fields are shown). **Estimate:** 2 prompts.

### 24.16 S21 — Daily frequency and series title template

**Status: draft / not approved.** §5.6 items 5 and 6, and §5.7 "Tekrar". Work list Dalga 4.3.

**What the user gets.** A series can repeat daily. A series can carry a title template whose date placeholders are
filled in separately for each meeting.

**Files / areas.** `MeetingEnums.cs` (`Daily = 4`), `Services/MeetingSeriesSchedule.cs`, `MeetingSeries.cs`
(`TitleTemplate`), `GenerateDueMeetingSeriesHandler.cs`, a new `Services/MeetingTitleTemplate.cs` and its JS twin
`wwwroot/assets/js/Meetings/title-template.js` with **one shared fixture table**, the series views and JS, resx in 7
languages, and the `MEETINGSERIES` help. Audit: `CreateMeetingSeriesCommand` and `UpdateMeetingSeriesCommand` move to
path `a`.

**Acceptance criteria**
- [ ] S21-1 — `MeetingSeriesFrequency.Daily = 4` is appended, and both the stepping and the occurrence-index switch
      in `MeetingSeriesSchedule` handle it (unit tests across a month and a year boundary).
- [ ] S21-2 — (Recommendation; the owner decides, Q8.) A daily occurrence that falls on a non-working day of the
      tenant's working calendar is **skipped**: the claim advances, no meeting is created, and the sweep response
      counts it as skipped.
- [ ] S21-3 — If `MeetingSeries.TitleTemplate` is set, each generated title is the template rendered for that
      occurrence's start in the tenant time zone. A template with a non-automatic placeholder → 400
      `MEETING_SERIES_TITLE_PLACEHOLDER_NOT_AUTO`. If it is null, the title is `Name` as today.
- [ ] S21-4 — The C# and JS renderers pass the **same** fixture table (ISO week across 31 Dec / 1 Jan, quarter
      edges, a tenant zone different from UTC).
- [ ] S21-5 — The series form offers Daily and the title template. 7 languages. The help is updated.

**Failure paths.** Interval < 1 → 400 (existing). A user placeholder in a series template → 400. A sweep with the job
disabled → nothing generated, as today (BL-536, a configuration question).

**Tests.** Schedule unit tests; the fixture parity test. Sabotage: change `{Week}` in JS only, then the parity test is
red. Remove the skip rule, then the working-day test is red. **L10n:** 7 languages. **Permissions:** `series-manage`.
**Tenant isolation:** unchanged (the sweep already runs inside each tenant's `TenantScope`). **Depends on:** nothing.
**Estimate:** 1–2 prompts (2 if the working-calendar skip is approved).

### 24.17 S22 — Create-form auto-fill and agenda at create

**Status: draft / not approved.** Scope B. §5.5 question 1. §5.6 "Ekranda doldurma". Work list Dalga 4.2.

**What the user gets.** Choosing a subtype fills the form, following the manager's text:
- Title = the title template.
- Type = main type / subtype.
- Place = the default place.
- Start = chosen by the user. End = Start + the default duration.
- Description = the description template.
- Participants = the holders of the default positions.
- Follow-up-of = according to the parent rule.
- Agenda = the template lines, editable before saving.

The agenda is saved together with the meeting.

**Files / areas.**
- `MeetingModels.cs` (`CreateMeetingRequest.AgendaItems`).
- `MeetingCommandHandlers.cs` (create), `Validators/MeetingValidators.cs` (placeholder rule on create and update).
- New GR-named query files `GetMeetingTypeDefaultAttendeesQuery`, `GetPreviousMeetingSuggestionQuery` and their
  handlers.
- `API/Controllers/MeetingsController.cs` and the Web proxy.
- `Views/Meetings/_Form.cshtml`, `Create.cshtml`, `form.js` (remove `?prefillAgenda=1`), the detail-page script (the
  prefill reader).
- resx in 7 languages; the `MEETINGCREATE` help.

**Acceptance criteria**
- [ ] S22-1 — Choosing a subtype fills Title, Location, End (Start + `DefaultDurationMinutes`, recomputed when Start
      changes unless End was edited by hand), Description, Attendees, Follow-up-of and Agenda. Automatic placeholders
      re-render when Start changes. User placeholders are highlighted. If the user changes the subtype after editing
      a field, the form asks before overwriting it (`showConfirm`).
- [ ] S22-2 — `CreateMeetingRequest.AgendaItems` (≤ 50 × ≤ 500) follows R1-D6. The lines are written in the same
      `isNew` block as the attendees and **before** the invite mail. Mandatory lines (S18) are always added by the
      server.
- [ ] S22-3 — The post-create `?prefillAgenda=1` path is removed, so the agenda is never applied twice. An old URL
      that still carries the parameter changes nothing.
- [ ] S22-4 — (Owner decides, Q7.) Follow-ups (S7) and series instances (S11) send `AgendaItems = null` and get the
      type template after the carried lines. If the owner declines, they send `[]` and their behavior is unchanged.
- [ ] S22-5 — `GET lookups/type-default-attendees?meetingTypeId=` (`create`) returns the **eligible current holders**
      of the subtype's positions, plus the positions nobody holds (shown as a warning, never a block; BL-356). It
      returns holders only for positions named on that subtype, so it cannot be used to enumerate arbitrary positions.
- [ ] S22-6 — `GET lookups/previous-meeting?meetingTypeId=&before=` (`read`) returns the latest meeting of that
      subtype before Start that the caller **can view** (`CanView`, including the S15 rule). It is used as the
      pre-selection for `PreviousInSeries`. The picker is shown for `UserSelects` and hidden for `None`.
- [ ] S22-7 — A title with an unfilled `{Name}` → 400 `MEETING_TITLE_PLACEHOLDER_UNFILLED` on create and update.
- [ ] S22-8 — A retried create (same server idempotency key) returns the same meeting, and its agenda is not
      duplicated.
- [ ] S22-9 — `ParticipantSelectionHint` is shown next to the attendee picker. The `MEETINGCREATE` help now says the
      agenda is set on the form.

**Failure paths.** A subtype without templates → nothing is filled and the form works as today. A position with no
holder → a warning. No visible previous meeting → empty. More than 50 lines → 400. No `create` → 403 on the lookups
and no Create button.

**Tests.** Handler tests for `null`, `[]` and a list; a retry test; the lookup HTTP tests (tenant, permission,
confidential); JS form tests. Sabotage: apply the template on the client again after create, then the
double-application test is red. Let the server ignore `null`, then the follow-up/series template test is red.
**L10n:** 7 languages. **Permissions:** `create` and `read` (no new key). **Tenant isolation:** the lookups use
tenant-scoped repositories and the existing eligibility query. **Depends on:** S18, S19, S20, S21. **Estimate:** 2
prompts.

### 24.18 S23 — Repeat on create

**Status: draft / not approved.** Scope B. §5.5 question 1 ("Tekrarla"). R1-D7.

**What the user gets.** A "Repeat" option on the create form. When it is ticked, the meeting is created and a series
starts from it. Later instances are generated as for any series, and the Series page remains the place to manage
them.

**Files / areas.** `MeetingModels.cs` (`CreateMeetingRequest.Repeat`: frequency, interval, end, lead time, series
name, title template), `MeetingCommandHandlers.cs` (create sends `CreateMeetingSeriesCommand`),
`MeetingSeriesCommandHandlers.cs` (adopt `CreatedFromMeetingId`), `MeetingSeries.cs`, the schema manifest (sparse
unique index), `_Form.cshtml`, `form.js`, resx in 7 languages, and the `MEETINGCREATE` / `MEETINGSERIES` help.

**Acceptance criteria**
- [ ] S23-1 — The Repeat panel is rendered only for holders of `series-manage` (Q9). Its frequency and interval
      default from the subtype's default recurrence. An event-based or empty default means Repeat is off. The series
      name is pre-filled with the subtype name and can be edited.
- [ ] S23-2 — After the meeting is created (`isNew`), the server sends `CreateMeetingSeriesCommand` with:
      `CreatedFromMeetingId` = the meeting; `StartsAt` = the meeting's start; `DurationMinutes` = End − Start;
      type, organizer, attendees and location from the meeting; `TitleTemplate` = the title with user placeholders
      filled and automatic ones kept; `ChainAsFollowUp` = `true` unless the parent rule is `None`.
- [ ] S23-3 — The adopted meeting is occurrence 0. `LastGeneratedMeetingId`, `LastGeneratedAt` and
      `LastProcessInstanceId` are set, so a sweep run right afterwards generates **no** duplicate first meeting
      (test).
- [ ] S23-4 — A meeting is adopted by at most one series (sparse unique index). A retried create does not create a
      second series.
- [ ] S23-5 — Partial success is visible (K12). If the series fails (duplicate name, no permission), the meeting is
      still returned with 201 together with `series: { created: false, reasonCode }`, and the UI says "Meeting created;
      repeat was not set" with the reason and a link to the Series page.
- [ ] S23-6 — No third creation path: a source-scan guard asserts that a `Meeting` is constructed only inside
      `CreateMeetingHandler`. Sabotage: add a construction elsewhere, then the guard is red.
- [ ] S23-7 — The form says honestly that later meetings are made by the background job (BL-536).

**Failure paths.** Series name duplicate → partial success (S23-5). No `series-manage` → no panel, and the server
refuses only the series part. Interval < 1 → 400 before anything is written. Concurrent sweep → the existing claim
guarantees one instance per occurrence.

**Tests.** Handler and HTTP tests, a sweep-after-create test, and the source-scan guard. **L10n:** 7 languages.
**Permissions:** `create` + `series-manage`. **Tenant isolation:** unchanged. **Audit:** `CreateMeetingCommand` is
already path `a` (S15) and `CreateMeetingSeriesCommand` is path `a` (S21). **Depends on:** S21, S22. **Estimate:** 2
prompts.

### 24.19 S24 — One-off type catalogue importer

**Status: draft / not approved.** Scope D. Work list Dalga 5.1 (this slice is the tool; the data is separate).

**What the user gets.** A `types-manage` holder uploads the meeting catalogue (main types and subtypes) as one file.
They first see a preview that lists what will be created, updated, left unchanged or reported, and then confirm.
Running the same file again changes nothing.

**File format (UTF-8 JSON, ≤ 1 MB, ≤ 200 rows; one object per subtype).** The keys are `mainType`,
`mainTypeSortOrder`, `subtype`, `sortOrder`, `titleTemplate`, `defaultDurationMinutes`, `defaultRecurrence`
(`Daily|Weekly|Monthly|Quarterly|Yearly|null`), `defaultRecurrenceInterval`, `defaultLocation`,
`defaultParticipantPositionCodes[]`, `participantSelectionHint`, `descriptionTemplate`, `parentMeetingRule`,
`defaultActionTaskTypeCode`, `agendaTemplate[]`, `mandatoryAgendaItems[]`, `isQualityRecord`, `requiresESignature`,
`attendanceMandatory` and `isConfidential`. On update, a `null` or absent key means **unchanged**. On create, it means
the default.

**Files / areas.** A new page `/Meetings/MeetingTypes/Import` (manifest `MEETING_TYPE_IMPORT`, parent
`MEETING_TYPES`, nav-hidden, `types-manage`; `Views/Meetings/MeetingTypeImport/Index.cshtml`; JS; resx in 7
languages; `Nav.Page.MEETINGTYPEIMPORT` and `Help.MEETINGTYPEIMPORT.*` in 7 languages). New GR-named
`Queries/PreviewMeetingTypeCatalogueImportQuery.cs` (writes nothing) and `Commands/ImportMeetingTypeCatalogueCommand.cs`
with their handlers and validators. Controller and proxy actions.

**Acceptance criteria**
- [ ] S24-1 — The page states `Layout = "_LayoutTenantShell"`, is reachable from the types list toolbar for
      `types-manage` only, and ships with its help text.
- [ ] S24-2 — A malformed file → 400 `MEETING_TYPE_IMPORT_FILE_INVALID`, naming the row and field. Nothing is written.
- [ ] S24-3 — `POST types/import/preview` **writes nothing** (a real-Mongo test counts documents before and after).
      For each row it returns `create` / `update` (with the changed field names) / `unchanged` / `conflict` /
      `error`, plus the unresolved task-type codes, the unresolved position codes and a plan hash.
- [ ] S24-4 — The idempotency key is (main type name, subtype name), trimmed and compared ordinal-ignore-case. An
      existing match is **updated**, never duplicated. Importing the same file a second time gives all `unchanged`
      and zero writes.
- [ ] S24-5 — A subtype name that exists under a **different** main type → `conflict`: skipped, reported, never
      moved. A subtype that exists with **no** main type ("Unassigned") → `update`, which assigns the main type. This
      is the bulk path for legacy rows (R1-D2).
- [ ] S24-6 — The QA flags (`isQualityRecord`, `requiresESignature`, `attendanceMandatory`, `isConfidential`,
      `mandatoryAgendaItems`) change only when the file states them explicitly, and the preview lists them in a
      separate "flag changes" block.
- [ ] S24-7 — `defaultActionTaskTypeCode` is resolved against the tenant's active task types through
      `GetActiveTaskTypesQuery` (read-only). An unknown code, or a dual value such as `A / B`, leaves the field
      **empty** on create or **unchanged** on update, and is reported. No task-type data (MOD-0024) is ever written.
- [ ] S24-8 — Position codes are resolved through `GetPositionsQuery`. An unknown code is dropped from that row and
      reported.
- [ ] S24-9 — `POST types/import` recomputes the plan and refuses with 409 `MEETING_TYPE_IMPORT_PLAN_CHANGED` if its
      hash differs from the preview's. It writes main types first and then subtypes, in **one transaction** using the
      same Mongo session mechanism as `PublishMinutesHandler`. If that is not available for these repositories, the
      slice stops and reports to the CT instead of writing row by row.
- [ ] S24-10 — Audited path `a`: one event `meetings.type-catalogue.imported` with the file SHA-256, the counts, the
      created ids and the updated ids with their changed field names (§25).
- [ ] S24-11 — No catalogue data file is added to the repository by this slice.

**Failure paths.** File too large or too many rows → 400. Plan changed between preview and commit → 409. No
`types-manage` → 403 and no page. Cross-tenant: impossible by construction, because only the caller's tenant is read
and written.

**Tests.** Preview has no side effects; second-run idempotency; the conflict and legacy-assignment cases; code
resolution; the plan-hash 409. Sabotage: match on subtype name only (ignoring the main type), then the conflict test
is red. Remove the hash check, then the 409 test is red. **L10n:** 7 languages. **Permissions:** `types-manage`.
**Tenant isolation:** as stated. **Depends on:** S19 and S20 (S15 and S18 for their flags). **Data dependencies for
the real run:** the task-type catalogue load including `COM-ACTION`, `FIN-ACTION` and `ITG-ACTION` (MOD-0024,
§5.7), the position mapping (HR), the agenda texts (business owners), the flags (QA), and the QA process owner's name
(§5.7, still open). **Estimate:** 1–2 prompts.

### 24.20 Record codes (RCS-001) — recorded and deferred, no slice

**1. Meeting subtype and main type: class İKİSİ DE.** Like Görev Türü (RCS-001 §3), these are few, long-lived
records that people refer to by a meaningful code. The system proposes a code, and a change needs a reason that goes
to the audit trail (§2, AUD-001).
- Proposed prefixes: **`MTY`** for the meeting subtype (`MeetingType`) and **`MTC`** for the main type
  (`MeetingCategory`). RCS-001 §5.5 forbids one prefix for two record types, so the main type cannot share `MTY`.
- Measured at `826150ef4`: neither prefix is in the RCS-001 §3 table, and neither appears anywhere in the repository
  as a code prefix. (`MTY` appears only in the work list's uncommitted item 6a.)
- Both records are tenant-wide and belong to the group, not to one company, so the company part of the code is the
  **group prefix** (RCS-001 §5.3, the tenant setting, same value as `OrgPrefix`). Example: `GMG-MTY-000001`.

**2. Meeting instance (an identity number, for example for quality-record minutes): class OTOMATİK.** This is
separate from the meeting type. Meetings are a high-volume record, so the system issues the number and nobody may
change it (RCS-001 §2).
- Proposed prefix: **`MTG`**. It is not in the RCS-001 §3 table at `826150ef4`.
- ⚠ The string `MTG-` is already used in code. `WorkCenterNext/app.js:9626` builds a client-side calendar event id
  `MTG-${item.id}`. Test fixtures use `MTG-TEST-01` (`workcenter-next-fixture-contract.test.js:112`,
  `workcenter-next-resolvers.test.js:52`). `wcn-review-meeting-flow.test.js:53` is a guard that asserts **no**
  `MTG-` id appears in a source file. A real code such as `GMG-MTG-000042` contains `MTG-`. Before `MTG` is accepted,
  the owner or CT checks whether that guard or the app.js event id would clash. If it would, a different prefix is
  chosen.
- The company part is the company of the organizer's legal entity (RCS-001 §5.2: the company that first opened the
  record), or the group prefix if the owner decides that meetings are group-wide (open question Q12).

**Why none of these is built in R1.** The shared code-issuing service (BL-565, RCS-001 §5.12) does not exist, and
RCS-001 §5.12 and §6 forbid a module-local counter. **No R1 slice adds a `Code` field** to `MeetingType`,
`MeetingCategory` or `Meeting`. When BL-565 lands, the field goes into a later revision of this pack, using the RCS-001
§6 checklist: a reason field for İKİSİ DE, audit of the old and new code, an API that refuses edits for OTOMATİK, ASCII
only, and archived codes never reused. Existing records keep having no code (RCS-001 §2: the standard applies to new
records).

**Separate owner approval needed.** Adding the `MTY`, `MTC` and `MTG` rows to the RCS-001 §3 table means editing
`.antigravity/rules/record-coding-standard.md`. That is a **protected path** (AGENTS.md §4) and needs its own owner
approval. R1 does not do it.

### 24.21 Gateway / API routing decision for R1

```text
Decision: no gateway change needed.
```

Measured in `gateway/Diten.ApiGateway/ocelot.json` (worktree at `2904f4029`): `/api/v1/meetings` (GET, POST,
OPTIONS) and the catch-all `/api/v1/meetings/{everything}` (GET, POST, PUT, DELETE, OPTIONS) → `localhost:5057`
already exist. Every R1 endpoint (`categories`, `categories/{id}`, `categories/bulk-delete`, `lookups/*`,
`types/import/preview`, `types/import`) falls under the catch-all. The Web proxy has **no** catch-all, so every slice
adds explicit actions to `frontend/Diten.Web/Controllers/MeetingsController.cs` (§24.6). `ocelot.json` stays
integration-agent owned; R1 does not touch it.

### 24.22 R1 ready-for-dev checklist (to be ticked by the reviewer and the owner, not by the author)

- [ ] Independent review of this version (SOP §20.4) recorded, with no `UYGUN DEĞİL` and no open ENGEL.
- [ ] The owner answered or explicitly deferred each question in §24.25.
- [ ] The owner approved the slice order and estimates in §24.7, or reordered them.
- [ ] Golden Reference: Compact for the meeting and subtype screens and Slim for the main-type screen; backend GR
      naming, one type per file for new code (§24.6).
- [ ] Layout stated explicitly for every new view; data mode for the new screen (`client`, max 100 rows).
- [ ] Validation rules (§24.5) and failure paths (each slice) cover duplicate, missing, unauthorized, concurrency and
      cross-tenant cases.
- [ ] Permissions: existing keys only (§24.6). Q9 answered for Repeat.
- [ ] Audited Events (§25): every existing and new write command appears exactly once. The CT has decided Q13
      (audit category) and Q14 (K2 for the minutes commands).
- [ ] Page help: every new page code ships with `Help.{KOD}.Purpose/Audience/Step{n}/Step{n}Title` in 7 languages.
      Tour: `MEETINGS` and `MEETINGDETAIL` with their `data-tour` targets (S13).
- [ ] Teslim dilimleri (§26) covers every R1 slice and states its **required evidence level** (E1 to E5, SOP §23.1;
      card S-85). Each WP prompt names `Kapsanan dilim: DL-n` and carries that level.
- [ ] Live-use readiness (§24.26) has been reviewed with the owner. Data part (a) is entered before part (b).
- [ ] The record inconsistencies in §24.23 have been reconciled by the CT, or explicitly accepted as known.

### 24.23 Record inconsistencies for the CT to reconcile (not fixed by R1)

1. **Header vs frontmatter.** The "Authority" paragraph (line 25) still says "This pack is `status: draft` and grants
   no code-start authority". The frontmatter says `status: ready-for-dev` (line 9, changed on 2026-09-11).
2. **Registry row.** `execution/registries/module-id-registry.md` row MOD-0357 says `planned` (line 185 at
   `2904f4029`). The pack cites `:160` in two places (lines 21 and in ADR-003), and that line number has drifted.
   ADR-003 "Sonuçlar" says the registry status is updated when the pack is approved.
3. **S12 not marked delivered.** §19 S12 and §23 still read as spec/ready-for-dev. S12 is in code: `340be2e7a`
   ("merge: S12 meeting report and action register (BL-396) into the meetings lane") and `bc0efda86` (report/export
   "snapshot, not a controlled copy" fix). The work list (§1) also says "15 slices, 14 on main; S10 half done; no
   slice has an acceptance record".
4. **Permission count.** §14 lists 9 keys, and §16 K9 says "All eight". The code has **10**:
   `MeetingPermissions.SeriesManage` (`platform.meetings.series-manage`, S11) is missing from §14.
5. **Commands that do not exist.** §3 and §14 list `DeleteMeeting`, `BulkDeleteMeeting` and `RescheduleMeeting`
   (and the routes `DELETE /{id}`, `POST /bulk-delete`, `POST /{id}/reschedule`). No such command exists in
   `services/Diten.Platform/src` (grep). The manifest still declares the `DELETE` and `BULK_DELETE` actions.
6. **Routes.** §9 says `/Meetings/Types`, `/Meetings/Types/Create` and `/Meetings/Types/{id}/Edit`. The code uses
   `/Meetings/MeetingTypes…`, `/Meetings/Series…` and `/Meetings/Report`, which §9 does not list.
7. **Missing `data_mode`.** Module-pack-standard §2 requires it for DataTable modules ("if missing, the pack cannot be
   ready-for-dev"), but the frontmatter has none. No Meetings `_DataTable.cshtml` declares `data-dt-data-mode`. R1
   does not edit the frontmatter (unmeasured screens are not written, §3). It proposes that the CT adds a
   `screens:` block after measuring: `Meetings` (server), `MeetingTypes` (client, measured maximum),
   `MeetingSeries` (client or server, measured), and from S20 `MeetingTypeCategories` (client, 100).
8. **Repo-scope paths.** §5 names `Configurations/MongoDbIndexConfigurations.cs`. The meeting indexes are actually in
   `Persistence/Schema/PlatformSchemaManifest.Meetings.cs`. The mailer `Infrastructure/Services/MeetingInviteMailer.cs`
   is not listed in §5.
9. **Missing sections.** Before R1 the pack had no `Audited Events` (AUD-001) and no `Teslim dilimleri` (CT-APR-02).
   R1 adds both (§25, §26).
10. **Golden Reference deviation in code.** Several public types share a file (`MeetingCommands.cs`,
    `MeetingTypeCommands.cs`, `MeetingCommandHandlers.cs`, `MeetingValidators.cs`), and the MeetingTypes and Series
    screens have no `Details.cshtml`. These are existing deviations. R1 does not refactor them.
11. **§18** still says "Owner has approved the S1–S10 slicing".
12. **§12** says `Meeting.MeetingTypeId` "must resolve … active", but `MeetingType` has no active flag. Deletion is
    refused while the type is in use (`MEETING_TYPE_IN_USE`).
13. **Minutes editor page code.** The manifest has no page code for `/Meetings/{id}/Minutes`. Which help code it
    shows is unmeasured (S13-7).
14. **Audit ledger.** All 25 existing Meetings write commands are in "Bilinen borç" (§25). The pack never stated this.

### 24.24 Follow-up items added by R1 (not in any R1 slice)

- Re-resolving default positions to people for each series instance (Q10).
- Agenda lines in the invite e-mail body and the `.ics` `DESCRIPTION` (the shared Notifications template seed).
  Confidential meetings never carry them.
- Per-agenda-item outcomes in the report and export (§23).
- A structured per-task outcome and gate for multi-task review meetings (Dalga 6; MOD-0024 gate, XMC-001).
- Visible record codes for `MeetingType` / `MeetingCategory` and the meeting identity number, after BL-565 (§24.20).
- "Busy only" display of confidential meetings in any future availability or other-person calendar view (Dalga 8),
  bound by S15-9.
- Retention and immutability of quality-record minutes (§22 D5; work list 9.3). `MeetingCapabilities.RetentionRuleActive`
  stays `false` until then.

### 24.25 Open questions to the owner (R1)

1. **Q1 — Names and the "Unassigned" rule.** Are "Main type" / "Ana tür" and "Subtype" / "Alt tür" the right
   terms? Is R1-D2 (no backfill; legacy rows shown under "Unassigned" until assigned) accepted?
2. **Q2 — Unique subtype names.** Is R1-D3 accepted (no two subtypes with the same name, even under different main
   types)?
3. **Q3 — Confidential details.**
   (a) Should a platform operator (`IsPlatformActor`) also be excluded? Recommended: yes.
   (b) Invite e-mail and `.ics` show the subtype name instead of the title, and keep Location. Recommended: yes.
   (c) Should the report show a `read-all` holder a count of hidden confidential meetings? Recommended: no.
4. **Q4 — Confidentiality ratchet.** A meeting never becomes non-confidential through an edit (R1-D5). A mistaken
   flag needs a new meeting. Is that accepted?
5. **Q5 — Declined invitees.** Must they also be marked (absent or excused) before publishing an attendance-mandatory
   meeting? Recommended: yes.
6. **Q6 — Outcome for mandatory lines.** Must every mandatory agenda line have an outcome before publishing (S18-5)?
   Recommended: yes (ISO 9001 §9.3.3 evidence).
7. **Q7 — Template on follow-ups and series.** Should hand-scheduled follow-ups and series instances now get the
   subtype's agenda template from the server (S22-4)? This changes today's behavior. Recommended: yes.
8. **Q8 — Daily on non-working days.** Should Daily skip days the tenant's working calendar marks as non-working
   (S21-2)? Recommended: yes.
9. **Q9 — Who may Repeat.** Repeat on create is offered only to `series-manage` holders (recommended; no change to
   what each key means). The alternative is that every creator may repeat, which would change what
   `platform.meetings.create` grants.
10. **Q10 — Series attendees.** Recommended: a snapshot of people at series creation (as today). The alternative is
    re-resolving positions for each instance, which is follow-up work.
11. **Q11 — Importer file format.** Recommended: JSON. The CT converts the manager's Excel, and no new parser
    dependency is added. Is CSV or XLSX required instead?
12. **Q12 — Record-code prefixes.** `MTY` for the subtype, `MTC` for the main type, `MTG` for the meeting instance
    (OTOMATİK). Should a meeting's company prefix be the organizer's legal entity or the group? Adding the rows to
    RCS-001 §3 needs separate approval (protected path).
13. **Q13 — Audit category (CT).** Should a new `AuditCategory.Meetings` value be added (a shared enum,
    `Domain/Enums/AuditEnums.cs`; `Tasks = 16` is the precedent), or should the configuration events use
    `PlatformConfiguration`?
14. **Q14 — K2 for minutes (CT, AUD-001 §10 K10).** Are `SaveMinutesDraft`, `PublishMinutes` and
    `CorrectPublishedMinutes` K2 ("GxP record") for quality-record types? R1 leaves them in known debt either way. If
    they are K2, they cannot move to path `a` (it would create new K2 debt) and must wait for the in-transaction path.
    The CT also confirms that main-type, subtype and import commands are not K2 (this draft reads them as
    configuration).
15. **Q15 — S13 first.** Is putting page help and the tour first (before use, A7) accepted? Each later slice then
    updates the help text of the pages it changes.
16. **Q16 — Template language.** Templates (title, description, agenda) are tenant data in **one** language per
    subtype, not translated for each reader. Is that accepted?
17. **Q17 (carried from §5.7, still open).** The QA process owner's name. It blocks the real catalogue import (data),
    not the code.
18. **Q18 — the expected manager document (work list §6 item 6a).** When it arrives, R1 is compared with it, and the
    owner is asked about anything that conflicts before approval.

**Owner answers (2026-10-27, owner's word "hepsi-evet" in the CT · Toplantılar conversation, given to 14 plain-language
items the CT put to the owner).** These answers are part of the draft; they become binding only when this revision is
approved.

| Question | Answer |
|---|---|
| Q1 | Yes — "Ana tür / Alt tür" (en "Main type / Subtype"); R1-D2 accepted (no backfill, legacy rows under "Unassigned"). |
| Q2 | Yes — R1-D3 accepted (subtype names tenant-unique). |
| Q3 | Yes to all recommendations: (a) platform operators excluded; (b) invite mail and `.ics` show the subtype name, Location kept; (c) no hidden-meeting count in the report. |
| Q4 | Yes — the confidentiality ratchet (R1-D5) is accepted. |
| Q5 | Yes — declined invitees are also marked before publishing an attendance-mandatory meeting. |
| Q6 | Yes — every mandatory agenda line needs an outcome before publishing. |
| Q7 | Yes — follow-ups and series instances get the subtype's agenda template from the server (a behavior change). |
| Q8 | Yes — Daily skips non-working days (DL-21 therefore needs E5). |
| Q9 | Yes — Repeat on create only for `platform.meetings.series-manage` holders. |
| Q10 | Yes — series attendees are a snapshot at series creation. |
| Q11 | Yes — JSON; the CT converts the manager's Excel. |
| Q12 | Yes — `MTY` (subtype) and `MTC` (main type), class İKİSİ DE. **The meeting-instance prefix is NOT `MTG`:** `MTG-` is already used in code (`frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js:9626`, guard `frontend/Diten.Web/tests/wcn-review-meeting-flow.test.js:53`, CT-measured 2026-10-27); a different prefix is chosen when the meeting number is built. Adding rows to RCS-001 §3 still needs separate owner approval (protected path). |
| Q13, Q14 | CT decisions; recorded with sources before the independent review. |
| Q15 | Yes — S13 (page help and tour) first. |
| Q16 | Yes — templates are tenant data in one language per subtype. |
| Q17 | Open — the QA process owner's name (blocks only the real catalogue import). |
| Q18 | Answered by the CT: the manager's document arrived on 2026-10-27 as a hand-over note on central record coding (RCS-001 / BL-565). It is consistent with R1 (no module-local counter; code field deferred to the shared service). No conflict. |

### 24.26 Live-use readiness (operating card §6.1, S-89)

The data the R1 slices need in the live tenant. **The owner enters the data (shared data). The CT measures it after
entry**, and asks the business questions one at a time (card §6.1 item 2). Part (a) is entered **before** part (b).

**(a) Data from other modules that R1 reads (entered first, on the owning module's screen):**

| Data | Owning module · screen | Owner of the data | Used by |
|---|---|---|---|
| Organization root and organization units | MOD-0288 · `Views/Organization/OrganizationUnits` | HR / organization manager | Organizer and attendee eligibility (no root means no meeting can be created, BL-366) |
| Positions (with codes) | MOD-0288 · `Views/Organization/Positions` | HR / organization manager | Default participants (S19, S22), importer position codes (S24) |
| Position assignments (who holds which position) | MOD-0299 · `Views/Organization/PositionAssignments` (a second screen, `HumanCapital/PositionAssignments`, also exists; plan §3 says a decision is pending on which one stays) | HR | Resolving positions to people (S22); eligibility |
| Users, roles, and the `platform.meetings.*` grants (including `types-manage`, `series-manage`, `read-all`) | MOD-0018 · `Views/Governance/Users`, `Roles`, `Permissions`, `UserRoleAssignments` | Tenant administrator | Every slice (permissions), confidentiality (S15) |
| Task-type catalogue with the codes the meeting catalogue names (including `COM-ACTION`, `FIN-ACTION`, `ITG-ACTION`) | MOD-0024 · `/Tasks/TaskTypes` (and the catalogue load, work list Dalga 2) | Task-type catalogue owner (who loads it is still open, work list §6 Q3) | Default action task type in the importer (S24-7) |
| Working calendar, holidays and the tenant time zone | MOD-0362 · `Views/WorkingCalendar` / `Platform/WorkingCalendars` | Tenant administrator | Title placeholders (S21, S22); Daily skip rule (S21-2, if Q8 is yes) |
| E-mail (SMTP) settings and the background jobs switched on in live | Notifications (platform) · live environment settings (BL-536) | Owner (live configuration) | Invite mail (S15-6); series generation after Repeat (S23) |

**(b) This module's own data (entered after (a)):**

| Data | Screen | Who prepares it |
|---|---|---|
| Main types | `/Meetings/MeetingTypes/Categories` (S20), or in bulk through the importer (S24) | Manager (catalogue) |
| Subtypes with their defaults (title template, duration, recurrence, place, description template, parent rule, sort order) | `/Meetings/MeetingTypes` (S20) or the importer (S24) | Manager / business owner |
| Agenda templates | same | Business owner of each meeting (§5.7 "Kim doldurur") |
| Quality-record, e-signature and attendance-mandatory flags, and the mandatory agenda lines | same | QA process owner (name still open, Q17) |
| Confidential flag (for example HR One-to-One) | same | HR with QA |
| Default participant positions for each subtype | same | HR / organization manager (role → position mapping) |
| Optional recurring series | `/Meetings/Series` or Repeat on create (S23) | Organizers |

Help and tour on these screens are measured by the CT (S13; S20 and S24 for the new pages).

### 24.27 Not verified while drafting

- **Nothing was run** except `verify_module_id.py` (OK). No build, no test and no browser. Every "today" statement
  comes from reading the code at `826150ef4` / `2904f4029`.
- Work list **§6 item 6a** exists only in the uncommitted worktree copy of
  `meetings-mod0357-work-list-2026-10-27.md`. It is not in `2904f4029`.
- Whether `AuditBehavior` (path `a`) accepts a command sent with **no user** (the series sweep and the follow-up path
  send `CreateMeetingCommand` with `Guid.Empty`). S15 must measure this before moving `CreateMeetingCommand` to path
  `a`. If it does not accept it, the slice stops and reports to the CT.
- The server-side source of the tenant time zone for S21 and S22. `TimesheetWeekReader` reads it from working hours
  (`hours.TimeZone`); S21 confirms the same seam.
- The working-calendar read that Daily's skip rule (S21-2) would use.
- Whether a Mongo session or transaction is available to the category and type repositories (S24-9).
- Which administrators can read stored `NotificationDispatch` rows (subject, variables, attachments). This is the
  reason for S15-6, but the reader surface was not opened.
- Whether the shared DataTable v2 standard supports row grouping (S20-2 falls back to a column, a filter and a sort
  order).
- That `TaskTypeDto` (returned by `GetActiveTaskTypesQuery`) carries `Code`. `TaskType` has `Code`; the DTO was not
  opened.
- The help code of the minutes editor page (S13-7).
- The prompt estimates are estimates, not measurements.

---

## 25. Audited Events

> Added by R1 (AUD-001 §10.1 transition rule: the first job that adds or changes a write command writes this section
> for **all** of the module's write commands). The ledger line numbers refer to
> `tests/architecture/audit-ledger/Diten.Platform.md@2904f4029`, section "Bilinen borç". That list **only shrinks**.
> A slice that moves a command to path `a` removes its line in the same commit. **No R1 command may be added to the
> debt list.** "Known debt" below is the measured status today. It is **not** an exception class. Yol `a` = Platform
> `IAuditableCommand` + `IAuditMetadataProvider` (the `platform-merkezi` trace, best effort with a warning log, AUD-001
> §4.2). The audit category is Q13. Confidential meetings: no `Title`, description or agenda text in the metadata.

| Komut | Olay adi | Nesne (tur) | Yol | Onceki/sonraki ya da degisen alanlar | Istisna sinifi + gerekce |
|---|---|---|---|---|---|
| CreateMeetingCommand | meetings.meeting.created | Meeting | today known debt (L76) → **a in S15** | After: Id, MeetingTypeId, StartAt, EndAt, OrganizerUserId, IsConfidential, attendee and agenda counts; Title only when not confidential | — |
| UpdateMeetingCommand | meetings.meeting.updated | Meeting | today known debt (L171) → **a in S15** | Before/after of the changed fields: MeetingTypeId, StartAt, EndAt, Location, Description, IsConfidential (Title rule as above) | — |
| CancelMeetingCommand | — | Meeting | known debt (L69), unchanged in R1 | — | not an exception; R1 does not change the command |
| ReassignMeetingOrganizerCommand | — | Meeting | known debt (L128), unchanged | — | not an exception; unchanged |
| AddMeetingAttendeesCommand | — | MeetingAttendee | known debt (L57), unchanged | — | not an exception; unchanged |
| RemoveMeetingAttendeeCommand | — | MeetingAttendee | known debt (L135), unchanged | — | not an exception; unchanged |
| AddAgendaItemCommand | — | AgendaItem | known debt (L55), unchanged | — | not an exception; unchanged |
| UpdateAgendaItemCommand | meetings.agenda-item.updated | AgendaItem | today known debt (L168) → **a in S18** | Before/after: Text; MeetingId; IsMandatory | — |
| DeleteAgendaItemCommand | meetings.agenda-item.deleted | AgendaItem | today known debt (L95) → **a in S18** | Before: Text, IsMandatory; MeetingId | — |
| ReorderAgendaCommand | — | AgendaItem | known debt (L137), unchanged | — | not an exception; unchanged |
| CreateTaskFromMeetingCommand | — | RecordLink (+ MOD-0024 task) | known debt (L85), unchanged | — | not an exception; unchanged (the task itself has its own trace, `gorev-etkinlik-akisi`) |
| LinkExistingTaskCommand | — | RecordLink | known debt (L124), unchanged | — | not an exception; unchanged |
| ScheduleReviewMeetingForTaskCommand | — | Meeting + RecordLink | known debt (L157), unchanged | — | not an exception; unchanged |
| RespondToInvitationCommand | — | MeetingAttendee | known debt (L141), unchanged | — | not an exception; unchanged |
| SaveMinutesDraftCommand | — | MeetingMinutesVersion | known debt (L155); changed by S17, **stays in debt pending Q14** | — | not an exception; K2 classification is the CT's call (AUD-001 §4.4, §10 K10) |
| PublishMinutesCommand | — | MeetingMinutesVersion | known debt (L126); changed by S16, S18, **stays pending Q14** | — | as above |
| CorrectPublishedMinutesCommand | — | MeetingMinutesVersion | known debt (L72); changed by S16, S17, S18, **stays pending Q14** | — | as above |
| ScheduleFollowUpMeetingCommand | — | Meeting | known debt (L156), unchanged (the meeting it creates is audited through `CreateMeetingCommand` from S15) | — | not an exception; unchanged |
| CreateMeetingSeriesCommand | meetings.series.created | MeetingSeries | today known debt (L77) → **a in S21** | After: Name, MeetingTypeId, Frequency, Interval, StartsAt, EndsAt, OrganizerUserId, TitleTemplate, CreatedFromMeetingId | — |
| UpdateMeetingSeriesCommand | meetings.series.updated | MeetingSeries | today known debt (L172) → **a in S21** | Before/after of the changed fields | — |
| DeleteMeetingSeriesCommand | — | MeetingSeries | known debt (L97), unchanged | — | not an exception; unchanged |
| GenerateDueMeetingSeriesCommand | — | — | known debt (L118), unchanged | — | an İ2 candidate only once every record it produces is audited; `ScheduleFollowUpMeetingCommand` still is not |
| CreateMeetingTypeCommand | meetings.type.created | MeetingType | today known debt (L78) → **a in S15** | After: every type field including the QA flags and MeetingCategoryId | — |
| UpdateMeetingTypeCommand | meetings.type.updated | MeetingType | today known debt (L173) → **a in S15** | Before/after of the changed fields; QA flags named separately | — |
| DeleteMeetingTypeCommand | meetings.type.deleted | MeetingType | today known debt (L98) → **a in S19** | Before: Name, MeetingCategoryId | — |
| CreateMeetingCategoryCommand | meetings.category.created | MeetingCategory | **a** (new, S19) | After: Name, Description, SortOrder | — |
| UpdateMeetingCategoryCommand | meetings.category.updated | MeetingCategory | **a** (new, S19) | Before/after: Name, Description, SortOrder | — |
| DeleteMeetingCategoryCommand | meetings.category.deleted | MeetingCategory | **a** (new, S19) | Before: Name | — |
| BulkDeleteMeetingCategoryCommand | meetings.category.bulk-deleted | MeetingCategory | **a** (new, S19) | Deleted ids; refused ids with reason | — |
| ImportMeetingTypeCatalogueCommand | meetings.type-catalogue.imported | MeetingCategory + MeetingType | **a** (new, S24) | File SHA-256; created, updated and unchanged counts; created ids; updated ids with changed field names | — |

Notes:
- `PreviewMeetingTypeCatalogueImportQuery` is a **query** that writes nothing, so it is not a command row. If it is
  ever implemented as a command, its class is İ5 ("hiçbir kalıcı kaydı değiştirmez — önizleme").
- **K2.** This draft reads the five new commands as configuration (not K2). The CT confirms this under Q14. No R1
  command is written "best effort" into the K2 class.
- `tests/architecture/audit-ledger/Diten.Platform.md` is in `tests/**`. Each slice that moves a command edits it
  (removing lines only), and `AuditTrailStandardTests` must be green in that slice.

---

## 26. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi (SOP §23.1) |
|---|---|---|---|
| DL-1 | Stage 1 corrections only (S1 to S12 delivered) | Implementation Notes | E4 for a state-changing fix · E5 if it crosses a module seam (stated in the WP row) |
| DL-13 | S13 page help and tour | S13 — Page help and Meetings tour | E3 (runtime UI smoke of help panel and tour) + E2 guards |
| DL-14 | S14 honest flags | S14 — Honest display of the type flags | E3 (display only; no state change) + E2 guards |
| DL-15 | S15 confidential type | S15 — Confidential meeting type, Audited Events | E5 (crosses the Task Center projection, notifications and the report) |
| DL-16 | S16 mandatory attendance | S16 — Mandatory attendance before publishing | E4 |
| DL-17 | S17 agenda note and outcome | S17 — Note and outcome per agenda item | E4 |
| DL-18 | S18 mandatory agenda items | S18 — Mandatory agenda items for quality-record types, Audited Events | E4 |
| DL-19 | S19 main type backend | S19 — Main type and subtype default fields (backend), Audited Events | E4 |
| DL-20 | S20 type screens | S20 — Type screens and grouped type picker | E4 |
| DL-21 | S21 daily and series title | S21 — Daily frequency and series title template, Audited Events | E5 (Q8 answered yes: reads the working calendar) |
| DL-22 | S22 create auto-fill | S22 — Create-form auto-fill and agenda at create | E5 (positions and people from the organization and task modules; follow-up and series paths) |
| DL-23 | S23 repeat on create | S23 — Repeat on create | E4 (including a sweep run after create) |
| DL-24 | S24 catalogue importer | S24 — One-off type catalogue importer, Audited Events | E5 (reads the MOD-0024 task-type catalogue and organization positions) |
