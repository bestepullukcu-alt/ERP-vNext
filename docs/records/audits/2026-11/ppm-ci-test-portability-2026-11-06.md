# PPM CI test portability — incomplete delivery, 2026-11-06

## Authority and revision

Owner authorized a fresh PPM test branch from current main, test-only portability fixes, depth-1 verification,
two CI matrix rows and matching exclusion removal, and local commits. No production edits, push or PR.
Base: `1fc896e33cc3012399468b3262e0f42082ef1584`.
Branch: `codex/ppm-ci-test-portability`.
Code/CI checkpoint: `9b8822249` (`test(ppm): prepare owned Mongo portability and CI enrollment`).
The old Portfolio dirty worktree and ES branch were not changed.

**Not ready to push or merge.** The CI enrollment is prepared locally, but both complete suites remain red
under shallow checkout. No test expectation, skip, hash or historical evidence was weakened to make them green.

## Completed changes

- Both disposable Mongo fixtures resolve `DITEN_TEST_MONGOD` first, then the existing macOS fallback locations.
  A nonblank explicit path that does not exist fails closed instead of silently selecting another binary.
- Six resolver regression cases cover precedence, invalid explicit paths, fallback order and missing binaries.
- Existing test-owned loopback listeners, port exclusions, database ownership and cleanup remain unchanged.
- Added `ppm-tests` and `ppm-itest` matrix rows; removed only their two `NotInThisJob` entries.
  Workflow checkout depth was not expanded to hide history dependence.
- Independent review found no production edits, weakened assertions, new skips or secret/artifact additions.
  Its delivery blocker (unresolved Git history dependencies) remains open.

## Actual test results

All runs used .NET 8 Debug on macOS arm64. These are not Linux CI results.

| Run | Passed / total | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Unit, original repository with local history | 381 / 381 | 0 | 0 |
| Integration after Mongo correction, original repository | 87 / 97 | 10 | 0 |
| Unit, independent depth-1 clone | 376 / 381 | 5 | 0 |
| Integration, independent depth-1 clone | 76 / 97 | 21 | 0 |
| CiServiceTestsGuardTests, scoped architecture run | 51 / 51 | 0 | 0 |

The six new resolver cases passed in both integration runs. No run was aborted or timed out.
The full architecture suite was not run; 51/51 refers only to the changed CI guard's test class.
The machine was heavily loaded (8 CPUs; load average measured up to 72.88); the deterministic failures
recurred in both original and shallow repositories. Test projects were run sequentially.

Commands (each test command also used `--logger trx`, `--results-directory /private/tmp/ppm-ci-tests-20261106`
and `--blame-hang-timeout 3m`):

```sh
dotnet test services/Diten.PpmService/tests/Diten.PpmService.Tests/Diten.PpmService.Tests.csproj -c Debug
DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod dotnet test services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Diten.PpmService.IntegrationTests.csproj -c Debug
git clone --depth 1 --single-branch --branch codex/ppm-ci-test-portability file:///Users/alitufanoglu/.codex/worktrees/ppm-ci-test-portability/ERP-vNext /private/tmp/ppm-ci-depth1.Cfk4D6/repo
# In that clone, both complete projects were run with DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod.
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj -c Debug --filter FullyQualifiedName~CiServiceTestsGuardTests
```

The clean clone had `--is-shallow-repository=true`, `rev-list --all --count=1`, no alternates,
and only the new test branch and its origin tracking ref. Historical producer commit objects were unavailable.
No existing local PPM producer branches were copied. It is a diagnostic clone of checkpoint `9b8822249`, not a
claim of completed portability. Mongo ran only in test-owned instances; there was no use of a shared database
on port 27017. After execution no matching test-owned Mongo or PPM API process remained.

Local raw TRX evidence (not committed, contains no credential export):

| File in `/private/tmp/ppm-ci-tests-20261106` | SHA-256 |
| --- | --- |
| ppm-unit-baseline.trx | b8c4e681f3192c76165160265178a51b0e0947e936e146e833d4e4cce6c6b304 |
| ppm-itest-local.trx | 7611624e0760842fa950652005129b457d96925ccc5790282ccd5db1226b259b |
| ppm-unit-depth1.trx | cce69b04a4ff043128df97810b2e61099908d28da0525a99b25c5eb9421931e9 |
| ppm-itest-depth1.trx | 210b4b1fe85c5fa61e7ab3bea37451748a650a0e3f32a4ebd28cba24c1aa5bf6 |
| ppm-ci-guard.trx | c620ef4877819ed18a3c41b0d39df2d7785eb18ab0af9f8c2d4168041aaebe66 |

## Blocker 1: current producer source is absent

History consumers remain unchanged in these four classes:

- Unit: `GateICMutationEvidenceTests`, `FundingScenarioArchitectureAndFixtureTests`.
- Integration: `DecisionTraceCheckpointEvidenceTests`, `Mod0072ImmutableFixtureTests`.

The audit fixture directories for MOD-0136, MOD-0138, MOD-0007 and MOD-0072 contain no current files in this
main-based checkout. Their historical closures are respectively `711962a3fdc1226d947672dc9b48d29296c960a0`,
`3df680d6e006bfce19e382253ddd1f2f873c2295`, `9968ecede48822f95a74461a4959c94b23abbc9b`, and
`b4589139e8c9db544de5b66300640b214db3acf4`.
Historical fixture directory counts are 11, 13, 9 and 11; these counts are inventory, not test counts.

The current tree also lacks these producer sources used by the tests:

- `services/Diten.FpaService/src/Diten.FpaService.Domain/Modules/Budgeting/Selections/BudgetingSelectionContracts.cs`
  at `1949b93ead3dc1ac3234673bbe00ed67e3615743`.
- `services/Diten.FpaService/src/Diten.FpaService.Domain/Modules/ScenarioPlanning/ScenarioPlanningCore.cs`
  at `acae87090f35e5e0a7f37ad66dd8e98fc69c07bb`.
- `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Domain/DecisionRegistry/DecisionRegistryModel.cs`
  and `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/DecisionRegistry/DecisionRegistryApplicationContracts.cs`
  at `2d354a97bfbe09ed665e44dba8665181d2a56d78`.

The historical combined PPM mutation sources are retrievable locally and match their manifest hashes, but
their current replacements are PPM consumer code, not those external producer contracts.
The MOD-0023 pack exists currently but differs from the historical approval checkpoint.

**Requested producer-contract sabotage was not performed.** There is no current producer source to change
under the authorized scope. Copying historical source into fixtures and checking only that copy would not
prove current-producer drift detection. No historical-only substitute or consumer-as-producer assertion was added.
Required input: an approved current producer source/baseline and its repository placement, or an explicit
owner revision of the acceptance requirement. Importing producer production code is not authorized here.

The shallow failures attributable to Git history are five unit tests and eleven integration tests.

## Blocker 2: nine real audit publishing failures

`PpmAuditIntentSubmittedV1` defines the production event name `ppm.audit-intent.submitted.v1`.
`PpmAuditIntentDispatcher` publishes that actual type via the real outbox event bus. The shared
`Diten.BuildingBlocks.Eventing/EventName.cs` regular expression rejects hyphens inside segments.
The tests use the production name; replacing it only in test data or bypassing the validator would mask the defect.
Nine integration cases fail at this production boundary in both runs. No production change was made.
Resolution needs an explicitly approved producer/consumer event-contract decision, not a blind global regex relaxation.

## Blocker 3: one authenticated API smoke failure

`GateILocalApiSmokeTests.Health_and_all_fourteen_authenticated_Gate_I_routes_prove_default_off_503_and_zero_residue`
expects 503 from the protected default-off route but receives `401 invalid_token`.
The test token's issuer, audience, key and validity configuration match the tested API configuration on inspection.
The built API dependency graph mixes IdentityModel JWT components 7.1.2 with Tokens 8.16.0; this is a candidate
cause, **not a proven diagnosis**. JWT validation was not disabled, assertions were not relaxed, and no production
dependency was changed. The failure recurred in both repositories.

## Changed files and next handoff

1. `.github/workflows/service-tests.yml`
2. `tests/architecture/TenantArchitecture.ArchitectureTests/CiServiceTestsGuardTests.cs`
3. `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/PpmDisposableMongo.cs`
4. `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/GateI/DecisionTrace/GateIDisposableMongoReplicaSet.cs`
5. `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/PpmTestMongoExecutable.cs` (new)
6. `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/PpmTestMongoExecutableTests.cs` (new)
7. This report (new).

HCM PR #393 was observed merged at `8bac00577ecfb5b42406d1c1e2a1e303721dbbd3` after this task's base.
No PR is being opened now. Before any future push/PR, fetch and merge current main as authorized and preserve
HCM's matrix/exclusion edits alongside PPM's; stop on conflicts outside those two lists.
Local checkpoints are not CI acceptance. Push and PR remain pending explicit owner authorization.
