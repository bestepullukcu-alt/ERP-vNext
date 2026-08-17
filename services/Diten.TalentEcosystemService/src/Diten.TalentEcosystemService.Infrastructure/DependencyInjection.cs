using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.TalentEcosystemService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        return services;
    }
}
