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

## 3. Owned Objects

| Object | Ownership purpose | Mutation model |
|---|---|---|
| `DecisionRecord` | Stable aggregate identity, tenant boundary, lifecycle and current published revision pointer | Created once; lifecycle changes only through named commands |
| `DecisionRevision` | Immutable semantic snapshot of decision text/result/rationale/provenance | Append-only; a recorded revision is never edited in place |
| `DecisionEvidenceReference` | Typed/versioned metadata reference to evidence owned elsewhere | Captured inside an immutable revision; no binary/payload copy |
| `DecisionSupersessionLink` | Append-only relationship from a superseding/withdrawing decision revision to the affected decision revision | Created atomically with publication; never rewritten |
| `DecisionReference` | Opaque consumer value object for an exact immutable revision and role | Validated authoritatively by MOD-0007 |

Planned repository, command, query, DTO, endpoint and transport artifacts remain design directions, not
authorized files. Any executable revision must preserve separate aggregate/repository/collection types from
MOD-0354 DWS and MOD-0355 BPM and must not share their domain models.

### PPM Gate I-A producer contract proposal

Exact contract name: **`management-governance.decision-reference`**. Exact initial version: **`1.0`**.

| Field | Type | v1 rule |
|---|---|---|
| `ContractName` | string | Exact `management-governance.decision-reference` |
| `ContractVersion` | string | Exact `1.0`; unknown versions fail closed |
| `DecisionId` | Guid | Canonical, non-empty stable Decision identity |
| `DecisionRevisionId` | Guid | Canonical, non-empty immutable published revision identity |
| `DecisionRevisionNumber` | positive integer | Immutable semantic revision; not technical concurrency `Version` |
| `DecisionRole` | closed enum | `Governing` or `Supporting` only |
| `SubjectReference.Namespace` | closed string | Exact `ppm` for Gate I-A |
| `SubjectReference.SubjectType` | closed string | Exact `InvestmentCase` for Gate I-A |
| `SubjectReference.SubjectId` | Guid | Canonical, non-empty opaque PPM InvestmentCase identity |
| `CorrelationId` | string | Required bounded trace identity |
| `IdempotencyKey` | string | Required, operation-scoped and payload-bound |

`TenantId`, requesting actor and delegated actor are authenticated server context, not contract payload.
Successful validation returns the exact reference plus `IsReferenceable: true`; it returns no decision text,
rationale, provenance or evidence payload. The authoritative result includes tenant, visibility and
referenceability evaluation without revealing which 404 predicate failed.

PPM cardinality is consumer-owned but contractually fixed for Gate I-A:

- `InvestmentCase → GoverningDecisionReference`: `0..1` structurally.
- Before PPM permits `InvestmentCase UnderAnalysis → Closed`: exactly `1` valid governing reference.
- `SupportingDecisionReferences`: `0..n`; duplicates of the same Decision revision and role are rejected.
- PPM stores only the opaque contract value. It cannot copy decision text, result, rationale, provenance or
  evidence payload and cannot infer validation from a cache.

## 4. Entity Fields

The following is the proposed semantic schema for a future tenant-owned implementation. `entity_base:
EntityBase` is a contract direction only; the concrete equivalent must be confirmed after topology is
approved. Base fields (`Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, technical
`Version`) are inherited and must not be redeclared. `TenantId` is always server-derived.

### DecisionRecord

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `DecisionId` | Guid | Yes | Stable canonical identity; may equal aggregate `Id`; immutable |
| `SubjectKind` | closed string enum | Yes | Registered typed subject kind; arbitrary free text forbidden |
| `SubjectId` | Guid | Yes | Canonical non-empty opaque identity |
| `LifecycleState` | closed enum | Yes | `Draft`, `Published`, `Withdrawn`, `Superseded`; no approval states |
| `CurrentRevisionId` | Guid? | No | Set only to a successfully published immutable revision |
| `CurrentRevisionNumber` | positive integer? | No | Monotonic semantic revision; never named `Version` |
| `CreatedBy` | Guid | Yes | Authenticated/delegated actor from server context |
| `UpdatedBy` | Guid? | No | Server context only |

Indexes: tenant-first unique active `(TenantId, DecisionId)` and lookup
`(TenantId, SubjectKind, SubjectId, IsDeleted, LifecycleState)`. Soft-deleted, invisible and cross-tenant
records are never referenceable.

### DecisionRevision

| Field | Type | Required | Rule / index direction |
|---|---|---:|---|
| `DecisionRevisionId` | Guid | Yes | Immutable canonical revision identity |
| `DecisionId` | Guid | Yes | Same-tenant parent DecisionRecord |
| `DecisionRevisionNumber` | positive integer | Yes | Monotonic per Decision; unique and immutable |
| `DecisionText` | string | Yes | Trim + NFC; non-empty; max 8000 |
| `DecisionResult` | string | Yes | Trim + NFC; non-empty; max 4000 |
| `Rationale` | string | Yes | Trim + NFC; non-empty; max 12000 |
| `ProvenanceKind` | closed enum | Yes | Owner-approved source category |
| `ProvenanceReference` | typed/versioned value | Yes | Opaque source reference; no foreign payload |
| `EvidenceReferences` | typed/versioned value list | No | Metadata references only; unique by contract/type/id/version |
| `RecordedBy` | Guid | Yes | Authenticated/delegated server actor |
| `RecordedAtUtc` | UTC instant | Yes | Server generated |
| `PublishedAtUtc` | UTC instant? | No | Server generated on publication |
| `CorrelationId` | string | Yes | Validated trace identity |
| `IdempotencyKey` | string | Yes | Scoped to tenant + operation + actor; immutable |

Indexes: unique `(TenantId, DecisionId, DecisionRevisionNumber)`, unique `(TenantId,
DecisionRevisionId)`, and unique idempotency scope. A recorded revision has no update path; correction creates
a new revision.

### DecisionEvidenceReference and DecisionSupersessionLink

| Field | Type | Required | Rule |
|---|---|---:|---|
| `EvidenceContractName` | string | Yes | Registered exact contract name |
| `EvidenceContractVersion` | string | Yes | Exact supported version; open decision for initial version |
| `EvidenceKind` | closed enum | Yes | Metadata classification only |
| `EvidenceId` | Guid | Yes | Opaque canonical identity; payload/binary forbidden |
| `SupersededDecisionId` | Guid | For supersession | Same-tenant visible Decision |
| `SupersededRevisionId` | Guid | For supersession | Exact immutable published revision |
| `SupersessionKind` | closed enum | For supersession | `Supersedes` or `Withdraws` |
| `EffectiveAtUtc` | UTC instant | For supersession | Server normalized; append-only history |

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
- Actor identity comes only from authenticated server context. A delegated actor requires separately
  authenticated and authorized delegation evidence; client actor claims are rejected.
- Missing, soft-deleted, cross-tenant, invisible and non-referenceable records are indistinguishable `404`.
- Syntactically invalid input is `400`; authentication/context failure is `401`; exact permission failure is `403`; immutable revision,
  idempotency or concurrency conflict is `409`; dependency timeout, malformed response, unsupported contract
  version or unavailable authoritative validator is `503`.
- No default tenant, fail-open, stale-cache ownership inference or local consumer validation substitute.
- Every semantic mutation and its producer-local audit intent/outbox record commit in the same Mongo
  replica-set transaction. Audit/outbox failure rolls back the mutation.
- Correlation and operation-scoped idempotency are mandatory. A replay with the same canonical request
  returns the same result; the same key with different payload returns `409`.
- PPM-facing validation uses a dedicated authenticated S2S identity and an independently validated delegated
  actor. End-user JWT forwarding alone is insufficient.
- Recorded revisions and supersession history are immutable. Technical optimistic concurrency `Version` is
  distinct from `DecisionRevisionNumber`.

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
`RecordDecisionRevision`, `PublishDecisionRevision`, `SupersedeDecision` and `WithdrawDecision`. It must not
generate generic CRUD update/delete handlers for recorded revisions and must not expose approval commands.

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
| `ActorId`, `DecisionActorId`, `ApprovedBy`, `TenantId` in body | Forbidden | Reject request; never trust or normalize into server context | Authenticated/delegated server context |
| `CorrelationId` / `IdempotencyKey` | Yes on mutation | Valid bounded format; scoped and payload-bound | Idempotency store/index |

## 13. Failure Path to Verify

- **Recorded revision update attempt:** `409`; original bytes and publication pointer remain unchanged.
- **Syntactically invalid identifier, required value or closed enum:** `400`; no authoritative lookup or mutation.
- **Cross-tenant, missing, soft-deleted or invisible Decision:** indistinguishable `404`; no existence leak.
- **Client actor/tenant/ApprovedBy claim:** request rejected; no mutation or audit intent persists.
- **Permission denied:** `403`; authentication/context failure remains `401`.
- **Duplicate idempotency replay:** identical payload returns the original outcome; altered payload returns
  `409`; no second revision or audit intent.
- **Unknown contract version, malformed response, timeout or validator unavailable:** `503`; no local-cache
  inference and no consumer transition.
- **Concurrent publication/revision number conflict:** one succeeds; loser receives `409`; no silent overwrite.
- **Audit intent/outbox persistence failure:** aggregate, revision, pointer and link all roll back.
- **Supersession target invalid or already incompatibly superseded:** `404` for visibility failures or `409`
  for visible state conflict; history remains append-only.
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

All commands/queries require authenticated tenant context, exact MOD-0018-backed permission evaluation and
visibility scope. Platform-admin bypass, if any, must be explicitly approved later; it is not inferred here.
No permission containing `approve`, `reject`, `approval`, `delegate`, `sla` or `escalate` belongs to MOD-0007.

## 15. Gateway / API Routing Decision

No Gateway/API transport or route is authorized. Exact transport remains open. A future executable revision
must name the internal validation transport, route, service identity, timeout/retry budget and compatibility
policy. Frontend, if later authorized, must call Gateway through the approved shell profile and never a
service port directly. Only `integration-agent` may modify `ocelot.json` after explicit route authority.

## 16. Acceptance Criteria

1. MOD-0007 is the sole generic Decision/rationale SoR; no owned object or API conflates Decision with
   MOD-0023 `ApprovalOutcome`.
2. Stable `DecisionId` points to immutable, uniquely numbered `DecisionRevision` records; a recorded revision
   has no in-place update path.
3. `management-governance.decision-reference` version `1.0` validates exact Guid identities, immutable revision,
   governing/supporting role, typed subject, tenant, visibility and referenceability fail closed.
4. PPM cardinality compatibility fixtures prove governing `0..1`, exactly one before
   `UnderAnalysis → Closed`, and supporting `0..n` without payload copies.
5. PPM stores only opaque DecisionReference values; architecture tests ban decision text, result, rationale,
   provenance and evidence payload fields in the consumer boundary.
6. Approval-required scenarios use a separate MOD-0023 `ApprovalOutcome` reference; no MOD-0007 or PPM
   approve/reject command, field, state or permission exists.
7. Cross-tenant, missing, soft-deleted, invisible and non-referenceable lookups return indistinguishable `404`.
8. Tenant and actor are derived from authenticated/delegated server context; client actor, tenant or
   `ApprovedBy` values are rejected and never persisted.
9. Revision publication, supersession/withdrawal link and audit intent/outbox commit atomically; injected audit
   failure proves full rollback.
10. Supersession and withdrawal history is append-only, targets an exact immutable revision and cannot be
    silently rewritten.
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

## 17. Test Expectations

| Test layer | Mandatory evidence before runtime promotion |
|---|---|
| Domain unit | Immutable revision, monotonic numbering, lifecycle, governing/supporting role, supersession/withdrawal invariants |
| Application unit | Exact permission, server actor/tenant rejection, idempotency, optimistic concurrency and Decision/Approval separation |
| Persistence integration | Real Mongo transaction rollback, unique revision/idempotency indexes, tenant-first queries, soft-delete and cross-tenant 404 |
| Contract compatibility | `management-governance.decision-reference` 1.0 fixtures, exact PPM/InvestmentCase subject, 400 syntax failures, PPM cardinality, unknown version 503, malformed/unavailable 503 |
| S2S security | Dedicated identity, delegated actor validation, 401/403/404/409/503 matrix, no tenant/existence disclosure |
| Architecture | Project/namespace/type/repository/collection/permission/migration isolation for DWS/BPM/DecisionRegistry; no approval artifacts; no foreign decision/rationale/evidence payload copy |
| Mutation testing | Remove immutability, tenant filter, visibility check, role/cardinality check, transaction, idempotency or version rejection and prove tests fail |
| Future UI only | Golden Compact verifier, explicit layout, seven-language RESX, no approval action; blocked until separately authorized |

Minimum mutation matrix:

| Mutation | Test must fail because |
|---|---|
| Permit recorded revision setter/update handler | Immutable-revision invariant is violated |
| Drop governing `0..1` or closure requirement | PPM Gate I-A cardinality is violated |
| Remove tenant filter or map foreign lookup to 403 | Cross-tenant indistinguishable 404 contract is violated |
| Trust client `DecisionActorId`/`ApprovedBy` | Server actor authority is violated |
| Treat Decision publication as approval | MOD-0007/MOD-0023 separation is violated |
| Rewrite/delete supersession link | Append-only history is violated |
| Commit aggregate while audit intent fails | Atomic audit rollback is violated |
| Disable idempotency unique scope | Duplicate semantic revision/audit is possible |
| Accept unknown contract version or cache fallback | Required 503 fail-closed behavior is violated |
| Add foreign rationale/evidence payload to PPM | Consumer payload-copy ban is violated |

## 18. Ready-for-dev Checklist

- [x] Blueprint and registry exact-ID/name preflight passes for `MOD-0007 — Decision & Rationale Log`.
- [x] Parent pack starts as `draft` with `service: TBD` and no production authority.
- [x] Decision/rationale ownership and Decision-versus-Approval separation are explicit.
- [x] Proposed PPM Gate I-A contract has an exact name, version, fields, roles and cardinality.
- [x] Current UI is `none`; future field count/Golden Compact direction is documented without UI authority.
- [ ] Enterprise Governance Process Owner approves lifecycle, revision publication authority and contract `1.0` semantics.
- [ ] Enterprise Architect closes physical topology A/B and records isolation evidence requirements.
- [ ] Exact transport, route, timeout/retry and compatibility policy are approved.
- [ ] Retention, legal hold, archival and purge policy are approved without violating immutable history.
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
5. Define who may record and publish a revision, and the lifecycle/revision publication authority model,
   without importing MOD-0023 approval behavior.

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
