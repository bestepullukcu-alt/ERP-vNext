using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;

// Selected by dedicated server routes, never bound from browser query parameters.
public enum GskuMutationOptionsOperation { Edit, Correction }

public sealed record GetGskuMutationOptionsQuery(Guid GskuId, GskuMutationOptionsOperation Operation)
    : IRequest<Response<ProductItemSkuMasterModels.GskuMutationOptionsDto>>;

public sealed record GetGskuCreateOptionsQuery
    : IRequest<Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
}
