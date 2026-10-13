# MVP6 UI module-pack drafts — MOD-0186 Returns and MOD-0187 Claims (Q33)

2026-09-26 · Lane: module-pack author, documents only. **DRAFT. Authorizes nothing.** No pack, contract, product
code, `.antigravity`, gateway or existing record was edited. No MOD ID was created, and there was no commit or push.

## Why

Returns and Claims have accepted bounded backends (`mvp6-mod0186-wp-acceptance-01`, `mvp6-mod0187-ct-accept-01`)
but no UI authority: both packs are `shell: none`. AGENTS.md §10 requires an approved pack before UI code. These drafts
prepare that approval so the pack revision can be written as soon as the owner decides.

## Contents

| Path | What |
|---|---|
| `returns/SCOPE.md`, `claims/SCOPE.md` | Identity (DCP-002), bound operations, screens/routes/permissions, list/create/transition design, errors, L10n, UAS-001, out-of-scope, open gates |
| `*/ACCEPTANCE.md` | Single acceptance matrix (HTTP/browser/DB per row), early vertical slice first, generic DataTable items decided OUT up front |
| `*/OWNED-PATHS.md` | 21 owned UI files per module; protected paths |
| `*/SHARED-HANDOFF.md` | Single integration owner: gateway routes (listed, not edited), registration/permissions, nav, shared L10n, root and backend uptake |
| `*/EFFORT.md` | O/M/P by delivery, Scope A vs B, as replacement of existing REMAINING rows |
| `*/APPROVAL-DECISION.md` | Exact owner text, **NOT APPROVED**, with two marked choices each |
| `SHA256SUMS` | Hashes of every file in this package |

## Main findings

1. **Identity:** both are UI revisions inside the existing packs, with no FU and no new ID. `verify_module_id.py` returned
   `OK`, exit 0, for `MOD-0186 "Reverse Logistics"` and `MOD-0187 "Claims Management"` (read-only run, 2026-09-26).
2. **Transition UI is feasible, unlike Loads (ROOT-UI-01).** Returns and Claims inherit the **Shipment's**
   `lifecycleCorrelationId`, and the published `getShipment` exposes it. The MVC adapter resolves it server-side, so
   no root is derived, cached or shown in the browser. This is the basis for recommending Scope A.
3. **No by-ID GET for Return/Claim.** So there is no Details page and QuickView uses the row only.
   `ClaimSummary` lacks `approvedAmount`, so it cannot be listed after reload (a recorded gap, not derived).
4. **Form counts:** 6 schema field types each → GoldenReferenceSlim. The bounded DataTable profile is decided up
   front: client-side over the returned set, with only `shipmentId` + single `status` sent. Bulk, edit/delete, import/export and
   server paging are OUT as proposed scope-change rows.
5. **New dependency G-SHIPREAD:** create/transition actors need `supplychain.shipments.read`, because the root and lines come from `getShipment`. This is an owner/security choice in each APPROVAL-DECISION.md.
6. **Backend uptake:** the common checkout lacks 43 Returns and 44 Claims accepted paths (per the CT records). The
   UI can only be verified on a target where the accepted WPs are integrated (gate G-TARGET).

## Joint delivery option (both modules, one wave)

The two UIs share the same pattern, integration owner and VER environment. Done together, shared integration is
estimated at 12/22/38 h instead of 16/32/56 (two separate passes), and one VER environment/fixture chain at
24/40/68 instead of 28/48/80. Joint Scope A for both modules is about **106 / 180 / 310 h** instead of 114 / 198 / 340.
These savings are estimates and are not yet reflected in the per-module EFFORT.md files.

## Inputs read (pinned, SHA-256)

| Input | SHA-256 |
|---|---|
| `AGENTS.md` | `51d92c75b761e5c7195eda102d08f2b7f472b979d9940bdf21ae72cb7cab7fcb` |
| `CLAUDE.md` | `86fd9d60328aa8225e713fb99bbbcfdec62e825c8d55dfca6188835a4e0ec521` |
| MOD-0186 pack (common checkout, draft) | `07a8a0159b5ab02fecc7b1aa6314106d81b1584150169f457cb9a395614a04b7` |
| MOD-0187 pack (common checkout, draft) | `a342054cddcd88a9fae67f21b51ca90eadcdf355e6119c77c9578bae744b1c1f` |
| `shipment-bundle.openapi.yaml` (3.0.0 / v1) | `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` |
| `returns-semantics-v3.0.0.md` | `00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11` |
| `claims-semantics-v3.0.0.md` | `16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63` |
| `mvp6-mod0186-wp-acceptance-01/SOP-22.md` | `3a61b6e5bb3ed039cee0c76e158a929160583212b286bbfaa35f1d42100d310e` |
| `mvp6-mod0187-ct-accept-01/SOP-22.md` | `30c9233e3d8ac170e53517531b75c616df427d9ceee76aa6bee7c4ea1bab7942` |
| `mvp6-shipment-acceptance-reconcile-01/ACCEPTANCE-MATRIX.tsv` | `282fc9a854e6a30eaea4536f81c6a19000e046da215b6718d96696421498cd21` |
| `mvp6-development-process-v1.0.md` | `ad0350508823c44b0ef62ca9721ed710db9e217bdb39e6af9122bc08efe1dd18` |
| `supply-chain-execution/domain-config.md` | `c49ec37a45b492e22988fdf7225aaa90b68c7a09fa4d0491dea06fe7cd793fe3` |
| `mvp6-effort-shipment-ct-update-07/EFFORT.tsv` | `0b643ca95e2c05de144952d1505efdc2321e0a2d56c8ec3603dd00e5f3162ad3` |
| `mvp6-loads-ui-scope-01/SCOPE.md` (pattern) | `ce1cadef9bd0b10ecdf0a7159fbc88ac4f6d423e33fe8040d0231e567d5f7b65` |
| `mvp6-carrier-ui-scope-01/OWNER-DECISION-TEXT.md` (pattern) | `e8e13fd68265f73da303d67c0ec0fe6e400041c02ec7d33660e950166cc0b407` |
| `mvp6-shipment-pod-ui-scope-01/SCREEN-ROUTE-PERMISSION.tsv` (pattern) | `de868bb87ef3d0c48fa31fa15bdf291e5797b4821342a60f177fa941babb1fb0` |
| `.antigravity/scripts/verify_module_id.py` | `3d20892853a526f14255ed2d2ff7afbf9a2fcce43e45f229e3a8947385792b63` |
| rules: unauthorized-surface / datatable / form / details / localization / views-organization / permission-key / routes / premium-modal / module-pack | `16f9fab9…` / `02dd882f…` / `8b787934…` / `d5c05da2…` / `4029a06b…` / `11931b05…` / `dcd1bd53…` / `31acc807…` / `3c15f254…` / `016dc6c5…` |

The published YAML and both annex hashes match the values in the two CT acceptance records.

## What was and was not done

Done: HEAD check (`feature/mvp6-logistics` @ `4a8d4d4b…`), reading the inputs above, a read-only DCP-002 identity run,
parsing the operation/schema/status list from the published YAML, and arithmetic checks of the effort tables.
Not done: no build, test, browser, gateway or runtime check. No pack patch was produced; that follows owner approval
against the preimage CT names. Only `git rev-parse` was used (with `GIT_OPTIONAL_LOCKS=0`), so the index was not touched.
Reading depth, stated honestly: `frontend-datatable-template.md` and `module-pack-standard.md` were read in their
relevant sections; `frontend-form-template.md`, `frontend-details-template.md`, `permission-key-standard.md` and
`routes.md` were read only in part or hashed. No Compact form or Details page is proposed, and the pack author must
reread all ten rules in full before writing the pack revision.
