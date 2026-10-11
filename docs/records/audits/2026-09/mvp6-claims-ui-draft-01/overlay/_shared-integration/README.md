# _shared-integration — MOD-0187 Claims (SR-D4 patch-style handoff)

DRAFT. These files are **not** module files and are never copied into the checkout by the module lane. Per the SR-D4
owner decision (`mvp6-ct-verdicts-q59-srd4-2026-09-26.md`) the single CT-appointed integration owner applies them to
the shared files at final integration, after the module overlay is built and verified in its isolated environment.

| File | Shared target | Kind |
|---|---|---|
| `supplychain-Program.cs.patch.txt` | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` | one `AddSingleton` line (spec patch) |
| `sharedresource-nav-keys/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx.fragment.xml` | `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` | 2 rows × 7 languages |
| `gateway-claims-routes.ocelot-fragment.json` | `gateway/Diten.ApiGateway/ocelot.json` | 2 explicit routes + 1 confirmation |
| `frontend-Program.cs.note.txt` | `frontend/Diten.Web/Program.cs` | no change; dependency note (A12 JSON challenge) |
| `icon-map.proposal.md` | `frontend/Diten.Web/tests/diten-field-icons.test.js` | proposal only (G-ICONMAP) |
| `platform-registration.md` | Platform catalog, roles, personalization, nav | checklist |

Order on the integrated target: backend uptake → gateway routes → Program.cs line + nav keys (same change, W-03 green)
→ icon map → role/personalization confirmation → build + tests → runtime.
