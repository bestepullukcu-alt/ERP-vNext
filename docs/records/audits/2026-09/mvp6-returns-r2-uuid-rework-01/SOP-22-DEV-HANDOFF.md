# SOP §22 — MVP6-RETURNS-R2-UUID-REWORK-01

## Verdict

**WRITER COMPLETE — candidate-model rework only.** F01 raw UUID equality is corrected in a new immutable rework package. No runtime service, canonical contract, producer, other module, old candidate or prior VER record was changed.

## RED → GREEN

- RED showed same UUID with upper-case representation was rejected as `409 CORRELATION_ROOT_MISMATCH` for create replay and transition (`RED.json`).
- GREEN parses validated UUIDs and compares UUID values. Upper-case replay returns `201 REPLAY`; upper-case transition returns `200`. A genuinely different UUID remains `409 CORRELATION_ROOT_MISMATCH`. Trimmed input remains invalid `502 RETURN_SHIPMENT_ROOT_INVALID`; braces/alternate formats are not admitted (`GREEN.json`).
- The fix also validates the request root lexically before value comparison; no trim or normalization expansion is introduced.

## Regression evidence

- `verify_r2.py`: 293 checks PASS, 279 refs, 174 examples.
- Root-before-fingerprint precedence is covered by the existing model path and the new replay/transition probes.
- 64 lifecycle pairs were replayed against the frozen edge oracle: 64 PASS, 0 FAIL. This is model/fixture evidence, not HTTP or persistence evidence.
- `recovered-harness.py` was not counted: its hard-coded external temporary path was unavailable in the extracted archive. No fabricated PASS was recorded.

## Decision → test mapping

F01 / root UUID value equality → `red-green-probe.py`, `green-probe.py`, `uuid-root-fix.patch`.
The approved semantics remain unchanged: lexical UUID validation first; UUID value equality for case variants; different values conflict; no payload fingerprint before root conflict; replay has no new write.

## Hash and scope

The prior R2 archive and candidate hashes are preserved in the parent audit. The new archive contains original/reworked model files, exact diff, RED/GREEN output, 64-lifecycle replay and verify output. Candidate YAML/annex bytes are unchanged.

Evidence archive: `returns-r2-uuid-rework-01-evidence.tar.gz`.

## Limits and next step

This is E1/E2 model evidence. It does not prove HTTP, Mongo transaction, JWT/RBAC, restart, outbox, producer uptake or consumer rollout. Independent VER is the next step; CT acceptance is not declared.
