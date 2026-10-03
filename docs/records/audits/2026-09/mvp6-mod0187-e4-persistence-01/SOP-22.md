# MVP6-MOD0187-E4-PERSISTENCE-01 — SOP §22

## Verdict

**PARTIAL.** Fresh process-boundary evidence closes R08, R19, R20, R23, R24 and R26. R22 and R25 remain exact GAPs because the production composition uses `NoOpClaimCommitProbe` and provides no authorized process-level collision/write-stage/unknown-commit activation seam. No hidden endpoint, Program.cs edit, shared probe edit or product-source change was introduced.

## Authority and inputs

- Independent R01–R30 authority: `docs/roadmap/plans/mod-0187-runtime-dispatch-01/runtime-acceptance-R01-R30.md`.
- Published Claims annex SHA-256: `16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63`.
- AUTH-05 immutable source archive SHA-256: `eaf786e647ef019f862c7dcc4f0b2ab29b2b5a36ff1bee98d2cca8d2863cc766`.
- AUTH-05 combined 97-path manifest SHA-256: `4ae0f286479761512f50216e5bd8be721e48ae4839c062b57cfdd29696e2daa8`.
- Program.cs remained `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`.

## Source, build, binary and processes

Disposable source: `/private/tmp/mvp6-mod0187-e4-persistence-01/source`. The 97 input entries passed before and after execution. Fresh no-incremental API build passed with 0 warnings and 0 errors. The executed API binary was `9468a3d06bb6cf67d8cfefc6d8b1b0332b70e7c556fee04738892207798ded2d`.

API processes PID 95466 and PID 96381 ran sequentially on `127.0.0.1:5065`. Mongo PID 94572 ran only on `127.0.0.1:27893`, replica set `claims_e4p01`, DB `diten_claims_e4p01`. Operational ports and databases were not used. Processes were stopped after capture.

## Fresh results

- R19/R23: exact 20-request same-key and different-key runs passed; two targets sharing one key remained independent; changed payload conflicted without writes.
- R20: revoked grant, different actor, Closed replay and soft-delete replay passed with preserved original audit actor and stable counts.
- R24: mixed-target and same-target 20-request races each yielded one winner and nineteen lifecycle losers, with one durable transition write group.
- R26: exact timestamp, root, null causation, payload, Pending state, aggregate identity and wire version passed from raw outbox documents.
- R08: long coefficient persisted exactly and survived a real stop/start boundary; the second process replayed without additional writes.

Detailed row evidence is in `row-results.md`; raw HTTP/DB is in `raw/process-boundary.json` and `raw/restart.json`.

## Unclosed requirements

R22 requires a real deterministic ID/claim-number collision. R25 requires distinct aggregate→receipt→audit→outbox/beforeCommit process failpoints plus unknown-commit and restart recovery. The approved probe interface exists in product code, but current DI binds the no-op implementation. Triggering either requirement would require a separately authorized runtime configuration seam or composition change. Server/database query failure was not substituted for these scenarios, and zero writes were not inferred from a failed query.

## Scope and no-change

Only lane-specific probes and this audit package were written. Production source, Program.cs, shared probes, contract, guard, other lanes and Git state were not changed. No commit, push or stash occurred. JWT secrets and bearer tokens are absent from the archive.
