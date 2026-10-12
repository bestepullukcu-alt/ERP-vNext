# Fresh execution baseline

- Branch: `feature/mvp6-logistics`
- HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Pre-existing dirty inventory (this owned output excluded) SHA-256: `f5b2c2aee030bb8007d9ff6c1b3bdd754ef81d9d30867d78d06301cea0c5d93f`
- The common checkout contains concurrent work from other lanes and is **not** an admissible transfer target.
- Immutable frontend baseline: Git object tree at the exact HEAD above.
- Accepted Shipment/root authority: bounded MOD-0183 CT decision and Root R2 Producer CT decision.
- Durable backend donor: `mvp6-mod0190-test-oracle-rework-01/source.tar.gz` (`8fa00d40814a81472ec4221ad909321d058dea18a11f64dd2ac16ea800ecb745`).
- Shared predecessor: Carrier target bytes listed in `mvp6-carrier-ui-dev-01/integration/SHARED-TARGET-VERIFICATION.tsv`; they were read and hash-verified, never edited.

The proposed Shipment target is a new registered isolated checkout built from the immutable HEAD, the 40-row backend transfer manifest, and the single-owner Carrier shared predecessor. No checkout was created in this work package.
