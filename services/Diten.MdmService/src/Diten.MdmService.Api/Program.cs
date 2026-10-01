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

// The selected one-shot path never constructs the API host or its schema/worker registrations.
if (SelectedAuditIntentDeliveryCommandLine.IsRequested(args))
{
    await SelectedAuditIntentDeliveryRunner.RunFromProcessAsync(CancellationToken.None);
    return;
}
var apiStartupExecutionMode = ApiStartupExecutionMode.Parse(args);
var runProductLegalEntityScopeOperational =
    ProductLegalEntityScopeOperationalCommandLine.IsRequested(args);
var runAuditIntentTemporalMigration =
    AuditIntentTemporalMigrationCommandLine.IsRequested(args);
var runProductIdentityWorkflowRecovery =
    ProductIdentityWorkflowRecoveryCommandLine.IsRequested(args);
var runGlobalProductCorrectionRecovery =
    GlobalProductCorrectionRecoveryCommandLine.IsRequested(args);
var runFirstGskuIdentityWorkflowRecovery = FirstGskuIdentityWorkflowRecoveryCommandLine.IsRequested(args);
var runGskuCorrectionRecovery = GskuCorrectionRecoveryCommandLine.IsRequested(args);
var runGskuRetirementRequestRecovery = GskuRetirementRequestRecoveryCommandLine.IsRequested(args);
var runGlobalProductRetirementRecovery =
    GlobalProductRetirementRequestRecoveryCommandLine.IsRequested(args);
var runLskuIdentityWorkflowRecovery = LskuIdentityWorkflowRecoveryCommandLine.IsRequested(args);
var runLskuRetirementRequestRecovery = LskuRetirementRequestRecoveryCommandLine.IsRequested(args);
var operationalCommandCount = (runProductLegalEntityScopeOperational ? 1 : 0)
    + (runLskuIdentityWorkflowRecovery ? 1 : 0)
    + (runLskuRetirementRequestRecovery ? 1 : 0)
    + (runAuditIntentTemporalMigration ? 1 : 0)
    + (runProductIdentityWorkflowRecovery ? 1 : 0)
    + (runGlobalProductCorrectionRecovery ? 1 : 0)
    + (runFirstGskuIdentityWorkflowRecovery ? 1 : 0)
    + (runGskuCorrectionRecovery ? 1 : 0)
    + (runGskuRetirementRequestRecovery ? 1 : 0)
    + (runGlobalProductRetirementRecovery ? 1 : 0);
apiStartupExecutionMode.EnsureNoOperationalCommandConflict(operationalCommandCount);
if (operationalCommandCount > 1)
{
    throw new InvalidOperationException("MDM_OPERATIONAL_COMMAND_AMBIGUOUS");
}

var builder = WebApplication.CreateBuilder(args);
apiStartupExecutionMode.EnsureEnvironment(builder.Environment.EnvironmentName);
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
builder.Services.AddPersistence(builder.Configuration, apiStartupExecutionMode.RunStartupMaintenance);
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
builder.Services.AddGlobalProductWorkflowExecutionConfigurations();
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
builder.Services.AddOptions<FirstGskuIdentityWorkflowOptions>()
    .Bind(builder.Configuration.GetSection(FirstGskuIdentityWorkflowOptions.SectionName))
    .Validate(options => !options.Enabled || IsValidFirstGskuIdentityWorkflowOptions(options),
        "FIRST_GSKU_IDENTITY_WORKFLOW_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddOptions<FirstGskuIdentityWorkflowWorkerOptions>()
    .Bind(builder.Configuration.GetSection(FirstGskuIdentityWorkflowWorkerOptions.SectionName))
    .Validate(IsValidFirstGskuIdentityWorkflowWorkerOptions,
        "FIRST_GSKU_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<FirstGskuIdentityWorkflowOptions>>().Value;
    var configuration = options.Enabled
        ? options.ToStartConfiguration()
        : new FirstGskuIdentityWorkflowStartConfiguration(
            null, null, [], string.Empty, false, false, null);
    return new FirstGskuIdentityWorkflowStartRequestFactory(
        configuration,
        sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<FirstGskuIdentityWorkflowWorkerOptions>>().Value;
    return new FirstGskuIdentityWorkflowExecutionConfiguration(
        TimeSpan.FromSeconds(options.LeaseSeconds),
        TimeSpan.FromSeconds(options.RetryDelaySeconds));
});
builder.Services.AddScoped<FirstGskuIdentityWorkflowProcessor>();
builder.Services.AddScoped<FirstGskuIdentityRetirementProcessor>();
builder.Services.AddSingleton<FirstGskuIdentityWorkflowRecoveryRunner>();
builder.Services.AddHostedService<FirstGskuIdentityWorkflowRecoveryWorker>();
builder.Services.AddOptions<GskuCorrectionWorkflowOptions>()
    .Bind(builder.Configuration.GetSection(GskuCorrectionWorkflowOptions.SectionName))
    .Validate(options => !options.Enabled || IsValidGskuCorrectionOptions(options,
        builder.Configuration.GetSection(FirstGskuIdentityWorkflowOptions.SectionName)
            .Get<FirstGskuIdentityWorkflowOptions>() ?? new()),
        "GSKU_CORRECTION_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddOptions<GskuCorrectionWorkflowWorkerOptions>()
    .Bind(builder.Configuration.GetSection(GskuCorrectionWorkflowWorkerOptions.SectionName))
    .Validate(options => options.IsValid(), "GSKU_CORRECTION_WORKER_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<GskuCorrectionWorkflowOptions>>().Value;
    var identity = sp.GetRequiredService<IOptions<FirstGskuIdentityWorkflowOptions>>().Value;
    var configuration = options.Enabled
        ? options.ToConfiguration(identity.TemplateId, identity.TemplateCode)
        : new GskuCorrectionStartConfiguration(null, null, [], string.Empty, false, false,
            null, identity.TemplateId, identity.TemplateCode);
    return new GskuCorrectionWorkflowStartRequestFactory(configuration, sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<GskuCorrectionWorkflowWorkerOptions>>().Value;
    return new GskuCorrectionExecutionConfiguration(TimeSpan.FromSeconds(options.LeaseSeconds),
        TimeSpan.FromSeconds(options.RetryDelaySeconds));
});
builder.Services.AddScoped<GskuCorrectionWorkflowProcessor>();
builder.Services.AddSingleton<GskuCorrectionRecoveryRunner>();
builder.Services.AddHostedService<GskuCorrectionRecoveryWorker>();
builder.Services.AddOptions<GskuRetirementRequestWorkflowOptions>()
    .Bind(builder.Configuration.GetSection(GskuRetirementRequestWorkflowOptions.SectionName))
    .Validate(options => !options.Enabled || IsValidGskuRetirementRequestOptions(options,
        builder.Configuration.GetSection(FirstGskuIdentityWorkflowOptions.SectionName)
            .Get<FirstGskuIdentityWorkflowOptions>() ?? new(),
        builder.Configuration.GetSection(GskuCorrectionWorkflowOptions.SectionName)
            .Get<GskuCorrectionWorkflowOptions>() ?? new()),
        "GSKU_RETIREMENT_REQUEST_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddOptions<GskuRetirementRequestWorkflowWorkerOptions>()
    .Bind(builder.Configuration.GetSection(GskuRetirementRequestWorkflowWorkerOptions.SectionName))
    .Validate(options => options.IsValid(), "GSKU_RETIREMENT_REQUEST_WORKER_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<GskuRetirementRequestWorkflowOptions>>().Value;
    var identity = sp.GetRequiredService<IOptions<FirstGskuIdentityWorkflowOptions>>().Value;
    var correction = sp.GetRequiredService<IOptions<GskuCorrectionWorkflowOptions>>().Value;
    var configuration = options.Enabled
        ? options.ToConfiguration(identity.TemplateId, identity.TemplateCode,
            correction.TemplateId, correction.TemplateCode)
        : new GskuRetirementRequestStartConfiguration(null, null, [], string.Empty, false, false,
            null, identity.TemplateId, identity.TemplateCode, correction.TemplateId, correction.TemplateCode);
    return new GskuRetirementRequestWorkflowStartRequestFactory(configuration,
        sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<GskuRetirementRequestWorkflowWorkerOptions>>().Value;
    return new GskuRetirementRequestExecutionConfiguration(TimeSpan.FromSeconds(options.LeaseSeconds),
        TimeSpan.FromSeconds(options.RetryDelaySeconds));
});
builder.Services.AddScoped<GskuRetirementRequestWorkflowProcessor>();
builder.Services.AddSingleton<GskuRetirementRequestRecoveryRunner>();
builder.Services.AddHostedService<GskuRetirementRequestRecoveryWorker>();

builder.Services.AddOptions<LskuIdentityWorkflowOptions>()
    .Bind(builder.Configuration.GetSection(LskuIdentityWorkflowOptions.SectionName))
    .Validate(options => options.IsValid(), "LSKU_IDENTITY_WORKFLOW_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddOptions<LskuIdentityWorkflowWorkerOptions>()
    .Bind(builder.Configuration.GetSection(LskuIdentityWorkflowWorkerOptions.SectionName))
    .Validate(IsValidLskuIdentityWorkflowWorkerOptions,
        "LSKU_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<LskuIdentityWorkflowOptions>>().Value;
    var configuration = options.Enabled ? options.ToStartConfiguration()
        : new LskuIdentityWorkflowStartConfiguration(null, null, [], string.Empty, false, false, null);
    return new LskuIdentityWorkflowStartRequestFactory(configuration,
        sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<LskuIdentityWorkflowWorkerOptions>>().Value;
    return new LskuIdentityWorkflowExecutionConfiguration(
        TimeSpan.FromSeconds(options.LeaseSeconds), TimeSpan.FromSeconds(options.RetryDelaySeconds));
});
builder.Services.AddScoped<LskuIdentityWorkflowProcessor>();
builder.Services.AddSingleton<LskuIdentityWorkflowRecoveryRunner>();
builder.Services.AddHostedService<LskuIdentityWorkflowRecoveryWorker>();
builder.Services.AddOptions<LskuRetirementRequestWorkflowOptions>()
    .Bind(builder.Configuration.GetSection(LskuRetirementRequestWorkflowOptions.SectionName))
    .Validate(options => !options.Enabled || IsValidLskuRetirementRequestOptions(options,
        builder.Configuration.GetSection(LskuIdentityWorkflowOptions.SectionName)
            .Get<LskuIdentityWorkflowOptions>() ?? new()),
        "LSKU_RETIREMENT_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddOptions<LskuRetirementRequestWorkflowWorkerOptions>()
    .Bind(builder.Configuration.GetSection(LskuRetirementRequestWorkflowWorkerOptions.SectionName))
    .Validate(options => options.IsValid(), "LSKU_RETIREMENT_WORKER_CONFIGURATION_INVALID")
    .ValidateOnStart();
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<LskuRetirementRequestWorkflowOptions>>().Value;
    var identity = sp.GetRequiredService<IOptions<LskuIdentityWorkflowOptions>>().Value;
    var configuration = options.Enabled
        ? options.ToConfiguration(identity.TemplateId, identity.TemplateCode)
        : new LskuRetirementRequestStartConfiguration(null, null, [], string.Empty, false, false, null);
    return new LskuRetirementRequestWorkflowStartRequestFactory(configuration,
        sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<LskuRetirementRequestWorkflowWorkerOptions>>().Value;
    return new LskuRetirementRequestExecutionConfiguration(TimeSpan.FromSeconds(options.LeaseSeconds),
        TimeSpan.FromSeconds(options.RetryDelaySeconds));
});
builder.Services.AddScoped<LskuRetirementRequestWorkflowProcessor>();
builder.Services.AddSingleton<LskuRetirementRequestRecoveryRunner>();
builder.Services.AddHostedService<LskuRetirementRequestRecoveryWorker>();

if (!runProductLegalEntityScopeOperational
    && !runLskuIdentityWorkflowRecovery
    && !runLskuRetirementRequestRecovery
    && !runAuditIntentTemporalMigration
    && !runProductIdentityWorkflowRecovery
    && !runGlobalProductCorrectionRecovery
    && !runFirstGskuIdentityWorkflowRecovery
    && !runGskuCorrectionRecovery
    && !runGskuRetirementRequestRecovery
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
apiStartupExecutionMode.AddModuleRegistrationHostedService(builder.Services);

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

if (runLskuIdentityWorkflowRecovery)
{
    await LskuIdentityWorkflowRecoveryCommandLine.RunAsync(
        app.Services.GetRequiredService<LskuIdentityWorkflowRecoveryRunner>(), app.Lifetime.ApplicationStopping);
    return;
}
if (runLskuRetirementRequestRecovery)
{
    var result = await LskuRetirementRequestRecoveryCommandLine.RunAsync(
        app.Services.GetRequiredService<LskuRetirementRequestRecoveryRunner>(), app.Lifetime.ApplicationStopping);
    if (result.FailedCount > 0) throw new InvalidOperationException("LSKU_RETIREMENT_REQUEST_RECOVERY_FAILED");
    return;
}

if (runFirstGskuIdentityWorkflowRecovery)
{
    var runner = app.Services.GetRequiredService<FirstGskuIdentityWorkflowRecoveryRunner>();
    var result = await FirstGskuIdentityWorkflowRecoveryCommandLine.RunAsync(
        runner,
        app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation(
        "First GSKU identity workflow recovery completed; tenants {TenantCount}, operations {OperationCount}, completed {CompletedCount}, deferred {DeferredCount}, failed {FailedCount}.",
        result.TenantCount,
        result.OperationCount,
        result.CompletedCount,
        result.DeferredCount,
        result.FailedCount);
    if (result.FailedCount > 0)
        throw new InvalidOperationException("FIRST_GSKU_IDENTITY_WORKFLOW_RECOVERY_FAILED");
    return;
}

if (runGskuCorrectionRecovery)
{
    var runner = app.Services.GetRequiredService<GskuCorrectionRecoveryRunner>();
    var result = await GskuCorrectionRecoveryCommandLine.RunAsync(runner, app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation(
        "GSKU correction recovery completed; tenants {TenantCount}, operations {OperationCount}, completed {CompletedCount}, deferred {DeferredCount}, failed {FailedCount}.",
        result.TenantCount, result.OperationCount, result.CompletedCount, result.DeferredCount, result.FailedCount);
    if (result.FailedCount > 0) throw new InvalidOperationException("GSKU_CORRECTION_RECOVERY_FAILED");
    return;
}

if (runGskuRetirementRequestRecovery)
{
    var runner = app.Services.GetRequiredService<GskuRetirementRequestRecoveryRunner>();
    var result = await GskuRetirementRequestRecoveryCommandLine.RunAsync(
        runner, app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation(
        "GSKU retirement-request recovery completed; tenants {TenantCount}, operations {OperationCount}, completed {CompletedCount}, deferred {DeferredCount}, failed {FailedCount}.",
        result.TenantCount, result.OperationCount, result.CompletedCount, result.DeferredCount, result.FailedCount);
    if (result.FailedCount > 0)
        throw new InvalidOperationException("GSKU_RETIREMENT_REQUEST_RECOVERY_FAILED");
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

static bool IsValidLskuIdentityWorkflowWorkerOptions(LskuIdentityWorkflowWorkerOptions options)
{
    try { options.EnsureValidWhenEnabled(); return true; }
    catch (InvalidOperationException) { return false; }
}

static bool IsValidLskuRetirementRequestOptions(LskuRetirementRequestWorkflowOptions options,
    LskuIdentityWorkflowOptions identity)
{
    try { options.ToConfiguration(identity.TemplateId, identity.TemplateCode); return true; }
    catch (InvalidOperationException) { return false; }
}

static bool IsValidGskuCorrectionOptions(GskuCorrectionWorkflowOptions options,
    FirstGskuIdentityWorkflowOptions identity)
{
    try
    {
        var configuration = options.ToConfiguration(identity.TemplateId, identity.TemplateCode);
        _ = new GskuCorrectionWorkflowStartRequestFactory(configuration, TimeProvider.System);
        return true;
    }
    catch (InvalidOperationException) { return false; }
}

static bool IsValidGskuRetirementRequestOptions(GskuRetirementRequestWorkflowOptions options,
    FirstGskuIdentityWorkflowOptions identity, GskuCorrectionWorkflowOptions correction)
{
    try
    {
        _ = options.ToConfiguration(identity.TemplateId, identity.TemplateCode,
            correction.TemplateId, correction.TemplateCode);
        return true;
    }
    catch (InvalidOperationException) { return false; }
}

static bool IsValidFirstGskuIdentityWorkflowOptions(FirstGskuIdentityWorkflowOptions options)
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

static bool IsValidFirstGskuIdentityWorkflowWorkerOptions(
    FirstGskuIdentityWorkflowWorkerOptions options)
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

static void ValidateRequiredJwtSetting(string? value, string key)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Configuration error: '{key}' is missing or empty.");
    }
}

public static class GlobalProductWorkflowExecutionConfigurationRegistrations
{
    public static IServiceCollection AddGlobalProductWorkflowExecutionConfigurations(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ProductIdentityWorkflowWorkerOptions>>().Value;
            EnsureExecutionWindowIsValid(
                options.LeaseSeconds,
                options.RetryDelaySeconds,
                "PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID");
            return new ProductIdentityWorkflowExecutionConfiguration(
                TimeSpan.FromSeconds(options.LeaseSeconds),
                TimeSpan.FromSeconds(options.RetryDelaySeconds));
        });
        services.AddScoped(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GlobalProductCorrectionWorkflowWorkerOptions>>().Value;
            EnsureExecutionWindowIsValid(
                options.LeaseSeconds,
                options.RetryDelaySeconds,
                "GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID");
            return new GlobalProductCorrectionExecutionConfiguration(
                TimeSpan.FromSeconds(options.LeaseSeconds),
                TimeSpan.FromSeconds(options.RetryDelaySeconds));
        });
        services.AddScoped(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GlobalProductRetirementRequestWorkflowWorkerOptions>>().Value;
            EnsureExecutionWindowIsValid(
                options.LeaseSeconds,
                options.RetryDelaySeconds,
                "GLOBAL_PRODUCT_RETIREMENT_WORKER_CONFIGURATION_INVALID");
            return new GlobalProductRetirementRequestExecutionConfiguration(
                TimeSpan.FromSeconds(options.LeaseSeconds),
                TimeSpan.FromSeconds(options.RetryDelaySeconds));
        });

        return services;
    }

    private static void EnsureExecutionWindowIsValid(
        int leaseSeconds,
        int retryDelaySeconds,
        string errorCode)
    {
        if (leaseSeconds is < 10 or > 900 || retryDelaySeconds is < 1 or > 3_600)
        {
            throw new InvalidOperationException(errorCode);
        }
    }
}
