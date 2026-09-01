---
id: MOD-0355
name: Business Process Architecture & Modeling
domain: management-governance
dcp: DCP-006
service: Diten.ManagementGovernanceService
internal_module: Modules/ProcessModeling
module_code: MOD-0355
shell: tenant
golden_reference: none
entity_base: EntityBase
status: ready-for-dev
owner: ali.tufanoglu / enterprise-architect-interim
business_owner: ali.tufanoglu / business-process-governance-owner-interim
technical_owner: ali.tufanoglu / management-governance-technical-owner-interim
legacy_parity_owner: ali.tufanoglu / business-process-governance-owner-interim
branch: feature/mg/mod-0355-business-process-architecture-modeling
started: 2026-08-25
target: 2026-09-15
form_field_count: 0
port: TBD
implementation_authority: explicit-user-control-tower-bounded-core-model-contract-test
production_authority: none
---

# MOD-0355 — Business Process Architecture & Modeling

> **Canonical materialization provenance:** Canonical Master 8.1 promotion checkpoint
> `ee1d7dc5766a67012df2af35d3eed1aa779da27d` was merged into the materialization base by merge commit
> `e034c5fddd0d05e0dda6ef72ce88f41a36efa3d4`. This materialized file is ready-for-dev governance authority
> only for the bounded Core Model & Contract-Test slice and grants no runtime or production authority.

> **Ready-for-dev bounded implementation guard:** `status: ready-for-dev`,
> `implementation_authority: explicit-user-control-tower-bounded-core-model-contract-test` and
> `production_authority: none` are binding. The explicit user decision dated 2026-08-25 authorizes only the
> exact Core scaffold, module and test paths in §5. It does not authorize a listener, endpoint, route, port
> binding, Gateway, frontend, WorkCenter, credential, migration, deployment, external adapter, production
> activation or modification of another internal module.

> **Planning boundary:** `target` is provisional planning metadata, not a delivery commitment or authority.

## 1. Module Summary

MOD-0355 owns the definition-time business process architecture and modeling boundary for the
`management-governance` domain.

Its exact provisional identity is:

- ID: `MOD-0355`
- Name: `Business Process Architecture & Modeling`
- Runtime ModuleCode: `MOD-0355`
- DCP: `DCP-006`
- Service: `Diten.ManagementGovernanceService`
- Internal module: `Modules/ProcessModeling`
- Shell: `tenant`
- Port: `TBD`

Minimal Core v1 is a backend-only **Core Model & Contract-Test** slice. It models tenant-owned process
architectures, domains, families, definitions, durable model identities, immutable published revisions,
generic definition-time activity nodes, definition-time control points and directional descriptive topology.

Minimal Core v1 does not execute a process and does not define classification vocabularies for activity,
control-point or relationship records.

This is not a conventional DataTable CRUD module. `golden_reference: none` and `form_field_count: 0` are
intentional. Frontend delivery is a separately authorized later slice.

Historical `CAND-CAP-0009` is a deprecated governance alias only. It must never appear in runtime literals,
permissions, routes, collections, events, jobs, configuration or persisted module identity.

Delivery is divided into five independently gated slices:

1. Core Model & Contract-Test
2. Approval/Auth/Audit
3. Optional reference integrations
4. Frontend & Legacy Parity
5. Runtime activation

Governance availability alone is not runtime or implementation availability. Present implementation authority
exists only for the bounded Core Model & Contract-Test paths and guards recorded in frontmatter and §5;
runtime, deployment and production authority remain absent.

## 2. Ownership and Boundaries

### In scope — Minimal Core v1

- Process architecture, domain and family catalogs.
- Process definition identity and descriptive metadata.
- Durable process-model identity and revision history.
- Process model version lifecycle:
  `Draft → Review → Published → Retired`.
- Generic definition-time activity nodes.
- Definition-time control points.
- Directional descriptive activity-to-activity topology.
- Exactly one open `Draft` or `Review` version for the same process model.
- Strictly monotonic revision allocation.
- Published and Retired version immutability.
- Tenant isolation.
- Optimistic concurrency through inherited `EntityBase.Version`.
- Command idempotency.
- Producer-local technical audit intent and outbox persistence.
- Canonical content hashing.
- Contract tests for ownership, lifecycle, immutability, tenancy, concurrency, atomicity and serialization.

### Deferred versioned classification amendment

`ActivityKind`, `ControlKind` and `RelationshipKind` are excluded from Minimal Core v1. They may be
introduced only through a future versioned module-pack amendment with exact case-sensitive values,
definition-time meanings, migration/compatibility rules and new normative hash vectors.

Their absence does not block Minimal Core v1 Core Model & Contract-Test authoring or `ready-for-dev`
promotion.

### Strictly out of Minimal Core v1

- Activity, control-point or relationship classification.
- Classification integration.
- KPI or Metric integration.
- Binary artifact storage.
- Approval-policy evaluation.
- ApprovalOutcome runtime binding.
- Approval/Auth/Audit runtime integration.
- Runtime acceptance of `ProcessOwnerReference`.
- Runtime acceptance of `ProcessInterfaceReference`.
- Frontend implementation.
- Gateway routes.
- WorkCenter provider or projection.
- Process instance or runtime execution engine.
- Workflow runs, approval tasks, route execution, SLA or escalation.
- Operational task/checklist creation or lifecycle.
- Token creation, movement or consumption.
- Executable expression, script or condition evaluation.
- Process mining, conformance checking or process intelligence.
- Legacy data migration, deletion, replacement or deprecation.
- Modification or removal of legacy files, controllers, views, routes or controls.

### Canonical external ownership

| Concern | Canonical owner | MOD-0355 relationship |
|---|---|---|
| Workflow run and authoritative approval outcome | `MOD-0023` | Second-slice typed submission/reference consumption only |
| Effective authorization and approver eligibility/delegation | `MOD-0018` | Second-slice authoritative result consumption only |
| Immutable audit event | `MOD-0021` | Second-slice delivery adapter; no competing audit SoR |
| Generic task/checklist | `MOD-0024` | No task lifecycle ownership |
| SOP/work instruction | `MOD-0029` | Optional later typed reference |
| Evidence and binary artifacts | `MOD-0031` and approved storage owner | Optional later typed reference; no binary storage |
| Canonical classification/reference data | `MOD-0048` or allocated canonical owner | Optional later typed reference |
| KPI/Metric definition | `MOD-0059` / `MOD-0060` | Optional later certified typed binding |
| Dashboard/scorecard projection | `MOD-0061` | No dashboard truth |
| Person/position/organization | `MOD-0288` | Optional typed owner reference |
| Portfolio/project context | `MOD-0117` | Optional typed reference |
| DWS structural model | `MOD-0354` | Separate internal module; no shared domain types |
| WorkCenter aggregation | DCP-004 and its canonical runtime owner | No provider, projection or lifecycle |
| Process execution engine | Unallocated future authority | Explicitly not owned by this pack |

## 3. Owned Objects

### Core domain objects

- `ProcessArchitecture`
- `ProcessDomain`
- `ProcessFamily`
- `ProcessDefinition`
- `ProcessModel`
- `ProcessModelVersion`
- `ProcessActivity`
- `ProcessControlPoint`
- `ProcessRelationship`

### Governance-only later-slice reference shapes

These shapes may be documented and contract-tested, but are not accepted by Core create/update contracts
without an approved producer contract and adapter:

- `ProcessInterfaceReference`
- `ProcessOwnerReference`
- `ApprovalOutcomeReferenceV1`

### Technical persistence objects

- `ProcessModelingIdempotencyReceipt`
- `ProcessModelingAuditIntent`
- `ProcessModelingOutboxMessage`

Technical records are infrastructure. They are not process aggregates, business lifecycle records or
alternative MOD-0021 audit truth.

### Planned Core commands

- `CreateProcessArchitectureCommand`
- `UpdateProcessArchitectureCommand`
- `ArchiveProcessArchitectureCommand`
- `CreateProcessDomainCommand`
- `UpdateProcessDomainCommand`
- `ArchiveProcessDomainCommand`
- `CreateProcessFamilyCommand`
- `UpdateProcessFamilyCommand`
- `ArchiveProcessFamilyCommand`
- `CreateProcessDefinitionCommand`
- `UpdateProcessDefinitionCommand`
- `ArchiveProcessDefinitionCommand`
- `CreateProcessModelCommand`
- `UpdateProcessModelCommand`
- `UpdateDraftProcessModelVersionCommand`
- `RequestProcessModelReviewCommand`
- `ReturnProcessModelToDraftCommand`
- `PublishProcessModelVersionCommand`
- `RetireProcessModelVersionCommand`
- `CreateProcessModelRevisionCommand`

### Planned Core queries

- `GetProcessArchitectureByIdQuery`
- `GetProcessArchitectureTreeQuery`
- `GetProcessDefinitionByIdQuery`
- `GetProcessModelByIdQuery`
- `GetProcessModelVersionByIdQuery`
- `GetProcessModelHistoryQuery`
- `GetProcessModelGraphQuery`
- `ValidateProcessModelVersionQuery`

### Model-version lifecycle contract

The valid primary lifecycle is:

```text
Draft → Review → Published → Retired
```

Additional rules:

- `Review → Draft` is the only permitted backward transition.
- `Published` and `Retired` versions are immutable.
- `Retired` is terminal.
- A Published version cannot be edited in place.
- A change after publication requires `CreateProcessModelRevisionCommand`.
- A new revision receives the next strictly monotonic revision number.
- Exactly one version for the same `ProcessModel` may be open in `Draft` or `Review`.
- Parallel variants are not supported in Minimal Core v1.
- Returning `Review → Draft` does not allocate a new revision.
- `Published → Draft`, `Retired → Published` and `Retired → Draft` are forbidden.
- Approval policy and ApprovalOutcome binding belong to the Approval/Auth/Audit second slice.
- Minimal Core v1 records no local approval truth.

### Exact permission inventory

1. `management-governance.process-modeling.architectures.read`
2. `management-governance.process-modeling.architectures.create`
3. `management-governance.process-modeling.architectures.update`
4. `management-governance.process-modeling.architectures.archive`
5. `management-governance.process-modeling.definitions.read`
6. `management-governance.process-modeling.definitions.create`
7. `management-governance.process-modeling.definitions.update`
8. `management-governance.process-modeling.definitions.archive`
9. `management-governance.process-modeling.models.read`
10. `management-governance.process-modeling.models.create`
11. `management-governance.process-modeling.models.update`
12. `management-governance.process-modeling.models.request-review`
13. `management-governance.process-modeling.models.return-to-draft`
14. `management-governance.process-modeling.models.publish`
15. `management-governance.process-modeling.models.retire`
16. `management-governance.process-modeling.models.create-revision`

No additional permission may be inferred from role names or introduced without a versioned pack amendment
and owner approval.

## 4. Entity Fields

All business entities are tenant-owned and conceptually inherit the Management Governance local
`EntityBase`:

- `Guid Id`
- required server-resolved `Guid TenantId`
- scalar BSON UTC `DateTime CreatedAtUtc`
- nullable scalar BSON UTC `DateTime UpdatedAtUtc`
- `bool IsDeleted`
- nullable scalar BSON UTC `DateTime DeletedAtUtc`
- technical optimistic-concurrency `int Version`

Technical concurrency uses inherited `EntityBase.Version` only. No entity redeclares another concurrency
field.

`IsDeleted` is reserved for a future, separately authorized exceptional migration/purge mechanism. Ordinary
archive commands do not write `IsDeleted` or `DeletedAtUtc`. Ordinary delete and hard-delete endpoints are
absent.

### Mutable catalog lifecycle

Only the following entities are mutable catalogs:

- `ProcessArchitecture`
- `ProcessDomain`
- `ProcessFamily`
- `ProcessDefinition`

Their exact lifecycle is:

```text
Active → Archived
```

Create produces `Active`. `Archived` is terminal in Minimal Core v1. Archived catalogs reject ordinary
update and new-child/reference attachment. Reactivation and cascade archive are absent. Archive is not
delete.

### ProcessArchitecture

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `ArchitectureCode` | string | Yes | Tenant-unique, immutable, normalized uppercase dash token |
| `Name` | string | Yes | NFC, trimmed, 1–200 |
| `Description` | string? | No | NFC, trimmed, maximum 2000 |
| `LifecycleState` | enum | Yes | Exactly `Active` or `Archived` |
| `SortOrder` | int | Yes | Non-negative |

Unique index: `TenantId + ArchitectureCode`, filtered by technical non-purged state.

### ProcessDomain

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `ProcessArchitectureId` | Guid | Yes | Same-tenant Active architecture |
| `DomainCode` | string | Yes | Immutable normalized uppercase dash token |
| `Name` | string | Yes | NFC, trimmed, 1–200 |
| `Description` | string? | No | Maximum 2000 |
| `LifecycleState` | enum | Yes | Exactly `Active` or `Archived` |
| `SortOrder` | int | Yes | Non-negative |

Unique index: `TenantId + ProcessArchitectureId + DomainCode`, filtered by technical non-purged state.

### ProcessFamily

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `ProcessDomainId` | Guid | Yes | Same-tenant Active domain |
| `FamilyCode` | string | Yes | Immutable normalized uppercase dash token |
| `Name` | string | Yes | NFC, trimmed, 1–200 |
| `Description` | string? | No | Maximum 2000 |
| `LifecycleState` | enum | Yes | Exactly `Active` or `Archived` |
| `SortOrder` | int | Yes | Non-negative |

Unique index: `TenantId + ProcessDomainId + FamilyCode`, filtered by technical non-purged state.

### ProcessDefinition

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `ProcessFamilyId` | Guid | Yes | Same-tenant Active family |
| `ProcessCode` | string | Yes | Tenant-unique, immutable normalized uppercase dash token |
| `Name` | string | Yes | NFC, trimmed, 1–200 |
| `Purpose` | string? | No | Maximum 2000 |
| `Description` | string? | No | Maximum 4000 |
| `LifecycleState` | enum | Yes | Exactly `Active` or `Archived` |

Core schema contains no owner-reference field. Owner-reference acceptance is an Optional reference
integration extension.

Unique index: `TenantId + ProcessCode`, filtered by technical non-purged state.

### ProcessModel

`ProcessModel` is a durable, non-archivable aggregate identity.

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `ProcessDefinitionId` | Guid | Yes | Same-tenant Active definition |
| `ModelCode` | string | Yes | Immutable normalized uppercase dash token |
| `Name` | string | Yes | NFC, trimmed, 1–200 |
| `Description` | string? | No | Maximum 4000 |
| `LatestRevisionNumber` | int | Yes | Server-managed, monotonic, minimum 1 |
| `PublishedVersionId` | Guid? | No | Server-managed pointer |
| `OpenVersionId` | Guid? | No | At most one Draft/Review version |

Unique index: `TenantId + ProcessDefinitionId + ModelCode`, filtered by technical non-purged state.

ProcessModel has no ordinary archive, delete, hard-delete, TTL or purge behavior. Model-version lifecycle
termination uses `RetireProcessModelVersionCommand`.

### ProcessModelVersion

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `ProcessModelId` | Guid | Yes | Same-tenant durable model |
| `RevisionNumber` | int | Yes | Server-allocated, strictly monotonic |
| `LifecycleState` | enum | Yes | Exactly `Draft`, `Review`, `Published`, `Retired` |
| `Title` | string | Yes | NFC, trimmed, 1–200 |
| `Description` | string? | No | Maximum 4000 |
| `ValidFromUtc` | DateTime? | No | Server-produced UTC when published |
| `PublishedAtUtc` | DateTime? | No | Server-produced UTC |
| `RetiredAtUtc` | DateTime? | No | Server-produced UTC |
| `ContentHash` | string | Yes | Exact §4 canonical SHA-256 representation |

Core schema contains no approval-outcome field. ApprovalOutcome binding is an Approval/Auth/Audit
later-slice extension.

Unique index: `TenantId + ProcessModelId + RevisionNumber`.

Partial unique index: one row per `TenantId + ProcessModelId` whose lifecycle is `Draft` or `Review`.

### ProcessActivity

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `ProcessModelVersionId` | Guid | Yes | Same-tenant version |
| `LogicalActivityId` | Guid | Yes | Stable logical identity across revisions |
| `ActivityCode` | string | Yes | Version-unique normalized token |
| `Name` | string | Yes | NFC, trimmed, 1–200 |
| `Description` | string? | No | Maximum 4000 |
| `SortOrder` | int | Yes | Non-negative |

`ProcessActivity` is a generic definition-time node. It is not a runtime task, job, process instance,
workflow step or execution record.

Core schema contains no owner-reference field.

Unique index: `TenantId + ProcessModelVersionId + ActivityCode`.

Unique index: `TenantId + ProcessModelVersionId + LogicalActivityId`.

### ProcessControlPoint

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `ProcessModelVersionId` | Guid | Yes | Same-tenant version |
| `LogicalControlPointId` | Guid | Yes | Stable logical identity across revisions |
| `ControlCode` | string | Yes | Version-unique normalized token |
| `Name` | string | Yes | NFC, trimmed, 1–200 |
| `Description` | string? | No | Maximum 4000 |
| `LogicalActivityId` | Guid? | No | Same-version logical activity reference |
| `SortOrder` | int | Yes | Non-negative |

The entity type identifies the record as a control point. A control point is descriptive definition-time
metadata only. It does not enforce a control, invoke a policy, execute code or produce runtime state.

Unique index: `TenantId + ProcessModelVersionId + ControlCode`.

Unique index: `TenantId + ProcessModelVersionId + LogicalControlPointId`.

### ProcessRelationship

| Field | Type | Required | Rule / index |
|---|---|---:|---|
| `ProcessModelVersionId` | Guid | Yes | Same-tenant version |
| `FromActivityId` | Guid | Yes | Same-version logical activity identity |
| `ToActivityId` | Guid | Yes | Same-version logical activity identity, different from source |
| `ConditionLabel` | string? | No | NFC, trimmed, maximum 500; descriptive only |
| `SortOrder` | int | Yes | Non-negative |

`ProcessRelationship` is a directional descriptive topology edge. `ConditionLabel` is presentation metadata;
it is not an executable expression, script, predicate, policy or route selector.

Self-reference and duplicate exact edge are rejected. Duplicate exact edge identity is:

```text
ProcessModelVersionId + FromActivityId + ToActivityId + normalized ConditionLabel + SortOrder
```

The relationship does not create or move a token, start work, schedule a job, select a route or invoke an
external side effect.

### ProcessInterfaceReference — governance shape only

| Field | Type | Required | Rule |
|---|---|---:|---|
| `ContractName` | string | Yes | Exact approved producer contract |
| `ContractVersion` | string | Yes | Exact supported version |
| `ReferenceId` | Guid | Yes | Opaque canonical non-empty UUID |
| `Direction` | string | Yes | Exact values require a future approved producer amendment |

Core create/update commands do not accept this shape. Until an approved producer contract, compatibility
fixture and runtime adapter exist, supplying it fails closed according to §13. Documentation does not claim
runtime availability.

### ProcessOwnerReference — governance shape only

| Field | Type | Required | Rule |
|---|---|---:|---|
| `ContractName` | string | Yes | Exact approved producer contract |
| `ContractVersion` | string | Yes | Exact supported version |
| `OwnerId` | Guid | Yes | Opaque canonical non-empty UUID |

Core create/update commands do not accept this shape. Until an approved producer contract, compatibility
fixture and runtime adapter exist, supplying it fails closed according to §13. Names, titles, organization
payloads and authorization results are never copied as truth.

### ApprovalOutcomeReferenceV1 — second-slice governance reference only

The exact case-sensitive allowlist is:

| Field | Exact rule |
|---|---|
| `ContractName` | Constant `platform.approval-outcome-reference` |
| `ContractVersion` | Constant `1.0` |
| `ApprovalOutcomeId` | Opaque canonical non-empty UUID |

The contract contains exactly these three fields and no others. `ApprovalOutcomeVersion`, outcome sequence,
decision payload, actor, time, reason, comment, task, route, assignee and workflow payload are forbidden.

This shape belongs only to the Approval/Auth/Audit slice. It is not a Core entity field, Core command input,
Core runtime dependency or Core runtime-availability claim.

### Exact ContentHash canonicalization contract

The exact contract is:

- Contract name: `management-governance.process-modeling.content-hash`
- Contract version: `1.0`
- Digest: SHA-256
- Stored form: `sha256:<64 lowercase hexadecimal characters>`

A dedicated canonical writer is mandatory. DTO serialization, BSON serialization, default JSON serializer
output, dictionary order and persistence row order are forbidden canonical inputs.

Exact root field order:

1. `contractName`
2. `contractVersion`
3. `title`
4. `description`
5. `activities`
6. `controlPoints`
7. `relationships`

Exact activity field order:

1. `logicalActivityId`
2. `activityCode`
3. `name`
4. `description`
5. `sortOrder`

Exact control-point field order:

1. `logicalControlPointId`
2. `controlCode`
3. `name`
4. `description`
5. `logicalActivityId`
6. `sortOrder`

Exact relationship field order:

1. `fromLogicalActivityId`
2. `toLogicalActivityId`
3. `conditionLabel`
4. `sortOrder`

Excluded from the canonical document:

- tenant identity;
- persistence IDs other than stable logical child identities and relationship endpoints;
- `ProcessModelId`;
- `RevisionNumber`;
- lifecycle state;
- timestamps;
- inherited `EntityBase.Version`;
- `ContentHash`;
- approval, owner and interface references;
- receipts, audit intents and outbox records.

Normalization:

- UUIDs use lowercase RFC 4122 `8-4-4-4-12` text without braces.
- Nil UUID is rejected.
- UUID comparison uses canonical ASCII text, never platform-specific GUID byte order.
- Strings use Unicode NFC and are emitted exactly after validation.
- Hash-time trimming, case-folding and locale transforms are forbidden.
- JSON escapes quotation mark, backslash and U+0000–U+001F controls.
- Non-ASCII text is emitted directly as UTF-8.
- Unpaired surrogates return `400`.
- Nullable properties are always emitted as literal `null` and never omitted.
- Arrays are always present and never `null`.
- Numbers are non-negative integers in shortest base-10 ASCII form.
- `+`, unnecessary leading zero, exponent, decimal, NaN, Infinity and negative zero are forbidden.
- No insignificant whitespace is emitted.

Deterministic ordering:

- activities:
  `(sortOrder asc, activityCode NFC UTF-8 unsigned-byte lexicographic asc, logicalActivityId canonical ASCII asc)`
- control points:
  `(sortOrder asc, controlCode NFC UTF-8 unsigned-byte lexicographic asc, logicalControlPointId canonical ASCII asc)`
- relationships:
  `(sortOrder asc, fromLogicalActivityId canonical ASCII asc, toLogicalActivityId canonical ASCII asc, conditionLabel null-first then NFC UTF-8 unsigned-byte lexicographic asc)`

Duplicate complete sort keys are rejected. Canonical ordering never depends on caller, DTO, dictionary, BSON
or database return order.

Encoding/framing:

- BOM-less UTF-8;
- one compact JSON object;
- no CR or LF;
- no trailing newline;
- hash begins at opening `{` and ends at closing `}`.

### Normative hash vector 1 — empty graph and null

Canonical JSON:

```json
{"contractName":"management-governance.process-modeling.content-hash","contractVersion":"1.0","title":"Order Fulfilment","description":null,"activities":[],"controlPoints":[],"relationships":[]}
```

UTF-8 byte length:

```text
194
```

Exact UTF-8 hex:

```text
7b22636f6e74726163744e616d65223a226d616e6167656d656e742d676f7665726e616e63652e70726f636573732d6d6f64656c696e672e636f6e74656e742d68617368222c22636f6e747261637456657273696f6e223a22312e30222c227469746c65223a224f726465722046756c66696c6d656e74222c226465736372697074696f6e223a6e756c6c2c2261637469766974696573223a5b5d2c22636f6e74726f6c506f696e7473223a5b5d2c2272656c6174696f6e7368697073223a5b5d7d
```

SHA-256:

```text
ab545fe0678b7ccc124136814af229d01f5d42b5180f8a903dc1276bb707eadd
```

Stored form:

```text
sha256:ab545fe0678b7ccc124136814af229d01f5d42b5180f8a903dc1276bb707eadd
```

### Normative hash vector 2 — NFC/non-ASCII

All accented characters are NFC.

Canonical JSON:

```json
{"contractName":"management-governance.process-modeling.content-hash","contractVersion":"1.0","title":"İade Süreci","description":"Café — müşteri","activities":[],"controlPoints":[],"relationships":[]}
```

UTF-8 byte length:

```text
208
```

Exact UTF-8 hex:

```text
7b22636f6e74726163744e616d65223a226d616e6167656d656e742d676f7665726e616e63652e70726f636573732d6d6f64656c696e672e636f6e74656e742d68617368222c22636f6e747261637456657273696f6e223a22312e30222c227469746c65223a22c4b06164652053c3bc72656369222c226465736372697074696f6e223a22436166c3a920e28094206dc3bcc59f74657269222c2261637469766974696573223a5b5d2c22636f6e74726f6c506f696e7473223a5b5d2c2272656c6174696f6e7368697073223a5b5d7d
```

SHA-256:

```text
931d38322970398e266ba8f9aab2f5fb1a90effbf56552c0858452014614215f
```

Stored form:

```text
sha256:931d38322970398e266ba8f9aab2f5fb1a90effbf56552c0858452014614215f
```

### Normative hash vector 3 — non-empty deterministic graph

The source collections are intentionally supplied in non-canonical order:

- Activities input order: `RELEASE`, `CAPTURE`, `REVIEW`
- Control-point input order: `CP-RELEASE`, `CP-REVIEW`
- Relationship input order: sort order `20`, then sort order `10`

The canonical writer must produce `CAPTURE`, `REVIEW`, `RELEASE`; then `CP-REVIEW`, `CP-RELEASE`; then
relationships with sort orders `10`, `20`.

Canonical JSON:

```json
{"contractName":"management-governance.process-modeling.content-hash","contractVersion":"1.0","title":"Order Fulfilment","description":null,"activities":[{"logicalActivityId":"11111111-1111-4111-8111-111111111111","activityCode":"CAPTURE","name":"Capture Order","description":null,"sortOrder":10},{"logicalActivityId":"22222222-2222-4222-8222-222222222222","activityCode":"REVIEW","name":"Review Order","description":"Manual business review","sortOrder":20},{"logicalActivityId":"33333333-3333-4333-8333-333333333333","activityCode":"RELEASE","name":"Release Order","description":null,"sortOrder":30}],"controlPoints":[{"logicalControlPointId":"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa","controlCode":"CP-REVIEW","name":"Review Check","description":null,"logicalActivityId":"22222222-2222-4222-8222-222222222222","sortOrder":10},{"logicalControlPointId":"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb","controlCode":"CP-RELEASE","name":"Release Check","description":"Definition-time checkpoint","logicalActivityId":"33333333-3333-4333-8333-333333333333","sortOrder":20}],"relationships":[{"fromLogicalActivityId":"11111111-1111-4111-8111-111111111111","toLogicalActivityId":"22222222-2222-4222-8222-222222222222","conditionLabel":null,"sortOrder":10},{"fromLogicalActivityId":"22222222-2222-4222-8222-222222222222","toLogicalActivityId":"33333333-3333-4333-8333-333333333333","conditionLabel":"Standard continuation","sortOrder":20}]}
```

UTF-8 byte length:

```text
1421
```

Exact UTF-8 hex:

```text
7b22636f6e74726163744e616d65223a226d616e6167656d656e742d676f7665726e616e63652e70726f636573732d6d6f64656c696e672e636f6e74656e742d68617368222c22636f6e747261637456657273696f6e223a22312e30222c227469746c65223a224f726465722046756c66696c6d656e74222c226465736372697074696f6e223a6e756c6c2c2261637469766974696573223a5b7b226c6f676963616c41637469766974794964223a2231313131313131312d313131312d343131312d383131312d313131313131313131313131222c226163746976697479436f6465223a2243415054555245222c226e616d65223a2243617074757265204f72646572222c226465736372697074696f6e223a6e756c6c2c22736f72744f72646572223a31307d2c7b226c6f676963616c41637469766974794964223a2232323232323232322d323232322d343232322d383232322d323232323232323232323232222c226163746976697479436f6465223a22524556494557222c226e616d65223a22526576696577204f72646572222c226465736372697074696f6e223a224d616e75616c20627573696e65737320726576696577222c22736f72744f72646572223a32307d2c7b226c6f676963616c41637469766974794964223a2233333333333333332d333333332d343333332d383333332d333333333333333333333333222c226163746976697479436f6465223a2252454c45415345222c226e616d65223a2252656c65617365204f72646572222c226465736372697074696f6e223a6e756c6c2c22736f72744f72646572223a33307d5d2c22636f6e74726f6c506f696e7473223a5b7b226c6f676963616c436f6e74726f6c506f696e744964223a2261616161616161612d616161612d346161612d386161612d616161616161616161616161222c22636f6e74726f6c436f6465223a2243502d524556494557222c226e616d65223a2252657669657720436865636b222c226465736372697074696f6e223a6e756c6c2c226c6f676963616c41637469766974794964223a2232323232323232322d323232322d343232322d383232322d323232323232323232323232222c22736f72744f72646572223a31307d2c7b226c6f676963616c436f6e74726f6c506f696e744964223a2262626262626262622d626262622d346262622d386262622d626262626262626262626262222c22636f6e74726f6c436f6465223a2243502d52454c45415345222c226e616d65223a2252656c6561736520436865636b222c226465736372697074696f6e223a22446566696e6974696f6e2d74696d6520636865636b706f696e74222c226c6f676963616c41637469766974794964223a2233333333333333332d333333332d343333332d383333332d333333333333333333333333222c22736f72744f72646572223a32307d5d2c2272656c6174696f6e7368697073223a5b7b2266726f6d4c6f676963616c41637469766974794964223a2231313131313131312d313131312d343131312d383131312d313131313131313131313131222c22746f4c6f676963616c41637469766974794964223a2232323232323232322d323232322d343232322d383232322d323232323232323232323232222c22636f6e646974696f6e4c6162656c223a6e756c6c2c22736f72744f72646572223a31307d2c7b2266726f6d4c6f676963616c41637469766974794964223a2232323232323232322d323232322d343232322d383232322d323232323232323232323232222c22746f4c6f676963616c41637469766974794964223a2233333333333333332d333333332d343333332d383333332d333333333333333333333333222c22636f6e646974696f6e4c6162656c223a225374616e6461726420636f6e74696e756174696f6e222c22736f72744f72646572223a32307d5d7d
```

SHA-256:

```text
221c8a60cb22d36b3c73ae740c9911bad5241e3f39e2fa4a1e4048c80717fd95
```

Stored form:

```text
sha256:221c8a60cb22d36b3c73ae740c9911bad5241e3f39e2fa4a1e4048c80717fd95
```

All three vectors were independently recomputed from their displayed exact UTF-8 byte sequences.

### Technical records and Core atomicity

`ProcessModelingIdempotencyReceipt` contains tenant, command family, idempotency key, authenticated subject
binding, canonical request hash, stable outcome and required timestamps.

`ProcessModelingAuditIntent` and `ProcessModelingOutboxMessage` contain minimal allowlisted technical
provenance and opaque aggregate/version references. They cannot contain full model graphs, secrets, tokens,
raw permission inventories or external producer payloads.

Every successful Core mutation atomically persists exactly four participant classes in the same local Mongo
transaction:

1. business mutation;
2. idempotency receipt;
3. producer-local audit intent;
4. producer-local outbox.

Failure to persist any participant rolls back all four. MOD-0021 delivery, signed event schema, publisher
credentials and delivery adapter remain in the Approval/Auth/Audit second slice.

## 5. Repo Scope

Governance materialization scope was limited to:

- `execution/domains/management-governance/module-packs/MOD-0355-business-process-architecture-modeling.md`

The explicit 2026-08-25 authorization permits the bounded Core Model & Contract-Test slice to create or
update only:

- `services/Diten.ManagementGovernanceService/Diten.ManagementGovernanceService.sln`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Api/Diten.ManagementGovernanceService.Api.csproj`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/Diten.ManagementGovernanceService.Application.csproj`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Domain/Diten.ManagementGovernanceService.Domain.csproj`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Persistence/Diten.ManagementGovernanceService.Persistence.csproj`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Infrastructure/Diten.ManagementGovernanceService.Infrastructure.csproj`
- compile/test-only `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Api/Program.cs`
- service-local composition files:
  `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/DependencyInjection.cs`,
  `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Persistence/DependencyInjection.cs`
  and
  `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Infrastructure/DependencyInjection.cs`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Domain/Modules/ProcessModeling/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/Modules/ProcessModeling/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Persistence/Modules/ProcessModeling/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Infrastructure/Modules/ProcessModeling/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.Tests/Diten.ManagementGovernanceService.Tests.csproj`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.IntegrationTests/Diten.ManagementGovernanceService.IntegrationTests.csproj`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.ArchitectureTests/Diten.ManagementGovernanceService.ArchitectureTests.csproj`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.Tests/Modules/ProcessModeling/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.IntegrationTests/Modules/ProcessModeling/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.ArchitectureTests/Modules/ProcessModeling/**`

The solution, five source projects, compile/test-only `Program.cs` and three root composition files are shared
service-shell artifacts. MOD-0355 may create an absent artifact or make an additive-only ProcessModeling
composition change. It may not overwrite, replace, remove or weaken an existing artifact, registration,
reference or content owned by MOD-0007 `DecisionRegistry`, MOD-0354 `Dws` or another authorized sibling.
Existing shared-shell content is preserved byte-for-byte except for the minimal additive ProcessModeling
project/reference/registration hunk. Any incompatible shell state or ambiguous ownership blocks the Core
scaffold fail-closed and requires a separate topology amendment.

The compile/test-only `Program.cs` cannot call `Run`, `RunAsync`, `Start`, `StartAsync` or equivalent host
activation; it cannot bind a port, expose an endpoint, start a listener/worker or load runtime configuration.
No `appsettings*`, `launchSettings.json`, controller, endpoint, worker, broker or deployment file is authorized.

Later slices require separate explicit scope activation:

- Approval/Auth/Audit adapters.
- Optional reference adapters.
- Frontend ProcessModeling surface.
- Gateway routing.
- Runtime deployment and configuration.

The exact Core paths above grant present bounded write authority only; every later-slice path remains
unauthorized.

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `frontend/**` during the Core Model & Contract-Test slice
- WorkCenter files, providers, projections, routes, views and controllers
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Views/_ViewStart.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- Existing Management Governance, Delivery Execution, ESBP, BPM or process-modeling prototype files
- Existing legacy controllers, views, routes, collections and migrations
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.PpmService/**`
- `services/Diten.Platform/**`
- `services/Diten.AuthService/**`
- MOD-0354 domain types, repositories and collections
- Other domains’ `services/**`, `execution/domains/**` and module packs
- DCP-003, DCP-004, DCP-005, DCP-006, registry and master-plan files unless separately authorized
- Master 8.1 workbook and promotion evidence

This pack grants no authority to modify, migrate, delete, replace, archive or deprecate any legacy file or
runtime record.

## 7. Dependencies

### Governance-authoring dependencies

- Canonical Master 8.1 promotion merged into the materialization target.
- `MOD-0355 — Business Process Architecture & Modeling` canonical registry identity.
- Management Governance domain config.
- DCP-006 active orchestration boundary.
- DCP-002 canonical ID/name/collision preflight.
- Accountable-owner confirmations listed in §18.

### Core Model & Contract-Test dependencies

- Management Governance local tenant entity contract.
- MongoDB replica-set transaction capability for executable mutations.
- Enforceable isolation between `Modules/Dws` and `Modules/ProcessModeling`.
- Closed lifecycle, concurrency, canonical-hash and serialization fixtures.

Minimal Core v1 does not depend on runtime availability of MOD-0018, MOD-0021, MOD-0023, MOD-0029,
MOD-0031, MOD-0048, MOD-0059, MOD-0060, frontend or Gateway integration.

### Approval/Auth/Audit dependencies

- MOD-0018 authoritative permission enforcement and, when required, eligibility/delegation decisions.
- MOD-0021 signed/versioned audit event contract and runtime delivery adapter.
- Promoted executable MOD-0023 ApprovalOutcome producer contract.
- Approval-policy owner decision.
- Bilateral exact serialization, tenancy, failure-mode and compatibility fixtures.

### Optional reference dependencies

- Classification contract from its canonical owner.
- Certified KPI/Metric contracts from MOD-0059/MOD-0060.
- Controlled document/evidence/artifact reference contracts from their canonical owners.
- Typed organization/person/position reference contract from MOD-0288.
- Typed portfolio/project or DWS references only when separately approved.

Missing Approval/Auth/Audit and optional producer integrations do not block Minimal Core v1 Core Model &
Contract-Test `ready-for-dev`.

## 8. Runtime Constraints

- Runtime `ModuleCode` is exactly `MOD-0355`.
- Service is `Diten.ManagementGovernanceService`.
- Internal module is exactly `Modules/ProcessModeling`.
- Service port is `TBD`; this draft allocates no port.
- No frontend or Gateway route is allocated.
- Tenant identity comes only from authenticated server context.
- Client-supplied tenant, actor, permission or approval authority is rejected.
- Every query and mutation applies the server-resolved tenant boundary fail-closed.
- Missing, deleted, cross-tenant or non-disclosable records return indistinguishable `404`.
- Cross-tenant references create no record and disclose no existence.
- MongoDB is the persistence technology.
- Core multi-record mutations require replica-set transactions.
- Standalone-Mongo partial-commit fallback is forbidden.
- Optimistic concurrency uses inherited `EntityBase.Version`.
- Stale writes return `409`.
- Revision allocation and open-version uniqueness are transactionally protected.
- Published and Retired versions are immutable.
- Retired is terminal.
- A change to published content creates a new monotonic revision.
- Exactly one Draft/Review version may be open for a ProcessModel.
- Parallel variants are forbidden in Minimal Core v1.
- Every successful Core mutation atomically persists the four participants defined in §4.
- Local audit intent/outbox persistence is Core infrastructure.
- MOD-0021 delivery adapter, signed/event contract and publisher credentials remain second-slice work.
- No synchronous remote authorization, approval or audit call is implied by this draft.
- No adapter may treat a governance-only contract as runtime available.
- Candidate aliases cannot appear in runtime literals.
- ProcessModeling cannot reference MOD-0354 domain types, repositories or collections.
- ProcessModeling cannot create workflow runs, approval tasks, operational tasks, jobs, process instances,
  tokens, SLAs or escalations.
- Definition-time relationships and condition labels cannot execute or select routes.
- ProcessModeling cannot store classification, KPI/Metric or artifact payload truth owned elsewhere.
- Ordinary archive commands write `LifecycleState = Archived`; they do not write `IsDeleted`.
- Ordinary delete and hard-delete endpoints are forbidden.
- Runtime activation is a separate explicit gate.

## 9. Layout & Shell Contract

- `shell: tenant`.
- Minimal Core v1 has no frontend.
- A future frontend must use `_LayoutTenantShell` explicitly on every Razor page.
- `_Layout.cshtml` and `_ViewStart.cshtml` remain unchanged.
- The future route family is not allocated by this draft.
- Existing legacy/prototype screens are evidence for parity review only.
- No legacy page is an implementation template or production authority.
- A future frontend requires seven-language localization:
  `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`.
- A process graph/modeling workspace may be used; DataTable is not required.
- WorkCenter embedding or projection is outside this pack.

## 10. Backend File Convention

The authorized bounded-module structure is:

```text
services/Diten.ManagementGovernanceService/src/
├── Diten.ManagementGovernanceService.Domain/
│   └── Modules/ProcessModeling/
│       ├── Entities/
│       ├── Enums/
│       ├── Events/
│       ├── ValueObjects/
│       └── Errors/
├── Diten.ManagementGovernanceService.Application/
│   └── Modules/ProcessModeling/
│       ├── Commands/
│       ├── Queries/
│       ├── Handlers/
│       │   ├── CommandHandlers/
│       │   └── QueryHandlers/
│       ├── Validators/
│       ├── Contracts/
│       └── ProcessModelingModels.cs
├── Diten.ManagementGovernanceService.Persistence/
│   └── Modules/ProcessModeling/
│       ├── Collections/
│       ├── Configurations/
│       ├── Indexes/
│       └── Repositories/
└── Diten.ManagementGovernanceService.Infrastructure/
    └── Modules/ProcessModeling/
        ├── Adapters/
        ├── Audit/
        └── Outbox/
```

Rules:

- One public command, query, handler or validator per file.
- Commands and queries are sealed records.
- Handler names omit `Command` and `Query` suffixes.
- Validators omit the `Command` suffix.
- Controllers remain thin.
- Domain objects do not depend on persistence, transport or producer DTOs.
- External contracts are consumed through versioned interfaces and adapters.
- `Modules/ProcessModeling` and `Modules/Dws` share no business entity, repository, Mongo collection or
  task/approval/workflow helper.
- Architecture tests fail if one internal module references the other module’s domain or persistence
  implementation.

## 11. Frontend File Contract

Frontend is excluded from Minimal Core v1.

A later Frontend & Legacy Parity amendment must define:

- Exact tenant route.
- Controller boundary.
- View and JavaScript paths.
- Seven-language resource paths.
- Process graph/editor interaction model.
- Accessibility and keyboard behavior.
- Loading, empty, validation, unauthorized, not-found and concurrency-conflict states.
- Read-only rendering of Published and Retired versions.
- Explicit new-revision interaction for Published-content changes.
- Approval-pending presentation without local approval truth.
- Legacy parity inventory and disposition.

The future frontend must not expose:

- Workflow execution controls.
- Approval task ownership.
- Operational start/complete/assign actions.
- SLA or escalation controls.
- WorkCenter projection controls.
- Runtime process-instance or token controls.
- Local KPI/Metric, classification or artifact payload editors before producer contracts are approved.

No legacy file may be modified, deleted, migrated or deprecated under this draft.

## 12. Validation Rules

### Identity and tenancy

- All IDs are canonical non-empty UUIDs.
- Tenant comes only from authenticated server context.
- Parent and child references resolve in the same tenant.
- Cross-tenant and invisible references return `404`.
- Unknown request fields return `400` for closed contracts.

### Strings and codes

- User text is trimmed and Unicode NFC normalized before persistence.
- Required names are 1–200 characters.
- Descriptions are bounded as specified in §4.
- Codes are immutable after creation.
- Codes normalize to uppercase dash-separated tokens.
- Empty, overlength, malformed, leading/trailing-dash and repeated-dash values fail validation.

### Catalog archive behavior

- Mutable catalog lifecycle contains only `Active` and `Archived`.
- Create produces `Active`.
- Archive changes `Active → Archived`.
- Archived catalog entities reject ordinary update and new-child attachment.
- Reactivation is not included.
- Archive does not set technical deletion fields.
- Cascade archive, ordinary delete and hard delete are forbidden.

### Model-version lifecycle

- Only `Draft → Review → Published → Retired` is valid.
- `Review → Draft` is the only permitted backward transition.
- Published and Retired content mutations return `409`.
- Retired cannot transition.
- New Published-content work requires a new revision.
- Revision numbers are server-generated, strictly increasing and never reused.
- A model with an existing Draft/Review version rejects another open version with `409`.
- Parallel variants are rejected.
- Core publish does not evaluate approval policy or attach an ApprovalOutcome.
- No local `IsApproved`, `ApprovedAt` or `ApprovedBy` field is permitted.

### Activity, control-point and relationship rules

- Activity is a generic definition-time node.
- Control point is descriptive definition-time metadata.
- Relationship is a directional descriptive edge.
- Activity codes and logical IDs are unique within a version.
- Control-point codes and logical IDs are unique within a version.
- Control-point activity reference, when present, resolves in the same version.
- Relationship endpoints resolve in the same version.
- Self-reference is rejected.
- Duplicate exact edge is rejected.
- `ConditionLabel` is never parsed or evaluated.
- Published topology is immutable.

### Concurrency and idempotency

- Mutations require inherited expected `EntityBase.Version` where applicable.
- Stale expected version returns `409` without mutation.
- Same idempotency key, subject and canonical payload returns the original stable result.
- Reuse with different payload or subject returns `409`.
- Receipt lookup cannot bypass current authentication, permission or visibility checks.
- Business mutation, receipt, local audit intent and local outbox are atomic.

### Governance-only references

- Core create/update commands reject owner, interface and approval-outcome reference members.
- A reference becomes acceptable only through its separately approved extension and runtime adapter.
- Unsupported contract, version or mode returns `400`.
- Provider unavailability or indeterminate authoritative validation returns `503`.
- No local cache or inferred truth substitutes for an authoritative provider.

## 13. Failure Path to Verify

The exact failure matrix is:

| HTTP | Exact failure classes |
|---:|---|
| `400` | Malformed reference/request; unsupported contract, version or mode |
| `401` | Unauthenticated request or invalid identity |
| `403` | Permission denial or Separation-of-Duties denial |
| `404` | Missing, deleted, cross-tenant or invisible resource/reference |
| `409` | Stale concurrency, lifecycle conflict or idempotency conflict |
| `503` | Authoritative provider unavailable/indeterminate or transaction infrastructure unavailable/indeterminate |

Additional required behavior:

- Every failure creates no partial business mutation.
- `404` does not disclose cross-tenant existence.
- Unsupported contract/version/mode never falls through to provider inference.
- Provider uncertainty never becomes `400`, `403`, `404` or local truth.
- Transaction infrastructure uncertainty never returns success.
- Approval/Auth/Audit input supplied to Core is rejected as unsupported input.
- Owner/interface reference input without an approved extension is rejected.
- A required receipt, audit-intent or outbox persistence failure rolls back the complete transaction.
- MOD-0021 post-commit delivery does not exist in Core and cannot rewrite Core business truth.

## 14. Authorization Convention

The permission inventory is the exact 16-value closed set in §3.

### Exact command-to-permission mapping

| Command | Exact permission |
|---|---|
| `CreateProcessArchitectureCommand` | `management-governance.process-modeling.architectures.create` |
| `UpdateProcessArchitectureCommand` | `management-governance.process-modeling.architectures.update` |
| `ArchiveProcessArchitectureCommand` | `management-governance.process-modeling.architectures.archive` |
| `CreateProcessDomainCommand` | `management-governance.process-modeling.architectures.create` |
| `UpdateProcessDomainCommand` | `management-governance.process-modeling.architectures.update` |
| `ArchiveProcessDomainCommand` | `management-governance.process-modeling.architectures.archive` |
| `CreateProcessFamilyCommand` | `management-governance.process-modeling.architectures.create` |
| `UpdateProcessFamilyCommand` | `management-governance.process-modeling.architectures.update` |
| `ArchiveProcessFamilyCommand` | `management-governance.process-modeling.architectures.archive` |
| `CreateProcessDefinitionCommand` | `management-governance.process-modeling.definitions.create` |
| `UpdateProcessDefinitionCommand` | `management-governance.process-modeling.definitions.update` |
| `ArchiveProcessDefinitionCommand` | `management-governance.process-modeling.definitions.archive` |
| `CreateProcessModelCommand` | `management-governance.process-modeling.models.create` |
| `UpdateProcessModelCommand` | `management-governance.process-modeling.models.update` |
| `UpdateDraftProcessModelVersionCommand` | `management-governance.process-modeling.models.update` |
| `RequestProcessModelReviewCommand` | `management-governance.process-modeling.models.request-review` |
| `ReturnProcessModelToDraftCommand` | `management-governance.process-modeling.models.return-to-draft` |
| `PublishProcessModelVersionCommand` | `management-governance.process-modeling.models.publish` |
| `RetireProcessModelVersionCommand` | `management-governance.process-modeling.models.retire` |
| `CreateProcessModelRevisionCommand` | `management-governance.process-modeling.models.create-revision` |

### Exact query-to-permission mapping

| Query family | Exact permission |
|---|---|
| Architecture, domain and family queries | `management-governance.process-modeling.architectures.read` |
| Definition queries | `management-governance.process-modeling.definitions.read` |
| Model, version, history, graph and validation queries | `management-governance.process-modeling.models.read` |

Authorization rules:

- Runtime module entitlement identity is exactly `ModuleCode = MOD-0355`.
- AuthService remains authoritative for grants and signed-JWT issuance.
- ProcessModeling consumes approved permission enforcement; it does not calculate roles or effective grants.
- Permission and SoD checks occur before mutation and idempotent outcome disclosure.
- `IEntitlementChecker` cannot substitute for command permission enforcement.
- Tenant entitlement and user permission are separate conjunctive gates when runtime activation is approved.
- Role names do not themselves grant authority.
- No wildcard permission is introduced.
- `management-governance.process-modeling.models.update` does not authorize archive.
- `management-governance.process-modeling.models.retire` authorizes only
  `RetireProcessModelVersionCommand`.
- Approval eligibility and delegation remain second-slice external concerns.
- Core may define constants and tests but does not claim AuthService provisioning is runtime available.

## 15. Gateway / API Routing Decision

- Port: `TBD`.
- No Gateway route is authorized.
- No browser-to-service route is authorized.
- No direct browser call to a future service port is permitted.
- Future frontend requests must pass through Gateway port `5000`.
- Any Ocelot change belongs only to `integration-agent` after separate explicit authorization.
- Exact API route family, controller route, internal service port, health route and transport topology are
  B-class runtime decisions.
- Core domain and contract tests do not depend on an allocated HTTP route.
- WorkCenter routes are prohibited.

## 16. Acceptance Criteria

### Core Model & Contract-Test

- [ ] Canonical identity is exactly `MOD-0355 — Business Process Architecture & Modeling`.
- [ ] Runtime ModuleCode is exactly `MOD-0355`.
- [ ] `CAND-CAP-0009` is absent from runtime literals.
- [ ] Service/internal boundary is exactly `Diten.ManagementGovernanceService` /
      `Modules/ProcessModeling`.
- [ ] Core-owned objects match §3.
- [ ] Model-version lifecycle is exactly Draft, Review, Published and Retired.
- [ ] Published and Retired are immutable.
- [ ] Retired is terminal.
- [ ] Published-content changes require a new monotonic revision.
- [ ] Exactly one Draft/Review version exists per model.
- [ ] Parallel variants are rejected.
- [ ] Review may return to Draft without allocating a revision.
- [ ] Minimal Core v1 stores no activity/control-point/relationship classification.
- [ ] Activity is a generic definition-time node, not a runtime task/job/instance.
- [ ] Control point produces no runtime enforcement.
- [ ] Relationship is descriptive topology and its label is not executable.
- [ ] Mutable catalog is limited to Architecture, Domain, Family and Definition.
- [ ] Archive commands do not write technical deletion fields.
- [ ] ProcessModel is durable and non-archivable.
- [ ] No ordinary delete or hard-delete endpoint exists.
- [ ] Concurrency uses inherited `EntityBase.Version` only.
- [ ] Every successful Core mutation atomically persists exactly four participant classes.
- [ ] Tenant isolation and cross-tenant `404` behavior are explicit.
- [ ] ProcessModeling and Dws boundaries are mechanically isolated.
- [ ] ContentHash contract and all three normative vectors pass.

### Approval/Auth/Audit

- [ ] Approval policy and ApprovalOutcome binding remain second-slice work.
- [ ] `ApprovalOutcomeReferenceV1` has exactly `ContractName`, `ContractVersion` and
      `ApprovalOutcomeId`.
- [ ] `ApprovalOutcomeVersion` and additional outcome fields are rejected.
- [ ] Approval outcome is absent from Core entity/request schemas.
- [ ] Governance documentation does not claim runtime availability.
- [ ] `PublishProcessModelVersionCommand` contract, domain transition specification and fail-closed tests may
      exist in Core, but no successful application handler or persistence mutation is authorized until the
      Approval/Auth/Audit second slice is separately approved and executable.
- [ ] MOD-0018, MOD-0021 and MOD-0023 integrations require separate approval and fixtures.

### Optional reference integrations

- [ ] Classification, KPI/Metric and artifact/evidence bindings are absent from Core runtime.
- [ ] Owner and interface references are absent from Core create/update contracts.
- [ ] Missing optional integrations do not block Minimal Core v1 `ready-for-dev`.
- [ ] Each integration requires an exact producer contract, adapter and separate slice approval.
- [ ] External payload truth is never copied into MOD-0355.

### Frontend & Legacy Parity

- [ ] Core readiness does not claim frontend completion.
- [ ] A later frontend uses tenant shell and seven-language localization.
- [ ] Legacy parity is inventoried before frontend implementation.
- [ ] Legacy files remain unchanged without a separate approved migration/deprecation pack.
- [ ] No WorkCenter surface or projection is introduced.

### Runtime activation

- [ ] Draft or ready-for-dev status is not runtime authority.
- [ ] Port, route, transport, credentials, persistence and deployment decisions are explicitly approved.
- [ ] Producer contracts are runtime available, not merely governance available.
- [ ] Applicable Control Tower gates and runtime evidence are green.
- [ ] Explicit implementation and production authority are recorded.

## 17. Test Expectations

### Identity tests

- DCP-002 exact ID/name preflight.
- Registry collision verification.
- Candidate-literal scan.
- Frontmatter and exact-section validation.
- Exact 16 unique permission scan.

### Architecture tests

The exact 12-test matrix below is parameterized and must pass independently for every co-hosted sibling.
The initial logical sibling set is explicitly `Modules/Dws` and `DecisionRegistry`. Dws physical roots remain
`src/Diten.ManagementGovernanceService.{Domain,Application,Persistence,Infrastructure}/Modules/Dws/**`.
DecisionRegistry physical roots are
`src/Diten.ManagementGovernanceService.{Domain,Application,Persistence,Infrastructure}/DecisionRegistry/**`
plus
`tests/Diten.ManagementGovernanceService.Tests/DecisionRegistry/**`,
`tests/Diten.ManagementGovernanceService.IntegrationTests/DecisionRegistry/**` and
`tests/Diten.ManagementGovernanceService.ArchitectureTests/DecisionRegistry/**`, all relative to
`services/Diten.ManagementGovernanceService/`. Any later sibling is added to the same matrix before
composition. Moving DecisionRegistry to `Modules/DecisionRegistry/**` requires a separate amendment and
topology reconciliation; this pack cannot infer that move. For each `Sibling`:

1. `Sibling` project layers cannot reference ProcessModeling project layers; the reciprocal rule applies.
2. Domain/Persistence type and namespace scans show no reciprocal implementation dependency.
3. DI composition registers only module-owned handlers, repositories and adapters; registrations cannot
   replace, decorate or capture the other module's services.
4. Repository interfaces and implementations are module-owned and cannot accept the other module's context.
5. Mongo collection names, mappings, serializers and tenant-first named indexes are disjoint.
6. No shared business entity, value object, aggregate or mutable persistence document exists.
7. Permission families are disjoint: ProcessModeling uses only
   `management-governance.process-modeling.*`; Dws and DecisionRegistry retain only their owner-approved
   permission families.
8. No shared task, approval, workflow, audit-intent or outbox business helper exists.
9. Cross-module communication uses only opaque IDs, versioned query contracts or versioned events.
10. Session and transaction ownership is module-local; no local transaction spans ProcessModeling and
    `Sibling` aggregates, repositories, collections, audit intents or outbox records.
11. Migration, bootstrap and persistence-owner manifests cannot create, rename, adopt or mutate the other
    module's collections or indexes.
12. Negative architecture and mutation tests prove representative type/namespace, DI, repository,
    collection/index, session/transaction, permission, audit-outbox and migration violations fail.

If mechanical isolation cannot be proven, same-host scaffolding is blocked. The fail-closed fallback is
separately owned/deployed service boundaries with separate projects, DI containers, persistence ownership,
collection namespaces and credentials. Exact fallback service names, ports and routes require separate
owner/runtime decisions. This fallback grants no runtime authority.

### Core unit tests

- Catalog `Active → Archived` transition.
- Archive does not set technical deletion fields.
- Exact model-version transition matrix.
- Published immutability.
- Retired terminal behavior.
- Monotonic revision allocation.
- Single-open-version invariant.
- Parallel-variant rejection.
- Review-to-Draft behavior.
- Code normalization and uniqueness.
- Activity/control logical identity uniqueness.
- Control-point activity-reference integrity.
- Relationship endpoint integrity.
- Self-reference rejection.
- Duplicate exact-edge rejection.
- ConditionLabel non-evaluation.
- Optimistic concurrency through inherited technical version.
- Idempotency replay/conflict behavior.
- Exact command-to-permission mapping.
- Exact error matrix.

### Canonical hash tests

- Exact contract-name/version fixture.
- Exact root and child property order.
- Exact normalization fixtures.
- Exact deterministic collection ordering.
- Exact BOM-less compact UTF-8 framing.
- Vector 1: `194` bytes and expected SHA-256.
- Vector 2: `208` bytes and expected SHA-256.
- Vector 3: `1421` bytes and expected SHA-256.
- Vector 3 intentionally unsorted input produces the displayed canonical output.
- Equivalent logical content produces identical bytes/hash.
- Materially different content produces a different hash.
- Default serializer ordering fails the fixture.

### Persistence and integration tests

- Real MongoDB BSON UTC representation.
- Required indexes and partial unique open-version index.
- Replica-set transaction rollback.
- Tenant isolation and cross-tenant indistinguishable `404`.
- Atomic revision allocation under concurrency.
- Atomic persistence of the four Core participants.
- Failure of any participant rolls back all four.
- No standalone-Mongo partial fallback.
- No ordinary delete, TTL or purge path.

### Reference contract tests

- Exact three-field `ApprovalOutcomeReferenceV1` serialization.
- Missing, duplicate and unknown outcome field rejection.
- `ApprovalOutcomeVersion` rejection.
- Unknown contract/version/mode returns `400`.
- Governance-available/runtime-unavailable behavior fails closed.
- Owner/interface input fails closed without approved adapters.
- Provider unavailable/indeterminate returns `503`.

Frontend and runtime activation tests become mandatory only in their separately approved slices.

## 18. Ready-for-dev Checklist

### A-class decisions

- [x] **A-01 — CLOSED WITH INTERIM ACCOUNTABLE ROLES:** Business owner is
      `ali.tufanoglu / business-process-governance-owner-interim`; technical owner is
      `ali.tufanoglu / management-governance-technical-owner-interim`. Permanent organizational assignees
      remain a pre-production follow-up, not a bounded Core blocker.
- [x] **A-02 — CLOSED FOR CORE:** Exact lifecycle, immutability, terminal Retired state, monotonic revisions,
      one open Draft/Review and no parallel variants.
- [x] **A-03 — CLOSED FOR CORE:** `ModuleCode = MOD-0355`, exact 16 permissions and exact command/query
      mapping. ProcessModel has no archive command.
- [ ] **A-04 — SECOND-SLICE OWNER DECISION / NOT A CORE CONTRACT BLOCKER:** Decide whether authoritative
      `approval not required` permits direct publish by an authorized publisher. Until this closes,
      `PublishProcessModelVersionCommand` may exist only as a contract/domain transition specification and
      fail-closed test target; a successful application handler or persistence mutation is unauthorized.
- [x] **A-05 — NOT APPLICABLE TO MINIMAL CORE V1:** Activity classification deferred to a future versioned
      amendment.
- [x] **A-06 — NOT APPLICABLE TO MINIMAL CORE V1:** Control-point classification deferred to a future
      versioned amendment.
- [x] **A-07 — NOT APPLICABLE TO MINIMAL CORE V1:** Relationship classification deferred to a future
      versioned amendment.
- [x] **A-08 — CLOSED FOR CORE:** Exact
      `management-governance.process-modeling.content-hash@1.0`, corrected field order, normalization,
      deterministic sorting, framing and three independently recomputed vectors.
- [x] **A-09 — CLOSED BY ACCOUNTABLE OWNER:** Exact archive and retention policy is confirmed: four mutable
      catalogs only; durable non-archivable ProcessModel; immutable Published/Retired history; no ordinary
      delete, TTL or purge.
- [x] **A-10 — CLOSED BY ACCOUNTABLE OWNER:** The exact 12-test isolation matrix and mandatory fail-closed
      split fallback are confirmed.
- [x] **A-11 — CLOSED BY CONTROL TOWER:** Missing classification, Approval/Auth/Audit, KPI/Metric, Artifact,
      owner/interface adapters, frontend, Gateway and runtime activation do not block Minimal Core v1
      Core Model & Contract-Test `ready-for-dev`.
- [x] **A-12 — CLOSED WITH INTERIM ACCOUNTABLE ROLE:** Legacy parity/disposition owner is
      `ali.tufanoglu / business-process-governance-owner-interim`; Control Tower is reviewer, not the
      accountable owner.
- [x] **A-13 — CLOSED BY CONTROL TOWER:** Governance availability, implementation authority and runtime
      activation are separate gates.

### Core ready-for-dev gates

- [x] Master 8.1 promotion is merged into the materialization target.
- [x] DCP-002 exact identity preflight passes.
- [x] Registry row resolves exactly to MOD-0355 without collision.
- [x] A-01, A-09 and A-10 accountable-owner confirmations are recorded.
- [x] Exact owned objects, lifecycle, permissions and protected paths are approved.
- [x] Exact ContentHash contract and three vectors pass.
- [x] Exact error matrix and four-participant transaction boundary are approved.
- [x] Core contract-test matrix is approved.
- [x] Ready-for-dev grants only the exact bounded implementation authority recorded in frontmatter and §5;
      no runtime or production authority is inferred.
- [x] Explicit user authorization dated 2026-08-25 is recorded for the bounded Core scaffold and
      Contract-Test slice only.

### B-class runtime blockers

- [ ] **B-01:** Service port allocation.
- [ ] **B-02:** Gateway route and transport topology.
- [ ] **B-03:** AuthService module catalog, entitlement and 16-permission provisioning.
- [ ] **B-04:** Reusable MOD-0018 permission/SoD enforcement.
- [ ] **B-05:** MOD-0023 ApprovalOutcome amendment promotion and adapter.
- [ ] **B-06:** Approval-policy resolution, eligibility/delegation and bilateral fixtures.
- [ ] **B-07:** MOD-0021 signed/versioned event contract, publisher identity, delivery and replay evidence.
- [ ] **B-08:** Classification producer contract.
- [ ] **B-09:** KPI/Metric producer contracts.
- [ ] **B-10:** Owner-reference producer contract and adapter.
- [ ] **B-11:** Interface-reference producer contract and adapter.
- [ ] **B-12:** Binary artifact/evidence storage and reference contracts.
- [ ] **B-13:** Frontend route, UX, localization and legacy-parity approval.
- [ ] **B-14:** Runtime credentials, observability, deployment and rollback evidence.
- [ ] **B-15:** Explicit runtime activation and production authority.

B-class items do not block Minimal Core v1 Core Model & Contract-Test `ready-for-dev`. They block only their
named later slice or runtime activation.

## 19. Implementation Notes

### Staged-slice contract

| Slice | Scope | Readiness relationship |
|---|---|---|
| Core Model & Contract-Test | Minimal unclassified definition model, lifecycle, revision, topology, tenancy, concurrency, hashing, idempotency, local audit-intent/outbox and architecture tests | Independent of external producer integrations |
| Approval/Auth/Audit | Permission/SoD runtime enforcement, approval policy, authoritative outcome binding and MOD-0021 delivery | Separate second slice |
| Optional reference integrations | Classification, KPI/Metric, owner, interface, document, evidence and artifact references | Independent later slices |
| Frontend & Legacy Parity | Tenant UI, localization, parity inventory and approved disposition | Separate later slice |
| Runtime activation | Port, Gateway, transport, credentials, deployment, observability and production authorization | Separate final gate |

### Core transaction boundary

Every successful Core mutation has exactly four atomic local Mongo transaction participants:

1. business mutation;
2. idempotency receipt;
3. producer-local audit intent;
4. producer-local outbox.

Local audit intent and outbox are Core durability infrastructure. They do not establish MOD-0021 runtime
availability. MOD-0021 signed event contract, publication adapter, publisher credentials, compatibility
fixtures, retry, dead-letter, alarm and replay behavior remain in the Approval/Auth/Audit slice.

### Archive and retention

Architecture, Domain, Family and Definition use only `Active → Archived`. Archive never writes technical
deletion fields and never cascades.

ProcessModel is a durable non-archivable identity. Published and Retired version history, activities,
control points, relationships, canonical content and hash are immutable and retained for tenant/service
lifetime.

Receipts, local audit intents and local outbox records have no TTL or purge under this pack. Future bounded
retention, tenant offboarding, legal erasure or exceptional purge requires separate authority.

### Approval boundary

`ApprovalOutcomeReferenceV1` is documented only for the second slice. It is absent from Core entity and
request schemas. Its documentation does not mean the producer amendment, endpoint, adapter or transport is
runtime available.

### Optional reference boundary

`ProcessOwnerReference` and `ProcessInterfaceReference` remain governance shapes only. Core create/update
contracts do not accept them. Acceptance requires a separately approved producer contract, compatibility
fixtures and runtime adapter.

### Legacy parity gate

Before Frontend & Legacy Parity implementation:

1. Inventory existing Management Governance, Delivery Execution, ESBP and process-modeling prototype
   surfaces read-only.
2. Map every legacy behavior to `preserve`, `replace later`, `quarantine` or `out of scope`.
3. Identify task, approval, SLA, escalation, WorkCenter and runtime-engine hazards.
4. Obtain accountable-owner approval for the parity matrix.
5. Obtain Control Tower review before any protected hazard is modified.
6. Use a separate approved migration/deprecation scope for any legacy change.

Passing the parity gate authorizes comparison and planning only. It does not authorize legacy mutation.

### Governance availability versus runtime availability

A contract is governance-available when its name, version, shape, ownership and failure semantics are
approved for authoring. It is runtime-available only after producer promotion, implementation,
authenticated transport, compatibility fixtures, operational evidence and explicit runtime authority.

Governance availability cannot become local fallback, hard-coded truth or inferred producer availability.

## 20. Follow-up Items

- Replace interim business, technical and legacy-parity accountable roles with permanent organizational
  assignees before production activation.
- Introduce activity/control-point/relationship classification only through a future versioned amendment.
- Resolve approval policy and ApprovalOutcome binding in the Approval/Auth/Audit slice.
- Add MOD-0018 permission/SoD enforcement through an owner-approved PSS slice.
- Add MOD-0021 delivery only after its signed/versioned contract and operational evidence exist.
- Add classification, KPI/Metric, owner, interface, document, evidence and artifact references through
  separately approved producer contracts.
- Define and approve tenant frontend and seven-language resources.
- Complete the read-only legacy parity inventory and owner-approved disposition.
- Allocate service port and Gateway route only during separately authorized runtime activation.
- Define any process-instance/runtime engine as a separate canonical capability.
- Preserve the bounded Core implementation authority recorded in frontmatter and §5. Runtime activation,
  deployment and production authority remain absent until separately recorded.

### Mechanical self-check

- Exact numbered headings: `20`
- Heading sequence: `## 1.` through `## 20.`
- Exact unique permissions: `16`
- Markdown fences: balanced
- ActivityKind occurrence: deferred/future-amendment explanation only
- ControlKind occurrence: deferred/future-amendment explanation only
- RelationshipKind occurrence: deferred/future-amendment explanation only
- Prohibited ProcessModel archive command identifier occurrence: `0`
- ProcessModel catalog lifecycle field occurrence: `0`
- Additional concurrency-field identifier occurrence: `0`
- ApprovalOutcomeReference in Core schema: absent
- Error matrix: exact `400 / 401 / 403 / 404 / 409 / 503`
- Core atomic participants: `4`
- Normative hash vectors: `3`
- Non-empty normative graph vectors: `1`
- Vector recomputations: `PASS / PASS / PASS`
- Status: `ready-for-dev`
- Implementation authority: `explicit-user-control-tower-bounded-core-model-contract-test`
- Production authority: `none`
- Initial materialization scope: exact-one MOD-0355 module-pack file
- Ready-for-dev promotion reconciliation scope: exact four governance files (this pack, DCP-005, DCP-006
  and the module registry)
- Bounded implementation scope: only the exact shared-shell and ProcessModeling paths and guards in §5
