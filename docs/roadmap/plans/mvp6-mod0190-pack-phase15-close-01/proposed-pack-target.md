---
id: MOD-0190
name: S&OP Workflow & Sign-offs
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Published SANDOP-CAPACITY 2.0.0 bound to fixture-only proposed scope; Phase 1.5 owner approval, pack promotion and isolated runtime authority remain open."
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

**Consumes in this bounded slice:** frozen DEMAND v1 via exact tenant/LE-scoped test fixtures and trusted JWT actor. No Workflow HTTP or Event Bus delivery; lifecycle events remain atomic Pending outbox records.

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
| API | Six `/api/supply-chain/sandop-plans/**` operations from published SANDOP-CAPACITY 2.0.0, wire `v1` |
| Events | Plan created, snapshot captured and sign-off recorded lifecycle events |
| Permissions | `supplychain.sandop-plans.read`, `.create`, `.snapshot.capture`, `.sign-off.record` |

## 4. Entity Fields

### SandopPlan

| Field | Rule |
|---|---|
| `Id` | Server-minted UUID |
| `TenantId` / `LegalEntityId` | Required, server-resolved, never request body |
| `Name` | Schema-valid nonempty string, exact value; no trim or added max-200 rule |
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

The 38 exact prospective runtime/test paths are pinned by `docs/roadmap/plans/mvp6-mod0190-0192-dispatch-preflight-01/MOD-0190-OWNED.tsv`. `Program.cs`, common DI, shared permission registry and gateway belong to integration owners.

`docs/analysis/contracts/sandop-capacity.openapi.yaml` is frozen implementation authority and is read-only during
runtime work. Contract changes require a separate versioned, single-writer specification task.

## 6. Protected Paths

- `.antigravity/**`.
- `docs/analysis/contracts/demand.openapi.yaml` and every other module-owned frozen contract.
- `docs/analysis/contracts/sandop-capacity.openapi.yaml` and `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md` during runtime implementation.
- `gateway/Diten.ApiGateway/**/ocelot.json`; integration-agent only.
- Frontend Archive paths and `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- Domain-external services and every non-MOD-0190 feature path.
- MOD-0183..0187, MOD-0192 and MOD-0147/0148 runtime paths.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| `SANDOP-CAPACITY` 2.0.0 / wire `v1` | PUBLISHED / FROZEN | YAML SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex SHA-256 `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`; read-only during DEV |
| `DEMAND` v1 | FROZEN / TEST FIXTURE ONLY | Exact tenant/LE/ID/version/checksum fixture; no invented endpoint or real producer validation claim |
| Shipment lane MOD-0183..0187 | BOUNDED SEQUENCE EVIDENCED | CT sequence disposition met; not full E5/G5 or shared-checkout uptake |
| Workflow / actor identity | JWT ACTOR ONLY | Trusted JWT actor; no Workflow HTTP/template integration in this slice |
| Event Bus | PENDING OUTBOX ONLY | Three frozen lifecycle events atomically persisted Pending; no publisher/worker/delivery claim |
| MOD-0192 | Parallel peer | Disjoint 38/43 paths; no shared persistence or internal types |

## 8. Runtime Constraints

- Planned service is `Diten.SupplyChainService` on port 5061, using five layers and CQRS/MediatR.
- MongoDB data is tenant/legal-entity scoped; cross-scope access returns 404 without existence leakage.
- Mutations require a nonempty exact parsed `Idempotency-Key`; all six operations require one valid UUID `X-Correlation-Id`. No trim, case folding, Unicode normalization or added max-200 rule.
- Snapshots and sign-offs are append-only evidence. Prior decisions are not overwritten.
- Each mutation atomically writes state, durable receipt, audit and one Pending lifecycle event or exposes an explicit failure. Same-key/same-valid-payload replay returns original status/body without reread or new writes; current response correlation differs from original audit/event correlation when retried. Changed valid payload returns 409 `IDEMPOTENCY_KEY_REUSED`; unresolved unknown commit follows published 503 `COMMIT_RESULT_UNRESOLVED` and scoped receipt recovery.
- DEMAND ID/version/checksum is checked only against exact scoped test fixtures; no live DEMAND HTTP, Workflow HTTP, direct database/internal-type sharing or publisher is in this slice.
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
| `Idempotency-Key` | Mutations | Nonempty exact parsed value; no trim or max-200 rule | Scoped operation+target receipt and decoded-JSON fingerprint |
| `X-Correlation-Id` | All six operations | Single valid UUID; missing/malformed/duplicate → 400 `INVALID_CORRELATION_ID` | Current response trace; original audit/event trace on replay |
| `Name` | Create | Schema-valid nonempty exact string; no trim/max-200 rule | — |
| Horizon | Create | Valid dates; end >= start | — |
| Demand reference | Create/snapshot | Nonempty ID/version, checksum/time where published | Exact scoped test fixture; DEMAND v1 has no ID+version endpoint |
| Source checksum/time | Snapshot | Non-empty checksum, UTC time | DEMAND version must match plan |
| Snapshot ID | Sign-off | UUID | Exists in same tenant/legal entity and plan |
| Role/decision | Sign-off | Frozen enum values | No prior decision for same role/snapshot |

## 13. Failure Path to Verify

- Unknown/unpublished/cross-scope or checksum-mismatched exact DEMAND fixture returns 422 `INVALID_DEMAND_REFERENCE` with zero writes; fixture evidence is not live producer validation.
- Cross-tenant or cross-legal-entity plan returns 404 without existence leakage.
- Duplicate plan horizon+demand version returns 409 and no duplicate aggregate.
- Capture Draft/InReview appends an immutable snapshot and sets InReview/currentSnapshotId; Approved/Rejected/Archived returns 409 `SANDOP_PLAN_STATE_CONFLICT`.
- Sign-off outside InReview returns 409 `SANDOP_SIGN_OFF_STATE_CONFLICT`; wrong-plan snapshot returns 422 `INVALID_SNAPSHOT_REFERENCE`.
- Repeated role sign-off returns 409 `SIGN_OFF_ALREADY_RECORDED` without overwriting evidence.
- Same scoped key and equivalent valid payload replays original status/body with current response correlation and original event/audit correlation, no reread or second event; changed valid payload returns 409 `IDEMPOTENCY_KEY_REUSED`.
- Missing/malformed/duplicate correlation returns 400 `INVALID_CORRELATION_ID` with one rejection-only UUID in error body/header and no write. Known dependency failure is 503 `DEPENDENCY_UNAVAILABLE`; unresolved mutation commit is 503 `COMMIT_RESULT_UNRESOLVED` after scoped receipt resolution.

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

- [ ] Create stores one scoped Draft plan with null currentSnapshotId referencing an exact scoped Published DEMAND ID/version fixture; this does not prove live producer validation.
- [ ] Draft/InReview capture appends immutable fixture ID/version/checksum/time provenance, updates plan to InReview and preserves older snapshots; no DEMAND series is stored.
- [ ] InReview sign-off binds to any same-plan immutable snapshot and role; duplicate role cannot overwrite the first and five approvals do not automatically advance plan status.
- [ ] Three mutations use scoped durable receipt, exact-key/decoded-JSON fingerprint, original-result replay and atomic audit plus at most one Pending event; no publisher or Workflow call.
- [ ] Current UUID is in response header/error; original UUID remains in audit/event on replay. Missing/malformed/duplicate UUID and application-generated 401 follow published fallback.
- [ ] Cross-scope access returns 404; missing permission returns 403.
- [ ] Six operations and three event payloads match published SANDOP-CAPACITY 2.0.0 YAML/annex, wire `contractVersion: v1`, including 400/401/409/422/503 precedence and required/null fields.
- [ ] MOD-0190 shares no persistence/internal DTOs with MOD-0192 and runs against frozen mocks.
- [ ] Bounded E4 evidence links decisions to immutable fixture provenance without overriding source SoRs; live producer uptake and E5/G5 are separate gates.

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
- [x] Published SANDOP-CAPACITY 2.0.0 YAML/annex and MOD-0190 consumer repin hashes verified; DEMAND v1 remains fixture-only here.
- [x] Fixture-only DEMAND, exact key/name, replay and validation boundaries map to the published annex; no uncontracted endpoint or narrower key/name rule.
- [x] Bounded MOD-0183..0187 sequence evidence is recorded by Returns/Claims CT; not full E5/G5.
- [x] Owner approved trusted JWT actor/no Workflow and atomic Pending-only outbox/no publisher for this bounded slice.
- [x] The 38 prospective SandopPlans paths are absent and disjoint from MOD-0192's 43; shared composition is integration-owned. Recheck hashes at dispatch.
- [ ] User/owner approves this pack for `ready-for-dev`.

Status remains `draft`; unchecked items prohibit dispatch.

## 19. Implementation Notes

- **ASSUMPTION:** runtime feature root will be `SandopPlans`, derived from the frozen route and owned aggregate.
- **Published bounded policy:** sign-off stores an immutable decision while the plan stays InReview; no automatic approval/quorum/status event. Any future approval transition needs separate owner/contract authority.
- **ASSUMPTION:** `demandPlanVersion` is an opaque string because DEMAND v1 does not publish a stronger version type.
- MOD-0190 and MOD-0192 are parallel peers only after the shipment lane gate; neither may write the other's data.

## 20. Follow-up Items

- Record shipment-lane independent verification before pack promotion.
- Keep Workflow and Event Bus delivery outside this bounded slice; later live integration needs separate producer/owner binding.
- Create an integration-agent WP for gateway and shared permission/registration changes.
- Define a separate tenant UI follow-up if product journeys require one.
- Complete G5 E5 integration across S&OP, capacity, logistics and source reconciliation.

## 21. Published-contract reconciliation (proposed draft target)

This draft target binds the six operations to published YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` and annex `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`. D190-01…03 bounded fixture/JWT/Pending scope and D190-04/05 candidate design were later followed by exact consumer consent/publication. Those decisions do not promote this pack, approve Phase 1.5, authorize runtime DEV or shared Program.cs composition. See `mvp6-mod0190-pack-phase15-close-01` for exact proposed patch and HELD dispatch.
