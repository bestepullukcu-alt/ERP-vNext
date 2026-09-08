---
id: CAND-CAP-0029
name: Development Plan Management
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0029-development-plan-management
started: 2026-09-04
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0029
legacy_excel_id: MOD-0308
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
bootstrap_type: metadata-only-backend-api-readiness
runtime_owner_key: hcm.development-plans
permission_namespace:
  - hcm.development-plans.read
  - hcm.development-plans.manage
  - hcm.development-plans.evaluate
  - hcm.development-plans.audit.read
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0029 - Development Plan Management

> Status: done. Metadata-only backend/API development plan readiness contract
> slice passed implementation review and done-promotion readiness under
> `services/Diten.HumanCapitalService/**`. This pack does not authorize
> frontend, gateway, real development plan workflow, goal/learning assignment
> execution, coaching/manager action UX, skill gap scoring, rating, ranking,
> recommendation/model output persistence, automated decision behavior,
> notification/document integrations, or sensitive persistence.
> `CAND-CAP-0029` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0308` remains blocked for HCM use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0029` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-C capability named `Development Plan Management`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0308`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0029` as a temporary governance identity pending
future canonical MOD allocation.

This done pack records the completed first metadata-only backend/API
development plan readiness contract slice. Development plan workflow,
goal/learning assignment execution, coaching/manager action UX, skill gap
scoring, rating, ranking, recommendation, automated decision behavior,
notification/document integration, and sensitive persistence remain closed.

## 2. Ownership and Boundaries

Owned by this done pack:

- HCM-native Development Plan Management governance boundary.
- Sequencing after completed HCM employee, governance, position, offboarding,
  applicant intake, candidate pipeline, offer management, onboarding,
  employment change, performance review, and competency/skills foundation
  modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only development plan readiness backend/API contract boundary.
- Explicit exclusion of development plan workflow execution.
- Explicit exclusion of goal/learning assignment execution.
- Explicit exclusion of coaching and manager action UX.
- Explicit exclusion of skill gap score, rating, rank, recommendation, model
  output, and automated decision behavior.
- Explicit exclusion of free-text development notes, coaching narrative,
  attachment payload, raw provider payload, credential/token/secret/password,
  and PII-heavy persistence.

Not owned by this pack:

- Real development plan workflow, plan execution, plan approval, goal
  assignment, learning assignment, coaching task execution, or completion
  tracking.
- Learning/training record ownership or learning management integration.
- Goal management, performance scoring, competency scoring, succession,
  analytics, TEP, or PSS ownership transfer.
- Frontend, Gateway routes, tenant shell navigation, or read-only UI.
- Notification, notification provider, controlled document, or external
  document repository implementation.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| DevelopmentPlanManagementBoundary | Governance boundary | Defines what a future HCM development plan module may own. |
| DevelopmentPlanReadinessBoundary | Governance boundary | Separates readiness metadata from workflow execution. |
| GoalLearningAssignmentBoundary | Governance boundary | Blocks goal and learning assignment execution. |
| CoachingActionBoundary | Governance boundary | Blocks coaching and manager action UX/runtime. |
| SkillGapDecisionBoundary | Governance boundary | Blocks skill gap score/rating/rank/recommendation/model-output persistence. |
| DevelopmentPlanSensitiveDataBoundary | Governance boundary | Blocks narrative, attachment, credential, and PII-heavy persistence. |
| DevelopmentPlanDependencyBoundary | Governance boundary | Keeps notification/document dependencies deferred and out of scope. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| DevelopmentPlanWorkflow | Deferred runtime | Plan workflow execution; not authorized. |
| GoalLearningAssignmentExecution | Deferred runtime | Goal/learning assignment execution; not authorized. |
| CoachingManagerActionExperience | Deferred frontend/runtime | Coaching and manager action UX; not authorized. |
| SkillGapRecommendationEngine | Deferred runtime | Scoring, rating, ranking, recommendations, and model output; not authorized. |
| DevelopmentPlanDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| DevelopmentPlanNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Only metadata-only readiness runtime objects are approved by this pack.

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields:

- `Code`
- `DisplayName`
- `DevelopmentPlanReadinessState`
- `DevelopmentPlanWorkflowBoundaryState`
- `GoalAssignmentBoundaryState`
- `LearningAssignmentBoundaryState`
- `CoachingActionBoundaryState`
- `ManagerActionUxBoundaryState`
- `SkillGapScoringBoundaryState`
- `RatingBoundaryState`
- `RankingBoundaryState`
- `RecommendationBoundaryState`
- `AutomatedDecisionBoundaryState`
- `NotificationDependencyState`
- `DocumentDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `DevelopmentPlanReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Forbidden field classes:

- Development plan workflow body, plan item body, execution payload,
  completion payload, approval decision body, coaching action evidence, or
  completed plan content.
- Goal assignment execution payload, learning assignment execution payload,
  training record payload, completion transcript, or LMS/provider payload.
- Skill gap score, rating value, rank, recommendation output, model output,
  automated decision result, or evaluator scoring payload.
- Free-text development notes, coaching narrative, manager notes, employee
  comments, HR notes, performance concern narrative, or sensitive plan body.
- Attachment payload, controlled document body, external document content, raw
  evidence, raw provider payload, or uploaded file metadata.
- Salary amount, compensation amount, benefits election, payroll details, tax
  details, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  PII-heavy employee/profile fields.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0029-development-plan-management.md`

Candidate future runtime service:

- `Diten.HumanCapitalService`

Runtime repo scope:

- `services/Diten.HumanCapitalService/**`

## 6. Protected Paths

This done pack authorizes only the first metadata-only backend/API
readiness slice under `services/Diten.HumanCapitalService/**` and does not
authorize changes to:

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
- `CAND-CAP-0027` - Performance Review Management, done.
- `CAND-CAP-0028` - Competency & Skills Assessment, done.

TEP context dependencies:

- `CAND-CAP-0011` through `CAND-CAP-0021` remain dependency/context only.

PSS backbone context:

- `MOD-0288`, `MOD-0251`, `MOD-0279`, `MOD-0280`, and `MOD-0281` remain
  dependency/backbone context only.

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is
authorized only for the first metadata-only backend/API readiness slice.

Authorized runtime boundary:

- Runtime owner/key: `hcm.development-plans`.
- Permission namespace:
  - `hcm.development-plans.read`
  - `hcm.development-plans.manage`
  - `hcm.development-plans.evaluate`
  - `hcm.development-plans.audit.read`
- Runtime repo scope: `services/Diten.HumanCapitalService/**`.
- Thin controller, CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository.
- Active tenant `Code` uniqueness.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.

Explicitly unauthorized:

- Frontend or gateway exposure.
- Development plan workflow.
- Goal/learning assignment execution.
- Coaching/manager action UX.
- Skill gap scoring, rating, ranking, recommendation, model output, or
  automated decision behavior.
- Notification/document integrations.
- Sensitive payload persistence.

## 9. Frontend / UX Boundary

Frontend and gateway remain closed.

Unauthorized UX:

- Tenant shell menu/page.
- Restricted read-only page.
- Employee development plan UX.
- Manager coaching/action UX.
- Goal assignment, learning assignment, plan workflow, recommendation, rating,
  ranking, or model-output UI.
- Document, notification, export, upload, or attachment UI.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized.

The first slice must stay limited to:

- Create/list/get development plan readiness metadata.
- Audit metadata read endpoint.
- Fail-closed evaluation/deferred metadata.
- Tenant-aware persistence and active-code uniqueness.
- No development plan workflow, goal/learning assignment execution,
  coaching/manager action UX, skill gap scoring, rating, ranking,
  recommendation/model output persistence, automated decision behavior,
  notification/document integration, or sensitive payload persistence.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only development plan readiness state.
- Boundary-state metadata for workflow, goal/learning assignments, coaching,
  manager UX, skill gap scoring, rating, ranking, recommendation, automated
  decision, notification, document, consent, minimization, retention, and
  evidence.
- Dependency/precondition metadata.

Forbidden:

- Development plan workflow payload.
- Goal/learning assignment execution payload.
- Coaching/manager action payload.
- Skill gap score/rating/rank/recommendation/model output.
- Automated decision result.
- Free-text development notes or coaching narrative.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy employee/profile details.
- Compensation/payroll/benefits payload.

## 12. Permission Boundary

Approved permission namespace:

- `hcm.development-plans.read`
- `hcm.development-plans.manage`
- `hcm.development-plans.evaluate`
- `hcm.development-plans.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Future runtime work must be tenant-aware and fail closed. The first backend/API
slice must resolve tenant server-side and must not expose `TenantId` in request
DTOs.

Security guardrails:

- Candidate identity must not become a runtime literal.
- Blocked legacy ID must not become a runtime literal.
- Backend authorization must be authoritative if a future API slice is
  approved.
- Frontend permission checks, if later approved, must remain UX-only.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation.
- Development data minimization fail-closed behavior.
- Retention/evidence/audit/legal-hold/deletion as local/deferred metadata.
- Exclusion of development narrative, scoring/recommendation payloads, and
  PII-heavy data.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, route exposure, provider payload persistence, or
document/notification UI is authorized.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `done`.
2. AC-02: Candidate identity is `CAND-CAP-0029`.
3. AC-03: Legacy Excel ID `MOD-0308` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Reservation exists in the module ID registry.
5. AC-05: Reservation exists in blueprint/master reconciliation.
6. AC-06: Candidate gate script absence is recorded as a governance note.
7. AC-07: Runtime repo scope is limited to
   `services/Diten.HumanCapitalService/**`.
8. AC-08: Development plan workflow remains unauthorized.
9. AC-09: Goal/learning assignment execution remains unauthorized.
10. AC-10: Coaching/manager action UX remains unauthorized.
11. AC-11: Skill gap scoring, rating, ranking, recommendation, model output,
    and automated decision behavior remain unauthorized.
12. AC-12: Free-text development notes, coaching narrative, attachment payload,
    raw provider payload, credential/token/secret/password, and PII-heavy
    persistence remain unauthorized.
13. AC-13: Notification/document dependencies remain deferred/out of scope.
14. AC-14: HCM foundation dependencies are recorded.
15. AC-15: TEP context dependencies are recorded.
16. AC-16: PSS backbone context dependencies are recorded.
17. AC-17: No canonical MOD is invented for `MOD-0308`.
18. AC-18: `CAND-CAP-0029` remains governance/documentation identity only.
19. AC-19: Frontend and gateway remain closed until backend/API review/done
    gates.
20. AC-20: Runtime literal scan for `CAND-CAP-0029|MOD-0308` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Governance validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: done`.
- Confirm all 20 sections are present.
- Confirm registry reservation exists.
- Confirm blueprint/master reconciliation reservation exists.
- Confirm candidate gate script result or not-executable governance note is
  recorded.
- Build HCM API:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `DevelopmentPlan` filter.
- Run full HCM Application tests.
- Verify metadata contract create/list/get behavior.
- Verify tenant isolation.
- Verify active tenant `Code` uniqueness.
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface.
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden workflow/assignment/coaching/scoring/rating/ranking/
  recommendation/decision/sensitive fields are absent.
- Verify no production in-memory repository exists.
- Verify frontend and gateway remain closed.
- Verify development plan workflow remains unauthorized.
- Verify goal/learning assignment execution remains unauthorized.
- Verify coaching/manager action UX remains unauthorized.
- Verify skill gap scoring/rating/ranking/recommendation/model output and
  automated decision behavior remain unauthorized.
- Verify notification/document dependencies remain deferred/out of scope.
- Verify forbidden sensitive payload classes remain closed.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0029|MOD-0308" services/Diten.HumanCapitalService frontend gateway tests`

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance-only approval.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Runtime/API/frontend/gateway prohibition is accepted.
- [x] Development plan workflow prohibition is accepted.
- [x] Goal/learning assignment execution prohibition is accepted.
- [x] Coaching/manager action UX prohibition is accepted.
- [x] Skill gap scoring/rating/ranking/recommendation/model-output prohibition
  is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Sensitive payload prohibition is accepted.
- [x] Notification/document dependency deferral is accepted.
- [x] HCM/TEP/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only development plan readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Development data minimization policy.
- [x] Recommendation/automated decision boundary.
- [x] Retention/evidence/audit/legal-hold/deletion boundary.
- [x] Frontend/gateway sequencing decision.

Runtime-ready blockers:

- none.

Review-ready reconciliation note:

- Implementation review PASS.
- Metadata-only backend/API slice completed under
  `services/Diten.HumanCapitalService/**`.
- Runtime owner/key remained only `hcm.development-plans`.
- Permission namespace remained only `hcm.development-plans.read`,
  `hcm.development-plans.manage`, `hcm.development-plans.evaluate`, and
  `hcm.development-plans.audit.read`.
- Build PASS: 0 warning, 0 error.
- DevelopmentPlan targeted tests PASS: 38/38.
- Full HCM Application tests PASS: 277/277.
- Runtime literal scan PASS: `CAND-CAP-0029|MOD-0308` no matches.
- Production in-memory repository scan PASS.
- Pack Section 4 metadata-only contract aligned with runtime public/persisted
  fields.
- Frontend/gateway/TEP/PSS/Platform scope remained closed.
- Development plan workflow, goal/learning assignment execution,
  coaching/manager action UX, skill gap scoring/rating/ranking/recommendation/
  model output, automated decision behavior, notification/document integration,
  free-text development notes, coaching narrative, attachment payload, raw
  provider payload, credential/token/secret/password, and PII-heavy persistence
  remained out of scope.

Done-promotion reconciliation note:

- Done-promotion readiness audit PASS.
- Pack was promoted from `review` to `done`.
- Open blockers remained none.
- Review-ready reconciliation note remained present.
- Metadata-only backend/API slice remained completed under
  `services/Diten.HumanCapitalService/**`.
- Runtime owner/key remained only `hcm.development-plans`.
- Permission namespace remained only `hcm.development-plans.read`,
  `hcm.development-plans.manage`, `hcm.development-plans.evaluate`, and
  `hcm.development-plans.audit.read`.
- Build evidence remained PASS: 0 warning, 0 error.
- DevelopmentPlan targeted test evidence remained PASS: 38/38.
- Full HCM Application test evidence remained PASS: 277/277.
- Runtime literal scan evidence remained PASS: `CAND-CAP-0029|MOD-0308` no
  matches.
- Production in-memory repository scan evidence remained PASS.
- Frontend/gateway remained closed and future-follow-up only.
- Development plan workflow, goal/learning assignment execution,
  coaching/manager action UX, skill gap scoring/rating/ranking/recommendation/
  model output, automated decision behavior, notification/document integration,
  free-text development notes, coaching narrative, attachment payload, raw
  provider payload, credential/token/secret/password, and PII-heavy persistence
  remained out of scope.

## 20. Open Blockers / Waivers

Open blockers for governance continuation:

- none.

Open blockers for runtime-ready:

- none.

Waivers recorded:

- Candidate gate script is not executable in this checkout because
  `.antigravity/scripts/verify_module_id.py` is missing; recorded as governance
  note.
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0029`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice.
- Legal/privacy/consent metadata-only waiver approved.
- Development data minimization fail-closed policy approved.
- Recommendation/automated decision boundary explicit closed.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0029|MOD-0308" services/Diten.HumanCapitalService frontend gateway tests`

Next safe step:

- HCM Development Plans gateway exposure.

Future follow-ups:

- Gateway exposure after backend/API done gate.
- Restricted read-only frontend slice after gateway exposure.
- Real development plan workflow.
- Goal/learning assignment execution.
- Coaching/manager action UX.
- Skill gap scoring/rating/ranking/recommendation governance.
- Automated decision approval.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
