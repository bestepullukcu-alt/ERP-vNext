---
id: MOD-0192
name: Capacity Planning
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Published SANDOP-CAPACITY 2.0.0 / wire v1 design alignment proposed; Phase 1.5 mapped, pack promotion and runtime DEV remain HELD pending exact owner authorization."
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
> service-scaffold, gateway, permission-registry or shared-registration authority. The bounded logistics sequence-evidence gate is recorded; this pack still needs exact owner promotion and bounded runtime authority.
>
> **ASSUMPTION:** UI journeys and form fields are not sufficiently defined, so the initial slice uses `shell: none` and
> `golden_reference: none`. Any UI requires an approved revision or follow-up pack.

## 1. Module Summary

MOD-0192 owns capacity plans, alternative capacity scenarios and their evaluations/bottleneck results. A plan records requested immutable provenance for one frozen DEMAND plan version; scenarios express opaque
constraint references and owned adjustments. Live DEMAND version/checksum verification remains outside this fixture-only slice; evaluation
calculation/execution use the design-approved literal fixture and fenced executor, pending runtime authority; no DEMAND facts are copied or changed.

The planned initial slice is backend/contract only. Its sequence-evidence gate is recorded; isolated DEV still requires this pack to be promoted and bounded runtime authority. MOD-0190 is a disjoint parallel peer.

## 2. Ownership and Boundaries

**Owns:** `CapacityPlan`, `CapacityScenario`, `CapacityEvaluation`, bottleneck result projections, idempotency records,
audit evidence and MOD-0192 lifecycle event payloads.

**Consumes in this slice:** exact tenant/LE-scoped test-only DEMAND ID/version/checksum and constraint fixtures, plus a trusted JWT actor. There is no live DEMAND/constraint HTTP, Workflow or Event Bus publisher.

**Must not:** copy demand forecast series into another SoR; edit MOD-0188; own production scheduling, inventory,
shipment, S&OP sign-offs, supplier or warehouse truth; accept tenant/legal-entity scope in request bodies.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `CapacityPlan` | Horizon and immutable frozen-DEMAND provenance |
| `CapacityScenario` | Named constraint-reference and capacity-adjustment alternative |
| `CapacityEvaluation` | Durable Accepted/Running/Completed/Failed state and bottleneck result, subject to an approved execution/calculation policy |
| Commands | Create capacity plan, create scenario, submit evaluation |
| Queries | Get capacity plan, get scenario, get evaluation |
| API | Six `/capacity-plans/**` operations from published `SANDOP-CAPACITY` `info.version: 2.0.0` / wire `contractVersion: v1` |
| Events | Capacity plan/scenario created and evaluation completed lifecycle events |
| Permissions | `supplychain.capacity-plans.read`, `.create`, `.scenario.create`, `.evaluate` |

## 4. Entity Fields

### CapacityPlan

| Field | Rule |
|---|---|
| `Id` | Server-minted UUID |
| `TenantId` / `LegalEntityId` | Required, server-resolved, never request body |
| `Name` | Exact parsed string; schema `minLength: 1`, no added trim/max-200 rule |
| `HorizonStart` / `HorizonEnd` | Date; end is not before start |
| Provenance | DEMAND ID/version, source contract/version, capture time and checksum |
| `Status` | `Draft` only in this bounded six-operation slice; no approval/archive transition endpoint |
| `Version` | Infrastructure optimistic-concurrency token |
| Soft-delete/audit | Repo-standard `IsDeleted`, `DeletedAt` and audit fields |

### CapacityScenario and CapacityEvaluation

| Field | Rule |
|---|---|
| Scenario name | Exact parsed string; schema `minLength: 1`, no added trim/max-200 rule |
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
| published `SANDOP-CAPACITY` 2.0.0 / wire v1 | FROZEN | Owned contract; implement exactly, do not edit during runtime work |
| `DEMAND` v1 | FROZEN | Test-only exact scoped fixture; no live ID/version/checksum endpoint or HTTP call in this slice; persist requested immutable provenance |
| Shipment lane MOD-0183..0187 | SEQUENCE EVIDENCE RECORDED | Bounded sequence disposition is in `mvp6-mod0186-wp-acceptance-01/SOP-22.md`; no E5/G5 inference |
| Supply constraints | TEST FIXTURE ONLY | Exact tenant/LE/source/id/version/resource/period/UoM tuple; opaque references; no live producer |
| Event Bus | PENDING OUTBOX ONLY | Atomically persist event as Pending; no worker/publisher or delivery assertion |
| MOD-0190 | Parallel peer | No shared persistence or internal types; integrate by contracts only |

## 8. Runtime Constraints

- Planned service is `Diten.SupplyChainService` on port 5061, using five layers and CQRS/MediatR.
- MongoDB data is tenant/legal-entity scoped; cross-scope access returns 404 without existence leakage.
- Mutations require `Idempotency-Key` and UUID `X-Correlation-Id` per frozen contract.
- DEMAND input is immutable provenance; evaluations cannot mutate or duplicate its source facts.
- Scenario/evaluation state, audit and outbox event must be atomic or expose an explicit failure.
- DEMAND and constraint sources use scoped, versioned test fixtures only; no live HTTP, database/internal-type sharing or producer verification.
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
| `Idempotency-Key` | Three POSTs | Exact parsed nonempty string; no trim, normalization or max-200 | Scoped operation+target+key receipt; canonical decoded JSON fingerprint |
| `X-Correlation-Id` | Six operations | Exactly one valid UUID; missing/malformed/duplicate → 400 `INVALID_CORRELATION_ID` | Current valid request UUID in response; persisted original in audit/event |
| `Name` | Plan/scenario create | `minLength: 1`; exact parsed value, no trim/max-200 | Unique in declared scope with simple collation |
| Horizon | Plan create | Valid dates; end >= start | — |
| Demand provenance | Plan create | Non-empty ID/version/checksum; UTC capture | Declared fixture can check exact tuple; DEMAND v1 cannot verify live ID+version+checksum |
| Constraint refs | Scenario | Published request schema and exact fixture tuple | Unknown/stale/cross-scope/mismatch → 422 `INVALID_CONSTRAINT_REFERENCE` |
| Adjustments | Scenario | Valid period, UoM, decimal-string delta | Resource belongs to resolved scope |
| Evaluation mode/resources | Evaluate | Frozen enum; at least one resource | Scenario belongs to plan and is evaluable |

## 13. Failure Path to Verify

- Unknown or unpublished DEMAND version returns 422 `INVALID_DEMAND_REFERENCE` and creates no plan **only when an approved exact-version authority/fixture resolves it**; DEMAND v1 live GET alone cannot prove this.
- Cross-tenant or cross-legal-entity plan/scenario/evaluation returns 404 without leakage.
- Duplicate plan horizon+demand version returns 409 and no duplicate aggregate.
- Scenario creation in a disallowed plan state returns 409 `CAPACITY_PLAN_STATE_CONFLICT`.
- Unknown scenario returns 404 `UNKNOWN_CAPACITY_SCENARIO`.
- Concurrent evaluation returns 409 `EVALUATION_ALREADY_ACTIVE` and starts no second job.
- Same scoped key and valid identical decoded payload replays original 201/202 business status/body with current correlation header; changed valid payload returns 409 `IDEMPOTENCY_KEY_REUSED` before current state/fixture checks, with no second effect.
- Missing/malformed/duplicate correlation UUID or invalid body/decimal format returns the published 400 code and performs no write; application rejection without a valid unique UUID generates one rejection-only response UUID.

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

- [ ] Capacity plan stores one tenant/legal-entity aggregate with immutable DEMAND reference provenance; Published/exact-version authority is identified and mock-only proof is labeled.
- [ ] No demand quantity, confidence or forecast series is persisted as a competing source of truth.
- [ ] Scenario creation stores only owned adjustments and opaque versioned constraint references.
- [ ] Evaluation acceptance persists once and exposes Accepted/Running/Completed/Failed; only `CAPACITY-EVAL-FIXTURE-192-01@1` literal Finite/Infinite results and the approved fenced executor design are in scope. E4 restart/race proof remains required.
- [ ] Terminal evaluation returns fixture-literal decimal-string bottleneck measures, releases active slot, appends one audit and one `capacity.evaluation.completed.v1` Pending outbox in one transaction; no optimizer/publisher.
- [ ] UUID correlation is preserved from HTTP request through response, audit and lifecycle event.
- [ ] Cross-scope access returns 404; missing permission returns 403.
- [ ] Six operations match published `SANDOP-CAPACITY` 2.0.0 YAML and annex, including required/null/error/header/replay matrix; wire `contractVersion: v1`.
- [ ] MOD-0192 shares no persistence/internal DTOs with MOD-0190 and runs against frozen mocks.
- [ ] G5 evidence reconciles capacity decisions to source provenance without overriding source SoRs.

## 17. Test Expectations

- DCP-002 identity verifier and OpenAPI syntax/reference validation PASS.
- Domain tests cover Draft plan/scenario, evaluation state and literal fixture Finite/Infinite output/decimal lexemes; no general optimizer arithmetic claim.
- Handler/API tests cover validation, RBAC, tenant/legal-entity isolation, idempotency and concurrency.
- Contract tests cover every declared success/error shape and lifecycle event example.
- Mongo integration tests use isolated DB-010 naming and tenant-first indexes.
- Exact scoped DEMAND/constraint fixtures and published SANDOP contract tests are distinct from live producer validation.
- Relevant service and repo architecture gates pass; G5 remains open until E5 integration evidence exists.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002 on 2026-09-15.
- [x] DCP-009, domain boundary, MVP-6 brief and frozen contracts were read.
- [x] Backend/contract initial slice, shell and golden-reference decisions are explicit.
- [x] Owned objects, paths, validation, failures and acceptance are specified.
- [x] Published `SANDOP-CAPACITY` 2.0.0 / wire v1 YAML and annex exact hashes are pinned; DEMAND and constraints are test-only fixtures.
- [x] Bounded logistics sequence evidence recorded in `mvp6-mod0186-wp-acceptance-01/SOP-22.md` (not E5/G5).
- [x] Owner bounded this slice to exact DEMAND/constraint fixtures; live producer proof stays out of scope.
- [x] Exact constraint fixture and Pending-only outbox/no publisher boundaries are recorded.
- [x] Executor and literal fixture oracle design approved for candidate preparation; runtime execution remains separately held.
- [x] Bounded sequence disposition recorded; shared Program.cs/permission/gateway remain one separately authorized integration-owner surface.
- [x] 43 prospective owned paths are disjoint from MOD-0190; current dirty input baseline has 190 hash-bound rows with separately recorded publication drift.
- [ ] User/owner approves this pack for `ready-for-dev`.

Status remains `draft`; unchecked items prohibit dispatch.

## 19. Implementation Notes

- **ASSUMPTION:** runtime feature root will be `CapacityPlans`, derived from the frozen route and owned aggregate.
- **ASSUMPTION:** capacity adjustment is an owned scenario assumption; referenced base constraint remains in its
  source SoR and is retained only as source/resource/version provenance.
- **ASSUMPTION:** `demandPlanVersion` is an opaque string because DEMAND v1 does not publish a stronger version type. DEMAND v1 GET `/plan` is keyed by item/period and returns no version, checksum or capture time, so the pack cannot claim live exact-version verification.
- Published six-operation Capacity surface has no approval/archive commands. Design-approved local executor may progress evaluation under later runtime authority; no general algorithm or publisher is inferred.
- MOD-0192 and MOD-0190 are parallel peers only after the shipment lane gate; neither may write the other's data.

## 20. Follow-up Items

- Preserve bounded logistics sequence record; do not conflate it with E5/G5.
- A live supply-constraint adapter, if needed later, requires separate producer-owned contract and owner scope; the bounded slice uses exact fixtures.
- Create an integration-agent WP for gateway and shared permission/registration changes.
- Define a separate tenant UI follow-up if product journeys require one.
- Complete G5 E5 integration across capacity, S&OP, logistics and source reconciliation.

## 21. Published 2.0.0 binding and bounded executor acceptance (proposed draft delta)

Canonical `docs/analysis/contracts/sandop-capacity.openapi.yaml` SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md` SHA-256 `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`. Metadata 2.0.0 does not change wire `contractVersion: v1`. The six operation IDs are `createCapacityPlan`, `getCapacityPlan`, `createCapacityScenario`, `getCapacityScenario`, `evaluateCapacityScenario`, `getCapacityEvaluation`; no other endpoint is owned. The annex's common matrix controls 401→403→400→404→receipt→409/422→commit, original response status/body on replay, current valid response correlation, and scoped receipt recovery on uncertain POST commit. Exact codes and shape are the published YAML/annex, not local guesses.

This slice has Draft plans and scenarios only; evaluation is Accepted→Running→Completed/Failed. `CAPACITY-EVAL-FIXTURE-192-01@1` provides literal Finite/Infinite outputs. Three POST transactions atomically create their aggregate/evaluation, receipt, audit and Pending outbox (submit also creates an active slot); reads are read-only. The feature-local executor design is bound to `mod-0192-executor-exact-decisions-01/DECISION.md` SHA-256 `cfddf953a1a98107869417ef8afbcad5f578fcfde2a8cad8e1d2b6b05197cc5d`: successful scoped claim increments attempt 0→1→2→3 with version/fence and Mongo server-time 30s lease; 10s scan/renewal uses current fence without attempt increment; stale terminal CAS has zero writes; terminal result+slot release+audit+one Pending event commit atomically; after expired claim 3, Failed without claim 4; unknown commit is read-resolved. Computation may repeat, but only one durable terminal effect is allowed. `10s/30s/3` is not a recovery SLA or exactly-once execution. X01–X10 require real isolated replica-set/process/restart evidence; local `$$NOW` smoke is syntax evidence only.

The 43 exact prospective source/test paths are in `mvp6-mod0190-0192-dispatch-preflight-01/MOD-0192-OWNED.tsv`. No `Program.cs`, shared permission, gateway, producer, MOD-0190, publisher, Event Bus worker, optimizer or stock path is included. UI/DataTable/lookup are N/A (`shell:none`). The current draft is not itself a DEV grant; pack promotion and bounded runtime require a separate exact owner decision.
