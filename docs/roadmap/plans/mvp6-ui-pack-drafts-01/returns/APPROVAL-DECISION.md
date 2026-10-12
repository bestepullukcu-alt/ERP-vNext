# MOD-0186 Returns UI — owner approval text

**STATUS: NOT APPROVED.** Nobody has signed this text. Copying it into a record does not approve it; only the
owner's explicit statement does. Two choices must be made in the text before it can be signed. Recommended options are marked.

## Choice 1 — UI scope

- **[A] list + create + transition (recommended).** The lifecycle root is readable through published
  `getShipment`, so the transition UI needs no root workaround (SCOPE.md §3). Without it, Returns cannot be operated past Requested from the UI.
- [B] list + create only; transition UI deferred to a later revision.

## Choice 2 — how create/transition actors read the Shipment (G-SHIPREAD)

- **[S1] Returns create/transition actors also hold `supplychain.shipments.read` (recommended).** No new API. The
  integration owner reflects it in role design; the UI shows create/transition controls only when both grants are present.
- [S2] Defer. Create/transition UI stays HELD until the integration owner proposes a different published path.

## Copyable decision text

> I approve the proposed MOD-0186 Reverse Logistics tenant UI **design scope** for pack-revision preparation only:
>
> - identity: a UI revision inside the existing MOD-0186 pack (no FU or new ID); the pack author reruns
>   `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0186 --name "Reverse Logistics"` against the target and stops on a non-zero exit;
> - surface: **[A: list, create and row transition | B: list and create]** over the published SHIPMENT-BUNDLE 3.0.0 /
>   wire v1 operations `queryReturns`, `createReturn`, `transitionReturn`, with `getShipment` consumed read-only for
>   shipment lines and for the lifecycle root, which is resolved server-side and never shown to or accepted from the browser;
> - form: GoldenReferenceSlim, `form_field_count: 6`, tenant shell, DataTables v2 bounded profile (client-side
>   search/sort/paging over the returned set; only `shipmentId` and one `status` sent), create-only offcanvas,
>   row-summary QuickView with no by-ID request; scope-change rows RU-SCR-01…06 are OUT as listed;
> - no Return detail page, edit, delete, bulk, import/export, catalogue, remaining-entitlement display, Inventory or
>   Warehouse call, or new backend behaviour;
> - Received is presented as a manual assertion, not warehouse-verified receipt or stock posting;
> - permissions stay exactly `supplychain.returns.read`, `.create`, `.transition` plus target keys `.authorize`,
>   `.transit`, `.cancel`, `.receive`, `.disposition`, `.close`; **[S1: create/transition actors also need
>   `supplychain.shipments.read` | S2: create/transition UI HELD until a different path is approved]**;
> - UAS-001, Premium SweetAlert2, seven tenant languages with RTL; the Returns annex controls validation, isolation,
>   errors, root, replay and conflict without client-side tightening;
> - the acceptance matrix RU-VS1, RU-01…RU-29 is the single UI acceptance matrix, and RU-VS1 runs first;
> - effort rows replace 0186-1/4-REMAINING and the UI sub-items of 0186-5/6-REMAINING as in EFFORT.md; they are not added.
>
> This decision authorizes preparing the exact pack revision patch against the pack preimage CT names. It does
> **not** authorize UI DEV, gateway/shared/registration changes, pack promotion, publication, rollout, E5/G5, commit
> or push. UI DEV stays HELD until the patch is approved, UI Phase 1.5 closes, one UI writer and one integration owner
> are appointed on an immutable target, and a versioned dispatch is released.
