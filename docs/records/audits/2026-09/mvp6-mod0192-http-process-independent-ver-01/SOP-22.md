# MVP6-MOD0192-HTTP-PROCESS-VER-01 — SOP §22

**Independent verdict: PARTIAL.** The immutable 422-path handoff reproduces the repository, five later-read fault, composed HTTP/JWT, normal restart, Pending-only and two-host single-terminal-effect results. Full hosted in-flight renewal-loss/stale-worker evidence and transport-boundary response-loss evidence remain open because the approved runtime exposes no bounded timing or response-loss seam. Repository evidence for those mechanics is preserved but is not relabeled as hosted evidence.

## Authority and inputs

The verifier did not author the DEV implementation or its evidence test. The user's 2026-09-23 decision approved the exact 43-source transfer/composition scope, Capacity-owned evidence/test work, isolated HTTP/JWT and process/restart verification, and a later independent VER. It explicitly excluded further production persistence, MOD-0190 behavior, duplicate-name policy, canonical/guard, gateway/shared permission, live producer, rollout, E5/G5 and git writes. It also states that event/audit physical Mongo `_id` equality is not an acceptance requirement.

| Input | Verified SHA-256 |
|---|---|
| DEV `SOURCE-MANIFEST.tsv` | `59d21906b77c82431df3746ad7725e305ed7286029266378fe7288c6a8a10151` |
| DEV `SOURCE-422.tar.gz` | `500c395438ee34875b872671f41ce36420512d0682bd1757310c7505e3ef9970` |
| DEV `EVIDENCE-TEST.patch` | `3f0dbbf7875837873d433f414f20468bd8a7fcf38573b411b58d3094705016fc` |
| DEV `PROGRAM-COMPOSITION.patch` | `78cc0fa61dd37019f525c621c572d1dc2ef518eb0b4faf520a280f01909db82e` |
| Published YAML | `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` |
| Published annex | `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` |
| Executor decision | `cfddf953a1a98107869417ef8afbcad5f578fcfde2a8cad8e1d2b6b05197cc5d` |

`SOURCE-422.tar.gz` was extracted to the unique disposable directory `/private/tmp/mvp6-mod0192-http-process-ver-01.pim70Z/source`. All **422/422** manifest entries matched before execution and again after build/tests/probes. The DEV mutable worktree and its Mongo data directory were not used.

## Fresh source → build → process chain

Fresh restore completed from the verifier copy. The API build passed with zero warnings and zero errors. The independently produced binaries were:

| Binary | SHA-256 |
|---|---|
| `Diten.SupplyChainService.Api.dll` | `2976bf342d393cda11a4d06d62f30a21f2a8599f82a93057bd46bbe937640294` |
| `Diten.SupplyChainService.Persistence.dll` | `67d85f6a26319949d886b4f4fdcd4abcc5925dc9b127e0712a14e922bd881c8d` |
| `Diten.SupplyChainService.Tests.dll` | `a75b1216a9cf332a7881caba82edee7394b028838786a5aafa7a28c58df8103f` |

The verifier used a fresh MongoDB 8.0.18 data directory and a newly initialized single-node `rsmod192` replica set. The immutable tests hard-code the DB-010 test endpoint `127.0.0.1:57192`; that previously unused port was bound to the verifier's fresh directory. API hosts used new ports `56292` and `56293` and isolated databases `DitenSupplyChain_Mod0192_VerHttp` and `DitenSupplyChain_Mod0192_VerRace`.

## Acceptance results

| Requirement | Fresh verdict | Exact evidence and boundary |
|---|---|---|
| Capacity repository group | **PASS — 36/36** | `raw/capacity-ver.trx`, duration 2m14s. The historical 20/20 is contained in the former 31/31; it is not added to 36/36. |
| Five X07 later reads | **PASS — 5/5** | Event broad/exact, audit broad/exact and active-slot each show `failpointEntries=1`, `injectedFailures=1`, refused reconciliation and unchanged state in `capacity-ver.trx`. Initial evaluation-read failure is a separate test and was not substituted for these five. |
| Six Capacity HTTP operations | **PASS** | Plan create/get, scenario create/get and evaluation create/get returned published 201/200/201/200/202/200 behavior in `raw/http-ver.jsonl`. |
| JWT/RBAC/header/isolation | **PASS** | Fresh 401 unauthenticated, 403 missing grant, 400 malformed correlation, 400 tenant query override and 404 foreign-tenant read. Response correlations were present where the application contract requires them. |
| Replay/conflict | **PASS** | Plan/scenario/evaluation same-key replay returned the original result with current response correlation. Changed valid plan payload returned 409 `IDEMPOTENCY_KEY_REUSED`. |
| Hosted terminal effect | **PASS** | Evaluation completed with literal `40.000` shortfall. DB snapshot has `attempt=1`, `fence=1`, one terminal event, one terminal audit and zero active slots; event/audit `OccurredAt` equals evaluation `CompletedAt`. Physical `_id` equality was neither required nor asserted. |
| Process restart | **PASS** | A new API process read the persisted plan and terminal evaluation, then replayed the original Accepted 202 receipt without creating another evaluation (`raw/restart-ver.jsonl`). |
| Two hosted executors | **PASS for single durable effect** | Two hosts on 56292/56293 shared the race DB. Result: Completed, attempt 1/fence 1, one terminal event, one terminal audit, zero active slots and one Pending terminal event (`raw/two-host-db-state.json`). |
| Pending-only / no publisher | **PASS** | Terminal event remained `Pending`; no Capacity publisher source or composition was added. Both hosts logged unregistered transport. |
| Hosted kill during live lease, renewal loss, stale fence | **PARTIAL** | The literal fixture finishes before a controlled kill can preserve a live lease. Repository tests cover lease/fence mechanics, but no approved timing seam exists for a hosted falsification. |
| Hosted terminal HTTP response loss / unknown commit | **PARTIAL** | Repository failpoints cover committed/uncommitted outcomes and normal restart proves durable replay. No approved transport-loss seam exists, so real response-loss observation was not produced. |

X01 repository closure remains **PASS/closed** because this run found no contrary evidence. The two hosted gaps do not reopen X01 or the repository-level X07 result.

## Discarded attempts and evidence limits

The first sandboxed restore stalled after `Determining projects to restore...`; it was bounded and terminated. It is preserved as `raw/restore.log` and is not counted as PASS. A separately authorized restore completed and is recorded in `raw/restore-success.log`. No discarded test attempt is counted.

This evidence is local E4-style isolated runtime evidence. It is not gateway, live DEMAND/constraint producer, publisher, rollout, E5/G5 or production deployment evidence. It does not change duplicate-name behavior and does not introduce a new acceptance oracle.

## Cleanup and no-change

API ports 56292/56293 and Mongo port 57192 were stopped; `raw/cleanup-listeners.txt` is empty. The extracted source matched the 422-entry input manifest after all runs. The repository received only this new audit directory. No product source, pack, contract, guard, Program.cs or git state was changed; no commit, push or stash was performed.

**Remaining blockers:** hosted live-lease kill/renewal-loss/stale-worker timing and hosted transport response-loss require a separately approved bounded test/control seam if CT requires those observations. Without such a seam, the technically accurate module evidence verdict remains PARTIAL.

