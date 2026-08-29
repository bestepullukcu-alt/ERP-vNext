using System.Text;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductLegalEntityScopes;
using Diten.MdmService.Api.Services.Audit;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var runProductLegalEntityScopeOperational =
    ProductLegalEntityScopeOperationalCommandLine.IsRequested(args);
var runAuditIntentTemporalMigration =
    AuditIntentTemporalMigrationCommandLine.IsRequested(args);
var runProductIdentityWorkflowRecovery =
    ProductIdentityWorkflowRecoveryCommandLine.IsRequested(args);
if ((runProductLegalEntityScopeOperational ? 1 : 0)
    + (runAuditIntentTemporalMigration ? 1 : 0)
    + (runProductIdentityWorkflowRecovery ? 1 : 0) > 1)
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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddProductLegalEntityScopeOperational(builder.Configuration);
builder.Services.Configure<AuditIntentTemporalMigrationOptions>(
    builder.Configuration.GetSection(AuditIntentTemporalMigrationOptions.SectionName));
builder.Services.Configure<AuditIntentDeliveryWorkerOptions>(
    builder.Configuration.GetSection(AuditIntentDeliveryWorkerOptions.SectionName));
builder.Services.AddHostedService<AuditIntentDeliveryWorker>();
builder.Services.AddOptions<ProductIdentityWorkflowOptions>()
    .Bind(builder.Configuration.GetSection(ProductIdentityWorkflowOptions.SectionName))
    .Validate(options => !options.Enabled || IsValidProductIdentityWorkflowOptions(options),
        "PRODUCT_IDENTITY_WORKFLOW_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddOptions<ProductIdentityWorkflowWorkerOptions>()
    .Bind(builder.Configuration.GetSection(ProductIdentityWorkflowWorkerOptions.SectionName))
    .Validate(IsValidProductIdentityWorkflowWorkerOptions,
        "PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<ProductIdentityWorkflowOptions>>().Value;
    var configuration = options.Enabled
        ? options.ToStartConfiguration()
        : new ProductIdentityWorkflowStartConfiguration(null, null, [], string.Empty, false, false, null);
    return new ProductIdentityWorkflowStartRequestFactory(
        configuration,
        sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<ProductIdentityWorkflowWorkerOptions>>().Value;
    return new ProductIdentityWorkflowExecutionConfiguration(
        TimeSpan.FromSeconds(options.LeaseSeconds),
        TimeSpan.FromSeconds(options.RetryDelaySeconds));
});
builder.Services.AddScoped<GlobalProductIdentityWorkflowProcessor>();
builder.Services.AddSingleton<ProductIdentityWorkflowRecoveryRunner>();
builder.Services.AddHostedService<ProductIdentityWorkflowRecoveryWorker>();

if (!runProductLegalEntityScopeOperational
    && !runAuditIntentTemporalMigration
    && !runProductIdentityWorkflowRecovery)
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
}

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

static bool IsValidProductIdentityWorkflowOptions(ProductIdentityWorkflowOptions options)
{
    try
    {
        _ = options.ToStartConfiguration();
        return true;
    }
    catch (InvalidOperationException)
    {
        return false;
    }
}

static bool IsValidProductIdentityWorkflowWorkerOptions(ProductIdentityWorkflowWorkerOptions options)
{
    if (options.LeaseSeconds is < 10 or > 900
        || options.RetryDelaySeconds is < 1 or > 3_600)
    {
        return false;
    }

    if (!options.Enabled)
    {
        return true;
    }

    try
    {
        options.EnsureValidWhenEnabled();
        return true;
    }
    catch (InvalidOperationException)
    {
        return false;
    }
}
