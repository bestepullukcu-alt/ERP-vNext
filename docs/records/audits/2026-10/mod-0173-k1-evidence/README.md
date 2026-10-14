# WP-0173-K1 evidence — permission namespace `inventory.*` + FU-S2-01, 2026-10-01

Base `c6306d34`. Raw outputs; no token or JWT secret is written (secrets are minted per run and passed only through
process environment variables).

| File | What it shows |
|---|---|
| `d1-build.log` | `dotnet build Diten.InventoryService.sln -c Debug --no-incremental` → 0 warnings, 0 errors |
| `d2-tests.log` | full suite: 278/278 passed, 0 skipped (S2 had 262; +16 = 3 namespace tests, 10 test-DB-name cases, 3 startup-gate tests) |
| `d3-grep.log` | `grep -rn "supplychain\.inventory"` over the service + API reference → 0 matches; where the new names occur; every `[HasPermission]` uses a constant |
| `d4-sabotage.log` | `InventoryPermissions.ManageReservations` set back to the legacy name → 5 of 21 permission tests red (new-name control 201→403, reservation access tests); restored → 21/21 green, file SHA-256 identical, diff empty |
| `d5-gate-probe.cs` | the D5 probe (run from a scratch console project referencing MongoDB.Driver 2.27.0; not built in the repo) |
| `d5a-service-testdb.log` | REAL service on test DB `diten_inventory_s1_tests` with an unscoped row: `warn:` line "skipped 6 group(s) … never guessed, never deleted", then "Now listening" (6 = 5 S1 residue + the probe's marker, which was removed afterwards by `_id`) |
| `d5b-service-nontestdb.log` | REAL service on temporary NON-test DB `diten_inventory_k1_gate_probe` with one unscoped row: `warn:` skipped 1, `crit:` "startup gate … refusing to start", process exits non-zero, never listens |
| `d5-gate-probe.json` | summary of (a) and (b): exit codes, matched lines, unscoped row still present after the refusal (nothing deleted), temp DB dropped (it did not exist before the probe) |
| `d5-d6-processes-after.log` | no listener on 5066 / 4010 / 4011 / 5091; 0 remaining Prism / service / probe processes |
| `d6-runtime-probe/` | `tests/runtime-probe/run-runtime-probe.ps1` with the new names: flows 48/48, after-restart 6/6 |
