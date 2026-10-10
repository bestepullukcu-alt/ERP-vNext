# Independent VER Handoff — MVP6 Loads Uptake Q77b

Status: **HELD / do not start runtime VER as PASS input yet**

The source overlay application is exact, but writer-complete is not achieved because restore/build/test/runtime did not complete in the disposable tree. A verifier may use this handoff only after a fresh writer rerun or continuation completes build, targeted tests and runtime evidence.

## Source Recipe To Re-run

1. Create a fresh disposable directory under `/private/tmp/mvp6-q77b-loads-ver-*`.
2. Extract `git archive 4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
3. Apply BC-SOURCE archive `docs/records/audits/2026-09/mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz`.
4. Verify BC-SOURCE archive hash `ebd5d80ca00541235868b94ed9917a53fb63be6e15aa78d0450fa90f33737064`.
5. Verify Q77a overlay archive hash `e0583eef83beee812ab2b4e6ac7a54dfe9c94e5c7f455a400163ef339cb9fd2d`.
6. Verify Q77a `SOURCE-MANIFEST.tsv` hash `6b7a77fc574341076a48bd63fc2a08859315d61c7a0497de4b789fbf3d5856b1`.
7. Check all Q77a preimages before applying; apply 11 full files and `runtime_probe.py.patch`; check all 12 result hashes after applying.

## Required VER Gates

Do not count this DEV lane as runtime evidence. Re-run independently:

- `dotnet restore` and `dotnet build` for SupplyChain in the disposable tree.
- Targeted Loads tests:
  - `LoadRootStorageTests`
  - `LoadRootQueryTests`
  - `LoadRootTransitionTests`
  - changed `LoadContractTests`
- Existing Loads regression suites named in Q77a `NOT-VERIFIED.md`.
- Full SupplyChain test project, listing pre-existing unrelated failures separately if any.
- Isolated Mongo replica set on a lane port, never operational `27017`.
- API runtime probe using the patched `services/Diten.SupplyChainService/tests/loads/runtime_probe.py`.
- `verify_evidence.py` against the published 3.1.0 YAML and annex pins.
- Restart check that confirms listed `lifecycleCorrelationId` survives process restart.

## Acceptance To Prove

| Row | Required proof |
|---|---|
| LU-01..LU-08 | list emits persisted Load root when present; missing/null/invalid emits explicit `null`; no derivation from GET trace, Load ID, Shipment, session, audit or user input |
| LU-09 | tenant/LE and read permission boundaries preserve disclosure limits |
| LU-10..LU-12 | transition, replay, lifecycle, atomicity, concurrency and restart regressions remain clean |
| LU-13 | verifier pins exact YAML `6dc1dd48...` and annex `9d8a3706...` |
| LU-14 | build and suite results are fresh, not inherited from Q77a static checks |
| F1-01..F1-04 | missing/null/invalid stored root cannot match nil inbound correlation; rejection has no receipt/audit/outbox/entity write |

## Forbidden

Do not modify canonical contracts, gateway, Program.cs, packs, guard rules, frontend, other module source, operational Mongo, git index, commits, pushes or stashes. Do not convert this Q77b OPEN result into PASS without fresh build/test/runtime evidence.

