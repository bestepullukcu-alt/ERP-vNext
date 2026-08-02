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
form_field_count: 13
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

- `Budget` stable identity, mutable `BudgetVersionDraft` construction and immutable certified `BudgetVersion` history.
- Explicit `BudgetPeriod` and separate `BudgetLine` financial breakdowns.
- Canonical currency reference, unit/scale semantics, precision and rounding policy captured by a version.
- Effective-dated selected-baseline truth through immutable `FundingBaselineSelectionOccurrence` and immutable
  `SelectionClosure`/`SelectionDisposition` events for an authoritative typed funding context; its exact type
  remains an FP&A promotion blocker.
- Authoritative minimal `BudgetVersionReference` referenceability validation and, separately, selected-baseline
  validation against a typed `FundingContextReference`.

### Does not own

- Portfolio or `InvestmentCase` lifecycle; MOD-0117 owns them.
- Scenario, scenario selection, comparator run or output; MOD-0138 owns them.
- Workflow, approval, SLA or escalation behavior; MOD-0023 owns it.
- Actual benefit outcome or realization; MOD-0072 owns it.
- A PPM-local amount, currency, period, line or budget snapshot.
- Accounting ledger, journal posting, commitment accounting, treasury or payment execution.
- Local approve/reject fields, commands, routes or UI. `Approved`, `Selected` and `Certified` are not synonyms.
- `InvestmentCase -> SelectedBudgetVersionReference` cardinality, lifecycle-transition requirements and
  consumer-side storage; MOD-0117 owns those rules.

### Version and selection model

- `Budget` is the stable identity. A revision never replaces that identity.
- Mutable construction occurs only in an explicit `BudgetVersionDraft`/working revision. Certification/sealing
  atomically creates the immutable `BudgetVersion` snapshot, server-derived provenance and versioned
  `ContentHash`; correction creates a new ID and monotonic `BudgetVersionNumber`.
- Supersession/retirement is append-only disposition history and never rewrites certified snapshot bytes.
- Certification is MOD-0136 financial sealing, not approval. Where policy requires approval, a typed immutable
  MOD-0023 `ApprovalOutcome` is a prerequisite/reference; MOD-0136 has no approval state or engine.
- `BudgetVersionReferenceV1` identifies one immutable certified version and does not assert selection.
  Selection is separate append-only/effective-dated history: an immutable `FundingBaselineSelectionOccurrence`
  plus immutable `SelectionClosure`/`SelectionDisposition` events.
- Selected-baseline validation accepts the immutable reference plus an authoritative typed
  `FundingContextReference`. The context must be a closed type, never free text, and v1 cannot be finalized
  until the owner selects Budget, fiscal plan, organization/cost center, InvestmentCase or another typed context.

## 3. Owned Objects

| Category | Proposed owned objects |
|---|---|
| Aggregates/entities | `Budget`, `BudgetVersionDraft`, immutable certified `BudgetVersion`, `BudgetPeriod`, `BudgetLine`, immutable `FundingBaselineSelectionOccurrence`, immutable `SelectionClosure`, immutable `SelectionDisposition` |
| Value objects | `MoneyAmount`, `CurrencyReference`, `TimePeriodReference`, `UnitSemantics`, `RoundingPolicy` |
| Repositories | Separate Budget and BudgetVersion repositories; period/line persistence must remain aggregate-isolated |
| Commands | Create/update/archive Budget; create/update/abandon draft; certify draft; replace/close selection; retire/supersede version; no approve command |
| Queries | Budget/draft/version detail and lists; effective selection/history; referenceability and selected-baseline validation |
| Contract DTOs | Minimal `BudgetVersionReferenceV1`, referenceability request/result, typed `FundingContextReference`, selected-baseline validation request/result; display contract separate |
| Proposed API | `/api/budgets`, `/api/budgets/{budgetId}/versions`, `/api/budget-versions/{id}`, `/api/budget-version-references/validate` |
| Proposed UI | `/FPandA/Budgets`, `/FPandA/Budgets/{budgetId}/Versions`; no PPM-hosted budget form |
| Permissions | `budgeting.budgets.read`<br>`budgeting.budgets.create`<br>`budgeting.budgets.update`<br>`budgeting.budgets.archive`<br>`budgeting.budget-version-drafts.read`<br>`budgeting.budget-version-drafts.create`<br>`budgeting.budget-version-drafts.update`<br>`budgeting.budget-version-drafts.abandon`<br>`budgeting.budget-versions.read`<br>`budgeting.budget-versions.certify`<br>`budgeting.budget-versions.retire`<br>`budgeting.funding-baseline-selections.read`<br>`budgeting.funding-baseline-selections.replace`<br>`budgeting.funding-baseline-selections.close`<br>`budgeting.budget-version-references.validate` |

Names above are governance proposals, not runtime literals, until this pack is approved and topology is fixed.

### Gate I-B typed contract — proposed exact v1

- Type name: `BudgetVersionReferenceV1`.
- `ContractName`: `fpa.budget-version-reference` (exact, case-sensitive).
- `ContractVersion`: `1.0` (exact major/minor; unknown major or unsupported version fails closed with `503`).
- Persistable fields are exactly `ContractName`, `ContractVersion`, `BudgetId`, `BudgetVersionId` and immutable
  `BudgetVersionNumber`. It is an opaque identity/referenceability tuple, not selection state.
- Currency, fiscal period, units, rounding, lines and amounts remain MOD-0136 financial truth. MOD-0117 cannot
  persist them. Display needs use a separately authorized read/display contract and never extend this value.
- Validation request adds the authenticated tenant/service context server-side; neither `TenantId` nor actor
  identity is accepted from the consumer payload.
- An affirmative referenceability result means the IDs belong together in the current tenant, the immutable
  number matches, and the certified version is historically referenceable and not quarantined. It says nothing
  about current selection.
- A separate selected-baseline validator takes `BudgetVersionReferenceV1` plus a typed
  `FundingContextReference` and verifies the current effective selection. Its v1 contract remains blocked until
  the context decision closes; no free-text discriminator is permitted.
- MOD-0117 owns `InvestmentCase -> SelectedBudgetVersionReference: 0..1`, whether it is mandatory for a PPM
  transition, that transition and minimal tuple storage. MOD-0136 may supply `0..1` compatibility fixtures but
  does not own PPM cardinality. Alternative/analytical versions remain in MOD-0136.
- MOD-0136 is the sole owner of selected funding-baseline state. A consumer cannot select by writing its own
  reference; it requests/validates selection through the producer contract.

## 4. Entity Fields

### Common tenant-owned base

`entity_base: EntityBase` is the valid tenant-owned logical contract required by the pack standard. It does not
select or invent a service-local class; concrete mapping remains blocked by topology. `Id`, `TenantId`, `IsDeleted`,
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
| `RoundingPolicy` | value object | Yes | Construction default for scale and midpoint mode |

These Budget-level currency, period, unit and rounding values are construction defaults only. Certification
captures every applicable value explicitly in the immutable snapshot. Later default changes cannot alter,
reinterpret or re-hash a certified version.

### BudgetVersionDraft / working revision

Mutable construction contains candidate version fields, periods and lines. It has no certification state,
certification provenance or authoritative `ContentHash`. An abandoned draft may be soft-deleted under policy.
Certification consumes an exact draft fence and atomically creates the immutable snapshot, provenance and hash.

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
| `CertifiedAtUtc` / `CertifiedByActorId` | server values | Yes | Created atomically with snapshot; never populated later |
| `ApprovalOutcomeReference` | typed immutable reference? | Conditional | Separate MOD-0023 prerequisite when policy requires; not certification state |
| `ContentHashAlgorithm` | constant | Yes | Proposed exact `SHA-256` |
| `ContentCanonicalizationVersion` | closed identity | Yes | Exact identity and vectors are ready-for-dev blockers |
| `ContentHash` | lowercase hex string | Yes | Created atomically over canonical snapshot; immutable |

### BudgetPeriod

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `BudgetPeriodId` | Guid | Yes | Snapshot-local identity; excluded from hash serialization |
| `BudgetVersionId` | Guid | Yes | Same immutable aggregate and tenant |
| `PeriodReference` | canonical reference | Yes | Deterministically generated from the authoritative version-level fiscal calendar; unique within version and not user-editable |
| `Order` | int | Yes | System-generated deterministic non-negative ordering; unique within version and not user-editable |
| `ContainmentReference` | typed reference? | Conditional | Parent period when canonical model requires it |
| `VersionTotal` | decimal? | Conditional | Derived-versus-asserted policy is a promotion blocker |

### BudgetLine

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `BudgetVersionId` | Guid | Yes | Same aggregate and tenant |
| `BudgetPeriodId` | Guid | Yes | Exactly one explicit same-snapshot BudgetPeriod |
| `LineKey` | string | Yes | Stable normalized breakdown key; unique within version and period |
| `BreakdownDimensions` | typed map/value objects | Conditional | Allowed dimensions and cardinalities require FP&A owner decision; arbitrary JSON forbidden |
| `Amount` | decimal | Yes | Proposed BSON Decimal128 / CLR decimal; precision 28, scale 4 baseline, final decision open |
| `CurrencyReference` | canonical reference | Yes | Must match version currency unless explicit FX policy is later approved |
| `UnitSemantics` | closed value | Yes | Must match version unit semantics |

Amounts are normalized with the version rounding policy before persistence and hashing. Negative values are
rejected by default; signed adjustment lines require an explicit line type and owner-approved rule. Floating
point types are forbidden. Indexes begin with `TenantId` and include `IsDeleted` where applicable.

### FundingBaselineSelectionOccurrence

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `FundingContextReference` | typed closed reference | Yes | Stable authoritative context; no free text |
| `SelectedBudgetVersionReference` | `BudgetVersionReferenceV1` | Yes | Certified and referenceable |
| `EffectiveFromUtc` | timestamp | Yes | Server time; inclusive |
| `SelectedByActorId` | actor reference | Yes | Trusted server context |
| `ReasonCode` / `ProvenanceReference` | closed/typed values | Yes | Auditable; no approval semantics |
| `CorrelationId` / `IdempotencyKey` | bounded identifiers | Yes | Correlation and replay control |
| `SelectionFence` | monotonic token | Yes | Compare-and-swap per tenant + context |

The occurrence is immutable after insertion and has no terminal timestamp that is populated later. Immutable
`SelectionClosure` and `SelectionDisposition` events reference the occurrence, record the server-resolved effective timestamp,
actor, reason/provenance, correlation/idempotency values and selection fence, and express closure, replacement
or retirement without editing the old occurrence. Current effective selection is derived from occurrence plus
closure/replacement history. Exactly one effective occurrence exists per tenant plus typed funding context.
Replacement transactionally appends the prior occurrence's closure/disposition event, its successor occurrence
and audit intent; concurrent attempts cannot leave two effective occurrences. Rollback leaves the previous
effective truth unchanged. Selection never changes BudgetVersion bytes/hash, and PPM cannot create selection by
storing a reference.

`SelectionClosure`/`SelectionDisposition` event fields are the referenced occurrence ID, closed/disposed-at UTC
timestamp, server-derived actor, closed reason/provenance, correlation ID, idempotency key and selection fence;
replacement disposition also references the successor occurrence. Event identity and all event fields are
immutable after insertion.

### ContentHash contract

The proposal is SHA-256 over exact UTF-8 bytes without BOM from a versioned canonicalization. Canonicalization
must define deterministic period, line and dimension ordering; invariant decimal representation; canonical
currency, period and typed-reference serialization; Unicode normalization, delimiters, null/empty and newline
rules. Because technical child IDs are excluded, each line's period linkage is serialized by the referenced
BudgetPeriod's canonical PeriodReference plus deterministic order, never by `BudgetPeriodId`. Database row/child
IDs and technical concurrency `Version` are excluded. Exact canonicalization identity
and normative positive/negative test vectors are ready-for-dev blockers; implementation must not invent them.

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
- Certified BudgetVersion content is immutable. Mutation attempts and stale selection/version
  conflicts return `409` and never partially update the aggregate.
- Malformed currency/period/reference shapes, decimal overflow and disallowed scale return `400`. An unavailable
  authoritative provider when validation is required, or unknown/unsupported contract version, returns `503`.
  Existing certified versions remain historically readable during provider outage and are never reinterpreted;
  cached results cannot authorize new certification or selection.
- Version creation, period/line persistence, selected-baseline uniqueness, outbox/audit intent and idempotency
  record use a Mongo replica-set transaction. Domain mutation without its audit intent must roll back.
- Every mutating request carries a bounded idempotency key scoped by tenant, actor, operation and normalized
  payload hash. Same key/same payload replays the original result; same key/different payload returns `409`.
- Idempotency persistence records the canonical request hash, operation, in-progress/completed state and stable
  response in the same transaction. Canonical decimal representation and deterministic line ordering are part
  of hashing; key length/charset, retention/TTL and unknown-commit recovery remain promotion decisions.
- Uncertified abandoned staging may be soft-deleted under policy. Certified, selected, referenced or historically
  effective versions cannot be ordinary CRUD-deleted: append-only retirement/supersession preserves queryable
  financial and selection history. Legal hold, retention and exceptional purge need separately approved compliance
  authority. PPM history is never rewritten when selection changes.
- No service, collection, route, event, transport or port is allocated by this draft.

### Service topology decision

| Option | Benefits | Risks / required controls | Draft disposition |
|---|---|---|---|
| A. New `Diten.FpaService`, isolated Budgeting internal module | Co-locates MOD-0136 and MOD-0138 FP&A consistency/transactions while retaining a bounded modular-monolith | Separate repositories, types, permissions, namespaces and Mongo collections per module; architecture tests must prevent cross-module persistence/type access | **Recommended** |
| B. Separate `Diten.BudgetingService` | Strongest deployment and data boundary | More operational, contract and deployment overhead; cross-service coordination with MOD-0138 | Fallback if isolation tests cannot enforce A |
| C. `Diten.EnterpriseStrategyService` | Reuses existing host | Code proximity may turn prototype fields into false production authority and blend strategy/FP&A ownership | Not recommended; evidence only unless separately justified |

The conditional recommendation is a new `Diten.FpaService` modular monolith containing isolated MOD-0136
Budgeting and MOD-0138 Scenario Planning internal modules. Architecture tests must forbid repository, domain
type, permission and collection leakage. Failure to maintain those boundaries triggers fallback to a separate
`Diten.BudgetingService`. Existing EnterpriseStrategy budget fields are prototype/migration evidence only.

## 9. Layout & Shell Contract

- Proposed shell: `tenant`; every future Razor page explicitly sets `Layout = "_LayoutTenantShell";`.
- Proposed view paths: `Views/FPandA/Budgets/**` and `Views/FPandA/BudgetVersions/**`; no ASP.NET `Areas/`.
- Budget surface field count: **7** (`Code`, `Name`, `Description`, `DefaultCurrencyReference`,
  `DefaultPeriodReference`, `UnitSemantics`, `RoundingPolicy`) → Golden Slim.
- BudgetVersionDraft create/edit surface field count: **13**. Draft inputs are `RevisionLabel`,
  `PeriodReference`, `CurrencyReference`, `UnitSemantics`, `RoundingPolicy`, `FundingBaselineType`,
  `EffectiveFrom`, `Notes` (8). The repeatable line editor adds `LineKey`, `BreakdownDimensions`, `Amount`,
  `CurrencyReference`, `UnitSemantics` (5) → Golden Compact. Child `BudgetPeriod.PeriodReference` values and
  `BudgetPeriod.Order` are deterministically generated from the authoritative fiscal calendar selected by the
  version-level `PeriodReference`; users do not select or edit child period references. Optional user-entered
  `ContainmentReference`/`VersionTotal` would increase the count. Route-derived
  `BudgetId` and server fields `BudgetVersionNumber`, certification, hash, tenant, actor and audit are excluded.
- Parent pack frontmatter selects `compact` and `form_field_count: 13` because the minimum primary
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
- BudgetVersionDraft construction (13 fields, Compact) and certified-version read-only detail: `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`,
  `_Form.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, marker class, scripts and
  seven-language RESX. Compact must not use create/edit or details offcanvas partials.
- Both tables use `data-dt-standard="v2"`, skeleton loader and shared bulk-action conventions where deletion is
  permitted for abandoned staging only. `Edit` targets drafts; a certified version has read-only detail and no
  edit/delete action. Selection/certification labels remain distinct and no mutable `Selected` flag exists.
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
| Draft fence | Mutation | Exact current technical token | Stale construction/certification returns 409 |
| ContentHash | Certification | SHA-256 + approved canonicalization identity | Seal-time recomputation + normative vectors |
| EffectiveFrom | Yes | Canonical date/period rule | Period compatibility validation |
| Amount | Yes | Decimal only; proposed precision 28/scale 4 | Decimal128; overflow/extra scale rejected or deterministically rounded per approved rule |
| Negative Amount | Conditional | Rejected unless explicit adjustment type permits it | Domain invariant |
| LineKey | Yes | Normalized and unique within version/period | Compound unique index |
| FundingContextReference | Selection | Closed authoritative typed shape; exact kind unresolved | No free-text discriminator |
| Selection | Conditional | One effective occurrence per tenant + typed funding context, derived from immutable occurrence and closure/disposition history | Fence + uniqueness enforcement + transaction |
| Selection effective time | Selection | Occurrence start and event timestamps are server-derived UTC; no occurrence is edited in place | No overlap per tenant + context |
| ContractName/Version | Yes | Exact v1 constants | Unknown unsupported version returns 503 |
| TenantId / ActorId | No client field | Server context only | Client-supplied value rejected |
| IdempotencyKey | Mutations | Non-empty, bounded, normalized | Unique scoped key + payload hash |

## 13. Failure Path to Verify

- Duplicate Budget code: `409`; no record or audit intent persists.
- Missing/malformed currency or period, decimal overflow or disallowed scale: `400`; provider unavailable or
  unknown version: `503`; no fail-open/local inference.
- Cross-tenant Budget/BudgetVersion/reference: `404`, with no existence disclosure.
- Authenticated actor missing permission: `403`; no mutation or audit intent.
- Attempt to modify a certified version or use a stale draft/selection fence: `409`; immutable bytes/hash remain.
- Concurrent version creation: exactly one next `BudgetVersionNumber` wins; loser receives `409` or safely
  replays by idempotency key; no duplicate number.
- Concurrent selected-baseline change: exactly one effective occurrence per tenant + typed context remains;
  replacement atomically appends closure/disposition, successor and audit intent without editing the old
  occurrence; transaction/audit rollback leaves the previous truth intact and preserves the version hash.
- Duplicate idempotency key: same payload returns original result; changed payload returns `409`.
- Audit/outbox write failure: entire domain transaction rolls back.
- MOD-0117 consumer attempts currency/period/unit/rounding/amount/line snapshot: architecture/contract test fails.
- Provider outage: existing certified history remains readable, but no cached result authorizes certification/selection.
- Prototype record cannot prove canonical identity/version/content hash: quarantined, not silently promoted.

## 14. Authorization Convention

- Policy: authenticated tenant actor plus independent entitlement and permission checks.
- Permission keys are exact lowercase dotted keys listed in `Owned Objects` and satisfy PKS-001.
- `TenantId`, end-user `ActorId` and delegated service identity are server-derived. S2S reference validation
  requires both authorized service identity and delegated tenant/actor context; service identity alone is not
  an end-user permission bypass.
- Budget CRUD, draft construction, version certification/retirement, selection read/replace/close and reference
  validation permissions are separate. Selection replacement does not imply certification; neither implies
  MOD-0023 approval.
- Cross-tenant lookup is `404`; authenticated insufficient permission is `403`; unauthenticated is `401`.
- Returned errors use stable safe codes and expose no Mongo index, stack, provider or cross-tenant existence
  detail. Rejected operations create neither a domain mutation nor an audit intent.

## 15. Gateway / API Routing Decision

Decision: **deferred and currently prohibited**. Service and port are `TBD`; this draft allocates neither.
Frontend must eventually call Gateway port 5000 through the approved profile, never a service port. After
topology approval, an integration-agent-only task must determine explicit upstream/downstream routes (including
`OPTIONS`) and test auth, tenant and correlation headers. This pack does not modify `ocelot.json`.

## 16. Acceptance Criteria

- [ ] MOD-0136 owns Budget, draft construction, immutable certified BudgetVersion, certification and context-scoped selection; MOD-0117 owns PPM cardinality/lifecycle/storage.
- [ ] Certification atomically seals snapshot, provenance and versioned SHA-256 hash; correction creates a new ID/number and disposition history is append-only.
- [ ] Selection is unique per tenant + typed funding context under concurrency and is derived from immutable
  occurrence plus immutable closure/disposition history; replacement edits no old occurrence and is hash-independent.
- [ ] `BudgetVersionReferenceV1` uses exact ContractName/version and validates referenceability authoritatively.
- [ ] MOD-0117's `0..1` compatibility fixture stores only the five-field opaque tuple; it contains no currency,
  period, unit, rounding, amount or line field, and architecture tests enforce this no-copy boundary.
- [ ] `Selected`, `Certified` and MOD-0023 `Approved` remain distinct in domain, API, UI and localization.
- [ ] Tenant/actor values are server-side; cross-tenant returns 404, missing permission 403, immutable conflict
  409 and unavailable/unknown contract dependency 503.
- [ ] Decimal precision, scale, currency, unit, rounding and negative-value rules are owner-approved and tested.
- [ ] Mutation, audit intent, selected uniqueness and idempotency are atomic in a Mongo replica-set transaction.
- [ ] Budget field count 7 selects Slim; BudgetVersionDraft field count 13 selects Compact because child period
  references/order are deterministically generated from the version fiscal calendar and are not user inputs; all tenant pages explicitly
  use `_LayoutTenantShell` and DataTables pass `verify_datatable_page.py` plus `quality-gate-datatable`.
- [ ] Seven-language RESX parity and browser smoke pass without approval UI, PPM budget forms or hardcoded lookup fallback.
- [ ] Prototype/migration candidates remain quarantined unless an explicit mapping, reconciliation, rollback and
  post-migration verification plan proves canonical identity and immutable content.
- [ ] Topology architecture tests prove repository/type/permission/collection isolation; otherwise Option B is selected.

## 17. Test Expectations

- Domain/unit: draft-to-atomic-seal, immutable certified versions, revision numbering, immutable occurrence plus
  closure/disposition-derived effective selection history,
  certification-vs-selection-vs-approval, defaults-vs-snapshot, rounding/negative rules and hash vectors.
- Integration: concurrent version creation and selection, Decimal128 precision/scale, canonical currency/period
  validation, tenant isolation, cross-tenant 404, permission 403, immutable 409, dependency/contract 503.
- Transaction: replica-set commit/rollback across aggregate, idempotency and audit intent; injected audit failure
  rolls back the appended closure/disposition event, successor occurrence and audit intent, leaving the prior
  effective truth intact with no old-occurrence mutation or partial version/selection.
- Contract: exact five-field v1 serialization, unknown version, S2S/delegated actor enforcement, referenceability
  independent of selection, typed-context selected validation, and no financial semantics in the PPM value.
- Provider resilience: outage does not block historical immutable reads or reinterpret stored bytes/hash; cached
  provider data cannot authorize a new certification or selection.
- Concurrency/mutation: simultaneous version numbers and selections, fence loss, immutable closure/disposition
  plus successor append atomicity, selected uniqueness, rollback invariance and unchanged certified hash;
  mutations verify idempotent replay.
- Architecture: PPM no-copy test; MOD-0136/MOD-0138 repository/type/permission/collection isolation; Domain has
  no Mongo driver dependency; no EnterpriseStrategy prototype treated as production baseline.
- Migration: classify/map/quarantine legacy EnterpriseStrategy budget evidence; collisions, precision loss,
  missing currency/period or unverifiable immutable token stay quarantined; rollback and counts reconcile.
- Migration quarantine: preserve source collection/document ID, tenant provenance, schema/mapping version,
  source hash, run ID and reason code; deterministic reruns create no duplicates. Ambiguous/missing tenant,
  currency, period, unit, dimension, total or negative-value semantics are never defaulted or silently rounded.
  Migrated candidates remain non-referenceable and unselected until explicit owner-reviewed reconciliation.
- Archive/retention: abandoned staging soft delete; certified/selected/referenced/history records reject CRUD
  delete and remain queryable after append-only retirement/supersession; no PPM history rewrite.
- Frontend (after authority): explicit tenant layout, Slim/Compact file sets and counts, DataTable verifier,
  `quality-gate-datatable`, seven-language RESX parity, same-origin/Gateway browser smoke, no approval/PPM forms.
- Build (after topology): selected service, frontend and gateway builds PASS; no port/Gateway allocation is
  inferred before approval.

## 18. Ready-for-dev Checklist

- [ ] FP&A Budgeting Process Owner approves ownership, lifecycle, version/certification and selection semantics.
- [ ] Financial open decisions in Follow-up Items are closed with exact values.
- [ ] Service topology is approved; `service`, concrete service-local mapping for logical `EntityBase`, collection ownership and repo scope are concrete without inventing a base type.
- [ ] Option A isolation architecture tests are specified and proven feasible, or Option B fallback is selected.
- [ ] Exact currency and time-period authorities/contracts/SLAs are approved.
- [ ] `BudgetVersionReferenceV1` producer contract is approved; separate MOD-0117 consumer amendment remains gated.
- [ ] Funding context is selected as a closed authoritative typed reference and selected-baseline v1 fixtures are approved; no free-text kind exists.
- [ ] SHA-256 canonicalization identity, exact UTF-8 rules, deterministic ordering/decimal/reference serialization and normative vectors are approved.
- [ ] Draft deletion, certified retirement/supersession, retention/legal hold and exceptional-purge authority are approved.
- [ ] Golden Reference Compact live pack/code and Budget Slim pattern are confirmed; field counts remain 13 and 7,
  with child periods deterministically generated from the authoritative version-level fiscal calendar.
- [ ] Layout, backend convention, frontend file contract and field validation are complete for authorized slices.
- [ ] The 15 exact lowercase permission keys are approved without wildcard, alias or normalization; permission
  catalog/grants, S2S identity and delegated actor propagation are approved.
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
- Define funding baseline classifications, certification prerequisites, replacement/closure permissions and
  whether selecting an uncertified version is always forbidden; confirm the authoritative selection scope
  (Budget, fiscal plan, organization/cost center, InvestmentCase or another typed funding context), its owner,
  exact typed shape and effective-time boundary. No selected-baseline v1 is final before this decision.
- Decide whether selected-baseline validation requires a distinct least-privilege permission. Do not add a key
  until entitlement evidence and the selected-baseline v1 boundary justify it.
- Approve SHA-256 canonicalization version, exact UTF-8 bytes, deterministic ordering/decimal/reference rules and
  normative vectors; implementation cannot create an interim scheme.
- Define retention/legal hold and exceptional purge authority. Certified, selected, referenced and historically
  effective versions use append-only retirement/supersession, never ordinary CRUD delete.
- Decide persistence/retention details for the explicit mutable `BudgetVersionDraft` staging model.

### Governance and delivery follow-ups

- Obtain topology architecture review for Option A and document Option B fallback trigger evidence.
- Author/approve the MOD-0138 parent pack with equal internal-module isolation.
- After this producer contract is approved, author a separate non-executable MOD-0117 Gate I-B consumer amendment.
- Resolve MOD-0018 permissions/S2S identity and MOD-0021/MOD-0035 audit runtime evidence.
- Inventory EnterpriseStrategy prototype documents; write explicit mapping, precision-loss detection, quarantine,
  reconciliation, rollback and post-migration verification before requesting migration authority.
