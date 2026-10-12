# MOD0186 prospective DEV allowlist v1.0 — HELD

No source write grant. All paths below are proposed new owned files; if any exists at dispatch, inspect ownership/drift before writing. No folder wildcard.

- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Returns/ReturnOrder.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Returns/ReturnStatus.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Returns/ReturnScope.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Returns/ReturnLifecycle.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Returns/ReturnAuditEntry.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Returns/ReturnMutationResult.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Returns/IReturnRepository.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Returns/ReturnLine.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Returns/ReturnEntitlement.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/Commands/CreateReturnCommand.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/Commands/TransitionReturnCommand.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/Queries/GetReturnListQuery.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/Handlers/CommandHandlers/CreateReturnHandler.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/Handlers/CommandHandlers/TransitionReturnHandler.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/Handlers/QueryHandlers/GetReturnListHandler.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/Validators/CreateReturnValidator.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/Validators/TransitionReturnValidator.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/Validators/GetReturnListValidator.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/ReturnModels.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/ReturnRequestContext.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/ReturnRequestFingerprint.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/IReturnReferenceReader.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Returns/ReturnSourceSnapshot.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Returns/ReturnsController.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Returns/ReturnContextMiddleware.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Returns/ReturnContractError.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Returns/ReturnRepository.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Returns/ReturnSchema.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Returns/ReturnPersistenceRegistration.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Returns/ReturnOutboxStore.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Returns/IReturnCommitProbe.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Returns/NoOpReturnCommitProbe.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Returns/ReturnReferenceReader.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Returns/ReturnPermissions.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Returns/ReturnPermissionAttribute.cs`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Returns/ReturnOutboxWorker.cs`
- `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnContractTests.cs`
- `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnLifecycleTests.cs`
- `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnIsolationTests.cs`
- `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnReplayTests.cs`
- `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnConcurrencyTests.cs`
- `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnAtomicityTests.cs`
- `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnReferenceTests.cs`
- `services/Diten.SupplyChainService/tests/returns/runtime_probe.py`
- `services/Diten.SupplyChainService/tests/returns/restart_probe.py`
- `services/Diten.SupplyChainService/tests/returns/verify_evidence.py`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Returns/ReturnQuantity.cs`

Separate shared single-writer approval ONLY: `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` for pack§29 composition. Protected: every other source/test/config, all contracts/annexes, other packs, .antigravity, gateway, registries/DCP/board, shared permissions, all historical evidence. No csproj/config modification assumed. Future DEV evidence must use a newly CT-assigned temp root; PREP evidence stays immutable.
