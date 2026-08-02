# Enterprise Strategy & Business Performance — Domain Config

> Domain-specific packaging and ownership contract. Engineering rules are inherited from
> [.antigravity/rules/](../../../.antigravity/rules/). This scaffold does not create a module pack, physical
> service topology, port, route, migration or production authority.

## Domain identity

- Name: `Enterprise Strategy & Business Performance`
- Slug: `enterprise-strategy-business-performance`
- Branch short code: `esbp`
- Governance-packaged modules: `MOD-0352`, `MOD-0136`, `MOD-0138`, `MOD-0072`
- Physical service topology: `TBD` independently in each approved parent module pack

Repository-domain packaging does not replace each module's Blueprint logical route. MOD-0136, MOD-0138 and
MOD-0072 are not automatically placed in `Diten.EnterpriseStrategyService`.

## Canonical ownership and logical routing

### MOD-0352 — Enterprise Strategy Management

Owns strategy, objectives/cascade and performance context. It does not duplicate Demand/PPM lifecycle,
MOD-0354 DWS, MOD-0355 BPM, MOD-0024 task or MOD-0023 approval ownership.

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

## Gate I routing and executability

- Gate I-A: mandatory MOD-0007, with MOD-0023 only when authoritative policy requires ApprovalOutcome.
- Gate I-B: MOD-0136 selected BudgetVersion and MOD-0138 scenario/comparator/selection references.
- Gate I-C: MOD-0072 outcome/realization references; planned BenefitCommitment remains MOD-0117-owned.

Gate I producer authoring may run in parallel. No MOD-0117 consumer adapter is executable until its producer
contract is approved, the matching MOD-0117 consumer amendment is approved and explicit runtime authority is
recorded. DCP-006 OD-04 remains `OPEN / PARTIAL`.

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

Code proximity does not establish a production SoR. Physical topology and any containment/migration decision
must be made in the owning parent pack with architecture tests, collection-level verification, quarantine and
rollback evidence. No prototype is moved, deleted or modified by this scaffold.

## WorkCenter and approval boundary

TaskAggregate, WorkCenter projection, DWS execution lifecycle/assignment, ApprovalTask,
approve/reject/delegate/request-info/cancel route/command/UI and local `ApprovedAt`/`ApprovedBy` behavior remain
`QUARANTINE`. This governance-only task does not trigger Gate 2; Gate 2 is mandatory immediately before the
first production DTO/API/UI/projection/migration/deletion/deprecation change. Pure hierarchy/order/structural
dependency is not itself a Gate 2 hazard.

## Repo scope and protected paths

This milestone may change only `execution/domains/enterprise-strategy-business-performance/**` plus explicitly
authorized root governance indexes. Future implementation scope is determined only by the relevant approved/
ready-for-dev pack.

Protected: `.antigravity/**`, `services/**`, `frontend/**`, `gateway/**`, archive/frozen paths and other domain
governance outside an explicit reconciliation task.

## Delivery gates

- DCP-006 remains `approved`; MOD-0117 remains `review`; OD-04 remains `OPEN / PARTIAL`.
- MOD-0136, MOD-0138 and MOD-0072 parent packs each begin as separate `draft` authoring jobs.
- Each pack preserves its logical Blueprint lane and independently decides physical service topology through
  architecture tests.
- No implementation begins before pack approval/ready-for-dev and explicit user runtime authority.
