---
id: MOD-0192
name: Capacity Planning
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Phase A specification draft only. Runtime code is not authorized; promote only after the shipment lane is verified and every ready-for-dev item passes."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 0
---

# MOD-0192 — Capacity Planning

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0192 --name "Capacity Planning"`
> returned `OK` on 2026-09-15. Blueprint 8.1 and the module ID registry prove the canonical identity.
>
> **Execution gate:** this is a draft specification for a backend/contract initial slice. It grants no runtime-code,
> service-scaffold, gateway, permission-registry or shared-registration authority. The shipment lane must be frozen and
> independently verified before MOD-0192 and MOD-0190 may enter their parallel implementation wave.
>
> **ASSUMPTION:** UI journeys and form fields are not sufficiently defined, so the initial slice uses `shell: none` and
> `golden_reference: none`. Any UI requires an approved revision or follow-up pack.

## 1. Module Summary

MOD-0192 owns capacity plans, alternative capacity scenarios and their evaluations/bottleneck results. A plan binds
to immutable provenance for one frozen DEMAND plan version; scenarios express capacity constraints and adjustments,
then evaluation reports gaps without copying or changing DEMAND facts.

The planned initial slice is backend/contract only. It may be dispatched after the MVP-6 shipment lane is verified,
in parallel with MOD-0190, subject to this pack reaching `ready-for-dev`.

## 2. Ownership and Boundaries

**Owns:** `CapacityPlan`, `CapacityScenario`, `CapacityEvaluation`, bottleneck result projections, idempotency records,
audit evidence and MOD-0192 lifecycle event payloads.

**Consumes:** frozen `DEMAND` v1 by plan ID/version, supply-constraint references, Event Bus and actor identity.

**Must not:** copy demand forecast series into another SoR; edit MOD-0188; own production scheduling, inventory,
shipment, S&OP sign-offs, supplier or warehouse truth; accept tenant/legal-entity scope in request bodies.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `CapacityPlan` | Horizon and immutable frozen-DEMAND provenance |
| `CapacityScenario` | Named constraint-reference and capacity-adjustment alternative |
| `CapacityEvaluation` | Asynchronous finite/infinite evaluation and bottleneck result |
| Commands | Create capacity plan, create scenario, submit evaluation |
| Queries | Get capacity plan, get scenario, get evaluation |
| API | `/api/supply-chain/capacity-plans/**` from `SANDOP-CAPACITY` v1 |
| Events | Capacity plan/scenario created and evaluation completed lifecycle events |
| Permissions | `supplychain.capacity-plans.read`, `.create`, `.scenario.create`, `.evaluate` |

## 4. Entity Fields

### CapacityPlan

| Field | Rule |
|---|---|
| `Id` | Server-minted UUID |
| `TenantId` / `LegalEntityId` | Required, server-resolved, never request body |
| `Name` | Trimmed, 1..200 |
| `HorizonStart` / `HorizonEnd` | Date; end is not before start |
| Provenance | DEMAND ID/version, source contract/version, capture time and checksum |
| `Status` | `Draft`, `Evaluating`, `Ready`, `Approved`, `Archived` |
| `Version` | Infrastructure optimistic-concurrency token |
| Soft-delete/audit | Repo-standard `IsDeleted`, `DeletedAt` and audit fields |

### CapacityScenario and CapacityEvaluation

| Field | Rule |
|---|---|
| Scenario name | Trimmed, 1..200 |
| Constraint references | Opaque source, resource ID and version; no foreign payload ownership |
| Adjustments | Resource, period, decimal-string delta and UoM; scenario-owned assumption |
| Evaluation mode | `Finite` or `Infinite` |
| Evaluation status | `Accepted`, `Running`, `Completed`, `Failed` |
| Bottleneck | Resource/period, required and available capacity, shortfall and UoM |
| CorrelationId | Required UUID propagated from command through completion event |

Tenant-first indexes: unique active plan horizon+demand version; unique scenario name per plan; evaluation lookup by
`(TenantId, LegalEntityId, CapacityPlanId, ScenarioId, SubmittedAt)` and one active evaluation per scenario.

## 5. Repo Scope

After a later `ready-for-dev` promotion, the bounded initial slice may write only:

- `services/Diten.SupplyChainService/src/**/Features/CapacityPlans/**`.
- `services/Diten.SupplyChainService/tests/**/CapacityPlans/**`.
- Module-local generated API reference and immutable verification evidence under canonical docs paths.
- This module pack's implementation notes/status when reality changes.

`docs/analysis/contracts/sandop-capacity.openapi.yaml` is frozen implementation authority and is read-only during
runtime work. Contract changes require a separate versioned, single-writer specification task.

## 6. Protected Paths

- `.antigravity/**`.
- `docs/analysis/contracts/demand.openapi.yaml` and every other module-owned frozen contract.
- `docs/analysis/contracts/sandop-capacity.openapi.yaml` during runtime implementation.
- `gateway/Diten.ApiGateway/**/ocelot.json`; integration-agent only.
- Frontend Archive paths and `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- Domain-external services and every non-MOD-0192 feature path.
- MOD-0183..0190, MOD-0191 and MOD-0147/0148 runtime paths.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| `SANDOP-CAPACITY` v1 | FROZEN | Owned contract; implement exactly, do not edit during runtime work |
| `DEMAND` v1 | FROZEN | Consume by mock/API; persist only ID/version and immutable provenance |
| Shipment lane MOD-0183..0187 | SEQUENCE GATE | Must be frozen and independently verified before dispatch |
| Supply constraints | Building Block / frozen adapter boundary required | Keep opaque versioned references; no foreign SoR |
| Event Bus | Building Block | Publish lifecycle events with UUID correlation |
| MOD-0190 | Parallel peer | No shared persistence or internal types; integrate by contracts only |

## 8. Runtime Constraints

- Planned service is `Diten.SupplyChainService` on port 5061, using five layers and CQRS/MediatR.
- MongoDB data is tenant/legal-entity scoped; cross-scope access returns 404 without existence leakage.
- Mutations require `Idempotency-Key` and UUID `X-Correlation-Id` per frozen contract.
- DEMAND input is immutable provenance; evaluations cannot mutate or duplicate its source facts.
- Scenario/evaluation state, audit and outbox event must be atomic or expose an explicit failure.
- DEMAND and constraint sources are consumed through frozen clients/mocks only; no database/internal-type sharing.
- Decimal values remain invariant-culture strings at contract boundaries.
- This draft authorizes no runtime code.

## 9. Layout & Shell Contract

`shell: none`. The initial slice has no Razor UI, menu, DataTable, localization surface or frontend route.

## 10. Backend File Convention

The eventual slice uses `Features/CapacityPlans/Commands`, `Queries`, `Handlers/CommandHandlers`,
`Handlers/QueryHandlers`, `Validators` and `CapacityPlanModels.cs`. Each public command/query/handler/validator has its
own file. Handler and validator names do not carry Command/Query suffixes. Runtime work must use the service's four
pipeline behaviors, base controller and explicit tenant/legal-entity repository predicates.

## 11. Frontend File Contract

Not applicable. A UI requires a revised/follow-up pack defining tenant shell, exact form-field count, localization,
authorization surface and Slim/Compact golden reference.

## 12. Validation Rules

| Field/Header | Required | Rule | Pre-check |
|---|---|---|---|
| `Idempotency-Key` | Mutations | Trimmed, 1..200 | Tenant+operation replay key |
| `X-Correlation-Id` | All contract operations | UUID | Persist/echo through audit and events |
| `Name` | Plan/scenario create | Trimmed, 1..200 | Unique in declared scope |
| Horizon | Plan create | Valid dates; end >= start | — |
| Demand provenance | Plan create | Non-empty ID/version/checksum; UTC capture | Frozen DEMAND mock returns Published |
| Constraint refs | Scenario | Source, ID and version required | Frozen adapter when source is available |
| Adjustments | Scenario | Valid period, UoM, decimal-string delta | Resource belongs to resolved scope |
| Evaluation mode/resources | Evaluate | Frozen enum; at least one resource | Scenario belongs to plan and is evaluable |

## 13. Failure Path to Verify

- Unknown or unpublished DEMAND version returns 422 `INVALID_DEMAND_REFERENCE` and creates no plan.
- Cross-tenant or cross-legal-entity plan/scenario/evaluation returns 404 without leakage.
- Duplicate plan horizon+demand version returns 409 and no duplicate aggregate.
- Scenario creation in a disallowed plan state returns 409 `CAPACITY_PLAN_STATE_CONFLICT`.
- Unknown scenario returns 404 `UNKNOWN_CAPACITY_SCENARIO`.
- Concurrent evaluation returns 409 `EVALUATION_ALREADY_ACTIVE` and starts no second job.
- Duplicate idempotency key replays the original result and emits no second event/job.
- Missing/invalid correlation UUID or decimal format returns 400 and performs no write.

## 14. Authorization Convention

Tenant actor/service actor with default-deny JWT and permission checks:

- `supplychain.capacity-plans.read`
- `supplychain.capacity-plans.create`
- `supplychain.capacity-plans.scenario.create`
- `supplychain.capacity-plans.evaluate`

Shared permission definition/seed remains the platform owner's seam. A future runtime WP may consume approved keys
but cannot edit the shared permission registry without a separately authorized owner/integration task.

## 15. Gateway / API Routing Decision

Desired public family is `/api/supply-chain/capacity-plans/**` routed to port 5061. This pack does not authorize an
`ocelot.json` change. An integration-agent WP must add/verify routes and propagation of authorization, tenant,
legal-entity, correlation and idempotency headers after endpoints exist.

## 16. Acceptance Criteria

- [ ] Capacity plan stores one tenant/legal-entity aggregate with immutable Published-DEMAND provenance.
- [ ] No demand quantity, confidence or forecast series is persisted as a competing source of truth.
- [ ] Scenario creation stores only owned adjustments and opaque versioned constraint references.
- [ ] Evaluation runs idempotently and exposes accepted/running/completed/failed state without duplicate jobs.
- [ ] Completed evaluation returns decimal-string bottleneck measures and emits the frozen event once.
- [ ] UUID correlation is preserved from HTTP request through response, audit and lifecycle event.
- [ ] Cross-scope access returns 404; missing permission returns 403.
- [ ] Success and error payloads match `SANDOP-CAPACITY` v1 examples and `contractVersion: v1`.
- [ ] MOD-0192 shares no persistence/internal DTOs with MOD-0190 and runs against frozen mocks.
- [ ] G5 evidence reconciles capacity decisions to source provenance without overriding source SoRs.

## 17. Test Expectations

- DCP-002 identity verifier and OpenAPI syntax/reference validation PASS.
- Domain tests cover plan/scenario lifecycle, evaluation state and bottleneck arithmetic/precision.
- Handler/API tests cover validation, RBAC, tenant/legal-entity isolation, idempotency and concurrency.
- Contract tests cover every declared success/error shape and lifecycle event example.
- Mongo integration tests use isolated DB-010 naming and tenant-first indexes.
- Prism-compatible mock smoke uses frozen `DEMAND` and `SANDOP-CAPACITY` contracts.
- Relevant service and repo architecture gates pass; G5 remains open until E5 integration evidence exists.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002 on 2026-09-15.
- [x] DCP-009, domain boundary, MVP-6 brief and frozen contracts were read.
- [x] Backend/contract initial slice, shell and golden-reference decisions are explicit.
- [x] Owned objects, paths, validation, failures and acceptance are specified.
- [x] `SANDOP-CAPACITY` v1 is frozen and DEMAND v1 consumption is reference-only.
- [ ] Shipment lane freeze and independent verification evidence is recorded.
- [ ] Supply-constraint and Event Bus consumed adapter boundaries are confirmed for implementation.
- [ ] Runtime allowed/protected paths are checked against current repository state.
- [ ] User/owner approves this pack for `ready-for-dev`.

Status remains `draft`; unchecked items prohibit dispatch.

## 19. Implementation Notes

- **ASSUMPTION:** runtime feature root will be `CapacityPlans`, derived from the frozen route and owned aggregate.
- **ASSUMPTION:** capacity adjustment is an owned scenario assumption; referenced base constraint remains in its
  source SoR and is retained only as source/resource/version provenance.
- **ASSUMPTION:** `demandPlanVersion` is an opaque string because DEMAND v1 does not publish a stronger version type.
- MOD-0192 and MOD-0190 are parallel peers only after the shipment lane gate; neither may write the other's data.

## 20. Follow-up Items

- Record shipment-lane independent verification before pack promotion.
- Freeze/confirm the supply-constraints adapter contract before a real source integration is enabled.
- Create an integration-agent WP for gateway and shared permission/registration changes.
- Define a separate tenant UI follow-up if product journeys require one.
- Complete G5 E5 integration across capacity, S&OP, logistics and source reconciliation.
