---
id: MOD-0192
name: Capacity Planning
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: ready-for-dev
status_note: "Owner-promoted isolated pack d01bf7a0… (2026-09-22) bound to CT-accepted bounded evidence: hosted narrow close and BC successor dec28b6a… on published SANDOP-CAPACITY 3.0.0 / wire v1 (§22). Common-checkout integration, gateway/shared registration, live producers, UI, E5/G5 and rollout remain open."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 7
---

# MOD-0192 — Capacity Planning

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0192 --name "Capacity Planning"`
> returned `OK` on 2026-09-15. Blueprint 8.1 and the module ID registry prove the canonical identity.
>
> **Execution gate:** this ready-for-dev pack authorizes only isolated bounded 43-path CapacityPlans core/executor DEV and independent VER under the exact 2026-09-22 owner decision. It grants no service-scaffold, gateway, permission-registry or shared-registration authority. The bounded logistics sequence-evidence gate is recorded.
>
> **ASSUMPTION:** UI journeys and form fields are not sufficiently defined, so the initial slice uses `shell: none` and
> `golden_reference: none`. Any UI requires an approved revision or follow-up pack.

## 1. Module Summary

MOD-0192 owns capacity plans, alternative capacity scenarios and their evaluations/bottleneck results. A plan records requested immutable provenance for one frozen DEMAND plan version; scenarios express opaque
constraint references and owned adjustments. Live DEMAND version/checksum verification remains outside this fixture-only slice; evaluation
calculation/execution use the authorized literal fixture and fenced executor within the isolated DEV scope; no DEMAND facts are copied or changed.

The authorized initial slice is backend/contract only. Its sequence-evidence gate, pack promotion and bounded runtime authority are recorded. MOD-0190 is a disjoint parallel peer.

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

In the approved isolated `ready-for-dev` DEV lane, the bounded initial slice may write only:

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
- `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md` and `docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md`.
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
- The 2026-09-22 exact owner decision authorizes only isolated bounded core/executor DEV and independent VER.

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
- [x] Executor and literal fixture oracle design approved; bounded isolated runtime DEV/independent VER authorized on 2026-09-22.
- [x] Bounded sequence disposition recorded; shared Program.cs/permission/gateway remain one separately authorized integration-owner surface.
- [x] 43 prospective owned paths are disjoint from MOD-0190; current dirty input baseline has 190 hash-bound rows with separately recorded publication drift.
- [x] User/owner approved exact draft target b5b948ee0803c535f91c9a3cc66e6098e7e29635c6b7f2aeaf6873b64eed74fc for isolated `ready-for-dev` promotion and bounded 43-path DEV/independent VER on 2026-09-22.

Status is `ready-for-dev` for the exact isolated 43-path DEV/independent VER scope; composed HTTP, shared integration and product acceptance remain separate.

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

## 21. Published 2.0.0 binding and bounded executor acceptance

Approved: `docs/records/decisions/2026-09/mvp6-capacity-pack-promotion-owner-decision-q28-01.md`

Canonical `docs/analysis/contracts/sandop-capacity.openapi.yaml` SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md` SHA-256 `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`. Metadata 2.0.0 does not change wire `contractVersion: v1`. The six operation IDs are `createCapacityPlan`, `getCapacityPlan`, `createCapacityScenario`, `getCapacityScenario`, `evaluateCapacityScenario`, `getCapacityEvaluation`; no other endpoint is owned. The annex's common matrix controls 401→403→400→404→receipt→409/422→commit, original response status/body on replay, current valid response correlation, and scoped receipt recovery on uncertain POST commit. Exact codes and shape are the published YAML/annex, not local guesses.

This slice has Draft plans and scenarios only; evaluation is Accepted→Running→Completed/Failed. `CAPACITY-EVAL-FIXTURE-192-01@1` provides literal Finite/Infinite outputs. Three POST transactions atomically create their aggregate/evaluation, receipt, audit and Pending outbox (submit also creates an active slot); reads are read-only. The feature-local executor design is bound to `mod-0192-executor-exact-decisions-01/DECISION.md` SHA-256 `cfddf953a1a98107869417ef8afbcad5f578fcfde2a8cad8e1d2b6b05197cc5d`: successful scoped claim increments attempt 0→1→2→3 with version/fence and Mongo server-time 30s lease; 10s scan/renewal uses current fence without attempt increment; stale terminal CAS has zero writes; terminal result+slot release+audit+one Pending event commit atomically; after expired claim 3, Failed without claim 4; unknown commit is read-resolved. Computation may repeat, but only one durable terminal effect is allowed. `10s/30s/3` is not a recovery SLA or exactly-once execution. X01–X10 require real isolated replica-set/process/restart evidence; local `$$NOW` smoke is syntax evidence only.

The 43 exact prospective source/test paths are in `mvp6-mod0190-0192-dispatch-preflight-01/MOD-0192-OWNED.tsv`. No `Program.cs`, shared permission, gateway, producer, MOD-0190, publisher, Event Bus worker, optimizer or stock path is included. UI/DataTable/lookup are N/A (`shell:none`). This promoted pack is bound to the 2026-09-22 exact owner decision for isolated core/executor DEV and independent VER only.

## 22. Accepted bounded scope binding

Approved: `docs/records/decisions/2026-09/mvp6-capacity-pack-promotion-owner-decision-q28-01.md`

This section records, without changing any business rule above, the Control Tower acceptance of the owner-promoted isolated pack and its later owner-approved successors. The owner decision prepared in `docs/roadmap/plans/mvp6-pack-alignment-01/mod-0192-capacity/PROMOTION-DECISION.md` is recorded in the `Approved:` record above.

| Binding | Exact value |
|---|---|
| Owner-promoted isolated pack (this pack before §22, apart from the §6 annex line) | SHA-256 `d01bf7a050472a9708a989d7bd4d13ab0a41a81984a272bbb15848f93fcac6e0`, from approved draft target `b5b948ee0803c535f91c9a3cc66e6098e7e29635c6b7f2aeaf6873b64eed74fc`; recorded in `docs/records/audits/2026-09/mvp6-mod0190-mod0192-isolated-dispatch-01/README.md` |
| X01/X07 production rework, independent VER | `docs/records/audits/2026-09/mvp6-mod0192-x01-x07-independent-ver-01/SOP-22-VER.md` SHA-256 `ae1b2f6bccab9cdf843283c3ba7f107739ceff5f53d95f2245c0b04211f701ad` — PASS at repository scope |
| Hosted HTTP/process narrow close (CT) | `docs/records/audits/2026-09/mvp6-mod0192-hosted-acceptance-consolidate-01/SOP-22.md` SHA-256 `08536c9b334708a802bc189639cf0fc0f9e30a1bebe20cdaa67372f3a903538a` — NARROW CLOSE / PASS on the 422-entry composition |
| Duplicate-name contract publication | `docs/records/audits/2026-09/mvp6-capacity-duplicate-publication-exec-01/SOP-22.md` SHA-256 `47a47bade07301d93904c3229cd5ce3ec1a69f1618f6b9756252d1f745636ebe`; published YAML `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`, annex `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64` |
| Controlling CT acceptance (BC successor) | `docs/records/audits/2026-09/mvp6-bc-successor-ct-handoff-01/README.md` SHA-256 `883efdae53cc3340b78bb8ae3ecb551bc4852e0834b9be926703ead37f692eac` — BOUNDED ACCEPTED; owner decision `mvp6-bc-integration-successor-01/OWNER-DECISION-TEXT.md` SHA-256 `40c830b2442ae6adb8bde2c165681f9ac624ef9a0a361fc5752fc295a013c954` |
| Accepted source | 422-entry manifest `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634`; archive `ebd5d80ca00541235868b94ed9917a53fb63be6e15aa78d0450fa90f33737064`; independent VER `4e4cac58bcc4f8afc3ea990a92cfdeed0e294de485a6cf4a7db7cab982b00b03`, 39/39 CapacityPlans |
| Accepted composition (`Program.cs`, integration-owner surface) | `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0` |

**Composition uptake gate (CT-QUEUE Q292, owner-approved 2026-10-03; ruling Q282):** the accepted composition above stands as the target shape, and its hash is kept. **Uptake is blocked** until MOD-0188 exists, a DEMAND v1.1 returns plan ID, version and checksum, and a SUPPLY-CONSTRAINTS contract exists. Until then this module is not composed, and its `IDemandFixtureReader` is not registered in any composed host, because the only implementation is a declared test-only fixture. Evidence: `docs/records/audits/2026-10/mvp6-q281-demand-seam-01/` (`OPTIONS.md`, `UNRESOLVED.tsv`) and CT-QUEUE Q281, Q282, Q283, Q292.
- DEMAND v1 has only `GET /plan` and `GET /forecast`, keyed by item and period, and returns no version and no checksum; this module matches plan ID + version + checksum (`OPTIONS.md` §2 item 2; `docs/analysis/contracts/demand.openapi.yaml:14-55`).
- The producer MOD-0188 is `reserved / planned` and has no pack (`OPTIONS.md` §2 item 1; `execution/registries/module-id-registry.md:284`).
- No SUPPLY-CONSTRAINTS contract exists in `docs/analysis/contracts/`; a live adapter requires a separate producer-owned contract (`OPTIONS.md` §2 item 4; §20 of this pack).
- The only implementation, `Infrastructure/Features/CapacityPlans/DemandFixtureReader.cs`, hard-codes tenant `19200000-…-0001`, legal entity `…-0002`, plan `dp-2027` version `3` and checksum `sha256:ee56d4f9a3c8`. Registered in a composed host it would accept writes for that one synthetic scope and refuse every real one (`OPTIONS.md` §4; F-Q281-2, CT-QUEUE Q281). The accepted composition registers it as a singleton (`docs/records/audits/2026-09/mvp6-mod0192-http-process-dev-01/PROGRAM-COMPOSITION.patch:18`).
- This `IDemandFixtureReader` is MOD-0192's own (`Domain/Features/CapacityPlans/CapacityScope.cs:10`, `IsExact`). MOD-0190 has a separate interface with a different signature; they are not one shared seam (F-Q281-1, `OPTIONS.md` §4).
- **Executor trap (CT-QUEUE Q283, F-Q281-3; recorded from code reading, not from a run):** `CapacityEvaluationExecutor` is a `BackgroundService` registered nowhere in `src/`. A composition that omits `AddHostedService<CapacityEvaluationExecutor>()` boots with no error, because an unregistered hosted service is absent rather than unresolvable, and evaluations are accepted with 202 and never complete (`UNRESOLVED.tsv`, CapacityEvaluationExecutor row). The accepted composition does register it (`PROGRAM-COMPOSITION.patch:20`), so the trap belongs to any composition that omits that line.

**Contract precedence:** the published 3.0.0 contract above supersedes the 2.0.0 pins in §§3, 7, 16, 18 and 21 only for its published change: `createCapacityScenario` adds 409 `CAPACITY_SCENARIO_NAME_CONFLICT` for an active exact-name duplicate or proven unique-index loser, and the annex pointer moves to v3. Routes, wire `contractVersion: v1` and every other operation are unchanged. No other business rule is changed by this section.

**Owned paths:** the 43 paths of `docs/roadmap/plans/mvp6-mod0190-0192-dispatch-preflight-01/MOD-0192-OWNED.tsv` (SHA-256 `88f2327d81bf2e456cfdb1109145f9b1eeac81b49413ecf5b6244466b6c247a7`). All 43 occur in the accepted 422-entry manifest; none exists in the common checkout yet. `Program.cs` and all MOD-0190 paths are not MOD-0192 owned paths.

**Still open:** common-checkout integration, gateway and shared permission registration, live DEMAND and constraint producers, publisher/Event Bus delivery, UI, exactly-once execution claims, rollout, E5/G5 and full-module acceptance. This section is not `done` status and grants no new DEV scope.

## 23. Tenant UI scope (UI-REVISION-01) — GoldenReferenceSlim

Approved: `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md`

This section adds the tenant UI scope approved as option A in `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-scope-owner-decision-01.md`
(SHA-256 `1814672b501cda4aa3a65e40b41930c013fcea77545a43e63ffb10b237be1553`), bound to the analysis package
`docs/roadmap/plans/mvp6-ui-scope-190-192-01/` (SHA256SUMS `83c6b7e96331e246cacc9f75890b8282c32fb6bda066b2d504d66e22e0f1e985`;
`UI-SCOPE-0192.md` `656ef7701798dd7cd7a8cd2367a19f8d16f1bc645cdecd33a21db66c9c9c03b3`; Phase 1.5 table `PH15-UI-192.md`
`5dee65d1d07b81974de600991ae8ed110ae2c0a1a6edf2eb093cc2dd55af4116`). It stacks on §22 and changes **no** business, contract,
backend, owned-backend-path or acceptance rule in §§1–22. The frontmatter changes `shell: tenant`, `golden_reference: slim` and
`form_field_count: 7` apply to the UI only. For the UI, §9 ("`shell: none`"), §11 ("Not applicable"), the header ASSUMPTION on `shell: none`
and the §21 sentence "UI/DataTable/lookup are N/A (`shell:none`)" are superseded by this section; for the backend they remain as written. `status` stays
`ready-for-dev` (backend scope, per §§21–22). This section does not by itself authorize UI code (§23.14).

### 23.1 Identity

UI revision inside MOD-0192; no FU, child or new ID. The pack author runs
`python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0192 --name "Capacity Planning"` before applying;
non-zero exit stops. The UI adds no owned object, operation, field or permission key.

### 23.2 Layout and shell contract

- `shell: tenant` → the Capacity page views (`Index.cshtml`, `Details.cshtml`) state `Layout = "_LayoutTenantShell";` explicitly; partial views (`_*.cshtml`) set no `Layout`, because an explicit Layout on a partial renders a second shell (Q64b D-02; UI-PM-01); `_ViewStart.cshtml` unchanged.
- View folder `frontend/Diten.Web/Views/SupplyChain/CapacityPlans/`; routes under `/SupplyChain/CapacityPlans`; no `/Platform` prefix; no `Areas/`.
- Skeleton, empty and error are three distinct states (VIEW-001 §3.1); no spinner or "Loading" text.

### 23.3 Bound operations (nothing else)

Published SANDOP-CAPACITY 3.0.0 / wire v1 (YAML `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`, per §22
contract precedence): `createCapacityPlan`, `getCapacityPlan`, `createCapacityScenario`, `getCapacityScenario`,
`evaluateCapacityScenario` (202), `getCapacityEvaluation`. No plan, scenario or evaluation list, search, paging, edit, delete,
approve/archive, cancel/rerun, optimizer, chart, DEMAND/constraint lookup or S&OP call.

### 23.4 Screens, routes, permissions

Browser → same-origin MVC (`proxy-profile`, NET-001) → Gateway 5000 → SupplyChain 5061. The browser never calls 5000/5061 or holds a bearer token.

| Surface | MVC route | Gateway downstream | UI display gate | Backend |
|---|---|---|---|---|
| Entry page | GET `/SupplyChain/CapacityPlans` | — | `supplychain.capacity-plans.read` | — ("Create plan", "Open plan by ID"; **no list**, F192-LIST) |
| Plan workspace page | GET `/SupplyChain/CapacityPlans/Details/{capacityPlanId:guid}`, optional query `?scenarioId={uuid}&evaluationId={uuid}` | — | read | — (plan summary; "Create scenario"; "Open scenario by ID"; scenario and evaluation panels; the optional IDs reopen those panels) |
| Get plan adapter | GET `/SupplyChain/CapacityPlans/api/{capacityPlanId:guid}` | GET `/api/supply-chain/capacity-plans/{capacityPlanId}` | read | `getCapacityPlan` |
| Create plan adapter | POST `/SupplyChain/CapacityPlans/api` | POST `/api/supply-chain/capacity-plans` | `.create` | `createCapacityPlan` |
| Create scenario adapter | POST `/SupplyChain/CapacityPlans/api/{capacityPlanId:guid}/scenarios` | POST `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios` | `.scenario.create` | `createCapacityScenario` |
| Get scenario adapter | GET `/SupplyChain/CapacityPlans/api/{capacityPlanId:guid}/scenarios/{scenarioId:guid}` | GET `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}` | read | `getCapacityScenario` |
| Evaluate adapter | POST `/SupplyChain/CapacityPlans/api/{capacityPlanId:guid}/scenarios/{scenarioId:guid}/evaluations` | POST `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}/evaluations` | `.evaluate` | `evaluateCapacityScenario` (202) |
| Get evaluation adapter | GET `/SupplyChain/CapacityPlans/api/{capacityPlanId:guid}/evaluations/{evaluationId:guid}` | GET `/api/supply-chain/capacity-plans/{capacityPlanId}/evaluations/{evaluationId}` | read | `getCapacityEvaluation` |
| Unsupported list/edit/delete/approve/archive/cancel/bulk/import/export | absent | absent | — | no route, control, proxy or request |

Permission keys stay exactly as published (§14): `supplychain.capacity-plans.read`, `.create`, `.scenario.create`, `.evaluate`.
The UI check is a display decision (UAS-001 §4); `[HasPermission]` on the backend stays the authority.

Adapter headers: `Authorization` server-side only; `X-Correlation-Id` = fresh UUID per request; `Idempotency-Key` = browser
per-intent key forwarded exactly (§12). Tenant and legal entity never come from the browser (§2). Antiforgery on every POST.
"Open plan/scenario by ID" rejects a malformed UUID client-side without a request.

Details address (owner decision F-Q79-05 = A, `docs/records/decisions/2026-09/mvp6-capacity-ui-ids-in-address-owner-decision-01.md`): when a scenario is opened or created and when an evaluation is submitted or
opened, the page writes its ID into the query (`scenarioId`, `evaluationId`) with `history.replaceState`, so a reload or a shared link
reopens the same view through the get-scenario and get-evaluation adapters above. Each value must be a UUID; an invalid value is
ignored with a localized message and sends no request. Unknown, foreign-scope or soft-deleted IDs give the safe-not-found of §23.8.
No new endpoint, field, permission or route segment; no browser storage; the manifest RoutePath (§24) is unchanged.

### 23.5 Workspace panels — bounded table profile (no lists)

The entry page has no DataTable (F192-LIST). The workspace shows the plan summary (name, horizon, provenance, localized status, created);
the **Scenario panel** (name, localized status, constraint references, adjustments with decimal strings as returned) for a scenario
created in this session or opened by ID; and the **Evaluation panel** (mode, localized status Accepted / Running / Completed / Failed,
submitted/completed times) with a client-side **Bottlenecks** table over one `getCapacityEvaluation` response (resource, period,
required, available, shortfall, UoM). The table uses `data-dt-standard="v2"`, `DtDefaults.create()`, `stateSave: false`,
`serverSide: false`; no filter, column visibility, saved view, export or QuickView. Decimal strings are shown exactly as returned,
LTR, with no grouping, rounding or conversion. UUIDs are LTR and copyable. Absent field → "not provided"; malformed envelope →
error, never empty.

### 23.6 Forms — field count and validation

`form_field_count: 7` (create-plan form) → **GoldenReferenceSlim**; offcanvas forms only (no edit mode), `.diten-field` + icon per field.
Client checks mirror the published schemas only; no tightening (§8, §12).

| Form (schema) | Field | Required | UI rule |
|---|---|---|---|
| Create plan (`CreateCapacityPlanRequest`) | `name` | yes | Text sent as typed; no trim or maxlength |
| | `horizonStart`, `horizonEnd` | yes | Dates; end ≥ start mirrored, server authoritative |
| | `demandPlanId`, `demandPlanVersion`, `sourceChecksum` | yes | Free text (F192-DEMAND); no lookup |
| | `sourceCapturedAt` | yes | UTC date-time |
| Create scenario (`CreateCapacityScenarioRequest`) | `name` | yes | Text sent as typed |
| | `constraintRefs[]` (`constraintId`, `source`, `sourceVersion`) | array yes (may be empty); each row all 3 | Repeater; `[]` sent when empty |
| | `adjustments[]` (`resourceRef`, `period`, `availableCapacityDelta`, `uomId`) | array yes (may be empty); each row all 4 | Repeater; `availableCapacityDelta` text, `inputmode=decimal`, pattern `^-?\d+(\.\d+)?$` mirrored, sent as a JSON **string** exactly as typed; never float |
| Evaluate (`EvaluateCapacityScenarioRequest`) | `evaluationMode` | yes | Finite / Infinite |
| | `resourceRefs[]` | yes, ≥ 1 | Repeatable text; order kept |

On 201 from create plan the browser navigates to the returned `capacityPlanId`; on 201 from create scenario the scenario panel opens;
on 202 from evaluate the evaluation panel opens. First-open progress: create-plan form 0/7 required filled.

### 23.7 State-gated actions and refresh

| Action | Shown when | Key |
|---|---|---|
| Create plan | always on the entry page | `.create` |
| Create scenario | plan loaded (server decides the state; 409 `CAPACITY_PLAN_STATE_CONFLICT` otherwise) | `.scenario.create` |
| Evaluate | scenario loaded and no evaluation of it shown as Accepted/Running in this session | `.evaluate` |
| Refresh | evaluation shown as Accepted or Running | read |
| any other | never | — |

**Manual Refresh only (F192-POLL):** each click issues exactly one `getCapacityEvaluation`; no timer, polling loop or automatic
retry. Plan status stays as returned (Draft only in this slice, §4); other enum values are shown only if the server returns them.
No native dialog, manual `Swal.fire` or inline handler.

### 23.8 Errors, replay and concurrency (published codes; localized, never raw text)

| Code (HTTP) | UI behaviour |
|---|---|
| `INVALID_REQUEST`, `INVALID_CORRELATION_ID` (400) | Localized form summary; safe field mapping only (including decimal format) |
| `UNAUTHENTICATED` (401) | Standard session surface; JSON adapters return 401 JSON without redirect |
| `FORBIDDEN` (403) | Page: `_AccessDenied` in shell. Action: close/disable + localized denial |
| `UNKNOWN_CAPACITY_PLAN`, `UNKNOWN_CAPACITY_SCENARIO`, `UNKNOWN_CAPACITY_EVALUATION` (404) | One identical safe-not-found text per resource + support reference for unknown, foreign-scope and soft-deleted targets; panel content removed; late responses never re-expose it |
| `CAPACITY_PLAN_ALREADY_EXISTS`, `CAPACITY_SCENARIO_NAME_CONFLICT` (409) | Specific localized message; inputs kept |
| `CAPACITY_PLAN_STATE_CONFLICT` (409) | Stale-state message + support reference; plan reloads |
| `EVALUATION_ALREADY_ACTIVE` (409) | "An evaluation of this scenario is already running"; no second submit |
| `IDEMPOTENCY_KEY_REUSED` (409) | Stop retry; new user intent required |
| `INVALID_DEMAND_REFERENCE`, `INVALID_CONSTRAINT_REFERENCE` (422) | Specific localized message; inputs kept |
| `DEPENDENCY_UNAVAILABLE`, `COMMIT_RESULT_UNRESOLVED` (503) | Temporarily unavailable; same-key retry; unknown commit never shown as rolled back |

Per intent: one pending request; the same key and **identical body text** on network/503 retry; an edited payload is a new intent.
A replayed 201/202 is shown as completed, then the panel reloads; a replay body is never shown as current state. The response
`X-Correlation-Id` is a copyable support reference.

### 23.9 Localization, UAS-001, accessibility

Seven tenant languages (en, tr, fr, es, zh, ar, ru) in `Resources/Views/SupplyChain/CapacityPlans/{CapacityPlansIndex,CapacityPlanDetails}.{lang}.resx`,
marker classes `CapacityPlansIndex` and `CapacityPlanDetails`, `_IndexL10n.cshtml`/`_DetailsL10n.cshtml` JSON bridges + `index.l10n.js`/`details.l10n.js`.
Keys: the list in `UI-SCOPE-0192.md` §7 (titles, field labels, plan/scenario/evaluation status and mode values, Refresh) plus one key per
error code in §23.8; shared toolbar words from `SharedResource`; no English placeholder in other languages, no hardcoded fallback.
Arabic RTL; UUIDs, versions, checksums, periods and decimal strings LTR-isolated; 390/768/1024/1440 without horizontal overflow;
keyboard focus/Escape in offcanvas and dialogs. UAS-001: without `.read` only `_AccessDenied` inside the shell (no title, form, panel,
skeleton, button, toast or redirect); no control without its key.

### 23.10 Owned UI paths (32, new files only), protected paths, shared handoff

Owned: `frontend/Diten.Web/Controllers/SupplyChainCapacityPlansController.cs`; `frontend/Diten.Web/Models/SupplyChain/CapacityPlans/CapacityPlanViewModels.cs`;
`frontend/Diten.Web/Views/SupplyChain/CapacityPlans/{CapacityPlansIndex.cs, CapacityPlanDetails.cs, Index.cshtml, Details.cshtml, _CreatePlanOffcanvas.cshtml, _CreateScenarioOffcanvas.cshtml, _EvaluateOffcanvas.cshtml, _IndexL10n.cshtml, _DetailsL10n.cshtml}`;
`frontend/Diten.Web/wwwroot/assets/js/SupplyChain/CapacityPlans/{index.js, index.l10n.js, details.js, details.l10n.js}`;
`frontend/Diten.Web/Resources/Views/SupplyChain/CapacityPlans/CapacityPlansIndex.{en,tr,fr,es,zh,ar,ru}.resx`;
`frontend/Diten.Web/Resources/Views/SupplyChain/CapacityPlans/CapacityPlanDetails.{en,tr,fr,es,zh,ar,ru}.resx`;
`frontend/Diten.Web.Tests/{Controllers/SupplyChainCapacityPlansControllerTests.cs, Forms/CapacityPlanFormContractTests.cs, JavaScript/CapacityPlanDetailsBehaviorTests.cs}`.
These are separate from, and add nothing to, the 43 backend owned paths of §22.

Protected for the UI writer: all backend source (including `Features/CapacityPlans/**` and service `Program.cs`), contracts/annexes,
`gateway/**`, shared layouts/partials/JS/CSS, `SharedResource.*.resx`, frontend `Program.cs`/DI, navigation/module/permission catalogues,
`frontend/Diten.Web/tests/diten-field-icons.test.js`, other modules' UI files (including MOD-0190), Golden Slim (read only), packs,
registries, `.antigravity/**`, guards, existing records and Git state.

One CT-appointed **integration owner** alone delivers exact diffs for: gateway routes (**listed, not edited here**): POST
`/api/supply-chain/capacity-plans`, GET `/api/supply-chain/capacity-plans/{capacityPlanId}`, POST `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios`,
GET `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}`, POST `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}/evaluations`,
GET `/api/supply-chain/capacity-plans/{capacityPlanId}/evaluations/{evaluationId}`, explicit routes with OPTIONS (NET-001), `/capacity-plansXYZ`
not matched (§15); page/permission registration; tenant navigation and Ctrl+K with registry-reconciled codes; shared L10n nav keys in
7 languages; the `ICON_MAP` entries; the DCP-009 §21.1 exclusion change; and uptake of the accepted Capacity backend (43 paths +
approved composition, including the executor) into the integrated target. That uptake is held by the §22 composition uptake gate
(CT-QUEUE Q292): it does not start until MOD-0188 exists, DEMAND v1.1 returns plan ID, version and checksum, and a SUPPLY-CONSTRAINTS
contract exists.

### 23.11 Single acceptance matrix (UI) — early vertical slice first

No row has run. HTTP/browser/DB expectations follow §§13 and 16 and the tables above.

| ID | Criterion | Readiness |
|---|---|---|
| CP-VS1 | Early vertical slice: real-Auth actor with all four keys creates a plan on the exact DEMAND fixture (201 Draft), creates a scenario with one constraint reference and one adjustment `"10.5"` (201), submits a Finite evaluation for one resource (202 Accepted), presses Refresh until Completed; the bottleneck table shows the `CAPACITY-EVAL-FIXTURE-192-01@1` literal decimal strings exactly; DB +1 plan, +1 scenario, +1 evaluation, active slot released, audits and Pending events per §16 | BLOCKED (target, gateway, permission seed, fixture seed, executor on target) |
| CP-01 | Same-origin chain: browser requests only `/SupplyChain/CapacityPlans/**`; none to 5000/5061; no bearer token in the browser | BLOCKED (gateway) |
| CP-02 | No list request anywhere; open plan/scenario by ID; malformed UUID blocked client-side; unknown IDs → safe-not-found per resource | READY |
| CP-03 | Skeleton / empty / error distinct in workspace, panels and bottleneck table | READY |
| CP-04 | UAS-001 without `.read` | READY |
| CP-05 | Action gating by key (§23.7); direct POST without key → 403, zero writes | READY |
| CP-06…CP-08 | Create-plan (7 fields), create-scenario (name + two arrays, `[]` when empty) and evaluate (mode, ≥ 1 resource) body parity with the schemas; no trim or tightening | READY |
| CP-09 | `availableCapacityDelta` sent as the typed string; bottleneck and adjustment decimals shown exactly as returned; no float, grouping or rounding | READY |
| CP-10 | 202 opens the evaluation panel; each Refresh click = exactly one `getCapacityEvaluation`; no automatic polling; Refresh hidden for Completed/Failed | READY |
| CP-11 | Second submit while active → 409 `EVALUATION_ALREADY_ACTIVE`; no second evaluation in DB | READY |
| CP-12 | 409 `CAPACITY_PLAN_ALREADY_EXISTS`, `CAPACITY_SCENARIO_NAME_CONFLICT` (3.0.0) and `CAPACITY_PLAN_STATE_CONFLICT` localized; inputs kept | READY |
| CP-13 | Unknown, foreign-scope and soft-deleted plan/scenario/evaluation → identical safe-not-found | READY |
| CP-14 | Idempotency: same key and body on retry; replay shown as completed; edited payload new intent; `IDEMPOTENCY_KEY_REUSED` stops | BLOCKED (DN-01) |
| CP-15, CP-16 | 422 demand/constraint reference messages with inputs kept; 503 same-key retry, unknown commit not shown as rolled back | READY |
| CP-17 | 7 languages + RTL; LTR isolation; 390/768/1024/1440; keyboard | READY |
| CP-18 | Family routing `/capacity-plansXYZ`; regression incl. Capacity backend suites on target | BLOCKED |
| CP-19 | Source→binary→process→browser binding | READY |
| CP-20 | PNG via supported export only | BLOCKED (PRES-183-04) |
| CP-21 | Reload of `/Details/{capacityPlanId}?scenarioId=…&evaluationId=…` reopens the same scenario and evaluation panels (one get-scenario and one get-evaluation request); the IDs are written to the address when a scenario is opened/created and an evaluation submitted/opened | READY |
| CP-22 | A shared link opened by another actor with `.read` in the same tenant and legal entity shows the same view; a foreign-scope actor gets the safe-not-found per resource | READY |
| CP-23 | A `scenarioId` or `evaluationId` that is not a UUID shows a localized message and sends no backend request; the plan still loads | READY |

OUT at the start (scope rows, approved as option A): CP-SCR-01 plan / scenario / evaluation lists, search, paging — **no list: the
contract has no list operation** (O-01, F192-LIST); CP-SCR-02 edit or delete plan/scenario, approve/archive plan (O-02); CP-SCR-03 cancel
or rerun other than a new submit (O-03); CP-SCR-04 charts, optimizer, browser-side what-if (O-04); CP-SCR-05 DEMAND/constraint lookups,
demand quantities (O-05); CP-SCR-06 Event Bus, publisher, notifications, live producers (O-06); CP-SCR-07 import/export, column
visibility, saved views (O-07); CP-SCR-08 S&OP data (O-08); CP-SCR-09 automatic polling (F192-POLL); CP-SCR-10 generic
`verify_datatable_page.py --reference slim` as acceptance (run and kept as a record only).

### 23.12 Test expectations (UI)

Module tests in the three owned test files; `python3 .antigravity/scripts/verify_datatable_page.py . --area SupplyChain --module CapacityPlans --reference slim`
run and recorded (CP-SCR-10); RESX parity 7/7 for both markers with no placeholder; build of frontend, gateway and SupplyChain service
on the integrated target; real-Auth browser smoke with read-only, create, scenario and evaluate identities in separate profiles, including
one evaluation reaching Completed and one reaching Failed on the fixture/executor; `grep` scans for native dialogs, inline handlers,
timers/polling and "Permission denied"/"Forbidden" text; independent VER on a frozen source.

### 23.13 Remaining gaps (recorded, not decided here)

- **F192-LIST:** no plan, scenario or evaluation browsing; a user reaches them only by returned or pasted IDs. Lists need a versioned contract change (§5).
- **F192-DEMAND:** provenance, constraint references, resource refs and UoM are free text, usable with the exact fixtures only.
- **F192-POLL:** manual Refresh only; automatic polling is an owner choice and a scope amendment.
- **Session memory:** resolved by F-Q79-05 = A — scenario and evaluation IDs are carried in the Details address (§23.4; CP-21…CP-23).
- **Verifier tension:** the generic DataTable verifier expects a list page; record-only until a scope-aware profile (DN-02 / PRES-183-03) exists.
- G-TARGET integrated target; Capacity backend and executor absent from the common checkout; DN-01 retry policy; PNG; nav codes; DCP-009 §21.1 exclusion; G-ICONMAP.

### 23.14 Effort and authorization boundary

O/M/P (person-hours, option A; replaces `0192-1-REMAINING` 4.8/8/12.8 and `0192-4-REMAINING` 38.4/64/128 and is the UI sub-item of
`0192-5-REMAINING` and `0192-6-REMAINING`; not additive): pack/Phase 1.5 4/8/16; frontend 32/52/88; shared integration 8/14/24;
independent UI VER 14/24/40; **total 58/98/168** (`ESTIMATE.tsv` `03669f33abb67a6be2ebdd1ae4e17fa26a7566d9190dc5d551f4969076a109e3`).

Not authorized by this section: UI code other than under the owner's separate decisions of 2026-09-26 ("modules first", draft
overlays; CT verdict Q64a F2) and a versioned UI dispatch; gateway, permission, navigation, L10n, icon-map or DCP edits except by the
single integration owner; contract or backend changes; `done` status; commit or push.

## 24. Self-registration

Approved: `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md`

**Authority:** `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md` (D1–D5 = A). **Design:** [`mvp6-self-registration-prep-01`](../../../../docs/roadmap/plans/mvp6-self-registration-prep-01/MANIFESTS.md) (MANIFESTS.md, NAV-L10N-KEYS.tsv, TEST-PLAN.md); MANIFESTS.md recorded MOD-0192 as "no UI scope → no manifest now", which §23 lifts.
**Foundation:** DCP-009 §21 (single integration owner).
This section specifies; it authorizes no code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, and no commit, push or stash.

### Identity (D3 = A)

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `capacity-planning` / `CapacityPlanning` / `Capacity Planning` |
| Domain / Service | `SupplyChainExecution` / `DitenSupplyChainService` |
| ModuleVersion / IsTenantAssignable / IsBaseline | `1.0.0` / true / false |
| SortOrder / Icon (SOFT, proposed) | 450 / `bx-bar-chart-alt-2` |
| Provider / tests (proposed paths) | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/CapacityPlanningManifestProvider.cs` / `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/CapacityPlanningManifestProviderTests.cs` |
| Scope | every RoutePath starts with `/SupplyChain/`, none with `/Platform/` → Tenant |

### Pages

| PageCode | DisplayName | RoutePath | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `CAPACITY_PLANS` | Capacity Plans | `/SupplyChain/CapacityPlans` (§23.4; not a route yet) | `supplychain.capacity-plans.read` | — | **true** | List (entry page without table, F192-LIST) | 10 |
| `CAPACITY_PLAN_DETAILS` | Capacity Plan | `/SupplyChain/CapacityPlans/Details/{capacityPlanId:guid}` | `supplychain.capacity-plans.read` | `CAPACITY_PLANS` | false | Detail | 11 |

### Actions

| Page | ActionCode | DisplayName | PermissionKey | Placement | Dangerous |
|---|---|---|---|---|---|
| `CAPACITY_PLANS` | `CREATE` | Create Plan | `supplychain.capacity-plans.create` | Toolbar | no |
| `CAPACITY_PLAN_DETAILS` | `CREATE_SCENARIO` | Create Scenario | `supplychain.capacity-plans.scenario.create` | Toolbar | no |
| `CAPACITY_PLAN_DETAILS` | `EVALUATE` | Evaluate | `supplychain.capacity-plans.evaluate` | Toolbar | no |

Existing keys only: `CapacityPermissions` constants `Read`, `Create`, `ScenarioCreate`, `Evaluate` (accepted source, archive `ebd5d80c…7064`). Pages and buttons mirror the §23 tenant UI scope.

### Navigation keys (0/7 present today)

| Key | Required languages | Ships with |
|---|---|---|
| `Nav.Module.CAPACITYPLANNING` | en, tr, fr, es, zh, ar, ru | this provider |
| `Nav.Page.CAPACITY_PLANS` | en, tr, fr, es, zh, ar, ru | this provider |

Values are added to `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` by the integration owner and reviewed by the l10n agent (no empty value, no English placeholder, no key echoing its own name). This pack does not supply or approve values.

### Tests (TEST-PLAN.md §2; all required before the module counts as closed)

| ID | Pass condition |
|---|---|
| M-01 | Identity exactly as above; `IsTenantAssignable` true |
| M-02 | Every manifest `RequiredPermission` / `PermissionKey` is in the reflected `public const string` fields of `CapacityPermissions` |
| M-03 | Every reflected key is in the manifest or on the API-only allow-list with a reason: none expected (all four constants are modeled) |
| M-04 | Manifest RoutePaths = the frontend view-route set of `SupplyChainCapacityPlansController` (§23.4; to be built), counts equal (cross-checked by W-01) |
| M-05 | The action table above equals the manifest actions per page (code, key, placement, dangerous flag) |
| M-06 | PageCodes, RoutePaths and ActionCodes (per page) unique, case-insensitive |
| M-07 | Exactly one `IsNavigationVisible` page, with a null parent; every other page has a parent |
| M-08 | No RoutePath starts with `/Platform/` |

Shared guards W-01…W-04 and reconcile-state R-01…R-04 (DCP-009 §21.3) must also be green for this module.

### Ship rule (D4 = A)

The provider, its `AddSingleton<IModuleManifestProvider, …>` line and its navigation keys ship **together with this module's UI** in the integrated target, never ahead of it (SR-D4 overlay built with the UI, applied only at final integration).

### Open gaps (carried, not solved)

1. `CapacityPermissions.cs` exists only in the accepted isolated source, not in the common checkout.
2. Closed: DCP-009 §21.1 no longer excludes MOD-0192 (applied DCP-009 text SHA-256 `ab728d67662037cd6bb73a8f5ad89f82c8fac64f2faf364d72d874cd21328fdf`; approved in `docs/records/decisions/2026-09/mvp6-text-patch-q83-signoff-owner-decision-01.md` (SHA-256 `d27458e19cda13c7453bd3c5378c22286078b3551f081ae5d91f0db1aff381da`)).
3. The Capacity UI is scoped (§23) but not built; routes and actions are re-checked against the built UI (M-04/M-05, W-01/W-02).
4. Runtime tests R-02…R-04 need a native executor and the integrated target.
