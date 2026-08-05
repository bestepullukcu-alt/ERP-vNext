using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDecisionRegistryInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
