using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers;

public sealed class GetProductLegalEntityScopePolicyHandler
    : IRequestHandler<GetProductLegalEntityScopePolicyQuery, Response<ProductLegalEntityScopeModels.PolicyDto>>
{
    private readonly IGlobalProductRepository _products;
    private readonly IProductLegalEntityScopePolicyRepository _policies;

    public GetProductLegalEntityScopePolicyHandler(
        IGlobalProductRepository products,
        IProductLegalEntityScopePolicyRepository policies)
    {
        _products = products;
        _policies = policies;
    }

    public async Task<Response<ProductLegalEntityScopeModels.PolicyDto>> Handle(
        GetProductLegalEntityScopePolicyQuery request,
        CancellationToken cancellationToken)
    {
        if (await _products.GetByIdAsync(request.GlobalProductId, cancellationToken) is null)
            return Response<ProductLegalEntityScopeModels.PolicyDto>.Fail("GLOBAL_PRODUCT_NOT_FOUND", 404);
        var policy = await _policies.GetByGlobalProductIdAsync(request.GlobalProductId, cancellationToken);
        return policy is null
            ? Response<ProductLegalEntityScopeModels.PolicyDto>.Fail("PRODUCT_SCOPE_POLICY_NOT_FOUND", 404)
            : Response<ProductLegalEntityScopeModels.PolicyDto>.Success(
                CreateProductLegalEntityScopePolicyHandler.Map(policy));
    }
}
