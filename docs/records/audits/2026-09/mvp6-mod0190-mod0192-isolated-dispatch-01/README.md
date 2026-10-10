# MVP6 MOD-0190 / MOD-0192 isolated dispatch — SOP §22 successor

Date: 2026-09-22  
Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Staged files at closure: `0`

## Control Tower result

| Work package | Pack promotion | DEV / independent VER | CT disposition for this dispatch |
|---|---|---|---|
| MOD-0190 bounded core | Approved draft `04f2e36f89cae0a21300217b63756b0cd3104c33af145d985b9fc101a83b38a2` was promoted separately to `ready-for-dev` pack `6a57769ced4396d2bc4228749a7e24b0daf36ce279930bb77c5dfdfe19fd0983`. | Exact 38-path core compiled. Final direct-Mongo rework changed three owned files; independent fresh build and 19/19 tests passed with real rollback, exact-key, collision and two unknown-commit modes. | **PARTIAL.** The approved isolated direct-Mongo core and its three-file rework are independently verified. Composed HTTP/JWT, service-process restart, shared composition and E5/G5 remain outside this dispatch and unaccepted. Historical full suite 147/152 and architecture 15/18 remain non-PASS. |
| MOD-0192 bounded core/executor | Approved draft `b5b948ee0803c535f91c9a3cc66e6098e7e29635c6b7f2aeaf6873b64eed74fc` was promoted separately to `ready-for-dev` pack `d01bf7a050472a9708a989d7bd4d13ab0a41a81984a272bbb15848f93fcac6e0`. | Exact 43-path source compiled. Restart harness rework independently passed. Final fault evidence independently reproduced a fresh build, 5/5 failpoint tests and 16/16 CapacityPlans tests. | **REWORK.** Positive repository/Mongo evidence is retained, but three product/wire findings remain open. Historical full suite 154/155 remains non-PASS; its sole failure is the unrelated Loads unknown-commit test. No composed HTTP/JWT or E5/G5 acceptance. |

The promoted pack hashes are intentionally different from the approved draft target hashes. Promotion changed status/checklist/authorization prose only; it did not silently redefine business behavior.

## Exact contract and source bindings

- Published SANDOP-CAPACITY YAML: `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`.
- Published annex: `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`.
- MOD-0190 final 38-source manifest: `b55e7b2128df259604e4a318cc9194bfeff24610b1dba1790f333ef20d74a824`; rework patch `46cad9ff04bc938cda177ba5125f27394739149a29a1c7a21a4b97165e97be3b`.
- MOD-0192 final 43-source manifest: `7b3778df5ed6e863b0f777f17ba61de6e5136276a7ec8144bd701d4cfb6e3c27`; restart patch `1d86461a2cf3d915f72bd885927346b66d254341dfbb679f2f0c9608159c58fa`; executor-fault test patch `946dff0f68ab7a8db8ca15df7352f5d5f77db5d0248401be2cb2738d6b047ee0`.
- `Program.cs`, shared DI/permissions, gateway, canonical contract/guard, UI and other module production paths were not changed by either feature writer.

## MOD-0190 evidence disposition

The final independent re-verification is `../mvp6-mod0190-core-rever-01/SOP-22-REVER-REPORT.md`. It reconstructed the DEV-01 baseline, applied the exact three-file patch, matched 38/38 final hashes, compiled with zero warnings/errors and reproduced 19/19 tests on a verifier-owned Mongo replica set with test commands enabled.

Closed at direct repository/Mongo scope:

- exact parsed idempotency key, including whitespace-only key differences;
- receipt-insert failure after staged aggregate write with five scoped collections unchanged;
- real unique-index collision and business conflict;
- no-receipt unknown commit followed by controlled session resolution and one same-key effect set;
- committed acknowledgement loss followed by durable original receipt replay;
- audit/outbox counts and `Pending` outbox state.

Still open: real routed HTTP/JWT/RBAC/header behavior, application 401, service-process restart, shared composition, live DEMAND/Workflow/Event Bus and E5/G5. These were explicitly excluded or require a later integration-owner work package.

## MOD-0192 evidence disposition and exact rework findings

The final independent report is `../mvp6-mod0192-executor-fault-rever-01/SOP-22-VER.md`, SHA-256 `0a2ce9e4f080d286164c27d998f6a4c46f5515164b4953f71bbdde60d43384a8`. Its raw archive is `66d56f5ef5426cc60969ccb3130b76f1797d4117d929f4edd85484c21a13efe6`.

Verified bounded evidence:

- 43/43 final source/test hashes and fresh zero-warning/error build;
- 16/16 CapacityPlans tests and five real app-name-scoped Mongo failpoint cases;
- X01 and X06 no-partial-write/rollback outcomes;
- X07 no-commit versus committed acknowledgement-loss outcomes;
- two-process, server-time lease expiry and restart harness behavior;
- one fenced terminal effect and `Pending` terminal outbox.

Open findings requiring separate disposition/rework:

1. `createCapacityScenario` duplicate name reaches `409 CAPACITY_SCENARIO_NAME_CONFLICT`, but the frozen operation publishes only `CAPACITY_PLAN_STATE_CONFLICT` and `IDEMPOTENCY_KEY_REUSED` for 409. A contract owner must select and publish an exact disposition before that branch can be accepted.
2. X01's known, label-free precommit error is returned as `503 COMMIT_RESULT_UNRESOLVED`; the published annex requires `503 DEPENDENCY_UNAVAILABLE` for known precommit dependency/storage failure. Data rollback passes, wire error parity fails.
3. X07 application reconciliation checks scoped terminal evaluation plus event, but does not verify the exact terminal audit record and does not prove fail-closed behavior when the reconciliation reads fail. The reviewable unapplied candidate is `../mvp6-mod0192-executor-fault-rework-01/BLOCKED-CapacityLeaseStore.patch`, SHA-256 `94f65a697acf533f4f1b3ea49e5d3c18e962426686873ec19961162f1ae50730`.

The duplicate-name and known-precommit findings are product/wire mismatches. The X07 proposal was not applied and is not counted as implementation.

## Test and environment closure

- MOD-0190 independent final targeted result: 19/19; verifier Mongo port 57490 stopped.
- MOD-0192 independent final targeted result: 16/16; final full-suite result was not rerun by the last verifier. The hash-bound DEV result remains 154/155 non-PASS, with the unrelated Loads unknown-commit test failing.
- Closure checks found ports 57190, 57192, 57390 and 57490 free.
- Common checkout staged state is empty. The common checkout intentionally retains other dirty lane work; no clean/reset/stash operation was performed.
- No commit, push, stash, migration, rollout, pack `done` promotion, E5/G5 or composed HTTP GO occurred.

## Automatic approval boundary

The exact X07 `CapacityLeaseStore.cs` production change was attempted twice through the direct edit tool after the file's presence in the approved 43-path list was demonstrated. Automatic approval review rejected both attempts as outside what it interpreted as a test-only rework scope. No shell or alternate mutation route was used to bypass those rejections. The candidate diff and test-first RED evidence are retained for an explicit application disposition.

## Next gates

1. Contract owner disposition for the duplicate-name 409 behavior.
2. A bounded MOD-0192 production rework for known-precommit 503 mapping and X07 audit/read-failure reconciliation, followed by independent VER.
3. Separate integration-owner composition packages for MOD-0190 and MOD-0192 if composed HTTP/JWT is desired. They must name exact `Program.cs`/shared permission diffs and cannot be inferred from this core evidence.

This report records the isolated dispatch outcome only. It is not full-module acceptance, capability completion, release verification or downstream GO.
