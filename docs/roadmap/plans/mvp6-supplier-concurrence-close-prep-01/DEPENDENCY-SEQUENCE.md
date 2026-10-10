# Post-concurrence dependency sequence

The policy selection is closed and is not part of this sequence.

## Wave 1 — owner decisions, parallel where independent

- DC-01 ownership may be decided independently.
- DC-02 MOD-0140 behavior/LE ownership and DC-03 security binding/revocation may be reviewed in parallel, but the resulting seam/carrier proposal requires both.
- DC-04 Metric Registry and DC-05 Risk taxonomy/source decisions may be reviewed in parallel. DC-05 score bands require Metric owner concurrence recorded with DC-04 or explicitly alongside DC-05.

No implementation, contract candidate or fixture result is needed to record these owner decisions.

## Wave 2 — exact owner artifacts

After the relevant concurrence:

1. **Governance lane:** from DC-01, prepare one exact proposed domain-config/DCP/registry alignment. Applying it remains separately authorized.
2. **Supplier/security producer lanes:** MOD-0140 and security each prepare their owned live-seam artifact. They may work in parallel, but must expose one compatible Tenant+LE boundary and revocation rule to CT.
3. **Metric/Risk producer lanes:** Metric and Risk owners prepare exact immutable-revision/pointer artifacts; the MOD-0148 portal-source contribution is supplied once, without creating a second contract writer.
4. **Fixture lane, optional:** prepare `supplier-exact-policy-fixture/1` only after the applicable semantics are owner-bound. All outputs remain `SIMULATED`; this lane never gates or proves live producer readiness.

## Wave 3 — one CT contract disposition

The Central Control Tower Supplier Seam Owner consumes the exact Wave 2 artifacts and decides whether SUPPLIER, SUPPLIER-PERFORMANCE or both require a successor. If required, one writer prepares one exact candidate/version/compatibility package covering required/null repair, LE/binding carriers, declared errors, correlation and revision/source fields.

Strict/external consumer inventory and exact consent bind to that future artifact. Repository search alone cannot close external inventory. No module lane creates a competing amendment.

## Wave 4 — module packs, in parallel

After governance placement and all dependencies needed by each module are exact:

- MOD-0147 owner may prepare one pack amendment incorporating the accepted Supplier, Metric and Risk artifacts plus exact owned paths and Phase 1.5.
- MOD-0148 owner may prepare one pack amendment incorporating the accepted Supplier/security artifacts plus exact owned paths and Phase 1.5.

These pack preparations can run in parallel because their module-owned paths are disjoint. Both packs remain `draft` until their own explicit promotion decisions.

## Wave 5 — future implementation gates

Only after exact publication/consumer consent where required, pack promotion, technical Phase 1.5 and runtime authorization may isolated DEV/VER begin. Shared composition, permissions and gateway remain separate single-writer integration work.

There is no DEV GO in this package.
