using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGlobalProductsHandler : IRequestHandler<GetGlobalProductsQuery, Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductListItemDto>>>
{
    private readonly IGlobalProductRepository _repository;

    public GetGlobalProductsHandler(IGlobalProductRepository repository) => _repository = repository;

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductListItemDto>>> Handle(
        GetGlobalProductsQuery request,
        CancellationToken cancellationToken)
    {
        var search = NormalizeSearch(request.Search);
        var page = await _repository.GetPageAsync(
            request.PageNumber,
            request.PageSize,
            search,
            request.LifecycleStatus,
            cancellationToken);
        var items = page.Items.Select(x => new ProductItemSkuMasterModels.GlobalProductListItemDto(
            x.Id,
            x.CanonicalCode,
            x.GlobalProductName,
            x.LifecycleStatus)).ToList();

        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductListItemDto>>.Success(
            new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }

    private static string? NormalizeSearch(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : GlobalProductNameRules.NormalizeDuplicateKey(search);

}
