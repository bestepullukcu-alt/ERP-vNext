---
id: CAND-CAP-0007-FU01
name: Enterprise Strategy Security, Tenancy & Data Migration Foundation
domain: enterprise-strategy-business-performance
service: Diten.EnterpriseStrategyService
shell: none
golden_reference: none
entity_base: BaseEntity
entity_base_namespace: Diten.Domain.Common
status: draft
owner: ali.tufanoglu / enterprise-architect
branch: feature/esbp/cand-cap-0007-fu01-security-tenancy-data-migration
started: 2026-07-27
target: 2026-08-10
form_field_count: 0
parent: MOD-0352
delivery_capability_pack: DCP-005
gate_2: blocked-for-protected-hazards
---

# CAND-CAP-0007-FU01 — Enterprise Strategy Security, Tenancy & Data Migration Foundation

> **Candidate identity guard:** `CAND-CAP-0007-FU01` is a temporary governance identity. It must never be
> written into runtime code, routes, permission prefixes, Mongo collection names, events, jobs or
> configuration literals.

> **Draft / no-code guard:** This module pack is planning-only. It authorizes no production code, migration
> script, test implementation, gateway edit or data mutation. Production work requires DCP-005 execution
> readiness, this pack's explicit promotion to `approved` / `ready-for-dev`, the correct implementation
> branch and all applicable Control Tower gates.

## 1. Module Summary

This backend-only foundation makes the existing `Diten.EnterpriseStrategyService` fail-closed, tenant-aware
and safely migratable before Strategy, Demand transition, DWS or BPM feature work proceeds. It covers JWT
authentication, permission enforcement, server-side tenant resolution, tenant/deleted filtering, audit,
optimistic concurrency, idempotency, collection-specific inventory/mapping/quarantine and reversible
migration evidence.

This pack does not validate existing business behavior merely because code exists. Its purpose is to make
that code safe to inspect and migrate without creating a second Demand, task, approval or WorkCenter system.

### Read-only AS-IS evidence

| Area | Repository evidence | Verdict |
|---|---|---|
| HTTP pipeline | `Diten.EnterpriseStrategy.API/Program.cs` has no `AddAuthentication`, `AddJwtBearer` or `UseAuthentication`; it calls `UseAuthorization` only | Authentication is absent; anonymous requests can reach controllers |
| Permission enforcement | `DefaultEnterpriseStrategyAuthorizationService` and the Delivery Execution equivalent default development bootstrap to enabled | Fail-open when environment switches are absent |
| Tenant concept | `Diten.Domain.Common.BaseEntity` and current aggregates have no `TenantId` | Tenancy is a schema/data-migration gap, not only a missing filter |
| Repository filters | `GenericRepository<T>` and specialized repositories query by ID or `_ => true` | No automatic tenant or `IsDeleted == false` enforcement |
| Delete behavior | `GenericRepository.DeleteAsync` uses `DeleteOneAsync` | Physical delete violates soft-delete contract |
| Base fields | Current base has string `Id`, scalar UTC `CreatedDate`, optional actor fields and `IsDeleted`; no `DeletedAt` or technical concurrency token | Controlled extension/migration required |
| Concurrency | Some Strategy/DWS services use `ExpectedVersion`; coverage and atomic Mongo compare-and-write are inconsistent | Must become one enforced persistence contract |
| Idempotency | No service-wide idempotency contract/store was found | Missing |
| Startup behavior | `DbInitializer.SeedData` runs migrations then writes TaskAggregate and DemandIdea demo data during startup | Production startup mutates business data and seeds ownership-conflicting fixtures |
| DWS hazards | `DecompositionStructureAggregate` has `ApprovedAt/ApprovedBy`; nodes have type/status/responsible/due-date task-like fields | Gate 2 blocked |
| Gateway/port | Existing gateway routes `/api/v1/enterprise-strategy*` to `5004`; launch settings also use `5003/5004` | Does not match canonical ES local port `5102`; integration-agent follow-up |
| Tests | Existing Application tests and `EnterpriseStrategyLineageE2ETests` are primarily in-memory; no authenticated real-Mongo tenant-isolation proof | Insufficient foundation evidence |

## 2. Ownership and Boundaries

### In scope

- Authentication scheme and JWT claim validation inside the ES API.
- Fail-closed permission evaluation.
- Server-resolved tenant and current-actor contexts.
- Tenant/audit/deletion/concurrency fields and tenant-enforcing persistence behavior.
- Idempotent write-command contract.
- Read-only collection inventory and collection-specific migration design.
- Deterministic tenant mapping, manual-review classification and quarantine.
- Forward/retry/partial-failure/verification/rollback design.
- Separation of production startup from fake/demo seed data.
- Real MongoDB and authenticated HTTP foundation tests.
- BL-030 BSON date representation decision.

### Out of scope

- New Strategy, Demand, DWS, BPM or WorkCenter business features.
- Demand lifecycle changes; canonical Demand SoR remains MOD-0117.
- TaskAggregate migration/deletion/deprecation before Gate 2 PASS.
- DWS task-like projection/UI/migration/behavior before Gate 2 PASS.
- Local approval behavior based on `ApprovedAt/ApprovedBy`.
- WorkCenter projection from free-text Demand identity.
- WC-5/cross-service bridge or ES `IWorkItemProvider`; these require separate DCP-004 approval.
- Changes to MOD-0023, MOD-0024, MOD-0117, MOD-0288 or shared evidence systems.
- Gateway route implementation.

## 3. Owned Objects

This foundation owns technical contracts, not new business aggregates:

| Object / contract | Responsibility |
|---|---|
| `IESbpTenantContext` | Immutable server-resolved tenant identity; never populated from body/query |
| `IESbpCurrentActorContext` | Authenticated actor ID and effective permission claims |
| Tenant-resolution/auth middleware | Validates header/claim consistency and establishes scoped contexts |
| Tenant-enforcing repository base/filter builder | Applies `TenantId` + `IsDeleted == false` automatically |
| Concurrency contract | Atomic `Id + TenantId + Version` compare-and-write; ETag mapping |
| Idempotency contract/store | Tenant + actor + operation + key scoped request/result record |
| Migration manifest | Collection-specific count, schema, mapping, index, verification and rollback state |
| Migration run/checkpoint | Durable retry/resume and partial-failure checkpoint |
| Quarantine record | Immutable source identity, reason, safe metadata, resolution status and audit reference |
| Foundation audit event | Actor, tenant, server UTC, correlation, action/outcome and safe before/after metadata |

No candidate ID is used as a runtime identifier for these objects.

## 4. Entity Fields

### Base entity decision

The approved default is to retain and deliberately extend the existing
`Diten.Domain.Common.BaseEntity`, preserving scalar UTC `DateTime` storage. The future implementation must
not inherit `Diten.Platform.Common.Persistence.BaseEntity` until BL-030 is closed with an approved serializer,
disk migration, real-Mongo proof and rollback.

| Field | Type / representation | Required | Source / behavior |
|---|---|---:|---|
| `Id` | Existing string ID during controlled migration; future ID normalization is separate | Yes | Server/generated or legacy-preserved |
| `TenantId` | `Guid`, scalar/string BSON representation agreed by the pack | Yes for tenant-owned operational records | Server tenant context only |
| `CreatedDate` | UTC scalar BSON datetime | Yes | Server clock |
| `CreatedBy` | Actor reference | Business writes | Current actor context |
| `LastModifiedDate` | UTC scalar BSON datetime | Updates | Server clock |
| `LastModifiedBy` | Actor reference | Business updates | Current actor context |
| `IsDeleted` | Boolean | Yes | Default false |
| `DeletedAt` | Nullable UTC scalar BSON datetime | When deleted | Server clock |
| `DeletedBy` | Nullable actor reference | When deleted | Current actor context |
| `Version` | Integer technical concurrency token | Yes | Starts at 1; atomically increments |

Business semantic versions must use explicit names such as `TemplateVersionNumber` or
`BaselineVersion`; they must not shadow the technical concurrency `Version`.

### Migration/control records

| Field | Required | Rule |
|---|---:|---|
| `MigrationRunId` | Yes | Stable opaque ID; never candidate/module ID |
| `CollectionName` | Yes | Exact existing Mongo collection |
| `SourceDocumentId` | Yes | Original immutable identifier |
| `SourceChecksum` | Yes | Detects source mutation and duplicate processing |
| `MappingDisposition` | Yes | `Deterministic`, `ManualReview`, `Quarantine`, `Applied`, `RolledBack` |
| `TenantId` | Conditional | Present only after proven mapping |
| `ReasonCode` | Yes for non-deterministic | Controlled non-sensitive code |
| `AttemptCount` | Yes | Monotonic retry counter |
| `LastAttemptAtUtc` | Yes after attempt | Server UTC scalar datetime |
| `CorrelationId` | Yes | End-to-end audit/diagnostic correlation |
| `SnapshotReference` | Yes before write | Recoverable backup/rollback reference |

## 5. Repo Scope

### This authoring task

Only this module-pack file is authorized:

- `execution/domains/enterprise-strategy-business-performance/module-packs/CAND-CAP-0007-FU01-enterprise-strategy-security-tenancy-data-migration.md`

### Future implementation scope

An approved implementation may authorize only the following concrete ES-owned paths:

- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/Program.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/Middleware/**`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/Security/**`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/Controllers/**` only for
  authentication/authorization/ETag contract application
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/appsettings*.json`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/Properties/launchSettings.json`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Application/Common/**`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Application/EnterpriseStrategy/Shared/**`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Application/DeliveryExecutionManagement/Shared/**`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Domain/Common/BaseEntity.cs`
- ES aggregate files only where tenant/audit/deletion/concurrency fields are applied by the approved
  collection migration manifest
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Persistence/Context/**`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Persistence/Repositories/**`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Persistence/EnterpriseStrategy/**`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Persistence/DbInitializer.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Infrastructure/**`
- `services/Diten.EnterpriseStrategyService/tests/Diten.Application.Tests/**`
- `services/Diten.EnterpriseStrategyService/tests/Diten.EnterpriseStrategy.EndToEnd.Tests/**`
- New ES-owned foundation test project paths under `services/Diten.EnterpriseStrategyService/tests/**`
- ES migration evidence under a future explicitly approved
  `docs/audits/enterprise-strategy-security-tenancy-migration/**`

Gate 2 blocked files remain read-only until the gate passes even if they appear under a broad listed
directory.

## 6. Protected Paths

- `/Users/alitufanoglu/ERP-vNext/**` — entire main worktree
- `.antigravity/**`
- `services/Diten.Platform/**`
- `services/Diten.AuthService/**`
- `services/Diten.Platform.Common/**`
- `services/Diten.DevEnablementService/**`
- all other domain service paths
- `frontend/**`
- `gateway/**`, including `gateway/Diten.ApiGateway/ocelot.json`
- other domains' `execution/domains/**`
- `execution/portfolio/**` and `execution/registries/**`
- archive/frozen paths
- Candidate identities in any runtime path or literal

Additional protected hazards pending Gate 2:

- `TaskAggregate` domain/repository/seed/data migration/deletion/deprecation paths
- DWS task-like node fields and their DTO/API/UI/projection/migration behavior
- `ApprovedAt/ApprovedBy` local approval behavior
- free-text Demand identity to WorkCenter projection

## 7. Dependencies

- DCP-005 approved governance and ordered delivery sequence.
- DCP-002 candidate-identity gate.
- ESBP `domain-config.md`.
- Central JWT/permission claim contract from platform/auth services, consumed without modifying their code.
- MongoDB backup/restore capability and a production-like isolated test database.
- MOD-0117, MOD-0023, MOD-0024, MOD-0288 and shared evidence contracts as ownership boundaries only.
- DCP-004 for any later WorkCenter/WC-5/provider work.
- BL-030 decision record and its real-Mongo guard evidence.

## 8. Runtime Constraints

- MongoDB single-database logical tenant isolation; every tenant-owned query is tenant-first.
- Request DTO/body/query cannot accept `TenantId`.
- Header tenant and authenticated tenant claim must match; mismatch is fail-closed.
- Missing/invalid tenant context returns controlled `400`; cross-tenant object access returns `404`.
- Standard reads exclude deleted records; soft delete uses `Id + TenantId + Version`.
- Every tenant-owned index begins with `TenantId`; unique constraints are tenant-scoped.
- Audit uses server UTC and never logs JWTs, secrets, connection strings or sensitive payloads.
- Writes require both permission and idempotency/concurrency contracts.
- Production startup cannot silently seed demo business data.
- Unknown tenant ownership is quarantined; default-tenant assignment is forbidden.
- Frontend uses Gateway `5000`; ES canonical local port is `5102`.
- Existing gateway port `5004` is AS-IS evidence, not an approved target.
- Candidate identity strings are governance-only and forbidden at runtime.

## 9. Layout & Shell Contract

`shell: none`. This foundation has no Razor views, DataTable, frontend route or localization surface. It must
not create or modify frontend files. Any future user-facing migration/admin surface requires a separate
approved pack and tenant-shell decision.

## 10. Backend File Convention

This is cross-cutting foundation work, not CRUD/DataTable generation; Golden Slim/Compact file conventions do
not apply. Future implementation must preserve the existing five-layer service boundaries:

- API: authentication middleware, filters and HTTP/ETag behavior only.
- Application: tenant/actor/idempotency interfaces and authorization contracts.
- Domain: storage-agnostic base contract; no Mongo driver imports.
- Persistence: Mongo filters, indexes, migrations and checkpoints.
- Infrastructure: JWT/external technical adapters where required.

Controllers remain thin. MongoDB driver usage remains in Persistence. One public type per new file is required.

## 11. Frontend File Contract

None. No frontend controller, view, JavaScript, RESX, menu, permission manifest or DataTable is authorized.

## 12. Validation Rules

| Input / field | Required | Rule | Failure |
|---|---:|---|---|
| Bearer token | Yes except explicitly approved health endpoint | Valid signature, issuer, audience, lifetime | `401` |
| Permission claim | Yes per endpoint/action | Exact approved permission; no bootstrap fallback | `403` |
| `X-Tenant-Id` | Yes for tenant endpoints | GUID; matches JWT tenant claim | `400` for missing/invalid, fail-closed for mismatch |
| Request/body/query `TenantId` | Forbidden | Reject or ignore via contract validation; never binds domain state | `400` |
| Object ID | Yes for detail/write | Lookup includes tenant + not-deleted | `404` |
| ETag / expected version | Writes | Positive current technical version | `409`/`412` with stable error code |
| Idempotency key | State-changing commands | Non-empty, bounded, tenant+actor+operation scoped | `400`; replay returns original safe result |
| Correlation ID | Yes | Server creates if absent; bounded safe format | Normalized server value |
| Mapping disposition | Migration | Controlled enum; tenant required only when proven | Row not applied |
| Tenant mapping evidence | Before apply | Deterministic rule + source evidence | Manual review/quarantine |
| Snapshot/checksum | Before apply | Backup exists; source checksum stable | Migration stops |

## 13. Failure Path to Verify

| Scenario | Expected behavior |
|---|---|
| Anonymous access to protected read/write | `401`; controller/business handler not executed |
| Authenticated actor lacks permission | `403`; no repository call |
| Missing/invalid tenant | Controlled `400` ProblemDetails |
| JWT/header tenant mismatch | Fail-closed; no data disclosure or mutation |
| Cross-tenant ID probing | `404` for read/update/delete |
| Deleted record requested | `404` outside authorized audit/recovery flow |
| Stale ETag/version | Atomic write affects zero records; `409`/`412`; no lost update |
| Duplicate idempotency key, same payload | Original result replayed; one business mutation/audit outcome |
| Duplicate key, different payload | Conflict; no second mutation |
| Dependency unavailable during migration | Checkpoint retained; retry resumes without duplicate mutation |
| Partial collection failure | Failed rows isolated; successful rows verified; run not marked complete |
| Unmapped tenant | No write to business collection; record quarantined |
| Quarantine retry after manual mapping | Idempotent transition with full audit trail |
| Verification count/checksum mismatch | Cutover blocked; rollback invoked |
| Fake seed enabled in production | Startup fails or skips by explicit safe configuration; no demo writes |
| BL-030 unsafe multi-date sort introduced | Real-Mongo guard fails |

## 14. Authorization Convention

- API independently authenticates JWT Bearer tokens; gateway trust is insufficient.
- Default policy requires an authenticated user except explicitly approved health endpoints.
- All existing ES permission attributes/services become fail-closed.
- Permission values remain stable technical ES permission keys; candidate IDs never prefix permissions.
- Actor and tenant are derived from validated claims/context, never request DTO fields.
- Authentication failure is `401`; authenticated-but-unauthorized is `403`; cross-tenant object probing is
  `404`.
- Development/test bypasses must use explicit test authentication handlers isolated from production startup;
  absence of configuration must never enable access.

## 15. Gateway / API Routing Decision

The future ES API target is local port `5102`, exposed to frontend only through Gateway `5000`. Current
gateway routes point to `5004`; this pack records the mismatch but does not edit it.

Gateway work requires a separate, explicitly approved `integration-agent` task after the ES API is proven on
`5102`. Token passthrough and tenant-header behavior must be covered by authenticated gateway smoke tests.

## 16. Acceptance Criteria

1. API registers and executes JWT authentication before authorization and endpoint mapping.
2. With no special environment switch, anonymous access to every protected endpoint returns `401`.
3. Permission services deny by default; no absent-configuration development bootstrap grants permissions.
4. Tenant context is derived server-side and request DTO/body/query cannot select `TenantId`.
5. Every tenant-owned collection has a documented and tested `TenantId`-first query/index contract.
6. Cross-tenant read/update/delete returns `404` and causes no audit/data leakage.
7. Normal repository reads exclude `IsDeleted == true`; delete is soft and sets server UTC/actor fields.
8. State-changing writes atomically compare technical version and increment it once.
9. Idempotent replay produces one mutation and one authoritative outcome.
10. Audit evidence binds actor, tenant, server UTC, correlation, action, outcome and safe before/after
    metadata.
11. Every collection in the migration matrix has measured counts/indexes/schema samples and an approved
    mapping disposition before mutation.
12. Records without deterministic tenant ownership are quarantined; none are assigned to a default tenant.
13. Migration can resume after interruption without duplicate changes and cannot mark completion with
    unverified rows.
14. Forward, partial-failure, verification and rollback evidence is produced on a production-like copy.
15. Demo `TaskAggregate` and `DemandIdea` seed data is not written by production startup.
16. Existing scalar UTC `DateTime` safety is preserved; Platform.Common BaseEntity is not inherited while
    BL-030 remains unresolved.
17. Real Mongo tests cover tenant filters, soft delete, tenant-first indexes, concurrency, idempotency,
    quarantine and rollback.
18. Authenticated HTTP tests cover `401`, `403`, tenant errors, cross-tenant `404`, stale write and replay.
19. Gate 2 blocked objects remain unchanged until written Gate 2 PASS.
20. `CAND-CAP-0007-FU01` has zero runtime literal hits.
21. No frontend, gateway, Platform/Auth service or other-domain file changes occur in this slice.
22. All scoped regression is green before the slice closes; failures are not deferred.

## 17. Test Expectations

### Unit

- JWT/permission fail-closed decision matrix.
- Header/claim tenant-resolution validation.
- Tenant/deleted filter composition.
- ETag/version and idempotency decision logic.
- Deterministic/manual/quarantine mapping classifier.
- Migration checkpoint/retry state machine.

### Real Mongo integration

- Cold database startup and index creation.
- Same-ID data in two tenants without leakage.
- Cross-tenant read/write/delete isolation.
- Soft-delete visibility and audit-only access.
- Atomic version compare/increment under concurrent writers.
- Duplicate idempotency replay and payload conflict.
- Collection inventory/count/checksum and quarantine behavior.
- Interrupted migration resume, partial failure, verification and rollback.
- Scalar UTC BSON date representation and BL-030 multi-date-sort guard.

### Authenticated HTTP E2E

- Real/test-signed JWT through the service pipeline.
- `401` anonymous, `403` missing permission, tenant `400`, cross-tenant `404`.
- Successful same-tenant write with ETag and idempotency key.
- Stale ETag and duplicate-key behavior.
- Restart/reload persistence proof; no startup demo seed.

### Commands

- `dotnet build services/Diten.EnterpriseStrategyService/Diten.EnterpriseStrategy.sln -c Debug`
- `dotnet test services/Diten.EnterpriseStrategyService`
- Candidate preflight and runtime-literal scan.
- Targeted real-Mongo integration/E2E commands defined by the implementation test project.

No DataTable verifier or RESX check applies because `shell: none`.

## 18. Ready-for-dev Checklist

- [ ] Human approves this draft and promotes it to `approved` / `ready-for-dev`.
- [ ] Future implementation branch is
  `feature/esbp/cand-cap-0007-fu01-security-tenancy-data-migration`.
- [ ] Candidate preflight exit 0 is recorded.
- [ ] Exact implementation file list is approved from §5.
- [ ] JWT issuer/audience/key source and permission claim contract are confirmed.
- [ ] Tenant claim/header resolution and error semantics are approved.
- [ ] BaseEntity scalar UTC/BSON representation decision is approved.
- [ ] Collection inventory has been executed read-only against the target data copy.
- [ ] Every collection has a mapping owner and disposition.
- [ ] Backup, quarantine retention/data steward and rollback plan are approved.
- [ ] Real Mongo and authenticated HTTP test environment exists.
- [ ] Gateway `5004 → 5102` follow-up is assigned to integration-agent but not bundled here.
- [ ] Gate 2 evidence package is ready.
- [ ] Written Gate 2 PASS exists before any protected-hazard change.
- [ ] DCP-004 approval exists before any WC-5/ES provider work.
- [ ] No open scoped regression is accepted as follow-up debt.

## 19. Implementation Notes

### Collection-by-collection migration matrix

Counts must be measured with exact collection-level `countDocuments`, existing indexes captured with
`listIndexes`, representative schemas sampled without exporting sensitive values, and source checksums
recorded before changes. The table is a planning contract; no count is guessed in this draft.

| Collection / aggregate | Count measurement | TenantId now | Tenant source / deterministic rule | Ambiguous behavior / quarantine | Index change | Retry / idempotency | Verification | Rollback | Gate 2 |
|---|---|---:|---|---|---|---|---|---|---|
| `TaskAggregate` | `countDocuments({})` + status histogram | No | No mapping assumed; ownership evidence required | Quarantine; no default tenant | Deferred | Source checksum + migration row | Count/checksum + owner evidence | Snapshot restore | **Blocked** |
| `DemandIdeaAggregate` | Count + status/source histograms | No | Only approved MOD-0117 transition evidence | Manual/quarantine; free-text identity is not proof | Tenant-first after contract | Source checksum + transition key | Count/checksum + MOD-0117 linkage | Snapshot/adapter rollback | Gate 2 if WorkCenter projection |
| `DecompositionStructureAggregate` (embedded nodes/dependencies/issues/audit) | Count + embedded-array cardinalities | No | Parent business object typed ownership evidence | Quarantine unresolved structures/nodes | Tenant + parent + deleted/status | Structure checksum + version | Graph/cardinality/cycle checks | Whole-aggregate snapshot | **Blocked for task-like/approval fields** |
| `GoalAggregate` | Count + status/company/period histograms | No | Approved company/legal-entity → tenant mapping | Manual/quarantine | Tenant + status/period/deleted | Source checksum + goal ID | Count/checksum/lineage | Snapshot restore | No |
| `StrategicGoalMetric` | Count + orphan goal IDs | No | Inherit only from deterministically mapped goal | Quarantine orphan/multi-tenant links | Tenant + goal ID | Parent+child checksum | No cross-tenant/orphans | Snapshot restore | No |
| `StrategicGoalMetricYearlyTarget` | Count + orphan metric IDs | No | Inherit through mapped metric→goal chain | Quarantine broken chain | Tenant + metric/year | Natural-key idempotency | Cardinality/value checksum | Snapshot restore | No |
| `StrategicGoalBudgetEnvelope` | Count + orphan goal IDs | No | Inherit through mapped goal | Quarantine broken chain | Tenant + goal/year | Natural-key idempotency | Totals/cardinality checksum | Snapshot restore | No |
| `ObjectiveAggregate` | Count + goal/company/period histograms | No | Deterministically mapped parent goal/company | Quarantine conflicts/orphans | Tenant + parent/status | Source checksum + objective ID | Lineage/count/checksum | Snapshot restore | No |
| `StrategyConnectionAggregate` | Count + endpoint-type pairs | No | Both endpoints must resolve to same tenant | Quarantine cross/unknown endpoints | Tenant + from/to | Edge natural key | No cross-tenant edges/cycles | Snapshot restore | No |
| `InitiativeStrategyLinkAggregate` | Count + initiative/objective IDs | No | Same-tenant typed endpoint proof | Quarantine unresolved links | Tenant + initiative/objective | Link natural key | No orphan/cross-tenant link | Snapshot restore | No |
| `ProjectStrategyLinkAggregate` | Count + project/initiative IDs | No | Same-tenant typed endpoint proof | Quarantine unresolved links | Tenant + project/initiative | Link natural key | No orphan/cross-tenant link | Snapshot restore | No |
| `PpmInitiativeReadModelAggregate` | Count + source IDs | No | MOD-0117 source tenant contract | Disable/quarantine without source proof | Tenant + source ID | Source version/event key | Reconcile against MOD-0117 | Drop/rebuild projection | No |
| `PpmProjectReadModelAggregate` | Count + source IDs | No | MOD-0117 source tenant contract | Disable/quarantine without source proof | Tenant + source ID | Source version/event key | Reconcile against MOD-0117 | Drop/rebuild projection | No |
| `PlanningCycleAggregate` | Count + code/status | No | Approved owning-company/tenant evidence | Manual/quarantine | Tenant + code unique; tenant + status | Source checksum + code | Count/code uniqueness | Snapshot/index rollback | No |
| `StrategyPeriodAggregate` | Count + cycle/scope | No | Mapped planning cycle tenant | Quarantine orphan period | Tenant + cycle/scope | Parent+period key | No orphan/cross-tenant period | Snapshot/index rollback | No |
| `AuditEvent` | Count + object/action/time buckets | No | Derive only when source object and actor evidence agree | Retain immutable quarantine; never guess | Tenant + object/time/correlation | Existing event ID/checksum | Event count/chain integrity | Append-only restore | No |
| `KpiTemplateAggregate` | Count + template code | No | Classify tenant-owned vs governed shared before write | Leave unchanged/quarantine until classification | Decision-dependent | Code+version key | Classification/count/checksum | Snapshot restore | No |
| `KpiThresholdModelAggregate` | Count + model code | No | Follow owning KPI template classification | Quarantine unknown parent | Decision-dependent | Code+version key | Parent integrity | Snapshot restore | No |
| `KpiScorecardPackAggregate` | Count + pack code/status | No | Approved pack owner/company mapping | Manual/quarantine | Tenant + pack code/status | Code+version key | Count/pack integrity | Snapshot restore | No |
| `KpiScorecardPackItemAggregate` | Count + parent/item IDs | No | Inherit mapped pack tenant; referenced KPI same tenant | Quarantine orphan/cross links | Tenant + pack/item | Parent+item key | No orphan/cross link | Snapshot restore | No |
| `KpiCatalogItemAggregate` | Count + ID/code | No | Classify certified shared vs tenant runtime KPI | Quarantine undecided classification | Decision-dependent | ID/code+version | Classification/count | Snapshot restore | No |
| `KpiGovernanceActionAggregate` | Count + action/time/object | No | Inherit governed target tenant + actor evidence | Quarantine missing target/actor | Tenant + target/time | Action ID/correlation | Audit chain integrity | Snapshot restore | No |
| `TemplateImportBatch` | Count + status/time | No | Initiating actor tenant + imported target classification | Quarantine unknown initiator/target | Tenant + status/time | Batch ID/checksum | Batch/item totals | Snapshot restore | No |
| `TemplateImportIssue` | Count + batch/severity | No | Inherit mapped batch tenant | Quarantine orphan issue | Tenant + batch/severity | Batch+issue key | No orphan issues | Snapshot restore | No |
| `GoalTemplate` | Count + ID/category/version | No | Explicit shared-vs-tenant classification | Leave unchanged until classified | Decision-dependent | ID+version | Classification/count | Snapshot restore | No |
| `GoalTemplateMetric` | Count + template IDs | No | Inherit classified template scope | Quarantine orphan | Scope-dependent | Parent+metric key | Parent integrity | Snapshot restore | No |
| `ObjectiveTemplate` | Count + ID/version | No | Explicit shared-vs-tenant classification | Leave unchanged until classified | Decision-dependent | ID+version | Classification/count | Snapshot restore | No |
| `ObjectiveTemplateMetric` | Count + template IDs | No | Inherit classified template scope | Quarantine orphan | Scope-dependent | Parent+metric key | Parent integrity | Snapshot restore | No |
| `InitiativeTemplate` | Count + ID/version | No | Explicit shared-vs-tenant classification | Leave unchanged until classified | Decision-dependent | ID+version | Classification/count | Snapshot restore | No |
| `InitiativeTemplateMetric` | Count + template IDs | No | Inherit classified template scope | Quarantine orphan | Scope-dependent | Parent+metric key | Parent integrity | Snapshot restore | No |
| `ProjectTemplate` | Count + ID/version | No | Explicit shared-vs-tenant classification | Leave unchanged until classified | Decision-dependent | ID+version | Classification/count | Snapshot restore | No |
| `ProjectTemplateMetric` | Count + template IDs | No | Inherit classified template scope | Quarantine orphan | Scope-dependent | Parent+metric key | Parent integrity | Snapshot restore | No |
| `StrategyBlueprintPack` | Count + pack/version/status | No | Explicit shared-vs-tenant classification | Leave unchanged until classified | Decision-dependent | Pack+version | Classification/count | Snapshot restore | No |
| `StrategyBlueprintPackItem` | Count + pack/item IDs | No | Inherit classified pack scope | Quarantine orphan | Scope-dependent | Parent+item key | Parent integrity | Snapshot restore | No |
| `TemplateVersion` | Count + type/key/version | No | Inherit classified template scope | Quarantine orphan/duplicate version | Scope + template/version | Natural version key | Version-chain integrity | Snapshot restore | No |
| `TemplatePublishHistory` | Count + target/version/time | No | Inherit target scope + actor evidence | Quarantine missing target/actor | Scope + target/time | Event ID/correlation | Publish-chain integrity | Snapshot restore | No |
| `InstantiationBatch` | Count + status/time | No | Initiating tenant and target proof | Quarantine unknown initiator/target | Tenant + status/time | Batch ID/idempotency key | Batch/result totals | Snapshot restore | No |
| `InstantiationRecord` | Count + batch/target | No | Inherit batch tenant; target must match | Quarantine orphan/cross target | Tenant + batch/target | Batch+record key | No orphan/cross target | Snapshot restore | No |
| `TemplateOverrideLog` | Count + target/time | No | Inherit target tenant + actor evidence | Quarantine missing evidence | Tenant + target/time | Event ID/correlation | Audit chain integrity | Snapshot restore | No |
| `TemplateUsageStat` | Count + item/type | No | Inherit template scope; split shared/tenant stats if needed | Hold undecided aggregates | Scope + item/type | Item/type key | Recomputed totals | Snapshot restore/rebuild | No |
| Strategic goal migration state/report/backup/manual-review collections | Count per exact collection + run IDs | No | Migration-control scope; not business default tenant | Retain and classify as control/quarantine data | Run ID/status/time | Run/checkpoint key | Run-to-row reconciliation | Preserve prior snapshots | No |

No bulk “set one TenantId on every collection” operation is allowed. A collection absent from this table but
present in `listCollectionNames` automatically blocks implementation until a new reviewed row is added.

### BL-030 decision

Current ES `Diten.Domain.Common.BaseEntity` uses scalar UTC `DateTime` and is retained. The pack does not
approve global `DateTimeOffset` serializer registration or Platform.Common inheritance. Any alternative must:

1. specify the target BSON representation;
2. inventory existing on-disk representations;
3. provide forward and rollback migrations;
4. prove real-Mongo multi-key sort behavior;
5. reconcile the repository-wide BL-030 guard;
6. receive explicit architecture approval.

### Gate 2 classification

`TaskAggregate` and DWS task-like/approval fields are explicitly **Gate 2 blocked**. The foundation may
inventory them read-only and design quarantine/migration behavior, but may not project, render, convert,
delete, deprecate or mutate them before written Gate 2 PASS. Demand records become Gate 2 blocked when
free-text identity is projected to WorkCenter.

## 20. Follow-up Items

- Control Tower Gate 2 review and written PASS for protected hazards.
- DCP-004 approval before WC-5/cross-service bridge or ES `IWorkItemProvider`.
- Integration-agent pack/task for gateway route and port `5004 → 5102`.
- MOD-0117 Demand transition module pack with DCP-003 owner.
- Any post-foundation MOD-0352 Strategy module pack requires a separate subdomain 1.1 scope decision.
- Separate DWS Wave 1 structural module pack.
- Separate BPM process-model/version module pack.
- Enterprise Architect allocation of the future canonical MOD identity.
- Reconciliation of final migration evidence into DCP-005 §20 after implementation.
