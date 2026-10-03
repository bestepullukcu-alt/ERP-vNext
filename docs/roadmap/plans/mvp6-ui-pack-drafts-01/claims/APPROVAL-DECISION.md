# MOD-0187 Claims UI — owner approval text

**STATUS: NOT APPROVED.** Nobody has signed this text. Two choices must be filled before signature; recommended options are marked.

## Choice 1 — UI scope

- **[A] list + create + transition (recommended).** The root is readable through published `getShipment` (SCOPE.md §3).
- [B] list + create only; claims stay at Open in the UI.

## Choice 2 — G-SHIPREAD

- **[S1] Create/transition actors also hold `supplychain.shipments.read` (recommended).**
- [S2] Create/transition UI HELD until a different published path is approved.

## Copyable decision text

> I approve the proposed MOD-0187 Claims Management tenant UI **design scope** for pack-revision preparation only:
>
> - identity: a UI revision inside the existing MOD-0187 pack (no FU or new ID); the pack author reruns
>   `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0187 --name "Claims Management"` against the target and stops on a non-zero exit;
> - surface: **[A: list, create and row transition | B: list and create]** over the published SHIPMENT-BUNDLE
>   3.0.0 / wire v1 operations `queryClaims`, `createClaim`, `transitionClaim`, with `getShipment` consumed read-only
>   for shipment status/carrier and for the lifecycle root, which is resolved server-side and never shown to or accepted from the browser;
> - form: GoldenReferenceSlim, `form_field_count: 6`, tenant shell, DataTables v2 bounded profile (client-side
>   search/sort/paging; only `shipmentId` and one `status` sent), create-only offcanvas, row-summary QuickView with no
>   by-ID request; scope-change rows CU-SCR-01…06 are OUT as listed;
> - amounts and currency are sent and displayed as exact text with no conversion, rounding, formatting, ISO list or
>   automatic uppercasing; the carrier is either omitted or the resolved Shipment's own carrier; there is no carrier lookup;
> - Settled is presented as an operational status with no payment/AP/AR; no finance, evidence-upload or new backend behaviour;
> - permissions stay exactly `supplychain.claims.read`, `.create`, `.investigate`, `.decide`, `.settle` with the annex
>   target map; **[S1: create/transition actors also need `supplychain.shipments.read` | S2: create/transition UI
>   HELD until a different path is approved]**;
> - UAS-001, Premium SweetAlert2, seven tenant languages with RTL; the Claims annex controls validation, isolation,
>   errors, root, replay and conflict without client-side tightening;
> - the acceptance matrix CU-VS1, CU-01…CU-31 is the single UI acceptance matrix, and CU-VS1 runs first;
> - effort rows replace 0187-1/4-REMAINING and the UI sub-items of 0187-5/6-REMAINING as in EFFORT.md; they are not added.
>
> This decision authorizes preparing the exact pack revision patch against the pack preimage CT names. It does
> **not** authorize UI DEV, gateway/shared/registration changes, pack promotion, publication, rollout, E5/G5, commit
> or push. UI DEV stays HELD until the patch is approved, UI Phase 1.5 closes, one UI writer and one integration owner
> are appointed on an immutable target, and a versioned dispatch is released.
