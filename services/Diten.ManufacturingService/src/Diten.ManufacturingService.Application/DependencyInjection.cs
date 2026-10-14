using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManufacturingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        // 4 zorunlu pipeline behavior (pipeline-behaviors.md).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ExceptionHandlingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.PerformanceBehavior<,>));

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: false);

        services.AddScoped<Common.CorrelationContext>();
        services.AddScoped<Common.ICorrelationContext>(sp => sp.GetRequiredService<Common.CorrelationContext>());
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
