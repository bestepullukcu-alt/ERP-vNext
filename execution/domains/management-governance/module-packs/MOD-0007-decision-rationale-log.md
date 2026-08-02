---
id: MOD-0007
name: Decision & Rationale Log
domain: management-governance
service: Diten.ManagementGovernanceService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
owner: Enterprise Governance Process Owner
branch: feature/mg/mod-0007-decision-rationale-log
started: 2026-08-02
target: 2026-08-02
form_field_count: 0
production_authority: none
implementation_authority: none
---

# MOD-0007 — Decision & Rationale Log

> **DRAFT / NON-EXECUTABLE PARENT PACK:** This document defines ownership and proposed contracts only. It
> authorizes no service scaffold, runtime code, persistence, migration, frontend, Gateway route, port, seed,
> permission registration or production integration. Physical topology is governed below, but implementation
> requires owner approval, successful pre-scaffold isolation evidence, a matching executable pack revision and explicit
> production authority.

## 1. Module Summary

MOD-0007 is the Blueprint-canonical authoritative registry for generic enterprise decisions and their
rationale. It gives each decision a stable identity, records its semantic history as immutable revisions,
links typed evidence without owning evidence payloads, and validates opaque typed references for consumers.
Its Blueprint route is `Enterprise Control Points → Enterprise Governance Registry Layer → Authoritative
Registries`; the institutional owner is the **Enterprise Governance Process Owner**.

The authoritative record answers what was decided, what result was selected, why, from which provenance and
evidence, and which later decision superseded or withdrew it. A Decision is not an approval result. When an
authoritative policy requires approval, MOD-0023 separately owns and produces an immutable
`ApprovalOutcome`; MOD-0007 neither creates approval tasks nor exposes approve/reject behavior.

## 2. Ownership and Boundaries

### Owned by MOD-0007

- Generic authoritative `DecisionRecord` identity and current publication pointer.
- Stable canonical `DecisionId` and immutable `DecisionRevisionId` / revision number.
- Decision text, selected result, rationale and provenance.
- Generic five-field immutable revision references and authoritative subject-bound referenceability validation.
- Typed metadata-only evidence references.
- Append-only supersession and withdrawal history.
- Authoritative, tenant-aware and visibility-aware `DecisionRevisionReferenceV1` validation.

### Explicitly not owned

- Approval task, approval eligibility, approval workflow, SLA, escalation, delegation or
  `ApprovalOutcome`; MOD-0023 owns these.
- Portfolio, InvestmentCase or BenefitCommitment; MOD-0117 owns these.
- BudgetVersion (MOD-0136), Scenario/Comparator (MOD-0138), or actual outcome/realization (MOD-0072).
- TaskAggregate (MOD-0024), WorkCenter, workflow runs or operational task lifecycle.
- Document binary, evidence payload or generic document metadata.
- Foreign consumer lifecycle transitions, including PPM `UnderAnalysis → Closed`.
- PPM's stored Gate I-A relationship wrapper, `Governing|Supporting` relationship role, `0..1`/`0..n`
  cardinality, duplicate prevention, closure prerequisite and InvestmentCase transition; MOD-0117 owns them.

### Decision versus Approval invariant

- A `DecisionRecord` is not an `ApprovalOutcome`, and publication of a Decision does not imply approval.
- If policy requires approval, MOD-0023 produces a distinct typed/versioned authoritative reference. A
  Decision may cite that opaque reference only after the separate contract is approved; it never copies the
  approval task, state, actor eligibility or outcome payload.
- MOD-0007 exposes no `Approve`, `Reject`, `Delegate`, `RequestInfo`, `Escalate` or approval-task command,
  endpoint, permission, UI action, collection or state.
- Client-supplied `ApprovedBy`, `DecisionActorId`, `ActorId` or `TenantId` is non-authoritative and rejected.
- `RecordedBy` and `PublishedBy` identify technical authenticated/delegated repository actors. They do not
  identify the accountable person, role or governance body that made the business Decision, and neither a
  technical actor nor a MOD-0023 approver is a substitute for Decision authority.

## 3. Owned Objects

| Object | Ownership purpose | Mutation model |
|---|---|---|
| `DecisionRecord` | Stable aggregate identity, tenant boundary, lifecycle and current published revision pointer | Created once; lifecycle changes only through named commands |
| `DecisionDraft` | Explicit mutable pre-publication working state | Optimistic-concurrency updates; it is not a DecisionRevision and may be soft-deleted only under the approved unpublished-draft policy |
| `DecisionRevision` | Immutable published semantic snapshot of decision text/result/rationale/provenance and authority | Created/finalized atomically at publication with all publication fields populated; never updated or deleted through CRUD |
| `DecisionEvidenceReference` | Deferred future typed/versioned evidence metadata reference | V1 core requires an empty evidence set and exposes no attach field/API/UI; activation requires an executable MOD-0031 amendment |
| `DecisionAuthorityReferenceV1` | Exact typed/versioned semantic identity of the accountable Person or Position | Validated authoritatively by MOD-0288 and captured in the immutable revision; never inferred from recorder, publisher, RBAC Role or approver |
| `DecisionPublicationHistory` | Append-only publication and disposition provenance | `Published`, `Withdrawn` and `Superseded` events append; published revision bytes never change |
| `DecisionRevisionReplacementProvenance` | Same-Decision append-only link from prior current revision to the next monotonic revision | Advances current pointer while record remains `Published`; prior revision remains historical |
| `DecisionSupersessionLink` | Different-Decision append-only edge from old published Decision/revision to replacement Decision/revision | Old record becomes terminal `Superseded`; same-tenant, cycle-safe and concurrency-safe |
| `DecisionRevisionReferenceV1` | Generic opaque five-field consumer value for an exact immutable revision | Validated authoritatively by MOD-0007; contains no consumer relationship role |
| Gate I-A PPM wrappers | Two PPM-owned relationship wrappers nesting `DecisionRevisionReferenceV1` unchanged | Exact identities/version are closed by MOD-0117; storage, role, cardinality and lifecycle remain PPM-owned |

Planned repository, command, query, DTO, endpoint and transport artifacts remain design directions, not
authorized files. Any executable revision must preserve separate aggregate/repository/collection types from
MOD-0354 DWS and MOD-0355 BPM and must not share their domain models.

### Generic DecisionRevisionReferenceV1 contract proposal

Exact contract name: **`management-governance.decision-reference`**. Exact initial version: **`1.0`**.

| Field | Type | v1 rule |
|---|---|---|
| `ContractName` | string | Exact `management-governance.decision-reference` |
| `ContractVersion` | string | Exact `1.0`; unknown versions fail closed |
| `DecisionId` | Guid | Canonical, non-empty stable Decision identity |
| `DecisionRevisionId` | Guid | Canonical, non-empty immutable published revision identity |
| `DecisionRevisionNumber` | positive integer | Immutable semantic revision; not technical concurrency `Version` |

The generic persistable `DecisionRevisionReferenceV1` contains exactly those five fields. It contains no
`DecisionRole`, subject, tenant, actor, `CorrelationId`, `IdempotencyKey`, S2S identity, audience or scope.
`Governing`/`Supporting` is not an intrinsic generic Decision or revision property. A later producer policy
could establish generic role eligibility only through separate explicit owner approval; none exists here.

### Authoritative validation request and trusted context

The Gate I-A read-only validation request contains the five-field `DecisionRevisionReferenceV1`, exact
`InvestmentCaseContextV1`, exact PPM wrapper name/version and validation mode. The request carries no tenant
or actor. Trusted request/transport context independently supplies server-derived
`TenantId`, effective actor and separately validated delegated actor, `CorrelationId`, and authenticated S2S
identity/audience/scope. A read-only validation query requires no mutation `IdempotencyKey` and persists no
receipt, audit intent, cache or business mutation.

Successful validation returns the exact five-field generic reference plus mode-aware resolution/eligibility
and disposition facts; `IsReferenceable` alone is insufficient. It returns no decision text, rationale,
provenance, evidence payload or trusted-context echo. MOD-0007 validates the exact
published revision, publication/referenceability, Decision subject binding to the exact PPM InvestmentCase
context, supported profile/mode, tenant and visibility without revealing which 404 predicate failed. Any
future producer-side relationship-role eligibility policy remains separately approval-blocked.

The core `management-governance.decision-reference` contract remains generic. MOD-0117 owns these exact,
case-sensitive Gate I-A wrappers with `ContractVersion = 1.0`:

| PPM-owned wrapper | Exact nested shape | Relationship semantics |
|---|---|---|
| `ppm.investment-case-governing-decision-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `DecisionRevisionReferenceV1` | Governing relationship; PPM cardinality `0..1` and closure prerequisite |
| `ppm.investment-case-supporting-decision-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `DecisionRevisionReferenceV1` | Supporting relationship; PPM cardinality `0..n` and duplicate prevention |

Both wrappers nest MOD-0007's exact five-field tuple unchanged; neither adds `DecisionRole` to the generic
DTO. Their role is encoded by distinct PPM wrapper identity. `InvestmentCaseContextV1` is exact
`ContractName = ppm.investment-case-context`, `ContractVersion = 1.0`, `InvestmentCaseId`; free-text kind or
discriminator is forbidden. A new consumer/profile/subject requires separately approved identity/version and
bilateral fixtures and cannot silently widen Gate I-A.

### PPM-owned Gate I-A compatibility requirements

MOD-0117, not MOD-0007, owns and enforces:

- the stored InvestmentCase relationship wrappers and the role encoded by their exact identities;
- `InvestmentCase → GoverningDecisionReference`: `0..1` structurally.
- Before PPM permits `InvestmentCase UnderAnalysis → Closed`: exactly `1` valid governing reference.
- `SupportingDecisionReferences`: `0..n`; duplicates of the same Decision revision and role are rejected.
- PPM stores its MOD-0117-owned relationship wrapper containing the opaque five-field generic reference and
  relationship role. It cannot copy decision text, result, rationale, provenance or evidence payload and
  cannot infer validation from a cache.

MOD-0007 supplies authoritative validation and compatibility fixtures only. It does not own the
InvestmentCase aggregate, stored-reference duplicate/cardinality enforcement, the closure prerequisite or
the PPM lifecycle transition/transaction. It owns Decision identity, immutable revision, Decision subject
binding, generic `DecisionRevisionReferenceV1` validation, and authoritative tenant/visibility/referenceability
validation for a supported consumer profile.

### Gate I-A bilateral validation modes

Gate I-A is a non-selection contract and supports exactly:

| Validation mode | Published active revision | Withdrawn/superseded exact revision | Contract result |
|---|---|---|---|
| `HistoricalResolve` | Resolve when tenant-visible and subject-bound | Resolve historical exact revision and disposition without rewriting history | Success with mode-aware resolution/disposition facts |
| `NewReferenceEligibility` | Eligible only when all current binding rules pass | Ineligible for a new binding | `409` for visible withdrawn/superseded revision |
| `CurrentSelectionEligibility` | Not supported | Not supported | `400` malformed mode for this non-selection contract |

Missing, cross-tenant, invisible or otherwise non-disclosable records are indistinguishable `404`; exact
permission/S2S denial is `403`; unknown/unsupported dependency or contract version, timeout, unavailable
provider or malformed authoritative response is `503`. A bare `IsReferenceable` boolean is never an
authoritative result. Bilateral fixtures must assert the requested mode, exact wrapper/context/reference,
publication/disposition facts and mode-specific outcome category together; they must not reduce historical
resolution and new-binding eligibility to one boolean.

## 4. Entity Fields

The following is the proposed semantic schema for a future tenant-owned implementation. Frontmatter
`entity_base: EntityBase` remains the module-pack logical marker. The concrete repo-standard decision is direct
inheritance from `Diten.Platform.Common.Persistence.TenantScopedEntity` (which extends `BaseEntity`), not a new
service-local generic base. Its inherited fields are `Id`, `TenantId`, `CreatedAt`, `CreatedBy`, `UpdatedAt`,
`UpdatedBy`, `IsDeleted` and technical `Version`; it does not supply `DeletedAt`. A module-owned `DeletedAt`
is therefore explicit only on the deletable draft/record persistence shape. Published revision, history and
graph types never use shared mutable update/delete behavior. Semantic Guid actor fields remain separate from
the shared string audit fields. `TenantId` is always server-derived.

### DecisionRecord

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `DecisionId` | Guid | Yes | Stable canonical identity; may equal aggregate `Id`; immutable |
| `SubjectKind` | closed string enum | Yes | Registered typed subject kind; arbitrary free text forbidden |
| `SubjectId` | Guid | Yes | Canonical non-empty opaque identity |
| `LifecycleState` | closed enum | Yes | `Unpublished`, `Published`, `Withdrawn`, `Superseded`; record disposition only, never revision mutability or approval |
| `CurrentRevisionId` | Guid? | No | Set only to a successfully published immutable revision |
| `CurrentRevisionNumber` | positive integer? | No | Monotonic semantic revision; never named `Version` |
| `CreatedBy` | Guid | Yes | Authenticated/delegated actor from server context |
| `UpdatedBy` | Guid? | No | Server context only |

Indexes: tenant-first unique active `(TenantId, DecisionId)` and lookup
`(TenantId, SubjectKind, SubjectId, IsDeleted, LifecycleState)`. Soft-deleted, invisible and cross-tenant
records are never referenceable.

The lifecycle is closed. Publishing the first draft moves `Unpublished → Published`. A correction to the
same Decision creates a new draft and next monotonic revision; atomic publication keeps the DecisionRecord
`Published`, advances `CurrentRevisionId/CurrentRevisionNumber`, and appends replacement provenance to the
prior historical revision. Only replacement by a different Decision identity moves the old record to
`Superseded`. `Withdrawn` is terminal without replacement. `Withdrawn` and `Superseded` records cannot reopen;
a later business decision requires a new Decision identity.

### DecisionDraft

`DecisionDraft` carries the pre-publication semantic fields, exact `DecisionAuthorityReferenceV1`,
provenance and an evidence list that must remain empty in v1 core. It has `DraftId`, technical `Version`, server-derived
creator/updater and timestamps, but no `DecisionRevisionId`, revision number or publication timestamp. It may
be edited with optimistic concurrency. An unpublished draft with no immutable publication/history may be
soft-deleted only under an approved draft policy; published records and revisions have no CRUD delete path.
The draft creator or latest updater cannot publish that draft's revision.

### DecisionRevision

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `DecisionRevisionId` | Guid | Yes | Immutable canonical revision identity |
| `DecisionId` | Guid | Yes | Same-tenant parent DecisionRecord |
| `DecisionRevisionNumber` | positive integer | Yes | Monotonic per Decision; unique and immutable |
| `DecisionText` | string | Yes | Trim + NFC; non-empty; max 8000 |
| `DecisionResult` | string | Yes | Trim + NFC; non-empty; max 4000 |
| `Rationale` | string | Yes | Trim + NFC; non-empty; max 12000 |
| `DecisionAuthorityReferenceV1` | typed/versioned value | Yes | Exact MOD-0288-backed Person/Position authority contract below |
| `ProvenanceKind` | closed enum | Yes | Owner-approved source category |
| `ProvenanceReference` | typed/versioned value | Yes | Opaque source reference; no foreign payload |
| `EvidenceReferences` | typed/versioned value list | Yes | Must be empty in v1 core; no provider call or local/mock substitute |
| `RecordedBy` | Guid | Yes | Authenticated/delegated server actor |
| `RecordedAtUtc` | UTC instant | Yes | Server generated |
| `PublishedBy` | Guid | Yes | Technical authenticated/delegated actor executing publication; not business Decision authority |
| `PublishedAtUtc` | UTC instant | Yes | Server generated before insert; immutable revision is born published |

Indexes: unique `(TenantId, DecisionId, DecisionRevisionNumber)`, unique `(TenantId,
DecisionRevisionId)`. Mutation idempotency receipt/index is command infrastructure, not a semantic revision
field. A published revision has no update path; correction creates a new draft and atomically publishes a
new revision. Trusted `CorrelationId` may be recorded in bounded publication/audit provenance, but is not a
semantic DecisionRevision field or consumer `DecisionRevisionReferenceV1` field.

### DecisionAuthorityReferenceV1

| Field | Type | Required | Rule |
|---|---|---:|---|
| `ContractName` | string | Yes | Exact `management-governance.decision-authority-reference` |
| `ContractVersion` | string | Yes | Exact `1.0` |
| `AuthorityKind` | closed enum | Yes | `Person` or `Position` only |
| `AuthorityId` | opaque canonical identity | Yes | Non-empty MOD-0288 identity; server-authoritatively validated |

MOD-0288 — Organization, Person & Position Directory is the authoritative provider. `Person` resolves an
active tenant-visible PersonReference; `Position` resolves an active tenant-visible Position. Missing,
cross-tenant, invisible or inactive authority is indistinguishable `404`; unavailable, malformed,
indeterminate or unsupported provider/version response is `503`. `Role` and `GovernanceBody` are deferred
beyond v1. AuthService RBAC Role cannot be business DecisionAuthority. This semantic reference is distinct
from draft author, `RecordedBy`, `PublishedBy`, and MOD-0023 approver/`ApprovalOutcome`.

### Deferred DecisionEvidenceReference, disposition and supersession

| Field | Type | Required | Rule |
|---|---|---:|---|
| `EvidenceReferences` | empty list | V1 core | Any non-empty value is `400`; evidence attachment is formally deferred |
| `SourceDecisionId` / `SourceDecisionRevisionId` | Guid pair | For supersession | Exact old immutable published revision; source Decision differs from target Decision |
| `TargetDecisionId` / `TargetDecisionRevisionId` | Guid pair | For supersession | Exact replacement immutable published revision under a different Decision identity |
| `SupersessionKind` | closed enum | For supersession | Owner-approved replacement kind; withdrawal is not a fake edge |
| `Reason/ProvenanceReference` | typed/versioned value | Yes | Required disposition reason and authoritative provenance |
| `EffectiveAtUtc` | UTC instant | For supersession | Server normalized; append-only history |

Different-Decision supersession endpoints are same-tenant published revisions. Self-links, same-Decision
edges, duplicate edges and directed cycles are
forbidden. Edge creation is concurrency-safe and atomic with disposition/publication effects and audit
intent. Withdrawal without a replacement appends a `DecisionPublicationHistory(Withdrawn)` event referencing
the affected exact revision; it never fabricates a source/target link. Same-Decision correction uses
`DecisionRevisionReplacementProvenance`, not `DecisionSupersessionLink`.

## 5. Repo Scope

This draft authoring milestone changes only:

- `execution/domains/management-governance/module-packs/MOD-0007-decision-rationale-log.md`

A future executable amendment may authorize exactly one of these mutually exclusive scopes; this draft grants
no write authority to either.

Primary scope:

- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Api/DecisionRegistry/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/DecisionRegistry/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Domain/DecisionRegistry/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Persistence/DecisionRegistry/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Infrastructure/DecisionRegistry/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.Tests/DecisionRegistry/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.IntegrationTests/DecisionRegistry/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.ArchitectureTests/DecisionRegistry/**`

Only an executable amendment may enumerate and add the five project files, API `Program.cs`, layer-local
composition files and test project files needed to compose those paths; unrelated module composition is out
of scope. If the mandatory isolation suite fails, the entire same five-layer/test shape moves instead under
`services/Diten.EnterpriseGovernanceService/`, with `Diten.EnterpriseGovernanceService` substituted in every
path above. Primary and fallback implementations cannot coexist. No frontend, Gateway, WorkCenter or PPM
consumer path is included.

## 6. Protected Paths

- `.antigravity/**`
- `services/**`, including both candidate service roots, until an executable amendment grants one exact scope
- `services/Diten.EnterpriseStrategyService/**`, `services/Diten.PpmService/**`, `services/Diten.Platform/**`,
  `services/Diten.Platform.Common/**`, `services/Diten.AuthService/**` and `services/Diten.Building.Blocks/**`
- `frontend/Diten.Web/**`, including every WorkCenter artifact
- `gateway/Diten.ApiGateway/**`, including every `ocelot.json`
- `execution/domains/**` outside this exact pack file
- `execution/portfolio/**`, `execution/registries/**` and Blueprint workbooks
- archive/frozen paths
- DCP-006, MOD-0023, MOD-0117, MOD-0136, MOD-0138, MOD-0072, MOD-0024, MOD-0354, MOD-0355 and WorkCenter runtime/governance
- existing Enterprise Strategy prototype fields, mocks, pages and data

## 7. Dependencies

| Dependency | Purpose | Gate |
|---|---|---|
| DCP-006 | Gate I-A orchestration and ownership boundaries | Approved orchestration; does not authorize MOD-0007 runtime |
| MOD-0018 | Exact permission and tenant/actor authorization evaluation | Runtime contract/evidence required before implementation |
| MOD-0021 / MOD-0035 | Immutable audit intent delivery over approved event/outbox mechanics | Exact producer contract and credentials required |
| MOD-0031 | Future typed evidence metadata integration only | Formally deferred from v1 core; requires a separate executable amendment |
| MOD-0288 | Authoritative Person/Position resolution for `DecisionAuthorityReferenceV1` | Exact v1 provider; runtime client/transport evidence remains a B-class implementation blocker |
| MOD-0023 | Optional distinct ApprovalOutcome only when policy requires approval | Separate PSS amendment and contract approval required |
| MOD-0117 | Consumer of `management-governance.decision-reference` for Gate I-A | Matching consumer amendment and explicit runtime authority required |

Prototype Enterprise Strategy `DecisionReference`, `DecisionReferencesJson`, `ApprovedBy` and rationale fields
are non-authoritative evidence only. They are not dependencies, schemas, migration truth or runtime baselines.

## 8. Runtime Constraints

- This draft assigns no port, route, database, deployment or production authority. It closes only the
  mutually exclusive topology and future module-local ownership boundaries below.
- Future persistence is MongoDB, single database and tenant-owned; every read/write filters server-derived
  `TenantId` plus `IsDeleted=false`.
- Tenant, effective/delegated actor, trusted `CorrelationId`, S2S client identity, audience and scope come
  only from authenticated middleware/transport context. Forbidden client fields are rejected rather than
  ignored; they never override trusted context. A delegated actor requires separately authenticated,
  tenant/operation-bound and anti-replay delegation evidence.
- Missing, soft-deleted, cross-tenant, invisible and non-referenceable records are indistinguishable `404`.
- Syntactically invalid input, malformed version syntax or forbidden context field is `400`; service
  authentication/audience/issuer or required authenticated context failure is `401`; exact service scope,
  actor permission or delegation failure is `403`; immutable publication, idempotency, graph or concurrency
  conflict is `409`; a well-formed unsupported contract/profile version, required dependency timeout,
  malformed authoritative response or unavailable required validator is `503`.
- No default tenant, fail-open, stale-cache ownership inference or local consumer validation substitute.
- Publishing atomically validates the draft, MOD-0288 authority and provenance, and confirms evidence is empty;
  allocates the next revision identity/number; inserts the already-final immutable `DecisionRevision` with
  non-null publication provenance; appends publication/disposition history; compare-and-swaps the
  `DecisionRecord` pointer/state; optionally inserts a valid supersession edge; persists the idempotency
  result; and writes the producer-local audit intent in one Mongo replica-set transaction. Failure at any
  step rolls back all state.
- An approved post-commit worker transforms/delivers the durable local audit intent through an approved
  outbox/eventing adapter. Dispatch failure leaves the intent retryable, cannot roll back or mutate committed
  Decision state, and cannot create a duplicate semantic mutation. No broker call occurs inside the business
  transaction; any local transport-outbox collection requires its own approved contract and evidence.
- Correlation and operation-scoped idempotency are mandatory. A replay with the same canonical request
  returns the same result; the same key with different payload returns `409`.
- Prepare/publish separation of duties is mandatory: the authenticated publisher must differ from both the
  draft creator and latest draft updater and must hold exact `.publish` permission. Publisher need not equal
  DecisionAuthority; even when the same Person is semantically accountable, publication remains a distinct
  technical act. V1 has no local/admin/emergency self-publish bypass. Import/migration requires a future
  separately approved process and cannot reuse ordinary publish permission to bypass SOD.
- PPM-facing validation uses a dedicated authenticated S2S identity with exact issuer/audience/scope and an
  independently validated delegated actor. End-user JWT forwarding alone is insufficient; the service and
  effective-actor authorization gates are independent.
- Read-only validation requires no idempotency key and writes no audit intent, receipt, cache or mutation.
- V1 evidence integration is disabled: `EvidenceReferences` must be empty, no evidence field/attach API/UI is
  exposed, and no provider call occurs. Non-empty evidence is `400`. If tenant policy requires evidence,
  publication returns `409`; local/mock evidence cannot satisfy it. Evidence-enabled publication requires a
  later executable MOD-0031 exact typed/versioned amendment.
- Only a policy-eligible unpublished draft with no immutable publication/history may use soft delete, and no
  automatic hard purge follows. Legal hold blocks deletion of every Decision, draft and history record.
  Published revisions, publication/disposition history and supersession graph have no automatic TTL and no
  ordinary update/delete/bulk-delete. V1 supports no exceptional purge. Withdrawal/supersession are append-only
  dispositions, never purge. Any future purge requires MOD-0030 retention/legal-hold authority plus a separate
  executable amendment.
- Exact withdrawn/superseded references remain immutable historical facts and resolve under
  `HistoricalResolve`; `NewReferenceEligibility` rejects their new binding with `409`. Disposition never
  physically rewrites consumer history.
- Published revisions, disposition events and supersession edges are immutable. Technical optimistic
  concurrency `Version` is distinct from `DecisionRevisionNumber`.

### Physical service topology decision direction

| Option | Assessment |
|---|---|
| A. Isolated `DecisionRegistry` internal module in a future `Diten.ManagementGovernanceService` | **Selected primary topology**, conditional on pre-scaffold architecture tests proving separate namespaces, types, repositories, collections, permissions, index initialization and dependency direction |
| B. Separate `Diten.EnterpriseGovernanceService` | **Fail-closed fallback** if any mandatory Option A isolation test cannot prove separation; it replaces Option A and cannot coexist with it |
| C. Placement in an existing unrelated service | Rejected; proximity, reuse or current prototype location is not ownership evidence |

MOD-0007 cannot be placed in `Diten.EnterpriseStrategyService`. `DecisionRegistry` shares no domain type,
repository interface/implementation, generic repository, Mongo collection, index initializer, transaction
unit, permission constant or migration with MOD-0354 DWS or MOD-0355 BPM. Its service-local owned collection
set is exactly `decision_registry_records`, `decision_registry_drafts`, `decision_registry_revisions`,
`decision_registry_publication_history`, `decision_registry_revision_replacements`,
`decision_registry_supersession_links`, `decision_registry_idempotency`,
and `decision_registry_audit_intents`. A later approved transport contract may add only a DecisionRegistry-local
outbox collection through an executable amendment. No other module may read/write these
collections directly.

One Mongo replica-set transaction owns revision, history, pointer, link, idempotency and audit-intent writes.
The outbox worker publishes only after commit through an approved `Diten.BuildingBlocks.Eventing`
adapter; MOD-0007 owns the logical audit intent and never writes Platform audit/outbox collections directly.

The authoritative DecisionReference validation provider is logically owned by the `DecisionRegistry`
Application layer and may later be exposed only through a service-local internal adapter. The MOD-0117
consumer calls it directly S2S; Browser, frontend, Gateway and WorkCenter are excluded, and PPM may not use a
cache or copied Decision state as authority. No provider endpoint is executable until dedicated identity,
delegation, security and bilateral compatibility evidence exists.

No authoritative, collision-free Management Governance port allocation exists: the documented table stops
at existing allocations while runtime services already occupy later values. Port remains `TBD` and is an
explicit blocker. Internal route, service discovery, DNS/TLS/network policy, credential mechanism,
issuer/audience/scope, client registration/rotation/revocation, timeout/retry/circuit budget, deployment,
Mongo provisioning, audit/event publisher credentials and MOD-0288 S2S evidence also remain open. This pack
does not add a Gateway route or authorize either service scaffold.

## 9. Layout & Shell Contract

Current shell is `none`; this draft authorizes no UI and no Razor layout. A future Decision Register/Details
surface must be separately approved as tenant-facing or platform-admin and must then use the corresponding
`_LayoutTenantShell` or `_LayoutPlatformAdmin` explicitly.

Provisional future editor count is **9 user-entered controls**: subject kind, subject identity, decision text,
decision result, rationale, provenance kind, provenance reference, evidence references and effective date.
Therefore a DataTable-based future UI would select Golden Compact (`>8`) with separate Create/Edit/Details
pages. This is planning guidance only; immutable publication means “Edit” may edit only an unrecorded draft
and must become “Create revision” after recording. No approval button is permitted.

## 10. Backend File Convention

No backend files are authorized. The future exact five-layer and test roots are fixed in Section 5; an
executable amendment must enumerate composition files and CQRS artifacts while preserving distinct commands such as `CreateDecisionDraft`,
`UpdateDecisionDraft`, `SoftDeleteDecisionDraft`, `PublishDecisionDraft`, `SupersedeDecision` and
`WithdrawDecision`. `PublishDecisionDraft` creates/finalizes the immutable revision atomically; there is no
pre-publication revision-recording operation and no later publication-field setter.
It must not generate generic update/delete/bulk-delete handlers for published records or revisions and must
not expose approval commands.

If a Golden Compact UI is later authorized, naming/folder conventions must follow the live Compact reference,
adapted for append-only revision semantics rather than pretending an immutable revision is CRUD-editable.

## 11. Frontend File Contract

No frontend route, view, JavaScript, resource or proxy is authorized. A future approved Compact surface must
declare its area, explicit layout, seven-language localization, DataTable v2 contract and exact field count in
an executable pack revision. Decision Details may display typed evidence links but never embed foreign
document binaries or approval actions.

## 12. Validation Rules

| Field / context | Required | Rule | Pre-check |
|---|---:|---|---|
| `DecisionId` / `DecisionRevisionId` | Yes | Canonical non-empty Guid; client cannot select server-generated identities on create | Same-tenant active lookup |
| `InvestmentCaseContextV1` | Gate I-A validation | Exact `ppm.investment-case-context` / `1.0` plus canonical non-empty `InvestmentCaseId` | Decision subject-binding validation; no free-text discriminator |
| `DecisionText` | Yes | Trim + NFC; 1..8000 | — |
| `DecisionResult` | Yes | Trim + NFC; 1..4000 | — |
| `Rationale` | Yes | Trim + NFC; 1..12000 | — |
| `ProvenanceKind/Reference` | Yes | Typed, versioned, supported and referenceable | Authoritative validator |
| `EvidenceReferences` | V1 empty-only | Non-empty list is `400`; no attach UI/API/provider call | Tenant evidence-required policy blocks publication with `409` |
| PPM wrapper profile | Gate I-A validation | Exact governing or supporting wrapper name above, version `1.0`; role encoded by identity and absent from generic reference | Supported-profile validation; producer role-eligibility is formally excluded/deferred from v1 |
| Validation mode | Gate I-A validation | `HistoricalResolve` or `NewReferenceEligibility`; `CurrentSelectionEligibility` forbidden | Unsupported non-selection mode is `400` |
| `DecisionRevisionNumber` | Yes | Positive, next monotonic number; client cannot overwrite current revision | Unique index + transaction |
| `DecisionAuthorityReferenceV1` | Yes on publication | Exact four fields; contract `management-governance.decision-authority-reference/1.0`; kind `Person|Position` only | Active tenant-visible MOD-0288 validation |
| `ConsumerProfile` | Yes on reference validation | Exact approved name/version; Gate I-A allowlist cannot be widened | Authenticated S2S registration must match |
| `ActorId`, `DecisionActorId`, `RecordedBy`, `PublishedBy`, `ApprovedBy`, `TenantId` in payload | Forbidden | Reject request; never trust, merge or normalize into server context | Authenticated/delegated server context |
| Trusted `CorrelationId` | Mutations and validation | Bounded authenticated trace context; absent from persisted `DecisionRevisionReferenceV1` | Server transport context |
| `IdempotencyKey` | Mutation commands only | Required, scoped to tenant + operation + effective actor + S2S client and payload-bound | Idempotency store/index; forbidden requirement on read validation |

V1 never contacts an evidence provider. Empty evidence proceeds only under evidence-disabled tenant policy;
evidence-required policy returns `409`, and non-empty/local/mock evidence returns `400`.

## 13. Failure Path to Verify

- **Published revision update/delete attempt:** `409` or absent route; original bytes, history and pointer remain unchanged.
- **Draft publication fault:** failure after any revision/history/pointer/edge/idempotency/audit write rolls
  back all writes; no premature revision or half-published state exists.
- **Draft soft-delete race:** only an unpublished draft with no immutable history may be soft-deleted under
  policy; concurrent publication versus deletion permits at most one successful transaction.
- **Prepare/publish SOD violation:** publisher equals draft creator or latest updater: `403`; no publication,
  revision, pointer, idempotency success or audit intent persists. Admin/emergency/import flags do not bypass.
- **Syntactically invalid identifier, required value or closed enum:** `400`; no authoritative lookup or mutation.
- **Cross-tenant, missing, soft-deleted or invisible Decision:** indistinguishable `404`; no existence leak.
- **Client tenant/actor/recorder/publisher/ApprovedBy/delegation/context override:** `400`; no lookup,
  mutation or audit intent persists.
- **Permission denied:** `403`; authentication/context failure remains `401`.
- **Duplicate idempotency replay:** identical payload returns the original outcome; altered payload returns
  `409`; no second revision or audit intent.
- **Unknown contract version, malformed response, timeout or validator unavailable:** `503`; no local-cache
  inference and no consumer transition.
- **Concurrent publication/revision number conflict:** one succeeds; loser receives `409`; no silent overwrite.
- **Audit intent/outbox persistence failure:** aggregate, revision, pointer and link all roll back.
- **Supersession graph violation:** cross-tenant/invisible endpoint is `404`; self-link, duplicate edge,
  cycle, incompatible visible disposition or concurrency race is `409`; no partial edge/history survives.
- **Withdrawal without replacement:** appends one withdrawal disposition and no supersession edge or fake
  target; history remains readable and immutable.
- **Authority resolution:** invalid kind/shape is `400`; missing/cross-tenant/invisible/inactive MOD-0288
  Person/Position is `404`; unavailable/malformed/indeterminate/unsupported provider response is `503`.
- **Evidence-disabled v1:** empty evidence makes no provider call; non-empty/local/mock evidence is `400`;
  evidence-required tenant policy is `409`; publication does not commit on either failure.
- **Retention/legal hold:** legal-held draft cannot soft-delete; published revision/history/graph has no
  TTL/delete/bulk-delete/purge path; withdrawal or supersession preserves all historical bytes.
- **Read-only reference validation:** succeeds without `IdempotencyKey` and writes no receipt, audit, cache or
  business mutation.
- **Approval substitution attempt:** any approve/reject command, approval field or approval-state persistence is
  rejected by architecture/contract tests.
- **Foreign payload copy attempt:** architecture test fails if PPM or MOD-0007 stores foreign decision,
  approval, evidence binary, budget, scenario or outcome payload outside the owning SoR.

## 14. Authorization Convention

Future exact permission keys are reserved by this pack but not registered or seeded:

- `management-governance.decisions.read`
- `management-governance.decisions.create`
- `management-governance.decisions.revise`
- `management-governance.decisions.publish`
- `management-governance.decisions.supersede`
- `management-governance.decisions.withdraw`
- `management-governance.decision-references.validate` — dedicated S2S validation identity

`.create` creates a mutable draft; `.revise` updates an unpublished draft or starts a new draft from a
published revision; `.publish` performs atomic immutable publication; `.supersede` creates the authorized
replacement/disposition graph operation; `.withdraw` appends withdrawal disposition only. None permits
published-byte mutation or ordinary deletion.

All commands/queries require authenticated tenant context, exact MOD-0018-backed permission evaluation and
visibility scope. V1 has no local, platform-admin or emergency self-publish bypass.
No permission containing `approve`, `reject`, `approval`, `delegate`, `sla` or `escalate` belongs to MOD-0007.
The validation permission grants no Decision-details read. V1 exposes no exceptional purge permission or
process; a future MOD-0030-backed amendment cannot reuse `.delete`, `.withdraw`, platform-admin bypass or
ordinary module permissions.

## 15. Gateway / API Routing Decision

No Gateway/API transport or route is authorized. Exact transport remains open. A future executable revision
must name the internal validation transport, route, service identity, timeout/retry budget and compatibility
policy. Frontend, if later authorized, must call Gateway through the approved shell profile and never a
service port directly. Only `integration-agent` may modify `ocelot.json` after explicit route authority.

## 16. Acceptance Criteria

1. MOD-0007 is the sole generic Decision/rationale SoR; no owned object or API conflates Decision with
   MOD-0023 `ApprovalOutcome`.
2. Mutable pre-publication state is an explicit `DecisionDraft`, not a DecisionRevision. Publication atomically
   creates/finalizes an already-published immutable uniquely numbered revision, history, pointer, idempotency
   result and audit intent; no later publication-field mutation exists.
3. Persisted `DecisionRevisionReferenceV1` under `management-governance.decision-reference` version `1.0`
   has exactly the five-field key set `ContractName`, `ContractVersion`, `DecisionId`, `DecisionRevisionId`,
   `DecisionRevisionNumber`; `DecisionRole` and all subject/context fields are absent.
4. The exact PPM/InvestmentCase Gate I-A wrapper identities and `ppm.investment-case-context` are accepted at
   version `1.0`. The validation request carries the five-field reference, exact typed context, exact wrapper
   identity and supported mode.
   MOD-0117-owned compatibility fixtures prove governing `0..1`, exactly one before
   `UnderAnalysis → Closed`, and supporting `0..n`; MOD-0007 does not enforce that transition/cardinality.
5. PPM stores only its relationship wrapper containing the opaque five-field reference and MOD-0117-owned
   role; architecture tests ban decision text, result, rationale, provenance and evidence payload fields.
6. Approval-required scenarios use a separate MOD-0023 `ApprovalOutcome` reference; no MOD-0007 or PPM
   approve/reject command, field, state or permission exists.
7. Cross-tenant, missing, soft-deleted, invisible and non-referenceable lookups return indistinguishable `404`.
8. Tenant and actor are derived from authenticated/delegated server context; client actor, tenant or
   `ApprovedBy` values are rejected and never persisted.
9. Revision publication, publication/disposition history, record pointer, optional supersession edge,
   idempotency result and audit intent/outbox commit atomically; injected failure after every write proves
   full rollback.
10. Supersession uses an exact older-source to replacement-target edge, rejects self/duplicate/cycle and is
    concurrency-safe. Withdrawal without replacement appends disposition only; neither operation rewrites a
    published revision.
11. Idempotent duplicate requests create one semantic mutation and one audit intent; mismatched replay is
    `409`.
12. Unsupported contract version, unavailable dependency, timeout and malformed authoritative response are
    `503`; no fail-open or cache ownership inference occurs.
13. Dedicated PPM-facing S2S identity and exact `management-governance.decision-references.validate` permission are required;
    delegated actor validation is separate.
14. Architecture tests prove DecisionRegistry/DWS/BPM project/namespace, type, repository, collection,
    permission and migration isolation;
    failure forces the separate-service topology fallback before implementation.
15. No runtime, UI, Gateway, migration, seed, port or service-scaffold change is made under this draft.
16. `DecisionAuthorityReferenceV1` has exactly `ContractName`, `ContractVersion`, `AuthorityKind`,
    `AuthorityId`; exact identity is `management-governance.decision-authority-reference/1.0`, kind is only
    `Person|Position`, and MOD-0288 active tenant-visible resolution is authoritative.
17. Draft creator/latest updater cannot publish the same draft. Publisher is a distinct authenticated actor
    with exact publish permission; no local/admin/emergency/import bypass exists.
18. V1 evidence is disabled: evidence list is empty, no attach UI/API/provider call exists, non-empty evidence
    is `400`, and evidence-required tenant policy blocks publication with `409`.
19. New consumers/subject types require an explicitly approved profile/version and fixtures; Gate I-A's exact
    `ppm`/`InvestmentCase` allowlist cannot widen silently.
20. `Governing|Supporting` is absent from generic Decision/DecisionRevision state and the persisted generic
    reference. Any future producer-side role-eligibility policy is a separately approved blocker.
21. `HistoricalResolve` resolves visible subject-bound withdrawn/superseded exact revisions, while
    `NewReferenceEligibility` returns `409` for their new binding; `CurrentSelectionEligibility` returns `400`.
22. Bilateral fixtures prove mode-aware facts and error mapping; no `IsReferenceable` boolean alone can
    authorize historical resolution or a new PPM relationship.
23. Same-Decision correction creates a new monotonic revision, keeps the record `Published`, advances the
    current pointer and appends replacement provenance. Different-Decision replacement supersedes the old
    record; Withdrawn/Superseded records never reopen.
24. Published revision/history/supersession graph has no TTL/update/delete/bulk-delete/purge. Legal hold blocks
    all deletion; eligible unpublished draft soft-delete never triggers automatic hard purge.
25. The selected primary topology is the single future `Diten.ManagementGovernanceService/DecisionRegistry`;
    failure of any mandatory isolation test selects the mutually exclusive `Diten.EnterpriseGovernanceService`
    fallback before scaffold. Both services and EnterpriseStrategy placement are forbidden.
26. Future scope is limited to the exact five-layer and test roots in Section 5. DecisionRegistry owns only
    the exact `decision_registry_*` collection allowlist and shares no type, repository, collection, index,
    transaction, permission or migration/bootstrap component with MOD-0354/MOD-0355.
27. Concrete tenant entities inherit `Diten.Platform.Common.Persistence.TenantScopedEntity`; inherited fields
    are not redeclared and module-local `DeletedAt` exists only where deletion is allowed.
28. The producer-owned reference validator is direct, read-only S2S with no Browser/Gateway/WorkCenter or
    persistence side effect. A MOD-0117 adapter cannot compile, register or execute before producer runtime,
    endpoint/security evidence, bilateral fixtures, consumer amendment and explicit runtime authority pass.

## 17. Test Expectations

| Test layer | Mandatory evidence before runtime promotion |
|---|---|
| Domain unit | Closed lifecycle, same-Decision revision replacement versus different-Decision supersession, terminal withdrawal, immutable birth, monotonic numbering and graph invariants |
| Application unit | Exact four-field MOD-0288 authority, prepare/publish SOD, evidence-disabled policy, exact permission, S2S/delegation, mutation idempotency and Decision/Approval separation |
| Persistence integration | Fault injection after revision/history/pointer/edge/idempotency/audit writes, SOD race, legal-hold draft delete, no-TTL/no-purge architecture and cross-tenant 404 equivalence |
| Contract compatibility | Exact five-field key-set serialization/roundtrip; exact two wrapper names + context at 1.0; HistoricalResolve/NewReferenceEligibility matrices; CurrentSelectionEligibility 400; mode-aware result with no boolean-only authority |
| S2S security | Dedicated identity, delegated actor validation, 401/403/404/409/503 matrix, no tenant/existence disclosure |
| Architecture | Project/namespace/type/repository/collection/permission/migration isolation for DWS/BPM/DecisionRegistry; no approval artifacts; no foreign decision/rationale/evidence payload copy |
| Mutation testing | Remove publication immutability, tenant/visibility/profile check, graph guard, transaction, mutation idempotency or version rejection and prove tests fail |
| Future UI only | Golden Compact verifier, explicit layout, seven-language RESX, no approval action; blocked until separately authorized |

Mandatory pre-scaffold topology/isolation matrix (all rows must pass for the primary; any failure selects the
fallback with no warning-only waiver):

| Gate | Required proof |
|---|---|
| Project graph | Exact layer dependency direction; zero DecisionRegistry↔DWS/BPM Domain/Application/Persistence references or cycles |
| Namespace/type | Decision aggregate, draft, revision, history and reference types exist only in DecisionRegistry; no reuse, inheritance or aliases across modules |
| Repository/DI | Module-private repository interfaces/implementations and registrations; no shared generic business repository or foreign service resolution |
| Mongo/index | Exact collection allowlist is disjoint; serializers, mappings, tenant-first indexes and index bootstrap fail on any DWS/BPM collision |
| Transaction | Only DecisionRegistry collections join the replica-set transaction; fault injection and concurrency prove rollback and one revision/pointer/audit intent |
| Audit/outbox | No direct Platform collection or in-transaction broker write; retryable post-commit dispatch cannot mutate Decision state or duplicate mutation |
| Migration/bootstrap | DecisionRegistry initializer touches only its owned collections; no TTL on immutable published state; no DWS/BPM seed/index reuse |
| Permission | MOD-0007 permission family is disjoint from DWS/BPM and approval permissions; no wildcard/bypass |
| Internal provider | Producer-owned direct S2S query only, zero writes, no Gateway/browser/client dependency |
| Contract | Exact five-field reference and Gate I-A fixtures; no generic role or foreign Decision payload copy |
| Topology exclusivity | Exactly one selected service tree/registration; no dual scaffold and no EnterpriseStrategy reference/namespace |
| Entity base | Direct `TenantScopedEntity` inheritance; no competing base or inherited-field redeclaration; scoped `DeletedAt` only |
| Consumer gate | Build/architecture assertion rejects executable MOD-0117 adapter before all producer and bilateral evidence gates pass |

Mandatory security/compatibility fixtures additionally prove:

- forbidden tenant/actor/recorder/publisher/approver/delegation/context fields at any payload nesting level
  return `400` with zero writes/audit;
- invalid/expired/revoked S2S issuer, audience, credential or scope, end-user-token-only calls, and wrong
  consumer registration fail before lookup, while delegation tenant/operation/validity/anti-replay checks
  remain an independent gate;
- missing, cross-tenant, soft-deleted, invisible and non-referenceable cases have the same `404` envelope and
  bounded timing behavior;
- fault injection after each revision/history/pointer/edge/idempotency/audit write leaves zero partial state,
  and concurrent publication allocates one revision number/pointer/audit result;
- idempotency cannot replay across tenant, operation, effective actor or authenticated S2S client;
- supersession rejects self, duplicate, concurrent inverse edge, multi-hop cycle, cross-tenant/invisible
  endpoint and forged withdrawal target without residue;
- generic delete/bulk-delete/TTL/purge is absent for published history and soft-deleted drafts; legal hold
  blocks draft deletion, and no platform-admin bypass implies purge authority;
- MOD-0288 Person/Position matrices cover active, inactive, missing, cross-tenant, invisible, unavailable,
  malformed, indeterminate and unsupported-version outcomes;
- publisher differs from draft creator and latest updater under ordinary/admin/emergency/import-shaped calls;
- v1 evidence list is empty-only, no attach surface/provider call exists, evidence-required policy is `409`;
- logs contain no credentials/tokens or full rationale/evidence payload.

Minimum mutation matrix:

| Mutation | Test must fail because |
|---|---|
| Create immutable revision before publication or later set `PublishedAtUtc` | Revision is not born final and immutable |
| Persist DecisionRole, SubjectReference, CorrelationId or IdempotencyKey in `DecisionRevisionReferenceV1` | Exact five-field generic serialization contract is violated |
| Collapse governing/supporting wrappers to one identity or swap their identity | MOD-0117-owned relationship role semantics are lost |
| Accept a missing/unknown Gate I-A profile name/version | Supported-profile fail-closed boundary is violated |
| Reject withdrawn/superseded revision during `HistoricalResolve` | Historical exact-reference resolution contract is violated |
| Permit withdrawn/superseded revision during `NewReferenceEligibility` | New-binding retirement guard is violated |
| Accept `CurrentSelectionEligibility` | Non-selection Gate I-A mode boundary is violated |
| Treat `IsReferenceable=true` as the complete authoritative result | Bilateral mode/disposition semantics are bypassed |
| Allow `Role`/`GovernanceBody` or an AuthService RBAC Role as authority | Closed Person/Position MOD-0288 authority contract is violated |
| Remove creator/latest-updater versus publisher inequality | Prepare/publish separation of duties is violated |
| Add admin/emergency/import self-publish bypass | V1 SOD cannot be bypassed |
| Accept non-empty/local/mock evidence or call an evidence provider | Evidence-disabled v1 boundary is violated |
| Permit reopen of Withdrawn/Superseded record | Closed terminal lifecycle is violated |
| Add TTL or purge path to published history/graph or soft-deleted drafts | Retention/legal-hold owner decision is violated |
| Require idempotency or persist a receipt on read-only validation | Query-purity contract is violated |
| Drop governing `0..1` or closure requirement in MOD-0117 fixture | Consumer-owned PPM Gate I-A cardinality is violated |
| Add PPM transition/cardinality enforcement to MOD-0007 | Producer/consumer ownership boundary is violated |
| Add `Governing|Supporting` to generic Decision/DecisionRevision state or producer eligibility rules | Consumer relationship role is misowned |
| Remove tenant filter or map foreign lookup to 403 | Cross-tenant indistinguishable 404 contract is violated |
| Trust client tenant/actor/recorder/publisher/delegation/context | Trusted server-context boundary is violated |
| Treat Decision publication as approval | MOD-0007/MOD-0023 separation is violated |
| Treat recorder/publisher/approver as Decision authority | Semantic authority distinction is violated |
| Allow supersession self/duplicate/cycle or fabricate withdrawal target | Append-only graph/disposition integrity is violated |
| Commit aggregate while audit intent fails | Atomic audit rollback is violated |
| Disable idempotency unique scope | Duplicate semantic revision/audit is possible |
| Accept unknown contract version or cache fallback | Required 503 fail-closed behavior is violated |
| Add foreign rationale/evidence payload to PPM | Consumer payload-copy ban is violated |
| Accept non-empty/local/mock evidence or invoke evidence provider | Evidence-disabled v1 boundary is violated |
| Allow evidence-required tenant policy to publish | Required `409` core-slice policy block is violated |
| Allow generic delete/bulk-delete of published record/history | Retention and immutable-history boundary is violated |
| Share a DecisionRegistry type/repository/collection/index/permission with DWS or BPM | Hard module isolation is violated |
| Scaffold both candidate services or place DecisionRegistry in EnterpriseStrategy | Mutually exclusive topology is violated |
| Add Gateway/browser dependency to the internal validation provider | Direct S2S provider boundary is violated |
| Enable the MOD-0117 adapter before producer runtime and bilateral evidence | Consumer executable gate is violated |
| Guess a port, internal route or credential scheme | Open B-class allocation/security blocker is bypassed |
| Treat an isolation failure as a warning | Fail-closed fallback selection is bypassed |

## 18. Ready-for-dev Checklist

- [x] Blueprint and registry exact-ID/name preflight passes for `MOD-0007 — Decision & Rationale Log`.
- [x] Parent pack remains `draft` with selected conditional primary service and no production authority.
- [x] Decision/rationale ownership and Decision-versus-Approval separation are explicit.
- [x] Generic `DecisionRevisionReferenceV1` has the exact canonical name/version and five-field key set with
  no `DecisionRole`; the MOD-0117-owned Gate I-A wrapper/role/cardinality is separate.
- [x] `DecisionRevisionReferenceV1`, Gate I-A wrapper/validation request and trusted request/transport context are separate.
- [x] Mutable draft and atomically born immutable published revision are separate.
- [x] PPM cardinality/lifecycle enforcement remains MOD-0117-owned.
- [x] Exact PPM governing/supporting wrapper identities, InvestmentCase context `1.0`, validation modes and
  mode-aware error semantics are reconciled from authority checkpoint `a2c7ff5b`.
- [x] Current UI is `none`; future field count/Golden Compact direction is documented without UI authority.
- [x] Enterprise Governance Process Owner closes lifecycle, revision replacement/publication and terminal disposition semantics.
- [x] Producer-side relationship-role eligibility is excluded/deferred from v1 and is not a core blocker.
- [x] `DecisionAuthorityReferenceV1` exact four-field contract, Person/Position kinds and MOD-0288 authority are approved.
- [x] Prepare/publish SOD is closed with creator/latest-updater inequality and no bypass.
- [x] V1 evidence integration is formally disabled; tenant evidence-required policy blocks with `409`.
- [x] V1 retention/legal-hold decision closes no-TTL/no-purge and draft soft-delete boundaries.
- [x] Primary/fallback physical topology and mandatory fail-closed isolation evidence are closed.
- [ ] Exact transport, route, timeout/retry and compatibility policy are approved.
- [ ] MOD-0018 exact permission provisioning and dedicated S2S identity are executable and evidenced.
- [ ] MOD-0021/MOD-0035 audit-intent contract, credential and atomic-outbox integration are approved.
- [ ] Matching MOD-0117 consumer amendment is approved.
- [x] Future primary/fallback five-layer and test roots are exact and mutually exclusive.
- [x] Conditional primary `service` and concrete `TenantScopedEntity` usage are fixed; shell remains intentionally `none`.
- [ ] Mandatory pre-scaffold architecture/isolation matrix passes for the selected primary.
- [ ] An authoritative collision-free service port is allocated; until then port is `TBD`.
- [ ] Explicit user production authority is recorded; only then may status become `approved` or
  `ready-for-dev`.

## 19. Implementation Notes

- Blueprint `Blueprint_Data` and registry both prove the exact canonical identity and name. Prototype fields
  are inventory evidence only and must not be copied into the authoritative schema.
- The primary topology is isolated DecisionRegistry within a future ManagementGovernance service. The separate
  EnterpriseGovernance service replaces it if any mandatory isolation gate fails; they never coexist.
  Lower-authority domain text that still calls MOD-0007 topology `TBD` is protected governance drift for a
  later reconciliation and does not reopen this pack-specific decision or grant runtime authority.
- Decision revision publication is a semantic operation, not a CRUD update. Technical `Version` remains
  optimistic concurrency and cannot represent semantic revision.
- `DecisionDraft` is the only mutable pre-publication object. `PublishDecisionDraft` creates the immutable
  revision with publication provenance in the same transaction as history, pointer, idempotency and audit;
  there is no recorded-but-unpublished revision state.
- The five-field generic `DecisionRevisionReferenceV1` is persistable. The MOD-0117-owned relationship role
  is encoded by the exact governing/supporting Gate I-A wrapper identity; context/profile/mode belongs to
  validation; tenant/actors/
  correlation/S2S belongs to trusted context; mutation idempotency is command infrastructure.
- MOD-0117 owns its InvestmentCase reference cardinality and closure transition. MOD-0007 validates the
  submitted typed binding under an approved profile and supplies compatibility fixtures only.
- PPM authority checkpoint `a2c7ff5b` closes the two exact Gate I-A wrapper identities, shared InvestmentCase
  context and consumer mode/error baseline. It does not authorize a MOD-0007 runtime, service, endpoint or
  status promotion.
- Authority is semantic business accountability, not technical recording/publishing or approval. V1 uses
  exact `management-governance.decision-authority-reference/1.0`, Person/Position only, with MOD-0288 as SoR.
- Prepare/publish SOD rejects the draft creator and latest updater as publisher; no admin/emergency/import
  bypass exists. Evidence is empty-only and provider-free in v1. Published history has no TTL or purge.
- Consumer reference validation returns minimal referenceability data. Full Decision details require a
  separately authorized read contract and permission; PPM does not need it for Gate I-A.
- Existing Enterprise Strategy `DecisionReference` strings, mock values, `DecisionReferencesJson`, rationale
  inputs and `ApprovedBy` fields require a separate containment/migration plan and WorkCenter Gate 2 where
  applicable. This pack neither activates nor changes them.

### Change Log

| Date | Change | Authority effect |
|---|---|---|
| 2026-08-02 | Final draft role-ownership cleanup: generic `DecisionRevisionReferenceV1` fixed to the canonical five-field value; `DecisionRole` moved out of the generic tuple into the then-proposed MOD-0117-owned Gate I-A relationship wrapper/request boundary; producer and consumer validation/cardinality/lifecycle ownership reconciled. | None; at this checkpoint profile identity and any role-eligibility policy remained OPEN; the later bilateral row closes only PPM profile identity. |
| 2026-08-02 | Bilaterally aligned with PPM checkpoint `a2c7ff5b`: accepted exact governing/supporting wrapper and InvestmentCase-context identities at `1.0`; fixed HistoricalResolve/NewReferenceEligibility semantics, rejected CurrentSelectionEligibility, and required mode-aware fixtures instead of boolean-only authority. | None; MOD-0117 still owns relationship/cardinality/lifecycle, MOD-0007 stays `draft`, and no runtime authority is created. |
| 2026-08-03 | Closed A-class owner decisions: closed lifecycle/revision replacement, exact MOD-0288 Person/Position DecisionAuthority v1, prepare/publish SOD without bypass, evidence-disabled core, and no-TTL/no-purge retention/legal-hold baseline. | None; at that checkpoint physical topology remained open, status stayed `draft`, and no runtime authority was created. |
| 2026-08-03 | Closed B-class physical topology: selected isolated `Diten.ManagementGovernanceService/DecisionRegistry`, fixed the mutually exclusive EnterpriseGovernance fallback, exact future five-layer/test roots, shared-base usage, collection/transaction/provider boundaries and mandatory pre-scaffold isolation matrix. | None; status and authorities remain unchanged, port/route/security/deployment blockers stay open, and no scaffold/runtime/consumer authority is created. |

## 20. Follow-up Items

Only B-class runtime/promotion blockers remain:

1. Run the mandatory pre-scaffold isolation matrix; any failure selects the sole EnterpriseGovernance fallback.
2. Obtain an authoritative collision-free port allocation and approve internal route/service discovery,
   DNS/TLS/network and deployment decisions.
3. Approve and evidence dedicated S2S credential, issuer/audience/scope, registration/rotation/revocation,
   timeout/retry/circuit and delegated-actor policy.
4. Provision and evidence exact MOD-0018 permissions and MOD-0288 provider connectivity.
5. Approve and evidence MOD-0021/MOD-0035 audit-intent/post-commit outbox integration.
6. Approve the matching MOD-0117 consumer amendment only after producer runtime and bilateral evidence exists.
7. Reconcile the protected lower-authority domain topology wording in a separately authorized governance change.
8. Enumerate exact composition/project files and record explicit runtime authority; until then this pack remains non-executable.

Deferred future amendments, not v1 core blockers: MOD-0031 evidence-enabled publication, MOD-0030-backed
retention/legal-hold purge process, `Role`/`GovernanceBody` authority kinds, producer role-eligibility policy,
and approved import/migration flow.

After those decisions, revise this parent pack (or create explicitly parented executable follow-ups), rerun
canonical and relative-link validation, obtain owner approval, and record explicit production authority before
any implementation.

## Authority References

- [Management & Governance domain config](../domain-config.md)
- [DCP-006 — Portfolio Delivery Process Core](../../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md)
- [MOD-0117 — Project & Portfolio Management](../../portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md)
- [Module ID Registry](../../../registries/module-id-registry.md)
- [Master Development Plan](../../../portfolio/master-development-plan.md)
- [Module Pack Standard](../../../../.antigravity/rules/module-pack-standard.md)
