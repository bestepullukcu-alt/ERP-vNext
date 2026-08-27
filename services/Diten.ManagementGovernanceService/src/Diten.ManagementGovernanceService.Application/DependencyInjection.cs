using Microsoft.Extensions.DependencyInjection;
using MediatR;

namespace Diten.ManagementGovernanceService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDwsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ExceptionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.PerformanceBehavior<,>));
        return services;
    }
}
