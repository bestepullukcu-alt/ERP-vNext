# Claims stateful rework — fresh independent SOP §22 VER

Verdict: **PASS, bounded E1/E2 candidate-model verification**. No CT/runtime/deployment acceptance.
Verifier: /root/claims_final_independent_ver, newly delegated verifier, not author of stateful rework. Parent performs permanent evidence packaging only. Prior PASS not inherited.

## Exact integrity and executions
Unique safe extraction in this directory; archive members checked for absolute/traversal/symlink entries before extraction. 92/92 internal manifest hashes matched before execution (integrity.json). Archive identity: input-archive.sha256. All execution used fresh-run copies; source/archive not altered.
- python3 test_stateful.py: exit0,35 fresh assertions.
- python3 mutation_checks.py: exit0,7/7 deliberately broken models rejected with AssertionError, not import/syntax failure.
- python3 independent_checks.py: exit0,45 new adversarial assertions (independent-command.log, independent-results.json).
- Independent assertions applied to same7 mutants:6/7 caught. Auth-bypass mutant survived this narrower independent suite, which contains no unauthorized-grant assertion. It was caught by freshly rerun supplied35-check suite. This is transparent test coverage, not a model failure or claim that all7 were independently covered.

## Findings and closure criteria
Changed valid payload against existing receipt returns409 IDEMPOTENCY_KEY_REUSED. Wrongroot+changedpayload yields409 CLAIM_CORRELATION_MISMATCH before fingerprint. UUID case equivalence replays original result. Original create result remainsOpen even after transition advanced aggregate, and no state changes occur on replay/rejection. Both create and transition were independently exercised.
All4 failpoints are AFTER distinct aggregate/receipt/audit/outbox writes (stateful_model.py35–42); common rollback at44 is correct shared compensation, not collapsed injection. Each prefix/count is observed separately and full preexisting business state restored. Transition rollback also restores old status. Telemetry attempts intentionally nontransactional. Fault retry succeeds.
Unknown-commit and postcommit loss return503 while model contains committed aggregate,receipt,audit,Pendingoutbox. Samekey recovers exact saved original result without extra writes, for both operations. No zero-write inference from503. This model selects committed-but-unknown outcome; it does not reproduce Mongo commit ambiguity or prove actual transaction behavior.
Mutants caught: accept changedpayload, bypass root precedence, collapse failpoints, disable rollback, erase committed state on unknown, lexical-root comparison; supplied suite additionally rejects auth bypass. See fresh-run/mutation-results.json and independent-mutations.json for assertion names and exits.

## R01–R30 evidence handling
Classification reviewed directly. Only R10/R15/R16/R19/R20/R25 acquire this new scoped model evidence; all remain EXECUTABLE_PARTIAL with declaration/unverified limits. Rows R01–05,08–09,13–14,18,22,24,26–30 explicitly lack behavior evidence. Older partial evidence in R06/07/11/12/17/21/23 is historical and not freshly certified by this run. No declaration, schema hash or row label accepted as behavior proof.
Already validated command bodies and pre-resolved authoritative root are model input preconditions. No complete amount/ref/parser/HTTP/JWT/lifecycle oracle was tested. Tenant/LE receipt separation has model evidence only; no Mongo unique index/concurrency enforcement proof.

## SOP §22 boundaries
Branch feature/mvp6-logistics, HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c read, not changed. Shared dirty worktree; parent owns before/after repo evidence. This verifier wrote only this unique temp directory, no repository/global no-change claim beyond inspected input hash immutability.
Changed product/candidate files: none. Golden/flow: Claims candidate-model receipt/fault recovery. Failures: expected mutation assertion failures only. Persistence/security/audit/observability: memory dictionaries, grant sets,stage telemetry; noDB,HTTP,JWT,worker orpublisher. Migration/rollback: none in production, rollback model only. Decisions: no new policy. Blockers/gaps: full R01–R30 behavior,runtime uptake,real transaction/concurrency/restart,publication/consent remain outside verdict. Source correction not performed; no pack/canonical/guard/Program.cs/git changes. Parent may archive these evidence files as authorized.
