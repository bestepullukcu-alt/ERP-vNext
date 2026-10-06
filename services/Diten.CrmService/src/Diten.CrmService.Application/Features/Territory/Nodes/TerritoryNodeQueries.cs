using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Nodes;

public sealed record GetTerritoryHierarchyQuery(Guid ModelId) : IRequest<Response<TerritoryHierarchyDto>>;

/// <summary>WP-SEG-DETAILS8 — bulk reverse lookup of nodes by id (a comma-separated id list off the query string). Reads
/// <c>territory_nodes</c> only, tenant-scoped and fail-closed: unparsable/empty input yields an empty list.</summary>
public sealed record GetTerritoryNodesByIdsQuery(string? Ids) : IRequest<Response<IReadOnlyList<TerritoryNodeLookupDto>>>;
