using Diten.AuthService.Api;
using Diten.AuthService.Application;
using Diten.AuthService.Infrastructure;
using Diten.AuthService.Persistence;
using Diten.Platform.Common.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

var observabilityOptions = builder.Configuration
    .GetSection(ObservabilityOptions.SectionName)
    .Get<ObservabilityOptions>() ?? new ObservabilityOptions();
observabilityOptions.Environment = string.IsNullOrWhiteSpace(observabilityOptions.Environment)
    ? builder.Environment.EnvironmentName
    : observabilityOptions.Environment;

if (observabilityOptions.Seq.Enabled
    && string.IsNullOrWhiteSpace(observabilityOptions.Seq.Url)
    && !observabilityOptions.Seq.SafeDisableWhenUrlMissing)
{
    throw new InvalidOperationException("Observability:Seq:Url is required when Observability:Seq:Enabled=true.");
}

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.With<SensitiveDataLogEventEnricher>()
        .Enrich.WithProperty("ServiceName", observabilityOptions.ServiceName)
        .Enrich.WithProperty("Environment", observabilityOptions.Environment)
        .WriteTo.Console(new JsonFormatter(renderMessage: true));

    if (observabilityOptions.Seq.Enabled && !string.IsNullOrWhiteSpace(observabilityOptions.Seq.Url))
    {
        loggerConfiguration.WriteTo.Seq(
            observabilityOptions.Seq.Url,
            apiKey: string.IsNullOrWhiteSpace(observabilityOptions.Seq.ApiKey)
                ? null
                : observabilityOptions.Seq.ApiKey);
    }
});

// Port ayarı (launcSettings de olacak ama Program.cs'te de belirtilebilir)
// builder.WebHost.UseUrls("http://localhost:5056");

// ── MediatR / Application / Infrastructure / Persistence ───────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddPersistence(builder.Configuration, builder.Environment);
builder.Services.AddDitenObservability(
    builder.Configuration,
    builder.Environment,
    healthChecks => healthChecks.AddCheck<MongoDbReadinessHealthCheck>("mongodb", tags: new[] { "ready" }));

// ── JWT / Auth ─────────────────────────────────────────────────────────────
// (Konfigürasyon Infrastructure/DependencyInjection.cs içinde yapılmıştır)

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder => builder
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});


// ── Controllers + ProblemDetails ──────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ── Swagger ───────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Diten Auth Service",
        Version = "v1"
    });

    // Swagger'da JWT + X-Tenant-Id desteği
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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    c.AddSecurityDefinition("X-Tenant-Id", new OpenApiSecurityScheme
    {
        Name = "X-Tenant-Id",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Multi-tenant GUID."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "X-Tenant-Id"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Build ─────────────────────────────────────────────────────────────────
var app = builder.Build();

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler();   // global ProblemDetails
app.UseStatusCodePages();

app.UseCors("AllowAllOrigins");
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) => ex is not null
        ? LogEventLevel.Error
        : httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Error
            : LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("ServiceName", observabilityOptions.ServiceName);
        diagnosticContext.Set("Environment", observabilityOptions.Environment);
        diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? string.Empty);
        diagnosticContext.Set("StatusCode", httpContext.Response.StatusCode);
        diagnosticContext.Set("TraceId", System.Diagnostics.Activity.Current?.TraceId.ToString());
    };
});

if (observabilityOptions.Metrics.Enabled)
{
    app.UseHttpMetrics();
}

// ── Auth & Isolation ───────────────────────────────────────────────────────
app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

app.MapHealthChecks(observabilityOptions.Health.LivePath, new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteSanitizedAsync
}).AllowAnonymous();

app.MapHealthChecks(observabilityOptions.Health.ReadyPath, new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteSanitizedAsync
}).AllowAnonymous();

app.MapHealthChecks(observabilityOptions.Health.Path, new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteSanitizedAsync
}).AllowAnonymous();

if (observabilityOptions.Metrics.Enabled)
{
    app.MapMetrics(observabilityOptions.Metrics.Path).AllowAnonymous();
}

app.MapControllers();

app.Run();

public partial class Program
{
}
