using Diten.FpaService.Application.Modules.ScenarioPlanning.GateI;
using Diten.Platform.Common.Authorization.Fu16;

namespace Diten.FpaService.Api.Modules.ScenarioPlanning;

public sealed class Fu16ScenarioGateIContextAccessor(IFu16TrustedRequestContextAccessor shared)
    : IScenarioGateIContextAccessor
{
    public ScenarioGateIContext Current => Project(shared.Current);

    private static ScenarioGateIContext Project(Fu16TrustedRequestContext? context)
    {
        if(context is null
           || !string.Equals(context.ClientId,ScenarioGateIProfile.ClientId,StringComparison.Ordinal)
           || !string.Equals(context.Audience,ScenarioGateIProfile.Audience,StringComparison.Ordinal)
           || !string.Equals(context.ProtocolScope,ScenarioGateIProfile.Scope,StringComparison.Ordinal)
           || !string.Equals(context.OperationId,ScenarioGateIProfile.Operation,StringComparison.Ordinal)
           || context.Permissions.Count!=1
           || !string.Equals(context.Permissions[0],ScenarioGateIProfile.Permission,StringComparison.Ordinal))
            return new(false,Guid.Empty,Guid.Empty,"","","","","","");
        return new(true,context.TenantId,context.EffectiveActorId,context.ClientId,context.Audience,
            context.ProtocolScope,context.OperationId,context.Permissions[0],context.RequestHash);
    }
}
