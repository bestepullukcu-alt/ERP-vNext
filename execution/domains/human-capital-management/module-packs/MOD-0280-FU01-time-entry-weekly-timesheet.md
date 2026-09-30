---
id: MOD-0280-FU01
name: Time Entry & Weekly Timesheet
parent: MOD-0280
parent_name: Time, Attendance & Leave Management
domain: human-capital-management
service: Diten.Platform
service_deviation: "HCM domain-config.md:58 names Diten.HcmService; ADR-004 places this slice in Diten.Platform with an extraction path (§2.4)"
adr: docs/records/decisions/2026-09/ADR-004-time-entry-mod-0280-fu01-lives-in-platform.md
shell: tenant
golden_reference: slim            # the one CRUD surface (work categories, 5 fields); the weekly grid is not a DataTable (§11)
screens:
  - module: Categories
    data_mode: client
    data_mode_max_rows: 50
  - module: Approvals
    data_mode: server
entity_base: TenantScopedEntity   # Platform's live tenant-owned base (TaskItem, Meeting); module-pack-standard's "BaseEntity" is the older name
status: ready-for-dev
status_changed: "draft -> ready-for-dev on 2026-09-29 (Control Tower resolved every §25 question and §26 conflict; none left blocking — see §26)"
decided_by: "Control Tower on the owner's delegation, 2026-09-29 (D1–D13 + resolutions R1–R11 + answers A1–A10); Time Entry SoR = MOD-0280 confirmed by the owner 2026-09-17 (DEC-002)"
owner: ali.tufanoglu
branch: feature/hr/mod-0280-fu01-time-entry-weekly-timesheet   # 'hr' short code: MOD-0251 precedent; not yet in AGENTS.md §9
started: 2026-09-29
target: TBD
form_field_count: 5
---

# MOD-0280-FU01 — Time Entry & Weekly Timesheet

> **Product name: "Zaman Çizelgem" / "My Timesheet".** `MOD-0280 Time, Attendance & Leave Management` is the
> Blueprint 8.1 canonical parent; this pack is its first child, `MOD-0280-FU01`. The runtime literals are the module
> code `time-entry`, the permission namespace `time-entry.*` and the collection prefix `time_entry_`. "Zaman Çizelgem"
> is a display name only and never appears in an identifier.

> **Authority.** `status: ready-for-dev` (2026-09-29). Code starts slice by slice (§23), each slice with its own WP;
> CT prerequisites listed per slice must be done first. The decisions (§21) were made by Control Tower on the owner's
> delegation; the placement is recorded in
> [ADR-004](../../../../docs/records/decisions/2026-09/ADR-004-time-entry-mod-0280-fu01-lives-in-platform.md).
> Every repo-rule conflict found while writing the pack is listed in §26 with the CT resolution next to it.

> **Identity gate (DCP-002) — passed 2026-09-29, re-run after the registry rows were written:**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0280 --name "Time, Attendance & Leave Management"` → `OK` (exit 0)
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0280-FU01 --name "Time Entry & Weekly Timesheet" --parent MOD-0280` → `OK` (exit 0)
>
> **Why an FU and not the parent ID.** MOD-0280's Blueprint scope is time entry **and** attendance, schedules/rosters,
> absence and leave balances (`Blueprint_Data` MOD-0280: SoR "Time Entry; Attendance; Schedule; Leave Balance"; soft
> pages "Time Entry; Attendance Calendar; Leave Request; Absence Record; Schedule / Roster"). This slice owns only
> the first of those. Taking the parent ID for a partial slice would make every later HR slice a rename; the
> MOD-0262 / MOD-0262-FU01 and MOD-0117 / MOD-0117-FU01 precedents reserve the parent and give the first slice an FU.

---

## 1. Module Summary

MOD-0280-FU01 is the system of record for **how a person's working time was spent**: per local day, how many
minutes went to which task or to which non-task work category, where the number came from (timer, typed, meeting,
calendar plan), and whether the person's line manager approved the week. It is the "Time Entry" object of the
Blueprint MOD-0280 row and the store DEC-002 names (confirmed by the owner 2026-09-17).

What it is **not**, stated on the screen and in this pack:

> **Effort allocation, not a statutory working-time record.** It does not replace a Swiss entity's ArGV 1 Art. 73
> working-time record, a Turkish entity's attendance record, a clock terminal, payroll time or leave. (§22)

Three things close today's gap (`docs/reference/modules/tenant/workcenter/workcenter-reporting-and-time-decisions.md`
§6, corrected 2026-09-29):

1. `TaskItem.SpentHours` has **no writer**: `CreateTaskItemHandler.cs:320` sets `0m`, the edit path skips it
   (`TaskItemWriteHandlers.cs:162`), the form field is `readonly disabled` (`Views/Tasks/_Form.cshtml:309`).
   After this slice a task's spent time is the **sum of approved entries**, read from this module (D7).
2. The Task Center timer is a **showcase**: `foldTimer` (`wwwroot/assets/js/WorkCenterNext/app.js:694`) adds browser
   time to an in-memory field and never reaches a server. The resx admits it (`WorkCenterNextIndex.en.resx:612`
   "timer started (mock)", `:941` "Elapsed time is not recorded"). This slice replaces it with a server-side timer
   whose output is a **draft** (D2, D3, Z-2).
3. There is no weekly sheet, no approval, no non-task time. This slice adds the weekly grid, MOD-0023 line-manager
   approval and a tenant category catalogue (D5, D6, D10).

Target user: every tenant user entitled to the module records time by hand; the **timer** exists only where a tenant
admin switched it on for the person's legal entity (default off everywhere — D12). The line manager approves. No one
else sees a person's time unless explicitly granted (D11).

---

## 2. Ownership and Boundaries

### 2.1 MOD-0280-FU01 owns

- Timer segments (raw working data; start/stop instants cleared at approval — D4), one running timer per person (D2).
- Time entries: local date + duration + task **or** category + source (D4).
- Timesheet weeks and their revisions: draft → submitted → approved, withdraw and reject back to draft, correction
  as a new revision, time-admin reopen of old weeks (D5).
- The approver resolution stored at submit (D6) and the link to the MOD-0023 workflow instance.
- Approved task totals, keyed by task id — the single source of a task's spent time (D7).
- Meeting time suggestions (D8); plan fill-in is computed, never stored (D9).
- The tenant work-category catalogue (D10) and module settings, including the per-legal-entity timer switch (D12).

### 2.2 Does NOT own (consume, never re-implement)

| Concern | Owner | How it is reached |
|---|---|---|
| Task lifecycle, holder, plan block | **MOD-0024** | `ITaskTransitionObserver` (one call at the choke point) + `ITimeEntryTaskGateway` (read port) — never a join on `task_items` |
| Approval decision | **MOD-0023** | `StartWorkflowInstanceCommand` with `CandidatePrincipalIds`; the decision is read back, never made locally. T1 adds one per-definition option to MOD-0023 (reason required on reject — R5) |
| Meeting, attendee response, minutes attendance | **MOD-0357** | `ITimeEntryMeetingGateway` (read port over the meetings repositories); no file in `Features/Meetings` edited |
| Person, seat, position, reports-to, legal entity of a unit | **MOD-0288** | `ITimeEntryOrgGateway` over `ITaskSeatDirectory` / `IPositionRepository` / `IOrganizationUnitRepository` |
| Working days, holidays, working window, time zone | **Working Calendar** (registry `CAND-CAP-0010`) + `IWorkingHoursProvider` | read; T1 adds a daily target and the half-day fix to the provider (D13) |
| Audit, notifications, scheduler, permissions | FG-005 audit · MOD-0027 · Hangfire seam · MOD-0018 | existing seams, unchanged |
| Leave, absence, attendance, schedules/rosters, overtime, payroll time | later MOD-0280 FUs / MOD-0279 | **out of scope** (§20) |

### 2.3 Not to be confused with

- **DitenHumanCapitalService "Time, Attendance & Leave"** (`/HumanCapital/TimeAttendanceLeave`,
  `hcm.time-attendance-leave.*`, `TimeAttendanceLeaveGuard.cs:11-15`) is a **readiness-record** module
  (`TimeAttendanceLeaveReadinessState`), not a time store. It is untouched by this pack. Its page name is almost the
  Blueprint name of MOD-0280, so the T2 nav labels must be distinct ("My Timesheet", "Timesheet Approvals").
- **PPM "Project Effort Log"** (`portfolio-delivery/domain-config.md:53`, ASSUMPTION-1, DCP-003 B2) — see §24.

### 2.4 The placement deviation and its extraction path (D1, ADR-004)

This slice runs **inside `Diten.Platform`** although `human-capital-management/domain-config.md:58` names
`Diten.HcmService` as the HCM backend. The decision, its reasons and its revisit triggers are in ADR-004. The
deviation is only acceptable because the slice is built so that moving it later is a **data move, not a rewrite**:

- Its own collections, prefix `time_entry_` — never a shared collection.
- Its own permission namespace `time-entry.*` — not `platform.*` (so extraction needs no key rename, PKS-001 §2) and
  not HumanCapitalService's `hcm.time-attendance-leave.*`.
- Its own module code `time-entry` and its own `IModuleManifestProvider`.
- Every record keyed by **TenantId + UserId**. No Employee id (HcmService's `Employee` has `PersonId` but no
  `UserId` — `Employee.cs:7`).
- Tasks, meetings and the org chart are reached **only through the ports in §3.3**. A source-scanning guard (§17
  T-01) fails the build if anything under `Features/TimeEntry/` outside `Adapters/` references a MOD-0024/MOD-0357/
  MOD-0288 repository or collection.
- The two points where other modules call this one (`ITaskTransitionObserver`, `ITaskSpentTimeSource`) are
  interfaces in `Application/Contracts`; in a later service they become an outbox event and an HTTP read.
- Extraction unit = `time_entry_*` collections + `time-entry.*` keys + `Features/TimeEntry/**` + the two contract
  interfaces + the gateway prefix. Revisit triggers (ADR-004): MOD-0251 live and linked to users, or the MOD-0280
  leave/attendance slices start.

---

## 3. Owned Objects

### 3.1 Runtime entities (all `TenantScopedEntity`, Mongo, soft delete, tenant-first indexes)

| Entity | Collection | Purpose |
|---|---|---|
| `TimerSegment` | `time_entry_timer_segments` | One run of the timer: start/stop instant (cleared at approval), duration, task or category, why it started/stopped. Never crosses local midnight |
| `TimeEntry` | `time_entry_entries` | One row per (week revision, local date, task-or-category, source): minutes |
| `TimesheetWeek` | `time_entry_timesheet_weeks` | One row per (person, ISO week, revision): status, approver resolution, workflow link, correction chain, reopen |
| `TaskTimeTotal` | `time_entry_task_totals` | Approved minutes per task — single writer = the approval finalizer (D7) |
| `TimeSuggestion` | `time_entry_suggestions` | Meeting suggestion per (meeting, person) (D8). Plan fill-in is not stored (D9) |
| `WorkCategory` | `time_entry_work_categories` | Tenant catalogue of non-task work (D10) |
| `TimeEntrySettings` | `time_entry_settings` | One per tenant: time-admin pool position (D6 fallback) |
| `LegalEntityTimeSetting` | `time_entry_legal_entity_settings` | One per (tenant, legal entity): timer switch, default off, who changed it and why (D12) |

### 3.2 CQRS (Golden Reference convention; one Feature folder, `Features/TimeEntry/`)

Commands (timer): `StartTimer`, `StopTimer`, `UndoTimerSwitch`, `CloseTimersAtLocalMidnight` (system),
`ReconcileOrphanTimer` (system).
Commands (sheet): `SaveTimeEntries` (bulk upsert of one week's draft rows), `AcceptTimeSuggestion`,
`DismissTimeSuggestion`, `SubmitTimesheetWeek`, `WithdrawTimesheetWeek`, `RequestTimesheetCorrection`,
`DiscardCorrectionDraft`, `ReopenTimesheetWeek` (time admin), `FinalizeTimesheetDecision` (system — the approval
finalizer, D7; also clears the approved week's segment instants, D4).
Commands (admin): `CreateWorkCategory`, `UpdateWorkCategory`, `ActivateWorkCategory`, `DeactivateWorkCategory`,
`InstallRecommendedWorkCategories`, `UpdateTimeEntrySettings`, `SetLegalEntityTimerSwitch`.
Queries: `GetMyTimer`, `GetMyTimesheetWeek` (grid: days, targets, holidays, entries, suggestions, flags),
`GetPlanFillIn` (ghost values, writes nothing), `GetApprovalList`, `GetApprovalWeek` (read-only),
`GetWorkCategoryList`, `GetWorkCategoryById`, `GetTimeEntrySettings`.

Nothing in this module is hard-deleted: entries of a submitted/approved revision cannot be removed, categories are
deactivated, segments are minimised (D4, D10).

### 3.3 Ports, providers, services

| Object | Where | Purpose |
|---|---|---|
| `ITaskTransitionObserver` | `Application/Contracts` | Called once by `TaskRepositories.RecordIfMovedAsync` after the transition row is written; receives tenant, task id, **transition id**, kind, from/to lifecycle, actor, previous/current holder, occurred-at. Never throws into the caller |
| `ITaskSpentTimeSource` | `Application/Contracts` | Batch read of approved (and, separately, submitted) minutes per task id — what MOD-0024's read sites call instead of `TaskItem.SpentHours` (D7) |
| `ITimeEntryTaskGateway` | `Features/TimeEntry` (port) + `Adapters/` (v1 impl over MOD-0024 read repositories) | Task facts: lifecycle, holder, title, type, plan block, latest invalidating transition |
| `ITimeEntryMeetingGateway` | same pattern | Accepted meetings of a person in a date range + current minutes attendance |
| `ITimeEntryOrgGateway` | same pattern | Primary seat → position → `ReportsToPositionId` chain → holders; legal entity of the primary seat |
| `TimesheetApprovalService` | `Features/TimeEntry/Services` | Ensures the timesheet approval definition (same `EnsureTemplateAsync` pattern as `TaskApprovalService`) **with "comment required on reject" on**; starts the instance, idempotency key `timesheet-week:{tenant}:{weekId}:{revision}`; cancels it on withdraw through MOD-0023's own cancel command |
| `TimesheetApprovalSourceResolver : IApprovalSourceResolver` | `Features/TimeEntry/Providers` | Gives MOD-0023's Task Center card a title ("Timesheet · 2026-W40 · {person}"), the flagged-day marker and a deep link; object type `timesheet-week` |
| `TimeEntryManifestProvider : IModuleManifestProvider` | `Features/TimeEntry/SelfRegistration` | Module code `time-entry` (catalog module, `IsBaseline: false`, entitlement-gated), pages, actions, permissions, notification events (§14) |
| Jobs | `Features/TimeEntry/BackgroundJobs` | `Diten.Platform.MOD-0280.TimerMidnightCloseJob`, `Diten.Platform.MOD-0280.TimesheetDecisionSweepJob` (T1); `Diten.Platform.MOD-0280.TimesheetReminderJob` (T3). Each behind `BackgroundJobs:RegisterStandardJobs` + its own `EnabledJobs[...]` flag; **correctness never depends on a job** (read-time paths, D3/D7) |

### 3.4 API (`api/v1/time-entry`)

`GET /timer` · `POST /timer/start` · `POST /timer/stop` · `POST /timer/undo-switch` ·
`GET /weeks/{weekKey}` · `PUT /weeks/{weekKey}/entries` · `GET /weeks/{weekKey}/plan-fill-in` ·
`POST /weeks/{weekKey}/suggestions/{id}/accept` · `POST /weeks/{weekKey}/suggestions/{id}/dismiss` ·
`POST /weeks/{weekKey}/submit` · `POST /weeks/{weekKey}/withdraw` · `POST /weeks/{weekKey}/corrections` ·
`DELETE /weeks/{weekKey}/corrections/draft` ·
`GET /approvals` · `GET /approvals/{weekId}` ·
`POST /admin/weeks/{weekId}/reopen` ·
`GET /categories` · `GET /categories/{id}` · `POST /categories` · `PUT /categories/{id}` ·
`POST /categories/{id}/activate` · `POST /categories/{id}/deactivate` · `POST /categories/install-recommended` ·
`GET /settings` · `PUT /settings` · `GET /settings/legal-entities` · `PUT /settings/legal-entities/{legalEntityId}`.

`weekKey` = ISO 8601 week, e.g. `2026-W40`. Approve/reject are **MOD-0023's own** actions
(`platform.workflow.tasks.approve` / `.reject`), reachable from the Task Center approval card and from the T2
approvals page; this module never exposes its own approve endpoint. `api/v1/time-entry` does not exist in the repo
today; no legacy collision.

---

## 4. Entity Fields

Business revision fields are named `RevisionNumber`, never `Version` — `Version` is the concurrency field
(module-pack-standard §14, entity-base-template).

### 4.1 `TimerSegment`

| Field | Type | Req. | Rule |
|---|---|---|---|
| `UserId` | Guid | Yes | the person; server-set from the caller or from the transition's holder |
| `TaskItemId` | Guid? | XOR | exactly one of `TaskItemId` / `CategoryCode` |
| `CategoryCode` | string? | XOR | active `WorkCategory.Code` |
| `IsRunning` | bool | System | partial unique index `{TenantId, UserId}` where `IsRunning == true && IsDeleted == false` — one running timer per person (D2) |
| `StartedAtUtc` | DateTimeOffset? | Until approval | server clock only; **cleared** when the week is approved (D4) |
| `StoppedAtUtc` | DateTimeOffset? | Until approval | null while running; **cleared** when the week is approved (D4) |
| `DurationSeconds` | int | System | written at stop; kept after minimisation |
| `LocalDate` | DateOnly | Yes | `WorkingHoursResult.LocalDateOf(StartedAtUtc)` — tenant time zone (R3) |
| `TimeZoneId` | string | Yes | the zone the provider returned at start |
| `WeekKey` | string | Yes | ISO week of `LocalDate` |
| `StartSource` | enum | Yes | `TaskStarted \| TaskResumed \| TimerControl \| UndoSwitch` |
| `StartTransitionId` | Guid? | No | `TaskTransition.Id` when started by the hook — unique sparse index `{TenantId, StartTransitionId}` (idempotency, D2) |
| `StopReason` | enum? | No | `TimerControl \| Switch \| TaskTransition \| LocalMidnight \| Reconcile \| SwitchedOff` |
| `StopTransitionId` | Guid? | No | transition that stopped it (idempotency) |
| `OutsideWorkingMinutes` | int | System | minutes outside the day's working window(s), computed at stop (D3); kept after minimisation |
| `MinimisedAtUtc` | DateTimeOffset? | System | set by the finalizer when the instants are cleared (audited update, not a delete) |

### 4.2 `TimeEntry`

| Field | Type | Req. | Rule |
|---|---|---|---|
| `TimesheetWeekId` | Guid | Yes | the revision this row belongs to |
| `UserId` | Guid | Yes | = week's `UserId` |
| `LocalDate` | DateOnly | Yes | inside the week; not after the person's local today (D5) |
| `DurationMinutes` | int | Yes | multiple of 15, 15…960 (> 960 refused as implausible — A3) |
| `TaskItemId` / `CategoryCode` | Guid? / string? | XOR | exactly one |
| `Source` | enum | Yes | `Timer \| Manual \| Meeting \| Plan` (D4, D8, D9) |
| `SourceRef` | string? | No | meeting id for `Meeting`; null otherwise (timer rows relate to segments by user + date + target) |
| `EditedFromTimer` | bool | System | true when the person changed a timer-derived duration |
| `OutsideWorkingMinutes` | int | System | carried from the segments (D3) |
| `Note` | string? | No | trim, max 500 |
| `MinutesConflict` | bool | Read-time | minutes say Absent/Excused for an accepted meeting row — shown to the person only (D8) |

Unique index `{TenantId, TimesheetWeekId, LocalDate, TaskItemId, CategoryCode, Source}`.

### 4.3 `TimesheetWeek`

| Field | Type | Req. | Rule |
|---|---|---|---|
| `UserId` | Guid | Yes | |
| `WeekKey` | string | Yes | ISO week, Monday start |
| `WeekStartDate` | DateOnly | Yes | the Monday |
| `TimeZoneId` | string | Yes | captured at first write |
| `RevisionNumber` | int | Yes | 1…n; unique `{TenantId, UserId, WeekKey, RevisionNumber}` |
| `Status` | enum | Yes | `Draft \| Submitted \| Approved \| Superseded` (withdraw and reject return to `Draft` — D5) |
| `InForce` | bool | System | exactly one approved revision in force per `{TenantId, UserId, WeekKey}` (partial unique index) |
| `CorrectionOfRevision` | int? | No | set on a correction revision |
| `CorrectionReason` | string? | Cond. | required when `CorrectionOfRevision` is set; trim, max 1000 |
| `LegalEntityId` | Guid? | System | snapshot of the primary seat's legal entity at submit — lets retention become per-entity later (§20) |
| `ApproverCandidateUserIds` | List\<Guid\> | System | resolved at submit (D6); never contains `UserId` |
| `ApproverResolution` | enum | System | `LineManager \| ManagerChain \| TimeAdminPool` |
| `WorkflowInstanceId` | Guid? | System | MOD-0023 instance (cleared reference kept in audit on withdraw) |
| `FlaggedDates` | List\<DateOnly\> | System | days whose total exceeds 660 minutes (A3) — shown to the approver |
| `SubmittedAtUtc` / `SubmittedByUserId` | | System | |
| `WithdrawnAtUtc` | DateTimeOffset? | System | last withdrawal (A6) |
| `ApprovedAtUtc` / `ApprovedByUserId` | | System | copied from MOD-0023's transition log by the finalizer |
| `LastRejectedAtUtc` / `LastRejectedByUserId` / `LastRejectionReason` | | System | from MOD-0023; the reason is mandatory there (R5) and shown to the person |
| `ReopenedAtUtc` / `ReopenedByUserId` / `ReopenReason` | | System | time-admin reopen of a week older than the edit window (A9); the week stays editable until submitted |
| `TotalMinutes` | int | System | cached at submit |

### 4.4 `TaskTimeTotal`

`TaskItemId` (unique per tenant), `ApprovedMinutes` (int), `RecomputedAtUtc`, `LastFinalizedWeekId`.
Recomputed from the in-force approved revisions' entries for that task — a recomputation, not an increment, so a
replayed finalization writes the same number.

### 4.5 `TimeSuggestion`

`UserId`, `MeetingId`, `LocalDate`, `ProposedMinutes` (scheduled duration, rounded to 15), `State`
(`Open \| Accepted \| Dismissed`), `AcceptedEntryId?`. Unique `{TenantId, MeetingId, UserId}`. Created lazily when the
person's week is read and an accepted meeting of theirs has ended. "Confirmed by minutes", "withdrawn" and
"conflict" are **derived at read time** from the current minutes attendance, never stored (D8).

### 4.6 `WorkCategory` (the Slim form — 5 user fields)

| Field | Type | Req. | Rule |
|---|---|---|---|
| `Code` | string | Yes | `^[A-Z][A-Z0-9_]{1,31}$`, tenant-unique, **immutable** after create |
| `LabelText` | string? | Cond. | tenant-typed label, shown as typed; required unless `LabelResourceKey` is set |
| `Description` | string? | No | max 500 |
| `CountsAsWork` | bool | Yes | default `true` (D10) |
| `SortOrder` | int | Yes | 0…999 |
| `LabelResourceKey` | string? | System | set only by `InstallRecommendedWorkCategories`; resolved from resx in 7 languages — the `TaskClosureOutcome` `LabelResourceKey \| LabelText` mechanism (R6) |
| `IsActive` | bool | System | changed only by activate/deactivate — never deleted (D10) |

### 4.7 `TimeEntrySettings`, `LegalEntityTimeSetting` and fixed v1 limits

`TimeEntrySettings`: `TimeAdminPoolPositionId` (Guid?, the fallback approver pool — D6).

`LegalEntityTimeSetting`: `LegalEntityId` (unique per tenant), `TimerEnabled` (bool), `ChangedAtUtc`,
`ChangedByUserId`, `Reason` (required when switching **on**). **No row = timer off**, for every legal entity; there
is no country lookup (R7). A person whose legal entity cannot be resolved has no timer. Manual entry works whatever
the switch says.

Fixed in v1 (constants; a tenant setting may come later):

| Limit | Value | Behaviour |
|---|---|---|
| Flag threshold per day | 660 min (11 h — Law 4857 Art. 63 daily maximum) | the day is saved and submitted, but flagged to the approver and the person (A3) |
| Implausible per day | 960 min (16 h) | a manual save that would exceed it is refused; timer drafts may exceed it but block submit until corrected (A3) |
| Edit window | current week + 4 previous weeks | older unsubmitted weeks need a time-admin reopen with reason (A9) |
| Timer draft rounding | per task-or-category per local day: sum → nearest 15 min (ties up); a sum under 8 min writes no row | the grid says "too short to count" (A2) |

---

## 5. Repo Scope

MOD-0357 precedent: a module of another domain inside `Diten.Platform`, authorised by its own pack and an ADR
(ADR-004). HCM `domain-config.md` lists this pack under In-Scope Modules with the deviation.

### 5.1 T1 — backend (Diten.Platform)

- `Domain/Entities/TimeEntry/**` (8 entities) + `Domain/Enums/TimeEntry/**`
- `Domain/Repositories/ITimeEntryRepositories.cs`
- `Application/Contracts/ITaskTransitionObserver.cs`, `Application/Contracts/ITaskSpentTimeSource.cs`
- `Application/Features/TimeEntry/**` — Commands / Queries / Handlers (CommandHandlers, QueryHandlers) / Validators /
  `TimeEntryModels.cs` / Services / Adapters / Providers / SelfRegistration / BackgroundJobs
- `Infrastructure/Persistence/Repositories/TimeEntryRepositories.cs`
- `Infrastructure/Persistence/Schema/PlatformCollections.cs` — **add** the eight `time_entry_*` constants
- `Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs` — **extend only**
- `Application/DependencyInjection.cs` — one additive block
- `API/Controllers/TimeEntryController.cs`
- `tests/Diten.Platform.Application.Tests/TimeEntry/**` (+ HTTP tests in the existing TestHost project)
- **Enumerated edits outside the feature folder (each reviewed on its own):**
  1. `Infrastructure/Persistence/Repositories/TaskRepositories.cs` — **one** call to `ITaskTransitionObserver` after
     `RecordAsync` inside `RecordIfMovedAsync` (:219), passing the new `TaskTransition.Id`. No other line changes.
  2. MOD-0024 read sites of `SpentHours` switch to `ITaskSpentTimeSource` (D7), measured 2026-09-29:
     `TaskItemMapper.cs:70`, `Providers/TaskWorkItemProvider.cs:1081,1231`, `Services/TaskLifecycleService.cs:146`,
     `Services/WorkReportTally.cs:357,406,568`, `Infrastructure/.../WorkReportRepository.cs:386,706,773`,
     `Services/WorkReportExportSerializer.cs:55`. `TaskItem.SpentHours` stays on the entity (always 0, never written);
     removing it is a MOD-0024 decision, not this pack's.
  3. `Providers/TaskWorkItemProvider.cs` — fills `TimerState` (`WorkAggregationModels.cs:339`) per reader and emits
     the `timeTracking` capability with the `timeEntries` data block the WC-1 contract already declares
     (`fixture-contract.js:200`) (R10). **Stop rule (no Web change in T1):** the existing detail card
     (`renderTimesheet`, `app.js:4556`) starts drawing once the capability is declared; T1 checks on a live page that
     it shows the real figures and offers no control that writes nowhere. If it would, T1 emits `TimerState` and
     `timeEntries` but holds the capability flag until T2, and says so in its report.
  4. `Application/Features/WorkingHours/IWorkingHoursProvider.cs` + `WorkingHoursProvider.cs` — additive
     `TargetMinutes` on `WorkingDay`; a half-day holiday gives half the target instead of a full working day
     (`WorkingCalendar.cs:129-132`) (D13, A8 — T1, CT's lane).
  5. `Domain/Entities/Tenant.cs` — additive `DefaultDailyTargetMinutes` (default 480), the tenant ring of D13.
  6. `Application/Features/Workflow/**` — additive per-definition option **"comment required on reject"**, enforced
     server-side in the reject path (today `RejectWorkflowTaskValidator.cs:14` only caps length); default off so no
     existing definition changes; the timesheet definition sets it on (R5).

### 5.2 T2 — frontend (Diten.Web) and Task Center actions

- `Controllers/TimeEntryController.cs` (proxy, `[Authorize]`, `/TimeEntry/api/*` → `{GatewayUrl}/api/v1/time-entry/*`)
- `Views/TimeEntry/Index.cshtml` (My Timesheet grid) + `_IndexL10n.cshtml` + `TimeEntryIndex.cs`
- `Views/TimeEntry/Approvals/**` (DataTable v2, read-only list + read-only week detail with flagged days)
- `Views/TimeEntry/Categories/**` (Golden Reference **Slim** set)
- `Views/TimeEntry/Settings/**` (form page: time-admin pool, per-legal-entity switch list with reason)
- `Views/Shared/_TimerChip.cshtml` + **one** include line in `Views/Shared/_LayoutTenantShell.cshtml`
- `wwwroot/assets/js/TimeEntry/**`, `wwwroot/assets/js/shared/timer-chip.js`
- `Resources/Views/TimeEntry/**.{en,tr,fr,es,zh,ar,ru}.resx`; `Resources/SharedResource.{7}.resx` — `Nav.Module.TIMEENTRY`,
  `Nav.Page.{MY_TIMESHEET,TIME_APPROVALS,TIME_CATEGORIES,TIME_SETTINGS}`
- `wwwroot/assets/css/backbone-custom.css` — `.time-entry-*` classes only (FG-003, no inline style)
- Task Center: `wwwroot/assets/js/WorkCenterNext/app.js` (timer controls; **remove** `foldTimer` and the mock
  `item.timesheet` loop; the detail card reads the real `timeEntries` block), `fixture-contract.js` (action codes
  `startTimer` / `stopTimer`), the resx keys `ToastTimerStarted`, `TimerFollowsStatusHint` (+ any other mock-timer
  key) **removed in all 7** `WorkCenterNextIndex.*.resx`
- Platform, T2 side: `TaskWorkItemProvider` adds `startTimer` / `stopTimer` actions and declares
  `time-entry.timesheets.update` in `RequiredActionPermissions` (connect-module-to-workcenter Ö8)

### 5.3 T3

`TimesheetReminderJob`, notification templates (7 languages), person-only minutes-conflict notification.

---

## 6. Protected Paths

- `services/Diten.HcmService/**`, `services/Diten.HumanCapitalService/**` — untouched (ADR-004).
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/TaskItem.cs` — **no field added**; `SpentHours`
  keeps its declaration.
- `Application/Features/Tasks/**` and `Infrastructure/.../TaskRepositories.cs` — only the enumerated edits in §5.1 (T1)
  and §5.2 (T2). Anything else is a new pack revision.
- `Application/Features/Meetings/**` — untouched; attendance is read through `ITimeEntryMeetingGateway`.
- `Application/Features/Workflow/**` — only the one additive option in §5.1 item 6.
- `Application/Features/WorkingHours/**` — only §5.1 item 4; `TaskCalendarGuardTests` must stay green.
- `services/Diten.AuthService/**` — the explicit-grant enrolment is a CT prerequisite (§23), not done by this pack.
- `gateway/Diten.ApiGateway/**/ocelot.json` — CT only (§15).
- `.antigravity/**` — including the PKS-001 namespace table (CT, §26 C-5).
- PPM: `services/Diten.PpmService/**`, `execution/domains/portfolio-delivery/**`, any `*ppm*` file, the MOD-0117
  registry row and registry identity rule :41 (§24).
- Blueprint `.xlsx`; other domains' `execution/domains/**`; `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.

---

## 7. Dependencies

| Dependency | Use | Status (measured 2026-09-29) |
|---|---|---|
| MOD-0024 transition log | Timer start/stop hook | **Exists** — `TaskRepositories.cs:193` → `RecordIfMovedAsync` (:219), the only place a lifecycle event is decided |
| MOD-0024 plan block (`PlannedDate`, `PlannedStartAt`, `PlannedDurationMinutes`) | D9 fill-in | **Exists** (MOD-0024 create-runtime pack §22) |
| MOD-0023 start / cancel / read | D6 approval, A6 withdraw | **Exists** — `StartWorkflowInstanceCommand`, `CandidatePrincipalIds`, `IApprovalSourceResolver`, cancel via MOD-0023's own command. No decision event → pull-based finalizer (R4). Reject reason → T1 option (R5) |
| MOD-0357 attendees + minutes | D8 | **Exists** — `MeetingAttendee.InvitationResponse`; `PublishMinutesHandler.SyncAsync` writes attendance (`MinutesCommandHandlers.cs:271-288`), the correction path re-runs it (`:389`) |
| MOD-0288 seats / positions | D6, D12 | **Exists** — `ITaskSeatDirectory.ActiveForUserAsync` orders primary first (`TaskSeatDirectory.cs:92-97`); `Position.ReportsToPositionId` (`Position.cs:10`); `HoldersOfAsync` |
| `IWorkingHoursProvider` | local date, windows, holidays, target | **Exists**; time zone is the tenant's (`WorkingHoursProvider.cs:62`) — the v1 rule (R3); target + half day added in T1 (D13) |
| Working Calendar (`CAND-CAP-0010`) | holidays read-only | **Exists** |
| `ConsumedEventStore.ExecuteOnceAsync` | D7 idempotency | **Exists**, typed on `EventEnvelope<T : IIntegrationEvent>` — the finalizer wraps each outcome in an envelope with a deterministic id `(workflowInstanceId, outcome)` |
| Audit (`AuditBehavior` + `IAuditableCommand`) | every state change incl. minimisation | **Exists** |
| `IDataExportAuditWriter` | access log for per-person reports (T4) | **Exists** (BL-347) |
| `INotificationEventDispatchAdapter` | submitted / approved / rejected / auto-closed / reminder | **Exists** |
| Hangfire seam | midnight close, decision sweep, reminder | **Exists**; flags may be off on live → read-time paths carry correctness |
| `ExplicitGrantOnlyPermissions` (AuthService) | D11 | **Exists**; enrolment of the two keys = CT prerequisite (§23) |

**Blueprint HARD dependencies of MOD-0280** (`Dependencies_Normalized`) and why none blocks slice 1:

| Blueprint dependency | What it is | Slice 1 |
|---|---|---|
| MOD-0003 Data Contract Registry | the governance registry of cross-system data contracts (Blueprint SoR "Data contracts", Control-Plane) — registry row `planned / missing` | **Not required.** Slice 1 has no cross-service contract: every call is in-process through the §3.3 ports. `TIME-LEAVE-BUNDLE` becomes a registered contract when a second service reads it — the ADR-004 extraction or a PPM read of entries over HTTP. Becomes blocking at that point |
| MOD-0037 Integration Monitoring & Reconciliation | monitoring of external integrations | **Not required** — no external integration in this slice |
| MOD-0017 SSO / MFA | login | consumed as-is (tenant login) |
| MOD-0032 API Gateway | routing | consumed; routes are a CT prerequisite for T2 (§15) |
| Gate "Core HR / Employee Master" (MOD-0251) | employee master | **Not required** — slice 1 is keyed by UserId; linking to the employee master is an ADR-004 revisit trigger |

---

## 8. Runtime Constraints

1. Mongo, tenant-scoped, **soft delete everywhere — nothing in this module is hard-deleted**; tenant-first compound
   indexes on every `time_entry_*` collection; `TenantId` from the server context only (`TenantRepository<T>`).
2. **Server clock only.** The browser never sends a start or stop instant; the timer chip only renders elapsed time
   from the server's `StartedAtUtc`.
3. **Local date = `WorkingHoursResult.LocalDateOf`**, which in v1 uses the **tenant's** time zone — the same rule as
   the calendar engine (R3). Known limit: a tenant holding legal entities in different zones (TR + CH) gets one zone;
   a per-legal-entity zone is a follow-up (§20). Weeks are ISO 8601 (Monday start, week-year rules — 2027-01-01
   belongs to `2026-W53`).
4. **Correctness never depends on a scheduler.** Midnight close and the approval finalizer run at read time as well
   as in their jobs (D3, D7).
5. **Idempotency:** timer start from a transition keyed by `TaskTransition.Id`; approval start keyed by
   `timesheet-week:{tenant}:{weekId}:{revision}`; finalization keyed per (workflow instance, outcome); meeting
   suggestion keyed by (meeting, user).
6. **Minimisation, not deletion (D4, R8):** when a week is approved the finalizer clears `StartedAtUtc` /
   `StoppedAtUtc` on that week's segments and sets `MinimisedAtUtc`; durations, local date, task/category and
   outside-hours minutes stay. It is an audited update.
7. **No productivity numbers.** No API field, export or screen shows recorded ÷ estimate, recorded ÷ target, a
   ranking or a score (reporting decisions §3 ban carries over; Z-4). Target vs recorded is shown to the **person**
   as two durations, never a percentage. The 11-hour flag is a plausibility marker, not a score.
8. **No live "who is running a timer" view.** `TimerState` and timer data are projected **per reader**: another
   person's item always says `notApplicable` to you (D11).
9. Browser → `/TimeEntry/api/*` → Gateway 5000 → Platform 5057. 7-language l10n (tenant). No inline CSS.
10. Every self-registered page/action/permission comes from the manifest; nothing hand-seeded.

---

## 9. Layout & Shell Contract

- `shell: tenant` → `Layout = "_LayoutTenantShell";` written **explicitly** in every `.cshtml`.
- Routes: `/TimeEntry` (My Timesheet, current week) and `/TimeEntry?week=2026-W40`, `/TimeEntry/Approvals`,
  `/TimeEntry/Approvals/{weekId}`, `/TimeEntry/Categories`, `/TimeEntry/Settings`.
- Nav (manifest `IsNavigationVisible`): `MY_TIMESHEET` visible; `TIME_APPROVALS` visible (permission-gated);
  `TIME_CATEGORIES` and `TIME_SETTINGS` hidden, reached from the Settings entry (self-registration §2a: tab ≠ page).
  Manifest domain `Human Capital` (→ existing `Nav.Domain.HUMANCAPITAL`). Catalog module, entitlement-gated.
- UAS-001: a user without the page permission gets the single explanation panel, no skeleton, no redirect.
- The timer chip renders in the tenant navbar only when the caller holds `time-entry.timesheets.update` **and** the
  timer is on for their legal entity; otherwise the partial renders nothing.

---

## 10. Backend File Convention

Golden Reference folder/naming, same as MOD-0357 §10:

```text
Features/TimeEntry/
├── Commands/            sealed records, one per file
├── Queries/
├── Handlers/
│   ├── CommandHandlers/ {Verb}{Object}Handler.cs   (no Command suffix)
│   └── QueryHandlers/   Get{Object}{List|ById|Week}Handler.cs
├── Validators/          {Verb}{Object}Validator.cs
├── Services/            TimerService, TimesheetApprovalService, ApproverResolver, TimesheetFinalizer, WeekCalendar
├── Adapters/            TaskGatewayAdapter, MeetingGatewayAdapter, OrgGatewayAdapter   (the ONLY place MOD-0024/0357/0288 types appear)
├── Providers/           TimesheetApprovalSourceResolver
├── BackgroundJobs/      TimerMidnightCloseJob, TimesheetDecisionSweepJob (T1), TimesheetReminderJob (T3)
├── SelfRegistration/    TimeEntryManifestProvider.cs
└── TimeEntryModels.cs   ALL DTOs in one file
```

Repositories inherit `TenantRepository<T>`; collections snake_case plural with `time_entry_` prefix; controller thin,
`CustomBaseController`, `[Authorize]` + `[HasPermission]`, `Response<T>`. No new base entity, repository base,
tenant, audit, event or job infrastructure.

---

## 11. Frontend File Contract

- **Categories** — Golden Reference **Slim**, file-for-file from `Views/DevEnablement/GoldenReferenceSlim/`
  (`Index`, `_Filter`, `_DataTable`, `_IndexL10n`, `_CreateEditOffcanvas`, `_DetailsQuickView`, marker class, JS, 7 resx).
  `data_mode: client`, max 50 rows. No delete/bulk-delete action — activate/deactivate instead (D10).
- **Approvals** — DataTable v2 list (`data_mode: server`), read-only, flagged-day badge; row opens
  `/TimeEntry/Approvals/{weekId}` (read-only grid). No edit control anywhere on the approver side (D6).
- **My Timesheet** — **not a DataTable**: a week grid (rows = task/category, columns = Mon…Sun, footer = day totals,
  day target, holiday marker, "outside working hours" marker, 11-hour flag, "too short to count" hint, suggestion
  strip). Status banner (Draft / Submitted — withdraw / Approved / Returned with reason / Correction in progress /
  Outside edit window — ask a time admin). Deliberately outside the Slim/Compact contract; the verifier is not run on
  it (§17 declares the expected skip).
- **Settings** — plain form page.
- **Timer chip** — navbar partial; shows the running task/category and elapsed time; stop and switch from the chip.

---

## 12. Validation Rules

| Field / action | Rule | Server-side check | Reason code |
|---|---|---|---|
| `TimeEntry.DurationMinutes` | multiple of 15, 15…960 | validator | `TIME_ENTRY_STEP_INVALID` |
| `TimeEntry.LocalDate` | inside the week; ≤ local today (tenant zone) | handler (provider date) | `TIME_ENTRY_FUTURE_DATE` |
| `TimeEntry` target | exactly one of task/category; task readable by the person; category active | handler via gateway | `TIME_ENTRY_TARGET_INVALID` / `TIME_ENTRY_CATEGORY_INACTIVE` |
| day total (manual save) | > 960 refused; > 660 saved and flagged | handler | `TIME_ENTRY_DAY_IMPLAUSIBLE` |
| any write to a week | revision `Status == Draft` **and** (inside the edit window **or** reopened) | handler | `TIMESHEET_WEEK_NOT_OPEN` / `TIMESHEET_WEEK_OUTSIDE_EDIT_WINDOW` |
| submit | ≥ 1 entry; no day > 960; approver resolvable | handler | `TIMESHEET_EMPTY_WEEK` / `TIME_ENTRY_DAY_IMPLAUSIBLE` / `TIMESHEET_NO_APPROVER` |
| withdraw | own week, `Submitted`, no decision recorded in MOD-0023 | handler + MOD-0023 read | `TIMESHEET_WITHDRAW_TOO_LATE` |
| correction | reason required; only from an in-force approved revision; one open correction per week | validator + unique index | `TIMESHEET_CORRECTION_REASON_REQUIRED` / `TIMESHEET_CORRECTION_ALREADY_OPEN` |
| reopen (time admin) | reason required; only a `Draft` week older than the edit window | validator + handler | `TIMESHEET_REOPEN_REASON_REQUIRED` / `TIMESHEET_REOPEN_NOT_NEEDED` |
| timer start (control) | task `InProgress` and held by caller, or active category; timer on for caller's legal entity | handler via gateways | `TIMER_TASK_NOT_HELD` / `TIMER_TASK_NOT_IN_PROGRESS` / `TIMER_DISABLED_FOR_LEGAL_ENTITY` |
| legal-entity switch on | reason required | validator | `TIMER_SWITCH_REASON_REQUIRED` |
| `WorkCategory.Code` | pattern, tenant-unique, immutable | unique index + validator | `WORK_CATEGORY_CODE_DUPLICATE` / `WORK_CATEGORY_CODE_IMMUTABLE` |
| `WorkCategory` label | `LabelText` or `LabelResourceKey` | validator | `WORK_CATEGORY_LABEL_REQUIRED` |
| MOD-0023 reject of a timesheet | comment non-empty (definition option) | MOD-0023 reject path | MOD-0023's own validation code |
| every write | `expectedVersion` | conditional write | `TIMESHEET_CONCURRENCY_CONFLICT` |

Timer → draft: segments are summed per task-or-category per local day; the sum is rounded to the nearest 15 minutes
(ties up); a sum under 8 minutes writes no row and the grid shows "too short to count" (A2).

---

## 13. Failure Path to Verify

- **Two timer starts for one person at the same instant** (two tabs, two tasks) → exactly one running segment in
  Mongo; the second request completes as a switch or returns 409, never a second running row (partial unique index).
- **Transition replayed** (same `TaskTransition.Id` observed twice) → one segment, not two.
- **Hook failed** (timer write threw after the task committed) → task transition stands; the next read reconciles:
  a running segment on a task that is no longer `InProgress` or no longer held by the person is closed at the
  invalidating transition's time, `StopReason = Reconcile`.
- **Task deleted / became unreadable while its timer runs** → closed at read time, `Reconcile`.
- **Timer left running overnight, jobs disabled** → any read after local midnight returns the segment closed at
  local midnight with `LocalMidnight`; the next morning's first page load shows the one-time banner.
- **Legal entity switch turned off with timers running** → those segments close at switch time, `SwitchedOff`;
  manual entry still works. **No switch row** → timer refused; task Start still succeeds.
- **Timer minutes land on a day of a submitted/approved week** → segment kept, no draft row written, the person
  sees "time outside an open week — request a correction"; never silently dropped, never auto-added.
- **Write to a submitted week / a week older than the edit window** → 409 with the reason code, nothing written.
- **Future date / 20-minute step / 17 hours in a day** → 400 with the reason code, nothing written.
- **Day of 12 hours** → saved, submitted, flagged to the approver; not refused.
- **Empty week submitted** → 409 `TIMESHEET_EMPTY_WEEK`.
- **Withdraw after the approver decided** (approved but not yet finalized, or rejected) → 409
  `TIMESHEET_WITHDRAW_TOO_LATE`; the finalizer applies the decision.
- **No approver resolvable** (no primary seat, chain vacant to the top, pool empty) → 409 `TIMESHEET_NO_APPROVER`;
  the week stays Draft; **never auto-approved**.
- **Resolved approver is the person** (self-reporting seat, delegated back to them) → walked past on resolution;
  if MOD-0023 still records the person as the approver, the finalizer refuses to finalize and flags the week.
- **Reject without a comment** (Task Center card or approvals page) → refused by MOD-0023 (definition option).
- **Correction rejected** → the correction returns to Draft; the original stays approved and in force; totals
  unchanged.
- **Finalizer runs twice for the same approval** → same `TaskTimeTotal` values (recomputation), segments already
  minimised stay minimised, one audit entry per effective change.
- **Concurrency** (two saves of one week) → 409, "the record changed meanwhile; reload and retry".
- **Unauthorized** — missing key → 403; approver opens a week not routed to them → 404; another tenant → 404, no
  metadata; approver reads a draft → 404 (D11).

---

## 14. Authorization Convention

```text
Policy:     [Authorize]   (tenant user)
Permissions (PKS-001, 3 segments, namespace time-entry owned by MOD-0280):
  time-entry.timesheets.read        own timer, weeks, suggestions
  time-entry.timesheets.update      own drafts, timer, submit, withdraw, correction request
  time-entry.approvals.read         approvals page: submitted weeks routed to the caller
  time-entry.weeks.reopen           time admin: reopen a week older than the edit window, with reason
  time-entry.categories.manage      category catalogue
  time-entry.settings.manage        time-admin pool, legal-entity timer switch
  time-entry.team-totals.read       T4 — ExplicitGrantOnly (D11)
  time-entry.person-reports.read    T4 — ExplicitGrantOnly (A7); every access logged via IDataExportAuditWriter
Decisions:  MOD-0023's own platform.workflow.tasks.approve / .reject (unchanged)
```

- `manage` and `reopen` are Tier-3 actions registered by this pack (PKS-001 §5).
- Module attribution: non-service namespace → module `time-entry` (`PermissionModuleAttribution.cs`); the manifest
  `ModuleCode: "time-entry"` says the same. Scope Tenant (no `/Platform/…` route).
- The two T4 keys are minted **with** their endpoints (T4), not before. Their enrolment in
  `ExplicitGrantOnlyPermissions.cs` is a **CT prerequisite listed in T1** (§23), so no later manifest can publish them
  into the entitlement sync first (the BL-392 lesson).
- Visibility (D11): the person sees all of their own; the approver sees **submitted** weeks routed to them, never
  drafts, never another team's; nobody sees who is running a timer.

Notification events (manifest-declared): `time-entry.week.submitted`, `time-entry.week.approved`,
`time-entry.week.rejected`, `time-entry.week.withdrawn`, `time-entry.timer.auto-closed`; T3:
`time-entry.week.reminder`, `time-entry.meeting.minutes-conflict`.

---

## 15. Gateway / API Routing Decision

```text
Decision: gateway change REQUIRED → Control Tower (protected path). Needed before T2 goes live; T1 does not need it.
```

Routes needed in `gateway/Diten.ApiGateway/ocelot.json`, placed before any catch-all, next to the `/api/v1/meetings`
pair (:1678-1703):

| Upstream | Downstream | Host | Methods |
|---|---|---|---|
| `/api/v1/time-entry` | `/api/v1/time-entry` | `localhost:5057` | GET, POST, PUT, PATCH, DELETE, OPTIONS |
| `/api/v1/time-entry/{everything}` | `/api/v1/time-entry/{everything}` | `localhost:5057` | GET, POST, PUT, PATCH, DELETE, OPTIONS |

One prefix on purpose: on extraction the pair is re-pointed to the new service and nothing else changes.

---

## 16. Acceptance Criteria (per decision; each maps to a test in §17)

### Governance
- [ ] Identity `MOD-0280-FU01`, parent `MOD-0280`; both preflights exit 0 (reproduce at T1 start).
- [ ] No file in `services/Diten.HcmService/**`, `services/Diten.HumanCapitalService/**`, `Features/Meetings/**`,
      AuthService, `ocelot.json` or PPM paths changes in T1.
- [ ] Outside `Features/TimeEntry/**`, T1 changes exactly the enumerated edits of §5.1.

### D1 — placement (ADR-004)
- [ ] All eight collections carry the `time_entry_` prefix; every document has `TenantId` + `UserId`; no document
      carries an Employee id.
- [ ] Source guard: no type from `Features/Tasks`, `Features/Meetings` or `Features/TenantOrganization` is referenced
      under `Features/TimeEntry/` outside `Adapters/`.

### D2 — timer
- [ ] Task `Start`/`Resume` by the holder starts the holder's timer on that task; the same transition id twice → one
      segment.
- [ ] Start/stop timer controls on an `InProgress` task never create a `TaskTransition` and never change `Lifecycle`.
- [ ] Starting a timer on task B while A runs stops A (`Switch`) and returns an undo token; undo stops B and starts a
      new segment on A from now (no backdating).
- [ ] `Waiting`, `SubmittedForReview`, `Completed`, `Cancelled`, `Released`, `Returned`, `Reassigned`, or any write
      that leaves `InProgress` / changes the holder, stops the timer.
- [ ] Mongo holds at most one `IsRunning` segment per (tenant, user) under concurrent starts.
- [ ] T1 projection: `TimerState` is `running` only for the reader's own running item, `inactive` for the reader's
      own other `InProgress` items, `notApplicable` otherwise; `paused` is never emitted (BL-237); the `timeEntries`
      block is present exactly when `timeTracking` is declared.

### D3 — forgotten timer
- [ ] A segment never spans local midnight (tenant zone): the job **or** the first read after midnight closes it at
      00:00 local.
- [ ] Minutes outside the day's working window are counted in `OutsideWorkingMinutes` and flagged on the grid.
- [ ] Exactly one morning notification per auto-closed person-day (idempotent); none when jobs are off (the banner
      replaces it).
- [ ] Timer output is written only as draft rows, with the A2 rounding and the 8-minute floor.

### D4 — record shape and minimisation
- [ ] An entry has date + minutes + task-or-category + source; the screen and the pack state "effort allocation, not
      a statutory working-time record".
- [ ] After approval every segment of that week has `StartedAtUtc == null`, `StoppedAtUtc == null`,
      `MinimisedAtUtc != null`, its duration intact, **no document deleted**, one audit entry for the minimisation.

### D5 — period and lifecycle
- [ ] Week = ISO week of the tenant-local date; 15-minute steps; future dates refused; > 960 min/day refused on save
      and blocks submit; > 660 min/day flagged to the approver; writes only in Draft and inside the edit window
      (current + 4 previous weeks) unless reopened with a reason (server-checked).
- [ ] Empty week cannot be submitted; submit locks; the person can withdraw until the approver's decision is
      recorded; approve locks; reject needs a comment (MOD-0023) and returns to Draft with the reason visible.
- [ ] Correction = new revision with mandatory reason, through approval again; the original stays approved and in
      force until the correction is approved, then becomes `Superseded`.
- [ ] Every state change writes an audit entry via `AuditBehavior` / `IAuditableCommand`.

### D6 — approver
- [ ] Candidates = holders of the primary seat's `ReportsToPositionId`, resolved and stored at submit; vacant →
      next level up; the person is never a candidate; none found → time-admin pool; pool empty → submit refused.
- [ ] The approval runs as a MOD-0023 instance with those `CandidatePrincipalIds` on a definition with "comment
      required on reject"; no local approve path exists.
- [ ] The approver can reject but has no edit endpoint or control.

### D7 — SpentHours
- [ ] After approval, `TaskTimeTotal.ApprovedMinutes` equals the sum of in-force approved entries for the task;
      MOD-0024's projection, work report, export and remaining-estimate read that value via `ITaskSpentTimeSource`.
- [ ] Submitted-not-approved minutes are returned separately and never added to the approved figure.
- [ ] Only the finalizer writes `time_entry_task_totals`; replaying it writes the same values; it runs on week read,
      on the approvals page and in the sweep job.
- [ ] Preflight query (read-only) run and its result recorded before T1 merges:
      `db.task_items.countDocuments({ SpentHours: { $exists: true, $ne: 0 } })` — expected 0.

### D8 — meetings
- [ ] No entry is created from a meeting without the person's action.
- [ ] After an accepted meeting ends, the person's week shows one suggestion at scheduled duration with
      "attended / did not attend"; declined or pending invitations produce nothing.
- [ ] Minutes `Present` → "confirmed by minutes" badge; `Absent`/`Excused` → an unaccepted suggestion disappears, an
      accepted row is flagged to the person only; a minutes correction changes the result on the next read.

### D9 — calendar plan
- [ ] "Fill from plan" returns ghost values only; nothing is written until the person accepts a row; ghost values
      appear only for (date, task) cells with no timer segment and only for dates ≤ local today; accepted rows carry
      `Source = Plan`.

### D10 — categories
- [ ] Categories are tenant-scoped, deactivated not deleted, carry `CountsAsWork`; labels come from
      `LabelResourceKey` (7 languages) or `LabelText`; there is no free-text "Other"; leave/absence cannot be created
      as a category; holidays show read-only on the grid from the working calendar.

### D11 — visibility
- [ ] No endpoint returns another person's draft, running timer or segments; the approver sees submitted weeks
      routed to them only; T4 keys are explicit-grant only and per-person report reads are access-logged.

### D12 — switch
- [ ] With no setting row (any country) or the switch off, timer endpoints refuse with
      `TIMER_DISABLED_FOR_LEGAL_ENTITY`, the task Start still succeeds without a timer, the chip is absent, manual
      entry works; switching on requires a reason and is audited.

### D13 — working hours
- [ ] The grid's day target comes from `IWorkingHoursProvider` (`TargetMinutes`), not from the 09:00–18:00 window; a
      half-day holiday shows half the target; `TaskCalendarGuardTests` stays green.

### l10n / platform links
- [ ] All TimeEntry resx sets share one key set across 7 languages; `Nav.*` keys present in 7 `SharedResource` files;
      mock-timer keys gone from all 7 `WorkCenterNextIndex` files.
- [ ] The 10-row platform-links table (§19.3) is filled with measured values at the end of T2.

---

## 17. Test Expectations

HTTP-level (TestHost, as `TaskAssignmentWriteGuardHttpTests`) against **real Mongo** for every index-dependent rule;
every guard gets a sabotage run (remove the rule, see red, restore, see green) recorded in the WP report.

| # | Decision | Test | Sabotage |
|---|---|---|---|
| T-01 | D1 | Source-scan guard: `Features/TimeEntry` outside `Adapters/` references no MOD-0024/0357/0288 repository or collection | add one reference → red |
| T-02 | D2 | Start task (holder) → one running segment; replay the same transition → still one | drop the `StartTransitionId` unique index → red |
| T-03 | D2 | **Race:** 20 parallel `POST /timer/start` for one user, alternating two tasks → Mongo `count(IsRunning == true) == 1` | drop the partial unique index → red |
| T-04 | D2 | Each stop transition kind (7) closes the timer; timer controls create no `task_transitions` row | skip one kind in the observer → red |
| T-05 | D2/D11 | Team-scope projection of a subordinate's running task says `notApplicable` to the manager | project the holder's state → red |
| T-06 | D3 | Segment started 23:30 local, read at 00:10 with jobs **off** → closed at 00:00 local, `LocalMidnight` | remove the read-time close → red |
| T-07 | D3/D5 | **DST week boundary:** tenant zone `Europe/Zurich`; timer 2026-10-25 00:30 → 03:30 local (25-hour Sunday) = 240 min on 2026-10-25 in `2026-W43`; a segment started Sunday 23:30 closes at Monday 00:00 local and stays in `2026-W43` | compute the date in UTC → red |
| T-08 | D5 | ISO week-year: an entry on 2027-01-01 lands in `2026-W53` | use calendar-year week → red |
| T-09 | D5 | Submitted week → 409; future date → 400; 20-minute step → 400; day > 960 → 400; day of 700 → saved + in `FlaggedDates`; empty week submit → 409 | remove each server check → red |
| T-10 | D5 | Correction: original stays `Approved`/in force while the correction is Draft/Submitted; on approval original → `Superseded`, totals recomputed | flip the original on submit → red |
| T-11 | D5 | Every command in §3.2 produces one audit entry | remove `IAuditableCommand` from one → red |
| T-12 | D6 | Resolver matrix: direct manager · vacant manager seat → next level · self-reporting seat → skip self · no chain → pool · empty pool → 409 | return the person as candidate → red |
| T-13 | D6 | MOD-0023 instance started with the stored candidates; idempotent on re-submit | — |
| T-14 | D7 | Approve → `ApprovedMinutes` = sum; run the finalizer twice → same values; submitted minutes returned separately | increment instead of recompute → red on replay |
| T-15 | D7 | Work report `SpentHours` and projection `effort.spent` equal the approved total | read `TaskItem.SpentHours` → red |
| T-16 | D8 | Accepted + ended → one suggestion; minutes Absent → gone; accept then correct minutes to Absent → row flagged, only for the person | store the badge instead of deriving → red after correction |
| T-17 | D9 | Fill-in writes nothing; skips cells with segments; skips future dates; accepted row `Source = Plan` | write on GET → red |
| T-18 | D10 | Deactivate keeps history readable; no delete endpoint; duplicate code 409; recommended set resolves labels in 7 languages | — |
| T-19 | D11 | Approver GET on a Draft → 404; other tenant → 404; no endpoint lists other users' timers (route inventory test) | expose drafts → red |
| T-20 | D12 | Legal entity with no setting row → timer refused, task Start OK, manual entry OK; switch on without reason → 400 | default on → red |
| T-21 | D13 | Half-day holiday → half target; window unchanged | treat half day as full → red |
| T-22 | Z-4 | No response DTO field named or computed as a ratio of recorded to estimate/target (reflection scan) | add a `%` field → red |
| T-23 | D4 | Approve → segments of that week minimised (instants null, durations kept), document count unchanged, audit entry written | delete instead of clear → red on count |
| T-24 | A6 | Withdraw while pending → Draft, MOD-0023 instance cancelled; withdraw after approve (before finalization) → 409 | allow withdraw regardless → red |
| T-25 | A9 | Week 5 back → 409 outside window; time-admin reopen with reason → editable until submitted; reopen without reason → 400 | skip the window check → red |
| T-26 | R5 | Reject a timesheet approval with an empty comment → refused by MOD-0023; a task approval (option off) still rejects without a comment | enforce globally → task test red; skip enforcement → timesheet test red |
| T-27 | A2 | Segments 3 + 4 min same task/day → no row, "too short"; 7 + 8 min → 15; 22.5 min → 30 | truncate instead of round → red |

Plus: build PASS (Platform, Web, gateway once routed); `verify_datatable_page.py --area TimeEntry --module Categories
--reference slim` and `--module Approvals` (the grid page is expected to be skipped by the verifier — declared here,
before the run); RESX parity PASS ×7; `NavL10nContractTests` green; browser smoke of the whole loop (start task →
timer → stop → draft → submit → approve in Task Center → task effort shows the approved figure) with a **second real
user** as approver.

---

## 18. Ready-for-dev Checklist

- [x] Every §25 question answered by CT (2026-09-29).
- [x] Every §26 conflict resolved or accepted by CT; placement recorded in ADR-004.
- [x] HCM `domain-config.md` In-Scope Modules lists this pack with the deviation; HCM `README.md` lists it.
- [x] Golden Reference Slim read for Categories; frontmatter complete; `Layout = "_LayoutTenantShell"` per file (§9).
- [x] Validation rules per field (§12); ≥ 4 failure paths (§13 lists 22); permission list + policy + actor type (§14).
- [x] Gateway routing decision explicit (§15, CT prerequisite for T2).
- [x] Acceptance criteria testable and mapped to tests (§16 ↔ §17).
- [x] Slicing approved by CT (§23).
- [ ] **Per slice, before its code:** the CT prerequisites in §23 done; for T2 the connect-module-to-workcenter
      deliverables (field mapping §19.2, capability list, action list, permission keys vs `RequiredActionPermissions`,
      one real item end-to-end).

---

## 19. Implementation Notes

### 19.1 Timer mechanics (D2, D3)

- **Start** — two doors, one service: (a) the observer sees `Started`/`Resumed` with `ActorUserId == current holder`
  and the timer is on for the holder's legal entity → start on that task (switching any other running segment);
  (b) `POST /timer/start` on a task the caller holds in `InProgress`, or on an active category.
- **Stop** — state-based, not kind-based: after any observed write, if the holder's running segment points at this
  task and the task is no longer `InProgress` or no longer held by that person, stop it. This also covers
  `TaskTransitionKind.Unknown` writes.
- **Switch** — stop the running segment (`Switch`), start the new one, return `{switchToken}` valid for 60 s.
- **Midnight** — segments are cut at local midnight (tenant zone); the day after, one notification ("your timer ran
  until midnight — check yesterday"). No automatic continuation after midnight (shift work is out of scope).
- **Reconcile** — on every timer or week read, the running segment is checked against the task gateway.
- **Draft rows** — per task-or-category per local day: sum segment durations, round to nearest 15 (ties up), skip
  under 8 minutes (A2).

### 19.2 Task Center field mapping (connect-module-to-workcenter §2)

| Value in MOD-0280 | What the Task Center shows | When absent |
|---|---|---|
| reader's running segment on this task | `timerState: running` (T1), chip + card stop control (T2) | `inactive` (own, InProgress) / `notApplicable` (anything else, other people's items) |
| `timeTracking` capability (T1) | timesheet card on the task detail | not declared — card not drawn (no "0 h" card) |
| `timeEntries` block (T1): reader's draft minutes, approved total, submitted total | card figures | block omitted with the capability |
| `effort.spent` (existing `taskContext` block) | approved total ÷ 60 | 0 until a week with this task is approved — shown as "no approved time yet", not "0 h" |
| actions `startTimer` / `stopTimer` (T2) | card buttons | withheld when the switch is off, the task is not the reader's, or not `InProgress` |

`timeEntries` is already declared by the WC-1 contract (`fixture-contract.js:200`) and only not emitted by the
backend — the same situation MOD-0357 S1 met with `relatedRecords`; emitting a declared block is not a new contract
field (R10).

### 19.3 Platform links table (filled with measured values at the end of T2)

| # | Link | Planned value |
|---|---|---|
| 1 | Module catalog | `time-entry`, self-registered catalog module, `IsTenantAssignable: true`, `IsBaseline: false` (entitlement-gated, A4) |
| 2 | Domain | `Human Capital` → `Nav.Domain.HUMANCAPITAL` (exists) |
| 3 | Page actions | Categories: create/update/activate/deactivate; Settings: update; My Timesheet: submit/withdraw/correct; Approvals: reopen (time admin) |
| 4 | Permission keys | §14 (6 in T1, 2 in T4) |
| 5 | Audit | every §3.2 command incl. minimisation; T4 report reads via `IDataExportAuditWriter` |
| 6 | Notifications | §14 events, templates ×7 |
| 7 | Nav | `Nav.Module.TIMEENTRY`, `Nav.Page.MY_TIMESHEET/TIME_APPROVALS/TIME_CATEGORIES/TIME_SETTINGS` ×7 |
| 8 | KVKK / DSG | §22; privacy notice text is the tenant's, the product shows the "not a statutory record" line |
| 9 | Onboarding | recommended category install; time-admin pool position; per-entity switch review (all off by default) |
| 10 | Self-registration | manifest + completeness test green |

Read-only Mongo queries for the table: `db.module_catalog_items.find({ModuleCode:"time-entry"})`,
`db.permissions.find({Key:/^time-entry\./})`, `db.time_entry_timer_segments.countDocuments({IsRunning:true})`
(exact collection names confirmed at measurement time).

---

## 20. Follow-up Items / Out of Scope

Deferred, each needs its own FU / pack / backlog line:

- Leave balances, absence records, attendance, schedules/rosters (the rest of MOD-0280).
- Overtime calculation; payroll export (MOD-0279); clock terminals.
- Cost / billable rates (Z-4 "cost" use).
- Entry on someone's behalf (delegate / assistant).
- Project-manager line approval (belongs with PPM once MOD-0117 exists).
- Outlook / Google calendar import.
- Per-meeting-series rules (e.g. "always attend, auto-accept").
- Idle detection.
- Retention automation — the model already carries `LegalEntityId` on the week so per-entity retention can be added
  without migration.
- **Per-legal-entity time zone** in `IWorkingHoursProvider` (v1 uses the tenant zone — R3).
- Tenant settings for the fixed v1 limits (660 / 960 minutes, 4-week edit window).
- Whether corrections of approved weeks should also be bound by the edit window (v1: not bound).
- T4 reporting (team totals by task type / category; per-person weekly report) under the D11 keys.

---

## 21. Decision Record (Control Tower, owner's delegation, 2026-09-29)

Blueprint anchor for all: `Blueprint_Data` MOD-0280 — W-3, Placement "Domain App (HCM Time & Leave)", SoR "Time
Entry; Attendance; Schedule; Leave Balance", gate "Core HR / Employee Master; Organization, Person & Position
Directory; RBAC / ABAC Authorization; Audit Trail Service; Workflow Designer", contract `TIME-LEAVE-BUNDLE`.

**D1 · Placement — inside `Diten.Platform`, own module, extraction path (ADR-004).**
*Why:* every v1 dependency lives in Platform — task lifecycle, meetings/minutes attendance, working calendar +
`IWorkingHoursProvider`, positions/reports-to, MOD-0023, audit, outbox, Hangfire. `Diten.HcmService` has no
outbox/eventing (0 files), its `Employee` has `PersonId` but no `UserId` (`Employee.cs:7`), and its workflow-decision
endpoint is a 409 stub (`EmployeeDraftsController.cs:92-98`). Precedent: MOD-0357 (ADR-003). *Deviation:* HCM
`domain-config.md:58`; extraction path §2.4. *SAP/Oracle/Workday:* SAP CATS is a cross-application component, not
part of HR or PS — it records once and hands time to HR, PS, CO and PM; Oracle Time and Labor is its own product
feeding payroll and project costing; Workday Time Tracking sits beside the worker record. All three keep time next
to the **worker identity** and the **work objects**; here both live in Platform today.

**D2 · Timer — server-side, one per person, draft output.**
*Why:* a browser clock is lost on reload and cannot be audited; one running timer per person is the only rule that
makes "what am I doing now" unambiguous. Task Start/Resume by the holder starts it; timer-only controls exist
because there is no pause state (BL-237); leaving `InProgress` stops it; the hook sits at the single transition choke
point with the transition id as idempotency key, plus read-time reconcile. T1 fills `TimerState`
(`WorkAggregationModels.cs:339`) and emits the declared `timeEntries` block (R10). *SAP/Oracle/Workday:* all three
are duration-first; clock events (SAP time events, Oracle time devices, Workday Time Clock) are inputs that become
reviewed time, never the record by themselves — same as Z-2.

**D3 · Forgotten timer — cut at local midnight, flagged, draft.**
*Why:* "a timer left on overnight is not 14 hours of work" (Z-2). Stopping at the end of the working window would
lose real overtime; minutes after the window are therefore kept but flagged; midnight (tenant zone) closes it in a
job **and** at read time because scheduler flags may be off on live. *Comparison:* clock-based systems treat an
unmatched in-punch as an exception for the worker/manager to resolve, not as worked time.

**D4 · Record shape — date + duration (+ task or category, source); minimise, never delete.**
*Why:* start/stop instants per person are exactly the behaviour data ArGV 3 Art. 26 is about; durations are what
every consumer (task effort, capacity, cost) needs. At approval the instants are cleared by an audited update; the
segment rows and durations stay (R8). *Comparison:* a CATS record is date + hours + receiver + activity/attendance
type; an Oracle time card entry is date + hours + attributes (project, task, time type); a Workday time block is
date + hours + time type. Start/end times are optional in all three.

**D5 · Period — ISO week, 15-minute steps, draft → submit (withdrawable) → approve, correction as new revision.**
*Why:* weekly is the natural review unit for a manager; server-side open-week checks make the lock real; the
correction model keeps the approved figure in force until the correction itself is approved (the
MeetingMinutesVersion pattern: append-only, reason required). v1 limits: 660 min/day flagged (Law 4857 Art. 63),
960 min/day refused, edit window current + 4 weeks with time-admin reopen, no empty weeks (A3, A6, A9).
*Comparison:* SAP CATS statuses in process → released → approved / rejected, and a change after approval keeps the
approved original (status "changed after approval") while the new record goes through approval; Oracle and Workday
re-route a changed approved time card for approval and restrict entry periods through entry profiles.

**D6 · Approver — line manager via primary seat, through MOD-0023.**
*Why:* the manager is the person who knows whether the week is plausible; MOD-0023 owns decisions (HCM
domain-config :53). Vacant seat walks up; never self; never auto; fallback pool; approver may reject (with a
comment — R5), not edit (an edited sheet would no longer be the person's statement). *Comparison:* SAP derives the
approver from the org structure (chief position), Oracle's default approval rule is the supervisor hierarchy,
Workday's Enter Time business process routes to the manager; project-manager approval is an optional extra step in
all three — deferred.

**D7 · SpentHours — approved totals in a separate collection, joined at read; pull-based finalizer.**
*Why:* `TaskItem` is written by whole-document replace (`FindOneAndReplaceAsync`, `TaskRepositories.cs:182`); a
second writer would either be overwritten or turn every edit into a 409. Measured: `SpentHours` is always 0 — no
migration. MOD-0023 emits no decision event, so the finalizer pulls the outcome (R4). *Comparison:* SAP PS actual
work on an activity comes from transferred CATS confirmations; Oracle Project Costing and Workday Projects receive
approved time. The task never stores a typed actual.

**D8 · Meetings — suggestions only.**
*Why:* acceptance is not attendance, and minutes are the evidence. Keyed by (meeting, user); corrections are
picked up because the badge is derived from current attendance. *Comparison:* timesheet tools that read calendars
present events as suggestions to accept, not as logged time; SAP/Oracle/Workday core do not create time from
meetings.

**D9 · Calendar plan — user-triggered ghost values, `Source = Plan`.**
*Why:* a plan is an intention; letting it fill the sheet silently would make Z-5 learn from plans. *Comparison:*
Workday "auto-fill from schedule / prior week", Oracle time card templates and CATS copy-from-previous all fill
values the worker then edits; we additionally keep the source.

**D10 · Non-task work — tenant category catalogue.**
*Why:* people spend time that is not a task; a free-text "Other" becomes the biggest bucket and says nothing.
Labels use the closure-outcome resource-key-or-text mechanism (R6). *Comparison:* SAP activity/attendance types,
Oracle time types, Workday time types — tenant-configured, retired not deleted; leave/absence is a separate module in
all three.

**D11 · Visibility — own everything; approver submitted only; no live view; explicit grants for aggregates.**
*Why:* the Swiss entity makes monitoring-capable features a legal question first (reporting decisions §5).
*Comparison:* managers see team time cards in Oracle ("team time cards") and Workday ("my team's time"); we are
stricter (no drafts, no live timers, per-person reports explicit-grant only).

**D12 · Timer switch per legal entity — default off for every entity.**
*Why:* the legal risk is per jurisdiction, and a default that needs no country lookup cannot be wrong by data. A
tenant admin turns it on per entity, with a reason (R7); the Swiss instruction lives in §22. *Comparison:* SAP
time-recording profiles by personnel area, Oracle time entry profiles by legal employer, Workday time entry templates
by location — enabling per legal employer is normal.

**D13 · Working hours — target distinct from window, holidays from the working calendar, no new module.**
*Why:* 09:00–18:00 includes lunch; a target of 9 h would make everyone look short. Half days must not count as full.
*Comparison:* SAP work schedule rules carry planned hours separately from time frames; Oracle schedules carry breaks;
half-day holidays reduce planned time.

### 21.1 Control Tower resolutions and answers (2026-09-29, second round)

| # | Subject | Resolution |
|---|---|---|
| R1 | Placement | ADR-004 written; pack references it |
| R2 | Calendar id | registry's `CAND-CAP-0010`; stale `CAND-CAP-0008` file name noted for CT (§26 C-4) |
| R3 | Time zone | tenant zone for every "local date" (same as the calendar engine); per-legal-entity zone is a follow-up |
| R4 | MOD-0023 outcome | pull-based finalizer (read, approvals page, sweep) |
| R5 | Reject reason | T1 adds a per-definition "comment required on reject" option to MOD-0023; the timesheet definition uses it |
| R6 | Category labels | `LabelResourceKey \| LabelText` (closure-outcome mechanism) |
| R7 | Timer switch | per legal entity, default off for all, admin turns on; no country lookup |
| R8 | D4 vs soft delete | nothing deleted; instants cleared at approval (audited minimisation) |
| R9 | Explicit-grant keys | CT does the AuthService change; exact keys in §23 T1 prerequisites |
| R10 | `timeTracking` | T1 emits the declared `timeEntries` block and fills `TimerState` |
| R11 | Minor | `time-entry.*` into PKS-001 (CT, `.antigravity`); `TenantScopedEntity`; MOD-0003 not required for slice 1 (§7) |
| A1 | Status | `ready-for-dev` — nothing in §26 left blocking |
| A2 | Rounding | per task per local day, nearest 15; under 8 min → no draft, "too short to count" |
| A3 | Daily limits | > 660 min flagged to approver (4857 Art. 63); > 960 min refused as implausible |
| A4 | Licensing | catalog module, self-registered, entitlement-gated |
| A5 | Legal-entity country | moot (default off) |
| A6 | Withdraw / empty | withdraw until the approver's decision is recorded; empty week cannot be submitted |
| A7 | Per-person report key | explicit-grant only |
| A8 | D13 provider change | T1 (Platform, CT's lane) |
| A9 | Backdating | current + 4 previous weeks editable (tenant setting later); older needs time-admin reopen with reason |
| A10 | ADR | yes — ADR-004 (R1) |

---

### 21.2 T2 screen decisions (Control Tower, owner's delegation, 2026-09-30)

Made after the clickable mock (https://claude.ai/artifact/VNbc7VR884XZ7jV8PXmbQr) and an independent benchmark (SAP CAT2 / My
Timesheet V2–V4, SuccessFactors clock, Oracle Redwood time card + Web Clock + Team Time Cards, Workday, Tempo, ClickUp, Harvest).

| # | Decision | Evidence |
|---|---|---|
| U1 | Start/stop the timer on the Task Center card (Start/Accept/Resume already start it). The top-bar chip appears **only while a timer runs**, links to the task, and has Stop. | ClickUp/Tempo pattern; SAP/Oracle offer a home-page quick action, not a permanent clock. Our people also work outside the Task Center (QMS, documents). |
| U2 | Desktop: rows × Mon–Sun grid with a daily target row and totals. Phone (< 768 px): one-day list with a day switcher. | SAP CAT2 and Oracle time card are grids; SAP My Timesheet V4 and Workday use a day list on mobile. |
| U3 | Suggestions (meeting, plan fill-in) sit **inside their day** as grey values; "Review suggestions" accepts several at once; copy previous week and fill from plan live in an Actions menu; the side panel is secondary (timer notices, "too short to count"). | Tempo inline suggestions + bulk log; Oracle "Copy Previous Time Card" / "Generate Entries Using Schedule"; Workday Auto-fill with review. |
| U4 | Both approval surfaces: the Task Center approval card (one by one) and a team page (overview + multi-select **approve** for unflagged weeks only). Reject is one at a time with its reason. | Oracle worklist + Team Time Cards multi-approve; Workday inbox + Review Time mass approve. |
| U5 | v1 must-haves: copy previous week (rows only, never overwrites), week navigation + Today + missing-days indicator, per-row note, keyboard entry (`1:30`, `1,5`, `1.5`, `90dk`, snap to 15), mobile day view, rejected state with reason → edit → resubmit, approval history strip, **approver sees what a correction revision changed** vs the in-force revision (T2b adds the backend field). | Benchmark §Q5. Reminders → T3; automatic substitution → BL-475. |

Slices: T2a = My Timesheet page, top-bar chip, error-code bridge, nav entry. T2b = Task Center timer actions (DeclareTimeTracking on,
mock timer removed), approvals page, categories, settings, correction diff field, 10-row platform-links table.

## 22. Legal — must confirm before go-live (not a start blocker)

> **Operating instruction (R7):** the timer ships **off** for every legal entity. A tenant admin must **not** switch
> it on for a Swiss entity until items 1–3 and 6 below are answered by counsel. For other entities the admin records
> the legal basis as the switch reason.

1. **CH:** each Swiss entity's existing ArGV 1 Art. 73 working-time record and any Art. 73a / 73b arrangement — this
   sheet is **not** that record and must not be presented as one.
2. **CH:** ArGV 3 Art. 26 applied to a person-controlled timer and to per-person reports.
3. **CH:** staff consultation, ArG Art. 48.
4. **TR:** any duty to record start/end times; evidential weight of unsigned records.
5. **KVKK:** legal basis (not consent), employee notice (Art. 10), VERBİS update; cross-border transfer TR↔CH under
   KVKK Art. 9 and DSG Art. 16.
6. **DPIA** under DSG Art. 22.
7. **Retention** per entity (CH 5/10 years, TR 10 years).
8. Whether any tenant use makes the sheet a **GxP record** (then 21 CFR Part 11 / EU GMP Annex 11 controls apply).
9. Status of the **revised Annex 11**.

These are questions for counsel, not answers.

---

## 23. Delivery Slices (each its own WP)

> T1 was split by Control Tower on 2026-09-29 into **T1a (the record)** and **T1b (capture)**, run one after the other in
> the same lane. Elsewhere in this pack "T1" means both halves together.

| Slice | Scope | CT prerequisites (done before the slice's code) | Not in it |
|---|---|---|---|
| **T1a — the record** (Platform only) | §5.1 without the capture half: `TimesheetWeek`, `TimeEntry`, `WorkCategory`, `TimeEntrySettings`, `LegalEntityTimeSetting`, `TaskTimeTotal` + indexes; weeks/entries API (save, submit, withdraw, corrections, reopen, edit window, limits A3), approver resolution (`ITimeEntryOrgGateway`) + MOD-0023 start/cancel + source resolver + pull finalizer (writes `TaskTimeTotal`), MOD-0023 "comment required on reject" option (§5.1 item 6), categories + settings API (switch stored, default off), D13 target + half-day fix (§5.1 items 4–5), manifest, audit, `TimesheetDecisionSweepJob` | (1) enrol in `ExplicitGrantOnlyPermissions.cs` the keys **`time-entry.team-totals.read`** and **`time-entry.person-reports.read`**; (2) the `time-entry.*` row in PKS-001 §4 — **done 2026-09-29** | no timer, no task hook, no SpentHours read switch, no suggestions; no Web, no `app.js`, no resx, no gateway |
| **T1b — capture** (Platform only, after T1a is accepted) | `TimerSegment` + timer API (start/stop/undo-switch, one per person, switch honoured), `ITaskTransitionObserver` hook (§5.1 item 1) + reconcile + midnight close (`TimerMidnightCloseJob`) + minimisation at approval (D4), `ITaskSpentTimeSource` + the SpentHours read-site switch (§5.1 item 2), `TimerState` + `timeEntries` projection with the stop rule (§5.1 item 3), meeting suggestions (`TimeSuggestion`, D8) + plan fill-in (D9), timer-draft rounding (A2) | — | no Web, no `app.js`, no resx, no gateway |
| **T2 — UI** | §5.2: My Timesheet grid, Approvals page, Categories (Slim), Settings, navbar timer chip, Task Center timer actions, removal of the mock timer (`foldTimer`, `WorkCenterNextIndex.*.resx:612/941` keys ×7), 7 languages, 10-row platform-links table | the §15 gateway pair live | — |
| **T3 — reminders** | unsubmitted-week reminder job, notification templates ×7, person-only minutes-conflict notification | — | the read-time minutes sync is already T1 |
| T4 — reports (not scheduled) | team totals and per-person report under the D11 keys | T1 prerequisite (1) | — |

---

## 24. PPM "Project Effort Log" — not a second time store

`portfolio-delivery/domain-config.md:53` and `README.md:32` let PPM own a temporary "Project Effort Log" (ASSUMPTION-1,
DCP-003 B2) until MOD-0280 exists. With DEC-002 confirmed and this pack ready, that log **must not grow into a second
store of worked time**: project effort should become a read of MOD-0280 entries whose task belongs to a project.
Control Tower informs the PPM lane; this pack edits no PPM file. The MOD-0117 registry row and registry identity rule
:41 still say "EA-TBD" for the SoR boundary — listed for CT, not edited here.

---

## 25. Open Questions for Control Tower

All answered on 2026-09-29 — see §21.1 (A1–A10). Remaining non-blocking follow-ups are in §20.

---

## 26. Recorded Deviations and Rule Conflicts — with CT resolutions

| # | Rule / source | Conflict | Resolution (CT 2026-09-29) | Blocking? |
|---|---|---|---|---|
| C-1 | HCM `domain-config.md:58` — backend `Diten.HcmService` | D1 places the slice in Platform | ADR-004 + domain-config In-Scope entry; extraction path §2.4 | No |
| C-2 | AGENTS.md §4 — a pack touches only its own domain's service; HCM `domain-config.md:44-46` | Platform is PSS's service | pack authority + ADR-004; MOD-0357/ADR-003 precedent | No |
| C-3 | HCM `domain-config.md:25` — time/attendance out of scope unless a separate pack approves | this is that pack | satisfied (`ready-for-dev`) | No |
| C-4 | Registry: working calendar = `CAND-CAP-0010`; `CAND-CAP-0008` = deprecated alias of MOD-0354 | the calendar pack file/frontmatter still say `CAND-CAP-0008` | this pack uses `CAND-CAP-0010`. **Note for CT:** `execution/domains/platform-shared-services/module-packs/CAND-CAP-0008-working-calendar-public-holidays.md` (file name + frontmatter `id`) is stale; not renamed here | No |
| C-5 | PKS-001 §4 namespace table | `time-entry.*` is a new first-segment namespace | owner = MOD-0280; the table lives in `.antigravity/rules/permission-key-standard.md` (protected) → CT prerequisite (§23) | No |
| C-6 | module-pack-standard §2 — Platform tenant entity base `BaseEntity` | live Platform base is `TenantScopedEntity` | use `TenantScopedEntity` | No |
| C-7 | D3/D5 "person's local date / midnight" | `IWorkingHoursProvider` resolves the **tenant** time zone (`WorkingHoursProvider.cs:62`) | v1 = tenant zone everywhere; known limit; per-legal-entity zone follow-up (§20) | No |
| C-8 | D7 "approval consumer … `ConsumedEventStore.ExecuteOnceAsync`" | MOD-0023 publishes no decision event; `ExecuteOnceAsync` takes an integration-event envelope | pull-based finalizer with a deterministic synthetic envelope | No |
| C-9 | D5/D6 "reject needs a reason" | `RejectWorkflowTaskValidator.cs:14` only caps length | T1 adds a per-definition option to MOD-0023 (§5.1 item 6) | No |
| C-10 | D10 "labels in 7 languages following TaskType/MeetingType" | those carry one tenant-typed `Name` | `LabelResourceKey \| LabelText` | No |
| C-11 | D12 "default off for Swiss legal entities" | Platform has no legal-entity country | moot: default off for every entity | No |
| C-12 | AGENTS.md §6 soft delete vs D4 "only durations remain" | — | minimisation (instants cleared, audited update); nothing deleted; no exception needed | No |
| C-13 | Blueprint MOD-0280 gate/deps: MOD-0251, HARD MOD-0003 / MOD-0037 / MOD-0017 / MOD-0032, `TIME-LEAVE-BUNDLE` | MOD-0003 missing, bundle undefined | not required for slice 1 (§7 table: no cross-service contract, no external integration, UserId keying); MOD-0003 becomes blocking at extraction | No |
| C-14 | D2 "no new field" | WC-1 needs `timeEntries` with `timeTracking` (`fixture-contract.js:200,318`) | T1 emits the already-declared block (§5.1 item 3, with stop rule) | No |

Nothing in this table blocks `ready-for-dev`. The CT prerequisites in §23 gate the start of their slice, not the
pack status.
