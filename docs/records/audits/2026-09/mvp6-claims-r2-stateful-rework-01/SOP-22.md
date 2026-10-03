# MVP6-CLAIMS-R2-STATEFUL-REWORK-01 — SOP §22

Agent verdict: PASS for F02/F03 candidate-model rework. Separate-agent VER: bounded PASS.
R01–R30 overall behavioral/runtime acceptance is NOT established.

## Exact corrections and RED→GREEN
F02: old verify-r2.py Store class AST extracted without executing old harness. Create1 then same key amount999
returns201 (RED: expected409). New stateful_model receipt identity tenant+LE+operation+target-or-create+key,
current action grant before receipt, UUID-valued root then lexical fingerprint, immutable original result replay.
Changed valid payload409 IDEMPOTENCY_KEY_REUSED; wrongroot+changedpayload409 CLAIM_CORRELATION_MISMATCH;
identical request replays original201/200 with no new writes. Actor not receipt identity; current authorization required.

F03: historical RED is structural AST evidence: all fail indices enter one `fail is not None` branch, no stage reached
trace. It is NOT a rerun of four genuine historical write boundaries. New GREEN models aggregate→receipt→audit→outbox
as distinct writes with observed prefixes/counts, then commit. Each precommit fault rolls back the full state including
existing records. A common exception rollback handler is intentional; injection points are distinct AFTER each write.
Unknown commit/postcommit response loss leaves committed model state;503 is uncertainty to caller, not zero-write claim;
same-key retry returns saved original result. Diagnostic attempts log is nontransactional test telemetry, not business state.

## Fresh results and ownership
-35/35 new author checks; repeated by separate verifier.
-16/16 independent adversarial checks including transition-stage rollback/retry and postcommit recovery.
-7/7 mutant defects caught with AssertionError in author and independent runs: payload acceptance, root bypass,
 collapsed failpoints, rollback disabled, unknown treated zero, auth bypass and lexical root.
-Exact rework.patch applies with zero fuzz to predecessor classification + added files; all4 output files byte-identical.
-Original candidate24 manifest entries verified; old archive bytes preserved. Candidate YAML/patch/annex/recovered model unchanged.
Separate verifier: /root/claims_stateful_ver (not rework writer); report INDEPENDENT-VER.md; commands/logs/inputs in archive.

## Classification and limits
R10/R15/R16/R19/R20/R25 gain named executable PARTIAL evidence; remaining rows preserve declaration/unverified limits.
Validated command / pre-resolved authoritative root is the new model boundary. This is not a complete reference/parser/amount
validator, full lifecycle implementation, JWT middleware or DB repository. No new business decisions/defaults.
Historical145/40 counts not freshly rerun or promoted; old Store state checks explicitly superseded, not silently rewritten.
Unknown transaction model is not real uncertain Mongo commit or concurrency/restart evidence. Pending outbox only.

## SOP22 record
Branch/HEAD: inherited feature/mvp6-logistics/4a8d4d4b339528a88e6220fb8402e5a2c771136c; no git mutation.
Worktree: shared dirty checkout; only this new archive/report directory written by this task. No global concurrency nochange claim.
Changed files: new model/test/mutation/classification delta in rework.patch; repository delivery archive/reports only.
Golden/Contract flow: candidate Claims model, inherited approved root/fingerprint/grant semantics.
Subflows/failure paths: integrated receipt conflicts, separate stage faults, committed response loss recovery.
Tests:35+16+7, fresh independent evidence; exact input/output hashes and stage traces archived.
Persistence/Security/Audit/Observability: in-memory state, grant sets and traces only; no HTTP/JWT/Mongo/transport.
Migration/Rollback: no production data/schema or migration; model rollback only.
Decisions/Blockers: F02/F03 bounded closure, independent verdict does not close broader unverified classifications.
Remaining gates: CT disposition, composition/release/publication/guard/consumer/runtime authority unchanged.
Out-of-scope changes: none. Canonical/runtime/Program.cs/pack/guard untouched; no commit/push/stash.

[Evidence archive](claims-stateful-rework.tar.gz) · [Hash manifest](SHA256SUMS) · [Independent VER](INDEPENDENT-VER.md)
