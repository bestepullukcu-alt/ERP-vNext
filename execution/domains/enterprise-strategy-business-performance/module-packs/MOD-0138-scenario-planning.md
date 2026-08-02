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
form_field_count: 11
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
| Aggregate | `Scenario` | Tenant-owned stable register/container; active Code unique per tenant |
| Staging aggregate | `ScenarioVersionDraft` | Mutable working content under an exact concurrency fence; abandon may soft-delete |
| Immutable entity | `ScenarioVersion` | Publishing atomically creates/seals it; published content is never edited |
| Value set | `ScenarioAssumption` / `ScenarioReferenceSet` | Draft-owned during construction, normalized and sealed into published content; no foreign payload copy |
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
| Scenario | `Lifecycle` | enum | Draft/Active/Archived proposal; exact lifecycle is open |
| ScenarioVersionDraft | `ScenarioId`, `DraftId` | Guid | Same-tenant active Scenario; mutable staging identity |
| ScenarioVersionDraft | `VersionLabel`, `EffectiveFrom`, `EffectiveTo`, `Notes` | bounded scalar/date | Ordered dates; expected technical fence on every mutation |
| ScenarioVersionDraft | `Assumptions`, `ForeignReferences` | bounded typed set | Mutable until publish; exact immutable foreign refs and contract versions; no payload snapshots |
| ScenarioVersion | `ScenarioId`, `ScenarioVersionNumber` | Guid/positive integer | Unique `(TenantId, ScenarioId, ScenarioVersionNumber)`; server assigned at publish |
| ScenarioVersion | sealed content/provenance | immutable snapshot | Publication actor/time, source DraftId, canonicalization version and canonical digest atomically sealed |
| ScenarioVersion | disposition history | append-only events | Supersede/retire never modifies published bytes; historical resolvability remains distinct from new-link eligibility |
| ComparatorRunDefinition | `ScenarioVersionReferences` | 2..50 refs | Exact distinct published pins; immutable foreign refs include their contract versions |
| ComparatorRunDefinition | `ComparatorKey`, `AlgorithmVersion`, `NormalizationVersion`, `ParameterSchemaVersion`, `OutputSchemaVersion` | string | Exact supported immutable literals |
| ComparatorRunDefinition | `RequestDigest`, `InputEvidenceDigest`, `IdempotencyKeyHash`, `ExecutionEngineVersion` | digest/string | Engine version required where material; nondeterminism additionally pins approved seed/model/runtime provenance |
| ComparatorExecutionEvent | attempt/status/provenance | append-only event | Requested/started/completed/failed/cancelled receipts append; no terminal rewrite |
| ComparatorOutput | `ComparatorRunId`, `OutputVersion` | Guid/integer | One successful output per run proposed; exact policy open |
| ComparatorOutput | `ResultDigest`, output schema/provenance | digest/versioned metadata | Immutable after success; PPM reference excludes algorithm, ranking, metrics and payload |
| Selection occurrence | `SelectionOccurrenceId`, `SelectionRevision` | Guid/positive integer | Immutable occurrence/revision identity |
| Selection occurrence | exact typed selection context | closed typed reference | Profile remains OPEN until scope is approved; never owns InvestmentCase |
| Selection occurrence | `SelectedScenarioVersionReference` | `ScenarioVersionReferenceV1` | Published and eligible when selected |
| Selection disposition | closes/replaces occurrence | append-only event | Actor/time/reason/correlation; no mutable current-state field on occurrence/history |
| Selection fence | tenant + typed scope token | concurrency control | Transactionally prevents more than one effective selection; effective state derives from history |

Required indexes are tenant-first, include `IsDeleted=false` only for mutable/soft-deletable records, and include
active Scenario Code, Scenario business version, idempotency receipt and Run/Output identity. The transactional
scope fence plus immutable occurrence/disposition history proves at most one effective selection without adding
a mutable current-state flag to an append-only revision.

### Gate I-B minimal reference contract — proposed exact v1

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

Future runtime paths remain `TBD` until topology, field inventory, contracts, migration and explicit production
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
| MOD-0136 Budgeting | Draft sibling proposes the same future FPA host; no shared SoR, domain type or repository | Contract-only coordination; neither pack grants runtime authority |
| MOD-0117 PPM | Gate I-B wrappers at authority checkpoint `a2c7ff5b...`; exact profiles in §4 | Governance shape closed; executable consumer amendment and runtime authority still required |
| MOD-0007 | Generic decision/rationale owner | Not duplicated here |
| MOD-0023 | Approval only when policy requires | Separate contract; never implied by selection |
| MOD-0072 | Actual realization owner | Not duplicated here |
| MOD-0018 | Exact permission/tenant entitlement enforcement | Runtime evidence required |
| MOD-0021 / MOD-0035 | Immutable audit/eventing mechanics | Exact integration remains open |
| DCP-006 | Gate I orchestration | Approved; OD-04 OPEN/PARTIAL |
| MongoDB replica set | Atomic publish/output/selection/idempotency/audit persistence | Runtime prerequisite |
| Comparator algorithm/artifact catalog | Deterministic version retention and compute provenance | Owner, SLA and retention open |

## 8. Runtime Constraints

- MongoDB single-database multi-tenancy; every lookup is tenant-first. Active mutable records also filter
  `IsDeleted=false`; immutable history is resolved through disposition/quarantine policy, not ordinary delete.
- Tenant and actor derive only from authenticated server context; client `TenantId`/`ActorId` is forbidden.
- Publishing consumes an exact draft fence and atomically creates the immutable ScenarioVersion, monotonic number,
  publication provenance, canonicalization version, canonical digest, idempotency receipt and audit intent.
- Published content is never edited. Supersession/retirement is append-only disposition history. Abandoned drafts
  may be soft-deleted; published versions, completed outputs and selection history cannot be CRUD-deleted.
  Exceptional purge requires separate compliance authority, and PPM history is never rewritten.
- ComparatorRunDefinition is immutable after acceptance. Execution progress is append-only events/receipts and a
  successful ComparatorOutput is immutable. Exact idempotent duplicate returns the original Run/result; explicit
  rerun creates new Run/Output IDs and never rewrites terminal history.
- Reproducibility pins exact immutable input references and contract versions, algorithm/version, normalization,
  parameter/output schema, request/input/result digests and material engine provenance. Nondeterministic algorithms
  are prohibited unless an approved seed/model/runtime provenance makes the outcome reproducible.
- Financial data is never a second Budget SoR: compute rereads an immutable authoritative producer contract or
  retains only an approved canonical evidence/input digest, never budget amounts/lines as MOD-0138 truth.
- Selection replace/close atomically appends immutable occurrence/disposition history and advances a transactional
  scope fence. Effective current selection is derived. Rollback is a compensating append-only occurrence.
- Mutation, idempotency receipt and audit intent persistence is atomic; audit failure rolls back the mutation.
- Idempotency scope is TenantId + actor/service identity + exact operation + key, with canonical request digest
  and stable original result. Same digest replays without duplicate compute/output/audit; another digest is 409.
  Retention must exceed retry and unknown-commit recovery windows so expiry cannot enable duplicate side effects.
- No local/cache/prototype fallback and no transport/dependency failure may be collapsed to 404.
- Draft limits: decoded/decompressed UTF-8 request body 256 KiB (oversize `413`) enforced before materialization;
  JSON depth 8, duplicate properties/invalid Unicode/unknown polymorphic kinds/compression bombs rejected;
  validation batch 100 refs; 2..50 scenarios; 200 assumptions; scalar assumptions only; key 128 and scalar string
  value 2000 characters; bounded comparator parameter count/keys/values; arbitrary nested JSON forbidden.
  Numbers are Decimal128-compatible only in `[-1e15, 1e15]`, precision 28, scale 8; binary float, silent rounding,
  NaN/Infinity and exponent overflow/underflow are rejected before allocation/compute. Internal summary metadata,
  if approved, is capped at 100 metrics with 128-character keys; reference responses remain payload-free.
- Immutable versions, run definitions, execution receipts, outputs, selection/audit/disposition history and
  quarantine records have no default TTL or CRUD delete. Exact retention/legal-hold rules are promotion blockers.
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
| ScenarioVersionDraft create/edit | VersionLabel, EffectiveFrom, EffectiveTo, Notes; repeatable assumption/reference row: AssumptionKey, ValueType, Value, UnitReference, PeriodReference, DimensionReferences, ProvenanceReference | **11** | Compact Create/Edit/Details + `_Form` |
| ComparatorRun request | ScenarioVersionReferences, ComparatorKey, AlgorithmVersion, ParameterSchemaVersion; repeatable ParameterKey, ParameterValue | 6 | Dedicated Slim-count action/full comparison page |
| Selection replace/close | SelectionContextContract, SelectionContextKind, SelectionContextId, ScenarioVersionReference, ReasonCode | 5 | Slim-count action/modal; not approval |

The maximum actual primary create/edit surface is ScenarioVersionDraft at **11**, so frontmatter uses
`golden_reference: compact` and `form_field_count: 11` under the
[Compact reference](../../developer-enablement/module-packs/DEV-0001-golden-reference-compact.md). Repeatable-row
inputs count once per editable column. Route IDs, technical versions, digests, provenance and audit fields do not
count. Exact assumption row and selection picker representation remain promotion blockers; any change requires recount.

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

## 12. Validation Rules

| Field/boundary | Required | Rule / pre-check |
|---|---:|---|
| Tenant/Actor | Server only | Valid authenticated claims; payload/header override rejected |
| Scenario Code/Name | Yes | Strict decode, Trim/NFC, lengths above; Code active tenant unique |
| ScenarioVersionDraft | Mutate/publish | Complete normalized assumptions; exact technical fence; abandoned draft only may soft-delete |
| Published ScenarioVersion | Server seal | Positive monotonic number; atomic provenance/canonicalization/digest/audit; no update/delete |
| Assumption | Per schema | Unique key/dimension; scalar typed; finite bounded number; no nested object |
| Foreign reference | When supplied | Exact contract/kind/id/version; authoritative same-tenant validation; no payload |
| Comparator inputs | Yes | 2..50 distinct Published visible refs; supported compatible unit/currency/period policy |
| Algorithm/normalization | Yes | Exact available deterministic versions; pinned in result |
| Contract | Yes | Exact `fpa.scenario-planning-reference` and `1.0`; exact minimal shapes in §4 |
| Reference validation mode | Yes | `HistoricalResolve` / `NewReferenceEligibility` for scenario and comparator wrappers; selected-scenario wrapper alone also permits `CurrentSelectionEligibility` |
| PPM bilateral wrapper | Consumer persistence | Exact wrapper name/version/field allowlist in §4; nested producer tuple unmodified; unknown/extra fields fail |
| Current selection request | Selection validation only | Exact `ppm.investment-case-context` `1.0` plus pinned `ScenarioVersionReferenceV1`; no occurrence/history supplied by PPM |
| Selection | Replace/close | Published eligible reference; closed typed scope; expected fence/revision; one effective derived selection |
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
  request/start/complete/fail/cancel; and selection replace/close. It records target IDs/versions, actor/service,
  exact contract/algorithm versions and operation provenance, never secrets, raw tokens or analytical payload.

## 15. Gateway / API Routing Decision

Decision: **not allocated in this draft**. If frontend/runtime is later authorized, browser traffic must remain
frontend `5001` → Gateway `5000`; direct service-port calls are forbidden. Any Ocelot change is a separate
integration-agent task after topology and port approval. The S2S validation surface must not be browser-exposed.

## 16. Acceptance Criteria

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

- [ ] Business owner approves Scenario lifecycle, assumption schema and referenceability policy.
- [ ] Mutable draft, atomic publish, append-only disposition and immutable delete/retention/legal-hold/purge policy are exact.
- [ ] PPM `InvestmentCaseContextV1` selection context and current-validation profile are bilaterally approved;
  occurrence/disposition history and scope fence stay producer-owned. Any additional selection context remains open.
- [ ] Comparator count, unit/currency/period rules, algorithm catalog/retention and sync/async model are approved.
- [ ] Service topology, collection ownership, entity base, port and future routes are approved explicitly.
- [ ] Same-service architecture isolation tests pass, or separate service fallback is selected.
- [ ] MOD-0136 shared-contract boundary and MOD-0117 consumer amendment are approved.
- [ ] Exact PPM wrapper/context fixtures at checkpoint `a2c7ff5b...` and bilateral supported-version/deprecation matrix are approved.
- [ ] Exact minimal reference field allowlists, supported-version/deprecation matrix, security/S2S identity and audit/idempotency design are approved.
- [ ] Dedicated PPM identity/audience, credential owner/rotation, delegated-actor mechanism, unknown-commit recovery and retention windows are approved.
- [ ] Immutable output/event/selection/audit/idempotency/quarantine retention, legal hold and exceptional purge are approved.
- [ ] Prototype quarantine/migration/cutover/rollback plan is separately approved.
- [ ] Separate field counts remain Scenario 3, VersionDraft 11, Comparator 6, Selection 5; Compact governs the parent and tenant layout is confirmed.
- [ ] Backend/frontend/Gateway file contracts and seven-language keys are enumerated.
- [ ] Acceptance, test and compatibility evidence is executable; explicit production authority is recorded.
- [ ] Status is promoted by user from `draft` to `approved` or `ready-for-dev`.

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
contract names in this draft are governance proposals, not registered runtime literals.

PPM checkpoint `a2c7ff5bbfd3ef2b4e3c8c15eab804db58d62520` was inspected read-only. It closes consumer
wrapper/context/mode governance only. MOD-0117 remains `review`, DCP-006 OD-04 remains OPEN/PARTIAL, and no producer
contract approval, topology/transport choice, adapter implementation or runtime authority is inferred here.

DCP-002 exact preflight command: `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0138 --name "Scenario Planning"`.

## 20. Follow-up Items

- Approve one-open-draft policy, monotonic numbering, copy-on-new-draft provenance, canonicalization identity/vectors,
  archive/retirement and the exact historical/new-reference eligibility matrix.
- Finalize the assumption/reference row schema and picker representation; recount all surfaces if it changes.
- Approve comparator min/max, cross-Scenario comparisons, parameter/output schemas, algorithm/artifact owner and
  retention, normalization, engine/seed/model provenance, conversion policy, async attempts and output retention.
- Decide exact closed typed selection scope, occurrence/disposition shape, fence/rebuild semantics and whether a
  historical selection used where current is required returns 409. Any non-PPM occurrence-reference contract is a
  separately named/versioned follow-up and must not reuse the PPM selected-wrapper identity.
- Define immutable-history retention/legal hold and exceptional purge authority.
- Resolve final service/entity base/collections/port/Gateway paths and S2S identity/credential ownership.
- Coordinate MOD-0136 contracts and author the later MOD-0117 Gate I-B consumer amendment.
- Publish checksum/schema-identified bilateral fixtures and the two-dimensional producer/wrapper compatibility
  matrix; later request a separately approved executable MOD-0117 I-B Scenario amendment and explicit authority.
- Create a separate prototype quarantine/migration/cutover/rollback pack before touching existing ES fields.
- Recount UI fields and enumerate exact routes/L10n keys before status promotion.
