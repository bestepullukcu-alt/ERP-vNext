using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Events;
using Diten.PpmService.Infrastructure.Audit;
using Diten.PpmService.Infrastructure.Authorization;
using Diten.PpmService.Infrastructure.Correlation;
using Diten.PpmService.Infrastructure.Entitlements;
using Diten.Platform.Common.Authorization;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<JwtRequestContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<JwtRequestContext>());
        services.AddScoped<ICurrentActorContext>(provider => provider.GetRequiredService<JwtRequestContext>());
        services.AddScoped<ICorrelationContext, CanonicalCorrelationContext>();
        services.AddSingleton<IPermissionClaimEvaluator, SignedJwtPermissionClaimEvaluator>();
        services.AddScoped<IEffectivePermissionEvaluator, SharedPermissionClaimEvaluatorAdapter>();
        services.AddHttpClient<IPpmEntitlementDecisionClient, PpmEntitlementDecisionClient>();

        var auditSection = configuration?.GetSection(PpmAuditProducerOptions.SectionName);
        var auditOptions = auditSection?.Get<PpmAuditProducerOptions>() ?? new PpmAuditProducerOptions();
        services.AddSingleton<IValidateOptions<PpmAuditProducerOptions>, PpmAuditProducerOptionsValidator>();
        var optionBuilder = services.AddOptions<PpmAuditProducerOptions>();
        if (auditSection is not null)
        {
            optionBuilder.Bind(auditSection);
        }
        optionBuilder.ValidateOnStart();

        services.AddSingleton<EventPayloadContractValidator>();
        services.AddScoped<ITrustedTransportMetadataProvider, PpmAuditTrustedTransportMetadataProvider>();
        services.AddScoped<IEventBus>(provider => new OutboxEventBus(
            provider.GetRequiredService<IEventOutboxWriter>(),
            provider.GetRequiredService<EventPayloadContractValidator>(),
            provider.GetRequiredService<ITrustedTransportMetadataProvider>(),
            "Diten.PpmService",
            PpmAuditIntentSubmittedV1.MaximumPayloadBytes));
        services.AddScoped<PpmAuditIntentDispatcher>();
        services.AddScoped(provider =>
        {
            var options = provider.GetRequiredService<IOptions<PpmAuditProducerOptions>>().Value;
            return new EventOutboxPublisherProcessor(
                provider.GetRequiredService<IEventOutboxStore>(),
                provider.GetRequiredService<IEventTransportPublisher>(),
                new EventOutboxPublisherOptions(
                    options.BatchSize,
                    options.MaxAttempts,
                    TimeSpan.FromSeconds(options.InitialRetryDelaySeconds),
                    TimeSpan.FromSeconds(options.MaximumRetryDelaySeconds),
                    TimeSpan.FromSeconds(options.PublishingStaleAfterSeconds)));
        });

        if (auditOptions.Enabled && auditOptions.WorkerEnabled)
        {
            services.AddMassTransit(configurator =>
            {
                configurator.UsingRabbitMq((context, rabbit) =>
                {
                    rabbit.Host(
                        auditOptions.RabbitMqHost!,
                        auditOptions.RabbitMqPort,
                        auditOptions.RabbitMqVirtualHost,
                        host =>
                        {
                            host.Username(auditOptions.RabbitMqUsername!);
                            host.Password(auditOptions.RabbitMqPassword!);
                        });
                    rabbit.ConfigureEndpoints(context);
                });
            });
            services.AddScoped<IEventTransportPublisher, MassTransitPpmEventTransportPublisher>();
        }
        else
        {
            services.AddScoped<IEventTransportPublisher, DisabledPpmEventTransportPublisher>();
        }

        services.AddHostedService<PpmAuditProducerWorker>();
        services.AddHostedService<PpmEventOutboxPublisherWorker>();
        return services;
    }
}

internal sealed class DisabledPpmEventTransportPublisher : IEventTransportPublisher
{
    public Task PublishAsync(
        EventTransportMessage message,
        CancellationToken cancellationToken = default) =>
        throw new EventTransportTerminalException(new EventOutboxTerminalFailure(
            EventOutboxTerminalFailureKind.Security,
            "ppm.audit-producer.disabled"));
}
