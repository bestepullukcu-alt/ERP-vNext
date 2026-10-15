using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

/*
 * LSKU FIX1 B4: the LSKU's adapter to the one product-identity background scope rule (ProductIdentityBackgroundScope).
 * An LSKU operation does not carry its Global Product; it is read through the parent GSKU and its revision. Both LSKU
 * recovery workers (identity, retirement request) ask HERE, so their scope rule lives in one place:
 *   no rollout / Preparation → every operation; Enforced → the operations whose product is in scope (the others are
 *   skipped, not the tenant); FailClosedSuspended or unreadable → none. A parent that cannot be read is out of scope.
 * Before FIX1 the identity worker skipped every operation of a tenant that was not in Preparation, so an approval
 * decided in an Enforced tenant never reached the LSKU, and the retirement worker had no scope rule at all.
 */
public sealed class LskuBackgroundScope(
    ProductIdentityBackgroundScope scope,
    ILskuRepository lskus,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions)
{
    private readonly Dictionary<Guid, Guid> _productByGsku = [];

    public Task<ProductIdentityBackgroundScope.TenantMode> ResolveAsync(CancellationToken cancellationToken) =>
        scope.ResolveAsync(cancellationToken);

    public async Task<bool> InScopeForGskuAsync(
        ProductIdentityBackgroundScope.TenantMode mode, Guid gskuId, CancellationToken cancellationToken)
    {
        if (mode == ProductIdentityBackgroundScope.TenantMode.Everything) return true;
        if (mode == ProductIdentityBackgroundScope.TenantMode.Nothing || gskuId == Guid.Empty) return false;
        return await scope.InScopeAsync(mode, await GlobalProductOfAsync(gskuId, cancellationToken), cancellationToken);
    }

    public async Task<bool> InScopeForLskuAsync(
        ProductIdentityBackgroundScope.TenantMode mode, Guid lskuId, CancellationToken cancellationToken)
    {
        if (mode == ProductIdentityBackgroundScope.TenantMode.Everything) return true;
        if (mode == ProductIdentityBackgroundScope.TenantMode.Nothing || lskuId == Guid.Empty) return false;
        var lsku = await lskus.GetByIdAsync(lskuId, cancellationToken);
        return lsku is not null && await InScopeForGskuAsync(mode, lsku.GskuId, cancellationToken);
    }

    private async Task<Guid> GlobalProductOfAsync(Guid gskuId, CancellationToken cancellationToken)
    {
        if (_productByGsku.TryGetValue(gskuId, out var known)) return known;
        var gsku = await gskus.GetByIdAsync(gskuId, cancellationToken);
        var revision = gsku is null ? null
            : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision?.GlobalProductId ?? Guid.Empty;
        _productByGsku[gskuId] = product;
        return product;
    }
}
