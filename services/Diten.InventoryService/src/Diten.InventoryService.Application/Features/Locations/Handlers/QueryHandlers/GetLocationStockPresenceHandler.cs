using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations.Queries;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Handlers.QueryHandlers;

/// <summary>
/// v1.1 <c>getLocationStockPresence</c> (K-10): booleans from the ledger's balance rows (OnHand ≠ 0, any status) on
/// the node and below — never a quantity. 404 as getLocation.
/// </summary>
public sealed class GetLocationStockPresenceHandler : IRequestHandler<GetLocationStockPresenceQuery, Response<LocationStockPresenceDto>>
{
    private readonly ILocationRepository _locations;

    public GetLocationStockPresenceHandler(ILocationRepository locations) => _locations = locations;

    public async Task<Response<LocationStockPresenceDto>> Handle(GetLocationStockPresenceQuery request, CancellationToken cancellationToken)
    {
        if (!LocationWriteSupport.TryParseId(request.LocationId, out var id)) return LocationWriteSupport.NotFound<LocationStockPresenceDto>();
        var location = await _locations.FindAsync(id, cancellationToken);
        if (location is null) return LocationWriteSupport.NotFound<LocationStockPresenceDto>();

        var presence = await _locations.StockPresenceAsync(location, cancellationToken);
        return Response<LocationStockPresenceDto>.Success(new LocationStockPresenceDto
        {
            LocationId = LocationMapping.Id(location.Id),
            HasStock = presence.HasStock,
            SubtreeHasStock = presence.SubtreeHasStock
        });
    }
}
