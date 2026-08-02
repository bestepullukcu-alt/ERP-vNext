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
golden_reference: compact
entity_base: EntityBase
started: 2026-08-02
target: 2026-08-02
form_field_count: 11
---

# MOD-0072 — Decision Logs & Outcome Tracking

## 1. Module Summary

MOD-0072 is the authoritative tenant-scoped system of record (SoR) for observed outcomes and benefit
realization. It records immutable outcome versions, append-only measurements, corrections/restatements and
evidence provenance; it produces typed OutcomeReferences consumed and relationship-owned by PPM, while
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
- Evidence/provenance references; never the evidence binary or payload.
- Realization-state calculation and authoritative `OutcomeReference` validation.

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
| `OutcomeMeasurement` | Append-only entity | Actual quantitative or qualitative observation for a period |
| `DecisionOutcomeLink` | Typed link | Optional MOD-0007 Decision/DecisionVersion → Outcome trace |
| `EvidenceProvenanceReference` | Value object | Opaque evidence identifier/version, provider and integrity metadata |
| `OutcomeReferenceabilityProjection` | Non-persisted contract/read response | Minimal authoritative validation response; never cached authorization truth or a second SoR |
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
| `RealizationStatusProjection` | read-only derived value | No persisted authority | Deterministically calculated from append-only measurements under an explicitly versioned realization policy; no mutation command |
| base/audit fields | governed | Yes | Logical tenant-owned `EntityBase` contract, soft-delete compatibility and optimistic concurrency conventions |

### OutcomeVersion

| Field | Type | Required | Invariant |
|---|---|---:|---|
| `Id`, `OutcomeId`, `OutcomeVersionNumber` | Guid, Guid, int | Yes | Unique `(TenantId, OutcomeId, OutcomeVersionNumber)`; business version number strictly increases |
| `Definition` | string | Yes | Trim + NFC; max 4000 |
| `MetricReference` | typed reference? | Conditional | Required for quantitative; authoritative provider decision is OPEN |
| `UnitReference` | typed reference? | Conditional | Required for quantitative unless metric fixes a canonical unit |
| `CurrencyReference` | typed reference? | Conditional | Required only for monetary metric; ISO/canonical provider is OPEN |
| `QualitativeScale` | closed definition? | Conditional | Required for qualitative; exact scale/source is OPEN |
| `EffectiveFrom` | UTC instant | Yes | Version applicability; immutable |
| `CreatedByActorId`, `CreatedAt` | trusted actor, UTC | Yes | Server-derived immutable provenance |

### OutcomeMeasurement

| Field | Type | Required | Invariant |
|---|---|---:|---|
| `Id`, `OutcomeId`, `OutcomeVersionId` | Guid | Yes | Same tenant; version must exist and be referenceable |
| `PeriodStart`, `PeriodEnd` | date/UTC boundary | Yes | Half-open `[start,end)` proposed; `start < end` |
| `QuantitativeValue` | decimal? | Conditional | Exactly one of quantitative value or qualitative result |
| `QualitativeResult` | string/scale key? | Conditional | Exactly one result mode; max 2000 when narrative |
| `MetricReference`, `UnitReference`, `CurrencyReference` | typed references | Conditional | Must match version semantics; no free-text unit/currency |
| `EvidenceReferences` | list | No | Opaque typed/versioned references plus provider and integrity provenance |
| `ObservedAt` | UTC instant | Yes | Observation time; not PPM target date |
| `RecordedByActorId`, `DelegatedActorId` | trusted actor ids | Yes/No | Server-derived; delegated provenance preserved separately |
| `IdempotencyKey` | string | Yes | Unique per tenant + operation + actor/client scope |
| `SupersedesMeasurementId`, `CorrectionReason` | Guid?, string? | Conditional | Both required for correction; original is never mutated |

Required indexes include active `(TenantId, Code)` uniqueness, unique
`(TenantId, OutcomeId, OutcomeVersionNumber)`, inherited technical concurrency `Version`, unique idempotency
scope, Outcome/period lookup and Decision reference lookup. Exact Mongo index definitions wait for topology
approval. `OutcomeVersionNumber` is the business revision ordinal; inherited `Version` remains exclusively
the technical optimistic-concurrency token and never appears as the business version field.

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
| Metric/unit/currency reference owner | Canonical measurement semantics | **BLOCKER / owner and contract unresolved** |
| Evidence provider | Opaque evidence referenceability/provenance | **BLOCKER / provider contract unresolved** |
| MOD-0018 | Tenant entitlement, actor and exact permission enforcement | Runtime evidence required |
| MOD-0021 / MOD-0035 | Atomic audit intent/outbox delivery mechanics | Runtime integration approval/evidence required |
| DCP-006 | Gate I-C orchestration and no-copy boundary | OD-04 remains OPEN/PARTIAL |

## 8. Runtime Constraints

- MongoDB, tenant isolation, server-derived tenant/actor, soft delete and four CQRS pipeline behaviors apply.
- `module-pack-standard.md` permits only `EntityBase`, `BaseEntity` or `GlobalEntity`, not `TBD`.
  Frontmatter therefore retains `entity_base: EntityBase` as the mandatory logical tenant-owned base contract.
  It does not invent or select a concrete service-local CLR base type; that implementation remains blocked
  until topology/service approval.
- Cross-tenant, missing or actor-invisible resources return indistinguishable `404`.
- Existing immutable versions and measurements are never updated in place. Stale version/correction attempts
  return `409`; correction creates a new append-only record with complete provenance.
- Unknown dependency, unavailable required validation provider, or unknown contract version fails
  closed with `503`; evidence-provider unavailability applies only when evidence is policy-required or a
  supplied evidence reference must be validated. An evidence-optional append without evidence does not call
  or depend on that provider. PPM has no fail-open or cached-truth fallback.
- Numeric values use bounded decimal precision/range fixed with the metric contract; NaN/infinity and silent
  rounding are forbidden. Period duration/future-date bounds are OPEN owner decisions.
- Business mutation plus producer-local audit intent/outbox write is atomic. Audit enqueue failure rolls back
  the business mutation.
- Every append/correction/DecisionOutcomeLink mutation requires an idempotency key. Same key + identical canonical payload
  is a no-op with the original result; same key + different payload returns `409`.
- S2S validation requires a trusted service identity, delegated end-user tenant/actor provenance when the
  consumer acts for a user, exact audience/scope, correlation/causation IDs and allowlisted contract version.
- Client payloads cannot assert `TenantId`, effective actor, delegated actor, authorization or referenceability.

### Measurement and version model decision

The parent pack fixes a hybrid model: `OutcomeVersion` is an immutable semantic baseline; measurements are
append-only observations against exactly one baseline; corrections/restatements supersede rather than mutate;
historical versions and measurements remain queryable. A correction/restatement is itself a new
`OutcomeMeasurement` with `SupersedesMeasurementId`, `CorrectionReason` and complete correction provenance;
there is no separate correction aggregate/entity. Quantitative and qualitative outcomes are both
supported but cannot mix result modes inside one Outcome version. Evidence is reference-only and carries
provider/version/integrity provenance. Realization status is a read-only MOD-0072 projection, calculated
deterministically from append-only measurements using an explicitly versioned realization policy, and never
inferred from PPM lifecycle or edited by PPM/MOD-0072 UI. If materialized later for query performance, it must
store policy version plus source OutcomeVersion/measurement provenance, support deterministic rebuild and
reconciliation, and expose no independent mutation command.

### Deletion, retirement and historical integrity

Repo-wide soft-delete fields remain available for compatibility, but ordinary deletion is narrowly governed.
A measured Outcome cannot be ordinary-deleted. A draft Outcome with no measurement and no local
DecisionOutcomeLink is not automatically delete-eligible: because BenefitCommitment relationships are
MOD-0117-owned, MOD-0072 cannot infer “externally unreferenced” from a local cache or backlink projection.
External-reference delete eligibility and the authoritative retirement policy therefore fail closed and block
`ready-for-dev` until an approved cross-service contract/policy exists. Until then ordinary delete is disabled.
When a future authoritative check exists, known external use returns `409`; unavailable, timed-out, malformed,
unknown-version or indeterminate validation returns `503`, with no delete/audit/idempotency residue. Once
ineligible, the Outcome is retired
or superseded while every version, measurement and provenance link remains historically queryable. Corrections
never delete or overwrite their original. Missing/cross-tenant/invisible remains `404`; an indeterminate or
invalid delete, retirement or supersession attempt returns `409` with no mutation.

MOD-0072 may later consume integration events to build a non-authoritative BenefitCommitment backlink
projection. Such a projection is not a SoR, authorization truth, delete-eligibility truth or part of this
runtime baseline; it cannot create dual persistence or authorize relationship mutation.

The canonical metric/unit/currency provider, qualitative scale semantics, half-open period approval, overlap
policy, allowed correction window, numeric bounds and exact realization rule remain blockers for
`ready-for-dev`; they are not silently invented by implementation.

## 9. Layout & Shell Contract

Proposed shell is `tenant`; every future Razor page must explicitly declare
`Layout = "_LayoutTenantShell"`. Proposed view area is `Views/DecisionIntelligence/OutcomeTracking/`.

Evaluated tenant surfaces:

| Surface | Purpose | Form fields | Decision |
|---|---|---:|---|
| Outcome register | Filter/list/create/version outcomes | 11 maximum authoritative fields | Compact |
| Measurements | Append/correct observed results and period/evidence | 10 | Compact |
| Realization details | Read-only version/measurement/link history | 0 | Details/read-only |
| Decision-outcome trace | Read-only trace plus separately permitted link management | up to 3 | Embedded/detail surface |

Parent-level `form_field_count: 11` therefore selects Golden Reference **Compact**. No PPM actual-value edit
form and no approval button/action may be produced.

## 10. Backend File Convention

Future implementation, if separately authorized, follows the Compact Golden Reference shape per object:
`Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/` and one
`{Object}Models.cs`. Commands/queries are sealed records; handlers are sealed classes named
`{Verb}{Object}Handler`; validators are `{Verb}{Object}Validator`. `Command`, `Query` or `Request` suffixes
on handler/validator names and generic `OutcomeTrackingContext` handlers are forbidden.

Because `service: TBD`, no concrete service path is authorized by this section.

## 11. Frontend File Contract

Future Compact surfaces use `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`,
`_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, marker class, localized JavaScript and seven-language
RESX resources. `data-dt-standard="v2"`, skeleton, bulk-action and explicit layout contracts apply.
`_CreateEditOffcanvas.cshtml` and `_DetailsQuickView.cshtml` are forbidden for Compact surfaces.

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
| metric/unit/currency refs | Conditional | typed/versioned and compatible | indexed as needed | authoritative provider validation |
| qualitative scale/result | Conditional | exactly one result mode | — | version compatibility |
| period | Yes | proposed `[start,end)`, start < end | overlap index/query | overlap policy OPEN |
| quantitative value | Conditional | bounded decimal, no silent rounding | persisted decimal | metric bounds |
| evidence refs | No | typed/versioned, provider/integrity metadata | — | authoritative referenceability |
| Decision ref | When trace required | typed/versioned; same tenant/visible | link index | MOD-0007 validation |
| correction | Conditional | target exists, reason required, no chain cycle | immutable append | latest/version policy |
| idempotency key | Mutation | non-empty, bounded, scoped | unique index | canonical payload hash |

## 13. Failure Path to Verify

- Duplicate Outcome code: `409`; no record/audit residue.
- Missing, cross-tenant or invisible Outcome/dependency: indistinguishable `404`.
- Unauthorized known same-tenant action: `403`; no mutation.
- Stale version or immutable overwrite/correction conflict: `409`; history unchanged.
- Invalid/overlapping period, incompatible unit or out-of-bounds measurement: `400`; no append.
- Metric provider unavailable/malformed, unknown contract version, or evidence provider unavailable while
  evidence is required/supplied: `503`; fail closed. Optional evidence omitted under an evidence-optional
  policy does not fail because the evidence provider is unavailable.
- Ordinary delete after any measurement/DecisionOutcomeLink, indeterminate external-reference eligibility, or
  invalid retirement/supersession: `409`; history unchanged.
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
cross-tenant, invisible or ineligible tuples return indistinguishable `404`. The response does **not** add
actual value, unit, evidence, realization state or relationship context. Success means the exact tuple is
same-tenant, actor-visible and valid under current Outcome lifecycle/version policy; it is not a promise that
the result will remain referenceable forever.
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

### Optional OutcomeMeasurementReference proposal — identity OPEN

A measurement-specific consumer may later require a separate `OutcomeMeasurementReference` containing
`outcomeId`, immutable `outcomeVersionId`/`outcomeVersionNumber`, `measurementId` and authoritative period identity
where applicable. This is an optional proposed contract only: its exact canonical contract name, version,
consumers and approval authority remain **OPEN**. This pack does not invent or approve a second canonical
contract identity, and MOD-0117 Gate I-C does not consume or store it.

## 16. Acceptance Criteria

1. MOD-0072 remains a draft parent pack with `production_authority: none` and no runtime authority.
2. Blueprint route and ESBP packaging distinction are explicit.
3. Owned objects and all stated non-ownership boundaries are enforceable by negative architecture tests.
4. Planned BenefitCommitment and actual Outcome truth remain separate; PPM copies no actual fields.
5. MOD-0117 exclusively owns BenefitCommitment→OutcomeReference `0..n`, stored reference values and consumer relationship mutations; MOD-0072 has no BenefitRealizationLink, benefit-link command/permission or dual persistence.
6. `OutcomeVersionNumber` is the business ordinal everywhere; inherited `Version` is only optimistic concurrency.
7. Outcome versions and measurements are immutable; corrections/restatements append provenance.
8. Correction/restatement is a new OutcomeMeasurement with supersession fields; no second correction entity exists.
9. Realization status is a deterministic, versioned-policy read projection with no independent mutation.
10. Quantitative/qualitative, period, metric/unit/currency, evidence, deletion/retirement and realization decisions are explicit or
   block `ready-for-dev`.
11. Gate I-C `OutcomeReferenceV1` contains exactly `contractName`, `contractVersion`, `outcomeId`, `outcomeVersionId`, `outcomeVersionNumber`; the optional measurement-reference identity remains OPEN.
12. BenefitCommitment context may be validation-request-only; decision provenance is a separate command/contract and absent MOD-0007 is irrelevant to ordinary OutcomeReference validation.
13. Tenant/actor/delegation/S2S rules and exact least-privilege permission keys fail closed.
14. Cross-tenant/missing/invisible returns `404`; immutable/delete conflicts `409`; required-dependency/unknown-version failures `503`.
15. Optional omitted evidence does not create an evidence-provider dependency.
16. Audit intent is atomic and measurement append/correction is idempotent.
17. OutcomeReferenceabilityProjection is non-persisted and never cached/replicated by PPM.
18. Future tenant pages explicitly use `Layout = "_LayoutTenantShell"` and Compact conventions.
19. No PPM actual-value edit form, editable realization status or approval button/action exists.
20. Existing ES actual/realization prototype fields stay quarantined pending approved migration.

## 17. Test Expectations

- Architecture: planned-versus-actual ownership and forbidden PPM copy DTO/entity/UI fields.
- Domain/unit: `OutcomeVersionNumber` monotonic business versioning independently from inherited concurrency `Version`; immutable versions; correction-as-measurement provenance; deterministic versioned-policy realization projection; no independent realization mutation.
- Validation: period boundaries/overlap, measurement bounds, quantitative-versus-qualitative exclusivity,
  metric/unit/currency compatibility and correction chain integrity.
- Persistence/integration: real Mongo tenant isolation, cross-tenant `404`, soft delete, indexes and concurrency.
- Transaction: audit failure rolls back business mutation and outbox intent.
- Idempotency: identical measurement append returns original; changed payload under same key returns `409`.
- Dependency: missing/invisible evidence reference; conditional provider invocation; optional evidence omitted while provider unavailable succeeds; required/supplied evidence with unavailable/malformed provider fails `503`; unknown contract version.
- Contract: exact five-field serialization for OutcomeReferenceV1; PPM `0..n` references before/after first measurement; BenefitCommitment/decision/measurement/period fields rejected from the persisted value; optional measurement-reference identity remains unapproved; request-only BenefitCommitment context creates no producer relationship; MOD-0007 outage does not affect validation without a decision provenance operation.
- Architecture/ownership: no MOD-0072 BenefitRealizationLink entity/collection/repository/command/permission; optional backlink projection is non-authoritative and outside baseline; PPM remains the sole consumer-relationship owner.
- Persistence: referenceability response is not stored; measured Outcome deletion conflicts; unknown external-reference eligibility fails closed; retirement preserves history.
- Security: exact permission, entitlement, delegated actor provenance, S2S audience/scope and no fail-open.
- Migration: prototype quarantine inventory/checksum; no legacy ES record is silently promoted or assigned a
  default tenant; explicit mapping/rejection/quarantine evidence for any future migration.
- UI: Compact file contract, DataTable v2 verifier, seven-language resources, explicit tenant layout, no
  actual-value PPM editor and no approval action.
- Quality: targeted builds/tests after topology approval, link check and `git diff --check`.

## 18. Ready-for-dev Checklist

- [x] MOD-0072 exact Blueprint/registry preflight passed during authoring.
- [x] Draft ownership, planned/actual boundary and Gate I-C v1 proposal documented.
- [x] Golden Reference Compact selected from 11-field maximum form surface.
- [x] Layout, backend/frontend conventions, validation, failure paths, authorization and routing documented.
- [ ] Process owner approves canonical metric/unit/currency and qualitative-scale sources.
- [ ] Process owner approves period/overlap semantics, measurement bounds and realization rule.
- [ ] Versioned realization policy, deterministic rebuild/reconciliation and non-editability are approved.
- [ ] Evidence provider contract, conditional validation rule and correction/restatement policy are approved.
- [ ] Draft-delete and retirement/supersession policy is approved.
- [ ] External-reference delete-eligibility contract/policy is approved; no local backlink/cache is treated as truth.
- [ ] `diten.decision-intelligence.outcome-reference` `1.0` is approved by MOD-0072 and MOD-0117 owners.
- [ ] Optional OutcomeMeasurementReference need and exact identity are approved or explicitly deferred.
- [ ] Service topology, concrete service-local base implementation, service port and deployment ownership are approved. Frontmatter `entity_base: EntityBase` remains the mandatory logical tenant-owned base contract required by the standard; it does not select a service-local class before topology approval.
- [ ] Exact collections/indexes, API routes, S2S credentials/audience and audit event are approved.
- [ ] Matching MOD-0117 Gate I-C consumer amendment is approved.
- [ ] Explicit user runtime authority is recorded.
- [ ] Pack status is changed by human review to `approved` or `ready-for-dev`.

## 19. Implementation Notes

### Service topology comparison

| Option | Assessment |
|---|---|
| A. New `Diten.DecisionIntelligenceService` | **Preferred.** Separates actual-outcome SoR, scaling, permissions and evidence/measurement evolution from strategy-planning prototype ownership. Requires separately approved scaffold, port, Gateway and operations. |
| B. Isolated OutcomeTracking module in `Diten.EnterpriseStrategyService` | Lower initial deployment cost, but creates a strong risk that prototype strategy `ActualValue` fields become accidental authority and couples planning with observed truth. Acceptable only if architecture tests and owner approval overcome this risk. |
| C. Another data/intelligence service | Potentially aligns with the logical route, but no approved candidate service/topology currently exists; selecting an arbitrary nearby service would invent authority. |

Recommendation: choose A in a later approved topology decision. This draft does not scaffold the service or
assign a port.

### Prototype quarantine/migration

Existing Enterprise Strategy actual-value/realization fields are non-authoritative inputs to a future
reconciliation only. Migration must be a separately approved expand/contract slice with tenant mapping,
semantic/version mapping, evidence provenance, checksum/count reconciliation, quarantine for ambiguous or
unknown-tenant records, rollback and negative proof that no legacy field remains a second SoR.

## 20. Follow-up Items / Open Decisions

1. Name the canonical metric/unit/currency and qualitative-scale provider(s) and their versioned contracts.
2. Approve half-open period semantics, overlap rules, future/backfill bounds and numeric precision/range.
3. Approve the versioned realization policy, deterministic rebuild/reconciliation, source provenance and restatement effects; manual mutation is forbidden.
4. Define evidence provider referenceability, integrity metadata and unavailable-provider behavior.
5. Approve correction window/reason taxonomy and whether a corrected measurement remains referenceable.
6. Approve base contract `diten.decision-intelligence.outcome-reference` `1.0` jointly with MOD-0117 and define
   consumer revalidation/revocation behavior.
7. Decide whether any consumer needs OutcomeMeasurementReference; if so, approve its exact identity separately.
8. Approve draft-delete eligibility and retirement/supersession state machine.
9. Approve the fail-closed external-reference delete-eligibility contract/policy; do not use a backlink projection as authority.
10. Approve topology A/B/C, concrete service-local base implementation, service port, routes, collections and operations ownership.
11. Author and approve the matching MOD-0117 Gate I-C consumer amendment that owns stored references and relationship lifecycle.
12. Author the separately approved prototype quarantine/migration slice if migration is desired.
13. Record explicit runtime authority; draft pack approval alone does not authorize implementation.

### Change Log

| Date | Change | Authority |
|---|---|---|
| 2026-08-02 | Draft parent pack authored; planned/actual ownership, Gate I-C proposal and topology recommendation recorded. | User / Enterprise Strategy Control Tower |
| 2026-08-02 | Control Tower correction: split base OutcomeReference from optional measurement reference; removed duplicate correction entity; made realization status a versioned-policy read projection; made referenceability non-persisted; tightened retirement, link permissions and conditional evidence failure semantics. | User / Enterprise Strategy Control Tower |
| 2026-08-02 | Final draft ownership cleanup: renamed the business ordinal to OutcomeVersionNumber, reduced OutcomeReferenceV1 to exactly five fields, assigned BenefitCommitment consumer relationships solely to MOD-0117, separated decision provenance and made external-reference delete eligibility fail closed. | User / Enterprise Strategy Control Tower |
