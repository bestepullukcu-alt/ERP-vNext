using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;

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
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ProductLegalEntityScopeWriteFenceBehavior<,>));
        // Registered last => innermost: only wraps real handler executions, so it audits the handler's actual outcome
        // (validation/exception failures short-circuit before reaching it).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.AuditForwardingBehavior<,>));
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<ProductAbbreviationAuthorization>();
        services.AddScoped<ProductAbbreviationWorkflow>();
        services.AddScoped<ProductLegalEntityScopeWriteFenceCoordinator>();

        return services;
    }
}
