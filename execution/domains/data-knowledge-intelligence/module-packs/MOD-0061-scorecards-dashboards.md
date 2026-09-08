---
id: MOD-0061
name: Scorecards / Dashboards
domain: data-knowledge-intelligence
blueprint_domain: Data, Knowledge & Intelligence
service: Diten.DataKnowledgeService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: runtime-authored
owner: enterprise-architect / dki-domain-owner / data-platform-owner / analytics-owner / platform-team / security-owner
branch: feature/dki/mod-0061-scorecards-dashboards
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
runtime_owner_key: dki.scorecards-dashboards
permission_namespace:
  - dki.scorecards-dashboards.read
  - dki.scorecards-dashboards.manage
  - dki.scorecards-dashboards.evaluate
  - dki.scorecards-dashboards.audit.read
runtime_repo_scope: services/Diten.DataKnowledgeService/**
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0061 - Scorecards / Dashboards

> Status: runtime-authored. This pack records the first metadata-only backend/API
> scorecard/dashboard composition readiness contract slice under
> `services/Diten.DataKnowledgeService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5000` -> DKI `5062`). `MOD-0061` is **Blueprint-canonical** (Blueprint
> domain **Data, Knowledge & Intelligence**); the canonical MOD ID is used
> directly and remains a governance/documentation identity that must not be
> written into runtime literals. The governance-first runtime deferral recorded in
> the prior draft is **REVERTED**: per `WP-DKI-SLICE-0061`, EA resolved the
> DCP-008 "Analytics backbone — Pending EA domain mapping" note by assigning the
> repo domain `data-knowledge-intelligence` and the dedicated data-plane runtime
> service `Diten.DataKnowledgeService` (namespace `Diten.DataKnowledgeService.*`,
> port `5062`, Mongo db `DitenDataKnowledge`, permission owner key `dki.*`). Real
> scorecard/dashboard composition/authoring, chart/visual rendering, widget/tile
> data or query results, chart series or measure values, rendered visual payloads,
> image/export blobs, embedded dataset rows, viewer/owner PII, individual
> attributions, model output or automated decision behavior, and sensitive/PII-heavy
> persistence remain closed.

## 1. Module Summary

`MOD-0061` is the Blueprint-canonical **Scorecards / Dashboards**: the
**composition and presentation layer** of the analytics backbone — the readiness
layer for how curated KPIs and metric definitions are *arranged*, *bound*, and
*published* into scorecards and dashboards: scorecard/dashboard identity, catalog
scope, widget-binding intake, layout scope, publication control, and dashboard
review. It is a **composition/presentation (governance) layer**, not a rendering,
computation, or reporting engine: it declares the readiness STATE of
*scorecard/dashboard composition metadata* and never holds widget/tile data or
query results, chart series or measure values, rendered visual payloads,
image/export blobs, embedded dataset rows, viewer/owner PII, or individual
attributions. It composes *references* to already-owned analytics concepts and is
explicitly **not** a chart-rendering engine.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and originally
marked its domain ownership as "Pending EA domain mapping" with no existing
service. That deferral is now resolved: EA assigned the repo domain
`data-knowledge-intelligence` and reused the dedicated data-plane runtime service
(`Diten.DataKnowledgeService`) to own this cross-cutting composition/presentation
capability, rather than defaulting it to `Diten.EnterpriseStrategyService` or
`Diten.Platform`. This pack records the first metadata-only backend/API readiness
contract slice authored under `WP-DKI-SLICE-0061`, mirroring the completed
readiness slices exactly.

The Scorecards / Dashboards capability is the readiness layer for the composition
plane that arranges curated analytics concepts into scorecard and dashboard
surfaces: it declares the readiness STATE of the scorecard catalog, the
widget-binding intake boundary, layout scope, publication control, dashboard
review, and the automated-decision boundary. It sits in the analytics-backbone
group (`MOD-0004`, `MOD-0059`–`MOD-0064`) directly downstream of KPI and metric
ownership: it composes KPIs from `MOD-0059 KPI Catalog` and definitions from
`MOD-0060 Metric Definitions & Ownership` over `MOD-0004 Metric & Semantic
Registry` semantics and `MOD-0063 Data Warehouse / Lakehouse` storage, holding
only composition metadata and tile/widget references — never the underlying
values. Real scorecard/dashboard composition/authoring, chart/visual rendering,
widget/tile data or query results, chart series or measure values, rendered
visual payloads, image/export blobs, embedded dataset rows, viewer/owner PII,
individual attributions, model output, automated decision behavior, and
sensitive/PII-heavy persistence remain closed.

## 2. Ownership and Boundaries

`MOD-0061` is Blueprint-canonical and is used directly as the canonical
**Scorecards / Dashboards** identity; no candidate identity and no name drift
apply to this ID.

Owned by this pack:

- DKI-native scorecard/dashboard composition governance boundary.
- Sequencing as the composition/presentation edge of the analytics-backbone group
  (`MOD-0004`, `MOD-0059`–`MOD-0064`), composing KPIs (`MOD-0059`) and definitions
  (`MOD-0060`) over `MOD-0004` semantics and `MOD-0063` storage.
- Metadata-only scorecard/dashboard composition readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the scorecard catalog, widget-binding intake,
  layout scope, publication control, and dashboard review, consumed by downstream
  analytics-backbone and consumer surfaces.
- Explicit exclusion of real scorecard/dashboard composition/authoring and
  chart/visual rendering.
- Explicit exclusion of widget/tile data or query results, chart series or measure
  value materialization, scoring, model output, and automated decision behavior.
- Explicit exclusion of author-facing and admin-facing composition-authoring UX
  beyond the readiness-metadata CRUD (those fields are reserved and out of scope).
- Explicit exclusion of widget/tile data or query results, chart series or measure
  values, rendered visual payloads, image/export blobs, embedded dataset rows,
  viewer/owner PII, individual attributions, free-text, and any PII dataset
  persistence.

Not owned by this pack:

- Curated KPIs — owned by `MOD-0059 KPI Catalog`, referenced here by readiness
  state only.
- Metric definitions, formulas, thresholds, and ownership — owned by `MOD-0060
  Metric Definitions & Ownership`, referenced here by readiness state only.
- The shared semantic vocabulary (metric identity, semantic type, unit, grain,
  aggregation semantics) — owned by `MOD-0004 Metric & Semantic Registry`, bound
  here by readiness state only.
- Baselines / experiments — owned by `MOD-0062 Baseline & Experiment
  Measurement`.
- Physical storage, query, and physical binding — owned by `MOD-0063 Data
  Warehouse / Lakehouse`, referenced here by readiness state only.
- Data movement / transformation — owned by `MOD-0064 ETL / ELT Pipelines`.
- The actual chart/visual rendering runtime — `MOD-0061` composes references and
  is explicitly **not** a rendering engine.
- Data dictionary and data contract registry ownership beyond dependency/context
  references.
- Secrets/vault, notification, and document ownership beyond dependency/context
  references.
- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped scorecards — a **distinct**
  consumer surface separate from `MOD-0061`'s general analytics-backbone
  scorecards/dashboards; ESBP is a consumer of composition concepts, not the owner
  of this data plane.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` and other facades — consumers
  that own no analytics runtime.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| ScorecardsDashboardsCapabilityBoundary | Governance boundary | Defines what the DKI scorecard/dashboard composition module may own. |
| ScorecardsDashboardsReadinessBoundary | Governance boundary | Separates readiness metadata from composition/authoring and rendering. |
| ScorecardCatalogBoundary | Governance boundary | Blocks widget/tile data and query-result persistence in the scorecard catalog. |
| WidgetBindingIntakeBoundary | Governance boundary | Blocks chart series/measure value and rendered visual payload persistence at widget-binding intake. |
| LayoutScopeBoundary | Governance boundary | Blocks rendered visual payload, image, and export-blob persistence in layout scope. |
| PublicationControlBoundary | Governance boundary | Blocks viewer/owner PII and publication-decision content persistence in the publication lifecycle. |
| DashboardReviewBoundary | Governance boundary | Blocks free-text review narrative and review-note body persistence. |
| ScorecardsDashboardsDecisionBoundary | Governance boundary | Blocks scoring, ranking, model-output, and automated decision persistence. |
| ScorecardsDashboardsUxBoundary | Governance boundary | Blocks author/admin composition-authoring UX beyond readiness-metadata CRUD. |
| ScorecardsDashboardsSensitiveDataBoundary | Governance boundary | Blocks widget/tile data or query results, chart series or measure values, rendered visual payloads, image/export blobs, embedded dataset rows, viewer/owner PII, individual attributions, and PII persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| ScorecardCompositionEngine | Deferred runtime | Real scorecard/dashboard composition/authoring and composition-body management; not authorized. |
| WidgetRenderingEngine | Deferred runtime | Chart/visual rendering, widget/tile data materialization, and chart-series production; not authorized. |
| MeasureValueMaterializationEngine | Deferred runtime | Measure/metric value materialization and evaluation; not authorized. |
| DashboardPublicationEngine | Deferred runtime | Publication/sharing distribution and viewer resolution; not authorized. |
| ScorecardsDashboardsScoringDecisionEngine | Deferred runtime | Scoring, ranking, model-output, and automated decision behavior; not authorized. |
| AuthorAdminDashboardExperience | Deferred frontend/runtime | Author/admin composition-authoring UX beyond readiness CRUD; not authorized. |
| ScorecardsDashboardsDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| ScorecardsDashboardsNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| ScorecardsDashboardsReadinessMetadata | Domain entity | Metadata-only readiness contract; `src/Diten.DataKnowledgeService.Domain/Entities/ScorecardsDashboardsReadinessMetadata.cs`. |
| ScorecardsDashboardsReadinessState | Domain enum | Readiness state; `src/Diten.DataKnowledgeService.Domain/Enums/ScorecardsDashboardsReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IScorecardsDashboardsReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `src/Diten.DataKnowledgeService.Domain/Repositories/IScorecardsDashboardsReadinessMetadataRepository.cs`. |
| MongoScorecardsDashboardsReadinessMetadataRepository | Persistence repository | Mongo-backed repository (db `DitenDataKnowledge`); collection `dki_scorecards_dashboards_readiness`; indexes `ux_dki_scorecards_dashboards_tenant_code_active` and `ix_dki_scorecards_dashboards_tenant_state`. |
| ScorecardsDashboards Application features | Application (CQRS/MediatR) | `src/Diten.DataKnowledgeService.Application/Features/ScorecardsDashboards/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| ScorecardsDashboardsController | API controller | Thin controller; `src/Diten.DataKnowledgeService.Api/Controllers/Dki/ScorecardsDashboardsController.cs`. |
| ScorecardsDashboards golden-compact CRUD | Frontend | `Controllers/ScorecardsDashboardsController.cs` + `Views/DataKnowledge/ScorecardsDashboards/**` + `Models/DataKnowledge/ScorecardsDashboards/**` + `wwwroot/assets/js/DataKnowledge/ScorecardsDashboards/**` + Resources + DKI nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract, verified against
`services/Diten.DataKnowledgeService/src/Diten.DataKnowledgeService.Domain/Entities/ScorecardsDashboardsReadinessMetadata.cs`:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `ScorecardsDashboardsReadinessState`):

- `Code`
- `DisplayName`
- `ScorecardsDashboardsReadinessState`
- `ScorecardCatalogBoundaryState`
- `WidgetBindingIntakeBoundaryState`
- `LayoutScopeBoundaryState`
- `PublicationControlBoundaryState`
- `DashboardReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `MetricSemanticRegistrySourceDependencyState`
- `DataWarehouseSourceDependencyState`
- `DataContractRegistryDependencyState`
- `NotificationDependencyState`
- `StewardshipPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `PublicationPolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `ScorecardsDashboardsReadinessVersion`
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
referencing `MOD-0004` rather than embedding a `series`/`measure`/`chart` marker),
and the physical-plane dependency field is named `DataWarehouseSourceDependencyState`
(using `DataWarehouseSource*`, referencing `MOD-0063` rather than embedding a
`dataset`/`query` marker), so neither collides with the forbidden marker class.
The forbidden-marker matcher uses a word/token boundary (`\b` regex), not a bare
substring `Contains`, so legitimate domain words (`scorecard`, `dashboard`,
`catalog`, `widget`, `layout`, `publication`) are not falsely rejected while true
forbidden markers (widget/tile data, query result, chart series, measure value,
rendered visual payload, image/export blob, embedded dataset row, viewer/owner
PII, individual attribution, `score`, `rating`, `rank`, `narrative`, PII, payload)
are still caught.

Reserved / out-of-scope fields (must not be added in this slice):

- Author-facing composition fields (author composition submission, author review,
  author action UX state) are reserved and out of scope.
- Admin-facing composition fields (admin composition/authoring administration,
  admin publication, admin self-service UX state) are reserved and out of scope.
- No money field (salary/compensation/cost/budget/target-value). This is a
  metadata-only readiness slice with no monetary attribute.
- No widget/tile data or query results, chart series or measure value, rendered
  visual payload, image/export blob, embedded dataset row, viewer/owner PII,
  individual attribution, or PII dataset field of any kind.

Forbidden field classes:

- Widget/tile data or query results, chart series, measure value, dataset
  row/record body, rendered visual payload, or completed composition-output
  content.
- Readiness score, quality score, rating value, rank, match confidence, model
  output, automated decision result, recommendation output, or evaluator scoring
  payload.
- Viewer/owner PII, individual attributions, free-text composition/review
  narrative, HR notes, or sensitive data body.
- Image/export blob, rendered visual payload, attachment payload, controlled
  document body, external document content, raw evidence, raw provider payload, or
  uploaded file metadata.
- Connection string, credential, token, secret, password, activation code,
  provider connection value, device secret, or account bootstrap secret.
- Salary amount, compensation amount, target/threshold amount, benefits election,
  payroll details, tax details, bank details, or payment instruction fields.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  any PII-heavy dataset/record field.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0061-scorecards-dashboards.md`
- The `MOD-0061` row in `execution/registries/module-id-registry.md`.

Runtime service:

- `Diten.DataKnowledgeService` (namespace `Diten.DataKnowledgeService.*`, port
  `5062`, Mongo db `DitenDataKnowledge`).

Runtime repo scope:

- `services/Diten.DataKnowledgeService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (`Controllers/ScorecardsDashboardsController.cs`,
  `Views/DataKnowledge/ScorecardsDashboards/**`,
  `Models/DataKnowledge/ScorecardsDashboards/**`,
  `wwwroot/assets/js/DataKnowledge/ScorecardsDashboards/**`, Resources, DKI nav
  entry at `/DataKnowledge/ScorecardsDashboards`).
- Gateway route `/api/scorecards-dashboards` exposure through the API gateway
  (`5000` -> DKI `5062`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.DataKnowledgeService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned ScorecardsDashboards CRUD objects listed in
  §3/§5.
- `gateway/**` outside the owned `/api/scorecards-dashboards` readiness route.
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.Platform/**`
- `services/Diten.HumanCapitalService/**`
- global `tests/**`
- the other analytics-backbone module packs (`MOD-0004`, `MOD-0059`, `MOD-0060`,
  `MOD-0062`–`MOD-0064`).
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/data-knowledge-intelligence/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Analytics-backbone relationships:

- Depends on: `MOD-0059 KPI Catalog` — `MOD-0061` tiles/widgets reference curated
  KPIs; its availability is treated as boundary/readiness metadata context, never
  the KPI values themselves.
- Depends on: `MOD-0060 Metric Definitions & Ownership` — `MOD-0061` tiles/widgets
  reference concrete metric definitions; its availability is treated as
  boundary/readiness metadata context, never the definition bodies themselves.
- Depends on (upstream vocabulary): `MOD-0004 Metric & Semantic Registry` — every
  referenced KPI/definition binds to the shared semantic vocabulary authored
  there; its availability is treated as boundary/readiness metadata context,
  consumed via `MetricSemanticRegistrySourceDependencyState`, never the vocabulary
  body itself.
- Depends on (physical plane): `MOD-0063 Data Warehouse / Lakehouse` — the
  physical models/binding backing referenced KPIs/definitions live there; its
  availability is treated as boundary/readiness metadata context, consumed via
  `DataWarehouseSourceDependencyState`, never the physical data itself.
- Related physical plane: `MOD-0064 ETL / ELT Pipelines` feeds `MOD-0063`;
  referenced as context only.
- Consumed by: `MOD-0062 Baseline & Experiment Measurement` and other downstream
  surfaces — consumption is boundary/readiness metadata context only.

Runtime dependency-state context (metadata-only, via boundary/dependency states):

- Metric & Semantic Registry Source (the `MOD-0004` shared semantic vocabulary),
  consumed via `MetricSemanticRegistrySourceDependencyState` (field named
  `MetricSemanticRegistrySource*`, marker-safe). Registry-source availability is a
  controlling precondition and is treated as boundary/readiness metadata only; no
  metric series, measure value, or definition body is persisted.
- Data Warehouse Source (the `MOD-0063` physical plane), consumed via
  `DataWarehouseSourceDependencyState` (field named `DataWarehouseSource*`).
  Warehouse-source availability is a controlling precondition; no dataset row or
  query result is persisted.
- Data Contract Registry, consumed via `DataContractRegistryDependencyState`.
- Notification, consumed via `NotificationDependencyState`.

Consumer context (not dependencies of this module, they depend on / consume it):

- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped scorecard runtime — a
  distinct consumer surface, non-owning.
- HCM `CAND-CAP-0034` HR KPI & Analytics Facade — a consumer facade, non-owning.

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is authorized
only for the first metadata-only backend/API readiness slice, along with the
golden-compact readiness CRUD frontend and gateway route.

Authorized runtime boundary:

- Runtime owner/key: `dki.scorecards-dashboards`.
- Permission namespace:
  - `dki.scorecards-dashboards.read`
  - `dki.scorecards-dashboards.manage`
  - `dki.scorecards-dashboards.evaluate`
  - `dki.scorecards-dashboards.audit.read`
- Runtime repo scope: `services/Diten.DataKnowledgeService/**`.
- Thin controller
  (`src/Diten.DataKnowledgeService.Api/Controllers/Dki/ScorecardsDashboardsController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository (db `DitenDataKnowledge`); collection
  `dki_scorecards_dashboards_readiness`.
- Active tenant `Code` uniqueness through
  `ux_dki_scorecards_dashboards_tenant_code_active`, with
  `ix_dki_scorecards_dashboards_tenant_state` supporting tenant/state queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`scorecard`,
  `dashboard`, `catalog`, `widget`, `layout`, `publication`) are not falsely
  rejected while true forbidden markers (widget/tile data, query result, chart
  series, measure value, rendered visual payload, image/export blob, embedded
  dataset row, viewer/owner PII, individual attribution, `score`, `rating`,
  `rank`, `narrative`, PII, payload) are still caught. Field names are
  deliberately marker-safe (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and warehouse-source
  dependency fields are `MetricSemanticRegistrySourceDependencyState` and
  `DataWarehouseSourceDependencyState`), enforced by the contract field-name test.

Explicitly unauthorized:

- Real scorecard/dashboard composition/authoring.
- Chart/visual rendering and widget/tile data materialization.
- Chart series or measure value materialization and persistence.
- Query-result persistence.
- Embedded dataset-row persistence.
- Rendered visual payload, image, and export-blob persistence.
- Viewer/owner PII and individual-attribution persistence.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Author/admin composition-authoring UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Widget/tile data or query results, chart series or measure values, rendered
  visual payloads, image/export blobs, embedded dataset rows, viewer/owner PII,
  individual attributions, or PII payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract, at `/DataKnowledge/ScorecardsDashboards`.

Authorized UX:

- DKI tenant shell nav entry `Scorecards / Dashboards`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Author composition UX beyond readiness-metadata CRUD.
- Admin composition/authoring administration UX beyond readiness-metadata CRUD.
- Scorecard catalog, widget binding, layout, publication control, dashboard
  review, scoring, or model-output UI.
- Chart/visual rendering, dashboard viewer, or tile/widget preview UI.
- Document, notification, export, upload, or attachment UI.
- Any widget/tile data or query results, chart series or measure value, rendered
  visual payload, image/export blob, or embedded dataset-row viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5000` -> DKI `5062`).

Authorized API surface:

- `GET /api/scorecards-dashboards` - list readiness metadata.
- `GET /api/scorecards-dashboards/{id}` - get readiness metadata by id.
- `POST /api/scorecards-dashboards` - create readiness metadata.
- `POST /api/scorecards-dashboards/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/scorecards-dashboards/{id}` - soft-delete.
- `GET /api/scorecards-dashboards/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get scorecard/dashboard composition readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No scorecard/dashboard composition/authoring, chart/visual rendering, widget/tile
  data or query-result persistence, chart series or measure value materialization,
  embedded dataset-row persistence, rendered visual payload / image / export-blob
  persistence, scoring/model-output persistence, automated decision behavior,
  notification/document integration, or viewer-owner-PII/individual-attribution/PII
  persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using a
  WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only scorecard/dashboard composition readiness state.
- Boundary-state metadata for scorecard catalog, widget-binding intake, layout
  scope, publication control, dashboard review, automated decision, stewardship,
  minimization, retention, and publication policy.
- Dependency-state metadata for Metric & Semantic Registry Source
  (`MetricSemanticRegistrySourceDependencyState`), Data Warehouse Source
  (`DataWarehouseSourceDependencyState`), data contract registry, and notification
  dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Scorecard/dashboard composition/authoring payload.
- Chart series or measure value.
- Widget/tile data or query result.
- Rendered visual payload, image, or export blob.
- Embedded dataset row / record body or computed-metric output.
- Dashboard-review narrative or review-note body.
- Readiness score/quality score/rating/rank/match confidence/model output.
- Automated decision result.
- Viewer/owner PII or individual attribution.
- Connection string, credential/token/secret/password value.
- Attachment payload.
- Raw provider payload.
- PII-heavy dataset/record details.
- Compensation/payroll/target-value payload.

Data-plane note: this module is a **composition/presentation layer** over the
metric stack, and this slice declares readiness STATE only. No widget/tile data or
query result, chart series or measure value, rendered visual payload, image/export
blob, embedded dataset row, viewer/owner PII, or individual attribution of any
kind may cross the boundary into persistence. Only boundary/readiness STATE
metadata governed by stewardship precondition, data minimization, retention, and
publication policy is permitted, keeping the strong analytics-governance concerns
(composition-metadata minimization, tenant isolation of scorecard/dashboard
entries, sharing/visibility scope of composition identity) resolved as
metadata-only state.

## 12. Permission Boundary

Approved permission namespace:

- `dki.scorecards-dashboards.read`
- `dki.scorecards-dashboards.manage`
- `dki.scorecards-dashboards.evaluate`
- `dki.scorecards-dashboards.audit.read`

Runtime owner key: `dki.scorecards-dashboards`. The canonical module ID `MOD-0061`
is a governance/documentation identity and must not be used as a runtime literal,
owner key, or permission namespace fragment.

## 13. Tenant / Security Boundary

Runtime work is tenant-aware and fails closed. The backend/API slice resolves
tenant server-side and does not expose `TenantId` in request DTOs.

Security guardrails:

- Canonical identity (`MOD-0061`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_dki_scorecards_dashboards_tenant_code_active` and
  `ix_dki_scorecards_dashboards_tenant_state`.
- Because scorecards/dashboards may be partly platform-global (shared baseline
  compositions) with tenant overlays and carry sharing/visibility scope, tenant
  isolation across composition entries and their bound KPI/definition/semantic/
  physical references is a first-class control even for readiness metadata; no
  widget/tile data, query result, chart series, measure value, rendered visual
  payload, viewer/owner PII, or individual attribution is persisted, and
  sharing/visibility scope of composition identity remains a governed readiness
  concern.

## 14. Compliance / Privacy Boundary

Legal/privacy/stewardship is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only stewardship/precondition representation, with a valid stewardship
  basis treated as a precondition to activation (`StewardshipPreconditionState`).
- Data minimization fail-closed behavior (`DataMinimizationState`): store only
  readiness state; prefer aggregated/derived over widget/tile data, query results,
  chart series, measure values, or viewer/owner PII.
- Retention and publication policy as local/deferred metadata
  (`RetentionPolicyState`, `PublicationPolicyState`), covering the defined
  lifecycle, purge policy, and publication/sharing posture for scorecard/dashboard
  composition readiness.
- Exclusion of widget/tile data or query results, chart series or measure values,
  rendered visual payloads, image/export blobs, embedded dataset rows,
  viewer/owner PII, individual attributions, and any PII dataset.

The Scorecards / Dashboards capability declares readiness STATE only; it does not
own or store any widget/tile data or query results, chart series or measure
values, rendered visual payloads, image/export blobs, embedded dataset rows,
viewer/owner PII, or individual attributions, and all downstream analytics-backbone
and consumer surfaces must treat its output as boundary/readiness metadata, not as
a composition-body, measure-value, or rendered-visual source.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, or document/
notification UI is authorized. `MOD-0059 KPI Catalog` and `MOD-0060 Metric
Definitions & Ownership` are consumed only as upstream reference/dependency-state
context; `MOD-0004 Metric & Semantic Registry` is consumed as
`MetricSemanticRegistrySourceDependencyState` and `MOD-0063 Data Warehouse /
Lakehouse` as `DataWarehouseSourceDependencyState`; data contract registry and
notification are consumed only as dependency-state context; `MOD-0062` and other
downstream surfaces are consumer references; and ESBP goal-scoped scorecards and
HCM `CAND-CAP-0034` remain non-owning consumers.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `runtime-authored`.
2. AC-02: Canonical identity is `MOD-0061` (Blueprint-backed; used directly, not
   a candidate) as **Scorecards / Dashboards**.
3. AC-03: Runtime owner/key is `dki.scorecards-dashboards`.
4. AC-04: Permission namespace is limited to `dki.scorecards-dashboards.read`,
   `dki.scorecards-dashboards.manage`, `dki.scorecards-dashboards.evaluate`, and
   `dki.scorecards-dashboards.audit.read`.
5. AC-05: Runtime repo scope is limited to
   `services/Diten.DataKnowledgeService/**`; owning service is
   `Diten.DataKnowledgeService` (port `5062`, Mongo db `DitenDataKnowledge`).
6. AC-06: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3), exposed through the gateway route
   `/api/scorecards-dashboards` (`5000` -> DKI `5062`).
7. AC-07: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
8. AC-08: Mongo persistence (collection `dki_scorecards_dashboards_readiness`)
   includes active tenant `Code` uniqueness
   (`ux_dki_scorecards_dashboards_tenant_code_active`), tenant/state indexing
   (`ix_dki_scorecards_dashboards_tenant_state`), soft delete, and tenant isolation
   (E4).
9. AC-09: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted records.
10. AC-10: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
11. AC-11: RBAC is enforced server-side across the controller surface.
12. AC-12: Analytics-backbone dependency boundaries are recorded (`MOD-0059` KPI
    Catalog and `MOD-0060` Metric Definitions & Ownership as upstream composed
    references; `MOD-0004` as upstream semantic vocabulary via
    `MetricSemanticRegistrySourceDependencyState`; `MOD-0063` as physical-plane
    dependency via `DataWarehouseSourceDependencyState`; data contract registry and
    notification as dependency-state context; `MOD-0062` and other downstream
    surfaces, ESBP goal-scoped scorecards, and HCM `CAND-CAP-0034` as non-owning
    consumers).
13. AC-13: Real scorecard/dashboard composition/authoring, chart/visual rendering,
    widget/tile data or query-result persistence, chart series or measure value
    materialization, embedded dataset-row persistence, rendered visual payload /
    image / export-blob persistence, viewer/owner PII / individual-attribution
    persistence, model output, and automated decision behavior remain unauthorized.
14. AC-14: Author/admin composition fields remain reserved/out of scope; no money
    field is present; no widget/tile data or query results, chart series or measure
    value, rendered visual payload, image/export blob, embedded dataset row,
    viewer/owner PII, or individual attribution field is present.
15. AC-15: Widget/tile data or query results, chart series or measure values,
    rendered visual payloads, image/export blobs, embedded dataset rows,
    viewer/owner PII, individual attributions, scores/ratings/ranks,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized; field names are marker-safe (no
    `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
    fragment in any property name; the registry-source and warehouse-source
    dependency fields are `MetricSemanticRegistrySourceDependencyState` and
    `DataWarehouseSourceDependencyState`) and the field-name test passes under the
    `\b` word-boundary matcher.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: `tr`/`en` localization parity is present.
18. AC-18: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
19. AC-19: Runtime literal scan for `MOD-0061` passes under
    runtime/frontend/gateway/test paths (module IDs are governance-only).

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: runtime-authored`.
- Confirm all 20 sections are present.
- Build DKI API:
  `dotnet build services/Diten.DataKnowledgeService/src/Diten.DataKnowledgeService.Api/Diten.DataKnowledgeService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `ScorecardsDashboards` filter
  (`Application.Tests/ScorecardsDashboardsTests.cs`, handler behavior; fix-absent
  should be RED).
- Run full DKI Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_dki_scorecards_dashboards_tenant_code_active`), and tenant/state indexing
  (`ix_dki_scorecards_dashboards_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex), not
  bare substring `Contains`, so legitimate words (`scorecard`, `dashboard`,
  `catalog`, `widget`, `layout`, `publication`) are not falsely rejected.
- Verify the contract field-name test confirms marker-safe field naming (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and warehouse-source
  dependency fields are `MetricSemanticRegistrySourceDependencyState` and
  `DataWarehouseSourceDependencyState`).
- Verify forbidden scorecard-catalog/widget-binding/layout/publication/review/
  decision/sensitive fields are absent.
- Verify author/admin composition fields are reserved/absent, no money field
  exists, and no widget/tile data or query result, chart series or measure value,
  rendered visual payload, image/export blob, embedded dataset row, viewer/owner
  PII, or individual attribution field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "MOD-0061" services frontend gateway`
  returns no runtime literal (module IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a runtime-authored slice.
- [x] Blueprint-canonical identity accepted (`MOD-0061`, used directly) as
  Scorecards / Dashboards.
- [x] Candidate gate script not-executable note is accepted.
- [x] EA runtime authorization accepted: domain `data-knowledge-intelligence` and
  dedicated data-plane service `Diten.DataKnowledgeService` assigned per
  `WP-DKI-SLICE-0061`.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope is
  accepted.
- [x] Real scorecard/dashboard composition/authoring and chart/visual rendering
  prohibition is accepted.
- [x] Widget/tile data or query result / chart series / measure value / embedded
  dataset-row / rendered visual payload / image-export-blob persistence prohibition
  is accepted.
- [x] Viewer/owner PII / individual-attribution persistence prohibition is
  accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved author/admin composition fields, no-money-field, and
  no-widget-tile-data/query-result/chart-series/measure-value/embedded-dataset-row/
  rendered-visual-payload/viewer-owner-PII/individual-attribution constraints are
  accepted.
- [x] Sensitive/PII payload prohibition is accepted.
- [x] Marker-safe field naming (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; registry-source and warehouse-source dependency
  fields `MetricSemanticRegistrySourceDependencyState`/`DataWarehouseSourceDependencyState`)
  with `\b` word-boundary matcher is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Analytics-backbone (`MOD-0004`, `MOD-0059`, `MOD-0060`, `MOD-0062`–`MOD-0064`),
  ESBP, and HCM dependency/consumer context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA runtime authorization / canonical MOD runtime assignment.
- [x] Runtime owner/key decision (`dki.scorecards-dashboards`).
- [x] Permission namespace decision.
- [x] Runtime repo scope decision (`services/Diten.DataKnowledgeService/**`).
- [x] Minimal metadata-only scorecard/dashboard composition readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/stewardship metadata-only waiver.
- [x] Data minimization policy.
- [x] Retention / publication / evidence / deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend and
  gateway route are scoped under `services/Diten.DataKnowledgeService/**` and the
  owned `frontend/Diten.Web/**` ScorecardsDashboards objects.
- Runtime owner/key remains limited to `dki.scorecards-dashboards`.
- Permission namespace remains limited to `dki.scorecards-dashboards.read`,
  `dki.scorecards-dashboards.manage`, `dki.scorecards-dashboards.evaluate`, and
  `dki.scorecards-dashboards.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime public/persisted
  contract and verified against `ScorecardsDashboardsReadinessMetadata.cs`.
- `MetricSemanticRegistrySourceDependencyState`, `DataWarehouseSourceDependencyState`,
  `DataContractRegistryDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states; no property name carries a
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment.
- Author/admin composition UX fields remain reserved/out of scope, no money field
  is present, and no widget/tile data or query result, chart series or measure
  value, rendered visual payload, image/export blob, embedded dataset row,
  viewer/owner PII, or individual attribution field is present.
- ESBP, HCM, and Platform scopes remain closed.
- Real scorecard/dashboard composition/authoring, chart/visual rendering,
  widget/tile data or query-result persistence, chart series or measure value
  materialization, embedded dataset-row persistence, rendered visual payload /
  image / export-blob persistence, model output, automated decision behavior,
  notification/document integration, viewer/owner PII, individual attributions, and
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
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0061`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted the runtime-authorization revert of the prior
  governance-first deferral: the DCP-008 "Analytics backbone — Pending EA domain
  mapping" note is resolved by assigning domain `data-knowledge-intelligence` and
  the dedicated data-plane service `Diten.DataKnowledgeService` per
  `WP-DKI-SLICE-0061`, rather than defaulting to `Diten.EnterpriseStrategyService`
  or `Diten.Platform`.
- EA/registry owner accepted verifier absence as a governance note.
- EA runtime policy waiver approved for the first metadata-only backend/API
  readiness slice (K19-complete), with golden-compact readiness CRUD frontend and
  gateway route in scope. `MOD-0061` is Blueprint-canonical and its DCP-002
  canonical registry row is reserved and used directly (no candidate identity).
  The DCP-008 reservation language (registry row + module pack required before
  runtime) is satisfied and superseded by this runtime authorization.
- Legal/privacy/stewardship metadata-only waiver approved.
- Data minimization fail-closed policy approved.
- Retention / publication / evidence / deletion local/deferred metadata policy
  approved.
- Marker-safe field-naming waiver approved: field names deliberately avoid
  forbidden-marker fragments (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and warehouse-source
  dependency fields are `MetricSemanticRegistrySourceDependencyState` and
  `DataWarehouseSourceDependencyState`), enforced by the contract field-name test
  using the `\b` word-boundary matcher.

Runtime literal scan:

- Required command:
  `rg -n "MOD-0061" services frontend gateway`
- Module IDs are governance-only and must return no runtime literal match; the
  runtime uses owner key `dki.scorecards-dashboards` and gateway route
  `/api/scorecards-dashboards`, never the MOD ID.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real scorecard/dashboard composition/authoring.
- Chart/visual rendering runtime and widget/tile data materialization.
- Chart series / measure value materialization and evaluation.
- Layout/composition versioning and lineage.
- Publication/sharing distribution and visibility-scope enforcement.
- Semantic-binding execution against `MOD-0004` vocabulary.
- Physical-binding execution against `MOD-0063` warehouse models.
- KPI catalog composition consumption (`MOD-0059`).
- Metric-definition composition consumption (`MOD-0060`).
- Baseline/experiment measurement consumption (`MOD-0062`).
- Automated decision approval.
- Author/admin scorecard/dashboard composition UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
