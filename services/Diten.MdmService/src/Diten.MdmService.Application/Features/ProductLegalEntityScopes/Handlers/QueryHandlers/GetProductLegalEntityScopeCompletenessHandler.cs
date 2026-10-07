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
    private readonly TimeProvider _clock;

    public GetProductLegalEntityScopeCompletenessHandler(
        IGlobalProductRepository products,
        IProductLegalEntityScopePolicyRepository policies,
        IProductLegalEntityScopeRolloutStateRepository rollout,
        TimeProvider? clock = null)
    {
        _products = products;
        _policies = policies;
        _rollout = rollout;
        // FIX3 (12): "now" from the TimeProvider, not DateTimeOffset.UtcNow.
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<ProductLegalEntityScopeModels.CompletenessDto>> Handle(
        GetProductLegalEntityScopeCompletenessQuery request,
        CancellationToken cancellationToken)
    {
        // FIX3 (12): an unreadable rollout record answers the guard's coded 503, not a generic 500.
        var (readable, rollout) = await CommandHandlers.CreateProductLegalEntityScopePolicyHandler.TryReadRolloutAsync(
            _rollout, "completeness", cancellationToken);
        if (!readable)
            return Response<ProductLegalEntityScopeModels.CompletenessDto>.Fail(
                ProductLegalEntityScopeWriteFenceCoordinator.RolloutUnreadableCode, 503);
        var inventory = await _products.GetProductLegalEntityScopeCompletenessInventoryAsync(
            _clock.GetUtcNow(),
            ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot,
            cancellationToken);
        return Response<ProductLegalEntityScopeModels.CompletenessDto>.Success(new(
            rollout?.Mode.ToString() ?? "Uninitialized",
            inventory.EligibleGlobalProductCount,
            inventory.ConfiguredGlobalProductCount,
            inventory.MissingGlobalProductIds,
            inventory.ConfiguredGlobalProductCount == inventory.EligibleGlobalProductCount));
    }
}
