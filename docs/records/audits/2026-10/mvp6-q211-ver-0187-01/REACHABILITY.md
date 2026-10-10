# Q211 — MOD-0187 Claims: reachability through the real service

Static proof only (E1). Nothing was built or run. Paths are under
`services/Diten.SupplyChainService/src/Diten.SupplyChainService.` unless written in full.

## Answer

**No Claims endpoint is reachable through the real service today: 0 of 3.**

| Operation | Through the Gateway (5000) | Directly on the service (5061) |
|---|---|---|
| GET `/api/shipment-bundle/claims` | no route | cannot succeed |
| POST `/api/shipment-bundle/claims` | no route | cannot succeed |
| POST `/api/shipment-bundle/claims/{claimId}/transition` | no route | cannot succeed |

## Proof, layer by layer

| # | What a real request needs | In the tree? | Evidence |
|---|---|---|---|
| 1 | Gateway route | **No** | `gateway/Diten.ApiGateway/ocelot.json`: 0 × `claims`, 0 × `shipment-bundle`. All 35 routes on port 5061 are `/api/crm` |
| 2 | Controller | Yes | `Api/Features/Claims/ClaimsController.cs:12-23` — `[Authorize, Route("api/shipment-bundle/claims")]`, three actions |
| 3 | Controller mapped | Yes, by framework rule | `Api/Program.cs:71` `app.MapControllers()` maps every controller in the Api assembly. Not executed |
| 4 | Claims DI: `ClaimRequestContext`, `IClaimRepository`, `ClaimOutboxStore`, `ClaimSchema` | **No** | `Persistence/Features/Claims/ClaimPersistenceRegistration.cs:9-16` defines `AddClaimPersistence()`; `Api/Program.cs:46-47` calls only `AddCarrierPersistence()` and `AddLoadPersistence()`. `grep AddClaimPersistence` outside `Features/Claims` = 0 |
| 5 | `IClaimReferenceReader` HTTP client | **No** | `Api/Program.cs:48` registers only `ILoadReferenceReader`. `AddClaimPersistence()` does not register the reader either |
| 6 | Claims middleware (scope, correlation, idempotency key, permissions) | **No** | `Api/Program.cs:66-67` wire only Carrier and Load middleware. `grep ClaimContextMiddleware` outside `Features/Claims` = 0 |
| 7 | Exclusion from the Shipment branch | **No** | `Api/Program.cs:68` excludes only Carrier and Load paths. `Api/Middleware/ShipmentContextMiddleware.cs:9` takes every `/api/shipment-bundle` path, so a Claims request enters the Shipment middleware |
| 8 | Model-error adapter for the Claims family | **No** | `Api/Program.cs:57-60` handle only Load and Carrier |
| 9 | Config `Claims:ReferenceBaseUrl` | **No** | `Infrastructure/Features/Claims/ClaimReferenceReader.cs:88-89` returns 503 `CLAIM_REFERENCE_UNAVAILABLE` without it. `grep Claims` in `Api/appsettings*.json` = 0 |
| 10 | Indexes created at start | **No** | `ClaimSchema` is registered only inside `AddClaimPersistence()` (`ClaimPersistenceRegistration.cs:15`). Without it `ClaimRepository.cs:36-49` fails closed with 503 |
| 11 | Permission keys granted to a role | **No** | `supplychain.claims.*` appears only in `Features/Claims` and its tests. No seed, no catalogue |

What happens to a direct request on 5061 today (reasoned from the code, not run): the route matches (row 3),
the Shipment middleware handles the path first (row 7), and the controller cannot be created because
`ClaimRequestContext` is not registered (row 4; `ClaimsController.cs:13`). No request reaches a handler.

## What the tests actually exercise

The prompt says the Claims tests "pass on a self-composed host". Measured, they do not compose a host at all:

- `grep WebApplicationFactory|TestServer` in `tests/Diten.SupplyChainService.Tests/Claims/` = **0**.
  (Shipments, Carriers and Loads do use `WebApplicationFactory<Program>`.)
- Repository tests build `ClaimRepository` by hand on a real replica set (`ClaimAtomicityTests.cs:73-81`).
- Middleware tests call `ClaimContextMiddleware.InvokeAsync` on a `DefaultHttpContext` with a pre-authenticated
  principal and a synthetic token (`ClaimIsolationTests.cs:28-55`). The file says so itself, lines 23-24:
  "Component checks only … These do not claim JWT signature/HTTP uptake."
- One handler is tested: `TransitionClaimHandler` with a counting fake (`ClaimPermissionTests.cs:17-22`).

Never executed by any test in this tree: `ClaimsController`, `CreateClaimHandler`, `GetClaimListHandler`,
the three FluentValidation validators, `AddClaimPersistence()`, real JWT validation, and JSON serialisation of
`ClaimResponse` / `ClaimListResponse`.

So the 128 green tests prove the domain rules, the repository and the middleware logic as components.
They prove nothing about HTTP, and less than "feature-level through a host".

## A start-up risk worth checking in Q209 (not verified here)

`Application/DependencyInjection.cs:11` registers every MediatR handler in the Application assembly. Since Q202a
that includes `CreateClaimHandler`, `TransitionClaimHandler` and `GetClaimListHandler`, whose constructors need
`IClaimRepository`, `ClaimRequestContext` and `IClaimReferenceReader` — none registered (rows 4, 5).

- The existing host tests set the environment to `Testing` (`ShipmentTests.cs:62`, `SourceIntakeTests.cs:54`,
  `Loads/LoadContractTests.cs:68`, `Carriers/CarrierContractTests.cs:59`), where the container is not validated at build.
- In the `Development` environment ASP.NET Core validates the container when it is built. Whether the service
  still starts there with these unresolvable handlers: **insufficient evidence** — it needs a run, and this lane
  does not run anything. If it fails, it affects the whole service, not only Claims.

## Shared seams needed to make Claims real (named, not patched)

1. `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` — `AddClaimPersistence()`,
   an HTTP client for `IClaimReferenceReader`, the Claims middleware branch, exact exclusion of the Claims family
   from the Shipment branch, and the Claims model-error adapter. The pack's accepted composition is sha256
   `11c586e0…` (pack line 532); the tree has `7fdb5ef0…`. Integration owner only (pack lines 509, 539).
2. Service configuration — `Claims:ReferenceBaseUrl`.
3. `gateway/Diten.ApiGateway/ocelot.json` — three Claims routes, explicit, with OPTIONS, `/claimsXYZ` not matched
   (pack lines 685-686), plus the gateway route-count test. integration-agent only.
4. Shared permission catalogue / seed — the five `supplychain.claims.*` keys; for the UI also
   `supplychain.shipments.read` (G-SHIPREAD, pack line 588).
5. Later, with the UI: `SharedResource.{lang}.resx` navigation keys in 7 languages, the manifest provider
   registration, the personalization codes and the field icon map (pack lines 687-689, 793, 812).
