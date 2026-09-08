---
id: CAND-CAP-0028
name: Competency & Skills Assessment
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0028-competency-skills-assessment
started: 2026-09-04
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0028
legacy_excel_id: MOD-0307
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
bootstrap_type: metadata-only-backend-api-readiness
runtime_owner_key: hcm.competency-skills
permission_namespace:
  - hcm.competency-skills.read
  - hcm.competency-skills.manage
  - hcm.competency-skills.evaluate
  - hcm.competency-skills.audit.read
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0028 - Competency & Skills Assessment

> Status: done. Done-promotion readiness audit passed for the first
> metadata-only backend/API competency/skills readiness contract slice under
> `services/Diten.HumanCapitalService/**`.
> Production competency/skills assessment workflow, skill scoring, rating,
> ranking, calibration, automated decision behavior, manager/employee
> assessment UX, frontend, gateway, notification/document integrations, and
> sensitive persistence remain closed. `CAND-CAP-0028` remains a
> governance/documentation identity only and must not be written into runtime
> literals. `MOD-0307` remains blocked for HCM use until future EA canonical
> MOD assignment.

## 1. Module Summary

`CAND-CAP-0028` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-C capability named `Competency & Skills Assessment`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0307`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0028` as a temporary governance identity pending
future canonical MOD allocation.

This done pack records the completed first metadata-only backend/API
competency/skills readiness contract slice. Competency/skills assessment
workflow, skill scoring, rating, ranking, calibration, automated decision
behavior, manager/employee assessment UX, notification/document integration,
and sensitive persistence remain closed.

## 2. Ownership and Boundaries

Owned by this done pack:

- HCM-native competency and skills assessment governance boundary.
- Sequencing after completed HCM employee, governance, position, offboarding,
  applicant intake, candidate pipeline, offer management, onboarding,
  employment change, and performance review foundation modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only competency and skills assessment readiness backend/API contract.
- Explicit exclusion of assessment workflow execution.
- Explicit exclusion of skill scoring, rating, ranking, calibration, model
  output, and automated decision behavior.
- Explicit exclusion of manager-facing and employee-facing assessment UX.
- Explicit exclusion of free-text assessment notes, appraisal narrative,
  attachment payload, raw provider payload, credential/token/secret/password,
  and PII-heavy persistence.

Not owned by this pack:

- Real competency assessment execution, assessor submissions, employee
  acknowledgements, review committees, calibration sessions, scores, ratings,
  rankings, or model output.
- Skill taxonomy ownership beyond dependency/context references.
- Frontend, Gateway routes, tenant shell navigation, or read-only UI.
- Notification, notification provider, controlled document, or external
  document repository implementation.
- Performance review, learning, development plan, succession, analytics, TEP,
  or PSS ownership transfer.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| CompetencySkillsAssessmentBoundary | Governance boundary | Defines what a future HCM competency/skills module may own. |
| AssessmentReadinessBoundary | Governance boundary | Separates readiness metadata from assessment workflow execution. |
| SkillScoringBoundary | Governance boundary | Blocks skill score, rating, rank, calibration, and model output persistence. |
| AssessmentUxBoundary | Governance boundary | Blocks manager and employee assessment UX until separately approved. |
| AssessmentSensitiveDataBoundary | Governance boundary | Blocks narrative, attachment, credential, and PII-heavy persistence. |
| AssessmentDependencyBoundary | Governance boundary | Keeps notification/document dependencies deferred and out of scope. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| CompetencyAssessmentWorkflow | Deferred runtime | Assessment execution; not authorized. |
| SkillScoringRatingEngine | Deferred runtime | Scoring, rating, ranking, calibration, and model output; not authorized. |
| ManagerEmployeeAssessmentExperience | Deferred frontend/runtime | Manager/employee assessment UX; not authorized. |
| AssessmentDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| AssessmentNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Only metadata-only readiness runtime objects are approved by this pack.

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields:

- `Code`
- `DisplayName`
- `CompetencySkillsReadinessState`
- `AssessmentWorkflowBoundaryState`
- `CompetencyFrameworkDependencyState`
- `SkillTaxonomyDependencyState`
- `SkillScoringBoundaryState`
- `RatingBoundaryState`
- `RankingBoundaryState`
- `CalibrationBoundaryState`
- `AutomatedDecisionBoundaryState`
- `ManagerAssessmentUxBoundaryState`
- `EmployeeAssessmentUxBoundaryState`
- `NotificationDependencyState`
- `DocumentDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `CompetencySkillsReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Forbidden field classes:

- Assessment workflow body, submission payload, acknowledgement payload,
  approval decision body, action evidence, or completed assessment content.
- Skill score, rating value, rank, calibration outcome, model output,
  automated decision result, recommendation output, or evaluator scoring
  payload.
- Free-text assessment notes, appraisal narrative, manager comments, employee
  comments, HR notes, competency concern narrative, or sensitive assessment
  body.
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

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0028-competency-skills-assessment.md`

Candidate future runtime service:

- `Diten.HumanCapitalService`

Runtime repo scope:

- `services/Diten.HumanCapitalService/**`

## 6. Protected Paths

This done pack records only the first metadata-only backend/API
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

TEP context dependencies:

- `CAND-CAP-0011` through `CAND-CAP-0021` remain dependency/context only.

PSS backbone context:

- `MOD-0288`, `MOD-0251`, `MOD-0279`, `MOD-0280`, and `MOD-0281` remain
  dependency/backbone context only.

## 8. Runtime Guard

Metadata-only backend/API readiness slice is authorized.

Authorized runtime boundary:

- Runtime owner/key: `hcm.competency-skills`.
- Permission namespace:
  - `hcm.competency-skills.read`
  - `hcm.competency-skills.manage`
  - `hcm.competency-skills.evaluate`
  - `hcm.competency-skills.audit.read`
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
- Competency/skills assessment workflow.
- Skill score/rating/rank/calibration/model-output persistence.
- Automated decision behavior.
- Manager/employee assessment UX.
- Notification/document integrations.
- Sensitive payload persistence.

## 9. Frontend / UX Boundary

Frontend and gateway remain closed.

Unauthorized UX:

- Tenant shell menu/page.
- Restricted read-only page.
- Manager assessment UX.
- Employee assessment UX.
- Assessment form, workflow, calibration, rating, ranking, or model-output UI.
- Document, notification, export, upload, or attachment UI.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized.

The first slice must stay limited to:

- Create/list/get competency/skills readiness metadata.
- Audit metadata read endpoint.
- Fail-closed evaluation/deferred metadata.
- Tenant-aware persistence and active-code uniqueness.
- No workflow, scoring, rating, ranking, calibration, automated decision,
  manager/employee UX, notification/document integration, or sensitive payload
  persistence.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only competency/skills readiness state.
- Boundary-state metadata for workflow, scoring, rating, ranking, calibration,
  automated decision, UX, notification, document, consent, minimization,
  retention, and evidence.
- Dependency/precondition metadata.

Forbidden in the first slice:

- Skill score/rating/rank/calibration outcome/model output.
- Assessment workflow payload.
- Free-text assessment notes or appraisal narrative.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy employee/profile details.
- Compensation/payroll/benefits payload.

## 12. Permission Boundary

Approved permission namespace:

- `hcm.competency-skills.read`
- `hcm.competency-skills.manage`
- `hcm.competency-skills.evaluate`
- `hcm.competency-skills.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Future runtime work must be tenant-aware and fail closed. The first backend/API
slice must resolve tenant server-side and must not expose `TenantId` in request
DTOs.

Security guardrails:

- Candidate identity must not become a runtime literal.
- Blocked legacy ID must not become a runtime literal.
- Backend authorization must remain authoritative if a future API slice is
  approved.
- Frontend permission checks, if later approved, must remain UX-only.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation.
- Data minimization fail-closed behavior.
- Retention/evidence/audit/legal-hold/deletion as local/deferred metadata.
- Exclusion of sensitive assessment narrative, scoring payloads, and PII-heavy
  data.

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
2. AC-02: Candidate identity is `CAND-CAP-0028`.
3. AC-03: Legacy Excel ID `MOD-0307` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `hcm.competency-skills`.
5. AC-05: Permission namespace is limited to `hcm.competency-skills.read`,
   `hcm.competency-skills.manage`, `hcm.competency-skills.evaluate`, and
   `hcm.competency-skills.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.HumanCapitalService/**`.
7. AC-07: First slice creates metadata-only competency/skills readiness
   contract.
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Tenant-aware persistence includes active tenant `Code` uniqueness.
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt`.
11. AC-11: Evaluation/activation remains fail-closed and can produce
   non-activating `Deferred` metadata.
12. AC-12: Assessment workflow remains unauthorized.
13. AC-13: Skill scoring, rating, ranking, calibration, model output, and
   automated decision behavior remain unauthorized.
14. AC-14: Manager/employee assessment UX remains unauthorized.
15. AC-15: Free-text assessment notes, appraisal narrative, attachment payload,
   raw provider payload, credential/token/secret/password, and PII-heavy
   persistence remain unauthorized.
16. AC-16: Notification/document dependencies remain deferred/out of scope.
17. AC-17: HCM, TEP, and PSS dependency boundaries are recorded.
18. AC-18: Candidate gate script absence is recorded as a governance note.
19. AC-19: Frontend and gateway remain closed until backend/API review/done
   gates.
20. AC-20: Runtime literal scan for `CAND-CAP-0028|MOD-0307` passes under
   runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: done`.
- Confirm all 20 sections are present.
- Build HCM API:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `CompetencySkills` filter.
- Run full HCM Application tests.
- Verify metadata contract create/list/get behavior.
- Verify tenant isolation.
- Verify active tenant `Code` uniqueness.
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface.
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden workflow/scoring/rating/ranking/calibration/decision/
  sensitive fields are absent.
- Verify no production in-memory repository exists.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0028|MOD-0307" services/Diten.HumanCapitalService frontend gateway tests`

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance-only approval.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Runtime/API/frontend/gateway prohibition is accepted.
- [x] Assessment workflow prohibition is accepted.
- [x] Skill scoring/rating/ranking/calibration/model-output prohibition is
  accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Sensitive payload prohibition is accepted.
- [x] Notification/document dependency deferral is accepted.
- [x] HCM/TEP/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only competency/skills readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Data minimization policy.
- [x] Retention/evidence/audit/legal-hold/deletion boundary.
- [x] Frontend/gateway remain closed until backend/API review/done gates.

Runtime-ready blockers:

- none.

Review-ready reconciliation note:

- Implementation review PASS.
- Metadata-only backend/API slice completed.
- Runtime owner/key remains limited to `hcm.competency-skills`.
- Permission namespace remains limited to `hcm.competency-skills.read`,
  `hcm.competency-skills.manage`, `hcm.competency-skills.evaluate`, and
  `hcm.competency-skills.audit.read`.
- Build PASS: 0 warning, 0 error.
- CompetencySkills targeted tests PASS: 32/32.
- Full HCM Application tests PASS: 239/239.
- Runtime literal scan PASS: `CAND-CAP-0028|MOD-0307` no matches.
- Production in-memory repository scan PASS.
- Section 4 metadata fields are aligned with the runtime public/persisted
  contract.
- `SkillTaxonomyDependencyState` remains metadata-only dependency state.
- Frontend, Gateway, TEP, PSS, and Platform scopes remain closed.
- Competency/skills workflow, skill scoring, rating, ranking, calibration,
  automated decision behavior, manager/employee assessment UX,
  notification/document integration, free-text assessment notes, appraisal
  narrative, attachment payload, skill score/rating/rank/calibration outcome,
  model output, raw provider payload, credential/token/secret/password, and
  PII-heavy persistence remain out of scope.

Done-promotion reconciliation note:

- Done-promotion readiness audit PASS.
- Pack status promoted from `review` to `done`.
- Open blockers remain none.
- Metadata-only backend/API slice remains completed and validated.
- Runtime owner/key remains limited to `hcm.competency-skills`.
- Permission namespace remains limited to `hcm.competency-skills.read`,
  `hcm.competency-skills.manage`, `hcm.competency-skills.evaluate`, and
  `hcm.competency-skills.audit.read`.
- Build PASS: 0 warning, 0 error.
- CompetencySkills targeted tests PASS: 32/32.
- Full HCM Application tests PASS: 239/239.
- Runtime literal scan PASS: `CAND-CAP-0028|MOD-0307` no matches.
- Production in-memory repository scan PASS.
- Section 4 metadata fields remain aligned with the runtime public/persisted
  contract.
- `SkillTaxonomyDependencyState` remains metadata-only dependency state.
- Gateway and frontend remain future follow-up.
- Competency/skills workflow, skill scoring, rating, ranking, calibration,
  automated decision behavior, manager/employee assessment UX, free-text
  assessment notes, appraisal narrative, attachment payload, skill
  score/rating/rank/calibration outcome/model output, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence remain out of
  scope.

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
  `CAND-CAP-0028`.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice.
- Legal/privacy/consent metadata-only waiver approved.
- Competency/skills data minimization fail-closed policy approved.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0028|MOD-0307" services/Diten.HumanCapitalService frontend gateway tests`

Next safe step:

- HCM Competency Skills gateway exposure.

Future follow-ups:

- Real competency/skills assessment workflow.
- Skill scoring/rating/ranking/calibration.
- Automated decision approval.
- Manager/employee assessment UX.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
