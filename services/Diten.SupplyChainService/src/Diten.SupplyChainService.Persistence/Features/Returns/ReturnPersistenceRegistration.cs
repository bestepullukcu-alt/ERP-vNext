using Microsoft.Extensions.DependencyInjection;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns;
namespace Diten.SupplyChainService.Persistence.Features.Returns;
public static class ReturnPersistenceRegistration
{
    public static IServiceCollection AddReturnPersistence(this IServiceCollection services)
    {
        services.AddScoped<ReturnRequestContext>();
        services.AddScoped<IReturnRepository, ReturnRepository>();
        services.AddSingleton<IReturnCommitProbe, NoOpReturnCommitProbe>();
        services.AddScoped<ReturnOutboxStore>();
        services.AddHostedService<ReturnSchema>();
        return services;
    }
}
