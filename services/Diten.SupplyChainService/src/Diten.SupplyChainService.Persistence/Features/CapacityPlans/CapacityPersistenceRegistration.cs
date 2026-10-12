using Microsoft.Extensions.DependencyInjection;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
using Diten.SupplyChainService.Application.Features.CapacityPlans;
namespace Diten.SupplyChainService.Persistence.Features.CapacityPlans;
public static class CapacityPersistenceRegistration
{
    // Integration owner must call this after feature types compile. This file does not modify shared DI.
    public static IServiceCollection AddCapacityPersistence(this IServiceCollection services)
    {
        services.AddScoped<CapacityRequestContext>();
        services.AddScoped<ICapacityRepository,CapacityRepository>();
        services.AddSingleton<ICapacityLeaseStore,CapacityLeaseStore>();
        services.AddHostedService<CapacitySchema>();
        return services;
    }
}
