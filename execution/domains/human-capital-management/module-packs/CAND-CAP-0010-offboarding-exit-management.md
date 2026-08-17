---
id: CAND-CAP-0010
name: Offboarding & Exit Management
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner
branch: feature/hcm/cand-cap-0010-offboarding-exit-management
started: 2026-08-11
target: 2026-10-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
candidate_identity: CAND-CAP-0010
legacy_excel_id: MOD-0305
canonicalization_status: candidate-runtime waiver approved / pending canonical MOD
runtime_service_candidate: Diten.HumanCapitalService
runtime_owner_key: hcm.offboarding
bootstrap_type: backend-api-first-slice
---

# CAND-CAP-0010 - Offboarding & Exit Management

> Status: done. The first HCM backend/API offboarding case slice has been
> implemented, reviewed, and promoted to done. Review-state handoff guard drift is
> fixed: `TepHandoffReady` review transitions require existing local handoff
> metadata. `CAND-CAP-0010` remains a governance/documentation identity and must
> never be written into runtime literals. No frontend, Gateway direct edit, PSS
> ownership transfer, TEP runtime/service/module implementation, employee master
> SoR ownership, payroll/time/attendance ownership, broad workflow engine, or
> raw/sensitive payload persistence is authorized.

## 1. Module Summary

`CAND-CAP-0010` reserves the temporary DCP-002 candidate identity for
`Offboarding & Exit Management` in the `human-capital-management` domain.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
where the Excel/DCP row arrived as `MOD-0305`. `MOD-0305` must not be used for
HR/HCM because it is absent from the Blueprint and has no canonical registry row.

This done pack records the implemented narrow provider-neutral HCM backend/API
contract for governed offboarding cases. The slice owns local offboarding case
metadata, minimal exit state governance, checklist/dependency deferred metadata,
and local/deferred TEP handoff payload metadata. It consumes existing HCM R1
foundation contracts for employee projection, sensitive access, and position/org
assignment overlay.

## 2. Ownership and Boundaries

Owned by this first runtime slice:

- Tenant-scoped HCM offboarding case backend/API contract.
- Minimal offboarding state model and guarded state transition rules.
- Local checklist/dependency metadata without workflow engine ownership.
- Local bounded audit/evidence/retention deferred metadata.
- Local/deferred TEP handoff payload metadata, without calling or implementing
  TEP runtime.
- Same-tenant employee projection anchor validation.
- Sensitive access precondition for read/manage/review operations.
- Optional assignment overlay context validation when assignment context is
  supplied.

Not owned by this pack:

- Employee master system of record ownership.
- Employment lifecycle beyond the offboarding case boundary.
- Payroll, time, attendance, compensation, benefits, bank, tax, or payslip
  ownership.
- TEP runtime service, TEP module pack, candidate/talent identity, API,
  persistence, webhook, background job, or workflow implementation.
- PSS workflow, audit, document, evidence, records retention, legal hold, or
  RBAC/ABAC substrate ownership.
- Frontend menus, Razor views, JavaScript, DataTables, RESX, Gateway direct
  routes, provider adapters, raw HRIS/provider payloads, credentials, tokens,
  secrets, biometric/geolocation data, national ID, DOB, home address, or
  PII-heavy profile ownership.

## 3. Owned Objects

Authorized first-slice runtime objects:

| Object | Type | Purpose |
|---|---|---|
| OffboardingCase | Runtime entity | Tenant-scoped governed offboarding case anchored to an employee projection. |
| OffboardingDependencyState | Runtime metadata | Captures workflow/audit/evidence/retention availability as local bounded/deferred state. |
| OffboardingChecklistSnapshot | Runtime metadata | Captures checklist/task planning state without implementing workflow engine ownership. |
| OffboardingTepHandoffMetadata | Runtime metadata | Captures local/deferred TEP handoff payload metadata without TEP runtime calls. |
| IOffboardingCaseRepository | Repository contract | Mongo-backed tenant-aware persistence for offboarding cases. |
| OffboardingCasesController | API controller | Thin HCM API controller that sends CQRS requests through MediatR and `Response<T>` envelope. |

No TEP entity, PSS entity, employee master SoR entity, workflow engine, Gateway
route, frontend object, payroll/time/attendance object, or raw/sensitive payload
object is authorized.

## 4. Entity Fields

Approved minimal runtime fields:

| Field | Required | Notes |
|---|---|---|
| `Code` | Yes | Tenant-scoped offboarding case code; duplicate active code returns 409. |
| `EmployeeProjectionId` | Yes | Anchor to CAND-CAP-0007 employee projection; same-tenant validation required. |
| `AssignmentOverlayId` | Optional | CAND-CAP-0009 assignment overlay context; validate when supplied. |
| `ExitReasonCode` | Yes | Reference code only; no free-form sensitive narrative. |
| `ExitTypeCode` | Yes | Approved taxonomy code such as resignation, termination, retirement, transfer, or other configured value. |
| `NoticeDate` | Optional | Minimal process date metadata only. |
| `PlannedExitDate` | Yes | Target exit date for process governance. |
| `ActualExitDate` | Optional | Completion metadata only; must not precede notice/planned constraints when configured. |
| `OffboardingState` | Yes | `Draft`, `ReviewRequired`, `Approved`, `Active`, `TepHandoffReady`, `Completed`, `Cancelled`, `Archived`. |
| `ChecklistState` | Yes | `NotStarted`, `Planned`, `Deferred`, `InProgress`, `Completed`; metadata only. |
| `SensitiveAccessDecisionState` | Yes | `Deferred`, `Allowed`, or `Denied` from CAND-CAP-0008 precondition. |
| `DependencyDecisionState` | Yes | `Deferred`, `Available`, or `Unavailable`; local bounded metadata for workflow/audit/evidence/retention. |
| `TepHandoffState` | Yes | `NotRequired`, `Planned`, `Ready`, `Deferred`, `Transferred`; local metadata only, no TEP call. |
| `TepHandoffReferenceKey` | Optional | Opaque local/deferred handoff reference key; not a TEP identity or payload copy. |
| `SourceContractVersion` | Yes | Local contract metadata; stale/empty values fail validation. |
| `OffboardingVersion` | Yes | Positive optimistic version metadata for update/review flows. |

All records must use repo-standard base fields such as `Id`, server-side
`TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, and `UpdatedAt`.

Forbidden field classes: raw HRIS/provider payload, credential, token, secret,
payroll data, time/attendance data, bank data, tax data, payslip data, biometric
data, geolocation data, national ID, DOB, home address, PII-heavy profile
attributes, and TEP candidate/talent identity ownership data.

## 5. Repo Scope

Authorized runtime repo scope for the first backend/API slice:

- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Application/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Domain/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Infrastructure/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Persistence/**`
- `services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/**`

Governance reconciliation scope remains this pack file only. Runtime
implementation must stay inside `services/Diten.HumanCapitalService/**`.

## 6. Protected Paths

This done pack must not change:

- `frontend/**`
- `gateway/**`
- `tests/**`, except `services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/talent-ecosystem-platform/**`
- `execution/domains/master-data-management/**`
- `execution/domains/developer-enablement/**`
- `execution/domains/human-capital-management/domain-config.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0006-hr-capability-block-shell.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0007-employee-profile-employment-record-projection.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0008-hr-governance-sensitive-access-controls.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0009-position-organization-assignment.md`
- `execution/registries/module-id-registry.md`, except a separate EA-approved
  identity reservation or canonicalization update.
- `.antigravity/**`

## 7. Dependencies

Governance dependencies:

- `AGENTS.md`
- `execution/domains/human-capital-management/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`

Required HCM runtime dependencies:

- `CAND-CAP-0006` - HR Capability Block Shell, governance boundary.
- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, completed
  first backend/API slice and required employee anchor.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, completed first
  backend/API slice and required sensitive-access precondition.
- `CAND-CAP-0009` - Position & Organization Assignment, completed first
  backend/API assignment overlay dependency when assignment context is supplied.

Deferred first-slice dependencies:

- Workflow/task orchestration substrate: local checklist/deferred metadata only.
- `MOD-0021` Audit Trail Service: local bounded audit/deferred metadata only.
- Records retention/legal hold substrate: local retention/deferred metadata only.
- Documentation/evidence linking substrate: local evidence/deferred metadata only.
- API Gateway: integration-agent follow-up only if external exposure is needed.
- TEP runtime: not implemented; handoff payload remains local/deferred metadata.

## 8. Runtime Constraints

- Runtime owner/key is `hcm.offboarding`.
- `CAND-CAP-0010` is a governance/documentation identity only and must never be
  written into runtime literals, permission seeds, route metadata, database
  records, job names, telemetry owner fields, config keys, or test fixtures.
- `MOD-0305` must not be used for this HCM capability.
- Runtime implementation is limited to the HCM offboarding case backend/API
  contract under `services/Diten.HumanCapitalService/**`.
- TenantId must be resolved server-side; request DTOs must not accept TenantId.
- Mongo-backed tenant-aware persistence is required.
- Duplicate active `Code` must return 409 within tenant scope.
- Cross-tenant get/update/archive/review/handoff operations must fail closed 404.
- Soft delete must set both `IsDeleted` and `DeletedAt`.
- CAND-CAP-0007 employee projection is the required runtime anchor; missing,
  deleted, or cross-tenant anchors return 404.
- CAND-CAP-0008 sensitive access enforcement is required before offboarding
  read/manage/review decisions. Unavailable data-scope returns explicit
  `Deferred` state or fail-closed response.
- CAND-CAP-0009 assignment overlay is optional/conditional context; if supplied,
  it must be same-tenant validated. Missing/deleted/cross-tenant assignment
  overlay returns 404, and unavailable optional validation may keep the case in
  explicit `Deferred` state.
- Workflow/checklist, audit, evidence, and retention/legal-hold integration are
  not blockers for the first slice; only local bounded/deferred metadata is
  authorized.
- TEP handoff payload is local/deferred metadata only. No TEP service call,
  entity, persistence model, webhook, background sync, or runtime implementation
  is authorized.
- No frontend menu, Razor view, JavaScript, DataTable, RESX, or Gateway route is
  authorized.
- No employee master SoR ownership, payroll/time/attendance ownership, or PSS
  substrate ownership transfer is authorized.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

This pack does not create an HCM page, workspace menu, Razor view, JavaScript
file, DataTable, RESX resource, layout binding, or frontend route.

Future UI exposure requires a separate UI-specific pack and must not be inferred
from this backend/API first-slice pack.

## 10. Backend File Convention

Backend implementation must follow the repo's 5-layer .NET service convention,
CQRS/MediatR patterns, `Response<T>` envelope, `CustomBaseController`, tenant
server-side resolution, Mongo-backed repository standard, soft delete rules,
same-tenant validation, and permission conventions defined by this pack.

Expected feature naming:

- Feature folder: `OffboardingCases`
- Controller: `OffboardingCasesController`
- Repository contract: `IOffboardingCaseRepository`
- Mongo repository: `MongoOffboardingCaseRepository`
- Runtime guard/owner key: `hcm.offboarding`

## 11. Frontend File Contract

Frontend implementation is N/A for this done backend/API pack.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, or frontend route may be added from this pack.

## 12. Validation Rules

Runtime validation rules:

- DCP-002 candidate gate for `CAND-CAP-0010` and
  `Offboarding & Exit Management` must pass before implementation.
- Runtime implementation must use the non-candidate owner/key `hcm.offboarding`.
- Runtime paths must not contain `CAND-CAP-0010` or `MOD-0305` literals.
- DTO payloads must not accept `TenantId`.
- `Code`, `EmployeeProjectionId`, `ExitReasonCode`, `ExitTypeCode`,
  `PlannedExitDate`, `OffboardingState`, `ChecklistState`,
  `SensitiveAccessDecisionState`, `DependencyDecisionState`,
  `TepHandoffState`, `SourceContractVersion`, and `OffboardingVersion` are
  required.
- `OffboardingVersion` must be greater than zero.
- `ActualExitDate`, when present, must not violate configured notice/planned exit
  constraints.
- `Archived` state is controlled by archive operation.
- `Completed` requires allowed sensitive access and available or explicitly
  deferred dependency metadata.
- `TepHandoffReady` requires local handoff metadata and must not call TEP
  runtime.
- Employee projection anchor validation must be same-tenant and fail closed 404.
- Assignment overlay validation is optional only when no assignment context is
  supplied; supplied assignment context must validate same-tenant or fail closed
  404/deferred per state rules.
- Validators must reject raw/sensitive markers and forbidden field names.

## 13. Failure Path to Verify

Required runtime failure paths:

- Missing/deleted/cross-tenant employee projection returns fail-closed 404.
- Missing/deleted/cross-tenant assignment overlay reference returns fail-closed
  404 when supplied.
- Sensitive/restricted offboarding cases do not leak through broad read/list
  paths.
- Data-scope unavailable produces explicit `Deferred` metadata or fail-closed
  response.
- Duplicate active `Code` inside the same tenant returns 409.
- Attempts to set final states without required validated/deferred dependency
  decisions fail closed.
- Attempts to persist raw payloads, credentials, secrets, payroll/time data,
  TEP identity data, or PII-heavy fields fail validation.
- Soft delete sets both `IsDeleted` and `DeletedAt`.
- Workflow/audit/evidence/retention unavailable states remain local bounded
  metadata and do not imply completed governance.
- TEP handoff remains local metadata; no TEP runtime call or persistence model is
  created.

## 14. Authorization Convention

Runtime owner/key:

- `hcm.offboarding`

Approved permission namespace:

- `hcm.offboarding.read`
- `hcm.offboarding.manage`
- `hcm.offboarding.review`
- `hcm.offboarding.archive`
- `hcm.offboarding.handoff.manage`

Permission checks must use these non-candidate strings. `CAND-CAP-0010` and
`MOD-0305` must not be used in permission seeds, route attributes, telemetry
owner fields, config keys, database records, or test fixtures.

## 15. Gateway / API Routing Decision

Gateway direct edit is not authorized.

The HCM service may expose local controller endpoints inside
`services/Diten.HumanCapitalService/**`. If Gateway exposure is later required,
it must be recorded as an integration-agent follow-up. Direct edits to
`gateway/Diten.ApiGateway/**/ocelot.json` remain out of scope.

TEP handoff payload metadata does not authorize a TEP API route, TEP service
call, TEP persistence model, webhook, background job, or Gateway route.

## 16. Acceptance Criteria

The first backend/API slice is done when:

1. AC-01: DCP-002 candidate gate passes for `CAND-CAP-0010` and
   `Offboarding & Exit Management`.
2. AC-02: Pack status is `done` after read-only implementation review PASS,
   first HCM backend/API offboarding case slice implementation, and review-state
   handoff guard drift fix.
3. AC-03: Runtime files do not contain `CAND-CAP-0010` or `MOD-0305` literals.
4. AC-04: Runtime owner/key is exactly `hcm.offboarding`.
5. AC-05: Permission checks cover `hcm.offboarding.read`, `manage`, `review`,
   `archive`, and `handoff.manage`.
6. AC-06: Controller is thin and delegates to MediatR/CQRS handlers with `Response<T>`
   envelope and `CustomBaseController`.
7. AC-07: TenantId is server-side resolved and never accepted from request DTOs.
8. AC-08: Mongo-backed repository filters by `TenantId` and `IsDeleted == false`.
9. AC-09: Tenant-aware active `Code` uniqueness returns 409 on duplicates.
10. AC-10: Get/update/archive/review/handoff operations fail closed 404 for cross-tenant
   or deleted records.
11. AC-11: Soft delete sets `IsDeleted` and `DeletedAt`.
12. AC-12: Employee projection anchor validation uses CAND-CAP-0007 runtime contract and
    returns 404 for missing/deleted/cross-tenant anchors.
13. AC-13: Sensitive access precondition uses CAND-CAP-0008 runtime contract and
    protects read/manage/review behavior.
14. AC-14: Assignment overlay context uses CAND-CAP-0009 runtime contract when supplied;
    unavailable optional validation results in explicit deferred metadata or
    fail-closed behavior.
15. AC-15: Workflow, audit, evidence, and retention/legal-hold dependencies are local
    bounded/deferred metadata only.
16. AC-16: TEP handoff payload is local/deferred metadata only, with no TEP runtime,
    service, module, webhook, background job, or persistence implementation.
17. AC-17: Review path cannot move a case to `TepHandoffReady` unless existing
    local handoff metadata state is `Planned`, `Ready`, or `Deferred`; otherwise
    it returns 400 fail-closed.
18. AC-18: Validators reject forbidden raw/sensitive/PII-heavy field markers.
19. AC-19: UI/DataTable/l10n verifier is N/A.
20. AC-20: Gateway route, if needed, is reported only as integration-agent follow-up.

## 17. Test Expectations

Required validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0010 --name "Offboarding & Exit Management"`
- `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore`
- HCM targeted offboarding tests.
- `rg -n "CAND-CAP-0010|MOD-0305" services/Diten.HumanCapitalService frontend gateway tests`

Required targeted test coverage:

- Tenant isolation for list/get/update/archive/review/handoff operations.
- Duplicate active `Code` tenant scope conflict.
- Soft delete sets `IsDeleted` and `DeletedAt`.
- Employee projection anchor validation fail-closed 404.
- Sensitive access precondition for read/manage/review.
- Assignment overlay optional/deferred/fail-closed behavior.
- Workflow/audit/evidence/retention deferred metadata behavior.
- TEP handoff local metadata only; no TEP runtime call or persistence model.
- Forbidden raw/sensitive field validation.
- Permission namespace checks for all approved `hcm.offboarding.*` permissions.
- Runtime literal scan for `CAND-CAP-0010|MOD-0305`.

UI/DataTable/l10n verifier: N/A because `shell: none` and
`golden_reference: none`.

## 18. Ready-for-dev / Done Checklist

- [x] EA candidate-runtime policy waiver approved.
- [x] `CAND-CAP-0010` remains governance/documentation identity only.
- [x] `MOD-0305` remains blocked and must not be used.
- [x] Runtime owner/key approved: `hcm.offboarding`.
- [x] Runtime repo scope approved under `services/Diten.HumanCapitalService/**`.
- [x] Runtime permission namespace approved:
  `hcm.offboarding.read`, `hcm.offboarding.manage`,
  `hcm.offboarding.review`, `hcm.offboarding.archive`, and
  `hcm.offboarding.handoff.manage`.
- [x] First backend/API slice limited to HCM offboarding case contract.
- [x] Minimal offboarding state model and field list approved for first slice.
- [x] CAND-CAP-0007 employee projection anchor validation required.
- [x] CAND-CAP-0008 sensitive access precondition required.
- [x] CAND-CAP-0009 assignment overlay validation optional/conditional.
- [x] Workflow/checklist substrate deferred waiver approved for local metadata.
- [x] MOD-0021 audit deferred waiver approved for local bounded metadata.
- [x] Evidence/documentation deferred waiver approved for local metadata.
- [x] Records retention/legal hold deferred waiver approved for local metadata.
- [x] TEP handoff payload boundary approved as local/deferred metadata only.
- [x] UI/frontend closed for this slice.
- [x] Gateway direct edit closed; integration-agent follow-up if needed.
- [x] PSS/TEP implementation files remain protected.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0010|MOD-0305`.
- [x] First HCM backend/API offboarding case slice implemented.
- [x] Review-state handoff guard drift fixed.
- [x] Candidate gate PASS.
- [x] HCM API build PASS.
- [x] Offboarding targeted tests PASS: 20/20.
- [x] Full HCM Application tests PASS: 60/60.
- [x] Runtime literal scan PASS: `CAND-CAP-0010|MOD-0305` no matches.
- [x] Frontend/gateway/PSS/TEP scope remained closed.

Open blockers: none for the implemented first HCM backend/API offboarding case
slice.

## 19. Implementation Notes

- DCP-008 lists Offboarding & Exit Management in R1-D with legacy Excel ID
  `MOD-0305` and output role "Governed offboarding + TEP handoff payload".
- DCP-002 blocks `MOD-0305` for HCM use because it is not Blueprint-backed and
  has no canonical registry row.
- EA approved candidate-runtime policy waiver for `CAND-CAP-0010`; runtime must
  use non-candidate owner/key `hcm.offboarding`.
- CAND-CAP-0007, CAND-CAP-0008, and CAND-CAP-0009 are required foundation
  dependencies. This pack must not change their runtime behavior.
- Workflow/audit/evidence/retention are deferred via local bounded metadata for
  the first slice; real integrations are future follow-ups.
- TEP handoff is local/deferred metadata only. TEP runtime/service/module
  implementation remains closed.

Done promotion reconciliation note:

- First HCM backend/API offboarding case slice implemented under
  `services/Diten.HumanCapitalService/**`.
- Read-only implementation review PASS.
- Review-state handoff guard drift fixed: review cannot transition to
  `TepHandoffReady` without existing local `Planned`, `Ready`, or `Deferred`
  handoff metadata.
- Validation evidence retained:
  candidate gate PASS; HCM API build PASS; Offboarding targeted tests PASS
  20/20; full HCM Application tests PASS 60/60; runtime literal scan PASS for
  `CAND-CAP-0010|MOD-0305` no matches.
- Frontend, Gateway, PSS, and TEP scope remained closed.

## 20. Follow-up Items

- Future EA canonical MOD assignment for Offboarding & Exit Management.
- Real workflow/checklist orchestration integration after substrate ownership is
  explicitly approved.
- Real MOD-0021 audit integration after audit contract is approved.
- Real evidence/documentation and records retention/legal hold integration after
  substrate contracts are approved.
- TEP native module identity and implementation decision after HCM handoff
  contract is reviewed.
- Integration-agent Gateway task if external API exposure is needed.
