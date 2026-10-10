# Q205 — MOD-0183 evidence statement

## Statement

**Yes, with one limit on what it covers.** MOD-0183 now has executable evidence at level E2: every MOD-0183 test
in the SupplyChain test project ran against an isolated replica set and passed.

The limit: the run was on the **working tree** (`feature/mvp6-logistics` @ `4a8d4d4b3` plus 565 uncommitted
entries), not on a tree composed from BASE-STACK v2. It is evidence for the code as it is on disk today.

## What ran

Suite: `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests`, full run, 139 tests, 0 skipped.
Mongo: `127.0.0.1:31994`, replica set `rsq205s1`, mongod 8.0.18.

| MOD-0183 test class | Tests | Needs Mongo | Without variables | On the replica set |
|---|---:|---|---|---|
| `ShipmentTests` | 12 | yes (`MOD0183_TEST_MONGO`) | 12 failed (guard) | **12 passed** |
| `SourceIntakeTests` | 15 | yes (`MOD0183_TEST_MONGO`) | 15 failed (guard) | **15 passed** |
| `ShipmentValidatorContractTests` | 32 | no | 32 passed | 32 passed |
| `ShipmentRootStorageTests` | 6 | no | 6 passed | 6 passed |
| `ShipmentRootHttpTests` | 1 | no | 1 passed | 1 passed |
| `SourceClientTests` | 7 | no | 7 passed | 7 passed |
| **MOD-0183 total** | **73** | | 46 passed · 27 failed | **73 passed · 0 failed** |

Assigning these six classes to MOD-0183 is by class name and by the variable they read; the module pack was not
consulted for a test list.

The one surviving failure in the suite is in `Loads` (MOD-0185), not MOD-0183 (`GENUINE-DEFECTS.tsv`).

## The two dependent acceptance criteria

| Pack | Line | Criterion (unchecked) |
|---|---|---|
| `MOD-0186-reverse-logistics.md` | 137 | "MOD-0183 dependency has executable verified evidence." |
| `MOD-0187-claims-management.md` | 138 | "MOD-0183 executable verification is available." |

This record supplies the **executable** part. Whether it counts as **verified** is CT's decision:

- it is one lane's run; no independent VER has re-run it;
- it is on the working tree, not on a stacked tree;
- the environment does not survive a restart (`PROVISIONING-STEPS.md` §6), though it is reproducible in seconds.

This WP did not tick either box and did not edit either pack.
