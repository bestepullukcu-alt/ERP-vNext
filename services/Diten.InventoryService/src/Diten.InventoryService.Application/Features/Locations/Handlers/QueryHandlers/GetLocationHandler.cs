using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations.Queries;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Handlers.QueryHandlers;

/// <summary>
/// Frozen v1 <c>getLocation</c>: the <c>LocationRef</c> of a location of the caller's company, any status (K-13 —
/// consumers read <c>status</c>). Unknown, malformed or another company's id → 404 <c>UNKNOWN_LOCATION</c>. The
/// owner's version travels as the <c>ETag</c> header, never in the frozen body.
/// </summary>
public sealed class GetLocationHandler : IRequestHandler<GetLocationQuery, Response<LocationRefDto>>
{
    private readonly ILocationRepository _locations;

    public GetLocationHandler(ILocationRepository locations) => _locations = locations;

    public async Task<Response<LocationRefDto>> Handle(GetLocationQuery request, CancellationToken cancellationToken)
    {
        if (!LocationWriteSupport.TryParseId(request.LocationId, out var id)) return LocationWriteSupport.NotFound<LocationRefDto>();
        var location = await _locations.FindAsync(id, cancellationToken);
        return location is null
            ? LocationWriteSupport.NotFound<LocationRefDto>()
            : Response<LocationRefDto>.Success(LocationMapping.ToRef(location, withETag: true));
    }
}
