# Q217 — What Q209 needs (named, not patched)

Nothing below was changed. This is the list of what the Development start demanded and what the HTTP calls
showed missing. Paths are under `services/Diten.SupplyChainService/src/`.

## 1. To make the service start in Development

The container must be able to build all 18 handlers. Two ways exist; the choice is the integration owner's.

| Option | What it takes | Note |
|---|---|---|
| A. Register the four modules | `Program.cs` calls the four existing extension methods | They exist on disk: `AddReturnPersistence()` (`Persistence/Features/Returns/ReturnPersistenceRegistration.cs:7`), `AddClaimPersistence()` (`…/Claims/ClaimPersistenceRegistration.cs:9`), `AddSandopPersistence()` (`…/SandopPlans/SandopPersistenceRegistration.cs:4`), `AddCapacityPersistence()` (`…/CapacityPlans/CapacityPersistenceRegistration.cs:8`). Whether these four calls alone are enough is **not measured**: validation reports only the first missing parameter per handler, and Q211 lists more unregistered Claims types (`IClaimReferenceReader`, `Claims:ReferenceBaseUrl`). Expect a second round of errors. |
| B. Keep the modules out of the container | `Application/DependencyInjection.cs:11` stops scanning the held modules' handlers | Changes shared Application code; undoes part of what Q202a delivered. |

Until one of them lands, nobody can run this service with `ASPNETCORE_ENVIRONMENT=Development`, which is
what `.antigravity/rules/dev-runbook.md` prescribes for every service ("dotnet run (Development)").
The project has no `Properties/launchSettings.json`, so a plain `dotnet run` does not select Development by
itself; it runs as Production, where the fault is hidden until a request needs one of the 18 handlers.

## 2. What the service needs from configuration (not in `appsettings.json` today)

`appsettings.json` holds only `Urls`, `AllowedHosts`, `Logging` and `PlatformRegistration`. There is no
`appsettings.Development.json` and no `Properties/launchSettings.json`. The start demanded, fail-fast:

| Key | Demanded at | Supplied in this WP by |
|---|---|---|
| `JwtSettings:Secret` (≥ 32 bytes) | `Program.cs:25-26` | env var, random |
| `JwtSettings:Issuer` | `Program.cs:36` | env var |
| `JwtSettings:Audience` | `Program.cs:37` | env var |
| `Mongo:ConnectionString` | `Persistence/DependencyInjection.cs:28` | env var, lane set on 31994 |
| `Mongo:DatabaseName` | `Persistence/DependencyInjection.cs:29` | env var |

So a plain `dotnet run` fails before it even reaches the handler problem. Where these values come from in a
developer environment is undecided in the tree.

## 3. To make the half-wired routes behave as their own modules

Measured in `HTTP-EVIDENCE.md`: Claims and Returns requests are answered by the Shipment middleware.

- `Api/Program.cs:68` — exclude the Claims and Returns families from the Shipment branch and add their own
  branches (`ClaimContextMiddleware`, `ReturnContextMiddleware` exist under `Api/Features/`).
- `Api/Program.cs:55-63` — model-error adapters for the new families.
- S&OP and Capacity (`/api/supply-chain/…`) currently run with **no** module middleware; their
  `SandopContextMiddleware` and `CapacityContextMiddleware` exist but are not in the pipeline.

## 4. Not touched by this WP, still open

- Gateway: no supply-chain route (Q211 row 1). All calls here went straight to 5061.
- Permission keys for the four modules: no seed, no catalogue (Q211 row 11).
- `ocelot.json`, the port table, `appsettings`, every `.csproj`, test code and service code: unchanged.

## 5. What a follow-up needs to finish the measurement

One authenticated call per module. That needs a decision on the token: either a token signed with the
scratch secret of a lane run (test values only, as the host tests do in `ShipmentTests.cs:83-90`), or a real
token from the Auth service on 5056 with the service configured to the same issuer, audience and secret.
This WP was told not to invent a token, so it stopped at 401.
