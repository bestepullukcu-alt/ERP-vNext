using Diten.DecisionIntelligenceService.Application.Modules.OutcomeTracking.GateI;
using Diten.Platform.Common.Authorization.Fu16;

namespace Diten.DecisionIntelligenceService.Api.Modules.OutcomeTracking;

public sealed class Fu16OutcomeGateIContextAccessor(IFu16TrustedRequestContextAccessor shared):IOutcomeGateIContextAccessor
{
 public OutcomeGateIContext Current=>Project(shared.Current);
 private static OutcomeGateIContext Project(Fu16TrustedRequestContext? context)
 {
  if(context is null||!string.Equals(context.ClientId,OutcomeGateIProfile.ClientId,StringComparison.Ordinal)||!string.Equals(context.Audience,OutcomeGateIProfile.Audience,StringComparison.Ordinal)||!string.Equals(context.ProtocolScope,OutcomeGateIProfile.Scope,StringComparison.Ordinal)||!string.Equals(context.OperationId,OutcomeGateIProfile.Operation,StringComparison.Ordinal)||context.Permissions.Count!=1||!string.Equals(context.Permissions[0],OutcomeGateIProfile.Permission,StringComparison.Ordinal))return new(false,Guid.Empty,Guid.Empty,"","","","","","");
  return new(true,context.TenantId,context.EffectiveActorId,context.ClientId,context.Audience,context.ProtocolScope,context.OperationId,context.Permissions[0],context.RequestHash);
 }
}
