using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetFinishedGoodByIdHandler
    : IRequestHandler<GetFinishedGoodByIdQuery, Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>>
{
    private readonly IFinishedGoodRepository _finishedGoods;
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public GetFinishedGoodByIdHandler(
        IFinishedGoodRepository finishedGoods,
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _finishedGoods = finishedGoods;
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>> Handle(
        GetFinishedGoodByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await _scopeGuard.ResolveContextAsync("mdm.finished-goods.read", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
        }

        var finishedGood = await _finishedGoods.GetByIdAsync(request.Id, cancellationToken);
        if (finishedGood is null)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>.Fail("FINISHED_GOOD_NOT_FOUND", 404);
        }

        var gsku = await _gskus.GetByIdAsync(finishedGood.GskuId, cancellationToken);
        if (gsku is null)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>.Fail(
                "FINISHED_GOOD_NOT_FOUND",
                404);
        }
        var revision = await _revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null
            ? null
            : await _globalProducts.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (revision is null || product is null)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>.Fail("FINISHED_GOOD_NOT_FOUND", 404);
        }
        var decision = await _scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed)
        {
            return Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>.Fail("FINISHED_GOOD_NOT_FOUND", 404);
        }

        return Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>.Success(new(
            finishedGood.Id,
            finishedGood.CanonicalCode,
            finishedGood.GskuId,
            gsku.CanonicalCode,
            gsku.CanonicalCode,
            finishedGood.LifecycleStatus,
            finishedGood.Version,
            finishedGood.CreatedAt,
            finishedGood.UpdatedAt));
    }
}
