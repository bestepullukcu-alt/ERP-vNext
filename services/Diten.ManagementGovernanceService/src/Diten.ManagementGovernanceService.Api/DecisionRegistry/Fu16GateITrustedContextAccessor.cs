using Diten.ManagementGovernanceService.Application.DecisionRegistry.GateI;
using Diten.Platform.Common.Authorization.Fu16;

namespace Diten.ManagementGovernanceService.Api.DecisionRegistry;

public sealed class Fu16GateITrustedContextAccessor(IFu16TrustedRequestContextAccessor shared)
    : IGateITrustedContextAccessor
{
    public GateITrustedContext Current => Project(shared.Current);

    private static GateITrustedContext Project(Fu16TrustedRequestContext? context)
    {
        if (context is null
            || !string.Equals(context.ClientId, DecisionReferenceValidationProfile.ClientId, StringComparison.Ordinal)
            || !string.Equals(context.Audience, DecisionReferenceValidationProfile.Audience, StringComparison.Ordinal)
            || !string.Equals(context.ProtocolScope, DecisionReferenceValidationProfile.Scope, StringComparison.Ordinal)
            || !string.Equals(context.OperationId, DecisionReferenceValidationProfile.Operation, StringComparison.Ordinal)
            || context.Permissions.Count != 1
            || !string.Equals(context.Permissions[0], DecisionReferenceValidationProfile.Permission, StringComparison.Ordinal))
            return new(false, Guid.Empty, Guid.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        return new GateITrustedContext(
            true,
            context.TenantId,
            context.EffectiveActorId,
            context.ClientId,
            context.Audience,
            context.ProtocolScope,
            context.OperationId,
            context.Permissions[0],
            context.RequestHash);
    }
}
