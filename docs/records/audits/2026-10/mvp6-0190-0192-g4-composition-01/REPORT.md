---
module: MOD-0192
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: pass
commit:
modules: [MOD-0190, MOD-0192]
---

# Wave 6.1 G4 — S&OP + Capacity composed where a demand source exists; live through the gateway

    run       : 2026-10-18 by CT (gmg-10)
    authority : §22 gate opened (`mvp6-0190-0192-section22-gate-open-owner-decision-01`); seed + production rule
                (`mvp6-next-steps-owner-decisions-2026-10-18` #3, refined by the owner the same day: compose ONLY
                where a demand source exists — production stays uncomposed until DEMAND v2, honouring the K4 note
                in Program.cs)
    spec      : `docs/roadmap/plans/mvp6-0190-0192-ui-wave-01/G4-COMPOSITION-SPEC.md` (C1–C3 + items A/B/C)
    verdict   : **PASS** for the composed (seeded, non-production) host. Production behaviour unchanged by design.

## What changed

| spec | change |
|---|---|
| gate | `Api/Composition/DemandConsumerComposition.SeedFor(config, env)`: returns a seed only outside Production, only when `DemandFixtures:Enabled` and every field is present; enabled-but-incomplete **throws** (never composes a module that refuses everything). |
| C1 | `AddApplication(includeDemandConsumers)` — one `AddMediatR`; the S&OP/Capacity exclusion is lifted only when seeded. Default false keeps every caller as it was. |
| C2 | Seeded hosts register `AddSandopPersistence`, `AddCapacityPersistence`, the S&OP reader built from the seed, a `CapacityDemandFixture`, and the Capacity demand/constraint readers. |
| C3 | Capacity's pipeline branch runs in seeded hosts as well as the Testing host (Q484 otherwise unchanged). |
| item A | Seed is configuration (`DemandFixtures:{Enabled,TenantId,LegalEntityId,PlanId,Version,Checksum}`), none in the repo and no GUIDs in Program.cs. Capacity readers gained a fixture constructor; the parameterless one keeps the bounded constants, so module tests are untouched. |
| item B | `SandopContextMiddleware.IsSandopPath` keeps the Shipment catch-all off `/api/supply-chain/sandop-plans` (S&OP resolves its own context; otherwise invalid S&OP requests got Shipment-contract errors — the Q271/Q272 shape). |
| item C | **Deferred to G6, deliberately**: the §24 manifest providers declare UI pages; registering them before the UI exists would put dead navigation in the Platform catalog. |

## Two defects found by running it live (both fixed)

1. **The Capacity evaluation executor was registered nowhere in production** (`CapacityEvaluationExecutor : BackgroundService`;
   tests call `RunOnceAsync` directly). A composed host would leave every evaluation `Accepted` forever. Now registered in the
   seeded block.
2. **Its fixture oracle hard-coded the bounded scope** (`CapacityEvaluationExecutor.cs:27`), so a seeded scope's evaluation
   ended `Failed`. Same two-constructor fix as the readers (bounded default, seeded constructor). Measured: run 1 `Failed`,
   run 2 `Completed`.

## Live evidence (guarded lane: gateway 5000 with signing key, supplychain 5066 seeded, mdm 5059, lane mongod 35151; 27017 untouched)

Through the gateway (`/api/supply-chain/...`, the routes reserved by the gateway ruling — they now serve):

| module | flow | result |
|---|---|---|
| MOD-0190 S&OP | create → get → capture snapshot → list → record sign-off → list | 201 · 200 · 201 · 200 · 201 · 200 |
| MOD-0192 Capacity | create plan → get → create scenario → get → evaluate → poll | 201 · 200 · 201 · 200 · 202 `Accepted` → **`Completed`** |

Bottleneck (fixture-literal oracle, decimal strings): `line-4 / 2027-W03 / required 520.000 / available 480.000 /
shortfall 40.000 / HOUR`. A repeat create for the same horizon + demand version answered `409 *_ALREADY_EXISTS` (correct).

Persistence reconciles with each module's model (two runs): S&OP audit 6 (Create 2, Capture 2, SignOff 2), outbox 6,
receipts 6; Capacity audit 8 (create 2, scenario 2, evaluate 2, terminal 2), outbox 6 (`plan.created` 2,
`scenario.created` 2, `evaluation.completed` 2), receipts 6, active slots 0.

**MOD-0192 `:199`/`:200` — measured and ticked in the pack:** direct `POST` with correlation C → 201 echoing C, exactly one
audit, one outbox and one receipt row carry C; malformed correlation → 400 `INVALID_CORRELATION_ID`, zero writes; own scope
200, foreign tenant (own ACTIVE LE) → 404, same tenant other ACTIVE LE → 404, no read permission → 403.

## Tests

- SupplyChain suite **550/551** (+10 new), before and after the executor change; the one failure is the recorded
  `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` ("restart mode required"), unchanged.
- `DemandConsumerCompositionTests` (9) measure the production gate; **sabotage**: removing the production check turns
  `Production_never_composes_on_a_fixture…` red, restored green.
- `CompositionRootGuardTests` now builds the **composed** production container (seeded): every controller incl.
  `CapacityPlansController` constructible, no silently-null Diten dependency; its stale `KnownUncomposed` Q273 entry is
  removed; a new fact proves the container hands over the **seeded** readers, not the bounded ones.

## Re-pin

`Program.cs` → `00a8d1fc593a59e4f68e8aaa0b166519543f94497aef69d567dfe123904545b8`; MOD-0190 `:261` and MOD-0192 `:273`
re-pinned under the opened §22 gate (previous values kept in the cell).

## Runtime / not covered

- To compose in Development/Staging set `DemandFixtures__Enabled=true` plus TenantId/LegalEntityId (an ACTIVE LE in that
  environment's MDM), PlanId/Version/Checksum. Production: never (honest 404 until DEMAND v2, G3).
- Not covered: the S&OP/Capacity **UI** (G6) and their manifests (item C); permissions seeding (G7); independent VER (G8);
  a real demand source (G3) and SUPPLY-CONSTRAINTS (G2).
