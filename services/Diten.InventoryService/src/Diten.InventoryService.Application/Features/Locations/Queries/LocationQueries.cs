using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Queries;

/// <summary>GET /api/locations/{id} (frozen v1 <c>getLocation</c>).</summary>
public sealed record GetLocationQuery(string? LocationId) : IRequest<Response<LocationRefDto>>;

/// <summary>GET /api/locations?type&amp;parentId&amp;legalEntityId (frozen v1 <c>listLocations</c>; every status, unpaged).</summary>
public sealed record ListLocationsQuery(string? Type, string? ParentId, string? LegalEntityId) : IRequest<Response<LocationListDto>>;

/// <summary>GET /api/locations/{id}/path (frozen v1 <c>getLocationPath</c>).</summary>
public sealed record GetLocationPathQuery(string? LocationId) : IRequest<Response<LocationPathDto>>;

/// <summary>POST /api/locations/validate (frozen v1 <c>validateLocations</c>; a read, despite the verb).</summary>
public sealed record ValidateLocationsQuery(IReadOnlyList<string> LocationIds) : IRequest<Response<LocationValidationResultDto>>;

/// <summary>GET /api/locations/search (v1.1 <c>searchLocations</c>). <c>Types</c> / <c>Statuses</c> repeat.</summary>
public sealed record SearchLocationsQuery(
    string? Page,
    string? PageSize,
    string? Search,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Statuses,
    string? IsStockable,
    string? ParentId,
    string? OrderBy,
    string? OrderDir) : IRequest<Response<LocationSearchPageDto>>;

/// <summary>GET /api/locations/{id}/history (v1.1 <c>getLocationHistory</c>).</summary>
public sealed record GetLocationHistoryQuery(string? LocationId, string? Page, string? PageSize) : IRequest<Response<LocationHistoryPageDto>>;

/// <summary>GET /api/locations/{id}/stock-presence (v1.1 <c>getLocationStockPresence</c>).</summary>
public sealed record GetLocationStockPresenceQuery(string? LocationId) : IRequest<Response<LocationStockPresenceDto>>;
