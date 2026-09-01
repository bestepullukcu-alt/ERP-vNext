---
id: MOD-0354
name: Decomposition & Work Structuring Engine
domain: management-governance
dcp: DCP-006
service: Diten.ManagementGovernanceService
internal_module: Modules/Dws
module_code: MOD-0354
shell: tenant
golden_reference: none
entity_base: EntityBase
status: ready-for-dev
owner: ali.tufanoglu / enterprise-architect-interim
business_owner: ali.tufanoglu / delivery-structure-governance-owner-interim
technical_owner: ali.tufanoglu / management-governance-technical-owner-interim
legacy_parity_owner: ali.tufanoglu / delivery-structure-governance-owner-interim
branch: feature/mg/mod-0354-decomposition-work-structuring-engine
started: 2026-07-28
target: 2026-08-14
form_field_count: 0
port: 5017
implementation_authority: explicit-user-control-tower-bounded-core-scaffold-contract-test
b02_evidence_authority: explicit-user-control-tower-non-runtime-persistence-integration-evidence
backend_local_testability_authority: explicit-user-control-tower-non-production-default-off
functional_local_dws_api_authority: explicit-user-control-tower-non-production-default-off
production_authority: none
---

# MOD-0354 — Decomposition & Work Structuring Engine

> **Ready-for-dev authority reconciliation:** The explicit user decision dated 2026-09-01 reconciles the
> already verified authority/status record from
> `feature/mg/mod-0354-decomposition-work-structuring-engine` onto current `origin/main`. This governance-only
> checkpoint does not itself authorize service scaffolding, code transfer, runtime tests, Gateway, frontend,
> WorkCenter, deployment, secrets/keys, push or merge. Existing bounded authority fields above remain
> descriptive provenance; `production_authority: none` is binding.

> **Planning date:** `target: 2026-08-14` is a provisional planning date, not a delivery commitment or
> implementation authorization.

## 1. Module Summary

Wave 1 delivers tenant-isolated structural decomposition mechanics: a structure can be defined, arranged as
a hierarchy, checked for structural integrity, frozen into an immutable baseline and compared across
versions/baselines. It deliberately does not execute work.

The primary user is a tenant planning architect who structures an already-authoritative external context
such as an initiative, program, project or portfolio by typed reference. This is a tree/hierarchy workspace,
not a DataTable CRUD module. Therefore `golden_reference: none` and `form_field_count: 0` are intentional:
there is no conventional create/edit DataTable form whose fields can be counted.

`MOD-0354` is the Blueprint 8.1 canonical identity. Historical candidate `CAND-CAP-0008` is retained only
as a deprecated governance alias and must never appear in runtime code, routes, permissions, collections,
events, jobs or configuration.

## 2. Ownership and Boundaries

### In scope

- Structure creation and draft structural metadata editing.
- Structure creation also creates mutable Working Revision #1.
- Node add, move/reparent, reorder and remove.
- Parent/child hierarchy.
- Self-parent, cycle, orphan and duplicate-sibling-order validation.
- Pure structural dependency without scheduling/execution semantics.
- Immutable structure baseline creation.
- Structure-version and baseline comparison.
- Typed external-context reference.
- Optimistic concurrency and command idempotency.

### Strictly out of scope

- ES `TaskAggregate`.
- Generic task/checklist lifecycle.
- Status, progress or percent-complete truth.
- Start, due or end dates.
- Owner, assignee or responsible-person truth.
- FS/SS/FF/SF, lag, critical path or scheduling semantics.
- Start, block, review, complete or close actions.
- Approve, assign or escalate UI/commands.
- `ApprovedAt`, `ApprovedBy` or `ApproveStructureAsync`.
- Workflow runs, approval decisions, approver eligibility/delegation, SLA or escalation.
- WorkCenter provider/projection.
- Demand lifecycle or free-text Demand identity.
- BPM process models.
- Capacity/resource allocation.
- Existing legacy DWS data migration, deletion or deprecation.

### Canonical external ownership

| Concern | Canonical owner | DWS relationship |
|---|---|---|
| Generic task/checklist | `MOD-0024` | No lifecycle copy or recalculation |
| Workflow/approval | `MOD-0023` | No decision, eligibility or delegation |
| Effective permission | `MOD-0018` | Consume authoritative result |
| Immutable audit event | `MOD-0021` | Publish/consume typed contract; do not recreate |
| Evidence | `MOD-0031` | Typed evidence reference only |
| Reference data | `MOD-0048` | Typed governed lookup/reference only |
| Person/position/organization | `MOD-0288` | No local person/owner truth |
| Initiative/program/project/portfolio | `MOD-0117` | Typed external-context reference only |
| WorkCenter projection | DCP-004 / `CAND-CAP-0006` | No provider or projection in this pack |
| BPM process modeling | `MOD-0355` | Separate bounded internal module |

## 3. Owned Objects

### Domain objects

- `StructureDefinition`
- `StructureRevision` — business revision object; does not shadow technical `EntityBase.Version`
- `StructureNode`
- `StructuralDependency`
- `StructureBaseline`
- `StructureComparisonResult`
- `ExternalContextReference` value object
- `LogicalNodeId` structural identity; stable across revisions and distinct from revision-local persistence `Id`
- Technical `IdempotencyReceipt` persistence record; this is not a DWS business aggregate
- Producer-local technical audit intent/outbox persistence record; this is infrastructure for reliable
  MOD-0021 publication, not an audit SoR, aggregate, revision or business lifecycle state

### Planned application contract

- Commands:
  - `CreateStructureCommand`
  - `UpdateStructureMetadataCommand`
  - `AddStructureNodeCommand`
  - `MoveStructureNodeCommand`
  - `ReorderStructureNodeCommand`
  - `RemoveStructureNodeCommand`
  - `AddStructuralDependencyCommand`
  - `RemoveStructuralDependencyCommand`
  - `CreateStructureBaselineCommand`
  - `CreateNextStructureRevisionCommand`
- Queries:
  - `GetStructureByIdQuery`
  - `GetStructureTreeQuery`
  - `ValidateStructureQuery`
  - `CompareStructureRevisionsQuery`
  - `CompareStructureBaselinesQuery`
- DTOs expose structural identity, hierarchy, ordering, validation and comparison only.
- Planned API resource family: `/api/dws/structures`.
- Planned tenant route: `/management-governance/delivery-execution/structures`.
- Permission family: `management-governance.dws.*`.

No command, DTO or entity may contain task/approval fields or actions listed in §2.

## 4. Entity Fields

All operational entities are tenant-owned and conceptually inherit local `EntityBase`. Inherited `Id`,
`TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt` and technical concurrency `Version` are not
redeclared. DCP-005 OD-03 closes the greenfield local base/BSON decision; exact UTC field names and types are
fixed in §8, while real-Mongo round-trip, sort/index and cold-start checks remain implementation evidence.

### StructureDefinition

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `ExternalContextReference` | value object | Yes | Immutable after creation; exact versioned MOD-0117 contract |
| `CurrentWorkingRevisionNumber` | int? | No | Working revision number; null when no unsealed revision exists |
| `LatestRevisionNumber` | int | Yes | Starts at 1 and increases monotonically |

`StructureDefinition` does not store `Name` or `Description`; those fields have exactly one truth in
`StructureRevision.StructuralMetadata`. `ExternalContextReference` cannot be changed to another MOD-0117
object after creation. A different context requires a new `StructureDefinition`.

### ExternalContextReference

| Field | Type | Required | Rule |
|---|---|---:|---|
| `ContractName` | string | Yes | Governance-approved exact value `ppm.external-context-reference`; arbitrary discriminator forbidden |
| `ContractVersion` | string | Yes | Governance-approved exact value `1.0` |
| `ContextKind` | enum | Yes | Only `Portfolio`, `Initiative`, `Program`, `Project`; Demand is forbidden |
| `ContextId` | Guid | Yes | Canonical GUID syntax; opaque to DWS and validated by authoritative MOD-0117 behavior |

The string pair `ExternalContextType + ExternalContextId` is not a typed contract and is forbidden.
This transport-independent contract is an **APPROVED GOVERNANCE BASELINE — NOT A RUNTIME CONTRACT**.
Governance approval covers the exact shape and boundary only; no authoritative MOD-0117 provider, module
pack, service/runtime implementation, endpoint, route, port, timeout, retry or transport is approved.
DWS cannot derive domain, hierarchy, ownership or existence from `ContextId`. `TenantId` and
`ActorId` are not fields in the trusted client payload; they come only from authenticated server context.
Demand, task, workflow, approval and free-text discriminators are forbidden.

`CreateStructureCommand` must obtain authoritative MOD-0117 validation for the exact kind/ID under the
server-resolved tenant and actor before creating the definition. A soft-deleted MOD-0117 context cannot
receive a new reference. Fail-open behavior and local-cache ownership/existence inference are forbidden.
After creation the reference is immutable; a different context requires a new definition. Later deletion of
the referenced context cannot delete or rewrite previously sealed revision/baseline history.

Failures are `400` for invalid GUID/kind; `403` when an authenticated actor lacks the required DWS
command permission, decided by MOD-0018 enforcement; `404` when the MOD-0117 context is absent, soft-deleted,
cross-tenant or not visible/referenceable to the actor; `409` for reference replacement; and `503` when
authoritative validation is unavailable. The MOD-0117 validator does not evaluate DWS permission, and context
invisibility never returns `403` or discloses object existence. Endpoint, route, controller, service port,
transport technology and physical API shape remain implementation-pack decisions. DCP-006 OD-03 is closed;
OD-04 remains OPEN/PARTIAL.

### StructureRevision

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `StructureDefinitionId` | Guid | Yes | Same-tenant existing structure |
| `RevisionNumber` | int | Yes | Positive; unique per tenant + structure |
| `StructuralMetadata` | value object | Yes | Revisioned `Name` and `Description` truth only |
| `IsSealed` | bool | Yes | Structural freeze marker; false for working revision |
| `SealedAtUtc` | UTC instant? | Conditional | Set atomically when baseline seals revision; not a task date |

Revision lifecycle is precise:

1. `CreateStructureCommand` creates the definition and mutable Working Revision #1, sets
   `CurrentWorkingRevisionNumber = 1` and `LatestRevisionNumber = 1`.
2. Node, metadata and dependency mutations operate only on
   `CurrentWorkingRevisionNumber` while that revision is unsealed.
   `UpdateStructureMetadataCommand` changes only that revision's `StructuralMetadata`.
3. `CreateStructureBaselineCommand` runs all structural validations atomically. If all pass, it seals the
   current revision, creates the immutable baseline and sets `CurrentWorkingRevisionNumber = null` in one
   proven atomic persistence boundary.
4. A sealed revision is immutable. Any later mutation returns 409.
5. Further editing requires `CreateNextStructureRevisionCommand`. It runs only while
   `CurrentWorkingRevisionNumber` is null, copies a selected sealed revision/baseline into
   `LatestRevisionNumber + 1`, and sets both definition pointers to the new number.
6. Exactly one unsealed working revision may exist. A second-working-revision attempt returns 409.
   `RevisionNumber` increases monotonically and is unique within tenant + structure.
7. `CompareStructureRevisionsQuery` compares sealed revisions only. A working revision can be preview
   validated but is not historical comparison evidence.

Sealing is not approval and conveys no decision authority. `IsSealed` and `SealedAtUtc` are technical
structural-version metadata, not task status/date fields. No field named `Status` is permitted.

### StructureNode

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| inherited `EntityBase.Id` | Guid | Yes | Revision-local persistence identity; regenerated when a revision is copied and never exposed as structural identity |
| `StructureRevisionId` | Guid | Yes | Same-tenant revision |
| `LogicalNodeId` | Guid | Yes | Server-generated; stable for the definition lifetime and preserved across copied revisions |
| `ParentLogicalNodeId` | Guid? | No | Same revision/tenant logical node; not self |
| `Code` | string | Yes | Trim → NFC; 1–100; normalized exact-ordinal/case-sensitive unique within revision; not identity |
| `Title` | string | Yes | Trim → NFC; 1–300 |
| `Description` | string? | No | Trim → NFC; empty becomes null; max 4000 |
| `SiblingOrder` | int | Yes | Non-negative; unique under the same parent |

`EntityBase.Id` is a Mongo row identity only. `CreateNextStructureRevisionCommand` creates new row IDs while
preserving every copied node's `LogicalNodeId`. `Code` may change and cannot be used as identity. Parent,
dependency, snapshot, comparison and external structural DTO contracts use logical identity and never
persistence node IDs.

`NodeKindReference` is removed from Wave 1. It may be introduced only by a pack revision that names an exact
versioned MOD-0048 governed-reference contract; no free or unvalidated lookup string is allowed.

### StructuralDependency

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `StructureRevisionId` | Guid | Yes | Same-tenant revision |
| `FromLogicalNodeId` | Guid | Yes | Same revision/tenant |
| `ToLogicalNodeId` | Guid | Yes | Same revision/tenant; differs from `FromLogicalNodeId` |

There is deliberately no dependency-type, lag, calendar, critical-path or execution field.
Pure structural dependencies form a directed acyclic graph: self edges, duplicate edges and dependency
cycles are forbidden, and both endpoints must belong to the same tenant and revision. This DAG constraint
is structural integrity only and grants no scheduling or execution authority.

### StructureBaseline

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `StructureDefinitionId` | Guid | Yes | Same-tenant structure |
| `SourceRevisionNumber` | int | Yes | Existing revision |
| `BaselineNumber` | int | Yes | Positive; unique per tenant + structure |
| `HashAlgorithm` | string | Yes | Fixed to `SHA-256` for this canonicalization version |
| `CanonicalizationVersion` | string | Yes | Versioned immutable canonicalization contract identifier |
| `ContentHash` | string | Yes | Lowercase 64-character SHA-256 hexadecimal output |
| `Snapshot` | value object | Yes | Complete structural snapshot; immutable after create |

Baseline hashing uses a versioned canonicalization format. It includes the immutable definition
`ExternalContextReference`, normalized revision metadata, logical nodes and pure structural dependencies.
Nodes are ordered with null `ParentLogicalNodeId` first, then by `ParentLogicalNodeId` UTF-8 bytes,
`SiblingOrder` and `LogicalNodeId`; dependencies are ordered by `FromLogicalNodeId` and then
`ToLogicalNodeId`. Audit timestamps, technical concurrency `Version` and transient UI state are excluded.
The same snapshot must produce the same hash on every run. A future
canonicalization change creates a new `CanonicalizationVersion` and cannot invalidate or reinterpret hashes
of existing baselines.

The normative canonicalization identity is `dws.structural-baseline.v1`. `ContentHash` is versioned canonical
structural-content equality plus immutable-snapshot integrity; it is not a persistence-container identity
hash. The hash includes `CanonicalizationVersion`, `ExternalContextReference`, normalized revision `Name` and
nullable `Description`, each node's `LogicalNodeId` and structural fields, and pure structural dependencies.
It excludes `StructureDefinitionId`, `RevisionNumber`, persistence row/revision/baseline IDs, `TenantId`,
actor/security-subject identity, timestamps, audit, concurrency, idempotency, correlation, UI and every
task/status/progress/owner/assignment/date/approval/workflow/scheduling field. Consequently, an unchanged
copied next revision produces the same `ContentHash` even though revision numbers and row IDs differ.

### StructureComparisonResult

Comparison results may be calculated response values. If persisted later, a pack revision must define
retention and indexing. Comparison joins nodes by `LogicalNodeId`: left-only is removed, right-only is added,
a changed `ParentLogicalNodeId` is moved, the same parent with changed `SiblingOrder` is reordered, and
changed normalized `Code`/`Title`/`Description` is structural metadata change. Dependency differences are set
differences over logical endpoint pairs. Output is deterministically ordered by `LogicalNodeId`. Wave 1
contains no execution delta.

### Technical IdempotencyReceipt

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `TenantId` | inherited/server context | Yes | Never accepted from request body |
| `SecuritySubjectId` | Guid | Yes | Server-parsed authenticated JWT `sub`, falling back to `ClaimTypes.NameIdentifier`; never client input |
| `CommandFamily` | closed string | Yes | Exact Wave 1 command-family value from the closed set below |
| `IdempotencyKey` | string | Yes | Unique with tenant + command family |
| `RequestPayloadHash` | string | Yes | Deterministic hash of canonical request payload |
| `RequestCanonicalizationVersion` | string | Yes | Exact Wave 1 value `dws.request-canonical-json.v1` |
| `OutcomeSchemaVersion` | string | Yes | Exact Wave 1 value `dws.idempotency-outcome.v1` |
| `OutcomeKind` | closed lowercase string enum | Yes | Only `succeeded` or `no-op`; every other value fails closed |
| `DomainCode` | ASCII string | Yes | Stable non-localized command-family allowlist value; never message/exception text |
| `StableOutcomeJson` | string | Yes | UTF-8, BOM-less, minified canonical JSON text under `dws.idempotency-outcome.v1`; maximum 4096 UTF-8 bytes |
| `CreatedAtUtc` | scalar UTC DateTime | Yes | Server-generated technical receipt timestamp |
| inherited technical `Version` | int | Yes | Optimistic concurrency only |

The receipt and its associated mutation persist atomically. This technical record is not part of the DWS
business model and cannot be exposed as a structure-owned aggregate.

The unique scope is `TenantId + CommandFamily + IdempotencyKey`, named **tenant-scoped key with subject
binding**. `SecuritySubjectId` is not client input and does not widen the unique key. A different subject
reusing the key receives generic `409 idempotency_key_owned_by_different_subject`, sees no outcome and creates
no second mutation. Wave 1 has no `ExpiresAtUtc`, TTL or automatic expiry; future retention requires a
separate versioned pack decision.

`SecuritySubjectId` is resolved server-side from authenticated JWT `sub`, with
`ClaimTypes.NameIdentifier` as fallback, and parsed as Guid. Missing, empty, `Guid.Empty` or unparseable
subject identity is not an authenticated command: standard authentication failure occurs before command
handling or receipt lookup. This exact type matches the Guid-based current-user/tenant authorization
contexts, but does not close the `PARTIAL` MOD-0018 reusable enforcement-integration blocker.

`CommandFamily` is not arbitrary. Its closed Wave 1 set is `CreateStructure`,
`UpdateStructureMetadata`, `AddStructureNode`, `MoveStructureNode`, `ReorderStructureNode`,
`RemoveStructureNode`, `AddStructuralDependency`, `RemoveStructuralDependency`,
`CreateStructureBaseline` and `CreateNextStructureRevision`. Unknown values fail closed.

`OutcomeKind` persists and travels as a closed lowercase string enum with only `succeeded` and `no-op`.
Validation, conflict, authorization, visibility and retryable infrastructure failures do not create receipts
and are not outcome kinds. `StableOutcomeJson` contains UTF-8, BOM-less, minified canonical JSON text whose
properties use canonical UTF-8 property-name order and whose `result` contains only the command-specific
allowlist in this section. Its normative envelope is:

```json
{"domainCode":"...","outcomeKind":"succeeded|no-op","result":{}}
```

`DomainCode` is a stable non-localized ASCII value from a closed command-family allowlist; it contains no
user message or exception text. Canonical `StableOutcomeJson` exceeding 4096 UTF-8 bytes fails closed before
mutation begins. Full response/entity/tree snapshots, localized text, JWTs, claims, unrestricted
dictionaries and sensitive data are forbidden.

### String normalization contract

Strict JSON decode occurs before field processing. Invalid Unicode or an unpaired surrogate returns `400`.
Strings then follow Trim → NFC normalization → required/length/format validation → uniqueness validation →
persistence. Canonical projections are built from persisted normalized values. `Name`, `Title` and `Code`
must be non-empty after normalization. Empty normalized `Description` becomes null. Code uniqueness is
NFC-normalized exact ordinal and case-sensitive; `ABC` and `abc` differ and no case folding occurs. If two
property names become equal after NFC normalization, the request returns `400`; last-value-wins behavior is
forbidden.

### Request canonicalization contract

The normative identity is `dws.request-canonical-json.v1`:

- UTF-8 without BOM; no whitespace outside JSON tokens.
- Object properties sort by the UTF-8 bytes of their NFC-normalized names.
- Array order is semantic and preserved; null and absent are different.
- Guid uses lowercase `D`; enum wire values use lowercase kebab-case; booleans are `true`/`false`.
- Integers use base 10 without leading zero or `+`; negative zero canonicalizes to `0`.
- Strings use NFC plus exact JSON escaping. Decimal and DateTime are unsupported in Wave 1 projections and
  fail closed if encountered.
- Tenant, security subject/actor, claims, headers, trace/correlation and transport metadata are excluded.
- SHA-256 output is lowercase 64 hex. Unknown version fails closed before mutation or receipt creation.
- Arbitrary DTO serialization and generic `JsonSerializer` output are not normative.

### Command-specific idempotency projections

Every row uses `RequestCanonicalizationVersion = dws.request-canonical-json.v1` and
`OutcomeSchemaVersion = dws.idempotency-outcome.v1`.

| Command family | Exact request-hash projection | Exact stable-outcome allowlist |
|---|---|---|
| `CreateStructure` | `ExternalContextReference`, normalized `Name`, normalized nullable `Description` | `StructureDefinitionId`, `RevisionNumber=1`, `DefinitionVersion`, `RevisionVersion` |
| `UpdateStructureMetadata` | `StructureDefinitionId`, normalized `Name`, normalized nullable `Description`, `ExpectedRevisionVersion` | `StructureDefinitionId`, `RevisionNumber`, `RevisionVersion` |
| `AddStructureNode` | `StructureDefinitionId`, `ParentLogicalNodeId`, normalized `Code`/`Title`/`Description`, `SiblingOrder`, `ExpectedRevisionVersion` | `StructureDefinitionId`, `RevisionNumber`, `LogicalNodeId`, `RevisionVersion` |
| `MoveStructureNode` | `StructureDefinitionId`, `LogicalNodeId`, `NewParentLogicalNodeId`, `NewSiblingOrder`, `ExpectedRevisionVersion` | `StructureDefinitionId`, `RevisionNumber`, `LogicalNodeId`, `ParentLogicalNodeId`, `SiblingOrder`, `RevisionVersion` |
| `ReorderStructureNode` | `StructureDefinitionId`, `LogicalNodeId`, `SiblingOrder`, `ExpectedRevisionVersion` | `StructureDefinitionId`, `RevisionNumber`, `LogicalNodeId`, `SiblingOrder`, `RevisionVersion` |
| `RemoveStructureNode` | `StructureDefinitionId`, `LogicalNodeId`, `ExpectedRevisionVersion` | `StructureDefinitionId`, `RevisionNumber`, `LogicalNodeId`, `Removed=true`, `RevisionVersion` |
| `AddStructuralDependency` | `StructureDefinitionId`, `FromLogicalNodeId`, `ToLogicalNodeId`, `ExpectedRevisionVersion` | `StructureDefinitionId`, `RevisionNumber`, `FromLogicalNodeId`, `ToLogicalNodeId`, `RevisionVersion` |
| `RemoveStructuralDependency` | `StructureDefinitionId`, `FromLogicalNodeId`, `ToLogicalNodeId`, `ExpectedRevisionVersion` | `StructureDefinitionId`, `RevisionNumber`, `FromLogicalNodeId`, `ToLogicalNodeId`, `Removed=true`, `RevisionVersion` |
| `CreateStructureBaseline` | `StructureDefinitionId`, `ExpectedRevisionVersion` | `StructureDefinitionId`, `SourceRevisionNumber`, `BaselineNumber`, `ContentHash`, `CanonicalizationVersion`, `DefinitionVersion` |
| `CreateNextStructureRevision` | `StructureDefinitionId`, exactly one of `SourceRevisionNumber`/`SourceBaselineNumber`, `ExpectedDefinitionVersion` | `StructureDefinitionId`, `NewRevisionNumber`, `DefinitionVersion`, `RevisionVersion` |

Stable outcomes exclude HTTP response/tree/entity snapshots, localized text, JWT/claims, unrestricted
dictionaries and sensitive data. Replay order is: JWT authentication; server tenant/subject resolution;
MOD-0018 command permission; command normalization/validation; canonical request hash; receipt lookup;
subject match; canonicalization/outcome-version support; request-hash match; current tenant/target/context
visibility; stable-outcome deserialize and mapping to the current HTTP envelope. Revoked permission returns
`403`; lost visibility returns `404`; the original mutation handler never runs during replay.

### Normative canonicalization vectors

Each code span is the exact UTF-8 canonical byte sequence represented as text.

| Case | Exact canonical bytes | Expected SHA-256 |
|---|---|---|
| NFC/NFD equivalence | `{"name":"Café"}` | `659906f125d844f7081786e4a1cba739414e49a9b9061d80ce09c691b5f56602` |
| Property-order equivalence | `{"code":"A","title":"Root"}` | `de7a782be467c511dbef69310abe5208d9e2a480d76fddfad7f9b7fd267ad17b` |
| Explicit null | `{"description":null,"name":"Plan"}` | `f4e250f367f7856aa340449797df4d6cb663581de459e4c960d9c4e519eb961a` |
| Absent description | `{"name":"Plan"}` | `fe153fe9078b057a070a5eb6a44e1542167d9545d6e136b623332b6289461f10` |
| Array A | `{"logicalNodeIds":["aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"]}` | `f7c9211f3bd38bd572071d4ecef84012def38159e7c297a5cbeb7280b110632b` |
| Array B | `{"logicalNodeIds":["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"]}` | `e17fcc2f62e05a8a240c398f1c14b59b1752d1af2fc28f7db0ccd656a07099ef` |
| Escaping/control | `{"description":"Line 1\n\"quoted\"\\end"}` | `b78609a53164160720f2278fcc3d258a788baf525b308183aa2d5c5ebb598a0a` |

For the escaping vector, the canonical byte sequence contains backslash + `n`, not a literal LF. The exact
NFD test input representation is `{"name":"Cafe\u0301"}`; `\u0301` is the test-input escape for the
decomposed combining acute accent. It normalizes to canonical output `{"name":"Café"}`, whose bytes contain
the real NFC `é` UTF-8 sequence and whose expected hash is
`659906f125d844f7081786e4a1cba739414e49a9b9061d80ce09c691b5f56602`. Persistence stores NFC `Café`.
Property-order input `{"title":"Root","code":"A"}` produces the second row. Null/absent and Array A/B hashes
must differ.

The stable next-revision baseline canonical bytes are:

```json
{"canonicalizationVersion":"dws.structural-baseline.v1","dependencies":[{"fromLogicalNodeId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","toLogicalNodeId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"}],"externalContext":{"contextId":"00112233-4455-6677-8899-aabbccddeeff","contextKind":"program","contractName":"ppm.external-context-reference","contractVersion":"1.0"},"nodes":[{"code":"A","description":null,"logicalNodeId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","parentLogicalNodeId":null,"siblingOrder":0,"title":"Root"},{"code":"B","description":"Child","logicalNodeId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","parentLogicalNodeId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","siblingOrder":0,"title":"Node"}],"revisionMetadata":{"description":"v1","name":"Plan"}}
```

Expected SHA-256 is
`1fcad71b78003f89414d23b7c203d544bfe70362b6fcf89c3500eade5a5217a7`. Copied revisions with different
row IDs and revision numbers but identical logical identity/content produce this same hash.

Negative vectors: unknown `dws.request-canonical-json.v999` returns
`ERR_UNKNOWN_CANONICALIZATION_VERSION`, produces no hash/receipt and performs no mutation. If Alice owns
tenant `T1` / command `CreateStructure` / key `K1` / hash `H1`, Bob's same tenant/key/hash attempt returns
generic `409 idempotency_key_owned_by_different_subject`, discloses no outcome and creates no mutation.
Alice's replay after permission revocation returns `403`.

### Mongo index contract

All business-entity unique indexes are tenant-first and use an `IsDeleted = false` partial filter:

| Collection | Unique index |
|---|---|
| Structure revisions | `TenantId + StructureDefinitionId + RevisionNumber` |
| Structure nodes | `TenantId + StructureRevisionId + LogicalNodeId` |
| Structure nodes | `TenantId + StructureRevisionId + normalized Code` |
| Structure nodes | `TenantId + StructureRevisionId + ParentLogicalNodeId + SiblingOrder` |
| Structural dependencies | `TenantId + StructureRevisionId + FromLogicalNodeId + ToLogicalNodeId` |
| Structure baselines | `TenantId + StructureDefinitionId + BaselineNumber` |
| Idempotency receipts | `TenantId + CommandFamily + IdempotencyKey` |

Wave 1 receipt indexes have no TTL. Real Mongo integration tests must prove index creation, uniqueness,
tenant separation, logical-identity behavior and the business-entity partial-filter behavior.

## 5. Repo Scope

This `draft` authoring task creates only this pack.

Future implementation scope may be authorized only after §18 closes:

- `services/Diten.ManagementGovernanceService/**` — new service only after explicit scaffold approval.
- `frontend/Diten.Web/Controllers/DwsStructuresController.cs`.
- `frontend/Diten.Web/Views/ManagementGovernance/Dws/**`.
- `frontend/Diten.Web/wwwroot/assets/js/pages/management-governance/dws/**`.
- `frontend/Diten.Web/Resources/Views/ManagementGovernance/Dws/**`.
- Repo-standard service, architecture, integration and frontend tests.

Exact paths must be revalidated when service scaffold and route decisions are approved. This list grants no
present write authority.

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent only after separate approval
- `services/Diten.EnterpriseStrategyService/**`
- Existing ES/DWS legacy collections and migrations
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Views/_ViewStart.cshtml`
- Existing `/DecompositionTreeBuilder` prototype
- Existing Management Governance Approve/Bulk approve/assign/escalate controls
- `frontend/Diten.Web/Controllers/ManagementGovernanceController.cs`
- `frontend/Diten.Web/Controllers/DeliveryExecutionManagementController.cs`
- `frontend/Diten.Web/Controllers/EnterpriseStrategyBusinessPerformanceController.cs`
- Existing Management Governance, Delivery Execution, ESBP and Decomposition legacy/prototype views/routes
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- Other domains' `services/**`, `execution/domains/**` and module packs
- DCP-003, DCP-004, DCP-005, DCP-006, registry and master-plan files unless a separate governance task
  explicitly authorizes them

Any runtime change, including removal, migration or deprecation of a protected legacy task/approval hazard,
requires Control Tower Gate 2 before the change.

## 7. Dependencies

- [Management Governance domain config](../domain-config.md)
- [DCP-006](../../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md)
- [DCP-005](../../../portfolio/delivery-capability-packs/DCP-005-management-governance-core.md)
- DCP-006 AD-01 / OD-02 service-isolation decision.
- DCP-005 OD-03 local entity/BSON representation and BL-030 closure — `PASS` for this greenfield service.
- DCP-006 OD-03 is `CLOSED` as of 2026-07-29: the permanent PPM business-owner role, minimum Phase 2A/2B
  scope and future `Diten.PpmService` SoR placement are approved. This grants no runtime/scaffold authority.
- DCP-006 OD-04 remains globally open. DWS Wave 1 runtime blockers are only the exact MOD-0117 typed
  `ExternalContextReference` + authoritative validation, the `PARTIAL` MOD-0018 reusable in-process signed-JWT
  permission enforcement integration and the versioned MOD-0021 audit append/event contract.
- `MOD-0023` and `MOD-0024` are prohibited-ownership boundaries only. `MOD-0031` and `MOD-0288` are not
  consumed in Wave 1. `MOD-0048` is `N/A` because `NodeKindReference` is excluded.
- DCP-004 for any future WorkCenter projection.
- `MOD-0355` only as a prohibited-reference boundary; DWS does not use its domain types.

## 8. Runtime Constraints

- Tenant ID comes only from authenticated server-side context; requests cannot choose or override it.
- Every tenant-owned query and mutation applies `TenantId` and `IsDeleted = false` fail-closed.
- Cross-tenant parent, node, dependency, revision or baseline reads/references return 404 and create no
  record.
- Records whose tenant cannot be determined are quarantined; never assigned to a default tenant.
- The local base is `Guid Id`, required `Guid TenantId`, scalar BSON UTC `DateTime CreatedAtUtc`,
  nullable scalar BSON UTC `DateTime UpdatedAtUtc`, `bool IsDeleted`, nullable scalar BSON UTC
  `DateTime DeletedAtUtc` and technical `int Version`. Local/`Unspecified` timestamps fail closed unless a
  server-produced value is explicitly normalized to UTC. Platform.Common and existing ES bases are neither
  inherited nor copied. DCP-005 OD-03 is closed.
- No current DWS service/collection exists, so existing-data migration is `N/A`. Any future legacy
  ES/DWS-prototype containment requires a separate pack and Gate 2; it is not this pack's ready blocker.
- DCP-006 OD-04 remains globally open; this pack blocks only on its three actual Wave 1 consumer contracts
  named in §7.
- The `ppm.external-context-reference` version `1.0` shape in §4 is an approved governance baseline, not a
  runtime contract. Structure creation validates it authoritatively against MOD-0117 and fails closed; no
  local cache or ID inference can prove ownership or existence. The missing authoritative provider/module
  pack/service/runtime, S2S identity/actor delegation, physical transport, compatibility and runtime/security
  evidence remain blockers under OD-04.
- Multi-record atomicity uses Mongo replica-set transactions. Definition, revision, nodes, dependencies,
  baseline, pointer changes, idempotency receipt and any required technical audit-outbox row participate in
  the same transaction boundary for their command.
- A single-document tree aggregate is rejected because growing trees create an unacceptable Mongo 16 MB
  limit risk. Standalone Mongo fallback, snapshot/compensating rollback and partial commit are forbidden.
  Missing transaction support fails readiness closed and mutation endpoints return `503`.
- Optimistic concurrency uses inherited technical `Version`; stale commands return conflict and never
  silently overwrite.
- New nodes receive server-generated `LogicalNodeId`; copied next revisions regenerate persistence row IDs
  and preserve logical IDs. Parent/dependency/snapshot/comparison/structural DTO contracts never use row IDs.
- Authentication resolves `SecuritySubjectId` as non-empty Guid from JWT `sub`, falling back to
  `ClaimTypes.NameIdentifier`; missing, `Guid.Empty` or unparseable identity fails authentication before
  command or receipt lookup. MOD-0018 reusable enforcement remains `PARTIAL`.
- Every mutation accepts an idempotency key under the tenant-scoped key with subject-binding model in §4.
- Idempotency receipts have a unique tenant + command-family + key identity, server-bound security subject,
  canonical request hash and versioned allowlisted stable outcome. Mutation and receipt persist in the same
  Mongo transaction. Same-subject/same-payload replay follows the authorization/visibility order in §4;
  different payload or subject reuse returns generic 409 without outcome disclosure. Wave 1 receipts
  have no TTL or automatic expiry, and cleanup that weakens replay guarantees is forbidden. Future retention
  requires a separate versioned pack decision. A receipt is technical infrastructure, not a DWS aggregate.
- Request payload hashing follows exact command-specific `dws.request-canonical-json.v1` projections and
  `dws.idempotency-outcome.v1`; arbitrary DTO serialization is forbidden.
- `CommandFamily` accepts only the ten closed §4 values. Receipt outcomes accept only lowercase `succeeded`
  and `no-op`; validation/conflict/authorization/visibility/retryable failures write no receipt.
- `StableOutcomeJson` is BOM-less minified canonical JSON under `dws.idempotency-outcome.v1`, contains only
  stable ASCII `DomainCode`, closed `OutcomeKind` and command-specific allowlisted result fields, and cannot
  exceed 4096 canonical UTF-8 bytes. Size/shape/type failure occurs before mutation.
- Only the current unsealed working revision is mutable. Baseline creation validates, seals the revision and
  persists the immutable baseline atomically. Editing after sealing requires the next-revision command.
- A baseline is append-only and immutable after creation. Correction creates a new revision/baseline.
- Baseline hashes follow `dws.structural-baseline.v1`; unchanged copied structural content has the same hash
  despite new row IDs/revision number. Old hashes remain valid only under their recorded version and are
  never reinterpreted.
- Every required audited mutation and its producer-local technical audit intent/outbox persist in the same
  replica-set transaction. Intent persistence failure rolls back the mutation. After commit, a versioned
  semantic event is published asynchronously with durable at-least-once delivery to an idempotent MOD-0021
  consumer; exactly-once is not claimed.
- Broker, consumer or Platform failure after commit does not roll back the mutation or sealed baseline.
  Retry, dead-letter, alarm and authorized replay are mandatory. `AuditIntentPersisted` and
  `AuditEventAcceptedByMOD0021` are technical observability states only and cannot become aggregate/revision
  business status, task lifecycle, workflow or approval state.
- DWS cannot write directly to Platform `audit_outbox` or `audit_events`. The existing shared-key
  `/api/internal/audit/append` endpoint is not an authoritative Wave 1 contract.
- Publisher service identity, tenant and actor are bound fail-closed from authenticated server/transport
  context and matched to the allowed source/module. The client cannot select them.
- The audit payload is minimal and allowlist-based: `StructureDefinitionId`, `RevisionNumber` or
  `BaselineId`, canonical structural hash, operation/outcome, correlation/event/idempotency information,
  minimal allowlisted metadata and an opaque authoritative structure/baseline reference. Full tree/revision
  snapshots, unrestricted dictionaries, client-supplied tenant/actor/source, secrets, tokens and raw
  permission inventory are forbidden. Explicit byte, depth, collection-count and string-length limits plus
  redaction are required.
- Exact event name, major version and schema are not allocated by this governance decision. Unknown future
  major versions fail closed to dead-letter, and provider/consumer compatibility vectors are mandatory.
  The MOD-0021 blocker remains `PARTIAL` until runtime evidence is complete. No mutable aggregate-local audit
  list is permitted.
- Pure structural dependency alone is not Gate 2. Any touch to legacy task/approval fields, routes, UI,
  commands, migration, deletion or deprecation requires Gate 2 first.
- Candidate identity is prohibited from runtime literals.
- No regression debt moves to a later slice.

## 9. Layout & Shell Contract

- `shell: tenant`.
- Planned route: `/management-governance/delivery-execution/structures`.
- Every Razor page explicitly contains:

```cshtml
@{
    Layout = "_LayoutTenantShell";
}
```

- Global `_ViewStart` and FROZEN `_Layout.cshtml` are not modified.
- Existing `/DecompositionTreeBuilder` and Management Governance prototypes are code-reality reference
  only, not implementation templates or production baseline.
- New implementation uses `DwsStructuresController.cs` and cannot extend the existing Management Governance,
  Delivery Execution or ESBP controllers.
- Tree/hierarchy workspace is permitted; DataTable is not required.
- All user-visible strings use `.resx` resources with parity for `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`.
- Loading, empty, validation-error, unauthorized, not-found and concurrency-conflict states are localized.

## 10. Backend File Convention

Because this is not a DataTable CRUD module, command/query names follow the repo CQRS separation without
inventing Slim/Compact CRUD artifacts:

```text
services/Diten.ManagementGovernanceService/src/
└── Diten.ManagementGovernanceService.Application/Features/Dws/
    ├── Commands/                  # one sealed record per file
    ├── Queries/                   # one sealed record per file
    ├── Handlers/
    │   ├── CommandHandlers/       # {Verb}{Object}Handler; no Command suffix
    │   └── QueryHandlers/         # {Verb}{Object}Handler; no Query suffix
    ├── Validators/                # {Verb}{Object}Validator; no Command suffix
    └── DwsModels.cs               # DTO/value-response models
```

- Domain entities and value objects live under a dedicated `Domain/Dws/` boundary.
- Persistence repositories/collections live under dedicated `Persistence/Dws/` paths.
- Controllers are thin, attribute-routed and return `Response<T>` through the service's standard base
  controller.
- Handlers perform guards, construct/update structural state and persist; shared-contract adapters and
  external access stay behind interfaces.
- `Dws` cannot reference `ProcessModeling` domain types, repositories or collections.
- Task, approval or workflow helpers and `TaskAggregate` references are forbidden.

## 11. Frontend File Contract

This workspace does not use the Slim/Compact DataTable file contract. Planned surface:

```text
frontend/Diten.Web/
├── Views/ManagementGovernance/Dws/
│   ├── Index.cshtml
│   ├── _StructureTree.cshtml
│   ├── _StructureInspector.cshtml
│   ├── _StructuralValidation.cshtml
│   ├── _BaselineCompare.cshtml
│   └── DwsIndex.cs
├── Resources/Views/ManagementGovernance/Dws/
│   └── DwsIndex.{en|fr|es|zh|ar|ru|tr}.resx
└── wwwroot/assets/js/pages/management-governance/dws/
    ├── index.js
    └── index.l10n.js
```

- Partial views contain no scripts or styles.
- Browser calls Gateway `5000` through the approved tenant API profile; no direct service-port call.
- UI exposes structural create/edit, add/move/reorder/remove, validate, baseline and compare only.
- UI can preview-validate the working revision, seal it by deterministic baseline creation and create a next
  working revision; sealing is never labeled or presented as approval.
- No status/progress/dates/owner, scheduling, task action, approve/assign/escalate or WorkCenter control.

## 12. Validation Rules

| Field / command | Required | Validation |
|---|---:|---|
| Revision metadata `Name` | Yes | Strict decode; Trim → NFC; non-empty, 1–200 after normalization; stored only in `StructuralMetadata` |
| Revision metadata `Description` | No | Strict decode; Trim → NFC; empty becomes null; max 2000 after normalization |
| `ExternalContextReference.ContractName` | Yes | Exact governance-approved value `ppm.external-context-reference` |
| `ExternalContextReference.ContractVersion` | Yes | Exact governance-approved value `1.0` |
| `ExternalContextReference.ContextKind` | Yes | Portfolio/Initiative/Program/Project only; Demand rejected |
| `ExternalContextReference.ContextId` | Yes | Canonical Guid; opaque to DWS; authoritative MOD-0117 validation required |
| Node `LogicalNodeId` | Generated | Server Guid; stable across copied revisions; not accepted for new-node allocation |
| Node `Code` | Yes | Trim → NFC; 1–100; exact ordinal/case-sensitive unique within revision |
| Node `Title` | Yes | Trim → NFC; non-empty, 1–300 |
| Node `Description` | No | Trim → NFC; empty becomes null; max 4000 |
| `ParentLogicalNodeId` | No | Same tenant/revision, exists, not self, no resulting cycle |
| `SiblingOrder` | Yes | Non-negative and unique under parent |
| Dependency endpoints | Yes | Distinct, same tenant/revision, both exist, no duplicate pair or DAG cycle |
| Mutation revision | Yes | Must be current working revision and `IsSealed = false` |
| Definition external context | Create only | Immutable after structure creation; context change rejected with 409 |
| Next-revision source | Yes | Current pointer must be null; selected source sealed; number is latest + 1 |
| Baseline source | Yes | Current unsealed working revision; all structural validation must pass |
| Baseline hash | Generated | SHA-256, lowercase 64 hex, recorded canonicalization version |
| Expected technical version | Mutations | Must equal persisted `EntityBase.Version` |
| Idempotency key | Mutations | Non-empty; tenant/command scoped; payload fingerprint checked |
| Request payload hash | Generated | SHA-256 over exact `dws.request-canonical-json.v1` command projection |
| Unicode/property names | Yes | Invalid/unpaired surrogate or NFC-normalized duplicate property name returns 400 |

Architecture validation rejects entity, DTO or command properties named `Status`, `Progress`, `DueDate`,
`AssignedTo`, `Owner`, `ApprovedAt` or `ApprovedBy`; it also rejects task-like aliases with equivalent
meaning. It rejects commands/actions for start, block, review, complete, close, approve, assign or escalate.

## 13. Failure Path to Verify

| Failure | Expected result |
|---|---|
| Self-parent | 400 validation response; hierarchy unchanged |
| Hierarchy cycle | 409 structural conflict; no partial move |
| Dependency cycle | 409 structural conflict; no edge created |
| Missing same-tenant parent/object | 404; no orphan or mutation |
| Cross-tenant read/reference | 404; no existence disclosure or write |
| Authenticated actor lacks required DWS command permission | 403 from MOD-0018 enforcement; no read/write side effect |
| Invalid payload/context kind | 400; Demand/arbitrary kind rejected |
| Missing, empty or non-Guid authenticated subject claim | Standard authentication failure before command/receipt lookup |
| Invalid Unicode/unpaired surrogate or NFC property collision | 400 before persistence/hash/receipt; no last-value-wins |
| Duplicate sibling order | 409 conflict; existing order remains intact |
| Duplicate NFC-normalized Code in one revision | 409; exact ordinal/case-sensitive uniqueness preserved |
| Stale concurrency version | 409 conflict; client prompted to reload; no silent overwrite |
| Immutable baseline mutation | 409; baseline bytes/hash unchanged |
| Sealed revision mutation | 409; no node, metadata or dependency mutation |
| Second working revision | 409; definition pointers unchanged |
| External context replacement | 409; original reference unchanged |
| Compare working revision | 409 validation response; working revision is preview-only evidence |
| Unsupported MOD-0117 contract name/version or invalid GUID/kind | 400; no structure created |
| MOD-0117 context is absent, soft-deleted, cross-tenant or not visible/referenceable to the actor | 404; no existence disclosed and no structure created |
| Authoritative MOD-0117 validator temporarily unavailable | 503; no structure created |
| Canonical hash drift for identical snapshot | Test/build failure; no baseline accepted |
| Copied revision changes row IDs only | Same `ContentHash`; logical comparison reports no structural change |
| Duplicate idempotency key, same subject/payload | Permission and visibility are rechecked; stable outcome returned; exactly one mutation |
| Duplicate idempotency key, different payload | 409 conflict; no second mutation |
| Duplicate idempotency key owned by another subject | Generic 409 `idempotency_key_owned_by_different_subject`; no outcome disclosure or mutation |
| Replay after permission revocation | 403 before outcome replay; original mutation is not rerun |
| Replay after target/context visibility loss | 404 without outcome or existence disclosure; original mutation is not rerun |
| Unknown request canonicalization/outcome version | Fail closed; no guessed decode, receipt or mutation |
| Unknown `CommandFamily` or `OutcomeKind` | Fail closed; no receipt, outcome replay or mutation |
| Canonical `StableOutcomeJson` exceeds 4096 UTF-8 bytes or violates envelope/allowlist | Fail closed before mutation; no receipt |
| Validation/conflict/authorization/visibility/retryable infrastructure failure | No receipt; value cannot be represented as `OutcomeKind` |
| Required local audit-intent insert failure | Transaction rollback; no mutation, baseline seal or partial intent |
| Duplicate audit delivery | Idempotent MOD-0021 consumer accepts at most one effective append |
| Unknown MOD-0021 contract major version | Fail closed to dead-letter; no guessed deserialization |
| Broker, consumer or Platform unavailable after commit | Mutation remains committed; retry then dead-letter/alarm/authorized replay |
| Forged publisher service, tenant or actor | Fail closed; no audit acceptance and a security diagnostic is emitted |
| Audit payload exceeds allowlist or byte/depth/count/string limits | Fail closed before publication; no unbounded snapshot is emitted |
| Task-like field/command introduced | Architecture test fails build before runtime |
| Candidate ID introduced in runtime | Runtime-literal scan fails build |

## 14. Authorization Convention

- Policy: authenticated tenant actor; tenant context must resolve fail-closed.
- Permission prefix: `management-governance.dws`.
- Planned permissions:
  - `management-governance.dws.read`
  - `management-governance.dws.create`
  - `management-governance.dws.update`
  - `management-governance.dws.validate`
  - `management-governance.dws.baseline`
  - `management-governance.dws.compare`
- MOD-0018 is authoritative for effective permission evaluation.
- AuthService owns permission grants and signed-JWT issuance. DWS applies the signed JWT permission claim
  locally and fail closed through the PSS-approved reusable in-process handler/policy/evaluator.
- Platform/AuthService service-specific filter/evaluator code cannot be copied into DWS. Wave 1 has no
  synchronous AuthService or remote permission-decision call on the enforcement hot path.
- `IEntitlementChecker` is module/feature entitlement only and cannot enforce permissions. JWT freshness and
  revocation follow MOD-0018-FU13.
- No DWS-local role, grant, RBAC/ABAC or effective-permission calculation, approver eligibility or delegation.
- The MOD-0117 validator checks context referenceability and does not evaluate DWS permission.
- The reusable shared integration is not yet allocated or implemented; the MOD-0018 blocker remains `PARTIAL`,
  OD-04 remains open and DWS runtime cannot start.
- UI visibility is not authorization; API enforcement is mandatory.
- Requests cannot submit actor, tenant or permission results as trusted fields.
- Audit publication uses the same fail-closed trust boundary: publisher service identity, tenant and actor
  come from authenticated server/transport context and are matched to the allowed source/module. Forged or
  client-supplied service, tenant or actor values cannot be accepted.
- `RemoveStructureNodeCommand` uses `management-governance.dws.update`.
- Wave 1 exposes no structure/baseline delete command. Inherited soft-delete fields do not grant a public
  delete capability.

## 15. Gateway / API Routing Decision

Decision: a gateway integration decision is required, but this `draft` does not allocate a service port or
change a route.

- `Diten.ManagementGovernanceService` does not yet exist.
- Service scaffold requires this pack to be approved/ready-for-dev, explicit user approval, architecture-test
  design, port allocation and an integration-agent gateway decision.
- Browser traffic uses Gateway `5000`, never a direct service port.
- After port allocation, integration-agent must inspect existing routes and, if necessary, add explicit
  `/api/dws/structures` and `/api/dws/structures/{everything}` mappings including `OPTIONS`.
- `gateway/Diten.ApiGateway/**/ocelot.json` remains protected.

## 16. Acceptance Criteria

- [ ] A tenant actor with `management-governance.dws.create` can create one structure whose tenant is taken
  only from server context, creates mutable Working Revision #1 and stores a validated
  immutable `ExternalContextReference`; definition pointers are both 1.
- [ ] Structure `Name` and `Description` exist only in revision metadata; definition holds no duplicate
  truth.
- [ ] `UpdateStructureMetadataCommand` updates only the current unsealed revision metadata.
- [ ] External context accepts only the exact versioned MOD-0117 contract and
  Portfolio/Initiative/Program/Project; arbitrary discriminators and Demand are rejected.
- [ ] Node add/move/reorder/remove preserves a valid parent/child tree and applies mutation atomically.
- [ ] Each new node receives a server-generated `LogicalNodeId`; next-revision copy regenerates row `Id`,
  preserves logical identity and rewrites no parent/dependency/snapshot/comparison contract to persistence ID.
- [ ] Comparison joins by `LogicalNodeId` and deterministically reports added/removed/moved/reordered,
  structural metadata changes and logical dependency set differences.
- [ ] Validation identifies self-parent, cycle, missing parent and duplicate sibling order without inventing
  task/execution state.
- [ ] Pure structural dependency accepts only same-tenant/same-revision node pairs and contains no
  self/duplicate/cyclic edges, FS/SS/FF/SF, lag, critical-path or scheduling semantics. DAG validation
  grants no scheduling authority.
- [ ] `CreateStructureBaselineCommand` atomically validates all structural rules, seals the current revision
  creates the immutable baseline, and clears the working pointer; any mutation of the sealed revision
  returns 409.
- [ ] `CreateNextStructureRevisionCommand` copies a selected sealed revision/baseline into the next mutable
  revision only when no working revision exists, then atomically advances both definition pointers.
- [ ] Exactly one unsealed working revision exists at a time; a second attempt returns 409.
- [ ] External context is immutable; changing MOD-0117 context requires a new definition.
- [ ] Revision comparison accepts sealed revisions only; working revisions are preview-validation inputs,
  not historical evidence.
- [ ] `dws.structural-baseline.v1` produces deterministic SHA-256 lowercase 64 hex over canonical structural
  content; definition/revision/row IDs are excluded, unchanged copied content hashes identically, and a
  future version cannot invalidate or reinterpret existing hashes.
- [ ] Revision and baseline comparison reports only structural changes.
- [ ] Stale technical versions return 409 and cannot overwrite newer state.
- [ ] Idempotent replay produces exactly one mutation; payload or subject mismatch on the same tenant-scoped
  key returns generic 409 without outcome disclosure.
- [ ] Receipt stores server-bound Guid `SecuritySubjectId`, closed `CommandFamily`,
  tenant+command-family+key, request hash, lowercase `succeeded|no-op` `OutcomeKind`, stable ASCII
  `DomainCode` and canonical `StableOutcomeJson` atomically with the mutation; it has no `ExpiresAtUtc`/TTL
  and is not a DWS business aggregate.
- [ ] `StableOutcomeJson` matches the `dws.idempotency-outcome.v1` envelope, uses only command allowlist
  result fields, is BOM-less/minified/canonical and at most 4096 UTF-8 bytes; failures write no receipt.
- [ ] Missing, empty, `Guid.Empty` or unparseable JWT `sub`/fallback NameIdentifier fails authentication
  before command execution or receipt lookup.
- [ ] Request hashing uses exact command-specific `dws.request-canonical-json.v1` projections, excludes
  server context and rejects arbitrary DTO serialization, unsupported types and unknown versions.
- [ ] Strict decode, Trim → NFC, normalized validation/uniqueness/persistence and normalized-property
  collision rejection are identical across validation, indexes, storage and canonical projection.
- [ ] The approved Mongo atomicity design leaves no partial state after persistence failure or process crash.
- [ ] Required tenant-first partial unique indexes exist and enforce uniqueness in real Mongo.
- [ ] Cross-tenant structure, parent, node, dependency, revision and baseline access returns 404 without
  disclosing existence.
- [ ] DWS code has no `TaskAggregate`, task/approval/workflow helper, task-like field/action or BPM domain
  type/repository/collection reference.
- [ ] DWS and `ProcessModeling` use separate repositories, Mongo collections and domain models.
- [ ] Runtime literal scan finds no deprecated alias `CAND-CAP-0008`.
- [ ] Permissions use only `management-governance.dws.*` and consume MOD-0018 evaluation.
- [ ] Every required audited mutation persists a producer-local technical audit intent in the same
  transaction; failure rolls back the mutation, while post-commit delivery failure uses
  retry/dead-letter/alarm/authorized replay without reverting the baseline.
- [ ] Versioned asynchronous MOD-0021 delivery is duplicate-safe, uses server-bound publisher/tenant/actor
  identity and emits only the approved minimal bounded payload.
- [ ] DWS neither accesses Platform audit collections nor uses the shared-key internal audit endpoint as its
  authoritative integration.
- [ ] `AuditIntentPersisted` and `AuditEventAcceptedByMOD0021` remain technical observability only and do not
  create DWS aggregate/revision business status, task lifecycle, workflow or approval state.
- [ ] Every Razor page explicitly uses `Layout = "_LayoutTenantShell"`; FROZEN `_Layout.cshtml` and
  `_ViewStart.cshtml` remain unchanged.
- [ ] Tenant UI contains no approve/assign/escalate or task lifecycle controls and has seven-language RESX
  parity.
- [ ] Browser requests travel through Gateway `5000`; no direct service-port URL exists.
- [ ] Existing `/DecompositionTreeBuilder`, Management Governance prototype and legacy DWS persistence are
  unchanged.

## 17. Test Expectations

### Architecture tests

- `Dws` cannot reference `ProcessModeling` domain types.
- DWS and BPM cannot share repositories or Mongo collections.
- No task/approval/workflow helper or `TaskAggregate` reference exists.
- DWS entity/DTO/command property scan rejects `Status`, `Progress`, `DueDate`, `AssignedTo`, `Owner`,
  `ApprovedAt`, `ApprovedBy` and equivalent task/approval aliases.
- Command/route/UI scan rejects start/block/review/complete/close and approve/assign/escalate behavior.
- Dependency model scan rejects FS/SS/FF/SF, lag and critical-path semantics.
- Deprecated-alias runtime-literal scan rejects `CAND-CAP-0008`.
- Permission scan accepts only `management-governance.dws.*`.
- Project-reference tests prove `Dws` has no `ProcessModeling` dependency.
- Architecture test proves DWS has no direct access to Platform `audit_outbox`, `audit_events`, repositories
  or collection-name literals and does not use the shared-key internal audit endpoint.

### Unit and integration tests

- Hierarchy self-parent, cycle, orphan and duplicate-order validators.
- Stable logical-node identity across next-revision copy; new row IDs cannot change parent/dependency links,
  baseline hash or revision comparison.
- Node move/reorder atomicity and deterministic logical-identity comparison classification.
- Pure structural dependency duplicate/cycle policy without scheduling semantics.
- Dependency DAG self-edge, duplicate-edge and cycle rejection.
- Working Revision #1 creation, sealed-revision mutation rejection and monotonic next-revision creation.
- Definition/revision single-SoR tests: metadata exists only in revision; external context is immutable;
  working/latest pointers follow create, seal and next-revision transitions.
- Baseline persistence failure cannot leave the revision sealed; seal failure cannot create a baseline.
- Receipt persistence failure cannot commit its business mutation.
- Next-revision copy failure cannot change definition pointers.
- Process crash/partial failure recovery finds no half-written state.
- Replica-set readiness tests prove transactions are supported; standalone Mongo fails readiness and
  mutation endpoints return `503`.
- Real Mongo crash/rollback tests prove that no command leaves partially committed definition, revision,
  node, dependency, baseline, pointer, receipt or required technical audit-outbox state.
- Required audit-intent insert failure rolls back the complete audited mutation and baseline seal.
- Versioned provider/consumer compatibility vectors cover supported major/minor evolution and unknown-major
  fail-closed dead-letter behavior.
- Idempotent MOD-0021 consumer tests cover duplicate delivery, concurrent duplicate handling, crash after
  append/before acknowledgement and replay.
- Publisher identity, tenant and actor binding negative tests reject forged service/module, cross-tenant and
  forged-actor messages.
- Audit payload tests enforce allowlist, redaction, byte/depth/collection-count/string-length limits and
  prohibit full tree/revision snapshots, secrets, tokens and raw permission inventories.
- Operational tests prove retry, dead-letter alarm, authorized replay and distinct observation of
  `AuditIntentPersisted` versus `AuditEventAcceptedByMOD0021`.
- Exact `dws.structural-baseline.v1` SHA-256 vector, stable logical ordering, unchanged-next-revision equality,
  deterministic rerun, hash format and unknown/older-version fail-closed compatibility.
- Sealed-only revision comparison and working-revision preview validation.
- Exact MOD-0117 `ExternalContextReference` contract/kind/canonical-Guid validation; the Guid remains opaque
  to DWS, and Demand/arbitrary kinds are rejected.
- Optimistic concurrency and idempotency replay/conflict.
- Atomic subject-bound idempotency receipt persistence, permission/visibility-before-replay order,
  actor/key collision rejection, stable-outcome replay without mutation re-execution, absence of Wave 1
  `ExpiresAtUtc`/TTL and rejection of cleanup that weakens replay guarantees.
- JWT `sub` then `ClaimTypes.NameIdentifier` Guid resolution tests cover missing, empty, `Guid.Empty` and
  malformed subject fail-before-command/receipt behavior.
- Closed `CommandFamily` and lowercase `succeeded|no-op` `OutcomeKind` tests reject every unknown value and
  prove validation/conflict/authorization/visibility/retryable failures persist no receipt.
- `StableOutcomeJson` tests prove exact canonical envelope/property order, command-specific result allowlists,
  stable ASCII `DomainCode`, BOM/minification rules, the 4096-byte boundary and pre-mutation oversize failure.
- Exact request canonicalization vectors prove NFC/NFD and property-order equivalence, null/absent and array
  order inequality, control-character escaping, server-context exclusion and unknown-version fail closed.
- NFD vector uses exact escaped input `{"name":"Cafe\u0301"}`, persists NFC `Café` and produces the verified
  NFC canonical bytes/hash.
- Normalization persistence/index tests prove NFD/NFC duplicate Code collision, exact ordinal case-sensitive
  Code behavior, invalid Unicode rejection and normalized property-name collision rejection.
- Authenticated/unauthorized and cross-tenant read/write failure paths.
- Real Mongo persistence, all seven tenant-first unique indexes, `IsDeleted = false` partial filters, soft
  delete and cold-restart reload.
- MOD-0021 audit contract emission without local audit truth.

### Frontend and delivery tests

- Seven-language RESX key parity.
- Tenant-shell layout assertion for every Razor page.
- Route smoke for `/management-governance/delivery-execution/structures`.
- Loading, empty, validation-error, 403, 404 and 409 UI states.
- Negative DOM/JS scan for task/approval fields and approve/assign/escalate controls.
- Browser network assertion: Gateway `5000` only.
- Relevant service, frontend and gateway builds pass after implementation.
- No DataTable verifier applies because `golden_reference: none`; tree workspace contract tests replace it.

## 18. Ready-for-dev Checklist

- [x] Canonical preflight passes for `MOD-0354 — Decomposition & Work Structuring Engine` on 2026-07-28.
- [x] All required frontmatter fields and 20 sections are present.
- [x] `golden_reference: none` / `form_field_count: 0` justified as a non-DataTable tree workspace.
- [x] Layout and seven-language tenant-shell contract are explicit.
- [x] Ownership, architecture tests, failure paths, authorization and gateway decisions are specified.
- [x] Revision sealing and next-revision design review passed on 2026-07-28.
- [x] Negative architecture-test design review passed on 2026-07-28; executable tests remain implementation
  evidence, not a design blocker.
- [x] DCP-005 OD-03/BL-030 greenfield base and scalar UTC BSON decision closed on 2026-07-28.
- [x] Mongo replica-set transaction strategy selected; single-document tree and standalone fallback rejected.
- [x] Wave 1 idempotency receipt retention decision approved: no TTL/automatic expiry.
- [x] Stable `LogicalNodeId`, logical parent/dependency endpoints, next-revision preservation and deterministic
  comparison design approved on 2026-07-28.
- [x] `dws.request-canonical-json.v1`, command-specific projections and verified canonical hash vectors
  approved on 2026-07-28.
- [x] `dws.idempotency-outcome.v1` minimal outcome, tenant-scoped key with subject binding and secure replay
  order approved on 2026-07-28; runtime evidence remains unchecked.
- [x] Idempotency outcome field types closed on 2026-07-29: Guid JWT subject, closed CommandFamily,
  lowercase `succeeded|no-op` outcome and maximum-4096-byte canonical `StableOutcomeJson`; runtime evidence
  remains unchecked and MOD-0018 remains `PARTIAL`.
- [x] `dws.structural-baseline.v1` content-equality/integrity semantics, logical ordering and backward
  compatibility approved on 2026-07-28; runtime evidence remains unchecked.
- [x] `ppm.external-context-reference` v1 transport-independent shape and failure semantics are an
  APPROVED GOVERNANCE BASELINE — NOT A RUNTIME CONTRACT; DCP-006 OD-03 is closed and OD-04 remains
  OPEN/PARTIAL.
- [x] PSS owner/Enterprise Architect approved the MOD-0021 producer-local transactional audit-intent,
  asynchronous at-least-once/idempotent-consumer and bounded-payload governance design; this is not runtime
  evidence or production authority.
- [x] Human approved promotion to `ready-for-dev` and current-main authority reconciliation on 2026-09-01;
  this governance-only checkpoint grants none of the excluded runtime/service delivery authorities.
- [ ] DWS Wave 1's DCP-006 OD-04 subset closes with exact MOD-0117, MOD-0018 and MOD-0021 contracts.
- [x] MOD-0117 `ppm.external-context-reference` v1 shape received Control Tower governance approval on
  2026-07-29.
- [ ] MOD-0117 authoritative provider/module pack/service/runtime, S2S identity/actor delegation,
  compatibility and runtime/security evidence are approved and proven.
- [ ] Replica-set environment/readiness and real Mongo crash/rollback tests prove the selected transaction
  strategy; no standalone fallback or partial commit exists.
- [ ] Exact versioned MOD-0021 semantic provider/consumer contract is approved and implemented.
- [ ] Transactional producer-local audit-outbox and real Mongo crash/rollback evidence are proven.
- [ ] Idempotent consumer duplicate-delivery and consumer-crash evidence are proven.
- [ ] Unknown-major-version fail-closed/dead-letter evidence is proven.
- [ ] Publisher service identity, tenant and actor binding negative tests pass.
- [ ] Payload allowlist, redaction and byte/depth/count/string-limit evidence passes.
- [ ] Dead-letter alarm, authorized replay and acceptance-observability evidence passes.
- [ ] Architecture test proves no direct Platform collection access or authoritative use of the shared-key
  internal audit endpoint.
- [ ] `Diten.ManagementGovernanceService` scaffold receives explicit user approval.
- [ ] Service port is allocated without collision.
- [ ] Integration-agent approves and owns the gateway route task.
- [ ] Exact future repo scope is revalidated after service placement/port decisions.
- [ ] Gate 2 PASS exists before any implementation touches, removes, migrates or deprecates a legacy
  task/approval hazard; pure structural-only work does not itself trigger Gate 2.

The pack is `ready-for-dev` only under its explicitly bounded authority record. Every unchecked item remains
a fail-closed gate for its corresponding later slice; production implementation and activation remain
forbidden because `production_authority: none`.

## 19. Implementation Notes

- DCP-006 is the active 1.3/1.4/1.6 orchestration contract; DCP-005 remains historical/foundation governance
  and Gate provenance.
- DCP-006 OD-02 is closed with provisional modular-monolith placement and a fail-closed split fallback.
- This pack narrows DWS Wave 1 below the legacy ES/DWS prototype: existing task, status, date, owner,
  scheduling and approval behavior is quarantine evidence only.
- Existing prototypes may inform pure tree/hierarchy interaction research but are not production templates.
- No DataTable Golden Reference applies. CQRS naming, tenant shell, response-envelope and architecture
  standards still apply.
- The `CAND-CAP-0008 → MOD-0354` identity transition is governance-only because the candidate was forbidden
  from runtime literals; any contrary runtime evidence must fail closed.
- The implementation target date is provisional and must be replanned when blockers close.

### Human review questions

1. Does the reviewer confirm that structural identity is `LogicalNodeId`, while `EntityBase.Id` remains
   revision-local persistence identity only?
2. Does the reviewer confirm that unchanged copied content produces the same
   `dws.structural-baseline.v1` hash because definition/revision/row IDs are excluded?
3. Does the reviewer confirm Trim → NFC persistence, exact-ordinal/case-sensitive Code uniqueness and
   invalid/ambiguous Unicode fail-closed behavior?
4. Does the reviewer confirm the tenant-scoped idempotency key with server-bound subject and the
   permission/visibility-before-replay sequence?
5. Does the reviewer confirm Guid JWT subject resolution, the closed ten-value `CommandFamily`, the
   `succeeded|no-op`-only outcome model and the 4096-byte canonical `StableOutcomeJson` envelope?
6. Does the reviewer confirm that runtime evidence and external MOD-0117/MOD-0018/MOD-0021 contracts remain
   blockers even though these internal governance decisions are now approved?

### Module-completion human review gate

After the entire module implementation is complete and all tests pass, the implementing agent stops and
reports exactly:

> MOD-0354 — Decomposition & Work Structuring Engine tamamlandı.<br>
> İnceleyebilirsin:<br>
> http://localhost:5001/management-governance/delivery-execution/structures

The agent does not stop page-by-page. BPM or any next module cannot start until the user responds
`onay` / `devam` after this whole-module review.

## 20. Follow-up Items

- Advanced cross-structure dependency, if it can remain purely structural under a separately approved scope.
- Baseline retention/archival policy and verified restore design.
- Structure/baseline deletion and retention policy; Wave 1 has no public delete command or permission.
- `NodeKindReference` may be added only by a separate pack revision with an exact versioned MOD-0048
  governed-reference contract.
- Canonical identity provenance and deprecated `CAND-CAP-0008` alias remain governed by DCP-002.
- Separate `MOD-0355` BPM process-modeling module pack.
- Any task-generation/link consumer contract through MOD-0024, without DWS lifecycle ownership.
- Any authoritative approval-outcome consumption through MOD-0023, without local approval state.
- Any WorkCenter projection through DCP-004 and its separate approval gates.
- Legacy DWS containment/migration/deletion/deprecation only through a separate approved migration scope and
  Gate 2.
