using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
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

    public GetFinishedGoodsHandler(IFinishedGoodRepository finishedGoods, IGskuRepository gskus)
    {
        _finishedGoods = finishedGoods;
        _gskus = gskus;
    }

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodListItemDto>>> Handle(
        GetFinishedGoodsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var search = NormalizeCodeSearch(request.Search);
        IReadOnlyList<Guid>? matchingGskuIds = null;
        if (search is not null)
        {
            matchingGskuIds = await _gskus.FindIdsByCanonicalCodeAsync(search, cancellationToken);
        }

        var page = await _finishedGoods.GetPageAsync(
            request.PageNumber,
            request.PageSize,
            search,
            matchingGskuIds,
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
