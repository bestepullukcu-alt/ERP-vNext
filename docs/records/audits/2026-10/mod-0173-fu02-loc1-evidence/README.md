# WP-0173-LOC1 evidence — MOD-0173-FU02 Location master (LOCATION v1 reads + v1.1 owner API, ledger link), 2026-10-01

Base `01118cf0e`. Commits: `2c48726b4` (contract v1.1), `2e8ff898e` (service + tests + runtime probe), then the
docs / evidence commit. Raw outputs; no token, password or signing secret is written here (probe bearer masked as
`Bearer ***(masked)`, Prism logs JWT-masked; a scan for `eyJ…` and the probe secret pattern finds nothing).

| File | What it shows |
|---|---|
| `d1-build-api.log`, `d1-build-tests.log`, `d1-build-probe.log` | `--no-incremental` builds of the Api, the test project and the runtime probe: 0 warnings / 0 errors each |
| `d2-inventory-tests.trx`, `d2-summary.txt` | InventoryService tests **353 / 353, 0 skipped** (295 before LOC1 + 2 ledger guards + 6 location route guards + 50 location API tests); AC-16 race line of that run |
| `d3-architecture-tests.log` | `tests/architecture` (TenantArchitecture.ArchitectureTests) **18 / 18** |
| `d4-sabotage-run.js`, `d4-sabotage.log` | 8 sabotage proofs on production code — K-01 level rule, K-02 case-insensitive code key, K-03 LE filter of every location query, K-05 stock check on deactivate / archive, K-06 parent-Active check, K-07 stock check on move, K-11 history write, K-13 in-transaction status check of the ledger: each test RED when broken (with the failure line), GREEN after `git checkout`, blob identical; final `git diff --stat` empty |
| `d5-contract-check.js`, `d5-contract-diff.txt` | `git diff 01118cf0e -- location.openapi.yaml`: 274 insertions, the only 2 removed lines are `info.version` 1.0.0 and the old `x-owner`; the 4 v1 operation blocks and the v1 components (`LocationType`, `LocationRef`, `Error`, `UnknownLocation`, `securitySchemes`) SHA-256-identical |
| `d6-processes-before.json`, `d6-processes-after.json` | listeners before / after: the user's Gateway 5000, Auth 5056, Platform 5057, MDM 5059 on the SAME PIDs; 5066 / 4010 / 4011 free before and after |
| `d6-runtime/` | runtime probe on a REAL 5066 process (Mock environment, Mongo `rs0` database `diten_inventory_mock`, Prism serves PRODUCT-MASTER only): `r-checks-flows.json` **65 / 65**, `r-checks-after-restart.json` **10 / 10**, `r-process.json` (two PIDs, same binary SHA-256), per-flow request / response / Mongo JSON |

## D6 — the location flow (`d6-runtime/r-loc1-location-flow.json`, `r-loc1-restart.json`)

1. Tree through the owner API: Site → Warehouse → StorageLocation → Bin A (4 × 201), Zone, bulk dry run (4, nothing
   written), bulk real run (A-01-01-01 … A-01-02-02); `listLocations` = 9 nodes.
2. `OPENING_BALANCE` 10 into Bin A → 201.
3. Deactivate Bin A → 422 `LOCATION_HAS_STOCK`.
4. Deactivate the empty bulk bin A-01-02-02 → 200 Inactive.
5. Receipt into that Inactive bin → 422 `UNKNOWN_LOCATION`, counts unchanged.
6. Bin A code → `BIN-loc1-RENAMED` → 200, same `locationId`, `ETag W/"2"`.
7. `/api/inventory/balance?locationId=<bin A>` onHand 10.000, `/transactions?locationId=` total 1, stock presence true.
8. New process (restart): 9 nodes, path intact, Bin A code + version kept, bin Inactive, history (Bin A
   `Updated, Created`; bin `StatusChanged, BulkCreated`), ledger row of Bin A still 10.000.

Every tenant of the earlier S1 / S2 flows also builds its own tree through the owner API (no hard-coded location id,
no Prism LOCATION mock); all their checks pass unchanged.

## Notes

1. **`headCommit` in `r-process.json` reads `01118cf0e`**: the probe ran on the working tree before the commits; the
   service code was committed afterwards unchanged as `2e8ff898e` (no source file changed after the probe — the D4
   sabotages restore byte-identical blobs). The binary SHA-256 identifies what ran.
2. **AC-16 race** (`LocationLedgerTests.AC16_…`, 30 rounds, release barrier + 0/4/8/12 ms jitter on the deactivation):
   both orders occur and no Inactive bin ever holds stock. The driver's transaction re-runs (Mongo WriteConflict) are
   counted by `TransactionRetryCounter`. Measured: run in `d2-summary.txt` = posting won 23, deactivation won 7,
   posting re-runs 7, location re-runs 24; an earlier run = 22 / 8 / 6 / 21; without jitter = 0 / 30 / 30 / 0. The
   numbers vary run to run; the invariant does not.
3. **Python verifiers** (none required by LOC1): not applicable; no Python on the machine.
4. **Not in LOC1** (pack slice plan): Gateway route `/api/locations/**` (INT-2), screens + manifest pages / actions +
   7-language resources (LOC2), AC-14 / AC-18…21.
