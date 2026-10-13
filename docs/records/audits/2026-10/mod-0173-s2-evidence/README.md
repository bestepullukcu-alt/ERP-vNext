# WP-0173-S2 evidence — MOD-0173 Slice S2 (Reservations) + FU-S1-01/02, 2026-10-01

Audit report: [`../mod-0173-inventory-ledger-s2-audit-2026-10-01.md`](../mod-0173-inventory-ledger-s2-audit-2026-10-01.md).
Raw outputs; nothing edited by hand. Bearer tokens in the Prism logs are masked by the probe script.

| File | What it proves | How it was produced |
|---|---|---|
| `d1-build.log` | 8 projects, 0 errors, 0 warnings | `dotnet build services/Diten.InventoryService/Diten.InventoryService.sln -c Debug --no-incremental` |
| `d2-inventory-tests.trx`, `d2-test-summary.log` | 262/262 passed (218 S1 + 44 S2), 0 skipped | `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests --logger trx` |
| `d3-tenant-architecture-tests.log` | repo-wide guards 18/18 | `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` |
| `d4-a-create-guard-sabotaged.log` / `-restored.log` | K3: reserve guard `AvailableOnHand − Reserved ≥ q` | temporary edit of `ReservationPositionOperations.TryReserveAsync` |
| `d4-b-sku-guard-sabotaged.log` / `-restored.log` | K3: reserved stock cannot be issued to others | temporary edit of `ReservationPositionOperations.ApplyAvailableDeltaAsync` |
| `d4-c-etag-sabotaged.log` / `-restored.log` | K3: ETag (version) check on release; restored log ends with the full suite 262/262 and an empty `git diff` | temporary edit of `ReservationPositionOperations.TryReleaseAsync` |
| `runtime-probe/r-process.json` | real process on :5066, stopped and restarted (new PID) from the same binary (timestamp + SHA-256), HEAD commit | `services/Diten.InventoryService/tests/runtime-probe/run-runtime-probe.ps1` |
| `runtime-probe/r-s2-reservations.json` | reserve → over-reserve 422 → guarded issue 422 → consumption → stale ETag 409 → release 200 → reserve kept → replay; persisted reservations, position, audit, outbox | probe `flows` |
| `runtime-probe/r-s2-fu-s1-01.json` | known SKU without stock → 200 zeros (Prism PRODUCT-MASTER) | probe `flows` |
| `runtime-probe/r-restart-read.json` | after restart: reserved 25 / available 55 / onHand 80; rebuilt position 80 / 25; S1 reads unchanged | probe `after-restart` |
| `runtime-probe/r-ac*.json` | S1 flows re-run against the S2 binary (AC10 now 200 zeros per FU-S1-01) | probe `flows` |
| `runtime-probe/r-checks-*.json` | every probe expectation with pass/fail (48 + 6) | probe |
