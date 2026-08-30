using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGlobalProductByIdHandler : IRequestHandler<GetGlobalProductByIdQuery, Response<ProductItemSkuMasterModels.GlobalProductDetailDto>>
{
    private readonly IGlobalProductRepository _repository;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public GetGlobalProductByIdHandler(
        IGlobalProductRepository repository,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates,
            policies,
            candidates,
            tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.GlobalProductDetailDto>> Handle(
        GetGlobalProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await _scopeGuard.ResolveContextAsync(
            ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission,
            cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.GlobalProductDetailDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
        }
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
        {
            return Response<ProductItemSkuMasterModels.GlobalProductDetailDto>.Fail(
                "GLOBAL_PRODUCT_NOT_FOUND",
                404);
        }
        var decision = await _scopeGuard.EvaluateAsync(
            scope.Context!,
            product.Id,
            cancellationToken);
        if (!decision.Allowed)
        {
            return Response<ProductItemSkuMasterModels.GlobalProductDetailDto>.Fail(
                "GLOBAL_PRODUCT_NOT_FOUND",
                404);
        }

        return Response<ProductItemSkuMasterModels.GlobalProductDetailDto>.Success(new(
            product.Id,
            product.CanonicalCode,
            product.GlobalProductName,
            product.LifecycleStatus,
            product.Version,
            product.CreatedAt,
            product.UpdatedAt));
    }
}
