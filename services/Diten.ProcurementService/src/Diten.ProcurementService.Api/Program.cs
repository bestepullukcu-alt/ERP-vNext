using Diten.ProcurementService.Api.ModuleRegistration;
using Diten.ProcurementService.Application;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.ProcurementService.Infrastructure;
using Diten.ProcurementService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

/*
 * HEADER BUDGET RAISED FROM KESTREL'S 32 KB DEFAULT (mirror: DevEnablement Program.cs). The access token carries one
 * claim per permission, so a tenant-admin JWT can exceed the default header ceiling and every service behind the
 * gateway answers 431. Raising the ceiling here matches the platform-wide symptom fix.
 */
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024;
});

// ── Katman DI ──────────────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddSecretsProvider(builder.Configuration, builder.Environment, options => options.ServiceName = "Procurement");
builder.Services.ValidateRequiredSecrets(builder.Configuration, builder.Environment, "Procurement", [
    new("JwtSettings:Secret", "Procurement", SecretRequirementKind.JwtCurrent),
    new("JwtSettings:PreviousSecrets", "Procurement", SecretRequirementKind.JwtPreviousCollection, Required: false),
    new("Mongo:ConnectionString", "Procurement", SecretRequirementKind.ConnectionString)
]);
builder.Services.AddInfrastructure();
builder.Services.AddPersistence(builder.Configuration, builder.Environment);

// ── Module self-registration: push MOD-0140 manifest to Platform at startup (best-effort, idempotent). ──
builder.Services.Configure<PlatformRegistrationOptions>(builder.Configuration.GetSection(PlatformRegistrationOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IModuleManifestProvider, SupplierOnboardingManifestProvider>();
builder.Services.AddHostedService<ModuleRegistrationHostedService>();

// ── JWT ───────────────────────────────────────────────────────────────────
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy
            .SetIsOriginAllowed(origin => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// ── Controllers + ProblemDetails ──────────────────────────────────────────
// Enums serialize as their string names (Active/Draft/Posted/…) to match the OWNED OpenAPI
// contracts (which define string enums) and the frontend status-badge mappings. The converter
// also ACCEPTS numeric input on deserialize, so no consumer breaks. (Runtime E2E found supplier
// status rendering as "Bilinmiyor" because numeric 0 was emitted instead of "Active".)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

// ── Swagger ───────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Diten Procurement Service",
        Version = "v1"
    });

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
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
    c.AddSecurityDefinition("X-Tenant-Id", new OpenApiSecurityScheme
    {
        Name = "X-Tenant-Id",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Multi-tenant GUID. Örnek: 3fa85f64-5717-4562-b3fc-2c963f66afa6"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "X-Tenant-Id" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Build ─────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();

app.UseCors("AllowAllOrigins");

app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

app.MapControllers();

app.Run();
