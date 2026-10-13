# MVP6-MOD0190-CT-ACCEPTANCE-REVIEW-01 — SOP §22

**Control Tower verdict: PARTIAL — EVIDENCE REWORK REQUIRED.** The approved isolated MOD-0190 implementation is accepted only for the explicitly evidenced behavior listed below. The work package as a whole is not `ACCEPTED`, because four pack-owned failure/correlation behaviors do not have independent runtime evidence. This decision does not change the promoted pack, source, tests or contracts and does not grant gateway, live integration, E5/G5, rollout or pack `done` status.

## Authority and exact reviewed scope

The controlling module authority is the promoted `ready-for-dev` pack at `/Users/natig/.codex/worktrees/mvp6-mod0190-core/ERP-vNext-recovery/execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md`, SHA-256 `6a57769ced4396d2bc4228749a7e24b0daf36ce279930bb77c5dfdfe19fd0983`. It authorizes the fixture-only DEMAND boundary, trusted JWT actor/no Workflow, atomic Pending-only outbox/no publisher and isolated 38-path core; the exact constraints are at pack lines 120–133 and acceptance criteria at lines 193–203. The later, separately approved composition chain is `11c586e0…` → patch `bd972051…` → `0f6bf84e…` → Testing-only DI patch `fb1f8a7e…` → final `Program.cs` `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5`.

Branch and HEAD at review were `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. The common checkout is intentionally dirty. Initial `git status --porcelain=v1` had 222 rows and SHA-256 `6a3033dfeb876b6f5a27c2c26edc5cb1761fe57a7e7efb847e9549d4ef935c`; this review does not treat a clean tree as a precondition.

## Source and evidence reconciliation

All supplied checksum files pass. The HTTP DEV source archive has 379 entries. The successor archive also has 379 entries and differs at exactly one path: `SandopAtomicityTests.cs`, `55d2522b…` → `eba2da6f…`. The remaining 378 entries, every production file and `Program.cs` are byte-identical. Therefore the earlier independent 22-record HTTP/DB evidence remains content-bound to the final production source. The successor test run is not described as a new HTTP run.

The old HTTP VER's 18/19 result remains immutable historical evidence: its committed-acknowledgement test required 503 even when the scoped durable receipt was visible. The separate test-only successor changed only that oracle, and a different verifier obtained full Sandop **19/19** plus the isolated test **1/1**. The isolated 1/1 is contained in the 19 and is not added to it. The successor independently confirms the failpoint fired and the receipt-visible 201 branch matches the published annex; it did not separately force the unresolved 503 branch.

The independent HTTP archive contains 22 records: 14 initial operation/security/replay requests, 4 restart requests, 2 rollback/retry requests and 2 concurrent same-key requests. These are records, not 22 unique tests. They prove the six operations, scoped persistence, JWT/RBAC, tenant/LE isolation, replay, changed-payload conflict, transaction rollback/recovery, one-effect concurrency, Pending outbox and same-binary/same-DB restart. The direct-Mongo re-VER independently proves exact-key/no-trim, duplicate-role concurrency, invalid fixture/checksum zero-write, unique-index conflict and unknown-commit receipt resolution.

Exact artifact bindings are in [EVIDENCE-HASHES.tsv](EVIDENCE-HASHES.tsv). The row-level acceptance disposition is in [ACCEPTANCE-MATRIX.tsv](ACCEPTANCE-MATRIX.tsv).

## Runtime version disposition

The failed historical pre-DI process used `/usr/local/share/dotnet` with `DOTNET_ROLL_FORWARD=Major`, because that host root had ASP.NET Core 10.0.2 but no ASP.NET Core 8. It is retained as historical failure evidence and is not used for final runtime acceptance.

The successful independent process was launched explicitly with `/Users/natig/.dotnet/dotnet` (`HTTP VER`, line 14). CT measured that exact host root as containing `Microsoft.AspNetCore.App 8.0.23` and `Microsoft.NETCore.App 8.0.23`; its API runtimeconfig targets `net8.0` and requests both frameworks at 8.0.0. The recorded launch environment keys contain no `DOTNET_ROLL_FORWARD`. Because this host root has no later major runtime, the successful process is disposed as native ASP.NET Core **8.0.23**, not a Major-roll-forward run. This supports the target .NET 8 boundary. The ASP.NET Core 10 attempt neither passes nor contaminates the successor chain.

## Acceptance decision

Accepted within this isolated package:

- scoped create/get, immutable snapshot/list and sign-off/list;
- exact-key replay, changed-payload conflict, original/current correlation preservation on exercised mutations;
- tenant and legal-entity 404, missing-permission 403 and parser-level 401 boundary;
- durable receipt, audit and one Pending outbox event per mutation;
- concurrent same-key one-effect behavior, receipt-insert rollback/retry and unknown-commit reconciliation;
- same database, fixture and binary restart durability;
- fixture-only immutable DEMAND provenance without a second demand SoR;
- final core test evidence at 19/19 and native ASP.NET Core 8.0.23 execution.

The following exact pack-owned evidence remains open and is the only reason this work package is not accepted:

1. Approved/Rejected/Archived capture → 409 `SANDOP_PLAN_STATE_CONFLICT`.
2. Sign-off outside InReview → 409 `SANDOP_SIGN_OFF_STATE_CONFLICT`.
3. Wrong-plan snapshot → 422 `INVALID_SNAPSHOT_REFERENCE`.
4. Independent direct-service checks for missing, malformed and duplicate `X-Correlation-Id`, including one fallback UUID, zero writes and the application-generated 401 correlation fallback. The independent run exercised parser-level 401 only; the DEV run's malformed-only case is not substituted for complete independent evidence.

These gaps also keep the pack's full six-operation 400/401/409/422/503 precedence matrix at `PARTIAL`. A narrow evidence-only DEV/VER may close them without changing production code if current behavior already conforms. A product mismatch requires a separate bounded DEV rework. No other closed behavior should be reopened.

## Preserved non-PASS and excluded gates

The broad same-host run remains **271/276**, and Loads `UnknownCommitResultRetriesAndCommitsExactlyOnce` remains **0/1** when isolated. They are not converted into repository PASS, waived or attributed to MOD-0190. Historical architecture non-PASS remains outside this decision. Live DEMAND, Workflow, Event Bus delivery, gateway/UI, shared permission registration, operational migration/rollout, E5/G5 and full-module acceptance remain excluded exactly as the pack requires.

## Repository disposition

This task wrote only `docs/records/audits/2026-09/mvp6-mod0190-ct-acceptance-review-01/`. At final measurement the branch and HEAD were unchanged. Excluding this owned output, status had 224 rows and SHA-256 `73b7915708a34789a28a5bd0f295af07a290a462765263d00aab4ec6cce7e9fb`, two rows beyond the initial baseline. The concurrent additions were the separately owned `mvp6-capacity-duplicate-candidate-reconcile-01/` and `mvp6-mod0192-x07-http-disposition-01/` audit directories; neither is a MOD-0190 source, pack, composition or reviewed evidence input. They were not attributed to or changed by this review.

The review did not rerun build/tests, modify source/test/pack/contract, alter another checkout, stage, commit, push or stash. Verdict is **PARTIAL / EVIDENCE REWORK REQUIRED**, not `ACCEPTED`.
