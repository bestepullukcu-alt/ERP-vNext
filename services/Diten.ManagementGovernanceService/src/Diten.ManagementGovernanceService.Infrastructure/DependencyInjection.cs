using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDwsInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
