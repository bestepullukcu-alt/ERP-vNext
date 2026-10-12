# _shared-integration — MOD-0190 S&OP (SR-D4 patch-style handoff)

DRAFT. These files are **not** module files. The module lane never copies them into the checkout. Per the SR-D4 owner
decision (`mvp6-ct-verdicts-q59-srd4-2026-09-26.md`) and pack §23.10/§24, the single CT-appointed integration owner applies
them to the shared files at final integration, after the module overlay is built and verified in its isolated environment.
For the isolated environment only (Q84b), the same items may be applied to the *environment copy* so that routing and the
navigation guard work there.

| File | Shared target | Kind |
|---|---|---|
| `supplychain-Program.cs.patch.txt` | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` | one `AddSingleton` line (spec patch) |
| `sharedresource-nav-keys/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx.fragment.xml` | `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` | 2 rows × 7 languages |
| `gateway-sandop-routes.ocelot-fragment.json` | `gateway/Diten.ApiGateway/ocelot.json` | 4 explicit route groups (pack §23.10 list) |
| `frontend-Program.cs.note.txt` | `frontend/Diten.Web/Program.cs` | no change; dependency note (A12 JSON challenge) |
| `icon-map.proposal.md` | `frontend/Diten.Web/tests/diten-field-icons.test.js` | proposal only (G-ICONMAP) |
| `platform-registration.md` | Platform catalog, roles, nav/Ctrl+K, DCP-009 §21.1 | checklist |

Order on the integrated target: S&OP backend uptake (38 paths + approved composition, pack §22) → gateway routes → Program.cs
line + nav keys (same change, W-03 green) → icon map → DCP-009 §21.1 exclusion change → role/permission seed confirmation →
build + tests → runtime.
