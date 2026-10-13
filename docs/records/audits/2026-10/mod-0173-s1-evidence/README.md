# WP-0173-S1 evidence — MOD-0173 Slice S1 (Ledger core), 2026-09-30

Audit report: [`../mod-0173-inventory-ledger-s1-audit-2026-09-30.md`](../mod-0173-inventory-ledger-s1-audit-2026-09-30.md).
Raw outputs only; nothing here is edited by hand except the masking of bearer tokens in the Prism logs
(`***(masked-jwt)`, done by the probe script itself).

| File | What it proves | How it was produced |
|---|---|---|
| `d1-build.log` | AC1 — 8 projects (5 layers, tests, runtime probe, referenced Secrets block), 0 errors, 0 warnings | `dotnet build services/Diten.InventoryService/Diten.InventoryService.sln -c Debug --no-incremental` |
| `d2-inventory-tests.trx`, `d2-test-summary.log` | 218/218 passed, 0 skipped (AC1–AC17 test side) | `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests --logger trx` |
| `d3-tenant-architecture-tests.log` | repo-wide guards 18/18 with the new service in their scan | `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` |
| `d4-1-idempotency-sabotaged.log` / `-restored.log` | K3: replay/reuse guard red when removed, green when restored | temporary edit of `PostMovementHandler.Replay` |
| `d4-1b-idempotency-precheck-sabotaged.log` | second layer: unique index still holds without the handler pre-check | temporary edit of the pre-check |
| `d4-2-negative-stock-sabotaged.log` / `-restored.log` | K3: coverage condition | temporary edit of `InventoryLedgerRepository.DecreaseAsync` |
| `d4-3-isolation-sabotaged.log`, `d4-3b-cross-le-sabotaged.log`, `d4-3-isolation-restored.log` | K3: Tenant+LE query filter and `CROSS_LE_FORBIDDEN`; restored log ends with the full suite 218/218 and an empty `git diff` | temporary edits of `BalanceScope` and the scope middleware |
| `runtime-probe/r-process.json` | real process on :5066 (`/health`), stopped and restarted as a new PID from the same binary (timestamp + SHA-256) | `services/Diten.InventoryService/tests/runtime-probe/run-runtime-probe.ps1` |
| `runtime-probe/r-ac2-opening.json` | AC2 request / response / correlation / persisted transaction, balance, audit, outbox | probe `flows` |
| `runtime-probe/r-ac3-ac4-ac5-issue-idempotency-negative.json` | AC3 (SHA-256 of the first transaction before = after), AC4 replay + reuse with unchanged counts, AC5 422 with details | probe `flows` |
| `runtime-probe/r-ac7-ac9-stock-status-base-uom.json` | AC7 byStatus, AC9 5 BOX → 50 EA on the Prism conversion | probe `flows` |
| `runtime-probe/r-ac10-isolation.json` | AC10 tenant B blind, CROSS_LE_FORBIDDEN, tenantId in query → 400 | probe `flows` |
| `runtime-probe/r-restart-read.json` | AC2 balance/availability 100 after the restart | probe `after-restart` |
| `runtime-probe/r-checks-*.json` | every probe expectation with pass/fail (33 + 4) | probe |
| `runtime-probe/service-*.log`, `prism-*.log` | stdout of the service runs and the two Prism mocks | probe script |

Environment: MongoDB 9.0.2 replica set `rs0` on 127.0.0.1:27017 (tests: database `diten_inventory_s1_tests`;
probe: `diten_inventory_mock`), .NET SDK 10.0.401 / runtime 8.0.31, Node 24.19.0, Prism 4.10.5.
