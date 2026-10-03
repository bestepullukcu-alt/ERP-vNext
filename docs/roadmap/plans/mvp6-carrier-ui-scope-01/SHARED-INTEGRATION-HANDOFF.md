# Shared integration handoff

These are separate integration-owner changes. None is part of the 21 UI-owned paths and none is authorized here.

| Owner | Exact surface | Need | Acceptance |
|---|---|---|---|
| integration-agent / single gateway writer | `gateway/Diten.ApiGateway/ocelot.json` | Add explicit Carrier forwarding from Gateway 5000 to SupplyChain 5061 for the published list/create/status paths. Do not add a backend operation. | GET/POST and required OPTIONS reach only the three published operations; unsupported verbs remain unsupported; other routes hash-stable. |
| permission/catalog owner | exact existing SupplyChain module-registration/permission provider and its registration/tests, to be named from the authorized baseline | Register module `carrier-management`, page `CARRIERS`, and `supplychain.carriers.read`, `.create`, `.status.change`; bind independent grants to page/actions. | default-deny, exact-case keys, direct action 403, no grant widening. |
| navigation/page-descriptor owner | the same registered module/page descriptor plus missing `SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` navigation keys, exact paths to be named from the authorized baseline | Add one Carrier page entry gated by `.read`. | entry absent without read; tenant shell route `/SupplyChain/Carriers`; no shared shell markup change. |
| source-baseline/integration owner | Lane A successor decision derived from `mvp6-integration-baseline-selection-01` | Bind the UI and shared patches to one immutable checkout/source manifest and authorize transfer. | every transferred input hash matches; missing/conflict files stop rather than overwrite. |

`frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml`, shared access-denied/confirmation partials, global JS, `Program.cs`, permission middleware, and gateway policy code remain protected. Existing components are consumed as-is. If the selected baseline proves an actual shared change is necessary, the integration owner must publish a new exact path/diff/target-hash decision.

The MVC adapter's downstream base is Gateway 5000. Browser requests are same-origin to the frontend; no JavaScript may call 5061. There is no Carrier route in the inspected gateway baseline, so composed evidence cannot pass before the gateway handoff is applied by its owner.
