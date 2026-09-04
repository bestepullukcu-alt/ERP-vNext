using Diten.ApiGateway.Middleware;
using Diten.ApiGateway.Authentication;
using Diten.ApiGateway.Observability;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.Platform.Common.Observability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Prometheus;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

/*
 * ⚠ HEADER BUDGET RAISED FROM KESTREL'S 32 KB DEFAULT (2026-09-04).
 *
 * MEASURED CAUSE, not a guess: the access token carries one claim per permission
 * (TokenService.cs:50-52), the tenant admin holds 408 of them, and the resulting JWT is
 * ~21.5 KB -- delivered as six chunked cookies (access_tokenC1..C6). A browser calling the
 * gateway DIRECTLY (the `direct-gateway-profile` pages: Golden Reference, Governance, parts
 * of CRM) sends those cookies plus the CORS headers, and the total crossed 32 KB. Kestrel
 * then answered 431 BEFORE the request reached any service, so the failure looked like a
 * dead backend while the service was answering 401 perfectly well one port over.
 *
 * ⚠ THIS IS A SYMPTOM FIX AND IT BUYS TIME, NOTHING MORE. The token grows with every module
 * that registers permissions -- it is now 407 and rising -- so a larger ceiling only moves
 * the day this breaks again. The real repair is to stop shipping permissions as claims and
 * resolve them server-side; recorded in the backlog. Do not treat this number as headroom
 * to spend.
 *
 * Pages on the `proxy-profile` were never affected: their MVC controller reads the HttpOnly
 * cookie server-side and forwards a clean `Authorization: Bearer`, so the browser's cookies
 * never travel to the gateway at all. That asymmetry is why only half the screens broke.
 */
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024;
});


builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

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
        .MinimumLevel.Override("Ocelot", LogEventLevel.Warning)
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

builder.Services.AddSecretsProvider(builder.Configuration, builder.Environment, options => options.ServiceName = "ApiGateway");
builder.Services.ValidateRequiredSecrets(builder.Configuration, builder.Environment, "ApiGateway", [
    new("JwtSettings:Secret", "ApiGateway", SecretRequirementKind.JwtCurrent),
    new("JwtSettings:PreviousSecrets", "ApiGateway", SecretRequirementKind.JwtPreviousCollection, Required: false)
]);

var gatewaySecret = builder.Configuration["JwtSettings:Secret"] ?? string.Empty;
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(gatewaySecret))
{
    throw new InvalidOperationException("JwtSettings:Secret is missing or empty in non-Development environment.");
}

builder.Services.AddDitenObservability(builder.Configuration, builder.Environment);
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationPropagationDelegatingHandler>();

builder.Services.AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions, GatewayJwtAuthenticationHandler>("Bearer", _ => { });

builder.Services.AddOcelot()
    .AddDelegatingHandler<CorrelationPropagationDelegatingHandler>(true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy
            .WithOrigins("http://localhost:5001", "http://localhost:5011")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

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
    app.UseMetricServer(observabilityOptions.Metrics.Path);
}

app.UseHealthChecks(observabilityOptions.Health.LivePath, new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteSanitizedAsync
});

app.UseHealthChecks(observabilityOptions.Health.ReadyPath, new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteSanitizedAsync
});

app.UseHealthChecks(observabilityOptions.Health.Path, new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteSanitizedAsync
});

app.Use(async (context, next) =>
{
    var cookieToken = AuthTokenCookies.GetAccessToken(context.Request);
    if (!context.Request.Headers.ContainsKey("Authorization") &&
        !string.IsNullOrWhiteSpace(cookieToken))
    {
        context.Request.Headers.Authorization = $"Bearer {cookieToken}";
    }

    await next();
});

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

await app.UseOcelot();

app.Run();
