using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Infrastructure.References;
using Diten.HumanCapitalService.Infrastructure.SensitiveAccess;
using Diten.HumanCapitalService.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.HumanCapitalService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddScoped<IEmployeeProjectionReferenceValidator, DeferredEmployeeProjectionReferenceValidator>();
        services.AddScoped<IPositionAssignmentReferenceValidator, DeferredPositionAssignmentReferenceValidator>();
        services.AddScoped<ISensitiveAccessDataScopeEvaluator, DeferredSensitiveAccessDataScopeEvaluator>();

        return services;
    }
}
