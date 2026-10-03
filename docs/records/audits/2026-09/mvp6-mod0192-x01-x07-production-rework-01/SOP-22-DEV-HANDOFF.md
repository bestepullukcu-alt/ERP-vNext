# MVP6-MOD0192-X01-X07-PRODUCTION-REWORK-01 — SOP §22 DEV handoff

**Developer verdict: PASS for the authorized isolated X01/X07 persistence rework; independent VER and CT acceptance pending.** No duplicate-name production behavior, composed HTTP, publisher, or shared composition is included.

## Authority and baseline

- User decision of 2026-09-22 explicitly authorizes production edits only to `CapacityRepository.cs`, `CapacityLeaseStore.cs`, and approved CapacityPlans tests. X01 patch `7ac18cdd58fb2441a4c9bea0847f1e79b51893137e81d0263f1f0ebc55da25ce`; X07 starting patch `94f65a697acf533f4f1b3ea49e5d3c18e962426686873ec19961162f1ae50730`; X07 exact-identity addendum is authorized as a separate hash. This report does not treat the separately authorized duplicate-name contract *candidate* as production authority.
- Checkout: `/private/tmp/mvp6-mod0192-core-dev-01`, branch `codex/mod-0192-core-dev-01`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Initial and final `git status --porcelain --untracked-files=all` each had 243 entries. Existing dirty inputs were preserved; no reset, clean, stash, commit or push.
- Promoted pack SHA-256 `d01bf7a050472a9708a989d7bd4d13ab0a41a81984a272bbb15848f93fcac6e0`. Published YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex `eb1df1383e637c744179abe4cccd19738aadf41896a9311bda`. `Program.cs` remains `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8`.
- Historical `FAULT-SOURCE-43.tar.gz` SHA-256 `092a0ecd4fc149d97e71c51d637cef83afe90c778b16a4e5b5808bafe060222a` provided the 43-path baseline. All three modified paths matched its expected baseline hashes before application. The final [SOURCE-MANIFEST.tsv](SOURCE-MANIFEST.tsv) (SHA-256 `36114191e1576b8b5654b9910f3347432d3bf36aac87339a4280cc85a852f8`) records 43/43 current bytes; exactly three entries changed.

## Exact patch chain and changed source

1. X01 proposal `7ac18c…` applied with `git apply --check` PASS. Known server-rejected `MongoCommandException` lacking `UnknownTransactionCommitResult` maps to `503 DEPENDENCY_UNAVAILABLE` after scoped receipt recovery; uncertain outcomes remain `COMMIT_RESULT_UNRESOLVED`. The existing test oracle was updated.
2. X07 starting candidate `94f65a…` applied with `git apply --check` PASS. It required exact-identity completion. [X07-ADDENDUM.patch](X07-ADDENDUM.patch), SHA-256 `3741f2f6332ef214456a26b915ca597790e45c254f9b316ea85dd72727ea058a`, adds exact Pending terminal event `OccurredAt`, original `CausationId`, `ContractVersion`, and payload comparison; exact audit `OccurredAt`; single scoped event/audit counts; and active-slot release. Reconciliation read exceptions fail closed. The addendum was applied in a disposable baseline after the two starting patches and produced byte-identical target files.

| Approved path | Baseline SHA-256 | Final SHA-256 |
|---|---|---|
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/CapacityPlans/CapacityRepository.cs` | `74153ee83d4f20c7b464558467025d5a58d7e594607c2b5e76a577d9f9c2d616` | `6bf026d3bfaeddf37ff4e461efedfa977a07765a413d5f363fcbdc80edfe0364` |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/CapacityPlans/CapacityLeaseStore.cs` | `2912a032deb172909cf7ae8f0730f0c412b0f05239c32aba0b3cf06794517b78` | `d6f932b5fcfcbadbb44b65ccac0cbccc1f1fb0e8c576b333c186a735a524902a` |
| `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityAtomicityTests.cs` | `576bf4001c0573f9884012c9aaaffeb57ab4e658b4eddc3be16de25a7acd98b8` | `91999330faf2a50f5ff2cda3595506ecded50d5b87ffb7bf393dd655f71e51db` |

## RED → GREEN and regression

All Mongo tests used this lane's `127.0.0.1:57192`, replica set `rsmod192`, fixed `DitenSupplyChain_Mod0192_Test` DB, `enableTestCommands=true`; readiness was PRIMARY. Operational 27017 was not used. The tests activate actual `failCommand` failpoints and assert activation counts.

| Check | Result | Raw evidence |
|---|---|---|
| X01 old post-commit-attempt classifier, code-8 definite rejection | RED, 1 expected failure / 1 run; returned unresolved instead of dependency unavailable | `RAW-EVIDENCE.tar.gz` → `x01-red.log`, `x01-red.trx` |
| X07 starting candidate, event causation mutation | RED, 1 expected failure / 1 run; incorrectly reconciled the wrong causation as success | archive → `x07-red.log`, `x07-red.trx` |
| Final X01/X07 fault tests | GREEN 20/20, including definite failure, unknown result, ack loss, 13 exact event/audit/slot corruption mutants and reconciliation read failure | archive → `x01x07-expanded.trx` |
| Entire CapacityPlans test filter | GREEN 31/31, 0 skipped, 3m03s | archive → `capacity-plans-regression.log`, `.trx`; [TEST-RESULTS.tsv](TEST-RESULTS.tsv) |
| Fresh .NET 8 build (`--no-restore --disable-build-servers`) | Exit 0, 0 warnings, 0 errors | archive → `build-verified.log` |

The first .NET 10 testhost attempt aborted because ASP.NET Core 8 was not installed at its default root; the rerun used `DOTNET_ROOT=/Users/natig/.dotnet` with .NET 8. An intermediate 18-case run had one **test-fixture** duplicate-key failure: the production unique terminal-effect index correctly rejected a synthetic duplicate before the reconciliation query. The fixture now explicitly removes that index only in that corruption case; it does not alter production schema. A redundant non-escalated final build stalled and was stopped; the subsequent captured .NET 8 build passed. These attempts are not counted as green evidence.

The 13 mutation cases invoke the private reconciliation routine against real persisted Mongo documents; they are test-level fault evidence, not composed HTTP or a production-process fault endpoint. Real commit-path failpoint cases separately cover no-commit and committed acknowledgement-loss. Independent VER must reproduce source→build→binary→Mongo outcomes and inspect this separation.

## Provenance and limits

- Final persistence DLL SHA-256 `549a6b14d3e4c88255784a4e78d1c2ce43f345bff0c8520bedec16d94fd55add`; test DLL `3148d9ed0e54c85a5209f8ae8a5e7dd8f0758f5881ecba3d38d81d93c04ded42`. The 31-case TRX came from these current source inputs; no older binary fallback was used.
- [RAW-EVIDENCE.tar.gz](RAW-EVIDENCE.tar.gz) SHA-256 `990aa99e896a02445ddad6cfc542c049e24e6f5dcac70ed3635864e94ef43f1c` contains raw TRX, logs and Mongo readiness. [TEST-RESULTS.tsv](TEST-RESULTS.tsv) SHA-256 `96d66d2d4ff292eb98ff300197ca8b12c9010cc352bab481d915e6c2b801203f` names each executed test and result. X01/X07 RED results are intentional and remain separate from GREEN counts.
- This DEV result closes the implementation work in its authorized scope only. Duplicate-name wire candidate/publication and production behavior remain separate; composed HTTP/JWT, hosted executor restart, independent VER, CT acceptance, E5/G5 and rollout remain open. No new contract/schema, Program.cs, shared permission, gateway, publisher, or other module file was changed.
- Lane-owned mongod PID 38714 was stopped with SIGTERM; port 57192 has no listener. **Source writer complete.**
