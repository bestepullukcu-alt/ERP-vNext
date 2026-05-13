# 01_repo_map_output.md — Platform Repo Map

**Status:** Ready

## Backend placement

| Area | Actual path | Notes | Safe for changes? |
|---|---|---|---|
| Backend umbrella root | `services` | Use this as the top-level backend boundary for Platform work | Y |
| Platform API/controllers | `services/Diten.Platform/src/Diten.Platform.API/Controllers` | HTTP route handlers | Y |
| Web middleware | `services/Diten.Platform/src/Diten.Platform.API/Middleware` | Includes correlation middleware | Y |
| Health checks | `services/Diten.Platform/src/Diten.Platform.API/Health` | Service health registration | Y |
| Auth/permissions wiring | `services/Diten.Platform/src/Diten.Platform.API/Security` | Security attributes / authz hooks | Y |
| API bootstrap / runtime entry | `services/Diten.Platform/src/Diten.Platform.API/Program.cs` | Main DI + middleware pipeline | Y |
| Application/services | `services/Diten.Application/Services` | Service layer | Y |
| Commands | `services/Diten.Application/Commands` | Command-side application logic | Y |
| Queries | `services/Diten.Application/Queries` | Query-side application logic | Y |
| Handlers | `services/Diten.Application/Handlers` | Request/event handling seams | Y |
| Domain/models | `services/Diten.Domain/Aggregates` | Domain aggregates | Y (except protected paths) |
| Repositories/data access | `services/Diten.Persistence/Repositories` | Repository implementations | Y |
| DB context | `services/Diten.Persistence/Context/MongoDbContext.cs` | Mongo context | Y |
| Persistence DI registration | `services/Diten.Persistence/DependencyInjection.cs` | Persistence service wiring | Y |
| Infrastructure DI registration | `services/Diten.Infrastructure/DependencyInjection.cs` | Infra service wiring | Y |
| Seed/init routines | `services/Diten.Persistence/DbInitializer.cs` | Startup seed pattern | Y |
| Event/integration area | `services/Diten.Application/Commands` and `services/Diten.Application/Handlers` | Current best-fit integration seam | Y |
| Observability/correlation middleware | `services/Diten.Platform/src/Diten.Platform.API/Middleware/CorrelationIdMiddleware.cs` | Correlation propagation hook | Y |

## Frontend placement

| Area | Actual path | Notes | Safe for changes? |
|---|---|---|---|
| Platform frontend module root | `frontend/Diten.Web` | ASP.NET Core MVC WebUI root | Y |
| Route entry | `frontend/Diten.Web/Program.cs` | Frontend app startup | Y |
| Navigation/menu registration | `frontend/Diten.Web/Views/Shared/_Layout.cshtml` | Global shell / menu placement | Y |
| Page shell/layout | `frontend/Diten.Web/Views/Shared/_Layout.cshtml` | Primary page shell | Y |
| Controllers | `frontend/Diten.Web/Controllers` | MVC page controllers | Y |
| Views | `frontend/Diten.Web/Views` | Feature views | Y (except protected paths) |
| Shared view components | `frontend/Diten.Web/Views/Shared` | Shared layout / validation partials | Y |
| Static assets | `frontend/Diten.Web/wwwroot` | JS / CSS / assets | Y |
| Frontend tests | `frontend/Diten.Web/tests` | Vitest-based tests | Y |

## Protected paths (do-not-touch)

| Path | Why protected |
|---|---|
| `services/Diten.Domain/Aggregates/DemandIdea` | Distinct business model outside Platform scope |
| `services/Diten.Application/EnterpriseStrategy` | Existing business-domain application logic |
| `frontend/Diten.Web/Views/DemandIdeas` | Business-domain UI outside Platform scope |
| `frontend/Diten.Web/Views/DeliveryExecutionManagement` | Business-domain UI outside Platform scope |
| `frontend/Diten.Web/Views/EnterpriseStrategyBusinessPerformance` | Core ES&BP UI and business logic; avoid cross-cutting contamination |
