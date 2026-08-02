---
id: MOD-0136
name: Budgeting
domain: enterprise-strategy-business-performance
service: TBD
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: draft
owner: FP&A Budgeting Process Owner
branch: feature/esbp/mod-0136-budgeting
started: 2026-08-02
target: 2026-08-02
form_field_count: 14
production_authority: none
---

# MOD-0136 — Budgeting

> **Draft-only authoring boundary:** This parent pack defines proposed ownership, contracts and promotion
> gates. It grants no runtime, frontend, service, collection, migration, port, Gateway or production authority.
> `service: TBD` is intentional until topology promotion. Repository packaging under ESBP does not imply
> placement in `Diten.EnterpriseStrategyService`.

## 1. Module Summary

MOD-0136 is the tenant-owned FP&A budgeting system of record for stable budgets, immutable budget versions,
period/line breakdown, currency and unit semantics, funding-baseline selection and authoritative version
referenceability. Its Blueprint route is `Enterprise Application Ecosystem → ERP Suite → FP&A / EPM`.

The primary users are FP&A budgeting practitioners and authorized tenant consumers. This parent pack proposes
the exact Gate I-B producer contract required by MOD-0117 while keeping all consumer implementation
non-executable until producer and consumer packs are separately approved and explicit runtime authority exists.

## 2. Ownership and Boundaries

### Owns

- `Budget` stable identity and its immutable `BudgetVersion` history.
- `BudgetPeriod`, `BudgetLine` or an equivalent necessary financial breakdown.
- Canonical currency reference, unit/scale semantics, precision and rounding policy captured by a version.
- Funding-baseline truth and the single selected/active budget-version truth for a Budget.
- Authoritative `BudgetVersionReference` validation, including tenant, existence, immutability,
  referenceability, selection/certification state and contract-version compatibility.

### Does not own

- Portfolio or `InvestmentCase` lifecycle; MOD-0117 owns them.
- Scenario, scenario selection, comparator run or output; MOD-0138 owns them.
- Workflow, approval, SLA or escalation behavior; MOD-0023 owns it.
- Actual benefit outcome or realization; MOD-0072 owns it.
- A PPM-local amount, currency, period, line or budget snapshot.
- Accounting ledger, journal posting, commitment accounting, treasury or payment execution.
- Local approve/reject fields, commands, routes or UI. `Approved`, `Selected` and `Certified` are not synonyms.

### Version and selection model

- `Budget` is the stable identity. A revision never replaces that identity.
- `BudgetVersion` is immutable after publication/certification; a revision creates a new version with a new
  `BudgetVersionId` and monotonically increasing `BudgetVersionNumber`.
- Draft construction may use a separate mutable staging model, but the certified/published artifact is an
  immutable snapshot. Promotion must decide whether staging is an entity or command-side document.
- `Selected` means the MOD-0136-owned funding baseline selected for downstream reference. `Certified` means
  the version passed the owner-defined financial completeness/certification rule. Neither means workflow
  approval. If approval is required, MOD-0023 supplies a typed `ApprovalOutcome` reference.
- Draft baseline: exactly zero or one non-deleted BudgetVersion is selected per Budget. Selection changes are
  atomic and auditable; historical versions are not mutated to rewrite their financial contents. Before
  promotion, the owner must confirm whether the authoritative selection scope is Budget, fiscal plan,
  organization/cost center or another explicit funding context.

## 3. Owned Objects

| Category | Proposed owned objects |
|---|---|
| Aggregates/entities | `Budget`, immutable `BudgetVersion`, `BudgetPeriod`, `BudgetLine`, funding-baseline selection record/projection |
| Value objects | `MoneyAmount`, `CurrencyReference`, `TimePeriodReference`, `UnitSemantics`, `RoundingPolicy` |
| Repositories | Separate Budget and BudgetVersion repositories; period/line persistence must remain aggregate-isolated |
| Commands | Create/update/archive Budget; create/certify BudgetVersion; select/unselect funding baseline; no approve command |
| Queries | Budget list/detail; version list/detail; selected baseline; authoritative reference validation |
| Contract DTOs | `BudgetVersionReferenceV1`, `BudgetVersionReferenceValidationRequestV1`, `BudgetVersionReferenceValidationResultV1` |
| Proposed API | `/api/budgets`, `/api/budgets/{budgetId}/versions`, `/api/budget-versions/{id}`, `/api/budget-version-references/validate` |
| Proposed UI | `/FPandA/Budgets`, `/FPandA/Budgets/{budgetId}/Versions`; no PPM-hosted budget form |
| Permissions | `budgeting.budgets.read|create|update|archive`; `budgeting.budget-versions.read|create|certify|select`; `budgeting.budget-version-references.validate` |

Names above are governance proposals, not runtime literals, until this pack is approved and topology is fixed.

### Gate I-B typed contract — proposed exact v1

- Type name: `BudgetVersionReferenceV1`.
- `ContractName`: `fpa.budget-version-reference` (exact, case-sensitive).
- `ContractVersion`: `1.0` (exact major/minor; unknown major or unsupported version fails closed with `503`).
- Fields: `ContractName`, `ContractVersion`, `BudgetId`, `BudgetVersionId`, immutable
  `BudgetVersionNumber`, `CurrencyReference`, `PeriodReference`.
- `CurrencyReference` and `PeriodReference` are canonical identity references used for authoritative matching,
  not denormalized currency metadata, dates, amounts, allocations or other financial snapshots.
- Validation request adds the authenticated tenant/service context server-side; neither `TenantId` nor actor
  identity is accepted from the consumer payload.
- An affirmative authoritative result means the referenced IDs belong together in the current tenant, the
  immutable token matches, the version exists and is not quarantined/deleted, and is referenceable under the
  requested purpose. For MOD-0117 selected funding baseline use, it also means it is the current selected
  baseline at validation time. It does not return or authorize amount/line payloads or denormalized currency/
  period snapshots beyond the canonical reference identifiers in the tuple.
- Cardinality: `InvestmentCase → SelectedBudgetVersionReference: 0..1`. Alternative/analytical versions remain
  in MOD-0136. PPM stores only this selected typed reference: canonical currency/period reference identifiers
  are allowed solely as tuple members; amount, line payload and denormalized currency/period snapshots are forbidden.
- MOD-0136 is the sole owner of selected funding-baseline state. A consumer cannot select by writing its own
  reference; it requests/validates selection through the producer contract.

## 4. Entity Fields

### Common tenant-owned base

`EntityBase` is the proposed logical base pending service selection. `Id`, `TenantId`, `IsDeleted`,
`DeletedAt`, `CreatedAt`, `UpdatedAt` and technical concurrency `Version` are inherited. `TenantId`, `ActorId`,
`CreatedBy` and `UpdatedBy` are server-resolved and absent from request DTOs. Business version identity is
named `BudgetVersionNumber`, never `Version`.

### Budget

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC, max 64; unique partial `(TenantId, Code)` where not deleted |
| `Name` | string | Yes | Trim + NFC, max 200 |
| `Description` | string? | No | Trim + NFC, max 2000 |
| `DefaultCurrencyReference` | canonical reference | Yes | Authoritative financial reference; no free-text currency |
| `DefaultPeriodReference` | canonical reference | Yes | Authoritative fiscal/time-period reference |
| `UnitSemantics` | closed value | Yes | Proposed `Units`, `Thousands`, `Millions`; owner confirmation required |
| `RoundingPolicy` | value object | Yes | Scale and midpoint mode; immutable per certified version |

### Immutable BudgetVersion

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `BudgetId` | Guid | Yes | Same-tenant Budget; immutable |
| `BudgetVersionNumber` | long | Yes | Server-assigned monotonic token; unique `(TenantId, BudgetId, BudgetVersionNumber)` |
| `RevisionLabel` | string | Yes | Trim + NFC, max 120; descriptive only, not identity |
| `PeriodReference` | canonical reference | Yes | Version-wide period/fiscal calendar identity |
| `CurrencyReference` | canonical reference | Yes | ISO/canonical financial identity; no symbol/free text |
| `UnitSemantics` | closed value | Yes | Captured on the immutable version |
| `RoundingPolicy` | value object | Yes | Decimal scale plus midpoint rule captured on the version |
| `FundingBaselineType` | closed value | Yes | Owner-defined baseline classification; not approval state |
| `EffectiveFrom` | date | Yes | Canonical period/date semantics; exact timezone rule open |
| `Notes` | string? | No | Trim + NFC, max 2000; must not contain approval decisions |
| `CertificationState` | closed value | Yes | Proposed `Draft`, `Certified`, `Superseded`; certification is not approval |
| `CertifiedAtUtc` / `CertifiedBy` | server values | Conditional | Required on certification; actor server-side |
| `ContentHash` | string | Yes when certified | Canonical immutable content hash over version, periods and lines |

### BudgetPeriod and BudgetLine

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `BudgetVersionId` | Guid | Yes | Same aggregate and tenant |
| `PeriodReference` | canonical reference | Yes | Must be contained by version period/fiscal calendar |
| `LineKey` | string | Yes | Stable normalized breakdown key; unique within version and period |
| `BreakdownDimensions` | typed map/value objects | Conditional | Allowed dimensions and cardinalities require FP&A owner decision; arbitrary JSON forbidden |
| `Amount` | decimal | Yes | Proposed BSON Decimal128 / CLR decimal; precision 28, scale 4 baseline, final decision open |
| `CurrencyReference` | canonical reference | Yes | Must match version currency unless explicit FX policy is later approved |
| `UnitSemantics` | closed value | Yes | Must match version unit semantics |

Amounts are normalized with the version rounding policy before persistence and hashing. Negative values are
rejected by default; signed adjustment lines require an explicit line type and owner-approved rule. Floating
point types are forbidden. Indexes begin with `TenantId` and include `IsDeleted` where applicable.

## 5. Repo Scope

### This draft authoring task

- `execution/domains/enterprise-strategy-business-performance/module-packs/MOD-0136-budgeting.md` only.

### Future implementation scope (non-authoritative until promotion)

- A topology-selected service's isolated `Budgeting` Domain/Application/Persistence/Infrastructure/Api paths.
- `frontend/Diten.Web` tenant-shell Budget and BudgetVersion paths only after explicit frontend authority.
- A separately authorized Gateway route task owned by `integration-agent` only after service/port selection.
- A separately approved MOD-0117 consumer amendment after this producer contract is approved.

## 6. Protected Paths

- `.antigravity/**`.
- `services/**`, `frontend/**` and `gateway/**` during this draft authoring task.
- `gateway/Diten.ApiGateway/**/ocelot.json` in all module-author work; integration-agent only.
- Other domain governance, especially `execution/domains/portfolio-delivery/**` and MOD-0117.
- `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**` and
  `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- Existing `Diten.EnterpriseStrategyService` budget-like fields; they are evidence/quarantine only.
- MOD-0138 Scenario, MOD-0023 workflow/approval, MOD-0072 actual outcome and accounting/payment internals.

## 7. Dependencies

| Dependency | Purpose | Current gate |
|---|---|---|
| DCP-002 / Blueprint | Canonical MOD-0136 identity and name | Exact preflight PASS on 2026-08-02 |
| DCP-006 / MOD-0117 | Gate I-B orchestration and future consumer | OPEN/PARTIAL; no consumer runtime authority |
| MOD-0018 | Tenant entitlement, JWT permissions and service identity | Runtime provisioning/evidence required |
| MOD-0021 / MOD-0035 | Immutable audit intent/consumer mechanics | Runtime evidence required; domain mutation and audit intent atomicity mandatory |
| MOD-0023 | Optional authoritative approval outcome | Separate typed contract; no local approval state |
| Canonical currency authority | Currency identity and metadata | Exact module/endpoint and availability SLA open |
| Canonical time-period authority | Fiscal calendar and period identity | Exact module/endpoint and availability SLA open |
| MongoDB replica set | Multi-document transaction and idempotency | Required before runtime promotion |

## 8. Runtime Constraints

- Data is tenant-owned. `TenantId` and `ActorId` come only from authenticated server context. Cross-tenant
  reads/validation return `404`; an authenticated actor lacking permission receives `403`.
- Client-supplied `TenantId` or `ActorId` is rejected with `400`. Every aggregate, child, selection, audit
  intent, idempotency and quarantine record carries server-assigned `TenantId`; repositories require it as a
  non-optional argument and every query, mutation and index predicate is tenant-scoped.
- Certified/published BudgetVersion content is immutable. Mutation attempts and stale selection/version
  conflicts return `409` and never partially update the aggregate.
- Unknown/unsupported contract version or unavailable authoritative currency/period/reference dependency
  returns `503` and fails closed; local cached existence must not create authority.
- Version creation, period/line persistence, selected-baseline uniqueness, outbox/audit intent and idempotency
  record use a Mongo replica-set transaction. Domain mutation without its audit intent must roll back.
- Every mutating request carries a bounded idempotency key scoped by tenant, actor, operation and normalized
  payload hash. Same key/same payload replays the original result; same key/different payload returns `409`.
- Idempotency persistence records the canonical request hash, operation, in-progress/completed state and stable
  response in the same transaction. Canonical decimal representation and deterministic line ordering are part
  of hashing; key length/charset, retention/TTL and unknown-commit recovery remain promotion decisions.
- Soft delete cannot erase or make a currently referenced/selected certified version ambiguous; retention,
  de-selection and legal-hold behavior require owner decision before promotion.
- No service, collection, route, event, transport or port is allocated by this draft.

### Service topology decision

| Option | Benefits | Risks / required controls | Draft disposition |
|---|---|---|---|
| A. New `Diten.FpaService`, isolated Budgeting internal module | Co-locates MOD-0136 and MOD-0138 FP&A consistency/transactions while retaining a bounded modular-monolith | Separate repositories, types, permissions, namespaces and Mongo collections per module; architecture tests must prevent cross-module persistence/type access | **Recommended** |
| B. Separate `Diten.BudgetingService` | Strongest deployment and data boundary | More operational, contract and deployment overhead; cross-service coordination with MOD-0138 | Fallback if isolation tests cannot enforce A |
| C. `Diten.EnterpriseStrategyService` | Reuses existing host | Code proximity may turn prototype fields into false production authority and blend strategy/FP&A ownership | Not recommended; evidence only unless separately justified |

The proposed production topology is a new `Diten.FpaService` modular monolith containing isolated MOD-0136
Budgeting and MOD-0138 Scenario Planning internal modules. Architecture tests must forbid repository, domain
type, permission and collection leakage. Failure to maintain those boundaries triggers fallback to a separate
`Diten.BudgetingService`. Existing EnterpriseStrategy budget fields are prototype/migration evidence only.

## 9. Layout & Shell Contract

- Proposed shell: `tenant`; every future Razor page explicitly sets `Layout = "_LayoutTenantShell";`.
- Proposed view paths: `Views/FPandA/Budgets/**` and `Views/FPandA/BudgetVersions/**`; no ASP.NET `Areas/`.
- Budget surface field count: **7** (`Code`, `Name`, `Description`, `DefaultCurrencyReference`,
  `DefaultPeriodReference`, `UnitSemantics`, `RoundingPolicy`) → Golden Slim.
- BudgetVersion create surface field count: **14**. Version-level inputs are `RevisionLabel`,
  `PeriodReference`, `CurrencyReference`, `UnitSemantics`, `RoundingPolicy`, `FundingBaselineType`,
  `EffectiveFrom`, `Notes` (8). The repeatable line editor adds `PeriodReference`, `LineKey`,
  `BreakdownDimensions`, `Amount`, `CurrencyReference`, `UnitSemantics` (6) → Golden Compact. Route-derived
  `BudgetId` and server fields `BudgetVersionNumber`, certification, hash, tenant, actor and audit are excluded.
- Parent pack frontmatter selects `compact` and `form_field_count: 14` because the highest-complexity primary
  create/edit surface governs the module template. Budget may use the Slim list/create pattern only if a later
  implementation slice preserves both explicit counts and verifier contracts.
- Approval UI and PPM-hosted budget forms are forbidden.

## 10. Backend File Convention

Future code must copy Golden Reference Compact action-based structure inside the topology-selected, isolated
Budgeting module: `Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`,
`Validators/`, and `{Feature}Models.cs`. Each public command/query/handler/validator has its own file;
handlers end in `Handler` without `Command`, `Query` or `Request` suffix. Controllers contain no business logic,
and all I/O is async with `CancellationToken`. Persistence concerns remain outside Domain/Application.

BudgetVersion aggregate creation/certification may require an application service coordinating one transaction;
handlers must not directly implement arbitrary child upsert loops or external HTTP calls.

## 11. Frontend File Contract

- Budget (7 fields, Slim candidate): `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`,
  `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, marker class, scripts and seven-language RESX.
- BudgetVersion (14 fields, Compact): `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`,
  `_Form.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, marker class, scripts and
  seven-language RESX. Compact must not use create/edit or details offcanvas partials.
- Both tables use `data-dt-standard="v2"`, skeleton loader and shared bulk-action conventions where deletion is
  permitted. A certified version has no edit action. Selection/certification labels remain distinct.
- Browser calls the frontend same-origin proxy/Gateway only; no direct service port or hardcoded currency/
  period fallback list. Approval controls and any PPM budget editor are absent.

## 12. Validation Rules

| Field / invariant | Required | Format/rule | DB-level / pre-check |
|---|---:|---|---|
| Budget Code | Yes | Trim + NFC; 1..64 | Unique active tenant index; duplicate pre-check |
| Budget Name | Yes | Trim + NFC; 1..200 | Validator |
| Description / Notes | No | Trim + NFC; max 2000 | Validator |
| CurrencyReference | Yes | Exact canonical typed identity | Authoritative fail-closed validation |
| PeriodReference | Yes | Exact canonical typed identity and containment | Authoritative fail-closed validation |
| UnitSemantics | Yes | Closed owner-approved enum | Same value across version/lines |
| RoundingPolicy | Yes | Scale within approved limit and closed midpoint mode | Normalize before hash/persist |
| BudgetVersionNumber | Server | Positive, monotonic, immutable | Unique tenant/budget index + transaction |
| EffectiveFrom | Yes | Canonical date/period rule | Period compatibility validation |
| Amount | Yes | Decimal only; proposed precision 28/scale 4 | Decimal128; overflow/extra scale rejected or deterministically rounded per approved rule |
| Negative Amount | Conditional | Rejected unless explicit adjustment type permits it | Domain invariant |
| LineKey | Yes | Normalized and unique within version/period | Compound unique index |
| Selection | Conditional | At most one selected non-deleted version per Budget | Partial unique index/transaction |
| ContractName/Version | Yes | Exact v1 constants | Unknown unsupported version returns 503 |
| TenantId / ActorId | No client field | Server context only | Client-supplied value rejected |
| IdempotencyKey | Mutations | Non-empty, bounded, normalized | Unique scoped key + payload hash |

## 13. Failure Path to Verify

- Duplicate Budget code: `409`; no record or audit intent persists.
- Missing/invalid currency or period: `400` for malformed input; authoritative dependency unavailable or
  unknown version: `503`; no fail-open/local inference.
- Cross-tenant Budget/BudgetVersion/reference: `404`, with no existence disclosure.
- Authenticated actor missing permission: `403`; no mutation or audit intent.
- Attempt to modify certified/published version or stale technical version: `409`; immutable bytes/hash remain.
- Concurrent version creation: exactly one next `BudgetVersionNumber` wins; loser receives `409` or safely
  replays by idempotency key; no duplicate number.
- Concurrent selected-baseline change: exactly one selected version remains; transaction/audit rollback leaves
  the previous truth intact on failure.
- Duplicate idempotency key: same payload returns original result; changed payload returns `409`.
- Audit/outbox write failure: entire domain transaction rolls back.
- MOD-0117 consumer attempts amount/line or denormalized currency/period snapshot: architecture/contract test fails.
- Prototype record cannot prove canonical identity/version/content hash: quarantined, not silently promoted.

## 14. Authorization Convention

- Policy: authenticated tenant actor plus independent entitlement and permission checks.
- Permission keys are exact lowercase dotted keys listed in `Owned Objects` and satisfy PKS-001.
- `TenantId`, end-user `ActorId` and delegated service identity are server-derived. S2S reference validation
  requires both authorized service identity and delegated tenant/actor context; service identity alone is not
  an end-user permission bypass.
- Read/create/update/archive, create/certify/select and validate permissions are separate. `select` does not
  imply `certify`; neither implies MOD-0023 approval.
- Cross-tenant lookup is `404`; authenticated insufficient permission is `403`; unauthenticated is `401`.
- Returned errors use stable safe codes and expose no Mongo index, stack, provider or cross-tenant existence
  detail. Rejected operations create neither a domain mutation nor an audit intent.

## 15. Gateway / API Routing Decision

Decision: **deferred and currently prohibited**. Service and port are `TBD`; this draft allocates neither.
Frontend must eventually call Gateway port 5000 through the approved profile, never a service port. After
topology approval, an integration-agent-only task must determine explicit upstream/downstream routes (including
`OPTIONS`) and test auth, tenant and correlation headers. This pack does not modify `ocelot.json`.

## 16. Acceptance Criteria

- [ ] MOD-0136 remains the sole owner of Budget, immutable BudgetVersion and selected funding-baseline truth.
- [ ] Certified/published versions cannot be updated; revision creates a new monotonic version and content hash.
- [ ] Selection is tenant-scoped and unique per Budget under concurrent requests.
- [ ] `BudgetVersionReferenceV1` uses exact ContractName/version and validates referenceability authoritatively.
- [ ] MOD-0117 cardinality is `0..1`, stores only the selected tuple (including its canonical currency/period
  reference identifiers), and contains no amount/line or denormalized currency/period snapshot fields;
  architecture tests enforce this no-copy boundary.
- [ ] `Selected`, `Certified` and MOD-0023 `Approved` remain distinct in domain, API, UI and localization.
- [ ] Tenant/actor values are server-side; cross-tenant returns 404, missing permission 403, immutable conflict
  409 and unavailable/unknown contract dependency 503.
- [ ] Decimal precision, scale, currency, unit, rounding and negative-value rules are owner-approved and tested.
- [ ] Mutation, audit intent, selected uniqueness and idempotency are atomic in a Mongo replica-set transaction.
- [ ] Budget field count 7 selects Slim; BudgetVersion field count 14 selects Compact; all tenant pages explicitly
  use `_LayoutTenantShell` and DataTables pass `verify_datatable_page.py` plus `quality-gate-datatable`.
- [ ] Seven-language RESX parity and browser smoke pass without approval UI, PPM budget forms or hardcoded lookup fallback.
- [ ] Prototype/migration candidates remain quarantined unless an explicit mapping, reconciliation, rollback and
  post-migration verification plan proves canonical identity and immutable content.
- [ ] Topology architecture tests prove repository/type/permission/collection isolation; otherwise Option B is selected.

## 17. Test Expectations

- Domain/unit: immutable certified versions, revision numbering, selected uniqueness, certification-vs-selection-
  vs-approval semantics, rounding/negative rules and content hash determinism.
- Integration: concurrent version creation and selection, Decimal128 precision/scale, canonical currency/period
  validation, tenant isolation, cross-tenant 404, permission 403, immutable 409, dependency/contract 503.
- Transaction: replica-set commit/rollback across aggregate, idempotency and audit intent; injected audit failure
  leaves no partial version/selection.
- Contract: exact v1 serialization and compatibility, unknown version, S2S/delegated actor enforcement,
  selected reference `0..1`, canonical currency/period reference identifiers only, and no amount/line or
  denormalized currency/period snapshot response.
- Architecture: PPM no-copy test; MOD-0136/MOD-0138 repository/type/permission/collection isolation; Domain has
  no Mongo driver dependency; no EnterpriseStrategy prototype treated as production baseline.
- Migration: classify/map/quarantine legacy EnterpriseStrategy budget evidence; collisions, precision loss,
  missing currency/period or unverifiable immutable token stay quarantined; rollback and counts reconcile.
- Migration quarantine: preserve source collection/document ID, tenant provenance, schema/mapping version,
  source hash, run ID and reason code; deterministic reruns create no duplicates. Ambiguous/missing tenant,
  currency, period, unit, dimension, total or negative-value semantics are never defaulted or silently rounded.
  Migrated candidates remain non-referenceable and unselected until explicit owner-reviewed reconciliation.
- Frontend (after authority): explicit tenant layout, Slim/Compact file sets and counts, DataTable verifier,
  `quality-gate-datatable`, seven-language RESX parity, same-origin/Gateway browser smoke, no approval/PPM forms.
- Build (after topology): selected service, frontend and gateway builds PASS; no port/Gateway allocation is
  inferred before approval.

## 18. Ready-for-dev Checklist

- [ ] FP&A Budgeting Process Owner approves ownership, lifecycle, version/certification and selection semantics.
- [ ] Financial open decisions in Follow-up Items are closed with exact values.
- [ ] Service topology is approved; `service`, entity base, collection ownership and repo scope are concrete.
- [ ] Option A isolation architecture tests are specified and proven feasible, or Option B fallback is selected.
- [ ] Exact currency and time-period authorities/contracts/SLAs are approved.
- [ ] `BudgetVersionReferenceV1` producer contract is approved; separate MOD-0117 consumer amendment remains gated.
- [ ] Golden Reference Compact live pack/code and Budget Slim pattern are confirmed; field counts remain 14 and 7.
- [ ] Layout, backend convention, frontend file contract and field validation are complete for authorized slices.
- [ ] Permission catalog/grants, S2S identity and delegated actor propagation are approved.
- [ ] Mongo replica set, transaction, audit intent, idempotency and indexes have executable test plans.
- [ ] Migration inventory/mapping/quarantine/rollback plan is approved; prototypes are not production baseline.
- [ ] Gateway decision is concrete but remains an integration-agent task; port is not guessed.
- [ ] Acceptance/test expectations include build, verifier, RESX, contract, architecture and browser smoke.
- [ ] User explicitly changes status to `approved` or `ready-for-dev` and separately grants runtime authority.

## 19. Implementation Notes

- DCP-002 exact preflight command passed on 2026-08-02:
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0136 --name "Budgeting"`.
- This is a parent pack, not an implementation slice. `production_authority: none` is deliberate.
- Existing `BudgetEnvelope`, `BudgetAmount`, `FundingSource` and yearly budget fields in
  `Diten.EnterpriseStrategyService` are prototype/migration evidence only. They establish neither ownership nor
  production baseline and must be inventoried and quarantined before any later migration.
- Gate I-B consumer work remains non-executable until the producer contract and matching MOD-0117 consumer
  amendment are each approved and explicit runtime authority is recorded. DCP-006 OD-04 remains OPEN/PARTIAL.
- No runtime literal, collection, permission registration, route, port, event or deployment was created here.

## 20. Follow-up Items

### Financial open decisions (promotion blockers)

- Confirm amount precision/scale (proposal `decimal(28,4)`/Decimal128), maximum aggregate totals and overflow behavior.
- Confirm midpoint rounding mode, when rounding occurs, and whether displayed vs persisted precision may differ.
- Confirm negative-value policy and exact permitted adjustment/credit line types.
- Name the canonical currency authority, typed reference shape, supported currency changes and any FX/rate owner.
- Name the canonical fiscal calendar/time-period authority, period granularity, timezone/date boundary and closed-period behavior.
- Approve unit semantics (`Units`, `Thousands`, `Millions`) and whether mixed units/currencies are ever allowed.
- Define BudgetPeriod and BudgetLine breakdown dimensions, mandatory line keys and uniqueness/cardinality limits.
- Define funding baseline classifications, certification prerequisites, selection/de-selection permissions and
  whether selecting an uncertified version is always forbidden; confirm the authoritative selection scope
  (Budget, fiscal plan, organization/cost center or another funding context).
- Define retention/legal hold and behavior when a selected/referenced version is archived or superseded.
- Decide whether draft construction is mutable staging or append-only commands before immutable certification.

### Governance and delivery follow-ups

- Obtain topology architecture review for Option A and document Option B fallback trigger evidence.
- Author/approve the MOD-0138 parent pack with equal internal-module isolation.
- After this producer contract is approved, author a separate non-executable MOD-0117 Gate I-B consumer amendment.
- Resolve MOD-0018 permissions/S2S identity and MOD-0021/MOD-0035 audit runtime evidence.
- Inventory EnterpriseStrategy prototype documents; write explicit mapping, precision-loss detection, quarantine,
  reconciliation, rollback and post-migration verification before requesting migration authority.
