using Diten.Shared.Core;

namespace Diten.PpmService.Application.Common;


public sealed class PpmAccessAuthorizer(
    ITenantContext tenant,
    ICurrentActorContext actor,
    IPpmEntitlementDecisionClient entitlement,
    IEffectivePermissionEvaluator permissions) : IPpmAccessAuthorizer
{
    public async Task<PpmAccessDecision> AuthorizeAsync(
        string permission,
        CancellationToken cancellationToken)
    {
        if (ApplicationGuard.InvalidContext(tenant, actor))
        {
            return PpmAccessDecision.Forbidden;
        }

        try
        {
            if (!await entitlement.IsAllowedAsync(tenant.TenantId, cancellationToken))
            {
                return PpmAccessDecision.Forbidden;
            }
        }
        catch (PpmEntitlementDependencyException)
        {
            return PpmAccessDecision.DependencyUnavailable;
        }

        return await permissions.HasPermissionAsync(permission, cancellationToken)
            ? PpmAccessDecision.Allowed
            : PpmAccessDecision.Forbidden;
    }
}
