# Open gates and critical path

## Program-wide gates

1. **Selected integration target acceptance.** The 422-source registered isolated target has writer evidence, but no independent integrated VER and no CT integrated acceptance. The mutable common checkout is not equivalent to it.
2. **Capacity v3 runtime uptake.** The published `CAPACITY_SCENARIO_NAME_CONFLICT` behavior is not applied to the selected 422 target. The prepared three-path patch SHA-256 is `f4fc82b44eb237d1dbc8bdd9c0d9633c948ca4e33cd2dcff91cd65f653780c8f`; target-bound application authority and independent post-apply VER are open.
3. **Carrier real-Auth closure.** The two-file Auth validator rework is writer-complete (`b346415769da7542533b816084d56fe314eb6e6c00d8368a9a419774434572e1`; final 22-path manifest `6221006b5e4bdccff9eb567b49a3e6c111ecb611b9e96d169ee3ff5fe57c1ad2`). The available independent VER report predates the writer handoff, so native independent verification and MVC → Gateway → Carrier E2E remain open.
4. **Supplier decisions.** MOD-0147/0148 need the five bounded concurrences: domain/service ownership, MOD-0140 Supplier+LE responsibility, security binding/revocation, Metric Registry immutable revision policy, and Risk taxonomy/bands/source responsibility. These precede any successor contract disposition, pack promotion, Phase 1.5 close, or DEV.
5. **Final UI dispositions.** Only Carrier has an implemented UI slice. Shipment, Loads, Returns, Claims, S&OP, Capacity, Supplier Performance/Risk, and Supplier Portal lack a final MVP6 UI implementation or a final-scope N/A decision. These rows remain in the denominator.
6. **G5/golden flow.** No record accepts the complete Shipment → Carrier/Load → POD → Return/Claim chain plus source reconciliation and Supplier feedback.
7. **Release operations.** Consumer uptake, live producer seams, publisher/event delivery, data/migration/backfill where applicable, rollback, operational deployment, E5/G5 and production release remain open.

## Module-specific gates

| Module | Exact remaining gates |
|---|---|
| MOD-0183 | Common/integration-target independent acceptance; live Warehouse/Inventory/Event Bus seams; final UI disposition; G5 and rollout. |
| MOD-0184 | Auth token successor independent VER; real Auth list/create/replay/status and persisted-row status offcanvas; fresh permission/tenant/LE/UAS browser evidence; durable PNG if required by acceptance; common-target CT; G5/rollout. |
| MOD-0185 | Multi-Shipment-root operational disposition/live producer uptake; common-target CT; gateway/UI/live ingress/publisher; G5/rollout. |
| MOD-0186 | Durable rematerialization of accepted isolated pack/source into selected target; independent integrated CT; final UI; real Warehouse receiving/Inventory posting if included in final scope; G5/rollout. |
| MOD-0187 | Durable rematerialization into selected target; independent integrated CT; final UI; worker/publisher and financial posting only under a separate approved scope; G5/rollout. |
| MOD-0190 | Independent integrated CT; common checkout uptake; gateway/shared permission; live DEMAND/Workflow/Event Bus publisher; final UI; E5/G5/rollout. |
| MOD-0192 | Apply and independently verify the v3 duplicate-name successor on the exact selected target; then integrated CT; live DEMAND/constraint producers, publisher, final UI, E5/G5/rollout. |
| MOD-0147 | Five owner concurrences; producer-owned artifacts and contract disposition; pack/Phase1.5/promotion/DEV; runtime/VER/integration/UI/release. |
| MOD-0148 | Same concurrences, especially actor binding/current membership/revocation; trusted transport/version; pack/Phase1.5/promotion/DEV; backend and supplier-facing UI; VER/integration/release. |

## Critical path

```text
Supplier five-owner concurrences
  -> producer-owned seam artifacts / one CT contract disposition
  -> MOD-0147 and MOD-0148 pack + Phase 1.5 + bounded DEV/VER

Capacity v3 target-bound uptake authority
  -> apply 3-path successor to selected integration target
  -> independent integrated VER + CT integration acceptance

Carrier Auth successor independent VER
  -> real Auth Carrier browser E2E
  -> Carrier integrated CT disposition

Above module closures + remaining UI decisions/live seams
  -> MVP6 G5/golden-flow VER
  -> operational release/rollout acceptance
```

The integration-target and Carrier Auth paths can proceed independently of Supplier owner concurrences. G5 and release acceptance depend on all relevant paths; bounded backend acceptances do not need to be reopened.
