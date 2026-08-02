---
id: MOD-0072
name: Decision Logs & Outcome Tracking
domain: enterprise-strategy-business-performance
status: draft
owner: Decision Intelligence & Benefit Realization Process Owner
branch: feature/esbp/mod-0072-decision-logs-outcome-tracking
service: TBD
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

This authoring job owns only:

- `execution/domains/enterprise-strategy-business-performance/module-packs/MOD-0072-decision-logs-outcome-tracking.md`

Any future implementation scope is **TBD** and requires this pack to become `approved` or `ready-for-dev`,
explicit runtime authority, service topology/port approval, and a matching MOD-0117 consumer amendment.

## 6. Protected Paths

- `.antigravity/**`
- `services/**`, `frontend/**`, `gateway/**` for this draft
- `gateway/Diten.ApiGateway/**/ocelot.json` except a separately authorized integration-agent task
- `execution/registries/**`, `execution/portfolio/**` and all other shared governance files
- Other domains' module packs and runtime ownership
- Archive controllers/views and frozen layouts defined by root `AGENTS.md`

## 7. Dependencies

| Dependency | Contract role | Draft state |
|---|---|---|
| MOD-0117 | Owns planned BenefitCommitment plus `BenefitCommitment → OutcomeReference` `0..n` relationship, stored values and consumer lifecycle | Consumer amendment and runtime authority required; no dual persistence in MOD-0072 |
| MOD-0007 | Optional Decision/DecisionVersion reference producer | Exact producer contract must be approved before runtime link |
| MOD-0023 | Approval only when separately required | No approval behavior in MOD-0072 |
| `fpa.currency-reference` `1.0` provider | Exact currency identity for `UnitKind.Currency` only | Bilateral contract/runtime evidence remains a B-class integration concern |
| MOD-0031 | Future typed/versioned evidence amendment | Formally deferred from v1 core; no runtime call or evidence surface |
| MOD-0018 | Tenant entitlement, actor and exact permission enforcement | Runtime evidence required |
| MOD-0021 / MOD-0035 | Atomic audit intent/outbox delivery mechanics | Runtime integration approval/evidence required |
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

Because `service: TBD`, no concrete service path is authorized by this section.

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
- PPM attempts to submit actual value/period/evidence/realized state: contract rejection; no copied truth.

## 14. Authorization Convention

Tenant endpoints require `[Authorize]`, entitlement and exact `[HasPermission]` checks. Tenant and actor are
resolved server-side. A delegated call preserves service identity, effective end-user actor and delegation
provenance as separate trusted values. Read visibility is re-evaluated by the producer; possession of a GUID
is not authorization.

The validation endpoint is S2S-only with `decision-intelligence.outcome-references.validate`, explicit
audience/scope, trusted tenant propagation and delegated actor context. A service credential alone may not
expand the user's visibility. Security failures never fall back to PPM-local cached actual data.

## 15. Gateway / API Routing Decision

Decision: **deferred / not authorized by this draft**. A service and port do not yet exist. If runtime is later
approved, browser traffic must use frontend `5001` → Gateway `5000`, never a direct service port. Exact
upstream/downstream routes, OPTIONS handling and health routing require a separate integration-agent task;
the protected `ocelot.json` is not modified by this pack.

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
- Retention: retirement with existing PPM references succeeds, existing references remain historically resolvable and unchanged, new eligibility `409`, provider outage does not block local history, no exceptional purge.
- Security: exact permission, entitlement, delegated actor provenance, S2S audience/scope and no fail-open.
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
- [ ] Separate `Diten.DecisionIntelligenceService` topology is approved.
- [ ] Exact repo paths, concrete service-local base, collections/indexes, routes and port are approved.
- [ ] Exact S2S/delegated-actor design and MOD-0018 provisioning are approved.
- [ ] MOD-0021/MOD-0035 audit/outbox integration is approved.
- [ ] Transaction, idempotency and unknown-commit behavior is approved.
- [ ] Metric/scale seed and administration runtime scope is approved.
- [ ] Prototype migration/quarantine scope is approved.
- [ ] Explicit runtime authority is recorded.
- [ ] Pack status is changed by human review to `approved` or `ready-for-dev`.

## 19. Implementation Notes

### Service topology boundary

The required topology direction is a separate future `Diten.DecisionIntelligenceService`, preserving the
actual-outcome SoR outside the Enterprise Strategy prototype. Exact repo paths, base implementation,
collections/indexes, routes, port, deployment and operations remain B-class blockers. This draft does not
scaffold the service or assign a port.

### Prototype quarantine/migration

Existing Enterprise Strategy actual-value/realization fields are non-authoritative inputs to a future
reconciliation only. Migration must be a separately approved expand/contract slice with tenant mapping,
semantic/version mapping, evidence provenance, checksum/count reconciliation, quarantine for ambiguous or
unknown-tenant records, rollback and negative proof that no legacy field remains a second SoR.

## 20. Follow-up Items / Open Decisions

Only the following B-class blockers remain:

1. Approve separate `Diten.DecisionIntelligenceService` topology.
2. Fix exact repo paths, concrete base, collections/indexes, routes and port.
3. Approve S2S/delegated-actor design and MOD-0018 provisioning.
4. Approve MOD-0021/MOD-0035 audit/outbox integration.
5. Approve transaction, idempotency and unknown-commit behavior.
6. Approve metric/scale seed and administration runtime scope.
7. Author the separately approved prototype migration/quarantine slice.
8. Record explicit runtime authority; draft status alone authorizes no implementation.

### Change Log

| Date | Change | Authority |
|---|---|---|
| 2026-08-02 | Draft parent pack authored; planned/actual ownership, Gate I-C proposal and topology recommendation recorded. | User / Enterprise Strategy Control Tower |
| 2026-08-02 | Control Tower correction: split base OutcomeReference from optional measurement reference; removed duplicate correction entity; made realization status a versioned-policy read projection; made referenceability non-persisted; tightened retirement, link permissions and conditional evidence failure semantics. | User / Enterprise Strategy Control Tower |
| 2026-08-02 | Final draft ownership cleanup: renamed the business ordinal to OutcomeVersionNumber, reduced OutcomeReferenceV1 to exactly five fields, assigned BenefitCommitment consumer relationships solely to MOD-0117, separated decision provenance and made external-reference delete eligibility fail closed. | User / Enterprise Strategy Control Tower |
| 2026-08-02 | Reconciled MOD-0072 with PPM checkpoint `a2c7ff5b`: exact four-field Gate I-C wrapper, nested five-field OutcomeReferenceV1, HistoricalResolve/NewReferenceEligibility profiles, non-selection rejection, supported-version behavior and bilateral error fixtures. No runtime authority granted. | User / Enterprise Strategy Control Tower |
| 2026-08-03 | Closed A-class Outcome semantics: Active/Retired lifecycle, producer-owned metric/scale versions, quantitative bounds, qualitative levels, LocalDate periods, append-only correction, v1 evidence deferral, deterministic observation status, no delete/purge, deferred measurement reference and Golden Slim maximum 7. Only named B-class runtime blockers remain. | User / Enterprise Strategy Control Tower |
