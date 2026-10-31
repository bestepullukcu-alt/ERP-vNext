# MVP-6 — start the Q503 and Q502 code locally before their gates are on `main` (owner decision)

- owner: **Natig** (K1, management) — decided in chat 2026-10-29, recorded by the MVP-6 Control Tower
- scope: Q503 (MOD-0186 DL-1, pack r8 `e96a9add…`) and Q502 (MOD-0147 DL-1, pack `f1c951bc…`)

## Decision

1. **Q503 — MOD-0186 DL-1.** The code worker starts before the queue row (PR #303) is on `main`. The pack is
   ready-for-dev with its CT-APR-01 HIGH row on `main` (#269), and its dependency Q500 (MOD-0183 DL-1) is on `main` (#294).
2. **Q502 — MOD-0147 DL-1.** The code worker starts before SUPPLIER 1.3.0 is frozen on `main` (§0.0 code condition 3). It
   mocks Procurement against the 1.3.0 text on `origin/feature/mvp2/supplier-dl1-contract-130` @ `cfd917df3`
   (`x-producer-status FROZEN`, MOD-0140 DL-1, not yet on `main`). The risk of rework if the contract changes is accepted.

## Limits

- Local branches and local commits only. No push and no PR until the gate is on `main`:
  - Q503: after PR #303 is merged;
  - Q502: after SUPPLIER 1.3.0 is on `main` and the CT has re-measured the file's sha256 and the `SupplierEligibility`
    schema against the text the worker mocked; any difference is fixed before the PR.
- Nothing else changes: the packs are followed as written, CT-APR-01 rows stay as they are, an independent review runs
  before each PR, and the MVP-1 live tours remain the acceptance.
