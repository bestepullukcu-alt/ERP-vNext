# Q210 — MOD-0186 Returns: reachability and shared seams

Static reading only. Nothing here was run, and no seam was patched.

Short paths used below: `S.` = `services/Diten.SupplyChainService/src/Diten.SupplyChainService.`

## 1. Where the module stands today

| Layer | State | Evidence |
|---|---|---|
| Returns source (36 files) | Present, compiles, byte-equal to the CT-accepted source | `SCOPE-DELTA.tsv`; build: Q208 `SOP-22.md` |
| Returns tests (7 files, 78 cases) | Green in Q208. They do **not** start a web host. | see §2 |
| Service composition (`Program.cs`) | No Returns line | `S.Api/Program.cs:46-48` |
| Service configuration | `Returns:ReferenceBaseUrl` not set anywhere | `S.Infrastructure/Features/Returns/ReturnReferenceReader.cs:71`; no match in `S.Api/appsettings*.json` |
| Gateway | 0 routes for `/api/shipment-bundle/**`; the 35 routes on port 5061 are all `/api/crm` | `gateway/Diten.ApiGateway/ocelot.json` (269 routes) |
| Permission keys | Exist only in `ReturnPermissions.cs` and one test | `S.Infrastructure/Features/Returns/ReturnPermissions.cs:4-14` |
| Self-registration provider, navigation keys | Absent | `S.Api/ModuleRegistration/` (2 providers, neither is Returns); `SharedResource.en.resx`: 0 matches |
| UI | Absent (0 of 21 owned paths) | pack `:721-725` |

## 2. How the tests pass without the service

The prompt says the Returns tests "compose their own host". Measured: **no Returns test builds a host.**

- 6 of the 7 classes call the repository directly through `ReturnTestFixture`. The fixture registers the shared
  Mongo services, then creates `ReturnSchema` and `ReturnRepository` itself
  (`tests/Diten.SupplyChainService.Tests/Returns/ReturnAtomicityTests.cs:55-60`).
- The middleware is tested as a unit with `DefaultHttpContext` and a fabricated principal. The file says so:
  "NOT JWT validation" (`ReturnIsolationTests.cs:24`).
- The reference reader is tested with a stub `HttpMessageHandler` (`ReturnReferenceTests.cs:79-82`).
- `ReturnsController`, `[Authorize]`, JwtBearer and the per-target grant branch are executed by **no** test.

So 78/78 proves the repository, the wire validator, the reference parser and the middleware's context checks.
It proves nothing about the HTTP surface.

## 3. The controller is not simply "not wired" — it is half-wired

- `Program.cs:71` calls `app.MapControllers()`. `ReturnsController` is a public controller in the Api assembly
  (`S.Api/Features/Returns/ReturnsController.cs:13-14`). MVC discovers it by convention. **Its three routes are
  mapped in the service today**, on port 5061, with no gateway in front.
- What a request to `/api/shipment-bundle/returns` would meet, by reading:
  1. `Program.cs:68` sends every path that is not Carrier or Load through **`ShipmentContextMiddleware`**. Returns
     paths are therefore handled by the Shipment context policy. The pack forbids exactly that
     (`MOD-0186-reverse-logistics.md:381`, `:386`).
  2. `ReturnContextMiddleware` is not in the pipeline. Nothing fills `ReturnRequestContext`, checks the scope
     headers, or checks the per-target grant (`ReturnContextMiddleware.cs:64-95`).
  3. `ReturnRequestContext`, `IReturnRepository` and `IReturnReferenceReader` are not registered
     (`ReturnPersistenceRegistration.cs:9-13` is never called). The controller's constructor needs the first one.
  4. `ReturnsController.cs:17` parses `status` and `shipmentId` with `Enum.Parse` / `Guid.Parse`. It relies on the
     absent middleware to have validated them.
- **What actually happens at run time: insufficient evidence.** This WP may not run the service. The static
  expectation is a failure before any Returns logic runs. It should be confirmed by the lane that owns the wiring.

## 4. Shared seams needed to make the module real (none patched)

| # | Seam | What is missing | Owner per record |
|---|---|---|---|
| 1 | `S.Api/Program.cs` | `AddReturnPersistence()`; `AddHttpClient<IReturnReferenceReader, ReturnReferenceReader>`; a `UseWhen(ReturnContextMiddleware.IsReturnPath, …)` branch; Returns excluded from the Shipment branch at `:68`; a Returns branch in the model-state factory at `:55-63` | single integration owner — Q209 HELD (`CT-QUEUE.tsv:305`); pack `:377-382` |
| 2 | Service configuration | `Returns:ReferenceBaseUrl` (absolute http/https URL of the Shipment API). Without it create returns 503 `DEPENDENCY_UNAVAILABLE` (`ReturnReferenceReader.cs:71`) | integration owner (shared appsettings) |
| 3 | `gateway/Diten.ApiGateway/ocelot.json` | GET + POST `/api/shipment-bundle/returns`, POST `/api/shipment-bundle/returns/{returnId}/transition`, and GET `/api/shipment-bundle/shipments/{shipmentId}`; pass-through of `Authorization`, `X-Correlation-Id`, `Idempotency-Key`, `X-Tenant-Id`, `X-Legal-Entity-Id`; `/returnsXYZ` not matched | integration-agent only (`AGENTS.md:108`); pack `:117`, `:734-736` |
| 4 | Gateway route-count guard | `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs` counts Supply Chain routes; Returns adds two (F-Q201-8) | integration owner |
| 5 | Shared permission catalogue / role seed | Nine keys: `supplychain.returns.read`, `.create`, `.transition`, `.authorize`, `.transit`, `.cancel`, `.receive`, `.disposition`, `.close`. Create and transition actors also need `supplychain.shipments.read` (G-SHIPREAD) | integration owner; pack `:393`, `:637-639` |
| 6 | Token claims | The middleware requires exactly one `tenant_id`, `legal_entity_id`, `sub`, and a `permission` claim per key (`ReturnContextMiddleware.cs:43`, `:64-65`). Whether the Auth service issues these claims: insufficient evidence (outside Q210's scope) | security owner |
| 7 | MOD-0183 producer | `getShipment` must return `lifecycleCorrelationId`. It is in the tree (`S.Application/Features/Shipments/ShipmentProjection.cs:31`). MOD-0183 as dependency evidence is still unverified (pack `:137`) | CT |
| 8 | Self-registration | `ReverseLogisticsManifestProvider` + its `AddSingleton` line in `Program.cs` + tests M-01…M-08; ships with the UI | integration owner; pack `:796`, `:859` |
| 9 | Navigation keys | `Nav.Module.REVERSELOGISTICS`, `Nav.Page.RETURNS` × 7 languages in `SharedResource.{lang}.resx` (0 present) | integration owner + l10n agent; pack `:833-840` |
| 10 | Tenant UI | 21 owned UI paths; the Returns UI draft v3 is not in the tree (Q202b HELD), v3a is ordered | UI writer after an integrated target exists; pack `:788` |
| 11 | Event transport | Events stay `Pending` in `returns_outbox`; there is no worker or publisher by design (`ReturnOutboxStore.cs:5`) | separate integration WP; pack `:393`, `:592` |
| 12 | Inbound receiving / Inventory reconciliation | `Received` is a manual assertion; the Inventory reference is opaque text | open by design; pack `:588`, `:592` |

## 5. Related work that changes this code, not yet in the tree

- **Q187 returns-guard overlay** (per-target transition guard): 4 Returns files changed + 1 test added. Accepted
  as a writer result; its VER (Q189) is CT NOT ACCEPTED; not applied (Q202b HELD, `CT-QUEUE.tsv:303`). The tree
  holds the pre-guard version, which is the version the earlier CT acceptance covers.
- **Q215 fail-point alignment** (`CT-QUEUE.tsv:311`): would make the unknown-commit test real evidence.
