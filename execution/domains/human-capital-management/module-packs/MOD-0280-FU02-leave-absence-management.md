---
id: MOD-0280-FU02
name: Leave & Absence Management
parent: MOD-0280
parent_name: Time, Attendance & Leave Management
domain: human-capital-management
service: Diten.Platform
service_deviation: "HCM domain-config.md names Diten.HcmService; ADR-005 places this slice in Diten.Platform next to MOD-0280-FU01, with a data-move extraction path"
adr: docs/records/decisions/2026-10/ADR-005-leave-absence-mod-0280-fu02-lives-in-platform.md
shell: tenant
golden_reference: slim
screens:
  - module: LeaveTypes
    data_mode: client
    data_mode_max_rows: 50
  - module: LeaveEntitlements
    data_mode: server
  - module: Leave
    data_mode: server
entity_base: TenantScopedEntity   # Platform's live tenant-owned base, as MOD-0280-FU01
status: draft
owner: ali.tufanoglu
branch: feature/hcm/mod-0280-fu02-leave-absence-management
started: 2026-10-27
target: 2026-12-15
form_field_count: 7               # largest form: Leave Type (§Entity Fields)
---

# MOD-0280-FU02 — Leave & Absence Management

> **Status: draft — for owner review and independent review only. No code may be written from this pack** (module-pack-standard
> §16). Revision 01, written by CT · İzin ve devamsızlık on the owner's word `paket-taslak` (2026-10-27).
> **Revision 02** (owner's word `rev02-oneri`, 2026-10-29) answers independent pack review 01
> (`docs/records/reviews/MOD-0280-FU02/2026-10-29-ea61e8c6.md`, UYGUN DEĞİL, 8 findings accepted): one allowed-field list
> for card, e-mail and audit; one transaction for request, balances and a per-employee lock, with workflow start retried
> and compensated after commit; resumable year-end carry-over; a type read key and an endpoint scope table; the module
> registration slice split from the gateway slice; an entitlement reduction floor; open decisions bound to slice gates; an
> unmatched-records report; target date and the DataTable / gateway gates (§Implementation Notes "Revision 02").
> **Revision 03** (owner's word `rev03-gonder`, 2026-10-29) answers pack review 02
> (`docs/records/reviews/MOD-0280-FU02/2026-10-29-47df0fc4.md`, UYGUN DEĞİL, 8 findings accepted). The request ↔ approval
> link no longer uses a new protocol: it copies MOD-0280-FU01's live one (`TimesheetApprovalService`) — start in the
> employee's own request, numbered submissions, retire an orphan before a new submission, read the decision before a
> cancel, approver read by MOD-0023's may-decide rule; plus a unique submission key for an unknown commit result, one
> transaction per carry-over row, a per-event allowed-field table, three read branches with a new approvals key, the
> corrected reduction floor and one meaning for the gates (§Implementation Notes "Revision 03").
> **Revision 04** (owner's word `kilit-gonder`, 2026-10-29) answers pack review 03
> (`docs/records/reviews/MOD-0280-FU02/2026-10-29-436bc38b.md`, UYGUN DEĞİL, 3 findings accepted) after the architecture
> reassessment `docs/records/decisions/2026-10/hcm-leave-architecture-reassessment-01.md` (card Y3, `status: decided`):
> a per-request **start lease** removes the start ↔ cancel / resubmit race; cancelling a **pending** request and
> cancelling an **approved** leave are two separate paths; the payload rule limits only business fields and the AUD-001
> actor envelope is defined apart (§Implementation Notes "Revision 04").
> **Revision 05** (owner's word `yeniden-oynat`, 2026-10-29) answers pack review 04
> (`docs/records/reviews/MOD-0280-FU02/2026-10-29-5fb4d1bb.md`, F-20) with reassessment addendum 2: before any release or
> new submission the employee's own request **replays the MOD-0023 start with the stored first facts**; MOD-0023's existing
> code makes the outcome final (one task per key, a closed instance is never reopened, a task cancel closes the instance,
> a late task write needs an `Active` instance). The start lease stays only as a user-experience guard and carries no
> correctness claim (§Implementation Notes "Revision 05").
>
> **Scope of this revision: delivery block 1 only** — leave types, leave entitlements entered by HR, and the employee's own
> leave requests with balance and cancellation; approval through MOD-0023 in the Task Center. Absence records, sickness,
> attendance calendar, schedules, MOD-0315 views and the timesheet link are **out of scope** (§Follow-up Items).
>
> **Inputs (all on `chore/ct/hcm-leave-ct-01`):** identity row `execution/registries/module-id-registry.md` (MOD-0280-FU02,
> `planned`); discovery decisions `docs/records/audits/2026-10/hcm-leave-discovery-01/RECORD.md`; ADR-005 + addendum 1;
> design decisions `docs/records/decisions/2026-10/hcm-leave-design-decisions-01.md` (D-1 access, D-2 sensitive data, D-3
> balance lifecycle); independent design review 01 (`docs/records/reviews/MOD-0280-FU02/2026-10-27-design-01.md`, KOŞULLU,
> 7 findings accepted); employee contract HCM-CORE-BUNDLE **v0.3 proposal** (`docs/analysis/contracts/drafts/hcm-core-employee-link-v0.3-proposal.md`
> @ 641542099 on `chore/ct/hcm-employee-ct-01` — a proposal, not frozen); Blueprint MCR draft
> `docs/records/decisions/2026-10/hcm-leave-mcr-draft-01.md` (not yet sent to Master).

## Module Summary

- **Purpose.** The internal system of record for leave requests and leave balances (Blueprint 8.8 `Blueprint_Data`
  MOD-0280: SoR "Time Entry; Attendance; Schedule; Leave Balance", soft page "Leave Request"). This child owns the leave
  part; MOD-0280-FU01 owns time entry.
- **Users.** Employee (requests own leave, sees own balance), line manager (approves in the Task Center), HR (defines
  leave types, enters yearly entitlements, sees every request in the tenant).
- **Capability in block 1.** One record type for leave (type decides whether it reduces the balance and whether approval
  is needed — owner proposal accepted in review 01 as defensible); pending requests reserve balance; approval via MOD-0023;
  cancellation with refund before start; working-day count from MOD-0362; calendar-year entitlements with carry-over.
- **Example.** HR defines "Annual leave" (category annual, reduces balance, needs approval, carry-over 5) and enters 14
  days for 2026. Ayşe asks for 3–7 Nov 2026; the page shows 5 working days and "14 → 9 if approved"; the request is
  pending and reserves 5. Her line manager approves in the Task Center; the balance is 9. Ayşe cancels before 3 Nov; the
  balance is 14 again.

## Ownership and Boundaries

### Owns

- `LeaveType`, `LeaveEntitlement`, `LeaveRequest`, `LeaveBalance` (ledger), `LeaveEmployeeSnapshot` (read cache),
  `LeaveSyncRun` (monitoring record); module code `leave`; permission namespace `leave.*`; API prefix `api/v1/leave`.

### Does NOT own (consume through ports, never re-implement)

| Concern | Owner | How this pack uses it |
|---|---|---|
| Employee identity, hire date, status, legal entity | **MOD-0251 Core HR / Employee Master** (CT · Çalışan kaydı) | HCM-CORE-BUNDLE read endpoints through `ILeaveEmployeeGateway` (§Dependencies); never writes |
| User ↔ person link | **K-1 open** (MOD-0360 or MOD-0288) | consumed inside HcmService's `/me` endpoint; this pack does not resolve it |
| Approval workflow, decisions, Task Center card | **MOD-0023 Workflow Designer** | same pattern as `TimesheetApprovalService`; this module exposes no approve endpoint |
| Approver chain | **MOD-0288** `PositionAssignment` (contract §4, K-8 proposal) | `ILeaveOrgGateway`, FU01 `ApproverResolver` pattern |
| Working days, holidays, half-days | **MOD-0362 Working Calendar & Public Holidays** | `IWorkingHoursProvider` (read) |
| Audit, notifications, scheduler, permissions | FG-005 audit · MOD-0027 · Hangfire seam · MOD-0018 | existing seams, unchanged |
| HR views over these records | **MOD-0315 Time, Attendance & Leave Workspace** | not built here |

### Not to be confused with

- **DitenHumanCapitalService "Time, Attendance & Leave"** (`hcm.time-attendance-leave.*`, route `time-attendance-leave`
  → 5063) — a readiness-record module that refuses leave requests and balances (`TimeAttendanceLeaveGuard.cs:156,161`).
  Untouched; its route is not reused (discovery decision 8).
- **MOD-0280-FU01** — same parent, same service, separate collections, keys and module code; no shared table.

### Placement deviation (ADR-005)

Runs in `Diten.Platform` (platform-shared-services' service) although HCM domain-config names `Diten.HcmService`. Reason,
extraction path (data move, together with FU01) and trigger are in ADR-005. Writing code in `Diten.Platform` needs XMC-001
consent from the platform-shared-services owner — **open** (§Open Questions).

## Owned Objects

### Runtime entities (`TenantScopedEntity`, Mongo, soft delete, tenant-first indexes)

| Entity | Collection | Purpose |
|---|---|---|
| `LeaveType` | `leave_types` | Tenant catalogue of leave types (D-2 category, balance and approval flags) |
| `LeaveEntitlement` | `leave_entitlements` | One per (employee, calendar year, leave type): entitled days and carry-over days, entered by HR |
| `LeaveRequest` | `leave_requests` | One request: dates, half-day part, day count per year, type snapshot, status, workflow link |
| `LeaveBalance` | `leave_balances` | Ledger per (employee, year, leave type): entitled, carried, reserved, taken — the only place the balance is written (D-3/1) |
| `LeaveEmployeeSnapshot` | `leave_employee_snapshots` | Cached `EmploymentSummary` from the sync job, for HR lists only — never used to open a request (ADR-005 addendum 1/3) |
| `LeaveEmployeeLock` | `leave_employee_locks` | One per (tenant, employee): `Version` bumped in every submit and cancel transaction, so two submits of one person never interleave — for every type, including types that do not reduce the balance (review 01 F-02) |
| `LeaveSyncRun` | `leave_sync_runs` | One per sync run: read / changed / unmatched counts, error, duration (ADR-005 addendum 1/2) |
| `LeaveSyncUnmatched` | `leave_sync_unmatched` | One per (run, employee) that could not be reconciled: `employeeId`, reason code, first-seen and last-seen run — no other personal field (ADR-005 addendum 1/2, review 01 F-07) |
| `LeaveCarryOverRun` | `leave_carry_over_runs` | One per (tenant, closing year): status `Running` / `Completed`, counts — monitoring only |
| `LeaveCarryOverMark` | `leave_carry_over_marks` | One per (tenant, employee, closing year, leave type), unique index: written in the **same transaction** as the next year's `CarriedOverDays` and both `LeaveBalance` rows, so a resumed run never skips or repeats a row (review 02 F-12) |

### CQRS (Golden Reference convention; one Feature folder `Features/Leave/`)

- Commands (types): `CreateLeaveType`, `UpdateLeaveType`, `ActivateLeaveType`, `DeactivateLeaveType`.
- Commands (entitlements): `SetLeaveEntitlement` (create or update one row), `RunLeaveYearEndCarryOver` (HR, one year at a
  time), `AdjustLeaveCarryOver` (HR, with reason code).
- Commands (requests): `SubmitLeaveRequest`, `CancelLeaveRequest` (own; pending or not yet started),
  `CancelStartedLeaveRequest` (HR), `ResubmitLeaveRequest` (own — retires the previous submission's orphan instance and
  starts the next submission, FU01 F1), `FinalizeLeaveDecision` (system or read path — applies MOD-0023's outcome once).
- Commands (system): `SyncLeaveEmployeeSnapshots`.
- Queries: `GetMyLeaveRequests`, `GetMyLeaveBalance`, `GetLeaveRequestById`, `GetLeaveRequestList` (HR),
  `PreviewLeaveDayCount`, `GetLeaveTypeList`, `GetLeaveTypeById`, `GetLeaveEntitlementList`, `GetLeaveSyncRunList`.

Nothing is hard-deleted: types are deactivated, requests are cancelled, entitlements are set to a new value (audited).

### Ports, providers, services

| Object | Where | Purpose |
|---|---|---|
| `ILeaveEmployeeGateway` | `Features/Leave` (port) + `Adapters/` | HCM-CORE-BUNDLE: `/me/employment-summary?asOf`, `/{employeeId}/employment-summary?asOf`, `/employment-summaries?updatedSince`. Fake adapter for development and tests; the HTTP adapter is written against the frozen contract version only |
| `ILeaveOrgGateway` | same pattern | Primary seat → `ReportsToPositionId` chain → holders; approver pool (FU01 pattern) |
| `LeaveApproverResolver` | `Features/Leave/Services` | FU01 `ApproverResolver` rules: line manager, next seat up, pool; never the person; null → submit refused |
| `LeaveApprovalService` | `Features/Leave/Services` | **Same shape as FU01 `TimesheetApprovalService`, plus `SettleAsync`** (reassessment 01 addendum 2: replay the start with the stored `StartFacts`, cancel an open task, answer closed / decided / unsettled); unlike FU01 `RetireSubmissionAsync` it never continues past an unsettled start (review 03 F-17, review 04 F-20) — (`ITimesheetApprovalService`: `StartAsync`, `CancelAsync`, `RetireSubmissionAsync`, `ReadDecisionAsync`, `DecidableInstanceIdsAsync`). Starts the instance inside the **employee's own** submit or resubmit request, after the submit transaction commits, so the starter is the employee (MOD-0023 `SameStarter`); key `leave-request:{tenant}:{requestId}:{submissionNumber}`. **No system job starts or retries a start** (review 02 F-11). Cancels only through MOD-0023's own cancel command and returns "decided first" when the decision won (FU01 F13) |
| `LeaveRequestWriter` | `Features/Leave/Services` | The one place a request changes state together with balances: request row, every affected `LeaveBalance` row and the `LeaveEmployeeLock` row are written in **one** `IPlatformTransactionExecutor` transaction. A failure inside the body rolls back; an **unknown commit result** (`PlatformTransactionUnavailableException`, executor :70–90) is answered 503 `LEAVE_SUBMIT_OUTCOME_UNKNOWN` and resolved by the client retrying with the same `SubmissionKey` (review 02 F-16) |
| `LeaveApprovalSourceResolver : IApprovalSourceResolver` | `Features/Leave/Providers` | Task Center card title: "Leave · {type label} · {from}–{to} · {days}" and a deep link; object type `leave-request`. The requester is shown by MOD-0023's own requester field, never in the title. Only the allowed fields of §Runtime Constraints 6 (D-2/3, review 01 F-01) |
| `LeaveDayCounter` | `Features/Leave/Services` | Working-day count over `IWorkingHoursProvider`; splits by calendar year (D-3/6, D-3/7) |
| `LeaveBalanceLedger` | `Features/Leave/Services` | The single writer of `LeaveBalance`; conditional writes on `Version` |
| `LeaveManifestProvider : IModuleManifestProvider` | `Features/Leave/SelfRegistration` | Module code `leave`, pages, actions, permissions, notification events |
| Jobs | `Features/Leave/BackgroundJobs` | `Diten.Platform.MOD-0280-FU02.LeaveDecisionSweepJob`, `Diten.Platform.MOD-0280-FU02.LeaveEmployeeSyncJob`; each behind its own `EnabledJobs[...]` flag; **correctness never depends on a job** (decisions are also finalized at read time) |

### API (`api/v1/leave`)

`GET /me/requests` · `GET /me/balance?year` · `POST /requests/preview` · `POST /requests` · `GET /requests/{id}` ·
`POST /requests/{id}/resubmit` · `POST /requests/{id}/cancel` · `GET /admin/requests` · `POST /admin/requests/{id}/cancel` ·
`GET /types` · `GET /types/{id}` · `POST /types` · `PUT /types/{id}` · `POST /types/{id}/activate` ·
`POST /types/{id}/deactivate` · `GET /entitlements?year` · `PUT /entitlements` · `POST /entitlements/year-end` ·
`POST /entitlements/{id}/carry-over` · `GET /admin/sync-runs` · `GET /admin/sync-runs/{id}/unmatched`.

Approve / reject are MOD-0023's own actions (`platform.workflow.tasks.approve` / `.reject`). `api/v1/leave` does not exist in
the repo today (measured 2026-10-27: 0 matches in `services/` and `gateway/`).

## Entity Fields

Business fields never use `Version` for anything but concurrency (module-pack-standard §14).

### `LeaveType` (Slim form — 7 user fields)

| Field | Type | Required | Rule |
|---|---|---|---|
| `Code` | string | yes | `^[A-Z][A-Z0-9_]{1,31}$`, tenant-unique, immutable |
| `LabelText` / `LabelResourceKey` | string | one of | label shown in 7 languages; `LabelResourceKey` for installed defaults |
| `Category` | enum | yes | `annual` · `excuse` · `unpaid` · `other` · `health` — **`health` is locked** (D-2/1): not selectable, not settable through any API, import or seed |
| `ReducesBalance` | bool | yes | false for `unpaid` |
| `RequiresApproval` | bool | yes | default true |
| `CarryOverLimitDays` | decimal(4,1) | no | 0–365, step 0.5; only when `ReducesBalance` |
| `IsActive` | bool | yes | deactivate instead of delete |

No "document required" field and no description field (D-2/2).

### `LeaveEntitlement`

| Field | Type | Required | Rule |
|---|---|---|---|
| `EmployeeId` | uuid | yes | contract §1.1 canonical employee id; key (`TenantId`, `EmployeeId`) |
| `Year` | int | yes | calendar year (D-3/7) |
| `LeaveTypeId` | uuid | yes | active, `ReducesBalance = true` |
| `EntitledDays` | decimal(5,1) | yes | 0–365, step 0.5 |
| `CarriedOverDays` | decimal(5,1) | system | written by `RunLeaveYearEndCarryOver` / `AdjustLeaveCarryOver` only |
| — | | | a carried-over row is identified by its `LeaveCarryOverMark` (same transaction), not by a field on this row |

Unique index (`TenantId`, `EmployeeId`, `Year`, `LeaveTypeId`).

**Reduction floor (review 01 F-05, review 02 F-14; refines D-3/5):** after the change, `EntitledDays + CarriedOverDays`
must stay ≥ `TakenDays + ReservedDays` of the same balance row; otherwise 409 `LEAVE_ENTITLEMENT_BELOW_USED` with the
pending requests listed. A balance therefore never goes negative and an approved request is never re-evaluated.
Example: entitled 14, carried 0, taken 5, reserved 3 → total may not go below 8. Approving the 3 reserved days moves them
to taken and the floor **stays** 8; to reach 6, HR must reject or cancel at least 2 reserved days first. Example with
carry-over: entitled 5, carried 5, taken 8, reserved 0 → valid (10 ≥ 8); entitled may go down to 3, not to 2.

### `LeaveRequest`

| Field | Type | Rule |
|---|---|---|
| `EmployeeId`, `RequesterUserId` | uuid | resolved server-side from `/me` (never from the body) |
| `LeaveTypeId` | uuid | active type |
| `TypeSnapshot` | object | `Code`, `Category`, `ReducesBalance`, `RequiresApproval` at submit time (D-3/5) |
| `StartDate`, `EndDate` | date | `StartDate ≤ EndDate`; both inside the employee's active span (`asOf` checks, contract §2.1) |
| `HalfDayPart` | enum | `none` · `morning` · `afternoon`; not `none` only when `StartDate = EndDate` (D-3/6) |
| `DayCount` | decimal(5,1) | computed by `LeaveDayCounter`, never sent by the browser |
| `YearAllocations` | list | `{Year, Days}` — a request crossing 31 Dec is split (D-3/7) |
| `Status` | enum | `Pending` · `Approved` · `Rejected` · `Cancelled` · `Withdrawn` (auto-approved types go straight to `Approved`) |
| `SubmissionKey` | uuid | sent by the browser once per "Send" click; unique per tenant; a retry with the same key returns the stored request and reserves nothing again (review 02 F-16) |
| `SubmissionNumber` | int | 1 on submit; +1 on each resubmit; part of the MOD-0023 idempotency key (FU01 pattern) |
| `WorkflowStartState` | enum | `NotNeeded` (type needs no approval) · `Unconfirmed` (start failed or its answer was lost) · `Started` (instance id recorded) · `NeedsReconciliation` (a replay was refused, e.g. the facts conflict). An `Unconfirmed` or `NeedsReconciliation` request keeps its days reserved; it is never released or retried by a job (review 02 F-10, F-11; revision 05) |
| `StartLeaseId`, `StartLeaseUntilUtc` | uuid, instant | user-experience guard only (revision 05): while a start call of this request is running, cancel and resubmit answer 409 `LEAVE_APPROVAL_STARTING` instead of waiting. Correctness does **not** depend on it (§Runtime Constraints 9) |
| `StartFacts` | object | the exact facts of the first start of the current submission: definition id, object type / id / ref, candidate approver user ids, submission key — stored in the submit transaction and used verbatim for every replay, so MOD-0023's fingerprint check matches (reassessment addendum 2) |
| `WorkflowInstanceId`, `ApproverResolution` | | as FU01 (line manager / chain / pool) |
| `DecisionAppliedAtUtc`, `CancelledByUserId`, `CancelReason` | | `CancelReason` is a fixed code list, not free text |

### `LeaveBalance`

`EmployeeId`, `Year`, `LeaveTypeId`, `EntitledDays`, `CarriedOverDays`, `ReservedDays` (pending), `TakenDays` (approved);
remaining = entitled + carried − reserved − taken (D-3/1). Unique (`TenantId`, `EmployeeId`, `Year`, `LeaveTypeId`).
Every change is a conditional write on `Version`.

### `LeaveEmployeeSnapshot`, `LeaveSyncRun`

Snapshot: the contract §2 `EmploymentSummary` fields this module needs (`employeeId`, `personId`, `employeeNumber`, `status`,
`hireDate`, `terminationDate`, `legalEntityId`, `employmentType`, `version`, `updatedAtUtc`) plus `SyncedAtUtc`; the
display name is read from MOD-0288 at display time, not stored. No date of birth, national id, personal contact, nationality.
Sync run: `StartedAtUtc`, `FinishedAtUtc`, `Read`, `Changed`, `Unmatched`, `ErrorCode`, `ContractVersion`.
Unmatched row: `RunId`, `EmployeeId`, `ReasonCode` (`NOT_FOUND` · `NOT_ACTIVE` · `CONTRACT_VERSION` · `TENANT_MISMATCH` ·
`OVERLAP`), `FirstSeenRunId`, `LastSeenRunId`. No name, no other personal field.
Employee lock: `EmployeeId`, `Version`, `LastChangedAtUtc`.

## Repo Scope

Only after `ready-for-dev` and XMC-001 consent (§Open Questions):

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Leave/**`, `.../Domain/Repositories/ILeave*Repository.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Leave/**`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/Leave/**`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/LeaveController.cs`
- `services/Diten.Platform/tests/**/Leave/**`
- `frontend/Diten.Web/Controllers/LeaveController.cs`, `frontend/Diten.Web/Views/Leave/**`,
  `frontend/Diten.Web/wwwroot/assets/js/Leave/**`, `frontend/Diten.Web/Resources/Views/Leave/**` (7 languages)
- `execution/domains/human-capital-management/**` (this pack, domain-config line for FU02)
- Gateway pair: separate integration-agent task (§Gateway / API Routing Decision)

## Protected Paths

- `.antigravity/**`; `frontend/Diten.Web/Views/Shared/_Layout.cshtml`; Archive folders
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent only)
- `services/Diten.HcmService/**` and every MOD-0251 file (CT · Çalışan kaydı)
- `services/Diten.HumanCapitalService/**` (readiness module, discovery decision 5)
- `services/Diten.Platform/src/**/Features/TimeEntry/**` (MOD-0280-FU01) — patterns are copied, files are not edited
- `services/Diten.Platform/src/**/Features/Workflow/**`, `Features/Organization/**`, `Features/WorkingCalendar/**`,
  `Features/Tasks/**` — consumed through ports only
- Other domains' `execution/domains/**`

## Dependencies

| Dependency | State (measured 2026-10-27) | Needed by |
|---|---|---|
| HCM-CORE-BUNDLE contract (MOD-0251) | v0.3 **proposal**, not frozen; no endpoint exists | DL-4 (real adapter); DL-1…DL-3 use the fake adapter |
| MOD-0251 activation slice (HCM-EMP-05) + K-1 user↔person link | not started; MOD-0251 review 01 UYGUN DEĞİL (10 engel, CT · Çalışan kaydı) | acceptance of DL-4 with real data |
| MOD-0023 approval workflow | in use by FU01 (`TimesheetApprovalService.cs`); module now has its own Control Tower (queue `pss-wf`) | DL-3; registering the leave approval definition is coordinated with that tower (card S-90) |
| `IPlatformTransactionExecutor` (Platform persistence) | exists (`Diten.Platform.Infrastructure/Persistence/PlatformTransactionExecutor.cs`; used by MOD-0023 start) | DL-2 |
| MOD-0288 positions / approver chain | in use by FU01 (`ApproverResolver.cs`, `PositionAssignment.UserId`) | DL-3 |
| MOD-0362 working calendar | in use by FU01 (`IWorkingHoursProvider`) | DL-2 |
| MOD-0027 notifications | approved | DL-3 |
| MOD-0003 Data Contract Registry, MOD-0037 Integration Monitoring | not built — interim per ADR-005 addendum 1 | DL-4 |
| Service identity for the sync job (contract K-4) | not built (Auth, another module) | DL-4 sync job |

## Runtime Constraints

1. Mongo, tenant-scoped, soft delete; tenant-first compound indexes on every `leave_*` collection; `TenantId` from the
   server context only.
2. **Local date and day count** from `IWorkingHoursProvider` in the tenant's time zone (FU01 R3; contract K-9 open).
3. **Fail closed when the employee service is unreachable** (ADR-005 addendum 1/3): `SubmitLeaveRequest` returns 503
   `LEAVE_EMPLOYEE_SERVICE_UNAVAILABLE`; reads of existing requests and balances continue; the snapshot cache never opens a
   request.
4. **Correctness never depends on a scheduler**: a pending request whose MOD-0023 instance is decided is finalized on the
   next read as well as by the sweep job.
5. **Idempotency**: submit keyed by `SubmissionKey` (unique); approval start keyed by
   `leave-request:{tenant}:{requestId}:{submissionNumber}`; finalization keyed per (instance, outcome);
   sync keyed by (`employeeId`, `version`).
6. **Data minimisation (D-2) — one allowed-field list (review 01 F-01).** For everything about a person's request — Task
   Center card, notification e-mail, request audit events, notification payload — the only fields are: leave type
   (`Code` and label), `StartDate`, `EndDate`, `HalfDayPart`, `DayCount`, `Status` and the request id. No person name, no
   free text, no document, no health category. The requester appears only through MOD-0023's own requester reference. Type
   catalogue events (`leave.type.*`) may carry the label because it is catalogue data, not a person's data.
   This list limits the **business payload** only. The audit record's standard actor envelope (AUD-001 §3: actor id,
   actor type, masked display, tenant, correlation) and its closed technical fields (§Audited Events) are defined apart and
   are not business payload (review 03 F-18).
   **Known risk (D-2/4):** HR can give an `other` type a label that hints at health; labels are not filtered. The guard is
   that no free text and no document exist; the pack accepts this risk for block 1 and the HR settings page says that
   labels are visible to managers.
7. Browser → `/Leave/api/*` → Gateway 5000 → Platform 5057. 7-language l10n (tenant). No inline CSS.
8. Every page, action and permission comes from the manifest; nothing hand-seeded.
9. **State change and the approval link — FU01 protocol, settled by replay (reassessment 01 addendum 2; review 01 F-02,
   review 02 F-10, F-11, F-12, F-16, review 03 F-17, F-19, review 04 F-20).**
   - Submit: one transaction writes the request (`SubmissionKey` unique) with its `StartFacts`, every affected balance row
     (both years across 31 Dec) and the employee lock. Unknown commit result → 503 `LEAVE_SUBMIT_OUTCOME_UNKNOWN`; the
     browser retries with the same key and gets exactly one request and one reservation.
   - Start: after commit, in the same employee request, `LeaveApprovalService.StartAsync(StartFacts)` (starter = the
     employee). Success records the instance id (`Started`); failure or lost answer → `Unconfirmed`. Nothing retries on its
     own.
   - **Settle** (`LeaveApprovalService.SettleAsync`) — the only way to a final answer, always in the submitting employee's
     own request: replay `StartAsync` with the stored `StartFacts` and the current submission's key. MOD-0023's existing code
     makes the result final:
     - a whole start (MOD-0023 completes an incomplete one for the same starter and writes exactly one task,
       `StartWorkflowInstanceHandler.cs:372–411`): if its task is open, cancel it with MOD-0023's own
       `CancelWorkflowTaskCommand` — that sets the instance `Cancelled` (`WorkflowTaskTransitionSupport.cs:670`); result
       **closed**. If a decision is recorded, result **decided**;
     - the "incomplete" conflict on a non-`Active` instance (closed by MOD-0023's abandoned-start recovery, :393–398): result
       **closed**;
     - a facts conflict or any other refusal: result **unsettled** → `NeedsReconciliation`, nothing released, the request
       appears on HR's reconciliation list; MOD-0023 unreachable → 503, nothing released.
     After **closed**, no later write can open a task under that key: a paused older start either finds the closed instance
     (never reopened) or loses its task claim, which needs an `Active` instance (:620–659).
   - Cancel of a **pending** request (the employee): settle; **closed** → request `Cancelled` and reserved days released in
     one transaction with the lock; **decided** → the decision is applied and the cancel is refused (FU01 F13);
     **unsettled** → nothing released.
   - Resubmit (the employee, `Unconfirmed` only): settle the current submission; **decided** → apply it, no new start;
     **closed** → `SubmissionNumber + 1`, new `StartFacts` stored, start; **unsettled** → refused (409
     `LEAVE_APPROVAL_NEEDS_RECONCILIATION`).
   - Cancel of an **approved** leave: MOD-0023 is not touched — the completed approval stands. Not started (`StartDate` >
     local today): the employee (`leave.requests.cancel`). Started: HR only (`leave.admin-requests.cancel`, reason code; a
     record correction). In both, one transaction sets `Cancelled`, returns the request's `TakenDays` and bumps the lock.
   - Start lease: while a start or settle call of the request is running, a second cancel or resubmit gets 409
     `LEAVE_APPROVAL_STARTING`. This is a courtesy; if the lease lapses, settle still gives the final answer.
   - Decisions are only **read** by the sweep job and the read path (finalize once); neither starts, settles nor cancels.
   - Year-end carry-over: per (employee, closing year, type) one transaction writes the `LeaveCarryOverMark` (unique), the
     next year's `CarriedOverDays` and both balance rows; a duplicate mark aborts that row's transaction, so a resumed run
     applies every row exactly once.
   - **Known limit:** if the leave approval definition changed between the first start and a settle, MOD-0023 refuses the
     replay as a facts conflict; the request then stays reserved in `NeedsReconciliation` (safe side). The definition is
     installed once and versioned; changing it is a pack revision.

## Layout & Shell Contract

- `shell: tenant` → `Layout = "_LayoutTenantShell";` written explicitly in every `.cshtml`.
- Routes: `/Leave` (My leave: requests, balance, new request; HR tab "All requests" when the caller holds
  `leave.admin-requests.read`), `/Leave/Types`, `/Leave/Entitlements`.
- Page codes (nav and help): `MYLEAVE`, `LEAVETYPES`, `LEAVEENTITLEMENTS`; nav keys `Nav.Page.MYLEAVE`,
  `Nav.Page.LEAVETYPES`, `Nav.Page.LEAVEENTITLEMENTS`; domain `Nav.Domain.HUMANCAPITAL` (existing).
- `MYLEAVE` visible in navigation; `LEAVETYPES` and `LEAVEENTITLEMENTS` reached from an HR settings entry.
- UAS-001: a user without the page permission gets the single explanation panel, no skeleton, no redirect.

## Backend File Convention

```text
Features/Leave/
├── Commands/            sealed records, one per file
├── Queries/
├── Handlers/
│   ├── CommandHandlers/ {Verb}{Object}Handler.cs
│   └── QueryHandlers/   Get{Object}{List|ById}Handler.cs
├── Validators/          {Verb}{Object}Validator.cs
├── Services/            LeaveApprovalService, LeaveApproverResolver, LeaveDayCounter, LeaveBalanceLedger, LeaveDecisionFinalizer
├── Adapters/            EmployeeGatewayAdapter (HTTP, contract-versioned), FakeEmployeeGateway (dev/test), OrgGatewayAdapter
├── Providers/           LeaveApprovalSourceResolver
├── BackgroundJobs/      LeaveDecisionSweepJob, LeaveEmployeeSyncJob
├── SelfRegistration/    LeaveManifestProvider.cs
└── LeaveModels.cs       all DTOs in one file
```

Repositories inherit `TenantRepository<T>`; collections snake_case with `leave_` prefix; thin controller,
`CustomBaseController`, `[Authorize]` + `[HasPermission]`, `Response<T>`. No new base entity, repository base, audit, event
or job infrastructure. MOD-0023, MOD-0288 and MOD-0362 types appear only in `Adapters/`.

## Frontend File Contract

- **LeaveTypes** — Golden Reference Slim, file-for-file from `Views/DevEnablement/GoldenReferenceSlim/` (`Index`, `_Filter`,
  `_DataTable`, `_IndexL10n`, `_CreateEditOffcanvas`, `_DetailsQuickView`, marker class, JS, 7 resx); client mode, max 50.
  No delete; activate / deactivate. The category select never offers `health`.
- **LeaveEntitlements** — Slim, server mode; filter by year and leave type; create/edit offcanvas (employee, year, type, days);
  year-end carry-over action with confirmation.
- **Leave** — Slim, server mode: balance cards per type (entitled, carried, reserved, taken, remaining — numbers, not
  percentages), request list, "New request" offcanvas (type, start, end, half-day part) with a live preview of working days
  and "remaining → after approval"; cancel action. HR tab with the same list for the whole tenant.

## Validation Rules

| Field / action | Rule | Check | Reason code |
|---|---|---|---|
| `LeaveType.Code` | pattern, tenant-unique, immutable | validator + unique index | `LEAVE_TYPE_CODE_DUPLICATE` / `LEAVE_TYPE_CODE_IMMUTABLE` |
| `LeaveType.Category` | in the open list; `health` refused | validator | `LEAVE_TYPE_CATEGORY_LOCKED` |
| `LeaveType` label | `LabelText` or `LabelResourceKey` | validator | `LEAVE_TYPE_LABEL_REQUIRED` |
| `LeaveEntitlement` | type active and reduces balance; days 0–365 step 0.5; employee known | validator + gateway | `LEAVE_ENTITLEMENT_TYPE_INVALID` / `LEAVE_ENTITLEMENT_DAYS_INVALID` / `LEAVE_EMPLOYEE_NOT_FOUND` |
| `LeaveEntitlement` reduction | entitled + carried ≥ taken + reserved | handler in the balance transaction | `LEAVE_ENTITLEMENT_BELOW_USED` |
| submit — employee | `/me` resolves an employee active on `StartDate` and on `EndDate` | gateway (`asOf`) | `LEAVE_EMPLOYEE_NOT_LINKED` / `LEAVE_EMPLOYEE_NOT_ACTIVE` |
| submit — dates | `StartDate ≤ EndDate`; ≥ 1 working day; half day only for one day | validator + `LeaveDayCounter` | `LEAVE_DATES_INVALID` / `LEAVE_NO_WORKING_DAY` / `LEAVE_HALF_DAY_INVALID` |
| submit — overlap | no pending or approved request of the same employee overlaps | handler inside the transaction that bumps `LeaveEmployeeLock.Version`; a concurrent submit of the same person fails its lock write and is retried once, then 409 | `LEAVE_REQUEST_OVERLAP` |
| submit — balance | for `ReducesBalance`: remaining ≥ allocation per year | `LeaveBalanceLedger` conditional write | `LEAVE_BALANCE_INSUFFICIENT` |
| submit — approver | resolvable when `RequiresApproval` | `LeaveApproverResolver` | `LEAVE_NO_APPROVER` |
| cancel (own) | `Pending` (workflow path) or `Approved` and `StartDate` > local today (business path) | handler | `LEAVE_CANCEL_TOO_LATE` |
| cancel / resubmit while starting | start lease held (courtesy) | conditional read of the lease | `LEAVE_APPROVAL_STARTING` |
| cancel / resubmit not settled | the replay of the stored start facts was refused | `LeaveApprovalService.SettleAsync` | `LEAVE_APPROVAL_NEEDS_RECONCILIATION` |
| cancel (HR) | `Approved`, started; reason code required | validator | `LEAVE_CANCEL_REASON_REQUIRED` |
| carry-over | year closed once per (tenant, year); adjust needs reason code | handler + index | `LEAVE_CARRY_OVER_ALREADY_RUN` / `LEAVE_CARRY_OVER_REASON_REQUIRED` |
| every write | `expectedVersion` | conditional write | `LEAVE_CONCURRENCY_CONFLICT` |

## Failure Path to Verify

- **Two submits at the same instant** for overlapping dates or for the last remaining days → exactly one request stored;
  the other gets 409 (`LEAVE_REQUEST_OVERLAP` or `LEAVE_BALANCE_INSUFFICIENT`); the balance is never negative. The same
  holds for two overlapping requests of a type that does not reduce the balance (the employee lock serialises them).
- **Failure inside the transaction body** (second year's balance row or the lock write fails) → rollback; no request, no
  reservation; 409 or 503 with the reason code.
- **Commit answer lost** (`PlatformTransactionUnavailableException`) → 503 `LEAVE_SUBMIT_OUTCOME_UNKNOWN`; retry with the
  same `SubmissionKey` returns the one stored request, or stores it once if the first commit did not happen.
- **Workflow start fails or its answer is lost** → request `Pending`, `Unconfirmed`, days reserved; "send again" retires the
  previous submission's instance by key (cancel if open, apply if decided) before starting the next one.
- **Cancel or resubmit while a start is in flight** (two tabs) → 409 `LEAVE_APPROVAL_STARTING`; nothing changes.
- **Older start paused before allocation, then resumes after a cancel** → the cancel's settle already created and closed
  the one instance for that key; the resumed start finds it closed and writes no task (review 04 F-20).
- **Older start paused after allocation, before writing its task** → the settle completes that same instance (one task)
  and cancels it; the resumed start loses its task claim because the instance is no longer `Active`.
- **Process dies during a start** → the next cancel or resubmit settles by replay; the lease is not needed.
- **Settle refused (facts conflict)** → `NeedsReconciliation`; nothing released; HR sees it on the reconciliation list.
- **Cancel an approved, not-started leave** → MOD-0023 untouched; request `Cancelled`; taken days returned in one
  transaction. **HR cancels a started leave** → same, with reason code (review 03 F-19).
- **Cancel when the decision came first** → cancel refused (FU01 F13); the decision is applied.
- **Year-end carry-over stops halfway** → rows with a mark are skipped by the unique index inside their own transaction;
  every row applied exactly once.
- **Entitlement reduced below taken + reserved** → 409 `LEAVE_ENTITLEMENT_BELOW_USED`, nothing written.
- **Decision and cancellation race** → whichever is recorded first wins (FU01 `TimesheetApprovalService.cs:239–243`
  pattern); the balance reflects exactly one outcome.
- **Decision replayed** (sweep job and read path both see it) → applied once; one audit entry.
- **Employee service down** → submit 503 `LEAVE_EMPLOYEE_SERVICE_UNAVAILABLE`, nothing written; lists still load.
- **Employee terminated before the end date** → submit 409 `LEAVE_EMPLOYEE_NOT_ACTIVE`.
- **No approver** → 409 `LEAVE_NO_APPROVER`; never auto-approved when the type requires approval.
- **Resolved approver is the person** → walked past; if MOD-0023 still records the person, the finalizer refuses and flags.
- **Health category through the API or an import** → 400 `LEAVE_TYPE_CATEGORY_LOCKED`.
- **Request across 31 Dec** → two year allocations; each year's balance reduced by its own days.
- **Concurrency** (two edits of one type or entitlement) → 409 "the record changed meanwhile; reload and retry".
- **Unauthorized** — missing key → 403; an approver opens a request not routed to them → 404; another tenant → 404, no
  metadata; an employee reads another employee's request → 404.

## Authorization Convention

```text
Policy:      [Authorize]   (tenant user)
Permissions (PKS-001, 3 segments, namespace leave owned by MOD-0280-FU02):
  leave.requests.read            own requests and own balance
  leave.requests.create          submit own request
  leave.requests.cancel          cancel own request (pending, or approved and not started)
  leave.approvals.read           manager: requests whose MOD-0023 approval they may decide (FU01 time-entry.approvals.read)
  leave.admin-requests.read      HR: every request in the tenant (D-1/3)
  leave.admin-requests.cancel    HR: cancel a started request, with reason code
  leave.types.read               every employee who may request leave: active types (needed by the request form)
  leave.types.manage             HR: leave type catalogue
  leave.entitlements.read        HR: entitlement list
  leave.entitlements.manage      HR: set entitlements, year-end carry-over, adjustments
  leave.sync-runs.read           HR / support: sync monitoring (ADR-005 addendum 1/2)
Decisions:   MOD-0023's own platform.workflow.tasks.approve / .reject (unchanged)
```

- `cancel` and `manage` are Tier-3 actions registered by this pack (PKS-001 §5); everything else is Tier 1.
- `leave.types.read` is granted together with `leave.requests.create` in the baseline employee role template; the type
  list it returns holds active types only and never the locked `health` category.

Endpoint → key → row scope (review 01 F-03). A key alone never widens the rows; every row is also tenant-filtered and
another tenant's row is 404.

| Endpoint | Key | Rows the caller may see or change |
|---|---|---|
| `GET /me/requests`, `GET /me/balance` | `leave.requests.read` | the caller's own employee only (resolved by `/me`) |
| `POST /requests/preview`, `POST /requests` | `leave.requests.create` | the caller's own employee only |
| `GET /requests/{id}` — own branch | `leave.requests.read` | the caller's own request |
| `GET /requests/{id}` — approver branch | `leave.approvals.read` | a request whose MOD-0023 instance has an open task the caller may decide, or whose newest task the caller decided (MOD-0023 `MayDecide` over the assignment snapshot — FU01 `DecidableInstanceIdsAsync`, `TimesheetApprovalService.cs:64–77`) |
| `GET /requests/{id}` — HR branch | `leave.admin-requests.read` | any request of the tenant (D-1/3) |
| `POST /requests/{id}/resubmit` | `leave.requests.create` | own request, `Pending` and `Unconfirmed` |
| `POST /requests/{id}/cancel` | `leave.requests.cancel` | own request, `Pending` or not started |
| `GET /admin/requests` | `leave.admin-requests.read` | every request of the tenant (D-1/3) |
| `POST /admin/requests/{id}/cancel` | `leave.admin-requests.cancel` | any request of the tenant, started, with reason code |
| `GET /types`, `GET /types/{id}` | `leave.types.read` | active types of the tenant (inactive ones only with `leave.types.manage`) |
| `POST /types`, `PUT /types/{id}`, `POST /types/{id}/activate`, `.../deactivate` | `leave.types.manage` | types of the tenant |
| `GET /entitlements` | `leave.entitlements.read` | every entitlement of the tenant |
| `PUT /entitlements`, `POST /entitlements/year-end`, `POST /entitlements/{id}/carry-over` | `leave.entitlements.manage` | entitlements of the tenant |
| `GET /admin/sync-runs`, `GET /admin/sync-runs/{id}/unmatched` | `leave.sync-runs.read` | runs and unmatched rows of the tenant |

A request outside every branch the caller holds is 404. The three branches are checked independently; none implies another.

- Read and write keys are separate (D-1/4). Scope: tenant-wide for HR in block 1 (D-1/3); legal-entity scope is a follow-up.
- The approver sees only requests they may decide or have decided, by MOD-0023's own rule (D-1/2); `leave.approvals.read`
  is granted with the manager role template, as FU01's `time-entry.approvals.read`.
- Notification events (manifest-declared; event key rule `^[a-z0-9]+(\.[a-z0-9]+)*$`): `leave.request.submitted`,
  `leave.request.approved`, `leave.request.rejected`, `leave.request.cancelled`. Payload: only the allowed fields of
  §Runtime Constraints 6.

## Audited Events

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateLeaveTypeCommand | leave.type.created | LeaveType | a | AfterState: Code, Category, ReducesBalance, RequiresApproval, CarryOverLimitDays | — |
| UpdateLeaveTypeCommand | leave.type.updated | LeaveType | a | Before/After: label, ReducesBalance, RequiresApproval, CarryOverLimitDays | — |
| ActivateLeaveTypeCommand | leave.type.activated | LeaveType | a | Before/After: IsActive | — |
| DeactivateLeaveTypeCommand | leave.type.deactivated | LeaveType | a | Before/After: IsActive | — |
| SetLeaveEntitlementCommand | leave.entitlement.set | LeaveEntitlement | a | Before/After: EntitledDays (EmployeeId, Year, LeaveTypeId) | — |
| RunLeaveYearEndCarryOverCommand | leave.entitlement.carried-over | LeaveEntitlement | a | per row Before/After: CarriedOverDays; run totals | — |
| AdjustLeaveCarryOverCommand | leave.entitlement.carry-over-adjusted | LeaveEntitlement | a | Before/After: CarriedOverDays; reason code | — |
| SubmitLeaveRequestCommand | leave.request.submitted | LeaveRequest | a | business: type, dates, half-day part, DayCount, Status · envelope: request id, SubmissionNumber, WorkflowStartState | — |
| ResubmitLeaveRequestCommand | leave.request.resubmitted | LeaveRequest | a | business: Status · envelope: request id, SubmissionNumber before/after, retired instance outcome code, WorkflowStartState | — |
| CancelLeaveRequestCommand | leave.request.cancelled | LeaveRequest | a | business: Status before/after, released DayCount · envelope: request id, cancel path code (`pending-workflow` · `approved-not-started`), workflow outcome code | — |
| CancelStartedLeaveRequestCommand | leave.request.cancelled-by-hr | LeaveRequest | a | business: Status before/after, released DayCount · envelope: request id, cancel reason code | — |
| FinalizeLeaveDecisionCommand | leave.request.decided | LeaveRequest | a | business: Status before/after · envelope: request id, workflow outcome code | — |
| SyncLeaveEmployeeSnapshotsCommand | leave.employee-sync.completed | LeaveSyncRun | a | run counts; no personal fields | — |

- **Per-event allowed fields (review 02 F-09).** The *business* part of a request event is the allowed-field list of
  §Runtime Constraints 6 (type, dates, half-day part, day count, status). The *envelope* part carries only identifiers and
  closed code lists: request id, submission number, workflow start state, workflow outcome code, cancel reason code — no
  free text. Every record also carries AUD-001 §3's standard actor envelope (actor id, actor type, masked display, tenant,
  correlation id) — that is the audit standard's own field set, not business payload. AC-5 governs the business part;
  AC-17 and AC-38 check the envelope and technical fields against this table. No person name in the business part, no
  document, no health or contact data anywhere. Type catalogue events carry the label (catalogue data).
- No command is K2 class in block 1 (no identity, permission, tenant-state, GxP or special-category personal data);
  sickness (special category) arrives only with block 2 and is K2 there.

## Gateway / API Routing Decision

```text
Decision: gateway change REQUIRED → integration-agent task (protected path); delivered and proven end to end in DL-7; needed before DL-5 goes live.
```

| Upstream | Downstream | Host | Methods |
|---|---|---|---|
| `/api/v1/leave` | `/api/v1/leave` | `localhost:5057` | GET, POST, PUT, PATCH, DELETE, OPTIONS |
| `/api/v1/leave/{everything}` | `/api/v1/leave/{everything}` | `localhost:5057` | GET, POST, PUT, PATCH, DELETE, OPTIONS |

One prefix on purpose: on extraction (ADR-005) the pair is re-pointed and nothing else changes. The existing
`time-attendance-leave` route (→ 5063) is not reused.

## Acceptance Criteria

- [ ] AC-1 (zorunlu) — Identity `MOD-0280-FU02`, parent `MOD-0280`; `verify_module_id.py . --check-id MOD-0280-FU02 --name "Leave & Absence Management" --parent MOD-0280` exits 0 at the start of DL-1.
- [ ] AC-2 (zorunlu) — Every `leave_*` collection is tenant-scoped with tenant-first indexes; a request, type or entitlement of another tenant returns 404 with no metadata.
- [ ] AC-3 (zorunlu) — An employee sees only their own requests and balance; a manager sees only requests routed to them by MOD-0023; HR with `leave.admin-requests.read` sees every request of the tenant.
- [ ] AC-4 (zorunlu) — The `health` category cannot be created or set through the UI, the API or any seed; the attempt returns 400 `LEAVE_TYPE_CATEGORY_LOCKED`.
- [ ] AC-5 (zorunlu) — The request form has no free-text and no document field; the **business payload** of the Task Center card, notification e-mail, notification payload and request audit events carries only the allowed fields of §Runtime Constraints 6 (type code and label, start, end, half-day part, day count, status, request id) and never the person's name; the audit actor envelope is governed by AC-38.
- [ ] AC-6 (zorunlu) — Day count excludes weekends and public holidays from MOD-0362 and counts a half holiday as half; a half-day request is accepted only for a single day.
- [ ] AC-7 (zorunlu) — A pending request reserves its days; remaining = entitled + carried − reserved − taken; a request above remaining returns 409 `LEAVE_BALANCE_INSUFFICIENT`; two concurrent submits never make a balance negative.
- [ ] AC-8 (zorunlu) — An overlapping request of the same employee returns 409 `LEAVE_REQUEST_OVERLAP`.
- [ ] AC-9 (zorunlu) — Approval runs through MOD-0023 with the FU01 approver rules (line manager, next seat, pool, never self); no approver → 409 `LEAVE_NO_APPROVER`; never auto-approved when the type requires approval.
- [ ] AC-10 (zorunlu) — A decision is applied exactly once, by the sweep job or at read time; decision and cancellation races leave exactly one outcome.
- [ ] AC-11 (zorunlu) — Cancelling a pending or not-yet-started approved request refunds its days; a started request can be cancelled only by HR with a reason code.
- [ ] AC-12 (zorunlu) — A request stores its type snapshot; later changes to the type or the entitlement do not change approved or past requests.
- [ ] AC-13 (zorunlu) — A request crossing 31 December is split into two year allocations; year-end carry-over moves remaining days up to the type limit and runs once per year; adjustments need a reason code.
- [ ] AC-14 (zorunlu) — When the employee service is unreachable, submit returns 503 `LEAVE_EMPLOYEE_SERVICE_UNAVAILABLE` and writes nothing; existing requests and balances still load.
- [ ] AC-15 (zorunlu) — The employee adapter is tested against the frozen HCM-CORE-BUNDLE version with a contract test on both producer and consumer side; an unknown contract version is refused.
- [ ] AC-16 (zorunlu) — Every sync run writes a `LeaveSyncRun` with read / changed / unmatched counts and is visible to `leave.sync-runs.read`.
- [ ] AC-17 (zorunlu) — Every write command in §Audited Events emits its audit event exactly once with the listed fields.
- [ ] AC-18 (zorunlu) — `LeaveTypes`, `LeaveEntitlements` and `Leave` pages pass `verify_datatable_page.py` with the pack's `data_mode`; every `.cshtml` writes `Layout = "_LayoutTenantShell"` explicitly.
- [ ] AC-19 (zorunlu) — All user-visible text exists in 7 languages (en, tr, fr, es, zh, ar, ru); `resx-check` reports no missing key; page help keys `Help.MYLEAVE.*`, `Help.LEAVETYPES.*`, `Help.LEAVEENTITLEMENTS.*` exist (UXH-001).
- [ ] AC-20 (zorunlu) — A user without the page permission sees the single explanation panel, no skeleton and no redirect (UAS-001).
- [ ] AC-21 (zorunlu) — Pages, actions, permissions and notification events come from `LeaveManifestProvider`; nothing is hand-seeded.
- [ ] AC-22 (zorunlu) — The gateway pair `/api/v1/leave` and `/api/v1/leave/{everything}` → 5057 exists before DL-5 is accepted.
- [ ] AC-23 (zorunlu) — A submit writes the request, every affected balance row and the employee lock in one transaction; a failure injected after the first balance write leaves no request and no reservation (sabotage evidence).
- [ ] AC-24 (zorunlu) — A workflow start that fails or whose answer is lost leaves the request `Pending`/`Unconfirmed` with its days reserved and nothing retries on its own; the employee's resubmit retires the previous submission's instance by key (cancelled if open, applied if decided) before starting the next; days are released only after MOD-0023 confirms no open task and no decision.
- [ ] AC-25 (zorunlu) — Two concurrent overlapping submits of one employee for a type that does not reduce the balance store exactly one request.
- [ ] AC-26 (zorunlu) — A year-end carry-over run stopped halfway and run again applies every row exactly once: mark, next year's carried days and both balance rows are written in one transaction per row.
- [ ] AC-27 (zorunlu) — A change that would leave `EntitledDays + CarriedOverDays` below `TakenDays + ReservedDays` returns 409 `LEAVE_ENTITLEMENT_BELOW_USED` and writes nothing (entitled 5 + carried 5 with taken 8 is accepted; entitled 2 is refused); approving reserved days does not lower the floor; approved requests are never re-evaluated.
- [ ] AC-28 (zorunlu) — An employee holding only the baseline employee keys can list active leave types through `leave.types.read` and cannot reach any `leave.types.manage` endpoint (403).
- [ ] AC-29 (zorunlu) — Every endpoint enforces the row scope of the §Authorization Convention table; `GET /requests/{id}` serves the own, approver and HR branches independently; a manager with `leave.approvals.read` reads only requests they may decide or have decided, otherwise 404; HR with `leave.admin-requests.read` reads any request of the tenant.
- [ ] AC-30 (zorunlu) — A sync run with unmatched employees writes one `LeaveSyncUnmatched` row per employee with a reason code and no other personal field; `GET /admin/sync-runs/{id}/unmatched` returns them only to `leave.sync-runs.read`.
- [ ] AC-31 (zorunlu) — Every DataTable screen passes the `quality-gate-datatable` workflow in addition to `verify_datatable_page.py`.
- [ ] AC-32 (zorunlu) — Platform, Web and Gateway build; a request through Gateway 5000 reaches `api/v1/leave` on Platform 5057 with the caller's tenant and permissions (end-to-end, DL-7).
- [ ] AC-33 (zorunlu) — A submit whose commit answer is lost returns 503 `LEAVE_SUBMIT_OUTCOME_UNKNOWN`; retrying with the same `SubmissionKey` yields exactly one request and one reservation whether or not the first commit happened (fault-probe evidence on the executor's commit).
- [ ] AC-34 (zorunlu) — No cancelled request ever has an open MOD-0023 task: race evidence with an older start paused (a) before instance allocation and (b) after allocation but before its task write, each resumed after a cancel, shows the cancel's settle closed the one instance for the key and the resumed start wrote no task; days released exactly once.
- [ ] AC-35 (zorunlu) — Every MOD-0023 start and cancel for a leave request runs as the employee who submitted it; no background job starts, retries or cancels an instance (MOD-0023 `SameStarter` honoured).
- [ ] AC-36 (zorunlu) — Cancelling an approved, not-started leave (employee) or a started leave (HR, reason code) leaves the MOD-0023 instance approved and untouched, sets the request `Cancelled` and returns its taken days in one transaction.
- [ ] AC-37 (zorunlu) — A resubmit starts a new submission only after the settle of the current one answered closed; decided → the decision is applied and nothing new starts; a refused replay (facts conflict) → 409 `LEAVE_APPROVAL_NEEDS_RECONCILIATION`, nothing released, the request on HR's reconciliation list.
- [ ] AC-38 (zorunlu) — Every leave audit record carries the AUD-001 §3 actor envelope with a masked display and only the closed technical fields of §Audited Events; no free text appears in either.
- [ ] AC-39 (zorunlu) — Settle replays `StartAsync` with the stored `StartFacts` byte-for-byte (same fingerprint inputs) as the submitting employee; every replay result maps to exactly one of closed, decided, unsettled; days are released only on closed.
- [ ] AC-40 (zorunlu) — The start lease is never a precondition for a release: with the lease forcibly expired during a paused start, the settle still prevents a second task and a release before the instance is closed.

## Test Expectations

- Unit: validators; `LeaveDayCounter` (weekend, holiday, half holiday, half-day request, 31 Dec split); `LeaveBalanceLedger`
  (reserve, take, refund, carry-over, never negative); approver resolution (chain, pool, self excluded, null); category lock.
- Integration (Mongo): tenant isolation and 404; soft delete; unique indexes (code, entitlement, balance); concurrent submit
  (one wins); decision replay (one effect); decision vs cancel race.
- Contract: consumer test of `EmployeeGatewayAdapter` against the frozen HCM-CORE-BUNDLE fixture; producer test is the
  CT · Çalışan kaydı's (ADR-005 addendum 1/1).
- Cross-module: MOD-0023 start / approve / reject / cancel with the Task Center card; MOD-0362 holiday read.
- Frontend: smoke of the three pages; `verify_datatable_page.py` and the `quality-gate-datatable` workflow for each screen;
  `resx-check` 7 languages; UAS-001 panel.
- Transactions: injected failure after the first balance write (rollback), after commit before workflow start (retry and
  compensation), carry-over stopped halfway (resume once).
- Build: Platform, Web and Gateway; known-failures comparison (`scripts/known_failures.py`) on the closing run.
- Sabotage evidence per guard: health category, balance check, overlap check and tenant filter each shown red with the guard
  removed and green with it (CT writes the evidence script for HIGH work, card S-83).

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt |
|---|---|---|---|
| DL-1 | izin türleri ve izin hakları (arka uç) | Owned Objects, Entity Fields, Validation Rules, Audited Events | E4 |
| DL-2 | izin talebi, gün sayısı ve bakiye defteri (arka uç) | Owned Objects, Entity Fields, Validation Rules, Failure Path to Verify, Runtime Constraints | E4 |
| DL-3 | onay akışı, onaylayan ve Görev Merkezi kartı | Ownership and Boundaries, Authorization Convention, Failure Path to Verify | E5 |
| DL-4 | çalışan sözleşmesi tüketicisi, eşitleme ve izleme | Dependencies, Runtime Constraints, Test Expectations | E5 |
| DL-5 | üç sayfa, 7 dil ve yardım metinleri | Layout & Shell Contract, Frontend File Contract | E4 |
| DL-6 | modül kaydı ve izin anahtarları | Authorization Convention, Layout & Shell Contract | E4 |
| DL-7 | gateway rotası ve uçtan uca bağlantı | Gateway / API Routing Decision, Test Expectations | E5 |

`gerekli kanıt` is the minimum evidence level (SOP §23.1, card S-85) an acceptance record of a work package of that slice
must reach; the acceptance record cites this table row as `required_evidence_source`.

Gates per slice (review 01 F-06) — development with the fake employee adapter is allowed before a gate opens; dispatch or
acceptance waits as written:

- DL-1, DL-2, DL-3: dispatch after this pack is approved and the registry rows are in force on origin/main. Day boundary
  (K-9): the tenant time zone (FU01 R3) is the **owner-decided default** (`rev03-gonder`, 2026-10-29); DL-2 is accepted with
  it; a later K-9 decision changes it only through a pack revision. DL-3 dispatch needs the coordination note with the
  MOD-0023 tower (card S-90).
- DL-4: development and DL-1…DL-3 use the fake adapter and are **not** gated by the contract. Only DL-4 **acceptance** with
  real data needs HCM-CORE-BUNDLE frozen at a named version, MOD-0251's activation slice, the K-1 user ↔ person link and the
  K-4 service identity; until then only fake-adapter evidence, which is not acceptance.
- DL-5: the `LeaveTypes` and `LeaveEntitlements` screens are dispatched only after Master has decided the Blueprint MCR
  (`docs/records/decisions/2026-10/hcm-leave-mcr-draft-01.md`) or the owner records an exception; the `Leave` screen
  ("Leave Request", already in Blueprint) is not gated by the MCR.
- DL-6, DL-7: XMC-001 consent for code in `Diten.Platform` (platform-shared-services owner, card S-90); DL-7 also needs
  the integration-agent task for `ocelot.json`.

## Ready-for-dev Checklist

- [ ] Golden Reference Slim read
- [ ] Frontmatter complete (service, shell, golden_reference, screens, entity_base)
- [ ] Layout & Shell Contract writes the Razor layout explicitly
- [ ] Backend File Convention matches the Golden Reference
- [ ] Frontend File Contract lists the Slim files per screen
- [ ] Validation Rules cover every field and action
- [ ] Failure Path to Verify has duplicate, missing, unauthorized and concurrency cases
- [ ] Authorization Convention lists permissions, policy and actor scope
- [ ] Gateway routing decision written; integration-agent task opened
- [ ] Acceptance criteria testable; Test Expectations cover build, verifier, resx and smoke
- [ ] Audited Events covers every write command once
- [ ] Page help keys for `MYLEAVE`, `LEAVETYPES`, `LEAVEENTITLEMENTS` in 7 languages; tour: none (leave is not one of the five tour flows)
- [ ] Every slice carries a required evidence level (card S-85) and its gates are written (§Teslim dilimleri)
- [ ] Coordination note with the MOD-0023 tower recorded before DL-3 (card S-90)
- [ ] Tenant-zone day boundary recorded as the owner-decided default (`rev03-gonder`) — K-9 changes it only by revision
- [ ] Master decision on the Blueprint MCR, or an owner exception, before the LeaveTypes / LeaveEntitlements screens
- [ ] HCM-CORE-BUNDLE freeze is **not** a ready-for-dev condition; it gates only DL-4 acceptance (§Teslim dilimleri)
- [ ] XMC-001 consent for code in `Diten.Platform` from the platform-shared-services owner recorded
- [ ] HCM domain-config lists MOD-0280-FU02 with the ADR-005 deviation
- [ ] Independent pack review (SOP §20.4) UYGUN and an approval row by an approver with `high: evet` who did not write the pack

## Implementation Notes

- Revision 01 (2026-10-27): first draft on the owner's word `paket-taslak`. Every decision above cites its source (D-1, D-2,
  D-3, ADR-005 + addendum 1, discovery decisions). Code patterns are copied from MOD-0280-FU01, not shared.
- The balance is a ledger, not a computed sum, so that a submit can reserve days with one conditional write (D-3/1, D-3/2).
- The employee snapshot exists for HR lists and monitoring only; a new request always asks the employee service with `asOf`.
- **Revision 02** (2026-10-29, owner's word `rev02-oneri`; review `docs/records/reviews/MOD-0280-FU02/2026-10-29-ea61e8c6.md`):
  F-01 allowed-field list and type-label risk (§Runtime Constraints 6, AC-5); F-02 one transaction with employee lock,
  post-commit workflow start with retry and compensation, resumable carry-over (§Owned Objects, §Runtime Constraints 9,
  §Failure Path to Verify, AC-23…AC-26); F-03 `leave.types.read` and the endpoint scope table (AC-28, AC-29); F-04 slice
  split: DL-6 E4, DL-7 E5; F-05 reduction floor (AC-27); F-06 gates per slice; F-07 unmatched report (AC-30); F-08 target
  2026-12-15, `quality-gate-datatable`, Gateway build (AC-31, AC-32). AC-1…AC-22 keep their numbers; AC-5 text widened.
- **Revision 03** (2026-10-29, owner's word `rev03-gonder`; review `docs/records/reviews/MOD-0280-FU02/2026-10-29-47df0fc4.md`):
  root cause of F-02/F-10/F-11/F-12/F-16 was a new start/compensation protocol; it is replaced by FU01's live protocol
  (§Runtime Constraints 9). F-09 per-event allowed fields (§Audited Events); F-10 no automatic release, release only after
  MOD-0023 confirms, orphan retired on resubmit (AC-24, AC-34); F-11 starts and cancels only as the employee (AC-35); F-12
  carry-over mark in the row's transaction (AC-26); F-13 three read branches and `leave.approvals.read` (AC-29); F-14 floor
  examples and AC-27; F-15 contract freeze gates only DL-4 acceptance, tenant-zone default owner-decided; F-16 unique
  `SubmissionKey` and AC-33. AC-24, AC-26, AC-27, AC-29 texts changed, numbers kept; AC-33…AC-35 added.
- **Revision 04** (2026-10-29, owner's word `kilit-gonder`; review `docs/records/reviews/MOD-0280-FU02/2026-10-29-436bc38b.md`;
  reassessment `docs/records/decisions/2026-10/hcm-leave-architecture-reassessment-01.md`): F-17 start lease, no
  warn-and-continue on an orphan, late-decision path removed (§Runtime Constraints 9, AC-34, AC-37); F-18 business payload
  vs AUD-001 actor envelope (§Runtime Constraints 6, §Audited Events, AC-5, AC-38); F-19 pending-workflow cancel vs
  approved-leave business cancel (AC-36). AC-5 and AC-34 texts changed, AC-35 narrowed to the employee; AC-36…AC-38 added.
  If this root cause fails review again, path 2 of the reassessment (MOD-0023 guarded start) opens with the MOD-0023 tower.
- **Revision 05** (2026-10-29, owner's word `yeniden-oynat`; review `docs/records/reviews/MOD-0280-FU02/2026-10-29-5fb4d1bb.md`;
  reassessment addendum 2): F-20 answered without a new MOD-0023 capability — settle by replaying the start with stored
  facts (§Runtime Constraints 9; `StartFacts`, `NeedsReconciliation`); the lease is a courtesy only. AC-34 and AC-37 texts
  changed (numbers kept); AC-39, AC-40 added. MOD-0023 design request 01 stays open as an improvement, not a gate.

## Follow-up Items

- Block 2 (HCM-LEAVE-07): absence record by HR; sickness / medical report types, document upload and description — only
  after the MOD-0314 rule and legal approval; K2 audit class for special-category data.
- Timesheet link (HCM-LEAVE-08): approved leave days read-only in MOD-0280-FU01's week (XMC-001).
- Legal-entity-scoped HR access; anniversary- and seniority-based statutory entitlement (Turkish labour law — needs a legal
  source; CT's general knowledge only); partial refund on early return; delegation while the manager is on leave.
- Attendance calendar, schedules / rosters, MOD-0315 HR views, Task Center "on leave" warning, meeting "on leave" status.
- Extraction to `Diten.HcmService` with FU01 (ADR-005 trigger).

## Open Questions

1. **XMC-001 approver** for code in `Diten.Platform` (platform-shared-services) — who signs? Card S-90 (2026-10-28): the
   requesting tower writes, the module owner reviews and approves; consent is per addition.
2. **HCM-CORE-BUNDLE freeze** — which version, and K-1 (who holds the user ↔ person link) — CT · Çalışan kaydı + owner.
3. **K-4 service identity** for the sync job (Auth, another module).
4. **K-9 time zone** for `asOf` and day boundaries (tenant vs legal entity).
5. **Blueprint MCR** — the owner sends `hcm-leave-mcr-draft-01.md` to Master; Leave Types and Leave Entitlements pages
   depend on it (Master-first).

## Recorded Deviations

| # | Rule | Deviation | Record |
|---|---|---|---|
| R-1 | HCM domain-config: backend `Diten.HcmService` | runs in `Diten.Platform` | ADR-005 |
| R-2 | Blueprint HARD dependencies MOD-0003, MOD-0037 | interim contract-in-repo and leave-side monitoring | ADR-005 addendum 1 |
| R-3 | Blueprint placement "Domain App (HCM Time & Leave)" | Platform module, extraction path | ADR-005 |
| R-4 | Blueprint Module Pages for MOD-0280 (stale external pages) | native pages used; Leave Types / Leave Entitlements not yet in Master | MCR draft (HCM-LEAVE-03) |
