# Independent candidate-model VER — MVP6-CLAIMS-R2-STATEFUL-REWORK-01

Verdict: PASS for the two bounded candidate-model corrections; not full R01–R30 acceptance.

Verifier is a separate child agent, not the rework author. Inputs were copied to this unique disposable directory before execution. Author tests rerun:35/35, exit0. Separate adversarial script:16/16, exit0. Exact copied/source hashes:inputs.json. No source correction performed.

## Findings
F02 closed within model scope: receipt lookup integrates current requested grant, tenant/LE/operation/target/key, UUID-value root equality, then lexical fingerprint conflict, then original-result replay. Changed valid create amount and transition note/time reject409 IDEMPOTENCY_KEY_REUSED; simultaneous wrong root takes409 CLAIM_CORRELATION_MISMATCH precedence. Equivalent UUID case accepts original replay without writes. Saved actor/audit/outbox are not overwritten. This models already-valid inputs; full HTTP/header/reference/current-resource-deletion validation is not proved.

F03 closed within model scope: aggregate, receipt, audit, outbox each execute their own write and boundary injection. Observed prefix/counts differ at each failpoint, all committed-state collections restore exactly, and retry succeeds. Additional transition tests demonstrate rollback restores existing aggregate status as well as related records. Unknown-commit and postcommit response loss leave committed model state and recover via same-key receipt rather than claiming zero writes.

## Evidence limitations
Historical failpoint RED evidence in author test is AST identification of one fail-is-not-None branch, not four independently observed historical stage traces. Fresh GREEN has distinct observed stage/count traces. Independent copied mutation-check execution now passed:7/7 corrupt models produced AssertionError, covering changed payload accepted, root precedence removed, all failpoints collapsed, rollback disabled, unknown commit misclassified as zero writes, auth bypass, and lexical root equality. See mutation-results.json and mutation-run.log. Mutation script hash added to inputs.json. No Mongo transaction, real concurrency, HTTP/JWT, persistence/restart or runtime uptake evidence. Retained full decision coverage classification must not promote amount validation/retention, references, deletion/TTL, event schema or whole security pipeline from this narrow Store. No new business policy authorized.

## No-change
Source inputs rehashed after tests: True. Only own disposable files written. No repository/runtime/canonical/guard/pack/git changes. This verdict does not authorize publication, consent, CT acceptance or DEV GO.
