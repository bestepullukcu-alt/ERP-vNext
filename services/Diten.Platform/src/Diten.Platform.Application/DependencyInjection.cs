using Diten.BuildingBlocks.Eventing;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Eventing;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tenants.Notifications;
using Diten.Platform.Application.Security;
using Diten.Platform.Application.Features.InterfaceRegistry.Auditing;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Services;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Contracts.Events;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Platform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ExceptionBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuditBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(_ => { }, assembly);
        services.AddSingleton<IInterfaceRegistryAuditSink, NullInterfaceRegistryAuditSink>();
        services.AddSingleton<EventPayloadContractValidator>();
        services.AddScoped<IEventBus, EventBus>();
        services.AddScoped<ConsumedEventStore>();
        services.AddScoped<IPlatformCatalogContract, PlatformCatalogContract>();
        services.AddSingleton<ITemporaryAccessProvider, NoOpTemporaryAccessProvider>();
        services.AddScoped<IDataScopeResolver, NoOpDataScopeResolver>();
        services.AddScoped<ITenantModuleAccessService, TenantModuleAccessService>();
        services.AddScoped<IActorSafetyGuard, ActorSafetyGuard>();
        services.AddScoped<IQuotaService, QuotaService>();
        services.AddScoped<IPlatformLookupProvider, PlatformLookupProvider>();
        services.AddScoped<ITenantMessagingSettingsResolver, TenantMessagingSettingsResolver>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddScoped<TenantCreatedV1NotificationMapper>();
        services.AddScoped<TenantSuspendedV1NotificationMapper>();
        services.AddScoped<TenantReactivatedV1NotificationMapper>();
        services.AddScoped<INotificationEventMapper<TenantCreatedV1>>(sp => sp.GetRequiredService<TenantCreatedV1NotificationMapper>());
        services.AddScoped<INotificationEventMapper<TenantSuspendedV1>>(sp => sp.GetRequiredService<TenantSuspendedV1NotificationMapper>());
        services.AddScoped<INotificationEventMapper<TenantReactivatedV1>>(sp => sp.GetRequiredService<TenantReactivatedV1NotificationMapper>());
        services.AddSingleton<AuditBehaviorOptions>();
        services.AddSingleton<ISensitiveFieldRedactionRegistry, SensitiveFieldRedactionRegistry>();
        services.AddSingleton<ISensitiveFieldRedactor, SensitiveFieldRedactor>();
        services.AddSingleton<IAuditIdempotencyKeyBuilder, AuditIdempotencyKeyBuilder>();
        services.AddSingleton<IAuditRecursionGuard, AuditRecursionGuard>();
        services.AddScoped<IAuditRetentionPolicyResolver, AuditRetentionPolicyResolver>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditMetaAuditWriter, AuditMetaAuditWriter>();
        services.AddScoped<IJobExecutionLogWriter, JobExecutionLogWriter>();
        services.AddScoped<SchedulerSmokeTestJob>();
        services.AddScoped<DeferredPlatformJobHandler>();
        services.AddScoped<EmailDispatchJob>();
        services.AddScoped<EmailDispatchSweepJob>();
        services.AddSingleton<IRecurringJobRegistrar, PlatformRecurringJobRegistrar>();

        return services;
    }
}
