# Q232 — Startup and HTTP

## Verdict (Development): STILL FAILS AT STARTUP with the four lines applied

Measured on a scratch copy holding the proposed `Program.cs`; the repo was not changed (`SOP-22.md` §1).

## How it was started

Working directory: the copy's `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api`, so
`appsettings.json` loads unchanged (`Urls = http://127.0.0.1:5061`). Port 5061 was free. Script: `evidence/run.sh`.

```text
ASPNETCORE_ENVIRONMENT=Development   DOTNET_ENVIRONMENT=Development
Mongo__ConnectionString=mongodb://127.0.0.1:31994/?replicaSet=rsq208s1&serverSelectionTimeoutMS=5000
Mongo__DatabaseName=diten_q232_unbreak
JwtSettings__Secret=<64 random hex characters from `openssl rand`, per run, never printed or stored>
JwtSettings__Issuer=q232-unbreak     JwtSettings__Audience=q232-unbreak
dotnet bin/Debug/net8.0/Diten.SupplyChainService.Api.dll
```

Environment variables only. No appsettings, `.csproj` or `ocelot.json` was edited. Nothing pointed at 27017.

## The exception (run A, 2026-10-02 23:17:30 +03; run C identical)

`System.AggregateException: Some services are not able to be constructed` at `Program.cs:72` (`builder.Build()`),
exit code 134, 13 inner exceptions:

| Unresolved type | Count | Who needs it | An implementation exists at (`src/…Infrastructure/Features/`) |
|---|---:|---|---|
| `Domain.Features.SandopPlans.IDemandFixtureReader` | 3 | `CaptureSandopSnapshotHandler`, `CreateSandopPlanHandler`, `RecordSandopSignOffHandler` | `SandopPlans/DemandFixtureReader.cs:5` (needs `IEnumerable<DemandFixture>`) |
| `Application.Features.Returns.IReturnReferenceReader` | 1 | `CreateReturnHandler` | `Returns/ReturnReferenceReader.cs:10` (typed `HttpClient` + configuration) |
| `Application.Features.Claims.IClaimReferenceReader` | 2 | `CreateClaimHandler`, `TransitionClaimHandler` | `Claims/ClaimReferenceReader.cs:8` (typed `HttpClient` + configuration) |
| `Domain.Features.CapacityPlans.IDemandFixtureReader` | 7 | `CapacityRepository` itself and, through it, all six Capacity handlers | `CapacityPlans/DemandFixtureReader.cs:3` |

- None of the four types is registered anywhere under `src/` (searched for `AddSingleton/AddScoped/AddTransient/
  AddHttpClient/TryAdd` naming them: 0 hits).
- This list is again a lower bound. `CapacityRepository.cs:6` also takes `IConstraintFixtureReader`, which is
  equally unregistered and would be reported next; `TransitionReturnHandler` was not checked.
- Registering these is outside the four lines and was **not done** (task 3; stop condition respected in spirit:
  no appsettings, `.csproj` or `ocelot.json` edit would have helped either).

## Task 7 — unauthenticated GETs in Development: NOT POSSIBLE

The process never listens in Development, so no request could be sent. The comparison with Q217 the WP asks for
therefore cannot be made in Development.

## Supplementary run (not the verdict): Production environment, proposed bytes

Same binary and variables, only the environment name changed. Container validation is off outside Development.

- Listening 23:18:14 +03 (pid 17713); `/health` 200 at 23:18:15 and again at 23:18:45; stopped 23:24:47,
  graceful (`Application is shutting down...`). Log: `evidence/service-prod.log`.
- It ran about 6 minutes longer than the planned 30 s: the script's SIGINT was ignored (background job of a
  non-interactive shell); it was stopped with SIGTERM. Nothing else was running against it.

| Request | Headers sent | Status | Body | Correlation header | Same as Q217 Production? |
|---|---|---|---|---|---|
| GET `/health` | scope + correlation | 200 | status JSON, `module: MOD-0183` | none | yes |
| GET `/api/shipment-bundle/shipments` | scope + correlation | 401 | Shipment contract error, "Authentication required." | echoed | yes |
| GET `/api/shipment-bundle/shipments` | none | 400 | Shipment contract error, correlation required | none | yes |
| GET `/api/shipment-bundle/claims` | scope + correlation | 401 | **Shipment** contract error | echoed | yes |
| GET `/api/shipment-bundle/claims` | none | 400 | **Shipment** contract error | none | yes |
| GET `/api/shipment-bundle/returns` | scope + correlation | 401 | **Shipment** contract error | echoed | yes |
| GET `/api/shipment-bundle/carriers` | scope + correlation | 401 + `WWW-Authenticate` | Carrier contract error | echoed | yes |
| GET `/api/shipment-bundle/loads` | scope + correlation | 401 + `WWW-Authenticate` | Load contract error | echoed | yes |
| GET `/api/supply-chain/sandop-plans/{id}` | scope + correlation | 401 + `WWW-Authenticate` | empty | none | yes |
| GET `/api/supply-chain/capacity-plans/{id}` | scope + correlation | 401 + `WWW-Authenticate` | empty | none | yes |

Raw captures: `evidence/http-production-supplementary.txt`. No token was sent; nothing needed redacting.

- At HTTP level nothing changed versus Q217: Claims and Returns are still answered by the Shipment middleware
  (`Program.cs:68` before, `:76` after). Expected — that exclusion belongs to Q209.
- No request reached a controller, so this says nothing about authenticated behaviour.

## What the Production run left on the lane mongod

Database `diten_q232_unbreak` on `127.0.0.1:31994`: 32 collections with their indexes, created by the hosted
schema services at start. By family: Shipment 8, Carrier 3, Load 5, Returns 5, Claims 4, Capacity 7,
**S&OP 0**. Left in place; nothing was dropped.

The zero is the known consequence measured directly: `AddSandopPersistence()` registers no hosted schema, so the
service boots without any `sandop_*` collection or unique index (F-Q221-4, Q220 blocked).
