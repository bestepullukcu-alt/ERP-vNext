---
id: CAND-CAP-0037
name: Compensation & Benefits Interface
domain: human-capital-management
service: Diten.HumanCapitalService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0037-compensation-benefits-interface
started: 2026-09-07
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0037
legacy_excel_id: MOD-0316
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
runtime_service_port: 5059
gateway_port: 5080
bootstrap_type: metadata-only-backend-api-readiness
runtime_owner_key: hcm.compensation-benefits
permission_namespace:
  - hcm.compensation-benefits.read
  - hcm.compensation-benefits.manage
  - hcm.compensation-benefits.evaluate
  - hcm.compensation-benefits.audit.read
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0037 - Compensation & Benefits Interface

> Status: draft. This pack records the first metadata-only backend/API
> compensation and benefits interface readiness contract slice under
> `services/Diten.HumanCapitalService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5080` -> HCM `5059`). Production compensation and benefits
> interface workflow, compensation plan, benefit program, pay grade mapping,
> benefit enrollment, compensation review, automated decision behavior,
> manager/employee compensation UX, notification/document integration, and
> sensitive persistence remain closed. This is a HIGHLY SENSITIVE money-domain
> CONSUMPTION INTERFACE over SHARED compensation, benefits, and payroll sources
> (e.g. payroll provider `MOD-0281`); it does not own, compute, or store
> compensation, benefit, or payroll data. It does NOT authorize and NEVER
> persists salary/compensation amounts, pay/wage figures, bonus/allowance
> amounts, pay grade values, benefit elections, benefit enrollment details,
> payroll runs or data, deductions, bank details, tax details, or any monetary
> value; it stores ONLY boundary/readiness STATE metadata. It does NOT authorize
> real compensation planning, pay grade assignment, benefit election capture,
> benefit enrollment execution, compensation review workflow, payroll feed,
> model output, scoring, or automated decision. `CAND-CAP-0037` remains a
> governance/documentation identity only and must not be written into runtime
> literals. `MOD-0316` remains blocked for HCM use until future EA canonical
> MOD assignment.

## 1. Module Summary

`CAND-CAP-0037` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-C capability named
`Compensation & Benefits Interface`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0316`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0037` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API compensation and
benefits interface readiness contract slice, mirroring the Time, Attendance &
Leave Interface slice (`CAND-CAP-0036` / `MOD-0315`) and the completed HR
Documentation & Evidence Workspace slice (`CAND-CAP-0035` / `MOD-0313`) exactly.
This capability is a HIGHLY SENSITIVE money-domain CONSUMPTION INTERFACE over the
shared compensation source and benefit provider source: it is metadata-only
readiness and does NOT own, compute, or store any monetary or benefit data.
Compensation and benefits interface workflow, compensation plan, benefit
program, pay grade mapping, benefit enrollment, compensation review, automated
decision behavior, manager/employee compensation UX, notification/document
integration, and sensitive persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- HCM-native compensation and benefits interface governance boundary.
- Sequencing after completed HCM employee, governance, position, offboarding,
  applicant intake, candidate pipeline, offer management, onboarding,
  employment change, performance review, competency/skills, development plan,
  learning/training, succession/high-potential, workforce planning,
  headcount/position budget planning, HR KPI/analytics facade, HR
  documentation/evidence workspace, and time/attendance/leave interface
  foundation modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only compensation and benefits interface readiness backend/API
  contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Explicit exclusion of compensation and benefits interface workflow
  execution.
- Explicit exclusion of compensation plan, benefit program, pay grade mapping,
  benefit enrollment, compensation review, model output, and automated decision
  behavior.
- Explicit exclusion of manager-facing and employee-facing compensation UX
  beyond the readiness-metadata CRUD (those fields are reserved and out of
  scope).
- Explicit exclusion of any monetary or benefit value: this interface does NOT
  own, compute, or store salary/compensation amounts, pay/wage figures,
  bonus/allowance amounts, pay grade values, benefit elections, benefit
  enrollment details, payroll runs or data, deductions, bank details, tax
  details, or any monetary value, and only boundary/readiness STATE metadata is
  persisted.
- Explicit exclusion of free-text compensation/benefit notes, compensation
  narrative, salary/compensation amount, pay/wage figure, bonus/allowance
  amount, pay grade value, benefit election, benefit enrollment detail, payroll
  data, deduction, bank detail, tax detail, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence.

Not owned by this pack:

- Real compensation and benefits interface execution, real compensation
  planning, pay grade assignment, benefit election capture, benefit enrollment
  execution, compensation review workflow execution, payroll run execution or
  persistence, deduction/tax/bank persistence, or model output.
- Compensation source ownership beyond dependency/context references.
- Benefit provider source ownership beyond dependency/context references.
- Payroll provider (`MOD-0281`) ownership beyond dependency/context references.
- Notification, notification provider, controlled document, or external
  compensation/benefit/payroll provider implementation.
- Development plan, competency/skills, performance review, learning/training,
  succession, workforce planning, headcount/budget, HR KPI/analytics facade, HR
  documentation/evidence workspace, time/attendance/leave interface, shared
  compensation source, benefit provider source, payroll provider, TEP, or PSS
  ownership transfer.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| CompensationBenefitsCapabilityBoundary | Governance boundary | Defines what a future HCM compensation and benefits interface module may own. |
| CompensationBenefitsReadinessBoundary | Governance boundary | Separates readiness metadata from compensation/benefit interface workflow execution. |
| CompensationPlanBenefitProgramBoundary | Governance boundary | Blocks compensation plan, benefit program, and pay grade mapping execution. |
| PayGradeEnrollmentBoundary | Governance boundary | Blocks benefit enrollment and compensation review execution. |
| CompensationBenefitsDecisionBoundary | Governance boundary | Blocks scoring, rating, ranking, model-output, and automated decision persistence. |
| CompensationBenefitsUxBoundary | Governance boundary | Blocks manager and employee compensation UX beyond readiness-metadata CRUD. |
| CompensationBenefitsSensitiveDataBoundary | Governance boundary | Blocks narrative, salary/compensation amount, pay/wage figure, bonus/allowance amount, pay grade value, benefit election, payroll data, deduction, bank/tax detail, credential, and PII-heavy persistence. |
| CompensationBenefitsDependencyBoundary | Governance boundary | Keeps compensation-source/benefit-provider-source/notification/document dependencies deferred and out of scope. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| CompensationBenefitsInterfaceWorkflow | Deferred runtime | Compensation and benefits interface workflow execution; not authorized. |
| CompensationPlanBenefitProgramEngine | Deferred runtime | Compensation plan, benefit program, and pay grade mapping; not authorized. |
| PayGradeEnrollmentEngine | Deferred runtime | Benefit enrollment, compensation review, and model output; not authorized. |
| ManagerEmployeeCompensationExperience | Deferred frontend/runtime | Manager/employee compensation UX beyond readiness CRUD; not authorized. |
| CompensationBenefitsDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| CompensationBenefitsNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| CompensationBenefitsReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/CompensationBenefitsReadinessMetadata.cs`. |
| CompensationBenefitsReadinessState | Domain enum | Readiness state; `Domain/Enums/CompensationBenefitsReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| ICompensationBenefitsReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/ICompensationBenefitsReadinessMetadataRepository.cs`. |
| MongoCompensationBenefitsReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `hcm_compensation_benefits_readiness`. |
| CompensationBenefits Application features | Application (CQRS/MediatR) | `Application/Features/CompensationBenefits/**` (Models/Guard/Commands/Queries/Handlers). |
| CompensationBenefitsController | API controller | Thin controller; `Api/Controllers/Hcm/CompensationBenefitsController.cs`. |
| CompensationBenefits golden-compact CRUD | Frontend | Controller + `Views/HumanCapital/CompensationBenefits/**` + `wwwroot/assets/js/HumanCapital/CompensationBenefits/**` + Resources + nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness-state fields are of type
`CompensationBenefitsReadinessState`):

- `Code`
- `DisplayName`
- `CompensationBenefitsReadinessState`
- `CompensationPlanBoundaryState`
- `BenefitProgramBoundaryState`
- `PayGradeMappingBoundaryState`
- `BenefitEnrollmentBoundaryState`
- `CompensationReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `CompensationSourceDependencyState`
- `BenefitProviderSourceDependencyState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `CompensationBenefitsReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Manager-facing compensation fields (manager compensation approval, manager
  review, manager action UX state) are reserved and out of scope.
- Employee-facing compensation fields (employee self-service input,
  employee benefit election, employee self-service UX state) are reserved and
  out of scope.
- NO money field / NO monetary attribute of any kind. This is a metadata-only
  readiness slice with no compensation/benefit content attribute and no
  monetary value. Salary/compensation AMOUNTS, pay/wage FIGURES,
  bonus/allowance AMOUNTS, pay grade VALUES, benefit ELECTIONS, benefit
  ENROLLMENT details, payroll RUNS/DATA, DEDUCTIONS, BANK details, and TAX
  details are FORBIDDEN; only boundary/readiness STATE metadata is persisted,
  never any salary/compensation amount, pay/wage figure, bonus/allowance amount,
  pay grade value, benefit election, benefit enrollment detail, payroll datum,
  deduction, bank detail, tax detail, or any monetary value.

Forbidden field classes:

- Compensation and benefits interface workflow body, compensation plan
  submission payload, benefit program payload, pay grade mapping payload,
  benefit enrollment payload, compensation review payload, approval decision
  body, action evidence, or completed compensation/benefit content.
- Readiness score, rating value, rank, compensation/benefit outcome, model
  output, automated decision result, recommendation output, or evaluator
  scoring payload.
- Free-text compensation/benefit notes, compensation narrative, manager
  comments, employee comments, HR notes, or sensitive compensation/benefit body.
- Salary/compensation amount, pay/wage figure, bonus/allowance amount, pay grade
  value, benefit election, benefit enrollment detail, payroll run/data,
  deduction, bank detail, tax detail, raw provider payload, or uploaded file
  metadata.
- Salary/compensation amount, pay/wage figure, bonus/allowance amount, pay grade
  value, benefit election, benefit enrollment detail, payroll data, deduction,
  bank detail, tax detail, score, rating, model output, or payment instruction
  fields, or any monetary value.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  PII-heavy employee/profile fields.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0037-compensation-benefits-interface.md`

Candidate future runtime service:

- `Diten.HumanCapitalService` (port `5059`)

Runtime repo scope:

- `services/Diten.HumanCapitalService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/HumanCapital/CompensationBenefits/**`,
  `wwwroot/assets/js/HumanCapital/CompensationBenefits/**`, Resources, nav entry).
- Gateway route exposure through the API gateway (`5080` -> HCM `5059`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.HumanCapitalService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned CompensationBenefits CRUD objects listed in
  §3/§5.
- `gateway/**` outside the owned CompensationBenefits readiness route.
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
- `CAND-CAP-0036` - Time, Attendance & Leave Interface, done.

TEP context dependencies:

- `CAND-CAP-0011` through `CAND-CAP-0021` remain dependency/context only.

PSS backbone context:

- `MOD-0288`, `MOD-0251`, `MOD-0279`, `MOD-0280`, and `MOD-0281` remain
  dependency/backbone context only (`MOD-0281` payroll provider is a source
  dependency).

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is
authorized only for the first metadata-only backend/API readiness slice, along
with the golden-compact readiness CRUD frontend and gateway route.

Authorized runtime boundary:

- Runtime owner/key: `hcm.compensation-benefits`.
- Permission namespace:
  - `hcm.compensation-benefits.read`
  - `hcm.compensation-benefits.manage`
  - `hcm.compensation-benefits.evaluate`
  - `hcm.compensation-benefits.audit.read`
- Runtime repo scope: `services/Diten.HumanCapitalService/**`.
- Thin controller (`Api/Controllers/Hcm/CompensationBenefitsController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `hcm_compensation_benefits_readiness`.
- Active tenant `Code` uniqueness.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice fixes the forbidden-marker matcher to use WORD/TOKEN
  boundaries (`\b` regex) instead of a bare substring `Contains`, so legitimate
  domain words (`compensation`, `benefit`, `pay`, `plan`) are not falsely
  rejected while true forbidden markers (`salary`, `wage`, `payroll`, `bank`,
  `tax`, `compensation_amount`, `benefits_election`, ...) are still caught.

Explicitly unauthorized:

- Compensation and benefits interface workflow.
- Compensation plan, benefit program, pay grade mapping, benefit enrollment,
  compensation review.
- Real compensation planning, pay grade assignment, benefit election capture,
  benefit enrollment execution, compensation review workflow execution, payroll
  run execution or persistence, deduction/tax/bank persistence, and payroll
  feed.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Manager/employee compensation UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Sensitive payload and compensation/benefit/monetary content persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- Tenant shell nav entry `Compensation & Benefits Interface`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Manager compensation UX beyond readiness-metadata CRUD.
- Employee compensation/self-service UX beyond readiness-metadata CRUD.
- Compensation plan, benefit program, pay grade mapping, benefit enrollment,
  compensation review, scoring, or model-output UI.
- Salary/compensation amount, pay/wage figure, bonus/allowance amount, pay grade
  value, benefit election, payroll, deduction, bank/tax, or any monetary UI.
- Document, notification, export, upload, or attachment UI.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> HCM `5059`). This is a HIGHLY SENSITIVE money-domain
CONSUMPTION INTERFACE that owns no compensation, benefit, or payroll runtime: it
does NOT authorize real compensation planning, pay grade assignment, benefit
election capture, benefit enrollment execution, compensation review workflow
execution, payroll run execution or persistence, deduction/tax/bank
persistence, payroll feed, or ownership of the shared compensation source /
benefit provider source / payroll provider. It does NOT authorize and NEVER
persists salary/compensation amounts, pay/wage figures, bonus/allowance amounts,
pay grade values, benefit elections, benefit enrollment details, payroll
runs/data, deductions, bank details, tax details, or any monetary value; only
boundary/readiness STATE metadata is persisted.

Authorized API surface:

- `GET /api/compensation-benefits` - list readiness metadata.
- `GET /api/compensation-benefits/{id}` - get readiness metadata by id.
- `POST /api/compensation-benefits` - create readiness metadata.
- `POST /api/compensation-benefits/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/compensation-benefits/{id}` - soft-delete.
- `GET /api/compensation-benefits/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get compensation and benefits interface readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No compensation/benefit interface workflow, compensation plan, benefit
  program, pay grade mapping, benefit enrollment, compensation review, pay grade
  assignment, benefit election capture, benefit enrollment execution, payroll
  run persistence, deduction/tax/bank persistence, payroll feed,
  scoring/model-output persistence, automated decision behavior,
  notification/document integration, or sensitive/monetary content payload
  persistence.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only compensation and benefits interface readiness state.
- Boundary-state metadata for compensation plan, benefit program, pay grade
  mapping, benefit enrollment, compensation review, automated decision,
  consent, minimization, retention, and evidence.
- Dependency-state metadata for compensation source, benefit provider source,
  document, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Compensation and benefits interface workflow payload.
- Compensation-plan/benefit-program/pay-grade-mapping/benefit-enrollment/compensation-review
  execution payload.
- Real compensation planning, pay grade assignment, benefit election capture, or
  benefit enrollment persistence.
- Salary/compensation amount, pay/wage figure, bonus/allowance amount, pay grade
  value, benefit election, benefit enrollment detail, or any monetary value.
- Compensation review workflow execution, payroll run execution or persistence,
  deduction, bank detail, tax detail, or payroll feed output.
- Readiness score/rating/rank/model output.
- Automated decision result.
- Free-text compensation/benefit notes or compensation narrative.
- Compensation review transcript payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy employee/profile details.
- Compensation/payroll/benefits payload and any monetary value.

## 12. Permission Boundary

Approved permission namespace:

- `hcm.compensation-benefits.read`
- `hcm.compensation-benefits.manage`
- `hcm.compensation-benefits.evaluate`
- `hcm.compensation-benefits.audit.read`

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
- Compensation/benefit data minimization fail-closed behavior.
- Retention/evidence/audit/enrollment/deletion as local/deferred metadata.
- Exclusion of compensation narrative, compensation/benefit/payroll payloads,
  monetary/benefit-election content, and PII-heavy data.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0281` Payroll Provider (external provider / source).

The shared payroll provider (`MOD-0281`) and compensation/benefit source
dependency are deferred and out of scope: this interface only consumes readiness
metadata and owns no compensation, benefit, or payroll runtime. No integration
implementation, provider payload persistence, or document/notification UI is
authorized.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0037`.
3. AC-03: Legacy Excel ID `MOD-0316` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `hcm.compensation-benefits`.
5. AC-05: Permission namespace is limited to `hcm.compensation-benefits.read`,
   `hcm.compensation-benefits.manage`, `hcm.compensation-benefits.evaluate`, and
   `hcm.compensation-benefits.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.HumanCapitalService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection `hcm_compensation_benefits_readiness`)
   includes active tenant `Code` uniqueness and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Compensation and benefits interface workflow, compensation plan,
    benefit program, pay grade mapping, benefit enrollment, compensation review,
    model output, and automated decision behavior remain unauthorized.
14. AC-14: Manager/employee compensation fields remain reserved/out of
    scope; no compensation/benefit content field and no monetary field is
    present.
15. AC-15: Free-text compensation/benefit notes, compensation narrative,
    salary/compensation amount, pay/wage figure, bonus/allowance amount, pay
    grade value, benefit election, benefit enrollment detail, payroll data,
    deduction, bank detail, tax detail, raw provider payload,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: HCM, TEP, and PSS dependency boundaries are recorded.
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0037|MOD-0316` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build HCM API:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `CompensationBenefits` filter
  (`Application.Tests/CompensationBenefitsTests.cs`, handler behavior; fix-absent
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
  not bare substring `Contains`, so legitimate words (`compensation`, `benefit`,
  `pay`, `plan`) are not falsely rejected while true forbidden
  markers (`salary`, `wage`, `payroll`, `bank`, `tax`, `compensation_amount`,
  `benefits_election`, ...) are still caught.
- Verify forbidden workflow/compensation-plan/benefit-program/pay-grade-mapping/benefit-enrollment/
  compensation-review/scoring/decision/sensitive/monetary-content fields are absent.
- Verify manager/employee compensation fields are reserved/absent and no
  compensation/benefit content field and no monetary field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0037|MOD-0316" services/Diten.HumanCapitalService frontend gateway tests`

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Compensation and benefits interface workflow prohibition is accepted.
- [x] Compensation plan/benefit program/pay grade mapping/benefit
  enrollment/compensation review prohibition is accepted.
- [x] Compensation planning/pay-grade assignment/benefit-election capture/benefit-enrollment-persistence
  prohibition is accepted.
- [x] Compensation review workflow/payroll run/deduction-tax-bank persistence/
  payroll-feed prohibition is accepted.
- [x] Salary/compensation amount/pay-wage figure/bonus-allowance amount/pay-grade
  value/benefit election/payroll/deduction/bank/tax/monetary prohibition is
  accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved manager/employee compensation fields and no-compensation-content-field/no-monetary-field
  constraint are accepted.
- [x] Sensitive payload and compensation/benefit/monetary content prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] HCM/TEP/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only compensation and benefits interface readiness
  contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Compensation/benefit data minimization policy.
- [x] Retention/evidence/audit/enrollment/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.HumanCapitalService/**`
  and the owned `frontend/Diten.Web/**` CompensationBenefits objects.
- Runtime owner/key remains limited to `hcm.compensation-benefits`.
- Permission namespace remains limited to `hcm.compensation-benefits.read`,
  `hcm.compensation-benefits.manage`, `hcm.compensation-benefits.evaluate`, and
  `hcm.compensation-benefits.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `CompensationSourceDependencyState`, `BenefitProviderSourceDependencyState`,
  `DocumentDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Manager/employee compensation UX fields remain reserved/out of scope
  and no compensation/benefit content field and no monetary field is present.
- TEP, PSS, and Platform scopes remain closed.
- Compensation and benefits interface workflow, compensation plan, benefit
  program, pay grade mapping, benefit enrollment, compensation review,
  compensation planning, pay grade assignment, benefit election capture, benefit
  enrollment persistence, payroll run execution or persistence,
  deduction/tax/bank persistence, payroll feed, model output, automated decision
  behavior, notification/document integration, free-text compensation/benefit
  notes, compensation narrative, salary/compensation amount, pay/wage figure,
  bonus/allowance amount, pay grade value, benefit election, raw provider
  payload, credential/token/secret/password, compensation/benefit/monetary
  content, and PII-heavy persistence remain out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0037 --name "Compensation & Benefits Interface"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0037`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Compensation/benefit data minimization fail-closed policy approved.
- Retention/evidence/audit/enrollment/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0037|MOD-0316" services/Diten.HumanCapitalService frontend gateway tests`

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real compensation and benefits interface workflow.
- Compensation plan and benefit program.
- Pay grade mapping, benefit enrollment, and compensation review governance.
- Automated decision approval.
- Manager/employee compensation UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
