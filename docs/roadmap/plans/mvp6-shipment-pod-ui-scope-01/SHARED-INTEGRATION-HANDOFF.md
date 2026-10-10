# Shared integration handoff

Bu yuzeyler 28 UI-owned yolun disindadir. Yalniz tek integration owner, secilmis immutable hedefin exact
preimage/patch/target hash'lerine bagli ayri bir kararla uygular.

| Owner | Shared surface | Minimum need | Acceptance |
|---|---|---|---|
| gateway integration owner | `gateway/Diten.ApiGateway/ocelot.json` | Gateway 5000'den 5061'e yalniz GET/POST Shipment collection, GET detail, POST transition ve POST POD yollarini ileten exact routes | Authorization, tenant, LE, correlation and idempotency headers preserved; unsupported verbs/paths stay unsupported; all unrelated routes hash-stable |
| SupplyChain registration owner | selected-baseline module manifest/provider, DI registration and completeness tests; exact paths must be named before approval | Module `shipment-tracking-pod`; nav page `SHIPMENTS`; non-nav Create/Details pages; actions create/dispatch/cancel/POD mapped to existing literals | Route/security scope correct; page/action↔permission both-direction completeness; restart reconciliation idempotent; no `reconcile` UI action |
| tenant navigation/l10n owner | the same manifest plus `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` | `Nav.Module.shipment-tracking-pod`, `Nav.Page.SHIPMENTS`, and domain key only if absent | Nav hidden without read; `/SupplyChain/Shipments`; Ctrl+K derives from nav; no global search JSON edit |
| source/integration owner | a durable registered checkout and immutable transfer manifest derived from accepted MOD-0183 sources | Transfer exact backend dependencies and later UI/shared patches without overwriting conflicts | All preimages match; selected 422 evidence target is not silently treated as common checkout or transfer authority |

Protected and consumed unchanged unless a later exact decision says otherwise: `_LayoutTenantShell.cshtml`, shared access-denied
and SweetAlert wrappers, global JS, Auth/permission middleware, backend Shipment source, canonical contract and guard files.

## Exact unresolved integration decisions

1. Select the durable target checkout and publish its immutable input manifest. The current 422 target proves a bounded
   integration snapshot but does not authorize copying into another checkout.
2. Name the existing module-registration/provider pattern and exact DI/test files from that selected target. No provider
   path is guessed in this preparation because the inspected SupplyChain source has no Shipment manifest provider.
3. Prepare one combined shared patch with per-file baseline/patch/target hashes. Missing preimage or overlap with another
   writer stops application; it is not resolved by overwrite.

