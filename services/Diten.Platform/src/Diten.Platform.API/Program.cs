using Diten.Platform.Application;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Infrastructure;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Common.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddDitenObservability(
    builder.Configuration,
    builder.Environment,
    healthChecks =>
    {
        healthChecks.AddCheck<MongoDbReadinessHealthCheck>("mongodb", tags: new[] { "ready" });
        if (string.Equals(
                builder.Configuration["Eventing:Transport"],
                "RabbitMQ",
                StringComparison.OrdinalIgnoreCase))
        {
            healthChecks.AddCheck<Diten.Platform.API.Observability.RabbitMqReadinessHealthCheck>("rabbitmq", tags: new[] { "ready" });
        }

        var backgroundJobs = builder.Configuration.GetSection("BackgroundJobs");
        if (backgroundJobs.GetValue<bool>("Enabled") || backgroundJobs.GetValue<bool>("DashboardEnabled"))
        {
            healthChecks.AddCheck<Diten.Platform.API.Observability.HangfireStorageReadinessHealthCheck>("hangfire_storage", tags: new[] { "ready" });
        }
    });
Diten.Platform.API.Observability.EventingObservabilityServiceCollectionExtensions.AddEventingObservabilityMetrics(builder.Services);
Diten.Platform.API.Observability.BackgroundJobObservabilityServiceCollectionExtensions.AddBackgroundJobObservabilityMetrics(builder.Services);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        cors => cors
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddControllers();
builder.Services.AddHostedService<BusinessReferenceDataCatalogLoadWorker>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<Diten.Platform.API.Middleware.GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Diten Platform",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Bearer token. Example: Bearer <token>"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
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
                    Id = "X-Tenant-Id"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler();
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

app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();
app.UsePlatformHangfireDashboard(app.Configuration);

app.MapHealthChecks(observabilityOptions.Health.LivePath, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteSanitizedAsync
}).AllowAnonymous();

app.MapHealthChecks(observabilityOptions.Health.ReadyPath, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteSanitizedAsync
}).AllowAnonymous();

app.MapHealthChecks(observabilityOptions.Health.Path, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
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
