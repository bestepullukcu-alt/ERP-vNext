using System.Text;
using Asp.Versioning;
using Diten.Application;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.Persistence;
using Diten.Infrastructure;
using Diten.WebAPI.Health;
using Diten.WebAPI.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Authentication (D-5 security fix) ────────────────────────────────────────
// This service previously called UseAuthorization() with NO authentication scheme registered and no
// UseAuthentication() in the pipeline, so every endpoint without an explicit filter — including
// UploadsController — was reachable anonymously through the gateway. Settings, validation parameters and
// clock skew are copied verbatim from the sibling Diten.CrmService so a token minted by the AuthService
// validates identically here; nothing about the JWT contract is invented locally.
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

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<DeliveryExecutionManagementDependencyHealthCheck>("delivery_execution_management_ppm_dependency");

// Add Layers
builder.Services.AddApplication();
builder.Services.AddPersistence();
// builder.Services.AddInfrastructure(); // Currently empty, but ready

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5001",
                "https://localhost:5001",
                "http://127.0.0.1:5001",
                "https://127.0.0.1:5001",
                "http://localhost:5002",
                "https://localhost:5002",
                "http://127.0.0.1:5002",
                "https://127.0.0.1:5002",
                "http://localhost:5003",
                "https://localhost:5003",
                "http://127.0.0.1:5003",
                "https://127.0.0.1:5003")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data", "uploads"));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var descriptions = app.DescribeApiVersions();
        foreach (var description in descriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        }
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("FrontendPolicy");
app.UseMiddleware<CorrelationIdMiddleware>();
// D-5: UseAuthentication MUST run before UseAuthorization. Without it the pipeline never populates
// HttpContext.User, so [Authorize] could not be satisfied and every unfiltered endpoint stayed anonymous.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/healthz");

var seedTask = DbInitializer.SeedData(app.Services);
var completed = await Task.WhenAny(seedTask, Task.Delay(TimeSpan.FromSeconds(5)));
if (completed == seedTask)
{
    try
    {
        await seedTask;
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database seed skipped during startup.");
    }
}
else
{
    app.Logger.LogWarning("Database seed skipped due to startup timeout.");
}

app.Run();

// D-5: copied verbatim from Diten.CrmService/Program.cs so the two services fail the same way on a
// misconfigured secret — fail-closed at startup rather than silently accepting unsigned traffic.
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
