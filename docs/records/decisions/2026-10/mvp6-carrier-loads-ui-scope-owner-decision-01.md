---
decision_id: MVP6-CARRIER-LOADS-UI-SCOPE-OWNER-DECISION-01
status: approved
decided_at_local: 2026-10-04T21:00+03:00
decided_by: repository owner
decision_source: owner answer "Approve both scopes, build" to the R-4 lane's in-app question, 2026-10-04 — the question stated that MOD-0184 and MOD-0185 had no owner-approved UI scope (both packs `shell: none`, §11 requiring a separately approved revision) and that their scope packages were PREPARED/HELD with unapproved pack deltas
bound_to: docs/roadmap/plans/mvp6-carrier-ui-scope-01/SHA256SUMS sha256 a292b4e014e7c808e81db7b0368665c67bd12da5b299912c4a251d90b9fd218c (OWNER-DECISION-TEXT.md e8e13fd6…b0407, PROPOSED-PACK.patch 93ee76da…7777d); docs/roadmap/plans/mvp6-loads-ui-scope-01/SHA256SUMS sha256 2ace6f78d97fc9987b1a688e9db42cc143521e20ac8768f45dbbe32328d05b41 (SCOPE.md ce1cadef…f7b65, PHASE15-PROPOSED.md e1e579fa…d900d287)
---

# MOD-0184 Carriers and MOD-0185 Loads — UI scope approval

The owner approves the prepared tenant UI scopes of both modules as written in their packages, and releases them to the
R-4 lanes (R-4a Carriers, R-4b Loads) for building:

- **Carriers** (`mvp6-carrier-ui-scope-01`): list, create and status change over `queryCarriers`, `createCarrier`,
  `changeCarrierStatus`; four create fields (`carrierCode`, `displayName`, `supportedModes`, `externalReference`);
  GoldenReferenceSlim, tenant shell, DataTables v2, UAS-001, seven tenant languages; the three permissions independent.
- **Loads** (`mvp6-loads-ui-scope-01`): list (with the `status`/`carrierId` filters), refresh and create over
  `queryLoads` and `createLoadPlan`. **No transition UI in this slice** (the scope keeps it HELD behind ROOT-UI-01: no
  list or by-ID field exposes a Load's root). LIVE-185 (mock references) stays open as written.

## How the approval is applied

- The approved text becomes a pack section in each pack (MOD-0184 §32, MOD-0185 §30), and each pack's frontmatter
  changes to `shell: tenant` / `golden_reference: slim` with the scope's `form_field_count`. The backend `ready-for-dev`
  scope and its accepted decisions are not reopened.
- **One sentence is not applied as written.** The Carrier scope's §31.3 says "A changed payload is a new intent and key."
  That is the rule measured producing two records from one intent (R-2, `docs/records/audits/2026-10/mvp6-r2-returns-ui-01/evidence/traps-browser.md`
  §T2) and removed from four packs by `cea01354e` (Q403). MODULE-RECIPE line 3.1 outranks the pack in the R-4 dispatch.
  The applying lane writes the Q403 definition (an intent is one opened form or panel) in its place and records the
  deviation. The Loads scope already forbids a resend under a new key while the old outcome is unresolved, so it needs
  no change.
- The gates the scope packages list (source baseline, single integration owner, owner approval, versioned dispatch,
  independent VER) are answered by the R-4 dispatch and this approval. Independent UI VER remains a separate step.

## What this does not approve

Gateway routes (C-03's catch-all already serves the family; MOD-0186 §32.10's explicit-route question stays open), a
Loads transition UI, any contract or backend change, any permission widening, commit or push.
