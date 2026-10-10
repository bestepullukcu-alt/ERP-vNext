using Microsoft.Extensions.DependencyInjection;
using Diten.SupplyChainService.Domain.Features.Carriers;
using Diten.SupplyChainService.Application.Features.Carriers;
namespace Diten.SupplyChainService.Persistence.Features.Carriers;
public static class CarrierPersistenceRegistration
{
    public static IServiceCollection AddCarrierPersistence(this IServiceCollection services)
    {
        services.AddScoped<CarrierRequestContext>();
        services.AddScoped<ICarrierRepository, CarrierRepository>();
        services.AddSingleton<ICarrierCommitProbe, NoOpCarrierCommitProbe>();
        services.AddHostedService<CarrierSchema>();
        return services;
    }
}
