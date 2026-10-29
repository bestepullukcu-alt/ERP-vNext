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
> deferred record codes, page help with the Meetings tour, and (owner "ekle-uc") three create-flow additions: agenda at
> create, preparation tasks, and required / optional attendees with agenda presenter and time box. After independent
> review 01 it also adds a meeting access security slice, placed first. They are new slices **S13 to S28**.
> **Approval state of this revision: draft. It needs independent review (SOP §20.4) and owner approval before any
> code.** The frontmatter `status: ready-for-dev` and the S1 to S12 rows in §19 are unchanged.
> `execution/approvals/approvals.tsv` holds **no** approval row for MOD-0357. `reviews.tsv` holds only review 01 of
> this draft (KOŞULLU, `090a620ae`). The S1 to S12 status therefore rests on the owner decisions recorded in §19 and
> §22, not on an approval row. Nothing in R1 authorizes code, a WP or a queue row. **The binding approval state is the
> approval row in `approvals.tsv` for this file's SHA-256, not this sentence.** If the owner approves this version,
> the words "draft / not approved" stay as they are, because changing them would need a new review (SOP §20.4
> item 1). Known record inconsistencies are listed in §24.23 for the CT to reconcile; this revision does not fix them.

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
- [ ] A meeting create retried with the same idempotency key **by a caller who can view the meeting** returns the
      SAME meeting id, no duplicate row. *(Narrowed by R1 fix round 3, review 03 F-19; binding once R1 is approved:
      from S28 on, the replay answers 200, a caller who cannot view the existing meeting gets 409
      `MEETING_CREATE_CONFLICT`, and a creator who would not be able to view the meeting they ask for is refused up
      front with 400 `MEETING_CREATOR_CANNOT_VIEW`, so their own retry never reaches the 409; S28-10. The up-front
      rule is defensive (R1 fix round 4, review 04 F-25): no screen can trigger it, because the create form and the
      Task Center always make the caller the organizer; only a direct API caller who names another organizer and is
      not an attendee reaches it.)*
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
> code.** Every slice in §24 (S13 to S28) is **draft / not approved**. No WP may cite one of them, `@orchestrator` may
> not run one of them, and no queue row may be opened for one of them until the owner approves this revision. The
> existing `ready-for-dev` status of S1 to S12 (§19, §23) does not change. There is no approval row for MOD-0357.
> Review 01 was given to SHA `bd7b82ce…`, and this fixed version needs its own review row and its own approval row
> (`scripts/pack_approval.py`, CT-APR-01 and CT-APR-02). As stated in the header note, the binding state is the
> approval row, not the word "draft" in this text.

### 24.0 Revision log

| Rev | Date | Change | Author · model · effort | Owner instruction |
|---|---|---|---|---|
| R1 | 2026-10-27 | Draft revision. Adds a header note under "Authority", §24 (slices S13 to S24, model decisions, record codes, record inconsistencies, open questions), §25 Audited Events (required by AUD-001 because R1 adds write commands and changes existing ones) and §26 Teslim dilimleri (CT-APR-02). §1 to §23 are not edited. | `module-pack-author` subagent working for CT · Toplantılar. **Model · effort requested (SOP §17.1 r5): Opus · high.** Used: `claude-opus-5-5`. The effort level is set by the dispatcher and cannot be measured from inside the session. | "paket-rev" (owner, 2026-10-27): draft the revision only. Do not commit, approve or write code. |
| R1 (addition) | 2026-10-27 | Three create-flow additions on top of the CT's commit `53dc6a97f`. **S25** takes agenda at create out of S22 and uses today's `AgendaTemplate`. **S26** adds preparation tasks on the create form, which never block anything. **S27** adds required / optional attendees and an agenda presenter and time box. Also updated: §24.2, §24.3 (R1-D13, R1-D14), §24.4, §24.5, §24.6, §24.7 (order and total), S22 (agenda criteria moved to S25), §24.25 (new questions Q19 to Q22 only; the owner-answers block is unchanged), §24.26, §24.27, §25 and §26 (DL-25 to DL-27). | Same subagent and model as R1. | "ekle-uc" (owner, 2026-10-27), sent through CT · Toplantılar: draft only, this pack file only. |
| R1 fix round 1 (review 01) | 2026-10-27 | Fixes all ten findings of independent review 01 (`docs/records/reviews/MOD-0357/2026-10-27-bd7b82ce.md`, KOŞULLU, 1 ENGEL). The CT accepted every finding. See the disposition table below. Adds slice **S28** (meeting access security for writes, and the meetings side of BL-541), placed **first**. Refines R1-D12. Adds R1-D15. Adds Q23 and Q24. The owner-answers block in §24.25 is unchanged. | Same subagent and model as R1 (requested Opus · high; used `claude-opus-5-5`). | "duzelt" (owner, 2026-10-27), sent through CT · Toplantılar: fix all ten findings; draft only, this pack file only. |
| R1 fix round 2 (review 02) | 2026-10-29 | Fixes all seven findings of independent review 02 (`docs/records/reviews/MOD-0357/2026-10-27-ab321ce2.md`, KOŞULLU, 0 ENGEL). The CT accepted every finding. See the "Review 02 disposition" table below. S28 gains S28-10 (the create replay and the follow-up "not new" refusal) and S28-11 (series), and S28-1, S28-2, S28-4, S28-6 and S28-8 are tightened. S23 gains S23-8. S26 gains S26-10. Q24 is answered ("drop"), and Q23 has one wording everywhere. The owner-answers block in §24.25 is unchanged except for the new Q24 row. | `module-pack-author` subagent working for CT · Toplantılar. **Model · effort requested (SOP §17.1 r5): Opus · high.** Used: `claude-opus-5-5`. The effort level is set by the dispatcher and cannot be measured from inside the session. | "duzelt-2" (owner, 2026-10-27/28), sent through CT · Toplantılar: fix all seven findings, and take the default for Q24 ("drop"); draft only, this pack file only. |
| R1 fix round 3 (review 03) | 2026-10-29 | Fixes all seven findings of independent review 03 (`docs/records/reviews/MOD-0357/2026-10-29-289f88b3.md`, KOŞULLU, 0 ENGEL). The CT accepted every finding. See the "Review 03 disposition" table below. S28-11 gains the unchained fallback (a series whose organizer cannot view the chain source keeps generating). S28-10 gains the general up-front creator rule (400 `MEETING_CREATOR_CANNOT_VIEW`, which replaces S26-10's own code) and the follow-up completion exception. S28-1, S28-2, S28-6, S28-8, the S28 tests and the estimate are tightened. S23-8 gains an HTTP test and its own adoption code. R1-D15, §24.5, §24.7, §24.27, S26-10, §25 and §26 DL-28 follow. One wording change outside §24 to §26: the §16 K11 box (F-19). New open question Q25. The owner-answers block in §24.25 is unchanged. | `module-pack-author` subagent working for CT · Toplantılar. **Model · effort requested (SOP §17.1 r5): Opus · high.** Used: `claude-opus-5-5`. The effort level is set by the dispatcher and cannot be measured from inside the session. | "Evet, düzelt" ("duzelt-3", owner GMG-ERP, K3, 2026-10-29), sent through CT · Toplantılar: fix all seven findings; draft only, this pack file only. |
| R1 fix round 4 (review 04 + Q25) | 2026-10-29 | Fixes all four findings of independent review 04 (`docs/records/reviews/MOD-0357/2026-10-29-6207d58c.md`, KOŞULLU, 0 ENGEL, four DÜŞÜK) and writes in the owner's answer to Q25 ("devam-katilimci", 2026-10-29, work list §6 item 6b). The CT accepted every finding, and also the eight corrections of the CT's interim check of the first draft of this round. See the "Review 04 disposition" table below. S28-10 gains the binding Q25 rule (a follow-up scheduler who is a party of the source but not the new organizer is added as an `Accepted` attendee, through a command-only `PreAcceptedUserId` parameter on `CreateMeetingCommand`) and the completion's lifecycle rule (scheduled meetings only). The creator rule is marked defensive (direct API callers only), and S26-10's form hint is dropped. S28 names the sweep unit test that changes, with two new unit rows. S21-6 lists the four sweep outcomes of §25, and S21 now depends on S28. R1-D12 states the deferral that keeps S28's write changes off path `a` until S21 and S25, and S25-7 tests the deferred field. S28-11 narrows "no case stops a series" to visibility and names the recovery. §24.5, §24.7, §24.10 (S15-10), §24.27, §24.28 (S25-7), §24.29 (S26-10), §24.31 and §25 follow. One wording change outside §24 to §26: the §16 K11 box (F-25). In §24.25 only Q25 changes (the question is marked answered, and the owner-answers block gains a Q25 row). | `module-pack-author` subagent working for CT · Toplantılar. **Model · effort requested (SOP §17.1 r5): Opus · high.** Used: `claude-opus-5-5`. The effort level is set by the dispatcher and cannot be measured from inside the session. | "duzelt-4" (owner, 2026-10-29), sent through CT · Toplantılar: fix all four findings; draft only, this pack file only. Q25: "devam-katilimci" (owner, 2026-10-29). |
| R1 fix round 5 (reviews 05 + 06) | 2026-10-29 | Fixes the four findings of independent review 05 (`docs/records/reviews/MOD-0357/2026-10-29-5383fd43.md`, KOŞULLU, 0 ENGEL, four DÜŞÜK: F-29 to F-32) and the five findings of independent review 06 (`docs/records/reviews/MOD-0357/2026-10-29-5383fd43-codex.md`, Codex, KOŞULLU, 0 ENGEL, five ORTA: F-33 to F-37), and writes in the owner's two decisions for F-34 and F-37. The CT accepted every finding. See the "Review 05 disposition" and "Review 06 disposition" tables below. S23's adoption index becomes a **partial** unique index on a real id (F-33). R1-D5 gains the type stamp: a type made confidential stamps its existing meetings, in one Platform transaction (S15-11, F-34). The follow-up's own idempotency link checks the target (S28-10, F-35). S27 keeps one required organizer row through a reassignment, and `ReassignMeetingOrganizerCommand` moves to path `a` in S27 (S27-8, F-36). A confidential meeting is an HR-confidential record, and its single opens write a central read-audit record, fail-closed (S15-12, §25 read rows, F-37). The follow-up path applies the creator rule to a non-party scheduler (F-29). S25 builds a post-handler audit carrier and measures it (F-30). S28-10 gains the `PreAcceptedUserId` body and reflection tests and a written scan rule (F-31), and S28-8 gains the missing rows (F-32). §24.3 (R1-D5, R1-D12, R1-D14, R1-D15), §24.4, §24.5, §24.7 (estimates and total), §24.10, §24.16 (S21-6), §24.18, §24.27, §24.28, §24.30, §24.31, §25 and §26 follow. In §24.25, the owner-answers block gains two owner-decision rows (F-34, F-37), and new open question Q26 (shared read-audit writer, XMC-001) is added. Nothing outside §24 to §26 changes. | `module-pack-author` subagent working for CT · Toplantılar. **Model · effort requested (SOP §17.1 r5): Opus · high.** Used: `claude-opus-5-5`. The effort level is set by the dispatcher and cannot be measured from inside the session. | "Evet, düzelt" ("duzelt-5", owner, 2026-10-29), sent through CT · Toplantılar, accepting this button text verbatim: "duzelt-5: F-29…F-37 tek turda düzeltilsin; F-34 için tür gizli yapılınca eski toplantılar kalıcı gizli işaretlensin, F-37 için gizli toplantı İK-gizli sayılsın ve tekil açılışı denetlensin." Two owner decisions: (1) F-34, a type made confidential marks its existing meetings confidential for good; (2) F-37, a confidential meeting counts as HR-confidential and its single open is audited. Draft only, this pack file only. |
| R1 fix round 6 (pre-review 1) | 2026-10-29 | Handles the eight findings of pre-review 1 (S-93), F-38 to F-45: six (F-38, F-39, F-40, F-42, F-43 and F-45) are written in, F-41 becomes the open question **Q27** (not decided here), and F-44 is disputed by the CT and not applied. A pre-review is not a formal review and has no `reviews.tsv` row. See the "Pre-review 1 disposition" table below. A write response of a confidential meeting never returns stored content the request did not carry, and the report masks the decision text of confidential meetings (S15-12, F-38). The importer calls the type stamp in its own transaction (S24-6, S24-9, F-39). Rows that touch a confidential meeting carry field names only, and a content value is never written when the carrier is red (S15-7, S25-7, §25, F-40). The stamp also runs when a type stops being confidential, which closes the race with a concurrent create or type change (S15-11, R1-D5, F-43). A create event's `EntityId` comes from an id generated on the command, and S25-7 measures it (F-42). The follow-up dialog does not offer confidential subtypes to a scheduler who is not a party of the source (S15-10, S28-10, F-45). §24.3 (R1-D5, R1-D12), §24.4, §24.5 (creator row), §24.7 (S15 estimate, Q27 dependency, total), §24.10, §24.13 (S18 audit), §24.19, §24.25 (Q27), §24.27, §24.28, §24.30 (S27-7), §24.31 (S28-10, failure paths), §25 and §26 (DL-15, DL-24) follow. Nothing outside §24 to §26 changes. | `module-pack-author` subagent working for CT · Toplantılar. **Model · effort requested (SOP §17.1 r5): Opus · high.** Used: `claude-opus-5-5`. The effort level is set by the dispatcher and cannot be measured from inside the session. | "Önce düzelt (Önerilen)" (owner, 2026-10-29), sent through CT · Toplantılar, with this button text: "Yazar alt ajanı F-38…F-45'i pakete yazar (F-44 hariç; F-41 için size soru getirir), commit edilir, ipucusuz ön inceleme 2 yapılır." Draft only, this pack file only. |

**Review 01 disposition** (findings at SHA `bd7b82ce…`; all accepted by the CT, none disputed):

| Finding | Severity | What changed | Where |
|---|---|---|---|
| F-1 | ENGEL | New slice **S28**, first in the order. Every write handler on an existing meeting applies the same visibility rule as reads (`CanView`) through one helper and answers 404 to a caller who cannot view the meeting. This is stated as a hole for **all** meetings today, not only confidential ones. A follow-up needs a viewable source. The confidentiality-inheritance rule is bound here and built in S15 (S15-10). S15 now depends on S28, and its matrix covers writes. | §24.31 (S28); §24.10 (S15-3, S15-10, depends); §24.7; §26 DL-28 |
| F-2 | ORTA | `GenerateDueMeetingSeriesCommand` moves to path `a` in S21. `ScheduleFollowUpMeetingCommand` moves to path `a` in S25, because S25 changes how its agenda is ordered. S25 now names `MeetingFollowUpCommandHandlers.cs` and fixes the `SortOrder` collision: carried lines first, then mandatory, then template. S18-2 refers to that order. R1-D12 now states that an **access-check-only** change (S28) does not trigger the move. | §24.3 R1-D12; §24.16 (S21); §24.28 (S25-5, S25-8); §24.13 (S18-2); §25 |
| F-3 | ORTA | Q13 and S25 now name the shared mirror `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs` and its drift test `CentralAuditDispatcherTests.cs:155` (`The_platform_enum_mirror_matches_AuditEnums_cs`). Editing the shared library is outside the module and needs separate owner approval (XMC-001, AGENTS §4) before S25 starts. | §24.25 Q23; §24.28 (S25 files, S25-7); §24.6 |
| F-4 | ORTA | The meetings side of BL-541 is closed inside S28. Report and export mask the title, assignee and due date of tasks the reader may not read, through `ITaskReadAccessPolicy.ReadableTaskIdsAsync`, an existing MOD-0024 service already used by Meetings. `MeetingRelatedRecordResolver` drops meetings the reader cannot view. No `Features/Tasks/**` edit. | §24.31 (S28-6, S28-7); §24.2 H; §24.24 |
| F-5 | DÜŞÜK | The hidden dependencies are now explicit: S15 → S25 and S28; S19 → S15; S27 → S18. Q13 says "the first slice that writes a meeting audit row" (S25 in the §24.7 order). | §24.7; slice "Depends on" lines; §24.25 Q13 note |
| F-6 | DÜŞÜK | `WorkAggregation/Calendar/GetMyWorkCalendarHandler.cs` is added to the S15-9 allow list. S15-6 now also covers the attendee-removed and organizer-changed mails. | §24.10 |
| F-7 | DÜŞÜK | The main-type unique index is **partial** (non-deleted rows only), with a test that creates, deletes, re-creates and deletes again. Today's subtype index has the same defect; this is recorded as a follow-up and a DL-1 candidate. | §24.4; §24.5; §24.14 (S19-1, S19-9); §24.24; §26 DL-1 |
| F-8 | DÜŞÜK | §26 now has one evidence level per row. DL-1 is E4, and a correction that crosses a module seam needs its own WP row at E5. DL-13 and DL-14 are E3. DL-16, 17 and 18 add persistence `L3` (quality-record minutes; SOP §23.1 on `origin/main`). | §26 |
| F-9 | DÜŞÜK | S26 retries: the preparation lines are sent on **every** create call, including a retry that finds the meeting already written (`isNew = false`). Each line uses the `ResolveForTaskBridge` key, so nothing is created twice and an interrupted first call is completed. | §24.29 (S26-2, S26-4) |
| F-10 | DÜŞÜK | All seven stale texts are fixed: (1) the Q19–Q22 header and S27-3; (2) S18-5 and S21-2 (owner answers Q6 and Q8); (3) Q7 now refers to S25-5; (4) S13-6 now says S25; (5) 6a is committed in `53dc6a97f`; (6) the header and band no longer claim approvals that do not exist, and they state that the binding state is the approval row; (7) the §24.6 MOD-0024 consumption list now includes the bridge command and `ITaskReadAccessPolicy`, and §24.2 now separates S26's reminder template from the deferred agenda-in-mail change. | header note; §24 band; §24.1; §24.2; §24.6; §24.8; §24.13; §24.16; §24.25; §24.27; §24.30 |

**Review 02 disposition** (findings at SHA `ab321ce2…`; all accepted by the CT, none disputed; owner "duzelt-2"):

| Finding | Severity | What changed | Where |
|---|---|---|---|
| F-11 | ORTA | **S28-10.** The create path's replay branch (`isNew = false`; the key is organizer, type, start, end and title, with no caller, `MeetingIdempotencyKeyResolver.cs:9-22`) returns the existing meeting only to a caller who passes `CanView` on it. Any other caller gets one fixed 409 `MEETING_CREATE_CONFLICT` body that carries no id and no field, and nothing is written. A replay to a viewer answers 200, and a new row answers 201. `ScheduleFollowUpMeetingHandler` refuses every meeting that was not newly written, with the same 409 body, before it writes a link or a carried line. Both cases are in the S28-8 matrix with sabotage proofs. S25-6, S26-2 and S26-4 now rely on this rule. | §24.31 (S28-10, S28-8, tests); §24.28 (S25-6); §24.29 (S26-2, S26-4); §24.5; §25 |
| F-12 | ORTA | **S28-6.** The report's "current meeting" title (`titleById` is built from all meetings, `MeetingReportQueryHandlers.cs:63, 122-133`) is shown only when `CanView` passes for the reader. Otherwise it becomes the placeholder code `MEETING_NOT_ACCESSIBLE` and the id is not rendered as a link. The section has a test and a sabotage proof. S15-4 refers to it. | §24.31 (S28-6, tests); §24.10 (S15-4) |
| F-13 | ORTA | **S28-11.** The sweep no longer bypasses the check. Its system-origin marker now names an acting identity, the **series organizer**, and `CanView` is applied with that identity (organizer or attendee only, no `read-all`). `UpdateMeetingSeriesHandler` (`MeetingSeriesCommandHandlers.cs:167-181`) keeps its key. The rule S28 applies is that nothing a series edit writes is trusted for access to a meeting. `CreatedFromMeetingId` is server-only: it is a command parameter that is never on an HTTP request model, and adoption applies `CanView` (S23-8). The S28-1 exception text and the S28-2 field list (now including `CreatedFromMeetingId` and `FollowUpOfMeetingId`) are updated, and the single-file marker guard is kept (S28-4). | §24.3 R1-D15; §24.31 (S28-1, S28-2, S28-4, S28-11); §24.18 (S23-8, depends); §24.27; §25 |
| F-14 | DÜŞÜK | **S28-1.** The access check runs immediately after the load and before any other check (409 state, the attendee-revealing 400). A refused caller gets **that handler's own** not-found body, and a test measures it byte for byte for each handler. The `RespondToInvitation` exception is justified, and its attendee lookup moves ahead of its 409. `CreateMeeting`'s `FollowUpOfMeetingId` check uses `CanView` (S28-10). The S28-2 scan is **class level**, and the matrix gains rows for cancelled and completed meetings. | §24.31 (S28-1, S28-2, S28-8, S28-10, tests); §25 |
| F-15 | DÜŞÜK | **S28-6.** In the export, both masked values (`TASK_NOT_ACCESSIBLE` and `MEETING_NOT_ACCESSIBLE`) are written as localized text in 7 languages, through one new `Services/MeetingReportExportPlaceholders.cs` that follows the `MeetingReportExportDisclaimer.cs:21-30` locale pattern. There is a test for each locale and a sabotage proof. | §24.31 (files, S28-6, tests, L10n) |
| F-16 | DÜŞÜK | The §24.7 cells now name S28 for S16, S17, S26 and S27 (and S23, from F-13), and the slice "Depends on" lines match. **S26-10:** when the creator could not view the meeting about to be created, a request with preparation lines is refused up front (400 `MEETING_PREPARATION_CREATOR_CANNOT_VIEW`) before anything is written. | §24.7; §24.11; §24.12; §24.18; §24.29 (S26-10, failure paths, depends); §24.30; §24.5 |
| F-17 | DÜŞÜK | Q24 is answered "drop" (owner, "duzelt-2") in the owner-answers block, and no text calls it open any more. Q23 has one wording everywhere it appears: "S25 does not start until Q23 is answered; yes → the shared mirror gets `["Meetings"] = 18` (written by this CT after the owner's explicit XMC-001 answer, reviewed by the library owner, per card S-90 §7c); no → `PlatformConfiguration` is used and Q13 is reopened." | §24.25 (Q13 note, Q23, Q24, answers table); §24.6; §24.7; §24.22; §24.24; §24.28 (files, S25-7, depends); §24.31 (S28-7); §25 |

**Review 03 disposition** (findings at SHA `289f88b3…`; all accepted by the CT, none disputed; owner "Evet, düzelt" = "duzelt-3"):

| Finding | Severity | What changed | Where |
|---|---|---|---|
| F-18 | ORTA | **S28-11, binding rule: the unchained fallback.** The sweep checks the chain source with the series organizer (marker). When the organizer cannot view it, the occurrence is still created, as a plain unchained meeting through the first-instance `CreateMeetingCommand` (`FollowUpOfMeetingId` null, no follow-up link, no carried line, no task link), `LastGeneratedMeetingId` moves to it, and a `meetings.series.chain_broken` warning is logged. A failed fallback create only misses that occurrence, so no case stops a series for good. The matrix has an attack row and a legitimate hand-over row (handler level, real Mongo), with sabotage proofs (round 2's "failed" rule turns the hand-over row red; a fallback that chains turns the attack row red). R1-D15 states the rule; §25 adds the outcome `created-unchained`. | §24.3 R1-D15; §24.31 (measured, files, S28-4, S28-8, S28-11, failure paths, tests); §25 |
| F-19 | DÜŞÜK | **Chosen: the general up-front rule**, not acceptance of the 409. `CreateMeetingHandler` refuses a creator who would not be able to view the meeting they ask for with 400 `MEETING_CREATOR_CANNOT_VIEW` before anything is written. This is S26-10's rule made general, and S26-10's own code is dropped. Why: without it, an assistant's own retry gets 409 for a meeting their call wrote, and the form sends them to a page they cannot open; with it, the refusal is clear and comes first, and a creator's retry answers 200. Not applied on the follow-up path (the dialog sends no attendees), which is guarded by a source-scan test and raised as Q25. The K11 box (§16) is narrowed to "a caller who can view the meeting". Tests: the creator-rule row (400, nothing written; with the creator as attendee 201, then 200 on retry). | §16 K11; §24.31 (S28-10, S28-8, files, tests); §24.29 (S26-10, failure paths, tests, depends); §24.5; §24.25 Q25; §25 |
| F-20 | DÜŞÜK | S28-1 and S28-8 name the two tests that change, `MeetingInvitationTests.cs:119-120` and `MeetingTwoUserFlowMongoTests.cs:119-120`, and their new expected code (404 `MEETING_NOT_FOUND`), and the Meetings page message change (`errorMeetingNotFound`). The "201 retry test" sentence is corrected: no such test exists, and the 201 assertions on first calls stay. The hand-built handler constructions that change are named (14 files, about 40 constructions). S28 is re-estimated at 3 prompts; §24.7 and the total are updated (25 to 30, now the exact sum of the cells). | §24.31 (measured, files, S28-1, S28-8, S28-10, estimate); §24.7 |
| F-21 | DÜŞÜK | **S28-6.** When `CanView` fails for the current meeting, `CurrentMeetingId` is null (the field becomes `Guid?`) in the response and in the export, next to the placeholder title. The test checks the CSV cell and the JSON value; sabotage "keep the id" turns it red. | §24.31 (files, S28-6, tests) |
| F-22 | DÜŞÜK | (a) `MeetingCommandHandlers.cs` holds seven handler classes and the static `MeetingEligibility`. (b) S28-1 lists every handler by class name, and the exception list is exactly five classes: `RespondToInvitationHandler`, `UpdateMeetingSeriesHandler`, `DeleteMeetingSeriesHandler`, `UpdateMeetingTypeHandler`, `DeleteMeetingTypeHandler`, each with its reason. (c) The S28-2 scan uses reflection for scope and the Roslyn syntax tree per class for the call, so a call in another class of the same file does not count (new test-only package `Microsoft.CodeAnalysis.CSharp`). (d) The series rows of S28-8 run at handler level with real Mongo, not over HTTP (DL-28 says so too). (e) R1-D15 no longer says "everyone gets 404"; the review-meeting replay sentence and the `form.js` sentence are corrected. | §24.3 R1-D15; §24.31 (measured, S28-1, S28-2, S28-8, S28-10); §26 DL-28 |
| F-23 | DÜŞÜK | **S23-8.** An HTTP test posts extra body fields (`createdFromMeetingId` and other names) to the series create endpoint and asserts that nothing is adopted; sabotage binds the field. A refused adoption is normally impossible after S28-10's up-front rule; the remaining concurrent case reports its own code `MEETING_SERIES_ADOPTION_REFUSED`, never `MEETING_NOT_FOUND`. | §24.18 (S23-8, tests); §24.5 |
| F-24 | DÜŞÜK | **S28-10, follow-up.** A caller who can view the existing meeting gets the narrow completion when its `FollowUpOfMeetingId` is the source and the follow-up link is missing (the link and the open lines are written idempotently, 200), 200 with nothing written when the link exists, and 409 `MEETING_FOLLOW_UP_CONFLICT` with only that meeting's id otherwise. The fixed body stays only for a caller who cannot view it. Behaviour change written down: the sweep no longer chains onto a manually created meeting with the same five values; that occurrence fails, and the next one chains again. | §24.31 (S28-10, S28-8, failure paths, tests); §24.5; §25 |

**Review 04 disposition** (findings at SHA `6207d58c…`; all accepted by the CT, none disputed; owner "duzelt-4"; and the owner's Q25 answer "devam-katilimci"):

| Finding | Severity | What changed | Where |
|---|---|---|---|
| F-25 | DÜŞÜK | The creator rule (400 `MEETING_CREATOR_CANNOT_VIEW`) is marked **defensive**. The create form always sends `organizerUserId: null` (`form.js:174`; `Views/Meetings/_Form.cshtml` has no organizer field), so the organizer is the caller (`MeetingCommandHandlers.cs:115`), and the Task Center's review meeting also names the caller as organizer (`MeetingTaskBridgeCommandHandlers.cs:457-468`). Only a direct API caller who names another organizer reaches the rule. The behaviour-change sentence now says "direct API caller". S26-10's form hint is dropped, so there is no dead text in 7 languages. The reason-code bridge text stays, because the bridge guard requires every `MeetingReasonCodes` value. The K11 box says the rule is defensive. The review 03 F-19 row above is kept as history, and this row replaces its "assistant … form" reasoning. | §16 K11; §24.5; §24.29 (S26-10, failure paths); §24.31 (S28-10, S28-8, failure paths) |
| F-26 | DÜŞÜK | The follow-up completion runs only on a **scheduled** meeting. A cancelled or completed follow-up of the source that has no link gets the existing lifecycle 409 (`MEETING_CANCELLED` / `MEETING_COMPLETED`, `UpdateMeetingHandler.CheckEditable`, the answer every agenda write gives), and nothing is written. There are matrix rows for both cases and a sabotage proof. | §24.5; §24.31 (S28-10, S28-8, failure paths, tests); §25 |
| F-27 | DÜŞÜK | `GenerateDueMeetingSeriesHandlerTests.cs` is named among the tests that change (the hand-built constructor at `:47`; the chain test at `:71-88` seeds its source). There are two new unit rows: when the source is visible, `ScheduleFollowUpMeetingCommand` is sent; when it is not, `CreateMeetingCommand` is sent and the pointer moves on. S28 stays at 3 prompts. | §24.31 (files, S28-8, tests, estimate); §24.7 |
| F-28 | DÜŞÜK | (a) S21-6 lists the four outcomes of §25 (created, created-unchained, skipped, failed). (b) R1-D12 states why S28's three write changes (the unchained fallback, the follow-up completion and the scheduler attendee) do not move their commands in S28: no event exists in S28, S21 and S25 move the commands, and none of the changes writes a new kind of data. The S28 audit paragraph and §25 follow. (c) S28-11 now says that no **visibility** rule stops a series. An organizer who is no longer an active tenant user still fails every occurrence, as today (`MeetingCommandHandlers.cs:116-121`), and the recovery is to change the organizer, to an active tenant user, through `UpdateMeetingSeries`. The review 03 F-18 row above ("so no case stops a series for good") is kept as history and is superseded by this row. Because S21-6 now tests `created-unchained`, S21 depends on S28 (no cycle: S28 depends on nothing). | §24.3 R1-D12; §24.7; §24.16 (S21-6, depends); §24.28 (S25-7); §24.31 (S28-11, audit); §25 |
| Q25 | owner answer | **S28-10, binding:** when the scheduler of a follow-up is a **party of the source** (its organizer, or a user with an attendee row on it) and not the new meeting's organizer, `ScheduleFollowUpMeetingHandler` adds the scheduler to the attendees of the create it sends, as **`Accepted`** through a command-only `PreAcceptedUserId` parameter on `CreateMeetingCommand` (never on the HTTP request model). `Accepted` is the CT's default, because the owner said "added as attendee" without a response status: a `Pending` row would put an invite card for their own follow-up on the scheduler's Task Center (`MeetingWorkItemProvider.cs:61, 78-82, 123-127, 161`), count it as pending in their work calendar (`GetMyWorkCalendarHandler.cs:132, 138`) and leave it out of time planning (`MeetingGatewayAdapter.cs:27-28`), which is why the organizer's own row is `Accepted` (`MeetingCommandHandlers.cs:157-167`). A `read-all` holder or platform operator who is not a party is **not** added, so the report's invited count (`MeetingReportQueryHandlers.cs:73-77`) and S16's mandatory attendance are not skewed. User calls only, never the sweep. `CreateMeetingHandler`'s eligibility filter and its new-meeting-only rule apply unchanged. The scheduler gets no invite mail and no `.ics` ATTENDEE line (actor exclusion). A confidential follow-up keeps the scheduler (S15-10). `RequireCreatorCanView: false` stays. Tests and sabotage proofs are added, and S25-7 tests the deferred event field. | §24.25 (Q25, answers table); §24.31 (files, S28-10, S28-8, tests, audit); §24.10 (S15-10); §24.28 (S25-7); §24.3 R1-D12; §24.27; §25 |

**Review 05 disposition** (findings at SHA `5383fd43…`; all accepted by the CT, none disputed; owner "duzelt-5"):

| Finding | Severity | What changed | Where |
|---|---|---|---|
| F-29 | DÜŞÜK | **The narrow fix.** On the follow-up path, `ScheduleFollowUpMeetingHandler` passes `RequireCreatorCanView: true` when the caller is **not a party** of the source (a `read-all` holder or platform operator), and `false` for a party and under the system-origin marker. For a non-confidential new meeting the creator rule then passes through `read-all`, as today; when the new meeting would be confidential (from S15, a confidential subtype chosen in the request), `read-all` does not count, so such a caller who is neither the requested organizer nor a requested eligible attendee gets 400 `MEETING_CREATOR_CANNOT_VIEW` before anything is written. The sentences that said a `read-all` scheduler "can always open" the new meeting are corrected. Rows: a handler-level row in S28-8 (the parameter value per caller kind) and an HTTP row in S15 (the 400), each with a sabotage. | §24.5; §24.10 (S15-10, tests); §24.31 (S28-10, S28-8, failure paths, tests) |
| F-30 | DÜŞÜK | **Chosen: a post-handler result carrier, measured in S25-7.** `AuditBehavior` builds its plan before the handler (`AuditBehavior.cs:47, 143`) but reads the request's metadata dictionaries only when it builds the append, after the handler (`:74-80`, `:179-180`, `MergeMetadata` at `:268-290`). S25 adds one Meetings type, `MeetingAuditFacts`, which a command hands to `GetAuditMetadata()` and its handler fills. S25-7 measures, through the real `AuditBehavior`, that a field the handler set is in the stored record; if it is not, the slice stops and reports, and §25 says what then leaves the events. The same carrier holds the other handler facts §25 lists (the new `MeetingId`, the carried line count, `created-unchained`, a stored before-value, the stamp count of F-34), so no row needs a hidden read. R1-D12's deferral now names the carrier. Dropping the fields was rejected: it would also drop `created-unchained` (S21-6, review 04 F-28 a), which is what makes the deferral of the fallback meaningful. | §24.3 R1-D12; §24.16 (S21-6); §24.28 (files, S25-7, tests); §25 |
| F-31 | DÜŞÜK | **S28-10.** Two tests and a written scan rule: (a) an HTTP test sends an extra `preAcceptedUserId` naming an invited user to `POST /` and to `POST /{id}/follow-up`, and that user's row stays `Pending`; (b) a reflection test asserts that no HTTP request model of the module has a member named `PreAcceptedUserId` (case-insensitive). The source scan is a Roslyn syntax scan whose counted forms are written down; sabotage: add the member to `CreateMeetingRequest` and pass it through. | §24.31 (S28-10, tests) |
| F-32 | DÜŞÜK | **S28-8, four rows with sabotages.** (a) The organizer branch of the party rule: a source organizer without an attendee row schedules a follow-up naming another organizer and is added `Accepted`. (b) A non-party who lists themself in `attendeeUserIds` gets a `Pending` row; **accepted as stated** (the same answer a creator who lists themself gets on `POST /` today; no screen sends it). (c) "Link exists" answers 200 for a cancelled and a completed follow-up, and a cancelled meeting that is not a follow-up of the source gets `MEETING_FOLLOW_UP_CONFLICT`, not a lifecycle code. (d) A party outside the eligibility filter gets no row, and the follow-up is still created. | §24.31 (S28-8, tests) |

**Review 06 disposition** (findings at SHA `5383fd43…`, Codex; all accepted by the CT, none disputed; owner "duzelt-5" with two decisions):

| Finding | Severity | What changed | Where |
|---|---|---|---|
| F-33 | ORTA | The adoption index is a **partial** unique index `(TenantId, CreatedFromMeetingId)` whose filter matches only a real id, `{ CreatedFromMeetingId: { $type: "binData" } }`. Platform writes a `Guid` as BSON binary (`GuidRepresentation.Standard`, `PlatformBsonSerializers.cs:29`; `DependencyInjection.cs:291`) and a null `Guid?` as BSON null (only the ignore-extra-elements convention is registered, `PlatformBsonConventions.cs:46-49`), so a sparse index or a `$exists: true` filter would still index the nulls (the same Mongo rule the `RecordLink.IdempotencyKey` comment records, `PlatformSchemaManifest.Meetings.cs:33-37`). Real-Mongo test with two legacy and two new unadopted series in one tenant, a double adoption that fails, and a sabotage. | §24.4; §24.18 (files, S23-4, tests); §26 DL-23 |
| F-34 | ORTA | **Owner decision (duzelt-5): the type stamp.** When `UpdateMeetingType` turns `IsConfidential` from false to true, the same call sets `Meeting.IsConfidential = true` on every meeting of that type (every lifecycle state, soft-deleted rows included) and bumps each row's `Version`, in **one Platform transaction** with the type update (`IPlatformTransactionExecutor`, the BL-533 path `PublishMinutesHandler` uses), so no half state can exist. Turning the type off later exposes none of them. The audit row carries the stamp count through S25's carrier. Matrix test false → true → false, a stale-edit row and a sabotage. The S28-1 exception reason for `UpdateMeetingTypeHandler` is corrected (it now writes meeting rows, and why `CanView` still does not apply). | §24.3 R1-D5; §24.4; §24.10 (files, S15-11, failure paths, tests, estimate); §24.25 (owner-decision row); §24.31 (S28-1); §25 |
| F-35 | ORTA | **S28-10.** The follow-up's own idempotency link (`MeetingFollowUpCommandHandlers.cs:68-77`) loads its **target** through `MeetingWriteAccess` before it reads the target's agenda. A caller who cannot view the target gets the fixed 409 `MEETING_CREATE_CONFLICT` body, the answer the follow-up path already gives when its create replay lands on a meeting the caller cannot view, so the caller cannot tell which replay path answered. HTTP test (source still visible, target access removed) and a sabotage. | §24.3 R1-D15; §24.5; §24.31 (S28-1, S28-10, S28-8, failure paths, tests) |
| F-36 | ORTA | **S27-8.** `ReassignMeetingOrganizerHandler` makes sure the new organizer has exactly one attendee row, required and `Accepted`, creating it when they were not an attendee, in one Platform transaction with the organizer change. The previous organizer's row stays an ordinary required attendee row. `ReassignMeetingOrganizerCommand` therefore moves to path `a` in S27 (R1-D12), and its ledger line L128 is removed there. Tests for an optional attendee, a non-attendee and the attendance-mandatory publish, with a sabotage. | §24.3 R1-D14; §24.30 (files, S27-7, S27-8, failure paths, tests, estimate); §24.7; §25; §26 DL-27 |
| F-37 | ORTA | **Owner decision (duzelt-5): a confidential meeting counts as an HR-confidential record** (AUD-001 §7, third class). Every single open of its content (`GET /{id}`, `GET /{id}/minutes`, `GET /{id}/tasks`; there is no single-meeting export or print in the code) writes a central read-audit record before the content is returned, and refuses with 503 `MEETING_CONFIDENTIAL_READ_NOT_RECORDED` when it cannot (fail-closed, the `IDataExportAuditWriter` posture). The shared writer is another owner's area, so its shape is recommended in new question **Q26** (XMC-001), with a module-local fallback. Read rows in §25, a real audit-record test and a sabotage. | §24.10 (files, S15-12, failure paths, tests, L10n, depends, estimate); §24.25 (Q26, owner-decision row); §24.7; §25; §26 DL-15 |

**Pre-review 1 disposition** (pre-review 1 (S-93) F-38 to F-45, at SHA `f561c04e…`; not a formal review and not in
`reviews.tsv`; owner "Önce düzelt (Önerilen)"; every claim was checked against the code before it was written in):

| Finding | Severity | What changed | Where |
|---|---|---|---|
| F-38 | ORTA | **Accepted, all three paths measured.** (a) `PublishMinutesRequest` carries only `ExpectedVersion` (`MeetingModels.cs:409`), and `PublishMinutesHandler` returns the stored draft (`MinutesCommandHandlers.cs:245-307`). (b) The report screen puts the decision text `d.Text` of published minutes into its rows (`MeetingReportQueryHandlers.cs:93-97`). (c) The create replay returns the existing meeting's description and location (`MeetingCommandHandlers.cs:151-152, 194-203`). **Chosen: exclusion by withholding, not a read record.** On a confidential meeting, the three minutes write responses and the create replay carry `ContentWithheld = true` and no stored content the request did not carry, and the report and export show the placeholder `MEETING_CONFIDENTIAL_CONTENT` in place of the decision text. Why: the screens do not use these bodies (the minutes editor reloads after save, publish and correct, `minutes-editor.js:294-300, 307-313, 339-346`; the create form reads only `data.id`, `form.js:194`), so the content is opened only through the audited `GET` calls; a fail-closed record on a write response would answer 503 after the write committed; and a record per confidential meeting on every report load would fail the whole report when one record fails. The "Not covered" reasoning of S15-12 is corrected. Each path has a test on the real outbox (no read record from that call, no content in the body) and a sabotage. **Needs owner approval with this revision.** | §24.10 (files, S15-12, tests, L10n) |
| F-39 | ORTA | **Accepted.** S24-6 lets a file change an existing subtype's `isConfidential`, and the stamp was bound only to `UpdateMeetingTypeHandler` (`MeetingTypeCommandHandlers.cs:57-93`). **Chosen: the importer calls the same stamp in its S24-9 transaction**, for every subtype whose flag changes (in either direction, F-43), not a refusal: the catalogue is the bulk path for the HR one-to-one flag. One shared stamp method serves both handlers. Test: three imports false → true → false, and a sabotage. The §25 import row carries the stamp count. | §24.3 R1-D5; §24.4; §24.10 (S15-11); §24.19 (files, S24-6, S24-9, tests); §25 |
| F-40 | ORTA | **Accepted.** On every §25 row that touches a meeting whose effective flag is true, changed content fields are written **by name only**; no value of `Title`, `Description`, `Location` or agenda `Text` is written, before or after. A content value reaches a record only through the carrier, filled by a handler that read the effective flag as false; a command never puts one in its own metadata. So when the S25-7 carrier measurement is red, no content value is written for any meeting. Real-record tests and sabotages in S15 (meeting rows), S18 and S27 (agenda rows). | §24.10 (S15-7, tests); §24.28 (S25-7); §25 (header, rows 3807-3815 at `f561c04e`) |
| F-41 | ORTA | **Not decided here (CT instruction).** New open question **Q27**: the K2 class of writes on a confidential meeting, with three options and a recommendation. S15 does not start until Q27 is answered. The §25 K2 note no longer asserts an answer. | §24.25 (Q27); §24.7; §24.10 (depends); §25 (notes) |
| F-42 | DÜŞÜK | **Accepted.** `AuditRequestMetadata.EntityId` is a `Guid?` fixed when the plan is built (`AuditRequestMetadata.cs:9`; `AuditBehavior.cs:47, 143, 168-187`), so the carrier cannot fill it. A command that creates a record carries a server-only id generated when the command is built (`NewMeetingId` on `CreateMeetingCommand` and `ScheduleFollowUpMeetingCommand`, which passes its own to the create), and the handler writes the new row with it. The sweep is one command over up to 200 series (`GenerateDueMeetingSeriesHandler.cs:30, 48, 59`), so its record is a batch record with `EntityId` null, as Platform's bulk commands write today; each occurrence has its own create record. S25-7 measures `EntityId` too. | §24.28 (S25-7, tests); §24.31 (S28-10 reflection test); §25 (header, rows) |
| F-43 | DÜŞÜK | **Accepted, with a different fix than the two named.** A re-read of the type before the insert leaves a window, and under MongoDB snapshot isolation the stamp's `UpdateMany` does not see an insert that commits after its transaction started, so no create-side check alone closes it. **Chosen: the closing stamp.** The same `UpdateMany` also runs when a type's flag goes true → false, before the flag is lowered, in the same transaction. While the type is confidential, a meeting written `false` by the race is still hidden by the effective flag (R1-D5), and the lowering write is the only way the type can stop protecting it. This also covers `UpdateMeeting` moving a meeting into a type that was just made confidential. Race test with a forced interleaving and a sabotage. | §24.3 R1-D5; §24.4; §24.10 (S15-11, tests); §25 |
| F-44 | DÜŞÜK | **Disputed by the CT, not applied:** the test exists at `services/Diten.Building.Blocks/Diten.BuildingBlocks.CentralAudit.Tests/CentralAuditDispatcherTests.cs:155` (`The_platform_enum_mirror_matches_AuditEnums_cs`); the pre-reviewer's ZIP lacked the test file. The author checked the line at `a846b746a`: the method is at line 155, with `[Fact]` at line 154. | — |
| F-45 | DÜŞÜK | **Accepted.** From S15, a `read-all` scheduler who is not a party of the source can reach 400 `MEETING_CREATOR_CANNOT_VIEW` from the follow-up dialog, which lists every type (`follow-up-dialog.js:55-59`) and has no attendee field (`:91-93`). **Chosen:** the detail response gains a server-computed `CallerIsParty`, and the dialog does not offer confidential subtypes when it is false; the code's message no longer says only "add yourself as an attendee". The S28-10 sentence "No screen can trigger it today" is corrected. JS test and a sabotage. | §24.10 (files, S15-10, tests, L10n); §24.31 (S28-10) |

### 24.1 Sources of the decisions (read; cited as path@commit)

| Source | What it decides for R1 |
|---|---|
| `docs/roadmap/plans/post-closure-program-2026-10-12.md@826150ef4` §5.1, §5.2, §5.5, §5.6 | The manager's catalogue (23 main types, 46 subtypes, per-subtype default fields), the measured gaps (three type flags that "do nothing today"), and the create-form complaint (no agenda or repeat on create; `form.js` applies the agenda after creation). |
| same file §5.7 (**binding**, manager and owner, 2026-10-12 evening) | "Ana tür + alt tür". One default recurrence. Parent rule is None / previous meeting in the series / user selects. Attendance mandatory means no publish until every invitee is marked present, absent or excused. "Owner / Due date / Action items" are not agenda lines. Invitations are by position. The **five software items before the list loads**: confidentiality, mandatory attendance, per-agenda-item note and outcome, honest display of the flags, mandatory agenda items. No 32 new action task types. |
| `docs/roadmap/plans/meetings-mod0357-work-list-2026-10-27.md@2904f4029` (Kısa liste A4 and A7, the "Toplantı türleri" block, §5 Dalga 3/4/5) | Slice content and estimates; A7 (page help and tour before use). The **§6 item 6a** (expected manager document; meeting type is RCS-001 İKİSİ DE with prefix `MTY`; no code field until BL-565) is **not in `2904f4029`**. It was committed later, in `53dc6a97f`. |
| `docs/records/decisions/2026-09/ADR-003-meetings-are-mod-0357-now-and-live-in-platform.md@826150ef4` | Identity, hosting in `Diten.Platform`, `meeting_*` collections, `platform.meetings.*`, no `TaskItem` field, e-signature deferred. |
| `.antigravity/rules/record-coding-standard.md@826150ef4` (RCS-001 §2, §3, §5.3, §5.5, §5.12, §6) | Record-code classes, prefix uniqueness, group prefix, and the rule that no module may keep its own counter. |
| `.antigravity/rules/page-help-and-keyboard.md@826150ef4` (UXH-001 §2, §5, §6) | Help keys, the ledger that only shrinks, "Toplantı" as one of the five tour flows, and `data-tour` targets. |
| `docs/roadmap/backlog/page-help-ledger.md@826150ef4` lines 134 to 140 | The seven meeting pages that have no help text today. |
| Code @826150ef4 (`2904f4029` is a docs-only commit on top of it) | `Domain/Entities/Meetings/*.cs`, `Domain/Enums/Meetings/MeetingEnums.cs`, `Application/Features/Meetings/**`, `Infrastructure/Services/MeetingInviteMailer.cs`, `Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs`, `frontend/Diten.Web/Controllers/MeetingsController.cs`, `wwwroot/assets/js/Meetings/**`, `wwwroot/assets/js/shared/diten-tour-catalog.js`, `tests/architecture/audit-ledger/Diten.Platform.md`, `gateway/Diten.ApiGateway/ocelot.json`. |
| DCP-002 preflight, run 2026-10-27 | `verify_module_id.py . --check-id MOD-0357 --name "Management Review & Cadence"` → `OK MOD-0357: proven against Blueprint/registry.` R1 opens no new identity. |

### 24.2 Scope of R1

**In scope (one revision, slices S13 to S28):**

- **A. Two-level meeting type.** A new tenant-scoped **main type** (`MeetingCategory`, collection
  `meeting_categories`). Today's `MeetingType` row becomes the **subtype** and gets a reference to its main type, plus
  new default fields. `Daily` is added to the series frequency. The type screen is grouped by main type, and so is the
  type picker on the create form.
- **B. Create-form auto-fill.** Choosing a subtype fills title, type, place, end time, description, participants,
  the follow-up-of field and the agenda. The agenda is written **at create time** (`CreateMeetingRequest` gains
  agenda items); since "ekle-uc" this part is its own early slice, S25. A "Repeat" option creates the meeting through the existing path and then a series that takes that
  meeting as its first instance. This adds no third creation path (§19 S11).
- **C. The manager's five pre-load items:** confidential meeting type, mandatory attendance, per-agenda-item note
  and outcome, honest display of the flags, and mandatory agenda items for quality-record types.
- **D. A one-off catalogue importer** for main types and subtypes. It is idempotent, has a dry-run preview and is
  audited.
- **E. Record codes (RCS-001).** These are recorded and deferred. No slice builds them (§24.20).
- **F. Page help** for the seven meeting pages, and the Meetings tour.
- **G. Create-flow additions (owner "ekle-uc", 2026-10-27):**
  - **Agenda at create, early (S25).** It uses today's `MeetingType.AgendaTemplate`, so it does not wait for the
    type model. The invitation then goes out with the agenda already saved.
  - **Preparation tasks on the create form (S26).** Ordinary MOD-0024 tasks raised through the existing
    meeting→task bridge, a "Preparation: x / y done" line on the detail page, and a reminder to the organizer.
    They **never block** the meeting (R1-D13).
  - **Required / optional attendees, agenda presenter and time box (S27).**
- **H. Meeting access security (S28, from review 01 F-1 and F-4; first in the order).**
  - **Write handlers get the read rule.** Every write handler on an existing meeting applies the same visibility rule
    reads use (`CanView`) and answers 404 to a caller who cannot view the meeting. Today **no** write handler checks
    it. This is a hole for every meeting, not only confidential ones.
  - **BL-541, meetings side, is closed.** Report and export mask task details the reader may not read, and the task
    side's related-record row for a meeting the reader cannot view is dropped.

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
- Agenda text inside the invite e-mail body or the `.ics` `DESCRIPTION`. This would **change the existing** invite,
  change and cancel templates in the shared seed (`Infrastructure/Persistence/Configurations/NotificationTemplateSeed.cs`)
  for every meeting mail, so it goes to follow-up §24.24. S26 differs: it **adds** one new template key there for its
  own reminder and changes no existing template.
- The task side of BL-541 (what the Task Center shows about meetings beyond the related-record row), and any change to
  `Features/Tasks/**`. S28 closes the meetings side only, through existing MOD-0024 services.

### 24.3 Model decisions proposed by R1 (each needs owner approval with the revision)

| # | Decision | Reason |
|---|---|---|
| R1-D1 | **Main type = `MeetingCategory`** (new entity, `TenantScopedEntity`, collection `meeting_categories`, soft delete). **Subtype = today's `MeetingType`** (same entity, same collection `meeting_types`, same ids). UI terms: en "Main type" / "Subtype", tr "Ana tür" / "Alt tür" (all 7 languages in resx). | §5.7 "Model: ana tür + alt tür" and §5.6 "Alt tür = bugünkü `MeetingType` satırı". Keeping the subtype as the existing entity means no id changes for any existing `Meeting.MeetingTypeId`, `MeetingSeries.MeetingTypeId` or report filter. |
| R1-D2 | **Migration and backfill: none.** `MeetingType.MeetingCategoryId` is stored as nullable. Existing rows keep `null` and are **never rewritten or deleted at deploy**. Wherever a row with `null` is listed (type screen, type picker, report filter), it appears under an **"Unassigned"** group (localized, sorted last). From R1 on, every create and every update of a subtype **requires** a main type. An admin assigns one to a legacy row by editing it, or in bulk through the importer (S24-5). Existing meetings of an unassigned subtype keep working unchanged. | Two alternatives were rejected. (a) A seeded "General" main type with a backfill invents tenant data and writes to every existing row, and a wrong group would then look intentional. (b) Refusing to create meetings with unassigned subtypes would break the module for every tenant on the day of deploy. "Unassigned" is honest, it is visible, and it disappears on its own once the catalogue is loaded. |
| R1-D3 | **Subtype names stay tenant-unique.** The existing unique index `ux_meeting_types_tenant_name` `(TenantId, Name, IsDeleted)` (`PlatformSchemaManifest.Meetings.cs`) is **not changed**, so the same subtype name cannot exist under two main types. | No index migration is needed. Picker, report and export show the subtype name alone without ambiguity. The importer reports a name clash as a conflict and never moves a row (S24-5). |
| R1-D4 | The catalogue **does not store start or end times**. It stores a default duration. End = Start + default duration. | §5.6 "Ekranda doldurma" and the work list "Toplantı türleri" block. |
| R1-D5 | **Confidentiality ratchet.** A meeting copies `IsConfidential` from its subtype when it is created. The **effective** value is `Meeting.IsConfidential OR (current subtype).IsConfidential`. No R1 command ever lowers `Meeting.IsConfidential`: when the type changes on update, the new value is the old value OR the new type's flag. **Type stamp (owner decision "duzelt-5", review 06 F-34):** when `UpdateMeetingType` turns a type's `IsConfidential` from false to true, the same call sets `Meeting.IsConfidential = true` on every existing meeting of that type, in one Platform transaction with the type update (S15-11). **Closing stamp (pre-review 1 F-43):** the same stamp also runs when the flag goes from true to false, before the flag is lowered, in the same transaction. **Importer (pre-review 1 F-39):** the catalogue importer calls the same stamp, in its own transaction, for every subtype whose flag it changes (S24-9). | Switching a type to confidential protects that type's past meetings at once, through the effective value and, permanently, through the stamp. Switching it off later does not expose them, because their stored value is already true. Without the stamp, a meeting created under a non-confidential type would be hidden only while the type stays confidential (review 06 F-34). A create or a type change that read the type just before it was made confidential can still write `false` after the stamp ran (F-43). The effective value hides such a meeting while the type stays confidential, and the closing stamp marks it before the type can stop hiding it. |
| R1-D6 | **Agenda at create.** `CreateMeetingRequest.AgendaItems`: `null` means the server applies the subtype's agenda template; `[]` means no template lines; a list means exactly those lines. Mandatory lines (S18) are always added by the server. | The UI sends the edited list. Callers without a UI (series sweep, follow-up) send `null` and get the standard agenda (Q7). |
| R1-D7 | **Repeat without a third creation path.** The meeting is created by `CreateMeetingCommand`. The same handler then sends the existing `CreateMeetingSeriesCommand` with `CreatedFromMeetingId`. The series **adopts** that meeting as occurrence 0, so the sweep never generates it again. | §19 S11: "No third creation path". The adopted meeting is a normal meeting, and later instances go through S7 or S11 exactly as today. |
| R1-D8 | **Title placeholders.** The automatic ones are `{Period}`, `{Week}`, `{Month}`, `{Quarter}`, `{Year}` and `{Date}`. They are rendered in **invariant ASCII** formats in the **tenant time zone**: `{Date}` = `2026-10-27`, `{Week}` = `W44` (ISO 8601), `{Month}` = `2026-10`, `{Quarter}` = `Q4`, `{Year}` = `2026`. `{Period}` follows the recurrence: weekly → `2026-W44`, monthly → `2026-10`, quarterly → `2026-Q4`, yearly → `2026`, daily or none → the date. Any other `{Name}` (such as `{Manager}` or `{Initiative}`) is filled by the user before saving. | The text does not depend on language for a 7-language tenant. Each series instance gets its own value (§5.6 item 6). |
| R1-D9 | **Participants by position.** The subtype stores position ids. The create form resolves them to the **current holders** through the attendee eligibility the module already uses (`MeetingEligibility.EligibleUserIdsAsync` → `GetTaskAssignmentPersonLookupQuery(Decision)`, whose rows carry `PositionId`). No new directory seam is added. Person-dependent roles ("Line Manager / Employee") are not stored as rules. The subtype carries a free-text `ParticipantSelectionHint` that is shown next to the picker. A series created by Repeat stores the resolved people (a snapshot, as `MeetingSeries.AttendeeUserIds` does today). | §5.6 item 2 and §5.7 "Davet pozisyon bazlı". Re-resolving positions for each series instance is follow-up Q10. |
| R1-D10 | **Honest flags through one server switch.** A single constant set (`MeetingCapabilities`) says which flag effects are active. The UI renders "not yet active" from it, and the slice that makes an effect active changes the constant. | The display cannot drift from the code. Pre-load item 4 can ship first (A4 in the work list). |
| R1-D11 | **No new permission key.** Main-type CRUD, the importer and the positions lookup use `platform.meetings.types-manage`. The auto-fill lookups use `platform.meetings.create` / `.read`. Repeat uses `platform.meetings.series-manage` (Q9). | The brief says no new permission family. Every endpoint is mapped in §24.6. |
| R1-D12 | **Audit.** New commands use path `a`. An existing **non-minutes** command that an R1 slice changes **in what it writes or records** moves to path `a` in that same slice, and its line leaves the known-debt list, which only shrinks. **Access-check-only rule (review 01, F-2):** a change that only adds a refusal (S28's visibility check) and changes neither the data the command writes nor what its event would carry does **not** trigger the move. Such a command stays where it is, and §25 names the slice that adds the check. **Stated deferral (review 04 F-28 b; R1 fix round 4).** S28 also makes three changes that are **not** access-check-only, because they write differently: the sweep's unchained fallback in `GenerateDueMeetingSeriesCommand` (S28-11: a different write in the same call; the pointer moves to a meeting made by the first-instance create); the follow-up completion in `ScheduleFollowUpMeetingCommand` (S28-10, review 03 F-24); and the scheduler added as an `Accepted` attendee (S28-10, owner answer Q25), which `ScheduleFollowUpMeetingCommand` asks for and `CreateMeetingCommand` writes through its command-only `PreAcceptedUserId` parameter. None of the three commands moves in S28, because all three of these conditions hold. (1) The command has no event in S28, because no meeting audit row can be written before S25 (category Q13, shared mirror Q23). (2) A named later R1 slice moves it to path `a`, and its event then records the change: `GenerateDueMeetingSeriesCommand` in S21 with the outcome `created-unchained`; `ScheduleFollowUpMeetingCommand` in S25 with `SchedulerAddedAsAttendee`, and `CreateMeetingCommand` in S25 with `PreAcceptedUserId` (§25; tested in S25-7). **How the event gets these values (review 05 F-30).** `AuditBehavior` asks the command for its metadata before the handler runs (`AuditBehavior.cs:47, 143`), so a fact only the handler knows (the outcome, `SchedulerAddedAsAttendee`, the id of the meeting that answered) cannot come from the command's own fields. The new meeting's id is the exception: it is generated on the command, because the record's `EntityId` is fixed with the plan (S25-7, pre-review 1 F-42). It comes from S25's post-handler carrier `MeetingAuditFacts`, which the handler fills and `AuditBehavior` reads when it builds the record after the handler (`:74-80`, `:179-180`, `:268-290`). S25-7 measures this through the real `AuditBehavior` before any command relies on it. `PreAcceptedUserId` needs no carrier: it is a field of the `CreateMeetingCommand` the follow-up handler sends. If the S25-7 measurement fails, the slice stops and reports. The carried facts then leave the events (§25 says which), and this deferral rests on conditions (1) and (3) alone: the commands are still moved and audited, but the event no longer says which branch ran. (3) By today's code, the change writes no new kind of data. The fallback writes the claim and the pointer that the sweep writes today, and it creates the meeting through `CreateMeetingCommand` as a first instance does, with no link and no carried line. The completion writes the follow-up link and the carried lines that a follow-up writes today. The scheduler's row is an attendee row, which `CreateMeetingCommand` writes today for any attendee a follow-up request names (`MeetingFollowUpCommandHandlers.cs:90`), with the `Accepted` response it already writes for the organizer's row (`MeetingCommandHandlers.cs:157-167`). A change that fails any of the three conditions moves its command in its own slice. The publish and correct minutes commands are K2 class and stay in known debt (Q14). Details in §25. | AUD-001 §10.1 transition rule and §4.4. AUD-001 only requires the debt list not to grow. This rule keeps the security slice S28 small and still moves every command whose recorded content changes, at the latest in the slice that §25 names for it. The stated deferral adds no debt line: the three commands are already on the list (L118, L156, L76), and they leave it in S21 and S25. |
| R1-D13 | **Preparation never blocks (binding once this revision is approved).** Preparation tasks and their status **never block** creating, holding, rescheduling, cancelling or publishing a meeting, and never block anything on the task side. A failed preparation line never undoes the meeting (K12). Rescheduling a meeting **does not move** a preparation task's due date. The task owners are notified as K7 states (§16), and the task itself is not changed. | ADR-003 §4 "Bağ asla örtük engel değildir" (the one exception is the task type's `reviewMeetingPolicy`, which is not touched here). ADR-003 §5 "erteleme/iptal: bağlı görev sahiplerine bildirim, görev değişmez". |
| R1-D14 | **Required is the default.** `MeetingAttendee.IsOptional = false` means required. Every row written before R1 is required. The organizer is always required, also after a reassignment: the new organizer then has exactly one required, `Accepted` row (S27-8, review 06 F-36). Optional only changes the label on screens and the `.ics` `ROLE`, plus the attendance-mandatory rule in Q19. It never changes who may see the meeting (D3). | RFC 5545 `ROLE=REQ-PARTICIPANT` / `OPT-PARTICIPANT` (the `.ics` default is REQ). An additive field leaves the existing rows unchanged. |
| R1-D15 | **Writes follow reads (S28).** A caller may change an existing meeting only if `MeetingEligibility.CanView` admits them: organizer, attendee or `read-all` holder, and from S15, never a non-participant of a confidential meeting. On an existing meeting named by id, everyone else gets **that handler's own not-found answer (404)**, the same answer it gives for a missing meeting, with no metadata (S28-1). On the create path, where no existing meeting is named, a caller the create replay or a follow-up collision must not reach gets **409 `MEETING_CREATE_CONFLICT`** with one fixed body instead (S28-10; review 03 F-22). One helper does the check for every write handler. The same rule covers a meeting reached **indirectly**: the create path's idempotent replay, a follow-up that lands on an existing meeting, the target of a follow-up's own idempotency link (review 06 F-35), the `FollowUpOfMeetingId` of a new meeting, and the meeting a series adopts (S28-10, S28-11). There is **no bypass** (review 02 F-13). The series sweep, which has no user, carries an explicit system-origin marker that names an **acting identity**, the series organizer. `CanView` is applied with that identity (organizer or attendee only, no `read-all`). The marker is never inferred from an empty user id, and a source-scan test lists the one file that creates it. When the series organizer cannot view the chain source, the sweep still makes the occurrence, as a plain **unchained** meeting that carries nothing from the source, and moves its chain pointer on, so a legitimate organizer hand-over never stops a series (S28-11; review 03 F-18). The existing permission keys still apply on top. | Review 01 F-1, measured: `CanView` is defined at `MeetingCommandHandlers.cs:63-64` and called only by query handlers. `AddMeetingAttendeesHandler` (`:429-503`) and `ReassignMeetingOrganizerHandler` (`:359-426`) check only existence and editability, so a non-participant with `update` can add themselves to any meeting by id and then read it. Review 02 F-11 and F-13, measured: the create key has no caller (`MeetingIdempotencyKeyResolver.cs:9-22`), and a series edit rewrites its organizer and attendees with no relation check (`MeetingSeriesCommandHandlers.cs:167-181`). |

### 24.4 Entity and field additions (R1)

All new entities are `TenantScopedEntity`, live in Mongo, use soft delete, inherit `TenantRepository<T>` and have
tenant-first indexes declared in `Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs`. Every new
field on an existing entity is **additive**: rows written before R1 deserialize with the default shown.

**`MeetingCategory` (new, collection `meeting_categories`, S19)**

| Field | Type | Required | Rule |
|---|---|---|---|
| `Name` | string | Yes | trim, max 200, unique among **non-deleted** rows of the tenant. Index `ux_meeting_categories_tenant_name` on `(TenantId, Name)` is **partial**, with `IsDeleted == false` as its filter (the partial-filter pattern already used in `PlatformSchemaManifest.WorkflowWorkCenter.cs:130`), plus a pre-check. A name can then be deleted, re-created and deleted again, which the `(TenantId, Name, IsDeleted)` shape of today's subtype index refuses on the second delete (review 01 F-7; soft delete only sets `IsDeleted`). |
| `Description` | string? | No | max 1000 |
| `SortOrder` | int | Yes | 0..9999, default 0 |

**`MeetingType` (the subtype) — additions**

| Field | Type | Default for old rows | Slice | Rule |
|---|---|---|---|---|
| `MeetingCategoryId` | Guid? | null ("Unassigned") | S19 | Required on create and update from R1 on. Must resolve to a non-deleted category in the tenant. |
| `TitleTemplate` | string? | null | S19 | max 200. Placeholders per §24.5 (letters, spaces, `/`). Unbalanced or nested braces are refused. |
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
| `Meeting.IsConfidential` | bool | false | S15 | Set from the subtype on create. Never lowered (R1-D5). Set to true on every meeting of a type when that type is made confidential (S15-11, review 06 F-34), and on every meeting of the type still stored `false` when the type stops being confidential (closing stamp, pre-review 1 F-43), whether through the type screen or the importer (F-39). |
| `AgendaItem.IsMandatory` | bool | false | S18 | Set only by the server from `MandatoryAgendaItems`. |
| `MeetingMinutesVersion.AgendaNotes` | List\<`MinutesAgendaNote`\> (embedded) | [] | S17 | `{ AgendaItemId, AgendaTextSnapshot, Note ≤ 4000, Outcome ≤ 1000 }`, ≤ 100 rows |
| `MeetingSeries.TitleTemplate` | string? | null | S21 | max 200, automatic placeholders only |
| `MeetingSeries.CreatedFromMeetingId` | Guid? | null | S23 | **Partial** unique index `ux_meeting_series_tenant_created_from_meeting` on `(TenantId, CreatedFromMeetingId)` with the filter `{ CreatedFromMeetingId: { $type: "binData" } }`, so one meeting can be adopted by at most one series and unadopted series (null or missing) are not in the index. Not sparse: a sparse compound index still holds every series, because `TenantId` is always present (review 06 F-33; S23-4). |
| `MeetingSeriesFrequency.Daily` | enum value `= 4` | — | S21 | **Appended. Existing values are never renumbered** (stored as int). This supersedes the enum's own comment "No `Daily` value" by owner decision §5.7. |
| `Meeting.PreparationReminderSentForStartAt` | DateTimeOffset? | null | S26 | Claim marker written under an expected-version write **before** the reminder is sent. It holds the start time it was sent for, so a rescheduled meeting gets one new reminder. It is never shown to users. |
| `MeetingAttendee.IsOptional` | bool | false (required) | S27 | Set on create, add and the new role change. Always `false` for the organizer's own row. |
| `AgendaItem.PresenterUserId` | Guid? | null | S27 | Must be an attendee of the same meeting when it is set. |
| `AgendaItem.DurationMinutes` | int? | null | S27 | 1..480 |

**Request-only additions (not stored on the meeting):** `CreateMeetingRequest.AgendaItems` (S25, R1-D6; with S27
each line may carry a presenter and a duration). `CreateMeetingRequest.PreparationTasks` (S26): a list of
`{ ClientLineKey, Title, AssigneeUserId?, DueAt?, Description? }`, at most 20. Each line becomes an ordinary MOD-0024
task through the existing bridge (`CreateTaskFromMeetingCommand`); only the `RecordLink` (type `preparation`) is
stored on the meeting side. `CreateMeetingRequest.OptionalAttendeeUserIds` (S27).

### 24.5 Validation rules for the R1 fields (§12 shape)

| Field | Required | Format/Rule | DB-level | Pre-check / reason code |
|---|---|---|---|---|
| `MeetingCategory.Name` | Yes | trim, max 200, tenant-unique | unique index | `ExistsByNameAsync` → 409 `MEETING_CATEGORY_NAME_DUPLICATE` |
| `MeetingCategory` delete / bulk delete | — | refused while any non-deleted subtype references it | — | 409 `MEETING_CATEGORY_IN_USE` (per row in bulk) |
| `MeetingType.MeetingCategoryId` | Yes (write) | resolves in-tenant, not deleted | — | 400 `MEETING_CATEGORY_REQUIRED` / 400 `MEETING_CATEGORY_NOT_FOUND` |
| `MeetingType.TitleTemplate` | No | max 200, balanced braces, no nested braces; placeholder pattern `\{[A-Za-z][A-Za-z /]{0,39}\}` (letters, spaces and `/`, so the catalogue's `{Team/Period}` and `{Budget Item}` are valid user placeholders; the six automatic ones are matched exactly). CT measured 2026-10-27: the narrower `\{[A-Za-z]+\}` refused 8 of the 46 catalogue titles. | — | 400 `MEETING_TYPE_TITLE_TEMPLATE_INVALID` |
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
| `CreateMeetingRequest.PreparationTasks[]` | No | ≤ 20 lines; Title trim, 1..200 (same limit as a task title); `ClientLineKey` non-empty and unique within the request; offered only when Start is in the future | — | validator. Assignment and scope refusals come from MOD-0024 **per line** (S26-3) and never fail the whole create. |
| Preparation line, caller without `platform.tasks.create` | — | that line is refused; the meeting is still created | — | per-line `MEETING_PREPARATION_TASK_FORBIDDEN` |
| Preparation line, Start already passed | — | that line is refused (the bridge would link it as `bornFromMeeting`) | — | per-line `MEETING_PREPARATION_START_PASSED` |
| Preparation `DueAt` | No | default = meeting Start; a due date after Start is **allowed with a warning** | — | warning `MEETING_PREPARATION_DUE_AFTER_START` (UI and response, never 4xx) |
| `MeetingAttendee.IsOptional` (organizer) | — | the organizer's row cannot be made optional | — | 409 `MEETING_ATTENDEE_ORGANIZER_REQUIRED` |
| `AgendaItem.PresenterUserId` | No | must be an attendee of the meeting | — | 400 `MEETING_AGENDA_PRESENTER_NOT_ATTENDEE` |
| `AgendaItem.DurationMinutes` | No | 1..480 | — | validator |
| Sum of agenda durations | — | larger than the meeting duration is **allowed with a warning** | — | UI warning only (no reason code, no refusal) |
| Create replay or follow-up collision (S28-10) | — | the five key values match an existing meeting the caller cannot view (on a create, or on the create a follow-up sends); or the follow-up's own idempotency link points to a meeting the caller cannot view (review 06 F-35) | — | 409 `MEETING_CREATE_CONFLICT` (one fixed body, no id and no field) |
| Follow-up collision, caller can view the existing meeting (S28-10; review 03 F-24, review 04 F-26) | — | the existing meeting's `FollowUpOfMeetingId` is not the source; or it is the source, the follow-up link is missing and the meeting is cancelled or completed | — | 409 `MEETING_FOLLOW_UP_CONFLICT` (its `data` carries only the existing meeting's id) when it is not a follow-up of the source; 409 `MEETING_CANCELLED` / 409 `MEETING_COMPLETED` (existing codes, `UpdateMeetingHandler.CheckEditable`) when it is a cancelled or completed follow-up of the source. Nothing is written. When `FollowUpOfMeetingId` is the source and the meeting is scheduled, the follow-up is completed instead (200) |
| Create by a creator who would not be able to view the meeting (S28-10; replaces S26-10's own code, review 03 F-19; defensive, review 04 F-25) | — | the creator is neither the requested organizer nor a requested (eligible) attendee and does not hold `read-all` (from S15, the request is also refused for a confidential subtype unless the creator is a participant). Only a direct API caller who names another organizer can reach it, because the create form and the Task Center make the caller the organizer. On the create a follow-up sends, it is applied only to a scheduler who is **not a party** of the source (review 05 F-29): such a caller passed the source check through `read-all`, which still passes the rule for a non-confidential new meeting, so in practice the refusal comes only when the new meeting would be confidential (from S15). The follow-up dialog does not offer confidential subtypes to such a caller (S15-10, pre-review 1 F-45), so on that path only a direct API call, or a type made confidential while the dialog was open, reaches it. A party of the source is added as an `Accepted` attendee instead (S28-10, Q25) | — | 400 `MEETING_CREATOR_CANNOT_VIEW`; nothing is written |
| Single open of a confidential meeting whose read-audit record cannot be written (S15-12; review 06 F-37) | — | `GET /{id}`, `/{id}/minutes` or `/{id}/tasks` by a caller who may view the meeting, when the central record is not queued | — | 503 `MEETING_CONFIDENTIAL_READ_NOT_RECORDED`; no meeting data in the body |
| Repeat adoption refused (S23-8; review 03 F-23) | — | the creator can no longer view the meeting just written when the series adopts it (only possible through a concurrent change) | — | partial success `series: { created: false, reasonCode: "MEETING_SERIES_ADOPTION_REFUSED" }`; the meeting stays, no series |

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
  | `POST /` with `agendaItems` (S25) | `platform.meetings.create` (unchanged) |
  | `POST /` with `preparationTasks` (S26) | `platform.meetings.create` + **`platform.tasks.create`** for the task lines (K9, the same two-key rule the bridge endpoint `POST /{id}/tasks` already has). Without `tasks.create`, the section is not rendered and the server refuses each line (the meeting is still created). |
  | `PUT {id}/attendees/{userId}/role` (S27) | `platform.meetings.update` |

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
- **MOD-0024 boundary (§6).** `TaskItem` gets no field, and `Features/Tasks/**` is not edited. MOD-0024 is consumed
  only through its **existing** public surface, which Meetings already uses today:
  - the mediator queries `GetTaskAssignmentPersonLookupQuery` and `GetActiveTaskTypesQuery`, plus
    `GetPositionsQuery` from TenantOrganization;
  - the read-rule service `ITaskReadAccessPolicy` (`CanReadAsync` and `ReadableTaskIdsAsync`, already used by
    `TaskRelatedRecordResolver` and `LinkExistingTaskHandler`), which S28 also uses in the report;
  - **task creation** through Meetings' own bridge command `CreateTaskFromMeetingCommand` (S4), which sends
    MOD-0024's public `CreateTaskItemCommand` through the unchanged assignment guard (K2). S26 calls this bridge
    in-process. It is not a query, and it is the only write path into MOD-0024 that R1 uses.
- **Shared libraries.** `services/Diten.Building.Blocks/**`, `Diten.Platform.Common` and `Diten.Platform.Contracts`
  are outside this module (AGENTS §4). The one R1 need there is the `AuditCategory` mirror for Q13. S25 does not start
  until Q23 is answered; yes → the shared mirror gets `["Meetings"] = 18` (written by this CT after the owner's
  explicit XMC-001 answer, reviewed by the library owner, per card S-90 §7c); no → `PlatformConfiguration` is used
  and Q13 is reopened.
- **Web proxy.** Measured: `frontend/Diten.Web/Controllers/MeetingsController.cs` has one explicit action per
  endpoint and no catch-all, so every new backend endpoint needs a proxy action in the same slice.
- **Sabotage proof.** Every slice names one guard and shows it red with the rule removed, then green again.
- **No commit to `main`.** Stage files by explicit path only (GIT-002).

### 24.7 Slice order and estimates

Prompt counts are **estimates** (one prompt ≈ one development session). They are not commitments.

| Order | Slice | Title | Estimate (prompts) | Depends on | DL (evidence level in §26) |
|---|---|---|---|---|---|
| 1 | S28 | Meeting access security (writes and BL-541) | 3 (estimate; review 03 F-20: 14 existing test files build a guarded handler by hand, about 40 constructions, and the series fallback, the creator rule and the follow-up completion are added; review 04 F-27, F-26 and Q25 add the sweep unit test file with two rows, the completion's two lifecycle rows and the scheduler-attendee rows, which fit in the same 3); 3–4 after fix round 5 (review 05 F-29, F-31, F-32 and review 06 F-35 add about eight HTTP rows, a reflection test and the idempotency-link target check) | — | DL-28 |
| 2 | S13 | Page help and Meetings tour | 2 (estimate) | — | DL-13 |
| 3 | S14 | Honest display of the type flags | 1 (estimate) | — | DL-14 |
| 4 | S25 | Agenda at create (today's type template) | 1 (estimate) | S25 does not start until Q23 is answered; yes → the shared mirror gets `["Meetings"] = 18` (written by this CT after the owner's explicit XMC-001 answer, reviewed by the library owner, per card S-90 §7c); no → `PlatformConfiguration` is used and Q13 is reopened. S28 (S25-6's retry rule is S28-10). S18's mandatory lines join whenever S18 lands (not a hard dependency). | DL-25 |
| 5 | S15 | Confidential meeting type | 3–4 (estimate; 2 before fix round 5, which adds the type stamp in a Platform transaction, review 06 F-34, and the confidential read audit, F-37; fix round 6 adds the closing stamp and its race test, the withheld write responses and the report placeholder, the names-only audit rows and the dialog filter, pre-review 1 F-38, F-40, F-43 and F-45) | S28 (the write guard it extends); S25 (`CreateMeetingCommand` already on path `a`; the audit carrier); S15 does not start until Q27 is answered (the K2 class of writes on a confidential meeting; A → as written; B → a slice that moves the meeting content commands to the in-transaction path comes first; C → S15 waits for that path, §24.25 Q27); S15-12 does not start until Q26 is answered (yes → the shared read-audit writer; no → the module-local writer, §24.25 Q26) | DL-15 |
| 6 | S16 | Mandatory attendance before publishing | 1 (estimate) | S14 (switch flip only); S28 (its failure path's 404 for a caller who cannot view the meeting) | DL-16 |
| 7 | S17 | Note and outcome per agenda item | 2 (estimate) | S28 (its failure path's 404 for a caller who cannot view the meeting) | DL-17 |
| 8 | S18 | Mandatory agenda items for quality-record types | 1–2 (estimate) | S17 (for S18-5), S15 (type commands audited), S14 (switch flip), S25 (agenda ordering on create and follow-up) | DL-18 |
| 9 | S26 | Preparation tasks on the create form (never a block) | 2 (estimate) | S25 (same create block and form); S28 (each line goes through the guarded `CreateTaskFromMeetingCommand`, and retries rely on S28-10) | DL-26 |
| 10 | S27 | Required and optional attendees with agenda presenter and time box | 2 (estimate; 1–2 before fix round 5, which adds the organizer row on reassignment and its audit move, review 06 F-36) | S25 (agenda lines on the form; the audit carrier for `ReassignMeetingOrganizerCommand`); S16 (S27-3, Q19); S18 (`UpdateAgendaItemCommand` already on path `a`); S28 (write guard for the new role change) | DL-27 |
| 11 | S19 | Main type and subtype default fields (backend) | 2 (estimate) | S15 (type create/update already on path `a`) | DL-19 |
| 12 | S20 | Type screens and grouped type picker | 2 (estimate) | S19; S15 and S18 if their fields are to be shown | DL-20 |
| 13 | S21 | Daily frequency and series title template | 1–2 (estimate) | S25 (follow-up command on path `a`; the sweep calls it); S28 (S21-6's `created-unchained` outcome comes from S28-11's fallback) | DL-21 |
| 14 | S22 | Create-form auto-fill of the other fields | 1–2 (estimate) | S25, S18, S19, S20, S21 (shared title renderer) | DL-22 |
| 15 | S23 | Repeat on create | 2 (estimate) | S21, S22; S28 (adoption applies `MeetingWriteAccess`, S23-8) | DL-23 |
| 16 | S24 | One-off type catalogue importer | 1–2 (estimate) | S19, S20 (S15 and S18 for the flags it imports). The real import run also waits on the task-type catalogue load (MOD-0024, Dalga 2) and on the QA process owner's name (§5.7). | DL-24 |

**Total: about 27 to 33 prompts (estimate)**; it was 27 to 32 before fix round 6, 25 to 30 before fix round 5, and 23
to 27 after "ekle-uc". **After fix round 6** the total is again the exact sum of the cells: S15 goes from 3 to 3–4
(+1 at the high end, pre-review 1 F-38, F-40, F-43 and F-45). The importer's stamp call (F-39) fits in S24's 1–2 and
the command-generated id (F-42) in S25's 1. If Q27 is answered B, one more slice of about 3 to 4 prompts is added. S28
adds about 3 prompts. After review 03 the total was the sum of the cells above, 25 at the low end and 30 at the high end: S28 is now 3 rather than
2–3, and the earlier low end of 25 was one more than its cells added up to. **After fix round 5 the total is about
27 to 32 prompts (estimate)**, again the exact sum of the cells: S15 goes from 2 to 3 (+1 at both ends), S27 from 1–2
to 2 (+1 at the low end) and S28 from 3 to 3–4 (+1 at the high end). Why
this order differs from §5.7's order:
- S28 comes first because it closes a security hole that exists today for every meeting (review 01 F-1), and the
  meetings side of BL-541, which the work list puts **before use** (A3).
- S13 and S14 follow because the short list puts them **before use** (A7, A4), and S14's switch lets the honest
  text ship before the effects it describes.
- S25 comes third (owner "ekle-uc"). Invitations already go out at create, and today they carry no agenda; S25 needs
  nothing from the type model.
- S15 to S18 are the five pre-load items in the manager's order.
- S26 and S27 follow the pre-load items, so the manager's binding five are not delayed. The owner may move them
  earlier.
- S19 to S24 follow "type model → auto-fill → catalogue load" from §5.7 rows 3 and 4.

Inside that order, any slice whose "Depends on" cell is empty may be pulled forward. After review 01 F-5 and review
02 F-16, the cells name every dependency that a slice's audit, ordering or failure-path rule relies on, so an empty
cell really is empty. The slice
texts are §24.8 to §24.19 (S13 to S24), §24.28 to §24.30 (S25 to S27) and §24.31 (S28).

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
      `MEETINGCREATE` does not say the agenda is set on the form until S25 lands. The reviewer checks each step against
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
- `RecordLinks/MeetingRelatedRecordResolver.cs`. S28 already gives it the caller and the `CanView` check; S15 only adds
  the confidential input.
- `Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs` (S15-10).
- `Infrastructure/Services/MeetingInviteMailer.cs` and `Services/MeetingIcsBuilder.cs` (title input for every mail
  that `BuildVariables` feeds).
- Type create/update validators; type and meeting views and JS (checkbox; lock badge); resx in 7 languages.
- **Type stamp (S15-11, review 06 F-34):** `MeetingTypeCommandHandlers.cs` (`UpdateMeetingTypeHandler` gains
  `IPlatformTransactionExecutor` and `IMeetingRepository`); `Domain/Repositories/IMeetingRepositories.cs` and
  `Infrastructure/Persistence/Repositories/MeetingRepositories.cs`: a session overload of
  `IMeetingTypeRepository.UpdateAsync` and a new `IMeetingRepository.MarkConfidentialByMeetingTypeIdAsync(session,
  meetingTypeId)` (one `UpdateMany`, see S15-11). The stamp is one new shared method,
  `Features/Meetings/Services/MeetingTypeConfidentialityStamp.cs` (to be written), which `UpdateMeetingTypeHandler`
  and the S24 importer both call inside their transaction (pre-review 1 F-39).
- **Confidential read audit (S15-12, review 06 F-37):** `MeetingQueryHandlers.cs` (`GetMeetingByIdHandler`,
  `GetMeetingMinutesHandler`, `GetLinkedTasksHandler`), the read-audit writer that Q26 decides, `MeetingModels.cs`
  (reason code `MEETING_CONFIDENTIAL_READ_NOT_RECORDED`), the reason-code bridge in 7 languages.
- **Withheld content (S15-12, pre-review 1 F-38):** `MinutesCommandHandlers.cs` (`SaveMinutesDraftHandler`,
  `PublishMinutesHandler`, `CorrectPublishedMinutesHandler`), the replay branch of `CreateMeetingHandler`,
  `MeetingModels.cs` (`ContentWithheld` on `MeetingDto` and `MeetingMinutesVersionDto`), `MeetingReportQueryHandlers.cs`
  and S28-6's `Services/MeetingReportExportPlaceholders.cs` (the new placeholder `MEETING_CONFIDENTIAL_CONTENT` in 7
  languages).
- **Follow-up dialog (S15-10, pre-review 1 F-45):** `MeetingModels.cs` (`MeetingDto.CallerIsParty`, set by
  `GetMeetingByIdHandler`; `MeetingTypeLookupItemDto` gains `IsConfidential`, today it carries only `Id`, `Name` and
  `AgendaTemplate`, `MeetingModels.cs:325`), `wwwroot/assets/js/Meetings/follow-up-dialog.js` and
  `frontend/Diten.Web/tests/meetings-follow-up.test.js`.
- Audit: `CreateMeetingTypeCommand`, `UpdateMeetingTypeCommand` and `UpdateMeetingCommand` move to path `a`.
  `CreateMeetingCommand` is already on path `a` from S25, and S15 adds the names-only rule for confidential meetings
  (S15-7, pre-review 1 F-40). Each command becomes its own file under `Commands/` if moving it out of the bundled file is needed to add the
  interfaces. Their lines are removed from `tests/architecture/audit-ledger/Diten.Platform.md` (§25).

**Acceptance criteria**
- [ ] S15-1 — `MeetingType.IsConfidential` (default `false`) can be set through type create/update under
      `types-manage`. Rows written before R1 read `false`.
- [ ] S15-2 — `Meeting.IsConfidential` = the subtype's flag at create. On `UpdateMeeting`, it becomes `old OR newType`.
      No command in R1 lowers it (unit test over all meeting write handlers).
- [ ] S15-3 — The effective flag (`Meeting.IsConfidential || currentType.IsConfidential`) is an input of the single
      predicate `MeetingEligibility.CanView`. When it is true, view is granted only to the organizer or an attendee.
      `read-all` and the `IsPlatformActor` shortcut do **not** grant it (Q3). A non-participant gets **404** on
      `GET /{id}`, `/{id}/minutes` and `/{id}/tasks`, the meeting is absent from `GET /` and the calendar view, and
      the follow-up title comes back as `null`. Because S28's write guard calls the same predicate, the same
      non-participant also gets **404 on every write** S28 lists (update, cancel, reassign organizer, add and remove
      attendees, agenda add, update, delete and reorder, minutes draft, publish and correct, follow-up, task from
      meeting, link existing task, and S27's role change once it exists), even when holding `read-all` and the write
      key.
- [ ] S15-4 — The report and export (§23) apply the same predicate. For that caller, tiles, attendance rate,
      decisions and the action register exclude those meetings, and export rows equal screen rows (the BL-346 rule).
      An action row of a meeting the caller can view whose **current** meeting is such a confidential meeting shows
      the placeholder, not its title (S28-6; review 02 F-12).
- [ ] S15-5 — Task side: S28 already drops a related meeting the caller cannot view. With S15, a confidential meeting
      is dropped for every caller who is not its organizer or attendee, `read-all` included, so neither title nor
      route is shown (same rule as a dangling link, §13). This is
      verified by reading the Task Center projection as a non-participant assignee of a meeting-born task. No file in
      `Features/Tasks/**` changes.
- [ ] S15-6 — For **every** mail of a confidential meeting that `MeetingInviteMailer.BuildVariables` feeds
      (`MeetingInviteMailer.cs:300-303`), the stored `NotificationDispatch.Subject`, `VariablesJson` and any
      `invite.ics` `SUMMARY` carry the **subtype name** in place of the meeting title. That covers invite, change and
      cancel, and also the **attendee-removed** (`:86-87`) and **organizer-changed** (`:100-102`) mails (review 01
      F-6). Each of the five is tested.
      Description and agenda are not sent today and stay unsent. `LOCATION` is kept (Q3). This is measured on the
      stored dispatch row, not on the template.
- [ ] S15-7 — **Names only on a confidential meeting (review 05 F-30; pre-review 1 F-40; AUD-001 §3 "a changed
      field's name is written, not its value").** On every §25 row that touches a meeting whose effective flag (S15-3)
      is true when the handler runs, a changed content field is written by **name only**: the record holds no value,
      before or after, of the meeting's `Title`, `Description` or `Location`, or of an agenda line's `Text`. It keeps
      the ids (`EntityId`, `MeetingId`, `AgendaItemId`, the user ids the command names), the counts, the
      `IsConfidential` value and the list of changed field names. This covers `CreateMeetingCommand`,
      `UpdateMeetingCommand` and `ScheduleFollowUpMeetingCommand` (S15), `UpdateAgendaItemCommand` and
      `DeleteAgendaItemCommand` (S18), and `AddAgendaItemCommand` (S27); each of those slices applies the rule to its
      own row. **How.** Confidentiality is a stored fact the command does not carry, so a content value reaches a
      record **only** through S25's carrier `MeetingAuditFacts`, written by a handler that has read the effective flag
      as false. A command never puts a content value into its own `GetAuditMetadata()`. A refused command (404, 409) and
      a command whose handler throws before it reads the flag therefore write no content value either. **Carrier
      fallback.** If the S25-7 measurement is red, no meeting row writes a content value for any meeting, confidential
      or not; the rows keep the ids and the changed field names (§25).
      Tests (through the real `AuditBehavior` and a recording `IAuditService`): `UpdateMeeting` on a confidential
      meeting changes `Description` and `Location`, and the serialized record contains `Description` and `Location`
      as names and neither the old nor the new text; `CreateMeeting` of a confidential subtype writes no `Title`
      value; the same update on a non-confidential meeting writes the before and after values. Sabotage: put
      `Description` into the command's own metadata, then the confidential row is red.
- [ ] S15-8 — Participants see everything as today, plus a localized "Confidential" lock badge on the list, detail
      page and calendar event. The personal calendar feed (`CalendarMeetingReader`, own meetings only) is unchanged.
- [ ] S15-9 — Binding rule for later surfaces (availability, another person's calendar; Dalga 8): a confidential
      meeting shows to a non-participant only as "busy" with start and end. R1 adds a **source-scan guard**: every
      reader of `IMeetingRepository` or `IMeetingAttendeeRepository` outside `Features/Meetings/**` must be on a
      reasoned allow-list. Today that list is `CalendarMeetingReader` (own), `GetMyWorkCalendarHandler`
      (`WorkAggregation/Calendar/GetMyWorkCalendarHandler.cs:27-28, 168`; the caller's own calendar, added after
      review 01 F-6), `MeetingGatewayAdapter` (own), `ReviewMeetingGateReader` (status only), `TaskWorkItemProvider`,
      and the notification handlers (attendee rows). The guard must be green on its first run, and a new reader cannot
      appear without review.
- [ ] S15-10 — Follow-up (binding rule from S28-3). When the source meeting is confidential (effective flag), the
      follow-up becomes confidential too (`Meeting.IsConfidential = true`), whatever type the request names. Its title
      is **not** copied from the source: the default is the source's subtype name plus " (devam)" instead of
      "{source title} (devam)" (`MeetingFollowUpCommandHandlers.cs:79`). The series sweep follows the same rule.
      The scheduler that S28-10 adds as an `Accepted` attendee (owner answer Q25) stays on a confidential follow-up.
      S28-10 adds only a scheduler who is a party of the source (its organizer or a user with an attendee row on it),
      so this gives nobody new access: that scheduler already took part in the meeting the follow-up continues. A
      `read-all` holder is never added, and from S15 cannot schedule a follow-up of a confidential source anyway
      (S28-1, S15-3). Test: a source attendee who is not the organizer schedules a follow-up of a confidential source;
      the follow-up is confidential, the scheduler opens it (200), and a `read-all` non-participant gets 404.
      **Non-party scheduler and a confidential subtype (review 05 F-29).** A `read-all` holder who is not a party of a
      non-confidential source can still name a confidential subtype in the follow-up request
      (`MeetingFollowUpCommandHandlers.cs:83`; the dialog lists every type, `follow-up-dialog.js:55-59, 87`). S28-10
      sends that caller's create with `RequireCreatorCanView: true`, and from S15 the creator rule does not count
      `read-all` for a confidential subtype, so the request is refused with 400 `MEETING_CREATOR_CANNOT_VIEW` before
      anything is written, unless the caller names themself as organizer or attendee. HTTP test: such a request gets
      400, and no meeting, attendee row, link or carried line exists; the same request naming a non-confidential
      subtype gets 201 and the caller opens the new meeting through `read-all` (200).
      **The dialog does not offer what it cannot do (pre-review 1 F-45).** The follow-up dialog has no attendee or
      organizer field (`follow-up-dialog.js:91-93`), so the 400's advice cannot be followed there. `GET /{id}` returns
      a server-computed `CallerIsParty` (the caller is the meeting's organizer or has an attendee row on it), and
      `lookups/types` returns each type's `IsConfidential`. When `CallerIsParty` is false, the dialog leaves
      confidential types out of its type list (`follow-up-dialog.js:55-59`). If the source's own type is confidential,
      the caller is a participant (S15-3), so `CallerIsParty` is true and the list is complete. The text of
      `MEETING_CREATOR_CANNOT_VIEW` says what works on every path: "You would not be able to see this meeting, so it
      was not created. Name yourself as organizer or attendee, or choose a type that is not confidential." (7
      languages), so a type made confidential while the dialog was open still gets usable advice. JS test
      (`meetings-follow-up.test.js`): with `CallerIsParty` false the confidential type is not an option and the others
      are; with `CallerIsParty` true every type is an option. Sabotage: drop the filter, then the first row is red.
- [ ] S15-11 — **Type stamp (owner decision "duzelt-5"; review 06 F-34; R1-D5).** When `UpdateMeetingTypeHandler`
      changes a type's stored `IsConfidential` from `false` to `true` (and, through the closing stamp below, from
      `true` to `false`; pre-review 1 F-43), the same call writes, in **one Platform
      transaction** (`IPlatformTransactionExecutor.ExecuteAsync`, the BL-533 path `PublishMinutesHandler` uses,
      `MinutesCommandHandlers.cs:286`):
      1. the type itself, under the request's `ExpectedVersion` (a new session overload of
         `IMeetingTypeRepository.UpdateAsync`);
      2. one `UpdateMany` on `meeting_meetings`, filtered by the tenant, `MeetingTypeId` = that type and
         `IsConfidential != true`, that sets `IsConfidential = true` and increments `Version`
         (`IMeetingRepository.MarkConfidentialByMeetingTypeIdAsync`). There is no per-meeting loop.

      Rules:
      - **Every lifecycle state** is stamped: scheduled, cancelled and completed. The minutes of a completed meeting
        are what most needs the protection.
      - **Soft-deleted meetings are stamped too.** The filter does not use the repository's not-deleted scope. A
        soft-deleted meeting and its minutes stay in the database (soft delete only), the module has no meeting
        delete command today (such rows come only from a data repair), and a later restore must not bring back an
        exposed meeting. Stamping them costs nothing.
      - **Why the version moves.** Every meeting write is an optimistic whole-document replace
        (`ReadVersionWrites.ReplaceAsync`, `MeetingRepositories.cs:180-185`). Without the bump, a writer who loaded the
        meeting before the stamp (an open edit form, a minutes publish) would write `IsConfidential = false` back.
        With it, that writer gets the existing 409 `MEETING_CONCURRENCY_CONFLICT` and reloads. Stated consequence:
        an edit form of such a meeting that was open during the stamp gets one 409.
      - **No half state.** The type update and the stamp commit together or not at all. A stale type `ExpectedVersion`
        → the existing 409 and nothing is stamped. A failed `UpdateMany` aborts the transaction, so the type stays
        non-confidential and no meeting changes. No transaction available → the existing 503 of
        `PlatformTransactionUnavailableException` (`IPlatformTransactionSession.cs:19-22`), and nothing is written.
        The body is retry-safe when the executor runs it again: the `UpdateMany` filter skips stamped rows, and the
        type's read version is put back first (`ReadVersionSnapshot`, as `PublishMinutesHandler` does).
      - **Every change of the flag runs the stamp; no change runs none (pre-review 1 F-43).** False → true stamps the
        type's meetings as above. **True → false runs the same `UpdateMany` first (the closing stamp) and lowers the
        type's flag after it, in the same transaction.** Why: `CreateMeetingHandler` reads the type at the start
        (`MeetingCommandHandlers.cs:108`) and inserts later (`:136-151`), and `UpdateMeeting` reads the new type
        before it writes. If a raise commits in between, that write stores `false` for a meeting of the now
        confidential type. A re-read of the type just before the insert would leave a smaller window, and the raise's
        `UpdateMany` cannot see an insert that commits after its transaction began (MongoDB snapshot isolation), so no
        check on the create side closes the race. The effective flag hides such a meeting while the type stays
        confidential, and lowering the flag is the only way the type stops hiding it, so the closing stamp marks it
        at that moment. In normal operation the closing stamp modifies no row: every meeting of a confidential type
        is already stored `true` (stamped on the raise, copied on create, or raised on a type change, S15-2). A type
        created confidential has no meetings. True → true and false → false write no meeting row. True → false still
        lowers nothing that was stamped, and does not lower any meeting.
      - **One stamp, two callers (pre-review 1 F-39).** The stamp is one shared method,
        `MeetingTypeConfidentialityStamp.ApplyAsync(session, meetingTypeId, storedFlag, newFlag)`, which runs the
        `UpdateMany` when the two flags differ and returns the modified count. `UpdateMeetingTypeHandler` and the S24
        importer (S24-9) call it inside their own transaction. No other code writes `Meeting.IsConfidential` for a
        type change.
      - The stamp reads no meeting content, returns none and sends no mail.
      - **Audit.** `UpdateMeetingTypeCommand` is on path `a` from this slice (§25). Its record carries the stored
        and the requested `IsConfidential` and `ConfidentialStampedMeetingCount` (the `UpdateMany` modified count, on
        a raise or a lowering), through S25's carrier `MeetingAuditFacts`. The record is written after the
        transaction commits (path `a`, best effort, configuration; Q14 (c); the K2 class of the meeting rows the stamp
        writes is part of Q27).

      Tests (real Mongo, matrix false → true → false): type T is non-confidential and has a scheduled, a cancelled, a
      completed (with published minutes) and a soft-deleted meeting; another type has one meeting. T is made
      confidential: the four meetings of T store `true` with `Version` + 1, and the other meeting is untouched. T is
      made non-confidential again: the three live meetings still store `true`, and the S15 read and write matrix
      rows for a `read-all` non-participant (detail, list, minutes, tasks, report, export, and every S28 write) still
      give 404 or exclusion. Stale edit: `UpdateMeeting` with the version read before the stamp gets 409, and the
      stored flag stays `true`. Fault row: the `UpdateMany` fails inside the transaction, and afterwards the type is
      still non-confidential and no meeting is stamped. The audit row carries `ConfidentialStampedMeetingCount` = 4.
      **Race rows (pre-review 1 F-43; handler level, real Mongo, forced interleaving).** A type repository wrapper
      lets T be raised to confidential right after `CreateMeetingHandler` has read T and before it inserts. The new
      meeting is stored `false`; a `read-all` non-participant gets 404 on it (effective flag). T is then lowered: in
      the same transaction the meeting is stored `true` with `Version` + 1, the lowering's audit row carries
      `ConfidentialStampedMeetingCount` = 1, and the `read-all` non-participant still gets 404. The same rows run
      with `UpdateMeetingHandler` moving a meeting from another type into T. Sabotage: run the `UpdateMany` only on
      false → true, then the lowering row is red (200 after the lowering).
- [ ] S15-12 — **Read audit of a confidential meeting (owner decision "duzelt-5": a confidential meeting counts as an
      HR-confidential record, AUD-001 §7 third class; review 06 F-37).** A meeting is in this class when its effective
      flag (S15-3) is true. Every **single open** of its content writes one central read-audit record:
      - **Covered (enumerated from the code):** `GET /{id}` (`GetMeetingByIdHandler`, `MeetingQueryHandlers.cs:14`;
        `MeetingsController.cs:54-55`), `GET /{id}/tasks` (`GetLinkedTasksHandler`, `:210`; controller `:137-138`)
        and `GET /{id}/minutes` (`GetMeetingMinutesHandler`, `:336`; controller `:160-161`). Measured: there is no
        single-meeting export, print or download (the Meetings views and JS have none); the only export is the
        report's, which is a bulk export already recorded by `IDataExportAuditWriter`.
      - **Not covered, with the reason (corrected by pre-review 1 F-38):** the list and the calendar views (lists,
        not a single open; they carry titles and times, and no description, location, agenda or minutes); the Task
        Center invitation card and related-record row (title only, participants only, S15-5); and the paths below,
        which are **made content-free** for a confidential meeting instead of recorded, so that the only way to read
        its stored content is one of the three recorded opens. The earlier reason, "content the caller wrote", was
        wrong for three paths, and the report did carry minutes text:
        - **Minutes write responses.** `PublishMinutesRequest` carries only `ExpectedVersion` (`MeetingModels.cs:409`),
          and `PublishMinutesHandler` returns the stored draft, which someone else may have written
          (`MinutesCommandHandlers.cs:245-307`). For a confidential meeting, the responses of save draft, publish and
          correct carry the version's id, number, status, `Version` and publish or correction stamp, with
          `ContentWithheld = true` and empty `Attendance`, `Decisions` and `ActionReferences` lists
          (`MeetingMinutesVersionDto`, `MeetingModels.cs:428-440`; from S17 also `AgendaNotes`). Save draft and correct are
          included because their responses also hold stored values the request did not carry (display names and, from
          S17, the agenda text snapshot). The minutes editor already reloads through the recorded
          `GET /{id}/minutes` after each of the three (`minutes-editor.js:294-300, 307-313, 339-346`).
        - **Create replay.** The 200 replay returns the existing meeting's stored description, location, attendees and
          agenda (`MeetingCommandHandlers.cs:151-152, 194-203`). For a confidential meeting, the replay answer
          carries the id, the five key values the request carried, `Lifecycle`, `FollowUpOfMeetingId`, `Version` and
          this call's own results (preparation lines, series), with `ContentWithheld = true` and no description,
          location, attendee list or agenda. The create form reads only `data.id` (`form.js:194`), and the follow-up
          and repeat paths read only the id, the lifecycle and `FollowUpOfMeetingId` (S28-10, S23-8). A new meeting
          (201) is unchanged: its content is what the request carried or the type template.
        - **Report and export decision text.** The report puts the decision text of published minutes into its rows
          (`MeetingReportQueryHandlers.cs:93-97`). For a decision row of a confidential meeting the reader can view,
          the text is the placeholder `MEETING_CONFIDENTIAL_CONTENT` (rendered in 7 languages in the export through
          S28-6's `MeetingReportExportPlaceholders.cs`, and through the reason-code bridge on screen); the code and the
          meeting link stay, so the reader opens the meeting to read it (a recorded open). Screen rows still equal
          export rows (BL-346). A read record per confidential meeting on every report load was rejected: one failed
          record would fail the whole report, and a cadence report is not where an HR one-to-one is read.
        These three are a behaviour change for participants of a confidential meeting and **need owner approval with
        this revision**. Write responses of a non-confidential meeting and the report rows of non-confidential
        meetings are unchanged.
      - **When.** After the access check passes and before the response is built. A caller who cannot view the
        meeting gets the existing 404, and no read record is written: they opened nothing.
      - **Where and what.** The central log (`IAuditService` → `audit_outbox` → `audit_events`), never an equivalent
        trail (AUD-001 §7), through the writer that Q26 decides. The record carries the actor from the token, the
        tenant from the request, `EntityType` `Meeting`, `EntityId` = the meeting id, `SourceModule` `MOD-0357`, the
        surface (`detail`, `tasks` or `minutes`) and the request correlation id, with a fresh correlation id per open.
        It carries no title, description, location, agenda, task or minutes text. Category and operation as Q26
        recommends (`DataPrivacy`; `Execute` with `access = read`, the single-read precedent
        `GetAuditEventByIdHandler` uses), so no shared enum changes. The detail page calls all three endpoints, so
        one page open writes up to three records, one per surface.
      - **Failure: fail-closed.** If the writer reports the record as not queued (or the principal has no recognised
        actor type), the call answers **503 `MEETING_CONFIDENTIAL_READ_NOT_RECORDED`**, and the body carries no
        meeting data. The detail page shows the localized text through the reason-code bridge. **Why closed, not best
        effort:** AUD-001 §7 audits this class because "the access itself is protected information", and a best-effort
        record would hand out an HR-confidential meeting with no trace exactly when the trace fails. The read writes
        no business data, so writing the record first and returning content only afterwards leaves no half state and
        needs no in-transaction path (the K1 gap that blocks K2 writes, AUD-001 §4.4, does not apply). It is the
        posture the module already takes for its export (`MeetingReportQueryHandlers.cs:354-358`, 503). **Stated
        consequence:** while the audit store is down, participants cannot open confidential meetings; other meetings
        are not affected. A record for an open whose response then fails to reach the client is accepted
        (over-recording, never under-recording).
      - **Non-confidential meetings** write no read record and behave as today.

      Tests (HTTP, real Mongo, with the real `IAuditService`): a participant opens the detail, tasks and minutes of a
      confidential meeting, and the central outbox holds one record per call with the actor, tenant, `EntityId`,
      surface and `SourceModule`, and no title. A meeting confidential only through its current type (stored flag
      `false`) is recorded too. The same three calls on a non-confidential meeting write no record. A `read-all`
      non-participant gets 404 and no record. With an `IAuditService` that answers "not queued", each of the three
      calls gives 503 `MEETING_CONFIDENTIAL_READ_NOT_RECORDED` and the body has no meeting field.
      **Content-free paths (pre-review 1 F-38; HTTP, real Mongo, real `IAuditService`).** On a confidential meeting
      whose draft another participant wrote: publish answers 200 with `ContentWithheld = true`, and the body holds none
      of the draft's decision texts or attendee names; save draft and correct answer the same way. A create replay by a
      participant answers 200 with `ContentWithheld = true` and without the stored description and location. A
      participant's report and export show `MEETING_CONFIDENTIAL_CONTENT` for that meeting's decision row and the real
      text for a non-confidential meeting's row. For each of these calls the outbox holds no read record from it, and a
      following `GET /{id}/minutes` writes one. Sabotages: return the stored DTO from `PublishMinutesHandler`; return the
      stored meeting on the replay branch; put `d.Text` back into the report row. Each turns its row red.

**Failure paths.**
- A `read-all` non-participant → 404 with no metadata (not 403).
- Cross-tenant → 404.
- Updating the type without `types-manage` → 403.
- Concurrent type update → 409 (existing).
- An organizer reassigned away loses access unless they are an attendee (existing D4 rule, stated because it now
  matters more).
- A `read-all` non-party who schedules a follow-up with a confidential subtype → 400 `MEETING_CREATOR_CANNOT_VIEW`,
  nothing written (S15-10; review 05 F-29).
- Making a type confidential, or no longer confidential (the closing stamp, pre-review 1 F-43), with a stale version
  or with no Platform transaction available → 409 or 503, and neither the type nor any meeting changes (S15-11).
- An edit of a meeting loaded before its type was made confidential → 409 `MEETING_CONCURRENCY_CONFLICT` (S15-11).
- A single open of a confidential meeting whose read-audit record cannot be written → 503
  `MEETING_CONFIDENTIAL_READ_NOT_RECORDED`, no meeting data (S15-12).

**Tests.** An HTTP matrix of {organizer, attendee, `read-all` non-participant, platform actor, other tenant} ×
{detail, list, minutes, tasks, report, export, related record, **and every write S28 lists**}, run on a confidential
meeting. It reuses S28's write matrix fixture. Also: a real-Mongo test for the five stored dispatch rows; a follow-up
test (S15-10, including the non-party row of review 05 F-29); the type-stamp matrix (S15-11); the read-audit tests
(S15-12); and the source-scan allow-list. Sabotage: remove the confidential branch from `CanView`, then the read
and write matrix rows are red. Make the resolver ignore the flag, then the related-record test is red. Copy the
source title into a confidential follow-up, then S15-10 is red. Pass `RequireCreatorCanView: false` for a non-party
scheduler as well, then the S15-10 non-party row is red (a confidential meeting is written that its scheduler cannot
open). Drop the stamp from `UpdateMeetingTypeHandler`, then the false → true → false rows are red (a `read-all`
non-participant gets 200 after the type is turned off). Drop the `Version` increment from the stamp, then the
stale-edit row is red (the edit writes `false` back). Remove the read-audit call from `GetMeetingByIdHandler`, then
the detail record row is red. Ignore the writer's "not queued" answer, then the 503 rows are red (200 with data).
Fix round 6 adds (pre-review 1): the names-only rows and their sabotage (S15-7, F-40); the race rows and the
closing-stamp sabotage (S15-11, F-43); the content-free rows and their three sabotages (S15-12, F-38); and the
dialog JS test and its sabotage (S15-10, F-45). Restore each one and it goes green.

**L10n.** 7 languages: checkbox, help text and badge, `MEETING_CONFIDENTIAL_READ_NOT_RECORDED` in the reason-code
bridge, the placeholder `MEETING_CONFIDENTIAL_CONTENT` (export placeholders and reason-code bridge, F-38), and the
reworded text of `MEETING_CREATOR_CANNOT_VIEW` (F-45). **Permissions:** `types-manage` sets the flag; no new key.
**Tenant isolation:** unchanged predicates, tenant-scoped repositories; the stamp's `UpdateMany` filters by the tenant.
**Depends on:** S28 (the write guard and resolver caller context it extends) and S25 (`CreateMeetingCommand` already
on path `a`; review 01 F-5; the audit carrier the stamp count uses). S15 does not start until Q27 is answered (A → as
written; B → the slice that moves the meeting content commands to the in-transaction path comes first; C → S15 waits
for that path; §24.25 Q27). S15-12 does not start until Q26 is answered (yes → the shared read-audit writer; no → the
module-local writer). **Estimate:** 3–4 prompts (backend and matrix; type stamp, closing stamp and read audit; the
content-free paths and the names-only rows; UI, dialog, mail and l10n).

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
(existing). No `minutes-publish` → 403. A caller who cannot view the meeting → 404 (S28's write guard; S15 for
confidential meetings).

**Tests.** Unit test of the rule; HTTP publish 409 then 200. Sabotage: skip the check, then the 409 test is red.
**L10n:** 7 languages. **Permissions:** unchanged. **Tenant isolation:** unchanged. **Audit:** `PublishMinutesCommand`
and `CorrectPublishedMinutesCommand` stay in known debt (Q14, §25). **Depends on:** S14 (switch only); S28 (the
404 failure path; review 02 F-16). **Estimate:** 1 prompt.

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
409. No `minutes-write` → 403. A caller who cannot view the meeting → 404 (S28; S15 for confidential meetings).

**Tests.** Unit and HTTP tests; a real-Mongo round trip of a published version. Sabotage: accept the client's snapshot
text, then the snapshot test is red. **L10n:** 7 languages. **Permissions:** unchanged (`minutes-write`,
`minutes-publish`). **Tenant isolation:** unchanged. **Audit:** the minutes commands stay in known debt (Q14).
**Depends on:** S28 (the 404 failure path; review 02 F-16). **Estimate:** 2 prompts.

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
`a` (§25), and their rows follow S15-7's names-only rule on a confidential meeting (pre-review 1 F-40): test through
the real `AuditBehavior` that an update and a delete of a line on a confidential meeting record `Text` as a name and
not its text, with the sabotage of S15-7.

**Acceptance criteria**
- [ ] S18-1 — `MandatoryAgendaItems` (≤ 20 × ≤ 200) is accepted only when `IsQualityRecord = true` (400
      `MEETING_TYPE_MANDATORY_AGENDA_REQUIRES_QUALITY_RECORD`). A line may not repeat an `AgendaTemplate` line (400
      `MEETING_TYPE_AGENDA_LINE_DUPLICATED`).
- [ ] S18-2 — Every meeting of such a type created after this slice gets the mandatory lines from the **server**,
      with `IsMandatory = true`, whatever the client sends. This holds for the Create page, follow-ups (S7) and series
      instances (S11). Order: carried-forward lines first (K6), then mandatory lines in the type's order, then all
      other lines. For follow-ups, this order relies on S25's re-sequencing step in the follow-up handler (S25-8),
      because the carried lines are written after `CreateMeetingCommand` returns.
- [ ] S18-3 — `DeleteAgendaItem` on a mandatory line → 409 `MEETING_AGENDA_ITEM_MANDATORY`. Changing its text through
      `UpdateAgendaItem` → the same 409. `ReorderAgenda` is allowed.
- [ ] S18-4 — When `UpdateMeeting` changes the type to one with mandatory lines, the missing lines are added and none
      are removed. Lines already flagged keep the flag if the type changes away.
- [ ] S18-5 — (Owner answer Q6 = yes, 2026-10-27.) Publish or correct of a meeting that has mandatory lines is
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
unchanged. **Depends on:** S17 (for S18-5), S15 (type commands already audited), S14, S25 (the agenda ordering on
create and follow-up). **Estimate:** 1–2 prompts.

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
  `PlatformSchemaManifest.Meetings.cs` (partial unique index on non-deleted rows).
- New GR-named files `Commands/{Create,Update,Delete,BulkDelete}MeetingCategoryCommand.cs`,
  `Queries/GetMeetingCategory{List,ById}Query.cs`, `Handlers/CommandHandlers/{…}MeetingCategoryHandler.cs`,
  `Handlers/QueryHandlers/GetMeetingCategory{List,ById}Handler.cs` and `Validators/{Create,Update}MeetingCategoryValidator.cs`.
- `MeetingModels.cs`, the type validators and handlers, `API/Controllers/MeetingCategoriesController.cs`
  (`api/v1/meetings/categories`), `MeetingsController.cs` (`lookups/positions`, grouped `lookups/types`), the
  `DependencyInjection.cs` lines, and the audit ledger.

**Acceptance criteria**
- [ ] S19-1 — Collection `meeting_categories` with the **partial** unique index `ux_meeting_categories_tenant_name`
      on `(TenantId, Name)` filtered to `IsDeleted == false` (§24.4), verified against the live index list. Endpoints `GET/POST categories`,
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
- [ ] S19-9 — Re-create and delete again (review 01 F-7): a real-Mongo test creates main type "X", deletes it,
      creates "X" again and deletes it again. Both deletes succeed, and a second **live** "X" is still refused (409
      `MEETING_CATEGORY_NAME_DUPLICATE`). Today's subtype index has the same defect; R1 does not change it (R1-D3,
      S19-7), and it is recorded in §24.24.

**Failure paths.** Duplicate category name → 409 `MEETING_CATEGORY_NAME_DUPLICATE`. Stale version → 409. A category
or position id from another tenant → 400 not-found. No `types-manage` → 403. Deleting a category in use → 409.

**Tests.** Real-Mongo index and deserialization tests; HTTP CRUD with tenant isolation. Sabotage: remove the in-use
check, then the delete test is red. Change the type index, then the S19-7 test is red. **L10n:** reason codes in 7
languages (bridge). **Permissions:** `types-manage`. **Tenant isolation:** `TenantRepository`, tenant-first index.
**Depends on:** S15, because S19 changes the type create and update commands that S15 moves to path `a` (R1-D12;
review 01 F-5). **Estimate:** 2 prompts.

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
languages, and the `MEETINGSERIES` help. Audit: `CreateMeetingSeriesCommand`, `UpdateMeetingSeriesCommand` and
`GenerateDueMeetingSeriesCommand` move to path `a`, and their ledger lines (L77, L172, L118) are removed from
`tests/architecture/audit-ledger/Diten.Platform.md`. S21 changes what the sweep writes: the title from the template,
and the claim advanced on skipped days (review 01 F-2).

**Acceptance criteria**
- [ ] S21-1 — `MeetingSeriesFrequency.Daily = 4` is appended, and both the stepping and the occurrence-index switch
      in `MeetingSeriesSchedule` handle it (unit tests across a month and a year boundary).
- [ ] S21-2 — (Owner answer Q8 = yes, 2026-10-27.) A daily occurrence that falls on a non-working day of the
      tenant's working calendar is **skipped**: the claim advances, no meeting is created, and the sweep response
      counts it as skipped.
- [ ] S21-3 — If `MeetingSeries.TitleTemplate` is set, each generated title is the template rendered for that
      occurrence's start in the tenant time zone. A template with a non-automatic placeholder → 400
      `MEETING_SERIES_TITLE_PLACEHOLDER_NOT_AUTO`. If it is null, the title is `Name` as today.
- [ ] S21-4 — The C# and JS renderers pass the **same** fixture table (ISO week across 31 Dec / 1 Jan, quarter
      edges, a tenant zone different from UTC).
- [ ] S21-5 — The series form offers Daily and the title template. 7 languages. The help is updated.
- [ ] S21-6 — `GenerateDueMeetingSeriesCommand` writes an audit row on path `a`: series id, occurrence, outcome
      (created, created-unchained, skipped or failed: the four values of §25; `created-unchained` comes from S28-11's
      unchained fallback), the created meeting id, the previous meeting id for `created-unchained`, and no titles of
      confidential meetings. A test asserts each of the four outcome values (review 04 F-28 a). These are facts only
      the handler knows (the command carries `NowUtc`, `MaxSeries` and the correlation id), so they go through S25's
      post-handler carrier `MeetingAuditFacts`, whose recording S25-7 measures (review 05 F-30). Before this, the
      slice measures that `AuditBehavior` accepts a command sent with no user (§24.27). If it does not, the slice
      stops and reports.

**Failure paths.** Interval < 1 → 400 (existing). A user placeholder in a series template → 400. A sweep with the job
disabled → nothing generated, as today (BL-536, a configuration question).

**Tests.** Schedule unit tests; the fixture parity test. Sabotage: change `{Week}` in JS only, then the parity test is
red. Remove the skip rule, then the working-day test is red. **L10n:** 7 languages. **Permissions:** `series-manage`.
**Tenant isolation:** unchanged (the sweep already runs inside each tenant's `TenantScope`). **Depends on:** S25 (the
follow-up command the sweep calls is already on path `a`) and S28 (S21-6's `created-unchained` outcome comes from
S28-11's unchained fallback; review 04 F-28 a). **Estimate:** 1–2 prompts (2 with the working-calendar skip,
which the owner approved).

### 24.17 S22 — Create-form auto-fill of the other fields

**Status: draft / not approved.** Scope B. §5.5 question 1. §5.6 "Ekranda doldurma". Work list Dalga 4.2. **Revised
by the owner's "ekle-uc" (2026-10-27):** agenda at create moved to its own early slice **S25** (§24.28). S22 keeps the
auto-fill of every other field and builds on S25's agenda list.

**What the user gets.** Choosing a subtype fills the form, following the manager's text:
- Title = the title template.
- Type = main type / subtype.
- Place = the default place.
- Start = chosen by the user. End = Start + the default duration.
- Description = the description template.
- Participants = the holders of the default positions.
- Follow-up-of = according to the parent rule.
- Agenda = the template lines. These are already on the form since S25. S22 adds S18's mandatory lines as locked rows.

**Files / areas.**
- `MeetingCommandHandlers.cs` (create), `Validators/MeetingValidators.cs` (placeholder rule on create and update).
- New GR-named query files `GetMeetingTypeDefaultAttendeesQuery`, `GetPreviousMeetingSuggestionQuery` and their
  handlers.
- `API/Controllers/MeetingsController.cs` and the Web proxy.
- `Views/Meetings/_Form.cshtml`, `Create.cshtml`, `form.js`.
- resx in 7 languages; the `MEETINGCREATE` help.

**Acceptance criteria**
- [ ] S22-1 — Choosing a subtype fills Title, Location, End (Start + `DefaultDurationMinutes`, recomputed when Start
      changes unless End was edited by hand), Description, Attendees and Follow-up-of, and refreshes S25's agenda
      list. Automatic placeholders re-render when Start changes. User placeholders are highlighted. If the user changes
      the subtype after editing a field, the form asks before overwriting it (`showConfirm`).
- [ ] S22-2 — *Moved to S25 (S25-2).*
- [ ] S22-3 — *Moved to S25 (S25-4).*
- [ ] S22-4 — *Moved to S25 (S25-5).*
- [ ] S22-5 — `GET lookups/type-default-attendees?meetingTypeId=` (`create`) returns the **eligible current holders**
      of the subtype's positions, plus the positions nobody holds (shown as a warning, never a block; BL-356). It
      returns holders only for positions named on that subtype, so it cannot be used to enumerate arbitrary positions.
- [ ] S22-6 — `GET lookups/previous-meeting?meetingTypeId=&before=` (`read`) returns the latest meeting of that
      subtype before Start that the caller **can view** (`CanView`, including the S15 rule). It is used as the
      pre-selection for `PreviousInSeries`. The picker is shown for `UserSelects` and hidden for `None`.
- [ ] S22-7 — A title with an unfilled `{Name}` → 400 `MEETING_TITLE_PLACEHOLDER_UNFILLED` on create and update.
- [ ] S22-8 — *Moved to S25 (S25-6).*
- [ ] S22-9 — `ParticipantSelectionHint` is shown next to the attendee picker. The `MEETINGCREATE` help describes the
      auto-filled fields.
- [ ] S22-10 — S18's mandatory lines appear on the create form's agenda list as locked rows as soon as the subtype is
      chosen. The server still adds them on its own (S18-2), so the form only previews them and is not the authority.

**Failure paths.** A subtype without templates → nothing is filled and the form works as today. A position with no
holder → a warning. No visible previous meeting → empty. No `create` → 403 on the lookups and no Create button.

**Tests.** Lookup HTTP tests (tenant, permission, confidential); placeholder validator tests; JS form tests (the
overwrite confirm, End recompute). Sabotage: let the previous-meeting lookup skip `CanView`, then the confidential
lookup test is red. **L10n:** 7 languages. **Permissions:** `create` and `read` (no new key). **Tenant isolation:**
the lookups use tenant-scoped repositories and the existing eligibility query. **Depends on:** S25, S18, S19, S20,
S21. **Estimate:** 1–2 prompts (it was 2 before the agenda part moved to S25).

### 24.18 S23 — Repeat on create

**Status: draft / not approved.** Scope B. §5.5 question 1 ("Tekrarla"). R1-D7.

**What the user gets.** A "Repeat" option on the create form. When it is ticked, the meeting is created and a series
starts from it. Later instances are generated as for any series, and the Series page remains the place to manage
them.

**Files / areas.** `MeetingModels.cs` (`CreateMeetingRequest.Repeat`: frequency, interval, end, lead time, series
name, title template), `MeetingCommandHandlers.cs` (create sends `CreateMeetingSeriesCommand`),
`MeetingSeriesCommandHandlers.cs` (adopt `CreatedFromMeetingId`), `MeetingSeries.cs`, the schema manifest
`PlatformSchemaManifest.Meetings.cs` (a **partial** unique index, S23-4; review 06 F-33), `_Form.cshtml`, `form.js`,
resx in 7 languages, and the `MEETINGCREATE` / `MEETINGSERIES` help.

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
- [ ] S23-4 — A meeting is adopted by at most one series. A retried create does not create a second series.
      **The index (review 06 F-33).** `ux_meeting_series_tenant_created_from_meeting` on `(TenantId,
      CreatedFromMeetingId)`, `Unique = true`, with `PartialFilterExpression` = `{ CreatedFromMeetingId: { $type:
      "binData" } }`. It is **not** sparse: a compound sparse index holds every document that has any of its fields,
      and `TenantId` is always present, so every unadopted series of a tenant would share the key `(TenantId, null)`.
      A `$exists: true` filter is not enough either: Platform writes a `Guid` as BSON binary
      (`GuidRepresentation.Standard`, `PlatformBsonSerializers.cs:29`; `DependencyInjection.cs:291`) and a null
      `Guid?` as BSON null, because the only registered convention pack is ignore-extra-elements
      (`PlatformBsonConventions.cs:46-49`). The module already records this Mongo rule for
      `RecordLink.IdempotencyKey` (`PlatformSchemaManifest.Meetings.cs:33-37`). Series written before S23 have no
      such field, and today's series create writes none (`MeetingSeriesCommandHandlers.cs:103-120`); both stay outside
      the index. Real-Mongo test: in one tenant, two series stored without the field (the legacy shape) and two new
      series created without adoption are written and the index is built over them, with no error; a second series
      with the same `CreatedFromMeetingId` in that tenant is refused with a duplicate key; the same id in another
      tenant is accepted. Sabotage: drop the partial filter (or use `$exists: true` instead), then the four-series row
      is red (duplicate key on the second null).
- [ ] S23-5 — Partial success is visible (K12). If the series fails (duplicate name, no permission), the meeting is
      still returned with 201 together with `series: { created: false, reasonCode }`, and the UI says "Meeting created;
      repeat was not set" with the reason and a link to the Series page.
- [ ] S23-6 — No third creation path: a source-scan guard asserts that a `Meeting` is constructed only inside
      `CreateMeetingHandler`. Sabotage: add a construction elsewhere, then the guard is red.
- [ ] S23-7 — The form says honestly that later meetings are made by the background job (BL-536).
- [ ] S23-8 — **`CreatedFromMeetingId` is server-only (S28-11; review 02 F-13).** It is a parameter of
      `CreateMeetingSeriesCommand` itself, never a property of `CreateMeetingSeriesRequest` or of any other HTTP
      request model, so no HTTP body can bind it (today the command carries the request model,
      `MeetingCommands.cs:80-81`; `MeetingModels.cs:473-486`). Only `CreateMeetingHandler`'s Repeat step sets it, and
      only for a meeting it has just written (`isNew`). Adoption in `CreateMeetingSeriesHandler` loads that meeting
      through `MeetingWriteAccess` and applies `CanView` for the caller. **Review 03 F-23:** a creator who would not be
      able to view the meeting is already refused before anything is written (S28-10's up-front rule, 400
      `MEETING_CREATOR_CANNOT_VIEW`), so a refused adoption can only follow a concurrent change between the write and
      the adoption (for example, someone removes the creator from the attendees). That case gets no series, reported
      as partial success (S23-5) with its own reason code `MEETING_SERIES_ADOPTION_REFUSED` (7 languages, reason-code
      bridge), never `MEETING_NOT_FOUND`, because the 201 has just told the caller that the meeting exists. The
      meeting is not adopted.
      Source-scan test: no request model in `MeetingModels.cs` has a `CreatedFromMeetingId` property, and exactly one
      file sets the command's value. **HTTP test (review 03 F-23), which states the real invariant: no HTTP body can
      trigger adoption, whatever a field is called.** A `series-manage` holder who can view an existing meeting posts
      to the series create endpoint (`POST api/v1/meetings/series`) with that meeting's id in extra body fields
      (`createdFromMeetingId`, and the same id under other names, for example `adoptMeetingId` and `meetingId`). The
      series is created **without** adoption: its `CreatedFromMeetingId`, `LastGeneratedMeetingId`, `LastGeneratedAt`
      and `LastProcessInstanceId` are empty, and a sweep run when its first occurrence is due generates that first
      instance as for any new series.
      This holds
      because the controller builds the command from the request model (`MeetingSeriesController.cs:54`), so a
      trailing command parameter cannot be bound from the body. Sabotage: add the property to
      `CreateMeetingSeriesRequest` and pass it through in the controller, then both the source scan and the HTTP test
      are red. A test with a concurrent attendee removal gives `MEETING_SERIES_ADOPTION_REFUSED`.

**Failure paths.** Series name duplicate → partial success (S23-5). No `series-manage` → no panel, and the server
refuses only the series part. Interval < 1 → 400 before anything is written. Concurrent sweep → the existing claim
guarantees one instance per occurrence.

**Tests.** Handler and HTTP tests, a sweep-after-create test, the source-scan guard, the S23-4 real-Mongo index test
(review 06 F-33), and the S23-8 HTTP test (extra body fields never adopt). **L10n:** 7 languages (including `MEETING_SERIES_ADOPTION_REFUSED`).
**Permissions:** `create` + `series-manage`. **Tenant isolation:** unchanged. **Audit:** `CreateMeetingCommand` is
already path `a` (S25) and `CreateMeetingSeriesCommand` is path `a` (S21). **Depends on:** S21, S22; S28
(`MeetingWriteAccess` for the adoption, S23-8). **Estimate:** 2 prompts.

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
      separate "flag changes" block. **A change of an existing subtype's `isConfidential` runs the type stamp
      (pre-review 1 F-39; R1-D5; S15-11):** the commit calls `MeetingTypeConfidentialityStamp.ApplyAsync` for that
      subtype inside the S24-9 transaction, so false → true stamps its meetings and true → false runs the closing
      stamp before the flag is lowered, exactly as `UpdateMeetingTypeHandler` does. The importer has no other path to
      `Meeting.IsConfidential`.
- [ ] S24-7 — `defaultActionTaskTypeCode` is resolved against the tenant's active task types through
      `GetActiveTaskTypesQuery` (read-only). An unknown code, or a dual value such as `A / B`, leaves the field
      **empty** on create or **unchanged** on update, and is reported. No task-type data (MOD-0024) is ever written.
- [ ] S24-8 — Position codes are resolved through `GetPositionsQuery`. An unknown code is dropped from that row and
      reported.
- [ ] S24-9 — `POST types/import` recomputes the plan and refuses with 409 `MEETING_TYPE_IMPORT_PLAN_CHANGED` if its
      hash differs from the preview's. It writes main types first and then subtypes, in **one transaction** using the
      same Mongo session mechanism as `PublishMinutesHandler`. If that is not available for these repositories, the
      slice stops and reports to the CT instead of writing row by row. The S24-6 stamp writes are part of that one
      transaction: a failed stamp aborts the whole import, and no subtype flag and no meeting changes.
- [ ] S24-10 — Audited path `a`: one event `meetings.type-catalogue.imported` with the file SHA-256, the counts, the
      created ids and the updated ids with their changed field names, and for each subtype whose `isConfidential`
      changed, its id and `ConfidentialStampedMeetingCount` (§25).
- [ ] S24-11 — No catalogue data file is added to the repository by this slice.

**Failure paths.** File too large or too many rows → 400. Plan changed between preview and commit → 409. No
`types-manage` → 403 and no page. Cross-tenant: impossible by construction, because only the caller's tenant is read
and written.

**Tests.** Preview has no side effects; second-run idempotency; the conflict and legacy-assignment cases; code
resolution; the plan-hash 409. **Stamp through the importer (pre-review 1 F-39; real Mongo):** subtype T is
non-confidential and has a scheduled, a completed and a soft-deleted meeting. A file sets `isConfidential: true` for
T: the three meetings store `true` with `Version` + 1, the audit record names T with a stamp count of 3, and a
`read-all` non-participant gets 404 on each live one. A second file sets it `false`: the meetings still store `true`,
and the non-participant still gets 404. A third file sets it `true` again: nothing is stamped (count 0). A fault in
the stamp aborts the import, and T and its meetings are unchanged. Sabotage: skip the stamp call in the importer,
then the second file's rows are red (200 after the lowering). Remove the hash check, then the 409 test is red.
Sabotage: match on subtype name only (ignoring the main type), then the conflict test is red. **L10n:** 7 languages. **Permissions:** `types-manage`.
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
`types/import/preview`, `types/import`, and from "ekle-uc" `{id}/attendees/{userId}/role`) falls under the catch-all.
S25 and S26 add no route: they extend `POST /`. The Web proxy has **no** catch-all, so every slice
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
- [ ] Review 01 findings F-1 to F-10 and review 02 findings F-11 to F-17 are closed in a new independent review of
      this version. S25 does not start until Q23 is answered; yes → the shared mirror gets `["Meetings"] = 18` (written
      by this CT after the owner's explicit XMC-001 answer, reviewed by the library owner, per card S-90 §7c); no →
      `PlatformConfiguration` is used and Q13 is reopened. S28 is the first slice dispatched.
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
- Fail-closed audit of publishing and correcting quality-record minutes (Q14 (d), AUD-001 §4.3 K2): today these two
  commands are in known debt; they move to the in-transaction audit path once K1 is closed. Owner to schedule.
- **Subtype index defect (review 01 F-7).** `ux_meeting_types_tenant_name` on `(TenantId, Name, IsDeleted)`
  (`PlatformSchemaManifest.Meetings.cs:131-136`) refuses the **second** soft delete of a name that was deleted,
  re-created and deleted again. The `meeting_series` index has the same shape (`:180-185`). The fix is the partial
  index S19 uses for main types. It is a schema change on an existing collection, so it is a **DL-1 correction** with
  its own WP row: drop and create the index, plus a re-create-and-delete test. R1 does not change it.
- **Task side of BL-541.** Anything the Task Center itself shows about meetings beyond the related-record row S28
  handles is MOD-0024's (XMC-001). The masked-row alternative in Q24 was not chosen (owner answer Q24 = drop); it
  would be such a MOD-0024 change if it were ever wanted.

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
   subtype's agenda template from the server (S25-5, formerly S22-4)? This changes today's behavior. Recommended: yes.
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
| Q13 | **CT decision (2026-10-27): append `AuditCategory.Meetings = 18`** to `services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs` (never renumber; stored values stay). Reason: every module that writes audit rows in Platform has its own category (`PortfolioDelivery = 15`, `Tasks = 16`, `ESignature = 17`); putting meeting events under `PlatformConfiguration = 4` would hide them among platform settings in any per-category filter or export. The slice that first writes a meeting audit row (S25) adds the value and any category label it needs, in 7 languages if the label is user-facing. |
| Q14 | **CT decision (2026-10-27), within AUD-001 §4.3–§4.4 and open item K10:** (a) `PublishMinutesCommand` and `CorrectPublishedMinutesCommand` are treated as **K2 class**, because for quality-record types the published minutes are a GxP record (AUD-001 §4.3 "GxP kaydı"; the command, not the row, carries the class). They stay in the ledger's known debt; **no R1 slice moves them to path `a`** (that would create new K2 debt, §4.4), even where S16, S17, S18 or S27 change their rules; they move only when the in-transaction path (K1) exists. (b) `SaveMinutesDraftCommand` is a working copy, **not K2**; it also stays in known debt in R1 (its change in S17 is a field addition) — moving it to path `a` is allowed and left to the slice. (c) Main-type, subtype and importer commands are **configuration, not K2** (path `a`, best effort, not silent). A change of a type's `IsQualityRecord` / `RequiresESignature` flag is configuration too, but it is QA-controlled; its audit row must carry the old and new value. (d) That GxP minutes publishing has no fail-closed audit today is recorded as a follow-up for the owner (§24.24); it is a real ISO 9001 / Annex 11 gap that R1 does not close. The K10 boundary itself remains an owner decision. |
| Q15 | Yes — S13 (page help and tour) first. |
| Q16 | Yes — templates are tenant data in one language per subtype. |
| Q17 | Open — the QA process owner's name (blocks only the real catalogue import). |
| Q18 | Answered by the CT: the manager's document arrived on 2026-10-27 as a hand-over note on central record coding (RCS-001 / BL-565). It is consistent with R1 (no module-local counter; code field deferred to the shared service). No conflict. |
| Q19 | Yes (owner, "dort-evet", 2026-10-27) — with "attendance mandatory", only REQUIRED attendees must be marked before publishing; the organizer is always required. |
| Q20 | Yes — "Preparation: x / y done" counts only tasks the reader may see, plus a neutral "k more you cannot open" line (a number, no titles). |
| Q21 | Yes — the preparation reminder goes to the organizer only, about 24 hours before the start, once per start time. |
| Q22 | Yes — adding preparation lines on the create form needs `platform.tasks.create` as well as `platform.meetings.create` (the existing two-key rule of "task from meeting"). |
| Q24 | Drop (owner, "duzelt-2", 2026-10-27/28; the default was taken) — a task reader who cannot view the linked meeting does not see that meeting row at all (S28-7). No MOD-0024 change. |
| Q25 | Yes — add the scheduler as attendee (owner, "devam-katilimci", 2026-10-29): when an attendee who is not the organizer schedules a follow-up, the scheduler is added automatically as an attendee of the new meeting. This is alternative (a) of Q25, not the recommendation the question carried ("leave the path as it is today"). It is built in S28 (S28-10, "Scheduler as attendee"). **Two CT readings, not said by the owner:** (1) `Accepted` is taken as the default response, because the owner said "added as attendee" without a response status, and a `Pending` row would show the scheduler an invite for their own follow-up (S28-10); (2) only a party of the source (its organizer or an attendee) is added, not a `read-all` holder or platform operator who schedules a follow-up. A confidential follow-up keeps the scheduler (S15-10). |
| F-34 (owner decision) | **Owner decision, not a CT reading** ("duzelt-5", 2026-10-29, given through the button text the owner accepted verbatim: "F-34 için tür gizli yapılınca eski toplantılar kalıcı gizli işaretlensin"): when a type is made confidential, its existing meetings are marked confidential for good. Built in S15 (S15-11) and stated in R1-D5. **CT readings of the details, not said by the owner:** every lifecycle state and soft-deleted rows are stamped; the stamp and the type update are one Platform transaction; each stamped row's version moves. |
| F-37 (owner decision) | **Owner decision, not a CT reading** ("duzelt-5", 2026-10-29, given through the button text the owner accepted verbatim: "F-37 için gizli toplantı İK-gizli sayılsın ve tekil açılışı denetlensin"): a confidential meeting counts as an HR-confidential record (AUD-001 §7, third class), and its single open is audited. Built in S15 (S15-12) with read rows in §25. **CT readings, not said by the owner:** the record is fail-closed (503 when it cannot be written); the covered opens are detail, tasks and minutes; the writer follows Q26. |

**Questions from "ekle-uc" (2026-10-27). All four were answered yes by the owner ("dort-evet", 2026-10-27); see
the Q19 to Q22 rows of the table above. The text below is kept as asked:**

19. **Q19 — Optional attendees and mandatory attendance (S27, S16).** When a type has "Attendance mandatory", must
    **only required** attendees be marked before publishing? Optional attendees could still be marked if someone
    wants to. Recommended: yes, required only. The organizer is always required. Q5 (declined invitees are marked)
    then applies to required attendees.
20. **Q20 — What "Preparation: x / y done" counts (S26, BL-541).** Recommended: count only the preparation tasks the
    reader may read under the task read rule. When there are others, add a neutral line "k more preparation
    task(s) you cannot open", which shows a number but no title, assignee or date. The alternative is counting only
    visible tasks and showing nothing about the rest. Which one?
21. **Q21 — Who gets the preparation reminder (S26).** Recommended: the organizer only, about 24 hours before Start,
    once per start time. Should the owners of the open preparation tasks also get it? They would be reminded by the
    task's own due-date notices, if MOD-0024 sends those.
22. **Q22 — Who may add preparation lines on the create form (S26).** Recommended: a creator who also holds
    `platform.tasks.create`. This is the same two-key rule (K9) the existing "task from meeting" action uses, and no
    key changes meaning. Is that accepted?

**Note on Q13 (review 01 F-3 and F-5).** The CT decision row above names "S25". Read it as **the first slice that
writes a meeting audit row**, which is S25 in the §24.7 order (S28 writes none, R1-D12). The decision also needs one
file the row did not name. `AuditCategory` has a **mirror** in the shared library:
- the mirror itself, `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs`
  (the `Categories` map, which today ends with `["ESignature"] = 17`);
- its drift test, `services/Diten.Building.Blocks/Diten.BuildingBlocks.CentralAudit.Tests/CentralAuditDispatcherTests.cs:155`
  (`The_platform_enum_mirror_matches_AuditEnums_cs`), which reads `AuditEnums.cs` and fails when the two lists
  differ.

Appending `Meetings = 18` without the mirror turns that test red. The mirror is outside this module (AGENTS §4:
`Diten.Building.Blocks` is a shared library), so editing it is an XMC-001 question: Q23. S25 does not start until Q23
is answered; yes → the shared mirror gets `["Meetings"] = 18` (written by this CT after the owner's explicit XMC-001
answer, reviewed by the library owner, per card S-90 §7c); no → `PlatformConfiguration` is used and Q13 is reopened.

**Questions from review 01 (2026-10-27). Q24 is answered (owner, "duzelt-2"; see the Q24 row of the table above).
Q23 is still open. The text below is kept as asked:**

23. **Q23 — Shared audit mirror (XMC-001, AGENTS §4).** May the first slice that writes a meeting audit row (S25)
    add `["Meetings"] = 18` to `CentralAuditPlatformValues.cs` in `Diten.Building.Blocks`, together with
    `AuditEnums.cs`, so the drift test stays green? The change is one line in a shared library outside this module.
    S25 does not start until Q23 is answered; yes → the shared mirror gets `["Meetings"] = 18` (written by this CT
    after the owner's explicit XMC-001 answer, reviewed by the library owner, per card S-90 §7c); no →
    `PlatformConfiguration` is used and Q13 is reopened.
24. **Q24 — A task reader who cannot view the linked meeting (S28-7).** *(Answered: drop.)* The recommended answer,
    and what S28 builds, is to **drop** the meeting from the task's related-record list. This needs no MOD-0024
    change. The alternative keeps the row and masks the title with a code, the way the meeting side already masks
    unreadable tasks. That needs the Task Center renderer (`WorkCenterNext/app.js`, MOD-0024 frontend) to show the
    new code in 7 languages, which is an XMC-001 change in another module. Is dropping accepted?

**Question from review 03 (2026-10-29, fix round 3). Answered by the owner ("devam-katilimci", 2026-10-29; see the
Q25 row of the table above): alternative (a), built in S28 (S28-10). The text below is kept as asked:**

25. **Q25 — A follow-up scheduled by someone who is not on it (S28-10).** *(Answered: (a), the follow-up adds its
    scheduler as an attendee; S28-10.)* S28-10 refuses a create up front when the
    creator would not be able to view the new meeting (400 `MEETING_CREATOR_CANNOT_VIEW`), but not on the follow-up
    path. The follow-up dialog sends no attendees and no organizer (`follow-up-dialog.js:91-93`), so when an attendee
    of a meeting who is not its organizer schedules a follow-up, the new meeting has only the source organizer on it.
    The scheduler cannot open it, and the dialog then sends them to its page. Recommended for S28: leave this path as
    it is today (no refusal), so no follow-up that works today is refused. The two alternatives change today's
    behaviour: (a) the follow-up adds its scheduler as an attendee, or (b) the follow-up path is refused like a
    create. Which one, and in which slice?

**Question from review 06 (2026-10-29, fix round 5). Open:**

26. **Q26 — Shared read-audit writer for HR-confidential single opens (XMC-001, AUD-001 §7; S15-12).** MOD-0357 is
    the first module that audits single opens of an HR-confidential record ("today: no module does read audit in this
    class", AUD-001 §7). The two central writers that exist are `IAuditMetaAuditWriter` (the audit module watching
    itself: platform-global meta-audit, failures swallowed) and `IDataExportAuditWriter` (bulk export: tenant-owned,
    fail-closed), both in `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/Services/`, which is
    the Audit feature's area, not this module's. Neither fits: the first is the wrong kind of record, and the second
    records `AuditOperation.Export` with export fields. **Recommended: yes**, a shared writer next to them, built by the
    Audit feature's owner or by this CT after the owner's explicit XMC-001 answer and reviewed by that owner (card S-90
    §7c). Recommended shape, copied from `IDataExportAuditWriter`: `ISensitiveRecordReadAuditWriter.RecordAsync(entry)`
    with the entry `{ SourceModule, RequestType, EntityType, EntityId, Surface, RequestCorrelationId }`; the actor
    taken only from the token (`AuditActorTypeResolver`, an unattributable principal refused), the tenant only from
    the request, a fresh correlation id per open, category fixed by the writer to `DataPrivacy` (an existing value)
    and operation `Execute` with metadata `access = read` (the single-read precedent of `GetAuditEventByIdHandler`), so
    no shared enum or mirror changes; it never throws for an audit failure and returns `IsRecorded`. Later modules
    in this class (HR, pharmacovigilance) then reuse one rule. **If no:** MOD-0357 adds its own
    `Features/Meetings/Services/MeetingConfidentialReadAuditWriter.cs` with the same contract, over the existing
    public `IAuditService.AppendAsync` (the call `DataExportAuditWriter` itself makes), and a later shared writer
    replaces it. S15-12 does not start until Q26 is answered; the rest of S15 does not wait.

**Question from pre-review 1 (S-93) F-41 (2026-10-29, fix round 6). Open; the author does not decide it (CT
instruction):**

27. **Q27 — Are writes on a confidential meeting K2 class? (owner, with the CT: AUD-001 §10 K10 is the K2 boundary in
    Platform; §4.3, §4.4, §7.)** The owner decision on F-37 made a confidential meeting an HR-confidential record.
    AUD-001 puts HR-confidential records in **§7** (single opens are read-audited, which S15-12 does fail-closed). Its
    **§4.3** K2 class (fail-closed writes: "kayıt ile iş verisi BİRLİKTE yazılır") names identity, role/permission,
    tenant state, GxP records and **KVKK special-category data**, not HR-confidential records as such. A confidential
    meeting may still hold special-category data in its free text (an HR one-to-one about health). The commands that
    write such a meeting's content or people are `CreateMeeting`, `UpdateMeeting`, `ReassignMeetingOrganizer`,
    `AddMeetingAttendees`, `RemoveMeetingAttendee`, the four agenda commands, `ScheduleFollowUpMeeting`,
    `SaveMinutesDraft`, `PublishMinutes`, `CorrectPublishedMinutes`, and the stamp rows of `UpdateMeetingType` and the
    importer. Path `a` writes their record after the handler, best effort (`AuditBehavior.cs:233-249`; AUD-001 §4.2).
    AUD-001 §4.4: "K1 kapanana kadar yeni K2 sınıfı Platform komutu = engelli", and the ledger accepts no new K2 debt.
    **Measured record inconsistency for the CT:** AUD-001 §4.4 and §10 K1 still call the in-transaction path `aday`,
    but the Platform ledger lists it as accepted path-`a` traces (`platform-islem-ici`, evidence 2026-10-02,
    WP-PLATFORM-AUDIT-INTX-01; `esign-islem-ici-denetim`, CT decision 2026-10-14), through
    `ITransactionalAuditOutboxWriter.TryEnqueueAsync(session, …)` and the 25-name list
    `TransactionOwnedAuditCommands.Authorized` (adding a name is a CT decision).
    - **(A) Not K2. Recommended.** Writes on a confidential meeting are "other business records" (AUD-001 §4.3 third
      row): path `a`, best effort with a warning log, and from pre-review 1 F-40 their rows carry field names only.
      Single opens stay fail-closed (S15-12). **S15, S18, S25, S27:** as written. **S28:** unaffected (it writes no
      audit row, and no meeting is confidential before S15). **Estimate:** no change (27 to 33).
    - **(B) K2, delivered in R1 through the in-transaction path.** Every command listed above writes its record in the
      same Platform transaction as its business write (`ITransactionalAuditOutboxWriter`, the `platform-islem-ici`
      pattern) and fails with 503 when the record is not queued. A command's path is fixed by its type, not by the
      meeting it touches (a per-meeting switch would write two records or none), so this applies to **every**
      meeting. Needs: the CT's decision to add the command names to `TransactionOwnedAuditCommands.Authorized`, the
      CT reconciling the K1 text above, and a transaction in handlers that have none today. **S25:** its path-`a` move
      of `CreateMeeting` and `ScheduleFollowUpMeeting` is replaced by the in-transaction move (the carrier stays for
      the handler facts). **S15, S18, S27:** their moves become in-transaction moves, and `PublishMinutes` and
      `CorrectPublishedMinutes` leave the K2 debt (Q14 (a)). **S28:** unaffected. **Estimate:** one more slice before
      S15, about 3 to 4 prompts (30 to 37). **Stated consequence:** while the audit store is down, no meeting can be
      changed.
    - **(C) K2, not delivered in R1.** The commands are K2 when they touch a confidential meeting, and §4.4 blocks them.
      S15 does not start until the CT accepts the in-transaction path for these commands (then (B) is built), and the
      slices that depend on S15 directly or through another slice (S18, S19, S20, S22, S23, S24 and S27; §24.7) wait
      with it. The block is then
      recorded in §24.24 and with the CT. **S25, S28:** unaffected. **Estimate:** no change in prompts; HR one-to-one meetings, the reason S15
      exists, cannot be used until then.

    **Recommendation: (A).** (1) AUD-001 maps the owner's "HR-confidential" to §7, whose fail-closed read S15-12
    already delivers; the §4.3 K2 write class names special-category data, for which the module has no field.
    (2) The K2 boundary in Platform is the open decision K10, and the ledger says that widening K2 adds lines to
    `## K2 borcu` and raises its fixed count; (A) adds none. (3) After F-40, a confidential write's record carries no
    content, so a lost record loses "who changed which field when", and path `a` logs every lost record as a warning
    (§4.3 "best effort, not silent"). (4) (B) puts every meeting write handler, most of them delivered in S1 to S12,
    into a transaction during a revision, and stops every meeting edit when the audit store is down. (C) blocks the
    HR one-to-one use S15 exists for. **Residual risk of (A), stated:** special-category text in a confidential
    meeting is written best effort, as it is in any free-text field of a non-K2 module, and the published minutes stay
    in K2 debt (Q14 (a)) under (A) and (C). If the owner wants that closed, (B) is the way, as its own slice.
    **S15 does not start until Q27 is answered** (A → as written; B → the in-transaction slice comes first; C → S15
    waits for that path). S18 and S27 follow the answer through S15. S25 and S28 do not wait.

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
| E-mail (SMTP) settings and the background jobs switched on in live | Notifications (platform) · live environment settings (BL-536) | Owner (live configuration) | Invite mail (S15-6, S25); series generation after Repeat (S23); the preparation reminder (S26-6 sends nothing while jobs are off) |
| `platform.tasks.create` for organizers, and positions for the people they assign preparation tasks to (assignment scope, BL-356) | MOD-0018 · `Views/Governance/Roles` · MOD-0299 · `Views/Organization/PositionAssignments` | Tenant administrator / HR | Preparation lines on the create form (S26) |

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
- Work list **§6 item 6a** was not in `2904f4029` when R1 was drafted. It is now committed, in `53dc6a97f`
  (corrected after review 01 F-10).
- Whether `AuditBehavior` (path `a`) accepts a command sent with **no user** (the series sweep and the follow-up path
  send `CreateMeetingCommand` with `Guid.Empty`). **S25** (the first slice that changes `CreateMeetingCommand`) must
  measure this before moving it to path `a`. The same question applies to S26's reminder job. If `AuditBehavior` does
  not accept such a command, the slice stops and reports to the CT.
- Whether K7 ("rescheduling or cancelling notifies every linked task's owner", §16) is implemented today. Its §16 box
  is unticked, and S26-7 depends on the answer.
- Whether the in-process call of `CreateTaskFromMeetingCommand` from the create handler gets the bridge's
  idempotency (`ResolveForTaskBridge`) and refusal codes unchanged. This was read in the code, not run.
- How mail clients (Outlook, Gmail) show `ROLE=OPT-PARTICIPANT` in the `.ics` (S27).
- The server-side source of the tenant time zone for S21 and S22. `TimesheetWeekReader` reads it from working hours
  (`hours.TimeZone`); S21 confirms the same seam.
- The working-calendar read that Daily's skip rule (S21-2) would use.
- Whether a Mongo session or transaction is available to the category and type repositories (S24-9).
- Which administrators can read stored `NotificationDispatch` rows (subject, variables, attachments). This is the
  reason for S15-6, but the reader surface was not opened.
- Whether the shared DataTable v2 standard supports row grouping (S20-2 falls back to a column, a filter and a sort
  order).
- ~~That `TaskTypeDto` carries `Code`~~: measured by review 01 (it does).
- The help code of the minutes editor page (S13-7).
- The prompt estimates are estimates, not measurements.
- **Added in fix round 1 (review 01); wording updated in fix round 2 (review 02 F-13).** Every write path that
  reaches an existing meeting from outside an HTTP request still has to be listed, so that S28's system-origin marker
  (an acting identity, the series organizer, never a bypass) is set by exactly the series sweep and nothing else.
  Read so far: `GenerateDueMeetingSeriesHandler` → `CreateMeetingCommand` and `ScheduleFollowUpMeetingCommand`
  (`GenerateDueMeetingSeriesHandler.cs:160`, `:181-182`). S28-1 measures the rest.
- That nothing outside `Features/Meetings/**` sends a meeting write command in-process. The Task Center's
  `MeetingWorkItemActionDispatcher` sends `RespondToInvitationCommand`, which is attendee-only already. S28 measures
  this.
- **Added in fix round 3 (review 03).** Not checked: whether a test package already brings `Microsoft.CodeAnalysis`
  in transitively (only direct references were searched; S28-2), and how the S28-8 completion test injects its fault
  after `CreateMeetingCommand` (a failing link service or a hand-built dispatcher; the slice chooses).
- **Added in fix round 4 (review 04 and Q25).** Not checked: whether any real caller of the follow-up endpoint
  falls outside the eligibility filter. This is the reason `RequireCreatorCanView: false` stays (S28-10); S28
  measures it. **Known wording gap, no new code:** when a follow-up retry meets a cancelled or completed follow-up
  of the source (S28-10, review 04 F-26), the dialog shows the existing lifecycle text through the reason-code
  bridge (`follow-up-dialog.js:85-97`), "This meeting is cancelled." / "This meeting is completed."
  (`MeetingsIndex.en.resx:86-87`). The dialog is opened from the source meeting's page, so the text may read as
  if the **source** were cancelled or completed. R1 accepts this, because the case needs a cut-off first call and
  a later cancel or completion; a distinct code would add 7-language text for it.
- **Added in fix round 5 (reviews 05 and 06).** Not run, read only: that `AuditBehavior` records a value the handler
  writes into the carrier after the plan is built (read at `AuditBehavior.cs:74-80, 179-180, 268-290`; S25-7 measures
  it); that the deployed MongoDB accepts `$type` in a partial index filter (supported by the server since 3.2; S23-4's
  real-Mongo test proves it); that the Platform transaction executor accepts an `UpdateMany` in its session for the
  number of meetings one type can have (S15-11; one statement, no loop); how the detail page shows a 503 on one of
  its three calls (S15-12; the reason-code bridge text is required, the layout is the slice's choice). The
  attendance-mandatory gap between S16 and S27 for a reassigned organizer without a row is stated in S27-8.
- **Added in fix round 6 (pre-review 1).** Not run, read or reasoned only: that the raise's `UpdateMany` inside a
  Platform transaction does not see a meeting inserted after the transaction began (MongoDB snapshot isolation; the
  S15-11 race rows prove the closing stamp, not this premise); that no client other than the Meetings screens reads
  the stored content of a minutes write response or a create replay (the Meetings JS was read, `minutes-editor.js`,
  `form.js`, `follow-up-dialog.js`; other callers of the API were not searched); and which AUD-001 text governs the in-transaction path, §4.4 / §10 K1
  (`aday`) or the Platform ledger (accepted, 2026-10-02 and 2026-10-14) (Q27).

### 24.28 S25 — Agenda at create (today's type template)

**Status: draft / not approved.** Owner "ekle-uc" (2026-10-27). Plan §5.5 question 1 ("davet e-postası
gündemsiz gidiyor"). R1-D6.

**Measured today (@826150ef4).**
- `CreateMeetingRequest` has no agenda field (`MeetingModels.cs:143-152`).
- `form.js:196-198` redirects a new meeting to `/Meetings/{id}?prefillAgenda=1`. The detail page then posts the
  type's `AgendaTemplate` lines one by one (`form.js:417-457`).
- `CreateMeetingHandler` sends the invite mail **inside** the create call (`MeetingCommandHandlers.cs:199`, after the
  attendee rows). So every invitation today goes out **before** any agenda line exists.
- `ScheduleFollowUpMeetingHandler` calls `CreateMeetingCommand` first (`MeetingFollowUpCommandHandlers.cs:92`). It
  then writes the carried-forward lines with `SortOrder` starting at 0 (`:230-248`). Once create writes template
  lines from 0 as well, the two sets would collide (review 01 F-2).

**What the user gets.** The create form shows an **editable agenda list**, pre-filled from the selected type's
current `AgendaTemplate`. The user can add, remove, edit and reorder lines before saving. The lines are saved together
with the meeting, so the invitation is sent for a meeting that already has its agenda.

**Files / areas.**
- `MeetingModels.cs`: `CreateMeetingRequest.AgendaItems`, an additive and nullable list of strings. S27 later turns
  each line into `{ Text, PresenterUserId?, DurationMinutes? }`.
- `Commands/CreateMeetingCommand.cs`, moved to its own file if that is needed to add the audit interfaces.
- `MeetingCommandHandlers.cs` (create), `Validators/MeetingValidators.cs`.
- `Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs` (S25-8 re-sequencing) and
  `Commands/ScheduleFollowUpMeetingCommand.cs` (path `a`).
- New `Features/Meetings/Services/MeetingAuditFacts.cs` (one public type): the post-handler audit carrier of S25-7
  (review 05 F-30), used by every meeting command that moves to path `a` and needs a fact only its handler knows.
- `Views/Meetings/_Form.cshtml`, `Create.cshtml`, `form.js` (agenda list; remove the `?prefillAgenda=1` redirect and
  reader).
- resx in 7 languages; the `MEETINGCREATE` and `MEETINGDETAIL` help.
- `tests/architecture/audit-ledger/Diten.Platform.md`: remove `CreateMeetingCommand` (L76) and
  `ScheduleFollowUpMeetingCommand` (L156).
- **Audit category (Q13, Q23).** S25 is the first slice that writes a meeting audit row. It appends `Meetings = 18`
  to `services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs` **and** the shared mirror
  `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs`, keeping the
  drift test `CentralAuditDispatcherTests.cs:155` green. S25 does not start until Q23 is answered; yes → the shared
  mirror gets `["Meetings"] = 18` (written by this CT after the owner's explicit XMC-001 answer, reviewed by the
  library owner, per card S-90 §7c); no → `PlatformConfiguration` is used and Q13 is reopened.

**Acceptance criteria**
- [ ] S25-1 — On Create, choosing a type fills an editable agenda list from that type's `AgendaTemplate` (the
      existing `lookups/types` already returns it). If the user changes the type after editing the list, the form asks
      before replacing it. On Edit, the list is not shown; the agenda keeps being edited on the detail page as today.
- [ ] S25-2 — `CreateMeetingRequest.AgendaItems` (≤ 50 lines, each trimmed and ≤ 500) follows R1-D6: `null` means
      the server applies the type's template, `[]` means none, a list means exactly those lines. Lines are written in
      the same `isNew` block as the attendee rows and **before** `SendInviteAsync`. A test asserts the order of the
      writes.
- [ ] S25-3 — When S18 is present, its mandatory lines are added by the server on top of the client's list (S18-2).
      Until S18 lands, S25 behaves without them. This is not a hard dependency.
- [ ] S25-4 — The post-create `?prefillAgenda=1` path is removed, so the agenda is never applied twice. An old URL
      that still carries the parameter changes nothing (JS test).
- [ ] S25-5 — Follow-ups (S7) and series instances (S11) send `AgendaItems = null` and get the type template after the
      carried-forward lines (owner answer Q7 = yes). The carried lines stay first (K6). Real-Mongo test through the
      sweep and through `ScheduleFollowUpMeetingCommand`.
- [ ] S25-6 — A retried create (same server idempotency key) returns the same meeting **only to a caller who can
      view it** (S28-10, 200), and its agenda is **not** duplicated. A caller who cannot view the existing meeting
      gets S28-10's fixed 409 body, and no agenda line is written (review 02 F-11).
- [ ] S25-7 — `CreateMeetingCommand` and `ScheduleFollowUpMeetingCommand` move to audit path `a`. Before that, the
      slice measures that `AuditBehavior` accepts them when sent with no user (sweep and follow-up, §24.27). If it
      does not, the slice stops and reports. The audit category follows Q23. S25 does not start until Q23 is
      answered; yes → the shared mirror gets `["Meetings"] = 18` (written by this CT after the owner's explicit
      XMC-001 answer, reviewed by the library owner, per card S-90 §7c); no → `PlatformConfiguration` is used and Q13
      is reopened. `AuditTrailStandardTests` and the Building.Blocks drift test are both green.
      **Deferred S28 changes are recorded (R1-D12 stated deferral; review 04 F-28 b, Q25).** The
      `ScheduleFollowUpMeetingCommand` event carries `SchedulerAddedAsAttendee`, and the `CreateMeetingCommand` event
      carries `PreAcceptedUserId` when it is set. Test: a follow-up scheduled by a source attendee who is not the
      organizer gives `SchedulerAddedAsAttendee = true` and a `CreateMeetingCommand` event naming that scheduler as
      `PreAcceptedUserId`; a follow-up scheduled by the source organizer gives `false` and no `PreAcceptedUserId`.
      **Post-handler carrier (review 05 F-30).** `AuditBehavior` takes the command's `AuditRequestMetadata` before the
      handler runs (`AuditBehavior.cs:47, 143`), so a command's own fields cannot hold `SchedulerAddedAsAttendee`, the
      id of an existing meeting that answered a replay, the carried line count, the effective confidentiality (the
      names-only rule, S15-7) or, later, a stored before-value. (The new meeting's own id is generated on the command
      instead; see `EntityId` below.) It does read the metadata's dictionaries only when it builds the record, after the handler
      (`:74-80`; `AfterState` and `Metadata` at `:179-180`; `MergeMetadata` enumerates at `:268-290`). S25 therefore
      adds `MeetingAuditFacts`: a command that needs handler facts holds one instance (created with the command,
      never bound from HTTP, the same rule as the S28-4 marker), hands its `Before` and `After` dictionaries to
      `GetAuditMetadata()`, and its handler fills them before it returns. Command fields (for example
      `PreAcceptedUserId`) go in directly. **Measurement first:** a test sends a carrier command through the real
      MediatR pipeline with the real `AuditBehavior` and a recording `IAuditService`, and asserts that a value the
      handler wrote after the plan was built is in the appended record. Sabotage: make `GetAuditMetadata()` copy the
      dictionaries (a snapshot), then the measurement is red. If the measurement is red on the real code, the slice
      stops and reports to the CT; then every carried field leaves §25 and R1-D12's deferral rests on its conditions
      (1) and (3), and no meeting row writes a content value (`Title`, `Description`, `Location`, agenda `Text`) for
      any meeting, because such a value is written only through the carrier (S15-7, pre-review 1 F-40). The carrier
      depends on how a shared behaviour reads its input, so this test is what keeps a later change of `AuditBehavior`
      from dropping the fields silently.
      **`EntityId` of a create comes from the command (pre-review 1 F-42).** `AuditRequestMetadata.EntityId` is a
      `Guid?` value fixed when the plan is built (`AuditRequestMetadata.cs:9`; `AuditBehavior.cs:143, 168-187`), so the
      carrier cannot set it. `CreateMeetingCommand` and `ScheduleFollowUpMeetingCommand` therefore carry a server-only
      `NewMeetingId`, generated when the command is built (an `init` property defaulting to a new id; never on an HTTP
      request model, held by the S28-10 reflection test). `CreateMeetingHandler` writes the new meeting with that id,
      and `GetAuditMetadata()` returns it as `EntityId`. `ScheduleFollowUpMeetingHandler` passes its own `NewMeetingId`
      to the `CreateMeetingCommand` it sends, so both records name the same meeting. On a replay (`isNew = false`) or a
      refusal, `EntityId` names an id that was never written; the carrier then records `ReplayOfMeetingId` (the
      existing meeting) for a replay, and a refusal has `Outcome = Failed`. Every other §25 row that creates a record
      follows the same rule in its own slice (§25 header). **Measurement:** the same carrier test asserts that the
      record's `EntityId` equals the stored new meeting's id, for a create over HTTP and for a follow-up (both
      records). Sabotage: let the handler keep the candidate's own new id instead of `NewMeetingId`, then the
      `EntityId` rows are red.
- [ ] S25-8 — **Follow-up ordering (review 01 F-2).** After `ScheduleFollowUpMeetingHandler` writes the carried
      lines, it re-sequences the whole agenda of the new meeting in one pass: carried lines first (K6, in their
      carry order), then mandatory lines (S18, once present), then template lines, numbered 0..n−1 with no duplicate
      `SortOrder`. Real-Mongo test: a follow-up of a meeting with two open actions, of a type with three template
      lines, gives the order carry, carry, template, template, template, with distinct `SortOrder` values. The same
      test runs through the series sweep (`ChainAsFollowUp = true`).

**Failure paths.** More than 50 lines or a line over 500 characters → 400, and nothing is written. A type with an
empty template → an empty list, and the form works as today. If the invite mail fails, the meeting and its agenda
stay (K12, existing). No `create` → no Create button (UAS-001).

**Tests.** Handler tests for `null`, `[]` and a list; write-order test (agenda before the invite); retry test;
follow-up and series template tests; JS tests (list editing, type-change confirm, no prefill parameter). Sabotage:
move the agenda write after `SendInviteAsync`, then the order test is red. Re-add the client prefill after create,
then the double-application test is red. Skip the re-sequencing pass, then the S25-8 test is red (duplicate
`SortOrder`). Snapshot the carrier in `GetAuditMetadata()`, then the S25-7 carrier measurement is red. **L10n:** 7 languages (list labels, confirm text, help). **Permissions:**
`platform.meetings.create` (unchanged). **Tenant isolation:** agenda rows take the meeting's tenant on the server
(`TenantRepository`). **Depends on:** S25 does not start until Q23 is answered; yes → the shared mirror gets
`["Meetings"] = 18` (written by this CT after the owner's explicit XMC-001 answer, reviewed by the library owner, per
card S-90 §7c); no → `PlatformConfiguration` is used and Q13 is reopened. S28 (S25-6 relies on S28-10). No other
slice: it uses today's `AgendaTemplate`, and S18 joins when it lands. **Estimate:** 1 prompt; with the follow-up re-sequencing it may need a
second. **Evidence:** E4.

### 24.29 S26 — Preparation tasks on the create form (never a block)

**Status: draft / not approved.** Owner "ekle-uc" (2026-10-27). ADR-003 §1 ("toplantıdan **önce** … görev açmak"),
§4, §5. R1-D13.

**Measured today (@826150ef4).**
- A preparation task can be raised only from the meeting detail page, after the meeting is saved, through
  `POST /{id}/tasks` (`MeetingsController.cs:213-215`: `platform.meetings.update` + `platform.tasks.create`).
- The bridge sets link type `preparation` when the task is created before `StartAt`, and `bornFromMeeting` otherwise
  (`MeetingTaskBridgeCommandHandlers.cs:242-244`, `RecordLinkTypes.Preparation` in `RecordLink.cs:100`).
- The due date is optional with no default (`task-from-meeting-dialog.js:44-79`).
- No meeting page shows preparation readiness.

**What the user gets.**
- On the create form, a "Preparation tasks" section with lines (title, assignee, due date). Each line becomes an
  ordinary MOD-0024 task, created right after the meeting and linked to it as preparation.
- The due date defaults to the meeting start. A later due date gets a warning, not a refusal.
- The meeting detail page shows "Preparation: x / y done".
- About 24 hours before the start, the organizer gets a reminder if preparation is still open.
- **Nothing about preparation ever blocks the meeting.**

**Files / areas.**
- `MeetingModels.cs`: `CreateMeetingRequest.PreparationTasks`, `MeetingDto.Preparation` (`Done`, `Total`,
  `NotVisible` per Q20), and a per-line result list on the create response.
- `MeetingCommandHandlers.cs` (create: one internal `CreateTaskFromMeetingCommand` per line, after the meeting rows
  and before the invite).
- `MeetingQueryHandlers.cs` (`GetMeetingById`: preparation counts through `IRecordLinkService` and the same
  `TaskRelatedRecordResolver` read rule as the linked-task list).
- New `BackgroundJobs/MeetingPreparationReminderJob.cs` and its args, on the existing Hangfire seam (with its own
  `EnabledJobs` key, the S11 pattern).
- New GR-named `Commands/SendDueMeetingPreparationRemindersCommand.cs` and its handler.
- `Infrastructure/Services/MeetingInviteMailer.cs` (a reminder send). One additive template key in
  `Infrastructure/Persistence/Configurations/NotificationTemplateSeed.cs`, in 7 languages.
- `Meeting.cs` (`PreparationReminderSentForStartAt`).
- `Views/Meetings/_Form.cshtml`, `form.js`, `Details.cshtml`; resx in 7 languages; the `MEETINGCREATE` and
  `MEETINGDETAIL` help.
- **No file in `Features/Tasks/**` changes, and `TaskItem` gets no field.**

**Acceptance criteria**
- [ ] S26-1 — The "Preparation tasks" section is rendered only for a creator who also holds `platform.tasks.create`
      (Q22), and only while Start is in the future. Each line has a title (required), an assignee (from the task
      assignment picker the detail dialog already uses), an optional description and a due date.
- [ ] S26-2 — After the meeting row exists, on **every** create call that returns the meeting (`isNew` true, **or**
      false when S28-10 returns the existing meeting to a caller who can view it; review 01 F-9, review 02 F-11), each
      line is sent **in-process** as the existing
      `CreateTaskFromMeetingCommand`. It goes through `CreateTaskItemCommand`, `TaskAssignmentIntentRules` and
      `ITaskAssignmentGuard` unchanged (K2), and is linked with `RecordLinkTypes.Preparation`. No second
      task-creation path and no new link type. The server checks `platform.tasks.create` itself, because an
      in-process send does not pass the controller's attributes (K9).
- [ ] S26-3 — **Per-line result, never all-or-nothing (K12).** The create response is still 201 for the meeting and
      lists each line as `created` (task id) or `failed` (reason code, for example the assignment-scope refusal or a
      missing permission). The form names the failed lines and offers to retry them from the detail page. A test
      with one in-scope and one out-of-scope assignee gives 1 created and 1 failed, and the meeting exists.
- [ ] S26-4 — **Idempotent on retry.** Each line carries a `ClientLineKey`. The bridge key is
      `ResolveForTaskBridge(meetingId, actor, ClientLineKey)`. Retrying the same create returns the same meeting and
      the same task ids, with no second task and no second link. Because the lines are sent even when `isNew` is false
      (`MeetingCommandHandlers.cs:151-155` and `:198-200` skip only the attendee rows and the invite), a request cut
      off after the meeting was written still gets its preparation tasks on the retry. This holds only for a caller
      S28-10 lets through: a retry by a caller who cannot view the existing meeting gets S28-10's fixed 409, and **no
      line is sent**. Test: the first call fails after the meeting insert, and the retry creates every task exactly
      once. A second test sends the same five key values as a non-viewer, and no task and no link are written.
- [ ] S26-5 — Due date: if it is left empty, it is the meeting **start**. A due date after the start is accepted and
      returns the warning `MEETING_PREPARATION_DUE_AFTER_START`, shown next to the line. This is never a 4xx.
- [ ] S26-6 — The detail page shows "Preparation: x / y done". It counts `preparation` links whose task is `Done` out
      of all `preparation` links, under the task read rule (Q20; BL-541). A task the reader may not read never shows
      its title, assignee or due date. With no preparation links, the line is not shown.
- [ ] S26-7 — **Binding rule R1-D13:** creating, holding, rescheduling, cancelling and publishing minutes are never
      refused or delayed because of preparation status. Tests try each one with open preparation tasks and assert
      success. Rescheduling does **not** change any preparation task's `DueAt` (test reads the task after a
      reschedule). Task owners are notified only through K7. If K7 is not implemented today (§24.27), the slice
      records the gap and does not build it.
- [ ] S26-8 — Reminder: `SendDueMeetingPreparationRemindersCommand` is run by `MeetingPreparationReminderJob`
      (hourly, tenant by tenant, like `MeetingSeriesSweepJob`). For each `Scheduled` meeting starting in the next 24
      hours that has at least one open preparation task, it claims
      `Meeting.PreparationReminderSentForStartAt = StartAt` under an expected-version write, **then** e-mails the
      organizer (Q21) once. A second run sends nothing, and a rescheduled meeting gets one new reminder. Confidential
      meetings follow S15-6: the subtype name, no title. The mail lists counts only, never task titles. **It does not
      run while background jobs are off (BL-536).** The detail page says so next to the preparation line.
- [ ] S26-9 — Help text in 7 languages explains preparation tasks, the never-a-block rule, and that the reminder
      needs background jobs.
- [ ] S26-10 — **A creator who could not view the meeting (review 02 F-16).** Each line goes through
      `CreateTaskFromMeetingCommand`, which S28-1 guards with the **creator** as the actor. If the creator is neither
      the organizer nor an attendee, every line would be refused with 404. No screen sends such a request. The create
      form always sends `organizerUserId: null` (`form.js:174`; `Views/Meetings/_Form.cshtml` has no organizer
      field), so the organizer is the creator (`MeetingCommandHandlers.cs:115`), and no R1 slice adds an organizer
      field. Only a direct API caller who names another organizer can send it. **Since review 03 (F-19) this is
      covered by S28-10's general up-front rule**, which S28 builds for every create from the create endpoint, with
      or without preparation lines. The handler applies the same `CanView` predicate to the meeting **about to be
      created**: the requested organizer, the requested attendees that pass the eligibility filter (`:178-181`), the
      creator's `read-all`, and, from S15, the chosen subtype's confidential flag. A creator who would not be able to
      view it is refused **before anything is written** with 400 `MEETING_CREATOR_CANNOT_VIEW`. The earlier
      S26-only code `MEETING_PREPARATION_CREATOR_CANNOT_VIEW` is dropped; it was never built. **S26 adds no form
      text for this rule** (review 04 F-25). The form cannot trigger the rule, so a hint there would be dead text in
      7 languages. The rule is defensive, and S26 tests it with preparation lines through the API.
      **Why this choice.** Creating the meeting and failing every line (K12 partial success) was rejected: the
      failure is certain before anything is written, and S26-3's recovery ("retry from the detail page") is not
      available to a creator who cannot open the meeting. "The organizer must be the creator" was rejected because
      the API accepts another organizer today (`:115`), and S28 narrows what the API accepts only by visibility.
      This refusal is input validation on the request, not preparation status, so R1-D13 ("never a block") is not
      affected. Test (a direct API request): an organizer-other, creator-not-attendee request with one line gives
      400, and no meeting, attendee, task or mail exists. The same request with the creator added as an attendee
      gives 201 and the task.

**Failure paths.**
- No `tasks.create` → no section, and a request that still carries lines gets each line refused with
  `MEETING_PREPARATION_TASK_FORBIDDEN`. The meeting is still created.
- Assignee out of scope → that line fails with MOD-0024's own reason code.
- Start in the past → the section is hidden. A request that still carries lines gets them refused with
  `MEETING_PREPARATION_START_PASSED`, because the bridge would otherwise link them as `bornFromMeeting`.
- Mail failure in the reminder → logged, and the claim stays, so no retry storm. Same posture as K12.
- Cross-tenant assignee → refused by the task guard (existing).
- A creator who could not view the meeting → 400 `MEETING_CREATOR_CANNOT_VIEW` before anything is written
  (S28-10's general rule; S26-10). Only a direct API request can reach it; the create form cannot.
- A retry by a caller who cannot view the existing meeting → S28-10's fixed 409, and no line is sent (S26-4).

**Tests.** The handler and HTTP per-line matrix with the **real** `ITaskAssignmentGuard`; the idempotency test; the
never-a-block tests (S26-7); the reminder claim test (two runs, one mail; reschedule, one more); the read-rule count
test. Sabotage: make one failed line abort the create, then S26-3 is red. Gate publish on open preparation, then
S26-7 is red. Drop the claim, then the double-reminder test is red. Skip S28-10's up-front creator check, then the
S26-10 test is red (a meeting is written and every line fails with 404). **L10n:** 7 languages (section, warnings,
reasons, reminder template, help). **Permissions:** `platform.meetings.create` + `platform.tasks.create` (no new
key). **Tenant isolation:** tasks and links are created in the caller's tenant through the existing commands; the
reminder job runs inside each tenant's `TenantScope`. **Audit:** `CreateTaskFromMeetingCommand` is called unchanged
(known debt L85). The reminder command uses path `a` (§25). **Depends on:** S25 (same create block and form section
order); S28 (the guarded bridge command, and S28-10's up-front creator rule and retry rule, which S26-10 and S26-4
rely on; review 02 F-16, review 03 F-19). **Estimate:** 2 prompts. **Evidence:** E5 (crosses into MOD-0024 task creation and runs a background job).

### 24.30 S27 — Required and optional attendees with agenda presenter and time box

**Status: draft / not approved.** Owner "ekle-uc" (2026-10-27). R1-D14.

**Measured today (@826150ef4).**
- `MeetingAttendee` has `MeetingId`, `UserId`, `InvitationResponse`, `AttendanceStatus` and `MailUndeliveredAt`. It
  has no role.
- `AgendaItem` has `MeetingId`, `Text`, `SortOrder`, `RecordLinkId` and `CarriedFromMeetingId`. It has no presenter
  and no duration.
- The `.ics` `ATTENDEE` line carries `CN`, `RSVP=TRUE` and `PARTSTAT` but no `ROLE` (`MeetingIcsBuilder.cs:98-102`).

**What the user gets.** Each invitee is marked required or optional on the create and edit forms and on the detail
page. Calendar invites say which one applies. Each agenda line can name a presenter (an attendee) and a time box in
minutes. The form warns when the time boxes add up to more than the meeting length.

**Files / areas.**
- `MeetingAttendee.cs` (`IsOptional`), `AgendaItem.cs` (`PresenterUserId`, `DurationMinutes`).
- `MeetingModels.cs`: `CreateMeetingRequest.OptionalAttendeeUserIds`, `AddMeetingAttendeesRequest` per-user
  `IsOptional`, agenda line shape with presenter and duration, the new `SetMeetingAttendeeRoleRequest`, and DTOs.
- The create and add-attendee handlers, `AgendaItemCommandHandlers.cs` (add and update).
- New GR-named `Commands/SetMeetingAttendeeRoleCommand.cs` with its handler and validator.
- `Services/MeetingIcsBuilder.cs` (`ROLE`).
- `MinutesCommandHandlers.cs`: the S16 rule per Q19.
- **Organizer reassignment (S27-8, review 06 F-36):** `MeetingCommandHandlers.cs` (`ReassignMeetingOrganizerHandler`
  gains `IMeetingAttendeeRepository` and `IPlatformTransactionExecutor`), `Commands/ReassignMeetingOrganizerCommand.cs`
  (moved out of the bundled file to add the audit interfaces), and session overloads of the attendee create and
  update in `Domain/Repositories/IMeetingRepositories.cs` and `Infrastructure/Persistence/Repositories/MeetingRepositories.cs`.
- API and proxy actions; `_Form.cshtml`, `form.js`, `Details.cshtml`; resx in 7 languages; help.
- The audit ledger (`AddMeetingAttendeesCommand` L57, `AddAgendaItemCommand` L55, `ReassignMeetingOrganizerCommand`
  L128).

**Acceptance criteria**
- [ ] S27-1 — `MeetingAttendee.IsOptional` (default `false`) is set from `OptionalAttendeeUserIds` on create, from
      the per-user flag on add, and through `PUT {id}/attendees/{userId}/role` (`platform.meetings.update`).
      Attendee rows written before S27 read as required. The organizer's row can never be optional (409
      `MEETING_ATTENDEE_ORGANIZER_REQUIRED`).
- [ ] S27-2 — Invite, change and cancel `.ics` carry `ROLE=REQ-PARTICIPANT` or `ROLE=OPT-PARTICIPANT` on every
      `ATTENDEE` line. The UID and SEQUENCE rules are unchanged (S5b). Golden-string test on the builder.
- [ ] S27-3 — (Owner answer Q19 = yes, 2026-10-27.) When S16's attendance-mandatory rule is active, publish and
      correct require a mark for every **required** attendee only, the organizer included. An optional attendee may
      be marked but does not have to be. Q5 (declined invitees are marked) applies to required attendees.
- [ ] S27-4 — `AgendaItem.PresenterUserId` must be an attendee of the same meeting (400
      `MEETING_AGENDA_PRESENTER_NOT_ATTENDEE`) when it is set on create, add or update. If the presenter is later
      removed as an attendee, the line keeps the value and the detail page labels it "no longer invited". It is not
      cleared silently, and `RemoveMeetingAttendeeCommand` is not changed.
- [ ] S27-5 — `AgendaItem.DurationMinutes` is 1..480 or null. On the create form and the detail agenda, when the sum
      of durations is larger than End − Start, a warning is shown with both numbers. The meeting is never refused.
- [ ] S27-6 — The list, detail page and minutes editor show "optional" next to optional attendees, and the presenter
      and minutes next to each agenda line. The calendar feed is unchanged. Visibility (D3, S15) does not depend on
      `IsOptional`.
- [ ] S27-7 — Audit: `AddMeetingAttendeesCommand`, `AddAgendaItemCommand` and `ReassignMeetingOrganizerCommand`
      (S27-8) move to path `a`, and `SetMeetingAttendeeRoleCommand` is new on path `a` (§25). `UpdateAgendaItemCommand`
      is already on path `a` from S18. The `AddAgendaItemCommand` row follows S15-7's names-only rule on a confidential
      meeting (pre-review 1 F-40): test through the real `AuditBehavior` that adding a line to a confidential meeting
      records `Text` as a name and not its text, with the sabotage of S15-7.
- [ ] S27-8 — **The organizer stays required after a reassignment (review 06 F-36).** Today
      `ReassignMeetingOrganizerHandler` changes only `Meeting.OrganizerUserId` and `PreviousOrganizerUserId`
      (`MeetingCommandHandlers.cs:407-408`) and touches no attendee row. From S27, after its existing checks, it writes
      in **one Platform transaction** (`IPlatformTransactionExecutor`, as `PublishMinutesHandler` does):
      - the meeting, under the request's `ExpectedVersion`, as today;
      - the new organizer's attendee row: if they have a non-deleted row, it becomes `IsOptional = false` and
        `InvitationResponse = Accepted`; if they have none, one row is created with `IsOptional = false` and
        `Accepted`. The unique index `(TenantId, MeetingId, UserId, IsDeleted)`
        (`PlatformSchemaManifest.Meetings.cs:95-101`) keeps it to one row.

      **Why `Accepted`, also over an earlier `Pending` or `Declined`:** the module counts an organizer `Accepted` in
      their own meeting (`MeetingCommandHandlers.cs:157-167`). A `Pending` row would put an invitation card for a
      meeting they organize on their Task Center and leave it out of their time planning, which counts only
      `Accepted` rows (`MeetingGatewayAdapter.cs:27-28`), the reason Q25's scheduler row is `Accepted` too; and the
      "added to your calendar" mail the reassignment already sends them (`:416-423`, BL-387) says the same.
      **The previous organizer's row** is not changed: it stays an ordinary required attendee row with its response
      (`Accepted` from the create), so they keep seeing the meeting as an attendee, and the next change or cancel
      mails them like any attendee (the comment at `:416-420`). Removing them is an ordinary
      `RemoveMeetingAttendee`.
      **No half state:** a stale `ExpectedVersion` gives the existing 409 and no row is written; no transaction
      available gives the existing 503 and nothing is written; the mail is sent only after the commit, as today.
      **Audit:** this changes what the command writes, so `ReassignMeetingOrganizerCommand` moves to path `a` here
      (R1-D12) and its ledger line L128 is removed in this slice. Its record carries `MeetingId` and
      `NewOrganizerUserId` (command fields) and, through S25's carrier, the previous organizer and the row action
      (`created`, `made-required-and-accepted` or `unchanged`).
      **Interim gap, stated:** S16 lands before S27 (§24.7). Until S27, a meeting reassigned to someone without an
      attendee row has no organizer row, so S16's attendance-mandatory rule (every row, S16-1) does not ask for the
      new organizer's mark. The owner may pull S27 forward to close it earlier.

      Tests (HTTP, real Mongo): reassign to an optional `Pending` attendee → that row is required and `Accepted`, and
      there is still one row for that user; reassign to a non-attendee → one new row, required and `Accepted`; the
      previous organizer's row is unchanged in both; a stale version → 409 and no attendee row change; on an
      attendance-mandatory type, publishing with the new organizer unmarked → 409
      `MEETING_MINUTES_ATTENDANCE_INCOMPLETE`, and with the mark → 200.

**Failure paths.** Making the organizer optional → 409. A presenter who is not an attendee → 400. A duration out of
range → 400. Without `update`, the role toggle is not rendered and the API answers 403. Changing the role of an
attendee not on the meeting → 404 `MEETING_ATTENDEE_NOT_FOUND` (existing code). Cross-tenant → 404. Concurrent edit
→ 409 (existing). A reassignment with a stale version or no Platform transaction → 409 or 503, and neither the
organizer nor any attendee row changes (S27-8).

**Tests.** Real-Mongo test that a pre-S27 attendee row reads as required; HTTP role change; the `.ics` golden
strings; the Q19 publish test (one optional attendee left unmarked, then publish succeeds); presenter validation; JS
sum-warning test; the S27-8 reassignment tests. Sabotage: drop `ROLE` from the builder, then the golden test is red.
Count optional attendees in the S16 check, then the Q19 test is red. Drop the organizer-row step from
`ReassignMeetingOrganizerHandler`, then the S27-8 rows are red (the optional row stays optional, no row is created,
and the publish with the new organizer unmarked succeeds). **L10n:** 7 languages. **Permissions:** `create` (on the form), `update`
(role change, agenda edits); no new key. **Tenant isolation:** unchanged (tenant-scoped repositories; attendee and
presenter checks are within the meeting). The new role write is covered by S28's guard (non-viewers get 404).
**Depends on:** S25 (agenda lines on the create form; the audit carrier S27-8 uses); S16 for S27-3; S18 (`UpdateAgendaItemCommand` already on path
`a`; review 01 F-5); S28 (write guard).
**Estimate:** 2 prompts (1–2 before fix round 5; S27-8 adds the reassignment rule and its audit move). **Evidence:**
E4.

### 24.31 S28 — Meeting access security (writes and BL-541)

**Status: draft / not approved.** From independent review 01, F-1 (ENGEL) and F-4. **First in the order.** R1-D15.

**Why first.** This is a security hole for **every** meeting today, not only confidential ones. A user who holds a
write key (for example `platform.meetings.update`) and knows a meeting's id can change a meeting they are not part
of, and can make themselves part of it. They can add themselves as an attendee or reassign the organizer to
themselves, and then read the whole meeting. The meetings side of BL-541 (work list A3, Dalga 1.1) is closed here as
well.

**Measured today (@826150ef4; confirmed by the CT in review 01).**
- `MeetingEligibility.CanView` is defined at `MeetingCommandHandlers.cs:63-64`. It is called **only** by query
  handlers (`MeetingQueryHandlers.cs:51-52, 245-246, 371-372`; `MeetingReportQueryHandlers.cs:39-53`). No command
  handler calls it.
- Write endpoints carry only `[HasPermission]` (`API/Controllers/MeetingsController.cs`).
- `AddMeetingAttendeesHandler` (`MeetingCommandHandlers.cs:429-503`) and `ReassignMeetingOrganizerHandler`
  (`:359-426`) check existence and editability only.
- `PublishMinutesHandler` returns the published content in its response (`MinutesCommandHandlers.cs:245-262, 306`).
- `ScheduleFollowUpMeetingHandler` reads the source without a visibility check
  (`MeetingFollowUpCommandHandlers.cs:57-62`), and copies its title by default (`:79`).
- BL-541 (`docs/roadmap/backlog/product-backlog.md:9857`):
  - the report's action rows carry the task title, assignee and due date without the task read rule
    (`MeetingReportQueryHandlers.cs:148-151`);
  - `MeetingRelatedRecordResolver.cs:28-31` gives a task reader the meeting title without the meeting's visibility
    rule.
- **Added in fix round 2 (review 02; read again at `274e417c4`, where the Meetings code is unchanged).**
  - The create key is organizer, type, start, end and title, with **no caller** (`MeetingIdempotencyKeyResolver.cs:9-22`).
    `CreateMeetingHandler` returns an existing meeting on a key match (`MeetingCommandHandlers.cs:151-152`), with its
    attendees, as a full `MeetingDto` and status 201 (`:194`, `:202-203`), whoever the caller is.
  - `CreateMeetingHandler` checks `FollowUpOfMeetingId` for existence only (`:123-131`): 404 or a write.
  - `ScheduleFollowUpMeetingHandler` takes whatever meeting `CreateMeetingCommand` returns (`MeetingFollowUpCommandHandlers.cs:92-100`),
    and then writes the follow-up link and carried agenda lines onto it (`:115-122`, `:233-248`). Its comment at
    `:102-108` names the key collision.
  - `ScheduleReviewMeetingForTaskHandler` sends `CreateMeetingCommand` with the caller as organizer
    (`MeetingTaskBridgeCommandHandlers.cs:457-468`).
  - Handlers check state right after the load: 409 in `UpdateMeetingHandler` (`MeetingCommandHandlers.cs:237-241`)
    and `AddMeetingAttendeesHandler` (`:460-465`); `SaveMinutesDraftHandler` answers a 400 that reveals the attendee
    list (`MinutesCommandHandlers.cs:139-144`); `RespondToInvitationHandler` answers 409 (`MeetingCommandHandlers.cs:603-607`)
    before it looks up the caller's attendee row (`:623-628`).
  - Not-found bodies differ by handler: `MEETING_NOT_FOUND` "The meeting does not exist." in most handlers
    (for example `MeetingCommandHandlers.cs:234`); `MEETING_FOLLOW_UP_NOT_FOUND` "This meeting could not be found."
    in the follow-up handler (`MeetingFollowUpCommandHandlers.cs:58-61`); `MEETING_NOT_FOUND` "This meeting could not
    be found." in the bridge (`MeetingTaskBridgeCommandHandlers.cs:137-141, 312-316`). `MeetingCommandHandlers.cs`
    holds seven handler classes and the static class `MeetingEligibility` (`:19, 67, 207, 295, 359, 429, 506, 579`;
    corrected in fix round 3, review 03 F-22).
  - The report builds `titleById` from **all** meetings (`MeetingReportQueryHandlers.cs:63`), adds continuation
    meetings the caller cannot see (`:122-133`), and prints the current-meeting title from it (`:148-151`). The
    export writes `CurrentMeetingTitle` and the task `Title` as they are (`MeetingReportExportSerializer.cs:45-53`).
    `TaskLinks.InaccessibleTitleCode` is `"TASK_NOT_ACCESSIBLE"` (`Features/Tasks/TaskLinks.cs:36`).
    `MeetingReportExportDisclaimer.cs:21-30` chooses text by locale from a 7-language map.
  - `UpdateMeetingSeriesHandler` rewrites the series organizer and attendees with no relation or visibility check
    (`MeetingSeriesCommandHandlers.cs:167-181`). The sweep chains from `LastGeneratedMeetingId`
    (`GenerateDueMeetingSeriesHandler.cs:147`, `:166-182`). `CreateMeetingSeriesCommand` carries the HTTP request
    model (`MeetingCommands.cs:80-81`; `MeetingModels.cs:473-486`).
- **Added in fix round 3 (review 03; the Meetings code is unchanged since `826150ef4`).**
  - A failed occurrence never moves `LastGeneratedMeetingId` (`GenerateDueMeetingSeriesHandler.cs:93-103`; the
    pointer is written only on success, `:107`), and every later occurrence chains from that pointer (`:147`). A
    source the series organizer cannot view would therefore stop the series for good, and there is no manual way
    out: attendees cannot be added to a cancelled or completed meeting (`MeetingCommandHandlers.cs:460-465`), and
    the organizer changes only on a scheduled one (`:389-398`).
  - `RespondToInvitationHandler` answers a caller without a row with 404 `MEETING_ATTENDEE_NOT_FOUND` "This meeting
    has no invitation for you." (`MeetingCommandHandlers.cs:623-628`). Two existing tests assert that code:
    `MeetingInvitationTests.cs:119-120` and `MeetingTwoUserFlowMongoTests.cs:119-120`. No existing test asserts 201
    on a create retry; the 201 assertions are on first calls (`MeetingInvitationTests.cs:308`,
    `MeetingTwoUserFlowMongoTests.cs:459`).
  - 14 test files under `tests/Diten.Platform.Application.Tests/Meetings/` build a handler that S28 guards by hand
    (about 40 `new …Handler(` constructions), for example `MeetingInvitationTests.cs:299`,
    `MeetingTwoUserFlowMongoTests.cs:447`, `MeetingHttpTests.cs:222` and `MeetingSeriesSweepEndToEndMongoTests.cs:208`
    for `CreateMeetingHandler` (review 03 cites the same constructions one to three lines further down).
  - The report's `CurrentMeetingId` is a non-nullable `Guid` (`MeetingReportModels.cs:112`), filled at
    `MeetingReportQueryHandlers.cs:151`, and the export writes it as a column (`MeetingReportExportSerializer.cs:52`).
    The report screen shows only the current-meeting title (`Report/index.js:124`).
  - The follow-up dialog sends `organizerUserId: null` and `attendeeUserIds: null` (`follow-up-dialog.js:91-93`), so
    a follow-up scheduled by a source attendee who is not the organizer has only the source organizer as attendee.
  - The create form reads `ok`, `reasonCode` and, on success, `data.id` from the create call (`form.js:179-194`);
    `form.js:103` is the edit page's load call, not the create call.
  - No project in the repository references `Microsoft.CodeAnalysis` today (searched in every `*.csproj`).

**What the user gets.** Nobody can change, take over or read a meeting through a write action unless they could
already see it. The report and its export stop showing details of tasks the reader may not read. The Task Center
stops showing the titles of meetings the reader may not see.

**Files / areas.**
- New `Features/Meetings/Services/MeetingWriteAccess.cs`: one helper with one public type. It loads the meeting and
  its attendee ids and applies `CanView` with the caller and `read-all` (or, under the system-origin marker, with
  the marker's acting identity and no `read-all`, S28-4), and with the confidential input once S15 lands. It answers
  "visible" with the loaded meeting, or "not visible"; it never builds a response itself, so each handler answers
  with its own not-found body (S28-1).
- New system-origin marker type (one public type in its own file under `Features/Meetings/Services/`), carried as a
  **command** parameter, never on a request model (S28-4).
- Every write handler listed in S28-1 and S28-10: `MeetingCommandHandlers.cs`, `AgendaItemCommandHandlers.cs`,
  `MinutesCommandHandlers.cs`, `MeetingFollowUpCommandHandlers.cs`, `MeetingTaskBridgeCommandHandlers.cs`.
- `Commands/MeetingCommands.cs`: the marker parameter on `CreateMeetingCommand` and `ScheduleFollowUpMeetingCommand`,
  and the `RequireCreatorCanView` and `PreAcceptedUserId` parameters on `CreateMeetingCommand` (S28-10; the second
  from owner answer Q25). All are optional trailing **command** parameters, never on an HTTP request model, so no
  existing caller changes and no client can set them.
- `GenerateDueMeetingSeriesHandler.cs`: creates the system-origin marker with the series organizer, checks the chain
  source through `MeetingWriteAccess` and falls back to an unchained create (S28-4, S28-11).
- `Handlers/QueryHandlers/MeetingReportQueryHandlers.cs`, `MeetingReportModels.cs` (the `MEETING_NOT_ACCESSIBLE`
  constant, and `CurrentMeetingId` becomes `Guid?`), `Services/MeetingReportExportSerializer.cs`, and a new
  `Services/MeetingReportExportPlaceholders.cs` (S28-6).
- `MeetingModels.cs`: the reason codes `MEETING_CREATE_CONFLICT`, `MEETING_CREATOR_CANNOT_VIEW` and
  `MEETING_FOLLOW_UP_CONFLICT` (S28-10).
- `Diten.Platform.Application.Tests.csproj`: one test-only package reference, `Microsoft.CodeAnalysis.CSharp`, for
  the S28-2 scan (new in the repository; the version is pinned in the slice like the other references).
- Existing tests that change (review 03 F-20): `MeetingInvitationTests.cs:119-120` and
  `MeetingTwoUserFlowMongoTests.cs:119-120` now expect 404 `MEETING_NOT_FOUND` (S28-1), and every hand-built
  construction of a guarded handler changes with that handler's constructor, which gains `MeetingWriteAccess` (the
  14 files measured above, including the hand-built dispatchers in `MeetingHttpTests.cs:222-236` and
  `MeetingSeriesSweepEndToEndMongoTests.cs:206-218`).
- **The sweep unit test also changes (review 04 F-27):** `GenerateDueMeetingSeriesHandlerTests.cs`. Its `Build`
  helper constructs `GenerateDueMeetingSeriesHandler` by hand with three arguments (`:47`). The handler gains
  `MeetingWriteAccess` (S28-11), so `Build` also creates one over the existing `FakeMeetingRepository` and
  `FakeMeetingAttendeeRepository` (`MeetingS2TestDoubles.cs:20, 91`). Its chain test (`:71-88`, assertions at
  `:85-87`) passes a random source id that is in no repository. Under S28-11 that source is "not visible", so the
  sweep would send `CreateMeetingCommand`, and the test's expectations (`CreateMeetingWasSent` false,
  `ScheduleFollowUpWasSent` true) would fail; its `RecordingMediator` answers only those two commands
  (`:230-264`). The slice seeds that source with the series organizer as its organizer, so the test keeps its
  meaning, and adds two unit rows:
  - **source visible** → `ScheduleFollowUpMeetingCommand` is sent with that source, and `CreateMeetingCommand` is
    not;
  - **source not visible** (another organizer, and no attendee row for the series organizer) →
    `CreateMeetingCommand` is sent with `FollowUpOfMeetingId` null and the series' own values,
    `ScheduleFollowUpMeetingCommand` is not sent, `MeetingsGenerated` is 1, `Failed` is 0, and
    `LastGeneratedMeetingId` is the new meeting.

  The plain-create test with `ChainAsFollowUp` false (`:90-102`) does not change: nothing is chained, so no source
  is checked.
- `RecordLinks/MeetingRelatedRecordResolver.cs` (S28-7).
- `wwwroot/assets/js/Meetings/Report/index.js` and its l10n; the Report resx set in 7 languages (the masked task
  title and the masked current-meeting title).
- The frontend reason-code bridge and its 7-language text for `MEETING_CREATE_CONFLICT`,
  `MEETING_CREATOR_CANNOT_VIEW` and `MEETING_FOLLOW_UP_CONFLICT` (`tests/meetings-reason-code-bridge.test.js` stays
  green).
- Tests under `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**`.
- **No file in `Features/Tasks/**` changes.** MOD-0024 is used only through `ITaskReadAccessPolicy`, which is already
  consumed by `TaskRelatedRecordResolver` and `LinkExistingTaskHandler` (§24.6).

**Acceptance criteria**
- [ ] S28-1 — These write handlers call `MeetingWriteAccess` **immediately after they load the meeting and before any
      other check** (review 02 F-14): before the 409 state checks (`CheckEditable`, for example
      `MeetingCommandHandlers.cs:237-241` and `:460-465`), before any 400 that reads the meeting's data (the attendee
      check at `MinutesCommandHandlers.cs:139-144`), and before any write or outward call (mail, task creation).
      Pipeline validators that read only the request are not affected. A caller who cannot view the meeting gets
      **that handler's own not-found response**, exactly as for an id that does not exist: the same status, reason
      code and message. For example, `ScheduleFollowUpMeeting` answers 404 `MEETING_FOLLOW_UP_NOT_FOUND` "This meeting
      could not be found." (`MeetingFollowUpCommandHandlers.cs:58-61`), the bridge answers 404 `MEETING_NOT_FOUND`
      "This meeting could not be found." (`MeetingTaskBridgeCommandHandlers.cs:137-141, 312-316`), and the others
      answer 404 `MEETING_NOT_FOUND` "The meeting does not exist.". No handler switches to a shared body, so no body
      tells "hidden" from "missing" (measured per handler in S28-8). The handler classes are (named by class, as the
      S28-2 scan reads them; review 03 F-22):
      - `UpdateMeetingHandler`, `CancelMeetingHandler`, `ReassignMeetingOrganizerHandler`;
      - `AddMeetingAttendeesHandler`, `RemoveMeetingAttendeeHandler`;
      - `AddAgendaItemHandler`, `UpdateAgendaItemHandler`, `DeleteAgendaItemHandler`, `ReorderAgendaHandler`;
      - `SaveMinutesDraftHandler`, `PublishMinutesHandler`, `CorrectPublishedMinutesHandler`;
      - `ScheduleFollowUpMeetingHandler` (on the source meeting);
      - `CreateTaskFromMeetingHandler`, `LinkExistingTaskHandler`;
      - later, S27's `SetMeetingAttendeeRoleHandler`.

      Covered by a special rule, not by the list above (each class still calls `MeetingWriteAccess` in its own body):
      - `CreateMeetingHandler`: its `FollowUpOfMeetingId` and its replay branch (S28-10);
      - `ScheduleFollowUpMeetingHandler`: also the meeting that `CreateMeetingCommand` returns to it, and the target
        of its own idempotency link (S28-10; review 06 F-35);
      - `CreateMeetingSeriesHandler`: the meeting it adopts, from S23 (S23-8, S28-11);
      - `GenerateDueMeetingSeriesHandler`: the chain source, checked with the acting identity of its system-origin
        marker (S28-4, S28-11).

      Not covered, each named by class with its reason. The S28-2 exception list is exactly the five classes marked
      "in scan scope" below:
      - `RespondToInvitationHandler` (in scan scope, `MeetingId`) has a rule **narrower** than `CanView`: only a
        caller with their own attendee row may answer. The organizer has an Accepted row, and a `read-all`
        non-attendee has no invitation to answer, so `MeetingWriteAccess` would admit callers this handler must
        refuse anyway. It keeps its own check, but S28 moves the attendee-row lookup
        (`MeetingCommandHandlers.cs:623-628`) to **immediately after the load**, before the 409 state check
        (`:603-607`) and the 400 response check (`:616-620`). A caller without a row gets the handler's
        missing-meeting body (404 `MEETING_NOT_FOUND` "The meeting does not exist.", `:598`) instead of today's 404
        `MEETING_ATTENDEE_NOT_FOUND` "This meeting has no invitation for you.", so they never learn that the meeting
        exists, or that it is cancelled or completed. **Consequences (review 03 F-20):** the two existing tests that
        assert the old code, `MeetingInvitationTests.cs:119-120` and `MeetingTwoUserFlowMongoTests.cs:119-120`, are
        changed in this slice to expect 404 `MEETING_NOT_FOUND`; the Meetings page then shows `errorMeetingNotFound`
        instead of `errorAttendeeNotFound` for such a caller (`api.js:46, 57`); the Task Center does not read either
        code (measured by review 03), so its invitation action is not affected.
      - `UpdateMeetingSeriesHandler` and `DeleteMeetingSeriesHandler` (in scan scope, `Id`): the `Id` is a series
        id, and the rule acts on a series, not on a meeting record. The sweep's writes onto meetings go through
        `CreateMeetingCommand` and `ScheduleFollowUpMeetingCommand`, which apply `CanView` with the series
        organizer (S28-11). The rule S28 applies to `UpdateMeetingSeriesHandler` is stated in S28-11.
      - `UpdateMeetingTypeHandler` and `DeleteMeetingTypeHandler` (in scan scope, `Id`; `MeetingTypeCommands.cs:9,
        12`): the `Id` is a meeting type id. A type is tenant configuration under `types-manage` and has no
        organizer or attendees, so `CanView` has nothing to decide. `DeleteMeetingTypeHandler` only asks whether any
        meeting uses the type (`MeetingTypeCommandHandlers.cs:115`) and answers 409 without naming one. **From S15,
        `UpdateMeetingTypeHandler` does write meeting rows (review 06 F-34):** the type stamp sets
        `IsConfidential = true` and moves `Version` on every meeting of the type (S15-11). It stays on the exception
        list because the stamp only raises protection, reads and returns no meeting content, and must reach **every**
        meeting of the type: a per-meeting `CanView` would leave the meetings the administrator cannot see exposed,
        which is the opposite of the rule's purpose. The `types-manage` key remains its gate.
      - Outside the scan scope (no member with one of the S28-2 names), listed for the reader:
        `ScheduleReviewMeetingForTaskHandler` and `CreateMeetingTypeHandler`. `ScheduleReviewMeetingForTaskHandler`
        creates a meeting with the caller as organizer (`MeetingTaskBridgeCommandHandlers.cs:457-468`) through
        `CreateMeetingCommand`, so S28-10 applies inside that call: the up-front creator rule always passes (the
        caller is the requested organizer), and the replay branch returns the existing meeting only if the caller
        can view it **now**. The key holds the organizer of the create and is not recomputed when the organizer is
        reassigned (`MeetingCommandHandlers.cs:133-134`, `:407-408`), so a replay can land on a meeting the caller
        no longer organizes; that caller gets the fixed 409 unless they can still view it (sentence corrected in fix
        round 3, review 03 F-22). Its own access rule is the task write gate (`:404-428`).
- [ ] S28-2 — Source-scan guard, **class level** (review 02 F-14): one file can hold several handlers
      (`MeetingCommandHandlers.cs` holds seven handler classes and the static `MeetingEligibility`), so the scan works
      per handler class, each class that implements `IRequestHandler<TCommand, …>` under
      `Features/Meetings/Handlers/CommandHandlers/`, not per file. A class is in scope when its command, or the
      request model the command carries, has a member named `Id`, `MeetingId`, `SourceMeetingId`,
      `CreatedFromMeetingId` or `FollowUpOfMeetingId`. Each class in scope calls `MeetingWriteAccess` inside its own
      body, or is named by class on the reasoned exception list in S28-1 (exactly `RespondToInvitationHandler`,
      `UpdateMeetingSeriesHandler`, `DeleteMeetingSeriesHandler`, `UpdateMeetingTypeHandler` and
      `DeleteMeetingTypeHandler`). A new handler cannot skip the check unseen.
      **How the scan works (review 03 F-22).** Text matching is not used, because one file holds several classes
      and a base list can span lines (`GenerateDueMeetingSeriesHandler.cs:27-28`). The guard:
      1. finds the handler classes and their command types by **reflection** on the Application assembly (every
         non-abstract type in the `…Features.Meetings.Handlers.CommandHandlers` namespace that implements
         `IRequestHandler<,>`), and decides scope from the command type's members and the members of every
         parameter type of its primary constructor (the carried request model);
      2. parses every `.cs` file in that folder with the **Roslyn syntax tree** (`CSharpSyntaxTree.ParseText`), takes
         the `ClassDeclarationSyntax` with the handler's name, and passes only if that class's own members contain an
         invocation whose target is a field, parameter or property of type `MeetingWriteAccess` (an identifier or
         member access, resolved by the declared type of that field or constructor parameter in the same class).
         A call in another class of the same file does not count;
      3. fails when a class in scope neither passes step 2 nor is on the exception list, and also when an exception
         list entry names a class that does not exist or is not in scope, so the list cannot go stale.
      This needs the test-only package `Microsoft.CodeAnalysis.CSharp` (Files). Sabotage (Tests): removing the call
      from `AddMeetingAttendeesHandler` only turns the guard red even though `UpdateMeetingHandler`, in the same file,
      still calls it.
- [ ] S28-3 — Follow-up: the source must be viewable (S28-1). **Binding rule, built in S15-10 once the field exists:**
      a confidential source passes its effective confidentiality to the follow-up, and its title is not copied.
- [ ] S28-4 — **No bypass** (review 02 F-13). The sweep has no user, so `GenerateDueMeetingSeriesHandler` creates an
      explicit system-origin marker that names an **acting identity**: the series organizer
      (`MeetingSeries.OrganizerUserId` at the time of the run). It passes the marker on its in-process
      `CreateMeetingCommand` and `ScheduleFollowUpMeetingCommand`, and `ScheduleFollowUpMeetingHandler` forwards the
      same marker unchanged to the `CreateMeetingCommand` it sends. The sweep also uses the marker for its own check
      of the chain source (S28-11). Under the marker, `MeetingWriteAccess` applies
      `CanView` with the acting identity as organizer or attendee only; `read-all` is not looked up for a job. The
      marker is a command parameter, never on an HTTP request model, and never inferred from an empty user id. A
      source-scan test asserts that exactly one file creates the marker (forwarding a received marker is not creating
      one).
- [ ] S28-5 — The existing permission keys still apply first. A caller without the key gets 403 from the attribute,
      as today, which says nothing about the meeting. `IsPlatformActor` keeps counting as `read-all` (unchanged; S15
      narrows it for confidential meetings, Q3).
- [ ] S28-6 — **BL-541, report and export.**
      - **Tasks.** For action-register rows whose task the caller may not read, decided in one batched
        `ITaskReadAccessPolicy.ReadableTaskIdsAsync` call: the title becomes `TaskLinks.InaccessibleTitleCode`;
        `AssigneeUserId` and `DueAt` become null; lifecycle and the overdue flag stay, so the tiles stay true.
      - **Current meeting (review 02 F-12).** An action row's current-meeting title is shown only when `CanView` passes
        for the reader on that meeting. This is decided with the same predicate and the meetings and attendees the
        report already loads (`MeetingReportQueryHandlers.cs:44-53`), and the extra batched read at `:122-133` goes
        through it too. Otherwise `CurrentMeetingTitle` becomes the placeholder code `MEETING_NOT_ACCESSIBLE` (a
        constant in `MeetingReportModels.cs`), and **`CurrentMeetingId` becomes null** in the response and in the
        export (review 03 F-21; the field becomes `Guid?`, `MeetingReportModels.cs:112`), by the same rule that
        empties a masked task's assignee and due date: the reader learns that some current meeting exists, never
        which one. The screen shows only the title today (`Report/index.js:124`) and renders no link from a null id.
        A placeholder is used rather than an empty string, because an empty cell would read as "no current meeting",
        which is false. The row itself belongs to a meeting the reader can view. The origin title already comes from
        the visible set and is unchanged.
      - **Export (review 02 F-15).** Export rows equal screen rows (BL-346 rule): the same rows and the same masked
        cells. The screen renders both codes in 7 languages from the Report resx. The export writes them as text
        through one new `Services/MeetingReportExportPlaceholders.cs`, which follows the
        `MeetingReportExportDisclaimer.cs:21-30` pattern: a 7-language map chosen by the export's `Locale`, with an
        English fallback, in one place. A raw code never reaches a CSV or JSON file.

      Tests: a `read-all` holder who cannot read the task sees the row masked on screen and in the export. A reader
      who cannot view an action's current meeting sees the placeholder, not its title, and a null `CurrentMeetingId`,
      in the response and in the export: the CSV `CurrentMeetingId` cell is empty and the JSON value is `null` (review
      03 F-21). A reader who can view it still gets the id (the existing `MeetingReportMongoTests.cs:227` stays green).
      The export in each of the 7 locales carries that locale's text for both placeholders and no raw code.
- [ ] S28-7 — **BL-541, the task side's related record.** `MeetingRelatedRecordResolver` gets the caller context and
      **drops** every meeting the caller cannot view (`CanView`), so neither title nor route is shown (owner answer
      Q24 = drop, "duzelt-2"). The test reads the Task Center projection as a task assignee who is not part of the
      linked meeting.
- [ ] S28-8 — The write matrix: {organizer, attendee, `read-all` non-participant, non-participant holding the write
      key, platform actor, other tenant} × every S28-1 handler. Expected answers: the allowed callers succeed as their
      keys allow; the non-participant without `read-all` gets that handler's 404, and the data is **unchanged**
      (asserted in the database: no attendee row added, no organizer change, no minutes version, no task, no mail
      dispatch); the other tenant gets 404. **Where the rows run (review 03 F-22):** every handler row runs over HTTP
      (TestHost) with real Mongo. The **series rows** run at **handler level with real Mongo**, in the shape of
      `MeetingSeriesSweepEndToEndMongoTests`, because the sweep has no HTTP endpoint: it runs from the job
      (`MeetingSeriesSweepJob.cs:81`) through `GenerateDueMeetingSeriesHandler`. **Existing tests that change (review
      03 F-20):** `MeetingInvitationTests.cs:119-120` and `MeetingTwoUserFlowMongoTests.cs:119-120` now expect 404
      `MEETING_NOT_FOUND` for a caller without an attendee row (S28-1); every hand-built construction of a guarded
      handler follows its new constructor, and the sweep unit test `GenerateDueMeetingSeriesHandlerTests.cs` changes
      and gains two rows as Files states (review 04 F-27). **Rows added in fix round 2 (review 02):**
      - every S28-1 handler on a **cancelled** and on a **completed** meeting: a non-viewer gets 404, never the 409
        state answer;
      - `SaveMinutesDraft` naming a non-attendee in `Attendance`: a non-viewer gets 404, never the 400 that reveals
        the attendee list;
      - **body equality**, for every handler: the body for an existing meeting the caller cannot view is
        byte-identical to that handler's body for an id that does not exist. Both requests carry the same correlation
        id, and the test pins and names any other per-request value in the envelope;
      - `RespondToInvitation`: a caller without an attendee row, on a cancelled meeting, gets the missing-meeting
        body, never 409;
      - **create replay** (S28-10): a non-viewer holding `create` sends the five key values of an existing meeting
        and gets 409 with the fixed body. The body is compared with the fixed bytes, so it carries no id, title,
        description, location, attendee, status or cancellation reason. No attendee row, agenda line, task, link or
        mail dispatch is written. The organizer sending the same gets 200 and the meeting;
      - **`FollowUpOfMeetingId`** (S28-10): a create that names a meeting the caller cannot view gets 404,
        byte-identical to an id that does not exist, and nothing is written.

      **Rows added or changed in fix round 3 (review 03):**
      - **creator rule** (S28-10, F-19), over HTTP, sent directly to the API (no screen sends this request; review
        04 F-25): a creator who names another organizer and is not among the attendees (no `read-all`) gets 400
        `MEETING_CREATOR_CANNOT_VIEW`, and no meeting, attendee row or mail dispatch exists. The same request with
        the creator added as an attendee gives 201, and its retry gives 200 with the same id (K11). A `read-all`
        creator who is not on the meeting gets 201. A create with `organizerUserId: null`, as the create form sends
        it (`form.js:174`), always passes. A source attendee who is not the organizer schedules a follow-up through
        the follow-up endpoint with no attendees and gets 201 (the rule is not applied there; the Q25 rows below
        check the scheduler's attendee row);
      - **follow-up collision** (S28-10, F-24), over HTTP, a caller who can view the source, a follow-up whose five
        values match an existing meeting:
        - the caller **cannot view** the existing meeting: 409 with the fixed `MEETING_CREATE_CONFLICT` body. On the
          existing meeting, no follow-up link, no carried agenda line and no agenda link is written, and the source's
          links are unchanged;
        - the caller **can view** it and its `FollowUpOfMeetingId` is **not** the source: 409
          `MEETING_FOLLOW_UP_CONFLICT`, whose `data` carries only the existing meeting's id. Nothing is written;
        - the caller **can view** it, its `FollowUpOfMeetingId` **is** the source and no follow-up link exists yet
          (a first call cut off after the meeting insert; the test injects the fault after `CreateMeetingCommand`):
          the retry answers 200 with that id, writes the follow-up link once and carries each open line once. A
          second retry writes nothing more and answers 200 with the same id and count;
      - **series** (S28-11, F-18), handler level with real Mongo:
        - **attack:** a `series-manage` holder who cannot view the series' last generated meeting rewrites the series
          organizer to themself and runs the sweep. The occurrence is created as a plain **unchained** meeting: its
          `FollowUpOfMeetingId` is null, there is no follow-up link from the source, no agenda line with
          `CarriedFromMeetingId` = the source and no agenda link to any task of the source. The source's agenda and
          links are unchanged. `LastGeneratedMeetingId` is the new meeting, and one `meetings.series.chain_broken`
          warning is logged (asserted through a test logger);
        - **legitimate hand-over:** the series organizer is changed to a colleague who is neither organizer nor
          attendee of the last generated meeting. The first sweep creates an unchained meeting and logs the warning;
          the sweep for the next occurrence chains from that new meeting (`FollowUpOfMeetingId` = it, follow-up link
          written). Both occurrences count `generated`, none `failed`;
        - **true organizer:** the same sweep with the organizer of the last meeting chains as today;
        - **collision** (behaviour change, F-24): a manually created meeting has the occurrence's five values and
          `FollowUpOfMeetingId` null. The occurrence counts `failed`, nothing is written onto the manual meeting, the
          pointer stays, and the sweep for the next occurrence chains from the same source again.

      **Rows added in fix round 4 (review 04 and Q25):**
      - **completion on a cancelled or completed meeting** (S28-10, F-26), over HTTP. The setup is the completion
        row above, but the existing follow-up of the source is cancelled (and, in a second row, completed) before
        the retry. The answer is 409 `MEETING_CANCELLED` (in the second row, 409 `MEETING_COMPLETED`). No follow-up
        link, no carried agenda line and no agenda link is written onto that meeting, and the source's links are
        unchanged;
      - **scheduler as attendee** (S28-10, Q25), over HTTP with real Mongo:
        - a source attendee who is not the organizer schedules a follow-up through the follow-up endpoint with no
          attendees: 201. The new meeting has exactly two attendee rows, the source organizer's and the
          scheduler's, both `Accepted`. The scheduler's `GET /{id}` of it answers 200. No invite dispatch is
          addressed to the scheduler, and the scheduler gets **no Task Center invite item** for it (the
          `MeetingWorkItemProviderTests.cs` shape: the provider returns nothing for that meeting);
        - the source organizer schedules the same: one attendee row (the organizer's), as today;
        - a `read-all` holder who is **not a party** of the source (neither its organizer nor an attendee) schedules
          a follow-up: 201, and **no** row is added for them; the new meeting has only the source organizer's row;
        - a request that already names the scheduler in `attendeeUserIds`: exactly one scheduler row, `Accepted`;
        - a retry with the same idempotency key: 200 with the same id from the follow-up's own link (`:68-77`), and
          no second scheduler row;
      - **scheduler under the marker** (S28-10, Q25), handler level with a recording mediator: a
        `ScheduleFollowUpMeetingCommand` sent with the system-origin marker forwards exactly the request's
        `AttendeeUserIds`, with no caller id and no empty id, and no `PreAcceptedUserId`. The series "true
        organizer" row above also asserts that the chained occurrence has no attendee row beyond the organizer and
        the series attendees;
      - **pre-accepted only for that user** (S28-10, Q25), handler level: a `CreateMeetingCommand` with
        `PreAcceptedUserId` = U and attendees U and V writes U `Accepted` and V `Pending`; with `PreAcceptedUserId`
        null, every non-organizer row is `Pending`, as today.

      **Rows added in fix round 5 (reviews 05 and 06):**
      - **non-party creator rule on the follow-up path** (S28-10, review 05 F-29), handler level with a recording
        mediator: a scheduler who is not a party of the source (passed through `read-all`) makes the handler send
        `CreateMeetingCommand` with `RequireCreatorCanView: true`; a party (the source organizer, and a source
        attendee) makes it send `false`; under the system-origin marker it sends `false`. The HTTP refusal for a
        confidential subtype runs in S15 (S15-10), because S28 has no confidential type yet;
      - **idempotency-link target** (S28-10, review 06 F-35), over HTTP: a source attendee schedules a follow-up
        (201; added `Accepted`, Q25), and the new meeting's organizer then removes them from it. They stay an attendee
        of the source, so the source check passes. Their retry with the same idempotency key gets 409 with the fixed
        `MEETING_CREATE_CONFLICT` bytes, with no meeting id and no carried line count, and nothing is written. The same
        retry while they are still an attendee of the target answers 200 with the id and count, as today;
      - **party rule, organizer branch** (S28-10, review 05 F-32 a), over HTTP: the source's organizer has no
        attendee row on it (the test removes that row in the database: before S27-8 a reassigned organizer can have
        none) and schedules a follow-up that names another organizer. The new meeting has the named organizer's row
        and an `Accepted` row for the scheduler;
      - **non-party who lists themself** (S28-10, review 05 F-32 b), over HTTP: a `read-all` holder who is not a party
        of the source schedules a follow-up whose `attendeeUserIds` names themself. Their row is written `Pending`
        (no `PreAcceptedUserId`), and they get a Task Center invitation item for it. **Accepted as stated:** it is the
        answer a creator who lists themself on `POST /` gets today, no screen sends this request (the dialog sends no
        attendees, `follow-up-dialog.js:91-93`), and making it `Accepted` would widen the Q25 rule beyond a party of
        the source;
      - **"link exists" answers 200 whatever the lifecycle** (S28-10, review 05 F-32 c), over HTTP: a follow-up of the
        source whose follow-up link was written by another call is cancelled (in a second row, completed); a request
        with another idempotency key and the same five values lands on it through the create replay and answers 200
        with its id and count, and nothing is written;
      - **a cancelled meeting that is not a follow-up of the source** (review 05 F-32 c), over HTTP: the replay lands
        on a viewable cancelled meeting whose `FollowUpOfMeetingId` is null; the answer is 409
        `MEETING_FOLLOW_UP_CONFLICT` with only its id, never `MEETING_CANCELLED`;
      - **party outside the eligibility filter** (S28-10, review 05 F-32 d), over HTTP with the eligibility query
        answering without the scheduler: a source attendee schedules a follow-up; 201, the follow-up exists with its
        link and carried lines, and no attendee row is written for the scheduler.
- [ ] S28-9 — The work list rows A3 and Dalga 1.1 (BL-541) are closed **for the meetings side**. Updating the BL-541
      backlog row is the CT's record step and is not part of this pack.
- [ ] S28-10 — **Meetings reached through the create path (review 02 F-11; F-14 item 4; review 03 F-19, F-24).**
      - **Creator rule (review 03 F-19).** After the organizer eligibility check and the `FollowUpOfMeetingId` check,
        and **before** `FindOrCreateAsync`, `CreateMeetingHandler` applies the `CanView` predicate to the meeting
        **about to be created**, with the creator as caller: the requested organizer, the requested attendees that
        pass the eligibility filter (`MeetingCommandHandlers.cs:178-181`), the creator's `read-all` (a platform actor
        counts, S28-5), and from S15 the chosen subtype's confidential flag. A creator who would not be able to view
        it is refused with **400 `MEETING_CREATOR_CANNOT_VIEW`** and a localized message ("You would not be able to
        see this meeting, so it was not created. Name yourself as organizer or attendee." in S28; from S15, which adds
        confidential types, it ends "…, or choose a type that is not confidential.", S15-10, pre-review 1 F-45), and
        nothing is written. This replaces S26-10's own code.
        - **Where it applies.** Every `CreateMeetingCommand` sent for a user: the create endpoint (with or without
          Repeat or preparation lines) and `ScheduleReviewMeetingForTaskHandler` (where it always passes, because
          the caller is the requested organizer). Under the system-origin marker the acting identity is the series
          organizer, who is the requested organizer, so the sweep always passes. On the `CreateMeetingCommand` that
          `ScheduleFollowUpMeetingHandler` sends, the handler sets the optional trailing parameter
          `RequireCreatorCanView` (default `true`) itself: **`false`** when the caller is a **party** of the source
          (its organizer or a user with an attendee row on it) and under the system-origin marker, and **`true`**
          when the caller is not a party (they passed the source check only through `read-all`; review 05 F-29). The
          follow-up dialog sends no attendees (`follow-up-dialog.js:91-93`). A party is instead added as an
          `Accepted` attendee (owner answer Q25, the "Scheduler as attendee" bullet below), so they can open the new
          meeting when eligible, and the rule stays off for them so that no follow-up that works today is refused.
          For a non-party the rule passes through `read-all` whenever the new meeting is not confidential, so
          nothing changes for them today; from S15, when the request names a confidential subtype, `read-all` does
          not count, and the request is refused with 400 `MEETING_CREATOR_CANNOT_VIEW` before anything is written
          unless the caller names themself as organizer or attendee (S15-10). Without this, such a caller would get a
          confidential meeting written that they cannot open, a dialog that sends them to its page
          (`follow-up-dialog.js:106`), and a fixed 409 on retry: the case Q25 closes for a party. That path's retries
          are answered by its own idempotency link (`MeetingFollowUpCommandHandlers.cs:68-77`) before
          `CreateMeetingCommand` is reached. A source-scan test asserts that exactly one file,
          `MeetingFollowUpCommandHandlers.cs`, passes `RequireCreatorCanView` at all.
        - **Why this choice, rather than accepting the 409.** The rule is **defensive** (review 04 F-25). No screen
          can trigger it today, and from S15 the follow-up dialog does not offer a confidential subtype to a
          scheduler who is not a party of the source (S15-10, pre-review 1 F-45), so the only screen path left is a
          type made confidential while that dialog was open, which gets the S15 text above. The create form always
          sends `organizerUserId: null` (`form.js:174`;
          `Views/Meetings/_Form.cshtml` has no organizer field), so the organizer is the caller
          (`MeetingCommandHandlers.cs:115`), and the Task Center's review meeting also names the caller as organizer
          (`MeetingTaskBridgeCommandHandlers.cs:457-468`). No R1 slice adds an organizer field to the form. Only a
          direct API caller who names another organizer and is not among the attendees reaches the rule. Without
          it, that caller would get 201 with the whole meeting, and their own retry would then get 409 for a meeting
          their own call wrote. With it, the refusal comes before anything is written, and a creator's retry reaches
          the replay branch only for a meeting they could view, so it answers 200 unless the meeting's people
          changed between the calls (then 409 is right). The rule only adds a refusal (R1-D12). The reason-code
          bridge carries its text in 7 languages, because the bridge guard requires every `MeetingReasonCodes`
          value (`tests/meetings-reason-code-bridge.test.js`); no form shows a hint for it (S26-10). **Behaviour
          change, stated:** a **direct API caller** who names another organizer, is not an attendee and has no
          `read-all` can no longer create that meeting; they add themselves as an attendee. The create form, the
          Task Center and the follow-up dialog are not affected.
      - **Replay branch.** When `FindOrCreateAsync` returns an existing meeting (`isNew = false`,
        `MeetingCommandHandlers.cs:151-152`), `CreateMeetingHandler` applies `MeetingWriteAccess` to that meeting
        before it reads its attendees or builds any response. A caller who passes gets the meeting with **200** (a
        replay is not a creation), and a new row still answers **201**. Any other caller gets **409
        `MEETING_CREATE_CONFLICT`** with one fixed message ("A meeting with these details could not be created.") and
        nothing else: no id and no field. Nothing is written on that branch (no attendee row, agenda line,
        preparation line, series or mail).
      - **Why 409 with a fixed body.** A refusal cannot hide that some meeting matches the five values: the caller
        chose all five, and any answer other than 201 tells them so. The fixed body makes sure it tells them nothing
        more, and it is the same body whether the existing meeting is confidential or not, and cancelled or not. A
        404 was rejected: a create request names no existing resource, so 404 would be a new and confusing meaning
        for this endpoint. A 409 is the answer the module already gives for "conflicts with existing data" (for example
        `MEETING_TYPE_NAME_DUPLICATE`). Putting the caller into the key was rejected too. It would change K11 for two
        people who submit the same meeting (one meeting today, two afterwards), and it would change how every future
        key is computed. That is more than an access-check slice (R1-D12) should change.
      - The create form reads `ok`, `reasonCode` and, on success, `data.id` from the create call (`form.js:179-194`;
        `form.js:103` is the edit page's load call, not the create call). `data.id` is present on 200 as on 201, so
        200 versus 201 changes nothing on screen. No existing test asserts 201 on a retry: the 201 assertions are on
        first calls (`MeetingInvitationTests.cs:308`, `MeetingTwoUserFlowMongoTests.cs:459`) and stay. The retry
        test is new (S28-8, creator rule row). *(Sentences corrected in fix round 3, review 03 F-20 and F-22.)*
      - **Follow-up (review 03 F-24).** `ScheduleFollowUpMeetingHandler` takes a meeting that `CreateMeetingCommand`
        has **newly written** (201) as today. For anything else it decides **before** it writes the follow-up link
        or any carried line:
        - a **409** from `CreateMeetingCommand` (the caller cannot view the existing meeting) is passed on as the same
          fixed `MEETING_CREATE_CONFLICT` body, and nothing is written;
        - a **200** replay (which `CreateMeetingCommand` returns only to a caller who can view the existing meeting):
          - its `FollowUpOfMeetingId` is the source, no follow-up link from the source to it exists yet, and it is
            **Scheduled** → **completion**: the handler writes the follow-up link with this call's idempotency key,
            then carries the source's open lines with the same method as a new follow-up, skipping every task that
            already has a line with `CarriedFromMeetingId` = the source on that meeting. It answers 200 with the
            meeting id and the number of lines carried from the source on that meeting;
          - its `FollowUpOfMeetingId` is the source, no follow-up link exists yet, and it is **Cancelled** or
            **Completed** (review 04 F-26) → the existing lifecycle conflict that every agenda write gives on such a
            meeting (`UpdateMeetingHandler.CheckEditable`, `MeetingCommandHandlers.cs:285-292`, used by the agenda
            handlers at `AgendaItemCommandHandlers.cs:38, 83, 126, 162`): 409 `MEETING_CANCELLED` or 409
            `MEETING_COMPLETED`. No follow-up link and no carried line is written. The replay itself does not look
            at the lifecycle (`FindByIdempotencyKeyAsync` and `FindOrCreateAsync`, `MeetingRepositories.cs:150-165`),
            so this check is the completion's own, made before its first write;
          - its `FollowUpOfMeetingId` is the source and the follow-up link already exists (written by another call)
            → 200 with the same id and count, and nothing is written, whatever the meeting's lifecycle;
          - its `FollowUpOfMeetingId` is not the source (null, or another meeting) → **409
            `MEETING_FOLLOW_UP_CONFLICT`** ("A meeting with these details already exists and is not a follow-up of
            this meeting."), whose `data` carries only that meeting's id. Nothing is written.
        - **Why this is safe.** The completion and the informative 409 are given only to a caller who can view both
          the source (checked at the load) and the existing meeting (checked by the replay branch), so neither
          reveals anything that caller cannot already open. `FollowUpOfMeetingId` is set only at create
          (`MeetingFollowUpCommandHandlers.cs:102-108`), so a meeting whose value is the source already declares
          itself that source's follow-up, and carrying writes no task title (a carried line holds a code,
          `:223-229`). The fixed body stays only for a caller who cannot view the existing meeting.
          The completion also writes only onto a **scheduled** meeting, the same lifecycle rule the agenda handlers
          apply, so it never writes a line that agenda editing would refuse (review 04 F-26).
        - The cycle check (`MeetingFollowUpCommandHandlers.cs:109-113`) stays as defence in depth. A first follow-up
          call cut off after the meeting was written and before its link is now completed by a retry from a caller who
          can view that meeting, while the meeting is scheduled. A retry by a scheduler who cannot view it still gets
          the fixed 409. Since Q25, a scheduler who is a party of the source is an attendee of the meeting their
          first call wrote, and a non-party scheduler can view it through `read-all` (a confidential one is refused
          before it is written, review 05 F-29), so this happens only when that
          call was cut off between the meeting insert and the scheduler's attendee row, or when the scheduler is
          outside the eligibility filter (the creator rule is not applied on this path). The
          meeting then stays a normal meeting that shows its source through `FollowUpOfMeetingId`, and anyone who can
          view both meetings completes it by scheduling the same follow-up again while it is scheduled. A retry that
          comes after the link was written is still answered by the follow-up's own idempotency link (`:68-77`). Two
          truly simultaneous completions keep today's window (the line-level skip narrows it, and nothing widens it).
          From S25 on, S25-8's re-sequencing also runs after a completion, because it runs on whatever meeting the
          handler carries lines onto.
        - **Behaviour change, stated (sweep).** Today the sweep chains onto any existing meeting with the same five
          values, a manually created one included, and writes the link and the carried lines onto it. From S28 on,
          it chains onto such a meeting only when that meeting's `FollowUpOfMeetingId` is the source. Otherwise the
          occurrence counts `failed` (409 `MEETING_FOLLOW_UP_CONFLICT`), the pointer stays, and the next occurrence
          chains from the same source again: one occurrence is missed, and the series does not stop.
          The same holds for a colliding follow-up of the source that is cancelled or completed and has no link:
          its lifecycle 409 counts `failed` (review 04 F-26).
      - **Scheduler as attendee (owner answer Q25, "devam-katilimci", 2026-10-29; binding once R1 is approved).**
        When an attendee who is not the organizer schedules a follow-up, the scheduler is added automatically as an
        attendee of the new meeting. `ScheduleFollowUpMeetingHandler` builds the `AttendeeUserIds` of the
        `CreateMeetingRequest` it sends (`MeetingFollowUpCommandHandlers.cs:81-90`) as the request's own list plus
        the caller, when both of these hold:
        - the caller is a **party of the source**: its organizer, or a user with an attendee row on it (any
          response). The attendee ids come from the same load `MeetingWriteAccess` makes for the source check
          (S28-1). A caller who passed the source check only through `read-all` (a `read-all` holder or a platform
          operator) is **not** added: they can open a non-confidential new meeting through `read-all`, and a
          confidential one is refused before anything is written unless they name themself as organizer or
          attendee (the non-party creator rule above; review 05 F-29, which corrects the earlier "anyway"). An extra row
          would inflate the report's invited count (`MeetingReportQueryHandlers.cs:73-77`) and, from S16, add a row
          that mandatory attendance requires to be marked;
        - the caller is not the new meeting's organizer (`request.OrganizerUserId ?? source.OrganizerUserId`,
          `:87`).

        Further rules:
        - **`Accepted`, not `Pending`.** The handler also passes the caller as `PreAcceptedUserId`, an optional
          trailing **command** parameter on `CreateMeetingCommand` (default null; never on `CreateMeetingRequest`
          or any HTTP request model, like the S28-4 marker). `CreateMeetingHandler` writes the row of that one user
          with `InvitationResponse.Accepted`, and every other non-organizer row as today (`Pending`). The reason is
          the same one that makes the organizer's own row `Accepted` (`MeetingCommandHandlers.cs:157-167`). A
          `Pending` row would put an accept/decline card for their own follow-up on the scheduler's Task Center
          (`MeetingWorkItemProvider.cs:61, 78-82, 123-127, 161`), show it as pending in their work calendar
          (`GetMyWorkCalendarHandler.cs:132, 138`), and leave it out of their time planning, which counts only
          `Accepted` rows (`MeetingGatewayAdapter.cs:27-28`). The owner said "added as attendee" without a response
          status; `Accepted` is the CT's default (Q25 row in §24.25). A source-scan test asserts that exactly one
          file, `MeetingFollowUpCommandHandlers.cs`, sets `PreAcceptedUserId`.
          **The body cannot set it (review 05 F-31).** Today this holds by construction: the controller builds the
          command from the request model (`MeetingsController.cs:42` for `POST /`, `:201` for the follow-up), and a
          trailing command parameter is not bound from the body. Because the field makes a user `Accepted` without
          their answer (no invitation card, counted in time planning), three guards keep it so:
          1. **HTTP test**, both endpoints: `POST /` and `POST /{id}/follow-up` with an extra body field
             `preAcceptedUserId` naming an invited user V (a second request spells it `PreAcceptedUserId`). V's row is
             `Pending`, and V gets a Task Center invitation item. On the follow-up, only the scheduler's own row
             (when Q25 adds it) is `Accepted`.
          2. **Reflection test**: no public property or constructor parameter named `PreAcceptedUserId`, compared
             case-insensitively as ASP.NET Core binds, exists on any type in `Features.Meetings` whose name ends with
             `Request` (the HTTP request models, `MeetingModels.cs`). S25 adds the name `NewMeetingId` to the same
             test, when it puts that command-generated id on `CreateMeetingCommand` and
             `ScheduleFollowUpMeetingCommand` (S25-7, pre-review 1 F-42).
          3. **What the source scan counts** (Roslyn syntax tree, the S28-2 package, over every `.cs` file under
             `services/Diten.Platform/src/`): a file counts when it contains (a) an argument named
             `PreAcceptedUserId:`, (b) an assignment to a member named `PreAcceptedUserId` in an object initializer or
             a `with` expression, or (c) an explicit `new CreateMeetingCommand(…)` with more than two positional
             arguments, so the trailing optional parameters can be passed only by name. Exactly one file may count.
             Measured today: the four constructions in `src` are explicit and positional with two arguments
             (`MeetingsController.cs:42`, `GenerateDueMeetingSeriesHandler.cs:160`,
             `MeetingFollowUpCommandHandlers.cs:92`, `MeetingTaskBridgeCommandHandlers.cs:468`), and there is no
             target-typed `new(…)` of the command; a target-typed construction is not found by a syntax scan, so the
             slice keeps the explicit form. The `RequireCreatorCanView` scan counts the same three forms.

          Sabotage: add `PreAcceptedUserId` to `CreateMeetingRequest` and pass it through to the command in
          `CreateMeetingHandler`'s caller, then the HTTP test (V is `Accepted`) and the reflection test are red.
        - **User calls only.** Under the system-origin marker (the series sweep), nothing is added and no
          `PreAcceptedUserId` is set: the acting identity is the series organizer, who is already the organizer,
          and an empty user id is never put on the list.
        - **`CreateMeetingHandler`'s other rules apply unchanged**, because the id only joins the request's list.
          It is written only for a new meeting (`isNew`, `MeetingCommandHandlers.cs:155`), only once (`Distinct()`
          and the organizer skip, `:171-176`), and only if it passes the eligibility filter (`:178-181`). A
          scheduler outside the filter is skipped like any attendee today (K12), and the follow-up is still created.
        - **No mail and no `.ics` line for the scheduler** (accepted behaviour). The mailer leaves out the acting
          user before it resolves recipients (`MeetingInviteMailer.cs:131-134`) and builds the `.ics` from those
          resolved recipients only (`:166`; ATTENDEE lines at `MeetingIcsBuilder.cs:77`), so the scheduler gets
          neither an invite e-mail nor an ATTENDEE line in the `.ics` others receive. The source organizer still
          gets the organizer's "added to your calendar" mail, as today.
        - **Replays write no attendee row.** The follow-up's own idempotency link (`:68-77`), the "link already
          exists" answer and the completion write no attendee row: the row belongs to the first call, which wrote
          the meeting.
        - **`RequireCreatorCanView: false` stays for a party** of the source. With the scheduler on the list, the
          creator rule would pass for every eligible scheduler anyway. It stays off so that a party outside the
          filter is not refused where the follow-up works today. For a non-party it is `true` (review 05 F-29, the
          "Where it applies" bullet above), and the S28-10 source-scan guard still names this one file.
        - **Confidential source (S15-10).** The follow-up inherits confidentiality, and the scheduler stays on it.
          This gives nobody new access: only a party of the source is added, so the scheduler already took part in
          the meeting the follow-up continues.
        - **Why.** Today the dialog sends the scheduler to the new meeting's page (`follow-up-dialog.js:106`), which
          they cannot open when they are only an attendee of the source and hold no `read-all` (measured above). The
          owner chose to add them (Q25 (a)) rather than leave the path as it is or refuse it. **Audit:** the
          `Accepted` attendee row is written by `CreateMeetingCommand`, which writes attendee rows today for any
          attendee a follow-up request names, and an `Accepted` row for the organizer. Neither
          `ScheduleFollowUpMeetingCommand` nor `CreateMeetingCommand` moves in S28 (R1-D12, stated deferral). S25's
          events record `SchedulerAddedAsAttendee` and `PreAcceptedUserId` (§25), and S25-7 tests them.
      - **The follow-up's own idempotency link checks its target (review 06 F-35).** Today, when the link exists,
        the handler reads the target's agenda and returns its id and carried line count without loading the target
        (`MeetingFollowUpCommandHandlers.cs:68-77`). From S28 it first loads the link's target through
        `MeetingWriteAccess` with the caller (the same helper and acting identity as the source check). A caller who
        can view it gets 200 with the id and count, as today, whatever its lifecycle. A caller who cannot, including a
        target that no longer exists, gets the fixed **409 `MEETING_CREATE_CONFLICT`** body (no id, no field), and
        nothing is read from the target's agenda or written. **Why this body and not a 404:** it is the answer the
        follow-up path already gives when its create replay lands on a meeting the caller cannot view (the
        "Follow-up (review 03 F-24)" bullet above), so a caller cannot tell which replay mechanism caught the request, and a 404 on
        `POST /{id}/follow-up` would say "this meeting could not be found" about a source the caller is looking at.
        Neither answer names the target.
      - **`FollowUpOfMeetingId`.** `CreateMeetingHandler` loads that meeting and applies `MeetingWriteAccess` right
        after the load. A caller who cannot view it gets the handler's own not-found body for it (404
        `MEETING_FOLLOW_UP_NOT_FOUND`, "The meeting this follows up on does not exist.", `:128-129`), and nothing is
        written.
- [ ] S28-11 — **Series (review 02 F-13; review 03 F-18).**
      - **Binding rule: the unchained fallback (review 03 F-18).** Before it chains, the sweep loads the chain
        source (`LastGeneratedMeetingId`) through `MeetingWriteAccess` under its system-origin marker, so `CanView`
        is applied with the series organizer as organizer or attendee only (S28-4). Series attendees never count for
        this check.
        - The source is visible → the chained follow-up runs as today, under S28-10's follow-up rules.
        - The source is **not** visible (the organizer is neither its organizer nor an attendee; the helper gives the
          same answer for a source that no longer exists) → the sweep still creates the occurrence, as a plain,
          **unchained** meeting: it sends the same `CreateMeetingCommand` it sends for a first instance
          (`GenerateDueMeetingSeriesHandler.cs:149-161`), with `FollowUpOfMeetingId` null. There is no follow-up
          link, no carried agenda line and no agenda link to a task of the source; nothing is read from the source
          beyond the visibility answer, and nothing is written onto it. On success `LastGeneratedMeetingId` moves to
          the new meeting, so the next occurrence chains from it, and the occurrence counts `generated`. The sweep
          logs one warning per such occurrence, `meetings.series.chain_broken SeriesId={SeriesId}
          ProcessInstanceId={ProcessInstanceId} PreviousMeetingId={PreviousMeetingId} MeetingId={MeetingId}
          CorrelationId={CorrelationId}` (no title), so an operator can see that a chain was broken.
        - If that plain create fails (for example S28-10's 409 for a colliding meeting the organizer cannot view),
          the occurrence counts `failed` on the existing path (`GenerateDueMeetingSeriesHandler.cs:93-103`), the
          pointer stays, and the next occurrence, with a different start, tries again. **No visibility rule stops
          the series for good** (narrowed in fix round 4, review 04 F-28 c). One case outside visibility still does,
          as today: when the series organizer is no longer an active tenant user, `CreateMeetingHandler` refuses
          every occurrence with 400 `MEETING_ORGANIZER_INVALID` (`MeetingCommandHandlers.cs:116-121`), on the
          chained and the unchained path alike. **Recovery:** a `series-manage` holder changes the series organizer
          through `UpdateMeetingSeries` (`MeetingSeriesCommandHandlers.cs:175`) to an **active tenant user**.
          `UpdateMeetingSeriesHandler` does not check eligibility (it refuses only an empty id,
          `MeetingSeriesCommandHandlers.cs:38-40`), so a recovery to another inactive user keeps failing. The next
          occurrence is then created, unchained if the new organizer cannot view the last generated meeting. S28 adds
          no automatic recovery.
        - **Why.** A failed occurrence never moves the pointer (`:93-103`, `:107`), and every later occurrence
          chains from it (`:147`), so refusing the source would stop the series for good after a legitimate
          organizer hand-over, with no manual way out (measured above). The fallback leaks nothing: the new meeting
          takes nothing from the source. An attacker who rewrites the series organizer only gets a new meeting they
          organize, which the `series-manage` key already let them shape (see below). **Stated consequence:** a
          series whose last meeting was deleted also continues, unchained; today it fails at every occurrence.
      - **`UpdateMeetingSeriesHandler`** (`MeetingSeriesCommandHandlers.cs:167-181`) keeps its rule: any
        `series-manage` holder may edit a series, including its organizer and attendees. `series-manage` stays an
        administrative key (R1-D11 adds no key), and S28 adds no check to this handler. The rule S28 applies instead
        is that **nothing a series edit writes is trusted for access to a meeting.** The sweep checks the stored
        organizer against the meeting itself at run time, and the stored attendee list is never a reason to admit
        anyone. A rewritten organizer or attendee list therefore reaches no meeting the new organizer could not
        already view. What the key still grants, stated: a holder may add people to **future** occurrences. They see
        carried lines only through the task read rule (a carried line never stores the task title,
        `MeetingFollowUpCommandHandlers.cs:223-229`), and the source meeting stays closed to them.
      - **`CreatedFromMeetingId`** (S23) is server-only, and adoption applies `CanView` (S23-8).

**Failure paths.**
- Non-viewer with the key → that handler's own 404 body, with no metadata (S28-1).
- Missing key → 403 (existing).
- Cross-tenant → 404.
- A meeting deleted while the request runs → 404.
- A creator who would not be able to view the meeting they ask for → 400 `MEETING_CREATOR_CANNOT_VIEW`, nothing
  written (S28-10). This is defensive: only a direct API caller who names another organizer reaches it (review 04
  F-25). From S15 a follow-up scheduler who is not a party of the source also reaches it with a confidential
  subtype, through the API or a type made confidential while the dialog was open (S15-10, pre-review 1 F-45).
- Create replay or follow-up collision by a caller who cannot view the existing meeting → 409
  `MEETING_CREATE_CONFLICT`, fixed body (S28-10).
- Follow-up collision with a meeting the caller can view that is not a follow-up of the source → 409
  `MEETING_FOLLOW_UP_CONFLICT` with only that meeting's id (S28-10).
- Follow-up completion onto a cancelled or completed follow-up of the source → 409 `MEETING_CANCELLED` / 409
  `MEETING_COMPLETED`, nothing written (S28-10; review 04 F-26).
- A follow-up scheduler outside the eligibility filter, or not a party of the source (a `read-all` holder or
  platform operator) → no scheduler row; the follow-up is still created, as today (S28-10, Q25). A non-party whose
  new meeting would be confidential (from S15) is refused with 400 `MEETING_CREATOR_CANNOT_VIEW` instead, and nothing
  is written (review 05 F-29).
- A follow-up retry whose idempotency link points to a meeting the caller can no longer view → 409
  `MEETING_CREATE_CONFLICT`, fixed body, nothing read or written (S28-10; review 06 F-35).
- A sweep without the marker → 404 for the follow-up. This is a defect, and the S28-4 test catches it.
- A sweep whose series organizer cannot view the source meeting → an unchained occurrence, the pointer moves on, and
  a `meetings.series.chain_broken` warning is logged; nothing is written onto the source (S28-11).

**Tests.** The S28-8 matrix, the source-scan guards (S28-2, S28-4, and the S28-10 `RequireCreatorCanView` and
`PreAcceptedUserId` guards), the report and export masking tests, the resolver drop test, the two sweep unit rows in
`GenerateDueMeetingSeriesHandlerTests.cs` (review 04 F-27), the Q25 marker and pre-accepted unit rows, the fix round
5 rows of S28-8, and the S28-10 `PreAcceptedUserId` body and reflection tests (review 05 F-31). Sabotage:
- remove the helper call from `AddMeetingAttendeesHandler` only (`UpdateMeetingHandler` in the same file still calls
  it) → the matrix row and the class-level S28-2 guard go red;
- move the access check after `CheckEditable` in `UpdateMeetingHandler` → the cancelled-meeting row goes red;
- make the follow-up handler's access refusal answer `MEETING_NOT_FOUND` → its body-equality row goes red;
- return the existing meeting to every caller on a replay → the create-replay row goes red;
- skip the creator rule → the creator-rule row goes red (a meeting is written);
- pass `RequireCreatorCanView: false` from the create endpoint as well → the `RequireCreatorCanView` source-scan
  guard and the creator-rule row go red;
- drop the `FollowUpOfMeetingId` = source condition from the completion → the "can view, not a follow-up" row goes
  red (a link and lines are written onto that meeting);
- drop the line-level skip in the completion → the second-retry row goes red (lines carried twice);
- drop the lifecycle check from the completion → the cancelled and completed completion rows go red (a link and
  lines are written onto a cancelled or completed meeting);
- drop the scheduler from the attendee list the follow-up sends → the Q25 row goes red (the scheduler gets 404 on
  the new meeting);
- drop `PreAcceptedUserId` (the scheduler's row is written `Pending`) → the Q25 row goes red (a Task Center invite
  item for their own follow-up, and a `Pending` row);
- add every caller who passed the source check, not only a party of the source → the `read-all` non-party row goes
  red (a row is added for them);
- add the caller to that list under the system-origin marker as well → the Q25 marker unit row goes red (the
  forwarded list differs from the request's);
- set `PreAcceptedUserId` in a second file → the `PreAcceptedUserId` source-scan guard goes red;
- skip `CanView` on `FollowUpOfMeetingId` → its row goes red;
- create the marker in a second file → the S28-4 guard goes red;
- give the marker an unconditional pass again → the series attack row goes red (lines carried onto the new
  meeting);
- count a source the organizer cannot view as `failed` again (the fix round 2 rule) → the hand-over row goes red
  (no meeting, the pointer stuck);
- skip the chain-source check in the sweep → the "source not visible" sweep unit row goes red
  (`ScheduleFollowUpMeetingCommand` is sent);
- let the unchained fallback set `FollowUpOfMeetingId` or carry lines → the series attack row goes red;
- remove the report task mask → S28-6 goes red;
- build the current-meeting title from all meetings again → the S28-6 current-meeting test goes red;
- keep `CurrentMeetingId` on a masked row → the S28-6 export test goes red;
- write the raw code into the export → the S28-6 export locale test goes red;
- make the resolver skip `CanView` → S28-7 goes red;
- pass `RequireCreatorCanView: false` for every follow-up caller again → the non-party creator-rule unit row goes red
  (`false` is sent for a non-party), and S15's HTTP row goes red once S15 exists (review 05 F-29);
- remove the target check from the follow-up's idempotency-link early return → the idempotency-link target row goes
  red (200 with the id and count; review 06 F-35);
- count only attendee rows as a party (drop the organizer branch) → the organizer-branch row goes red (no row for
  the scheduler; review 05 F-32 a);
- give a non-party who lists themself `PreAcceptedUserId` as well → the non-party self-listed row goes red (an
  `Accepted` row; review 05 F-32 b);
- move the completion's lifecycle check ahead of its link check, or ahead of the `FollowUpOfMeetingId` check → the
  "link exists" rows (409 instead of 200) or the cancelled non-follow-up row (`MEETING_CANCELLED` instead of
  `MEETING_FOLLOW_UP_CONFLICT`) go red (review 05 F-32 c);
- write the scheduler's row even when the eligibility filter leaves them out → the eligibility row goes red (a row is
  written; review 05 F-32 d);
- add `PreAcceptedUserId` to `CreateMeetingRequest` and pass it through → the S28-10 HTTP body test and the
  reflection test go red (review 05 F-31).

Restore each one and it goes green.

**L10n.** 7 languages: the masked task title and the masked current-meeting title on the report screen (Report resx)
and in the export (`MeetingReportExportPlaceholders`), and the `MEETING_CREATE_CONFLICT`,
`MEETING_CREATOR_CANNOT_VIEW` and `MEETING_FOLLOW_UP_CONFLICT` texts in the reason-code bridge (the task code is
already rendered on the meeting detail page). **Permissions:** unchanged keys; this slice
only adds the visibility rule (R1-D15). **Tenant isolation:** unchanged and re-tested (S28-8). **Audit:** the
access checks are access-check-only (R1-D12), so no command moves because of them. The replay's 200 and 409, the
creator rule's 400, the completion's lifecycle 409 and the reordered `RespondToInvitation` checks change answers,
not what any command writes. Three changes do write differently and are **not** access-check-only (review 04 F-28
b): the sweep's unchained fallback (S28-11), the follow-up completion and the scheduler's `Accepted` attendee row
(S28-10, Q25). They fall under R1-D12's stated deferral. None of `GenerateDueMeetingSeriesCommand`,
`ScheduleFollowUpMeetingCommand` and `CreateMeetingCommand` has an event in S28 (no meeting audit row can be written
before S25, Q13 and Q23). S21 and S25 move them to path `a`, and their events record the change through S25's
post-handler carrier (§25; S25-7 measures the carrier and tests `SchedulerAddedAsAttendee` and `PreAcceptedUserId`;
review 05 F-30). The idempotency-link target check (review 06 F-35) and the non-party creator rule (review 05 F-29)
are refusals only. None of the three changes writes a new kind of data. §25 names
S28 on each affected row.
**Depends on:** nothing. **Estimate:** 3–4 prompts (review 03 F-20: the test load measured above; review 04 F-27, F-26
and Q25 add one unit test file and about nine rows, which fit in 3; fix round 5 adds the idempotency-link target
check, the non-party creator rule, about eight HTTP rows and a reflection test, review 05 F-29, F-31, F-32 and review
06 F-35, which may need a fourth). **Evidence:** E5.
It crosses the MOD-0024 read rule and the Task Center projection.

---

## 25. Audited Events

> Added by R1 (AUD-001 §10.1 transition rule: the first job that adds or changes a write command writes this section
> for **all** of the module's write commands). The ledger line numbers refer to
> `tests/architecture/audit-ledger/Diten.Platform.md@2904f4029`, section "Bilinen borç". That list **only shrinks**.
> A slice that moves a command to path `a` removes its line in the same commit. **No R1 command may be added to the
> debt list.** "Known debt" below is the measured status today. It is **not** an exception class. Yol `a` = Platform
> `IAuditableCommand` + `IAuditMetadataProvider` (the `platform-merkezi` trace, best effort with a warning log, AUD-001
> §4.2). The audit category is Q13 (with Q23 for the shared mirror). **Confidential meetings: names only (S15-7,
> pre-review 1 F-40).** On every row below that touches a meeting whose effective flag is true, a changed content
> field is written by name only: no value, before or after, of `Title`, `Description`, `Location` or agenda `Text`.
> The ids, counts, `IsConfidential` and the changed field names stay. Where a row below lists one of those four
> fields, it is a value only for a non-confidential meeting, and only through the carrier. **`EntityId` (pre-review 1
> F-42).** A row whose command creates a record takes its `EntityId` from a server-only id generated on the command
> (`NewMeetingId` for `CreateMeetingCommand` and `ScheduleFollowUpMeetingCommand`, S25-7; the same rule for the series,
> type, category and agenda-line creates in their slices), because `AuditBehavior` fixes `EntityId` when it builds the
> plan. A row whose one command writes several records (the sweep, a bulk delete, the import, the reminder job,
> attendee adds) is a batch record with `EntityId` null, as Platform's bulk commands write today
> (`BulkDeleteDocumentAccessPolicyCommand.cs:11-17`), and names its ids in the record. **S28 adds only an access
> check to many rows** (R1-D12 access-check-only rule),
> **except the three stated deferred write changes** of R1-D12 (the sweep's unchained fallback, the follow-up
> completion, and the scheduler's `Accepted` attendee row; review 04 F-28 b, Q25), which the
> `GenerateDueMeetingSeriesCommand`, `ScheduleFollowUpMeetingCommand` and `CreateMeetingCommand` rows name. The
> access-check rows say "S28: access check" and stay where they are unless another slice changes what they write.
> **Handler facts (review 05 F-30).** `AuditBehavior` takes a command's metadata before its handler runs, so every
> field below that only the handler knows (a written count, an outcome, a stored before-value, the effective
> confidentiality that decides the names-only rule, a content value, the stamp count) is written through S25's
> post-handler carrier `MeetingAuditFacts`, whose recording S25-7 measures first. If that measurement fails, those
> fields leave the rows and only command fields stay (R1-D12), and no content value is written for any meeting
> (S15-7). **Read rows (review 06 F-37).** The last three rows are **reads**:
> single opens of a confidential meeting, an HR-confidential record by owner decision (AUD-001 §7). They go to the
> central log through the writer Q26 decides, never to an equivalent trail.

| Komut | Olay adi | Nesne (tur) | Yol | Onceki/sonraki ya da degisen alanlar | Istisna sinifi + gerekce |
|---|---|---|---|---|---|
| CreateMeetingCommand | meetings.meeting.created | Meeting | today known debt (L76) → **a in S25** (S15, S26 and S27 then extend it). S28 (before S25): `CanView` on `FollowUpOfMeetingId` and on the replay branch, 200 or 409 on a replay, the up-front creator rule (400 `MEETING_CREATOR_CANNOT_VIEW`, review 03 F-19) and the system-origin marker (S28-4, S28-10); those are refusals and access checks only. S28 also adds the command-only `PreAcceptedUserId` parameter (owner answer Q25), set only by `ScheduleFollowUpMeetingHandler`: the row of that one user is written `Accepted`. This is a write change, which does not move the command in S28 under R1-D12's stated deferral (no event in S28; S25 moves it and S25-7 tests the field; an `Accepted` attendee row is written today for the organizer) | EntityId = the command's `NewMeetingId` (pre-review 1 F-42). After: MeetingTypeId, StartAt, EndAt, OrganizerUserId, IsConfidential, attendee and agenda counts, PreAcceptedUserId when set; a `Title` value only when not confidential, otherwise the name only (S15-7, F-40); on a replay `ReplayOfMeetingId`. `NewMeetingId`, `PreAcceptedUserId`, `MeetingTypeId`, `StartAt`, `EndAt` are command fields; the effective organizer, `IsConfidential`, the written counts, `ReplayOfMeetingId` and the `Title` value go through the S25 carrier (review 05 F-30) | — |
| UpdateMeetingCommand | meetings.meeting.updated | Meeting | today known debt (L171) → **a in S15** (S28: access check first) | Before/after of the changed fields: MeetingTypeId, StartAt, EndAt, Location, Description, IsConfidential; on a confidential meeting `Title`, `Location` and `Description` by name only (S15-7, pre-review 1 F-40) | — |
| CancelMeetingCommand | — | Meeting | known debt (L69). S28: access check only (R1-D12), so it stays | — | not an exception; what it writes does not change in R1 |
| ReassignMeetingOrganizerCommand | meetings.meeting.organizer-reassigned | Meeting (+ MeetingAttendee) | today known debt (L128) → **a in S27**, which changes what it writes: the new organizer's attendee row is made or created required and `Accepted` in the same Platform transaction (S27-8; review 06 F-36). S28: access check before that | After: MeetingId, NewOrganizerUserId (command fields); through the carrier, the previous OrganizerUserId and the organizer-row action (created, made-required-and-accepted or unchanged) | — |
| AddMeetingAttendeesCommand | meetings.attendee.added | MeetingAttendee | today known debt (L57) → **a in S27** (gains `IsOptional`). S28: access check before that | After: MeetingId, UserIds added, IsOptional each | — |
| RemoveMeetingAttendeeCommand | — | MeetingAttendee | known debt (L135). S28: access check only, so it stays | — | not an exception; what it writes does not change in R1 |
| AddAgendaItemCommand | meetings.agenda-item.added | AgendaItem | today known debt (L55) → **a in S27** (gains presenter and duration). S28: access check before that | After: MeetingId, Text, PresenterUserId, DurationMinutes; on a confidential meeting `Text` by name only (S15-7, pre-review 1 F-40); EntityId = a command-generated id (F-42) | — |
| UpdateAgendaItemCommand | meetings.agenda-item.updated | AgendaItem | today known debt (L168) → **a in S18** (S27 extends it). S28: access check before that | Before/after: Text, PresenterUserId, DurationMinutes; MeetingId; IsMandatory; on a confidential meeting `Text` by name only (S15-7, pre-review 1 F-40) | — |
| DeleteAgendaItemCommand | meetings.agenda-item.deleted | AgendaItem | today known debt (L95) → **a in S18**. S28: access check before that | Before: Text, IsMandatory; MeetingId; on a confidential meeting `Text` by name only (S15-7, pre-review 1 F-40) | — |
| ReorderAgendaCommand | — | AgendaItem | known debt (L137). S28: access check only, so it stays | — | not an exception; what it writes does not change in R1 |
| CreateTaskFromMeetingCommand | — | RecordLink (+ MOD-0024 task) | known debt (L85). S28: access check only. S26 **calls** it in-process for each preparation line and does not change what it writes. If S26 has to change it, it moves to **a in S26**. | — | not an exception (the task itself has its own trace, `gorev-etkinlik-akisi`) |
| LinkExistingTaskCommand | — | RecordLink | known debt (L124). S28: access check only, so it stays | — | not an exception; what it writes does not change in R1 |
| ScheduleReviewMeetingForTaskCommand | — | Meeting + RecordLink | known debt (L157), unchanged | — | not an exception; unchanged |
| RespondToInvitationCommand | — | MeetingAttendee | known debt (L141). S28: its attendee-row check moves ahead of its 409 and 400 checks (S28-1; access check only), so it stays | — | not an exception; what it writes does not change in R1 |
| SaveMinutesDraftCommand | — | MeetingMinutesVersion | known debt (L155); S28 access check; S17 adds a field. It **stays in debt** under CT decision Q14 (b) (not K2; moving it to path `a` is allowed and left to S17) | — | not an exception; Q14 (b) |
| PublishMinutesCommand | — | MeetingMinutesVersion | known debt (L126); S28 access check; changed by S16, S18 and S27. **Stays in debt**: K2 class (CT decision Q14 (a)), no path `a` until the in-transaction path (K1) exists | — | not an exception; K2 debt rule, AUD-001 §4.4 |
| CorrectPublishedMinutesCommand | — | MeetingMinutesVersion | known debt (L72); S28 access check; changed by S16, S17, S18 and S27. **Stays in debt**: K2 class (Q14 (a)) | — | as above |
| ScheduleFollowUpMeetingCommand | meetings.meeting.follow-up-scheduled | Meeting (+ AgendaItem, RecordLink) | today known debt (L156) → **a in S25**, which changes how its agenda is ordered (S25-8; review 01 F-2). S28: source access check, the system-origin marker forwarded, `RequireCreatorCanView: false` on the create it sends, and the rule for a meeting that was not newly written: the fixed 409 for a caller who cannot view it, 409 `MEETING_FOLLOW_UP_CONFLICT` when it is not a follow-up of the source, and the idempotent completion when it is and the meeting is scheduled; a cancelled or completed one gets its lifecycle 409 (S28-10; review 03 F-24, review 04 F-26). S28 also adds the scheduler as an `Accepted` attendee of the create it sends (`PreAcceptedUserId`) when the scheduler is a party of the source and not the new organizer (owner answer Q25). The completion and the scheduler row are write changes, not access checks. They do not move the command in S28, under R1-D12's stated deferral (review 04 F-28 b): there is no event in S28, S25 moves the command (S25-7 tests the new field), and both changes write only kinds of rows a follow-up writes today (a link and carried lines; an attendee row through `CreateMeetingCommand`). S15: inherited confidentiality (S15-10) | EntityId = the command's `NewMeetingId`, which it passes to the create it sends (pre-review 1 F-42). After: SourceMeetingId, carried line count, SchedulerAddedAsAttendee (true when S28-10's Q25 rule added the caller), IsConfidential, `ReplayOfMeetingId` when an existing meeting answered; a `Title` value only when not confidential, otherwise the name only (S15-7, F-40). `NewMeetingId` and `SourceMeetingId` are command fields; the rest are handler facts and go through the S25 carrier (review 05 F-30) | — |
| CreateMeetingSeriesCommand | meetings.series.created | MeetingSeries | today known debt (L77) → **a in S21**. S23 adds the server-only `CreatedFromMeetingId` and the adoption's `CanView` (S23-8) | After: Name, MeetingTypeId, Frequency, Interval, StartsAt, EndsAt, OrganizerUserId, TitleTemplate, CreatedFromMeetingId | — |
| UpdateMeetingSeriesCommand | meetings.series.updated | MeetingSeries | today known debt (L172) → **a in S21** | Before/after of the changed fields | — |
| DeleteMeetingSeriesCommand | — | MeetingSeries | known debt (L97), unchanged | — | not an exception; unchanged |
| GenerateDueMeetingSeriesCommand | meetings.series.occurrence-processed | MeetingSeries | today known debt (L118) → **a in S21**, which changes what the sweep writes (titles from the template, the claim advanced on skipped days; review 01 F-2). S28 (before S21): creates the system-origin marker with the series organizer, checks the chain source with it, and falls back to an unchained create with a `meetings.series.chain_broken` warning log when the organizer cannot view the source (S28-4, S28-11; review 03 F-18). The fallback is not an access-check-only change: it is a different write in the same call. It does not move the command in S28, under R1-D12's stated deferral (review 04 F-28 b). The command has no event in S28 (no meeting audit row before S25, Q13 and Q23). S21 moves it, and its event records `created-unchained`. The fallback writes no new kind of data: it writes the claim and the pointer as today, and it creates the meeting through `CreateMeetingCommand` as a first instance does, with no link and no carried line | One batch record per sweep run, `EntityId` null: the command processes up to 200 series (`GenerateDueMeetingSeriesHandler.cs:30, 48, 59`), and each occurrence's meeting has its own create or follow-up record with its own `EntityId` (pre-review 1 F-42). After, per processed series: SeriesId, occurrence id, outcome (created, created-unchained, skipped or failed), created MeetingId, and for created-unchained the previous MeetingId. All are handler facts (the command carries `NowUtc`, `MaxSeries` and the correlation id) and go through the S25 carrier (review 05 F-30) | — (not İ2: it writes the claim `LastProcessInstanceId` on `MeetingSeries`) |
| CreateMeetingTypeCommand | meetings.type.created | MeetingType | today known debt (L78) → **a in S15** | After: every type field including the QA flags and MeetingCategoryId | — |
| UpdateMeetingTypeCommand | meetings.type.updated | MeetingType (+ Meeting, the type stamp) | today known debt (L173) → **a in S15**, which also adds the type stamp: a false → true change of `IsConfidential` sets `Meeting.IsConfidential = true` on every meeting of the type in the same Platform transaction (S15-11; owner decision, review 06 F-34), and a true → false change runs the same stamp before the flag is lowered (the closing stamp, pre-review 1 F-43) | Before/after of the changed fields; QA flags named separately; `IsConfidential` before and after and `ConfidentialStampedMeetingCount` on a raise or a lowering (the stored values and the count are handler facts, through the S25 carrier) | — |
| DeleteMeetingTypeCommand | meetings.type.deleted | MeetingType | today known debt (L98) → **a in S19** | Before: Name, MeetingCategoryId | — |
| CreateMeetingCategoryCommand | meetings.category.created | MeetingCategory | **a** (new, S19) | After: Name, Description, SortOrder | — |
| UpdateMeetingCategoryCommand | meetings.category.updated | MeetingCategory | **a** (new, S19) | Before/after: Name, Description, SortOrder | — |
| DeleteMeetingCategoryCommand | meetings.category.deleted | MeetingCategory | **a** (new, S19) | Before: Name | — |
| BulkDeleteMeetingCategoryCommand | meetings.category.bulk-deleted | MeetingCategory | **a** (new, S19) | Deleted ids; refused ids with reason | — |
| ImportMeetingTypeCatalogueCommand | meetings.type-catalogue.imported | MeetingCategory + MeetingType (+ Meeting, the type stamp) | **a** (new, S24) | Batch record, `EntityId` null (F-42). File SHA-256; created, updated and unchanged counts; created ids; updated ids with changed field names; for each subtype whose `isConfidential` changed, its id and `ConfidentialStampedMeetingCount` (the type stamp in the import's transaction, S24-6, pre-review 1 F-39) | — |
| SetMeetingAttendeeRoleCommand | meetings.attendee.role-changed | MeetingAttendee | **a** (new, S27) | Before/after: IsOptional; MeetingId, UserId | — |
| SendDueMeetingPreparationRemindersCommand | meetings.preparation-reminder.sent | Meeting | **a** (new, S26) | After: MeetingId, PreparationReminderSentForStartAt, open count. No titles. It is job-triggered with no user, so the same no-user question as §24.27 applies. | — (not İ2: it writes the claim marker on `Meeting`) |
| GetMeetingByIdQuery (**read**; confidential meeting only) | meetings.meeting.confidential-opened (surface `detail`) | Meeting | **read audit, new in S15** (S15-12): central log through the Q26 writer, written after the access check and before the response; **fail-closed** (503 `MEETING_CONFIDENTIAL_READ_NOT_RECORDED`). Owner decision: a confidential meeting is an HR-confidential record (AUD-001 §7; review 06 F-37) | Actor (from the token), tenant, EntityId = MeetingId, SourceModule `MOD-0357`, surface, request correlation id. No title, description, location, agenda or minutes text | — (a read row, AUD-001 §7; not a write command) |
| GetLinkedTasksQuery (**read**; confidential meeting only) | meetings.meeting.confidential-opened (surface `tasks`) | Meeting | **read audit, new in S15** (S15-12), as above | As above | — (a read row, AUD-001 §7) |
| GetMeetingMinutesQuery (**read**; confidential meeting only) | meetings.meeting.confidential-opened (surface `minutes`) | Meeting | **read audit, new in S15** (S15-12), as above | As above | — (a read row, AUD-001 §7) |

Notes:
- `PreviewMeetingTypeCatalogueImportQuery` is a **query** that writes nothing, so it is not a command row. If it is
  ever implemented as a command, its class is İ5 ("hiçbir kalıcı kaydı değiştirmez — önizleme").
- **K2.** This draft reads the seven new commands as configuration or operational records, not K2 (S26's reminder
  and S27's role change included). The CT confirmed this for the type, main-type and import commands under Q14 (c).
  **Writes on a confidential meeting are not classified yet: Q27 (pre-review 1 F-41), open.** The F-37 owner decision
  made a confidential meeting an HR-confidential record (AUD-001 §7), and whether its writes are also K2 (§4.3, §4.4)
  is Q27's question; this table does not answer it. Until Q27 is answered, the rows above that touch a meeting are
  written as path `a` for the meetings that exist before S15 (none is confidential), and S15 does not start. If the
  answer is (B) or (C), the rows of the commands Q27 lists change in the slice the answer names.
- **The first audit row and the shared mirror.** S25 is the first slice that writes a meeting audit row. Its category
  follows Q13 and Q23. S25 does not start until Q23 is answered; yes → the shared mirror gets `["Meetings"] = 18`
  (written by this CT after the owner's explicit XMC-001 answer, reviewed by the library owner, per card S-90 §7c), and
  its drift test (`CentralAuditDispatcherTests.cs:155`) stays green in the same slice; no → `PlatformConfiguration` is
  used and Q13 is reopened.
- **Read audit (review 06 F-37).** Only the three single opens above are read-audited. The list, calendar and
  report screen are not single opens; the report's bulk export is already recorded by `IDataExportAuditWriter`; no
  single-meeting export or print exists. **Content-free instead of recorded (pre-review 1 F-38):** for a confidential
  meeting, the minutes write responses and the create replay withhold stored content (`ContentWithheld`), and the
  report and export show `MEETING_CONFIDENTIAL_CONTENT` in place of its decision text (S15-12), so these paths write
  no read row. A non-confidential meeting writes no read record. A refused open (404) writes
  none either, because nothing was opened. `AuditBehavior` skips queries (`AuditBehavior.cs:109-118`), so the read rows
  never use path `a`'s pipeline; they use the central writer that Q26 decides.
- **No-user commands.** `CreateMeetingCommand`, `ScheduleFollowUpMeetingCommand`, `GenerateDueMeetingSeriesCommand`
  and `SendDueMeetingPreparationRemindersCommand` run from the sweep or the reminder job with no user. The first
  slice that moves each one measures that `AuditBehavior` accepts it (§24.27).
- `tests/architecture/audit-ledger/Diten.Platform.md` is in `tests/**`. Each slice that moves a command edits it
  (removing lines only), and `AuditTrailStandardTests` must be green in that slice.

---

## 26. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi (SOP §23.1) |
|---|---|---|---|
| DL-1 | Corrections inside the delivered S1 to S12 that stay within this module (for example the subtype index fix in §24.24). A correction that crosses a module seam is not DL-1: it needs its own WP row at E5. | Implementation Notes | E4 |
| DL-13 | S13 page help and tour | S13 — Page help and Meetings tour | E3 |
| DL-14 | S14 honest flags | S14 — Honest display of the type flags | E3 |
| DL-15 | S15 confidential type | S15 — Confidential meeting type, Audited Events | E5 (crosses the Task Center projection, notifications, the report and the central audit log; the type stamp with its closing-stamp race rows and the confidential read audit run on real Mongo) |
| DL-16 | S16 mandatory attendance | S16 — Mandatory attendance before publishing | E4 + persistence L3 (quality-record minutes; SOP §23.1 "Compliance/financial persistent record: L3") |
| DL-17 | S17 agenda note and outcome | S17 — Note and outcome per agenda item | E4 + persistence L3 (quality-record minutes) |
| DL-18 | S18 mandatory agenda items | S18 — Mandatory agenda items for quality-record types, Audited Events | E4 + persistence L3 (quality-record minutes) |
| DL-19 | S19 main type backend | S19 — Main type and subtype default fields (backend), Audited Events | E4 |
| DL-20 | S20 type screens | S20 — Type screens and grouped type picker | E4 |
| DL-21 | S21 daily and series title | S21 — Daily frequency and series title template, Audited Events | E5 (Q8 answered yes: reads the working calendar) |
| DL-22 | S22 create auto-fill | S22 — Create-form auto-fill of the other fields | E5 (positions and people from the organization and task modules) |
| DL-23 | S23 repeat on create | S23 — Repeat on create | E4 (including a sweep run after create and the real-Mongo partial index test) |
| DL-24 | S24 catalogue importer | S24 — One-off type catalogue importer, Audited Events | E5 (reads the MOD-0024 task-type catalogue and organization positions; the type stamp it calls runs on real Mongo, pre-review 1 F-39) |
| DL-25 | S25 agenda at create | S25 — Agenda at create (today's type template), Audited Events | E4 (create, persistence, invite mail, follow-up and series paths) |
| DL-26 | S26 preparation tasks | S26 — Preparation tasks on the create form (never a block), Audited Events | E5 (creates MOD-0024 tasks through the bridge under the real assignment guard; background reminder) |
| DL-27 | S27 attendee roles and agenda time box | S27 — Required and optional attendees with agenda presenter and time box, Audited Events | E4 (persistence, RBAC and tenant, `.ics` content, the organizer row on reassignment) |
| DL-28 | S28 meeting access security | S28 — Meeting access security (writes and BL-541), Audited Events | E5 (the write matrix over HTTP with real Mongo, its series rows at handler level with real Mongo; crosses the MOD-0024 read rule and the Task Center projection) |
