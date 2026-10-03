using Diten.SupplyChainService.Application.Features.Claims;
using Diten.SupplyChainService.Domain.Features.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Diten.SupplyChainService.Persistence.Features.Claims;

public static class ClaimPersistenceRegistration
{
    public static IServiceCollection AddClaimPersistence(this IServiceCollection services)
    {
        services.AddScoped<ClaimRequestContext>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.TryAddSingleton<IClaimCommitProbe, NoOpClaimCommitProbe>();
        services.AddScoped<ClaimOutboxStore>();
        services.AddHostedService<ClaimSchema>();
        return services;
    }
}
