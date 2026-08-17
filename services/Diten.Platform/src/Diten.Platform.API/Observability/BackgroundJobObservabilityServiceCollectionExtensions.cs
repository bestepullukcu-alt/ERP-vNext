using Diten.Platform.Application.BackgroundJobs;

namespace Diten.Platform.API.Observability;

public static class BackgroundJobObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundJobObservabilityMetrics(this IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(service => service.ServiceType == typeof(IJobExecutionLogWriter));
        if (descriptor is null)
        {
            return services;
        }

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(
            typeof(IJobExecutionLogWriter),
            serviceProvider =>
            {
                var inner = CreateInnerLogWriter(serviceProvider, descriptor);
                var logger = serviceProvider.GetRequiredService<ILogger<BackgroundJobExecutionLogMetricsDecorator>>();
                var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Diten.Platform.Common.Observability.ObservabilityOptions>>();
                return new BackgroundJobExecutionLogMetricsDecorator(inner, options, logger);
            },
            descriptor.Lifetime));

        return services;
    }

    private static IJobExecutionLogWriter CreateInnerLogWriter(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IJobExecutionLogWriter instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IJobExecutionLogWriter)descriptor.ImplementationFactory(serviceProvider)!;
        }

        if (descriptor.ImplementationType is not null)
        {
            return (IJobExecutionLogWriter)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException("IJobExecutionLogWriter registration cannot be decorated.");
    }
}
