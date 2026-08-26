using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddDwsPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
