# Q335 — Suite result and delta against Q266

Run 2026-10-03 18:40:24–18:45:39 +03, branch `feature/mvp6-logistics` @ `4a8d4d4b3`, working tree as copied at
18:39 +03 (scratch copy manifest `evidence/copy-manifest.sha256`, 443 files, sha256 in `ARTIFACTS.sha256`).

## Result

**432 passed / 1 failed / 433.** The one failure is the known restart-mode test
`Claims.ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` (`FAILURES.tsv`). **Genuine regressions: none.**
`dotnet test --list-tests` lists 433 tests; the seven module filters together ran 433, so every listed test ran once.

## Against Q266 (417 / 1 / 418)

+15 tests, all in the Shipments module (75 → 90). Every other module has the same total and the same per-class
counts as Q266 (compared class by class from the two runs' trx files).

| change in Shipments | tests | source of the change |
|---|---|---|
| `ShipmentTrackingPodManifestProviderTests`: 1 test replaced by 3 | +2 | Q288 group 3 (repointed at pack §22); made green by Q300 (provider aligned) |
| `ContractErrorCodeParityTests` (new class) | +3 | Q288 group 1 |
| `ShipmentTelemetryTests` (new class), 7 tests | +7 | Q288 group 2 |
| `ShipmentTelemetryTests`, 3 drift tests | +3 | Q315 |
| **total** | **+15** | |

Removed by name: `ShipmentTrackingPodManifestProviderTests.Declares_exact_three_page_topology_and_existing_permissions`
(Q288 replaced it). Added: 16 test names (listed by comparing the two trx files).

Files that differ between Q266's copy and this one (`services/` part of the two copy manifests): 7 added, 0 removed,
9 changed — all in `Diten.SupplyChainService`:

- added: `Api/Logging/CorrelationIdEnricher.cs`, `Api/appsettings.Development.json`,
  `Api/appsettings.Development.example.json`, `Application/Common/ShipmentTelemetry.cs`, and the tests
  `Shipments/ContractErrorCodeParityTests.cs`, `Shipments/RepositoryFile.cs`, `Shipments/ShipmentTelemetryTests.cs`;
- changed: `Api/Middleware/ShipmentContextMiddleware.cs`, `Api/ModuleRegistration/ShipmentTrackingPodManifestProvider.cs`,
  `Api/Program.cs`, `Application/Behaviors/PerformanceBehavior.cs`, `Application/DependencyInjection.cs`,
  `Application/Features/SourceIntake/WarehouseIntakeCoordinator.cs`, `Persistence/Features/Shipments/ShipmentOutboxStore.cs`,
  `Persistence/Features/Shipments/ShipmentRepository.cs`, and the test `Shipments/ShipmentTrackingPodManifestProviderTests.cs`.

## Figures quoted today without a record, now measured

| quoted by | figure | this run |
|---|---|---|
| Q288 | 427 / 3 / 430 | superseded: its two extra failures were the manifest tests that Q300 later turned green |
| Q300 | Shipments 87 / 87 | superseded: 90 / 90 after Q315's three drift tests |
| Q315 | 432 / 1 / 433, Shipments 90 / 90 | **matches** this run |

## What this run cannot establish

- **Which WP changed which product file.** The changed files above are measured; attributing each to a WP (Q265,
  Q271/Q272, Q275, Q280, Q300, Q315, Q270, Q287) comes from those WPs' own reports, not from this run. Several WPs
  touched the same file (`Program.cs`, `ShipmentRepository.cs`, `WarehouseIntakeCoordinator.cs`).
- **Whether a test that kept its name changed its body.** Only one test file changed under a kept class
  (`ShipmentTrackingPodManifestProviderTests.cs`); other kept tests were not diffed line by line.
- **Frontend.** Q329's UI fixes are under `frontend/` and are not part of this suite (`Diten.SupplyChainService.Tests`
  only). No frontend test project was run.
- **Restart mode.** The excluded Claims test was not run in its two-process mode.
