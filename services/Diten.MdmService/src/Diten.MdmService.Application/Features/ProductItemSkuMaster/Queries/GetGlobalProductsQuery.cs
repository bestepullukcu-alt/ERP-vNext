using Diten.MdmService.Domain.Enums;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;

public sealed record GetGlobalProductsQuery : IRequest<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductListItemDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public ProductIdentityLifecycleStatus? LifecycleStatus { get; init; }
}
