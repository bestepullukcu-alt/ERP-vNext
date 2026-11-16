# Dilim B partial browser tour (MOD-0183 + MOD-0186 DL-3 / DL-4) · 2026-11-06

Run by the MVP-6 CT on the lane stack, branch `feature/sce/mod0183-dl3-01` at `d0da4d4c9` (+ `8062bd97b` gateway route).
**Partial**: not the SB-16 / RB-18 acceptance run. CRM, INVENTORY, PRODUCT-MASTER, TRACE, QUARANTINE, Auth and Platform were
not started, so no shipment could be dispatched (dispatch issues stock in INVENTORY) and no return could be created.

## Stack

Lane replica set on 127.0.0.1:6156 (`rs-tour`), databases `tour_mdm`, `tour_sc` only (listDatabases; 27017 untouched).
MDM 5059, SupplyChain 5066, Gateway 5000 (lane signing key), Web 5001 (Mongo overridden to the lane). Token minted in the
lane (HS256, lane secret, no `legal_entity_id`, shipments / returns / mdm legal-entity keys + `crm.account.read` +
`quarantine.holds.read`), handed to the browser through the one-shot `access_token` cookie bridge on 5199; never printed.
Run folder: `ERP-vNext-lanes/mod0183dl3/tour/run-20261106-101400`. Seed: one legal entity (create 201, activate 204,
lookup-validation 200); two shipments through the Gateway, one with `customerRef`, one without (old record), both 201 Draft.
All lane processes stopped after the run (ports 5000/5001/5059/5066/6156 free).

## Measured

1. **getReturn Gateway route (VER F-1):** `GET /api/shipment-bundle/returns/{unknown}` through the Gateway answers the
   service's own `404 {"error":{"code":"RETURN_NOT_FOUND"…},"contractVersion":"v1"}`, so the route reaches SupplyChain.
2. **Shipments list (SB-9, SB-10, SB-11a, SB-13):** server-mode table draws after the shared company selector (no list
   before a company); customer cell "Name unavailable" (CRM down) with no id; old record marked "Old record" with its
   `shipToReference` as stored; warehouse "Name unavailable" plus the reference small; no horizontal overflow at 1024.
   The account id and the string `customerRef` are absent from the whole page HTML.
3. **List latency (DL-4 VER F-1):** list adapter calls 495 ms (first) and 44 ms (second) with every name source refusing
   connections; the real cost with CRM / LOCATION answering is not measured.
4. **Shipment details (SB-12):** sender company name resolved from MDM; warehouse, SKU and location "Name unavailable" with
   the reference small; customer without an id. Visible technical ids: lifecycle root, the shipment number's GUID part (known
   DL-4 VER F-8 and the SHP follow-up).
5. **Create form (SB-5, SB-3, SB-7):** Golden Compact cards; sender company read-only; warehouse / customer / product pickers;
   lot disabled with "Choose a product first."; the customer picker with CRM down shows the localized "The list could not be
   loaded. Try again later.".
6. **Returns list (RB-25):** customer column drawn (token holds `crm.account.read`); empty list (no returns).
7. **Returns adapters in the page (RB-3, RB-4, RB-5):** eligible picker (create) 200 with 0 rows (both shipments Draft); list
   filter picker (`any=true`) 200 with both shipments, customer `unavailable` / `oldRecord`, the id only as the option value;
   reason select has the 7 codes; `returnShipmentId` is a `<select>`; no UUID text input on form or filter.
8. **Return details (RB-9):** unknown id shows the localized not-found text with the support reference; no cards drawn.

## Not measured

Dispatch, POD, every return flow (create, transition, Received, KRN link), picker rows with real CRM / LOCATION /
PRODUCT-MASTER / TRACE answers, export download, 390 / 768 / 1440 widths, Arabic RTL, the six other languages, the
permission-less variants (no `crm.account.read`, no `quarantine.holds.read`) in the browser, Auth sign-in.
