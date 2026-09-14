using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Infrastructure.LegalEntities;
using Diten.TalentEcosystemService.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.TalentEcosystemService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        // Legal-entity scoping (R-TEP): selection + actable set + MDM-backed descendant resolution.
        services.Configure<MdmServiceOptions>(configuration.GetSection(MdmServiceOptions.SectionName));
        services.AddHttpClient<MdmLegalEntityHierarchyClient>();
        services.AddScoped<ILegalEntityHierarchyCache, LegalEntityHierarchyCache>();
        services.AddScoped<ILegalEntityContext, HttpLegalEntityContext>();

        return services;
    }
}
