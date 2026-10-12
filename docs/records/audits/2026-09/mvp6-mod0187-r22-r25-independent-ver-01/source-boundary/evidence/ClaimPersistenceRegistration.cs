using Diten.SupplyChainService.Application.Features.Claims;
using Diten.SupplyChainService.Domain.Features.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Diten.SupplyChainService.Persistence.Features.Claims;

public static class ClaimPersistenceRegistration
{
    public static IServiceCollection AddClaimPersistence(this IServiceCollection services)
    {
        services.AddScoped<ClaimRequestContext>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.TryAddSingleton<IClaimCommitProbe>(provider =>
        {
            var environment = provider.GetRequiredService<IHostEnvironment>();
            if (!environment.IsEnvironment("ClaimsEvidence"))
                return new NoOpClaimCommitProbe();
            return EvidenceClaimCommitProbe.From(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<ILogger<EvidenceClaimCommitProbe>>());
        });
        services.AddScoped<ClaimOutboxStore>();
        services.AddHostedService<ClaimSchema>();
        return services;
    }
}
