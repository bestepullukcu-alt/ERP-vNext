using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetFinishedGoodGskuSelectorHandler
    : IRequestHandler<GetFinishedGoodGskuSelectorQuery,
        Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodGskuSelectorDto>>>
{
    private readonly IGskuRepository _gskus;

    public GetFinishedGoodGskuSelectorHandler(IGskuRepository gskus) => _gskus = gskus;

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodGskuSelectorDto>>> Handle(
        GetFinishedGoodGskuSelectorQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var search = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : request.Search.Trim().ToUpperInvariant();
        var page = await _gskus.GetReferenceablePageAsync(
            request.PageNumber,
            request.PageSize,
            search,
            cancellationToken);
        var items = page.Items.Select(gsku => new ProductItemSkuMasterModels.FinishedGoodGskuSelectorDto(
            gsku.Id,
            gsku.CanonicalCode,
            gsku.CanonicalCode)).ToList();
        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodGskuSelectorDto>>
            .Success(new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }
}
