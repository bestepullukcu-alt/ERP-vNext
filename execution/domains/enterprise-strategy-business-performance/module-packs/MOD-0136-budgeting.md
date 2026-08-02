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
form_field_count: 11
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
| Aggregates/entities | `Budget`, `BudgetFiscalCalendar`, immutable `BudgetFiscalCalendarPeriodDefinition`, `BudgetVersionDraft`, immutable certified `BudgetVersion`, `BudgetPeriod`, `BudgetLine`, immutable `FundingBaselineSelectionOccurrence`, immutable `SelectionClosure`, immutable `SelectionDisposition` |
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
| DCP-006 / MOD-0117 | Gate I-B orchestration and PPM consumer profiles | PPM v1 profile ledger closed at checkpoint `a2c7ff5bbfd3ef2b4e3c8c15eab804db58d62520`; bilateral runtime remains unauthorized |
| MOD-0018 | Tenant entitlement, JWT permissions and service identity | Runtime provisioning/evidence required |
| MOD-0021 / MOD-0035 | Immutable audit intent/consumer mechanics | Runtime evidence required; domain mutation and audit intent atomicity mandatory |
| MOD-0023 | Optional authoritative approval outcome | Separate typed contract; no local approval state |
| Currency allowlist provider | Validate exact `CurrencyReferenceV1` at new certification/selection | Provider implementation/SLA is runtime-blocked; financial shape is closed |
| MOD-0136 BudgetFiscalCalendar | Budgeting-only monthly calendar and period identity | Financial semantics closed; persistence/API remains runtime-blocked |
| MongoDB replica set | Multi-document transaction and idempotency | Required before runtime promotion |

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
- Every mutating request carries a bounded idempotency key scoped by tenant, actor, operation and normalized
  payload hash. Same key/same payload replays the original result; same key/different payload returns `409`.
- Idempotency persistence records the canonical request hash, operation, in-progress/completed state and stable
  response in the same transaction. Canonical decimal representation and deterministic line ordering are part
  of hashing; key length/charset, retention/TTL and unknown-commit recovery remain promotion decisions.
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
- No service, collection, route, event, transport or port is allocated by this draft.
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
- [ ] Budget field count 6 selects Slim; BudgetVersionDraft combined field count 11 selects Compact because
  calendar definitions/order are server-generated and only a line's generated `PeriodReference` is selected; all tenant pages explicitly
  use `_LayoutTenantShell` and DataTables pass `verify_datatable_page.py` plus `quality-gate-datatable`.
- [ ] Seven-language RESX parity and browser smoke pass without approval UI, PPM budget forms or hardcoded lookup fallback.
- [ ] Prototype/migration candidates remain quarantined unless an explicit mapping, reconciliation, rollback and
  post-migration verification plan proves canonical identity and immutable content.
- [ ] Topology architecture tests prove repository/type/permission/collection isolation; otherwise Option B is selected.

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
- Contract: exact five-field producer tuple plus exact PPM context/wrapper serialization and round-trip; reject
  missing/extra/renamed/case-changed fields and financial/certification/selection payload copying; exercise every
  validation mode, exact status mapping, supported `1.0`, unknown/unsupported versions, S2S/delegated actor,
  historical retired resolution and new-reference-versus-current-selection independence.
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
  delete, TTL, bulk-delete and exceptional purge and remain queryable after append-only retirement/supersession;
  legal hold blocks draft deletion, MOD-0030-approved future amendment gates immutable-history purge, and PPM
  history is never rewritten.
- Frontend (after authority): explicit tenant layout, Slim/Compact file sets and counts, DataTable verifier,
  `quality-gate-datatable`, seven-language RESX parity, same-origin/Gateway browser smoke, no approval/PPM forms.
- Build (after topology): selected service, frontend and gateway builds PASS; no port/Gateway allocation is
  inferred before approval.

## 18. Ready-for-dev Checklist

- [x] FP&A Control Tower closed the v1 ownership, lifecycle, version/certification, selection and financial semantics on 2026-08-03.
- [ ] Closed v1 financial semantics and normative canonical-hash vectors are implemented without local widening.
- [ ] Service topology is approved; `service`, concrete service-local mapping for logical `EntityBase`, collection ownership and repo scope are concrete without inventing a base type.
- [ ] Option A isolation architecture tests are specified and proven feasible, or Option B fallback is selected.
- [ ] Currency allowlist provider implementation/SLA and BudgetFiscalCalendar persistence/API wiring are authorized.
- [ ] `BudgetVersionReferenceV1` producer contract is approved; PPM's closed `InvestmentCaseContextV1` and
  `SelectedBudgetVersionReferenceV1` profiles have exact bilateral serialization/mode/error fixtures approved,
  while a separately authorized executable consumer slice remains gated.
- [ ] Gate I-B funding context is exact `ppm.investment-case-context` `1.0`; CurrentSelectionEligibility fixtures
  use it without free-text kind or any transfer of InvestmentCase ownership to MOD-0136.
- [ ] SHA-256 implementation reproduces the approved exact UTF-8 bytes and both normative hashes.
- [ ] Draft soft deletion and immutable-history no-purge/legal-hold rules are implemented as specified.
- [ ] Golden Reference Compact live pack/code and Budget Slim pattern are confirmed; field counts remain 11 and 6,
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
- Gate I-B consumer profiles are reconciled to PPM checkpoint
  `a2c7ff5bbfd3ef2b4e3c8c15eab804db58d62520`; executable producer/consumer work remains unauthorized until
  their delivery gates and explicit runtime authority are separately approved. DCP-006 OD-04 remains OPEN/PARTIAL.
- No runtime literal, collection, permission registration, route, port, event or deployment was created here.

### Change log

| Date | Change | Authority |
|---|---|---|
| 2026-08-03 | Closed the A-class v1 financial semantics: Decimal128/scale/bounds/ToEven, exact currency and budgeting-calendar references, units, line schema/sign rules, certification separation-of-duties, selection timing, canonical JSON with verified normative SHA-256 vectors, immutable retention and field counts 6/11. Status, topology and runtime authority remain unchanged. | User / FP&A Control Tower |

## 20. Follow-up Items

### Closed A-class financial decisions

Money precision/scale/bounds, ToEven normalization, single-currency/no-FX behavior, budgeting-owned monthly
calendar semantics, unit semantics, exact line fields/types/sign rules, certification prerequisites and
separation-of-duties, baseline classifications/selection timing, canonical hash schema/vectors, retention and
UI field counts are closed by this Control Tower decision. They are normative v1 requirements, not open/TBD
items, and implementation may not widen or reinterpret them.

### Remaining B-class promotion blockers

- Approve the conditional `Diten.FpaService` isolated Budgeting-module topology and architecture-test fallback
  to `Diten.BudgetingService`.
- Fix exact repository paths, concrete service-local entity base, Mongo collection/index names, routes and port.
- Provision dedicated S2S credentials and delegated actor propagation.
- Complete MOD-0018 permission/entitlement provisioning for the exact 15 keys.
- Complete MOD-0021/MOD-0035 transactional audit-intent/outbox integration evidence.
- Close the replica-set transaction, idempotency receipt, retry and unknown-commit runtime design.
- Approve migration inventory, quarantine, cutover, rollback and reconciliation evidence for prototype data.
- Record explicit runtime authority; until then no service, frontend, Gateway, PPM or WorkCenter implementation
  is executable and `status: draft`, `service: TBD`, `production_authority: none` remain unchanged.
