---
id: CAND-CAP-0043
name: Early Warning Signals
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0043-early-warning-signals
started: 2026-09-07
target: 2026-12-08
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0043
legacy_excel_id: MOD-0333
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.TalentEcosystemService
runtime_service_port: 5060
gateway_port: 5080
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.early-warning-signals
permission_namespace:
  - tep.early-warning-signals.read
  - tep.early-warning-signals.manage
  - tep.early-warning-signals.evaluate
  - tep.early-warning-signals.audit.read
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0043 - Early Warning Signals

> Status: draft. This pack records the first metadata-only backend/API early
> warning signals readiness contract slice under
> `services/Diten.TalentEcosystemService/**`, plus a golden-compact readiness
> CRUD frontend under `frontend/Diten.Web/**` and gateway exposure through the
> API gateway (`5080` -> TEP `5060`). Real signal scoring, computed risk
> ratings, cross-company pattern-detection execution, cross-company alert
> content persistence, raw signal/pattern payload persistence,
> candidate/applicant PII or individual attribution persistence, alert-routing
> execution, signal-review adjudication, model output or automated decision
> behavior, manager/employee/candidate early-warning UX, notification/document
> integration, and sensitive/PII-heavy persistence remain closed.
> `CAND-CAP-0043` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0333` remains blocked for TEP use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0043` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R4 capability named `Early Warning Signals`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0333`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0043` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API early warning signals
readiness contract slice, mirroring the completed TEP candidate readiness slices
(`CAND-CAP-0017` / `MOD-0322` through `CAND-CAP-0021` / `MOD-0331`), the
completed `CAND-CAP-0041` / `MOD-0336` Talent Data Foundation slice, and the
completed `CAND-CAP-0042` / `MOD-0332` Hiring Risk Indicators slice exactly. The
Early Warning Signals capability is the TEP-native readiness layer for
cross-company early-warning and pattern-detection capabilities: it declares the
readiness STATE of the signal catalog, pattern detection, cross-company
correlation, alert routing, and signal review. It depends on the shared Talent
Data Foundation (`CAND-CAP-0041` / `MOD-0336`) and the Hiring Risk Indicators
capability (`CAND-CAP-0042` / `MOD-0332`) as its upstream sources. It stores NO
signal scores, NO computed risk ratings, NO cross-company alert content, NO
candidate/applicant PII, NO individual attributions, and NO raw signal/pattern
payloads - only boundary/readiness STATE metadata. Because the capability spans
company boundaries, its cross-company privacy sensitivity is paramount: no
cross-company signal, correlation, or alert content of any kind may be
persisted. Real signal scoring, computed risk ratings, cross-company
pattern-detection execution, cross-company alert content persistence, raw
signal/pattern payload persistence, candidate/applicant PII or individual
attribution persistence, alert-routing execution, signal-review adjudication,
model output, automated decision behavior, manager/employee/candidate
early-warning UX, notification/document integration, and sensitive/PII-heavy
persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- TEP-native early warning signals governance boundary.
- Sequencing after completed HCM foundation modules, completed TEP foundation and
  candidate readiness modules through `CAND-CAP-0021`, the completed
  `CAND-CAP-0041` Talent Data Foundation data-source dependency, and the
  completed `CAND-CAP-0042` Hiring Risk Indicators data-source dependency.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only early warning signals readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the signal catalog, pattern detection,
  cross-company correlation, alert routing, and signal review.
- Explicit exclusion of real signal scoring and computed risk-rating execution.
- Explicit exclusion of signal catalog execution, pattern-detection execution,
  cross-company correlation execution, alert-routing execution, signal-review
  execution, model output, and automated decision behavior.
- Explicit exclusion of manager-facing, employee-facing, and candidate-facing
  early-warning UX beyond the readiness-metadata CRUD (those fields are reserved
  and out of scope).
- Explicit exclusion of signal scores, computed risk ratings, cross-company
  alert content, raw signal/pattern payloads, candidate/applicant PII,
  individual attributions, attachment payload, raw provider payload,
  credential/token/secret/password, and any PII dataset persistence.

Not owned by this pack:

- Real signal scoring, computed risk-rating, or correlation-scoring execution.
- Raw signal payload, raw pattern payload, or cross-company alert content storage.
- Signal catalog execution, ingestion runs, or catalog collection output.
- Pattern-detection execution, detection runs, or pattern output.
- Cross-company correlation execution, correlation runs, or correlation output.
- Alert-routing execution, alert delivery, or alert action logs.
- Signal-review execution, adjudication output, or automated early-warning
  decisions.
- Talent-data ownership beyond dependency/context references (owned by
  `CAND-CAP-0041` Talent Data Foundation).
- Hiring-risk-indicator ownership beyond dependency/context references (owned by
  `CAND-CAP-0042` Hiring Risk Indicators).
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
| EarlyWarningSignalsCapabilityBoundary | Governance boundary | Defines what a future TEP early warning signals module may own. |
| EarlyWarningSignalsReadinessBoundary | Governance boundary | Separates readiness metadata from signal scoring/pattern-detection execution. |
| SignalCatalogBoundary | Governance boundary | Blocks raw signal catalog record persistence. |
| PatternDetectionBoundary | Governance boundary | Blocks real pattern-detection execution and raw pattern payload persistence. |
| CrossCompanyCorrelationBoundary | Governance boundary | Blocks cross-company correlation execution, scoring, and correlation output. |
| AlertRoutingBoundary | Governance boundary | Blocks alert-routing execution, alert delivery, and alert action logs. |
| SignalReviewBoundary | Governance boundary | Blocks signal review execution, adjudication, and review output. |
| EarlyWarningSignalsDecisionBoundary | Governance boundary | Blocks scoring, rating, model-output, alert-content, and automated decision persistence. |
| EarlyWarningSignalsUxBoundary | Governance boundary | Blocks manager, employee, and candidate early-warning UX beyond readiness-metadata CRUD. |
| EarlyWarningSignalsSensitiveDataBoundary | Governance boundary | Blocks signal scores, PII, individual attributions, cross-company alert content, raw signal/pattern payload persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| SignalCatalogEngine | Deferred runtime | Real signal catalog intake, ingestion, and raw signal payload capture; not authorized. |
| PatternDetectionEngine | Deferred runtime | Pattern-detection execution, scoring, and pattern output; not authorized. |
| CrossCompanyCorrelationEngine | Deferred runtime | Cross-company correlation execution, scoring, and correlation output; not authorized. |
| AlertRoutingEngine | Deferred runtime | Alert-routing execution, alert delivery, and alert action logs; not authorized. |
| SignalReviewEngine | Deferred runtime | Signal review execution, adjudication, and automated early-warning output; not authorized. |
| ManagerEmployeeCandidateEarlyWarningExperience | Deferred frontend/runtime | Manager/employee/candidate early-warning UX beyond readiness CRUD; not authorized. |
| EarlyWarningDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| EarlyWarningNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| EarlyWarningSignalsReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/EarlyWarningSignalsReadinessMetadata.cs`. |
| EarlyWarningSignalsReadinessState | Domain enum | Readiness state; `Domain/Enums/EarlyWarningSignalsReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IEarlyWarningSignalsReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/IEarlyWarningSignalsReadinessMetadataRepository.cs`. |
| MongoEarlyWarningSignalsReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `tep_early_warning_signals_readiness`; indexes `ux_tep_early_warning_signals_tenant_code_active` and `ix_tep_early_warning_signals_tenant_state`. |
| EarlyWarningSignals Application features | Application (CQRS/MediatR) | `Application/Features/EarlyWarningSignals/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| EarlyWarningSignalsController | API controller | Thin controller; `Api/Controllers/Tep/EarlyWarningSignalsController.cs`. |
| EarlyWarningSignals golden-compact CRUD | Frontend | Controller + `Views/TalentEcosystem/EarlyWarningSignals/**` + `wwwroot/assets/js/TalentEcosystem/EarlyWarningSignals/**` + Resources + TEP nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `EarlyWarningSignalsReadinessState`):

- `Code`
- `DisplayName`
- `EarlyWarningSignalsReadinessState`
- `SignalCatalogBoundaryState`
- `PatternDetectionBoundaryState`
- `CrossCompanyCorrelationBoundaryState`
- `AlertRoutingBoundaryState`
- `SignalReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `TalentDataSourceDependencyState`
- `RiskIndicatorSourceDependencyState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `EarlyWarningSignalsReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Manager-facing early-warning fields (manager signal submission, manager
  review, manager action UX state) are reserved and out of scope.
- Employee-facing early-warning fields (employee self-service input, employee
  acknowledgement, employee self-service UX state) are reserved and out of
  scope.
- Candidate-facing early-warning fields (candidate response, candidate
  acknowledgement, candidate self-service UX state) are reserved and out of
  scope.
- No money field (salary/compensation/cost/budget). This is a metadata-only
  readiness slice with no monetary attribute.
- No signal score, computed risk rating, cross-company alert content,
  candidate/applicant PII, individual attribution, or raw signal/pattern payload
  field of any kind.

Forbidden field classes:

- Signal catalog record body, pattern-detection payload, cross-company
  correlation payload, alert-routing execution payload, signal-review payload, or
  completed early-warning content.
- Signal score, computed risk rating, correlation score, rating value, rank,
  match confidence, model output, alert decision result, recommendation output,
  or evaluator scoring payload.
- Candidate/applicant PII, individual attribution, raw signal payload, raw
  pattern payload, cross-company alert content, cover letter, free-text profile
  narrative, HR notes, or sensitive candidate body.
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

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0043-early-warning-signals.md`

Candidate runtime service:

- `Diten.TalentEcosystemService` (port `5060`)

Runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/TalentEcosystem/EarlyWarningSignals/**`,
  `wwwroot/assets/js/TalentEcosystem/EarlyWarningSignals/**`, Resources, TEP nav
  entry).
- Gateway route exposure through the API gateway (`5080` -> TEP `5060`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.TalentEcosystemService/**` plus its golden-compact
readiness CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned EarlyWarningSignals CRUD objects listed in
  §3/§5.
- `gateway/**` outside the owned EarlyWarningSignals readiness route.
- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `services/Diten.PssService/**`
- global `tests/**`
- completed TEP foundation and candidate readiness packs (including the
  `CAND-CAP-0041` Talent Data Foundation and `CAND-CAP-0042` Hiring Risk
  Indicators data-source dependencies).
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Upstream source dependencies:

- `CAND-CAP-0041` - Talent Data Foundation (`MOD-0336`), done. Consumed as the
  talent-data source for early warning signals via
  `TalentDataSourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as a raw data source.
- `CAND-CAP-0042` - Hiring Risk Indicators (`MOD-0332`), done. Consumed as the
  risk-indicator source for early warning signals via
  `RiskIndicatorSourceDependencyState`; its output is treated as
  boundary/readiness metadata, not as a risk-scoring or data source.

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

- Runtime owner/key: `tep.early-warning-signals`.
- Permission namespace:
  - `tep.early-warning-signals.read`
  - `tep.early-warning-signals.manage`
  - `tep.early-warning-signals.evaluate`
  - `tep.early-warning-signals.audit.read`
- Runtime repo scope: `services/Diten.TalentEcosystemService/**`.
- Thin controller (`Api/Controllers/Tep/EarlyWarningSignalsController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `tep_early_warning_signals_readiness`.
- Active tenant `Code` uniqueness through
  `ux_tep_early_warning_signals_tenant_code_active`, with
  `ix_tep_early_warning_signals_tenant_state` supporting tenant/state queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`signal`, `pattern`,
  `correlation`, `alert`, `review`) are not falsely rejected while true
  forbidden markers (signal score, raw signal payload, PII, individual
  attribution, alert content, credential, payload) are still caught.

Explicitly unauthorized:

- Real signal scoring, computed risk-rating, or correlation-scoring execution.
- Signal catalog record persistence.
- Pattern-detection execution, ingestion, or raw pattern payload persistence.
- Cross-company correlation execution, scoring runs, or correlation output.
- Alert-routing execution, alert delivery, or alert action logs.
- Signal-review execution, adjudication, or automated early-warning output.
- Scoring/rating/ranking/model-output persistence.
- Automated decision and alert-content behavior.
- Manager/employee/candidate early-warning UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Signal scores, computed risk ratings, cross-company alert content,
  candidate/applicant PII, individual attributions, or raw signal/pattern
  payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- TEP tenant shell nav entry `Early Warning Signals`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Manager early-warning UX beyond readiness-metadata CRUD.
- Employee early-warning/self-service UX beyond readiness-metadata CRUD.
- Candidate early-warning/self-service UX beyond readiness-metadata CRUD.
- Signal catalog, pattern detection, cross-company correlation, alert routing,
  signal review, scoring, or model-output UI.
- Document, notification, export, upload, or attachment UI.
- Any signal score, computed risk rating, cross-company alert content,
  candidate/applicant PII, individual attribution, or raw signal/pattern payload
  viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> TEP `5060`).

Authorized API surface:

- `GET /api/early-warning-signals` - list readiness metadata.
- `GET /api/early-warning-signals/{id}` - get readiness metadata by id.
- `POST /api/early-warning-signals` - create readiness metadata.
- `POST /api/early-warning-signals/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/early-warning-signals/{id}` - soft-delete.
- `GET /api/early-warning-signals/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get early warning signals readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No signal scoring/rating, signal catalog record persistence, pattern-detection
  execution, cross-company correlation execution, alert-routing execution,
  signal review, scoring/model-output persistence, alert-content or automated
  decision behavior, notification/document integration, or signal-score/PII/
  individual-attribution/raw-payload persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only early warning signals readiness state.
- Boundary-state metadata for signal catalog, pattern detection, cross-company
  correlation, alert routing, signal review, automated decision, consent,
  minimization, retention, and evidence.
- Dependency-state metadata for talent-data source, risk-indicator source,
  document, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Signal catalog intake/ingestion payload.
- Signal catalog record body.
- Pattern-detection/scoring payload.
- Cross-company correlation payload, alert-routing execution payload, or alert
  action logs.
- Signal-review/adjudication payload.
- Signal score/computed risk rating/correlation score/rank/match confidence/model
  output.
- Cross-company alert content, alert decision result, or automated decision
  result.
- Candidate/applicant PII, individual attribution, raw signal payload, raw
  pattern payload, or cross-company alert content.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy candidate/profile details.
- Compensation/payroll/benefits payload.

Cross-company privacy note: because this capability spans company boundaries, no
cross-company signal, correlation, alert content, or individual attribution of
any kind may cross the boundary into persistence. Only aggregate boundary/
readiness STATE metadata is permitted.

## 12. Permission Boundary

Approved permission namespace:

- `tep.early-warning-signals.read`
- `tep.early-warning-signals.manage`
- `tep.early-warning-signals.evaluate`
- `tep.early-warning-signals.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Runtime work must be tenant-aware and fail closed. The backend/API slice must
resolve tenant server-side and must not expose `TenantId` in request DTOs.

Security guardrails:

- Candidate identity (`CAND-CAP-0043`) must not become a runtime literal.
- Blocked legacy ID (`MOD-0333`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_tep_early_warning_signals_tenant_code_active` and
  `ix_tep_early_warning_signals_tenant_state`.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation.
- Early-warning data minimization fail-closed behavior.
- Retention/evidence/audit/legal-hold/deletion as local/deferred metadata.
- Exclusion of signal scores, computed risk ratings, cross-company alert content,
  candidate/applicant PII, individual attributions, raw signal/pattern payloads,
  and any PII dataset.

The Early Warning Signals capability declares readiness STATE only; it does not
own or store any signal scores, computed risk ratings, cross-company alert
content, candidate/applicant PII, individual attributions, or raw signal/pattern
payloads, and all downstream consumers must treat its output as
boundary/readiness metadata, not as a signal-scoring or data source. Because the
capability operates across company boundaries, its cross-company privacy
sensitivity is treated as the highest-risk constraint: no cross-company signal
content, correlation, alert, or individual attribution may ever be persisted in
this slice.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, signal-intake
connector, or document/notification UI is authorized. The `CAND-CAP-0041` Talent
Data Foundation and `CAND-CAP-0042` Hiring Risk Indicators are consumed only as
upstream source dependency-state context, HCM foundation (`CAND-CAP-0007`
through `CAND-CAP-0010`) is consumed only as prerequisite dependency-state
context, and TEP `CAND-CAP-0011` through `CAND-CAP-0021` are consumed only as
sibling/context references.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0043`.
3. AC-03: Legacy Excel ID `MOD-0333` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `tep.early-warning-signals`.
5. AC-05: Permission namespace is limited to `tep.early-warning-signals.read`,
   `tep.early-warning-signals.manage`, `tep.early-warning-signals.evaluate`,
   and `tep.early-warning-signals.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.TalentEcosystemService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection `tep_early_warning_signals_readiness`)
   includes active tenant `Code` uniqueness
   (`ux_tep_early_warning_signals_tenant_code_active`), tenant/state indexing
   (`ix_tep_early_warning_signals_tenant_state`), soft delete, and tenant
   isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Real signal scoring/rating, signal catalog record persistence,
    pattern-detection execution, cross-company correlation execution,
    alert-routing execution, signal-review execution, model output,
    alert-content, and automated decision behavior remain unauthorized.
14. AC-14: Manager/employee/candidate early-warning fields remain reserved/out
    of scope; no money field is present; no signal-score/PII/individual-
    attribution/raw signal/pattern payload field is present.
15. AC-15: Signal scores, computed risk ratings, cross-company alert content,
    candidate/applicant PII, individual attributions, raw signal/pattern
    payload, attachment payload, raw provider payload,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: Upstream source, TEP, HCM foundation, and PSS dependency boundaries
    are recorded (`CAND-CAP-0041` Talent Data Foundation and `CAND-CAP-0042`
    Hiring Risk Indicators as upstream sources; TEP `CAND-CAP-0011` through
    `CAND-CAP-0021` as sibling/context; HCM `CAND-CAP-0007` through
    `CAND-CAP-0010` as prerequisite context).
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0043|MOD-0333` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build TEP API:
  `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `EarlyWarningSignals` filter
  (`Application.Tests/EarlyWarningSignalsTests.cs`, handler behavior; fix-absent
  should be RED).
- Run full TEP Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_tep_early_warning_signals_tenant_code_active`), and tenant/state indexing
  (`ix_tep_early_warning_signals_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`signal`, `pattern`,
  `correlation`, `alert`, `review`) are not falsely rejected.
- Verify forbidden catalog/pattern/correlation/alert/review/scoring/
  decision/sensitive fields are absent.
- Verify manager/employee/candidate early-warning fields are reserved/absent, no
  money field exists, and no signal-score/PII/individual-attribution/raw signal/
  pattern payload field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0043|MOD-0333" services frontend gateway`
  returns no runtime literal (candidate IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real signal scoring/rating prohibition is accepted.
- [x] Signal catalog record persistence prohibition is accepted.
- [x] Pattern-detection / cross-company correlation / alert-routing / signal
  review execution prohibition is accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Alert-content and automated decision behavior prohibition is accepted.
- [x] Reserved manager/employee/candidate early-warning fields, no-money-field,
  and no-signal-score/PII/individual-attribution/raw-payload constraints are
  accepted.
- [x] Sensitive/PII, cross-company alert content, and raw signal/pattern payload
  prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Talent-data source, risk-indicator source, TEP/HCM/PSS dependency context
  is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only early warning signals readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Early-warning data minimization policy.
- [x] Retention/evidence/audit/legal-hold/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.TalentEcosystemService/**`
  and the owned `frontend/Diten.Web/**` EarlyWarningSignals objects.
- Runtime owner/key remains limited to `tep.early-warning-signals`.
- Permission namespace remains limited to `tep.early-warning-signals.read`,
  `tep.early-warning-signals.manage`, `tep.early-warning-signals.evaluate`,
  and `tep.early-warning-signals.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `TalentDataSourceDependencyState`, `RiskIndicatorSourceDependencyState`,
  `DocumentDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Manager/employee/candidate early-warning UX fields remain reserved/out of
  scope, no money field is present, and no signal-score/PII/individual-
  attribution/raw signal/pattern payload field is present.
- HCM, PSS, and Platform scopes remain closed.
- Real signal scoring/rating, signal catalog record persistence,
  pattern-detection execution, cross-company correlation execution,
  alert-routing execution, signal-review execution, model output, alert-content,
  automated decision behavior, notification/document integration, signal scores,
  computed risk ratings, cross-company alert content, candidate/applicant PII,
  individual attributions, raw signal/pattern payload, attachment payload, raw
  provider payload, credential/token/secret/password, and PII-heavy persistence
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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0043 --name "Early Warning Signals"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0043`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Early-warning data minimization fail-closed policy approved.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0043|MOD-0333" services frontend gateway`
- Candidate IDs are governance-only and must return no runtime literal match.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real signal catalog intake and ingestion.
- Signal catalog record management.
- Pattern-detection execution and scoring.
- Cross-company correlation execution and scoring.
- Alert routing and delivery.
- Signal review and automated early-warning adjudication.
- Automated decision approval.
- Manager/employee/candidate early-warning UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
