using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDwsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
