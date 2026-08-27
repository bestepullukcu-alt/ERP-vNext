using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDwsInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // Production adapters are intentionally absent. Local-test composition is explicit and separate.
        return services;
    }
}
