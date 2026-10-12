# MVP6-MOD0192-SPEC-01 — Capacity Planning spec delta

**Status:** specification candidate; pack remains `draft`; no runtime dispatch. Authority pins on 2026-09-22: `MOD-0192-capacity-planning.md` SHA-256 `edd550b84451af082b934b392cd34f7dc24dd6e462f7e1d9ee0e03c21b4469f7`; `sandop-capacity.openapi.yaml` SHA-256 `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c`; `demand.openapi.yaml` SHA-256 `3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d`. Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; checkout was dirty and concurrently used. No whole-tree cleanliness claim.

## Exact operation boundary

| Frozen operation | Input and response | Persistence/effect the draft can require | Absent from frozen surface |
|---|---|---|---|
| `POST /api/supply-chain/capacity-plans` | Name, horizon, DEMAND ID/version, source capture timestamp/checksum → 201 `CapacityPlan`/Draft | Scoped plan, immutable provenance, idempotency receipt, audit, `capacity.plan.created.v1` outbox | Authoritative DEMAND ID/version/checksum lookup; plan approval/archive command |
| `GET /api/supply-chain/capacity-plans/{capacityPlanId}` | Scoped ID → 200 plan or 404 | Read persisted plan; no mutation | Plan listing or state mutation |
| `POST /api/supply-chain/capacity-plans/{capacityPlanId}/scenarios` | Name, `constraintRefs[]`, `adjustments[]` → 201 Draft scenario | Scoped scenario with opaque constraint IDs/source versions and owned deltas, idempotency receipt, audit, `capacity.scenario.created.v1` outbox | Constraint producer lookup/version proof; base constraint data copy |
| `GET /api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}` | Scoped nested IDs → 200/404 | Read the persisted nested scenario | Scenario update/archive command |
| `POST /api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}/evaluations` | `Finite`/`Infinite`, nonempty `resourceRefs[]` → 202 `Accepted`, empty bottlenecks, null `completedAt` | Atomically persist accepted evaluation and one active-scenario claim/receipt; execution may begin only through a separately pinned bounded mechanism | Trigger transport, worker ownership/restart policy, finite/infinite calculation semantics, source quantities |
| `GET /api/supply-chain/capacity-plans/{capacityPlanId}/evaluations/{evaluationId}` | Scoped IDs → 200 Accepted/Running/Completed/Failed plus bottlenecks or 404 | Read durable evaluation state/result; Completed result is persisted, not recomputed on GET | Completion deadline, retry schedule, extra status endpoint |

Frozen `CapacityPlan` statuses are `Draft/Evaluating/Ready/Approved/Archived`, scenario statuses `Draft/Evaluating/Evaluated/Archived`, and evaluation statuses `Accepted/Running/Completed/Failed`. The six operations contain no explicit plan approval/archive or scenario archive transition. Their enum values and examples alone do not authorize such commands. `capacity.evaluation.completed.v1` has an event payload status enum `Completed/Failed`; the exact event contract does not define `capacity.evaluation.accepted` or `running` events.

## Constraint and evaluation input split

`ConstraintReference` requires `constraintId`, `source`, `sourceVersion`; `CapacityAdjustment` requires `resourceRef`, `period`, invariant decimal-string `availableCapacityDelta`, `uomId`. Empty arrays are permitted by the frozen schema. The draft describes an opaque `SUPPLY-CONSTRAINTS` building block, but no versioned public lookup or resource-scope contract was found. Thus the initial isolated mock can return declared fixture refs and exercise validation/retention; it cannot attest a live producer version, resource ownership, available capacity or UoM conversion.

Evaluation POST supplies mode and resource references, not required-capacity facts or an algorithm. The sample bottleneck (`520.000/480.000/40.000 HOUR`) is an example, not a normative optimization formula. No scheduler, worker frequency, queue or retry policy is specified. Owner must pin how an Accepted record is executed and how a result is calculated or injected from a declared reference fixture before DEV acceptance. Until then, an endpoint can prove durable `202 Accepted` only; it cannot claim completed evaluation/bottleneck correctness.

`CapacityEvaluation` requires `bottlenecks[]` at every status and permits nullable `completedAt`. For terminal outcomes, store an immutable result/status/time and emit the single frozen completion event through an outbox bound to that transition. Failed-result error detail and whether Failed carries empty bottlenecks are not specified; no invented failure payload is proposed.

## DEMAND provenance boundary

The request requires `demandPlanId`, `demandPlanVersion`, `sourceCapturedAt`, `sourceChecksum`, and the response persists `InputSnapshotProvenance` with `sourceContract=DEMAND`, `sourceContractVersion=v1`. `DEMAND` v1 `GET /plan` is queried by `itemId` (optional period), and returns `planId/status` without plan version, checksum or capture timestamp. `GET /forecast` also lacks this check. `DemandPlanPublishedEvent` carries plan/item/period only. Consequently a frozen mock with a separately declared fixture may verify a chosen ID/version/checksum tuple **within that fixture**; the current DEMAND contract cannot prove that tuple against a live producer. Do not treat client-supplied checksum/timestamp as authoritative producer evidence. No DEMAND forecast quantity/confidence/series may be persisted as another SoR.

MOD-0190 consumes the same DEMAND seam. This package records one shared GAP and defers any producer amendment to the DEMAND/CT single writer; it does not design a parallel 0192-only endpoint or redefine 0190's fixture.

## Owned paths and protected seams

Potential later DEV owner, only after explicit dispatch: `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/CapacityPlans/**`, `...Application/Features/CapacityPlans/**`, `...Domain/Features/CapacityPlans/**`, `...Persistence/Features/CapacityPlans/**`, `...Infrastructure/Features/CapacityPlans/**`, and `services/Diten.SupplyChainService/tests/**/CapacityPlans/**`. The draft's wildcard `src/**/Features/CapacityPlans/**` is interpreted as these exact five project roots, not permission for another project or shared file. `Features/SandopPlans/**` belongs solely to MOD-0190.

Shared `Program.cs`, project/solution registration, permission catalog/seed, Event Bus binding, gateway Ocelot and frozen `sandop-capacity.openapi.yaml` require a separate single integration/contract writer and exact source/target hash authorization. MOD-0192 cannot edit DEMAND, other feature paths, registry, gateway, shared contract or pack status in this task.

## Proposed pack change

`proposed-pack.patch` is the only proposed unified diff. It clarifies existing draft assertions and adds explicit unresolved gates; it is **not applied**. It does not change the pack status or frozen contract. Owner decisions, consumed-seam gaps, acceptance and held dispatch are in adjacent files.
