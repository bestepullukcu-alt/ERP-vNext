---
decision_id: MVP6-RETURNS-CLAIMS-UI-SCOPE-OWNER-DECISION-01
status: approved
decided_at_local: 2026-09-26T01:24+03:00
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner answer "Approve both as drafted" in the MVP6 Control Tower conversation (in-app question), 2026-09-26; scope preferences (full scope, supplychain.shipments.read grant) first given in the lane chat and confirmed here
bound_to: docs/roadmap/plans/mvp6-ui-pack-drafts-01/SHA256SUMS sha256 b568c94f7f524dd0bff1e2fa3277571fed174c6214ac0a9efd3ab13200abb57f; returns/APPROVAL-DECISION.md edb2b06df9d3c67958eade30dd16d826173455d45e5a2ffbdb773bb668d2b3b3; claims/APPROVAL-DECISION.md ea11f670af31355e67e939c219c8d27142977c2ea79c09398efded4a47bf4813
---

# MOD-0186 Returns and MOD-0187 Claims — UI scope approval (drafts)

The owner approves the UI **scope** of both drafts in `docs/roadmap/plans/mvp6-ui-pack-drafts-01/` exactly as drafted:
in-pack UI revisions (no new module IDs), Golden Reference Slim, list + create panel + status-change (transition) screens,
no detail page, the additional `supplychain.shipments.read` grant for users who create or transition, and the explicitly
out-of-scope items (bulk, edit, delete, import/export, server paging, and the rest listed as OUT in each ACCEPTANCE.md).

## What this approves and what not

- Approves preparing the exact pack-revision patches for both packs from these drafts (next CT dispatch).
- Does **not** yet change the packs, promote status, authorize UI code, gateway, permission, navigation or localization changes,
  or any runtime work. The pack revisions need the owner's final sign-off; UI development needs an integrated target (Q14/Q15).
- The Claims approved-amount list gap stays a recorded contract gap; no contract change is authorized.

## Revision history

- r1 2026-09-26 01:35 (CT, administrative correction, no content change): `decided_at_local` corrected from 01:30 to 01:24 — the owner answered at about 01:24 and this record was written at 01:24 (file time 22:24Z); 01:30 was a CT estimate. Noted by lane Q35.
