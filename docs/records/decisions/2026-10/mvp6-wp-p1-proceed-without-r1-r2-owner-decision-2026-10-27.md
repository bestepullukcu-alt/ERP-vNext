# MVP-6 WP-P1 — proceed without the MVP-1 CT's R-1 / R-2 answers (owner decision)

- owner: **Natig** (K1, management) — decided in chat 2026-10-27, recorded by the MVP-6 Control Tower
- scope: MOD-0183 §23 (r6, `07152235…`) and MOD-0186 §34 (r8, `e96a9add…`), PR #262

## Decision

DL-1 work for MOD-0183 and then MOD-0186 does not wait for the MVP-1 CT's answers to R-1 (REVERSAL bodies, incl. the
reversal of a `CUSTOMER_RETURN` out of `QUARANTINE`) and R-2 (`ALREADY_REVERSED`). The packs are followed as written:

- REVERSAL bodies as MOD-0183 §23.4 and MOD-0186 §34.2;
- `ALREADY_REVERSED` on our REVERSAL counts as reversed and is logged as an integrity fault (MOD-0183 §23.4; MOD-0186 plan r3
  §5 point 2 applies the same rule to the late REVERSAL).

## What this does not change

- Code still starts only after the CT-APR-01 rows are on `main` (AGENTS Core 7); MOD-0186 DL-1 also after MOD-0183 DL-1.
- The MVP-1 live tours remain the acceptance. If an R-1 / R-2 answer differs from the packs, it is handled as a new pack
  revision (CT-APR-02 + CT-APR-01) before the affected tests are changed.
