---
id: DCP-009
slug: hcm-runtime-authorization
name: HCM Runtime Authorization and Service Scaffold Decision Pack
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: human-capital-management
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner
branch: feature/governance/hris-source-readiness
created: 2026-08-10
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
production_implementation_authorized: false
service_scaffold_authorized: true
runtime_authorization_authorized: true
---

# DCP-009 - HCM Runtime Authorization and Service Scaffold Decision Pack

> **Artifact type:** This is a Delivery Capability Pack. It is not a runtime
> entity, not a product module, not a service scaffold, and not a substitute for
> the member module packs required by `module-pack-standard.md`.

> **Execution authorization:** approved for service scaffold governance and
> runtime authorization reconciliation only. This pack authorizes
> `Diten.HumanCapitalService` scaffold completion and closes the
> `CAND-CAP-0007` runtime-ready decision gate through an EA candidate-runtime
> waiver. It does not start production implementation, frontend work, Gateway
> route changes, or business-module orchestration by itself.

## 1. Identity and status

| Field | Value |
|---|---|
| ID | `DCP-009` |
| Name | HCM Runtime Authorization and Service Scaffold Decision Pack |
| Type | Delivery Capability Pack |
| Standard | `CAP-001` |
| Status | `approved` |
| Owner domain | `human-capital-management` |
| Candidate runtime service | `Diten.HumanCapitalService` |
| Production implementation | Not authorized by this DCP |
| Service scaffold | Authorized and completed |
| Runtime authorization | Authorized for `CAND-CAP-0007` ready-for-dev promotion |
| Runtime/API/frontend/gateway/database changes | Runtime API/database only after a separate ready-for-dev module-pack orchestration; frontend/gateway remain closed |
| Source roadmap | `DCP-008` |
| Related HCM shell pack | `CAND-CAP-0006` |
| First runtime slice candidate | `CAND-CAP-0007` |

## 2. Business outcome

Define the governance gates required before native HCM runtime work begins:
service scaffold authorization, repo scope, port assignment, runtime owner/key
policy, permission namespace, privacy/security boundary, dependency validation,
and the first runtime slice decision.

The expected business outcome is a safe transition from HCM governance packs to
runtime implementation without moving HR/HCM ownership into PSS, without using
blocked Excel IDs, and without leaking candidate identities or sensitive HR data
into runtime surfaces.

## 3. Problem statement

HCM has governance-only domain bootstrap and approved governance module packs
for:

- `CAND-CAP-0006` - HR Capability Block Shell.
- `CAND-CAP-0007` - Employee Profile & Employment Record Projection.

This DCP authorizes the completed service scaffold decision and reconciles the
runtime authorization decisions for the first Employee Profile projection slice.
Production implementation still requires a separate ready-for-dev module pack and
explicit orchestration because:

- `services/Diten.HumanCapitalService/**` scaffold-only follow-up is completed.
- `CAND-CAP-0006` and `CAND-CAP-0007` are governance-only identities and cannot
  be runtime literals.
- `MOD-0297` and `MOD-0298` are deprecated platform aliases and must not be
  moved into HCM.
- Runtime owner/key, repo scope, permission namespace, minimal field list,
  data-scope policy, and validation behavior are approved by this reconciliation
  for the first `CAND-CAP-0007` backend/API slice only.

## 4. Capability boundary

In boundary:

- Governance decision for whether to authorize `Diten.HumanCapitalService`
  scaffold.
- Proposed HCM runtime repo scope.
- Proposed HCM service port.
- Proposed runtime permission namespace for the first employee projection slice.
- Candidate-ID runtime-literal prevention policy.
- Candidate-runtime policy waiver decision or canonical MOD requirement.
- Privacy/security boundary for the first runtime slice.
- Validation-contract gate for `MOD-0251` and `MOD-0288-FU02`.
- Gateway ownership decision for future external exposure.

Out of boundary:

- Creating `services/Diten.HumanCapitalService/**` in this pack; scaffold
  creation requires the separate scaffold prompt.
- Creating API controllers, commands, validators, repositories, or Mongo indexes.
- Creating frontend pages, menus, JavaScript, DataTables, or RESX.
- Editing Gateway `ocelot.json`.
- Implementing employee master SoR, lifecycle logic, payroll/time ownership, or
  provider adapters.
- Writing `CAND-CAP-0006`, `CAND-CAP-0007`, `MOD-0297`, or `MOD-0298` into
  runtime literals.

## 5. Member modules and follow-ups

| ID | Name | Type | Current status | Role in this DCP |
|---|---|---|---|---|
| `CAND-CAP-0006` | HR Capability Block Shell | HCM governance module pack | approved / governance-only | HCM shell and R1 sequencing boundary. |
| `CAND-CAP-0007` | Employee Profile & Employment Record Projection | HCM module pack | review / implemented first backend/API slice | First runtime slice implemented and ready for completion review. |
| `MOD-0251` | HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR] | PSS substrate | implemented first slice | HRIS source/profile reference dependency. |
| `MOD-0288-FU02` | Person Reference Directory Projection | PSS substrate | implemented first slice | Person reference dependency. |
| `Diten.HumanCapitalService` | HCM runtime service candidate | Service scaffold decision | authorized for scaffold-only follow-up | Proposed runtime host for native HCM. |

Follow-up module packs:

- Service scaffold follow-up for `Diten.HumanCapitalService` under scaffold-only
  scope.
- Runtime-ready promotion pack for `CAND-CAP-0007` after identity/runtime-policy,
  scaffold, repo scope, permission, validation, and privacy decisions are closed.
- Future HCM packs for HR Governance & Sensitive Access Controls and Position &
  Organization Assignment after EA identity decisions.

## 6. Ownership map

| Area | Owner | Decision |
|---|---|---|
| Native HCM runtime ownership | `human-capital-management` | Approved for first employee projection backend/API slice only. |
| Runtime service scaffold | `Diten.HumanCapitalService` | Authorized and completed. |
| HRIS source ownership | `platform-shared-services` / `MOD-0251` | Remains PSS substrate. |
| Person reference projection | `platform-shared-services` / `MOD-0288-FU02` | Remains PSS substrate. |
| HCM shell governance | `CAND-CAP-0006` | Approved governance-only. |
| Employee projection governance | `CAND-CAP-0007` | Approved governance-only. |
| Gateway routes | integration-agent | Direct edits forbidden. |
| Permission namespace approval | EA / security-owner / hcm-domain-owner | Approved for `hcm.employee-projections.*`. |
| Privacy/security minimal field approval | security-owner / legal-owner / hcm-domain-owner | Approved for the listed minimal projection fields only. |

## 7. Dependency graph

```text
DCP-002 canonicalization
  -> CAND-CAP-0006 governance shell
  -> CAND-CAP-0007 employee projection governance
  -> DCP-009 runtime authorization decision
      -> Diten.HumanCapitalService scaffold decision
      -> runtime owner/key policy decision
      -> permission namespace decision
      -> privacy/security field boundary
      -> MOD-0251 same-tenant source validation
      -> MOD-0288-FU02 same-tenant person validation
      -> CAND-CAP-0007 runtime-ready module pack
      -> integration-agent gateway task if external exposure is needed
```

## 8. Ordered delivery sequence

1. Treat this DCP as approved for scaffold governance and runtime authorization
   reconciliation.
2. Keep service port `5059` for `Diten.HumanCapitalService`.
3. Use the EA candidate-runtime waiver until a canonical MOD migration decision
   is recorded.
4. Promote `CAND-CAP-0007` module pack to ready-for-dev for the first
   provider-neutral backend/API slice only.
5. Keep `CAND-CAP-0007`, `MOD-0298`, `CAND-CAP-0006`, and `MOD-0297` out of
   runtime literals.
6. Delegate gateway routing to integration-agent only if external exposure is
   required by the runtime pack.

## 9. Prerequisites

- `CAND-CAP-0006` governance pack is approved.
- `CAND-CAP-0007` governance pack is approved.
- DCP-002 candidate gates pass for `CAND-CAP-0006` and `CAND-CAP-0007`.
- `MOD-0297` and `MOD-0298` remain blocked for HCM use.
- `Diten.HumanCapitalService` scaffold-only follow-up is completed.
- `CAND-CAP-0007` runtime-ready module pack must carry the closed waiver,
  permission, field, validation, and scope decisions before implementation starts.
- PSS substrate modules `MOD-0251` and `MOD-0288-FU02` remain owned by PSS.

## 10. Architecture decisions

| Decision | Draft recommendation | Status |
|---|---|---|
| Authorize HCM service scaffold? | Yes, scaffold-only authorization granted. | Approved |
| Runtime service name | `Diten.HumanCapitalService` | Approved for scaffold-only |
| Service port candidate | `5059`, after Auth `5056`, Platform `5057`, DevEnablement `5058` | Approved |
| Scaffold-only repo scope | `services/Diten.HumanCapitalService/**`; matching test project scaffold path if needed; solution/project registration if required; AGENTS.md service/port list update if needed | Approved for scaffold-only |
| Runtime repo scope | `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/**`; `Application/**`; `Domain/**`; `Infrastructure/**`; `Persistence/**`; `tests/Diten.HumanCapitalService.Application.Tests/**` | Approved for first backend/API slice |
| Scaffold completion | `services/Diten.HumanCapitalService/**` scaffold completed; business runtime not implemented | Reconciled |
| Frontend scope | Out of first runtime slice | Approved |
| Gateway scope | integration-agent only, if external exposure is required | Approved |
| First runtime slice | Employee Profile & Employment Record Projection | Approved |
| Permission namespace | `hcm.employee-projections.*` with `read`, `manage`, `archive`, `source-link.manage` | Approved |
| Runtime owner/key | Candidate-runtime waiver approved with non-candidate key `hcm.employee-projections` | Approved |
| Candidate IDs in runtime | Forbidden | Required |
| Employee master SoR | Out of first slice | Required |
| PII-heavy/raw provider payload persistence | Forbidden | Required |

## 11. Scope

Approved scaffold-governance scope:

- Authorize HCM service scaffold preparation.
- Define the repo paths a later scaffold-only task may touch.
- Define the first runtime slice boundary.
- Define permission namespace and HR-sensitive access requirements.
- Define minimal field and sensitive-data exclusions.
- Define validation contract requirements against PSS substrate.
- Define runtime identity policy so candidate IDs never appear in code.

## 12. Explicit exclusions

- Production code.
- Service scaffold creation in this pack.
- API/controller/command/validator/repository/index/database work.
- Frontend/menu/Razor/JavaScript/DataTable/RESX work.
- Direct gateway edits.
- Provider-specific HRIS adapters or background sync.
- Employee master SoR ownership.
- Employment lifecycle processing.
- Payroll/time-attendance ownership.
- Raw HRIS/provider payload persistence.
- Credential/token/secret persistence.
- Payroll, bank, tax, payslip, biometric, geolocation, national ID, DOB, home
  address, or PII-heavy profile persistence.
- Runtime literals containing `CAND-CAP-0006`, `CAND-CAP-0007`, `MOD-0297`, or
  `MOD-0298` for HCM ownership.

## 13. Governance drift risks

- Treating governance-only `approved` module packs as runtime-ready.
- Creating `Diten.HumanCapitalService` without an explicit scaffold decision.
- Writing candidate IDs into runtime permissions, routes, telemetry, job names,
  database records, tests, or audit owner fields.
- Moving `MOD-0251` or `MOD-0288-FU02` ownership from PSS into HCM.
- Expanding the first employee projection slice into employee master SoR.
- Persisting sensitive HR or raw HRIS data before privacy/security approval.
- Editing gateway routes outside integration-agent ownership.

## 14. Review questions

| Question | Owner | Required before |
|---|---|---|
| Is `Diten.HumanCapitalService` scaffold authorized? | EA / platform-team | Service scaffold prompt |
| Is port `5059` acceptable for HCM service? | EA / platform-team | Scaffold prompt |
| Is runtime allowed before canonical MOD assignment? | EA / registry owner | Runtime-ready module pack |
| If yes, what non-candidate runtime owner/key is approved? | EA / registry owner | Runtime-ready module pack |
| Is `hcm.employee-projections.*` approved for runtime permissions? | security-owner / hcm-domain-owner | Runtime-ready module pack |
| What minimal employee projection fields are allowed? | security-owner / legal-owner | Runtime-ready module pack |
| Are `MOD-0251` and `MOD-0288-FU02` validation contracts sufficient? | PSS owner / HCM owner | Runtime-ready module pack |
| Is gateway exposure needed for first slice? | HCM owner / integration-agent | Runtime-ready module pack |

## 15. Gate criteria

This DCP is approved for scaffold governance and runtime authorization
reconciliation because:

- Scaffold authorization decision is explicitly recorded.
- Scaffold-only repo scope is approved.
- Port candidate `5059` is accepted.
- Runtime identity policy is closed through the candidate-runtime waiver.
- Permission namespace is approved for the first backend/API slice.
- Minimal field list and privacy/security boundary are approved for projection
  metadata only.
- PSS dependency validation behavior is approved: validate through available
  contracts/repositories; otherwise use explicit `Deferred` state or fail-closed
  `404`.
- Gateway ownership remains integration-agent only.

Runtime module work may start only when:

- This DCP is `approved` for runtime authorization reconciliation.
- The relevant module pack is `ready-for-dev`.
- `Diten.HumanCapitalService` scaffold exists.
- Candidate IDs and blocked legacy IDs are absent from runtime literals.

## 16. Acceptance criteria

- AC-01: DCP status is `approved` for scaffold authorization and runtime
  authorization reconciliation.
- AC-02: No production implementation is started or directly authorized by this
  DCP without a ready-for-dev module pack and orchestration prompt.
- AC-03: Service scaffold authorization was used only for scaffold creation; no
  business runtime was created.
- AC-04: `Diten.HumanCapitalService` scaffold decision checklist is included.
- AC-05: Scaffold-only repo scope is included.
- AC-06: Port `5059` is approved.
- AC-07: Permission namespace proposal is included.
- AC-08: Candidate-ID runtime-literal prevention policy is included.
- AC-09: Runtime blockers are reconciled and the candidate-runtime waiver is
  explicit.
- AC-10: First runtime module-pack promotion conditions are closed for
  `CAND-CAP-0007`.
- AC-11: `MOD-0297` and `MOD-0298` remain blocked for HCM use.
- AC-12: PSS dependencies remain substrate only.
- AC-13: Privacy/security boundary excludes raw payloads and PII-heavy data.
- AC-14: Gateway work is assigned to integration-agent only if needed.
- AC-15: All 20 CAP-001 sections are present.

## 17. Downstream business-module impacts

- `CAND-CAP-0007` can become ready-for-dev for the first provider-neutral
  backend/API slice after its module pack records this DCP's closed decisions.
- Future HCM modules must consume the HCM service boundary rather than
  implementing native HCM under PSS.
- TEP must wait for HCM foundation contracts where talent/candidate identity
  depends on employee/employment projection.
- Position/organization assignment must continue to consume `MOD-0288` /
  `MOD-0288-FU02` instead of duplicating directory ownership.
- Sensitive HR access policy must be reusable by offboarding, employee relations,
  HR case management, and HR analytics facades.

## 18. Open decisions

### Scaffold authorization decision checklist

- [x] Authorize `Diten.HumanCapitalService` scaffold.
- [x] Approve service port `5059`.
- [x] Approve scaffold-only repo scope:
  `services/Diten.HumanCapitalService/**`, matching test project scaffold path
  if needed, solution/project registration if required, and AGENTS.md
  service/port list update if needed.
- [x] Confirm no frontend/gateway/database implementation in scaffold step.
- [x] Confirm AGENTS/domain config updates required for service list and port
  only after scaffold authorization.

### Runtime authorization reconciliation checklist

- [x] EA candidate-runtime policy waiver approved.
- [x] Runtime owner/key approved: `hcm.employee-projections`.
- [x] Runtime repo scope approved under `services/Diten.HumanCapitalService/**`
  for the first backend/API slice.
- [x] Runtime permission namespace approved: `hcm.employee-projections.*`.
- [x] Runtime permissions approved:
  `hcm.employee-projections.read`,
  `hcm.employee-projections.manage`,
  `hcm.employee-projections.archive`, and
  `hcm.employee-projections.source-link.manage`.
- [x] `MOD-0251` same-tenant validation behavior approved: validate through
  available contract/repository; otherwise use explicit `Deferred` state or
  fail-closed `404`.
- [x] `MOD-0288-FU02` same-tenant validation behavior approved: validate through
  available contract/repository; otherwise use explicit `Deferred` state or
  fail-closed `404`.
- [x] Minimal employee/employment projection field list approved.
- [x] Privacy/security approval granted for the minimal projection fields only.
- [x] Sensitive HR visibility/data-scope policy approved with mandatory
  `VisibilityClassification`.
- [x] Gateway direct edit remains closed; integration-agent follow-up only if
  external exposure is needed.

### Candidate-runtime policy waiver option

EA allows runtime before canonical MOD allocation under this waiver:

- `CAND-CAP-0007` remains documentation/governance only.
- Runtime owner/key may be `hcm.employee-projections` or another approved
  non-candidate string.
- Runtime permission prefix may be `hcm.employee-projections.*`.
- No runtime route, seed, job, telemetry, audit owner, database record, test
  fixture, or config key may contain `CAND-CAP-0007`.
- The waiver expires when EA assigns canonical MOD and a migration decision is
  recorded.

Approved minimal projection fields:

- `Code`
- `DisplayName`
- `HrisSourceProfileId`
- `PersonReferenceId`
- `ExternalEmployeeReference`
- `EmploymentRecordReferenceKey`
- `EmploymentStatusCode`
- `WorkerTypeCode`
- `SourceContractVersion`
- `ProjectionState`
- `VisibilityClassification`
- `SourceLastSyncedAt`
- `ProjectionVersion`

## 19. Future follow-ups

Recommended follow-ups after this approval:

1. Promote `CAND-CAP-0007` to ready-for-dev for the first provider-neutral
   backend/API slice.
2. Start production implementation only through a later `@orchestrator` prompt
   that references the ready-for-dev module pack.
3. Prepare HR Governance & Sensitive Access Controls identity reservation and
   module pack before broadening HCM runtime beyond employee projection.
4. Prepare Position & Organization Assignment identity decision; do not use
   `MOD-0299`.

## 20. Audit and reconciliation notes

Evidence used:

- Initial authorization evidence: `AGENTS.md` recorded
  `Diten.HumanCapitalService` as not scaffolded and requires
  approved/ready-for-dev module packs before development.
- `human-capital-management/domain-config.md` states HCM bootstrap is
  governance-only and production scaffold is not authorized.
- `CAND-CAP-0006` is approved for governance continuation only and not
  ready-for-dev.
- `CAND-CAP-0007` was approved for governance continuation only before this
  reconciliation and is now eligible for ready-for-dev promotion through the
  candidate-runtime waiver recorded below.
- `DCP-002` states candidate IDs are governance/documentation identities only
  and never runtime literals.
- `module-id-registry.md` retains `MOD-0297` and `MOD-0298` as deprecated
  platform aliases and records `CAND-CAP-0006` / `CAND-CAP-0007` as HCM
  temporary candidate identities.

No production business code, frontend, gateway, runtime/API, database
collections, or business-module test file is authorized by this approved DCP.
Service scaffold creation is authorized only as a separate scaffold-only
follow-up.

### Scaffold completion reconciliation - 2026-08-10

The `Diten.HumanCapitalService` scaffold-only follow-up consumed the scaffold
authorization granted by this DCP and is recorded as completed.

Completed scaffold scope:

- `services/Diten.HumanCapitalService/**` service scaffold created.
- `Diten.HumanCapitalService.sln` created for solution/project registration.
- API/Application/Domain/Infrastructure/Persistence project skeletons created.
- Scaffold wiring test project created.
- `AGENTS.md` service list, port table, and build command updated for
  `Diten.HumanCapitalService` on port `5059`.

Verification recorded:

- HCM scaffold API build PASS:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore`
- HCM scaffold solution build PASS:
  `dotnet build services/Diten.HumanCapitalService/Diten.HumanCapitalService.sln -c Debug --no-restore`
- HCM scaffold tests PASS:
  `dotnet test services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/Diten.HumanCapitalService.Application.Tests.csproj -c Debug --no-restore`
- Candidate/blocked ID runtime literal scan PASS:
  `rg -n "CAND-CAP-0006|CAND-CAP-0007|MOD-0297|MOD-0298" services/Diten.HumanCapitalService`
  returned no matches.

Runtime authorization reconciliation:

- `production_implementation_authorized: false`.
- `runtime_authorization_authorized: true`.
- Employee Profile runtime implementation is authorized only after
  `CAND-CAP-0007` is updated to ready-for-dev and a separate implementation
  prompt is issued.
- Frontend and direct gateway work remain closed.

### CAND-CAP-0007 runtime authorization reconciliation - 2026-08-10

EA, security, platform, and HCM ownership decisions are recorded as closed for
the first provider-neutral backend/API slice:

- Candidate-runtime waiver approved; `CAND-CAP-0007` remains
  governance/documentation only and is never a runtime literal.
- `MOD-0298` remains blocked for HCM and is never a runtime literal.
- Runtime owner/key: `hcm.employee-projections`.
- Runtime repo scope: `services/Diten.HumanCapitalService/**` first backend/API
  slice paths only.
- Permission namespace: `hcm.employee-projections.*`.
- Minimal projection field list and sensitive HR visibility policy approved.
- `MOD-0251` and `MOD-0288-FU02` validation behavior: validate when
  contract/repository is available; otherwise use explicit `Deferred` state or
  fail-closed `404`.
- UI/frontend closed; Gateway direct edit closed and remains integration-agent
  follow-up if external exposure is required.

### CAND-CAP-0007 implementation completion reconciliation - 2026-08-10

The first provider-neutral backend/API slice for Employee Profile & Employment
Record Projection was implemented under `services/Diten.HumanCapitalService/**`
and is recorded as ready for review.

Completed scope:

- Minimal employee/employment projection backend/API contract.
- Thin controller + MediatR/CQRS + `Response<T>` envelope.
- Runtime owner/key `hcm.employee-projections`.
- Tenant server-side resolution and tenant-aware fail-closed behavior.
- Soft delete with `IsDeleted + DeletedAt`.
- Mongo-backed production persistence for `IEmployeeProjectionRepository`.
- Mongo collection: `hcm_employee_profile_projections`.
- Tenant-aware active `Code` unique index:
  `ux_hcm_employee_projections_tenant_code_active`.
- MOD-0251 / MOD-0288-FU02 reference behavior: validate same tenant when a
  contract/repository exists; otherwise use explicit `Deferred` state or
  fail-closed `404`.
- Validators reject forbidden raw/sensitive markers.

Closed blocker:

- The persistence blocker is closed. Production DI no longer uses an in-memory
  employee projection repository; test-only in-memory behavior is confined to
  the HCM test project.

Verification recorded:

- Build PASS:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore`
  returned 0 warnings and 0 errors.
- Targeted HCM tests PASS:
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
- Candidate ID runtime literal prohibition remains enforced:
  `CAND-CAP-0007` is governance/documentation only.
- `MOD-0298` remains blocked for HCM and is not a runtime literal.
- Employee master SoR, HR lifecycle, provider adapters, raw HRIS/provider
  payloads, credentials, payroll/bank/tax/payslip/biometric/geolocation,
  national ID, DOB, home address, and PII-heavy persistence remain out of scope.
- Review governance drift fixed: CAND-CAP-0007 member status now reflects
  `review / implemented first backend/API slice`, while candidate/legacy runtime
  literal prohibition, frontend/gateway/PSS closure, and build/test/literal
  scan/in-memory scan evidence remain intact.

## Next safe prompt

For CAND-CAP-0007 implementation review, use:

```text
/read-only-audit

Review CAND-CAP-0007 Employee Profile & Employment Record Projection completed
backend/API first slice.
Kod yazma. Dosya değiştirme.

Sources:
- execution/domains/human-capital-management/module-packs/CAND-CAP-0007-employee-profile-employment-record-projection.md
- execution/portfolio/delivery-capability-packs/DCP-009-hcm-runtime-authorization.md
- services/Diten.HumanCapitalService/**

Check:
- Mongo-backed persistence and tenant-aware indexes.
- Candidate/legacy IDs absent from runtime literals.
- Tenant isolation, soft delete, duplicate code, validation, permission, and
  sensitive-data tests.
- Frontend/gateway/PSS scope remained closed.

Output:
- Review findings.
- PASS/BLOCKED decision for promoting CAND-CAP-0007 from review to done.
```
