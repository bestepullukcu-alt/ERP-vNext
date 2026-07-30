---
id: DCP-006
slug: portfolio-delivery-process-core
name: Portfolio, Delivery & Process Core
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: cross-domain
owner: ali.tufanoglu / enterprise-architect
branch: feature/es/enterprise-strategy
created: 2026-07-28
identity_allocation: enterprise-architect-explicit
gate_1: dcp-005-workcenter-boundary-pass-carried-forward
gate_2: required-before-protected-hazards
---

# DCP-006 — Portfolio, Delivery & Process Core

> **Artifact type:** Governance and orchestration contract only. This is not a runtime entity, module pack,
> MOD-0014 Capability Group or code authorization.

> **Approved / implementation guard:** Approval authorizes only the governed next-step work allowed by this
> DCP's prerequisites. It does not by itself authorize production code, service/domain scaffolding, migration,
> gateway changes or data mutation. Every production slice still requires its code-owning module pack to be
> approved/ready-for-dev and all applicable open-decision, domain-config and Control Tower gates to be closed.

## 1. Identity and status

| Field | Value |
|---|---|
| ID | `DCP-006` |
| Slug | `portfolio-delivery-process-core` |
| Name | Portfolio, Delivery & Process Core |
| Standard | CAP-001 |
| Status | `approved` — Enterprise Architect, 2026-07-28 |
| Scope | Active Management & Governance subdomains 1.3, 1.4 and 1.6 |
| Owner | Enterprise Architect; participating domain owners remain accountable for their SoRs |
| Branch | `feature/es/enterprise-strategy` |
| Identity authority | Explicit, singular Enterprise Architect allocation dated 2026-07-28 |
| Allocation rule | This allocation is not inferred from the highest existing DCP number |
| Collision check | Path and repository identity search clear on 2026-07-28 |
| Gate 1 | DCP-005 WorkCenter boundary review `CONDITIONAL PASS` 2026-07-27 → `PASS` 2026-07-28; only the unchanged four WorkCenter hazard boundaries are carried into this DCP |
| Gate 2 | Required immediately before any protected hazard in §15 |

### Identity allocation record

The Enterprise Architect explicitly allocated `DCP-006` to this pack on 2026-07-28. The allocation is
exclusive to:

- 1.3 Portfolio, Investment & Value Management;
- 1.4 Delivery & Execution Management; and
- 1.6 Business Process & Operational Management.

`DCP-005` remains approved as the historical/foundation governance and Gate provenance source. Per the
2026-07-28 scope partition, DCP-003 remains only a
deferred, non-executable legacy safe-parity planning source; its `draft` status grants no production
implementation authority. DCP-006 is the sole active orchestration contract for the
1.3/1.4/1.6 delivery scope, but it neither supersedes nor changes DCP-005 until a later explicit governance
decision records that relationship.

## 2. Business outcome

Deliver a governed, auditable chain in which portfolio intent becomes a finite delivery plan, the plan uses
reusable structural mechanics, and repeatable operations bind to controlled process definitions without
creating duplicate portfolio, project, task, workflow, approval, evidence or WorkCenter systems.

The minimum integrated proof is:

```text
MOD-0117 portfolio/investment decision
  → MOD-0117 initiative/project
  → DWS immutable structural baseline
  → BPM published process-model version
  → typed MOD-0024 task / MOD-0023 workflow / MOD-0031 evidence links
  → auditable portfolio value and delivery projection
```

## 3. Problem statement

The audited domain model describes a coherent Management & Governance domain, but current evidence is split
across canonical Blueprint Master 8.1, historical Master 7 evidence, DCP-003/004/005 and an existing ES service
whose code presence is not proof of correct ownership or production readiness.

- MOD-0117 is the canonical PPM SoR, while current ES Demand and delivery code can form competing lifecycles.
- Existing DWS code contains task-like status, responsible, due-date and dependency fields plus local
  `ApprovedAt`/`ApprovedBy` and `ApproveStructureAsync` behavior.
- Existing ES `TaskAggregate` conflicts with generic task/checklist ownership.
- BPM runtime evidence is insufficient; workflow runtime cannot be renamed as BPM.
- The current PPM frontend adapter/mock is not evidence of a real cross-service integration.
- Deprecated candidate aliases are governance provenance only and must never leak into runtime literals.

Without one successor DCP, agents can independently implement plausible slices that duplicate SoRs or cross
the WorkCenter Control Tower hazards.

## 4. Capability boundary

### 4.1 In scope

| Subdomain | Active boundary |
|---|---|
| 1.3 Portfolio, Investment & Value | Portfolio, investment, funding alignment, benefit/value realization and typed budget/scenario/outcome references |
| 1.4 Delivery & Execution | Initiative/program/project ownership plus DWS structural definition, hierarchy, ordering, structural dependency, baseline/version/compare |
| 1.6 Business Process & Operational | Process architecture, domain/family, process, model/version, activity, control point and typed role/KPI binding |

Also in scope: interface baselines, ownership enforcement, migration/retirement planning, negative acceptance
criteria, gates, architecture tests and one integrated golden-flow proof.

### 4.2 Participating capabilities

| Identity | Role |
|---|---|
| `MOD-0117 — Project & Portfolio Management (PPM)` | Canonical PPM parent and Demand SoR; portfolio/program/project/initiative/benefit/value context |
| `MOD-0018` | Authorization, RBAC/ABAC and authoritative effective-permission evaluation; consumed, not reimplemented |
| `MOD-0021` | Immutable audit-event contract; consumed, not reimplemented |
| `MOD-0136` | Budget and budget-version SoR |
| `MOD-0138` | Scenario and comparator-output SoR |
| `MOD-0072` | Outcome/value and decision-to-outcome linkage |
| `MOD-0354` | Decomposition & Work Structuring Engine |
| `MOD-0355` | Business Process Architecture & Modeling |
| `MOD-0023` | Workflow runs, approval decisions, approver eligibility/delegation, SLA and escalation; consumes MOD-0018's authoritative permission result |
| `MOD-0024` | Generic task/checklist mechanics |
| `MOD-0029` | SOP/work-instruction controlled state |
| `MOD-0031` | Evidence objects, links and provenance |
| `MOD-0048` | Canonical reference-data contract; consumed, not reimplemented |
| `MOD-0059`–`MOD-0061` | KPI/metric definitions and derivative scorecard bindings |
| `MOD-0288` | Person/position/organization typed references |
| DCP-004 / `CAND-CAP-0006` | Separate WorkCenter aggregation/projection boundary |

`CAND-CAP-0007-FU01` is not an automatic prerequisite. It participates only if an approved slice touches
existing ES containment, tenancy or migration.

Subdomain 1.2 and Demand implementation are outside DCP-006. DCP-006 may use only a typed
portfolio-transition/reference to the MOD-0117 parent. Demand implementation remains outside both DCP-006
and DCP-003; it requires a new explicit Demand DCP/FU decision, parent-aware preflight, PPM domain owner
approval and Enterprise Architect approval.

## 5. Member modules and follow-ups

| Lane | Member | Delivery role | Identity state |
|---|---|---|---|
| P | MOD-0117 — Project & Portfolio Management (PPM) | Portfolio/investment/value and finite project baseline | Blueprint 8.1 canonical |
| P | MOD-0136 | Budget/version adapter | Blueprint 8.1 canonical |
| P | MOD-0138 | Scenario/comparator adapter | Blueprint 8.1 canonical |
| P | MOD-0072 | Outcome/value adapter | Blueprint 8.1 canonical |
| D | MOD-0354 | DWS Wave 1 structural mechanics | Blueprint 8.1 canonical; draft pack |
| B | MOD-0355 | BPM Wave 1 process model/version/publish | Blueprint 8.1 canonical; pack not yet authored |
| S | MOD-0018/0021/0023/0024/0029/0031/0048/0059–0061/0288 | Shared typed contracts | Dependencies; not reimplemented |

Blueprint Master 8.1 contains `MOD-0354` and `MOD-0355`; the Enterprise Architect has canonicalized the
historical `CAND-CAP-0008/0009` identities to those exact modules. Deprecated candidate aliases remain
governance provenance only and no new MOD or FU number is implied by this DCP.

## 6. Ownership map

| Object / behavior | Canonical SoR | Allowed here | Forbidden duplicate |
|---|---|---|---|
| Demand/idea | MOD-0117 — Project & Portfolio Management (PPM) | Typed portfolio-transition/reference only | Demand implementation or ES-native competing lifecycle |
| Portfolio/investment/benefit/value | MOD-0117 | Owned PPM behavior | DWS/BPM copy as truth |
| Budget/version | MOD-0136 | Versioned typed reference/adapter | PPM-local budget SoR |
| Scenario/comparator | MOD-0138 | Versioned typed reference/adapter | PPM-local scenario engine |
| Outcome/value tracking | MOD-0072 | Typed linkage | Browser-only outcome truth |
| Initiative/program/project | MOD-0117 | Owned PPM lifecycle | DWS project header as SoR |
| Structure definition/template/instance/node | MOD-0354 | Structural mechanics | Task or approval lifecycle |
| Generic task/checklist | MOD-0024 | Typed link/generation request | DWS node or ES TaskAggregate as second task SoR |
| Process architecture/model/version | MOD-0355 | Definition and publish semantics | Workflow-run engine |
| Workflow run/approval decision and approver eligibility/delegation | MOD-0023 | Submit/reference/apply authoritative outcome; consume MOD-0018 permission result | Local approval/eligibility/delegation decision |
| Authorization/RBAC/ABAC/effective permission | MOD-0018 | Consume authoritative permission result | DWS, BPM or WorkCenter permission recalculation |
| Immutable audit event | MOD-0021 | Emit/consume authoritative event contract | DWS/BPM-local competing audit-event standard |
| SOP/work instruction | MOD-0029 | Typed version/state reference | BPM document payload copy |
| Evidence | MOD-0031/shared document modules | Typed evidence link | Local evidence payload store |
| Canonical reference data | MOD-0048 | Typed reference-data lookup | DWS/BPM-local canonical lookup truth |
| KPI/metric | MOD-0059/0060 | Certified typed binding | BPM-local KPI definition |
| Dashboard | MOD-0061 | Derivative projection | Operational SoR |
| Person/position/org | MOD-0288 | Typed principal reference | `ResponsibleName` identity truth |
| WorkCenter item/overlay | DCP-004 / CAND-CAP-0006 | Separate approved projection only | Native lifecycle or decision truth |

## 7. Dependency graph

```text
EA DCP-006 allocation + collision PASS
             │
             ▼
       DCP-006 approval
             │
      shared contract baseline
        ┌────┴───────────┐
        ▼                ▼
 MOD-0117 minimal PPM   security/tenant foundation when applicable
        │
        ├───────────────┐
        ▼               ▼
 DWS structural W1   BPM model/version W1
        └──────┬────────┘
               ▼
 typed task/workflow/evidence adapters
               │
               ▼
 integrated golden flow + Gate 3
```

WorkCenter delivery remains outside this graph behind DCP-004. Gate 2 is inserted before the first protected
hazard, wherever that hazard first occurs.

## 8. Ordered delivery sequence

### Slice 0 — Identity and successor-DCP approval

- Confirm DCP-006 allocation, collision freedom and CAP-001 completeness.
- Record DCP-006 approval without changing DCP-005; the formal DCP-005/DCP-006 relationship remains OD-06.
- Resolve actual Master 8.1 authority before any candidate-to-MOD transition.

### Slice 1 — Minimal shared contracts and foundation

- Freeze typed ID/query/versioned-event contracts.
- Define tenant, authorization, audit, concurrency, idempotency and failure semantics.
- Invoke CAND-CAP-0007-FU01 only for approved ES containment/migration work.

### Slice 2 — MOD-0117 Portfolio, Investment & Value minimum

- OD-07 scope partition and portfolio-delivery domain-config reconciliation are `PASS` as of 2026-07-28.
- OD-03 is `CLOSED` as of 2026-07-29. The permanent institutional business-owner role is
  **Portfolio Governance Process Owner (PPM Business Owner)**; the Enterprise Architect remains technical/
  governance owner and does not provide PPM business acceptance.
- **Phase 2A — Context & Referenceability Core (implementation authorized 2026-07-29):** user-approved
  `Diten.PpmService` scaffold, Portfolio/Initiative/Program/Project backend and tenant UI, Mongo persistence,
  tenant isolation, concurrency, lifecycle-derived referenceability, transactional producer-local audit
  intent/outbox foundation and gateway-ready API contracts. Service port is `5061`; browser traffic remains
  frontend `5001` → Gateway `5000`, never direct `5061`.
- **Phase 2B — Investment & Value Linkage:** `InvestmentDecision` and `BenefitValueLink`; MOD-0136 budget,
  MOD-0138 scenario and MOD-0072 outcome/value are consumed only as typed references. Their systems of
  record are not copied into MOD-0117 and no fake budget/scenario/outcome lifecycle is permitted.
- MOD-0117 pack is in `review` for Phase 2A only. ExternalContextReference provider endpoint/transport, DWS
  runtime integration, Phase 2B, Gateway-file work beyond the exact mapping authorized below and WorkCenter
  hazards remain unauthorized. MOD-0117 is not complete until separately authorized Phase 2B is complete.
- **Phase 2A PPM object API Gateway mapping authorized; integration-agent only.** Scope is exactly
  `/api/v1/ppm` plus `/api/v1/ppm/{everything}` to downstream port `5061`; no other route authority is
  created.

### Slice 3 — DWS Wave 1 structural mechanics

- Structure definition/template/instance/node.
- Hierarchy, ordering, structural dependencies, validation and immutable baseline/version/compare.
- No node execution lifecycle, assignment, due-date/progress, task action or local approval.
- Its OD-04 runtime subset is limited to MOD-0117 typed external-context validation, MOD-0018 authoritative
  permission enforcement/evaluation integration and a versioned MOD-0021 audit append/event contract.
  MOD-0023/0024 are prohibition boundaries; MOD-0031/0288 are unused and MOD-0048 is `N/A` in Wave 1.

### Slice 4 — BPM Wave 1 model/version/publish

- Process architecture/domain/family/process/model/version.
- Activity, control point, typed role and certified KPI binding.
- No workflow run, approval task, SLA or escalation engine.

### Slice 5 — Typed shared-module integration

- MOD-0024 task link/generation request.
- MOD-0023 workflow submission and authoritative outcome.
- MOD-0031 evidence and MOD-0029 controlled-document references.
- No shared helper that smuggles task/approval/workflow ownership into DWS or BPM.

### Slice 6 — Integrated golden flow

Prove Portfolio → project → DWS immutable baseline → BPM published version, with typed shared-module
references and tenant/audit/concurrency/failure evidence.

### Slice 7 — Merge assurance

- Close regression debt in the same slice; it cannot move to a later slice.
- Run architecture and runtime evidence gates.
- Obtain Gate 3 immediately before each applicable ES and WorkCenter feature branch is merged into `main`;
  this does not mean merging the two feature branches into each other.

## 9. Prerequisites

1. DCP-006 is `approved` or `ready-for-execution`.
2. Each production slice has an approved/ready-for-dev module pack.
3. MOD-0117, MOD-0354 and MOD-0355 pass DCP-002 canonical preflight at authoring time.
4. Candidate-to-MOD transition is blocked until authoritative Master 8.1 evidence exists.
5. Shared interface versions, failure semantics and owners are approved.
6. Gate 2 PASS exists before any §15 protected hazard.
7. Real MongoDB, authenticated HTTP, tenant isolation and rollback-capable test environments exist.
8. `PASS — 2026-07-28:` OD-07 scope partition is recorded and
   `execution/domains/portfolio-delivery/domain-config.md` is reconciled. DCP-003 is deferred and
   non-executable; DCP-006 is the sole active 1.3/MOD-0117 orchestration contract.
9. `PASS — 2026-07-28:` OD-08 closed by the separate `management-governance` domain scaffold. DWS/BPM
   module-pack authoring still requires separate draft packs and human approval; this reconciliation grants
   no service or production authority.

## 10. Architecture decisions

### AD-01 — Service placement

- 1.3 future production placement: `Diten.PpmService`.
- Initial placement is a planned `Diten.ManagementGovernanceService` modular monolith with independent
  `Dws` and `ProcessModeling` internal modules. The service does not exist and this decision does not
  authorize scaffolding.
- They may not reference each other's domain types, share repositories/collections, or share
  approval/task/workflow helpers.
- Cross-module communication is limited to typed IDs, query contracts and versioned events.
- Permission and collection families remain separate.
- Mandatory module-pack architecture tests must enforce these constraints. If they cannot, service
  scaffolding is blocked fail-closed and DWS/BPM must use separate services.

This decision does not authorize service scaffolding.

### AD-02 — Runtime naming

Candidate IDs are never runtime literals. Pending canonical allocation, technical families are:

- `management-governance.dws.*`
- `management-governance.process-modeling.*`

### AD-03 — DWS Wave 1

DWS Wave 1 is structural only. Pure structural dependency between structure nodes is allowed and is not a
Gate 2 hazard. Task/execution dependency and task-like status/dates/progress/assignment/lifecycle behavior
are excluded and require Gate 2, even where current code or an older specification contains them.

### AD-04 — BPM is not workflow

BPM owns process definition and version semantics. MOD-0023 owns workflow definitions/runs and approval
decisions; MOD-0024 owns operational tasks.

### AD-05 — Tenancy and migration

MongoDB tenant-owned records require server-resolved `TenantId`. Records without deterministic tenant
ownership are quarantined, never assigned to a default tenant. `Diten.Platform.Common.BaseEntity` is not
copied into ES while BL-030 remains unresolved.

### AD-06 — APPROVED GOVERNANCE BASELINE — NOT A RUNTIME CONTRACT: MOD-0117 ExternalContextReference v1

The governance-approved transport-independent contract boundary consumed by MOD-0354 Wave 1 is:

| Contract element | Approved v1 governance rule |
|---|---|
| Contract name | `ppm.external-context-reference` |
| Contract version | `1.0` |
| `ContextKind` | Closed set: `Portfolio`, `Initiative`, `Program`, `Project` |
| `ContextId` | Canonical non-empty `Guid`; opaque to MOD-0354 |
| Tenant and actor | `TenantId` and `ActorId` come only from authenticated server context and are never trusted from a client payload |

MOD-0354 may not infer domain, hierarchy, ownership or existence from `ContextId`. Demand, task, workflow,
approval and free-text discriminators are not valid external contexts. Creating a `StructureDefinition`
requires authoritative MOD-0117 validation of the exact kind/ID under the server-resolved tenant and actor.
Soft-deleted MOD-0117 objects cannot receive new references. Validation fails closed: no local cache or
locally inferred ownership/existence result may substitute for the authoritative validator.

Once the definition is created, its `ExternalContextReference` is immutable. A later context requires a new
definition. Soft deletion of the referenced MOD-0117 object does not delete or rewrite previously sealed
MOD-0354 revision/baseline history.

Failure semantics are `400` for invalid shape/kind; `403` when an authenticated actor lacks the
required DWS command permission, decided by MOD-0018 enforcement; `404` when the MOD-0117 context is absent,
soft-deleted, cross-tenant or not visible/referenceable to the actor; `409` for attempted reference replacement;
and `503` when the authoritative validator is unavailable. The MOD-0117 validator does not evaluate DWS
permissions, and context invisibility never returns `403` or discloses object existence. This baseline does not
select an endpoint, route, controller, service port, transport technology or physical API shape; those remain
implementation-pack decisions.

This is an approved governance baseline, not a runtime contract or runtime-availability claim. It grants no
endpoint, route, port, timeout, retry, transport, service-scaffold or production implementation authority.

### AD-07 — MOD-0018 permission-enforcement integration

AuthService remains the permission-grant and signed-JWT issuance system of record. Tenant services enforce the
signed JWT permission claim locally and fail closed through a PSS-approved reusable in-process
handler/policy/evaluator. Platform/AuthService service-specific filter or evaluator code cannot be copied into
DWS. Wave 1 neither requires nor designs a synchronous AuthService or remote decision call on the enforcement
hot path. `IEntitlementChecker` remains limited to module/feature entitlement and cannot be used as a permission
evaluator; JWT freshness and revocation follow MOD-0018-FU13.

DWS consumes this enforcement result and never calculates roles, grants, RBAC/ABAC or effective permission.
Because the reusable shared integration has not yet been allocated and implemented, the MOD-0018 portion of
the DWS Wave 1 blocker is `PARTIAL`. OD-04 remains open. This decision allocates no new follow-up identity and
grants no production authority.

The permanent MOD-0117 business-owner role is **Portfolio Governance Process Owner (PPM Business Owner)**.
The Enterprise Architect remains technical/governance owner only and does not replace PPM business acceptance.
AD-06 closes the contract-shape governance question but only narrows the DWS portion of OD-04; OD-04 remains
open and no production authority is granted.

### AD-08 — MOD-0021 cross-service audit integration

Identity decision A applies: the existing file remains the canonical
`MOD-0021 — Audit Trail Service` parent pack, while “General Audit Trail” describes its current delivery
phase/slice and `MOD-0021-PLAN` remains a non-executable planning artifact.

For a required audited DWS mutation, the mutation and producer-local technical audit intent/outbox persist
in the same replica-set Mongo transaction. Failure to persist the local intent prevents commit. After commit,
a versioned semantic integration event is published asynchronously to an idempotent MOD-0021 consumer.
Delivery is durable at-least-once; exactly-once is not claimed. Broker, consumer or Platform failure after
commit does not roll back the mutation or sealed baseline and is handled through retry, dead-letter, alarm
and authorized replay.

DWS cannot write directly to Platform `audit_outbox` or `audit_events`. The existing shared-key
`/api/internal/audit/append` endpoint is not the DWS Wave 1 authoritative baseline. Publisher service
identity, tenant and actor are bound fail-closed from authenticated server/transport context and matched to
the permitted source/module. Payloads are minimal and allowlist-based, have explicit byte/depth/count/string
limits and redaction, and cannot contain a full DWS tree/revision snapshot or unrestricted dictionaries.

For MOD-0117, the approved event identity is `PpmAuditIntentSubmittedV1` with EventName/routing key
`ppm.audit-intent.submitted.v1`. Its final **Minimal Mutation Audit v1** payload contains exactly
`auditIntentId`, `actorId`, `entityType`, `entityId`, `mutation` and `occurredAtUtc`; the MOD-0021 consumer
mapping is fixed to that contract. Compatibility fixtures and authenticated publisher credentials remain
runtime evidence gates. PPM handlers/controllers cannot call RabbitMQ
or MassTransit directly. A producer worker may publish only through MOD-0035's public `IEventBus` plus
outbox abstraction.
The versioned HMAC signs, in exact newline-delimited UTF-8 order, scheme, EventId, EventName, EventVersion,
TenantId, CorrelationId, Producer, CausationId (or literal `-`), OccurredAtUtc and payload byte length,
followed by exact canonical payload bytes; only a lowercase 64-hex signature is valid.

The integration governance design is approved, but the MOD-0021 subset remains `PARTIAL` until the
implementation, compatibility, transaction, idempotency, security, payload-limit,
dead-letter/replay and observability evidence exist. OD-04 remains open. This decision allocates no FU and
grants no implementation or production authority.

### AD-09 — MOD-0117 tenant-entitlement and user-permission gates

PPM access requires two independent, ordered, fail-closed decisions:

1. The server-resolved tenant must have an active PPM module entitlement. Disabled, suspended, expired,
   missing or indeterminate entitlement denies access immediately.
2. Within an entitled tenant, the authenticated actor must carry the required canonical `ppm.*` permission
   granted through AuthService's tenant-scoped `RolePermission` mechanism.

The canonical tenant-module entitlement/catalog identity is exactly `ModuleCode = PPM`. Lowercase `ppm.*`
values are permission keys, not aliases for the module-code contract.

The MOD-0117 Phase 2A permission contract is the exact closed set
`ppm.portfolios.read`, `ppm.portfolios.create`, `ppm.portfolios.update`,
`ppm.portfolios.change-lifecycle`, `ppm.initiatives.read`, `ppm.initiatives.create`,
`ppm.initiatives.update`, `ppm.initiatives.change-lifecycle`, `ppm.programs.read`,
`ppm.programs.create`, `ppm.programs.update`, `ppm.programs.change-lifecycle`,
`ppm.projects.read`, `ppm.projects.create`, `ppm.projects.update`, and
`ppm.projects.change-lifecycle`. These 16 lowercase-dotted keys may be registered in the global AuthService
permission catalog. Catalog presence grants no access. PPM is not added to FU9's locked Auth+MDM default
grant template, and tenant administrators or Viewer roles receive no implicit PPM grant. Wildcards, alias
keys, raw-token exposure, role-name bypasses and hard-coded allow paths are forbidden. In particular,
`ppm.portfolios.archive` is non-canonical and must be reconciled to
`ppm.portfolios.change-lifecycle` on the PPM branch; the PSS slice cannot create an alias. Phase 2B
investment/benefit and external-context permission keys remain outside this runtime slice.

`IEntitlementChecker` owns only module/feature entitlement evaluation; AuthService remains the permission
grant and signed-JWT permission-claim SoR. PPM consumes both decisions and recalculates neither. Entitlement
denial returns `403`, matching the existing Platform tenant-module enforcement standard and disclosing no
commercial status detail; permission denial also returns `403`. Only an authorized request proceeds to
object lookup, where missing/soft-deleted/cross-tenant objects remain indistinguishable `404`.

Entitlement removal blocks access immediately at the entitlement gate even when a stale JWT still contains
`ppm.*`. Existing tenant-scoped `RolePermission` rows remain dormant rather than being deleted: commercial
entitlement and RBAC configuration remain separate SoRs. Entitlement-cache invalidation must reach every
instance; token refresh/revocation follows MOD-0018-FU13 and cannot substitute for the immediate entitlement
deny.

**Code-reality correction:** the current generic AuthService entitlement bridge cannot implement this PPM
contract unchanged. It automatically grants entitled module keys to the default Admin and Viewer roles, and
its revoke/reconcile path deletes module-source grant rows. PPM entitlement enablement must create no
permission grant; grant creation is only an explicit, tenant-scoped, auditable management operation. PPM
entitlement removal must not invoke destructive grant removal.

The implementation choice remains open: a PPM-specific bridge strategy or a generic bridge revision.
Existing MDM and other module behavior cannot change under this DCP. A generic revision requires broader PSS
migration/regression approval. With dormant grants, later PPM re-entitlement could reactivate historical
access; before implementation, human review must decide whether reactivation is automatic or requires
re-approval after reviewing grant inventory, current role membership, least privilege and audit history.

**Control Tower resolution:** use the PPM-specific strategy. Existing MDM/other-module generic behavior is
unchanged. On re-entitlement, only still-existing explicit grants held by current role memberships become
effective; deleted grants are not reconstructed and no automatic grant is created. Re-entitlement and the
administrator's read-only current grant/role inventory view are audited.

#### Runtime authorization handoff

This cross-domain slice uses existing pack identities; it does not allocate a new MOD/FU/DCP.

| Atomic delivery | Authoritative pack(s) | Allowed runtime scope after explicit approval |
|---|---|---|
| PSS entitlement and PPM-specific grant strategy | CAND-CAP-0002-FU05, MOD-0018, MOD-0018-FU9/FU13 | `services/Diten.Platform/**`, `services/Diten.AuthService/**`, narrowly required `services/Diten.Platform.Common/**`, and their tests |
| Shared event mechanics and MOD-0021 consumer | MOD-0035, MOD-0021 | `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/**`, narrow Platform eventing/audit consumer paths and tests; Platform is consumer-only for the PPM event |
| PPM producer-local worker/adapter and logical event | MOD-0117 | `services/Diten.PpmService/**`, including planned `services/Diten.PpmService/src/Diten.PpmService.Contracts/Events/**`, and its tests only |

PSS work uses a separate `feature/pss/...` branch/worktree. PPM producer work remains a separately
reviewable MOD-0117 atomic change; neither branch grants the other domain unrestricted service ownership.
Shared contract fixtures must be identical before integration. Gateway, frontend, MDM runtime,
`.antigravity/**`, WorkCenter, MOD-0354/MOD-0355 and unrelated service/domain paths are protected.

Runtime acceptance requires:

1. `ModuleCode = PPM`; enable/reconcile creates zero grants and removal deletes zero explicit grants.
2. The AuthService catalog exposes exactly the 16 canonical Phase 2A keys above—no missing, extra,
   duplicate, wildcard or alias key—and catalog registration creates zero grants. Only an explicit
   tenant-scoped auditable administration command creates/removes `ppm.*` grants.
3. Re-entitlement restores only current grant/current membership effect, creates nothing, and writes audit
   plus authorized read-only inventory evidence.
4. Entitlement is checked before permission; both deny as `403`; object lookup follows and preserves `404`.
5. PPM local mutation plus `ppm_audit_intents` is atomic; producer uses outbox/`IEventBus`, never direct
   RabbitMQ/MassTransit.
6. `PpmAuditIntentSubmittedV1` contract, authenticated identity binding, idempotent consumer, limits,
   compatibility, 5-total-attempt delivery, DLQ, alarm and authorized replay pass.
7. Existing MDM and other generic bridge grant/revoke tests are unchanged and green.

Required test matrix:

| Layer | Required evidence |
|---|---|
| AuthService unit/contract | Exact 16-key equality (no missing/extra/duplicate), rejection of wildcard/alias and `ppm.portfolios.archive`, catalog-presence-with-zero-grant, PPM dispatch bypasses generic auto-grant/revoke, no Admin/Viewer implicit grant, explicit grant audit, role/tenant isolation, re-entitlement current-grant behavior, MDM/generic regression |
| Platform entitlement | active/disabled/suspended/expired/missing decisions; every-instance invalidation; stale JWT cannot bypass; no permission mutation |
| PPM application/API | gate order; entitlement/permission `403`; object `404`; no role/effective-permission recomputation |
| Mongo integration | mutation+intent commit; intent failure rollback; worker lease/idempotency; no intent loss |
| MOD-0035/MOD-0021 contract | exact envelope/payload bytes, identity mismatch rejection, size/depth/unknown-field rejection, duplicate delivery, v1 compatibility |
| Broker failure | retry schedule, DLQ after fifth failure, alarm, authorized same-EventId replay, no duplicate AuditEvent |
| Architecture | no direct broker call from handler/controller; no Platform audit collection access from PPM; protected-domain diff scan |

MOD-0035 parent status remains `partial`, while its named **PPM Audit Transport Slice** is
`ready-for-dev`. Runtime starts only after the explicit user authorization sentence recorded in the handoff
report and in a separate PSS worktree.

Identity preflight is fail-closed: existing approved
`CAND-CAP-0002-FU05 — Tenant Module Entitlements` is reconciled in the ledger as child of
`CAND-CAP-0002`, with deprecated alias `MOD-0298`, owner `platform-shared-services`, and governance-only,
pending-EA, no-runtime-literal constraints. No replacement identity is invented.

Shared `EventEnvelope`, `IEventBus`, outbox and inbox mechanics are owned by
`Diten.BuildingBlocks.Eventing`. MOD-0117 owns the logical PPM event; Platform is consumer-only.
`Diten.Platform.Contracts` owns other Platform events where applicable, but not the PPM event.

The final Minimal Mutation Audit v1 evidence is limited to actor, minimal mutation, PPM aggregate and time;
it is not authorization/entitlement evidence, a business snapshot or lifecycle history. Delivery uses 5
total attempts: 10 seconds after the first failure, exponential backoff with jitter, and a 5-minute
maximum; the fifth failed attempt causes DLQ plus alarm. The initial attempt is included, leaving four retry attempts.

Authorized replay uses the same `EventId` and identical canonical bytes; changed bytes are rejected. If the
first delivery was not accepted, replay may create exactly one `AuditEvent`; accepted delivery creates
none. Idempotency is `ConsumerName + EventId`; unauthorized replay and replay UI/API are forbidden.

Future runtime work is PSS-owned: AuthService catalog/grant provisioning, Platform entitlement enforcement
and MOD-0035/MOD-0021 integration must be delivered on a separate PSS branch/worktree under an executable PSS
pack and explicit user approval. MOD-0117 authorizes no AuthService, Platform, MOD-0035 or Gateway runtime
change; Gateway remains integration-agent-only.

## 11. Scope

Authorized planning artifacts are ownership matrices, interface contracts, delivery sequencing, gates,
migration/retirement designs, test expectations and human-review questions for 1.3/1.4/1.6.

Future implementation is limited to paths explicitly named by approved member module packs. This DCP does
not expand any domain-config repo scope.

## 12. Explicit exclusions

- Subdomains 1.1, 1.2, 1.5, 1.7, 1.8, 1.9 and 1.10.
- Demand implementation and lifecycle. Only a typed MOD-0117 portfolio-transition/reference is allowed;
  future implementation requires a new explicit Demand DCP/FU decision, parent-aware preflight, PPM domain
  owner approval and Enterprise Architect approval; neither DCP-006 nor DCP-003 authorizes it.
- Strategy/Demand production migration except a separately approved containment/transition slice.
- Full R&D–RA/CTD Release 1.
- WorkCenter implementation, WC-5 or ES `IWorkItemProvider`.
- DWS node execution lifecycle or local approval.
- BPM workflow runtime, operational tasks, approval, SLA or escalation.
- Service/domain scaffolding, production code, module pack, gateway, registry or master-plan changes.
- Any invented MOD/PSS/NEW identity.

## 13. Governance drift risks

| Risk | Control |
|---|---|
| Treating reconciliation-only MOD-0354/0355 as canonical | Fail closed until actual Master 8.1 + DCP-002 proof |
| Scope partition regresses and DCP-003 becomes executable | OD-07 CLOSED record + reconciled domain docs; DCP-003 remains deferred/non-executable |
| ES Demand becomes second SoR | MOD-0117-only command ownership and transition tests |
| DWS becomes task engine | Negative schema/API/UI tests and architecture rules |
| BPM becomes workflow engine | Separate types, collections, permissions and contract tests |
| Local approval flags decide business state | Gate 2 + MOD-0023 authoritative outcome |
| WorkCenter becomes native lifecycle owner | DCP-004 boundary tests |
| Unknown tenant mapped to default | Quarantine count/checksum assertions |
| Shared service hides coupled modules | Architecture tests; split-service fallback |
| Management Governance scaffold treated as service authority | Scaffold is governance-only; separate approved packs, architecture tests and explicit user approval remain mandatory |
| Regression debt deferred | Slice cannot close while baseline regression remains |

## 14. Review questions

1. Is the actual authoritative Master 8.1 workbook available, and does its `Blueprint_Data` canonically
   allocate MOD-0354 and MOD-0355?
2. Does the Enterprise Architect approve the proposed successor relationship while retaining DCP-005
   unchanged?
3. Are DWS and BPM separation rules mechanically enforceable in one service?
4. Does the Portfolio Governance Process Owner accept the approved Phase 2A/2B minimum scope and its
   completion boundary?
5. Which shared contract versions are available versus still mock/partial?
6. Who owns quarantine disposition and retention for any ES containment slice?

## 15. Gate criteria

### Gate 1 — Baseline

The `PASS` recorded on 2026-07-28 came from the DCP-005 WorkCenter boundary review after its two written
conditions were closed. Only DCP-005's four unchanged WorkCenter hazard boundaries are carried forward into
DCP-006. That WorkCenter `PASS` is not, by itself, general Enterprise Architecture approval or production
authorization; DCP-006's separate Enterprise Architect approval is recorded in §1 and the change log.

### Gate 2 — Before the first protected production change

Claude WorkCenter Control Tower Gate 2 is mandatory immediately before:

1. ES `TaskAggregate` modification, migration, deletion or deprecation;
2. any DWS task/execution dependency or task-like node status/date/progress/assignment/lifecycle projection,
   UI, migration or behavior; pure structural dependency is explicitly not a Gate 2 hazard;
3. local approval behavior based on `ApprovedAt`/`ApprovedBy`, `ApproveStructureAsync`, an Approve button,
   UI action, route or command; or
4. WorkCenter projection from free-text Demand identities.

Read-only inventory may prepare evidence but may not mutate these hazards. WC-5 and ES
`IWorkItemProvider` remain behind the separate DCP-004 approval gate.

### Gate 3 — Merge assurance

Required immediately before each applicable ES feature branch and WorkCenter feature branch is merged into
`main`. It does not authorize or require merging those feature branches into each other. It must prove
ownership boundaries, candidate-literal absence, tenant isolation, authoritative shared-module decisions
and no deferred regression debt.

## 16. Acceptance criteria

1. DCP-006 identity is collision-free and explicitly attributed to the 2026-07-28 EA allocation.
2. Status is `approved` by the Enterprise Architect on 2026-07-28; approval alone does not authorize
   production implementation.
3. All CAP-001 mandatory sections are present.
4. MOD-0117 owns Demand, portfolio, initiative/program/project, benefit and value records.
5. MOD-0136/0138/0072 remain their respective SoRs; PPM uses typed/versioned contracts.
6. DWS Wave 1 persists only structural mechanics and immutable baselines.
7. BPM persists process models/versions and cannot start workflow runs or operational tasks.
8. MOD-0023 approval outcomes and MOD-0024 task records remain authoritative.
9. DWS/BPM have separate domain types, repositories, collections and permissions.
10. Candidate IDs are absent from production code, routes, permissions, collections, events and jobs.
11. Unknown-tenant records are quarantined; no default-tenant assignment exists.
12. Real persistence and authenticated cross-tenant tests prove 404/fail-closed behavior.
13. Concurrency, idempotency, audit, retry, partial-failure and rollback paths are proven.
14. Integrated golden flow and negative ownership tests pass.
15. No slice closes with regression debt deferred to a later slice.

### Negative acceptance criteria

- No DWS node command can complete/start/assign a generic task.
- No BPM command can create a workflow run, approval decision, SLA or escalation.
- No PPM record stores budget/scenario/KPI/evidence payload truth owned elsewhere.
- No WorkCenter projection decides business state.
- No `CAND-CAP-*` value appears in runtime artifacts.

## 17. Downstream business-module impacts

- DCP-003 is retained only for deferred legacy safe-parity planning; its draft status authorizes no
  implementation. DCP-006 exclusively governs active 1.3/MOD-0117 orchestration.
- MOD-0117 module packs must distinguish portfolio/project commands from DWS structural references.
- Demand implementation remains outside DCP-006 and DCP-003; it requires a new explicit Demand DCP/FU
  decision, parent-aware preflight, PPM domain owner approval and Enterprise Architect approval.
- DWS/BPM module packs must adopt the service-separation and runtime-name decisions in §10.
- DWS/BPM now belong to the `management-governance` domain. They require separate module packs and human
  approval; the domain scaffold does not authorize service or production implementation.
- Shared integration packs publish or consume typed contracts rather than shared domain models; DCP-006 does
  not reimplement them. For DWS Wave 1, runtime blockers are only MOD-0117 typed context validation,
  MOD-0018 authoritative permission enforcement/evaluation integration and a versioned MOD-0021 audit
  append/event contract. MOD-0023 and MOD-0024 remain prohibited-ownership boundaries, MOD-0031 and MOD-0288
  are not consumed, and MOD-0048 is `N/A` because NodeKind is excluded. MOD-0023 consumes MOD-0018 results
  for approver eligibility/delegation; DWS, BPM and WorkCenter recalculate neither.
- CAND-CAP-0007-FU01 remains conditional and must be reconciled to DCP-006 before any active-scope ES
  containment work.
- DCP-004 remains authoritative for WorkCenter projection and provider integration.

## 18. Open decisions

| ID | Decision | Blocked work | Owner |
|---|---|---|---|
| OD-01 | ✅ **CLOSED — 2026-07-28:** Master 8.1 is canonical and historical CAND-CAP-0008/0009 aliases resolve to MOD-0354/0355 | None | Enterprise Architect |
| OD-02 | ✅ **CLOSED — 2026-07-28:** Initial modular-monolith placement with mandatory architecture tests and fail-closed split fallback | Module packs must prove isolation before service scaffold | Enterprise Architect |
| OD-03 | ✅ **CLOSED — 2026-07-29:** Permanent owner role is Portfolio Governance Process Owner (PPM Business Owner); minimum Slice 2 Phase 2A/2B scope and future `Diten.PpmService` SoR placement are approved. This grants no scaffold or production authority | None; runtime gates remain under OD-04 | Portfolio Governance Process Owner |
| OD-04 | 🔴 **OPEN / PARTIAL:** MOD-0117 pack is approved and Phase 2A object implementation is authorized. PPM's two-gate entitlement/permission policy, audit event identity and Minimal Mutation Audit v1 consumer contract are governance-locked. PSS-B1 fixes the provider-side `platform.ppm-entitlement-decision.v1` contract and every-instance invalidation behavior, but the PPM-service consumer, authenticated audit publisher credential, `ppm.external-context-reference` provider transport/DWS runtime and final runtime evidence remain unauthorized or unevidenced | PSS-owned runtime integration, provider consumer/DWS runtime and Phase 2B consumers; does not block the already authorized Phase 2A object implementation | Shared-module owners |
| OD-05 | ES containment/migration necessity | CAND-CAP-0007-FU01 participation | ESBP + EA |
| OD-06 | DCP-005/DCP-006 formal successor relationship after approval | Portfolio governance | Enterprise Architect |
| OD-07 | ✅ **CLOSED — scope partition, 2026-07-28.** DCP-006 is sole active 1.3/MOD-0117 orchestration; DCP-003 is deferred/non-executable and portfolio-delivery domain config is reconciled | None; draft MOD-0117 pack authoring is eligible after OD-03 closure and canonical preflight, while OD-04 still blocks ready-for-dev/runtime | Enterprise Architect technical/governance owner |
| OD-08 | ✅ **CLOSED — 2026-07-28:** Separate `management-governance` domain scaffold | No production authority; separate module packs still required | Enterprise Architect |

## 19. Future follow-ups

- Preserve Master 8.1 provenance and rerun canonical ID/name/parent/collision preflights when packs change.
- Implement only the user-authorized MOD-0117 Phase 2A scaffold/backend/frontend on service port `5061`.
- Keep ExternalContextReference provider/transport, DWS runtime integration, Phase 2B, Gateway-file work
  beyond the authorized Phase 2A PPM mapping and all four WorkCenter hazards fail-closed until their named
  gates close.
- Author DWS/BPM as separate module packs; OD-08 is closed, but human approval and architecture-test gates
  remain mandatory.
- If a delivery touches any preserved WorkCenter hazard in §15, obtain Gate 2 before the first production
  change.
- Implement the approved DWS/BPM negative architecture-test design as delivery evidence before service
  completion; the absence of a test project is not a remaining design decision.
- Define MOD-0117 typed interfaces for budget, scenario, outcome and structural baselines.
- Reconcile any later WorkCenter provider work through DCP-004, not this DCP.
- Treat 1.1/1.2/1.5/1.7–1.10 as separate future DCP scope.

## 20. Audit and reconciliation notes

### Evidence basis

- CAP-001 and repo execution contract.
- Blueprint Master 8.1 `Blueprint_Data` for MOD-0117, MOD-0136, MOD-0138, MOD-0072, MOD-0354, MOD-0355 and shared modules; Master 7 is historical predecessor evidence.
- Management & Governance Domain Structure v2.1 audited ownership/interface/task taxonomy.
- 26 July 2026 Integrated R&D–RA Lifecycle v4 reconciliation, treated as evidence but not as the
  authoritative Master 8.1 workbook.
- DCP-002, DCP-003, DCP-004, DCP-005, domain configs and current repository code.

### Reconciliation verdicts

- Current ES `TaskAggregate`, Demand lifecycle and DWS task/approval fields are hazard evidence, not a
  production baseline.
- Existing `frontend/Diten.Web` Management Governance, Delivery Execution and related ESBP/DWS surfaces
  are pre-existing mock/prototype/legacy code-reality evidence, not completed capabilities, authoritative
  module status or implementation authority. Registry `Active` / `Monitor` labels are non-authoritative.
- `/management-governance` also exposes 1.1/1.2/1.5/1.7/1.8/1.9/1.10; those surfaces are not active
  DCP-006 delivery scope. Approve/assign/escalate controls and hard-coded permission results are
  `QUARANTINE` Gate 2 hazard evidence.
- DWS FS + due date/owner/overdue/status is not structural Wave 1 and remains `QUARANTINE`; pure
  hierarchy/order/structural dependency may be retained only as reference. BPM placeholders are not
  implementation proof.
- Global `_ViewStart` use of FROZEN `_Layout` is not a production foundation. Future tenant module packs
  use `_LayoutTenantShell`; `_Layout.cshtml` remains unchanged.
- No sufficient BPM runtime proof was found; status remains unproven/not assessed.
- The PPM mock/adapter surface is not real integration evidence.
- Master 8.1 proves MOD-0354/0355; CAND-CAP-0008/0009 remain deprecated governance aliases only.
- DCP-005 remains approved as the historical/foundation governance and Gate provenance source; DCP-006
  does not fully supersede it while OD-06 remains open.

### Change log

| Date | Change | Authority |
|---|---|---|
| 2026-07-28 | Enterprise Architect explicitly and singularly allocated `DCP-006`, slug `portfolio-delivery-process-core`, name `Portfolio, Delivery & Process Core`, exclusively to active subdomains 1.3, 1.4 and 1.6. Allocation was not inferred from sequence. | Enterprise Architect |
| 2026-07-28 | Created initial CAP-001 `draft`; recorded DCP-005 non-modification, candidate guards, Gate 1/2/3 boundaries and no-code guard. | DCP author/reviewer |
| 2026-07-28 | Correction audit added DCP-003/MOD-0117 scope reconciliation, conditional DWS/BPM domain placement, shared MOD-0018/0021/0048 ownership, precise Gate 1/2/3 provenance and Demand implementation exclusion. | DCP correction reviewer |
| 2026-07-28 | Enterprise Architect approved DCP-006 after Control Tower review; approval authorizes governed prerequisite closure and eligible module-pack authoring, not production implementation by itself. | Enterprise Architect |
| 2026-07-28 | OD-07 CLOSED by scope partition: DCP-006 became sole active 1.3/MOD-0117 orchestration; DCP-003 and portfolio-delivery governance documents were reconciled as deferred/non-executable. OD-03 and OD-04 remain OPEN and block Slice 2 module-pack authoring. | Enterprise Architect — interim governance owner only |
| 2026-07-28 | OD-02 CLOSED with initial modular-monolith placement plus mandatory split fallback; OD-08 CLOSED with the separate `management-governance` domain scaffold. No service/module-pack/production authority granted. | Enterprise Architect |
| 2026-07-28 | Governance-only code-reality reconciliation recorded pre-existing frontend/prototype surfaces and DWS task/approval behavior as non-authoritative reference or quarantined Gate 2 hazard evidence. DCP-005 remains the approved historical/foundation and Gate provenance source; DCP-006 remains the sole active 1.3/1.4/1.6 orchestration contract while OD-06 stays OPEN. | Governance reconciliation |
| 2026-07-28 | Foundation reconciliation narrowed the DWS Wave 1 OD-04 subset to its three actual runtime consumers: MOD-0117 typed context validation, MOD-0018 authoritative permission integration and versioned MOD-0021 audit append/event. Boundary-only and unused shared modules no longer block DWS Wave 1. | Governance reconciliation |
| 2026-07-28 | OD-01 CLOSED: Master 8.1 became the canonical Blueprint source and DWS/BPM identities were canonicalized from historical CAND-CAP-0008/0009 aliases to MOD-0354/MOD-0355. Scope and ownership remain unchanged. | Enterprise Architect |
| 2026-07-28 | Recorded transport-independent `ppm.external-context-reference` v1 as a PROPOSED GOVERNANCE BASELINE for the MOD-0117 → MOD-0354 boundary. OD-03/OD-04 remain OPEN; no endpoint, transport or production authority was created. | Enterprise Strategy Control Tower governance reconciliation |
| 2026-07-28 | PSS owner/Enterprise Architect recorded identity decision A (`MOD-0018 — RBAC / ABAC Authorization` remains the parent; production wiring remains its phase/slice), approved signed-JWT plus reusable in-process enforcement, and approved the MOD-0018 `403` / MOD-0117 invisibility `404` ownership boundary. The MOD-0018 subset remains `PARTIAL`; OD-04 remains OPEN. No production authority or new FU allocation was created. | PSS owner / Enterprise Architect |
| 2026-07-28 | PSS owner/Enterprise Architect recorded AD-08: `MOD-0021 — Audit Trail Service` remains the parent; DWS uses a producer-local transactional audit intent and versioned asynchronous at-least-once/idempotent-consumer integration. Direct Platform collection access and the existing shared-key internal endpoint are not authoritative baselines. The MOD-0021 subset remains `PARTIAL`, OD-04 remains OPEN, and no FU or production authority was allocated. | PSS owner / Enterprise Architect |
| 2026-07-29 | Enterprise Strategy Control Tower closed OD-03: assigned the permanent institutional Portfolio Governance Process Owner (PPM Business Owner), approved Slice 2 Phase 2A/2B minimum scope and confirmed future `services/Diten.PpmService/` as the sole production SoR placement. AD-06 became an approved governance baseline, not a runtime contract. Draft MOD-0117 pack authoring is eligible; OD-04 remains OPEN/PARTIAL and no scaffold or production authority was granted. | Enterprise Strategy Control Tower |
| 2026-07-29 | User explicitly approved `Diten.PpmService` scaffolding and MOD-0117 Phase 2A backend/frontend. Recorded service port `5061`, Gateway `5000` browser boundary, exact lifecycle/cardinality/referenceability and tenant Golden Slim surfaces. Provider/transport, DWS runtime, Phase 2B and WorkCenter hazards remain blocked; OD-04 remains OPEN/PARTIAL. | User / Enterprise Strategy Control Tower |
| 2026-07-29 | Authorized only the Phase 2A PPM object API Gateway mapping, restricted to `integration-agent`; provider/DWS/Phase 2B and other routes remain blocked. | Enterprise Strategy Control Tower |
| 2026-07-30 | Governance-only reconciliation locked independent PPM tenant-entitlement and user-permission gates, dormant-grant behavior, and the `PpmAuditIntentSubmittedV1` / `ppm.audit-intent.submitted.v1` event identity. PSS-owned runtime work still requires an executable PSS pack, explicit user approval and separate PSS branch/worktree; OD-04 remains OPEN/PARTIAL. | User / Enterprise Strategy Control Tower |
| 2026-07-30 | Code-reality correction fixed `ModuleCode = PPM` and recorded that the current generic Admin/Viewer auto-grant plus destructive revoke/reconcile bridge cannot serve PPM unchanged. PPM-specific versus generic bridge revision and dormant-grant reactivation policy remain explicit PSS/security human-review blockers; no runtime authority was granted. | User / Enterprise Strategy Control Tower |
| 2026-07-30 | PSS-B1 recorded the authoritative PPM entitlement decision provider (`platform.ppm-entitlement-decision.v1`) and corrected local cache invalidation so shared persistent consumer dedupe cannot suppress eviction on another Platform instance. OD-04 remains OPEN/PARTIAL because the PPM consumer and other named runtime evidence are outside this slice. | User / Enterprise Strategy Control Tower |
| 2026-07-30 | PSS-B1 activation was made fail-closed and deployment-safe: provider activation defaults off; disabled returns infrastructure `503` before lookup, while enabled startup requires a dedicated validated PPM credential. Disabled is never interpreted as business entitlement denial. | User / Enterprise Strategy Control Tower |
