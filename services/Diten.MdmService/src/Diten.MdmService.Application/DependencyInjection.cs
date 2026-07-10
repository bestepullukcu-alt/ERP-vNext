using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.MdmService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ExceptionHandlingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.PerformanceBehavior<,>));
        // Registered last => innermost: only wraps real handler executions, so it audits the handler's actual outcome
        // (validation/exception failures short-circuit before reaching it).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.AuditForwardingBehavior<,>));
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
