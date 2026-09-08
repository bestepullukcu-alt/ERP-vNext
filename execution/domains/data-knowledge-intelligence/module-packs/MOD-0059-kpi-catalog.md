---
id: MOD-0059
name: KPI Catalog
domain: data-knowledge-intelligence
blueprint_domain: Data, Knowledge & Intelligence
service: Diten.DataKnowledgeService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: runtime-authored
owner: enterprise-architect / dki-domain-owner / data-platform-owner / analytics-owner / platform-team / security-owner
branch: feature/dki/mod-0059-kpi-catalog
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
runtime_owner_key: dki.kpi-catalog
permission_namespace:
  - dki.kpi-catalog.read
  - dki.kpi-catalog.manage
  - dki.kpi-catalog.evaluate
  - dki.kpi-catalog.audit.read
runtime_repo_scope: services/Diten.DataKnowledgeService/**
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0059 - KPI Catalog

> Status: runtime-authored. This pack records the first metadata-only backend/API
> KPI catalog readiness contract slice under
> `services/Diten.DataKnowledgeService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5000` -> DKI `5062`). `MOD-0059` is **Blueprint-canonical** (Blueprint
> domain **Data, Knowledge & Intelligence**); the canonical MOD ID is used
> directly and remains a governance/documentation identity that must not be
> written into runtime literals. The governance-first runtime deferral recorded in
> the prior draft is **REVERTED**: per `WP-DKI-SLICE-0059`, EA resolved the
> DCP-008 "Analytics backbone — Pending EA domain mapping" note by assigning the
> repo domain `data-knowledge-intelligence` and the dedicated data-plane runtime
> service `Diten.DataKnowledgeService` (namespace `Diten.DataKnowledgeService.*`,
> port `5062`, Mongo db `DitenDataKnowledge`, permission owner key `dki.*`). Real
> KPI computation/measurement, KPI values or measure results, metric query
> expressions, target/threshold numbers, formula/definition bodies, dataset rows,
> individual attributions, model output or automated decision behavior, and
> sensitive/PII-heavy persistence remain closed.

## 1. Module Summary

`MOD-0059` is the Blueprint-canonical **KPI Catalog**: the **curated, governed
catalog layer** of the analytics backbone — the readiness layer for the curated
set of Key Performance Indicators, their identity, their binding to backing
metric definitions, their ownership scope, and their publication lifecycle. It is
a **catalog (governance/curation) layer**, not a computation, measurement, or
reporting engine: it declares the readiness STATE of KPI *catalog metadata* and
never holds computed KPI values, measured data, metric query expressions,
target/threshold numbers, or formula/definition bodies.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and originally
marked its domain ownership as "Pending EA domain mapping" with no existing
service. That deferral is now resolved: EA assigned the repo domain
`data-knowledge-intelligence` and reused the dedicated data-plane runtime service
(`Diten.DataKnowledgeService`) to own this cross-cutting analytics-catalog
capability, rather than defaulting it to `Diten.EnterpriseStrategyService` or
`Diten.Platform`. This pack records the first metadata-only backend/API readiness
contract slice authored under `WP-DKI-SLICE-0059`, mirroring the completed
readiness slices exactly.

The KPI Catalog capability is the readiness layer for the curation plane over
concrete metric definitions: it declares the readiness STATE of the KPI identity
catalog, the definition-binding intake boundary, KPI ownership scope, publication
control, catalog review, and automated-decision boundary. It sits in the
analytics-backbone group (`MOD-0004`, `MOD-0059`–`MOD-0064`) as the curation
layer that references — never authors — concrete metric definitions
(`MOD-0060`) bound to the shared semantic vocabulary (`MOD-0004`), and publishes
its readiness to downstream visual-composition modules (`MOD-0061`). Real KPI
computation/measurement, KPI values or measure results, metric query expressions,
target/threshold numbers, formula/definition bodies, dataset rows, individual
attributions, model output, automated decision behavior, and sensitive/PII-heavy
persistence remain closed.

## 2. Ownership and Boundaries

`MOD-0059` is Blueprint-canonical and is used directly as the canonical **KPI
Catalog** identity; no candidate identity and no name drift apply to this ID.

Owned by this pack:

- DKI-native KPI catalog governance boundary.
- Sequencing as the curation edge of the analytics-backbone group
  (`MOD-0004`, `MOD-0059`–`MOD-0064`), referencing `MOD-0060` and feeding
  `MOD-0061`.
- Metadata-only KPI catalog readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the KPI identity catalog, definition-binding
  intake, KPI ownership scope, publication control, and catalog review, consumed
  by downstream analytics-backbone modules.
- Explicit exclusion of real KPI computation/measurement.
- Explicit exclusion of metric query/definition execution, KPI value
  computation, scoring, model output, and automated decision behavior.
- Explicit exclusion of steward-facing and admin-facing catalog UX beyond the
  readiness-metadata CRUD (those fields are reserved and out of scope).
- Explicit exclusion of KPI values, measure results, metric query expressions,
  target/threshold numbers, formula/definition bodies, dataset rows, individual
  attributions, free-text, and any PII dataset persistence.

Not owned by this pack:

- Concrete metric definitions, formulas, and thresholds — owned by `MOD-0060
  Metric Definitions & Ownership`, referenced here by readiness state only.
- The shared semantic vocabulary — owned by `MOD-0004 Metric & Semantic
  Registry`.
- Visual composition / scorecards — owned by `MOD-0061 Scorecards / Dashboards`,
  the distinct downstream consumer this module's readiness feeds.
- Baselines / experiments — owned by `MOD-0062 Baseline & Experiment
  Measurement`.
- Physical storage and query — owned by `MOD-0063 Data Warehouse / Lakehouse`.
- Data movement / transformation — owned by `MOD-0064 ETL / ELT Pipelines`.
- Data dictionary and data contract registry ownership beyond dependency/context
  references.
- Secrets/vault, notification, and document ownership beyond dependency/context
  references.
- `Diten.EnterpriseStrategyService` (ESBP) — a consumer of the shared KPI
  catalog (goal-scoped strategy KPIs), not the owner of this data plane.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` and other facades — consumers
  that own no analytics runtime.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| KpiCatalogCapabilityBoundary | Governance boundary | Defines what the DKI KPI catalog module may own. |
| KpiCatalogReadinessBoundary | Governance boundary | Separates readiness metadata from KPI computation/measurement. |
| KpiIdentityCatalogBoundary | Governance boundary | Blocks raw KPI value/measure-result and individual-attribution persistence in the identity catalog. |
| DefinitionBindingIntakeBoundary | Governance boundary | Blocks metric query expression, formula/definition body, and target/threshold number persistence at binding intake. |
| KpiOwnershipScopeBoundary | Governance boundary | Blocks individual-attribution / PII persistence in KPI ownership scope. |
| PublicationControlBoundary | Governance boundary | Blocks published KPI value / dataset-row content in the publication lifecycle. |
| CatalogReviewBoundary | Governance boundary | Blocks free-text review narrative and review-note body persistence. |
| KpiCatalogDecisionBoundary | Governance boundary | Blocks scoring, ranking, model-output, and automated decision persistence. |
| KpiCatalogUxBoundary | Governance boundary | Blocks steward/admin catalog UX beyond readiness-metadata CRUD. |
| KpiCatalogSensitiveDataBoundary | Governance boundary | Blocks KPI values, measure results, metric query expressions, target/threshold numbers, formula/definition bodies, dataset rows, individual attributions, and PII persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| KpiComputationEngine | Deferred runtime | Real KPI computation/measurement and value evaluation; not authorized. |
| MetricQueryExecutionEngine | Deferred runtime | Metric query / definition expression execution; not authorized. |
| TargetThresholdEvaluationEngine | Deferred runtime | Target/threshold number evaluation and breach detection; not authorized. |
| KpiPublicationEngine | Deferred runtime | Published KPI value / dataset delivery; not authorized. |
| KpiScoringDecisionEngine | Deferred runtime | Scoring, ranking, model-output, and automated decision behavior; not authorized. |
| StewardAdminCatalogExperience | Deferred frontend/runtime | Steward/admin KPI catalog UX beyond readiness CRUD; not authorized. |
| KpiCatalogDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| KpiCatalogNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| KpiCatalogReadinessMetadata | Domain entity | Metadata-only readiness contract; `src/Diten.DataKnowledgeService.Domain/Entities/KpiCatalogReadinessMetadata.cs`. |
| KpiCatalogReadinessState | Domain enum | Readiness state; `src/Diten.DataKnowledgeService.Domain/Enums/KpiCatalogReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IKpiCatalogReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `src/Diten.DataKnowledgeService.Domain/Repositories/IKpiCatalogReadinessMetadataRepository.cs`. |
| MongoKpiCatalogReadinessMetadataRepository | Persistence repository | Mongo-backed repository (db `DitenDataKnowledge`); collection `dki_kpi_catalog_readiness`; indexes `ux_dki_kpi_catalog_tenant_code_active` and `ix_dki_kpi_catalog_tenant_state`. |
| KpiCatalog Application features | Application (CQRS/MediatR) | `src/Diten.DataKnowledgeService.Application/Features/KpiCatalog/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| KpiCatalogController | API controller | Thin controller; `src/Diten.DataKnowledgeService.Api/Controllers/Dki/KpiCatalogController.cs`. |
| KpiCatalog golden-compact CRUD | Frontend | `Controllers/KpiCatalogController.cs` + `Views/DataKnowledge/KpiCatalog/**` + `Models/DataKnowledge/KpiCatalog/**` + `wwwroot/assets/js/DataKnowledge/KpiCatalog/**` + Resources + DKI nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `KpiCatalogReadinessState`):

- `Code`
- `DisplayName`
- `KpiCatalogReadinessState`
- `KpiIdentityCatalogBoundaryState`
- `DefinitionBindingIntakeBoundaryState`
- `OwnershipScopeBoundaryState`
- `PublicationControlBoundaryState`
- `CatalogReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `MetricSemanticRegistrySourceDependencyState`
- `DataDictionaryDependencyState`
- `DataContractRegistryDependencyState`
- `NotificationDependencyState`
- `StewardshipPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `PublicationPolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `KpiCatalogReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Field-naming discipline (marker-safe): field names are deliberately chosen to
avoid forbidden-marker fragments so the contract field-name test does not falsely
reject legitimate domain fields. In particular, no property name carries a
`score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
marker fragment: the backing-definition dependency field is named
`MetricSemanticRegistrySourceDependencyState` (using `MetricSemanticRegistrySource*`,
referencing `MOD-0004` rather than embedding a `definition`/`formula` marker), and
the data-dictionary dependency field is named `DataDictionaryDependencyState`
(using `DataDictionary*`), so neither collides with the forbidden marker class.
The forbidden-marker matcher uses a word/token boundary (`\b` regex), not a bare
substring `Contains`, so legitimate domain words (`catalog`, `metric`,
`publication`, `taxonomy` — which contains `tax`, `scorecard` — which contains
`score`, `dictionary`) are not falsely rejected while true forbidden markers
(KPI value / measure result, metric query expression, target/threshold number,
formula/definition body, dataset row, individual attribution, `score`, `rating`,
`rank`, `narrative`, PII, payload) are still caught.

Reserved / out-of-scope fields (must not be added in this slice):

- Steward-facing catalog fields (steward KPI submission, steward review, steward
  action UX state) are reserved and out of scope.
- Admin-facing catalog fields (admin catalog/taxonomy administration, admin
  approval, admin self-service UX state) are reserved and out of scope.
- No money field (salary/compensation/cost/budget/target-value). This is a
  metadata-only readiness slice with no monetary attribute.
- No KPI value, measure result, metric query expression, target/threshold number,
  formula/definition body, dataset row, individual attribution, or PII dataset
  field of any kind.

Forbidden field classes:

- KPI value or measure result, metric query expression, target/threshold number,
  formula/definition body, dataset row/record body, published-KPI content, or
  completed catalog-output content.
- Readiness score, quality score, rating value, rank, match confidence, model
  output, automated decision result, recommendation output, or evaluator scoring
  payload.
- Individual attributions, free-text catalog/review narrative, HR notes, or
  sensitive data body.
- Attachment payload, controlled document body, external document content, raw
  evidence, raw provider payload, or uploaded file metadata.
- Connection string, credential, token, secret, password, activation code,
  provider connection value, device secret, or account bootstrap secret.
- Salary amount, compensation amount, target/threshold amount, benefits election,
  payroll details, tax details, bank details, or payment instruction fields.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  any PII-heavy dataset/record field.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0059-kpi-catalog.md`
- The `MOD-0059` row in `execution/registries/module-id-registry.md`.

Runtime service:

- `Diten.DataKnowledgeService` (namespace `Diten.DataKnowledgeService.*`, port
  `5062`, Mongo db `DitenDataKnowledge`).

Runtime repo scope:

- `services/Diten.DataKnowledgeService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (`Controllers/KpiCatalogController.cs`,
  `Views/DataKnowledge/KpiCatalog/**`,
  `Models/DataKnowledge/KpiCatalog/**`,
  `wwwroot/assets/js/DataKnowledge/KpiCatalog/**`, Resources, DKI nav
  entry at `/DataKnowledge/KpiCatalog`).
- Gateway route `/api/kpi-catalog` exposure through the API gateway
  (`5000` -> DKI `5062`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.DataKnowledgeService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned KpiCatalog CRUD objects listed in §3/§5.
- `gateway/**` outside the owned `/api/kpi-catalog` readiness route.
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.Platform/**`
- `services/Diten.HumanCapitalService/**`
- global `tests/**`
- the other analytics-backbone module packs (`MOD-0004`, `MOD-0060`–`MOD-0064`).
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/data-knowledge-intelligence/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Analytics-backbone relationships:

- Depends on: `MOD-0060 Metric Definitions & Ownership` — the KPI catalog binds
  to concrete metric definitions authored there; their availability is treated as
  boundary/readiness metadata context (definition-binding intake), never the
  definition/formula body itself.
- Depends on: `MOD-0004 Metric & Semantic Registry` — the shared semantic
  vocabulary those definitions bind to, consumed via
  `MetricSemanticRegistrySourceDependencyState`.
- Consumed by: `MOD-0061 Scorecards / Dashboards` — composes the catalog's
  readiness into visual surfaces; its consumption is treated as boundary/readiness
  metadata context, not as a raw KPI-value source.

Runtime dependency-state context (metadata-only, via boundary/dependency states):

- Metric & Semantic Registry Source (the `MOD-0004` shared semantic vocabulary),
  consumed via `MetricSemanticRegistrySourceDependencyState` (field named
  `MetricSemanticRegistrySource*`, marker-safe). Registry-source availability is a
  controlling precondition and is treated as boundary/readiness metadata only; no
  metric query expression or definition body is persisted.
- Data Dictionary, consumed via `DataDictionaryDependencyState` (field named
  `DataDictionary*`). Data-dictionary availability is a controlling precondition;
  no dictionary payload or dataset row is persisted.
- Data Contract Registry, consumed via `DataContractRegistryDependencyState`.
- Notification, consumed via `NotificationDependencyState`.

Consumer context (not dependencies of this module, they depend on / consume it):

- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped strategy KPI runtime.
- HCM `CAND-CAP-0034` HR KPI & Analytics Facade.

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is authorized
only for the first metadata-only backend/API readiness slice, along with the
golden-compact readiness CRUD frontend and gateway route.

Authorized runtime boundary:

- Runtime owner/key: `dki.kpi-catalog`.
- Permission namespace:
  - `dki.kpi-catalog.read`
  - `dki.kpi-catalog.manage`
  - `dki.kpi-catalog.evaluate`
  - `dki.kpi-catalog.audit.read`
- Runtime repo scope: `services/Diten.DataKnowledgeService/**`.
- Thin controller
  (`src/Diten.DataKnowledgeService.Api/Controllers/Dki/KpiCatalogController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository (db `DitenDataKnowledge`); collection
  `dki_kpi_catalog_readiness`.
- Active tenant `Code` uniqueness through
  `ux_dki_kpi_catalog_tenant_code_active`, with
  `ix_dki_kpi_catalog_tenant_state` supporting tenant/state queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`catalog`, `metric`,
  `publication`, `taxonomy`, `scorecard`, `dictionary`) are not falsely rejected
  while true forbidden markers (KPI value / measure result, metric query
  expression, target/threshold number, formula/definition body, dataset row,
  individual attribution, `score`, `rating`, `rank`, `narrative`, PII, payload)
  are still caught. Field names are deliberately marker-safe (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and data-dictionary
  dependency fields are `MetricSemanticRegistrySourceDependencyState` and
  `DataDictionaryDependencyState`), enforced by the contract field-name test.

Explicitly unauthorized:

- Real KPI computation/measurement.
- KPI value / measure result persistence.
- Metric query expression execution or persistence.
- Target/threshold number evaluation and persistence.
- Formula/definition body persistence.
- Published KPI value / dataset-row delivery and persistence.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Steward/admin catalog UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- KPI values, measure results, metric query expressions, target/threshold
  numbers, formula/definition bodies, dataset rows, individual attributions, or
  PII payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract, at `/DataKnowledge/KpiCatalog`.

Authorized UX:

- DKI tenant shell nav entry `KPI Catalog`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Steward catalog UX beyond readiness-metadata CRUD.
- Admin catalog/taxonomy administration UX beyond readiness-metadata CRUD.
- KPI identity catalog, definition binding, ownership scope, publication control,
  catalog review, scoring, or model-output UI.
- Document, notification, export, upload, or attachment UI.
- Any KPI value, measure result, metric query expression, target/threshold
  number, formula/definition body, or dataset-row viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5000` -> DKI `5062`).

Authorized API surface:

- `GET /api/kpi-catalog` - list readiness metadata.
- `GET /api/kpi-catalog/{id}` - get readiness metadata by id.
- `POST /api/kpi-catalog` - create readiness metadata.
- `POST /api/kpi-catalog/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/kpi-catalog/{id}` - soft-delete.
- `GET /api/kpi-catalog/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get KPI catalog readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No KPI computation/measurement, KPI value / measure-result persistence, metric
  query expression execution, target/threshold number evaluation, formula/
  definition body persistence, dataset-row persistence, scoring/model-output
  persistence, automated decision behavior, notification/document integration, or
  individual-attribution/PII persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only KPI catalog readiness state.
- Boundary-state metadata for KPI identity catalog, definition-binding intake,
  ownership scope, publication control, catalog review, automated decision,
  stewardship, minimization, retention, and publication policy.
- Dependency-state metadata for Metric & Semantic Registry Source
  (`MetricSemanticRegistrySourceDependencyState`), data dictionary
  (`DataDictionaryDependencyState`), data contract registry, and notification
  dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- KPI computation/measurement payload.
- KPI value or measure result.
- Metric query expression body.
- Target/threshold number.
- Formula/definition body.
- Dataset row / record body or published-KPI output.
- Catalog-review narrative or review-note body.
- Readiness score/quality score/rating/rank/match confidence/model output.
- Automated decision result.
- Individual attribution.
- Connection string, credential/token/secret/password value.
- Attachment payload.
- Raw provider payload.
- PII-heavy dataset/record details.
- Compensation/payroll/target-value payload.

Data-plane note: this module is a **curation/catalog layer** over the metric
stack, and this slice declares readiness STATE only. No KPI value, measure result,
metric query expression, target/threshold number, formula/definition body,
dataset row, or individual attribution of any kind may cross the boundary into
persistence. Only boundary/readiness STATE metadata governed by stewardship
precondition, data minimization, retention, and publication policy is permitted,
keeping the strong analytics-governance concerns (KPI-metadata minimization,
tenant isolation of catalog entries, curated-vs-tenant scope of catalog identity)
resolved as metadata-only state.

## 12. Permission Boundary

Approved permission namespace:

- `dki.kpi-catalog.read`
- `dki.kpi-catalog.manage`
- `dki.kpi-catalog.evaluate`
- `dki.kpi-catalog.audit.read`

Runtime owner key: `dki.kpi-catalog`. The canonical module ID `MOD-0059` is a
governance/documentation identity and must not be used as a runtime literal,
owner key, or permission namespace fragment.

## 13. Tenant / Security Boundary

Runtime work is tenant-aware and fails closed. The backend/API slice resolves
tenant server-side and does not expose `TenantId` in request DTOs.

Security guardrails:

- Canonical identity (`MOD-0059`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_dki_kpi_catalog_tenant_code_active` and `ix_dki_kpi_catalog_tenant_state`.
- Because the KPI catalog may be partly platform-curated with tenant overlays,
  tenant isolation across catalog entries and their bound-definition references is
  a first-class control even for readiness metadata; no KPI value, measure result,
  or individual attribution is persisted, and curated-vs-tenant catalog scope
  remains a governed readiness concern.

## 14. Compliance / Privacy Boundary

Legal/privacy/stewardship is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only stewardship/precondition representation, with a valid stewardship
  basis treated as a precondition to activation (`StewardshipPreconditionState`).
- Data minimization fail-closed behavior (`DataMinimizationState`): store only
  readiness state; prefer aggregated/derived over raw KPI values or dataset rows.
- Retention and publication policy as local/deferred metadata
  (`RetentionPolicyState`, `PublicationPolicyState`), covering the defined
  lifecycle, purge policy, and publication posture for KPI catalog readiness.
- Exclusion of KPI values, measure results, metric query expressions,
  target/threshold numbers, formula/definition bodies, dataset rows, individual
  attributions, and any PII dataset.

The KPI Catalog capability declares readiness STATE only; it does not own or store
any KPI values, measure results, metric query expressions, target/threshold
numbers, formula/definition bodies, dataset rows, or individual attributions, and
all downstream analytics-backbone consumers must treat its output as
boundary/readiness metadata, not as a KPI-value source.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, or document/
notification UI is authorized. `MOD-0060 Metric Definitions & Ownership` and
`MOD-0004 Metric & Semantic Registry` are consumed only as upstream
dependency-state context (definition-binding intake and
`MetricSemanticRegistrySourceDependencyState`); data dictionary and data contract
registry are consumed only as dependency-state context; `MOD-0061` is a downstream
consumer reference; and ESBP and HCM `CAND-CAP-0034` remain non-owning consumers.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `runtime-authored`.
2. AC-02: Canonical identity is `MOD-0059` (Blueprint-backed; used directly, not
   a candidate) as **KPI Catalog**.
3. AC-03: Runtime owner/key is `dki.kpi-catalog`.
4. AC-04: Permission namespace is limited to
   `dki.kpi-catalog.read`, `dki.kpi-catalog.manage`,
   `dki.kpi-catalog.evaluate`, and `dki.kpi-catalog.audit.read`.
5. AC-05: Runtime repo scope is limited to
   `services/Diten.DataKnowledgeService/**`; owning service is
   `Diten.DataKnowledgeService` (port `5062`, Mongo db `DitenDataKnowledge`).
6. AC-06: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3), exposed through the gateway route `/api/kpi-catalog`
   (`5000` -> DKI `5062`).
7. AC-07: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
8. AC-08: Mongo persistence (collection `dki_kpi_catalog_readiness`) includes
   active tenant `Code` uniqueness (`ux_dki_kpi_catalog_tenant_code_active`),
   tenant/state indexing (`ix_dki_kpi_catalog_tenant_state`), soft delete, and
   tenant isolation (E4).
9. AC-09: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
   records.
10. AC-10: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
11. AC-11: RBAC is enforced server-side across the controller surface.
12. AC-12: Analytics-backbone dependency boundaries are recorded (`MOD-0060` as
    backing-definition dependency via definition-binding intake; `MOD-0004` as
    upstream via `MetricSemanticRegistrySourceDependencyState`; `MOD-0061` as
    downstream consumer; data dictionary, data contract registry, and
    notification as dependency-state context; ESBP and HCM `CAND-CAP-0034` as
    non-owning consumers).
13. AC-13: Real KPI computation/measurement, KPI value / measure-result
    persistence, metric query expression execution, target/threshold number
    evaluation, formula/definition body persistence, dataset-row persistence,
    model output, and automated decision behavior remain unauthorized.
14. AC-14: Steward/admin catalog fields remain reserved/out of scope; no money
    field is present; no KPI value, measure result, metric query expression,
    target/threshold number, formula/definition body, dataset row, or individual
    attribution field is present.
15. AC-15: KPI values, measure results, metric query expressions, target/threshold
    numbers, formula/definition bodies, dataset rows, individual attributions,
    scores/ratings/ranks, credential/token/secret/password, and PII-heavy
    persistence remain unauthorized; field names are marker-safe (no
    `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
    fragment in any property name; the registry-source and data-dictionary
    dependency fields are `MetricSemanticRegistrySourceDependencyState` and
    `DataDictionaryDependencyState`) and the field-name test passes under the `\b`
    word-boundary matcher.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: `tr`/`en` localization parity is present.
18. AC-18: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
19. AC-19: Runtime literal scan for `MOD-0059` passes under
    runtime/frontend/gateway/test paths (module IDs are governance-only).

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: runtime-authored`.
- Confirm all 20 sections are present.
- Build DKI API:
  `dotnet build services/Diten.DataKnowledgeService/src/Diten.DataKnowledgeService.Api/Diten.DataKnowledgeService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `KpiCatalog` filter
  (`Application.Tests/KpiCatalogTests.cs`, handler behavior; fix-absent should be
  RED).
- Run full DKI Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_dki_kpi_catalog_tenant_code_active`), and tenant/state indexing
  (`ix_dki_kpi_catalog_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`catalog`, `metric`,
  `publication`, `taxonomy`, `scorecard`, `dictionary`) are not falsely rejected.
- Verify the contract field-name test confirms marker-safe field naming (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and data-dictionary
  dependency fields are `MetricSemanticRegistrySourceDependencyState` and
  `DataDictionaryDependencyState`).
- Verify forbidden identity-catalog/definition-binding/ownership/publication/
  review/decision/sensitive fields are absent.
- Verify steward/admin catalog fields are reserved/absent, no money field exists,
  and no KPI value, measure result, metric query expression, target/threshold
  number, formula/definition body, dataset row, or individual attribution field
  exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "MOD-0059" services frontend gateway`
  returns no runtime literal (module IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a runtime-authored slice.
- [x] Blueprint-canonical identity accepted (`MOD-0059`, used directly) as KPI
  Catalog.
- [x] Candidate gate script not-executable note is accepted.
- [x] EA runtime authorization accepted: domain `data-knowledge-intelligence` and
  dedicated data-plane service `Diten.DataKnowledgeService` assigned per
  `WP-DKI-SLICE-0059`.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real KPI computation/measurement prohibition is accepted.
- [x] KPI value / measure-result / dataset-row persistence prohibition is
  accepted.
- [x] Metric query expression / target-threshold number / formula-definition body
  prohibition is accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved steward/admin catalog fields, no-money-field, and
  no-KPI-value/measure-result/metric-query-expression/target-threshold-number/
  formula-definition-body/dataset-row/individual-attribution constraints are
  accepted.
- [x] Sensitive/PII payload prohibition is accepted.
- [x] Marker-safe field naming (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; registry-source and data-dictionary dependency
  fields `MetricSemanticRegistrySourceDependencyState`/`DataDictionaryDependencyState`)
  with `\b` word-boundary matcher is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Analytics-backbone (`MOD-0004`, `MOD-0060`–`MOD-0064`), ESBP, and HCM
  dependency/consumer context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA runtime authorization / canonical MOD runtime assignment.
- [x] Runtime owner/key decision (`dki.kpi-catalog`).
- [x] Permission namespace decision.
- [x] Runtime repo scope decision (`services/Diten.DataKnowledgeService/**`).
- [x] Minimal metadata-only KPI catalog readiness contract.
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
  owned `frontend/Diten.Web/**` KpiCatalog objects.
- Runtime owner/key remains limited to `dki.kpi-catalog`.
- Permission namespace remains limited to `dki.kpi-catalog.read`,
  `dki.kpi-catalog.manage`, `dki.kpi-catalog.evaluate`, and
  `dki.kpi-catalog.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `MetricSemanticRegistrySourceDependencyState`, `DataDictionaryDependencyState`,
  `DataContractRegistryDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states; no property name carries a
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment.
- Steward/admin catalog UX fields remain reserved/out of scope, no money field is
  present, and no KPI value, measure result, metric query expression,
  target/threshold number, formula/definition body, dataset row, or individual
  attribution field is present.
- ESBP, HCM, and Platform scopes remain closed.
- Real KPI computation/measurement, KPI value / measure-result persistence, metric
  query expression execution, target/threshold number evaluation, formula/
  definition body persistence, dataset-row persistence, model output, automated
  decision behavior, notification/document integration, individual attributions,
  and PII-heavy persistence remain out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0059`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted the runtime-authorization revert of the prior
  governance-first deferral: the DCP-008 "Analytics backbone — Pending EA domain
  mapping" note is resolved by assigning domain `data-knowledge-intelligence` and
  the dedicated data-plane service `Diten.DataKnowledgeService` per
  `WP-DKI-SLICE-0059`, rather than defaulting to `Diten.EnterpriseStrategyService`
  or `Diten.Platform`.
- EA/registry owner accepted verifier absence as a governance note.
- EA runtime policy waiver approved for the first metadata-only backend/API
  readiness slice (K19-complete), with golden-compact readiness CRUD frontend and
  gateway route in scope. `MOD-0059` is Blueprint-canonical and its DCP-002
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
  fragment in any property name; the registry-source and data-dictionary
  dependency fields are `MetricSemanticRegistrySourceDependencyState` and
  `DataDictionaryDependencyState`), enforced by the contract field-name test using
  the `\b` word-boundary matcher.

Runtime literal scan:

- Required command:
  `rg -n "MOD-0059" services frontend gateway`
- Module IDs are governance-only and must return no runtime literal match; the
  runtime uses owner key `dki.kpi-catalog` and gateway route `/api/kpi-catalog`,
  never the MOD ID.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real KPI computation/measurement.
- KPI value / measure-result evaluation and publication.
- Metric query expression binding and execution (via `MOD-0060` definitions).
- Target/threshold number evaluation and breach detection.
- Formula/definition-body management (owned by `MOD-0060`).
- Scorecard/dashboard composition consumption (`MOD-0061`).
- Baseline/experiment measurement integration (`MOD-0062`).
- Automated decision approval.
- Steward/admin KPI catalog UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
