# MVP6-MOD0192-EXECUTOR-FAULT-REVER-01 — independent SOP §22

**Verdict: PARTIAL / REWORK.** The one-test-file executor fault evidence rework is reproducible: exact 43/43 source bytes build, five real `failCommand` cases pass with one measured hit each, and CapacityPlans passes 16/16 on a verifier-owned replica set. X06 closes at repository scope. X01 remains partial because its known-precommit response violates the published error policy. X07 remains partial because production reconciliation does not read the scoped audit record or fail closed on reconciliation read failure. The existing duplicate-name frozen-wire mismatch also remains open.

## Authority and source boundary

- Common checkout: branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, intentionally dirty, no verifier Git mutation. The DEV handoff is writer-complete.
- DEV package `SHA256SUMS` validated 9/9 files. Baseline restart-rework 43-file set plus `fault-test.patch` SHA-256 `946dff0f68ab7a8db8ca15df7352f5d5f77db5d0248401be2cb2738d6b047ee0` produces **43/43** `SOURCE-MANIFEST.tsv` bytes. Only `CapacityAtomicityTests.cs` changes: `0170f76fdaca43a69b913bdd6df994b9d22813e999f5b80acb931f5485b85229` → `576bf4001c0573f9884012c9aaaffeb57ab4e658b4eddc3be16de25a7acd98b8`. The source archive contains exactly 43 members.
- Approved draft `b5b948ee0803c535f91c9a3cc66e6098e7e29635c6b7f2aeaf6873b64eed74fc`, promotion-only diff `dd31bece8349cd19a914a05b0d5d77552f719bcd48a3d8993f255c87980e0c8f`, and promoted pack `d01bf7a050472a9708a989d7bd4d13ab0a41a81984a272bbb15848f93fcac6e0` were kept distinct. Published YAML/annex are `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` / `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`.
- `Program.cs` remains `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8`; `CapacityLeaseStore.cs` remains `2912a032deb172909cf7ae8f0730f0c412b0f05239c32aba0b3cf06794517b78`. No production, contract, pack or shared source changed.

## Independent execution

| Control | Result |
|---|---|
| Build | Fresh .NET 8 test-project build, exit 0, 0 warnings/errors. Existing restore metadata was reused; no independent NuGet restore is claimed. First exact build API DLL SHA `885bb8d7e93f48d1fb9eba7e43303f4be72761f0ae4c7a85137bd2e27fc72437`, test DLL SHA `ee33e608695926e4f7938721c9dce5b7e9877f680587954fbb579e5d78df33fc`. |
| Environment | Verifier-owned Mongo PID 32340, `rsmod192`, port 57192, unique dbpath, PRIMARY, `enableTestCommands=true`; no port 27017 use. |
| Five fault cases | **5/5 PASS**. TRX output independently records hit delta `1` for X01 definite precommit, X01 commit acknowledgement loss, X06 terminal audit insert, X07 no-commit unknown result and X07 committed acknowledgement loss. |
| CapacityPlans | **16/16 PASS**, exact source, `--no-build --no-restore`. This includes the prior restart harness and all five new fault cases. |
| Negative sensitivity | In a disposable-only copy, deleting the terminal audit immediately before the X07 postcondition assertion produced the expected RED: `Expected: 1; Actual: 0`. Exact source was restored to SHA `576bf...` and rebuilt. This shows the final assertion detects a missing audit; it does **not** prove the product catch reads audit or handles a read failure. |
| Full suite | Not rerun because the only source delta is a test and DEV already records hash-bound **154/155 non-PASS**. Sole historical failure is `Loads.LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce` (201 expected, 503 actual). It remains non-PASS; no waiver or unrelated fix. |

## Path:line findings

1. **X01 frozen-wire mismatch — OPEN.** `CapacityAtomicityTests.cs:108-114` injects Mongo error code 8 with an empty label set; `:158-166` calls this a definite precommit cut, proves five scoped collections remain zero, but expects `503 COMMIT_RESULT_UNRESOLVED`. `CapacityRepository.cs:163` sets `commitAttempted=true` before commit and `:169-170` maps every Mongo/timeout after that flag to `COMMIT_RESULT_UNRESOLVED` without checking uncertainty labels. Published annex `sandop-capacity-semantics-v2.0.0.md:15` requires `DEPENDENCY_UNAVAILABLE` for known storage failure and reserves `COMMIT_RESULT_UNRESOLVED` for uncertain POST commit after scoped receipt resolution. Rollback/recovery evidence passes; response-code parity fails.
2. **X07 audit/read-failure reconciliation — OPEN.** `CapacityLeaseStore.cs:109-114` catches `UnknownTransactionCommitResult`, reads only scoped evaluation and outbox, and returns true when terminal status plus any terminal event exist. It never reads the matching terminal audit identity/status/correlation, and a read exception is not converted to an unresolved result. `CapacityAtomicityTests.cs:233-247` verifies one audit after committed ack loss, so the assertion is postcondition-sensitive, but does not establish the catch consulted it. `BLOCKED-CapacityLeaseStore.patch` SHA `94f65a697acf533f4f1b3ea49e5d3c18e962426686873ec19961162f1ae50730` is **unapplied** and is not accepted evidence.
3. **Duplicate-name frozen-wire mismatch — OPEN.** `CapacityRepository.cs:119-120` deterministically returns `409 CAPACITY_SCENARIO_NAME_CONFLICT`; published YAML `sandop-capacity.openapi.yaml:1619` lists only `CAPACITY_PLAN_STATE_CONFLICT` and `IDEMPOTENCY_KEY_REUSED` for create-scenario 409. The unique-index race path retries duplicate-key at `:131-136` and can end in 503, so deterministic and race behavior are not one contract-defined branch.
4. **X06 repository-scope closure — PASS.** `CapacityAtomicityTests.cs:195-212` independently proves staged evaluation update, slot delete, audit and Pending outbox are atomically rolled back at injected audit insert, then one fenced retry commits all four effects and rejects a stale duplicate. This is direct repository evidence, not a hosted process or HTTP/JWT result.

## Evidence limits and handoff

`ACCEPTANCE-MATRIX.md` records A192-01…18 and X01…10 without converting inherited evidence into fresh execution. No composed HTTP/JWT, `Program.cs` registration, hosted executor, live DEMAND/constraint producer, publisher, E5/G5 or CT acceptance is claimed. Raw build logs, TRX files, command results and cleanup are archived and hash-bound. The common checkout source was not modified by verification; verifier Mongo was stopped and port 57192 is free.

**Required rework:** (a) contract-compatible classification of known precommit versus uncertain commit in X01; (b) authorized production reconciliation that verifies evaluation + exact event + exact audit and fails closed on scoped read error, followed by independent fault verification; (c) separate disposition for duplicate scenario-name 409/race behavior.

## Automatic approval boundary

The DEV lane's two attempts to apply a production `CapacityLeaseStore.cs` change were automatically rejected because that mutation exceeded the authorized test-only rework scope. The proposal remains unapplied; this verifier did not retry or bypass that decision. A separate explicit production-change authority is required before the X07 reconciliation gap can be fixed.

**Verifier writer-complete:** yes. Persistent changes are limited to this audit directory.
