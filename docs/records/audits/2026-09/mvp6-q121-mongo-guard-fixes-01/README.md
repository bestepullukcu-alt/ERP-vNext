# Q121a — Mongo test-DB guard fixes (2 Platform tests) — DRAFT overlay, archive only

WP-MVP6-FIX-121a · Prompt Q121a v1 · Lane: Cowork LANE 2 (Linux VM, no dotnet) · @testing-agent (+ code-quality-agent review) ·
Authority: owner decision ~12:40 (Mongo test-DB guard = A: fix the 2 Platform tests); development in LANEs (owner ~13:46).
Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, `GIT_OPTIONAL_LOCKS=0`, read-only. Start 2026-09-27T10:48:08Z (13:48 +03:00).
**Status: DRAFT — static checks only. Nothing was built or tested here; build/test proof is Q121b on the Mac.**

## What the guard requires (tests/architecture/TenantArchitecture.ArchitectureTests/MongoTestDatabaseGuardTests.cs, sha256 `55dfb24b…7cae`, HEAD blob f351520, unchanged)

- It scans every `*.cs` whose path contains `/tests/` or `.Tests/` (excluding obj/bin/node_modules and itself), after `WithoutComments` (comments stripped, string literals kept).
- `NoTestCreatesItsOwnDatabasePerRun` fails on any file, not in `KnownPerRunDatabase` (13 entries, may only shrink), that matches either pattern:
  - `PerRunDatabaseName` `\b(?:databaseName|dbName|DatabaseName|_databaseName|_dbName)\b[^;]{0,400}?Guid\.NewGuid`
  - `PerRunDatabaseInline` `GetDatabase\s*\([^;]{0,400}?Guid\.NewGuid`
- `NoTestBuildsThePlatformSchema` fails on any `EnsureIndexesAsync` outside `KnownTestSideIndexBuild` (2 entries).
- The two `…ExceptionListStaysHonest` facts fail if a listed file no longer matches (stale licence).
- `TheScanActuallySeesTheTestTree` requires more than 200 scanned files.
- Rule behind it, `.antigravity/rules/mongo-indexing.md` DB-010:
  - no database per run;
  - isolation is by `TenantId`;
  - a test that is not tenant-bound (an idempotent seed, a database-wide rule) takes its own database with a **fixed suffix, not a GUID**;
  - adding a line to the exception list to turn a red test green is forbidden.

## Inputs (read-only)

| Input | sha256 |
|---|---|
| `…/Audit/PpmAuditRetentionPolicySeedMongoTests.cs` (preimage) | `c22f0cdab58c2b229fa529c1cdb466d876a99a68abb1cae40952a8f1d4acbc46` = BASE-MANIFEST = HEAD blob 1b70c0e, unmodified in the working tree |
| `…/Persistence/DisposableStandaloneMongo.cs` (preimage) | `0fb06a1d020ada8deaaaf0d702fe2007ac89d2104e585d1f2bca3868c9cb00ef` = BASE-MANIFEST = HEAD blob 6e37158, unmodified in the working tree |
| `mvp6-q103-accepted-base-01/BASE-MANIFEST.tsv` (BASE a8a236de…) | both rows match; guard file row `55dfb24b…` matches |
| `mvp6-q117-test-guard-fixes-01/OVERLAY-MANIFEST.tsv` | 11 paths, none under `services/Diten.Platform/` → Q117 does not touch either file |
| `mvp6-q119-ver-q117-01/REPORT.md` V9 + `RAW-LOGS.tar.gz:test/build-fix--architecture.trx` | 18 total / 17 passed / 1 failed; the failure message lists exactly these 2 files |

## Changed files (2; no other file, no test-config file needed)

| Path | Preimage sha256 (bytes) | Postimage sha256 (bytes) |
|---|---|---|
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/PpmAuditRetentionPolicySeedMongoTests.cs` | `c22f0cdab58c2b229fa529c1cdb466d876a99a68abb1cae40952a8f1d4acbc46` (9040) | `ea6fa3623810de341bb5b4fe2e3de9bf62a6cbce5557aa0dd623ba243238aa76` (9304) |
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Persistence/DisposableStandaloneMongo.cs` | `0fb06a1d020ada8deaaaf0d702fe2007ac89d2104e585d1f2bca3868c9cb00ef` (4256) | `4aaec42c9919961b08005eb9cd142e5dd7b82d651a5bf65c78acbce2f987fc48` (4508) |

No test-config file was changed: the guard reads source text only.

### The changes

- **Ppm:**
  - Adds `private const string SeedDatabaseName = "diten_platform_audit_seed";`.
  - Line 79 becomes `_client.GetDatabase(SeedDatabaseName)`.
  - The class still starts its own mongod on a fresh port with a temporary dbpath, seeds the same baseline, runs the same single `[Fact]` with the same assertions, and drops the database and kills the process in `DisposeAsync`.
- **DisposableStandaloneMongo:**
  - Adds `private const string StandaloneDatabaseName = "diten_platform_standalone";`.
  - `CreateDatabase()` now returns that fixed database.
  - `DisposeAsync` drops exactly that name. Before, it dropped every name with the prefix `diten_platform_standalone_`, which the fixed name would no longer match.
  - Its only consumers — `StandaloneMongoTransactionFailClosedTests` and the standalone fact in `SubscriptionSessionAndStandaloneFailureTests` — call `StartAsync()` and then `CreateDatabase()` once per instance, so each still gets a fresh, empty database on its own mongod.
  - BASE adds one Platform test file not in the working tree (`CarrierAuthLegalEntitySuccessorTests.cs`); it uses neither helper nor a database.
- The per-class temporary **directory** names keep their GUID. They are dbpath folders of disposable mongod processes, not database names, and neither the guard nor DB-010 covers them.

## Static checks (this lane)

| Check | Result |
|---|---|
| tree-sitter 0.26.0 + tree-sitter-c-sharp 0.23.5 parse, pre and post | 4/4 files, `has_error = False`, 0 ERROR/MISSING nodes |
| Brace / parenthesis / bracket balance | 0 / 0 / 0 in all 4 files |
| Guard replay (Python port of `WithoutComments` + the 3 regexes + both exception lists, repo test tree, 785 files) on the preimages | `NoTestCreatesItsOwnDatabasePerRun` offenders = exactly the 2 files (same as Q119 V9, positive control); the other 4 facts green |
| Same replay with the 2 postimages | 0 offenders; `NoTestBuildsThePlatformSchema` 0; both exception lists not stale (13 / 2 unchanged); scan 785 > 200 |
| Pattern vs new code, line by line | Ppm L83 `GetDatabase(SeedDatabaseName)` and DSM L29 `GetDatabase(StandaloneDatabaseName)`: no `Guid.NewGuid` before the next `;` → PerRunDatabaseInline 0; no `databaseName/dbName/DatabaseName/_databaseName/_dbName` token followed by `Guid.NewGuid` before `;` → PerRunDatabaseName 0; the remaining `Guid.NewGuid` (Ppm L52, DSM L37) feed temporary directory paths only |
| Not an evasion | the database names are constants; no GUID reaches `GetDatabase` through any variable |
| `git apply --check` + `git apply` of FIXES.patch on the preimages (scratch git in /tmp) | clean; result byte-equal to the postimages (2/2); `patch -p1 --dry-run` OK |
| Archive ↔ postimages | extracted 2/2 byte-equal; members are exactly the 2 repo paths, mode 0644, owner 0/0 |
| No product-code or guard change | only 2 files under `services/Diten.Platform/tests/`; guard file and exception lists untouched (sha256 `55dfb24b…` unchanged) |
| Grep for other users of the old names | none outside the 2 files |

## Q121b — build/test plan (Mac, isolated tree; expected results)

1. **Tree:** `~/mvp6-env/q121b/` = BASE a8a236de (Q103) + the Q117 overlay `83e6322c…` + this archive (verify its sha256 first). Keep this order.
2. **Build:**
   - `dotnet build tests/architecture/TenantArchitecture.ArchitectureTests`
   - `dotnet build services/Diten.Platform` (the test project)
   - Expected: 0 errors, warnings equal to BASE+Q117.
3. **Architecture:** `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests`. Expected **18/18** (was 17/18 in Q117/Q119); `NoTestCreatesItsOwnDatabasePerRun` passes and both `…ExceptionListStaysHonest` facts stay green.
4. **Platform, measured before and after on the same machine:**
   - Run `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests` (lane mongod, never 27017) on BASE+Q117, then on BASE+Q117+Q121.
   - Expected: identical totals, passed, failed and skipped. No Platform baseline exists in the Q103, Q117 or Q119 records, so the "before" run is the baseline.
   - The three touched facts (`PpmAuditRetentionPolicySeedMongoTests.Inserts_only_missing_portfolio_delivery_policy_and_is_idempotent`, `StandaloneMongoTransactionFailClosedTests.StandaloneMongo_ReturnsTyped503_WithAllParticipantResidueZero`, `SubscriptionSessionAndStandaloneFailureTests.StandaloneMongo_ProductionSubscriptionWriter_IsTyped503AndAllParticipantsZero`) keep the same outcome. They need a local mongod (`/opt/homebrew/bin/mongod` or `DITEN_TEST_MONGOD`).
5. **SupplyChain:** expected **407/408**, unchanged. The one failure is `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, excluded by design. This fix does not touch SupplyChain; the run proves there are no side effects.
6. **Residue:** after the runs, no `diten_platform_audit_seed` / `diten_platform_standalone` database or temporary folder remains, and no lane mongod is left running.

## ASSUMPTIONs

- **A1:** Linux lane per the dispatch (no dotnet). Nothing here claims build or test.
- **A2:** "BASE version" = HEAD. Both files are byte-identical in HEAD, the working tree and BASE-MANIFEST.
- **A3:** DB-010's fixed-suffix clause is the "guarded disposable test-database pattern". Tenant-scoped isolation does not apply, because both tests are database-wide: a seed, and a standalone transaction fail-closed check.
- **A4:** `rm -rf` ran only on this lane's own scratch sub-folders under `/tmp/q121a/` (patch-apply copies). Nothing in the repo was removed.
- **A5:** The guard replay is a faithful port but not the C# test itself. Q121b is the proof.

## Files

`q121-mongo-guard-fixes-overlay.tar.gz` (sha256 `93bf1c07249c43853870cd74150a1f0813bff1d74a8661259f8e14112258ea90`), `FIXES.patch`
(sha256 `5dd2945434c6c0cd1ecac1eadf79d8ec4592f18582836689981126a4adfaa6f9`), this README, `SHA256SUMS`.

Agent PASS ≠ CT ACCEPTED — returning to CT.
