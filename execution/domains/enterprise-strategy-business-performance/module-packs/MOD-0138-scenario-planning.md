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
form_field_count: 9
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

- `Scenario`, `ScenarioVersion`, `ComparatorRun`, and `ComparatorOutput`.
- Scenario assumptions/reference set and its deterministic normalization.
- Selected-scenario truth and its immutable audit history.
- Authoritative Scenario/Comparator reference validation.

MOD-0138 does not own:

- `Budget` / `BudgetVersion` (MOD-0136).
- `Portfolio` / `InvestmentCase` (MOD-0117).
- Generic `Decision` / `Rationale` (MOD-0007).
- Approval workflow or `ApprovalOutcome` (MOD-0023).
- Actual realization (MOD-0072).
- A PPM-local scenario engine, selection truth, or comparator UI.
- Foreign payload snapshots. Foreign inputs remain opaque typed/versioned references.

Selection means “chosen analytical scenario”; it is not approval and creates no Approved/Rejected state.

## 3. Owned Objects

| Kind | Proposed object | Draft invariant |
|---|---|---|
| Aggregate | `Scenario` | Tenant-owned register/container; active Code unique per tenant |
| Entity | `ScenarioVersion` | Exactly one Scenario; published version immutable and permanently referenceable |
| Value set | `ScenarioAssumption` / `ScenarioReferenceSet` | Version-owned, normalized and sealed at publish; no foreign payload copy |
| Aggregate | `ComparatorRun` | Immutable request/receipt pinned to inputs, algorithm and normalization versions |
| Entity | `ComparatorOutput` | Completed output immutable; rerun creates new Run/Output |
| Aggregate/ledger | `ScenarioSelectionRevision` | Audited append-only selection change; one current selection per chosen scope |
| Contract | `fpa.scenario-planning-reference.v1` | Exact `ContractVersion = "1.0"`; bounded validation/reference contract |
| DTOs | `ScenarioReferenceV1`, `ComparatorOutputReferenceV1`, `SelectedScenarioReferenceV1` | Opaque typed/versioned references only |

Proposed permissions are exact, case-sensitive and non-wildcard:

- `fpa.scenario-planning.scenarios.read|create|update`
- `fpa.scenario-planning.versions.publish`
- `fpa.scenario-planning.comparators.read|run`
- `fpa.scenario-planning.selection.read|set`
- `fpa.scenario-planning.references.read` (delegated actor permission for dedicated PPM S2S validation only)

No endpoint, route, controller or collection is authorized by this draft.

## 4. Entity Fields

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
| ScenarioVersion | `ScenarioId` | Guid | Same-tenant active Scenario |
| ScenarioVersion | `ScenarioVersionNumber` | positive integer | Unique `(TenantId, ScenarioId, ScenarioVersionNumber)` |
| ScenarioVersion | `VersionLabel`, `EffectiveFrom`, `EffectiveTo` | bounded scalar/date | Ordered dates when both supplied |
| ScenarioVersion | `Status` | enum | Draft/Published/Superseded proposal |
| ScenarioVersion | `Assumptions`, `ForeignReferences` | bounded typed set | Canonical order; no duplicate key/dimension; foreign refs opaque |
| ScenarioVersion | `CanonicalDigest` | SHA-256 | Sealed on publish |
| ComparatorRun | `ScenarioVersionReferences` | 2..50 refs | Distinct published immutable refs; canonical ordering |
| ComparatorRun | `ComparatorKey`, `AlgorithmVersion`, `NormalizationVersion` | string | Exact supported immutable literals |
| ComparatorRun | `RequestDigest`, `IdempotencyKeyHash` | digest | Pins canonical input and replay identity |
| ComparatorRun | `Status`, `RequestedAtUtc`, `RequestedBy` | enum/audit | Append-only execution receipt |
| ComparatorOutput | `ComparatorRunId`, `OutputVersion` | Guid/integer | One successful output per run proposed; exact policy open |
| ComparatorOutput | `ResultDigest`, `SummaryMetrics` | digest/bounded map | Immutable; maximum 100 metrics; no foreign payload/blob |
| Selection | `SelectionId`, `SelectionRevision` | Guid/positive integer | Append-only audit identity |
| Selection | `ScopeContract`, `ScopeKind`, `ScopeId` | opaque typed ref | Exact selection scope is open; never owns InvestmentCase |
| Selection | `ScenarioReference` | `ScenarioReferenceV1` | Published/referenceable only |
| Selection | `IsCurrent`, `ChangedAtUtc`, `ChangedBy`, `ReasonCode` | state/audit | Atomic 0..1-current invariant per selected scope |

Required indexes are tenant-first, include `IsDeleted=false` where appropriate, and include active Scenario Code,
Scenario business version, idempotency receipt, Run/Output identity, and a partial unique current-selection key.

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
| MOD-0136 Budgeting | FP&A sibling; typed/versioned contracts only | Parent pack/topology coordination open |
| MOD-0117 PPM | Gate I-B opaque consumer | Consumer amendment and runtime authority required |
| MOD-0007 | Generic decision/rationale owner | Not duplicated here |
| MOD-0023 | Approval only when policy requires | Separate contract; never implied by selection |
| MOD-0072 | Actual realization owner | Not duplicated here |
| MOD-0018 | Exact permission/tenant entitlement enforcement | Runtime evidence required |
| MOD-0021 / MOD-0035 | Immutable audit/eventing mechanics | Exact integration remains open |
| DCP-006 | Gate I orchestration | Approved; OD-04 OPEN/PARTIAL |

## 8. Runtime Constraints

- MongoDB single-database multi-tenancy; every lookup is tenant-first and filters `IsDeleted=false`.
- Tenant and actor derive only from authenticated server context; client `TenantId`/`ActorId` is forbidden.
- Published ScenarioVersion and completed ComparatorRun/ComparatorOutput are append-only and immutable.
- Rerun creates a new ComparatorRun and ComparatorOutput. An exact idempotent replay returns the original result;
  the same key with another canonical digest returns 409.
- Comparator identity pins scenario references, algorithm name/version, normalization version and canonical digest.
- Selection set/replace/clear is atomic, audited, idempotent and optimistic-concurrency protected. Rollback is a new
  compensating selection revision, never history editing.
- Mutation/audit intent persistence is atomic; audit failure rolls back the mutation.
- No local/cache/prototype fallback and no transport/dependency failure may be collapsed to 404.
- Draft limits: request body 256 KiB; JSON depth 8; validation batch 100 refs; 2..50 scenarios; 200 assumptions
  per version; scalar assumptions only; decimal range `-1e15..1e15`, precision 28, scale 8; NaN/Infinity and
  exponent overflow forbidden. Names/codes 128, descriptions/reasons 2000, keys 128 characters.
- No port, Gateway route, service project, collection or production credential is allocated here.

## 9. Layout & Shell Contract

Draft UI choice: `shell: tenant`. Any later authorized Razor page must explicitly declare
`Layout = "_LayoutTenantShell"`; `_ViewStart.cshtml` defaults are insufficient. Proposed view location is
`Views/Fpa/ScenarioPlanning/`.

The nine counted create/edit inputs are Code, Name, Description, VersionLabel, EffectiveFrom, EffectiveTo,
AssumptionReferenceSet, ComparatorAlgorithmVersion and ComparatorParameters. This yields
`golden_reference: compact` under the [Compact reference](../../developer-enablement/module-packs/DEV-0001-golden-reference-compact.md).
Recount before approval if the field inventory changes. Selection is an action, not an approval or form field.

## 10. Backend File Convention

If runtime is later authorized, use the Compact CQRS convention: `Commands/`, `Queries/`,
`Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/`, and `ScenarioPlanningModels.cs`.
Commands/queries are records; handler names omit Command/Query suffix; validator names omit Command suffix.

MOD-0136 and MOD-0138 must have separate module boundaries, collections, repositories, handlers and DI
registrations. Only approved typed/versioned contract assemblies/ports may cross the boundary.

## 11. Frontend File Contract

Proposed surfaces:

- Scenario register: Compact DataTable v2 index/filter/table.
- Scenario/version authoring: separate Create/Edit/Details and `_Form.cshtml`; published edit is unavailable.
- Comparator: dedicated run/details/result-reference surface showing pinned input and algorithm versions.
- Selection: permission-gated set/replace/clear with concurrency and audit context; no Approve/Reject button.

The Compact set is `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`,
`_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `ScenarioPlanningIndex.cs`, `index.js`, and
`index.l10n.js`. PPM may later show opaque reference picker/status links only; it must not copy comparator UI,
assumptions, calculations, rankings or output payload.

## 12. Validation Rules

| Field/boundary | Required | Rule / pre-check |
|---|---:|---|
| Tenant/Actor | Server only | Valid authenticated claims; payload/header override rejected |
| Scenario Code/Name | Yes | Strict decode, Trim/NFC, lengths above; Code active tenant unique |
| Scenario version | Publish | Positive unique ordinal; complete normalized assumptions; expected technical Version |
| Assumption | Per schema | Unique key/dimension; scalar typed; finite bounded number; no nested object |
| Foreign reference | When supplied | Exact contract/kind/id/version; authoritative same-tenant validation; no payload |
| Comparator inputs | Yes | 2..50 distinct Published visible refs; supported compatible unit/currency/period policy |
| Algorithm/normalization | Yes | Exact available deterministic versions; pinned in result |
| Contract | Yes | Exact `fpa.scenario-planning-reference.v1` and `1.0` |
| Selection | Set/replace | Published reference; exact scope; expected revision; 0..1 current |
| Idempotency-Key | Mutation/run | 16..128 ASCII; tenant+operation scoped; canonical request SHA-256 |
| JSON/numeric/string | All | Enforce §8 before allocation/compute; invalid Unicode fails closed |

## 13. Failure Path to Verify

| Scenario | Expected |
|---|---|
| Malformed/invalid/out-of-bounds input | 400; no allocation, compute or mutation |
| Missing/invalid token or server tenant/actor context | 401 |
| Authenticated caller/actor lacks exact permission or S2S entitlement | 403 |
| Missing, deleted, cross-tenant, invisible or non-referenceable object | Indistinguishable 404 |
| Published mutation, stale Version, selection race, or idempotency digest mismatch | 409; no overwrite |
| Unknown contract/algorithm version, compute/dependency timeout/unavailable/malformed response | 503; no fallback |
| Same idempotency key and digest | Original outcome; no duplicate Run/Output/selection/audit |
| Caller cancellation | Propagated cancellation; not rewritten as 503 |

## 14. Authorization Convention

- Tenant UI/API uses authenticated tenant actor plus each exact permission listed in §3.
- Internal PPM validation requires a dedicated allowlisted S2S identity bound to the exact audience/contract **and**
  a separately validated delegated actor with `fpa.scenario-planning.references.read`.
- Authentication/audience and server context are established before authoritative tenant-first lookup.
- Service identity alone never bypasses delegated actor visibility/referenceability.
- Wildcard, alias, wrong-case and prefix permissions are denied. No approve/reject permission exists.
- Audit records tenant, actor, old/new opaque refs, timestamp, reason code, correlation/causation and hashed
  idempotency key; assumptions/output payload and raw secrets are excluded.

## 15. Gateway / API Routing Decision

Decision: **not allocated in this draft**. If frontend/runtime is later authorized, browser traffic must remain
frontend `5001` → Gateway `5000`; direct service-port calls are forbidden. Any Ocelot change is a separate
integration-agent task after topology and port approval. The S2S validation surface must not be browser-exposed.

## 16. Acceptance Criteria

- [ ] `fpa.scenario-planning-reference.v1` / `1.0` is approved with typed Scenario, ComparatorOutput and SelectedScenario references.
- [ ] `ScenarioReferenceV1` contains ScenarioId, ScenarioVersionId, positive ScenarioVersionNumber and authoritative immutable referenceability.
- [ ] `ComparatorOutputReferenceV1` contains RunId, OutputId, OutputVersion and exact compared ScenarioReference pins.
- [ ] InvestmentCase holds Scenario/Comparator references `0..n` and SelectedScenarioReference `0..1`; MOD-0138 owns selected truth.
- [ ] PPM persists only opaque contract/kind/IDs/versions; no assumptions, calculations, result payload or selected flag.
- [ ] Published versions and completed comparator results are immutable; rerun creates new IDs and pins deterministic versions.
- [ ] Selection is unique per approved scope, audited, idempotent and concurrency-safe; it never implies approval.
- [ ] Exact 400/401/403/404/409/503 semantics and §8 bounds are proven fail-closed.
- [ ] Same-service topology is permitted only when architecture tests prove MOD-0136/MOD-0138 isolation.
- [ ] Existing ScenarioType/ES scenario fields remain quarantined prototype/migration evidence with no fallback/dual write.
- [ ] Authorized UI explicitly uses `_LayoutTenantShell`, Compact/DataTable v2 where applicable, seven-language localization, and no PPM comparator copy or approval action.
- [ ] Gate I-B remains OPEN until producer approval, matching consumer amendment, compatibility/security evidence and explicit runtime authority.

## 17. Test Expectations

- Unit/domain: publish immutability, next-version isolation, deterministic canonical digest, algorithm/version pin,
  rerun-new-ID behavior, unique selection, compensating rollback and idempotency.
- Real Mongo integration: tenant-first/soft-delete behavior; unique/partial indexes; concurrent publish/selection and
  idempotency races; transaction fault injection proving mutation/output/receipt/audit atomicity.
- Contract: golden serialization for all v1 discriminators; exact compared-reference verification; 0..n/0..1
  consumer cardinalities; independently built producer/PPM fixtures and unknown-version 503.
- Security: exact permission matrix, dedicated S2S plus delegated actor, cross-tenant/invisible 404 equivalence,
  forged context rejection, payload boundary/fuzz tests and audit redaction.
- Architecture: no Budgeting domain/repository/collection coupling; contract-only boundary; no direct service port;
  no MOD-0138 production types in EnterpriseStrategyService by proximity.
- PPM no-copy source/schema test: no assumption/calculation/ranking/output payload, selected boolean or local engine.
- Frontend/browser: Compact DataTable verifier, permission actions, immutable edit denial, stale-selection 409 flow,
  pinned comparator versions, seven-language/RTL/accessibility, no Approve/Reject, no browser mock/localStorage truth.
- Prototype quarantine/migration: inventory and negative import/collection/route tests; deterministic idempotent dry-run,
  unknown-tenant quarantine, explicit mapping/provenance, rollback proof and untouched source.
- Pack validation: exact MOD-ID preflight, Markdown links, `git diff --check`, staging empty, and no runtime diff.

## 18. Ready-for-dev Checklist

- [ ] Business owner approves Scenario lifecycle, assumption schema and referenceability policy.
- [ ] Selection scope/uniqueness and historical-vs-current validation semantics are exact.
- [ ] Comparator count, unit/currency/period rules, algorithm catalog/retention and sync/async model are approved.
- [ ] Service topology, collection ownership, entity base, port and future routes are approved explicitly.
- [ ] Same-service architecture isolation tests pass, or separate service fallback is selected.
- [ ] MOD-0136 shared-contract boundary and MOD-0117 consumer amendment are approved.
- [ ] Exact contract/security/S2S identity and audit/idempotency persistence design are approved.
- [ ] Prototype quarantine/migration/cutover/rollback plan is separately approved.
- [ ] Compact field inventory is recounted; Golden Reference and explicit tenant layout are confirmed.
- [ ] Backend/frontend/Gateway file contracts and seven-language keys are enumerated.
- [ ] Acceptance, test and compatibility evidence is executable; explicit production authority is recorded.
- [ ] Status is promoted by user from `draft` to `approved` or `ready-for-dev`.

## 19. Implementation Notes

Topology comparison:

| Option | Assessment |
|---|---|
| A. Isolated `ScenarioPlanning` module in future `Diten.FpaService` with MOD-0136 | **Recommended planning target.** FP&A/EPM cohesion, but no shared domain types/repositories/collections; typed/versioned contracts only. |
| B. `Diten.ScenarioPlanningService` | Mandatory fallback if module isolation cannot be mechanically proven; also suitable for independent scaling/release/compute blast radius, with greater S2S/operations cost. |
| C. `Diten.EnterpriseStrategyService` | Not baseline. Existing proximity is prototype/migration evidence, not Scenario Planning SoR authority. Reconsider only in a later approved architecture/migration slice. |

Existing `ScenarioType` and Enterprise Strategy scenario fields are quarantined evidence. A later migration slice
must inventory/classify records, reject default-tenant assignment, prohibit dual-read/dual-write and automatic
promotion, prove deterministic dry-run and mapping, verify collections/cutover, and prove rollback before authority.

## 20. Follow-up Items

- Decide exact selection scope (tenant/global vs opaque portfolio/InvestmentCase context) without owning foreign objects.
- Decide whether historical selection revisions remain valid references while current-use validation marks staleness.
- Approve Scenario lifecycle/archive referenceability, version scalar and assumptions copy-on-new-version semantics.
- Approve comparator min/max, cross-Scenario comparisons, algorithm catalog owner/retention, normalization,
  conversion policy, async model, output count/retention and large-result storage.
- Resolve final service/entity base/collections/port/Gateway paths and S2S identity/credential ownership.
- Coordinate MOD-0136 contracts and author the later MOD-0117 Gate I-B consumer amendment.
- Create a separate prototype quarantine/migration/cutover/rollback pack before touching existing ES fields.
- Recount UI fields and enumerate exact routes/L10n keys before status promotion.
