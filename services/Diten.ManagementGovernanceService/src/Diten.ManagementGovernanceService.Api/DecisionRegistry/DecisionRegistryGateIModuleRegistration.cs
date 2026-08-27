using Diten.ManagementGovernanceService.Application.DecisionRegistry.GateI;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Api.DecisionRegistry;

public static class DecisionRegistryGateIModuleRegistration
{
    public static IServiceCollection AddDecisionRegistryGateILocalProvider(this IServiceCollection services) =>
        services
            .AddSingleton(DecisionReferenceProviderOptions.Disabled)
            .AddSingleton<IDecisionReferenceSnapshotReader, UnavailableDecisionReferenceSnapshotReader>()
            .AddSingleton<IGateITrustedContextAccessor, UnavailableGateITrustedContextAccessor>()
            .AddSingleton<DecisionReferenceValidationProvider>();

    public static IServiceCollection AddDecisionRegistryGateIFu16Evidence(
        this IServiceCollection services,
        Action<Fu16AuthenticationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddAuthentication().AddFu16DelegatedActor(configure);
        services.AddScoped<IGateITrustedContextAccessor, Fu16GateITrustedContextAccessor>();
        return services;
    }
}
