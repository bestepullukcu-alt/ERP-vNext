---
id: CAND-CAP-0038
name: Employee / Manager Self-Service Channel
domain: human-capital-management
service: Diten.HumanCapitalService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0038-employee-manager-self-service-channel
started: 2026-09-07
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0038
legacy_excel_id: MOD-0319
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
runtime_service_port: 5059
gateway_port: 5080
bootstrap_type: metadata-only-backend-api-readiness
runtime_owner_key: hcm.self-service
permission_namespace:
  - hcm.self-service.read
  - hcm.self-service.manage
  - hcm.self-service.evaluate
  - hcm.self-service.audit.read
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0038 - Employee / Manager Self-Service Channel

> Status: draft. This pack records the first metadata-only backend/API
> employee / manager self-service channel readiness contract slice under
> `services/Diten.HumanCapitalService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5080` -> HCM `5059`). Production self-service channel
> workflow, request intake, approval routing, inbox delivery,
> profile self-update, delegation scope, automated decision behavior,
> manager/employee self-service UX, notification/document integration, and
> interaction-content persistence remain closed. This is a self-service
> interaction CHANNEL/READINESS slice over SHARED backing HCM capabilities and a
> shared identity directory; it does not own, execute, or store self-service
> transactions, and stores NO interaction content. It does NOT authorize and
> NEVER persists self-service request bodies, form submissions, approval
> decisions, manager notes, employee statements, inbox/message content, or
> profile change payloads; it stores ONLY boundary/readiness STATE metadata. It
> does NOT authorize real self-service request capture, form submission storage,
> approval workflow execution, inbox/message content, profile self-update
> execution or PII changes, manager delegation execution, notes/comments, model
> output, scoring, or automated decision. `CAND-CAP-0038` remains a
> governance/documentation identity only and must not be written into runtime
> literals. `MOD-0319` remains blocked for HCM use until future EA canonical
> MOD assignment.

## 1. Module Summary

`CAND-CAP-0038` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-C capability named
`Employee / Manager Self-Service Channel`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0319`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0038` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API employee / manager
self-service channel readiness contract slice, mirroring the Compensation &
Benefits Interface slice (`CAND-CAP-0037` / `MOD-0316`) and the completed Time,
Attendance & Leave Interface slice (`CAND-CAP-0036` / `MOD-0315`) exactly.
This capability is a self-service interaction CHANNEL/READINESS slice over the
shared backing HCM capabilities and shared identity directory: it is
metadata-only readiness and does NOT own, execute, or store any self-service
transaction or interaction content. Self-service channel workflow, request
intake, approval routing, inbox delivery, profile self-update, delegation scope,
automated decision behavior, manager/employee self-service UX,
notification/document integration, and interaction-content persistence remain
closed.

## 2. Ownership and Boundaries

Owned by this pack:

- HCM-native employee / manager self-service channel governance boundary.
- Sequencing after completed HCM employee, governance, position, offboarding,
  applicant intake, candidate pipeline, offer management, onboarding,
  employment change, performance review, competency/skills, development plan,
  learning/training, succession/high-potential, workforce planning,
  headcount/position budget planning, HR KPI/analytics facade, HR
  documentation/evidence workspace, time/attendance/leave interface, and
  compensation & benefits interface foundation modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only self-service channel readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Explicit exclusion of self-service channel workflow execution.
- Explicit exclusion of request intake, approval routing, inbox delivery,
  profile self-update, delegation scope, model output, and automated decision
  behavior.
- Explicit exclusion of manager-facing and employee-facing self-service UX
  beyond the readiness-metadata CRUD (those fields are reserved and out of
  scope).
- Explicit exclusion of any interaction content: this channel does NOT own,
  execute, or store self-service request bodies, form submissions, approval
  decisions, manager notes, employee statements, inbox/message content, profile
  change payloads, or any self-service transaction, and only boundary/readiness
  STATE metadata is persisted.
- Explicit exclusion of free-text self-service notes, interaction narrative,
  self-service request body, form submission, approval decision, manager note,
  employee statement, inbox message content, profile change payload, raw
  provider payload, credential/token/secret/password, and PII-heavy persistence.

Not owned by this pack:

- Real self-service channel execution, real request capture, form submission
  storage, approval workflow execution, inbox/message content, profile
  self-update execution or PII changes, manager delegation execution, or model
  output.
- Backing HCM capability ownership beyond dependency/context references.
- Identity directory ownership beyond dependency/context references.
- Compensation & Benefits Interface (`CAND-CAP-0037`) ownership beyond
  dependency/context references.
- Notification, notification provider, controlled document, or external
  self-service/identity/HCM provider implementation.
- Development plan, competency/skills, performance review, learning/training,
  succession, workforce planning, headcount/budget, HR KPI/analytics facade, HR
  documentation/evidence workspace, time/attendance/leave interface,
  compensation & benefits interface, backing HCM capabilities, identity
  directory, TEP, or PSS ownership transfer.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| SelfServiceCapabilityBoundary | Governance boundary | Defines what a future HCM employee / manager self-service channel module may own. |
| SelfServiceReadinessBoundary | Governance boundary | Separates readiness metadata from self-service channel workflow execution. |
| RequestApprovalChannelBoundary | Governance boundary | Blocks request intake, approval routing, and inbox delivery execution. |
| InboxProfileDelegationBoundary | Governance boundary | Blocks profile self-update and delegation scope execution. |
| SelfServiceDecisionBoundary | Governance boundary | Blocks scoring, rating, ranking, model-output, and automated decision persistence. |
| SelfServiceUxBoundary | Governance boundary | Blocks manager and employee self-service UX beyond readiness-metadata CRUD. |
| SelfServiceSensitiveDataBoundary | Governance boundary | Blocks narrative, self-service request body, form submission, approval decision, manager note, employee statement, inbox message content, profile change payload, credential, and PII-heavy persistence. |
| SelfServiceDependencyBoundary | Governance boundary | Keeps identity-directory/HCM-capability/notification/document dependencies deferred and out of scope. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| SelfServiceChannelWorkflow | Deferred runtime | Self-service channel workflow execution; not authorized. |
| RequestApprovalRoutingEngine | Deferred runtime | Request intake, approval routing, and inbox delivery; not authorized. |
| ProfileDelegationEngine | Deferred runtime | Profile self-update, delegation scope, and model output; not authorized. |
| ManagerEmployeeSelfServiceExperience | Deferred frontend/runtime | Manager/employee self-service UX beyond readiness CRUD; not authorized. |
| SelfServiceDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| SelfServiceNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| SelfServiceReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/SelfServiceReadinessMetadata.cs`. |
| SelfServiceReadinessState | Domain enum | Readiness state; `Domain/Enums/SelfServiceReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| ISelfServiceReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/ISelfServiceReadinessMetadataRepository.cs`. |
| MongoSelfServiceReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `hcm_self_service_readiness`. |
| SelfService Application features | Application (CQRS/MediatR) | `Application/Features/SelfService/**` (Models/Guard/Commands/Queries/Handlers). |
| SelfServiceController | API controller | Thin controller; `Api/Controllers/Hcm/SelfServiceController.cs`. |
| SelfService golden-compact CRUD | Frontend | Controller + `Views/HumanCapital/SelfService/**` + `wwwroot/assets/js/HumanCapital/SelfService/**` + Resources + nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness-state fields are of type
`SelfServiceReadinessState`):

- `Code`
- `DisplayName`
- `SelfServiceReadinessState`
- `RequestIntakeBoundaryState`
- `ApprovalRoutingBoundaryState`
- `InboxDeliveryBoundaryState`
- `ProfileSelfUpdateBoundaryState`
- `DelegationScopeBoundaryState`
- `AutomatedDecisionBoundaryState`
- `IdentityDirectoryDependencyState`
- `HcmCapabilityDependencyState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `SelfServiceReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Manager-facing self-service fields (manager approval, manager review, manager
  action UX state) are reserved and out of scope.
- Employee-facing self-service fields (employee self-service input, employee
  profile self-update, employee self-service UX state) are reserved and out of
  scope.
- Reserved manager/employee UX fields, no interaction-content field, no money
  field. This is a metadata-only readiness slice with no self-service
  interaction-content attribute and no monetary value. Self-service REQUEST
  bodies, FORM submissions, APPROVAL decisions, MANAGER notes, EMPLOYEE
  statements, INBOX/message content, and PROFILE change payloads are FORBIDDEN;
  only boundary/readiness STATE metadata is persisted, never any self-service
  request body, form submission, approval decision, manager note, employee
  statement, inbox/message content, profile change payload, or any interaction
  content.

Forbidden field classes:

- Self-service channel workflow body, request intake submission payload,
  approval routing payload, inbox delivery payload, profile self-update payload,
  delegation scope payload, approval decision body, action evidence, or
  completed self-service interaction content.
- Readiness score, rating value, rank, self-service outcome, model output,
  automated decision result, recommendation output, or evaluator scoring
  payload.
- Free-text self-service notes, interaction narrative, manager comments,
  employee comments, HR notes, or sensitive self-service body.
- Self-service request body, form submission, approval decision, manager note,
  employee statement, inbox message content, profile change payload, raw
  provider payload, or uploaded file metadata.
- Self-service request body, form submission, approval decision, manager note,
  employee statement, inbox message content, profile change payload, score,
  rating, model output, or interaction-content fields, or any monetary value.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  PII-heavy employee/profile fields.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0038-employee-manager-self-service-channel.md`

Candidate future runtime service:

- `Diten.HumanCapitalService` (port `5059`)

Runtime repo scope:

- `services/Diten.HumanCapitalService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/HumanCapital/SelfService/**`,
  `wwwroot/assets/js/HumanCapital/SelfService/**`, Resources, nav entry).
- Gateway route exposure through the API gateway (`5080` -> HCM `5059`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.HumanCapitalService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned SelfService CRUD objects listed in §3/§5.
- `gateway/**` outside the owned SelfService readiness route.
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
- `CAND-CAP-0037` - Compensation & Benefits Interface, done.

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

- Runtime owner/key: `hcm.self-service`.
- Permission namespace:
  - `hcm.self-service.read`
  - `hcm.self-service.manage`
  - `hcm.self-service.evaluate`
  - `hcm.self-service.audit.read`
- Runtime repo scope: `services/Diten.HumanCapitalService/**`.
- Thin controller (`Api/Controllers/Hcm/SelfServiceController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `hcm_self_service_readiness`.
- Active tenant `Code` uniqueness.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice fixes the forbidden-marker matcher to use WORD/TOKEN
  boundaries (`\b` regex) instead of a bare substring `Contains`, so legitimate
  domain words (`self-service`, `request`, `approval`, `inbox`, `profile`,
  `delegation`) are not falsely rejected while true forbidden markers
  (`manager_note`, `employee_statement`, `free_text`, `salary`, ...) are still
  caught.

Explicitly unauthorized:

- Self-service channel workflow.
- Request intake, approval routing, inbox delivery, profile self-update,
  delegation scope.
- Real self-service request capture, form submission storage, approval workflow
  execution, inbox/message content, profile self-update execution or PII
  changes, and manager delegation execution.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Manager/employee self-service UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Sensitive payload and self-service interaction-content persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- Tenant shell nav entry `Employee / Manager Self-Service Channel`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Manager self-service UX beyond readiness-metadata CRUD.
- Employee self-service UX beyond readiness-metadata CRUD.
- Request intake, approval routing, inbox delivery, profile self-update,
  delegation scope, scoring, or model-output UI.
- Self-service request body, form submission, approval decision, manager note,
  employee statement, inbox message content, profile change payload, or any
  interaction-content UI.
- Document, notification, export, upload, or attachment UI.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> HCM `5059`). This is a self-service interaction
CHANNEL/READINESS slice that owns no self-service runtime: it does NOT authorize
real self-service request capture, form submission storage, approval workflow
execution, inbox/message content, profile self-update execution or PII changes,
manager delegation execution, or ownership of the backing HCM capabilities /
identity directory. It does NOT authorize and NEVER persists self-service
request bodies, form submissions, approval decisions, manager notes, employee
statements, inbox/message content, or profile change payloads; only
boundary/readiness STATE metadata is persisted.

Authorized API surface:

- `GET /api/self-service` - list readiness metadata.
- `GET /api/self-service/{id}` - get readiness metadata by id.
- `POST /api/self-service` - create readiness metadata.
- `POST /api/self-service/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/self-service/{id}` - soft-delete.
- `GET /api/self-service/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get self-service channel readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No self-service channel workflow, request intake, approval routing, inbox
  delivery, profile self-update, delegation scope, form submission storage,
  approval workflow execution, profile self-update execution, manager delegation
  execution, scoring/model-output persistence, automated decision behavior,
  notification/document integration, or sensitive/interaction-content payload
  persistence.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only self-service channel readiness state.
- Boundary-state metadata for request intake, approval routing, inbox delivery,
  profile self-update, delegation scope, automated decision, consent,
  minimization, retention, and evidence.
- Dependency-state metadata for identity directory, HCM capability, document,
  and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Self-service channel workflow payload.
- Request-intake/approval-routing/inbox-delivery/profile-self-update/delegation-scope
  execution payload.
- Real self-service request capture, form submission storage, approval workflow
  execution, or profile self-update persistence.
- Self-service request body, form submission, approval decision, manager note,
  employee statement, inbox message content, profile change payload, or any
  interaction content.
- Profile self-update execution or PII changes, manager delegation execution,
  inbox/message content, or notes/comments persistence.
- Readiness score/rating/rank/model output.
- Automated decision result.
- Free-text self-service notes or interaction narrative.
- Self-service interaction transcript payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy employee/profile details.
- Self-service request body / form submission / approval decision / manager note
  / employee statement / inbox message content / profile change payload and any
  interaction content.

## 12. Permission Boundary

Approved permission namespace:

- `hcm.self-service.read`
- `hcm.self-service.manage`
- `hcm.self-service.evaluate`
- `hcm.self-service.audit.read`

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
- Self-service interaction data minimization fail-closed behavior.
- Retention/evidence/audit/deletion as local/deferred metadata.
- Exclusion of interaction narrative, self-service request/form/approval/inbox/profile
  payloads, interaction content, and PII-heavy data.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.

The backing HCM capabilities and shared identity directory dependency are
deferred and out of scope: this channel only consumes readiness metadata and
owns no self-service runtime. No integration implementation, provider payload
persistence, or document/notification UI is authorized.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0038`.
3. AC-03: Legacy Excel ID `MOD-0319` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `hcm.self-service`.
5. AC-05: Permission namespace is limited to `hcm.self-service.read`,
   `hcm.self-service.manage`, `hcm.self-service.evaluate`, and
   `hcm.self-service.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.HumanCapitalService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection `hcm_self_service_readiness`)
   includes active tenant `Code` uniqueness and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Self-service channel workflow, request intake, approval routing,
    inbox delivery, profile self-update, delegation scope, model output, and
    automated decision behavior remain unauthorized.
14. AC-14: Manager/employee self-service fields remain reserved/out of
    scope; no self-service interaction-content field and no monetary field is
    present.
15. AC-15: Free-text self-service notes, interaction narrative, self-service
    request body, form submission, approval decision, manager note, employee
    statement, inbox message content, profile change payload, raw provider
    payload, credential/token/secret/password, and PII-heavy persistence remain
    unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: HCM, TEP, and PSS dependency boundaries are recorded.
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0038|MOD-0319` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build HCM API:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `SelfService` filter
  (`Application.Tests/SelfServiceTests.cs`, handler behavior; fix-absent
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
  not bare substring `Contains`, so legitimate words (`self-service`, `request`,
  `approval`, `inbox`, `profile`, `delegation`) are not falsely rejected while
  true forbidden markers (`manager_note`, `employee_statement`, `free_text`,
  `salary`, ...) are still caught.
- Verify forbidden workflow/request-intake/approval-routing/inbox-delivery/profile-self-update/
  delegation-scope/scoring/decision/sensitive/interaction-content fields are absent.
- Verify manager/employee self-service fields are reserved/absent and no
  self-service interaction-content field and no monetary field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0038|MOD-0319" services/Diten.HumanCapitalService frontend gateway tests`

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Self-service channel workflow prohibition is accepted.
- [x] Request intake/approval routing/inbox delivery/profile self-update/delegation
  scope prohibition is accepted.
- [x] Self-service request capture/form-submission storage/approval-workflow-execution/profile-self-update-persistence
  prohibition is accepted.
- [x] Profile self-update execution/PII change/manager delegation execution/
  inbox-message-content prohibition is accepted.
- [x] Self-service request body/form submission/approval decision/manager note/
  employee statement/inbox message content/profile change payload/interaction-content
  prohibition is accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved manager/employee self-service fields and no-interaction-content-field/no-money-field
  constraint are accepted.
- [x] Sensitive payload and self-service interaction-content prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] HCM/TEP/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only self-service channel readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Self-service interaction data minimization policy.
- [x] Retention/evidence/audit/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.HumanCapitalService/**`
  and the owned `frontend/Diten.Web/**` SelfService objects.
- Runtime owner/key remains limited to `hcm.self-service`.
- Permission namespace remains limited to `hcm.self-service.read`,
  `hcm.self-service.manage`, `hcm.self-service.evaluate`, and
  `hcm.self-service.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `IdentityDirectoryDependencyState`, `HcmCapabilityDependencyState`,
  `DocumentDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Manager/employee self-service UX fields remain reserved/out of scope
  and no self-service interaction-content field and no monetary field is present.
- TEP, PSS, and Platform scopes remain closed.
- Self-service channel workflow, request intake, approval routing, inbox
  delivery, profile self-update, delegation scope, self-service request capture,
  form submission storage, approval workflow execution, profile self-update
  persistence, profile self-update execution or PII changes, manager delegation
  execution, model output, automated decision behavior, notification/document
  integration, free-text self-service notes, interaction narrative, self-service
  request body, form submission, approval decision, manager note, employee
  statement, inbox message content, profile change payload, raw provider
  payload, credential/token/secret/password, self-service interaction content,
  and PII-heavy persistence remain out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0038 --name "Employee / Manager Self-Service Channel"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0038`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Self-service interaction data minimization fail-closed policy approved.
- Retention/evidence/audit/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0038|MOD-0319" services/Diten.HumanCapitalService frontend gateway tests`

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real employee / manager self-service channel workflow.
- Request intake and approval routing.
- Inbox delivery, profile self-update, and delegation scope governance.
- Automated decision approval.
- Manager/employee self-service UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
