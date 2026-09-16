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
builder.Services.AddHostedService<ShipmentOutboxWorker>();
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
    o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = c =>
{
    var context = c.HttpContext.RequestServices.GetRequiredService<RequestContext>();
    return new BadRequestObjectResult(ContractError.Create("INVALID_REQUEST", "Request schema validation failed.", context.CorrelationId == Guid.Empty ? Guid.NewGuid() : context.CorrelationId));
});
var app = builder.Build();
app.UseAuthentication();
app.UseMiddleware<ShipmentContextMiddleware>();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "up", module = "MOD-0183", warehouseIntake = "unimplemented", outboxTransport = "integration-owned" })).AllowAnonymous();
app.MapControllers();
app.Run();
public partial class Program { }
