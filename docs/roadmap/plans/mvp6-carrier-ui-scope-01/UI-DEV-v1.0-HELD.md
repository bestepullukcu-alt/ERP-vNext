# MVP6-MOD0184-UI-DEV-01 — v1.0 — HELD

**Role:** frontend DEV + single UI writer  
**State:** HELD; do not execute from this document.

## Release conditions

Start only after all conditions are recorded with exact hashes:

1. the owner approves the UI scope text, proposed pack target SHA256 `28019ebe9fd6e34ca719d244cc0129fe2046ee0f692a9ffd6a5a6eed16028f3f`, and UI Phase 1.5;
2. MOD-0184 records the approved UI scope without treating the existing backend readiness as UI authority, and this prompt is reissued as an active version;
3. Lane A successor selects and authorizes one immutable source checkout/transfer manifest;
4. the exact gateway, SupplyChain permission/module-registration, and navigation changes have approved single owners and target hashes;
5. no concurrent writer owns any shared path.

## Inputs

- `docs/roadmap/plans/mvp6-carrier-ui-scope-01/`
- the exact UI-scoped MOD-0184 pack target
- SHIPMENT-BUNDLE contract SHA256 `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`
- Carrier annex SHA256 `87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee`
- the successor baseline/checkout manifest and separately approved integration artifacts

## Work

Implement only the 21 paths in `OWNED-PATHS.txt`. Build the tenant-shell Golden Slim Carrier list/create/status UI and same-origin MVC adapter. The adapter calls Gateway 5000 only and preserves exact auth, tenant/LE, correlation, error, and replay semantics. Use DataTables v2 client-side search/order/page and send only the exact optional status query. Use UAS-001, seven module resource sets, accessible validation, and Premium SweetAlert2/shared confirmation behavior.

Do not add detail/edit/delete/bulk/import/lookup endpoints or controls. Do not change backend Carrier behavior, `Program.cs`, shared shell/resources, permission catalog/module registration, gateway, canonical contract/annex, or other module files. If a shared change is necessary, stop that part and hand off an exact baseline/patch/target to its named integration owner; continue disjoint owned work.

## Required evidence

- source/input/changed-file hashes and dirty baseline;
- frontend build plus focused controller/form/JS/resource-parity tests;
- DataTables v2 and Golden Slim verifier;
- A01–A12 mapping including direct MVC 401/403, empty list, validation, duplicate, lifecycle, and same-key retry;
- source→build→process→browser/HTTP evidence where the authorized integration baseline permits it;
- explicit boundary between UI-isolated PASS and composed Gateway PASS;
- SOP §22 handoff and writer-complete.

No commit, push, stash, rollout, E5/G5, or full-module acceptance.
