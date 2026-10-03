# MVP6 Consumer R2 Rework — CT Close (SOP §22)

**Verdict: ACCEPTED — bounded E1/E2 candidate-model rework only.**

This CT disposition closes the recorded Returns UUID comparison finding and the Claims stateful F02/F03 findings within their candidate-model scope. It is not runtime, publication, consumer-uptake, pack-promotion, E5/G5, or DEV-GO acceptance.

## Baseline and integrity

- Branch: `feature/mvp6-logistics`.
- HEAD was read as unchanged during this review. The checkout was already dirty with unrelated MVP6/module work; that inventory is not attributed to this review. No source, contract, pack, runtime, guard, or historical record was changed.
- This record is the sole owned repository output of this CT close.
- Evidence archive hashes verified from the repository:
  - Returns rework: `e4a5e5c80fe3b306898abe50d632525f94794765e07d927b4dbbf52644d78eb9`.
  - Returns independent VER archive: `c3eb552998d51eb463a09920c0523fac54714411fbacab10deb6eaf40a5a372d`.
  - Claims rework: `bd533a4da51657e0e6eb6736e07e603bdf849dda82dd85a99ed0b715c489da60`.
  - Claims independent VER archive: `0262d231ae47b4ca069c48bd217bf20d199a28160a3b705fe966a1d18ebea29d`.
- Claims manifests independently report the same rework archive hash and 92/92 internal member hashes. Historical candidate inputs and contract/annex/runtime inputs are reported byte-preserved.

## Chain and findings

| Workstream | Chain | CT result |
|---|---|---|
| Returns UUID | Original R2 candidate → `mvp6-returns-r2-uuid-rework-01` (only `returns_model.py` changed) → separate `mvp6-returns-r2-uuid-ver-01` | **Closed, bounded** |
| Claims stateful | R2 candidate → `mvp6-claims-r2-stateful-rework-01` → separate `mvp6-claims-stateful-independent-ver-02` | **Closed, bounded** |

### Returns evidence

The independent VER reproduced actual model calls: UUID lower/mixed/upper case is value-equivalent for replay and transition; malformed, trimmed and otherwise disallowed lexical forms are rejected; a different UUID returns the recorded correlation-root conflict; changed payload returns the idempotency conflict. The 64-state lifecycle regression remained `64/64`. The supplied verifier recorded `293` passes, `279` refs and `174` examples. The recovered harness was unavailable and is excluded from PASS counts. This is E1/E2 model evidence, not HTTP, Mongo, JWT, restart, outbox, or uptake evidence.

### Claims evidence

The independent VER confirmed separate aggregate, receipt, audit and outbox write stages, distinct failpoint prefixes, full rollback before commit, transition rollback, receipt fingerprint/idempotency behavior, UUID-value root comparison, and unknown-commit/post-commit response loss recovery through the saved receipt. Changed payload is rejected with `409 IDEMPOTENCY_KEY_REUSED`; wrong root takes `409 CLAIM_CORRELATION_MISMATCH` precedence; same-key retry replays the original result without additional writes. The rework author ran `35/35` checks, the independent adversarial suite ran `45` assertions, and the supplied mutation suite rejected `7/7` mutants. The separate verifier's narrower mutation command caught `6/7`; the auth-bypass mutant was caught by the supplied 35-check suite. This is transparent combined coverage, not a claim that either suite individually achieved 100%.

## Coverage disposition

- **Returns existing suite:** 293 verifier passes (with the stated refs/examples), plus the added UUID and 64-lifecycle cases in the independent VER.
- **Claims author suite:** 35/35.
- **Claims added independent adversarial suite:** 45 assertions.
- **Claims mutation suite:** 7/7 mutants rejected; independent narrower mutation run: 6/7, with the remaining auth-bypass mutant covered by the 35-check suite.
- Declaration-level R01–R30 rows, schema labels, and historical counts are not executable evidence. Only the explicitly identified stateful partial rows (R10/R15/R16/R19/R20/R25) gain this bounded model evidence; the remaining rows retain their prior unverified/partial classification.
- `recovered-harness.py` was not runnable and is not counted.

## Exact accepted scope

1. Returns root UUID lexical validation is distinct from UUID value equality for replay, transition and root-before-fingerprint checks; no trim/braces/alternate-format expansion was introduced.
2. Claims receipt identity includes tenant, legal entity, operation, target/create scope and idempotency key; current authorization is checked before replay; fingerprint and root conflicts remain deterministic.
3. Claims model write stages and rollback/unknown-commit semantics are independently exercised, including receipt fingerprint and retry recovery.

## Remaining gates and exclusions

- No real HTTP transport, JWT/RBAC middleware, Mongo transaction/index/concurrency behavior, process restart, durable outbox worker, live publisher, production persistence, or consumer uptake was accepted.
- No contract YAML/annex publication, guard activation, pack promotion, runtime DEV GO, E5/G5, or downstream module GO is granted.
- Unknown-commit evidence is a stateful model selection, not proof of an actual Mongo commit ambiguity.
- Amount/reference/parser/deletion/TTL/event-schema and other R01–R30 rows remain outside this disposition unless separately evidenced.

## CT conclusion

**ACCEPTED for the exact bounded E1/E2 candidate-model rework above.** Any broader runtime or release claim requires a new authorized dispatch and independent verification; no additional test loop is opened by this record.
