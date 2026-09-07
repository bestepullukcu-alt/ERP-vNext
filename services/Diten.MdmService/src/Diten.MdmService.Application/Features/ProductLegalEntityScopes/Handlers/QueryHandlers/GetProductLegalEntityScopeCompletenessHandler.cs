using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers;

public sealed class GetProductLegalEntityScopeCompletenessHandler
    : IRequestHandler<GetProductLegalEntityScopeCompletenessQuery,
        Response<ProductLegalEntityScopeModels.CompletenessDto>>
{
    private readonly IGlobalProductRepository _products;
    private readonly IProductLegalEntityScopePolicyRepository _policies;
    private readonly IProductLegalEntityScopeRolloutStateRepository _rollout;

    public GetProductLegalEntityScopeCompletenessHandler(
        IGlobalProductRepository products,
        IProductLegalEntityScopePolicyRepository policies,
        IProductLegalEntityScopeRolloutStateRepository rollout)
    {
        _products = products;
        _policies = policies;
        _rollout = rollout;
    }

    public async Task<Response<ProductLegalEntityScopeModels.CompletenessDto>> Handle(
        GetProductLegalEntityScopeCompletenessQuery request,
        CancellationToken cancellationToken)
    {
        var inventory = await _products.GetProductLegalEntityScopeCompletenessInventoryAsync(
            DateTimeOffset.UtcNow,
            ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot,
            cancellationToken);
        var rollout = await _rollout.GetAsync(cancellationToken);
        return Response<ProductLegalEntityScopeModels.CompletenessDto>.Success(new(
            rollout?.Mode.ToString() ?? "Uninitialized",
            inventory.EligibleGlobalProductCount,
            inventory.ConfiguredGlobalProductCount,
            inventory.MissingGlobalProductIds,
            inventory.ConfiguredGlobalProductCount == inventory.EligibleGlobalProductCount));
    }
}
