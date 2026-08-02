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
realization. It records immutable outcome versions, append-only measurements, corrections/restatements,
evidence provenance and typed links to planned benefit commitments and decisions.

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
- `BenefitRealizationLink` from an Outcome to a MOD-0117 `BenefitCommitment` reference.
- `DecisionOutcomeLink` from an Outcome to a MOD-0007 Decision reference when a decision trace exists.
- Evidence/provenance references; never the evidence binary or payload.
- Realization-state calculation and authoritative `OutcomeReference` validation.

### Explicitly not owned

- Planned `BenefitCommitment`, its target or lifecycle — MOD-0117.
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
| `OutcomeMeasurementCorrection` | Immutable provenance record | Supersedes a measurement without overwriting history |
| `BenefitRealizationLink` | Typed link | `BenefitCommitmentReference` → Outcome, cardinality many links per commitment |
| `DecisionOutcomeLink` | Typed link | Optional MOD-0007 Decision/DecisionVersion → Outcome trace |
| `EvidenceProvenanceReference` | Value object | Opaque evidence identifier/version, provider and integrity metadata |
| `OutcomeReferenceabilityProjection` | Read model | Minimal authoritative validation response for consumers |
| `diten.decision-intelligence.outcome-reference` | Contract | Exact Gate I-C typed contract, version `1.0` |

Proposed command/query families are object-specific: create/version Outcome; append/correct Measurement;
link/unlink BenefitCommitment or Decision; get register/details/history; and validate OutcomeReference.
Arbitrary generic entity handlers are forbidden.

### Exact permissions

- `decision-intelligence.outcomes.read`
- `decision-intelligence.outcomes.create`
- `decision-intelligence.outcomes.version`
- `decision-intelligence.measurements.append`
- `decision-intelligence.measurements.correct`
- `decision-intelligence.realization-links.manage`
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
| `RealizationStatus` | enum | Yes | `NotMeasured`, `InProgress`, `Realized`, `PartiallyRealized`, `NotRealized`, `Superseded`; MOD-0072-owned |
| base/audit fields | governed | Yes | `EntityBase`, soft-delete and optimistic concurrency conventions |

### OutcomeVersion

| Field | Type | Required | Invariant |
|---|---|---:|---|
| `Id`, `OutcomeId`, `Version` | Guid, Guid, int | Yes | Unique `(TenantId, OutcomeId, Version)`; version strictly increases |
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

Required indexes include active `(TenantId, Code)` uniqueness, unique Outcome version, unique idempotency
scope, Outcome/period lookup, BenefitCommitment reference lookup and Decision reference lookup. Exact Mongo
index definitions wait for topology approval.

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
| MOD-0117 | Owns planned BenefitCommitment; Gate I-C consumer | Consumer amendment and runtime authority required |
| MOD-0007 | Optional Decision/DecisionVersion reference producer | Exact producer contract must be approved before runtime link |
| MOD-0023 | Approval only when separately required | No approval behavior in MOD-0072 |
| Metric/unit/currency reference owner | Canonical measurement semantics | **BLOCKER / owner and contract unresolved** |
| Evidence provider | Opaque evidence referenceability/provenance | **BLOCKER / provider contract unresolved** |
| MOD-0018 | Tenant entitlement, actor and exact permission enforcement | Runtime evidence required |
| MOD-0021 / MOD-0035 | Atomic audit intent/outbox delivery mechanics | Runtime integration approval/evidence required |
| DCP-006 | Gate I-C orchestration and no-copy boundary | OD-04 remains OPEN/PARTIAL |

## 8. Runtime Constraints

- MongoDB, tenant isolation, server-derived tenant/actor, soft delete and four CQRS pipeline behaviors apply.
- Cross-tenant, missing or actor-invisible resources return indistinguishable `404`.
- Existing immutable versions and measurements are never updated in place. Stale version/correction attempts
  return `409`; correction creates a new append-only record with complete provenance.
- Unknown dependency, unavailable metric/evidence validation provider, or unknown contract version fails
  closed with `503`; PPM has no fail-open or cached-truth fallback.
- Numeric values use bounded decimal precision/range fixed with the metric contract; NaN/infinity and silent
  rounding are forbidden. Period duration/future-date bounds are OPEN owner decisions.
- Business mutation plus producer-local audit intent/outbox write is atomic. Audit enqueue failure rolls back
  the business mutation.
- Every append/correction/link mutation requires an idempotency key. Same key + identical canonical payload
  is a no-op with the original result; same key + different payload returns `409`.
- S2S validation requires a trusted service identity, delegated end-user tenant/actor provenance when the
  consumer acts for a user, exact audience/scope, correlation/causation IDs and allowlisted contract version.
- Client payloads cannot assert `TenantId`, effective actor, delegated actor, authorization or referenceability.

### Measurement and version model decision

The parent pack fixes a hybrid model: `OutcomeVersion` is an immutable semantic baseline; measurements are
append-only observations against exactly one baseline; corrections/restatements supersede rather than mutate;
historical versions and measurements remain queryable. Quantitative and qualitative outcomes are both
supported but cannot mix result modes inside one Outcome version. Evidence is reference-only and carries
provider/version/integrity provenance. Realization status is MOD-0072-owned, explicitly evaluated from
approved measurement policy, and never inferred from PPM lifecycle.

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

Realization details and decision trace default to read-only displays. Link mutation controls appear only with
the exact manage permission. There is no PPM-owned actual edit surface and no approval UI.

## 12. Validation Rules

| Field / operation | Required | Format/rule | DB-level | Pre-check |
|---|---:|---|---|---|
| `Code` | Yes | NFC, max 64 | active tenant unique | duplicate check |
| `Title` | Yes | NFC, max 200 | — | — |
| `OutcomeKind` | Yes | closed enum | — | immutable after measurement |
| definition | Yes | max 4000 | immutable version | — |
| metric/unit/currency refs | Conditional | typed/versioned and compatible | indexed as needed | authoritative provider validation |
| qualitative scale/result | Conditional | exactly one result mode | — | version compatibility |
| period | Yes | proposed `[start,end)`, start < end | overlap index/query | overlap policy OPEN |
| quantitative value | Conditional | bounded decimal, no silent rounding | persisted decimal | metric bounds |
| evidence refs | No | typed/versioned, provider/integrity metadata | — | authoritative referenceability |
| BenefitCommitment ref | For realization link | typed/versioned; same tenant/visible | link index | MOD-0117 validation |
| Decision ref | When trace required | typed/versioned; same tenant/visible | link index | MOD-0007 validation |
| correction | Conditional | target exists, reason required, no chain cycle | immutable append | latest/version policy |
| idempotency key | Mutation | non-empty, bounded, scoped | unique index | canonical payload hash |

## 13. Failure Path to Verify

- Duplicate Outcome code: `409`; no record/audit residue.
- Missing, cross-tenant or invisible Outcome/dependency: indistinguishable `404`.
- Unauthorized known same-tenant action: `403`; no mutation.
- Stale version or immutable overwrite/correction conflict: `409`; history unchanged.
- Invalid/overlapping period, incompatible unit or out-of-bounds measurement: `400`; no append.
- Evidence/metric provider unavailable or malformed, or unknown contract version: `503`; fail closed.
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

### Gate I-C typed/versioned contract v1

Proposed exact contract name: **`diten.decision-intelligence.outcome-reference`**, semantic version **`1.0`**.
Version 1 is a synchronous authoritative referenceability query/response, not a replicated outcome payload.

Minimum request/reference:

| Field | v1 semantic |
|---|---|
| `contractName`, `contractVersion` | Exact name and allowlisted `1.0`; unknown version → `503` |
| `outcomeId` | Stable MOD-0072 Outcome identity |
| `outcomeVersionId`, `outcomeVersion` | Exact immutable semantic baseline identity and ordinal |
| `measurementId`, `periodStart`, `periodEnd` | Exact authoritative observation/half-open period reference |
| `benefitCommitmentReference` | Typed MOD-0117 identity/version linked by `BenefitRealizationLink` |
| `decisionReference` | Optional typed MOD-0007 Decision/DecisionVersion when trace is required |

Minimum response adds `isReferenceable` and the canonical IDs/versions needed to prove the requested tuple.
It does **not** return actual value, unit, evidence or realized state to be persisted by PPM. `isReferenceable`
means the exact tuple exists, is same-tenant, actor-visible, not deleted/superseded under policy, mutually
linked and valid at validation time; it is not a promise that the result will remain referenceable forever.

Cardinality is `BenefitCommitment → Outcome/Realization = 0..n`. One Outcome contains `1..n` immutable
versions and `0..n` measurements; a measurement belongs to exactly one OutcomeVersion. PPM stores only the
opaque typed/versioned reference tuple required by its approved consumer amendment.

## 16. Acceptance Criteria

1. MOD-0072 remains a draft parent pack with `production_authority: none` and no runtime authority.
2. Blueprint route and ESBP packaging distinction are explicit.
3. Owned objects and all stated non-ownership boundaries are enforceable by negative architecture tests.
4. Planned BenefitCommitment and actual Outcome truth remain separate; PPM copies no actual fields.
5. Outcome versions and measurements are immutable; corrections/restatements append provenance.
6. Quantitative/qualitative, period, metric/unit/currency, evidence and realization decisions are explicit or
   block `ready-for-dev`.
7. Gate I-C uses exact `diten.decision-intelligence.outcome-reference` `1.0` semantics and cardinalities.
8. Tenant/actor/delegation/S2S rules and exact permission keys fail closed.
9. Cross-tenant/missing/invisible returns `404`; conflicts `409`; dependency/unknown-version failures `503`.
10. Audit intent is atomic and measurement append/correction is idempotent.
11. Future tenant pages explicitly use `Layout = "_LayoutTenantShell"` and Compact conventions.
12. No PPM actual-value edit form or approval button/action exists.
13. Existing ES actual/realization prototype fields stay quarantined pending approved migration.

## 17. Test Expectations

- Architecture: planned-versus-actual ownership and forbidden PPM copy DTO/entity/UI fields.
- Domain/unit: immutable versioning; correction/restatement provenance; realization-state ownership.
- Validation: period boundaries/overlap, measurement bounds, quantitative-versus-qualitative exclusivity,
  metric/unit/currency compatibility and correction chain integrity.
- Persistence/integration: real Mongo tenant isolation, cross-tenant `404`, soft delete, indexes and concurrency.
- Transaction: audit failure rolls back business mutation and outbox intent.
- Idempotency: identical measurement append returns original; changed payload under same key returns `409`.
- Dependency: missing/invisible evidence reference; unavailable/malformed provider; unknown contract version.
- Contract: PPM `0..n` links, authoritative referenceability and no-copy serialization/storage tests.
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
- [ ] Evidence provider contract and correction/restatement policy are approved.
- [ ] `diten.decision-intelligence.outcome-reference` `1.0` is approved by MOD-0072 and MOD-0117 owners.
- [ ] Service topology, concrete `entity_base`, service port and deployment ownership are approved.
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
3. Define realization status calculation, authorized manual override (if any) and restatement effects.
4. Define evidence provider referenceability, integrity metadata and unavailable-provider behavior.
5. Approve correction window/reason taxonomy and whether a corrected measurement remains referenceable.
6. Approve contract `diten.decision-intelligence.outcome-reference` `1.0` jointly with MOD-0117 and define
   consumer revalidation/revocation behavior.
7. Approve topology A/B/C, concrete entity base, service port, routes, collections and operations ownership.
8. Author and approve the matching MOD-0117 Gate I-C consumer amendment.
9. Author the separately approved prototype quarantine/migration slice if migration is desired.
10. Record explicit runtime authority; draft pack approval alone does not authorize implementation.
