# MVP6-CLAIMS-R2-INDEPENDENT-VER-01 — SOP §22

Verdict: PARTIAL. Fresh verification passes artifact integrity and bounded executable checks; complete behavioral acceptance is not established. Same conversation/agent authored the candidate; this is a separate fresh verification execution and separate oracle, NOT independent-writer sign-off. A different verifier is still required if CT requires author separation.

## Authority / input integrity
Actual role=user D187 approval matches archived owner-approval.txt exactly (session line1151); design/candidate preparation only, no release consent. Archive SHA and all24 internal manifest entries verified before and after reruns. Exact patch applied with zero fuzz produces candidate bytes. Canonical baseline remains93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571. Current branch/HEAD/status captured separately. Existing dirty/concurrent work not adopted or changed.

## Executions
- Recovered immutable model:40/40; preserved hash c8b3c3bbd2a0f17558f1b1811a65aae9823f760a9cd17ac662a2aa75d4f10e57.
- Candidate author harness:145/145.
- Separate verification script:127/127. Canonical transition description parsed into seven legal edges; all49 source/target fixtures and actual adapter outputs compared:7 legal,42 rejected. All local refs resolve; operation delta restricted to description/responses; shared components, non-Claims operations, metadata and webhooks identical.
- Seven policy mutation witnesses pass. They cover withdrawn permission, lifecycle status, root precedence, lexical amount, retention, zero approval, worker flag. Worker-flag mutation is not a process test.
- Ten root helper cases and in-memory state cases rerun. Root R2 referenced artifact hash matches dab5a2f9974283e2404a29a7fb8ed07a77ae8ed843929b1c7b4546c5615dd7cb. Producer patch absent; ShipmentDetail unchanged. This binds an observed dependency artifact, not publication/uptake or independently accepted Root R2 handoff.

## Exact findings
F01 — Evidence coverage: recovered40 includes14 unconditional annex/process True records plus simplified/constant witnesses. New145 includes static policy equality checks and schema validation. Neither count equals the number of independently exercised business behaviors. Per-row classifications supplied for all R01–R30.
F02 — Integrated replay not proved: verify-r2.py Store.create returns saved201 on same tuple with changed amount999 after original1. Its store does not call root/fingerprint/current authorization. Those helpers have separate tests; full R15/R16/R19/R20 stateful replay remains unverified. This is a test-model limitation, not a runtime defect assertion.
F03 — Distinct failpoints not proved: fail indices0..3 all enter the same unconditional post-write rollback branch; no four separate write boundary injection. Unknown commit model directly reads an in-memory receipt; real uncertain persistence/CAS/restart is not represented.
F04 — Reference/parser/decimal capacity/number collision/event policies mostly static; R01–05,08–09,13–14,18,22,24,26–30 do not gain full behavioral proof from BOUND/POLICY/SCHEMA/hash checks. Some partial checks exist but do not cover the exact decision acceptance. Root missing/null both use None; no parsing distinction verified.
F05 — Verification ownership: candidate and this verification share author identity. Do not label this independent-agent approval.

Required follow-up: separate verifier reviews this exact package; strengthen integrated candidate-model evidence for replay/authorization/scope and distinct failpoints before claiming those decisions behavior-tested. Preserve recovered model; added tests must be separately identified. No source changes were made during VER.

## Preserved exact policies
Withdrawn=investigate; lifecycle422 INVALID_CLAIM_TRANSITION; payload409 IDEMPOTENCY_KEY_REUSED; root409 CLAIM_CORRELATION_MISMATCH verified in candidate profile and examples. Worker/publisher false and Pending outbox are declarations only. No finance/posting authority.

## Boundaries
No runtime, operational DB, persistence, HTTP/JWT, producer uptake, publication, release consent, Phase1.5, pack promotion or DEV GO. No canonical/runtime/guard/pack/git change. New durable audit report, exact manifest and evidence archive only. Evidence levels E1/E2 candidate models; limitations explicit.
