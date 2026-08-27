using System.Reflection;
using Diten.ManagementGovernanceService.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Application;
public static class DependencyInjection
{
    public static IReadOnlyList<string> ProcessModelingRegistrations { get; } = ["MOD-0355:Modules/ProcessModeling:Application"];
    public static IServiceCollection AddManagementGovernanceApplication(this IServiceCollection services)
    {
        services.AddMediatR(x=>x.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddTransient(typeof(IPipelineBehavior<,>),typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>),typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>),typeof(ExceptionHandlingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>),typeof(PerformanceBehavior<,>));
        return services;
    }
}
