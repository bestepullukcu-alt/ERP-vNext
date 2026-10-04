using Diten.SupplyChainService.Api.Features.Loads;
using Diten.SupplyChainService.Application.Features.Loads;
using Diten.SupplyChainService.Persistence.Features.Loads;
using Diten.SupplyChainService.Infrastructure.Features.Loads;
using Diten.SupplyChainService.Api.Features.Carriers;
using Diten.SupplyChainService.Api.Features.Returns;
using Diten.SupplyChainService.Api.Features.Claims;
using Diten.SupplyChainService.Application.Features.Carriers;
using Diten.SupplyChainService.Persistence.Features.Carriers;
// Q271/Q272 (2026-10-03): composing Returns (MOD-0186) and Claims (MOD-0187).
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Persistence.Features.Returns;
using Diten.SupplyChainService.Infrastructure.Features.Returns;
using Diten.SupplyChainService.Application.Features.Claims;
using Diten.SupplyChainService.Persistence.Features.Claims;
using Diten.SupplyChainService.Infrastructure.Features.Claims;
using System.Text;
using Diten.BuildingBlocks.Security.Secrets;
using System.Text.Json.Serialization;
using Diten.SupplyChainService.Api.Middleware;
using Diten.SupplyChainService.Api.Logging;
using Diten.SupplyChainService.Api.ModuleRegistration;
using Diten.SupplyChainService.Application;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Persistence;
using Diten.SupplyChainService.Infrastructure.Eventing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.IdentityModel.Tokens;
using MediatR;
using System.Reflection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestHeadersTotalSize = 64 * 1024);
// Render only the three trace properties; never {Properties}, which would also print request paths and scope values.
builder.Services.AddHttpContextAccessor();
builder.Host.UseSerilog((context, services, logger) => logger.MinimumLevel.Information().Enrich.FromLogContext()
    .Enrich.With(new CorrelationIdEnricher(services.GetRequiredService<IHttpContextAccessor>()))
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} CorrelationId={CorrelationId} TenantId={TenantId} LegalEntityId={LegalEntityId}{NewLine}{Exception}"));
string Required(string key) => !string.IsNullOrWhiteSpace(builder.Configuration[key]) ? builder.Configuration[key]! : throw new InvalidOperationException(key + " is required.");
var secret = Required("JwtSettings:Secret");
if (Encoding.UTF8.GetByteCount(secret) < 32) throw new InvalidOperationException("JWT signing secret must be at least 32 bytes.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = Required("JwtSettings:Issuer"),
        ValidAudience = Required("JwtSettings:Audience"),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ClockSkew = JwtValidationDefaults.ClockSkew,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
});
builder.Services.AddAuthorization();
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddCarrierPersistence();
builder.Services.AddLoadPersistence();
builder.Services.AddHttpClient<ILoadReferenceReader, LoadReferenceReader>(client => client.Timeout = TimeSpan.FromSeconds(5));
// Q271/Q272 (2026-10-03): Returns (MOD-0186) and Claims (MOD-0187) were written, tested and
// unreachable — Program.cs composed only Carriers and Loads, so DependencyInjection.cs had to exclude
// their handlers from MediatR or container validation would fail at builder.Build() and take
// Shipments, Carriers and Loads down with it (Q217, Q232, Q236). Their persistence registrations
// already registered everything except the reference reader, which lives in Infrastructure and so
// cannot be registered from Persistence (Q220). These four lines follow the Loads pattern above.
builder.Services.AddReturnPersistence();
builder.Services.AddHttpClient<IReturnReferenceReader, ReturnReferenceReader>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddClaimPersistence();
builder.Services.AddHttpClient<IClaimReferenceReader, ClaimReferenceReader>(client => client.Timeout = TimeSpan.FromSeconds(5));
// Capacity (MOD-0192) is NOT composed, for the same reason as S&OP below. CT tried it on 2026-10-03
// and K3 caught it: the service failed at builder.Build() with 3 unresolved registrations, because
// CapacityRepository itself depends on IDemandFixtureReader. CT's first reading of the handler
// constructors missed it — the dependency is in the repository, not the handlers.
// S&OP (MOD-0190) is deliberately NOT composed here. Its handlers need IDemandFixtureReader, and the
// only implementation is DemandFixtureReader, whose own comment reads "deliberately a test-only exact
// fixture seam; no DEMAND HTTP endpoint is inferred". It takes IEnumerable<DemandFixture>, so in a
// composed host it would receive an empty set and MatchesAsync would always return false — every S&OP
// create would be rejected as a demand-plan mismatch while the module looked alive. A module that
// silently refuses everything is worse than one that is plainly unreachable (K4), so S&OP stays in the
// MediatR exclusion list until a real demand reader exists. See ledger Q273.
builder.Services.AddHostedService<ShipmentOutboxWorker>();
// Q339-R2 (2026-10-03): ShipmentTrackingPodManifestProvider and ModuleRegistrationHostedService were
// written and tested, but nothing composed them, so Shipment never reached the Platform module catalog.
// These four lines follow MdmService Program.cs:88-92. They make the hosted service push the one Shipment
// manifest to Platform after startup, best-effort: blank config logs a warning and returns, and a dead
// Platform is retried 5 times and then logged. The API serves either way (measured, Q339-R2 P1-P3).
// Carrier is absent on purpose: MOD-0184:473 forbids its AddSingleton until Carrier has a UI (none exists).
builder.Services.Configure<PlatformRegistrationOptions>(builder.Configuration.GetSection(PlatformRegistrationOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IModuleManifestProvider, ShipmentTrackingPodManifestProvider>();
// R-2 (2026-10-04): Returns (MOD-0186 §33) ships with its UI in the same change, per the §33 ship rule.
builder.Services.AddSingleton<IModuleManifestProvider, ReverseLogisticsManifestProvider>();
builder.Services.AddHostedService<ModuleRegistrationHostedService>();
// Q381 (2026-10-04, owner decision §4): routes of uncomposed modules answer 404, not 500. The filter below reads
// which modules exist from the registrations above; it keeps no list of its own (K6).
builder.Services.AddControllers()
    .ConfigureApplicationPartManager(m => m.FeatureProviders.Add(new ComposedFeatureControllerFilter(builder.Services)))
    .AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
    o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = c =>
{
    if (LoadContextMiddleware.IsLoadPath(c.HttpContext))
        return new BadRequestObjectResult(LoadContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<LoadRequestContext>().CorrelationId));
    if (CarrierContextMiddleware.IsCarrierPath(c.HttpContext))
        return new BadRequestObjectResult(CarrierContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<CarrierRequestContext>().CorrelationId));
    var context = c.HttpContext.RequestServices.GetRequiredService<RequestContext>();
    return new BadRequestObjectResult(ContractError.Create("INVALID_REQUEST", "Request schema validation failed.", context.CorrelationId == Guid.Empty ? Guid.NewGuid() : context.CorrelationId));
});
var app = builder.Build();
app.UseAuthentication();
app.UseWhen(CarrierContextMiddleware.IsCarrierPath, branch => branch.UseMiddleware<CarrierContextMiddleware>());
app.UseWhen(LoadContextMiddleware.IsLoadPath, branch => branch.UseMiddleware<LoadContextMiddleware>());
// Q271/Q272 (2026-10-03): registering a module's persistence makes it RESOLVABLE, not REACHABLE. Until
// these two branches existed, every /returns and /claims request fell through to ShipmentContextMiddleware
// below, which answered them with Shipment-contract errors and left ReturnRequestContext and
// ClaimRequestContext at their defaults — empty scope, empty correlation. An authenticated request would
// have reached the handlers with no tenant scope at all. Measured by Q272 in four boot variants: with the
// composition lines alone, GET /claims with a valid read token returned 400 "Scope headers are required
// UUIDs." from the Shipment middleware; with this branch wired it returns 200 and the handler runs.
app.UseWhen(ReturnContextMiddleware.IsReturnPath, branch => branch.UseMiddleware<ReturnContextMiddleware>());
app.UseWhen(ClaimContextMiddleware.IsClaimPath, branch => branch.UseMiddleware<ClaimContextMiddleware>());
app.UseWhen(context => !CarrierContextMiddleware.IsCarrierPath(context) && !LoadContextMiddleware.IsLoadPath(context)
    && !ReturnContextMiddleware.IsReturnPath(context) && !ClaimContextMiddleware.IsClaimPath(context),
    branch => branch.UseMiddleware<ShipmentContextMiddleware>());
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "up", module = "MOD-0183", warehouseIntake = "unimplemented", outboxTransport = "integration-owned" })).AllowAnonymous();
app.MapControllers();
app.Run();
public partial class Program { }

// Q381: drops the controllers of feature modules this composition does not serve, so MapControllers() never maps
// them. A feature controller (namespace *.Features.<Module>) is kept only when MediatR registered a handler for at
// least one request of that module and every Diten.* constructor parameter has a registration. Measured before
// this filter: authenticated Capacity requests failed with 500 in the controller activator (CapacityRequestContext
// unregistered) and S&OP requests with 500 in MediatR (handlers excluded by Application/DependencyInjection.cs).
// Controllers outside Features (Shipments) are always kept.
file sealed class ComposedFeatureControllerFilter(IServiceCollection services) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var controller in feature.Controllers.Where(c => !IsComposed(c)).ToArray())
            feature.Controllers.Remove(controller);
    }

    private bool IsComposed(TypeInfo controller)
    {
        var module = ModuleOf(controller.Namespace);
        if (module is null) return true;
        var requests = typeof(RequestContext).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBaseRequest).IsAssignableFrom(t) && ModuleOf(t.Namespace) == module)
            .ToHashSet();
        var handled = requests.Count == 0 || services.Any(d => d.ServiceType.IsGenericType
            && d.ServiceType.GetGenericTypeDefinition() is var definition
            && (definition == typeof(IRequestHandler<,>) || definition == typeof(IRequestHandler<>))
            && requests.Contains(d.ServiceType.GenericTypeArguments[0]));
        var constructible = controller.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First().GetParameters()
            .Where(p => p.ParameterType.Namespace?.StartsWith("Diten.", StringComparison.Ordinal) == true)
            .All(p => services.Any(d => d.ServiceType == p.ParameterType));
        return handled && constructible;
    }

    private static string? ModuleOf(string? ns)
    {
        const string marker = ".Features.";
        var start = ns?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
        if (start < 0) return null;
        var rest = ns![(start + marker.Length)..];
        var end = rest.IndexOf('.');
        return end < 0 ? rest : rest[..end];
    }
}
