---
id: CAND-CAP-0023
name: Candidate Pipeline & Interview Management
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0023-candidate-pipeline-interview-management
started: 2026-09-01
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0023
legacy_excel_id: MOD-0301
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
bootstrap_type: completed-first-slice
runtime_owner_key: hcm.candidate-pipeline
permission_namespace: hcm.candidate-pipeline.*
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0023 - Candidate Pipeline & Interview Management

> Status: done. Done-promotion readiness audit PASS confirms the first
> metadata-only candidate pipeline/interview readiness backend/API slice under
> `services/Diten.HumanCapitalService/**` is complete.
> Runtime owner/key is `hcm.candidate-pipeline`; permission namespace is
> `hcm.candidate-pipeline.*`.
> This done status does not authorize frontend, Gateway exposure, candidate-facing
> scheduling/application UX, real interview workflow, interview note persistence,
> free-text evaluation persistence, resume/CV body persistence, attachment
> payload persistence, notification/document integration, scoring/ranking/
> automated decision behavior, raw provider payload persistence,
> credential/token/secret persistence, or PII-heavy persistence.
> `CAND-CAP-0023` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0301` remains blocked for HCM use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0023` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-A capability named `Candidate Pipeline & Interview
Management`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0301`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0023` as a temporary governance identity pending
future canonical MOD allocation.

EA has approved candidate-based governance continuation and a candidate-runtime
policy waiver for the first metadata-only backend/API slice. Done-promotion
readiness audit has passed for that backend/API slice. The completed slice is limited to
candidate pipeline stage metadata, interview scheduling readiness, interviewer
assignment readiness, and evaluation governance readiness.
Candidate-facing scheduling UX, real interview workflow, interview notes,
evaluation forms, notification/document integrations, and automated decision
behavior remain future follow-ups.

## 2. Ownership and Boundaries

Owned by this governance-approved pack:

- HCM-native candidate pipeline and interview management governance boundary.
- Sequencing after completed HCM applicant intake and foundation modules.
- Runtime owner/key `hcm.candidate-pipeline`.
- Permission namespace `hcm.candidate-pipeline.*`.
- First-slice metadata-only readiness contract for pipeline stage governance,
  interview scheduling readiness, interviewer assignment readiness, and
  evaluation governance.
- Explicit exclusion of candidate-facing scheduling/application UX.
- Explicit exclusion of interview notes, free-text evaluation, resume/CV body,
  attachments, notification/document integrations, scoring/ranking, and
  automated decision behavior.

Not owned by this pack:

- Frontend, Gateway routes, tenant shell navigation, or read-only UI.
- Candidate-facing public application or scheduling UX.
- Interview body notes, free-text evaluations, attachments, resume/CV bodies, or
  document repository payloads.
- Notification, notification provider, controlled document, or external document
  repository integration.
- TEP candidate identity, dispute, recommendation, reference exchange, or
  marketplace ownership.
- PSS organization/person/position directory, HRIS, payroll, time, or provider
  data ownership.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| CandidatePipelineBoundary | Governance boundary | Defines what a future HCM pipeline module may own. |
| InterviewReadinessBoundary | Governance boundary | Separates scheduling readiness metadata from actual scheduling workflows. |
| InterviewerAssignmentBoundary | Governance boundary | Defines interviewer assignment readiness without calendar, notification, or participant messaging. |
| EvaluationGovernanceBoundary | Governance boundary | Blocks interview notes, free-text evaluation, scoring/ranking, and automated decisions until approved. |
| PipelineDataMinimizationPolicy | Governance boundary | Blocks PII-heavy and narrative application/interview content from the first slice. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| CandidatePipelineReadinessMetadata | Deferred runtime entity | Potential metadata-only readiness record if runtime-ready approval is granted. |
| InterviewScheduleWorkflow | Deferred runtime | Actual interview scheduling workflow; not authorized. |
| CandidateSchedulingExperience | Deferred frontend/runtime | Candidate-facing scheduling UX; not authorized. |
| InterviewEvaluationWorkflow | Deferred runtime | Interview feedback, notes, forms, and evaluation workflow; not authorized. |
| NotificationIntegration | Deferred dependency | Candidate/recruiter/interviewer notifications; out of scope. |
| DocumentIntegration | Deferred dependency | Resume, attachments, templates, evidence, and controlled/external document integrations; out of scope. |

First-slice runtime objects approved for implementation:

| Object | Type | Purpose |
|---|---|---|
| CandidatePipelineReadinessMetadata | Runtime entity | Persists tenant-owned metadata-only candidate pipeline/interview readiness records without notes, free-text evaluations, resume/CV bodies, attachments, scoring outputs, or PII-heavy data. |
| CandidatePipelineReadinessDto | API response DTO | Exposes only the approved metadata contract through `Response<T>`. |
| CreateCandidatePipelineReadinessCommand | CQRS command | Creates metadata-only readiness records; `TenantId` is resolved server-side. |
| EvaluateCandidatePipelineReadinessCommand | CQRS command | Produces fail-closed readiness/deferred metadata without scheduling workflow, interview workflow, scoring, ranking, or automated decision execution. |
| CandidatePipelineReadinessRepository | Persistence contract | Provides Mongo-backed, tenant-aware storage with active tenant `Code` uniqueness. |

## 4. Entity Fields

The first backend/API slice must expose and persist only this metadata contract:

- `Code`
- `DisplayName`
- `PipelineReadinessState`
- `PipelineStageGovernanceState`
- `InterviewSchedulingReadinessState`
- `InterviewerAssignmentReadinessState`
- `EvaluationGovernanceState`
- `CandidateCommunicationBoundaryState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `CalendarDependencyState`
- `NotificationDependencyState`
- `DocumentDependencyState`
- `AutomatedDecisionBoundaryState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `PipelineReadinessVersion`
- `DeferredReason`

Contract rules:

- `TenantId` must not be accepted on request DTOs; tenant ownership is resolved
  server-side.
- Activation/evaluation must fail closed when required pipeline, scheduling,
  interviewer assignment, consent, minimization, retention/evidence, or
  dependency preconditions are missing.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Interview scheduling and interviewer assignment details must be expressed as
  readiness metadata only.
- Evaluation governance must remain metadata-only and must not create scoring,
  ranking, free-text evaluation, or automated decision behavior.

Forbidden field classes:

- Interview notes, free-text evaluation, candidate response text, screening
  narrative, recommendation notes, or reviewer narrative.
- Resume/CV body, cover letter body, free-text application narrative, attachment
  payload, document payload, raw evidence, or raw provider payload.
- Score, rank, model output, eligibility label, automated decision result, or
  recommendation result.
- Credential, token, secret, password, provider connection value, bank/tax/
  payroll data, payslip, biometric/geolocation data, national ID, DOB, home
  address, salary, wage, or PII-heavy applicant/candidate profile fields.

## 5. Repo Scope

Authorized scope for this done promotion change:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0023-candidate-pipeline-interview-management.md`

Authorized runtime scope for the completed first metadata-only backend/API slice:

- `services/Diten.HumanCapitalService/**`

## 6. Protected Paths

This done pack must not change:

- `services/**`, except `services/Diten.HumanCapitalService/**` during the
  approved backend/API implementation step.
- `frontend/**`
- `gateway/**`
- global `tests/**`, except `services/Diten.HumanCapitalService/tests/**`
- `execution/domains/talent-ecosystem-platform/**`
- `execution/domains/platform-shared-services/**`
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/human-capital-management/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`
- `execution/portfolio/frontend-completion-reconciliation.md`

Completed HCM foundation dependencies:

- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, done.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, done.
- `CAND-CAP-0009` - Position & Organization Assignment, done.
- `CAND-CAP-0010` - Offboarding & Exit Management, done.
- `CAND-CAP-0022` - Recruitment / Applicant Intake, done.

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

- Runtime implementation is authorized only for the first metadata-only
  backend/API slice under `services/Diten.HumanCapitalService/**`.
- Runtime owner/key is `hcm.candidate-pipeline`.
- Permission namespace is limited to:
  - `hcm.candidate-pipeline.read`
  - `hcm.candidate-pipeline.manage`
  - `hcm.candidate-pipeline.evaluate`
  - `hcm.candidate-pipeline.audit.read`
- `CAND-CAP-0023` is a governance/documentation identity only and must never be
  written into runtime literals, permission seeds, route metadata, database
  records, job names, telemetry owner fields, config keys, or test fixtures.
- `MOD-0301` must not be used for this HCM capability until EA assigns a
  canonical MOD.
- Tenant isolation must be server-side and must not accept `TenantId` from
  request DTOs.
- Evaluation/activation paths must fail closed when pipeline stage, scheduling,
  interviewer assignment, consent, minimization, retention/evidence, or
  dependency preconditions are missing.
- Candidate-facing scheduling/application UX remains blocked.
- Notification/document integrations remain blocked and out of scope.
- Scoring/ranking/automated decision behavior remains blocked.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

This pack does not create an HCM page, workspace menu, Razor view, JavaScript
file, DataTable, RESX resource, layout binding, or frontend route.

Future UI exposure requires separate backend/API, Gateway, and frontend readiness
decisions. The first UI, if approved later, must be restricted/admin read-only
metadata only.

## 10. Backend File Convention

Backend implementation must follow the repo 5-layer .NET service convention,
CQRS/MediatR patterns, `Response<T>` envelope, server-side tenant resolution,
Mongo-backed repository standard, soft delete rules, and approved permission
conventions.

Expected feature naming:

- Feature folder: `CandidatePipeline`
- Controller: `CandidatePipelineController`
- Repository contract: `ICandidatePipelineReadinessMetadataRepository`
- Mongo repository: `MongoCandidatePipelineReadinessMetadataRepository`
- Runtime guard/owner key: `hcm.candidate-pipeline`.

## 11. Frontend File Contract

Frontend implementation is N/A for this completed backend/API pack.

No Razor, JavaScript, DataTable, RESX, menu, route, API client, GatewayUrl
binding, candidate-facing scheduling UX, or public application UX is authorized.

Any future frontend must be restricted/admin read-only metadata unless a separate
legal, privacy, consent, candidate-facing UX, and data minimization approval
explicitly authorizes more.

## 12. API Surface Contract

API surface approved for the first metadata-only backend/API slice:

- `GET /api/candidate-pipeline`
- `GET /api/candidate-pipeline/{id}`
- `POST /api/candidate-pipeline`
- `POST /api/candidate-pipeline/{id}/evaluate`
- `DELETE /api/candidate-pipeline/{id}`
- `GET /api/candidate-pipeline/{id}/audit-metadata`

Any future API must expose only readiness metadata and must not expose interview
notes, free-text evaluations, resume/CV bodies, attachments, raw payloads,
scores, ranks, automated decision results, or PII-heavy details. The `DELETE`
endpoint, if approved, must implement soft delete only.

## 13. Data Boundary

Allowed in this completed first-slice boundary:

- Governance metadata.
- Dependency sequencing notes.
- Readiness blockers and waiver placeholders.
- Pipeline stage, scheduling, interviewer assignment, and evaluation governance
  boundaries as planning concepts only.

Forbidden:

- Candidate-facing scheduling/application UX.
- Interview notes, free-text evaluation, screening narrative, candidate response
  text, or reviewer narrative.
- Resume/CV body, cover letter body, free-text application narrative,
  attachment/document payload, raw evidence, or raw provider payload.
- Scoring/ranking/analytics, automated decision behavior, model output,
  eligibility labels, recommendation outcomes, or recalculation workflows.
- Credential, token, secret, password, or provider connection value.
- National ID, DOB, home address, bank, tax, payroll, payslip, biometric,
  geolocation, salary, wage, or PII-heavy applicant/candidate profile data.
- Notification delivery or document repository integration.

## 14. Permissions & Security

Approved permission namespace:

- `hcm.candidate-pipeline.read`
- `hcm.candidate-pipeline.manage`
- `hcm.candidate-pipeline.evaluate`
- `hcm.candidate-pipeline.audit.read`

Backend `[HasPermission]` enforcement remains authoritative. Any future
frontend permission behavior must remain UX-only hide/disable behavior.

Security constraints for runtime:

- Candidate pipeline lawful basis and consent model.
- Applicant/candidate data minimization.
- Interview scheduling and interviewer assignment authorization.
- Interview evidence, retention, legal hold, and deletion policy.
- Evaluation governance and free-text/narrative exclusion boundary.
- Automated decision, scoring, ranking, and analytics exclusion boundary.
- Notification/document dependency deferral.

## 15. Gateway / API Routing Decision

Gateway routing is N/A for this done promotion.

No Gateway route, Ocelot entry, frontend API proxy, or service port exposure is
authorized by this pack update. Gateway exposure remains the next future
follow-up because the backend/API slice has passed done-promotion readiness.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `done`.
2. AC-02: Candidate identity is `CAND-CAP-0023`.
3. AC-03: Legacy Excel ID `MOD-0301` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Completed backend/API implementation is limited to
   `services/Diten.HumanCapitalService/**`.
5. AC-05: Frontend and Gateway implementation remain future follow-ups and are
   not authorized by this done promotion.
6. AC-06: Candidate-facing scheduling/application UX is not authorized.
7. AC-07: Interview notes, free-text evaluation, resume/CV body, and attachment
   payload persistence are blocked.
8. AC-08: Notification/document integrations remain deferred/out of scope.
9. AC-09: Scoring/ranking/automated decision behavior is blocked.
10. AC-10: `TenantId` is never accepted by request DTOs.
11. AC-11: Mongo repository is tenant-aware and has active tenant `Code`
   uniqueness.
12. AC-12: Soft delete sets `IsDeleted` and `DeletedAt` and hides deleted rows.
13. AC-13: Evaluation/activation is fail-closed and can produce explicit
   `Deferred` metadata.
14. AC-14: Runtime owner/key is exactly `hcm.candidate-pipeline`.
15. AC-15: Permission namespace is limited to the four approved permissions.
16. AC-16: Forbidden interview/free-text/resume/attachment/scoring/sensitive
   fields are absent from public and persisted contracts.

## 17. Test Expectations

Runtime tests completed for the first backend/API slice:

- Create/List/Get metadata contract.
- Tenant isolation.
- Active tenant `Code` uniqueness.
- Soft delete hides deleted records and sets `DeletedAt`.
- Permission-gated controller surface.
- Dependency/precondition fail-closed behavior.
- Non-activating evaluation deferred metadata behavior.
- Forbidden interview/free-text/resume/attachment/scoring/sensitive field guard.
- Runtime literal regression guard.

Validation commands for the first implementation slice:

- `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- `dotnet test services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/Diten.HumanCapitalService.Application.Tests.csproj -c Debug --no-restore --filter CandidatePipeline`
- `dotnet test services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/Diten.HumanCapitalService.Application.Tests.csproj -c Debug --no-restore`
- `rg -n "CAND-CAP-0023|MOD-0301" services/Diten.HumanCapitalService frontend gateway tests`
- `rg -n "InMemory.*CandidatePipeline|CandidatePipeline.*InMemory" services/Diten.HumanCapitalService/src`

Review-ready reconciliation note:

- Implementation review status: PASS.
- Metadata-only backend/API slice completed under
  `services/Diten.HumanCapitalService/**`.
- Runtime owner/key remained exactly `hcm.candidate-pipeline`.
- Permission namespace remained limited to:
  - `hcm.candidate-pipeline.read`
  - `hcm.candidate-pipeline.manage`
  - `hcm.candidate-pipeline.evaluate`
  - `hcm.candidate-pipeline.audit.read`
- Build PASS: 0 warning, 0 error.
- CandidatePipeline targeted tests PASS: 20/20.
- Full HCM Application tests PASS: 98/98.
- Runtime literal scan PASS: `CAND-CAP-0023|MOD-0301` no matches.
- Production in-memory repository scan PASS.
- Frontend/Gateway/TEP/PSS/Platform scope remained closed.
- Candidate-facing scheduling/application UX, interview notes/free-text
  evaluation, resume/CV body, attachment payload, notification/document
  integration, scoring/ranking, and automated decision behavior remained out of
  scope.

Done-promotion reconciliation note:

- Done-promotion readiness audit status: PASS.
- Pack status promoted from `review` to `done`.
- Open blockers: none.
- Metadata-only backend/API slice completed under
  `services/Diten.HumanCapitalService/**`.
- Runtime owner/key remained exactly `hcm.candidate-pipeline`.
- Permission namespace remained limited to:
  - `hcm.candidate-pipeline.read`
  - `hcm.candidate-pipeline.manage`
  - `hcm.candidate-pipeline.evaluate`
  - `hcm.candidate-pipeline.audit.read`
- Build PASS: 0 warning, 0 error.
- CandidatePipeline targeted tests PASS: 20/20.
- Full HCM Application tests PASS: 98/98.
- Runtime literal scan PASS: `CAND-CAP-0023|MOD-0301` no matches.
- Production in-memory repository scan PASS.
- Frontend/Gateway remain future follow-ups.
- Candidate-facing scheduling/application UX, interview notes/free-text
  evaluation, resume/CV body, attachment payload, notification/document
  integration, scoring/ranking, automated decision behavior, raw provider
  payload, credential/token/secret/password, and PII-heavy persistence remained
  out of scope.

## 18. Governance Approval Checklist

- [x] EA approves candidate-based governance continuation.
- [x] Registry owner accepts candidate gate result or verifier absence note.
- [x] `CAND-CAP-0023` remains documentation identity only.
- [x] `MOD-0301` remains blocked until EA canonical MOD assignment.
- [x] First metadata-only backend/API implementation passed review.
- [x] Frontend/Gateway implementation remains closed.
- [x] Candidate-facing scheduling/application UX remains closed.
- [x] Interview notes, free-text evaluation, resume/CV body, and attachment
  payload persistence remain closed.
- [x] Notification/document dependencies remain deferred/out of scope.
- [x] Scoring/ranking/automated decision behavior remains closed.
- [x] Sensitive/raw/PII-heavy data boundary is accepted.

## 19. Runtime-Ready Checklist

Runtime-ready checklist:

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Minimal metadata-only contract field list approval.
- [x] Candidate pipeline lawful basis and consent decision.
- [x] Applicant/candidate data minimization decision.
- [x] Interview scheduling authorization and ownership decision.
- [x] Interviewer assignment authorization and ownership decision.
- [x] Evaluation governance and free-text/narrative exclusion decision.
- [x] Retention/evidence/audit/legal-hold/deletion decision.
- [x] Notification/document dependencies stay deferred or receive separate
  approved module-pack readiness.
- [x] Scoring/ranking/automated decision behavior stays blocked or receives a
  separate legal/security approval.
- [x] Tenant isolation and fail-closed evaluation rules become testable.

Open blockers for governance continuation:

- None after EA/registry owner approval.

Open blockers for runtime-ready:

- None for the approved first metadata-only candidate pipeline/interview
  readiness backend/API slice.

## 20. Notes, Waivers, and Future Follow-Up

Candidate gate result:

- Candidate gate script not executable in this checkout; reservation recorded.

Runtime literal scan:

- To be verified with
  `rg -n "CAND-CAP-0023|MOD-0301" services/Diten.HumanCapitalService frontend gateway tests`.

Current waivers:

- Governance continuation waiver approved for candidate-based documentation
  identity.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API slice.
- Legal/privacy/consent metadata-only waiver approved.
- Applicant/pipeline data minimization fail-closed boundary approved.
- Interview scheduling and interviewer assignment metadata-only readiness
  boundary approved.
- Evaluation governance approved only without free-text evaluation,
  scoring/ranking, or automated decision behavior.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata waiver
  approved.
- Notification/document dependencies remain deferred/out of scope.

Future follow-ups:

- Gateway exposure, because the backend/API implementation has passed
  done-promotion readiness.
- Restricted read-only frontend slice, if gateway exposure is later approved.
- Candidate-facing scheduling UX.
- Interview scheduling and interviewer assignment workflows.
- Interview evaluation workflow.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
- Scoring/ranking/automated decision legal/security review.
