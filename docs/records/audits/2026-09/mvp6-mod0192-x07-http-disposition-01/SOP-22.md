# MVP6-MOD0192-X07-AND-HTTP-DISPOSITION-01 — SOP §22

**Verdict: PARTIAL acceptance; integration package PREPARED and HELD.** X01 is closed at repository/failpoint scope. X07's scoped terminal reconciliation is repository-PASS, while five later reconciliation-read failure boundaries and all composed HTTP/hosted-process scenarios remain open. The prepared shared change is a one-file `Program.cs` candidate; it has not been applied to a real checkout and has no owner authority yet.

## Exact inputs

| Input | Exact binding | Result |
|---|---|---|
| Promoted MOD-0192 pack | `/private/tmp/mvp6-mod0192-core-dev-01/execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md`; SHA-256 `d01bf7a050472a9708a989d7bd4d13ab0a41a81984a272bbb15848f93fcac6e0` | `ready-for-dev`; 43-path core/executor scope; shared composition excluded |
| Final Capacity source set | [CAPACITY-43-SOURCE-MANIFEST.tsv](CAPACITY-43-SOURCE-MANIFEST.tsv); SHA-256 `36114191e1576b8b5654b9910f3347432d5d3bf36aac87339a4280cc85a852f8` | 43/43 final bytes verified; X01/X07 independent VER source targets preserved |
| Repository VER | [independent VER](../mvp6-mod0192-x01-x07-independent-ver-01/SOP-22-VER.md) | fresh 20/20 fault group contained within fresh 31/31 Capacity group; the counts are not additive |
| Controlling executor decision | [DECISION.md](../../../roadmap/plans/mod-0192-executor-exact-decisions-01/DECISION.md) §§ terminal atomiklik and X01–X10 | exact scoped effect, process/restart and persisted-state acceptance authority |
| Published contract | SANDOP-CAPACITY 2.0.0/wire v1 YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` | unchanged |
| Integration baseline | MOD-0190 HTTP DEV-02 [source archive](../mvp6-mod0190-http-dev-02/source.tar.gz) `f4d1b79d7aa74f8db86f782ac638cc6d606c8cbfcf0c183e50d5ace1ff02c3e5`; [379-entry manifest](../mvp6-mod0190-http-dev-02/source-manifest.tsv) `83966e5e641546ba094e47f8585d7e2ce7affb7001b1e909fe220e7811fdac14` | immutable baseline; `Program.cs` `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5` |

The 43 Capacity paths and the 379 baseline paths have zero intersection. Overlaying them produces 422 source/test paths before the separately owned `Program.cs` edit. This avoids the stale intermediate `0f6bf…` Program snapshot and preserves MOD-0190's approved Testing-only DEMAND DI, Claims, Returns, Shipment, Loads, Carriers, and strict UTF-8 `Idempotency-Key` handling.

## Acceptance disposition

| Topic | Controlling requirement | Current evidence | Disposition / exact remaining falsification |
|---|---|---|---|
| X01 definite precommit rejection | `DECISION.md` X01; known rejection must not become unknown commit | independent VER: real commit failpoint, `503 DEPENDENCY_UNAVAILABLE`, zero five-collection partial state, stable retry | **PASS/CLOSED at repository scope.** X07 gaps do not reopen it. The composed 503 envelope remains an HTTP observation, not a repository defect. |
| X01 uncertain and committed outcomes | DB outcome read precedes classification; no blind duplicate | independent VER distinguishes no-commit unresolved from committed acknowledgement loss and stable original replay | **PASS/CLOSED at repository scope.** Hosted response-loss/restart observation is listed separately. |
| X07 terminal effect identity | `DECISION.md` terminal atomiklik: scoped evaluation plus unique `(tenant, LE, evaluationId, terminal-event-type)` event, exact audit/event content/count, slot release | independent VER: exact tuple/count and 13 mutation cases | **PASS at repository scope. Physical Mongo event/audit `_id` equality is not required.** No accepted source persists an expected event/audit `_id`; requiring it would be a new storage/provenance rule. |
| X07 initial evaluation read failure | all reconciliation reads fail closed | independent VER injected initial evaluation `find`; one hit and no success | **PASS for this boundary.** |
| X07 later event/audit/slot reads | same fail-closed rule | only common catch/source inspection; no independent injection at later count boundaries | **PARTIAL.** Independently fault broad-event count, exact-event count, broad-audit count, exact-audit count, and active-slot count. Each test must prove target boundary reached, one fault hit, reconciliation refused/threw, and terminal DB state unchanged. If the Mongo failpoint cannot isolate a boundary, use a separately approved test seam or leave it unverified. |
| Composed HTTP/JWT | pack and published annex: real auth, permission, tenant/LE, headers, status/code/body/correlation | no Capacity `Program.cs` registration in an accepted integration baseline | **PENDING.** Run six operations, 401/403 separation, tenant/LE/soft-delete, exact replay/conflict and request/response correlation after authorized composition. |
| Hosted executor/restart | `DECISION.md` X03–X09 and line requiring separate process/restart | repository child-process tests do not establish composed host behavior | **PENDING.** Kill/restart with valid/expired leases; stale fence zero writes; renewal loss; two-worker lease race; committed and uncommitted terminal outcomes; publisher absent while terminal progresses and event stays Pending. |
| Fixture/publisher boundary | exact tenant/LE test fixture; literal Finite/Infinite oracle; no live producer or publisher | fixture readers and hosted executor are in final 43; no Capacity publisher is added | **Preserved.** Evidence must label fixture results as fixture evidence. `ShipmentOutboxWorker` is not a Capacity publisher. |
| Duplicate scenario name | separate unpublished contract/behavior gate | explicitly excluded from X01/X07 rework and this task | **UNCHANGED/PENDING elsewhere.** No behavior change in this package. |

## Prepared integration candidate

The immutable MOD-0190 baseline was reconstructed in `/private/tmp/mvp6-mod0192-x07-http-candidate-final`, the exact 43 Capacity files were overlaid, and [PROGRAM-COMPOSITION.patch](PROGRAM-COMPOSITION.patch) was generated against baseline `Program.cs` `a2a216…`. Patch SHA-256 is `78cc0fa61dd37019f525c621c572d1dc2ef518eb0b4faf520a280f01909db82e`; target `Program.cs` SHA-256 is `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0`.

The patch adds only Capacity namespace aliases, persistence registration, exact fixture-reader DI, the module-local hosted executor, Capacity model-validation envelope selection, Capacity context middleware, and exclusion from the Shipment fallback middleware. Namespace aliases keep S&OP and Capacity fixture-reader types unambiguous. It changes no permission catalog, gateway, canonical contract, publisher, or MOD-0190 behavior.

Fresh disposable restore succeeded. The first no-restore build failed only because the archive intentionally lacked `obj/project.assets.json`; after fresh restore, `dotnet build` succeeded with 0 warnings and 0 errors. A second clean-room restore attempt later remained at dependency discovery without progress and was interrupted with exit 130; it is retained in `evidence/RESTORE.log` and is not counted as success. The already restored candidate was then rebuilt with `--no-restore`, again succeeding with 0 warnings and 0 errors in [BUILD.log](evidence/BUILD.log). This proves composition compiles; it is not HTTP/JWT, Mongo, executor restart, E4, E5/G5, or rollout evidence.

[CAPACITY-43-SOURCE.tar.gz](CAPACITY-43-SOURCE.tar.gz) contains exactly 43 regular files and matches all 43 manifest hashes. It is a transfer package, not a new source revision.

## Authority and next action

Existing decisions authorize the 43 Capacity-owned core/executor paths, but explicitly exclude `Program.cs`. Therefore [OWNER-DECISION-REQUEST.md](OWNER-DECISION-REQUEST.md) is the only new authorization required. After that exact one-file authorization, one integration writer may apply the transfer archive and patch to a checkout reconstructed from the 379-entry baseline, then execute [HTTP-PROCESS-DEV-v1.0-HELD.md](HTTP-PROCESS-DEV-v1.0-HELD.md). An independent writer must follow with [HTTP-PROCESS-VER-v1.0-HELD.md](HTTP-PROCESS-VER-v1.0-HELD.md).

No production source, real `Program.cs`, contract, pack, guard, gateway, or git state was changed. Only this audit directory and a disposable `/private/tmp` candidate were written. No commit, push, or stash was performed.
