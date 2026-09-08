---
id: CAND-CAP-0036
name: Time, Attendance & Leave Interface
domain: human-capital-management
service: Diten.HumanCapitalService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0036-time-attendance-leave-interface
started: 2026-09-07
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0036
legacy_excel_id: MOD-0315
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
runtime_service_port: 5059
gateway_port: 5080
bootstrap_type: metadata-only-backend-api-readiness
runtime_owner_key: hcm.time-attendance-leave
permission_namespace:
  - hcm.time-attendance-leave.read
  - hcm.time-attendance-leave.manage
  - hcm.time-attendance-leave.evaluate
  - hcm.time-attendance-leave.audit.read
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0036 - Time, Attendance & Leave Interface

> Status: draft. This pack records the first metadata-only backend/API
> time, attendance and leave interface readiness contract slice under
> `services/Diten.HumanCapitalService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5080` -> HCM `5059`). Production time, attendance and leave
> interface workflow, timesheet intake, attendance sync, leave request, leave
> balance, schedule consumption, automated decision behavior, manager/employee
> time/leave UX, notification/document integration, and sensitive persistence
> remain closed. This is a CONSUMPTION INTERFACE over the SHARED
> time & attendance source and leave source; it does not own or store
> timesheet, attendance, or leave data. It does NOT authorize real timesheet
> capture, clock/punch record persistence, attendance log storage, leave
> request workflow execution, leave balance/accrual calculation or persistence,
> schedule/roster management, approval workflow, payroll feed, model output,
> scoring, or automated decision. `CAND-CAP-0036` remains a
> governance/documentation identity only and must not be written into runtime
> literals. `MOD-0315` remains blocked for HCM use until future EA canonical
> MOD assignment.

## 1. Module Summary

`CAND-CAP-0036` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-C capability named
`Time, Attendance & Leave Interface`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0315`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0036` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API time, attendance and
leave interface readiness contract slice, mirroring the HR Documentation &
Evidence Workspace slice (`CAND-CAP-0035` / `MOD-0313`) and the completed HR KPI
& Analytics Facade slice (`CAND-CAP-0034` / `MOD-0312`) exactly. This
capability is a CONSUMPTION INTERFACE over the shared time & attendance source
and leave source: it is metadata-only readiness and does NOT own or store
timesheet, attendance, or leave data. Time, attendance and leave interface
workflow, timesheet intake, attendance sync, leave request, leave balance,
schedule consumption, automated decision behavior, manager/employee time/leave
UX, notification/document integration, and sensitive persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- HCM-native time, attendance and leave interface governance boundary.
- Sequencing after completed HCM employee, governance, position, offboarding,
  applicant intake, candidate pipeline, offer management, onboarding,
  employment change, performance review, competency/skills, development plan,
  learning/training, succession/high-potential, workforce planning,
  headcount/position budget planning, HR KPI/analytics facade, and HR
  documentation/evidence workspace foundation modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only time, attendance and leave interface readiness backend/API
  contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Explicit exclusion of time, attendance and leave interface workflow
  execution.
- Explicit exclusion of timesheet intake, attendance sync, leave request, leave
  balance, schedule consumption, model output, and automated decision behavior.
- Explicit exclusion of manager-facing and employee-facing time/leave
  UX beyond the readiness-metadata CRUD (those fields are reserved and out of
  scope).
- Explicit exclusion of free-text time/leave notes, time/leave narrative,
  timesheet entry, punch record, attendance log, leave balance, accrual amount,
  schedule payload, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence.

Not owned by this pack:

- Real time, attendance and leave interface execution, real timesheet capture,
  clock/punch record persistence, attendance log storage, leave request workflow
  execution, leave balance/accrual calculation or persistence, schedule/roster
  management, approval workflow, payroll feed, or model output.
- Time & attendance source ownership beyond dependency/context references.
- Leave source ownership beyond dependency/context references.
- Notification, notification provider, controlled document, or external
  time & attendance provider implementation.
- Development plan, competency/skills, performance review, learning/training,
  succession, workforce planning, headcount/budget, HR KPI/analytics facade, HR
  documentation/evidence workspace, shared time & attendance source, leave
  source, TEP, or PSS ownership transfer.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| TimeAttendanceLeaveCapabilityBoundary | Governance boundary | Defines what a future HCM time, attendance and leave interface module may own. |
| TimeAttendanceLeaveReadinessBoundary | Governance boundary | Separates readiness metadata from time/attendance/leave interface workflow execution. |
| TimesheetAttendanceBoundary | Governance boundary | Blocks timesheet intake, attendance sync, and leave request execution. |
| LeaveRequestBalanceBoundary | Governance boundary | Blocks leave balance and schedule consumption execution. |
| TimeAttendanceLeaveDecisionBoundary | Governance boundary | Blocks scoring, rating, ranking, model-output, and automated decision persistence. |
| TimeAttendanceLeaveUxBoundary | Governance boundary | Blocks manager and employee time/leave UX beyond readiness-metadata CRUD. |
| TimeAttendanceLeaveSensitiveDataBoundary | Governance boundary | Blocks narrative, timesheet entry, punch record, attendance log, leave balance, credential, and PII-heavy persistence. |
| TimeAttendanceLeaveDependencyBoundary | Governance boundary | Keeps time-attendance-source/leave-source/notification/document dependencies deferred and out of scope. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| TimeAttendanceLeaveInterfaceWorkflow | Deferred runtime | Time, attendance and leave interface workflow execution; not authorized. |
| TimesheetAttendanceEngine | Deferred runtime | Timesheet intake, attendance sync, and leave request; not authorized. |
| LeaveRequestBalanceEngine | Deferred runtime | Leave balance, schedule consumption, and model output; not authorized. |
| ManagerEmployeeTimeLeaveExperience | Deferred frontend/runtime | Manager/employee time/leave UX beyond readiness CRUD; not authorized. |
| TimeAttendanceLeaveDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| TimeAttendanceLeaveNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| TimeAttendanceLeaveReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/TimeAttendanceLeaveReadinessMetadata.cs`. |
| TimeAttendanceLeaveReadinessState | Domain enum | Readiness state; `Domain/Enums/TimeAttendanceLeaveReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| ITimeAttendanceLeaveReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/ITimeAttendanceLeaveReadinessMetadataRepository.cs`. |
| MongoTimeAttendanceLeaveReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `hcm_time_attendance_leave_readiness`. |
| TimeAttendanceLeave Application features | Application (CQRS/MediatR) | `Application/Features/TimeAttendanceLeave/**` (Models/Guard/Commands/Queries/Handlers). |
| TimeAttendanceLeaveController | API controller | Thin controller; `Api/Controllers/Hcm/TimeAttendanceLeaveController.cs`. |
| TimeAttendanceLeave golden-compact CRUD | Frontend | Controller + `Views/HumanCapital/TimeAttendanceLeave/**` + `wwwroot/assets/js/HumanCapital/TimeAttendanceLeave/**` + Resources + nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness-state fields are of type
`TimeAttendanceLeaveReadinessState`):

- `Code`
- `DisplayName`
- `TimeAttendanceLeaveReadinessState`
- `TimesheetIntakeBoundaryState`
- `AttendanceSyncBoundaryState`
- `LeaveRequestBoundaryState`
- `LeaveBalanceBoundaryState`
- `ScheduleConsumptionBoundaryState`
- `AutomatedDecisionBoundaryState`
- `TimeAttendanceSourceDependencyState`
- `LeaveSourceDependencyState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `TimeAttendanceLeaveReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Manager-facing time/leave fields (manager timesheet approval, manager
  review, manager action UX state) are reserved and out of scope.
- Employee-facing time/leave fields (employee self-service input,
  employee leave request, employee self-service UX state) are reserved and
  out of scope.
- No timesheet/attendance/leave content field (timesheet entry/punch
  record/attendance log/leave balance/accrual amount/schedule payload). This is a
  metadata-only readiness slice with no timesheet/attendance/leave content
  attribute. Timesheet ENTRIES, punch RECORDS, attendance LOGS, leave
  BALANCES, and schedule PAYLOADS are FORBIDDEN; only boundary/readiness STATE
  metadata is persisted, never any timesheet entry, punch record, attendance
  log, leave balance, accrual amount, or schedule payload.

Forbidden field classes:

- Time, attendance and leave interface workflow body, timesheet intake
  submission payload, attendance sync payload, leave request payload,
  leave balance payload, schedule consumption payload, approval decision body,
  action evidence, or completed time/leave content.
- Readiness score, rating value, rank, time/leave outcome, model output,
  automated decision result, recommendation output, or evaluator scoring
  payload.
- Free-text time/leave notes, time/leave narrative, manager comments,
  employee comments, HR notes, or sensitive time/leave body.
- Timesheet entry, punch record, attendance log, leave balance, accrual amount,
  schedule payload, raw provider payload, or uploaded file metadata.
- Timesheet entry, punch record, attendance log, leave balance, accrual amount,
  schedule payload, score, rating, model output, benefits election,
  payroll details, tax details, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  PII-heavy employee/profile fields.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0036-time-attendance-leave-interface.md`

Candidate future runtime service:

- `Diten.HumanCapitalService` (port `5059`)

Runtime repo scope:

- `services/Diten.HumanCapitalService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/HumanCapital/TimeAttendanceLeave/**`,
  `wwwroot/assets/js/HumanCapital/TimeAttendanceLeave/**`, Resources, nav entry).
- Gateway route exposure through the API gateway (`5080` -> HCM `5059`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.HumanCapitalService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned TimeAttendanceLeave CRUD objects listed in
  §3/§5.
- `gateway/**` outside the owned TimeAttendanceLeave readiness route.
- `services/Diten.TalentEcosystemService/**`
- `services/Diten.Platform/**`
- `services/Diten.PssService/**`
- global `tests/**`
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/human-capital-management/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Completed HCM foundation dependencies:

- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, done.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, done.
- `CAND-CAP-0009` - Position & Organization Assignment, done.
- `CAND-CAP-0010` - Offboarding & Exit Management, done.
- `CAND-CAP-0022` - Recruitment / Applicant Intake, done.
- `CAND-CAP-0023` - Candidate Pipeline & Interview Management, done.
- `CAND-CAP-0024` - Offer Management, done.
- `CAND-CAP-0025` - Employee Onboarding, done.
- `CAND-CAP-0026` - Employment Change / Transfer / Promotion, done.
- `CAND-CAP-0027` - Performance Review Management, done.
- `CAND-CAP-0028` - Competency & Skills Assessment, done.
- `CAND-CAP-0029` - Development Plan Management, done.
- `CAND-CAP-0030` - Learning / Training Records, done.
- `CAND-CAP-0031` - Succession & High-Potential Tracking, done.
- `CAND-CAP-0032` - Workforce Planning, done.
- `CAND-CAP-0033` - Headcount & Position Budget Planning, done.
- `CAND-CAP-0034` - HR KPI & Analytics Facade, done.
- `CAND-CAP-0035` - HR Documentation & Evidence Workspace, done.

TEP context dependencies:

- `CAND-CAP-0011` through `CAND-CAP-0021` remain dependency/context only.

PSS backbone context:

- `MOD-0288`, `MOD-0251`, `MOD-0279`, `MOD-0280`, and `MOD-0281` remain
  dependency/backbone context only.

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is
authorized only for the first metadata-only backend/API readiness slice, along
with the golden-compact readiness CRUD frontend and gateway route.

Authorized runtime boundary:

- Runtime owner/key: `hcm.time-attendance-leave`.
- Permission namespace:
  - `hcm.time-attendance-leave.read`
  - `hcm.time-attendance-leave.manage`
  - `hcm.time-attendance-leave.evaluate`
  - `hcm.time-attendance-leave.audit.read`
- Runtime repo scope: `services/Diten.HumanCapitalService/**`.
- Thin controller (`Api/Controllers/Hcm/TimeAttendanceLeaveController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `hcm_time_attendance_leave_readiness`.
- Active tenant `Code` uniqueness.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice fixes the forbidden-marker matcher to use WORD/TOKEN
  boundaries (`\b` regex) instead of a bare substring `Contains`, so legitimate
  domain words (`time`, `attendance`, `leave`, `timesheet`, `schedule`) are not
  falsely rejected while true forbidden markers (`payroll`, `salary`,
  `raw_payload`, `attachment`, ...) are still caught.

Explicitly unauthorized:

- Time, attendance and leave interface workflow.
- Timesheet intake, attendance sync, leave request, leave balance, schedule
  consumption.
- Real timesheet capture, clock/punch record persistence, attendance log
  storage, leave request workflow execution, leave balance/accrual calculation
  or persistence, schedule/roster management, approval workflow, and payroll
  feed.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Manager/employee time/leave UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Sensitive payload and timesheet/attendance/leave content persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- Tenant shell nav entry `Time, Attendance & Leave Interface`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Manager time/leave UX beyond readiness-metadata CRUD.
- Employee time/leave/self-service UX beyond readiness-metadata CRUD.
- Timesheet intake, attendance sync, leave request, leave balance, schedule
  consumption, scoring, or model-output UI.
- Document, notification, export, upload, or attachment UI.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> HCM `5059`). This is a CONSUMPTION INTERFACE that owns no
timesheet, attendance, or leave runtime: it does NOT authorize real timesheet
capture, clock/punch record persistence, attendance log storage, leave request
workflow execution, leave balance/accrual calculation or persistence,
schedule/roster management, approval workflow, payroll feed, or ownership of the
shared time & attendance source / leave source.

Authorized API surface:

- `GET /api/time-attendance-leave` - list readiness metadata.
- `GET /api/time-attendance-leave/{id}` - get readiness metadata by id.
- `POST /api/time-attendance-leave` - create readiness metadata.
- `POST /api/time-attendance-leave/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/time-attendance-leave/{id}` - soft-delete.
- `GET /api/time-attendance-leave/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get time, attendance and leave interface readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No time/leave interface workflow, timesheet intake, attendance sync, leave
  request, leave balance, schedule consumption, timesheet capture, clock/punch
  record or attendance log persistence, leave balance/accrual calculation or
  persistence, schedule/roster management, approval workflow, payroll feed,
  scoring/model-output persistence, automated decision behavior,
  notification/document integration, or sensitive/time-leave-content payload
  persistence.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only time, attendance and leave interface readiness state.
- Boundary-state metadata for timesheet intake, attendance sync, leave request,
  leave balance, schedule consumption, automated decision,
  consent, minimization, retention, and evidence.
- Dependency-state metadata for time & attendance source, leave source,
  document, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Time, attendance and leave interface workflow payload.
- Timesheet-intake/attendance-sync/leave-request/leave-balance/schedule-consumption
  execution payload.
- Real timesheet capture, clock/punch record or attendance log persistence.
- Timesheet entry, punch record, attendance log, leave balance, accrual amount,
  or schedule payload.
- Leave request workflow execution, leave balance/accrual calculation,
  schedule/roster management, approval workflow, or payroll feed output.
- Readiness score/rating/rank/model output.
- Automated decision result.
- Free-text time/leave notes or time/leave narrative.
- Leave request transcript payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy employee/profile details.
- Compensation/payroll/benefits payload.

## 12. Permission Boundary

Approved permission namespace:

- `hcm.time-attendance-leave.read`
- `hcm.time-attendance-leave.manage`
- `hcm.time-attendance-leave.evaluate`
- `hcm.time-attendance-leave.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Runtime work must be tenant-aware and fail closed. The backend/API slice must
resolve tenant server-side and must not expose `TenantId` in request DTOs.

Security guardrails:

- Candidate identity must not become a runtime literal.
- Blocked legacy ID must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation.
- Time/leave data minimization fail-closed behavior.
- Retention/evidence/audit/leave-balance/deletion as local/deferred metadata.
- Exclusion of time/leave narrative, timesheet/attendance/leave payloads, leave
  balance content, and PII-heavy data.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0280` Time & Attendance (external provider / source).

The shared external time & attendance provider (`MOD-0280`) and leave source
dependency are deferred and out of scope: this interface only consumes readiness
metadata and owns no time, attendance, or leave runtime. No integration
implementation, provider payload persistence, or document/notification UI is
authorized.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0036`.
3. AC-03: Legacy Excel ID `MOD-0315` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `hcm.time-attendance-leave`.
5. AC-05: Permission namespace is limited to `hcm.time-attendance-leave.read`,
   `hcm.time-attendance-leave.manage`, `hcm.time-attendance-leave.evaluate`, and
   `hcm.time-attendance-leave.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.HumanCapitalService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection `hcm_time_attendance_leave_readiness`)
   includes active tenant `Code` uniqueness and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Time, attendance and leave interface workflow, timesheet intake,
    attendance sync, leave request, leave balance, schedule consumption, model
    output, and automated decision behavior remain unauthorized.
14. AC-14: Manager/employee time/leave fields remain reserved/out of
    scope; no timesheet/attendance/leave content field is present.
15. AC-15: Free-text time/leave notes, time/leave narrative, timesheet entry,
    punch record, attendance log, leave balance, accrual amount, schedule
    payload, raw provider payload, credential/token/secret/password, and
    PII-heavy persistence remain unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: HCM, TEP, and PSS dependency boundaries are recorded.
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0036|MOD-0315` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build HCM API:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `TimeAttendanceLeave` filter
  (`Application.Tests/TimeAttendanceLeaveTests.cs`, handler behavior; fix-absent
  should be RED).
- Run full HCM Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, and active tenant `Code`
  uniqueness (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`time`, `attendance`,
  `leave`, `timesheet`, `schedule`) are not falsely rejected while true forbidden
  markers (`payroll`, `salary`, `raw_payload`, `attachment`, ...) are
  still caught.
- Verify forbidden workflow/timesheet-intake/attendance-sync/leave-request/leave-balance/
  schedule-consumption/scoring/decision/sensitive/time-leave-content fields are absent.
- Verify manager/employee time/leave fields are reserved/absent and no
  timesheet/attendance/leave content field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0036|MOD-0315" services/Diten.HumanCapitalService frontend gateway tests`

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Time, attendance and leave interface workflow prohibition is accepted.
- [x] Timesheet intake/attendance sync/leave request/leave
  balance/schedule consumption prohibition is accepted.
- [x] Timesheet capture/clock-punch record/attendance log/leave-balance-persistence
  prohibition is accepted.
- [x] Leave request workflow/leave balance-accrual calculation/schedule-roster
  management/approval-workflow/payroll-feed prohibition is accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved manager/employee time/leave fields and no-time-leave-content-field
  constraint are accepted.
- [x] Sensitive payload and timesheet/attendance/leave content prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] HCM/TEP/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only time, attendance and leave interface readiness
  contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Time/leave data minimization policy.
- [x] Retention/evidence/audit/leave-balance/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.HumanCapitalService/**`
  and the owned `frontend/Diten.Web/**` TimeAttendanceLeave objects.
- Runtime owner/key remains limited to `hcm.time-attendance-leave`.
- Permission namespace remains limited to `hcm.time-attendance-leave.read`,
  `hcm.time-attendance-leave.manage`, `hcm.time-attendance-leave.evaluate`, and
  `hcm.time-attendance-leave.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `TimeAttendanceSourceDependencyState`, `LeaveSourceDependencyState`,
  `DocumentDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Manager/employee time/leave UX fields remain reserved/out of scope
  and no timesheet/attendance/leave content field is present.
- TEP, PSS, and Platform scopes remain closed.
- Time, attendance and leave interface workflow, timesheet intake, attendance
  sync, leave request, leave balance, schedule consumption, timesheet capture,
  clock/punch record or attendance log persistence, leave balance/accrual
  calculation or persistence, schedule/roster management, approval workflow,
  payroll feed, model output, automated decision behavior,
  notification/document integration, free-text time/leave notes, time/leave
  narrative, raw provider payload, credential/token/secret/password,
  timesheet/attendance/leave content, and PII-heavy persistence remain out of
  scope.

Done-promotion reconciliation note:

- Pending. Done-promotion readiness audit will be recorded when the draft slice
  completes implementation review, with build/test/runtime-literal-scan and
  golden-compact/column-parity evidence attached.

## 20. Open Blockers / Waivers

Open blockers for governance continuation:

- none.

Open blockers for runtime-ready:

- Draft implementation and done-promotion readiness audit are pending.

Waivers recorded:

- Candidate gate script is not executable in this checkout because
  `.antigravity/scripts/verify_module_id.py` is missing; recorded as governance
  note. The specified command
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0036 --name "Time, Attendance & Leave Interface"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0036`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Time/leave data minimization fail-closed policy approved.
- Retention/evidence/audit/leave-balance/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0036|MOD-0315" services/Diten.HumanCapitalService frontend gateway tests`

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real time, attendance and leave interface workflow.
- Timesheet intake and attendance sync.
- Leave request, leave balance, and schedule consumption governance.
- Automated decision approval.
- Manager/employee time/leave UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
