# MVP6-ROOT-EXACT-ARTIFACT-RECOVERY-01

## Verdict

**EXACT RECOVERY: NOT FOUND.** No accessible YAML or patch matched the approved target SHA-256 `91d505c900ce214262fd35ecb6bad9f005b4bbc42f1680d9db7cc1932bf0fbc9`.

The current canonical YAML remains the exact 2.0.0 baseline at `docs/analysis/contracts/shipment-bundle.openapi.yaml` (SHA-256 `93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571`). The previously recorded target and publication patch hashes are references only; their bytes are not present in the accessible repository/audit artifacts searched for this recovery.

## Concrete candidates checked

- `docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1/shipment-bundle.openapi.candidate.yaml` — `05a7ad0c…d8034`, mismatch.
- `docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0/shipment-bundle.openapi.candidate.yaml` — `c650bf44…e8ee1`, mismatch.
- `docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1/publication-after-approval.patch` — `9f96389b…4cd88`, not the recorded exact patch.
- `docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0/publication-after-approval.patch` — `2cbcf65b…920f`, not the recorded exact patch.
- `docs/records/audits/2026-09/mvp6-loads-r2-publication-2026-09-18/publication.patch` — `3533d85a…4426ac`, unrelated Loads publication patch.

The exact recorded publication patch hash `0ef69539bc35a7be2b7e97f87558690262138d55711e9394ae09e5bc33b64b72` and target YAML bytes were unavailable. No approximate reconstruction was attempted.

## Recovery boundary

Because the exact bytes are missing, no disposable patch application can produce a byte-identical target, and no active Producer DEV lane YAML path can be supplied. Existing runtime/publication approval is not transferred to a reconstructed candidate. A new candidate would require a separately versioned disposition and new exact owner decision.

No canonical contract, runtime, guard, authority, historical file, or git state was changed. Hash inventory is preserved in the companion recovery output under `/private/tmp/mvp6-root-exact-artifact-recovery-01/SHA256SUMS`.
