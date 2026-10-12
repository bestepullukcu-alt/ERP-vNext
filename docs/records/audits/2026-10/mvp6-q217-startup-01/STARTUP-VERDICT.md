# Q217 — Startup verdict

## Verdict (Development): FAILS AT STARTUP

The service does not start in the `Development` environment. It exits with code 1 at
`Program.cs:64` (`builder.Build()`), before Kestrel listens. Port 5061 never opened.

This answers F-Q211-7: yes, Q202a broke Development startup for the whole service, MOD-0183 included.

## How it was started

Working directory `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api` (so `appsettings.json`
loads unchanged; it sets `Urls = http://127.0.0.1:5061`). Port 5061 was free.

```text
ASPNETCORE_ENVIRONMENT=Development   DOTNET_ENVIRONMENT=Development
Mongo__ConnectionString=mongodb://127.0.0.1:31994/?replicaSet=rsq208s1&serverSelectionTimeoutMS=5000
Mongo__DatabaseName=diten_q217_startup
JwtSettings__Secret=<64 random hex characters, generated in the shell, never printed or stored>
JwtSettings__Issuer=q217-startup     JwtSettings__Audience=q217-startup
dotnet bin/Debug/net8.0/Diten.SupplyChainService.Api.dll
```

Environment variables only. No file was edited. Process start 2026-10-02 23:01 +03; gone within 0.5 s.

## The exception

```text
Unhandled exception. System.AggregateException: Some services are not able to be constructed
 (Error while validating the service descriptor 'ServiceType: MediatR.IRequestHandler`2[…SandopPlans.Queries.GetSandopPlanQuery,…SandopResult]
  Lifetime: Transient ImplementationType: …SandopPlans.Handlers.QueryHandlers.GetSandopPlanHandler':
  Unable to resolve service for type 'Diten.SupplyChainService.Domain.Features.SandopPlans.ISandopRepository'
  while attempting to activate '…GetSandopPlanHandler'.) (… 17 more …)
   at Microsoft.Extensions.DependencyInjection.ServiceProvider.ValidateService(ServiceDescriptor descriptor)
   at Microsoft.Extensions.DependencyInjection.ServiceProvider..ctor(ICollection`1 serviceDescriptors, ServiceProviderOptions options)
   at Microsoft.Extensions.Hosting.HostApplicationBuilder.Build()
   at Microsoft.AspNetCore.Builder.WebApplicationBuilder.Build()
   at Program.<Main>$(String[] args) in …/Diten.SupplyChainService.Api/Program.cs:line 64
```

18 inner exceptions (#0–#17), one per handler. Full log: `/private/tmp/q217-startup-01/service.log` (204 lines).

## Unresolved dependencies — 18 handlers, 4 repository interfaces

| Unresolved type | Module | Handlers that need it |
|---|---|---|
| `Domain.Features.Returns.IReturnRepository` | MOD-0186 | `CreateReturnHandler`, `GetReturnListHandler`, `TransitionReturnHandler` |
| `Domain.Features.Claims.IClaimRepository` | MOD-0187 | `CreateClaimHandler`, `GetClaimListHandler`, `TransitionClaimHandler` |
| `Domain.Features.SandopPlans.ISandopRepository` | MOD-0190 | `CaptureSandopSnapshotHandler`, `CreateSandopPlanHandler`, `GetSandopPlanHandler`, `ListSandopSignOffsHandler`, `ListSandopSnapshotsHandler`, `RecordSandopSignOffHandler` |
| `Domain.Features.CapacityPlans.ICapacityRepository` | MOD-0192 | `CreateCapacityPlanHandler`, `CreateCapacityScenarioHandler`, `EvaluateCapacityScenarioHandler`, `GetCapacityEvaluationHandler`, `GetCapacityPlanHandler`, `GetCapacityScenarioHandler` |

Container validation stops at the first constructor parameter it cannot resolve, so this list is a lower
bound per handler. REACHABILITY.md (Q211) names further unregistered Claims types (`ClaimRequestContext`,
`IClaimReferenceReader`); this run neither confirms nor excludes them.

Mechanism, as measured: `Application/DependencyInjection.cs:11` registers every MediatR handler in the
Application assembly; `Program.cs:46-47` registers only Carrier and Load persistence; in Development the
host validates every registration when the container is built.

## Supplementary run (not the verdict): it boots outside Development

Same binary, same variables, only `ASPNETCORE_ENVIRONMENT=Production` and `DOTNET_ENVIRONMENT=Production`.
Container validation is off outside Development, so the same registrations are not checked at start.

```text
[23:02:17 INF] User profile is available. Using '/Users/natig/.aspnet/DataProtection-Keys' as key repository; keys will not be encrypted at rest.
[23:02:18 WRN] Shipment outbox transport is not registered. Durable events remain pending; integration is held.
[23:02:18 INF] Now listening on: http://127.0.0.1:5061
[23:02:18 INF] Application started. Press Ctrl+C to shut down.
[23:02:18 INF] Hosting environment: Production
[23:02:18 INF] Content root path: …/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api
```

It stayed up from 23:02:18 to 23:02:54 (pid 15026) and answered 20 requests; it was then stopped with SIGINT
(`Application is shutting down...`, exit code 0). Full log: `/private/tmp/q217-startup-01/service-run2-production.log`.

In this mode the fault is deferred, not removed: the 18 handlers stay unresolvable and would fail when first
requested. That per-request failure was **not observed**, because no request got past authentication
(`HTTP-EVIDENCE.md`).

| Environment | Result | Measured |
|---|---|---|
| Development | FAILS AT STARTUP | yes, this WP |
| Production (supplementary) | BOOTS | yes, this WP |
| Testing (host tests) | boots | Q208 (not re-run here) |
| Any, authenticated call to an unwired module | expected to fail on resolution | **not measured** |
