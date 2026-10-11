# MVP6-INTEGRATION-BASELINE-SELECTION-01 — SOP §22

**Verdict: PREPARED / HELD.** One exact, provenance-based 422-path integration selection is reviewable. No existing checkout equals it byte-for-byte, and this turn authorizes no source transfer, implementation, test run or shared-file mutation.

## Identity and preflight

- Repository: `/Users/natig/Projects/ERP-vNext-recovery`
- Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Risk/evidence: HIGH, inspection/preparation only
- Initial observed checkout was already dirty/untracked. The count changed concurrently from 238 to 240 while read-only inspection was in progress; therefore this report makes no repo-wide no-change assertion. It binds only its owned output and the inspected hashes.
- CT SOP §16 and §27 were applied: dependency provenance, parallel ownership, single shared writer, exact revision binding and isolated-versus-integrated evidence boundaries remain explicit.

## Decision

Select the accepted final MOD-0190 379 archive as the composite base for MOD-0183–0187 plus MOD-0190. Overlay the disjoint accepted Capacity 43 archive, then apply the exact Program composition patch and the accepted hosted Capacity test-only patch. This yields 422 unique paths and deterministic manifest SHA-256 `cdc6228aa3d215f34f2f2f9d5b784cf57ac20b6eb8f2c2f26bdcace9dcd4d76c`.

Do not select the historical hosted 422 bytes verbatim: they predate the accepted MOD-0190 `SandopAtomicityTests.cs` successor. Do not select the later MOD-0192 duplicate-name candidate: its v3 publication and production uptake are HELD. `SOURCE-SELECTION.md` records each module's controlling CT decision, promoted pack and source provenance.

## Current checkout/worktree disposition

The common checkout is 247 identical / 174 missing / 1 conflict against the selected 422. The sole existing conflict is `Program.cs` (`7fdb5ef0…` versus selected `50c48a2…`); all Returns, Claims, S&OP and Capacity owned paths account for the 174 missing entries. The closest registered Capacity integration worktree is 420 identical / 0 missing / 2 conflict: both S&OP and Capacity atomicity test files are stale relative to accepted successors. Other registered worktrees are donors/evidence only. Prunable paths that no longer exist are not sources.

The MOD-0190 38-path and MOD-0192 43-path owned sets have zero intersection. Returns 46 and Claims 47 are likewise disjoint. `Program.cs` is the only selected shared product source and has exactly one future integration owner. No concrete shared-permission or gateway diff is selected; those paths remain protected and require their own owner if later evidence identifies a need.

## Authority disposition

Existing isolated development and hosted evidence decisions establish provenance and acceptance boundaries; they do not automatically authorize copying the same bytes into a new target checkout. The earlier Capacity transfer/Program decision was exercised by its named hosted lane. A future integration run therefore needs the exact target-bound decision embedded in `INTEGRATION-DISPATCH-v1.0-HELD.md`. There is no current authority for the duplicate-name successor, gateway, shared permissions or pack status changes.

## Verification performed and evidence boundary

Read-only checks verified the base HEAD, archive/manifest/patch hashes, 379/43 zero overlap, deterministic selected-manifest digest, module-owned intersections, current checkout/worktree file states and successor exclusion. No restore, build, test, process, HTTP, Mongo, patch application, source copy, branch, staging, commit, push or stash occurred. Consequently this is source-selection readiness, not integrated PASS.

## Handoff

- Exact selection recipe: `SELECTED-SOURCE.json`
- Per-module provenance: `SOURCE-SELECTION.md`
- Checkout/worktree conflicts: `CONFLICTS.tsv`
- Authority matrix: `AUTHORITY.tsv`
- Ordered future transfer and minimum regression: `TRANSFER-ORDER.md`
- Copyable dispatch and exact release decision: `INTEGRATION-DISPATCH-v1.0-HELD.md`

**Remaining gate:** one new owner message must bind the exact target checkout and the listed hashes, then a single integration writer may execute the HELD dispatch. Integrated verification remains a subsequent independent lane.
