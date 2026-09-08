---
id: CAND-CAP-0040
name: HR Compliance & Statutory Reporting
domain: human-capital-management
service: Diten.HumanCapitalService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0040-hr-compliance-statutory-reporting
started: 2026-09-07
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0040
legacy_excel_id: MOD-0318
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
runtime_service_port: 5059
gateway_port: 5080
bootstrap_type: metadata-only-backend-api-readiness
runtime_owner_key: hcm.hr-compliance
permission_namespace:
  - hcm.hr-compliance.read
  - hcm.hr-compliance.manage
  - hcm.hr-compliance.evaluate
  - hcm.hr-compliance.audit.read
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0040 - HR Compliance & Statutory Reporting

> Status: draft. This pack records the first metadata-only backend/API
> HR compliance & statutory reporting readiness contract slice under
> `services/Diten.HumanCapitalService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5080` -> HCM `5059`). Production HR compliance / statutory reporting
> execution, obligation catalog, control mapping, statutory report definition,
> filing schedule, attestation closure, automated decision behavior,
> compliance UX, notification/document integration, and
> report/filing-content persistence remain closed. This is a compliance /
> statutory-reporting READINESS facade over SHARED backing HCM data sources and
> shared regulatory reference data; it does not own, compute, generate, or store
> compliance reports or statutory filings, and stores NO report content. It
> does NOT authorize and NEVER persists report bodies, statutory filing content,
> generated report data, exported datasets, findings, control-test results,
> attestation content, narratives, notes/comments, or model output; it stores
> ONLY boundary/readiness STATE metadata. It does NOT authorize real compliance
> obligation execution, control testing, statutory report generation/rendering/
> export, regulatory filing/submission, e-filing, attestation execution, report
> content/dataset persistence, model output, scoring, or automated decision.
> `CAND-CAP-0040` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0318` remains blocked for HCM use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0040` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-C capability named
`HR Compliance & Statutory Reporting`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0318`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0040` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API HR compliance &
statutory reporting readiness contract slice, mirroring the Employee Relations &
HR Case Management slice (`CAND-CAP-0039` / `MOD-0317`) and the completed
Employee / Manager Self-Service Channel slice (`CAND-CAP-0038` / `MOD-0319`)
exactly. This capability is a compliance / statutory-reporting READINESS facade
over the shared backing HCM data sources and shared regulatory reference data:
it is metadata-only readiness and does NOT own, compute, generate, or store any
compliance report or statutory filing. HR compliance / statutory reporting
execution, obligation catalog, control mapping, statutory report definition,
filing schedule, attestation closure, automated decision behavior, compliance
UX, notification/document integration, and report/filing-content persistence
remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- HCM-native HR compliance & statutory reporting governance boundary.
- Sequencing after completed HCM employee, governance, position, offboarding,
  applicant intake, candidate pipeline, offer management, onboarding,
  employment change, performance review, competency/skills, development plan,
  learning/training, succession/high-potential, workforce planning,
  headcount/position budget planning, HR KPI/analytics facade, HR
  documentation/evidence workspace, time/attendance/leave interface,
  compensation & benefits interface, employee / manager self-service channel, and
  employee relations & HR case management foundation modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only HR compliance & statutory reporting readiness backend/API
  contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Explicit exclusion of HR compliance / statutory reporting execution.
- Explicit exclusion of obligation catalog, control mapping, statutory report
  definition, filing schedule, attestation closure, model output, and automated
  decision behavior.
- Explicit exclusion of compliance UX beyond the readiness-metadata CRUD (those
  fields are reserved and out of scope).
- Explicit exclusion of any report content: this module does NOT own,
  compute, generate, or store report bodies, statutory filing content, generated
  report data, exported datasets, findings, control-test results, attestation
  content, or any report/filing detail, and only boundary/readiness STATE
  metadata is persisted.
- Explicit exclusion of free-text compliance notes, report narrative,
  report body, statutory filing content, generated report data, exported
  dataset, finding, control-test result, attestation content, raw provider
  payload, credential/token/secret/password, and PII-heavy persistence.

Not owned by this pack:

- Real HR compliance / statutory reporting execution, real compliance obligation
  execution, control testing, statutory report generation/rendering/export,
  regulatory filing/submission, e-filing, attestation execution, report
  content/dataset persistence, or model output.
- Backing HCM data source ownership beyond dependency/context references.
- Regulatory reference-data / regulatory source ownership beyond
  dependency/context references.
- Employee Relations & HR Case Management (`CAND-CAP-0039`) ownership beyond
  dependency/context references.
- Notification, notification provider, controlled document, or external
  compliance/regulatory/filing provider implementation.
- Development plan, competency/skills, performance review, learning/training,
  succession, workforce planning, headcount/budget, HR KPI/analytics facade, HR
  documentation/evidence workspace, time/attendance/leave interface,
  compensation & benefits interface, self-service channel, employee relations &
  HR case management, backing HCM data sources, regulatory reference data, TEP,
  or PSS ownership transfer.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| HrComplianceCapabilityBoundary | Governance boundary | Defines what a future HCM HR compliance & statutory reporting module may own. |
| HrComplianceReadinessBoundary | Governance boundary | Separates readiness metadata from HR compliance / statutory reporting execution. |
| ObligationControlBoundary | Governance boundary | Blocks obligation catalog, control mapping, and statutory report definition execution. |
| StatutoryReportFilingBoundary | Governance boundary | Blocks filing schedule and attestation closure execution. |
| HrComplianceDecisionBoundary | Governance boundary | Blocks scoring, rating, ranking, model-output, and automated decision persistence. |
| HrComplianceUxBoundary | Governance boundary | Blocks compliance UX beyond readiness-metadata CRUD. |
| HrComplianceSensitiveDataBoundary | Governance boundary | Blocks report body, statutory filing content, generated report data, exported dataset, finding, control-test result, attestation content, narrative, credential, and PII-heavy persistence. |
| HrComplianceDependencyBoundary | Governance boundary | Keeps regulatory-source/HCM-data-source/notification/document dependencies deferred and out of scope. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| HrComplianceWorkflow | Deferred runtime | HR compliance / statutory reporting execution; not authorized. |
| ObligationControlEngine | Deferred runtime | Obligation catalog, control mapping, and statutory report definition; not authorized. |
| StatutoryReportFilingEngine | Deferred runtime | Filing schedule, attestation closure, and model output; not authorized. |
| ComplianceReportingExperience | Deferred frontend/runtime | Compliance UX beyond readiness CRUD; not authorized. |
| HrComplianceDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| HrComplianceNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| HrComplianceReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/HrComplianceReadinessMetadata.cs`. |
| HrComplianceReadinessState | Domain enum | Readiness state; `Domain/Enums/HrComplianceReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IHrComplianceReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/IHrComplianceReadinessMetadataRepository.cs`. |
| MongoHrComplianceReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `hcm_hr_compliance_readiness`. |
| HrCompliance Application features | Application (CQRS/MediatR) | `Application/Features/HrCompliance/**` (Models/Guard/Commands/Queries/Handlers). |
| HrComplianceController | API controller | Thin controller; `Api/Controllers/Hcm/HrComplianceController.cs`. |
| HrCompliance golden-compact CRUD | Frontend | Controller + `Views/HumanCapital/HrCompliance/**` + `wwwroot/assets/js/HumanCapital/HrCompliance/**` + Resources + nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness-state fields are of type
`HrComplianceReadinessState`):

- `Code`
- `DisplayName`
- `HrComplianceReadinessState`
- `ObligationCatalogBoundaryState`
- `ControlMappingBoundaryState`
- `StatutoryReportDefinitionBoundaryState`
- `FilingScheduleBoundaryState`
- `AttestationClosureBoundaryState`
- `AutomatedDecisionBoundaryState`
- `RegulatorySourceDependencyState`
- `HcmDataSourceDependencyState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `HrComplianceReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Compliance-officer-facing fields (control-testing UX, statutory report
  authoring, filing-submission UX state) are reserved and out of scope.
- Reviewer-facing fields (attestation input, exception submission, reporting UX
  state) are reserved and out of scope.
- Reserved compliance UX fields, no report-content field, no money field,
  no PII-heavy field. This is a metadata-only readiness slice with no
  report-content attribute, no monetary value, and no PII-heavy attribute. REPORT
  BODIES, STATUTORY FILING content, GENERATED REPORT data, EXPORTED datasets,
  FINDINGS, CONTROL-TEST results, ATTESTATION content, and REPORT narratives are
  FORBIDDEN; only boundary/readiness STATE metadata is persisted, never any
  report body, statutory filing content, generated report data, exported
  dataset, finding, control-test result, attestation content, narrative, or any
  report content.

Forbidden field classes:

- HR compliance / statutory reporting workflow body, obligation catalog payload,
  control mapping payload, statutory report definition payload, filing schedule
  payload, attestation closure payload, statutory filing body, report evidence,
  or completed report content.
- Readiness score, rating value, rank, compliance outcome, model output,
  automated decision result, recommendation output, or evaluator scoring
  payload.
- Free-text compliance notes, report narrative, reviewer comments,
  officer comments, compliance notes, or sensitive report body.
- Report body, statutory filing content, generated report data, exported
  dataset, finding, control-test result, attestation content, narrative, raw
  provider payload, or uploaded file metadata.
- Report body, statutory filing content, generated report data, exported
  dataset, finding, control-test result, attestation content, score, rating,
  model output, or report-content fields, or any monetary value.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  PII-heavy employee/report fields.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0040-hr-compliance-statutory-reporting.md`

Candidate future runtime service:

- `Diten.HumanCapitalService` (port `5059`)

Runtime repo scope:

- `services/Diten.HumanCapitalService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/HumanCapital/HrCompliance/**`,
  `wwwroot/assets/js/HumanCapital/HrCompliance/**`, Resources, nav entry).
- Gateway route exposure through the API gateway (`5080` -> HCM `5059`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.HumanCapitalService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned HrCompliance CRUD objects listed in §3/§5.
- `gateway/**` outside the owned HrCompliance readiness route.
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
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, done. Key
  governance dependency for this sensitive HR compliance / statutory reporting
  facade: obligation catalog, control mapping, statutory report definition,
  filing schedule, and attestation closure readiness consume the shared HCM data
  source under the governing HR sensitive-access policy.
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
- `CAND-CAP-0037` - Compensation & Benefits Interface, done.
- `CAND-CAP-0038` - Employee / Manager Self-Service Channel, done.
- `CAND-CAP-0039` - Employee Relations & HR Case Management, done.

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

- Runtime owner/key: `hcm.hr-compliance`.
- Permission namespace:
  - `hcm.hr-compliance.read`
  - `hcm.hr-compliance.manage`
  - `hcm.hr-compliance.evaluate`
  - `hcm.hr-compliance.audit.read`
- Runtime repo scope: `services/Diten.HumanCapitalService/**`.
- Thin controller (`Api/Controllers/Hcm/HrComplianceController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `hcm_hr_compliance_readiness`.
- Active tenant `Code` uniqueness.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice fixes the forbidden-marker matcher to use WORD/TOKEN
  boundaries (`\b` regex) instead of a bare substring `Contains`, so legitimate
  domain words (`compliance`, `statutory`, `reporting`, `obligation`,
  `attestation`) are not falsely rejected while true forbidden markers
  (`report_body`, `model_output`, `salary`, `narrative`, `free_text`, ...) are
  still caught.

Explicitly unauthorized:

- HR compliance / statutory reporting workflow.
- Obligation catalog, control mapping, statutory report definition, filing
  schedule, attestation closure.
- Real compliance obligation execution, control testing, statutory report
  generation/rendering/export, regulatory filing/submission, e-filing, and
  attestation execution.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Compliance UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Sensitive payload and report-content persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- Tenant shell nav entry `HR Compliance & Statutory Reporting`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Compliance-officer UX beyond readiness-metadata CRUD.
- Reviewer/attestation UX beyond readiness-metadata CRUD.
- Obligation catalog, control mapping, statutory report definition, filing
  schedule, attestation closure, scoring, or model-output UI.
- Report body, statutory filing content, generated report data, exported
  dataset, finding, control-test result, attestation content, or any
  report-content UI.
- Document, notification, export, upload, or attachment UI.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> HCM `5059`). This is a compliance / statutory-reporting
READINESS facade that owns no reporting runtime: it does NOT authorize
real compliance obligation execution, control testing, statutory report
generation/rendering/export, regulatory filing/submission, e-filing, attestation
execution, report content/dataset persistence, or ownership of the backing HCM
data sources / regulatory reference data. It does NOT authorize and NEVER
persists report bodies, statutory filing content, generated report data,
exported datasets, findings, control-test results, or attestation content; only
boundary/readiness STATE metadata is persisted.

Authorized API surface:

- `GET /api/hr-compliance` - list readiness metadata.
- `GET /api/hr-compliance/{id}` - get readiness metadata by id.
- `POST /api/hr-compliance` - create readiness metadata.
- `POST /api/hr-compliance/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/hr-compliance/{id}` - soft-delete.
- `GET /api/hr-compliance/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get HR compliance readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No HR compliance / statutory reporting workflow, obligation catalog, control
  mapping, statutory report definition, filing schedule, attestation closure,
  report content storage, statutory report generation/export, regulatory
  filing/submission, scoring/model-output persistence, automated decision
  behavior, notification/document integration, or sensitive/report-content
  payload persistence.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only HR compliance readiness state.
- Boundary-state metadata for obligation catalog, control mapping, statutory
  report definition, filing schedule, attestation closure, automated decision,
  consent, minimization, retention, and evidence.
- Dependency-state metadata for regulatory source, HCM data source,
  document, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- HR compliance / statutory reporting workflow payload.
- Obligation-catalog/control-mapping/statutory-report-definition/filing-schedule/attestation-closure
  execution payload.
- Real compliance obligation execution, control testing, statutory report
  generation/rendering/export, regulatory filing/submission, e-filing, or
  attestation execution.
- Report body, statutory filing content, generated report data, exported
  dataset, finding, control-test result, attestation content, or any report
  content.
- Statutory report generation/export or PII changes, filing/submission
  execution, report content, or notes/comments persistence.
- Readiness score/rating/rank/model output.
- Automated decision result.
- Free-text compliance notes or report narrative.
- Report interaction transcript payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy employee/report details.
- Report body / statutory filing content / generated report data / exported
  dataset / finding / control-test result / attestation content and any report
  content.

## 12. Permission Boundary

Approved permission namespace:

- `hcm.hr-compliance.read`
- `hcm.hr-compliance.manage`
- `hcm.hr-compliance.evaluate`
- `hcm.hr-compliance.audit.read`

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
- HR sensitive-access controls (`CAND-CAP-0008`) remain the governing
  sensitive-access dependency for this sensitive compliance domain; no report
  content is ever exposed or persisted through this readiness slice.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation.
- HR compliance data minimization fail-closed behavior.
- Retention/evidence/audit/deletion as local/deferred metadata.
- Exclusion of report narrative, report body, statutory filing content,
  generated report data, exported datasets, findings, control-test results,
  attestation content, report content, and PII-heavy data.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.

The backing HCM data sources and shared regulatory reference / analytics/
reporting substrate dependency are deferred and out of scope: this compliance
facade only consumes readiness metadata and owns no reporting runtime. No
integration implementation, provider payload persistence, or document/
notification UI is authorized.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0040`.
3. AC-03: Legacy Excel ID `MOD-0318` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `hcm.hr-compliance`.
5. AC-05: Permission namespace is limited to `hcm.hr-compliance.read`,
   `hcm.hr-compliance.manage`, `hcm.hr-compliance.evaluate`, and
   `hcm.hr-compliance.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.HumanCapitalService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection `hcm_hr_compliance_readiness`)
   includes active tenant `Code` uniqueness and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: HR compliance / statutory reporting workflow, obligation catalog,
    control mapping, statutory report definition, filing schedule, attestation
    closure, model output, and automated decision behavior remain unauthorized.
14. AC-14: Compliance UX fields remain reserved/out of
    scope; no report-content field, no monetary field, and no PII-heavy field is
    present.
15. AC-15: Free-text compliance notes, report narrative, report body, statutory
    filing content, generated report data, exported dataset, finding,
    control-test result, attestation content, raw provider payload,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: HCM, TEP, and PSS dependency boundaries are recorded.
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0040|MOD-0318` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build HCM API:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `HrCompliance` filter
  (`Application.Tests/HrComplianceTests.cs`, handler behavior; fix-absent
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
  not bare substring `Contains`, so legitimate words (`compliance`, `statutory`,
  `reporting`, `obligation`, `attestation`) are not falsely rejected while
  true forbidden markers (`report_body`, `model_output`, `salary`, `narrative`,
  `free_text`, ...) are still caught.
- Verify forbidden workflow/obligation-catalog/control-mapping/statutory-report-definition/filing-schedule/
  attestation-closure/scoring/decision/sensitive/report-content fields are absent.
- Verify compliance UX fields are reserved/absent and no
  report-content field, no monetary field, and no PII-heavy field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0040|MOD-0318" services/Diten.HumanCapitalService frontend gateway tests`

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] HR compliance / statutory reporting workflow prohibition is accepted.
- [x] Obligation catalog/control mapping/statutory report definition/filing schedule/attestation
  closure prohibition is accepted.
- [x] Compliance obligation execution/report-content storage/statutory-report-generation/attestation-content-persistence
  prohibition is accepted.
- [x] Statutory report generation/PII change/filing-submission execution/
  report-content prohibition is accepted.
- [x] Report body/statutory filing content/generated report data/exported
  dataset/finding/control-test result/attestation content/report-content
  prohibition is accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved compliance UX fields and no-report-content-field/no-money-field/no-PII-heavy-field
  constraint are accepted.
- [x] Sensitive payload and report-content prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] HCM/TEP/PSS dependency context is accepted, including the HR
  sensitive-access controls (`CAND-CAP-0008`) governance dependency.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only HR compliance readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] HR compliance data minimization policy.
- [x] Retention/evidence/audit/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.HumanCapitalService/**`
  and the owned `frontend/Diten.Web/**` HrCompliance objects.
- Runtime owner/key remains limited to `hcm.hr-compliance`.
- Permission namespace remains limited to `hcm.hr-compliance.read`,
  `hcm.hr-compliance.manage`, `hcm.hr-compliance.evaluate`, and
  `hcm.hr-compliance.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `RegulatorySourceDependencyState`, `HcmDataSourceDependencyState`,
  `DocumentDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Compliance UX fields remain reserved/out of scope
  and no report-content field, no monetary field, and no PII-heavy field is present.
- TEP, PSS, and Platform scopes remain closed.
- HR compliance / statutory reporting workflow, obligation catalog, control
  mapping, statutory report definition, filing schedule, attestation closure,
  compliance obligation execution, report content storage, statutory report
  generation/export, filing/submission execution or PII changes, control testing,
  model output, automated decision behavior, notification/document
  integration, free-text compliance notes, report narrative, report body,
  statutory filing content, generated report data, exported dataset, finding,
  control-test result, attestation content, raw provider payload,
  credential/token/secret/password, report content, and PII-heavy persistence
  remain out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0040 --name "HR Compliance & Statutory Reporting"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0040`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- HR compliance data minimization fail-closed policy approved.
- Retention/evidence/audit/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0040|MOD-0318" services/Diten.HumanCapitalService frontend gateway tests`

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real HR compliance & statutory reporting workflow.
- Obligation catalog and control mapping.
- Statutory report definition, filing schedule, and attestation closure governance.
- Automated decision approval.
- Compliance UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
