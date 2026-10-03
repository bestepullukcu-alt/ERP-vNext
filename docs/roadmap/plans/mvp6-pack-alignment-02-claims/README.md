# MVP6-PACK-ALIGNMENT-02-CLAIMS — MOD-0187 pack delta refresh (CT queue Q29)

**PREPARED PROPOSAL — NOT APPROVED.** Documents only. It is the successor to the stale `docs/roadmap/plans/mod-0187-final-pack-delta-01/`, which is unchanged. CT basis: `docs/records/audits/2026-09/mvp6-ct-disposition-q08-q17-2026-09-26.md`; pattern: `docs/roadmap/plans/mvp6-pack-alignment-01/`.

- Repo / branch / HEAD: `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. The working tree was already dirty (353 status rows at start); other lanes' changes are not attributed here.
- Start 2026-09-26T00:21:47+03:00 (Europe/Istanbul).

## Result

| Item | Value |
|---|---|
| Shared pack (preimage) | `a342054cddcd88a9fae67f21b51ca90eadcdf355e6119c77c9578bae744b1c1f`, `draft` |
| Owner-promoted isolated pack | `d035d42059141f17edb90bb97183892a3c98c171a16770795ce2557f834ac9b0`, `ready-for-dev`; reconstructed exactly from the approved activation patch |
| Proposed target | `f0e4d3bd6af5c16a0d0904721019bfb33f10818a6f9baffd92d3f50740bb1baf`, `ready-for-dev` (never `done`) |
| Controlling acceptance | `mvp6-mod0187-ct-accept-01` (ACCEPTED, bounded isolated WP) |
| Contracts | SHIPMENT-BUNDLE `5dfe7c1b…`, Claims annex `16e65c26…`, root annex `7d1327a1…`; identical to the acceptance pins, so no Claims drift |
| Owned paths | 47, hash-bound to the accepted 341-entry manifest; 0 present in the common checkout |
| Patch checks | `git apply --check` and `patch --fuzz=0` OK; byte-equal target; DCP-002 OK |

Remaining gaps are listed honestly in `SOP-22-PACK-DELTA.md`: an R14 authority hash typo in an existing record, an un-retraced composition hop, a 44-vs-47 absent-path count, the Loads 3.1.0 forward drift risk, and stale historical body text handled by a precedence clause.

## Files

| File | Purpose |
|---|---|
| `SOP-22-PACK-DELTA.md` | Lineage, patch content, Phase 1.5 delta, gaps, checks |
| `proposed-pack.patch` | Unified patch preimage → target (header lines start with `#`) |
| `owned-paths.md` | 47 owned paths with accepted SHA-256, protected paths, `Program.cs` slot |
| `PROMOTION-DECISION.md` | Exact owner decision text — NOT APPROVED — with options |
| `SHA256SUMS` | Repo-root-relative checksums of the five files above |
