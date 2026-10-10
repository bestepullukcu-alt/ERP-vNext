# _shared-integration — MOD-0192 Capacity Planning (SR-D4 patch-style handoff)

DRAFT. **Not applied.** These files are **not** module files. The module lane never copies them into the checkout. Per the
SR-D4 owner decision and pack §23.10/§24, the single CT-appointed integration owner applies them to the shared files at final
integration, after the module overlay is built and verified in its isolated environment. For the isolated environment only
(Q88b), the same items may be applied to the *environment copy* so that routing and the navigation guard work there.

| File | Shared target | Kind |
|---|---|---|
| `supplychain-Program.cs.patch.txt` | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` | one `AddSingleton<IModuleManifestProvider, CapacityPlanningManifestProvider>` line (spec patch) |
| `sharedresource-nav-keys/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx.fragment.xml` | `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` | 2 rows × 7 languages (`Nav.Module.CAPACITYPLANNING`, `Nav.Page.CAPACITY_PLANS`) |
| `gateway-capacity-routes.ocelot-fragment.json` | `gateway/Diten.ApiGateway/ocelot.json` | 6 explicit routes (pack §23.10 list), port 5061 |
| `frontend-Program.cs.note.txt` | `frontend/Diten.Web/Program.cs` | no change; dependency note (A12 JSON challenge) |
| `icon-map.proposal.md` | `frontend/Diten.Web/tests/diten-field-icons.test.js` + nav module icon | proposal only (G-ICONMAP); icon `bx-bar-chart-alt-2`, SortOrder 450 |
| `platform-registration.md` | Platform catalog, roles, nav/Ctrl+K, DCP-009 §21.1 | checklist |

Order on the integrated target: Capacity backend uptake (43 paths + approved composition + executor, pack §22) → gateway routes →
Program.cs line + nav keys (same change, W-03 green) → icon map → DCP-009 §21.1 exclusion change → role/permission and fixture
seed confirmation → build + tests → runtime.
