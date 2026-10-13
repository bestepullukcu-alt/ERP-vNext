# MVP6-PACK-ALIGNMENT-03-RETURNS — MOD-0186 pack alignment + UI revision (CT queue Q36)

**PREPARED PROPOSAL — NOT APPROVED.** Documents only. It replaces the non-applicable pseudo-patch in `docs/roadmap/plans/mvp6-final-pack-delta-01/`
(unchanged) as the Returns promotion path, and adds the Returns UI revision the Q35 lane could not prepare. Pattern:
`mvp6-pack-alignment-02-claims/` (alignment) and `mvp6-ui-pack-revisions-01/claims/` (UI revision). CT basis:
`docs/records/audits/2026-09/mvp6-ct-disposition-q08-q17-2026-09-26.md`, addendum 01:36.

- Repo / branch / HEAD: `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Start 2026-09-26T01:33:38+03:00 (Europe/Istanbul).

## Result

| Item | Value |
|---|---|
| Shared pack (preimage) | `07a8a0159b5ab02fecc7b1aa6314106d81b1584150169f457cb9a395614a04b7`, `draft` |
| Owner-promoted isolated pack | `1c80cca70ef774d18f5d30174566e68c9b6332b61dd1f564d54de3429886482f`, `ready-for-dev`; taken byte-exact from three archived copies in the Returns evidence records |
| Alignment target | `f4396e8a03f663e34526e3bdaa4c85a4f3c96683ae73bae608f2981e87b33b1f` (`alignment.patch` `8af3287c…8c3b`), `ready-for-dev` |
| UI revision target | `6c8fbe28f0b058cacf08aaf3941e82845b0842db3f8b8f295a753e746a65a5a0` (`ui-revision.patch` `62c7b164…9d63`), `ready-for-dev` |
| Controlling acceptance | `mvp6-mod0186-wp-acceptance-01` (ACCEPTED, bounded isolated WP) |
| Contracts | SHIPMENT-BUNDLE `5dfe7c1b…`, Returns annex `00990a28…`, root annex `7d1327a1…`; identical to the acceptance pins |
| Patch checks | Both patches: `git apply --check` and `patch --fuzz=0` OK, byte-equal; order A→B proven; UI patch fails on the unaligned pack; DCP-002 OK |

Remaining gaps are listed in `SOP-22-PACK-DELTA.md`: the missing original Phase 1.5 package (`mvp6-mod0186-phase15-close-01/`), the owned-path list taken from `returns46.json`, stale body text handled by a precedence clause, the UI gaps shared with Claims, the integration target, inherited open items, and a stale decision-record hash cited by the Q35 Claims patch.

## Files

| File | Purpose |
|---|---|
| `SOP-22-PACK-DELTA.md` | Lineage, patch content, checks with output, gaps |
| `alignment.patch` | Unified patch shared pack → aligned pack (header lines start with `#`) |
| `ui-revision.patch` | Unified patch aligned pack → UI-revised pack (header lines start with `#`) |
| `SIGN-OFF-DECISION.md` | Exact owner decision text binding both patches in order — NOT APPROVED — with options |
| `SHA256SUMS` | Checksums of the five files above (paths relative to this directory) |
