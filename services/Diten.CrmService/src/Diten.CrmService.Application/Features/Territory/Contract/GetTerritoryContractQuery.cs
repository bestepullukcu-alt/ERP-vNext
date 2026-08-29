using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Contract;

public sealed record GetTerritoryContractQuery : IRequest<Response<TerritoryContractDto>>;
