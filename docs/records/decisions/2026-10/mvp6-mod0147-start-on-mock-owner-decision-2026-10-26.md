# MOD-0147 v1 — DL-1 code may start on a mock of SUPPLIER 1.3.0 (owner decision, 2026-10-26)

- owner: **Natig** (K1, management) — decided in the MVP-6 Control Tower chat 2026-10-26 (question box), recorded by the CT
- status: **ACCEPTED**
- module: MOD-0147 Supplier Performance & Risk (pack §0.0, PR #236)

## Decision

DL-1 code may be written, tested and merged once the revision's CT-APR-01 row is on `main`, the PR #198 audit path is on
`main` and the SUPPLIER 1.3.0 contract is frozen on `main` (MOD-0140 Revision 2 DL-1). Tests use a mock built from the frozen
contract. Enabling MOD-0147 for users (Gateway routes, role permissions) still waits for MOD-0140 DL-3 on `main` and the live
Gateway run of P2.

## Why it is safe

Before MOD-0140 DL-3 the eligibility endpoint answers a route 404, which MOD-0147 §0.0 maps to 503 `SUPPLIER_BASE_UNAVAILABLE`;
create fails closed and writes nothing.

## Same session, not changed

- PR #236's CT-APR-01 rows stay with Beste.
- MOD-0183 / MOD-0186 r4 is reviewed now by a fresh independent session (CT-APR-02); the MVP-1 CT still gets r4 for R-1 / R-2.
- The MOD-0183 / MOD-0186 approval PR follows the agreed plan (Natig merges `origin/main` into `-dl1`, the CT opens the PR).

This record authorizes no code by itself.
