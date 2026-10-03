# MVP6-MOD0190-TEST-ORACLE-INDEPENDENT-VER-01 — SOP §22

**Independent verdict: PASS for the bounded test-only oracle rework.** The previously failing Sandop committed-acknowledgement test and the full 19-test Sandop core suite pass on a fresh independent build. This is neither MOD-0190 CT acceptance nor a new HTTP/operational verification.

## Exact input and change boundary

- Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. DEV [handoff](../mvp6-mod0190-test-oracle-rework-01/SOP-22-DEV-HANDOFF.md) recorded writer-complete. Source archive SHA-256 `8fa00d40814a81472ec4221ad909321d058dea18a11f64dd2ac16ea800ecb745`; 379-path manifest SHA-256 `ae7ef59e0158113c22389ccd3eec84207418f3a859c40480e70b42ee888a9bef`; exact patch SHA-256 `5b812bf421e6a83e06c388b555820472c36bb1a96136394d78df5d76354952b3`. All 379 extracted entries matched the manifest before and after testing.
- Compared against the previous independent [HTTP VER source manifest](../mvp6-mod0190-http-dev-02/source-manifest.tsv): **only** `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/SandopPlans/SandopAtomicityTests.cs` changed, SHA-256 `55d2522b959c34016075f60b6c9bc91da66250eae2d6241e2ee70e333e6c0eb4` → `eba2da6f47a4935b61857a95534b3d1697635910190e3838494d96d956af7f43`. The patch applied cleanly to the previous exact test file and yielded the new target byte-for-byte. The other **378/379** paths, including all production code, were hash-identical. `Program.cs` stayed `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5`. Published SANDOP YAML/annex were unchanged.

## Independent execution

A new disposable source copy at `/private/tmp/mvp6-mod0190-test-oracle-ver-01/source` was created from `git archive HEAD` plus the exact new 379-path source archive. Only cached NuGet restore metadata was reused; no DEV build output was copied. Fresh command:

```text
/Users/natig/.dotnet/dotnet build services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false
```

Exit 0, zero errors, five offline `NU1900` warnings. Independently compiled test DLL SHA-256 `ba07bd1a88d2ba702168c3d943e02d3c9aa08f72ee68fddf4337669568fc675c`; API DLL SHA-256 `b2743c0e5c1a502407a68ee5927b670ac829664f8d9caeb4ed589882a25f16ce`. Different DLL hashes from prior runs are not treated as drift; source parity, fresh compile and the executed test DLL form this run's provenance chain.

A lane-owned `rs190oraclever` MongoDB on `127.0.0.1:57882` reached PRIMARY with `enableTestCommands=true`; operational port 27017 was untouched. Both commands used `DOTNET_ROOT=/Users/natig/.dotnet` and `MVP6_MOD0190_MONGO_URI=mongodb://127.0.0.1:57882/?replicaSet=rs190oraclever`, with `--no-build --no-restore` and the freshly compiled test DLL:

| Filter | Result | Raw record |
|---|---|---|
| `FullyQualifiedName~SandopPlans` | Exit 0, **19/19 PASS**, 0 skipped, 19 s | `sandop-19.trx` and `.log` in [raw archive](raw-evidence.tar.gz) |
| `FullyQualifiedName~Committed_write_concern_uncertainty_resolves_original_receipt` | Exit 0, **1/1 PASS** | `commit-ack-isolated.trx` and `.log` in the archive |

Mongo log independently shows `failCommand` activation for `commitTransaction` with the injected `writeConcernError` and the counter advancing on the isolated run. Thus PASS was not obtained by skipping the failpoint.

## Oracle review and evidence limit

The published [SANDOP annex](../../../../analysis/contracts/sandop-capacity-semantics-v2.0.0.md:15) says a POST uncertain commit must first resolve the scoped receipt: if found, return the stored original result; only an unresolved result returns 503 `COMMIT_RESULT_UNRESOLVED`. The new assertions in `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/SandopPlans/SandopAtomicityTests.cs:73–85` implement those two allowed outcomes. They require a real failpoint hit; reject every status other than 201/503; require the exact 503 code on that branch; require a persisted original receipt and original correlation; require exact receipt body for 201; require a subsequent 201 replay with that same body; and require one scoped plan/receipt/audit/outbox effect set. Consequently wrong 201 body, wrong 503 code, absent receipt, duplicate durable effects and failpoint misses still fail. **This run did not separately force both conditional branches**, so this report proves the executed branch and reviews the other branch's assertions, not two independently executed branch outcomes.

The previous [22-record independent HTTP/DB archive](../mvp6-mod0190-http-independent-ver-01/raw-evidence.tar.gz) remains **content-bound historical evidence** for the unchanged production source. HTTP, JWT, concurrency, rollback and restart were not rerun for this test-only patch. The prior broad 271/276 and separate Loads failure remain recorded non-PASS outside this test-only rework; no waiver is granted. No live DEMAND/Workflow, publisher, gateway, operational rollout, E5/G5 or full-module conclusion is made.

## No-change and cleanup

The repository source was not edited by this verifier. Before the owned audit report was created, start/end `git status --porcelain=v1` snapshots were byte-identical; all 379 candidate source hashes still matched. The only durable output is this owned report, checksum file and raw archive. Own Mongo PID `47604` was stopped; port 57882 had no listener afterward. No commit, push or stash.
