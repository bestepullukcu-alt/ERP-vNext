using Diten.ManagementGovernanceService.Application.DecisionRegistry.GateI;
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
}
