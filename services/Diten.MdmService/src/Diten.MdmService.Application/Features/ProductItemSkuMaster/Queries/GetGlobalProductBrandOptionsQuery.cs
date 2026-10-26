using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;

/// <summary>
/// MOD-0290-FU05 (T-1) — the brands a Global Product can be linked to now: this tenant's, active and inside their validity
/// window (<c>Brand.IsLinkableAt</c>), paged, by code. Read with the Global Product write keys, not <c>mdm.brands.read</c>.
/// </summary>
public sealed record GetGlobalProductBrandOptionsQuery : IRequest<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductBrandOptionDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
}
