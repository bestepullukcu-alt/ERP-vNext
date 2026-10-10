# MOD-0187 Claims — Platform / navigation / personalization items (integration owner; SR-D4)

Nothing in this list is applied by the module overlay. Each item is carried from pack §32.10 / §33 and DCP-009 §21.

1. **Self-registration push.** With the provider line applied (`supplychain-Program.cs.patch.txt`), the hosted service
   pushes `claims-management` to Platform `/api/internal/module-catalog/register-manifest` with `X-Internal-Api-Key`.
   No Platform change (existing legacy path). Reconcile tests R-01…R-04 and shared guards W-01…W-04 run on the target.
2. **Navigation keys (7 languages).** Insert `sharedresource-nav-keys/SharedResource.{lang}.resx.fragment.xml` rows into
   `frontend/Diten.Web/Resources/SharedResource.{lang}.resx`: `Nav.Module.CLAIMSMANAGEMENT`, `Nav.Page.CLAIMS`.
   `Nav.Domain.SUPPLYCHAINEXECUTION` already exists in the A12 360 overlay SharedResource files (all 7 languages).
   `NavManifestL10nGuardTests` fails `dotnet test` if the provider lands without these rows (DCP-009 §21.5 item 7).
   Values are proposals for l10n-agent review; the pack does not approve values.
3. **G-SHIPREAD role design.** Create/transition actors also need `supplychain.shipments.read` (pack §32.4). The manifest's
   single-key actions cannot express the conjunction (DCP-009 §21.5 item 4); role templates must grant it.
4. **Personalization codes.** The draft uses `moduleKey: 'claims-management'`, `pageKey: 'CLAIMS'` (the §33 codes) for
   Save View — ASSUMPTION A6; confirm or replace with the registry-reconciled codes.
5. **Ctrl+K / tenant menu.** Page `CLAIMS` at `/SupplyChain/Claims`, icon `bx-receipt`, sort 430 (SOFT, seed-once).
6. **Backend uptake.** Claims backend (47 paths + approved `Program.cs` composition) from `normal-source.tar.gz`
   `edb759a0…` must be in the integrated target; Shipment root emission (`lifecycleCorrelationId`) must be live (CU-18).
