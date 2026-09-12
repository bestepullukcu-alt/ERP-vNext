# Diten.CrmService Scaffold Implementation (MOD-0149-PREREQ)

**Date:** 2026-07-14 · **Type:** runtime scaffold implementation (skeleton-only) · **Verdict:** PASS

## Scope

MOD-0149-PREREQ (`ready-for-dev`, `runtime_code_scope: scaffold-only`) uyarınca `Diten.CrmService` 5-katman runtime
iskeleti oluşturuldu. **Hiçbir Account/CRM iş mantığı yok** — yalnız `/health` + altyapı wiring.

## Created (28 kaynak dosya, MdmService/HcmService pattern birebir)

- **Api:** csproj, Program.cs (port 5061, "Diten CRM Service"), CustomBaseController, launchSettings (5061), appsettings(.Development).
- **Application:** csproj, DependencyInjection (MediatR + 4 behavior + FluentValidation), Behaviors/* (Validation/Logging/Exception/Performance), Common/Models/Response, Common/ITenantContext + TenantContext.
- **Domain:** csproj (boş), DomainAssemblyMarker (entity YOK).
- **Persistence:** csproj (MongoDB.Driver), DependencyInjection (Guid serializer + Mongo client/database; collection/entity/repo YOK).
- **Infrastructure:** csproj (AspNetCore.App), DependencyInjection (TenantContext + HttpContextAccessor + generic permission authz), Middleware/TenantResolutionMiddleware, Authorization/* (HasPermissionAttribute, PermissionRequirement, PermissionPolicyProvider, PermissionAuthorizationHandler).
- **Tests:** Diten.CrmService.Application.Tests + ScaffoldSmokeTests (5 test).

## Validation

| Check | Result |
|---|---|
| `dotnet build` Api + 4 katman | **0 error / 0 warning** (her biri) |
| `dotnet test` scaffold smoke | **5/5 PASS** (DI resolves, tenant guard, response envelope, no-Account-type, no-AccountController) |
| Live boot (`dotnet run`, port 5061) | **Boot OK** |
| `GET /health` | **200** `{"status":"Healthy","service":"Diten.CrmService"}` |
| AccountController (source) | **Yok** (yalnız CustomBaseController) |
| Account/Customer/WorkPlace entity | **Yok** (0) |
| `/api/crm/accounts` route | **Yok** (0) |
| `[HasPermission("crm...")]` attribute / seed | **Yok** (yalnız doc comment) |
| frontend/** · gateway/** · ocelot.json | **Untouched** |

## Port

CRM = **5061** (Gateway 5000, Web 5001, Auth 5056, Platform 5057, DevEnb 5058, Mdm 5059, Hcm 5060; 5061 çakışmasız).

## Follow-ups (bu task kapsamı dışı)

- `execution/registries/module-implementation-status.md`'e `Diten.CrmService` scaffold satırı — kod-truth PR'ında (registries bu task'ın izin listesinde değil).
- `watch-diten-bg.ps1` fleet script'ine CRM/5061 eklenmesi (root operasyonel script; opsiyonel).
- Gateway `/api/crm/accounts*` downstream (5061) registration — **integration-agent**, MOD-0149 implementation ile.
- MOD-0149 Account implementation — ayrı task; scaffold PASS sonrası kullanıcı onayıyla MOD-0149 `ready-for-dev`.
