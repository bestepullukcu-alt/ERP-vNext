---
id: MOD-0062
name: Baseline & Experiment Measurement
domain: data-knowledge-intelligence
blueprint_domain: Data, Knowledge & Intelligence
service: Diten.DataKnowledgeService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: runtime-authored
owner: enterprise-architect / dki-domain-owner / data-platform-owner / analytics-owner / platform-team / security-owner
branch: feature/dki/mod-0062-baseline-experiment-measurement
started: 2026-09-07
target: 2026-12-08
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
identity_class: canonical (Blueprint-backed)
canonicalization_status: canonical / registry-row-created / runtime-authored
runtime_service_candidate: Diten.DataKnowledgeService
runtime_service_port: 5062
gateway_port: 5000
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: dki.baseline-experiment-measurement
permission_namespace:
  - dki.baseline-experiment-measurement.read
  - dki.baseline-experiment-measurement.manage
  - dki.baseline-experiment-measurement.evaluate
  - dki.baseline-experiment-measurement.audit.read
runtime_repo_scope: services/Diten.DataKnowledgeService/**
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0062 - Baseline & Experiment Measurement

> Status: runtime-authored. This pack records the first metadata-only backend/API
> baseline/experiment measurement readiness contract slice under
> `services/Diten.DataKnowledgeService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5000` -> DKI `5062`). `MOD-0062` is **Blueprint-canonical** (Blueprint
> domain **Data, Knowledge & Intelligence**); the canonical MOD ID is used
> directly and remains a governance/documentation identity that must not be
> written into runtime literals. The governance-first runtime deferral recorded in
> the prior draft is **REVERTED**: per `WP-DKI-SLICE-0062`, EA resolved the
> DCP-008 "Analytics backbone — Pending EA domain mapping" note by assigning the
> repo domain `data-knowledge-intelligence` and the dedicated data-plane runtime
> service `Diten.DataKnowledgeService` (namespace `Diten.DataKnowledgeService.*`,
> port `5062`, Mongo db `DitenDataKnowledge`, permission owner key `dki.*`). Real
> baseline/experiment measurement, statistical computation, measurement
> results/metric values, baseline/experiment sample or observation rows,
> statistical values (p-values / effect sizes / confidence intervals), variant
> assignments, raw event/telemetry data, subject/participant PII, individual
> attributions, model output or automated decision behavior, and sensitive/PII-heavy
> persistence remain closed. `MOD-0062` is the **final** module of the analytics
> backbone (`MOD-0004`, `MOD-0059`–`MOD-0064`); this slice completes that group.

## 1. Module Summary

`MOD-0062` is the Blueprint-canonical **Baseline & Experiment Measurement**: the
**measurement-setup layer** of the analytics backbone — the readiness layer for
how **baselines** and **experiments** (A/B, cohort, before/after) are *defined*,
*bound*, and *published* over metrics: baseline/experiment identity, baseline
catalog scope, experiment design intake, measurement binding scope, result
publication control, and experiment review. It is a **measurement-setup
(governance) layer**, not a statistics engine, an experiment-execution runtime,
or a results store: it declares the readiness STATE of *baseline/experiment
measurement-setup metadata* and never holds measurement results or metric values,
baseline/experiment sample or observation rows, statistical values (p-values,
effect sizes, confidence intervals), variant assignments, raw event/telemetry
data, subject/participant PII, or individual attributions. It defines **how
measurement is set up**; it does **not** store the measured results or run the
statistics engine.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and originally
marked its domain ownership as "Pending EA domain mapping" with no existing
service. That deferral is now resolved: EA assigned the repo domain
`data-knowledge-intelligence` and reused the dedicated data-plane runtime service
(`Diten.DataKnowledgeService`) to own this measurement-setup capability, rather
than defaulting it to `Diten.EnterpriseStrategyService` or `Diten.Platform`. This
pack records the first metadata-only backend/API readiness contract slice authored
under `WP-DKI-SLICE-0062`, mirroring the completed readiness slices exactly, and
completing the analytics backbone.

The Baseline & Experiment Measurement capability is the readiness layer for the
measurement-setup plane that defines baselines and experiments over already-owned
analytics concepts: it declares the readiness STATE of the baseline catalog, the
experiment design intake boundary, measurement binding scope, result publication
control, experiment review, and the automated-decision boundary. It sits in the
analytics-backbone group (`MOD-0004`, `MOD-0059`–`MOD-0064`) as the final module:
it measures against the concrete definitions of `MOD-0060 Metric Definitions &
Ownership` over `MOD-0004 Metric & Semantic Registry` semantics, reads
measurement data from `MOD-0063 Data Warehouse / Lakehouse` storage, and is
surfaced via `MOD-0061 Scorecards / Dashboards`, holding only measurement-setup
metadata and metric/scorecard references — never the underlying values, samples,
or statistics. Real baseline/experiment measurement, statistical computation,
measurement results or metric values, baseline/experiment sample or observation
rows, statistical values, variant assignments, raw event/telemetry data,
subject/participant PII, individual attributions, model output, automated
decision behavior, and sensitive/PII-heavy persistence remain closed.

## 2. Ownership and Boundaries

`MOD-0062` is Blueprint-canonical and is used directly as the canonical
**Baseline & Experiment Measurement** identity; no candidate identity and no name
drift apply to this ID.

Owned by this pack:

- DKI-native baseline/experiment measurement-setup governance boundary.
- Sequencing as the final module of the analytics-backbone group (`MOD-0004`,
  `MOD-0059`–`MOD-0064`), measuring against `MOD-0060` definitions over `MOD-0004`
  semantics and `MOD-0063` storage, surfaced via `MOD-0061` scorecards.
- Metadata-only baseline/experiment measurement readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the baseline catalog, experiment design
  intake, measurement binding scope, result publication control, and experiment
  review, consumed by downstream analytics-backbone and consumer surfaces.
- Explicit exclusion of real baseline/experiment measurement and statistical
  computation.
- Explicit exclusion of measurement results or metric values, sample/observation
  row materialization, statistical values, variant assignments, scoring, model
  output, and automated decision behavior.
- Explicit exclusion of author-facing and admin-facing experiment-authoring UX
  beyond the readiness-metadata CRUD (those fields are reserved and out of scope).
- Explicit exclusion of measurement results or metric values, baseline/experiment
  sample or observation rows, statistical values (p-values / effect sizes /
  confidence intervals), variant assignments, raw event/telemetry data,
  subject/participant PII, individual attributions, free-text, and any PII dataset
  persistence.

Not owned by this pack:

- Concrete metric definitions, formulas, thresholds, and ownership — owned by
  `MOD-0060 Metric Definitions & Ownership`, referenced here by readiness state
  only.
- The shared semantic vocabulary (metric identity, semantic type, unit, grain,
  aggregation semantics) — owned by `MOD-0004 Metric & Semantic Registry`, bound
  here by readiness state only.
- Curated KPIs — owned by `MOD-0059 KPI Catalog`.
- Visual composition — owned by `MOD-0061 Scorecards / Dashboards`, referenced
  here by readiness state only.
- Physical/measured measurement data and datasets — owned by `MOD-0063 Data
  Warehouse / Lakehouse`, referenced here by readiness state only.
- Data movement / transformation — owned by `MOD-0064 ETL / ELT Pipelines`.
- The statistical computation runtime that computes experiment results and
  significance — `MOD-0062` sets up measurement and is explicitly **not** a
  statistics engine.
- Data dictionary and data contract registry ownership beyond dependency/context
  references.
- Secrets/vault, notification, and document ownership beyond dependency/context
  references.
- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped experiments — a **distinct**
  consumer surface separate from `MOD-0062`'s general analytics-backbone baseline/
  experiment measurement; ESBP is a consumer of measurement-setup concepts, not
  the owner of this data plane.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` and other facades — consumers
  that own no analytics runtime.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| BaselineExperimentMeasurementCapabilityBoundary | Governance boundary | Defines what the DKI baseline/experiment measurement-setup module may own. |
| BaselineExperimentMeasurementReadinessBoundary | Governance boundary | Separates readiness metadata from real measurement, statistical computation, and results. |
| BaselineCatalogBoundary | Governance boundary | Blocks measurement results / metric value and baseline sample/observation-row persistence in the baseline catalog. |
| ExperimentDesignIntakeBoundary | Governance boundary | Blocks variant assignment and subject/participant PII persistence at experiment design intake. |
| MeasurementBindingScopeBoundary | Governance boundary | Blocks raw event/telemetry data and sample/observation-row persistence in measurement binding scope. |
| ResultPublicationControlBoundary | Governance boundary | Blocks statistical values (p-values / effect sizes / confidence intervals) and result-content persistence in the result publication lifecycle. |
| ExperimentReviewBoundary | Governance boundary | Blocks free-text review narrative and review-note body persistence. |
| BaselineExperimentMeasurementDecisionBoundary | Governance boundary | Blocks scoring, ranking, model-output, and automated decision persistence. |
| BaselineExperimentMeasurementUxBoundary | Governance boundary | Blocks author/admin experiment-authoring UX beyond readiness-metadata CRUD. |
| BaselineExperimentMeasurementSensitiveDataBoundary | Governance boundary | Blocks measurement results or metric values, sample/observation rows, statistical values, variant assignments, raw event/telemetry data, subject/participant PII, individual attributions, and PII persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| BaselineExperimentComputationEngine | Deferred runtime | Real baseline/experiment measurement and statistical computation; not authorized. |
| MeasurementResultMaterializationEngine | Deferred runtime | Measurement result / metric value materialization and sample/observation-row production; not authorized. |
| StatisticalSignificanceEngine | Deferred runtime | Statistical value (p-value / effect size / confidence interval) computation and evaluation; not authorized. |
| VariantAssignmentEngine | Deferred runtime | Variant/cohort assignment and subject segmentation; not authorized. |
| ResultPublicationEngine | Deferred runtime | Result publication/sharing distribution and viewer resolution; not authorized. |
| BaselineExperimentMeasurementScoringDecisionEngine | Deferred runtime | Scoring, ranking, model-output, and automated decision behavior; not authorized. |
| AuthorAdminExperimentExperience | Deferred frontend/runtime | Author/admin experiment-authoring UX beyond readiness CRUD; not authorized. |
| BaselineExperimentMeasurementDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| BaselineExperimentMeasurementNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| BaselineExperimentMeasurementReadinessMetadata | Domain entity | Metadata-only readiness contract; `src/Diten.DataKnowledgeService.Domain/Entities/BaselineExperimentMeasurementReadinessMetadata.cs`. |
| BaselineExperimentMeasurementReadinessState | Domain enum | Readiness state; `src/Diten.DataKnowledgeService.Domain/Enums/BaselineExperimentMeasurementReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IBaselineExperimentMeasurementReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `src/Diten.DataKnowledgeService.Domain/Repositories/IBaselineExperimentMeasurementReadinessMetadataRepository.cs`. |
| MongoBaselineExperimentMeasurementReadinessMetadataRepository | Persistence repository | Mongo-backed repository (db `DitenDataKnowledge`); collection `dki_baseline_experiment_measurement_readiness`; indexes `ux_dki_baseline_experiment_measurement_tenant_code_active` and `ix_dki_baseline_experiment_measurement_tenant_state`. |
| BaselineExperimentMeasurement Application features | Application (CQRS/MediatR) | `src/Diten.DataKnowledgeService.Application/Features/BaselineExperimentMeasurement/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| BaselineExperimentMeasurementController | API controller | Thin controller; `src/Diten.DataKnowledgeService.Api/Controllers/Dki/BaselineExperimentMeasurementController.cs`. |
| BaselineExperimentMeasurement golden-compact CRUD | Frontend | `Controllers/BaselineExperimentMeasurementController.cs` + `Views/DataKnowledge/BaselineExperimentMeasurement/**` + `Models/DataKnowledge/BaselineExperimentMeasurement/**` + `wwwroot/assets/js/DataKnowledge/BaselineExperimentMeasurement/**` + Resources + DKI nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract, verified against
`services/Diten.DataKnowledgeService/src/Diten.DataKnowledgeService.Domain/Entities/BaselineExperimentMeasurementReadinessMetadata.cs`:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `BaselineExperimentMeasurementReadinessState`):

- `Code`
- `DisplayName`
- `BaselineExperimentMeasurementReadinessState`
- `BaselineCatalogBoundaryState`
- `ExperimentDesignIntakeBoundaryState`
- `MeasurementBindingScopeBoundaryState`
- `ResultPublicationControlBoundaryState`
- `ExperimentReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `MetricSemanticRegistrySourceDependencyState`
- `ScorecardSourceDependencyState`
- `DataContractRegistryDependencyState`
- `NotificationDependencyState`
- `StewardshipPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `MeasurementPolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `BaselineExperimentMeasurementReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Field-naming discipline (marker-safe): field names are deliberately chosen to
avoid forbidden-marker fragments so the contract field-name test does not falsely
reject legitimate domain fields. In particular, no property name carries a
`score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
marker fragment: the upstream semantic-vocabulary dependency field is named
`MetricSemanticRegistrySourceDependencyState` (using `MetricSemanticRegistrySource*`,
referencing `MOD-0004` rather than embedding a `sample`/`observation`/`result`
marker), and the composition-plane dependency field is named
`ScorecardSourceDependencyState` (using `ScorecardSource*`, referencing `MOD-0061`
rather than embedding a `chart`/`measure`/`payload` marker), so neither collides
with the forbidden marker class. The forbidden-marker matcher uses a word/token
boundary (`\b` regex), not a bare substring `Contains`, so legitimate domain words
(`baseline`, `experiment`, `measurement`, `catalog`, `design`, `binding`,
`publication`, `review`) are not falsely rejected while true forbidden markers
(measurement result, metric value, baseline/experiment sample, observation row,
p-value, effect size, confidence interval, variant assignment, raw event,
telemetry, subject/participant PII, individual attribution, `score`, `rating`,
`rank`, `narrative`, PII, payload) are still caught.

Reserved / out-of-scope fields (must not be added in this slice):

- Author-facing experiment fields (author experiment submission, author review,
  author action UX state) are reserved and out of scope.
- Admin-facing experiment fields (admin experiment/authoring administration,
  admin result publication, admin self-service UX state) are reserved and out of
  scope.
- No money field (salary/compensation/cost/budget/target-value). This is a
  metadata-only readiness slice with no monetary attribute.
- No measurement result or metric value, baseline/experiment sample or observation
  row, statistical value (p-value / effect size / confidence interval), variant
  assignment, raw event/telemetry datum, subject/participant PII, individual
  attribution, or PII dataset field of any kind.

Forbidden field classes:

- Measurement result or metric value, baseline/experiment sample, observation
  row/record body, raw event/telemetry datum, or completed measurement-output
  content.
- Statistical value (p-value, effect size, confidence interval, lift,
  significance), readiness score, quality score, rating value, rank, match
  confidence, model output, automated decision result, recommendation output, or
  evaluator scoring payload.
- Variant/cohort assignment, subject/participant PII, individual attributions,
  free-text experiment/review narrative, HR notes, or sensitive data body.
- Image/export blob, rendered payload, attachment payload, controlled document
  body, external document content, raw evidence, raw provider payload, or uploaded
  file metadata.
- Connection string, credential, token, secret, password, activation code,
  provider connection value, device secret, or account bootstrap secret.
- Salary amount, compensation amount, target/threshold amount, benefits election,
  payroll details, tax details, bank details, or payment instruction fields.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  any PII-heavy dataset/record field.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0062-baseline-experiment-measurement.md`
- The `MOD-0062` row in `execution/registries/module-id-registry.md`.

Runtime service:

- `Diten.DataKnowledgeService` (namespace `Diten.DataKnowledgeService.*`, port
  `5062`, Mongo db `DitenDataKnowledge`).

Runtime repo scope:

- `services/Diten.DataKnowledgeService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (`Controllers/BaselineExperimentMeasurementController.cs`,
  `Views/DataKnowledge/BaselineExperimentMeasurement/**`,
  `Models/DataKnowledge/BaselineExperimentMeasurement/**`,
  `wwwroot/assets/js/DataKnowledge/BaselineExperimentMeasurement/**`, Resources,
  DKI nav entry at `/DataKnowledge/BaselineExperimentMeasurement`).
- Gateway route `/api/baseline-experiment-measurement` exposure through the API
  gateway (`5000` -> DKI `5062`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.DataKnowledgeService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned BaselineExperimentMeasurement CRUD objects
  listed in §3/§5.
- `gateway/**` outside the owned `/api/baseline-experiment-measurement` readiness
  route.
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.Platform/**`
- `services/Diten.HumanCapitalService/**`
- global `tests/**`
- the other analytics-backbone module packs (`MOD-0004`, `MOD-0059`–`MOD-0061`,
  `MOD-0063`, `MOD-0064`).
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/data-knowledge-intelligence/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Analytics-backbone relationships:

- Depends on: `MOD-0060 Metric Definitions & Ownership` — `MOD-0062` baselines/
  experiments measure against concrete metric definitions; its availability is
  treated as boundary/readiness metadata context, never the definition bodies
  themselves.
- Depends on (upstream vocabulary): `MOD-0004 Metric & Semantic Registry` — every
  measured metric binds to the shared semantic vocabulary authored there; its
  availability is treated as boundary/readiness metadata context, consumed via
  `MetricSemanticRegistrySourceDependencyState`, never the vocabulary body itself.
- Depends on (composition plane): `MOD-0061 Scorecards / Dashboards` — baseline/
  experiment measurement readiness is surfaced via scorecards/dashboards; its
  availability is treated as boundary/readiness metadata context, consumed via
  `ScorecardSourceDependencyState`, never the composition body itself.
- Depends on (physical plane): `MOD-0063 Data Warehouse / Lakehouse` — the
  physical/measured measurement data backing baselines/experiments lives there;
  its availability is treated as boundary/readiness metadata context, never the
  physical data itself.
- Related physical plane: `MOD-0064 ETL / ELT Pipelines` feeds `MOD-0063`;
  referenced as context only. `MOD-0059 KPI Catalog` sits alongside as a sibling
  consumer of the same definitions; referenced as context only.
- Consumed by: downstream analytics-backbone and consumer surfaces — consumption
  is boundary/readiness metadata context only. `MOD-0062` is the final module of
  the analytics backbone.

Runtime dependency-state context (metadata-only, via boundary/dependency states):

- Metric & Semantic Registry Source (the `MOD-0004` shared semantic vocabulary),
  consumed via `MetricSemanticRegistrySourceDependencyState` (field named
  `MetricSemanticRegistrySource*`, marker-safe). Registry-source availability is a
  controlling precondition and is treated as boundary/readiness metadata only; no
  metric definition body, measurement result, or metric value is persisted.
- Scorecard Source (the `MOD-0061` composition plane), consumed via
  `ScorecardSourceDependencyState` (field named `ScorecardSource*`).
  Scorecard-source availability is a controlling precondition; no composition body
  or rendered visual is persisted.
- Data Contract Registry, consumed via `DataContractRegistryDependencyState`.
- Notification, consumed via `NotificationDependencyState`.

Consumer context (not dependencies of this module, they depend on / consume it):

- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped experiment runtime — a
  distinct consumer surface, non-owning.
- HCM `CAND-CAP-0034` HR KPI & Analytics Facade — a consumer facade, non-owning.

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is authorized
only for the first metadata-only backend/API readiness slice, along with the
golden-compact readiness CRUD frontend and gateway route.

Authorized runtime boundary:

- Runtime owner/key: `dki.baseline-experiment-measurement`.
- Permission namespace:
  - `dki.baseline-experiment-measurement.read`
  - `dki.baseline-experiment-measurement.manage`
  - `dki.baseline-experiment-measurement.evaluate`
  - `dki.baseline-experiment-measurement.audit.read`
- Runtime repo scope: `services/Diten.DataKnowledgeService/**`.
- Thin controller
  (`src/Diten.DataKnowledgeService.Api/Controllers/Dki/BaselineExperimentMeasurementController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository (db `DitenDataKnowledge`); collection
  `dki_baseline_experiment_measurement_readiness`.
- Active tenant `Code` uniqueness through
  `ux_dki_baseline_experiment_measurement_tenant_code_active`, with
  `ix_dki_baseline_experiment_measurement_tenant_state` supporting tenant/state
  queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`baseline`,
  `experiment`, `measurement`, `catalog`, `design`, `binding`, `publication`,
  `review`) are not falsely rejected while true forbidden markers (measurement
  result, metric value, baseline/experiment sample, observation row, p-value,
  effect size, confidence interval, variant assignment, raw event, telemetry,
  subject/participant PII, individual attribution, `score`, `rating`, `rank`,
  `narrative`, PII, payload) are still caught. Field names are deliberately
  marker-safe (no `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and scorecard-source
  dependency fields are `MetricSemanticRegistrySourceDependencyState` and
  `ScorecardSourceDependencyState`), enforced by the contract field-name test.

Explicitly unauthorized:

- Real baseline/experiment measurement and statistical computation.
- Statistics engine and significance/lift computation.
- Measurement result or metric value materialization and persistence.
- Baseline/experiment sample, observation-row, and raw event/telemetry-data
  persistence.
- Statistical value (p-value / effect size / confidence interval) persistence.
- Variant/cohort assignment persistence.
- Subject/participant PII and individual-attribution persistence.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Author/admin experiment-authoring UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Measurement results or metric values, sample/observation rows, statistical
  values, variant assignments, raw event/telemetry data, subject/participant PII,
  individual attributions, or PII payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract, at
`/DataKnowledge/BaselineExperimentMeasurement`.

Authorized UX:

- DKI tenant shell nav entry `Baseline & Experiment Measurement`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Author experiment UX beyond readiness-metadata CRUD.
- Admin experiment/authoring administration UX beyond readiness-metadata CRUD.
- Baseline catalog, experiment design intake, measurement binding, result
  publication control, experiment review, scoring, or model-output UI.
- Statistics engine, experiment-execution, or results/significance viewer UI.
- Document, notification, export, upload, or attachment UI.
- Any measurement result or metric value, sample/observation row, statistical
  value, variant assignment, raw event/telemetry data, subject/participant PII, or
  individual-attribution viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5000` -> DKI `5062`).

Authorized API surface:

- `GET /api/baseline-experiment-measurement` - list readiness metadata.
- `GET /api/baseline-experiment-measurement/{id}` - get readiness metadata by id.
- `POST /api/baseline-experiment-measurement` - create readiness metadata.
- `POST /api/baseline-experiment-measurement/{id}/evaluate` - fail-closed
  evaluation.
- `DELETE /api/baseline-experiment-measurement/{id}` - soft-delete.
- `GET /api/baseline-experiment-measurement/{id}/audit-metadata` - audit metadata
  read.

The first slice must stay limited to:

- Create/list/get baseline/experiment measurement readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No real baseline/experiment measurement, statistical computation, measurement
  result or metric value persistence, sample/observation-row persistence,
  statistical-value persistence, variant-assignment persistence, raw
  event/telemetry-data persistence, scoring/model-output persistence, automated
  decision behavior, notification/document integration, or subject-participant-PII/
  individual-attribution/PII persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using a
  WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only baseline/experiment measurement readiness state.
- Boundary-state metadata for baseline catalog, experiment design intake,
  measurement binding scope, result publication control, experiment review,
  automated decision, stewardship, minimization, retention, and measurement
  policy.
- Dependency-state metadata for Metric & Semantic Registry Source
  (`MetricSemanticRegistrySourceDependencyState`), Scorecard Source
  (`ScorecardSourceDependencyState`), data contract registry, and notification
  dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Real baseline/experiment measurement or statistical-computation payload.
- Measurement result or metric value.
- Baseline/experiment sample or observation row.
- Statistical value (p-value, effect size, confidence interval, lift,
  significance).
- Variant/cohort assignment.
- Raw event/telemetry datum or record body or computed-metric output.
- Experiment-review narrative or review-note body.
- Readiness score/quality score/rating/rank/match confidence/model output.
- Automated decision result.
- Subject/participant PII or individual attribution.
- Connection string, credential/token/secret/password value.
- Attachment payload.
- Raw provider payload.
- PII-heavy dataset/record details.
- Compensation/payroll/target-value payload.

Data-plane note: this module is a **measurement-setup layer** over the metric
stack, and this slice declares readiness STATE only. No measurement result or
metric value, baseline/experiment sample or observation row, statistical value,
variant assignment, raw event/telemetry datum, subject/participant PII, or
individual attribution of any kind may cross the boundary into persistence. Only
boundary/readiness STATE metadata governed by stewardship precondition, data
minimization, retention, and measurement policy is permitted, keeping the strong
analytics-governance concerns (measurement-setup-metadata minimization, tenant
isolation of baseline/experiment entries, sharing/visibility scope of measurement
identity) resolved as metadata-only state.

## 12. Permission Boundary

Approved permission namespace:

- `dki.baseline-experiment-measurement.read`
- `dki.baseline-experiment-measurement.manage`
- `dki.baseline-experiment-measurement.evaluate`
- `dki.baseline-experiment-measurement.audit.read`

Runtime owner key: `dki.baseline-experiment-measurement`. The canonical module ID
`MOD-0062` is a governance/documentation identity and must not be used as a
runtime literal, owner key, or permission namespace fragment.

## 13. Tenant / Security Boundary

Runtime work is tenant-aware and fails closed. The backend/API slice resolves
tenant server-side and does not expose `TenantId` in request DTOs.

Security guardrails:

- Canonical identity (`MOD-0062`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_dki_baseline_experiment_measurement_tenant_code_active` and
  `ix_dki_baseline_experiment_measurement_tenant_state`.
- Because baselines/experiments may be partly platform-global (shared baseline
  templates) with tenant overlays and carry sharing/visibility scope, tenant
  isolation across measurement entries and their bound metric/semantic/scorecard/
  physical references is a first-class control even for readiness metadata; no
  measurement result, metric value, sample/observation row, statistical value,
  variant assignment, subject/participant PII, or individual attribution is
  persisted, and sharing/visibility scope of measurement identity remains a
  governed readiness concern.

## 14. Compliance / Privacy Boundary

Legal/privacy/stewardship is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only stewardship/precondition representation, with a valid stewardship
  basis treated as a precondition to activation (`StewardshipPreconditionState`).
- Data minimization fail-closed behavior (`DataMinimizationState`): store only
  readiness state; prefer aggregated/derived over measurement results, metric
  values, samples/observation rows, statistical values, variant assignments, or
  subject/participant PII.
- Retention and measurement policy as local/deferred metadata
  (`RetentionPolicyState`, `MeasurementPolicyState`), covering the defined
  lifecycle, purge policy, and measurement/publication posture for
  baseline/experiment measurement readiness.
- Exclusion of measurement results or metric values, baseline/experiment sample or
  observation rows, statistical values (p-values / effect sizes / confidence
  intervals), variant assignments, raw event/telemetry data, subject/participant
  PII, individual attributions, and any PII dataset.

The Baseline & Experiment Measurement capability declares readiness STATE only; it
does not own or store any measurement results or metric values, baseline/experiment
sample or observation rows, statistical values, variant assignments, raw
event/telemetry data, subject/participant PII, or individual attributions, and all
downstream analytics-backbone and consumer surfaces must treat its output as
boundary/readiness metadata, not as a measurement-result, metric-value, sample, or
statistics source.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, or document/
notification UI is authorized. `MOD-0060 Metric Definitions & Ownership` is
consumed only as upstream reference/dependency-state context; `MOD-0004 Metric &
Semantic Registry` is consumed as `MetricSemanticRegistrySourceDependencyState`,
`MOD-0061 Scorecards / Dashboards` as `ScorecardSourceDependencyState`, and
`MOD-0063 Data Warehouse / Lakehouse` as physical-plane reference context; data
contract registry and notification are consumed only as dependency-state context;
downstream surfaces are consumer references; and ESBP goal-scoped experiments and
HCM `CAND-CAP-0034` remain non-owning consumers.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `runtime-authored`.
2. AC-02: Canonical identity is `MOD-0062` (Blueprint-backed; used directly, not
   a candidate) as **Baseline & Experiment Measurement**.
3. AC-03: Runtime owner/key is `dki.baseline-experiment-measurement`.
4. AC-04: Permission namespace is limited to
   `dki.baseline-experiment-measurement.read`,
   `dki.baseline-experiment-measurement.manage`,
   `dki.baseline-experiment-measurement.evaluate`, and
   `dki.baseline-experiment-measurement.audit.read`.
5. AC-05: Runtime repo scope is limited to
   `services/Diten.DataKnowledgeService/**`; owning service is
   `Diten.DataKnowledgeService` (port `5062`, Mongo db `DitenDataKnowledge`).
6. AC-06: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3), exposed through the gateway route
   `/api/baseline-experiment-measurement` (`5000` -> DKI `5062`).
7. AC-07: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
8. AC-08: Mongo persistence (collection
   `dki_baseline_experiment_measurement_readiness`) includes active tenant `Code`
   uniqueness (`ux_dki_baseline_experiment_measurement_tenant_code_active`),
   tenant/state indexing (`ix_dki_baseline_experiment_measurement_tenant_state`),
   soft delete, and tenant isolation (E4).
9. AC-09: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted records.
10. AC-10: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
11. AC-11: RBAC is enforced server-side across the controller surface.
12. AC-12: Analytics-backbone dependency boundaries are recorded (`MOD-0060`
    Metric Definitions & Ownership as upstream measured definitions; `MOD-0004` as
    upstream semantic vocabulary via `MetricSemanticRegistrySourceDependencyState`;
    `MOD-0061` as composition plane via `ScorecardSourceDependencyState`; `MOD-0063`
    as physical-plane reference; data contract registry and notification as
    dependency-state context; downstream surfaces, ESBP goal-scoped experiments,
    and HCM `CAND-CAP-0034` as non-owning consumers). `MOD-0062` completes the
    analytics backbone (`MOD-0004`, `MOD-0059`–`MOD-0064`).
13. AC-13: Real baseline/experiment measurement, statistical computation,
    measurement result or metric value persistence, baseline/experiment sample or
    observation-row persistence, statistical-value (p-value / effect size /
    confidence interval) persistence, variant-assignment persistence, raw
    event/telemetry-data persistence, subject/participant PII / individual-attribution
    persistence, model output, and automated decision behavior remain unauthorized.
14. AC-14: Author/admin experiment fields remain reserved/out of scope; no money
    field is present; no measurement result or metric value, baseline/experiment
    sample or observation row, statistical value, variant assignment, raw
    event/telemetry datum, subject/participant PII, or individual attribution field
    is present.
15. AC-15: Measurement results or metric values, baseline/experiment sample or
    observation rows, statistical values (p-values / effect sizes / confidence
    intervals), variant assignments, raw event/telemetry data, subject/participant
    PII, individual attributions, scores/ratings/ranks,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized; field names are marker-safe (no
    `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
    fragment in any property name; the registry-source and scorecard-source
    dependency fields are `MetricSemanticRegistrySourceDependencyState` and
    `ScorecardSourceDependencyState`) and the field-name test passes under the
    `\b` word-boundary matcher.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: `tr`/`en` localization parity is present.
18. AC-18: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
19. AC-19: Runtime literal scan for `MOD-0062` passes under
    runtime/frontend/gateway/test paths (module IDs are governance-only).

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: runtime-authored`.
- Confirm all 20 sections are present.
- Build DKI API:
  `dotnet build services/Diten.DataKnowledgeService/src/Diten.DataKnowledgeService.Api/Diten.DataKnowledgeService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `BaselineExperimentMeasurement` filter
  (`Application.Tests/BaselineExperimentMeasurementTests.cs`, handler behavior;
  fix-absent should be RED).
- Run full DKI Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_dki_baseline_experiment_measurement_tenant_code_active`), and tenant/state
  indexing (`ix_dki_baseline_experiment_measurement_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex), not
  bare substring `Contains`, so legitimate words (`baseline`, `experiment`,
  `measurement`, `catalog`, `design`, `binding`, `publication`, `review`) are not
  falsely rejected.
- Verify the contract field-name test confirms marker-safe field naming (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and scorecard-source
  dependency fields are `MetricSemanticRegistrySourceDependencyState` and
  `ScorecardSourceDependencyState`).
- Verify forbidden baseline-catalog/experiment-design-intake/measurement-binding/
  result-publication/review/decision/sensitive fields are absent.
- Verify author/admin experiment fields are reserved/absent, no money field
  exists, and no measurement result or metric value, baseline/experiment sample or
  observation row, statistical value, variant assignment, raw event/telemetry
  datum, subject/participant PII, or individual attribution field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "MOD-0062" services frontend gateway`
  returns no runtime literal (module IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a runtime-authored slice.
- [x] Blueprint-canonical identity accepted (`MOD-0062`, used directly) as
  Baseline & Experiment Measurement.
- [x] Candidate gate script not-executable note is accepted.
- [x] EA runtime authorization accepted: domain `data-knowledge-intelligence` and
  dedicated data-plane service `Diten.DataKnowledgeService` assigned per
  `WP-DKI-SLICE-0062`.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope is
  accepted.
- [x] Real baseline/experiment measurement and statistical computation prohibition
  is accepted.
- [x] Measurement result or metric value / baseline-experiment sample /
  observation row / statistical value / variant assignment / raw event-telemetry
  data persistence prohibition is accepted.
- [x] Subject/participant PII / individual-attribution persistence prohibition is
  accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved author/admin experiment fields, no-money-field, and
  no-measurement-result/metric-value/sample/observation-row/statistical-value/
  variant-assignment/subject-participant-PII/individual-attribution constraints are
  accepted.
- [x] Sensitive/PII payload prohibition is accepted.
- [x] Marker-safe field naming (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; registry-source and scorecard-source dependency
  fields `MetricSemanticRegistrySourceDependencyState`/`ScorecardSourceDependencyState`)
  with `\b` word-boundary matcher is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Analytics-backbone (`MOD-0004`, `MOD-0059`–`MOD-0061`, `MOD-0063`, `MOD-0064`),
  ESBP, and HCM dependency/consumer context is accepted; `MOD-0062` completes the
  backbone.

## 19. Runtime-Ready Checklist

- [x] EA runtime authorization / canonical MOD runtime assignment.
- [x] Runtime owner/key decision (`dki.baseline-experiment-measurement`).
- [x] Permission namespace decision.
- [x] Runtime repo scope decision (`services/Diten.DataKnowledgeService/**`).
- [x] Minimal metadata-only baseline/experiment measurement readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/stewardship metadata-only waiver.
- [x] Data minimization policy.
- [x] Retention / measurement / evidence / deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend and
  gateway route are scoped under `services/Diten.DataKnowledgeService/**` and the
  owned `frontend/Diten.Web/**` BaselineExperimentMeasurement objects.
- Runtime owner/key remains limited to `dki.baseline-experiment-measurement`.
- Permission namespace remains limited to
  `dki.baseline-experiment-measurement.read`,
  `dki.baseline-experiment-measurement.manage`,
  `dki.baseline-experiment-measurement.evaluate`, and
  `dki.baseline-experiment-measurement.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime public/persisted
  contract and verified against `BaselineExperimentMeasurementReadinessMetadata.cs`.
- `MetricSemanticRegistrySourceDependencyState`, `ScorecardSourceDependencyState`,
  `DataContractRegistryDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states; no property name carries a
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment.
- Author/admin experiment UX fields remain reserved/out of scope, no money field
  is present, and no measurement result or metric value, baseline/experiment sample
  or observation row, statistical value, variant assignment, raw event/telemetry
  datum, subject/participant PII, or individual attribution field is present.
- ESBP, HCM, and Platform scopes remain closed.
- Real baseline/experiment measurement, statistical computation, measurement
  result or metric value persistence, sample/observation-row persistence,
  statistical-value persistence, variant-assignment persistence, raw
  event/telemetry-data persistence, model output, automated decision behavior,
  notification/document integration, subject/participant PII, individual
  attributions, and PII-heavy persistence remain out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0062`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted the runtime-authorization revert of the prior
  governance-first deferral: the DCP-008 "Analytics backbone — Pending EA domain
  mapping" note is resolved by assigning domain `data-knowledge-intelligence` and
  the dedicated data-plane service `Diten.DataKnowledgeService` per
  `WP-DKI-SLICE-0062`, rather than defaulting to `Diten.EnterpriseStrategyService`
  or `Diten.Platform`.
- EA/registry owner accepted verifier absence as a governance note.
- EA runtime policy waiver approved for the first metadata-only backend/API
  readiness slice (K19-complete), with golden-compact readiness CRUD frontend and
  gateway route in scope. `MOD-0062` is Blueprint-canonical and its DCP-002
  canonical registry row is reserved and used directly (no candidate identity).
  The DCP-008 reservation language (registry row + module pack required before
  runtime) is satisfied and superseded by this runtime authorization. `MOD-0062`
  is the final module of the analytics backbone (`MOD-0004`, `MOD-0059`–`MOD-0064`);
  this slice completes the backbone's runtime authorization coverage.
- Legal/privacy/stewardship metadata-only waiver approved.
- Data minimization fail-closed policy approved.
- Retention / measurement / evidence / deletion local/deferred metadata policy
  approved.
- Marker-safe field-naming waiver approved: field names deliberately avoid
  forbidden-marker fragments (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and scorecard-source
  dependency fields are `MetricSemanticRegistrySourceDependencyState` and
  `ScorecardSourceDependencyState`), enforced by the contract field-name test
  using the `\b` word-boundary matcher.

Runtime literal scan:

- Required command:
  `rg -n "MOD-0062" services frontend gateway`
- Module IDs are governance-only and must return no runtime literal match; the
  runtime uses owner key `dki.baseline-experiment-measurement` and gateway route
  `/api/baseline-experiment-measurement`, never the MOD ID.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real baseline/experiment measurement and statistical computation.
- Statistics engine and significance/lift computation runtime.
- Measurement result / metric value materialization and evaluation.
- Baseline/experiment sample and observation-row handling.
- Variant/cohort assignment and subject segmentation.
- Result publication / sharing distribution and visibility-scope enforcement.
- Semantic-binding execution against `MOD-0004` vocabulary.
- Metric-definition measurement against `MOD-0060`.
- Scorecard/dashboard surfacing against `MOD-0061`.
- Physical measurement-data read against `MOD-0063` warehouse models.
- Automated decision approval.
- Author/admin baseline/experiment authoring UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
