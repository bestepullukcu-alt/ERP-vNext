using Diten.BuildingBlocks.Security.Secrets;
using Diten.ManufacturingService.Api.Controllers;
using Diten.ManufacturingService.Api.ModuleRegistration;
using Diten.ManufacturingService.Application;
using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Infrastructure;
using Diten.ManufacturingService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

// Diten.ManufacturingService — MOD-0193 BOM & Routings (MVP-3, DCP-009). Port 5067 (AGENTS.md §3).
var builder = WebApplication.CreateBuilder(args);

// Mirror of Procurement/DevEnablement: a tenant-admin JWT carries one claim per permission and can exceed Kestrel's
// 32 KB header default; every service behind the gateway would otherwise answer 431.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestHeadersTotalSize = 64 * 1024);

builder.Services.AddApplication();
builder.Services.AddSecretsProvider(builder.Configuration, builder.Environment, options => options.ServiceName = "Manufacturing");
builder.Services.ValidateRequiredSecrets(builder.Configuration, builder.Environment, "Manufacturing", [
    new("JwtSettings:Secret", "Manufacturing", SecretRequirementKind.JwtCurrent),
    new("JwtSettings:PreviousSecrets", "Manufacturing", SecretRequirementKind.JwtPreviousCollection, Required: false),
    new("Mongo:ConnectionString", "Manufacturing", SecretRequirementKind.ConnectionString)
]);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration, builder.Environment);

// Module self-registration: push the MOD-0193 manifest to Platform at startup (best-effort, idempotent).
builder.Services.Configure<PlatformRegistrationOptions>(builder.Configuration.GetSection(PlatformRegistrationOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IModuleManifestProvider, BomRoutingsManifestProvider>();
builder.Services.AddHostedService<ModuleRegistrationHostedService>();

var jwtRotationResolver = new JwtSecretRotationResolver(builder.Configuration);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKeys = jwtRotationResolver.GetValidationKeys(),
            ClockSkew = JwtValidationDefaults.ClockSkew
        };
    });
builder.Services.AddAuthorization();

// A body that does not bind (bad uuid, wrong type) answers the contract Error shape, not ProblemDetails.
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var messages = context.ModelState
        .Where(entry => entry.Value?.Errors.Count > 0)
        .Select(entry => $"{entry.Key}: {entry.Value!.Errors[0].ErrorMessage}".Trim())
        .ToList();
    return new BadRequestObjectResult(ContractErrors.Body(BomErrorCodes.InvalidRequest,
        messages.Count == 0 ? BomErrorCodes.Message(BomErrorCodes.InvalidRequest) : string.Join(" ", messages), context.HttpContext));
});

var app = builder.Build();

app.UseCorrelation();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(ContractErrors.Body(BomErrorCodes.InternalError, BomErrorCodes.Message(BomErrorCodes.InternalError), context));
}));
app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "up", module = "MOD-0193", service = "Diten.ManufacturingService" })).AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program;
