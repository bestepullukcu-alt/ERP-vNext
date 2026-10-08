using System.Text.Json.Serialization;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Api.Quarantine;
using Diten.InventoryService.Api.Traceability;
using Diten.InventoryService.Application;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Infrastructure;
using Diten.InventoryService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

/*
 * HEADER BUDGET RAISED FROM KESTREL'S 32 KB DEFAULT (mirror: ProcurementService / DevEnablement Program.cs). The
 * access token carries one claim per permission, so a tenant-admin JWT can exceed the default header ceiling.
 */
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024;
});

// ── Katman DI ──────────────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddSecretsProvider(builder.Configuration, builder.Environment, options => options.ServiceName = "Inventory");
builder.Services.ValidateRequiredSecrets(builder.Configuration, builder.Environment, "Inventory", [
    new("JwtSettings:Secret", "Inventory", SecretRequirementKind.JwtCurrent),
    new("JwtSettings:PreviousSecrets", "Inventory", SecretRequirementKind.JwtPreviousCollection, Required: false),
    new("Mongo:ConnectionString", "Inventory", SecretRequirementKind.ConnectionString)
]);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddPersistence(builder.Configuration, builder.Environment);

// ── Module self-registration (MOD-0173-FU01, module-self-registration-standard §4): HTTP push of the UI manifest to
// Platform at startup — best-effort, never blocks startup; skipped (warning) when PlatformRegistration is unset.
builder.Services.Configure<PlatformRegistrationOptions>(builder.Configuration.GetSection(PlatformRegistrationOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IModuleManifestProvider, InventoryLedgerManifestProvider>();
builder.Services.AddSingleton<IModuleManifestProvider, LotSerialTrackingManifestProvider>(); // MOD-0174 (pack §14)
builder.Services.AddTraceability(builder.Configuration); // MOD-0174 slice P1
// MOD-0173 posting path shared by postMovement and the MOD-0175 apply engine (MOD-0175 pack §5 Q1 seam).
builder.Services.AddScoped<Diten.InventoryService.Application.Features.Inventory.Services.InternalMovementPostingService>();
builder.Services.AddSingleton<IModuleManifestProvider, QuarantineBlockedStockManifestProvider>(); // MOD-0175 (pack §14)
builder.Services.AddQuarantine(builder.Configuration); // MOD-0175 slice Q1
builder.Services.AddHostedService<ModuleRegistrationHostedService>();

// ── JWT (issuer / audience / keys from configuration; shared clock skew — BL-296) ─────────────────────────
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
var jwtAudience = builder.Configuration["JwtSettings:Audience"];
var jwtRotationResolver = new JwtSecretRotationResolver(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKeys = jwtRotationResolver.GetValidationKeys(),
            ClockSkew = JwtValidationDefaults.ClockSkew
        };
    });

builder.Services.AddAuthorization();

// No CORS policy: MOD-0173 has no browser caller (shell: none); traffic arrives through the gateway (INT-1).

// ── Controllers: unwrapped INVENTORY-BUNDLE v1 wire (pack §8) ─────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Null members are omitted, never written as null (ASSUMPTION-S1-06).
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Any framework-level binding failure still answers with the contract Error schema.
        options.InvalidModelStateResponseFactory = context =>
        {
            var correlation = context.HttpContext.RequestServices.GetRequiredService<ICorrelationContext>();
            return new BadRequestObjectResult(ContractError.Create(
                InventoryErrorCodes.InvalidRequest, "The request is malformed.", correlation.CorrelationId));
        };
    });

if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment(InventoryPolicyOptions.MockEnvironmentName))
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Diten Inventory Service (MOD-0173)", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Bearer token. Örnek: Bearer <token>"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
    });
}

// ── Build ─────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseInventoryCorrelation();

// Unhandled exceptions outside the MediatR pipeline: contract Error, no code, no stack trace on the wire.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var correlation = context.RequestServices.GetRequiredService<ICorrelationContext>();
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(
        ContractError.Create(null, "An unexpected error occurred.", correlation.CorrelationId));
}));

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment(InventoryPolicyOptions.MockEnvironmentName))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseInventoryScopeResolution();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", module = InventoryContract.ModuleId }))
    .AllowAnonymous();
app.MapControllers();

app.Run();

/// <summary>Entry point, exposed for <c>WebApplicationFactory&lt;Program&gt;</c> in the service tests.</summary>
public partial class Program;
