using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX1 S1 — "may this caller see this product?" for the scope screen's own reads (policy,
/// history, effective facts, create options). A permission to manage scopes is not row access (Pack:1004-1007): in an
/// Enforced tenant the caller sees only the products of the legal entities in which they hold the endpoint's permission
/// — the same rule (and the same guard) as the Global Product reads. A product they may not see answers exactly like a
/// product that does not exist (404 GLOBAL_PRODUCT_NOT_FOUND), so neither its scope nor its existence leaks. In
/// Preparation every product of the tenant is visible, as today.
/// </summary>
public sealed class ProductLegalEntityScopeProductAccess(
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository policies,
    ProductLegalEntityScopeCandidateFacade candidates,
    ITenantContext tenantContext,
    IGlobalProductRepository products,
    TimeProvider clock)
{
    public const string ReadPermission = "mdm.product-legal-entity-scopes.read";
    public const string ConfigurePermission = "mdm.product-legal-entity-scopes.configure";

    public async Task<ProductLegalEntityScopeProductAccessResult> ResolveAsync(
        Guid globalProductId,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var guard = new ProductLegalEntityScopeConsumerGuard(rolloutStates, policies, candidates, tenantContext, clock);
        var scope = await guard.ResolveContextAsync(permissionKey, cancellationToken);
        if (!scope.IsSuccessful)
            return new(false, scope.StatusCode, scope.FailureCode, null, null);
        var product = await products.GetByIdAsync(globalProductId, cancellationToken);
        if (product is null)
            return NotFound;
        var decision = await guard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        return decision.Allowed ? new(true, 200, null, product, scope.Context) : NotFound;
    }

    private static readonly ProductLegalEntityScopeProductAccessResult NotFound =
        new(false, 404, "GLOBAL_PRODUCT_NOT_FOUND", null, null);
}

public sealed record ProductLegalEntityScopeProductAccessResult(
    bool Allowed,
    int StatusCode,
    string? FailureCode,
    GlobalProduct? Product,
    ProductLegalEntityScopeConsumerContext? Context);
