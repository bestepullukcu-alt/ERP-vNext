---
id: CAND-CAP-0009
name: Position & Organization Assignment
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team
branch: feature/hcm/cand-cap-0009-position-organization-assignment
started: 2026-08-11
target: 2026-09-30
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
candidate_identity: CAND-CAP-0009
legacy_excel_id: MOD-0299
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.HumanCapitalService
runtime_owner_key: hcm.position-assignments
bootstrap_type: runtime-ready backend/API first slice
---

# CAND-CAP-0009 - Position & Organization Assignment

> Status: done. The first HCM backend/API assignment overlay slice has been
> implemented, reviewed, and reconciled. No frontend, gateway direct edit, PSS
> ownership transfer, employee master SoR ownership, organization/position
> directory copy, or broad HR lifecycle implementation is authorized.

## 1. Module Summary

`CAND-CAP-0009` reserves the temporary DCP-002 candidate identity for
`Position & Organization Assignment` in the `human-capital-management` domain.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
where the Excel/DCP row arrived as `MOD-0299`. `MOD-0299` must not be used for
HR/HCM because the registry retains it as a deprecated platform alias to
`CAND-CAP-0005` SaaS Billing & Invoicing.

This pack prepares the governance boundary for assigning HCM employee projection
records to organization units, positions, and manager relationships without
duplicating PSS directory ownership. `MOD-0288`, `MOD-0288-FU01`, and
`MOD-0288-FU02` remain PSS reference substrate only.

## 2. Ownership and Boundaries

Owned by this pack:

- HCM-native employee position/org/manager assignment overlay runtime contract.
- Employee projection to position/org reference assignment boundary.
- Assignment projection/overlay governance for HCM workflows.
- Same-tenant reference validation expectations against PSS directory substrate.
- First HCM runtime sequencing for assignment-aware employee projection flows.

Not owned by this pack:

- `MOD-0288` Organization, Person & Position Directory ownership.
- `MOD-0288-FU01` Position Assignment User Reference Validation ownership.
- `MOD-0288-FU02` Person Reference Directory Projection ownership.
- Employee master system of record.
- Organization/position master data lifecycle, hierarchy ownership, or directory
  CRUD.
- Payroll/time-attendance ownership, compensation, benefits, performance,
  learning, succession, offboarding execution, or TEP candidate/talent identity.
- Frontend menus, Gateway direct routes, provider adapters, raw HRIS payloads,
  credentials, payroll, bank, tax, payslip, biometric, geolocation, national ID,
  DOB, home address, or PII-heavy profile ownership.

## 3. Owned Objects

Authorized first-slice runtime objects:

| Object | Type | Purpose |
|---|---|---|
| EmployeePositionAssignmentOverlay | Runtime entity | Tenant-scoped HCM overlay that links an employee projection to PSS person/org/position references. |
| EmployeeAssignmentReferenceValidation | Runtime metadata | Captures validated/deferred/fail-closed reference state for PSS substrate references. |
| EmployeeAssignmentSensitiveAccessSnapshot | Runtime metadata | Captures bounded CAND-CAP-0008 sensitive access precondition result without copying policy engine ownership. |
| EmployeeAssignmentRepository | Runtime repository contract | Mongo-backed tenant-aware persistence for HCM assignment overlay records. |
| PositionAssignmentsController | Runtime API surface | Thin HCM API controller that sends CQRS requests through MediatR and `Response<T>` envelope. |

No PSS directory entity, PSS position assignment implementation, employee master
SoR entity, permission seed, Gateway route, frontend object, provider adapter, or
raw/sensitive payload object is authorized by this pack.

## 4. Entity Fields

Authorized first-slice runtime fields:

| Field | Required | Notes |
|---|---|---|
| `Code` | Yes | Stable tenant-scoped assignment overlay code; duplicate active code returns 409. |
| `EmployeeProjectionId` | Yes | HCM CAND-CAP-0007 runtime anchor; same-tenant employee projection validation required. |
| `PersonReferenceId` | Yes | PSS MOD-0288-FU02 person reference substrate; same-tenant validate where contract exists. |
| `OrganizationUnitId` | Conditional | PSS MOD-0288 org unit reference; same-tenant validate where contract exists. |
| `PositionId` | Conditional | PSS MOD-0288 position reference; same-tenant validate where contract exists. |
| `ManagerEmployeeProjectionId` | Optional | HCM employee projection reference for manager overlay; same-tenant validate when present. |
| `EffectiveFrom` | Yes | Start of HCM assignment overlay interval. |
| `EffectiveTo` | Optional | End of HCM assignment overlay interval; must be after `EffectiveFrom` when present. |
| `AssignmentState` | Yes | `Deferred`, `Validated`, `Active`, `Archived`; `Validated`/`Active` require validated anchors/references. |
| `SourceContractVersion` | Yes | Local contract metadata; stale/empty values fail validation. |
| `ReferenceValidationState` | Yes | `Deferred`, `Validated`, or `FailedClosed`. |
| `SensitiveAccessDecisionState` | Yes | `Deferred`, `Allowed`, or `Denied` from CAND-CAP-0008 precondition. |
| `AssignmentVersion` | Yes | Monotonic optimistic version for update/review flows. |

The following field classes remain forbidden: raw HRIS payload, provider
response, credential, token, secret, payroll data, bank data, tax data, payslip
data, biometric data, geolocation data, national ID, DOB, home address, and
PII-heavy profile attributes.

## 5. Repo Scope

Authorized runtime repo scope for the first backend/API slice:

- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Application/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Domain/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Infrastructure/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Persistence/**`
- `services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/**`

Governance reconciliation scope remains this pack file only. The runtime
implementation must stay inside `services/Diten.HumanCapitalService/**`.

## 6. Protected Paths

This review pack must not change:

- `frontend/**`
- `gateway/**`
- test projects outside `services/Diten.HumanCapitalService/tests/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/talent-ecosystem-platform/**`
- `execution/domains/human-capital-management/domain-config.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0006-hr-capability-block-shell.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0007-employee-profile-employment-record-projection.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0008-hr-governance-sensitive-access-controls.md`
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

Required HCM dependencies:

- `CAND-CAP-0006` - HR Capability Block Shell, governance boundary.
- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, completed
  first backend/API slice dependency.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, completed first
  backend/API slice dependency for sensitive assignment visibility.

Required PSS substrate dependencies:

- `MOD-0288` - Organization, Person & Position Directory, substrate only.
- `MOD-0288-FU01` - Position Assignment User Reference Validation, substrate only.
- `MOD-0288-FU02` - Person Reference Directory Projection, substrate only.
- `MOD-0251` - HRIS External SoR, source linkage dependency where assignment
  provenance is HRIS-sourced.
- `MOD-0021` - Audit Trail Service, future audit dependency if assignment changes
  become auditable events.

Additional controls to verify before runtime delivery:

- Records / retention / legal hold impact if assignment decisions are exported
  as evidence.
- Evidence linking impact if assignment review records need evidence graph
  support.
- Gateway ownership if an external API route is later required.

## 8. Runtime Constraints

- Runtime owner/key is `hcm.position-assignments`.
- `CAND-CAP-0009` is a governance identity only and must never be written into
  runtime literals, permission seeds, route metadata, database records, job
  names, telemetry owner fields, or test fixtures.
- `MOD-0299` must not be used for this HCM capability.
- `MOD-0288`, `MOD-0288-FU01`, and `MOD-0288-FU02` must not be used as this
  HCM-native assignment identity.
- Implementation is limited to HCM assignment overlay backend/API contract under
  `services/Diten.HumanCapitalService/**`.
- No frontend menu, Razor view, JavaScript, DataTable, RESX, or Gateway route is
  authorized.
- No employee master SoR ownership is authorized.
- No organization/position directory SoR ownership is authorized.
- CAND-CAP-0007 employee projection is the HCM runtime anchor; assignment overlay
  writes must fail closed 404 if the employee projection is missing, deleted, or
  cross-tenant.
- CAND-CAP-0008 sensitive access enforcement is a precondition for assignment
  read/manage operations; unavailable data-scope must fail closed or return an
  explicit deferred state.
- `Validated` or `Active` assignment state requires same-tenant validation of
  employee projection plus all provided PSS references. If PSS validation
  contract/client is unavailable, `Validated`/`Active` fails closed 404 or the
  assignment remains explicitly `Deferred`.
- Duplicate active `Code` must return 409 within tenant scope.
- Soft delete must set both `IsDeleted` and `DeletedAt`.
- PSS substrate modules remain dependencies and do not transfer ownership into
  `human-capital-management`.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

This pack does not create a visible HCM workspace, navigation menu, shell page,
Razor view, JavaScript, DataTable, RESX, or frontend route.

Future UI exposure must be authorized by a separate UI-specific pack after
runtime service, API, route, permission, privacy, and assignment-field decisions
are explicit.

## 10. Backend File Convention

Backend implementation must follow the repo's 5-layer .NET service convention,
CQRS/MediatR patterns, `Response<T>` envelope, tenant server-side resolution,
Mongo-backed repository standard, soft delete rules, same-tenant validation, and
permission conventions defined by this pack and the domain config.

## 11. Frontend File Contract

Frontend implementation is N/A for this review-stage backend/API pack.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, or frontend route may be added from this pack.

## 12. Validation Rules

Runtime validation rules:

- DCP-002 candidate gate for `CAND-CAP-0009` and
  `Position & Organization Assignment` must pass before this pack is used for
  any governance decision.
- Runtime implementation must fail closed if it attempts to use `MOD-0299` as the
  HCM identity.
- Runtime implementation must fail closed if it writes `CAND-CAP-0009` into
  runtime literals.
- Runtime policy must use the non-candidate owner/key `hcm.position-assignments`.
- Runtime policy must not duplicate `MOD-0288`, `MOD-0288-FU01`, or
  `MOD-0288-FU02` directory/reference data ownership or implementation.
- Runtime policy must not mutate CAND-CAP-0007 employee projection behavior or
  CAND-CAP-0008 sensitive access behavior; it may consume those contracts as
  preconditions.
- Same-tenant PSS reference validation must be available before assignment state
  can become `Validated` or `Active`; if validation is unavailable, runtime must
  fail closed 404 or use an explicit `Deferred` state.
- Validators must reject raw/sensitive markers and forbidden field names.

## 13. Failure Path to Verify

Before implementation or review promotion, verify these failure paths remain
blocked:

- Reusing `MOD-0299` for HR/HCM.
- Treating `CAND-CAP-0009` as a runtime module ID.
- Treating `MOD-0288`, `MOD-0288-FU01`, or `MOD-0288-FU02` as the HCM-native
  assignment identity.
- Copying or forking the PSS Organization, Person & Position Directory.
- Mutating `CAND-CAP-0007` employee projection runtime behavior from this
  pack.
- Bypassing `CAND-CAP-0008` sensitive access constraints for sensitive
  assignment visibility.
- Adding HCM frontend menus, pages, or Gateway routes from this pack.
- Claiming employee master SoR ownership.
- Claiming organization/position directory SoR ownership.
- Persisting raw HRIS payloads, provider responses, credentials, tokens,
  secrets, payroll, bank, tax, payslip, biometric, geolocation, national ID,
  DOB, home address, or PII-heavy data.
- Building TEP candidate/talent identity from this assignment boundary without
  dedicated HCM/TEP contracts.

## 14. Authorization Convention

Runtime permission namespace:

- `hcm.position-assignments.read`
- `hcm.position-assignments.manage`
- `hcm.position-assignments.archive`
- `hcm.position-assignments.reference-link.manage`

These permissions are authorized only for this first HCM assignment overlay API
slice. They do not authorize frontend menus, Gateway routes, broad HR lifecycle
permissions, PSS directory permissions, or CAND-CAP/MOD legacy runtime literals.

## 15. Gateway / API Routing Decision

API routing is authorized only inside `Diten.HumanCapitalService`.

No Ocelot route may be created. If external exposure is needed, the Gateway route
must be handled only by the integration-agent workflow after this backend/API
slice exists.

## 16. Acceptance Criteria

- AC-01: DCP-002 candidate gate passes for `CAND-CAP-0009` /
  `Position & Organization Assignment`.
- AC-02: Pack status is `done` after read-only implementation review PASS for
  the first HCM backend/API assignment overlay slice under
  `services/Diten.HumanCapitalService/**`.
- AC-03: The pack explicitly blocks `MOD-0299` for HR/HCM use.
- AC-04: The pack states that `CAND-CAP-0009` is governance-only and not a
  runtime literal.
- AC-05: Runtime owner/key is `hcm.position-assignments`.
- AC-06: `MOD-0288`, `MOD-0288-FU01`, and `MOD-0288-FU02` are listed as
  substrate only, not as HCM-native assignment identity.
- AC-07: `CAND-CAP-0006`, `CAND-CAP-0007`, and `CAND-CAP-0008` are listed as HCM
  dependencies.
- AC-08: CAND-CAP-0007 employee projection is same-tenant validated as the HCM
  runtime anchor; missing/deleted/cross-tenant anchors return 404.
- AC-09: CAND-CAP-0008 sensitive access enforcement is applied as a precondition
  for assignment read/manage operations.
- AC-10: HCM ownership is not moved into PSS and PSS ownership is not moved into
  HCM.
- AC-11: Employee master SoR ownership is explicitly out of scope.
- AC-12: Organization/position directory SoR ownership is explicitly out of
  scope.
- AC-13: Raw HRIS, secret, payroll, bank, tax, payslip, biometric, geolocation,
  national ID, DOB, home address, and PII-heavy persistence are explicitly out
  of scope.
- AC-14: Duplicate active assignment `Code` returns 409 within tenant scope.
- AC-15: Soft delete sets `IsDeleted` and `DeletedAt`.
- AC-16: PSS reference validation uses available contracts/clients when present;
  otherwise `Validated`/`Active` fails closed 404 or explicit `Deferred` state is
  used.
- AC-17: Permission namespace checks cover `hcm.position-assignments.read`,
  `manage`, `archive`, and `reference-link.manage`.
- AC-18: Frontend, Gateway direct edit, PSS implementation files, employee master
  SoR, and directory copy remain out of scope.
- AC-19: Runtime literal scan for `CAND-CAP-0009` and `MOD-0299` passes.
- AC-20: The module pack includes all 20 required sections.

## 17. Test Expectations

Required governance check:

```bash
python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0009 --name "Position & Organization Assignment"
```

Expected result:

```text
OK  candidate CAND-CAP-0009: temporary governance identity, pending EA, not Blueprint-backed, not in runtime.
```

Runtime literal scan:

```bash
rg -n "CAND-CAP-0009|MOD-0299" services/Diten.HumanCapitalService frontend gateway tests
```

Expected result: no matches. `CAND-CAP-0009` and `MOD-0299` must not be runtime
literals in HCM, frontend, Gateway, or tests.

Build, unit tests, API tests, UI verifier, DataTable verifier, and localization
verifier expectations:

- `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore`
- HCM targeted PositionAssignments tests:
  - tenant isolation
  - duplicate active assignment guard
  - soft delete + `DeletedAt`
  - CAND-CAP-0007 employee projection anchor validation
  - CAND-CAP-0008 sensitive access precondition
  - GetAll sensitive-access precondition/filtering
  - GetById broad-read fail-closed behavior
  - reference-link.manage cannot perform full assignment mutation
  - PSS reference `Deferred`/fail-closed behavior
  - forbidden raw/sensitive field marker validation
  - runtime literal scan
- UI verifier, DataTable verifier, and localization verifier are N/A because no
  frontend implementation is authorized.

## 18. Ready-for-Dev Checklist

- [x] HCM governance-only domain exists.
- [x] `CAND-CAP-0009` candidate reservation exists in the registry.
- [x] DCP-002 candidate gate passes.
- [x] `MOD-0299` is documented as blocked for HR/HCM use.
- [x] `MOD-0288`, `MOD-0288-FU01`, and `MOD-0288-FU02` are retained as PSS
  substrate only.
- [x] `CAND-CAP-0006` shell dependency is listed.
- [x] `CAND-CAP-0007` employee projection dependency is listed.
- [x] `CAND-CAP-0008` sensitive access dependency is listed.
- [x] Runtime implementation is authorized only for the first backend/API
  assignment overlay slice under `services/Diten.HumanCapitalService/**`.
- [x] UI/frontend/gateway work is closed.
- [x] Employee master SoR ownership is excluded.
- [x] Organization/position directory SoR ownership is excluded.
- [x] Raw HRIS, sensitive PII-heavy, credential, payroll, bank, tax, payslip,
  biometric, and geolocation persistence are excluded.
- [x] EA explicitly approves candidate-based governance continuation for
  `CAND-CAP-0009`.
- [x] HCM domain owner approves assignment overlay boundary.
- [x] Platform owner confirms `MOD-0288` / `MOD-0288-FU01` /
  `MOD-0288-FU02` remain PSS substrate/dependency and no PSS directory copy is
  authorized.
- [x] Security owner confirms sensitive assignment visibility must flow through
  CAND-CAP-0008 constraints.
- [x] EA candidate-runtime policy waiver approved.
- [x] Runtime owner/key approved: `hcm.position-assignments`.
- [x] Runtime permission namespace approved:
  `hcm.position-assignments.read`, `hcm.position-assignments.manage`,
  `hcm.position-assignments.archive`, and
  `hcm.position-assignments.reference-link.manage`.
- [x] CAND-CAP-0007 employee projection anchor validation is required.
- [x] CAND-CAP-0008 sensitive access precondition is required for assignment
  read/manage operations.
- [x] PSS reference validation contract decision is closed: validate when
  available; otherwise fail closed 404 for `Validated`/`Active` or use explicit
  `Deferred`.
- [x] UI/frontend/gateway/PSS work remains out of scope; API and persistence are
  authorized only inside `services/Diten.HumanCapitalService/**`.
- [x] Gateway direct edit is closed; integration-agent follow-up only if external
  exposure is needed.

## 19. Implementation Notes

`@orchestrator` may implement only the first backend/API assignment overlay slice
authorized by this pack. The implementation must stay under
`services/Diten.HumanCapitalService/**` and must not write `CAND-CAP-0009` or
`MOD-0299` into runtime literals.

The intended sequencing is:

1. Keep `CAND-CAP-0009` as the temporary governance identity for this HCM
   assignment boundary.
2. Keep `MOD-0299` blocked because it maps to platform SaaS Billing &
   Invoicing / `CAND-CAP-0005`.
3. Keep `MOD-0288`, `MOD-0288-FU01`, and `MOD-0288-FU02` as PSS substrate only.
4. Use CAND-CAP-0007 employee projection as the HCM runtime anchor without
   mutating its existing behavior.
5. Use CAND-CAP-0008 sensitive access as the access precondition for assignment
   read/manage operations without copying its policy engine.
6. Keep this slice as HCM assignment overlay only; no PSS directory copy,
   employee master SoR, HR lifecycle module, payroll/time-attendance module, RBAC
   engine, audit engine, or TEP identity implementation is authorized.

This pack should remain an assignment projection/overlay boundary contract. It
must not become an employee master, organization directory, position directory,
payroll, time-attendance, RBAC engine, audit engine, or TEP identity pack.

Done promotion reconciliation note, 2026-08-11:

- First backend/API assignment overlay slice implemented under
  `services/Diten.HumanCapitalService/**`.
- Read-only implementation review PASS.
- Security drift fixed:
  - GetAll sensitive-access precondition/filtering prevents sensitive/restricted
    assignment metadata exposure through broad read.
  - GetById broad read fails closed for protected assignment metadata.
  - `reference-link.manage` no longer performs full assignment mutation and uses
    a reference-link-only request/command/handler.
- Candidate gate PASS:
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0009 --name "Position & Organization Assignment"`.
- HCM API build PASS:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false`
  returned 0 warnings and 0 errors.
- PositionAssignment targeted tests PASS: 16/16.
- Full HCM Application tests PASS: 40/40.
- Runtime literal scan PASS:
  `rg -n "CAND-CAP-0009|MOD-0299" services/Diten.HumanCapitalService frontend gateway tests`
  returned no matches.
- Frontend, Gateway, and PSS implementation scope remained closed.
- `CAND-CAP-0009` and `MOD-0299` runtime literal prohibition remains enforced.

## 20. Follow-up Items

Open blockers: none.

Runtime guardrails:

- EA canonical MOD assignment for `Position & Organization Assignment` remains a
  future governance follow-up, not a first-slice runtime blocker.
- `CAND-CAP-0009` is not Blueprint-backed and cannot be a runtime literal.
- `MOD-0299` is blocked for HR/HCM and must not be used.
- Candidate-runtime waiver applies only to non-candidate runtime owner/key
  `hcm.position-assignments`.
- Permission namespace is limited to the four approved
  `hcm.position-assignments.*` permissions listed in Section 14.
- MOD-0288 / MOD-0288-FU01 / MOD-0288-FU02 remain PSS substrate; PSS
  directory/reference implementation must not be copied.
- CAND-CAP-0008 sensitive access integration is a precondition and must not be
  bypassed or duplicated.
- Gateway exposure remains integration-agent follow-up only if needed.

Waivers:

- Candidate-runtime waiver is granted only for non-candidate runtime owner/key
  `hcm.position-assignments`.
- No employee master SoR waiver is granted.
- No PSS directory ownership transfer waiver is granted.
- No sensitive-data persistence waiver is granted.
- No MOD-0299 reuse waiver is granted.

Recommended next safe step:

- Continue with the next HCM/TEP roadmap identity decision or run a delivery
  board/reconciliation update for completed HCM/PSS backbone slices.
