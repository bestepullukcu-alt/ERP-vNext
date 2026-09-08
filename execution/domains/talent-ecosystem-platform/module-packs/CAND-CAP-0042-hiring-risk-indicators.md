---
id: CAND-CAP-0042
name: Hiring Risk Indicators
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0042-hiring-risk-indicators
started: 2026-09-07
target: 2026-12-08
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0042
legacy_excel_id: MOD-0332
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.TalentEcosystemService
runtime_service_port: 5060
gateway_port: 5080
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.hiring-risk-indicators
permission_namespace:
  - tep.hiring-risk-indicators.read
  - tep.hiring-risk-indicators.manage
  - tep.hiring-risk-indicators.evaluate
  - tep.hiring-risk-indicators.audit.read
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0042 - Hiring Risk Indicators

> Status: draft. This pack records the first metadata-only backend/API hiring
> risk indicators readiness contract slice under
> `services/Diten.TalentEcosystemService/**`, plus a golden-compact readiness
> CRUD frontend under `frontend/Diten.Web/**` and gateway exposure through the
> API gateway (`5080` -> TEP `5060`). Real risk scoring/rating, computed risk
> ratings, raw risk signal payload persistence, candidate/applicant PII or
> background-check/credit content persistence, adverse-action decisions,
> mitigation execution, model output or automated decision behavior,
> manager/employee/candidate risk-indicator UX, notification/document
> integration, and sensitive/PII-heavy persistence remain closed.
> `CAND-CAP-0042` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0332` remains blocked for TEP use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0042` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R4 capability named `Hiring Risk Indicators`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0332`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0042` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API hiring risk indicators
readiness contract slice, mirroring the completed TEP candidate readiness slices
(`CAND-CAP-0017` / `MOD-0322` through `CAND-CAP-0021` / `MOD-0331`) and the
completed `CAND-CAP-0041` / `MOD-0336` Talent Data Foundation slice exactly. The
Hiring Risk Indicators capability is the TEP-native readiness layer for
hiring-risk-indicator capabilities: it declares the readiness STATE of the risk
indicator catalog, risk signal intake, risk assessment, mitigation tracking, and
indicator review. It depends on the shared Talent Data Foundation
(`CAND-CAP-0041` / `MOD-0336`) as its talent-data source. It stores NO risk
scores, NO computed risk ratings, NO candidate/applicant PII, NO
background-check/credit content, NO adverse-action decisions, and NO raw signal
payloads - only boundary/readiness STATE metadata. Real risk scoring/rating,
computed risk ratings, raw risk signal payload persistence, candidate/applicant
PII or background-check/credit content persistence, adverse-action decisions,
mitigation execution, model output, automated decision behavior,
manager/employee/candidate risk-indicator UX, notification/document integration,
and sensitive/PII-heavy persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- TEP-native hiring risk indicators governance boundary.
- Sequencing after completed HCM foundation modules, completed TEP foundation and
  candidate readiness modules through `CAND-CAP-0021`, and the completed
  `CAND-CAP-0041` Talent Data Foundation data-source dependency.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only hiring risk indicators readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the risk indicator catalog, risk signal
  intake, risk assessment, mitigation tracking, and indicator review.
- Explicit exclusion of real risk scoring/rating and computed risk rating
  execution.
- Explicit exclusion of risk signal intake execution, risk assessment execution,
  mitigation execution, indicator review execution, model output, and automated
  decision behavior.
- Explicit exclusion of manager-facing, employee-facing, and candidate-facing
  risk-indicator UX beyond the readiness-metadata CRUD (those fields are reserved
  and out of scope).
- Explicit exclusion of risk scores, computed risk ratings, raw risk signal
  payloads, candidate/applicant PII, background-check/credit content,
  adverse-action decisions, attachment payload, raw provider payload,
  credential/token/secret/password, and any PII dataset persistence.

Not owned by this pack:

- Real risk scoring, risk rating, or computed risk-rating execution.
- Raw risk signal payload, background-check content, or credit content storage.
- Risk signal intake execution, ingestion runs, or signal collection output.
- Risk assessment execution, evaluation runs, or assessment output.
- Mitigation execution, remediation workflow runtime, or mitigation action logs.
- Adverse-action decisions, adjudication output, or automated hiring decisions.
- Talent-data ownership beyond dependency/context references (owned by
  `CAND-CAP-0041` Talent Data Foundation).
- Candidate identity/profile ownership beyond dependency/context references
  (owned by `CAND-CAP-0017`).
- Notification, notification provider, controlled document, or external document
  repository implementation.
- HCM, PSS directory, HRIS, payroll, time, analytics, or provider ownership
  transfer.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| HiringRiskIndicatorsCapabilityBoundary | Governance boundary | Defines what a future TEP hiring risk indicators module may own. |
| HiringRiskIndicatorsReadinessBoundary | Governance boundary | Separates readiness metadata from risk scoring/assessment execution. |
| RiskIndicatorCatalogBoundary | Governance boundary | Blocks raw risk indicator catalog record persistence. |
| RiskSignalIntakeBoundary | Governance boundary | Blocks real risk signal intake, ingestion, and raw signal payload persistence. |
| RiskAssessmentBoundary | Governance boundary | Blocks risk assessment execution, scoring, and assessment output. |
| MitigationTrackingBoundary | Governance boundary | Blocks mitigation execution, remediation runtime, and mitigation action logs. |
| IndicatorReviewBoundary | Governance boundary | Blocks indicator review execution, adjudication, and review output. |
| HiringRiskIndicatorsDecisionBoundary | Governance boundary | Blocks scoring, rating, model-output, adverse-action, and automated decision persistence. |
| HiringRiskIndicatorsUxBoundary | Governance boundary | Blocks manager, employee, and candidate risk-indicator UX beyond readiness-metadata CRUD. |
| HiringRiskIndicatorsSensitiveDataBoundary | Governance boundary | Blocks risk scores, PII, background-check/credit content, raw signal payload, and adverse-action persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| RiskSignalIntakeEngine | Deferred runtime | Real risk signal intake, ingestion, and raw signal payload capture; not authorized. |
| RiskAssessmentEngine | Deferred runtime | Risk assessment execution, scoring, and rating output; not authorized. |
| MitigationTrackingEngine | Deferred runtime | Mitigation execution, remediation workflow, and mitigation action logs; not authorized. |
| IndicatorReviewEngine | Deferred runtime | Indicator review execution, adjudication, and adverse-action output; not authorized. |
| ManagerEmployeeCandidateRiskExperience | Deferred frontend/runtime | Manager/employee/candidate risk-indicator UX beyond readiness CRUD; not authorized. |
| HiringRiskDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| HiringRiskNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| HiringRiskIndicatorsReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/HiringRiskIndicatorsReadinessMetadata.cs`. |
| HiringRiskIndicatorsReadinessState | Domain enum | Readiness state; `Domain/Enums/HiringRiskIndicatorsReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IHiringRiskIndicatorsReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/IHiringRiskIndicatorsReadinessMetadataRepository.cs`. |
| MongoHiringRiskIndicatorsReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `tep_hiring_risk_indicators_readiness`; indexes `ux_tep_hiring_risk_indicators_tenant_code_active` and `ix_tep_hiring_risk_indicators_tenant_state`. |
| HiringRiskIndicators Application features | Application (CQRS/MediatR) | `Application/Features/HiringRiskIndicators/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| HiringRiskIndicatorsController | API controller | Thin controller; `Api/Controllers/Tep/HiringRiskIndicatorsController.cs`. |
| HiringRiskIndicators golden-compact CRUD | Frontend | Controller + `Views/TalentEcosystem/HiringRiskIndicators/**` + `wwwroot/assets/js/TalentEcosystem/HiringRiskIndicators/**` + Resources + TEP nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `HiringRiskIndicatorsReadinessState`):

- `Code`
- `DisplayName`
- `HiringRiskIndicatorsReadinessState`
- `RiskIndicatorCatalogBoundaryState`
- `RiskSignalIntakeBoundaryState`
- `RiskAssessmentBoundaryState`
- `MitigationTrackingBoundaryState`
- `IndicatorReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `TalentDataSourceDependencyState`
- `ConsentPolicyDependencyState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `HiringRiskIndicatorsReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Manager-facing risk-indicator fields (manager catalog submission, manager
  review, manager action UX state) are reserved and out of scope.
- Employee-facing risk-indicator fields (employee self-service input, employee
  acknowledgement, employee self-service UX state) are reserved and out of
  scope.
- Candidate-facing risk-indicator fields (candidate response, candidate
  acknowledgement, candidate self-service UX state) are reserved and out of
  scope.
- No money field (salary/compensation/cost/budget). This is a metadata-only
  readiness slice with no monetary attribute.
- No risk score, computed risk rating, candidate/applicant PII,
  background-check/credit content, adverse-action decision, or raw signal payload
  field of any kind.

Forbidden field classes:

- Risk indicator catalog record body, risk signal intake payload, risk
  assessment payload, mitigation execution payload, indicator review payload, or
  completed hiring-risk content.
- Risk score, computed risk rating, rating value, rank, match confidence, model
  output, adverse-action decision result, recommendation output, or evaluator
  scoring payload.
- Candidate/applicant PII, raw risk signal payload, background-check content,
  credit content, cover letter, free-text profile narrative, HR notes, or
  sensitive candidate body.
- Attachment payload, controlled document body, external document content, raw
  evidence, raw provider payload, or uploaded file metadata.
- Salary amount, compensation amount, benefits election, payroll details, tax
  details, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  any PII-heavy candidate/profile field.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0042-hiring-risk-indicators.md`

Candidate runtime service:

- `Diten.TalentEcosystemService` (port `5060`)

Runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/TalentEcosystem/HiringRiskIndicators/**`,
  `wwwroot/assets/js/TalentEcosystem/HiringRiskIndicators/**`, Resources, TEP nav
  entry).
- Gateway route exposure through the API gateway (`5080` -> TEP `5060`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.TalentEcosystemService/**` plus its golden-compact
readiness CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned HiringRiskIndicators CRUD objects listed in
  §3/§5.
- `gateway/**` outside the owned HiringRiskIndicators readiness route.
- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `services/Diten.PssService/**`
- global `tests/**`
- completed TEP foundation and candidate readiness packs (including the
  `CAND-CAP-0041` Talent Data Foundation data-source dependency).
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Talent-data source dependency:

- `CAND-CAP-0041` - Talent Data Foundation (`MOD-0336`), done. Consumed as the
  talent-data source for hiring risk indicators via
  `TalentDataSourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as a raw data source.

Completed TEP foundation and candidate readiness dependencies:

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

HCM foundation dependency context (prerequisite context only):

- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, done.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, done.
- `CAND-CAP-0009` - Position & Organization Assignment, done.
- `CAND-CAP-0010` - Offboarding & Exit Management, done.

PSS backbone context:

- `MOD-0288`, `MOD-0251`, `MOD-0279`, `MOD-0280`, and `MOD-0281` remain
  dependency/backbone context only.

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is
authorized only for the first metadata-only backend/API readiness slice, along
with the golden-compact readiness CRUD frontend and gateway route.

Authorized runtime boundary:

- Runtime owner/key: `tep.hiring-risk-indicators`.
- Permission namespace:
  - `tep.hiring-risk-indicators.read`
  - `tep.hiring-risk-indicators.manage`
  - `tep.hiring-risk-indicators.evaluate`
  - `tep.hiring-risk-indicators.audit.read`
- Runtime repo scope: `services/Diten.TalentEcosystemService/**`.
- Thin controller (`Api/Controllers/Tep/HiringRiskIndicatorsController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `tep_hiring_risk_indicators_readiness`.
- Active tenant `Code` uniqueness through
  `ux_tep_hiring_risk_indicators_tenant_code_active`, with
  `ix_tep_hiring_risk_indicators_tenant_state` supporting tenant/state queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`risk`, `indicator`,
  `signal`, `mitigation`, `assessment`) are not falsely rejected while true
  forbidden markers (risk score, raw signal payload, PII, background-check,
  credential, payload) are still caught.

Explicitly unauthorized:

- Real risk scoring, risk rating, or computed risk-rating execution.
- Risk indicator catalog record persistence.
- Risk signal intake execution, ingestion, or raw signal payload persistence.
- Risk assessment execution, scoring runs, or assessment output.
- Mitigation execution, remediation workflow runtime, or mitigation action logs.
- Indicator review execution, adjudication, or adverse-action output.
- Scoring/rating/ranking/model-output persistence.
- Automated decision and adverse-action behavior.
- Manager/employee/candidate risk-indicator UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Risk scores, computed risk ratings, candidate/applicant PII,
  background-check/credit content, raw signal payload, or adverse-action
  persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- TEP tenant shell nav entry `Hiring Risk Indicators`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Manager risk-indicator UX beyond readiness-metadata CRUD.
- Employee risk-indicator/self-service UX beyond readiness-metadata CRUD.
- Candidate risk-indicator/self-service UX beyond readiness-metadata CRUD.
- Risk indicator catalog, signal intake, risk assessment, mitigation tracking,
  indicator review, scoring, or model-output UI.
- Document, notification, export, upload, or attachment UI.
- Any risk score, computed risk rating, candidate/applicant PII,
  background-check/credit, or raw signal payload viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> TEP `5060`).

Authorized API surface:

- `GET /api/hiring-risk-indicators` - list readiness metadata.
- `GET /api/hiring-risk-indicators/{id}` - get readiness metadata by id.
- `POST /api/hiring-risk-indicators` - create readiness metadata.
- `POST /api/hiring-risk-indicators/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/hiring-risk-indicators/{id}` - soft-delete.
- `GET /api/hiring-risk-indicators/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get hiring risk indicators readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No risk scoring/rating, risk indicator catalog record persistence, risk signal
  intake, risk assessment, mitigation execution, indicator review,
  scoring/model-output persistence, adverse-action or automated decision
  behavior, notification/document integration, or risk-score/PII/raw-payload
  persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only hiring risk indicators readiness state.
- Boundary-state metadata for risk indicator catalog, risk signal intake, risk
  assessment, mitigation tracking, indicator review, automated decision, consent,
  minimization, retention, and evidence.
- Dependency-state metadata for talent-data source, consent policy, document, and
  notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Risk signal intake/ingestion payload.
- Risk indicator catalog record body.
- Risk assessment/scoring payload.
- Mitigation execution payload or mitigation action logs.
- Indicator review/adjudication payload.
- Risk score/computed risk rating/rating/rank/match confidence/model output.
- Adverse-action decision result or automated decision result.
- Candidate/applicant PII, raw risk signal payload, background-check content, or
  credit content.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy candidate/profile details.
- Compensation/payroll/benefits payload.

## 12. Permission Boundary

Approved permission namespace:

- `tep.hiring-risk-indicators.read`
- `tep.hiring-risk-indicators.manage`
- `tep.hiring-risk-indicators.evaluate`
- `tep.hiring-risk-indicators.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Runtime work must be tenant-aware and fail closed. The backend/API slice must
resolve tenant server-side and must not expose `TenantId` in request DTOs.

Security guardrails:

- Candidate identity (`CAND-CAP-0042`) must not become a runtime literal.
- Blocked legacy ID (`MOD-0332`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_tep_hiring_risk_indicators_tenant_code_active` and
  `ix_tep_hiring_risk_indicators_tenant_state`.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation.
- Hiring-risk data minimization fail-closed behavior.
- Retention/evidence/audit/legal-hold/deletion as local/deferred metadata.
- Exclusion of risk scores, computed risk ratings, candidate/applicant PII,
  background-check/credit content, adverse-action decisions, raw signal payloads,
  and any PII dataset.

The Hiring Risk Indicators capability declares readiness STATE only; it does not
own or store any risk scores, computed risk ratings, candidate/applicant PII,
background-check/credit content, adverse-action decisions, or raw signal
payloads, and all downstream consumers must treat its output as
boundary/readiness metadata, not as a risk-scoring or data source.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, signal-intake
connector, or document/notification UI is authorized. The `CAND-CAP-0041` Talent
Data Foundation is consumed only as a talent-data-source dependency-state
context, HCM foundation (`CAND-CAP-0007` through `CAND-CAP-0010`) is consumed
only as prerequisite dependency-state context, and TEP `CAND-CAP-0011` through
`CAND-CAP-0021` are consumed only as sibling/context references.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0042`.
3. AC-03: Legacy Excel ID `MOD-0332` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `tep.hiring-risk-indicators`.
5. AC-05: Permission namespace is limited to `tep.hiring-risk-indicators.read`,
   `tep.hiring-risk-indicators.manage`, `tep.hiring-risk-indicators.evaluate`,
   and `tep.hiring-risk-indicators.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.TalentEcosystemService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection `tep_hiring_risk_indicators_readiness`)
   includes active tenant `Code` uniqueness
   (`ux_tep_hiring_risk_indicators_tenant_code_active`), tenant/state indexing
   (`ix_tep_hiring_risk_indicators_tenant_state`), soft delete, and tenant
   isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Real risk scoring/rating, risk indicator catalog record persistence,
    risk signal intake execution, risk assessment execution, mitigation
    execution, indicator review execution, model output, adverse-action, and
    automated decision behavior remain unauthorized.
14. AC-14: Manager/employee/candidate risk-indicator fields remain reserved/out
    of scope; no money field is present; no risk-score/PII/background-check/raw
    signal payload field is present.
15. AC-15: Risk scores, computed risk ratings, candidate/applicant PII,
    background-check/credit content, adverse-action decisions, raw signal
    payload, attachment payload, raw provider payload,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: Talent-data source, TEP, HCM foundation, and PSS dependency
    boundaries are recorded (`CAND-CAP-0041` Talent Data Foundation as
    talent-data source; TEP `CAND-CAP-0011` through `CAND-CAP-0021` as
    sibling/context; HCM `CAND-CAP-0007` through `CAND-CAP-0010` as prerequisite
    context).
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0042|MOD-0332` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build TEP API:
  `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `HiringRiskIndicators` filter
  (`Application.Tests/HiringRiskIndicatorsTests.cs`, handler behavior; fix-absent
  should be RED).
- Run full TEP Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_tep_hiring_risk_indicators_tenant_code_active`), and tenant/state indexing
  (`ix_tep_hiring_risk_indicators_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`risk`, `indicator`,
  `signal`, `mitigation`, `assessment`) are not falsely rejected.
- Verify forbidden catalog/intake/assessment/mitigation/review/scoring/
  decision/sensitive fields are absent.
- Verify manager/employee/candidate risk-indicator fields are reserved/absent, no
  money field exists, and no risk-score/PII/background-check/raw signal payload
  field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0042|MOD-0332" services frontend gateway`
  returns no runtime literal (candidate IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real risk scoring/rating prohibition is accepted.
- [x] Risk indicator catalog record persistence prohibition is accepted.
- [x] Risk signal intake / risk assessment / mitigation / indicator review
  execution prohibition is accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Adverse-action and automated decision behavior prohibition is accepted.
- [x] Reserved manager/employee/candidate risk-indicator fields, no-money-field,
  and no-risk-score/PII/raw-payload constraints are accepted.
- [x] Sensitive/PII, background-check/credit, and raw signal payload prohibition
  is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Talent-data source, TEP/HCM/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only hiring risk indicators readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Hiring-risk data minimization policy.
- [x] Retention/evidence/audit/legal-hold/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.TalentEcosystemService/**`
  and the owned `frontend/Diten.Web/**` HiringRiskIndicators objects.
- Runtime owner/key remains limited to `tep.hiring-risk-indicators`.
- Permission namespace remains limited to `tep.hiring-risk-indicators.read`,
  `tep.hiring-risk-indicators.manage`, `tep.hiring-risk-indicators.evaluate`,
  and `tep.hiring-risk-indicators.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `TalentDataSourceDependencyState`, `ConsentPolicyDependencyState`,
  `DocumentDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Manager/employee/candidate risk-indicator UX fields remain reserved/out of
  scope, no money field is present, and no risk-score/PII/background-check/raw
  signal payload field is present.
- HCM, PSS, and Platform scopes remain closed.
- Real risk scoring/rating, risk indicator catalog record persistence, risk
  signal intake execution, risk assessment execution, mitigation execution,
  indicator review execution, model output, adverse-action, automated decision
  behavior, notification/document integration, risk scores, computed risk
  ratings, candidate/applicant PII, background-check/credit content,
  adverse-action decisions, raw signal payload, attachment payload, raw provider
  payload, credential/token/secret/password, and PII-heavy persistence remain
  out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0042 --name "Hiring Risk Indicators"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0042`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Hiring-risk data minimization fail-closed policy approved.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0042|MOD-0332" services frontend gateway`
- Candidate IDs are governance-only and must return no runtime literal match.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real risk signal intake and ingestion.
- Risk indicator catalog record management.
- Risk assessment execution and scoring.
- Mitigation tracking and remediation workflow.
- Indicator review and adverse-action adjudication.
- Automated decision approval.
- Manager/employee/candidate risk-indicator UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
