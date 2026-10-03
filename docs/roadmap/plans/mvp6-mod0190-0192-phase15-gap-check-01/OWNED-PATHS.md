# Prospective exact owned paths — Phase 1.5 mapping, no DEV authorization

The two draft packs authorize only **future** feature-local `Features/SandopPlans/**` and `Features/CapacityPlans/**` roots after owner promotion. These names are a concrete Phase 1.5 plan using the existing SupplyChain service five-layer convention; they are not present files or a runtime allowlist grant. A later DEV prompt must re-pin the exact source baseline and amend this manifest if source registration requires different files. The two sets below have no path intersection.

## MOD-0190 — SandopPlans

Base: `services/Diten.SupplyChainService/`.

| Layer | Prospective owned files |
|---|---|
| Api | `src/Diten.SupplyChainService.Api/Features/SandopPlans/SandopPlansController.cs`; `.../SandopContextMiddleware.cs`; `.../SandopContractError.cs` |
| Application commands | `src/Diten.SupplyChainService.Application/Features/SandopPlans/Commands/CreateSandopPlanCommand.cs`; `.../CaptureSandopSnapshotCommand.cs`; `.../RecordSandopSignOffCommand.cs` |
| Application queries | `src/Diten.SupplyChainService.Application/Features/SandopPlans/Queries/GetSandopPlanQuery.cs`; `.../ListSandopSnapshotsQuery.cs`; `.../ListSandopSignOffsQuery.cs` |
| Command handlers | `src/Diten.SupplyChainService.Application/Features/SandopPlans/Handlers/CommandHandlers/CreateSandopPlanHandler.cs`; `.../CaptureSandopSnapshotHandler.cs`; `.../RecordSandopSignOffHandler.cs` |
| Query handlers | `src/Diten.SupplyChainService.Application/Features/SandopPlans/Handlers/QueryHandlers/GetSandopPlanHandler.cs`; `.../ListSandopSnapshotsHandler.cs`; `.../ListSandopSignOffsHandler.cs` |
| Application validation/models | `src/Diten.SupplyChainService.Application/Features/SandopPlans/Validators/CreateSandopPlanValidator.cs`; `.../CaptureSandopSnapshotValidator.cs`; `.../RecordSandopSignOffValidator.cs`; `src/Diten.SupplyChainService.Application/Features/SandopPlans/SandopPlanModels.cs`; `.../SandopRequestFingerprint.cs`; `.../SandopProjection.cs` |
| Domain | `src/Diten.SupplyChainService.Domain/Features/SandopPlans/SandopPlan.cs`; `.../SandopSnapshot.cs`; `.../SandopSignOff.cs`; `.../SandopScope.cs`; `.../ISandopRepository.cs` |
| Persistence | `src/Diten.SupplyChainService.Persistence/Features/SandopPlans/SandopRepository.cs`; `.../SandopSchema.cs`; `.../SandopPersistenceRegistration.cs` |
| Infrastructure | `src/Diten.SupplyChainService.Infrastructure/Features/SandopPlans/SandopPermissionAttribute.cs`; `.../SandopPermissions.cs`; `.../DemandFixtureReader.cs` |
| Tests | `tests/Diten.SupplyChainService.Tests/SandopPlans/SandopContractTests.cs`; `.../SandopLifecycleTests.cs`; `.../SandopReplayTests.cs`; `.../SandopConcurrencyTests.cs`; `.../SandopAtomicityTests.cs`; `.../SandopIsolationTests.cs` |

## MOD-0192 — CapacityPlans

Base: `services/Diten.SupplyChainService/`.

| Layer | Prospective owned files |
|---|---|
| Api | `src/Diten.SupplyChainService.Api/Features/CapacityPlans/CapacityPlansController.cs`; `.../CapacityContextMiddleware.cs`; `.../CapacityContractError.cs` |
| Application commands | `src/Diten.SupplyChainService.Application/Features/CapacityPlans/Commands/CreateCapacityPlanCommand.cs`; `.../CreateCapacityScenarioCommand.cs`; `.../EvaluateCapacityScenarioCommand.cs` |
| Application queries | `src/Diten.SupplyChainService.Application/Features/CapacityPlans/Queries/GetCapacityPlanQuery.cs`; `.../GetCapacityScenarioQuery.cs`; `.../GetCapacityEvaluationQuery.cs` |
| Command handlers | `src/Diten.SupplyChainService.Application/Features/CapacityPlans/Handlers/CommandHandlers/CreateCapacityPlanHandler.cs`; `.../CreateCapacityScenarioHandler.cs`; `.../EvaluateCapacityScenarioHandler.cs` |
| Query handlers | `src/Diten.SupplyChainService.Application/Features/CapacityPlans/Handlers/QueryHandlers/GetCapacityPlanHandler.cs`; `.../GetCapacityScenarioHandler.cs`; `.../GetCapacityEvaluationHandler.cs` |
| Application validation/models | `src/Diten.SupplyChainService.Application/Features/CapacityPlans/Validators/CreateCapacityPlanValidator.cs`; `.../CreateCapacityScenarioValidator.cs`; `.../EvaluateCapacityScenarioValidator.cs`; `src/Diten.SupplyChainService.Application/Features/CapacityPlans/CapacityPlanModels.cs`; `.../CapacityRequestFingerprint.cs`; `.../CapacityProjection.cs` |
| Domain | `src/Diten.SupplyChainService.Domain/Features/CapacityPlans/CapacityPlan.cs`; `.../CapacityScenario.cs`; `.../CapacityEvaluation.cs`; `.../CapacityScope.cs`; `.../ICapacityRepository.cs` |
| Persistence | `src/Diten.SupplyChainService.Persistence/Features/CapacityPlans/CapacityRepository.cs`; `.../CapacitySchema.cs`; `.../CapacityPersistenceRegistration.cs`; `.../CapacityLeaseStore.cs` |
| Infrastructure | `src/Diten.SupplyChainService.Infrastructure/Features/CapacityPlans/CapacityPermissionAttribute.cs`; `.../CapacityPermissions.cs`; `.../DemandFixtureReader.cs`; `.../ConstraintFixtureReader.cs`; `.../CapacityEvaluationExecutor.cs` |
| Tests | `tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityContractTests.cs`; `.../CapacityLifecycleTests.cs`; `.../CapacityReplayTests.cs`; `.../CapacityConcurrencyTests.cs`; `.../CapacityAtomicityTests.cs`; `.../CapacityIsolationTests.cs`; `.../CapacityLeaseTests.cs`; `.../CapacityRestartTests.cs` |

**Protected/shared, not in either set:** `src/Diten.SupplyChainService.Api/Program.cs`; shared `.csproj`/solution registration; shared permission catalog/seed; gateway `ocelot.json`; canonical SANDOP/DEMAND contracts; `Features/Shipments|Carriers|Loads|Returns|Claims/**`; the peer feature root; `.antigravity/**`. Registration/startup of the 0192 executor cannot be asserted from a feature-local type alone: one integration owner must later supply an exact `Program.cs` baseline/patch/target and permission/gateway disposition. No Event Bus publisher is included in either prospective set.
