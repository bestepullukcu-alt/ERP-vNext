---
id: CAND-CAP-0008
name: HR Governance & Sensitive Access Controls
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner
branch: feature/hcm/cand-cap-0008-hr-governance-sensitive-access-controls
started: 2026-08-10
target: 2026-09-30
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
candidate_identity: CAND-CAP-0008
legacy_excel_id: MOD-0314
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.HumanCapitalService
runtime_owner_key: hcm.sensitive-access
bootstrap_type: backend-api-first-slice
---

# CAND-CAP-0008 - HR Governance & Sensitive Access Controls

> Status: done. The first HCM backend/API sensitive access enforcement slice has
> been implemented and passed read-only implementation review. No frontend,
> gateway, PSS ownership transfer, MOD-0018 engine copy, or broad HR
> authorization engine is authorized. CAND-CAP-0008 remains a
> governance/documentation identity and must never be written into runtime
> literals.

## 1. Module Summary

`CAND-CAP-0008` reserves the temporary DCP-002 candidate identity for
`HR Governance & Sensitive Access Controls` in the `human-capital-management`
domain.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
where the Excel/DCP row arrived as `MOD-0314`. `MOD-0314` must not be used
because it is absent from the Blueprint and has no canonical registry row.

This pack prepares the governance boundary for HR-sensitive visibility,
data-scope rules, access decisions, and audit expectations across HCM employee
projection and future HR foundation modules. `MOD-0018` remains the PSS
RBAC / ABAC Authorization substrate only; this pack must not copy the platform
authorization engine or move MOD-0018 ownership into HCM.

## 2. Ownership and Boundaries

Owned by this pack:

- HCM-sensitive visibility and access-control boundary.
- Employee projection sensitive access policy expectations.
- Data-scope and visibility classification policy for HCM-owned projections.
- HCM-side policy metadata planning for sensitive HR domains.
- Audit and evidence expectations for sensitive HR access decisions.

Not owned by this pack:

- Platform RBAC / ABAC engine ownership (`MOD-0018`).
- Audit Trail Service ownership (`MOD-0021`).
- Employee master system of record.
- Employee projection runtime behavior already completed under `CAND-CAP-0007`.
- HR lifecycle transactions, compensation, benefits, performance, learning,
  succession, offboarding execution, or TEP candidate/talent identity.
- Frontend menus, Gateway direct routes, provider adapters, raw HRIS payloads,
  credentials, payroll, bank, tax, payslip, biometric, geolocation, national ID,
  DOB, home address, or PII-heavy profile ownership.

## 3. Owned Objects

Authorized runtime contract objects:

| Object | Type | Purpose |
|---|---|---|
| HcmSensitiveVisibilityPolicy | Runtime contract | Defines HCM visibility classifications and allowed access outcomes. |
| HcmSensitiveAccessRuleSet | Runtime contract | Defines HCM-side rule grouping for the first backend/API enforcement slice. |
| HcmDataScopeBoundary | Runtime contract | Defines tenant, worker, manager, HR partner, and restricted HR access scope concepts. |
| HcmSensitiveAccessAuditExpectation | Runtime contract | Defines local bounded audit/deferred-state expectations for sensitive HR access decisions. |
| HcmSensitiveAccessRuntimeDecisionLog | Runtime metadata | Captures runtime permission, audit, and validation decisions without raw sensitive data. |

Runtime entities, DTOs, controllers, handlers, repositories, indexes, permission
attributes, and tests are authorized only inside `services/Diten.HumanCapitalService/**`
and only for the first sensitive-access backend/API enforcement slice.

## 4. Entity Fields

Runtime fields are limited to policy/enforcement metadata required for the first
backend/API slice.

Allowed field groups:

| Field Group | Status | Notes |
|---|---|---|
| Visibility classification | Runtime | Must align with `CAND-CAP-0007` `VisibilityClassification`. |
| Access decision reason | Runtime | Must avoid raw sensitive data and must be audit-safe. |
| Data-scope rule key | Runtime | Must use non-candidate runtime owner/key `hcm.sensitive-access`. |
| Local audit/deferred reference | Runtime | Bounded local metadata only when `MOD-0021` contract is unavailable. |
| Policy version metadata | Runtime | Must support stale/immutable policy validation. |

The following field classes are explicitly forbidden: raw HRIS payload, provider
response, credential, token, secret, payroll data, bank data, tax data, payslip
data, biometric data, geolocation data, national ID, DOB, home address, and
PII-heavy profile attributes.

## 5. Repo Scope

Authorized runtime scope for this review pack:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0008-hr-governance-sensitive-access-controls.md`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Application/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Domain/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Infrastructure/**`
- `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Persistence/**`
- `services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/**`

The runtime owner/key is `hcm.sensitive-access`. Runtime files must not use
`CAND-CAP-0008` or `MOD-0314` as literals.

## 6. Protected Paths

This review pack must not change:

- `frontend/**`
- `gateway/**`
- `tests/**`, except `services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/talent-ecosystem-platform/**`
- `execution/domains/human-capital-management/domain-config.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0006-hr-capability-block-shell.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0007-employee-profile-employment-record-projection.md`
- `execution/registries/module-id-registry.md`, except a separate EA-approved
  identity reservation or canonicalization update.
- `.antigravity/**`

## 7. Dependencies

Governance dependencies:

- `AGENTS.md`
- `execution/domains/human-capital-management/domain-config.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0006-hr-capability-block-shell.md`
- `execution/domains/human-capital-management/module-packs/CAND-CAP-0007-employee-profile-employment-record-projection.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`
- `execution/registries/module-id-registry.md`

Required substrate dependencies for future runtime planning:

- `MOD-0018` - RBAC / ABAC Authorization, substrate only.
- `MOD-0021` - Audit Trail Service, audit dependency.
- `CAND-CAP-0006` - HR Capability Block Shell, governance boundary.
- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, completed
  first backend/API slice dependency.

Additional controls to verify before runtime delivery:

- Records / retention / legal hold impact if sensitive access decisions are
  exported as evidence.
- Evidence linking impact if access review records need evidence graph support.
- Gateway ownership if an external API route is later required.

## 8. Runtime Constraints

- Runtime implementation is authorized only for the first HCM backend/API
  sensitive access enforcement slice in `Diten.HumanCapitalService`.
- `CAND-CAP-0008` is a governance identity only and must never be written into
  runtime literals, permission seeds, route metadata, database records, job
  names, telemetry owner fields, or test fixtures.
- `MOD-0314` must not be used for this HCM capability.
- `MOD-0018` must not be used as this HCM-native capability identity.
- No frontend menu, Razor view, JavaScript, DataTable, RESX, Gateway route, PSS
  implementation edit, MOD-0018 engine copy, or broad HR authorization engine is
  authorized.
- No employee master SoR ownership is authorized.
- `CAND-CAP-0007` employee projection may be consumed only through
  `VisibilityClassification` and data-scope enforcement. Broad behavior changes
  or employee master ownership are out of scope.
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
runtime service, API, route, permission, and privacy decisions are explicit.

## 10. Backend File Convention

Backend implementation must follow the repo's 5-layer .NET service convention,
CQRS/MediatR patterns, `Response<T>` envelope, tenant server-side resolution,
soft delete rules, same-tenant validation, and permission conventions defined by
this pack and the HCM domain config.

## 11. Frontend File Contract

Frontend implementation is N/A for this reviewed backend/API pack.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, or frontend route may be added from this pack.

## 12. Validation Rules

Runtime validation rules:

- DCP-002 candidate gate for `CAND-CAP-0008` and
  `HR Governance & Sensitive Access Controls` must pass before this pack is used
  for any governance decision.
- Any future runtime pack must fail closed if it attempts to use `MOD-0314` as
  the HCM identity.
- Any future runtime pack must fail closed if it writes `CAND-CAP-0008` into
  runtime literals.
- Runtime policy must use non-candidate owner/key `hcm.sensitive-access`.
- Runtime policy must not duplicate `MOD-0018` RBAC/ABAC data ownership or
  implementation.
- Runtime policy must not mutate `CAND-CAP-0007` employee projection behavior
  beyond `VisibilityClassification` + data-scope enforcement without a dedicated
  follow-up pack.
- Sensitive HR visibility decisions must use `MOD-0021` where available; if the
  contract is unavailable, use bounded local audit/deferred metadata and record a
  future MOD-0021 integration follow-up.

## 13. Failure Path to Verify

Before promoting this pack or preparing a runtime follow-up, verify these failure
paths remain blocked:

- Reusing `MOD-0314` for HR/HCM.
- Treating `CAND-CAP-0008` as a runtime module ID.
- Treating `MOD-0018` as the HCM-native module identity.
- Copying or forking the platform RBAC/ABAC engine.
- Mutating `CAND-CAP-0007` employee projection runtime behavior.
- Adding HCM frontend menus, pages, or Gateway routes from this review
  backend/API pack.
- Claiming employee master SoR ownership.
- Persisting raw HRIS payloads, provider responses, credentials, tokens,
  secrets, payroll, bank, tax, payslip, biometric, geolocation, national ID,
  DOB, home address, or PII-heavy data.
- Building TEP candidate/talent identity from this access boundary without
  dedicated HCM/TEP contracts.

## 14. Authorization Convention

Runtime permission namespace:

- `hcm.sensitive-access.*`

Approved concrete permissions:

- `hcm.sensitive-access.read`
- `hcm.sensitive-access.manage`
- `hcm.sensitive-access.review`
- `hcm.sensitive-access.audit.read`

Permission checks must use these non-candidate strings. `CAND-CAP-0008` and
`MOD-0314` must not appear in permission seeds, attributes, route metadata,
configuration, telemetry, database records, or test fixtures.

## 15. Gateway / API Routing Decision

Gateway routing is N/A for this first backend/API slice.

Direct Ocelot edits are not authorized. If external exposure is required after
backend/API implementation, the route must be handled only by the
integration-agent workflow. UI/frontend remains closed.

## 16. Acceptance Criteria

- AC-01: DCP-002 candidate gate passes for
  `CAND-CAP-0008` / `HR Governance & Sensitive Access Controls`.
- AC-02: Pack status is `done`; the first HCM backend/API sensitive access
  enforcement slice has been implemented and passed read-only implementation
  review.
- AC-03: The pack explicitly blocks `MOD-0314` for HR/HCM use.
- AC-04: The pack states that `CAND-CAP-0008` is governance-only and not a
  runtime literal.
- AC-05: Runtime owner/key is `hcm.sensitive-access`, and runtime literals do not
  contain `CAND-CAP-0008` or `MOD-0314`.
- AC-06: `MOD-0018` is listed as substrate only, not as HCM-native identity.
- AC-07: `MOD-0021` audit dependency is listed.
- AC-08: `CAND-CAP-0006` and `CAND-CAP-0007` are listed as HCM dependencies.
- AC-09: CAND-CAP-0007 employee projection behavior is changed only through
  `VisibilityClassification` + data-scope enforcement.
- AC-10: HCM ownership is not moved into PSS.
- AC-11: Employee master SoR ownership is explicitly out of scope.
- AC-12: Raw HRIS, secret, payroll, bank, tax, payslip, biometric, geolocation,
  national ID, DOB, home address, and PII-heavy persistence are explicitly out
  of scope.
- AC-13: `hcm.sensitive-access.*` remains the primary CAND-CAP-0008 runtime
  permission boundary and runtime owner/key remains only `hcm.sensitive-access`.
  `hcm.employee-projections.read` may be consumed only as the CAND-CAP-0007
  employee projection substrate/precondition needed to reach and evaluate the
  projection; it is not the CAND-CAP-0008 owner/key and does not grant sensitive
  access by itself.
- AC-14: `StandardHr`, `SensitiveHr`, and `RestrictedHr` behavior is covered by
  tests.
- AC-15: Data-scope unavailable behavior is fail-closed or explicitly deferred;
  it never silently grants broad access.
- AC-16: MOD-0021 unavailable behavior uses bounded local audit/deferred metadata
  and records real audit integration as a follow-up.
- AC-17: UI/frontend, Gateway direct edit, PSS implementation files, MOD-0018
  engine copy, and broad HR authorization engine are closed.
- AC-18: The module pack includes all 20 required sections.

## 17. Test Expectations

Required governance check:

```bash
python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0008 --name "HR Governance & Sensitive Access Controls"
```

Expected result:

```text
OK  candidate CAND-CAP-0008: temporary governance identity, pending EA, not Blueprint-backed, not in runtime.
```

Runtime literal scan:

```bash
rg -n "CAND-CAP-0008|MOD-0314" services/Diten.HumanCapitalService frontend gateway tests
```

Expected result: no matches.

Required implementation checks:

- Candidate gate PASS.
- Runtime literal scan PASS.
- Permission namespace checks for `hcm.sensitive-access.read`, `manage`,
  `review`, and `audit.read`.
- Employee projection read precondition check for `hcm.employee-projections.read`
  as CAND-CAP-0007 substrate only.
- `VisibilityClassification` enforcement tests.
- `StandardHr`, `SensitiveHr`, and `RestrictedHr` behavior tests.
- Data-scope unavailable fail-closed/deferred behavior tests.
- MOD-0021 unavailable local bounded audit/deferred behavior tests.
- UI/DataTable/localization verifier: N/A.
- Gateway direct edit verifier: no `gateway/**` changes.

## 18. Governance Approval Checklist

- [x] HCM governance-only domain exists.
- [x] `CAND-CAP-0008` candidate reservation exists in the registry.
- [x] DCP-002 candidate gate passes.
- [x] `MOD-0314` is documented as blocked for HR/HCM use.
- [x] `MOD-0018` is retained as RBAC/ABAC substrate only.
- [x] `MOD-0021` audit dependency is listed.
- [x] `CAND-CAP-0006` shell dependency is listed.
- [x] `CAND-CAP-0007` employee projection dependency is listed.
- [x] Runtime implementation is closed.
- [x] UI/frontend/gateway work is closed.
- [x] Employee master SoR ownership is excluded.
- [x] Raw HRIS, sensitive PII-heavy, credential, payroll, bank, tax, payslip,
  biometric, and geolocation persistence are excluded.
- [x] EA explicitly approves candidate-based governance continuation for
  `CAND-CAP-0008`.
- [x] Security owner approves the sensitive HR visibility policy taxonomy for
  governance planning.
- [x] Platform owner confirms `MOD-0018` integration pattern and no engine copy.
- [x] Audit owner confirms `MOD-0021` access-decision audit requirements for
  governance planning.
- [x] EA candidate-runtime policy waiver approved.
- [x] Runtime owner/key approved: `hcm.sensitive-access`.
- [x] Runtime permission namespace approved:
  `hcm.sensitive-access.read`, `hcm.sensitive-access.manage`,
  `hcm.sensitive-access.review`, and `hcm.sensitive-access.audit.read`.
- [x] MOD-0021 first slice blocker waived; bounded local audit/deferred metadata
  is authorized until real audit integration follow-up.
- [x] Gateway direct edit remains closed; integration-agent follow-up is required
  if future external exposure is authorized.

## 19. Implementation Notes

This pack is in review after the first HCM backend/API sensitive access
enforcement slice. Do not start frontend, direct gateway, PSS implementation
work, MOD-0018 engine copy, or broad HR authorization engine work.

The intended sequencing is:

1. Keep `CAND-CAP-0008` as the temporary governance identity for this access
   boundary.
2. Keep `MOD-0314` blocked because it is not Blueprint-backed.
3. Keep `MOD-0018` as PSS substrate only.
4. Keep `CAND-CAP-0007` employee projection behavior unchanged.
5. Implement only the minimal backend/API enforcement slice after orchestration.
6. Keep MOD-0021 real audit integration and Gateway exposure as follow-ups unless
   separately authorized.

This pack should remain a sensitive access boundary contract. It must not become
an employee master, HR lifecycle, payroll, time-attendance, RBAC engine, audit
engine, or TEP identity pack.

### Done promotion reconciliation - 2026-08-11

The first HCM backend/API sensitive access enforcement slice has been implemented
under `services/Diten.HumanCapitalService/**` and passed read-only implementation
review.

Permission drift fix completed:

- `SensitiveAccessController` evaluate endpoint now uses
  `hcm.sensitive-access.read` as the API entry gate so `StandardHr`,
  `SensitiveHr`, and `RestrictedHr` requests can reach the handler policy matrix.
- `EvaluateEmployeeProjectionSensitiveAccessHandler` remains the fail-closed
  policy decision point for `VisibilityClassification` and data-scope behavior.
- `hcm.employee-projections.read` is consumed only as the CAND-CAP-0007 employee
  projection substrate/precondition. It does not become the CAND-CAP-0008
  runtime owner/key and does not grant sensitive access by itself.
- `HasPermissionAttribute` now supports space- or comma-delimited permission
  lists in `permission`, `permissions`, and `scope` claims.

Evidence retained:

- Candidate gate PASS:
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0008 --name "HR Governance & Sensitive Access Controls"`.
- HCM API build PASS:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore`
  with `0 Warning / 0 Error`.
- SensitiveAccess targeted tests PASS:
  `dotnet test services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/Diten.HumanCapitalService.Application.Tests.csproj -c Debug --no-restore --filter SensitiveAccess`
  with `14 passed`.
- Runtime literal scan PASS:
  `rg -n "CAND-CAP-0008|MOD-0314" services/Diten.HumanCapitalService frontend gateway tests`
  returned no matches.
- Frontend, Gateway, PSS implementation scope, MOD-0018 engine copy, broad HR
  authorization engine, employee master SoR, and sensitive/raw persistence
  remained closed.

## 20. Follow-up Items

Open blockers: none for the implemented first HCM backend/API sensitive access
enforcement slice.

Follow-ups:

- EA canonical MOD assignment for `HR Governance & Sensitive Access Controls` is
  pending but is not a runtime-ready blocker under the approved candidate-runtime
  waiver.
- `CAND-CAP-0008` remains governance/documentation only and cannot be a runtime
  literal.
- `MOD-0314` is not Blueprint-backed and must not be used.
- Real MOD-0021 audit integration remains a future follow-up.
- Gateway integration-agent follow-up is required if external exposure is needed.

Waivers:

- Candidate-runtime waiver is granted only for non-candidate runtime owner/key
  `hcm.sensitive-access`.
- No employee master SoR waiver is granted.
- No sensitive-data persistence waiver is granted.
- No MOD-0018 ownership transfer waiver is granted.

Recommended next safe step:

- Prepare the next HCM/TEP governance or module-pack candidate. Any future EA
  canonical MOD assignment, real MOD-0021 integration, or Gateway exposure must
  remain a separate governance follow-up.
