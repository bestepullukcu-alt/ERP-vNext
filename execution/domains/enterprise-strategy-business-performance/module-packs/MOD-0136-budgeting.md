---
id: MOD-0136
name: Budgeting
domain: enterprise-strategy-business-performance
service: Diten.FpaService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: draft
owner: FP&A Budgeting Process Owner
branch: feature/esbp/mod-0136-budgeting
started: 2026-08-02
target: 2026-08-02
form_field_count: 11
production_authority: none
---

# MOD-0136 — Budgeting

> **Draft-only authoring boundary:** This parent pack defines proposed ownership, contracts and promotion
> gates. It grants no runtime, frontend, service, collection, migration, port, Gateway or production authority.
> `service: Diten.FpaService` records the authoritative primary topology only; it does not authorize a scaffold
> or runtime. The fail-closed fallback is the coordinated `Diten.BudgetingService` plus
> `Diten.ScenarioPlanningService` profile; this pack owns only its Budgeting member. Placement in
> `Diten.EnterpriseStrategyService` is prohibited.

## 1. Module Summary

MOD-0136 is the tenant-owned FP&A budgeting system of record for stable budgets, immutable budget versions,
period/line breakdown, currency and unit semantics, funding-baseline selection and authoritative version
referenceability. Its Blueprint route is `Enterprise Application Ecosystem → ERP Suite → FP&A / EPM`.

The primary users are FP&A budgeting practitioners and authorized tenant consumers. This parent pack proposes
the exact Gate I-B producer contract required by MOD-0117 while keeping all consumer implementation
non-executable until producer and consumer packs are separately approved and explicit runtime authority exists.
Physical placement follows the authoritative
[ESBP domain topology](../domain-config.md#authoritative-physical-topology).

## 2. Ownership and Boundaries

### Owns

- `Budget` stable identity, mutable `BudgetVersionDraft` construction and immutable certified `BudgetVersion` history.
- MOD-0136-specific `BudgetFiscalCalendar`, its immutable monthly period definitions, explicit `BudgetPeriod`
  snapshots and separate `BudgetLine` financial breakdowns. This is a budgeting calendar, not an enterprise calendar SoR.
- Exact currency and budgeting-calendar references, closed unit semantics, decimal precision and system-owned
  `RoundingPolicyV1` captured by a version.
- Effective-dated selected-baseline truth through immutable `FundingBaselineSelectionOccurrence` and immutable
  `SelectionClosure`/`SelectionDisposition` events. For Gate I-B, the authoritative typed funding context is
  exact `ppm.investment-case-context` `1.0` with only `ContractName`, `ContractVersion`, `InvestmentCaseId`.
- Authoritative minimal `BudgetVersionReference` validation and, separately, current-selection validation
  against exact PPM-owned `InvestmentCaseContextV1` for Gate I-B.

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
- Gate I-B current-selection validation accepts the immutable reference plus exact PPM-owned
  `InvestmentCaseContextV1`. Consuming this context gives MOD-0136 no ownership of InvestmentCase fields,
  cardinality, storage, transitions or lifecycle; MOD-0117 retains all of those responsibilities.

## 3. Owned Objects

| Category | Proposed owned objects |
|---|---|
| Aggregates/entities | `Budget`, `BudgetFiscalCalendar`, immutable `BudgetFiscalCalendarPeriodDefinition`, `BudgetVersionDraft`, immutable certified `BudgetVersion`, immutable `BudgetVersionDisposition`, `BudgetPeriod`, `BudgetLine`, immutable `FundingBaselineSelectionOccurrence`, immutable `SelectionClosure`, immutable `SelectionDisposition` |
| Producer-local persistence records | `FundingBaselineSelectionFence`, immutable `BudgetingIdempotencyReceipt`, immutable audit intent, local event-outbox record and migration-quarantine record; none is a foreign business SoR |
| Value objects | `MoneyAmount`, `CurrencyReferenceV1`, `BudgetFiscalCalendarReferenceV1`, `PeriodReference`, `UnitSemantics`, `RoundingPolicyV1`, `FundingBaselineType`, `LineType` |
| Repositories | Separate Budget and BudgetVersion repositories; period/line persistence must remain aggregate-isolated |
| Commands | Create/update/archive Budget; create/update/abandon draft; certify draft; replace/close selection; retire/supersede version; no approve command |
| Queries | Budget/draft/version detail and lists; effective selection/history; referenceability and selected-baseline validation |
| Contract DTOs | Minimal producer `BudgetVersionReferenceV1`; exact PPM-owned `InvestmentCaseContextV1` request context and `SelectedBudgetVersionReferenceV1` wrapper compatibility fixtures; mode-specific validation request/result; display contract separate |
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
- `CurrentSelectionEligibility` takes `BudgetVersionReferenceV1` plus exact `InvestmentCaseContextV1` and
  verifies current effective selection. It is separate from historical resolution and new-reference eligibility;
  no free-text discriminator or locally inferred InvestmentCase state is permitted.
- MOD-0117 owns `InvestmentCase -> SelectedBudgetVersionReference: 0..1`, whether it is mandatory for a PPM
  transition, that transition and minimal tuple storage. MOD-0136 may supply `0..1` compatibility fixtures but
  does not own PPM cardinality. Alternative/analytical versions remain in MOD-0136.
- MOD-0136 is the sole owner of selected funding-baseline state. A consumer cannot select by writing its own
  reference; it requests/validates selection through the producer contract.

### Gate I-B bilateral PPM consumer profile — exact v1

- Typed funding context is PPM-owned `InvestmentCaseContextV1`, exact
  `ContractName = ppm.investment-case-context`, `ContractVersion = 1.0`, with fields exactly `ContractName`, `ContractVersion`,
  `InvestmentCaseId`. `TenantId`, `ActorId`, consumer kind and discriminator are not payload fields.
- PPM wrapper is PPM-owned `SelectedBudgetVersionReferenceV1`, exact
  `ContractName = ppm.investment-case-selected-budget-version-reference`, `ContractVersion = 1.0`, with fields exactly
  `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `BudgetVersionReference`.
  `BudgetVersionReference` is the exact unmodified five-field producer tuple. The wrapper carries no amount,
  currency, period, unit, rounding, `BudgetLine`, certification payload or selected-baseline truth.
- MOD-0117 owns wrapper cardinality `InvestmentCase -> SelectedBudgetVersionReference: 0..1`, persistence,
  attach/replace/remove behavior and every lifecycle gate. MOD-0136 only owns producer validation and current
  `FundingBaselineSelection` truth; accepting PPM context does not transfer InvestmentCase ownership.

Validation mode is an exact closed value:

| Mode | MOD-0136 meaning |
|---|---|
| `HistoricalResolve` | Resolve an immutable certified BudgetVersion already referenced in history. A retired version remains historically resolvable when visible and not quarantined; success never authorizes a new PPM relationship. |
| `NewReferenceEligibility` | Decide whether the exact five-field tuple is currently eligible for a new PPM wrapper relationship. Success does not prove it is the current funding baseline. |
| `CurrentSelectionEligibility` | Validate only that the tuple is the current effective MOD-0136 `FundingBaselineSelection` for the exact `InvestmentCaseContextV1`. It is not general referenceability and does not transfer selection truth to PPM. |

Exact compact serialization fixtures (property names and nesting are case-sensitive; an extra or missing field
is malformed caller input) are:

```json
{"ContractName":"ppm.investment-case-context","ContractVersion":"1.0","InvestmentCaseId":"11111111-1111-1111-1111-111111111111"}
```

```json
{"ContractName":"ppm.investment-case-selected-budget-version-reference","ContractVersion":"1.0","InvestmentCaseContext":{"ContractName":"ppm.investment-case-context","ContractVersion":"1.0","InvestmentCaseId":"11111111-1111-1111-1111-111111111111"},"BudgetVersionReference":{"ContractName":"fpa.budget-version-reference","ContractVersion":"1.0","BudgetId":"22222222-2222-2222-2222-222222222222","BudgetVersionId":"33333333-3333-3333-3333-333333333333","BudgetVersionNumber":7}}
```

The bilateral adapter sends the exact producer tuple, exact context when the requested mode requires it, and
one of the three exact mode tokens above; normative request/response envelopes and transport remain a
ready-for-dev decision. Supported versions are exact allowlists: the PPM context and wrapper accept only `1.0`,
and this producer contract accepts only `fpa.budget-version-reference` `1.0`. No trimming, case normalization,
alias, minor-version coercion or best-effort downgrade is permitted. An unknown or unsupported context, wrapper,
producer contract or authoritative response version fails closed with `503`; malformed caller fields or mode
fail with `400`.

| Bilateral validation condition | Exact result |
|---|---:|
| Malformed caller field set, shape or mode | `400` |
| Authentication or trusted server-context failure | `401` |
| Permission or dedicated S2S entitlement failure | `403` |
| Missing, cross-tenant, invisible or otherwise non-disclosable reference | `404` |
| Visible but retired/ineligible for `NewReferenceEligibility`, or stale/not-current for `CurrentSelectionEligibility` | `409` |
| Unknown/unsupported version, timeout, provider unavailable or malformed authoritative response | `503` |

A visible retired version can therefore succeed only for `HistoricalResolve`; it returns `409` for a new
relationship or current-selection claim when that requested eligibility is false. A single `IsReferenceable`
boolean is never sufficient bilateral authority, and failure classes must not be collapsed or inferred locally.

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
| `DefaultCurrencyReference` | `CurrencyReferenceV1` | Yes | Construction-only exact `fpa.currency-reference` / `1.0` default |
| `DefaultFiscalCalendarReference` | `BudgetFiscalCalendarReferenceV1` | Yes | Construction-only exact budgeting-calendar default |
| `UnitSemantics` | closed enum | Yes | Construction-only `Units`, `Thousands` or `Millions` default |

These Budget-level currency, calendar and unit values are construction defaults only. Certification
captures every applicable value explicitly in the immutable snapshot. Later default changes cannot alter,
reinterpret or re-hash a certified version.

### BudgetVersionDraft / working revision

Mutable construction contains candidate version fields, periods and lines. It has no certification state,
certification provenance or authoritative `ContentHash`. An abandoned draft may be soft-deleted under policy.
Certification consumes an exact draft fence and atomically creates the immutable snapshot, provenance and hash.

Its exact user-editable header fields are `RevisionLabel`, `BudgetFiscalCalendarReference`, `CurrencyReference`,
`UnitSemantics`, `FundingBaselineType`, `EffectiveFrom` and `Notes`. `RoundingPolicyV1` is not user-selectable.

### CurrencyReferenceV1 and BudgetFiscalCalendarReferenceV1

- `CurrencyReferenceV1` has exactly `ContractName`, `ContractVersion`, `AlphabeticCode`, with exact identity
  `fpa.currency-reference` / `1.0`. `AlphabeticCode` is exactly three uppercase ASCII ISO-4217 letters. There is
  no trimming, case normalization, symbol or free-text alternative. MOD-0136 validates against a versioned
  allowlist: an unknown code is `400`; validator/catalog unavailability is `503`.
- `BudgetFiscalCalendarReferenceV1` has exactly `ContractName`, `ContractVersion`,
  `BudgetFiscalCalendarId`, with exact identity `fpa.budget-fiscal-calendar-reference` / `1.0`.
- A v1 `BudgetFiscalCalendar` uses an IANA tenant timezone, `FiscalYearStartMonth` in `1..12`, and exactly 12
  non-overlapping monthly immutable period definitions. Boundaries are `LocalDate`; end is exclusive. Arbitrary,
  weekly and quarter-only calendars are deferred. Closed periods do not block historical reads; certification
  requires an authoritative calendar/version that still deterministically produces all 12 periods.

### Immutable BudgetVersion

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `BudgetId` | Guid | Yes | Same-tenant Budget; immutable |
| `BudgetVersionNumber` | long | Yes | Server-assigned monotonic token; unique `(TenantId, BudgetId, BudgetVersionNumber)` |
| `RevisionLabel` | string | Yes | Trim + NFC, max 120; descriptive only, not identity |
| `BudgetFiscalCalendarReference` | `BudgetFiscalCalendarReferenceV1` | Yes | Exact authoritative budgeting calendar identity |
| `CurrencyReference` | `CurrencyReferenceV1` | Yes | One exact currency for the whole version |
| `UnitSemantics` | closed enum | Yes | Exactly `Units`, `Thousands` or `Millions`; whole-version semantic scale |
| `RoundingPolicy` | system constant | Yes | `Scale = 4`, `Midpoint = ToEven`; captured in certified snapshot, not user input |
| `FundingBaselineType` | closed enum | Yes | `Original`, `Reforecast` or `Contingency`; neither approval nor selection truth |
| `EffectiveFrom` | LocalDate | Yes | Interpreted against the selected calendar's IANA tenant timezone |
| `Notes` | string? | No | Trim + NFC, max 2000; must not contain approval decisions |
| `CertifiedAtUtc` / `CertifiedByActorId` | server values | Yes | Created atomically with snapshot; never populated later |
| `ApprovalOutcomeReference` | typed immutable reference? | Conditional | Separate MOD-0023 prerequisite when policy requires; not certification state |
| `ContentHashAlgorithm` | constant | Yes | Exact `SHA-256` |
| `ContentCanonicalizationVersion` | closed identity | Yes | Exact `fpa.budget-content-canonical-json.v1` |
| `ContentHash` | lowercase hex string | Yes | Created atomically over canonical snapshot; immutable |

### BudgetPeriod

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `BudgetPeriodId` | Guid | Yes | Snapshot-local identity; excluded from hash serialization |
| `BudgetVersionId` | Guid | Yes | Same immutable aggregate and tenant |
| `PeriodReference` | canonical reference | Yes | Deterministically generated from the authoritative version-level fiscal calendar; unique within version and not user-editable |
| `Order` | int | Yes | System-generated deterministic non-negative ordering; unique within version and not user-editable |
| `LocalStartDate` / `LocalEndDateExclusive` | LocalDate | Yes | Copied from authoritative definition; monthly, non-overlapping |

### BudgetLine

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `BudgetVersionId` | Guid | Yes | Same aggregate and tenant |
| `PeriodReference` | string | Yes | Exact server-generated reference to one of the snapshot's 12 periods |
| `LineKey` | string | Yes | Trim + NFC, 1..128; case-sensitive ordinal identity |
| `LineType` | closed enum | Yes | Exactly `Base`, `Adjustment` or `Credit` |
| `Amount` | decimal | Yes | BSON Decimal128 / CLR decimal, precision max 28, persisted scale exactly 4 |

Currency and units are inherited from the version and are not line input; mixed currency, mixed units and FX
conversion are forbidden in v1. `Base` requires `Amount >= 0`, `Adjustment` may be signed, `Credit` requires
`Amount <= 0`, and zero is valid. Unique identity is `(TenantId, BudgetVersionDraftId/VersionId,
PeriodReference, normalized LineKey, LineType)`. Breakdown dimensions and arbitrary JSON maps are deferred and
forbidden in v1. Binary floating point is forbidden. Input scale above four is rejected with `400`, never
silently rounded. Each line's absolute bound is `1,000,000,000,000,000.0000`.

### FundingBaselineSelectionOccurrence

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `FundingContextReference` | `InvestmentCaseContextV1` for Gate I-B | Yes | Exact PPM-owned `ppm.investment-case-context` `1.0`; only ContractName, ContractVersion, InvestmentCaseId |
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

`fpa.budget-content-canonical-json.v1` is exact compact JSON encoded as UTF-8 without BOM or trailing newline.
All strings are NFC; UUIDs use lowercase canonical D; LocalDate is `yyyy-MM-dd`; contract identities are exact.
Properties use this closed order: `ContractName`, `ContractVersion`, `BudgetId`, `BudgetVersionNumber`,
`RevisionLabel`, `CurrencyReference` (`ContractName`, `ContractVersion`, `AlphabeticCode`),
`BudgetFiscalCalendarReference` (`ContractName`, `ContractVersion`, `BudgetFiscalCalendarId`), `UnitSemantics`,
`RoundingPolicy` (`Scale`, `Midpoint`), `FundingBaselineType`, `EffectiveFrom`, `Notes`,
`ApprovalOutcomeReference`, `Periods`, `Lines`. `ApprovalOutcomeReference` is either JSON `null`, or the exact
separately ratified immutable MOD-0023 tuple; certification fails closed until that tuple is known when policy
requires it. A period property order is `PeriodReference`, `Order`, `LocalStartDate`,
`LocalEndDateExclusive`; a line property order is `PeriodReference`, `LineKey`, `LineType`, `Amount`.

Periods sort by calendar `Order`. Lines sort by period order, then NFC `LineKey` ordinal, then `LineType`
ordinal token. Amount is a JSON string with exactly four decimal digits, no exponent, grouping or locale form.
Database/child IDs, `TenantId`, audit timestamps, technical `Version` and derived totals are excluded. Derived
totals are recomputed with checked decimal and normalized to scale four using `MidpointRounding.ToEven` before
certification; `VersionTotal` is not authoritative input. Included content is the semantic references, calendar,
currency, units, system rounding policy, baseline type, effective date, notes, optional approval input, periods
and lines. SHA-256 output is lowercase 64 hex.

Normative minimal positive canonical bytes (one zero-valued valid line and 12 generated periods):

```json
{"ContractName":"fpa.budget-content-canonical-json.v1","ContractVersion":"1.0","BudgetId":"11111111-1111-1111-1111-111111111111","BudgetVersionNumber":1,"RevisionLabel":"FY2027","CurrencyReference":{"ContractName":"fpa.currency-reference","ContractVersion":"1.0","AlphabeticCode":"USD"},"BudgetFiscalCalendarReference":{"ContractName":"fpa.budget-fiscal-calendar-reference","ContractVersion":"1.0","BudgetFiscalCalendarId":"22222222-2222-2222-2222-222222222222"},"UnitSemantics":"Units","RoundingPolicy":{"Scale":4,"Midpoint":"ToEven"},"FundingBaselineType":"Original","EffectiveFrom":"2026-07-01","Notes":"","ApprovalOutcomeReference":null,"Periods":[{"PeriodReference":"FY2027-P01","Order":1,"LocalStartDate":"2026-07-01","LocalEndDateExclusive":"2026-08-01"},{"PeriodReference":"FY2027-P02","Order":2,"LocalStartDate":"2026-08-01","LocalEndDateExclusive":"2026-09-01"},{"PeriodReference":"FY2027-P03","Order":3,"LocalStartDate":"2026-09-01","LocalEndDateExclusive":"2026-10-01"},{"PeriodReference":"FY2027-P04","Order":4,"LocalStartDate":"2026-10-01","LocalEndDateExclusive":"2026-11-01"},{"PeriodReference":"FY2027-P05","Order":5,"LocalStartDate":"2026-11-01","LocalEndDateExclusive":"2026-12-01"},{"PeriodReference":"FY2027-P06","Order":6,"LocalStartDate":"2026-12-01","LocalEndDateExclusive":"2027-01-01"},{"PeriodReference":"FY2027-P07","Order":7,"LocalStartDate":"2027-01-01","LocalEndDateExclusive":"2027-02-01"},{"PeriodReference":"FY2027-P08","Order":8,"LocalStartDate":"2027-02-01","LocalEndDateExclusive":"2027-03-01"},{"PeriodReference":"FY2027-P09","Order":9,"LocalStartDate":"2027-03-01","LocalEndDateExclusive":"2027-04-01"},{"PeriodReference":"FY2027-P10","Order":10,"LocalStartDate":"2027-04-01","LocalEndDateExclusive":"2027-05-01"},{"PeriodReference":"FY2027-P11","Order":11,"LocalStartDate":"2027-05-01","LocalEndDateExclusive":"2027-06-01"},{"PeriodReference":"FY2027-P12","Order":12,"LocalStartDate":"2027-06-01","LocalEndDateExclusive":"2027-07-01"}],"Lines":[{"PeriodReference":"FY2027-P01","LineKey":"Revenue","LineType":"Base","Amount":"0.0000"}]}
```

SHA-256: `2262b02bc94923998e72e797f4e3996246e38b69aaa34349b119a111b919be13`.

Normative multi-line canonical bytes (including the displayed NFC `é`) are:

```json
{"ContractName":"fpa.budget-content-canonical-json.v1","ContractVersion":"1.0","BudgetId":"11111111-1111-1111-1111-111111111111","BudgetVersionNumber":2,"RevisionLabel":"Réforecast","CurrencyReference":{"ContractName":"fpa.currency-reference","ContractVersion":"1.0","AlphabeticCode":"USD"},"BudgetFiscalCalendarReference":{"ContractName":"fpa.budget-fiscal-calendar-reference","ContractVersion":"1.0","BudgetFiscalCalendarId":"22222222-2222-2222-2222-222222222222"},"UnitSemantics":"Units","RoundingPolicy":{"Scale":4,"Midpoint":"ToEven"},"FundingBaselineType":"Reforecast","EffectiveFrom":"2026-07-01","Notes":"Board case","ApprovalOutcomeReference":null,"Periods":[{"PeriodReference":"FY2027-P01","Order":1,"LocalStartDate":"2026-07-01","LocalEndDateExclusive":"2026-08-01"},{"PeriodReference":"FY2027-P02","Order":2,"LocalStartDate":"2026-08-01","LocalEndDateExclusive":"2026-09-01"},{"PeriodReference":"FY2027-P03","Order":3,"LocalStartDate":"2026-09-01","LocalEndDateExclusive":"2026-10-01"},{"PeriodReference":"FY2027-P04","Order":4,"LocalStartDate":"2026-10-01","LocalEndDateExclusive":"2026-11-01"},{"PeriodReference":"FY2027-P05","Order":5,"LocalStartDate":"2026-11-01","LocalEndDateExclusive":"2026-12-01"},{"PeriodReference":"FY2027-P06","Order":6,"LocalStartDate":"2026-12-01","LocalEndDateExclusive":"2027-01-01"},{"PeriodReference":"FY2027-P07","Order":7,"LocalStartDate":"2027-01-01","LocalEndDateExclusive":"2027-02-01"},{"PeriodReference":"FY2027-P08","Order":8,"LocalStartDate":"2027-02-01","LocalEndDateExclusive":"2027-03-01"},{"PeriodReference":"FY2027-P09","Order":9,"LocalStartDate":"2027-03-01","LocalEndDateExclusive":"2027-04-01"},{"PeriodReference":"FY2027-P10","Order":10,"LocalStartDate":"2027-04-01","LocalEndDateExclusive":"2027-05-01"},{"PeriodReference":"FY2027-P11","Order":11,"LocalStartDate":"2027-05-01","LocalEndDateExclusive":"2027-06-01"},{"PeriodReference":"FY2027-P12","Order":12,"LocalStartDate":"2027-06-01","LocalEndDateExclusive":"2027-07-01"}],"Lines":[{"PeriodReference":"FY2027-P01","LineKey":"Revenue","LineType":"Base","Amount":"1000.0000"},{"PeriodReference":"FY2027-P01","LineKey":"Revenue","LineType":"Adjustment","Amount":"-25.1250"},{"PeriodReference":"FY2027-P02","LineKey":"Credit","LineType":"Credit","Amount":"-10.0000"}]}
```

SHA-256: `2e0c437ca89d23d2602f8b2320d158e59d924284761ed6d8d4529a05a319fceb`.
Normative tests compare both complete closed-schema byte sequences before hashes. Property-reordered input is canonicalized to this
order; NFD strings are NFC-normalized; numeric/short/exponent decimal input is invalid (`400`) rather than
silently reformatted; unsorted lines canonicalize to the mandated sort. Extra/missing properties fail closed.

## 5. Repo Scope

### This draft authoring task

- `execution/domains/enterprise-strategy-business-performance/module-packs/MOD-0136-budgeting.md` only.

### Future implementation scope (non-authoritative until promotion)

- Primary five-layer module scope is exactly:
  - `services/Diten.FpaService/src/Diten.FpaService.Api/Modules/Budgeting/**`
  - `services/Diten.FpaService/src/Diten.FpaService.Application/Modules/Budgeting/**`
  - `services/Diten.FpaService/src/Diten.FpaService.Domain/Modules/Budgeting/**`
  - `services/Diten.FpaService/src/Diten.FpaService.Persistence/Modules/Budgeting/**`
  - `services/Diten.FpaService/src/Diten.FpaService.Infrastructure/Modules/Budgeting/**`
- Primary test scope is exactly:
  - `services/Diten.FpaService/tests/Diten.FpaService.UnitTests/Modules/Budgeting/**`
  - `services/Diten.FpaService/tests/Diten.FpaService.IntegrationTests/Modules/Budgeting/**`
  - `services/Diten.FpaService/tests/Diten.FpaService.ArchitectureTests/Modules/Budgeting/**`
  - `services/Diten.FpaService/tests/Diten.FpaService.ContractTests/Modules/Budgeting/**`
- If and only if any mandatory primary isolation test fails, the complete `Diten.FpaService` profile is
  rejected and the coordinated fallback activates both `Diten.BudgetingService` and
  `Diten.ScenarioPlanningService`. This pack's fallback-owned five-layer scope is exactly:
  - `services/Diten.BudgetingService/src/Diten.BudgetingService.Api/**`
  - `services/Diten.BudgetingService/src/Diten.BudgetingService.Application/**`
  - `services/Diten.BudgetingService/src/Diten.BudgetingService.Domain/**`
  - `services/Diten.BudgetingService/src/Diten.BudgetingService.Persistence/**`
  - `services/Diten.BudgetingService/src/Diten.BudgetingService.Infrastructure/**`
- Fallback test scope is exactly:
  - `services/Diten.BudgetingService/tests/Diten.BudgetingService.UnitTests/**`
  - `services/Diten.BudgetingService/tests/Diten.BudgetingService.IntegrationTests/**`
  - `services/Diten.BudgetingService/tests/Diten.BudgetingService.ArchitectureTests/**`
  - `services/Diten.BudgetingService/tests/Diten.BudgetingService.ContractTests/**`
- The partner `Diten.ScenarioPlanningService` paths are owned by MOD-0138 and are not writable through this
  pack. Nevertheless, coordinated fallback selection is invalid unless that service is selected with
  `Diten.BudgetingService`; partial fallback, hybrid, mixed or dual activation is prohibited.
- The single authoritative persistence-owner manifest path is
  `services/Diten.FpaService/src/Diten.FpaService.Persistence/Modules/Budgeting/Ownership/BudgetingPersistenceOwnershipManifest.cs`.
  It contains only the exact closed entries in Section 8 and cannot share an entry with ScenarioPlanning. If
  mandatory isolation failure selects coordinated fallback, a separately authorized topology cutover relocates
  this same closed manifest to
  `services/Diten.BudgetingService/src/Diten.BudgetingService.Persistence/Ownership/BudgetingPersistenceOwnershipManifest.cs`
  while the primary manifest is absent. Both paths can never be authoritative or deployed together.
- `frontend/Diten.Web` tenant-shell Budget and BudgetVersion paths only after explicit frontend authority.
- A separately authorized Gateway route task owned by `integration-agent` only after service/port selection.
- A separately approved MOD-0117 consumer amendment only after this producer contract is approved and matching
  producer runtime evidence exists; before then no consumer adapter may be DI-registered, configured, deployed
  or otherwise executable.

## 6. Protected Paths

- `.antigravity/**`.
- `services/**`, `frontend/**` and `gateway/**` during this draft authoring task.
- `gateway/Diten.ApiGateway/**/ocelot.json` in all module-author work; integration-agent only.
- Other domain governance, especially `execution/domains/portfolio-delivery/**` and MOD-0117.
- `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**` and
  `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- Existing `Diten.EnterpriseStrategyService` budget-like fields; they are evidence/quarantine only.
- MOD-0138 ScenarioPlanning domain, repositories, collections, indexes, DI registrations, Mongo sessions and
  transactions; no direct persistence access is permitted in either direction.
- MOD-0023 workflow/approval, MOD-0072 OutcomeTracking/actual outcome and accounting/payment internals.

## 7. Dependencies

| Dependency | Purpose | Current gate |
|---|---|---|
| DCP-002 / Blueprint | Canonical MOD-0136 identity and name | Exact preflight PASS on 2026-08-02 |
| DCP-006 / MOD-0117 | Gate I-B orchestration and PPM consumer profiles | PPM v1 profile ledger closed at checkpoint `a2c7ff5bbfd3ef2b4e3c8c15eab804db58d62520`; bilateral runtime remains unauthorized |
| MOD-0018-FU16 | S2S identity, delegated actor/request binding and permission provisioning | Exact MOD-0136 owner manifest closed; runtime provisioning/evidence required |
| MOD-0021 / MOD-0035 | Immutable audit intake and signed event transport | Exact MOD-0136 mapping/onboarding inputs closed; runtime consumer/key/broker evidence required |
| MOD-0023 | Optional authoritative approval outcome | Separate typed contract; no local approval state |
| Currency allowlist provider | Validate exact `CurrencyReferenceV1` at new certification/selection | Provider implementation/SLA is runtime-blocked; financial shape is closed |
| MOD-0136 BudgetFiscalCalendar | Budgeting-only monthly calendar and period identity | Financial semantics closed; persistence/API remains runtime-blocked |
| MongoDB replica set | Multi-document transaction and idempotency | Required before runtime promotion |

### Gate I onboarding reconciliation — exact governance manifest

This owner-authored manifest closes MOD-0136's inputs to the governance contracts proven at FU16 checkpoint
`40e38905`, MOD-0021 checkpoint `de81a135` and MOD-0035 checkpoint `23f290ec`. It remains non-executable:
catalog registration, role grants, credentials, signing keys, routes, adapters, consumers and deployment require
separately approved implementation authority and evidence.

#### Service principal and manifest separation

| Field | Exact MOD-0136 value |
|---|---|
| Service | `Diten.FpaService` |
| Audience | `diten-fpa-service` |
| Client ID | `diten.fpa` |
| Owner module | `MOD-0136` |
| Protocol scope | `diten.s2s.delegated.invoke` |
| Operation namespace | `budgeting.*` only |

`diten.fpa` identifies the shared FPA workload and grants no permission or module entitlement. Budgeting and
ScenarioPlanning manifests are unconditionally separate: MOD-0136 owns only the 15 `budgeting.*` entries below;
MOD-0138 must provide its own owner manifest, `scenario-planning.*` operation IDs, permissions, event identity,
signing identity and fixture. Neither manifest may contain, alias, wildcard, infer, merge or provision the other's
entries. Registration of one module's manifest never satisfies or activates the other module's Gate I profile.

#### Exact operation-to-permission manifest — 15 entries

Operation IDs and permission keys are exact, ordinal and case-sensitive. Wildcard, prefix match, trim, case fold,
alias, fallback and one permission satisfying another operation are forbidden.

| Exact `operation_id` | Exact required permission |
|---|---|
| `budgeting.budgets.read` | `budgeting.budgets.read` |
| `budgeting.budgets.create` | `budgeting.budgets.create` |
| `budgeting.budgets.update` | `budgeting.budgets.update` |
| `budgeting.budgets.archive` | `budgeting.budgets.archive` |
| `budgeting.budget-version-drafts.read` | `budgeting.budget-version-drafts.read` |
| `budgeting.budget-version-drafts.create` | `budgeting.budget-version-drafts.create` |
| `budgeting.budget-version-drafts.update` | `budgeting.budget-version-drafts.update` |
| `budgeting.budget-version-drafts.abandon` | `budgeting.budget-version-drafts.abandon` |
| `budgeting.budget-versions.read` | `budgeting.budget-versions.read` |
| `budgeting.budget-versions.certify` | `budgeting.budget-versions.certify` |
| `budgeting.budget-versions.retire` | `budgeting.budget-versions.retire` |
| `budgeting.funding-baseline-selections.read` | `budgeting.funding-baseline-selections.read` |
| `budgeting.funding-baseline-selections.replace` | `budgeting.funding-baseline-selections.replace` |
| `budgeting.funding-baseline-selections.close` | `budgeting.funding-baseline-selections.close` |
| `budgeting.budget-version-references.validate` | `budgeting.budget-version-references.validate` |

The manifest registers catalog definitions only. It creates no role grant. Effective access independently requires
the MOD-0136 module entitlement when applicable, an explicit current tenant role grant for the delegated actor,
an active exact service principal/credential, exact audience/scope and the operation's exact permission.

#### FU16 delegated actor and request binding

Every protected S2S call uses FU16 `DelegatedActorProofV1`. The receiver requires exact `iss = diten-auth-service`,
`aud = diten-fpa-service`, `client_id = azp = diten.fpa`, `actor_type = service`, one server-matched `tenant_id`,
one non-empty `delegated_actor_id` with `delegated_actor_type = tenant_user`, one exact operation ID above and
its exact permission. `request_hash` is the FU16 base64url SHA-256 binding of canonical method, canonical path,
tenant, operation and body digest. `jti` plus `nonce` is atomically single-use; delegation, tenant-grant,
service-principal and credential-generation versions must be current. Tenant, actor, operation, method, path or
body mismatch, replay, wrong audience/client/scope or stale proof fails before business lookup. Service identity
alone, a human bearer token alone, catalog presence, entitlement alone or a permission claim without a current
explicit grant is never sufficient.

#### MOD-0021 audit mapping and compatibility fixture

- Event type: `BudgetingAuditIntentSubmittedV1`.
- EventName/routing key: `fpa.budgeting-audit-intent.submitted.v1`.
- EventVersion: numeric `1`, which must match the `.v1` suffix.
- Producer: exact ordinal `Diten.FpaService`.
- Producer module: exact `MOD-0136`.
- Payload profile: **Minimal Mutation Audit v1**, with exactly the six lower-camel-case fields
  `auditIntentId`, `actorId`, `entityType`, `entityId`, `mutation`, `occurredAtUtc` in that canonical order.
- `auditIntentId` is a non-empty immutable GUID and equals envelope `EventId`; `actorId` is the effective
  authenticated/delegated actor; `occurredAtUtc` is UTC and equals envelope `OccurredAtUtc`.
- `TenantId`, `CorrelationId`, `CausationId`, `Producer`, module, authorization and delegation evidence are trusted
  envelope/transport metadata and are forbidden as payload fields. Extra/missing/renamed/case-changed fields,
  full entities, before/after snapshots, financial values, permission inventories and unrestricted maps reject.

The MOD-0021 allowlist and projection are exact:

| Payload `entityType` | Allowed payload `mutation` | MOD-0021 entity projection | MOD-0021 operation projection |
|---|---|---|---|
| `Budget` | `created` | `Budgeting.Budget` | `AuditOperation.Create` |
| `Budget` | `updated` | `Budgeting.Budget` | `AuditOperation.Update` |
| `Budget` | `archived` | `Budgeting.Budget` | `AuditOperation.Deactivate` |
| `BudgetFiscalCalendar` | `created` | `Budgeting.BudgetFiscalCalendar` | `AuditOperation.Create` |
| `BudgetFiscalCalendar` | `updated` | `Budgeting.BudgetFiscalCalendar` | `AuditOperation.Update` |
| `BudgetFiscalCalendar` | `archived` | `Budgeting.BudgetFiscalCalendar` | `AuditOperation.Deactivate` |
| `BudgetVersionDraft` | `created` | `Budgeting.BudgetVersionDraft` | `AuditOperation.Create` |
| `BudgetVersionDraft` | `updated` | `Budgeting.BudgetVersionDraft` | `AuditOperation.Update` |
| `BudgetVersionDraft` | `abandoned` | `Budgeting.BudgetVersionDraft` | `AuditOperation.Deactivate` |
| `BudgetVersion` | `certified` | `Budgeting.BudgetVersion` | `AuditOperation.Activate` |
| `BudgetVersion` | `retired` | `Budgeting.BudgetVersion` | `AuditOperation.Deactivate` |
| `BudgetVersion` | `superseded` | `Budgeting.BudgetVersion` | `AuditOperation.Deactivate` |
| `FundingBaselineSelection` | `replaced` | `Budgeting.FundingBaselineSelection` | `AuditOperation.Update` |
| `FundingBaselineSelection` | `closed` | `Budgeting.FundingBaselineSelection` | `AuditOperation.Deactivate` |

Every row projects to existing `AuditCategory.System`, `SourceModule = MOD-0136`, tenant from trusted envelope,
effective `ActorId` from the six-field payload and delegated provenance from verified FU16 transport metadata.
This pack does not authorize an `AuditCategory` enum change. Any unknown producer, module, event identity/version,
entity/mutation pair, property or metadata disagreement is terminal and creates no audit projection.

The producer-owned compact compatibility fixture is exact:

```json
{"auditIntentId":"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa","actorId":"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb","entityType":"BudgetVersion","entityId":"cccccccc-cccc-4ccc-8ccc-cccccccccccc","mutation":"certified","occurredAtUtc":"2026-08-04T09:30:00.0000000Z"}
```

Its expected projection is `AuditCategory.System`, `AuditOperation.Activate`, entity type
`Budgeting.BudgetVersion`, entity ID `cccccccc-cccc-4ccc-8ccc-cccccccccccc`, source module `MOD-0136`, effective
actor `bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb` and occurrence time `2026-08-04T09:30:00.0000000Z`. Envelope
`EventId` is `aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa`; a different payload `auditIntentId`, occurrence time,
producer/module or tenant is a terminal mismatch. MOD-0021 idempotency remains exact
`ConsumerName + EventId`: identical canonical bytes are a duplicate no-op; changed bytes are a terminal conflict.

#### MOD-0035 signed-producer onboarding

The exact non-wildcard signed allowlist tuple is:

| Field | Exact value |
|---|---|
| Producer | `Diten.FpaService` |
| EventName | `fpa.budgeting-audit-intent.submitted.v1` |
| EventVersion | `1` |
| Signing scheme | `fpa-budgeting-event-hmac-sha256.v1` |
| Signing identity | `diten.fpa.budgeting.audit` |

The signing identity, active/previous key IDs, key namespace and derived material are Budgeting-specific and must
never equal or reuse PPM or ScenarioPlanning material. The onboarding fixture binds the exact immutable shared
event envelope, the six-field canonical UTF-8 payload bytes and byte length, EventId, EventName, EventVersion,
TenantId, CorrelationId, CausationId, Producer, OccurredAtUtc, signing scheme, signing identity, key ID and expected
signature. A separately approved executable onboarding slice must add concrete active/previous key IDs, bounded
overlap deadline, freshness/future-skew values, revocation authority, fixture digest and signature generated from
provisioned secret material; this draft neither invents nor exposes secrets and remains deny-by-default until those
runtime values and the MOD-0035 verification matrix pass. New producers emit only
`Diten.BuildingBlocks.Eventing.EventTransportMessage`; the legacy Platform URN is inbound compatibility only.

#### Atomic producer boundary and publication

Each successful MOD-0136 mutation writes the business state, idempotency receipt, one producer-local audit intent
and one producer-local event-outbox row in the same Budgeting-owned Mongo transaction. Any participant failure
rolls back all four results. No Platform/MOD-0021 collection or network/broker participant may join that transaction.
Only after commit may a Budgeting-owned dispatcher read `budgeting_event_outbox` and invoke MOD-0035's public
`IEventBus`; handlers never call `IEventBus`, RabbitMQ or MassTransit directly. Post-commit failure leaves the
durable row retryable and cannot roll back or re-run the business mutation.

#### PPM BudgetVersion bilateral fixture and adapter boundary

The exact bilateral fixtures remain the `InvestmentCaseContextV1` and
`SelectedBudgetVersionReferenceV1` compact JSON values in Section 3, nesting the exact unmodified five-field
`BudgetVersionReferenceV1`. Contract tests must preserve the three exact validation modes and
`400/401/403/404/409/503` mapping, prove no copied amount/currency/period/unit/rounding/line/certification/selection
truth and prove MOD-0117's `0..1` wrapper/cardinality/lifecycle ownership. Until this draft is promoted, the
producer endpoint and security/audit evidence pass, a matching MOD-0117 amendment is approved and explicit
runtime activation authority is recorded, every MOD-0117 BudgetVersion adapter type, DI registration,
configuration, background worker, route and feature flag remains absent or hard-disabled. A fixture is contract
evidence only and cannot make an adapter executable.

## 8. Runtime Constraints

- Data is tenant-owned. `TenantId` and `ActorId` come only from authenticated server context. Cross-tenant
  reads/validation return `404`; an authenticated actor lacking permission receives `403`.
- Client-supplied `TenantId` or `ActorId` is rejected with `400`. Every aggregate, child, selection, audit
  intent, idempotency and quarantine record carries server-assigned `TenantId`; repositories require it as a
  non-optional argument and every query, mutation and index predicate is tenant-scoped.
- Certified BudgetVersion content is immutable. Mutation attempts and stale selection/version
  conflicts return `409` and never partially update the aggregate.
- Malformed currency/calendar/reference shapes, decimal overflow and disallowed scale return `400`. An unavailable
  authoritative provider when validation is required, or unknown/unsupported contract version, returns `503`.
  Existing certified versions remain historically readable during provider outage and are never reinterpreted;
  cached results cannot authorize new certification or selection.
- Version creation, period/line persistence, selected-baseline uniqueness, outbox/audit intent and idempotency
  record use a Mongo replica-set transaction. Domain mutation without its audit intent must roll back.
- Every mutating request follows the exact idempotency key, payload-hash, receipt, replay and unknown-commit
  decision below. Same canonical key/same payload returns the committed result; same key/different payload is
  `409`; indeterminate receipt reconciliation is `503`. V1 receipts have no TTL or reuse window.
- Uncertified abandoned staging may be soft-deleted under policy, but has no automatic hard purge. Certified,
  periods, lines, selection history and hashes have no TTL, CRUD delete or bulk delete. Selected, referenced or historically
  effective versions cannot be ordinary CRUD-deleted: append-only retirement/supersession preserves queryable
  financial and selection history. V1 has no exceptional purge; legal hold blocks deletion/soft deletion, and
  immutable history cannot be purged without a future MOD-0030-approved amendment. PPM history is never rewritten.
- Certification requires an active Budget, current non-abandoned draft fence, exact authoritative currency and
  fiscal-calendar references, 12 generated periods, at least one valid unique line, no orphan period/line,
  successful canonical bytes/hash, and idempotency receipt plus audit intent in the same transaction. When policy
  requires approval, the exact immutable MOD-0023 `ApprovalOutcomeReference` is required; otherwise no local
  Approved state is created. Certifier needs `budgeting.budget-versions.certify` and must differ from both draft
  creator and latest updater; there is no bypass.
- Checked decimal is used throughout. Calculated intermediates normalize to scale four with ToEven only before
  certification. Line absolute bound is `1,000,000,000,000,000.0000`; version aggregate absolute bound is
  `1,000,000,000,000,000,000.0000`; overflow stops certification with `400`.
- No service scaffold/runtime instance, physical collection/index, route, event, transport or port is created by
  this draft; service and persistence literals are governance-only future composition decisions.
- Gate I-B validation is mode-specific: `HistoricalResolve` can resolve visible retired certified history;
  `NewReferenceEligibility` cannot infer current selection; `CurrentSelectionEligibility` consults only the
  immutable MOD-0136 selection occurrence/event truth for exact `InvestmentCaseContextV1`. PPM wrapper storage
  is never used as selection authority.
- Selection accepts only a certified, visible, non-retired version that passes `NewReferenceEligibility`;
  uncertified versions are never selectable. `EffectiveFromUtc` is the server commit time: future scheduling
  and backdating are forbidden in v1. There is exactly one current occurrence per tenant plus exact
  `InvestmentCaseContextV1`. Replace appends prior disposition, successor occurrence and audit intent in one
  transaction; close appends prior closure and audit intent. Both use the existing exact replace/close
  permissions, while selection validation uses `budgeting.budget-version-references.validate`; no new
  validation permission is introduced.

### Budgeting Core persistence-owner manifest — exact v1

The manifest is the only place allowed to register a Budgeting repository, collection, index initializer or
Mongo transaction participant. Dynamic collection names, generic foreign collection access and collection
registration outside this manifest fail architecture tests. Its exact entries are:

| Owner entry | Exact collection | Repository / writer ownership |
|---|---|---|
| `Budgets` | `budgeting_budgets` | `IBudgetRepository` / `BudgetRepository` |
| `FiscalCalendars` | `budgeting_fiscal_calendars` | `IBudgetFiscalCalendarRepository` / `BudgetFiscalCalendarRepository` |
| `BudgetVersionDrafts` | `budgeting_budget_version_drafts` | `IBudgetVersionDraftRepository` / `BudgetVersionDraftRepository`; draft candidate periods/lines are embedded |
| `BudgetVersions` | `budgeting_budget_versions` | `IBudgetVersionRepository` / `BudgetVersionRepository`; immutable certified header/snapshot root |
| `BudgetVersionDispositions` | `budgeting_budget_version_dispositions` | same repository; immutable retirement/supersession disposition append |
| `BudgetPeriods` | `budgeting_budget_periods` | `IBudgetPeriodRepository` / `BudgetPeriodRepository`; certification writer only |
| `BudgetLines` | `budgeting_budget_lines` | `IBudgetLineRepository` / `BudgetLineRepository`; certification writer only |
| `FundingBaselineSelectionOccurrences` | `budgeting_funding_baseline_selection_occurrences` | `IFundingBaselineSelectionRepository`; immutable occurrence append |
| `FundingBaselineSelectionDispositions` | `budgeting_funding_baseline_selection_dispositions` | same repository; immutable closure/replacement disposition append |
| `FundingBaselineSelectionFences` | `budgeting_funding_baseline_selection_fences` | same repository; context-scoped CAS/uniqueness fence, not an alternate selection SoR |
| `IdempotencyReceipts` | `budgeting_idempotency_receipts` | `IBudgetingIdempotencyReceiptStore` |
| `AuditIntents` | `budgeting_audit_intents` | `IBudgetingAuditIntentWriter`; producer-local only |
| `EventOutbox` | `budgeting_event_outbox` | `IBudgetingOutboxWriter`; producer-local post-commit dispatch source |
| `MigrationQuarantine` | `budgeting_migration_quarantine` | `IBudgetingMigrationQuarantineWriter`; inactive until a separately approved migration slice |

No entry is shared with or accessible to ScenarioPlanning. Budgeting code cannot resolve a ScenarioPlanning
repository, database context, collection handle, session or transaction participant; ScenarioPlanning has the
same zero-access boundary toward every collection above.

### Tenant-first named index matrix — exact v1

All keys use the listed order. Every index begins with `TenantId`; `1` is ascending and `-1` descending.
`IsDeleted: false` partial filters apply only where stated. No TTL index exists.

| Exact index name | Collection | Ordered keys | Uniqueness / filter / purpose |
|---|---|---|---|
| `ux_budgeting_budgets_tenant_code_active_v1` | `budgeting_budgets` | `TenantId:1, Code:1` | unique; partial `IsDeleted=false` |
| `ix_budgeting_budgets_tenant_list_v1` | `budgeting_budgets` | `TenantId:1, IsDeleted:1, UpdatedAt:-1, Id:1` | list/detail support |
| `ux_budgeting_fiscal_calendars_tenant_id_v1` | `budgeting_fiscal_calendars` | `TenantId:1, Id:1` | unique tenant-qualified identity |
| `ix_budgeting_fiscal_calendars_tenant_list_v1` | `budgeting_fiscal_calendars` | `TenantId:1, IsDeleted:1, UpdatedAt:-1, Id:1` | active-list support |
| `ux_budgeting_budget_version_drafts_tenant_id_v1` | `budgeting_budget_version_drafts` | `TenantId:1, Id:1` | unique tenant-qualified draft identity |
| `ix_budgeting_budget_version_drafts_tenant_budget_v1` | `budgeting_budget_version_drafts` | `TenantId:1, BudgetId:1, IsDeleted:1, UpdatedAt:-1, Id:1` | budget draft list/fence lookup |
| `ux_budgeting_budget_versions_tenant_budget_number_v1` | `budgeting_budget_versions` | `TenantId:1, BudgetId:1, BudgetVersionNumber:1` | unique immutable business ordinal |
| `ux_budgeting_budget_versions_tenant_id_v1` | `budgeting_budget_versions` | `TenantId:1, Id:1` | unique referenceability lookup |
| `ux_budgeting_budget_version_dispositions_tenant_id_v1` | `budgeting_budget_version_dispositions` | `TenantId:1, Id:1` | unique immutable disposition identity |
| `ix_budgeting_budget_version_dispositions_tenant_version_time_v1` | `budgeting_budget_version_dispositions` | `TenantId:1, BudgetVersionId:1, DisposedAtUtc:-1, Id:1` | lifecycle history/eligibility lookup |
| `ux_budgeting_budget_periods_tenant_version_reference_v1` | `budgeting_budget_periods` | `TenantId:1, BudgetVersionId:1, PeriodReference:1` | unique period identity in snapshot |
| `ux_budgeting_budget_periods_tenant_version_order_v1` | `budgeting_budget_periods` | `TenantId:1, BudgetVersionId:1, Order:1` | unique deterministic order |
| `ux_budgeting_budget_lines_tenant_version_period_key_type_v1` | `budgeting_budget_lines` | `TenantId:1, BudgetVersionId:1, PeriodReference:1, LineKey:1, LineType:1` | unique closed line identity |
| `ix_budgeting_budget_lines_tenant_version_order_v1` | `budgeting_budget_lines` | `TenantId:1, BudgetVersionId:1, PeriodReference:1, LineKey:1, LineType:1, Id:1` | deterministic snapshot read |
| `ux_budgeting_selection_occurrences_tenant_id_v1` | `budgeting_funding_baseline_selection_occurrences` | `TenantId:1, Id:1` | unique immutable occurrence identity |
| `ix_budgeting_selection_occurrences_tenant_context_time_v1` | `budgeting_funding_baseline_selection_occurrences` | `TenantId:1, FundingContext.ContractName:1, FundingContext.ContractVersion:1, FundingContext.InvestmentCaseId:1, EffectiveFromUtc:-1, Id:1` | context history derivation |
| `ux_budgeting_selection_dispositions_tenant_occurrence_v1` | `budgeting_funding_baseline_selection_dispositions` | `TenantId:1, OccurrenceId:1` | unique terminal closure/replacement per occurrence |
| `ix_budgeting_selection_dispositions_tenant_context_time_v1` | `budgeting_funding_baseline_selection_dispositions` | `TenantId:1, FundingContext.ContractName:1, FundingContext.ContractVersion:1, FundingContext.InvestmentCaseId:1, DisposedAtUtc:-1, Id:1` | context history derivation |
| `ux_budgeting_selection_fences_tenant_context_v1` | `budgeting_funding_baseline_selection_fences` | `TenantId:1, FundingContext.ContractName:1, FundingContext.ContractVersion:1, FundingContext.InvestmentCaseId:1` | unique current-selection CAS fence |
| `ux_budgeting_idempotency_tenant_actor_operation_key_v1` | `budgeting_idempotency_receipts` | `TenantId:1, ActorId:1, Operation:1, CanonicalKey:1` | unique replay scope; no TTL |
| `ix_budgeting_idempotency_tenant_created_v1` | `budgeting_idempotency_receipts` | `TenantId:1, CreatedAt:-1, Id:1` | operational reconciliation; no TTL |
| `ux_budgeting_audit_intents_tenant_id_v1` | `budgeting_audit_intents` | `TenantId:1, Id:1` | unique local audit intent |
| `ix_budgeting_audit_intents_tenant_delivery_v1` | `budgeting_audit_intents` | `TenantId:1, DeliveryState:1, CreatedAt:1, Id:1` | delivery/reconciliation scan; no TTL |
| `ux_budgeting_event_outbox_tenant_id_v1` | `budgeting_event_outbox` | `TenantId:1, Id:1` | unique local outbox record |
| `ix_budgeting_event_outbox_tenant_delivery_v1` | `budgeting_event_outbox` | `TenantId:1, DeliveryState:1, NextAttemptAtUtc:1, CreatedAt:1, Id:1` | post-commit dispatcher scan; no TTL |
| `ux_budgeting_migration_quarantine_tenant_source_v1` | `budgeting_migration_quarantine` | `TenantId:1, SourceSystem:1, SourceCollection:1, SourceDocumentId:1, MappingVersion:1` | unique quarantine provenance; no TTL |

Index creation is idempotent but fail-closed: an existing name with a different key, uniqueness or partial
filter blocks startup/review promotion. Collection/index names are exact lowercase literals and cannot be
prefixed, normalized, aliased or reused by ScenarioPlanning.

### Transaction composition — exact v1

Every command set below uses one Budgeting-owned Mongo replica-set session/transaction. It includes the exact
business writes plus one `budgeting_idempotency_receipts` record, one local `budgeting_audit_intents` record and
one local `budgeting_event_outbox` record. Failure of any participant aborts the entire transaction.

| Transaction set | Exact business participants | Boundary |
|---|---|---|
| `BudgetIdentityMutationV1` | `budgeting_budgets` | create/update/archive one Budget under optimistic fence; no version/draft/selection write |
| `BudgetFiscalCalendarMutationV1` | `budgeting_fiscal_calendars` | create/update/archive calendar according to referenceability rules; no certified snapshot rewrite |
| `BudgetVersionDraftMutationV1` | `budgeting_budget_version_drafts` | create/update/abandon one draft including embedded candidate periods/lines; no certified collection write |
| `BudgetVersionCertificationV1` | `budgeting_budget_version_drafts`, `budgeting_budget_versions`, `budgeting_budget_periods`, `budgeting_budget_lines` | CAS-consume exact draft fence; atomically allocate monotonic version number and publish immutable certified snapshot/children; publication means certification, not approval |
| `FundingBaselineSelectionReplaceV1` | `budgeting_funding_baseline_selection_fences`, `budgeting_funding_baseline_selection_dispositions`, `budgeting_funding_baseline_selection_occurrences` | CAS current fence; append prior replacement disposition and successor occurrence; update fence pointer; old occurrence remains immutable |
| `FundingBaselineSelectionCloseV1` | `budgeting_funding_baseline_selection_fences`, `budgeting_funding_baseline_selection_dispositions` | CAS current fence; append closure and clear fence pointer; old occurrence remains immutable |
| `BudgetVersionRetireV1` | `budgeting_budget_version_dispositions` | CAS append immutable retirement/supersession disposition; `budgeting_budget_versions` and certified content/hash/period/line bytes never change |

No transaction may enlist ScenarioPlanning, Platform, PPM or EnterpriseStrategy collections, repositories,
sessions or contexts. Provider calls occur before opening the transaction; their responses are pinned into the
validated command. A transaction never remains open across network or broker I/O.

### Idempotency and unknown-commit decision — exact v1

- Every mutating command requires `Idempotency-Key` with 1..128 ASCII characters from
  `[A-Za-z0-9._:-]`. Empty, non-ASCII, whitespace, leading/trailing whitespace or any other character is `400`.
  The canonical key is the exact case-sensitive accepted byte sequence; no trim, case fold, Unicode
  normalization, aliasing or server-generated fallback occurs.
- Receipt identity is exact `(TenantId, ActorId, Operation, CanonicalKey)`. `TenantId` and effective `ActorId`
  are server-derived; `Operation` is the closed command token named by the transaction set.
- `PayloadHash` is lowercase SHA-256 of the command's exact versioned canonical request bytes after server-side
  defaults are resolved but excluding tenant, actor, correlation, retry count and the idempotency key. Each
  command contract must publish a golden canonical-byte/hash fixture before implementation.
- The receipt stores `PayloadHash`, transaction/operation identity, stable resource/result identity, safe
  response code/body hash, `CommittedAtUtc` and correlation/causation provenance in the same transaction as the
  mutation, audit intent and outbox.
- Same receipt identity plus the same `PayloadHash` returns the stored stable result without re-running domain,
  provider or broker work. Same receipt identity plus a different `PayloadHash` returns `409` with no mutation.
- On Mongo `UnknownTransactionCommitResult`, retry **commit only** on the same session/transaction according to
  the bounded driver policy; never re-run the transaction body. After commit-only retries are exhausted,
  reconcile by the exact receipt identity and payload hash. A committed matching receipt returns the stored
  result; a conflicting receipt returns `409`; absent, unreadable or indeterminate receipt state returns `503`
  and never guesses success or replays the body.
- V1 receipts have no TTL, expiry, purge, reuse window or delete endpoint. Operational archival/purge requires
  a future approved compatibility and retention amendment.

### Audit/outbox and post-commit delivery — exact composition

- Every successful mutation writes its producer-local audit intent and local outbox record atomically with the
  business mutation and idempotency receipt. Missing audit/outbox composition is a transaction failure.
- Broker publication begins only after commit, from `budgeting_event_outbox`, through MOD-0035's public
  `IEventBus`. At-least-once retry, duplicate-safe consumer behavior and delivery-state reconciliation apply.
- Direct RabbitMQ client, direct MassTransit publish/send/consume integration, direct Platform audit/event/outbox
  collection access and cross-service distributed transactions are prohibited.
- Exact audit event identity/schema, retention and downstream deployment remain open B-class decisions. Their
  absence blocks runtime promotion but does not change the local atomic composition fixed here.

### Provider outage and initial migration boundary

- Currency and BudgetFiscalCalendar providers are called before certification or a new selection when that
  operation requires current eligibility. Definitive same-version invalid input is `400`; timeout, unavailable,
  malformed response or unknown/unsupported provider contract version is `503`. There is no stale-cache,
  local-default or fail-open authorization. The transaction is never opened after an indeterminate response.
- Historical reads/resolution of already certified local immutable snapshots continue without a provider call;
  provider outage never rewrites or reinterprets stored currency/calendar/period/hash truth.
- Initial migration is **NO-OP / quarantine-only**. No startup import, backfill, dual-read, dual-write, shadow
  write, fallback query or authoritative mapping from EnterpriseStrategy occurs. The quarantine collection is
  reserved but receives writes only under a separately approved migration slice with explicit provenance.
- Ambiguous/invalid legacy evidence remains outside Budgeting SoR and cannot become certified, selected or
  referenceable. Cutover, reconciliation and rollback remain open B-class migration work.

### Service topology decision

The authoritative primary is the isolated `Modules/Budgeting` module in `Diten.FpaService`. Co-location with
ScenarioPlanning is physical hosting only: Budgeting shares no Domain type, repository, Mongo collection,
index, DI registration, session or transaction with ScenarioPlanning and cannot access ScenarioPlanning
persistence directly. Communication is only through an approved typed/versioned Application contract that
leaks no entity, repository, Mongo/session handle or persistence DTO.

The fallback is one coordinated profile containing separate five-layer `Diten.BudgetingService` and
`Diten.ScenarioPlanningService` services. It is eligible only when one or more mandatory `Diten.FpaService`
isolation-matrix tests fail; any such failure rejects the complete primary profile. Primary and coordinated
fallback are exclusive: partial fallback is invalid, and primary/fallback components cannot be scaffolded,
registered, deployed or run as a hybrid, mixed or dual activation. The fallback cannot be selected for
convenience. This pack owns only the Budgeting fallback member; MOD-0138 owns the ScenarioPlanning member.
`Diten.EnterpriseStrategyService` placement is prohibited; its budget-like fields remain migration/quarantine
evidence only. MOD-0072 `OutcomeTracking` belongs to the separate
`Diten.DecisionIntelligenceService` and cannot be hosted in `Diten.FpaService` or share Budgeting persistence.

This placement is fixed by the [ESBP domain topology](../domain-config.md#authoritative-physical-topology), but
creates no scaffold, runtime, route, controller, port, Gateway, collection or deployment authority. Port remains
`TBD` because no authoritative collision-free allocation exists.

## 9. Layout & Shell Contract

- Proposed shell: `tenant`; every future Razor page explicitly sets `Layout = "_LayoutTenantShell";`.
- Proposed view paths: `Views/FPandA/Budgets/**` and `Views/FPandA/BudgetVersions/**`; no ASP.NET `Areas/`.
- Budget surface field count: **6** (`Code`, `Name`, `Description`, `DefaultCurrencyReference`,
  `DefaultFiscalCalendarReference`, `UnitSemantics`) → Golden Slim.
- BudgetVersionDraft combined field count: **11**. Header inputs are `RevisionLabel`,
  `BudgetFiscalCalendarReference`, `CurrencyReference`, `UnitSemantics`, `FundingBaselineType`, `EffectiveFrom`,
  `Notes` (7). The line editor adds `PeriodReference`, `LineKey`, `LineType`, `Amount` (4) → Golden Compact.
  Child calendar identities and
  `BudgetPeriod.Order` are deterministically generated from the authoritative fiscal calendar selected by the
  version-level reference; users select a generated period for a line but do not create/edit child definitions. Route-derived
  `BudgetId` and server fields `BudgetVersionNumber`, certification, hash, tenant, actor and audit are excluded.
- Parent pack frontmatter selects `compact` and `form_field_count: 11` because the minimum primary
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

- Budget (6 fields, Slim candidate): `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`,
  `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, marker class, scripts and seven-language RESX.
- BudgetVersionDraft construction (11 fields, Compact) and certified-version read-only detail: `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`,
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
| CurrencyReference | Yes | Exact `fpa.currency-reference` / `1.0`; three uppercase ASCII ISO-4217 letters | Versioned allowlist; unknown `400`, unavailable `503` |
| BudgetFiscalCalendarReference | Yes | Exact `fpa.budget-fiscal-calendar-reference` / `1.0`; exact three-field shape | Authoritative budgeting calendar validation |
| BudgetFiscalCalendar | Yes | Monthly, 12 periods, IANA timezone, start month 1..12, LocalDate end-exclusive, no overlap | Deterministic generated definitions |
| UnitSemantics | Yes | `Units`, `Thousands`, `Millions` | One version value; no line input/mixing |
| RoundingPolicy | Server | Exact scale 4 / ToEven system constant | Captured in snapshot; not user-selectable |
| BudgetVersionNumber | Server | Positive, monotonic, immutable | Unique tenant/budget index + transaction |
| Draft fence | Mutation | Exact current technical token | Stale construction/certification returns 409 |
| ContentHash | Certification | SHA-256 + `fpa.budget-content-canonical-json.v1` | Seal-time recomputation + normative vectors |
| EffectiveFrom | Yes | LocalDate compatible with generated fiscal year | Calendar compatibility validation |
| Amount | Yes | Decimal128/CLR decimal; precision max 28, persisted/display scale 4; input extra scale `400` | Checked arithmetic and exact line/aggregate bounds |
| LineType/sign | Yes | `Base >= 0`; `Adjustment` signed; `Credit <= 0`; zero accepted | Domain invariant |
| LineKey | Yes | Trim + NFC, 1..128, case-sensitive ordinal | Unique tenant/version/period/key/type index |
| FundingContextReference | Gate I-B selection | Exact `InvestmentCaseContextV1`: `ppm.investment-case-context` / `1.0`, fields ContractName, ContractVersion, InvestmentCaseId only | No free-text discriminator; PPM owns context/lifecycle |
| Selection | Conditional | One effective occurrence per tenant + typed funding context, derived from immutable occurrence and closure/disposition history | Fence + uniqueness enforcement + transaction |
| Selection effective time | Selection | Occurrence start and event timestamps are server-derived UTC; no occurrence is edited in place | No overlap per tenant + context |
| ContractName/Version | Yes | Exact, case-sensitive v1 allowlists for producer, PPM context and wrapper; no coercion | Malformed caller shape `400`; unknown/unsupported authoritative version `503` |
| TenantId / ActorId | No client field | Server context only | Client-supplied value rejected |
| IdempotencyKey | Mutations | Exact case-sensitive 1..128 ASCII `[A-Za-z0-9._:-]`; no trim/normalization | Unique tenant/actor/operation/key receipt + canonical payload hash; no TTL |

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
- Unknown commit: bounded same-session commit-only retry, then exact receipt reconciliation; matching committed
  receipt returns the stored result, conflict is `409`, and absent/unreadable/indeterminate state is `503` with
  no transaction-body replay.
- Audit/outbox write failure: entire domain transaction rolls back.
- MOD-0117 consumer attempts currency/period/unit/rounding/amount/line snapshot: architecture/contract test fails.
- Provider outage: existing certified history remains readable, but no cached result authorizes certification/selection.
- Bilateral mode/error fixtures: malformed shape/mode `400`; auth/context `401`; permission/S2S `403`;
  missing/cross-tenant/invisible `404`; visible retired/ineligible/stale/not-current for the requested eligibility
  `409`; unknown/unsupported version, timeout, unavailable provider or malformed authoritative response `503`.
- Historical retired resolution succeeds when visible and not quarantined, but the same tuple fails a new/current
  eligibility request with `409` when ineligible/not current; no mode result is reused as another mode's proof.
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
- `CurrentSelectionEligibility` uses the existing exact
  `budgeting.budget-version-references.validate` permission; v1 defines no additional selection-validation key.
- Cross-tenant lookup is `404`; authenticated insufficient permission is `403`; unauthenticated is `401`.
- Returned errors use stable safe codes and expose no Mongo index, stack, provider or cross-tenant existence
  detail. Rejected operations create neither a domain mutation nor an audit intent.

## 15. Gateway / API Routing Decision

Decision: **deferred and currently prohibited**. Primary service identity is `Diten.FpaService`, but runtime
activation and port are unauthorized and the port remains `TBD`; this draft allocates neither. Frontend must
eventually call Gateway port 5000 through the approved public profile, never a service port. After explicit
runtime authority and authoritative port allocation, an integration-agent-only task must determine explicit
public upstream/downstream routes (including `OPTIONS`) and test auth, tenant and correlation headers. Internal
S2S providers receive no browser/Gateway route. This pack does not modify `ocelot.json`.

## 16. Acceptance Criteria

- [ ] MOD-0136 owns Budget, draft construction, immutable certified BudgetVersion, certification and context-scoped selection; MOD-0117 owns PPM cardinality/lifecycle/storage.
- [ ] Certification atomically seals snapshot, provenance and versioned SHA-256 hash; correction creates a new ID/number and disposition history is append-only.
- [ ] Selection is unique per tenant + typed funding context under concurrency and is derived from immutable
  occurrence plus immutable closure/disposition history; replacement edits no old occurrence and is hash-independent.
- [ ] `BudgetVersionReferenceV1` uses exact ContractName/version and validates referenceability authoritatively.
- [ ] Exact `InvestmentCaseContextV1` and `SelectedBudgetVersionReferenceV1` serialization round-trip without
  renaming/coercion; the wrapper nests only exact context plus the exact five-field opaque producer tuple and
  contains no currency, period, unit, rounding, amount, line, certification or selected-baseline truth.
- [ ] `HistoricalResolve`, `NewReferenceEligibility` and `CurrentSelectionEligibility` remain semantically
  separate; Current consults only MOD-0136 selection truth, while MOD-0117 retains `0..1` storage/lifecycle.
- [ ] Bilateral fixtures prove exact `400/401/403/404/409/503` mapping and exact supported-version behavior;
  unknown/unsupported authoritative versions and malformed authoritative responses fail closed with `503`.
- [ ] `Selected`, `Certified` and MOD-0023 `Approved` remain distinct in domain, API, UI and localization.
- [ ] Tenant/actor values are server-side; cross-tenant returns 404, missing permission 403, immutable conflict
  409 and unavailable/unknown contract dependency 503.
- [ ] Decimal128/CLR decimal precision 28, persisted/display scale four, ToEven certification normalization,
  exact bounds, currency/calendar shapes, closed unit/line enums and sign rules are enforced and tested.
- [ ] Selection permits only certified, visible, non-retired, newly eligible versions; server commit time is the
  effective time, future/backdated selection is rejected, and replace/close preserve exactly one current
  occurrence using the existing exact permissions.
- [ ] Mutation, audit intent, selected uniqueness and idempotency are atomic in a Mongo replica-set transaction.
- [ ] The single Budgeting persistence-owner manifest contains exactly the 14 closed collection/repository
  entries and exact tenant-first named-index matrix; duplicate names, drift, TTL or ScenarioPlanning access fail.
- [ ] Budget identity/calendar, draft, certification/publication, selection replace/close and retirement use only
  their exact transaction sets plus receipt, audit intent and local outbox; no provider/broker/foreign participant
  is enlisted.
- [ ] Idempotency enforces exact case-sensitive ASCII canonical keys and canonical payload hashes; same
  key/payload replays, different payload returns `409`, unknown commit uses commit-only retry plus receipt
  reconciliation, indeterminate state returns `503`, and v1 receipts have no TTL.
- [ ] Broker delivery is post-commit through MOD-0035 `IEventBus`; direct RabbitMQ, MassTransit and Platform
  collection access are absent. Initial migration is NO-OP/quarantine-only with no dual-read/write.
- [ ] Budget field count 6 selects Slim; BudgetVersionDraft combined field count 11 selects Compact because
  calendar definitions/order are server-generated and only a line's generated `PeriodReference` is selected; all tenant pages explicitly
  use `_LayoutTenantShell` and DataTables pass `verify_datatable_page.py` plus `quality-gate-datatable`.
- [ ] Seven-language RESX parity and browser smoke pass without approval UI, PPM budget forms or hardcoded lookup fallback.
- [ ] Prototype/migration candidates remain quarantined unless an explicit mapping, reconciliation, rollback and
  post-migration verification plan proves canonical identity and immutable content.
- [ ] Primary topology passes the applicable domain matrix `ESBP-TOP-001` through `ESBP-TOP-013`, including
  profile exclusivity, five-layer namespace, repository/DI, collection/index, transaction, typed-contract,
  EnterpriseStrategy prohibition, S2S, audit/outbox and PPM non-executability proofs. Any primary isolation
  failure rejects the complete `Diten.FpaService` profile and selects the coordinated
  `Diten.BudgetingService` plus `Diten.ScenarioPlanningService` fallback; partial/hybrid/mixed/dual activation fails.
- [ ] FU16 onboarding uses exact audience `diten-fpa-service`, client ID `diten.fpa`, owner `MOD-0136`, protocol
  scope and the exact 15-entry operation-to-permission manifest; the MOD-0138 manifest remains separate.
- [ ] Every protected S2S operation validates FU16 delegated actor, tenant, operation, permission, request hash,
  replay and freshness bindings independently; service identity or entitlement alone never authorizes access.
- [ ] `BudgetingAuditIntentSubmittedV1` / `fpa.budgeting-audit-intent.submitted.v1` carries exactly the six-field
  Minimal Mutation Audit v1 payload and passes the closed producer/entity/mutation mapping fixture.
- [ ] The MOD-0035 exact signed allowlist tuple, Budgeting-specific signing identity, immutable-byte binding,
  rotation/revocation/freshness and PPM/ScenarioPlanning credential-isolation tests pass.
- [ ] Producer mutation, receipt, audit intent and outbox are atomic locally; publish is post-commit through
  `IEventBus` only, and MOD-0021 inbox plus audit outbox is consumer-local and idempotent.
- [ ] The PPM BudgetVersion bilateral fixture passes unchanged while every MOD-0117 adapter remains
  non-executable until all producer, consumer and explicit activation gates pass.

## 17. Test Expectations

- Domain/unit: draft-to-atomic-seal, immutable certified versions, revision numbering, immutable occurrence plus
  closure/disposition-derived effective selection history,
  certification-vs-selection-vs-approval, defaults-vs-snapshot, rounding/negative rules and hash vectors.
- Financial matrix: exact scale-four input rejection, checked-intermediate ToEven normalization, line/aggregate
  bounds, all `LineType` sign/zero cases, ordinal line uniqueness, single currency/unit inheritance, mixed/FX
  rejection, 12 monthly LocalDate periods, exclusive/non-overlap boundaries and closed-period historical reads.
- Certification: reject inactive Budget, stale/abandoned draft, unavailable/invalid currency or calendar,
  incomplete 12-period generation, zero/orphan/duplicate/invalid lines, hash failure and missing required approval;
  reject creator/latest-updater as certifier and prove no bypass. Successful certification atomically persists
  snapshot, receipt and audit intent; correction produces a new draft, ID and monotonic version number.
- Integration: concurrent version creation and selection, Decimal128 precision/scale/bounds, exact currency/calendar
  validation, tenant isolation, cross-tenant 404, permission 403, immutable 409, dependency/contract 503.
- Transaction: replica-set commit/rollback across aggregate, idempotency and audit intent; injected audit failure
  rolls back the appended closure/disposition event, successor occurrence and audit intent, leaving the prior
  effective truth intact with no old-occurrence mutation or partial version/selection.
- Persistence manifest/index: exact 14 collection entries, exact repository/writer ownership, every named
  tenant-first index key/order/uniqueness/filter, no name/spec collision, no TTL, no dynamic/foreign collection
  registration and zero ScenarioPlanning access.
- Transaction composition: independently fault every participant in each exact transaction set; assert atomic
  rollback of business state, receipt, audit intent and outbox, no network/broker call inside the transaction,
  immutable draft/certified/occurrence history and selection-fence CAS uniqueness.
- Idempotency/unknown commit: accepted/rejected key charset and case sensitivity, canonical payload golden
  fixtures, same-key replay without handler/provider execution, different-payload `409`, bounded same-session
  commit-only retry, matching receipt reconciliation and absent/indeterminate receipt `503`; prove no TTL index.
- Audit/outbox: audit/outbox persistence failure rolls back the mutation; dispatcher cannot publish pre-commit,
  publishes only through `IEventBus`, and direct RabbitMQ/MassTransit/Platform collection references fail the
  architecture scan.
- Gate I authorization: exact FU16 audience/client/owner/scope and all 15 operation-permission pairs; reject
  cross-module ScenarioPlanning entries, wildcard/alias/case changes, service-only or entitlement-only calls,
  stale grants/credentials, wrong tenant/actor/operation/request hash and replayed `jti`/nonce.
- Gate I audit mapping: exact event type/name/version/producer/module, six-field canonical fixture, all closed
  entity/mutation pairs and expected MOD-0021 projections; reject extra/missing/reordered canonical fields,
  unknown pairs, metadata mismatch, duplicate-same bytes versus duplicate-changed bytes and missing delegated provenance.
- Gate I signed eventing: exact MOD-0035 allowlist tuple, Budgeting-only signing identity/key namespace,
  immutable shared-URN envelope/payload binding, active/previous overlap, expiry, revocation, freshness/skew,
  fixed-time verification, retry/terminal/DLQ/cancellation and unchanged PPM signed-event regression.
- Provider/migration: currency/calendar definitive-invalid `400` versus unavailable/malformed/version `503`,
  historical local reads during outage, no transaction after indeterminate validation, startup migration NO-OP,
  quarantine-only authorized writes and negative proof of dual-read, dual-write, shadow or fallback query.
- Contract: exact five-field producer tuple plus exact PPM context/wrapper serialization and round-trip; reject
  missing/extra/renamed/case-changed fields and financial/certification/selection payload copying; exercise every
  validation mode, exact status mapping, supported `1.0`, unknown/unsupported versions, S2S/delegated actor,
  historical retired resolution and new-reference-versus-current-selection independence.
- Provider resilience: outage does not block historical immutable reads or reinterpret stored bytes/hash; cached
  provider data cannot authorize a new certification or selection.
- Concurrency/mutation: simultaneous version numbers and selections, fence loss, immutable closure/disposition
  plus successor append atomicity, selected uniqueness, rollback invariance and unchanged certified hash;
  mutations verify idempotent replay.
- Architecture/isolation: execute the Budgeting-applicable
  [ESBP topology matrix](../domain-config.md#architecture-and-isolation-test-matrix), including primary XOR
  fallback activation; separate five-layer namespaces; Budgeting-only repository/DI resolution and owned
  collection/index manifest; no ScenarioPlanning repository, collection, index, context, session or transaction
  access; Mongo driver only in Budgeting Persistence; typed/versioned Application-contract-only communication;
  no MOD-0072/OutcomeTracking co-hosting or persistence sharing; no EnterpriseStrategy project/runtime/SoR
  dependency; internal S2S has no browser/Gateway route; audit/outbox rollback and post-commit delivery; PPM
  no-copy and pre-evidence adapter non-executability; guessed port/credential/deployment rejection.
- Migration: classify/map/quarantine legacy EnterpriseStrategy budget evidence; collisions, precision loss,
  missing currency/period or unverifiable immutable token stay quarantined; rollback and counts reconcile.
- Migration quarantine: preserve source collection/document ID, tenant provenance, schema/mapping version,
  source hash, run ID and reason code; deterministic reruns create no duplicates. Ambiguous/missing tenant,
  currency, period, unit, dimension, total or negative-value semantics are never defaulted or silently rounded.
  Migrated candidates remain non-referenceable and unselected until explicit owner-reviewed reconciliation.
- Archive/retention: abandoned staging soft delete; certified/selected/referenced/history records reject CRUD
  delete, TTL, bulk-delete and exceptional purge and remain queryable after append-only retirement/supersession;
  legal hold blocks draft deletion, MOD-0030-approved future amendment gates immutable-history purge, and PPM
  history is never rewritten.
- Frontend (after authority): explicit tenant layout, Slim/Compact file sets and counts, DataTable verifier,
  `quality-gate-datatable`, seven-language RESX parity, same-origin/Gateway browser smoke, no approval/PPM forms.
- Build (after topology): selected service, frontend and gateway builds PASS; no port/Gateway allocation is
  inferred before approval.

## 18. Ready-for-dev Checklist

- [x] FP&A Control Tower closed the v1 ownership, lifecycle, version/certification, selection and financial semantics on 2026-08-03.
- [x] Closed v1 financial semantics, exact normative canonical bytes/hashes and negative invariants are fixed as
  implementation/test inputs without local widening; executable implementation proof belongs to review.
- [x] Primary `Diten.FpaService/Modules/Budgeting`, coordinated conditional
  `Diten.BudgetingService + Diten.ScenarioPlanningService` fallback, complete-primary rejection, exclusivity,
  exact logical repo/test scopes and EnterpriseStrategy/OutcomeTracking prohibitions are reconciled to the
  authoritative ESBP domain topology; this records no runtime authority.
- [ ] Concrete service-local mapping for logical `EntityBase` is approved without importing or sharing a foreign base.
- [x] The single Budgeting persistence-owner manifest path, its exact 14 collection/repository entries and the
  exact tenant-first named-index matrix are closed without collision, TTL or ScenarioPlanning ownership.
- [x] The applicable `ESBP-TOP-001` through `ESBP-TOP-013` design projection names unit, integration,
  architecture and contract test scopes, test plans, negative invariants and protected paths. Scaffold/code/live
  dependencies are not required to PASS before `ready-for-dev`.
- [x] Currency/BudgetFiscalCalendar fail-closed outage boundary and pre-transaction validation ordering are exact;
  provider implementation, credentials and SLA remain runtime-promotion evidence.
- [ ] `BudgetVersionReferenceV1` producer contract is approved; PPM's closed `InvestmentCaseContextV1` and
  `SelectedBudgetVersionReferenceV1` profiles have exact bilateral serialization/mode/error fixtures approved,
  while a separately authorized executable consumer slice remains gated.
- [ ] Gate I-B funding context is exact `ppm.investment-case-context` `1.0`; CurrentSelectionEligibility fixtures
  use it without free-text kind or any transfer of InvestmentCase ownership to MOD-0136.
- [x] SHA-256 implementation plan and contract-test projection pin the approved exact UTF-8 bytes and both
  normative hashes; executable reproduction evidence belongs to review.
- [x] Draft soft-deletion and immutable-history no-purge/legal-hold design, negative invariants and test plan are
  fixed; executable persistence evidence belongs to review.
- [x] Golden Reference Compact and Budget Slim projections are fixed; field counts remain 11 and 6, with child
  periods deterministically generated from the authoritative version-level fiscal calendar. Implemented UI
  conformance belongs to review and requires separate frontend authority.
- [x] Layout, backend convention, frontend file contract and field validation design are complete for authorized
  future slices; implemented file/browser evidence belongs to review.
- [x] The owner-authored 15-entry operation-to-permission manifest, exact `diten-fpa-service` audience,
  `diten.fpa` client ID, `MOD-0136` ownership and strict separation from MOD-0138 are closed without wildcard,
  alias or normalization; live catalog/grants and credentials remain review/runtime-promotion evidence.
- [x] FU16 delegated actor/request binding is reconciled for exact tenant, actor, operation, permission,
  request hash, replay and freshness checks; executable AuthService/producer evidence remains a later gate.
- [x] `BudgetingAuditIntentSubmittedV1` / `fpa.budgeting-audit-intent.submitted.v1`, the exact six-field Minimal
  Mutation Audit v1 payload, closed producer/entity/mutation allowlist and MOD-0021 mapping/fixture are fixed.
- [x] MOD-0035 onboarding inputs fix the exact signed allowlist tuple, Budgeting-specific signing identity,
  immutable-byte/shared-URN boundary and no credential reuse; provisioned key/signature/live-broker evidence remains open.
- [x] The PPM BudgetVersion bilateral fixture and adapter non-executability boundary are explicit; fixture
  presence cannot activate a MOD-0117 adapter before matching producer/consumer approval and runtime authority.
- [x] Exact transaction sets, idempotency canonicalization/hash/receipt/replay, unknown-commit reconciliation,
  no-TTL policy, audit/outbox atomicity and post-commit `IEventBus` boundary have executable test plans.
- [x] Initial migration behavior is NO-OP/quarantine-only with no dual-read/write; any later migration inventory,
  mapping, cutover, reconciliation and rollback requires a separate approval.
- [ ] Port remains `TBD` until an authoritative collision-free allocation exists; any later public Gateway
  decision remains a separate integration-agent task and internal S2S providers receive no browser/Gateway route.
- [x] Acceptance/test projections include build, verifier, RESX, contract, architecture, integration, security
  and browser/live evidence at the gates where their required scaffold/code/runtime exists.
- [ ] User explicitly changes status to `approved` or `ready-for-dev`; that status authorizes only the named
  implementation slice and does not grant production, deployment, route, port or runtime authority.

### Review/runtime-promotion evidence — not ready-for-dev prerequisites

- [ ] The selected profile's architecture, integration, contract and security suites are executable and PASS.
- [ ] If any mandatory primary isolation test fails, evidence shows complete `Diten.FpaService` rejection and
  coordinated activation of both fallback services with no partial, hybrid, mixed or dual profile.
- [ ] Mongo replica-set transaction/rollback, idempotency/unknown-commit, audit/outbox post-commit delivery,
  tenant isolation and concurrency evidence PASS against the implemented Budgeting persistence owner manifest.
- [ ] Dedicated S2S credential, audience/scope, delegated actor, exact permission and supported-version evidence PASS.
- [ ] Budgeting-specific MOD-0035 active/previous key IDs, overlap/freshness/skew, revocation authority, fixture
  digest/signature, consumer mapping and live transport evidence PASS without PPM or ScenarioPlanning key reuse.
- [ ] Approved MOD-0117 producer/consumer runtime fixtures prove no-copy behavior and adapter executability gates.
- [ ] Any authorized public Gateway/browser, deployment, service-discovery, health and live-provider evidence
  PASS; internal S2S remains absent from browser/Gateway routes.
- [ ] Explicit runtime/production promotion authority is recorded separately; `production_authority: none`
  remains until that decision.

## 19. Implementation Notes

- DCP-002 exact preflight command passed on 2026-08-02:
  `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0136 --name "Budgeting"`.
- This is a parent pack, not an implementation slice. `production_authority: none` is deliberate.
- Existing `BudgetEnvelope`, `BudgetAmount`, `FundingSource` and yearly budget fields in
  `Diten.EnterpriseStrategyService` are prototype/migration evidence only. They establish neither ownership nor
  production baseline, cannot host MOD-0136 and must be inventoried and quarantined before any later migration.
- `service: Diten.FpaService` names only the primary governance topology. The conditional fallback and exact
  logical scopes are defined above; neither profile is executable while this pack is `draft` and
  `production_authority: none`.
- Gate I-B consumer profiles are reconciled to PPM checkpoint
  `a2c7ff5bbfd3ef2b4e3c8c15eab804db58d62520`; executable producer/consumer work remains unauthorized until
  their delivery gates, matching producer runtime evidence and explicit runtime authority are separately
  approved. A MOD-0117 adapter cannot be registered or activated beforehand. DCP-006 OD-04 remains OPEN/PARTIAL.
- No runtime literal, collection, permission registration, route, port, event or deployment was created here.

### Change log

| Date | Change | Authority |
|---|---|---|
| 2026-08-03 | Closed the A-class v1 financial semantics: Decimal128/scale/bounds/ToEven, exact currency and budgeting-calendar references, units, line schema/sign rules, certification separation-of-duties, selection timing, canonical JSON with verified normative SHA-256 vectors, immutable retention and field counts 6/11. Status, topology and runtime authority remain unchanged. | User / FP&A Control Tower |
| 2026-08-03 | Reconciled service topology to the authoritative ESBP domain checkpoint: primary isolated `Diten.FpaService/Modules/Budgeting`, conditional fallback, exact future scopes and architecture matrix. Financial semantics, permissions, hash vectors, draft status and production authority remain unchanged. | User / ESBP Control Tower |
| 2026-08-04 | Corrected fallback to the coordinated `Diten.BudgetingService + Diten.ScenarioPlanningService` profile and separated ready-for-dev design/test-plan gates from executable review/runtime evidence. Status and production authority remain unchanged. | User / ESBP Control Tower |
| 2026-08-04 | Closed Budgeting Core composition: exact collections/indexes/owner manifest, transaction sets, idempotency and unknown-commit behavior, atomic audit/outbox with post-commit `IEventBus`, fail-closed providers and NO-OP/quarantine-only initial migration. Runtime evidence, audit event identity, status and production authority remain open/unchanged. | User / FP&A Control Tower |
| 2026-08-04 | Reconciled Gate I onboarding to FU16/MOD-0021/MOD-0035: exact FPA audience/client and separate MOD-0136 manifest, 15 operation-permission pairs, delegated request binding, Budgeting audit identity/six-field mapping/fixture, signed-producer tuple, atomic local outbox, post-commit `IEventBus` and non-executable PPM adapter boundary. Financial decisions, permission keys, normative hashes, draft status and production authority remain unchanged. | User / FP&A Control Tower |

## 20. Follow-up Items

### Closed A-class financial decisions

Money precision/scale/bounds, ToEven normalization, single-currency/no-FX behavior, budgeting-owned monthly
calendar semantics, unit semantics, exact line fields/types/sign rules, certification prerequisites and
separation-of-duties, baseline classifications/selection timing, canonical hash schema/vectors, retention and
UI field counts are closed by this Control Tower decision. They are normative v1 requirements, not open/TBD
items, and implementation may not widen or reinterpret them.

### Remaining B-class promotion blockers

- At review/runtime promotion, implement and pass the Budgeting-applicable `ESBP-TOP-001` through
  `ESBP-TOP-013` matrix for the primary; if any isolation test fails, record complete primary rejection and the
  coordinated selection of both `Diten.BudgetingService` and `Diten.ScenarioPlanningService`, with no partial,
  hybrid, mixed or dual activation. This executable evidence is not a ready-for-dev prerequisite.
- Fix the concrete service-local `EntityBase`; collection/index names, owner manifest entries and logical
  primary/fallback repository/test paths are closed above.
- Provision the closed `diten.fpa` / `diten-fpa-service` S2S profile and FU16 delegated actor/request binding.
- Register and explicitly grant the closed MOD-0136 15-entry permission manifest; do not merge MOD-0138 entries.
- Provision Budgeting-specific signing keys/runtime fixture values and complete MOD-0021/MOD-0035 executable
  consumer, signing, retry/DLQ/replay and live integration evidence for the closed audit event identity/schema.
- Produce executable replica-set transaction, idempotency/unknown-commit and post-commit outbox evidence against
  the closed design; composition itself is no longer open.
- Approve any post-NO-OP migration inventory, mapping, quarantine writes, cutover, rollback and reconciliation slice.
- Reserve an authoritative collision-free port and approve service discovery, deployment/operations and any
  public route separately; port remains `TBD` and internal S2S receives no Gateway/browser route.
- Record explicit runtime authority; until then no service, frontend, Gateway, PPM or WorkCenter implementation
  is executable and `status: draft`, `service: Diten.FpaService`, `production_authority: none` remain unchanged.
