using System.Text;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Api.Services.Audit;
using Diten.MdmService.Api.Services.ProductLegalEntityScopes;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var runProductLegalEntityScopeOperational =
    ProductLegalEntityScopeOperationalCommandLine.IsRequested(args);
var runAuditIntentTemporalMigration =
    AuditIntentTemporalMigrationCommandLine.IsRequested(args);
var runProductIdentityWorkflowRecovery =
    ProductIdentityWorkflowRecoveryCommandLine.IsRequested(args);
var runGlobalProductCorrectionRecovery =
    GlobalProductCorrectionRecoveryCommandLine.IsRequested(args);
var runGlobalProductRetirementRecovery =
    GlobalProductRetirementRequestRecoveryCommandLine.IsRequested(args);
if ((runProductLegalEntityScopeOperational ? 1 : 0)
    + (runAuditIntentTemporalMigration ? 1 : 0)
    + (runProductIdentityWorkflowRecovery ? 1 : 0)
    + (runGlobalProductCorrectionRecovery ? 1 : 0)
    + (runGlobalProductRetirementRecovery ? 1 : 0) > 1)
{
    throw new InvalidOperationException("MDM_OPERATIONAL_COMMAND_AMBIGUOUS");
}
var auditIntentTemporalMigrationRequest = runAuditIntentTemporalMigration
    ? AuditIntentTemporalMigrationCommandLine.ValidateAndCreateRequest(builder.Environment, builder.Configuration)
    : null;
if (runProductLegalEntityScopeOperational)
{
    ProductLegalEntityScopeOperationalRunner.EnsureDevelopment(builder.Environment);
    ProductLegalEntityScopeOperationalConfiguration.EnsureValid(builder.Configuration);
}

/*
 * ⚠ HEADER BUDGET RAISED FROM KESTREL'S 32 KB DEFAULT (2026-09-04). The access token carries
 * one claim per permission (AuthService TokenService.cs:50-52) and the tenant admin holds 408,
 * so the JWT is ~21.5 KB. The gateway forwards the caller's headers, so raising the ceiling at
 * the edge alone was NOT enough -- measured: gateway answered 200 while every service behind it
 * still answered 431. Symptom fix; the real repair is to stop shipping permissions as claims.
 */
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024;
});


builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddProductLegalEntityScopeOperational(builder.Configuration);
builder.Services.Configure<AuditIntentTemporalMigrationOptions>(
    builder.Configuration.GetSection(AuditIntentTemporalMigrationOptions.SectionName));
builder.Services.Configure<AuditIntentDeliveryWorkerOptions>(
    builder.Configuration.GetSection(AuditIntentDeliveryWorkerOptions.SectionName));
builder.Services.AddHostedService<AuditIntentDeliveryWorker>();
builder.Services.Configure<ProductIdentityWorkflowOptions>(
    builder.Configuration.GetSection(ProductIdentityWorkflowOptions.SectionName));
builder.Services.Configure<ProductIdentityWorkflowWorkerOptions>(
    builder.Configuration.GetSection(ProductIdentityWorkflowWorkerOptions.SectionName));
builder.Services.Configure<GlobalProductCorrectionWorkflowOptions>(
    builder.Configuration.GetSection(GlobalProductCorrectionWorkflowOptions.SectionName));
builder.Services.Configure<GlobalProductCorrectionWorkflowWorkerOptions>(
    builder.Configuration.GetSection(GlobalProductCorrectionWorkflowWorkerOptions.SectionName));
builder.Services.Configure<GlobalProductRetirementRequestWorkflowOptions>(
    builder.Configuration.GetSection(GlobalProductRetirementRequestWorkflowOptions.SectionName));
builder.Services.Configure<GlobalProductRetirementRequestWorkflowWorkerOptions>(
    builder.Configuration.GetSection(GlobalProductRetirementRequestWorkflowWorkerOptions.SectionName));
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<ProductIdentityWorkflowOptions>>().Value;
    var configuration = options.Enabled
        ? options.ToStartConfiguration()
        : new ProductIdentityWorkflowStartConfiguration(null, null, [], string.Empty, false, false, null);
    return new ProductIdentityWorkflowStartRequestFactory(configuration, sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<GlobalProductCorrectionWorkflowOptions>>().Value;
    var identity = sp.GetRequiredService<IOptions<ProductIdentityWorkflowOptions>>().Value;
    var configuration = options.Enabled
        ? options.ToConfiguration(identity.GlobalProductTemplateId, identity.GlobalProductTemplateCode)
        : new GlobalProductCorrectionStartConfiguration(null, null, [], string.Empty, false, false, null,
            identity.GlobalProductTemplateId, identity.GlobalProductTemplateCode);
    return new GlobalProductCorrectionWorkflowStartRequestFactory(configuration, sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<GlobalProductRetirementRequestWorkflowOptions>>().Value;
    var identity = sp.GetRequiredService<IOptions<ProductIdentityWorkflowOptions>>().Value;
    var correction = sp.GetRequiredService<IOptions<GlobalProductCorrectionWorkflowOptions>>().Value;
    var configuration = options.Enabled
        ? options.ToConfiguration(identity.GlobalProductTemplateId, identity.GlobalProductTemplateCode,
            correction.TemplateId, correction.TemplateCode)
        : new GlobalProductRetirementRequestStartConfiguration(null, null, [], string.Empty, false, false, null,
            identity.GlobalProductTemplateId, identity.GlobalProductTemplateCode, correction.TemplateId, correction.TemplateCode);
    return new GlobalProductRetirementRequestWorkflowStartRequestFactory(configuration,
        sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddScoped<GlobalProductIdentityWorkflowProcessor>();
builder.Services.AddScoped<GlobalProductCorrectionWorkflowProcessor>();
builder.Services.AddScoped<GlobalProductRetirementRequestWorkflowProcessor>();
builder.Services.AddSingleton<ProductIdentityWorkflowRecoveryRunner>();
builder.Services.AddSingleton<GlobalProductCorrectionRecoveryRunner>();
builder.Services.AddSingleton<GlobalProductRetirementRequestRecoveryRunner>();
builder.Services.AddHostedService<ProductIdentityWorkflowRecoveryWorker>();
builder.Services.AddHostedService<GlobalProductCorrectionRecoveryWorker>();
builder.Services.AddHostedService<GlobalProductRetirementRequestRecoveryWorker>();

if (!runProductLegalEntityScopeOperational
    && !runAuditIntentTemporalMigration
    && !runProductIdentityWorkflowRecovery
    && !runGlobalProductCorrectionRecovery
    && !runGlobalProductRetirementRecovery)
{
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
}

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

if (runAuditIntentTemporalMigration)
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var runner = migrationScope.ServiceProvider
        .GetRequiredService<Diten.MdmService.Persistence.Repositories.AuditIntentTemporalMigrationRunner>();
    var result = await runner.RunAsync(
        auditIntentTemporalMigrationRequest!,
        app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation(
        "Audit intent temporal migration completed at phase {Phase}; scanned {ScannedCount}, migrated {MigratedCount}, current {AlreadyCurrentCount}.",
        result.Phase,
        result.ScannedCount,
        result.MigratedCount,
        result.AlreadyCurrentCount);
    return;
}

if (runProductLegalEntityScopeOperational)
{
    await using var operationalScope = app.Services.CreateAsyncScope();
    var runner = operationalScope.ServiceProvider
        .GetRequiredService<ProductLegalEntityScopeOperationalRunner>();
    var result = await ProductLegalEntityScopeOperationalCommandLine.RunAsync(
        runner,
        app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation(
        "Product Legal Entity scope operational action {Action} completed with rollout mode {Mode}, eligible {EligibleCount}, configured {ConfiguredCount}.",
        result.Action,
        result.Rollout.Mode,
        result.Completeness.EligibleGlobalProductCount,
        result.Completeness.ConfiguredGlobalProductCount);
    return;
}

if (runProductIdentityWorkflowRecovery)
{
    var runner = app.Services.GetRequiredService<ProductIdentityWorkflowRecoveryRunner>();
    var result = await ProductIdentityWorkflowRecoveryCommandLine.RunAsync(
        runner,
        app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation(
        "Product identity workflow recovery completed; tenants {TenantCount}, operations {OperationCount}, completed {CompletedCount}, deferred {DeferredCount}, failed {FailedCount}.",
        result.TenantCount,
        result.OperationCount,
        result.CompletedCount,
        result.DeferredCount,
        result.FailedCount);
    if (result.FailedCount > 0)
        throw new InvalidOperationException("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_FAILED");
    return;
}

if (runGlobalProductCorrectionRecovery)
{
    var runner = app.Services.GetRequiredService<GlobalProductCorrectionRecoveryRunner>();
    var result = await GlobalProductCorrectionRecoveryCommandLine.RunAsync(
        runner,
        app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation(
        "Global Product correction recovery completed; tenants {TenantCount}, operations {OperationCount}, completed {CompletedCount}, deferred {DeferredCount}, failed {FailedCount}.",
        result.TenantCount,
        result.OperationCount,
        result.CompletedCount,
        result.DeferredCount,
        result.FailedCount);
    if (result.FailedCount > 0)
        throw new InvalidOperationException("GLOBAL_PRODUCT_CORRECTION_RECOVERY_FAILED");
    return;
}

if (runGlobalProductRetirementRecovery)
{
    var runner = app.Services.GetRequiredService<GlobalProductRetirementRequestRecoveryRunner>();
    var result = await GlobalProductRetirementRequestRecoveryCommandLine.RunAsync(
        runner,
        app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation(
        "Global Product retirement recovery completed; tenants {TenantCount}, operations {OperationCount}, completed {CompletedCount}, deferred {DeferredCount}, failed {FailedCount}.",
        result.TenantCount,
        result.OperationCount,
        result.CompletedCount,
        result.DeferredCount,
        result.FailedCount);
    if (result.FailedCount > 0)
        throw new InvalidOperationException("GLOBAL_PRODUCT_RETIREMENT_RECOVERY_FAILED");
    return;
}

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
