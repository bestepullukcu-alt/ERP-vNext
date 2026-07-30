---
id: MOD-0117
name: Project & Portfolio Management (PPM)
domain: portfolio-delivery
service: Diten.PpmService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: review
owner: Portfolio Governance Process Owner / enterprise-architect
branch: feature/es/enterprise-strategy
started: 2026-07-29
target: phase-2a
form_field_count: 7
implementation_authorization: phase-2a
implementation_authorized_on: 2026-07-29
implementation_authority: explicit-user-control-tower
---

# MOD-0117 — Project & Portfolio Management (PPM)

> **SCOPED IMPLEMENTATION AUTHORITY — 2026-07-29:** The user explicitly approved `Diten.PpmService`
> scaffolding and MOD-0117 backend plus tenant frontend implementation for **Phase 2A — Context &
> Referenceability Core**. The pack is now in `review` on the strength of scoped Phase 2A
> implementation evidence; this does not authorize Phase 2B, the authoritative
> ExternalContextReference provider endpoint/transport, DWS runtime integration, any Gateway change beyond
> the scoped PPM mappings, legacy migration or any WorkCenter hazard. Those boundaries remain fail-closed.

> **Composite-pack UI decision:** `form_field_count: 7` is the maximum create/edit field count across the
> four authorized Phase 2A surfaces, not a single composite form. All four select Golden Slim.

## 1. Module Summary

MOD-0117 is the Blueprint-canonical PPM parent and the future authoritative system of record for Portfolio,
Initiative, Program, Project and their controlled investment/value linkage. The permanent institutional
business owner is **Portfolio Governance Process Owner (PPM Business Owner)**. The Enterprise Architect owns
technical/governance consistency only and does not replace PPM business acceptance.

The authorized production placement is `services/Diten.PpmService/`; the Phase 2A scaffold and vertical
slice now exist and are under review. The current
Enterprise Strategy mock/resilient adapters, browser-held state, Delivery Execution frontend prototypes and
gateway routes targeting legacy ES port `5004` are code-reality evidence only. They are not a production
baseline, capability completion proof, data migration source of truth or implementation authority.

Delivery is divided into two internal phases:

- **Phase 2A — Context & Referenceability Core (authorized):** service scaffold; Portfolio, Initiative,
  Program and Project backend plus tenant UI; Mongo persistence; tenant isolation; soft delete; concurrency;
  lifecycle-derived referenceability; producer-local audit-intent/outbox foundation; and gateway-ready API
  contracts.
- **Phase 2B — Investment & Value Linkage:** InvestmentDecision, BenefitValueLink and typed references to
  MOD-0136 Budget, MOD-0138 Scenario and MOD-0072 Outcome/Value.

The physical ExternalContextReference provider/transport and DWS runtime consumption remain blocked even
though the four Phase 2A objects must be referenceability-ready. MOD-0117 cannot be marked `done` until
Phase 2B is separately authorized and complete.

## 2. Ownership and Boundaries

### Owned by MOD-0117

- Portfolio identity and owner-approved lifecycle.
- Initiative identity and owner-approved lifecycle.
- Program identity and owner-approved lifecycle.
- Project identity and owner-approved lifecycle.
- PPM actor visibility and referenceability decisions.
- InvestmentDecision as PPM investment-decision context.
- BenefitValueLink as the typed association between a PPM context and canonical external value/outcome.
- Authoritative `ppm.external-context-reference` version `1.0` referenceability result.

### Cannot become a second PPM SoR

- `Diten.EnterpriseStrategyService`
- `Diten.Platform`
- `Diten.ManagementGovernanceService`
- MOD-0354
- frontend browser state, mock data or prototype pages

### Strict out of scope

- Demand implementation or Demand lifecycle.
- ES `TaskAggregate`.
- Generic task/checklist ownership; MOD-0024 owns it.
- PPM-local WorkCenter or WorkCenter lifecycle.
- Task assignment, progress, due date, execution dependency or scheduling.
- Workflow, approval, SLA or escalation engine; MOD-0023 owns these.
- DWS structure, node, structural dependency or baseline; MOD-0354 owns these.
- Budget, scenario, outcome or value SoR copies; MOD-0136/MOD-0138/MOD-0072 remain authoritative.
- Capacity/resource scheduling.
- Legacy mock/prototype data migration.
- Workstream, PPM Task/My Tasks, Calendar, Project Effort Log, Meeting/Status Report.
- Free-text external identity.
- New `Workflow*` route, permission, class, namespace, collection, UI label, pack or branch naming.

### WorkCenter Control Tower hazards

This pack cannot create or alter an ES TaskAggregate, add task lifecycle/assignment behavior to DWS nodes,
introduce Approve UI/action/route/command behavior, or project free-text Demand identities into WorkCenter.
This authoring task is governance-only and does not call Gate 2. If a later pack revision or implementation
touches one of those hazards, Claude WorkCenter Control Tower Gate 2 is mandatory before the first dangerous
production change.

## 3. Owned Objects

| Object | Phase | Ownership purpose | Required owner decision before promotion |
|---|---|---|---|
| `Portfolio` | 2A | Portfolio identity and governance context | CLOSED for Phase 2A |
| `Initiative` | 2A | Finite initiative identity within PPM | CLOSED for Phase 2A |
| `Program` | 2A | Program identity and grouping context | CLOSED for Phase 2A |
| `Project` | 2A | Finite project identity within PPM | CLOSED for Phase 2A |
| `InvestmentDecision` | 2B | PPM investment-decision context, not approval engine | Decision state model and required PPM relationships |
| `BenefitValueLink` | 2B | Typed linkage to MOD-0072 and optional budget/scenario context | Cardinality, link lifecycle and external contract versions |
| `ExternalContextReferenceValidationProjection` | Later integration | Minimal authoritative typed-reference result | BLOCKED: exact provider/consumer runtime contract and transport |

No generic `PpmContext` entity may hide all business semantics behind one discriminator. Shared typed value
objects may reduce duplication, but Portfolio, Initiative, Program and Project remain separate domain types,
repositories and business rules.

Command/query families are object-specific and cannot collapse into arbitrary
`CreatePpmContext`/`UpdatePpmContext` handlers. Phase 2A authorizes object-specific create/read/update,
controlled lifecycle transitions, soft delete and local derived-referenceability queries. The authoritative
external-context provider and all Phase 2B commands remain unauthorized.

### Phase 2A lifecycle and relationship decisions

- Defaults: Portfolio `Draft`; Initiative `Proposed`; Program `Draft`; Project `Draft`.
- Portfolio: `Draft → Active`; `Draft|Active → Archived`; `Archived` is terminal.
- Initiative: `Proposed → Active|Cancelled`; `Active → OnHold|Completed|Cancelled`;
  `OnHold → Active|Completed|Cancelled`; `Completed|Cancelled` are terminal.
- Program: `Draft → Active|Cancelled`; `Active → OnHold|Completed|Cancelled`;
  `OnHold → Active|Completed|Cancelled`; `Completed|Cancelled` are terminal.
- Project: `Draft → Planned|Cancelled`; `Planned → Active|OnHold|Cancelled`;
  `Active → OnHold|Completed|Cancelled`; `OnHold → Active|Completed|Cancelled`;
  `Completed|Cancelled` are terminal.
- Initiative has an optional Portfolio; Program has an optional Portfolio.
- Project has exactly one parent: Initiative XOR Program. Null, dual-parent, self and cyclic relationships
  are invalid; cross-tenant parent lookup returns 404.
- Referenceability is derived from tenant visibility, soft-delete and lifecycle. New references are allowed
  for Portfolio `Draft|Active`, Initiative `Proposed|Active|OnHold`, Program `Draft|Active|OnHold`, and
  Project `Draft|Planned|Active|OnHold`. Terminal states reject new references without rewriting sealed
  DWS history.

## 4. Entity Fields

### 4.1 Common planned `EntityBase` contract

This is the intended local `Diten.PpmService` base contract, not permission to copy or inherit a base from
Platform.Common or EnterpriseStrategyService.

| Field | CLR/BSON direction | Required | Rule |
|---|---|---:|---|
| `Id` | `Guid`, BSON subtype 4 | Yes | Server-generated; `Guid.Empty` forbidden |
| `TenantId` | `Guid`, BSON subtype 4 | Yes | Authenticated server context only; never client payload |
| `CreatedAtUtc` | scalar UTC `DateTime` | Yes | Server-generated UTC |
| `UpdatedAtUtc` | nullable scalar UTC `DateTime` | No | Server-generated UTC |
| `IsDeleted` | `bool` | Yes | Defaults false; all reads filter false |
| `DeletedAtUtc` | nullable scalar UTC `DateTime` | No | Required when soft-deleted |
| `Version` | `int` | Yes | Technical optimistic concurrency only; starts at 1 |
| `CreatedBy` | `Guid` | Yes | Authenticated actor; never client payload |
| `UpdatedBy` | nullable `Guid` | No | Authenticated actor on mutation |

Local/Unspecified timestamps must fail closed unless the service explicitly creates and normalizes the value
to UTC. Exact serializer registration and cold-start BSON round-trip evidence remain promotion blockers.

### 4.2 Portfolio — Phase 2A fields

| Field | Type | Required | Phase 2A invariant |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC; max 64; unique per active tenant |
| `Name` | string | Yes | Trim + NFC; non-empty; max 200 |
| `Description` | string? | No | Trim + NFC; empty becomes null; max 2000 |
| `LifecycleState` | closed string enum | Yes | `Draft`, `Active`, `Archived`; default `Draft` |
| `VisibilityPolicyKey` | string? | No | Max 128; non-null requires authoritative MOD-0018 validation; arbitrary ACL JSON forbidden |

Index direction: unique partial `(TenantId, Code)` for `IsDeleted=false`; list index starts with
`TenantId, IsDeleted`. Referenceability is derived from owner-approved lifecycle + actor visibility +
soft-delete, never from a stored standalone boolean.

### 4.3 Initiative — Phase 2A fields

| Field | Type | Required | Phase 2A invariant |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC; max 64; unique per active tenant |
| `Name` | string | Yes | Trim + NFC; non-empty; max 200 |
| `Description` | string? | No | Trim + NFC; max 2000 |
| `PortfolioId` | Guid? | No | Same-tenant non-deleted Portfolio if supplied; optional-one cardinality |
| `LifecycleState` | closed string enum | Yes | `Proposed`, `Active`, `OnHold`, `Completed`, `Cancelled`; default `Proposed` |
| `VisibilityPolicyKey` | string? | No | Max 128; non-null requires authoritative MOD-0018 validation |

Index direction: unique active `(TenantId, Code)`; relationship index
`(TenantId, PortfolioId, IsDeleted)` if the relationship is approved.

### 4.4 Program — Phase 2A fields

| Field | Type | Required | Phase 2A invariant |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC; max 64; unique per active tenant |
| `Name` | string | Yes | Trim + NFC; non-empty; max 200 |
| `Description` | string? | No | Trim + NFC; max 2000 |
| `PortfolioId` | Guid? | No | Same-tenant non-deleted Portfolio if supplied; optional-one cardinality |
| `LifecycleState` | closed string enum | Yes | `Draft`, `Active`, `OnHold`, `Completed`, `Cancelled`; default `Draft` |
| `VisibilityPolicyKey` | string? | No | Max 128; non-null requires authoritative MOD-0018 validation |

Whether a Program may contain Initiatives, Projects, both, or neither is an explicit owner decision. No
implicit hierarchy may be copied from the current frontend prototype.

### 4.5 Project — Phase 2A fields

| Field | Type | Required | Phase 2A invariant |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC; max 64; unique per active tenant |
| `Name` | string | Yes | Trim + NFC; non-empty; max 200 |
| `Description` | string? | No | Trim + NFC; max 2000 |
| `ParentType` | closed string enum | Yes | `Initiative` or `Program` only |
| `ParentId` | Guid | Yes | Non-empty same-tenant non-deleted typed parent |
| `LifecycleState` | closed string enum | Yes | `Draft`, `Planned`, `Active`, `OnHold`, `Completed`, `Cancelled`; default `Draft` |
| `VisibilityPolicyKey` | string? | No | Max 128; non-null requires authoritative MOD-0018 validation |

`ParentType + ParentId` represents exactly one parent. No project task, progress, assignment, due-date,
schedule or DWS node fields are allowed here.

### 4.6 InvestmentDecision — proposed fields

| Field | Type | Required | Proposed invariant / open business decision |
|---|---|---:|---|
| `DecisionCode` | string | Yes | Trim + NFC; unique per active tenant |
| `Title` | string | Yes | Trim + NFC; non-empty |
| `Description` | string? | No | Trim + NFC |
| `PortfolioId` | Guid | Yes | Same-tenant, active, visible Portfolio |
| `DecisionState` | closed string enum | Yes | Owner review required; cannot encode MOD-0023 approval truth |
| `DecisionEffectiveAtUtc` | scalar UTC `DateTime`? | No | Business effective time only; not `ApprovedAt` |

`IsApproved`, `ApprovedAt`, `ApprovedBy`, local Approve commands and local approval UI are forbidden.
MOD-0023 owns authoritative approval decisions if approval is later required.

### 4.7 BenefitValueLink — proposed fields

| Field | Type | Required | Proposed invariant / open business decision |
|---|---|---:|---|
| `SourceContextKind` | closed string enum | Yes | Portfolio/Initiative/Program/Project only |
| `SourceContextId` | Guid | Yes | Same-tenant, active and visible PPM object |
| `OutcomeValueContractName` | string | Yes | Exact MOD-0072 contract name is a promotion blocker |
| `OutcomeValueContractVersion` | string | Yes | Exact supported version is a promotion blocker |
| `OutcomeValueReferenceId` | Guid | Yes | Opaque typed reference; MOD-0117 does not copy outcome/value |
| `BudgetReference` | typed reference? | No | MOD-0136 only; exact versioned shape required |
| `ScenarioReference` | typed reference? | No | MOD-0138 only; exact versioned shape required |
| `LinkState` | closed string enum | Yes | Owner review required; cannot become external lifecycle truth |

Index direction: active uniqueness/cardinality over
`(TenantId, SourceContextKind, SourceContextId, OutcomeValueReferenceId)` is owner review required.

### 4.8 ExternalContextReference validation projection

This is an **APPROVED GOVERNANCE BASELINE — NOT A RUNTIME CONTRACT**:

| Field | Type | Rule |
|---|---|---|
| `ContractName` | string | Exact `ppm.external-context-reference` |
| `ContractVersion` | string | Exact `1.0` |
| `ContextKind` | closed string enum | `Portfolio`, `Initiative`, `Program`, `Project` only |
| `ContextId` | Guid | Canonical non-empty Guid; opaque to consumer |

`TenantId` and `ActorId` come only from authenticated server context. Success returns only the typed
reference, never a full PPM object. Missing, soft-deleted, cross-tenant, invisible or not-referenceable all
produce 404 without existence disclosure. Referenceability is derived, not a stored `IsReferenceable`
business truth.

All writes use tenant-first filters and optimistic concurrency:
`TenantId + Id + IsDeleted=false + Version`. A version mismatch produces 409; silent overwrite is forbidden.

## 5. Repo Scope

The authorized Phase 2A implementation may create only:

- `services/Diten.PpmService/**` — user-approved Phase 2A scaffold/backend.
- `frontend/Diten.Web/Views/PPM/**` — user-approved tenant-shell Phase 2A surfaces.
- `frontend/Diten.Web/Controllers/PpmController.cs` or equivalent same-origin proxy — exact decision pending.
- `frontend/Diten.Web/wwwroot/assets/js/PPM/**`.
- `frontend/Diten.Web/Resources/Views/PPM/**`.
- `services/Diten.PpmService/tests/**`.
- Gateway route work only through a separate `integration-agent` task after route/port approval.

Gateway configuration remains unauthorized and integration-agent-only.

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent only.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` — FROZEN.
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.Platform/**`
- `services/Diten.Platform.Common/**`
- `services/Diten.AuthService/**`
- `services/Diten.ManagementGovernanceService/**`
- MOD-0354 code and collections.
- Existing delivery-execution prototype/mock code; containment/migration requires a separate approved pack.
- Office documents and Blueprint workbooks.

## 7. Dependencies

| Dependency | Use | Current gate |
|---|---|---|
| DCP-006 | Active Slice 2 orchestration | Approved; OD-03 closed, OD-04 OPEN/PARTIAL |
| MOD-0018 | Independent tenant entitlement plus JWT permission enforcement and actor context | PARTIAL; two-gate policy fixed, real PSS-owned AuthService catalog/grant provisioning and entitlement runtime evidence open |
| MOD-0021 / MOD-0035 | Immutable audit integration over shared eventing mechanics | PARTIAL; event and final Minimal Mutation Audit v1 consumer contract fixed; publisher credential and runtime evidence open |
| MOD-0136 | Budget typed reference | Exact versioned contract open |
| MOD-0138 | Scenario typed reference | Exact versioned contract open |
| MOD-0072 | Outcome/value typed reference | Exact versioned contract open |
| MOD-0354 | Consumer of typed ExternalContextReference only | Draft; provider runtime evidence blocks it |
| MOD-0023 | Approval/workflow boundary only | No local implementation |
| MOD-0024 | Task/checklist boundary only | No local implementation |

The existing MDM/Auth lookup-validation clients are pattern evidence only. Their bearer/tenant propagation
does not settle PPM S2S identity/delegation. The MDM validator behavior that collapses transport, timeout,
malformed response and all non-success responses into 404 must not be copied.

## 8. Runtime Constraints

- MongoDB, single database, tenant-owned collections.
- TenantId and ActorId are resolved from authenticated server context; client payload values are forbidden.
- Unknown/unresolved tenant fails closed; no default-tenant fallback.
- Every read/write filters `TenantId` and `IsDeleted=false`.
- Cross-tenant reads and references return 404.
- Soft delete uses `IsDeleted` and `DeletedAtUtc`; physical delete is forbidden.
- Tenant-first indexes and owner-approved partial unique active-code indexes are required.
- Optimistic concurrency uses technical `Version`; semantic states cannot reuse that name.
- Phase 2A mutation and producer-local technical audit intent/outbox are written in the same Mongo
  replica-set transaction; inability to persist either rolls back the mutation.
- The approved producer event identity is `PpmAuditIntentSubmittedV1`; EventName/routing key is
  `ppm.audit-intent.submitted.v1`. PPM handlers/controllers never call RabbitMQ or MassTransit directly;
  a future authorized producer worker uses only MOD-0035's public `IEventBus`/outbox abstraction.
- Its `ppm-event-hmac-sha256.v1` input signs exact newline-delimited envelope provenance in this order:
  scheme, EventId, EventName, EventVersion, TenantId, CorrelationId, Producer, CausationId (or literal `-`),
  OccurredAtUtc and payload byte length, followed by exact canonical payload bytes. The wire signature is
  lowercase `[0-9a-f]{64}` only.
- Shared `EventEnvelope`, `IEventBus`, outbox and inbox mechanics belong to
  `Diten.BuildingBlocks.Eventing`. MOD-0117 owns this logical PPM event at the planned
  `services/Diten.PpmService/src/Diten.PpmService.Contracts/Events/**` path. Platform is consumer-only;
  `Diten.Platform.Contracts` does not own this PPM event.
- Future producer implementation is restricted to narrow
  `services/Diten.PpmService` Infrastructure/Application/Persistence worker-outbox paths and PPM tests.
  It requires a separately reviewable MOD-0117 atomic change after the DCP-006 MOD-0035/PSS status gate and
  explicit user runtime approval; the present `review` status does not authorize it.
- Delivery is durable at-least-once with idempotent consumption; exactly-once is not claimed. Post-commit
  failure uses retry, dead-letter, alarm and authorized replay.
- The final MOD-0021 payload/consumer mapping is **Minimal Mutation Audit v1**, containing exactly
  `auditIntentId`, `actorId`, `entityType`, `entityId`, `mutation` and `occurredAtUtc`. It evidences only
  actor, minimal mutation, PPM aggregate and time—not authorization/entitlement, a business snapshot or
  complete lifecycle history. The authenticated publisher credential remains a runtime evidence gate;
  local intent is not the business audit SoR. Platform `audit_outbox`/`audit_events`, the shared-key
  internal append endpoint, full snapshots, secrets, tokens and raw permission inventories are forbidden.
- Delivery uses 5 total attempts: 10 seconds after the first failure, then exponential backoff with jitter
  capped at 5 minutes; the fifth failed attempt causes DLQ plus alarm. Authorized replay preserves the same
  `EventId` and identical canonical bytes; changed bytes are rejected. If first delivery was not accepted,
  replay may create exactly one `AuditEvent`; if accepted, it creates none. Idempotency is
  `ConsumerName + EventId`; unauthorized replay and replay UI/API are forbidden.
- A boolean `IsApproved` or `IsReferenceable` cannot be business truth.
- Lifecycle and referenceability states cannot be copied from mock/prototype code.
- `init-001`, `prj-001` and any candidate/legacy identity are forbidden runtime literals.
- Service port is `5061`. Frontend remains `5001`; browser traffic uses Gateway `5000`, never direct `5061`.
- Existing port `5004` delivery-execution routes are legacy ES evidence, not MOD-0117 route allocation.
- Phase 2A may expose gateway-ready object CRUD/lifecycle contracts. No ExternalContextReference provider
  endpoint, gateway route, S2S credential/delegation or DWS integration is authorized.
- Provider outage, timeout or malformed transport maps to 503, never 404.
- Fail-open and local-cache ownership/existence inference are forbidden.

## 9. Layout & Shell Contract

- Frontmatter `shell: tenant`.
- Every future MOD-0117 Razor page must explicitly set:

```cshtml
@{
    Layout = "_LayoutTenantShell";
}
```

- `_ViewStart.cshtml` is not used to infer the shell and `_Layout.cshtml` remains unchanged.
- Planned view root: `frontend/Diten.Web/Views/PPM/{Surface}/`.
- Seven languages are required: en, fr, es, zh, ar, ru, tr.
- Future UI must cover loading, empty, 400, 401, 403, 404, 409 and 503 states.
- Referenceability/visibility must be explainable without disclosing inaccessible object existence.
- Existing Management Governance / Delivery Execution mock pages are not production templates.

## 10. Backend File Convention

The authorized service scaffold and object features follow:

```text
services/Diten.PpmService/src/Diten.PpmService.Application/Features/{Object}/
├── Commands/                       # one sealed command record per file
├── Queries/                        # one sealed query record per file
├── Handlers/
│   ├── CommandHandlers/            # separate, mandatory
│   └── QueryHandlers/              # separate, mandatory
├── Validators/                     # one validator per file
└── {Object}Models.cs
```

- Commands: `{Verb}{Object}Command`.
- Queries: `Get{Object}{Qualifier}Query`.
- Handlers: `{Verb}{Object}Handler`; no `CommandHandler`/`QueryHandler` suffix.
- Validators: `{Verb}{Object}Validator`; no `Command` suffix.
- Commands/queries use `IRequest<Response<T>>`.
- Controllers contain no business logic.
- External validation is consumed behind an Application interface and implemented in Infrastructure.
- MongoDB driver types remain in Persistence.

Scaffold authorization is recorded; provider and Phase 2B paths remain blocked.

## 11. Frontend File Contract

| Surface | Authorized capability | Form-field count | Golden reference | State |
|---|---|---:|---|---|
| Portfolio | `/ppm/portfolios`; Code, Name, Description, LifecycleState, VisibilityPolicyKey | 5 | slim | AUTHORIZED 2A |
| Initiative | `/ppm/initiatives`; Code, Name, Description, PortfolioId, LifecycleState, VisibilityPolicyKey | 6 | slim | AUTHORIZED 2A |
| Program | `/ppm/programs`; Code, Name, Description, PortfolioId, LifecycleState, VisibilityPolicyKey | 6 | slim | AUTHORIZED 2A |
| Project | `/ppm/projects`; Code, Name, Description, ParentType, ParentId, LifecycleState, VisibilityPolicyKey | 7 | slim | AUTHORIZED 2A |
| InvestmentDecision | Decision register/details | TBD | TBD | BLOCKED 2B |
| BenefitValueLink | Typed link register/details | TBD | TBD | BLOCKED 2B |

For each surface:

- `≤8` approved user-entered fields selects Slim with `_CreateEditOffcanvas.cshtml` and
  `_DetailsQuickView.cshtml`.
- `>8` selects Compact with separate `Create.cshtml`, `Edit.cshtml`, `Details.cshtml` and `_Form.cshtml`.
- List surfaces use DataTable v2, skeleton loader, filter, L10n bridge and seven-language RESX parity.
- Navigation must support Portfolio/Initiative/Program/Project context without manufacturing an unapproved
  hierarchy.
- InvestmentDecision and BenefitValueLink are separate surfaces, not generic context tabs.
- Browser code cannot embed mock rows or fallback to ES prototype endpoints.

The four Phase 2A frontend surfaces are authorized. `TBD` applies only to blocked Phase 2B surfaces and
does not expand this pack's implementation authority.

## 12. Validation Rules

| Field / boundary | Required | Format/rule | DB/pre-check |
|---|---:|---|---|
| Common `Id` | Yes | Canonical non-empty Guid | Tenant-scoped lookup |
| Common `Code` | Yes | Strict decode, Trim → NFC, owner-approved length | Active tenant unique partial index |
| Common name/title | Yes | Strict decode, Trim → NFC, non-empty | — |
| Description | No | Strict decode, Trim → NFC; empty → null | — |
| Lifecycle/state | Yes | Closed owner-approved value | Unknown value fails closed |
| Parent PPM reference | When supplied | Correct typed ID, same tenant, active and visible | Authoritative repository check |
| External contract name | Yes | Exact `ppm.external-context-reference` | Unknown → 400 |
| External contract version | Yes | Exact `1.0` | Unknown → 400 |
| External ContextKind | Yes | Portfolio/Initiative/Program/Project only | Demand/task/workflow/approval rejected |
| External ContextId | Yes | Canonical non-empty Guid | Authoritative referenceability check |
| TenantId/ActorId | No client field | Authenticated server context only | Client value rejected |
| Concurrency Version | Mutations | Positive expected technical version | Atomic compare-and-update |
| External MOD-0136/0138/0072 references | Phase 2B | Exact approved name/version/Guid | Authoritative contract validation |

Invalid Unicode, unpaired surrogate and ambiguous normalized values fail closed. Validation, persistence and
unique indexes must use the same normalized values.

## 13. Failure Path to Verify

| Scenario | Expected result |
|---|---|
| Missing/invalid required field | 400; no mutation |
| Duplicate active Code in tenant | 409; no duplicate |
| Same Code in different tenant | Allowed without information leak |
| Missing/soft-deleted/cross-tenant PPM object | 404 |
| Actor cannot see/reference context | 404; existence not disclosed |
| Actor lacks DWS command permission | MOD-0018 returns 403 before provider call |
| Invalid contract name/version/kind/Guid | 400 |
| External provider timeout/unavailable/malformed response | 503; never converted to 404 |
| Attempted MOD-0354 reference replacement | Consumer-owned 409 |
| Stale Version | 409; no silent overwrite |
| Repeated idempotent request | Stable approved outcome; mutation not repeated |
| Same idempotency key with different request | 409; no mutation |
| Local cache contains context while provider is unavailable | 503; cache cannot prove existence/ownership |
| Provider success contains full PPM entity | Contract rejection; success must be minimal typed reference |
| Unknown tenant | Authentication/tenant resolution failure; never default tenant |

Soft deletion of a referenced PPM object prevents new references but cannot delete or rewrite already sealed
MOD-0354 revision/baseline history.

## 14. Authorization Convention

- Authentication: JWT.
- Enforcement owner: MOD-0018.
- Authorization is two independent gates: active tenant PPM module entitlement first, then the required
  canonical `ppm.*` permission. Missing/disabled/suspended/expired/indeterminate entitlement is `403` and
  cannot be bypassed by a stale JWT permission claim.
- Canonical entitlement/catalog identity is exactly `ModuleCode = PPM`; lowercase `ppm.*` values are
  permission keys, not module-code aliases.
- The 16 lowercase-dotted manifest keys may exist in the global AuthService permission catalog; catalog
  presence grants nothing. Only explicit tenant-scoped `RolePermission` grants produce user access.
- PPM is not part of FU9's locked Auth+MDM default grants. Tenant administrators receive no implicit PPM
  permissions; aliases, role-name bypasses, raw token access and hard-coded allow paths are forbidden.
- Entitlement removal leaves role grants dormant rather than deleting them. Entitlement invalidation must
  deny immediately on every instance; token refresh/revocation follows MOD-0018-FU13.
- Current code reality is incompatible with this PPM rule: generic `GrantModuleWithKeysAsync` auto-grants
  Admin/Viewer, while `RevokeModuleAsync`/reconcile deletes module-source grants. These generic operations
  must not process `PPM` unchanged. Existing MDM/other-module behavior remains protected.
- Control Tower selected the PPM-specific strategy. Re-entitlement makes only still-existing explicit grants
  held by current role memberships effective; deleted grants are not reconstructed and no automatic new
  grant is created. Re-entitlement and authorized administrator visibility of the current grant/role
  inventory are audited.
- `IEntitlementChecker` evaluates only module/feature entitlement. AuthService owns grants and JWT permission
  claims; PPM does not recalculate roles, grants or effective permission.
- After both gates allow, missing/soft-deleted/cross-tenant objects return indistinguishable `404`.
- Actor: authenticated tenant user; exact service-to-service identity + actor delegation remains OPEN.
- DWS permission is evaluated by MOD-0018 before MOD-0117 provider invocation.
- MOD-0117 evaluates only PPM context visibility/referenceability; it does not recalculate DWS permission.
- **Phase 2A canonical permission contract (closed set):**
  - `ppm.portfolios.read`
  - `ppm.portfolios.create`
  - `ppm.portfolios.update`
  - `ppm.portfolios.change-lifecycle`
  - `ppm.initiatives.read`
  - `ppm.initiatives.create`
  - `ppm.initiatives.update`
  - `ppm.initiatives.change-lifecycle`
  - `ppm.programs.read`
  - `ppm.programs.create`
  - `ppm.programs.update`
  - `ppm.programs.change-lifecycle`
  - `ppm.projects.read`
  - `ppm.projects.create`
  - `ppm.projects.update`
  - `ppm.projects.change-lifecycle`
- The list above is exact and exhaustive for Phase 2A. Wildcards and alias permissions are forbidden.
  `ppm.portfolios.archive` is not canonical and must be reconciled to
  `ppm.portfolios.change-lifecycle` in the PPM branch before final Phase 2A closure. Phase 2B
  investment/benefit permissions and the external-context validation permission are not part of this
  contract and cannot be added through the PSS-A runtime slice.
- Catalog registration may contain exactly these 16 keys under `ModuleCode = PPM`; catalog presence does
  not grant access and cannot add PPM to default Admin/Viewer role templates.
- Service-specific HasPermission/filter/evaluator code cannot be copied from another service.
- Existing shared `X-Internal-Api-Key` alone is insufficient for actor visibility and cannot be adopted as
  the authoritative decision without an approved service-identity + actor-delegation design.

## 15. Gateway / API Routing Decision

**Decision:** `Diten.PpmService` local port `5061`; frontend `5001`; browser entry Gateway `5000`.

**Phase 2A PPM object API Gateway mapping authorized; integration-agent only.** The authorization is limited
to `/api/v1/ppm` and `/api/v1/ppm/{everything}` → port `5061`; it does not authorize provider, DWS, Phase 2B
or any other Gateway route.

- Future browser traffic must use Gateway `5000`; direct backend-port calls are forbidden.
- Existing `/api/v1/delivery-execution*` routes to ES port `5004` are legacy/prototype evidence and cannot
  silently become MOD-0117 routes.
- Phase 2A object APIs and the integration-agent-owned Gateway mapping are implemented and evidenced by
  the targeted route test plus a temporary end-to-end verification chain.
- ExternalContextReference provider endpoint/transport and S2S validation remain undefined and blocked.
- Timeout, retry/circuit-breaker, credential and actor-delegation designs remain promotion blockers.

## 16. Acceptance Criteria

- [x] Phase 2A implements distinct Portfolio, Initiative, Program and Project domain types; no generic
  PpmContext entity exists.
- [x] The recorded lifecycle and visibility rules derive referenceability with soft-delete; standalone
  `IsApproved`/`IsReferenceable` truth does not exist.
- [ ] Phase 2A objects are provider-ready; a later authorized integration provides the exact
  `ppm.external-context-reference` `1.0` runtime contract.
- [ ] Invalid contract name/version/kind/Guid returns 400.
- [ ] Missing, soft-deleted, cross-tenant, invisible or not-referenceable context returns indistinguishable 404.
- [ ] MOD-0018 DWS permission denial returns 403 before provider invocation.
- [ ] Provider outage, timeout and malformed transport return 503 and never 404.
- [ ] Fail-open and local-cache existence/ownership inference are absent.
- [ ] Phase 2B implements distinct InvestmentDecision and BenefitValueLink objects.
- [ ] MOD-0136/MOD-0138/MOD-0072 are versioned typed references only; no copied lifecycle or payload SoR.
- [x] All tenant-owned queries/writes use server TenantId, soft-delete filters and tenant-first indexes.
- [x] Optimistic concurrency produces 409 on stale Version.
- [x] Authorized mutations and producer-local audit intent commit atomically in a replica-set transaction.
- [x] Architecture tests prevent ES, Platform, ManagementGovernanceService, MOD-0354 and frontend state from
  becoming a second PPM SoR.
- [x] No Demand, TaskAggregate, task/checklist, WorkCenter, workflow, approval, SLA, escalation, scheduling,
  DWS structure/node/dependency/baseline or capacity ownership is introduced.
- [x] No runtime mock literals such as `init-001`, `prj-001`, candidate IDs or legacy IDs exist.
- [x] `/ppm`, `/ppm/portfolios`, `/ppm/initiatives`, `/ppm/programs` and `/ppm/projects` use
  `_LayoutTenantShell`, DataTable v2, Golden Slim, SweetAlert2 and seven-language RESX parity.
- [x] UI shows lifecycle badge, parent and derived referenceability; has no bulk delete/lifecycle or Approve
  action and excludes tenant/audit fields.
- [x] Browser traffic uses Gateway 5000 only; no direct service-port JavaScript call exists.
- [x] Loading/empty/400/401/403/404/409/503 states are testable without existence disclosure.
- [x] Real Mongo evidence proves tenant isolation, unique indexes, concurrency and required transactions.
- [x] Phase 2A alone does not unblock MOD-0354 runtime; provider compatibility/security evidence is separate.
- [ ] MOD-0117 is not marked done until Phase 2B is complete.

## 17. Test Expectations

### Unit

- Normalization and validation for each proposed field.
- Owner-approved lifecycle transition matrix and derived referenceability.
- Actor visibility decisions and non-disclosure mapping.
- Duplicate active Code and soft-deleted Code policy.
- Optimistic concurrency and idempotency outcomes.
- External contract name/version/kind/Guid validation.
- Phase 2B typed-reference version guards.

### Integration

- Real Mongo tenant/cross-tenant and soft-delete behavior.
- Tenant-first unique/list indexes and concurrency filters.
- Same-tenant/different-tenant duplicate cases.
- Provider 404 versus outage/timeout/malformed 503 separation.
- No fail-open/cache inference.
- Minimal typed success response; full object rejection.
- MOD-0018 403 before provider call.
- MOD-0021 approved audit producer behavior.
- S2S service identity + actor delegation, replay and revocation cases.
- Runtime compatibility tests for supported/unknown contract versions.

### Architecture and negative tests

- No repository/collection/entity duplication in ES, Platform, ManagementGovernanceService or MOD-0354.
- No task, workflow, approval, WorkCenter, DWS structure or external budget/scenario/outcome lifecycle types.
- No `Workflow*`, free-text external identity or runtime candidate/legacy/mock literals.
- No direct Mongo driver dependency outside Persistence.
- No direct service-port browser calls.

### Frontend

- Per-surface Slim/Compact verifier after decisions are approved.
- DataTable v2/skeleton behavior where list tables are selected.
- `_LayoutTenantShell` explicitly present.
- Seven-language RESX parity.
- Loading/empty/error/non-disclosure smoke tests.
- Gateway-only browser integration.

### DataTable verifier disposition

The four repeated verifier findings on Portfolio, Initiative, Program and Project are formally dispositioned
as policy/profile mismatches rather than MOD-0117 product defects:

- `BulkDelete` and `BulkDeleteConfirm` are intentionally absent because bulk delete is prohibited for all
  four Phase 2A PPM objects. Their absence must not be remediated by adding a bulk-delete UI or API.
- Browser-side `getAuthHeaders()` is intentionally absent and must not be added. MOD-0117 uses the
  same-origin MVC proxy with the authenticated HttpOnly cookie; browser JavaScript must not read or expose
  the JWT.
- The protected `.antigravity` verifier is not changed by this disposition. Until the verifier supports this
  approved profile, its four findings per surface are recorded as expected advisory/profile findings while
  the applicable DataTable v2, localization, layout and gateway-only checks remain mandatory.

Phase 2A evidence is recorded in the
[implementation audit](../../../../docs/audits/mod-0117-phase-2a-implementation-audit-2026-07-29.md):
shared JWT evaluation 21/21, PPM 17/17, targeted Gateway 1/1, isolated Mongo replica-set 9/9,
Web 22/22, delegated jsdom PASS, and real browser CRUD through a temporary full chain. DataTable
verification reports 60 PASS plus four formally dispositioned policy/profile findings per surface.

## 18. Ready-for-dev Checklist

- [x] DCP-006 is approved.
- [x] DCP-006 OD-03 is closed.
- [x] MOD-0117 canonical ID/name preflight passed on 2026-07-29.
- [x] Permanent owner role and Phase 2A/2B high-level boundary are recorded.
- [x] ExternalContextReference shape is an approved governance baseline, not a runtime contract.
- [x] Control Tower recorded Phase 2A lifecycle, referenceability and cardinality decisions.
- [x] Phase 2A object/UI scope and Golden Slim selections are approved.
- [ ] Phase 2B exact InvestmentDecision/BenefitValueLink fields, states, invariants and cardinalities are
  approved.
- [ ] MOD-0136/MOD-0138/MOD-0072 exact versioned typed-reference contracts are approved.
- [x] Every Phase 2A frontend surface has an exact field count and Golden Slim decision.
- [x] Phase 2A hub/routes and visibility/referenceability presentation are approved.
- [ ] Exact S2S service identity and actor delegation are approved.
- [ ] Exact physical transport, route and versioning are approved.
- [ ] Timeout, retry and circuit-breaker policy is approved.
- [x] MOD-0018 reusable signed-JWT enforcement integration and PPM adapter are exact and evidenced; real
  AuthService PPM grant provisioning remains open.
- [ ] MOD-0021 audit contract/integration is exact and evidenced.
- [ ] Idempotency key scope, canonicalization, receipt and retention are approved.
- [x] EntityBase CLR/BSON representation and isolated real-Mongo replica-set evidence pass.
- [ ] Runtime contract compatibility and security evidence pass.
- [x] Service port `5061`, frontend `5001` and browser Gateway `5000` boundaries are approved; the
  integration-agent-owned Phase 2A mapping has targeted and end-to-end evidence.
- [x] Explicit user approval to scaffold `Diten.PpmService` and implement Phase 2A backend/frontend is
  recorded on 2026-07-29.
- [x] Human approval promoted this pack to `approved`; scoped implementation and runtime evidence now
  promote Phase 2A to `review`.
- [ ] Any later WorkCenter hazard has Gate 2 PASS before production change.

Unchecked provider, shared-contract and Phase 2B items do not revoke the scoped Phase 2A authority; they
block only their named runtime boundaries. This pack is not unconditional `ready-for-dev`.

## 19. Implementation Notes

- Target `phase-2a` is scoped authorization, not whole-module completion.
- DCP-006 is the sole active 1.3/1.4/1.6 orchestration contract. DCP-003 remains deferred/non-executable
  legacy safe-parity planning and gives no implementation authority.
- Registry and master-plan delivery status are reconciled in the same governance closure.
- Master 8.1 places MOD-0117 in Portfolio/Investment and Delivery/Execution context; MOD-0354 remains the
  structural engine. V5 sequences WS-C1 MOD-0117 context before WS-C2 MOD-0354/MOD-0355.
- Current PPM mock adapters use string IDs and static `init-001`/`prj-001` data; resilient wrappers can fall
  back to process-local cache. Neither pattern is authoritative or reusable for validation.
- Existing frontend routes and gateway port 5004 routes remain prototype/legacy evidence. No migration or
  route reuse is implied.
- Existing MDM/Auth reference validators demonstrate typed-client and tenant/bearer propagation patterns,
  but the MDM client collapses dependency/transport failures to 404. MOD-0117 must preserve the 404/503
  boundary.
- Artifact-tool workbook import produced no inspect result in this environment. The canonical verifier passed,
  and previously verified narrow workbook rows in this Control Tower task chain support the identity/sequence
  statements; workbook files were not modified.
- Temporary review topology `:5191 → :5200 → :5061 → :27018` produced real CRUD evidence and is not a
  production URL. Its signed JWT bootstrap was ephemeral and changed no repository/Auth data.

### Open business decisions

1. Phase 2B InvestmentDecision semantics without duplicating MOD-0023 approval.
2. Phase 2B BenefitValueLink cardinality, lifecycle and MOD-0072 meaning.

### Open technical decisions

1. PSS-owned AuthService registration of the 16 PPM catalog keys and explicit tenant-scoped role-grant
   provisioning remains evidenced by PSS-A. The PSS-owned
   `platform.ppm-entitlement-decision.v1` provider uses the fixed `ModuleCode = PPM`, an endpoint-specific
   PPM service credential, and fail-closed `200 allow/deny` versus `503 indeterminate` semantics. The
   provider is default-disabled; disabled deployments return `503` without entitlement lookup, while enabled
   deployments require a valid dedicated secret at startup. Disabled is not a business entitlement deny.
   The PPM-service consumer and final normal-port evidence remain separate work.
2. Executable PSS authority for the selected PPM-specific strategy and MOD-0035 slice; existing generic
   MDM/other-module behavior remains unchanged.
3. ExternalContextReference provider transport, route and compatibility/security evidence.
4. Exact allowlisted `PpmAuditIntentSubmittedV1` payload/consumer mapping, authenticated publisher credential
   and MOD-0035/MOD-0021 delivery integration.
5. Phase 2B contracts with MOD-0136/MOD-0138/MOD-0072.

### Open UI decisions

1. Phase 2B InvestmentDecision and BenefitValueLink interaction models.
2. Later composite Project Workspace integrations are tracked in the
   [R1 PPM MVP backlog](../../../release/release-backlog/R1-ppm-mvp-backlog.md); Phase 2A keeps PPM,
   DWS, WorkCenter, finance, resource/capacity, document, compliance and audit ownership separate.

### Change log

| Date | Change | Authority |
|---|---|---|
| 2026-07-29 | Formally dispositioned the four repeated DataTable verifier findings per Phase 2A surface as approved policy/profile mismatches: bulk delete is prohibited, and authentication uses a same-origin MVC proxy plus HttpOnly cookie rather than browser-side `getAuthHeaders()`. No bulk-delete surface, browser JWT exposure or verifier change is authorized. | User / Enterprise Strategy Control Tower |
| 2026-07-30 | Locked independent PPM tenant-entitlement and user-permission gates, dormant grants after entitlement removal, and the `PpmAuditIntentSubmittedV1` / `ppm.audit-intent.submitted.v1` producer identity. Minimal Mutation Audit v1 is final; publisher credential, runtime evidence and PSS production authorization remain open. | User / Enterprise Strategy Control Tower |
| 2026-07-30 | Code-reality correction fixed `ModuleCode = PPM` and blocked the existing generic Admin/Viewer auto-grant plus destructive revoke/reconcile flow from unchanged PPM use. PPM-specific versus generic bridge revision and dormant-grant reactivation remain PSS/security human-review decisions. | User / Enterprise Strategy Control Tower |
| 2026-07-30 | Recorded PSS-B1 physical provider contract `platform.ppm-entitlement-decision.v1`, dedicated PPM caller credential, exact minimal response and fail-closed `503` boundary. PSS owns the provider and every-instance invalidation; the PPM-service consumer remains unimplemented and this pack stays `review`. | User / Enterprise Strategy Control Tower |
| 2026-07-29 | Promoted the authorized Phase 2A slice to `review` after shared JWT 21/21, PPM 17/17, targeted Gateway 1/1, isolated Mongo replica-set 9/9, Web 22/22, delegated jsdom PASS and real browser CRUD evidence. The smoke used an ephemeral signed JWT bootstrap because the real tenant-admin token has zero PPM permissions; AuthService grant provisioning and MOD-0021 delivery remain open. DataTable reports 60 PASS plus four policy/profile findings per surface. | Orchestrator — Phase 2A verification evidence |
| 2026-07-29 | Promoted pack to `approved` and recorded explicit user authorization for the `Diten.PpmService` scaffold plus Phase 2A backend/frontend. Fixed lifecycle, cardinality, referenceability, Golden Slim surfaces, port `5061`, Gateway `5000` boundary and transactional local audit-intent foundation. Provider transport, DWS runtime integration, Phase 2B and WorkCenter hazards remain blocked. | User / Enterprise Strategy Control Tower |
| 2026-07-29 | Reconciled stale pack branch metadata to the active Enterprise Strategy worktree `feature/es/enterprise-strategy`; scope and authorization are unchanged. | Orchestrator — active Enterprise Strategy worktree reconciliation |
| 2026-07-29 | Phase 1.5 plan explicitly approved by the user. Reconciled exact Phase 2A field limits, `ParentType + ParentId`, and Golden Slim form counts `5/6/6/7` before production coding. | User / Orchestrator |
| 2026-07-29 | Authorized the narrow Phase 2A PPM object API Gateway mapping; implementation is restricted to `integration-agent`. | Enterprise Strategy Control Tower |

## 20. Follow-up Items

- Product Owner/PMO classification of every
  [deferred Project Workspace integration](../../../release/release-backlog/R1-ppm-mvp-backlog.md) after
  its named dependency gates close; backlog presence alone is not implementation or R1 authority.
- Portfolio Governance Process Owner review of all open business decisions.
- AuthService/PSS provisioning of real PPM grants; MOD-0021 runtime integration remains open.
- Contract-owner review for MOD-0136, MOD-0138 and MOD-0072 typed references.
- Exact ExternalContextReference provider/consumer runtime design and compatibility/security evidence.
- Keep the formal DataTable verifier disposition above visible until the protected verifier supports the
  no-bulk-delete and same-origin HttpOnly-cookie profile; no product remediation is required.
- MOD-0354 promotion only after its MOD-0117 provider blocker and other OD-04 subsets close.
- Any legacy mock/prototype containment or migration through a separate approved pack.
- Any future WorkCenter-related behavior through DCP-004 and the applicable Gate 2 process.
