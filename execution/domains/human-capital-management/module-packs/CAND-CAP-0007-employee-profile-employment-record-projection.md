---
id: CAND-CAP-0007
name: Employee Profile & Employment Record Projection
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner
branch: feature/hcm/cand-cap-0007-employee-profile-employment-record-projection
started: 2026-08-10
target: 2026-09-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
shell_dependency: execution/domains/human-capital-management/module-packs/CAND-CAP-0006-hr-capability-block-shell.md
candidate_identity: CAND-CAP-0007
legacy_excel_id: MOD-0298
canonicalization_status: candidate-runtime waiver approved / pending canonical MOD
runtime_service_candidate: Diten.HumanCapitalService
runtime_owner_key: hcm.employee-projections
bootstrap_type: runtime-ready first backend-api slice
---

# CAND-CAP-0007 - Employee Profile & Employment Record Projection

> Status: done for the completed first provider-neutral backend/API slice. The
> implementation review passed, governance drift was fixed, and no additional
> runtime slice is authorized without a new module-pack decision.
> `CAND-CAP-0007` remains a governance/documentation identity and must never be
> written into runtime literals. This pack authorizes no frontend, direct Gateway
> edits, provider adapter, employee master SoR, or payroll/time-attendance scope.

## 1. Module Summary

`CAND-CAP-0007` reserves the temporary DCP-002 candidate identity for
`Employee Profile & Employment Record Projection` in the
`human-capital-management` domain.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
where the Excel row arrived as `MOD-0298`. `MOD-0298` must not be used for
HR/HCM because it remains a deprecated platform alias for Tenant Module
Entitlements / `CAND-CAP-0002-FU05`.

This pack prepares the first runtime-ready provider-neutral backend/API slice for
HRIS-sourced employee and employment reference projections. EA has approved a
candidate-runtime policy waiver: `CAND-CAP-0007` remains governance/documentation
only while runtime uses the non-candidate owner/key `hcm.employee-projections`.
This is not an EA canonical MOD allocation. The pack does not authorize employee
master system-of-record ownership, employment lifecycle processing, payroll/time
ownership, raw provider payload persistence, frontend, or direct Gateway work.

## 2. Ownership and Boundaries

Owned by this pack:

- HRIS-sourced employee profile projection backend/API contract.
- Employment record projection metadata backend/API contract.
- HCM-sensitive visibility boundary for the first runtime slice.
- Future HCM runtime ownership boundary for employee/employment projections.
- Same-tenant validation behavior against approved PSS substrate.

Not owned by this pack:

- Employee master system of record.
- Employment lifecycle transactions, onboarding, offboarding, compensation,
  benefits, performance, learning, succession, or TEP talent identity.
- HRIS provider ownership, raw HRIS payload storage, provider adapter logic, or
  background sync implementation.
- Payroll, time-attendance, bank, tax, payslip, biometric, geolocation, national
  ID, DOB, home address, or PII-heavy profile ownership.
- Frontend menus, Gateway direct routes, provider adapters, and broad HCM shell
  runtime.

## 3. Owned Objects

Runtime-ready first-slice objects:

| Object | Type | Purpose |
|---|---|---|
| EmployeeProfileProjection | Entity | Tenant-owned minimal employee profile reference projection. |
| EmploymentRecordProjection | Entity | Tenant-owned employment record projection metadata. |
| EmployeeProjectionSourceLink | Entity | HRIS source and person reference correlation metadata. |
| EmployeeProjectionHealthSnapshot | Entity | Redacted operational health metadata for the projection slice. |
| IEmployeeProjectionRepository | Repository contract | Tenant-aware persistence for projection records. |
| EmployeeProjectionsController | API controller | Thin controller delegating to MediatR handlers. |

No frontend, Gateway direct edit, provider adapter, payroll/time-attendance, or
employee master SoR object is authorized by this pack.

## 4. Entity Fields

Approved minimal runtime fields:

| Field | Required | Notes |
|---|---|---|
| `Code` | Yes | Tenant-scoped active uniqueness; no candidate or legacy MOD literal. |
| `DisplayName` | Yes | Minimal routing label only; no PII-heavy profile attributes. |
| `HrisSourceProfileId` | Yes | Same-tenant validation against `MOD-0251` when available. |
| `PersonReferenceId` | Yes | Same-tenant validation against `MOD-0288-FU02` when available. |
| `ExternalEmployeeReference` | Yes | Opaque external employee reference; no raw payload fragments. |
| `EmploymentRecordReferenceKey` | Yes | Opaque employment record reference key; not employee master SoR. |
| `EmploymentStatusCode` | Yes | Reference/status code metadata only. |
| `WorkerTypeCode` | Yes | Worker type metadata only. |
| `SourceContractVersion` | Yes | Local source contract version; stale/unsupported values fail validation. |
| `ProjectionState` | Yes | `Deferred`, `SourceLinked`, `Validated`, `Archived`; `Validated` requires same-tenant source/person validation. |
| `VisibilityClassification` | Yes | Mandatory HR-sensitive visibility classification. |
| `SourceLastSyncedAt` | No | Source sync timestamp metadata; no background sync ownership. |
| `ProjectionVersion` | Yes | Optimistic/version metadata for projection updates. |

All records also use repo-standard base fields such as `Id`, server-side
`TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, and `UpdatedAt`.

The following field classes are explicitly forbidden unless a later approved
runtime pack grants a narrow exception: raw HRIS payload, provider response,
credential, token, secret, payroll data, bank data, tax data, payslip data,
biometric data, geolocation data, national ID, DOB, home address, and PII-heavy
profile attributes.

## 5. Repo Scope

Authorized scope for this ready-for-dev backend/API pack:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0007-employee-profile-employment-record-projection.md`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Application/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Domain/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Infrastructure/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Persistence/**`
- `services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/**`

The runtime owner/key is `hcm.employee-projections`. Runtime files must not use
`CAND-CAP-0007` or `MOD-0298` as literals.

## 6. Protected Paths

This ready-for-dev pack must not change:

- `frontend/**`
- `gateway/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/talent-ecosystem-platform/**`
- `execution/domains/human-capital-management/domain-config.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0006-hr-capability-block-shell.md`
- `execution/registries/module-id-registry.md`, except a separate EA-approved
  identity reservation or canonicalization update.
- `.antigravity/**`

## 7. Dependencies

Governance dependencies:

- `AGENTS.md`
- `execution/domains/human-capital-management/domain-config.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0006-hr-capability-block-shell.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Required substrate dependencies for future runtime planning:

- `MOD-0251` - HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR].
- `MOD-0288-FU02` - Person Reference Directory Projection.
- `CAND-CAP-0006` - HR Capability Block Shell governance boundary.

Additional R0 controls to verify before runtime delivery:

- `MOD-0018` - RBAC / ABAC Authorization.
- `MOD-0021` - Audit Trail Service.
- `MOD-0030` - Records / Retention / Legal Hold.
- `MOD-0031` - Evidence Linking.
- `MOD-0048` - Reference Data Management.
- `MOD-0057` - Taxonomy / Tagging Service.

## 8. Runtime Constraints

- Runtime implementation is authorized only for the first provider-neutral
  backend/API slice after a separate `@orchestrator` prompt references this
  ready-for-dev pack.
- `CAND-CAP-0007` is a governance identity only and must never be written into
  runtime literals, permission seeds, route metadata, database records, job
  names, telemetry owner fields, or test fixtures.
- `MOD-0298` must not be used for this HCM capability.
- Runtime owner/key is `hcm.employee-projections`.
- No frontend menu, Razor view, JavaScript, DataTable, RESX, or direct Gateway
  route is authorized.
- No employee master SoR ownership is authorized unless a future EA decision and
  ready-for-dev runtime pack explicitly grant it.
- PSS backbone modules remain dependencies/substrate and do not transfer HR/HCM
  ownership into `platform-shared-services`.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

This pack depends on the governance boundary approved in
`CAND-CAP-0006 - HR Capability Block Shell`. It does not create a visible HCM
workspace, navigation menu, shell page, or frontend route.

Future UI exposure must be authorized by a separate UI-specific pack after the
backend/API slice is implemented and gateway/security decisions are explicit.

## 10. Backend File Convention

Backend implementation must follow the repo's 5-layer .NET service convention,
CQRS/MediatR patterns, `Response<T>` envelope, tenant server-side resolution,
soft delete rules, same-tenant validation, and permission conventions defined by
this pack and `human-capital-management/domain-config.md`.

## 11. Frontend File Contract

Frontend implementation is N/A for this governance-approved pack.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, or frontend route may be added from this pack.

## 12. Validation Rules

Runtime validation rules:

- DCP-002 candidate gate for `CAND-CAP-0007` and
  `Employee Profile & Employment Record Projection` must pass before this pack
  is used for any governance decision.
- Runtime implementation must fail closed if it attempts to use `MOD-0298` as
  the HCM identity.
- Runtime implementation must fail closed if it writes `CAND-CAP-0007` into
  runtime literals.
- HRIS source/profile data must validate same tenant against `MOD-0251`
  contracts/repositories where available; if unavailable, use explicit
  `Deferred` state or fail-closed `404`.
- Person reference projection must validate same tenant against `MOD-0288-FU02`
  contracts/repositories where available; if unavailable, use explicit
  `Deferred` state or fail-closed `404`.
- Employee/employment projection cannot move to `Validated` if source/person
  references are unresolved.
- `VisibilityClassification` is mandatory.
- Validators must reject raw HRIS/provider payloads, credentials, tokens,
  secrets, payroll, bank, tax, payslip, biometric, geolocation, national ID,
  DOB, home address, and PII-heavy field markers.

## 13. Failure Path to Verify

Before promoting this pack or preparing a runtime follow-up, verify these failure
paths remain blocked:

- Reusing `MOD-0298` for HR/HCM.
- Treating `CAND-CAP-0007` as a runtime module ID.
- Adding HCM frontend menus, pages, or gateway routes from this governance pack.
- Claiming employee master SoR ownership.
- Persisting raw HRIS payloads or provider responses.
- Persisting credentials, tokens, secrets, payroll, bank, tax, payslip,
  biometric, geolocation, national ID, DOB, home address, or PII-heavy data.
- Building TEP candidate/talent identity from this projection without dedicated
  HCM/TEP contracts.
- Accepting cross-tenant HRIS/person/org/position references.

## 14. Authorization Convention

Runtime permission namespace:

- `hcm.employee-projections.*`

Approved concrete permissions:

- `hcm.employee-projections.read`
- `hcm.employee-projections.manage`
- `hcm.employee-projections.archive`
- `hcm.employee-projections.source-link.manage`

Permission checks must use these non-candidate strings. `CAND-CAP-0007` and
`MOD-0298` must not appear in permission seeds, attributes, route metadata, or
tests.

## 15. Gateway / API Routing Decision

Gateway routing is N/A for this first slice.

API routes inside `Diten.HumanCapitalService` are authorized by this
ready-for-dev pack. No Ocelot route may be created or edited directly. If
external exposure is needed, route work must be handled only by the
integration-agent workflow after implementation scope is confirmed.

## 16. Acceptance Criteria

- AC-01: DCP-002 candidate gate passes for
  `CAND-CAP-0007` / `Employee Profile & Employment Record Projection`.
- AC-02: Pack status is `review` for the completed first provider-neutral
  backend/API slice.
- AC-03: The pack explicitly blocks `MOD-0298` for HR/HCM use.
- AC-04: The pack states that `CAND-CAP-0007` is governance-only and not a
  runtime literal.
- AC-05: Runtime implementation is authorized only within the approved
  `services/Diten.HumanCapitalService/**` backend/API scope after a separate
  orchestration prompt; frontend and direct gateway work are closed.
- AC-06: `CAND-CAP-0006`, `MOD-0251`, and `MOD-0288-FU02` are listed as required
  governance/runtime-planning dependencies.
- AC-07: HCM ownership is not moved into PSS.
- AC-08: Employee master SoR ownership is explicitly out of scope.
- AC-09: Raw HRIS, secret, payroll, bank, tax, payslip, biometric, geolocation,
  national ID, DOB, home address, and PII-heavy persistence are explicitly out
  of scope.
- AC-10: The module pack includes all 20 required sections.
- AC-11: Runtime owner/key is `hcm.employee-projections`.
- AC-12: Runtime permissions are limited to
  `hcm.employee-projections.read`, `manage`, `archive`, and
  `source-link.manage`.
- AC-13: Same-tenant `MOD-0251` / `MOD-0288-FU02` validation uses available
  contracts/repositories, otherwise explicit `Deferred` state or fail-closed
  `404`.
- AC-14: `VisibilityClassification` is mandatory.

## 17. Test Expectations

Required governance check:

```bash
python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0007 --name "Employee Profile & Employment Record Projection"
```

Expected result:

```text
OK  candidate CAND-CAP-0007: temporary governance identity, pending EA, not Blueprint-backed, not in runtime.
```

Required implementation verification after orchestration:

- DCP-002 candidate gate passes.
- `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug`
- Targeted `Diten.HumanCapitalService` tests cover tenant isolation, duplicate
  active `Code`, authorization, soft delete `DeletedAt`, no raw payload/secret
  persistence, same-tenant source/person validation, `Deferred`/fail-closed
  behavior, mandatory `VisibilityClassification`, and absence of
  `CAND-CAP-0007` / `MOD-0298` runtime literals.
- UI/DataTable/localization verifier: N/A for first backend/API slice.

## 18. Governance Approval Checklist

- [x] HCM governance-only domain exists.
- [x] `CAND-CAP-0007` candidate reservation exists in the registry.
- [x] DCP-002 candidate gate passes.
- [x] `MOD-0298` is documented as blocked for HR/HCM use.
- [x] `CAND-CAP-0006` shell dependency is listed.
- [x] `MOD-0251` HRIS substrate dependency is listed.
- [x] `MOD-0288-FU02` person reference substrate dependency is listed.
- [x] Runtime implementation is limited to the approved first backend/API slice.
- [x] UI/frontend/gateway work is closed.
- [x] Employee master SoR ownership is excluded.
- [x] Raw HRIS, sensitive PII-heavy, credential, payroll, bank, tax, payslip,
  biometric, and geolocation persistence are excluded.
- [x] EA explicitly approves candidate-based governance continuation for
  `CAND-CAP-0007`.
- [x] User approves this pack for governance-only continuation.
- [x] EA candidate-runtime policy waiver is approved.
- [x] Runtime owner/key is approved as `hcm.employee-projections`.
- [x] Runtime repo scope is approved under `services/Diten.HumanCapitalService/**`.
- [x] Runtime permission namespace is approved as
  `hcm.employee-projections.*`.
- [x] Same-tenant validation behavior for `MOD-0251` and `MOD-0288-FU02` is
  approved: validate through available contracts/repositories, otherwise
  explicit `Deferred` state or fail-closed `404`.
- [x] Privacy/security review approves the minimal projection fields.
- [x] Sensitive HR visibility/data-scope policy is approved for runtime use.
- [x] Gateway direct edit is closed; integration-agent follow-up only if external
  exposure is needed.

## 19. Implementation Notes

Call `@orchestrator` only with this ready-for-dev pack and only for the approved
provider-neutral backend/API slice. Do not start frontend, direct gateway,
provider adapter, payroll/time-attendance, employee master SoR, or broad HCM
runtime work from this pack.

The intended sequencing is:

1. Keep `CAND-CAP-0007` as the temporary governance identity for the projection
   pack.
2. Keep `MOD-0298` bound to its existing platform deprecated-alias chain.
3. Use the approved candidate-runtime waiver until EA assigns a canonical MOD and
   records migration.
4. Keep the first runtime slice projection-only.
5. Keep `Diten.HumanCapitalService` as the runtime host for this slice.

This pack should remain a projection boundary contract. It must not become an
employee master, HR lifecycle, payroll, time-attendance, or TEP identity pack.

## 20. Follow-up Items

Open blockers: none.

Future governance follow-ups:

- EA canonical MOD assignment for `Employee Profile & Employment Record
  Projection` remains pending, but it is not a runtime completion blocker for
  this finished first backend/API slice.
- Gateway route remains a follow-up only if external exposure is needed and must
  stay integration-agent owned.

Waivers:

- EA candidate-runtime policy waiver is granted for the first backend/API slice.
- No employee master SoR waiver is granted.
- No sensitive-data persistence waiver is granted.

### Implementation completion reconciliation - 2026-08-10

The first provider-neutral backend/API slice was implemented under
`services/Diten.HumanCapitalService/**`, passed implementation review, and is
now done.

Completed implementation scope:

- Thin `EmployeeProjectionsController` with MediatR/CQRS and `Response<T>`
  envelope.
- Runtime owner/key: `hcm.employee-projections`.
- Permission checks:
  `hcm.employee-projections.read`,
  `hcm.employee-projections.manage`,
  `hcm.employee-projections.archive`, and
  `hcm.employee-projections.source-link.manage`.
- Tenant server-side resolution, tenant-aware lookup/list/update/archive
  behavior, duplicate active `Code` guard, and soft delete with
  `IsDeleted + DeletedAt`.
- Mongo-backed `IEmployeeProjectionRepository` production binding.
- Mongo collection: `hcm_employee_profile_projections`.
- Tenant-aware active code unique index:
  `ux_hcm_employee_projections_tenant_code_active`.
- MOD-0251 / MOD-0288-FU02 references validate same tenant when a
  contract/repository exists; otherwise implementation uses explicit
  `Deferred` state or fail-closed `404`.
- Validators reject forbidden raw/sensitive markers.

Closed blocker:

- Mongo-backed persistence blocker is closed. Production DI no longer binds
  `IEmployeeProjectionRepository` to an in-memory repository. Any in-memory test
  double is confined to the test project.

Verification evidence:

- Build PASS:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore`
  returned 0 warnings and 0 errors.
- Targeted tests PASS:
  `dotnet test services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/Diten.HumanCapitalService.Application.Tests.csproj -c Debug --no-restore --filter EmployeeProjection`
  returned 9/9 passing tests.
- Candidate/legacy runtime literal scan PASS:
  `rg -n "CAND-CAP-0007|MOD-0298" services/Diten.HumanCapitalService frontend gateway tests`
  returned no matches.
- Production in-memory repository scan PASS:
  `rg -n "AddSingleton<IEmployeeProjectionRepository, InMemoryEmployeeProjectionRepository>|InMemoryEmployeeProjectionRepository" services/Diten.HumanCapitalService/src`
  returned no matches.

Scope retained:

- Frontend/UI/Razor/JavaScript/DataTable/RESX/menu remained closed.
- Gateway/`ocelot.json` remained closed; any future external exposure remains an
  integration-agent follow-up.
- PSS backbone files and ownership remained untouched.
- `CAND-CAP-0007` remains governance/documentation only and is not a runtime
  literal.
- `MOD-0298` remains blocked for HCM and is not a runtime literal.
- Employee master SoR, HR lifecycle, provider adapters, raw HRIS/provider
  payloads, credentials, payroll/bank/tax/payslip/biometric/geolocation,
  national ID, DOB, home address, and PII-heavy persistence remain out of scope.

Recommended next safe step:

- Continue with the next HCM/TEP governance decision or module-pack preparation.
- Review governance drift was fixed after implementation review, and the pack is
  now `done` with build/test/literal scan/in-memory scan evidence recorded above.

### Done promotion - 2026-08-10

Done promotion is recorded because:

- Implementation review PASS.
- Governance drift fixed.
- Mongo-backed persistence blocker closed.
- Build/test/literal scan/in-memory scan PASS.
- Frontend/gateway/PSS scope remained closed.
- Candidate-runtime waiver remains valid:
  runtime owner/key is `hcm.employee-projections`;
  `CAND-CAP-0007` remains a governance/documentation identity; and
  `MOD-0298` remains a blocked legacy ID for HCM.
