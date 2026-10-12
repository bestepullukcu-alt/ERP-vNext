# Q213 — MOD-0192 Capacity Planning: reachability and shared seams

Static proof only (E1). Nothing was built, run or started. Paths are under
`services/Diten.SupplyChainService/src/Diten.SupplyChainService.` unless written in full.
The pack lists all of this as open (pack line 279). It is recorded here for Q209, not as a defect.

## Answer

**No Capacity endpoint is usable through the real service today: 0 of 6.** The routes are mapped; nothing behind
them is registered.

## Layer by layer

| # | What a real request needs | In the tree? | Evidence |
|---|---|---|---|
| 1 | Gateway route | **No** | `gateway/Diten.ApiGateway/ocelot.json`: 0 × `capacity-plans`, 0 × `supply-chain` |
| 2 | Controller with six actions | Yes | `Api/Features/CapacityPlans/CapacityPlansController.cs:11-31` |
| 3 | Routes mapped | Yes, by framework rule | `Api/Program.cs:71` `MapControllers()`. Not executed |
| 4 | `CapacityRequestContext`, `ICapacityRepository`, `ICapacityLeaseStore`, `CapacitySchema` | **No** | Defined in `Persistence/Features/CapacityPlans/CapacityPersistenceRegistration.cs:8-15`; `Api/Program.cs:46-47` calls only Carrier and Load. `grep AddCapacityPersistence` outside the feature = 0 |
| 5 | `IDemandFixtureReader`, `IConstraintFixtureReader` | **No — and not in `AddCapacityPersistence()` either** | `CapacityRepository` needs both (`CapacityRepository.cs:6`). The registration method registers neither (`CapacityPersistenceRegistration.cs:10-13`) |
| 6 | The evaluation executor | **No — and not in `AddCapacityPersistence()` either** | see "The executor" below |
| 7 | Capacity middleware (scope, correlation, idempotency key) | **No** | `Api/Program.cs:66-67` wire only Carrier and Load. Without it `CapacityRequestContext.Scope` stays all-zero (`CapacityPlanModels.cs:34`) and the repository throws (`CapacityScope.cs:6-7`) |
| 8 | Model-error adapter for the Capacity family | **No** | `Api/Program.cs:57-60` handle only Load and Carrier |
| 9 | Permission keys granted to a role | **No** | `supplychain.capacity-plans.*` exists only in `Infrastructure/Features/CapacityPlans/CapacityPermissions.cs` |

The Shipment branch at `Program.cs:68` does take a Capacity request, but `ShipmentContextMiddleware.cs:9` passes
every path outside `/api/shipment-bundle` straight through. For `/api/supply-chain/capacity-plans` it is a no-op.

What a direct request on 5061 would meet (reasoned from the code, **not run**): without a token, 401 from
`[Authorize]`; with a token lacking the permission claim, a bare 403 from `CapacityPermissionAttribute.cs:10-12`
(no contract error body, because the middleware that writes it is not wired); with the permission, the controller
cannot be constructed because `CapacityRequestContext` is not registered. Runtime behaviour is unconfirmed.

## The executor — flag for Q209

`Infrastructure/Features/CapacityPlans/CapacityEvaluationExecutor.cs:5` is a `BackgroundService` (a hosted
service) with a 10-second timer (`:12-13`). It is the only thing that moves an evaluation from Accepted to
Completed or Failed.

- It is registered **nowhere**. `AddCapacityPersistence()` registers one hosted service, `CapacitySchema`
  (`CapacityPersistenceRegistration.cs:13`), and not the executor. `grep AddHostedService` in the feature = that one line.
- So calling `AddCapacityPersistence()` alone gives a service that accepts evaluations (202) and never finishes
  them: every scenario stays blocked by its active slot (`CapacityRepository.cs:159-160`).
- Q209 needs, besides `AddCapacityPersistence()`: `AddHostedService<CapacityEvaluationExecutor>()`, and the two
  fixture readers (row 5).
- The prompt quotes the pack as saying a feature-local executor "alone is not startup composition". That sentence
  was not found in the pack or in `mod-0192-executor-exact-decisions-01/` by grep (insufficient evidence for the
  quote). The substance holds from the code, as shown above; the pack does say the approved composition includes
  the executor (pack line 437-438).

Two more things Q209 should know about the executor:

1. **`RenewAsync` has no production caller.** The pack describes "10s scan/renewal" (line 255). The lease store
   implements renewal (`CapacityLeaseStore.cs:52-64`) and a test covers it (`CapacityLeaseTests.cs:12`), but the
   executor never calls it (`grep RenewAsync` in source = the interface and the implementation only). With the
   instant fixture oracle it does not matter; with any real computation longer than 30 s it would.
2. **Discovery is cross-tenant.** `FindPendingScopesAsync` (`CapacityLeaseStore.cs:23-29`) reads without a tenant
   filter. See `BOUNDARY-REFS.tsv` B-11.

## A naming hazard at the seam

MOD-0190 and MOD-0192 each define their own `IDemandFixtureReader` and their own `DemandFixtureReader`
(`Domain/Features/CapacityPlans/CapacityScope.cs:10` and `Domain/Features/SandopPlans/ISandopRepository.cs:3`;
`Infrastructure/Features/CapacityPlans/DemandFixtureReader.cs:3` and
`Infrastructure/Features/SandopPlans/DemandFixtureReader.cs:5`). They share nothing, which is correct. A
`Program.cs` that imports both namespaces must qualify the names, and must register each class against its own
interface. `BOUNDARY-REFS.tsv` B-03.

## What the 48 tests exercise

Measured, as the prompt asked:

- `grep -c "WebApplicationFactory|TestServer|CapacityContextMiddleware|CapacityPlansController|Handler(|Validator(|CapacityPermissionAttribute|DefaultHttpContext"`
  over the ten files in `tests/…/CapacityPlans/` = **0 in every file**.
- The Mongo tests build `CapacityRepository` and `CapacityLeaseStore` by hand on a real replica set
  (e.g. `CapacityAtomicityTests.cs:15-22`).
- The executor is tested once, against a fake lease store (`CapacityLifecycleTests.cs:8-37`).
- The contract tests serialise one error body and one projection and deserialise one request
  (`CapacityContractTests.cs:9-35`).

Never executed by any test in this tree: the controller, the middleware, all six handlers, all three validators,
the permission attribute, `AddCapacityPersistence()`, the executor's timer loop, real JWT.

Capacity is one step further from HTTP than Claims: Claims at least has component tests of its middleware;
Capacity has none.

## Shared seams needed to make the module real (named, not patched)

1. `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` —
   `AddCapacityPersistence()`; the two fixture readers; `AddHostedService<CapacityEvaluationExecutor>()`;
   the Capacity middleware branch; the Capacity model-error adapter. Accepted composition: sha256 `50c48a2b…`
   (pack line 273); the tree has `7fdb5ef0…`.
2. `gateway/Diten.ApiGateway/ocelot.json` — the six routes of pack lines 432-435, with OPTIONS, `/capacity-plansXYZ`
   not matched, plus the gateway route-count test. integration-agent only.
3. Shared permission definition / seed — four keys (pack lines 178-184).
4. Later, with the UI: `SharedResource.{lang}.resx` navigation keys in 7 languages, the manifest provider
   registration, the field icon map (pack lines 436-438, 545, 564).
5. Not a seam but a prerequisite for any live use: a real DEMAND and constraint source. Today the module works for
   one hard-coded fixture tenant only (`BOUNDARY-REFS.tsv` B-12).
