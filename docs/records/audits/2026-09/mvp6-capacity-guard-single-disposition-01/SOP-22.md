# MVP6-CAPACITY-GUARD-SINGLE-DISPOSITION-01 — SOP §22

## Verdict

**CANDIDATE PASS; production activation HELD for one exact owner decision.** The only production guard failure is correctly classified as one immutable `historical-data` JSON. Existing reader/schema/policy already supports the disposition, so the narrow repair is one exact seal and a new payload-bound decision. No v3 release decision is reopened.

## Baseline and authority

- Branch: `feature/mvp6-logistics`; HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- The working tree was already dirty with 238 status rows at entry. This lane preserved ambient work and only wrote this owned output directory.
- Current authority SHA-256: `063e0cde7c436cf0926101ac9a300b9b1e6147dd5bea5c7dbfd100e65a868fe9`.
- Reader SHA-256: `da5ec0cc0f180d7237bdf599c7d71eb1fb96962ed14496427a1ea789b18c5050`; docs policy SHA-256: `0eaff863249654ff2dbe1990ebdcae0f610b0f3ee2bf31f197d5d8b5a744e2a9`.
- The v3 owner decision SHA-256 is `80c343e6ad07c053c69aa1c9de003396bf2fd347379bf72e734264caef42f91b`; it grants no guard mutation or activation, so it cannot authorize this new payload.

## Measured production failure and provenance

The preflight raw TRX `pre-publication-authorized.trx` SHA-256 `08b9a1aba31dd242eeb3a20b2ba870d3d8034dc037a207787c8454affeb9065f` reports 38 PASS / 1 FAIL. Its only offender is:

- `input-bindings-recomputed.json:46` → the then-published SANDOP YAML path;
- `input-bindings-recomputed.json:53` → the then-published SANDOP annex path.

The file SHA-256 matches the required `edfeaace44b96c2699e62da3bd8fe9f16b6f836e5ef5d1988ced3f657b43d21a`. It was emitted by the completed independent verifier at `verify.py:170-171`; verifier source SHA-256 `52f5a02082e6e26da661348d5c4fca0e7f6060ee1517e8ee64db0cd69530c9b2` and its SOP SHA-256 `8d1eb5b91612f86d2b3c1620eb4cc4e1b5b155b9b58710bafd073c7a387352f2`. The JSON is immutable recomputed evidence data. It is not an executable tool and has no canonical target responsibility. `historical-data` with `targets:[]` is the exact existing type.

## Exact candidate

| Artifact | SHA-256 / result |
|---|---|
| Authority baseline | `063e0cde7c436cf0926101ac9a300b9b1e6147dd5bea5c7dbfd100e65a868fe9` |
| UNAPPROVED authority candidate | `3a6dc437fcf5f0b5679ce9b42c2fc10e56b0efd10c5341be417994d6da11f193` |
| Candidate patch | `5a508276323c3f8195579dc4aeb7d14565612c899b898a20bb3fb9a2bb7a9f24` |
| Reader payload | `3bd20e2608ec62cd5c2da1fc60bc4419b6aefa594e0efc45f32b35df17ad8dfb` |
| Provenance | `401174ea2ff6e12861a9f2e375f5990f42bd879623ecc20b8c83e21f93f2ceda` |
| UNAPPROVED decision template | `6904a6b0f7b971645ac833602682e2d8342600c04e9b8fd73ed698352f3a8a63` |

The candidate changes `APPROVED` to `UNAPPROVED`, clears the prior decision reference, and appends exactly one 35th seal. This prevents accidental activation under the old decision. `PRESERVATION.tsv` proves both canonical targets and the previous 34 seals remain byte-equivalent. There is no policy, schema, reader, canonical target or historical input change.

## Verification

- Patch apply check: PASS; disposable applied authority is byte-identical to the candidate.
- Actual `DocsPathGuardTests` against a disposable, structurally approved copy: **39/39 PASS**.
- Positive path: the new exact seal is consumed at both offending lines with real reader payload hashing.
- Negative path: real reader tests reject wrong/missing hashes, changed history, invalid provenance, unapproved/missing decision, tampered payload, unknown path/kind, wildcard/traversal and other fail-closed mutants.
- Candidate production boundary: repository authority was not changed; synthetic disposable approval is not owner consent.

Details and the raw TRX are in `VALIDATION.md` and `DISPOSABLE-DOCS-PATH-GUARD.trx`.

## Scope preservation and remaining gate

All new records use `.md`, `.txt`, `.tsv`, `.patch` or `.trx`; no new scanned `.json` or `.py` artifact is introduced by this package. The original JSON and all prior authority inputs remain unchanged. Canonical publication, runtime, pack promotion and git mutation were not performed.

The only remaining action is the narrow decision in `OWNER-DECISION-TEXT.md`. After real approval, one guard owner must create the actual decision record, bind its file hash into an approved authority, recheck the exact baseline/input/provenance hashes, and run the production-mode reader plus full DocsPathGuard on the real checkout. Until those steps pass, activation remains **HELD**.
