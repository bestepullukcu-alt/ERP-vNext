# MVP6-MOD0187-R22-R25-EVIDENCE-02 — SOP §22

## Verdict

**WRITER COMPLETE; R22 PASS; R25 PARTIAL.** Yetkilendirilen evidence-only patch yalnız disposable AUTH-05 kopyasına uygulandı. Altı named write-stage ve committed Mongo unknown-result/restart replay zinciri geçti. Not-committed unknown-result ilk process restart sonrasında yeniden 503 verdi; server transaction lifetime sonrasında aynı key 201 ile tek durable write group oluşturdu. Bu gecikmeli iyileşme, ilk restart başarısızlığını PASS'e çevirmez.

## Authority and exact source binding

Owner, 2026-09-21 18:14:22Z kararında `AUTHORITY-REQUEST.md` içindeki exact persistence baseline/target bağını ve patch `d0844d8c8e4b28dc3ab1f1559b6f49c45005567f6f1ea252fca6c51f84f75eca` değerini yalnız izole evidence çalışması için onayladı. Karar deployment/rollout, Program.cs, Returns, canonical/guard, commit/push veya E5/G5 yetkisi vermedi.

| Item | SHA-256 |
|---|---|
| Baseline `NoOpClaimCommitProbe.cs` | `4082bb189256e6bffbec4440913f4fd2544ed79ce88252324cd86007d069a222` |
| Baseline `ClaimPersistenceRegistration.cs` | `0361088e461cfac00ecf3b34b39ee311a9e58f0b82d51327b82f44ae196752de` |
| Authorized / reconstructed patch | `d0844d8c8e4b28dc3ab1f1559b6f49c45005567f6f1ea252fca6c51f84f75eca` |
| Target `NoOpClaimCommitProbe.cs` | `05cb87877bf427e3232fd2a074915a26e4256d9a5c61e357c4d7ab2b8ceef68b` |
| Target `ClaimPersistenceRegistration.cs` | `c4ca2690bb964529cb7d1d2a1265c514c10946f792829d4da821597636761a9c` |

Reconstructed `raw/applied.patch` is byte-identical to the authorized candidate. The non-generated source comparison contains exactly these two changed files. Disposable Program.cs remained `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`.

## Build and process chain

Fresh `dotnet build --no-restore --no-incremental -m:1 /nr:false` succeeded with 0 warnings and 0 errors. The process binary hashes are:

- API: `cf7194de59aab991dd1885a15d287f33f738a9f90a4f48318c9312a0c24f2042`
- Persistence: `5cac5ae8d8d45ba29603c93d317ccd95d4c6988ea752c1c3a24fb33c013ffdc1`
- Application: `37b4dfdd2d2a7756cc86d61c60e8a7c9b0e750c221264b29dfa9d5e2cc547ee5`
- Domain: `2bac1106c0415466990983d91b9c1ed7cbe05d12ca4dccfaf1c8a708eace5083`

All API processes used `127.0.0.1:5066`. MongoDB used isolated port `27894`, replica set `claims_r22r25_e02`, and fixed DB `diten_claims_r22r25_e02`. Scenario isolation used tenant/legal-entity scope rather than per-test databases. The final DB snapshot records the indexes and `isWritablePrimary=true`. API and Mongo processes were stopped after capture.

## Normal mode and evidence mode

Production mode was started with deliberately invalid evidence probe values. Claims list returned 200, proving normal composition ignored those values and retained `NoOpClaimCommitProbe` behavior.

Exact `ClaimsEvidence` with missing config, malformed fixed UUID, or unsupported stage returned 500 on the first Claims resolution. This is fail-closed request-path behavior. The singleton is lazy: `/health` becomes ready before the first Claims resolution, so the candidate does not fail process startup. Logs expose `InvalidOperationException` type but the shared exception pipeline suppresses the exact exception text. This distinction is retained as an observation rather than rewritten as startup rejection.

## R22

A different `_id` was seeded with the exact fixed-ID-derived `ClaimNumber`, in the same tenant/LE scope. The create transaction returned `503 CLAIM_STORAGE_UNAVAILABLE`. Counts remained `claims=1, receipts=0, audit=0, outbox=0`, and the sole claim remained the seed document. This isolates the `claim_number` unique collision from `_id_` collision.

## R25

Each named stage ran in a distinct `ClaimsEvidence` API process. `aggregate`, `receipt`, `audit`, `outbox`, and `beforeCommit` returned 503 with independently queried `0/0/0/0`. `afterCommit` returned 201 with `idempotentReplay=true` and `1/1/1/1`; it is classified only as an injected post-commit exception, not as Mongo unknown commit.

The external Mongo `commitTransaction` failpoint produced two measured branches:

1. **Committed, acknowledgement unknown:** first HTTP 503 while DB was `1/1/1/1`; a new API process and the same key returned 201 receipt replay with counts unchanged.
2. **Not committed:** first HTTP 503 and DB `0/0/0/0`; the immediate new-process same-key retry also returned 503 and remained `0/0/0/0`. After the server transaction lifetime cleared, another process returned 201 non-replay and exactly `1/1/1/1`.

The earlier same-fixed-ID cross-scope attempt is retained as discarded harness evidence. Its `_id_` collision was caused by the probe setup and is not a product verdict.

## Boundaries

No Program.cs, reference-reader, Returns, canonical, guard, operational DB, rollout, commit, push, or stash action was performed. Results are evidence-mode process observations; they are not normal-production acceptance, E5/G5, or DEV GO.

## Evidence map

- `row-results.md`: requirement-level verdicts.
- `normal-vs-evidence.md`: composition and rejection behavior.
- `raw/process-boundary.json`: R22, stage results, and retained first unknown-run setup.
- `raw/unknown-commit-rerun.json`: committed unknown-result PASS and immediate not-committed restart failure.
- `raw/unknown-not-committed-251.json`: second immediate restart failure with a server-aborted code.
- `raw/unknown-not-committed-eventual.json`: eventual single-group recovery.
- `raw/logs/`: build, API process, Mongo, runner, launch/exit transcripts.
- `raw/raw-db/final-db.json`: final DB/index/replica observation.
- `raw/manifests/`: source diff, source hashes, binaries, Program.cs boundary, and repo status snapshot.

