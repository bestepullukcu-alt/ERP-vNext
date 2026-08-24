using System.Text;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Application;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);

var jwtSecret = builder.Configuration["JwtSettings:Secret"];
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
var jwtAudience = builder.Configuration["JwtSettings:Audience"];

ValidateRequiredJwtSetting(jwtSecret, "JwtSettings:Secret");
ValidateRequiredJwtSetting(jwtIssuer, "JwtSettings:Issuer");
ValidateRequiredJwtSetting(jwtAudience, "JwtSettings:Audience");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret ?? string.Empty)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Diten MDM Service",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Bearer token."
    });

    c.AddSecurityDefinition("X-Tenant-Id", new OpenApiSecurityScheme
    {
        Name = "X-Tenant-Id",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Tenant GUID."
    });
});

// MC-3b-expand (Part B) — self-register the legal-entity module with the Platform catalog at startup (HTTP push,
// cross-service). Best-effort with retry; never blocks MDM startup if Platform is down.
builder.Services.Configure<PlatformRegistrationOptions>(builder.Configuration.GetSection(PlatformRegistrationOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IModuleManifestProvider, LegalEntityManifestProvider>();
builder.Services.AddSingleton<IModuleManifestProvider, ProductItemSkuMasterManifestProvider>();
builder.Services.AddHostedService<ModuleRegistrationHostedService>();

// MOD-0021 Faz 2 — forward MDM audit events to Platform's central store (S2S), reusing the same Platform base URL +
// internal key as module self-registration. Actor/tenant are read from the current request's JWT.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Diten.MdmService.Application.Contracts.Audit.IPlatformAuditForwarder, Diten.MdmService.Api.Audit.PlatformAuditForwarder>();

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
app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapControllers();

app.Run();

static void ValidateRequiredJwtSetting(string? value, string key)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Configuration error: '{key}' is missing or empty.");
    }
}
