---
decision_id: MVP6-CLAIMS-PACK-SIGNOFF-OWNER-DECISION-01
status: approved
decided_at_local: 2026-09-26T01:37+03:00
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner answer "Sign both, in order" in the MVP6 Control Tower conversation (in-app question); record written 2026-09-26T01:32+0300
covers_queue: Q37 and Q31
bound_to: docs/roadmap/plans/mvp6-ui-pack-revisions-01/claims/SIGN-OFF-DECISION.md sha256 c55ab9aaf378b825c0a735a8b992a26b646ed3a2dbcdc88d26daf243bc435de2; revision package SHA256SUMS 6b7d770bb97ec2c49269ad094a0ed1384d536722ebe56924380b3b83d5854d62
---

# MOD-0187 Claims — pack sign-off (alignment + UI revision, in order)

The owner approves applying, by one pack-writer lane, exactly these two patches to
`execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md`, in this order:

1. Alignment: `docs/roadmap/plans/mvp6-pack-alignment-02-claims/proposed-pack.patch` (sha256 00dee2b1fd202f1adc25ca4d3c2aeae532e1bf8d944aca0c68a7fe0417ca5c59) — preimage a342054cddcd88a9fae67f21b51ca90eadcdf355e6119c77c9578bae744b1c1f → f0e4d3bd6af5c16a0d0904721019bfb33f10818a6f9baffd92d3f50740bb1baf.
2. UI revision: `docs/roadmap/plans/mvp6-ui-pack-revisions-01/claims/ui-revision.patch` (sha256 5644479a77db172adf9d24f5a7031eed82c4eb82067d326403a37058270dae92) — f0e4d3bd… → 762ab533… (full target hash as recorded in `docs/roadmap/plans/mvp6-ui-pack-revisions-01/claims/SOP-22-PACK-REVISION.md`).

Apply only if every preimage and target hash matches; otherwise STOP with the pack unchanged. Status stays `ready-for-dev`.
The open items listed in the revision (approved amount absent from list data, transition modal fit, field icons in a shared test file,
date-time with offset, generic DataTable verifier vs approved OUT rows) remain open items for the UI writer and integration owner.

Not authorized: UI code (until an integrated target exists), gateway, permission, navigation or localization edits (single integration
owner only), contract changes, other packs, commit, push.
