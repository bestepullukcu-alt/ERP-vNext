# Owned and forbidden paths — Loads producer uptake (MOD-0185, decision C option A)

🤖 Applying knowledge of @backend-architect.

All paths are relative to the isolated environment root (ISOLATED-ENV.md). The writer changes only the files below; any other file needed is a STOP.

## Owned (write)

| # | Path | Action |
|---|---|---|
| 1 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Loads/LoadReadResult.cs` | new |
| 2 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Loads/ILoadRepository.cs` | change (QueryAsync signature only) |
| 3 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Loads/LoadRootMaterializer.cs` | new |
| 4 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Loads/LoadRepository.cs` | change (QueryAsync only) |
| 5 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Loads/LoadModels.cs` | change (LoadSummary record only) |
| 6 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Loads/Handlers/QueryHandlers/GetLoadListHandler.cs` | change |
| 7 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Loads/LoadRootStorageTests.cs` | new |
| 8 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Loads/LoadRootQueryTests.cs` | new |
| 9 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Loads/LoadContractTests.cs` | change (one assertion block) |
| 10 | `services/Diten.SupplyChainService/tests/loads/verify_evidence.py` | change (contract pins) |
| 11 | `services/Diten.SupplyChainService/tests/loads/runtime_probe.py` | change (list-field assertion) |

Preimages today (common checkout = BC-SOURCE bytes, 42/42 Loads files identical): `LoadModels.cs` `edb0b15e…`, `GetLoadListHandler.cs` `9b2f43a8…`, `LoadRepository.cs` `1f50f47a…`, `ILoadRepository.cs` `6a7cce0e…`, `LoadContractTests.cs` `01bf7087…`, `verify_evidence.py` `bb1a8c9f…`, `runtime_probe.py` `e5ee0aed…`. The writer re-checks them in the isolated env and stops on mismatch.

## Read-only references (do not edit)

`LoadPlan.cs` (`f852baee…`), `LoadsController.cs` (`a75f1571…`), `ShipmentDetailMaterializer.cs` (`832bc229…`) and `ShipmentRootStorageTests.cs` (`e6203237…`) as the pattern, the other Loads tests (run only).

## Forbidden

`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` and any `.csproj`/appsettings; `gateway/**`; other services; `docs/analysis/contracts/**` (published 3.1.0 is input only); packs and DCPs; `frontend/**`; Shipment, Carrier, Returns, Claims, S&OP, Capacity feature files; `LoadRepository.MutateAsync` and every transition/create path; `.antigravity/**`; existing records and ledgers; Git state (no add/commit/push/stash).

## Shared-seam overlays (SR-D4)

**None required.** No DI registration (the materializer is static, like the Shipment one), no route, no permission, no navigation, no L10n and no Gateway change. If the writer finds one is needed, it stops and reports; it does not create an overlay on its own.
