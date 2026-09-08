---
id: MOD-0063
name: Data Warehouse / Lakehouse
domain: data-knowledge-intelligence
blueprint_domain: Data, Knowledge & Intelligence
service: Diten.DataKnowledgeService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: runtime-authored
owner: enterprise-architect / dki-domain-owner / data-platform-owner / platform-team / security-owner
branch: feature/dki/mod-0063-data-warehouse-lakehouse
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
runtime_owner_key: dki.data-warehouse-lakehouse
permission_namespace:
  - dki.data-warehouse-lakehouse.read
  - dki.data-warehouse-lakehouse.manage
  - dki.data-warehouse-lakehouse.evaluate
  - dki.data-warehouse-lakehouse.audit.read
runtime_repo_scope: services/Diten.DataKnowledgeService/**
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
---

# MOD-0063 - Data Warehouse / Lakehouse

> Status: runtime-authored. This pack records the first metadata-only backend/API
> data warehouse / lakehouse readiness contract slice under
> `services/Diten.DataKnowledgeService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5000` -> DKI `5062`). `MOD-0063` is **Blueprint-canonical** (Blueprint
> domain **Data, Knowledge & Intelligence**); the canonical MOD ID is used
> directly and remains a governance/documentation identity that must not be
> written into runtime literals. The governance-first runtime deferral recorded in
> the prior draft is **REVERTED**: per `WP-DKI-SLICE-0063`, EA assigned the repo
> domain `data-knowledge-intelligence` and the dedicated data-plane runtime
> service `Diten.DataKnowledgeService` (namespace `Diten.DataKnowledgeService.*`,
> port `5062`, Mongo db `DitenDataKnowledge`, permission owner key `dki.*`). Real
> warehouse/lakehouse storage and query, dataset/table/warehouse rows or column
> values, raw ingested records, query results, connection strings or storage
> credentials, partition/lineage payloads, model output or automated decision
> behavior, and sensitive/PII-heavy persistence remain closed. The legacy
> mis-numbered `MOD-0008` "Data Warehouse" name collision is superseded and is not
> this module.

## 1. Module Summary

`MOD-0063` is the Blueprint-canonical **Data Warehouse / Lakehouse**: the physical
**data-plane foundation** of the analytics backbone — the readiness layer for the
warehouse / lakehouse storage and query substrate that `MOD-0004` semantic
bindings and `MOD-0060` metric definitions map onto.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
which records this module as part of the "Analytics backbone" and originally
marked its domain ownership as "Pending EA domain mapping" with no existing
service. That deferral is now resolved: EA assigned the repo domain
`data-knowledge-intelligence` and stood up a dedicated data-plane runtime service
(`Diten.DataKnowledgeService`) to own this cross-cutting data infrastructure,
rather than defaulting it to `Diten.EnterpriseStrategyService` or
`Diten.Platform`. This pack records the first metadata-only backend/API readiness
contract slice authored under `WP-DKI-SLICE-0063`, mirroring the completed
readiness slices exactly.

The Data Warehouse / Lakehouse capability is the foundational readiness layer for
the analytical data plane: it declares the readiness STATE of the storage layer
catalog, the ingestion intake boundary, partitioning scope, lineage control, and
warehouse review. It underpins downstream analytics-backbone modules by
publishing the readiness of the shared data-plane substrate. Real
warehouse/lakehouse storage and query, dataset/table/warehouse rows or column
values, raw ingested records, query results, connection strings or storage
credentials, partition/lineage payloads, model output, automated decision
behavior, and sensitive/PII-heavy persistence remain closed.

## 2. Ownership and Boundaries

Blueprint-reconciliation / name-collision context: during blueprint
reconciliation, a legacy mis-numbered `MOD-0008` was name-matched to "Data
Warehouse". **`MOD-0063` is the canonical Data Warehouse / Lakehouse identity**;
the legacy `MOD-0008` name collision is superseded and is not this module.

Owned by this pack:

- DKI-native data warehouse / lakehouse governance boundary.
- Sequencing as the physical data-plane root of the analytics-backbone group
  (`MOD-0004`, `MOD-0059`–`MOD-0064`).
- Metadata-only data warehouse / lakehouse readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the storage layer catalog, ingestion intake,
  partitioning scope, lineage control, and warehouse review, consumed by
  downstream analytics-backbone modules.
- Explicit exclusion of real warehouse/lakehouse storage and query execution.
- Explicit exclusion of ingestion/ETL execution, partitioning execution, lineage
  capture execution, warehouse-review execution, model output, and automated
  decision behavior.
- Explicit exclusion of steward-facing and admin-facing warehouse UX beyond the
  readiness-metadata CRUD (those fields are reserved and out of scope).
- Explicit exclusion of dataset/table/warehouse rows, column values, raw ingested
  records, query results, connection strings / storage credentials,
  partition/lineage payloads, and any PII dataset persistence.

Not owned by this pack:

- The physical storage / query substrate execution itself (real warehouse or
  lakehouse tables, files, or query engine runs).
- The semantic vocabulary — owned by `MOD-0004 Metric & Semantic Registry`.
- Concrete metric definitions, formulas, thresholds — owned by `MOD-0060 Metric
  Definitions & Ownership`.
- Curated KPIs — owned by `MOD-0059 KPI Catalog`.
- Visual composition / scorecards — owned by `MOD-0061 Scorecards / Dashboards`.
- Baselines / experiments — owned by `MOD-0062 Baseline & Experiment
  Measurement`.
- Data movement / transformation (ETL/ELT) — owned by `MOD-0064 ETL / ELT
  Pipelines`, a distinct upstream module that feeds this substrate.
- Source operational databases (the systems of record that feed the warehouse).
- Secrets/vault, logging/monitoring, and data-contract-registry ownership beyond
  dependency/context references.
- `Diten.EnterpriseStrategyService` (ESBP) — a consumer of analytics, not the
  owner of this data plane.
- HCM's `CAND-CAP-0034 HR KPI & Analytics Facade` and other facades — consumers
  that own no analytics runtime.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| DataWarehouseLakehouseCapabilityBoundary | Governance boundary | Defines what the DKI data warehouse / lakehouse module may own. |
| DataWarehouseLakehouseReadinessBoundary | Governance boundary | Separates readiness metadata from warehouse/lakehouse storage/query execution. |
| StorageLayerCatalogBoundary | Governance boundary | Blocks raw storage layer catalog / dataset / table / column persistence. |
| IngestionIntakeBoundary | Governance boundary | Blocks real ingestion/ETL intake execution and raw ingested record persistence. |
| PartitioningScopeBoundary | Governance boundary | Blocks partition execution and partition/layout payload persistence. |
| LineageControlBoundary | Governance boundary | Blocks lineage capture runtime, provenance storage, and lineage payload persistence. |
| WarehouseReviewBoundary | Governance boundary | Blocks warehouse-review execution, query result, and review output persistence. |
| DataWarehouseLakehouseDecisionBoundary | Governance boundary | Blocks scoring, ranking, model-output, and automated decision persistence. |
| DataWarehouseLakehouseUxBoundary | Governance boundary | Blocks steward/admin warehouse UX beyond readiness-metadata CRUD. |
| DataWarehouseLakehouseSensitiveDataBoundary | Governance boundary | Blocks dataset/table rows, column values, raw records, query results, connection strings / storage credentials, partition/lineage payloads, and PII persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| WarehouseStorageQueryEngine | Deferred runtime | Real warehouse/lakehouse storage and query execution; not authorized. |
| IngestionIntakeEngine | Deferred runtime | Real ingestion/ETL intake, streaming, and batch load; not authorized. |
| PartitioningEngine | Deferred runtime | Partition/layout execution and optimization; not authorized. |
| LineageCaptureEngine | Deferred runtime | Lineage capture, provenance graph, and transformation logs; not authorized. |
| WarehouseReviewEngine | Deferred runtime | Warehouse-review execution, query runs, and review output; not authorized. |
| StewardAdminWarehouseExperience | Deferred frontend/runtime | Steward/admin warehouse UX beyond readiness CRUD; not authorized. |
| WarehouseDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| WarehouseNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| DataWarehouseLakehouseReadinessMetadata | Domain entity | Metadata-only readiness contract; `src/Diten.DataKnowledgeService.Domain/Entities/DataWarehouseLakehouseReadinessMetadata.cs`. |
| DataWarehouseLakehouseReadinessState | Domain enum | Readiness state; `src/Diten.DataKnowledgeService.Domain/Enums/DataWarehouseLakehouseReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IDataWarehouseLakehouseReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `src/Diten.DataKnowledgeService.Domain/Repositories/IDataWarehouseLakehouseReadinessMetadataRepository.cs`. |
| MongoDataWarehouseLakehouseReadinessMetadataRepository | Persistence repository | Mongo-backed repository (db `DitenDataKnowledge`); collection `dki_data_warehouse_lakehouse_readiness`; indexes `ux_dki_data_warehouse_lakehouse_tenant_code_active` and `ix_dki_data_warehouse_lakehouse_tenant_state`. |
| DataWarehouseLakehouse Application features | Application (CQRS/MediatR) | `src/Diten.DataKnowledgeService.Application/Features/DataWarehouseLakehouse/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| DataWarehouseLakehouseController | API controller | Thin controller; `src/Diten.DataKnowledgeService.Api/Controllers/Dki/DataWarehouseLakehouseController.cs`. |
| DataWarehouseLakehouse golden-compact CRUD | Frontend | `Controllers/DataWarehouseLakehouseController.cs` + `Views/DataKnowledge/DataWarehouseLakehouse/**` + `Models/DataKnowledge/DataWarehouseLakehouse/**` + `wwwroot/assets/js/DataKnowledge/DataWarehouseLakehouse/**` + Resources + DKI nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `DataWarehouseLakehouseReadinessState`):

- `Code`
- `DisplayName`
- `DataWarehouseLakehouseReadinessState`
- `StorageLayerCatalogBoundaryState`
- `IngestionIntakeBoundaryState`
- `PartitioningScopeBoundaryState`
- `LineageControlBoundaryState`
- `WarehouseReviewBoundaryState`
- `AutomatedDecisionBoundaryState`
- `VaultDependencyState`
- `LoggingMonitoringDependencyState`
- `DataContractRegistryDependencyState`
- `NotificationDependencyState`
- `StewardshipPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `StorageTierPolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `DataWarehouseLakehouseReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Field-naming discipline (marker-safe): field names are deliberately chosen to
avoid forbidden-marker fragments so the contract field-name test does not falsely
reject legitimate domain fields. In particular, the Secrets Vault dependency
field is named `VaultDependencyState` (using `Vault*`, **not** `Secret*`), so it
does not collide with the forbidden `secret`/`credential` marker class. The
forbidden-marker matcher uses a word/token boundary (`\b` regex), not a bare
substring `Contains`, so legitimate domain words (`warehouse`, `lakehouse`,
`storage`, `ingestion`, `partition`, `lineage`, `catalog`) are not falsely
rejected while true forbidden markers (dataset/table row body, column value, raw
record, query result, connection string, storage credential, PII, payload) are
still caught.

Reserved / out-of-scope fields (must not be added in this slice):

- Steward-facing warehouse fields (steward catalog submission, steward review,
  steward action UX state) are reserved and out of scope.
- Admin-facing warehouse fields (admin storage/query administration, admin
  approval, admin self-service UX state) are reserved and out of scope.
- No money field (salary/compensation/cost/budget/storage-cost). This is a
  metadata-only readiness slice with no monetary attribute.
- No dataset/table/warehouse row, column value, raw ingested record, query
  result, connection string, storage credential, partition/lineage payload, or
  PII dataset field of any kind.

Forbidden field classes:

- Dataset/table/warehouse record body, column value, storage-layer catalog
  payload, ingestion/ETL payload, partition/layout payload, lineage/provenance
  payload, query result, or completed warehouse content.
- Readiness score, quality score, rating value, rank, match confidence, model
  output, automated decision result, recommendation output, or evaluator scoring
  payload.
- Raw ingested records, query results, free-text warehouse narrative, HR notes,
  or sensitive data body.
- Attachment payload, controlled document body, external document content, raw
  evidence, raw provider payload, or uploaded file metadata.
- Connection string, storage credential, credential, token, secret, password,
  activation code, provider connection value, device secret, or account bootstrap
  secret.
- Salary amount, compensation amount, storage-cost amount, benefits election,
  payroll details, tax details, bank details, or payment instruction fields.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  any PII-heavy dataset/record field.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/data-knowledge-intelligence/module-packs/MOD-0063-data-warehouse-lakehouse.md`
- The `MOD-0063` row in `execution/registries/module-id-registry.md`.

Runtime service:

- `Diten.DataKnowledgeService` (namespace `Diten.DataKnowledgeService.*`, port
  `5062`, Mongo db `DitenDataKnowledge`).

Runtime repo scope:

- `services/Diten.DataKnowledgeService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (`Controllers/DataWarehouseLakehouseController.cs`,
  `Views/DataKnowledge/DataWarehouseLakehouse/**`,
  `Models/DataKnowledge/DataWarehouseLakehouse/**`,
  `wwwroot/assets/js/DataKnowledge/DataWarehouseLakehouse/**`, Resources, DKI nav
  entry at `/DataKnowledge/DataWarehouseLakehouse`).
- Gateway route `/api/data-warehouse-lakehouse` exposure through the API gateway
  (`5000` -> DKI `5062`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.DataKnowledgeService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned DataWarehouseLakehouse CRUD objects listed in
  §3/§5.
- `gateway/**` outside the owned `/api/data-warehouse-lakehouse` readiness route.
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.Platform/**`
- `services/Diten.HumanCapitalService/**`
- global `tests/**`
- the other analytics-backbone module packs (`MOD-0004`, `MOD-0059`–`MOD-0062`,
  `MOD-0064`).
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/data-knowledge-intelligence/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Analytics-backbone relationships:

- Fed by: `MOD-0064 ETL / ELT Pipelines` (ingests/transforms source data into
  the warehouse/lakehouse models). Its output is treated as boundary/readiness
  metadata context, not as a raw data source.
- Consumed by: `MOD-0060 Metric Definitions & Ownership` (physical binding of
  metric definitions) and `MOD-0062 Baseline & Experiment Measurement`
  (measurement data).
- Bound onto by: the semantic bindings of `MOD-0004 Metric & Semantic Registry`
  target this physical substrate.

Runtime dependency-state context (metadata-only, via boundary/dependency states):

- Secrets Vault, consumed via `VaultDependencyState` (field named `Vault*`,
  marker-safe). Vault availability is a controlling precondition and is treated
  as boundary/readiness metadata only; no connection string or storage credential
  is persisted.
- Logging & Monitoring, consumed via `LoggingMonitoringDependencyState`.
- Data Contract Registry, consumed via `DataContractRegistryDependencyState`.
- Notification, consumed via `NotificationDependencyState`.

Consumer context (not dependencies of this module, they depend on / consume it):

- `Diten.EnterpriseStrategyService` (ESBP) goal-scoped KPI/metric runtime.
- HCM `CAND-CAP-0034` HR KPI & Analytics Facade.

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is authorized
only for the first metadata-only backend/API readiness slice, along with the
golden-compact readiness CRUD frontend and gateway route.

Authorized runtime boundary:

- Runtime owner/key: `dki.data-warehouse-lakehouse`.
- Permission namespace:
  - `dki.data-warehouse-lakehouse.read`
  - `dki.data-warehouse-lakehouse.manage`
  - `dki.data-warehouse-lakehouse.evaluate`
  - `dki.data-warehouse-lakehouse.audit.read`
- Runtime repo scope: `services/Diten.DataKnowledgeService/**`.
- Thin controller
  (`src/Diten.DataKnowledgeService.Api/Controllers/Dki/DataWarehouseLakehouseController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository (db `DitenDataKnowledge`); collection
  `dki_data_warehouse_lakehouse_readiness`.
- Active tenant `Code` uniqueness through
  `ux_dki_data_warehouse_lakehouse_tenant_code_active`, with
  `ix_dki_data_warehouse_lakehouse_tenant_state` supporting tenant/state queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`warehouse`,
  `lakehouse`, `storage`, `ingestion`, `partition`, `lineage`, `catalog`) are not
  falsely rejected while true forbidden markers (dataset/table row body, column
  value, raw record, query result, connection string, storage credential, PII,
  payload) are still caught. Field names are deliberately marker-safe (the
  Secrets Vault dependency field is `VaultDependencyState`, using `Vault*` not
  `Secret*`), enforced by the contract field-name test.

Explicitly unauthorized:

- Real warehouse/lakehouse storage and query execution.
- Storage layer catalog / dataset / table / column persistence.
- Ingestion/ETL intake execution, streaming, or batch load.
- Partition/layout execution and partition payload persistence.
- Lineage capture runtime, provenance graph storage, or transformation logs.
- Warehouse-review execution, query runs, or query result persistence.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Steward/admin warehouse UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Dataset/table/warehouse rows, column values, raw ingested records, query
  results, connection strings / storage credentials, partition/lineage payloads,
  or PII payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract, at `/DataKnowledge/DataWarehouseLakehouse`.

Authorized UX:

- DKI tenant shell nav entry `Data Warehouse / Lakehouse`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Steward warehouse UX beyond readiness-metadata CRUD.
- Admin storage/query administration UX beyond readiness-metadata CRUD.
- Storage layer catalog, ingestion, partitioning, lineage, warehouse-review,
  scoring, or model-output UI.
- Document, notification, export, upload, or attachment UI.
- Any dataset/table/warehouse row, column value, raw ingested record, query
  result, or connection-string/credential viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5000` -> DKI `5062`).

Authorized API surface:

- `GET /api/data-warehouse-lakehouse` - list readiness metadata.
- `GET /api/data-warehouse-lakehouse/{id}` - get readiness metadata by id.
- `POST /api/data-warehouse-lakehouse` - create readiness metadata.
- `POST /api/data-warehouse-lakehouse/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/data-warehouse-lakehouse/{id}` - soft-delete.
- `GET /api/data-warehouse-lakehouse/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get data warehouse / lakehouse readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No warehouse/lakehouse storage and query, storage layer catalog record
  persistence, ingestion/ETL intake, partitioning execution, lineage capture,
  warehouse-review/query-result persistence, scoring/model-output persistence,
  automated decision behavior, notification/document integration, or
  raw-record/column-value/connection-string/storage-credential/partition/lineage/
  PII payload persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only data warehouse / lakehouse readiness state.
- Boundary-state metadata for storage layer catalog, ingestion intake,
  partitioning scope, lineage control, warehouse review, automated decision,
  stewardship, minimization, retention, and storage-tier policy.
- Dependency-state metadata for Secrets Vault (`VaultDependencyState`), logging &
  monitoring, data contract registry, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Warehouse/lakehouse storage/query payload.
- Storage layer catalog / dataset / table record body or column value.
- Ingestion/ETL intake payload or raw ingested records.
- Partition/layout payload.
- Lineage/provenance payload or transformation logs.
- Query result or warehouse-review output.
- Readiness score/quality score/rating/rank/match confidence/model output.
- Automated decision result.
- Connection string, storage credential, credential/token/secret/password value.
- Attachment payload.
- Raw provider payload.
- PII-heavy dataset/record details.
- Compensation/payroll/storage-cost payload.

Data-plane note: this module **is** the physical data plane, but this slice
declares readiness STATE only. No dataset/table/warehouse row, column value, raw
ingested record, query result, connection string, storage credential, or
partition/lineage payload of any kind may cross the boundary into persistence.
Only boundary/readiness STATE metadata governed by stewardship precondition, data
minimization, retention, and storage-tier policy is permitted, keeping the strong
data-warehouse governance concerns (PII minimization, tenant isolation of
analytical data, retention) resolved as metadata-only state.

## 12. Permission Boundary

Approved permission namespace:

- `dki.data-warehouse-lakehouse.read`
- `dki.data-warehouse-lakehouse.manage`
- `dki.data-warehouse-lakehouse.evaluate`
- `dki.data-warehouse-lakehouse.audit.read`

Runtime owner key: `dki.data-warehouse-lakehouse`. The canonical module ID
`MOD-0063` is a governance/documentation identity and must not be used as a
runtime literal, owner key, or permission namespace fragment.

## 13. Tenant / Security Boundary

Runtime work is tenant-aware and fails closed. The backend/API slice resolves
tenant server-side and does not expose `TenantId` in request DTOs.

Security guardrails:

- Canonical identity (`MOD-0063`) must not become a runtime literal.
- Legacy name-collision ID (`MOD-0008`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_dki_data_warehouse_lakehouse_tenant_code_active` and
  `ix_dki_data_warehouse_lakehouse_tenant_state`.
- Because a data warehouse concentrates data, tenant isolation of analytical data
  is a first-class control even for readiness metadata; no connection string or
  storage credential is persisted, and Secrets Vault availability is treated as a
  controlling dependency precondition.

## 14. Compliance / Privacy Boundary

Legal/privacy/stewardship is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only stewardship/precondition representation, with a valid stewardship
  basis treated as a precondition to activation (`StewardshipPreconditionState`).
- Data minimization fail-closed behavior (`DataMinimizationState`): store only
  readiness state; prefer aggregated/derived over raw analytical data.
- Retention and storage-tier policy as local/deferred metadata
  (`RetentionPolicyState`, `StorageTierPolicyState`), covering the defined
  lifecycle and purge policy for analytical data.
- Exclusion of dataset/table/warehouse rows, column values, raw ingested records,
  query results, connection strings / storage credentials, partition/lineage
  payloads, and any PII dataset.

The Data Warehouse / Lakehouse capability declares readiness STATE only; it does
not own or store any dataset/table/warehouse rows, column values, raw ingested
records, query results, connection strings, storage credentials, or
partition/lineage payloads, and all downstream analytics-backbone consumers must
treat its output as boundary/readiness metadata, not as a data source.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, ingestion/ETL
connector, or document/notification UI is authorized. `MOD-0064 ETL / ELT
Pipelines` is consumed only as an upstream feed dependency-state context; Secrets
Vault, logging & monitoring, and data contract registry are consumed only as
dependency-state context; `MOD-0004`/`MOD-0060`/`MOD-0062` are consumed only as
sibling/consumer references; and ESBP and HCM `CAND-CAP-0034` remain non-owning
consumers.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `runtime-authored`.
2. AC-02: Canonical identity is `MOD-0063` (Blueprint-backed; used directly, not
   a candidate).
3. AC-03: Legacy name-collision ID `MOD-0008` remains superseded and must not
   appear as a runtime literal.
4. AC-04: Runtime owner/key is `dki.data-warehouse-lakehouse`.
5. AC-05: Permission namespace is limited to
   `dki.data-warehouse-lakehouse.read`, `dki.data-warehouse-lakehouse.manage`,
   `dki.data-warehouse-lakehouse.evaluate`, and
   `dki.data-warehouse-lakehouse.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.DataKnowledgeService/**`; owning service is
   `Diten.DataKnowledgeService` (port `5062`, Mongo db `DitenDataKnowledge`).
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3), exposed through the gateway route
   `/api/data-warehouse-lakehouse` (`5000` -> DKI `5062`).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection `dki_data_warehouse_lakehouse_readiness`)
   includes active tenant `Code` uniqueness
   (`ux_dki_data_warehouse_lakehouse_tenant_code_active`), tenant/state indexing
   (`ix_dki_data_warehouse_lakehouse_tenant_state`), soft delete, and tenant
   isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Real warehouse/lakehouse storage and query, storage layer catalog
    record persistence, ingestion/ETL intake execution, partitioning execution,
    lineage capture, warehouse-review/query-result persistence, model output, and
    automated decision behavior remain unauthorized.
14. AC-14: Steward/admin warehouse fields remain reserved/out of scope; no money
    field is present; no dataset/table row, column value, raw ingested record,
    query result, connection string, storage credential, or partition/lineage
    payload field is present.
15. AC-15: Dataset/table/warehouse rows, column values, raw ingested records,
    query results, connection strings / storage credentials, partition/lineage
    payloads, credential/token/secret/password, and PII-heavy persistence remain
    unauthorized; field names are marker-safe (Secrets Vault dependency field is
    `VaultDependencyState`, using `Vault*` not `Secret*`) and the field-name test
    passes under the `\b` word-boundary matcher.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: Analytics-backbone dependency boundaries are recorded (`MOD-0064` as
    upstream feed; `MOD-0004`/`MOD-0060`/`MOD-0062` as sibling/consumer; Secrets
    Vault, logging & monitoring, data contract registry, and notification as
    dependency-state context; ESBP and HCM `CAND-CAP-0034` as non-owning
    consumers).
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `MOD-0063|MOD-0008` passes under
    runtime/frontend/gateway/test paths (module IDs are governance-only).

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: runtime-authored`.
- Confirm all 20 sections are present.
- Build DKI API:
  `dotnet build services/Diten.DataKnowledgeService/src/Diten.DataKnowledgeService.Api/Diten.DataKnowledgeService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `DataWarehouseLakehouse` filter
  (`Application.Tests/DataWarehouseLakehouseTests.cs`, handler behavior; fix-absent
  should be RED).
- Run full DKI Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_dki_data_warehouse_lakehouse_tenant_code_active`), and tenant/state
  indexing (`ix_dki_data_warehouse_lakehouse_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`warehouse`, `lakehouse`,
  `storage`, `ingestion`, `partition`, `lineage`, `catalog`) are not falsely
  rejected.
- Verify the contract field-name test confirms marker-safe field naming (the
  Secrets Vault dependency field is `VaultDependencyState`, using `Vault*` not
  `Secret*`).
- Verify forbidden catalog/ingestion/partition/lineage/query-result/decision/
  sensitive fields are absent.
- Verify steward/admin warehouse fields are reserved/absent, no money field
  exists, and no dataset/table row, column value, raw ingested record, query
  result, connection string, storage credential, or partition/lineage payload
  field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "MOD-0063|MOD-0008" services frontend gateway`
  returns no runtime literal (module IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a runtime-authored slice.
- [x] Blueprint-canonical identity accepted (`MOD-0063`, used directly).
- [x] Legacy `MOD-0008` name collision recorded as superseded.
- [x] Candidate gate script not-executable note is accepted.
- [x] EA runtime authorization accepted: domain `data-knowledge-intelligence` and
  dedicated data-plane service `Diten.DataKnowledgeService` assigned per
  `WP-DKI-SLICE-0063`.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real warehouse/lakehouse storage and query prohibition is accepted.
- [x] Storage layer catalog / dataset / table / column persistence prohibition is
  accepted.
- [x] Ingestion/partitioning/lineage/warehouse-review execution prohibition is
  accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved steward/admin warehouse fields, no-money-field, and
  no-raw-record/column-value/query-result/connection-string/storage-credential/
  partition/lineage-payload constraints are accepted.
- [x] Sensitive/PII payload prohibition is accepted.
- [x] Marker-safe field naming (Secrets Vault dependency field `Vault*`, not
  `Secret*`) with `\b` word-boundary matcher is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] Analytics-backbone (`MOD-0004`/`MOD-0059`–`MOD-0064`), ESBP, and HCM
  dependency/consumer context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA runtime authorization / canonical MOD runtime assignment.
- [x] Runtime owner/key decision (`dki.data-warehouse-lakehouse`).
- [x] Permission namespace decision.
- [x] Runtime repo scope decision (`services/Diten.DataKnowledgeService/**`).
- [x] Minimal metadata-only data warehouse / lakehouse readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/stewardship metadata-only waiver.
- [x] Data minimization policy.
- [x] Retention / storage-tier / evidence / deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend and
  gateway route are scoped under `services/Diten.DataKnowledgeService/**` and the
  owned `frontend/Diten.Web/**` DataWarehouseLakehouse objects.
- Runtime owner/key remains limited to `dki.data-warehouse-lakehouse`.
- Permission namespace remains limited to `dki.data-warehouse-lakehouse.read`,
  `dki.data-warehouse-lakehouse.manage`, `dki.data-warehouse-lakehouse.evaluate`,
  and `dki.data-warehouse-lakehouse.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `VaultDependencyState`, `LoggingMonitoringDependencyState`,
  `DataContractRegistryDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states; the Secrets Vault dependency field is
  marker-safe (`Vault*`, not `Secret*`).
- Steward/admin warehouse UX fields remain reserved/out of scope, no money field
  is present, and no dataset/table row, column value, raw ingested record, query
  result, connection string, storage credential, or partition/lineage payload
  field is present.
- ESBP, HCM, and Platform scopes remain closed.
- Real warehouse/lakehouse storage and query, storage layer catalog record
  persistence, ingestion/ETL intake execution, partitioning execution, lineage
  capture, warehouse-review/query-result persistence, model output, automated
  decision behavior, notification/document integration, dataset/table/warehouse
  rows, column values, raw ingested records, query results, connection strings /
  storage credentials, partition/lineage payloads, credential/token/secret/
  password, and PII-heavy persistence remain out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0063`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted the runtime-authorization revert of the prior
  governance-first deferral: the DCP-008 "Analytics backbone — Pending EA domain
  mapping" note is resolved by assigning domain `data-knowledge-intelligence` and
  the dedicated data-plane service `Diten.DataKnowledgeService` per
  `WP-DKI-SLICE-0063`, rather than defaulting to `Diten.EnterpriseStrategyService`
  or `Diten.Platform`.
- EA/registry owner accepted verifier absence as a governance note.
- EA runtime policy waiver approved for the first metadata-only backend/API
  readiness slice (K19-complete), with golden-compact readiness CRUD frontend and
  gateway route in scope. `MOD-0063` is Blueprint-canonical and its DCP-002
  canonical registry row is reserved and used directly (no candidate identity);
  the legacy `MOD-0008` name collision is superseded.
- Legal/privacy/stewardship metadata-only waiver approved.
- Data minimization fail-closed policy approved.
- Retention / storage-tier / evidence / deletion local/deferred metadata policy
  approved.
- Marker-safe field-naming waiver approved: field names deliberately avoid
  forbidden-marker fragments (Secrets Vault dependency field named `Vault*`, not
  `Secret*`), enforced by the contract field-name test using the `\b`
  word-boundary matcher.

Runtime literal scan:

- Required command:
  `rg -n "MOD-0063|MOD-0008" services frontend gateway`
- Module IDs are governance-only and must return no runtime literal match; the
  runtime uses owner key `dki.data-warehouse-lakehouse` and gateway route
  `/api/data-warehouse-lakehouse`, never the MOD ID.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real warehouse/lakehouse storage and query execution.
- Storage layer catalog / dataset / table model management.
- Ingestion/ETL intake execution (feed from `MOD-0064`).
- Partitioning and layout optimization execution.
- Lineage / provenance tracking execution.
- Warehouse-review and query execution.
- Automated decision approval.
- Steward/admin warehouse UX beyond readiness CRUD.
- Physical binding targets exposed to `MOD-0004` semantic bindings and `MOD-0060`
  metric definitions.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
