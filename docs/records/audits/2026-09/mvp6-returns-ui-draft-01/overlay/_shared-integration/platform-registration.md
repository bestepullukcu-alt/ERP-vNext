# MOD-0186 Returns — Platform / navigation / personalization items (integration owner; SR-D4). NOT APPLIED.

Nothing in this list is applied by the module overlay. Each item is carried from pack §32.10 / §33 and DCP-009 §21.

1. **Backend uptake.** The accepted Returns backend (46 paths + its separately approved `Program.cs` composition, pack §31;
   source `normal-source.tar.gz` `edb759a0…5a21`) must be in the integrated target. `ReturnPermissions.cs` exists only there
   today (pack §33 open gap 1). Shipment root emission (`lifecycleCorrelationId`) must be live (RU-15, producer uptake).
2. **Gateway.** Merge `gateway-returns-routes.ocelot-fragment.json` (explicit routes, OPTIONS, no catch-all) and confirm the
   passthrough of Authorization, X-Correlation-Id, Idempotency-Key, X-Tenant-Id and X-Legal-Entity-Id.
3. **Self-registration push.** With the provider line applied (`supplychain-Program.cs.patch.txt`), the hosted service pushes
   `reverse-logistics` to Platform `/api/internal/module-catalog/register-manifest` with `X-Internal-Api-Key`. Reconcile tests
   R-01…R-04 and shared guards W-01…W-04 run on the target.
4. **Navigation keys (7 languages).** Insert `sharedresource-nav-keys/SharedResource.{lang}.resx.fragment.xml` rows:
   `Nav.Module.REVERSELOGISTICS`, `Nav.Page.RETURNS`, in the same change as the provider line (W-03). Values are proposals
   for l10n-agent review; the pack does not approve values.
5. **G-SHIPREAD and the transition conjunction.** Create and every transition actor also need `supplychain.shipments.read`;
   every transition actor also needs `supplychain.returns.transition` (pack §33 open gap 3). Role templates must grant them.
6. **Personalization codes.** The draft uses `moduleKey: 'reverse-logistics'`, `pageKey: 'RETURNS'` for Save View
   (README A6); confirm or replace with the registry-reconciled codes.
7. **Ctrl+K / tenant menu.** Page `RETURNS` at `/SupplyChain/Returns`, icon `bx-undo`, module sort 420 (SOFT, seed-once).
8. **Icon map.** `icon-map.proposal.md` (G-ICONMAP).
