using System.Text;
using Diten.CrmService.Application;
using Diten.CrmService.Infrastructure;
using Diten.CrmService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Diten.BuildingBlocks.Security.Secrets;

// NOTE (MOD-0149-PREREQ scaffold): This is the Diten.CrmService runtime skeleton only.
// It intentionally hosts NO Account/CRM business endpoints. Only /health + infrastructure wiring.
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Production,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Dev-fleet bind fix (WP-CRM-PORTFIX): CreateEmptyBuilder does NOT flow ASPNETCORE_URLS into Kestrel the way
// CreateBuilder's default host config does, so dev falls back to :5000 (the Gateway port) and every gateway
// route 404s. Re-apply the env-provided URLs to Kestrel ourselves — but ONLY in non-Production and ONLY when the
// value is present, so the IIS binding path below (where the ANCM module owns the port) is never disturbed.
var devUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (!builder.Environment.IsProduction() && !string.IsNullOrWhiteSpace(devUrls))
    builder.WebHost.UseUrls(devUrls.Split(';', StringSplitOptions.RemoveEmptyEntries));

// CreateEmptyBuilder registers NO server, so wire the same set WebApplication.CreateBuilder does — otherwise
// hosting under IIS fails (in-process: "No service for type 'IServer'"; or a self-bind clash with the module).
// Kestrel serves standalone/dev; UseIIS is the in-process IIS server (IISHttpServer); UseIISIntegration is the
// out-of-process reverse-proxy path. Do NOT hard-code UseUrls("http://localhost:5061"): under IIS the module
// owns the port (in-process it IS the server; out-of-process it assigns one and proxies), and self-binding it
// collides with the module — the app failed to start (500.30 in-process, 502 with SocketException 10013
// out-of-process). Binding is left to the host: ASPNETCORE_URLS in dev (launchSettings), the ANCM binding under IIS.
builder.WebHost.UseKestrel();
builder.WebHost.UseIIS();
builder.WebHost.UseIISIntegration();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);

var jwtSecret = builder.Configuration["JwtSettings:Secret"];
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
var jwtAudience = builder.Configuration["JwtSettings:Audience"];
var jwtPreviousSecrets = builder.Configuration
    .GetSection("JwtSettings:PreviousSecrets")
    .GetChildren()
    .Select(section => section.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .ToArray();

ValidateRequiredJwtSetting(jwtSecret, "JwtSettings:Secret");
ValidateRequiredJwtSetting(jwtIssuer, "JwtSettings:Issuer");
ValidateRequiredJwtSetting(jwtAudience, "JwtSettings:Audience");
var jwtSigningKeys = BuildJwtSigningKeys(jwtSecret, jwtPreviousSecrets);

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
            IssuerSigningKeys = jwtSigningKeys,
            ClockSkew = JwtValidationDefaults.ClockSkew
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
        Title = "Diten CRM Service",
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

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Diten.CrmService" })).AllowAnonymous();
app.MapControllers();

app.Run();

static void ValidateRequiredJwtSetting(string? value, string key)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Configuration error: '{key}' is missing or empty.");
    }
}

static IReadOnlyList<SecurityKey> BuildJwtSigningKeys(string? currentSecret, IEnumerable<string?> previousSecrets)
{
    var secrets = new[] { currentSecret }
        .Concat(previousSecrets)
        .Where(secret => !string.IsNullOrWhiteSpace(secret))
        .Distinct(StringComparer.Ordinal)
        .Select(secret => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret!)))
        .Cast<SecurityKey>()
        .ToArray();

    if (secrets.Length == 0)
    {
        throw new InvalidOperationException("Configuration error: 'JwtSettings:Secret' is missing or empty.");
    }

    return secrets;
}

public partial class Program
{
}
