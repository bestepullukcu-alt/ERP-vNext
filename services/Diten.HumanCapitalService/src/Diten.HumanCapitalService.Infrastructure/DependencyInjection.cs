using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Infrastructure.LegalEntities;
using Diten.HumanCapitalService.Infrastructure.References;
using Diten.HumanCapitalService.Infrastructure.SensitiveAccess;
using Diten.HumanCapitalService.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.HumanCapitalService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddScoped<IEmployeeProjectionReferenceValidator, DeferredEmployeeProjectionReferenceValidator>();
        services.AddScoped<IPositionAssignmentReferenceValidator, DeferredPositionAssignmentReferenceValidator>();
        // Sensitive-access scope evaluator — deferred fail-closed stub (returns
        // ContractUnavailable) until Ali wires the main HcmService feed. 2nd PENDING seam.
        services.AddScoped<ISensitiveAccessDataScopeEvaluator, DeferredSensitiveAccessDataScopeEvaluator>();

        // Legal-entity scoping (F4 pilot): selection + actable set + MDM-backed descendant resolution.
        services.Configure<MdmServiceOptions>(configuration.GetSection(MdmServiceOptions.SectionName));
        services.AddHttpClient<MdmLegalEntityHierarchyClient>();
        services.AddScoped<ILegalEntityHierarchyCache, LegalEntityHierarchyCache>();
        services.AddScoped<ILegalEntityContext, HttpLegalEntityContext>();

        return services;
    }
}
