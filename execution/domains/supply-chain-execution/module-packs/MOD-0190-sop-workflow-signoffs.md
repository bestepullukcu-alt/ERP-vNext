---
id: MOD-0190
name: S&OP Workflow & Sign-offs
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

# MOD-0190 — S&OP Workflow & Sign-offs

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0190 --name "S&OP Workflow & Sign-offs"`
> returned `OK` on 2026-09-15. Blueprint 8.1 and the module ID registry prove the canonical identity.
>
> **Execution gate:** this is a draft specification for a backend/contract initial slice. It grants no runtime-code,
> service-scaffold, gateway, permission-registry or shared-registration authority. The shipment lane must be frozen and
> independently verified before MOD-0190 and MOD-0192 may enter their parallel implementation wave.
>
> **ASSUMPTION:** UI journeys and form fields are not sufficiently defined, so the initial slice uses `shell: none` and
> `golden_reference: none`. Any UI requires an approved revision or follow-up pack.

## 1. Module Summary

MOD-0190 owns the governed S&OP plan, immutable review-input snapshot references and role-based sign-off decisions.
It lets planning, supply, finance, operations and executive actors approve or reject a known snapshot while preserving
an auditable correlation chain. It consumes frozen DEMAND v1 by reference and never becomes another demand source.

The planned initial slice is backend/contract only. It may be dispatched after the MVP-6 shipment lane is verified,
in parallel with MOD-0192, subject to this pack reaching `ready-for-dev`.

## 2. Ownership and Boundaries

**Owns:** `S&OPPlan`, immutable `S&OPSnapshot` provenance, `SignOff`, plan/sign-off lifecycle, idempotency records,
audit evidence and MOD-0190 lifecycle event payloads.

**Consumes:** frozen `DEMAND` v1 (`demandPlanId` and demand plan version only), Event Bus, Workflow and actor identity.

**Must not:** copy forecast series or demand quantities into a second SoR; edit MOD-0188 data; own capacity scenarios,
shipment, warehouse, inventory, supplier or gateway truth; accept tenant/legal-entity scope in request bodies.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `SandopPlan` | Planning horizon, frozen DEMAND reference and current review state |
| `SandopSnapshot` | Immutable input-reference provenance for a review cycle |
| `SignOff` | One role decision against one immutable snapshot |
| Commands | Create plan, capture snapshot, record sign-off |
| Queries | Get plan, list snapshots, list sign-offs |
| API | `/api/supply-chain/sandop-plans/**` from `SANDOP-CAPACITY` v1 |
| Events | Plan created, snapshot captured and sign-off recorded lifecycle events |
| Permissions | `supplychain.sandop-plans.read`, `.create`, `.snapshot.capture`, `.sign-off.record` |

## 4. Entity Fields

### SandopPlan

| Field | Rule |
|---|---|
| `Id` | Server-minted UUID |
| `TenantId` / `LegalEntityId` | Required, server-resolved, never request body |
| `Name` | Trimmed, 1..200 |
| `HorizonStart` / `HorizonEnd` | Date; end is not before start |
| `DemandPlanId` / `DemandPlanVersion` | Required opaque frozen-DEMAND reference |
| `Status` | `Draft`, `InReview`, `Approved`, `Rejected`, `Archived` |
| `CurrentSnapshotId` | Optional UUID; must belong to this plan |
| `Version` | Infrastructure optimistic-concurrency token |
| Soft-delete/audit | Repo-standard `IsDeleted`, `DeletedAt` and audit fields |

### SandopSnapshot and SignOff

| Field | Rule |
|---|---|
| Snapshot provenance | DEMAND plan ID/version, source contract/version, captured time and checksum |
| Supply input references | Opaque external references only; no foreign SoR payload copy |
| Sign-off role | DemandPlanning, SupplyPlanning, Finance, Operations or Executive |
| Decision | Approved or Rejected |
| DecidedBy / DecidedAt | Actor server-resolved; UTC timestamp |
| CorrelationId | Required UUID propagated from command to event/audit |

Tenant-first indexes: unique plan horizon+demand version among active records; unique snapshot sequence per plan;
unique active sign-off by `(TenantId, LegalEntityId, SnapshotId, Role)`.

## 5. Repo Scope

After a later `ready-for-dev` promotion, the bounded initial slice may write only:

- `services/Diten.SupplyChainService/src/**/Features/SandopPlans/**`.
- `services/Diten.SupplyChainService/tests/**/SandopPlans/**`.
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
- Domain-external services and every non-MOD-0190 feature path.
- MOD-0183..0187, MOD-0192 and MOD-0147/0148 runtime paths.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| `SANDOP-CAPACITY` v1 | FROZEN | Owned contract; implement exactly, do not edit during runtime work |
| `DEMAND` v1 | FROZEN | Consume by mock/API; retain only plan ID/version and provenance |
| Shipment lane MOD-0183..0187 | SEQUENCE GATE | Must be frozen and independently verified before dispatch |
| Workflow / actor identity | Building Block | Consume public contract; do not own shared definitions |
| Event Bus | Building Block | Publish lifecycle events with UUID correlation |
| MOD-0192 | Parallel peer | No shared persistence or internal types; integrate by contracts only |

## 8. Runtime Constraints

- Planned service is `Diten.SupplyChainService` on port 5061, using five layers and CQRS/MediatR.
- MongoDB data is tenant/legal-entity scoped; cross-scope access returns 404 without existence leakage.
- Mutations require `Idempotency-Key` and UUID `X-Correlation-Id` per frozen contract.
- Snapshots and sign-offs are append-only evidence. Prior decisions are not overwritten.
- State, audit and outbox event must be atomic or expose an explicit failure; silent partial success is forbidden.
- DEMAND is accessed only through its frozen client/mock; no direct database/internal-type sharing.
- This draft authorizes no runtime code.

## 9. Layout & Shell Contract

`shell: none`. The initial slice has no Razor UI, menu, DataTable, localization surface or frontend route.

## 10. Backend File Convention

The eventual slice uses `Features/SandopPlans/Commands`, `Queries`, `Handlers/CommandHandlers`,
`Handlers/QueryHandlers`, `Validators` and `SandopPlanModels.cs`. Each public command/query/handler/validator has its
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
| `Name` | Create | Trimmed, 1..200 | — |
| Horizon | Create | Valid dates; end >= start | — |
| Demand reference | Create/snapshot | Non-empty ID/version | Frozen DEMAND mock returns Published |
| Source checksum/time | Snapshot | Non-empty checksum, UTC time | DEMAND version must match plan |
| Snapshot ID | Sign-off | UUID | Exists in same tenant/legal entity and plan |
| Role/decision | Sign-off | Frozen enum values | No prior decision for same role/snapshot |

## 13. Failure Path to Verify

- Unknown or unpublished DEMAND version returns 422 `INVALID_DEMAND_REFERENCE` and creates no plan/snapshot.
- Cross-tenant or cross-legal-entity plan returns 404 without existence leakage.
- Duplicate plan horizon+demand version returns 409 and no duplicate aggregate.
- Snapshot in a disallowed plan state returns 409 `SANDOP_PLAN_STATE_CONFLICT`.
- Snapshot not belonging to the plan returns 422 `INVALID_SNAPSHOT_REFERENCE`.
- Repeated role sign-off returns 409 `SIGN_OFF_ALREADY_RECORDED` without overwriting evidence.
- Duplicate idempotency key replays the original result and emits no second event.
- Missing/invalid correlation UUID returns 400 and performs no write.

## 14. Authorization Convention

Tenant actor/service actor with default-deny JWT and permission checks:

- `supplychain.sandop-plans.read`
- `supplychain.sandop-plans.create`
- `supplychain.sandop-plans.snapshot.capture`
- `supplychain.sandop-plans.sign-off.record`

Shared permission definition/seed remains the platform owner's seam. A future runtime WP may consume approved keys
but cannot edit the shared permission registry without a separately authorized owner/integration task.

## 15. Gateway / API Routing Decision

Desired public family is `/api/supply-chain/sandop-plans/**` routed to port 5061. This pack does not authorize an
`ocelot.json` change. An integration-agent WP must add/verify routes and propagation of authorization, tenant,
legal-entity, correlation and idempotency headers after endpoints exist.

## 16. Acceptance Criteria

- [ ] Plan creation stores one tenant/legal-entity plan referencing a Published DEMAND plan version.
- [ ] Snapshot capture stores reference provenance only; no DEMAND quantity/forecast series is persisted locally.
- [ ] Sign-off is bound to an immutable snapshot and role; duplicate role decision cannot overwrite the first.
- [ ] Every command is idempotent and emits its frozen lifecycle event at most once.
- [ ] UUID correlation is preserved from HTTP request through response, audit and lifecycle event.
- [ ] Cross-scope access returns 404; missing permission returns 403.
- [ ] Success and error payloads match `SANDOP-CAPACITY` v1 examples and `contractVersion: v1`.
- [ ] MOD-0190 shares no persistence/internal DTOs with MOD-0192 and runs against frozen mocks.
- [ ] G5 evidence links S&OP decisions to immutable demand provenance without overriding source SoRs.

## 17. Test Expectations

- DCP-002 identity verifier and OpenAPI syntax/reference validation PASS.
- Domain tests cover plan lifecycle, snapshot immutability and sign-off uniqueness.
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
- [ ] Workflow and Event Bus consumed contracts/adapter boundaries are confirmed for implementation.
- [ ] Runtime allowed/protected paths are checked against current repository state.
- [ ] User/owner approves this pack for `ready-for-dev`.

Status remains `draft`; unchecked items prohibit dispatch.

## 19. Implementation Notes

- **ASSUMPTION:** runtime feature root will be `SandopPlans`, derived from the frozen route and owned aggregate.
- **ASSUMPTION:** recording the final required role decision drives plan status through domain policy; the frozen event
  remains the sign-off record, and any additional plan-status event requires a versioned additive contract change.
- **ASSUMPTION:** `demandPlanVersion` is an opaque string because DEMAND v1 does not publish a stronger version type.
- MOD-0190 and MOD-0192 are parallel peers only after the shipment lane gate; neither may write the other's data.

## 20. Follow-up Items

- Record shipment-lane independent verification before pack promotion.
- Resolve/confirm Workflow and Event Bus public contract bindings without editing their shared seams.
- Create an integration-agent WP for gateway and shared permission/registration changes.
- Define a separate tenant UI follow-up if product journeys require one.
- Complete G5 E5 integration across S&OP, capacity, logistics and source reconciliation.
