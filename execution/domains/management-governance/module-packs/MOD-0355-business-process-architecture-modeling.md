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
port: 5017
implementation_authority: explicit-user-control-tower-bounded-core-local-api-test-and-performance-improvement-contract-test
production_authority: none
---

# MOD-0355 — Business Process Architecture & Modeling

> **Canonical materialization provenance:** Canonical Master 8.1 promotion checkpoint
> `ee1d7dc5766a67012df2af35d3eed1aa779da27d` was merged into the materialization base by merge commit
> `e034c5fddd0d05e0dda6ef72ce88f41a36efa3d4`. This materialized file is ready-for-dev governance authority
> for the bounded Core Model & Contract-Test slice. The later explicit Control Tower amendment separately
> authorizes the exact non-production Local API-Test slice; neither authority grants production runtime.

> **Ready-for-dev bounded implementation guard:** `status: ready-for-dev`,
> `implementation_authority: explicit-user-control-tower-bounded-core-local-api-test-and-performance-improvement-contract-test` and
> `production_authority: none` are binding. The explicit user decision dated 2026-08-25 authorizes the exact
> Core scaffold, module and test paths in §5. The later Local API-Test amendment is the sole scoped exception
> to the listener/endpoint/port-binding prohibitions in this guard and §§5/8/15: it permits only the exact
> `--local-test` loopback surface and paths named there. It grants no Gateway, frontend, WorkCenter,
> credential, migration, deployment, external adapter, production activation or modification of another
> internal module.

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
- Port: `5017` (canonical reservation; binding only under the exact non-production `--local-test` exception)

Minimal Core v1 is a backend-only **Core Model & Contract-Test** slice. It models tenant-owned process
architectures, domains, families, definitions, durable model identities, immutable published revisions,
generic definition-time activity nodes, definition-time control points and directional descriptive topology.

Minimal Core v1 does not execute a process and does not define classification vocabularies for activity,
control-point or relationship records.

This is not a conventional DataTable CRUD module. `golden_reference: none` and `form_field_count: 0` are
intentional. Frontend delivery is a separately authorized later slice.

Historical `CAND-CAP-0009` is a deprecated governance alias only. It must never appear in runtime literals,
permissions, routes, collections, events, jobs, configuration or persisted module identity.

Delivery is divided into seven independently gated slices:

1. Core Model & Contract-Test
2. Approval Policy & Publish Authorization Contract-Test
3. Local API-Test Runtime — NON-PRODUCTION / DEFAULT-OFF
4. Approval/Auth/Audit Runtime
5. Optional reference integrations
6. Frontend & Legacy Parity
7. Runtime activation

The Blueprint 8.1 Performance & Improvement Contract-Test amendment in §19 is an eighth independently
gated, non-production/default-off slice. It authorizes only the exact improvement contract/persistence/test
paths in §5. Process KPI Binding remains fail-closed because its certified metric/calculation reference
contract is not allocated in executable form.

Governance availability alone is not runtime or implementation availability. Present implementation authority
exists only for (a) the bounded Core Model & Contract-Test paths in §5 and (b) the exact NON-PRODUCTION /
DEFAULT-OFF Local API-Test Runtime amendment in §19; runtime deployment, Gateway and production authority
remain absent.

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
- Process KPI binding and process-improvement runtime behavior; the Blueprint 8.1 ownership amendment in
  §19 is governance-only, non-executable and default-off.
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
| KPI/Metric definition | `MOD-0059` / `MOD-0060` | Definitions, calculations and certified metric truth stay external; MOD-0355 owns only the exact process-version binding record declared by the Blueprint 8.1 amendment in §19 |
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

### Blueprint 8.1 bounded amendment owned objects

The following canonical objects are owned by MOD-0355, not by a new module or FU identity:

- `Process KPI Binding` (`ProcessKPIBindingID`)
- `Process Improvement Opportunity` (`ProcessImprovementOpportunityID`)
- `Process Improvement Outcome` (`ProcessImprovementOutcomeID`)

This ownership declaration does not add them to Minimal Core v1 schemas. The separate bounded executable
contract in §19 authorizes only the exact default-off Improvement paths in §5; KPI Binding and every
host-level authorization/delivery path remain dependency-blocked.

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
- Approval policy and ApprovalOutcome contract binding belong to the non-runtime contract-test second slice;
  executable enforcement and delivery belong to the later Approval/Auth/Audit Runtime slice.
- Minimal Core v1 records no local approval truth.

### Core exact permission inventory

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

### Performance & Improvement additive permission/operation inventory — DEFAULT-OFF

This versioned amendment owner-declares exactly nine additional ordinal operation/permission pairs. Each
operation ID is identical to its permission key; aliases, wildcard, prefix, trim and case-fold matching are
forbidden:

| Exact operation ID | Exact permission |
|---|---|
| `management-governance.process-modeling.kpi-bindings.read` | `management-governance.process-modeling.kpi-bindings.read` |
| `management-governance.process-modeling.kpi-bindings.create` | `management-governance.process-modeling.kpi-bindings.create` |
| `management-governance.process-modeling.kpi-bindings.archive` | `management-governance.process-modeling.kpi-bindings.archive` |
| `management-governance.process-modeling.improvements.read` | `management-governance.process-modeling.improvements.read` |
| `management-governance.process-modeling.improvements.create` | `management-governance.process-modeling.improvements.create` |
| `management-governance.process-modeling.improvements.update` | `management-governance.process-modeling.improvements.update` |
| `management-governance.process-modeling.improvements.qualify` | `management-governance.process-modeling.improvements.qualify` |
| `management-governance.process-modeling.improvements.close` | `management-governance.process-modeling.improvements.close` |
| `management-governance.process-modeling.improvements.record-outcome` | `management-governance.process-modeling.improvements.record-outcome` |

The resulting owner declaration is exactly twenty-five unique operations and twenty-five unique permissions:
the existing sixteen plus these nine. The existing FU16 sixteen-entry manifest is not silently reinterpreted;
the nine-entry delta requires a separate bilateral FU16 amendment and executable fixture before any local API,
delegated invocation or authorization success. Until then every new operation is unavailable (`503`), never
mapped to a Core permission by analogy.

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
| `ModelAuthorActorId` | Guid | Yes | Server-resolved effective actor captured once at version creation; immutable and never accepted from client input |
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

`ModelAuthorActorId` is the authoritative source for `PublishActorProvenanceV1.ModelAuthorActorId`. It is
security provenance, not an owner assignment or audit-derived field. Create/revision paths capture it from
the fresh authenticated/FU16 effective actor in the same transaction as the new version; update paths cannot
change it.

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

For `UpdateDraftProcessModelVersionCommand`, graph replacement is a single Draft-only transaction operation:
after the tenant/lifecycle/version fence succeeds, the existing activity, control-point and relationship rows
for that exact `TenantId + ProcessModelVersionId` are removed and the complete validated replacement sets are
inserted in the same transaction. This internal `DeleteMany + InsertMany` composition is the sole scoped
exception to the ordinary hard-delete prohibition: it is not an endpoint, purge, retention policy or delete
capability; it cannot target Published/Retired, another tenant or any other version. A failure in any of the
three replacement collections rolls back the version CAS and all graph changes. No tombstone or historical
row is exposed as current graph state for a mutable Draft.

### Process KPI Binding — exact dependency-blocked contract

The canonical object identity is `ProcessKPIBindingID`. A valid binding must contain the inherited tenant
entity fields plus exactly these business meanings:

- one required exact `ProcessModelVersionId`;
- one required certified metric-definition reference;
- one required calculation-contract reference;
- one required lifecycle value `Active` or `Archived`.

Create would require a same-tenant non-deleted `Published` ProcessModelVersion. Archive would be the only
mutation after create; `Active → Archived` is terminal. Rebinding, update-in-place, delete, hard delete, TTL
and cascade are forbidden. The future unique identity must be tenant-first and distinguish the exact process
version plus both exact external references.

MOD-0059/MOD-0060 have no executable pack in this materialized baseline that supplies the exact contract
names, versions, reference fields, calculation identity or compatibility fixtures. Therefore the two
reference meanings above are not serialized field names and no entity/request/schema/index is authorized for
this subpath. Create, archive and read return unavailable (`503`) with zero residue until that producer
contract and its bilateral fixture are approved. A free-text metric code, local formula, copied metric payload
or opaque GUID guessed as metric truth is forbidden.

### Process Improvement Opportunity — bounded executable contract

`ProcessImprovementOpportunityID` maps to a tenant-owned entity with the inherited `EntityBase` fields and
exactly these business fields:

| Field | Type | Required | Exact rule |
|---|---|---:|---|
| `ProcessModelVersionId` | Guid | Yes | Same-tenant, non-deleted Published version at create |
| `Title` | string | Yes | NFC, trimmed, 1–200 |
| `ProblemStatement` | string | Yes | NFC, trimmed, 1–4000 |
| `ExpectedOutcome` | string? | No at create; Yes at qualify | NFC, trimmed, 1–2000 when present |
| `PriorityRank` | int? | No at create; Yes at qualify | Positive integer; ordinal priority only |
| `LifecycleState` | enum | Yes | Exactly `Open`, `Qualified` or `Closed` |

Lifecycle is exact and forward-only:

```text
Open → Qualified → Closed
```

Create produces `Open`. Only `Open` permits title/problem/expected-outcome/priority update. Qualify requires
non-empty `ExpectedOutcome`, positive `PriorityRank`, current expected `EntityBase.Version` and the original
same-tenant process-model version to remain referenceable. Closed is terminal and requires at least one
same-tenant immutable outcome. Return-to-open, requalification, delete, archive, task/project creation,
workflow/approval execution and process-instance behavior are absent.

The tenant-first query index is
`TenantId + ProcessModelVersionId + LifecycleState + PriorityRank + Id`; it is non-unique and has no TTL.
Cross-tenant, deleted or non-disclosable records return `404`; stale CAS returns `409`.

### Process Improvement Outcome — bounded executable contract

`ProcessImprovementOutcomeID` maps to a tenant-owned immutable entity with the inherited `EntityBase` fields
and exactly these business fields:

| Field | Type | Required | Exact rule |
|---|---|---:|---|
| `ProcessImprovementOpportunityId` | Guid | Yes | Same-tenant Qualified opportunity |
| `Summary` | string | Yes | NFC, trimmed, 1–4000; locally authored summary, not copied evidence truth |
| `ObservedAtUtc` | scalar BSON UTC DateTime | Yes | Server-produced UTC; client values rejected |

Outcome creation is append-only and idempotent. After creation no update, archive, delete, hard delete, TTL or
lifecycle transition exists. Multiple outcomes may belong to one Qualified opportunity. The tenant-first
query index is `TenantId + ProcessImprovementOpportunityId + ObservedAtUtc + Id`, with deterministic
`ObservedAtUtc` then `Id` ordering.

Decision, project, task, experiment and evidence references are not fields in this bounded contract because
their exact producer contracts are absent here. Supplying any such member is unsupported input (`400`), not a
free-text or opaque-ID fallback. Their absence does not block the local opportunity/outcome core above.

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

### A-04 publish policy closure — contract-test-only / non-runtime

The accountable owner decision is closed as follows:

- When the authoritative approval policy returns **approval not required**, direct publish is eligible only
  after all of these independent gates allow: current tenant entitlement for `ModuleCode = MOD-0355`, the exact
  `management-governance.process-modeling.models.publish` permission, and a fresh authoritative
  Separation-of-Duties/eligibility decision.
- When the authoritative approval policy returns **approval required**, publish additionally requires one exact
  `ApprovalOutcomeReferenceV1` whose MOD-0023 authority resolves it as final and approved for the current trusted
  tenant and the exact `ProcessModelId`, `ProcessModelVersionId` and `ContentHash` being published.
- The effective publisher must differ from the model author/requester. When approval is required, the effective
  publisher must also differ from the authoritative approval decision actor. Equality is a terminal SoD denial;
  role names, local grants, cached guesses and client-supplied actor fields cannot override it.
- Delegated execution requires the exact effective actor from a fresh, request-bound FU16
  `DelegatedActorProofV1`. Missing, stale, replayed or invalid proof is never converted into local actor truth.
- An explicit authoritative entitlement, permission, SoD, eligibility or approval denial returns `403`.
  Unavailable, timed-out, malformed or indeterminate authority, including unresolved effective actor identity,
  returns `503` and no publish mutation runs.

#### PublishActorProvenanceV1 — module-local server-owned contract

`PublishActorProvenanceV1` is a MOD-0355-owned, non-runtime value contract with exactly these fields:

| Field | Type | Exact rule |
|---|---|---|
| `TenantId` | Guid | Required server-resolved tenant; never client input |
| `ProcessModelId` | Guid | Required trusted MOD-0355 model identity |
| `ProcessModelVersionId` | Guid | Required trusted MOD-0355 version identity |
| `ContentHash` | string | Required exact current §4 canonical content hash |
| `ModelAuthorActorId` | Guid | Required server-trusted effective actor that created this exact version |
| `PublishRequesterActorId` | Guid | Required effective actor initiating this publish request |
| `CapturedAtUtc` | scalar UTC DateTime | Required server timestamp for the composed publish-request provenance |
| `ProvenanceVersion` | string | Required constant `1.0` |

The server captures `ModelAuthorActorId` when the draft/version is created and keeps it immutable for that
version. A later updater never becomes the author and latest-updater identity is never used for A-04 SoD.
At publish-request receipt, the server resolves `PublishRequesterActorId` from the authenticated effective
actor, binds the current trusted tenant/model/version/hash tuple, stamps `CapturedAtUtc` and composes the
immutable provenance value. The requester is therefore exactly the actor initiating publish; it is not a
client-selected owner, assignee or approver. No field is copied from request JSON, claims projected by the
client, audit history or a prior attempt. Tenant, version or hash mismatch is terminal and the value cannot be
rebound to another model/version.

This provenance is an authorization input, not authorization truth. Audit may record a bounded decision
result after evaluation, but an audit record, audit actor, latest updater or local history can never source or
override author/requester identity.

#### PublishApprovalPolicyDecisionV1 — MOD-0355 Approval Policy Owner contract

The MOD-0355 Approval Policy Owner evaluates an exact module-local request tuple with these seven fields and
no aliases or optional security inputs:

| Field | Exact binding |
|---|---|
| `TenantId` | trusted `PublishActorProvenanceV1.TenantId` |
| `ModelId` | trusted `PublishActorProvenanceV1.ProcessModelId` |
| `VersionId` | trusted `PublishActorProvenanceV1.ProcessModelVersionId` |
| `ContentHash` | trusted `PublishActorProvenanceV1.ContentHash` |
| `PublisherActorId` | effective publisher resolved by authoritative authentication/delegation |
| `RequesterActorId` | trusted `PublishActorProvenanceV1.PublishRequesterActorId` |
| `AuthorActorId` | trusted `PublishActorProvenanceV1.ModelAuthorActorId` |

`ModelId` and `VersionId` are policy-request field names only; they bind exactly to the established
`ProcessModelId` and `ProcessModelVersionId` identities and do not create aliases or new model identities.
The typed decision contains:

- closed `AuthorityState`: `Available`, `Unavailable`, `Malformed` or `Indeterminate`;
- closed `Requirement`: `Required` or `NotRequired`, present only when `AuthorityState = Available`;
- monotonic positive `PolicyVersion`;
- scalar UTC `ObservedAtUtc` and `ValidUntilUtc`, with `ValidUntilUtc > ObservedAtUtc`.

Only a fresh `Available` decision for the exact tuple may continue to the independent entitlement,
permission, SoD/eligibility and optional approval-outcome gates. Cache, last-known-good, local policy,
default-not-required and stale allow are forbidden. The exact tuple, `Requirement` and `PolicyVersion` are
revalidated immediately before the publish transaction would commit. A changed policy/version/requirement,
changed trusted tuple, or a decision outside its freshness interval returns `409` with no mutation.
`Unavailable`, `Malformed` or `Indeterminate` authority returns `503`; none may be translated into
`Required`, `NotRequired`, `403` or a local guess.

The effective publisher still must differ from `RequesterActorId` and `AuthorActorId`; when approval is
required it must also differ from the authoritative approval decision actor. A fresh request-bound FU16 proof
is mandatory for delegated execution. These module-local contracts allocate no external provider identity,
transport, route, credential, event identity or production composition.

The approval reference remains exactly three fields. The tenant/model/version/hash binding is trusted
server-side validation context and is not added to `ApprovalOutcomeReferenceV1`. The upstream immutable workflow
template-version identity and its eligibility rules remain a MOD-0023 producer decision; this pack does not invent
or select a template identity.

This closes the A-04 policy decision only. It does not make publish executable. The approved non-runtime
governance checkpoints are reconciled exactly as follows:

| Boundary | Immutable checkpoint | Closed governance evidence | Still-open executable/runtime evidence |
|---|---|---|---|
| MOD-0018 FU16 | `eefcbc63691c92fbbf4d41977df70d751ad8744b` | Existing Management Governance workload reuse, exact `ModuleCode = MOD-0355`, separate tenant entitlement, explicit-grant-only posture and exact sixteen-operation/sixteen-permission onboarding manifest are accepted governance-only | Executable manifest fixtures, AuthService provisioning, current entitlement/grant/freshness enforcement, credentials and production composition |
| MOD-0023 ProcessModelVersion source | `10b679baf3f9de83cf7069b9e5f8fb22a72c240e` | Exact `management-governance.process-model-version-context` / `1.0` source profile, pinned published template-version mapping and exact source-attestation family are defined DRAFT / NON-EXECUTABLE; `ApprovalOutcomeReferenceV1` remains exact three fields | Immutable bilateral source/attestation/outcome fixtures, production template mapping, provider/adapter implementation, signing/JWKS provisioning and executable outcome binding |
| MOD-0355 producer fixture | `3e69577e787ac7ccca149b4065e83eeb97fd5146` | Exact producer event/schema/allowlist/projection/provenance/test-only signing fixture set is immutable NON-RUNTIME evidence | Production signing/key provisioning, publisher composition and live transport evidence |
| MOD-0021 consumer admission | `58b8b0e766314a86314a888ec865bb01ed5bb54e` | The exact MOD-0355 producer checkpoint is accepted by the five-producer bilateral governance matrix | Executable consumer adapter, inbox/outbox transaction evidence, broker delivery, retry/DLQ/alarm/replay and live PPM regression evidence |

No governance checkpoint above is runtime or production authority. No missing route, production credential,
template mapping instance, adapter or transport value is inferred. Until the still-open executable/runtime
evidence is separately authorized and passes, the only executable publish behavior remains the existing
fail-closed `503` contract; no successful application handler, persistence mutation or delivery adapter is
authorized.

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

Failure to persist any participant rolls back all four. MOD-0021 governance admission is recorded at
`58b8b0e766314a86314a888ec865bb01ed5bb54e`; executable consumer/delivery composition, runtime event type,
publisher credentials and delivery adapter remain in the Approval/Auth/Audit Runtime slice.

### Producer-owned audit event fixture contract — NON-RUNTIME

This subsection authorizes governance and executable fixture validation only. It does not authorize a service
event type, publisher, broker, listener, DI registration, credential, MOD-0021 admission or runtime delivery.

Exact identity:

- Event type: `ProcessModelingAuditIntentSubmittedV1`
- Event name: `management-governance.process-modeling-audit-intent.submitted.v1`
- Event version: JSON integer `1`
- Schema identity: `urn:diten:management-governance:process-modeling-audit-intent-submitted:v1`
- Producer: `Diten.ManagementGovernanceService`
- Module code: `MOD-0355`
- Audit category: `AuditCategory.Integration`
- Signature algorithm: `HmacSha256`
- Signature scheme: `management-governance-event-hmac-sha256.v1`
- Reserved production signing identity: `diten.management-governance.process-modeling-audit-intent.v1`
- Exact test-only signing identity:
  `diten.management-governance.process-modeling-audit-intent.v1.test-only`
- Exact test-only key ID: `mod-0355-process-modeling-audit-fixture-test-only-20260826`

No production key, production key ID, active/previous slot or production authority is allocated. The public
test-only key is fixture material only and production validators must reject its identity, key ID and signature
in both active and previous slots.

The canonical payload is minified UTF-8 without BOM or trailing newline and has exactly this property order:
`auditIntentId`, `actorId`, `entityType`, `entityId`, `mutation`, `occurredAtUtc`. Producer payload fields never
carry trusted transport provenance, signing identity, FU16 proof or permission state.

The exact approved entity/mutation pairs are:

| Entity type | Mutations |
|---|---|
| `ProcessArchitecture` | `Created`, `Updated`, `Archived` |
| `ProcessDomain` | `Created`, `Updated`, `Archived` |
| `ProcessFamily` | `Created`, `Updated`, `Archived` |
| `ProcessDefinition` | `Created`, `Updated`, `Archived` |
| `ProcessModel` | `Created`, `Updated` |
| `ProcessModelVersion` | `DraftUpdated`, `ReviewRequested`, `ReturnedToDraft`, `Published`, `Retired`, `RevisionCreated` |

The matrix contains exactly `20` positive pairs and all `34` remaining cross-pairs are terminal fail-closed.
Projection is exact: source service `Diten.ManagementGovernanceService`, source module `MOD-0355`, category
`AuditCategory.Integration`, entity type `ProcessModeling.<entityType>` and operation mapping:
`Created|RevisionCreated → AuditOperation.Create`, `Updated|DraftUpdated → AuditOperation.Update`,
`Archived|Retired → AuditOperation.Deactivate`, `Published → AuditOperation.Activate`, and
`ReviewRequested|ReturnedToDraft → AuditOperation.LifecycleTransition`.

Delegated provenance is trusted transport metadata, never producer payload. The fixture profile binds the
exact operation `process-modeling.architectures.create.v1` to exact permission
`management-governance.process-modeling.architectures.create`; missing, malformed, unverified, expired or
request/tenant/actor/operation/permission-mismatched proof is terminal and produces no `AuditEvent`. The
non-delegated profile requires structural absence of delegation fields; the same absence in a delegated profile
is missing proof and fails closed.

The fixture root is producer-owned:
`execution/domains/management-governance/module-packs/fixtures/MOD-0355/audit/**`. Its canonical payload,
checksum, schema, envelope, projection, allowlist, provenance, production-rejection, signing-vector and manifest
files are one indivisible contract-test set. The producer fixture set is immutable at
`3e69577e787ac7ccca149b4065e83eeb97fd5146` and MOD-0021 governance admission accepts that exact checkpoint at
`58b8b0e766314a86314a888ec865bb01ed5bb54e`. This closes only the bilateral governance/design gate; it does not
provide a consumer adapter, credential, publisher, transport or runtime authority.

## 5. Repo Scope

Governance materialization scope was limited to:

- `execution/domains/management-governance/module-packs/MOD-0355-business-process-architecture-modeling.md`

The explicit 2026-08-26 producer-fixture authorization additionally permits only the pack above and:

- `execution/domains/management-governance/module-packs/fixtures/MOD-0355/audit/**`

That authority is governance/fixture-only and cannot create or modify service source, runtime composition,
publisher code, transport, credential, frontend, Gateway, WorkCenter or deployment files.

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

The Blueprint 8.1 Performance & Improvement Contract-Test amendment narrows its additional implementation
authority to these exact paths only:

- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Domain/Modules/ProcessModeling/PerformanceImprovement/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/Modules/ProcessModeling/PerformanceImprovement/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Persistence/Modules/ProcessModeling/PerformanceImprovement/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Infrastructure/Modules/ProcessModeling/PerformanceImprovement/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.Tests/Modules/ProcessModeling/PerformanceImprovement/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.IntegrationTests/Modules/ProcessModeling/PerformanceImprovement/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.ArchitectureTests/Modules/ProcessModeling/PerformanceImprovement/**`
- `execution/domains/management-governance/module-packs/fixtures/MOD-0355/performance-improvement-audit/**`

Within that allowlist, executable source is authorized only for the local Improvement Opportunity/Outcome
Domain, Application and Persistence contracts, owner-side permission/audit fixtures and tests. KPI Binding
source is limited to fail-closed dependency-gate contracts/tests; no entity, collection or successful handler
is authorized until the exact MOD-0059/MOD-0060 reference contract closes. Root DI files, shared Program,
controllers, local-test routes, listeners, workers and AuthService/Platform source are not in this amendment.
No allowlisted file may make the slice reachable from a running host.

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

### Approval Policy & Publish Authorization Contract-Test scope

After a separate explicit implementation authorization, the non-runtime contract-test-only second slice may
create files only under these exact module-local roots:

- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Domain/Modules/ProcessModeling/Approval/**`
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/Modules/ProcessModeling/Approval/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.Tests/Modules/ProcessModeling/Approval/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.IntegrationTests/Modules/ProcessModeling/Approval/**`
- `services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.ArchitectureTests/Modules/ProcessModeling/Approval/**`

That future authorization is limited to policy/value contracts, strict parsing and serialization, provider
ports, fail-closed orchestration contracts and test-owned fakes/fixtures. It does not authorize a successful
publish handler, Mongo mutation, repository, DI activation, external adapter, controller, endpoint, listener,
worker, broker, configuration, credential, port, route, frontend, WorkCenter, deployment or production change.
Existing Core files and shared composition files remain unchanged unless a later exact allowlist explicitly
names an additive hunk. Upstream producer fixture bytes remain owned by their producer checkpoints and cannot be
copied, regenerated or approximated here.

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

- MOD-0018 executable authoritative permission enforcement and, when required, eligibility/delegation decisions;
  the FU16 governance onboarding checkpoint alone is not enforcement.
- MOD-0021 executable consumer/delivery adapter for the governance-admitted producer fixture contract in §4.
- Executable MOD-0023 ProcessModelVersion source-attestation and ApprovalOutcome producer/adapter; the current
  source-profile amendment is DRAFT / NON-EXECUTABLE.
- Executable approval-policy provider, freshness/revalidation and SoD/eligibility evidence for the closed A-04
  owner decision.
- Runtime implementation evidence for the governance-accepted producer serialization, tenancy, failure-mode and
  compatibility fixture contracts.

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
- Service port reservation is `5017`; this pack grants no port binding, listener or activation authority.
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
- MOD-0021 executable delivery adapter, runtime event/publisher composition and publisher credentials remain
  second-slice work; the producer fixture and consumer admission checkpoints are governance-only evidence.
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
- Approval-policy mode, entitlement, permission, SoD, eligibility and outcome validation are separate gates.
- An authoritative negative entitlement/permission/SoD/eligibility or non-approved final outcome is `403`.
- Missing, deleted, cross-tenant or non-disclosable model/version/outcome is `404` only after the owning
  authenticated authority evaluates visibility; absence of an upstream profile is not `404`.
- A reference whose authoritative model/version/ContentHash binding is stale or conflicts with current publish
  state is `409` with no mutation.
- A missing MOD-0355 onboarding/profile/admission, unresolved trusted actor, governance-only provider,
  timeout, malformed authoritative response or indeterminate result is `503`, never local allow or deny.

## 14. Authorization Convention

The Minimal Core and existing Local API-Test permission inventory remains the exact sixteen-value closed set
below. The separate Performance & Improvement Contract-Test owner declaration adds exactly nine default-off
operation/permission pairs in §3; no host may accept them until the FU16 bilateral gate closes.

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

For the contract-test-only Approval Policy slice, the decision order is fail-closed and exact: authenticated
trusted identity; current MOD-0355 entitlement; exact publish permission; fresh effective/delegated actor;
authoritative approval-policy mode; SoD/eligibility; and, when required, authoritative final-approved outcome
binding. A later successful implementation must complete every gate before idempotent outcome disclosure or
business mutation. One allow cannot substitute for another. Missing MOD-0355 FU16 onboarding keeps the complete
decision unavailable (`503`), not denied, allowed or locally inferred.

## 15. Gateway / API Routing Decision

- Port: `5017` (canonical reservation only).
- No Gateway route is authorized.
- No browser-to-service route is authorized.
- No direct browser call to a future service port is permitted.
- Future frontend requests must pass through Gateway port `5000`.
- Any Ocelot change belongs only to `integration-agent` after separate explicit authorization.
- Production API route family, Gateway/controller exposure and transport topology remain B-class runtime
  decisions. The later Local API-Test amendment fixes only its loopback-only non-production routes and does
  not close this production/Gateway decision.
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

- [ ] A-04 policy branches match the closed direct-publish and approval-required decision in §4.
- [ ] `PublishActorProvenanceV1` has exactly eight fields; author is captured from the trusted effective actor
      at version creation, requester is the effective actor initiating publish, latest updater is excluded,
      and no client or audit value can supply or change the provenance.
- [ ] `PublishApprovalPolicyDecisionV1` consumes exactly the seven-field policy tuple in §4 and returns only
      the closed authority-state/requirement values with a monotonic version and fresh UTC validity interval.
- [ ] Exact policy tuple and version are revalidated before commit; changed or stale evidence returns `409`,
      while unavailable, malformed or indeterminate authority returns `503`, with no cache/LKG/local allow.
- [ ] `ApprovalOutcomeReferenceV1` has exactly `ContractName`, `ContractVersion` and
      `ApprovalOutcomeId`.
- [ ] `ApprovalOutcomeVersion` and additional outcome fields are rejected.
- [ ] Approval outcome is absent from Core entity/request schemas.
- [ ] Governance documentation does not claim runtime availability.
- [ ] Approval-not-required still requires current MOD-0355 entitlement, exact publish permission and fresh
      authoritative SoD/eligibility allow.
- [ ] Approval-required additionally resolves one final-approved outcome bound server-side to exact trusted
      TenantId, ProcessModelId, ProcessModelVersionId and ContentHash.
- [ ] Effective publisher differs from model author/requester and, when approval is required, approval decision
      actor; explicit equality denial is `403`.
- [ ] Governance-accepted MOD-0355 FU16 onboarding, the DRAFT / NON-EXECUTABLE MOD-0023 ProcessModelVersion
      profile and MOD-0021 bilateral admission are not treated as runtime-available; missing executable
      fixtures/providers/provisioning still returns fail-closed `503` where authority is required.
- [ ] `PublishProcessModelVersionCommand` contract, domain transition specification and fail-closed tests may
      exist in Core, but no successful application handler or persistence mutation is authorized until the
      Approval/Auth/Audit Runtime slice is separately approved and executable.
- [ ] MOD-0018, MOD-0021 and MOD-0023 executable integrations still require separate implementation authority,
      immutable runtime fixtures and operational evidence.
- [x] Producer-owned MOD-0355 audit event identity, exact schema, `20/34` allowlist matrix, projection,
      provenance and test-only HMAC fixtures are governance-available and executable only as fixture validation.
- [x] MOD-0021 governance admission binds the immutable MOD-0355 producer checkpoint exactly.
- [ ] MOD-0021 executable consumer/publisher composition, credentials, transport and live delivery remain
      separately authorized runtime gates.

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

The approved same-host local-test composition for MOD-0354 and MOD-0355 uses the single loopback `5017`
host while retaining separate authentication schemes and module-owned Domain, repository, Mongo collection,
permission and transaction boundaries. Shared Program, DI, response and validation composition may register
both modules additively; architecture evidence rejects direct sibling implementation dependency rather than
the mere presence of both internal modules in one service assembly or DI container.

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

### Approval Policy & Publish Authorization contract tests

- Exact eight-field `PublishActorProvenanceV1` serialization, required-field and unknown-field rejection.
- Server-owned provenance tests prove author capture at version creation, requester capture at publish request,
  latest-updater exclusion, immutable tenant/model/version/hash binding and rejection of every client-supplied
  provenance field.
- Exact seven-field `PublishApprovalPolicyDecisionV1` request binding and exact closed
  `AuthorityState`/`Requirement` parsing; aliases, missing fields and unknown values fail closed.
- Monotonic `PolicyVersion`, UTC observation/validity interval and pre-commit exact-tuple revalidation tests;
  changed/stale decisions return `409`, while unavailable/malformed/indeterminate authority returns `503`.
- Cache/LKG/default-not-required/local-policy mutation tests fail closed and produce no publish mutation.
- Exact approval-not-required and approval-required branch matrix.
- Entitlement, exact publish permission, SoD and eligibility are independent conjunctive gates.
- Publisher-versus-author/requester and publisher-versus-decision-actor negative cases.
- Exact three-field `ApprovalOutcomeReferenceV1` serialization and missing, duplicate, unknown-field,
  unsupported-version and noncanonical-UUID rejection.
- Trusted server-side TenantId, ProcessModelId, ProcessModelVersionId and ContentHash binding; no fourth
  reference field.
- Final-approved outcome acceptance shape; non-final or non-approved authoritative outcome denial.
- Fresh delegated actor proof requirement and request/effective-actor binding.
- Explicit deny `403`; missing/cross-tenant visible-resource boundary `404`; stale version/hash/lifecycle or
  idempotency conflict `409`; unavailable/timeout/malformed/indeterminate authority `503`.
- Missing upstream profile/fixture/runtime evidence proves no successful production/default handler,
  persistence mutation, receipt, audit intent, outbox record or external call. The sole non-production
  exception is the §19 `--local-test` path when the canonical policy result is `NotRequired` and every
  independent authorization/freshness/CAS gate passes.
- Core `68/68` checkpoint regression, exact sibling isolation matrix and protected-path scan remain green.

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
- [x] **A-04 — CLOSED FOR CONTRACT-TEST POLICY / NON-RUNTIME:** Authoritative `approval not required` permits
      direct-publish eligibility only with current MOD-0355 entitlement, exact publish permission and fresh
      authoritative SoD/eligibility allow. Approval-required additionally needs the exact three-field,
      final-approved MOD-0023 reference bound server-side to tenant, model, version and ContentHash. Publisher
      differs from author/requester and, when applicable, decision actor. Missing onboarding/profile/admission
      evidence keeps successful publish unauthorized and fail-closed `503` as defined in §4.
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
      Contract-Test slice; the later exact §19 amendment separately records the bounded Local API-Test
      authority and supersedes the earlier listener/endpoint prohibition only within that allowlist.

### B-class runtime blockers

- [x] **B-01:** Service port `5017` is canonically reserved without collision; historical Management
      Governance `5011` is superseded and `5011` remains exclusively assigned to `Diten.PvgService`.
      Reservation is not port binding, listener, route, runtime or production authority.
- [ ] **B-02:** Gateway route and transport topology.
- [x] **B-03a — GOVERNANCE CLOSED / NON-RUNTIME:** MOD-0355 FU16 onboarding accepts the existing Management
      Governance workload, exact ModuleCode, separate entitlement gate and exact 16-operation/16-permission
      manifest at `eefcbc63691c92fbbf4d41977df70d751ad8744b`.
- [ ] **B-03b:** Executable manifest fixtures, AuthService module/permission provisioning, entitlement/grant
      enforcement, credentials and production composition.
- [ ] **B-03c:** FU16 bilateral acceptance and executable fixtures for the additive nine-entry delta and exact
      twenty-five-entry MOD-0355 owner manifest.
- [ ] **B-04:** Reusable MOD-0018 permission/SoD enforcement, including fresh delegated-actor and actor-inequality
      decisions for MOD-0355.
- [x] **B-05a — GOVERNANCE PROFILE DEFINED / NON-EXECUTABLE:** Exact ProcessModelVersion source profile,
      pinned-template semantics and trusted source-attestation family are defined at
      `10b679baf3f9de83cf7069b9e5f8fb22a72c240e`; the amendment remains DRAFT / NON-EXECUTABLE.
- [ ] **B-05b:** Immutable bilateral source/attestation/outcome fixtures, production template mapping,
      provider/adapter implementation, signing/JWKS provisioning and executable ApprovalOutcome binding.
- [ ] **B-06:** Executable approval-policy resolution, eligibility/delegation and bilateral fixtures for both
      A-04 branches.
- [x] **B-07a:** Producer-owned MOD-0355 event identity/schema/allowlist/provenance/test-only signing fixture
      contract is closed as NON-RUNTIME governance evidence.
- [x] **B-07b — GOVERNANCE ADMISSION CLOSED / NON-RUNTIME:** Producer fixture checkpoint
      `3e69577e787ac7ccca149b4065e83eeb97fd5146` is immutable and MOD-0021 accepts it at
      `58b8b0e766314a86314a888ec865bb01ed5bb54e`.
- [ ] **B-07c:** Production signing/key provisioning, executable consumer/publisher composition, delivery,
      inbox/outbox atomicity, retry/DLQ/alarm/replay and live evidence.
- [ ] **B-07d:** Producer fixture checkpoint and MOD-0021 bilateral admission for the exact seven-positive /
      eleven-negative Performance & Improvement event profile.
- [ ] **B-08:** Classification producer contract.
- [x] **B-09a — BLUEPRINT OWNERSHIP CLOSED / DEPENDENCY-BLOCKED:** MOD-0355 owns `Process KPI Binding` and
      MOD-0059/MOD-0060 retain metric-definition, calculation and certified-value truth. Owner-side
      permission/operation/audit names are reserved in §3/§19, but no entity, collection, successful handler,
      route or provider contract is authorized.
- [ ] **B-09b:** Exact certified metric/calculation reference contract, binding fields/lifecycle,
      compatibility fixtures and authoritative provider adapter.
- [ ] **B-10:** Owner-reference producer contract and adapter.
- [ ] **B-11:** Interface-reference producer contract and adapter.
- [ ] **B-12:** Binary artifact/evidence storage and reference contracts.
- [ ] **B-13:** Frontend route, UX, localization and legacy-parity approval.
- [ ] **B-14:** Runtime credentials, observability, deployment and rollback evidence.
- [ ] **B-15:** Explicit runtime activation and production authority.
- [x] **B-16a — BLUEPRINT OWNERSHIP CLOSED / NON-EXECUTABLE:** MOD-0355 owns lightweight
      `Process Improvement Opportunity` and `Process Improvement Outcome` governance records without owning
      decisions, projects, tasks, experiments, evidence payloads or workflow execution.
- [x] **B-16b — LOCAL CONTRACT READY / DEFAULT-OFF:** Exact local opportunity/outcome fields, lifecycle,
      tenant/CAS rules, nine-entry additive owner operations/permissions, two collections and seven-pair
      producer audit mapping are closed in §4/§19 for bounded implementation under §5.
- [ ] **B-16c:** FU16 acceptance, producer fixture checkpoint, MOD-0021 admission and any exact
      decision/PPM/task/experiment/evidence bilateral reference contracts.

B-class items do not block Minimal Core v1 Core Model & Contract-Test `ready-for-dev`. They block only their
named later slice or runtime activation.

## 19. Implementation Notes

### Staged-slice contract

| Slice | Scope | Readiness relationship |
|---|---|---|
| Core Model & Contract-Test | Minimal unclassified definition model, lifecycle, revision, topology, tenancy, concurrency, hashing, idempotency, local audit-intent/outbox and architecture tests | Independent of external producer integrations |
| Approval Policy & Publish Authorization Contract-Test | Closed A-04 branch semantics, exact three-field reference, trusted binding, SoD/eligibility and fail-closed provider contracts | Non-runtime second-slice foundation; successful publish remains blocked |
| Local API-Test Runtime — NON-PRODUCTION / DEFAULT-OFF | Exact loopback-only HTTP contract, test-host identity, CQRS composition and transaction evidence | Bounded local evidence only; no deployment, Gateway or production authority |
| Approval/Auth/Audit Runtime | Permission/SoD runtime enforcement, executable outcome binding and MOD-0021 delivery | Separate later executable slice after runtime fixtures, providers, provisioning and explicit authority |
| Optional reference integrations | Classification, KPI/Metric, owner, interface, document, evidence and artifact references | Independent later slices |
| Frontend & Legacy Parity | Tenant UI, localization, parity inventory and approved disposition | Separate later slice |
| Runtime activation | Port, Gateway, transport, credentials, deployment, observability and production authorization | Separate final gate |

### Local API-Test Runtime amendment — NON-PRODUCTION / DEFAULT-OFF

The Control Tower authorizes a bounded, executable local-test surface for MOD-0355. This amendment does not
change `production_authority: none` and does not authorize deployment or production activation. The process
starts no listener unless the exact `--local-test` switch is present. In that mode only, it may bind
`http://127.0.0.1:5017`; wildcard, non-loopback and implicit port binding are forbidden.

The exact implementation allowlist is limited to:

- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Api/Program.cs`, the API
  project file and `Controllers/CustomBaseController.cs` plus
  `Controllers/ProcessModelingLocalTestController.cs`;
- the Application project file, `DependencyInjection.cs`, `Behaviors/**` and
  `Features/ProcessModelingLocalTest/**`;
- the existing ProcessModeling Domain, Persistence and Infrastructure paths, their project files and
  dependency-injection files;
- existing ProcessModeling unit/architecture/integration paths plus additive
  `Modules/ProcessModeling/LocalTest/**` tests and mutation evidence.

The API allowlist additionally names exactly
`services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Api/LocalTestSecurity/LocalTestAuthenticationHandler.cs`.
This handler accepts only a test-host-installed ephemeral identity when `--local-test` is present. It cannot be
selected by configuration, request headers or production composition. The existing architecture test that
forbids every `WebApplication`/`Run` call must be reconciled to the narrower invariant: no host or listener
without `--local-test`; with the switch, exact loopback `127.0.0.1:5017` only.

The exact scheme is `Diten.MOD0355.LocalTest`. Its identity source is an in-memory
`ILocalTestIdentitySource` nested in the same allowlisted file. Default DI has no source; only the test-host
composition installs one before host construction. HTTP headers, query/body values, configuration and
environment variables cannot select or alter an identity. A resolved identity contains exactly these claims:
`sub` = non-empty actor GUID, `tenant_id` = non-empty tenant GUID, `client_id` = exact
`diten.management-governance.local-test`, `module_code` = exact `MOD-0355`, `test_identity` = exact
`mod0355-local-evidence-v1`, and one or more ordinal `permission` values drawn only from the exact sixteen-value
manifest. Duplicate permission claims, aliases, wildcards, case/trim drift and extra authority claims are
rejected. `Program.cs` never registers `ILocalTestIdentitySource`. Spawned-process smoke is limited to the
anonymous health route plus authenticated-command rejection. Successful command HTTP smoke runs only through
an in-process test host that installs an explicit per-case `ILocalTestIdentitySource` before host construction.
No process-host identity source exists. Production/default composition does not register the scheme or source
and therefore cannot authenticate this identity.

The persistence allowlist additionally names exactly
`services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Persistence/Modules/ProcessModeling/ProcessModelingMongoGuidPolicy.cs`
and the existing `ProcessModelingMongoPersistence.cs` plus its existing ProcessModeling integration tests.
`ProcessModelingMongoGuidPolicy.Canonical` is the single authoritative value and is exact
`GuidRepresentation.Standard`. The ProcessModeling client settings, every concrete member serializer, every
raw BSON UUID write and every rendered tenant/identity/receipt predicate use that value. Receipt
reconciliation decodes subtype-4 values with the same policy; `CSharpLegacy`, Java/Python legacy, dual-read,
identity fallback and client-wide implicit conversion are forbidden. Evidence must prove raw subtype `4`,
exact filter bytes, same-client/session ownership and that replacing the policy with `CSharpLegacy` is killed
by an executable test.
The exact new evidence files are
`services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.IntegrationTests/Modules/ProcessModeling/ProcessModelingMongoGuidPolicyTests.cs`
and
`services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.ArchitectureTests/Modules/ProcessModeling/LocalTest/ProcessModelingMongoGuidPolicyArchitectureTests.cs`.
They assert the production-composed client policy, RFC 4122 Standard raw bytes/subtype `4`, rendered
tenant/identity/receipt filter bytes, Standard receipt decoding, wrong-client/session rejection and the
expected-red `Standard → CSharpLegacy` mutation. Dual-read or migration behavior is not authorized.

The AuthService manifest provenance is also closed without reinterpretation. The immutable MOD-0355 manifest
payload uses `ManifestVersion = 7e8fe7d45ce80cb09f28c0089415d4f40400bc37` and exact registration provenance
`MOD-0355 checkpoint 7e8fe7d45ce80cb09f28c0089415d4f40400bc37`; this is the pack checkpoint whose exact
sixteen operations and permissions the executable manifest materialized. FU16 governance onboarding remains
the separate checkpoint `eefcbc63691c92fbbf4d41977df70d751ad8744b`, and executable Auth enforcement evidence
remains `fb2745ea0cfcf3e918127d6e11ff725d7f4f225c`. These three identities have different roles and cannot be
substituted, collapsed or synthesized.
Executable evidence reads
`fb2745ea0cfcf3e918127d6e11ff725d7f4f225c:services/Diten.AuthService/src/Diten.AuthService.Application/S2S/PermissionCatalogManifestV1.cs`
and proves the exact MOD-0355 `ManifestVersion`, registration provenance and `16/16` map above. It separately
proves the roles of `eefcbc63691c92fbbf4d41977df70d751ad8744b` and
`fb2745ea0cfcf3e918127d6e11ff725d7f4f225c`; the Local API-Test slice changes no AuthService source.

#### Exact local-only HTTP contract

The local surface is explicit and closed. `GET /internal/local-test/v1/process-modeling/health` is the only
anonymous action and returns no database, credential or topology detail. All command routes require the
test-only authentication scheme, exact `tenant_id` claim agreement with the canonical `X-Tenant-Id` header,
the mapped permission and a non-empty
ordinal `Idempotency-Key`. `TenantId`, actor, permission, request hash and command identity are forbidden body
fields. Every JSON body rejects duplicate, case-changed, unknown and extra properties.

| Command | Method and exact path | Exact body fields |
|---|---|---|
| `CreateProcessArchitectureCommand` | `POST /internal/local-test/v1/process-modeling/architectures` | `architectureCode`, `name`, optional `description`, `sortOrder` |
| `UpdateProcessArchitectureCommand` | `PUT /internal/local-test/v1/process-modeling/architectures/{id}` | `name`, optional `description`, `sortOrder`, `expectedVersion` |
| `ArchiveProcessArchitectureCommand` | `POST /internal/local-test/v1/process-modeling/architectures/{id}/archive` | `expectedVersion` |
| `CreateProcessDomainCommand` | `POST /internal/local-test/v1/process-modeling/domains` | `processArchitectureId`, `domainCode`, `name`, optional `description`, `sortOrder` |
| `UpdateProcessDomainCommand` | `PUT /internal/local-test/v1/process-modeling/domains/{id}` | `name`, optional `description`, `sortOrder`, `expectedVersion` |
| `ArchiveProcessDomainCommand` | `POST /internal/local-test/v1/process-modeling/domains/{id}/archive` | `expectedVersion` |
| `CreateProcessFamilyCommand` | `POST /internal/local-test/v1/process-modeling/families` | `processDomainId`, `familyCode`, `name`, optional `description`, `sortOrder` |
| `UpdateProcessFamilyCommand` | `PUT /internal/local-test/v1/process-modeling/families/{id}` | `name`, optional `description`, `sortOrder`, `expectedVersion` |
| `ArchiveProcessFamilyCommand` | `POST /internal/local-test/v1/process-modeling/families/{id}/archive` | `expectedVersion` |
| `CreateProcessDefinitionCommand` | `POST /internal/local-test/v1/process-modeling/definitions` | `processFamilyId`, `processCode`, `name`, optional `purpose`, optional `description` |
| `UpdateProcessDefinitionCommand` | `PUT /internal/local-test/v1/process-modeling/definitions/{id}` | `name`, optional `purpose`, optional `description`, `expectedVersion` |
| `ArchiveProcessDefinitionCommand` | `POST /internal/local-test/v1/process-modeling/definitions/{id}/archive` | `expectedVersion` |
| `CreateProcessModelCommand` | `POST /internal/local-test/v1/process-modeling/models` | `processDefinitionId`, `modelCode`, `name`, optional `description` |
| `UpdateProcessModelCommand` | `PUT /internal/local-test/v1/process-modeling/models/{id}` | `name`, optional `description`, `expectedVersion` |
| `UpdateDraftProcessModelVersionCommand` | `PUT /internal/local-test/v1/process-modeling/model-versions/{id}/draft-content` | `title`, optional `description`, `activities`, `controlPoints`, `relationships`, `expectedVersion` |
| `RequestProcessModelReviewCommand` | `POST /internal/local-test/v1/process-modeling/model-versions/{id}/request-review` | `expectedVersion` |
| `ReturnProcessModelToDraftCommand` | `POST /internal/local-test/v1/process-modeling/model-versions/{id}/return-to-draft` | `expectedVersion` |
| `PublishProcessModelVersionCommand` | `POST /internal/local-test/v1/process-modeling/model-versions/{id}/publish` | `expectedVersion` |
| `RetireProcessModelVersionCommand` | `POST /internal/local-test/v1/process-modeling/model-versions/{id}/retire` | `expectedVersion` |
| `CreateProcessModelRevisionCommand` | `POST /internal/local-test/v1/process-modeling/models/{id}/revisions` | `title`, optional `description`, `expectedVersion` |

Create IDs are server-generated and returned through `Response<Guid>` with `201`. Update/archive/lifecycle
handlers return `Response<NoContent>`; `CustomBaseController` emits HTTP `204` with an intentionally empty
wire body, as required by HTTP semantics. No response envelope body is asserted for `204`. `expectedVersion`
is a non-negative technical fence. Codes,
names, lengths, sort order and graph child shapes are exactly the §4 domain contract; API DTOs cannot widen or
reinterpret them. Activity, control-point and relationship items use only their exact §4 fields and never
accept tenant, actor, lifecycle, approval, task, route or execution data.

The three draft graph item shapes are exact and exclude `ProcessModelVersionId`, every entity-base/audit field
and lifecycle/approval data. The route `{id}` is injected server-side as `ProcessModelVersionId`:

- activity item: `logicalActivityId`, `activityCode`, `name`, optional `description`, `sortOrder`;
- control-point item: `logicalControlPointId`, `controlCode`, `name`, optional `description`, optional
  `logicalActivityId`, `sortOrder`;
- relationship item: `fromActivityId`, `toActivityId`, optional `conditionLabel`, `sortOrder`.

The canonical mutation/request-binding hash is lowercase hexadecimal SHA-256. ASCII
`DITEN-MOD0355-LOCAL-COMMAND-V1` is emitted verbatim first and has no length prefix. Exactly seven components
follow in this order: UTF-8 method, exact resolved path (route GUID formatted lowercase `D`), exact command
name, tenant GUID lowercase `D`, actor GUID lowercase `D`, exact permission and the unmodified raw request-body
bytes. Each of these seven components, and only these seven, is emitted as its unsigned four-byte big-endian
byte length followed by its exact bytes; no delimiter, whitespace normalization, JSON reserialization or
property reordering occurs. Thus route ID and `expectedVersion` are bound, while the server-derived fields
cannot be overridden. Same idempotency scope with any byte/provenance change is `409`.

The normative vector is exact: method `POST`; path
`/internal/local-test/v1/process-modeling/architectures`; command `CreateProcessArchitectureCommand`; tenant
`11111111-1111-4111-8111-111111111111`; actor `22222222-2222-4222-8222-222222222222`; permission
`management-governance.process-modeling.architectures.create`; and raw body
`{"architectureCode":"EA","name":"Enterprise Architecture","sortOrder":1}`. The seven component byte lengths
are `4, 54, 32, 36, 36, 59, 72`; the complete framed input is `351` bytes and its exact SHA-256 is
`6bba6bff9291532f124c3b5087521b7e3ceb3c3725467236fe22f1e0c7f428f4`.

Command identity is derived only from the matched action and is mapped through
`ProcessModelingPermissions.ExactCommandMap`; it is not accepted from route, body or header. The persistence
dispatcher is closed by command: architecture/domain/family/definition/model commands write their exact
manifest collection; model-version commands write `mg_process_model_versions` and, where required, the
same-transaction model pointer; draft graph replacement writes the three child collections in the same
transaction. Parent/child reads are tenant-first and `IsDeleted=false`. Archive changes only business
`LifecycleState`; it never changes technical deletion fields.

`Idempotency-Key` header-name matching follows HTTP's case-insensitive header-name rules. Ordinal exactness
applies only to the non-empty header value and its persisted scope; the value is never trimmed, normalized,
case-folded or reused with different request/provenance bytes.

The surface uses MediatR and the exact four pipeline behaviors (validation, logging, exception and
performance), `Response<T>` and `CustomBaseController`. It exposes exactly the twenty command identities and
sixteen permissions in `ProcessModelingPermissions`; aliases, wildcard permissions and case/trim
normalization are rejected. Tenant, actor, permission and request-binding context are server-owned. The
local-test composition uses a test-owned, environment-gated identity only; default and production
composition reject that identity and remain fail-closed. No production key, secret, PEM, JWK, JWKS endpoint,
Auth endpoint or fallback user/session JWT behavior is introduced.

Every effective mutation uses the existing four-participant Mongo transaction: business state,
idempotency receipt, audit intent and outbox. Same key/same canonical payload returns the durable result;
same key/different payload returns `409`; unknown commit uses commit-only retry and receipt reconciliation,
never a blind body retry. Cross-tenant and soft-deleted targets return non-disclosing `404`. Cancellation is
propagated. Standalone Mongo and provider uncertainty fail closed with `503` and zero residue.

The test-only direct-publish path is permitted only when the authoritative approval-policy result is
`NotRequired`, current MOD-0355 entitlement is `Active`, the exact publish permission and explicit tenant grant
are present, a request-bound FU16 proof resolves the effective actor, the actor differs from the immutable
author and requester, the authoritative SoD/eligibility decision allows, and the initial policy tuple/version
is revalidated unchanged immediately before commit. Any changed tuple, content hash, lifecycle/version fence,
policy version or requirement returns `409` before the four-participant write. Explicit denial returns `403`;
unavailable, malformed or indeterminate authority returns `503`. The
`Required` path remains unavailable until the MOD-0023 runtime provider exists and must return exact
`503` with zero business, receipt, audit-intent and outbox residue. No local approval truth or fallback is
allowed.
The local reconciliation may update only the existing
`services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/Modules/ProcessModeling/Approval/PublishApprovalAuthorizationContracts.cs`
and its existing exact unit/integration/architecture Approval test roots, or add a local-only evaluator under
the already allowlisted `Features/ProcessModelingLocalTest/**`. The default approval contract remains
non-executable and `503`; local success cannot be inferred from `ContractGatesSatisfied` alone. `Required`
never invokes or accepts MOD-0023 in this slice. Tests must independently kill entitlement, explicit-grant,
permission, FU16 actor, author/requester inequality, SoD/eligibility, policy revalidation and pre-commit CAS
bypasses.

For every successful local-test mutation the producer-local audit intent and outbox use the one authoritative
event identity `management-governance.process-modeling-audit-intent.submitted.v1`, version integer `1`, and
the exact §4 producer fixture mapping: the twenty command identities map one-to-one to the twenty approved
entity/mutation pairs. The canonical six-field payload order is `auditIntentId`, `actorId`, `entityType`,
`entityId`, `mutation`, `occurredAtUtc`; trusted tenant, FU16 provenance, permission and signing identity remain
transport metadata and never enter those six payload fields. `CommandFamily + ".accepted"`, aliases and local
event-name invention are forbidden. The outbox is persisted in the same transaction; publication, if later
activated, is post-commit only through the existing MOD-0035 boundary. No direct broker publish is permitted.

| Command | Exact audit entity | Exact mutation | Exact entity ID source |
|---|---|---|---|
| `CreateProcessArchitectureCommand` | `ProcessArchitecture` | `Created` | server-generated created ID |
| `UpdateProcessArchitectureCommand` | `ProcessArchitecture` | `Updated` | route target ID |
| `ArchiveProcessArchitectureCommand` | `ProcessArchitecture` | `Archived` | route target ID |
| `CreateProcessDomainCommand` | `ProcessDomain` | `Created` | server-generated created ID |
| `UpdateProcessDomainCommand` | `ProcessDomain` | `Updated` | route target ID |
| `ArchiveProcessDomainCommand` | `ProcessDomain` | `Archived` | route target ID |
| `CreateProcessFamilyCommand` | `ProcessFamily` | `Created` | server-generated created ID |
| `UpdateProcessFamilyCommand` | `ProcessFamily` | `Updated` | route target ID |
| `ArchiveProcessFamilyCommand` | `ProcessFamily` | `Archived` | route target ID |
| `CreateProcessDefinitionCommand` | `ProcessDefinition` | `Created` | server-generated created ID |
| `UpdateProcessDefinitionCommand` | `ProcessDefinition` | `Updated` | route target ID |
| `ArchiveProcessDefinitionCommand` | `ProcessDefinition` | `Archived` | route target ID |
| `CreateProcessModelCommand` | `ProcessModel` | `Created` | server-generated created ID |
| `UpdateProcessModelCommand` | `ProcessModel` | `Updated` | route target ID |
| `UpdateDraftProcessModelVersionCommand` | `ProcessModelVersion` | `DraftUpdated` | route target ID |
| `RequestProcessModelReviewCommand` | `ProcessModelVersion` | `ReviewRequested` | route target ID |
| `ReturnProcessModelToDraftCommand` | `ProcessModelVersion` | `ReturnedToDraft` | route target ID |
| `PublishProcessModelVersionCommand` | `ProcessModelVersion` | `Published` | route target ID |
| `RetireProcessModelVersionCommand` | `ProcessModelVersion` | `Retired` | route target ID |
| `CreateProcessModelRevisionCommand` | `ProcessModelVersion` | `RevisionCreated` | new server-generated version ID, never the route model ID |

Each command emits exactly one audit intent/outbox event; model-pointer and graph-child participants emit no
additional event. The exact additive audit source is
`services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/Features/ProcessModelingLocalTest/ProcessModelingLocalAuditContractV1.cs`;
the persistence source is the existing `ProcessModelingMongoPersistence.cs`. Exact evidence files are
`tests/Diten.ManagementGovernanceService.Tests/Modules/ProcessModeling/LocalTest/ProcessModelingLocalAuditContractTests.cs`,
`tests/Diten.ManagementGovernanceService.IntegrationTests/Modules/ProcessModeling/LocalTest/ProcessModelingLocalAuditMongoTests.cs`
and
`tests/Diten.ManagementGovernanceService.ArchitectureTests/Modules/ProcessModeling/LocalTest/ProcessModelingLocalAuditArchitectureTests.cs`
under the service root. They read the producer fixture from its immutable checkpoint without copying it and
assert the exact event identity, integer version, six-field order, provenance exclusion, `20/34` matrix and
rejection of `CommandFamily + ".accepted"`.

Required evidence includes exact command/permission mapping, default-off and loopback-only composition,
health and command HTTP smoke, tenant/auth/permission/idempotency/error matrices, real replica-set
transaction rollback/concurrency/unknown-commit tests, audit/outbox exact-one behavior, production rejection
of the local identity, mutation expected-red runs with byte-identical restore, and full relevant
ManagementGovernance/Auth/Platform Eventing regressions. Gateway, frontend, WorkCenter, live broker,
deployment, appsettings, launch profiles and legacy mutation remain outside this amendment.

### Blueprint 8.1 performance and improvement ownership amendment — NON-PRODUCTION / NON-EXECUTABLE / DEFAULT-OFF

Canonical Master 8.1 evidence is binding and keeps this scope inside `MOD-0355`:

- `Blueprint_Data` row 355 assigns process KPI bindings and lightweight improvement
  opportunities/outcomes to Business Process Architecture & Modeling.
- `Module Pages` rows 3398–3400 define `Process KPI Binding`, `Improvement Opportunity Register` and
  `Improvement Detail & Outcome Review`.
- `Enterprise Object Catalog` rows 123–125 assign `ProcessKPIBindingID`,
  `ProcessImprovementOpportunityID` and `ProcessImprovementOutcomeID` to MOD-0355.

No new MOD, FU or candidate identity may be minted for these records. The bounded ownership semantics are:

- `Process KPI Binding` links an exact MOD-0355 process-model version to external certified metric and
  calculation-contract references. It never owns metric definition, formula, calculation execution,
  certified value, scorecard or dashboard truth owned by MOD-0059/MOD-0060 and MOD-0061.
- `Process Improvement Opportunity` is a lightweight process-governance record that may later reference
  authoritative decisions and execution objects. It does not create or execute a project, task, experiment,
  workflow run or approval.
- `Process Improvement Outcome` is a lightweight realized-outcome record that may later reference evidence and
  authoritative decision/execution records. It does not copy evidence payloads or become decision, project,
  task, workflow, metric or audit truth.

Master 8.1 does not itself define the exact field set, lifecycle, operation names, permission keys, producer
contract names/versions or audit entity/mutation mapping. The owner decisions immediately below close only
the locally owned Improvement core and additive protocol names. They do not invent the absent external
reference contracts. Reusing a Core permission or operation by analogy remains forbidden.

### Performance & Improvement Contract-Test executable amendment — NON-PRODUCTION / DEFAULT-OFF

This amendment authorizes bounded code only under the exact §5 allowlist. It creates no host-reachable API,
DI registration, listener, worker or production activation.

#### Exact command/query mapping

| Contract | Exact operation / permission | Availability |
|---|---|---|
| KPI binding queries | `management-governance.process-modeling.kpi-bindings.read` | `503` until exact MOD-0059/MOD-0060 reference contract |
| `CreateProcessKpiBindingCommand` | `management-governance.process-modeling.kpi-bindings.create` | `503` until exact MOD-0059/MOD-0060 reference contract |
| `ArchiveProcessKpiBindingCommand` | `management-governance.process-modeling.kpi-bindings.archive` | `503` until exact MOD-0059/MOD-0060 reference contract |
| Opportunity/outcome queries | `management-governance.process-modeling.improvements.read` | Bounded contract/persistence tests only |
| `CreateProcessImprovementOpportunityCommand` | `management-governance.process-modeling.improvements.create` | Bounded contract/persistence tests only |
| `UpdateProcessImprovementOpportunityCommand` | `management-governance.process-modeling.improvements.update` | Bounded contract/persistence tests only |
| `QualifyProcessImprovementOpportunityCommand` | `management-governance.process-modeling.improvements.qualify` | Bounded contract/persistence tests only |
| `CloseProcessImprovementOpportunityCommand` | `management-governance.process-modeling.improvements.close` | Bounded contract/persistence tests only |
| `RecordProcessImprovementOutcomeCommand` | `management-governance.process-modeling.improvements.record-outcome` | Bounded contract/persistence tests only |

The implementation persists only `mg_process_improvement_opportunities` and
`mg_process_improvement_outcomes`; a KPI-binding collection is forbidden while its mandatory reference
contract is unresolved. Every effective improvement mutation uses the existing four-participant invariant:
business mutation, idempotency receipt, producer-local audit intent and producer-local outbox in one
replica-set transaction. Tenant-first predicates, `IsDeleted = false`, expected `EntityBase.Version`, body-once
transaction execution, commit-only retry, receipt reconciliation, cancellation propagation and standalone
Mongo `503` behavior are mandatory. No-op and every failure produce zero new participant residue.

#### Additive FU16 impact — OWNER DECLARATION ONLY

Checkpoint `eefcbc63691c92fbbf4d41977df70d751ad8744b` remains the immutable accepted sixteen-entry baseline.
The exact nine-entry delta in §3 changes the prospective MOD-0355 owner manifest to twenty-five ordinal
operation/permission pairs without changing ModuleCode, workload, audience, tenant entitlement, explicit
grant or delegated-proof semantics. It does not modify AuthService and is not accepted by FU16 in this
checkpoint. A separate FU16 bilateral amendment must prove exact 25/25 content, collision freedom, explicit
grant/dormant-entitlement behavior, request binding, tenant/actor/freshness enforcement and cross-owner
negative fixtures. Until then no new operation can be authorized and every host-level attempt returns `503`.

#### Additive audit impact — PRODUCER FIXTURE ONLY

The immutable `ProcessModelingAuditIntentSubmittedV1` `20/34` profile and MOD-0021 admission at
`58b8b0e766314a86314a888ec865bb01ed5bb54e` remain byte-for-byte unchanged. New mutations use a separate
producer-owned profile so the admitted v1 allowlist is never widened implicitly:

- Event type: `ProcessPerformanceImprovementAuditIntentSubmittedV1`
- Event name: `management-governance.process-performance-improvement-audit-intent.submitted.v1`
- Event version: JSON integer `1`
- Schema identity:
  `urn:diten:management-governance:process-performance-improvement-audit-intent-submitted:v1`
- Producer: `Diten.ManagementGovernanceService`
- ModuleCode: `MOD-0355`
- Audit category: `AuditCategory.Integration`

The canonical payload retains the exact six-field order
`auditIntentId`, `actorId`, `entityType`, `entityId`, `mutation`, `occurredAtUtc`. The closed mapping is:

| Entity type | Exact mutation | MOD-0021 projection operation |
|---|---|---|
| `ProcessKpiBinding` | `Created` | `Create` |
| `ProcessKpiBinding` | `Archived` | `Deactivate` |
| `ProcessImprovementOpportunity` | `Created` | `Create` |
| `ProcessImprovementOpportunity` | `Updated` | `Update` |
| `ProcessImprovementOpportunity` | `Qualified` | `LifecycleTransition` |
| `ProcessImprovementOpportunity` | `Closed` | `LifecycleTransition` |
| `ProcessImprovementOutcome` | `Recorded` | `Create` |

This is exactly seven positive pairs; the remaining eleven pairs in the 3-by-6 Cartesian matrix are
terminal negatives. The producer fixture may be authored under the exact §5 root with test-only signing
material and production-rejection evidence. No publisher, production signing identity/key, consumer adapter,
broker or delivery is authorized. MOD-0021 must separately admit the exact producer checkpoint before any
delivery; until then the local outbox is durable default-off evidence only.

#### Dependency and activation result

- Improvement Opportunity/Outcome Domain/Application/Persistence and owner fixtures are implementation-
  authorized only inside §5 and cannot be composed into a host.
- KPI Binding successful persistence is not authorized; missing exact MOD-0059/MOD-0060 contracts returns
  `503` with zero residue.
- Decision, PPM/project, task, experiment and evidence linking is not authorized; unexpected members return
  `400` and no external provider is invoked.
- FU16 authorization success and MOD-0021 delivery are not runtime-available until their separate bilateral
  amendments close.
- Production authority, migration, frontend, Gateway and WorkCenter remain absent.

Missing or indeterminate authority is `503`; malformed/unsupported contract is `400`; cross-tenant or
non-disclosable resource is `404`; stale version/reference conflict is `409`. Cache, last-known-good,
free-text reference, copied upstream payload and local fallback truth are forbidden.

#### Exact contract-test and mutation gates

The bounded implementation is not a PASS until all of these executable gates pass:

- exact field serialization and unknown-member rejection for Opportunity and Outcome;
- `Open → Qualified → Closed`, terminal Closed, qualify prerequisites and outcome-before-close;
- immutable Outcome and server-owned `ObservedAtUtc`;
- same-tenant Published ProcessModelVersion create fence, cross-tenant non-disclosure and soft-delete guard;
- expected-version CAS, duplicate idempotency and no-op behavior;
- exact two-collection/two-index snapshots and explicit proof that no KPI-binding collection exists;
- four-participant real replica-set commit plus participant-by-participant rollback, concurrent CAS,
  cancellation residue, standalone fail-closed and unknown-commit body-once reconciliation;
- exact nine-entry owner permission/operation fixture and proof that the currently accepted FU16 sixteen-entry
  manifest rejects every new operation;
- exact seven-positive/eleven-negative producer audit fixture, schema/checksum/provenance/production-reject
  evidence and proof that current MOD-0021 v1 admission rejects the new event identity;
- architecture proof of zero controller, route, root DI, listener, worker, direct broker, frontend, Gateway and
  WorkCenter reachability;
- full existing ProcessModeling and shared-host DWS regression.

At minimum, physical non-equivalent mutants must remove: tenant predicate; Published-version create fence;
Opportunity CAS; qualify ExpectedOutcome guard; qualify PriorityRank guard; outcome-before-close guard;
Outcome immutability; business transaction participant; receipt participant; audit-intent participant; outbox
participant; FU16-new-operation rejection; audit invalid-pair rejection; and KPI dependency `503`. Every
mutant must compile, break its targeted test for the expected invariant and restore byte-identically.

### Core transaction boundary

Every successful Core mutation has exactly four atomic local Mongo transaction participants:

1. business mutation;
2. idempotency receipt;
3. producer-local audit intent;
4. producer-local outbox.

Local audit intent and outbox are Core durability infrastructure. They do not establish MOD-0021 runtime
availability. The producer event identity and compatibility fixtures are governance-available only; MOD-0021
admission, publication adapter, production signing/key provisioning, retry, dead-letter, alarm and replay
behavior remain in the Approval/Auth/Audit runtime slice.

### Archive and retention

Architecture, Domain, Family and Definition use only `Active → Archived`. Archive never writes technical
deletion fields and never cascades.

ProcessModel is a durable non-archivable identity. Published and Retired version history, activities,
control points, relationships, canonical content and hash are immutable and retained for tenant/service
lifetime.

Receipts, local audit intents and local outbox records have no TTL or purge under this pack. Future bounded
retention, tenant offboarding, legal erasure or exceptional purge requires separate authority.

### Approval boundary

`ApprovalOutcomeReferenceV1` is documented only for the contract-test second slice. It is absent from Core entity and
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
- Materialize the closed A-04 policy only through the exact contract-test scope in §5; keep successful publish
  disabled until upstream bilateral amendments are executable.
- Implement only the exact module-local `PublishActorProvenanceV1` and
  `PublishApprovalPolicyDecisionV1` contracts in that contract-test slice; do not infer an upstream provider,
  transport or runtime identity.
- Implement MOD-0018 permission/SoD enforcement only through a separately approved PSS executable slice; the
  governance onboarding checkpoint is not provisioning or runtime evidence.
- Preserve the closed MOD-0021 governance admission and add executable delivery only after production signing,
  transport and operational evidence exist.
- Implement only the allowlisted local Improvement Opportunity/Outcome contract/persistence evidence while
  keeping host composition default-off. KPI Binding stays `503` until MOD-0059/MOD-0060 exact reference
  contracts exist; all nine new operations stay host-unavailable until FU16 acceptance; audit delivery stays
  absent until the new producer fixture and MOD-0021 admission close.
- Add classification, owner, interface, document, evidence and artifact references through separately
  approved producer contracts.
- Define and approve tenant frontend and seven-language resources.
- Complete the read-only legacy parity inventory and owner-approved disposition.
- Allocate service port and Gateway route only during separately authorized runtime activation.
- Define any process-instance/runtime engine as a separate canonical capability.
- Preserve the bounded Core implementation authority recorded in frontmatter and §5. Runtime activation,
  deployment and production authority remain absent until separately recorded.

### Mechanical self-check

- Exact numbered headings: `20`
- Heading sequence: `## 1.` through `## 20.`
- Exact unique permissions: Core/Local API-Test `16`; owner-declared total `25`; FU16-accepted runtime `16`
- Blueprint amendment owned objects: exact `3`; bounded local implementation `2`; dependency-blocked `1`
- Markdown fences: balanced
- ActivityKind occurrence: deferred/future-amendment explanation only
- ControlKind occurrence: deferred/future-amendment explanation only
- RelationshipKind occurrence: deferred/future-amendment explanation only
- Prohibited ProcessModel archive command identifier occurrence: `0`
- ProcessModel catalog lifecycle field occurrence: `0`
- Additional concurrency-field identifier occurrence: `0`
- ApprovalOutcomeReference in Core schema: absent
- PublishActorProvenanceV1 fields: exact `8`
- PublishApprovalPolicyDecisionV1 request fields: exact `7`
- PublishApprovalPolicyDecisionV1 authority states: exact `4`
- PublishApprovalPolicyDecisionV1 requirements: exact `2`
- Error matrix: exact `400 / 401 / 403 / 404 / 409 / 503`
- Core atomic participants: `4`
- Normative hash vectors: `3`
- Audit fixture identity: exact producer-owned `ProcessModelingAuditIntentSubmittedV1` / version integer `1`
- Audit fixture allowlist: exact `20` positive and `34` terminal invalid cross-pairs
- Audit fixture provenance: delegated/non-delegated structural profiles; producer payload/trusted metadata split
- Audit fixture production authority: `none`; production signing key/KeyId provisioned: `0`
- Non-empty normative graph vectors: `1`
- Vector recomputations: `PASS / PASS / PASS`
- Status: `ready-for-dev`
- Implementation authority: `explicit-user-control-tower-bounded-core-local-api-test-and-performance-improvement-contract-test`
- Production authority: `none`
- Initial materialization scope: exact-one MOD-0355 module-pack file
- Ready-for-dev promotion reconciliation scope: exact four governance files (this pack, DCP-005, DCP-006
  and the module registry)
- Bounded implementation scope: exact §5 Core paths plus the exact §19 Local API-Test allowlist; no other later slice
