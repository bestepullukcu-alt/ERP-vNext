using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

/*
 * Which operations a recovery worker may carry on, in a tenant that has a legal-entity scope rollout. One rule for
 * every product-identity worker (Global Product, GSKU identity, GSKU correction, GSKU retirement request — LSKU joins
 * in its own round), each of which only says which Global Product its operation belongs to.
 *
 * A worker has no user and so no legal entities of its own: an operation is in scope when its product is classified
 * and in force — a valid policy with a scope period that is effective now. Then:
 *   - no rollout, or Preparation  → every operation (unchanged);
 *   - Enforced                    → the operations whose product is in scope; the others are skipped, NOT the tenant;
 *   - FailClosedSuspended, or anything that cannot be read → none (fail closed).
 * Before WP-MDM-GSKU-ACCEPT-01 FIX1 an Enforced tenant got no background recovery at all, so an approval decided in the
 * Task Center never reached MDM there (Global Product included).
 */
public sealed class ProductIdentityBackgroundScope(
    IProductLegalEntityScopeRolloutStateRepository rollouts,
    IProductLegalEntityScopePolicyRepository policies,
    ITenantContext tenantContext,
    TimeProvider clock)
{
    public enum TenantMode
    {
        Everything,
        PerProduct,
        Nothing
    }

    public async Task<TenantMode> ResolveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rollout = await rollouts.GetAsync(cancellationToken);
            if (rollout is null) return TenantMode.Everything;
            rollout.EnsureValid();
            if (rollout.IsDeleted || rollout.TenantId != tenantContext.TenantId) return TenantMode.Nothing;
            return rollout.Mode switch
            {
                ProductLegalEntityScopeRolloutMode.Preparation => TenantMode.Everything,
                ProductLegalEntityScopeRolloutMode.Enforced => TenantMode.PerProduct,
                _ => TenantMode.Nothing
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return TenantMode.Nothing;
        }
    }

    public async Task<bool> InScopeAsync(TenantMode mode, Guid globalProductId, CancellationToken cancellationToken)
    {
        if (mode == TenantMode.Everything) return true;
        if (mode == TenantMode.Nothing || globalProductId == Guid.Empty) return false;
        try
        {
            var policy = await policies.GetByGlobalProductIdAsync(globalProductId, cancellationToken);
            if (policy is null || policy.IsDeleted || policy.TenantId != tenantContext.TenantId) return false;
            var now = clock.GetUtcNow().ToUniversalTime();
            policy.EnsureValid(now);
            return policy.GetEffectivePeriod(now) is not null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
