---
id: CAND-CAP-0024
name: Offer Management
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0024-offer-management
started: 2026-09-01
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
  - execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md
candidate_identity: CAND-CAP-0024
legacy_excel_id: MOD-0302
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
bootstrap_type: approved-first-slice
runtime_owner_key: hcm.offer-management
permission_namespace: hcm.offer-management.*
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0024 - Offer Management

> Status: done. Done-promotion readiness audit passed for the first metadata-only
> offer readiness backend/API slice under `services/Diten.HumanCapitalService/**`;
> the pack is complete for the approved backend/API slice and no open blockers
> remain.
> Runtime owner/key is `hcm.offer-management`; permission namespace is
> `hcm.offer-management.*`.
> `CAND-CAP-0024` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0302` remains blocked for HCM use until
> future EA canonical MOD assignment.
> This done status does not authorize frontend, Gateway exposure, real
> offer workflow execution, approval workflow, candidate-facing offer acceptance
> UX, offer letter document generation, notification/document integration,
> compensation/benefits/payroll payload persistence, raw provider payload persistence,
> credential/token/secret/password persistence, salary amount persistence, bank/
> payroll/tax details, or PII-heavy persistence.

## 1. Module Summary

`CAND-CAP-0024` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-A capability named `Offer Management`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0302`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0024` as a temporary governance identity pending
future canonical MOD allocation.

EA has approved candidate-based governance continuation and a candidate-runtime
policy waiver for the first metadata-only backend/API slice. The approved slice
is limited to offer readiness metadata, workflow boundary state, approval
boundary state, candidate acceptance boundary state, document boundary state,
compensation minimization state, consent, evidence, retention, and dependency
states. Real offer workflow execution, approval workflow, candidate acceptance,
offer letter generation, notification/document integrations, and compensation/
payroll payload persistence remain future follow-ups.

## 2. Ownership and Boundaries

Owned by this done pack:

- HCM-native offer management governance boundary.
- Sequencing after completed HCM applicant intake and candidate pipeline
  foundation modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Runtime owner/key `hcm.offer-management`.
- Permission namespace `hcm.offer-management.*`.
- First-slice metadata-only readiness contract for offer readiness, workflow
  boundary, approval boundary, candidate acceptance boundary, offer document
  boundary, compensation minimization, consent, retention, evidence, and
  dependency states.
- Explicit exclusion of offer workflow execution and approval workflow.
- Explicit exclusion of candidate-facing offer acceptance UX.
- Explicit exclusion of offer letter generation and document integration.
- Explicit exclusion of compensation, benefits, payroll, tax, bank, salary
  amount, and PII-heavy payload persistence.

Not owned by this pack:

- Frontend, Gateway routes, tenant shell navigation, or read-only UI.
- Offer approval workflow execution, offer acceptance workflow, e-signature, or
  candidate communication.
- Notification, notification provider, controlled document, or external document
  repository integration.
- TEP candidate identity, reference exchange, recommendation, dispute, or
  marketplace ownership.
- PSS organization/person/position directory, HRIS, payroll, time, or provider
  data ownership.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| OfferManagementBoundary | Governance boundary | Defines what a future HCM offer module may own. |
| OfferReadinessBoundary | Governance boundary | Separates metadata readiness from offer workflow execution. |
| OfferApprovalBoundary | Governance boundary | Blocks approval workflow execution until runtime-ready decisions close. |
| OfferAcceptanceBoundary | Governance boundary | Blocks candidate-facing acceptance UX and communication until approved. |
| OfferDocumentBoundary | Governance boundary | Blocks offer letter body/generation and document repository integration. |
| OfferCompensationDataBoundary | Governance boundary | Blocks compensation/benefits/payroll payload persistence in the first slice. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| OfferReadinessMetadata | Deferred runtime entity | Potential metadata-only readiness record if runtime-ready approval is granted. |
| OfferWorkflow | Deferred runtime | Real offer creation, approval, and issuing workflow; not authorized. |
| OfferAcceptanceExperience | Deferred frontend/runtime | Candidate-facing offer acceptance UX; not authorized. |
| OfferLetterGeneration | Deferred dependency | Offer document/template generation; out of scope. |
| NotificationIntegration | Deferred dependency | Offer notifications and reminders; out of scope. |
| PayrollCompensationIntegration | Deferred dependency | Compensation, benefits, payroll, bank, and tax payload handling; out of scope. |

First-slice runtime objects approved for implementation:

| Object | Type | Purpose |
|---|---|---|
| OfferReadinessMetadata | Runtime entity | Persists tenant-owned metadata-only offer readiness records without offer letter bodies, compensation amounts, benefits elections, payroll/tax/bank data, raw payloads, or PII-heavy data. |
| OfferReadinessDto | API response DTO | Exposes only the approved metadata contract through `Response<T>`. |
| CreateOfferReadinessCommand | CQRS command | Creates metadata-only readiness records; `TenantId` is resolved server-side. |
| EvaluateOfferReadinessCommand | CQRS command | Produces fail-closed readiness/deferred metadata without offer workflow, approval workflow, acceptance workflow, document generation, or compensation/payroll payload persistence. |
| OfferReadinessRepository | Persistence contract | Provides Mongo-backed, tenant-aware storage with active tenant `Code` uniqueness. |

## 4. Entity Fields

The first backend/API slice must expose and persist only this metadata contract:

- `Code`
- `DisplayName`
- `OfferReadinessState`
- `OfferWorkflowBoundaryState`
- `ApprovalWorkflowBoundaryState`
- `CandidateAcceptanceBoundaryState`
- `OfferDocumentBoundaryState`
- `CompensationDataBoundaryState`
- `BenefitsDataBoundaryState`
- `PayrollDataBoundaryState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `NotificationDependencyState`
- `DocumentDependencyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `OfferReadinessVersion`
- `DeferredReason`

Forbidden field classes:

- Offer letter body, generated document content, attachment payload, document
  payload, raw evidence, or raw provider payload.
- Salary amount, compensation amount, bonus amount, benefits election, payroll
  details, tax details, bank details, payslip, or payment instruction fields.
- Candidate acceptance text, free-text negotiation narrative, recruiter notes,
  approval comments, or legal narrative.
- Credential, token, secret, password, provider connection value, national ID,
  DOB, home address, biometric/geolocation data, or PII-heavy applicant/
  candidate profile fields.

## 5. Repo Scope

Authorized scope for this done pack update:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0024-offer-management.md`

Authorized runtime scope for the first metadata-only backend/API slice:

- `services/Diten.HumanCapitalService/**`

## 6. Protected Paths

This done pack must not change:

- `services/**`, except `services/Diten.HumanCapitalService/**` during the
  approved backend/API implementation step.
- `frontend/**`
- `gateway/**`
- global `tests/**`
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

Completed HCM foundation dependencies:

- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, done.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, done.
- `CAND-CAP-0009` - Position & Organization Assignment, done.
- `CAND-CAP-0010` - Offboarding & Exit Management, done.
- `CAND-CAP-0022` - Recruitment / Applicant Intake, done.
- `CAND-CAP-0023` - Candidate Pipeline & Interview Management, done.

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
- Runtime owner/key is `hcm.offer-management`.
- Permission namespace is limited to:
  - `hcm.offer-management.read`
  - `hcm.offer-management.manage`
  - `hcm.offer-management.evaluate`
  - `hcm.offer-management.audit.read`
- `CAND-CAP-0024` is a governance/documentation identity only and must never be
  written into runtime literals, permission seeds, route metadata, database
  records, job names, telemetry owner fields, config keys, or test fixtures.
- `MOD-0302` must not be used for this HCM capability until EA assigns a
  canonical MOD.
- Offer workflow execution and approval workflow remain blocked.
- Candidate-facing offer acceptance UX remains blocked.
- Offer letter generation and document integration remain blocked.
- Notification integration remains blocked.
- Compensation/benefits/payroll payload persistence remains blocked.
- Salary amount, bank, payroll, tax, credential, token, secret, password, and
  PII-heavy data persistence remain blocked.

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

Backend implementation must follow the repo
5-layer .NET service convention, CQRS/MediatR patterns, `Response<T>` envelope,
server-side tenant resolution, Mongo-backed repository standard, soft delete
rules, and approved permission conventions.

Expected feature naming:

- Feature folder: `OfferManagement`
- Controller: `OfferManagementController`
- Repository contract: `IOfferReadinessMetadataRepository`
- Mongo repository: `MongoOfferReadinessMetadataRepository`
- Runtime guard/owner key: `hcm.offer-management`.

## 11. Frontend File Contract

Frontend implementation is N/A for this governance-approved pack.

No Razor, JavaScript, DataTable, RESX, menu, route, API client, GatewayUrl
binding, candidate-facing acceptance UX, or offer workflow UX is authorized.

Any future frontend must be restricted/admin read-only metadata unless a
separate legal, privacy, compensation-data, document, notification, and
candidate-facing UX approval explicitly authorizes more.

## 12. API Surface Contract

API surface approved for the first metadata-only backend/API slice:

- `GET /api/offer-management`
- `GET /api/offer-management/{id}`
- `POST /api/offer-management`
- `POST /api/offer-management/{id}/evaluate`
- `DELETE /api/offer-management/{id}`
- `GET /api/offer-management/{id}/audit-metadata`

Any future API must expose only readiness metadata and must not expose offer
letter content, compensation amounts, benefits elections, payroll/tax/bank data,
raw payloads, candidate acceptance text, negotiation narratives, or PII-heavy
details. The `DELETE` endpoint, if implemented, must implement soft delete only.

## 13. Data Boundary

Allowed in this reviewed first-slice boundary:

- Governance metadata.
- Dependency sequencing notes.
- Readiness blockers and waiver placeholders.
- Offer workflow, approval, acceptance, document, compensation, and notification
  boundary states as metadata only.

Forbidden:

- Offer workflow execution, approval workflow execution, candidate acceptance
  workflow, e-signature execution, or candidate communication.
- Offer letter body, generated document content, attachment/document payload,
  raw evidence, or raw provider payload.
- Salary amount, compensation amount, bonus amount, benefits election, payroll
  details, tax details, bank details, payslip, or payment instruction fields.
- Candidate acceptance text, free-text negotiation narrative, recruiter notes,
  approval comments, or legal narrative.
- Credential, token, secret, password, or provider connection value.
- National ID, DOB, home address, biometric/geolocation data, or PII-heavy
  applicant/candidate profile data.
- Notification delivery or document repository integration.

## 14. Permissions & Security

Approved permission namespace:

- `hcm.offer-management.read`
- `hcm.offer-management.manage`
- `hcm.offer-management.evaluate`
- `hcm.offer-management.audit.read`

Backend `[HasPermission]` enforcement remains authoritative. Any future
frontend permission behavior must remain UX-only hide/disable behavior.

Security constraints for runtime:

- Offer lawful basis and consent model.
- Compensation, benefits, payroll, tax, and bank data minimization.
- Offer approval authorization and segregation of duties.
- Candidate acceptance authorization and communication boundary.
- Offer letter document generation and retention policy.
- Evidence, audit, legal hold, and deletion policy.
- Notification/document dependency deferral or separate module readiness.

## 15. Gateway / API Routing Decision

Gateway routing is N/A for this done backend/API pack.

No Gateway route, Ocelot entry, frontend API proxy, or service port exposure is
authorized.

Gateway exposure must wait until the backend/API slice is implemented and
reviewed, and frontend must use GatewayUrl rather than direct service ports.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `done`.
2. AC-02: Candidate identity is `CAND-CAP-0024`.
3. AC-03: Legacy Excel ID `MOD-0302` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Reservation exists in registry and reconciliation ledger.
5. AC-05: Runtime implementation is limited to
   `services/Diten.HumanCapitalService/**`.
6. AC-06: Frontend and Gateway implementation remain closed.
7. AC-07: Runtime owner/key is exactly `hcm.offer-management`.
8. AC-08: Offer workflow execution and approval workflow remain blocked.
9. AC-09: Candidate-facing offer acceptance UX remains blocked.
10. AC-10: Offer letter document generation remains blocked.
11. AC-11: Notification/document integrations remain deferred/out of scope.
12. AC-12: Compensation/benefits/payroll payload persistence remains blocked.
13. AC-13: Salary/compensation amount, bank/payroll/tax details, raw provider
   payload, credential/token/secret/password, and PII-heavy persistence remain
   blocked.
14. AC-14: Permission namespace is limited to the four approved permissions.
15. AC-15: `TenantId` is never accepted by request DTOs.
16. AC-16: Mongo repository is tenant-aware and has active tenant `Code`
   uniqueness.
17. AC-17: Soft delete sets `IsDeleted` and `DeletedAt` and hides deleted rows.
18. AC-18: Evaluation/activation is fail-closed and can produce explicit
   `Deferred` metadata.
19. AC-19: Forbidden offer-letter/compensation/payroll/benefits/sensitive
   fields are absent from public and persisted contracts.
20. AC-20: Runtime literal scan for `CAND-CAP-0024|MOD-0302` passes under
   runtime paths.

## 17. Test Expectations

Runtime tests required for the first backend/API slice:

- Create/List/Get metadata contract.
- Tenant isolation.
- Active tenant `Code` uniqueness.
- Soft delete hides deleted records and sets `DeletedAt`.
- Permission-gated controller surface.
- Dependency/precondition fail-closed behavior.
- Non-activating evaluation deferred metadata behavior.
- Forbidden offer letter/compensation/payroll/benefits/sensitive field guard.
- Runtime literal regression guard.

Validation commands for the first implementation slice:

- `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- `dotnet test services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/Diten.HumanCapitalService.Application.Tests.csproj -c Debug --no-restore --filter OfferManagement`
- `dotnet test services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/Diten.HumanCapitalService.Application.Tests.csproj -c Debug --no-restore`
- `rg -n "CAND-CAP-0024|MOD-0302" services/Diten.HumanCapitalService frontend gateway tests`
- `rg -n "InMemory.*OfferManagement|OfferManagement.*InMemory|InMemory.*OfferReadiness|OfferReadiness.*InMemory" services/Diten.HumanCapitalService/src`

## 18. Governance Approval Checklist

- [x] EA approves candidate-based governance continuation.
- [x] Registry owner accepts candidate gate result or verifier absence note.
- [x] `CAND-CAP-0024` remains documentation identity only.
- [x] `MOD-0302` remains blocked until EA canonical MOD assignment.
- [x] First metadata-only backend/API implementation is authorized.
- [x] Frontend/Gateway implementation remains closed.
- [x] Offer workflow execution and approval workflow remain closed.
- [x] Candidate-facing offer acceptance UX remains closed.
- [x] Offer letter document generation remains closed.
- [x] Notification/document dependencies remain deferred/out of scope.
- [x] Compensation/benefits/payroll payload persistence remains closed.
- [x] Sensitive/raw/PII-heavy data boundary is accepted.

## 19. Runtime-Ready Checklist

Runtime-ready checklist:

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Minimal metadata-only contract field list approval.
- [x] Offer lawful basis and consent decision.
- [x] Offer workflow and approval workflow exclusion or readiness policy.
- [x] Candidate-facing acceptance UX exclusion or readiness policy.
- [x] Offer letter document generation exclusion or document dependency decision.
- [x] Compensation, benefits, payroll, tax, and bank data minimization decision.
- [x] Retention/evidence/audit/legal-hold/deletion decision.
- [x] Notification/document dependencies stay deferred or receive separate
  approved module-pack readiness.
- [x] Tenant isolation and fail-closed evaluation rules become testable.

Open blockers for governance continuation:

- None after EA/registry owner approval.

Open blockers for runtime-ready:

- Open blockers: none.
- None for the approved first metadata-only offer readiness backend/API slice.

## 20. Notes, Waivers, and Future Follow-Up

Candidate gate result:

- Candidate gate script not executable in this checkout; reservation recorded in
  governance sources.

Runtime literal scan:

- PASS: `rg -n "CAND-CAP-0024|MOD-0302" services/Diten.HumanCapitalService frontend gateway tests`
  returned no matches during implementation review.

Review-ready reconciliation note:

- Date: 2026-09-01.
- Reconciliation status: PASS.
- Open blockers: none.
- Metadata-only backend/API slice completed under
  `services/Diten.HumanCapitalService/**`.
- Runtime owner/key remained only `hcm.offer-management`.
- Permission namespace remained limited to:
  - `hcm.offer-management.read`
  - `hcm.offer-management.manage`
  - `hcm.offer-management.evaluate`
  - `hcm.offer-management.audit.read`
- Build PASS: 0 warning, 0 error.
- OfferManagement targeted tests PASS: 22/22.
- Full HCM Application tests PASS: 120/120.
- Runtime literal scan PASS: `CAND-CAP-0024|MOD-0302` no matches.
- Production in-memory repository scan PASS.
- DTO `TenantId` scan PASS.
- Forbidden field scan PASS.
- Frontend/Gateway/TEP/PSS/Platform scope remained closed.
- Offer workflow execution, approval workflow, candidate-facing acceptance UX,
  offer letter generation, notification/document integration, and
  compensation/benefits/payroll payload persistence remained out of scope.
- Salary/compensation amount, benefits election, bank/payroll/tax details,
  offer letter body, raw provider payload, credential/token/secret/password,
  and PII-heavy persistence remained absent.

Done-promotion reconciliation note:

- Date: 2026-09-01.
- Reconciliation status: PASS.
- Open blockers: none.
- Done-promotion readiness audit passed from `review` to `done`.
- Metadata-only backend/API slice completed under
  `services/Diten.HumanCapitalService/**`.
- Runtime owner/key remained only `hcm.offer-management`.
- Permission namespace remained limited to:
  - `hcm.offer-management.read`
  - `hcm.offer-management.manage`
  - `hcm.offer-management.evaluate`
  - `hcm.offer-management.audit.read`
- Build PASS: 0 warning, 0 error.
- OfferManagement targeted tests PASS: 22/22.
- Full HCM Application tests PASS: 120/120.
- Runtime literal scan PASS: `CAND-CAP-0024|MOD-0302` no matches.
- Production in-memory repository scan PASS.
- Forbidden field scan PASS.
- Frontend/Gateway remain closed and future follow-up.
- Offer workflow execution, approval workflow, candidate-facing acceptance UX,
  offer letter generation, notification/document integration, and
  compensation/benefits/payroll payload persistence remained out of scope.
- Salary/compensation amount, benefits election, bank/payroll/tax details,
  offer letter body, raw provider payload, credential/token/secret/password,
  and PII-heavy persistence remained absent.

Current waivers:

- Governance continuation waiver approved for candidate-based documentation
  identity.
- EA/registry owner accepted candidate gate verifier absence for governance-only
  approval.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API slice.
- Legal/privacy/consent metadata-only waiver approved.
- Compensation, benefits, payroll, tax, bank, and salary amount exclusion
  boundary approved.
- Offer workflow, approval workflow, candidate acceptance UX, offer document
  generation, and notification/document integrations remain deferred/out of
  scope.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata waiver
  approved.

Future follow-ups:

- Gateway exposure, only after done promotion.
- Restricted read-only frontend slice, only after Gateway exposure.
- Real offer workflow execution and approval workflow.
- Candidate-facing offer acceptance UX.
- Offer letter generation and document repository integration.
- Notification integration.
- Export governance and real audit/evidence/retention integration.
