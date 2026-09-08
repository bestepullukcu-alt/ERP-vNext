---
id: CAND-CAP-0026
name: Employment Change / Transfer / Promotion
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0026-employment-change-transfer-promotion
started: 2026-09-03
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0026
legacy_excel_id: MOD-0304
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.HumanCapitalService
bootstrap_type: metadata-only-backend-api-readiness
runtime_owner_key: hcm.employment-changes
permission_namespace:
  - hcm.employment-changes.read
  - hcm.employment-changes.manage
  - hcm.employment-changes.evaluate
  - hcm.employment-changes.audit.read
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0026 - Employment Change / Transfer / Promotion

> Status: done. Metadata-only backend/API employment change readiness contract
> slice completed, implementation review passed, and done-promotion readiness
> audit passed; gateway/frontend exposure remains future follow-up.
> `CAND-CAP-0026` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0304` remains blocked for HCM use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0026` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-B capability named `Employment Change / Transfer /
Promotion`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0304`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0026` as a temporary governance identity pending
future canonical MOD allocation.

This done-stage pack reconciles the completed first metadata-only backend/API
employment change readiness contract. Real employment change workflow,
transfer/promotion approval
workflow, position assignment mutation, employee/manager action UX,
compensation/payroll/benefits payload persistence, notification/document
integration, and controlled/external document repository integration remain
blocked until separate readiness decisions close.

## 2. Ownership and Boundaries

Owned by this done-stage pack:

- HCM-native employment change, transfer, and promotion governance boundary.
- Sequencing after completed HCM employee, governance, position, offboarding,
  applicant intake, candidate pipeline, offer management, and onboarding
  foundation modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only employment change readiness backend/API contract.
- Explicit exclusion of employment change workflow runtime.
- Explicit exclusion of transfer/promotion approval workflow.
- Explicit exclusion of position assignment mutation.
- Explicit exclusion of employee-facing and manager-facing action UX.
- Explicit exclusion of compensation, payroll, and benefits payload persistence.
- Explicit exclusion of notification and controlled/external document
  repository integrations.
- Explicit exclusion of salary/compensation amount, payroll/tax, benefits
  election, manager notes, free-text narrative, attachment payload, raw provider
  payload, credential/token/secret/password, and PII-heavy persistence.

Not owned by this pack:

- Real employment change workflow, approval workflow, position mutation, or
  compensation/payroll/benefits execution.
- Frontend, Gateway routes, tenant shell navigation, or read-only UI.
- Position assignment write ownership or PSS organization/person/position
  directory ownership.
- Payroll, benefits, compensation, time, access, device, or external provider
  system ownership.
- Notification, notification provider, controlled document, or external
  document repository implementation.
- TEP candidate identity, reference, recommendation, dispute, or marketplace
  ownership.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| EmploymentChangeBoundary | Governance boundary | Defines what a future HCM employment change module may own. |
| EmploymentChangeReadinessBoundary | Governance boundary | Separates readiness metadata from workflow execution. |
| TransferPromotionBoundary | Governance boundary | Blocks transfer and promotion execution until runtime-ready approval. |
| ApprovalBoundary | Governance boundary | Blocks approval workflow until legal/security/process decisions close. |
| PositionMutationBoundary | Governance boundary | Blocks position assignment mutation and PSS ownership transfer. |
| CompensationBoundary | Governance boundary | Blocks salary, benefits, payroll, and tax payload persistence. |
| ActionUxBoundary | Governance boundary | Blocks employee/manager action UX until separate approval. |
| EmploymentChangeSensitiveDataBoundary | Governance boundary | Blocks raw, narrative, credential, and PII-heavy persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| EmploymentChangeWorkflow | Deferred runtime | Real change lifecycle execution; not authorized. |
| TransferPromotionApprovalWorkflow | Deferred runtime | Approval routing and decision execution; not authorized. |
| PositionAssignmentMutation | Deferred dependency | Position assignment writes; not authorized. |
| EmployeeManagerActionExperience | Deferred frontend/runtime | Employee/manager action UX; not authorized. |
| CompensationPayloadIntegration | Deferred dependency | Compensation, benefits, payroll, and tax payloads; not authorized. |
| EmploymentChangeDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| EmploymentChangeNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Only metadata-only readiness runtime objects are approved by this pack.

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields:

- `Code`
- `DisplayName`
- `EmploymentChangeReadinessState`
- `ChangeLifecycleBoundaryState`
- `TransferBoundaryState`
- `PromotionBoundaryState`
- `ApprovalBoundaryState`
- `PositionAssignmentBoundaryState`
- `EmployeeActionBoundaryState`
- `ManagerActionBoundaryState`
- `CompensationDataBoundaryState`
- `BenefitsDataBoundaryState`
- `PayrollDataBoundaryState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `EmploymentChangeReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Forbidden field classes:

- Employment change workflow body, transfer/promotion approval decision body,
  completed action evidence, manager notes, HR notes, employee statement,
  comments, or free-text narrative.
- Position assignment mutation payload, PSS directory write payload, or external
  provider write result.
- Attachment payload, controlled document body, external document content, raw
  evidence, raw provider payload, or uploaded file metadata beyond approved
  reference-only identifiers.
- Salary amount, compensation amount, benefits election, payroll details, tax
  details, payslip, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  PII-heavy employee/profile fields.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0026-employment-change-transfer-promotion.md`

Runtime repo scope:

- `services/Diten.HumanCapitalService/**`

Candidate future runtime service:

- `Diten.HumanCapitalService`

## 6. Protected Paths

This done-stage pack reconciles only `services/Diten.HumanCapitalService/**`
for the first metadata-only backend/API slice and does not authorize changes to:

- `services/Diten.HumanCapitalService/**`
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

- Only the metadata-only backend/API readiness contract is authorized.
- `CAND-CAP-0026` is a governance/documentation identity only and must never be
  written into runtime literals, permission seeds, route metadata, database
  records, job names, telemetry owner fields, config keys, or test fixtures.
- `MOD-0304` must not be used for this HCM capability until EA assigns a
  canonical MOD.
- Employment change workflow runtime remains blocked.
- Transfer/promotion approval workflow remains blocked.
- Position assignment mutation remains blocked.
- Employee-facing and manager-facing action UX remains blocked.
- Compensation/payroll/benefits payload persistence remains blocked.
- Notification/document integrations remain deferred/out of scope.
- Salary/compensation amount, payroll/tax, benefits election, manager notes,
  free-text narrative, attachment payload, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence remain blocked.

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

- Feature folder: `EmploymentChanges`
- Controller: `EmploymentChangesController`
- Repository contract: `IEmploymentChangeReadinessMetadataRepository`
- Mongo repository: `MongoEmploymentChangeReadinessMetadataRepository`

Any future request DTO must not contain `TenantId`; tenant identity must be
resolved server-side.

## 11. Frontend File Contract

Frontend implementation is N/A for this done-stage backend/API-only pack.

No Razor, JavaScript, DataTable, RESX, menu, route, API client, GatewayUrl
binding, employee-facing UX, manager-facing UX, transfer/promotion action UX,
approval UX, or position mutation UX is authorized.

Any future frontend must wait for backend/API implementation review and Gateway
exposure. It must be restricted/admin read-only metadata unless separate legal,
privacy, approval workflow, compensation, notification, document, and PSS
ownership approvals explicitly authorize more.

## 12. API Surface Contract

API surface is authorized only for metadata-only backend/API readiness.

Metadata-only backend/API endpoints:

- `GET /api/employment-changes`
- `GET /api/employment-changes/{id}`
- `GET /api/employment-changes/{id}/audit-metadata`

Create, evaluate, and soft-delete endpoints may exist only as metadata
readiness boundaries when the first backend/API slice needs testable create,
fail-closed evaluation, and soft delete behavior. They must not execute
employment change workflow, approval workflow, position assignment mutation,
compensation/benefits/payroll/tax changes, notification delivery, or document
integration. Any API must expose only readiness metadata and must not expose
workflow bodies, approval decision content, position mutation payloads,
compensation/benefits/payroll/tax data, document bodies, attachment payloads,
raw provider payloads, credential/token/secret/password values, or PII-heavy
details.

## 13. Data Boundary

Allowed in this done-stage pack:

- Governance metadata.
- Dependency sequencing notes.
- Planning-only boundary states for employment change, transfer, promotion,
  approval, position assignment, compensation, benefits, payroll, notification,
  documents, retention, evidence, and minimization.
- Metadata-only backend/API readiness persistence under the approved runtime
  repo scope.

Forbidden:

- Employment change workflow execution.
- Transfer/promotion approval workflow execution.
- Position assignment mutation or PSS directory write ownership.
- Employee-facing or manager-facing action UX.
- Notification delivery, reminders, message templates, or delivery provider
  integration.
- Controlled/external document repository integration, document generation,
  document upload, attachment payload, or document body display.
- Salary amount, compensation amount, benefits election, bank details, payroll
  details, tax details, payslip, or payment instruction persistence.
- Raw provider payload, raw evidence, credential, token, secret, password,
  activation code, device secret, or provider connection value.
- Free-text narrative, HR notes, manager notes, employee statement, national ID,
  DOB, home address, biometric/geolocation data, medical data, or PII-heavy
  profile persistence.

## 14. Permissions & Security

Approved permission namespace:

- `hcm.employment-changes.read`
- `hcm.employment-changes.manage`
- `hcm.employment-changes.evaluate`
- `hcm.employment-changes.audit.read`

Backend `[HasPermission]` enforcement must remain authoritative in any future
slice. Any future frontend permission behavior must remain UX-only hide/disable
behavior.

Security decisions for the first slice:

- Legal/privacy/consent is metadata-only waiver state.
- Employment transition minimization is fail-closed.
- Position assignment mutation remains excluded.
- Compensation/payroll/benefits payload remains excluded.
- Approval workflow remains excluded.
- Evidence, retention, audit, legal hold, and deletion remain local/deferred
  metadata.

## 15. Gateway / API Routing Decision

Gateway routing is N/A for this done-stage backend/API-only pack.

No Gateway route, Ocelot entry, frontend API proxy, or service port exposure is
authorized.

Gateway exposure must wait until a backend/API slice is implemented and
reviewed, and frontend must use GatewayUrl rather than direct service ports.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `done`.
2. AC-02: Candidate identity is `CAND-CAP-0026`.
3. AC-03: Legacy Excel ID `MOD-0304` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `hcm.employment-changes`.
5. AC-05: Permission namespace is limited to
   `hcm.employment-changes.read`, `hcm.employment-changes.manage`,
   `hcm.employment-changes.evaluate`, and
   `hcm.employment-changes.audit.read`.
6. AC-06: Runtime repo scope is `services/Diten.HumanCapitalService/**`.
7. AC-07: Create/List/Get metadata readiness contract is testable.
8. AC-08: Tenant isolation is server-side and fail-closed.
9. AC-09: Active tenant `Code` uniqueness is testable.
10. AC-10: Soft delete hides deleted records and sets `DeletedAt`.
11. AC-11: Dependency/precondition evaluation is fail-closed.
12. AC-12: Non-activating evaluation can emit explicit `Deferred` metadata.
13. AC-13: Frontend and Gateway implementation remain closed.
14. AC-14: Employment change workflow runtime remains blocked.
15. AC-15: Transfer/promotion approval workflow remains blocked.
16. AC-16: Position assignment mutation remains blocked.
17. AC-17: Employee-facing and manager-facing action UX remains blocked.
18. AC-18: Compensation/payroll/benefits payload persistence remains blocked.
19. AC-19: Notification/document integrations remain deferred/out of scope.
20. AC-20: Controlled/external document repository integrations remain
   deferred/out of scope.
21. AC-21: Salary/compensation amount, payroll/tax, benefits election, manager
   notes, free-text narrative, attachment payload, raw provider payload,
   credential/token/secret/password, and PII-heavy persistence remain blocked.
22. AC-22: Runtime literal scan for `CAND-CAP-0026|MOD-0304` passes under
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
- Forbidden workflow/approval/position-mutation/compensation/sensitive field
  guard.
- Runtime literal regression guard.

Runtime-ready validation command:

- `rg -n "CAND-CAP-0026|MOD-0304" services/Diten.HumanCapitalService frontend gateway tests`

Review-ready reconciliation note:

- Implementation review PASS.
- Metadata-only backend/API slice completed under
  `services/Diten.HumanCapitalService/**`.
- Runtime owner/key remains `hcm.employment-changes`.
- Permission namespace remains limited to `hcm.employment-changes.read`,
  `hcm.employment-changes.manage`, `hcm.employment-changes.evaluate`, and
  `hcm.employment-changes.audit.read`.
- Build PASS: 0 warning, 0 error.
- EmploymentChange targeted tests PASS: 29/29.
- Full HCM Application tests PASS: 174/174.
- Runtime literal scan PASS: `CAND-CAP-0026|MOD-0304` no matches.
- Production in-memory repository scan PASS.
- Frontend/gateway/TEP/PSS/Platform scope remains closed.
- Real employment change workflow, transfer/promotion approval workflow,
  position assignment mutation, employee/manager action UX,
  compensation/payroll/benefits payload, notification/document integration,
  salary/compensation amount, payroll/tax, benefits election, manager notes,
  free-text narrative, attachment payload, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence remain outside
  the completed first slice.

Done-promotion reconciliation note:

- Done-promotion readiness audit PASS.
- Pack status promoted from `review` to `done`.
- Open blockers remain none.
- Metadata-only backend/API slice remains completed with runtime owner/key
  `hcm.employment-changes`.
- Permission namespace remains limited to `hcm.employment-changes.read`,
  `hcm.employment-changes.manage`, `hcm.employment-changes.evaluate`, and
  `hcm.employment-changes.audit.read`.
- Build PASS: 0 warning, 0 error.
- EmploymentChange targeted tests PASS: 29/29.
- Full HCM Application tests PASS: 174/174.
- Runtime literal scan PASS: `CAND-CAP-0026|MOD-0304` no matches.
- Production in-memory repository scan PASS.
- Frontend/gateway remain future follow-up.
- Real employment change workflow, transfer/promotion approval workflow,
  position assignment mutation, employee/manager action UX,
  compensation/payroll/benefits payload, notification/document integration,
  salary/compensation amount, payroll/tax, benefits election, manager notes,
  free-text narrative, attachment payload, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence remain outside
  the done slice.

## 18. Governance Approval Checklist

- [x] EA approves candidate-based governance continuation.
- [x] Registry owner accepts candidate gate result or verifier absence note.
- [x] `CAND-CAP-0026` remains documentation identity only.
- [x] `MOD-0304` remains blocked until EA canonical MOD assignment.
- [x] Runtime/API/controller/entity/repository/database/service work remains
  limited to the metadata-only backend/API readiness slice.
- [x] Frontend/Gateway implementation remains closed.
- [x] Employment change workflow runtime remains closed.
- [x] Transfer/promotion approval workflow remains closed.
- [x] Position assignment mutation remains closed.
- [x] Employee/manager action UX remains closed.
- [x] Compensation/payroll/benefits persistence remains closed.
- [x] Notification/document dependencies remain deferred/out of scope.
- [x] Controlled/external document repository integrations remain deferred/out
  of scope.
- [x] Sensitive/raw/PII-heavy data boundary is accepted.

## 19. Runtime-Ready Checklist

Runtime-ready checklist:

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only contract field list approval.
- [x] Legal/privacy/consent metadata-only waiver or policy.
- [x] Employee data minimization and visibility decision.
- [x] Employment change workflow exclusion or readiness policy.
- [x] Transfer/promotion approval workflow exclusion or readiness policy.
- [x] Position assignment mutation exclusion or ownership policy.
- [x] Employee/manager action UX exclusion or readiness policy.
- [x] Compensation/payroll/benefits payload exclusion policy.
- [x] Notification/document dependencies stay deferred or receive separate
  approved module-pack readiness.
- [x] Retention/evidence/audit/legal-hold/deletion decision.
- [x] Tenant isolation and fail-closed evaluation rules become testable.

Open blockers for governance continuation:

- None.

Open blockers for runtime-ready:

- None.

## 20. Notes, Waivers, and Future Follow-Up

Candidate gate result:

- Candidate gate script not executable in this checkout; reservation recorded in
  governance sources.

Runtime literal scan:

- To be verified with
  `rg -n "CAND-CAP-0026|MOD-0304" services/Diten.HumanCapitalService frontend gateway tests`.

Current waivers:

- EA governance-only continuation approval accepted for this candidate pack.
- EA candidate-runtime policy waiver accepted for the first metadata-only
  backend/API readiness contract.
- Legal/privacy/consent is approved only as metadata-only waiver state.
- Retention/evidence/audit/legal-hold/deletion are approved only as
  local/deferred metadata.
- `CAND-CAP-0026` remains governance/documentation identity and is not a
  runtime literal.
- `MOD-0304` remains blocked and is not a runtime literal.

Future follow-ups:

- Gateway exposure, only after backend/API implementation review passes.
- Restricted read-only frontend slice, only after Gateway exposure.
- Real employment change workflow.
- Transfer/promotion approval workflow.
- Position assignment integration, only with explicit ownership decision.
- Employee/manager action UX.
- Compensation/payroll/benefits integration.
- Notification/document integrations.
- Export governance and real audit/evidence/retention integration.
