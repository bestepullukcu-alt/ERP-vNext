using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetLskusHandler
    : IRequestHandler<GetLskusQuery,
        Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>>>
{
    private readonly ILskuRepository _lskus;
    private readonly IGskuRepository _gskus;

    public GetLskusHandler(ILskuRepository lskus, IGskuRepository gskus)
    {
        _lskus = lskus;
        _gskus = gskus;
    }

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>>> Handle(
        GetLskusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var search = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : request.Search.Trim().ToUpperInvariant();
        var page = await _lskus.GetPageAsync(
            request.PageNumber,
            request.PageSize,
            search,
            cancellationToken);
        var gskus = await _gskus.GetByIdsAsync(
            page.Items.Select(x => x.GskuId).Distinct().ToArray(),
            cancellationToken);
        var gskuById = gskus.ToDictionary(x => x.Id);
        if (page.Items.Any(x => !gskuById.ContainsKey(x.GskuId)))
        {
            return Fail("LSKU_PARENT_BINDING_INVARIANT_VIOLATION");
        }

        var items = page.Items.Select(lsku =>
        {
            var gsku = gskuById[lsku.GskuId];
            return new ProductItemSkuMasterModels.LskuListItemDto(
                lsku.Id,
                lsku.CanonicalCode,
                lsku.GskuId,
                gsku.CanonicalCode,
                lsku.MarketCode,
                lsku.LifecycleStatus,
                lsku.Version,
                lsku.CreatedAt,
                lsku.UpdatedAt);
        }).ToList();
        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>>.Success(
            new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }

    private static Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>> Fail(
        string code) =>
        Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>>.Fail(code, 409);
}
