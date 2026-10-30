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
status: ready-for-dev
owner: ali.tufanoglu
branch: feature/hcm/mod-0280-fu02-leave-absence-management
started: 2026-10-27
target: 2026-12-15
form_field_count: 7               # largest form: Leave Type (§Entity Fields)
---

# MOD-0280-FU02 — Leave & Absence Management

> **Status: ready-for-dev (revision 10).** Code is written only slice by slice, after this exact version's approval row
> is in force on origin/main and the slice's gates in §Teslim dilimleri are open — among them XMC-001 consent for every
> slice that writes code in `Diten.Platform`, and for DL-0 in AuthService (module-pack-standard §16). Revision 01, written by CT · İzin ve devamsızlık
> on the owner's word `paket-taslak` (2026-10-27).
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
> **Revision 06** (2026-10-29, owner's word `rev06-gonder`) answers review 05
> (`docs/records/reviews/MOD-0280-FU02/2026-10-29-948f931b.md`, KOŞULLU, F-21) and the liveness points of an interim
> sub-agent check: the definition-version limit is stated as MOD-0023 really behaves, the full fingerprint inputs are fixed,
> the settle cancel allows an escalated task with a fixed cancel key, and every settle or re-check maps the **re-read
> instance status** (never the shared 409) to closed / decided / unsettled; a read-only re-check (job, read path, HR
> endpoint) leads out of `NeedsReconciliation`; the re-check has its own branch for `Unconfirmed`, the settle cancels the
> instance's open task of any step, the employee's intent is stored (`PendingIntent`), a passive closure withdraws the
> request, settle / re-check / start-result races are version-guarded, only a `Pending` request is ever re-checked or
> finalized (leaving `Pending` sets `WorkflowStartState = Closed`), every path has one audit event, HR has a
> reconciliation list and the cases that can still stay reserved are named (§Implementation Notes "Revision 06").
> **Revision 07** (2026-10-29) answers review 06 (`docs/records/reviews/MOD-0280-FU02/2026-10-29-878a24f0.md`, KOŞULLU,
> F-67): a closed instance found by the re-check of an `Unconfirmed` request ends `Cancelled` when the stored intent is
> `cancel`, everywhere the rule is written (§Implementation Notes "Revision 07").
> **Revision 08** (2026-10-29, owner's word `rev08-hazir`) after review 07
> (`docs/records/reviews/MOD-0280-FU02/2026-10-29-0517be85.md`, UYGUN): status `ready-for-dev`; the Ready-for-dev Checklist
> is filled in and names which items are slice gates (§Teslim dilimleri) rather than conditions of this status; the
> XMC-001 gate covers every slice that writes code in `Diten.Platform`; validation rows, two failure paths, the lookup
> decision and two recorded deviations are added; after review 08 the two reason-code lists and the page-help contract
> are written; no runtime rule changed, of the AC texts only AC-11, AC-13, AC-19 and AC-55 (§Implementation Notes
> "Revision 08").
> **Revision 09** (2026-10-30, owner's choice "Paketi düzelt") after the approval of revision 08: §Repo Scope is measured
> against `Diten.Platform`'s real project layout (there is no `Persistence` project; repositories and schema live in
> `Infrastructure/Persistence/`) and names the shared registration files a new module adds lines to, as
> MOD-0280-FU01 did; the audit category is written; the Task Center card's owner is corrected; the XMC-001 consent is
> recorded. No runtime rule, entity, endpoint, permission or AC changed (§Implementation Notes "Revision 09").
> **Revision 10** (owner's choice "Yalnız açık izin", 2026-10-31) corrects revision 09's sentence that no role template gives
> the leave keys: AuthService's default `Viewer` takes every tenant `read` key, so the three HR read keys become
> explicit-grant only in a new slice DL-0 (`docs/records/decisions/2026-10/hcm-leave-xmc001-authservice-consent-01.md`, AC-58). It also closes gaps the first work package's read found: reason
> codes and rules the validation table did not name, the closed employee adapter, and how audit names and values are carried
> (§Implementation Notes "Revision 10").
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
| Approval workflow and decisions | **MOD-0023 Workflow Designer** | same pattern as `TimesheetApprovalService`; this module exposes no approve endpoint; MOD-0023 carries only the start's display context (title, subtitle, labels; MOD-0023 tower's answer `docs/records/decisions/2026-10/hcm-leave-mod0023-s90-answer-01.md`) |
| Task Center card | **Task Center** (`Features/WorkAggregation`, platform-shared-services) | leave's own `LeaveApprovalSourceResolver : IApprovalSourceResolver` (`Features/WorkAggregation/Services/IApprovalSourceResolver.cs`) in `Features/Leave/Providers`, FU01 `Features/TimeEntry/Providers/TimesheetApprovalSourceResolver.cs` pattern; no Task Center code changes |
| Approver chain | **MOD-0288** `PositionAssignment` (contract §4, K-8 proposal) | `ILeaveOrgGateway`, FU01 `ApproverResolver` pattern |
| Working days, holidays, half-days | **MOD-0362 Working Calendar & Public Holidays** | `IWorkingHoursProvider` (read) |
| Audit, notifications, scheduler, permissions | FG-005 audit · MOD-0027 · Hangfire seam · MOD-0018 | existing seams, unchanged; AuthService's explicit-grant list gains three keys and nothing else (slice DL-0) |
| HR views over these records | **MOD-0315 Time, Attendance & Leave Workspace** | not built here |

### Not to be confused with

- **DitenHumanCapitalService "Time, Attendance & Leave"** (`hcm.time-attendance-leave.*`, route `time-attendance-leave`
  → 5063) — a readiness-record module that refuses leave requests and balances (`TimeAttendanceLeaveGuard.cs:156,161`).
  Untouched; its route is not reused (discovery decision 8).
- **MOD-0280-FU01** — same parent, same service, separate collections, keys and module code; no shared table.

### Placement deviation (ADR-005)

Runs in `Diten.Platform` (platform-shared-services' service) although HCM domain-config names `Diten.HcmService`. Reason,
extraction path (data move, together with FU01) and trigger are in ADR-005. Writing code in `Diten.Platform` needs XMC-001
consent — **recorded** 2026-10-30: the owner consented, for the paths of §Repo Scope only (`docs/records/decisions/2026-10/hcm-leave-xmc001-platform-consent-01.md`; Open Question 1).

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
| `LeaveCarryOverMark` | `leave_carry_over_marks` | One per (tenant, employee, closing year, leave type), unique index: written in the **same transaction** as the next year's `CarriedOverDays`, the closing year's `CarriedOutDays` and both `LeaveBalance` rows, so a resumed run never skips or repeats a row (review 02 F-12) |

### CQRS (Golden Reference convention; one Feature folder `Features/Leave/`)

- Commands (types): `CreateLeaveType`, `UpdateLeaveType`, `ActivateLeaveType`, `DeactivateLeaveType`.
- Commands (entitlements): `SetLeaveEntitlement` (create or update one row), `RunLeaveYearEndCarryOver` (HR, one year at a
  time), `AdjustLeaveCarryOver` (HR, with reason code).
- Commands (requests): `SubmitLeaveRequest`, `CancelLeaveRequest` (own; pending or not yet started),
  `CancelStartedLeaveRequest` (HR), `ResubmitLeaveRequest` (own — settles the current submission and starts the next
  only on closed), `FinalizeLeaveDecision` (system or read path — `Pending` + `Started` requests only, read by the
  recorded instance id; applies MOD-0023's outcome once), `RecheckLeaveSettlement` (system, read path or HR — `Pending`
  requests in `Unconfirmed` or `NeedsReconciliation` only, read by key; never writes to MOD-0023).
- Commands (system): `SyncLeaveEmployeeSnapshots`.
- Queries: `GetMyLeaveRequests`, `GetMyLeaveBalance`, `GetLeaveRequestById`, `GetLeaveRequestList` (HR),
  `PreviewLeaveDayCount`, `GetLeaveTypeList`, `GetLeaveTypeById`, `GetLeaveEntitlementList`, `GetLeaveSyncRunList`.

Nothing is hard-deleted: types are deactivated, requests are cancelled, entitlements are set to a new value (audited).

### Ports, providers, services

| Object | Where | Purpose |
|---|---|---|
| `ILeaveEmployeeGateway` | `Features/Leave` (port) + `Adapters/` | HCM-CORE-BUNDLE: `/me/employment-summary?asOf`, `/{employeeId}/employment-summary?asOf`, `/employment-summaries?updatedSince`. Fake adapter for development and tests; the HTTP adapter is written against the frozen contract version only; until DL-4 production registers the closed `UnavailableLeaveEmployeeGateway`, which always answers "unavailable", never the fake; the module's registration adds it with `TryAdd`, and only a test host registers the fake first |
| `ILeaveOrgGateway` | same pattern | Primary seat → `ReportsToPositionId` chain → holders; approver pool (FU01 pattern) |
| `LeaveApproverResolver` | `Features/Leave/Services` | FU01 `ApproverResolver` rules: line manager, next seat up, pool; never the person; null → submit refused |
| `LeaveApprovalService` | `Features/Leave/Services` | **Same shape as FU01 `TimesheetApprovalService`, plus `SettleAsync`** (reassessment 01 addendum 2: replay the start with the stored `StartFacts`, cancel an open task, answer closed / decided / unsettled); unlike FU01 `RetireSubmissionAsync` it never continues past an unsettled start (review 03 F-17, review 04 F-20) — reused from FU01's shape: `StartAsync`, `ReadDecisionAsync`, `DecidableInstanceIdsAsync`; FU01's `CancelAsync` and `RetireSubmissionAsync` are **not** reused. Starts the instance inside the **employee's own** submit or resubmit request, after the submit transaction commits, so the starter is the employee (MOD-0023 `SameStarter`); key `leave-request:{tenant}:{requestId}:{submissionNumber}`. **No system job starts or retries a start** (review 02 F-11). Cancels only inside `SettleAsync`, through MOD-0023's own cancel command; when the decision won, the decision is applied (FU01 F13) |
| `LeaveRequestWriter` | `Features/Leave/Services` | The one place a request changes state together with balances: request row, every affected `LeaveBalance` row and the `LeaveEmployeeLock` row are written in **one** `IPlatformTransactionExecutor` transaction. A failure inside the body rolls back; an **unknown commit result** (`PlatformTransactionUnavailableException`, executor :70–90) is answered 503 `LEAVE_SUBMIT_OUTCOME_UNKNOWN` and resolved by the client retrying with the same `SubmissionKey` (review 02 F-16) |
| `LeaveApprovalSourceResolver : IApprovalSourceResolver` | `Features/Leave/Providers` | Task Center card title: "Leave · {type label} · {from}–{to} · {days}" and a deep link — an app-relative path built inside the resolver, never a service port or an absolute origin (`IApprovalSourceResolver.cs:41`; FU01 `TimesheetApprovalSourceResolver.cs:69`); object type `leave-request`. The requester is shown by MOD-0023's own requester field, never in the title. Only the allowed fields of §Runtime Constraints 6 (D-2/3, review 01 F-01) |
| `LeaveDayCounter` | `Features/Leave/Services` | Working-day count over `IWorkingHoursProvider`; splits by calendar year (D-3/6, D-3/7) |
| `LeaveBalanceLedger` | `Features/Leave/Services` | The single writer of `LeaveBalance`; conditional writes on `Version` |
| `LeaveDecisionFinalizer` | `Features/Leave/Services` | The one step that applies a MOD-0023 outcome to a request (`FinalizeLeaveDecision`, and the re-check's "decided" branch); keyed per (instance, outcome) |
| `LeaveManifestProvider : IModuleManifestProvider` | `Features/Leave/SelfRegistration` | Module code `leave`, pages, actions, permissions, notification events |
| Jobs | `Features/Leave/BackgroundJobs` | `Diten.Platform.MOD-0280-FU02.LeaveDecisionSweepJob`, `Diten.Platform.MOD-0280-FU02.LeaveEmployeeSyncJob`; each behind its own `EnabledJobs[...]` flag; **correctness never depends on a job** (decisions are also finalized at read time) |

### API (`api/v1/leave`)

`GET /me/requests` · `GET /me/balance?year` · `POST /requests/preview` · `POST /requests` · `GET /requests/{id}` ·
`POST /requests/{id}/resubmit` · `POST /requests/{id}/cancel` · `GET /admin/requests` · `POST /admin/requests/{id}/cancel` ·
`POST /admin/requests/{id}/recheck` ·
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
| `LabelText` / `LabelResourceKey` | string | one of | label shown in 7 languages; `LabelResourceKey` for installed defaults; `LabelText` at most 200 characters |
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
| `Year` | int | yes | calendar year (D-3/7), 2000–2100 |
| `LeaveTypeId` | uuid | yes | active, `ReducesBalance = true` |
| `EntitledDays` | decimal(5,1) | yes | 0–365, step 0.5 |
| `CarriedOverDays` | decimal(5,1) | system | written by `RunLeaveYearEndCarryOver` / `AdjustLeaveCarryOver` only |
| — | | | a carried-over row is identified by its `LeaveCarryOverMark` (same transaction), not by a field on this row |

Unique index (`TenantId`, `EmployeeId`, `Year`, `LeaveTypeId`).

**Reduction floor (review 01 F-05, review 02 F-14; refines D-3/5):** after the change, `EntitledDays + CarriedOverDays`
must stay ≥ `TakenDays + ReservedDays + CarriedOutDays` of the same balance row; otherwise 409 `LEAVE_ENTITLEMENT_BELOW_USED`. The answer
carries the amounts, and lists the pending requests only to a caller who also holds `leave.admin-requests.read` (they are
other people's data, and `leave.entitlements.manage` does not open them; D-1/4). A balance therefore never goes negative and an approved request is never re-evaluated.
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
| `WorkflowStartState` | enum | `NotNeeded` (type needs no approval) · `Unconfirmed` (written by the submit or resubmit transaction; stays when the start failed or its answer was lost) · `Started` (instance id recorded) · `NeedsReconciliation` (a settle ended unsettled — e.g. a cancel MOD-0023 did not record, no instance and no approver at replay time, or — when settle step 4 is left out or refused — a refused or incomplete replay) · `Closed` (the request left `Pending`). **Every transition out of `Pending`** (`Approved`, `Rejected`, `Cancelled`, `Withdrawn`) sets `Closed` in the same transaction, so nothing re-checks, finalizes or lists it again. An `Unconfirmed` or `NeedsReconciliation` request keeps its days reserved until a settle or a re-check re-reads a final MOD-0023 status; no job ever starts, replays or cancels (review 02 F-10, F-11; revision 05, 06) |
| `PendingIntent` | enum | `none` · `cancel` · `resubmit` — what the employee asked for when a settle ended unsettled; written by the settle's lease write, before any MOD-0023 call, so it survives a settle that loses its final write or dies; the employee's latest cancel or resubmit overwrites it; cleared when the intent completes or a decision is applied (revision 06) |
| `StartLeaseId`, `StartLeaseUntilUtc` | uuid, instant | user-experience guard only (revision 05): while a start call of this request is running, cancel and resubmit answer 409 `LEAVE_APPROVAL_STARTING` instead of waiting. Correctness does **not** depend on it (§Runtime Constraints 9). The first start's lease is written in the submit or resubmit transaction; a settle writes its lease and `PendingIntent` in one conditional write, and the `Version` that write produces is the one the settle's final write is conditional on. The final write clears the lease in the same transaction; a lease is never released by a separate write (an expired one is ignored) |
| `StartFacts` | object | the exact facts of the first start of the current submission — every input of MOD-0023's start fingerprint (`StartWorkflowInstanceHandler.cs:750–769`): definition id, object type / id / ref, candidate approver user ids, reason code, comment-required, evidence-required and due-at — plus the submission key. Leave sends constants for reason code (none), comment-required (false — a reject-only comment rule is the definition's own option, not a start fact), evidence-required (false) and **no due-at**, as FU01 does (`TimesheetApprovalService.cs:166–178`); all are stored in the submit transaction and replayed verbatim (reassessment addendum 2; revision 06) |
| `WorkflowInstanceId`, `ApproverResolution` | | as FU01 (line manager / chain / pool) |
| `DecisionAppliedAtUtc`, `CancelledByUserId`, `CancelReason` | | `CancelReason` is a closed code list, never free text: `ENTERED_IN_ERROR` (the leave was recorded by mistake) · `NOT_TAKEN` (the employee did not take the leave) · `HR_DECISION` (cancelled by an HR decision). Required only on HR's cancel of a started leave; an employee's cancel stores none |

### `LeaveBalance`

`EmployeeId`, `Year`, `LeaveTypeId`, `EntitledDays`, `CarriedOverDays`, `ReservedDays` (pending), `TakenDays` (approved), `CarriedOutDays` (moved to the next year by the carry-over run), `CarryOverCorrectionNeeded`
(bool — days returned to this year after its carry-over ran);
remaining = entitled + carried − reserved − taken − carried out (D-3/1). Unique (`TenantId`, `EmployeeId`, `Year`, `LeaveTypeId`).
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

Only after `ready-for-dev` and XMC-001 consent (`docs/records/decisions/2026-10/hcm-leave-xmc001-platform-consent-01.md`; for DL-0 `docs/records/decisions/2026-10/hcm-leave-xmc001-authservice-consent-01.md`). Paths measured on 2026-10-30 against
`Diten.Platform`'s projects `API`, `Application`, `Domain` and `Infrastructure` (there is no `Persistence` project);
`src/` below is `services/Diten.Platform/src/`. Each slice's work package names the subset it writes.

- Leave's own files:
  - `src/Diten.Platform.Domain/Entities/Leave/**`, `src/Diten.Platform.Domain/Enums/Leave/**`
  - `src/Diten.Platform.Domain/Repositories/ILeaveRepositories.cs` (one file for every leave repository interface, FU01
    `ITimeEntryRepositories.cs` pattern)
  - `src/Diten.Platform.Application/Features/Leave/**`
  - `src/Diten.Platform.Infrastructure/Persistence/Repositories/LeaveRepositories.cs` (FU01 `TimeEntryRepositories.cs`
    pattern)
  - `src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Leave.cs` (the `leave_*` indexes, FU01
    `PlatformSchemaManifest.TimeEntry.cs` pattern)
  - `src/Diten.Platform.Infrastructure/Services/LeaveLinks.cs` (the absolute links in the four notification e-mails,
    from `FrontendBaseUrl`, FU01 `TimeEntryLinks.cs` pattern; the Task Center card's link is app-relative and built in
    `LeaveApprovalSourceResolver`, §Ports)
  - `src/Diten.Platform.API/Controllers/LeaveController.cs`
  - `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Leave/**`
- Shared registration files — **lines added only**; no existing line changes, moves or is removed; each addition names
  only leave types (FU01 precedent, MOD-0280-FU01 pack §5.1). Two edits count as additions: a trailing comma put on the
  list's last existing item so that the leave item can follow it (`SchemaProfile.cs`, `NotificationTemplateSeed.cs`,
  `PlatformRecurringJobRegistrar.cs` end their lists without one), and `using` lines for leave namespaces in the file
  that needs them:
  - `src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformCollections.cs` — the `leave_*` collection constants
  - `src/Diten.Platform.Infrastructure/Persistence/Schema/SchemaProfile.cs` — one new value `Leave`, after the highest
    existing value
  - `src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.cs` — one `.Concat(LeaveCollections)`
  - `src/Diten.Platform.Infrastructure/DependencyInjection.cs` — the leave repository registrations and `LeaveLinks`
  - `src/Diten.Platform.Application/DependencyInjection.cs` — one `services.AddLeaveModule();` and its `using`
  - `src/Diten.Platform.Application/BackgroundJobs/PlatformRecurringJobRegistrar.cs` — the two leave jobs (DL-3, DL-4),
    each behind its own `EnabledJobs` id
  - `src/Diten.Platform.Infrastructure/Persistence/Configurations/NotificationTemplateSeed.cs` — the templates of the four
    leave notification events in 7 languages (DL-3; Platform's template store, FU01 precedent — pages, actions and
    permissions still come only from the manifest, §Runtime Constraints 8)
- `frontend/Diten.Web/Controllers/LeaveController.cs`, `frontend/Diten.Web/Views/Leave/**`,
  `frontend/Diten.Web/wwwroot/assets/js/Leave/**`, `frontend/Diten.Web/Resources/Views/Leave/**` (7 languages)
- Web tests (FU01 pattern, measured 2026-10-30): `frontend/Diten.Web/tests/leave-*.test.js` and
  `frontend/Diten.Web.Tests/Controllers/Leave*Tests.cs`
- `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs` — only the entries of `MYLEAVE`, `LEAVETYPES`, `LEAVEENTITLEMENTS`;
  `frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` — only `Help.MYLEAVE.*`,
  `Help.LEAVETYPES.*`, `Help.LEAVEENTITLEMENTS.*`; `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx`
  — only `Nav.Page.MYLEAVE`, `Nav.Page.LEAVETYPES`, `Nav.Page.LEAVEENTITLEMENTS` (shared files: added keys only, card S-38)
- `execution/domains/human-capital-management/**` (this pack, domain-config line for FU02)
- Gateway pair: separate integration-agent task (§Gateway / API Routing Decision)
- AuthService, slice DL-0 only (XMC-001 consent `docs/records/decisions/2026-10/hcm-leave-xmc001-authservice-consent-01.md`):
  `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs` — three constants and
  three items of `Keys`, lines added only (a trailing comma on the set's last existing item counts as an addition);
  `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Users/AccountKindTests.cs` — the test that counts the
  set exactly: its name, its comments, its count and three new lines;
  `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/LeaveExplicitGrantPermissionTests.cs` (new)

## Protected Paths

- `.antigravity/**`; `frontend/Diten.Web/Views/Shared/_Layout.cshtml`; Archive folders
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent only)
- `services/Diten.HcmService/**` and every MOD-0251 file (CT · Çalışan kaydı)
- `services/Diten.HumanCapitalService/**` (readiness module, discovery decision 5)
- `services/Diten.AuthService/**`, except the DL-0 paths of §Repo Scope
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
| MOD-0023 abandoned-start recovery (`WorkflowStartRecoverySweepJob`, BL-549) | exists, **off by default**: it runs only where the operator enables `Diten.Platform.MOD-0023.WorkflowStartRecoverySweepJob` (`PlatformRecurringJobRegistrar.cs:169–180`) | not needed for correctness; where it is off, a task-less start on which no settle runs (or, without settle step 4, one no settle can complete) stays reserved (§Runtime Constraints 9, "Cases that stay reserved") |
| MOD-0023 `SupersedeWorkflowHalfStartCommand` | exists; used in process by the Tasks feature after a refused replay (`TaskApprovalService.cs:254–319`) | DL-3 (settle step 4) — the MOD-0023 tower confirms leave's use first (card S-90), for both cases leave needs: a refused replay and a task-less start left by a failed first start (e.g. 503, no approver); without it step 4 is left out |
| AuthService's explicit-grant set (`ExplicitGrantOnlyPermissions.Keys`) | exists; DL-0 adds the three leave keys | DL-1, DL-2, DL-4 (the slices whose code names those keys) |
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
3. **Fail closed when the employee service is unreachable** (ADR-005 addendum 1/3): `SubmitLeaveRequest` and `SetLeaveEntitlement` return 503
   `LEAVE_EMPLOYEE_SERVICE_UNAVAILABLE`; reads of existing requests and balances continue; the snapshot cache never opens a
   request.
4. **Correctness never depends on a scheduler**: a pending request whose MOD-0023 instance is decided or closed is
   finalized (or re-checked) on **every read of that request** — the employee's list and detail, HR's list and detail,
   the approver's read — as well as by the sweep job; an employee who left does not hold it up, because HR's reads run
   the same step.
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
     employee). Success records the instance id (`Started`); failure or lost answer leaves `Unconfirmed`. Nothing retries
     on its own. **The result write is conditional** on the request `Version` the submit or resubmit transaction wrote
     (same `SubmissionNumber`, `Pending`, `Unconfirmed`): a start call that paused while a cancel, a resubmit or a re-check
     changed the request writes nothing (review: interim sub-agent F-38). `FinalizeLeaveDecision` additionally reads only
     an instance whose idempotency key is the current submission's.
   - **Settle** (`LeaveApprovalService.SettleAsync`) — the only path that may write to MOD-0023 after the first start,
     always in the submitting employee's own request:
     1. Replay `StartAsync` with the stored `StartFacts` and the current submission's key (MOD-0023 completes an
        incomplete `Active` start for the same starter with exactly one task, `StartWorkflowInstanceHandler.cs:372–411`;
        a whole start is answered as it is, :385–391; a closed incomplete one answers the shared 409, :393–398, :562–567).
     2. If the replay returned a whole start, read the instance's **open task** with
        `IApprovalTaskRepository.ListByInstanceIdAsync` filtered to `WaitingApproval`, `WaitingEvidence` and `Escalated`,
        as FU01 does (`TimesheetApprovalService.cs:248–251`) — **not** `GetActiveByInstanceIdAsync`, whose filter leaves
        out `Escalated` (`WorkflowRepositories.cs:555–563`, :584–587) — whatever step it belongs to: MOD-0023 keeps
        at most one open task per instance, writing the next step's task only when the previous one is approved
        (`WorkflowTaskTransitionSupport.cs:214–246`). If one is open (waiting or escalated), cancel it with MOD-0023's own
        `CancelWorkflowTaskCommand`, `AllowEscalated: true`; a task cancel closes the whole instance (:664–671); idempotency
        key
        `leave-settle-cancel:{tenant}:{requestId}:{submissionNumber}:{taskId}` — fixed, so a cancel whose answer was lost is
        answered again by MOD-0023 instead of being repeated (`WorkflowTaskTransitionSupport.cs:622–630`).
     3. **Always re-read the instance by key** (`IWorkflowInstanceRepository.GetByIdempotencyKeyAsync`, tenant-scoped — the
        read MOD-0023's own start uses, `StartWorkflowInstanceHandler.cs:81`) and map **only its status** — never the
        replay's or the cancel's answer:

        | Re-read status | Result |
        |---|---|
        | `Cancelled` | **closed** |
        | `TimedOut` | **closed** — no task can be written and nobody decided |
        | `Completed` or `Approved` | **decided: approved** |
        | `Rejected` | **decided: rejected** |
        | `Active`, `Escalated`, `Pending`, or no instance — read after step 4 | **unsettled** → `NeedsReconciliation`, nothing released (`PendingIntent` already holds the employee's action) |

     4. **Half start** (revision 06): if the re-read says `Active` and MOD-0023's task store lists no task for the instance
        (the replay was refused or could not complete it), send MOD-0023's own `SupersedeWorkflowHalfStartCommand(instanceId)`
        in process. It closes a task-less, non-trusted `Active` instance at once — no age and no starter condition — and a
        paused first call loses its claim (`SupersedeWorkflowHalfStartCommand.cs:8–19`,
        `SupersedeWorkflowHalfStartHandler.cs:59–75`). The Tasks feature sends it after a refused replay
        (`TaskApprovalService.cs:254–319`); leave also sends it for a task-less start a failed first call left behind — the
        command makes no difference between the two.
        Then re-read and map again; a refusal (not a half start) changes nothing and the re-read decides. **Its use by leave
        needs the MOD-0023 tower's confirmation (card S-90) before DL-3**; without it, step 4 is left out and case (a) of
        "Cases that stay reserved" applies to the employee's own settle too.

     This map holds whatever the replay answered (review: interim sub-agent F-22): a replay that loses its claim to an older
     start returns the shared 409 even if an approver decided meanwhile; the re-read then says `Completed`/`Rejected` and the
     result is **decided**, never closed. A task-less "true", a failed cancel or a 409 alone is never success. FU01's
     `CancelAsync` and `RetireSubmissionAsync` are not used. After **closed**, no later write can open a task under that key: a
     paused older start either finds the closed instance (never reopened) or loses its task claim, which needs an `Active`
     instance at the expected version (`WorkflowRepositories.cs:431–442`, written in the start's transaction,
     `StartWorkflowInstanceHandler.cs:662–688`).
   - **Re-check** (`RecheckLeaveSettlement`, read-only): re-read the instance by the current key. It runs in the sweep
     job, on every read of the request (the employee's, HR's list and detail, the approver's) and on HR's
     `POST /admin/requests/{id}/recheck` (`leave.admin-requests.reconcile`). It writes nothing to MOD-0023, so it needs no starter. It runs only on a `Pending`
     request (a `Closed` one is never touched) and has one branch per state:

     | Request state | Re-read | Result |
     |---|---|---|
     | `Unconfirmed` | `Active` or `Escalated` with an open task | **adopted**: instance id recorded, `Started` (the first start worked, its answer was lost); from here `FinalizeLeaveDecision` owns it |
     | `Unconfirmed` | decided (`Completed`, `Approved`, `Rejected`) | the decision is applied |
     | `Unconfirmed` | closed (`Cancelled`, `TimedOut`) | with `PendingIntent = none` **withdrawn**: `Withdrawn`, recheck result code `withdrawn`; with a stored intent the intent completes instead (code `intent-completed`): `cancel` → `Cancelled`, `resubmit` → `Withdrawn`; days released once — a closed instance under the current key is final (never reopened, `StartWorkflowInstanceHandler.cs:393–398`; no task claim without `Active`, `WorkflowRepositories.cs:431–442`); the same rule `FinalizeLeaveDecision` applies to a `Started` request; after `Withdrawn` the employee may send a new request |
     | `Unconfirmed` | no instance, or an incomplete `Active` start | no change; stays `Unconfirmed`, so the employee's resubmit or cancel can settle it |
     | `NeedsReconciliation` | closed | `PendingIntent` completes (recheck result code `intent-completed`): `cancel` → `Cancelled`, days released; `resubmit` → `Withdrawn`, days released (a re-check cannot start a submission — only the employee can be the starter; after `Withdrawn` the employee may send a new request) |
     | `NeedsReconciliation` | decided | the decision is applied |
     | `NeedsReconciliation` | unsettled | no change; the employee may repeat the cancel or resubmit — the settle is idempotent (same start key, fixed cancel key) |

     An exit that completes the intent or applies a decision clears `PendingIntent`; an **adoption keeps it**, so a cancel
     that was under way is still mapped by `FinalizeLeaveDecision` (`PendingIntent = cancel` → `Cancelled`).
     `FinalizeLeaveDecision` never touches an `Unconfirmed` or `NeedsReconciliation` request and the re-check never touches
     a `Started` one.
   - **One audit event per path** (AC-17, AC-50):

     | Command | Result | Event · answer |
     |---|---|---|
     | `CancelLeaveRequest` (pending) | closed | `leave.request.cancelled` · 200 |
     | `CancelLeaveRequest` (pending) or `ResubmitLeaveRequest` | decided | `leave.request.decided` only · 409 `LEAVE_ALREADY_DECIDED` with the applied status |
     | `CancelLeaveRequest` (pending) or `ResubmitLeaveRequest` | unsettled | `leave.request.reconciliation-needed` when `WorkflowStartState` or `PendingIntent` differs from its value before the settle's lease write, nothing when a repeat ends the same · 409 `LEAVE_APPROVAL_NEEDS_RECONCILIATION` |
     | `ResubmitLeaveRequest` | closed | `leave.request.resubmitted` · 200 |
     | `RecheckLeaveSettlement` | adopted, withdrawn or intent completed | `leave.request.rechecked` (recheck result code) · HR endpoint 200 with the result code |
     | `RecheckLeaveSettlement` or `FinalizeLeaveDecision` | decided | `leave.request.decided` · HR endpoint 200 `decided` |
     | `FinalizeLeaveDecision` | closed (`TimedOut`, `Cancelled`) → `Cancelled` if `PendingIntent = cancel`, else `Withdrawn` | `leave.request.decided` (outcome code `cancelled` or `withdrawn`) |
     | any command | lost the version race | none · 409 `LEAVE_CONCURRENCY_CONFLICT` carrying the current `Status` and `WorkflowStartState` (the HR re-check endpoint answers the same) |
     | `RecheckLeaveSettlement` | no change | none · HR endpoint 200 `no-change` |
     | `RecheckLeaveSettlement` (HR) | request not `Pending`, or not `Unconfirmed` / `NeedsReconciliation` | none · 409 `LEAVE_RECHECK_NOT_APPLICABLE` |

     The sweep job and the read path have no answer of their own; they only write what the table says.

     `leave.request.decided` is keyed per (instance, outcome), so whichever path applies a decision writes it once.
   - **Concurrent start result, settle, re-check and finalize** (the sweep job and the employee at the same time): every
     final write of a start result, a settle, a re-check or a finalize is conditional on the request `Version` read
     **before** the MOD-0023 call, and every
     release bumps `LeaveEmployeeLock.Version` in the same transaction. The loser writes nothing, re-reads the request and
     answers its current state; days are released once and at most one new submission starts (AC-51).
   - **Cases that stay reserved** (safe side; each is on HR's reconciliation list, §Frontend File Contract; AC-52):
     (a) an incomplete, task-less `Active` start — a replay refused for a facts conflict (only an incomplete start is
     fingerprint-checked; a whole start is answered as it is, `StartWorkflowInstanceHandler.cs:385–391`) or one no settle can
     complete (no eligible approver; an incomplete start is completed with the version stored on the instance,
     `StartWorkflowInstanceHandler.cs:441–446`, so a missing active version is not a cause here). No decision and no SLA
     timeout can reach it — both work
     on a task (`RunWorkflowEscalationsHandler.cs:288–293`). The employee's own settle closes it through step 4 (once the
     MOD-0023 tower confirms). It stays only while no settle runs — the employee cannot or does not act — and then leaves
     when the paused first call writes its task after all (an `Unconfirmed` request is adopted by the re-check; a
     `NeedsReconciliation` one waits for the decision, which the re-check applies, or for the employee's next settle), or
     when MOD-0023's abandoned-start recovery closes it — **off by default** (§Dependencies) — after which the re-check
     withdraws the request or completes the intent (`cancel` → `Cancelled`). (b) No instance at all: the first start never
     allocated one and every replay fails (no approver, or no active definition version — a new start needs one). The recovery cannot help — it closes only task-less `Active`
     instances (`CloseAbandonedWorkflowStartHandler.cs:80–85`); it leaves only through the employee's own resubmit or cancel
     once the cause is fixed. (c) Either case when the employee can no longer act (left the company, user link removed,
     permission withdrawn). Block 1 accepts these: the days stay reserved and HR sees the request with its state and age.
     MOD-0023 already has an immediate close for a task-less start (step 4); letting HR trigger it from the reconciliation
     list is a later improvement, recorded in MOD-0023 design request 01.
   - Cancel of a **pending** request (the employee): settle; **closed** → request `Cancelled` and reserved days released in
     one transaction with the lock; **decided** → the decision is applied and the cancel is refused (FU01 F13);
     **unsettled** → nothing released.
   - Resubmit (the employee, `Unconfirmed` or `NeedsReconciliation`): settle the current submission; **decided** → apply it, no new start;
     **closed** → `SubmissionNumber + 1`, new `StartFacts` stored, start; **unsettled** → refused (409
     `LEAVE_APPROVAL_NEEDS_RECONCILIATION`).
   - Cancel of an **approved** leave: MOD-0023 is not touched — the completed approval stands. Not started (`StartDate` >
     local today): the employee (`leave.requests.cancel`). Started: HR only (`leave.admin-requests.cancel`, reason code; a
     record correction). In both, one transaction sets `Cancelled`, returns the request's `TakenDays` and bumps the lock.
   - Start lease: while a start or settle call of the request is running, a second cancel or resubmit gets 409
     `LEAVE_APPROVAL_STARTING`. This is a courtesy; if the lease lapses, settle still gives the final answer.
   - Decisions are only **read** by the sweep job and the read path (finalize once, by the same status map:
     `Completed`/`Approved` → `Approved`; `Rejected` → `Rejected`, days released; `TimedOut` or `Cancelled` → `Cancelled`
     when `PendingIntent = cancel`, otherwise `Withdrawn`, days released once); neither starts, settles nor cancels.
   - **Days returning to a closed year:** carry-over moves the closing year's remaining days, reserved days counting as
     used. The run for year Y is allowed only after 31 Dec Y in the tenant's time zone (`LEAVE_CARRY_OVER_TOO_EARLY`),
     and once a year has a `LeaveCarryOverMark` no new reservation may allocate days to it
     (`LEAVE_YEAR_CARRIED_OVER`; a back-dated request into a closed year is refused). **Every release** of days to a year
     with a mark (a pending request cancelled, withdrawn or rejected, an approved leave cancelled by HR) writes the balance
     as usual and sets `CarryOverCorrectionNeeded` on that row in the same transaction; HR sees those rows on the
     entitlements page and corrects with `AdjustLeaveCarryOver` (D-3/7). `POST /entitlements/{id}/carry-over` names the
     **closing year's** entitlement; one transaction changes that year's `CarriedOutDays` and the next year's
     `CarriedOverDays` by the same amount (they stay equal), keeps the type's carry-over limit, keeps the closing year's
     remaining ≥ 0 and the next year's reduction floor, and clears `CarryOverCorrectionNeeded` on the closing year's row.
     The run reports how many rows had pending requests (AC-55, AC-57).
   - Year-end carry-over: per (employee, closing year, type) one transaction writes the `LeaveCarryOverMark` (unique), the
     next year's `CarriedOverDays`, the closing year's `CarriedOutDays` (the same amount) and both balance rows; a duplicate
     mark aborts that row's transaction, so a resumed run
     applies every row exactly once.
   - **Definition versions (review 05 F-21):** MOD-0023's fingerprint does not include the definition version, and it
     completes an incomplete start with the version stored on the instance (`StartWorkflowInstanceHandler.cs:441–446`,
     `:750–769`); a whole start is answered as-is (:385–391). So a newer version of the leave definition does **not** block a
     settle: the replay closes the start under the version it was started with. That is accepted — the closure is still
     final. A replay is refused only when the definition **id** or another stored fact differs; then step 4 closes the
     task-less half start (closed); without step 4 the result is unsettled and nothing is released.
   - **Shared 409 (revision 06):** MOD-0023 answers "incomplete on a closed instance", a lost claim and a facts conflict with
     the same 409 reason code (:742–743); settle never interprets it — only the status map above decides.
   - **Escalated task (revision 06):** the settle cancel is sent with `AllowEscalated: true`, as FU01 does for an owner
     withdrawing its own object (`TimesheetApprovalService.cs:256–265`).
   - **First start fails inside a settle** (no approver, no active definition version, MOD-0023 503): unsettled, days stay
     reserved; the employee may try again later and HR sees the request on the reconciliation list. Safe side, by design.

## Layout & Shell Contract

- `shell: tenant` → `Layout = "_LayoutTenantShell";` written explicitly in every `.cshtml`.
- Routes: `/Leave` (My leave: requests, balance, new request; HR tab "All requests" when the caller holds
  `leave.admin-requests.read`), `/Leave/Types`, `/Leave/Entitlements`.
- Page codes (nav and help): `MYLEAVE`, `LEAVETYPES`, `LEAVEENTITLEMENTS`; nav keys `Nav.Page.MYLEAVE`,
  `Nav.Page.LEAVETYPES`, `Nav.Page.LEAVEENTITLEMENTS`; domain `Nav.Domain.HUMANCAPITAL` (existing).
- `MYLEAVE` visible in navigation; `LEAVETYPES` and `LEAVEENTITLEMENTS` reached from an HR settings entry.
- UAS-001: a user without the page permission gets the single explanation panel, no skeleton, no redirect.

### Page Help (UXH-001)

Keys per page: `Help.{KOD}.Purpose`, `Help.{KOD}.Audience`, `Help.{KOD}.Step{n}` and `Help.{KOD}.Step{n}Title`,
`Help.{KOD}.Immediate`, and `Help.{KOD}.Related.{KOD}` for each related page written in `PageHelpCatalog`
(`Services/Help/PageHelpCatalog.cs`, with the step icons); 7 languages (tenant module); tour: none.

| Page code | Purpose | Audience | Steps (titles) | Immediate | Related (`PageHelpCatalog`) |
|---|---|---|---|---|---|
| `MYLEAVE` | ask for leave, see the balance per type and follow each request | every employee; HR also sees the "All requests" tab | 1 See your balance · 2 Send a request · 3 Follow the approval · 4 Cancel or send again · 5 HR: reconcile, re-check and cancel a started leave with a reason | yes — a request of a type that needs no approval and cancelling an approved, not-yet-started leave take effect at once; cancelling a pending request first checks the approval and may answer that it was already decided or needs reconciliation | none (the HR pages need HR keys) |
| `LEAVETYPES` | define the tenant's leave types and their balance and approval rules | HR | 1 Create a leave type · 2 Set balance and approval · 3 Deactivate instead of delete | yes — saving, activating and deactivating apply at once | `LEAVEENTITLEMENTS` |
| `LEAVEENTITLEMENTS` | enter each employee's yearly entitlement and close the year | HR | 1 Enter entitlements · 2 Run the year-end carry-over · 3 Correct flagged rows with a reason | yes — saving an entitlement, the carry-over run and an adjust apply at once | `LEAVETYPES` |

The help ledger guard `frontend/Diten.Web/tests/page-help-ledger.test.js` (UXH-001) measures the keys with `resx-check`.

## Backend File Convention

```text
Features/Leave/
├── Commands/            sealed records, one per file
├── Queries/
├── Handlers/
│   ├── CommandHandlers/ {Verb}{Object}Handler.cs
│   └── QueryHandlers/   Get{Object}{List|ById}Handler.cs
├── Validators/          {Verb}{Object}Validator.cs
├── Services/            LeaveApprovalService, LeaveApproverResolver, LeaveDayCounter, LeaveBalanceLedger, LeaveRequestWriter, LeaveDecisionFinalizer
├── Adapters/            EmployeeGatewayAdapter (HTTP, contract-versioned), FakeEmployeeGateway (dev/test), UnavailableLeaveEmployeeGateway (closed, production until DL-4), OrgGatewayAdapter
├── Providers/           LeaveApprovalSourceResolver
├── BackgroundJobs/      LeaveDecisionSweepJob, LeaveEmployeeSyncJob
├── SelfRegistration/    LeaveManifestProvider.cs
└── LeaveModels.cs       all DTOs in one file
```

Repositories inherit `TenantRepository<T>` and live in `Infrastructure/Persistence/Repositories/LeaveRepositories.cs`
(§Repo Scope); collections snake_case with `leave_` prefix; thin controller,
`CustomBaseController`, `[Authorize]` + `[HasPermission]`, `Response<T>`. No new base entity, repository base, audit, event
or job infrastructure. MOD-0023, MOD-0288 and MOD-0362 types appear only in `Adapters/`.

## Frontend File Contract

- **LeaveTypes** — Golden Reference Slim, file-for-file from `Views/DevEnablement/GoldenReferenceSlim/` (`Index`, `_Filter`,
  `_DataTable`, `_IndexL10n`, `_CreateEditOffcanvas`, `_DetailsQuickView`, marker class, JS, 7 resx); client mode, max 50.
  No delete; activate / deactivate. The category select never offers `health`.
- **LeaveEntitlements** — Slim, server mode; filter by year and leave type; create/edit offcanvas (employee, year, type, days);
  year-end carry-over action with confirmation; a "Carry-over correction needed" filter lists balance rows flagged after
  the run, with the adjust action (days and a reason select over the closed adjust-reason list).
- **Leave** — Slim, server mode: balance cards per type (entitled, carried in, carried out, reserved, taken, remaining — numbers, not
  percentages), request list, "New request" offcanvas (type, start, end, half-day part) with a live preview of working days
  and "remaining → after approval"; cancel action. A request in `Unconfirmed` or `NeedsReconciliation` shows its approval
  link state as a label ("approval not confirmed" · "needs reconciliation") and a "Send again" action
  (`POST /requests/{id}/resubmit`) next to cancel. HR tab with the same list for the whole tenant and a "To reconcile"
  filter (`GET /admin/requests?workflowStartState=…`, oldest first, showing state and age) with a "Re-check" action
  (`POST /admin/requests/{id}/recheck`, shown only with `leave.admin-requests.reconcile`), and on a started approved
  leave a "Cancel" action (`POST /admin/requests/{id}/cancel`, shown only with `leave.admin-requests.cancel`) whose
  confirmation requires a reason select over the closed `CancelReason` list. All labels from resx, 7 languages.

## Validation Rules

| Field / action | Rule | Check | Reason code |
|---|---|---|---|
| `LeaveType.Code` | pattern, tenant-unique, immutable | validator + unique index | `LEAVE_TYPE_CODE_INVALID` / `LEAVE_TYPE_CODE_DUPLICATE` / `LEAVE_TYPE_CODE_IMMUTABLE` |
| `LeaveType.Category` | one of `annual`, `excuse`, `unpaid`, `other`; `health`, an empty or any other value refused; fixed at creation — an update naming another category is refused (no audited event changes it) | validator + handler | `LEAVE_TYPE_CATEGORY_LOCKED` |
| `LeaveType` label | `LabelText` or `LabelResourceKey`; `LabelText` at most 200 characters | validator | `LEAVE_TYPE_LABEL_REQUIRED` / `LEAVE_TYPE_LABEL_TOO_LONG` |
| `LeaveType` id | names a type of the caller's tenant; unknown or another tenant's id → 404, the same answer for both | handler | `LEAVE_TYPE_NOT_FOUND` |
| `LeaveType.ReducesBalance` | false when `Category = unpaid` | validator | `LEAVE_TYPE_UNPAID_REDUCES_BALANCE` |
| `LeaveType.CarryOverLimitDays` | empty, or 0–365 in steps of 0.5 and only when `ReducesBalance` | validator | `LEAVE_TYPE_CARRY_OVER_LIMIT_INVALID` |
| `LeaveEntitlement` | type of the caller's tenant (`LeaveType` id row), active and reduces balance; days 0–365 step 0.5; year 2000–2100; employee known — the employee service answers for the id, whatever its `activeAsOf` (else 404); service unreachable, or any other answer → 503, nothing written (the caller's identity and the 403 and 409 mapping of `/{employeeId}/employment-summary` are settled with the frozen contract in DL-4) | validator + handler + gateway | `LEAVE_ENTITLEMENT_TYPE_INVALID` / `LEAVE_ENTITLEMENT_DAYS_INVALID` / `LEAVE_ENTITLEMENT_YEAR_INVALID` / `LEAVE_EMPLOYEE_NOT_FOUND` / `LEAVE_EMPLOYEE_SERVICE_UNAVAILABLE` |
| `LeaveEntitlement` reduction | entitled + carried ≥ taken + reserved + carried out | handler in the balance transaction | `LEAVE_ENTITLEMENT_BELOW_USED` |
| every write except submit | unknown commit result (`PlatformTransactionUnavailableException`, which the shared exception handler does not map) → 503; the caller re-reads. Submit keeps `LEAVE_SUBMIT_OUTCOME_UNKNOWN` and its retry by key | handler | `LEAVE_WRITE_OUTCOME_UNKNOWN` |
| submit — type | `LeaveTypeId` names a type of the caller's tenant (unknown or another tenant's id → 404) that is active | handler | `LEAVE_TYPE_NOT_ACTIVE` |
| submit — employee | `/me` resolves an employee active on `StartDate` and on `EndDate` | gateway (`asOf`) | `LEAVE_EMPLOYEE_NOT_LINKED` / `LEAVE_EMPLOYEE_NOT_ACTIVE` |
| submit — dates | `StartDate ≤ EndDate`; ≥ 1 working day; half day only for one day | validator + `LeaveDayCounter` | `LEAVE_DATES_INVALID` / `LEAVE_NO_WORKING_DAY` / `LEAVE_HALF_DAY_INVALID` |
| submit — overlap | no pending or approved request of the same employee overlaps | handler inside the transaction that bumps `LeaveEmployeeLock.Version`; a concurrent submit of the same person fails its lock write and is retried once, then 409 | `LEAVE_REQUEST_OVERLAP` |
| submit — balance | for `ReducesBalance`: remaining ≥ allocation per year | `LeaveBalanceLedger` conditional write | `LEAVE_BALANCE_INSUFFICIENT` |
| submit — approver | resolvable when `RequiresApproval` | `LeaveApproverResolver` | `LEAVE_NO_APPROVER` |
| cancel (own) | `Pending` (workflow path) or `Approved` and `StartDate` > local today (business path) | handler | `LEAVE_CANCEL_TOO_LATE` |
| cancel / resubmit while starting | start lease held (courtesy) | conditional read of the lease | `LEAVE_APPROVAL_STARTING` |
| cancel / resubmit not settled | the re-read status after the settle maps to unsettled | `LeaveApprovalService.SettleAsync` | `LEAVE_APPROVAL_NEEDS_RECONCILIATION` |
| HR re-check | request `Pending` and `Unconfirmed` or `NeedsReconciliation` | handler | `LEAVE_RECHECK_NOT_APPLICABLE` |
| resubmit (own) | request `Pending` and `Unconfirmed` or `NeedsReconciliation` | handler | `LEAVE_RESUBMIT_NOT_APPLICABLE` |
| cancel / resubmit after a decision | the settle's re-read status maps to decided | `LeaveApprovalService.SettleAsync` | `LEAVE_ALREADY_DECIDED` |
| cancel (HR) | `Approved`, started; `CancelReason` required and one of `ENTERED_IN_ERROR` · `NOT_TAKEN` · `HR_DECISION` | validator | `LEAVE_CANCEL_REASON_REQUIRED` / `LEAVE_CANCEL_REASON_INVALID` |
| carry-over | year closed once per (tenant, year), only after 31 Dec of that year; adjust needs a reason code, one of `RELEASE_AFTER_CARRY_OVER` (days returned to the closed year, the flagged case) · `ENTRY_ERROR` · `HR_DECISION` | handler + index | `LEAVE_CARRY_OVER_ALREADY_RUN` / `LEAVE_CARRY_OVER_TOO_EARLY` / `LEAVE_CARRY_OVER_REASON_REQUIRED` / `LEAVE_CARRY_OVER_REASON_INVALID` |
| carry-over adjust | the move keeps the type's carry-over limit, the closing year's remaining ≥ 0 and the next year's reduction floor | handler inside the balance transaction | `LEAVE_CARRY_OVER_LIMIT_EXCEEDED` / `LEAVE_CARRY_OVER_BELOW_REMAINING` / `LEAVE_ENTITLEMENT_BELOW_USED` |
| submit — closed year | no new allocation to a year that has a `LeaveCarryOverMark` (a resubmit allocates nothing — it keeps the reservation the run already counted as used) | handler inside the balance transaction | `LEAVE_YEAR_CARRIED_OVER` |
| every write | `expectedVersion` | conditional write | `LEAVE_CONCURRENCY_CONFLICT` |

## Failure Path to Verify

- **Two submits at the same instant** for overlapping dates or for the last remaining days → exactly one request stored;
  the other gets 409 (`LEAVE_REQUEST_OVERLAP` or `LEAVE_BALANCE_INSUFFICIENT`); the balance is never negative. The same
  holds for two overlapping requests of a type that does not reduce the balance (the employee lock serialises them).
- **Failure inside the transaction body** (second year's balance row or the lock write fails) → rollback; no request, no
  reservation; 409 or 503 with the reason code.
- **Commit answer lost** (`PlatformTransactionUnavailableException`) → 503 `LEAVE_SUBMIT_OUTCOME_UNKNOWN`; retry with the
  same `SubmissionKey` returns the one stored request, or stores it once if the first commit did not happen.
- **Workflow start fails or its answer is lost** → request `Pending`, `Unconfirmed`, days reserved; "send again" settles the
  current submission first and starts the next one only when the re-read status maps to closed.
- **Settle loses its claim while an approver decides** → MOD-0023 answers the shared 409; the re-read shows `Completed`
  or `Rejected`; result decided, the decision is applied, nothing is cancelled and no new submission starts.
- **Settle cancel answer lost** → the same settle's re-read (step 3) shows `Cancelled` if MOD-0023 recorded the cancel:
  closed, the cancel completes once. Only if the cancel was not recorded does the request go to `NeedsReconciliation`; the
  next settle repeats the cancel with the same fixed key (MOD-0023 answers a recorded one again) and releases nothing twice.
- **Sweep re-check and the employee's settle at the same time** → both read the request `Version` before calling MOD-0023;
  one final write wins, the other writes nothing and answers the winner's state; days released once.
- **A request is cancelled, then the sweep re-checks it** → the cancel set `Closed`; the re-check does not touch it; days
  released once.
- **Start call pauses after MOD-0023 created the instance; the employee resubmits; the old call wakes** → its result write
  is conditional on the old `Version` and writes nothing; the request stays on the new submission's instance.
- **Start never confirmed, instance closed by MOD-0023's recovery** → the re-check withdraws the request (or cancels it
  when `PendingIntent = cancel`) and releases its days once; after `Withdrawn` the employee may send a new request.
- **Cancel settle stops after MOD-0023 closed the instance, before the final leave write** → the request stays
  `Unconfirmed` (or `Started`) with `PendingIntent = cancel`; the re-check (or `FinalizeLeaveDecision`) re-reads closed
  and sets `Cancelled`, days released once (AC-46, AC-52).
- **Instance times out** → re-read `TimedOut` → closed; a pending request becomes `Withdrawn` (`Cancelled` when
  `PendingIntent = cancel`) and its days are released once.
- **Cancel or resubmit while a start is in flight** (two tabs) → 409 `LEAVE_APPROVAL_STARTING`; nothing changes.
- **Older start paused before allocation, then resumes after a cancel** → the cancel's settle already created and closed
  the one instance for that key; the resumed start finds it closed and writes no task (review 04 F-20).
- **Older start paused after allocation, before writing its task** → the settle completes that same instance (one task)
  and cancels it; the resumed start loses its task claim because the instance is no longer `Active`.
- **Process dies during a start** → the next cancel or resubmit settles by replay; the lease is not needed.
- **Settle refused (facts conflict)** → the half start is task-less; step 4 closes it, the re-read says `Cancelled`,
  closed. Without step 4 (MOD-0023 tower not confirmed) → `NeedsReconciliation`; nothing released; HR sees it on the
  reconciliation list.
- **Step 4 races the paused first call** → whichever MOD-0023 write lands first wins (the supersede closes on the version
  it read, the first call writes its claim and task in one transaction); the re-read says `Cancelled` (closed, no task
  left) or `Active` with a task (step 2 on the next settle cancels it); days released once.
- **Re-check adopts a start while the employee's settle is cancelling it** → the settle's final write loses the version
  race; the instance is `Cancelled` under the current key; finalize maps it with `PendingIntent = cancel` → `Cancelled`,
  days released once.
- **Start worked but its answer was lost** → `Unconfirmed`; the next re-check finds an open task and adopts the instance
  (`Started`); the employee's resubmit is not blocked meanwhile.
- **Cancel after step 1 was approved on a two-step definition** → the settle reads the instance's open task (step 2) and
  cancels it; the instance closes; the days are released once.
- **Cancel an approved, not-started leave** → MOD-0023 untouched; request `Cancelled`; taken days returned in one
  transaction. **HR cancels a started leave** → same, with reason code (review 03 F-19).
- **Cancel when the decision came first** → cancel refused (FU01 F13); the decision is applied.
- **Year-end carry-over stops halfway** → rows with a mark are skipped by the unique index inside their own transaction;
  every row applied exactly once.
- **Entitlement reduced below taken + reserved** → 409 `LEAVE_ENTITLEMENT_BELOW_USED`, nothing written.
- **Decision and cancellation race** → whichever MOD-0023 records first wins; the settle's re-read status says which
  (§Runtime Constraints 9, AC-44); the balance reflects exactly one outcome.
- **Decision replayed** (sweep job and read path both see it) → applied once; one audit entry.
- **Employee service down** → submit and entitlement set 503 `LEAVE_EMPLOYEE_SERVICE_UNAVAILABLE`, nothing written; lists still load.
- **Employee terminated before the end date** → submit 409 `LEAVE_EMPLOYEE_NOT_ACTIVE`.
- **No approver** → 409 `LEAVE_NO_APPROVER`; never auto-approved when the type requires approval.
- **Resolved approver is the person** → walked past; MOD-0023 itself refuses a starter's approval and a delegation to the
  starter (`WorkflowTaskTransitionSupport.cs:134`, :464), so no leave-side state is needed.
- **Health category through the API or an import** → 400 `LEAVE_TYPE_CATEGORY_LOCKED`.
- **Missing required field** (a leave type without `LabelText` and `LabelResourceKey`, a submit without dates) → 400
  with the field's reason code (`LEAVE_TYPE_LABEL_REQUIRED`, `LEAVE_DATES_INVALID`); nothing is written.
- **Code outside a closed list** — HR's cancel with a `CancelReason` not in the list, a carry-over adjust with an unknown
  reason code → 400 `LEAVE_CANCEL_REASON_INVALID` / `LEAVE_CARRY_OVER_REASON_INVALID`; nothing is written, nothing
  released.
- **Duplicate** — a second leave type with an existing `Code` in the tenant → 409 `LEAVE_TYPE_CODE_DUPLICATE`, nothing
  written (unique index); a repeated "Send" with the same `SubmissionKey` → the stored request, nothing reserved again.
- **Request across 31 Dec** → two year allocations; each year's balance reduced by its own days.
- **Concurrency** (two edits of one type or entitlement) → 409 "the record changed meanwhile; reload and retry".
- **Commit answer lost on a write other than submit** → 503 `LEAVE_WRITE_OUTCOME_UNKNOWN`; the caller re-reads the row.
- **Default role** — a user holding only the row-scoped read keys (`leave.requests.read`, `leave.types.read`, `leave.approvals.read`) → 403 on `GET /admin/requests`, `GET /entitlements` and `GET /admin/sync-runs` (those three routes' keys reach no default role).
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
  leave.admin-requests.read      HR: every request in the tenant (D-1/3) — explicit grant only
  leave.admin-requests.cancel    HR: cancel a started request, with reason code
  leave.admin-requests.reconcile HR: read-only re-check of an unconfirmed or unsettled request (Tier 3, registered here)
  leave.types.read               every employee who may request leave: active types (needed by the request form)
  leave.types.manage             HR: leave type catalogue
  leave.entitlements.read        HR: entitlement list — explicit grant only
  leave.entitlements.manage      HR: set entitlements, year-end carry-over, adjustments
  leave.sync-runs.read           HR / support: sync monitoring (ADR-005 addendum 1/2) — explicit grant only
Decisions:   MOD-0023's own platform.workflow.tasks.approve / .reject (unchanged)
```

- `cancel`, `manage` and `reconcile` are Tier-3 actions registered by this pack (PKS-001 §5); everything else is Tier 1.
- **How keys reach users (measured for revision 10: AuthService `DefaultRolePermissionTemplate.SelectFor`,
  `DefaultRoleProvisioningRule.GrantsFor`, `FullCatalogPermissionGrantService`, `EntitlementPermissionSyncService`; Platform
  `PlatformPermissionAutoRegistrationWorker`).** Platform pushes every `[HasPermission]` key to AuthService's catalog at
  start-up, so a slice's key exists from then on, with or without DL-6's manifest, and a first-time key is granted to the
  default tenant's `SuperAdmin` (full catalog). A default role gets the template only in the call that creates that role (a
  new tenant, or a tenant that lacks the role); afterwards only the tenant's administrator changes a role. In the template
  `Viewer` takes every tenant-scoped key whose action is `read`, and `Admin` the keys of its listed modules (leave is not
  one); the module-entitlement sync gives an entitled tenant's `Admin` the module's keys. This pack changes no role template.
  So:
  - `leave.admin-requests.read`, `leave.entitlements.read` and `leave.sync-runs.read` read other people's data across the
    tenant. They are **explicit-grant only**: enrolled in `ExplicitGrantOnlyPermissions.Keys` (slice DL-0), the path
    MOD-0280-FU01 took for `time-entry.team-totals.read` and `time-entry.person-reports.read`. The template, the full-catalog
    grant and the entitlement sync all read that one set, so no automatic path gives them; only a person's explicit role
    assignment gives them to a tenant user. The enrolment reaches main before any code that names these keys, and AuthService
    with DL-0 is deployed before or with the Platform build that first carries them: a later enrolment removes no grant
    already made.
  - `leave.requests.read`, `leave.types.read` and `leave.approvals.read` reach a `Viewer` role created after the key exists,
    whether or not the tenant is entitled to the module. Accepted: the row scope below limits them to the caller's own
    requests, active types and requests the caller may decide. Where the `Viewer` role already exists they come from the
    tenant's role administration or the entitlement sync.
  - Every other key (`create`, `cancel`, `manage`, `reconcile`) reaches users through the tenant's role administration
    (MOD-0018 role assignments) or the module-entitlement sync. A default grant of `leave.requests.create` to a role template
    is a follow-up item that needs AuthService's consent.
  - `leave.types.read` is meant to be held together with `leave.requests.create`; the type list it returns holds active
    types only and never the locked `health` category.

Endpoint → key → row scope (review 01 F-03). A key alone never widens the rows; every row is also tenant-filtered and
another tenant's row is 404.

| Endpoint | Key | Rows the caller may see or change |
|---|---|---|
| `GET /me/requests`, `GET /me/balance` | `leave.requests.read` | the caller's own employee only (resolved by `/me`) |
| `POST /requests/preview`, `POST /requests` | `leave.requests.create` | the caller's own employee only |
| `GET /requests/{id}` — own branch | `leave.requests.read` | the caller's own request |
| `GET /requests/{id}` — approver branch | `leave.approvals.read` | a request whose MOD-0023 instance has an open task the caller may decide, or whose newest task the caller decided (MOD-0023 `MayDecide` over the assignment snapshot — FU01 `DecidableInstanceIdsAsync`, `TimesheetApprovalService.cs:64–77`) |
| `GET /requests/{id}` — HR branch | `leave.admin-requests.read` | any request of the tenant (D-1/3) |
| `POST /requests/{id}/resubmit` | `leave.requests.create` | own request, `Pending` and `Unconfirmed` or `NeedsReconciliation` |
| `POST /requests/{id}/cancel` | `leave.requests.cancel` | own request, `Pending` or not started |
| `GET /admin/requests` | `leave.admin-requests.read` | every request of the tenant (D-1/3); filter `workflowStartState` (`Unconfirmed` · `NeedsReconciliation`, `Pending` requests only) gives the reconciliation list, oldest first |
| `POST /admin/requests/{id}/recheck` | `leave.admin-requests.reconcile` | any `Pending` request of the tenant in `Unconfirmed` or `NeedsReconciliation`; read-only towards MOD-0023 |
| `POST /admin/requests/{id}/cancel` | `leave.admin-requests.cancel` | any request of the tenant, started, with reason code |
| `GET /types`, `GET /types/{id}` | `leave.types.read` | active types of the tenant (inactive ones only with `leave.types.manage`) |
| `POST /types`, `PUT /types/{id}`, `POST /types/{id}/activate`, `.../deactivate` | `leave.types.manage` | types of the tenant |
| `GET /entitlements` | `leave.entitlements.read` | every entitlement of the tenant |
| `PUT /entitlements`, `POST /entitlements/year-end`, `POST /entitlements/{id}/carry-over` | `leave.entitlements.manage` | entitlements of the tenant |
| `GET /admin/sync-runs`, `GET /admin/sync-runs/{id}/unmatched` | `leave.sync-runs.read` | runs and unmatched rows of the tenant |

A request outside every branch the caller holds is 404. The three branches are checked independently; none implies another.

- Read and write keys are separate (D-1/4). Scope: tenant-wide for HR in block 1 (D-1/3); legal-entity scope is a follow-up.
- The approver sees only requests they may decide or have decided, by MOD-0023's own rule (D-1/2); `leave.approvals.read`
  reaches users as the row-scoped read keys above, like FU01's `time-entry.approvals.read`, and opens nothing by itself: the
  row scope is MOD-0023's own rule.
- Notification events (manifest-declared; event key rule `^[a-z0-9]+(\.[a-z0-9]+)*$`): `leave.request.submitted`,
  `leave.request.approved`, `leave.request.rejected`, `leave.request.cancelled`. Payload: only the allowed fields of
  §Runtime Constraints 6.

## Audited Events

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateLeaveTypeCommand | leave.type.created | LeaveType | a | AfterState: Code, label, Category, ReducesBalance, RequiresApproval, CarryOverLimitDays | — |
| UpdateLeaveTypeCommand | leave.type.updated | LeaveType | a | Before/After: label, ReducesBalance, RequiresApproval, CarryOverLimitDays | — |
| ActivateLeaveTypeCommand | leave.type.activated | LeaveType | a | Before/After: IsActive | — |
| DeactivateLeaveTypeCommand | leave.type.deactivated | LeaveType | a | Before/After: IsActive | — |
| SetLeaveEntitlementCommand | leave.entitlement.set | LeaveEntitlement | a | Before/After: EntitledDays (EmployeeId, Year, LeaveTypeId) | — |
| RunLeaveYearEndCarryOverCommand | leave.entitlement.carried-over | LeaveEntitlement | a | per row Before/After: next year's CarriedOverDays, closing year's CarriedOutDays; run totals | — |
| AdjustLeaveCarryOverCommand | leave.entitlement.carry-over-adjusted | LeaveEntitlement | a | Before/After: next year's CarriedOverDays, closing year's CarriedOutDays, CarryOverCorrectionNeeded; reason code | — |
| SubmitLeaveRequestCommand | leave.request.submitted | LeaveRequest | a | business: type, dates, half-day part, DayCount, Status · envelope: request id, SubmissionNumber, WorkflowStartState | — |
| ResubmitLeaveRequestCommand | leave.request.resubmitted | LeaveRequest | a | business: Status · envelope: request id, SubmissionNumber before/after, settle result code (`closed` · `decided` · `unsettled`), re-read instance status code, WorkflowStartState | — |
| CancelLeaveRequestCommand | leave.request.cancelled | LeaveRequest | a | business: Status before/after, released DayCount · envelope: request id, cancel path code (`pending-workflow` · `approved-not-started`), settle result code, re-read instance status code, WorkflowStartState before/after, PendingIntent code | — |
| CancelLeaveRequestCommand / ResubmitLeaveRequestCommand (unsettled) | leave.request.reconciliation-needed | LeaveRequest | a | business: Status · envelope: request id, SubmissionNumber, WorkflowStartState before/after, PendingIntent code, settle result code, re-read instance status code | — |
| CancelStartedLeaveRequestCommand | leave.request.cancelled-by-hr | LeaveRequest | a | business: Status before/after, released DayCount · envelope: request id, cancel reason code | — |
| FinalizeLeaveDecisionCommand | leave.request.decided | LeaveRequest | a | business: Status before/after · envelope: request id, workflow outcome code | — |
| RecheckLeaveSettlementCommand | leave.request.rechecked | LeaveRequest | a | business: Status before/after, released DayCount · envelope: request id, SubmissionNumber, WorkflowStartState before/after, re-read instance status code, PendingIntent code, recheck result code (`adopted` · `withdrawn` · `intent-completed`) | — |
| SyncLeaveEmployeeSnapshotsCommand | leave.employee-sync.completed | LeaveSyncRun | a | run counts; no personal fields | — |

- **Per-event allowed fields (review 02 F-09).** The *business* part of a request event is the allowed-field list of
  §Runtime Constraints 6 (type, dates, half-day part, day count, status). The *envelope* part carries only identifiers and
  closed code lists: request id, submission number, workflow start state, workflow outcome code, settle result code,
  re-read instance status code, pending intent code, recheck result code, cancel path code, cancel reason code — no free
  text. Every record also carries AUD-001 §3's standard actor envelope (actor id, actor type, masked display, tenant,
  correlation id) — that is the audit standard's own field set, not business payload. AC-5 governs the business part;
  AC-17 and AC-38 check the envelope and technical fields against this table. No person name in the business part, no
  document, no health or contact data anywhere. Type catalogue events carry the label (catalogue data).
- No command is K2 class in block 1 (no identity, permission, tenant-state, GxP or special-category personal data);
  sickness (special category) arrives only with block 2 and is K2 there.
- **Business event and the pipeline's record (revision 10).** `AuditBehavior` reads a command's metadata before the handler
  and writes one record after it for every dispatched audited command: `Succeeded`, or `Failed` with its own outcome fields
  when the response is a failure (AUD-001 §3: a refused command is recorded too). The audit record has no event-name field. A
  **business event** is a record whose `Metadata["event"]` names an event of this table. The handler sets that name, and fills
  the before and after dictionaries the command hands over, only on the path that applies the change: the run whose
  conditional final write won, which is what "keyed per (instance, outcome)" means.
  - "None" or "nothing" in §Runtime Constraints 9's path table means no business event; a refused command's own `Failed` record
    (no event name, no after values) still exists. AC-17 and AC-50 count business events.
  - A decision applied inside another command's request (cancel or resubmit answering `LEAVE_ALREADY_DECIDED`) is sent as a
    nested audited `FinalizeLeaveDecisionCommand`, which writes the business event; the refused command adds its own `Failed`
    record.
  - The read path, the sweep job and HR's re-check endpoint read first and send an audited command only when that read shows a
    change to apply (FU01 `TimesheetDecisionPuller`), so an unchanged read writes no record.
  - A command that changes many rows (`RunLeaveYearEndCarryOverCommand`) sends one nested audited command per row (FU01
    `CorrectCapturedTimeEntryCommand`).
  - `EntityId` is read before the handler: a create, a submit and a first entitlement set generate the id in the command.
  - Values are string, bool, int, decimal or Guid only, dates and enums as text, because the payload is stored as objects in
    the audit outbox. A leave type's label is catalogue data, not free text.
  - A validator refusal writes no record (`ValidationBehavior` runs first). An unknown commit result writes a `Failed` record
    even if the commit happened; the caller's re-read and the row's own `UpdatedBy` and `Version` show the truth. Accepted limit.
- **Category and source (revision 09).** Every command opts into the existing audit pipeline (`IAuditableCommand` +
  `IAuditMetadataProvider`, `AuditBehavior`), as FU01 does. Source module `leave`; category `AuditCategory.Tasks`, FU01's
  choice for the same reason (`TimeEntryModels.cs:152–157`: the platform has no HCM category, and adding one changes the
  shared audit enum, which this pack does not enumerate); the source module tells the modules apart. A dedicated HCM
  category for both FU01 and FU02 is a follow-up item.

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

- [ ] AC-1 (zorunlu) — Identity `MOD-0280-FU02`, parent `MOD-0280`; `verify_module_id.py . --check-id MOD-0280-FU02 --name "Leave & Absence Management" --parent MOD-0280` exits 0 at the start of the first slice (DL-0).
- [ ] AC-2 (zorunlu) — Every `leave_*` collection is tenant-scoped with tenant-first indexes; a request, type or entitlement of another tenant returns 404 with no metadata.
- [ ] AC-3 (zorunlu) — An employee sees only their own requests and balance; a manager sees only requests routed to them by MOD-0023; HR with `leave.admin-requests.read` sees every request of the tenant.
- [ ] AC-4 (zorunlu) — The `health` category cannot be created or set through the UI, the API or any seed; the attempt returns 400 `LEAVE_TYPE_CATEGORY_LOCKED`.
- [ ] AC-5 (zorunlu) — The request form has no free-text and no document field; the **business payload** of the Task Center card, notification e-mail, notification payload and request audit events carries only the allowed fields of §Runtime Constraints 6 (type code and label, start, end, half-day part, day count, status, request id) and never the person's name; the audit actor envelope is governed by AC-38.
- [ ] AC-6 (zorunlu) — Day count excludes weekends and public holidays from MOD-0362 and counts a half holiday as half; a half-day request is accepted only for a single day.
- [ ] AC-7 (zorunlu) — A pending request reserves its days; remaining = entitled + carried − reserved − taken − carried out; a request above remaining returns 409 `LEAVE_BALANCE_INSUFFICIENT`; two concurrent submits never make a balance negative.
- [ ] AC-8 (zorunlu) — An overlapping request of the same employee returns 409 `LEAVE_REQUEST_OVERLAP`.
- [ ] AC-9 (zorunlu) — Approval runs through MOD-0023 with the FU01 approver rules (line manager, next seat, pool, never self); no approver → 409 `LEAVE_NO_APPROVER`; never auto-approved when the type requires approval.
- [ ] AC-10 (zorunlu) — A decision is applied exactly once, by the sweep job or at read time; decision and cancellation races leave exactly one outcome.
- [ ] AC-11 (zorunlu) — Cancelling a pending or not-yet-started approved request refunds its days; a started request can be cancelled only by HR with a `CancelReason` from the closed list (`ENTERED_IN_ERROR` · `NOT_TAKEN` · `HR_DECISION`); a missing code → 400 `LEAVE_CANCEL_REASON_REQUIRED`, a code outside the list → 400 `LEAVE_CANCEL_REASON_INVALID`, and in both cases nothing is written and no day is released.
- [ ] AC-12 (zorunlu) — A request stores its type snapshot; later changes to the type or the entitlement do not change approved or past requests.
- [ ] AC-13 (zorunlu) — A request crossing 31 December is split into two year allocations; year-end carry-over moves remaining days up to the type limit and runs once per year; adjustments need a reason code from the closed list (`RELEASE_AFTER_CARRY_OVER` · `ENTRY_ERROR` · `HR_DECISION`); a missing code → 400 `LEAVE_CARRY_OVER_REASON_REQUIRED`, a code outside the list → 400 `LEAVE_CARRY_OVER_REASON_INVALID`, nothing written.
- [ ] AC-14 (zorunlu) — When the employee service is unreachable, submit and entitlement set return 503 `LEAVE_EMPLOYEE_SERVICE_UNAVAILABLE` and write nothing; existing requests and balances still load.
- [ ] AC-15 (zorunlu) — The employee adapter is tested against the frozen HCM-CORE-BUNDLE version with a contract test on both producer and consumer side; an unknown contract version is refused.
- [ ] AC-16 (zorunlu) — Every sync run writes a `LeaveSyncRun` with read / changed / unmatched counts and is visible to `leave.sync-runs.read`.
- [ ] AC-17 (zorunlu) — Every write command in §Audited Events emits its business event (§Audited Events) exactly once with the listed fields; for cancel, resubmit, re-check and finalize the event is the one §Runtime Constraints 9's path table names for the result (a re-check with no change writes none).
- [ ] AC-18 (zorunlu) — `LeaveTypes`, `LeaveEntitlements` and `Leave` pages pass `verify_datatable_page.py` with the pack's `data_mode`; every `.cshtml` writes `Layout = "_LayoutTenantShell"` explicitly.
- [ ] AC-19 (zorunlu) — All user-visible text exists in 7 languages (en, tr, fr, es, zh, ar, ru); `resx-check` reports no missing key; page help keys of §Page Help exist for each of `MYLEAVE`, `LEAVETYPES`, `LEAVEENTITLEMENTS` — Purpose, Audience, every Step{n} and Step{n}Title of the table, Immediate, and Related for each `PageHelpCatalog` entry; `page-help-ledger.test.js` passes (UXH-001).
- [ ] AC-20 (zorunlu) — A user without the page permission sees the single explanation panel, no skeleton and no redirect (UAS-001).
- [ ] AC-21 (zorunlu) — Pages, actions, permissions and notification events come from `LeaveManifestProvider`; nothing is hand-seeded.
- [ ] AC-22 (zorunlu) — The gateway pair `/api/v1/leave` and `/api/v1/leave/{everything}` → 5057 exists before DL-5 is accepted.
- [ ] AC-23 (zorunlu) — A submit writes the request, every affected balance row and the employee lock in one transaction; a failure injected after the first balance write leaves no request and no reservation (sabotage evidence).
- [ ] AC-24 (zorunlu) — A workflow start that fails or whose answer is lost leaves the request `Pending`/`Unconfirmed` with its days reserved and nothing retries on its own; a resubmit or cancel first runs the settle of §Runtime Constraints 9; the cancel or resubmit itself releases days and starts a new submission only when the re-read instance status maps to closed (a re-read `Rejected` is a decision: its days are released by the decision path, AC-46) — never on a task-less "true" or on an empty task list.
- [ ] AC-25 (zorunlu) — Two concurrent overlapping submits of one employee for a type that does not reduce the balance store exactly one request.
- [ ] AC-26 (zorunlu) — A year-end carry-over run stopped halfway and run again applies every row exactly once: mark, next year's carried days and both balance rows are written in one transaction per row.
- [ ] AC-27 (zorunlu) — A change that would leave `EntitledDays + CarriedOverDays` below `TakenDays + ReservedDays + CarriedOutDays` returns 409 `LEAVE_ENTITLEMENT_BELOW_USED` and writes nothing (entitled 5 + carried 5 with taken 8 is accepted; entitled 2 is refused; a closed year with entitled 14, taken 9, carried out 5 may not go below 14); approving reserved days does not lower the floor; approved requests are never re-evaluated; the answer lists pending requests only to a caller who also holds `leave.admin-requests.read`.
- [ ] AC-28 (zorunlu) — A user whose token holds only `leave.types.read` and `leave.requests.create` (the employee key set; `leave.requests.create` comes from the tenant's role administration) can list active leave types and gets 403 on every `leave.types.manage` endpoint.
- [ ] AC-29 (zorunlu) — Every endpoint enforces the row scope of the §Authorization Convention table; `GET /requests/{id}` serves the own, approver and HR branches independently; a manager with `leave.approvals.read` reads only requests they may decide or have decided, otherwise 404; HR with `leave.admin-requests.read` reads any request of the tenant.
- [ ] AC-30 (zorunlu) — A sync run with unmatched employees writes one `LeaveSyncUnmatched` row per employee with a reason code and no other personal field; `GET /admin/sync-runs/{id}/unmatched` returns them only to `leave.sync-runs.read`.
- [ ] AC-31 (zorunlu) — Every DataTable screen passes the `quality-gate-datatable` workflow in addition to `verify_datatable_page.py`.
- [ ] AC-32 (zorunlu) — Platform, AuthService, Web and Gateway build; a request through Gateway 5000 reaches `api/v1/leave` on Platform 5057 with the caller's tenant and permissions (end-to-end, DL-7).
- [ ] AC-33 (zorunlu) — A submit whose commit answer is lost returns 503 `LEAVE_SUBMIT_OUTCOME_UNKNOWN`; retrying with the same `SubmissionKey` yields exactly one request and one reservation whether or not the first commit happened (fault-probe evidence on the executor's commit).
- [ ] AC-34 (zorunlu) — No cancelled request ever has an open MOD-0023 task: race evidence with an older start paused (a) before instance allocation and (b) after allocation but before its task write, each resumed after a cancel, shows the cancel's settle closed the one instance for the key and the resumed start wrote no task; days released exactly once.
- [ ] AC-35 (zorunlu) — Every MOD-0023 start and cancel for a leave request runs as the employee who submitted it; no background job starts, retries or cancels an instance (MOD-0023 `SameStarter` honoured).
- [ ] AC-36 (zorunlu) — Cancelling an approved, not-started leave (employee) or a started leave (HR, reason code) leaves the MOD-0023 instance approved and untouched, sets the request `Cancelled` and returns its taken days in one transaction.
- [ ] AC-37 (zorunlu) — A resubmit starts a new submission only after the settle of the current one answered closed; decided → the decision is applied and nothing new starts; a refused replay (facts conflict) whose half start step 4 does not close (step 4 left out or refused) → 409 `LEAVE_APPROVAL_NEEDS_RECONCILIATION`, nothing released, the request returned by `GET /admin/requests?workflowStartState=NeedsReconciliation`.
- [ ] AC-38 (zorunlu) — Every leave audit record carries the AUD-001 §3 actor envelope with a masked display and only the closed technical fields of §Audited Events; no free text appears in either. The pipeline's own fields on a `Failed` record (`AuditBehaviorOutcome`, `ResponseStatusCode`, `ResponseErrors`) are allowed; response messages are fixed sentences that carry no user input.
- [ ] AC-39 (zorunlu) — Settle replays `StartAsync` with the stored `StartFacts` byte-for-byte (same fingerprint inputs) as the submitting employee, always before any cancel, and decides only by the re-read instance status map (`Cancelled`/`TimedOut` → closed, `Completed`/`Approved`/`Rejected` → decided, anything else → unsettled); sabotage evidence: a task-less `Active` instance that step 4 did not close (left out or refused), a shared 409 alone and a failed cancel each leave the days reserved.
- [ ] AC-40 (zorunlu) — The start lease is never a precondition for a release: with the lease forcibly expired during a paused start, the settle still prevents a second task and a release before the instance is closed.
- [ ] AC-41 (zorunlu) — `StartFacts` holds every MOD-0023 fingerprint input (definition id, object type / id / ref, candidates, reason code, comment-required, evidence-required, due-at) and a replay with them is accepted by MOD-0023's fingerprint check; a leave start never sends a time-dependent due-at.
- [ ] AC-42 (zorunlu) — The settle cancel is sent with `AllowEscalated: true` and the fixed key `leave-settle-cancel:{tenant}:{requestId}:{submissionNumber}:{taskId}`; an escalated open task is cancelled and the days released once; a repeated settle after a lost cancel answer releases nothing twice.
- [ ] AC-43 (zorunlu) — A new version of the leave approval definition published between the first start and a settle does not block the settle; the replay closes the start under its stored version and the days are released once.
- [ ] AC-44 (zorunlu) — Race evidence: a settle whose claim is lost to an older start that an approver then approves (or rejects) ends decided — the decision is applied, the cancel is refused, no days are released by the cancel and no second submission starts.
- [ ] AC-45 (zorunlu) — A request in `NeedsReconciliation` leaves it through the read-only re-check (sweep job, every read of the request — employee, HR list and detail, approver — and HR's `POST /admin/requests/{id}/recheck` with `leave.admin-requests.reconcile`) once MOD-0023 reaches a final status; the re-check never writes to MOD-0023 and completes the recorded intent exactly once.
- [ ] AC-46 (zorunlu) — Finalization maps MOD-0023 statuses as §Runtime Constraints 9 says: `Completed`/`Approved` → `Approved`; `Rejected` → `Rejected` with days released; `TimedOut` or `Cancelled` → `Cancelled` when `PendingIntent = cancel`, otherwise `Withdrawn`, days released once — every `Cancelled` instance under the current key is mapped, whoever cancelled it.
- [ ] AC-47 (zorunlu) — Re-check of an `Unconfirmed` request: an open task under the key → `Started` with the instance id recorded; a decided instance → the decision applied; a closed instance (`Cancelled`, `TimedOut`) → `Cancelled` when `PendingIntent = cancel`, otherwise `Withdrawn`, days released once (AC-52); no instance or an incomplete start → no change and the employee's resubmit stays allowed (evidence: start answer dropped after MOD-0023 wrote the task; one re-check adopts it, no second instance or task exists).
- [ ] AC-48 (zorunlu) — The settle cancels the instance's currently open task whatever step it is on: on a two-step definition with step 1 approved, a cancel closes the instance and releases the days once; it never ends unsettled for that reason.
- [ ] AC-49 (zorunlu) — `PendingIntent` is stored by the settle's lease write before any MOD-0023 call (it survives a lost final write or a crash), the latest employee action overwrites it, and a re-check that re-reads closed completes exactly that intent once (`cancel` → `Cancelled`, `resubmit` → `Withdrawn`); `FinalizeLeaveDecision` handles only `Pending` + `Started` requests and the re-check only `Pending` requests in `Unconfirmed` / `NeedsReconciliation`; an adoption or completed intent writes `leave.request.rechecked`, an applied decision `leave.request.decided`, each once.
- [ ] AC-50 (zorunlu) — Each (command, result) pair of the path table in §Runtime Constraints 9 writes exactly the named business event (§Audited Events) and answers the named status: cancel or resubmit decided → only `leave.request.decided` and 409 `LEAVE_ALREADY_DECIDED`; unsettled → `leave.request.reconciliation-needed` on a change, nothing on an identical repeat; a decision applied by both the re-check and the finalize writes `leave.request.decided` once.
- [ ] AC-51 (zorunlu) — Sweep re-check and the employee's cancel or resubmit run on the same request at the same time: exactly one final write succeeds (conditional on the request `Version` read before the MOD-0023 call), the other writes nothing and answers the winner's state, the days are released once and at most one new submission starts; including a re-check that adopts the start while the employee's settle cancels it (the request ends `Cancelled` through finalize, days released once).
- [ ] AC-52 (zorunlu) — Re-check of an `Unconfirmed` request whose instance is `Cancelled` or `TimedOut` sets `Cancelled` when `PendingIntent = cancel`, otherwise `Withdrawn`, and releases the days once (evidence: the employee's cancel settle on an `Unconfirmed` request stops after MOD-0023 closed the instance and before the final leave write, so the request stays `Unconfirmed` with intent `cancel`; the next re-check sets `Cancelled`, releases the days once and writes `leave.request.rechecked` once with result code `intent-completed`; a cancel by the employee afterwards is refused by the existing validation rule — the request is no longer `Pending` — with 409 `LEAVE_CANCEL_TOO_LATE` and releases nothing; set up as AC-53 with a second `Pending` reservation on the same balance row at least as large as the released days, so a double release stays visible under the never-negative rule); a `NeedsReconciliation` request with intent `resubmit` and a closed instance becomes `Withdrawn`; requests in the named cases that stay reserved (task-less `Active` start, no instance) appear in `GET /admin/requests?workflowStartState=…` with state and age, and the Leave page shows the approval-link label and the "Send again" action for them.
- [ ] AC-53 (zorunlu) — Every transition out of `Pending` sets `WorkflowStartState = Closed` in the same transaction; a cancelled, withdrawn, approved or rejected request is never re-checked, finalized or listed for reconciliation again (sabotage evidence, set up with the cancelled request in `Unconfirmed` or `NeedsReconciliation` — the states the re-check selects — and a second `Pending` reservation on the same balance row at least as large as the released days, so a double release stays visible under the never-negative rule; both guards named: with the `Closed` write **and** the re-check selector's `Status = Pending` condition removed, a cancel followed by a sweep re-check releases the days twice — red; with either guard alone restored, once — green; each guard is shown on its own).
- [ ] AC-54 (zorunlu) — The start result write is conditional on the `Version` written by its submit or resubmit transaction: a start call paused after MOD-0023 created the instance, overtaken by a resubmit (lease expired), writes nothing when it wakes; the request keeps the new submission's instance, finalize never reads the old one, and no days are released while the new instance is open.
- [ ] AC-55 (zorunlu) — Every release of days to a year after its carry-over ran (cancelled, withdrawn or rejected pending request, approved leave cancelled by HR) sets `CarryOverCorrectionNeeded` on that year's balance row in the release transaction, and `AdjustLeaveCarryOver` clears it in the transaction that moves the returned days (closing year's `CarriedOutDays` and next year's `CarriedOverDays` change by the same amount; a move beyond the carry-over limit, below the closing year's zero remaining or below the next year's floor is refused with `LEAVE_CARRY_OVER_LIMIT_EXCEEDED`, `LEAVE_CARRY_OVER_BELOW_REMAINING` or `LEAVE_ENTITLEMENT_BELOW_USED` and writes nothing); the row appears under the entitlements page's "Carry-over correction needed" filter; the carry-over run reports the number of rows with pending requests; nothing is moved to the next year automatically.
- [ ] AC-56 (zorunlu; applies only once the MOD-0023 tower confirms under card S-90 — otherwise it is not applicable and the evidence is the "left out" branch of AC-37 and AC-39; confirmed 2026-10-29, §Teslim dilimleri) — Settle step 4: a refused or incomplete replay that leaves a task-less `Active` instance is closed by `SupersedeWorkflowHalfStartCommand` and the days are released once; an instance with any task is refused by MOD-0023 and the settle decides by the re-read; racing a paused first call leaves no open task under a closed instance (evidence: the first call paused before its claim; the supersede wins; the resumed call writes nothing).
- [ ] AC-57 (zorunlu) — The carry-over run writes `CarriedOutDays` on the closing year's row (remaining drops by the same amount); a run before 31 Dec of that year answers 409 `LEAVE_CARRY_OVER_TOO_EARLY`; a submit allocating days to a year with a carry-over mark answers 409 `LEAVE_YEAR_CARRIED_OVER` and reserves nothing, while a resubmit of a pending request of that year keeps its reservation and allocates nothing new; no day is usable in both years.
- [ ] AC-58 (zorunlu) — `leave.admin-requests.read`, `leave.entitlements.read` and `leave.sync-runs.read` are in `ExplicitGrantOnlyPermissions.Keys` and enter no default role (`SuperAdmin`, `Admin`, `Viewer`) while `leave.requests.read` reaches a newly created `Viewer`: DL-0, by an AuthService test on the production `DefaultRolePermissionTemplate.SelectFor` (sabotage evidence: a key removed from the set goes red). A user holding only the row-scoped read keys gets 403 on `GET /entitlements` (DL-1), `GET /admin/requests` (DL-2) and `GET /admin/sync-runs` (DL-4), measured in those slices at their own evidence level.
- [ ] AC-59 (zorunlu) — Until DL-4 production registers `UnavailableLeaveEmployeeGateway` for `ILeaveEmployeeGateway` and only test hosts register `FakeEmployeeGateway`; with it `SetLeaveEntitlement` answers 503 `LEAVE_EMPLOYEE_SERVICE_UNAVAILABLE` and writes nothing (sabotage evidence: the fake in the module's registration goes red).
- [ ] AC-60 (zorunlu) — A leave type's category is fixed at creation: an update naming another category answers 400 `LEAVE_TYPE_CATEGORY_LOCKED` and changes nothing. A write other than submit whose commit result is unknown answers 503 `LEAVE_WRITE_OUTCOME_UNKNOWN` (fault-probe evidence on the executor's commit). A leave type id of another tenant answers as an unknown id, 404 `LEAVE_TYPE_NOT_FOUND`.

## Test Expectations

- Unit: validators; `LeaveDayCounter` (weekend, holiday, half holiday, half-day request, 31 Dec split); `LeaveBalanceLedger`
  (reserve, take, refund, carry-over, never negative); approver resolution (chain, pool, self excluded, null); category lock.
- Integration (Mongo): tenant isolation and 404; soft delete; unique indexes (code, entitlement, balance); concurrent submit
  (one wins); decision replay (one effect); decision vs cancel race.
- Contract: consumer test of `EmployeeGatewayAdapter` against the frozen HCM-CORE-BUNDLE fixture; producer test is the
  CT · Çalışan kaydı's (ADR-005 addendum 1/1).
- Cross-module: MOD-0023 start / approve / reject / cancel with the Task Center card; MOD-0362 holiday read.
- Frontend: smoke of the three pages; `verify_datatable_page.py` and the `quality-gate-datatable` workflow for each screen;
  `page-help-ledger.test.js` for the three page codes;
  `resx-check` 7 languages; UAS-001 panel.
- Transactions: injected failure after the first balance write (rollback); start answer lost after commit (`Unconfirmed`,
  settle on resubmit or cancel, re-check); settle claim lost to an older start that is then decided (decided, not closed);
  settle cancel answer lost (fixed key, released once); start answer lost then adopted by re-check; cancel on a
  two-step definition after step 1 approved; settle step 4 against a paused first call; re-check adopting while the
  employee's settle cancels; cancel settle stopped after MOD-0023 closed the instance and before the final leave write
  (`Cancelled` by the re-check, released once); carry-over stopped halfway (resume once); release into a carried-over
  year (flagged).
- AuthService (DL-0): AC-58's first half in `Diten.AuthService.Application.Tests`, and the existing test that counts the explicit-grant set.
- Build: Platform, AuthService, Web and Gateway; known-failures comparison (`scripts/known_failures.py`) on the closing run.
- Sabotage evidence per guard: health category, balance check, overlap check and tenant filter each shown red with the guard
  removed and green with it (CT writes the evidence script for HIGH work, card S-83).

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt |
|---|---|---|---|
| DL-0 | İK okuma anahtarlarının açık izin kaydı (AuthService) | Authorization Convention, Repo Scope | E2 |
| DL-1 | izin türleri ve izin hakları (arka uç) | Owned Objects, Entity Fields, Validation Rules, Audited Events | E4 |
| DL-2 | izin talebi, gün sayısı ve bakiye defteri (arka uç) | Owned Objects, Entity Fields, Validation Rules, Failure Path to Verify, Runtime Constraints | E4 |
| DL-3 | onay akışı, onaylayan ve Görev Merkezi kartı | Ownership and Boundaries, Authorization Convention, Failure Path to Verify | E5 |
| DL-4 | çalışan sözleşmesi tüketicisi, eşitleme ve izleme | Dependencies, Runtime Constraints, Test Expectations | E5 |
| DL-5 | üç sayfa, 7 dil ve yardım metinleri | Layout & Shell Contract, Frontend File Contract | E4 |
| DL-6 | modül kaydı ve izin anahtarları | Authorization Convention, Layout & Shell Contract | E4 |
| DL-7 | gateway rotası ve uçtan uca bağlantı | Gateway / API Routing Decision, Test Expectations | E5 |

`gerekli kanıt` is the minimum evidence level (SOP §23.1, card S-85) an acceptance record of a work package of that slice
must reach; the acceptance record cites this table row as `required_evidence_source`.

Gates per slice (review 01 F-06) — development with the fake employee adapter is allowed before the DL-4 contract gate
opens; no code is written in `Diten.Platform` before the XMC-001 gate opens (§Repo Scope); every other dispatch or
acceptance gate waits as written:

- Every slice: dispatch only after this exact version's approval row is in force on origin/main (CT-APR-01,
  module-pack-standard §16).

- Every slice that writes code in `Diten.Platform` (DL-1, DL-2, DL-3, DL-4, DL-6, DL-7): dispatch only after the XMC-001
  consent of the platform-shared-services owner is recorded (card S-90; §Repo Scope; ADR-005 "Platform'da kod yazan her
  iş paketi XMC-001 onayını ayrıca ister") — recorded 2026-10-30 (`docs/records/decisions/2026-10/hcm-leave-xmc001-platform-consent-01.md`; Open Question 1 answered).
- DL-0: the XMC-001 consent is recorded 2026-10-31 (`docs/records/decisions/2026-10/hcm-leave-xmc001-authservice-consent-01.md`); the author is this tower, the reviewer is AuthService's owner
  (card S-90; today the repository owner, no tower holds MOD-0018). DL-1's code reaches main only after DL-0, and AuthService
  with DL-0 is deployed before or with the Platform build that first carries DL-1's keys (the BL-392 lesson: a key is
  enrolled before anything can publish it). DL-1's acceptance records that no role-permission row holds the three keys.
- DL-0, DL-1, DL-2, DL-3: dispatch after this pack is approved and the registry rows are in force on origin/main. Day boundary
  (K-9): the tenant time zone (FU01 R3) is the **owner-decided default** (`rev03-gonder`, 2026-10-29); DL-2 is accepted with
  it; a later K-9 decision changes it only through a pack revision. DL-3 dispatch needs the MOD-0023 tower's recorded
  consent under card S-90 for leave's use of MOD-0023 (the leave approval definition, start and task cancel, and its
  answer on settle step 4, `SupersedeWorkflowHalfStartCommand`; without that answer step 4 is left out, AC-56) —
  **met 2026-10-29** by the tower's answer (`docs/records/decisions/2026-10/hcm-leave-mod0023-s90-answer-01.md`): all four uses need no MOD-0023 change, step 4 is confirmed (AC-56
  applies), and its warning binds DL-3's work package — after MOD-0023's next slice a repeat with the same key and a
  different body answers 409 and a cancel repeated by another actor 403; leave treats both as "re-read" and always repeats
  the cancel as the same actor. The record is the tower's measurement and consent, not an approval of this pack.
- DL-4: development and DL-1…DL-3 use the fake adapter and are **not** gated by the contract. Only DL-4 **acceptance** with
  real data needs HCM-CORE-BUNDLE frozen at a named version, MOD-0251's activation slice, the K-1 user ↔ person link and the
  K-4 service identity; until then only fake-adapter evidence, which is not acceptance.
- DL-5: the `LeaveTypes` and `LeaveEntitlements` screens are dispatched only after Master has decided the Blueprint MCR
  (`docs/records/decisions/2026-10/hcm-leave-mcr-draft-01.md`) or the owner records an exception; the `Leave` screen
  ("Leave Request", already in Blueprint) is not gated by the MCR.
- DL-7: the integration-agent task for `ocelot.json`; the gateway route is needed before DL-5 goes live (§Gateway / API
  Routing Decision).

## Ready-for-dev Checklist

Conditions of `ready-for-dev` (revision 08; unchanged in revisions 09 and 10):

- [x] Golden Reference Slim read (`golden_reference: slim`; §Frontend File Contract follows it file for file)
- [x] Frontmatter complete (service, shell, golden_reference, screens, entity_base)
- [x] Layout & Shell Contract writes the Razor layout explicitly (`_LayoutTenantShell`)
- [x] Backend File Convention matches the Golden Reference
- [x] Frontend File Contract lists the Slim files per screen
- [x] Validation Rules cover every field and action
- [x] Failure Path to Verify has duplicate, missing, unauthorized and concurrency cases
- [x] Authorization Convention lists permissions, policy and actor scope
- [x] Gateway routing decision written (§Gateway / API Routing Decision); the integration-agent task is a DL-7 gate below
- [x] Acceptance criteria testable; Test Expectations cover build, verifier, resx and smoke
- [x] Audited Events covers every write command once
- [x] Page help per page code — Purpose, Audience, steps with titles, Immediate and Related — written in §Page Help, measured by `page-help-ledger.test.js` (UXH-001); tour: none (leave is not one of the five tour flows); the 7-language texts are a DL-5 deliverable checked by `resx-check` and AC-19
- [x] Every slice carries a required evidence level (card S-85) and its gates are written (§Teslim dilimleri)
- [x] Tenant-zone day boundary recorded as the owner-decided default (`rev03-gonder`) — K-9 changes it only by revision
- [x] HCM-CORE-BUNDLE freeze is **not** a ready-for-dev condition; it gates only DL-4 acceptance (§Teslim dilimleri)
- [x] HCM domain-config lists MOD-0280-FU02 with the ADR-005 deviation (revision 08)
- [x] Lookup & Reference Data Decision (module-pack-standard §13.1): no `/api/lookups/{key}` and no new Platform lookup
  key. `Category`, `HalfDayPart`, `CancelReason` and the carry-over adjust reason are module-owned closed enums whose
  values are listed in §Entity Fields and §Validation Rules and carry rules (`health` locked, half-day only for one day,
  reason codes audited), labelled from resx; leave types come from the module's own
  `GET /types`; employees from the employee adapter; no MDM reference data
- [ ] Independent pack review (SOP §20.4) UYGUN on this exact version and an approval row by an approver with `high: evet`
  who did not write the pack — not shown by this text; measured by `scripts/pack_approval.py check` on origin/main

Slice gates — not conditions of this status; all of §Teslim dilimleri's gates, each opening before the named slice is
dispatched or accepted:

- [x] DL-1, DL-2, DL-3, DL-4, DL-6, DL-7 (code in `Diten.Platform`): XMC-001 consent recorded 2026-10-30 for the paths of
  §Repo Scope (`docs/records/decisions/2026-10/hcm-leave-xmc001-platform-consent-01.md`; card S-90; Open Question 1)
- [x] DL-0: XMC-001 consent recorded 2026-10-31 for the AuthService paths of §Repo Scope (`docs/records/decisions/2026-10/hcm-leave-xmc001-authservice-consent-01.md`; card S-90)
- [ ] Every slice: this version's approval row in force on origin/main (CT-APR-01); DL-0, DL-1, DL-2, DL-3 also the registry
  rows
- [x] DL-3: the MOD-0023 tower's consent under card S-90 recorded — the leave approval definition, start and task cancel,
  and its answer on settle step 4 (`SupersedeWorkflowHalfStartCommand`; without it step 4 is left out, AC-56) — met
  2026-10-29 (`docs/records/decisions/2026-10/hcm-leave-mod0023-s90-answer-01.md`; §Teslim dilimleri)
- [ ] DL-4 acceptance with real data: HCM-CORE-BUNDLE frozen at a named version, MOD-0251's activation slice, the K-1
  user ↔ person link and the K-4 service identity
- [ ] DL-5: Master decision on the Blueprint MCR, or an owner exception, before the LeaveTypes / LeaveEntitlements screens;
  the gateway route (DL-7) before DL-5 goes live
- [ ] DL-7: integration-agent task for `ocelot.json` opened

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
  Before review 05 (owner's word `rev05-duzelt`): the MOD-0023 tower's condition is written into the settle contract —
  replay before cancel, closed only on a re-read proof, a task-less "true" never counts (§Runtime Constraints 9, AC-39).
- **Revision 06** (2026-10-29; review `docs/records/reviews/MOD-0280-FU02/2026-10-29-948f931b.md` KOŞULLU, F-21; interim
  sub-agent check, not a formal review): F-21 definition-version limit rewritten as MOD-0023 behaves (AC-43); full
  fingerprint inputs in `StartFacts`, no due-at (AC-41); re-read to tell closed from refused and `AllowEscalated: true` on
  the settle cancel (AC-42); first-start failure inside a settle stated as unsettled. AC-1…AC-40 numbers kept (after
  the second check the texts of AC-24 and AC-39 changed; after the fourth, AC-17 and AC-37).
  Second interim sub-agent check (fresh context, different model, not a formal review): F-22 settle decides only by the
  re-read status map — `Cancelled`/`TimedOut` closed, `Completed`/`Approved`/`Rejected` decided, everything else unsettled
  (AC-39, AC-44, AC-46); F-23 fixed settle cancel key and a read-only re-check with HR endpoint and
  `leave.admin-requests.reconcile` lead out of `NeedsReconciliation` (AC-42, AC-45); F-24 AC-24, the command list, a
  failure path and the test expectations no longer describe the retired FU01 retire/compensation path.
  Third interim sub-agent check (fresh context, not a formal review): F-25 the re-check has its own `Unconfirmed` branch
  (adopt an open task as `Started`, apply a decision, otherwise no change) and the employee may resubmit from
  `NeedsReconciliation` (AC-47 for the re-check; the endpoint table and validation rules for the resubmit); F-26 the settle cancels the instance's open task of any step (AC-48); F-27 the
  abandoned-start recovery job is off by default — listed in §Dependencies, and the cases that stay reserved are named;
  F-28 persisted `PendingIntent`, finalize and re-check made disjoint, audit events fixed (AC-49); F-29 leftover FU01
  references, the race rule, audit envelopes and the claim citation corrected.
  Fourth interim sub-agent check (fresh context, not a formal review): F-30 a passive closure of an unconfirmed start
  withdraws the request (closed under the current key is final) and a completed `resubmit` intent withdraws too; the
  cases that stay reserved are named in full (task-less `Active` start, no instance, employee unable to act) (AC-52);
  F-31 settle, re-check and finalize are conditional on the request version read before the MOD-0023 call (AC-51);
  F-32 one audit event per path, `leave.request.reconciliation-needed` added, `LEAVE_ALREADY_DECIDED` (AC-17, AC-50);
  F-33 reconciliation list = `GET /admin/requests` filter, HR "To reconcile" filter and "Re-check" action, employee
  "Send again" and approval-link label (AC-37, AC-52); F-34 task read method named (`ListByInstanceIdAsync`, not
  `GetActiveByInstanceIdAsync`); F-35 case (a) exits corrected; F-36 note texts, the lost-cancel failure path and
  `LEAVE_RESUBMIT_NOT_APPLICABLE`.
  Fifth interim sub-agent check (fresh context, not a formal review): F-37 only `Pending` requests are re-checked,
  finalized or listed; leaving `Pending` sets `WorkflowStartState = Closed` (AC-53); F-38 the start result write is
  version-conditional and finalize reads only the current submission's instance (AC-54); F-39 AC-49 and the
  `WorkflowStartState` row corrected; F-40 "no active definition version" moved to the no-instance case; F-41 the HR
  re-check endpoint's answers are in the path table (AC-50).
  Sixth interim sub-agent check (fresh context, not a formal review; KOŞULLU, no blocker): F-42 MOD-0023 already closes a
  task-less start at once (`SupersedeWorkflowHalfStartCommand`); settle step 4 uses it, pending the MOD-0023 tower's
  confirmation (S-90), and the earlier statement that closing a start needs a new MOD-0023 capability is withdrawn;
  F-43 AC-47 aligned with AC-52; F-44 days returning to a closed year are flagged for HR (AC-55); F-45 AC-53 names both
  guards; F-46 finalize-withdrawn and race-loser rows in the path table; F-47 lease and version written together;
  F-48 the starter-approver case is MOD-0023's own refusal.
  Seventh interim sub-agent check (fresh context, not a formal review; KOŞULLU, no blocker): F-49 every "refused replay
  stays reserved" text now says "unless step 4 closes it" and step 4 has its own AC (AC-37, AC-39, AC-56); F-50
  `PendingIntent` is written before any MOD-0023 call and finalize maps every `Cancelled` instance (AC-46, AC-49, AC-51);
  F-51 `CarriedOutDays`, carry-over only after year end, no allocation into a carried-over year (AC-57); F-52 every
  release into a carried-over year is flagged and the adjust clears the flag (AC-55); F-53 AC-53 sabotage set-up and the
  case (a) exits corrected.
  Eighth interim sub-agent check (fresh context, not a formal review; KOŞULLU, no blocker): F-54 an adoption keeps
  `PendingIntent`; F-55 `CarriedOutDays` in AC-7, AC-27, the balance cards and the carry-over audit events; F-56
  `AdjustLeaveCarryOver` moves both rows by the same amount within the limit and floors and clears the flag (AC-55);
  F-57 the `NeedsReconciliation` examples tied to step 4; F-58 AC-53 set-up states the selected states and the size of
  the second reservation; F-59 the reconciliation-needed event compares with the value before the lease write; F-60 a
  resubmit keeps its reservation in a carried-over year (AC-57); F-61 the Tasks citation and the two cases the MOD-0023
  tower is asked to confirm; F-62 every read of a request (employee, HR, approver) runs finalize or re-check.
  Ninth interim sub-agent check (fresh context, not a formal review): **UYGUN**, no blocker and no medium finding; its
  four low notes applied: F-63 the re-check list of triggers matches every read (AC-45); F-64 AC-24 separates a
  cancel's release from a rejection's; F-65 AC-56 applies only after the MOD-0023 tower's confirmation; F-66
  `LeaveDecisionFinalizer` and `LeaveRequestWriter` listed in both the component table and the folder tree.
- **Revision 07** (2026-10-29; review `docs/records/reviews/MOD-0280-FU02/2026-10-29-878a24f0.md` KOŞULLU, F-67; F-21
  closed): a closed instance re-read for an `Unconfirmed` request maps the stored intent as the runtime table already
  said — `Cancelled` when `PendingIntent = cancel`, otherwise `Withdrawn` — in AC-47, AC-52 and two failure paths; the
  stop between MOD-0023's close and the final leave write is a failure path and the evidence of AC-52. AC numbers kept;
  only AC-47 and AC-52 texts changed. Interim sub-agent check (fresh context, not a formal review; KOŞULLU, no blocker):
  F-68 AC-52 names the answer of a later cancel (`LEAVE_CANCEL_TOO_LATE`, existing rule) instead of an undefined one;
  F-69 AC-52 uses AC-53's second-reservation set-up; F-70 the re-check result code of that exit is `intent-completed`;
  F-71 case (a) names `cancel` → `Cancelled`; F-72 the revision 06 header now records the owner's word `rev06-gonder`.
  Second interim sub-agent check (fresh context, not a formal review): **UYGUN**; its three low notes applied: F-73 one
  recheck result code per result (`withdrawn` only without an intent, `intent-completed` for any completed intent);
  F-74 the stopped cancel settle is in the transaction test list; F-75 "send a new request" follows `Withdrawn` only.
- **Revision 08** (2026-10-29, owner's word `rev08-hazir`; review `docs/records/reviews/MOD-0280-FU02/2026-10-29-0517be85.md`
  UYGUN, F-67 closed): `status: ready-for-dev` — an approval row authorizes code only on a ready-for-dev version
  (`scripts/pack_approval.py` `DRAFT_WARNING`, setup finding S-49 in
  `docs/records/audits/2026-10/ct-setup-test-01/FINDINGS.md`). The opening status warning now says ready-for-dev. The
  Ready-for-dev Checklist is split into the conditions of this status (met, with where each is shown) and the slice gates
  of §Teslim dilimleri, which stay open. Item texts changed: the gateway item keeps the decision and moves the
  integration-agent task to the DL-7 gate; the help-key item names the keys and leaves their 7-language texts to DL-5;
  the review-and-approval item stays unchecked and is measured by `pack_approval.py check`. §Teslim dilimleri: the
  XMC-001 gate now covers every slice that writes code in `Diten.Platform` (DL-1…DL-4, DL-6, DL-7), as §Repo Scope and
  ADR-005 already required; the DL-3 MOD-0023 gate names what the tower consents to; the gateway route is needed before
  DL-5 goes live; every slice waits for this version's approval row; fake-adapter development is limited to the DL-4
  gate. Validation Rules gained the rows the entity fields already implied (`LEAVE_TYPE_UNPAID_REDUCES_BALANCE`,
  `LEAVE_TYPE_CARRY_OVER_LIMIT_INVALID`, `LEAVE_TYPE_NOT_ACTIVE`, the carry-over adjust codes) and AC-55 names the adjust
  codes; Failure Path to Verify gained the missing-field and duplicate cases; the checklist gained the Lookup & Reference
  Data Decision; Recorded Deviations R-5 (`entity_base`) and R-6 (folder layout) follow the MOD-0280-FU01 precedent.
  HCM domain-config lists MOD-0280-FU02 with the ADR-005 deviation. No runtime rule changed; of the AC texts only AC-55
  changed (its refusals now carry codes).
  After review 08 (`docs/records/reviews/MOD-0280-FU02/2026-10-29-775ce53c.md`, KOŞULLU; owner's choice "Şimdi düzelt"):
  F-76 `CancelReason` and the carry-over adjust reason are listed closed code lists with membership validation, an
  invalid-code answer and a failure path; F-77 §Page Help fixes the help keys, steps, Immediate and Related per page and
  AC-19 measures them. Then: Repo Scope names the help catalog, help resx and nav-key files (added keys only); the help
  table has a purpose per page, an Immediate text that matches the cancel rules and an HR step; the Frontend File
  Contract shows HR's cancel with a reason select and the adjust reason select; AC-11 and AC-13 measure the closed
  reason lists; AC-19 and Test Expectations name the help ledger guard. Of the AC texts AC-11, AC-13 and AC-19 changed.
- **Revision 09** (2026-10-30, owner's choice "Paketi düzelt", after revision 08 was approved): the first work package's
  measurement of `Diten.Platform` (HEAD 63acdf2ec) found §Repo Scope naming a `Diten.Platform.Persistence` project that does
  not exist and leaving out the shared registration files every new module adds lines to (collections, schema profile and
  manifest, the two DI files, the job registrar, the notification template seed) — MOD-0280-FU01 enumerated the same
  kind of additions in its pack §5.1. §Repo Scope now lists the measured paths, leave's own files and the shared files
  with "lines added only", and the Web test paths; §Backend File Convention names the repository file. §Audited Events states the audit
  category (`AuditCategory.Tasks`, as FU01) and source module `leave`. §Ownership and Boundaries: the Task Center card is
  the Task Center's `IApprovalSourceResolver`, implemented in leave's own folder, not MOD-0023's (MOD-0023 tower's
  answer). The XMC-001 consent is recorded (§Placement deviation, §Teslim dilimleri, checklist, Open Question 1).
  After an interim sub-agent check (fresh context, not a formal review; KOŞULLU, no blocker): F-78 no role template is
  changed — keys are granted by the tenant's role administration, as FU01; AC-28 measures a token with the employee key
  set; a default role-template grant is a follow-up; F-79 `LeaveLinks` builds the e-mail links, the card link is
  app-relative in the resolver; F-80 a trailing comma on a list's last item and leave `using` lines count as additions;
  F-81 the DL-3 MOD-0023 gate is marked met by the tower's answer (`docs/records/decisions/2026-10/hcm-leave-mod0023-s90-answer-01.md`) with its 403 / 409 warning, and AC-56 applies.
  No runtime rule, entity, field, validation row, endpoint, permission or slice changed; of the AC texts AC-28 and the
  AC-56 condition note changed.
- **Revision 10** (2026-10-31, owner's choice "Yalnız açık izin"): an independent read of the first work package's prompt
  found revision 09's role sentence false. AuthService `DefaultRolePermissionTemplate.SelectFor` gives the default `Viewer`
  every tenant `read` key and `PlatformPermissionAutoRegistrationWorker` registers each `[HasPermission]` key at start-up, so
  the three HR read keys would have reached ordinary users; review 10 had said it could not verify that claim from its ZIP.
  No code had been written. §Authorization Convention now states the measured paths; the three keys are explicit-grant only
  through a new slice DL-0 (§Repo Scope, §Protected Paths, §Teslim dilimleri, checklist, AC-58; consent `docs/records/decisions/2026-10/hcm-leave-xmc001-authservice-consent-01.md`). The same read
  closed gaps the pack left to the work package: `LEAVE_TYPE_CODE_INVALID`, `LEAVE_TYPE_NOT_FOUND`, `LEAVE_TYPE_LABEL_TOO_LONG`,
  `LEAVE_ENTITLEMENT_YEAR_INVALID`, `LEAVE_WRITE_OUTCOME_UNKNOWN`; the label limit and the year range; a category fixed at
  creation; an entitlement naming an unknown or foreign type answers 404 (AC-2); entitlement set is closed when the employee
  service is unreachable; the closed production adapter until DL-4. §Audited Events now separates the business event from the
  record `AuditBehavior` writes for every dispatched command, because the path table's "none" could not otherwise hold, and
  says how names, values and ids are carried. The floor's answer lists pending requests only to a holder of
  `leave.admin-requests.read`. New text cites files and section names, not line numbers (card §1.1/5); older citations stay as
  they are. No entity field, endpoint or permission key changed; of the AC texts AC-1, AC-14, AC-17, AC-27, AC-28, AC-32, AC-38
  and AC-50 changed and AC-58, AC-59 and AC-60 are new.

## Follow-up Items

- Block 2 (HCM-LEAVE-07): absence record by HR; sickness / medical report types, document upload and description — only
  after the MOD-0314 rule and legal approval; K2 audit class for special-category data.
- Timesheet link (HCM-LEAVE-08): approved leave days read-only in MOD-0280-FU01's week (XMC-001).
- Legal-entity-scoped HR access; anniversary- and seniority-based statutory entitlement (Turkish labour law — needs a legal
  source; CT's general knowledge only); partial refund on early return; delegation while the manager is on leave.
- Attendance calendar, schedules / rosters, MOD-0315 HR views, Task Center "on leave" warning, meeting "on leave" status.
- Extraction to `Diten.HcmService` with FU01 (ADR-005 trigger).
- A dedicated HCM audit category for MOD-0280-FU01 and MOD-0280-FU02 instead of the borrowed `AuditCategory.Tasks`
  (shared audit enum; needs its own consent).
- Default grant of the leave keys in AuthService's role templates (employee / manager), if the platform adds such templates;
  needs AuthService's consent (XMC-001). The three explicit-grant keys stay out of any default.
- A `leave.*` row in PKS-001 §4's namespace table, as `time-entry.*` has (requested from Kurulum 2026-10-31 for its weekly
  window; `.antigravity/**`; §Authorization Convention names the namespace until then).

## Open Questions

1. **XMC-001 approver** for code in `Diten.Platform` (platform-shared-services) — **answered 2026-10-30**: the card names no
   owner for platform-shared-services today, so the owner (repository owner) consented, for the paths of §Repo Scope
   only (`docs/records/decisions/2026-10/hcm-leave-xmc001-platform-consent-01.md`). It assigns no lasting owner to platform-shared-services; a change outside §Repo Scope needs a new
   consent.
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
| R-5 | module-pack-standard §2 `entity_base` list | `TenantScopedEntity`, Platform's live tenant-owned base (the standard's "BaseEntity" is the older name) | MOD-0280-FU01 precedent (künye) |
| R-6 | module-pack-standard §4 `Features/{Module}/` without extra sub-folders | one `Features/Leave/` for three screens with `Services/`, `Adapters/`, `Providers/`, `BackgroundJobs/`, `SelfRegistration/` (§Backend File Convention) | MOD-0280-FU01 precedent (`Features/TimeEntry/` with `Services/`, `Adapters/`) |
| R-7 | card S-90/2: a proposal to the owning module's pack first | the enrolment is proposed in this pack (DL-0): `MOD-0018-rbac-abac-authorization.md` does not list the explicit-grant set, and MOD-0280-FU01's enrolment went the same way | `docs/records/decisions/2026-10/hcm-leave-xmc001-authservice-consent-01.md` |
