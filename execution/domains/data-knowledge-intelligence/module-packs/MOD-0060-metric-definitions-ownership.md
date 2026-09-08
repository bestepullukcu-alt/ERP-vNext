---
id: MOD-0060
name: Metric Definitions & Ownership
domain: data-knowledge-intelligence
blueprint_domain: Data, Knowledge & Intelligence
service: Diten.DataKnowledgeService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: runtime-authored
owner: enterprise-architect / dki-domain-owner / data-platform-owner / analytics-owner / platform-team / security-owner
branch: feature/dki/mod-0060-metric-definitions-ownership
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
runtime_owner_key: dki.metric-definitions-ownership
permission_namespace:
  - dki.metric-definitions-ownership.read
  - dki.metric-definitions-ownership.manage
  - dki.metric-definitions-ownership.evaluate
  - dki.metric-definitions-ownership.audit.read
runtime_repo_scope: services/Diten.DataKnowledgeService/**
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0060 - Metric Definitions & Ownership

> Status: runtime-authored. This pack records the first metadata-only backend/API
> metric-definition & ownership readiness contract slice under
> `services/Diten.DataKnowledgeService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5000` -> DKI `5062`). `MOD-0060` is **Blueprint-canonical** (Blueprint
> domain **Data, Knowledge & Intelligence**); the canonical MOD ID is used
> directly and remains a governance/documentation identity that must not be
> written into runtime literals. The governance-first runtime deferral recorded in
> the prior draft is **REVERTED**: per `WP-DKI-SLICE-0060`, EA resolved the
> DCP-008 "Analytics backbone — Pending EA domain mapping" note by assigning the
> repo domain `data-knowledge-intelligence` and the dedicated data-plane runtime
> service `Diten.DataKnowledgeService` (namespace `Diten.DataKnowledgeService.*`,
> port `5062`, Mongo db `DitenDataKnowledge`, permission owner key `dki.*`). Real
> metric-definition authoring/execution, metric formula/definition/expression
> bodies, calculation logic, metric or measure values, dataset rows,
> owner/steward PII, individual attributions, model output or automated decision
> behavior, and sensitive/PII-heavy persistence remain closed.

## 1. Module Summary

`MOD-0060` is the Blueprint-canonical **Metric Definitions & Ownership**: the
**authoring and stewardship layer** for concrete metric definitions across the
analytics backbone — the readiness layer for what a metric *means*, how it is
*calculated*, and *who is accountable for it*: definition identity, ownership
assignment, stewardship scope, approval control, and definition review. It is a
**definition/stewardship (governance/authoring) layer**, not a computation,
measurement, or reporting engine: it declares the readiness STATE of
*metric-definition & ownership metadata* and never holds metric formula/
definition/expression bodies, calculation logic, metric or measure values,
dataset rows, owner/steward PII, or individual attributions.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and originally
marked its domain ownership as "Pending EA domain mapping" with no existing
service. That deferral is now resolved: EA assigned the repo domain
`data-knowledge-intelligence` and reused the dedicated data-plane runtime service
(`Diten.DataKnowledgeService`) to own this cross-cutting metric-authoring
capability, rather than defaulting it to `Diten.EnterpriseStrategyService` or
`Diten.Platform`. This pack records the first metadata-only backend/API readiness
contract slice authored under `WP-DKI-SLICE-0060`, mirroring the completed
readiness slices exactly.

The Metric Definitions & Ownership capability is the readiness layer for the
authoring plane that binds concrete definitions to the shared semantic vocabulary
and maps them onto physical models: it declares the readiness STATE of the
definition catalog, the ownership-assignment intake boundary, stewardship scope,
approval control, definition review, and the automated-decision boundary. It sits
in the analytics-backbone group (`MOD-0004`, `MOD-0059`–`MOD-0064`) as the
authoring layer that binds every concrete definition to the shared semantic
vocabulary (`MOD-0004`) and maps it to the physical binding of `MOD-0063`, and
publishes its readiness to downstream curation/composition/measurement modules
(`MOD-0059`, `MOD-0061`, `MOD-0062`). Real metric-definition authoring/execution,
metric formula/definition/expression bodies, calculation logic, metric or measure
values, dataset rows, owner/steward PII, individual attributions, model output,
automated decision behavior, and sensitive/PII-heavy persistence remain closed.

## 2. Ownership and Boundaries

`MOD-0060` is Blueprint-canonical and is used directly as the canonical **Metric
Definitions & Ownership** identity; no candidate identity and no name drift apply
to this ID.

Owned by this pack:

- DKI-native metric-definition & ownership governance boundary.
- Sequencing as the authoring edge of the analytics-backbone group
  (`MOD-0004`, `MOD-0059`–`MOD-0064`), binding to `MOD-0004` / `MOD-0063` and
  feeding `MOD-0059` / `MOD-0061` / `MOD-0062`.
- Metadata-only metric-definition & ownership readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the definition catalog, ownership-assignment
  intake, stewardship scope, approval control, and definition review, consumed by
  downstream analytics-backbone modules.
- Explicit exclusion of real metric-definition authoring/execution.
- Explicit exclusion of metric formula/definition/expression execution,
  calculation logic, metric/measure value computation, scoring, model output, and
  automated decision behavior.
- Explicit exclusion of steward-facing and admin-facing definition-authoring UX
  beyond the readiness-metadata CRUD (those fields are reserved and out of scope).
- Explicit exclusion of metric formula/definition/expression bodies, calculation
  logic, metric or measure values, dataset rows, owner/steward PII, individual
  attributions, free-text, and any PII dataset persistence.

Not owned by this pack:

- The shared semantic vocabulary (metric identity, semantic type, unit, grain,
  aggregation semantics) — owned by `MOD-0004 Metric & Semantic Registry`, bound
  here by readiness state only.
- Curated KPIs — owned by `MOD-0059 KPI Catalog`, the distinct downstream consumer
  this module's readiness feeds.
- Visual composition / scorecards — owned by `MOD-0061 Scorecards / Dashboards`.
- Baselines / experiments — owned by `MOD-0062 Baseline & Experiment
  Measurement`.
- Physical storage, query, and physical binding — owned by `MOD-0063 Data
  Warehouse / Lakehouse`, referenced here by readiness state only.
- Data movement / transformation — owned by `MOD-0064 ETL / ELT Pipelines`.
- Data dictionary and data contract registry ownership beyond dependency/context
  references.
- Secrets/vault, notification, and document ownership beyond dependency/context
  references.
- `Diten.EnterpriseStrategyService` (ESBP) — a consumer of the shared metric
  definitions (goal-scoped strategy metrics), not the owner of this data plane.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` and other facades — consumers
  that own no analytics runtime.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| MetricDefinitionsOwnershipCapabilityBoundary | Governance boundary | Defines what the DKI metric-definition & ownership module may own. |
| MetricDefinitionsOwnershipReadinessBoundary | Governance boundary | Separates readiness metadata from metric-definition authoring/computation. |
| DefinitionCatalogBoundary | Governance boundary | Blocks metric formula/definition/expression body and calculation-logic persistence in the definition catalog. |
| OwnershipAssignmentIntakeBoundary | Governance boundary | Blocks owner/steward PII and individual-attribution persistence at ownership-assignment intake. |
| StewardshipScopeBoundary | Governance boundary | Blocks owner/steward PII / individual-attribution persistence in stewardship scope. |
| ApprovalControlBoundary | Governance boundary | Blocks approval-decision content and reviewer identity persistence in the approval lifecycle. |
| DefinitionReviewBoundary | Governance boundary | Blocks free-text review narrative and review-note body persistence. |
| MetricDefinitionsOwnershipDecisionBoundary | Governance boundary | Blocks scoring, ranking, model-output, and automated decision persistence. |
| MetricDefinitionsOwnershipUxBoundary | Governance boundary | Blocks steward/admin definition-authoring UX beyond readiness-metadata CRUD. |
| MetricDefinitionsOwnershipSensitiveDataBoundary | Governance boundary | Blocks metric formula/definition/expression bodies, calculation logic, metric or measure values, dataset rows, owner/steward PII, individual attributions, and PII persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| MetricDefinitionAuthoringEngine | Deferred runtime | Real metric-definition authoring/execution and definition-body management; not authorized. |
| MetricExpressionExecutionEngine | Deferred runtime | Metric formula/definition/expression execution and calculation logic; not authorized. |
| MetricValueComputationEngine | Deferred runtime | Metric/measure value computation and evaluation; not authorized. |
| OwnershipAttributionEngine | Deferred runtime | Owner/steward identity resolution and individual-attribution delivery; not authorized. |
| MetricDefinitionsOwnershipScoringDecisionEngine | Deferred runtime | Scoring, ranking, model-output, and automated decision behavior; not authorized. |
| StewardAdminDefinitionExperience | Deferred frontend/runtime | Steward/admin definition-authoring UX beyond readiness CRUD; not authorized. |
| MetricDefinitionsOwnershipDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| MetricDefinitionsOwnershipNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| MetricDefinitionsOwnershipReadinessMetadata | Domain entity | Metadata-only readiness contract; `src/Diten.DataKnowledgeService.Domain/Entities/MetricDefinitionsOwnershipReadinessMetadata.cs`. |
| MetricDefinitionsOwnershipReadinessState | Domain enum | Readiness state; `src/Diten.DataKnowledgeService.Domain/Enums/MetricDefinitionsOwnershipReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IMetricDefinitionsOwnershipReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `src/Diten.DataKnowledgeService.Domain/Repositories/IMetricDefinitionsOwnershipReadinessMetadataRepository.cs`. |
| MongoMetricDefinitionsOwnershipReadinessMetadataRepository | Persistence repository | Mongo-backed repository (db `DitenDataKnowledge`); collection `dki_metric_definitions_ownership_readiness`; indexes `ux_dki_metric_definitions_ownership_tenant_code_active` and `ix_dki_metric_definitions_ownership_tenant_state`. |
| MetricDefinitionsOwnership Application features | Application (CQRS/MediatR) | `src/Diten.DataKnowledgeService.Application/Features/MetricDefinitionsOwnership/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| MetricDefinitionsOwnershipController | API controller | Thin controller; `src/Diten.DataKnowledgeService.Api/Controllers/Dki/MetricDefinitionsOwnershipController.cs`. |
| MetricDefinitionsOwnership golden-compact CRUD | Frontend | `Controllers/MetricDefinitionsOwnershipController.cs` + `Views/DataKnowledge/MetricDefinitionsOwnership/**` + `Models/DataKnowledge/MetricDefinitionsOwnership/**` + `wwwroot/assets/js/DataKnowledge/MetricDefinitionsOwnership/**` + Resources + DKI nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract, verified against
`services/Diten.DataKnowledgeService/src/Diten.DataKnowledgeService.Domain/Entities/MetricDefinitionsOwnershipReadinessMetadata.cs`:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `MetricDefinitionsOwnershipReadinessState`):

- `Code`
- `DisplayName`
- `MetricDefinitionsOwnershipReadinessState`
- `DefinitionCatalogBoundaryState`
- `OwnershipAssignmentIntakeBoundaryState`
- `StewardshipScopeBoundaryState`
- `ApprovalControlBoundaryState`
- `DefinitionReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `MetricSemanticRegistrySourceDependencyState`
- `KpiCatalogSourceDependencyState`
- `DataContractRegistryDependencyState`
- `NotificationDependencyState`
- `StewardshipPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `ApprovalPolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `MetricDefinitionsOwnershipReadinessVersion`
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
referencing `MOD-0004` rather than embedding an `expression`/`formula` marker),
and the curation-source dependency field is named `KpiCatalogSourceDependencyState`
(using `KpiCatalogSource*`, referencing `MOD-0059`), so neither collides with the
forbidden marker class. The forbidden-marker matcher uses a word/token boundary
(`\b` regex), not a bare substring `Contains`, so legitimate domain words
(`definition`, `catalog`, `metric`, `ownership`, `stewardship`, `approval`) are
not falsely rejected while true forbidden markers (metric formula/definition/
expression body, calculation logic, metric or measure value, dataset row,
owner/steward PII, individual attribution, `score`, `rating`, `rank`,
`narrative`, PII, payload) are still caught.

Reserved / out-of-scope fields (must not be added in this slice):

- Steward-facing definition fields (steward definition submission, steward review,
  steward action UX state) are reserved and out of scope.
- Admin-facing definition fields (admin definition/authoring administration, admin
  approval, admin self-service UX state) are reserved and out of scope.
- No money field (salary/compensation/cost/budget/target-value). This is a
  metadata-only readiness slice with no monetary attribute.
- No metric formula/definition/expression body, calculation logic, metric or
  measure value, dataset row, owner/steward PII, individual attribution, or PII
  dataset field of any kind.

Forbidden field classes:

- Metric formula/definition/expression body, calculation logic, metric or measure
  value, dataset row/record body, computed metric content, or completed
  definition-output content.
- Readiness score, quality score, rating value, rank, match confidence, model
  output, automated decision result, recommendation output, or evaluator scoring
  payload.
- Owner/steward PII, individual attributions, free-text definition/review
  narrative, HR notes, or sensitive data body.
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

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0060-metric-definitions-ownership.md`
- The `MOD-0060` row in `execution/registries/module-id-registry.md`.

Runtime service:

- `Diten.DataKnowledgeService` (namespace `Diten.DataKnowledgeService.*`, port
  `5062`, Mongo db `DitenDataKnowledge`).

Runtime repo scope:

- `services/Diten.DataKnowledgeService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (`Controllers/MetricDefinitionsOwnershipController.cs`,
  `Views/DataKnowledge/MetricDefinitionsOwnership/**`,
  `Models/DataKnowledge/MetricDefinitionsOwnership/**`,
  `wwwroot/assets/js/DataKnowledge/MetricDefinitionsOwnership/**`, Resources, DKI
  nav entry at `/DataKnowledge/MetricDefinitionsOwnership`).
- Gateway route `/api/metric-definitions-ownership` exposure through the API
  gateway (`5000` -> DKI `5062`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.DataKnowledgeService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned MetricDefinitionsOwnership CRUD objects listed
  in §3/§5.
- `gateway/**` outside the owned `/api/metric-definitions-ownership` readiness
  route.
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.Platform/**`
- `services/Diten.HumanCapitalService/**`
- global `tests/**`
- the other analytics-backbone module packs (`MOD-0004`, `MOD-0059`,
  `MOD-0061`–`MOD-0064`).
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/data-knowledge-intelligence/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Analytics-backbone relationships:

- Depends on: `MOD-0004 Metric & Semantic Registry` — every concrete definition
  binds to the shared semantic vocabulary authored there; its availability is
  treated as boundary/readiness metadata context, consumed via
  `MetricSemanticRegistrySourceDependencyState`, never the vocabulary body itself.
- Depends on: `MOD-0063 Data Warehouse / Lakehouse` — each definition maps onto
  the physical models/binding owned there; its availability is treated as
  boundary/readiness metadata context, never the physical data itself.
- Consumed by: `MOD-0059 KPI Catalog` — curates KPIs from these definitions; its
  consumption is treated as boundary/readiness metadata context, consumed here via
  `KpiCatalogSourceDependencyState`, not as a definition-body source.
- Consumed by: `MOD-0061 Scorecards / Dashboards` — composes definitions into
  visual surfaces; consumption is boundary/readiness metadata context only.
- Consumed by: `MOD-0062 Baseline & Experiment Measurement` — measures against
  these definitions; consumption is boundary/readiness metadata context only.

Runtime dependency-state context (metadata-only, via boundary/dependency states):

- Metric & Semantic Registry Source (the `MOD-0004` shared semantic vocabulary),
  consumed via `MetricSemanticRegistrySourceDependencyState` (field named
  `MetricSemanticRegistrySource*`, marker-safe). Registry-source availability is a
  controlling precondition and is treated as boundary/readiness metadata only; no
  metric expression or definition body is persisted.
- KPI Catalog Source (the `MOD-0059` curation plane), consumed via
  `KpiCatalogSourceDependencyState` (field named `KpiCatalogSource*`). Catalog-
  source availability is a controlling precondition; no KPI value or dataset row is
  persisted.
- Data Contract Registry, consumed via `DataContractRegistryDependencyState`.
- Notification, consumed via `NotificationDependencyState`.

Consumer context (not dependencies of this module, they depend on / consume it):

- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped strategy metric runtime.
- HCM `CAND-CAP-0034` HR KPI & Analytics Facade.

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is authorized
only for the first metadata-only backend/API readiness slice, along with the
golden-compact readiness CRUD frontend and gateway route.

Authorized runtime boundary:

- Runtime owner/key: `dki.metric-definitions-ownership`.
- Permission namespace:
  - `dki.metric-definitions-ownership.read`
  - `dki.metric-definitions-ownership.manage`
  - `dki.metric-definitions-ownership.evaluate`
  - `dki.metric-definitions-ownership.audit.read`
- Runtime repo scope: `services/Diten.DataKnowledgeService/**`.
- Thin controller
  (`src/Diten.DataKnowledgeService.Api/Controllers/Dki/MetricDefinitionsOwnershipController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository (db `DitenDataKnowledge`); collection
  `dki_metric_definitions_ownership_readiness`.
- Active tenant `Code` uniqueness through
  `ux_dki_metric_definitions_ownership_tenant_code_active`, with
  `ix_dki_metric_definitions_ownership_tenant_state` supporting tenant/state
  queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`definition`,
  `catalog`, `metric`, `ownership`, `stewardship`, `approval`) are not falsely
  rejected while true forbidden markers (metric formula/definition/expression
  body, calculation logic, metric or measure value, dataset row, owner/steward
  PII, individual attribution, `score`, `rating`, `rank`, `narrative`, PII,
  payload) are still caught. Field names are deliberately marker-safe (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and catalog-source dependency
  fields are `MetricSemanticRegistrySourceDependencyState` and
  `KpiCatalogSourceDependencyState`), enforced by the contract field-name test.

Explicitly unauthorized:

- Real metric-definition authoring/execution.
- Metric formula/definition/expression body persistence.
- Calculation-logic persistence.
- Metric/measure value computation and persistence.
- Dataset-row persistence.
- Owner/steward PII and individual-attribution persistence.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Steward/admin definition-authoring UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Metric formula/definition/expression bodies, calculation logic, metric or
  measure values, dataset rows, owner/steward PII, individual attributions, or PII
  payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract, at
`/DataKnowledge/MetricDefinitionsOwnership`.

Authorized UX:

- DKI tenant shell nav entry `Metric Definitions & Ownership`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Steward definition UX beyond readiness-metadata CRUD.
- Admin definition/authoring administration UX beyond readiness-metadata CRUD.
- Definition catalog, ownership assignment, stewardship scope, approval control,
  definition review, scoring, or model-output UI.
- Document, notification, export, upload, or attachment UI.
- Any metric formula/definition/expression body, calculation logic, metric or
  measure value, or dataset-row viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5000` -> DKI `5062`).

Authorized API surface:

- `GET /api/metric-definitions-ownership` - list readiness metadata.
- `GET /api/metric-definitions-ownership/{id}` - get readiness metadata by id.
- `POST /api/metric-definitions-ownership` - create readiness metadata.
- `POST /api/metric-definitions-ownership/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/metric-definitions-ownership/{id}` - soft-delete.
- `GET /api/metric-definitions-ownership/{id}/audit-metadata` - audit metadata
  read.

The first slice must stay limited to:

- Create/list/get metric-definition & ownership readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No metric-definition authoring/execution, metric formula/definition/expression
  body persistence, calculation logic, metric/measure value computation, dataset-
  row persistence, scoring/model-output persistence, automated decision behavior,
  notification/document integration, or owner-steward-PII/individual-attribution/
  PII persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only metric-definition & ownership readiness state.
- Boundary-state metadata for definition catalog, ownership-assignment intake,
  stewardship scope, approval control, definition review, automated decision,
  stewardship, minimization, retention, and approval policy.
- Dependency-state metadata for Metric & Semantic Registry Source
  (`MetricSemanticRegistrySourceDependencyState`), KPI Catalog Source
  (`KpiCatalogSourceDependencyState`), data contract registry, and notification
  dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Metric-definition authoring/computation payload.
- Metric or measure value.
- Metric formula/definition/expression body.
- Calculation logic.
- Dataset row / record body or computed-metric output.
- Definition-review narrative or review-note body.
- Readiness score/quality score/rating/rank/match confidence/model output.
- Automated decision result.
- Owner/steward PII or individual attribution.
- Connection string, credential/token/secret/password value.
- Attachment payload.
- Raw provider payload.
- PII-heavy dataset/record details.
- Compensation/payroll/target-value payload.

Data-plane note: this module is a **definition/authoring layer** over the metric
stack, and this slice declares readiness STATE only. No metric formula/definition/
expression body, calculation logic, metric or measure value, dataset row,
owner/steward PII, or individual attribution of any kind may cross the boundary
into persistence. Only boundary/readiness STATE metadata governed by stewardship
precondition, data minimization, retention, and approval policy is permitted,
keeping the strong analytics-governance concerns (definition-metadata
minimization, tenant isolation of definition entries, curated-vs-tenant scope of
definition identity) resolved as metadata-only state.

## 12. Permission Boundary

Approved permission namespace:

- `dki.metric-definitions-ownership.read`
- `dki.metric-definitions-ownership.manage`
- `dki.metric-definitions-ownership.evaluate`
- `dki.metric-definitions-ownership.audit.read`

Runtime owner key: `dki.metric-definitions-ownership`. The canonical module ID
`MOD-0060` is a governance/documentation identity and must not be used as a
runtime literal, owner key, or permission namespace fragment.

## 13. Tenant / Security Boundary

Runtime work is tenant-aware and fails closed. The backend/API slice resolves
tenant server-side and does not expose `TenantId` in request DTOs.

Security guardrails:

- Canonical identity (`MOD-0060`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_dki_metric_definitions_ownership_tenant_code_active` and
  `ix_dki_metric_definitions_ownership_tenant_state`.
- Because metric definitions may be partly platform-global (shared baseline
  definitions) with tenant overlays, tenant isolation across definition entries
  and their bound semantic/physical references is a first-class control even for
  readiness metadata; no metric/measure value, definition body, owner/steward PII,
  or individual attribution is persisted, and curated-vs-tenant definition scope
  remains a governed readiness concern.

## 14. Compliance / Privacy Boundary

Legal/privacy/stewardship is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only stewardship/precondition representation, with a valid stewardship
  basis treated as a precondition to activation (`StewardshipPreconditionState`).
- Data minimization fail-closed behavior (`DataMinimizationState`): store only
  readiness state; prefer aggregated/derived over raw definition bodies or
  owner/steward PII.
- Retention and approval policy as local/deferred metadata
  (`RetentionPolicyState`, `ApprovalPolicyState`), covering the defined lifecycle,
  purge policy, and approval posture for metric-definition & ownership readiness.
- Exclusion of metric formula/definition/expression bodies, calculation logic,
  metric or measure values, dataset rows, owner/steward PII, individual
  attributions, and any PII dataset.

The Metric Definitions & Ownership capability declares readiness STATE only; it
does not own or store any metric formula/definition/expression bodies, calculation
logic, metric or measure values, dataset rows, owner/steward PII, or individual
attributions, and all downstream analytics-backbone consumers must treat its
output as boundary/readiness metadata, not as a definition-body or metric-value
source.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, or document/
notification UI is authorized. `MOD-0004 Metric & Semantic Registry` and
`MOD-0063 Data Warehouse / Lakehouse` are consumed only as upstream
dependency-state context (`MetricSemanticRegistrySourceDependencyState` and
physical-binding readiness); `MOD-0059 KPI Catalog` is consumed as
`KpiCatalogSourceDependencyState`; data contract registry and notification are
consumed only as dependency-state context; `MOD-0059`, `MOD-0061`, and `MOD-0062`
are downstream consumer references; and ESBP and HCM `CAND-CAP-0034` remain
non-owning consumers.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `runtime-authored`.
2. AC-02: Canonical identity is `MOD-0060` (Blueprint-backed; used directly, not
   a candidate) as **Metric Definitions & Ownership**.
3. AC-03: Runtime owner/key is `dki.metric-definitions-ownership`.
4. AC-04: Permission namespace is limited to
   `dki.metric-definitions-ownership.read`,
   `dki.metric-definitions-ownership.manage`,
   `dki.metric-definitions-ownership.evaluate`, and
   `dki.metric-definitions-ownership.audit.read`.
5. AC-05: Runtime repo scope is limited to
   `services/Diten.DataKnowledgeService/**`; owning service is
   `Diten.DataKnowledgeService` (port `5062`, Mongo db `DitenDataKnowledge`).
6. AC-06: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3), exposed through the gateway route
   `/api/metric-definitions-ownership` (`5000` -> DKI `5062`).
7. AC-07: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
8. AC-08: Mongo persistence (collection `dki_metric_definitions_ownership_readiness`)
   includes active tenant `Code` uniqueness
   (`ux_dki_metric_definitions_ownership_tenant_code_active`), tenant/state
   indexing (`ix_dki_metric_definitions_ownership_tenant_state`), soft delete, and
   tenant isolation (E4).
9. AC-09: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
   records.
10. AC-10: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
11. AC-11: RBAC is enforced server-side across the controller surface.
12. AC-12: Analytics-backbone dependency boundaries are recorded (`MOD-0004` as
    upstream semantic vocabulary via `MetricSemanticRegistrySourceDependencyState`;
    `MOD-0063` as physical-binding dependency; `MOD-0059` as curation source via
    `KpiCatalogSourceDependencyState` and downstream consumer; `MOD-0061` and
    `MOD-0062` as downstream consumers; data contract registry and notification as
    dependency-state context; ESBP and HCM `CAND-CAP-0034` as non-owning
    consumers).
13. AC-13: Real metric-definition authoring/execution, metric formula/definition/
    expression body persistence, calculation-logic persistence, metric/measure
    value computation, dataset-row persistence, owner/steward PII / individual-
    attribution persistence, model output, and automated decision behavior remain
    unauthorized.
14. AC-14: Steward/admin definition fields remain reserved/out of scope; no money
    field is present; no metric formula/definition/expression body, calculation
    logic, metric or measure value, dataset row, owner/steward PII, or individual
    attribution field is present.
15. AC-15: Metric formula/definition/expression bodies, calculation logic, metric
    or measure values, dataset rows, owner/steward PII, individual attributions,
    scores/ratings/ranks, credential/token/secret/password, and PII-heavy
    persistence remain unauthorized; field names are marker-safe (no
    `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
    fragment in any property name; the registry-source and catalog-source
    dependency fields are `MetricSemanticRegistrySourceDependencyState` and
    `KpiCatalogSourceDependencyState`) and the field-name test passes under the
    `\b` word-boundary matcher.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: `tr`/`en` localization parity is present.
18. AC-18: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
19. AC-19: Runtime literal scan for `MOD-0060` passes under
    runtime/frontend/gateway/test paths (module IDs are governance-only).

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: runtime-authored`.
- Confirm all 20 sections are present.
- Build DKI API:
  `dotnet build services/Diten.DataKnowledgeService/src/Diten.DataKnowledgeService.Api/Diten.DataKnowledgeService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `MetricDefinitionsOwnership` filter
  (`Application.Tests/MetricDefinitionsOwnershipTests.cs`, handler behavior;
  fix-absent should be RED).
- Run full DKI Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_dki_metric_definitions_ownership_tenant_code_active`), and tenant/state
  indexing (`ix_dki_metric_definitions_ownership_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`definition`, `catalog`,
  `metric`, `ownership`, `stewardship`, `approval`) are not falsely rejected.
- Verify the contract field-name test confirms marker-safe field naming (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and catalog-source
  dependency fields are `MetricSemanticRegistrySourceDependencyState` and
  `KpiCatalogSourceDependencyState`).
- Verify forbidden definition-catalog/ownership-intake/stewardship/approval/
  review/decision/sensitive fields are absent.
- Verify steward/admin definition fields are reserved/absent, no money field
  exists, and no metric formula/definition/expression body, calculation logic,
  metric or measure value, dataset row, owner/steward PII, or individual
  attribution field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "MOD-0060" services frontend gateway`
  returns no runtime literal (module IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a runtime-authored slice.
- [x] Blueprint-canonical identity accepted (`MOD-0060`, used directly) as Metric
  Definitions & Ownership.
- [x] Candidate gate script not-executable note is accepted.
- [x] EA runtime authorization accepted: domain `data-knowledge-intelligence` and
  dedicated data-plane service `Diten.DataKnowledgeService` assigned per
  `WP-DKI-SLICE-0060`.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real metric-definition authoring/execution prohibition is accepted.
- [x] Metric formula/definition/expression body / calculation logic / metric-or-
  measure value / dataset-row persistence prohibition is accepted.
- [x] Owner/steward PII / individual-attribution persistence prohibition is
  accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved steward/admin definition fields, no-money-field, and
  no-metric-formula-definition-expression-body/calculation-logic/metric-or-measure-
  value/dataset-row/owner-steward-PII/individual-attribution constraints are
  accepted.
- [x] Sensitive/PII payload prohibition is accepted.
- [x] Marker-safe field naming (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; registry-source and catalog-source dependency
  fields `MetricSemanticRegistrySourceDependencyState`/`KpiCatalogSourceDependencyState`)
  with `\b` word-boundary matcher is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Analytics-backbone (`MOD-0004`, `MOD-0059`, `MOD-0061`–`MOD-0064`), ESBP,
  and HCM dependency/consumer context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA runtime authorization / canonical MOD runtime assignment.
- [x] Runtime owner/key decision (`dki.metric-definitions-ownership`).
- [x] Permission namespace decision.
- [x] Runtime repo scope decision (`services/Diten.DataKnowledgeService/**`).
- [x] Minimal metadata-only metric-definition & ownership readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/stewardship metadata-only waiver.
- [x] Data minimization policy.
- [x] Retention / approval / evidence / deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend and
  gateway route are scoped under `services/Diten.DataKnowledgeService/**` and the
  owned `frontend/Diten.Web/**` MetricDefinitionsOwnership objects.
- Runtime owner/key remains limited to `dki.metric-definitions-ownership`.
- Permission namespace remains limited to `dki.metric-definitions-ownership.read`,
  `dki.metric-definitions-ownership.manage`,
  `dki.metric-definitions-ownership.evaluate`, and
  `dki.metric-definitions-ownership.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract and verified against
  `MetricDefinitionsOwnershipReadinessMetadata.cs`.
- `MetricSemanticRegistrySourceDependencyState`, `KpiCatalogSourceDependencyState`,
  `DataContractRegistryDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states; no property name carries a
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment.
- Steward/admin definition UX fields remain reserved/out of scope, no money field
  is present, and no metric formula/definition/expression body, calculation logic,
  metric or measure value, dataset row, owner/steward PII, or individual
  attribution field is present.
- ESBP, HCM, and Platform scopes remain closed.
- Real metric-definition authoring/execution, metric formula/definition/expression
  body persistence, calculation logic, metric/measure value computation, dataset-
  row persistence, model output, automated decision behavior,
  notification/document integration, owner/steward PII, individual attributions,
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
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0060`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted the runtime-authorization revert of the prior
  governance-first deferral: the DCP-008 "Analytics backbone — Pending EA domain
  mapping" note is resolved by assigning domain `data-knowledge-intelligence` and
  the dedicated data-plane service `Diten.DataKnowledgeService` per
  `WP-DKI-SLICE-0060`, rather than defaulting to `Diten.EnterpriseStrategyService`
  or `Diten.Platform`.
- EA/registry owner accepted verifier absence as a governance note.
- EA runtime policy waiver approved for the first metadata-only backend/API
  readiness slice (K19-complete), with golden-compact readiness CRUD frontend and
  gateway route in scope. `MOD-0060` is Blueprint-canonical and its DCP-002
  canonical registry row is reserved and used directly (no candidate identity).
  The DCP-008 reservation language (registry row + module pack required before
  runtime) is satisfied and superseded by this runtime authorization.
- Legal/privacy/stewardship metadata-only waiver approved.
- Data minimization fail-closed policy approved.
- Retention / approval / evidence / deletion local/deferred metadata policy
  approved.
- Marker-safe field-naming waiver approved: field names deliberately avoid
  forbidden-marker fragments (no
  `score`/`rating`/`rank`/`narrative`/`credential`/`secret`/`password`/`token`
  fragment in any property name; the registry-source and catalog-source dependency
  fields are `MetricSemanticRegistrySourceDependencyState` and
  `KpiCatalogSourceDependencyState`), enforced by the contract field-name test
  using the `\b` word-boundary matcher.

Runtime literal scan:

- Required command:
  `rg -n "MOD-0060" services frontend gateway`
- Module IDs are governance-only and must return no runtime literal match; the
  runtime uses owner key `dki.metric-definitions-ownership` and gateway route
  `/api/metric-definitions-ownership`, never the MOD ID.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real metric-definition authoring/execution.
- Metric formula/definition/expression body management and calculation logic.
- Metric/measure value computation and evaluation.
- Ownership/stewardship assignment and reassignment lineage.
- Definition approval and lifecycle transitions (draft/active/deprecated).
- Semantic-binding execution against `MOD-0004` vocabulary.
- Physical-binding execution against `MOD-0063` warehouse models.
- KPI catalog curation consumption (`MOD-0059`).
- Scorecard/dashboard composition consumption (`MOD-0061`).
- Baseline/experiment measurement consumption (`MOD-0062`).
- Automated decision approval.
- Steward/admin metric-definition UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
