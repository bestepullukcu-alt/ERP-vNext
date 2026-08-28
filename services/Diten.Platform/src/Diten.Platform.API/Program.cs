using Diten.Platform.Application;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Infrastructure;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Common.Observability;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

/*
 * ⚠ THE CONTAINER IS CHECKED WHEN THE APP BOOTS, NOT WHEN A USER HITS THE ENDPOINT.
 *
 * Without ValidateOnBuild, an unregistered dependency is discovered the first time somebody resolves the
 * thing that needs it — which for a MediatR handler means the first request to that one endpoint, in
 * whatever environment it happens to reach first. Measured 2026-08-26: the TaskDocumentReferenceFreezer seam
 * was OPTIONAL precisely so this would not happen, and the price was that forgetting the registration
 * silently discarded every document citation the author had entered. The fix was to make the argument
 * required; this line is the other half, and without it "required" only moves the failure from silence to
 * the first request.
 *
 * ValidateScopes is the companion rule: a singleton that captures a scoped service is a bug that shows up as
 * stale tenant data under load, and nowhere earlier.
 */
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

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

/*
 * BL-024 Phase 2 — "does the caller hold permission P", answered from the request's claims.
 *
 * Registered HERE and not in Infrastructure because the implementation calls PermissionClaimEvaluator, which
 * lives in this project and owns the canonical + legacy-alias matching the [HasPermission] filter uses.
 * Answering the question anywhere else would mean a second, slightly-different matcher, and field authorization
 * would then disagree with the endpoint guarding the same controller.
 */
builder.Services.AddScoped<Diten.Platform.Application.Contracts.IActorPermissionContext,
    Diten.Platform.API.Security.ClaimsActorPermissionContext>();
builder.Services.AddDitenObservability(
    builder.Configuration,
    builder.Environment,
    healthChecks =>
    {
        healthChecks.AddCheck<MongoDbReadinessHealthCheck>("mongodb", tags: new[] { "ready" });
        healthChecks.AddCheck<Diten.Platform.API.Observability.BusinessReferenceDataProviderReadinessHealthCheck>(
            "business_reference_data_provider",
            tags: new[] { "ready" });
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
builder.Services.Configure<ModuleRegistrationCredentialOptions>(
    builder.Configuration.GetSection(ModuleRegistrationCredentialOptions.SectionName));
builder.Services.Configure<VerifiedGskuResolverCredentialOptions>(
    builder.Configuration.GetSection(VerifiedGskuResolverCredentialOptions.SectionName));
builder.Services.Configure<VerifiedGskuOperationalProvisioningOptions>(
    builder.Configuration.GetSection(VerifiedGskuOperationalProvisioningOptions.SectionName));
builder.Services.Configure<VerifiedMarketOperationalProvisioningOptions>(
    builder.Configuration.GetSection(VerifiedMarketOperationalProvisioningOptions.SectionName));
builder.Services.AddScoped<
    Diten.Platform.Application.Features.BusinessReferenceData.Services.IBusinessReferenceDataVerifiedGskuOperationalEligibility,
    DevelopmentBusinessReferenceDataVerifiedGskuOperationalEligibility>();
builder.Services.AddScoped<Diten.Platform.Application.Features.BusinessReferenceData.Services.IBusinessReferenceDataVerifiedMarketOperationalEligibility,
    DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility>();
builder.Services.AddScoped<VerifiedMarketOperationalProvisioningRunner>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IModuleRegistrationCredentialAuthenticator, ModuleRegistrationCredentialAuthenticator>();
builder.Services.AddSingleton<IVerifiedGskuResolverCredentialAuthenticator, VerifiedGskuResolverCredentialAuthenticator>();
builder.Services.AddScoped<IVerifiedGskuResolverJwtTenantContext, VerifiedGskuResolverJwtTenantContext>();

// AG-STEP-011 / MOD-0018-FU14 Group B — self-explain observer (API-layer; reuses the API-layer PermissionClaimEvaluator).
builder.Services.AddScoped<Diten.Platform.API.Observability.ICorrelationContext, Diten.Platform.API.Observability.CorrelationContext>();
builder.Services.AddScoped<Diten.Platform.API.Authorization.Explain.ISelfAccessExplainService, Diten.Platform.API.Authorization.Explain.SelfAccessExplainService>();
builder.Services.AddHostedService<BusinessReferenceDataCatalogLoadWorker>();
builder.Services.AddHostedService<VerifiedGskuOperationalProvisioningRunner>();
// Startup ordering gate: the A1 permission worker must not sync until module self-registration has FINISHED,
// otherwise A1 (which syncs moduleCode/scope = null) can create a key first and permanently stamp it
// Module="platform" + Scope=PlatformAdmin — a scope AuthService cannot downgrade. Registration order alone is
// NOT sufficient (the manifest walk is slower than the flat key sweep), so a real completion signal is used.
//
// ⚠ MERGE 2026-08-26: main registered the A1 worker HERE, immediately after the catalog worker. That line is
// not dropped — it MOVED, to just below the self-registration worker, which is the whole point of the gate
// above. Leaving both would register A1 twice and race the very ordering this exists to guarantee.
builder.Services.AddSingleton<Diten.Platform.API.Services.ModuleRegistration.ModuleSelfRegistrationGate>();
// MC-3b — self-register Platform-internal module manifests (workflow, …) into the catalog in-process at startup.
builder.Services.AddHostedService<Diten.Platform.API.Services.ModuleRegistration.PlatformModuleSelfRegistrationWorker>();
// A1 — auto-register every controller [HasPermission] key into AuthService at startup (best-effort, idempotent).
// Gated on the signal above; falls back after a bounded timeout so a manifest failure cannot block it forever.
builder.Services.AddHostedService<Diten.Platform.API.Services.Security.PlatformPermissionAutoRegistrationWorker>();
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

if (VerifiedMarketOperationalCommandLine.IsRequested(args))
{
    VerifiedMarketOperationalCommandLine.EnsureDevelopment(builder.Environment);
    await using var operationalScope = app.Services.CreateAsyncScope();
    await operationalScope.ServiceProvider
        .GetRequiredService<VerifiedMarketOperationalProvisioningRunner>()
        .RunAsync();
    return;
}

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
