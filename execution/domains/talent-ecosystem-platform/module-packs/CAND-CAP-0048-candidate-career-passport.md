---
id: CAND-CAP-0048
name: Candidate Career Passport
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0048-candidate-career-passport
started: 2026-09-08
target: 2026-12-08
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0048
legacy_excel_id: MOD-0339
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.TalentEcosystemService
runtime_service_port: 5060
gateway_port: 5080
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.candidate-career-passport
permission_namespace:
  - tep.candidate-career-passport.read
  - tep.candidate-career-passport.manage
  - tep.candidate-career-passport.evaluate
  - tep.candidate-career-passport.audit.read
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0048 - Candidate Career Passport

> Status: draft. This pack records the first metadata-only backend/API
> candidate career passport readiness contract slice under
> `services/Diten.TalentEcosystemService/**`, plus a golden-compact readiness
> CRUD frontend under `frontend/Diten.Web/**` and gateway exposure through the
> API gateway (`5080` -> TEP `5060`). This is a consent-, visibility-, and
> candidate-ownership-governed, privacy sensitive passport: real candidate-owned
> career passport handling, career-milestone / experience-intake / roster content
> persistence, candidate-ownership-scope or ordering execution, visibility-control
> execution, passport-review adjudication, candidate PII or roster persistence,
> free-text/resume/CV/raw payload persistence, model output or automated decision
> behavior, and sensitive/PII-heavy persistence remain closed.
> `CAND-CAP-0048` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0339` remains blocked for TEP use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0048` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R4 capability named `Candidate Career Passport`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0339`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0048` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API candidate career passport
readiness contract slice, mirroring the completed TEP candidate readiness
slices (`CAND-CAP-0017` / `MOD-0322` through `CAND-CAP-0021` / `MOD-0331`), the
completed `CAND-CAP-0041` / `MOD-0336` Talent Data Foundation slice, the
completed `CAND-CAP-0042` / `MOD-0332` Hiring Risk Indicators slice, the
completed `CAND-CAP-0043` / `MOD-0333` Early Warning Signals slice, the
completed `CAND-CAP-0044` / `MOD-0334` Restricted Integrity Registry slice, the
completed `CAND-CAP-0045` / `MOD-0335` Professional Reputation Ledger slice, the
completed `CAND-CAP-0046` / `MOD-0337` Industry Talent Pool slice, and the
completed `CAND-CAP-0047` / `MOD-0338` Industry Skill Passport slice exactly.
The Candidate Career Passport capability is the TEP-native readiness layer that
declares the readiness of a candidate-owned, portable career passport: it declares
the readiness STATE of the career milestone catalog, experience intake, candidate
ownership scope, visibility control, and passport review. It depends on the Talent
Data Foundation capability (`CAND-CAP-0041` / `MOD-0336`) as its upstream talent
data source, on the Industry Skill Passport capability (`CAND-CAP-0047` /
`MOD-0338`) as its upstream skill passport source, and on consent-policy
governance as its controlling dependency. Because this is a consent-, visibility-,
and candidate-ownership-governed, privacy sensitive passport, its sensitivity
posture is paramount: it stores NO career history rosters, NO milestone/experience
content, NO candidate contact/resume/CV/profile content, NO proficiency/ranking
scores, NO individual rankings, NO candidate PII, NO free-text, and NO
attachments - only boundary/readiness STATE metadata. Real candidate-owned
career passport handling, career-milestone / experience-intake / roster content
persistence, candidate-ownership-scope or ordering execution, visibility-control
execution, passport-review adjudication, candidate PII or roster persistence,
free-text/resume/CV/raw payload persistence, model output, automated decision
behavior, candidate/verifier/company passport UX, notification/document
integration, and sensitive/PII-heavy persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- TEP-native candidate career passport governance boundary.
- Sequencing after completed HCM foundation modules, completed TEP foundation and
  candidate readiness modules through `CAND-CAP-0021`, the completed
  `CAND-CAP-0042` Hiring Risk Indicators and `CAND-CAP-0043` Early Warning
  Signals context dependencies, the completed `CAND-CAP-0044` Restricted
  Integrity Registry context dependency, the completed `CAND-CAP-0045`
  Professional Reputation Ledger context dependency, the completed
  `CAND-CAP-0046` Industry Talent Pool context dependency, the completed
  `CAND-CAP-0047` Industry Skill Passport skill-passport-source dependency, and
  the completed `CAND-CAP-0041` Talent Data Foundation data-source dependency.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only candidate career passport readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the career milestone catalog, experience
  intake, candidate ownership scope, visibility control, and passport review.
- Explicit exclusion of real candidate-owned career passport handling and
  career-milestone / experience-intake / roster content persistence.
- Explicit exclusion of career milestone catalog execution, experience-intake
  execution, candidate-ownership-scope execution, visibility-control execution,
  passport-review execution, proficiency/ranking scores, model output, and
  automated decision behavior.
- Explicit exclusion of candidate-facing, verifier-facing, and company-facing
  passport UX beyond the readiness-metadata CRUD (those fields are reserved and
  out of scope).
- Explicit exclusion of career history rosters, milestone/experience content,
  candidate contact/resume/CV/profile content, proficiency/ranking scores,
  candidate PII, individual rankings, free-text, attachment payload,
  raw provider payload, credential/token/secret/password, and any PII dataset
  persistence.

Not owned by this pack:

- Real candidate-owned career passport handling, scoring, or ranking execution.
- Career milestone roster, experience-intake record, or resume/CV/profile content
  storage.
- Career milestone catalog execution, intake runs, or catalog collection output.
- Candidate-ownership-scope execution, ownership enforcement, or scope output.
- Visibility-control execution, visibility delivery, visibility decisions, or
  visibility action logs.
- Passport-review execution, adjudication output, or automated passport
  decisions.
- Talent-data ownership beyond dependency/context references (owned by
  `CAND-CAP-0041` Talent Data Foundation).
- Skill-passport ownership beyond dependency/context references (owned by
  `CAND-CAP-0047` Industry Skill Passport).
- Consent-policy and visibility-policy ownership beyond dependency/context
  references.
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
| CandidateCareerPassportCapabilityBoundary | Governance boundary | Defines what a future TEP candidate career passport module may own. |
| CandidateCareerPassportReadinessBoundary | Governance boundary | Separates readiness metadata from candidate-owned career passport execution. |
| CareerMilestoneCatalogBoundary | Governance boundary | Blocks career milestone roster / experience-claim / catalog record persistence. |
| ExperienceIntakeBoundary | Governance boundary | Blocks real experience-intake execution and milestone/experience content persistence. |
| CandidateOwnershipScopeBoundary | Governance boundary | Blocks candidate-ownership-scope execution, candidate resume/CV/profile content, and raw ownership payload persistence. |
| VisibilityControlBoundary | Governance boundary | Blocks visibility-control execution, visibility decisions, and visibility action logs. |
| PassportReviewBoundary | Governance boundary | Blocks passport review execution, adjudication, and review output. |
| CandidateCareerPassportDecisionBoundary | Governance boundary | Blocks proficiency/ranking-score, model-output, resume/CV-content, and automated decision persistence. |
| CandidateCareerPassportUxBoundary | Governance boundary | Blocks candidate, verifier, and company passport UX beyond readiness-metadata CRUD. |
| CandidateCareerPassportSensitiveDataBoundary | Governance boundary | Blocks career history rosters, milestone/experience content, candidate resume/CV/profile content, proficiency/ranking scores, PII, individual rankings, free-text, attachment, and raw payload persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| CareerMilestoneCatalogEngine | Deferred runtime | Real career milestone intake, catalog management, and roster / experience-claim / history capture; not authorized. |
| ExperienceIntakeEngine | Deferred runtime | Experience-intake execution, milestone/experience capture, and intake output; not authorized. |
| CandidateOwnershipScopeEngine | Deferred runtime | Candidate-ownership-scope execution, resume/CV/profile capture, and scope output; not authorized. |
| VisibilityControlEngine | Deferred runtime | Visibility-control execution, visibility decisions, and visibility action logs; not authorized. |
| PassportReviewEngine | Deferred runtime | Passport review execution, adjudication, and automated passport output; not authorized. |
| CandidateVerifierCompanyExperience | Deferred frontend/runtime | Candidate/verifier/company passport UX beyond readiness CRUD; not authorized. |
| CandidateCareerPassportDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| CandidateCareerPassportNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| CandidateCareerPassportReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/CandidateCareerPassportReadinessMetadata.cs`. |
| CandidateCareerPassportReadinessState | Domain enum | Readiness state; `Domain/Enums/CandidateCareerPassportReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| ICandidateCareerPassportReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/ICandidateCareerPassportReadinessMetadataRepository.cs`. |
| MongoCandidateCareerPassportReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `tep_candidate_career_passport_readiness`; indexes `ux_tep_candidate_career_passport_tenant_code_active` and `ix_tep_candidate_career_passport_tenant_state`. |
| CandidateCareerPassport Application features | Application (CQRS/MediatR) | `Application/Features/CandidateCareerPassport/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| CandidateCareerPassportController | API controller | Thin controller; `Api/Controllers/Tep/CandidateCareerPassportController.cs`. |
| CandidateCareerPassport golden-compact CRUD | Frontend | Controller + `Views/TalentEcosystem/CandidateCareerPassport/**` + `wwwroot/assets/js/TalentEcosystem/CandidateCareerPassport/**` + Resources + TEP nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `CandidateCareerPassportReadinessState`):

- `Code`
- `DisplayName`
- `CandidateCareerPassportReadinessState`
- `CareerMilestoneCatalogBoundaryState`
- `ExperienceIntakeBoundaryState`
- `CandidateOwnershipScopeBoundaryState`
- `VisibilityControlBoundaryState`
- `PassportReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `TalentDataSourceDependencyState`
- `ConsentPolicyDependencyState`
- `SkillPassportSourceDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `PortabilityPolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `CandidateCareerPassportReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Candidate-facing passport fields (candidate milestone submission, candidate
  review, candidate action UX state) are reserved and out of scope.
- Verifier-facing passport fields (verifier adjudication input, verifier
  decision, verifier self-service UX state) are reserved and out of scope.
- Company-facing passport fields (company response, company acknowledgement,
  company self-service UX state) are reserved and out of scope.
- No money field (salary/compensation/cost/budget). This is a metadata-only
  readiness slice with no monetary attribute.
- No career history roster, milestone/experience content, candidate
  resume/CV/profile content, proficiency/ranking score, candidate PII,
  individual ranking, free-text, or attachment field of any kind.

Forbidden field classes:

- Career milestone roster body, experience-intake payload, ownership-scope
  payload, resume/CV/profile content, visibility-control execution payload,
  passport-review payload, or completed career-passport content.
- Proficiency score result, ranking value, ordering result, rank, match
  confidence, model output, recommendation output, or evaluator scoring payload.
- Candidate PII, individual ranking, resume/CV/profile content, milestone/
  experience content, visibility content, cover letter, free-text profile
  narrative, HR notes, reference notes, or sensitive individual body.
- Attachment payload, controlled document body, external document content, raw
  resume, raw provider payload, or uploaded file metadata.
- Salary amount, compensation amount, benefits election, payroll details, tax
  details, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  any PII-heavy candidate/individual field.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0048-candidate-career-passport.md`

Candidate runtime service:

- `Diten.TalentEcosystemService` (port `5060`)

Runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/TalentEcosystem/CandidateCareerPassport/**`,
  `wwwroot/assets/js/TalentEcosystem/CandidateCareerPassport/**`, Resources, TEP nav
  entry).
- Gateway route exposure through the API gateway (`5080` -> TEP `5060`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.TalentEcosystemService/**` plus its golden-compact
readiness CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned CandidateCareerPassport CRUD objects listed
  in §3/§5.
- `gateway/**` outside the owned CandidateCareerPassport readiness route.
- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `services/Diten.PssService/**`
- global `tests/**`
- completed TEP foundation and candidate readiness packs (including the
  `CAND-CAP-0041` Talent Data Foundation data-source dependency, `CAND-CAP-0042`
  Hiring Risk Indicators, `CAND-CAP-0043` Early Warning Signals, `CAND-CAP-0044`
  Restricted Integrity Registry, `CAND-CAP-0045` Professional Reputation
  Ledger, `CAND-CAP-0046` Industry Talent Pool, and `CAND-CAP-0047` Industry
  Skill Passport).
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
  talent data source for the candidate career passport via
  `TalentDataSourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as a raw roster or data source.
- `CAND-CAP-0047` - Industry Skill Passport (`MOD-0338`), done. Consumed as the
  skill passport source for the candidate career passport via
  `SkillPassportSourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as raw skill claim / milestone / experience content or a data
  source.

Governance/control dependencies:

- Consent policy, consumed via `ConsentPolicyDependencyState`. Because this is a
  consent-, visibility-, and candidate-ownership-governed passport, a valid
  consent basis is a precondition and is treated as boundary/readiness metadata
  only.
- Visibility and candidate-ownership scope, consumed via
  `VisibilityControlBoundaryState` and `PortabilityPolicyState`. Visibility/access,
  candidate-ownership scope, and portability scope are controlling preconditions
  and are treated as boundary/readiness metadata only.

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
- `CAND-CAP-0042` - Hiring Risk Indicators (`MOD-0332`), done. Sibling/context.
- `CAND-CAP-0043` - Early Warning Signals (`MOD-0333`), done. Sibling/context.
- `CAND-CAP-0044` - Restricted Integrity Registry (`MOD-0334`), done. Sibling/context.
- `CAND-CAP-0045` - Professional Reputation Ledger (`MOD-0335`), done. Sibling/context.
- `CAND-CAP-0046` - Industry Talent Pool (`MOD-0337`), done. Sibling/context.
- `CAND-CAP-0047` - Industry Skill Passport (`MOD-0338`), done. Upstream skill
  passport source and sibling/context.

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

- Runtime owner/key: `tep.candidate-career-passport`.
- Permission namespace:
  - `tep.candidate-career-passport.read`
  - `tep.candidate-career-passport.manage`
  - `tep.candidate-career-passport.evaluate`
  - `tep.candidate-career-passport.audit.read`
- Runtime repo scope: `services/Diten.TalentEcosystemService/**`.
- Thin controller (`Api/Controllers/Tep/CandidateCareerPassportController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `tep_candidate_career_passport_readiness`.
- Active tenant `Code` uniqueness through
  `ux_tep_candidate_career_passport_tenant_code_active`, with
  `ix_tep_candidate_career_passport_tenant_state` supporting tenant/state
  queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`passport`, `career`,
  `milestone`, `experience`, `ownership`, `visibility`, `portability`) are not
  falsely rejected while true forbidden markers (roster, resume, cv, attestation,
  evidence, proficiency, ranking, PII, credential, certificate, license, payload)
  are still caught.

Explicitly unauthorized:

- Real candidate-owned career passport handling, scoring, or ranking execution.
- Career milestone roster / experience-intake / history record persistence.
- Experience-intake execution, enforcement, or resume/CV payload persistence.
- Candidate-ownership-scope execution, resume/CV/profile capture, or scope output.
- Visibility-control execution, visibility delivery, visibility decisions, or
  visibility action logs.
- Passport-review execution, adjudication, or automated passport output.
- Proficiency/ranking-score/finding/model-output persistence.
- Automated decision and proficiency-scoring/ranking behavior.
- Candidate/verifier/company passport UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Career history rosters, milestone/experience content, candidate resume/CV/profile
  content, proficiency/ranking scores, candidate PII, individual rankings,
  free-text, attachment payload, or raw payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- TEP tenant shell nav entry `Candidate Career Passport`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Candidate passport UX beyond readiness-metadata CRUD.
- Verifier passport/adjudication UX beyond readiness-metadata CRUD.
- Company passport/self-service UX beyond readiness-metadata CRUD.
- Career milestone catalog, experience intake, candidate ownership scope,
  visibility control, passport review, proficiency/ranking-score, or model-output
  UI.
- Document, notification, export, upload, or attachment UI.
- Any career history roster, milestone/experience content, candidate
  resume/CV/profile content, proficiency/ranking score, candidate PII,
  individual ranking, free-text, or raw payload viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> TEP `5060`).

Authorized API surface:

- `GET /api/candidate-career-passport` - list readiness metadata.
- `GET /api/candidate-career-passport/{id}` - get readiness metadata by id.
- `POST /api/candidate-career-passport` - create readiness metadata.
- `POST /api/candidate-career-passport/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/candidate-career-passport/{id}` - soft-delete.
- `GET /api/candidate-career-passport/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get candidate career passport readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No candidate-owned career passport handling, career milestone roster /
  experience-intake / history record persistence, experience-intake execution,
  candidate-ownership-scope execution, visibility-control execution, passport-review
  execution, proficiency/ranking-score or model-output persistence,
  proficiency-scoring or automated decision behavior, notification/document
  integration, or
  roster/resume/CV/PII/individual-ranking/free-text/
  attachment/raw-payload persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only candidate career passport readiness state.
- Boundary-state metadata for career milestone catalog, experience intake,
  candidate ownership scope, visibility control, passport review, automated
  decision, consent, minimization, retention, and portability.
- Dependency-state metadata for talent-data source, consent-policy,
  skill-passport-source, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Career milestone intake/catalog payload.
- Career milestone roster body, experience-intake content, or history content.
- Experience-intake execution/enforcement payload.
- Candidate-ownership-scope execution payload, resume/CV/profile content, or scope
  output.
- Visibility-control execution payload, visibility delivery, visibility
  decisions, or visibility action logs.
- Passport-review/adjudication payload.
- Proficiency score result / ranking value / ordering result / rank / match
  confidence / model output.
- Candidate PII, individual ranking, resume/CV/profile content, milestone/
  experience content, visibility content, or free-text.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy candidate/individual details.
- Compensation/payroll/benefits payload.

Consent-sensitivity note: because this is a consent-, visibility-, and
candidate-ownership-governed, privacy-sensitive candidate career passport, no
career history roster, milestone/experience content, candidate resume/CV/profile
content, proficiency/ranking score, or individual ranking of any kind may cross
the boundary into persistence. Only boundary/readiness STATE metadata governed by
a valid consent basis, visibility scope, and candidate-ownership scope is
permitted.

## 12. Permission Boundary

Approved permission namespace:

- `tep.candidate-career-passport.read`
- `tep.candidate-career-passport.manage`
- `tep.candidate-career-passport.evaluate`
- `tep.candidate-career-passport.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Runtime work must be tenant-aware and fail closed. The backend/API slice must
resolve tenant server-side and must not expose `TenantId` in request DTOs.

Security guardrails:

- Candidate identity (`CAND-CAP-0048`) must not become a runtime literal.
- Blocked legacy ID (`MOD-0339`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_tep_candidate_career_passport_tenant_code_active` and
  `ix_tep_candidate_career_passport_tenant_state`.
- Because this is a consent-, visibility-, and candidate-ownership-governed,
  privacy-sensitive passport, consent basis, visibility scope, and
  candidate-ownership scope are treated as the highest-risk controls even for
  readiness metadata; access is restricted and audited.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation, with a valid consent basis
  treated as a precondition to activation.
- Candidate-career-passport data minimization fail-closed behavior.
- Retention/portability/audit/visibility/deletion as local/deferred metadata,
  with visibility scope and candidate-ownership scope treated as controlling
  preconditions.
- Exclusion of career history rosters, milestone/experience content, candidate
  resume/CV/profile content, proficiency/ranking scores, candidate PII,
  individual rankings, free-text, attachments, and any PII dataset.

The Candidate Career Passport capability declares readiness STATE only; it does not
own or store any career history rosters, milestone/experience content, candidate
resume/CV/profile content, proficiency/ranking scores, candidate PII, individual
rankings, or free-text, and all downstream consumers must treat its output as
boundary/readiness metadata, not as a passport-handling or data source. Because
this is a consent-, visibility-, and candidate-ownership-governed,
privacy-sensitive passport, its consent-basis, visibility, candidate-ownership,
portability, retention, and restricted-access constraints are treated as the
highest-risk controls: no career milestone content, resume/CV/profile content,
milestone/experience content, proficiency/ranking score, or individual ranking
may ever be persisted in this slice, and access to even the readiness metadata is
restricted and audited.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, passport-intake
connector, or document/notification UI is authorized. The `CAND-CAP-0041` Talent
Data Foundation capability is consumed only as an upstream source dependency-state
context; the `CAND-CAP-0047` Industry Skill Passport capability is consumed only
as an upstream skill-passport-source dependency-state context; consent-policy and
visibility-policy are consumed only as governance dependency-state context; HCM
foundation (`CAND-CAP-0007` through `CAND-CAP-0010`) is consumed only as
prerequisite dependency-state context; and TEP `CAND-CAP-0011` through
`CAND-CAP-0021`, `CAND-CAP-0042`, `CAND-CAP-0043`, `CAND-CAP-0044`,
`CAND-CAP-0045`, `CAND-CAP-0046`, and `CAND-CAP-0047` are consumed only as
sibling/context references.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0048`.
3. AC-03: Legacy Excel ID `MOD-0339` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `tep.candidate-career-passport`.
5. AC-05: Permission namespace is limited to
   `tep.candidate-career-passport.read`,
   `tep.candidate-career-passport.manage`,
   `tep.candidate-career-passport.evaluate`, and
   `tep.candidate-career-passport.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.TalentEcosystemService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection
   `tep_candidate_career_passport_readiness`) includes active tenant `Code`
   uniqueness (`ux_tep_candidate_career_passport_tenant_code_active`),
   tenant/state indexing (`ix_tep_candidate_career_passport_tenant_state`),
   soft delete, and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Real candidate-owned career passport handling, career milestone roster /
    experience-intake / history record persistence, experience-intake
    execution, candidate-ownership-scope execution, visibility-control execution,
    passport-review execution, model output, proficiency/ranking-score, and
    automated decision behavior remain unauthorized.
14. AC-14: Candidate/verifier/company passport fields remain reserved/out of
    scope; no money field is present; no roster/resume/CV/
    PII/individual-ranking/free-text/attachment field is present.
15. AC-15: Career history rosters, milestone/experience content, candidate
    resume/CV/profile content, proficiency/ranking scores, candidate PII,
    individual rankings, free-text, attachment payload, raw provider payload,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: Upstream source, governance, TEP, HCM foundation, and PSS dependency
    boundaries are recorded (`CAND-CAP-0041` Talent Data Foundation as upstream
    talent data source; `CAND-CAP-0047` Industry Skill Passport as upstream
    skill passport source; consent-policy and visibility-policy as governance
    dependencies; TEP `CAND-CAP-0011` through `CAND-CAP-0021`, `CAND-CAP-0042`,
    `CAND-CAP-0043`, `CAND-CAP-0044`, `CAND-CAP-0045`, `CAND-CAP-0046`, and
    `CAND-CAP-0047` as sibling/context; HCM `CAND-CAP-0007` through
    `CAND-CAP-0010` as prerequisite context).
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0048|MOD-0339` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build TEP API:
  `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `CandidateCareerPassport` filter
  (`Application.Tests/CandidateCareerPassportTests.cs`, handler behavior;
  fix-absent should be RED).
- Run full TEP Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_tep_candidate_career_passport_tenant_code_active`), and tenant/state
  indexing (`ix_tep_candidate_career_passport_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`passport`, `career`,
  `milestone`, `experience`, `ownership`, `visibility`, `portability`) are not
  falsely rejected.
- Verify forbidden catalog/experience/ownership/visibility/passport-review/
  decision/sensitive fields are absent.
- Verify candidate/verifier/company passport fields are reserved/absent, no
  money field exists, and no roster/resume/CV/PII/individual-ranking/free-text/
  attachment field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0048|MOD-0339" services frontend gateway`
  returns no runtime literal (candidate IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real candidate-owned career passport handling prohibition is accepted.
- [x] Career milestone roster / experience-intake / history record persistence
  prohibition is accepted.
- [x] Experience-intake / candidate-ownership-scope / visibility-control / passport
  review execution prohibition is accepted.
- [x] Proficiency/ranking-score and model-output prohibition is accepted.
- [x] Proficiency-scoring/ranking and automated decision behavior prohibition is
  accepted.
- [x] Reserved candidate/verifier/company passport fields, no-money-field, and
  no-roster/resume/CV/PII/individual-ranking/
  free-text/attachment constraints are accepted.
- [x] Sensitive/PII, career milestone content, resume/CV content, and raw
  payload prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Talent-data source, skill-passport source, consent-policy, visibility-policy,
  TEP/HCM/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only candidate career passport readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Candidate-career-passport data minimization policy.
- [x] Retention/portability/audit/visibility/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.TalentEcosystemService/**`
  and the owned `frontend/Diten.Web/**` CandidateCareerPassport objects.
- Runtime owner/key remains limited to `tep.candidate-career-passport`.
- Permission namespace remains limited to
  `tep.candidate-career-passport.read`,
  `tep.candidate-career-passport.manage`,
  `tep.candidate-career-passport.evaluate`, and
  `tep.candidate-career-passport.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `TalentDataSourceDependencyState`, `ConsentPolicyDependencyState`,
  `SkillPassportSourceDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Candidate/verifier/company passport UX fields remain reserved/out of scope,
  no money field is present, and no roster/resume/CV/
  PII/individual-ranking/free-text/attachment field is present.
- HCM, PSS, and Platform scopes remain closed.
- Real candidate-owned career passport handling, career milestone roster /
  experience-intake / history record persistence, experience-intake
  execution, candidate-ownership-scope execution, visibility-control execution,
  passport-review execution, model output, proficiency/ranking-score,
  automated decision behavior, notification/document integration, career history
  rosters, milestone/experience content, candidate resume/CV/profile content,
  proficiency/ranking scores, candidate PII, individual rankings, free-text,
  attachment payload, raw provider payload, credential/token/secret/password, and
  PII-heavy persistence remain out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0048 --name "Candidate Career Passport"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0048`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Candidate-career-passport data minimization fail-closed policy approved.
- Retention/portability/audit/visibility/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0048|MOD-0339" services frontend gateway`
- Candidate IDs are governance-only and must return no runtime literal match.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real career milestone intake and catalog management.
- Experience-intake execution and enforcement.
- Candidate-ownership-scope execution and portability management.
- Visibility-control execution and visibility decisions.
- Passport review and automated passport adjudication.
- Automated decision approval.
- Candidate/verifier/company passport UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
