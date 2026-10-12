# Findings

## SHIP-UI-B01 — list page partial resolution

`SupplyChainShipmentsController.Index` returns the explicit nested view, but `Index.cshtml` uses relative partial names. MVC resolves those relative to the controller convention and raises `InvalidOperationException: The partial view '_Filter' was not found.` The file is present at `Views/SupplyChain/Shipments/_Filter.cshtml`; missing bytes are not the cause.

Minimum rework should make the nested partial references explicit and cover runtime Razor view resolution. No fix is included here.

## SHIP-UI-B02 — mutation root/trace mismatch

The detail response renders `lifecycleCorrelationId`, yet `details.js` generates a fresh UUID for the mutation `X-Correlation-Id`. The accepted Shipment backend treats the persisted root as authoritative for lifecycle mutation and correctly fails the request with `400 INVALID_REQUEST`. The failed attempt produced no additional lifecycle/audit/outbox/receipt write.

Minimum rework must preserve the published root/trace rules and bind transition/POD requests to the authoritative root without deriving or replacing it. No fix is included here.

