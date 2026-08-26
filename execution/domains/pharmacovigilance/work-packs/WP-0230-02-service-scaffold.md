---
id: WP-0230-02
title: Diten.PvgService scaffold
module: MOD-0230
service: Diten.PvgService
depends_on: [WP-0230-01]
gate: build/test only
status: ready
estimate: 1 d
---

# WP-0230-02 - `Diten.PvgService` scaffold

## Objective

Stand up the PVG service projects, host wiring, and cross-cutting behaviours - tenancy, correlation, JWT,
permission authorization, MediatR pipeline, Mongo connection, health checks - on port **5011**, mirroring
`Diten.MdmService` and `Diten.DevEnablementService`. No business logic.

## Preconditions

- [ ] **`rm -rf services/Diten.PvgService`** first. It currently exists as ignored `bin`/`obj` output with no tracked source and will collide.
- [ ] Port 5011 confirmed free: `lsof -nP -iTCP:5011 | grep LISTEN` returns nothing.

## File manifest

```text
services/Diten.PvgService/
├── Diten.PvgService.sln
├── src/
│   ├── Diten.Pvg.Domain/            Diten.Pvg.Domain.csproj
│   │   └── Entities/EntityBase.cs
│   ├── Diten.Pvg.Application/       Diten.Pvg.Application.csproj
│   │   ├── Common/ITenantContext.cs, TenantContext.cs
│   │   ├── Common/Models/Response.cs          (namespace Diten.Shared.Core)
│   │   ├── Behaviors/ValidationBehavior.cs, LoggingBehavior.cs,
│   │   │             ExceptionHandlingBehavior.cs, PerformanceBehavior.cs
│   │   ├── Interfaces/ICurrentUserContext.cs
│   │   ├── RegPvBase/**                        (from WP-01)
│   │   └── DependencyInjection.cs
│   ├── Diten.Pvg.Persistence/       Diten.Pvg.Persistence.csproj
│   │   ├── Repositories/RepositoryBase.cs
│   │   └── DependencyInjection.cs      registers IMongoClient + IMongoDatabase
│   ├── Diten.Pvg.Infrastructure/    Diten.Pvg.Infrastructure.csproj
│   │   ├── Authorization/HasPermissionAttribute.cs, PermissionRequirement.cs,
│   │   │                 PermissionAuthorizationHandler.cs, PermissionPolicyProvider.cs
│   │   └── DependencyInjection.cs
│   └── Diten.Pvg.API/               Diten.Pvg.API.csproj
│       ├── Program.cs
│       ├── Controllers/CustomBaseController.cs
│       ├── Health/  (readiness + liveness)
│       ├── Properties/launchSettings.json      applicationUrl http://localhost:5011
│       ├── appsettings.json
│       └── appsettings.Development.json
└── tests/
    └── Diten.Pvg.Application.Tests/ Diten.Pvg.Application.Tests.csproj
```

Also edit (append one line each):

- `run_all.sh` - add `dotnet build services/Diten.PvgService/src/Diten.Pvg.API/Diten.Pvg.API.csproj -v q` to the build block, and the corresponding `dotnet run` entry to the run block, both following the existing pattern.

## Implementation spec

### Target framework and packages

`net8.0`, `Nullable=enable`, `ImplicitUsings=enable`. Match the versions already in the repo - do not upgrade:

| Package | Version | Project |
|---|---|---|
| `MediatR` | 12.3.0 | Application, API |
| `FluentValidation.DependencyInjectionExtensions` | 12.1.1 | Application |
| `Microsoft.Extensions.Logging.Abstractions` | 8.0.1 | Application |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.11 | API |
| `System.IdentityModel.Tokens.Jwt` | 8.16.0 | API |
| `Swashbuckle.AspNetCore` | 6.6.2 | API |
| `MongoDB.Driver` | match `Diten.MdmService.Persistence` | Persistence |

Project references: `API → Application, Infrastructure, Persistence`; `Application → Domain`;
`Persistence → Application, Domain`; `Infrastructure → Application`.
Also reference `services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj` from
Application and API - that is where `IEntitlementChecker`, `ITenantAuthorizationContext`, `ICorrelationContext`,
`CorrelationIdMiddleware`, `SensitiveDataRedactor`, and `TenantResolutionMiddleware` live. Do **not** copy those
types; reference them.

### `EntityBase` - copy the MdmService shape exactly

```csharp
namespace Diten.Pvg.Domain.Entities;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }          // non-nullable: PVG is tenant-owned
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public int Version { get; set; }             // concurrency only - never business versioning
}
```

Reference: `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs`.
`TenantId` is **non-nullable** here, unlike the DevEnablement copy, because PVG has no global reference data.

### `Response<T>`

Copy `services/Diten.DevEnablementService/src/.../Application/Common/Models/Response.cs` verbatim, keeping
namespace `Diten.Shared.Core`. Do not redesign the envelope.

### `Program.cs` wiring order

1. Serilog with `SensitiveDataLogEventEnricher` from `Diten.Platform.Common.Observability`.
2. `CorrelationIdMiddleware` - must run before anything that logs or audits.
3. JWT bearer auth, mirroring `Diten.MdmService` `Program.cs`.
4. `TenantResolutionMiddleware` from `Diten.Platform.Common.Tenancy`.
5. `PermissionPolicyProvider` + `PermissionAuthorizationHandler`.
6. MediatR + the four pipeline behaviours.
7. `services.AddRegPvBasePorts(builder.Configuration, builder.Environment);` (WP-01).
8. Mongo, health checks (`MongoDbReadinessHealthCheck` from Platform.Common), Swagger in Development only.

### Mongo registration - mirror MdmService, not EnterpriseStrategy

`Diten.MdmService` registers Mongo directly in `Persistence/DependencyInjection.cs`:

```csharp
var client = new MongoClient(MongoClientSettings.FromConnectionString(connectionString));
services.AddSingleton<IMongoClient>(client);
services.AddScoped<IMongoDatabase>(_ => client.GetDatabase(databaseName));
```

Copy that. **Do not** introduce a `MongoDbContext` wrapper - only `Diten.EnterpriseStrategyService` uses one,
and `RepositoryBase<T>` takes `IMongoDatabase` directly. Reference:
`services/Diten.MdmService/src/Diten.MdmService.Persistence/{DependencyInjection.cs,Repositories/RepositoryBase.cs}`.

### `launchSettings.json`

`"applicationUrl": "http://localhost:5011"` and `ASPNETCORE_URLS=http://localhost:5011`.

### `appsettings.Development.json`

```jsonc
{
  "Pvg": { "RegPvBase": { "UseNonProductionAdapters": true } },
  "MongoDb": { "ConnectionString": "…", "DatabaseName": "diten_pvg" }
}
```

The `UseNonProductionAdapters` key must appear **only** here.

## Forbidden

- Editing `.antigravity/rules/ports.md`. Port 5011 registration is a separate approval; record it in the pack, not the protected file.
- Any `ocelot.json` change - that is WP-06.
- Any frontend change - that is WP-07.
- Any entity beyond `EntityBase`, any repository beyond `RepositoryBase<T>`, any controller beyond `CustomBaseController`.
- Seed data, `IModuleSeedDataInitializer`, module manifest providers, background jobs, Hangfire.
- Copying `Delete*`, `BulkDelete*`, or export scaffolding from the Golden Reference template.

## Acceptance criteria

- [ ] `dotnet build services/Diten.PvgService/src/Diten.Pvg.API/Diten.Pvg.API.csproj -v q` succeeds.
- [ ] Service starts on 5011 and `/health/live` + `/health/ready` respond.
- [ ] A request without a JWT gets 401; with a JWT lacking the permission, 403.
- [ ] `X-Correlation-Id` is echoed on responses and present in logs.
- [ ] `TenantId` is resolved from `ITenantContext`, never from a request body.
- [ ] `AddRegPvBasePorts` is registered and the three deny defaults resolve from DI.
- [ ] Startup **throws** with `ASPNETCORE_ENVIRONMENT=Production` and `UseNonProductionAdapters=true`.
- [ ] `run_all.sh` builds and runs the new service alongside the existing six.
- [ ] No file outside the manifest changed except `run_all.sh`.

## Tests

- Host startup smoke: builds the DI container and resolves `IPvgFieldSecurityPolicy`, `IPvgWorkflowTransitionGate`, `IPvgEvidenceLinkPort` - all deny adapters by default.
- Production guard: `WebApplicationFactory` with `ASPNETCORE_ENVIRONMENT=Production` and the switch on → expects `InvalidOperationException` containing `PVG-REGPVBASE-001`.
- Config scan: assert `appsettings.json` does not contain `UseNonProductionAdapters`.

## Verify

```bash
lsof -nP -iTCP:5011 | grep LISTEN            # expect empty before starting
dotnet build services/Diten.PvgService/src/Diten.Pvg.API/Diten.Pvg.API.csproj -v q
dotnet run   --project services/Diten.PvgService/src/Diten.Pvg.API/Diten.Pvg.API.csproj &
curl -s http://localhost:5011/health/ready
```

## Agent prompt

> Implement WP-0230-02 in `/Users/natig/Projects/ERP-vNext-recovery`.
>
> Read first: `execution/domains/pharmacovigilance/work-packs/WP-0230-02-service-scaffold.md`,
> `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`,
> `execution/domains/pharmacovigilance/domain-config.md`, `AGENTS.md`,
> `.antigravity/rules/{ports,routes,multi-tenancy,security-jwt,response-envelope,entity-base-template,logging-observability}.md`.
>
> First delete the stale ignored folder: `rm -rf services/Diten.PvgService`.
>
> Scaffold `Diten.PvgService` on port 5011 by mirroring `Diten.MdmService` for structure and
> `Diten.DevEnablementService` for the response envelope, controller base, and permission attribute. Reference
> `Diten.Platform.Common` for authorization, tenancy, correlation, and redaction - do not copy those types.
>
> No entities beyond `EntityBase`, no repositories beyond `RepositoryBase<T>`, no controllers beyond
> `CustomBaseController`, no seed data, no manifest provider, no gateway or frontend change.
>
> Do not edit `.antigravity/rules/ports.md` - it is protected. Add the two `run_all.sh` lines.
>
> Report build output and the `/health/ready` response.
