using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGlobalProductSelectorHandler : IRequestHandler<GetGlobalProductSelectorQuery, Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductSelectorDto>>>
{
    private readonly IGlobalProductRepository _repository;

    public GetGlobalProductSelectorHandler(IGlobalProductRepository repository) => _repository = repository;

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductSelectorDto>>> Handle(
        GetGlobalProductSelectorQuery request,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : GlobalProductNameRules.NormalizeDuplicateKey(request.Search);
        var page = await _repository.GetPageAsync(
            request.PageNumber,
            request.PageSize,
            search,
            lifecycleStatus: null,
            cancellationToken);
        var items = page.Items.Select(x => new ProductItemSkuMasterModels.GlobalProductSelectorDto(
            x.Id,
            x.CanonicalCode,
            x.GlobalProductName)).ToList();
        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductSelectorDto>>.Success(
            new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }
}
