# MVP6-CAPACITY-GUARD-SINGLE-ACTIVATION-01 — SOP §22

## Verdict

**ACTIVATED / PASS.** The exact owner-approved historical JSON seal is bound to a real `DOCS_PATH_OWNER_DECISION`; disposable production-mode and actual-checkout full DocsPathGuard both pass 39/39.

## Authority chain

| Item | SHA-256 |
|---|---|
| Authority preimage | `063e0cde7c436cf0926101ac9a300b9b1e6147dd5bea5c7dbfd100e65a868fe9` |
| Owner-approved disposition patch | `5a508276323c3f8195579dc4aeb7d14565612c899b898a20bb3fb9a2bb7a9f24` |
| Reader payload | `3bd20e2608ec62cd5c2da1fc60bc4419b6aefa594e0efc45f32b35df17ad8dfb` |
| Provenance | `401174ea2ff6e12861a9f2e375f5990f42bd879623ecc20b8c83e21f93f2ceda` |
| Real owner decision | `877c3878d9b38446fc437a384d5e61630ccbec601dc5dd025a536ea21aa20ae2` |
| Final activation diff | `24b8420c3e6ff963afde087edfb4bb8fff8af1ff8cc0ecc7e6d911f9fbd27b15` |
| Final approved authority | `6d3865f80707ff0e9ca7c9cf2e2343f34d2961be2916a3eceb37168d1b0a8167` |

Decision path: `docs/records/decisions/2026-09/mvp6-capacity-guard-single-disposition-owner-decision-01.json`. The decision records the current-role user message as accountable approval and binds the exact payload.

## Applied scope

Only `docs/reference/architecture/docs-path-authority.json` and the new owner decision were applied. The authority retains the two existing canonical targets and the previous 34 seals, then appends the exact 35th `historical-data` seal for:

`docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01/raw/input-bindings-recomputed.json`

Its SHA-256 remains `edfeaace44b96c2699e62da3bd8fe9f16b6f836e5ef5d1988ced3f657b43d21a`. Policy, schema and reader bytes remain unchanged: reader `da5ec0cc0f180d7237bdf599c7d71eb1fb96962ed14496427a1ea789b18c5050`; policy `0eaff863249654ff2dbe1990ebdcae0f610b0f3ee2bf31f197d5d8b5a744e2a9`.

## Verification

1. Authority preimage and all approved input hashes matched.
2. The final authority was tested first in a disposable checkout with the real decision: 39 passed, 0 failed. TRX SHA-256 `96ea37536c4d53273abf506bb7f36af1301119da7e107ce13782a545a4caba2c`.
3. The final activation patch applied cleanly to the still-matching real checkout baseline.
4. Production-mode full `DocsPathGuardTests` on the actual checkout: 39 passed, 0 failed. TRX SHA-256 `460d32ee665ded6bde395f225d8f9093517a32eb488013f3bffd87ab0bc583a8`.
5. Negative cases remained active, including invalid/missing decision, payload tampering, wrong source/provenance hash, changed history, wildcard and unknown path rejection.

## Boundaries

No policy/schema/reader change, historical JSON rewrite, canonical publication, pack promotion, runtime, gateway/UI/shared permission, rollout, commit, push or stash was performed. This activation does not grant or imply those authorities.
