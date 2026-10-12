# Single integration-owner handoff

## Immutable inputs

1. Materialized BC successor source manifest must be exactly `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634`. Its materialization requires a separate final target-bound owner authorization; this package does not provide it.
2. Every baseline in `candidates/BASELINE-PATCH-TARGET.tsv` must match. `ABSENT` means the path must not exist.
3. Combined patch must be `3fdc9188f635a2428e3ffc5c4ef07c9c4b747b2f74a4196d8e6207cd85ccf1d5`.
4. The UI pack target must already be explicitly approved/applied as `28019ebe9fd6e34ca719d244cc0129fe2046ee0f692a9ffd6a5a6eed16028f3f` before UI dispatch.

## Application order after authority

1. Create/select one registered integration checkout and materialize the separately authorized BC successor without overlaying current common-checkout files.
2. Recheck all 18 preimages. Any mismatch is a conflict and stops application.
3. Apply the combined candidate once: Gateway route → SupplyChain registration → seven navigation resources.
4. Verify all 18 target hashes, JSON/XML parse, exact path inventory, and preservation of `_LayoutTenantShell.cshtml`, non-Carrier routes, non-Carrier manifests, and unrelated resource rows.
5. Restore/build and run Carrier manifest/registration tests plus `NavManifestL10nGuardTests`. Configure only the existing non-MDM `X-Internal-Api-Key` registration credential in the disposable runtime.
6. Verify manifest reconciliation/restart, read-gated sidebar/Ctrl+K, and exact gateway method/path behavior. Produce the immutable integration handoff consumed by UI DEV and VER.

The integration owner does not write any of the 21 UI-owned files. The UI writer does not change these 18 shared/integration paths. No implicit resolution, new permission, layout edit, manual catalog seed, generic gateway verb widening, or direct 5061 browser route is allowed.
