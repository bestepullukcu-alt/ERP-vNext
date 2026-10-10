# Q221 — `Program.cs` proposal for Q209 (SupplyChain service) — PROPOSAL ONLY, nothing applied

Target: `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs`. This file was **not** edited, built or run.

| Version | sha256 | Lines | Source |
|---|---|---:|---|
| Working tree today | `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8` | 73 | read at 2026-10-02 21:02 +03 |
| BASE (decided base, do not re-open) | `33027bcd65b7274eda322578da15ef7fa9b9ce6d6d9ef75fe01eb8bc25b29752` | 123 | member `…/Diten.SupplyChainService.Api/Program.cs` of `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` (`7b6a0d1a…`), read in memory |
| **Proposed, step P0** = BASE verbatim | `33027bcd65b7274eda322578da15ef7fa9b9ce6d6d9ef75fe01eb8bc25b29752` | 123 | lines 1-89 and 94-127 below |
| **Proposed, step P1** = BASE + 4 draft provider lines | `22d426c50fa830c75e1fc3fc80c2362ed11d447d0dd67fef95a6f3cec252c358` | 127 | the whole listing below (LF line ends, final newline, as BASE) |

- P0 can be applied as soon as its preconditions hold (`INTEGRATION-ORDER.md`). Every type BASE names exists in the tree today (checked by grep, §4).
- P1 lines 90-93 name four classes that are **not in the tree** (`grep` = 0 for each). Each line goes in only together with its module's provider file (UI draft; Q202b HELD). Until then the file is P0.
- Nothing here picks an answer where no record decides one. Those points are in `OPEN-DECISIONS.tsv` and are marked `OD-n` in the table.

## 1. Proposed file content (P1; drop lines 90-93 for P0)

```csharp
  1  using Diten.SupplyChainService.Api.Features.Returns;
  2  using CapacityApi = Diten.SupplyChainService.Api.Features.CapacityPlans;
  3  using CapacityApplication = Diten.SupplyChainService.Application.Features.CapacityPlans;
  4  using CapacityDomain = Diten.SupplyChainService.Domain.Features.CapacityPlans;
  5  using CapacityInfrastructure = Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
  6  using Diten.SupplyChainService.Persistence.Features.CapacityPlans;
  7  using Diten.SupplyChainService.Persistence.Features.SandopPlans;
  8  using Diten.SupplyChainService.Domain.Features.SandopPlans;
  9  using Diten.SupplyChainService.Infrastructure.Features.SandopPlans;
 10  using Diten.SupplyChainService.Application.Features.Returns;
 11  using Diten.SupplyChainService.Persistence.Features.Returns;
 12  using Diten.SupplyChainService.Infrastructure.Features.Returns;
 13  using Diten.SupplyChainService.Api.Features.Claims;
 14  using Diten.SupplyChainService.Application.Features.Claims;
 15  using Diten.SupplyChainService.Persistence.Features.Claims;
 16  using Diten.SupplyChainService.Infrastructure.Features.Claims;
 17  using Diten.SupplyChainService.Api.Features.Loads;
 18  using Diten.SupplyChainService.Api.ModuleRegistration;
 19  using Diten.SupplyChainService.Application.Features.Loads;
 20  using Diten.SupplyChainService.Persistence.Features.Loads;
 21  using Diten.SupplyChainService.Infrastructure.Features.Loads;
 22  using Diten.SupplyChainService.Api.Features.Carriers;
 23  using Diten.SupplyChainService.Application.Features.Carriers;
 24  using Diten.SupplyChainService.Persistence.Features.Carriers;
 25  using System.Text;
 26  using Diten.BuildingBlocks.Security.Secrets;
 27  using System.Text.Json.Serialization;
 28  using Diten.SupplyChainService.Api.Middleware;
 29  using Diten.SupplyChainService.Application;
 30  using Diten.SupplyChainService.Application.Common;
 31  using Diten.SupplyChainService.Persistence;
 32  using Diten.SupplyChainService.Infrastructure.Eventing;
 33  using Microsoft.AspNetCore.Authentication.JwtBearer;
 34  using Microsoft.AspNetCore.Mvc;
 35  using Microsoft.IdentityModel.Tokens;
 36  using Serilog;
 37  
 38  var builder = WebApplication.CreateBuilder(args);
 39  var strictUtf8RequestHeaders = new UTF8Encoding(false, true);
 40  builder.WebHost.ConfigureKestrel(o =>
 41  {
 42      o.Limits.MaxRequestHeadersTotalSize = 64 * 1024;
 43      o.RequestHeaderEncodingSelector = name =>
 44          name.Equals("Idempotency-Key", StringComparison.OrdinalIgnoreCase) ? strictUtf8RequestHeaders : null;
 45  });
 46  builder.Host.UseSerilog((context, logger) => logger.MinimumLevel.Information().Enrich.FromLogContext().WriteTo.Console());
 47  string Required(string key) => !string.IsNullOrWhiteSpace(builder.Configuration[key]) ? builder.Configuration[key]! : throw new InvalidOperationException(key + " is required.");
 48  var secret = Required("JwtSettings:Secret");
 49  if (Encoding.UTF8.GetByteCount(secret) < 32) throw new InvalidOperationException("JWT signing secret must be at least 32 bytes.");
 50  builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
 51  {
 52      o.MapInboundClaims = false;
 53      o.TokenValidationParameters = new TokenValidationParameters
 54      {
 55          ValidateIssuer = true,
 56          ValidateAudience = true,
 57          ValidateLifetime = true,
 58          ValidateIssuerSigningKey = true,
 59          ValidIssuer = Required("JwtSettings:Issuer"),
 60          ValidAudience = Required("JwtSettings:Audience"),
 61          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
 62          ClockSkew = JwtValidationDefaults.ClockSkew,
 63          ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
 64      };
 65  });
 66  builder.Services.AddAuthorization();
 67  builder.Services.AddApplication();
 68  builder.Services.AddPersistence(builder.Configuration);
 69  builder.Services.AddCarrierPersistence();
 70  builder.Services.AddLoadPersistence();
 71  builder.Services.AddClaimPersistence();
 72  builder.Services.AddReturnPersistence();
 73  builder.Services.AddSandopPersistence();
 74  builder.Services.AddCapacityPersistence();
 75  builder.Services.AddSingleton<CapacityDomain.IDemandFixtureReader, CapacityInfrastructure.DemandFixtureReader>();
 76  builder.Services.AddSingleton<CapacityDomain.IConstraintFixtureReader, CapacityInfrastructure.ConstraintFixtureReader>();
 77  builder.Services.AddHostedService<CapacityInfrastructure.CapacityEvaluationExecutor>();
 78  var sandopFixtures = builder.Environment.IsEnvironment("Testing")
 79      ? builder.Configuration.GetSection("Sandop:DemandFixtures").Get<DemandFixture[]>() ?? []
 80      : [];
 81  builder.Services.AddSingleton<IDemandFixtureReader>(new DemandFixtureReader(sandopFixtures));
 82  builder.Services.AddHttpClient<IReturnReferenceReader, ReturnReferenceReader>(client => client.Timeout = TimeSpan.FromSeconds(5));
 83  builder.Services.AddHttpClient<IClaimReferenceReader, ClaimReferenceReader>(client => client.Timeout = TimeSpan.FromSeconds(5));
 84  builder.Services.AddHttpClient<ILoadReferenceReader, LoadReferenceReader>(client => client.Timeout = TimeSpan.FromSeconds(5));
 85  builder.Services.AddHostedService<ShipmentOutboxWorker>();
 86  builder.Services.Configure<PlatformRegistrationOptions>(builder.Configuration.GetSection(PlatformRegistrationOptions.SectionName));
 87  builder.Services.AddHttpClient();
 88  builder.Services.AddSingleton<IModuleManifestProvider, CarrierManagementManifestProvider>();
 89  builder.Services.AddSingleton<IModuleManifestProvider, ShipmentTrackingPodManifestProvider>();
 90  builder.Services.AddSingleton<IModuleManifestProvider, SopWorkflowSignoffsManifestProvider>();
 91  builder.Services.AddSingleton<IModuleManifestProvider, CapacityPlanningManifestProvider>();
 92  builder.Services.AddSingleton<IModuleManifestProvider, ReverseLogisticsManifestProvider>();
 93  builder.Services.AddSingleton<IModuleManifestProvider, ClaimsManagementManifestProvider>();
 94  builder.Services.AddHostedService<ModuleRegistrationHostedService>();
 95  builder.Services.AddControllers().AddJsonOptions(o =>
 96  {
 97      o.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
 98      o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
 99  });
100  builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = c =>
101  {
102      if (CapacityApi.CapacityContextMiddleware.IsCapacityPath(c.HttpContext))
103          return new BadRequestObjectResult(CapacityApi.CapacityContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<CapacityApplication.CapacityRequestContext>().CorrelationId));
104      if (ReturnContextMiddleware.IsReturnPath(c.HttpContext))
105          return new BadRequestObjectResult(ReturnContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<ReturnRequestContext>().CorrelationId));
106      if (ClaimContextMiddleware.IsClaimPath(c.HttpContext))
107          return new BadRequestObjectResult(ClaimContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<ClaimRequestContext>().CorrelationId));
108      if (LoadContextMiddleware.IsLoadPath(c.HttpContext))
109          return new BadRequestObjectResult(LoadContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<LoadRequestContext>().CorrelationId));
110      if (CarrierContextMiddleware.IsCarrierPath(c.HttpContext))
111          return new BadRequestObjectResult(CarrierContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<CarrierRequestContext>().CorrelationId));
112      var context = c.HttpContext.RequestServices.GetRequiredService<RequestContext>();
113      return new BadRequestObjectResult(ContractError.Create("INVALID_REQUEST", "Request schema validation failed.", context.CorrelationId == Guid.Empty ? Guid.NewGuid() : context.CorrelationId));
114  });
115  var app = builder.Build();
116  app.UseAuthentication();
117  app.UseWhen(CapacityApi.CapacityContextMiddleware.IsCapacityPath, branch => branch.UseMiddleware<CapacityApi.CapacityContextMiddleware>());
118  app.UseWhen(ReturnContextMiddleware.IsReturnPath, branch => branch.UseMiddleware<ReturnContextMiddleware>());
119  app.UseWhen(ClaimContextMiddleware.IsClaimPath, branch => branch.UseMiddleware<ClaimContextMiddleware>());
120  app.UseWhen(CarrierContextMiddleware.IsCarrierPath, branch => branch.UseMiddleware<CarrierContextMiddleware>());
121  app.UseWhen(LoadContextMiddleware.IsLoadPath, branch => branch.UseMiddleware<LoadContextMiddleware>());
122  app.UseWhen(context => !CapacityApi.CapacityContextMiddleware.IsCapacityPath(context) && !CarrierContextMiddleware.IsCarrierPath(context) && !LoadContextMiddleware.IsLoadPath(context) && !ClaimContextMiddleware.IsClaimPath(context) && !ReturnContextMiddleware.IsReturnPath(context) && !context.Request.Path.StartsWithSegments("/api/supply-chain/sandop-plans"), branch => branch.UseMiddleware<ShipmentContextMiddleware>());
123  app.UseAuthorization();
124  app.MapGet("/health", () => Results.Ok(new { status = "up", module = "MOD-0183", warehouseIntake = "unimplemented", outboxTransport = "integration-owned" })).AllowAnonymous();
125  app.MapControllers();
126  app.Run();
127  public partial class Program { }
```

The line numbers are not part of the file. BASE line n = proposed line n for n ≤ 89, and proposed line n + 4 for n ≥ 90.

## 2. Every line and its source

| Proposed line | Source | Evidence |
|---:|---|---|
| 1 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 2 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 3 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 4 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 5 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 6 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 7 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 8 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 9 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 10 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 11 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 12 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 13 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 14 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 15 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 16 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 17 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 18 | BASE (added) | `using` needed by the BASE lines below; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:28 |
| 19 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 20 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 21 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 22 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 23 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 24 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 25 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 26 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 27 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 28 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 29 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 30 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 31 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 32 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 33 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 34 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 35 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 36 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 37 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 38 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 39 | BASE (added) · OD-5 | Kestrel strict UTF-8 selector for `Idempotency-Key`; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:29 |
| 40 | BASE (reshaped) | multi-line form of working-tree line 22; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26 |
| 41 | BASE (reshaped) | same |
| 42 | BASE (reshaped) | same value as working-tree line 22 (64 KiB) |
| 43 | BASE (added) · OD-5 | docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:29 |
| 44 | BASE (added) · OD-5 | docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:29 |
| 45 | BASE (reshaped) | same |
| 46 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 47 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 48 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 49 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 50 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 51 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 52 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 53 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 54 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 55 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 56 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 57 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 58 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 59 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 60 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 61 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 62 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 63 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 64 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 65 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 66 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 67 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 68 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 69 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 70 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 71 | BASE (added) + VER record | docs/records/audits/2026-10/mvp6-q211-ver-0187-01/REACHABILITY.md:23 (row 4: `AddClaimPersistence()` never called); `ClaimPersistenceRegistration.cs:9-16` |
| 72 | BASE (added) + VER record | docs/records/audits/2026-10/mvp6-q210-ver-0186-01/REACHABILITY.md:57 (seam 1); `ReturnPersistenceRegistration.cs:7-15` |
| 73 | BASE (added) + VER record · OD-3 | docs/records/audits/2026-10/mvp6-q212-ver-0190-01/REACHABILITY.md:55 (seam 1); `SandopPersistenceRegistration.cs:4-5` registers the repository only — no index creation (F-Q212-4) |
| 74 | BASE (added) + VER record | docs/records/audits/2026-10/mvp6-q213-ver-0192-01/REACHABILITY.md:19 (row 4); `CapacityPersistenceRegistration.cs:8-15` |
| 75 | BASE (added) + VER record · trap 1 | docs/records/audits/2026-10/mvp6-q213-ver-0192-01/REACHABILITY.md:20 (row 5), :60-67; interface `Domain/Features/CapacityPlans/CapacityScope.cs:10`, class `Infrastructure/Features/CapacityPlans/DemandFixtureReader.cs:3` |
| 76 | BASE (added) + VER record | docs/records/audits/2026-10/mvp6-q213-ver-0192-01/REACHABILITY.md:20 (row 5); `CapacityScope.cs:14`, `ConstraintFixtureReader.cs:3` |
| 77 | BASE (added) + VER record · trap 2 · OD-1, OD-2 | docs/records/audits/2026-10/mvp6-q213-ver-0192-01/REACHABILITY.md:36-45; pack MOD-0192 :437-438 (approved composition includes the executor) |
| 78 | BASE (added) + VER record · trap 1 | docs/records/audits/2026-10/mvp6-q212-ver-0190-01/REACHABILITY.md:58 (seam 4: fixture only in a test environment); pack MOD-0190 :119 |
| 79 | BASE (added) + VER record · trap 1 | same; record type `Infrastructure/Features/SandopPlans/DemandFixtureReader.cs:3` |
| 80 | BASE (added) + VER record · trap 1 | same; outside `Testing` the list is empty, so every create is 422 by design (F-Q212-11) |
| 81 | BASE (added) + VER record · trap 1 | docs/records/audits/2026-10/mvp6-q212-ver-0190-01/REACHABILITY.md:55 (seam 1); interface `Domain/Features/SandopPlans/ISandopRepository.cs:3`, class `Infrastructure/Features/SandopPlans/DemandFixtureReader.cs:5` |
| 82 | BASE (added) + VER record · OD-4 | docs/records/audits/2026-10/mvp6-q210-ver-0186-01/REACHABILITY.md:57 (seam 1); config key `Returns:ReferenceBaseUrl`, `ReturnReferenceReader.cs:71` |
| 83 | BASE (added) + VER record · OD-4 | docs/records/audits/2026-10/mvp6-q211-ver-0187-01/REACHABILITY.md:24 (row 5); config key `Claims:ReferenceBaseUrl`, `ClaimReferenceReader.cs:88-89` |
| 84 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 85 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 86 | BASE (added) | registration foundation; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:32-33; `ModuleRegistration/PlatformRegistrationOptions.cs:5` (section `PlatformRegistration`, present in `appsettings.json:10-13`) |
| 87 | BASE (added) | `IHttpClientFactory` for `ModuleRegistrationHostedService.cs:20`; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:32 |
| 88 | BASE (added) | docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:33; class present in the tree (`ModuleRegistration/CarrierManagementManifestProvider.cs:5`) |
| 89 | BASE (added) | docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:33; class present (`ModuleRegistration/ShipmentTrackingPodManifestProvider.cs:5`) |
| 90 | draft provider (in no layer) · conditional | S&OP v3 `supplychain-Program.cs.patch.txt` in 2026-09/mvp6-sop-ui-draft-03/sop-ui-draft-overlay-v3.tar.gz `716e7c4c…`; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:37-46. The class is **absent from the tree** — the line compiles only after that draft's provider file lands |
| 91 | draft provider (in no layer) · conditional | Capacity v3 `supplychain-Program.cs.patch.txt` in 2026-09/mvp6-capacity-ui-draft-03/capacity-ui-draft-overlay-v3.tar.gz `5c0a3b61…`; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:37-46. The class is **absent from the tree** — the line compiles only after that draft's provider file lands |
| 92 | draft provider (in no layer) · conditional | Returns v3 `supplychain-Program.cs.patch.txt` in 2026-10/mvp6-returns-ui-draft-03/returns-ui-draft-overlay-v3.tar.gz `50724097…`; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:37-46. The class is **absent from the tree** — the line compiles only after that draft's provider file lands |
| 93 | draft provider (in no layer) · conditional | Claims v4 `supplychain-Program.cs.patch.txt` in 2026-09/mvp6-claims-ui-draft-04/claims-ui-draft-overlay-v4.tar.gz `2b34741a…`; docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:37-46. The class is **absent from the tree** — the line compiles only after that draft's provider file lands |
| 94 | BASE (added) | docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:33; skips itself when `InternalApiKey` is empty (`ModuleRegistrationHostedService.cs:44-48`; `appsettings.json:12` is empty) |
| 95 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 96 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 97 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 98 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 99 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 100 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 101 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 102 | BASE (added) + VER record | docs/records/audits/2026-10/mvp6-q213-ver-0192-01/REACHABILITY.md:23 (row 8: model-error adapter for Capacity) |
| 103 | BASE (added) + VER record | same |
| 104 | BASE (added) + VER record | docs/records/audits/2026-10/mvp6-q210-ver-0186-01/REACHABILITY.md:57 (seam 1: Returns branch in the model-state factory) |
| 105 | BASE (added) + VER record | same |
| 106 | BASE (added) + VER record | docs/records/audits/2026-10/mvp6-q211-ver-0187-01/REACHABILITY.md:27 (row 8) |
| 107 | BASE (added) + VER record | same |
| 108 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 109 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 110 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 111 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 112 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 113 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 114 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 115 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 116 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 117 | BASE (added) + VER record | docs/records/audits/2026-10/mvp6-q213-ver-0192-01/REACHABILITY.md:22 (row 7: Capacity middleware) |
| 118 | BASE (added) + VER record | docs/records/audits/2026-10/mvp6-q210-ver-0186-01/REACHABILITY.md:57 (seam 1: `UseWhen(ReturnContextMiddleware.IsReturnPath, …)`) |
| 119 | BASE (added) + VER record | docs/records/audits/2026-10/mvp6-q211-ver-0187-01/REACHABILITY.md:25 (row 6) |
| 120 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 121 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 122 | BASE (reshaped) + VER records · trap 3 | replaces working-tree line 68. Returns: docs/records/audits/2026-10/mvp6-q210-ver-0186-01/REACHABILITY.md:41-43, :57. Claims: docs/records/audits/2026-10/mvp6-q211-ver-0187-01/REACHABILITY.md:26 (row 7). S&OP: docs/records/audits/2026-10/mvp6-q212-ver-0190-01/REACHABILITY.md:56 (seam 2) — see §3: the S&OP and Capacity terms change nothing at run time (`ShipmentContextMiddleware.cs:9`) |
| 123 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 124 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 125 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 126 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |
| 127 | BASE = working tree | unchanged line (also in the working-tree file `7fdb5ef0…`); docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/SHARED-SEAM-PATCH-NEEDS.md:26-27 |

Counts: BASE (added): 47 · BASE (reshaped): 5 · BASE = working tree: 71 · draft provider (in no layer): 4 · total 127.

## 3. The five traps, resolved into exact text

### Trap 1 — two `IDemandFixtureReader` interfaces

Proposed lines 75 and 78-81 are already unambiguous as BASE writes them: Capacity is reached only through the aliases
`CapacityDomain` / `CapacityInfrastructure` (lines 4-5), and the unqualified names resolve to S&OP because only the S&OP
namespaces are imported (lines 8-9). The fully-qualified equivalents, for the reviewer and for any later edit:

```csharp
// MOD-0192 Capacity  (interface CapacityScope.cs:10 · class Infrastructure/Features/CapacityPlans/DemandFixtureReader.cs:3)
builder.Services.AddSingleton<Diten.SupplyChainService.Domain.Features.CapacityPlans.IDemandFixtureReader, Diten.SupplyChainService.Infrastructure.Features.CapacityPlans.DemandFixtureReader>();
builder.Services.AddSingleton<Diten.SupplyChainService.Domain.Features.CapacityPlans.IConstraintFixtureReader, Diten.SupplyChainService.Infrastructure.Features.CapacityPlans.ConstraintFixtureReader>();
// MOD-0190 S&OP      (interface ISandopRepository.cs:3 · class Infrastructure/Features/SandopPlans/DemandFixtureReader.cs:5, constructor takes IEnumerable<DemandFixture>)
builder.Services.AddSingleton<Diten.SupplyChainService.Domain.Features.SandopPlans.IDemandFixtureReader>(new Diten.SupplyChainService.Infrastructure.Features.SandopPlans.DemandFixtureReader(sandopFixtures));
```

- The two registrations have different service types, so neither replaces the other.
- The failure mode to avoid: adding `using Diten.SupplyChainService.Domain.Features.CapacityPlans;` or
  `using …Infrastructure.Features.CapacityPlans;` to this file. With both families imported, the unqualified names in lines
  79 and 81 become ambiguous (compile error CS0104). Keep the Capacity aliases.
- The S&OP class takes `IEnumerable<DemandFixture>` in its constructor; the Capacity class takes nothing. If the S&OP pair
  were registered as `AddSingleton<I, T>()`, the container would hand it an empty list and the configured test fixtures
  (line 79) would be ignored — by reading, not run. The instance form in line 81 is the one that carries them.

### Trap 2 — `CapacityEvaluationExecutor`: **recommend REGISTER (line 77), with two open decisions**

Reasons, each from a record:

- The pack lists "uptake of the accepted Capacity backend (43 paths + approved composition, **including the executor**)" as
  the integration owner's task — pack MOD-0192 :437-438.
- Without it, an evaluation is accepted (202) and never completes; the scenario stays blocked by its active slot —
  F-Q213-2; `REACHABILITY.md` (Q213) :40-45; `CapacityRepository.cs:159-160`.
- BASE registers it (BASE line 77), and BASE is the decided base.
- The lease gap does not bite with today's code: the executor claims and writes the terminal state in one pass with a
  literal fixture result and no work in between (`CapacityEvaluationExecutor.cs:21-32`), far inside the 30 s lease
  (`CapacityLeaseStore.cs:45`).

What stays open (not decided here):

- **OD-1** — the executor decision requires a separate 10 s heartbeat that renews a Running lease
  (`docs/roadmap/plans/mod-0192-executor-exact-decisions-01/DECISION.md:21`, sha256 `cfddf953…`; pack :255). The code never
  calls `RenewAsync` (F-Q213-7). Registering as-is starts a worker that does not match that decision line.
- **OD-2** — the worker's scan reads `capacity_evaluations` without a tenant filter every 10 s
  (`CapacityLeaseStore.cs:23-29`, F-Q213-6), while the same decision says tenant/LE filters are mandatory in every scan
  (`DECISION.md:27`). Registration turns that scan on.

What would have to change for the answer to be **do not register**: CT (or the pack owner) rules that `DECISION.md:21`
and/or `:27` are preconditions for start-up. Then the module lane must first change the executor (call `RenewAsync` on a
10 s heartbeat; scope the scan) inside its 43 owned paths — a DEV work package, not Q209 — and `AddCapacityPersistence()`
would be called without line 77. In that state `POST …/evaluations` must not be routed, because every evaluation would
stay Accepted forever.

### Trap 3 — the Shipment branch (working-tree line 68 → proposed line 122)

Exact expression (BASE line 118, verbatim):

```csharp
app.UseWhen(context => !CapacityApi.CapacityContextMiddleware.IsCapacityPath(context) && !CarrierContextMiddleware.IsCarrierPath(context) && !LoadContextMiddleware.IsLoadPath(context) && !ClaimContextMiddleware.IsClaimPath(context) && !ReturnContextMiddleware.IsReturnPath(context) && !context.Request.Path.StartsWithSegments("/api/supply-chain/sandop-plans"), branch => branch.UseMiddleware<ShipmentContextMiddleware>());
```

**Measured fact that changes the picture (F-Q221-1).** `ShipmentContextMiddleware.cs:9` is
`if (!http.Request.Path.StartsWithSegments("/api/shipment-bundle")) { await next(http); return; }`. S&OP is routed at
`api/supply-chain/sandop-plans` (`SandopPlansController.cs:4`). So for an S&OP request the Shipment middleware returns at
line 9 and never reaches its correlation check (:11-12), its scope check (:24) or its idempotency check (:32-34) — today,
with the working-tree line 68, and equally with the proposed line. The three contradictions in F-Q212-5 exist between the
two texts, but they cannot occur on an S&OP path. Q213 states the same for Capacity (`REACHABILITY.md` (Q213) :26-27).

Which contract wins, per path family:

| Path family | Correlation error | Scope mismatch | Idempotency key | Decided by |
|---|---|---|---|---|
| `/api/supply-chain/sandop-plans/**` | S&OP: 400 `INVALID_CORRELATION_ID` | S&OP: 403 `FORBIDDEN` | S&OP: nonempty exact value, no trim, no added maximum | The S&OP gate runs inside every action (`SandopContextMiddleware.cs:15`, `:17`, `:19`); pack MOD-0190 :129, :155, :156. The Shipment middleware is a pass-through here (`ShipmentContextMiddleware.cs:9`). **No owner decision needed.** |
| `/api/supply-chain/capacity-plans/**` | Capacity middleware | Capacity middleware | Capacity middleware | proposed line 117; Q213 `REACHABILITY.md` :22, :26-27 |
| `/api/shipment-bundle/returns/**` | Returns middleware | Returns middleware | Returns middleware | proposed line 118 + exclusion term `!ReturnContextMiddleware.IsReturnPath`; pack MOD-0186 :381, :386 via Q210 `REACHABILITY.md` :41-43. Here the exclusion **is** needed: the path starts with `/api/shipment-bundle` |
| `/api/shipment-bundle/claims/**` | Claims middleware | Claims middleware | Claims middleware | proposed line 119 + exclusion term `!ClaimContextMiddleware.IsClaimPath`; Q211 `REACHABILITY.md` :26 (row 7). Needed for the same reason |
| `/api/shipment-bundle/carriers/**`, `/loads/**` | their own middleware | same | same | unchanged from the working tree (lines 66-68 today) |
| `/api/shipment-bundle/shipments/**` and anything else under `/api/shipment-bundle` | Shipment: 400 with a Shipment message (`:12`) | Shipment: 404 `SHIPMENT_NOT_FOUND` (`:24`) | Shipment: 1-128 characters, stored trimmed (`:32-34`) | `ShipmentContextMiddleware.cs`; not in Q221's scope to judge |

- The S&OP and Capacity terms of the expression are therefore **redundant for behaviour** and harmless: they only stop the
  middleware from being constructed for those paths. Keeping them is BASE content (decided); removing them is not proposed.
- One service-wide rule does reach S&OP paths: the Kestrel strict UTF-8 selector for the `Idempotency-Key` header (proposed
  lines 39-45). A key with invalid UTF-8 bytes is rejected by the server before any S&OP code runs, with a server-level 400
  and no S&OP error body. Whether that satisfies the S&OP "exact parsed value" rule (pack :129, :155): **insufficient
  evidence — owner decision (OD-5)**.

### Trap 4 — persistence calls, readers and configuration keys

| Module | `Program.cs` lines (proposed numbering) | Configuration key | State of the key |
|---|---|---|---|
| Claims (MOD-0187) | 71 `AddClaimPersistence()` · 83 `AddHttpClient<IClaimReferenceReader, ClaimReferenceReader>(…5 s)` · 106-107 model-error branch · 119 middleware branch · 122 exclusion term | `Claims:ReferenceBaseUrl` — absolute http/https; the reader appends the path (`ClaimReferenceReader.cs:88-90`) | **not set anywhere** (`appsettings.json` has no `Claims` section). Without it: 503 `CLAIM_REFERENCE_UNAVAILABLE`. Value: **OD-4** |
| Returns (MOD-0186) | 72 `AddReturnPersistence()` · 82 `AddHttpClient<IReturnReferenceReader, ReturnReferenceReader>(…5 s)` · 104-105 model-error branch · 118 middleware branch · 122 exclusion term | `Returns:ReferenceBaseUrl` — absolute http/https; the reader appends `api/shipment-bundle/shipments/{id}` (`ReturnReferenceReader.cs:71-72`) | **not set anywhere**. Without it: 503 `DEPENDENCY_UNAVAILABLE`. Value: **OD-4** |
| S&OP (MOD-0190) | 73 `AddSandopPersistence()` · 78-81 fixture reader · 122 exclusion term (redundant, §Trap 3) | none named by the VER record. `Sandop:DemandFixtures` is read only when the environment is `Testing` (lines 78-80) | no key needed outside tests. **Index creation is missing — OD-3** |
| Capacity (MOD-0192) | 74 `AddCapacityPersistence()` · 75-76 fixture readers · 77 executor · 102-103 model-error branch · 117 middleware branch · 122 exclusion term (redundant, Trap 3) | none (the fixture scope is hard-coded, `DemandFixtureReader.cs:6-7`) | — |

- `AddSandopPersistence()` was read at 2026-10-02 21:02 +03: sha256 `0e74fec06876…`, two lines of body, registers
  `ISandopRepository` only (`SandopPersistenceRegistration.cs:4-5`). It is byte-equal to the BASE archive member. Its content
  was re-read at the end of this WP; see `SOP-22.md` §5 for the end hash. Q220 may change it; this proposal does not
  assume what Q220 will write.
- The `appsettings.json` keys are **not** part of `Program.cs`. `appsettings.json` is a shared file of the service
  (integration owner). The exact JSON, once OD-4 gives the value `<URL>`:

```json
  "Returns": { "ReferenceBaseUrl": "<URL>" },
  "Claims": { "ReferenceBaseUrl": "<URL>" }
```

- Already-wired neighbour with the same gap, outside the four VER records: `Loads:ReferenceBaseUrl`
  (`LoadReferenceReader.cs:44`) is not set in `appsettings.json` either. Recorded, not proposed.

### Trap 5 — gateway

See `OCELOT-PROPOSAL.md`.

## 4. Static checks behind this proposal (E1; nothing compiled)

- Every type named by BASE lines exists in the tree: the Returns, Claims and Capacity contract-error, request-context and
  middleware classes; both reference readers and their interfaces; `PlatformRegistrationOptions` (+ `SectionName`),
  `IModuleManifestProvider`, `ModuleRegistrationHostedService`, the Carrier and Shipment providers. `grep` hit for each.
- `Diten.SupplyChainService.Api.csproj` in the tree equals the BASE member (`159fae6f…`) and already references
  `Diten.BuildingBlocks.ModuleRegistration.Abstractions` (line 5).
- Lifetimes read: `IMongoDatabase` singleton (`Persistence/DependencyInjection.cs:31`); `CapacityLeaseStore(IMongoDatabase)`
  singleton; the executor (hosted) depends only on the lease store and a logger; repositories are scoped and take
  singletons. No scoped-into-singleton capture was found by reading.
- **Not checked, because nothing may be built or run:** that the proposed file compiles; that the container validates in
  `Development` (the start-up risk in Q211 `REACHABILITY.md` :55-65 — with P0 every handler dependency named there is
  registered, by reading); any run-time behaviour.
