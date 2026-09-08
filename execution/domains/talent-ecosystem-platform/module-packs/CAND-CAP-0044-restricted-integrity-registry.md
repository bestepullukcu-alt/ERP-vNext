---
id: CAND-CAP-0044
name: Restricted Integrity Registry
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0044-restricted-integrity-registry
started: 2026-09-07
target: 2026-12-08
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0044
legacy_excel_id: MOD-0334
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.TalentEcosystemService
runtime_service_port: 5060
gateway_port: 5080
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.restricted-integrity-registry
permission_namespace:
  - tep.restricted-integrity-registry.read
  - tep.restricted-integrity-registry.manage
  - tep.restricted-integrity-registry.evaluate
  - tep.restricted-integrity-registry.audit.read
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0044 - Restricted Integrity Registry

> Status: draft. This pack records the first metadata-only backend/API restricted
> integrity registry readiness contract slice under
> `services/Diten.TalentEcosystemService/**`, plus a golden-compact readiness
> CRUD frontend under `frontend/Diten.Web/**` and gateway exposure through the
> API gateway (`5080` -> TEP `5060`). This is a HIGHLY SENSITIVE, legally
> sensitive registry: real restricted-integrity case handling, case body /
> allegation / finding / evidence content persistence, restriction-scope
> execution, evidence-chain custody execution, disclosure or adverse-action
> decisions, case-review adjudication, candidate/individual PII or attribution
> persistence, free-text/attachment/raw payload persistence, model output or
> automated decision behavior, and sensitive/PII-heavy persistence remain closed.
> `CAND-CAP-0044` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0334` remains blocked for TEP use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0044` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R4 capability named `Restricted Integrity Registry`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0334`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0044` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API restricted integrity
registry readiness contract slice, mirroring the completed TEP candidate
readiness slices (`CAND-CAP-0017` / `MOD-0322` through `CAND-CAP-0021` /
`MOD-0331`), the completed `CAND-CAP-0041` / `MOD-0336` Talent Data Foundation
slice, the completed `CAND-CAP-0042` / `MOD-0332` Hiring Risk Indicators slice,
and the completed `CAND-CAP-0043` / `MOD-0333` Early Warning Signals slice
exactly. The Restricted Integrity Registry capability is the TEP-native readiness
layer that declares the readiness of serious restricted-integrity case handling:
it declares the readiness STATE of the integrity case catalog, restriction scope,
evidence chain, disclosure control, and case review. It depends on the Early
Warning Signals capability (`CAND-CAP-0043` / `MOD-0333`) as its upstream source
and on consent-policy and legal-hold governance as its controlling dependencies.
Because this is a legally sensitive registry, its sensitivity posture is
paramount: it stores NO restricted-integrity case bodies, NO
allegations/findings/evidence content, NO disclosure/adverse-action decisions, NO
candidate/individual PII or attributions, NO free-text notes, and NO attachments
- only boundary/readiness STATE metadata. Real restricted-integrity case
handling, case body / allegation / finding / evidence content persistence,
restriction-scope execution, evidence-chain custody execution, disclosure or
adverse-action decision execution, case-review adjudication, candidate/individual
PII or attribution persistence, free-text/attachment/raw payload persistence,
model output, automated decision behavior, investigator/reviewer/subject
restricted-integrity UX, notification/document integration, and sensitive/PII-heavy
persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- TEP-native restricted integrity registry governance boundary.
- Sequencing after completed HCM foundation modules, completed TEP foundation and
  candidate readiness modules through `CAND-CAP-0021`, the completed
  `CAND-CAP-0041` Talent Data Foundation and `CAND-CAP-0042` Hiring Risk
  Indicators context dependencies, and the completed `CAND-CAP-0043` Early Warning
  Signals data-source dependency.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only restricted integrity registry readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the integrity case catalog, restriction
  scope, evidence chain, disclosure control, and case review.
- Explicit exclusion of real restricted-integrity case handling and
  case-body/allegation/finding/evidence content persistence.
- Explicit exclusion of integrity case catalog execution, restriction-scope
  execution, evidence-chain custody execution, disclosure-control execution,
  case-review execution, disclosure/adverse-action decisions, model output, and
  automated decision behavior.
- Explicit exclusion of investigator-facing, reviewer-facing, and subject-facing
  restricted-integrity UX beyond the readiness-metadata CRUD (those fields are
  reserved and out of scope).
- Explicit exclusion of restricted-integrity case bodies, allegations, findings,
  evidence content, disclosure/adverse-action decisions, candidate/individual
  PII, individual attributions, free-text notes, attachment payload, raw provider
  payload, credential/token/secret/password, and any PII dataset persistence.

Not owned by this pack:

- Real restricted-integrity case handling, adjudication, or disposition execution.
- Case body, allegation, finding, or evidence content storage.
- Integrity case catalog execution, intake runs, or catalog collection output.
- Restriction-scope execution, restriction enforcement, or scope output.
- Evidence-chain custody execution, chain-of-custody runs, or evidence output.
- Disclosure-control execution, disclosure delivery, disclosure/adverse-action
  decisions, or disclosure action logs.
- Case-review execution, adjudication output, or automated integrity decisions.
- Early-warning-signal ownership beyond dependency/context references (owned by
  `CAND-CAP-0043` Early Warning Signals).
- Consent-policy and legal-hold ownership beyond dependency/context references.
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
| RestrictedIntegrityRegistryCapabilityBoundary | Governance boundary | Defines what a future TEP restricted integrity registry module may own. |
| RestrictedIntegrityRegistryReadinessBoundary | Governance boundary | Separates readiness metadata from restricted-integrity case-handling execution. |
| IntegrityCaseCatalogBoundary | Governance boundary | Blocks integrity case body / allegation / finding record persistence. |
| RestrictionScopeBoundary | Governance boundary | Blocks real restriction-scope execution and scope payload persistence. |
| EvidenceChainBoundary | Governance boundary | Blocks evidence-chain custody execution, evidence content, and raw evidence payload persistence. |
| DisclosureControlBoundary | Governance boundary | Blocks disclosure-control execution, disclosure/adverse-action decisions, and disclosure action logs. |
| CaseReviewBoundary | Governance boundary | Blocks case review execution, adjudication, and review output. |
| RestrictedIntegrityRegistryDecisionBoundary | Governance boundary | Blocks disclosure/adverse-action, model-output, finding-content, and automated decision persistence. |
| RestrictedIntegrityRegistryUxBoundary | Governance boundary | Blocks investigator, reviewer, and subject restricted-integrity UX beyond readiness-metadata CRUD. |
| RestrictedIntegrityRegistrySensitiveDataBoundary | Governance boundary | Blocks case bodies, allegations, findings, evidence content, PII, individual attributions, free-text, attachment, and raw payload persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| IntegrityCaseCatalogEngine | Deferred runtime | Real integrity case intake, catalog management, and case body / allegation / finding capture; not authorized. |
| RestrictionScopeEngine | Deferred runtime | Restriction-scope execution, enforcement, and scope output; not authorized. |
| EvidenceChainEngine | Deferred runtime | Evidence-chain custody execution, evidence content capture, and chain-of-custody output; not authorized. |
| DisclosureControlEngine | Deferred runtime | Disclosure-control execution, disclosure/adverse-action decisions, and disclosure action logs; not authorized. |
| CaseReviewEngine | Deferred runtime | Case review execution, adjudication, and automated integrity output; not authorized. |
| InvestigatorReviewerSubjectRestrictedIntegrityExperience | Deferred frontend/runtime | Investigator/reviewer/subject restricted-integrity UX beyond readiness CRUD; not authorized. |
| RestrictedIntegrityDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| RestrictedIntegrityNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| RestrictedIntegrityRegistryReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/RestrictedIntegrityRegistryReadinessMetadata.cs`. |
| RestrictedIntegrityRegistryReadinessState | Domain enum | Readiness state; `Domain/Enums/RestrictedIntegrityRegistryReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IRestrictedIntegrityRegistryReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/IRestrictedIntegrityRegistryReadinessMetadataRepository.cs`. |
| MongoRestrictedIntegrityRegistryReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `tep_restricted_integrity_registry_readiness`; indexes `ux_tep_restricted_integrity_registry_tenant_code_active` and `ix_tep_restricted_integrity_registry_tenant_state`. |
| RestrictedIntegrityRegistry Application features | Application (CQRS/MediatR) | `Application/Features/RestrictedIntegrityRegistry/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| RestrictedIntegrityRegistryController | API controller | Thin controller; `Api/Controllers/Tep/RestrictedIntegrityRegistryController.cs`. |
| RestrictedIntegrityRegistry golden-compact CRUD | Frontend | Controller + `Views/TalentEcosystem/RestrictedIntegrityRegistry/**` + `wwwroot/assets/js/TalentEcosystem/RestrictedIntegrityRegistry/**` + Resources + TEP nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `RestrictedIntegrityRegistryReadinessState`):

- `Code`
- `DisplayName`
- `RestrictedIntegrityRegistryReadinessState`
- `IntegrityCaseCatalogBoundaryState`
- `RestrictionScopeBoundaryState`
- `EvidenceChainBoundaryState`
- `DisclosureControlBoundaryState`
- `CaseReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `EarlyWarningSourceDependencyState`
- `ConsentPolicyDependencyState`
- `LegalHoldDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `RestrictedIntegrityRegistryReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Investigator-facing restricted-integrity fields (investigator case submission,
  investigator review, investigator action UX state) are reserved and out of
  scope.
- Reviewer-facing restricted-integrity fields (reviewer adjudication input,
  reviewer decision, reviewer self-service UX state) are reserved and out of
  scope.
- Subject-facing restricted-integrity fields (subject response, subject
  acknowledgement, subject self-service UX state) are reserved and out of scope.
- No money field (salary/compensation/cost/budget). This is a metadata-only
  readiness slice with no monetary attribute.
- No restricted-integrity case body, allegation, finding, evidence content,
  disclosure/adverse-action decision, candidate/individual PII, individual
  attribution, free-text note, or attachment field of any kind.

Forbidden field classes:

- Integrity case record body, allegation payload, finding payload, evidence-chain
  custody payload, disclosure-control execution payload, case-review payload, or
  completed restricted-integrity content.
- Disclosure decision result, adverse-action result, finding result, rating value,
  rank, match confidence, model output, recommendation output, or evaluator
  scoring payload.
- Candidate/individual PII, individual attribution, allegation content, evidence
  content, disclosure content, cover letter, free-text profile narrative, HR
  notes, investigation notes, or sensitive individual body.
- Attachment payload, controlled document body, external document content, raw
  evidence, raw provider payload, or uploaded file metadata.
- Salary amount, compensation amount, benefits election, payroll details, tax
  details, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  any PII-heavy candidate/individual field.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0044-restricted-integrity-registry.md`

Candidate runtime service:

- `Diten.TalentEcosystemService` (port `5060`)

Runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/TalentEcosystem/RestrictedIntegrityRegistry/**`,
  `wwwroot/assets/js/TalentEcosystem/RestrictedIntegrityRegistry/**`, Resources, TEP nav
  entry).
- Gateway route exposure through the API gateway (`5080` -> TEP `5060`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.TalentEcosystemService/**` plus its golden-compact
readiness CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned RestrictedIntegrityRegistry CRUD objects listed
  in §3/§5.
- `gateway/**` outside the owned RestrictedIntegrityRegistry readiness route.
- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `services/Diten.PssService/**`
- global `tests/**`
- completed TEP foundation and candidate readiness packs (including the
  `CAND-CAP-0041` Talent Data Foundation, `CAND-CAP-0042` Hiring Risk Indicators,
  and `CAND-CAP-0043` Early Warning Signals data-source dependency).
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Upstream source dependencies:

- `CAND-CAP-0043` - Early Warning Signals (`MOD-0333`), done. Consumed as the
  early-warning source for the restricted integrity registry via
  `EarlyWarningSourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as a raw signal or data source.

Governance/control dependencies:

- Consent policy, consumed via `ConsentPolicyDependencyState`. Because this is a
  legally sensitive registry, a valid consent basis is a precondition and is
  treated as boundary/readiness metadata only.
- Legal hold, consumed via `LegalHoldDependencyState`. Legal-hold status is a
  controlling precondition and is treated as boundary/readiness metadata only.

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
- `CAND-CAP-0041` - Talent Data Foundation (`MOD-0336`), done. Sibling/context.
- `CAND-CAP-0042` - Hiring Risk Indicators (`MOD-0332`), done. Sibling/context.

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

- Runtime owner/key: `tep.restricted-integrity-registry`.
- Permission namespace:
  - `tep.restricted-integrity-registry.read`
  - `tep.restricted-integrity-registry.manage`
  - `tep.restricted-integrity-registry.evaluate`
  - `tep.restricted-integrity-registry.audit.read`
- Runtime repo scope: `services/Diten.TalentEcosystemService/**`.
- Thin controller (`Api/Controllers/Tep/RestrictedIntegrityRegistryController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `tep_restricted_integrity_registry_readiness`.
- Active tenant `Code` uniqueness through
  `ux_tep_restricted_integrity_registry_tenant_code_active`, with
  `ix_tep_restricted_integrity_registry_tenant_state` supporting tenant/state
  queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`integrity`, `case`,
  `restriction`, `evidence`, `disclosure`, `review`) are not falsely rejected
  while true forbidden markers (case body, allegation, finding, evidence content,
  disclosure decision, PII, individual attribution, credential, payload) are
  still caught.

Explicitly unauthorized:

- Real restricted-integrity case handling, adjudication, or disposition execution.
- Integrity case body / allegation / finding record persistence.
- Restriction-scope execution, enforcement, or scope payload persistence.
- Evidence-chain custody execution, evidence content capture, or chain-of-custody
  output.
- Disclosure-control execution, disclosure delivery, disclosure/adverse-action
  decisions, or disclosure action logs.
- Case-review execution, adjudication, or automated integrity output.
- Disclosure-decision/finding/model-output persistence.
- Automated decision and disclosure/adverse-action behavior.
- Investigator/reviewer/subject restricted-integrity UX beyond readiness-metadata
  CRUD.
- Notification and document integrations.
- Restricted-integrity case bodies, allegations, findings, evidence content,
  disclosure/adverse-action decisions, candidate/individual PII, individual
  attributions, free-text notes, attachment payload, or raw payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- TEP tenant shell nav entry `Restricted Integrity Registry`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Investigator restricted-integrity UX beyond readiness-metadata CRUD.
- Reviewer restricted-integrity/adjudication UX beyond readiness-metadata CRUD.
- Subject restricted-integrity/self-service UX beyond readiness-metadata CRUD.
- Integrity case catalog, restriction scope, evidence chain, disclosure control,
  case review, disclosure/adverse-action, or model-output UI.
- Document, notification, export, upload, or attachment UI.
- Any restricted-integrity case body, allegation, finding, evidence content,
  disclosure/adverse-action decision, candidate/individual PII, individual
  attribution, free-text note, or raw payload viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> TEP `5060`).

Authorized API surface:

- `GET /api/restricted-integrity-registry` - list readiness metadata.
- `GET /api/restricted-integrity-registry/{id}` - get readiness metadata by id.
- `POST /api/restricted-integrity-registry` - create readiness metadata.
- `POST /api/restricted-integrity-registry/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/restricted-integrity-registry/{id}` - soft-delete.
- `GET /api/restricted-integrity-registry/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get restricted integrity registry readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No restricted-integrity case handling, case body / allegation / finding record
  persistence, restriction-scope execution, evidence-chain custody execution,
  disclosure-control execution, case-review execution, disclosure/adverse-action
  or model-output persistence, disclosure or automated decision behavior,
  notification/document integration, or case-body/allegation/finding/evidence/
  PII/individual-attribution/free-text/attachment/raw-payload persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only restricted integrity registry readiness state.
- Boundary-state metadata for integrity case catalog, restriction scope, evidence
  chain, disclosure control, case review, automated decision, consent,
  minimization, retention, and evidence.
- Dependency-state metadata for early-warning source, consent-policy, legal-hold,
  and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Integrity case intake/catalog payload.
- Integrity case record body, allegation content, or finding content.
- Restriction-scope execution/enforcement payload.
- Evidence-chain custody payload, evidence content, or chain-of-custody output.
- Disclosure-control execution payload, disclosure delivery, disclosure/
  adverse-action decisions, or disclosure action logs.
- Case-review/adjudication payload.
- Disclosure decision result / adverse-action result / finding result / rating /
  rank / match confidence / model output.
- Candidate/individual PII, individual attribution, allegation content, evidence
  content, disclosure content, or free-text notes.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy candidate/individual details.
- Compensation/payroll/benefits payload.

Legal-sensitivity note: because this is a legally sensitive restricted-integrity
registry, no restricted-integrity case body, allegation, finding, evidence
content, disclosure/adverse-action decision, or individual attribution of any
kind may cross the boundary into persistence. Only boundary/readiness STATE
metadata governed by a valid consent basis and legal-hold status is permitted.

## 12. Permission Boundary

Approved permission namespace:

- `tep.restricted-integrity-registry.read`
- `tep.restricted-integrity-registry.manage`
- `tep.restricted-integrity-registry.evaluate`
- `tep.restricted-integrity-registry.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Runtime work must be tenant-aware and fail closed. The backend/API slice must
resolve tenant server-side and must not expose `TenantId` in request DTOs.

Security guardrails:

- Candidate identity (`CAND-CAP-0044`) must not become a runtime literal.
- Blocked legacy ID (`MOD-0334`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_tep_restricted_integrity_registry_tenant_code_active` and
  `ix_tep_restricted_integrity_registry_tenant_state`.
- Because this is a highly sensitive, legally sensitive registry, restricted
  access, consent basis, and legal-hold status are treated as the highest-risk
  controls even for readiness metadata; access is restricted and audited.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation, with a valid consent basis
  treated as a precondition to activation.
- Restricted-integrity data minimization fail-closed behavior.
- Retention/evidence/audit/legal-hold/deletion as local/deferred metadata, with
  legal-hold status treated as a controlling precondition.
- Exclusion of restricted-integrity case bodies, allegations, findings, evidence
  content, disclosure/adverse-action decisions, candidate/individual PII,
  individual attributions, free-text notes, attachments, and any PII dataset.

The Restricted Integrity Registry capability declares readiness STATE only; it
does not own or store any restricted-integrity case bodies, allegations,
findings, evidence content, disclosure/adverse-action decisions,
candidate/individual PII, individual attributions, free-text notes, or
attachments, and all downstream consumers must treat its output as
boundary/readiness metadata, not as a case-handling or data source. Because this
is a legally sensitive registry, its consent-basis, legal-hold, retention, and
restricted-access constraints are treated as the highest-risk controls: no
restricted-integrity case content, evidence, disclosure decision, or individual
attribution may ever be persisted in this slice, and access to even the readiness
metadata is restricted and audited.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, case-intake
connector, or document/notification UI is authorized. The `CAND-CAP-0043` Early
Warning Signals capability is consumed only as an upstream source dependency-state
context; consent-policy and legal-hold are consumed only as governance
dependency-state context; HCM foundation (`CAND-CAP-0007` through
`CAND-CAP-0010`) is consumed only as prerequisite dependency-state context; and
TEP `CAND-CAP-0011` through `CAND-CAP-0021`, `CAND-CAP-0041`, and `CAND-CAP-0042`
are consumed only as sibling/context references.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0044`.
3. AC-03: Legacy Excel ID `MOD-0334` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `tep.restricted-integrity-registry`.
5. AC-05: Permission namespace is limited to
   `tep.restricted-integrity-registry.read`,
   `tep.restricted-integrity-registry.manage`,
   `tep.restricted-integrity-registry.evaluate`, and
   `tep.restricted-integrity-registry.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.TalentEcosystemService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection
   `tep_restricted_integrity_registry_readiness`) includes active tenant `Code`
   uniqueness (`ux_tep_restricted_integrity_registry_tenant_code_active`),
   tenant/state indexing (`ix_tep_restricted_integrity_registry_tenant_state`),
   soft delete, and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Real restricted-integrity case handling, case body / allegation /
    finding record persistence, restriction-scope execution, evidence-chain
    custody execution, disclosure-control execution, case-review execution, model
    output, disclosure/adverse-action, and automated decision behavior remain
    unauthorized.
14. AC-14: Investigator/reviewer/subject restricted-integrity fields remain
    reserved/out of scope; no money field is present; no case-body/allegation/
    finding/evidence/PII/individual-attribution/free-text/attachment field is
    present.
15. AC-15: Restricted-integrity case bodies, allegations, findings, evidence
    content, disclosure/adverse-action decisions, candidate/individual PII,
    individual attributions, free-text notes, attachment payload, raw provider
    payload, credential/token/secret/password, and PII-heavy persistence remain
    unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: Upstream source, governance, TEP, HCM foundation, and PSS dependency
    boundaries are recorded (`CAND-CAP-0043` Early Warning Signals as upstream
    source; consent-policy and legal-hold as governance dependencies; TEP
    `CAND-CAP-0011` through `CAND-CAP-0021`, `CAND-CAP-0041`, and `CAND-CAP-0042`
    as sibling/context; HCM `CAND-CAP-0007` through `CAND-CAP-0010` as
    prerequisite context).
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0044|MOD-0334` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build TEP API:
  `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `RestrictedIntegrityRegistry` filter
  (`Application.Tests/RestrictedIntegrityRegistryTests.cs`, handler behavior;
  fix-absent should be RED).
- Run full TEP Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_tep_restricted_integrity_registry_tenant_code_active`), and tenant/state
  indexing (`ix_tep_restricted_integrity_registry_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`integrity`, `case`,
  `restriction`, `evidence`, `disclosure`, `review`) are not falsely rejected.
- Verify forbidden catalog/restriction/evidence/disclosure/review/decision/
  sensitive fields are absent.
- Verify investigator/reviewer/subject restricted-integrity fields are
  reserved/absent, no money field exists, and no case-body/allegation/finding/
  evidence/PII/individual-attribution/free-text/attachment field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0044|MOD-0334" services frontend gateway`
  returns no runtime literal (candidate IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real restricted-integrity case handling prohibition is accepted.
- [x] Integrity case body / allegation / finding record persistence prohibition
  is accepted.
- [x] Restriction-scope / evidence-chain custody / disclosure-control / case
  review execution prohibition is accepted.
- [x] Disclosure/adverse-action and model-output prohibition is accepted.
- [x] Disclosure/adverse-action and automated decision behavior prohibition is
  accepted.
- [x] Reserved investigator/reviewer/subject restricted-integrity fields,
  no-money-field, and no-case-body/allegation/finding/evidence/PII/individual-
  attribution/free-text/attachment constraints are accepted.
- [x] Sensitive/PII, restricted-integrity case content, evidence content, and raw
  payload prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Early-warning source, consent-policy, legal-hold, TEP/HCM/PSS dependency
  context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only restricted integrity registry readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Restricted-integrity data minimization policy.
- [x] Retention/evidence/audit/legal-hold/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.TalentEcosystemService/**`
  and the owned `frontend/Diten.Web/**` RestrictedIntegrityRegistry objects.
- Runtime owner/key remains limited to `tep.restricted-integrity-registry`.
- Permission namespace remains limited to
  `tep.restricted-integrity-registry.read`,
  `tep.restricted-integrity-registry.manage`,
  `tep.restricted-integrity-registry.evaluate`, and
  `tep.restricted-integrity-registry.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `EarlyWarningSourceDependencyState`, `ConsentPolicyDependencyState`,
  `LegalHoldDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Investigator/reviewer/subject restricted-integrity UX fields remain
  reserved/out of scope, no money field is present, and no case-body/allegation/
  finding/evidence/PII/individual-attribution/free-text/attachment field is
  present.
- HCM, PSS, and Platform scopes remain closed.
- Real restricted-integrity case handling, case body / allegation / finding
  record persistence, restriction-scope execution, evidence-chain custody
  execution, disclosure-control execution, case-review execution, model output,
  disclosure/adverse-action, automated decision behavior, notification/document
  integration, restricted-integrity case bodies, allegations, findings, evidence
  content, disclosure/adverse-action decisions, candidate/individual PII,
  individual attributions, free-text notes, attachment payload, raw provider
  payload, credential/token/secret/password, and PII-heavy persistence remain out
  of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0044 --name "Restricted Integrity Registry"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0044`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Restricted-integrity data minimization fail-closed policy approved.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0044|MOD-0334" services frontend gateway`
- Candidate IDs are governance-only and must return no runtime literal match.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real integrity case intake and catalog management.
- Restriction-scope execution and enforcement.
- Evidence-chain custody execution and chain-of-custody management.
- Disclosure-control execution and disclosure/adverse-action decisions.
- Case review and automated integrity adjudication.
- Automated decision approval.
- Investigator/reviewer/subject restricted-integrity UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
