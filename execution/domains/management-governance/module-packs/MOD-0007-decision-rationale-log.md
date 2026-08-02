---
id: MOD-0007
name: Decision & Rationale Log
domain: management-governance
service: TBD
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
> permission registration or production integration. Physical service placement remains `TBD`; implementation
> requires owner approval, an approved topology decision, a matching executable pack revision and explicit
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
- Governing and supporting roles on typed decision references.
- Typed metadata-only evidence references.
- Append-only supersession and withdrawal history.
- Authoritative, tenant-aware and visibility-aware DecisionReference validation.

### Explicitly not owned

- Approval task, approval eligibility, approval workflow, SLA, escalation, delegation or
  `ApprovalOutcome`; MOD-0023 owns these.
- Portfolio, InvestmentCase or BenefitCommitment; MOD-0117 owns these.
- BudgetVersion (MOD-0136), Scenario/Comparator (MOD-0138), or actual outcome/realization (MOD-0072).
- TaskAggregate (MOD-0024), WorkCenter, workflow runs or operational task lifecycle.
- Document binary, evidence payload or generic document metadata.
- Foreign consumer lifecycle transitions, including PPM `UnderAnalysis → Closed`.

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
| `DecisionEvidenceReference` | Typed/versioned metadata reference to evidence owned elsewhere | Captured inside an immutable revision; no binary/payload copy |
| `DecisionAuthorityReference` | Typed/versioned semantic identity of the accountable decision-maker, role or governance body | Server-authoritatively validated and captured in the immutable published revision; never inferred from recorder, publisher or approver |
| `DecisionPublicationHistory` | Append-only publication and disposition provenance | `Published`, `Withdrawn` and `Superseded` events append; published revision bytes never change |
| `DecisionSupersessionLink` | Exact append-only edge from the older affected published revision to its replacement published revision | Same-tenant, cycle-safe and concurrency-safe creation; withdrawal without replacement creates no edge |
| `DecisionReference` | Opaque consumer value object for an exact immutable revision and role | Validated authoritatively by MOD-0007 |

Planned repository, command, query, DTO, endpoint and transport artifacts remain design directions, not
authorized files. Any executable revision must preserve separate aggregate/repository/collection types from
MOD-0354 DWS and MOD-0355 BPM and must not share their domain models.

### Generic DecisionReference contract proposal

Exact contract name: **`management-governance.decision-reference`**. Exact initial version: **`1.0`**.

| Field | Type | v1 rule |
|---|---|---|
| `ContractName` | string | Exact `management-governance.decision-reference` |
| `ContractVersion` | string | Exact `1.0`; unknown versions fail closed |
| `DecisionId` | Guid | Canonical, non-empty stable Decision identity |
| `DecisionRevisionId` | Guid | Canonical, non-empty immutable published revision identity |
| `DecisionRevisionNumber` | positive integer | Immutable semantic revision; not technical concurrency `Version` |
| `DecisionRole` | closed enum | `Governing` or `Supporting` only |

The persistable value contains exactly those six fields. It contains no subject, tenant, actor,
`CorrelationId`, `IdempotencyKey`, S2S identity, audience or scope.

### Authoritative validation request and trusted context

The read-only `DecisionReferenceValidationRequest` contains the six-field `DecisionReference`, a typed
`SubjectReference { Namespace, SubjectType, SubjectId }`, and an exact consumer profile name/version. The
request carries no tenant or actor. Trusted request/transport context independently supplies server-derived
`TenantId`, effective actor and separately validated delegated actor, `CorrelationId`, and authenticated S2S
identity/audience/scope. A read-only validation query requires no mutation `IdempotencyKey` and persists no
receipt, audit intent, cache or business mutation.

Successful validation returns the exact six-field reference plus `IsReferenceable: true`; it returns no
decision text, rationale, provenance, evidence payload or trusted-context echo. MOD-0007 validates the exact
published revision, governing/supporting role validity, subject binding, profile compatibility, tenant,
visibility and referenceability without revealing which 404 predicate failed.

The core `management-governance.decision-reference` contract is generic. Gate I-A uses a separately named
and versioned PPM/InvestmentCase consumer profile whose exact identity remains owner approval-blocked. Its
allowlist is exactly `Namespace=ppm`, `SubjectType=InvestmentCase`, and roles `Governing|Supporting`. A new
consumer or subject type requires an explicitly approved profile/version and compatibility fixtures; it
cannot silently widen Gate I-A.

### PPM-owned Gate I-A compatibility requirements

MOD-0117, not MOD-0007, owns and enforces:

- `InvestmentCase → GoverningDecisionReference`: `0..1` structurally.
- Before PPM permits `InvestmentCase UnderAnalysis → Closed`: exactly `1` valid governing reference.
- `SupportingDecisionReferences`: `0..n`; duplicates of the same Decision revision and role are rejected.
- PPM stores only the opaque contract value. It cannot copy decision text, result, rationale, provenance or
  evidence payload and cannot infer validation from a cache.

MOD-0007 supplies authoritative validation and compatibility fixtures only. It does not own the
InvestmentCase aggregate, stored-reference duplicate/cardinality enforcement, the closure prerequisite or
the PPM lifecycle transition/transaction.

## 4. Entity Fields

The following is the proposed semantic schema for a future tenant-owned implementation. Frontmatter
`entity_base: EntityBase` is the valid logical tenant-owned persistence contract required by the module-pack
standard; it is not `TBD`. The exact service-local implementation remains blocked by physical topology and
must be fixed in an executable revision without inventing a new base type. Base fields (`Id`, `TenantId`,
`IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, technical `Version`) are inherited and must not be
redeclared. `TenantId` is always server-derived.

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

### DecisionDraft

`DecisionDraft` carries the pre-publication semantic fields, proposed `DecisionAuthorityReference`,
provenance and optional evidence-reference candidates. It has `DraftId`, technical `Version`, server-derived
creator/updater and timestamps, but no `DecisionRevisionId`, revision number or publication timestamp. It may
be edited with optimistic concurrency. An unpublished draft with no immutable publication/history may be
soft-deleted only under an approved draft policy; published records and revisions have no CRUD delete path.

### DecisionRevision

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `DecisionRevisionId` | Guid | Yes | Immutable canonical revision identity |
| `DecisionId` | Guid | Yes | Same-tenant parent DecisionRecord |
| `DecisionRevisionNumber` | positive integer | Yes | Monotonic per Decision; unique and immutable |
| `DecisionText` | string | Yes | Trim + NFC; non-empty; max 8000 |
| `DecisionResult` | string | Yes | Trim + NFC; non-empty; max 4000 |
| `Rationale` | string | Yes | Trim + NFC; non-empty; max 12000 |
| `DecisionAuthorityReference` | typed/versioned value | Yes | Accountable `Person`, `Position`, `Role` or `GovernanceBody`; exact provider/model remains open |
| `ProvenanceKind` | closed enum | Yes | Owner-approved source category |
| `ProvenanceReference` | typed/versioned value | Yes | Opaque source reference; no foreign payload |
| `EvidenceReferences` | typed/versioned value list | No | Metadata references only; unique by contract/type/id/version |
| `RecordedBy` | Guid | Yes | Authenticated/delegated server actor |
| `RecordedAtUtc` | UTC instant | Yes | Server generated |
| `PublishedBy` | Guid | Yes | Technical authenticated/delegated actor executing publication; not business Decision authority |
| `PublishedAtUtc` | UTC instant | Yes | Server generated before insert; immutable revision is born published |

Indexes: unique `(TenantId, DecisionId, DecisionRevisionNumber)`, unique `(TenantId,
DecisionRevisionId)`. Mutation idempotency receipt/index is command infrastructure, not a semantic revision
field. A published revision has no update path; correction creates a new draft and atomically publishes a
new revision. Trusted `CorrelationId` may be recorded in bounded publication/audit provenance, but is not a
semantic DecisionRevision field or consumer DecisionReference field.

### DecisionAuthorityReference

| Field | Type | Required | Rule |
|---|---|---:|---|
| `AuthorityContractName` | string | Yes | Exact approved authority contract |
| `AuthorityContractVersion` | string | Yes | Exact supported version |
| `AuthorityProvider` | string | Yes | Authoritative provider/namespace; no free text fallback |
| `AuthorityType` | closed enum | Yes | `Person`, `Position`, `Role` or `GovernanceBody` only, subject to owner approval |
| `AuthorityId` | opaque canonical identity | Yes | Server-authoritatively validated; no client-asserted ActorId |

The exact provider, type/version model and validation authority remain OPEN. `DecisionAuthorityReference` is
distinct from `RecordedBy`, `PublishedBy`, and MOD-0023 approver/`ApprovalOutcome`.

### DecisionEvidenceReference, disposition and supersession

| Field | Type | Required | Rule |
|---|---|---:|---|
| `EvidenceContractName` | string | Yes | Registered exact contract name |
| `EvidenceContractVersion` | string | Yes | Exact supported version; open decision for initial version |
| `EvidenceKind` | closed enum | Yes | Metadata classification only |
| `EvidenceId` | Guid | Yes | Opaque canonical identity; payload/binary forbidden |
| `SourceDecisionId` / `SourceDecisionRevisionId` | Guid pair | For supersession | Exact older affected immutable published revision |
| `TargetDecisionId` / `TargetDecisionRevisionId` | Guid pair | For supersession | Exact replacement immutable published revision |
| `SupersessionKind` | closed enum | For supersession | Owner-approved replacement kind; withdrawal is not a fake edge |
| `Reason/ProvenanceReference` | typed/versioned value | Yes | Required disposition reason and authoritative provenance |
| `EffectiveAtUtc` | UTC instant | For supersession | Server normalized; append-only history |

Both edge endpoints are same-tenant published revisions. Self-links, duplicate edges and directed cycles are
forbidden. Edge creation is concurrency-safe and atomic with disposition/publication effects and audit
intent. Withdrawal without a replacement appends a `DecisionPublicationHistory(Withdrawn)` event referencing
the affected exact revision; it never fabricates a source/target link.

## 5. Repo Scope

This draft authoring milestone changes only:

- `execution/domains/management-governance/module-packs/MOD-0007-decision-rationale-log.md`

A future executable revision must replace this section with exact service, contract, test and optional UI
paths after physical topology and transport are approved. This pack grants no scope under `services/**`,
`frontend/**` or `gateway/**`.

## 6. Protected Paths

- `.antigravity/**`
- `services/**`
- `frontend/**`
- `gateway/**`, including every `ocelot.json`
- `execution/domains/**` outside this exact pack file
- `execution/portfolio/**`, `execution/registries/**` and Blueprint workbooks
- archive/frozen paths
- MOD-0023, MOD-0117, MOD-0136, MOD-0138, MOD-0072, MOD-0024 and WorkCenter runtime/governance
- existing Enterprise Strategy prototype fields, mocks, pages and data

## 7. Dependencies

| Dependency | Purpose | Gate |
|---|---|---|
| DCP-006 | Gate I-A orchestration and ownership boundaries | Approved orchestration; does not authorize MOD-0007 runtime |
| MOD-0018 | Exact permission and tenant/actor authorization evaluation | Runtime contract/evidence required before implementation |
| MOD-0021 / MOD-0035 | Immutable audit intent delivery over approved event/outbox mechanics | Exact producer contract and credentials required |
| MOD-0031 or approved evidence producer contracts | Typed evidence metadata validation | Contract name/version remains open |
| MOD-0023 | Optional distinct ApprovalOutcome only when policy requires approval | Separate PSS amendment and contract approval required |
| MOD-0117 | Consumer of `management-governance.decision-reference` for Gate I-A | Matching consumer amendment and explicit runtime authority required |

Prototype Enterprise Strategy `DecisionReference`, `DecisionReferencesJson`, `ApprovedBy` and rationale fields
are non-authoritative evidence only. They are not dependencies, schemas, migration truth or runtime baselines.

## 8. Runtime Constraints

- This draft assigns no service, port, route, collection, database or production authority.
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
- Publishing atomically validates the draft, authority, provenance and any required/submitted evidence;
  allocates the next revision identity/number; inserts the already-final immutable `DecisionRevision` with
  non-null publication provenance; appends publication/disposition history; compare-and-swaps the
  `DecisionRecord` pointer/state; optionally inserts a valid supersession edge; persists the idempotency
  result; and writes producer-local audit intent/outbox in one Mongo replica-set transaction. Failure at any
  step rolls back all state.
- Correlation and operation-scoped idempotency are mandatory. A replay with the same canonical request
  returns the same result; the same key with different payload returns `409`.
- PPM-facing validation uses a dedicated authenticated S2S identity with exact issuer/audience/scope and an
  independently validated delegated actor. End-user JWT forwarding alone is insufficient; the service and
  effective-actor authorization gates are independent.
- Read-only validation requires no idempotency key and writes no audit intent, receipt, cache or mutation.
- Optional empty evidence causes no provider call. Evidence-provider availability matters only when policy
  requires evidence or the submitted draft contains a reference requiring validation; required timeout,
  unavailable, malformed or unsupported-version validation returns `503` and blocks publication.
- Only an unpublished draft with no immutable publication/history may use policy-approved soft delete.
  Published records/revisions/history/edges have no ordinary delete or bulk-delete path; withdrawal and
  supersession are named append-only dispositions. Any exceptional purge is a separately authorized
  compliance process subject to retention/legal hold and cannot reuse module CRUD permissions.
- Exact references to withdrawn/superseded revisions remain immutable historical facts. Whether they accept
  new bindings or only historical verification is decided by an explicitly versioned referenceability
  profile; disposition never physically rewrites consumer history.
- Published revisions, disposition events and supersession edges are immutable. Technical optimistic
  concurrency `Version` is distinct from `DecisionRevisionNumber`.

### Physical service topology decision direction

| Option | Assessment |
|---|---|
| A. Isolated `DecisionRegistry` internal module in a future `Diten.ManagementGovernanceService` | **Preferred direction**, conditional on hard architecture tests proving separate project/namespace, DWS/BPM/DecisionRegistry domain types, repositories, collections, permissions, migrations and dependency direction |
| B. Separate `Diten.EnterpriseGovernanceService` | Mandatory fallback if Option A isolation cannot be proven or operational/security ownership requires independent deployment |
| C. Placement in an existing unrelated service | Rejected; proximity, reuse or current prototype location is not ownership evidence |

The final A/B decision remains open. This pack does not reserve a port, add a Gateway route or authorize
either service scaffold.

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

No backend files are authorized. After topology approval, the executable pack revision must define exact
five-layer paths and CQRS artifacts while preserving distinct commands such as `CreateDecisionDraft`,
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
| `SubjectReference` | Yes | Gate I-A exact namespace `ppm`, type `InvestmentCase`, canonical non-empty Guid | Authoritative contract-version support and dedicated producer validation |
| `DecisionText` | Yes | Trim + NFC; 1..8000 | — |
| `DecisionResult` | Yes | Trim + NFC; 1..4000 | — |
| `Rationale` | Yes | Trim + NFC; 1..12000 | — |
| `ProvenanceKind/Reference` | Yes | Typed, versioned, supported and referenceable | Authoritative validator |
| `EvidenceReferences` | No | Typed/versioned; no duplicate tuple; metadata only | Authoritative validator |
| `DecisionRole` | Yes for consumer reference | `Governing` or `Supporting` | Contract exact-version validation |
| `DecisionRevisionNumber` | Yes | Positive, next monotonic number; client cannot overwrite current revision | Unique index + transaction |
| `DecisionAuthorityReference` | Yes on publication | Exact supported provider/type/version + opaque ID; no free text or actor substitution | Authoritative server-side validation |
| `ConsumerProfile` | Yes on reference validation | Exact approved name/version; Gate I-A allowlist cannot be widened | Authenticated S2S registration must match |
| `ActorId`, `DecisionActorId`, `RecordedBy`, `PublishedBy`, `ApprovedBy`, `TenantId` in payload | Forbidden | Reject request; never trust, merge or normalize into server context | Authenticated/delegated server context |
| Trusted `CorrelationId` | Mutations and validation | Bounded authenticated trace context; absent from persisted DecisionReference | Server transport context |
| `IdempotencyKey` | Mutation commands only | Required, scoped to tenant + operation + effective actor + S2S client and payload-bound | Idempotency store/index; forbidden requirement on read validation |

When evidence is optional and none is submitted, publication does not contact the evidence provider. When
policy requires evidence, absence is a policy-validation failure; when evidence is required or submitted,
authoritative validation is mandatory and provider failure is `503`.

## 13. Failure Path to Verify

- **Published revision update/delete attempt:** `409` or absent route; original bytes, history and pointer remain unchanged.
- **Draft publication fault:** failure after any revision/history/pointer/edge/idempotency/audit write rolls
  back all writes; no premature revision or half-published state exists.
- **Draft soft-delete race:** only an unpublished draft with no immutable history may be soft-deleted under
  policy; concurrent publication versus deletion permits at most one successful transaction.
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
- **Authority spoof/unavailable:** free-text/client actor substitution is rejected; a required authority
  provider timeout, malformed response or unsupported version is `503`; publication does not commit.
- **Optional evidence unused/provider unavailable:** publication proceeds and provider is not called. If
  evidence is policy-required or submitted, provider failure is `503` and publication fully rolls back.
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
visibility scope. Platform-admin bypass, if any, must be explicitly approved later; it is not inferred here.
No permission containing `approve`, `reject`, `approval`, `delegate`, `sla` or `escalate` belongs to MOD-0007.
The validation permission grants no Decision-details read. Any exceptional compliance purge requires a
separately approved identity/process and cannot reuse `.delete`, `.withdraw`, platform-admin bypass or these
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
3. Persisted `management-governance.decision-reference` version `1.0` has exactly six fields and excludes
   subject, correlation, idempotency, tenant, actor and S2S context. The read-only validation request and
   trusted transport context are separate.
4. An exact approved PPM/InvestmentCase Gate I-A profile validates role, subject binding and referenceability.
   MOD-0117-owned compatibility fixtures prove governing `0..1`, exactly one before
   `UnderAnalysis → Closed`, and supporting `0..n`; MOD-0007 does not enforce that transition/cardinality.
5. PPM stores only opaque DecisionReference values; architecture tests ban decision text, result, rationale,
   provenance and evidence payload fields in the consumer boundary.
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
16. `DecisionAuthorityReference` identifies the accountable business person/position/role/body and is
    server-authoritatively validated; it is distinct from recorder, publisher and MOD-0023 approver/outcome.
17. Only policy-eligible unpublished drafts may be soft-deleted. Published records/revisions/history have no
    CRUD delete/bulk-delete; exceptional purge remains a separately authorized compliance process.
18. Empty optional evidence does not call or depend on the evidence provider; required or submitted evidence
    fails publication with `503` when authoritative validation is unavailable/malformed/unsupported.
19. New consumers/subject types require an explicitly approved profile/version and fixtures; Gate I-A's exact
    `ppm`/`InvestmentCase` allowlist cannot widen silently.

## 17. Test Expectations

| Test layer | Mandatory evidence before runtime promotion |
|---|---|
| Domain unit | Draft versus published-revision boundary, atomic immutable birth, monotonic numbering, authority distinction, supersession graph and withdrawal-disposition invariants |
| Application unit | Exact permission, forbidden context-field rejection, independent S2S/delegated-actor gates, mutation idempotency, query purity, optimistic concurrency and Decision/Approval separation |
| Persistence integration | Fault injection after revision/history/pointer/edge/idempotency/audit writes, tenant-first queries, draft soft-delete/publication race and cross-tenant 404 equivalence |
| Contract compatibility | Exact six-field DecisionReference serialization; separate request/context; generic 1.0 plus approved Gate I-A profile; MOD-0117-owned cardinality fixtures; malformed 400 versus well-formed unsupported 503 |
| S2S security | Dedicated identity, delegated actor validation, 401/403/404/409/503 matrix, no tenant/existence disclosure |
| Architecture | Project/namespace/type/repository/collection/permission/migration isolation for DWS/BPM/DecisionRegistry; no approval artifacts; no foreign decision/rationale/evidence payload copy |
| Mutation testing | Remove publication immutability, tenant/visibility/profile check, graph guard, transaction, mutation idempotency or version rejection and prove tests fail |
| Future UI only | Golden Compact verifier, explicit layout, seven-language RESX, no approval action; blocked until separately authorized |

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
- generic delete/bulk-delete is absent for published records/history, legal hold blocks exceptional purge,
  and no platform-admin bypass implies purge authority;
- logs contain no credentials/tokens or full rationale/evidence payload.

Minimum mutation matrix:

| Mutation | Test must fail because |
|---|---|
| Create immutable revision before publication or later set `PublishedAtUtc` | Revision is not born final and immutable |
| Persist SubjectReference, CorrelationId or IdempotencyKey in DecisionReference | Exact six-field serialization contract is violated |
| Require idempotency or persist a receipt on read-only validation | Query-purity contract is violated |
| Drop governing `0..1` or closure requirement in MOD-0117 fixture | Consumer-owned PPM Gate I-A cardinality is violated |
| Add PPM transition/cardinality enforcement to MOD-0007 | Producer/consumer ownership boundary is violated |
| Remove tenant filter or map foreign lookup to 403 | Cross-tenant indistinguishable 404 contract is violated |
| Trust client tenant/actor/recorder/publisher/delegation/context | Trusted server-context boundary is violated |
| Treat Decision publication as approval | MOD-0007/MOD-0023 separation is violated |
| Treat recorder/publisher/approver as Decision authority | Semantic authority distinction is violated |
| Allow supersession self/duplicate/cycle or fabricate withdrawal target | Append-only graph/disposition integrity is violated |
| Commit aggregate while audit intent fails | Atomic audit rollback is violated |
| Disable idempotency unique scope | Duplicate semantic revision/audit is possible |
| Accept unknown contract version or cache fallback | Required 503 fail-closed behavior is violated |
| Add foreign rationale/evidence payload to PPM | Consumer payload-copy ban is violated |
| Call evidence provider for empty optional evidence or fail on its outage | Optional-evidence independence is violated |
| Skip validation for submitted/policy-required evidence | Required authoritative evidence validation is violated |
| Allow generic delete/bulk-delete of published record/history | Retention and immutable-history boundary is violated |

## 18. Ready-for-dev Checklist

- [x] Blueprint and registry exact-ID/name preflight passes for `MOD-0007 — Decision & Rationale Log`.
- [x] Parent pack starts as `draft` with `service: TBD` and no production authority.
- [x] Decision/rationale ownership and Decision-versus-Approval separation are explicit.
- [x] Proposed generic DecisionReference contract has an exact name/version/six-field value; Gate I-A roles
  and MOD-0117-owned compatibility cardinality are explicit while the profile identity remains blocked.
- [x] DecisionReference, validation request and trusted request/transport context are separate.
- [x] Mutable draft and atomically born immutable published revision are separate.
- [x] PPM cardinality/lifecycle enforcement remains MOD-0117-owned.
- [x] Current UI is `none`; future field count/Golden Compact direction is documented without UI authority.
- [ ] Enterprise Governance Process Owner approves lifecycle, revision publication authority and contract `1.0` semantics.
- [ ] Enterprise Governance Process Owner approves the exact Gate I-A consumer profile name/version and
  withdrawn/superseded referenceability policy.
- [ ] Exact DecisionAuthority provider/type/version, validation authority and prepare/publish separation of
  duties are approved.
- [ ] Enterprise Architect closes physical topology A/B and records isolation evidence requirements.
- [ ] Exact transport, route, timeout/retry and compatibility policy are approved.
- [ ] Retention, legal hold, archival and separately authorized exceptional-purge policy are approved without
  exposing ordinary delete/bulk-delete for published history.
- [ ] Initial typed evidence contract name/version and producer validation are approved.
- [ ] MOD-0018 exact permission provisioning and dedicated S2S identity are executable and evidenced.
- [ ] MOD-0021/MOD-0035 audit-intent contract, credential and atomic-outbox integration are approved.
- [ ] Matching MOD-0117 consumer amendment is approved.
- [ ] Repo scope is replaced with exact service/contracts/tests/UI paths.
- [ ] `service`, concrete base entity, shell, field count and Golden Reference are finalized.
- [ ] Explicit user production authority is recorded; only then may status become `approved` or
  `ready-for-dev`.

## 19. Implementation Notes

- Blueprint `Blueprint_Data` and registry both prove the exact canonical identity and name. Prototype fields
  are inventory evidence only and must not be copied into the authoritative schema.
- Option A (isolated DecisionRegistry within a future ManagementGovernance service) is the preferred direction
  only if architecture tests enforce type/repository/permission/collection boundaries. Option B (separate
  EnterpriseGovernance service) is the fail-closed fallback. Proximity never decides ownership.
- Decision revision publication is a semantic operation, not a CRUD update. Technical `Version` remains
  optimistic concurrency and cannot represent semantic revision.
- `DecisionDraft` is the only mutable pre-publication object. `PublishDecisionDraft` creates the immutable
  revision with publication provenance in the same transaction as history, pointer, idempotency and audit;
  there is no recorded-but-unpublished revision state.
- The six-field generic DecisionReference is persistable. Subject/profile belongs to the validation request;
  tenant/actors/correlation/S2S belongs to trusted context; mutation idempotency is command infrastructure.
- MOD-0117 owns its InvestmentCase reference cardinality and closure transition. MOD-0007 validates the
  submitted typed binding under an approved profile and supplies compatibility fixtures only.
- Authority is semantic business accountability, not technical recording/publishing or approval. Exact
  authority-provider policy remains a ready-for-dev blocker.
- Consumer reference validation returns minimal referenceability data. Full Decision details require a
  separately authorized read contract and permission; PPM does not need it for Gate I-A.
- Existing Enterprise Strategy `DecisionReference` strings, mock values, `DecisionReferencesJson`, rationale
  inputs and `ApprovedBy` fields require a separate containment/migration plan and WorkCenter Gate 2 where
  applicable. This pack neither activates nor changes them.

## 20. Follow-up Items

Only unresolved owner/architecture decisions remain:

1. Close physical service topology: conditional Option A or separate-service Option B fallback.
2. Choose exact validation transport and route plus timeout/retry/compatibility policy.
3. Approve retention, archival, legal-hold and purge behavior for immutable revisions/history.
4. Approve the initial typed evidence contract name/version and evidence referenceability semantics.
5. Approve the DecisionAuthority provider/type/version and authoritative validation model.
6. Define who may prepare a draft and publish a revision, including separation of duties, without importing
   MOD-0023 approval behavior.
7. Approve the exact Gate I-A consumer profile name/version and versioned referenceability behavior for
   withdrawn/superseded exact revisions.

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
