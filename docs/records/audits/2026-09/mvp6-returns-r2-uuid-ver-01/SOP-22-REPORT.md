# SOP §22 — MVP6-RETURNS-R2-UUID-VER-01

## Verdict

**PASS for the bounded UUID model rework; runtime acceptance not claimed.**

The rework archive hash `e4a5e5c80fe3b306898abe50d632525f94794765e07d927b4dbbf52644d78eb9` and its internal manifest were verified. The prior independent VER archive remains unchanged at `ceca0b3ea50079574dac3ca3ad576e9bbf8515defd4ef392b1142e2fe1c82e3c`.

## Independent model calls

Using the reworked model directly, fresh calls proved:

- lower → mixed-case same UUID replay: `201 REPLAY`;
- lower → mixed-case same UUID transition: `200`;
- genuinely different UUID: `409 CORRELATION_ROOT_MISMATCH`;
- malformed/trimmed UUID: `502 RETURN_SHIPMENT_ROOT_INVALID`;
- same UUID with changed payload: `409 IDEMPOTENCY_KEY_REUSED`;
- 64 lifecycle regression pairs: `64/64 PASS`.

No trim, braces or alternate UUID format was accepted. Root checks remain before payload fingerprint checks.

## Exact diff and scope

The patch changes only `returns_model.py`: UUID value comparison is introduced for stored roots and request roots are lexically validated before parsing. Candidate YAML, annex, canonical contract, producer, runtime service, other modules and prior reports are unchanged. The diff is limited to the root comparison/validation lines and was hash-verified from the rework archive.

`verify_r2.py` remains `293 PASS` with `279 refs` and `174 examples`. The unavailable hard-coded `recovered-harness.py` path was not counted as a PASS.

## Limits

Evidence is model-level E1/E2. No HTTP, Mongo, JWT/RBAC, restart, outbox, producer uptake, consumer rollout, publication or DEV GO is established. The rework writer is complete; any further acceptance requires normal CT/runtime gates.
