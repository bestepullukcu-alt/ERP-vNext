using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Models;

public sealed record GetTerritoryModelListQuery(string? Search, string? Status, int Page = 1, int PageSize = 25)
    : IRequest<Response<TerritoryModelListDto>>;

public sealed record GetTerritoryModelByIdQuery(Guid Id) : IRequest<Response<TerritoryModelDetailDto>>;
