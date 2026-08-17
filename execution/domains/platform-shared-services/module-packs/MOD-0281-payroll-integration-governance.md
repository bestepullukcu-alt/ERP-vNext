---
id: MOD-0281
name: Payroll Integration & Governance
friendly_name: Payroll Integration Governance
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
owner: enterprise-architect / platform-team / integration-ops
branch: feature/pss/mod-0281-payroll-integration-governance
started: 2026-08-10
target: 2026-08-31
form_field_count: 0
canonical_source: "docs/System Capability & Implementation Blueprint - master 5.xlsx#Blueprint_Data row 282"
source_dcp: "execution/portfolio/delivery-capability-packs/DCP-005-payroll-integration-governance-readiness.md"
---

# MOD-0281 - Payroll Integration & Governance

> **Execution status:** This module pack is ready-for-dev for the first
> provider-neutral payroll integration governance backend/API contract slice in
> `Diten.Platform` under `platform-shared-services`.
>
> **Canonical-name guard:** The canonical identity is exactly
> `MOD-0281 - Payroll Integration & Governance`. Do not rename this pack to
> `Payroll Governance & Payroll Controls` without an Enterprise Architect
> supersession through DCP-002.
>
> **DCP-002 preflight evidence:**
>
> ```text
> python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0281 --name "Payroll Integration & Governance"
> OK  MOD-0281: proven against Blueprint/registry.
> ```
>
> **Authorized production scope:** Only MOD-0281 payroll integration governance
> metadata, tenant-scoped API behavior, validation, persistence, authorization,
> and tests described in Section 5 may be implemented.
>
> **Explicit exclusions:** No UI, Razor, JavaScript, DataTable, RESX, gateway
> `ocelot.json` direct edit, provider-specific adapter/client/webhook/background
> sync, payroll calculation, payroll SoR ownership, MOD-0279/MOD-0280 record
> copying, raw payroll/time/attendance/HRIS payload persistence, or
> credential/token/secret persistence is authorized by this pack.

## 1. Module Summary

- **Purpose:** Provide a tenant-scoped payroll integration governance contract
  for payroll integration runs, source references, reconciliation control
  metadata, exception/retry/replay governance metadata, and audit/export evidence
  references.
- **First slice target:** Provider-neutral backend/API contract slice in
  `Diten.Platform`.
- **Source DCP:** DCP-005 Payroll Integration & Governance Readiness.
- **Blueprint placement:** Platform & Shared Services / Integration &
  Interoperability / Integration Assurance.
- **Golden Reference decision:** `golden_reference: none`; first slice is
  backend/API only and has no CRUD/DataTable UI.
- **Implementation posture:** MOD-0281 is an integration governance facade. It is
  not payroll calculation, payroll SoR, time-and-attendance SoR, HRIS SoR,
  workflow engine, audit store, records engine, gateway, event bus, or provider
  adapter.
- **Governance reconciliation:** MOD-0030, MOD-0003, and MOD-0037 are not runtime
  blockers for the first slice because their first-slice responsibilities are
  limited to local metadata/reference fields. Full integration is deferred to
  follow-up packs.

## 2. Ownership and Boundaries

### In scope for the first approved backend/API slice

- Payroll integration run governance metadata.
- Payroll source and time-attendance source reference governance.
- Mapping and reconciliation control metadata.
- Exception, retry, replay, and idempotency governance metadata.
- Audit/export evidence metadata references.
- Tenant-scoped API behavior.
- Authorization, validation, persistence, and tests.
- References to approved upstream records from MOD-0251, MOD-0279, MOD-0280,
  MOD-0288, MOD-0021, and MOD-0030 where available; if same-tenant validation is
  unavailable, handlers must fail closed or store only an explicit deferred
  reference state, never an approved/governed/mapped state.

### Out of scope

- Payroll calculation, earnings/deductions computation, tax calculation, gross or
  net pay computation, payslip generation, or payment execution.
- Payroll SoR data ownership.
- Copying or duplicating MOD-0279 payroll source records.
- Copying or duplicating MOD-0280 time-attendance provider records.
- Provider-specific ADP, Workday Payroll, SAP Payroll, UKG, Kronos, HRIS, webhook,
  background sync, client, SDK wrapper, or adapter implementation.
- Raw payroll, time, attendance, or HRIS payload persistence.
- Credential, token, secret, bank, tax, payslip, biometric, or geolocation data
  persistence.
- Direct edits to `gateway/Diten.ApiGateway/**/ocelot.json`.
- UI, Razor, JavaScript, DataTable, menu, or RESX files unless a later explicit
  UI slice is approved.

### Boundary rules

- **MOD-0251** owns HRIS external SoR readiness and HRIS source semantics.
- **MOD-0279** owns external payroll SoR profiles, contract metadata, cycle
  references, result references, and payroll source health.
- **MOD-0280** owns external time-and-attendance provider profiles, contract
  metadata, employee reference maps, event references, summary references,
  checkpoints, and source health.
- **MOD-0288** owns Organization, Person & Position Directory references.
- **MOD-0021** owns audit trail persistence.
- **MOD-0030** or an approved records capability owns records retention and legal
  hold behavior. First slice stores evidence export metadata references only;
  real records/legal-hold integration is deferred.
- **MOD-0032** owns API Gateway routing and must be handled by integration-agent.
- **MOD-0035** owns event bus mechanics and event transport behavior.

## 3. Owned Objects

### Domain entities

- `PayrollIntegrationRun`
- `PayrollIntegrationSourceLink`
- `PayrollIntegrationMappingControl`
- `PayrollReconciliationControl`
- `PayrollIntegrationException`
- `PayrollIntegrationRetryReplayRequest`
- `PayrollIntegrationEvidenceExportReference`
- `PayrollIntegrationHealthSnapshot`

### Repository

- `IPayrollIntegrationGovernanceRepository`

### Commands

- `CreatePayrollIntegrationRunCommand`
- `UpdatePayrollIntegrationRunStatusCommand`
- `RecordPayrollIntegrationSourceLinkCommand`
- `UpdatePayrollIntegrationMappingControlCommand`
- `RecordPayrollReconciliationControlCommand`
- `CreatePayrollIntegrationExceptionCommand`
- `ResolvePayrollIntegrationExceptionCommand`
- `RequestPayrollIntegrationReplayCommand`
- `RecordPayrollIntegrationEvidenceExportReferenceCommand`
- `RecordPayrollIntegrationHealthSnapshotCommand`

### Queries

- `GetPayrollIntegrationRunListQuery`
- `GetPayrollIntegrationRunByIdQuery`
- `GetPayrollIntegrationSourceLinksQuery`
- `GetPayrollIntegrationMappingControlsQuery`
- `GetPayrollReconciliationControlsQuery`
- `GetPayrollIntegrationExceptionsQuery`
- `GetPayrollIntegrationEvidenceExportsQuery`
- `GetPayrollIntegrationHealthQuery`

### API endpoints

- `GET /api/payroll-integration-governance/runs`
- `GET /api/payroll-integration-governance/runs/{id}`
- `POST /api/payroll-integration-governance/runs`
- `PATCH /api/payroll-integration-governance/runs/{id}/status`
- `POST /api/payroll-integration-governance/runs/{id}/source-links`
- `GET /api/payroll-integration-governance/runs/{id}/source-links`
- `PUT /api/payroll-integration-governance/runs/{id}/mapping-controls/{controlId}`
- `GET /api/payroll-integration-governance/runs/{id}/mapping-controls`
- `POST /api/payroll-integration-governance/runs/{id}/reconciliation-controls`
- `GET /api/payroll-integration-governance/runs/{id}/reconciliation-controls`
- `POST /api/payroll-integration-governance/runs/{id}/exceptions`
- `PATCH /api/payroll-integration-governance/runs/{id}/exceptions/{exceptionId}/resolve`
- `POST /api/payroll-integration-governance/runs/{id}/replay-requests`
- `POST /api/payroll-integration-governance/runs/{id}/evidence-export-references`
- `GET /api/payroll-integration-governance/runs/{id}/evidence-export-references`
- `POST /api/payroll-integration-governance/runs/{id}/health-snapshots`
- `GET /api/payroll-integration-governance/runs/{id}/health`

### Permission namespace

- `platform.payroll-integration-governance.read`
- `platform.payroll-integration-governance.create-run`
- `platform.payroll-integration-governance.update-run-status`
- `platform.payroll-integration-governance.link-source`
- `platform.payroll-integration-governance.manage-mapping`
- `platform.payroll-integration-governance.record-reconciliation`
- `platform.payroll-integration-governance.create-exception`
- `platform.payroll-integration-governance.resolve-exception`
- `platform.payroll-integration-governance.request-replay`
- `platform.payroll-integration-governance.record-evidence-export`
- `platform.payroll-integration-governance.record-health`
- `platform.payroll-integration-governance.read-health`

### Not owned

- `PayrollExternalSystemProfile`, `PayrollContractProfile`,
  `PayrollEmployeeReferenceMap`, `PayrollCycleReference`,
  `PayrollResultReference`, and `PayrollSourceHealthSnapshot` from MOD-0279.
- `TimeAttendanceExternalProviderProfile`, `TimeAttendanceContractProfile`,
  `TimeAttendanceEmployeeReferenceMap`, `TimeAttendanceEventReference`,
  `AttendanceSummaryReference`, `TimeAttendanceSyncCheckpoint`, and
  `TimeAttendanceProviderHealthSnapshot` from MOD-0280.
- HRIS source records, person records, organization units, positions, audit event
  entities, records/legal-hold entities, gateway route files, event bus
  infrastructure, and provider adapters.

## 4. Entity Fields

All first-slice records are tenant-owned and must use `BaseEntity` behavior in
`Diten.Platform`, including server-resolved `TenantId`, `IsDeleted`, `DeletedAt`,
`CreatedAt`, and `UpdatedAt`. Client payloads must not accept `TenantId`.

### `PayrollIntegrationRun`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| RunCode | string | Yes | Tenant-unique where active; trim, uppercase, max 64. |
| PayrollSourceProfileId | Guid | Yes | Same-tenant MOD-0279 reference; fail closed if validator unavailable. |
| TimeAttendanceProviderProfileId | Guid? | No | Same-tenant MOD-0280 reference; fail closed if provided and validator unavailable. |
| HrisSourceProfileId | Guid? | No | Same-tenant MOD-0251 reference; fail closed if provided and validator unavailable. |
| ContractVersion | string | Yes | Provider-neutral contract version, max 64. |
| RunType | enum/string | Yes | `Manual`, `Scheduled`, `Replay`, `Correction`, `DryRun`. |
| Status | enum/string | Yes | `Draft`, `Queued`, `Running`, `Completed`, `CompletedWithExceptions`, `Failed`, `Cancelled`, `Blocked`. |
| RequestedByActorId | Guid? | No | Reference only; no actor profile snapshot. |
| CorrelationId | string | Yes | Max 128; used for audit and idempotency. |
| IdempotencyKey | string | Yes | Tenant/run deterministic key; unique active index. |
| StartedAt | DateTimeOffset? | No | Run timestamp metadata only. |
| CompletedAt | DateTimeOffset? | No | Must be after StartedAt when both exist. |
| Summary | string? | No | Redacted operational text only, max 1000. |

### `PayrollIntegrationSourceLink`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| RunId | Guid | Yes | Same-tenant run reference. |
| SourceType | enum/string | Yes | `PayrollSource`, `TimeAttendanceProvider`, `HrisSource`, `PersonDirectory`, `OrganizationDirectory`, `PositionDirectory`. |
| SourceReferenceId | Guid | Yes | Same-tenant external/canonical source reference. |
| SourceContractVersion | string | Yes | Max 64. |
| LinkState | enum/string | Yes | `PendingValidation`, `Validated`, `Rejected`, `Superseded`. |
| ValidationMessage | string? | No | Redacted, max 500. |

### `PayrollIntegrationMappingControl`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| RunId | Guid | Yes | Same-tenant run reference. |
| MappingScope | enum/string | Yes | `Employee`, `Org`, `Job`, `PayrollResult`, `AttendanceSummary`. |
| SourceReferenceId | Guid | Yes | Reference to approved MOD-0279/MOD-0280/MOD-0251/MOD-0288 record. |
| TargetReferenceId | Guid? | No | Canonical target reference when available. |
| ControlState | enum/string | Yes | `Pending`, `Validated`, `Mismatch`, `Waived`, `Blocked`. |
| MismatchCode | string? | No | Max 64; no raw payload. |
| ResolutionNote | string? | No | Redacted, max 1000. |

### `PayrollReconciliationControl`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| RunId | Guid | Yes | Same-tenant run reference. |
| ReconciliationType | enum/string | Yes | `Count`, `ReferenceCoverage`, `ContractCompatibility`, `ExceptionClosure`, `EvidenceExport`. |
| ExpectedCount | int? | No | Metadata only; not payroll calculation. |
| ObservedCount | int? | No | Metadata only; no payroll amount. |
| ControlState | enum/string | Yes | `Pending`, `Matched`, `Mismatch`, `Waived`, `Blocked`. |
| EvidenceReferenceId | Guid? | No | MOD-0031/MOD-0030 evidence/record reference if approved. |
| Notes | string? | No | Redacted, max 1000. |

### `PayrollIntegrationException`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| RunId | Guid | Yes | Same-tenant run reference. |
| ExceptionCode | string | Yes | Max 64. |
| Severity | enum/string | Yes | `Info`, `Warning`, `Error`, `Critical`. |
| ExceptionState | enum/string | Yes | `Open`, `Assigned`, `Resolved`, `Waived`, `Rejected`, `Reopened`. |
| SourceReferenceId | Guid? | No | Reference only. |
| AssignedToActorId | Guid? | No | Actor reference only; no profile snapshot. |
| ResolutionWorkflowId | Guid? | No | MOD-0023 reference only; fail closed if workflow validation is required but unavailable. |
| RedactedMessage | string | Yes | Max 1000; no raw payload or sensitive payroll data. |
| ResolutionNote | string? | No | Max 1000; no raw payload or sensitive payroll data. |

### `PayrollIntegrationRetryReplayRequest`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| RunId | Guid | Yes | Same-tenant run reference. |
| RequestType | enum/string | Yes | `Retry`, `Replay`, `CorrectionReplay`. |
| RequestedByActorId | Guid? | No | Reference only. |
| PurposeCode | string | Yes | Required for audit and compliance, max 64. |
| IdempotencyKey | string | Yes | Unique active key per tenant/run/request. |
| ApprovalWorkflowId | Guid? | No | MOD-0023 reference if required. |
| RequestState | enum/string | Yes | `Requested`, `Approved`, `Rejected`, `Executed`, `Cancelled`, `Blocked`. |
| RedactedReason | string | Yes | Max 1000; no raw payload. |

### `PayrollIntegrationEvidenceExportReference`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| RunId | Guid | Yes | Same-tenant run reference. |
| ExportPurposeCode | string | Yes | Required, max 64. |
| AuditEventId | Guid? | No | MOD-0021 reference. |
| EvidenceReferenceId | Guid? | No | MOD-0031 evidence-link reference if approved. |
| RecordsRetentionReferenceId | Guid? | No | Optional MOD-0030 reference metadata only; real records/legal-hold enforcement is future work. |
| ExportState | enum/string | Yes | `Requested`, `Approved`, `Exported`, `Failed`, `Blocked`. |
| RequestedByActorId | Guid? | No | Reference only. |
| CorrelationId | string | Yes | Max 128. |
| RedactedNotes | string? | No | Max 1000. |

### `PayrollIntegrationHealthSnapshot`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| RunId | Guid? | No | Optional same-tenant run reference. |
| HealthState | enum/string | Yes | `Ready`, `Degraded`, `Blocked`, `Failed`, `Unknown`. |
| CheckedAt | DateTimeOffset | Yes | Observation timestamp. |
| RedactedMessage | string? | No | Max 1000; no raw payload. |
| CorrelationId | string? | No | Max 128. |

### MongoDB indexes required

- `PayrollIntegrationRun`: unique active `{ TenantId, RunCode }`, unique active
  `{ TenantId, IdempotencyKey }`, list index `{ TenantId, Status, CreatedAt }`.
- `PayrollIntegrationSourceLink`: unique active
  `{ TenantId, RunId, SourceType, SourceReferenceId }`, list index
  `{ TenantId, RunId, LinkState }`.
- `PayrollIntegrationMappingControl`: list indexes
  `{ TenantId, RunId, MappingScope, ControlState }` and
  `{ TenantId, SourceReferenceId }`.
- `PayrollReconciliationControl`: list index
  `{ TenantId, RunId, ReconciliationType, ControlState }`.
- `PayrollIntegrationException`: list indexes
  `{ TenantId, RunId, ExceptionState, Severity }` and
  `{ TenantId, AssignedToActorId, ExceptionState }`.
- `PayrollIntegrationRetryReplayRequest`: unique active
  `{ TenantId, RunId, IdempotencyKey }`.
- `PayrollIntegrationEvidenceExportReference`: list index
  `{ TenantId, RunId, ExportState, CreatedAt }`.
- `PayrollIntegrationHealthSnapshot`: list indexes
  `{ TenantId, RunId, CheckedAt }` and `{ TenantId, HealthState, CheckedAt }`.

## 5. Repo Scope

The first provider-neutral backend/API slice may touch only:

- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/PayrollIntegrationGovernanceController.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/PayrollIntegrationGovernance/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/PayrollIntegrationGovernance/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/PayrollIntegration*.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IPayrollIntegrationGovernanceRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/PayrollIntegrationGovernance*.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/PayrollIntegrationGovernance/**`

Governance pack authoring scope:

- `execution/domains/platform-shared-services/module-packs/MOD-0281-payroll-integration-governance.md`

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `frontend/Diten.Web/**`
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**`
- `execution/domains/platform-shared-services/module-packs/MOD-0251-hris-external-sor.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0279-payroll-engine-external-sor.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0280-time-attendance-ukg-kronos-external-provider.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0288-organization-person-position-directory.md`
- provider adapter/client/webhook/background worker paths unless a later
  provider-specific pack approves them.

## 7. Dependencies

| Dependency | Role | First-slice decision |
|---|---|---|
| MOD-0251 HRIS External SoR | HRIS source/profile reference boundary | Validate same-tenant when repository/contract exists; otherwise fail closed or keep explicit deferred reference state. |
| MOD-0279 Payroll Engine External SoR | Payroll source, cycle, result reference boundary | Validate same-tenant against existing repository/contract; never copy MOD-0279 records. |
| MOD-0280 Time & Attendance External Provider | Time/attendance provider and event/summary reference boundary | Validate same-tenant against existing repository/contract; never copy MOD-0280 records. |
| MOD-0288 Organization, Person & Position Directory | Person, org, position canonical references | Same-tenant validation required where available; unavailable validator fails closed. |
| MOD-0021 Audit Trail | Audit event and export evidence references | Store audit reference metadata and correlation fields; audit store ownership remains MOD-0021. |
| MOD-0030 Records / Retention / Legal Hold | Records retention and legal-hold metadata | Not a runtime blocker for first slice; store metadata reference only. Real integration is future pack. |
| MOD-0031 Evidence Linking | Evidence reference link target | Optional metadata reference only unless evidence-link contract is available. |
| MOD-0032 API Gateway | Route exposure | Integration-agent only. |
| MOD-0035 Event Bus | Future event publication/consumption | Future/owned outside this first slice. |
| MOD-0003 Data Contract Registry | Contract version governance | Not a runtime blocker for first slice; local contract-version metadata and stale/immutable validation are sufficient. Broad publication is future pack. |
| MOD-0037 Integration Monitoring & Reconciliation | Monitoring/reconciliation handoff | Not a runtime blocker for first slice; local reconciliation/checkpoint/exception metadata is sufficient. Handoff is future pack. |
| MOD-0023 Workflow Designer / approvals | Exception/replay approval references | Workflow-backed approval is optional metadata only; if required validator is unavailable, fail closed. |

## 8. Runtime Constraints

- Runtime service is `Diten.Platform` for the first provider-neutral backend/API
  slice.
- Records are tenant-scoped and must use server-side `TenantId` resolution.
- Client DTOs must not accept `TenantId`.
- Cross-tenant get/update/archive/link/resolve/replay/export/health operations
  must return fail-closed `404`.
- Duplicate active `RunCode` and duplicate active idempotency key must return
  `409`.
- Soft delete must set both `IsDeleted` and `DeletedAt`.
- API responses must use `Response<T>` envelope and `CustomBaseController`.
- Controllers must be thin and route all behavior through MediatR.
- Raw payroll/time/attendance/HRIS payloads, credentials, tokens, secrets,
  bank/tax/payslip data, biometric data, geolocation data, and provider-specific
  adapter fields must be rejected by validators.
- No event bus consumer/publisher or background worker is part of the first
  slice unless later explicitly approved.
- Direct Gateway changes are forbidden; route need is an integration-agent task.
- MOD-0281 registry-row cleanup is not an implementation blocker because
  DCP-002 verifier passes; keep cleanup as follow-up.

## 9. Layout & Shell Contract

- `shell: none`
- `golden_reference: none`
- `form_field_count: 0`
- No Razor layout applies.
- No `frontend/Diten.Web/**` file is in scope.
- No DataTable verifier or localization verifier applies to this ready-for-dev
  backend/API slice.
- Any future UI must be prepared as a separate approved slice with its own shell,
  Golden Reference, form-field count, frontend file list, RESX scope, and
  browser verification.

## 10. Backend File Convention

Implementation must follow the repository backend convention:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/PayrollIntegrationGovernance/
├── Commands/
├── Queries/
├── Handlers/
│   ├── CommandHandlers/
│   └── QueryHandlers/
├── Validators/
├── PayrollIntegrationGovernanceModels.cs
├── PayrollIntegrationGovernanceCodeNormalizer.cs
└── PayrollIntegrationGovernanceSensitiveValueGuard.cs
```

Handler and validator names must not use `CommandHandler`, `QueryHandler`, or
`RequestHandler` suffixes. Request/DTO models should be grouped in the single
models file unless the implementation becomes too large and a local pattern is
approved.

## 11. Frontend File Contract

Frontend is explicitly out of scope:

- No `Views/**`.
- No `wwwroot/**`.
- No menu/navigation changes.
- No JavaScript, DataTable, or RESX.
- UI/DataTable/l10n verification is N/A for this pack while `shell: none`.

If a later UI slice is approved, it must define:

- `shell: platform-admin` or `tenant`;
- `golden_reference: slim` or `compact`;
- exact view/controller/resource/script paths;
- form-field count;
- DataTable v2 verifier and localization expectations.

## 12. Validation Rules

- `RunCode`: required, trim, uppercase, max 64, tenant-unique active.
- `ContractVersion`: required, max 64, provider-neutral, must be compatible with
  approved MOD-0279/MOD-0280/MOD-0251 contracts or fail closed.
- `CorrelationId`: required for run and evidence export operations, max 128.
- `IdempotencyKey`: required for run and retry/replay operations, max 256,
  tenant/run unique active.
- `SourceReferenceId` fields: must be same-tenant validated against the owning
  module boundary; if validator is unavailable, fail closed.
- `MappingScope`: only provider-neutral governance scopes are allowed:
  `Employee`, `Org`, `Job`, `PayrollResult`, `AttendanceSummary`.
- `RunType`, `Status`, `Severity`, `ExceptionState`, `ControlState`,
  `RequestState`, and `HealthState`: enum validation required.
- `StartedAt`/`CompletedAt`: `CompletedAt` must be after `StartedAt`.
- Count fields: non-negative; no payroll amount fields are allowed.
- Text fields: bounded length and redacted-only.
- Validators must reject markers that look like raw secrets, tokens,
  credentials, raw JSON/XML provider payloads, bank/tax/payslip data, payroll
  amounts, biometric data, geolocation data, provider-specific adapter fields,
  and internal engine ownership fields.
- Workflow-backed resolve/replay actions must fail closed if MOD-0023 reference
  validation is unavailable and the request attempts an approved workflow-backed
  state.
- Evidence export must require purpose metadata and may store optional MOD-0030
  reference metadata only; real retention/legal-hold enforcement is future work.

## 13. Failure Path to Verify

Tests must prove:

- Cross-tenant get/update/archive/source-link/mapping/reconciliation/exception
  resolve/replay/export/health returns `404`.
- Duplicate active `RunCode` returns `409`.
- Duplicate active idempotency key returns `409`.
- Missing payroll source, time-attendance provider, HRIS source, person,
  organization unit, or position reference fails closed.
- Unavailable reference validators fail closed or allow only an explicit deferred
  reference state; they must never mark blind references as governed, approved, or
  mapped.
- Unsupported contract version fails closed.
- Unauthorized or missing permission requests are denied.
- Raw payload/secret/token/credential/bank/tax/payslip/biometric/geolocation
  markers are rejected.
- Payroll calculation fields or provider-specific adapter fields are rejected.
- Soft delete sets `IsDeleted` and `DeletedAt`.
- Retry/replay request state is idempotent and does not create duplicate active
  requests.
- Evidence export requires purpose and actor/correlation metadata; MOD-0030
  retention/legal-hold integration is metadata-only in this slice.

## 14. Authorization Convention

- Controller must use `[Authorize(Policy = "PlatformActor")]`.
- Permission prefix: `platform.payroll-integration-governance.*`.
- Sensitive actions must have separate permissions:
  - replay/retry request;
  - exception resolve/waive;
  - evidence export reference creation;
  - health recording.
- Read permissions must not imply replay, resolve, or export permissions.
- Authorization tests must inspect controller attributes and permission strings.

## 15. Gateway / API Routing Decision

- API route candidate: `/api/payroll-integration-governance`.
- Direct edit of `gateway/Diten.ApiGateway/**/ocelot.json` is forbidden.
- If HTTP exposure is required after backend approval, create an integration-agent
  task for MOD-0032 to add:
  - `/api/payroll-integration-governance`
  - `/api/payroll-integration-governance/{everything}`
- Until that task is completed, service-local controller build/tests may proceed
  only after pack approval, but gateway exposure is not considered done.

## 16. Acceptance Criteria

First-slice implementation must satisfy:

1. DCP-002 preflight passes for exact canonical identity.
2. Only Section 5 repo scope is touched.
3. `PayrollIntegrationGovernanceController` is thin and uses
   `CustomBaseController`, MediatR, and `Response<T>`.
4. Client request DTOs do not expose `TenantId`.
5. Tenant-scoped repository methods filter by current tenant and active records.
6. Cross-tenant operations fail closed with `404`.
7. Duplicate active run code and idempotency key return `409`.
8. Soft-delete operations set `IsDeleted` and `DeletedAt`.
9. Same-tenant reference validation is implemented for MOD-0279, MOD-0280,
   MOD-0251, and MOD-0288 references where accepted.
10. Missing/unavailable reference validators fail closed or allow only an
    explicit deferred reference state; blind references must never become
    governed, approved, or mapped.
11. Validators reject raw payloads, credentials, tokens, secrets, bank/tax/payslip
    data, biometric/geolocation markers, provider-specific adapter fields, and
    payroll calculation fields.
12. Evidence export reference creation requires purpose metadata, correlation
    metadata, and optional audit/evidence/records reference metadata. Real
    records/retention/legal-hold enforcement remains future work.
13. Authorization prefix is exactly
    `platform.payroll-integration-governance.*`.
14. Mongo indexes are created for active uniqueness and tenant-scoped list paths.
15. No UI, frontend, RESX, gateway, provider adapter, background worker, payroll
    calculation, or source data duplication is introduced.

## 17. Test Expectations

Run:

```text
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0281 --name "Payroll Integration & Governance"
dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug
dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj -c Debug --filter FullyQualifiedName~PayrollIntegrationGovernance
```

Targeted tests must cover:

- tenant isolation;
- duplicate active `RunCode`;
- duplicate active idempotency key;
- authorization / missing permission;
- soft delete `DeletedAt`;
- no raw payroll/time/attendance/HRIS payload;
- no credential/token/secret persistence;
- no bank/tax/payslip/biometric/geolocation persistence;
- no payroll calculation fields;
- provider-specific adapter field rejection;
- MOD-0251/MOD-0279/MOD-0280/MOD-0288 reference fail-closed behavior;
- MOD-0030 evidence export metadata-only behavior;
- retry/replay idempotency;
- unsupported contract version fail-closed.

UI/DataTable/l10n verification is N/A for this backend/API slice.

## 18. Ready-for-dev Checklist

- [x] DCP-002 preflight passes for `MOD-0281 - Payroll Integration & Governance`.
- [x] Source DCP identified: DCP-005.
- [x] Domain candidate identified: `platform-shared-services`.
- [x] Runtime service candidate identified: `Diten.Platform`.
- [x] First-slice UI explicitly excluded: `shell: none`,
  `golden_reference: none`.
- [x] Payroll calculation excluded.
- [x] Payroll/time/attendance/HRIS SoR data duplication excluded.
- [x] Provider-specific adapters/webhooks/background sync excluded.
- [x] Raw payload and secret persistence excluded.
- [x] MOD-0281 registry row gap is not an implementation blocker because DCP-002
  verifier passes; registry cleanup remains follow-up.
- [x] MOD-0279 reference rule accepted: validate same-tenant when repository /
  contract exists; otherwise fail closed or deferred state only.
- [x] MOD-0280 reference rule accepted: validate same-tenant when repository /
  contract exists; otherwise fail closed or deferred state only.
- [x] MOD-0251 reference rule accepted: validate same-tenant when repository /
  contract exists; otherwise fail closed or deferred state only.
- [x] MOD-0288 reference rule accepted: validate same-tenant where available;
  unavailable validator fails closed.
- [x] MOD-0003 waiver recorded: local contract version metadata plus stale /
  immutable validation is sufficient for first slice.
- [x] MOD-0037 waiver recorded: local reconciliation state, checkpoint, exception,
  and health metadata are sufficient for first slice.
- [x] MOD-0030 waiver recorded: evidence export metadata references only; real
  records/legal-hold integration is future pack.
- [x] MOD-0021 audit export reference policy accepted as metadata/correlation
  reference only for first slice.
- [x] Sensitive evidence export permission model accepted.
- [x] Pack promoted to `ready-for-dev`.

Open blockers:

- none

## 19. Implementation Notes

- Keep MOD-0281 as a governance facade over integration control records.
- Store references, statuses, contract versions, idempotency keys, correlation
  IDs, redacted messages, and evidence metadata only.
- Do not store payroll amounts, payslip contents, provider payloads, employee PII
  snapshots, raw HRIS data, raw attendance events, credentials, or secrets.
- Prefer provider-neutral enum names and metadata. Names such as ADP, Workday,
  SAP Payroll, UKG, or Kronos may appear only as external source references owned
  by MOD-0279/MOD-0280 or display metadata, not adapter code.
- If reference validators are unavailable in `Diten.Platform`, handlers must fail
  closed or store only explicit deferred reference state rather than treating
  references as governed, approved, or mapped.
- MOD-0030, MOD-0003, and MOD-0037 are reconciled as first-slice waivers, not
  blockers.
- Gateway route work belongs to integration-agent / MOD-0032.
- Event publication/consumption belongs to a future event-bus slice / MOD-0035
  decision.
- Any UI must be prepared as a separate module-pack update or follow-up slice.

## 20. Follow-up Items

- Registry owner: add direct MOD-0281 registry row as cleanup, if desired.
- Records owner: prepare future MOD-0030 records/retention/legal-hold integration
  pack for evidence export retention enforcement.
- Platform team: prepare future MOD-0003 broad contract publication/governance
  pack.
- Platform team: prepare future MOD-0037 monitoring/reconciliation handoff pack.
- Platform team: prepare future MOD-0021 audit export purpose/policy hardening if
  runtime audit export semantics expand beyond metadata references.
- Backend team: implement only Section 5 Diten.Platform backend/API scope.
- Integration-agent: add gateway routes only after backend route shape is approved.
- Future pack update: define UI slice only if explicitly requested and approved.
