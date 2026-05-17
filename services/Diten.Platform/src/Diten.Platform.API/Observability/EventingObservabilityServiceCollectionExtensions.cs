using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.Platform.API.Observability;

public static class EventingObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddEventingObservabilityMetrics(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventingObservabilitySink, EventConsumeMetricsSink>());
        services.AddHostedService<OutboxPendingCountMetricsService>();

        var descriptor = services.LastOrDefault(service => service.ServiceType == typeof(IEventTransportPublisher));
        if (descriptor is null)
        {
            return services;
        }

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(
            typeof(IEventTransportPublisher),
            serviceProvider =>
            {
                var inner = CreateInnerPublisher(serviceProvider, descriptor);
                var logger = serviceProvider.GetRequiredService<ILogger<EventTransportPublisherMetricsDecorator>>();
                var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Diten.Platform.Common.Observability.ObservabilityOptions>>();
                return new EventTransportPublisherMetricsDecorator(inner, options, logger);
            },
            descriptor.Lifetime));

        return services;
    }

    private static IEventTransportPublisher CreateInnerPublisher(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IEventTransportPublisher instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IEventTransportPublisher)descriptor.ImplementationFactory(serviceProvider)!;
        }

        if (descriptor.ImplementationType is not null)
        {
            return (IEventTransportPublisher)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException("IEventTransportPublisher registration cannot be decorated.");
    }
}
