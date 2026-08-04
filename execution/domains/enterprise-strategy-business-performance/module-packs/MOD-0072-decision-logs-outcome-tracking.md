---
id: MOD-0072
name: Decision Logs & Outcome Tracking
domain: enterprise-strategy-business-performance
status: draft
owner: Decision Intelligence & Benefit Realization Process Owner
branch: feature/esbp/mod-0072-decision-logs-outcome-tracking
service: Diten.DecisionIntelligenceService
production_authority: none
shell: tenant
golden_reference: slim
entity_base: EntityBase
started: 2026-08-02
target: 2026-08-02
form_field_count: 7
---

# MOD-0072 — Decision Logs & Outcome Tracking

## 1. Module Summary

MOD-0072 is the authoritative tenant-scoped system of record (SoR) for observed outcomes and benefit
realization. It records immutable outcome versions, append-only measurements, corrections/restatements and
formally deferred evidence state; it produces typed OutcomeReferences consumed and relationship-owned by PPM, while
DecisionOutcomeLink remains a separate MOD-0072 provenance link.

Its Blueprint logical route remains **Data, Knowledge & Intelligence → Decision Intelligence**. Packaging
under Enterprise Strategy & Business Performance is repository governance only and does not transfer logical
ownership to strategy planning or place the module in `Diten.EnterpriseStrategyService`.

This is a draft parent pack. It authorizes no service scaffold, port, runtime, frontend, Gateway route,
migration or production deployment.

Binding physical topology: MOD-0072 may exist only as isolated `Modules/OutcomeTracking` inside the future
`Diten.DecisionIntelligenceService`. Placement in `Diten.FpaService`, `Diten.BudgetingService`,
`Diten.ScenarioPlanningService` or `Diten.EnterpriseStrategyService` is forbidden. This module does not
participate in the FP&A primary/fallback profile and has no fallback, dual-host or dual-write topology.

Governing context: [ESBP domain config](../domain-config.md),
[DCP-006 Portfolio Delivery Process Core](../../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md),
[MOD-0117 parent pack](../../portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md) and
[Module Pack Standard](../../../../.antigravity/rules/module-pack-standard.md).

## 2. Ownership and Boundaries

### Owned by MOD-0072

- `Outcome` identity and tenant-scoped authoritative referenceability.
- Immutable `OutcomeVersion` history.
- Append-only `OutcomeMeasurement`, including measurement period and observed actual result/value.
- Metric, unit and currency references used to interpret an observed value.
- `DecisionOutcomeLink` from an Outcome to a MOD-0007 Decision reference when a decision trace exists.
- `OutcomeMetric`, immutable `OutcomeMetricVersion`, `QualitativeScale` and immutable
  `QualitativeScaleVersion` as producer-owned measurement semantics.
- Deterministic observation-status calculation and authoritative `OutcomeReference` validation.

### Explicitly not owned

- Planned `BenefitCommitment`, its target or lifecycle — MOD-0117.
- `BenefitCommitment → OutcomeReference` cardinality (`0..n`), stored reference values, consumer-side
  add/remove/retire relationship behavior and all PPM lifecycle use of those references — MOD-0117.
- Generic Decision, DecisionVersion, rationale or decision evidence — MOD-0007.
- Approval task, approval workflow or `ApprovalOutcome` — MOD-0023.
- `BudgetVersion` or budget truth — MOD-0136.
- Scenario, Comparator, analytical output or selected-scenario truth — MOD-0138.
- Portfolio, InvestmentCase or PPM lifecycle.
- Evidence/document binary, content or payload.
- Enterprise Strategy prototype `ActualValue` or realization fields as authoritative truth.

### Planned versus actual invariant

`BenefitCommitment` is a PPM-owned **planned commitment**. `Outcome` and `OutcomeMeasurement` are
MOD-0072-owned **actual/observed truth**. `BenefitCommitment.Closed` does not mean realized, and an Outcome's
realization state cannot be inferred from a PPM lifecycle. PPM must not persist actual value, period,
evidence, realization state or a snapshot of MOD-0072 data. Existing Enterprise Strategy `ActualValue`,
benefit-realization date and related fields are prototype/legacy evidence only and must remain quarantined
until an explicitly approved migration slice exists.

## 3. Owned Objects

| Object | Kind | Ownership purpose |
|---|---|---|
| `Outcome` | Aggregate root | Stable tenant-scoped outcome identity and current-version pointer |
| `OutcomeVersion` | Immutable entity | Versioned definition, interpretation and status policy |
| `OutcomeMetric` | Aggregate root | Stable tenant-scoped quantitative metric identity |
| `OutcomeMetricVersion` | Immutable entity | Unit kind, bounds, scale, currency and freshness semantics |
| `QualitativeScale` | Aggregate root | Stable tenant-scoped qualitative scale identity |
| `QualitativeScaleVersion` | Immutable entity | Ordered closed qualitative levels |
| `OutcomeMeasurement` | Append-only entity | Actual quantitative or qualitative observation for a period |
| `DecisionOutcomeLink` | Typed link | Optional MOD-0007 Decision/DecisionVersion → Outcome trace |
| `OutcomeReferenceabilityProjection` | Non-persisted contract/read response | Minimal authoritative validation response; never cached authorization truth or a second SoR |
| `ObservationStatusProjection` | Non-persisted read projection | Deterministic `NoObservation`, `Current` or `Stale` result under policy `decision-intelligence.observation-status-policy` `1.0` |
| `diten.decision-intelligence.outcome-reference` | Contract | Exact Gate I-C typed contract, version `1.0` |

Proposed command/query families are object-specific: create/version/retire Outcome; append/correct Measurement;
create/retire Decision provenance links with immutable provenance; get register/details/history; and validate
OutcomeReference. MOD-0072 has no BenefitCommitment relationship mutation command.
Arbitrary generic entity handlers are forbidden.

### Exact permissions

- `decision-intelligence.outcomes.read`
- `decision-intelligence.outcomes.create`
- `decision-intelligence.outcomes.version`
- `decision-intelligence.measurements.append`
- `decision-intelligence.measurements.correct`
- `decision-intelligence.decision-links.manage`
- `decision-intelligence.outcome-references.validate` (trusted S2S only)

Wildcard, alias, `.view`, uppercase and client-asserted permission substitutions are forbidden.
`decision-intelligence.outcomes.version` governs both publishing the next immutable semantic version and the
append-only retirement disposition; it authorizes no delete or reopen. Metric/scale seed and administration
runtime permissions are not invented in this draft and remain within the named B-class runtime-scope blocker.

## 4. Entity Fields

### Outcome

| Field | Type | Required | Invariant |
|---|---|---:|---|
| `Id` | Guid | Yes | Stable identity; server generated |
| `TenantId` | Guid | Yes | Server-derived; never accepted from payload |
| `Code` | string | Yes | Trim + NFC; max 64; unique among active records per tenant |
| `Title` | string | Yes | Trim + NFC; max 200 |
| `OutcomeKind` | enum | Yes | `Quantitative` or `Qualitative`; immutable after first measurement |
| `CurrentVersionId` | Guid | Yes | Points to an immutable same-tenant OutcomeVersion |
| `LifecycleState` | enum | Yes | Closed `Active`, `Retired`; retirement is append-only disposition and cannot reopen |
| `ObservationStatusProjection` | read-only derived value | No persisted authority | `NoObservation`, `Current`, `Stale`; policy `decision-intelligence.observation-status-policy` `1.0` |
| base/audit fields | governed | Yes | Logical tenant-owned `EntityBase` audit/concurrency contract; any inherited delete fields remain unused and no delete mutation is exposed |

### OutcomeVersion

| Field | Type | Required | Invariant |
|---|---|---:|---|
| `Id`, `OutcomeId`, `OutcomeVersionNumber` | Guid, Guid, int | Yes | Unique `(TenantId, OutcomeId, OutcomeVersionNumber)`; business version number strictly increases |
| `Definition` | string | Yes | Trim + NFC; max 4000 |
| `MetricReference` | exact typed reference? | Conditional | Quantitative requires `decision-intelligence.outcome-metric-reference` `1.0`; qualitative forbids it |
| `QualitativeScaleReference` | exact typed reference? | Conditional | Qualitative requires `decision-intelligence.qualitative-scale-reference` `1.0`; quantitative forbids it |
| `EffectiveFrom` | UTC instant | Yes | Version applicability; immutable |
| `CreatedByActorId`, `CreatedAt` | trusted actor, UTC | Yes | Server-derived immutable provenance |

### OutcomeMetric and immutable OutcomeMetricVersion

Metric reference contract `decision-intelligence.outcome-metric-reference` / `1.0` contains exactly
`contractName`, `contractVersion`, `metricId`, `metricVersionId`, `metricVersionNumber`.

| Field | Type | Required | Invariant |
|---|---|---:|---|
| `MetricId`, `MetricVersionId`, `MetricVersionNumber` | Guid, Guid, int | Yes | Same-tenant visible immutable version; monotonically numbered server-side |
| `Code`, `Name` | string | Yes | Trim + NFC; Code active-unique per tenant; Name 1..200 |
| `UnitKind` | enum | Yes | Closed `Count`, `Percentage`, `Currency`, `Days`, `Score` |
| `CurrencyReference` | typed reference? | Conditional | Currency requires exact `fpa.currency-reference` `1.0`; every other UnitKind requires null |
| `DecimalScale` | int | Yes | Exactly `4`; immutable |
| `MinimumValue`, `MaximumValue` | decimal | Yes | Scale 4; inside absolute hard bound; minimum <= maximum; immutable |
| `FreshnessDays` | int | Yes | `1..366`; immutable |

Quantitative persistence uses BSON Decimal128 / CLR `decimal`, precision 28 and persisted scale exactly 4.
Input scale above 4 returns `400`; silent rounding, NaN, infinity and binary floating-point payloads are
forbidden. Absolute hard bound is `-1,000,000,000,000,000.0000` through
`1,000,000,000,000,000.0000`. Percentage bounds are exactly `0.0000..100.0000`; Count and Days minimum is
at least `0.0000`; Score defaults to and cannot exceed `0.0000..100.0000`. Measurement inherits unit/currency
from the exact MetricVersion and cannot select or mix them.

### QualitativeScale and immutable QualitativeScaleVersion

Scale reference contract `decision-intelligence.qualitative-scale-reference` / `1.0` contains exactly
`contractName`, `contractVersion`, `qualitativeScaleId`, `qualitativeScaleVersionId`,
`qualitativeScaleVersionNumber`.

Each immutable scale version contains `2..20` levels. A level contains exactly `LevelKey`, `Ordinal`, `Label`:
`LevelKey` is Trim + NFC, 1..64 and case-sensitive unique; `Ordinal` is positive and unique; `Label` is NFC,
1..200. A qualitative measurement stores only the exact `LevelKey`. Free-text qualitative result/narrative
cannot be authoritative. Scale changes create a new version and never reinterpret old measurements.

### OutcomeMeasurement

| Field | Type | Required | Invariant |
|---|---|---:|---|
| `Id`, `OutcomeId`, `OutcomeVersionId` | Guid | Yes | Same tenant; version must exist and be referenceable |
| `PeriodStart`, `PeriodEnd` | LocalDate | Yes | Immutable half-open `[start,end)`; start < end; maximum 366 days |
| `QuantitativeValue` | decimal? | Conditional | Exactly one of quantitative value or qualitative result |
| `QualitativeLevelKey` | string? | Conditional | Exact key from the referenced immutable scale version; no narrative result |
| `EvidenceReferences` | empty list | Yes | Must be exactly empty in v1; no provider call, local/mock document ID or evidence API/UI |
| `RecordedByActorId`, `DelegatedActorId` | trusted actor ids | Yes/No | Server-derived; delegated provenance preserved separately |
| `IdempotencyKey` | string | Yes | Unique per tenant + operation + actor/client scope |
| `SupersedesMeasurementId` | Guid? | Correction only | Normal append null; correction targets exact current-effective measurement in same Outcome/version/period |
| `CorrectionReason` | enum? | Correction only | Closed `DataCorrection`, `SourceRestatement`, `MethodologyCorrection`; required for correction |

Required indexes include active `(TenantId, Code)` uniqueness, unique
`(TenantId, OutcomeId, OutcomeVersionNumber)`, inherited technical concurrency `Version`, unique idempotency
scope, unique metric/scale business version indexes, effective OutcomeVersion/period overlap lookup and
Decision reference lookup. Exact Mongo index definitions wait for topology approval. `OutcomeVersionNumber`,
`MetricVersionNumber` and `QualitativeScaleVersionNumber` are business ordinals; inherited `Version` remains
exclusively the technical optimistic-concurrency token and never appears as a business version field.

## 5. Repo Scope

This governance authoring job owns only:

- `execution/domains/enterprise-strategy-business-performance/module-packs/MOD-0072-decision-logs-outcome-tracking.md`
- `execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0072/audit/**`

Any future implementation remains non-executable and requires this pack to become `approved` or
`ready-for-dev`, explicit runtime/deployment authority, authoritative port, approved S2S/MOD-0018 and
MOD-0021/MOD-0035 contracts, approved bootstrap fixture content, producer runtime evidence and matching
MOD-0117 consumer activation gates. Core composition/collection/transaction decisions below are closed
governance contracts, not implementation authority.

### Exact future primary logical scope

These reserved logical paths define the only future production location; they authorize no file creation:

- `services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Api/Modules/OutcomeTracking/**`
- `services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Application/Modules/OutcomeTracking/**`
- `services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Domain/Modules/OutcomeTracking/**`
- `services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Persistence/Modules/OutcomeTracking/**`
- `services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Infrastructure/Modules/OutcomeTracking/**`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.UnitTests/Modules/OutcomeTracking/**`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.IntegrationTests/Modules/OutcomeTracking/**`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.ArchitectureTests/Modules/OutcomeTracking/**`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.ProjectionTests/Modules/OutcomeTracking/**`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.ContractTests/Modules/OutcomeTracking/**`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.SecurityTests/Modules/OutcomeTracking/**`

Exact future composition files are:

- `...Api/Modules/OutcomeTracking/OutcomeTrackingModule.cs` — sole module composition entry point.
- `...Application/Modules/OutcomeTracking/DependencyInjection.cs` — Application registrations only.
- `...Domain/Modules/OutcomeTracking/Common/OutcomeTrackingEntityBase.cs` — concrete service-local logical
  `EntityBase`; importing an EnterpriseStrategy/FP&A/shared business base is forbidden.
- `...Persistence/Modules/OutcomeTracking/DependencyInjection.cs` — repositories, Mongo mappings and local
  transaction participants only.
- `...Infrastructure/Modules/OutcomeTracking/DependencyInjection.cs` — post-commit public abstractions only;
  no Domain/Persistence ownership.

Concrete future test-project files are exactly:

- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.UnitTests/Diten.DecisionIntelligenceService.UnitTests.csproj`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.IntegrationTests/Diten.DecisionIntelligenceService.IntegrationTests.csproj`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.ArchitectureTests/Diten.DecisionIntelligenceService.ArchitectureTests.csproj`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.ProjectionTests/Diten.DecisionIntelligenceService.ProjectionTests.csproj`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.ContractTests/Diten.DecisionIntelligenceService.ContractTests.csproj`
- `services/Diten.DecisionIntelligenceService/tests/Diten.DecisionIntelligenceService.SecurityTests/Diten.DecisionIntelligenceService.SecurityTests.csproj`

The single future owner manifest is exactly
`services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Persistence/Modules/OutcomeTracking/Manifests/OutcomeTrackingMongoOwnershipManifest.cs`.
No second manifest, convention scan or configuration override may claim an OutcomeTracking collection/index.

### Exact collection owner manifest

| Collection name | Sole owner | Purpose |
|---|---|---|
| `diten_di_outcomes` | OutcomeTracking | Outcome identity/lifecycle/current-version pointer |
| `diten_di_outcome_versions` | OutcomeTracking | Immutable OutcomeVersion history |
| `diten_di_outcome_metrics` | OutcomeTracking | OutcomeMetric identity |
| `diten_di_outcome_metric_versions` | OutcomeTracking | Immutable metric semantics/bounds/freshness |
| `diten_di_qualitative_scales` | OutcomeTracking | QualitativeScale identity |
| `diten_di_qualitative_scale_versions` | OutcomeTracking | Immutable level definitions |
| `diten_di_outcome_measurements` | OutcomeTracking | Append-only observations/correction chains |
| `diten_di_decision_outcome_links` | OutcomeTracking | Append-only DecisionOutcome provenance |
| `diten_di_idempotency_receipts` | OutcomeTracking | Permanent v1 mutation receipts/request hashes/results |
| `diten_di_audit_intents` | OutcomeTracking | Producer-local pre-publication audit intents |
| `diten_di_outbox_messages` | OutcomeTracking | Producer-local post-commit delivery state |

No collection exists for ObservationStatus, OutcomeReferenceability, projection checkpoints, rebuild
checkpoints, Metric/Scale authoring drafts, evidence, PPM wrappers/backlinks or legacy migration mirrors.

### Tenant-first named index matrix

Every tenant-owned index begins with `TenantId`; startup/owner-manifest validation fails on a missing, renamed,
foreign-owned or key-order-divergent index.

| Exact index name | Collection | Ordered key / constraint |
|---|---|---|
| `ux_di_ot_outcomes_tenant_code_active` | `diten_di_outcomes` | `(TenantId, Code)` unique for `LifecycleState=Active` |
| `ix_di_ot_outcomes_tenant_lifecycle` | `diten_di_outcomes` | `(TenantId, LifecycleState, Id)` |
| `ux_di_ot_outcome_versions_tenant_outcome_number` | `diten_di_outcome_versions` | `(TenantId, OutcomeId, OutcomeVersionNumber)` unique |
| `ix_di_ot_outcome_versions_tenant_outcome_effective` | `diten_di_outcome_versions` | `(TenantId, OutcomeId, EffectiveFrom, Id)` |
| `ux_di_ot_metrics_tenant_code_active` | `diten_di_outcome_metrics` | `(TenantId, Code)` unique for active identity |
| `ux_di_ot_metric_versions_tenant_metric_number` | `diten_di_outcome_metric_versions` | `(TenantId, MetricId, MetricVersionNumber)` unique |
| `ux_di_ot_scales_tenant_code_active` | `diten_di_qualitative_scales` | `(TenantId, Code)` unique for active identity |
| `ux_di_ot_scale_versions_tenant_scale_number` | `diten_di_qualitative_scale_versions` | `(TenantId, QualitativeScaleId, QualitativeScaleVersionNumber)` unique |
| `ix_di_ot_measurements_tenant_outcome_version_period` | `diten_di_outcome_measurements` | `(TenantId, OutcomeId, OutcomeVersionId, PeriodStart, PeriodEnd, Id)` overlap/effective-head lookup |
| `ux_di_ot_measurements_tenant_supersedes` | `diten_di_outcome_measurements` | `(TenantId, SupersedesMeasurementId)` unique when non-null; prevents correction branching |
| `ix_di_ot_measurements_tenant_recorded` | `diten_di_outcome_measurements` | `(TenantId, OutcomeId, RecordedAt, Id)` history/status scan |
| `ux_di_ot_decision_links_tenant_tuple` | `diten_di_decision_outcome_links` | `(TenantId, OutcomeId, OutcomeVersionId, DecisionId, DecisionVersionId)` unique |
| `ux_di_ot_receipts_tenant_actor_operation_key` | `diten_di_idempotency_receipts` | `(TenantId, ActorId, Operation, IdempotencyKey)` unique; no TTL |
| `ix_di_ot_audit_intents_tenant_state_created` | `diten_di_audit_intents` | `(TenantId, State, CreatedAt, Id)` |
| `ux_di_ot_outbox_tenant_message` | `diten_di_outbox_messages` | `(TenantId, MessageId)` unique |
| `ix_di_ot_outbox_tenant_state_available` | `diten_di_outbox_messages` | `(TenantId, State, AvailableAt, Id)` |

## 6. Protected Paths

- `.antigravity/**`
- `services/**`, `frontend/**`, `gateway/**` for this draft
- Future co-hosting/persistence access in `Diten.FpaService`, `Diten.BudgetingService`,
  `Diten.ScenarioPlanningService` and `Diten.EnterpriseStrategyService` is permanently forbidden for MOD-0072.
- `gateway/Diten.ApiGateway/**/ocelot.json` except a separately authorized integration-agent task
- `execution/registries/**`, `execution/portfolio/**` and all other shared governance files
- Other domains' module packs and runtime ownership
- Archive controllers/views and frozen layouts defined by root `AGENTS.md`

## 7. Dependencies

| Dependency | Contract role | Draft state |
|---|---|---|
| [ESBP domain topology authority](../domain-config.md#decision-intelligence-topology) | Binding physical placement and isolation | Closed: only `Diten.DecisionIntelligenceService/Modules/OutcomeTracking`; implementation remains unauthorized |
| MOD-0117 | Owns planned BenefitCommitment plus `BenefitCommitment → OutcomeReference` `0..n` relationship, stored values and consumer lifecycle | Consumer amendment and runtime authority required; no dual persistence in MOD-0072 |
| MOD-0007 | Optional Decision/DecisionVersion reference producer | Exact producer contract must be approved before runtime link |
| MOD-0023 | Approval only when separately required | No approval behavior in MOD-0072 |
| `fpa.currency-reference` `1.0` provider | Exact currency identity for `UnitKind.Currency` only | Bilateral contract/runtime evidence remains a B-class integration concern |
| MOD-0031 | Future typed/versioned evidence amendment | Formally deferred from v1 core; no runtime call or evidence surface |
| MOD-0018-FU16 | Exact service identity, delegated actor/request binding and permission-manifest intake | Governance contract closed below; runtime provisioning/evidence required |
| MOD-0021 / MOD-0035 | Producer-specific audit mapping/fixture and signed post-commit event onboarding | Governance contract closed below; runtime integration approval/evidence required |
| DCP-006 | Gate I-C orchestration and no-copy boundary | OD-04 remains OPEN/PARTIAL |

## 8. Runtime Constraints

- MongoDB, tenant isolation, server-derived tenant/actor and four CQRS pipeline behaviors apply. V1 Outcome,
  versions, metrics/scales, measurements, correction chains, DecisionOutcomeLink and audit history expose no
  ordinary delete, soft-delete, hard-delete, bulk-delete, TTL or exceptional purge operation.
- `module-pack-standard.md` permits only `EntityBase`, `BaseEntity` or `GlobalEntity`, not `TBD`.
  Frontmatter therefore retains `entity_base: EntityBase` as the mandatory logical tenant-owned base contract.
  It does not invent or select a concrete service-local CLR base type; that implementation remains blocked
  until topology/service approval.
- Cross-tenant, missing or actor-invisible resources return indistinguishable `404`.
- Existing immutable versions and measurements are never updated in place. Stale version/correction attempts
  return `409`; correction creates a new append-only record with complete provenance.
- Unknown dependency, unavailable required validation provider or unknown contract version fails closed with
  `503`; PPM has no fail-open or cached-truth fallback. EvidenceReferences is always empty in v1, so core
  measurement never calls an evidence provider.
- Numeric values, exact scale, unit/currency inheritance and hard bounds are fixed by the immutable
  OutcomeMetricVersion contract above.
- Business mutation plus producer-local audit intent/outbox write is atomic. Audit enqueue failure rolls back
  the business mutation.
- Every append/correction/DecisionOutcomeLink mutation requires an idempotency key. Same key + identical canonical payload
  is a no-op with the original result; same key + different payload returns `409`.
- S2S validation requires a trusted service identity, delegated end-user tenant/actor provenance when the
  consumer acts for a user, exact audience/scope, correlation/causation IDs and allowlisted contract version.
- Client payloads cannot assert `TenantId`, effective actor, delegated actor, authorization or referenceability.

### Service, persistence and transaction isolation

- OutcomeTracking owns its Domain types, Application handlers/contracts, repository interfaces, Persistence
  implementations, Mongo owner manifest, collections, indexes, idempotency/fence records, projection rebuild
  state and producer-local audit intent/outbox exclusively inside `Diten.DecisionIntelligenceService`.
- `Diten.FpaService`, `Diten.BudgetingService`, `Diten.ScenarioPlanningService` and
  `Diten.EnterpriseStrategyService` cannot host OutcomeTracking, reference its owned Domain/Persistence types,
  inject its repositories/context, or read/write its collections/indexes.
- OutcomeTracking cannot read/write FP&A or EnterpriseStrategy collections/indexes or resolve their
  repositories/context. No shared discriminator collection, database model, Mongo session, transaction,
  idempotency store, audit intent or outbox exists across these boundaries.
- Each OutcomeTracking Mongo session/replica-set transaction contains only OutcomeTracking-owned aggregate,
  idempotency/fence and local audit-intent/outbox records. Cross-service/distributed Mongo transactions and
  shared unit-of-work abstractions are forbidden.
- Communication with Budgeting, ScenarioPlanning, Enterprise Strategy evidence and MOD-0117 occurs only via
  approved typed/versioned reference contracts. Foreign entities, repositories, collection/session handles,
  persistence DTOs and mutable payload copies are forbidden.
- OutcomeTracking has one authoritative SoR deployment only. It does not participate in the FP&A primary/
  fallback model; no second host, fallback replica authority, dual-write, mirror-write or active/passive SoR
  may operate the same OutcomeTracking truth.

### Core transaction, idempotency and unknown-commit contract

Every create, publish-version, retire, measurement append/correct and DecisionOutcomeLink mutation executes in
one OutcomeTracking-owned Mongo replica-set transaction containing exactly:

1. the aggregate/entity append or compare-and-set mutation;
2. one immutable `diten_di_idempotency_receipts` record with tenant, effective actor, operation, exact
   IdempotencyKey, canonical request hash, final response/status and mutation identity;
3. one or more producer-local `diten_di_audit_intents` records required by the mutation; and
4. corresponding producer-local `diten_di_outbox_messages` records when post-commit publication is required.

Failure to persist any participant aborts all participants. Repository code cannot commit the business record
before receipt/audit/outbox, and no external call or broker publish occurs inside the transaction.

- Same tenant+actor+operation+key with the same canonical payload hash returns the stored final response and
  performs no second mutation, audit intent or outbox append.
- The same scoped key with a different canonical payload hash returns `409` and persists nothing.
- V1 receipts are permanent: no TTL index, cleanup job, expiry field or implicit retention truncation exists.
- On a Mongo unknown-commit result, the application retries **commit only** using the same session/transaction
  identity as allowed by the driver; it never reruns handlers, business mutation logic or external calls.
- After bounded commit-only retry, the application reconciles the exact scoped receipt. A matching committed
  receipt returns its stored result; a confirmed absent/aborted receipt permits the normal safe failure path;
  conflicting receipt returns `409`. If commit state remains indeterminate, return `503` and do not claim
  success, start a fresh mutation or publish externally.
- Caller retry after `503` uses the same idempotency key and is resolved solely through the receipt contract.

### Projection and rebuild non-persistence

`ObservationStatusProjection` and `OutcomeReferenceabilityProjection` are read results, never persisted
documents or authorization cache. Rebuild means deterministic recomputation in tests from immutable Outcome,
version, metric/scale and effective measurement heads. It writes no projection, checkpoint, watermark, cursor,
lease or rebuild-status collection. The owner manifest and architecture tests fail any collection/index,
repository, migration or DI registration for observation status/referenceability/checkpoints.

### Post-commit publication boundary

Broker publication occurs only after a successful commit through the future MOD-0035-approved public
`IEventBus` abstraction. Direct RabbitMQ client, direct MassTransit bus usage, direct broker channel/session,
or direct Platform/MOD-0021 audit collection write/read is forbidden. The exact MOD-0021/MOD-0035 producer
contract is closed by the Gate I onboarding reconciliation below. Until its producer fixture, consumer mapping,
signing onboarding and runtime evidence are separately approved, outbox rows cannot be published and runtime
activation remains blocked.
At-least-once delivery requires idempotent consumers and never changes the local commit result.

### Metric/scale bootstrap and initial migration

The initial slice exposes no Metric/Scale authoring command, handler, endpoint or UI and creates no permission;
the exact seven permissions remain unchanged. Initial Metric/Scale data may enter only through a future,
separately approved, versioned and immutable bootstrap fixture. Without approved fixture content and
reconciliation evidence, quantitative and qualitative production activation is forbidden. Metric/Scale
administration is a separate future amendment.

The first legacy migration slice is **NO-OP / quarantine-only**: it inventories and quarantines candidates but
creates no OutcomeTracking aggregate, collection mirror, projection or compatibility record. Legacy dual-read,
fallback-read, dual-write, shadow-write and read-through migration are forbidden. Any later transformation
requires a separately approved expand/contract, tenant/provenance mapping, reconciliation and rollback plan.

### Measurement and version model decision

The parent pack fixes a closed model: `OutcomeVersion` is an immutable semantic baseline; measurements are
append-only observations against exactly one baseline; corrections/restatements supersede rather than mutate;
historical versions and measurements remain queryable. A correction/restatement is itself a new
`OutcomeMeasurement` with `SupersedesMeasurementId`, `CorrectionReason` and complete correction provenance;
there is no separate correction aggregate/entity. Quantitative and qualitative outcomes are both
supported but cannot mix their exact references/result modes inside one Outcome version.

Outcome mutable content is never edited directly. Semantic change publishes a new immutable OutcomeVersion;
server-side `OutcomeVersionNumber` increases monotonically and the former version remains historical.
Lifecycle is closed `Active → Retired`; a retired Outcome cannot reopen and new work requires a new Outcome.

Measurement periods are immutable tenant-local LocalDate half-open ranges `[PeriodStart, PeriodEnd)`. Start is
before End, maximum duration is 366 days, End cannot exceed tenant-local current date + 1 day, and Start cannot
precede tenant-local current date - 10 years. Effective measurements for one OutcomeVersion cannot overlap;
adjacent periods are valid and overlap returns `409`. A correction uses the target's exact period. No timezone
conversion is applied.

A normal append has no `SupersedesMeasurementId`. Correction targets the exact current-effective head within
the same tenant, Outcome, OutcomeVersion and period. It appends a new immutable measurement; the original is
never updated/deleted. Chains are linear, acyclic and single-head; stale target or second branch returns `409`.
V1 has no correction time limit. Legal hold prevents history deletion but not an authorized append-only
correction. Free-text comment is not correction authority; the exact existing
`decision-intelligence.measurements.correct` permission applies without alias/wildcard.

### Observation status policy v1

Policy **`decision-intelligence.observation-status-policy` / `1.0`** derives only `NoObservation`, `Current`,
`Stale`. No effective measurement yields `NoObservation`. Otherwise only the correction chain's effective head
participates: when the newest effective measurement `PeriodEnd` is on or after
`asOfLocalDate - OutcomeMetricVersion.FreshnessDays`, status is `Current`; otherwise `Stale`. The projection is
not persistence authority and must reproduce identically on every read or deterministic rebuild. There is no
manual status mutation/UI. Achievement/realized comparison against PPM BenefitCommitment target is deferred
until a future typed planned-target contract; PPM stores neither actual value, measurement nor projection.

### Retirement, retention and historical integrity

Retirement is an append-only lifecycle disposition, never deletion. It is allowed even when external
references exist because `HistoricalResolve` continues to resolve retired Outcome/version history; existing
PPM references are never rewritten or deleted. Retirement makes `NewReferenceEligibility` return `409`.
Outcome, versions, metrics/scales, measurements, correction chains, DecisionOutcomeLink and audit history have
no TTL/delete/bulk-delete. V1 has no exceptional purge. Legal hold reinforces deletion prohibition. A future
purge/delete requires MOD-0030 authority plus a separate amendment. External-reference delete eligibility is
not a runtime blocker because v1 exposes no delete operation. Provider outage does not prevent local historical
read; a new-reference eligibility call may still fail closed with `503` when its required dependency is unavailable.

## 9. Layout & Shell Contract

Proposed shell is `tenant`; every future Razor page must explicitly declare
`Layout = "_LayoutTenantShell"`. Proposed view area is `Views/DecisionIntelligence/OutcomeTracking/`.

Evaluated tenant surfaces:

| Surface | Purpose | Form fields | Decision |
|---|---|---:|---|
| Outcome create | Code, Title, OutcomeKind | 3 | Slim |
| OutcomeVersion create | Definition, MetricReference or QualitativeScaleReference, EffectiveFrom | max 3 | Slim |
| MetricVersion | Code, Name, UnitKind, CurrencyReference, MinimumValue, MaximumValue, FreshnessDays | 7 | Slim |
| QualitativeScale | Code, Name, repeatable LevelKey, Ordinal, Label | 5 | Slim |
| Measurement append/correct | PeriodStart, PeriodEnd, QuantitativeValue or QualitativeLevelKey, SupersedesMeasurement, CorrectionReason | max 5 | Slim |
| Realization details | Read-only version/measurement/link history | 0 | Details/read-only |
| Decision-outcome trace | Read-only trace plus separately permitted link management | up to 3 | Embedded/detail surface |

Parent maximum `form_field_count: 7` therefore selects Golden Reference **Slim**. Measurement/reference/result
details remain separate read-only history. No PPM actual-value edit
form and no approval button/action may be produced.

## 10. Backend File Convention

Future implementation, if separately authorized, follows the Slim Golden Reference shape per object:
`Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/` and one
`{Object}Models.cs`. Commands/queries are sealed records; handlers are sealed classes named
`{Verb}{Object}Handler`; validators are `{Verb}{Object}Validator`. `Command`, `Query` or `Request` suffixes
on handler/validator names and generic `OutcomeTrackingContext` handlers are forbidden.

The only reserved host/module boundary is
`services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.{Api|Application|Domain|Persistence|Infrastructure}/Modules/OutcomeTracking/**`.
This logical reservation is not scaffold or runtime authority. Host composition, concrete project/test paths
and every file remain blocked until an approved implementation slice enumerates them.

## 11. Frontend File Contract

Future Slim surfaces use `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`,
`_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, marker class, localized JavaScript and seven-language
RESX resources. `data-dt-standard="v2"`, skeleton and explicit layout contracts apply. Bulk delete remains
forbidden by the no-delete policy.

Realization details and decision trace default to read-only displays. Decision provenance link controls are
gated by `decision-intelligence.decision-links.manage`. MOD-0072 exposes no BenefitCommitment relationship
mutation UI. Realization status is never editable. There is no PPM-owned actual edit surface and no approval UI.

## 12. Validation Rules

| Field / operation | Required | Format/rule | DB-level | Pre-check |
|---|---:|---|---|---|
| `Code` | Yes | NFC, max 64 | active tenant unique | duplicate check |
| `Title` | Yes | NFC, max 200 | — | — |
| `OutcomeKind` | Yes | closed enum | — | immutable after measurement |
| definition | Yes | max 4000 | immutable version | — |
| `OutcomeVersionNumber` | Yes | positive, strictly increasing business ordinal; never supplied as inherited concurrency `Version` | unique tenant/outcome index | current-version compare-and-swap |
| MetricReference | Quantitative | exact five-field metric reference v1; qualitative forbids | indexed | same-tenant visible immutable version |
| QualitativeScaleReference | Qualitative | exact five-field scale reference v1; quantitative forbids | indexed | same-tenant visible immutable version |
| UnitKind/bounds/FreshnessDays | MetricVersion | closed unit; scale 4; hard/unit-specific bounds; freshness 1..366 | immutable version | currency contract when required |
| qualitative levels | ScaleVersion | 2..20; exact LevelKey/Ordinal/Label rules | unique key/ordinal | — |
| period | Yes | LocalDate `[start,end)`, max 366 days, future +1 day, backfill 10 years | effective overlap index/query | tenant-local date |
| quantitative value | Conditional | Decimal128/decimal precision 28 scale exactly 4; hard and metric bounds; no rounding/float | persisted decimal | metric version |
| qualitative level key | Conditional | exact case-sensitive key; no authoritative free text | — | scale version |
| evidence refs | Yes | exact empty list in v1 | — | no provider call |
| Decision ref | When trace required | typed/versioned; same tenant/visible | link index | MOD-0007 validation |
| correction | Conditional | exact effective head, same Outcome/version/period, closed reason, no cycle/branch | immutable append | current head |
| idempotency key | Mutation | non-empty, bounded, scoped | unique index | canonical payload hash |

## 13. Failure Path to Verify

- Duplicate Outcome code: `409`; no record/audit residue.
- Missing, cross-tenant or invisible Outcome/dependency: indistinguishable `404`.
- Unauthorized known same-tenant action: `403`; no mutation.
- Stale version or immutable overwrite/correction conflict: `409`; history unchanged.
- Malformed period, scale above 4, incompatible unit/currency, out-of-bound value or invalid qualitative key:
  `400`; no append.
- Effective-period overlap, stale correction target/branch, retired new-reference eligibility or invalid
  lifecycle transition: `409`; history unchanged.
- EvidenceReferences non-empty, mock/local document ID or tenant policy requiring v1 evidence: `409`; no
  provider call and no append.
- Unknown contract version, required provider timeout/unavailable/malformed response or indeterminate
  dependency: `503`; fail closed. Local `HistoricalResolve` remains available when no dependency is required.
- Ordinary delete/soft-delete/hard-delete/bulk-delete/purge endpoint or command is absent in v1; no delete
  semantics are inferred from retirement.
- Audit intent/outbox persistence failure: complete transaction rollback.
- Repeated identical idempotency request: original result/no duplicate; differing payload: `409`.
- Unknown commit: bounded commit-only retry followed by exact scoped receipt reconciliation; matching receipt
  returns stored result, conflicting receipt returns `409`, and unresolved commit state returns `503`. Handler,
  mutation, audit creation and outbox creation are never rerun during commit-only retry.
- Projection/rebuild attempt to persist ObservationStatus/referenceability/checkpoint state: architecture/
  owner-manifest failure; no collection or mutation is allowed.
- Direct RabbitMQ/MassTransit/broker or Platform audit collection access: architecture failure; publishing is
  disabled until the exact MOD-0021/MOD-0035 contract is approved and then occurs post-commit via `IEventBus`.
- PPM attempts to submit actual value/period/evidence/realized state: contract rejection; no copied truth.

## 14. Authorization Convention

Tenant endpoints require `[Authorize]`, entitlement and exact `[HasPermission]` checks. Tenant and actor are
resolved server-side. A delegated call preserves service identity, effective end-user actor and delegation
provenance as separate trusted values. Read visibility is re-evaluated by the producer; possession of a GUID
is not authorization.

The OutcomeReference provider is logically owned by
`Diten.DecisionIntelligenceService.Application/Modules/OutcomeTracking` with its future service-network Api
adapter. Validation is S2S-only with `decision-intelligence.outcome-references.validate`, explicit
audience/scope, trusted tenant propagation and delegated actor context. A service credential alone may not
expand the user's visibility. Security failures never fall back to PPM-local cached actual data.

## 15. Gateway / API Routing Decision

Decision: **no Gateway/browser route for internal OutcomeReference validation**. The future provider is
service-network-only; browser traffic and same-origin MVC proxies cannot call or expose it. Its logical owner
is the isolated OutcomeTracking module, not PPM, FP&A, EnterpriseStrategy or Gateway. Exact internal endpoint,
S2S credential, service discovery and port remain B-class blockers. The authoritative port allocation is
`TBD`; no port is guessed. Any future tenant UI/public API route is a separate explicitly approved runtime
slice and must use frontend `5001` → Gateway `5000`, never a direct service port. This pack changes no route or
protected `ocelot.json`.

### Gate I-C OutcomeReferenceV1 typed/versioned contract

Proposed exact contract name: **`diten.decision-intelligence.outcome-reference`**, semantic version **`1.0`**.
Version 1 is a synchronous authoritative referenceability query/response, not a replicated outcome payload.

The persisted `OutcomeReferenceV1` contains **exactly five fields**:

| Field | v1 semantic |
|---|---|
| `contractName` | Exact `diten.decision-intelligence.outcome-reference` |
| `contractVersion` | Exact allowlisted `1.0`; unknown version → `503` |
| `outcomeId` | Stable MOD-0072 Outcome identity |
| `outcomeVersionId` | Exact immutable semantic baseline identity |
| `outcomeVersionNumber` | Positive business revision ordinal; distinct from inherited concurrency `Version` |

Successful validation returns `200` with the same exact five canonical fields in a non-persisted,
non-cacheable response projection; there is no stored or returned `isReferenceable` boolean. Missing,
cross-tenant, invisible or otherwise non-disclosable tuples return indistinguishable `404`. A visible tuple's
lifecycle eligibility is evaluated by the requested supported mode: retired/superseded history may return
`200` for `HistoricalResolve`, while visible new-link ineligibility returns `409` for
`NewReferenceEligibility`. The response does **not** add actual value, unit, evidence, realization state or
relationship context. Success means the exact tuple is same-tenant, actor-visible and valid for that explicit
mode; it is not a promise of eligibility for another mode or future time.
It cannot become a collection, a cached authorization decision, a replicated actual-value model or a
PPM-owned projection.

MOD-0117 owns cardinality `BenefitCommitment → OutcomeReference = 0..n`, the stored five-field references and
adding/removing/retiring that consumer relationship. One Outcome contains `1..n` immutable versions and
`0..n` measurements; a measurement belongs to exactly one OutcomeVersion. An Outcome is linkable before its
first measurement. PPM never stores `measurementId`, measurement period, actual value, evidence or realization
state. MOD-0072 validates its own Outcome identity/version/referenceability only; it does not mutually persist
or own the PPM relationship.

Where an approved policy requires consumer context, an OutcomeReference validation **request** may carry a
typed BenefitCommitment context outside the persisted five-field value. That context is validation input only,
does not become part of OutcomeReferenceV1, is not echoed in the five-field response, cannot override trusted
tenant/actor identity and creates no MOD-0072 relationship ownership or backlink truth. Context-free base
validation makes no MOD-0117 or MOD-0007 call.
Likewise, `decisionReference` is excluded from OutcomeReferenceV1 and belongs only to a separate
DecisionOutcomeLink provenance command/contract. MOD-0007 availability is irrelevant to OutcomeReference
validation unless a decision-provenance operation is explicitly requested.

### PPM bilateral Gate I-C consumer profile

MOD-0117 owns exact wrapper **`ppm.benefit-commitment-outcome-reference` / `1.0`**. Its persisted value contains
exactly four fields and no others:

| Field | Exact bilateral serialization rule |
|---|---|
| `ContractName` | Exact `ppm.benefit-commitment-outcome-reference` |
| `ContractVersion` | Exact `1.0` |
| `BenefitCommitmentId` | Non-empty MOD-0117-owned identifier |
| `OutcomeReference` | Exact, unmodified nested five-field MOD-0072 `OutcomeReferenceV1` tuple |

The wrapper is the PPM-owned relationship value. MOD-0117 owns its `0..n` cardinality and attach/detach/retire
lifecycle. It never adds or copies actual value, measurement, period, evidence or realization state. MOD-0072
does not persist the wrapper, reverse relationship or consumer lifecycle.

#### Exact mode fixture

| Mode | MOD-0072 producer behavior |
|---|---|
| `HistoricalResolve` | Resolves the exact OutcomeReferenceV1 tuple even when the Outcome/version is retired or superseded; historical resolution never authorizes a new PPM relationship |
| `NewReferenceEligibility` | Returns success only when the exact visible Outcome/version is eligible for a new relationship; visible retired, superseded or otherwise ineligible state returns `409` |
| `CurrentSelectionEligibility` | Rejected with `400`; OutcomeReference is a non-selection contract |

#### Exact error fixture

| Condition | HTTP result |
|---|---:|
| Malformed caller request, unexpected field set or unsupported mode for this non-selection contract | `400` |
| Authentication or trusted-context failure | `401` |
| Permission or dedicated S2S entitlement failure | `403` |
| Missing, cross-tenant, invisible or otherwise non-disclosable Outcome/version | `404` |
| Visible but retired/superseded/ineligible for `NewReferenceEligibility` | `409` |
| Unknown/unsupported wrapper or nested producer version, timeout, provider unavailable, malformed authoritative response or indeterminate dependency | `503` |

Supported-version behavior is fail-closed and exact: the bilateral adapter supports only wrapper
`ppm.benefit-commitment-outcome-reference` `1.0` with nested producer
`diten.decision-intelligence.outcome-reference` `1.0`. Names and versions are case-sensitive; no alias,
normalization, downgrade, nearest-version selection, fallback, cached allow or partial-field tolerance exists.
An unknown version or a response whose name/version/field allowlist does not match its exact fixture returns
`503`. Delete/retirement eligibility remains separately fail-closed: unavailable, malformed, unsupported-version
or indeterminate authoritative external-reference validation returns `503`, never `409` or a local inference.

#### Bilateral fixture assertions

- Wrapper serialization property set equals exactly `ContractName`, `ContractVersion`,
  `BenefitCommitmentId`, `OutcomeReference`.
- Nested serialization property set equals exactly `contractName`, `contractVersion`, `outcomeId`,
  `outcomeVersionId`, `outcomeVersionNumber`; casing is preserved by the approved producer fixture and the
  nested value is byte/semantic-field equivalent after round-trip.
- Extra actual/measurement/period/evidence/realization fields fail fixture validation and are never persisted.
- Mode fixtures exercise all three exact tokens and prove `HistoricalResolve` and `NewReferenceEligibility`
  have distinct lifecycle meaning while `CurrentSelectionEligibility` is `400`.
- Error fixtures prove exact `400/401/403/404/409/503` mapping without existence disclosure or fail-open.

### OutcomeMeasurementReference — deferred from v1

V1 has no OutcomeMeasurementReference requirement, contract name, identity, version or consumer storage.
The capability is explicitly **DEFERRED** to a future separately approved amendment. MOD-0117 stores only the
five-field OutcomeReferenceV1 and never measurement identity, period, value, evidence or observation projection.

### Gate I onboarding reconciliation — producer-owned exact contract

> **Status boundary:** This subsection is `draft` governance authority only. It closes MOD-0072's producer
> declarations against MOD-0018-FU16 checkpoint `40e38905`, MOD-0021 checkpoint `de81a135` and MOD-0035
> checkpoint `23f290ec`. It creates no runtime/configuration/secret/route/deployment authority, does not make
> either the audit event or the PPM adapter executable and does not change `production_authority: none`.

#### Service identity and exact permission manifest

- Service audience: exact `diten-decision-intelligence-service`.
- Client ID: exact `diten.decision-intelligence`.
- Permission-manifest owner module: exact `MOD-0072`; workload identity, module entitlement and permission
  ownership remain separate gates and are never inferred from one another.
- Delegated calls use MOD-0018-FU16 `DelegatedActorProofV1`: exact single audience, protocol scope
  `diten.s2s.delegated.invoke`, server-resolved `tenant_id`, effective `delegated_actor_id`, exact
  `operation_id`, required permission, and `request_hash` over canonical method + path + tenant + operation +
  body digest. `sub`/`client_id`/`azp`, `jti`, nonce, delegation id and freshness versions remain required by
  FU16. A service credential without valid delegated proof cannot exercise user-visible or mutation authority.

The owner manifest contains exactly the existing seven permission keys; no eighth key, wildcard, alias or
Metric/Scale authoring permission is admitted. Exact operation IDs map as follows:

| Exact `operation_id` | Exact required permission |
|---|---|
| `outcome-tracking.outcomes.read` | `decision-intelligence.outcomes.read` |
| `outcome-tracking.outcomes.create` | `decision-intelligence.outcomes.create` |
| `outcome-tracking.outcomes.publish-version` | `decision-intelligence.outcomes.version` |
| `outcome-tracking.outcomes.retire` | `decision-intelligence.outcomes.version` |
| `outcome-tracking.measurements.append` | `decision-intelligence.measurements.append` |
| `outcome-tracking.measurements.correct` | `decision-intelligence.measurements.correct` |
| `outcome-tracking.decision-links.create` | `decision-intelligence.decision-links.manage` |
| `outcome-tracking.decision-links.retire` | `decision-intelligence.decision-links.manage` |
| `outcome-tracking.outcome-references.validate` | `decision-intelligence.outcome-references.validate` |

Operation matching is ordinal and exact. `outcomes.version` deliberately governs both immutable version
publication and retirement without adding a permission. `decision-links.manage` deliberately governs create
and retire provenance-link operations. Catalog registration grants no tenant access; tenant role grants remain
explicit under FU16, and entitlement removal makes them dormant rather than silently remapping them.

Metric/Scale authoring remains outside the initial slice. No authoring operation ID, permission, command,
handler, API, UI, seed editor or generic manifest entry exists. The separately approved immutable bootstrap
fixture remains the only possible initial activation source and its absence continues to block quantitative or
qualitative production activation.

#### Exact Minimal Mutation Audit v1 producer contract

- Event CLR/type identity: exact `OutcomeTrackingAuditIntentSubmittedV1`.
- EventName/routing identity: exact `decision-intelligence.outcome-tracking-audit-intent.submitted.v1`.
- EventVersion: exact JSON numeric integer `1`; it matches the `.v1` EventName suffix. Non-integer numeric,
  string and semantic-version representations are rejected.
- Producer: exact `Diten.DecisionIntelligenceService`; `ModuleCode`: exact `MOD-0072`.
- MOD-0035 signing identity: exact `diten.decision-intelligence.outcome-tracking-audit-intent` using exact
  scheme `decision-intelligence-event-hmac-sha256.v1`; it cannot reuse PPM or another producer's identity,
  key id, secret, namespace or derived material.

The canonical payload contains exactly the six MOD-0021 Minimal Mutation Audit v1 fields and no others:

| Exact field | MOD-0072 rule |
|---|---|
| `auditIntentId` | Non-empty immutable id; equals transport envelope `EventId` |
| `actorId` | Effective actor from trusted mutation context; equals the delegated actor for a delegated call |
| `entityType` | Exact allowlisted value below |
| `entityId` | Non-empty mutated MOD-0072 aggregate/entity id |
| `mutation` | Exact allowlisted value below and exact mapping target |
| `occurredAtUtc` | Server UTC mutation time; equals envelope `OccurredAtUtc` |

`TenantId`, `CorrelationId`, `CausationId`, `EventId`, `Producer` and delegated/security provenance are trusted
envelope metadata, never payload fields. Unknown, missing, extra, case-changed or normalized payload fields;
full entities; before/after snapshots; actual values; periods; evidence; permission inventories; secrets; and
unbounded dictionaries/graphs are rejected. The event-specific initial allowlist is closed:

| Exact `entityType` | Exact producer `mutation` | Exact MOD-0021 AuditEvent projection |
|---|---|---|
| `Outcome` | `Created` | `OutcomeTracking.Outcome` / `AuditOperation.Create` |
| `Outcome` | `Retired` | `OutcomeTracking.Outcome` / `AuditOperation.Deactivate` |
| `OutcomeVersion` | `Published` | `OutcomeTracking.OutcomeVersion` / `AuditOperation.Activate` |
| `OutcomeMeasurement` | `Appended` | `OutcomeTracking.OutcomeMeasurement` / `AuditOperation.Create` |
| `OutcomeMeasurement` | `Corrected` | `OutcomeTracking.OutcomeMeasurement` / `AuditOperation.Update` |

No other pair is accepted. Read/reference validation emits no mutation audit event. DecisionOutcomeLink audit
publication is outside this initial event allowlist and remains blocked until a separately approved event-version
amendment; its local mutation/audit-intent atomicity requirement is unchanged. Metric/Scale types and mutations
are absent from this allowlist and cannot enter through a generic mapper.

#### Atomicity, fixture and signed onboarding

For each allowlisted mutation, the OutcomeTracking-local Mongo transaction atomically commits business state,
the permanent idempotency receipt, a producer-local `diten_di_audit_intents` record and the corresponding
producer-local `diten_di_outbox_messages` record. Failure of any participant rolls back all participants. No
MOD-0021/Platform write, broker call or external call occurs in that transaction. Only after successful commit
may a service-local publisher invoke the public MOD-0035 `IEventBus`; direct RabbitMQ, MassTransit, broker or
Platform audit storage access remains forbidden.

The producer-owned compatibility fixture records exact canonical UTF-8 payload bytes, the immutable envelope,
the six-field property allowlist, expected MOD-0021 projection, EventName/EventVersion/Producer, signing scheme
and identity, key id, signing input and expected signature. MOD-0035 authorization is the exact ordinal tuple
`Producer + EventName + EventVersion + SigningIdentity`; wildcard/prefix/normalization is forbidden. Signing
binds scheme, EventId, EventName, EventVersion, TenantId, CorrelationId, Producer, CausationId, OccurredAtUtc,
payload byte length and the exact canonical bytes. The fixture must also prove active/previous rotation,
bounded overlap, expiry/revocation, fixed-time verification, freshness/skew, header rejection, replay/conflict,
retry/DLQ/cancellation and redaction behavior without changing the PPM regression fixture.

The authoritative governance fixture root is
`fixtures/MOD-0072/audit/` relative to this pack. Its exact baseline and companion evidence set is:

| Fixture | Exact purpose |
|---|---|
| `outcome-tracking-audit-intent-submitted-v1.canonical.json` | Exact minified UTF-8 canonical payload; 246 bytes, no BOM and no trailing newline |
| `outcome-tracking-audit-intent-submitted-v1.expected-audit-event.json` | Exact MOD-0021 projection with `sourceService` and `sourceModule = MOD-0072` |
| `outcome-tracking-audit-intent-submitted-v1.schema.json` | Exact `$id`, five identity constants, six-field schema and five closed entity/mutation pairs |
| `outcome-tracking-audit-intent-submitted-v1.canonical.sha256` | Exact 64 lowercase hex + one LF checksum record; no filename decoration |
| `outcome-tracking-audit-intent-submitted-v1.signing-vector.json` | DI-specific test-only envelope/key/signing-input vector |
| `outcome-tracking-audit-intent-submitted-v1.expected-signature.json` | DI-specific test-only expected HMAC-SHA256 signature |
| `outcome-tracking-audit-intent-submitted-v1.compatibility-matrix.json` | Schema/checksum/envelope/projection pair binding, five positives, ten invalid cross-pairs and complete contract negatives |
| `outcome-tracking-audit-intent-submitted-v1.delegated-provenance.positive.json` | Verified delegated actor/request binding and expected immutable AuditEvent provenance |
| `outcome-tracking-audit-intent-submitted-v1.delegated-provenance.negative.json` | Missing/broken/mismatched delegated provenance terminal cases |
| `outcome-tracking-audit-intent-submitted-v1.nondelegated-provenance.positive.json` | Non-delegated omission rule with authenticated `ExecutingService` retained |

The canonical payload SHA-256 is exactly
`6e1e750ffddc6f65d45556e703b0aa282b8469dec00bcc516a7a0a5f823cc2a3`. JSON property order is the literal
file order and canonicalization never parses/re-serializes the bytes. The checksum record is exactly 65 bytes:
the digest plus one LF, with no sibling filename. EventVersion is a JSON numeric integer `1` in every
envelope/vector/projection and rejects decimal-form or string-form alternatives. The schema identity is the
exact tuple `Diten.DecisionIntelligenceService + MOD-0072 +
decision-intelligence.outcome-tracking-audit-intent.submitted.v1 + 1 + MinimalMutationAuditV1`. The expected
MOD-0021 fixture maps `AuditCategory.Integration`, exact `sourceService`, exact `sourceModule = MOD-0072` and
the exact entity/operation projection declared above. The test-only key namespace, signing identity and
expected signature are Decision Intelligence-specific; PPM, FP&A or any other producer key/id/secret/
derived-material reuse is forbidden. Test-only vector material is never provisioned to runtime.

The compatibility matrix binds the exact case-sensitive canonical, checksum, schema and expected-projection
sibling basenames in one verification case. Digest format/value is verified before deserialization; schema
`$id` and all five constants must match the trusted envelope before the five exact allowlist pairs are
evaluated. Any missing, renamed, foreign or mismatched pair member is terminal and creates no AuditEvent.
The matrix covers all five positives, all ten invalid Cartesian cross-pairs and negative checksum, schema,
SourceModule, ModuleCode/event/version/type, canonical-byte, tenant/actor/operation and unknown-property cases.

Delegated provenance fixtures bind payload `actorId` to the verified effective actor and project exact
`DelegatedActorId`, `DelegatingPrincipalId`, `DelegationId`, `DelegatedOperationId`, `DelegatedPermission`,
`DelegationVerifiedAtUtc` and `ExecutingService` metadata without tokens, credentials, nonce, request body or
unrestricted claims. Missing/broken/unverified provenance or tenant/actor/operation/permission/request binding
mismatch is terminal with no AuditEvent and no retry. Non-delegated evidence omits every delegated/delegating
field rather than writing null identities while retaining authenticated `ExecutingService`.

MOD-0021 registers an explicit mapping for this exact producer/event version and fixture; event-name heuristics
and generic catch-all mapping are forbidden. Its consumer transaction atomically commits inbox plus zero-or-one
MOD-0021 audit outbox item. Idempotency is `ConsumerName + EventId`: byte-identical replay is a duplicate no-op;
the same EventId with changed canonical bytes is terminal conflict. Contract/security/unsupported failures are
terminal; only transient dependency failures retry and may reach DLQ/alarm under MOD-0035. Producer mutation
commit never depends synchronously on MOD-0021, Platform or broker availability.

#### Bilateral PPM fixture and executability boundary

The existing bilateral fixture remains unchanged: exact PPM wrapper
`ppm.benefit-commitment-outcome-reference` `1.0`, exactly four wrapper fields and the exact nested five-field
`diten.decision-intelligence.outcome-reference` `1.0`. It remains a separate synchronous reference contract,
not part of `OutcomeTrackingAuditIntentSubmittedV1`, and it copies no actual value, measurement, period,
evidence or realization state. MOD-0117 alone owns wrapper persistence and relationship lifecycle.

The PPM consumer adapter remains non-executable until all are separately satisfied: MOD-0072 producer runtime
evidence, approved bilateral producer/consumer fixtures, approved MOD-0117 consumer amendment, exact FU16
onboarding/provisioning evidence, MOD-0021 mapping fixture, MOD-0035 signing/onboarding fixture and explicit
runtime authority. This reconciliation satisfies none of those execution gates by documentation alone.

## 16. Acceptance Criteria

1. MOD-0072 remains a draft parent pack with `production_authority: none` and no runtime authority.
2. Blueprint route and ESBP packaging distinction are explicit.
3. Owned objects and all stated non-ownership boundaries are enforceable by negative architecture tests.
4. Planned BenefitCommitment and actual Outcome truth remain separate; PPM copies no actual fields.
5. MOD-0117 exclusively owns BenefitCommitment→OutcomeReference `0..n`, stored reference values and consumer relationship mutations; MOD-0072 has no BenefitRealizationLink, benefit-link command/permission or dual persistence.
6. Outcome lifecycle is exactly `Active → Retired`; retirement cannot reopen, is not delete and preserves HistoricalResolve.
7. Semantic change creates an immutable monotonically numbered OutcomeVersion; inherited `Version` is only optimistic concurrency.
8. OutcomeMetric/OutcomeMetricVersion and QualitativeScale/QualitativeScaleVersion are MOD-0072-owned and expose the exact five-field references defined above.
9. Quantitative/qualitative references are mutually exclusive; metric unit kinds, Decimal128 scale/bounds/currency/freshness rules are exact.
10. Scale versions contain 2..20 exact ordered levels; measurements store only LevelKey and old results are never reinterpreted.
11. LocalDate half-open period, 366-day duration, +1-day future, 10-year backfill, adjacency and no-overlap rules are exact.
12. Correction is an append-only measurement with exact current head/period and closed reason; no cycle, branch, time limit, update or delete exists.
13. V1 evidence is formally deferred: EvidenceReferences is empty, no evidence API/UI/provider call exists, and evidence-required tenant policy blocks append with `409`.
14. Observation policy `decision-intelligence.observation-status-policy` `1.0` deterministically derives only `NoObservation`, `Current`, `Stale`; no achievement/realized result or manual mutation exists.
15. Gate I-C OutcomeReferenceV1 remains exact five fields; OutcomeMeasurementReference is explicitly deferred with no identity minted.
16. PPM wrapper remains exact four fields with the nested producer tuple unchanged and no actual/measurement/period/evidence/projection copy.
17. HistoricalResolve resolves retired/superseded history; NewReferenceEligibility returns `409`; CurrentSelectionEligibility returns `400`.
18. V1 exposes no delete/soft-delete/hard-delete/bulk-delete/TTL/purge; retirement may occur with external references and existing PPM references remain unchanged.
19. Tenant/actor/delegation/S2S rules, exact permission keys, atomic audit intent and idempotency fail closed.
20. Future tenant pages explicitly use `Layout = "_LayoutTenantShell"` and Golden Slim with parent maximum field count 7.
21. No PPM actual-value form, evidence surface, editable observation/realization status or approval action exists.
22. Existing ES prototype fields remain quarantined pending separately authorized migration.
23. Bilateral supported-version/error fixtures enforce exact names/versions/allowlists and `400/401/403/404/409/503` without fallback.
24. Frontmatter service is exactly `Diten.DecisionIntelligenceService`; OutcomeTracking has no placement in FP&A fallback/primary or EnterpriseStrategy.
25. OutcomeTracking uses only the five reserved `Modules/OutcomeTracking/**` layer scopes and future service-local test scopes; no scaffold authority exists.
26. Repository/collection/index ownership and Mongo sessions/transactions are OutcomeTracking-local with no FP&A or EnterpriseStrategy access/sharing.
27. Inter-service communication is typed/versioned-contract-only; no foreign entity/repository/persistence DTO/session leakage, dual-write or second SoR exists.
28. OutcomeReference provider is OutcomeTracking-owned internal S2S with no browser/Gateway route.
29. MOD-0117 adapter remains non-executable until producer runtime evidence, bilateral fixtures, consumer amendment approval and explicit runtime authority all exist.
30. Port remains `TBD`; no scaffold, controller, route, Gateway, deployment or runtime literal is created.
31. Exact five-layer composition/test project paths, service-local EntityBase, eleven collection names, single owner-manifest path and tenant-first named index matrix are closed without creating files.
32. Each mutation atomically commits business state, permanent idempotency receipt, audit intent and required outbox; partial commit is forbidden.
33. Same-key/same-payload replays stored result, changed payload returns `409`; unknown commit uses commit-only retry then receipt reconciliation, unresolved ambiguity returns `503`.
34. ObservationStatus/referenceability and rebuild checkpoints are non-persisted; owner manifest forbids projection/checkpoint collections.
35. Broker delivery is post-commit through future MOD-0035 public `IEventBus` only; direct RabbitMQ/MassTransit/Platform audit persistence access is forbidden.
36. Initial migration is NO-OP/quarantine-only with no legacy dual-read/write or mirror; Metric/Scale activation requires a separately approved immutable bootstrap fixture and adds no permission/authoring surface.
37. FU16 onboarding is exact: audience `diten-decision-intelligence-service`, client ID `diten.decision-intelligence`, owner `MOD-0072`, nine exact operation IDs and only the unchanged seven permission keys.
38. Audit publication uses only `OutcomeTrackingAuditIntentSubmittedV1` / `decision-intelligence.outcome-tracking-audit-intent.submitted.v1` / numeric integer EventVersion `1` with the exact six-field Minimal Mutation Audit v1 payload and closed Outcome/version/measurement allowlist.
39. Producer-local mutation/receipt/audit-intent/outbox atomicity, post-commit public `IEventBus`, exact MOD-0021 mapping fixture and exact MOD-0035 signed tuple are mandatory; no synchronous Platform dependency or direct broker/audit storage access exists.
40. The PPM wrapper fixture remains unchanged and non-executable until every named bilateral, FU16, MOD-0021, MOD-0035, consumer-amendment, runtime-evidence and explicit-authority gate passes.
41. The ten-file governance fixture set is exact under `fixtures/MOD-0072/audit/`; canonical payload length is 246 bytes and SHA-256 is `6e1e750ffddc6f65d45556e703b0aa282b8469dec00bcc516a7a0a5f823cc2a3`.
42. All audit envelope/projection/vector EventVersion values are JSON numeric integer `1`; DI test-only signing material is isolated from PPM/FP&A and has no runtime authority.
43. Checksum/schema/envelope/projection pair binding, exact `SourceModule`, all five positive and ten invalid cross-pairs, complete contract negatives and delegated/non-delegated provenance fixtures fail closed as specified.

## 17. Test Expectations

- Architecture: planned-versus-actual ownership and forbidden PPM copy DTO/entity/UI fields.
- Lifecycle/version: Active→Retired only, no reopen/delete, immutable history, server-monotonic business numbers distinct from concurrency Version, retired HistoricalResolve and new-reference `409`.
- Metric references: exact contract names/versions/field sets, tenant visibility, immutable metric/scale versions and mutual quantitative/qualitative exclusion.
- Quantitative: every UnitKind, Decimal128/decimal precision 28 scale 4, >4-scale `400`, no rounding/float/NaN/infinity, absolute/unit-specific bounds, currency-only reference, inherited unit/currency and FreshnessDays 1..366.
- Qualitative: 2/20 boundary levels, NFC/key/ordinal/label validation, case-sensitive key uniqueness, exact LevelKey measurement, no narrative authority and no reinterpretation after scale versioning.
- Period: LocalDate `[start,end)`, start/end, 366-day, +1-day future, 10-year backfill, adjacent success, effective overlap `409`, immutable no-timezone semantics.
- Correction: normal-null supersedes, exact current head/outcome/version/period, each closed reason, immutable original, no cycle/branch, stale branch `409`, no time limit and legal-hold-compatible append.
- Evidence: exact empty list succeeds with zero provider call; any supplied/mock/local ID rejected; evidence-required tenant policy returns `409`; no API/UI; future enablement requires MOD-0031 amendment.
- Observation projection: exact policy/version, NoObservation/Current/Stale boundary at `asOfLocalDate - FreshnessDays`, effective-head-only, read/rebuild reproducibility, no persistence/manual status/PPM target comparison.
- Persistence/integration: real Mongo tenant isolation, cross-tenant `404`, exact indexes and concurrency; architecture scan proves zero delete/soft-delete/bulk-delete/TTL/purge operation.
- Transaction: audit failure rolls back business mutation and outbox intent.
- Idempotency: identical measurement append returns original; changed payload under same key returns `409`.
- Contract: exact five-field OutcomeReferenceV1; PPM `0..n`; forbidden payload fields rejected; OutcomeMeasurementReference name/type/storage absent and explicitly deferred; MOD-0007 outage isolated from base validation.
- Bilateral PPM fixtures: exact four-field `BenefitCommitmentOutcomeReferenceV1` wrapper round-trip with exact nested five-field producer tuple; extra/missing/case-changed fields rejected; no-copy scan excludes actual value, measurement, period, evidence and realization state.
- Bilateral modes: retired/superseded tuple succeeds under `HistoricalResolve`; the same visible tuple returns `409` under `NewReferenceEligibility`; `CurrentSelectionEligibility` returns `400`; historical resolution never grants new-link eligibility.
- Bilateral errors/versions: malformed/mode `400`, auth context `401`, permission/S2S entitlement `403`, missing/cross-tenant/invisible/non-disclosable `404`, visible new-link ineligibility `409`, and unknown wrapper/nested version, timeout, unavailable/malformed provider or indeterminate dependency `503`; exact `1.0` only with no alias/downgrade/cache/fallback.
- Architecture/ownership: no MOD-0072 BenefitRealizationLink entity/collection/repository/command/permission; optional backlink projection is non-authoritative and outside baseline; PPM remains the sole consumer-relationship owner.
- `ESBP-TOP-007`: dependency/DI/owner-manifest/transaction scans prove OutcomeTracking exists only in DecisionIntelligence and rejects FP&A placement or persistence sharing.
- `ESBP-TOP-008`: project/namespace/DI/collection/deployment scans prove zero MOD-0072 authority or ownership in `Diten.EnterpriseStrategyService`.
- `ESBP-TOP-009`: server tenant/actor, delegated actor, exact permission/S2S and cross-tenant/invisible failure tests pass.
- `ESBP-TOP-010`: Gateway/browser scans prove the internal OutcomeReference provider has no public route and browser-origin calls fail.
- `ESBP-TOP-011`: replica-set fault injection proves module-local mutation+audit/outbox rollback, post-commit delivery and idempotent duplicate consumption without foreign collection/session participation.
- `ESBP-TOP-012`: MOD-0117 serialization/adapter tests prove no-copy ownership and non-executability before producer evidence/bilateral/consumer/runtime gates.
- `ESBP-TOP-013`: configuration/deployment scans reject guessed port, unresolved service discovery, credential or unapproved route/runtime authority.
- Isolation matrix: Domain/Application/Persistence/Infrastructure source references, DI graph, repository/context injection, collection/index owner manifest and Mongo session instrumentation reject any FPA/Budgeting/ScenarioPlanning/EnterpriseStrategy sharing; communication fixtures admit typed/versioned contracts only.
- Composition paths: exact five layer module entry/DI/base paths and six concrete test-project roots match the pack allowlist; any second composition root or foreign base reference fails.
- Owner manifest/collision: exactly one manifest owns the eleven exact collection names; repo/config scan rejects duplicate collection/index literals, owner collisions, foreign access and any missing/extra/renamed index.
- Tenant-first indexes: ordered-key assertions cover every named index; unique/partial constraints, correction branch prevention and zero TTL on receipts are exercised against real Mongo.
- Transaction matrix: success commits mutation+receipt+audit intent+required outbox; injected failure at each participant rolls back all; no broker/external call occurs before commit.
- Idempotency matrix: same scoped key/hash returns stored result with one mutation/audit/outbox; changed hash returns `409` with no write; receipt survives retention horizon because v1 has no TTL.
- Unknown-commit matrix: commit-only retry never re-enters handler; matching receipt returns stored success, conflicting receipt `409`, confirmed abort follows safe failure, indeterminate reconciliation `503`, and caller replay with same key converges.
- Projection matrix: read/rebuild produces identical ObservationStatus/referenceability without collection/repository/checkpoint/DI writes; prohibited projection names are absent from owner manifest and Mongo.
- Publication matrix: publisher starts only after commit and resolves only public `IEventBus`; architecture/source tests reject RabbitMQ/MassTransit/direct broker and Platform audit collection clients.
- Bootstrap/migration matrix: initial permission set remains exact seven, zero Metric/Scale authoring command/UI, missing fixture blocks quantitative/qualitative activation, approved fixture is immutable/versioned, and first migration creates no target/dual-read/dual-write record.
- Retention: retirement with existing PPM references succeeds, existing references remain historically resolvable and unchanged, new eligibility `409`, provider outage does not block local history, no exceptional purge.
- Security: exact permission, entitlement, delegated actor provenance, S2S audience/scope and no fail-open.
- FU16 manifest: exact audience/client/owner, all nine ordinal operation mappings, only seven distinct permissions,
  request-hash/tenant/actor binding, replay/freshness failures and service-token-without-delegation rejection.
- Audit fixture: exact event type/name/version/producer/signing identity, exact six property names and canonical
  bytes, exact five allowlisted entity/mutation pairs, and rejection of extra/case-changed/Metric/Scale/link pairs.
- MOD-0021 mapping: expected projection for every allowlisted pair; unknown pair/version/property, metadata
  mismatch and missing delegated provenance are terminal; inbox + audit outbox and duplicate/conflict matrices pass.
- MOD-0035 onboarding: exact allowlist tuple, unique non-PPM signing material, active/previous rotation,
  expiry/revocation, fixed-time signature, freshness/skew, headers, replay, retry/DLQ/cancellation and redaction pass.
- PPM bilateral regression: the existing exact four-field wrapper/nested five-field fixture is unchanged and the
  adapter remains impossible to activate without all explicitly named gates.
- Fixture byte contract: all JSON parses as strict UTF-8 without BOM/trailing newline, is minified in declared
  property order, canonical byte length/SHA match, checksum verification passes and any byte/order change fails.
- Signing vector: recomputed DI test-only HMAC equals the expected lowercase signature; EventVersion has integer
  JSON type/value `1`, and scans reject PPM/FP&A signing identity, key-id, secret or derived-material reuse.
- Compatibility matrix: one bound test resolves exact sibling basenames, verifies 65-byte checksum format/value
  before parse, exact schema `$id`/five constants, envelope identity and expected `SourceService`/`SourceModule`;
  five positives pass and ten cross-pairs plus every named contract negative create no AuditEvent.
- Delegated provenance: verified actor/service/request metadata produces the exact immutable provenance block;
  missing/broken/unverified or tenant/actor/operation/permission/request mismatch is terminal. Non-delegated
  evidence omits delegated fields and retains only authenticated `ExecutingService`.
- Migration: prototype quarantine inventory/checksum; no legacy ES record is silently promoted or assigned a
  default tenant; explicit mapping/rejection/quarantine evidence for any future migration.
- UI: Slim file contract, parent maximum 7, DataTable v2 verifier, seven-language resources, explicit tenant layout, read-only history, no evidence/PPM actual editor/approval action.
- Quality: targeted builds/tests after topology approval, link check and `git diff --check`.

## 18. Ready-for-dev Checklist

- [x] MOD-0072 exact Blueprint/registry preflight passed during authoring.
- [x] Draft ownership, planned/actual boundary and Gate I-C v1 proposal documented.
- [x] Golden Reference Slim selected from parent maximum 7-field form surface.
- [x] Layout, backend/frontend conventions, validation, failure paths, authorization and routing documented.
- [x] A-class Outcome lifecycle/version, metric/scale authority, quantitative bounds, qualitative levels, period/overlap, correction, evidence deferral, observation status and retention semantics are closed in this draft.
- [x] `diten.decision-intelligence.outcome-reference` `1.0` and bilateral governance fixtures are semantically closed by Control Tower; execution remains separately unauthorized.
- [x] PPM authority checkpoint `a2c7ff5bbfd3ef2b4e3c8c15eab804db58d62520` was reviewed read-only; bilateral wrapper/mode/error profiles are reconciled in this draft without runtime authority.
- [x] Bilateral exact serialization, mode, error and supported-version fixtures are documented; no runtime executability is inferred.
- [x] OutcomeMeasurementReference is explicitly deferred from v1 and no contract identity is minted.
- [x] Binding topology is reconciled: only `Diten.DecisionIntelligenceService/Modules/OutcomeTracking`; no FP&A/EnterpriseStrategy co-host or fallback/dual-write model.
- [x] Exact five-layer composition paths, concrete test-project roots, service-local EntityBase path, collection/index names and single owner manifest are closed.
- [x] Transaction/idempotency/permanent receipt/unknown-commit behavior, projection non-persistence, post-commit IEventBus boundary and NO-OP migration are closed.
- [x] Exact FU16 audience/client/owner, operation-to-permission manifest and delegated actor/request binding are documented; runtime provisioning/evidence remains separately blocked.
- [x] Exact MOD-0021 mapping/fixture and MOD-0035 signed-producer onboarding contracts are documented; runtime integration/evidence remains separately blocked.
- [x] Exact ten-file audit fixture set, canonical checksum, schema/pair binding, full allowlist/negative matrix,
      delegated provenance and DI-only test signing vector are recorded; runtime credentials remain blocked.
- [ ] Exact immutable Metric/Scale bootstrap fixture content is approved; without it quantitative/qualitative activation remains blocked.
- [x] Metric/Scale administration is deferred to a separate amendment and the initial slice adds no commands/UI/permissions.
- [x] First migration slice is NO-OP/quarantine-only; any later transformation requires separate authority.
- [ ] Deployment/runtime authority is approved.
- [ ] MOD-0117 adapter activation gates are satisfied.
- [ ] Explicit runtime authority is recorded.
- [ ] Pack status is changed by human review to `approved` or `ready-for-dev`.

## 19. Implementation Notes

### Service topology boundary

The required topology direction is a separate future `Diten.DecisionIntelligenceService`, preserving the
actual-outcome SoR outside the Enterprise Strategy prototype. Core repo/test paths, module-local base path,
collection/index manifest and transaction semantics are closed above. Port, S2S/PSS producer contracts,
bootstrap fixture content, deployment/runtime authority and MOD-0117 activation remain open. This draft does
not scaffold the service or assign a port.

### Prototype quarantine/migration

Existing Enterprise Strategy actual-value/realization fields are non-authoritative inputs to a future
reconciliation only. The first migration slice is NO-OP/quarantine-only and creates no target/compatibility
record. Any later transformation requires a separately approved expand/contract slice with tenant mapping,
semantic/version mapping, evidence provenance, checksum/count reconciliation, rollback and negative proof
that no legacy field remains a second SoR.

## 20. Follow-up Items / Open Decisions

Only the following B-class blockers remain:

1. Allocate the authoritative port.
2. Implement and prove FU16 service-principal, delegated-proof and explicit permission provisioning for the closed manifest; add no permission.
3. Implement and prove the closed MOD-0021 mapping/compatibility fixture and MOD-0035 producer-specific signing onboarding.
4. Approve exact immutable/versioned Metric/Scale bootstrap fixture content and reconciliation evidence; authoring remains outside the initial slice.
5. Approve deployment/runtime activation and operations ownership.
6. Satisfy producer runtime evidence, bilateral fixtures, MOD-0117 consumer amendment and explicit adapter activation authority.

### Change Log

| Date | Change | Authority |
|---|---|---|
| 2026-08-02 | Draft parent pack authored; planned/actual ownership, Gate I-C proposal and topology recommendation recorded. | User / Enterprise Strategy Control Tower |
| 2026-08-02 | Control Tower correction: split base OutcomeReference from optional measurement reference; removed duplicate correction entity; made realization status a versioned-policy read projection; made referenceability non-persisted; tightened retirement, link permissions and conditional evidence failure semantics. | User / Enterprise Strategy Control Tower |
| 2026-08-02 | Final draft ownership cleanup: renamed the business ordinal to OutcomeVersionNumber, reduced OutcomeReferenceV1 to exactly five fields, assigned BenefitCommitment consumer relationships solely to MOD-0117, separated decision provenance and made external-reference delete eligibility fail closed. | User / Enterprise Strategy Control Tower |
| 2026-08-02 | Reconciled MOD-0072 with PPM checkpoint `a2c7ff5b`: exact four-field Gate I-C wrapper, nested five-field OutcomeReferenceV1, HistoricalResolve/NewReferenceEligibility profiles, non-selection rejection, supported-version behavior and bilateral error fixtures. No runtime authority granted. | User / Enterprise Strategy Control Tower |
| 2026-08-03 | Closed A-class Outcome semantics: Active/Retired lifecycle, producer-owned metric/scale versions, quantitative bounds, qualitative levels, LocalDate periods, append-only correction, v1 evidence deferral, deterministic observation status, no delete/purge, deferred measurement reference and Golden Slim maximum 7. Only named B-class runtime blockers remain. | User / Enterprise Strategy Control Tower |
| 2026-08-03 | Reconciled binding ESBP topology: MOD-0072 only in future `Diten.DecisionIntelligenceService/Modules/OutcomeTracking`, isolated persistence/transactions, internal S2S provider without Gateway/browser route, no FP&A/EnterpriseStrategy placement or dual SoR. Port/runtime remain unauthorized and TBD. | User / Enterprise Strategy Control Tower |
| 2026-08-04 | Closed OutcomeTracking Core composition: exact layer/test paths, service-local base, eleven collections and tenant-first indexes, atomic mutation/receipt/audit/outbox transaction, permanent idempotency receipts, unknown-commit reconciliation, non-persisted projections, post-commit public IEventBus boundary and NO-OP migration. Kept only named port/PSS/bootstrap/deployment/MOD-0117 activation blockers. | User / Enterprise Strategy Control Tower |
| 2026-08-04 | Reconciled Gate I onboarding from FU16 `40e38905`, MOD-0021 `de81a135` and MOD-0035 `23f290ec`: exact service audience/client/owner and operation-permission manifest; exact OutcomeTracking audit event, six-field payload and mutation allowlist; atomic local intent/outbox, post-commit IEventBus, consumer/signing fixtures and unchanged non-executable PPM wrapper boundary. Status remains draft; production authority remains none. | User / Enterprise Strategy Control Tower |
| 2026-08-04 | Closed the MOD-0072 audit governance fixture: six exact fixture files, numeric integer EventVersion `1`, ModuleCode `MOD-0072`, 246-byte ordered/minified UTF-8 payload, SHA-256 `6e1e750f...cc2a3`, exact MOD-0021 projection and DI-only test signing vector/signature. No permission, Outcome semantic, status or production authority changed. | User / Enterprise Strategy Control Tower |
| 2026-08-04 | Completed MOD-0072 audit fixtures against MOD-0021 checkpoint `9b75c213`: exact 65-byte checksum record, schema identity/pair enforcement, `SourceModule`, five-positive/ten-cross-pair and contract-negative matrix, delegated/non-delegated provenance fixtures. Canonical 246 bytes/SHA/signature, seven permissions, Outcome semantics and PPM no-copy remain unchanged; no runtime authority. | User / Enterprise Strategy Control Tower |
