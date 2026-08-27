using Diten.FpaService.Application.Modules.Budgeting.GateI;
using Diten.Platform.Common.Authorization.Fu16;

namespace Diten.FpaService.Api.Modules.Budgeting;

public sealed class Fu16BudgetGateITrustedContextAccessor(IFu16TrustedRequestContextAccessor shared)
    : IBudgetGateITrustedContextAccessor
{
    public BudgetGateITrustedContext Current => Project(shared.Current);

    private static BudgetGateITrustedContext Project(Fu16TrustedRequestContext? context)
    {
        if (context is null
            || !string.Equals(context.ClientId, BudgetGateIProfile.ClientId, StringComparison.Ordinal)
            || !string.Equals(context.Audience, BudgetGateIProfile.Audience, StringComparison.Ordinal)
            || !string.Equals(context.ProtocolScope, BudgetGateIProfile.Scope, StringComparison.Ordinal)
            || !string.Equals(context.OperationId, BudgetGateIProfile.Operation, StringComparison.Ordinal)
            || context.Permissions.Count != 1
            || !string.Equals(context.Permissions[0], BudgetGateIProfile.Permission, StringComparison.Ordinal))
            return new(false, Guid.Empty, Guid.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        return new(true, context.TenantId, context.EffectiveActorId, context.ClientId, context.Audience,
            context.ProtocolScope, context.OperationId, context.Permissions[0], context.RequestHash);
    }
}
