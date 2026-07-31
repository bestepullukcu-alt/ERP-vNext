using System.Text;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Eventing;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories.BusinessReferenceData;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.Audit;
using Diten.Platform.Infrastructure.Services.Http;
using Diten.Platform.Infrastructure.Services.Mdm;
using Diten.Platform.Infrastructure.Services.Notifications;
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
        services.Configure<BusinessReferenceDataCatalogLoadOptions>(configuration.GetSection(BusinessReferenceDataCatalogLoadOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<AuthServiceOptions>(configuration.GetSection(AuthServiceOptions.SectionName));
        // MOD-0024 §12 K2 — which workflow template task approval starts. A tenant that designs its own flow in
        // the Workflow Designer overrides Tasks:Approval:TemplateCode; the built-in default is only a fallback.
        services.Configure<Diten.Platform.Application.Features.Tasks.Services.TaskApprovalOptions>(
            configuration.GetSection(Diten.Platform.Application.Features.Tasks.Services.TaskApprovalOptions.SectionName));
        // Faz 3b — which workflow template task REVIEW starts. Same override story as approval above, under its
        // own section so a tenant can point review and approval at different flows.
        services.Configure<Diten.Platform.Application.Features.Tasks.Services.TaskReviewOptions>(
            configuration.GetSection(Diten.Platform.Application.Features.Tasks.Services.TaskReviewOptions.SectionName));
        // WC-2 — how far ahead of a deadline the warning window opens. A POLICY a tenant may tune, which is why
        // it is here and not declared in the executable contract (the contract declares the state VOCABULARY).
        services.Configure<Diten.Platform.Application.Features.WorkAggregation.Services.WorkItemSlaOptions>(
            configuration.GetSection(
                Diten.Platform.Application.Features.WorkAggregation.Services.WorkItemSlaOptions.SectionName));
        services.Configure<MdmServiceOptions>(configuration.GetSection(MdmServiceOptions.SectionName));
        services.Configure<FakeMessagingProviderOptions>(configuration.GetSection(FakeMessagingProviderOptions.SectionName));
        services.AddOptions<SmtpProviderOptions>()
            .Bind(configuration.GetSection(SmtpProviderOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SmtpProviderOptions>, SmtpProviderOptionsValidator>();
        services.Configure<EventBusOptions>(configuration.GetSection(EventBusOptions.SectionName));
        services.Configure<RabbitMqEventingOptions>(configuration.GetSection(RabbitMqEventingOptions.SectionName));
        services.Configure<BackgroundJobSchedulerOptions>(configuration.GetSection(BackgroundJobSchedulerOptions.SectionName));
        // MOD-0029-FU01 — controlled-document feature flags + Phase 1 content-storage options.
        services.Configure<Diten.Platform.Application.Features.DocumentManagementControlledDocuments.ControlledDocumentsFeatureFlagOptions>(
            configuration.GetSection(Diten.Platform.Application.Features.DocumentManagementControlledDocuments.ControlledDocumentsFeatureFlagOptions.SectionName));
        services.Configure<Diten.Platform.Application.Features.DocumentManagementControlledDocuments.ContentStorageOptions>(
            configuration.GetSection(Diten.Platform.Application.Features.DocumentManagementControlledDocuments.ContentStorageOptions.SectionName));
        // MOD-0029-FU04 — access matrix rollout/enforcement mode (defaults to Compatibility when unset).
        services.Configure<Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services.AccessMatrixOptions>(
            configuration.GetSection(Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services.AccessMatrixOptions.SectionName));
        // MOD-0029-FU05 — access-profile template role mapping.
        services.Configure<Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates.AccessProfileTemplateOptions>(
            configuration.GetSection(Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates.AccessProfileTemplateOptions.SectionName));

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddTenantAuthorizationContext();
        services.AddScoped<ITenantDefaultsProvider, TenantDefaultsProvider>();
        services.AddSingleton<EntitlementCacheService>();
        services.AddScoped<IEntitlementChecker, EntitlementChecker>();
        services.AddScoped<IAdminUserInvitationService, AdminUserInvitationService>();
        services.AddScoped<ITenantActivationNotifier, AuthServiceTenantActivationNotifier>();
        services.AddScoped<ICatalogPermissionSyncService, CatalogPermissionSyncService>();
        services.AddScoped<IAuthPermissionModulesClient, AuthPermissionModulesClient>();
        // MOD-0024 §K6.4 — display-name resolution for task assignees/requesters (best-effort S2S).
        services.AddScoped<IUserDisplayNameResolver, AuthUserDisplayNameClient>();
        services.AddScoped<IPlatformLookupCache, PlatformLookupMemoryCache>();
        services.AddScoped<IPlatformAdministratorProvisioningService, PlatformAdministratorProvisioningService>();
        services.AddScoped<IPlatformAdministratorInvitationEmailService, PlatformAdministratorInvitationEmailService>();
        services.AddTransient<TenantPropagationHandler>();
        services.AddHttpClient("TenantAwareClient").AddHttpMessageHandler<TenantPropagationHandler>();
        services.AddHttpClient<ILegalEntityReferenceValidator, MdmLegalEntityReferenceValidator>()
            .AddHttpMessageHandler<TenantPropagationHandler>();
        services.AddHttpClient<IUserReferenceValidator, Diten.Platform.Infrastructure.Services.Auth.AuthServiceUserReferenceValidator>()
            .AddHttpMessageHandler<TenantPropagationHandler>();

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
        services.AddScoped<IModuleDomainRepository, ModuleDomainRepository>();
        services.AddScoped<IModuleServiceRepository, ModuleServiceRepository>();
        services.AddScoped<IModulePageDescriptorRepository, ModulePageDescriptorRepository>();
        services.AddScoped<IModulePageActionDescriptorRepository, ModulePageActionDescriptorRepository>();
        services.AddScoped<ITenantNavPreferenceRepository, TenantNavPreferenceRepository>();
        services.AddScoped<ITenantNavDomainPreferenceRepository, TenantNavDomainPreferenceRepository>();
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
        services.AddScoped<IBusinessReferenceDataStewardshipRepository, BusinessReferenceDataStewardshipRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IAuditRetentionPolicyRepository, AuditRetentionPolicyRepository>();
        services.AddScoped<ITenantAuditPreferenceRepository, TenantAuditPreferenceRepository>();
        services.AddScoped<ITenantMessagingSettingsRepository, TenantMessagingSettingsRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationDispatchRepository, NotificationDispatchRepository>();
        services.AddScoped<INotificationEventDefinitionRepository, NotificationEventDefinitionRepository>();
        services.AddScoped<IOrganizationUnitRepository, OrganizationUnitRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IPositionAssignmentRepository, PositionAssignmentRepository>();
        services.AddScoped<IPersonReferenceRepository, PersonReferenceRepository>();

        // Workflow Repositories
        services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
        services.AddScoped<IWorkflowTemplateVersionRepository, WorkflowTemplateVersionRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        services.AddScoped<IApprovalTaskRepository, ApprovalTaskRepository>();
        services.AddScoped<IRuntimeAssignmentSnapshotRepository, RuntimeAssignmentSnapshotRepository>();
        services.AddScoped<IWorkflowTransitionLogRepository, WorkflowTransitionLogRepository>();
        services.AddScoped<ISlaEscalationRuleRepository, SlaEscalationRuleRepository>();

        // MOD-0024 Task Engine Repositories (Phase 1 uses the first five; the rest exist so Phases 2–5 are additive)
        services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        services.AddScoped<ITaskAssignmentRepository, TaskAssignmentRepository>();
        services.AddScoped<ITaskDependencyRepository, TaskDependencyRepository>();
        services.AddScoped<ITaskWatcherRepository, TaskWatcherRepository>();
        services.AddScoped<ITaskCommentRepository, TaskCommentRepository>();
        services.AddScoped<ITaskFieldDefinitionRepository, TaskFieldDefinitionRepository>();
        services.AddScoped<IChecklistTemplateRepository, ChecklistTemplateRepository>();
        services.AddScoped<IChecklistRunRepository, ChecklistRunRepository>();
        services.AddScoped<ITaskTemplateRepository, TaskTemplateRepository>();
        services.AddScoped<ITaskRecurrenceRuleRepository, TaskRecurrenceRuleRepository>();

        /*
         * WC-4 — task notification recipients, resolved from AuthService.
         *
         * THIS LINE is the seam: swap the implementation and every task notification is addressed differently.
         * Before it existed, MOD-0024 put the recipient's user GUID into the email field, so no task
         * notification had ever been deliverable.
         */
        services.AddScoped<Diten.Platform.Application.Contracts.ITaskNotificationRecipientResolver,
            Services.AuthTaskNotificationRecipientClient>();

        // Document Management Repositories
        services.AddScoped<IBaselineReleaseRepository, BaselineReleaseRepository>();
        services.AddScoped<ICollectionDefinitionRepository, CollectionDefinitionRepository>();
        services.AddScoped<IBaselineSnapshotManifestRepository, BaselineSnapshotManifestRepository>();
        services.AddScoped<ICollectionInstanceRepository, CollectionInstanceRepository>();
        services.AddScoped<IInstantiationOperationRepository, InstantiationOperationRepository>();
        services.AddScoped<IInstantiationOutcomeRepository, InstantiationOutcomeRepository>();

        // MOD-0029-FU01 — controlled documents / templates / versions / shares repositories + seams.
        services.AddScoped<IControlledDocumentRepository, ControlledDocumentRepository>();
        services.AddScoped<IControlledDocumentVersionRepository, ControlledDocumentVersionRepository>();
        services.AddScoped<ITemplateDocumentRepository, TemplateDocumentRepository>();
        services.AddScoped<ITemplateVersionRepository, TemplateVersionRepository>();
        services.AddScoped<ITemplateMasterRepository, TemplateMasterRepository>();
        services.AddScoped<ITemplateMasterVersionRepository, TemplateMasterVersionRepository>();
        // MOD-0029-FU03 — tenant-scoped template variant governance + drift repository.
        services.AddScoped<ITemplateVariantRepository, TemplateVariantRepository>();
        // MOD-0028-FU09 — provisioning evidence + read-back deviation repositories (sidecar).
        services.AddScoped<IProvisioningEvidenceRepository, ProvisioningEvidenceRepository>();
        services.AddScoped<IDocumentCollectionDeviationRepository, DocumentCollectionDeviationRepository>();
        // MOD-0029-FU04 — generalized document access matrix policy repository.
        services.AddScoped<IDocumentAccessPolicyRepository, DocumentAccessPolicyRepository>();
        services.AddScoped<IFolderDocumentAccessPolicyRepository, FolderDocumentAccessPolicyRepository>();
        services.AddScoped<IDocumentShareRecordRepository, DocumentShareRecordRepository>();
        services.AddScoped<IFolderShareOperationRepository, FolderShareOperationRepository>();
        services.AddScoped<IFolderShareOutcomeRepository, FolderShareOutcomeRepository>();
        services.AddScoped<IDocumentFavoriteRepository, DocumentFavoriteRepository>();
        services.AddScoped<Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services.ICollectionInstanceReferenceReader,
            Diten.Platform.Infrastructure.Services.DocumentManagement.CollectionInstanceReferenceReader>();
        services.AddScoped<Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services.IDocumentAccessPrincipalAccessor,
            Diten.Platform.Infrastructure.Services.DocumentManagement.DocumentAccessPrincipalAccessor>();
        services.AddScoped<Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services.IContentStorageGateway,
            Diten.Platform.Infrastructure.Services.DocumentManagement.LocalFileSystemContentStorageGateway>();

        services.AddScoped<IMessagingProvider, FakeMessagingProvider>();
        services.AddScoped<IMessagingProvider, SmtpMessagingProvider>();
        services.AddSingleton<ISmtpClientFactory, MailKitSmtpClientFactory>();
        services.AddScoped<SecretReferenceResolver>();
        services.AddScoped<IMessagingProviderResolver, MessagingProviderResolver>();
        services.AddScoped<AuditOutboxRepository>();
        services.AddScoped<IAuditOutboxWriter>(provider => provider.GetRequiredService<AuditOutboxRepository>());
        services.AddScoped<IAuditOutboxProcessingRepository>(provider => provider.GetRequiredService<AuditOutboxRepository>());
        services.AddSingleton<AuditOutboxWorkerOptions>();
        services.AddScoped<AuditOutboxPayloadMapper>();
        services.AddScoped<AuditOutboxProcessor>();
        services.AddHostedService<AuditOutboxWorker>();

        LegacySavedViewMigration.MigrateAsync(database).GetAwaiter().GetResult();
        // MC-2 — drop duplicate live module-service rows before the unique partial index is (re)created.
        ModuleServiceDeduplicationMigration.MigrateAsync(database).GetAwaiter().GetResult();
        // FIX-DOMAIN-DEDUP — collapse cross-format duplicate domain rows + backfill CodeKey BEFORE the unique
        // partial index (ux_platform_module_domains_code_key) is (re)created, else the index build would fail.
        ModuleDomainDeduplicationMigration.MigrateAsync(database).GetAwaiter().GetResult();
        MongoDbIndexConfigurations.EnsureIndexesAsync(database).GetAwaiter().GetResult();
        var auditRetentionSeedOptions = configuration
            .GetSection(AuditRetentionSeedOptions.SectionName)
            .Get<AuditRetentionSeedOptions>()
            ?? throw new InvalidOperationException($"Configuration error: '{AuditRetentionSeedOptions.SectionName}' is missing in appsettings.json.");
        AuditRetentionPolicySeed.EnsureSeededAsync(database, auditRetentionSeedOptions).GetAwaiter().GetResult();
        SubscriptionPlanSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
        PlatformAdministratorSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
        TenantSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
        NotificationTemplateSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
        // WC-4 — the platform-default messaging settings row. Without it QueueEmailNotificationHandler refuses at its
        // FIRST line and no producer's notification ever reaches a template, a locale or a provider. Derived from the
        // Smtp section so that block finally configures what it appears to configure. Idempotent; never overrides a
        // row an operator created.
        NotificationMessagingSettingsSeed.EnsureSeededAsync(
                database,
                configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>() ?? new SmtpOptions())
            .GetAwaiter().GetResult();
        // BL-042 — stamp AcceptedByUserId on tasks accepted under the OLD inferred rule. Without this every
        // already-accepted task reverts to pendingAcceptance on deploy and the tenant's My Work empties into the
        // Inbox. Idempotent: only unstamped rows are touched.
        TaskAcceptanceBackfillMigration.MigrateAsync(database).GetAwaiter().GetResult();
        // MOD-0027-FU03A (Bridge) — PlatformSeed/SystemSeed notification events; runs after templates exist. No-op
        // until FU04A adds seed content.
        NotificationEventSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
        ModuleCatalogSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
        ModuleDomainSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
        ModuleServiceSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
        // FIX-DOMAIN-SERVICE-CANONICAL — must run AFTER the domain/service lookups are seeded: pins catalog
        // Domain/Service to canonical Codes and fixes the 'Servicec' DisplayName typo. Marker-gated + idempotent.
        ModuleCatalogTaxonomyCanonicalizationMigration.MigrateAsync(database).GetAwaiter().GetResult();
        PositionSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
        PositionAssignmentSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();

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
                AddPlatformEventConsumers(x);
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

        RunMongoStartupInitialization(
            database,
            mongoSettings,
            configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>() ?? new SmtpOptions());

        return services;
    }

    public static IServiceCollection AddTenantAuthorizationContext(this IServiceCollection services)
    {
        services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
        return services;
    }

    internal static void AddPlatformEventConsumers(IBusRegistrationConfigurator configurator)
    {
        // Default competing-consumer topology: each of these runs once cluster-wide (no duplicate side-effects).
        configurator.AddConsumer<TenantActivatedV1Consumer>();
        configurator.AddConsumer<TenantLifecycleAuditConsumer>();
        configurator.AddConsumer<TenantLifecycleNotificationConsumer>();

        // AG-STEP-010 / MOD-0018-FU13 Group A — per-instance fan-out (OD-FU13-02, A-Option 1).
        // The entitlement cache lives in a per-instance IMemoryCache, so EVERY instance must receive each
        // invalidation event and evict its own copy. Bind ONLY this consumer to a per-instance, temporary
        // (non-durable + auto-delete) receive endpoint via a process-lifetime InstanceId. ConfigureEndpoints(context)
        // honours this per-consumer endpoint definition (one endpoint per consumer — no duplicate binding). The other
        // three consumers keep the shared competing-consumer queue above.
        configurator.AddConsumer<EntitlementCacheInvalidationConsumer>()
            .Endpoint(endpoint =>
            {
                endpoint.InstanceId = PlatformInstanceIdentity.InstanceId;
                endpoint.Temporary = true;
            });
    }

    private static void RunMongoStartupInitialization(
        IMongoDatabase database,
        MongoDbSettings mongoSettings,
        SmtpOptions smtpOptions)
    {
        try
        {
            LegacySavedViewMigration.MigrateAsync(database).GetAwaiter().GetResult();
            // MC-2 — drop duplicate live module-service rows before the unique partial index is (re)created.
            ModuleServiceDeduplicationMigration.MigrateAsync(database).GetAwaiter().GetResult();
            // FIX-DOMAIN-DEDUP — collapse cross-format duplicate domain rows + backfill CodeKey BEFORE the unique
            // partial index (ux_platform_module_domains_code_key) is (re)created, else the index build would fail.
            ModuleDomainDeduplicationMigration.MigrateAsync(database).GetAwaiter().GetResult();
            MongoDbIndexConfigurations.EnsureIndexesAsync(database).GetAwaiter().GetResult();
            SubscriptionPlanSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            PlatformAdministratorSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            TenantSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            NotificationTemplateSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            // WC-4 — the platform-default messaging settings row. Without it QueueEmailNotificationHandler refuses at its
            // FIRST line and no producer's notification ever reaches a template, a locale or a provider. Derived from the
            // Smtp section so that block finally configures what it appears to configure. Idempotent; never overrides a
            // row an operator created.
            NotificationMessagingSettingsSeed.EnsureSeededAsync(database, smtpOptions).GetAwaiter().GetResult();
            // BL-042 — stamp AcceptedByUserId on tasks accepted under the OLD inferred rule. Without this every
            // already-accepted task reverts to pendingAcceptance on deploy and the tenant's My Work empties into the
            // Inbox. Idempotent: only unstamped rows are touched.
            TaskAcceptanceBackfillMigration.MigrateAsync(database).GetAwaiter().GetResult();
            // MOD-0027-FU03A (Bridge) — PlatformSeed/SystemSeed notification events; runs after templates exist. No-op
            // until FU04A adds seed content.
            NotificationEventSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            ModuleCatalogSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            ModuleDomainSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            ModuleServiceSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            // FIX-DOMAIN-SERVICE-CANONICAL — after the lookups are seeded: canonicalize catalog Domain/Service and
            // fix the 'Servicec' DisplayName typo. Marker-gated + idempotent.
            ModuleCatalogTaxonomyCanonicalizationMigration.MigrateAsync(database).GetAwaiter().GetResult();
            PositionSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
            PositionAssignmentSeed.EnsureSeededAsync(database).GetAwaiter().GetResult();
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
