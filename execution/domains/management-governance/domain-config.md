# Management & Governance — Domain Config

> Domain-specific ownership and packaging contract. Engineering implementation rules remain in
> [.antigravity/rules/](../../../.antigravity/rules/). This scaffold is governance-only and creates no module
> pack, service, port, route, migration or production authority.

## Domain identity

- Name: `Management & Governance`
- Slug: `management-governance`
- Branch short code: `mg`
- Governance-packaged modules: `MOD-0007`, `MOD-0354`, `MOD-0355`
- MOD-0007 physical service topology: `TBD` at its approved parent-pack architecture decision
- MOD-0354/MOD-0355 placement: DCP-006 OD-02 initial conditional `Diten.ManagementGovernanceService`
  modular monolith, mandatory isolation architecture tests and fail-closed split fallback

Repository packaging is not Blueprint logical routing or physical service placement. In particular,
MOD-0007 is not automatically assigned to `Diten.ManagementGovernanceService`. This scaffold does not create
the OD-02 service, port or route and does not grant MOD-0354/MOD-0355 runtime authority.

## Canonical ownership

### MOD-0007 — Decision & Rationale Log

- Blueprint route: `Enterprise Control Points → Enterprise Governance Registry Layer → Authoritative Registries`
- Institutional owner: **Enterprise Governance Process Owner**
- Owns generic authoritative Decision records, immutable decision revision/version, rationale/provenance,
  governing/supporting roles, typed evidence references and authoritative DecisionReference validation.
- Does not own MOD-0023 approval tasks/workflow/SLA/escalation, MOD-0117 Portfolio/InvestmentCase/
  BenefitCommitment, MOD-0136 budget/version, MOD-0138 scenario/comparator, MOD-0072 actual realization,
  MOD-0024 TaskAggregate, DCP-004 WorkCenter or generic document binary/payload.

### MOD-0354 — Decomposition & Work Structuring Engine

Owns structure definition/template/instance/node, hierarchy, order, pure structural dependency and immutable
baseline/version/compare mechanics. It does not own task/execution dependency, assignment, dates, progress,
execution lifecycle, task actions or approval.

### MOD-0355 — Business Process Architecture & Modeling

Owns process architecture/domain/family, model/version, activity, control point and typed role/KPI binding. It
does not own workflow runs, ApprovalTask, approval decisions, eligibility/delegation, SLA, escalation or
operational tasks.

## Gate I-A contract boundary

MOD-0007 is the mandatory Decision Trace producer. MOD-0023 is conditional and participates only when an
authoritative policy requires approval. Generic Decision and ApprovalOutcome are distinct concepts. No
MOD-0117 consumer adapter is executable until the producer contract is approved, the matching MOD-0117
consumer amendment is approved and explicit runtime authority is recorded.

MOD-0023 requires a separate PSS-owned amendment. Its current code reality is not a PPM integration contract:
client ActorId, free-text ObjectType/ObjectId/ObjectRef, non-authoritative candidate principals, missing typed/
versioned PPM outcome, unproven atomic transition persistence and missing immutable authoritative outcome
reference remain remediation hazards. The amendment must require server-derived tenant/actor, authenticated
S2S plus separately validated delegated actor, typed/versioned source reference, immutable template-version
pin, idempotent submit, MOD-0018-backed eligibility, immutable terminal outcome, server decision actor/time,
atomic persistence and exact `401/403/404/409/503` compatibility fixtures.

## Prototype and migration classification

| Existing evidence | Classification | Governance disposition |
|---|---|---|
| Enterprise Strategy `DecisionReference` fields and mock values | Prototype/legacy evidence; MOD-0007 ownership hazard; migration candidate | Not authoritative; inventory and reconcile only through a future approved containment/migration plan |
| DWS hierarchy/order/pure structural dependency | Reference candidate | May inform MOD-0354 authoring; never copied as a runtime baseline |
| DWS status/dates/progress/responsible/execution dependency | Ownership hazard; quarantine | Not structural Wave 1 and not migratable without Gate 2 |
| `TaskAggregate`, `ApprovedAt`/`ApprovedBy`, `ApproveStructureAsync` | Ownership hazard; quarantine | MOD-0024/MOD-0023 boundaries; no activation, removal, migration or deprecation without Gate 2 |
| Approve/reject/delegate/assign/escalate UI, routes, commands or fixtures | Synthetic prototype/hazard | Not a permission, workflow or provider contract |
| BPM placeholders | Prototype/legacy evidence | Not implementation or completion evidence |

## WorkCenter Control Tower Gate 2

This governance scaffold does not trigger Gate 2. Gate 2 is mandatory immediately before any runtime, DTO/API,
UI, projection, migration, deletion or deprecation change involving:

1. ES `TaskAggregate`;
2. DWS task/execution dependency or task-like status/date/progress/assignment/lifecycle;
3. ApprovalTask, local `ApprovedAt`/`ApprovedBy`, or approve/reject/delegate/request-info/cancel action;
4. free-text Demand identity to WorkCenter projection.

Pure hierarchy, ordering and structural dependency alone are not Gate 2 hazards. WorkCenter remains only the
DCP-004 aggregation/projection boundary and decides no business lifecycle.

## Repo scope and protected paths

This milestone may change only `execution/domains/management-governance/**` plus explicitly authorized root
governance indexes. Future implementation scope must be declared by an approved/ready-for-dev module pack.

Protected: `.antigravity/**`, `services/**`, `frontend/**`, `gateway/**`, archive/frozen paths and other domain
governance outside an explicit reconciliation task.

## Delivery gates

- DCP-006 remains `approved`; OD-04 remains `OPEN / PARTIAL`.
- Every canonical parent pack begins as `draft` and carries its own physical-topology architecture decision.
- MOD-0354 and MOD-0355 remain separate packs and retain their structural-only/process-model-only boundaries.
- No implementation begins before pack approval/ready-for-dev and explicit user runtime authority.
