---
id: CAND-CAP-0022
name: Recruitment / Applicant Intake
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0022-recruitment-applicant-intake
started: 2026-09-01
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
  - execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md
candidate_identity: CAND-CAP-0022
legacy_excel_id: MOD-0300
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: hcm.applicant-intake
permission_namespace: hcm.applicant-intake.*
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0022 - Recruitment / Applicant Intake

> Status: done. The first metadata-only applicant intake readiness backend/API
> slice has passed implementation review and done-promotion readiness audit.
> Runtime implementation remains limited to
> `services/Diten.HumanCapitalService/**` with runtime owner/key
> `hcm.applicant-intake` and permission namespace `hcm.applicant-intake.*`.
> It does not authorize frontend, Gateway exposure, candidate-facing public
> application UX, resume/CV body persistence, cover letter persistence,
> free-text application narrative persistence, attachment payload persistence,
> notification/document integration, raw provider payload persistence,
> credential/token/secret persistence, or PII-heavy persistence.
> `CAND-CAP-0022` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0300` remains blocked for HCM use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0022` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-A capability named `Recruitment / Applicant Intake`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0300`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0022` as a temporary governance identity pending
future canonical MOD allocation.

EA candidate-runtime policy waiver permitted a narrow first backend/API slice
for metadata-only applicant intake readiness. That slice is implemented and
done. Public applicant experience, application body persistence,
attachment/document handling, notification integration, and provider
integrations require later approval gates.

## 2. Ownership and Boundaries

Owned by this done pack:

- HCM-native recruitment/applicant intake governance boundary.
- Runtime owner/key `hcm.applicant-intake`.
- Permission namespace `hcm.applicant-intake.*`.
- Candidate reservation and dependency sequencing after completed HCM foundation
  modules.
- First-slice metadata-only applicant intake readiness contract.
- Fail-closed metadata for privacy, consent, minimization, retention, duplicate
  handling, source/channel ownership, and provider payload exclusion.

Not owned by this pack:

- Frontend, Gateway routes, public application UX, or candidate self-service.
- Resume/CV body, cover letter body, free-text application narrative, attachment
  payload, or document repository persistence.
- Notification, notification provider, controlled document, or external document
  repository integration.
- TEP candidate identity, marketplace, reference exchange, recommendation, or
  dispute workflow ownership.
- PSS organization/person/position directory, HRIS, payroll, time, or provider
  data ownership.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| ApplicantIntakeBoundary | Governance boundary | Defines what a future HCM intake module may own. |
| ApplicantSourceBoundary | Governance boundary | Separates source/channel metadata from provider payloads and documents. |
| ApplicantDataMinimizationPolicy | Governance boundary | Blocks PII-heavy fields and free-text bodies until approved. |
| ApplicantDuplicateHandlingPolicy | Governance boundary | Requires deterministic duplicate policy before runtime. |
| ApplicantRetentionEvidencePolicy | Governance boundary | Requires retention, evidence, legal hold, and deletion decisions before runtime. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| ApplicantIntakeReadinessMetadata | Runtime entity | Implemented metadata-only readiness record after runtime-ready approval. |
| PublicApplicationChannel | Deferred runtime | Public/candidate-facing application UX; not authorized. |
| ResumeDocumentIntegration | Deferred dependency | Resume/CV and attachment handling; not authorized. |
| NotificationIntegration | Deferred dependency | Candidate/recruiter notifications; out of scope. |

First-slice runtime objects approved for implementation:

| Object | Type | Purpose |
|---|---|---|
| ApplicantIntakeReadinessMetadata | Runtime entity | Persists tenant-owned metadata-only applicant intake readiness records without application bodies, documents, provider payloads, or PII-heavy data. |
| ApplicantIntakeReadinessDto | API response DTO | Exposes only the approved metadata contract through `Response<T>`. |
| CreateApplicantIntakeReadinessCommand | CQRS command | Creates metadata-only readiness records; `TenantId` is resolved server-side. |
| EvaluateApplicantIntakeReadinessCommand | CQRS command | Produces fail-closed readiness/deferred metadata without public application UX or workflow execution. |
| ApplicantIntakeReadinessRepository | Persistence contract | Provides Mongo-backed, tenant-aware storage with active tenant `Code` uniqueness. |

## 4. Entity Fields

The first backend/API slice must expose and persist only this metadata contract:

- `Code`
- `DisplayName`
- `IntakeState`
- `SourceChannelState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `DuplicateHandlingState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `ApplicantIdentityBoundaryState`
- `PublicUxBoundaryState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `ApplicantIntakeVersion`
- `DeferredReason`

Contract rules:

- `TenantId` must not be accepted on request DTOs; tenant ownership is resolved
  server-side.
- Evaluation/activation must fail closed when required preconditions are not
  satisfied.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Dependency and readiness details must be expressed as metadata states only.

Forbidden field classes:

- Resume/CV body, cover letter body, free-text application narrative, interview
  notes, sensitive screening notes, attachment/document payload, or raw evidence.
- Raw provider payload, credentials, tokens, secrets, passwords, bank/tax/
  payroll data, payslips, biometric/geolocation data, national ID, DOB, home
  address, or PII-heavy profile fields.
- TEP marketplace transaction, reference exchange, recommendation score/rank,
  dispute body, or automated decision result fields.

## 5. Repo Scope

Authorized scope for this promotion change:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0022-recruitment-applicant-intake.md`

Authorized future runtime scope for the first metadata-only backend/API slice:

- `services/Diten.HumanCapitalService/**`

## 6. Protected Paths

This done pack must not change:

- `frontend/**`
- `gateway/**`
- global `tests/**`, except `services/Diten.HumanCapitalService/tests/**`
- `services/Diten.TalentEcosystemService/**`
- `services/Diten.Platform/**`
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

- Runtime owner/key is `hcm.applicant-intake`.
- Permission namespace is limited to:
  - `hcm.applicant-intake.read`
  - `hcm.applicant-intake.manage`
  - `hcm.applicant-intake.evaluate`
  - `hcm.applicant-intake.audit.read`
- `CAND-CAP-0022` is a governance/documentation identity only and must never be
  written into runtime literals, permission seeds, route metadata, database
  records, job names, telemetry owner fields, config keys, or test fixtures.
- `MOD-0300` must not be used for this HCM capability until EA assigns a
  canonical MOD.
- Tenant isolation must be server-side and must not accept `TenantId` from
  request DTOs.
- Activation/evaluation paths must fail closed when consent, minimization,
  duplicate handling, retention/evidence, source/channel, or dependency
  preconditions are missing.
- Candidate-facing public application UX remains blocked.
- Notification/document integrations remain blocked and out of scope.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

This pack does not create an HCM page, workspace menu, Razor view, JavaScript
file, DataTable, RESX resource, layout binding, or frontend route.

Future UI exposure requires a separate frontend-ready decision after backend/API
and Gateway readiness.

## 10. Backend File Convention

Backend implementation must follow the repo 5-layer .NET service convention,
CQRS/MediatR patterns, `Response<T>` envelope, tenant server-side resolution,
Mongo-backed repository standard, soft delete rules, and the approved permission
conventions.

Expected feature naming:

- Feature folder: `ApplicantIntake`
- Controller: `ApplicantIntakeController`
- Repository contract: `IApplicantIntakeReadinessMetadataRepository`
- Mongo repository: `MongoApplicantIntakeReadinessMetadataRepository`
- Runtime guard/owner key: `hcm.applicant-intake`

## 11. Frontend File Contract

Frontend implementation is N/A for this done backend/API pack.

No Razor, JavaScript, DataTable, RESX, menu, route, API client, GatewayUrl
binding, or public/candidate-facing UX is authorized.

Any future frontend must be restricted/admin read-only unless a separate legal,
privacy, consent, candidate-facing UX, and data minimization approval explicitly
authorizes more.

## 12. API Surface Contract

API surface approved for the first metadata-only backend/API slice:

- `GET /api/applicant-intake`
- `GET /api/applicant-intake/{id}`
- `POST /api/applicant-intake`
- `POST /api/applicant-intake/{id}/evaluate`
- `DELETE /api/applicant-intake/{id}`
- `GET /api/applicant-intake/{id}/audit-metadata`

All endpoints must expose only readiness metadata and must not expose applicant
body, CV, cover letter, attachments, raw payload, or PII-heavy details. The
`DELETE` endpoint must implement soft delete only.

## 13. Data Boundary

Allowed in this done first-slice boundary:

- Governance metadata.
- Dependency sequencing notes.
- Readiness blockers and waiver placeholders.
- Source/channel ownership boundaries.

Forbidden:

- Candidate-facing public application UX.
- Resume/CV body, cover letter body, free-text application narrative, screening
  narrative, attachment/document payload, or raw evidence.
- Raw HRIS/recruiting/provider payload.
- Credential, token, secret, password, or provider connection value.
- National ID, DOB, home address, bank, tax, payroll, payslip, biometric,
  geolocation, salary, wage, or PII-heavy applicant profile data.
- Notification delivery or document repository integration.

## 14. Permissions & Security

Approved permission namespace:

- `hcm.applicant-intake.read`
- `hcm.applicant-intake.manage`
- `hcm.applicant-intake.evaluate`
- `hcm.applicant-intake.audit.read`

Backend `[HasPermission]` enforcement remains authoritative. Any future frontend
permission behavior must remain UX-only hide/disable behavior.

Security blockers before runtime:

- Applicant data minimization.
- Consent and lawful basis.
- Retention/evidence/legal hold/deletion.
- Source/channel ownership and provider payload exclusion.
- Duplicate applicant handling.
- Public/candidate-facing UX authorization model.

## 15. Gateway / API Routing Decision

Gateway routing is N/A.

No Gateway route, Ocelot entry, frontend API proxy, or service port exposure is
authorized by the first backend/API slice. Gateway exposure may only be
considered after the backend/API slice passes implementation review.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `done`.
2. AC-02: Candidate identity is `CAND-CAP-0022`.
3. AC-03: Legacy Excel ID `MOD-0300` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime implementation is authorized only under
   `services/Diten.HumanCapitalService/**`.
5. AC-05: Frontend and Gateway implementation are not authorized.
6. AC-06: Candidate-facing public application UX is not authorized.
7. AC-07: Resume/CV body, cover letter body, free-text application narrative,
   and attachment payload persistence are blocked.
8. AC-08: Notification/document integrations remain deferred/out of scope.
9. AC-09: `TenantId` is never accepted by request DTOs.
10. AC-10: Mongo repository is tenant-aware and has active tenant `Code`
   uniqueness.
11. AC-11: Soft delete sets `IsDeleted` and `DeletedAt` and hides deleted rows.
12. AC-12: Evaluation/activation is fail-closed and can produce explicit
   `Deferred` metadata.
13. AC-13: Runtime owner/key is exactly `hcm.applicant-intake`.
14. AC-14: Permission namespace is limited to the four approved permissions.
15. AC-15: Forbidden raw/sensitive/application body fields are absent from
   public and persisted contracts.

## 17. Test Expectations

Expected runtime tests for the first backend/API slice:

- Create/List/Get metadata contract.
- Tenant isolation.
- Active tenant `Code` uniqueness.
- Soft delete hides deleted records and sets `DeletedAt`.
- Permission-gated controller surface.
- Dependency/precondition fail-closed behavior.
- Non-activating evaluation deferred metadata behavior.
- Forbidden raw/sensitive/application body field guard.
- Runtime literal regression guard.

Validation commands for the first implementation slice:

- `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- `dotnet test services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/Diten.HumanCapitalService.Application.Tests.csproj -c Debug --no-restore --filter ApplicantIntake`
- `dotnet test services/Diten.HumanCapitalService/tests/Diten.HumanCapitalService.Application.Tests/Diten.HumanCapitalService.Application.Tests.csproj -c Debug --no-restore`
- `rg -n "CAND-CAP-0022|MOD-0300" services/Diten.HumanCapitalService frontend gateway tests`
- `rg -n "InMemory.*ApplicantIntake|ApplicantIntake.*InMemory" services/Diten.HumanCapitalService/src`

## 18. Governance Approval Checklist

- [x] EA approves candidate-based governance continuation.
- [x] Registry owner accepts candidate gate result or verifier absence note.
- [x] `CAND-CAP-0022` remains documentation identity only.
- [x] `MOD-0300` remains blocked until EA canonical MOD assignment.
- [x] Runtime implementation remains limited to the approved metadata-only
  backend/API slice; frontend/Gateway implementation remains closed.
- [x] Candidate-facing public application UX remains closed.
- [x] Resume/CV body, cover letter, free-text application narrative, and
  attachment payload persistence remain closed.
- [x] Notification/document dependencies remain deferred/out of scope.
- [x] Sensitive/raw/PII-heavy data boundary is accepted.

## 19. Runtime-Ready Checklist

Runtime-ready blockers:

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Minimal metadata-only contract field list approval.
- [x] Legal/privacy/consent lawful basis decision.
- [x] Applicant data minimization decision.
- [x] Retention/evidence/audit/legal-hold/deletion decision.
- [x] Duplicate applicant handling decision.
- [x] Source/channel ownership and provider payload exclusion decision.
- [x] Public/candidate-facing UX remains explicitly blocked or receives a
  separate approved auth/consent design.
- [x] Notification/document dependencies stay deferred or receive separate
  approved module-pack readiness.
- [x] Tenant isolation and fail-closed evaluation rules are testable.

Open blockers for governance continuation:

- None after EA/registry owner approval.

Open blockers for runtime-ready:

- None for the approved first metadata-only applicant intake readiness backend/API
  slice.

## 20. Notes, Waivers, and Future Follow-Up

Review-ready reconciliation note dated 2026-09-01:

- Implementation review status: PASS.
- Metadata-only backend/API slice completed under
  `services/Diten.HumanCapitalService/**`.
- Runtime owner/key remained limited to `hcm.applicant-intake`.
- Permission namespace remained limited to:
  - `hcm.applicant-intake.read`
  - `hcm.applicant-intake.manage`
  - `hcm.applicant-intake.evaluate`
  - `hcm.applicant-intake.audit.read`
- Build validation PASS: 0 warning, 0 error.
- ApplicantIntake targeted tests PASS: 18/18.
- Full HCM Application tests PASS: 78/78.
- Runtime literal scan PASS:
  `CAND-CAP-0022|MOD-0300` no matches.
- Production in-memory repository scan PASS.
- Frontend/Gateway/TEP/PSS/Platform scope remained closed.
- Candidate-facing public UX, resume/document intake, notification/document
  integration, raw provider payload, credential/token/secret/password, and
  PII-heavy persistence remained out of scope.
- Open blockers: none.

Done-promotion reconciliation note dated 2026-09-01:

- Done-promotion readiness audit status: PASS.
- Pack status promoted from `review` to `done`.
- Open blockers: none.
- Metadata-only backend/API slice remains completed under
  `services/Diten.HumanCapitalService/**`.
- Runtime owner/key remained limited to `hcm.applicant-intake`.
- Permission namespace remained limited to:
  - `hcm.applicant-intake.read`
  - `hcm.applicant-intake.manage`
  - `hcm.applicant-intake.evaluate`
  - `hcm.applicant-intake.audit.read`
- Build validation PASS: 0 warning, 0 error.
- ApplicantIntake targeted tests PASS: 18/18.
- Full HCM Application tests PASS: 78/78.
- Runtime literal scan PASS:
  `CAND-CAP-0022|MOD-0300` no matches.
- Production in-memory repository scan PASS.
- Frontend/Gateway remain future follow-up.
- Candidate-facing public UX, resume/document intake, notification/document
  integration, raw provider payload, credential/token/secret/password, and
  PII-heavy persistence remained out of scope.

Candidate gate result:

- Candidate gate script not executable in this checkout; reservation recorded.
- EA/registry owner accepted verifier absence as a governance note for the
  governance-only approval gate.

Runtime literal scan:

- To be verified with
  `rg -n "CAND-CAP-0022|MOD-0300" services/Diten.HumanCapitalService frontend gateway tests`.

Current waivers:

- Governance continuation waiver approved for candidate-based documentation
  identity only.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API slice.
- Legal/privacy/consent metadata-only waiver approved.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata waiver
  approved.
- Duplicate applicant handling metadata-only precondition approved.
- Source/channel ownership and provider payload exclusion boundary approved.
- Notification/document dependencies remain deferred/out of scope.

Future follow-ups:

- Gateway exposure after backend/API done promotion.
- Restricted read-only frontend slice after Gateway exposure.
- Candidate-facing public application UX only after separate legal/privacy/
  consent and self-service authorization approval.
- Notification/document integrations only after deferred modules are brought
  back into scope.
- Export governance.
- Real audit/evidence/retention integration.
