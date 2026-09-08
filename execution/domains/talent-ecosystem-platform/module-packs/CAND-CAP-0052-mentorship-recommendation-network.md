---
id: CAND-CAP-0052
name: Mentorship & Recommendation Network
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0052-mentorship-recommendation-network
started: 2026-09-08
target: 2026-12-08
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0052
legacy_excel_id: MOD-0343
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.TalentEcosystemService
runtime_service_port: 5060
gateway_port: 5080
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.mentorship-recommendation-network
permission_namespace:
  - tep.mentorship-recommendation-network.read
  - tep.mentorship-recommendation-network.manage
  - tep.mentorship-recommendation-network.evaluate
  - tep.mentorship-recommendation-network.audit.read
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0052 - Mentorship & Recommendation Network

> Status: draft. This pack records the first metadata-only backend/API
> mentorship & recommendation network readiness contract slice under
> `services/Diten.TalentEcosystemService/**`, plus a golden-compact readiness
> CRUD frontend under `frontend/Diten.Web/**` and gateway exposure through the
> API gateway (`5080` -> TEP `5060`). This is a consent-, visibility-, and
> publication-governed, privacy sensitive network: real mentorship &
> recommendation network handling, network-catalog / pairing-intake /
> recommendation-scope content persistence, recommendation-scope execution,
> visibility-control execution, network-review adjudication, mentor/mentee
> pairing persistence, member roster or participant-list persistence,
> recommendation / message / conversation content persistence,
> participant/individual PII or contact persistence, free-text
> recommendation/endorsement narrative persistence, model output or automated
> decision behavior, and sensitive/PII-heavy persistence remain closed.
> `CAND-CAP-0052` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0343` remains blocked for TEP use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0052` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R4 capability named `Mentorship & Recommendation
Network`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0343`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0052` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API mentorship &
recommendation network readiness contract slice, mirroring the completed TEP
candidate readiness slices (`CAND-CAP-0017` / `MOD-0322` through `CAND-CAP-0021`
/ `MOD-0331`), the completed `CAND-CAP-0041` / `MOD-0336` Talent Data Foundation
slice, the completed `CAND-CAP-0042` / `MOD-0332` Hiring Risk Indicators slice,
the completed `CAND-CAP-0043` / `MOD-0333` Early Warning Signals slice, the
completed `CAND-CAP-0044` / `MOD-0334` Restricted Integrity Registry slice, the
completed `CAND-CAP-0045` / `MOD-0335` Professional Reputation Ledger slice, the
completed `CAND-CAP-0046` / `MOD-0337` Industry Talent Pool slice, the
completed `CAND-CAP-0047` / `MOD-0338` Industry Skill Passport slice, the
completed `CAND-CAP-0048` / `MOD-0339` Candidate Career Passport slice, the
completed `CAND-CAP-0049` / `MOD-0340` Talent Development Network slice, the
completed `CAND-CAP-0050` / `MOD-0341` Industry Succession Pool slice, and the
completed `CAND-CAP-0051` / `MOD-0342` Verified Certification Registry slice
exactly. The Mentorship & Recommendation Network capability is the TEP-native
readiness layer that declares the readiness of a consent- and
visibility-governed mentorship and recommendation network: it declares the
readiness STATE of the network catalog, pairing intake, recommendation scope,
visibility control, and network review. It depends on the Talent Data Foundation
capability (`CAND-CAP-0041` / `MOD-0336`) as its upstream talent data source, on
the Professional Reputation Ledger capability (`CAND-CAP-0045` / `MOD-0335`) as
its upstream reputation source, and on consent-policy governance as its
controlling dependency. Because this is a consent-, visibility-, and
publication-governed, privacy sensitive network, its sensitivity posture is
paramount: it stores NO mentor/mentee pairings, NO member rosters, NO
participant lists, NO recommendation content, NO message/conversation content,
NO network-catalog record content, NO pairing-intake content, NO network
narrative/assessment content, NO participant contact/profile content, NO
recommendation scores, NO individual recommendation rankings, NO participant
PII, NO free-text recommendation/endorsement narrative, and NO attachments -
only boundary/readiness STATE metadata. Real mentorship & recommendation network
handling, network-catalog / pairing-intake / recommendation-scope content
persistence, recommendation-scope execution, visibility-control execution,
network-review adjudication, participant PII or roster persistence,
free-text/narrative/raw payload persistence, model output, automated decision
behavior, mentor/mentee/reviewer network UX, notification/document integration,
and sensitive/PII-heavy persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- TEP-native mentorship & recommendation network governance boundary.
- Sequencing after completed HCM foundation modules, completed TEP foundation and
  candidate readiness modules through `CAND-CAP-0021`, the completed
  `CAND-CAP-0042` Hiring Risk Indicators and `CAND-CAP-0043` Early Warning
  Signals context dependencies, the completed `CAND-CAP-0044` Restricted
  Integrity Registry context dependency, the completed `CAND-CAP-0045`
  Professional Reputation Ledger reputation-source dependency, the completed
  `CAND-CAP-0046` Industry Talent Pool context dependency, the completed
  `CAND-CAP-0047` Industry Skill Passport context dependency, the completed
  `CAND-CAP-0048` Candidate Career Passport context dependency, the completed
  `CAND-CAP-0049` Talent Development Network context dependency, the completed
  `CAND-CAP-0050` Industry Succession Pool context dependency, the completed
  `CAND-CAP-0051` Verified Certification Registry context dependency, and the
  completed `CAND-CAP-0041` Talent Data Foundation data-source dependency.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only mentorship & recommendation network readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the network catalog, pairing intake,
  recommendation scope, visibility control, and network review.
- Explicit exclusion of real mentorship & recommendation network handling and
  network-catalog / pairing-intake / recommendation-scope content persistence.
- Explicit exclusion of network catalog execution, pairing-intake execution,
  recommendation-scope execution, visibility-control execution, network-review
  execution, recommendation scores, model output, and automated decision
  behavior.
- Explicit exclusion of mentor-facing, mentee-facing, and reviewer-facing
  network UX beyond the readiness-metadata CRUD (those fields are reserved and
  out of scope).
- Explicit exclusion of mentor/mentee pairings, member rosters, participant
  lists, recommendation content, message/conversation content, network-catalog
  record content, pairing-intake content, network narrative/assessment content,
  participant contact/profile content, recommendation scores, participant PII,
  individual recommendation rankings, free-text, endorsement narrative,
  attachment payload, raw provider payload, credential/token/secret/password, and
  any PII dataset persistence.

Not owned by this pack:

- Real mentorship & recommendation network handling, scoring, or ranking execution.
- Mentor/mentee pairing, member roster, or participant-list content storage.
- Network catalog execution, intake runs, or catalog collection output.
- Recommendation-scope execution, scope enforcement, or scope output.
- Visibility-control execution, visibility delivery, visibility decisions, or
  visibility action logs.
- Network-review execution, adjudication output, or automated network
  decisions.
- Talent-data ownership beyond dependency/context references (owned by
  `CAND-CAP-0041` Talent Data Foundation).
- Reputation-ledger ownership beyond dependency/context references (owned by
  `CAND-CAP-0045` Professional Reputation Ledger).
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
| MentorshipRecommendationNetworkCapabilityBoundary | Governance boundary | Defines what a future TEP mentorship & recommendation network module may own. |
| MentorshipRecommendationNetworkReadinessBoundary | Governance boundary | Separates readiness metadata from mentorship & recommendation network execution. |
| NetworkCatalogBoundary | Governance boundary | Blocks network-catalog record / member roster persistence. |
| PairingIntakeBoundary | Governance boundary | Blocks real pairing-intake execution and mentor/mentee pairing content persistence. |
| RecommendationScopeBoundary | Governance boundary | Blocks recommendation-scope execution, recommendation narrative, and raw recommendation payload persistence. |
| VisibilityControlBoundary | Governance boundary | Blocks visibility-control execution, visibility decisions, and visibility action logs. |
| NetworkReviewBoundary | Governance boundary | Blocks network review execution, adjudication, and review output. |
| MentorshipRecommendationNetworkDecisionBoundary | Governance boundary | Blocks recommendation-score, model-output, message-content, and automated decision persistence. |
| MentorshipRecommendationNetworkUxBoundary | Governance boundary | Blocks mentor, mentee, and reviewer network UX beyond readiness-metadata CRUD. |
| MentorshipRecommendationNetworkSensitiveDataBoundary | Governance boundary | Blocks mentor/mentee pairings, member rosters, participant lists, recommendation/message/conversation content, participant profile content, recommendation scores, PII, individual rankings, free-text, endorsement narrative, and raw payload persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| NetworkCatalogEngine | Deferred runtime | Real network-catalog intake, catalog management, and roster / catalog capture; not authorized. |
| PairingIntakeEngine | Deferred runtime | Pairing-intake execution, mentor/mentee pairing capture, and intake output; not authorized. |
| RecommendationScopeEngine | Deferred runtime | Recommendation-scope execution, recommendation narrative capture, and scope output; not authorized. |
| VisibilityControlEngine | Deferred runtime | Visibility-control execution, visibility decisions, and visibility action logs; not authorized. |
| NetworkReviewEngine | Deferred runtime | Network review execution, adjudication, and automated network output; not authorized. |
| MentorMenteeReviewerExperience | Deferred frontend/runtime | Mentor/mentee/reviewer network UX beyond readiness CRUD; not authorized. |
| MentorshipRecommendationNetworkDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| MentorshipRecommendationNetworkNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| MentorshipRecommendationNetworkReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/MentorshipRecommendationNetworkReadinessMetadata.cs`. |
| MentorshipRecommendationNetworkReadinessState | Domain enum | Readiness state; `Domain/Enums/MentorshipRecommendationNetworkReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IMentorshipRecommendationNetworkReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/IMentorshipRecommendationNetworkReadinessMetadataRepository.cs`. |
| MongoMentorshipRecommendationNetworkReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `tep_mentorship_recommendation_network_readiness`; indexes `ux_tep_mentorship_recommendation_network_tenant_code_active` and `ix_tep_mentorship_recommendation_network_tenant_state`. |
| MentorshipRecommendationNetwork Application features | Application (CQRS/MediatR) | `Application/Features/MentorshipRecommendationNetwork/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| MentorshipRecommendationNetworkController | API controller | Thin controller; `Api/Controllers/Tep/MentorshipRecommendationNetworkController.cs`. |
| MentorshipRecommendationNetwork golden-compact CRUD | Frontend | Controller + `Views/TalentEcosystem/MentorshipRecommendationNetwork/**` + `wwwroot/assets/js/TalentEcosystem/MentorshipRecommendationNetwork/**` + Resources + TEP nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `MentorshipRecommendationNetworkReadinessState`):

- `Code`
- `DisplayName`
- `MentorshipRecommendationNetworkReadinessState`
- `NetworkCatalogBoundaryState`
- `PairingIntakeBoundaryState`
- `RecommendationScopeBoundaryState`
- `VisibilityControlBoundaryState`
- `NetworkReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `TalentDataSourceDependencyState`
- `ConsentPolicyDependencyState`
- `ReputationSourceDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `PublicationPolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `MentorshipRecommendationNetworkReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Marker-safe field naming discipline:

- The persisted field names deliberately avoid the singular forbidden tokens
  `mentor` and `mentee` and the forbidden tokens `roster`, `member`, and
  `message`. The pairing scope is named with the legitimate domain word
  `Pairing` (as in `PairingIntakeBoundaryState`), and the recommendation scope
  uses the `Recommendation` naming (`RecommendationScopeBoundaryState`) rather
  than `endorsement`/`message` wording. This keeps the entity contract
  marker-safe under a WORD/TOKEN boundary (`\b` regex) forbidden-marker guard:
  legitimate domain words (`mentorship`, `recommendation`, `network`, `pairing`,
  `visibility`, `reputation`) are not falsely rejected, while true forbidden
  markers (`mentor`, `mentee`, `roster`, `member`, `message`, `conversation`,
  `endorsement`, `contact`, `narrative`, `list`, `PII`, `payload`) are still
  caught. The word-boundary discipline is deliberate: the legitimate word
  `mentorship` must pass while the singular forbidden token `mentor` (and
  `mentee`) is still rejected.

Reserved / out-of-scope fields (must not be added in this slice):

- Mentor-facing network fields (mentor pairing submission, mentor review, mentor
  action UX state) are reserved and out of scope.
- Mentee-facing network fields (mentee pairing input, mentee decision, mentee
  self-service UX state) are reserved and out of scope.
- Reviewer-facing network fields (reviewer response, reviewer acknowledgement,
  reviewer self-service UX state) are reserved and out of scope.
- No money field (salary/compensation/cost/budget). This is a metadata-only
  readiness slice with no monetary attribute.
- No mentor/mentee pairing, member roster, participant list, recommendation
  content, message/conversation content, participant profile content,
  recommendation score, participant PII, individual ranking, free-text,
  endorsement narrative, or attachment field of any kind.

Forbidden field classes:

- Network-catalog record body, pairing-intake payload, recommendation-scope
  payload, participant profile content, visibility-control execution payload,
  network-review payload, or completed mentorship-recommendation-network content.
- Recommendation score result, recommendation ranking value, ordering result,
  rank, match confidence, model output, recommendation output, or evaluator
  scoring payload.
- Mentor/mentee pairing, member roster / participant list content, participant
  PII, individual ranking, participant profile content, recommendation content,
  message/conversation content, network narrative, assessment content,
  visibility content, cover letter, free-text profile narrative, endorsement
  narrative, HR notes, reviewer notes, or sensitive individual body.
- Attachment payload, controlled document body, external document content, raw
  message, raw provider payload, or uploaded file metadata.
- Salary amount, compensation amount, benefits election, payroll details, tax
  details, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  any PII-heavy participant/individual field.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0052-mentorship-recommendation-network.md`

Candidate runtime service:

- `Diten.TalentEcosystemService` (port `5060`)

Runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/TalentEcosystem/MentorshipRecommendationNetwork/**`,
  `wwwroot/assets/js/TalentEcosystem/MentorshipRecommendationNetwork/**`, Resources, TEP nav
  entry).
- Gateway route exposure through the API gateway (`5080` -> TEP `5060`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.TalentEcosystemService/**` plus its golden-compact
readiness CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned MentorshipRecommendationNetwork CRUD objects
  listed in §3/§5.
- `gateway/**` outside the owned MentorshipRecommendationNetwork readiness route.
- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `services/Diten.PssService/**`
- global `tests/**`
- completed TEP foundation and candidate readiness packs (including the
  `CAND-CAP-0041` Talent Data Foundation data-source dependency, `CAND-CAP-0042`
  Hiring Risk Indicators, `CAND-CAP-0043` Early Warning Signals, `CAND-CAP-0044`
  Restricted Integrity Registry, `CAND-CAP-0045` Professional Reputation
  Ledger, `CAND-CAP-0046` Industry Talent Pool, `CAND-CAP-0047` Industry
  Skill Passport, `CAND-CAP-0048` Candidate Career Passport, `CAND-CAP-0049`
  Talent Development Network, `CAND-CAP-0050` Industry Succession Pool, and
  `CAND-CAP-0051` Verified Certification Registry).
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
  talent data source for the mentorship & recommendation network via
  `TalentDataSourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as a raw roster or data source.
- `CAND-CAP-0045` - Professional Reputation Ledger (`MOD-0335`), done. Consumed
  as the reputation source for the mentorship & recommendation network via
  `ReputationSourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as raw reputation ledger / recommendation content / roster
  content or a data source.

Governance/control dependencies:

- Consent policy, consumed via `ConsentPolicyDependencyState`. Because this is a
  consent-, visibility-, and publication-governed network, a valid
  consent basis is a precondition and is treated as boundary/readiness metadata
  only.
- Visibility and publication policy, consumed via
  `VisibilityControlBoundaryState` and `PublicationPolicyState`. Visibility/access,
  recommendation scope, and publication policy are controlling preconditions
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
- `CAND-CAP-0045` - Professional Reputation Ledger (`MOD-0335`), done. Upstream
  reputation source and sibling/context.
- `CAND-CAP-0046` - Industry Talent Pool (`MOD-0337`), done. Sibling/context.
- `CAND-CAP-0047` - Industry Skill Passport (`MOD-0338`), done. Sibling/context.
- `CAND-CAP-0048` - Candidate Career Passport (`MOD-0339`), done. Sibling/context.
- `CAND-CAP-0049` - Talent Development Network (`MOD-0340`), done. Sibling/context.
- `CAND-CAP-0050` - Industry Succession Pool (`MOD-0341`), done. Sibling/context.
- `CAND-CAP-0051` - Verified Certification Registry (`MOD-0342`), done. Sibling/context.

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

- Runtime owner/key: `tep.mentorship-recommendation-network`.
- Permission namespace:
  - `tep.mentorship-recommendation-network.read`
  - `tep.mentorship-recommendation-network.manage`
  - `tep.mentorship-recommendation-network.evaluate`
  - `tep.mentorship-recommendation-network.audit.read`
- Runtime repo scope: `services/Diten.TalentEcosystemService/**`.
- Thin controller (`Api/Controllers/Tep/MentorshipRecommendationNetworkController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `tep_mentorship_recommendation_network_readiness`.
- Active tenant `Code` uniqueness through
  `ux_tep_mentorship_recommendation_network_tenant_code_active`, with
  `ix_tep_mentorship_recommendation_network_tenant_state` supporting tenant/state
  queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`mentorship`,
  `recommendation`, `network`, `pairing`, `visibility`, `reputation`) are not
  falsely rejected while true forbidden markers (mentor, mentee, roster, member,
  message, conversation, endorsement, contact, narrative, assessment, score,
  ranking, PII, payload) are still caught. The word-boundary discipline is
  deliberate: the legitimate word `mentorship` must pass while the singular
  forbidden token `mentor` is still rejected.

Explicitly unauthorized:

- Real mentorship & recommendation network handling, scoring, or ranking execution.
- Network-catalog record / pairing-intake / recommendation-scope record persistence.
- Pairing-intake execution, enforcement, or pairing payload persistence.
- Recommendation-scope execution, recommendation narrative capture, or scope output.
- Visibility-control execution, visibility delivery, visibility decisions, or
  visibility action logs.
- Network-review execution, adjudication, or automated network output.
- Recommendation-score/finding/model-output persistence.
- Automated decision and recommendation-scoring/ranking behavior.
- Mentor/mentee/reviewer network UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Mentor/mentee pairings, member rosters, participant lists, recommendation
  content, message/conversation content, recommendation-scope narrative,
  participant profile content, recommendation scores, participant PII, individual
  rankings, free-text, endorsement narrative, attachment payload, or raw payload
  persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- TEP tenant shell nav entry `Mentorship & Recommendation Network`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Mentor network UX beyond readiness-metadata CRUD.
- Mentee network/pairing UX beyond readiness-metadata CRUD.
- Reviewer network/self-service UX beyond readiness-metadata CRUD.
- Network catalog, pairing intake, recommendation scope, visibility control,
  network review, recommendation-score, or model-output UI.
- Document, notification, export, upload, or attachment UI.
- Any mentor/mentee pairing, member roster, participant list, recommendation
  content, message/conversation content, participant profile content,
  recommendation score, participant PII, individual ranking, free-text,
  endorsement narrative, assessment content, or raw payload viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> TEP `5060`).

Authorized API surface:

- `GET /api/mentorship-recommendation-network` - list readiness metadata.
- `GET /api/mentorship-recommendation-network/{id}` - get readiness metadata by id.
- `POST /api/mentorship-recommendation-network` - create readiness metadata.
- `POST /api/mentorship-recommendation-network/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/mentorship-recommendation-network/{id}` - soft-delete.
- `GET /api/mentorship-recommendation-network/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get mentorship & recommendation network readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No mentorship & recommendation network handling, network-catalog record /
  pairing-intake / recommendation-scope record persistence, pairing-intake
  execution, recommendation-scope execution, visibility-control execution, network-review
  execution, recommendation-score or model-output persistence,
  recommendation-scoring or automated decision behavior, notification/document
  integration, or
  mentor-mentee-pairing/roster/list/message/PII/individual-ranking/
  free-text/endorsement/narrative/raw-payload persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only mentorship & recommendation network readiness state.
- Boundary-state metadata for network catalog, pairing intake, recommendation
  scope, visibility control, network review, automated decision, consent,
  minimization, retention, and publication policy.
- Dependency-state metadata for talent-data source, consent-policy,
  reputation-source, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Pairing-intake/catalog payload.
- Network-catalog record body, pairing-intake content, or recommendation-scope content.
- Pairing-intake execution/enforcement payload.
- Recommendation-scope execution payload, recommendation narrative, or scope
  output.
- Visibility-control execution payload, visibility delivery, visibility
  decisions, or visibility action logs.
- Network-review/adjudication payload.
- Recommendation score result / recommendation ranking value / ordering result / rank / match
  confidence / model output.
- Mentor/mentee pairing, member roster / participant list content, participant
  PII, individual ranking, participant profile content, recommendation content,
  message/conversation content, visibility content, endorsement narrative, or
  free-text.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy participant/individual details.
- Compensation/payroll/benefits payload.

Consent-sensitivity note: because this is a consent-, visibility-, and
publication-governed, privacy-sensitive mentorship & recommendation network, no
mentor/mentee pairing, member roster, participant list, recommendation content,
message/conversation content, recommendation narrative, recommendation score, or
individual ranking of any kind may cross the boundary into persistence. Only
boundary/readiness STATE metadata governed by a valid consent basis, visibility
scope, and publication policy is permitted.

## 12. Permission Boundary

Approved permission namespace:

- `tep.mentorship-recommendation-network.read`
- `tep.mentorship-recommendation-network.manage`
- `tep.mentorship-recommendation-network.evaluate`
- `tep.mentorship-recommendation-network.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Runtime work must be tenant-aware and fail closed. The backend/API slice must
resolve tenant server-side and must not expose `TenantId` in request DTOs.

Security guardrails:

- Candidate identity (`CAND-CAP-0052`) must not become a runtime literal.
- Blocked legacy ID (`MOD-0343`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_tep_mentorship_recommendation_network_tenant_code_active` and
  `ix_tep_mentorship_recommendation_network_tenant_state`.
- Because this is a consent-, visibility-, and publication-governed,
  privacy-sensitive network, consent basis, visibility scope, and
  publication policy are treated as the highest-risk controls even for
  readiness metadata; access is restricted and audited.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation, with a valid consent basis
  treated as a precondition to activation.
- Mentorship-recommendation-network data minimization fail-closed behavior.
- Retention/publication/audit/visibility/deletion as local/deferred metadata,
  with visibility scope and publication policy treated as controlling
  preconditions.
- Exclusion of mentor/mentee pairings, member rosters, participant lists,
  recommendation content, message/conversation content, participant profile
  content, recommendation scores, participant PII, individual rankings,
  free-text, endorsement narrative, attachments, and any PII dataset.

The Mentorship & Recommendation Network capability declares readiness STATE only;
it does not own or store any mentor/mentee pairings, member rosters, participant
lists, recommendation content, message/conversation content, participant profile
content, recommendation scores, participant PII, individual rankings, or
free-text, and all downstream consumers must treat its output as
boundary/readiness metadata, not as a network-handling or data source. Because
this is a consent-, visibility-, and publication-governed,
privacy-sensitive network, its consent-basis, visibility, publication,
retention, and restricted-access constraints are treated as the
highest-risk controls: no mentor/mentee pairing, member roster, participant
list, recommendation content, recommendation score, or individual ranking may
ever be persisted in this slice, and access to even the readiness metadata is
restricted and audited.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, pairing-intake
connector, or document/notification UI is authorized. The `CAND-CAP-0041` Talent
Data Foundation capability is consumed only as an upstream source dependency-state
context; the `CAND-CAP-0045` Professional Reputation Ledger capability is consumed
only as an upstream reputation-source dependency-state context; consent-policy and
visibility-policy are consumed only as governance dependency-state context; HCM
foundation (`CAND-CAP-0007` through `CAND-CAP-0010`) is consumed only as
prerequisite dependency-state context; and TEP `CAND-CAP-0011` through
`CAND-CAP-0021`, `CAND-CAP-0042`, `CAND-CAP-0043`, `CAND-CAP-0044`,
`CAND-CAP-0045`, `CAND-CAP-0046`, `CAND-CAP-0047`, `CAND-CAP-0048`,
`CAND-CAP-0049`, `CAND-CAP-0050`, and `CAND-CAP-0051` are consumed only as
sibling/context references.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0052`.
3. AC-03: Legacy Excel ID `MOD-0343` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `tep.mentorship-recommendation-network`.
5. AC-05: Permission namespace is limited to
   `tep.mentorship-recommendation-network.read`,
   `tep.mentorship-recommendation-network.manage`,
   `tep.mentorship-recommendation-network.evaluate`, and
   `tep.mentorship-recommendation-network.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.TalentEcosystemService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection
   `tep_mentorship_recommendation_network_readiness`) includes active tenant `Code`
   uniqueness (`ux_tep_mentorship_recommendation_network_tenant_code_active`),
   tenant/state indexing (`ix_tep_mentorship_recommendation_network_tenant_state`),
   soft delete, and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Real mentorship & recommendation network handling, network-catalog
    record / pairing-intake / recommendation-scope record persistence,
    pairing-intake execution, recommendation-scope execution,
    visibility-control execution, network-review execution, model output,
    recommendation-score, mentor/mentee pairing or member roster
    persistence, recommendation/message/conversation content persistence, and
    automated decision behavior remain unauthorized.
14. AC-14: Mentor/mentee/reviewer network fields remain reserved/out of
    scope; no money field is present; no mentor/mentee-pairing/roster/list/message/
    PII/individual-ranking/free-text/endorsement/narrative field is present.
    Field names remain marker-safe (`Mentorship`/`Recommendation`/`Pairing`
    naming, never the singular tokens `mentor`/`mentee`).
15. AC-15: Mentor/mentee pairings, member rosters, participant lists,
    recommendation content, message/conversation content, participant
    profile content, recommendation scores, participant PII, individual rankings,
    free-text, endorsement narrative, attachment payload, raw provider payload,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized. The forbidden-marker guard asserts a WORD/TOKEN boundary
    (`\b` regex) match, NOT a bare substring `Contains`, so `mentorship`
    passes while `mentor`/`mentee`/`roster`/`member` are caught.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: Upstream source, governance, TEP, HCM foundation, and PSS dependency
    boundaries are recorded (`CAND-CAP-0041` Talent Data Foundation as upstream
    talent data source; `CAND-CAP-0045` Professional Reputation Ledger as upstream
    reputation source; consent-policy and visibility-policy as governance
    dependencies; TEP `CAND-CAP-0011` through `CAND-CAP-0021`, `CAND-CAP-0042`,
    `CAND-CAP-0043`, `CAND-CAP-0044`, `CAND-CAP-0045`, `CAND-CAP-0046`,
    `CAND-CAP-0047`, `CAND-CAP-0048`, `CAND-CAP-0049`, `CAND-CAP-0050`, and
    `CAND-CAP-0051` as sibling/context; HCM `CAND-CAP-0007` through
    `CAND-CAP-0010` as prerequisite context).
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0052|MOD-0343` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build TEP API:
  `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `MentorshipRecommendationNetwork` filter
  (`Application.Tests/MentorshipRecommendationNetworkTests.cs`, handler behavior;
  fix-absent should be RED).
- Run full TEP Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_tep_mentorship_recommendation_network_tenant_code_active`), and tenant/state
  indexing (`ix_tep_mentorship_recommendation_network_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`mentorship`,
  `recommendation`, `network`, `pairing`, `visibility`, `reputation`) are not
  falsely rejected while the singular tokens `mentor`, `mentee`, `roster`,
  and `member` are still caught.
- Verify forbidden catalog/pairing-intake/recommendation-scope/visibility/network-review/
  decision/sensitive fields are absent.
- Verify mentor/mentee/reviewer network fields are reserved/absent, no
  money field exists, and no mentor/mentee-pairing/roster/list/message/
  PII/individual-ranking/free-text/endorsement/narrative field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0052|MOD-0343" services frontend gateway`
  returns no runtime literal (candidate IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real mentorship & recommendation network handling prohibition is accepted.
- [x] Network-catalog record / pairing-intake / recommendation-scope record persistence
  prohibition is accepted.
- [x] Pairing-intake / recommendation-scope / visibility-control / network
  review execution prohibition is accepted.
- [x] Recommendation-score and model-output prohibition is accepted.
- [x] Recommendation-scoring/ranking and automated decision behavior prohibition is
  accepted.
- [x] Reserved mentor/mentee/reviewer network fields, no-money-field, and
  no-mentor/mentee-pairing/roster/list/message/PII/individual-ranking/
  free-text/endorsement/narrative constraints are accepted.
- [x] Sensitive/PII, recommendation/message/conversation content, member roster content, and raw
  payload prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Talent-data source, reputation source, consent-policy, visibility-policy,
  TEP/HCM/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only mentorship & recommendation network readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Mentorship-recommendation-network data minimization policy.
- [x] Retention/publication/audit/visibility/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.TalentEcosystemService/**`
  and the owned `frontend/Diten.Web/**` MentorshipRecommendationNetwork objects.
- Runtime owner/key remains limited to `tep.mentorship-recommendation-network`.
- Permission namespace remains limited to
  `tep.mentorship-recommendation-network.read`,
  `tep.mentorship-recommendation-network.manage`,
  `tep.mentorship-recommendation-network.evaluate`, and
  `tep.mentorship-recommendation-network.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract, and remain marker-safe (`Mentorship`/
  `Recommendation`/`Pairing` naming, never the singular tokens
  `mentor`/`mentee`).
- `TalentDataSourceDependencyState`, `ConsentPolicyDependencyState`,
  `ReputationSourceDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Mentor/mentee/reviewer network UX fields remain reserved/out of scope,
  no money field is present, and no mentor/mentee-pairing/roster/list/message/
  PII/individual-ranking/free-text/endorsement/narrative field is present.
- HCM, PSS, and Platform scopes remain closed.
- Real mentorship & recommendation network handling, network-catalog record /
  pairing-intake / recommendation-scope record persistence, pairing-intake
  execution, recommendation-scope execution, visibility-control execution,
  network-review execution, model output, recommendation-score,
  automated decision behavior, notification/document integration,
  mentor/mentee pairings, member rosters, participant lists, recommendation
  content, message/conversation content, participant profile content,
  recommendation scores, participant PII, individual rankings,
  free-text, endorsement narrative, attachment payload, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence remain out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0052 --name "Mentorship & Recommendation Network"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0052`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Mentorship-recommendation-network data minimization fail-closed policy approved.
- Retention/publication/audit/visibility/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0052|MOD-0343" services frontend gateway`
- Candidate IDs are governance-only and must return no runtime literal match.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real network-catalog intake and catalog management.
- Pairing-intake execution and enforcement.
- Recommendation-scope execution and recommendation management.
- Visibility-control execution and visibility decisions.
- Network review and automated network adjudication.
- Automated decision approval.
- Mentor/mentee/reviewer network UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
