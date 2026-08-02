# Enterprise Strategy & Business Performance — Domain Config

> Domain-specific packaging, ownership and physical-topology authority. Engineering rules are inherited from
> [.antigravity/rules/](../../../.antigravity/rules/). This governance decision creates no service scaffold,
> port, route, deployment, migration, runtime authority or production authority.

## Domain identity

- Name: `Enterprise Strategy & Business Performance`
- Slug: `enterprise-strategy-business-performance`
- Branch short code: `esbp`
- Governance-packaged modules: `MOD-0352`, `MOD-0136`, `MOD-0138`, `MOD-0072`
- Physical topology authority in this decision: `MOD-0136`, `MOD-0138`, `MOD-0072`

Repository-domain packaging does not replace any module's Blueprint logical route. In particular, no module
covered by this topology decision may be placed in `Diten.EnterpriseStrategyService`.

This later Control Tower topology authority supersedes the ESBP README's earlier scaffold wording that left
physical placement `TBD`. The README and the three higher-authority draft packs must be reconciled in separately
authorized documentation turns before pack promotion; their earlier conditional options grant no runtime exception.

## Canonical ownership and logical routing

### MOD-0352 — Enterprise Strategy Management

Owns strategy, objectives/cascade and performance context. It does not duplicate Demand/PPM lifecycle,
MOD-0354 DWS, MOD-0355 BPM, MOD-0024 task or MOD-0023 approval ownership. This topology decision does not
select or alter MOD-0352 placement.

### MOD-0136 — Budgeting

- Blueprint route: `Enterprise Application Ecosystem → ERP Suite → FP&A / EPM`
- Institutional owner: **FP&A Budgeting Process Owner**
- Owns Budget, immutable BudgetVersion, funding baseline/selection truth and budget-version validation.
- Does not own Portfolio/InvestmentCase, a PPM-local budget copy, Scenario, approval workflow or actual benefit outcome.

### MOD-0138 — Scenario Planning

- Blueprint route: `Enterprise Application Ecosystem → ERP Suite → FP&A / EPM`
- Institutional owner: **FP&A Scenario Planning Process Owner**
- Owns Scenario, ScenarioVersion, ComparatorRun, ComparatorOutput and selected-scenario truth.
- Does not own a PPM-local scenario engine, budget truth, approval workflow or Portfolio/InvestmentCase lifecycle.

### MOD-0072 — Decision Logs & Outcome Tracking

- Blueprint route: `Data, Knowledge & Intelligence → Decision Intelligence`
- Institutional owner: **Decision Intelligence & Benefit Realization Process Owner**
- Owns Outcome, OutcomeVersion, actual measure/period, benefit realization, decision-to-outcome linkage and
  evidence reference/provenance.
- Does not own planned BenefitCommitment, Portfolio/InvestmentCase, local approval or a PPM actual-value copy.

## Authoritative physical topology

### FP&A primary topology

The primary topology is one future modular-monolith host, `Diten.FpaService`, containing exactly two isolated
internal modules for this decision: `Budgeting` (MOD-0136) and `ScenarioPlanning` (MOD-0138). Co-location does
not create shared domain or persistence ownership. Each module owns its own Domain, Application, Persistence,
Infrastructure and Api module surface, namespaces, repositories, Mongo collections, indexes, DI registrations
and transaction boundary.

### FP&A fail-closed fallback

If any mandatory isolation test below cannot be implemented or fails, the primary topology is rejected
fail-closed before runtime promotion. The only fallback is two separate five-layer services:
`Diten.BudgetingService` for MOD-0136 and `Diten.ScenarioPlanningService` for MOD-0138. The failed shared host
is not partially activated and its collections, registrations, credentials, endpoints and deployment are not
retained as a parallel authority.

### Topology exclusivity

Primary and fallback are mutually exclusive deployment profiles. One environment/release may activate either
the single `Diten.FpaService` profile or both fallback services, never a mixture. Configuration, service
discovery, deployment manifests, health registration and collection-owner manifests must select exactly one
profile and fail startup for zero, mixed or dual activation. Data migration between profiles requires a
separately approved expand/contract and rollback plan; it is not implicit in fallback selection.

### Decision Intelligence topology

MOD-0072 is placed only in a separate future `Diten.DecisionIntelligenceService` as the `OutcomeTracking`
module. It cannot be placed in `Diten.FpaService`, either FP&A fallback service or
`Diten.EnterpriseStrategyService`. It shares no domain model, repository, collection, index, Mongo transaction
or persistence implementation with MOD-0136 or MOD-0138. Communication is only through approved
typed/versioned reference contracts.

### Enterprise Strategy prohibition

MOD-0136, MOD-0138 and MOD-0072 cannot be implemented in, referenced as owned by, or migrated into
`Diten.EnterpriseStrategyService`. Existing decision, budget, scenario, actual-value and realization runtime
fields there are prototype/legacy migration evidence or quarantine candidates only. They are not an
authoritative contract, production baseline or system of record. Code proximity, an existing endpoint,
collection or UI does not override this prohibition.

## Exact future logical five-layer repo scopes

These paths are reserved logical boundaries, not permission to create files. A promoted implementation pack
must select exactly one applicable topology and then authorize concrete files within it.

| Profile/module | Exact future logical scope |
|---|---|
| Primary / Budgeting | `services/Diten.FpaService/src/Diten.FpaService.Api/Modules/Budgeting/**`; `services/Diten.FpaService/src/Diten.FpaService.Application/Modules/Budgeting/**`; `services/Diten.FpaService/src/Diten.FpaService.Domain/Modules/Budgeting/**`; `services/Diten.FpaService/src/Diten.FpaService.Persistence/Modules/Budgeting/**`; `services/Diten.FpaService/src/Diten.FpaService.Infrastructure/Modules/Budgeting/**` |
| Primary / ScenarioPlanning | `services/Diten.FpaService/src/Diten.FpaService.Api/Modules/ScenarioPlanning/**`; `services/Diten.FpaService/src/Diten.FpaService.Application/Modules/ScenarioPlanning/**`; `services/Diten.FpaService/src/Diten.FpaService.Domain/Modules/ScenarioPlanning/**`; `services/Diten.FpaService/src/Diten.FpaService.Persistence/Modules/ScenarioPlanning/**`; `services/Diten.FpaService/src/Diten.FpaService.Infrastructure/Modules/ScenarioPlanning/**` |
| Fallback / Budgeting | `services/Diten.BudgetingService/src/Diten.BudgetingService.Api/**`; `services/Diten.BudgetingService/src/Diten.BudgetingService.Application/**`; `services/Diten.BudgetingService/src/Diten.BudgetingService.Domain/**`; `services/Diten.BudgetingService/src/Diten.BudgetingService.Persistence/**`; `services/Diten.BudgetingService/src/Diten.BudgetingService.Infrastructure/**` |
| Fallback / ScenarioPlanning | `services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Api/**`; `services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Application/**`; `services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Domain/**`; `services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Persistence/**`; `services/Diten.ScenarioPlanningService/src/Diten.ScenarioPlanningService.Infrastructure/**` |
| Decision Intelligence / OutcomeTracking | `services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Api/Modules/OutcomeTracking/**`; `services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Application/Modules/OutcomeTracking/**`; `services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Domain/Modules/OutcomeTracking/**`; `services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Persistence/Modules/OutcomeTracking/**`; `services/Diten.DecisionIntelligenceService/src/Diten.DecisionIntelligenceService.Infrastructure/Modules/OutcomeTracking/**` |

Host composition roots, solution/test projects, deployment and configuration paths remain blocked until an
approved implementation slice enumerates them. Shared code may contain only infrastructure abstractions that
carry no module entity, repository, permission, business rule or persistence ownership.

## Module isolation and persistence ownership

- The two primary FP&A modules use distinct root namespaces in every layer. Direct project/type references
  from one module's Domain, Application, Persistence or Infrastructure surface to the other's corresponding
  surface are forbidden.
- A module cannot inject, resolve, wrap or call the other module's repositories or Mongo database context; it
  cannot read or write the other module's collections, indexes, idempotency records, audit intents or outbox.
- Every Mongo collection and index has exactly one module owner recorded in a fail-closed allowlist. Exact
  collection/index names are a promotion blocker; no shared collection or discriminator-based co-ownership is allowed.
- Repository interfaces remain in the owning Domain surface and implementations in the owning Persistence
  surface. `MongoDB.Driver`/`MongoDB.Bson` access remains confined to Persistence.
- Mongo sessions and replica-set transactions are module-local. A transaction may include only the owning
  module's aggregate records, idempotency/fence records and local audit intent/outbox. There is no
  cross-module or cross-service Mongo transaction.
- Cross-module coordination uses an approved typed/versioned Application contract. It cannot return an entity,
  repository, collection handle, driver/session object or foreign persistence DTO.
- MOD-0072 follows the same rules in its separate service and has no shared database model or transaction with FP&A.

Tenant-owned persisted objects use the logical `EntityBase` contract from
[entity-base-template](../../../.antigravity/rules/entity-base-template.md): `Id`, server-derived `TenantId`,
`IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt` and technical concurrency `Version`. User-aware business
records add server-derived actor provenance as required by the standard. A concrete service-local base type is
a promotion blocker and cannot be imported from `Diten.EnterpriseStrategyService` or another service.

## Contracts, S2S, audit and event boundaries

- Budgeting ↔ ScenarioPlanning and FP&A ↔ OutcomeTracking communication is only through producer-owned,
  typed/versioned Application reference contracts with exact supported-version and fail-closed compatibility
  behavior. Foreign domain entities, mutable payload copies and shared persistence models are forbidden.
- Gate I producer contracts follow [DCP-006 AD-10](../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md#ad-10--gate-i-staged-contract-authority-and-common-baseline): server-derived tenant/actor, dedicated authenticated
  S2S identity, separately validated delegated actor where applicable, exact audience/scope/permission,
  correlation, idempotency and explicit compatibility policy.
- Internal S2S provider endpoints are service-network-only. They receive no browser surface and no Gateway
  route; browser traffic cannot call or proxy them. Service identity never bypasses tenant, delegated-actor,
  visibility or exact-permission evaluation.
- A business mutation and its producer-local audit intent/outbox are committed atomically in the owning
  module's Mongo replica-set transaction. Audit-intent persistence failure rolls back the business mutation.
- Delivery occurs only after commit through the approved shared `IEventBus`/outbox abstraction. It is
  asynchronous, at-least-once and requires idempotent consumers; direct writes to Platform audit/event
  collections, direct broker publishing and a distributed transaction are forbidden.
- `Diten.Platform.Common` authorization/tenancy primitives and `Diten.BuildingBlocks.Eventing` are shared
  technical evidence, not a license to share ESBP domain or persistence ownership. Exact package/version and
  integration wiring remain promotion blockers.

## Gate I routing and executability

- Gate I-A: mandatory MOD-0007, with MOD-0023 only when authoritative policy requires ApprovalOutcome.
- Gate I-B: MOD-0136 selected BudgetVersion and MOD-0138 scenario/comparator/selection references.
- Gate I-C: MOD-0072 outcome/realization references; planned BenefitCommitment remains MOD-0117-owned.

Gate I producer authoring may run in parallel. No MOD-0117 consumer adapter, provider client, executable
fixture, route or background worker may be activated before the relevant producer pack and exact contract are
approved, matching producer runtime evidence exists, the matching MOD-0117 consumer amendment is approved and
explicit runtime authority is recorded. Schema-only governance fixtures remain non-executable. DCP-006 OD-04
remains `OPEN / PARTIAL`.

## Architecture and isolation test matrix

Every row is mandatory before runtime promotion. Any FP&A primary-profile failure selects the complete
fail-closed fallback; waivers and partial activation are not permitted.

| Test ID | Exact invariant | Required negative proof |
|---|---|---|
| `ESBP-TOP-001` | Exactly one FP&A profile is configured | Startup rejects zero, primary+fallback, partial fallback or mixed service discovery/deployment registration |
| `ESBP-TOP-002` | Budgeting and ScenarioPlanning have separate five-layer namespace/project surfaces | Static dependency test rejects cross-module Domain/Application/Persistence/Infrastructure type references |
| `ESBP-TOP-003` | Repository ownership is module-local | DI graph and source scan reject resolving, injecting or wrapping the other module's repository/context |
| `ESBP-TOP-004` | Collection and index ownership is exclusive | Owner-manifest/integration test rejects shared names, foreign collection access and missing tenant-first indexes |
| `ESBP-TOP-005` | Transaction ownership is module-local | Instrumented replica-set test rejects a foreign module collection/session participant and proves rollback includes local audit intent/outbox |
| `ESBP-TOP-006` | Cross-module calls are typed/versioned contract-only | Architecture/serialization tests reject entity, repository, Mongo/session or persistence DTO leakage and unknown versions fail closed |
| `ESBP-TOP-007` | OutcomeTracking is isolated in DecisionIntelligence | References, DI graph, collection manifests and transaction tests reject FP&A domain/persistence sharing or FPA placement |
| `ESBP-TOP-008` | EnterpriseStrategy is prohibited | Project-reference, namespace, DI, collection-owner and deployment scans find no MOD-0136/0138/0072 authority in `Diten.EnterpriseStrategyService` |
| `ESBP-TOP-009` | Tenant and authorization boundaries fail closed | Server-derived tenant/actor, cross-tenant/invisible `404`, permission/S2S `403`, exact audience/scope and delegated-actor tests pass |
| `ESBP-TOP-010` | Internal S2S providers are not public | Gateway config/browser route scan finds no internal provider route; direct browser-origin request is rejected |
| `ESBP-TOP-011` | Audit and delivery respect the commit boundary | Fault injection proves audit/outbox failure rolls back mutation; publisher runs post-commit and duplicate delivery is idempotent |
| `ESBP-TOP-012` | PPM remains a reference consumer only | MOD-0117 adapter tests reject copied producer truth and remain non-executable until producer runtime-evidence gates pass |
| `ESBP-TOP-013` | Runtime allocation is explicit | Config/deployment scans reject guessed port, hardcoded credential, unresolved service discovery or unapproved route/authority |

## Prototype and migration classification

| Existing evidence | Classification | Governance disposition |
|---|---|---|
| Enterprise Strategy `DecisionReference` fields and frontend mock references | Prototype/legacy evidence; MOD-0007 ownership hazard; migration candidate | Not an authoritative DecisionReference contract or SoR |
| `BudgetEnvelope`, `BudgetAmount`, `FundingSource` and yearly budget fields | Prototype/legacy evidence; MOD-0136 ownership hazard; migration candidate | Not an authoritative budget/version/selection baseline |
| `ScenarioType` and scenario planning fields | Prototype/legacy evidence; MOD-0138 ownership hazard; migration candidate | Not an authoritative Scenario/Comparator runtime |
| `ActualValue`, benefit-realization dates and related fields | Prototype/legacy evidence; MOD-0072 ownership hazard; migration candidate | Not authoritative actual outcome/period/evidence/realized-state truth |
| Strategy/objective/cascade fields within MOD-0352 boundary | Potential domain evidence | Still requires an approved MOD-0352 pack/slice before runtime authority |
| `TaskAggregate`, local approval and approve/assign/escalate surfaces | Ownership hazard; quarantine | MOD-0024/MOD-0023/DCP-004 boundaries; Gate 2 before any production change |
| Enterprise Strategy fields unrelated to decision, budget, scenario, realization or protected hazards | Unrelated to this Gate I reconciliation | No authority or migration disposition is inferred here |

Any migration requires a separately approved expand/contract slice, source provenance, tenant mapping,
deterministic transformation, quarantine, reconciliation, rollback and proof that the legacy field is not a
second SoR. Nothing is moved, deleted or modified by this decision.

## WorkCenter and approval boundary

TaskAggregate, WorkCenter projection, DWS execution lifecycle/assignment, ApprovalTask,
approve/reject/delegate/request-info/cancel route/command/UI and local `ApprovedAt`/`ApprovedBy` behavior remain
`QUARANTINE`. This governance-only task does not trigger Gate 2; Gate 2 is mandatory immediately before the
first production DTO/API/UI/projection/migration/deletion/deprecation change. Pure hierarchy/order/structural
dependency is not itself a Gate 2 hazard.

## Repo scope and protected paths

This governance task may change only
`execution/domains/enterprise-strategy-business-performance/domain-config.md`. Future implementation scope is
limited to the selected logical profile above and only after the relevant pack/runtime gates are satisfied.

Protected for this task and until separately authorized:

- `.antigravity/**`
- `services/**`, including `services/Diten.EnterpriseStrategyService/**`
- `frontend/**`, including every WorkCenter surface and archive/frozen path
- `gateway/**`, especially `gateway/Diten.ApiGateway/**/ocelot.json`
- `execution/domains/enterprise-strategy-business-performance/module-packs/**`
- other domain governance, registries, portfolio/DCP files and root governance indexes
- runtime configuration, service discovery, credentials, deployment manifests, collection migrations and ports

## Promotion, runtime authority and open blockers

- DCP-006 remains `approved`; MOD-0117 remains `review`; OD-04 remains `OPEN / PARTIAL`.
- MOD-0136, MOD-0138 and MOD-0072 retain their current pack statuses, `service: TBD` and
  `production_authority: none`. This domain decision does not promote them or make an adapter executable.
- Each pack must be reconciled to this domain-level topology authority before promotion. No implementation
  begins before its pack is `approved`/`ready-for-dev`, every applicable test above is executable and passing,
  and explicit user runtime authority names the selected topology and implementation slice.
- Ports remain `TBD`: the current authoritative repo registry assigns no collision-free port to
  `Diten.FpaService`, either fallback service or `Diten.DecisionIntelligenceService`. A future authorized
  integration task must reserve ports in the authoritative registry and prove collision freedom before route work.
- B-class blockers are: exact collection/index names and owner manifests; concrete service-local `EntityBase`;
  host composition/test/deployment paths; topology-profile configuration; Mongo replica-set/transaction,
  idempotency and unknown-commit design; local audit-intent/outbox integration; exact package/version wiring;
  dedicated S2S credentials, audience/scope, delegated actor and permission provisioning; service discovery,
  health/operations and deployment ownership; port allocation; public browser-route design (separate from
  internal S2S); prototype migration/quarantine; approved producer contracts and matching MOD-0117 consumer
  amendments/runtime evidence.
- No Gateway route, WorkCenter change, service scaffold, collection, credential, deployment or runtime literal
  may be inferred from this governance decision.
