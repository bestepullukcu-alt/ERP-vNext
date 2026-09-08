---
id: CAND-CAP-0058
name: Sector Mobility Intelligence
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0058-sector-mobility-intelligence
started: 2026-09-08
target: 2026-12-08
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0058
legacy_excel_id: MOD-0349
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.TalentEcosystemService
runtime_service_port: 5060
gateway_port: 5080
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.sector-mobility-intelligence
permission_namespace:
  - tep.sector-mobility-intelligence.read
  - tep.sector-mobility-intelligence.manage
  - tep.sector-mobility-intelligence.evaluate
  - tep.sector-mobility-intelligence.audit.read
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0058 - Sector Mobility Intelligence

> Status: draft. This pack records the first metadata-only backend/API sector
> mobility intelligence readiness contract slice under
> `services/Diten.TalentEcosystemService/**`, plus a golden-compact readiness
> CRUD frontend under `frontend/Diten.Web/**` and gateway exposure through the
> API gateway (`5080` -> TEP `5060`). This is a consent-, visibility-, and
> publication-governed, privacy sensitive capability: real mobility-score/flow
> output/measurement value handling, mobility-catalog / flow-binding-intake /
> corridor-scope content persistence, corridor-scope execution, visibility-control
> execution, mobility-review adjudication, inflow/outflow-quantity/rate or per-individual
> mobility-data persistence, mobility-score/flow-output/query result-value persistence,
> employee/company roster or participant-list persistence, corridor/transition cell
> value or aggregated distribution persistence, participant/individual PII or
> contact persistence, free-text mobility/assessment narrative persistence, model
> output or automated decision behavior, and sensitive/PII-heavy persistence
> remain closed. `CAND-CAP-0058` remains a governance/documentation identity only
> and must not be written into runtime literals. `MOD-0349` remains blocked for
> TEP use until future EA canonical MOD assignment.
>
> MARKER-SAFE IDENTITY NOTE (see §4 and §19): unlike `CAND-CAP-0053` (which needed
> the `PayBenchmarking` marker-safe alias because the field-name contract test
> forbids property names containing "Salary"/"Amount"/"Wage"), the runtime C#
> type, namespace, and property identity for this capability IS
> `SectorMobilityIntelligence` - type == slug aligned - and carries no
> field-name-test fragment to avoid. The public slug/route/permission/collection
> use the string constant `sector-mobility-intelligence` (and
> `tep_sector_mobility_intelligence_readiness`), and the visible l10n title is
> "Sector Mobility Intelligence" / "Sektörel Hareketlilik Zekâsı".
> The marker-safe field-naming discipline is still documented and enforced: the
> forbidden-marker guard uses a WORD/TOKEN boundary (`\b` regex) matcher, NOT a
> bare substring `Contains`, so legitimate domain words (`sector`, `mobility`,
> `flow`, `binding`, `corridor`, `catalog`, `visibility`, `taxonomy`)
> pass while true forbidden markers (`measurement`, `value`, `result`, `figure`,
> `distribution`, `roster`, `headcount`, `PII`, `payload`, `score`, `ranking`)
> are still caught.

## 1. Module Summary

`CAND-CAP-0058` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R4 capability named `Sector Mobility Intelligence`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0349`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0058` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API sector mobility intelligence
readiness contract slice, mirroring the completed TEP candidate
readiness slices (`CAND-CAP-0017` / `MOD-0322` through `CAND-CAP-0021` /
`MOD-0331`), the completed `CAND-CAP-0041` / `MOD-0336` Talent Data Foundation
slice, the completed `CAND-CAP-0042` / `MOD-0332` Hiring Risk Indicators slice,
the completed `CAND-CAP-0043` / `MOD-0333` Early Warning Signals slice, the
completed `CAND-CAP-0044` / `MOD-0334` Restricted Integrity Registry slice, the
completed `CAND-CAP-0045` / `MOD-0335` Professional Reputation Ledger slice, the
completed `CAND-CAP-0046` / `MOD-0337` Industry Talent Pool slice, the completed
`CAND-CAP-0047` / `MOD-0338` Industry Skill Passport slice, the completed
`CAND-CAP-0048` / `MOD-0339` Candidate Career Passport slice, the completed
`CAND-CAP-0049` / `MOD-0340` Talent Development Network slice, the completed
`CAND-CAP-0050` / `MOD-0341` Industry Succession Pool slice, the completed
`CAND-CAP-0051` / `MOD-0342` Verified Certification Registry slice, the completed
`CAND-CAP-0052` / `MOD-0343` Mentorship & Recommendation Network slice, the
completed `CAND-CAP-0053` / `MOD-0344` Shared Salary Benchmarking slice, the
completed `CAND-CAP-0054` / `MOD-0345` Workforce Analytics slice, the completed
`CAND-CAP-0055` / `MOD-0346` Sector Talent Trends slice, the completed
`CAND-CAP-0056` / `MOD-0347` Talent Supply & Demand Forecasting slice, and the
completed `CAND-CAP-0057` / `MOD-0348` Skills Gap Heatmap slice exactly.
The Sector Mobility Intelligence capability is the TEP-native readiness layer
that declares the readiness of a consent-, visibility-, and publication-governed
sector mobility intelligence capability: it declares the readiness STATE of
the mobility catalog, flow binding intake, corridor scope, visibility control, and
mobility review. It depends on the Sector Talent Trends capability
(`CAND-CAP-0055` / `MOD-0346`) as its upstream sector trend source, on the
Workforce Analytics capability (`CAND-CAP-0054` / `MOD-0345`) as its upstream
workforce analytics source, and on the Industry Skill Passport
capability (`CAND-CAP-0047` / `MOD-0338`) as its upstream skills taxonomy
source. Because this is a consent-, visibility-, and publication-governed, privacy
sensitive capability, its sensitivity posture is paramount: it stores NO real
mobility-score/flow output/measurement values or results, NO corridor/transition cell
values or aggregated distributions, NO inflow/outflow-quantity/rate or per-individual
mobility data, NO mobility-score/flow-output/query result values, NO employee/company
rosters, NO participant lists, NO mobility-catalog record content, NO
flow-binding-intake content, NO mobility narrative/assessment content, NO
participant contact/profile content, NO mobility scores, NO individual/per-role mobility
rankings, NO participant PII, NO free-text mobility/assessment narrative, and NO
attachments - only boundary/readiness STATE metadata. Real mobility-score/flow
output/measurement value handling, mobility-catalog / flow-binding-intake /
corridor-scope content persistence, corridor-scope execution, visibility-control
execution, mobility-review adjudication, inflow/outflow-quantity/rate or per-individual
mobility-data or roster persistence, free-text/narrative/raw payload persistence, model
output, automated decision behavior, producer/reviewer/consumer mobility UX,
notification/document integration, and sensitive/PII-heavy persistence remain
closed.

## 2. Ownership and Boundaries

Owned by this pack:

- TEP-native sector mobility intelligence governance boundary.
- Sequencing after completed HCM foundation modules, completed TEP foundation and
  candidate readiness modules through `CAND-CAP-0021`, the completed
  `CAND-CAP-0042` Hiring Risk Indicators and `CAND-CAP-0043` Early Warning
  Signals context dependencies, the completed `CAND-CAP-0044` Restricted
  Integrity Registry context dependency, the completed `CAND-CAP-0045`
  Professional Reputation Ledger context dependency, the completed
  `CAND-CAP-0046` Industry Talent Pool context dependency, the completed
  `CAND-CAP-0048` Candidate Career Passport context dependency, the completed
  `CAND-CAP-0049` Talent Development Network context dependency, the completed
  `CAND-CAP-0050` Industry Succession Pool context dependency, the completed
  `CAND-CAP-0051` Verified Certification Registry context dependency, the
  completed `CAND-CAP-0052` Mentorship & Recommendation Network context
  dependency, the completed `CAND-CAP-0053` Shared Salary Benchmarking context
  dependency, the completed `CAND-CAP-0056` Talent Supply & Demand Forecasting
  context dependency, the completed `CAND-CAP-0057` Skills Gap Heatmap context
  dependency, the completed `CAND-CAP-0055` Sector Talent Trends
  sector-trend-source dependency, the completed `CAND-CAP-0054` Workforce
  Analytics data-source dependency, and the completed `CAND-CAP-0047` Industry
  Skill Passport skills-taxonomy-source dependency.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only sector mobility intelligence readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the mobility catalog, flow binding
  intake, corridor scope, visibility control, and mobility review.
- Explicit exclusion of real mobility-score/flow output/measurement value handling and
  mobility-catalog / flow-binding-intake / corridor-scope content
  persistence.
- Explicit exclusion of mobility-catalog execution, flow-binding-intake
  execution, corridor-scope execution, visibility-control execution,
  mobility-review execution, mobility-score/flow-output/query result values, model
  output, and automated decision behavior.
- Explicit exclusion of producer-facing, reviewer-facing, and consumer-facing
  mobility UX beyond the readiness-metadata CRUD (those fields are reserved
  and out of scope).
- Explicit exclusion of real mobility-score/flow output/measurement values,
  corridor/transition cell values, aggregated distributions, inflow/outflow-quantity/rate or
  per-individual mobility data, mobility-score/flow-output/query result values,
  employee/company rosters, participant lists, mobility-catalog record content,
  flow-binding-intake content, mobility narrative/assessment content,
  participant contact/profile content, mobility scores, participant PII,
  individual/per-role mobility rankings, free-text, mobility narrative, attachment
  payload, raw provider payload, credential/token/secret/password, and any PII
  dataset persistence.

Not owned by this pack:

- Real mobility-score/flow output/measurement value handling, aggregation, or ranking
  execution.
- Employee/company roster, participant-list, or inflow/outflow-quantity/rate/per-individual
  mobility-data content storage.
- Mobility-catalog execution, catalog runs, or catalog collection output.
- Flow-binding-intake execution, intake enforcement, or intake output.
- Corridor-scope execution, corridor enforcement, or corridor output.
- Visibility-control execution, visibility delivery, visibility decisions, or
  visibility action logs.
- Mobility-review execution, adjudication output, mobility-score/flow-output/query
  result values, or automated mobility decisions.
- Skills-taxonomy ownership beyond dependency/context references (owned by
  `CAND-CAP-0047` Industry Skill Passport).
- Workforce-analytics ownership beyond dependency/context references (owned by
  `CAND-CAP-0054` Workforce Analytics).
- Sector-trend ownership beyond dependency/context references (owned by
  `CAND-CAP-0055` Sector Talent Trends).
- Consent-policy, data-governance-policy, and visibility-policy ownership beyond
  dependency/context references.
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
| SectorMobilityIntelligenceCapabilityBoundary | Governance boundary | Defines what a future TEP sector mobility intelligence module may own. |
| SectorMobilityIntelligenceReadinessBoundary | Governance boundary | Separates readiness metadata from sector mobility intelligence execution. |
| MobilityCatalogBoundary | Governance boundary | Blocks mobility-catalog record / real mobility-value / distribution persistence. |
| FlowBindingIntakeBoundary | Governance boundary | Blocks real flow-binding-intake execution and per-binding mobility-data content persistence. |
| CorridorScopeBoundary | Governance boundary | Blocks corridor-scope execution, mobility-score/flow-output/query result values, and raw corridor payload persistence. |
| VisibilityControlBoundary | Governance boundary | Blocks visibility-control execution, visibility decisions, and visibility action logs. |
| MobilityReviewBoundary | Governance boundary | Blocks mobility review execution, adjudication, and review output. |
| SectorMobilityIntelligenceDecisionBoundary | Governance boundary | Blocks mobility-score/flow-output/query-result-value, model-output, distribution, and automated decision persistence. |
| SectorMobilityIntelligenceUxBoundary | Governance boundary | Blocks producer, reviewer, and consumer mobility UX beyond readiness-metadata CRUD. |
| SectorMobilityIntelligenceSensitiveDataBoundary | Governance boundary | Blocks real mobility-score/flow output/measurement values, corridor/transition cell values/aggregated distributions, inflow/outflow-quantity/rate or per-individual mobility data, mobility-score/flow-output/query result values, employee/company rosters, participant lists, mobility narrative content, participant profile content, PII, individual/per-role mobility rankings, free-text, and raw payload persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| MobilityCatalogEngine | Deferred runtime | Real mobility-catalog intake, catalog management, and mobility-value / distribution capture; not authorized. |
| FlowBindingIntakeEngine | Deferred runtime | Flow-binding-intake execution, per-binding mobility-data capture, and intake output; not authorized. |
| CorridorScopeEngine | Deferred runtime | Corridor-scope execution, mobility-score/flow-output/query result-value computation, and corridor output; not authorized. |
| VisibilityControlEngine | Deferred runtime | Visibility-control execution, visibility decisions, and visibility action logs; not authorized. |
| MobilityReviewEngine | Deferred runtime | Mobility review execution, adjudication, and automated mobility output; not authorized. |
| SectorMobilityIntelligenceExperience | Deferred frontend/runtime | Producer/reviewer/consumer mobility UX beyond readiness CRUD; not authorized. |
| SectorMobilityIntelligenceDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| SectorMobilityIntelligenceNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| SectorMobilityIntelligenceReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/SectorMobilityIntelligenceReadinessMetadata.cs`. |
| SectorMobilityIntelligenceReadinessState | Domain enum | Readiness state; `Domain/Enums/SectorMobilityIntelligenceReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| ISectorMobilityIntelligenceReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/ISectorMobilityIntelligenceReadinessMetadataRepository.cs`. |
| MongoSectorMobilityIntelligenceReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `tep_sector_mobility_intelligence_readiness`; indexes `ux_tep_sector_mobility_intelligence_tenant_code_active` and `ix_tep_sector_mobility_intelligence_tenant_state`. |
| SectorMobilityIntelligence Application features | Application (CQRS/MediatR) | `Application/Features/SectorMobilityIntelligence/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| SectorMobilityIntelligenceController | API controller | Thin controller; `Api/Controllers/Tep/SectorMobilityIntelligenceController.cs`. |
| SectorMobilityIntelligence golden-compact CRUD | Frontend | Controller + `Views/TalentEcosystem/SectorMobilityIntelligence/**` + `wwwroot/assets/js/TalentEcosystem/SectorMobilityIntelligence/**` + Resources + TEP nav entry under `frontend/Diten.Web/**` (frontend route `/TalentEcosystem/SectorMobilityIntelligence`). |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `SectorMobilityIntelligenceReadinessState`):

- `Code`
- `DisplayName`
- `SectorMobilityIntelligenceReadinessState`
- `MobilityCatalogBoundaryState`
- `FlowBindingIntakeBoundaryState`
- `CorridorScopeBoundaryState`
- `VisibilityControlBoundaryState`
- `MobilityReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `SectorTrendSourceDependencyState`
- `WorkforceAnalyticsSourceDependencyState`
- `SkillsTaxonomySourceDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `PublicationPolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `SectorMobilityIntelligenceReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Marker-safe field naming discipline:

- The runtime C# type, namespace, and property identity for this capability IS
  `SectorMobilityIntelligence` (type == slug aligned). Unlike `CAND-CAP-0053`,
  this capability carries NO field-name-test fragment to avoid: the visible/public
  term "Sector Mobility Intelligence" contains no fragment that the
  field-name contract test rejects, so the readiness scope is named
  `SectorMobilityIntelligenceReadinessState` /
  `SectorMobilityIntelligenceReadinessVersion` directly, with no alias prefix.
  The boundary sub-state property names are `MobilityCatalogBoundaryState`,
  `FlowBindingIntakeBoundaryState`, `CorridorScopeBoundaryState`,
  `VisibilityControlBoundaryState`, and `MobilityReviewBoundaryState` - never
  `MobilityValue`, `MobilityResult`, `HeadcountFigure`, or `DistributionRoster`.
  This keeps the entity contract marker-safe under a WORD/TOKEN boundary (`\b`
  regex) forbidden-marker guard: legitimate domain words (`sector`, `mobility`,
  `flow`, `binding`, `corridor`, `catalog`, `visibility`, `taxonomy`,
  `review`) are not falsely rejected, while true forbidden markers
  (`measurement`, `value`, `result`, `figure`, `distribution`, `roster`,
  `headcount`, `PII`, `payload`, `score`, `ranking`) are still caught. The public
  slug, route, permission namespace, and Mongo collection use the string constant
  `sector-mobility-intelligence` (and
  `tep_sector_mobility_intelligence_readiness`), and the visible l10n title is
  "Sector Mobility Intelligence" / "Sektörel Hareketlilik Zekâsı"; these are
  string literals, not property names, so they lie outside the field-name contract
  test. Although no marker-safe rename was required here, the same word-boundary
  guard discipline that governs the Credential->Attestation, SecretsVault->Vault,
  and SalaryBenchmarking->PayBenchmarking precedents still applies to keep true
  forbidden markers out of the field-name contract. Note the upstream sector
  trend source is bound through `SectorTrendSourceDependencyState` and the upstream
  skills taxonomy source through `SkillsTaxonomySourceDependencyState`, which occupy
  the dependency-state slots used by the sibling Skills Gap Heatmap slice; data
  minimization/governance is represented here as the `DataMinimizationState`
  governance/precondition field.

Reserved / out-of-scope fields (must not be added in this slice):

- Producer-facing mobility fields (producer binding submission, producer review,
  producer action UX state) are reserved and out of scope.
- Reviewer-facing mobility fields (reviewer response, reviewer acknowledgement,
  reviewer self-service UX state) are reserved and out of scope.
- Consumer-facing mobility fields (consumer mobility view, consumer decision,
  consumer self-service UX state) are reserved and out of scope.
- No real mobility-score/flow output/measurement value, corridor/transition cell value,
  aggregated distribution, or mobility-score/flow-output/query result field of
  any kind. This is a metadata-only readiness slice with no
  mobility-score/flow-output/measurement attribute.
- No inflow/outflow-quantity/rate or per-individual mobility data, employee/company roster,
  participant list, mobility-catalog record content, flow-binding-intake content,
  mobility score, participant PII, individual/per-role mobility ranking, free-text, mobility
  narrative, or attachment field of any kind.

Forbidden field classes:

- Mobility-catalog record body, flow-binding-intake payload, corridor-scope
  payload, participant profile content, visibility-control execution payload,
  mobility-review payload, or completed mobility-score/flow-output/measurement-value
  content.
- Real mobility-score value, flow output value, measurement value, corridor/transition cell
  value, distribution, mobility-score/flow-output/query result value, mobility-score
  result, mobility ranking value, ordering result, rank, match confidence, model
  output, flow output, or evaluator scoring payload.
- Inflow/outflow-quantity/rate or per-individual mobility data, employee/company roster /
  participant list content, participant PII, individual/per-role mobility ranking,
  participant profile content, mobility-catalog record content, mobility narrative,
  assessment content, visibility content, cover letter, free-text profile
  narrative, HR notes, reviewer notes, or sensitive individual body.
- Attachment payload, controlled document body, external document content, raw
  intake message, raw provider payload, or uploaded file metadata.
- Salary amount, compensation amount, benefits election, payroll details, tax
  details, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  any PII-heavy participant/individual field.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0058-sector-mobility-intelligence.md`

Candidate runtime service:

- `Diten.TalentEcosystemService` (port `5060`)

Runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/TalentEcosystem/SectorMobilityIntelligence/**`,
  `wwwroot/assets/js/TalentEcosystem/SectorMobilityIntelligence/**`, Resources, TEP nav
  entry; frontend route `/TalentEcosystem/SectorMobilityIntelligence`).
- Gateway route exposure through the API gateway (`5080` -> TEP `5060`,
  `/api/sector-mobility-intelligence`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.TalentEcosystemService/**` plus its golden-compact
readiness CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned SectorMobilityIntelligence CRUD objects
  listed in §3/§5.
- `gateway/**` outside the owned SectorMobilityIntelligence readiness route.
- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `services/Diten.PssService/**`
- global `tests/**`
- completed TEP foundation and candidate readiness packs (including the
  `CAND-CAP-0047` Industry Skill Passport skills-taxonomy-source dependency,
  `CAND-CAP-0041` Talent Data Foundation, `CAND-CAP-0042`
  Hiring Risk Indicators, `CAND-CAP-0043` Early Warning Signals, `CAND-CAP-0044`
  Restricted Integrity Registry, `CAND-CAP-0045` Professional Reputation
  Ledger, `CAND-CAP-0046` Industry Talent Pool, `CAND-CAP-0048` Candidate Career
  Passport, `CAND-CAP-0049` Talent Development Network, `CAND-CAP-0050` Industry
  Succession Pool, `CAND-CAP-0051` Verified Certification Registry, `CAND-CAP-0052`
  Mentorship & Recommendation Network, `CAND-CAP-0053` Shared Salary
  Benchmarking, `CAND-CAP-0056` Talent Supply & Demand Forecasting, `CAND-CAP-0057`
  Skills Gap Heatmap, the `CAND-CAP-0055` Sector Talent Trends
  sector-trend-source dependency, the `CAND-CAP-0054`
  Workforce Analytics data-source dependency, and the `CAND-CAP-0047` Industry
  Skill Passport skills-taxonomy-source dependency).
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Upstream source dependencies:

- `CAND-CAP-0055` - Sector Talent Trends (`MOD-0346`), done. Consumed as the
  sector trend source for the sector mobility intelligence capability via
  `SectorTrendSourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as a raw roster, trends dataset, or data source.
- `CAND-CAP-0054` - Workforce Analytics (`MOD-0345`), done. Consumed as the
  workforce analytics source for the sector mobility intelligence capability
  via `WorkforceAnalyticsSourceDependencyState`; its output is treated as
  boundary/readiness metadata, not as a raw metric/measurement value, corridor/transition
  cell value/distribution, or data source.
- `CAND-CAP-0047` - Industry Skill Passport (`MOD-0338`), done. Consumed
  as the skills taxonomy source for the sector mobility intelligence capability via
  `SkillsTaxonomySourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as a raw skills/metric value, corridor/transition cell value/distribution, or
  data source.

Governance/control dependencies:

- Consent policy, consumed via `ConsentPreconditionState`. Because this is a
  consent-, visibility-, and publication-governed capability, a valid consent
  basis is a precondition and is treated as boundary/readiness metadata only.
- Data governance / minimization, consumed via `DataMinimizationState`. Data
  minimization, anonymization, and aggregation governance are controlling
  preconditions and are treated as boundary/readiness metadata only.
- Visibility and publication policy, consumed via `VisibilityControlBoundaryState`
  and `PublicationPolicyState`. Visibility/access, corridor scope, and
  publication policy are controlling preconditions and are treated as
  boundary/readiness metadata only.
- Notification, consumed via `NotificationDependencyState`. Notification/reminder
  delivery is deferred/out of scope and is treated as boundary/readiness metadata
  only.

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
- `CAND-CAP-0043` - Early Warning Signals (`MOD-0333`), done. Sibling/context.
- `CAND-CAP-0044` - Restricted Integrity Registry (`MOD-0334`), done. Sibling/context.
- `CAND-CAP-0045` - Professional Reputation Ledger (`MOD-0335`), done. Sibling/context.
- `CAND-CAP-0046` - Industry Talent Pool (`MOD-0337`), done. Sibling/context.
- `CAND-CAP-0047` - Industry Skill Passport (`MOD-0338`), done. Upstream skills
  taxonomy source dependency.
- `CAND-CAP-0048` - Candidate Career Passport (`MOD-0339`), done. Sibling/context.
- `CAND-CAP-0049` - Talent Development Network (`MOD-0340`), done. Sibling/context.
- `CAND-CAP-0050` - Industry Succession Pool (`MOD-0341`), done. Sibling/context.
- `CAND-CAP-0051` - Verified Certification Registry (`MOD-0342`), done. Sibling/context.
- `CAND-CAP-0052` - Mentorship & Recommendation Network (`MOD-0343`), done. Sibling/context.
- `CAND-CAP-0053` - Shared Salary Benchmarking (`MOD-0344`), done. Sibling/context.
- `CAND-CAP-0054` - Workforce Analytics (`MOD-0345`), done. Upstream workforce
  analytics source dependency.
- `CAND-CAP-0055` - Sector Talent Trends (`MOD-0346`), done. Upstream sector
  trend source dependency.
- `CAND-CAP-0056` - Talent Supply & Demand Forecasting (`MOD-0347`), done. Sibling/context.
- `CAND-CAP-0057` - Skills Gap Heatmap (`MOD-0348`), done. Sibling/context.

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

- Runtime owner/key: `tep.sector-mobility-intelligence`.
- Permission namespace:
  - `tep.sector-mobility-intelligence.read`
  - `tep.sector-mobility-intelligence.manage`
  - `tep.sector-mobility-intelligence.evaluate`
  - `tep.sector-mobility-intelligence.audit.read`
- Runtime repo scope: `services/Diten.TalentEcosystemService/**`.
- Thin controller (`Api/Controllers/Tep/SectorMobilityIntelligenceController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `tep_sector_mobility_intelligence_readiness`.
- Active tenant `Code` uniqueness through
  `ux_tep_sector_mobility_intelligence_tenant_code_active`, with
  `ix_tep_sector_mobility_intelligence_tenant_state` supporting tenant/state queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`sector`, `mobility`,
  `flow`, `binding`, `corridor`, `catalog`, `visibility`, `taxonomy`)
  are not falsely rejected while true forbidden markers (measurement, value,
  result, figure, distribution, roster, headcount, participant, mobility score,
  ranking, PII, payload) are still caught. The word-boundary discipline is
  deliberate and pairs with the type == slug aligned property identity: the C#
  property names use the `SectorMobilityIntelligence` prefix directly (no
  field-name-test fragment to avoid), so the field-name contract test passes, and
  the compound `SectorMobilityIntelligence` and the domain word `mobility` pass
  the guard while the standalone forbidden tokens `measurement`, `value`, and
  `result` are still rejected.

Explicitly unauthorized:

- Real mobility-score/flow output/measurement value handling, aggregation, or ranking
  execution.
- Mobility-catalog record / flow-binding-intake / corridor-scope record
  persistence.
- Flow-binding-intake execution, enforcement, or per-binding mobility-data
  payload persistence.
- Corridor-scope execution, mobility-score/flow-output/query result-value computation, or
  corridor output.
- Visibility-control execution, visibility delivery, visibility decisions, or
  visibility action logs.
- Mobility-review execution, adjudication, or automated mobility output.
- Mobility-score/flow-output/query-result-value/finding/model-output persistence.
- Automated decision and mobility-scoring/ranking behavior.
- Producer/reviewer/consumer mobility UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Real mobility-score/flow output/measurement values, corridor/transition cell values/aggregated
  distributions, inflow/outflow-quantity/rate or per-individual mobility data,
  mobility-score/flow-output/query result values, employee/company rosters,
  participant lists, mobility-catalog content, mobility narrative, participant
  profile content, mobility scores, participant PII, individual/per-role mobility rankings,
  free-text, attachment payload, or raw payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- TEP tenant shell nav entry `Sector Mobility Intelligence` (frontend route
  `/TalentEcosystem/SectorMobilityIntelligence`).
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources
  ("Sector Mobility Intelligence" / "Sektörel Hareketlilik Zekâsı").

Unauthorized UX:

- Producer mobility UX beyond readiness-metadata CRUD.
- Reviewer mobility/self-service UX beyond readiness-metadata CRUD.
- Consumer mobility/self-service UX beyond readiness-metadata CRUD.
- Mobility catalog, flow binding intake, corridor scope, visibility
  control, mobility review, mobility-score/flow-output/query-result-value, or
  model-output UI.
- Document, notification, export, upload, or attachment UI.
- Any real mobility-score/flow output/measurement value, corridor/transition cell value/aggregated
  distribution, inflow/outflow-quantity/rate or per-individual mobility data, employee/company
  roster, participant list, mobility-catalog content, mobility score, participant
  PII, individual/per-role mobility ranking, free-text, mobility narrative, assessment
  content, or raw payload viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> TEP `5060`).

Authorized API surface:

- `GET /api/sector-mobility-intelligence` - list readiness metadata.
- `GET /api/sector-mobility-intelligence/{id}` - get readiness metadata by id.
- `POST /api/sector-mobility-intelligence` - create readiness metadata.
- `POST /api/sector-mobility-intelligence/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/sector-mobility-intelligence/{id}` - soft-delete.
- `GET /api/sector-mobility-intelligence/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get sector mobility intelligence readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No mobility-score/flow-output/measurement value handling, mobility-catalog record /
  flow-binding-intake / corridor-scope record persistence,
  flow-binding-intake execution, corridor-scope execution,
  visibility-control execution, mobility-review execution,
  mobility-score/flow-output/query-result-value or model-output persistence, mobility-scoring or
  automated decision behavior, notification/document integration, or
  mobility-value/roster/participant-list/mobility-score/PII/individual-ranking/
  free-text/narrative/raw-payload persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only sector mobility intelligence readiness state.
- Boundary-state metadata for mobility catalog, flow binding intake,
  corridor scope, visibility control, mobility review, automated decision,
  consent, minimization, retention, and publication policy.
- Dependency-state metadata for sector-trend source, workforce-analytics source,
  skills-taxonomy source, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Flow-binding-intake/mobility-catalog payload.
- Mobility-catalog record body, flow-binding-intake content, or
  corridor-scope content.
- Flow-binding-intake execution/enforcement payload.
- Corridor-scope execution payload, mobility-score/flow-output/query result value, or corridor
  output.
- Visibility-control execution payload, visibility delivery, visibility
  decisions, or visibility action logs.
- Mobility-review/adjudication payload.
- Mobility-score/flow-output/query result value / mobility-ranking value / ordering result / rank /
  match confidence / model output.
- Real mobility-score/flow output/measurement value, corridor/transition cell value, aggregated
  distribution, inflow/outflow-quantity/rate or per-individual mobility data, employee/company roster / participant list
  content, participant PII, individual/per-role mobility ranking, participant profile content,
  mobility-catalog content, mobility narrative, visibility content, or
  free-text.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy participant/individual details.
- Compensation/payroll/benefits payload.

Consent-sensitivity note: because this is a consent-, visibility-, and
publication-governed, privacy-sensitive sector mobility intelligence
capability, no real mobility-score/flow output/measurement value, corridor/transition cell value,
aggregated distribution, inflow/outflow-quantity/rate or per-individual mobility data,
employee/company roster, participant list, mobility-catalog content,
mobility-score/flow-output/query result value, or individual/per-role mobility ranking of any
kind may cross the boundary into persistence. Only boundary/readiness STATE
metadata governed by a valid consent basis, data governance policy, visibility
scope, and publication policy is permitted.

## 12. Permission Boundary

Approved permission namespace:

- `tep.sector-mobility-intelligence.read`
- `tep.sector-mobility-intelligence.manage`
- `tep.sector-mobility-intelligence.evaluate`
- `tep.sector-mobility-intelligence.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Runtime work must be tenant-aware and fail closed. The backend/API slice must
resolve tenant server-side and must not expose `TenantId` in request DTOs.

Security guardrails:

- Candidate identity (`CAND-CAP-0058`) must not become a runtime literal.
- Blocked legacy ID (`MOD-0349`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_tep_sector_mobility_intelligence_tenant_code_active` and
  `ix_tep_sector_mobility_intelligence_tenant_state`.
- Because this is a consent-, visibility-, and publication-governed,
  privacy-sensitive capability, consent basis, data governance policy, visibility
  scope, and publication policy are treated as the highest-risk controls even for
  readiness metadata; access is restricted and audited.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation, with a valid consent basis
  treated as a precondition to activation.
- Sector-mobility-intelligence data minimization fail-closed behavior, with data
  governance policy treated as a controlling precondition.
- Retention/publication/audit/visibility/deletion as local/deferred metadata,
  with visibility scope and publication policy treated as controlling
  preconditions.
- Exclusion of real mobility-score/flow output/measurement values, corridor/transition cell values/aggregated
  distributions, inflow/outflow-quantity/rate or per-individual mobility data, mobility-score/flow-output/query result values,
  employee/company rosters, participant lists, mobility-catalog content, mobility
  narrative content, participant profile content, mobility scores, participant PII,
  individual/per-role mobility rankings, free-text, attachments, and any PII dataset.

The Sector Mobility Intelligence capability declares readiness STATE only; it does not own
or store any real mobility-score/flow output/measurement values, corridor/transition cell values/aggregated
distributions, inflow/outflow-quantity/rate or per-individual mobility data, mobility-score/flow-output/query result values,
employee/company rosters, participant lists, mobility-catalog content, mobility narrative
content, participant profile content, mobility scores, participant PII, individual/per-role
mobility rankings, or free-text, and all downstream consumers must treat its
output as boundary/readiness metadata, not as a mobility-value/flow-output or data
source. Because this is a consent-, visibility-, and publication-governed,
privacy-sensitive capability, its consent-basis, data governance, visibility,
publication, retention, and restricted-access constraints are treated as the
highest-risk controls: no real mobility-score/flow output/measurement value, corridor/transition
cell value/distribution, inflow/outflow-quantity/rate or per-individual mobility data, employee/company roster,
mobility-score/flow-output/query result value, or individual/per-role mobility ranking may ever be persisted
in this slice, and access to even the readiness metadata is restricted and audited.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, flow-binding-intake
connector, or document/notification UI is authorized. The `CAND-CAP-0055` Sector
Talent Trends capability is consumed only as an upstream source dependency-state
context; the `CAND-CAP-0054` Workforce Analytics capability is consumed only as an
upstream source dependency-state context; the `CAND-CAP-0047` Industry Skill
Passport capability is consumed only as an upstream source dependency-state
context; consent-policy and data-governance-policy are consumed only as governance
dependency-state context; HCM foundation (`CAND-CAP-0007` through `CAND-CAP-0010`)
is consumed only as prerequisite dependency-state context; and TEP `CAND-CAP-0011`
through `CAND-CAP-0021`, `CAND-CAP-0041`, `CAND-CAP-0042`, `CAND-CAP-0043`,
`CAND-CAP-0044`, `CAND-CAP-0045`, `CAND-CAP-0046`, `CAND-CAP-0048`,
`CAND-CAP-0049`, `CAND-CAP-0050`, `CAND-CAP-0051`, `CAND-CAP-0052`,
`CAND-CAP-0053`, `CAND-CAP-0056`, and `CAND-CAP-0057` are consumed only as sibling/context
references.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0058`.
3. AC-03: Legacy Excel ID `MOD-0349` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `tep.sector-mobility-intelligence`.
5. AC-05: Permission namespace is limited to
   `tep.sector-mobility-intelligence.read`,
   `tep.sector-mobility-intelligence.manage`,
   `tep.sector-mobility-intelligence.evaluate`, and
   `tep.sector-mobility-intelligence.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.TalentEcosystemService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection
   `tep_sector_mobility_intelligence_readiness`) includes active tenant `Code`
   uniqueness (`ux_tep_sector_mobility_intelligence_tenant_code_active`),
   tenant/state indexing (`ix_tep_sector_mobility_intelligence_tenant_state`),
   soft delete, and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Real mobility-score/flow output/measurement value handling, mobility-catalog record /
    flow-binding-intake / corridor-scope record persistence,
    flow-binding-intake execution, corridor-scope execution,
    visibility-control execution, mobility-review execution, model output,
    mobility-score/flow-output/query result values, inflow/outflow-quantity/rate or per-individual mobility-data or
    employee/company roster persistence, mobility-catalog / mobility-narrative content
    persistence, and automated decision behavior remain unauthorized.
14. AC-14: Producer/reviewer/consumer mobility fields remain reserved/out of
    scope; no real mobility-score/flow output/measurement value or result field is present; no
    mobility-value/corridor-cell/distribution/roster/participant-list/mobility-score/
    PII/individual-ranking/free-text/narrative field is present. Field names
    remain marker-safe: the runtime C# type/namespace/property identity uses the
    `SectorMobilityIntelligence` prefix (type == slug aligned, no field-name-test fragment
    to avoid) and the boundary names never contain the forbidden markers
    `measurement`/`value`/`result`/`figure`/`distribution`.
15. AC-15: Real mobility-score/flow output/measurement values, corridor/transition cell values/aggregated distributions,
    inflow/outflow-quantity/rate or per-individual mobility data, mobility-score/flow-output/query result values,
    employee/company rosters, participant lists, mobility-catalog content, mobility
    narrative, participant profile content, mobility scores, participant PII,
    individual/per-role mobility rankings, free-text, attachment payload, raw provider payload,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized. The forbidden-marker guard asserts a WORD/TOKEN boundary
    (`\b` regex) match, NOT a bare substring `Contains`, so legitimate domain
    words (`sector`, `mobility`, `flow`, `binding`, `corridor`, `taxonomy`) pass
    while `measurement`/`value`/`result`/`figure`/`distribution`/`roster` are
    caught; and the entity field names use the `SectorMobilityIntelligence` prefix
    directly so the field-name contract test passes.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: Upstream source, governance, TEP, HCM foundation, and PSS dependency
    boundaries are recorded (`CAND-CAP-0055` Sector Talent Trends as upstream
    sector trend source; `CAND-CAP-0054` Workforce Analytics as upstream workforce
    analytics source; `CAND-CAP-0047` Industry Skill Passport as upstream
    skills taxonomy source; consent-policy and data-governance-policy as governance
    dependencies; TEP `CAND-CAP-0011` through `CAND-CAP-0021`, `CAND-CAP-0041`,
    `CAND-CAP-0042`, `CAND-CAP-0043`, `CAND-CAP-0044`, `CAND-CAP-0045`,
    `CAND-CAP-0046`, `CAND-CAP-0048`, `CAND-CAP-0049`, `CAND-CAP-0050`,
    `CAND-CAP-0051`, `CAND-CAP-0052`, `CAND-CAP-0053`, `CAND-CAP-0056`, and `CAND-CAP-0057` as sibling/context;
    HCM `CAND-CAP-0007` through `CAND-CAP-0010` as prerequisite context).
18. AC-18: `tr`/`en` localization parity is present
    ("Sector Mobility Intelligence" / "Sektörel Hareketlilik Zekâsı").
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0058|MOD-0349` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build TEP API:
  `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `SectorMobilityIntelligence` filter
  (`Application.Tests/SectorMobilityIntelligenceTests.cs`, handler behavior;
  fix-absent should be RED).
- Run full TEP Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_tep_sector_mobility_intelligence_tenant_code_active`), and tenant/state
  indexing (`ix_tep_sector_mobility_intelligence_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`sector`, `mobility`,
  `flow`, `binding`, `corridor`, `taxonomy`, `visibility`) are not falsely
  rejected while the forbidden tokens `measurement`, `value`, `result`, `figure`,
  and `distribution` are still caught.
- Verify the entity field names use the `SectorMobilityIntelligence` prefix (type == slug
  aligned property identity, no field-name-test fragment to avoid) so the
  field-name contract test passes.
- Verify forbidden mobility-catalog/flow-binding-intake/corridor-scope/
  visibility/mobility-review/decision/sensitive fields are absent.
- Verify producer/reviewer/consumer mobility fields are reserved/absent, no
  real mobility-score/flow output/measurement value or result field exists, and no
  mobility-value/roster/participant-list/mobility-score/PII/individual-ranking/
  free-text/narrative field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0058|MOD-0349" services frontend gateway`
  returns no runtime literal (candidate IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real mobility-score/flow output/measurement value handling prohibition is accepted.
- [x] Mobility-catalog record / flow-binding-intake / corridor-scope record
  persistence prohibition is accepted.
- [x] Flow-binding-intake / corridor-scope / visibility-control / mobility
  review execution prohibition is accepted.
- [x] Mobility-score/flow-output/query-result-value and model-output prohibition is accepted.
- [x] Mobility-scoring/ranking and automated decision behavior prohibition is
  accepted.
- [x] Reserved producer/reviewer/consumer mobility fields,
  no-real-mobility-value/result-field, and
  no-mobility-value/roster/participant-list/mobility-score/PII/individual-ranking/
  free-text/narrative constraints are accepted.
- [x] Sensitive/PII, inflow/outflow-quantity/rate or per-individual mobility-data, employee/company roster
  content, and raw payload prohibition is accepted.
- [x] Marker-safe `SectorMobilityIntelligence` property identity (type == slug aligned,
  no field-name-test fragment to avoid) is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Sector-trend source, workforce-analytics source, skills-taxonomy source,
  consent-policy, data-governance-policy, visibility-policy, TEP/HCM/PSS dependency
  context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only sector mobility intelligence readiness contract.
- [x] Marker-safe runtime identity decision (`SectorMobilityIntelligence` C#
  type/namespace/property prefix, type == slug aligned; `sector-mobility-intelligence`
  public slug/route/permission/collection string constants).
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Sector-mobility-intelligence data minimization policy.
- [x] Retention/publication/audit/visibility/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.TalentEcosystemService/**`
  and the owned `frontend/Diten.Web/**` SectorMobilityIntelligence objects.
- Runtime owner/key remains limited to `tep.sector-mobility-intelligence`.
- Permission namespace remains limited to
  `tep.sector-mobility-intelligence.read`,
  `tep.sector-mobility-intelligence.manage`,
  `tep.sector-mobility-intelligence.evaluate`, and
  `tep.sector-mobility-intelligence.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract, and remain marker-safe: the runtime C#
  type/namespace/property identity uses the `SectorMobilityIntelligence` prefix (type ==
  slug aligned, no field-name-test fragment to avoid) and never contains the
  forbidden markers `measurement`/`value`/`result`/`figure`/`distribution`, while
  the public slug/route/permission/collection use the `sector-mobility-intelligence`
  string constant and the visible l10n title is "Sector Mobility Intelligence" /
  "Sektörel Hareketlilik Zekâsı". Although no marker-safe rename was required
  (unlike the SalaryBenchmarking->PayBenchmarking precedent), the same
  word-boundary guard discipline as the Credential->Attestation and
  SecretsVault->Vault precedents is enforced.
- `SectorTrendSourceDependencyState`, `WorkforceAnalyticsSourceDependencyState`,
  `SkillsTaxonomySourceDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states; consent is represented as the
  `ConsentPreconditionState` governance/precondition field and data minimization
  as the `DataMinimizationState` governance/precondition field.
- Producer/reviewer/consumer mobility UX fields remain reserved/out of
  scope, no real mobility-score/flow output/measurement value or result field is present, and no
  mobility-value/roster/participant-list/mobility-score/PII/individual-ranking/
  free-text/narrative field is present.
- HCM, PSS, and Platform scopes remain closed.
- Real mobility-score/flow output/measurement value handling, mobility-catalog record /
  flow-binding-intake / corridor-scope record persistence,
  flow-binding-intake execution, corridor-scope execution,
  visibility-control execution, mobility-review execution, model output,
  mobility-score/flow-output/query result values, automated decision behavior,
  notification/document integration, real mobility-score/flow output/measurement values, corridor/transition
  cell values/distributions, inflow/outflow-quantity/rate or per-individual mobility data, employee/company
  rosters, participant lists, mobility-catalog content, mobility narrative, participant
  profile content, mobility scores, participant PII, individual/per-role mobility
  rankings, free-text, attachment payload, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence remain out of
  scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0058 --name "Sector Mobility Intelligence"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0058`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Marker-safe identity waiver approved: the runtime C# type/namespace/property
  identity uses the `SectorMobilityIntelligence` prefix (type == slug aligned, no
  field-name-test fragment to avoid), so the field-name contract test passes,
  while the public slug/route/permission/collection use the `sector-mobility-intelligence`
  string constant and the l10n title stays "Sector Mobility Intelligence" / "Sektörel
  Hareketlilik Zekâsı". The word-boundary guard discipline mirrors the
  Credential->Attestation and SecretsVault->Vault precedents even though no
  rename was required.
- Legal/privacy/consent metadata-only waiver approved.
- Sector-mobility-intelligence data minimization fail-closed policy approved.
- Retention/publication/audit/visibility/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0058|MOD-0349" services frontend gateway`
- Candidate IDs are governance-only and must return no runtime literal match.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real mobility-catalog intake and catalog management.
- Flow-binding-intake execution and enforcement.
- Corridor-scope execution and mobility-score/flow-output/query result-value computation.
- Visibility-control execution and visibility decisions.
- Mobility review and automated mobility adjudication.
- Automated decision approval.
- Producer/reviewer/consumer mobility UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
