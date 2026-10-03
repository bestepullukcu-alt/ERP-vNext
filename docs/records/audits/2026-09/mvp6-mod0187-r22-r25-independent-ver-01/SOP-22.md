# MVP6-MOD0187-R22-R25-INDEPENDENT-VER-01 — SOP §22

**Independent verdict:** R22 **PASS**; R25 **PASS for the bounded, authorized E4 evidence-mode acceptance**. This is not a normal-production, rollout, E5/G5, or full-module verdict. No product rework is warranted by the first post-restart 503 observed here.

**Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. The common checkout was already dirty; this lane did not modify product, pack, contract, Program.cs, guard, git state, or the normal integration baseline. Independent work ran in `/private/tmp/mvp6-mod0187-r22-r25-independent-ver-01-bp1jJM/source`, using API port 51925 and isolated Mongo replica set `claims_r22r25_indver` on port 27925. Both listeners were closed at handoff.

## Authority, source and binary binding

The real user message immediately preceding `MVP6-MOD0187-R14-APPLY-01` authorized exact patch `d0844d8c8e4b28dc3ab1f1559b6f49c45005567f6f1ea252fca6c51f84f75eca` only for isolated Claims evidence, with `ClaimsEvidence` bounded configuration and normal `NoOpClaimCommitProbe`. The writer's authority request alone was not treated as approval. All 54 writer-package `SHA256SUMS` entries verified, and `evidence.tar.gz` listed successfully.

The fresh normal-versus-evidence source manifest compared 14,556 non-generated files on each side. Exactly two differ:

| File | Normal SHA256 | Evidence SHA256 |
|---|---|---|
| `NoOpClaimCommitProbe.cs` | `4082bb189256e6bffbec4440913f4fd2544ed79ce88252324cd86007d069a222` | `05cb87877bf427e3232fd2a074915a26e4256d9a5c61e357c4d7ab2b8ceef68b` |
| `ClaimPersistenceRegistration.cs` | `0361088e461cfac00ecf3b34b39ee311a9e58f0b82d51327b82f44ae196752de` | `c4ca2690bb964529cb7d1d2a1265c514c10946f792829d4da821597636761a9c` |

The patch passed `git apply --check`, applied to copied normal baseline files, and reproduced both target hashes. Snapshot `Program.cs` remained `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`. Independent `dotnet build --no-restore --no-incremental` passed with 0 warnings/errors. The fresh API binary SHA256 is `513150e9bcfa87d408c8074c992144634a88e73272e56afc7ab31f2a220636df`; the fresh Persistence binary is `9b8d57bca6fde68446b0019bbf1a5162870da1c007edb219768f8a9ccadf7a2f`. This is a fresh compile using copied restore assets, not a fresh restore claim.

## R22 and six stages

Independent signed HTTP plus scoped Mongo queries reproduced a real `claim_number` unique collision: a different `_id` held the fixed-ID-derived number in the same tenant and LE. Create returned `503 CLAIM_STORAGE_UNAVAILABLE`; the seed remained the sole aggregate and counts stayed `1/0/0/0`. This is not an `_id_` collision. Evidence: `raw/process-boundary.json` and the R22 process log.

Each stage used a separate `ClaimsEvidence` API process. `aggregate`, `receipt`, `audit`, `outbox`, and `beforeCommit` each logged its own reached stage, returned `503 CLAIM_STORAGE_UNAVAILABLE`, and left scoped counts `0/0/0/0`. `afterCommit` logged its stage, returned `201` with `idempotentReplay=true`, and left exactly `1/1/1/1`. The latter is an injected postcommit response-loss path; it is not Mongo unknown-commit evidence. Source stage calls are in `ClaimRepository.cs:116,122,129,135-142` in the immutable snapshot.

## Unknown-result recovery chronology

The independent committed branch injected Mongo's `UnknownTransactionCommitResult` write-concern error. The first HTTP response was `201` through durable receipt recovery, and a new API process returned the original `201` receipt with `idempotentReplay=true`. Scoped counts remained `1/1/1/1`, with one claim, receipt, audit and Pending outbox entry. The writer's separate run observed first `503` then restart replay `201`; both are consistent with the published policy because success is reported only when a durable receipt is resolved. Evidence: `raw/unknown-commit-rerun.json` and process logs.

For the not-committed branch, the independently recorded event times (UTC) were:

| Time | Observation |
|---|---|
| 06:15:22.962 | `commitTransaction` failpoint enabled (`ok=1`, code 251, unknown-result label) |
| 06:15:23.297 | First request: `503 CLAIM_STORAGE_UNAVAILABLE` |
| 06:15:26.187 | Scoped DB `0/0/0/0`; Mongo `currentOpen=1` |
| 06:15:27.417 | Failpoint disabled (`ok=1`) |
| 06:15:29.126–06:15:32.083 | First API stopped; new API healthy |
| 06:15:40.492 | First same-key retry: `503 CLAIM_STORAGE_UNAVAILABLE`, scoped DB still `0/0/0/0`, `currentOpen=1` |
| 06:16:23.800 | Poll still showed `currentOpen=1` |
| 06:16:29.679 | Poll showed `currentOpen=0`, aborted count increased by one; configured transaction lifetime was 60 seconds |
| 06:16:33.605 | Fresh API and same key: `201`, `idempotentReplay=false`, exactly one `1/1/1/1` write set |

`raw/independent-timeline.json` preserves every timestamp, failpoint reply, response body, DB document and transaction counter. The failpoint was already off at the failed retry. The still-open server transaction and its later abort explain the temporary 503; the evidence does not support classifying it as a remaining failpoint or a proven product defect. The first 503 is not proof of zero writes in an arbitrary unknown-commit case; this branch was independently queried and measured as empty. Source `ClaimRepository.cs:136-172` retries unknown commit at most twice, then looks up the receipt and returns 503 if unresolved.

## Contract and R08 disposition

The controlling [Claims annex](../../../../analysis/contracts/claims-semantics-v3.0.0.md) D187-06 says: resolve a durable receipt or return 503 pending same-key recovery; it sets no immediate-after-restart 201 deadline (`:207`). Acceptance C03 requires original-result replay after a **committed** response loss and restart (`:222`). Those requirements passed. The earlier R25 PARTIAL was driven by an immediate not-committed restart returning 503; that expectation is not a published acceptance requirement. Eventual 201 alone was not used as a PASS oracle: failpoint-off state, open-transaction lifetime, empty first state, final one-group DB state, exact same key and committed replay were separately checked.

R08's long-decimal two-process persistence/replay PASS is separately recorded in `mvp6-mod0187-e4-persistence-01/row-results.md:5`. This R25 run adds the previously missing process-level commit-uncertainty evidence; it neither reruns nor replaces R08's decimal evidence. The evidence-mode patch does not establish uptake in the normal production composition.

## Normal mode and configuration behavior

With deliberately invalid evidence settings under `Production`, Claims GET returned 200 and selected `NoOpClaimCommitProbe`. Under exact `ClaimsEvidence`, missing config, malformed fixed UUID and unsupported stage each produced HTTP 500 on the first Claims resolution, without mutation. `/health` was ready before that lazy singleton resolution. Thus the observed failure is **request-time fail-closed**, not process-startup failure. The owner-approved text requires bounded configuration and no normal-mode activation; it does not explicitly require startup rejection. The earlier proposed `RED-GREEN-PLAN.md:23` uses “fails startup”; that wording is not demonstrated and would require a separately explicit acceptance decision if startup failure is intended. It is not silently promoted into a product defect or R25 blocker.

## Evidence quality and limits

The independent run used a copy of the writer's HTTP/JWT/DB mechanics with a new source root, ports, replica set and database; the verifier supplied and executed a separate timeline probe and independent assertions over the raw JSON, source hashes and DB documents. The copied author's runner marked its aggregate result FAIL because it hardcodes an immediate 201 for some unknown-result branches. That runner exit is preserved, not hidden; its R22 and six stage observations passed, while the separate published-contract oracle above determines R25 disposition. A second authored runner similarly marked first-201 committed recovery as FAIL despite durable receipt replay. No historical result is represented as fresh without rerun.

No minimal product rework is requested. This bounded PASS does not grant normal integration acceptance, deployment, E5/G5 or CT module acceptance. No source, contract, pack, Program.cs, guard or git mutation was performed by this verifier.
