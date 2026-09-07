using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers;

public sealed class GetEffectiveProductLegalEntityScopeHandler
    : IRequestHandler<GetEffectiveProductLegalEntityScopeQuery,
        Response<ProductLegalEntityScopeModels.EffectiveFactsDto>>
{
    private readonly IGlobalProductRepository _products;
    private readonly IProductLegalEntityScopePolicyRepository _policies;
    private readonly IProductLegalEntityScopeRolloutStateRepository _rollout;

    public GetEffectiveProductLegalEntityScopeHandler(
        IGlobalProductRepository products,
        IProductLegalEntityScopePolicyRepository policies,
        IProductLegalEntityScopeRolloutStateRepository rollout)
    {
        _products = products;
        _policies = policies;
        _rollout = rollout;
    }

    public async Task<Response<ProductLegalEntityScopeModels.EffectiveFactsDto>> Handle(
        GetEffectiveProductLegalEntityScopeQuery request,
        CancellationToken cancellationToken)
    {
        if (await _products.GetByIdAsync(request.GlobalProductId, cancellationToken) is null)
            return Response<ProductLegalEntityScopeModels.EffectiveFactsDto>.Fail("GLOBAL_PRODUCT_NOT_FOUND", 404);
        var rollout = await _rollout.GetAsync(cancellationToken);
        var policy = await _policies.GetByGlobalProductIdAsync(request.GlobalProductId, cancellationToken);
        var current = policy?.GetEffectivePeriod(DateTimeOffset.UtcNow);
        return Response<ProductLegalEntityScopeModels.EffectiveFactsDto>.Success(new(
            request.GlobalProductId,
            rollout?.Mode.ToString() ?? "Uninitialized",
            policy is not null,
            policy?.Version,
            current is null ? null : CreateProductLegalEntityScopePolicyHandler.Map(current)));
    }
}
