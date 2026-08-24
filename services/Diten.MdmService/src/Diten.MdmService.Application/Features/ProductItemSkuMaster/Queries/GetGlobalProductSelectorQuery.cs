using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;

public sealed record GetGlobalProductSelectorQuery : IRequest<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductSelectorDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
}
