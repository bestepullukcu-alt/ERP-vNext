using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetFinishedGoodsHandler
    : IRequestHandler<GetFinishedGoodsQuery,
        Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodListItemDto>>>
{
    private readonly IFinishedGoodRepository _finishedGoods;
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public GetFinishedGoodsHandler(
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

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodListItemDto>>> Handle(
        GetFinishedGoodsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var search = NormalizeCodeSearch(request.Search);
        var scope = await _scopeGuard.ResolveContextAsync("mdm.finished-goods.read", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodListItemDto>>
                .Fail(scope.FailureCode!, scope.StatusCode);
        }
        IReadOnlyList<Guid>? matchingGskuIds = null;
        if (search is not null)
        {
            matchingGskuIds = await _gskus.FindIdsByCanonicalCodeAsync(search, cancellationToken);
        }

        var page = scope.Context!.RolloutMode == ProductLegalEntityScopeRolloutMode.Preparation
            ? await _finishedGoods.GetPageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                matchingGskuIds,
                cancellationToken)
            : await _finishedGoods.GetEnforcedLegalEntityScopePageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                matchingGskuIds,
                scope.Context.EffectiveCandidateLegalEntityIds,
                scope.Context.ServerNowUtc,
                cancellationToken);
        var gskus = await _gskus.GetByIdsAsync(page.Items.Select(item => item.GskuId).Distinct().ToArray(), cancellationToken);
        var gskuById = gskus.ToDictionary(item => item.Id);
        if (page.Items.Any(item => !gskuById.ContainsKey(item.GskuId)))
        {
            return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodListItemDto>>
                .Fail("FINISHED_GOOD_BINDING_INVARIANT_VIOLATION", 500);
        }

        var items = page.Items.Select(item =>
        {
            var code = gskuById[item.GskuId].CanonicalCode;
            return new ProductItemSkuMasterModels.FinishedGoodListItemDto(
                item.Id,
                item.CanonicalCode,
                item.GskuId,
                code,
                code,
                item.LifecycleStatus,
                item.Version,
                item.CreatedAt,
                item.UpdatedAt);
        }).ToList();

        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodListItemDto>>
            .Success(new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }

    private static string? NormalizeCodeSearch(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToUpperInvariant();

}
