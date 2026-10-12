# Exact RED→GREEN plan

## R14 — shared transport decoding

**RED:** raw HTTP/1 request with `Idempotency-Key` containing 128 UTF-8 encoded `é` scalars (`c3 a9` repeated) is rejected before Claims middleware under the current Program baseline. Record request bytes, bare response and absence of application correlation.

**GREEN after exact authorization:** apply `R14-program-candidate.patch`; 128 decoded scalars reaches Claims and succeeds, 129 reaches Claims and returns the contract 400 envelope. Repeat with one-byte ASCII, comma data, 1/128/129, four-byte Unicode scalars, malformed UTF-8 and duplicate field lines. Verify all non-Idempotency headers retain the default decoder. Run Shipment/POD, Carrier, Loads, Returns and Claims header regressions because the selector is shared by header name.

## R21 — Claims-owned root/trace separation

**RED produced:** set inbound Claims correlation/business root to nil and assert the outbound Shipment GET trace is non-nil. Baseline reader failed.

**GREEN produced:** target reader generates a dependency-only non-nil UUID; returned authoritative Shipment root remains nil. Targeted test 1/1 and ClaimReferenceTests 28/28 passed.

**Required E4 GREEN after application:** seed a real Shipment with persisted nil lifecycle root; call Claims create with nil root; capture outbound Shipment GET with non-nil trace, producer 200 body with nil root, Claims 201, stored Claim/audit/outbox root nil, and response trace nil. Repeat missing/null/malformed/different-root cases to prove no relaxation.

## R22 — definite number collision

1. Start the same fresh binary in `ClaimsEvidence` with an explicit fixed UUID.
2. In a fresh tenant/LE, pre-seed a soft-deleted Claim having a different `_id` but ClaimNumber `CLM-` plus the fixed lowercase UUID-N.
3. POST create through authenticated HTTP.
4. Expect 503 `CLAIM_STORAGE_UNAVAILABLE`, exact correlation parity, zero receipt/audit/outbox delta, unchanged Claim count, and no alternate ClaimNumber.
5. Negative controls: Production environment ignores evidence keys and uses `NoOpClaimCommitProbe`; malformed/missing evidence configuration fails startup in `ClaimsEvidence`.

## R25 — stage faults and unknown commit

For `aggregate`, `receipt`, `audit`, `outbox`, and `beforeCommit`, start an isolated `ClaimsEvidence` process with exactly one fault stage. Require ordered stage logs through the selected stage, 503, and independently queried 0/0/0/0 after abort. `afterCommit` must show 1/1/1/1 and receipt recovery; never call it a precommit rollback.

Unknown commit is a separate Mongo scenario. Configure `failCommand` for `commitTransaction` with `UnknownTransactionCommitResult`, run one create and record the actual DB result without assuming zero. Stop the process, disable the failpoint, restart the same binary/DB, retry the same key, and require one final Claim/receipt/audit/outbox group with no blind duplicate. This result must remain distinct from the probe's `afterCommit` response-loss scenario.
