using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers;

public sealed class GetProductLegalEntityScopeHistoryHandler
    : IRequestHandler<GetProductLegalEntityScopeHistoryQuery,
        Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>>
{
    private readonly IGlobalProductRepository _products;
    private readonly IProductLegalEntityScopePolicyRepository _policies;

    public GetProductLegalEntityScopeHistoryHandler(
        IGlobalProductRepository products,
        IProductLegalEntityScopePolicyRepository policies)
    {
        _products = products;
        _policies = policies;
    }

    public async Task<Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>> Handle(
        GetProductLegalEntityScopeHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (await _products.GetByIdAsync(request.GlobalProductId, cancellationToken) is null)
            return Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>.Fail(
                "GLOBAL_PRODUCT_NOT_FOUND", 404);
        var policy = await _policies.GetByGlobalProductIdAsync(request.GlobalProductId, cancellationToken);
        if (policy is null)
            return Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>.Fail(
                "PRODUCT_SCOPE_POLICY_NOT_FOUND", 404);
        return Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>.Success(
            policy.ScopePeriods.Select(CreateProductLegalEntityScopePolicyHandler.Map).ToArray());
    }
}
