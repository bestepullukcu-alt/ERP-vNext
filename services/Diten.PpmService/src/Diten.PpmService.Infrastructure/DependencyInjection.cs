using Diten.PpmService.Application.Common;
using Diten.PpmService.Infrastructure.Authorization;
using Diten.PpmService.Infrastructure.Entitlements;
using Diten.Platform.Common.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.PpmService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<JwtRequestContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<JwtRequestContext>());
        services.AddScoped<ICurrentActorContext>(provider => provider.GetRequiredService<JwtRequestContext>());
        services.AddSingleton<IPermissionClaimEvaluator, SignedJwtPermissionClaimEvaluator>();
        services.AddScoped<IEffectivePermissionEvaluator, SharedPermissionClaimEvaluatorAdapter>();
        services.AddHttpClient<IPpmEntitlementDecisionClient, PpmEntitlementDecisionClient>();
        return services;
    }
}
