using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations.Queries;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Handlers.QueryHandlers;

/// <summary>Frozen v1 <c>getLocationPath</c>: root Site → … → the node, from its materialized path. 404 as getLocation.</summary>
public sealed class GetLocationPathHandler : IRequestHandler<GetLocationPathQuery, Response<LocationPathDto>>
{
    private readonly ILocationRepository _locations;

    public GetLocationPathHandler(ILocationRepository locations) => _locations = locations;

    public async Task<Response<LocationPathDto>> Handle(GetLocationPathQuery request, CancellationToken cancellationToken)
    {
        if (!LocationWriteSupport.TryParseId(request.LocationId, out var id)) return LocationWriteSupport.NotFound<LocationPathDto>();
        var location = await _locations.FindAsync(id, cancellationToken);
        if (location is null) return LocationWriteSupport.NotFound<LocationPathDto>();

        var ancestors = (await _locations.FindManyAsync(location.AncestorIds, cancellationToken)).ToDictionary(a => a.Id);
        var path = location.AncestorIds.Where(ancestors.ContainsKey).Select(a => ancestors[a]).Append(location)
            .Select(l => new LocationPathEntryDto { Type = l.Type.ToString(), Id = LocationMapping.Id(l.Id), Code = l.Code })
            .ToList();
        return Response<LocationPathDto>.Success(new LocationPathDto { LocationId = LocationMapping.Id(location.Id), Path = path });
    }
}
