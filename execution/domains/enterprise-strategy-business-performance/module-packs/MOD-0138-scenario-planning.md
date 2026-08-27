---
id: MOD-0138
name: Scenario Planning
domain: enterprise-strategy-business-performance
service: Diten.FpaService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
owner: FP&A Scenario Planning Process Owner
branch: feature/esbp/mod-0138-scenario-planning
started: 2026-08-02
target: 2026-08-02
form_field_count: 10
production_authority: none
---

# MOD-0138 — Scenario Planning

> **READY-FOR-DEV / NON-RUNTIME.** All pre-development ScenarioPlanning design, scope and contract decisions are
> closed. Promotion authorizes only the bounded Core Scaffold & Contract-Test handoff in §5; it allocates no
> collection, Gateway route, deployment, credential, broker, live worker or production authority.
> `production_authority: none` remains binding.

## 1. Module Summary

- Blueprint logical route: **Enterprise Application Ecosystem → ERP Suite → FP&A / EPM**.
- Institutional owner: **FP&A Scenario Planning Process Owner**.
- Purpose: author immutable scenario versions, run deterministic version-pinned comparisons, expose bounded
  authoritative references, and own audited selected-scenario truth.
- Repository packaging under ESBP does **not** place this module in `Diten.EnterpriseStrategyService`.
- Gate I-B remains `OPEN`: consumer execution requires this producer pack and contract to be approved, a matching
  MOD-0117 consumer amendment to be approved, and explicit runtime authority.

Physical topology authority: [domain config — Authoritative physical topology](../domain-config.md#authoritative-physical-topology).
Other authorities: [DCP-006](../../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md),
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

No executable endpoint, route, controller, collection or runtime binding is authorized by this promotion. The only
implementation authority is the bounded non-runtime Core Scaffold & Contract-Test slice in §5.

### Gate I onboarding manifest — exact governance contract

This owner declaration closes MOD-0138's producer inputs to FU16 checkpoint
`cfbfee00b24314819101faa88d7ed8f1c83c7bd2`, MOD-0021 checkpoint
`525f6275a91eab7892741d8239dc1d0390915c3c` and MOD-0035 checkpoint
`f1568ace514a33e951c652802347dd22ddd1ac11` without granting runtime authority:

- Service audience: `diten-fpa-service`.
- Client ID: `diten.fpa`.
- ModuleCode: `MOD-0138`.
- Permission/event owner module: `MOD-0138`.
- Producer: `Diten.FpaService`; event type: `ScenarioPlanningAuditIntentSubmittedV1`; exact EventName/routing key:
  `fpa.scenario-planning-audit-intent.submitted.v1`; `EventVersion = 1`.
- Signing scheme: `fpa-scenario-planning-event-hmac-sha256.v1`; signing identity:
  `diten.fpa.mod-0138.scenario-planning`; key namespace: `diten/fpa/mod-0138/scenario-planning/audit-intent`.
  Active/previous production key IDs remain provisioning evidence, not values invented by this pack.

Audience and Client ID are shared workload identities for the future FPA host; they confer no MOD-0136 or MOD-0138
permission. `OwnerModuleId`, operation owner, permission owner, signing identity and key namespace remain module-local.
The MOD-0138 manifest must fail registration/startup if any key or operation ID is also declared by MOD-0136, if an
entry has another owner module, or if a wildcard/alias/case-normalized match is required. MOD-0136 may share the
audience and Client ID but may not reuse the `fpa.scenario-planning.*` operation namespace, any permission below,
the event identity, signing identity or key namespace.

The FU16 operation/worker-to-permission manifest is exactly the following 16 entries. `delegated` means a signed
FU16 delegated-actor proof is mandatory. `worker-after-acceptance` is not a seventeenth permission or a delegated
user call: the worker may execute only an already accepted immutable run carrying the original actor/request
binding, and its service authority cannot submit a run or acquire the delegated actor's permission.

| Exact operation ID | Execution class | Exact permission |
|---|---|---|
| `fpa.scenario-planning.scenarios.read` | delegated | `fpa.scenario-planning.scenarios.read` |
| `fpa.scenario-planning.scenarios.create` | delegated | `fpa.scenario-planning.scenarios.create` |
| `fpa.scenario-planning.scenarios.update` | delegated | `fpa.scenario-planning.scenarios.update` |
| `fpa.scenario-planning.version-drafts.read` | delegated | `fpa.scenario-planning.version-drafts.read` |
| `fpa.scenario-planning.version-drafts.create` | delegated | `fpa.scenario-planning.version-drafts.create` |
| `fpa.scenario-planning.version-drafts.update` | delegated | `fpa.scenario-planning.version-drafts.update` |
| `fpa.scenario-planning.version-drafts.abandon` | delegated | `fpa.scenario-planning.version-drafts.abandon` |
| `fpa.scenario-planning.versions.read` | delegated | `fpa.scenario-planning.versions.read` |
| `fpa.scenario-planning.versions.publish` | delegated | `fpa.scenario-planning.versions.publish` |
| `fpa.scenario-planning.versions.retire` | delegated | `fpa.scenario-planning.versions.retire` |
| `fpa.scenario-planning.comparators.read` | delegated | `fpa.scenario-planning.comparators.read` |
| `fpa.scenario-planning.comparators.run` | delegated request + `fpa.scenario-planning.comparators.execute` worker-after-acceptance | `fpa.scenario-planning.comparators.run` |
| `fpa.scenario-planning.selections.read` | delegated | `fpa.scenario-planning.selections.read` |
| `fpa.scenario-planning.selections.replace` | delegated | `fpa.scenario-planning.selections.replace` |
| `fpa.scenario-planning.selections.close` | delegated | `fpa.scenario-planning.selections.close` |
| `fpa.scenario-planning.references.validate` | delegated S2S | `fpa.scenario-planning.references.validate` |

Every delegated request binds the exact audience, Client ID, tenant, effective actor, operation ID, permission and
FU16 canonical `request_hash`; body, method, path or tenant variation invalidates the proof. The immutable accepted
ComparatorRunDefinition additionally pins the originating `delegation_id`, effective actor, operation ID,
permission, request hash, correlation and causation. A worker service credential is separately authenticated and
authorized for `fpa.scenario-planning.comparators.execute`; it cannot manufacture or replace that originating
binding. Audit `actorId` remains the originating effective actor, while trusted provenance records the executing
service separately without adding a payload field.

### Gate I audit/event mapping — exact v1

`ScenarioPlanningAuditIntentSubmittedV1` uses MOD-0021 **Minimal Mutation Audit v1** and serializes exactly, in this
order, `auditIntentId`, `actorId`, `entityType`, `entityId`, `mutation`, `occurredAtUtc`. No tenant, producer,
correlation, permission, delegation, before/after, Scenario content, assumptions, comparator inputs/output or PPM
wrapper is present in the payload. `auditIntentId` equals envelope `EventId`; `occurredAtUtc` equals envelope
`OccurredAtUtc`. Tenant, producer, correlation/causation and signed delegation provenance come only from trusted
envelope/transport metadata.

The closed MOD-0021 mapping/allowlist is:

| Exact `entityType` | Exact allowed `mutation` values | MOD-0021 category / operation projection |
|---|---|---|
| `Scenario` | `Created`, `Updated`, `Archived` | `Integration` / `Create`, `Update`, `LifecycleTransition` respectively |
| `ScenarioVersionDraft` | `Created`, `Updated`, `Abandoned` | `Integration` / `Create`, `Update`, `Delete` respectively; `Delete` denotes draft abandon, never audit-record deletion |
| `ScenarioVersion` | `Published`, `Retired` | `Integration` / `Create`, `LifecycleTransition` respectively |
| `ComparatorRun` | `Requested`, `Started`, `Completed`, `Failed` | `Integration` / `Create`, `Execute`, `Execute`, `Execute` respectively; outcome is `Succeeded` except `Failed` → `Failed` |
| `ScenarioSelection` | `Replaced`, `Closed` | `Integration` / `Update`, `LifecycleTransition` respectively |

Unknown entity/mutation pairs, unknown/extra payload properties, aliases and case normalization are terminal
mapping failures. The producer-owned compatibility fixture contains the exact six-field canonical bytes, trusted
envelope metadata, FU16 delegated provenance and expected MOD-0021 projection for every allowlisted pair plus
negative unknown-field/value/version cases. MOD-0021 consumes through its transactional inbox + audit-outbox
boundary; MOD-0138 never reads/writes Platform audit storage or calls a synchronous audit append endpoint.

Business mutation, idempotency receipt and the producer-local audit intent/outbox row commit atomically. Insertion
failure rolls back the local mutation. Only after commit may the ScenarioPlanning outbox dispatcher call MOD-0035's
public `IEventBus`; controllers, handlers and comparator compute code never publish directly. The comparator worker
commits Started/Completed/Failed state plus its local intent atomically, then the same post-commit dispatcher path
publishes. Broker/Platform failure cannot roll back committed business state and follows MOD-0035 retry/DLQ/alarm.

MOD-0035 signed onboarding is exact and deny-by-default on
`Diten.FpaService + fpa.scenario-planning-audit-intent.submitted.v1 + 1 + diten.fpa.mod-0138.scenario-planning`.
Its producer-owned fixture binds the permanent shared transport URN, exact envelope, canonical six-field payload
bytes/length, scheme, signing identity, provisioned fixture key ID and expected signature. PPM signing identity,
key IDs, secrets, namespace, fixtures and derived material are forbidden. Active/previous rotation, bounded overlap,
expiry/revocation, freshness/skew, fixed-time verification, replay/conflict and redaction follow MOD-0035 unchanged.

The governance fixture root is exactly
`execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0138/audit/`:

| Fixture | Exact contract |
|---|---|
| `scenario-planning-audit-intent-submitted-v1.canonical.json` | Strict UTF-8, no BOM/newline/whitespace; exact property order `auditIntentId`, `actorId`, `entityType`, `entityId`, `mutation`, `occurredAtUtc`; 247 bytes |
| `scenario-planning-audit-intent-submitted-v1.expected-audit-event.json` | Exact MOD-0021 projection with `sourceModule = MOD-0138`, `sourceService = Diten.FpaService`, `Integration/Update/Succeeded` and numeric metadata `eventVersion = 1` |
| `scenario-planning-audit-intent-submitted-v1.schema.json` | Closed six-field schema plus exact entity/mutation pair allowlist; unknown/extra/mismatched pairs fail |
| `scenario-planning-audit-intent-submitted-v1.canonical.sha256` | Exact lowercase digest `db9385def75d885a581554e10ce38877408ed445ed3c4f62e17283b30830462b` plus exactly one final LF; 65 bytes, no filename/CRLF |
| `scenario-planning-audit-intent-submitted-v1.signing-vector.json` | MOD-0138-only test vector; numeric `eventVersion = 1`, exact envelope/signing-input fields, 32-byte test-only secret, payload and signing-input digests |
| `scenario-planning-audit-intent-submitted-v1.expected-signature.json` | Exact lowercase HMAC-SHA256 `05c7cdee1fa8933f00502460178a2b76fa4d68819a5d2e1801c118e0e68b47df` |
| `scenario-planning-audit-intent-submitted-v1.fixture-manifest.json` | One-case binding of raw canonical/checksum, exact schema identity, envelope, delegated provenance, expected projection and signing evidence; pair drift is terminal |
| `scenario-planning-audit-intent-submitted-v1.allowlist-matrix.json` | Full 5×12 Cartesian ledger: exactly 14 accepted pairs, 46 rejected cross-pairs, plus unknown/case/alias negatives |
| `scenario-planning-audit-intent-submitted-v1.contract-negative-matrix.json` | Raw-byte, checksum/pair, schema identity, envelope/module/event/version, projection and provenance fail-closed cases |
| `scenario-planning-audit-intent-submitted-v1.delegated-provenance.positive.json` | Exact tenant/effective/delegating actor, delegation, operation, permission, request binding and executing-service projections, including comparator worker separation |
| `scenario-planning-audit-intent-submitted-v1.delegated-provenance.negative.json` | Missing/broken/unverified provenance and tenant/actor/operation/permission/request/worker mismatch cases; no credential/token/raw claims |
| `scenario-planning-audit-intent-submitted-v1.non-delegated-provenance.matrix.json` | Explicit non-delegated acceptance with exact service/audience/client/module/tenant/actor/operation/permission and structural absence of every delegated proof/claim; the identical absence under the delegated profile is terminal |
| `scenario-planning-audit-intent-submitted-v1.production-validator.negative.json` | Five production-profile terminal-security cases proving the test signing identity, fixture KeyId and mathematically correct fixture HMAC remain outside the production allowlist, including production-compatible envelope and valid canonical/checksum-pair variants |

The exact schema identity is `$id = urn:diten:fpa:scenario-planning-audit-intent-submitted:v1` plus top-level
`x-producer = Diten.FpaService`, `x-moduleCode = MOD-0138`,
`x-eventName = fpa.scenario-planning-audit-intent.submitted.v1`, numeric integer `x-eventVersion = 1` and
`x-profile = MinimalMutationAuditV1`. Consumer verification reads the raw canonical bytes first, validates the
65-byte checksum record and digest before deserialization, then binds that digest to this schema identity, exact
envelope identity, delegated provenance and expected projection in one case. Missing, foreign or mismatched pairs
are terminal and create neither audit projection nor audit outbox item.

The test-only key ID is `mod-0138-scenario-planning-fixture-current`; the fixture secret bytes are `0x80..0x9f`.
They are public deterministic test evidence only and are forbidden in configuration, deployment or production.
The vector must not equal or derive from any MOD-0136 or PPM key/vector. Verification reconstructs the MOD-0035
newline-delimited signing prefix with numeric integer EventVersion `1`, appends the exact 247 canonical bytes without
reserialization, and compares the expected signature in fixed time. Any BOM, trailing newline, property reorder,
case normalization, reserialization, byte-count change, checksum mismatch or cross-module key/vector reuse fails.

The non-delegated provenance profile binds exact producer/executing service `Diten.FpaService`, audience
`diten-fpa-service`, Client ID `diten.fpa`, owner `MOD-0138`, tenant, actor, operation and permission while every
delegated proof and delegated audit claim is structurally absent; null/default placeholders do not count as absence.
The same identity tuple and absence under the delegated profile is a terminal security failure and creates no
projection or audit outbox item. This audit-ingress classification fixture grants no service-only authority to a
business endpoint and does not weaken the FU16 rule that delegated Scenario mutations require a valid proof.

Production validation must classify the profile and enforce the production signing-identity/KeyId allowlist before
accepting any signature as a production credential. The public test identity, fixture KeyId and correct lowercase
fixture HMAC are terminally rejected even with an otherwise production-compatible envelope or the valid 247-byte
payload/checksum pair. The rejection is an identity/KeyId allowlist failure, never `InvalidHmac`; it is non-retryable
and creates no projection/outbox. No production signing identity, KeyId, secret or credential is defined here.

The three PPM wrapper fixtures are bilateral governance fixtures only:

1. `InvestmentCaseScenarioVersionReferenceV1` with exact nested `ScenarioVersionReferenceV1`;
2. `InvestmentCaseComparatorOutputReferenceV1` with exact nested `ComparatorOutputReferenceV1`;
3. PPM `SelectedScenarioReferenceV1` with only the exact pinned `ScenarioVersionReferenceV1`.

Each independently round-trips against the exact PPM `1.0` wrapper and producer `1.0` tuple, rejects missing/extra
fields, payload copy, producer occurrence/current-state fields and unknown versions, and pins a bilateral checksum.
They authorize no executable adapter: MOD-0117 still owns adapter code, wrapper persistence/cardinality/lifecycle and
consumer audit; execution remains blocked pending its approved amendment, matching runtime evidence and explicit
production authority. `InvestmentCaseContextV1` remains shared typed context, not a fourth reference wrapper.

## 4. Entity Fields

`entity_base: EntityBase` is the tenant-owned logical contract required by the module-pack standard. The bounded
primary slice maps it to the module-local
`Diten.FpaService.Domain.Modules.ScenarioPlanning.Common.ScenarioPlanningEntityBase`, defined only at
`services/Diten.FpaService/src/Diten.FpaService.Domain/Modules/ScenarioPlanning/Common/ScenarioPlanningEntityBase.cs`.
It supplies exactly the common fields below and may not inherit from or reference an EnterpriseStrategy, Budgeting
or other service entity base. This is a scaffold design decision, not a collection/runtime allocation.
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

### Persistence composition — exact logical v1

The primary owner manifest is exactly
`services/Diten.FpaService/src/Diten.FpaService.Persistence/Modules/ScenarioPlanning/ScenarioPlanningPersistenceOwnerManifest.cs`.
The coordinated-fallback equivalent is exactly
`services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Persistence/ScenarioPlanningPersistenceOwnerManifest.cs`.
Exactly one selected topology may register the manifest. Every entry is ScenarioPlanning-owned, read/write unless
stated append-only, and forbidden to Budgeting repositories, contexts, sessions and transactions.

| Exact collection | Manifest entry / ownership |
|---|---|
| `fpa_scenario_planning_scenarios` | `Scenario`; aggregate root/read-write |
| `fpa_scenario_planning_version_drafts` | `ScenarioVersionDraft`; mutable staging/soft-delete |
| `fpa_scenario_planning_versions` | `ScenarioVersion`; immutable append-only |
| `fpa_scenario_planning_version_dispositions` | `ScenarioVersionDisposition`; append-only |
| `fpa_scenario_planning_comparator_runs` | `ComparatorRunDefinition`; immutable append-only |
| `fpa_scenario_planning_comparator_events` | `ComparatorExecutionEvent`; append-only |
| `fpa_scenario_planning_comparator_outputs` | `ComparatorOutput`; immutable append-only |
| `fpa_scenario_planning_selection_occurrences` | `ScenarioSelectionOccurrence`; append-only |
| `fpa_scenario_planning_selection_dispositions` | `ScenarioSelectionDisposition`; append-only |
| `fpa_scenario_planning_selection_fences` | `ScenarioSelectionFence`; CAS-only scope head |
| `fpa_scenario_planning_idempotency_receipts` | `ScenarioPlanningIdempotencyReceipt`; immutable key/payload identity plus atomic outcome state |
| `fpa_scenario_planning_outbox` | local audit/event intent envelope; append-only, post-commit dispatch state only |

The exact tenant-first named index matrix is closed below. Key order is normative; `ux` means unique and `ix`
non-unique. No collection has a TTL index.

| Exact index name | Collection | Ordered keys | Exact filter/purpose |
|---|---|---|---|
| `ux_sp_scenario_tenant_code` | `fpa_scenario_planning_scenarios` | `TenantId, Code` | unique where `IsDeleted=false` |
| `ix_sp_scenario_tenant_lifecycle_name` | `fpa_scenario_planning_scenarios` | `TenantId, Lifecycle, Name, _id` | register/list |
| `ux_sp_draft_tenant_scenario_open` | `fpa_scenario_planning_version_drafts` | `TenantId, ScenarioId` | unique where `IsDeleted=false AND DraftState=Open` |
| `ix_sp_draft_tenant_scenario_updated` | `fpa_scenario_planning_version_drafts` | `TenantId, ScenarioId, UpdatedAt, _id` | draft history/list |
| `ux_sp_version_tenant_scenario_number` | `fpa_scenario_planning_versions` | `TenantId, ScenarioId, ScenarioVersionNumber` | immutable business version |
| `ix_sp_version_tenant_scenario_published` | `fpa_scenario_planning_versions` | `TenantId, ScenarioId, PublishedAt, _id` | current/history resolution |
| `ux_sp_version_disposition_tenant_version_sequence` | `fpa_scenario_planning_version_dispositions` | `TenantId, ScenarioVersionId, SequenceNumber` | append order |
| `ix_sp_version_disposition_tenant_scenario_occurred` | `fpa_scenario_planning_version_dispositions` | `TenantId, ScenarioId, OccurredAt, _id` | history rebuild |
| `ux_sp_comparator_run_tenant_id` | `fpa_scenario_planning_comparator_runs` | `TenantId, ComparatorRunId` | immutable run identity |
| `ix_sp_comparator_run_tenant_requested` | `fpa_scenario_planning_comparator_runs` | `TenantId, RequestedAt, ComparatorRunId` | worker/list; digest is intentionally non-unique so explicit rerun can create new IDs |
| `ux_sp_comparator_event_tenant_run_sequence` | `fpa_scenario_planning_comparator_events` | `TenantId, ComparatorRunId, SequenceNumber` | append order |
| `ix_sp_comparator_event_tenant_run_state` | `fpa_scenario_planning_comparator_events` | `TenantId, ComparatorRunId, State, OccurredAt` | terminal/current derivation |
| `ux_sp_comparator_output_tenant_run_version` | `fpa_scenario_planning_comparator_outputs` | `TenantId, ComparatorRunId, OutputVersion` | one v1 success per run |
| `ux_sp_comparator_output_tenant_id` | `fpa_scenario_planning_comparator_outputs` | `TenantId, ComparatorOutputId` | reference resolution |
| `ux_sp_selection_occurrence_tenant_scope_revision` | `fpa_scenario_planning_selection_occurrences` | `TenantId, ScopeToken, SelectionRevision` | immutable revision order |
| `ix_sp_selection_occurrence_tenant_scenario_version` | `fpa_scenario_planning_selection_occurrences` | `TenantId, ScenarioVersionId, OccurredAt` | reverse validation/history |
| `ux_sp_selection_disposition_tenant_occurrence_sequence` | `fpa_scenario_planning_selection_dispositions` | `TenantId, SelectionOccurrenceId, SequenceNumber` | append order |
| `ix_sp_selection_disposition_tenant_scope_occurred` | `fpa_scenario_planning_selection_dispositions` | `TenantId, ScopeToken, OccurredAt, _id` | effective-state rebuild |
| `ux_sp_selection_fence_tenant_scope` | `fpa_scenario_planning_selection_fences` | `TenantId, ScopeToken` | one CAS head per typed InvestmentCase scope |
| `ux_sp_receipt_tenant_principal_operation_key` | `fpa_scenario_planning_idempotency_receipts` | `TenantId, PrincipalIdentity, Operation, IdempotencyKey` | exact idempotency identity; no TTL |
| `ix_sp_receipt_tenant_state_updated` | `fpa_scenario_planning_idempotency_receipts` | `TenantId, ReceiptState, UpdatedAt, _id` | unknown-commit reconciliation; no TTL |
| `ux_sp_outbox_tenant_message` | `fpa_scenario_planning_outbox` | `TenantId, MessageId` | local intent identity |
| `ix_sp_outbox_tenant_dispatch_created` | `fpa_scenario_planning_outbox` | `TenantId, DispatchedAt, CreatedAt, _id` | post-commit dispatcher scan; no TTL |

Collection aliases, discriminator-based sharing, unlisted collection access and index-name reuse are forbidden.
The manifest fails startup on a missing/extra entry, duplicate owner, key/filter mismatch, any TTL, or collision
with Budgeting's manifest. The named slice may compile the declarative manifest/mappings only; physical collection
or index creation remains a later runtime implementation authority.

### Transaction, receipt and replay composition — exact logical v1

- **Draft/publication:** one Mongo replica-set transaction CASes the Scenario publication fence, verifies/consumes
  the one open draft, allocates the next monotonic `ScenarioVersionNumber`, inserts immutable ScenarioVersion and
  required disposition, commits the idempotency receipt and local audit/outbox intents. Concurrent open-draft or
  publication losers are 409; no partial version, number gap caused by a committed loser, or orphan intent remains.
- **Comparator request/completion:** request acceptance transaction inserts immutable RunDefinition, Requested
  event, committed receipt and local intents. It does not compute. A separate worker completion transaction appends
  Completed and inserts the single immutable Output plus local intents; failure appends Failed plus local intents.
  Output uniqueness is fenced by `(TenantId, ComparatorRunId, OutputVersion)`. Explicit rerun uses a new key and
  new Run/Output IDs; an exact replay returns the original receipt locator.
- **Selection:** replace/close is one transaction that CASes the tenant+ScopeToken fence and appends the new
  occurrence and prior disposition/closure, committed receipt and local intents. No occurrence is updated and no
  current flag is stored; a stale fence loses with 409 and deterministic history rebuild must equal the fence head.
- These transactions can enlist only collections listed in this manifest. Budgeting repository, collection,
  index, Mongo context, client session or transaction participation is zero; BudgetVersion validation occurs
  before the local transaction through the exact typed/versioned provider contract and is never revalidated by
  direct persistence access inside the transaction.

For every mutating operation, canonical request bytes use an operation-specific closed property allowlist, stated
property order, UTF-8/no-BOM/no-newline, NFC strings, lowercase canonical-D UUIDs and the §4 number/date rules.
`PayloadHash` is lowercase SHA-256 of those bytes. The receipt identity is exactly TenantId + PrincipalIdentity +
Operation + IdempotencyKey and records PayloadHash, `Pending|Committed|Aborted`, correlation, created/updated time,
and a stable opaque response locator/status; it never stores assumptions, comparator output or foreign payload.

- Same receipt identity plus same PayloadHash returns the original committed outcome without duplicate business,
  audit, outbox or worker work. Same identity plus a different PayloadHash is 409.
- On an unknown Mongo commit result, only commit is retried on the original transaction/session. Business handlers,
  validation, computation and inserts are not rerun. After commit retry, a majority read reconciles the receipt:
  `Committed` replays; definitively `Aborted` permits a fresh transaction under the same hash; absent/Pending or
  conflicting evidence that remains ambiguous returns 503 and schedules no duplicate work.
- V1 idempotency receipts have no TTL, purge or expiry path. Their retention matches the immutable authoritative
  history; a future retention change requires a versioned governance amendment and reconciliation proof.
- Business mutation, committed receipt and local audit/event intents are atomic. Audit-intent/outbox insertion
  failure aborts the transaction. After commit, delivery uses only MOD-0035 `IEventBus` through the outbox
  dispatcher; handlers/controllers cannot publish directly, write Platform audit/event collections or join a
  distributed transaction. The exact §3 event/schema/signing contract is closed governance; fixture approval,
  credentials, consumer materialization and runtime wiring remain open blockers.

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

### Gate I-B Scenario local validation provider amendment R2 — AUTHORIZED / LOCAL-ONLY / NON-PRODUCTION

Control Tower authorizes the owner-side read-only validation provider required by MOD-0117 §4.9.1 and §4.9.3.
The exact operation and permission are both `fpa.scenario-planning.references.validate`; audience is
`diten-fpa-service`, Client ID `diten.fpa`, owner `MOD-0138`, and protocol scope
`diten.s2s.delegated.invoke`. The shared workload identity never merges MOD-0138 with MOD-0136 authority.

The provider accepts only exact five-field `ScenarioVersionReferenceV1` or
`ComparatorOutputReferenceV1`, the three exact PPM wrapper identities fixed in this pack, and exact
`ppm.investment-case-context/1.0`. `HistoricalResolve` and `NewReferenceEligibility` apply to all three
wrappers; `CurrentSelectionEligibility` is valid only for the selected-scenario wrapper and is `400` for
scenario-version and comparator-output wrappers. Exact results are `400/401/403/404/409/503`; non-disclosable
state is `404`. Success returns only the exact reference plus lifecycle, disposition and current-selection
facts; assumptions, comparator inputs/algorithm/ranking/metrics/output payload and selection history are
forbidden.

The exact owner-local route is `POST /internal/v1/fpa/scenario-planning/references/validate`. It is
`[Authorize]`, service-network-only, has no Gateway/browser route and remains default-off. Committed
configuration cannot enable it or contain URI/credential/key/secret material.

Additive implementation authority is limited to:

- `services/Diten.FpaService/src/Diten.FpaService.Application/Modules/ScenarioPlanning/GateI/**`;
- `services/Diten.FpaService/src/Diten.FpaService.Api/Modules/ScenarioPlanning/InternalScenarioReferenceValidationController.cs`;
- `services/Diten.FpaService/src/Diten.FpaService.Api/Modules/ScenarioPlanning/ScenarioPlanningModuleRegistration.cs`;
- `services/Diten.FpaService/src/Diten.FpaService.Api/Program.cs`, only to add the compile-time/default-off ScenarioPlanning module registration beside Budgeting; no host/run/listener;
- `services/Diten.FpaService/tests/Diten.FpaService.UnitTests/Modules/ScenarioPlanning/GateI/**`;
- `services/Diten.FpaService/tests/Diten.FpaService.ContractTests/Modules/ScenarioPlanning/GateI/**`;
- `services/Diten.FpaService/tests/Diten.FpaService.ArchitectureTests/Modules/ScenarioPlanning/GateI/**`.

An Application snapshot-reader port may be supplied by a test fake. Production Mongo wiring and transport
configuration remain later gates. Validation is query-pure: zero mutation/cache/receipt/audit/outbox. Direct
Budgeting persistence access, cache/LKG/local truth, frontend, Gateway, WorkCenter, production
key/secret/deployment/activation and push remain forbidden. This checkpoint alone does not complete atomic
Gate I-B or make it Local API-Test Ready.

### Gate I-B FU16 HTTP trusted-context adapter amendment — AUTHORIZED / DEFAULT-OFF / NON-PRODUCTION

Control Tower authorizes the ScenarioPlanning owner-local projection from a successfully authenticated
shared `Fu16TrustedRequestContext` into `ScenarioGateIContext`. The controller must select exact named scheme
`Diten.FU16.DelegatedActor`; default authentication and HS256 user/session fallback are forbidden. The
adapter requires exact ordinal `diten.fpa`, `diten-fpa-service`, `diten.s2s.delegated.invoke`,
`fpa.scenario-planning.references.validate` and its single MOD-0138 permission. The shared FPA workload is
not authority to accept Budgeting operation/permission, aliases, wildcards, case changes or extra claims.

Existing contract-discovery composition remains provider-disabled with the unavailable accessor. An
additive evidence registration may add only the named scheme and adapter when explicit shared FU16 options,
key provider and authority client are supplied externally. No such dependencies, key, credential, issuer,
audience, host, listener or committed activation are registered here.

Exact additive implementation paths are:

- `services/Diten.FpaService/src/Diten.FpaService.Api/Modules/ScenarioPlanning/Fu16ScenarioGateIContextAccessor.cs`;
- `services/Diten.FpaService/src/Diten.FpaService.Api/Modules/ScenarioPlanning/ScenarioPlanningModuleRegistration.cs`;
- `services/Diten.FpaService/src/Diten.FpaService.Api/Modules/ScenarioPlanning/InternalScenarioReferenceValidationController.cs`;
- `services/Diten.FpaService/src/Diten.FpaService.Api/Diten.FpaService.Api.csproj`, only for the shared `Diten.Platform.Common` project reference;
- `services/Diten.FpaService/tests/Diten.FpaService.UnitTests/Modules/ScenarioPlanning/GateI/Fu16ScenarioGateIContextTests.cs`;
- `services/Diten.FpaService/tests/Diten.FpaService.UnitTests/Diten.FpaService.UnitTests.csproj`, only for the API project reference;
- `services/Diten.FpaService/tests/Diten.FpaService.ArchitectureTests/Modules/ScenarioPlanning/GateI/Fu16ScenarioGateICompositionArchitectureTests.cs`;
- `services/Diten.FpaService/tests/Diten.FpaService.ArchitectureTests/Topology/FpaPrimaryTopologyTests.cs`, only to reconcile API-to-shared-auth and UnitTests-to-API references while all module isolation guards remain exact.

Acceptance requires exact-profile projection, Budgeting cross-profile rejection, named-scheme evidence,
preserved default-disabled composition, no Program/listener mutation and physical expected-red proof for
profile bypass, default-scheme fallback and trusted-context substitution. Production authority and
activation remain `none`; no Gateway, frontend or WorkCenter change is authorized.

## 5. Repo Scope

Current governance/fixture authoring scope is exactly:

- `execution/domains/enterprise-strategy-business-performance/module-packs/MOD-0138-scenario-planning.md`
- `execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0138/audit/**`

### Named implementation slice — MOD-0138 Core Scaffold & Contract-Test

Status: **READY-FOR-DEV / NON-RUNTIME**. The slice authorizes only compile-time project shells, ScenarioPlanning
domain/application contracts, declarative persistence ownership and pure unit/architecture/contract tests for the
binding primary profile. Its exact file scope is:

- Project shells only:
  `services/Diten.FpaService/src/Diten.FpaService.Api/Diten.FpaService.Api.csproj`,
  `services/Diten.FpaService/src/Diten.FpaService.Application/Diten.FpaService.Application.csproj`,
  `services/Diten.FpaService/src/Diten.FpaService.Domain/Diten.FpaService.Domain.csproj`,
  `services/Diten.FpaService/src/Diten.FpaService.Persistence/Diten.FpaService.Persistence.csproj`, and
  `services/Diten.FpaService/src/Diten.FpaService.Infrastructure/Diten.FpaService.Infrastructure.csproj`.
- `services/Diten.FpaService/src/Diten.FpaService.Domain/Modules/ScenarioPlanning/**`, limited to the exact §3–4
  entities/value objects, domain errors/interfaces, immutable comparator definitions/results/transitions and
  selection history/fence contracts.
- `services/Diten.FpaService/src/Diten.FpaService.Application/Modules/ScenarioPlanning/**`, limited to CQRS
  contracts/validators, canonicalization/digests, idempotency/transaction abstractions, typed reference contracts
  and a comparator work-submission interface/fake boundary; no scheduler or transport implementation.
- `services/Diten.FpaService/src/Diten.FpaService.Persistence/Modules/ScenarioPlanning/ScenarioPlanningPersistenceOwnerManifest.cs`
  plus `Mappings/**` and `Indexes/**` below that same module directory, declarative only; no connection, database,
  collection creation, migration or live Mongo execution.
- Test project shells and their ScenarioPlanning module scopes only:
  `services/Diten.FpaService/tests/Diten.FpaService.UnitTests/Diten.FpaService.UnitTests.csproj` plus
  `Modules/ScenarioPlanning/**`,
  `services/Diten.FpaService/tests/Diten.FpaService.ArchitectureTests/Diten.FpaService.ArchitectureTests.csproj`
  plus `Modules/ScenarioPlanning/**`, and
  `services/Diten.FpaService/tests/Diten.FpaService.ContractTests/Diten.FpaService.ContractTests.csproj` plus
  `Modules/ScenarioPlanning/**`.

The slice excludes the solution/root build registration, API `Program`/controllers/endpoints, Infrastructure source,
host composition/DI activation, Budgeting source, runtime configuration/topology selection, port binding, service
discovery/health/deployment, Mongo connection/collection creation/migrations/live transactions, IntegrationTests,
WorkerTests, production S2S/signing/credentials/vault, runtime `IEventBus` wiring, scheduler/worker host/broker/queue/
topic/DLQ/alarm, Gateway, frontend, WorkCenter, MOD-0117 adapters and every fallback-service file. Adding any excluded
surface requires a separate explicit implementation/runtime slice.

If and only if executable evidence later shows that a mandatory primary isolation invariant cannot be implemented
or fails, a separate coordinated-fallback decision may authorize this reserved five-layer ScenarioPlanning scope:

- `services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Api/**`
- `services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Application/**`
- `services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Domain/**`
- `services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Persistence/**`
- `services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Infrastructure/**`
- `services/Diten.ScenarioPlanningService/tests/Diten.ScenarioPlanningService.UnitTests/**`
- `services/Diten.ScenarioPlanningService/tests/Diten.ScenarioPlanningService.IntegrationTests/**`
- `services/Diten.ScenarioPlanningService/tests/Diten.ScenarioPlanningService.ArchitectureTests/**`
- `services/Diten.ScenarioPlanningService/tests/Diten.ScenarioPlanningService.WorkerTests/**`
- `services/Diten.ScenarioPlanningService/tests/Diten.ScenarioPlanningService.ContractTests/**`

The same fallback release must also authorize and activate the separate five-layer `Diten.BudgetingService`.
ScenarioPlanning-only fallback, partial/hybrid activation and primary+fallback dual authority are forbidden.

The future ScenarioPlanning-owned persistence allowlist/owner manifest location is
`services/Diten.FpaService/src/Diten.FpaService.Persistence/Modules/ScenarioPlanning/ScenarioPlanningPersistenceOwnerManifest.cs`
in primary, or
`services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Persistence/ScenarioPlanningPersistenceOwnerManifest.cs`
in coordinated fallback. §4 closes its exact logical entries. This named slice authorizes only the primary declarative
manifest; the fallback manifest remains reserved and non-authorized.

## 6. Protected Paths

- `.antigravity/**`
- `services/**` except the exact named §5 Core Scaffold & Contract-Test files; all `frontend/**` and `gateway/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent only)
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` and Archive paths
- Other domains' governance and runtime paths
- Existing `Diten.EnterpriseStrategyService` ScenarioType/scenario fields and collections
- Shared governance/runtime/frontend/Gateway files

## 7. Dependencies

| Dependency | Boundary | Gate |
|---|---|---|
| MOD-0136 Budgeting | Exact typed/versioned BudgetVersion provider contract (`BudgetVersionReferenceV1` and `fpa.currency-reference`/`1.0`); no shared SoR/type/repository/persistence | Foreign-reference resolve/eligibility only through the provider Application contract; neither pack grants runtime authority |
| MOD-0117 PPM | Gate I-B wrappers at authority checkpoint `a2c7ff5b...`; exact profiles in §4 | Governance fixtures only; consumer adapter/provider client remains non-executable until matching producer runtime evidence, approved consumer amendment and explicit runtime authority exist |
| MOD-0007 | Generic decision/rationale owner | Not duplicated here |
| MOD-0023 | Approval only when policy requires | Separate contract; never implied by selection |
| MOD-0072 | Actual realization owner | Not duplicated here |
| MOD-0018-FU16 `cfbfee00b24314819101faa88d7ed8f1c83c7bd2` | Exact permission/tenant entitlement enforcement | Pre-development design accepted; production principal/key/JWKS/replay/grant provisioning and executable evidence remain runtime gates |
| MOD-0021 `525f6275a91eab7892741d8239dc1d0390915c3c` | Exact §3 audit mapping and bilateral fixture acceptance | Pre-development PASS; consumer inbox/audit-outbox and live delivery evidence remain runtime gates |
| MOD-0035 `f1568ace514a33e951c652802347dd22ddd1ac11` | Exact signed-producer tuple and universal production rejection policy | Pre-development security policy PASS; production identity/keys/vault, broker and runtime `IEventBus` wiring remain runtime gates |
| DCP-006 | Gate I orchestration | Approved; OD-04 OPEN/PARTIAL |
| MongoDB replica set | Exact §4 transaction/receipt/unknown-commit design | Runtime prerequisite and executable evidence remain open |
| Comparator worker/runtime | Domain/application comparator semantics and work-submission boundary are closed in the named slice | Physical scheduler/worker host/broker/queue/DLQ/alarm/credentials are runtime activation gates, not Core Scaffold promotion blockers |

## 8. Runtime Constraints

- The primary profile is exactly one `Diten.FpaService` host with isolated `Modules/ScenarioPlanning` and
  `Modules/Budgeting` surfaces. It shares no domain type, repository, collection, index, Mongo context/session/
  transaction, idempotency/audit/outbox store or module DI registration with Budgeting.
- The logical topology selector is closed as exact `FpaTopologyProfile = Primary | CoordinatedFallback`. `Primary`
  means only `Diten.FpaService`; `CoordinatedFallback` means both `Diten.BudgetingService` and
  `Diten.ScenarioPlanningService`. Zero, partial fallback, mixed/hybrid, dual/simultaneous or parallel-authority
  activation fails startup. This design names no runtime configuration file and the named §5 slice cannot activate it.
- Cross-module access is only through producer-owned typed/versioned Application contracts. In particular,
  BudgetVersion foreign-reference resolution/eligibility cannot use a Budgeting entity, repository, persistence
  DTO, collection handle or Mongo session.
- Failure or non-implementability of any mandatory isolation row rejects the primary profile and selects the
  coordinated pair `Diten.BudgetingService` + `Diten.ScenarioPlanningService`. Primary and fallback are mutually
  exclusive; partial fallback, hybrid wiring, dual activation and ScenarioPlanning-only extraction are forbidden.
- `Diten.EnterpriseStrategyService` is prohibited for MOD-0138. MOD-0072/`OutcomeTracking` cannot be co-hosted in
  `Diten.FpaService` or either FP&A fallback service.
- MongoDB single-database multi-tenancy; every lookup is tenant-first. Active mutable records also filter
  `IsDeleted=false`; immutable history is resolved through disposition/quarantine policy, not ordinary delete.
- Tenant and actor derive only from authenticated server context; client `TenantId`/`ActorId` is forbidden.
- Gate I request identity is exactly audience `diten-fpa-service`, Client ID `diten.fpa` and owner `MOD-0138`.
  FU16 actor/request binding and the exact §3 operation manifest are fail-closed; service, entitlement, delegated
  permission and resource visibility remain independent gates.
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
- The sole audit event identity is `ScenarioPlanningAuditIntentSubmittedV1` /
  `fpa.scenario-planning-audit-intent.submitted.v1`; its payload and mapping are the exact §3 six-field/allowlist
  contract. Post-commit transport is only MOD-0035 `IEventBus` with module-unique signing onboarding.
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
- Canonical port-registry checkpoint `496ab31b490b98b11a514bb1a03993d3f6da7140` reserves planned primary
  `Diten.FpaService = 5012` and coordinated fallback `Diten.BudgetingService = 5014` plus
  `Diten.ScenarioPlanningService = 5015`. These collision-free reservations supersede this pack's earlier `TBD`
  statement but allocate no binding, listener, route, deployment or runtime authority. Ports `5014` and `5015`
  activate only together under a separately authorized fallback and never beside primary `5012`.

### Worker and migration boundaries

- Comparator compute is owned by a ScenarioPlanning worker process/host surface, never an API controller, CQRS
  command handler or request thread. API/application acceptance ends after the request transaction; it may signal
  only the ScenarioPlanning-owned logical work queue after commit. Inline fallback compute is forbidden.
- The worker authenticates under separate service authority for exact operation
  `fpa.scenario-planning.comparators.execute`; it neither presents nor inherits the delegated actor permission.
  It may process only an accepted immutable run carrying the original FU16 actor/request binding.
- ScenarioPlanning logically owns the comparator queue subscription, scheduler trigger, retry state and DLQ/alarm
  disposition. The closed retry profile remains initial attempt plus 10s/30s retries and 60s per attempt. The exact
  broker, queue/scheduler product, queue/topic/DLQ names, worker hosting, credentials and operations ownership remain
  review/runtime-activation evidence gates, not named Core Scaffold & Contract-Test promotion blockers. Until that
  infrastructure is separately authorized and proven, runtime readiness fails closed and run acceptance returns 503.
- Initial prototype migration is exact `NO-OP / quarantine-only`: it imports, transforms, backfills, dual-writes or
  promotes no EnterpriseStrategy `ScenarioType`/scenario record into authoritative collections. A read-only
  inventory may classify provenance and emit quarantine evidence in a separately approved migration artifact;
  unknown tenant/identity/version stays quarantined. Any later data movement requires a new expand/contract,
  reconciliation and rollback amendment.

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

In primary, MOD-0136 and MOD-0138 have separate five-layer module namespaces, collections, repositories, domain
types, permissions, handlers, DI registrations, Mongo contexts/sessions/transactions and persistence manifests.
Budgeting repository/collection/index/session/transaction access from ScenarioPlanning is forbidden. Project-
reference/type, namespace, DI graph, repository-interface, collection/index owner-manifest and instrumented Mongo
tests must prove the boundary. Only approved typed/versioned Application contracts may cross it; co-location never
authorizes a shared domain/persistence model or transaction. A failed row selects the complete coordinated
two-service fallback, never hybrid or partial activation.

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
- Producer-local intent records the exact six-field Minimal Mutation Audit v1 payload only; tenant, producer,
  correlation/causation, delegation and signing evidence remain trusted envelope/transport metadata. Assumptions,
  comparator output, PPM wrappers, raw permissions, tokens and secrets are excluded.
- Immutable audit covers Scenario create/update/archive; draft create/update/abandon; publish/retire; comparator
  request/start/complete/fail; and selection replace/close. It records target IDs/versions, actor/service,
  exact contract/algorithm versions and operation provenance, never secrets, raw tokens or analytical payload.

## 15. Gateway / API Routing Decision

Decision: **planned port reserved; no route allocated**. Primary port `5012` and coordinated-fallback ports
`5014`/`5015` come only from registry checkpoint `496ab31b...`; this pack does not bind them. If frontend/runtime is
later authorized, browser traffic must remain frontend `5001` → Gateway `5000`; direct service-port calls are
forbidden. Any Ocelot change is a separate integration-agent task after explicit runtime authority. The S2S
validation surface must not be browser-exposed.

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
- [ ] FU16 onboarding uses exact audience `diten-fpa-service`, Client ID `diten.fpa`, owner `MOD-0138`, all and
  only the unchanged 16 permissions and the exact §3 operation/worker mapping; MOD-0136 key/operation ownership
  collision fails registration/startup.
- [ ] `ScenarioPlanningAuditIntentSubmittedV1` / `fpa.scenario-planning-audit-intent.submitted.v1` serializes
  exactly the six Minimal Mutation Audit v1 fields and accepts only the exact Scenario/draft/version/comparator/
  selection entity/mutation pairs and MOD-0021 projections in §3.
- [ ] Every local mutation or comparator worker state change commits its receipt plus local intent/outbox atomically;
  only the post-commit dispatcher invokes `IEventBus`, and direct controller/handler/compute publication is rejected.
- [ ] MOD-0035 onboarding fixture binds the exact producer/name/version/signing-identity tuple with a module-unique
  scheme/key namespace and passes rotation, revocation, freshness, replay/conflict and unchanged PPM regression gates.
- [ ] All thirteen §3 audit fixtures satisfy their exact formats; canonical JSON remains minified 247-byte/no-newline,
  checksum record is exactly 64 lowercase hex + one LF, schema/checksum/envelope/projection/provenance bind as one
  pair, SHA-256 and signing input/signature recompute exactly, `ModuleCode = MOD-0138`, EventVersion is integer `1`,
  and MOD-0136/PPM key or vector reuse is rejected.
- [ ] The allowlist fixture enumerates exactly 60 Cartesian pairs (14 accepted, 46 rejected) plus unknown/case/alias
  negatives; every accepted row has exact MOD-0021 entity/operation/outcome and every rejected row is terminal with
  no audit projection/outbox.
- [ ] Delegated positive fixtures preserve effective/delegating actor, delegation, exact operation/permission,
  request hash and executing service; negative fixtures fail closed on missing/broken/unverified provenance and
  tenant/actor/operation/permission/request/worker mismatch without storing credentials, tokens or raw claims.
- [ ] The non-delegated fixture accepts the exact authenticated service/audience/client/module/tenant/actor/
  operation/permission tuple only when every delegated proof/claim is structurally absent; the same absence under
  the delegated profile is terminal and creates no audit projection/outbox.
- [ ] Every production-validator fixture terminally rejects the test signing identity, fixture KeyId and otherwise
  correct fixture HMAC at the production allowlist gate—including production-compatible-envelope and valid
  canonical/checksum-pair cases—without reporting `InvalidHmac` or introducing production credentials.
- [ ] Primary topology is exactly isolated `Modules/ScenarioPlanning` in `Diten.FpaService`; all applicable
  `ESBP-TOP-001`..`013` domain-config tests pass, including five-layer, DI/repository, persistence-owner,
  transaction, contract-only, security, S2S, audit, PPM no-copy and runtime-allocation negative proofs.
- [ ] Any primary isolation failure activates the coordinated `Diten.BudgetingService` plus
  `Diten.ScenarioPlanningService` fallback together; tests reject partial fallback, hybrid or dual activation.
- [ ] ScenarioPlanning cannot access Budgeting repositories, collections, indexes, Mongo contexts/sessions/
  transactions or module DI; BudgetVersion access is solely an exact typed/versioned provider contract.
- [ ] Architecture/deployment evidence rejects MOD-0138 placement in `Diten.EnterpriseStrategyService` and rejects
  MOD-0072/`OutcomeTracking` co-hosting in `Diten.FpaService` or either FP&A fallback service.
- [ ] The exact 12-collection owner manifest and 23 tenant-first named indexes in §4 have no duplicate name,
  Budgeting collision, unlisted access or TTL; startup fails closed on manifest drift.
- [ ] Publication, comparator request/completion and selection transactions match the exact §4 collection boundary,
  atomically include receipt plus local intents, and never enlist Budgeting persistence/session/transaction state.
- [ ] Request canonicalization and PayloadHash are deterministic; same key/hash replays, same key/different hash is
  409, and unknown commit performs commit-only retry plus majority receipt reconciliation with ambiguity returning 503.
- [ ] Comparator compute runs only in the worker boundary; controller/handler inline compute and direct publish are
  rejected, while absent authorized queue/scheduler/retry/DLQ infrastructure remains a fail-closed blocker.
- [ ] Initial prototype migration is NO-OP/quarantine-only and produces no authoritative import, backfill,
  transformation, dual-write or promotion.
- [ ] Existing ScenarioType/ES scenario fields remain quarantined prototype/migration evidence with no fallback/dual write.
- [ ] Authorized UI explicitly uses `_LayoutTenantShell`, Compact/DataTable v2 where applicable, seven-language localization, and no PPM comparator copy or approval action.
- [ ] Gate I-B remains OPEN until producer approval, matching consumer amendment, compatibility/security evidence and explicit runtime authority.
- [ ] The three bilateral PPM wrapper fixtures round-trip exact `1.0` shapes/checksums and remain non-executable;
  no fixture grants MOD-0117 adapter, persistence, lifecycle or runtime authority.
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
- Gate I onboarding contracts: exact FU16 audience/client/owner and 16-entry operation manifest; forged/wrong
  actor, tenant, request hash, operation, permission and MOD-0136 owner/key collision rejection; comparator request
  requires delegation while worker execution proves separate service authority plus immutable origin binding.
- Audit/event fixtures: reflection and canonical-byte tests enforce the exact six properties/order, event type/name/
  version, closed entity/mutation mapping and expected MOD-0021 projections; unknown fields/values/version and trusted
  metadata mismatch are terminal. Transaction faults prove local mutation+intent atomicity and post-commit-only
  `IEventBus`; signed onboarding proves the exact allowlist tuple, unique scheme/identity/key namespace, rotation,
  revocation, freshness, replay/conflict, redaction and unchanged PPM signed regression.
- Fixture closure command/test independently reads raw bytes, rejects BOM/newline/whitespace/order drift, validates
  the JSON Schema entity/mutation pair, recomputes the canonical SHA-256, reconstructs the signing input and verifies
  the exact HMAC with the test-only key. It also asserts numeric JSON `eventVersion = 1`, expected audit
  `sourceModule = MOD-0138`, and byte/key/vector inequality against every MOD-0136 and PPM fixture.
- Contract fixture tests enforce the exact schema `$id` and five identity annotations, the 65-byte checksum sidecar,
  manifest-bound schema/checksum/envelope/projection/provenance pairing, all 14 positive and 46 negative cross-pairs,
  unknown property/entity/mutation/case/alias inputs and the complete delegated provenance positive/negative matrix.
- Provenance-profile fixture tests prove the explicit non-delegated accepted tuple has no delegated proof or claims
  anywhere (null/default does not qualify), preserves only executing-service provenance, and fails terminally when
  the identical absence is classified as delegated.
- Production-validator fixture tests first recompute the unchanged test-only HMAC, then prove all five test tuple
  variants fail the production identity/KeyId allowlist with terminal security disposition, zero projection/outbox,
  no retry and no `InvalidHmac` oracle; fixture content contains no production secret, KeyId or credential.
- Architecture/isolation matrix mirrors [domain config](../domain-config.md#architecture-and-isolation-test-matrix):
  `ESBP-TOP-001` rejects zero/mixed/dual/partial profiles; `002` five-layer cross-types; `003` foreign repository/
  context DI; `004` shared/foreign collection or index ownership; `005` foreign session/transaction participation;
  `006` non-contract or persistence DTO leakage; `007` OutcomeTracking FP&A co-hosting; `008` EnterpriseStrategy
  placement; `009` tenant/auth failure; `010` public internal S2S; `011` non-atomic audit/outbox or duplicate delivery;
  `012` copied PPM truth or premature adapter execution; and `013` guessed port/credential/route/deployment authority.
- Primary-profile failure test selects both `Diten.BudgetingService` and `Diten.ScenarioPlanningService`; startup,
  config and deployment scans reject ScenarioPlanning-only extraction, primary/fallback hybrid and parallel authority.
- Persistence-isolation tests scan source and DI, inspect the ScenarioPlanning owner manifest, and instrument Mongo
  to prove no Budgeting repository, collection, index, context, session or transaction is reachable. BudgetVersion
  fixtures cross only the exact typed/versioned Application provider contract.
- Manifest/index golden tests assert all 12 exact collection names and 23 exact index names/key orders/filters,
  reject duplicates, aliases, TTLs, extra/missing entries and collisions against the Budgeting owner manifest.
- Replica-set fault tests cover one-open-draft and monotonic publish races, comparator acceptance/output uniqueness,
  selection fence CAS, atomic receipt/audit/outbox rollback, post-commit-only dispatch and zero foreign participants.
- Idempotency golden vectors canonicalize each mutation twice to identical bytes/hash; replay returns the stable
  locator, changed payload under the same key is 409, unknown-result injection retries commit only, and absent/
  Pending ambiguity after majority reconciliation is 503. Architecture tests prove no receipt TTL/index/purge.
- Worker boundary tests reject compute/retry loops and direct `IEventBus` calls in controllers/handlers; worker tests
  prove logical queue ownership, fixed retry/timeout, terminal DLQ/alarm and 503 when runtime infrastructure is not
  authorized. Migration tests prove initial NO-OP/quarantine-only leaves every authoritative collection untouched.
- PPM no-copy source/schema test: no assumption/calculation/ranking/output payload, selected boolean or local engine.
- Frontend/browser: Scenario Slim and VersionDraft Compact verifier/count evidence, permission actions, immutable edit denial, stale-selection 409 flow,
  pinned comparator versions, seven-language/RTL/accessibility, no Approve/Reject, no browser mock/localStorage truth.
- Prototype quarantine/migration: inventory and negative import/collection/route tests; deterministic idempotent dry-run,
  unknown-tenant quarantine, explicit mapping/provenance, rollback proof and untouched source.
- Pack validation: exact MOD-ID preflight, Markdown links, `git diff --check`, staging empty, and no runtime diff.

## 18. Ready-for-dev Checklist

- [x] Control Tower fixed Scenario lifecycle, one-open-draft/publication, AssumptionV1, Budget-only references,
  canonical JSON/hash vectors, BaselineDeltaV1, InvestmentCase selection, no-purge retention and UI 3/10/2/2.
- [x] Reconcile the binding primary `Diten.FpaService/Modules/ScenarioPlanning` topology and coordinated
  `Diten.BudgetingService` + `Diten.ScenarioPlanningService` fail-closed fallback with domain config.
- [x] Close the exact design projection for `ESBP-TOP-001`..`013`: named Unit/Architecture/Contract suites,
  negative invariants, primary-only bounded scope and protected exclusions. Executable DI/Mongo/startup/deployment
  PASS remains a review/runtime gate; a primary failure requires the complete coordinated fallback.
- [x] Close the exact ScenarioPlanning collection/index matrix, owner-manifest path/entries, transaction boundaries,
  canonical request/hash/receipt/replay, unknown-commit reconciliation, no-TTL receipts and local outbox composition.
- [x] Fix the module-local `ScenarioPlanningEntityBase`, exact bounded project/test paths, logical exact-one topology
  profile and planned collision-free ports `5012` / coordinated `5014+5015`; runtime configuration, binding, route,
  host composition and deployment remain outside the named slice.
- [x] Authorize only the §5 `MOD-0138 Core Scaffold & Contract-Test` slice and exclude API/Infrastructure source,
  live Mongo, Integration/Worker tests, Budgeting/fallback, frontend/Gateway/WorkCenter and deployment surfaces.
- [ ] Approve/provision dedicated S2S identity, exact audience/credential/rotation and delegated actor propagation.
- [ ] Complete MOD-0018 entitlement/permission provisioning and runtime evidence.
- [x] Reconcile FU16 `cfbfee00...`, MOD-0021 `525f6275...` and MOD-0035 `f1568ace...`: exact 16-key owner
  manifest, bilateral audit fixtures and universal signed-producer production-rejection policy pass pre-development.
- [ ] Provision/approve the declared S2S/signing credentials, consumer fixtures and runtime `IEventBus` wiring evidence.
- [x] Close comparator domain/application semantics, immutable accepted-run origin binding and fake work-submission
  boundary for the core slice; real scheduler/worker host/broker/queue/DLQ/alarm/credentials remain a runtime gate.
- [x] Close initial migration disposition as NO-OP/quarantine-only; any later movement needs a separate approved slice.
- [ ] Record explicit runtime authority, including approved bilateral fixtures and executable MOD-0117 I-B Scenario amendment evidence.

All pre-development design/scope/test decisions are closed. Unchecked items above require executable infrastructure,
credentials, consumer/runtime wiring or bilateral runtime authority; they do not revoke the named ready-for-dev
handoff, but they block review/runtime activation and production use. `production_authority: none` remains exact.

## 19. Implementation Notes

Topology comparison:

| Option | Assessment |
|---|---|
| A. Isolated `Modules/ScenarioPlanning` in future `Diten.FpaService` with isolated MOD-0136 `Modules/Budgeting` | **Binding primary.** Separate five-layer surfaces, persistence/DI/session/transaction ownership and typed/versioned Application contracts only. |
| B. Coordinated `Diten.BudgetingService` + `Diten.ScenarioPlanningService` | **Only fail-closed fallback.** Both five-layer services activate together when any mandatory primary isolation test cannot be implemented or fails. ScenarioPlanning-only extraction is forbidden. |
| C. `Diten.EnterpriseStrategyService` | **Prohibited.** Existing proximity is prototype/migration evidence, never Scenario Planning SoR authority. |

Primary and fallback are mutually exclusive profiles. Hybrid, partial, simultaneous or parallel-authority
activation is forbidden. MOD-0072/`OutcomeTracking` cannot be hosted in the primary or either fallback FP&A service.

Existing `ScenarioType` and Enterprise Strategy scenario fields are quarantined evidence. A later migration slice
must inventory/classify records, reject default-tenant assignment, prohibit dual-read/dual-write and automatic
promotion, prove deterministic dry-run and mapping, verify collections/cutover, and prove rollback before authority.

MOD-0138 owns producer schemas, authoritative validation, supported-version/deprecation policy and golden producer
fixtures. MOD-0117 owns consumer cardinality/storage/transitions, adapter behavior and pinned supported versions.
Compatibility approval is bilateral; producer change never grants consumer runtime authority. Permission and
contract names are closed governance identities but are not activated as runtime literals by this promotion.

PPM checkpoint `a2c7ff5bbfd3ef2b4e3c8c15eab804db58d62520` was inspected read-only. It closes consumer
wrapper/context/mode governance only. MOD-0117 remains `review`, DCP-006 OD-04 remains OPEN/PARTIAL, and no producer
contract approval, topology/transport choice, adapter implementation or runtime authority is inferred here.

DCP-002 exact preflight command: `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0138 --name "Scenario Planning"`.

### Change log

| Date | Change | Authority effect |
|---|---|---|
| 2026-08-03 | Closed A-class Scenario semantics: Active/Archived lifecycle, one-open-draft publication, exact AssumptionV1 and Budget-only refs, canonical JSON plus two normative SHA-256 vectors, deterministic BaselineDeltaV1, InvestmentCase selection history/fence, no-purge retention and UI counts 3/10/2/2. | Governance semantics only; status remains draft and no runtime authority/route/port is granted. |
| 2026-08-03 | Reconciled the domain-config topology: isolated ScenarioPlanning in primary `Diten.FpaService`, coordinated two-service fallback, exact future logical scopes and isolation matrix. | Physical planning boundary only; no scaffold, route, port, deployment or production authority. |
| 2026-08-04 | Closed core composition: exact owner manifest, 12 collections, 23 tenant-first indexes, three transaction families, canonical idempotency/unknown-commit handling, local outbox, worker boundary and NO-OP migration. | Logical design only; exact events, worker infrastructure, S2S, port, deployment and MOD-0117 activation remain blocked. |
| 2026-08-04 | Reconciled Gate I onboarding against FU16 `40e38905`, MOD-0021 `de81a135` and MOD-0035 `23f290ec`: exact FPA audience/client/owner, unchanged 16-key operation manifest, FU16 actor/request binding, audit event and six-field mapping, signed onboarding, worker separation and three bilateral PPM wrapper fixtures. | Governance contract only; status remains `draft`, `production_authority: none`, A-class semantics and canonical hash vectors are unchanged, and credentials/runtime/adapter authority remain blocked. |
| 2026-08-04 | Added the six exact MOD-0138 audit/signing fixtures: canonical payload/schema/MOD-0021 projection, SHA-256 and module-unique test-only signing vector/signature; fixed `ModuleCode = MOD-0138` and numeric `EventVersion = 1`. | Fixture governance only; status remains `draft`, `production_authority: none`, the 16 permissions and existing Scenario canonical hashes are unchanged, and no runtime key/configuration/adapter authority is granted. |
| 2026-08-04 | Completed MOD-0021 checkpoint reconciliation: checksum final-LF, exact schema `$id`/identity tuple, manifest-bound schema/checksum pair, 60-row allowlist ledger and delegated provenance/negative matrices. | Governance fixtures only; canonical audit payload remains 247 bytes with SHA `db9385...462b`, existing signature and Scenario 309/623-byte hashes remain unchanged, and no runtime/consumer authority is granted. |
| 2026-08-04 | Closed final audit-security fixture gaps: explicit non-delegated provenance/absence acceptance plus delegated-profile fail-closed evidence, and five universal production-validator rejections for the test identity, fixture KeyId and correct test-only HMAC. | Governance fixture evidence only; production credentials/authority remain absent, while the 16 permissions, canonical 247-byte payload/SHA/HMAC and Scenario business hashes remain unchanged. |
| 2026-08-04 | Reconciled canonical ports `5012` / coordinated `5014+5015`, FU16 `cfbfee00`, MOD-0021 `525f6275` and MOD-0035 `f1568ace`; fixed the module-local EntityBase, exact topology profile and bounded Core Scaffold & Contract-Test scope, then promoted to `ready-for-dev`. | Named non-runtime implementation handoff only; `production_authority: none`, exact 16 permissions, A-class Scenario semantics, 309/623-byte hashes and all audit fixtures remain unchanged. Runtime host/config/route/Mongo/S2S/signing/eventing/worker/deployment and MOD-0117 adapter authority remain blocked. |

## 20. Follow-up Items

- Implement and prove the domain-config isolation matrix; any primary failure requires the coordinated
  `Diten.BudgetingService` + `Diten.ScenarioPlanningService` fallback.
- Implement the bounded module-local base/project/test scaffold. Authorize host composition, runtime topology-profile
  configuration, port binding, routes, deployment and Gateway only in separate explicit slices.
- Provision the declared S2S identity/audience/credential rotation and FU16 delegated actor/request binding.
- Complete MOD-0018 permission/entitlement provisioning evidence.
- Implement the accepted MOD-0021 consumer and MOD-0035 producer runtime wiring; prove transactional inbox/outbox,
  post-commit `IEventBus` and live delivery without changing the approved governance identities or test-key deny set.
- Select/prove worker host, broker, queue/scheduler/retry/DLQ names, credentials and operations ownership.
- Keep initial migration NO-OP/quarantine-only; authorize a separate expand/contract amendment before any data movement.
- Record explicit runtime authority after bilateral fixtures and executable MOD-0117 I-B Scenario amendment approval.
