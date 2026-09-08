---
id: CAND-CAP-0027
name: Performance Review Management
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0027-performance-review-management
started: 2026-09-03
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0027
legacy_excel_id: MOD-0306
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
bootstrap_type: metadata-only-backend-api-readiness
runtime_owner_key: hcm.performance-reviews
permission_namespace:
  - hcm.performance-reviews.read
  - hcm.performance-reviews.manage
  - hcm.performance-reviews.evaluate
  - hcm.performance-reviews.audit.read
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0027 - Performance Review Management

> Status: done. Done-promotion readiness audit PASS for the first metadata-only
> backend/API performance review readiness contract slice; production workflow,
> scoring, rating, calibration, ranking, automated decision behavior,
> manager/employee review UX, frontend, and gateway implementation remain
> closed. `CAND-CAP-0027` remains a governance/documentation identity only and
> must not be written into runtime literals. `MOD-0306` remains blocked for HCM
> use until future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0027` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-C capability named `Performance Review Management`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0306`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0027` as a temporary governance identity pending
future canonical MOD allocation.

This done pack records the completed first metadata-only backend/API
performance review readiness contract slice. Performance review cycle/runtime
workflow, goal scoring, rating, calibration, ranking behavior, automated
decision behavior, manager/employee review UX, frontend, gateway,
notification/document integration, and sensitive appraisal persistence remain
closed.

## 2. Ownership and Boundaries

Owned by this done pack:

- HCM-native performance review management governance boundary.
- Sequencing after completed HCM employee, governance, position, offboarding,
  applicant intake, candidate pipeline, offer management, onboarding, and
  employment change foundation modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only performance review readiness backend/API contract.
- Explicit exclusion of performance review cycle/runtime workflow.
- Explicit exclusion of goal scoring, rating, calibration, ranking, and
  automated decision behavior.
- Explicit exclusion of manager-facing and employee-facing review UX.
- Explicit exclusion of free-text review notes, appraisal narrative, attachment
  payload, raw provider payload, credential/token/secret/password, compensation
  payload, payroll payload, benefits payload, and PII-heavy persistence.

Not owned by this pack:

- Real review cycle execution, manager submissions, employee acknowledgements,
  calibration sessions, scorecards, ratings, rankings, or model output.
- Goal management ownership or enterprise scorecard ownership.
- Frontend, Gateway routes, tenant shell navigation, or read-only UI.
- Notification, notification provider, controlled document, or external
  document repository implementation.
- Compensation, payroll, benefits, learning, succession, analytics, TEP, or PSS
  ownership transfer.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| PerformanceReviewBoundary | Governance boundary | Defines what a future HCM performance review module may own. |
| PerformanceReviewReadinessBoundary | Governance boundary | Separates readiness metadata from review workflow execution. |
| ReviewCycleBoundary | Governance boundary | Blocks cycle launch, submission, close, and reopen runtime behavior. |
| ScoringRatingBoundary | Governance boundary | Blocks score, rating, rank, calibration, and automated decision behavior. |
| ReviewUxBoundary | Governance boundary | Blocks manager and employee review UX until separately approved. |
| ReviewSensitiveDataBoundary | Governance boundary | Blocks narrative, attachment, appraisal, credential, and PII-heavy persistence. |
| ReviewDependencyBoundary | Governance boundary | Keeps notification/document dependencies deferred and out of scope. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| PerformanceReviewCycleWorkflow | Deferred runtime | Review-cycle execution; not authorized. |
| GoalScoringRatingEngine | Deferred runtime | Scoring, rating, calibration, and ranking; not authorized. |
| ManagerEmployeeReviewExperience | Deferred frontend/runtime | Manager/employee review UX; not authorized. |
| ReviewDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| ReviewNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |
| ReviewAnalyticsIntegration | Deferred dependency | Analytics, model output, and workforce insights; not authorized. |

Only metadata-only readiness runtime objects are approved by this pack.

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields:

- `Code`
- `DisplayName`
- `PerformanceReviewReadinessState`
- `ReviewCycleBoundaryState`
- `GoalDependencyState`
- `ScoringBoundaryState`
- `RatingBoundaryState`
- `CalibrationBoundaryState`
- `RankingBoundaryState`
- `AutomatedDecisionBoundaryState`
- `ManagerReviewUxBoundaryState`
- `EmployeeReviewUxBoundaryState`
- `CompensationDataBoundaryState`
- `BenefitsDataBoundaryState`
- `PayrollDataBoundaryState`
- `NotificationDependencyState`
- `DocumentDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `PerformanceReviewReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Forbidden field classes:

- Review cycle execution body, submission payload, acknowledgement payload,
  reopen reason, approval decision body, or completed action evidence.
- Goal score, rating value, rank, calibration outcome, model output, automated
  decision result, recommendation output, or evaluator scoring payload.
- Free-text review notes, appraisal narrative, manager comments, employee
  comments, HR notes, performance concern narrative, or sensitive review body.
- Attachment payload, controlled document body, external document content, raw
  evidence, raw provider payload, or uploaded file metadata beyond approved
  reference-only identifiers.
- Salary amount, compensation amount, bonus amount, benefits election, payroll
  details, tax details, payslip, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  PII-heavy employee/profile fields.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0027-performance-review-management.md`

Candidate future runtime service:

- `Diten.HumanCapitalService`

Authorized runtime repo scope:

- `services/Diten.HumanCapitalService/**`

Runtime owner/key:

- `hcm.performance-reviews`

## 6. Protected Paths

This done pack records the completed metadata-only backend/API slice
under `services/Diten.HumanCapitalService/**` and does not authorize changes to:

- `frontend/**`
- `gateway/**`
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

TEP dependency/context only:

- `CAND-CAP-0011` - Talent Ecosystem Platform Shell, done.
- `CAND-CAP-0012` - Association Membership & Member Company Registry, done.
- `CAND-CAP-0013` - Consent, Visibility & Access Policy, done.
- `CAND-CAP-0014` - Verified HR Participant & Company Access, done.
- `CAND-CAP-0015` - Talent Ecosystem Governance & Review Board, done.
- `CAND-CAP-0016` - Trust Level & Multi-Signature Engine, done.
- `CAND-CAP-0017` - Industry Candidate Identity & Talent Profile, done.
- `CAND-CAP-0018` - Industry Exit Reference Record Registry, done.
- `CAND-CAP-0019` - Reference Exchange Marketplace, done.
- `CAND-CAP-0020` - Rehire Recommendation Network, done.
- `CAND-CAP-0021` - Candidate Response & Dispute Management, done.

PSS/backbone context only:

- `MOD-0288` - Organization, Person & Position Directory.
- `MOD-0251` - HRIS External SoR.
- `MOD-0279` - Payroll Engine External SoR.
- `MOD-0280` - Time & Attendance External Provider.
- `MOD-0281` - Payroll Integration & Governance.

Deferred/out-of-scope dependencies:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

## 8. Runtime Constraints

- `CAND-CAP-0027` is a governance/documentation identity only and must never be
  written into runtime literals, permission seeds, route metadata, database
  records, job names, telemetry owner fields, config keys, or test fixtures.
- `MOD-0306` must not be used for this HCM capability until EA assigns a
  canonical MOD.
- Metadata-only backend/API readiness slice is authorized under
  `services/Diten.HumanCapitalService/**`.
- Production performance review workflow implementation is not authorized.
- Frontend and Gateway implementation are not authorized until backend/API
  review and done gates complete.
- Performance review cycle/runtime workflow remains blocked.
- Goal scoring, rating, calibration, ranking, model output, and automated
  decision behavior remain blocked.
- Manager-facing and employee-facing review UX remains blocked.
- Compensation/payroll/benefits payload persistence remains blocked.
- Notification/document integrations remain deferred/out of scope.
- Free-text review notes, appraisal narrative, attachment payload, raw provider
  payload, credential/token/secret/password, and PII-heavy persistence remain
  blocked.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

This pack does not create an HCM page, workspace menu, Razor view, JavaScript
file, DataTable, RESX resource, layout binding, or frontend route.

Any future frontend requires separate backend/API, Gateway, and frontend
readiness decisions. The first UI, if approved later, must be restricted/admin
read-only metadata only.

## 10. Backend File Convention

Backend implementation is authorized only for the metadata-only readiness slice.

First-slice naming:

- Feature folder: `PerformanceReviews`
- Controller: `PerformanceReviewsController`
- Repository contract: `IPerformanceReviewReadinessMetadataRepository`
- Mongo repository: `MongoPerformanceReviewReadinessMetadataRepository`

Any future request DTO must not contain `TenantId`; tenant identity must be
resolved server-side.

## 11. Frontend File Contract

Frontend implementation is not authorized.

No Razor, JavaScript, DataTable, RESX, menu, route, API client, GatewayUrl
binding, manager-facing UX, employee-facing UX, scoring UX, rating UX,
calibration UX, review submission UX, or attachment/document UX is authorized.

Any future frontend must wait for backend/API implementation review and Gateway
exposure. It must be restricted/admin read-only metadata unless separate legal,
privacy, scoring/rating, calibration, notification, document, and HR process
approvals explicitly authorize more.

## 12. API Surface Contract

API surface is authorized only for metadata-only backend/API readiness.

Metadata-only backend/API endpoints:

- `GET /api/performance-reviews`
- `GET /api/performance-reviews/{id}`
- `GET /api/performance-reviews/{id}/audit-metadata`

Create, evaluate, and soft-delete endpoints may exist only as metadata
readiness boundaries when the first backend/API slice needs testable create,
fail-closed evaluation, and soft delete behavior. They must not execute
performance review cycles, goal scoring, rating, calibration, ranking,
automated decision behavior, notification delivery, document integration, or
manager/employee review workflow. Any API must expose only readiness metadata
and must not expose review bodies, appraisal narratives, scoring/rating values,
calibration outcomes, ranking data, automated decision output, compensation,
benefits, payroll data, document bodies, attachment payloads, raw provider
payloads, credential/token/secret/password values, or PII-heavy details.

## 13. Data Boundary

Allowed in this done pack:

- Governance metadata.
- Dependency sequencing notes.
- Planning-only boundary states for review cycle readiness, goal dependency,
  scoring, rating, calibration, ranking, automated decision exclusion,
  manager/employee UX exclusion, notification, documents, retention, evidence,
  consent, and minimization.
- Metadata-only backend/API readiness persistence under the approved runtime
  repo scope.

Forbidden:

- Performance review cycle/runtime workflow execution.
- Review launch, submission, acknowledgement, close, reopen, reminder, or action
  workflow execution.
- Goal score, rating value, calibration outcome, rank, model output, automated
  decision result, or recommendation result persistence.
- Manager-facing or employee-facing review UX.
- Notification delivery, reminders, message templates, or delivery provider
  integration.
- Controlled/external document repository integration, document generation,
  document upload, attachment payload, or document body display.
- Salary amount, compensation amount, bonus amount, benefits election, bank
  details, payroll details, tax details, payslip, or payment instruction
  persistence.
- Raw provider payload, raw evidence, credential, token, secret, password,
  activation code, device secret, or provider connection value.
- Free-text review notes, appraisal narrative, HR notes, manager comments,
  employee comments, national ID, DOB, home address, biometric/geolocation data,
  medical data, or PII-heavy profile persistence.

## 14. Permissions & Security

Approved permission namespace:

- `hcm.performance-reviews.read`
- `hcm.performance-reviews.manage`
- `hcm.performance-reviews.evaluate`
- `hcm.performance-reviews.audit.read`

Backend `[HasPermission]` enforcement must remain authoritative in any future
slice. Any future frontend permission behavior must remain UX-only hide/disable
behavior.

Security decisions for the first slice:

- EA candidate-runtime policy waiver is approved for the first metadata-only
  backend/API slice.
- Legal/privacy/consent is metadata-only waiver state.
- Review data minimization is fail-closed.
- Scoring/rating/calibration/ranking and automated decision behavior remain
  excluded.
- Free-text review notes, appraisal narrative, and attachment payload remain
  excluded.
- Compensation/payroll/benefits payload remains excluded.
- Evidence, retention, audit, legal hold, and deletion remain local/deferred
  metadata.

## 15. Gateway / API Routing Decision

Gateway routing is not authorized.

No Gateway route, Ocelot entry, frontend API proxy, or service port exposure is
authorized until the backend/API slice is implemented and reviewed.

Gateway exposure must wait until a backend/API slice is approved, implemented,
and reviewed. Any future frontend must use GatewayUrl rather than direct service
ports.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `done`.
2. AC-02: Candidate identity is `CAND-CAP-0027`.
3. AC-03: Legacy Excel ID `MOD-0306` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `hcm.performance-reviews`.
5. AC-05: Permission namespace is limited to
   `hcm.performance-reviews.read`, `hcm.performance-reviews.manage`,
   `hcm.performance-reviews.evaluate`, and
   `hcm.performance-reviews.audit.read`.
6. AC-06: Runtime repo scope is `services/Diten.HumanCapitalService/**`.
7. AC-07: Create/List/Get metadata readiness contract is testable.
8. AC-08: Tenant isolation is server-side and fail-closed.
9. AC-09: Active tenant `Code` uniqueness is testable.
10. AC-10: Soft delete hides deleted records and sets `DeletedAt`.
11. AC-11: Dependency/precondition evaluation is fail-closed.
12. AC-12: Non-activating evaluation can emit explicit `Deferred` metadata.
13. AC-13: Frontend and Gateway implementation remain closed.
14. AC-14: Performance review cycle/runtime workflow remains blocked.
15. AC-15: Goal scoring, rating, calibration, ranking, and automated decision
    behavior remain blocked.
16. AC-16: Manager/employee review UX remains blocked.
17. AC-17: Notification/document dependencies remain deferred/out of scope.
18. AC-18: Compensation/payroll/benefits payload persistence remains blocked.
19. AC-19: Free-text review notes, appraisal narrative, attachment payload,
    raw provider payload, credential/token/secret/password, and PII-heavy
    persistence remain blocked.
20. AC-20: Runtime literal scan for `CAND-CAP-0027|MOD-0306` passes under
    runtime paths.

## 17. Test Expectations

Runtime test expectations for the first backend/API slice:

- Create/List/Get metadata contract.
- Tenant isolation.
- Active tenant `Code` uniqueness.
- Soft delete hides deleted records and sets `DeletedAt`.
- Permission-gated controller surface.
- Dependency/precondition fail-closed behavior.
- Non-activating evaluation deferred metadata behavior.
- Forbidden review-cycle/scoring/rating/calibration/ranking/sensitive field
  guard.
- Runtime literal regression guard.

Runtime-ready validation command:

- `rg -n "CAND-CAP-0027|MOD-0306" services/Diten.HumanCapitalService frontend gateway tests`

## 18. Governance Approval Checklist

- [x] EA approves candidate-based governance continuation.
- [x] Registry owner accepts candidate gate result or verifier absence note.
- [x] `CAND-CAP-0027` remains documentation identity only.
- [x] `MOD-0306` remains blocked until EA canonical MOD assignment.
- [x] Metadata-only backend/API readiness slice is completed and reviewed.
- [x] Frontend/Gateway implementation remains closed.
- [x] Performance review cycle/runtime workflow remains closed.
- [x] Goal scoring/rating/calibration/ranking behavior remains closed.
- [x] Automated decision behavior remains closed.
- [x] Manager/employee review UX remains closed.
- [x] Notification/document dependencies remain deferred/out of scope.
- [x] Sensitive/raw/PII-heavy data boundary is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime policy waiver or canonical MOD assignment approved.
- [x] Runtime owner/key approved.
- [x] Permission namespace approved.
- [x] Runtime repo scope approved.
- [x] First-slice metadata-only performance review readiness contract approved.
- [x] Review cycle runtime exclusion approved.
- [x] Goal scoring/rating/calibration/ranking exclusion approved.
- [x] Automated decision exclusion approved.
- [x] Manager/employee review UX exclusion approved.
- [x] Compensation/payroll/benefits payload exclusion approved.
- [x] Legal/privacy/consent metadata-only waiver approved.
- [x] Review data minimization fail-closed policy approved.
- [x] Sensitive appraisal narrative exclusion approved.
- [x] Retention/evidence/audit/legal-hold/deletion local/deferred metadata
  approved.
- [x] Notification/document dependencies confirmed deferred/out of scope.
- [x] Frontend/Gateway remain closed as future follow-up after backend/API
  review.

Runtime-ready blockers:

- none.

## 20. Open Blockers, Waivers, and Notes

Open blockers for governance continuation:

- none.

Open blockers for runtime-ready:

- none.

Waivers:

- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API slice.
- Legal/privacy/consent metadata-only waiver approved.
- Review data minimization fail-closed decision approved.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata decision
  approved.
- Scoring/rating/calibration/ranking and automated decision exclusion approved.
- Frontend/Gateway deferred until backend/API review passes.

Review-ready reconciliation note:

- Implementation review PASS.
- Metadata-only backend/API slice completed.
- Runtime owner/key remains limited to `hcm.performance-reviews`.
- Permission namespace remains limited to `hcm.performance-reviews.read`,
  `hcm.performance-reviews.manage`, `hcm.performance-reviews.evaluate`, and
  `hcm.performance-reviews.audit.read`.
- Build PASS: 0 warning, 0 error.
- PerformanceReview targeted tests PASS: 33/33.
- Full HCM Application tests PASS: 207/207.
- Runtime literal scan PASS: `CAND-CAP-0027|MOD-0306` no matches.
- Production in-memory repository scan PASS.
- Section 8 runtime guard is aligned with the metadata-only backend/API
  readiness slice.
- Section 4 runtime public/persisted contract is aligned.
- `CompensationDataBoundaryState`, `BenefitsDataBoundaryState`, and
  `PayrollDataBoundaryState` remain boundary-state metadata only.
- Frontend, Gateway, TEP, PSS, and Platform scopes remain closed.
- Performance review cycle/runtime workflow, goal scoring, rating,
  calibration, ranking, automated decision behavior, manager/employee review
  UX, notification/document integration, salary/compensation amount, benefits
  election, payroll/tax/bank details, free-text review notes, appraisal
  narrative, attachment payload, compensation/payroll/benefits payload, raw
  provider payload, credential/token/secret/password, and PII-heavy persistence
  remain out of scope.

Done-promotion reconciliation note:

- Done-promotion readiness audit PASS.
- Pack status promoted from `review` to `done`.
- Open blockers remain none.
- Metadata-only backend/API slice remains completed and validated.
- Runtime owner/key remains limited to `hcm.performance-reviews`.
- Permission namespace remains limited to `hcm.performance-reviews.read`,
  `hcm.performance-reviews.manage`, `hcm.performance-reviews.evaluate`, and
  `hcm.performance-reviews.audit.read`.
- Build PASS: 0 warning, 0 error.
- PerformanceReview targeted tests PASS: 33/33.
- Full HCM Application tests PASS: 207/207.
- Runtime literal scan PASS: `CAND-CAP-0027|MOD-0306` no matches.
- Production in-memory repository scan PASS.
- Gateway and frontend remain future follow-up.
- Performance review cycle/runtime workflow, goal scoring, rating,
  calibration, ranking, automated decision behavior, manager/employee review
  UX, notification/document integration, salary/compensation amount, benefits
  election, payroll/tax/bank details, free-text review notes, appraisal
  narrative, attachment payload, compensation/payroll/benefits payload, raw
  provider payload, credential/token/secret/password, and PII-heavy persistence
  remain out of scope.

Candidate gate note:

- Candidate gate script `.antigravity/scripts/verify_module_id.py` is not
  executable in this checkout because the file is absent. EA/registry owner
  reservation has been recorded in the registry and blueprint reconciliation
  ledger.

Runtime literal scan note:

- PASS expected for
  `rg -n "CAND-CAP-0027|MOD-0306" services/Diten.HumanCapitalService frontend gateway tests`.

Next safe step:

- HCM Performance Reviews gateway exposure.

Future follow-ups:

- Real performance review workflow.
- Scoring/rating/calibration/ranking.
- Manager/employee review UX.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
