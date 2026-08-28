using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Nodes;

public sealed record GetTerritoryHierarchyQuery(Guid ModelId) : IRequest<Response<TerritoryHierarchyDto>>;
