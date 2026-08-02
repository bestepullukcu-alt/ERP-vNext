---
id: MOD-0138
name: Scenario Planning
domain: enterprise-strategy-business-performance
service: TBD
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: draft
owner: FP&A Scenario Planning Process Owner
branch: feature/esbp/mod-0138-scenario-planning
started: 2026-08-02
target: 2026-08-02
form_field_count: 10
production_authority: none
---

# MOD-0138 — Scenario Planning

> **Draft/non-executable.** This parent pack proposes boundaries and contracts only. It allocates no service,
> collection, port, Gateway route or production authority. `service: TBD` and `production_authority: none` are
> deliberate approval gates.

## 1. Module Summary

- Blueprint logical route: **Enterprise Application Ecosystem → ERP Suite → FP&A / EPM**.
- Institutional owner: **FP&A Scenario Planning Process Owner**.
- Purpose: author immutable scenario versions, run deterministic version-pinned comparisons, expose bounded
  authoritative references, and own audited selected-scenario truth.
- Repository packaging under ESBP does **not** place this module in `Diten.EnterpriseStrategyService`.
- Gate I-B remains `OPEN`: consumer execution requires this producer pack and contract to be approved, a matching
  MOD-0117 consumer amendment to be approved, and explicit runtime authority.

Authorities: [domain config](../domain-config.md), [DCP-006](../../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md),
[master plan](../../../portfolio/master-development-plan.md), [module registry](../../../registries/module-id-registry.md),
and [MOD-0117](../../portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md).

## 2. Ownership and Boundaries

MOD-0138 owns:

- `Scenario`, mutable `ScenarioVersionDraft`, immutable published `ScenarioVersion`, comparator execution/output,
  and their append-only disposition histories.
- Scenario assumptions/reference set and its deterministic normalization.
- Selected-scenario truth for an approved typed scope and its immutable occurrence/disposition history.
- Authoritative current and historical Scenario/Comparator/selection reference validation.

MOD-0138 does not own:

- `Budget` / `BudgetVersion` (MOD-0136).
- `Portfolio` / `InvestmentCase` (MOD-0117).
- Generic `Decision` / `Rationale` (MOD-0007).
- Approval workflow or `ApprovalOutcome` (MOD-0023).
- Actual realization (MOD-0072).
- Task, ApprovalTask, WorkCenter, assignment, SLA, escalation or execution behavior.
- A PPM-local scenario engine, selection truth, or comparator UI.
- Foreign payload snapshots. Foreign inputs remain opaque typed/versioned references.
- `InvestmentCase` reference cardinalities, whether selection is required, PPM lifecycle transitions or PPM
  minimal-reference persistence; MOD-0117 owns these consumer rules. MOD-0138 supplies compatibility fixtures only.

Selection means “chosen analytical scenario”; it is not approval and creates no Approved/Rejected state.

Bilateral ownership is asymmetric and explicit. MOD-0138 owns producer tuple schemas/validation, scenario and
comparator truth, selection occurrence/disposition/effective-current truth, producer audit, supported producer
versions/deprecation and producer fixtures. MOD-0117 owns the four PPM profile identities/fields in §4, wrapper
version policy, `0..n`/`0..n`/`0..1` cardinalities, relationship attach/detach/retire behavior, consumer persistence,
InvestmentCase transitions, adapter behavior and consumer audit. Consuming `InvestmentCaseContextV1` transfers no
InvestmentCase ownership or visibility/lifecycle inference to MOD-0138.

## 3. Owned Objects

| Kind | Proposed object | Draft invariant |
|---|---|---|
| Aggregate | `Scenario` | Created `Active`; only transition is `Active → Archived`; reopen forbidden |
| Staging aggregate | `ScenarioVersionDraft` | At most one open per tenant + Scenario; copy-on-new-draft provenance; abandon may soft-delete |
| Immutable entity | `ScenarioVersion` | Publishing atomically creates/seals it; published content is never edited |
| Value set | `ScenarioAssumptionV1` / `BudgetVersionReferenceSetV1` | Exact closed schemas below; normalized and sealed; no foreign payload copy |
| Aggregate | `ComparatorRunDefinition` | Immutable accepted request pinned to exact inputs, schemas, algorithm and digests |
| Event/receipt | `ComparatorExecutionEvent` | Append-only execution attempt/terminal receipt; terminal history is never rewritten |
| Immutable entity | `ComparatorOutput` | Created only after successful execution; explicit rerun creates new Run/Output IDs |
| Selection ledger | `ScenarioSelectionOccurrence` plus closure/replacement/disposition event | Immutable events; effective current selection is derived, never stored as a mutable flag on history |
| Contract | `fpa.scenario-planning-reference` | Exact `ContractVersion = "1.0"`; name does not also encode v1 |
| Producer Gate I tuples | `ScenarioVersionReferenceV1`, `ComparatorOutputReferenceV1` | Minimal opaque typed/versioned references only |
| Consumed/fixture-only PPM values | `InvestmentCaseContextV1` plus three PPM-owned wrappers in §4 | Not MOD-0138-owned and never extended by this pack |

Proposed permissions are exact, individual, case-sensitive and non-wildcard:

- `fpa.scenario-planning.scenarios.read`
- `fpa.scenario-planning.scenarios.create`
- `fpa.scenario-planning.scenarios.update`
- `fpa.scenario-planning.version-drafts.read`
- `fpa.scenario-planning.version-drafts.create`
- `fpa.scenario-planning.version-drafts.update`
- `fpa.scenario-planning.version-drafts.abandon`
- `fpa.scenario-planning.versions.read`
- `fpa.scenario-planning.versions.publish`
- `fpa.scenario-planning.versions.retire`
- `fpa.scenario-planning.comparators.read`
- `fpa.scenario-planning.comparators.run`
- `fpa.scenario-planning.selections.read`
- `fpa.scenario-planning.selections.replace`
- `fpa.scenario-planning.selections.close`
- `fpa.scenario-planning.references.validate`

No endpoint, route, controller or collection is authorized by this draft.

## 4. Entity Fields

`entity_base: EntityBase` is the tenant-owned **logical** contract required by the module-pack standard. It does
not invent or select a concrete service-local base class while `service: TBD`; that mapping is a promotion blocker.
Business `ScenarioVersionNumber`, `OutputVersion`, `AlgorithmVersion`, `ContractVersion`, and technical concurrency
`Version` are distinct concepts; business fields must never use the reserved bare name `Version`.

| Object | Field | Type | Required / rule |
|---|---|---|---|
| Common | `Id` | Guid | Server-generated, non-empty |
| Common | `TenantId` | Guid | Server context only; absent from client DTOs |
| Common | `Version` | integer/ETag | Technical optimistic concurrency only |
| Scenario | `Code` | string(128) | Trim/NFC; active tenant unique |
| Scenario | `Name` | string(128) | Trim/NFC, non-empty |
| Scenario | `Description` | string(2000)? | Trim/NFC; empty becomes null |
| Scenario | `Lifecycle` | enum | Exact `Active` / `Archived`; create = Active; Archive terminal and irreversible |
| ScenarioVersionDraft | `ScenarioId`, `DraftId` | Guid | Same-tenant Active Scenario; partial unique one-open-draft fence |
| ScenarioVersionDraft | `VersionLabel`, `EffectiveFrom`, `EffectiveTo`, `Notes` | bounded scalar/date | Ordered dates; expected technical fence on every mutation |
| ScenarioVersionDraft | `BasedOnScenarioVersionId` | Guid? | Null for from-scratch; otherwise current published version copied with immutable provenance |
| ScenarioVersionDraft | `Assumptions`, `ForeignReferences` | closed v1 sets | Exact schemas below; mutable only until publish |
| ScenarioVersion | `ScenarioId`, `ScenarioVersionNumber` | Guid/positive integer | Unique `(TenantId, ScenarioId, ScenarioVersionNumber)`; server assigned at publish |
| ScenarioVersion | sealed content/provenance | immutable snapshot | Publication actor/time, source DraftId, canonicalization version and canonical digest atomically sealed |
| ScenarioVersion | disposition history | append-only events | Prior current becomes historical/superseded when next publishes; bytes unchanged |
| ComparatorRunDefinition | exact v1 definition | immutable | `BaselineDelta` catalog, 2..10 exact inputs, empty parameter allowlist |
| ComparatorExecutionEvent | Requested/Started/Completed/Failed | append-only event | Async worker attempts; no terminal rewrite |
| ComparatorOutput | exact v1 output | immutable | At most one successful output per run; schema below |
| Selection occurrence | `SelectionOccurrenceId`, `SelectionRevision` | Guid/positive integer | Immutable occurrence/revision identity |
| Selection occurrence | `InvestmentCaseContextV1` | `ppm.investment-case-context` / `1.0` | Exact sole v1 scope; PPM ownership unchanged |
| Selection occurrence | `SelectedScenarioVersionReference` | `ScenarioVersionReferenceV1` | Published and eligible when selected |
| Selection disposition | closes/replaces occurrence | append-only event | Actor/time/reason/correlation; no mutable current-state field on occurrence/history |
| Selection fence | tenant + typed scope token | concurrency control | Transactionally prevents more than one effective selection; effective state derives from history |

### Scenario lifecycle and publication — closed v1

- Scenario is created `Active`; the only lifecycle transition is `Active → Archived`. Archive is irreversible.
- Mutable analytical content exists only in `ScenarioVersionDraft`. Each `(TenantId, ScenarioId)` has at most one
  open draft through a tenant-first partial unique constraint and transactional fence.
- A draft starts empty or copies the current published version. Copy-on-new-draft stores immutable
  `BasedOnScenarioVersionId`; subsequent edits never mutate the source.
- Publish on an Active Scenario atomically assigns the next monotonic server-side `ScenarioVersionNumber`, seals
  immutable content/provenance/hash, appends audit intent and consumes/closes the draft. A concurrent loser is 409.
- Publishing a successor makes the prior current version historical/superseded by derivation; its bytes and hash
  never change. Current published version is the highest committed ScenarioVersionNumber for the Scenario; it is
  derived, not a mutable flag. Archived Scenario blocks new draft, publish, comparator run and selection and cannot reopen.
- `HistoricalResolve` may resolve archived/superseded published versions. `NewReferenceEligibility` returns 409.
  Only an unpublished abandoned draft may soft-delete.

### `ScenarioAssumptionV1` — exact closed schema

Exact fields are `AssumptionKey`, `ValueType`, `Value`, `UnitKind`, and `CurrencyReference`; extra fields fail.

| Field | Closed rule |
|---|---|
| `AssumptionKey` | Trim + NFC, 1..128, case-sensitive ordinal unique within draft/version |
| `ValueType` | Exact `Decimal`, `Integer`, `Boolean`, `String`, or `Date` |
| `Value` | Type-governed canonical scalar; arbitrary/nested JSON forbidden |
| `UnitKind` | Exact `None`, `Units`, `Thousands`, `Millions`, or `Currency` |
| `CurrencyReference` | Required only for `Currency`; exact MOD-0136 `fpa.currency-reference` / `1.0`; otherwise null |

- String is NFC and at most 2000 characters. Integer is signed 64-bit canonical decimal text. Decimal is
  Decimal128-compatible, precision ≤28 and scale ≤4, plain fixed-point canonical text with no locale/exponent;
  binary float, NaN and Infinity are forbidden. Boolean is only JSON `true`/`false`; Date is only `yyyy-MM-dd`.
- DimensionReferences, per-assumption PeriodReference and evidence/provenance payload are forbidden/deferred in v1.
  Every assumption applies to the ScenarioVersion effective interval. Maximum count is 200.

### Foreign reference set v1 — exact closed schema

- Only exact MOD-0136 `BudgetVersionReferenceV1` using `fpa.budget-version-reference` / `1.0` is accepted.
- Maximum 20 distinct references, sorted by ContractName → ContractVersion → BudgetId → BudgetVersionId →
  BudgetVersionNumber. Amount, line, currency detail or budget payload/snapshot is never copied.
- Malformed caller shape is 400; unknown/unsupported contract or version is 503.

### Scenario publication hash — exact canonical JSON v1

- Contract identity is `fpa.scenario-content-canonical-json.v1`. Bytes are UTF-8 without BOM or trailing newline.
  Strings are NFC; UUIDs are lowercase canonical D; dates are `yyyy-MM-dd`; Decimal values use invariant plain
  fixed-point text with no exponent/locale, insignificant fractional zeroes removed and zero rendered `"0"`.
- Top-level property order is exactly `CanonicalizationVersion`, `VersionLabel`, `EffectiveFrom`,
  `EffectiveTo`, `Notes`, `Assumptions`, `ForeignReferences`. Assumption property order is the exact
  five-field schema order. Foreign BudgetVersion properties are `ContractName`, `ContractVersion`,
  `BudgetId`, `BudgetVersionId`, `BudgetVersionNumber`.
- Assumptions sort by AssumptionKey ordinal. Foreign references sort by ContractName → ContractVersion → IDs →
  version number. Semantic content, effective interval, assumptions and foreign references are included.
  Database/generated child IDs, Scenario/Tenant IDs, audit timestamps and technical Version are excluded.
- Missing/extra properties or invalid scalar formats fail publication. NFD input is normalized to NFC; source JSON
  property order and assumption insertion order cannot change canonical bytes. Digest is SHA-256 lowercase 64 hex.

Normative vector 1 — minimal, 309 UTF-8 bytes:

```json
{"CanonicalizationVersion":"fpa.scenario-content-canonical-json.v1","VersionLabel":"Base","EffectiveFrom":"2026-01-01","EffectiveTo":"2026-12-31","Notes":null,"Assumptions":[{"AssumptionKey":"GrowthRate","ValueType":"Decimal","Value":"0.05","UnitKind":"None","CurrencyReference":null}],"ForeignReferences":[]}
```

SHA-256: `38ff8c101b72104847a1a34765c5fd23e1ea423f8a05b6488c2e2ece288a7788`.

Normative vector 2 — multiple assumptions plus BudgetVersion reference, 623 UTF-8 bytes:

```json
{"CanonicalizationVersion":"fpa.scenario-content-canonical-json.v1","VersionLabel":"Plan","EffectiveFrom":"2026-01-01","EffectiveTo":"2026-12-31","Notes":"Plan","Assumptions":[{"AssumptionKey":"Cost","ValueType":"Decimal","Value":"1250000","UnitKind":"Millions","CurrencyReference":null},{"AssumptionKey":"Headcount","ValueType":"Integer","Value":"42","UnitKind":"Units","CurrencyReference":null}],"ForeignReferences":[{"ContractName":"fpa.budget-version-reference","ContractVersion":"1.0","BudgetId":"11111111-1111-1111-1111-111111111111","BudgetVersionId":"22222222-2222-2222-2222-222222222222","BudgetVersionNumber":3}]}
```

SHA-256: `b7dbd424e46da5f3d1d12739dff22be2c55f606a7a959dce3e0ad0f530bcd80b`.

### Comparator v1 — exact closed catalog and output

- Exact catalog: `ComparatorKey = BaselineDelta`, `AlgorithmVersion = 1.0`,
  `NormalizationVersion = fpa.scenario-normalization.v1`, `ParameterSchemaVersion = 1.0`, and
  `OutputSchemaVersion = 1.0`. Parameter allowlist is empty; arbitrary parameters are forbidden.
- Run `RequestDigest` is SHA-256 over no-whitespace UTF-8/no-BOM canonical JSON ordered exactly as ComparatorKey,
  AlgorithmVersion, NormalizationVersion, ParameterSchemaVersion, OutputSchemaVersion, then ordered
  ScenarioVersionReferences (each exact producer tuple order). `InputDigest` is SHA-256 over canonical JSON array
  of the referenced Scenario content-hash strings in baseline/candidate order. Both are lowercase 64 hex.
- Input is exactly 2..10 distinct published/current/new-reference-eligible `ScenarioVersionReferenceV1` values:
  first baseline, remaining candidates. All share effective interval, assumption key set and matching ValueType,
  UnitKind and CurrencyReference. Only Decimal/Integer keys score; other scalar keys are compatibility evidence only.
- Per numeric key delta is candidate minus baseline; Score is the exact decimal sum. Rank is Score descending with
  canonical ScenarioVersionId ordinal tie-breaker. Overflow or incompatible interval/key/unit/currency is 400.
  Binary float, ML/model, random seed and nondeterministic execution are forbidden.
- Worker is asynchronous. Immutable accepted definition emits Requested, Started and Completed or Failed events.
  One initial attempt plus two transient retries use 10-second then 30-second backoff; each attempt times out at
  60 seconds. Contract/validation/security failures fail fast. Terminal transient failure appends Failed and crosses
  the alarm/DLQ boundary. Exact idempotent duplicate returns the original run/output; explicit rerun creates new IDs.
- Immutable successful `ComparatorOutput` contains `ComparatorRunId`, `OutputVersion = 1`,
  `BaselineScenarioVersionReference`, `CandidateResults`, `ResultDigest`, and exact algorithm/normalization/
  parameter-schema/output-schema/execution-engine provenance. Each CandidateResult contains
  `ScenarioVersionReference`, `Score`, `Rank`, and Delta rows ordered by AssumptionKey. Each Delta row contains
  exactly `AssumptionKey`, `BaselineValue`, `CandidateValue`, and `Delta` as canonical fixed-decimal strings.
  Canonical output property order follows the field order stated here; `ResultDigest` is SHA-256 over those exact
  canonical output bytes. PPM stores only `ComparatorOutputReferenceV1`.

### Selection v1 — exact closed semantics

- Sole scope is exact `ppm.investment-case-context` / `1.0`. Tenant + InvestmentCaseContext has exactly one
  effective selection after replace and zero after close; PPM context/cardinality/lifecycle ownership does not move.
- Only published, visible, non-archived ScenarioVersion passing `NewReferenceEligibility` can be selected.
  `EffectiveFromUtc` is server commit time; scheduled-future and backdated selection are forbidden.
- Occurrence is immutable. Replace writes prior disposition, successor occurrence and audit intent atomically;
  close writes prior closure and audit intent atomically. Nothing updates an occurrence in place.
- Current truth derives from event history plus monotonic scope fence. Business rollback is a new occurrence that
  selects the prior version. Deterministic rebuild must produce the same head. Multiple effective occurrences or a
  broken fence fail readiness closed; local inference/fallback is forbidden.
- `HistoricalResolve` may show an old occurrence/version. `CurrentSelectionEligibility` is allowed only for PPM
  SelectedScenario wrapper; a visible stale/not-current pinned reference returns 409. Selection is not approval.

### Retention and purge — exact closed v1

- Scenario, published ScenarioVersion, ComparatorRunDefinition/events/output, selection occurrence/disposition history,
  canonical hashes and receipts have no ordinary delete, bulk-delete or TTL. Retired/archived history is preserved.
- V1 supports no exceptional purge. Legal hold blocks deletion. Abandoned unpublished drafts may soft-delete but
  have no automatic hard purge. Bilateral version support cannot be removed until persisted-reference inventory is zero.
- Any future purge requires MOD-0030 authority plus a separate approved amendment; this pack grants neither.

Required indexes are tenant-first, include `IsDeleted=false` only for mutable/soft-deletable records, and include
active Scenario Code, Scenario business version, idempotency receipt and Run/Output identity. The transactional
scope fence plus immutable occurrence/disposition history proves at most one effective selection without adding
a mutable current-state flag to an append-only revision.

### Gate I-B minimal reference contract — exact v1

- `ContractName = fpa.scenario-planning-reference` (exact, case-sensitive).
- `ContractVersion = 1.0` (exact major/minor). V1 is not duplicated in `ContractName`.
- `ScenarioVersionReferenceV1` persists exactly `ContractName`, `ContractVersion`, `ScenarioId`,
  `ScenarioVersionId`, and `ScenarioVersionNumber`.
- `ComparatorOutputReferenceV1` persists exactly `ContractName`, `ContractVersion`, `ComparatorRunId`,
  `ComparatorOutputId`, and `OutputVersion`.
- A PPM ComparatorOutput reference never persists compared-scenario lists, assumptions, algorithms, rankings,
  metrics, parameters, financial inputs or output payload. MOD-0138 validates provenance authoritatively.
- Historical resolvability, eligibility for a new reference and current-selection validity are separate results.
  Archived/superseded published versions may remain historically resolvable while closed to new links/selection.

### PPM Gate I-B bilateral wrapper profiles — authoritative consumer checkpoint

At PPM authority checkpoint `a2c7ff5bbfd3ef2b4e3c8c15eab804db58d62520`, MOD-0117 owns these exact,
case-sensitive `1.0` wrappers. They do not extend or rename the nested MOD-0138 tuples:

| PPM-owned type | Exact PPM `ContractName` | Persisted fields, exactly | Nested MOD-0138 value |
|---|---|---|---|
| `InvestmentCaseContextV1` | `ppm.investment-case-context` | `ContractName`, `ContractVersion`, `InvestmentCaseId` | None; shared typed context |
| `InvestmentCaseScenarioVersionReferenceV1` | `ppm.investment-case-scenario-version-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `ScenarioVersionReference` | Exact `ScenarioVersionReferenceV1` |
| `InvestmentCaseComparatorOutputReferenceV1` | `ppm.investment-case-comparator-output-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `ComparatorOutputReference` | Exact `ComparatorOutputReferenceV1` |
| PPM `SelectedScenarioReferenceV1` | `ppm.investment-case-selected-scenario-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `ScenarioVersionReference` | Exact pinned `ScenarioVersionReferenceV1` only |

The PPM selected-scenario wrapper is a consumer relationship value, not a producer selection-occurrence reference and not
MOD-0138 selection truth. It persists no selection occurrence/revision, disposition, current-state flag or
producer history. For `CurrentSelectionEligibility`, PPM supplies the exact `InvestmentCaseContextV1` plus its
pinned `ScenarioVersionReferenceV1`; MOD-0138 resolves the authoritative current selection for that typed context.
If a future producer-owned occurrence/reference contract is required for non-PPM consumers, it must be separately
named, versioned and approved after its scope closes. It is not part of the Gate I-B PPM persistence family and
must never be named or serialized as PPM `SelectedScenarioReferenceV1`.

PPM stores no compared-scenario list, assumption, algorithm/version, normalization, parameter, input/evidence
digest, ranking, metric or output payload. MOD-0117 alone owns wrapper cardinality, attachment/detachment/storage,
requiredness and InvestmentCase lifecycle effects; none of those consumer rules move into MOD-0138.

## 5. Repo Scope

Current authoring scope is exactly:

- `execution/domains/enterprise-strategy-business-performance/module-packs/MOD-0138-scenario-planning.md`

Future runtime paths remain `TBD` until topology, exact repo/base/collection/route/port mapping, migration and explicit production
authority are approved. This pack does not authorize creating `Diten.FpaService` or any other project.

## 6. Protected Paths

- `.antigravity/**`
- `services/**`, `frontend/**`, and `gateway/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent only)
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` and Archive paths
- Other domains' governance and runtime paths
- Existing `Diten.EnterpriseStrategyService` ScenarioType/scenario fields and collections
- Shared governance/runtime/frontend/Gateway files

## 7. Dependencies

| Dependency | Boundary | Gate |
|---|---|---|
| MOD-0136 Budgeting | Exact BudgetVersionReferenceV1 and `fpa.currency-reference`/`1.0`; no shared SoR/type/repository | Contract-only consumption; neither pack grants runtime authority |
| MOD-0117 PPM | Gate I-B wrappers at authority checkpoint `a2c7ff5b...`; exact profiles in §4 | Governance shape closed; executable consumer amendment and runtime authority still required |
| MOD-0007 | Generic decision/rationale owner | Not duplicated here |
| MOD-0023 | Approval only when policy requires | Separate contract; never implied by selection |
| MOD-0072 | Actual realization owner | Not duplicated here |
| MOD-0018 | Exact permission/tenant entitlement enforcement | Runtime evidence required |
| MOD-0021 / MOD-0035 | Immutable audit/eventing mechanics | Exact integration remains open |
| DCP-006 | Gate I orchestration | Approved; OD-04 OPEN/PARTIAL |
| MongoDB replica set | Atomic publish/output/selection/idempotency/audit persistence | Runtime prerequisite |
| Comparator worker/runtime | Closed BaselineDeltaV1 semantics; async delivery mechanics | Runtime infrastructure remains a B blocker |

## 8. Runtime Constraints

- MongoDB single-database multi-tenancy; every lookup is tenant-first. Active mutable records also filter
  `IsDeleted=false`; immutable history is resolved through disposition/quarantine policy, not ordinary delete.
- Tenant and actor derive only from authenticated server context; client `TenantId`/`ActorId` is forbidden.
- Publishing consumes an exact draft fence and atomically creates the immutable ScenarioVersion, monotonic number,
  publication provenance, canonicalization version, canonical digest, idempotency receipt and audit intent.
- Published content is never edited. Supersession/retirement is append-only disposition history. Abandoned drafts
  may be soft-deleted; published versions, completed outputs and selection history cannot be CRUD-deleted.
  V1 has no exceptional purge, and PPM history is never rewritten.
- ComparatorRunDefinition is immutable after acceptance. Execution progress is append-only events/receipts and a
  successful ComparatorOutput is immutable. Exact idempotent duplicate returns the original Run/result; explicit
  rerun creates new Run/Output IDs and never rewrites terminal history.
- Reproducibility pins exact immutable input references and contract versions, BaselineDeltaV1 catalog,
  normalization, parameter/output schema, request/input/result digests and engine provenance. Nondeterministic
  algorithms, seeds/models and binary floating point are prohibited in v1.
- Financial data is never a second Budget SoR: compute rereads an immutable authoritative producer contract or
  retains only an approved canonical evidence/input digest, never budget amounts/lines as MOD-0138 truth.
- Selection replace/close atomically appends immutable occurrence/disposition history and advances a transactional
  scope fence. Effective current selection is derived. Rollback is a compensating append-only occurrence.
- Mutation, idempotency receipt and audit intent persistence is atomic; audit failure rolls back the mutation.
- Idempotency scope is TenantId + actor/service identity + exact operation + key, with canonical request digest
  and stable original result. Same digest replays without duplicate compute/output/audit; another digest is 409.
  Retention must exceed retry and unknown-commit recovery windows so expiry cannot enable duplicate side effects.
- No local/cache/prototype fallback and no transport/dependency failure may be collapsed to 404.
- V1 limits: decoded/decompressed UTF-8 request body 256 KiB (oversize `413`) enforced before materialization;
  JSON depth 8, duplicate properties/invalid Unicode/unknown polymorphic kinds/compression bombs rejected;
  validation batch 100 refs; comparator input 2..10; foreign references 20; assumptions 200; scalar assumptions
  only; key 128 and scalar string value 2000 characters; comparator parameters forbidden; arbitrary nested JSON forbidden.
  Numbers are Decimal128-compatible only in `[-1e15, 1e15]`, precision 28, scale 4; binary float, silent rounding,
  NaN/Infinity and exponent overflow/underflow are rejected before allocation/compute. Internal summary metadata,
  if approved, is capped at 100 metrics with 128-character keys; reference responses remain payload-free.
- Immutable versions, run definitions, execution receipts, outputs, selection/audit/disposition history and
  quarantine records have no TTL or CRUD delete; v1 has no exceptional purge and legal hold always blocks deletion.
- Bilateral supported-version matrix initially contains only MOD-0138 `fpa.scenario-planning-reference` `1.0`,
  the three PPM scenario wrappers `1.0`, and shared `ppm.investment-case-context` `1.0`. No version coercion,
  downgrade, alias, default-latest behavior or unknown-version fallback is allowed.
- The compatibility matrix is two-dimensional: each row pins one producer tuple ContractVersion and one PPM
  wrapper/context ContractVersion. Producer and wrapper versions may evolve independently. V1 field allowlists and
  semantics are immutable; any breaking/additional persisted shape requires a new approved version and fixtures.
- Deprecation is bilateral and staged: announce a replacement and overlap window; stop `NewReferenceEligibility`
  for the retiring version only after both producer and consumer approve the matrix; preserve `HistoricalResolve`
  for persisted history through the retention period; remove support only with bilateral fixtures, migration/
  rollback evidence and explicit producer/consumer authority. Unsupported or prematurely retired versions are 503.
- A known deprecated version is not unknown: `HistoricalResolve` may still succeed during retention, while new or
  current use returns 409 according to the mode. Removal requires persisted-reference inventory, zero unsupported
  live/current references or an approved migration, rollback evidence and consumer deployment evidence; neither
  producer nor consumer may remove/down-convert support unilaterally.
- No port, Gateway route, service project, collection or production credential is allocated here.

## 9. Layout & Shell Contract

Draft UI choice: `shell: tenant`. Any later authorized Razor page must explicitly declare
`Layout = "_LayoutTenantShell"`; `_ViewStart.cshtml` defaults are insufficient. Proposed view location is
`Views/Fpa/ScenarioPlanning/`.

Each surface is counted independently; unrelated screens are never combined:

| Surface | Counted user inputs | Count | Decision |
|---|---|---:|---|
| Scenario create/edit | Code, Name, Description | 3 | Slim/offcanvas candidate |
| ScenarioVersionDraft create/edit | Header: VersionLabel, EffectiveFrom, EffectiveTo, Notes (4); assumption row: AssumptionKey, ValueType, Value, UnitKind, CurrencyReference (5); foreign BudgetVersion picker (1) | **10** | Compact Create/Edit/Details + `_Form` |
| ComparatorRun request | BaselineScenarioVersionReference, CandidateScenarioVersionReferences; algorithm/schema values are fixed read-only | 2 | Slim-count action/full result page |
| Selection replace/close | ScenarioVersionReference, ReasonCode; InvestmentCaseContext route/server-derived | 2 | Slim-count modal; not approval |

The maximum actual primary create/edit surface is ScenarioVersionDraft at **10**, so frontmatter uses
`golden_reference: compact` and `form_field_count: 10` under the
[Compact reference](../../developer-enablement/module-packs/DEV-0001-golden-reference-compact.md). Repeatable-row
inputs count once per editable column. Route IDs, fixed catalog/schema values, technical versions, digests,
provenance and audit fields do not count. These counts are closed for v1.

## 10. Backend File Convention

If runtime is later authorized, use the Compact CQRS convention: `Commands/`, `Queries/`,
`Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/`, and `ScenarioPlanningModels.cs`.
Commands/queries are records; handler names omit Command/Query suffix; validator names omit Command suffix.

MOD-0136 and MOD-0138 must have separate namespaces/projects as selected later, collections, repositories,
domain types, permissions, handlers and DI registrations. Project-reference, namespace, DI, repository-interface
and collection-name architecture tests must prove the boundary. Only approved typed/versioned application
contracts/ports may cross it; co-location never authorizes a shared domain model or cross-module transaction.

## 11. Frontend File Contract

Proposed surfaces:

- Scenario register: Slim-count DataTable v2 with create/edit offcanvas and quick view.
- ScenarioVersionDraft: Compact separate Create/Edit/Details and `_Form.cshtml`; published detail is read-only and
  has no edit/delete action.
- Comparator: dedicated run/details/result-reference surface showing pinned input/schema/algorithm/engine versions;
  completed output is read-only and rerun is a distinct action.
- Selection: permission-gated replace/close with concurrency and audit context; no Approve/Reject button.

The Compact set is `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`,
`_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `ScenarioPlanningIndex.cs`, `index.js`, and
`index.l10n.js`. PPM may later show opaque reference picker/status links only; it must not copy comparator UI,
assumptions, calculations, rankings or output payload.
No MOD-0138 surface exposes delete/bulk-delete for Scenario, published versions, comparator history/output or
selection history. Draft abandon is the only soft-delete action. Scenario Archive uses the existing exact
`fpa.scenario-planning.scenarios.update` permission; this closure adds no seventeenth permission key.

## 12. Validation Rules

| Field/boundary | Required | Rule / pre-check |
|---|---:|---|
| Tenant/Actor | Server only | Valid authenticated claims; payload/header override rejected |
| Scenario Code/Name | Yes | Strict decode, Trim/NFC, lengths above; Code active tenant unique |
| Scenario lifecycle | Yes | Create Active; only Active→Archived; archived operations/reopen return 409 |
| ScenarioVersionDraft | Mutate/publish | One open per tenant+Scenario; exact fence; source provenance; abandoned unpublished only may soft-delete |
| Published ScenarioVersion | Server seal | Positive monotonic number; atomic canonical bytes/hash/provenance/audit; no update/delete |
| Assumption | 0..200 | Exact five fields/enums/scalar rules in §4; ordinal key unique; extra/nested/dimension/period/evidence fields fail |
| CurrencyReference | Conditional | Exact `fpa.currency-reference`/`1.0` required iff UnitKind Currency; otherwise null |
| Foreign reference | 0..20 | Only distinct exact MOD-0136 BudgetVersionReferenceV1; no financial payload/snapshot |
| Comparator inputs | Exactly 2..10 | Baseline first; current eligible; same interval/key/type/unit/currency; distinct published refs |
| Comparator catalog | Fixed | Exact BaselineDelta/1.0 + fixed normalization/parameter/output schemas; empty parameter allowlist |
| Comparator execution | Async | Initial+2 transient retries, 10s/30s backoff, 60s attempt timeout; deterministic decimal only |
| Canonical content | Publish | Exact closed property order/normalization and both normative SHA-256 vectors in §4 |
| Contract | Yes | Exact `fpa.scenario-planning-reference` and `1.0`; exact minimal shapes in §4 |
| Reference validation mode | Yes | `HistoricalResolve` / `NewReferenceEligibility` for scenario and comparator wrappers; selected-scenario wrapper alone also permits `CurrentSelectionEligibility` |
| PPM bilateral wrapper | Consumer persistence | Exact wrapper name/version/field allowlist in §4; nested producer tuple unmodified; unknown/extra fields fail |
| Current selection request | Selection validation only | Exact `ppm.investment-case-context` `1.0` plus pinned `ScenarioVersionReferenceV1`; no occurrence/history supplied by PPM |
| Selection | Replace/close | Only InvestmentCaseContext/1.0; server commit time; published visible active eligible ref; exact fence/revision |
| Idempotency-Key | Mutation/run | 16..128 ASCII; tenant+operation scoped; canonical request SHA-256 |
| JSON/numeric/string | All | Enforce §8 before allocation/compute; invalid Unicode fails closed |

## 13. Failure Path to Verify

| Scenario | Expected |
|---|---|
| Malformed/invalid/out-of-bounds input | 400; decoded body over 256 KiB is 413; no allocation, compute or mutation |
| Missing/invalid token or server tenant/actor context | 401 |
| Authenticated caller/actor lacks exact permission or S2S entitlement | 403 |
| Missing, deleted, cross-tenant or invisible object | Indistinguishable 404 |
| Published mutation, retired/ineligible new reference, stale/closed selection used as current, stale Version, selection race, or idempotency digest mismatch | 409; no overwrite |
| Unknown contract/algorithm version, compute/dependency timeout/unavailable/malformed response | 503; no fallback |
| Same idempotency key and digest | Original outcome; no duplicate Run/Output/selection/audit |
| Caller cancellation | Propagated cancellation; not rewritten as 503 |
| Audit/idempotency persistence failure | Entire publish/run/selection transaction rolls back |
| CRUD delete of published/output/selection history | 409; bytes/history unchanged |
| Provider/algorithm outage | Historical immutable read remains available; new publish/run/selection validation is 503 |
| Prototype cannot prove canonical identity/version/provenance | Quarantined; never promoted or used as fallback |
| Concurrent second open draft or publish-number race | Exactly one transaction wins; loser 409; no partial seal/audit |
| Draft/publish/run/select against Archived Scenario or reopen attempt | 409; historical bytes remain resolvable |
| Assumption extra field, invalid scalar, duplicate key, bad currency conditional or >200 | 400; no publish |
| Foreign reference not exact BudgetVersion tuple, duplicate, >20 or payload-bearing | 400; unsupported version 503 |
| Canonical bytes/hash differ from normative vectors | Publish fails closed; no immutable version created |
| BaselineDelta input count/key/type/unit/currency/interval mismatch, overflow or parameter supplied | 400; no run accepted |
| Transient compute timeout/failure | Retry after 10s then 30s, max three attempts; exhausted run appends Failed and returns/records 503 boundary |
| Broken selection fence or multiple effective occurrences | Readiness/current validation fails closed; no local repair/inference |
| Ordinary delete/bulk-delete/TTL or exceptional-purge attempt | Rejected; no v1 operation exists and history remains unchanged |

Mode semantics are exact: `HistoricalResolve` succeeds for an authentic historically resolvable retired/archived
published reference; `NewReferenceEligibility` returns 409 for a retired/archived but visible reference; and
`CurrentSelectionEligibility` returns 409 for a valid historical occurrence that is closed/stale against the
derived head. Missing/cross-tenant/invisible remains 404 in every mode. Unknown/malformed mode is caller error 400;
unsupported contract version or malformed authoritative dependency response is 503.

`InvestmentCaseScenarioVersionReferenceV1` and `InvestmentCaseComparatorOutputReferenceV1` reject
`CurrentSelectionEligibility` with 400. Only PPM `SelectedScenarioReferenceV1` may request that mode.

For the PPM selected-scenario wrapper, `HistoricalResolve` resolves the pinned scenario reference and wrapper
history without asserting current selection. `CurrentSelectionEligibility` compares the exact typed InvestmentCase
context and pinned ScenarioVersion reference with MOD-0138's effective producer-owned selection. It never accepts a
PPM-provided occurrence/revision as current truth. A visible historical occurrence may remain resolvable while its
pinned version is stale/not-current for that context and therefore returns 409 in current-selection mode.

## 14. Authorization Convention

- Tenant UI/API uses authenticated tenant actor plus each exact permission listed in §3.
- Internal PPM validation requires a dedicated allowlisted S2S identity bound to the exact audience/contract **and**
  a separately validated delegated actor with `fpa.scenario-planning.references.validate`.
- Authentication/audience and server context are established before authoritative tenant-first lookup.
- Enforcement order is endpoint configuration → dedicated S2S credential and exact audience → server tenant/actor
  claims → caller allowlist/entitlement → delegated actor exact permission → tenant-first authoritative lookup.
- Invalid/missing/expired credential, audience or actor context is 401; authenticated but non-allowlisted/
  non-entitled caller or missing delegated permission is 403; payload/query/header tenant or actor override is 400.
- Service identity alone never bypasses delegated actor visibility/referenceability.
- Wildcard, alias, wrong-case and prefix permissions are denied. No approve/reject permission exists.
- Audit records tenant, actor, old/new opaque refs, timestamp, reason code, correlation/causation and hashed
  idempotency key; assumptions/output payload and raw secrets are excluded.
- Immutable audit covers Scenario create/update/archive; draft create/update/abandon; publish/retire; comparator
  request/start/complete/fail; and selection replace/close. It records target IDs/versions, actor/service,
  exact contract/algorithm versions and operation provenance, never secrets, raw tokens or analytical payload.

## 15. Gateway / API Routing Decision

Decision: **not allocated in this draft**. If frontend/runtime is later authorized, browser traffic must remain
frontend `5001` → Gateway `5000`; direct service-port calls are forbidden. Any Ocelot change is a separate
integration-agent task after topology and port approval. The S2S validation surface must not be browser-exposed.

## 16. Acceptance Criteria

- [ ] Scenario creates Active, transitions only to terminal Archived, never reopens, and archived mutation/run/selection is 409 while historical reads remain.
- [ ] Tenant + Scenario permits one open draft; copy provenance is pinned; publish atomically seals the next monotonic immutable version and audit/hash evidence.
- [ ] Assumption serialization contains exactly five fields, closed enums/scalars, ordinal unique key and maximum 200; v1 forbidden fields are rejected.
- [ ] Foreign set contains only at most 20 distinct exact MOD-0136 BudgetVersionReferenceV1 tuples and no financial payload/snapshot.
- [ ] `fpa.scenario-content-canonical-json.v1` produces the exact 309/623-byte normative vectors and their recorded SHA-256 values independent of NFD/source order/insertion order.
- [ ] BaselineDeltaV1 accepts exactly 2..10 eligible inputs, empty parameters and fixed catalog versions; decimal deltas, score/rank/tie-breaker and exact internal output are deterministic.
- [ ] Async execution emits only Requested/Started/Completed-or-Failed, uses initial+2 retries at 10s/30s and 60s per attempt, one successful output and new IDs for explicit rerun.
- [ ] Selection scope is only InvestmentCaseContext/1.0; occurrence/disposition/fence are immutable/atomic/rebuildable and current mismatch is 409 without approval semantics.
- [ ] No ordinary delete/bulk-delete/TTL or v1 purge exists for authoritative history; draft abandon alone soft-deletes and future purge requires MOD-0030 amendment.
- [ ] UI evidence proves Scenario 3/Slim, VersionDraft 10/Compact, Comparator 2 and Selection 2 with no bulk-delete, approval or PPM comparator UI.
- [ ] Exact `ContractName = fpa.scenario-planning-reference` and `ContractVersion = 1.0` are approved without duplicate v1 encoding.
- [ ] `ScenarioVersionReferenceV1` persists exactly ContractName, ContractVersion, ScenarioId, ScenarioVersionId and ScenarioVersionNumber.
- [ ] `ComparatorOutputReferenceV1` persists exactly ContractName, ContractVersion, ComparatorRunId, ComparatorOutputId and OutputVersion.
- [ ] No producer selection-occurrence DTO is part of the PPM persistence family; any future non-PPM occurrence
  reference requires a separately named/versioned approved contract.
- [ ] PPM wrappers are exact: scenario-version and comparator-output wrappers nest their matching producer tuple;
  selected-scenario wrapper nests only pinned `ScenarioVersionReferenceV1`; all use shared exact
  `ppm.investment-case-context` `1.0`.
- [ ] PPM selected-scenario wrapper is never represented as a producer occurrence reference; occurrence,
  disposition history and effective-current truth remain solely MOD-0138-owned.
- [ ] MOD-0117—not MOD-0138—owns InvestmentCase `0..n` and `0..1` cardinalities, requiredness, transitions and minimal storage; MOD-0138 supplies fixtures and authoritative validation.
- [ ] PPM persists only opaque contract/kind/IDs/versions; no assumptions, calculations, result payload or selected flag.
- [ ] Publishing atomically seals an immutable ScenarioVersion from a mutable draft with provenance, canonicalization, digest, audit and idempotency.
- [ ] Published versions and completed comparator outputs are immutable; explicit rerun creates new IDs and pins all reproducibility versions/digests.
- [ ] Effective selection is derived from immutable occurrence/disposition history under a transactional scope fence; history rows carry no mutable current-state flag.
- [ ] Abandoned drafts alone may soft-delete; immutable versions/outputs/selection history reject CRUD delete and preserve retirement/supersession history.
- [ ] Exact 400/401/403/404/409/503 semantics and §8 bounds are proven fail-closed.
- [ ] Same-service topology is permitted only when architecture tests prove MOD-0136/MOD-0138 isolation.
- [ ] Existing ScenarioType/ES scenario fields remain quarantined prototype/migration evidence with no fallback/dual write.
- [ ] Authorized UI explicitly uses `_LayoutTenantShell`, Compact/DataTable v2 where applicable, seven-language localization, and no PPM comparator copy or approval action.
- [ ] Gate I-B remains OPEN until producer approval, matching consumer amendment, compatibility/security evidence and explicit runtime authority.
- [ ] Bilateral supported-version/deprecation matrix proves exact 1.0 fixtures, no coercion/fallback, overlap before
  retirement, historical support through retention and explicit two-sided removal authority.

## 17. Test Expectations

- Unit/domain: draft mutation/fence, atomic publish seal, next-version isolation, disposition history, historical/
  new/current eligibility, deterministic canonical digest, all comparator version pins, rerun-new-ID behavior,
  effective selection derivation, compensating rollback and idempotency.
- Lifecycle/concurrency: create Active; irreversible archive; archived operation 409; two-client one-open-draft and
  monotonic publish races yield one winner; copied draft mutation leaves source bytes/hash unchanged.
- Assumption/foreign schema: every ValueType/UnitKind/currency conditional and boundary; normalized ordinal duplicate;
  max 200/20; exact-field reflection; BudgetVersion-only tuple; nested/dimension/period/evidence and payload rejection.
- Canonical hash: recompute both normative byte strings and exact SHA-256 values. Negative vectors permute source
  properties/insertion order and NFD (same canonical result), while UUID case, invalid date/decimal exponent/scale,
  duplicate/missing/extra fields fail before immutable persistence.
- BaselineDelta golden vectors cover positive/negative/zero deltas, score sums, descending ranking and canonical
  ScenarioVersionId tie-break. Negative cases cover 1/11 inputs, duplicates, draft/archived/ineligible versions,
  interval/key/type/unit/currency mismatch, overflow, parameter injection, binary float and alternate algorithm.
- Worker tests assert Requested→Started→Completed/Failed only; initial+2 attempts at 10s/30s, 60s timeout,
  fail-fast non-transient errors, retry exhaustion alarm/DLQ, one successful output, replay-original and rerun-new-ID.
- Selection tests assert atomic replace/close/audit, one effective occurrence, monotonic fence, deterministic rebuild,
  compensating rollback, broken-history fail-closed and historical-vs-current 200/409 behavior.
- Retention architecture tests reject delete/bulk-delete/TTL/purge routes, commands, indexes and background jobs;
  legal hold remains non-bypassable and abandoned draft has soft-delete but no hard-purge path.
- Real Mongo integration: tenant-first/soft-delete behavior; unique/partial indexes; concurrent publish/selection and
  idempotency races; transaction fault injection proving mutation/output/receipt/audit atomicity.
- Contract: reflection plus golden JSON/BSON fixtures enforce exact serialized allowlists:
  `ScenarioVersionReferenceV1` = `{ContractName, ContractVersion, ScenarioId, ScenarioVersionId, ScenarioVersionNumber}`
  and `ComparatorOutputReferenceV1` = `{ContractName, ContractVersion, ComparatorRunId, ComparatorOutputId, OutputVersion}`.
  No producer selection-occurrence DTO participates in these PPM fixtures. Missing or extra serialized properties
  fail. PPM negative-schema tests forbid
  compared lists, assumptions, algorithms, parameters, rankings, metrics and payload; independently built producer/
  PPM fixtures verify exact string `1.0`, reject a version suffix in ContractName and prove unknown-version 503.
- Bilateral fixture ledger at PPM checkpoint `a2c7ff5b...` independently builds and round-trips:
  `InvestmentCaseContextV1`; `InvestmentCaseScenarioVersionReferenceV1` nesting the exact scenario tuple;
  `InvestmentCaseComparatorOutputReferenceV1` nesting the exact comparator tuple; and PPM
  `SelectedScenarioReferenceV1` nesting only the pinned scenario tuple. Fixtures reject missing/extra fields,
  producer DTO mutation, occurrence/current-state fields in PPM, unknown wrapper/producer versions and payload copy.
- Consumer-owned boundary fixtures cover empty/one/max `0..n`, duplicate exact scenario/comparator rejection and
  selected `0..1` uniqueness without transferring those cardinality rules to MOD-0138. Fixture artifacts pin an
  immutable checksum/schema identity so producer and consumer approvals refer to the same bytes.
- Validation-mode matrix covers each wrapper/reference across historical, eligible, retired, stale/not-current,
  missing, cross-tenant, invisible, permission-denied, unsupported-version and provider-failure states with exact
  200/400/403/404/409/503 outcomes. Selected current-mode fixtures send typed InvestmentCase context plus pinned
  scenario reference and prove producer occurrence/history remains internal.
- Compatibility lifecycle tests keep old persisted fixtures historically resolvable during an approved overlap,
  block new links at the announced boundary, reject silent downgrade/default-latest and prevent support removal
  without bilateral approval, migration/rollback evidence and retention completion.
- Security: exact permission matrix, dedicated S2S plus delegated actor, cross-tenant/invisible 404 equivalence,
  forged context rejection, exact `.references.validate` allow/deny, wrong identity/audience, payload and compressed/
  JSON/numeric bomb boundary tests, immutable-history no-delete/no-TTL tests and audit redaction/rollback.
- Architecture: no Budgeting domain/repository/collection coupling; contract-only boundary; no direct service port;
  no MOD-0138 production types in EnterpriseStrategyService by proximity.
- PPM no-copy source/schema test: no assumption/calculation/ranking/output payload, selected boolean or local engine.
- Frontend/browser: Scenario Slim and VersionDraft Compact verifier/count evidence, permission actions, immutable edit denial, stale-selection 409 flow,
  pinned comparator versions, seven-language/RTL/accessibility, no Approve/Reject, no browser mock/localStorage truth.
- Prototype quarantine/migration: inventory and negative import/collection/route tests; deterministic idempotent dry-run,
  unknown-tenant quarantine, explicit mapping/provenance, rollback proof and untouched source.
- Pack validation: exact MOD-ID preflight, Markdown links, `git diff --check`, staging empty, and no runtime diff.

## 18. Ready-for-dev Checklist

- [x] Control Tower fixed Scenario lifecycle, one-open-draft/publication, AssumptionV1, Budget-only references,
  canonical JSON/hash vectors, BaselineDeltaV1, InvestmentCase selection, no-purge retention and UI 3/10/2/2.
- [ ] Prove isolated ScenarioPlanning module in future `Diten.FpaService`; select
  `Diten.ScenarioPlanningService` fallback if isolation fails.
- [ ] Fix exact repo paths, concrete service-local EntityBase mapping, Mongo collections/index names, routes, port
  and later integration-agent Gateway task.
- [ ] Approve/provision dedicated S2S identity, exact audience/credential/rotation and delegated actor propagation.
- [ ] Complete MOD-0018 entitlement/permission provisioning and runtime evidence.
- [ ] Complete MOD-0021/MOD-0035 audit-intent/outbox publisher, transaction and atomicity evidence.
- [ ] Select and prove comparator worker/queue/scheduler/runtime infrastructure for the closed BaselineDeltaV1 profile.
- [ ] Fix idempotency receipt/unknown-commit persistence, recovery and safe retry-window retention implementation.
- [ ] Author/approve prototype inventory, quarantine, migration/cutover/rollback slice.
- [ ] Record explicit runtime authority, including approved bilateral fixtures and executable MOD-0117 I-B Scenario amendment evidence.

## 19. Implementation Notes

Topology comparison:

| Option | Assessment |
|---|---|
| A. Isolated `ScenarioPlanning` module in a new/future `Diten.FpaService` modular monolith with MOD-0136 | **Recommended planning target.** FP&A/EPM cohesion, but mechanically separate types/repositories/permissions/DI/collections and approved contracts only. |
| B. `Diten.ScenarioPlanningService` | Mandatory fallback if module isolation cannot be mechanically proven; also suitable for independent scaling/release/compute blast radius, with greater S2S/operations cost. |
| C. `Diten.EnterpriseStrategyService` | Not baseline. Existing proximity is prototype/migration evidence, not Scenario Planning SoR authority. Reconsider only in a later approved architecture/migration slice. |

Existing `ScenarioType` and Enterprise Strategy scenario fields are quarantined evidence. A later migration slice
must inventory/classify records, reject default-tenant assignment, prohibit dual-read/dual-write and automatic
promotion, prove deterministic dry-run and mapping, verify collections/cutover, and prove rollback before authority.

MOD-0138 owns producer schemas, authoritative validation, supported-version/deprecation policy and golden producer
fixtures. MOD-0117 owns consumer cardinality/storage/transitions, adapter behavior and pinned supported versions.
Compatibility approval is bilateral; producer change never grants consumer runtime authority. Permission and
contract names are closed governance identities but are not registered runtime literals by this draft.

PPM checkpoint `a2c7ff5bbfd3ef2b4e3c8c15eab804db58d62520` was inspected read-only. It closes consumer
wrapper/context/mode governance only. MOD-0117 remains `review`, DCP-006 OD-04 remains OPEN/PARTIAL, and no producer
contract approval, topology/transport choice, adapter implementation or runtime authority is inferred here.

DCP-002 exact preflight command: `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0138 --name "Scenario Planning"`.

### Change log

| Date | Change | Authority effect |
|---|---|---|
| 2026-08-03 | Closed A-class Scenario semantics: Active/Archived lifecycle, one-open-draft publication, exact AssumptionV1 and Budget-only refs, canonical JSON plus two normative SHA-256 vectors, deterministic BaselineDeltaV1, InvestmentCase selection history/fence, no-purge retention and UI counts 3/10/2/2. | Governance semantics only; status remains draft and no runtime authority/topology/route/port is granted. |

## 20. Follow-up Items

- Prove isolated ScenarioPlanning in future `Diten.FpaService`; use `Diten.ScenarioPlanningService` fallback if isolation fails.
- Fix exact repo paths, concrete service-local base mapping, collections/index names, routes, port and Gateway task.
- Provision dedicated S2S identity/audience/credential rotation and delegated actor propagation.
- Complete MOD-0018 permission/entitlement provisioning evidence.
- Complete MOD-0021/MOD-0035 audit-intent/outbox publisher and atomicity evidence.
- Select/prove worker, queue/scheduler and runtime infrastructure for the closed BaselineDeltaV1 profile.
- Fix idempotency/unknown-commit persistence, recovery and safe retention implementation.
- Author/approve prototype inventory, quarantine, migration/cutover/rollback slice before touching ES prototypes.
- Record explicit runtime authority after bilateral fixtures and executable MOD-0117 I-B Scenario amendment approval.
