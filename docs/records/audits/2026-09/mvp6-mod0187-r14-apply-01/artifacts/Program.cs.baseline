using Diten.SupplyChainService.Api.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Persistence.Features.Returns;
using Diten.SupplyChainService.Infrastructure.Features.Returns;
using Diten.SupplyChainService.Api.Features.Claims;
using Diten.SupplyChainService.Application.Features.Claims;
using Diten.SupplyChainService.Persistence.Features.Claims;
using Diten.SupplyChainService.Infrastructure.Features.Claims;
using Diten.SupplyChainService.Api.Features.Loads;
using Diten.SupplyChainService.Application.Features.Loads;
using Diten.SupplyChainService.Persistence.Features.Loads;
using Diten.SupplyChainService.Infrastructure.Features.Loads;
using Diten.SupplyChainService.Api.Features.Carriers;
using Diten.SupplyChainService.Application.Features.Carriers;
using Diten.SupplyChainService.Persistence.Features.Carriers;
using System.Text;
using Diten.BuildingBlocks.Security.Secrets;
using System.Text.Json.Serialization;
using Diten.SupplyChainService.Api.Middleware;
using Diten.SupplyChainService.Application;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Persistence;
using Diten.SupplyChainService.Infrastructure.Eventing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestHeadersTotalSize = 64 * 1024);
builder.Host.UseSerilog((context, logger) => logger.MinimumLevel.Information().Enrich.FromLogContext().WriteTo.Console());
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
builder.Services.AddClaimPersistence();
builder.Services.AddReturnPersistence();
builder.Services.AddHttpClient<IReturnReferenceReader, ReturnReferenceReader>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHttpClient<IClaimReferenceReader, ClaimReferenceReader>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHttpClient<ILoadReferenceReader, LoadReferenceReader>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHostedService<ShipmentOutboxWorker>();
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
    o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = c =>
{
    if (ReturnContextMiddleware.IsReturnPath(c.HttpContext))
        return new BadRequestObjectResult(ReturnContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<ReturnRequestContext>().CorrelationId));
    if (ClaimContextMiddleware.IsClaimPath(c.HttpContext))
        return new BadRequestObjectResult(ClaimContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<ClaimRequestContext>().CorrelationId));
    if (LoadContextMiddleware.IsLoadPath(c.HttpContext))
        return new BadRequestObjectResult(LoadContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<LoadRequestContext>().CorrelationId));
    if (CarrierContextMiddleware.IsCarrierPath(c.HttpContext))
        return new BadRequestObjectResult(CarrierContractError.Create("INVALID_REQUEST", 400, c.HttpContext.RequestServices.GetRequiredService<CarrierRequestContext>().CorrelationId));
    var context = c.HttpContext.RequestServices.GetRequiredService<RequestContext>();
    return new BadRequestObjectResult(ContractError.Create("INVALID_REQUEST", "Request schema validation failed.", context.CorrelationId == Guid.Empty ? Guid.NewGuid() : context.CorrelationId));
});
var app = builder.Build();
app.UseAuthentication();
app.UseWhen(ReturnContextMiddleware.IsReturnPath, branch => branch.UseMiddleware<ReturnContextMiddleware>());
app.UseWhen(ClaimContextMiddleware.IsClaimPath, branch => branch.UseMiddleware<ClaimContextMiddleware>());
app.UseWhen(CarrierContextMiddleware.IsCarrierPath, branch => branch.UseMiddleware<CarrierContextMiddleware>());
app.UseWhen(LoadContextMiddleware.IsLoadPath, branch => branch.UseMiddleware<LoadContextMiddleware>());
app.UseWhen(context => !CarrierContextMiddleware.IsCarrierPath(context) && !LoadContextMiddleware.IsLoadPath(context) && !ClaimContextMiddleware.IsClaimPath(context) && !ReturnContextMiddleware.IsReturnPath(context), branch => branch.UseMiddleware<ShipmentContextMiddleware>());
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "up", module = "MOD-0183", warehouseIntake = "unimplemented", outboxTransport = "integration-owned" })).AllowAnonymous();
app.MapControllers();
app.Run();
public partial class Program { }
