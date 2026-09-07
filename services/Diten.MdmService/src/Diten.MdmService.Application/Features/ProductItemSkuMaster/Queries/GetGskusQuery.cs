using Diten.Shared.Core;
using Diten.MdmService.Domain.Enums;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;

public sealed record GetGskusQuery
    : IRequest<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GskuListItemDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public ProductIdentityLifecycleStatus? LifecycleStatus { get; init; }
}
