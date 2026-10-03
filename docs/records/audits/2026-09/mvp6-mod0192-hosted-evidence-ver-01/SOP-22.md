# MVP6-MOD0192-HOSTED-EVIDENCE-VER-01 — SOP §22

## Verdict

**PASS — independently verified for the exact authorized MOD-0192 hosted-evidence scope.** The writer package is internally sealed, the approved 379-path MOD-0190 baseline plus 43-path Capacity overlay is exact with zero overlap, the authorized `Program.cs` patch reconstructs its exact target, and the hosted X07 evidence is consistent with the raw HTTP, MongoDB, process, test, and log records. X01 remains closed without product rework.

This verdict does not approve the unpublished duplicate-name successor, canonical/guard publication, gateway changes, a live optimizer/producer, publisher delivery, E5/G5, rollout, pack promotion, or git operations.

## Authority and immutable inputs

The verifier consumed the exact decision in the writer package. Recalculation verified every entry in the writer `SHA256SUMS` and all nine external entries in `INPUT-MANIFEST.tsv`. No writer file was changed.

Independent source calculation found:

| Check | Result |
|---|---|
| MOD-0190 baseline | 379 entries, all exact |
| Capacity overlay | 43 entries, all exact |
| overlap | 0 paths |
| combined set | 422 paths |
| `Program.cs` baseline | `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5` |
| Program patch | `78cc0fa61dd37019f525c621c572d1dc2ef518eb0b4faf520a280f01909db82e` |
| reconstructed target | `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0` |
| authorized test-only target | `2c133ac1b6b431db074b0054b0337bbc3957fce5869c7aca9291454a53681c43` |

The Program patch was applied independently to `Program.cs` extracted from the sealed MOD-0190 archive; it exited 0 and reproduced the exact target. The authorized X07 test delta adds count/scope assertions for the five reads and adds no physical Mongo `_id` equality acceptance condition.

## Build and independent executable check

The verifier used `/private/tmp/mvp6-mod0192-hosted-evidence-ver-01-20260923-a`, port 57492, replica set `rsmod192ver`, database `DitenSupplyChain_Mod0192_Ver01`, and a separate data path. The only verifier-local source adjustment changed the Capacity test harness port, replica-set name, and database name; its exact diff is retained in `raw/verifier-harness-isolation.patch`. Product sources and `Program.cs` remained unchanged.

A fresh API build passed with 0 warnings and 0 errors. Its separately built API DLL is `f494174e83bafed8ea4191dac4a930331afa7e73b7ad7f2ddd12b41430c9c077`; it is an independent build output and was not substituted into the writer's process chain. The writer's five native processes remain bound by the sealed binary manifest and process transcript to API DLL `f43af77c5c7ffeb5ea438daac4801b61cdf89e0e39f6d5f9dfb40576e3e8ee8f`.

The verifier reran eight focused executable checks:

- five separate X07 reads: broad event skip 0, exact event skip 1, broad audit skip 2, exact audit skip 3, active-slot count skip 4;
- X01 definite precommit failure, unresolved unknown commit, and committed acknowledgement-loss recovery.

Result: **8/8 PASS**. Every X07 case recorded exactly one failpoint entry and one injected failure. Each X01 case recorded one intended commit fault and retained the expected classification/recovery behavior.

The first verifier testhost launch aborted before tests because the sandbox denied its local coordination socket. The authorized rerun outside that restriction is recorded separately. Neither run is combined. Likewise, the writer's initial missing-.NET-8 launch executed zero tests and remains separate from its later 36/36 roll-forward run.

## Independent raw-evidence findings

The verifier parsed the sealed raw files directly rather than accepting `SUMMARY.json` as an oracle:

- The writer roll-forward TRX has 36 executed, 36 passed, 0 failed. Its five later-read rows each show one entry and one injection at the intended boundary.
- The 21 HTTP records yield 200×3, 201×8, 202×3, 401×1, 403×1, 404×3, and 409×1. Missing authentication produced 401; insufficient permission produced 403. Foreign tenant and foreign legal entity produced scoped 404s. Same-key replay returned the original resource and changed payload returned `IDEMPOTENCY_KEY_REUSED`.
- The finite fixture completed with resource `line-4`, period `2027-W03`, required `520.000`, available `480.000`, shortfall `40.000`, and `HOUR`. This remains fixture-oracle evidence, not a live optimizer claim.
- Response-loss evidence records transport closure before read, a durable receipt before retry, and recovery of the same CapacityPlan identifier.
- All five launched processes bind to the same writer API DLL. The killed main process exits `-9`; restart and race processes exit 0.
- After restart while the lease remained valid, the evaluation stayed Running at attempt/fence 1. After expiry it completed at attempt/fence 2. Kill/restart final effects are one terminal event, one scoped terminal audit, and zero active slots.
- In the stale-worker race, worker B completed with the newer fence and worker A logged lease loss. Final effects are again one terminal event, one scoped terminal audit, and zero active slots. This proves fenced recovery; it does not assert exactly-once execution.
- Every process log records the missing outbox transport. The final snapshot has 10 Capacity outbox rows still Pending; no publisher delivery is claimed.

## Disposition and boundaries

- X01: **CLOSED/PRESERVED**. No contradictory evidence and no product rework.
- X07 hosted/process evidence: **PASS for this exact artifact and authority**.
- Duplicate-name successor: **HELD/BLOCKED as a separate contract/release gate**. The existing reconciliation identifies `3.0.0-rc.1` only as the working base; final bytes, consumer consent, cutover, independent release VER, publication, and production uptake remain unresolved.
- Product defect: none found in this scope.
- No production source, `Program.cs`, MOD-0190 source, canonical/guard, gateway, pack, or git state was changed by VER.

## Cleanup and evidence

The verifier MongoDB shut down with process exit 0, and port 57492 had no remaining listener. The disposable verifier tree is retained only as a reproducibility workspace. `raw/verification-results.json` contains the independent machine-readable checks; `raw/ver-x01-x07-rerun.trx` and `raw/rerun-summary.json` contain the successful focused rerun. `EXACT-MANIFEST.tsv` and `SHA256SUMS` seal this verifier package.

Verifier state: **COMPLETE**.
