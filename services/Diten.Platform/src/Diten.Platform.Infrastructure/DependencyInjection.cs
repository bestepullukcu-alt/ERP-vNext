using System.Text;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Eventing;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.Audit;
using Diten.Platform.Infrastructure.Services.Http;
using Diten.Platform.Infrastructure.Settings;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MassTransit;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;

namespace Diten.Platform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSecretsProvider(configuration, environment, options => options.ServiceName = "Platform");
        services.ValidateRequiredSecrets(configuration, environment, "Platform", BuildSecretRequirements(configuration));

        var jwtSecret = configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("Configuration error: 'JwtSettings:Secret' is missing in appsettings.json.");
        var jwtIssuer = configuration["JwtSettings:Issuer"]
            ?? throw new InvalidOperationException("Configuration error: 'JwtSettings:Issuer' is missing in appsettings.json.");
        var jwtAudience = configuration["JwtSettings:Audience"]
            ?? throw new InvalidOperationException("Configuration error: 'JwtSettings:Audience' is missing in appsettings.json.");
        var jwtRotationResolver = new JwtSecretRotationResolver(configuration);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKeys = jwtRotationResolver.GetValidationKeys(),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("PlatformActor", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                {
                    var actorType = context.User.Claims
                        .FirstOrDefault(claim => string.Equals(claim.Type, "actor_type", StringComparison.OrdinalIgnoreCase))
                        ?.Value;

                    return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
                });
            });

            // Phase 5 baseline policy: audit endpoints accept ONLY platform_admin.
            // Partner admin audit scope support (per-tenant filter, partner-scoped redaction)
            // is follow-up; see MOD-0021 Phase 5A review H1/H2.
            options.AddPolicy("PlatformAdminOnly", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                {
                    var actorType = context.User.Claims
                        .FirstOrDefault(claim => string.Equals(claim.Type, "actor_type", StringComparison.OrdinalIgnoreCase))
                        ?.Value;

                    return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase);
                });
            });
        });
        services.AddSingleton<IAuthorizationPolicyProvider, EntitlementAuthorizationPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, TenantModuleAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, TenantFeatureAuthorizationHandler>();
        services.AddScoped<IEntitlementAuditSink, PlatformEntitlementAuditSink>();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.Configure<EntitlementCacheOptions>(configuration.GetSection(EntitlementCacheOptions.SectionName));
        services.Configure<TenantManagementOptions>(configuration.GetSection(TenantManagementOptions.SectionName));
        services.Configure<AuditRetentionSeedOptions>(configuration.GetSection(AuditRetentionSeedOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<AuthServiceOptions>(configuration.GetSection(AuthServiceOptions.SectionName));
        services.Configure<EventBusOptions>(configuration.GetSection(EventBusOptions.SectionName));
        services.Configure<RabbitMqEventingOptions>(configuration.GetSection(RabbitMqEventingOptions.SectionName));
        services.Configure<BackgroundJobSchedulerOptions>(configuration.GetSection(BackgroundJobSchedulerOptions.SectionName));

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<ITenantDefaultsProvider, TenantDefaultsProvider>();
        services.AddSingleton<EntitlementCacheService>();
        services.AddScoped<IEntitlementChecker, EntitlementChecker>();
        services.AddScoped<IAdminUserInvitationService, AdminUserInvitationService>();
        services.AddScoped<IPlatformLookupCache, PlatformLookupMemoryCache>();
        services.AddScoped<IPlatformAdministratorProvisioningService, PlatformAdministratorProvisioningService>();
        services.AddScoped<IPlatformAdministratorInvitationEmailService, PlatformAdministratorInvitationEmailService>();
        services.AddTransient<TenantPropagationHandler>();
        services.AddHttpClient("TenantAwareClient").AddHttpMessageHandler<TenantPropagationHandler>();

        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));

        var connectionString = configuration["MongoDbSettings:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration error: 'MongoDbSettings:ConnectionString' is missing in appsettings.json.");
        var databaseName = configuration["MongoDbSettings:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration error: 'MongoDbSettings:DatabaseName' is missing in appsettings.json.");

        var mongoSettings = new MongoDbSettings
        {
            ConnectionString = connectionString,
            DatabaseName = databaseName,
            AllowStartupWithoutDatabase = configuration.GetValue<bool>("MongoDbSettings:AllowStartupWithoutDatabase")
        };

        services.AddSingleton(mongoSettings);
        var mongoClientSettings = MongoClientSettings.FromConnectionString(mongoSettings.ConnectionString);
        mongoClientSettings.GuidRepresentation = GuidRepresentation.Standard;
        var mongoClient = new MongoClient(mongoClientSettings);
        var database = mongoClient.GetDatabase(mongoSettings.DatabaseName);

        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddSingleton<IPlatformDbContext>(new PlatformDbContext(mongoClient, database));
        services.AddScoped<IMongoDatabase>(_ => database);
        services.AddScoped<ISavedViewRepository, SavedViewRepository>();
        services.AddScoped<ITenantRegistryRepository, TenantRegistryRepository>();
        services.AddScoped<ITenantDomainRepository, TenantDomainRepository>();
        services.AddScoped<ITenantLoginSettingsRepository, TenantLoginSettingsRepository>();
        services.AddScoped<IModuleCatalogRepository, ModuleCatalogRepository>();
        services.AddScoped<IModulePageDescriptorRepository, ModulePageDescriptorRepository>();
        services.AddScoped<IModulePageActionDescriptorRepository, ModulePageActionDescriptorRepository>();
        services.AddScoped<IPlatformAdministratorRepository, PlatformAdministratorRepository>();
        services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
        services.AddScoped<ITenantSubscriptionRepository, TenantSubscriptionRepository>();
        services.AddScoped<ITenantModuleEntitlementRepository, TenantModuleEntitlementRepository>();
        services.AddScoped<IQuotaUsageRepository, QuotaUsageRepository>();
        services.AddScoped<IQuotaEventRepository, QuotaEventRepository>();
        services.AddScoped<IInterfaceRegistryRepository, InterfaceRegistryRepository>();
        services.AddScoped<IFeatureDefinitionRepository, FeatureDefinitionRepository>();
        services.AddScoped<IFeatureCategoryRepository, FeatureCategoryRepository>();
        services.AddScoped<IPlanFeatureMappingRepository, PlanFeatureMappingRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IAuditRetentionPolicyRepository, AuditRetentionPolicyRepository>();
        services.AddScoped<ITenantAuditPreferenceRepository, TenantAuditPreferenceRepository>();
        services.AddScoped<AuditOutboxRepository>();
        services.AddScoped<IAuditOutboxWriter>(provider => provider.GetRequiredService<AuditOutboxRepository>());
        services.AddScoped<IAuditOutboxProcessingRepository>(provider => provider.GetRequiredService<AuditOutboxRepository>());
        services.AddSingleton<AuditOutboxWorkerOptions>();
        services.AddScoped<AuditOutboxPayloadMapper>();
        services.AddScoped<AuditOutboxProcessor>();
        services.AddHostedService<AuditOutboxWorker>();

        services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
        services.AddScoped<IOutboxObservabilityReader>(sp => (IOutboxObservabilityReader)sp.GetRequiredService<IOutboxEventRepository>());
        services.AddScoped<IConsumedEventRepository, ConsumedEventRepository>();
        services.AddScoped<IJobExecutionLogRepository, JobExecutionLogRepository>();
        services.AddScoped<OutboxPublisherProcessor>();
        services.AddScoped<HangfireBackgroundJobExecutor>();

        ConfigureHangfire(services, configuration, mongoSettings);

        var eventingOptions = configuration.GetSection(RabbitMqEventingOptions.SectionName).Get<RabbitMqEventingOptions>()
                              ?? new RabbitMqEventingOptions();
        if (eventingOptions.UseRabbitMq)
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumer<TenantActivatedV1Consumer>();
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(eventingOptions.Host, eventingOptions.Port, eventingOptions.VirtualHost, h =>
                    {
                        h.Username(eventingOptions.Username);
                        h.Password(eventingOptions.Password);
                        if (eventingOptions.UseTls)
                        {
                            h.UseSsl(s => s.Protocol = System.Security.Authentication.SslProtocols.Tls12);
                        }
                    });

                    cfg.UseMessageRetry(r => r.Exponential(
                        eventingOptions.RetryCount,
                        TimeSpan.FromSeconds(eventingOptions.InitialRetryDelaySeconds),
                        TimeSpan.FromSeconds(eventingOptions.MaxRetryDelaySeconds),
                        TimeSpan.FromSeconds(eventingOptions.InitialRetryDelaySeconds)));
                    cfg.ConfigureEndpoints(context);
                });
            });
            services.AddScoped<IEventTransportPublisher, MassTransitRabbitMqEventPublisher>();
        }
        else
        {
            services.AddSingleton<InMemoryEventBus>();
            services.AddSingleton<IEventTransportPublisher>(sp => sp.GetRequiredService<InMemoryEventBus>());
        }

        services.AddHostedService<OutboxPublisherWorker>();

        var auditRetentionSeedOptions = configuration
            .GetSection(AuditRetentionSeedOptions.SectionName)
            .Get<AuditRetentionSeedOptions>()
            ?? throw new InvalidOperationException($"Configuration error: '{AuditRetentionSeedOptions.SectionName}' is missing in appsettings.json.");
        AuditRetentionPolicySeed.EnsureSeededAsync(database, auditRetentionSeedOptions).GetAwaiter().GetResult();

        RunMongoStartupInitialization(database, mongoSettings);

        return services;
    }

    private static void RunMongoStartupInitialization(IMongoDatabase database, MongoDbSettings mongoSettings)
    {
        try
        {
            LegacySavedViewMigration.MigrateAsync(database).GetAwaiter().GetResult();
            MongoDbIndexConfigurations.EnsureIndexesAsync(database).GetAwaiter().GetResult();
            SubscriptionPlanSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            PlatformAdministratorSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            TenantSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (mongoSettings.AllowStartupWithoutDatabase)
        {
            Console.Error.WriteLine(
                $"Platform MongoDB startup initialization failed ({ex.GetType().Name}). Startup continues because MongoDbSettings:AllowStartupWithoutDatabase=true; readiness will report MongoDB status.");
        }
    }

    private static void ConfigureHangfire(IServiceCollection services, IConfiguration configuration, MongoDbSettings mongoSettings)
    {
        var schedulerOptions = configuration.GetSection(BackgroundJobSchedulerOptions.SectionName)
            .Get<BackgroundJobSchedulerOptions>() ?? new BackgroundJobSchedulerOptions();

        if (!schedulerOptions.Enabled && !schedulerOptions.DashboardEnabled)
        {
            return;
        }

        var storageDatabaseName = string.IsNullOrWhiteSpace(schedulerOptions.StorageDatabaseName)
            ? mongoSettings.DatabaseName
            : schedulerOptions.StorageDatabaseName;

        services.AddHangfire(config =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseMongoStorage(
                    mongoSettings.ConnectionString,
                    storageDatabaseName,
                    new MongoStorageOptions
                    {
                        CheckConnection = true,
                        CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.TailNotificationsCollection,
                        MigrationOptions = new MongoMigrationOptions
                        {
                            MigrationStrategy = new MigrateMongoMigrationStrategy(),
                            BackupStrategy = new CollectionMongoBackupStrategy()
                        }
                    });
        });
        services.AddSingleton<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();

        if (schedulerOptions.Enabled)
        {
            services.AddHangfireServer(options =>
            {
                options.ServerName = $"{Environment.MachineName}.Diten.Platform";
                options.Queues = new[] { "platform", "default" };
            });
            services.AddHostedService<HangfireRecurringJobRegistrationHostedService>();
        }
    }

    private static IReadOnlyList<RequiredSecretDefinition> BuildSecretRequirements(IConfiguration configuration)
    {
        var smtpEnabled = configuration.GetValue<bool>("Smtp:Enabled");

        return
        [
            new("JwtSettings:Secret", "Platform", SecretRequirementKind.JwtCurrent),
            new("JwtSettings:PreviousSecrets", "Platform", SecretRequirementKind.JwtPreviousCollection, Required: false),
            new("MongoDbSettings:ConnectionString", "Platform", SecretRequirementKind.ConnectionString),
            new("AuthService:InternalApiKey", "Platform", SecretRequirementKind.InternalApiKey),
            new("Smtp:Password", "Platform", MinimumLength: 8, Required: smtpEnabled, IsEnabled: () => smtpEnabled)
        ];
    }
}
