---
id: CAND-CAP-0056
name: Talent Supply & Demand Forecasting
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0056-talent-supply-demand-forecasting
started: 2026-09-08
target: 2026-12-08
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0056
legacy_excel_id: MOD-0347
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.TalentEcosystemService
runtime_service_port: 5060
gateway_port: 5080
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.talent-supply-demand-forecasting
permission_namespace:
  - tep.talent-supply-demand-forecasting.read
  - tep.talent-supply-demand-forecasting.manage
  - tep.talent-supply-demand-forecasting.evaluate
  - tep.talent-supply-demand-forecasting.audit.read
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0056 - Talent Supply & Demand Forecasting

> Status: draft. This pack records the first metadata-only backend/API talent
> supply & demand forecasting readiness contract slice under
> `services/Diten.TalentEcosystemService/**`, plus a golden-compact readiness
> CRUD frontend under `frontend/Diten.Web/**` and gateway exposure through the
> API gateway (`5080` -> TEP `5060`). This is a consent-, visibility-, and
> publication-governed, privacy sensitive capability: real forecast/model
> output/measurement value handling, forecast-catalog / model-binding-intake /
> horizon-scope content persistence, horizon-scope execution, visibility-control
> execution, forecast-review adjudication, supply/demand or per-individual
> forecast-data persistence, forecast/model-output/query result-value persistence,
> employee/company roster or participant-list persistence, supply/demand figure or
> projected/aggregated distribution persistence, participant/individual PII or
> contact persistence, free-text forecast/assessment narrative persistence, model
> output or automated decision behavior, and sensitive/PII-heavy persistence
> remain closed. `CAND-CAP-0056` remains a governance/documentation identity only
> and must not be written into runtime literals. `MOD-0347` remains blocked for
> TEP use until future EA canonical MOD assignment.
>
> MARKER-SAFE IDENTITY NOTE (see §4 and §19): unlike `CAND-CAP-0053` (which needed
> the `PayBenchmarking` marker-safe alias because the field-name contract test
> forbids property names containing "Salary"/"Amount"/"Wage"), the runtime C#
> type, namespace, and property identity for this capability IS
> `TalentSupplyDemandForecasting` - type == slug aligned - and carries no
> field-name-test fragment to avoid. The public slug/route/permission/collection
> use the string constant `talent-supply-demand-forecasting` (and
> `tep_talent_supply_demand_forecasting_readiness`), and the visible l10n title is
> "Talent Supply & Demand Forecasting" / "Yetenek Arz ve Talep Tahmini".
> The marker-safe field-naming discipline is still documented and enforced: the
> forbidden-marker guard uses a WORD/TOKEN boundary (`\b` regex) matcher, NOT a
> bare substring `Contains`, so legitimate domain words (`talent`, `supply`,
> `demand`, `forecast`, `model`, `binding`, `horizon`, `catalog`, `visibility`)
> pass while true forbidden markers (`measurement`, `value`, `result`, `figure`,
> `distribution`, `roster`, `headcount`, `PII`, `payload`, `score`, `ranking`)
> are still caught.

## 1. Module Summary

`CAND-CAP-0056` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R4 capability named `Talent Supply & Demand Forecasting`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0347`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0056` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API talent supply & demand
forecasting readiness contract slice, mirroring the completed TEP candidate
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
completed `CAND-CAP-0054` / `MOD-0345` Workforce Analytics slice, and the
completed `CAND-CAP-0055` / `MOD-0346` Sector Talent Trends slice exactly.
The Talent Supply & Demand Forecasting capability is the TEP-native readiness layer
that declares the readiness of a consent-, visibility-, and publication-governed
talent supply & demand forecasting capability: it declares the readiness STATE of
the forecast catalog, model binding intake, horizon scope, visibility control, and
forecast review. It depends on the Talent Data Foundation capability
(`CAND-CAP-0041` / `MOD-0336`) as its upstream talent data source, on the
Workforce Analytics capability (`CAND-CAP-0054` / `MOD-0345`) as its upstream
workforce analytics source, and on the Sector Talent Trends capability
(`CAND-CAP-0055` / `MOD-0346`) as its upstream sector trend source. Because this
is a consent-, visibility-, and publication-governed, privacy sensitive
capability, its sensitivity posture is paramount: it stores NO real
forecast/model output/measurement values or results, NO supply/demand figures or
projected/aggregated distributions, NO supply/demand or per-individual forecast
data, NO forecast/model-output/query result values, NO employee/company rosters,
NO participant lists, NO forecast-catalog record content, NO model-binding-intake
content, NO forecast narrative/assessment content, NO participant contact/profile
content, NO forecast scores, NO individual forecast rankings, NO participant PII,
NO free-text forecast/assessment narrative, and NO attachments - only
boundary/readiness STATE metadata. Real forecast/model output/measurement value
handling, forecast-catalog / model-binding-intake / horizon-scope content
persistence, horizon-scope execution, visibility-control execution, forecast-review
adjudication, supply/demand or per-individual forecast-data or roster persistence,
free-text/narrative/raw payload persistence, model output, automated decision
behavior, producer/reviewer/consumer forecast UX, notification/document
integration, and sensitive/PII-heavy persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- TEP-native talent supply & demand forecasting governance boundary.
- Sequencing after completed HCM foundation modules, completed TEP foundation and
  candidate readiness modules through `CAND-CAP-0021`, the completed
  `CAND-CAP-0042` Hiring Risk Indicators and `CAND-CAP-0043` Early Warning
  Signals context dependencies, the completed `CAND-CAP-0044` Restricted
  Integrity Registry context dependency, the completed `CAND-CAP-0045`
  Professional Reputation Ledger context dependency, the completed
  `CAND-CAP-0046` Industry Talent Pool context dependency, the completed
  `CAND-CAP-0047` Industry Skill Passport context dependency, the completed
  `CAND-CAP-0048` Candidate Career Passport context dependency, the completed
  `CAND-CAP-0049` Talent Development Network context dependency, the completed
  `CAND-CAP-0050` Industry Succession Pool context dependency, the completed
  `CAND-CAP-0051` Verified Certification Registry context dependency, the
  completed `CAND-CAP-0052` Mentorship & Recommendation Network context
  dependency, the completed `CAND-CAP-0053` Shared Salary Benchmarking context
  dependency, the completed `CAND-CAP-0041` Talent Data Foundation data-source
  dependency, the completed `CAND-CAP-0054` Workforce Analytics data-source
  dependency, and the completed `CAND-CAP-0055` Sector Talent Trends
  sector-trend-source dependency.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only talent supply & demand forecasting readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the forecast catalog, model binding
  intake, horizon scope, visibility control, and forecast review.
- Explicit exclusion of real forecast/model output/measurement value handling and
  forecast-catalog / model-binding-intake / horizon-scope content
  persistence.
- Explicit exclusion of forecast-catalog execution, model-binding-intake
  execution, horizon-scope execution, visibility-control execution,
  forecast-review execution, forecast/model-output/query result values, model
  output, and automated decision behavior.
- Explicit exclusion of producer-facing, reviewer-facing, and consumer-facing
  forecast UX beyond the readiness-metadata CRUD (those fields are reserved
  and out of scope).
- Explicit exclusion of real forecast/model output/measurement values,
  supply/demand figures, projected/aggregated distributions, supply/demand or
  per-individual forecast data, forecast/model-output/query result values,
  employee/company rosters, participant lists, forecast-catalog record content,
  model-binding-intake content, forecast narrative/assessment content,
  participant contact/profile content, forecast scores, participant PII,
  individual forecast rankings, free-text, forecast narrative, attachment
  payload, raw provider payload, credential/token/secret/password, and any PII
  dataset persistence.

Not owned by this pack:

- Real forecast/model output/measurement value handling, aggregation, or ranking
  execution.
- Employee/company roster, participant-list, or supply/demand/per-individual
  forecast-data content storage.
- Forecast-catalog execution, catalog runs, or catalog collection output.
- Model-binding-intake execution, intake enforcement, or intake output.
- Horizon-scope execution, horizon enforcement, or horizon output.
- Visibility-control execution, visibility delivery, visibility decisions, or
  visibility action logs.
- Forecast-review execution, adjudication output, forecast/model-output/query
  result values, or automated forecast decisions.
- Talent-data ownership beyond dependency/context references (owned by
  `CAND-CAP-0041` Talent Data Foundation).
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
| TalentSupplyDemandForecastingCapabilityBoundary | Governance boundary | Defines what a future TEP talent supply & demand forecasting module may own. |
| TalentSupplyDemandForecastingReadinessBoundary | Governance boundary | Separates readiness metadata from talent supply & demand forecasting execution. |
| ForecastCatalogBoundary | Governance boundary | Blocks forecast-catalog record / real forecast-value / distribution persistence. |
| ModelBindingIntakeBoundary | Governance boundary | Blocks real model-binding-intake execution and per-model forecast-data content persistence. |
| HorizonScopeBoundary | Governance boundary | Blocks horizon-scope execution, forecast/model-output/query result values, and raw horizon payload persistence. |
| VisibilityControlBoundary | Governance boundary | Blocks visibility-control execution, visibility decisions, and visibility action logs. |
| ForecastReviewBoundary | Governance boundary | Blocks forecast review execution, adjudication, and review output. |
| TalentSupplyDemandForecastingDecisionBoundary | Governance boundary | Blocks forecast/model-output/query-result-value, model-output, distribution, and automated decision persistence. |
| TalentSupplyDemandForecastingUxBoundary | Governance boundary | Blocks producer, reviewer, and consumer forecast UX beyond readiness-metadata CRUD. |
| TalentSupplyDemandForecastingSensitiveDataBoundary | Governance boundary | Blocks real forecast/model output/measurement values, supply/demand figures/projected distributions, supply/demand or per-individual forecast data, forecast/model-output/query result values, employee/company rosters, participant lists, forecast narrative content, participant profile content, PII, individual forecast rankings, free-text, and raw payload persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| ForecastCatalogEngine | Deferred runtime | Real forecast-catalog intake, catalog management, and forecast-value / distribution capture; not authorized. |
| ModelBindingIntakeEngine | Deferred runtime | Model-binding-intake execution, per-model forecast-data capture, and intake output; not authorized. |
| HorizonScopeEngine | Deferred runtime | Horizon-scope execution, forecast/model-output/query result-value computation, and horizon output; not authorized. |
| VisibilityControlEngine | Deferred runtime | Visibility-control execution, visibility decisions, and visibility action logs; not authorized. |
| ForecastReviewEngine | Deferred runtime | Forecast review execution, adjudication, and automated forecast output; not authorized. |
| TalentSupplyDemandForecastingExperience | Deferred frontend/runtime | Producer/reviewer/consumer forecast UX beyond readiness CRUD; not authorized. |
| TalentSupplyDemandForecastingDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| TalentSupplyDemandForecastingNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| TalentSupplyDemandForecastingReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/TalentSupplyDemandForecastingReadinessMetadata.cs`. |
| TalentSupplyDemandForecastingReadinessState | Domain enum | Readiness state; `Domain/Enums/TalentSupplyDemandForecastingReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| ITalentSupplyDemandForecastingReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/ITalentSupplyDemandForecastingReadinessMetadataRepository.cs`. |
| MongoTalentSupplyDemandForecastingReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `tep_talent_supply_demand_forecasting_readiness`; indexes `ux_tep_talent_supply_demand_forecasting_tenant_code_active` and `ix_tep_talent_supply_demand_forecasting_tenant_state`. |
| TalentSupplyDemandForecasting Application features | Application (CQRS/MediatR) | `Application/Features/TalentSupplyDemandForecasting/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| TalentSupplyDemandForecastingController | API controller | Thin controller; `Api/Controllers/Tep/TalentSupplyDemandForecastingController.cs`. |
| TalentSupplyDemandForecasting golden-compact CRUD | Frontend | Controller + `Views/TalentEcosystem/TalentSupplyDemandForecasting/**` + `wwwroot/assets/js/TalentEcosystem/TalentSupplyDemandForecasting/**` + Resources + TEP nav entry under `frontend/Diten.Web/**` (frontend route `/TalentEcosystem/TalentSupplyDemandForecasting`). |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `TalentSupplyDemandForecastingReadinessState`):

- `Code`
- `DisplayName`
- `TalentSupplyDemandForecastingReadinessState`
- `ForecastCatalogBoundaryState`
- `ModelBindingIntakeBoundaryState`
- `HorizonScopeBoundaryState`
- `VisibilityControlBoundaryState`
- `ForecastReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `TalentDataSourceDependencyState`
- `WorkforceAnalyticsSourceDependencyState`
- `SectorTrendSourceDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `PublicationPolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `TalentSupplyDemandForecastingReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Marker-safe field naming discipline:

- The runtime C# type, namespace, and property identity for this capability IS
  `TalentSupplyDemandForecasting` (type == slug aligned). Unlike `CAND-CAP-0053`,
  this capability carries NO field-name-test fragment to avoid: the visible/public
  term "Talent Supply & Demand Forecasting" contains no fragment that the
  field-name contract test rejects, so the readiness scope is named
  `TalentSupplyDemandForecastingReadinessState` /
  `TalentSupplyDemandForecastingReadinessVersion` directly, with no alias prefix.
  The boundary sub-state property names are `ForecastCatalogBoundaryState`,
  `ModelBindingIntakeBoundaryState`, `HorizonScopeBoundaryState`,
  `VisibilityControlBoundaryState`, and `ForecastReviewBoundaryState` - never
  `ForecastValue`, `ForecastResult`, `HeadcountFigure`, or `DistributionRoster`.
  This keeps the entity contract marker-safe under a WORD/TOKEN boundary (`\b`
  regex) forbidden-marker guard: legitimate domain words (`talent`, `supply`,
  `demand`, `forecast`, `model`, `binding`, `horizon`, `catalog`, `visibility`,
  `review`) are not falsely rejected, while true forbidden markers
  (`measurement`, `value`, `result`, `figure`, `distribution`, `roster`,
  `headcount`, `PII`, `payload`, `score`, `ranking`) are still caught. The public
  slug, route, permission namespace, and Mongo collection use the string constant
  `talent-supply-demand-forecasting` (and
  `tep_talent_supply_demand_forecasting_readiness`), and the visible l10n title is
  "Talent Supply & Demand Forecasting" / "Yetenek Arz ve Talep Tahmini"; these are
  string literals, not property names, so they lie outside the field-name contract
  test. Although no marker-safe rename was required here, the same word-boundary
  guard discipline that governs the Credential->Attestation, SecretsVault->Vault,
  and SalaryBenchmarking->PayBenchmarking precedents still applies to keep true
  forbidden markers out of the field-name contract. Note the upstream sector
  trend source is bound through `SectorTrendSourceDependencyState`, which replaces
  the data-governance-policy dependency-state field used by the sibling Sector
  Talent Trends slice; data minimization/governance is represented here as the
  `DataMinimizationState` governance/precondition field.

Reserved / out-of-scope fields (must not be added in this slice):

- Producer-facing forecast fields (producer model submission, producer review,
  producer action UX state) are reserved and out of scope.
- Reviewer-facing forecast fields (reviewer response, reviewer acknowledgement,
  reviewer self-service UX state) are reserved and out of scope.
- Consumer-facing forecast fields (consumer forecast view, consumer decision,
  consumer self-service UX state) are reserved and out of scope.
- No real forecast/model output/measurement value, supply/demand figure,
  projected/aggregated distribution, or forecast/model-output/query result field of
  any kind. This is a metadata-only readiness slice with no
  forecast/model-output/measurement attribute.
- No supply/demand or per-individual forecast data, employee/company roster,
  participant list, forecast-catalog record content, model-binding-intake content,
  forecast score, participant PII, individual forecast ranking, free-text, forecast
  narrative, or attachment field of any kind.

Forbidden field classes:

- Forecast-catalog record body, model-binding-intake payload, horizon-scope
  payload, participant profile content, visibility-control execution payload,
  forecast-review payload, or completed forecast/model-output/measurement-value
  content.
- Real forecast value, model output value, measurement value, supply/demand
  figure, distribution, forecast/model-output/query result value, forecast score
  result, forecast ranking value, ordering result, rank, match confidence, model
  output, forecast output, or evaluator scoring payload.
- Supply/demand or per-individual forecast data, employee/company roster /
  participant list content, participant PII, individual forecast ranking,
  participant profile content, forecast-catalog record content, forecast narrative,
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

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0056-talent-supply-demand-forecasting.md`

Candidate runtime service:

- `Diten.TalentEcosystemService` (port `5060`)

Runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/TalentEcosystem/TalentSupplyDemandForecasting/**`,
  `wwwroot/assets/js/TalentEcosystem/TalentSupplyDemandForecasting/**`, Resources, TEP nav
  entry; frontend route `/TalentEcosystem/TalentSupplyDemandForecasting`).
- Gateway route exposure through the API gateway (`5080` -> TEP `5060`,
  `/api/talent-supply-demand-forecasting`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.TalentEcosystemService/**` plus its golden-compact
readiness CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned TalentSupplyDemandForecasting CRUD objects
  listed in §3/§5.
- `gateway/**` outside the owned TalentSupplyDemandForecasting readiness route.
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
  Talent Development Network, `CAND-CAP-0050` Industry Succession Pool,
  `CAND-CAP-0051` Verified Certification Registry, `CAND-CAP-0052`
  Mentorship & Recommendation Network, `CAND-CAP-0053` Shared Salary
  Benchmarking, the `CAND-CAP-0054` Workforce Analytics data-source dependency,
  and the `CAND-CAP-0055` Sector Talent Trends sector-trend-source dependency).
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
  talent data source for the talent supply & demand forecasting capability via
  `TalentDataSourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as a raw roster, forecast dataset, or data source.
- `CAND-CAP-0054` - Workforce Analytics (`MOD-0345`), done. Consumed as the
  workforce analytics source for the talent supply & demand forecasting capability
  via `WorkforceAnalyticsSourceDependencyState`; its output is treated as
  boundary/readiness metadata, not as a raw metric/measurement value, supply/demand
  figure/distribution, or data source.
- `CAND-CAP-0055` - Sector Talent Trends (`MOD-0346`), done. Consumed as the
  sector trend source for the talent supply & demand forecasting capability via
  `SectorTrendSourceDependencyState`; its output is treated as boundary/readiness
  metadata, not as a raw trend/metric value, supply/demand figure/distribution, or
  data source.

Governance/control dependencies:

- Consent policy, consumed via `ConsentPreconditionState`. Because this is a
  consent-, visibility-, and publication-governed capability, a valid consent
  basis is a precondition and is treated as boundary/readiness metadata only.
- Data governance / minimization, consumed via `DataMinimizationState`. Data
  minimization, anonymization, and aggregation governance are controlling
  preconditions and are treated as boundary/readiness metadata only.
- Visibility and publication policy, consumed via `VisibilityControlBoundaryState`
  and `PublicationPolicyState`. Visibility/access, horizon scope, and
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
- `CAND-CAP-0042` - Hiring Risk Indicators (`MOD-0332`), done. Sibling/context.
- `CAND-CAP-0043` - Early Warning Signals (`MOD-0333`), done. Sibling/context.
- `CAND-CAP-0044` - Restricted Integrity Registry (`MOD-0334`), done. Sibling/context.
- `CAND-CAP-0045` - Professional Reputation Ledger (`MOD-0335`), done. Sibling/context.
- `CAND-CAP-0046` - Industry Talent Pool (`MOD-0337`), done. Sibling/context.
- `CAND-CAP-0047` - Industry Skill Passport (`MOD-0338`), done. Sibling/context.
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

- Runtime owner/key: `tep.talent-supply-demand-forecasting`.
- Permission namespace:
  - `tep.talent-supply-demand-forecasting.read`
  - `tep.talent-supply-demand-forecasting.manage`
  - `tep.talent-supply-demand-forecasting.evaluate`
  - `tep.talent-supply-demand-forecasting.audit.read`
- Runtime repo scope: `services/Diten.TalentEcosystemService/**`.
- Thin controller (`Api/Controllers/Tep/TalentSupplyDemandForecastingController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `tep_talent_supply_demand_forecasting_readiness`.
- Active tenant `Code` uniqueness through
  `ux_tep_talent_supply_demand_forecasting_tenant_code_active`, with
  `ix_tep_talent_supply_demand_forecasting_tenant_state` supporting tenant/state queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`talent`, `supply`,
  `demand`, `forecast`, `model`, `binding`, `horizon`, `catalog`, `visibility`)
  are not falsely rejected while true forbidden markers (measurement, value,
  result, figure, distribution, roster, headcount, participant, forecast score,
  ranking, PII, payload) are still caught. The word-boundary discipline is
  deliberate and pairs with the type == slug aligned property identity: the C#
  property names use the `TalentSupplyDemandForecasting` prefix directly (no
  field-name-test fragment to avoid), so the field-name contract test passes, and
  the compound `TalentSupplyDemandForecasting` and the domain word `forecast` pass
  the guard while the standalone forbidden tokens `measurement`, `value`, and
  `result` are still rejected.

Explicitly unauthorized:

- Real forecast/model output/measurement value handling, aggregation, or ranking
  execution.
- Forecast-catalog record / model-binding-intake / horizon-scope record
  persistence.
- Model-binding-intake execution, enforcement, or per-model forecast-data
  payload persistence.
- Horizon-scope execution, forecast/model-output/query result-value computation, or
  horizon output.
- Visibility-control execution, visibility delivery, visibility decisions, or
  visibility action logs.
- Forecast-review execution, adjudication, or automated forecast output.
- Forecast/model-output/query-result-value/finding/model-output persistence.
- Automated decision and forecast-scoring/ranking behavior.
- Producer/reviewer/consumer forecast UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Real forecast/model output/measurement values, supply/demand figures/projected
  distributions, supply/demand or per-individual forecast data,
  forecast/model-output/query result values, employee/company rosters,
  participant lists, forecast-catalog content, forecast narrative, participant
  profile content, forecast scores, participant PII, individual forecast rankings,
  free-text, attachment payload, or raw payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- TEP tenant shell nav entry `Talent Supply & Demand Forecasting` (frontend route
  `/TalentEcosystem/TalentSupplyDemandForecasting`).
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources
  ("Talent Supply & Demand Forecasting" / "Yetenek Arz ve Talep Tahmini").

Unauthorized UX:

- Producer forecast UX beyond readiness-metadata CRUD.
- Reviewer forecast/self-service UX beyond readiness-metadata CRUD.
- Consumer forecast/self-service UX beyond readiness-metadata CRUD.
- Forecast catalog, model binding intake, horizon scope, visibility
  control, forecast review, forecast/model-output/query-result-value, or
  model-output UI.
- Document, notification, export, upload, or attachment UI.
- Any real forecast/model output/measurement value, supply/demand figure/projected
  distribution, supply/demand or per-individual forecast data, employee/company
  roster, participant list, forecast-catalog content, forecast score, participant
  PII, individual forecast ranking, free-text, forecast narrative, assessment
  content, or raw payload viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> TEP `5060`).

Authorized API surface:

- `GET /api/talent-supply-demand-forecasting` - list readiness metadata.
- `GET /api/talent-supply-demand-forecasting/{id}` - get readiness metadata by id.
- `POST /api/talent-supply-demand-forecasting` - create readiness metadata.
- `POST /api/talent-supply-demand-forecasting/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/talent-supply-demand-forecasting/{id}` - soft-delete.
- `GET /api/talent-supply-demand-forecasting/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get talent supply & demand forecasting readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No forecast/model-output/measurement value handling, forecast-catalog record /
  model-binding-intake / horizon-scope record persistence,
  model-binding-intake execution, horizon-scope execution,
  visibility-control execution, forecast-review execution,
  forecast/model-output/query-result-value or model-output persistence, forecast-scoring or
  automated decision behavior, notification/document integration, or
  forecast-value/roster/participant-list/forecast-score/PII/individual-ranking/
  free-text/narrative/raw-payload persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only talent supply & demand forecasting readiness state.
- Boundary-state metadata for forecast catalog, model binding intake,
  horizon scope, visibility control, forecast review, automated decision,
  consent, minimization, retention, and publication policy.
- Dependency-state metadata for talent-data source, workforce-analytics source,
  sector-trend source, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Model-binding-intake/forecast-catalog payload.
- Forecast-catalog record body, model-binding-intake content, or
  horizon-scope content.
- Model-binding-intake execution/enforcement payload.
- Horizon-scope execution payload, forecast/model-output/query result value, or horizon
  output.
- Visibility-control execution payload, visibility delivery, visibility
  decisions, or visibility action logs.
- Forecast-review/adjudication payload.
- Forecast/model-output/query result value / forecast-ranking value / ordering result / rank /
  match confidence / model output.
- Real forecast/model output/measurement value, supply/demand figure, projected/aggregated
  distribution, supply/demand or per-individual forecast data, employee/company roster / participant list
  content, participant PII, individual forecast ranking, participant profile content,
  forecast-catalog content, forecast narrative, visibility content, or
  free-text.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy participant/individual details.
- Compensation/payroll/benefits payload.

Consent-sensitivity note: because this is a consent-, visibility-, and
publication-governed, privacy-sensitive talent supply & demand forecasting
capability, no real forecast/model output/measurement value, supply/demand figure,
projected/aggregated distribution, supply/demand or per-individual forecast data,
employee/company roster, participant list, forecast-catalog content,
forecast/model-output/query result value, or individual forecast ranking of any
kind may cross the boundary into persistence. Only boundary/readiness STATE
metadata governed by a valid consent basis, data governance policy, visibility
scope, and publication policy is permitted.

## 12. Permission Boundary

Approved permission namespace:

- `tep.talent-supply-demand-forecasting.read`
- `tep.talent-supply-demand-forecasting.manage`
- `tep.talent-supply-demand-forecasting.evaluate`
- `tep.talent-supply-demand-forecasting.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Runtime work must be tenant-aware and fail closed. The backend/API slice must
resolve tenant server-side and must not expose `TenantId` in request DTOs.

Security guardrails:

- Candidate identity (`CAND-CAP-0056`) must not become a runtime literal.
- Blocked legacy ID (`MOD-0347`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_tep_talent_supply_demand_forecasting_tenant_code_active` and
  `ix_tep_talent_supply_demand_forecasting_tenant_state`.
- Because this is a consent-, visibility-, and publication-governed,
  privacy-sensitive capability, consent basis, data governance policy, visibility
  scope, and publication policy are treated as the highest-risk controls even for
  readiness metadata; access is restricted and audited.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation, with a valid consent basis
  treated as a precondition to activation.
- Talent-supply-demand-forecasting data minimization fail-closed behavior, with data
  governance policy treated as a controlling precondition.
- Retention/publication/audit/visibility/deletion as local/deferred metadata,
  with visibility scope and publication policy treated as controlling
  preconditions.
- Exclusion of real forecast/model output/measurement values, supply/demand figures/projected
  distributions, supply/demand or per-individual forecast data, forecast/model-output/query result values,
  employee/company rosters, participant lists, forecast-catalog content, forecast
  narrative content, participant profile content, forecast scores, participant PII,
  individual forecast rankings, free-text, attachments, and any PII dataset.

The Talent Supply & Demand Forecasting capability declares readiness STATE only; it does not own
or store any real forecast/model output/measurement values, supply/demand figures/projected
distributions, supply/demand or per-individual forecast data, forecast/model-output/query result values,
employee/company rosters, participant lists, forecast-catalog content, forecast narrative
content, participant profile content, forecast scores, participant PII, individual
forecast rankings, or free-text, and all downstream consumers must treat its
output as boundary/readiness metadata, not as a forecast-value/model-output or data
source. Because this is a consent-, visibility-, and publication-governed,
privacy-sensitive capability, its consent-basis, data governance, visibility,
publication, retention, and restricted-access constraints are treated as the
highest-risk controls: no real forecast/model output/measurement value, supply/demand
figure/distribution, supply/demand or per-individual forecast data, employee/company roster,
forecast/model-output/query result value, or individual forecast ranking may ever be persisted
in this slice, and access to even the readiness metadata is restricted and audited.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, model-binding-intake
connector, or document/notification UI is authorized. The `CAND-CAP-0041` Talent
Data Foundation capability is consumed only as an upstream source dependency-state
context; the `CAND-CAP-0054` Workforce Analytics capability is consumed only as an
upstream source dependency-state context; the `CAND-CAP-0055` Sector Talent Trends
capability is consumed only as an upstream source dependency-state context;
consent-policy and data-governance-policy are consumed only as governance
dependency-state context; HCM foundation (`CAND-CAP-0007` through `CAND-CAP-0010`)
is consumed only as prerequisite dependency-state context; and TEP `CAND-CAP-0011`
through `CAND-CAP-0021`, `CAND-CAP-0042`, `CAND-CAP-0043`, `CAND-CAP-0044`,
`CAND-CAP-0045`, `CAND-CAP-0046`, `CAND-CAP-0047`, `CAND-CAP-0048`,
`CAND-CAP-0049`, `CAND-CAP-0050`, `CAND-CAP-0051`, `CAND-CAP-0052`, and
`CAND-CAP-0053` are consumed only as sibling/context references.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0056`.
3. AC-03: Legacy Excel ID `MOD-0347` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `tep.talent-supply-demand-forecasting`.
5. AC-05: Permission namespace is limited to
   `tep.talent-supply-demand-forecasting.read`,
   `tep.talent-supply-demand-forecasting.manage`,
   `tep.talent-supply-demand-forecasting.evaluate`, and
   `tep.talent-supply-demand-forecasting.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.TalentEcosystemService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection
   `tep_talent_supply_demand_forecasting_readiness`) includes active tenant `Code`
   uniqueness (`ux_tep_talent_supply_demand_forecasting_tenant_code_active`),
   tenant/state indexing (`ix_tep_talent_supply_demand_forecasting_tenant_state`),
   soft delete, and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Real forecast/model output/measurement value handling, forecast-catalog record /
    model-binding-intake / horizon-scope record persistence,
    model-binding-intake execution, horizon-scope execution,
    visibility-control execution, forecast-review execution, model output,
    forecast/model-output/query result values, supply/demand or per-individual forecast-data or
    employee/company roster persistence, forecast-catalog / forecast-narrative content
    persistence, and automated decision behavior remain unauthorized.
14. AC-14: Producer/reviewer/consumer forecast fields remain reserved/out of
    scope; no real forecast/model output/measurement value or result field is present; no
    forecast-value/supply-demand-figure/distribution/roster/participant-list/forecast-score/
    PII/individual-ranking/free-text/narrative field is present. Field names
    remain marker-safe: the runtime C# type/namespace/property identity uses the
    `TalentSupplyDemandForecasting` prefix (type == slug aligned, no field-name-test fragment
    to avoid) and the boundary names never contain the forbidden markers
    `measurement`/`value`/`result`/`figure`/`distribution`.
15. AC-15: Real forecast/model output/measurement values, supply/demand figures/projected distributions,
    supply/demand or per-individual forecast data, forecast/model-output/query result values,
    employee/company rosters, participant lists, forecast-catalog content, forecast
    narrative, participant profile content, forecast scores, participant PII,
    individual forecast rankings, free-text, attachment payload, raw provider payload,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized. The forbidden-marker guard asserts a WORD/TOKEN boundary
    (`\b` regex) match, NOT a bare substring `Contains`, so legitimate domain
    words (`talent`, `supply`, `demand`, `forecast`, `model`, `horizon`) pass
    while `measurement`/`value`/`result`/`figure`/`distribution`/`roster` are
    caught; and the entity field names use the `TalentSupplyDemandForecasting` prefix
    directly so the field-name contract test passes.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: Upstream source, governance, TEP, HCM foundation, and PSS dependency
    boundaries are recorded (`CAND-CAP-0041` Talent Data Foundation as upstream
    talent data source; `CAND-CAP-0054` Workforce Analytics as upstream workforce
    analytics source; `CAND-CAP-0055` Sector Talent Trends as upstream sector
    trend source; consent-policy and data-governance-policy as governance
    dependencies; TEP `CAND-CAP-0011` through `CAND-CAP-0021`, `CAND-CAP-0042`,
    `CAND-CAP-0043`, `CAND-CAP-0044`, `CAND-CAP-0045`, `CAND-CAP-0046`,
    `CAND-CAP-0047`, `CAND-CAP-0048`, `CAND-CAP-0049`, `CAND-CAP-0050`,
    `CAND-CAP-0051`, `CAND-CAP-0052`, and `CAND-CAP-0053` as sibling/context;
    HCM `CAND-CAP-0007` through `CAND-CAP-0010` as prerequisite context).
18. AC-18: `tr`/`en` localization parity is present
    ("Talent Supply & Demand Forecasting" / "Yetenek Arz ve Talep Tahmini").
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0056|MOD-0347` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build TEP API:
  `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `TalentSupplyDemandForecasting` filter
  (`Application.Tests/TalentSupplyDemandForecastingTests.cs`, handler behavior;
  fix-absent should be RED).
- Run full TEP Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_tep_talent_supply_demand_forecasting_tenant_code_active`), and tenant/state
  indexing (`ix_tep_talent_supply_demand_forecasting_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`talent`, `supply`,
  `demand`, `forecast`, `model`, `horizon`, `visibility`) are not falsely
  rejected while the forbidden tokens `measurement`, `value`, `result`, `figure`,
  and `distribution` are still caught.
- Verify the entity field names use the `TalentSupplyDemandForecasting` prefix (type == slug
  aligned property identity, no field-name-test fragment to avoid) so the
  field-name contract test passes.
- Verify forbidden forecast-catalog/model-binding-intake/horizon-scope/
  visibility/forecast-review/decision/sensitive fields are absent.
- Verify producer/reviewer/consumer forecast fields are reserved/absent, no
  real forecast/model output/measurement value or result field exists, and no
  forecast-value/roster/participant-list/forecast-score/PII/individual-ranking/
  free-text/narrative field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0056|MOD-0347" services frontend gateway`
  returns no runtime literal (candidate IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real forecast/model output/measurement value handling prohibition is accepted.
- [x] Forecast-catalog record / model-binding-intake / horizon-scope record
  persistence prohibition is accepted.
- [x] Model-binding-intake / horizon-scope / visibility-control / forecast
  review execution prohibition is accepted.
- [x] Forecast/model-output/query-result-value and model-output prohibition is accepted.
- [x] Forecast-scoring/ranking and automated decision behavior prohibition is
  accepted.
- [x] Reserved producer/reviewer/consumer forecast fields,
  no-real-forecast-value/result-field, and
  no-forecast-value/roster/participant-list/forecast-score/PII/individual-ranking/
  free-text/narrative constraints are accepted.
- [x] Sensitive/PII, supply/demand or per-individual forecast-data, employee/company roster
  content, and raw payload prohibition is accepted.
- [x] Marker-safe `TalentSupplyDemandForecasting` property identity (type == slug aligned,
  no field-name-test fragment to avoid) is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Talent-data source, workforce-analytics source, sector-trend source,
  consent-policy, data-governance-policy, visibility-policy, TEP/HCM/PSS dependency
  context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only talent supply & demand forecasting readiness contract.
- [x] Marker-safe runtime identity decision (`TalentSupplyDemandForecasting` C#
  type/namespace/property prefix, type == slug aligned; `talent-supply-demand-forecasting`
  public slug/route/permission/collection string constants).
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Talent-supply-demand-forecasting data minimization policy.
- [x] Retention/publication/audit/visibility/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.TalentEcosystemService/**`
  and the owned `frontend/Diten.Web/**` TalentSupplyDemandForecasting objects.
- Runtime owner/key remains limited to `tep.talent-supply-demand-forecasting`.
- Permission namespace remains limited to
  `tep.talent-supply-demand-forecasting.read`,
  `tep.talent-supply-demand-forecasting.manage`,
  `tep.talent-supply-demand-forecasting.evaluate`, and
  `tep.talent-supply-demand-forecasting.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract, and remain marker-safe: the runtime C#
  type/namespace/property identity uses the `TalentSupplyDemandForecasting` prefix (type ==
  slug aligned, no field-name-test fragment to avoid) and never contains the
  forbidden markers `measurement`/`value`/`result`/`figure`/`distribution`, while
  the public slug/route/permission/collection use the `talent-supply-demand-forecasting`
  string constant and the visible l10n title is "Talent Supply & Demand Forecasting" /
  "Yetenek Arz ve Talep Tahmini". Although no marker-safe rename was required
  (unlike the SalaryBenchmarking->PayBenchmarking precedent), the same
  word-boundary guard discipline as the Credential->Attestation and
  SecretsVault->Vault precedents is enforced.
- `TalentDataSourceDependencyState`, `WorkforceAnalyticsSourceDependencyState`,
  `SectorTrendSourceDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states; consent is represented as the
  `ConsentPreconditionState` governance/precondition field and data minimization
  as the `DataMinimizationState` governance/precondition field.
- Producer/reviewer/consumer forecast UX fields remain reserved/out of
  scope, no real forecast/model output/measurement value or result field is present, and no
  forecast-value/roster/participant-list/forecast-score/PII/individual-ranking/
  free-text/narrative field is present.
- HCM, PSS, and Platform scopes remain closed.
- Real forecast/model output/measurement value handling, forecast-catalog record /
  model-binding-intake / horizon-scope record persistence,
  model-binding-intake execution, horizon-scope execution,
  visibility-control execution, forecast-review execution, model output,
  forecast/model-output/query result values, automated decision behavior,
  notification/document integration, real forecast/model output/measurement values, supply/demand
  figures/distributions, supply/demand or per-individual forecast data, employee/company
  rosters, participant lists, forecast-catalog content, forecast narrative, participant
  profile content, forecast scores, participant PII, individual forecast
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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0056 --name "Talent Supply & Demand Forecasting"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0056`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Marker-safe identity waiver approved: the runtime C# type/namespace/property
  identity uses the `TalentSupplyDemandForecasting` prefix (type == slug aligned, no
  field-name-test fragment to avoid), so the field-name contract test passes,
  while the public slug/route/permission/collection use the `talent-supply-demand-forecasting`
  string constant and the l10n title stays "Talent Supply & Demand Forecasting" / "Yetenek
  Arz ve Talep Tahmini". The word-boundary guard discipline mirrors the
  Credential->Attestation and SecretsVault->Vault precedents even though no
  rename was required.
- Legal/privacy/consent metadata-only waiver approved.
- Talent-supply-demand-forecasting data minimization fail-closed policy approved.
- Retention/publication/audit/visibility/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0056|MOD-0347" services frontend gateway`
- Candidate IDs are governance-only and must return no runtime literal match.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real forecast-catalog intake and catalog management.
- Model-binding-intake execution and enforcement.
- Horizon-scope execution and forecast/model-output/query result-value computation.
- Visibility-control execution and visibility decisions.
- Forecast review and automated forecast adjudication.
- Automated decision approval.
- Producer/reviewer/consumer forecast UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
